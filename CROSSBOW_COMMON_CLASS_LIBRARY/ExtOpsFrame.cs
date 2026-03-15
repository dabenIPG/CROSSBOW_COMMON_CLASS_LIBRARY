// ExtOpsFrame.cs  —  EXT_OPS framing layer  ICD_EXTERNAL_INT / ICD_EXTERNAL_OPS v3.0.1
//
// Bidirectional frame protocol for THEIA ↔ external integrators on UDP:10009.
// Magic: 0xCB 0x48 — distinct from A2 (0xCB 0x49) and A3 (0xCB 0x58).
//
// Frame layout:
//   [0]     Magic HI  = 0xCB
//   [1]     Magic LO  = 0x48
//   [2]     CMD_BYTE
//   [3–4]   SEQ_NUM   uint16 LE
//   [5–6]   PAYLOAD_LEN uint16 LE
//   [7..7+N-1] PAYLOAD (N bytes)
//   [7+N..8+N] CRC16 uint16 LE  (CRC over bytes 0..7+N-1)
//
// CRC: CRC-16/CCITT  poly=0x1021  init=0xFFFF  no reflect  no final XOR
//
// CMD_BYTE assignments:
//   0xAA  CUE inbound        integrator → THEIA   payload 62 B  total 71 B
//   0xAF  Status response    THEIA → integrator   payload 30 B  total 39 B
//   0xAB  POS/ATT report     THEIA → integrator   payload 32 B  total 41 B

using System;
using System.Diagnostics;

namespace CROSSBOW
{
    /// <summary>
    /// Parsed inbound EXT_OPS frame.
    /// </summary>
    public class ParsedExtOpsFrame
    {
        public byte   Cmd        { get; set; }
        public ushort Seq        { get; set; }
        public byte[] Payload    { get; set; } = Array.Empty<byte>();
        public ushort PayloadLen { get; set; }
    }

    public static class ExtOpsFrame
    {
        // ── Frame constants ──────────────────────────────────────────────────
        public const byte   MAGIC_HI  = 0xCB;
        public const byte   MAGIC_LO  = 0x48;
        public const int    HDR_LEN   = 7;   // magic(2) + cmd(1) + seq(2) + plen(2)
        public const int    CRC_LEN   = 2;
        public const int    OVERHEAD  = HDR_LEN + CRC_LEN;  // 9 bytes

        // ── CMD byte assignments ──────────────────────────────────────────────
        public const byte CMD_CUE_INBOUND       = 0xAA;  // integrator → THEIA
        public const byte CMD_STATUS_RESPONSE   = 0xAF;  // THEIA → integrator
        public const byte CMD_POSATT_REPORT     = 0xAB;  // THEIA → integrator

        // ── Payload sizes ─────────────────────────────────────────────────────
        public const int PAYLOAD_LEN_CUE        = 62;
        public const int PAYLOAD_LEN_STATUS     = 30;
        public const int PAYLOAD_LEN_POSATT     = 32;

        // ── Total frame sizes ─────────────────────────────────────────────────
        public const int FRAME_LEN_CUE          = HDR_LEN + PAYLOAD_LEN_CUE    + CRC_LEN;  // 71
        public const int FRAME_LEN_STATUS       = HDR_LEN + PAYLOAD_LEN_STATUS + CRC_LEN;  // 39
        public const int FRAME_LEN_POSATT       = HDR_LEN + PAYLOAD_LEN_POSATT + CRC_LEN;  // 41

        // ── Track CMD enum (CUE inbound payload byte [17]) ────────────────────
        public enum TrackCmd : byte
        {
            Drop               = 0,
            Track              = 1,
            ReportOnce         = 2,
            ReportPosAtt       = 3,
            WeaponHold         = 4,
            WeaponFreeToFire   = 5,
            ReportContinuousOn  = 254,
            ReportContinuousOff = 255,
        }

        // ── Track Class enum (CUE inbound payload byte [16]) ──────────────────
        public enum TrackClass : byte
        {
            None        = 0,
            GroundObs   = 3,
            Sailplane   = 4,
            Balloon     = 5,
            UAV         = 8,
            Space       = 9,
            AcLight     = 10,
            AcMed       = 11,
            AcHeavy     = 13,
            AcHighPerf  = 14,
            AcRotor     = 15,
        }

