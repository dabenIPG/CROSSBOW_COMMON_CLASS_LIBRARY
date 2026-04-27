using System;
using Serilog;
using System.Diagnostics;

namespace CROSSBOW
{
    // MSG_BDC.cs  —  updated for ICD v3.0.0 session 4 register layout
    //               updated session 32 — PTP time source integration
    //
    // Key changes from previous version:
    //   - A3 frame validation added — ParseA3() (magic 0xCB 0x58, CRC, STATUS)
    //   - ParseA2() renamed from Parse() — raw A2 payload path
    //   - ParseMSG02 removed — REG2 data folded into REG1 in ICD v3.0.0
    //   - LastFrameStatus property added — set in ParseA3 only
    //   - dt_us > 25000 log: MSG_BDC: prefix added + Serilog merged
    //   - isFMCEnabled / isFMCReady legacy aliases added
    //   - FW_VERSION_STRING: v prefix removed
    //   - RTC removed — _epochTime is UInt64 only (was _ntpTime — session 32)
    //   - TEMPERATURE_VICOR was float — now sbyte (int8)
    //   - TransportPath enum — constructor selects A2 or A3 at construction time.
    //     Callers always use Parse(). ParseA3/ParseA2 are private implementation.
    //     THEIA:    new MSG_BDC(log, TransportPath.A3_External)
    //     ENG GUI:  new MSG_BDC(log, TransportPath.A2_Internal)
    // Session 32 additions:
    //   - isPTP_Enabled / isPTP_DeviceReady — DeviceEnabled/Ready bit 7
    //   - ntpUsingFallback / ntpHasFallback / usingPTP — initially StatusBits bits 1/2/3,
    //     moved to TimeBits byte 391 (session 32). Named properties now redirect to tb_*.
    //   - activeTimeSource — "PTP" | "NTP" | "NONE" derived from StatusBits bit 3
    //   - epochTime replaces ntpTime (source-agnostic); ntpTime kept as legacy alias
    // ---------------------------------------------------------------------------

    public class MSG_BDC
    {
        // ── Frame constants ───────────────────────────────────────────────────
        private const byte MAGIC_HI           = 0xCB;
        private const int  FRAME_RESPONSE_LEN = 521;
        private const int  PAYLOAD_OFFSET     = 7;      // payload starts here
        private const byte STATUS_OK          = 0x00;

        // MAGIC_LO is transport-dependent — computed from TransportPath
        private byte MagicLo => Transport == TransportPath.A3_External
            ? (byte)0x58   // A3 external  — 0xCB 0x58
            : (byte)0x49;  // A2 internal  — 0xCB 0x49

        // ── Transport + Logger + constructors ─────────────────────────────────
        public TransportPath Transport { get; private set; }
        private ILogger Log { get; set; }

        public MSG_BDC(ILogger _log, TransportPath transport = TransportPath.A3_External)
        {
            Log       = _log;
            Transport = transport;
        }
        public MSG_BDC(TransportPath transport = TransportPath.A3_External)
        {
            Transport = transport;
        }

        // ── System state ──────────────────────────────────────────────────────
        public SYSTEM_STATES State       { get; private set; } = SYSTEM_STATES.OFF;
        public BDC_MODES     Mode        { get; private set; } = BDC_MODES.OFF;
        public BDC_CAM_IDS   ActiveCamID { get; private set; } = BDC_CAM_IDS.VIS;

        // ── Heartbeat / timing ────────────────────────────────────────────────
        // ICD v3.0.0 session 4: HB_ms uint16 ms (was UInt32 us)
        public UInt16 HB_ms    { get; private set; } = 1000;
        public double HB_TX_ms { get { return (double)HB_ms; } }

        // ICD v3.0.0 session 4: dt_us uint16 (was UInt32)
        public UInt16 dt_us { get; private set; } = 0;

        // ── Version ───────────────────────────────────────────────────────────
        public UInt32 FW_VERSION { get; private set; } = 0;
        public string FW_VERSION_STRING
        {
            get
            {
                uint major = (FW_VERSION >> 24) & 0xFF;
                uint minor = (FW_VERSION >> 12) & 0xFFF;
                uint patch =  FW_VERSION        & 0xFFF;
                return $"{major}.{minor}.{patch}";
            }
        }
        public uint FW_MAJOR => (FW_VERSION >> 24) & 0xFF;   // firmware major version
        public bool IsV4 => FW_MAJOR >= 4;

        // ── Status / device bits ──────────────────────────────────────────────
        public byte DeviceEnabledBits { get; private set; } = 0;
        public byte DeviceReadyBits { get; private set; } = 0;
        public byte HealthBits { get; private set; } = 0;   // byte [10] — renamed from StatusBits
        public byte PowerBits { get; private set; } = 0;   // byte [11] — renamed from StatusBits2
        // Backward-compat aliases — existing call sites unbroken
        public byte StatusBits => HealthBits;
        public byte StatusBits2 => PowerBits;

        public bool isVerboseLogEnabled { get; set; } = true;

        private byte _voteBitsBDC2 { get; set; } = 0;
        public byte LastVOTE_BITS_BDC2 { get; private set; } = 0;
        public byte VOTE_BITS_BDC2  // [164] BDC raw/override detail
        {
            get { return _voteBitsBDC2; }
            set
            {
                if (isVerboseLogEnabled && _voteBitsBDC2 != value)
                    Log?.Information($"VOTE_BITS_BDC2 CHANGED {Convert.ToString(_voteBitsBDC2, 2).PadLeft(8, '0')} -> {Convert.ToString(value, 2).PadLeft(8, '0')}");
                LastVOTE_BITS_BDC2 = value;
                _voteBitsBDC2 = value;
            }
        }

        private byte _voteBitsBDC { get; set; } = 0;
        public byte LastVOTE_BITS_BDC { get; private set; } = 0;
        public byte VOTE_BITS_BDC  // [165] BDC computed vote summary
        {
            get { return _voteBitsBDC; }
            set
            {
                if (isVerboseLogEnabled && _voteBitsBDC != value)
                    Log?.Information($"VOTE_BITS_BDC CHANGED {Convert.ToString(_voteBitsBDC, 2).PadLeft(8, '0')} -> {Convert.ToString(value, 2).PadLeft(8, '0')}");
                LastVOTE_BITS_BDC = value;
                _voteBitsBDC = value;
            }
        }

