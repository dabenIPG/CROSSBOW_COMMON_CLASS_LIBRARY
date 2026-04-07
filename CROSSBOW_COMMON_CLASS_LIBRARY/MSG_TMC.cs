using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace CROSSBOW
{
    // -----------------------------------------------------------------------
    // MSG_TMC — parses a framed A2 response from the TMC controller.
    //
    // Session 35 changes (unified client model):
    //   - Parse(frame): CMD_BYTE check now accepts both 0xA1 (unsolicited) and
    //     0xA4 (FRAME_KEEPALIVE poll response) as valid REG1 data frames.
    //   - isUnsolicitedEnabled: STATUS_BITS1 bit 7 retired in FW — property
    //     preserved for backward compat but marked Obsolete (always returns false).
    //
    // Session 30 changes (PTP integration):
    //   - TmcReg1.StatBits3 added at byte 61 (was RESERVED)
    //   - STATUS_BITS3 raw property + full accessor set added
    //   - isNTPSynched moved: STAT_BITS1 bit 5 → STAT_BITS3 bit 3
    //   - ntpUsingFallback added: STAT_BITS3 bit 4 (was RES in BITS1 bit 6)
    //   - ntpHasFallback added: STAT_BITS3 bit 5
    //   - isPTP_Enabled, isPTP_Synched, usingPTP added: STAT_BITS3 bits 0-2
    //   - epochTime added — preferred alias for ntpTime (field reflects active source post-FW update)
    //   - TIME_SOURCE enum (from MSG_MCC.cs/defines) — activeTimeSource + activeTimeSourceLabel
    //
    // Wire format (521 bytes total):
    //   [0]      MAGIC_HI  = 0xCB
    //   [1]      MAGIC_LO  = 0x49  (internal A2)
    //   [2]      SEQ_NUM   uint8
    //   [3]      CMD_BYTE  uint8   0xA1 = unsolicited REG1; 0xA4 = keepalive poll REG1; other = ACK only
    //   [4]      STATUS    uint8   0x00 = OK
    //   [5–6]    PAYLOAD_LEN uint16 LE  always 512
    //   [7–518]  PAYLOAD   512 bytes   (TMC REG1 at [7–70] when CMD_BYTE is 0xA1 or 0xA4 with data; rest 0x00)
    //   [519–520] CRC-16/CCITT uint16 BE
    //
    // REG1 layout (64 bytes at payload offset 0, i.e. frame bytes 7–70):
    //   see ICD_v3.0.0.md — TMC Register 1 section
    // -----------------------------------------------------------------------

    public class MSG_TMC
    {
        // -------------------------------------------------------------------
        // Frame constants
        // -------------------------------------------------------------------
        public const byte   MAGIC_HI           = 0xCB;
        public const byte   MAGIC_LO           = 0x49;   // A2 internal
        public const int    FRAME_RESPONSE_LEN = 521;
        public const int    PAYLOAD_OFFSET     = 7;      // byte index of payload start
        public const byte   STATUS_OK          = 0x00;

        // -------------------------------------------------------------------
        // TmcReg1 — 64-byte packed struct, little-endian
        // Offsets verified against ICD v3.0.0 (TMC REG1 section)
        // -------------------------------------------------------------------
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TmcReg1
        {
            public byte   CmdByte;        // [0]    0xA1 in unsolicited frames; echoes request CMD_BYTE in solicited responses (0xA4 for keepalive poll with data)
            public byte   SystemState;    // [1]    SYSTEM_STATES enum
            public byte   SystemMode;     // [2]    BDC_MODES enum
            public ushort HB_ms;          // [3–4]  ms between sends
            public ushort dt_us;          // [5–6]  µs in processing loop
            public byte   StatBits1;      // [7]
            public byte   StatBits2;      // [8]
            public ulong  NtpEpochMs;     // [9–16] ms since Unix epoch
            public ushort PumpSpeed;      // [17–18] DAC counts [0–4095]
            public ushort Lcm1Speed;      // [19–20]
            public ushort Lcm1CurrentRb;  // [21–22] ADC counts (IIR filtered)
            public ushort Lcm2Speed;      // [23–24]
            public ushort Lcm2CurrentRb;  // [25–26]
            public byte   F1_x10;         // [27]   flow 1 ×10 LPM
            public byte   F2_x10;         // [28]   flow 2 ×10 LPM
            public sbyte  Tt;             // [29]   target temp setpoint °C [10–40]
            public sbyte  Ta1;            // [30]   air temp 1 °C
            public sbyte  Tf1;            // [31]   fluid temp 1 °C
            public sbyte  Tf2;            // [32]   fluid temp 2 °C
            public sbyte  Tc1;            // [33]   compressor 1 °C
            public sbyte  Tc2;            // [34]   compressor 2 °C
            public sbyte  To1;            // [35]   output ch1 °C
            public sbyte  To2;            // [36]   output ch2 °C
            public sbyte  Tv1;            // [37]   vicor 1 °C
            public sbyte  Tv2;            // [38]   vicor 2 °C
            public sbyte  Tv3;            // [39]   vicor heater °C
            public sbyte  Tv4;            // [40]   vicor pump °C
            public float  TphTemp;        // [41–44] ambient °C (BME280)
            public float  TphPressure;    // [45–48] Pa
            public float  TphHumidity;    // [49–52] %
            public uint   VersionWord;    // [53–56] VERSION_PACK(maj,min,pat)
            public float  McuTemp;        // [57–60] STM32F7 die temp °C
            public byte   StatBits3;      // [61]    PTP + NTP time status (session 30, was RESERVED)
            public byte   HW_REV;         // [62]    self-detecting hardware revision for MSG_TMC.cs
            // [63] RESERVED — 1 byte padding to 64-byte block
        }

        // -------------------------------------------------------------------
        // Parsed properties
        // -------------------------------------------------------------------
        public SYSTEM_STATES System_State        { get; private set; } = SYSTEM_STATES.OFF;
        public BDC_MODES     BDC_Mode            { get; private set; } = BDC_MODES.OFF;
        public ushort        HB_TX_ms            { get; private set; } = 0;
        public ushort        dt_us               { get; private set; } = 0;
        public byte          STATUS_BITS1        { get; private set; } = 0;
        public byte          STATUS_BITS2        { get; private set; } = 0;
        public byte          STATUS_BITS3        { get; private set; } = 0;   // byte 61 — PTP+NTP time status (session 30)
        public byte HW_REV { get; private set; } = 0;                         // byte 62 — HW VS

        public DateTime      lastMsgRx           { get; private set; } = DateTime.UtcNow;

        // Version — semver layout: bits[31:24]=major  [23:12]=minor  [11:0]=patch
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

        // STAT BITS1
        // STAT BITS1 [byte 7] — session 30: bits 5/6 vacated, NTP/PTP state moved to BITS3
        public bool isReady               { get { return IsBitSet(STATUS_BITS1, 0); } }
        public bool isPumpEnabled         { get { return IsBitSet(STATUS_BITS1, 1); } }
        public bool isHeaterEnabled       { get { return IsBitSet(STATUS_BITS1, 2); } }
        public bool isInputFan1Enabled    { get { return IsBitSet(STATUS_BITS1, 3); } }
        public bool isInputFan2Enabled    { get { return IsBitSet(STATUS_BITS1, 4); } }
        // bit 5 = RES (was isNTPSynched — moved to STATUS_BITS3 bit 3)
        // bit 6 = RES (was ntpUsingFallback/isRTCInit — moved to STATUS_BITS3 bit 4)
        // Session 35: STATUS_BITS1 bit 7 retired in firmware (was isUnSolicitedEnabled).
        // A2 subscription state is now per-client in FrameClient.wantsUnsolicited and
        // is not exposed in REG1. This property is preserved for build compatibility
        // but will always return false against session 35+ firmware.
        [Obsolete("STATUS_BITS1 bit 7 retired session 35. Always returns false against current firmware.")]
        public bool isUnsolicitedEnabled  { get { return IsBitSet(STATUS_BITS1, 7); } }

        // STAT BITS3 [byte 61] — session 30 — PTP + NTP time status
        public bool isPTP_Enabled         { get { return IsBitSet(STATUS_BITS3, 0); } }   // PTP client enabled
        public bool isPTP_Synched         { get { return IsBitSet(STATUS_BITS3, 1); } }   // ptp.isSynched
        public bool usingPTP              { get { return IsBitSet(STATUS_BITS3, 2); } }   // PTP is active time source
        public bool isNTPSynched          { get { return IsBitSet(STATUS_BITS3, 3); } }   // moved from BITS1 bit 5
        public bool ntpUsingFallback      { get { return IsBitSet(STATUS_BITS3, 4); } }   // moved from BITS1 bit 6
        public bool ntpHasFallback        { get { return IsBitSet(STATUS_BITS3, 5); } }   // new — fallback server configured

        // STAT BITS2
        public bool isVicor1Enabled { get { return IsBitSet(STATUS_BITS2, 0); } }
        public bool isLCM1Enabled   { get { return IsBitSet(STATUS_BITS2, 1); } }
        public bool isLCM1Error     { get { return IsBitSet(STATUS_BITS2, 2); } }
        public bool isFlow1Error    { get { return IsBitSet(STATUS_BITS2, 3); } }
        public bool isVicor2Enabled { get { return IsBitSet(STATUS_BITS2, 4); } }
        public bool isLCM2Enabled   { get { return IsBitSet(STATUS_BITS2, 5); } }
        public bool isLCM2Error     { get { return IsBitSet(STATUS_BITS2, 6); } }
        public bool isFlow2Error    { get { return IsBitSet(STATUS_BITS2, 7); } }

        // Epoch time — post FW session 30 update, field reflects active source (PTP or NTP).
        // Use epochTime (preferred) or ntpTime (backward-compat alias).
        private long _ntpTime { get; set; } = 0;
        public DateTime epochTime { get { return DateTimeOffset.FromUnixTimeMilliseconds(_ntpTime).UtcDateTime; } }
        public DateTime ntpTime   { get { return epochTime; } }   // backward-compat alias

        /// <summary>Active time source — derived from STATUS_BITS3.</summary>
        public TIME_SOURCE activeTimeSource
        {
            get
            {
                if (isPTP_Synched && usingPTP) return TIME_SOURCE.PTP;
                if (isNTPSynched)              return TIME_SOURCE.NTP;
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

        // Actuator readbacks
        public ushort PumpSpeed       { get; private set; } = 0;
        public ushort LCM1_SPEED      { get; private set; } = 0;
        public ushort LCM1_CURRENT_RB { get; private set; } = 0;
        public ushort LCM2_SPEED      { get; private set; } = 0;
        public ushort LCM2_CURRENT_RB { get; private set; } = 0;

        // Derived current — ADC counts → amps
        public double LCM1_CURRENT { get { return (double)LCM1_CURRENT_RB / 4095.0 * 2.5 * 2.0 * 10.0; } }
        public double LCM2_CURRENT { get { return (double)LCM2_CURRENT_RB / 4095.0 * 2.5 * 2.0 * 10.0; } }

        // Flow (×10 encoding decoded to LPM)
        public float FLOW1 { get; private set; } = 0;
        public float FLOW2 { get; private set; } = 0;

        // Temperatures — int8 on the wire, exposed as sbyte
        public sbyte TEMP_TARGET { get; private set; } = 0;
        public sbyte TEMP_AIR1   { get; private set; } = 0;
        public sbyte TEMP_TF1    { get; private set; } = 0;
        public sbyte TEMP_TF2    { get; private set; } = 0;
        public sbyte TEMP_C1     { get; private set; } = 0;
        public sbyte TEMP_C2     { get; private set; } = 0;
        public sbyte TEMP_O1     { get; private set; } = 0;
        public sbyte TEMP_O2     { get; private set; } = 0;
        public sbyte TEMP_V1     { get; private set; } = 0;
        public sbyte TEMP_V2     { get; private set; } = 0;
        public sbyte TEMP_V3     { get; private set; } = 0;
        public sbyte TEMP_V4     { get; private set; } = 0;
        public float TEMP_MCU    { get; private set; } = 0;

        // TPH (BME280)
        public float TEMPERATURE { get; private set; } = 0;
        public float PRESSURE    { get; private set; } = 0;
        public float HUMIDITY    { get; private set; } = 0;

        // Derived aggregates
        public double AVG_FLOW
        {
            get
            {
                var flows = new double[] { FLOW1, FLOW2 }.Where(n => n != 0).ToList();
                return flows.Any() ? flows.Average() : 0.0;
            }
        }
        public double AVG_TEMP
        {
            get
            {
                var temps = new double[] { TEMP_TF1, TEMP_TF2 }.Where(n => n != 0).ToList();
                return temps.Any() ? temps.Average() : 0.0;
            }
        }
        public double AVG_LCM
        {
            get
            {
                double val = 0;
                int count = 0;
                if (isLCM1Enabled) { val += LCM1_SPEED / 4095.0 * 100.0; count++; }
                if (isLCM2Enabled) { val += LCM2_SPEED / 4095.0 * 100.0; count++; }
                return count > 0 ? val / count : 0.0;
            }
        }

        // Last raw frame STATUS byte received — useful for diagnostics
        public byte LastFrameStatus { get; private set; } = 0xFF;

        // -------------------------------------------------------------------
        // Parse — validate and parse a 521-byte A2 framed response.
        // Returns true on success; false if frame is malformed, bad CRC,
        // bad magic, or non-OK status.
        // -------------------------------------------------------------------
        public bool Parse(byte[] frame)
        {
            if (frame == null || frame.Length != FRAME_RESPONSE_LEN)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"MSG_TMC.Parse: bad length {frame?.Length} (expected {FRAME_RESPONSE_LEN})");
                return false;
            }

            if (frame[0] != MAGIC_HI || frame[1] != MAGIC_LO)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"MSG_TMC.Parse: bad magic 0x{frame[0]:X2} 0x{frame[1]:X2}");
                return false;
            }

            ushort computed = CrcHelper.Crc16(frame, FRAME_RESPONSE_LEN - 2);
            ushort received = (ushort)((frame[519] << 8) | frame[520]);
            if (computed != received)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"MSG_TMC.Parse: CRC mismatch computed=0x{computed:X4} received=0x{received:X4}");
                return false;
            }

            LastFrameStatus = frame[4];
            if (frame[4] != STATUS_OK)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"MSG_TMC.Parse: non-OK status 0x{frame[4]:X2}");
                return false;
            }

            // Parse REG1 payload for unsolicited frames (CMD_BYTE = 0xA1) and
            // keepalive poll responses with data (CMD_BYTE = 0xA4, payload {0x01}).
            // All other CMD_BYTEs (0xA0 subscribe ACK, 0xA4 bare ACK, etc.) have
            // zero-filled payloads — update liveness only, no REG1 parse.
            byte cmdByte = frame[3];
            if (cmdByte != (byte)ICD.RES_A1 && cmdByte != (byte)ICD.FRAME_KEEPALIVE)
            {
                lastMsgRx = DateTime.UtcNow;
                return true;
            }

            TmcReg1 reg = MemoryMarshal.Read<TmcReg1>(frame.AsSpan(PAYLOAD_OFFSET));
            ParseBlock(reg);

            lastMsgRx = DateTime.UtcNow;
            return true;
        }

        // -------------------------------------------------------------------
        // Parse(byte[] msg, int ndx) → int
        //
        // Embedded-block entry point called from MSG_MCC.ParseMSG01().
        // TMC REG1 64-byte block sits at MCC REG1 bytes 66–129.
        // Reads exactly TMC_EMBEDDED_LEN (64) bytes starting at ndx,
        // populates the same properties as the framed path, and returns
        // ndx + TMC_EMBEDDED_LEN so the caller can continue parsing.
        //
        // No magic or CRC check — framing already validated by MSG_MCC.
        // -------------------------------------------------------------------
        public const int TMC_EMBEDDED_LEN = 64;

        public int Parse(byte[] msg, int ndx)
        {
            if (msg == null || ndx + TMC_EMBEDDED_LEN > msg.Length)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"MSG_TMC.Parse(embedded): buffer too short at ndx={ndx}");
                return ndx + TMC_EMBEDDED_LEN;
            }

            TmcReg1 reg = MemoryMarshal.Read<TmcReg1>(msg.AsSpan(ndx));
            ParseBlock(reg);

            lastMsgRx = DateTime.UtcNow;
            return ndx + TMC_EMBEDDED_LEN;
        }

        // -------------------------------------------------------------------
        // ParseBlock — shared field extraction from a TmcReg1 struct.
        // Called by both Parse(frame) and Parse(msg, ndx).
        // -------------------------------------------------------------------
        public uint dtmax = 0;

        private void ParseBlock(TmcReg1 reg)
        {
            System_State      = (SYSTEM_STATES)reg.SystemState;
            BDC_Mode          = (BDC_MODES)reg.SystemMode;
            HB_TX_ms          = reg.HB_ms;
            dt_us             = reg.dt_us;
            STATUS_BITS1      = reg.StatBits1;
            STATUS_BITS2      = reg.StatBits2;
            STATUS_BITS3      = reg.StatBits3;
            _ntpTime          = (long)reg.NtpEpochMs;

            PumpSpeed         = reg.PumpSpeed;
            LCM1_SPEED        = reg.Lcm1Speed;
            LCM1_CURRENT_RB   = reg.Lcm1CurrentRb;
            LCM2_SPEED        = reg.Lcm2Speed;
            LCM2_CURRENT_RB   = reg.Lcm2CurrentRb;

            FLOW1             = reg.F1_x10 / 10.0f;
            FLOW2             = reg.F2_x10 / 10.0f;

            TEMP_TARGET       = reg.Tt;
            TEMP_AIR1         = reg.Ta1;
            TEMP_TF1          = reg.Tf1;
            TEMP_TF2          = reg.Tf2;
            TEMP_C1           = reg.Tc1;
            TEMP_C2           = reg.Tc2;
            TEMP_O1           = reg.To1;
            TEMP_O2           = reg.To2;
            TEMP_V1           = reg.Tv1;
            TEMP_V2           = reg.Tv2;
            TEMP_V3           = reg.Tv3;
            TEMP_V4           = reg.Tv4;

            TEMPERATURE       = reg.TphTemp;
            PRESSURE          = reg.TphPressure;
            HUMIDITY          = reg.TphHumidity;

            FW_VERSION        = reg.VersionWord;
            TEMP_MCU          = reg.McuTemp;
            HW_REV            = reg.HW_REV;
            if (dt_us > 1000)
            {
                dtmax = dt_us;
                Debug.WriteLine($"MSG_TMC: dt us = {dt_us}");
                //if (isVerboseLogEnabled) Log?.Debug("MSG_MCC: dt max = {Dt}", dtmax);
            }

        }

        private static bool IsBitSet(byte b, int pos) => (b & (1 << pos)) != 0;
    }
}
