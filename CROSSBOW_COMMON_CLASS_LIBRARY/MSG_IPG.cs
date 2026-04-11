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
using System.Diagnostics;

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

        // PowerSetting_W — max power driven by sensed model
        public double PowerSetting_W => SetPoint / 100.0 * MaxPower_W;

        // Convenience
        public bool isError    { get { return ErrorWord    != 0; } }
        public bool isEmitting { get { return OutputPower_W > 0; } }

        public string ModelName { get; set; } = "---";
        public string SerialNumber { get; set; } = "---";

        public LASER_MODEL LaserModel { get; set; } = LASER_MODEL.UNKNOWN;

        public bool IsSensed => LaserModel.IsSensed();
        public int MaxPower_W => LaserModel.MaxPower_W();

        // Emission bit — 3K=bit0, 6K=bit2
        public bool IsEMON => LaserModel == LASER_MODEL.YLR_6K
            ? (StatusWord & (1u << 2)) != 0
            : (StatusWord & (1u << 0)) != 0;

        // Not-ready — 3K=bit9, 6K=bit11 (PSU off)
        public bool IsNotReady => LaserModel == LASER_MODEL.YLR_6K
            ? (StatusWord & (1u << 11)) != 0
            : (StatusWord & (1u << 9)) != 0;

        public static bool GetBit(uint word, int bit) => (word & (1u << bit)) != 0;
        private void TrySenseModel(string payload)
        {
            var parts = payload.Split('-');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int power))
            {
                if (power == 3000) { LaserModel = LASER_MODEL.YLM_3K; ModelName = payload; }
                else if (power == 6000) { LaserModel = LASER_MODEL.YLR_6K; ModelName = payload; }
                else Debug.WriteLine($"IPG ERROR — unrecognised power field: {power}");
            }
            else Debug.WriteLine($"IPG ERROR — sense parse failed: '{payload}'");
        }
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

        // ── ParseDirect — ASCII response path (direct TCP from HEL eng GUI)
        // Called from hel.cs with the split cmd/payload from each laser response line.
        // Handles both 3K and 6K responses — model-conditional fields left at 0
        // when the command is not applicable (e.g. RHKPS on 6K never called).
        public void ParseDirect(string cmd, string payload)
        {
            switch (cmd.ToUpper())
            {
                case "RMODEL":
                    // 3K path — returns model string e.g. "YLM-3000-SM-VV"
                    // 6K returns empty — ignore if payload empty
                    if (!string.IsNullOrWhiteSpace(payload))
                        TrySenseModel(payload);
                    break;
                case "RMN":
                    // 3K: returns hostname e.g. "IPGP578" — ignore (no '-')
                    // 6K: returns model string e.g. "YLM-6000-U3-SM"
                    if (payload.Contains('-'))
                        TrySenseModel(payload);
                    else
                        Debug.WriteLine($"IPG RMN (hostname): {payload}");
                    break;
                case "RSN":
                    SerialNumber = payload;
                    break;
                case "RHKPS":
                    if (double.TryParse(payload, out double hk))
                        HKVoltage = hk;
                    break;
                case "RBSTPS":
                    if (double.TryParse(payload, out double bv))
                        BusVoltage = bv;
                    break;
                case "RCT":
                    if (double.TryParse(payload, out double tmp))
                        Temperature = tmp;
                    break;
                case "STA":
                    if (uint.TryParse(payload, out uint sta))
                        StatusWord = sta;
                    break;
                case "RMEC":
                    if (uint.TryParse(payload, out uint err))
                        ErrorWord = err;
                    break;
                case "RCS":
                case "SDC":
                case "SCS":
                    if (double.TryParse(payload, out double sp))
                        SetPoint = sp;
                    break;
                case "ROP":
                    if (payload == "OFF" || payload == "LOW")
                        OutputPower_W = 0;
                    else if (double.TryParse(payload, out double op))
                        OutputPower_W = op;
                    break;
            }
        }

    }
}