        private byte _voteBitsMCC_RB { get; set; } = 0;
        public byte LastVOTE_BITS_MCC_RB { get; private set; } = 0;
        public byte VOTE_BITS_MCC_RB  // [166] MCC_VOTES readback
        {
            get { return _voteBitsMCC_RB; }
            set
            {
                if (isVerboseLogEnabled && _voteBitsMCC_RB != value)
                    Log?.Information($"VOTE_BITS_MCC_RB CHANGED {Convert.ToString(_voteBitsMCC_RB, 2).PadLeft(8, '0')} -> {Convert.ToString(value, 2).PadLeft(8, '0')}");
                LastVOTE_BITS_MCC_RB = value;
                _voteBitsMCC_RB = value;
            }
        }

        private byte _voteBitsMCC2_RB { get; set; } = 0;
        public byte LastVOTE_BITS_MCC2_RB { get; private set; } = 0;
        public byte VOTE_BITS_MCC2_RB  // [404] MCC_VOTES2 readback
        {
            get { return _voteBitsMCC2_RB; }
            set
            {
                if (isVerboseLogEnabled && _voteBitsMCC2_RB != value)
                    Log?.Information($"VOTE_BITS_MCC2_RB CHANGED {Convert.ToString(_voteBitsMCC2_RB, 2).PadLeft(8, '0')} -> {Convert.ToString(value, 2).PadLeft(8, '0')}");
                LastVOTE_BITS_MCC2_RB = value;
                _voteBitsMCC2_RB = value;
            }
        }

        private byte _voteBitsKIZ { get; set; } = 0;
        public  byte LastVoteBitsKIZ { get; private set; } = 0;
        public  byte VoteBitsKIZ
        {
            get { return _voteBitsKIZ; }
            set
            {
                if (isVerboseLogEnabled && _voteBitsKIZ != value)
                    Log?.Information($"KIZ VOTE CHANGED {Convert.ToString(_voteBitsKIZ, 2).PadLeft(8, '0')} -> {Convert.ToString(value, 2).PadLeft(8, '0')}");
                LastVoteBitsKIZ = value;
                _voteBitsKIZ    = value;
            }
        }

        private byte _voteBitsLCH { get; set; } = 0;
        public  byte LastVoteBitsLCH { get; private set; } = 0;
        public  byte VoteBitsLCH
        {
            get { return _voteBitsLCH; }
            set
            {
                if (isVerboseLogEnabled && _voteBitsLCH != value)
                {
                    Log?.Information($"LCH VOTE CHANGED {Convert.ToString(_voteBitsLCH, 2).PadLeft(8, '0')} -> {Convert.ToString(value, 2).PadLeft(8, '0')}");
                    Log?.Information($"LOS {gimbalMSG.NED_Azimuth_deg.ToString("0.00")}, {gimbalMSG.NED_Elevation_deg.ToString("0.00")}");
                }
                LastVoteBitsLCH = value;
                _voteBitsLCH    = value;
            }
        }

        // ── Sub-message objects ───────────────────────────────────────────────
        public MSG_GIMBAL gimbalMSG { get; private set; } = new MSG_GIMBAL();
        public MSG_TRC    trcMSG    { get; private set; } = new MSG_TRC();
        public MSG_FMC    fmcMSG    { get; private set; } = new MSG_FMC();

        // ── Receive stats ─────────────────────────────────────────────────────
        public DateTime lastMsgRx { get; private set; } = DateTime.UtcNow;
        public double   RX_HB     { get; private set; } = 0;

        public double GimbalBasePitch { get; private set; } = 0;
        public double GimbalBaseRoll  { get; private set; } = 0;

        // ── Epoch time (PTP when synched, NTP otherwise) ──────────────────────
        // Session 32: renamed from _ntpTime / ntpTime — now source-agnostic.
        // ICD v3.0.0 session 4: RTC removed — uint64 ms only
        private UInt64  _epochTime { get; set; } = 0;
        public DateTime epochTime  { get { return DateTimeOffset.FromUnixTimeMilliseconds((long)_epochTime).UtcDateTime; } }

        // ── Temperatures ──────────────────────────────────────────────────────
        // ICD v3.0.0 session 4: TEMPERATURE_VICOR is int8 (was float)
        public sbyte TEMPERATURE_VICOR { get; private set; } = 0;

        // ── V2 temperature sensors (BDC Controller 1.0 Rev A) ────────────────
        // 0 on V1 — backward-compatible (was RESERVED 0x00)
        public sbyte TEMP_RELAY { get; private set; } = 0;
        public sbyte TEMP_BAT { get; private set; } = 0;
        public sbyte TEMP_USB { get; private set; } = 0;

        // HB counters — REG1 bytes [396-403]
        public double HB_NTP { get; private set; } = 0;   // seconds (x0.1s units / 10.0)
        public int HB_FMC_ms { get; private set; } = 0;   // raw ms
        public int HB_TRC_ms { get; private set; } = 0;   // raw ms
        public int HB_MCC_ms { get; private set; } = 0;   // raw ms
        public int HB_GIM_ms { get; private set; } = 0;   // raw ms
        public int HB_FUJI_ms { get; private set; } = 0;   // raw ms
        public int HB_MWIR_ms { get; private set; } = 0;   // raw ms
        public int HB_INCL_ms { get; private set; } = 0;   // raw ms
                                                           
        // Device STATUS_BITS bytes [404-411] — per-device health packed by firmware
        public byte DeviceWarnBits { get; private set; } = 0;  // [405] DEVICE_WARN_BITS
        public byte GIM_StatusBits { get; private set; } = 0;  // [406] BDC_GIM_STATUS_BITS
        public byte VIS_StatusBits { get; private set; } = 0;  // [407] BDC_VIS_STATUS_BITS
        public byte MWIR_StatusBits { get; private set; } = 0;  // [408] BDC_MWIR_STATUS_BITS
        public byte FSM_StatusBits { get; private set; } = 0;  // [409] BDC_FSM_STATUS_BITS
        public byte JET_StatusBits { get; private set; } = 0;  // [410] BDC_JET_STATUS_BITS
        public byte INCL_StatusBits { get; private set; } = 0;  // [411] BDC_INCL_STATUS_BITS

