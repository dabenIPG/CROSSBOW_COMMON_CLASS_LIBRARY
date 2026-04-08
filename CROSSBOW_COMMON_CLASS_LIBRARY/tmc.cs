using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CROSSBOW
{
    // -----------------------------------------------------------------------
    // TMC — engineering GUI connection to the TMC controller on A2.
    //
    // A2 protocol (ICD v3.4.0 session 35):
    //   Port    : 10018
    //   Magic   : 0xCB 0x49 (internal)
    //   Framing : 8-byte min request, 521-byte fixed response
    //   CRC     : CRC-16/CCITT (poly 0x1021, init 0xFFFF), big-endian
    //   SEQ     : rolling uint8, client-managed, replay window = 32
    //
    // Client model:
    //   Register   : send 0xA4 FRAME_KEEPALIVE (any accepted command registers)
    //   Keep-alive : send 0xA4 every KEEPALIVE_INTERVAL_MS (must be < 60s)
    //   Subscribe  : send 0xA0 {0x01} SET_UNSOLICITED after registering
    //   Poll       : send 0xA4 {0x01} for one-shot REG1 (rate-gated 1 Hz)
    //
    // All outgoing commands are wrapped in BuildA2Frame().
    // All incoming frames are validated in MSG_TMC.Parse() before use.
    // -----------------------------------------------------------------------
    public class TMC
    {
        // -------------------------------------------------------------------
        // Configuration
        // -------------------------------------------------------------------
        public string IP   { get; private set; } = "192.168.1.12";
        public int    Port { get; private set; } = 10018;   // A2 engineering port

        // Frame magic bytes — internal A2
        private const byte MAGIC_HI = 0xCB;
        private const byte MAGIC_LO = 0x49;

        // Local bind IP — internal NIC (<100) so TMC firmware accepts the source address.
        // Detected at runtime to handle dual-NIC machines regardless of adapter order.
        private string LocalIP => CrossbowNic.GetInternalIP();

        // Keepalive — re-send SET_UNSOLICITED every 30 s to stay within the
        // firmware's 60-second liveness window (frame.hpp CLIENT_TIMEOUT_MS).
        private const int KEEPALIVE_INTERVAL_MS = 30_000;

        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        private UdpClient               _udp;
        private IPEndPoint              _remoteEP;
        private CancellationTokenSource _ts;
        private CancellationToken       _ct;
        private byte                    _seq = 0;
        private DateTime _lastKeepalive = DateTime.MinValue;
        private bool _wasConnected = false;
        private DateTime _connectedSince = DateTime.MinValue;
        private DateTime _dropTime = DateTime.MinValue;
        private int _dropCount = 0;

        public bool isConnected { get; private set; } = false;
        public DateTime ConnectedSince { get { return _connectedSince; } }
        public int DropCount { get { return _dropCount; } }
        public DateTime lastMsgRx  { get; private set; } = DateTime.UtcNow;
        public double   HB_RX_ms   { get; private set; } = 0;

        public MSG_TMC         LatestMSG    { get; private set; } = new MSG_TMC();
        public SYSTEM_STATES   System_State { get { return LatestMSG.System_State; } }
        public BDC_MODES       BDC_Mode     { get { return LatestMSG.BDC_Mode; } }

        public TMC() { }

        // -------------------------------------------------------------------
        // Start / Stop
        // -------------------------------------------------------------------
        public void Start()
        {
            _ts = new CancellationTokenSource();
            _ct = _ts.Token;

            // Randomise starting SEQ to avoid landing in the firmware's stale
            // replay window from the previous session (window = ±32 of last_seq).
            _seq = (byte)new Random().Next(33, 224);   // 33–223: clear of both wrap edges

            Debug.WriteLine("TMC: starting listener");
            _ = BackgroundUDPRead();
            _ = KeepaliveLoop();
        }

        public void Stop()
        {
            Debug.WriteLine("TMC: stopping listener");
            _ts?.Cancel();
        }

        // -------------------------------------------------------------------
        // BackgroundUDPRead — receive loop (runs on thread pool)
        // -------------------------------------------------------------------
        private async Task BackgroundUDPRead()
        {
            await Task.Run(async () =>
            {
                _udp      = new UdpClient();
                _udp.Client.Bind(new IPEndPoint(IPAddress.Parse(LocalIP), 0));  // pin to internal NIC
                _remoteEP = new IPEndPoint(IPAddress.Parse(IP), Port);
                _udp.Connect(_remoteEP);
                Debug.WriteLine($"TMC: UDP connected ({LocalIP} → {IP}:{Port})");

                // Registration burst: advance past firmware's stale SEQ replay window,
                // and register this client in the firmware's client table.
                // Three consecutive 0xA4 FRAME_KEEPALIVE frames guarantee at least one
                // lands outside [last_seq-32, last_seq] regardless of where last_seq sits.
                // Does NOT auto-subscribe — user must tick UnSolicited checkbox explicitly.
                Send(BuildA2Frame((byte)ICD.FRAME_KEEPALIVE));
                _lastKeepalive = DateTime.UtcNow;
                Debug.WriteLine("TMC: registration sent (0xA4)");

                while (!_ct.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udp.ReceiveAsync(_ct);
                        byte[] frame = result.Buffer;

                        if (frame.Length == MSG_TMC.FRAME_RESPONSE_LEN)
                        {
                            // Any valid frame from firmware counts as liveness
                            isConnected = true;
                            if (!_wasConnected)
                            {
                                _wasConnected = true;
                                _connectedSince = DateTime.UtcNow;
                                Debug.WriteLine("TMC: connection established");
                            }
                            HB_RX_ms = (DateTime.UtcNow - lastMsgRx).TotalMilliseconds;
                            lastMsgRx = DateTime.UtcNow;

                            if (frame[3] == (byte)ICD.RES_A1)
                                LatestMSG.Parse(frame);
                            else
                                Debug.WriteLine($"TMC: A2 ACK rx CMD=0x{frame[3]:X2}");
                        }
                        else
                        {
                            Debug.WriteLine($"TMC: unexpected frame length {frame.Length}");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"TMC: receive error: {ex.Message}");
                    }
                }
                isConnected = false;
                _udp.Close();
                Debug.WriteLine("TMC: UDP closed");
            }, _ct);
        }

        // -------------------------------------------------------------------
        // KeepaliveLoop — runs independently of the receive loop.
        // Fires every KEEPALIVE_INTERVAL_MS regardless of packet activity,
        // so the firmware liveness timer is maintained even during network blips
        // where ReceiveAsync would block indefinitely.
        // Send() resets _lastKeepalive so user commands suppress the next tick.
        // -------------------------------------------------------------------
        private const double STALE_WARN_MS = 2000.0;

        private async Task KeepaliveLoop()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(KEEPALIVE_INTERVAL_MS));
            try
            {
                while (await timer.WaitForNextTickAsync(_ct))
                {
                    SendKeepalive();

                    bool stale = isConnected &&
                        (DateTime.UtcNow - lastMsgRx).TotalMilliseconds > STALE_WARN_MS;

                    if (isConnected && !_wasConnected)
                    {
                        var downTime = (_dropCount > 0 && _dropTime != DateTime.MinValue)
                            ? (DateTime.UtcNow - _dropTime).TotalSeconds : 0.0;
                        _connectedSince = DateTime.UtcNow;
                        _wasConnected = true;
                        if (_dropCount > 0)
                            Debug.WriteLine($"TMC: connection restored — was down {downTime:0.0}s");
                    }

                    if (stale && _wasConnected && _connectedSince != DateTime.MinValue
                        && (DateTime.UtcNow - _connectedSince).TotalMilliseconds > KEEPALIVE_INTERVAL_MS)
                    {
                        _dropTime = DateTime.UtcNow;
                        _dropCount++;
                        _wasConnected = false;
                        Debug.WriteLine($"TMC: connection lost — drop #{_dropCount} after {(DateTime.UtcNow - _connectedSince).TotalSeconds:0.0}s uptime");
                    }
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
        }

        // -------------------------------------------------------------------
        // BuildA2Frame — wrap a command payload in a framed A2 request.
        //
        // Request layout:
        //   [0]     MAGIC_HI
        //   [1]     MAGIC_LO
        //   [2]     SEQ_NUM  (auto-incremented)
        //   [3]     CMD_BYTE
        //   [4–5]   PAYLOAD_LEN  uint16 LE
        //   [6+]    PAYLOAD      (may be empty)
        //   [last2] CRC-16/CCITT BE
        // -------------------------------------------------------------------
        private byte[] BuildA2Frame(byte cmd, byte[] payload = null)
        {
            payload ??= Array.Empty<byte>();
            int payloadLen = payload.Length;
            int frameLen   = 6 + payloadLen + 2;   // header + payload + CRC

            byte[] frame = new byte[frameLen];
            frame[0] = MAGIC_HI;
            frame[1] = MAGIC_LO;
            frame[2] = _seq++;
            frame[3] = cmd;
            frame[4] = (byte)( payloadLen       & 0xFF);   // LE low
            frame[5] = (byte)((payloadLen >> 8)  & 0xFF);   // LE high

            if (payloadLen > 0)
                Array.Copy(payload, 0, frame, 6, payloadLen);

            // CRC over bytes [0 .. frameLen-3]
            ushort crc = CrcHelper.Crc16(frame, frameLen - 2);
            frame[frameLen - 2] = (byte)((crc >> 8) & 0xFF);   // BE high
            frame[frameLen - 1] = (byte)( crc        & 0xFF);   // BE low

            return frame;
        }

        private void Send(byte[] frame)
        {
            if (_udp == null) return;
            try
            {
                _udp.Send(frame);
            }
            catch (Exception ex) { Debug.WriteLine($"TMC: send error: {ex.Message}"); }
        }

        // 0xA4 FRAME_KEEPALIVE — register/refresh liveness without changing subscription state.
        // Called automatically every KEEPALIVE_INTERVAL_MS during idle periods.
        // Any other TX (commands, subscribe) also resets _lastKeepalive via Send(), so
        // active sessions naturally suppress unnecessary keepalive packets.
        private void SendKeepalive()
        {
            Send(BuildA2Frame((byte)ICD.FRAME_KEEPALIVE));
            Debug.WriteLine("TMC: keepalive (0xA4) sent");
            _lastKeepalive = DateTime.UtcNow;   // any TX resets the keepalive clock

        }

        // -------------------------------------------------------------------
        // ICD commands — verified against ICD v1.7
        // -------------------------------------------------------------------

        // 0xA0 SET_UNSOLICITED — register (true) or deregister (false) for unsolicited stream
        public bool UnsolicitedMode
        {
            set { Send(BuildA2Frame((byte)ICD.SET_UNSOLICITED, new[] { (byte)(value ? 1 : 0) })); }
        }


        // 0xA2 SET_NTP_CONFIG (INT only)
        // 0 bytes  = force resync on current server
        // 1 byte   = set primary server last octet + resync
        // 2 bytes  = set primary + fallback last octets + resync
        public void SetNtpConfig(byte? primaryOctet = null, byte? fallbackOctet = null)
        {
            byte[] payload = primaryOctet == null  ? Array.Empty<byte>() :
                             fallbackOctet == null ? new[] { primaryOctet.Value } :
                                                     new[] { primaryOctet.Value, fallbackOctet.Value };
            Send(BuildA2Frame((byte)ICD.SET_NTP_CONFIG, payload));
        }

        // 0xA5 SET_SYSTEM_STATE
        public void SetState(SYSTEM_STATES state)
        {
            Send(BuildA2Frame((byte)ICD.SET_SYSTEM_STATE, new[] { (byte)state }));
        }

        // 0xA6 SET_GIMBAL_MODE
        public void SetMode(BDC_MODES mode)
        {
            Send(BuildA2Frame((byte)ICD.SET_GIMBAL_MODE, new[] { (byte)mode }));
        }

        // 0xE7 TMS_INPUT_FAN_SPEED — payload: uint8 which (0/1), uint8 speed
        public void SetInputFanSpeed(int fan, TMC_FAN_SPEEDS spd)
        {
            Send(BuildA2Frame((byte)ICD.TMS_INPUT_FAN_SPEED,
                new[] { (byte)fan, (byte)spd }));
        }

        // 0xE8 TMS_SET_DAC_VALUE — payload: uint8 channel, uint16 LE value
        // PUMP (0x04) and HEATER (0x06) channels are V1 only — TRACO PSUs have no analog input
        // and heater hardware is removed in V2. Suppressed silently on V2 to prevent reject errors.
        public void SetDAC(TMC_DAC_CHANNELS ch, ushort val)
        {
            if (LatestMSG.IsV2 &&
                (ch == TMC_DAC_CHANNELS.PUMP || ch == TMC_DAC_CHANNELS.HEATER))
            {
                Debug.WriteLine($"TMC.SetDAC: channel {ch} not valid on V2 hardware — suppressed");
                return;
            }
            byte[] b = BitConverter.GetBytes(val);   // little-endian
            Send(BuildA2Frame((byte)ICD.TMS_SET_DAC_VALUE,
                new[] { (byte)ch, b[0], b[1] }));
        }

        // 0xE9 TMS_SET_VICOR_ENABLE — payload: uint8 vicor enum, uint8 0/1
        //
        // Channel validity by revision:
        //   V1: LCM1(0), LCM2(1), PUMP(2), HEAT(3)
        //   V2: LCM1(0), LCM2(1), PUMP1(2), PUMP2(4)  — HEAT does not exist
        //
        // GUI code must use LatestMSG.IsV1 / IsV2 to determine which overload to call.
        // Sending an invalid channel (e.g. HEAT on V2) is silently rejected by firmware
        // (STATUS_CMD_REJECTED), but this guard prevents accidental sends entirely.
        public void EnableVicor(TMC_VICORS v, bool en)
        {
            // Block V1-only channels on V2 hardware
            if (LatestMSG.IsV2 && ((byte)v == (byte)TMC_VICORS.HEAT))
            {
                Debug.WriteLine($"TMC.EnableVicor: HEAT channel not valid on V2 hardware — suppressed");
                return;
            }
            Send(BuildA2Frame((byte)ICD.TMS_SET_VICOR_ENABLE,
                new[] { (byte)v, (byte)(en ? 1 : 0) }));
        }

        // Convenience: enable/disable both pumps together (V2 TRACO PSU normal operation)
        public void EnableBothPumps(bool en)
        {
            if (!LatestMSG.IsV2)
            {
                Debug.WriteLine("TMC.EnableBothPumps: V2 only — use EnableVicor(TMC_VICORS.PUMP) on V1");
                return;
            }
            Send(BuildA2Frame((byte)ICD.TMS_SET_VICOR_ENABLE,
                new[] { (byte)TMC_VICORS.PUMP1, (byte)(en ? 1 : 0) }));
            Send(BuildA2Frame((byte)ICD.TMS_SET_VICOR_ENABLE,
                new[] { (byte)TMC_VICORS.PUMP2, (byte)(en ? 1 : 0) }));
        }

        // 0xEA TMS_SET_LCM_ENABLE — payload: uint8 lcm enum, uint8 0/1
        public void EnableLCM(TMC_LCMS lcm, bool en)
        {
            Send(BuildA2Frame((byte)ICD.TMS_SET_LCM_ENABLE,
                new[] { (byte)lcm, (byte)(en ? 1 : 0) }));
        }

        // 0xEB TMS_SET_TARGET_TEMP — payload: uint8 temp °C (firmware clamps to [10–40])
        public void SetTargetTemp(byte tt)
        {
            Send(BuildA2Frame((byte)ICD.TMS_SET_TARGET_TEMP, new[] { tt }));
        }

        // -------------------------------------------------------------------
        // CRC delegated to shared CrcHelper.cs
    }
}
