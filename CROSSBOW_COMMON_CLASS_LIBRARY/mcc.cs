// mcc.cs  —  CROSSBOW MCC controller class — A2 (ENG GUI) and A3 (THEIA HMI)
//
// Transport is selected at construction via TransportPath:
//   THEIA:    new MCC(log, TransportPath.A3_External)  — port 10050, magic 0xCB 0x58
//   ENG GUI:  new MCC(log, TransportPath.A2_Internal)  — port 10018, magic 0xCB 0x49
//
// INT_ENG commands (EnablePower, ReInitDevice, EnableDevice,
//   SetFanSpeed, SetTargetTemp) are guarded — calling them on
//   an A3 instance logs a warning and does nothing.
//
// MSG classes are in separate files:
//   MSG_MCC.cs, MSG_BATTERY.cs, MSG_IPG.cs, MSG_TMC.cs, MSG_GNSS.cs, MSG_CMC.cs

using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace CROSSBOW
{
    public class MCC
    {
        public enum READY_STATUS
        {
            ALIVE,
            READY,
            WARN,
            ERROR,
            NA,
        }

        // ── Frame constants ───────────────────────────────────────────────────
        private const byte MAGIC_HI             = 0xCB;
        private const int  FRAME_RESPONSE_LEN   = 521;
        private const int  PAYLOAD_OFFSET       = 7;
        private const int  PAYLOAD_LEN          = 512;
        private const byte STATUS_OK            = 0x00;

        // Transport-dependent computed properties
        private byte   MagicLo    => Transport == TransportPath.A3_External ? (byte)0x58   : (byte)0x49;
        private int    ActivePort => Transport == TransportPath.A3_External ? 10050         : 10018;
        private string LocalIP    => Transport == TransportPath.A3_External
                                        ? CrossbowNic.GetExternalIP()
                                        : CrossbowNic.GetInternalIP();

        // Keepalive — re-send SET_UNSOLICITED every 30 s to stay within the
        // firmware's 60-second liveness window (frame.hpp CLIENT_TIMEOUT_MS).
        private const int KEEPALIVE_INTERVAL_MS = 30_000;

        public string IP   { get; private set; } = IPS.MCC;
        public int    Port => ActivePort;

        // ── Transport + Logger ────────────────────────────────────────────────
        public TransportPath Transport { get; private set; }
        private ILogger Log { get; set; }

        // ── State ─────────────────────────────────────────────────────────────
        private UdpClient?               udpClient;
        private IPEndPoint?              ipEndPoint;
        private CancellationTokenSource? ts;
        private CancellationToken        ct;
        private bool                     _isStarted     = false;
        private byte                     _seq           = 0;
        private DateTime _lastKeepalive = DateTime.MinValue;
        private bool _wasConnected = false;
        private DateTime _connectedSince = DateTime.MinValue;
        private DateTime _dropTime = DateTime.MinValue;
        private int _dropCount = 0;

        public DateTime ConnectedSince { get { return _connectedSince; } }
        public int DropCount { get { return _dropCount; } }
        public DateTime lastMsgRx  { get; private set; } = DateTime.UtcNow;
        //public double   HB_RX_us   { get; private set; } = 0;
        public double HB_RX_ms { get; private set; } = 0;
        public bool     isConnected { get; private set; } = false;

        public MSG_MCC       LatestMSG    { get; private set; }
        
        public SYSTEM_STATES System_State { get { return LatestMSG.System_State; } }
        public BDC_MODES     BDC_Mode     { get { return LatestMSG.BDC_Mode; } }

        // ── Constructors ──────────────────────────────────────────────────────
        public MCC(ILogger _log, TransportPath transport = TransportPath.A3_External)
        {
            Log       = _log;
            Transport = transport;
            LatestMSG = new MSG_MCC(Log, transport);
        }

        public MCC(TransportPath transport = TransportPath.A3_External)
        {
            Transport = transport;
            LatestMSG = new MSG_MCC(transport);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        public void Start()
        {
            if (_isStarted) return;
            _isStarted = true;
            ts = new CancellationTokenSource();
            ct = ts.Token;
            // Randomise SEQ to avoid landing in the firmware's replay-rejection window
            // (last_seq − 32) from the previous session. Range 33–223 clears both wrap edges.
            _seq = (byte)new Random().Next(33, 224);
            Log?.Information("MCC starting ({Transport})", Transport);
            _ = backgroundUDPRead();
            _ = KeepaliveLoop();
        }

        public void Stop()
        {
            Log?.Information("MCC stopping");
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
                    // A3: bind to external IP so firmware accepts packets from .200–.254 range
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
                    Debug.WriteLine("MCC: A2 registration sent (0xA4)");
                }
            }
            catch (Exception ex)
            {
                Log?.Error("MCC socket init failed: {Ex}", ex.Message);
                _isStarted = false;
                return;
            }

            if (Transport == TransportPath.A3_External)
            {
                Send(BuildFrame((byte)ICD.FRAME_KEEPALIVE));
                _lastKeepalive = DateTime.UtcNow;
                Debug.WriteLine("MCC: A3 registration sent (0xA4)");
            }

            Log?.Information("MCC UDP connected ({LocalIp}:{Port} → {RemoteIp})",
                LocalIP, ActivePort, IP);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var res = await udpClient.ReceiveAsync(ct).ConfigureAwait(false);

                    if (Transport == TransportPath.A3_External)
                    {
                        // A3: pass full 521-byte frame — MSG_MCC.ParseA3 validates internally
                        if (!res.RemoteEndPoint.Address.Equals(IPAddress.Parse(IP))) continue;
                        byte[] rxBuff = res.Buffer;
                        if (rxBuff.Length == FRAME_RESPONSE_LEN)
                        {
                            // Any valid frame from firmware counts as liveness
                            isConnected = true;
                            if (!_wasConnected)
                            {
                                _wasConnected = true;
                                _connectedSince = DateTime.UtcNow;
                                Log?.Information("MCC: connection established");
                                Debug.WriteLine("MCC: connection established");
                            }
                            var now = DateTime.UtcNow;
                            HB_RX_ms = (now - lastMsgRx).TotalMilliseconds;
                            lastMsgRx = now;

                            if (rxBuff[3] == 0x00 || rxBuff[3] == 0xA1)  // REG1 CMD_BYTE: 0x00 (v4.0.0) | 0xA1 (legacy pre-FW-C10)
                                LatestMSG.Parse(rxBuff);
                            else
                                Debug.WriteLine($"MCC: A3 ACK rx CMD=0x{rxBuff[3]:X2}");
                        }
                    }
                    else
                    {
                        // A2: validate frame here, strip to 512-byte payload, pass to ParseA2
                        byte[] frame = res.Buffer;
                        if (frame.Length == FRAME_RESPONSE_LEN
                            && frame[0] == MAGIC_HI && frame[1] == MagicLo
                            && frame[4] == STATUS_OK
                            && CrcHelper.Crc16(frame, FRAME_RESPONSE_LEN - 2)
                               == (ushort)((frame[519] << 8) | frame[520]))
                        {
                            // Any valid frame from firmware counts as liveness
                            isConnected = true;
                            if (!_wasConnected)
                            {
                                _wasConnected = true;
                                _connectedSince = DateTime.UtcNow;
                                Log?.Information("MCC: connection established");
                                Debug.WriteLine("MCC: connection established");
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
                                Debug.WriteLine($"MCC: A2 ACK rx CMD=0x{frame[3]:X2}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"MCC: A2 frame rejected length={frame.Length}");
                        }
                    }
                }
            }
            catch (OperationCanceledException) { /* clean shutdown */ }
            catch (Exception ex)
            {
                Log?.Warning("MCC receive error: {Ex}", ex.Message);
            }
            finally
            {
                udpClient?.Close();
                udpClient   = null;
                isConnected = false;
                Log?.Information("MCC UDP closed");
            }
        }

        // A2 remote endpoint — used only on A2 path
        private IPEndPoint? _remoteEP;

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
            frame[4] = (byte)(plen & 0xFF);    // payload len LE
            frame[5] = (byte)(plen >> 8);
            if (plen > 0)
                Buffer.BlockCopy(payload, 0, frame, 6, plen);
            ushort crc = CrcHelper.Crc16(frame, frameLen - 2);
            frame[frameLen - 2] = (byte)(crc >> 8);  // CRC BE
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
                Log?.Warning("MCC: send error: {Ex}", ex.Message);
                Debug.WriteLine($"MCC: send error: {ex.Message}");
            }
        }

        // ── INT_ENG command guard ─────────────────────────────────────────────
        // INT_ENG commands are valid on A2 only. Firmware rejects them on A3
        // regardless, but this guard catches mistakes early at the call site.
        private bool AssertIntEng(string cmdName)
        {
            if (Transport == TransportPath.A3_External)
            {
                Log?.Warning("MCC: {Cmd} is INT_ENG only — not sent on A3 transport", cmdName);
                Debug.WriteLine($"MCC: {cmdName} blocked — A3 transport does not support INT_ENG commands");
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
                            Log?.Information("MCC: connection restored — was down {DownTime:0.0}s",
                                downTime);
                            Debug.WriteLine($"MCC: connection restored — was down {downTime:0.0}s");
                        }
                    }

                    if (stale && _wasConnected && _connectedSince != DateTime.MinValue
                        && (DateTime.UtcNow - _connectedSince).TotalMilliseconds > KEEPALIVE_INTERVAL_MS)
                    {
                        _dropTime = DateTime.UtcNow;
                        _dropCount++;
                        _wasConnected = false;
                        Log?.Warning("MCC: connection lost — drop #{Count} after {Uptime:0.0}s uptime",
                            _dropCount,
                            (DateTime.UtcNow - _connectedSince).TotalSeconds);
                        Debug.WriteLine($"MCC: connection lost — drop #{_dropCount} after {(DateTime.UtcNow - _connectedSince).TotalSeconds:0.0}s uptime");
                    }
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
        }

        private void SendKeepalive()
        {
            Send(BuildFrame((byte)ICD.FRAME_KEEPALIVE));
            _lastKeepalive = DateTime.UtcNow;
            Log?.Information("MCC: keepalive (0xA4) sent");
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

        // 0xAF SET_CHARGER — merged from 0xE3+0xED (v4.0.0). Level required every call.
        // level=0: disable. level>0: enable+set. V2: rejects level>0 (no charger I2C).
        public void SetCharger(CHARGE_LEVELS level)
        {
            Send((byte)ICD.SET_CHARGER, new byte[] { (byte)level });
        }

        // 0xAD SET_HEL_POWER
        public void SetLaserPower(uint pow)
        {
            Send((byte)ICD.SET_HEL_POWER, new byte[] { Convert.ToByte(pow) });
        }

        // 0xAE CLEAR_HEL_ERROR
        public void ClearLaserError()
        {
            Send((byte)ICD.CLEAR_HEL_ERROR);
        }
        // 0xAF SET_HEL_TRAINING_MODE
        public void SetHELTrainingMode(bool en)
        {
            Send((byte)ICD.SET_HEL_TRAINING_MODE, new byte[] { (byte)(en ? 1 : 0) });
        }
        // 0xAB SET_FIRE_REQUESTED_VOTE — moved from 0xE6, INT_OPS (v4.0.0) — heartbeat required
        public bool wasLaserFireRequested { get; private set; } = false;
        public Stopwatch LaserFireStopwatch { get; set; } = new Stopwatch();

        public bool LaserFireRequest
        {
            set
            {
                wasLaserFireRequested = value;
                Send((byte)ICD.SET_FIRE_REQUESTED_VOTE, new byte[] { Convert.ToByte(value) });
                if (wasLaserFireRequested)
                    LaserFireStopwatch.Restart();
                else
                    LaserFireStopwatch.Stop();
            }
        }

        // ── INT_ENG commands — A2 only, guarded ──────────────────────────────

        // 0xE2 PMS_POWER_ENABLE — unified power output control
        // INT_ENG only. Invalid MCC_POWER for active HW revision rejected by firmware.
        public void EnablePower(MCC_POWER p, bool en)
        {
            if (!AssertIntEng("EnablePower")) return;
            Send((byte)ICD.PMS_POWER_ENABLE, new byte[] { (byte)p, (byte)(en ? 1 : 0) });
        }

        // 0xE0 SET_MCC_REINIT
        public void ReInitDevice(MCC_DEVICES dev)
        {
            if (!AssertIntEng("ReInitDevice")) return;
            Send((byte)ICD.SET_REINIT, new byte[] { (byte)dev });
        }

        // 0xE1 SET_MCC_DEVICES_ENABLE
        public void EnableDevice(MCC_DEVICES dev, bool en)
        {
            if (!AssertIntEng("EnableDevice")) return;
            Send((byte)ICD.SET_DEVICES_ENABLE, new byte[] { (byte)dev, (byte)(en ? 1 : 0) });
        }

        // 0xE7 TMS_INPUT_FAN_SPEED
        public void SetFanSpeed(int fan, TMC_FAN_SPEEDS spd)
        {
            if (!AssertIntEng("SetFanSpeed")) return;
            Send((byte)ICD.TMS_INPUT_FAN_SPEED, new byte[] { (byte)fan, (byte)spd });
        }

        // 0xEB TMS_SET_TARGET_TEMP — firmware clamps to [10–40 °C]
        public void SetTargetTemp(byte tt)
        {
            if (!AssertIntEng("SetTargetTemp")) return;
            Send((byte)ICD.TMS_SET_TARGET_TEMP, new byte[] { tt });
        }

        // ── Connection + status ───────────────────────────────────────────────
        public bool MCC_STATUS { get { return LatestMSG.HB_ms < 200; } }
        public bool TMC_STATUS { get { return LatestMSG.isTMC_DeviceReady; } }
        public bool GPS_STATUS { get { return LatestMSG.isGNSS_DeviceReady && LatestMSG.GNSSMsg.SIV >= 4; } }

        private const double HEL_HB_STALE_S = 2.0;   // laser silent > 2s = stale

        public READY_STATUS HEL_STATUS
        {
            get
            {
                if (!LatestMSG.isHEL_Sensed) return READY_STATUS.ERROR;
                if (LatestMSG.HB_HEL > HEL_HB_STALE_S) return READY_STATUS.ERROR;

                if (LatestMSG.LaserModel == LASER_MODEL.YLM_6K)
                {
                    // 6K has no HK/bus voltage readback — use notready bit only
                    return LatestMSG.isHEL_NOTREADY ? READY_STATUS.WARN : READY_STATUS.READY;
                }

                // 3K — validate HK and bus voltage before declaring ready
                if ((LatestMSG.IPGMsg.HKVoltage > 23.3) && (LatestMSG.IPGMsg.BusVoltage > 40))
                    return LatestMSG.isHEL_NOTREADY ? READY_STATUS.WARN : READY_STATUS.READY;

                return READY_STATUS.ERROR;
            }
        }

        public Color COLOR_FROM_STATUS(READY_STATUS status)
        {
            switch (status)
            {
                case READY_STATUS.READY: return Color.Green;
                case READY_STATUS.WARN:  return Color.Orange;
                default:
                case READY_STATUS.ERROR: return Color.Red;
            }
        }
    }
}
