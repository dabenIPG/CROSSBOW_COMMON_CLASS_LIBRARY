// MSG_IPG.cs  —  ICD v3.0.0 session 4 laser (IPG) block
//
// IPG block layout (21 bytes, MCC REG1 bytes 45–65):
//
//   [0–1]    Laser HK Voltage     uint16   centi-volts  (e.g. 2430 = 24.30 V)
//   [2–3]    Laser Bus Voltage    uint16   centi-volts
//   [4]      Laser Temperature    int8     °C
//   [5–8]    Laser Status Word    uint32   LE
//   [9–12]   Laser Error Word     uint32   LE
//   [13–16]  Laser SetPoint       float    %
//   [17–20]  Laser Output Power   float    W
//
// Session 4 changes from previous version:
//   HKVoltage  : float V     → uint16 centi-volts
//   BusVoltage : float V     → uint16 centi-volts
//   Temperature: float °C    → int8 °C
//   Block size : 28 bytes    → 21 bytes
//
// All three are decoded to double for callers — property interface
// is unchanged from the previous class so existing callers compile as-is.
//
// Parse(byte[] msg, int ndx) → int
//   Called from MSG_MCC.ParseMSG01() with ndx = 45.
//   Reads exactly IPG_BLOCK_LEN (21) bytes and returns ndx + 21.

using System;

namespace CROSSBOW
{
    public class MSG_IPG
    {
        // -------------------------------------------------------------------
        // Block size constant — used by MSG_MCC to advance ndx
        // -------------------------------------------------------------------
        public const int IPG_BLOCK_LEN = 21;

        // -------------------------------------------------------------------
        // Properties — engineering units
        // -------------------------------------------------------------------
        public double HKVoltage     { get; private set; } = 0;   // V
        public double BusVoltage    { get; private set; } = 0;   // V
        public double Temperature   { get; private set; } = 0;   // °C
        public UInt32 StatusWord    { get; private set; } = 0;
        public UInt32 ErrorWord     { get; private set; } = 0;
        public double SetPoint      { get; private set; } = 0;   // %
        public double OutputPower_W { get; private set; } = 0;   // W

        // Derived — 3000 W max power
        public double PowerSetting_W { get { return SetPoint / 100.0 * 3000; } }

        // Convenience
        public bool isError    { get { return ErrorWord    != 0; } }
        public bool isEmitting { get { return OutputPower_W > 0; } }

        // -------------------------------------------------------------------
        // Parse — reads 21 bytes at msg[ndx], returns updated ndx
        // -------------------------------------------------------------------
        public int Parse(byte[] msg, int ndx)
        {
            // uint16 centi-volts → V
            HKVoltage   = BitConverter.ToUInt16(msg, ndx) / 100.0; ndx += sizeof(UInt16);
            BusVoltage  = BitConverter.ToUInt16(msg, ndx) / 100.0; ndx += sizeof(UInt16);

            // int8 °C
            Temperature = (sbyte)msg[ndx]; ndx += sizeof(byte);

            StatusWord    = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32);
            ErrorWord     = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(UInt32);
            SetPoint      = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);
            OutputPower_W = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single);

            return ndx;
        }
    }
}
