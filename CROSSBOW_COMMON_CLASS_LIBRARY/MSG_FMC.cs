// MSG_FMC.cs  —  ICD v4.0.0 FMC REG1 parser
//
// FMC REG1 payload layout (ICD v4.0.0, 59 bytes defined):
//   [0]      CMD BYTE         uint8   = 0x00 (legacy 0xA1 retired — FW-C10)
//   [1]      System State     uint8
//   [2]      System Mode      uint8
//   [3–4]    HB_ms            uint16 LE   ms between firmware sends
//   [5–6]    dt_us            uint16 LE   µs in firmware processing loop
//   [7]      FMC HEALTH_BITS  uint8       bit0:isReady  bit1-7:RES
//   [8–11]   Stage Pos        uint32 LE
//   [12–15]  Stage Err        uint32 LE
//   [16–19]  Stage Status     uint32 LE
//   [20–23]  FSM Pos X        int32 LE    ADC readback counts
//   [24–27]  FSM Pos Y        int32 LE    ADC readback counts
//   [28–35]  epoch ms         int64 LE    PTP when synched, NTP otherwise
//   [36–39]  VERSION WORD     uint32 LE   semver: [major:8][minor:12][patch:12]
//   [40–43]  MCU Temp         float LE    °C
//   [44]     TIME_BITS        uint8       bit0:isPTP_Enabled bit1:ptp.isSynched bit2:usingPTP
//                                         bit3:ntp.isSynched bit4:ntpUsingFallback bit5:ntpHasFallback
//   [45]     HW_REV           uint8       0x01=V1 (SAMD21)  0x02=V2 (STM32F7)
//   [46]     FMC POWER_BITS   uint8       bit0:isFSM_Powered  bit1:isStageEnabled  bit2-7:RES
//   [47–50]  TPH Temp         float LE    °C   (V2 only; V1 = 0.0)
//   [51–54]  TPH Pressure     float LE    Pa   (V2 only; V1 = 0.0)
//   [55–58]  TPH Humidity     float LE    %    (V2 only; V1 = 0.0)
//   [59–63]  RESERVED         0x00
//
// ICD v4.0.0 changes vs v3.5.2:
//   Bytes [47–58] BME280 TPH — V2 only (FMC-TPH); V1 reads 0.0f
//   REG1 CMD_BYTE [0] 0xA1 → 0x00 (FW-C10) — Parse() accepts both for legacy
//
// ICD v3.5.2 changes vs v3.3.5:
//   Byte [7]  FSM STAT BITS → FMC HEALTH_BITS — isReady only (isFSM_Powered/isStageEnabled moved to [46])
//   Byte [45] RESERVED → HW_REV (self-detecting hardware revision for IsV1/IsV2)
//   Byte [46] RESERVED → FMC POWER_BITS (isFSM_Powered bit0, isStageEnabled bit1)

using System;
using System.Diagnostics;

namespace CROSSBOW
{
    public class MSG_FMC
    {
        // -------------------------------------------------------------------
        // Frame constants — must match frame.hpp / fmc.cpp
        // -------------------------------------------------------------------
        public const int  FRAME_RESPONSE_LEN = 521;
        private const int PAYLOAD_OFFSET     = 7;
        private const int PAYLOAD_LEN        = 512;
        private const byte MAGIC_HI          = 0xCB;
        private const byte MAGIC_LO          = 0x49;   // A2 internal

        // -------------------------------------------------------------------
        // Properties
        // -------------------------------------------------------------------
        public SYSTEM_STATES System_State { get; private set; } = SYSTEM_STATES.OFF;
        public BDC_MODES     BDC_Mode     { get; private set; } = BDC_MODES.OFF;

        // ICD v3.0.0 session 4 — uint16 ms (was uint32 µs)
        public UInt16 HB_ms { get; private set; } = 0;
        public UInt16 dt_us { get; private set; } = 0;

        // FMC HEALTH_BITS — byte [7] — ICD v3.5.2
        // Renamed from FSM STAT BITS. isReady only — FSM/stage moved to PowerBits [46].
        public byte HealthBits { get; private set; } = 0;
        public byte StatusBits1 { get { return HealthBits; } }  // backward-compat alias

        public bool isReady { get { return IsBitSet(HealthBits, 0); } }
        // bits 1–7: RES

        // HW_REV — byte [45] — ICD v3.5.2
        public byte HW_REV { get; private set; } = 0;
        public bool IsV1 { get { return HW_REV == 0x01; } }
        public bool IsV2 { get { return HW_REV == 0x02; } }
        public string HW_REV_Label { get { return IsV1 ? "V1 — SAMD21" : IsV2 ? "V2 — STM32F7" : $"UNKNOWN (0x{HW_REV:X2})"; } }