        // =========================================================================
        // Device STATUS_BITS accessors — mirror firmware bdc.hpp bit layout
        // =========================================================================

        // GIM_StatusBits [406]
        public bool isGIM_Connected { get { return (GIM_StatusBits & 0x01) != 0; } }  // b0
        public bool isGIM_Ready { get { return (GIM_StatusBits & 0x02) != 0; } }  // b1
        public bool isGIM_Started { get { return (GIM_StatusBits & 0x04) != 0; } }  // b2
        public bool isGIM_Moving { get { return (GIM_StatusBits & 0x10) != 0; } }  // b4 — display only

        // VIS_StatusBits [407]
        public bool isVIS_FujiConnected { get { return (VIS_StatusBits & 0x01) != 0; } }  // b0
        public bool isVIS_FujiHB_OK { get { return (VIS_StatusBits & 0x02) != 0; } }  // b1
        public bool isVIS_FOV_Valid { get { return (VIS_StatusBits & 0x04) != 0; } }  // b2
        public bool isVIS_AlviumPowered { get { return (VIS_StatusBits & 0x08) != 0; } }  // b3
        public bool isVIS_AlviumConnected { get { return (VIS_StatusBits & 0x10) != 0; } }  // b4
        public bool isVIS_Capturing { get { return (VIS_StatusBits & 0x20) != 0; } }  // b5

        // MWIR_StatusBits [408]
        public bool isMWIR_Connected { get { return (MWIR_StatusBits & 0x01) != 0; } }  // b0
        public bool isMWIR_HB_OK { get { return (MWIR_StatusBits & 0x02) != 0; } }  // b1
        public bool isMWIR_WarmupComplete { get { return (MWIR_StatusBits & 0x04) != 0; } }  // b2
        public bool isMWIR_FOV_Valid { get { return (MWIR_StatusBits & 0x08) != 0; } }  // b3
        public bool isMWIR_Capturing { get { return (MWIR_StatusBits & 0x10) != 0; } }  // b4

        // FSM_StatusBits [409]
        public bool isFSM_FMCConnected { get { return (FSM_StatusBits & 0x01) != 0; } }  // b0
        public bool isFSM_HB_OK { get { return (FSM_StatusBits & 0x02) != 0; } }  // b1
        public bool isFSM_NotLimited { get { return (FSM_StatusBits & 0x08) != 0; } }  // b3

        // JET_StatusBits [410]
        public bool isJET_Connected { get { return (JET_StatusBits & 0x01) != 0; } }  // b0
        public bool isJET_Ready { get { return (JET_StatusBits & 0x02) != 0; } }  // b1
        public bool isJET_Started { get { return (JET_StatusBits & 0x04) != 0; } }  // b2
        public bool isJET_Streaming { get { return (JET_StatusBits & 0x08) != 0; } }  // b3
        public bool isJET_CPU_OK { get { return (JET_StatusBits & 0x10) != 0; } }  // b4
        public bool isJET_GPU_OK { get { return (JET_StatusBits & 0x20) != 0; } }  // b5
        public bool isJET_CPU_TempOK { get { return (JET_StatusBits & 0x40) != 0; } }  // b6
        public bool isJET_GPU_TempOK { get { return (JET_StatusBits & 0x80) != 0; } }  // b7

        // INCL_StatusBits [411]
        public bool isINCL_Connected { get { return (INCL_StatusBits & 0x01) != 0; } }  // b0
        public bool isINCL_HB_OK { get { return (INCL_StatusBits & 0x02) != 0; } }  // b1
        public bool isINCL_DataValid { get { return (INCL_StatusBits & 0x04) != 0; } }  // b2

        // ── Hardware revision ─────────────────────────────────────────────────
        public byte HW_REV { get; private set; } = 0;
        public bool IsV1 => HW_REV == 0x01;
        public bool IsV2 => HW_REV == 0x02;
        public string HW_REV_Label => HW_REV == 0x01 ? "V1"
                                    : HW_REV == 0x02 ? "V2 — Controller 1.0 Rev A"
                                    : $"unknown (0x{HW_REV:X2})";

        public float TEMPERATURE { get; private set; } = 0;
        public float  PRESSURE         { get; private set; } = 0;
        public float  HUMIDITY         { get; private set; } = 0;

        // ── MWIR / VIS optics ─────────────────────────────────────────────────
        public MWIR_RUN_STATES MWIR_Run_State { get; private set; } = MWIR_RUN_STATES.BOOT;
        public float MWIR_Temp_S0  { get; private set; } = 0;
        public float MWIR_Temp_FPA { get; private set; } = 0;
        public uint  MWIR_FOV_ndx  { get; private set; } = 0;
        public float MWIR_FOV      { get; private set; } = 0;
        public uint  VIS_FOV_ndx   { get; private set; } = 0;
        public float VIS_FOV       { get; private set; } = 0;

        // ── FSM commanded X/Y — BDC REG1 [233-236] ───────────────────────────
        // ICD v3.0.0 session 4: FSM commanded X/Y now at BDC REG1 [233-236] (int16)
        // FSM ADC readback X/Y are inside the FMC 64-byte block at FMC [20-27] (int32)
        public Int16 FSM_X_C { get; private set; } = 0;
        public Int16 FSM_Y_C { get; private set; } = 0;

        // ── Platform + track ──────────────────────────────────────────────────
        public ptLLA  PLATFORM_LLA  { get; private set; } = new ptLLA();
        public RPY    PLATFORM_RPY  { get; private set; } = new RPY();
        public Int32  TARGET_PAN    { get; private set; } = 0;
        public Int32  TARGET_TILT   { get; private set; } = 0;

        public double3 PID_GAINS_CUE_PAN  { get; private set; } = new double3(0, 0, 0);
        public double3 PID_GAINS_CUE_TILT { get; private set; } = new double3(0, 0, 0);
        public double3 PID_GAINS_VID_PAN  { get; private set; } = new double3(0, 0, 0);
        public double3 PID_GAINS_VID_TILT { get; private set; } = new double3(0, 0, 0);

