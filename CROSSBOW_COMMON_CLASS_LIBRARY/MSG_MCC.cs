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

        // Backward-compat aliases — existing UI bindings continue to work
        public UInt32 HB_TX_us { get { return (UInt32)HB_ms * 1000u; } }
        public double HB_TX_ms { get { return (double)HB_ms; } }
        public UInt32 dt       { get { return (UInt32)dt_us; } }

        // ── Version ───────────────────────────────────────────────────────────
        public UInt32 SW_VERSION_WORD { get; private set; } = 0;
        public string SW_VERSION_STRING
        {
            get
            {
                // ICD v3.0.0 session 4 semver encoding:
                //   bits[31:24] = major  (8 bits,  0-255)
                //   bits[23:12] = minor  (12 bits, 0-4095)
                //   bits[11:0]  = patch  (12 bits, 0-4095)
                // e.g. VERSION_PACK(3,0,1) = 0x03000001  ->  "3.0.1"
                UInt32 major = (SW_VERSION_WORD >> 24) & 0xFF;
                UInt32 minor = (SW_VERSION_WORD >> 12) & 0xFFF;
                UInt32 patch =  SW_VERSION_WORD        & 0xFFF;
                return $"{major}.{minor}.{patch}";
            }
        }

        // ── Status / device bits ──────────────────────────────────────────────
        public byte DeviceEnabledBits { get; private set; } = 0;
        public byte DeviceReadyBits   { get; private set; } = 0;
        public byte StatusBits        { get; private set; } = 0;
        public byte StatusBits2       { get; private set; } = 0;

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


        // ── Vote bits with change logging ─────────────────────────────────────
        private byte _voteBits { get; set; } = 0;
        public  byte LastVoteBits { get; private set; } = 0;
        public  byte VoteBits
        {
            get { return _voteBits; }
            set
            {
                if (isVerboseLogEnabled && _voteBits != value)
                {
                    Log?.Information($"MCC VOTE CHANGED {Convert.ToString(_voteBits, 2).PadLeft(8, '0')} -> {Convert.ToString(value, 2).PadLeft(8, '0')}");
                }
                LastVoteBits = value;
                _voteBits    = value;
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
        public double HB_HEL  { get; private set; } = 0;
        public double HB_BAT  { get; private set; } = 0;
        public double HB_CRG  { get; private set; } = 0;
        public double HB_GNSS { get; private set; } = 0;

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
            if (cmd == ICD.RES_A1 || cmd == ICD.FRAME_KEEPALIVE)
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

            if (cmd == ICD.RES_A1)
                ParseMSG01(msg, ndx);
        }

        public uint dtmax = 0;

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
        //   [9]       StatusBits
        //   [10]      StatusBits2
        //   [11]      VoteBits
        //   [12-19]   NTP epoch ms   Int64
        //   [20]      Temp1 (Charger) int8
        //   [21]      Temp2 (Air)     int8
        //   [22-25]   TPH Temp        float
        //   [26-29]   TPH Pressure    float
        //   [30-33]   TPH Humidity    float
        //   [34-44]   Battery block   (MSG_BATTERY — 11 bytes)
        //   [45-65]   Laser block     (MSG_IPG — 21 bytes)
        //   [66-129]  TMC REG1        64-byte block (MSG_TMC)
        //   [130]     HB_NTP           uint8 (/10 = seconds)
        //   [131]     HB_HEL           uint8
        //   [132]     HB_BAT           uint8
        //   [133]     HB_CRG           uint8
        //   [134]     HB_GNSS          uint8
        //   [135-212] GNSS data        (MSG_GNSS — 78 bytes)
        //   [213-244] Charger data     (MSG_CMC — 32 bytes)
        //   [245-248] VERSION_WORD     uint32
        //   [249-252] MCU Temp         float
        //   [253]      TIME_BITS (session 32) — isPTP_En, ptp.isSynched, usingPTP, ntp.isSynched, ntpUsingFB, ntpHasFB
        //   [254-255]  RESERVED
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
            StatusBits        = msg[ndx]; ndx++;
            StatusBits2       = msg[ndx]; ndx++;
            VoteBits          = msg[ndx]; ndx++;

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
            HB_HEL  = (double)msg[ndx] / 10.0; ndx++;
            HB_BAT  = (double)msg[ndx] / 10.0; ndx++;
            HB_CRG  = (double)msg[ndx] / 10.0; ndx++;
            HB_GNSS = (double)msg[ndx] / 10.0; ndx++;

            ndx = GNSSMsg.ParseMsg(msg, ndx);
            ndx = CMCMsg.Parse(msg, ndx);       // embedded entry point — Parse() not ParseMsg()

            SW_VERSION_WORD = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32);
            TEMP_MCU        = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            // [253] TIME_BITS (session 32) — consolidated time source status
            TimeBits = msg[ndx]; ndx++;

            if (dt_us > dtmax)
            {
                dtmax = dt_us;
                Debug.WriteLine($"MSG_MCC: dt max = {dtmax}");
                if (isVerboseLogEnabled) Log?.Debug("MSG_MCC: dt max = {Dt}", dtmax);
            }
        }

        // =========================================================================
        // StatusBits accessors
        // =========================================================================
        public bool isSolenoid1_Enabled       { get { return IsBitSet(StatusBits, 1); } }
        public bool isSolenoid2_Enabled       { get { return IsBitSet(StatusBits, 2); } }
        public bool isLaserPowerBus_Enabled   { get { return IsBitSet(StatusBits, 3); } }
        public bool isCharger_Enabled         { get { return IsBitSet(StatusBits, 4); } }
        public bool isNotBatLowVoltage        { get { return IsBitSet(StatusBits, 5); } }
        public bool isUnSolicitedMode_Enabled { get { return IsBitSet(StatusBits, 7); } }

        // StatusBits2 [byte 10] — session 32: bits 0-2 now RES (moved to TimeBits byte 253)
        // Named properties below redirect to TimeBits for backward compatibility.
        public bool ntpUsingFallback  { get { return tb_ntpUsingFallback; } }   // → TimeBits bit 4
        public bool ntpHasFallback    { get { return tb_ntpHasFallback;   } }   // → TimeBits bit 5
        public bool usingPTP          { get { return tb_usingPTP;         } }   // → TimeBits bit 2
        public bool isVicor_Enabled   { get { return IsBitSet(StatusBits2, 3); } }
        public bool isRelay1_Enabled  { get { return IsBitSet(StatusBits2, 4); } }
        public bool isRelay2_Enabled  { get { return IsBitSet(StatusBits2, 5); } }
        public bool isRelay3_Enabled  { get { return IsBitSet(StatusBits2, 6); } }   // added session 4
        public bool isRelay4_Enabled  { get { return IsBitSet(StatusBits2, 7); } }   // added session 4

        // =========================================================================
        // VoteBits accessors
        // =========================================================================
        public bool isLaserTotalHW_Vote_rb       { get { return IsBitSet(VoteBits, 0); } }
        public bool isNotAbort_Vote_rb           { get { return IsBitSet(VoteBits, 1); } }
        public bool isArmed_Vote_rb              { get { return IsBitSet(VoteBits, 2); } }
        public bool isBDA_Vote_rb                { get { return IsBitSet(VoteBits, 3); } }
        public bool isEMON                       { get { return IsBitSet(VoteBits, 4); } }
        public bool isLaserFireRequested_Vote_rb { get { return IsBitSet(VoteBits, 5); } }
        public bool isLaserTotal_Vote_rb         { get { return IsBitSet(VoteBits, 6); } }
        public bool isCombat_Vote_rb             { get { return IsBitSet(VoteBits, 7); } }

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

        // =========================================================================
        // HEL accessors (via IPGMsg)
        // =========================================================================
        public bool isHEL_EMON           { get { return IsBitSet(IPGMsg.StatusWord,  0); } }
        public bool isHEL_EXT_EM_ENABLED { get { return IsBitSet(IPGMsg.StatusWord,  5); } }
        public bool isHEL_NOTREADY       { get { return IsBitSet(IPGMsg.StatusWord,  9); } }
        public bool isHEL_LowPowerMode   { get { return IsBitSet(IPGMsg.StatusWord, 15); } }

        // =========================================================================
        // Helpers
        // =========================================================================
        bool IsBitSet(byte   b, int pos) { return (b & (1    << pos)) != 0; }
        bool IsBitSet(UInt32 b, int pos) { return (b & (1u   << pos)) != 0; }
        bool IsBitSet(UInt16 b, int pos) { return (b & (1    << pos)) != 0; }
    }

}