        // FMC POWER_BITS — byte [46] — ICD v3.5.2
        // isFSM_Powered and isStageEnabled moved here from STATUS_BITS1.
        public byte PowerBits { get; private set; } = 0;
        public bool isFSM_Power_Enabled { get { return IsBitSet(PowerBits, 0); } }
        public bool isStageEnabled { get { return IsBitSet(PowerBits, 1); } }

        public UInt32 StagePosition { get;  set; } = 0;
        public UInt32 StageError    { get; private set; } = 0;
        public UInt32 StageStatus   { get; private set; } = 0;

        public Int32 FSM_PosX { get; private set; } = 0;   // ADC readback counts
        public Int32 FSM_PosY { get; private set; } = 0;

        // NTP / PTP epoch (session 33: routes through GetCurrentTime — PTP when synched)
        private Int64   _ntpTime  = 0;
        public DateTime epochTime { get { return DateTimeOffset.FromUnixTimeMilliseconds(_ntpTime).UtcDateTime; } }
        public DateTime ntpTime   { get { return epochTime; } }   // backward-compat alias

        // TIME_BITS — byte 44 (session 33)
        // Identical layout to MCC (byte 253), BDC (byte 391), TMC (STATUS_BITS3 byte 61)
        public byte TimeBits { get; private set; } = 0;

        public bool tb_isPTP_Enabled    { get { return IsBitSet(TimeBits, 0); } }
        public bool tb_isPTP_Synched    { get { return IsBitSet(TimeBits, 1); } }
        public bool tb_usingPTP         { get { return IsBitSet(TimeBits, 2); } }
        public bool tb_isNTP_Synched    { get { return IsBitSet(TimeBits, 3); } }
        public bool tb_ntpUsingFallback { get { return IsBitSet(TimeBits, 4); } }
        public bool tb_ntpHasFallback   { get { return IsBitSet(TimeBits, 5); } }

        // Active time source — derived from TIME_BITS
        public enum TIME_SOURCE { None, PTP, NTP }
        public TIME_SOURCE activeTimeSource
        {
            get
            {
                if (tb_isPTP_Enabled && tb_isPTP_Synched && tb_usingPTP) return TIME_SOURCE.PTP;
                if (tb_isNTP_Synched)                                     return TIME_SOURCE.NTP;
                return TIME_SOURCE.None;
            }
        }
        public string activeTimeSourceLabel
        {
            get
            {
                switch (activeTimeSource)
                {
                    case TIME_SOURCE.PTP: return "PTP";
                    case TIME_SOURCE.NTP: return tb_ntpUsingFallback ? "NTP (fallback)" : "NTP";
                    default:             return "NONE";
                }
            }
        }

        // VERSION WORD — semver: [major:8][minor:12][patch:12]
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
        public bool IsV4 => FW_MAJOR >= 4;                   // true = ICD v4.0.0 command space

        // MCU Temperature
        // MCU Temperature
        public float TEMP_MCU { get; private set; } = 0;

        // BME280 TPH — bytes [47–58] — ICD v4.0.0 (FMC-TPH)
        // V2 only; V1 returns 0.0f (firmware does not write these bytes on V1).
        // Use IsV2 to gate display.
        public float TPH_Temp { get; private set; } = 0;   // °C
        public float TPH_Pressure { get; private set; } = 0;   // Pa
        public float TPH_Humidity { get; private set; } = 0;   // %
        // REG2 / BDC REG1 calibration fields — set externally
        public UInt32 StageHome  { get;  set; } = 0;
        public Int16  FSM_X0     { get;  set; } = 0;
        public Int16  FSM_Y0     { get;  set; } = 0;
        public double FSM_iFOV_X { get;  set; } = 0;
        public double FSM_iFOV_Y { get;  set; } = 0;
        public sbyte  FSM_SIGN_X { get;  set; } = 0;
        public sbyte  FSM_SIGN_Y { get;  set; } = 0;

        // Receive heartbeat
        public DateTime lastMsgRx { get; set; } = DateTime.UtcNow;
        public double   HB_RX_ms  { get; set; } = 0;

        public Int32 FSM_PosX_c { get; set; } = 0;
        public Int32 FSM_PosY_c { get; set; } = 0;

