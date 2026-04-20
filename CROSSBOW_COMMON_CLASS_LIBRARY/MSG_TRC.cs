// MSG_TRC.cs  —  ICD v4.1.0 TRC REG1 parser
//
// 64-byte TRC REG1 block embedded in BDC REG1 bytes [60–123].
// StatusBits0 at BDC REG1 byte [59] is set externally before ParseMsg().
//
// TRC REG1 layout (64 bytes):
//   [0]      cmd_byte        uint8   0x00 (v4.0.0) | 0xA1 (legacy pre-FW-C10)
//   [1–4]    version_word    uint32
//   [5]      systemState     uint8
//   [6]      systemMode      uint8
//   [7–8]    HB_ms           uint16  ms between sends
//   [9–10]   dt_us           uint16  µs in processing loop
//   [11]     overlayMask     uint8
//   [12–13]  fps             uint16  framerate ×100
//   [14–15]  deviceTemp      int16   VIS sensor temp °C
//   [16]     camid           uint8
//   [17]     status_cam0     uint8
//   [18]     status_track0   uint8
//   [19]     status_cam1     uint8
//   [20]     status_track1   uint8
//   [21–22]  tx              int16
//   [23–24]  ty              int16
//   [25]     atX0            int8
//   [26]     atY0            int8
//   [27]     ftX0            int8
//   [28]     ftY0            int8
//   [29–32]  focusScore      float
//   [33–40]  ntpEpochTime    int64   ms since epoch
//   [41]     voteBitsMcc     uint8
//   [42]     voteBitsBdc     uint8
//   [43–44]  nccScore        int16   NCC ×10000
//   [45]     jetsonTemp      uint8   CPU temp °C
//   [46]     jetsonCpuLoad   uint8   CPU load %
//   [47]     jetsonGpuLoad   uint8   GPU load %
//   [48]     jetsonGpuTemp   uint8   GPU temp °C — thermal_zone1 — v4.1.0
//   [49–56]  somSerial       uint64  Jetson SOM serial — from /proc/device-tree/serial-number
//   [57–63]  RESERVED        7 bytes

using System;
using System.Diagnostics;

namespace CROSSBOW
{
    public class MSG_TRC
    {
        // -------------------------------------------------------------------
        // Frame constants — A2 framed response (521 bytes)
        // Wire format:
        //   [0]      MAGIC_HI  = 0xCB
        //   [1]      MAGIC_LO  = 0x49  (internal A2)
        //   [2]      SEQ_NUM   uint8
        //   [3]      CMD_BYTE  uint8
        //   [4]      STATUS    uint8   0x00 = OK
        //   [5–6]    PAYLOAD_LEN uint16 LE  always 512
        //   [7–518]  PAYLOAD   512 bytes   (TRC REG1 64 bytes at [7–70]; rest 0x00)
        //   [519–520] CRC-16/CCITT uint16 BE
        // -------------------------------------------------------------------
        public const byte MAGIC_HI           = 0xCB;
        public const byte MAGIC_LO           = 0x49;
        public const int  FRAME_RESPONSE_LEN = 521;
        public const int  PAYLOAD_OFFSET     = 7;
        public const byte STATUS_OK          = 0x00;

        // Last raw frame STATUS byte received — useful for diagnostics
        public byte LastFrameStatus { get; private set; } = 0xFF;

        // -------------------------------------------------------------------
        // Parsed properties
        // -------------------------------------------------------------------
        public CAMERA[] Cameras { get; set; } =
        [
            new CAMERA(BDC_CAM_IDS.VIS),
            new CAMERA(BDC_CAM_IDS.MWIR),
        ];

        public SYSTEM_STATES System_State { get; set; } = SYSTEM_STATES.OFF;
        public BDC_MODES     BDC_Mode     { get; set; } = BDC_MODES.OFF;
        public BDC_CAM_IDS   Active_CAM   { get; set; } = BDC_CAM_IDS.VIS;

        // ICD v3.0.0 session 4: HB_ms is uint16 ms (was float µs)
        public UInt16 HB_ms    { get; private set; } = 1000;
        public double HB_TX_ms { get { return (double)HB_ms; } }
        public UInt16 dt_us    { get; private set; } = 0;

        // Version — semver: bits[31:24]=major  [23:12]=minor  [11:0]=patch
        public UInt32 SW_VERSION_WORD { get; private set; } = 0;
        public string SW_VERSION_STRING
        {
            get
            {
                uint major = (SW_VERSION_WORD >> 24) & 0xFF;
                uint minor = (SW_VERSION_WORD >> 12) & 0xFFF;
                uint patch =  SW_VERSION_WORD        & 0xFFF;
                return $"{major}.{minor}.{patch}";
            }
        }
        public string FW_VERSION_STRING => SW_VERSION_STRING;   // alias matching frmTMC pattern
        public uint SW_MAJOR => (SW_VERSION_WORD >> 24) & 0xFF;
        public bool IsV4     => SW_MAJOR >= 4;                  // true = ICD v3.6.0 command space

