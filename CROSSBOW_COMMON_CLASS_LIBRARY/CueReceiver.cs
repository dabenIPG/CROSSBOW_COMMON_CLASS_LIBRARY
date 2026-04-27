// CueReceiver.cs  —  THEIA EXT_OPS receiver / responder  ICD_EXTERNAL_INT v3.0.1
//
// Listens on UDP:10009 for framed CUE packets (CMD 0xAA) from HYPERION or any
// conforming integrator. On receipt of a valid frame, dispatches on Track CMD
// and sends a framed status response (CMD 0xAF) to the sender.
//
// Continuous reporting (Track CMD 254 = REPORT CONTINUOUS ON):
//   Starts a 10 Hz timer that sends 0xAF to the last registered sender.
//
// POS/ATT report (Track CMD 3 = REPORT POS/ATT):
//   Sends one 0xAB frame to the sender immediately.
//
// Dependencies:
//   ExtOpsFrame.cs  —  framing, CRC, helpers
//   crossbow.cs     —  CB object (system state, BDC/MCC messages, platform LLA/ATT)

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Serilog;

namespace CROSSBOW
{
    public class CueReceiver : IDisposable
    {
        // ── Configuration ────────────────────────────────────────────────────
        private const int    UDP_PORT          = 10009;
        private const int    CONTINUOUS_HZ     = 10;
        private const int    CONTINUOUS_MS     = 1000 / CONTINUOUS_HZ;  // 100 ms
        private const int    RECV_TIMEOUT_MS   = 1000;

        // ── State ────────────────────────────────────────────────────────────
        private readonly CB  _cb;
        private readonly ILogger   _log;

        private UdpClient          _udp;
        private Thread             _recvThread;
        private System.Threading.Timer  _continuousTimer;
        private volatile bool      _running        = false;
        private volatile bool      _weaponHold     = false;
        private ushort             _txSeq          = 0;

        // Last sender — used for continuous report delivery
        private IPEndPoint         _lastSender     = null;
        private readonly object    _senderLock     = new object();

        // ── Constructor ──────────────────────────────────────────────────────
        public CueReceiver(CB cb, ILogger log = null)
        {
            _cb  = cb  ?? throw new ArgumentNullException(nameof(cb));
            _log = log;
        }

        // ── Public API ───────────────────────────────────────────────────────
        public bool IsRunning   => _running;
        public bool WeaponHold  => _weaponHold;

        public void Start()
        {
            if (_running) return;
            _running = true;

            _udp = new UdpClient(UDP_PORT);
            _udp.Client.ReceiveTimeout = RECV_TIMEOUT_MS;

            _recvThread = new Thread(RecvThreadFunc) { IsBackground = true, Name = "CueReceiver" };
            _recvThread.Start();

            _log?.Information("[CueReceiver] Started on UDP:{Port}", UDP_PORT);
            Debug.WriteLine($"[CueReceiver] Started on UDP:{UDP_PORT}");
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;

            StopContinuous();

            try { _udp?.Close(); } catch { }
            _recvThread?.Join(2000);

            _log?.Information("[CueReceiver] Stopped");
            Debug.WriteLine("[CueReceiver] Stopped");
        }

        public void Dispose() => Stop();

        // ── Receive thread ────────────────────────────────────────────────────
        private void RecvThreadFunc()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (_running)
            {
                try
                {
                    byte[] buf = _udp.Receive(ref remoteEP);

                    if (!ExtOpsFrame.TryParseFrame(buf, buf.Length, out var parsed))
                        continue;

                    if (parsed.Cmd != ExtOpsFrame.CMD_CUE_INBOUND)
                    {
                        Debug.WriteLine($"[CueReceiver] Unexpected CMD 0x{parsed.Cmd:X2} — ignored");
                        continue;
                    }

                    if (parsed.PayloadLen != ExtOpsFrame.PAYLOAD_LEN_CUE)
                    {
                        Debug.WriteLine($"[CueReceiver] Bad CUE payload length {parsed.PayloadLen}");
                        continue;
                    }

                    // Register sender for continuous reports
                    lock (_senderLock)
                        _lastSender = new IPEndPoint(remoteEP.Address, remoteEP.Port);

                    ProcessCuePayload(parsed.Payload, remoteEP);
                }
                catch (SocketException) { /* timeout — loop */ }
                catch (Exception ex)
                {
                    _log?.Warning(ex, "[CueReceiver] Recv error");
                    Debug.WriteLine($"[CueReceiver] Recv error: {ex.Message}");
                }
            }
        }

