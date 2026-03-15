// CrcHelper.cs  —  CRC-16/CCITT shared helper
//
// Algorithm: CRC-16/CCITT
//   Poly  : 0x1021
//   Init  : 0xFFFF
//   RefIn : false
//   RefOut: false
//   XorOut: 0x0000
//
// Known-answer: Crc16("123456789") = 0x29B1
//
// Usage:
//   ushort crc = CrcHelper.Crc16(frame, frame.Length - 2);
//   frame[^2] = (byte)(crc >> 8);    // big-endian high
//   frame[^1] = (byte)(crc & 0xFF);  // big-endian low

namespace CROSSBOW
{
    public static class CrcHelper
    {
        private static readonly ushort[] _table = BuildTable();

        private static ushort[] BuildTable()
        {
            ushort[] table = new ushort[256];
            for (int i = 0; i < 256; i++)
            {
                ushort crc = (ushort)(i << 8);
                for (int j = 0; j < 8; j++)
                    crc = (ushort)((crc & 0x8000) != 0
                        ? (crc << 1) ^ 0x1021
                        :  crc << 1);
                table[i] = crc;
            }
            return table;
        }

        /// <summary>
        /// Compute CRC-16/CCITT over <paramref name="len"/> bytes starting at buf[0].
        /// </summary>
        public static ushort Crc16(byte[] buf, int len)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < len; i++)
                crc = (ushort)((crc << 8) ^ _table[(crc >> 8) ^ buf[i]]);
            return crc;
        }
    }
}