        public PointF LOS_GIM    { get { return new PointF((float)gimbalMSG.NED_Azimuth_deg, (float)gimbalMSG.NED_Elevation_deg); } }
        public PointF LOS_FSM_RB { get; private set; } = new PointF();
        public PointF LOS_FSM_C  { get; private set; } = new PointF();
        public float  HorizonBuffer { get; private set; } = -1.5f;

        // ICD v3.0.0 session 4: MCU Temp float at BDC REG1 [387-390]
        public float TEMP_MCU { get; private set; } = 0;

        // TIME_BITS [byte 391] — session 32, mirrors TMC STATUS_BITS3 exactly
        // Single authoritative time source byte. Named tb_ accessors below.
        // The existing accessors (isPTP_Enabled, usingPTP, etc.) remain valid —
        // TimeBits consolidates the same information into one register byte.
        public byte TimeBits { get; private set; } = 0;
        public bool tb_isPTP_Enabled    { get { return IsBitSet(TimeBits, 0); } }
        public bool tb_isPTP_Synched    { get { return IsBitSet(TimeBits, 1); } }
        public bool tb_usingPTP         { get { return IsBitSet(TimeBits, 2); } }
        public bool tb_isNTP_Synched    { get { return IsBitSet(TimeBits, 3); } }
        public bool tb_ntpUsingFallback { get { return IsBitSet(TimeBits, 4); } }
        public bool tb_ntpHasFallback   { get { return IsBitSet(TimeBits, 5); } }

        // Last STATUS byte received from A3 frame — useful for diagnostics
        public byte LastFrameStatus { get; private set; } = 0xFF;

        // =========================================================================
        // Parse  —  single public entry point. Dispatches to ParseA3 or ParseA2
        // based on TransportPath set at construction. Callers never choose directly.
        // =========================================================================
        public void Parse(byte[] data)
        {
            if (Transport == TransportPath.A3_External)
                ParseA3(data);
            else
                ParseA2(data);
        }

        // =========================================================================
        // ParseA3  —  validates 521-byte A3 framed response, dispatches on CMD
        // =========================================================================
        private void ParseA3(byte[] frame)
        {
            if (frame == null || frame.Length != FRAME_RESPONSE_LEN)
            {
                Log?.Warning("MSG_BDC: bad frame length {Len}", frame?.Length);
                return;
            }
            if (frame[0] != MAGIC_HI || frame[1] != MagicLo)
            {
                Log?.Warning("MSG_BDC: bad magic 0x{Hi:X2} 0x{Lo:X2}", frame[0], frame[1]);
                return;
            }
            ushort computed = CrcHelper.Crc16(frame, FRAME_RESPONSE_LEN - 2);
            ushort received = (ushort)((frame[519] << 8) | frame[520]);
            if (computed != received)
            {
                Log?.Warning("MSG_BDC: CRC mismatch computed=0x{Comp:X4} received=0x{Recv:X4}", computed, received);
                return;
            }
            LastFrameStatus = frame[4];
            if (frame[4] != STATUS_OK)
            {
                if (isVerboseLogEnabled)
                    Log?.Debug("MSG_BDC: STATUS=0x{Status:X2}", frame[4]);
                return;
            }

            var now = DateTime.UtcNow;
            RX_HB     = (now - lastMsgRx).TotalMilliseconds;
            lastMsgRx = now;

            ICD cmd = (ICD)frame[3];
            if ((byte)cmd == 0x00 || (byte)cmd == 0xA1 || cmd == ICD.FRAME_KEEPALIVE)  // REG1 CMD_BYTE: 0x00 (v4.0.0) | 0xA1 (legacy pre-FW-C10)
                ParseMSG01(frame, PAYLOAD_OFFSET + 1);
        }

        // =========================================================================
        // ParseA2  —  raw A2 payload path (ENG GUI, no frame header)
        // =========================================================================
        private void ParseA2(byte[] msg)
        {
            RX_HB     = (DateTime.UtcNow - lastMsgRx).TotalMilliseconds;
            lastMsgRx = DateTime.UtcNow;

            int ndx = 0;
            ICD cmd = (ICD)msg[ndx]; ndx++;

            if ((byte)cmd == 0x00 || (byte)cmd == 0xA1 || cmd == ICD.FRAME_KEEPALIVE)  // REG1 CMD_BYTE: 0x00 (v4.0.0) | 0xA1 (legacy pre-FW-C10)
                ParseMSG01(msg, ndx);
        }


        // maxes + avgs
        public uint dtmax = 0;
        public double HbMax = 0;
        public double DtAvg = 0;
        public double HbAvg = 0;
        public double DUtcMax = 0;

        // thresholds
        public const double DT_WARN_US = 15000.0;
        public const double DT_BAD_US = 30000.0;
        public const double HB_WARN_MS = 15000.0;
        public const double HB_BAD_MS = 30000.0;
        public const double DUTC_WARN_MS = 10.0;
        public const double DUTC_BAD_MS = 30.0;
        private const double EWMA_ALPHA = 0.10;

        public CB.READY_STATUS CommHealth
        {
            get
            {
                if (dt_us > DT_BAD_US || HB_ms > HB_BAD_MS) return CB.READY_STATUS.ERROR;
                if (dt_us > DT_WARN_US || HB_ms > HB_WARN_MS) return CB.READY_STATUS.WARN;
                if (dt_us == 0 && HB_ms == 0) return CB.READY_STATUS.NA;
                return CB.READY_STATUS.READY;
            }
        }

        public CB.READY_STATUS DUtcHealth(double dUtcAbsMs)
        {
            if (dUtcAbsMs > DUTC_BAD_MS) return CB.READY_STATUS.ERROR;
            if (dUtcAbsMs > DUTC_WARN_MS) return CB.READY_STATUS.WARN;
            return CB.READY_STATUS.READY;
        }

        public void ResetDt() { dtmax = 0; DtAvg = 0; }
        public void ResetHb() { HbMax = 0; HbAvg = 0; }
        public void ResetDUtc() { DUtcMax = 0; }
        public void ResetCommHealth() { ResetDt(); ResetHb(); ResetDUtc(); }

