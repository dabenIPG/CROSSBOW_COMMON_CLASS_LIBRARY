using System;
using Serilog;
using System.Diagnostics;


namespace CROSSBOW
{

    // MSG_MCC.cs  —  updated for ICD v3.3.0 session 28
    //
    // Session 28 changes (PTP integration):
    //   - isPTP_DeviceEnabled / isPTP_DeviceReady added (DeviceEnabledBits/DeviceReadyBits bit 4)
    //     Previously bit 4 was RTCLOCK (deprecated session 4) then RES — now PTP time service.
    //     MCC_DEVICES.PTP = 4 (renamed from RTCLOCK session 29 — resolved)
    //   - StatusBits2 accessors added: ntpUsingFallback (bit 0), ntpHasFallback (bit 1),
    //     usingPTP (bit 2). Previously StatusBits2 had no named accessors.
    //     Session 32: these bits moved to TimeBits byte 253 — bits 0-2 now RES.
    //     ntpUsingFallback / ntpHasFallback / usingPTP now redirect to tb_* for compat.
    //   - TIME_SOURCE enum added: None, PTP, NTP
    //   - activeTimeSource computed property — derived from isPTP_DeviceReady + usingPTP
    //   - activeTimeSourceLabel string property — for status display ("PTP" / "NTP" / "NTP (fallback)" / "NONE")
    //   - epochTime property added — preferred alias for ntpTime. The time field at
    //     bytes [12–19] reflects whichever source MCC is running on (PTP or NTP).
    //     ntpTime retained as backward-compatible alias.
    //   - MCC FW version updated to 3.1.0
    //
    // Session 4 changes (preserved for reference):
    //   - ActiveCamID removed (field no longer in REG1)
    //   - HB_TX_us uint32 µs  →  HB_ms ushort ms  (HB_TX_ms / HB_TX_us kept as compat aliases)
    //   - dt uint32           →  dt_us ushort      (dt kept as compat alias)
    //   - RTC block removed   (_rtcTime, rtcTime, dTime_NTP2RTC, HB_RTC)
    //   - NTP field is now ms-since-epoch Int64 only (8 bytes, no RTC field after it)
    //   - TEMPERATURE_CHARGER / TEMPERATURE_AIR: float (4 bytes) → int8 (1 byte each)
    //   - TMCMsg.Parse() enabled — 64-byte embedded block at bytes 66–129
    //   - SW_VERSION_STRING updated for new semver uint32 encoding
    //   - isRTC_DeviceEnabled / isRTC_DeviceReady deprecated (bit 4: RTCLOCK → RES → PTP)
    //   - isRelay3_Enabled / isRelay4_Enabled added (StatusBits2 bits 6 and 7)
    //   - TransportPath enum — constructor selects A2 or A3 at construction time.
    //     Callers always use Parse(). ParseA3/ParseA2 are private implementation.
    //     THEIA:    new MSG_MCC(log, TransportPath.A3_External)
    //     ENG GUI:  new MSG_MCC(log, TransportPath.A2_Internal)
    // ---------------------------------------------------------------------------

    /// <summary>Active time source on MCC — derived from register bits.</summary>
    public enum TIME_SOURCE { None, PTP, NTP }

    /// <summary>Transport path — set once at construction, determines Parse() behaviour.</summary>
    public enum TransportPath { A2_Internal, A3_External }

    public class MSG_MCC
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

        public MSG_MCC(ILogger _log, TransportPath transport = TransportPath.A3_External)
        {
            Log       = _log;
            Transport = transport;
        }
        public MSG_MCC(TransportPath transport = TransportPath.A3_External)
        {
            Transport = transport;
        }

        // ── Embedded sub-parsers ──────────────────────────────────────────────
        public MSG_BATTERY BatteryMsg { get; private set; } = new MSG_BATTERY();
        public MSG_IPG     IPGMsg     { get; private set; } = new MSG_IPG();
        public MSG_TMC     TMCMsg     { get; private set; } = new MSG_TMC();
        public MSG_GNSS    GNSSMsg    { get; private set; } = new MSG_GNSS();
        public MSG_CMC     CMCMsg     { get; private set; } = new MSG_CMC();

        // ── System state ──────────────────────────────────────────────────────
        public SYSTEM_STATES System_State { get; private set; } = SYSTEM_STATES.OFF;
        public BDC_MODES     BDC_Mode     { get; private set; } = BDC_MODES.OFF;

