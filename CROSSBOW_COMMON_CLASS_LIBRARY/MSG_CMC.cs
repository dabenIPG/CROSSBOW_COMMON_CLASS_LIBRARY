// MSG_CMC.cs  —  ICD v3.0.0 session 4 charger (CMC) block
//
// Embedded block layout (32 bytes, MCC REG1 bytes 213–244):
//
//   [0–3]    Charger Voltage Input    float    V
//   [4–7]    Charger Voltage Output   float    V
//   [8–11]   Charger Current Output   float    A
//   [12–15]  Fan1 Speed               float    RPM
//   [16–19]  Fan2 Speed               float    RPM
//   [20–21]  CHARGE STATUS            uint16
//   [22]     CHARGE LEVEL             uint8    CHARGE_LEVELS enum
//   [23–26]  Current Limit (IOUT_MAX) float    A
//   [27–30]  Voltage Limit (VOUT_MAX) float    V
//   [31]     CHARGER STATUS BITS      uint8    bit0:isConnected; 1:isHealthy; 2:isCharging;
//                                              3:isFullyCharged; 4:isHighCharge; 5:is220V
//
// Two entry points:
//   Parse(byte[] msg, int ndx) → int
//     Embedded use — called from MSG_MCC.ParseMSG01() with ndx = 213.
//     Reads exactly CHARGER_BLOCK_LEN (32) bytes, returns ndx + 32.
//
//   ParseMsg(byte[] msg, int ndx) → int
//     Standalone use — called when receiving direct CMC frames.
//     Dispatches on CMD byte to ParseMSG01 (REG1) or ParseMSG02 (REG2).

using System;

namespace CROSSBOW
{
    public class MSG_CMC
    {
        // -------------------------------------------------------------------
        // Block size constant — used by MSG_MCC to advance ndx
        // -------------------------------------------------------------------
        public const int CHARGER_BLOCK_LEN = 32;

        // -------------------------------------------------------------------
        // Charge status derived enum
        // -------------------------------------------------------------------
        public enum CHRG_STATUS { Full, CC, CV, Float, NA }

        public CHRG_STATUS STATUS
        {
            get
            {
                if (isFullyCharged) return CHRG_STATUS.Full;
                if (isFloatMode)    return CHRG_STATUS.Float;
                if (isCCMode)       return CHRG_STATUS.CC;
                if (isCVMode)       return CHRG_STATUS.CV;
                return CHRG_STATUS.NA;
            }
        }

        // -------------------------------------------------------------------
        // Properties
        // -------------------------------------------------------------------
        public DateTime lastMsgRx { get; private set; } = DateTime.UtcNow;
        public double   RX_HB     { get; private set; } = 0;

        public double VIN        { get; private set; } = 0;   // V
        public double VOUT       { get; private set; } = 0;   // V
        public double IOUT       { get; private set; } = 0;   // A
        public double IOUT_MAX   { get; private set; } = 0;   // A  (Current Limit)
        public double VOUT_MAX   { get; private set; } = 0;   // V  (Voltage Limit)
        public double VFLOAT     { get; private set; } = 0;   // V  (REG2 only)
        public double FAN1_SPEED { get; private set; } = 0;   // RPM
        public double FAN2_SPEED { get; private set; } = 0;   // RPM

        public string MFR_NAME  { get; private set; } = "NA";   // REG2 only
        public string MFR_MODEL { get; private set; } = "NA";   // REG2 only

        public ushort        CHARGE_STATUS       { get; private set; } = 0;
        public ushort        CHARGE_CURVE_CONFIG { get; private set; } = 0;   // REG2 only
        public byte          ONOFF_CONFIG        { get; private set; } = 0;   // REG2 only
        public byte          STATUS_BITS1        { get; set; }          = 0;
        public CHARGE_LEVELS ChargeLevel         { get; private set; } = CHARGE_LEVELS.LO;

        // STATUS_BITS1 — ICD v3.0.0 session 4
        public bool isConnected    { get { return IsBitSet(STATUS_BITS1, 0); } }
        public bool isHealthy      { get { return IsBitSet(STATUS_BITS1, 1); } }
        public bool isCharging     { get { return IsBitSet(STATUS_BITS1, 2); } }
        public bool isFullyCharged2 { get { return IsBitSet(STATUS_BITS1, 3); } }
        public bool isHighCharge   { get { return IsBitSet(STATUS_BITS1, 4); } }
        public bool is220V         { get { return IsBitSet(STATUS_BITS1, 5); } }