        // -------------------------------------------------------------------
        // Parse — entry point from fmc.cs BackgroundUDPRead
        // Validates 521-byte framed response then calls ParseMSG01.
        // -------------------------------------------------------------------
        public void Parse(byte[] frame)
        {
            if (frame == null || frame.Length != FRAME_RESPONSE_LEN)
            {
                Debug.WriteLine($"MSG_FMC.Parse: unexpected frame length {frame?.Length}");
                return;
            }

            if (frame[0] != MAGIC_HI || frame[1] != MAGIC_LO)
            {
                Debug.WriteLine($"MSG_FMC.Parse: bad magic 0x{frame[0]:X2} 0x{frame[1]:X2}");
                return;
            }

            ushort computed = CrcHelper.Crc16(frame, FRAME_RESPONSE_LEN - 2);
            ushort received = (ushort)((frame[FRAME_RESPONSE_LEN - 2] << 8)
                                      | frame[FRAME_RESPONSE_LEN - 1]);
            if (computed != received)
            {
                Debug.WriteLine($"MSG_FMC.Parse: CRC mismatch computed=0x{computed:X4} received=0x{received:X4}");
                return;
            }

            byte status = frame[4];
            if (status != 0x00)
            {
                Debug.WriteLine($"MSG_FMC.Parse: STATUS=0x{status:X2}");
                return;
            }

            byte[] payload = new byte[PAYLOAD_LEN];
            Array.Copy(frame, PAYLOAD_OFFSET, payload, 0, PAYLOAD_LEN);

            byte cmd = payload[0];
            if (cmd == 0x00 || cmd == 0xA1)  // REG1 CMD_BYTE: 0x00 (v4.0.0) | 0xA1 (legacy pre-FW-C10)
                ParseMSG01(payload, 0);
            //else if (cmd == (byte)ICD.GET_REGISTER2)
            //    ParseMSG02(payload, 0);
        }

        // -------------------------------------------------------------------
        // ParseMSG01 — FMC REG1 payload (ICD v4.0.0)
        // Called embedded from MSG_BDC.ParseMSG01() with ndx = BDC REG1 [169].
        // Returns ndx + 64 (full 64-byte block).
        // -------------------------------------------------------------------
        public int ParseMSG01(byte[] msg, int ndx)
        {
            int startNdx = ndx;
            ndx++;                                                                    // [0] CMD byte
            System_State = (SYSTEM_STATES)msg[ndx]; ndx++;                          // [1]
            BDC_Mode = (BDC_MODES)msg[ndx]; ndx++;                          // [2]
            HB_ms = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(UInt16);  // [3–4]
            dt_us = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(UInt16);  // [5–6]
            HealthBits = msg[ndx]; ndx++;                                          // [7] FMC HEALTH_BITS

            StagePosition = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32); // [8–11]
            StageError = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32); // [12–15]
            StageStatus = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32); // [16–19]

            FSM_PosX = BitConverter.ToInt32(msg, ndx); ndx += sizeof(Int32);        // [20–23]
            FSM_PosY = BitConverter.ToInt32(msg, ndx); ndx += sizeof(Int32);        // [24–27]

            _ntpTime = BitConverter.ToInt64(msg, ndx); ndx += sizeof(Int64);     // [28–35]
            FW_VERSION = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32);    // [36–39]
            TEMP_MCU = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);    // [40–43]
            TimeBits = msg[ndx]; ndx++;                                            // [44] TIME_BITS
            HW_REV = msg[ndx]; ndx++;                                            // [45] HW_REV
            PowerBits = msg[ndx]; ndx++;                                            // [46] POWER_BITS

            // [47–58] BME280 TPH — V2 only; V1 reads 0.0f (firmware leaves bytes 0x00)
            TPH_Temp = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single); // [47–50]
            TPH_Pressure = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single); // [51–54]
            TPH_Humidity = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single); // [55–58]
            // [59–63] RESERVED — skip to end of 64-byte block

            return startNdx + 64;
        }
        // -------------------------------------------------------------------
        // ParseMSG02 — REG2 extended data (direct request only, not embedded)
        // -------------------------------------------------------------------
        public int ParseMSG02(byte[] msg, int ndx)
        {
            ndx++;  // CMD byte
            FSM_iFOV_X = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            FSM_iFOV_Y = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            FSM_X0     = BitConverter.ToInt16(msg, ndx);  ndx += sizeof(Int16);
            FSM_Y0     = BitConverter.ToInt16(msg, ndx);  ndx += sizeof(Int16);
            FSM_SIGN_X = unchecked((sbyte)msg[ndx]);       ndx++;
            FSM_SIGN_Y = unchecked((sbyte)msg[ndx]);       ndx++;
            ndx += sizeof(UInt32);  // reserved
            StageHome  = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32);
            return ndx;
        }

        private static bool IsBitSet(byte b, int pos) => (b & (1 << pos)) != 0;
    }
}