        // ── Heartbeat / timing ────────────────────────────────────────────────
        // ICD v3.0.0 session 4: HB is now uint16 milliseconds (was uint32 microseconds)
        public UInt16 HB_ms { get; private set; } = 1000;
        public UInt16 dt_us { get; private set; } = 0;

        public double HB_TX_ms { get { return (double)HB_ms; } }

        // ── Version ───────────────────────────────────────────────────────────
        public UInt32 FW_VERSION { get; private set; } = 0;
        public string FW_VERSION_STRING
        {
            get
            {
                uint major = (FW_VERSION >> 24) & 0xFF;
                uint minor = (FW_VERSION >> 12) & 0xFFF;
                uint patch = FW_VERSION & 0xFFF;
                return $"{major}.{minor}.{patch}";
            }
        }
        public uint FW_MAJOR => (FW_VERSION >> 24) & 0xFF;   // firmware major version
        public bool IsV4 => FW_MAJOR >= 4;                   // true = ICD v3.6.0 command space (v4.0.0+)

        // ── Status / device bits ──────────────────────────────────────────────
        public byte DeviceEnabledBits { get; private set; } = 0;
        public byte DeviceReadyBits   { get; private set; } = 0;
        public byte HealthBits { get; private set; } = 0;   // byte 9 — renamed from StatusBits
        public byte PowerBits { get; private set; } = 0;   // byte 10 — renamed from StatusBits2
        public byte HW_REV { get; private set; } = 0;   // byte 254 — 0x01=V1, 0x02=V2, 0x03=V3
        public bool IsV1 => HW_REV == 0x01;
        public bool IsV2 => HW_REV == 0x02;
        public bool IsV3 => HW_REV == 0x03;
        public string HW_REV_Label => HW_REV == 0x01 ? "V1 — 48V·3kW · relay/solenoids/chargerI2C"
                                    : HW_REV == 0x02 ? "V2 — 300V·6kW · dualVicor/noSol/noI2C"
                                    : HW_REV == 0x03 ? "V3 — 48V or 300V · monolithic PCB"
                                    : $"unknown (0x{HW_REV:X2})";

        public LASER_MODEL LaserModel { get; private set; } = LASER_MODEL.UNKNOWN;  // byte 255 — v3.5.0

        // Device STATUS_BITS bytes [256-263] — per-device health packed by firmware
        public byte DeviceWarnBits { get; private set; } = 0;  // [257] DEVICE_WARN_BITS
        public byte TMC_StatusBits { get; private set; } = 0;  // [258] MCC_TMC_STATUS_BITS
        public byte HEL_StatusBits { get; private set; } = 0;  // [259] MCC_HEL_STATUS_BITS
        public byte BAT_StatusBits { get; private set; } = 0;  // [260] MCC_BAT_STATUS_BITS
        public byte CRG_StatusBits { get; private set; } = 0;  // [261] MCC_CRG_STATUS_BITS
        public byte GNSS_StatusBits { get; private set; } = 0;  // [262] MCC_GNSS_STATUS_BITS
        public byte BDC_StatusBits { get; private set; } = 0;  // [263] MCC_BDC_STATUS_BITS

        // =========================================================================
        // Device STATUS_BITS accessors — mirror firmware mcc.hpp bit layout
        // =========================================================================

        // TMC_StatusBits [258]
        public bool isTMC_Connected { get { return (TMC_StatusBits & 0x01) != 0; } }  // b0

        // HEL_StatusBits [259]
        public bool isHEL_Sensed { get { return (HEL_StatusBits & 0x01) != 0; } }  // b0
        public bool isHEL_HB_OK { get { return (HEL_StatusBits & 0x02) != 0; } }  // b1
        public bool isHEL_NotReady { get { return (HEL_StatusBits & 0x04) != 0; } }  // b2 — set = error
        public bool isHEL_ModelMatch { get { return (HEL_StatusBits & 0x08) != 0; } }  // b3
        public bool isHEL_EMON_sb { get { return (HEL_StatusBits & 0x10) != 0; } }  // b4 — display only
        public bool isHEL_EMON_Missing { get { return (HEL_StatusBits & 0x20) != 0; } }  // b5
        public bool isHEL_EMON_Unexpected { get { return (HEL_StatusBits & 0x40) != 0; } }  // b6
        public bool isHEL_FireInterlocked { get { return (HEL_StatusBits & 0x80) != 0; } }  // b7

