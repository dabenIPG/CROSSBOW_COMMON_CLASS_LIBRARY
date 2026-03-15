// bdc.cs  —  THEIA HMI BDC controller class  (ICD v1.7, A3 framing)
//
// Changes from HMI original:
//   Port 10020 → 10050 (A3)
//   Socket bound explicitly to 192.168.1.208:10050 (firmware enforces src IP .200–.254)
//   EXEC_UDP_CMD() replaced by BuildA3Frame() / SendA3()
//   INT-only sends removed:
//     VicorEnabled, EnableRelay, SetOverrideVote, GimbalSetHome,
//     FMC_SET_FSM_SIGNS, FSMTestScan, STAGE_ENABLED, STAGE_CALIBRATE, SEND_LCH_PRINT
//   CMD_MWIR_NUC1 and SET_CUE_OFFSET kept — EXT promotion
//   Thread.Sleep(20) → await Task.Delay(20, ct) in LCH_UPLOAD
//   Debug.WriteLine → Serilog Log?.Information/Warning/Debug
//   TRC_MSG property renames applied:
//     DeviceTemp → deviceTemperature, JetsonTemp → jetsonTemp,
//     OverlayMaskRB → overlayMask
//
// MSG classes are in separate files:
//   BDC_MSG.cs, GIMBAL_MSG.cs, TRC_MSG.cs, FMC_MSG.cs, CAMERA.cs

