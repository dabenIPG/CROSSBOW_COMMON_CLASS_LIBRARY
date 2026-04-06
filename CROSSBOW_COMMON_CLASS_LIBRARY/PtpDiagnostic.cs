// PtpDiagnostic.cs  —  IEEE 1588 PTP one-shot diagnostic query
//
// Mirrors NtpDiagnostic.cs exactly in usage:
//   var result = await PtpDiagnostic.QueryAsync("192.168.1.30");
//   textBox.AppendText(result.Summary());
//
// Profile matches ptpClient.hpp (CROSSBOW firmware):
//   NovAtel GNSS grandmaster at 192.168.1.30
//   Multicast 224.0.1.129, domain 0, ports 319 (event) / 320 (general)
//   2-step clock — SYNC + FOLLOW_UP, then DELAY_REQ → DELAY_RESP
//   PTPTIMESCALE UTC_TIME assumed (PTP_GPS_UTC_OFFSET_SEC = 0)
//   DELAY_REQ sent unicast to grandmaster (matches firmware behaviour)
//
// Exchange sequence:
//   [optional] ANNOUNCE → grandmaster identity + clock quality
//   SYNC         (port 319 multicast) → record t2 (local arrival)
//   FOLLOW_UP    (port 320 multicast) → extract t1 (grandmaster origin)
//   DELAY_REQ TX (port 319 unicast)   → record t3 (local departure)
//   DELAY_RESP   (port 320 multicast) → extract t4 (grandmaster arrival)
//
//   offset    = ((t2-t1) + (t3-t4)) / 2
//   roundtrip = (t4-t1) - (t3-t2)
//
// ⚠️  Windows firewall note: ports 319 and 320 may need inbound UDP rules.
//     Run as administrator or add rules for the ENG GUI executable.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CROSSBOW
{
    public class PtpDiagnostic
    {
        // ── Result metadata ───────────────────────────────────────────────────
        public string ServerIP  { get; private set; }
        public bool   Success   { get; private set; }
        public string Error     { get; private set; }

        // ── ANNOUNCE fields (grandmaster identity) ────────────────────────────
        public bool   AnnounceReceived            { get; private set; }
        public byte[] GrandmasterIdentity         { get; private set; } = new byte[8];
        public byte   GrandmasterPriority1        { get; private set; }
        public byte   GrandmasterPriority2        { get; private set; }
        public byte   ClockClass                  { get; private set; }
        public byte   ClockAccuracy               { get; private set; }
        public ushort OffsetScaledLogVariance      { get; private set; }
        public short  CurrentUtcOffset            { get; private set; }   // seconds
        public byte   TimeSource                  { get; private set; }
        public ushort StepsRemoved                { get; private set; }
        public int    Domain                      { get; private set; }

        // ── Four-timestamp exchange ───────────────────────────────────────────
        public DateTime T1 { get; private set; }   // SYNC origin at master  (FOLLOW_UP body)
        public DateTime T2 { get; private set; }   // SYNC arrival at us     (local snapshot)
        public DateTime T3 { get; private set; }   // DELAY_REQ departure    (local snapshot)
        public DateTime T4 { get; private set; }   // DELAY_REQ arrival at master (DELAY_RESP body)

        public double RoundTripMs { get; private set; }
        public double OffsetMs    { get; private set; }

        // ── PTP constants ─────────────────────────────────────────────────────
        private const int   EVENT_PORT   = 319;
        private const int   GENERAL_PORT = 320;
        private const byte  MSG_SYNC        = 0x0;
        private const byte  MSG_DELAY_REQ   = 0x1;
        private const byte  MSG_FOLLOW_UP   = 0x8;
        private const byte  MSG_DELAY_RESP  = 0x9;
        private const byte  MSG_ANNOUNCE    = 0xB;
        private const ushort FLAG_TWO_STEP  = 0x0200;

        private static readonly IPAddress MULTICAST_IP = IPAddress.Parse("224.0.1.129");
        private static readonly DateTime  PTP_EPOCH    = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Diagnostic tool clock identity — arbitrary EUI-64, distinct from firmware
        private static readonly byte[] CLIENT_CLOCK_ID = { 0xDE, 0xAD, 0x00, 0xFF, 0xFE, 0x00, 0xEE, 0x01 };
        private const ushort CLIENT_PORT_NUM = 1;
        private static int _seqCounter = 0;

        // ── State machine fields (used during QueryAsync) ─────────────────────
        private ushort _syncSeq     = 0;
        private ushort _delayReqSeq = 0;

        // ── Static factory ────────────────────────────────────────────────────
        public static async Task<PtpDiagnostic> QueryAsync(
            string serverIP,
            int timeoutMs       = 6000,
            int announceWaitMs  = 2000)
        {
            var diag = new PtpDiagnostic { ServerIP = serverIP };
            try
            {
                await diag.RunExchange(serverIP, timeoutMs, announceWaitMs);
                diag.Success = true;
            }
            catch (OperationCanceledException)
            {
                if (string.IsNullOrEmpty(diag.Error))
                    diag.Error = $"Timeout ({timeoutMs} ms) — no PTP response. " +
                                 "Check: master running? firewall ports 319/320 open? " +
                                 "multicast route to 224.0.1.129 present?";
            }
            catch (Exception ex)
            {
                diag.Error = ex.Message;
            }
            return diag;
        }

        // ── Exchange state machine ────────────────────────────────────────────
        private async Task RunExchange(string serverIP, int timeoutMs, int announceWaitMs)
        {
            // Open event socket (port 319) and general socket (port 320)
            using var udpEvent   = CreateMulticastSocket(EVENT_PORT);
            using var udpGeneral = CreateMulticastSocket(GENERAL_PORT);

            // Phase 1: Optionally capture ANNOUNCE (no request needed — master broadcasts)
            using var announceCts = new CancellationTokenSource(announceWaitMs);
            try
            {
                await WaitForAnnounce(udpGeneral, announceCts.Token);
            }
            catch (OperationCanceledException)
            {
                // ANNOUNCE not received in time — not fatal, continue with exchange
            }

            // Phase 2 onwards: full exchange must complete within remaining timeout
            using var cts = new CancellationTokenSource(timeoutMs);

            // Wait for SYNC → record t2, save sequenceId
            await WaitForSync(udpEvent, cts.Token);

            // Wait for matching FOLLOW_UP → extract t1
            await WaitForFollowUp(udpGeneral, cts.Token);

            // Send DELAY_REQ → record t3
            _delayReqSeq = (ushort)Interlocked.Increment(ref _seqCounter);
            byte[] delayReqFrame = BuildDelayReq(_delayReqSeq);
            T3 = DateTime.UtcNow;
            var masterEP = new IPEndPoint(IPAddress.Parse(serverIP), EVENT_PORT);
            await udpEvent.SendAsync(delayReqFrame, delayReqFrame.Length, masterEP);

            // Wait for DELAY_RESP matching our clockId + seq → extract t4
            await WaitForDelayResp(udpGeneral, cts.Token);

            // Calculate timing
            double t1ms = T1.Subtract(PTP_EPOCH).TotalMilliseconds;
            double t2ms = T2.Subtract(PTP_EPOCH).TotalMilliseconds;
            double t3ms = T3.Subtract(PTP_EPOCH).TotalMilliseconds;
            double t4ms = T4.Subtract(PTP_EPOCH).TotalMilliseconds;

            RoundTripMs = (t4ms - t1ms) - (t3ms - t2ms);
            OffsetMs    = ((t2ms - t1ms) + (t3ms - t4ms)) / 2.0;
        }

        // ── Phase 1: ANNOUNCE ─────────────────────────────────────────────────
        private async Task WaitForAnnounce(UdpClient udp, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var res = await udp.ReceiveAsync(ct);
                byte[] p = res.Buffer;
                if (p.Length < 64) continue;
                if ((p[0] & 0x0F) != MSG_ANNOUNCE) continue;
                if (p[4] != 0) continue;   // domain 0 only

                // ANNOUNCE body starts at byte 34 (after 34-byte header)
                // [34-43] originTimestamp — skip
                // [44-45] currentUtcOffset
                // [46]    reserved
                // [47]    grandmasterPriority1
                // [48]    grandmasterClockClass
                // [49]    grandmasterClockAccuracy
                // [50-51] grandmasterOffsetScaledLogVariance
                // [52]    grandmasterPriority2
                // [53-60] grandmasterIdentity
                // [61-62] stepsRemoved
                // [63]    timeSource

                Domain                   = p[4];
                CurrentUtcOffset         = (short)((p[44] << 8) | p[45]);
                GrandmasterPriority1     = p[47];
                ClockClass               = p[48];
                ClockAccuracy            = p[49];
                OffsetScaledLogVariance  = (ushort)((p[50] << 8) | p[51]);
                GrandmasterPriority2     = p[52];
                Array.Copy(p, 53, GrandmasterIdentity, 0, 8);
                StepsRemoved             = (ushort)((p[61] << 8) | p[62]);
                TimeSource               = p[63];
                AnnounceReceived         = true;
                return;
            }
        }

        // ── Phase 2: SYNC ─────────────────────────────────────────────────────
        private async Task WaitForSync(UdpClient udp, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var res = await udp.ReceiveAsync(ct);
                byte[] p = res.Buffer;
                if (p.Length < 44) continue;
                if ((p[0] & 0x0F) != MSG_SYNC) continue;
                if (p[4] != 0) continue;  // domain 0

                T2       = DateTime.UtcNow;   // t2 — as close to arrival as possible
                _syncSeq = (ushort)((p[30] << 8) | p[31]);
                return;
            }
        }

        // ── Phase 3: FOLLOW_UP ────────────────────────────────────────────────
        private async Task WaitForFollowUp(UdpClient udp, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var res = await udp.ReceiveAsync(ct);
                byte[] p = res.Buffer;
                if (p.Length < 44) continue;
                if ((p[0] & 0x0F) != MSG_FOLLOW_UP) continue;
                if (p[4] != 0) continue;  // domain 0

                ushort seq = (ushort)((p[30] << 8) | p[31]);
                if (seq != _syncSeq) continue;   // must match our SYNC

                // preciseOriginTimestamp at [34] — 6B seconds + 4B nanoseconds
                T1 = ExtractTimestamp(p, 34);
                return;
            }
        }

        // ── Phase 5: DELAY_RESP ───────────────────────────────────────────────
        private async Task WaitForDelayResp(UdpClient udp, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var res = await udp.ReceiveAsync(ct);
                byte[] p = res.Buffer;
                if (p.Length < 54) continue;
                if ((p[0] & 0x0F) != MSG_DELAY_RESP) continue;
                if (p[4] != 0) continue;  // domain 0

                ushort seq = (ushort)((p[30] << 8) | p[31]);
                if (seq != _delayReqSeq) continue;

                // requestingPortIdentity at [44] — 8B clockId + 2B portNum
                // Verify it's addressed to us
                bool match = true;
                for (int i = 0; i < 8; i++)
                    if (p[44 + i] != CLIENT_CLOCK_ID[i]) { match = false; break; }
                if (!match) continue;

                // receiveTimestamp at [34] — 6B seconds + 4B nanoseconds = t4
                T4 = ExtractTimestamp(p, 34);
                return;
            }
        }

        // ── DELAY_REQ frame builder ───────────────────────────────────────────
        private byte[] BuildDelayReq(ushort seq)
        {
            // 34-byte header + 10-byte originTimestamp (zeroed) = 44 bytes
            byte[] p = new byte[44];

            p[0]  = MSG_DELAY_REQ;    // transportSpecific=0, messageType=0x1
            p[1]  = 0x02;             // versionPTP = 2
            p[2]  = 0x00; p[3] = 44; // messageLength
            p[4]  = 0x00;             // domainNumber = 0
            // p[5]  = 0x00 reserved
            // p[6-7] = 0x0000 flags (no two-step)
            // p[8-15] = 0 correctionField
            // p[16-19] = 0 reserved
            Array.Copy(CLIENT_CLOCK_ID, 0, p, 20, 8);  // sourcePortIdentity.clockIdentity
            p[28] = (byte)(CLIENT_PORT_NUM >> 8);
            p[29] = (byte)(CLIENT_PORT_NUM & 0xFF);     // sourcePortIdentity.portNumber
            p[30] = (byte)(seq >> 8);
            p[31] = (byte)(seq & 0xFF);                 // sequenceId
            p[32] = 0x01;                               // controlField = 1 (DELAY_REQ)
            p[33] = 0x7F;                               // logMessageInterval = 0x7F (unspecified)
            // p[34-43] = 0 originTimestamp (zeroed — t3 recorded locally)

            return p;
        }

        // ── Socket factory ────────────────────────────────────────────────────
        private static UdpClient CreateMulticastSocket(int port)
        {
            var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket,
                                       SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            udp.JoinMulticastGroup(MULTICAST_IP);
            udp.MulticastLoopback = false;
            return udp;
        }

        // ── PTP timestamp decoder ─────────────────────────────────────────────
        // 10-byte PTP timestamp: 6 bytes seconds (BE) + 4 bytes nanoseconds (BE)
        // Matches firmware extractTimestamp() in ptpClient.hpp
        private static DateTime ExtractTimestamp(byte[] p, int offset)
        {
            ulong secs = 0;
            for (int i = 0; i < 6; i++)
                secs = (secs << 8) | p[offset + i];
            ulong ns = ((ulong)p[offset + 6] << 24) | ((ulong)p[offset + 7] << 16) |
                       ((ulong)p[offset + 8] <<  8) |  (ulong)p[offset + 9];
            if (secs == 0 && ns == 0) return DateTime.MinValue;
            return PTP_EPOCH.AddSeconds((double)secs)
                            .AddMilliseconds((double)ns / 1e6);
        }

        // ── Summary for textbox ───────────────────────────────────────────────
        public string Summary()
        {
            if (!Success)
                return $"[{ServerIP}] FAILED: {Error}\r\n";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== PTP Diagnostic: {ServerIP} ===");

            if (AnnounceReceived)
            {
                sb.AppendLine($"  Grandmaster ID:     {FormatClockId(GrandmasterIdentity)}");
                sb.AppendLine($"  Domain:             {Domain}");
                sb.AppendLine($"  Priority 1/2:       {GrandmasterPriority1} / {GrandmasterPriority2}");
                sb.AppendLine($"  Clock Class:        {ClockClass}  {ClockClassDesc(ClockClass)}");
                sb.AppendLine($"  Clock Accuracy:     0x{ClockAccuracy:X2}  {ClockAccuracyDesc(ClockAccuracy)}");
                sb.AppendLine($"  OSLV:               0x{OffsetScaledLogVariance:X4}");
                sb.AppendLine($"  UTC Offset:         {CurrentUtcOffset} s");
                sb.AppendLine($"  Steps Removed:      {StepsRemoved}");
                sb.AppendLine($"  Time Source:        0x{TimeSource:X2}  {TimeSourceDesc(TimeSource)}");
            }
            else
            {
                sb.AppendLine("  ANNOUNCE:           not received (passive only)");
            }

            sb.AppendLine($"  T1 (master origin): {T1:HH:mm:ss.ffffff} UTC");
            sb.AppendLine($"  T2 (local rx):      {T2:HH:mm:ss.ffffff} UTC");
            sb.AppendLine($"  T3 (local tx):      {T3:HH:mm:ss.ffffff} UTC");
            sb.AppendLine($"  T4 (master rx):     {T4:HH:mm:ss.ffffff} UTC");
            sb.AppendLine($"  Round Trip:         {RoundTripMs:F3} ms");
            sb.AppendLine($"  Clock Offset:       {OffsetMs:F3} ms");
            sb.AppendLine("=====================================");

            return sb.ToString();
        }

        // ── Descriptor helpers ────────────────────────────────────────────────
        private static string FormatClockId(byte[] id) =>
            $"{id[0]:X2}:{id[1]:X2}:{id[2]:X2}:{id[3]:X2}:{id[4]:X2}:{id[5]:X2}:{id[6]:X2}:{id[7]:X2}";

        private static string ClockClassDesc(byte cc) => cc switch
        {
            6   => "(primary — locked to GPS/GNSS)",
            7   => "(primary — holdover, was GPS)",
            52  => "(application-specific — degraded)",
            135 => "(secondary — traceable)",
            165 => "(default — not traceable)",
            255 => "(slave-only clock)",
            _   => ""
        };

        private static string ClockAccuracyDesc(byte acc) => acc switch
        {
            0x20 => "(< 25 ns)",
            0x21 => "(< 100 ns)",
            0x22 => "(< 250 ns)",
            0x23 => "(< 1 µs)",
            0x24 => "(< 2.5 µs)",
            0x25 => "(< 10 µs)",
            0x26 => "(< 25 µs)",
            0x27 => "(< 100 µs)",
            0x28 => "(< 250 µs)",
            0x29 => "(< 1 ms)",
            0x2A => "(< 2.5 ms)",
            0x2B => "(< 10 ms)",
            0xFE => "(unknown)",
            _    => ""
        };

        private static string TimeSourceDesc(byte ts) => ts switch
        {
            0x10 => "(ATOMIC_CLOCK)",
            0x20 => "(GPS)",
            0x30 => "(TERRESTRIAL_RADIO)",
            0x40 => "(PTP)",
            0x50 => "(NTP)",
            0x60 => "(HAND_SET)",
            0x90 => "(OTHER)",
            0xA0 => "(INTERNAL_OSCILLATOR)",
            _    => ""
        };
    }
}
