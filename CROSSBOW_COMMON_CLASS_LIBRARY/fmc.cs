using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CROSSBOW
{
    // -----------------------------------------------------------------------
    // FMC — engineering GUI connection to the FMC controller on A2.
    //
    // A2 protocol (ICD v1.7):
    //   Port    : 10018
    //   Magic   : 0xCB 0x49 (internal)
    //   Framing : 8-byte min request, 521-byte fixed response
    //   CRC     : CRC-16/CCITT (poly 0x1021, init 0xFFFF), big-endian
    //   SEQ     : rolling uint8, client-managed, replay window = 32
    //
    // All outgoing commands are wrapped in BuildA2Frame().
    // All incoming frames are validated in MSG_FMC.Parse() before use.
    // -----------------------------------------------------------------------
    public class FMC
    {
        // -------------------------------------------------------------------
        // Configuration
        // -------------------------------------------------------------------
        public string IP   { get; private set; } = "192.168.1.23";
        public int    Port { get; private set; } = 10018;   // A2 engineering port

        // Local bind IP — internal NIC (<100) so FMC firmware accepts the source address.
        // Detected at runtime to handle dual-NIC machines regardless of adapter order.
        private string LocalIP => CrossbowNic.GetInternalIP();

        // Frame magic bytes — internal A2
        private const byte MAGIC_HI = 0xCB;
        private const byte MAGIC_LO = 0x49;

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
        public int DropCount { get { return _dropCount; } }
        public DateTime ConnectedSince { get { return _connectedSince; } }
        public DateTime lastMsgRx { get; private set; } = DateTime.UtcNow;
        public double HB_RX_ms { get; private set; } = 0;

        public MSG_FMC       LatestMSG    { get; private set; } = new MSG_FMC();
        public SYSTEM_STATES System_State { get { return LatestMSG.System_State; } }
        public BDC_MODES     BDC_Mode     { get { return LatestMSG.BDC_Mode; } }

        public FMC() { }

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

            Debug.WriteLine("FMC: starting listener");
            _ = BackgroundUDPRead();
            _ = KeepaliveLoop();
        }

        public void Stop()
        {
            Debug.WriteLine("FMC: stopping listener");
            _ts?.Cancel();
        }

        // -------------------------------------------------------------------
        // BackgroundUDPRead — receive loop (runs on thread pool)
        // -------------------------------------------------------------------
        private async Task BackgroundUDPRead()
        {
            await Task.Run(async () =>
            {
                _udp = new UdpClient();
                _udp.Client.Bind(new IPEndPoint(IPAddress.Parse(LocalIP), 0));
                _remoteEP = new IPEndPoint(IPAddress.Parse(IP), Port);
                Debug.WriteLine($"FMC: UDP bound to {LocalIP}:0 -> {IP}:{Port}");

                // Single registration frame — firmware replay fix handles reconnects cleanly
                Send(BuildA2Frame((byte)ICD.FRAME_KEEPALIVE));
                _lastKeepalive = DateTime.UtcNow;
                Debug.WriteLine("FMC: registration sent (0xA4)");

                while (!_ct.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udp.ReceiveAsync(_ct);
                        if (!result.RemoteEndPoint.Address.Equals(IPAddress.Parse(IP))) continue;
                        byte[] frame = result.Buffer;

                        if (frame.Length == MSG_FMC.FRAME_RESPONSE_LEN)
                        {
                            isConnected = true;
                            if (!_wasConnected)
                            {
                                _wasConnected = true;
                                _connectedSince = DateTime.UtcNow;
                                Debug.WriteLine("FMC: connection established");
                            }
                            HB_RX_ms = (DateTime.UtcNow - lastMsgRx).TotalMilliseconds;
                            lastMsgRx = DateTime.UtcNow;
                            LatestMSG.Parse(frame);   // validates magic, CRC, STATUS internally
                        }
                        else
                        {
                            Debug.WriteLine($"FMC: unexpected frame length {frame.Length}");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"FMC: receive error: {ex.Message}");
                    }
                }

                _udp.Close();
                Debug.WriteLine("FMC: UDP closed");
            }, _ct);
        }

        // -------------------------------------------------------------------
        // KeepaliveLoop — runs independently of the receive loop.
        // Fires every KEEPALIVE_INTERVAL_MS regardless of packet activity.
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
                            Debug.WriteLine($"FMC: connection restored — was down {downTime:0.0}s");
                    }

                    if (stale && _wasConnected && _connectedSince != DateTime.MinValue
                        && (DateTime.UtcNow - _connectedSince).TotalMilliseconds > KEEPALIVE_INTERVAL_MS)
                    {
                        _dropTime = DateTime.UtcNow;
                        _dropCount++;
                        _wasConnected = false;
                        isConnected = false;
                        Debug.WriteLine($"FMC: connection lost — drop #{_dropCount} after {(DateTime.UtcNow - _connectedSince).TotalSeconds:0.0}s uptime");
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
            int frameLen   = 6 + payloadLen + 2;

            byte[] frame = new byte[frameLen];
            frame[0] = MAGIC_HI;
            frame[1] = MAGIC_LO;
            frame[2] = _seq++;
            frame[3] = cmd;
            frame[4] = (byte)( payloadLen       & 0xFF);
            frame[5] = (byte)((payloadLen >> 8)  & 0xFF);

            if (payloadLen > 0)
                Array.Copy(payload, 0, frame, 6, payloadLen);

            ushort crc = CrcHelper.Crc16(frame, frameLen - 2);
            frame[frameLen - 2] = (byte)((crc >> 8) & 0xFF);
            frame[frameLen - 1] = (byte)( crc        & 0xFF);

            return frame;
        }

        private void Send(byte[] frame)
        {
            if (_udp == null) return;
            try
            {
                _udp.Send(frame, frame.Length, _remoteEP);
            }
            catch (Exception ex) { Debug.WriteLine($"FMC: send error: {ex.Message}"); }
        }

        private void SendKeepalive()
        {
            Send(BuildA2Frame((byte)ICD.FRAME_KEEPALIVE));
            _lastKeepalive = DateTime.UtcNow;
            Debug.WriteLine("FMC: keepalive (0xA4) sent");
        }

        // -------------------------------------------------------------------
        // ICD commands — all wrapped in BuildA2Frame
        // -------------------------------------------------------------------

        // 0xA0 SET_UNSOLICITED
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

        // 0xFB FMC_SET_STAGE_POS — uint32 LE position counts
        public void SetStagePos(UInt32 pos)
        {
            byte[] b = BitConverter.GetBytes(pos);
            Send(BuildA2Frame((byte)ICD.FMC_SET_STAGE_POS, b));
        }

        // 0xFE FMC_SET_STAGE_ENABLE
        public bool STAGE_ENABLED
        {
            set { Send(BuildA2Frame((byte)ICD.FMC_SET_STAGE_ENABLE, new[] { (byte)(value ? 1 : 0) })); }
        }

        // 0xFC FMC_STAGE_CALIB
        public void STAGE_CALIBRATE()
        {
            Send(BuildA2Frame((byte)ICD.FMC_STAGE_CALIB));
        }

        // 0xF0 FMC_SET_FSM_POW
        public bool FSM_POWER_ENABLED
        {
            set { Send(BuildA2Frame((byte)ICD.FMC_SET_FSM_POW, new[] { (byte)(value ? 1 : 0) })); }
        }

        // 0xF7 FMC_READ_FSM_POS
        public void FSM_READ_POS()
        {
            Send(BuildA2Frame((byte)ICD.FMC_READ_FSM_POS));
        }

        // 0xF5 FMC_FSM_TEST_SCAN
        public void FSMTestScan()
        {
            Send(BuildA2Frame((byte)ICD.FMC_FSM_TEST_SCAN));
        }

        // 0xF3 FMC_SET_FSM_POS — int16 x, int16 y (LE)
        public void SetFSMPos(Int16 x, Int16 y)
        {
            byte[] bx = BitConverter.GetBytes(x);
            byte[] by = BitConverter.GetBytes(y);
            Send(BuildA2Frame((byte)ICD.FMC_SET_FSM_POS,
                new[] { bx[0], bx[1], by[0], by[1] }));
        }

        // -------------------------------------------------------------------
        // CRC delegated to shared CrcHelper.cs
    }
}
