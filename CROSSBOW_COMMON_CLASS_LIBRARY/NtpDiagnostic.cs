// NtpDiagnostic.cs  —  One-shot NTP query with full packet decode
// Usage: var result = await NtpDiagnostic.QueryAsync("192.168.1.33");
//        textBox.AppendText(result.Summary());

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CROSSBOW
{
    public class NtpDiagnostic
    {
        public string ServerIP { get; private set; }
        public bool Success { get; private set; }
        public string Error { get; private set; }

        // Packet fields
        public int LeapIndicator { get; private set; }  // 0=ok, 1=+1s, 2=-1s, 3=unknown
        public int Version { get; private set; }  // should be 4
        public int Mode { get; private set; }  // 4=server
        public int Stratum { get; private set; }  // 1=GPS/primary, 16=unsynced
        public string StratumDesc => Stratum == 0 ? "unspecified/KoD" :
                                         Stratum == 1 ? "primary (GPS/GNSS)" :
                                         Stratum <= 15 ? $"secondary (stratum {Stratum})" :
                                                         "UNSYNCHRONIZED (stratum 16)";
        public int PollInterval { get; private set; }  // log2 seconds
        public double Precision { get; private set; }  // seconds
        public double RootDelay { get; private set; }  // seconds
        public double RootDispersion { get; private set; }  // seconds
        public string RefID { get; private set; }  // "GPS", "PPS", etc for stratum 1

        public DateTime ReferenceTime { get; private set; }  // last time server synced
        public DateTime OriginTime { get; private set; }  // T1 — our request transmit time
        public DateTime ReceiveTime { get; private set; }  // T2 — server received request
        public DateTime TransmitTime { get; private set; }  // T3 — server sent response
        public DateTime DestinationTime { get; private set; } // T4 — we received response

        public double RoundTripMs { get; private set; }  // (T4-T1)-(T3-T2) in ms
        public double OffsetMs { get; private set; }  // ((T2-T1)+(T3-T4))/2 in ms

        // ── Static factory ────────────────────────────────────────────────────
        public static async Task<NtpDiagnostic> QueryAsync(string serverIP, int timeoutMs = 3000)
        {
            var diag = new NtpDiagnostic { ServerIP = serverIP };
            try
            {
                byte[] packet = BuildRequest();
                DateTime t1 = DateTime.UtcNow;

                using var udp = new UdpClient();
                udp.Client.ReceiveTimeout = timeoutMs;
                var ep = new IPEndPoint(IPAddress.Parse(serverIP), 123);
                await udp.SendAsync(packet, packet.Length, ep);

                var res = await Task.Run(() => udp.Receive(ref ep));
                diag.DestinationTime = DateTime.UtcNow;

                if (res.Length < 48) { diag.Error = $"Short packet: {res.Length} bytes"; return diag; }

                diag.Parse(res, t1);
                diag.Success = true;
            }
            catch (Exception ex)
            {
                diag.Error = ex.Message;
            }
            return diag;
        }

        // ── Packet builder ────────────────────────────────────────────────────
        private static byte[] BuildRequest()
        {
            byte[] p = new byte[48];
            p[0] = 0x1B;  // LI=0, VN=3, Mode=3 (client)
            return p;
        }

        // ── Packet parser ─────────────────────────────────────────────────────
        private void Parse(byte[] p, DateTime t1)
        {
            LeapIndicator = (p[0] >> 6) & 0x03;
            Version = (p[0] >> 3) & 0x07;
            Mode = p[0] & 0x07;
            Stratum = p[1];
            PollInterval = p[2];
            Precision = Math.Pow(2, (sbyte)p[3]);
            RootDelay = ToFixed(p, 4);
            RootDispersion = ToFixed(p, 8);
            RefID = Stratum == 1
                                ? System.Text.Encoding.ASCII.GetString(p, 12, 4).TrimEnd('\0')
                                : $"{p[12]}.{p[13]}.{p[14]}.{p[15]}";

            ReferenceTime = ToDateTime(p, 16);
            OriginTime = t1;   // we stamped T1 ourselves
            ReceiveTime = ToDateTime(p, 32);
            TransmitTime = ToDateTime(p, 40);

            double t1ms = ToUnixMs(t1);
            double t2ms = ToUnixMs(ReceiveTime);
            double t3ms = ToUnixMs(TransmitTime);
            double t4ms = ToUnixMs(DestinationTime);

            RoundTripMs = (t4ms - t1ms) - (t3ms - t2ms);
            OffsetMs = ((t2ms - t1ms) + (t3ms - t4ms)) / 2.0;
        }

        // ── Summary for textbox ───────────────────────────────────────────────
        public string Summary()
        {
            if (!Success)
                return $"[{ServerIP}] FAILED: {Error}\r\n";

            return
                $"=== NTP Diagnostic: {ServerIP} ===\r\n" +
                $"  Success:          YES\r\n" +
                $"  Stratum:          {Stratum}  →  {StratumDesc}\r\n" +
                $"  Ref ID:           {RefID}\r\n" +
                $"  Leap Indicator:   {LeapIndicator}  ({LeapDesc()})\r\n" +
                $"  NTP Version:      {Version}\r\n" +
                $"  Mode:             {Mode}  (4=server)\r\n" +
                $"  Poll Interval:    2^{PollInterval} = {Math.Pow(2, PollInterval):F0} s\r\n" +
                $"  Root Delay:       {RootDelay * 1000:F3} ms\r\n" +
                $"  Root Dispersion:  {RootDispersion * 1000:F3} ms\r\n" +
                $"  Reference Time:   {ReferenceTime:HH:mm:ss.fff} UTC\r\n" +
                $"  Transmit Time:    {TransmitTime:HH:mm:ss.fff} UTC\r\n" +
                $"  Round Trip:       {RoundTripMs:F3} ms\r\n" +
                $"  Clock Offset:     {OffsetMs:F3} ms\r\n" +
                $"=====================================\r\n";
        }

        private string LeapDesc() =>
            LeapIndicator switch { 0 => "no warning", 1 => "+1s", 2 => "-1s", 3 => "UNSYNCHRONIZED", _ => "?" };

        // ── NTP timestamp helpers ─────────────────────────────────────────────
        private static readonly DateTime _epoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static DateTime ToDateTime(byte[] p, int offset)
        {
            ulong secs = ((ulong)p[offset] << 24) | ((ulong)p[offset + 1] << 16) |
                         ((ulong)p[offset + 2] << 8) | (ulong)p[offset + 3];
            ulong frac = ((ulong)p[offset + 4] << 24) | ((ulong)p[offset + 5] << 16) |
                         ((ulong)p[offset + 6] << 8) | (ulong)p[offset + 7];
            double ms = secs * 1000.0 + (frac * 1000.0) / 0x100000000L;
            return secs == 0 ? DateTime.MinValue : _epoch.AddMilliseconds(ms);
        }

        private static double ToFixed(byte[] p, int offset) =>
            ((p[offset] << 8) | p[offset + 1]) +
            ((p[offset + 2] << 8) | p[offset + 3]) / 65536.0;

        private static double ToUnixMs(DateTime dt) =>
            (dt - _epoch).TotalMilliseconds;
    }
}