        // =========================================================================
        // ParseMSG01 — BDC REG1 512-byte block (ICD v3.0.0 session 4)
        //
        // ndx enters at payload[1] (State), i.e. frame byte 8 on A3 path.
        //
        // [1]       State
        // [2]       Mode
        // [3]       ActiveCamID
        // [4–5]     HB_ms        uint16
        // [6–7]     dt_us        uint16
        // [8–11]    Device / status bits (×4)
        // [12–19]   epoch ms uint64  (PTP when synched, NTP otherwise — session 32)
        // [20–58]   Gimbal block (MSG_GIMBAL)
        // [59]      TRC STATUS BITS
        // [60–123]  TRC 64-byte block (MSG_TRC)
        // [124–131] Gimbal inclinometer base angles (2×float)
        // [132]     TEMPERATURE_VICOR  int8
        // [133–144] TPH Temp/Pressure/Humidity (3×float)
        // [145–163] MWIR + VIS (1+4+4+1+4+1+4 bytes = 19)
        // [164–168] Vote bits (×5)
        // [169–232] FMC 64-byte block (MSG_FMC)
        // [233–236] FSM commanded X/Y (2×int16)
        // [237–244] Gimbal home int32 (×2)
        // [245–276] Platform LLA + RPY
        // [277–284] CUE track target (2×int32)
        // [285–308] CUE PID gains (6×float)
        // [309–332] VIDEO PID gains (6×float)
        // [333–362] FSM calibration data
        // [363–378] FSM NED LOS (4×float)
        // [379–382] Horizon buffer float
        // [383–386] BDC version word uint32
        // [387–390] MCU temp float
        // [391]     TIME_BITS (session 32) — isPTP_En, ptp.isSynched, usingPTP, ntp.isSynched, ntpUsingFB, ntpHasFB
        // [392]     HW_REV
        // [393-395] V2 temps (TEMP_RELAY, TEMP_BAT, TEMP_USB)
        // [396]     HB_NTP    uint8  x0.1s units
        // [397]     HB_FMC_ms uint8  raw ms
        // [398]     HB_TRC_ms uint8  raw ms
        // [399]     HB_MCC_ms uint8  raw ms
        // [400]     HB_GIM_ms uint8  raw ms
        // [401]     HB_FUJI_ms uint8  raw ms
        // [402]     HB_MWIR_ms uint8  raw ms
        // [403]     HB_INCL_ms uint8  raw ms
        // [404]     MCC_VOTES2_RB — MCC detail vote readback
        // [405]     DEVICE_WARN_BITS — per-device warn summary
        // [406]     BDC_GIM_STATUS_BITS
        // [407]     BDC_VIS_STATUS_BITS
        // [408]     BDC_MWIR_STATUS_BITS
        // [409]     BDC_FSM_STATUS_BITS
        // [410]     BDC_JET_STATUS_BITS
        // [411]     BDC_INCL_STATUS_BITS
        // [404-511]  RESERVED
        // =========================================================================
        private void ParseMSG01(byte[] msg, int ndx)
        {
            // [1-3] State / Mode / CAM
            State       = (SYSTEM_STATES)msg[ndx]; ndx++;
            Mode        = (BDC_MODES)    msg[ndx]; ndx++;
            ActiveCamID = (BDC_CAM_IDS)  msg[ndx]; ndx++;

            // [4-5] HB_ms uint16
            HB_ms = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(UInt16);

            // [6-7] dt_us uint16
            dt_us = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(UInt16);

            if (dt_us > dtmax)
            {
                dtmax = dt_us;
                Debug.WriteLine($"MSG_BDC: dt max = {dtmax}");
                if (isVerboseLogEnabled) Log?.Debug("MSG_BDC: dt max = {Dt}", dtmax);
            }
            if (HB_ms > HbMax) HbMax = HB_ms;
            DtAvg = (DtAvg == 0) ? dt_us : DtAvg + EWMA_ALPHA * (dt_us - DtAvg);
            HbAvg = (HbAvg == 0) ? HB_ms : HbAvg + EWMA_ALPHA * (HB_ms - HbAvg);

            // [8-11] Device / status bits
            DeviceEnabledBits = msg[ndx]; ndx++;
            DeviceReadyBits = msg[ndx]; ndx++;
            HealthBits = msg[ndx]; ndx++;   // byte [10] — was StatusBits
            PowerBits = msg[ndx]; ndx++;   // byte [11] — was StatusBits2

            // [12-19] epoch ms uint64 (PTP when synched, NTP otherwise — session 32)
            _epochTime = BitConverter.ToUInt64(msg, ndx); ndx += sizeof(UInt64);

            // [20-58] Gimbal block — returns 59
            ndx = gimbalMSG.ParseMsg(msg, ndx);

            // [59] TRC STATUS BITS — set on trcMSG before its parse
            trcMSG.StatusBits0 = msg[ndx]; ndx++;

            // [60-123] TRC 64-byte fixed block — returns 124
            ndx = trcMSG.ParseMsg(msg, ndx);

            // [124-131] Gimbal inclinometer base angles
            GimbalBasePitch      = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            GimbalBaseRoll       = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            gimbalMSG.BasePitch  = (float)GimbalBasePitch;
            gimbalMSG.BaseRoll   = (float)GimbalBaseRoll;

            // [132] Vicor temp int8 (was float — session 4)
            TEMPERATURE_VICOR = (sbyte)msg[ndx]; ndx++;

            // [133-144] TPH
            TEMPERATURE = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            PRESSURE    = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            HUMIDITY    = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            // [145-163] MWIR + VIS
            MWIR_Run_State = (MWIR_RUN_STATES)msg[ndx]; ndx++;
            MWIR_Temp_S0   = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            MWIR_Temp_FPA  = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            MWIR_FOV_ndx   = msg[ndx]; ndx++;
            MWIR_FOV       = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            VIS_FOV_ndx    = msg[ndx]; ndx++;
            VIS_FOV        = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            // [164-168] Vote bits
            VOTE_BITS_BDC2 = msg[ndx]; ndx++;  // [164] BDC_VOTES2 — raw/override detail
            VOTE_BITS_BDC = msg[ndx]; ndx++;  // [165] BDC_VOTES  — computed vote summary
            VOTE_BITS_MCC_RB = msg[ndx]; ndx++;  // [166] MCC_VOTES  readback
            VoteBitsKIZ = msg[ndx]; ndx++;  // [167] KIZ vote
            VoteBitsLCH = msg[ndx]; ndx++;  // [168] LCH vote

            // [169-232] FMC 64-byte fixed block
            ndx = fmcMSG.ParseMSG01(msg, ndx);

            // [233-236] FSM commanded X/Y int16 (BDC REG1 — not in FMC block)
            FSM_X_C = BitConverter.ToInt16(msg, ndx); ndx += sizeof(Int16);
            FSM_Y_C = BitConverter.ToInt16(msg, ndx); ndx += sizeof(Int16);

            // [237-244] Gimbal home int32 (was UInt32 — session 4)
            gimbalMSG.HomeX = BitConverter.ToInt32(msg, ndx); ndx += sizeof(Int32);
            gimbalMSG.HomeY = BitConverter.ToInt32(msg, ndx); ndx += sizeof(Int32);

            // [245-276] Platform LLA + RPY
            double lat = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            double lng = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            float  alt = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            PLATFORM_LLA = new ptLLA(lat, lng, alt);

            Single r = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single p = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single y = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            PLATFORM_RPY = new RPY(r, p, y);

            // [277-284] CUE track target
            TARGET_PAN  = BitConverter.ToInt32(msg, ndx); ndx += sizeof(Int32);
            TARGET_TILT = BitConverter.ToInt32(msg, ndx); ndx += sizeof(Int32);

            // [285-308] CUE PID gains
            Single pkp = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single pki = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single pkd = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single tkp = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single tki = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single tkd = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            PID_GAINS_CUE_PAN  = new double3(pkp, pki, pkd);
            PID_GAINS_CUE_TILT = new double3(tkp, tki, tkd);

            // [309-332] VIDEO PID gains
            Single vpkp = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single vpki = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single vpkd = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single vtkp = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single vtki = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single vtkd = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            PID_GAINS_VID_PAN  = new double3(vpkp, vpki, vpkd);
            PID_GAINS_VID_TILT = new double3(vtkp, vtki, vtkd);

            // [333-362] FSM calibration data — set on fmcMSG directly
            // NOTE: These are BDC REG1 fields — not part of the FMC 64-byte block
            fmcMSG.FSM_iFOV_X    = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            fmcMSG.FSM_iFOV_Y    = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            fmcMSG.FSM_X0        = BitConverter.ToInt16(msg, ndx);  ndx += sizeof(Int16);
            fmcMSG.FSM_Y0        = BitConverter.ToInt16(msg, ndx);  ndx += sizeof(Int16);
            fmcMSG.FSM_SIGN_X    = (sbyte)msg[ndx]; ndx++;
            fmcMSG.FSM_SIGN_Y    = (sbyte)msg[ndx]; ndx++;
            fmcMSG.StagePosition = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32);
            fmcMSG.StageHome     = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32);