        // ── CRC-16/CCITT ──────────────────────────────────────────────────────
        /// <summary>
        /// CRC-16/CCITT: poly=0x1021, init=0xFFFF, no reflect, no final XOR.
        /// Known-answer: Crc16("123456789") == 0x29B1.
        /// </summary>
        public static ushort Crc16(byte[] data, int offset, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = offset; i < offset + length; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int j = 0; j < 8; j++)
                    crc = ((crc & 0x8000) != 0) ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
            return crc;
        }

        // ── Frame builder ─────────────────────────────────────────────────────
        /// <summary>
        /// Build a complete EXT_OPS frame. Returns the framed byte array.
        /// </summary>
        public static byte[] BuildFrame(byte cmd, ushort seq, byte[] payload)
        {
            int plen  = payload?.Length ?? 0;
            int total = HDR_LEN + plen + CRC_LEN;
            byte[] frame = new byte[total];

            frame[0] = MAGIC_HI;
            frame[1] = MAGIC_LO;
            frame[2] = cmd;
            frame[3] = (byte)(seq & 0xFF);
            frame[4] = (byte)(seq >> 8);
            frame[5] = (byte)(plen & 0xFF);
            frame[6] = (byte)(plen >> 8);

            if (plen > 0)
                Buffer.BlockCopy(payload, 0, frame, HDR_LEN, plen);

            ushort crc = Crc16(frame, 0, HDR_LEN + plen);
            frame[HDR_LEN + plen]     = (byte)(crc & 0xFF);
            frame[HDR_LEN + plen + 1] = (byte)(crc >> 8);

            return frame;
        }

        // ── Frame parser ──────────────────────────────────────────────────────
        /// <summary>
        /// Validate and parse a received EXT_OPS frame.
        /// Returns false if magic, length, or CRC check fails.
        /// </summary>
        public static bool TryParseFrame(byte[] buf, int len, out ParsedExtOpsFrame parsed)
        {
            parsed = null;

            if (len < OVERHEAD)
            {
                Debug.WriteLine($"[ExtOpsFrame] Too short: {len}");
                return false;
            }
            if (buf[0] != MAGIC_HI || buf[1] != MAGIC_LO)
            {
                Debug.WriteLine($"[ExtOpsFrame] Bad magic: 0x{buf[0]:X2} 0x{buf[1]:X2}");
                return false;
            }

            ushort payloadLen = (ushort)(buf[5] | (buf[6] << 8));
            int    expected   = HDR_LEN + payloadLen + CRC_LEN;

            if (len != expected)
            {
                Debug.WriteLine($"[ExtOpsFrame] Length mismatch: got {len}, expected {expected}");
                return false;
            }

            ushort crcReceived  = (ushort)(buf[HDR_LEN + payloadLen] | (buf[HDR_LEN + payloadLen + 1] << 8));
            ushort crcComputed  = Crc16(buf, 0, HDR_LEN + payloadLen);

            if (crcReceived != crcComputed)
            {
                Debug.WriteLine($"[ExtOpsFrame] CRC fail: got 0x{crcReceived:X4}, computed 0x{crcComputed:X4}");
                return false;
            }

            parsed = new ParsedExtOpsFrame
            {
                Cmd        = buf[2],
                Seq        = (ushort)(buf[3] | (buf[4] << 8)),
                PayloadLen = payloadLen,
                Payload    = new byte[payloadLen],
            };
            if (payloadLen > 0)
                Buffer.BlockCopy(buf, HDR_LEN, parsed.Payload, 0, payloadLen);

            return true;
        }

        // ── Little-endian helpers ─────────────────────────────────────────────
        public static void WriteFloat(byte[] buf, int offset, float value)
            => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buf, offset, 4);

        public static void WriteDouble(byte[] buf, int offset, double value)
            => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buf, offset, 8);

        public static void WriteInt64(byte[] buf, int offset, long value)
            => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buf, offset, 8);

        public static float  ReadFloat (byte[] buf, int offset) => BitConverter.ToSingle(buf, offset);
        public static double ReadDouble(byte[] buf, int offset) => BitConverter.ToDouble(buf, offset);
        public static long   ReadInt64 (byte[] buf, int offset) => BitConverter.ToInt64(buf, offset);
    }
}