        // TRC has no hardware revision — single Jetson platform
        public string HW_REV_Label => "--";

        // Time source — TRC uses OS NTP only (PTP pending NEW-38d)
        public DateTime epochTime             => ntpTime;
        public string   activeTimeSourceLabel => "NTP";

        // SOM serial display helper — "N/A" on parse failure (0) or missing file
        public string SomSerialLabel => SomSerial == 0 ? "N/A" : SomSerial.ToString();

        private Int64   _ntpTime { get; set; } = 0;
        public DateTime ntpTime  { get { return DateTimeOffset.FromUnixTimeMilliseconds(_ntpTime).UtcDateTime; } }

        public DateTime lastMsgRx { get; private set; } = DateTime.UtcNow;
        public double   RX_HB     { get; private set; } = 0;

        // VIS camera sensor temp (int16 °C)
        public Int16 deviceTemperature { get; private set; } = 0;

        // Jetson health
        public byte jetsonTemp    { get; private set; } = 0;
        public byte jetsonCpuLoad { get; private set; } = 0;
        public byte jetsonGpuLoad { get; private set; } = 0;   // v4.0.3 (CB-20260419)
        public byte jetsonGpuTemp { get; private set; } = 0;   // v4.1.0 (CB-20260419b) — thermal_zone1

        // SOM serial number — read once at TRC startup from /proc/device-tree/serial-number,
        // packed as uint64 LE into TRC REG1 [49–56]. 0 on parse failure or missing file.
        public UInt64 SomSerial { get; private set; } = 0;

        // fps = framerate ×100, unpack: value / 100.0
        public double streamFPS  { get; private set; } = 0;
        public Size   streamSize { get; private set; } = new Size(1280, 720);

        public Point TrackPoint   { get; private set; } = new Point(0, 0);
        public Point AT_OFFSET_RB { get; private set; } = new Point(0, 0);
        public Point FT_OFFSET_RB { get; private set; } = new Point(0, 0);

        // focusScore float (Laplacian variance)
        public float VIS_FOCUS_SCORE { get; private set; } = 0;

        // Overlay mask — HUD_OVERLAY_FLAGS bitmask
        public byte overlayMask { get; private set; } = 0;
        public bool isStreaming  { get { return IsBitSet(Cameras[(int)BDC_CAM_IDS.VIS].StatusBits, 2); } }

        // Fire control vote readbacks from TRC telemetry
        public byte voteBitsMcc { get; private set; } = 0;
        public byte voteBitsBdc { get; private set; } = 0;

        // NCC quality score ×10000, unpack: value / 10000.0
        public Int16 nccScoreRaw { get; private set; } = 0;
        public float nccScore    { get { return (float)nccScoreRaw / 10000.0f; } }

        // StatusBits0 set externally from BDC REG1 byte [59] (TRC STATUS BITS)
        // Not parsed from the 64-byte TRC REG1 block itself
        public byte StatusBits0 { get; set; } = 0;
        public bool isReady     { get { return IsBitSet(StatusBits0, 0); } }
        public bool isConnected { get { return IsBitSet(StatusBits0, 1); } }
        public bool isStarted   { get { return IsBitSet(StatusBits0, 2); } }

        // -------------------------------------------------------------------
        // Parse — validate and parse a 521-byte A2 framed response.
        // Called from trc.cs BackgroundUDPRead on every received frame.
        // Returns true on success; false if frame is malformed, bad CRC,
        // bad magic, or non-OK status.
        // -------------------------------------------------------------------
        public bool Parse(byte[] frame)
        {
            if (frame == null || frame.Length != FRAME_RESPONSE_LEN)
            {
                Debug.WriteLine($"MSG_TRC.Parse: bad length {frame?.Length} (expected {FRAME_RESPONSE_LEN})");
                return false;
            }

            if (frame[0] != MAGIC_HI || frame[1] != MAGIC_LO)
            {
                Debug.WriteLine($"MSG_TRC.Parse: bad magic 0x{frame[0]:X2} 0x{frame[1]:X2}");
                return false;
            }

            ushort computed = CrcHelper.Crc16(frame, FRAME_RESPONSE_LEN - 2);
            ushort received = (ushort)((frame[519] << 8) | frame[520]);
            if (computed != received)
            {
                Debug.WriteLine($"MSG_TRC.Parse: CRC mismatch computed=0x{computed:X4} received=0x{received:X4}");
                return false;
            }

            LastFrameStatus = frame[4];
            if (frame[4] != STATUS_OK)
            {
                Debug.WriteLine($"MSG_TRC.Parse: non-OK status 0x{frame[4]:X2}");
                return false;
            }

            // REG1 frame: CMD_BYTE 0x00 (v4.0.0 FW-C10) | 0xA1 (legacy pre-FW-C10) | 0xA4 keepalive poll
            // All other CMD_BYTEs (0xA0 subscribe ACK, etc.) have zero-filled payloads — update
            // liveness only, no REG1 parse.
            byte cmdByte = frame[3];
            if (cmdByte != 0x00 && cmdByte != 0xA1 && cmdByte != (byte)ICD.FRAME_KEEPALIVE)
            {
                lastMsgRx = DateTime.UtcNow;
                return true;
            }

            ParseMsg(frame, PAYLOAD_OFFSET);
            return true;
        }