            // [363-378] FSM NED LOS
            Single azFSM_rb = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single elFSM_rb = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single azFSM_c  = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            Single elFSM_c  = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            LOS_FSM_RB = new PointF(azFSM_rb, elFSM_rb);
            LOS_FSM_C  = new PointF(azFSM_c,  elFSM_c);

            // [379-382] Horizon buffer
            HorizonBuffer = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            // [383-386] BDC version word
            FW_VERSION = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32);

            // [387-390] MCU temp float
            TEMP_MCU = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            // [391] TIME_BITS (session 32) — consolidated time source status
            TimeBits = msg[ndx]; ndx++;

            // [392] HW_REV — 0x01=V1, 0x02=V2 (BDC Controller 1.0 Rev A)
            HW_REV = msg[ndx]; ndx++;

            // [393-395] V2 temperature sensors — 0x00 on V1 (backward-compatible)
            TEMP_RELAY = (sbyte)msg[ndx]; ndx++;
            TEMP_BAT = (sbyte)msg[ndx]; ndx++;
            TEMP_USB = (sbyte)msg[ndx]; ndx++;

            // [396-403] HB counters
            HB_NTP = (double)msg[ndx] / 10.0; ndx++;   // x0.1s → seconds
            HB_FMC_ms = (int)msg[ndx]; ndx++;
            HB_TRC_ms = (int)msg[ndx]; ndx++;
            HB_MCC_ms = (int)msg[ndx]; ndx++;
            HB_GIM_ms = (int)msg[ndx]; ndx++;
            HB_FUJI_ms = (int)msg[ndx]; ndx++;
            HB_MWIR_ms = (int)msg[ndx]; ndx++;
            HB_INCL_ms = (int)msg[ndx]; ndx++;

            VOTE_BITS_MCC2_RB = msg[ndx]; ndx++;  // [404] MCC_VOTES2 readback
            DeviceWarnBits = msg[ndx]; ndx++;  // [405] DEVICE_WARN_BITS
            GIM_StatusBits = msg[ndx]; ndx++;  // [406] BDC_GIM_STATUS_BITS
            VIS_StatusBits = msg[ndx]; ndx++;  // [407] BDC_VIS_STATUS_BITS
            MWIR_StatusBits = msg[ndx]; ndx++;  // [408] BDC_MWIR_STATUS_BITS
            FSM_StatusBits = msg[ndx]; ndx++;  // [409] BDC_FSM_STATUS_BITS
            JET_StatusBits = msg[ndx]; ndx++;  // [410] BDC_JET_STATUS_BITS
            INCL_StatusBits = msg[ndx]; ndx++;  // [411] BDC_INCL_STATUS_BITS
        }

        // =========================================================================
        // DeviceEnabled / DeviceReady accessors
        // ICD v3.0.0 session 4: bit 7 changed from RTC → RES
        // Session 32: bit 7 = PTP
        // =========================================================================
        public bool isNTP_DeviceEnabled { get { return IsBitSet(DeviceEnabledBits, (int)BDC_DEVICES.NTP); } }
        public bool isGimbal_DeviceEnabled { get { return IsBitSet(DeviceEnabledBits, (int)BDC_DEVICES.GIMBAL); } }
        public bool isFuji_DeviceEnabled { get { return IsBitSet(DeviceEnabledBits, (int)BDC_DEVICES.FUJI); } }
        public bool isMWIR_DeviceEnabled { get { return IsBitSet(DeviceEnabledBits, (int)BDC_DEVICES.MWIR); } }
        public bool isFSM_DeviceEnabled { get { return IsBitSet(DeviceEnabledBits, (int)BDC_DEVICES.FSM); } }
        public bool isFMC_DeviceEnabled { get { return isFSM_DeviceEnabled; } }   // alias
        public bool isTRC_DeviceEnabled { get { return IsBitSet(DeviceEnabledBits, (int)BDC_DEVICES.JETSON); } }
        public bool isINCL_DeviceEnabled { get { return IsBitSet(DeviceEnabledBits, (int)BDC_DEVICES.INCL); } }
        public bool isPTP_DeviceEnabled { get { return IsBitSet(DeviceEnabledBits, (int)BDC_DEVICES.PTP); } }

        public bool isNTP_DeviceReady { get { return IsBitSet(DeviceReadyBits, (int)BDC_DEVICES.NTP); } }
        public bool isGimbal_DeviceReady { get { return IsBitSet(DeviceReadyBits, (int)BDC_DEVICES.GIMBAL); } }
        public bool isFuji_DeviceReady { get { return IsBitSet(DeviceReadyBits, (int)BDC_DEVICES.FUJI); } }
        public bool isMWIR_DeviceReady { get { return IsBitSet(DeviceReadyBits, (int)BDC_DEVICES.MWIR); } }
        public bool isFSM_DeviceReady { get { return IsBitSet(DeviceReadyBits, (int)BDC_DEVICES.FSM); } }
        public bool isFMC_DeviceReady { get { return isFSM_DeviceReady; } }   // alias
        public bool isTRC_DeviceReady { get { return IsBitSet(DeviceReadyBits, (int)BDC_DEVICES.JETSON); } }
        public bool isINCL_DeviceReady { get { return IsBitSet(DeviceReadyBits, (int)BDC_DEVICES.INCL); } }
        public bool isPTP_DeviceReady { get { return IsBitSet(DeviceReadyBits, (int)BDC_DEVICES.PTP); } }

        public bool isNTP_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)BDC_DEVICES.NTP); } }
        public bool isGimbal_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)BDC_DEVICES.GIMBAL); } }
        public bool isFuji_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)BDC_DEVICES.FUJI); } }
        public bool isMWIR_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)BDC_DEVICES.MWIR); } }
        public bool isFSM_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)BDC_DEVICES.FSM); } }
        public bool isJetson_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)BDC_DEVICES.JETSON); } }
        public bool isINCL_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)BDC_DEVICES.INCL); } }
        public bool isPTP_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)BDC_DEVICES.PTP); } }

        // ADD — allows pre-connect ping to populate bits before REG1 arrives
        public void SetDeviceReady(BDC_DEVICES dev, bool ready)
        {
            if (ready)
                DeviceReadyBits |= (byte)(1 << (int)dev);
            else
                DeviceReadyBits &= (byte)~(1 << (int)dev);
        }

        // HealthBits accessors [byte 10] — renamed from StatusBits (ICD v3.5.0 BDC unification)
        // bit 0: isReady   bit 1: isSwitchEnabled (V2 only)   bits 2-7: RES
        // =========================================================================
        public bool isBDCReady { get { return IsBitSet(HealthBits, 0); } }
        public bool isSwitchEnabled { get { return IsV2 && IsBitSet(HealthBits, 1); } }

        public bool ntpUsingFallback { get { return tb_ntpUsingFallback; } }   // → TimeBits bit 4
        public bool ntpHasFallback { get { return tb_ntpHasFallback; } }   // → TimeBits bit 5
        public bool usingPTP { get { return tb_usingPTP; } }   // → TimeBits bit 2

        // Derived: active time source label — mirrors firmware TIME_SOURCE enum
        public string activeTimeSourceLabel
        {
            get
            {
                if (usingPTP) return "PTP";
                if (tb_isNTP_Synched) return ntpUsingFallback ? "NTP (fallback)" : "NTP";
                return "NONE";
            }
        }

        // PowerBits accessors [byte 11] — renamed from StatusBits2 (ICD v3.5.0 BDC unification)
        // Bit layout unchanged — rename only.
        public bool isPID_Enabled { get { return IsBitSet(PowerBits, 0); } }
        public bool isVPID_Enabled { get { return IsBitSet(PowerBits, 1); } }
        public bool isFT_Enabled { get { return IsBitSet(PowerBits, 2); } }
        public bool isVicor_Enabled { get { return IsBitSet(PowerBits, 3); } }
        public bool isRelay1_Enabled { get { return IsBitSet(PowerBits, 4); } }
        public bool isRelay2_Enabled { get { return IsBitSet(PowerBits, 5); } }
        public bool isRelay3_Enabled { get { return IsBitSet(PowerBits, 6); } }
        public bool isRelay4_Enabled { get { return IsBitSet(PowerBits, 7); } }

        // =========================================================================
        // VOTE_BITS_BDC2 [164] — BDC raw/override detail
        // =========================================================================
        public bool isHorizVoteOverride { get { return (VOTE_BITS_BDC2 & (byte)BDC_VOTES2.HORIZ_VOTE_OVERRIDE) != 0; } }
        public bool isKIZVoteOverride { get { return (VOTE_BITS_BDC2 & (byte)BDC_VOTES2.KIZ_VOTE_OVERRIDE) != 0; } }
        public bool isLCHVoteOverride { get { return (VOTE_BITS_BDC2 & (byte)BDC_VOTES2.LCH_VOTE_OVERRIDE) != 0; } }
        public bool isBDCVoteOverride { get { return (VOTE_BITS_BDC2 & (byte)BDC_VOTES2.BDC_VOTE_OVERRIDE) != 0; } }
        public bool isBelowHoriz { get { return (VOTE_BITS_BDC2 & (byte)BDC_VOTES2.IS_BELOW_HORIZ) != 0; } }
        public bool isInKIZ { get { return (VOTE_BITS_BDC2 & (byte)BDC_VOTES2.IS_IN_KIZ) != 0; } }
        public bool isInLCH { get { return (VOTE_BITS_BDC2 & (byte)BDC_VOTES2.IS_IN_LCH) != 0; } }

        // =========================================================================
        // VOTE_BITS_BDC [165] — BDC computed vote summary
        // =========================================================================
        public bool BelowHorizVote { get { return (VOTE_BITS_BDC & (byte)BDC_VOTES.BELOW_HORIZ_VOTE) != 0; } }
        public bool InKIZVote { get { return (VOTE_BITS_BDC & (byte)BDC_VOTES.IN_KIZ_VOTE) != 0; } }
        public bool InLCHVote { get { return (VOTE_BITS_BDC & (byte)BDC_VOTES.IN_LCH_VOTE) != 0; } }
        public bool BDCTotalVote { get { return (VOTE_BITS_BDC & (byte)BDC_VOTES.BDC_TOTAL_VOTE) != 0; } }
        public bool isHorizonLoaded { get { return (VOTE_BITS_BDC & (byte)BDC_VOTES.HORIZ_LOADED) != 0; } }
        public bool isFSMNotLimited { get { return (VOTE_BITS_BDC & (byte)BDC_VOTES.FSM_NOT_LIMITED) != 0; } }

        // =========================================================================
        // VOTE_BITS_MCC_RB [166] — MCC_VOTES readback (MCC→BDC via 0xE0)
        // =========================================================================
        public bool isNotAbort_Vote_rb { get { return (VOTE_BITS_MCC_RB & (byte)MCC_VOTES.NOT_ABORT) != 0; } }
        public bool isArmed_Vote_rb { get { return (VOTE_BITS_MCC_RB & (byte)MCC_VOTES.ARMED) != 0; } }
        public bool isBDC_Vote_rb { get { return (VOTE_BITS_MCC_RB & (byte)MCC_VOTES.BDC_VOTE) != 0; } }
        public bool isLaserTotalHW_Vote_rb { get { return (VOTE_BITS_MCC_RB & (byte)MCC_VOTES.LASER_TOTAL_HW) != 0; } }
        public bool isSW_Vote_rb { get { return (VOTE_BITS_MCC_RB & (byte)MCC_VOTES.SW_VOTE) != 0; } }
        public bool isTrigger_Vote_rb { get { return (VOTE_BITS_MCC_RB & (byte)MCC_VOTES.TRIGGER) != 0; } }
        public bool isFireState_Vote_rb { get { return (VOTE_BITS_MCC_RB & (byte)MCC_VOTES.FIRE_STATE) != 0; } }
        public bool isEMON { get { return (VOTE_BITS_MCC_RB & (byte)MCC_VOTES.EMON) != 0; } }

        // =========================================================================
        // VOTE_BITS_MCC2_RB [404] — MCC_VOTES2 readback (MCC→BDC via 0xE0)
        // =========================================================================
        public bool isMCC2_BatNotLow { get { return (VOTE_BITS_MCC2_RB & (byte)MCC_VOTES2.BAT_NOT_LOW) != 0; } }
        public bool isMCC2_TrainingMode { get { return (VOTE_BITS_MCC2_RB & (byte)MCC_VOTES2.TRAINING_MODE) != 0; } }
        public bool isMCC2_Combat { get { return (VOTE_BITS_MCC2_RB & (byte)MCC_VOTES2.COMBAT) != 0; } }
        public bool isMCC2_EMON_Missing { get { return (VOTE_BITS_MCC2_RB & (byte)MCC_VOTES2.EMON_MISSING) != 0; } }
        public bool isMCC2_EMON_Unexpected { get { return (VOTE_BITS_MCC2_RB & (byte)MCC_VOTES2.EMON_UNEXPECTED) != 0; } }
        public bool isMCC2_FireInterlocked { get { return (VOTE_BITS_MCC2_RB & (byte)MCC_VOTES2.FIRE_INTERLOCKED) != 0; } }


        // =========================================================================
        // VoteBitsKIZ [167]
        // =========================================================================
        public bool isKIZLoaded        { get { return IsBitSet(VoteBitsKIZ, 0); } }
        public bool isKIZEnabled       { get { return IsBitSet(VoteBitsKIZ, 1); } }
        public bool isKIZTimeValid     { get { return IsBitSet(VoteBitsKIZ, 2); } }
        public bool isKIZOperatorValid { get { return IsBitSet(VoteBitsKIZ, 3); } }
        public bool isKIZPositionValid { get { return IsBitSet(VoteBitsKIZ, 4); } }
        public bool isKIZForExec       { get { return IsBitSet(VoteBitsKIZ, 5); } }
        public bool isInKIZ2           { get { return IsBitSet(VoteBitsKIZ, 6); } }
        public bool InKIZVote2         { get { return IsBitSet(VoteBitsKIZ, 7); } }

        // =========================================================================
        // VoteBitsLCH [168]
        // =========================================================================
        public bool isLCHLoaded        { get { return IsBitSet(VoteBitsLCH, 0); } }
        public bool isLCHEnabled       { get { return IsBitSet(VoteBitsLCH, 1); } }
        public bool isLCHTimeValid     { get { return IsBitSet(VoteBitsLCH, 2); } }
        public bool isLCHOperatorValid { get { return IsBitSet(VoteBitsLCH, 3); } }
        public bool isLCHPositionValid { get { return IsBitSet(VoteBitsLCH, 4); } }
        public bool isLCHForExec       { get { return IsBitSet(VoteBitsLCH, 5); } }
        public bool isInLCH2           { get { return IsBitSet(VoteBitsLCH, 6); } }
        public bool InLCHVote2         { get { return IsBitSet(VoteBitsLCH, 7); } }

        // =========================================================================
        // Helpers
        // =========================================================================
        bool IsBitSet(byte b, int pos) { return (b & (1 << pos)) != 0; }
    }
}