        // CHARGE_STATUS word — lower byte
        public bool isFullyCharged { get { return IsBitSet((byte)(CHARGE_STATUS & 0xFF), 0); } }
        public bool isCCMode       { get { return IsBitSet((byte)(CHARGE_STATUS & 0xFF), 1); } }
        public bool isCVMode       { get { return IsBitSet((byte)(CHARGE_STATUS & 0xFF), 2); } }
        public bool isFloatMode    { get { return IsBitSet((byte)(CHARGE_STATUS & 0xFF), 3); } }

        // CHARGE_STATUS word — upper byte
        public bool isEEPROMError     { get { return IsBitSet((byte)(CHARGE_STATUS >> 8), 0); } }
        public bool isTCShort         { get { return IsBitSet((byte)(CHARGE_STATUS >> 8), 1); } }
        public bool isBatteryDetected { get { return IsBitSet((byte)(CHARGE_STATUS >> 8), 2); } }

        // -------------------------------------------------------------------
        // Parse — embedded entry point, called from MSG_MCC.ParseMSG01()
        // Reads exactly 32 bytes at msg[ndx], returns ndx + 32
        // -------------------------------------------------------------------
        public int Parse(byte[] msg, int ndx)
        {
            if (msg == null || ndx + CHARGER_BLOCK_LEN > msg.Length)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"MSG_CMC.Parse: buffer too short at ndx={ndx}");
                return ndx + CHARGER_BLOCK_LEN;
            }

            ndx = ParseMSG01(msg, ndx);
            return ndx;
        }

        // -------------------------------------------------------------------
        // ParseMsg — standalone entry point, dispatches on CMD byte
        // -------------------------------------------------------------------
        public int ParseMsg(byte[] msg, int ndx)
        {
            RX_HB     = (DateTime.UtcNow - lastMsgRx).TotalMilliseconds;
            lastMsgRx = DateTime.UtcNow;

            ICD cmd = (ICD)msg[0];
            switch (cmd)
            {
                case ICD.GET_REGISTER1: ndx = ParseMSG01(msg, ndx); break;
                //case ICD.GET_REGISTER2: ndx = ParseMSG02(msg, ndx); break;
                default: break;
            }
            return ndx;
        }

        // -------------------------------------------------------------------
        // ParseMSG01 — REG1 / embedded block (32 bytes)
        // Field order matches ICD v3.0.0 MCC REG1 bytes 213–244 exactly.
        // -------------------------------------------------------------------
        private int ParseMSG01(byte[] msg, int ndx)
        {
            VIN           = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            VOUT          = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            IOUT          = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            FAN1_SPEED    = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            FAN2_SPEED    = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            CHARGE_STATUS = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(ushort);
            ChargeLevel   = (CHARGE_LEVELS)msg[ndx];          ndx++;
            IOUT_MAX      = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            VOUT_MAX      = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            STATUS_BITS1  = msg[ndx];                          ndx++;
            return ndx;
        }

        // -------------------------------------------------------------------
        // ParseMSG02 — REG2 extended data (direct request only, not embedded)
        // -------------------------------------------------------------------
        private int ParseMSG02(byte[] msg, int ndx)
        {
            VIN    = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            VOUT   = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            IOUT   = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            IOUT_MAX = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            VOUT_MAX = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            VFLOAT   = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            MFR_NAME  = System.Text.Encoding.Default.GetString(msg, ndx, 13).Trim(); ndx += 13;
            MFR_MODEL = System.Text.Encoding.Default.GetString(msg, ndx, 13).Trim(); ndx += 13;

            FAN1_SPEED = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            FAN2_SPEED = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            CHARGE_CURVE_CONFIG = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(ushort);
            CHARGE_STATUS       = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(ushort);
            ONOFF_CONFIG        = msg[ndx];                          ndx++;
            return ndx;
        }

        private static bool IsBitSet(byte b, int pos) => (b & (1 << pos)) != 0;
    }
}
