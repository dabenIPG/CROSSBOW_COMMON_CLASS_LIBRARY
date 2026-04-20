using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CROSSBOW
{
    // -----------------------------------------------------------------------
    // TRC — engineering GUI connection to the TRC controller on A2.
    //
    // A2 protocol (ICD v4.1.0):
    //   Port    : 10018
    //   Magic   : 0xCB 0x49 (internal)
    //   Framing : 8-byte min request, 521-byte fixed response
    //   CRC     : CRC-16/CCITT (poly 0x1021, init 0xFFFF), big-endian
    //   SEQ     : rolling uint8, client-managed, replay window = 32
    //
    // Client model:
    //   Register   : send 0xA4 FRAME_KEEPALIVE on connect
    //   Keep-alive : send 0xA4 every KEEPALIVE_INTERVAL_MS (must be < 60s)
    //   Subscribe  : send 0xA0 {0x01} SET_UNSOLICITED after registering
    //   Poll       : send 0xA4 {0x01} for one-shot REG1 (rate-gated 1 Hz)
    //
    // All outgoing commands are wrapped in BuildA2Frame().
    // All incoming frames are validated against magic, length, and CRC.
    //
    // Legacy port 5010 — RETIRED. Pending deprecation (TRC-M9).
    // -----------------------------------------------------------------------
    public class TRC
    {
        // -------------------------------------------------------------------
        // Configuration
        // -------------------------------------------------------------------
        public string IP   { get; private set; } = IPS.TRC;
        public int    Port { get; private set; } = 10018;   // A2 engineering port

        // Frame magic bytes — internal A2
        private const byte MAGIC_HI = 0xCB;
        private const byte MAGIC_LO = 0x49;

        // Local bind IP — internal NIC (<100) so TRC firmware accepts the source address.
        private string LocalIP => CrossbowNic.GetInternalIP();

        // Keepalive — re-send 0xA4 every 30s to stay within firmware's 60s liveness window.
        private const int    KEEPALIVE_INTERVAL_MS = 30_000;
        private const double STALE_WARN_MS         = 2000.0;

        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        private UdpClient               _udp;
        private IPEndPoint              _remoteEP;
        private CancellationTokenSource _ts;
        private CancellationToken       _ct;
        private byte                    _seq = 0;
        private DateTime _lastKeepalive  = DateTime.MinValue;
        private bool     _wasConnected   = false;
        private DateTime _connectedSince = DateTime.MinValue;
        private DateTime _dropTime       = DateTime.MinValue;
        private int      _dropCount      = 0;

        public bool     isConnected    { get; private set; } = false;
        public DateTime ConnectedSince { get { return _connectedSince; } }
        public int      DropCount      { get { return _dropCount; } }
        public DateTime lastMsgRx      { get; private set; } = DateTime.UtcNow;
        public double   HB_RX_ms      { get; private set; } = 0;

        public MSG_TRC       LatestMSG    { get; private set; } = new MSG_TRC();
        public SYSTEM_STATES System_State { get { return LatestMSG.System_State; } }
        public BDC_MODES     BDC_Mode     { get { return LatestMSG.BDC_Mode; } }

        public TRC() { }

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

            Debug.WriteLine("TRC: starting listener");
            _ = BackgroundUDPRead();
            _ = KeepaliveLoop();
        }

        public void Stop()
        {
            Debug.WriteLine("TRC: stopping listener");
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
                Debug.WriteLine($"TRC: UDP connected ({LocalIP} → {IP}:{Port})");

                // Single 0xA4 FRAME_KEEPALIVE registers this client in the firmware's
                // client table. Does NOT auto-subscribe — user must tick UnSolicited checkbox.
                Send(BuildA2Frame((byte)ICD.FRAME_KEEPALIVE));
                _lastKeepalive = DateTime.UtcNow;
                Debug.WriteLine("TRC: registration sent (0xA4)");

                while (!_ct.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udp.ReceiveAsync(_ct);
                        byte[] frame = result.Buffer;

                        if (frame.Length == MSG_TRC.FRAME_RESPONSE_LEN)
                        {
                            // Any valid-length frame from firmware counts as liveness
                            isConnected = true;
                            if (!_wasConnected)
                            {
                                _wasConnected   = true;
                                _connectedSince = DateTime.UtcNow;
                                Debug.WriteLine("TRC: connection established");
                            }
                            HB_RX_ms  = (DateTime.UtcNow - lastMsgRx).TotalMilliseconds;
                            lastMsgRx = DateTime.UtcNow;

                            LatestMSG.Parse(frame);   // validates magic, CRC, status; routes to ParseMsg internally
                        }
                        else
                        {
                            Debug.WriteLine($"TRC: unexpected frame length {frame.Length}");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"TRC: receive error: {ex.Message}");
                    }
                }
                isConnected = false;
                _udp.Close();
                Debug.WriteLine("TRC: UDP closed");
            }, _ct);
        }

        // -------------------------------------------------------------------
        // KeepaliveLoop — runs independently of the receive loop.
        // Fires every KEEPALIVE_INTERVAL_MS regardless of packet activity.
        // Send() resets _lastKeepalive so user commands suppress the next tick.
        // -------------------------------------------------------------------
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
                        _wasConnected   = true;
                        if (_dropCount > 0)
                            Debug.WriteLine($"TRC: connection restored — was down {downTime:0.0}s");
                    }

                    if (stale && _wasConnected && _connectedSince != DateTime.MinValue
                        && (DateTime.UtcNow - _connectedSince).TotalMilliseconds > KEEPALIVE_INTERVAL_MS)
                    {
                        _dropTime  = DateTime.UtcNow;
                        _dropCount++;
                        _wasConnected = false;
                        Debug.WriteLine($"TRC: connection lost — drop #{_dropCount} after {(DateTime.UtcNow - _connectedSince).TotalSeconds:0.0}s uptime");
                    }
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
        }

        // -------------------------------------------------------------------
        // BuildA2Frame — wrap a command payload in a framed A2 request.
        // FRAME_RESPONSE_LEN, PAYLOAD_OFFSET, STATUS_OK live on MSG_TRC.
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
            catch (Exception ex) { Debug.WriteLine($"TRC: send error: {ex.Message}"); }
        }

        // 0xA4 FRAME_KEEPALIVE — register/refresh liveness without changing subscription state.
        private void SendKeepalive()
        {
            Send(BuildA2Frame((byte)ICD.FRAME_KEEPALIVE));
            Debug.WriteLine("TRC: keepalive (0xA4) sent");
            _lastKeepalive = DateTime.UtcNow;
        }

        // -------------------------------------------------------------------
        // ICD commands — verified against ICD v4.1.0
        // -------------------------------------------------------------------

        // 0xA0 SET_UNSOLICITED — register (true) or deregister (false) for unsolicited stream
        public bool UnsolicitedMode
        {
            set { Send(BuildA2Frame((byte)ICD.SET_UNSOLICITED, new[] { (byte)(value ? 1 : 0) })); }
        }

        // 0xA2 SET_NTP_CONFIG
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
        public void SetSystemState(SYSTEM_STATES state)
        {
            Send(BuildA2Frame((byte)ICD.SET_SYSTEM_STATE, new[] { (byte)state }));
        }

        // 0xA6 SET_GIMBAL_MODE
        public void SetGimbalMode(BDC_MODES mode)
        {
            Send(BuildA2Frame((byte)ICD.SET_GIMBAL_MODE, new[] { (byte)mode }));
        }

        // 0xD0 ORIN_CAM_SET_ACTIVE — select active camera (VIS/MWIR)
        public void SetActiveCamera(BDC_CAM_IDS id)
        {
            Send(BuildA2Frame((byte)ICD.ORIN_CAM_SET_ACTIVE, new[] { (byte)id }));
        }

        // 0xD3 ORIN_SET_STREAM_OVERLAYS — send HUD_OVERLAY_FLAGS bitmask directly
        public void SetOverlayMask(byte mask)
        {
            Send(BuildA2Frame((byte)ICD.ORIN_SET_STREAM_OVERLAYS, new[] { mask }));
        }

        // 0xD4 ORIN_ACAM_SET_CUE_FLAG
        public bool CueFlag
        {
            set { Send(BuildA2Frame((byte)ICD.ORIN_ACAM_SET_CUE_FLAG, new[] { (byte)(value ? 1 : 0) })); }
        }

        // 0xD5 ORIN_ACAM_SET_TRACKGATE_SIZE — set trackgate dimensions in pixels
        public void SetTrackGateSize(byte w, byte h)
        {
            Send(BuildA2Frame((byte)ICD.ORIN_ACAM_SET_TRACKGATE_SIZE, new[] { w, h }));
        }

        // 0xD7 ORIN_ACAM_SET_TRACKGATE_CENTER — set trackgate center (uint16 x LE, uint16 y LE)
        // Use SetTrackGateSize() separately to change dimensions.
        public void setTrackBox(Point pt)
        {
            byte[] px = BitConverter.GetBytes((UInt16)pt.X);
            byte[] py = BitConverter.GetBytes((UInt16)pt.Y);
            Send(BuildA2Frame((byte)ICD.ORIN_ACAM_SET_TRACKGATE_CENTER,
                new[] { px[0], px[1], py[0], py[1] }));
        }

        // 0xDB ORIN_ACAM_ENABLE_TRACKERS — enable/disable tracker for active camera
        public void SetTrackerEnable(BDC_TRACKERS tracker, bool en)
        {
            Send(BuildA2Frame((byte)ICD.ORIN_ACAM_ENABLE_TRACKERS,
                new[] { (byte)tracker, (byte)(en ? 1 : 0) }));
        }

        // 0xDB ORIN_ACAM_ENABLE_TRACKERS — ICD v4.1.0: 3rd byte mosseReseed
        // NCC-gated MOSSE template reseed from LK bbox. LK tracker only.
        public void SetTrackerEnable(BDC_TRACKERS tracker, bool en, bool mosseReseed)
        {
            Send(BuildA2Frame((byte)ICD.ORIN_ACAM_ENABLE_TRACKERS,
                new[] { (byte)tracker, (byte)(en ? 1 : 0), (byte)(mosseReseed ? 1 : 0) }));
        }

        // 0xC4 CMD_VIS_AWB — trigger VIS auto white balance once, no payload (CB-20260416e)
        public void TriggerAWB()
        {
            Send(BuildA2Frame((byte)ICD.CMD_VIS_AWB));
        }

        // 0xE0 SET_BCAST_FIRECONTROL_STATUS — fire control vote bytes (INT_ENG)
        // voteBitsMcc: VOTE_BITS_MCC bitmask; voteBitsBdc: VOTE_BITS_BDC bitmask
        public void SetFireStatus(byte voteBitsMcc, byte voteBitsBdc = 0)
        {
            Send(BuildA2Frame((byte)ICD.SET_BCAST_FIRECONTROL_STATUS,
                new[] { voteBitsMcc, voteBitsBdc }));
        }
    }
}