        // BAT_StatusBits [260]
        public bool isBAT_Connected { get { return (BAT_StatusBits & 0x01) != 0; } }  // b0
        public bool isBAT_NotLowVoltage { get { return (BAT_StatusBits & 0x02) != 0; } }  // b1
        public bool isBAT_Charging { get { return (BAT_StatusBits & 0x04) != 0; } }  // b2 — display only
        public bool isBAT_Discharging { get { return (BAT_StatusBits & 0x08) != 0; } }  // b3 — display only

        // CRG_StatusBits [261]
        public bool isCRG_Connected { get { return (CRG_StatusBits & 0x01) != 0; } }  // b0
        public bool isCRG_Enabled { get { return (CRG_StatusBits & 0x02) != 0; } }  // b1

        // GNSS_StatusBits [262]
        public bool isGNSS_Connected { get { return (GNSS_StatusBits & 0x01) != 0; } }  // b0
        public bool isGNSS_HB_OK { get { return (GNSS_StatusBits & 0x02) != 0; } }  // b1
        public bool isGNSS_PositionValid { get { return (GNSS_StatusBits & 0x04) != 0; } }  // b2
        public bool isGNSS_SIV_OK { get { return (GNSS_StatusBits & 0x08) != 0; } }  // b3

        // BDC_StatusBits [263]
        public bool isBDC_Enabled { get { return (BDC_StatusBits & 0x01) != 0; } }  // b0
        public bool isBDC_Reachable { get { return (BDC_StatusBits & 0x02) != 0; } }  // b1

        public bool isVerboseLogEnabled { get; set; } = true;

        /// <summary>
        /// Active time source — derived from register bits.
        /// PTP: isPTP_DeviceReady=true AND usingPTP=true.
        /// NTP: isPTP_DeviceReady=false OR usingPTP=false, AND isNTP_DeviceReady=true.
        /// </summary>
        public TIME_SOURCE activeTimeSource
        {
            get
            {
                if (isPTP_DeviceReady && usingPTP) return TIME_SOURCE.PTP;
                if (isNTP_DeviceReady)             return TIME_SOURCE.NTP;
                return TIME_SOURCE.None;
            }
        }

        /// <summary>Human-readable label for status panels.</summary>
        public string activeTimeSourceLabel
        {
            get
            {
                switch (activeTimeSource)
                {
                    case TIME_SOURCE.PTP: return "PTP";
                    case TIME_SOURCE.NTP: return ntpUsingFallback ? "NTP (fallback)" : "NTP";
                    default:             return "NONE";
                }
            }
        }
        private byte _voteBitsMCC { get; set; } = 0;
        public byte LastVOTE_BITS_MCC { get; private set; } = 0;
        public byte VOTE_BITS_MCC  // [11] MCC gate-chain summary
        {
            get { return _voteBitsMCC; }
            set
            {
                if (isVerboseLogEnabled && _voteBitsMCC != value)
                    Log?.Information($"MCC VOTES CHANGED {Convert.ToString(_voteBitsMCC, 2).PadLeft(8, '0')} -> {Convert.ToString(value, 2).PadLeft(8, '0')}");
                LastVOTE_BITS_MCC = value;
                _voteBitsMCC = value;
            }
        }

        private byte _voteBitsMCC2 { get; set; } = 0;
        public byte LastVOTE_BITS_MCC2 { get; private set; } = 0;
        public byte VOTE_BITS_MCC2  // [256] MCC detail
        {
            get { return _voteBitsMCC2; }
            set
            {
                if (isVerboseLogEnabled && _voteBitsMCC2 != value)
                    Log?.Information($"MCC VOTES2 CHANGED {Convert.ToString(_voteBitsMCC2, 2).PadLeft(8, '0')} -> {Convert.ToString(value, 2).PadLeft(8, '0')}");
                LastVOTE_BITS_MCC2 = value;
                _voteBitsMCC2 = value;
            }
        }

        // ── Receive stats ─────────────────────────────────────────────────────
        public DateTime lastMsgRx { get; private set; } = DateTime.UtcNow;
        public double   RX_HB     { get; private set; } = 0;