        // -------------------------------------------------------------------
        // ParseMsg — parses 64-byte TRC REG1 block starting at ndx.
        //
        // Two call sites:
        //   1. Parse(byte[] frame) above — A2 framed response, ndx = PAYLOAD_OFFSET (7)
        //   2. MSG_BDC.ParseMSG01()  — BDC REG1 embedding,   ndx = BDC REG1 [60]
        //
        // Returns ndx advanced by 64 bytes.
        // -------------------------------------------------------------------
        public int ParseMsg(byte[] rxBuff, int ndx)
        {
            RX_HB     = (DateTime.UtcNow - lastMsgRx).TotalMilliseconds;
            lastMsgRx = DateTime.UtcNow;

            ndx++;                                                                          // [0]  cmd_byte (0x00 v4.0.0 | 0xA1 legacy)

            SW_VERSION_WORD   = BitConverter.ToUInt32(rxBuff, ndx); ndx += sizeof(UInt32); // [1–4]
            System_State      = (SYSTEM_STATES)rxBuff[ndx]; ndx++;                         // [5]
            BDC_Mode          = (BDC_MODES)rxBuff[ndx]; ndx++;                             // [6]
            HB_ms             = BitConverter.ToUInt16(rxBuff, ndx); ndx += sizeof(UInt16); // [7–8]
            dt_us             = BitConverter.ToUInt16(rxBuff, ndx); ndx += sizeof(UInt16); // [9–10]
            overlayMask       = rxBuff[ndx]; ndx++;                                         // [11]
            streamFPS         = (double)BitConverter.ToUInt16(rxBuff, ndx) / 100.0;
            ndx += sizeof(UInt16);                                                          // [12–13]
            deviceTemperature = BitConverter.ToInt16(rxBuff, ndx); ndx += sizeof(Int16);   // [14–15]
            Active_CAM        = (BDC_CAM_IDS)rxBuff[ndx]; ndx++;                           // [16]

            for (int i = 0; i < Cameras.Length; i++)
            {
                Cameras[i].StatusBits = rxBuff[ndx]; ndx++;                                // [17,19]
                Cameras[i].TrackBits  = rxBuff[ndx]; ndx++;                                // [18,20]
            }

            int tx = BitConverter.ToInt16(rxBuff, ndx); ndx += sizeof(Int16);              // [21–22]
            int ty = BitConverter.ToInt16(rxBuff, ndx); ndx += sizeof(Int16);              // [23–24]
            TrackPoint = new Point(tx, ty);

            int atx = (sbyte)rxBuff[ndx]; ndx++;                                           // [25]
            int aty = (sbyte)rxBuff[ndx]; ndx++;                                           // [26]
            int ftx = (sbyte)rxBuff[ndx]; ndx++;                                           // [27]
            int fty = (sbyte)rxBuff[ndx]; ndx++;                                           // [28]
            AT_OFFSET_RB = new Point(atx, aty);
            FT_OFFSET_RB = new Point(ftx, fty);

            VIS_FOCUS_SCORE = BitConverter.ToSingle(rxBuff, ndx); ndx += sizeof(Single);   // [29–32]
            _ntpTime        = BitConverter.ToInt64(rxBuff, ndx);  ndx += sizeof(Int64);    // [33–40]
            voteBitsMcc     = rxBuff[ndx]; ndx++;                                           // [41]
            voteBitsBdc     = rxBuff[ndx]; ndx++;                                           // [42]
            nccScoreRaw   = BitConverter.ToInt16(rxBuff, ndx); ndx += sizeof(Int16);     // [43–44]
            jetsonTemp    = rxBuff[ndx]; ndx++;                                           // [45]
            jetsonCpuLoad = rxBuff[ndx]; ndx++;                                           // [46]
            jetsonGpuLoad = rxBuff[ndx]; ndx++;                                           // [47]
            jetsonGpuTemp = rxBuff[ndx]; ndx++;                                           // [48] v4.1.0
            SomSerial     = BitConverter.ToUInt64(rxBuff, ndx); ndx += sizeof(UInt64);   // [49–56]
            ndx += 7;                                                                     // [57–63] RESERVED

            return ndx;
        }

        private static bool IsBitSet(byte b,   int pos) { return (b  & (1    << pos)) != 0; }
        private static bool IsBitSet(UInt32 b, int pos) { return (b  & (1u   << pos)) != 0; }
    }
}