        // ── CUE payload dispatch ──────────────────────────────────────────────
        private void ProcessCuePayload(byte[] p, IPEndPoint sender)
        {
            // Parse header fields
            long   msTimestamp  = ExtOpsFrame.ReadInt64(p, 0);
            // track_id bytes [8–15] — available if needed
            byte   trackClass   = p[16];
            var    trackCmd     = (ExtOpsFrame.TrackCmd)p[17];
            double lat          = ExtOpsFrame.ReadDouble(p, 18);
            double lng          = ExtOpsFrame.ReadDouble(p, 26);
            float  altHAE       = ExtOpsFrame.ReadFloat(p, 34);
            float  heading      = ExtOpsFrame.ReadFloat(p, 38);
            float  speed        = ExtOpsFrame.ReadFloat(p, 42);
            float  vz           = ExtOpsFrame.ReadFloat(p, 46);

            switch (trackCmd)
            {
                case ExtOpsFrame.TrackCmd.Drop:
                    HandleDrop();
                    break;

                case ExtOpsFrame.TrackCmd.Track:
                    HandleTrack(lat, lng, altHAE, trackClass, heading, speed, vz);
                    SendStatusResponse(sender);
                    break;

                case ExtOpsFrame.TrackCmd.ReportOnce:
                    SendStatusResponse(sender);
                    break;

                case ExtOpsFrame.TrackCmd.ReportPosAtt:
                    SendPosAttReport(sender);
                    break;

                case ExtOpsFrame.TrackCmd.WeaponHold:
                    _weaponHold = true;
                    _log?.Warning("[CueReceiver] WEAPON HOLD set by {Sender}", sender);
                    Debug.WriteLine($"[CueReceiver] WEAPON HOLD — set");
                    break;

                case ExtOpsFrame.TrackCmd.WeaponFreeToFire:
                    _weaponHold = false;
                    _log?.Information("[CueReceiver] WEAPON FREE TO FIRE — hold cleared");
                    Debug.WriteLine($"[CueReceiver] WEAPON FREE TO FIRE — hold cleared");
                    break;

                case ExtOpsFrame.TrackCmd.ReportContinuousOn:
                    StartContinuous(sender);
                    break;

                case ExtOpsFrame.TrackCmd.ReportContinuousOff:
                    StopContinuous();
                    break;

                default:
                    Debug.WriteLine($"[CueReceiver] Unhandled Track CMD 0x{(byte)trackCmd:X2}");
                    break;
            }
        }

        // ── Track handlers ────────────────────────────────────────────────────
        private void HandleDrop()
        {
            _log?.Information("[CueReceiver] DROP — exiting CUE mode");
            Debug.WriteLine("[CueReceiver] DROP");
            // Operator must change mode explicitly — THEIA does not auto-switch on DROP
            // Wire to CB mode manager if desired: _cb.SetMode(BDC_MODES.RATE);
        }

        private void HandleTrack(double lat, double lng, float altHAE,
                                  byte trackClass, float heading, float speed, float vz)
        {
            // Update active CUE — THEIA computes Bearing/Elevation from Position via COMMON.GetBearing.
            // HeadingSpeed stored for AC display overlay (heading) and future use (speed, vz).
            _cb.CurrentCUE.Position       = new ptLLA(lat, lng, altHAE);
            _cb.CurrentCUE.Classification = (TRACK_CLASSIFICATION)trackClass;
            _cb.CurrentCUE.HeadingSpeed   = new HeadingSpeed(heading, speed, vz);

            if (_cb.BDC_Mode != BDC_MODES.CUE && _cb.isCUE_FLAG_SET)
                _cb.SetMode(BDC_MODES.CUE);

            if (!_cb.CUE_TRACK_ENABLED && _cb.BDC_Mode == BDC_MODES.CUE)
                _cb.CUE_TRACK_ENABLED = true;
        }

        // ── Response builders ─────────────────────────────────────────────────

