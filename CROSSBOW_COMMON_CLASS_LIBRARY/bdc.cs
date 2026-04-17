// bdc.cs  —  CROSSBOW BDC controller class — A2 (ENG GUI) and A3 (THEIA HMI)
//
// Transport is selected at construction via TransportPath:
//   THEIA:    new BDC(log, TransportPath.A3_External)  — port 10050, magic 0xCB 0x58
//   ENG GUI:  new BDC(log, TransportPath.A2_Internal)  — port 10018, magic 0xCB 0x49
//
// INT_ENG commands (VicorEnabled, EnableRelay, SetOverrideVote, GimbalSetHome,
//   FMC_SET_FSM_SIGNS, FSMTestScan, STAGE_ENABLED, STAGE_CALIBRATE, SEND_LCH_PRINT)
//   are guarded — calling them on an A3 instance logs a warning and does nothing.
//   Firmware rejects INT commands on A3 regardless, but the guard catches mistakes early.
//
// MSG classes are in separate files:
//   MSG_BDC.cs, MSG_GIMBAL.cs, MSG_TRC.cs, MSG_FMC.cs, CAMERA.cs

using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace CROSSBOW
{
    public class BDC
    {
        public enum READY_STATUS { ALIVE, READY, WARN, ERROR, NA }
        public enum BDC_FOCUS_DIR { NEAR = -1, FAR = 1 }

        // ── Frame constants ───────────────────────────────────────────────────
        private const byte MAGIC_HI           = 0xCB;
        private const int  FRAME_RESPONSE_LEN = 521;
        private const int  PAYLOAD_OFFSET     = 7;
        private const int  PAYLOAD_LEN        = 512;
        private const byte STATUS_OK          = 0x00;

        // Transport-dependent computed properties
        private byte   MagicLo    => Transport == TransportPath.A3_External ? (byte)0x58     : (byte)0x49;
        private int    ActivePort => Transport == TransportPath.A3_External ? 10050           : 10018;
        private string LocalIP    => Transport == TransportPath.A3_External
                                        ? CrossbowNic.GetExternalIP()
                                        : CrossbowNic.GetInternalIP();

        // Keepalive — send FRAME_KEEPALIVE (0xA4) every 30 s to stay within the
        // firmware's 60-second liveness window (frame.hpp CLIENT_TIMEOUT_MS).
        private const int KEEPALIVE_INTERVAL_MS = 30_000;

        public string IP   { get; private set; } = IPS.BDC;
        public int    Port => ActivePort;

        // ── Transport + Logger ────────────────────────────────────────────────
        public TransportPath Transport           { get; private set; }
        public bool          isVerboseLogEnabled { get; set; } = true;
        private ILogger      Log                 { get; set; }

        // ── State ─────────────────────────────────────────────────────────────
        private UdpClient?               udpClient;
        private IPEndPoint?              ipEndPoint;
        private IPEndPoint?              _remoteEP;
        private CancellationTokenSource? ts;
        private CancellationToken        ct;
        private bool                     _isStarted     = false;
        private byte                     _seq           = 0;
        private DateTime                 _lastKeepalive = DateTime.MinValue;
        private bool _wasConnected = false;
        private DateTime _connectedSince = DateTime.MinValue;
        private DateTime _dropTime = DateTime.MinValue; 
        private int _dropCount = 0;

        public DateTime ConnectedSince { get { return _connectedSince; } }
        public int DropCount { get { return _dropCount; } }
        public DateTime lastMsgRx  { get; private set; } = DateTime.UtcNow;
        public double HB_RX_ms { get; private set; } = 0;
        public bool     isConnected { get; private set; } = false;

        public MSG_BDC   LatestMSG { get; private set; }
        public BDC_MODES Mode      { get { return LatestMSG.Mode; } }

        // ── Constructors ──────────────────────────────────────────────────────
        public BDC(ILogger _log, TransportPath transport = TransportPath.A3_External)
        {
            Log       = _log;
            Transport = transport;
            LatestMSG = new MSG_BDC(Log, transport);
        }

        public BDC(TransportPath transport = TransportPath.A3_External)
        {
            Transport = transport;
            LatestMSG = new MSG_BDC(transport);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        public void Start()
        {
            if (_isStarted) return;
            _isStarted = true;
            ts = new CancellationTokenSource();
            ct = ts.Token;
            _seq = (byte)new Random().Next(33, 224);
            Log?.Information("BDC starting ({Transport})", Transport);
            //_ = Task.Run(async () => await backgroundUDPRead(), ct);
            _ = backgroundUDPRead();
            _ = KeepaliveLoop();
        }

        public void Stop()
        {
            Log?.Information("BDC stopping");
            _isStarted  = false;
            isConnected = false;
            ts?.Cancel();
        }

        // ── Receive loop ──────────────────────────────────────────────────────
        private async Task backgroundUDPRead()
        {
            try
            {
                if (udpClient != null) { udpClient.Close(); udpClient = null; }

                if (Transport == TransportPath.A3_External)
                {
                    // A3: bind to external IP — firmware enforces src .200–.254
                    udpClient = new UdpClient();
                    udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(LocalIP), 0));
                    ipEndPoint = new IPEndPoint(IPAddress.Parse(IP), ActivePort);
                }
                else
                {
                    // A2: bind to internal NIC (<100) so TMC/FMC firmware accepts source IP
                    udpClient = new UdpClient();
                    udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(LocalIP), 0));
                    _remoteEP = new IPEndPoint(IPAddress.Parse(IP), ActivePort);
                    udpClient.Connect(_remoteEP);

                    // Single registration frame — firmware replay fix handles reconnects cleanly
                    Send(BuildFrame((byte)ICD.FRAME_KEEPALIVE));
                    _lastKeepalive = DateTime.UtcNow;
                    Debug.WriteLine("BDC: A2 registration sent (0xA4)");
                }
            }
            catch (Exception ex)
            {
                Log?.Error("BDC socket init failed: {Ex}", ex.Message);
                _isStarted = false;
                return;
            }

            if (Transport == TransportPath.A3_External)
            {
                Send(BuildFrame((byte)ICD.FRAME_KEEPALIVE));
                _lastKeepalive = DateTime.UtcNow;
                Debug.WriteLine("BDC: A3 registration sent (0xA4)");
            }

            Log?.Information("BDC UDP connected ({LocalIp}:{Port} → {RemoteIp})",
                LocalIP, ActivePort, IP);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var res = await udpClient.ReceiveAsync(ct).ConfigureAwait(false);

                    if (Transport == TransportPath.A3_External)
                    {
                        // A3: pass full frame — MSG_BDC.ParseA3 validates internally
                        if (!res.RemoteEndPoint.Address.Equals(IPAddress.Parse(IP))) continue;
                        byte[] rxBuff = res.Buffer;
                        if (rxBuff.Length == FRAME_RESPONSE_LEN)
                        {
                            isConnected = true;
                            if (!_wasConnected)
                            {
                                _wasConnected = true;
                                _connectedSince = DateTime.UtcNow;
                                Log?.Information("BDC: connection established");
                                Debug.WriteLine("BDC: connection established");
                            }
                            var now = DateTime.UtcNow;
                            HB_RX_ms = (now - lastMsgRx).TotalMilliseconds;
                            lastMsgRx = now;
                            LatestMSG.Parse(rxBuff);
                        }
                    }
                    else
                    {
                        // A2: validate frame, strip to payload, pass to ParseA2
                        byte[] frame = res.Buffer;
                        if (frame.Length == FRAME_RESPONSE_LEN
                            && frame[0] == MAGIC_HI && frame[1] == MagicLo
                            && frame[4] == STATUS_OK
                            && CrcHelper.Crc16(frame, FRAME_RESPONSE_LEN - 2)
                               == (ushort)((frame[519] << 8) | frame[520]))
                        {
                            isConnected = true;
                            if (!_wasConnected)
                            {
                                _wasConnected = true;
                                _connectedSince = DateTime.UtcNow;
                                Log?.Information("BDC: connection established");
                                Debug.WriteLine("BDC: connection established");
                            }
                            HB_RX_ms = (DateTime.UtcNow - lastMsgRx).TotalMilliseconds;
                            lastMsgRx = DateTime.UtcNow;

                            if (frame[3] == 0x00 || frame[3] == 0xA1)  // REG1 CMD_BYTE: 0x00 (v4.0.0) | 0xA1 (legacy pre-FW-C10)
                            {
                                byte[] payload = new byte[PAYLOAD_LEN];
                                Array.Copy(frame, PAYLOAD_OFFSET, payload, 0, PAYLOAD_LEN);
                                LatestMSG.Parse(payload);
                            }
                            else
                            {
                                Debug.WriteLine($"BDC: A2 ACK rx CMD=0x{frame[3]:X2}");
                            }
                        }
                        else
                        {
                            ushort computed = CrcHelper.Crc16(frame, FRAME_RESPONSE_LEN - 2);
                            ushort received = frame.Length >= FRAME_RESPONSE_LEN
                                ? (ushort)((frame[519] << 8) | frame[520]) : (ushort)0;
                            Debug.WriteLine($"BDC RX FAIL: len={frame.Length} magic=0x{frame[0]:X2}{frame[1]:X2} STATUS=0x{frame[4]:X2} CRC computed=0x{computed:X4} received=0x{received:X4}");
                        }
                    }
                }
            }
            catch (OperationCanceledException) { /* clean shutdown */ }
            catch (Exception ex)
            {
                Log?.Warning("BDC receive error: {Ex}", ex.Message);
            }
            finally
            {
                udpClient?.Close();
                udpClient   = null;
                isConnected = false;
                Log?.Information("BDC UDP closed");
            }
        }

        // ── Frame builder ─────────────────────────────────────────────────────
        private byte[] BuildFrame(byte cmd, byte[]? payload = null)
        {
            payload ??= Array.Empty<byte>();
            ushort plen     = (ushort)payload.Length;
            int    frameLen = 6 + plen + 2;
            byte[] frame    = new byte[frameLen];
            frame[0] = MAGIC_HI;
            frame[1] = MagicLo;
            frame[2] = _seq++;
            frame[3] = cmd;
            frame[4] = (byte)(plen & 0xFF);
            frame[5] = (byte)(plen >> 8);
            if (plen > 0)
                Buffer.BlockCopy(payload, 0, frame, 6, plen);
            ushort crc = CrcHelper.Crc16(frame, frameLen - 2);
            frame[frameLen - 2] = (byte)(crc >> 8);
            frame[frameLen - 1] = (byte)(crc & 0xFF);
            return frame;
        }

        private void Send(byte cmd, byte[]? payload = null)
        {
            Send(BuildFrame(cmd, payload));
        }

        private void Send(byte[] frame)
        {
            if (udpClient == null) return;
            try
            {
                if (Transport == TransportPath.A3_External && ipEndPoint != null)
                    udpClient.Send(frame, frame.Length, ipEndPoint);
                else
                    udpClient.Send(frame);
            }
            catch (Exception ex)
            {
                Log?.Warning("BDC: send error: {Ex}", ex.Message);
                Debug.WriteLine($"BDC: send error: {ex.Message}");
            }
        }

        // ── INT_ENG command guard ─────────────────────────────────────────────
        private bool AssertIntEng(string cmdName)
        {
            if (Transport == TransportPath.A3_External)
            {
                Log?.Warning("BDC: {Cmd} is INT_ENG only — not sent on A3 transport", cmdName);
                Debug.WriteLine($"BDC: {cmdName} blocked — A3 transport does not support INT_ENG commands");
                return false;
            }
            return true;
        }

        // ── Keepalive / staleness watchdog ────────────────────────────────────
        private const double STALE_WARN_MS = 2000.0;   // warn after 2 s of no telemetry

        private async Task KeepaliveLoop()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(KEEPALIVE_INTERVAL_MS));
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    SendKeepalive();

                    // ── Connection state tracking ─────────────────────────────
                    bool stale = isConnected &&
                        (DateTime.UtcNow - lastMsgRx).TotalMilliseconds > STALE_WARN_MS;

                    if (isConnected && !_wasConnected)
                    {
                        var downTime = (_dropCount > 0 && _dropTime != DateTime.MinValue)
                            ? (DateTime.UtcNow - _dropTime).TotalSeconds
                            : 0.0;
                        _connectedSince = DateTime.UtcNow;
                        _wasConnected = true;
                        if (_dropCount > 0)
                        {
                            Log?.Information("BDC: connection restored — was down {DownTime:0.0}s",
                                downTime);
                            Debug.WriteLine($"BDC: connection restored — was down {downTime:0.0}s");
                        }
                    }

                    if (stale && _wasConnected)
                    {
                        _dropTime = DateTime.UtcNow;
                        _dropCount++;
                        _wasConnected = false;
                        Log?.Warning("BDC: connection lost — drop #{Count} after {Uptime:0.0}s uptime",
                            _dropCount,
                            (DateTime.UtcNow - _connectedSince).TotalSeconds);
                        Debug.WriteLine($"BDC: connection lost — drop #{_dropCount} after {(DateTime.UtcNow - _connectedSince).TotalSeconds:0.0}s uptime");
                    }
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
        }

        private void SendKeepalive()
        {
            Send(BuildFrame((byte)ICD.FRAME_KEEPALIVE));
            _lastKeepalive = DateTime.UtcNow;
            Log?.Information("BDC: keepalive (0xA4) sent");
        }

        // ── Status aggregates ─────────────────────────────────────────────────
        public DateTime BDC_TIME_UTC { get { return LatestMSG.epochTime.ToUniversalTime(); } }
        public bool BDC_STATUS  { get { return LatestMSG.RX_HB > 10 && LatestMSG.RX_HB < 60.0; } }
        public bool GIM_STATUS  { get { return LatestMSG.gimbalMSG.isReady && LatestMSG.gimbalMSG.isStarted && LatestMSG.gimbalMSG.isConnected; } }
        public bool FMC_STATUS  { get { return LatestMSG.fmcMSG.isReady && LatestMSG.fmcMSG.HB_ms > 10 && LatestMSG.fmcMSG.HB_ms < 30; } }
        public bool VIS_STATUS  { get { return LatestMSG.trcMSG.Cameras[(int)BDC_CAM_IDS.VIS].isCapturing; } }
        public bool MWIR_STATUS { get { return LatestMSG.trcMSG.Cameras[(int)BDC_CAM_IDS.MWIR].isCapturing; } }

        public bool TRC_STATUS
        {
            get
            {
                return (LatestMSG.trcMSG.HB_TX_ms > 5.0 && LatestMSG.trcMSG.HB_TX_ms < 20.0)
                    && (LatestMSG.trcMSG.deviceTemperature <= 70 && LatestMSG.trcMSG.jetsonTemp <= 85)
                    && (LatestMSG.trcMSG.isReady && LatestMSG.trcMSG.isStarted && LatestMSG.trcMSG.isConnected);
            }
        }

        // ── INT_OPS commands — available on both A2 and A3 ───────────────────
        // 0xA2 SET_NTP_CONFIG (INT only, A2 path only)
        // 0 bytes  = force resync on current server
        // 1 byte   = set primary server last octet + resync
        // 2 bytes  = set primary + fallback last octets + resync
        public void SetNtpConfig(byte? primaryOctet = null, byte? fallbackOctet = null)
        {
            byte[] payload = primaryOctet == null ? Array.Empty<byte>() :
                             fallbackOctet == null ? new[] { primaryOctet.Value } :
                                                     new[] { primaryOctet.Value, fallbackOctet.Value };
            Send(BuildFrame((byte)ICD.SET_NTP_CONFIG, payload));
        }

        // 0xA0 SET_UNSOLICITED
        public bool UnsolicitedMode
        {
            set { Send((byte)ICD.SET_UNSOLICITED, new byte[] { Convert.ToByte(value) }); }
        }

        // 0xA5 SET_SYSTEM_STATE
        public void SetState(SYSTEM_STATES state)
        {
            Send((byte)ICD.SET_SYSTEM_STATE, new byte[] { Convert.ToByte(state) });
        }

        // 0xA6 SET_GIMBAL_MODE
        public void SetMode(BDC_MODES mode)
        {
            Send((byte)ICD.SET_GIMBAL_MODE, new byte[] { Convert.ToByte(mode) });
        }

        // ORIN_CAM_SET_ACTIVE
        public void SetActiveCamera(BDC_CAM_IDS id)
        {
            Send((byte)ICD.ORIN_CAM_SET_ACTIVE, new byte[] { (byte)id });
        }

        // 0xA9 SET_REINIT — unified controller reinitialise, INT_OPS (v4.0.0)
        public void ReInitDevice(BDC_DEVICES dev)
        {
            Send((byte)ICD.SET_REINIT, new byte[] { Convert.ToByte(dev) });
        }

        // SET_GIM_SPD — jog gimbal
        public void Jog(Int32 x, Int32 y)
        {
            byte[] payload = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, payload, 4, 4);
            Send((byte)ICD.SET_GIM_SPD, payload);
        }

        // CMD_GIM_PARK
        public void GimbalPark() { Send((byte)ICD.CMD_GIM_PARK); }

        // SET_GIM_POS
        public void GimbalSetPOS(Int32 x, Int32 y)
        {
            byte[] payload = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, payload, 4, 4);
            Send((byte)ICD.SET_GIM_POS, payload);
        }

        // SET_SYS_ATT
        public void SetPlatformATT(Single r, Single p, Single y)
        {
            byte[] payload = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes(r), 0, payload, 0,  4);
            Buffer.BlockCopy(BitConverter.GetBytes(p), 0, payload, 4,  4);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, payload, 8,  4);
            Send((byte)ICD.SET_SYS_ATT, payload);
        }

        // SET_SYS_LLA
        public void SetPlatformLLA(Single lat, Single lng, Single alt)
        {
            byte[] payload = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes(lat), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(lng), 0, payload, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(alt), 0, payload, 8, 4);
            Send((byte)ICD.SET_SYS_LLA, payload);
        }

        // SET_PID_GAINS
        public void SetPIDGains(byte which, Single pkp, Single pki, Single pkd,
                                            Single tkp, Single tki, Single tkd)
        {
            byte[] payload = new byte[25];
            int ndx = 0;
            payload[ndx] = which; ndx++;
            Buffer.BlockCopy(BitConverter.GetBytes(pkp), 0, payload, ndx, 4); ndx += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(pki), 0, payload, ndx, 4); ndx += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(pkd), 0, payload, ndx, 4); ndx += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(tkp), 0, payload, ndx, 4); ndx += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(tki), 0, payload, ndx, 4); ndx += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(tkd), 0, payload, ndx, 4);
            Send((byte)ICD.SET_PID_GAINS, payload);
        }

        // SET_PID_ENABLE
        public bool EnableCUETrack   { set { Send((byte)ICD.SET_PID_ENABLE, new byte[] { 0x00, Convert.ToByte(value) }); } }
        public bool EnableVideoTrack { set { Send((byte)ICD.SET_PID_ENABLE, new byte[] { 0x01, Convert.ToByte(value) }); } }

        // SET_PID_TARGET
        public void SetPIDCUETargetNED(Single az, Single el, Single spd)
        {
            byte[] payload = new byte[13];
            int ndx = 0;
            payload[ndx] = 0x00; ndx++;
            Buffer.BlockCopy(BitConverter.GetBytes(az),  0, payload, ndx, 4); ndx += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(el),  0, payload, ndx, 4); ndx += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(spd), 0, payload, ndx, 4);
            Send((byte)ICD.SET_PID_TARGET, payload);
        }

        // Camera commands
        public void SetAcamMag(byte mag)  { Send((byte)ICD.SET_CAM_MAG,  new byte[] { mag }); }
        public void SetAcamIris(byte pos) { Send((byte)ICD.SET_CAM_IRIS, new byte[] { pos }); }

        public void SetAcamFocus(ushort pos)
        {
            Send((byte)ICD.SET_CAM_FOCUS, BitConverter.GetBytes(pos));
        }
        public void TriggerAWB() { Send((byte)ICD.CMD_VIS_AWB); }
        public bool VIS_FILTER_ENABLE            { set { Send((byte)ICD.CMD_VIS_FILTER_ENABLE,       new byte[] { Convert.ToByte(value) }); } }
        public bool MWIR_WhiteHot                { set { Send((byte)ICD.SET_MWIR_WHITEHOT,           new byte[] { Convert.ToByte(value) }); } }
        public AF_MODES MWIR_SET_AF_MODE         { set { Send((byte)ICD.CMD_MWIR_AF_MODE,            new byte[] { Convert.ToByte(value) }); } }
        public BDC_FOCUS_DIR MWIR_BUMP_FOCUS     { set { Send((byte)ICD.CMD_MWIR_BUMP_FOCUS,         new byte[] { Convert.ToByte(value < 0 ? 0 : 1) }); } }

        // CMD_MWIR_NUC1 — rate-gated to 5 minute minimum interval
        private DateTime _lastNUC_request = DateTime.UtcNow;
        public void MWIR_NUC1()
        {
            if ((DateTime.UtcNow - _lastNUC_request).TotalMinutes > 5)
            {
                Send((byte)ICD.CMD_MWIR_NUC1);
                _lastNUC_request = DateTime.UtcNow;
            }
            else
            {
                if (isVerboseLogEnabled) Log?.Debug("MWIR_NUC1: too soon — must wait 5 min between NUCs");
                Debug.WriteLine("BDC: NUC rejected — must wait 5 minutes between NUCs");
            }
        }

        // Tracker commands
        public void SetAcamTrackerEnable(BDC_TRACKERS tracker, bool en)
        {
            Send((byte)ICD.ORIN_ACAM_ENABLE_TRACKERS,
                new byte[] { Convert.ToByte(tracker), Convert.ToByte(en) });
        }

        public void SetAITrackPriority(byte num)
        {
            Send((byte)ICD.ORIN_ACAM_COCO_CLASS_FILTER, new byte[] { num });
        }

        public bool EnableCUEFlag               { set { Send((byte)ICD.ORIN_ACAM_SET_CUE_FLAG,        new byte[] { Convert.ToByte(value) }); } }
        public void ResetTrackB()               { Send((byte)ICD.ORIN_ACAM_RESET_TRACKB); }
        public bool SetAcamFocusScoreActiveFlag { set { Send((byte)ICD.ORIN_ACAM_ENABLE_FOCUSSCORE,   new byte[] { Convert.ToByte(value) }); } }

        public void SetTrackGateSize(Size gate)
        {
            Send((byte)ICD.ORIN_ACAM_SET_TRACKGATE_SIZE,
                new byte[] { Convert.ToByte(gate.Width), Convert.ToByte(gate.Height) });
        }

        public void SetTrackGateCenter(Point ctr)
        {
            byte[] payload = new byte[4];
            Buffer.BlockCopy(BitConverter.GetBytes((UInt16)ctr.X), 0, payload, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((UInt16)ctr.Y), 0, payload, 2, 2);
            Send((byte)ICD.ORIN_ACAM_SET_TRACKGATE_CENTER, payload);
        }

        // Offset commands
        public void SetCUEOffset(float dx, float dy)
        {
            byte[] payload = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(dx), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(dy), 0, payload, 4, 4);
            Send((byte)ICD.SET_CUE_OFFSET, payload);
        }

        public void SetATOffset(sbyte dx, sbyte dy) { Send((byte)ICD.ORIN_ACAM_SET_ATOFFSET, new byte[] { (byte)dx, (byte)dy }); }
        public void SetFTOffset(sbyte dx, sbyte dy) { Send((byte)ICD.ORIN_ACAM_SET_FTOFFSET, new byte[] { (byte)dx, (byte)dy }); }

        // Overlay / view commands
        public void SetOverlayBitmask(byte mask) { Send((byte)ICD.ORIN_SET_STREAM_OVERLAYS, new byte[] { mask }); }
        public void SetViewMode(VIEW_MODES mode)  { Send((byte)ICD.ORIN_SET_VIEW_MODE, new byte[] { (byte)mode }); }

        public void SetOSD(bool enable)
        {
            byte current = LatestMSG.trcMSG.overlayMask;
            byte updated = HudOverlay.Set(current, HUD_OVERLAY_FLAGS.OSD, enable);
            SetOverlayBitmask(updated);
        }

        public void SetPIP(bool enable)
        {
            if (enable)
                SetViewMode(VIEW_MODES.PIP4);
            else
                SetViewMode(LatestMSG.trcMSG.Active_CAM == BDC_CAM_IDS.MWIR
                    ? VIEW_MODES.CAM2
                    : VIEW_MODES.CAM1);
        }

        // FSM commands
        public void FMC_SET_FSM_HOME(short x, short y)
        {
            byte[] payload = new byte[4];
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, payload, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, payload, 2, 2);
            Send((byte)ICD.BDC_SET_FSM_HOME, payload);
        }

        public void FMC_SET_FSM_POS(short x, short y)
        {
            byte[] payload = new byte[4];
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, payload, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, payload, 2, 2);
            Send((byte)ICD.FMC_SET_FSM_POS, payload);
        }

        public void FMC_SET_FSM_IFOVS(float x, float y)
        {
            byte[] payload = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, payload, 4, 4);
            Send((byte)ICD.BDC_SET_FSM_IFOVS, payload);
        }

        public void FMC_SET_TRACK_ENABLE(bool en)
        {
            Send((byte)ICD.BDC_SET_FSM_TRACK_ENABLE, new byte[] { Convert.ToByte(en) });
        }

        public void FMC_SET_STAGE_POSITION(UInt32 p)
        {
            Send((byte)ICD.FMC_SET_STAGE_POS, BitConverter.GetBytes(p));
        }

        // Horizon commands
        public void LoadHorizon(UInt16 iaz, float el)
        {
            byte[] payload = new byte[6];
            Buffer.BlockCopy(BitConverter.GetBytes(iaz), 0, payload, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(el),  0, payload, 2, 4);
            Send((byte)ICD.SET_BDC_HORIZ, payload);
        }

        public void PrintHorizon()        { Send((byte)ICD.SET_BDC_HORIZ); }

        public void SET_HORIZON_BUFFER(Single b)
        {
            Send((byte)ICD.SET_BDC_HORIZ, BitConverter.GetBytes(b));
        }

        // LCH commands
        public void SET_LCH_VOTE(LCH.FILETYPE fileType, bool operatorValid,
                                  bool locationValid, bool forExec)
        {
            Send((byte)ICD.SET_BDC_PALOS_VOTE,
                new byte[] { Convert.ToByte(fileType),
                             Convert.ToByte(operatorValid),
                             Convert.ToByte(locationValid),
                             Convert.ToByte(forExec) });
        }

        public void SEND_LCH_MISSION_DATA(LCH aLCH)
        {
            UInt64 t1  = (UInt64)(new DateTimeOffset(aLCH.MissionStartDateTime.ToUniversalTime()).ToUnixTimeSeconds());
            UInt64 t2  = (UInt64)(new DateTimeOffset(aLCH.MissionEndDateTime.ToUniversalTime()).ToUnixTimeSeconds());
            UInt16 az1 = Convert.ToUInt16(aLCH.Az1);
            UInt16 az2 = Convert.ToUInt16(aLCH.Az2);
            Int16  el1 = Convert.ToInt16(aLCH.El1);
            Int16  el2 = Convert.ToInt16(aLCH.El2);
            UInt16 nt  = Convert.ToUInt16(aLCH.NumberTarget);
            UInt16 nw  = Convert.ToUInt16(aLCH.NumberWindows);

            if (isVerboseLogEnabled) Log?.Debug("LCH mission t1={T1} t2={T2}", t1, t2);

            byte[] payload = new byte[30];
            int ndx = 0;
            payload[ndx] = (byte)aLCH.FileType; ndx++;
            payload[ndx] = 1;                    ndx++;   // isValid
            Buffer.BlockCopy(BitConverter.GetBytes(t1),  0, payload, ndx, sizeof(UInt64)); ndx += sizeof(UInt64);
            Buffer.BlockCopy(BitConverter.GetBytes(t2),  0, payload, ndx, sizeof(UInt64)); ndx += sizeof(UInt64);
            Buffer.BlockCopy(BitConverter.GetBytes(az1), 0, payload, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(el1), 0, payload, ndx, sizeof(Int16));  ndx += sizeof(Int16);
            Buffer.BlockCopy(BitConverter.GetBytes(az2), 0, payload, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(el2), 0, payload, ndx, sizeof(Int16));  ndx += sizeof(Int16);
            Buffer.BlockCopy(BitConverter.GetBytes(nt),  0, payload, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(nw),  0, payload, ndx, sizeof(UInt16));
            Send((byte)ICD.SET_LCH_MISSION_DATA, payload);
        }

        public void SEND_LCH_TARGET_WITH_WINDOWS(LCH aLCH, UInt16 targetIndex)
        {
            LCH_TARGET aTarget = aLCH.LCH_Targets[targetIndex];
            UInt16 t1  = (UInt16)((aTarget.StartDateTime - aLCH.MissionStartDateTime).TotalSeconds);
            UInt16 t2  = (UInt16)((aTarget.EndDateTime   - aLCH.MissionStartDateTime).TotalSeconds);
            UInt16 az1 = Convert.ToUInt16(aTarget.Az1);
            UInt16 az2 = Convert.ToUInt16(aTarget.Az2);
            Int16  el1 = Convert.ToInt16(aTarget.El1);
            Int16  el2 = Convert.ToInt16(aTarget.El2);
            float  flat = (Single)aTarget.Latitude;
            float  flng = (Single)aTarget.Longitude;
            float  falt = (Single)aTarget.Altitude;

            int windowBytes = aTarget.LCH_Windows.Count * 2 * sizeof(UInt16);
            byte[] payload = new byte[29 + windowBytes];
            int ndx = 0;
            payload[ndx] = (byte)aLCH.FileType; ndx++;
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)aTarget.LCH_Windows.Count), 0, payload, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(t1),   0, payload, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(t2),   0, payload, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(az1),  0, payload, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(el1),  0, payload, ndx, sizeof(Int16));  ndx += sizeof(Int16);
            Buffer.BlockCopy(BitConverter.GetBytes(az2),  0, payload, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(el2),  0, payload, ndx, sizeof(Int16));  ndx += sizeof(Int16);
            Buffer.BlockCopy(BitConverter.GetBytes(flat), 0, payload, ndx, sizeof(Single)); ndx += sizeof(Single);
            Buffer.BlockCopy(BitConverter.GetBytes(flng), 0, payload, ndx, sizeof(Single)); ndx += sizeof(Single);
            Buffer.BlockCopy(BitConverter.GetBytes(falt), 0, payload, ndx, sizeof(Single)); ndx += sizeof(Single);

            foreach (LCH_WINDOW win in aTarget.LCH_Windows)
            {
                UInt16 wt1 = (UInt16)((win.StartDateTime - aLCH.MissionStartDateTime).TotalSeconds);
                UInt16 wt2 = (UInt16)((win.EndDateTime   - aLCH.MissionStartDateTime).TotalSeconds);
                Buffer.BlockCopy(BitConverter.GetBytes(wt1), 0, payload, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
                Buffer.BlockCopy(BitConverter.GetBytes(wt2), 0, payload, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            }
            Send((byte)ICD.SET_LCH_TARGET_DATA, payload);
        }

        public async Task LCH_UPLOAD(IProgress<int> progress, LCH aLCH)
        {
            SEND_LCH_MISSION_DATA(aLCH);
            await Task.Delay(20, ct).ConfigureAwait(false);
            progress?.Report(5);

            ushort ti = 0;
            foreach (LCH_TARGET aTarget in aLCH.LCH_Targets)
            {
                if (isVerboseLogEnabled) Log?.Debug("LCH upload: sending target {Idx}", ti);
                SEND_LCH_TARGET_WITH_WINDOWS(aLCH, ti);
                await Task.Delay(20, ct).ConfigureAwait(false);
                int pct = 5 + (int)(((double)ti / aLCH.NumberTarget) * 95);
                progress?.Report(pct);
                ti++;
            }
        }

        public void Check_PALOS_Vote(LCH.FILETYPE fileType, DateTime currentTime, float az, float el)
        {
            UInt64 t1 = (UInt64)(new DateTimeOffset(currentTime).ToUnixTimeSeconds());
            if (isVerboseLogEnabled) Log?.Debug("BDC PALOS vote check at {T1}", t1);
            byte[] payload = new byte[17];
            int ndx = 0;
            payload[ndx] = (byte)fileType; ndx++;
            Buffer.BlockCopy(BitConverter.GetBytes(t1), 0, payload, ndx, sizeof(UInt64)); ndx += sizeof(UInt64);
            Buffer.BlockCopy(BitConverter.GetBytes(az), 0, payload, ndx, sizeof(Single)); ndx += sizeof(Single);
            Buffer.BlockCopy(BitConverter.GetBytes(el), 0, payload, ndx, sizeof(Single));
            Send((byte)ICD.GET_BDC_PALOS_VOTE, payload);
        }

        // ── INT_ENG commands — A2 only, guarded ──────────────────────────────

        // SET_BDC_VICOR_ENABLE
        public bool VicorEnabled
        {
            set
            {
                if (!AssertIntEng("VicorEnabled")) return;
                Send((byte)ICD.SET_BDC_VICOR_ENABLE, new byte[] { Convert.ToByte(value) });
            }
        }

        // SET_BDC_RELAY_ENABLE
        public void EnableRelay(int w, bool en)
        {
            if (!AssertIntEng("EnableRelay")) return;
            Send((byte)ICD.SET_BDC_RELAY_ENABLE, new byte[] { (byte)w, Convert.ToByte(en) });
        }

        // 0xAA SET_DEVICES_ENABLE — unified device enable, INT_OPS (v4.0.0)
        public void EnableDevice(BDC_DEVICES dev, bool en)
        {
            Send((byte)ICD.SET_DEVICES_ENABLE, new byte[] { (byte)dev, (byte)(en ? 1 : 0) });
        }

        // SET_BDC_VOTE_OVERRIDE
        public void SetOverrideVote(BDC_VOTE_OVERRIDES vote, bool val)
        {
            if (!AssertIntEng("SetOverrideVote")) return;
            Send((byte)ICD.SET_BDC_VOTE_OVERRIDE, new byte[] { (byte)vote, Convert.ToByte(val) });
        }

        // BDC_SET_FSM_SIGNS
        public void FMC_SET_FSM_SIGNS(sbyte x, sbyte y)
        {
            if (!AssertIntEng("FMC_SET_FSM_SIGNS")) return;
            Send((byte)ICD.BDC_SET_FSM_SIGNS, new byte[] { (byte)x, (byte)y });
        }

        // FMC_FSM_TEST_SCAN
        public void FSMTestScan()
        {
            if (!AssertIntEng("FSMTestScan")) return;
            Send((byte)ICD.FMC_FSM_TEST_SCAN);
        }

        // FMC_SET_STAGE_ENABLE
        public bool STAGE_ENABLED
        {
            set
            {
                if (!AssertIntEng("STAGE_ENABLED")) return;
                Send((byte)ICD.FMC_SET_STAGE_ENABLE, new byte[] { Convert.ToByte(value) });
            }
        }

        // FMC_STAGE_CALIB
        public void STAGE_CALIBRATE()
        {
            if (!AssertIntEng("STAGE_CALIBRATE")) return;
            Send((byte)ICD.FMC_STAGE_CALIB);
        }
    }
}