using Microsoft.VisualBasic.Logging;
using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace CROSSBOW
{
    public class BDC
    {
        public enum READY_STATUS { ALIVE, READY, WARN, ERROR, NA }
        public enum BDC_FOCUS_DIR { NEAR = -1, FAR = 1 }

        // ── A3 constants ──────────────────────────────────────────────────────
        private const byte   A3_MAGIC_HI = 0xCB;
        private const byte   A3_MAGIC_LO = 0x58;
        private const string LOCAL_IP    = "192.168.1.208";
        private const int    A3_PORT     = 10050;

        // Keepalive — re-send SET_UNSOLICITED every 30 s to stay within the
        // firmware's 60-second liveness window (frame.hpp CLIENT_TIMEOUT_MS).
        private const int    KEEPALIVE_INTERVAL_MS = 30_000;

        public string IP   { get; private set; } = "192.168.1.20";
        public int    Port { get; private set; } = A3_PORT;

        // ── State ─────────────────────────────────────────────────────────────
        private UdpClient?              udpClient;
        private IPEndPoint?             ipEndPoint;
        private CancellationTokenSource? ts;
        private CancellationToken       ct;
        private bool                    _isStarted = false;
        private byte                    _seq       = 0;
        private DateTime                _lastKeepalive = DateTime.MinValue;
        private ILogger                 Log        { get; set; }

        public bool isVerboseLogEnabled { get; set; } = true;

        public DateTime lastMsgRx { get; private set; } = DateTime.UtcNow;
        public double   HB_RX_us  { get; private set; } = 0;

        public MSG_BDC LatestMSG { get; private set; }

        public BDC_MODES Mode { get { return LatestMSG.Mode; } }
        public TransportPath Transport { get; private set; }

        // ── Constructor ───────────────────────────────────────────────────────
        public BDC(ILogger _log, TransportPath transport = TransportPath.A3_External)
        {
            Log       = _log;
            // LatestMSG = new BDC_MSG(Log);
            LatestMSG = new MSG_BDC(Log, TransportPath.A3_External);
            Transport = transport;

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
            Log?.Information("BDC starting");
            _ = Task.Run(async () => await backgroundUDPRead(), ct);
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
                udpClient = new UdpClient();
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(LOCAL_IP), 0));
                ipEndPoint = new IPEndPoint(IPAddress.Parse(IP), A3_PORT);
            }
            catch (Exception ex)
            {
                Log?.Error("BDC socket init failed: {Ex}", ex.Message);
                _isStarted = false;
                return;
            }

            await Task.Delay(50, ct).ConfigureAwait(false);
            UnsolicitedMode = true;

            Log?.Information("BDC UDP connected ({LocalIp}:{Port} → {RemoteIp})", LOCAL_IP, A3_PORT, IP);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var    res    = await udpClient.ReceiveAsync(ct).ConfigureAwait(false);
                    if (!res.RemoteEndPoint.Address.Equals(IPAddress.Parse(IP))) continue;
                    byte[] rxBuff = res.Buffer;
                    if (rxBuff.Length > 0)
                    {
                        isConnected = true;
                        var now   = DateTime.UtcNow;
                        HB_RX_us  = (now - lastMsgRx).TotalMicroseconds;
                        lastMsgRx = now;
                        LatestMSG.Parse(rxBuff);
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

        // ── A3 frame builder ──────────────────────────────────────────────────
        private byte[] BuildA3Frame(byte cmd, byte[]? payload = null)
        {
            payload ??= Array.Empty<byte>();
            ushort plen     = (ushort)payload.Length;
            int    frameLen = 6 + plen + 2;
            byte[] frame    = new byte[frameLen];
            frame[0] = A3_MAGIC_HI;
            frame[1] = A3_MAGIC_LO;
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

        private void SendA3(byte cmd, byte[]? payload = null)
        {
            if (udpClient == null || ipEndPoint == null) return;
            byte[] frame = BuildA3Frame(cmd, payload);
            udpClient.Send(frame, frame.Length, ipEndPoint);
            _lastKeepalive = DateTime.UtcNow;   // any TX resets the keepalive clock
        }

        // ── Keepalive / staleness watchdog ────────────────────────────────────
        // Runs independently of the receive loop so it ticks even during network
        // blips where ReceiveAsync blocks. SendA3() resets _lastKeepalive so any
        // user command suppresses the next idle tick.
        // Staleness warning fires if no RX for >2× keepalive interval (60 s).
        private async Task KeepaliveLoop()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(KEEPALIVE_INTERVAL_MS));
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    if ((DateTime.UtcNow - _lastKeepalive).TotalMilliseconds >= KEEPALIVE_INTERVAL_MS)
                        SendKeepalive();

                    if (isConnected && (DateTime.UtcNow - lastMsgRx).TotalMilliseconds > KEEPALIVE_INTERVAL_MS * 2)
                        Log?.Warning("BDC: no telemetry for >{Seconds}s — firmware stream may have dropped",
                            KEEPALIVE_INTERVAL_MS * 2 / 1000);
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
        }

        private void SendKeepalive()
        {
            SendA3((byte)ICD.SET_UNSOLICITED, new byte[] { 1 });
            Log?.Information("BDC: keepalive sent");
        }

        // ── Status ────────────────────────────────────────────────────────────
        public bool isConnected { get; private set; } = false;

        public DateTime BDC_TIME_UTC { get { return LatestMSG.ntpTime.ToUniversalTime(); } }
        public bool BDC_STATUS  { get { return (LatestMSG.RX_HB > 10 && LatestMSG.RX_HB < 60.0); } }
        public bool GIM_STATUS  { get { return (LatestMSG.gimbalMSG.isReady && LatestMSG.gimbalMSG.isStarted && LatestMSG.gimbalMSG.isConnected); } }
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

        // ── EXT commands ──────────────────────────────────────────────────────
        // INT-only removed: VicorEnabled, EnableRelay, SetOverrideVote, GimbalSetHome,
        //   FMC_SET_FSM_SIGNS, FSMTestScan, STAGE_ENABLED, STAGE_CALIBRATE, SEND_LCH_PRINT

        public bool UnsolicitedMode
        {
            set { SendA3((byte)ICD.SET_UNSOLICITED, new byte[] { Convert.ToByte(value) }); }
        }

        public void REQ_REG_01() { SendA3((byte)ICD.GET_REGISTER1); }
        public void REQ_REG_02() { SendA3((byte)ICD.GET_REGISTER2); }

        public void SetState(SYSTEM_STATES _state)
        {
            SendA3((byte)ICD.SET_SYSTEM_STATE, new byte[] { Convert.ToByte(_state) });
        }

        public void SetMode(BDC_MODES _mode)
        {
            SendA3((byte)ICD.SET_GIMBAL_MODE, new byte[] { Convert.ToByte(_mode) });
        }

        public void SetActiveCamera(BDC_CAM_IDS _id)
        {
            SendA3((byte)ICD.ORIN_CAM_SET_ACTIVE, new byte[] { (byte)_id });
        }

        public void ReInitDevice(BDC_DEVICES _dev)
        {
            SendA3((byte)ICD.SET_BDC_REINIT, new byte[] { Convert.ToByte(_dev) });
        }
        // SET_BDC_VOTE_OVERRIDE
        public void SetOverrideVote(BDC_VOTE_OVERRIDES vote, bool val)
        {
            //Send(BuildA2Frame((byte)ICD.SET_BDC_VOTE_OVERRIDE, new[] { (byte)vote, (byte)(val ? 1 : 0) }));
            SendA3((byte)ICD.SET_BDC_VOTE_OVERRIDE, new[] { (byte)vote, (byte)(val ? 1 : 0) });

        }
        public void Jog(Int32 x, Int32 y)
        {
            byte[] vx = BitConverter.GetBytes(x);
            byte[] vy = BitConverter.GetBytes(y);
            SendA3((byte)ICD.SET_GIM_SPD, new byte[]
            {
                vx[0], vx[1], vx[2], vx[3],
                vy[0], vy[1], vy[2], vy[3]
            });
        }

        public void GimbalPark() { SendA3((byte)ICD.CMD_GIM_PARK); }

        public void SetPlatformATT(Single r, Single p, Single y)
        {
            byte[] br = BitConverter.GetBytes(r);
            byte[] bp = BitConverter.GetBytes(p);
            byte[] by = BitConverter.GetBytes(y);
            SendA3((byte)ICD.SET_SYS_ATT, new byte[]
            {
                br[0], br[1], br[2], br[3],
                bp[0], bp[1], bp[2], bp[3],
                by[0], by[1], by[2], by[3]
            });
        }

        public void SetPlatformLLA(Single _lat, Single _lng, Single _alt)
        {
            byte[] blat = BitConverter.GetBytes(_lat);
            byte[] blng = BitConverter.GetBytes(_lng);
            byte[] balt = BitConverter.GetBytes(_alt);
            SendA3((byte)ICD.SET_SYS_LLA, new byte[]
            {
                blat[0], blat[1], blat[2], blat[3],
                blng[0], blng[1], blng[2], blng[3],
                balt[0], balt[1], balt[2], balt[3]
            });
        }

        public void SetPIDGains(byte _which, Single pkp, Single pki, Single pkd,
                                              Single tkp, Single tki, Single tkd)
        {
            byte[] bpkp = BitConverter.GetBytes(pkp);
            byte[] bpki = BitConverter.GetBytes(pki);
            byte[] bpkd = BitConverter.GetBytes(pkd);
            byte[] btkp = BitConverter.GetBytes(tkp);
            byte[] btki = BitConverter.GetBytes(tki);
            byte[] btkd = BitConverter.GetBytes(tkd);
            SendA3((byte)ICD.SET_PID_GAINS, new byte[]
            {
                _which,
                bpkp[0], bpkp[1], bpkp[2], bpkp[3],
                bpki[0], bpki[1], bpki[2], bpki[3],
                bpkd[0], bpkd[1], bpkd[2], bpkd[3],
                btkp[0], btkp[1], btkp[2], btkp[3],
                btki[0], btki[1], btki[2], btki[3],
                btkd[0], btkd[1], btkd[2], btkd[3]
            });
        }

        public bool EnableCUETrack
        {
            set { SendA3((byte)ICD.SET_PID_ENABLE, new byte[] { 0x00, Convert.ToByte(value) }); }
        }

        public bool EnableVideoTrack
        {
            set { SendA3((byte)ICD.SET_PID_ENABLE, new byte[] { 0x01, Convert.ToByte(value) }); }
        }

        public void SetPIDCUETargetNED(Single x, Single y, Single s)
        {
            byte[] bx = BitConverter.GetBytes(x);
            byte[] by = BitConverter.GetBytes(y);
            byte[] bs = BitConverter.GetBytes(s);
            SendA3((byte)ICD.SET_PID_TARGET, new byte[]
            {
                0x00,
                bx[0], bx[1], bx[2], bx[3],
                by[0], by[1], by[2], by[3],
                bs[0], bs[1], bs[2], bs[3]
            });
        }

        public void SetAcamMag(byte _mag)  { SendA3((byte)ICD.SET_CAM_MAG,  new byte[] { _mag }); }
        public void SetAcamIris(byte _pos) { SendA3((byte)ICD.SET_CAM_IRIS, new byte[] { _pos }); }

        public void SetAcamFocus(ushort _pos)
        {
            byte[] b = BitConverter.GetBytes((UInt16)_pos);
            SendA3((byte)ICD.SET_CAM_FOCUS, new byte[] { b[0], b[1] });
        }

        public bool VIS_FILTER_ENABLE
        {
            set { SendA3((byte)ICD.CMD_VIS_FILTER_ENABLE, new byte[] { Convert.ToByte(value) }); }
        }

        public bool MWIR_WhiteHot
        {
            set { SendA3((byte)ICD.SET_MWIR_WHITEHOT, new byte[] { Convert.ToByte(value) }); }
        }

        public AF_MODES MWIR_SET_AF_MODE
        {
            set { SendA3((byte)ICD.CMD_MWIR_AF_MODE, new byte[] { Convert.ToByte(value) }); }
        }

        public BDC_FOCUS_DIR MWIR_BUMP_FOCUS
        {
            set { SendA3((byte)ICD.CMD_MWIR_BUMP_FOCUS, new byte[] { Convert.ToByte(value < 0 ? 0 : 1) }); }
        }

        // CMD_MWIR_NUC1 — kept, EXT promotion
        private DateTime lastNUC_request { get; set; } = DateTime.UtcNow;
        public void MWIR_NUC1()
        {
            if ((DateTime.UtcNow - lastNUC_request).TotalMinutes > 5)
            {
                SendA3((byte)ICD.CMD_MWIR_NUC1);
                lastNUC_request = DateTime.UtcNow;
            }
            else
            {
                if (isVerboseLogEnabled) Log?.Debug("MWIR_NUC1: too soon — must wait 5 min between NUCs");
            }
        }

        public void SetAcamTrackerEnable(BDC_TRACKERS _tracker, bool _en)
        {
            SendA3((byte)ICD.ORIN_ACAM_ENABLE_TRACKERS,
                   new byte[] { Convert.ToByte(_tracker), Convert.ToByte(_en) });
        }

        public void SetAITrackPriority(byte _num)
        {
            //SendA3((byte)ICD.ORIN_ACAM_SET_AI_TRACK_PRIORITY, new byte[] { _num });
        }

        public bool EnableCUEFlag
        {
            set { SendA3((byte)ICD.ORIN_ACAM_SET_CUE_FLAG, new byte[] { Convert.ToByte(value) }); }
        }

        public void ResetTrackB() { SendA3((byte)ICD.ORIN_ACAM_RESET_TRACKB); }

        public void SetOverlayBitmask(byte mask)
        {
            SendA3((byte)ICD.ORIN_SET_STREAM_OVERLAYS, new byte[] { mask });
        }

        public void SetViewMode(VIEW_MODES mode)
        {
            SendA3((byte)ICD.ORIN_SET_VIEW_MODE, new byte[] { (byte)mode });
        }

        /// <summary>
        /// Toggle OSD overlay on/off. Reads current overlay mask readback, sets or clears
        /// bit7 (HUD_OVERLAY_FLAGS.OSD), and sends the updated mask.
        /// </summary>
        public void SetOSD(bool enable)
        {
            byte current = LatestMSG.trcMSG.overlayMask;
            byte updated = HudOverlay.Set(current, HUD_OVERLAY_FLAGS.OSD, enable);
            SetOverlayBitmask(updated);
        }

        /// <summary>
        /// Toggle PIP mode. Enable sends PIP4 (1/4 inset); disable returns to
        /// the currently active camera.
        /// </summary>
        public void SetPIP(bool enable)
        {
            if (enable)
                SetViewMode(VIEW_MODES.PIP4);
            else
                SetViewMode(LatestMSG.trcMSG.Active_CAM == BDC_CAM_IDS.MWIR
                    ? VIEW_MODES.CAM2
                    : VIEW_MODES.CAM1);
        }

        public void SetTrackGateSize(Size _gate)
        {
            SendA3((byte)ICD.ORIN_ACAM_SET_TRACKGATE_SIZE,
                   new byte[] { Convert.ToByte(_gate.Width), Convert.ToByte(_gate.Height) });
        }

        public void SetTrackGateCenter(Point _ctr)
        {
            byte[] bx = BitConverter.GetBytes((UInt16)_ctr.X);
            byte[] by = BitConverter.GetBytes((UInt16)_ctr.Y);
            SendA3((byte)ICD.ORIN_ACAM_SET_TRACKGATE_CENTER,
                   new byte[] { bx[0], bx[1], by[0], by[1] });
        }

        // SET_CUE_OFFSET — kept, EXT promotion
        public void SetCUEOffset(float dx, float dy)
        {
            byte[] bx = BitConverter.GetBytes(dx);
            byte[] by = BitConverter.GetBytes(dy);
            SendA3((byte)ICD.SET_CUE_OFFSET, new byte[]
            {
                bx[0], bx[1], bx[2], bx[3],
                by[0], by[1], by[2], by[3]
            });
        }

        public void SetATOffset(sbyte dx, sbyte dy)
        {
            SendA3((byte)ICD.ORIN_ACAM_SET_ATOFFSET, new byte[] { (byte)dx, (byte)dy });
        }

        public void SetFTOffset(sbyte dx, sbyte dy)
        {
            SendA3((byte)ICD.ORIN_ACAM_SET_FTOFFSET, new byte[] { (byte)dx, (byte)dy });
        }

        public bool SetAcamFocusScoreActiveFlag
        {
            set { SendA3((byte)ICD.ORIN_ACAM_ENABLE_FOCUSSCORE, new byte[] { Convert.ToByte(value) }); }
        }

        public void FMC_SET_FSM_HOME(short x, short y)
        {
            byte[] bx = BitConverter.GetBytes(x);
            byte[] by = BitConverter.GetBytes(y);
            SendA3((byte)ICD.BDC_SET_FSM_HOME,
                   new byte[] { bx[0], bx[1], by[0], by[1] });
        }

        public void FMC_SET_FSM_POS(short x, short y)
        {
            byte[] bx = BitConverter.GetBytes(x);
            byte[] by = BitConverter.GetBytes(y);
            SendA3((byte)ICD.FMC_SET_FSM_POS,
                   new byte[] { bx[0], bx[1], by[0], by[1] });
        }

        public void FMC_SET_FSM_IFOVS(float x, float y)
        {
            byte[] bx = BitConverter.GetBytes(x);
            byte[] by = BitConverter.GetBytes(y);
            SendA3((byte)ICD.BDC_SET_FSM_IFOVS, new byte[]
            {
                bx[0], bx[1], bx[2], bx[3],
                by[0], by[1], by[2], by[3]
            });
        }

        public void FMC_SET_TRACK_ENABLE(bool _en)
        {
            SendA3((byte)ICD.BDC_SET_FSM_TRACK_ENABLE, new byte[] { Convert.ToByte(_en) });
        }

        public void FMC_SET_STAGE_POSITION(UInt32 p)
        {
            byte[] bx = BitConverter.GetBytes(p);
            SendA3((byte)ICD.FMC_SET_STAGE_POS, new byte[] { bx[0], bx[1], bx[2], bx[3] });
        }

        public void LoadHorizon(UInt16 iaz, float el)
        {
            byte[] b1 = BitConverter.GetBytes(iaz);
            byte[] b2 = BitConverter.GetBytes(el);
            SendA3((byte)ICD.SET_BDC_HORIZ,
                   new byte[] { b1[0], b1[1], b2[0], b2[1], b2[2], b2[3] });
        }

        public void PrintHorizon() { SendA3((byte)ICD.SET_BDC_HORIZ); }

        public void SET_HORIZON_BUFFER(Single b)
        {
            byte[] bx = BitConverter.GetBytes(b);
            SendA3((byte)ICD.SET_BDC_HORIZ, new byte[] { bx[0], bx[1], bx[2], bx[3] });
        }

        public void SET_LCH_VOTE(LCH.FILETYPE _fileType, bool _operatorValid,
                                  bool _locationValid, bool _forExec)
        {
            SendA3((byte)ICD.SET_BDC_PALOS_VOTE, new byte[]
            {
                Convert.ToByte(_fileType),
                Convert.ToByte(_operatorValid),
                Convert.ToByte(_locationValid),
                Convert.ToByte(_forExec)
            });
        }

        public void SEND_LCH_MISSION_DATA(LCH aLCH)
        {
            UInt64 t1 = (UInt64)(new DateTimeOffset(aLCH.MissionStartDateTime.ToUniversalTime()).ToUnixTimeSeconds());
            UInt64 t2 = (UInt64)(new DateTimeOffset(aLCH.MissionEndDateTime.ToUniversalTime()).ToUnixTimeSeconds());

            UInt16 az1 = Convert.ToUInt16(aLCH.Az1);
            UInt16 az2 = Convert.ToUInt16(aLCH.Az2);
            Int16  el1 = Convert.ToInt16(aLCH.El1);
            Int16  el2 = Convert.ToInt16(aLCH.El2);
            UInt16 nt  = Convert.ToUInt16(aLCH.NumberTarget);
            UInt16 nw  = Convert.ToUInt16(aLCH.NumberWindows);

            if (isVerboseLogEnabled)
            {
                Log?.Debug("LCH mission t1={T1} t2={T2}", t1, t2);
            }

            byte[] msg = new byte[31];
            int ndx = 0;
            msg[ndx] = (byte)ICD.SET_LCH_MISSION_DATA; ndx++;
            msg[ndx] = Convert.ToByte(aLCH.FileType);  ndx++;
            msg[ndx] = Convert.ToByte(true);            ndx++;
            Buffer.BlockCopy(BitConverter.GetBytes(t1),  0, msg, ndx, sizeof(UInt64)); ndx += sizeof(UInt64);
            Buffer.BlockCopy(BitConverter.GetBytes(t2),  0, msg, ndx, sizeof(UInt64)); ndx += sizeof(UInt64);
            Buffer.BlockCopy(BitConverter.GetBytes(az1), 0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(el1), 0, msg, ndx, sizeof(Int16));  ndx += sizeof(Int16);
            Buffer.BlockCopy(BitConverter.GetBytes(az2), 0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(el2), 0, msg, ndx, sizeof(Int16));  ndx += sizeof(Int16);
            Buffer.BlockCopy(BitConverter.GetBytes(nt),  0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(nw),  0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);

            // Send without CMD byte prefix — mission data frame carries its own ICD.SET_LCH_MISSION_DATA
            // in msg[0]; A3 wraps the entire msg as payload.
            SendA3(msg[0], msg[1..]);
        }

        public void SEND_LCH_TARGET_WITH_WINDOWS(LCH aLCH, UInt16 _targetIndex)
        {
            LCH_TARGET aLCH_TARGET = aLCH.LCH_Targets[_targetIndex];

            UInt16 t1  = (UInt16)((aLCH_TARGET.StartDateTime - aLCH.MissionStartDateTime).TotalSeconds);
            UInt16 t2  = (UInt16)((aLCH_TARGET.EndDateTime   - aLCH.MissionStartDateTime).TotalSeconds);
            UInt16 az1 = Convert.ToUInt16(aLCH_TARGET.Az1);
            UInt16 az2 = Convert.ToUInt16(aLCH_TARGET.Az2);
            Int16  el1 = Convert.ToInt16(aLCH_TARGET.El1);
            Int16  el2 = Convert.ToInt16(aLCH_TARGET.El2);
            float flat = (Single)aLCH_TARGET.Latitude;
            float flng = (Single)aLCH_TARGET.Longitude;
            float falt = (Single)aLCH_TARGET.Altitude;

            byte[] msg = new byte[18 + 12 + aLCH_TARGET.LCH_Windows.Count * 2 * sizeof(UInt16)];
            int ndx = 0;
            msg[ndx] = (byte)ICD.SET_LCH_TARGET_DATA; ndx++;
            msg[ndx] = Convert.ToByte(aLCH.FileType);  ndx++;
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)aLCH_TARGET.LCH_Windows.Count), 0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(t1),   0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(t2),   0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(az1),  0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(el1),  0, msg, ndx, sizeof(Int16));  ndx += sizeof(Int16);
            Buffer.BlockCopy(BitConverter.GetBytes(az2),  0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            Buffer.BlockCopy(BitConverter.GetBytes(el2),  0, msg, ndx, sizeof(Int16));  ndx += sizeof(Int16);
            Buffer.BlockCopy(BitConverter.GetBytes(flat), 0, msg, ndx, sizeof(Single)); ndx += sizeof(Single);
            Buffer.BlockCopy(BitConverter.GetBytes(flng), 0, msg, ndx, sizeof(Single)); ndx += sizeof(Single);
            Buffer.BlockCopy(BitConverter.GetBytes(falt), 0, msg, ndx, sizeof(Single)); ndx += sizeof(Single);

            foreach (LCH_WINDOW aLCH_WINDOW in aLCH_TARGET.LCH_Windows)
            {
                UInt16 wt1 = (UInt16)((aLCH_WINDOW.StartDateTime - aLCH.MissionStartDateTime).TotalSeconds);
                UInt16 wt2 = (UInt16)((aLCH_WINDOW.EndDateTime   - aLCH.MissionStartDateTime).TotalSeconds);
                Buffer.BlockCopy(BitConverter.GetBytes(wt1), 0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
                Buffer.BlockCopy(BitConverter.GetBytes(wt2), 0, msg, ndx, sizeof(UInt16)); ndx += sizeof(UInt16);
            }

            SendA3(msg[0], msg[1..]);
        }

        // Thread.Sleep → await Task.Delay — BDC migration fix
        public async Task LCH_UPLOAD(IProgress<int> progress, LCH aLCH)
        {
            SEND_LCH_MISSION_DATA(aLCH);
            await Task.Delay(20, ct).ConfigureAwait(false);
            progress?.Report(5);

            ushort _targetIndex = 0;
            foreach (LCH_TARGET aTarget in aLCH.LCH_Targets)
            {
                if (isVerboseLogEnabled) Log?.Debug("LCH upload: sending target {Idx}", _targetIndex);
                SEND_LCH_TARGET_WITH_WINDOWS(aLCH, _targetIndex);
                await Task.Delay(20, ct).ConfigureAwait(false);
                int irpt = 5 + (int)(((double)_targetIndex / aLCH.NumberTarget) * 95);
                progress?.Report(irpt);
                _targetIndex++;
            }
        }

        public void GimbalSetPOS(Int32 x, Int32 y)
        {
            byte[] px = BitConverter.GetBytes(x);
            byte[] py = BitConverter.GetBytes(y);
            SendA3((byte)ICD.SET_GIM_POS, new byte[]
            {
                px[0], px[1], px[2], px[3],
                py[0], py[1], py[2], py[3]
            });
        }

        public void Check_PALOS_Vote(LCH.FILETYPE _fileType, DateTime currentTime,
                                      float az, float el)
        {
            UInt64 t1 = (UInt64)(new DateTimeOffset(currentTime).ToUnixTimeSeconds());

            if (isVerboseLogEnabled) Log?.Debug("BDC PALOS vote check at {T1}", t1);

            byte[] msg = new byte[17];   // everything after the CMD byte
            int ndx = 0;
            msg[ndx] = Convert.ToByte(_fileType); ndx++;
            Buffer.BlockCopy(BitConverter.GetBytes(t1), 0, msg, ndx, sizeof(UInt64)); ndx += sizeof(UInt64);
            Buffer.BlockCopy(BitConverter.GetBytes(az), 0, msg, ndx, sizeof(Single)); ndx += sizeof(Single);
            Buffer.BlockCopy(BitConverter.GetBytes(el), 0, msg, ndx, sizeof(Single)); ndx += sizeof(Single);

            SendA3((byte)ICD.GET_BDC_PALOS_VOTE, msg);
        }
    }
}
