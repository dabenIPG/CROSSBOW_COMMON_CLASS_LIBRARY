// MSG_BATTERY.cs  —  ICD v3.0.0 session 4 battery block
//
// Battery block layout (11 bytes, MCC REG1 bytes 34–44):
//
//   [0–1]   Battery Pack Voltage   uint16  centi-volts  (e.g. 1260 = 12.60 V)
//   [2–3]   Battery Pack Current   int16   centi-amps   (e.g. −450 = −4.50 A)
//   [4–5]   Battery Bus Voltage    uint16  centi-volts
//   [6]     Battery Pack Temp      int8    °C
//   [7]     Battery ASOC           uint8   %
//   [8]     Battery RSOC           uint8   %
//   [9–10]  Battery Status Word    int16   16-bit flags
//
// Parse(byte[] msg, int ndx) → int
//   Called from MSG_MCC.ParseMSG01() with ndx = 34.
//   Reads exactly BATTERY_BLOCK_LEN (11) bytes and returns ndx + 11.

using System;

namespace CROSSBOW
{
    public class MSG_BATTERY
    {
        // -------------------------------------------------------------------
        // Block size constant — used by MSG_MCC to advance ndx
        // -------------------------------------------------------------------
        public const int BATTERY_BLOCK_LEN = 11;

        // -------------------------------------------------------------------
        // Parsed properties — wire units
        // -------------------------------------------------------------------
        public ushort PackVoltage_cV  { get; private set; } = 0;   // centi-volts
        public short  PackCurrent_cA  { get; private set; } = 0;   // centi-amps (signed)
        public ushort BusVoltage_cV   { get; private set; } = 0;   // centi-volts
        public sbyte  PackTemp        { get; private set; } = 0;   // °C
        public byte   ASOC            { get; private set; } = 0;   // %
        public byte   RSOC            { get; private set; } = 0;   // %
        public short  StatusWord      { get; private set; } = 0;   // 16-bit flags

        // -------------------------------------------------------------------
        // Derived properties — engineering units
        // -------------------------------------------------------------------
        public double PackVoltage { get { return PackVoltage_cV / 100.0; } }   // V
        public double PackCurrent { get { return PackCurrent_cA / 100.0; } }   // A (signed)
        public double BusVoltage  { get { return BusVoltage_cV  / 100.0; } }   // V

        // Convenience — HK rail alias
        public double HKVoltage         { get { return BusVoltage; } }
        public bool   isBreakerClosed   { get { return IsBitSet(StatusWord, 2); } }
        public bool   isContractorClosed { get { return IsBitSet(StatusWord, 3); } }

        bool IsBitSet(Int16 b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }

        // -------------------------------------------------------------------
        // Parse — reads 11 bytes at msg[ndx], returns ndx + 11
        // -------------------------------------------------------------------
        public int Parse(byte[] msg, int ndx)
        {
            if (msg == null || ndx + BATTERY_BLOCK_LEN > msg.Length)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"MSG_BATTERY.Parse: buffer too short at ndx={ndx}");
                return ndx + BATTERY_BLOCK_LEN;
            }

            PackVoltage_cV = (ushort)(msg[ndx + 0] | (msg[ndx + 1] << 8));   // LE
            PackCurrent_cA =  (short)(msg[ndx + 2] | (msg[ndx + 3] << 8));   // LE signed
            BusVoltage_cV  = (ushort)(msg[ndx + 4] | (msg[ndx + 5] << 8));   // LE
            PackTemp       =  (sbyte) msg[ndx + 6];
            ASOC           =          msg[ndx + 7];
            RSOC           =          msg[ndx + 8];
            StatusWord     =  (short)(msg[ndx + 9] | (msg[ndx + 10] << 8));  // LE signed

            return ndx + BATTERY_BLOCK_LEN;
        }
    }
}