        // ── NTP ───────────────────────────────────────────────────────────────
        // ICD v3.0.0 session 4: epoch ms field (bytes 12-19) — RTC removed.
        // Session 28: reflects whichever source MCC is running on (PTP or NTP).
        // Use epochTime (preferred) or ntpTime (backward-compat alias).
        private Int64   _ntpTime  { get; set; } = 0;
        public DateTime epochTime { get { return DateTimeOffset.FromUnixTimeMilliseconds(_ntpTime).UtcDateTime; } }
        public DateTime ntpTime   { get { return epochTime; } }   // backward-compat alias

        // ── Temperatures ──────────────────────────────────────────────────────
        // ICD v3.0.0 session 4: Temp1/Temp2 changed from float to int8
        public double TEMPERATURE_CHARGER { get; private set; } = 0;
        public double TEMPERATURE_AIR     { get; private set; } = 0;

        // TPH sensor — float, unchanged
        public double TEMPERATURE { get; private set; } = 0;
        public double PRESSURE    { get; private set; } = 0;
        public double HUMIDITY    { get; private set; } = 0;

        // ── Heartbeat counters ────────────────────────────────────────────────
        // ICD v3.0.0 session 4: HB_RTC removed
        public double HB_NTP  { get; private set; } = 0;
        public int HB_HEL_ms  { get; private set; } = 0;
        public int HB_BAT_ms { get; private set; } = 0;
        public int HB_CRG_ms { get; private set; } = 0;
        public int HB_GNSS_ms { get; private set; } = 0;

        // MCU die temperature
        public double TEMP_MCU { get; private set; } = 0;

