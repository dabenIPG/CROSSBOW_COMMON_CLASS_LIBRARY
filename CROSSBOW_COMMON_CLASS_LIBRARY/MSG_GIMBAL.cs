// MSG_GIMBAL.cs  —  ICD v3.0.0 session 4 gimbal block
//
// Gimbal block layout (39 bytes, BDC REG1 bytes 20–58):
//
//   [20]      GIMBAL STATUS BITS   uint8
//   [21–24]   Gimbal Pan Count     int32   from Galil (dr)
//   [25–28]   Gimbal Tilt Count    int32   from Galil (dr)
//   [29–32]   Gimbal Pan Speed     int32   from Galil (dr)
//   [33–36]   Gimbal Tilt Speed    int32   from Galil (dr)
//   [37]      StopCodeX            uint8
//   [38]      StopCodeY            uint8
//   [39–40]   StatusX              uint16
//   [41–42]   StatusY              uint16
//   [43–46]   RelativeAnglePan     float   °
//   [47–50]   RelativeAngleTilt    float   °
//   [51–54]   NED_Azimuth          float   °
//   [55–58]   NED_Elevation        float   °
//
// Non-contiguous fields set externally after full BDC REG1 parse:
//   HomeX / HomeY     — BDC REG1 [237–244]  int32
//   BasePitch / BaseRoll — BDC REG1 [124–131]  float
//
// ParseMsg(byte[] msg, int ndx) → int
//   Called from MSG_BDC.ParseMSG01() with ndx = 20.
//   Reads exactly 39 bytes and returns 59.
//   Caller continues with TRC STATUS BITS at [59].

using System;

namespace CROSSBOW
{
    public class MSG_GIMBAL
    {
        // Place Holders
        // Place Holders
        public string FW_VERSION_STRING { get; private set; } = "NA";
        public float TEMP { get; private set; } = 0;

        // -------------------------------------------------------------------
        // Encoder constants — firmware-defined, not in ICD
        // -------------------------------------------------------------------
        public UInt32 EncoderMaxX { get; private set; } = 524288;
        public UInt32 EncoderMaxY { get; private set; } = 524288;

        // -------------------------------------------------------------------
        // Non-contiguous fields — set externally by MSG_BDC after full parse
        // -------------------------------------------------------------------
        // ICD v3.0.0 session 4: HomeX/Y are int32 (were UInt32)
        // Set from BDC REG1 [237–244]
        public Int32 HomeX { get; set; } = 0;
        public Int32 HomeY { get; set; } = 0;

        // Set from BDC REG1 [124–131]
        public float BasePitch { get; set; } = 0;
        public float BaseRoll  { get; set; } = 0;

        // -------------------------------------------------------------------
        // Parsed properties
        // -------------------------------------------------------------------
        // [20] GIMBAL STATUS BITS
        public byte StatusBits { get; private set; } = 0;
        public bool isReady     { get { return IsBitSet(StatusBits, 0); } }
        public bool isConnected { get { return IsBitSet(StatusBits, 1); } }
        public bool isStarted   { get { return IsBitSet(StatusBits, 2); } }

        // [21–28] Position / Speed
        public Int32 PositionX { get; private set; } = 0;
        public Int32 PositionY { get; private set; } = 0;
        public Int32 SpeedX    { get; private set; } = 0;
        public Int32 SpeedY    { get; private set; } = 0;

        // [37–42] Stop codes / status
        public byte   StopCodeX { get; private set; } = 0;
        public byte   StopCodeY { get; private set; } = 0;
        public UInt16 StatusX   { get; private set; } = 0;
        public UInt16 StatusY   { get; private set; } = 0;

        // [43–58] Angles
        public double RelativeAnglePan_deg  { get; private set; } = 0;
        public double RelativeAngleTilt_deg { get; private set; } = 0;
        public double NED_Azimuth_deg       { get; private set; } = 0;
        public double NED_Elevation_deg     { get; private set; } = 0;

        // -------------------------------------------------------------------
        // ParseMsg — reads contiguous gimbal block [20–58], returns 59
        // -------------------------------------------------------------------
        public int ParseMsg(byte[] msg, int ndx)
        {
            StatusBits = msg[ndx]; ndx++;                                                    // [20]

            PositionX = BitConverter.ToInt32(msg, ndx); ndx += sizeof(Int32);               // [21–24]
            PositionY = BitConverter.ToInt32(msg, ndx); ndx += sizeof(Int32);               // [25–28]
            SpeedX    = BitConverter.ToInt32(msg, ndx); ndx += sizeof(Int32);               // [29–32]
            SpeedY    = BitConverter.ToInt32(msg, ndx); ndx += sizeof(Int32);               // [33–36]

            StopCodeX = msg[ndx]; ndx++;                                                     // [37]
            StopCodeY = msg[ndx]; ndx++;                                                     // [38]

            StatusX = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(UInt16);               // [39–40]
            StatusY = BitConverter.ToUInt16(msg, ndx); ndx += sizeof(UInt16);               // [41–42]

            RelativeAnglePan_deg  = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single); // [43–46]
            RelativeAngleTilt_deg = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single); // [47–50]
            NED_Azimuth_deg       = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single); // [51–54]
            NED_Elevation_deg     = BitConverter.ToSingle(msg, ndx); ndx += sizeof(Single); // [55–58]

            return ndx;   // returns 59 — caller continues with TRC STATUS BITS at [59]
        }

        bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }
    }
}
