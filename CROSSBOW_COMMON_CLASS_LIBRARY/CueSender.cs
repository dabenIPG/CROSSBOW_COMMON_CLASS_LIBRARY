// CueSender.cs  —  HYPERION EXT_OPS CUE sender  ICD_EXTERNAL_INT v3.0.1
//
// Builds and sends framed CUE packets (CMD 0xAA) to THEIA on UDP:10009.
// Receives and parses THEIA status responses (CMD 0xAF) and POS/ATT reports (CMD 0xAB).
//
// Usage:
//   var sender = new CueSender("192.168.1.8", log);
//   sender.Start();
//   sender.SendTrack(trackId, TrackClass.UAV, lat, lng, altHAE, heading, speed, vz);
//   // Status responses arrive on StatusReceived event
//   sender.Stop();

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CROSSBOW
{
    // ── Response data structures ──────────────────────────────────────────────

    /// <summary>
    /// Parsed 0xAF THEIA status response payload.
    /// </summary>
    public class TheiaStatus
    {
        public byte   SystemState   { get; set; }  // SYSTEM_STATES enum value
        public byte   SystemMode    { get; set; }  // BDC_MODES enum value
        public byte   ActiveCamId   { get; set; }  // 0=VIS, 1=MWIR
        public byte   MccVoteBits   { get; set; }  // fire control votes
        public byte   BdcVoteBits1  { get; set; }  // raw geometry bits
        public byte   BdcVoteBits2  { get; set; }  // computed geometry votes

        public float  GimbalAzNed   { get; set; }  // degrees NED
        public float  GimbalElNed   { get; set; }  // degrees NED
        public float  LaserAzNed    { get; set; }  // degrees NED (gimbal + FSM)
        public float  LaserElNed    { get; set; }  // degrees NED (gimbal + FSM)

        // ── Convenience vote bit accessors ────────────────────────────────────
        private bool IsBitSet(byte b, int bit) => (b & (1 << bit)) != 0;

        // MCC Vote Bits
        public bool IsLaserEnabled_Vote    => IsBitSet(MccVoteBits, 0);
        public bool IsNotAbort_Vote        => IsBitSet(MccVoteBits, 1); // inverted: 1 = abort NOT active
        public bool IsArmed_Vote           => IsBitSet(MccVoteBits, 2);
        public bool IsBDA_Vote             => IsBitSet(MccVoteBits, 3);
        public bool IsEMON                 => IsBitSet(MccVoteBits, 4);
        public bool IsLaserFireRequested   => IsBitSet(MccVoteBits, 5);
        public bool IsLaserTotal_Vote      => IsBitSet(MccVoteBits, 6); // master MCC vote
        public bool IsCombat_Vote          => IsBitSet(MccVoteBits, 7);

        // BDC Vote Bits2 (computed)
        public bool BelowHorizVote         => IsBitSet(BdcVoteBits2, 0);
        public bool InKIZVote              => IsBitSet(BdcVoteBits2, 1);
        public bool InLCHVote              => IsBitSet(BdcVoteBits2, 2);
        public bool BDCVote                => IsBitSet(BdcVoteBits2, 3); // master BDC vote
        public bool IsHorizonLoaded        => IsBitSet(BdcVoteBits2, 5);
        public bool IsKIZLoaded            => IsBitSet(BdcVoteBits2, 6);
        public bool IsLCHLoaded            => IsBitSet(BdcVoteBits2, 7);

        /// <summary>True when both master votes pass — system geometry and fire control clear.</summary>
        public bool IsFireReady            => BDCVote && IsLaserTotal_Vote;

        public override string ToString() =>
            $"State={SystemState} Mode={SystemMode} CAM={ActiveCamId} " +
            $"MCC=0b{Convert.ToString(MccVoteBits,2).PadLeft(8,'0')} " +
            $"BDC2=0b{Convert.ToString(BdcVoteBits2,2).PadLeft(8,'0')} " +
            $"GimAz={GimbalAzNed:F2} GimEl={GimbalElNed:F2} " +
            $"LasAz={LaserAzNed:F2} LasEl={LaserElNed:F2} " +
            $"FireReady={IsFireReady}";
    }

    /// <summary>
    /// Parsed 0xAB THEIA POS/ATT report payload.
    /// </summary>
    public class TheiaPosAtt
    {
        public double Latitude   { get; set; }  // WGS-84 degrees
        public double Longitude  { get; set; }  // WGS-84 degrees
        public float  AltHAE     { get; set; }  // metres HAE
        public float  Roll       { get; set; }  // degrees NED
        public float  Pitch      { get; set; }  // degrees NED
        public float  Yaw        { get; set; }  // degrees NED

        public override string ToString() =>
            $"LLA=({Latitude:F6},{Longitude:F6},{AltHAE:F1}m HAE) RPY=({Roll:F2},{Pitch:F2},{Yaw:F2})";
    }

    // ── CueSender ─────────────────────────────────────────────────────────────

    public class CueSender : IDisposable
    {
        // ── Configuration ────────────────────────────────────────────────────
        private const int THEIA_PORT    = 10009;
        private const int RECV_TIMEOUT  = 1000;

        // ── State ────────────────────────────────────────────────────────────
        private readonly string   _theiaHost;
        private readonly object   _logObj;   // ILogger — kept as object to avoid Serilog dep in Hyperion
        private UdpClient         _udp;
        private Thread            _recvThread;
        private volatile bool     _running = false;
        private ushort            _seq     = 0;
        private IPEndPoint        _theiaDest;

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>Raised on every valid 0xAF status response from THEIA.</summary>
        public event Action<TheiaStatus>  StatusReceived;

        /// <summary>Raised on every valid 0xAB POS/ATT report from THEIA.</summary>
        public event Action<TheiaPosAtt>  PosAttReceived;

        // ── Latest parsed responses (poll if preferred over events) ───────────
        public TheiaStatus  LatestStatus  { get; private set; }
        public TheiaPosAtt  LatestPosAtt  { get; private set; }

        // ── Constructor ──────────────────────────────────────────────────────
        public CueSender(string theiaHost = "192.168.1.8")
        {
            _theiaHost = theiaHost;
        }

        // ── Public API ───────────────────────────────────────────────────────
        public bool IsRunning => _running;

        public void Start()
        {
            if (_running) return;
            _running = true;

            _theiaDest = new IPEndPoint(IPAddress.Parse(_theiaHost), THEIA_PORT);

            // Bind on any port — THEIA replies to our source IP:port
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, THEIA_PORT));
            _udp.Client.ReceiveTimeout = RECV_TIMEOUT;

            _recvThread = new Thread(RecvThreadFunc) { IsBackground = true, Name = "CueSender.Recv" };
            _recvThread.Start();

            Debug.WriteLine($"[CueSender] Started → {_theiaHost}:{THEIA_PORT}");
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            try { _udp?.Close(); } catch { }
            _recvThread?.Join(2000);
            Debug.WriteLine("[CueSender] Stopped");
        }

        public void Dispose() => Stop();

        // ── CUE send methods ──────────────────────────────────────────────────

        /// <summary>
        /// Send a TRACK command with position, heading, speed and vertical rate.
        /// This is the normal 100 Hz update path.
        /// HYPERION converts heading+speed to NED components internally for Kalman filter.
        /// THEIA uses heading for AC display overlay only.
        /// </summary>
        public void SendTrack(string trackId, ExtOpsFrame.TrackClass trackClass,
                               double lat, double lng, float altHAE,
                               float heading, float speed, float vz = 0.0f)
            => SendCue(trackId, trackClass, ExtOpsFrame.TrackCmd.Track,
                       lat, lng, altHAE, heading, speed, vz);

        /// <summary>Send DROP — THEIA exits CUE mode.</summary>
        public void SendDrop(string trackId = "")
            => SendCue(trackId, ExtOpsFrame.TrackClass.None, ExtOpsFrame.TrackCmd.Drop,
                       0, 0, 0, 0, 0, 0);

        /// <summary>Request one status response immediately.</summary>
        public void RequestStatusOnce()
            => SendCue("", ExtOpsFrame.TrackClass.None, ExtOpsFrame.TrackCmd.ReportOnce,
                       0, 0, 0, 0, 0, 0);

        /// <summary>Request one POS/ATT report from THEIA.</summary>
        public void RequestPosAtt()
            => SendCue("", ExtOpsFrame.TrackClass.None, ExtOpsFrame.TrackCmd.ReportPosAtt,
                       0, 0, 0, 0, 0, 0);

        /// <summary>Enable continuous 10 Hz 0xAF status stream from THEIA.</summary>
        public void StartContinuousReporting()
            => SendCue("", ExtOpsFrame.TrackClass.None, ExtOpsFrame.TrackCmd.ReportContinuousOn,
                       0, 0, 0, 0, 0, 0);

        /// <summary>Disable continuous status stream.</summary>
        public void StopContinuousReporting()
            => SendCue("", ExtOpsFrame.TrackClass.None, ExtOpsFrame.TrackCmd.ReportContinuousOff,
                       0, 0, 0, 0, 0, 0);

        /// <summary>Assert WEAPON HOLD — suppresses THEIA weapon release.</summary>
        public void SetWeaponHold()
            => SendCue("", ExtOpsFrame.TrackClass.None, ExtOpsFrame.TrackCmd.WeaponHold,
                       0, 0, 0, 0, 0, 0);

        /// <summary>Release WEAPON HOLD — votes govern firing.</summary>
        public void SetWeaponFreeToFire()
            => SendCue("", ExtOpsFrame.TrackClass.None, ExtOpsFrame.TrackCmd.WeaponFreeToFire,
                       0, 0, 0, 0, 0, 0);

        // ── Core send ─────────────────────────────────────────────────────────
        private void SendCue(string trackId, ExtOpsFrame.TrackClass trackClass,
                              ExtOpsFrame.TrackCmd trackCmd,
                              double lat, double lng, float altHAE,
                              float heading, float speed, float vz)
        {
            try
            {
                byte[] payload = BuildCuePayload(trackId, trackClass, trackCmd,
                                                  lat, lng, altHAE, heading, speed, vz);
                byte[] frame   = ExtOpsFrame.BuildFrame(ExtOpsFrame.CMD_CUE_INBOUND,
                                                         _seq++, payload);
                _udp.Send(frame, frame.Length, _theiaDest);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CueSender] Send error: {ex.Message}");
            }
        }

        private static byte[] BuildCuePayload(string trackId, ExtOpsFrame.TrackClass trackClass,
                                               ExtOpsFrame.TrackCmd trackCmd,
                                               double lat, double lng, float altHAE,
                                               float heading, float speed, float vz)
        {
            byte[] payload = new byte[ExtOpsFrame.PAYLOAD_LEN_CUE];  // 62 bytes, zero-initialised

            // [0–7]   ms timestamp
            ExtOpsFrame.WriteInt64(payload, 0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            // [8–15]  track ID — ASCII null-padded
            if (!string.IsNullOrEmpty(trackId))
            {
                byte[] idBytes = System.Text.Encoding.ASCII.GetBytes(trackId);
                int    copy    = Math.Min(idBytes.Length, 8);
                Buffer.BlockCopy(idBytes, 0, payload, 8, copy);
            }

            // [16]  track class
            payload[16] = (byte)trackClass;

            // [17]  track command
            payload[17] = (byte)trackCmd;

            // [18–25]  latitude double LE
            ExtOpsFrame.WriteDouble(payload, 18, lat);

            // [26–33]  longitude double LE
            ExtOpsFrame.WriteDouble(payload, 26, lng);

            // [34–37]  altitude HAE float LE
            ExtOpsFrame.WriteFloat(payload, 34, altHAE);

            // [38–41]  heading degrees true (0–360, North=0)
            ExtOpsFrame.WriteFloat(payload, 38, heading);

            // [42–45]  ground speed m/s
            ExtOpsFrame.WriteFloat(payload, 42, speed);

            // [46–49]  vertical speed m/s (positive = climbing)
            ExtOpsFrame.WriteFloat(payload, 46, vz);

            // [50–61]  RESERVED — already zero

            return payload;
        }

        // ── Receive thread (responses from THEIA) ─────────────────────────────
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

                    switch (parsed.Cmd)
                    {
                        case ExtOpsFrame.CMD_STATUS_RESPONSE:
                            if (parsed.PayloadLen == ExtOpsFrame.PAYLOAD_LEN_STATUS)
                            {
                                var status = ParseStatusResponse(parsed.Payload);
                                LatestStatus = status;
                                StatusReceived?.Invoke(status);
                            }
                            break;

                        case ExtOpsFrame.CMD_POSATT_REPORT:
                            if (parsed.PayloadLen == ExtOpsFrame.PAYLOAD_LEN_POSATT)
                            {
                                var posAtt = ParsePosAttReport(parsed.Payload);
                                LatestPosAtt = posAtt;
                                PosAttReceived?.Invoke(posAtt);
                            }
                            break;

                        default:
                            Debug.WriteLine($"[CueSender] Unexpected CMD 0x{parsed.Cmd:X2}");
                            break;
                    }
                }
                catch (SocketException) { /* timeout — loop */ }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CueSender] Recv error: {ex.Message}");
                }
            }
        }

        // ── Response parsers ──────────────────────────────────────────────────
        private static TheiaStatus ParseStatusResponse(byte[] p)
        {
            return new TheiaStatus
            {
                SystemState  = p[0],
                SystemMode   = p[1],
                ActiveCamId  = p[2],
                MccVoteBits  = p[3],
                BdcVoteBits1 = p[4],
                BdcVoteBits2 = p[5],
                GimbalAzNed  = ExtOpsFrame.ReadFloat(p, 6),
                GimbalElNed  = ExtOpsFrame.ReadFloat(p, 10),
                LaserAzNed   = ExtOpsFrame.ReadFloat(p, 14),
                LaserElNed   = ExtOpsFrame.ReadFloat(p, 18),
            };
        }

        private static TheiaPosAtt ParsePosAttReport(byte[] p)
        {
            return new TheiaPosAtt
            {
                Latitude  = ExtOpsFrame.ReadDouble(p, 0),
                Longitude = ExtOpsFrame.ReadDouble(p, 8),
                AltHAE    = ExtOpsFrame.ReadFloat(p, 16),
                Roll      = ExtOpsFrame.ReadFloat(p, 20),
                Pitch     = ExtOpsFrame.ReadFloat(p, 24),
                Yaw       = ExtOpsFrame.ReadFloat(p, 28),
            };
        }
    }
}