        /// <summary>
        /// Build and send CMD 0xAF — 30-byte status response payload.
        /// </summary>
        public void SendStatusResponse(IPEndPoint dest)
        {
            try
            {
                byte[] payload = new byte[ExtOpsFrame.PAYLOAD_LEN_STATUS];

                payload[0] = (byte)_cb.System_State;
                payload[1] = (byte)_cb.BDC_Mode;
                payload[2] = (byte)_cb.Active_CAM;
                payload[3] = _cb.aMCC.LatestMSG.VOTE_BITS_MCC;          // MCC vote bits
                payload[4] = _cb.aBDC.LatestMSG.VOTE_BITS_BDC;         // BDC raw geometry bits
                payload[5] = _cb.aBDC.LatestMSG.VOTE_BITS_BDC2;         // BDC computed votes

                // Gimbal LOS NED (from gimbal encoder + platform attitude)
                ExtOpsFrame.WriteFloat(payload, 6,  _cb.aBDC.LatestMSG.LOS_GIM.X);
                ExtOpsFrame.WriteFloat(payload, 10, _cb.aBDC.LatestMSG.LOS_GIM.Y);

                // Laser LOS NED (gimbal LOS + FSM offset, ADC readback)
                ExtOpsFrame.WriteFloat(payload, 14, _cb.aBDC.LatestMSG.LOS_FSM_RB.X);
                ExtOpsFrame.WriteFloat(payload, 18, _cb.aBDC.LatestMSG.LOS_FSM_RB.Y);

                // [22–29] RESERVED — already zero

                byte[] frame = ExtOpsFrame.BuildFrame(ExtOpsFrame.CMD_STATUS_RESPONSE,
                                                       _txSeq++, payload);
                _udp.Send(frame, frame.Length, dest);
            }
            catch (Exception ex)
            {
                _log?.Warning(ex, "[CueReceiver] SendStatusResponse failed");
                Debug.WriteLine($"[CueReceiver] SendStatusResponse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Build and send CMD 0xAB — 32-byte POS/ATT report payload.
        /// </summary>
        public void SendPosAttReport(IPEndPoint dest)
        {
            try
            {
                byte[] payload = new byte[ExtOpsFrame.PAYLOAD_LEN_POSATT];

                ExtOpsFrame.WriteDouble(payload, 0,  _cb.BaseStation.lat);
                ExtOpsFrame.WriteDouble(payload, 8,  _cb.BaseStation.lng);
                ExtOpsFrame.WriteFloat (payload, 16, (float)_cb.BaseStation.alt);   // HAE — see MSG_GNSS fix
                ExtOpsFrame.WriteFloat (payload, 20, (float)_cb.BaseStationAtitude.roll);
                ExtOpsFrame.WriteFloat (payload, 24, (float)_cb.BaseStationAtitude.pitch);
                ExtOpsFrame.WriteFloat (payload, 28, (float)_cb.BaseStationAtitude.yaw);

                byte[] frame = ExtOpsFrame.BuildFrame(ExtOpsFrame.CMD_POSATT_REPORT,
                                                       _txSeq++, payload);
                _udp.Send(frame, frame.Length, dest);
            }
            catch (Exception ex)
            {
                _log?.Warning(ex, "[CueReceiver] SendPosAttReport failed");
                Debug.WriteLine($"[CueReceiver] SendPosAttReport error: {ex.Message}");
            }
        }

        // ── Continuous reporting ──────────────────────────────────────────────
        private void StartContinuous(IPEndPoint sender)
        {
            StopContinuous();
            lock (_senderLock)
                _lastSender = new IPEndPoint(sender.Address, sender.Port);

            _continuousTimer = new System.Threading.Timer(_ =>
            {
                IPEndPoint dest;
                lock (_senderLock) dest = _lastSender;
                if (dest != null && _running)
                    SendStatusResponse(dest);
            }, null, 0, CONTINUOUS_MS);

            _log?.Information("[CueReceiver] Continuous 0xAF reporting started → {Sender}", sender);
            Debug.WriteLine($"[CueReceiver] Continuous reporting ON → {sender}");
        }

        private void StopContinuous()
        {
            _continuousTimer?.Dispose();
            _continuousTimer = null;
            Debug.WriteLine("[CueReceiver] Continuous reporting OFF");
        }
    }
}