        // TIME_BITS [byte 253] — session 32, mirrors TMC STATUS_BITS3 exactly
        // Single authoritative time source byte. Named tb_ accessors below.
        // The existing accessors (isPTP_DeviceReady, usingPTP, etc.) remain valid —
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
                Log?.Warning("MSG_MCC: bad frame length {Len}", frame?.Length);
                return;
            }
            if (frame[0] != MAGIC_HI || frame[1] != MagicLo)
            {
                Log?.Warning("MSG_MCC: bad magic 0x{Hi:X2} 0x{Lo:X2}", frame[0], frame[1]);
                return;
            }
            ushort computed = CrcHelper.Crc16(frame, FRAME_RESPONSE_LEN - 2);
            ushort received = (ushort)((frame[519] << 8) | frame[520]);
            if (computed != received)
            {
                Log?.Warning("MSG_MCC: CRC mismatch computed=0x{Comp:X4} received=0x{Recv:X4}", computed, received);
                return;
            }
            LastFrameStatus = frame[4];
            if (frame[4] != STATUS_OK)
            {
                if (isVerboseLogEnabled)
                    Log?.Debug("MSG_MCC: STATUS=0x{Status:X2}", frame[4]);
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

            if ((byte)cmd == 0x00 || (byte)cmd == 0xA1)  // REG1 CMD_BYTE: 0x00 (v4.0.0) | 0xA1 (legacy pre-FW-C10)
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
        // ParseMSG01  —  ICD v3.0.0 session 4 layout
        //
        // Byte offsets in payload (payload[0] = CMD byte, already consumed):
        //   [1]       System State
        //   [2]       System Mode
        //   [3-4]     HB_ms          uint16
        //   [5-6]     dt_us          uint16
        //   [7]       DeviceEnabledBits
        //   [8]       DeviceReadyBits
        //   [9]       HealthBits     (isReady b0, isChargerEnabled b1, isNotBatLowVoltage b2, isTrainingMode b3, isLaserModelMatch b4)
        //   [10]      PowerBits      (bit N = MCC_POWER value N — RELAY_GPS/VICOR_BUS/RELAY_LASER/VICOR_GIM/VICOR_TMS/SOL_HEL/SOL_BDA/RELAY_NTP)
        //   [11]      MCC_VOTES_BITS  
        //   [12-19]   NTP epoch ms   Int64
        //   [20]      Temp1 (Charger) int8
        //   [21]      Temp2 (Air)     int8
        //   [22-25]   TPH Temp        float
        //   [26-29]   TPH Pressure    float
        //   [30-33]   TPH Humidity    float
        //   [34-44]   Battery block   (MSG_BATTERY — 11 bytes)
        //   [45-65]   Laser block     (MSG_IPG — 21 bytes)
        //   [66-129]  TMC REG1        64-byte block (MSG_TMC)
        //   [130]     HB_NTP           uint8  x0.1s units — /10.0 = seconds (NTP ~10s interval)
        //   [131]     HB_HEL_ms        uint8  raw ms      — laser TCP poll ~20ms
        //   [132]     HB_BAT_ms        uint8  raw ms      — RS485 poll ~100ms
        //   [133]     HB_CRG_ms        uint8  raw ms      — I2C poll ~100ms, V1 only (0 on V2)
        //   [134]     HB_GNSS_ms       uint8  raw ms      — NovAtel UDP 1–12Hz
        //   [135-212] GNSS data        (MSG_GNSS — 78 bytes)
        //   [213-244] Charger data     (MSG_CMC — 32 bytes)
        //   [245-248] VERSION_WORD     uint32
        //   [249-252] MCU Temp         float
        //   [253]      TIME_BITS (session 32) — isPTP_En, ptp.isSynched, usingPTP, ntp.isSynched, ntpUsingFB, ntpHasFB
        //   [254]      HW_REV — 0x01=V1, 0x02=V2 (MCC unification session)
        //   [255]      LASER_MODEL — 0x00=UNKNOWN, 0x01=YLM_3K, 0x02=YLM_6K (v3.5.0)
        //   [256]      MCC_VOTES2_BITS — BAT_NOT_LOW, TRAINING_MODE, COMBAT, EMON_MISSING, EMON_UNEXPECTED, FIRE_INTERLOCKED
        //   [257]      DEVICE_WARN_BITS — per-device warn summary (same bit layout as DEVICE_ENABLED_BITS)
        //   [258]      MCC_TMC_STATUS_BITS
        //   [259]      MCC_HEL_STATUS_BITS
        //   [260]      MCC_BAT_STATUS_BITS
        //   [261]      MCC_CRG_STATUS_BITS
        //   [262]      MCC_GNSS_STATUS_BITS
        //   [263]      MCC_BDC_STATUS_BITS
        // =========================================================================
        private void ParseMSG01(byte[] msg, int ndx)
        {
            System_State = (SYSTEM_STATES)msg[ndx]; ndx++;
            BDC_Mode     = (BDC_MODES)    msg[ndx]; ndx++;

            // ICD v3.0.0 session 4: HB now uint16 ms (was uint32 µs); dt now uint16
            HB_ms = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(UInt16);
            dt_us = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(UInt16);

            DeviceEnabledBits = msg[ndx]; ndx++;
            DeviceReadyBits   = msg[ndx]; ndx++;
            HealthBits = msg[ndx]; ndx++;   // was StatusBits
            PowerBits = msg[ndx]; ndx++;   // was StatusBits2
            VOTE_BITS_MCC = msg[ndx]; ndx++;  // [11]  MCC_VOTES

            // ICD v3.0.0 session 4: NTP epoch ms only — no RTC field
            _ntpTime = BitConverter.ToInt64(msg, ndx); ndx += sizeof(Int64);

            // ICD v3.0.0 session 4: Temp1/Temp2 changed from float (4 bytes) to int8 (1 byte)
            TEMPERATURE_CHARGER = (double)(sbyte)msg[ndx]; ndx++;
            TEMPERATURE_AIR     = (double)(sbyte)msg[ndx]; ndx++;

            TEMPERATURE = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            PRESSURE    = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            HUMIDITY    = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            ndx = BatteryMsg.Parse(msg, ndx);
            ndx = IPGMsg.Parse(msg, ndx);

            // ICD v3.0.0 session 4: TMC 64-byte embedded block at payload[66-129]
            ndx = TMCMsg.Parse(msg, ndx);

            // HB counters — HB_RTC removed in session 4
            HB_NTP  = (double)msg[ndx] / 10.0; ndx++;
            HB_HEL_ms = (int)msg[ndx]; ndx++;  // raw ms
            HB_BAT_ms  = (int)msg[ndx]; ndx++;  // raw ms
            HB_CRG_ms  = (int)msg[ndx]; ndx++;  // raw ms
            HB_GNSS_ms = (int)msg[ndx]; ndx++;  // raw ms

            ndx = GNSSMsg.ParseMsg(msg, ndx);
            ndx = CMCMsg.Parse(msg, ndx);       // embedded entry point — Parse() not ParseMsg()

            FW_VERSION = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32);
            TEMP_MCU        = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            // [253] TIME_BITS (session 32) — consolidated time source status
            TimeBits = msg[ndx]; ndx++;
            HW_REV = msg[ndx]; ndx++;   // [254] HW_REV — 0x01=V1, 0x02=V2
            LaserModel = (LASER_MODEL)msg[ndx]; ndx++;  // [255] LASER_MODEL — v3.5.0

            // propagate to IPGMsg so PowerSetting_W and IsEMON decode correctly
            IPGMsg.LaserModel = LaserModel;

            VOTE_BITS_MCC2 = msg[ndx]; ndx++;  // [256] MCC_VOTES2
            DeviceWarnBits = msg[ndx]; ndx++;  // [257] DEVICE_WARN_BITS
            TMC_StatusBits = msg[ndx]; ndx++;  // [258] MCC_TMC_STATUS_BITS
            HEL_StatusBits = msg[ndx]; ndx++;  // [259] MCC_HEL_STATUS_BITS
            BAT_StatusBits = msg[ndx]; ndx++;  // [260] MCC_BAT_STATUS_BITS
            CRG_StatusBits = msg[ndx]; ndx++;  // [261] MCC_CRG_STATUS_BITS
            GNSS_StatusBits = msg[ndx]; ndx++;  // [262] MCC_GNSS_STATUS_BITS
            BDC_StatusBits = msg[ndx]; ndx++;  // [263] MCC_BDC_STATUS_BITS

            if (dt_us > dtmax)
            {
                dtmax = dt_us;
                Debug.WriteLine($"MSG_MCC: dt max = {dtmax}");
                if (isVerboseLogEnabled) Log?.Debug("MSG_MCC: dt max = {Dt}", dtmax);
            }
            if (HB_ms > HbMax) HbMax = HB_ms;
            DtAvg = (DtAvg == 0) ? dt_us : DtAvg + EWMA_ALPHA * (dt_us - DtAvg);
            HbAvg = (HbAvg == 0) ? HB_ms : HbAvg + EWMA_ALPHA * (HB_ms - HbAvg);
        }

        // =========================================================================
        // HealthBits / PowerBits accessors
        // =========================================================================
        // HealthBits accessors [byte 9] — isReady(0), isChargerEnabled(1), isNotBatLowVoltage(2)
        public bool isReady { get { return IsBitSet(HealthBits, 0); } }   // new — was missing from old StatusBits
        public bool isCharger_Enabled { get { return IsBitSet(HealthBits, 1); } }   // was bit 4
        public bool isNotBatLowVoltage { get { return (VOTE_BITS_MCC2 & (byte)MCC_VOTES2.BAT_NOT_LOW) != 0; } }
        public bool isHEL_TrainingMode { get { return (VOTE_BITS_MCC2 & (byte)MCC_VOTES2.TRAINING_MODE) != 0; } }
        // Solenoid/laser moved to PowerBits — compat aliases so existing call sites don't break
        public bool isSolenoid1_Enabled { get { return IsBitSet(PowerBits, (int)MCC_POWER.SOL_HEL); } }
        public bool isSolenoid2_Enabled { get { return IsBitSet(PowerBits, (int)MCC_POWER.SOL_BDA); } }
        public bool isLaserPowerBus_Enabled { get { return IsBitSet(PowerBits, (int)MCC_POWER.RELAY_LASER); } }
        // Retired entirely — not in any register byte
        // isUnSolicitedMode_Enabled removed (StatusBits bit 7 — retired session 35)

        // PowerBits accessors [byte 10] — bit N = MCC_POWER value N (all revisions)
        public bool pb_RelayGps { get { return IsBitSet(PowerBits, (int)MCC_POWER.RELAY_GPS); } }  // V1/V3 live | V2 always 0
        public bool pb_VicorBus { get { return IsBitSet(PowerBits, (int)MCC_POWER.VICOR_BUS); } }  // V1/V3·3kW live | V2/V3·6kW always 0
        public bool pb_RelayLaser { get { return IsBitSet(PowerBits, (int)MCC_POWER.RELAY_LASER); } }  // all revisions
        public bool pb_VicorGim { get { return IsBitSet(PowerBits, (int)MCC_POWER.VICOR_GIM); } }  // V2/V3·6kW live | V1/V3·3kW always 0
        public bool pb_VicorTms { get { return IsBitSet(PowerBits, (int)MCC_POWER.VICOR_TMS); } }  // V2/V3·6kW live | V1/V3·3kW always 0
        public bool pb_SolHel { get { return IsBitSet(PowerBits, (int)MCC_POWER.SOL_HEL); } }  // V1/V3·3kW live | V2/V3·6kW always 0
        public bool pb_SolBda { get { return IsBitSet(PowerBits, (int)MCC_POWER.SOL_BDA); } }  // V1/V3·3kW live | V2/V3·6kW always 0
        public bool pb_RelayNtp { get { return IsBitSet(PowerBits, (int)MCC_POWER.RELAY_NTP); } }  // V3 only | V1/V2 always 0

        // Backward-compat pb_* aliases — old names retained so existing call sites compile
        public bool pb_GpsRelay => pb_RelayGps;
        public bool pb_LaserRelay => pb_RelayLaser;
        public bool pb_GimVicor => pb_VicorGim;
        public bool pb_TmsVicor => pb_VicorTms;

        // isLaserModelMatch — HealthBits bit 4
        // true = laser connected + runtime model matches compile-time LASER_xK axis
        // false until laser connects; combined with isHEL_Ready for full diagnostic
        public bool isLaserModelMatch { get { return IsBitSet(HealthBits, 4); } }

        // TimeBits redirects — unchanged
        public bool ntpUsingFallback { get { return tb_ntpUsingFallback; } }
        public bool ntpHasFallback { get { return tb_ntpHasFallback; } }
        public bool usingPTP { get { return tb_usingPTP; } }

        // =========================================================================
        // VoteBits accessors
        // =========================================================================
        // VOTE_BITS_MCC [11] — gate-chain order b0→b7
        public bool isNotAbort_Vote_rb { get { return (VOTE_BITS_MCC & (byte)MCC_VOTES.NOT_ABORT) != 0; } }
        public bool isArmed_Vote_rb { get { return (VOTE_BITS_MCC & (byte)MCC_VOTES.ARMED) != 0; } }
        public bool isBDC_Vote_rb { get { return (VOTE_BITS_MCC & (byte)MCC_VOTES.BDC_VOTE) != 0; } }
        public bool isLaserTotalHW_Vote_rb { get { return (VOTE_BITS_MCC & (byte)MCC_VOTES.LASER_TOTAL_HW) != 0; } }
        public bool isSW_Vote_rb { get { return (VOTE_BITS_MCC & (byte)MCC_VOTES.SW_VOTE) != 0; } }
        public bool isTrigger_Vote_rb { get { return (VOTE_BITS_MCC & (byte)MCC_VOTES.TRIGGER) != 0; } }
        public bool isFireState_Vote_rb { get { return (VOTE_BITS_MCC & (byte)MCC_VOTES.FIRE_STATE) != 0; } }
        public bool isEMON { get { return (VOTE_BITS_MCC & (byte)MCC_VOTES.EMON) != 0; } }

        // VOTE_BITS_MCC2 [256] — detail / diagnostic
        public bool isBatNotLow { get { return (VOTE_BITS_MCC2 & (byte)MCC_VOTES2.BAT_NOT_LOW) != 0; } }
        public bool isTrainingMode { get { return (VOTE_BITS_MCC2 & (byte)MCC_VOTES2.TRAINING_MODE) != 0; } }
        public bool isCombat_Vote_rb { get { return (VOTE_BITS_MCC2 & (byte)MCC_VOTES2.COMBAT) != 0; } }
        public bool isEMON_Missing { get { return (VOTE_BITS_MCC2 & (byte)MCC_VOTES2.EMON_MISSING) != 0; } }
        public bool isEMON_Unexpected { get { return (VOTE_BITS_MCC2 & (byte)MCC_VOTES2.EMON_UNEXPECTED) != 0; } }
        public bool isFireInterlocked { get { return (VOTE_BITS_MCC2 & (byte)MCC_VOTES2.FIRE_INTERLOCKED) != 0; } }

        // =========================================================================
        // DeviceEnabled / DeviceReady accessors
        // ICD v3.0.0 session 4: bit 4 changed from RTCLOCK → RES
        // ICD v3.3.1 session 29: bit 4 = MCC_DEVICES.PTP (renamed from RTCLOCK — resolved)
        // isPTP accessors use (int)MCC_DEVICES.PTP
        // =========================================================================
        public bool isNTP_DeviceEnabled  { get { return IsBitSet(DeviceEnabledBits, (int)MCC_DEVICES.NTP);  } }
        public bool isTMC_DeviceEnabled  { get { return IsBitSet(DeviceEnabledBits, (int)MCC_DEVICES.TMC);  } }
        public bool isHEL_DeviceEnabled  { get { return IsBitSet(DeviceEnabledBits, (int)MCC_DEVICES.HEL);  } }
        public bool isBAT_DeviceEnabled  { get { return IsBitSet(DeviceEnabledBits, (int)MCC_DEVICES.BAT);  } }
        public bool isPTP_DeviceEnabled  { get { return IsBitSet(DeviceEnabledBits, (int)MCC_DEVICES.PTP); } }   // bit 4: MCC_DEVICES.PTP (was RTCLOCK)
        public bool isCRG_DeviceEnabled  { get { return IsBitSet(DeviceEnabledBits, (int)MCC_DEVICES.CRG);  } }
        public bool isGNSS_DeviceEnabled { get { return IsBitSet(DeviceEnabledBits, (int)MCC_DEVICES.GNSS); } }
        public bool isBDC_DeviceEnabled  { get { return IsBitSet(DeviceEnabledBits, (int)MCC_DEVICES.BDC);  } }

        public bool isNTP_DeviceReady    { get { return IsBitSet(DeviceReadyBits, (int)MCC_DEVICES.NTP);  } }
        public bool isTMC_DeviceReady    { get { return IsBitSet(DeviceReadyBits, (int)MCC_DEVICES.TMC);  } }
        public bool isHEL_DeviceReady    { get { return IsBitSet(DeviceReadyBits, (int)MCC_DEVICES.HEL);  } }
        public bool isBAT_DeviceReady    { get { return IsBitSet(DeviceReadyBits, (int)MCC_DEVICES.BAT);  } }
        public bool isPTP_DeviceReady    { get { return IsBitSet(DeviceReadyBits,  (int)MCC_DEVICES.PTP); } }   // bit 4: MCC_DEVICES.PTP (ptp.isSynched)
        public bool isCRG_DeviceReady    { get { return IsBitSet(DeviceReadyBits, (int)MCC_DEVICES.CRG);  } }
        public bool isGNSS_DeviceReady   { get { return IsBitSet(DeviceReadyBits, (int)MCC_DEVICES.GNSS); } }
        public bool isBDC_DeviceReady    { get { return IsBitSet(DeviceReadyBits, (int)MCC_DEVICES.BDC);  } }

        public bool isNTP_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)MCC_DEVICES.NTP); } }
        public bool isTMC_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)MCC_DEVICES.TMC); } }
        public bool isHEL_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)MCC_DEVICES.HEL); } }
        public bool isBAT_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)MCC_DEVICES.BAT); } }
        public bool isPTP_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)MCC_DEVICES.PTP); } }
        public bool isCRG_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)MCC_DEVICES.CRG); } }
        public bool isGNSS_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)MCC_DEVICES.GNSS); } }
        public bool isBDC_DeviceWarn { get { return IsBitSet(DeviceWarnBits, (int)MCC_DEVICES.BDC); } }

        // ADD — allows pre-connect ping to populate bits before REG1 arrives
        public void SetDeviceReady(MCC_DEVICES dev, bool ready)
        {
            if (ready)
                DeviceReadyBits |= (byte)(1 << (int)dev);
            else
                DeviceReadyBits &= (byte)~(1 << (int)dev);
        }

        // =========================================================================
        // HEL accessors (via IPGMsg)
        // =========================================================================
        public bool isHEL_EMON { get { return IPGMsg.IsEMON; } }       // model-aware — 3K=bit0, 6K=bit2
        public bool isHEL_NOTREADY { get { return IPGMsg.IsNotReady; } }   // model-aware — 3K=bit9, 6K=bit11
        public bool isHEL_EXT_EM_ENABLED { get { return IsBitSet(IPGMsg.StatusWord, 5); } }  // 3K only
        public bool isHEL_LowPowerMode { get { return IsBitSet(IPGMsg.StatusWord, 15); } }  // 3K only — reserved
        //public bool isHEL_Sensed { get { return IPGMsg.IsSensed; } }
        public int HEL_MaxPower_W { get { return IPGMsg.MaxPower_W; } }

        // =========================================================================
        // Helpers
        // =========================================================================
        bool IsBitSet(byte   b, int pos) { return (b & (1    << pos)) != 0; }
        bool IsBitSet(UInt32 b, int pos) { return (b & (1u   << pos)) != 0; }
        bool IsBitSet(UInt16 b, int pos) { return (b & (1    << pos)) != 0; }
    }

}
