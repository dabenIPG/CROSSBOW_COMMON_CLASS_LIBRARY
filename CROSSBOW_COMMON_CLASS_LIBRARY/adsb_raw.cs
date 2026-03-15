using GeographicLib;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
// System.Numerics removed — BigInteger no longer used (see Convert.FromHexString fix)
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CROSSBOW
{
    public class ADSB_MSG
    {
        public enum MSGTYPES
        {
            None = 0,
            ID = 1,
            POS_SURF = 2,
            POS_AIR_BARO = 3,
            VEL_AIR = 4,
            SURV_ALT = 5,
            SURV_ID = 6,
            AIR2AIR = 7,
            ALLCALL = 8,
            STATUS = 9,
            RESERVED = 10,
            POS_AIR_GPS = 11,

        }

        public enum WAKEVORTEXCTGS
        {
            None = 0,
            SURF_EMERG_VEH = 1,
            SURF_SERV_VEH = 2,
            GROUND_OBS = 3,
            SAILPLANE = 4,
            BALLOON = 5,
            SKYDIVER = 6,
            ULTRALIGHT = 7,
            UAV = 8,
            SPACE = 9,
            AC_LIGHT = 10,
            AC_MED = 11,
            AC_HVTX = 12,
            AC_HEAVY = 13,
            AC_HIGHPERF = 14,
            AC_ROTOR = 15,
            RESERVED = 16,
        }


        public string ICAO { get; private set; } = "NA";
        public string CallSign { get; private set; } = "NA";
        public string Squawk { get; private set; } = "NA";
        public DateTime LastUpdateTime { get; private set; } = DateTime.UtcNow;
        public bool ValidMsg { get; private set; } = false;
        public bool ToAdd { get; private set; } = false;
        public bool ToDelete { get; private set; } = false;
        public double Latitude { get { return lat; } }
        public double Longitude { get { return lng; } }
        /// <summary>Pressure altitude in metres (29.92 inHg reference). NOT MSL below FL180; NOT HAE.
        /// Apply the GNSS-Baro delta (dAlt_gps_m) before feeding to lla2ned().</summary>
        public double Alt_Baro_m { get { return COMMON.ft2m(alt_ft); } }
        /// <summary>GNSS minus Baro altitude delta, metres. NaN when TC=19 dALT field is all-zeros (no data).</summary>
        public double dAlt_gps_m { get { return COMMON.ft2m(dalt_gps_ft); } }
        public double Speed_mps { get { return COMMON.kts2mps(speed_kts); } }
        /// <summary>Height Above Ellipsoid (WGS-84 HAE), metres.
        /// <list type="bullet">
        /// <item>TC 9–18 (POS_AIR_BARO): pressure altitude + GNSS/baro delta. NaN if either component is NaN.</item>
        /// <item>TC 20–22 (POS_AIR_GPS): GNSS height directly — delta must NOT be added again.</item>
        /// <item>TC 5–8  (POS_SURF):     BaseStation elevation — no altitude field in surface messages.</item>
        /// </list>
        /// Guard callers with <c>if (!double.IsNaN(Alt_HAE_m))</c>.</summary>
        public double Alt_HAE_m
        {
            get
            {
                switch (MsgType)
                {
                    case MSGTYPES.POS_AIR_GPS: // TC 20–22: alt_ft is GNSS HAE from GetGnssAltitude() — do not add delta
                    case MSGTYPES.POS_SURF:    // TC 5–8:   alt_ft is BaseStation elevation approx — delta is meaningless
                        return COMMON.ft2m(alt_ft);
                    default:                   // TC 9–18:  baro alt + GNSS/baro delta
                        return Alt_Baro_m + dAlt_gps_m;
                }
            }
        }

        public double Heading_deg { get { return heading_deg; } }
        public double VerticalRate_mps { get { return vr_fpm * 0.00508; } } // ft/min → m/s (1 ft/min = 0.3048/60 m/s)

        private double lat, lng, alt_ft, speed_kts, heading_deg, vr_fpm, dalt_gps_ft;
        public MSGTYPES MsgType { get; private set; } = MSGTYPES.None;
        public WAKEVORTEXCTGS WakeVortexCat { get; private set; } = WAKEVORTEXCTGS.None;
        public ptLLA BaseStation { get; set; } = new ptLLA(34.4593583, -86.4326550, 174.6); // HAE, WGS-84 — matches CROSSBOW.BaseStation canonical

        public ADSB_MSG(byte[] bmsg, ptLLA _bs)
        {
            // parses a raw ADSB bitarry and sends back an ADSBLog -- assume ACAS 112bit for now
            /*
             * BIT      #Bits   ABB     INFO                        BYTE
             * 1-5      5       DF      Downlink Format             0*
             * 6-8      3       CA      Transponder Capability      0*
             * 9-32     24      ICAO    ICAO                        1-3
             * 33-88    56      ME      Message, ext squitter       4-10
             * (33-37)  5       (TC)    (Type Code)                 4*
             * 89-112   24      PI      Parity                      11-13
             * 
             * DF=17 ADS-B
             * 
             * 
             */

            BaseStation = new ptLLA(_bs.lat, _bs.lng, _bs.alt);

            string bitmsg = GetBits(bmsg);


            int DF = bmsg[0] >> 3; // upper 5 bits
            int CAP = bmsg[0] & 7; // lower 3 bits

            if (DF != 17)
                return;


            byte[] bICAO = new byte[3];
            Array.Copy(bmsg, 1, bICAO, 0, 3);
            ICAO = BitConverter.ToString(bICAO).Replace("-", string.Empty);

            // within ME are TYPE CODE, AC CAT, CALL SIGN
            int TC = bmsg[4] >> 3; // upper 5 bits

            //Debug.WriteLine($"ICAO: [{ICAO}]; DF = {DF}; CAP = {CAP}; TC = {TC}; CA = {CA}");
            ValidMsg = true;

            if (TC >= 1 && TC <= 4)
            {
                MsgType = MSGTYPES.ID;

                // get Wake Vortex 
                int CA = bmsg[4] & 7; // lower 3 bits
                WakeVortexCat = GetWakeVortexCateg(TC, CA);

                // get CAll Sign
                CallSign = GetCallSign(bitmsg);


            }
            if (TC >= 5 && TC <= 8)
            {
                // TC 5–8: Surface Position. Ground speed from Movement field; heading from bits 44–51;
                // CPR lat/lon identical layout to TC 9–18. No altitude field — alt set to BaseStation.alt.
                MsgType = MSGTYPES.POS_SURF;
                GetPositionSurface(bitmsg);
            }
            if (TC >= 9 && TC <= 18)
            {
                MsgType = MSGTYPES.POS_AIR_BARO; // alt baro
                int SS = bmsg[4] & 6; // lower 3,2 bits
                int SAF = bmsg[4] & 1; // lower 1 bit
                GetPositionBaro(bitmsg);

            }
            if (TC == 19)
            {
                MsgType = MSGTYPES.VEL_AIR;
                GetVelocity(bitmsg);

            }
            if (TC >= 20 && TC <= 22)
            {
                // TC 20–22: Airborne Position with GNSS height (HAE). CPR lat/lon layout is identical
                // to TC 9–18. Altitude field (bits 41–52) is raw GNSS height — no Q-bit encoding.
                MsgType = MSGTYPES.POS_AIR_GPS;
                GetPositionGps(bitmsg);
            }
            if (TC >= 23 && TC <= 27)
            {
                MsgType = MSGTYPES.RESERVED;
            }
            if (TC >= 28 && TC <= 29)
            {
                MsgType = MSGTYPES.STATUS;
            }
        }

        public string GetBits(byte[] bmsg)
        {
            //int c = 0;
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bmsg)
            {
                sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
                //Debug.WriteLine($"{c}: {Convert.ToString(b, 2).PadLeft(8,'0')}");
                //c++;
            }
            return sb.ToString();
        }

        public string GetCallSign(string bitmsg)
        {
            // ICAO Annex 10 Vol III §3.1.2.6.7.1: 6-bit character encoding.
            // Index 0 = space (filler/pad); 1–26 = A–Z; 48–57 = 0–9.
            // All undefined/reserved positions map to space per the spec (not '#').
            string LUT = " ABCDEFGHIJKLMNOPQRSTUVWXYZ                     0123456789      ";
            StringBuilder cs = new StringBuilder();
            for (int i = 0; i < 8; i++)
                cs.Append(LUT.Substring(Convert.ToInt16(bitmsg.Substring(40 + i * 6, 6), 2), 1));

            return cs.ToString().TrimEnd(); // strip trailing space pads (callsigns shorter than 8 chars)
        }

        public double GetBaroAltitude(string bitmsg)
        {
            // 12 bits from 41-52, 1 based of bitmsg
            string alt_str_raw = bitmsg.Substring(40, 12);
            bool qbit = alt_str_raw.Substring(7, 1) == "1";
            string alt_str2 = alt_str_raw.Substring(0, 7) + alt_str_raw.Substring(8, 4);
            uint alt = Convert.ToUInt32(alt_str2, 2);

            if (qbit)
                return alt * 25 - 1000; // feet, pressure altitude (25 ft resolution)
            else
                return double.NaN; // Gillham Gray code (Q=0) — not yet decoded; callers must guard against NaN
        }

        public void GetPositionBaro(string bitmsg)
        {

            string T_str = bitmsg.Substring(52, 1); // TIME?
            string F_str = bitmsg.Substring(53, 1); // CPR even/odd frame

            string lat_str_raw = bitmsg.Substring(54, 17);
            string lng_str_raw = bitmsg.Substring(71, 17);

            int frm = F_str == "1" ? 1 : 0;

            double lat_ref = BaseStation.lat;
            double lng_ref = BaseStation.lng;

            uint lat_cpr = Convert.ToUInt32(lat_str_raw, 2);
            double dLAT = 360.0 / (4.0 * 15.0 - frm);
            double lat_rel = lat_cpr / (double)131072;// Math.Pow(2.0, 17);
            double j = Math.Floor(lat_ref / dLAT) + Math.Floor(COMMON.dmod(lat_ref, dLAT) / dLAT - lat_rel + 0.5f); // MOD IS WRONG IN C#
            lat = dLAT * (j + lat_rel);

            uint lng_cpr = Convert.ToUInt32(lng_str_raw, 2);
            double lng_rel = lng_cpr / (double)131072; // Math.Pow(2.0, 17);
            double dLNG = 360.0 / Math.Max(nZoneLong(lat) - frm, 1); // 
            //double dLNG = 360.0 / nZoneLong(lat);

            double m = Math.Floor(lng_ref / dLNG) + Math.Floor(COMMON.dmod(lng_ref, dLNG) / dLNG - lng_rel + 0.5f);

            lng = dLNG * (m + lng_rel);

            alt_ft = GetBaroAltitude(bitmsg);
        }


        /// <summary>
        /// TC 20–22: GNSS height field (bits 41–52, 0-indexed). Unlike the baro altitude field,
        /// all 12 bits are data — there is no Q-bit. Encoding: alt_ft = raw × 25 − 1000.
        ///
        /// Offset note: ICAO Doc 9684 §3.1.2.6.7.4 specifies the same −1000 ft bias as the baro
        /// formula. Some third-party decoders omit this offset. At FL350 the difference is
        /// negligible (~0.28%); near the surface a mismatched offset produces ~300 m apparent error.
        /// If cross-checking against another tool, confirm which convention it uses.
        /// </summary>
        public double GetGnssAltitude(string bitmsg)
        {
            // 12 bits, 0-indexed positions 40–51 (same window as the baro alt field in TC 9–18)
            uint raw = Convert.ToUInt32(bitmsg.Substring(40, 12), 2);
            return raw * 25.0 - 1000.0; // feet, WGS-84 HAE
        }

        /// <summary>TC 20–22: CPR lat/lon decode (identical layout to TC 9–18) plus GNSS altitude.</summary>
        public void GetPositionGps(string bitmsg)
        {
            string F_str   = bitmsg.Substring(53, 1);
            string lat_str = bitmsg.Substring(54, 17);
            string lng_str = bitmsg.Substring(71, 17);

            int    frm     = F_str == "1" ? 1 : 0;
            double lat_ref = BaseStation.lat;
            double lng_ref = BaseStation.lng;

            uint   lat_cpr = Convert.ToUInt32(lat_str, 2);
            double dLAT    = 360.0 / (4.0 * 15.0 - frm);
            double lat_rel = lat_cpr / 131072.0;
            double j       = Math.Floor(lat_ref / dLAT) + Math.Floor(COMMON.dmod(lat_ref, dLAT) / dLAT - lat_rel + 0.5f);
            lat            = dLAT * (j + lat_rel);

            uint   lng_cpr = Convert.ToUInt32(lng_str, 2);
            double lng_rel = lng_cpr / 131072.0;
            double dLNG    = 360.0 / Math.Max(nZoneLong(lat) - frm, 1);
            double m       = Math.Floor(lng_ref / dLNG) + Math.Floor(COMMON.dmod(lng_ref, dLNG) / dLNG - lng_rel + 0.5f);
            lng            = dLNG * (m + lng_rel);

            alt_ft = GetGnssAltitude(bitmsg);
        }

        /// <summary>
        /// TC 5–8 Movement field → ground speed in knots (lower bound of each encoded range).
        /// Table per ICAO Doc 9684 §3.1.2.6.7.3 Table 3-10.
        /// Returns NaN for value 0 (no information) and values 125–127 (reserved).
        /// </summary>
        private double GetSurfaceSpeed_kts(int movement)
        {
            if (movement == 0)   return double.NaN; // no information available
            if (movement == 1)   return 0.0;        // stopped: V < 0.125 kt
            if (movement <= 8)   return (movement - 1) * 0.125;           // 0.125–0.875 kt  (0.125 kt steps)
            if (movement <= 12)  return 1.0  + (movement - 9)   * 0.25;   // 1.00–1.75  kt  (0.25  kt steps)
            if (movement <= 38)  return 2.0  + (movement - 13)  * 0.5;    // 2.0–14.5   kt  (0.5   kt steps)
            if (movement <= 93)  return 15.0 + (movement - 39);            // 15–69       kt  (1.0   kt steps)
            if (movement <= 108) return 70.0 + (movement - 94)  * 2.0;    // 70–98       kt  (2.0   kt steps)
            if (movement <= 123) return 100.0 + (movement - 109) * 5.0;   // 100–170     kt  (5.0   kt steps)
            if (movement == 124) return 175.0;                             // ≥175 kt
            return double.NaN;                                             // 125–127: reserved
        }

        /// <summary>
        /// TC 5–8 Surface Position decode. ME bit layout (0-indexed string positions):
        ///   37–43: Movement (7 bits) — encoded surface speed; see GetSurfaceSpeed_kts()
        ///   44:    Heading status bit (1 = heading valid, 0 = not available)
        ///   45–51: Heading value (7 bits); decoded = value × 360/128°, true North
        ///   52:    Time flag (T)
        ///   53:    CPR frame (F: 0=even, 1=odd)
        ///   54–70: Lat-CPR (17 bits) — identical layout to TC 9–18
        ///   71–87: Lon-CPR (17 bits) — identical layout to TC 9–18
        ///
        /// Altitude: TC 5–8 carries no altitude field. alt_ft is set to BaseStation.alt in feet
        /// as the best available ground-level approximation. For airports at significantly different
        /// elevation from the base station, supply the airport elevation through a separate mechanism.
        /// </summary>
        public void GetPositionSurface(string bitmsg)
        {
            // Ground speed from Movement field
            int movement = Convert.ToInt32(bitmsg.Substring(37, 7), 2);
            speed_kts = GetSurfaceSpeed_kts(movement);

            // True heading (valid only when status bit set)
            bool hdgValid = bitmsg.Substring(44, 1) == "1";
            heading_deg = hdgValid
                ? Convert.ToInt32(bitmsg.Substring(45, 7), 2) * (360.0 / 128.0)
                : double.NaN;

            // CPR lat/lon — identical decode to TC 9–18
            string F_str   = bitmsg.Substring(53, 1);
            string lat_str = bitmsg.Substring(54, 17);
            string lng_str = bitmsg.Substring(71, 17);

            int    frm     = F_str == "1" ? 1 : 0;
            double lat_ref = BaseStation.lat;
            double lng_ref = BaseStation.lng;

            uint   lat_cpr = Convert.ToUInt32(lat_str, 2);
            double dLAT    = 360.0 / (4.0 * 15.0 - frm);
            double lat_rel = lat_cpr / 131072.0;
            double j       = Math.Floor(lat_ref / dLAT) + Math.Floor(COMMON.dmod(lat_ref, dLAT) / dLAT - lat_rel + 0.5f);
            lat            = dLAT * (j + lat_rel);

            uint   lng_cpr = Convert.ToUInt32(lng_str, 2);
            double lng_rel = lng_cpr / 131072.0;
            double dLNG    = 360.0 / Math.Max(nZoneLong(lat) - frm, 1);
            double m       = Math.Floor(lng_ref / dLNG) + Math.Floor(COMMON.dmod(lng_ref, dLNG) / dLNG - lng_rel + 0.5f);
            lng            = dLNG * (m + lng_rel);

            // No altitude field in surface messages — use base station elevation as approximation
            alt_ft = BaseStation.alt / 0.3048; // metres → feet
        }

        public int nZoneLong(double lat)
        {
            // At |lat| >= 87° cos²(lat) → 0, r → ∞, arccos(r) = NaN → return 1 (single zone, polar)
            if (Math.Abs(lat) >= 87.0) return 1;

            double nz = 15; // NZ = number of latitude zones between equator and pole (Mode S fixed at 15)
            double a = 1 - Math.Cos(Math.PI / (2 * nz));
            double b = Math.Cos(Math.PI / 180.0 * lat);

            double r = 1.0 - a / (b * b);
            r = Math.Max(-1.0, Math.Min(1.0, r)); // clamp to valid arccos domain to prevent NaN near poles
            double c = Math.Acos(r);

            int q = (int)Math.Floor(2.0 * Math.PI / c);
            return q;
        }

        public void GetVelocity(string bitmsg)
        {
            string ST_str = bitmsg.Substring(37, 3); // SUB TYPE
            string IC_flag_str = bitmsg.Substring(40, 1); // Intent Change flag (bit 41, string index 40)
            string IFR_flag_str = bitmsg.Substring(41, 1); // IFR Capability flag (bit 42, string index 41)
            string NAV_uncert_str = bitmsg.Substring(42, 3); // Navigation uncertainty category for velocity

            string ST_data_str = bitmsg.Substring(45, 22); // SUB FIELDS

            string VrSrc_str = bitmsg.Substring(67, 1); // Source bit for vertical rate [0: GNSS, 1: Barometer]
            int VrSgn = Convert.ToInt32(bitmsg.Substring(68, 1), 2); // Sign bit for vertical rate [0: Up, 1: Down]
            uint Vr = Convert.ToUInt32(bitmsg.Substring(69, 9), 2); // Vertical rate [All zeros: no information; VR = 64 x (Decimal value - 1); LSB: 64 ft/min]

            string RES_str = bitmsg.Substring(78, 2); // RESERVED

            string SDif_str = bitmsg.Substring(80, 1); // Sign bit for GNSS and Baro altitudes difference [0: GNSS alt above Baro alt; 1: GNSS alt below Baro alt]
            uint dALT = Convert.ToUInt32(bitmsg.Substring(81, 7), 2); // Difference between GNSS and Baro altitudes [All zeros: no information; LSB: 25 ft]

            /*
             * 
             * Subtypes 1 and 2 are used to report ground speeds of aircraft. Subtypes 3 and 4 are
                used to report aircraft true airspeed or indicated airspeed. Reporting of airspeed in
                ADS-B only occurs when aircraft position cannot be determined based on the GNSS
                system. In the real world, subtype 3 messages are very rare.

                Sub-type 2 and 4 are designed for supersonic aircraft. Their message structures are
                identical to subtypes 1 and 3, but with the speed resolution of 4 kt instead of 1 kt.
                However, since there are no operational supersonic airliners currently, there is no
                ADS-B airborne velocity message with sub-type 2 and 4 at this moment.
             * 
             */

            int ST = Convert.ToUInt16(ST_str, 2);

            // Spec: all-zeros = "no information available". Guard before decode to avoid computing negative values.
            vr_fpm      = (Vr   == 0) ? double.NaN : (VrSgn == 0 ? 1 : -1) * 64.0 * (Vr   - 1); // ft/min; NaN when no data
            dalt_gps_ft = (dALT == 0) ? double.NaN : (SDif_str == "0" ? 1 : -1) * 25.0 * (dALT - 1); // ft; NaN when no data

            GetHeadingSpeed(bitmsg);
        }

        public void GetHeadingSpeed(string bitmsg)
        {
            string ST_str = bitmsg.Substring(37, 3); // SUB TYPE
            int ST = Convert.ToInt16(ST_str, 2);

            if (ST == 1 || ST == 2)
            {
                // ── ST 1/2: Ground speed (subsonic / supersonic) ─────────────────────
                // E-W and N-S velocity components, then vector-sum to heading + speed
                string DEW_str = bitmsg.Substring(45, 1);  // 0=Eastward, 1=Westward
                string VEW_str = bitmsg.Substring(46, 10); // E-W speed; 0=no data; value-1 kts (ST1) or 4*(value-1) kts (ST2)
                string DNS_str = bitmsg.Substring(56, 1);  // 0=Northward, 1=Southward
                string VNS_str = bitmsg.Substring(57, 10); // N-S speed; same encoding

                uint VEW_raw = Convert.ToUInt32(VEW_str, 2);
                uint VNS_raw = Convert.ToUInt32(VNS_str, 2);

                if (VEW_raw == 0 || VNS_raw == 0)
                {
                    // all-zeros = no information available for that component
                    speed_kts   = double.NaN;
                    heading_deg = double.NaN;
                    return;
                }

                double scale = (ST == 1) ? 1.0 : 4.0; // ST2 = 4 kt/LSB for supersonic
                double vx = scale * (DEW_str == "0" ?  1 : -1) * (VEW_raw - 1); // East  component, kts
                double vy = scale * (DNS_str == "0" ?  1 : -1) * (VNS_raw - 1); // North component, kts

                speed_kts   = Math.Sqrt(vx * vx + vy * vy);
                heading_deg = COMMON.dmod(Math.Atan2(vx, vy) * 360.0 / (2 * Math.PI), 360.0); // atan2(East,North) → bearing
            }
            else if (ST == 3 || ST == 4)
            {
                // ── ST 3/4: Airspeed (IAS or TAS) ────────────────────────────────────
                // Transmitted only when GNSS is unavailable. Provides magnetic heading + airspeed.
                string HDG_stat = bitmsg.Substring(45, 1);  // 1=heading valid, 0=not available
                string HDG_raw  = bitmsg.Substring(46, 10); // 10-bit heading code; decoded = value × (360/1024)°
                string AS_type  = bitmsg.Substring(56, 1);  // 0=IAS, 1=TAS
                string AS_str   = bitmsg.Substring(57, 10); // 10-bit airspeed; 0=no data; decoded = value-1 kts (ST3) or 4*(value-1) kts (ST4)

                uint AS_raw = Convert.ToUInt32(AS_str, 2);

                if (HDG_stat == "1")
                    heading_deg = Convert.ToUInt32(HDG_raw, 2) * (360.0 / 1024.0); // magnetic heading, degrees
                else
                    heading_deg = double.NaN; // heading not available

                if (AS_raw == 0)
                    speed_kts = double.NaN; // no airspeed information
                else
                {
                    double scale = (ST == 3) ? 1.0 : 4.0;
                    speed_kts = scale * (AS_raw - 1); // IAS or TAS in kts (see AS_type flag)
                }
                // Note: airspeed is not ground speed; heading is magnetic, not true track.
                // Speed_mps from airspeed is an approximation only — wind correction not available.
            }
            else
            {
                // Unknown subtype
                speed_kts   = double.NaN;
                heading_deg = double.NaN;
            }
        }

        public WAKEVORTEXCTGS GetWakeVortexCateg(int TC, int CA)
        {

            switch (TC)
            {
                default:
                case 1:
                    return WAKEVORTEXCTGS.None;
                case 2:
                    switch (CA)
                    {
                        case 1:
                            return WAKEVORTEXCTGS.SURF_EMERG_VEH;
                        case 3:
                            return WAKEVORTEXCTGS.SURF_SERV_VEH;
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                            return WAKEVORTEXCTGS.GROUND_OBS;
                        default:
                            return WAKEVORTEXCTGS.None;
                    }
                case 3:
                    switch (CA)
                    {
                        case 1:
                            return WAKEVORTEXCTGS.SAILPLANE;
                        case 2:
                            return WAKEVORTEXCTGS.BALLOON;
                        case 3:
                            return WAKEVORTEXCTGS.SKYDIVER;
                        case 4:
                            return WAKEVORTEXCTGS.ULTRALIGHT;
                        case 5:
                            return WAKEVORTEXCTGS.RESERVED;
                        case 6:
                            return WAKEVORTEXCTGS.UAV;
                        case 7:
                            return WAKEVORTEXCTGS.SPACE;
                        default:
                            return WAKEVORTEXCTGS.None;
                    }
                    ;
                case 4:
                    switch (CA)
                    {
                        case 1:
                            return WAKEVORTEXCTGS.AC_LIGHT;
                        case 2:
                            return WAKEVORTEXCTGS.AC_MED;
                        case 3:
                            return WAKEVORTEXCTGS.AC_MED;
                        case 4:
                            return WAKEVORTEXCTGS.AC_HVTX;
                        case 5:
                            return WAKEVORTEXCTGS.AC_HEAVY;
                        case 6:
                            return WAKEVORTEXCTGS.AC_HIGHPERF;
                        case 7:
                            return WAKEVORTEXCTGS.AC_ROTOR;
                        default:
                            return WAKEVORTEXCTGS.None;
                    }
                    ;
            }

        }

    }

    public class ADSB2
    {
        public string IP_ADDRESS { get; private set; } = "192.168.1.31";
        public int PORT { get; private set; } = 30002;
        CancellationTokenSource? ts;
        CancellationToken ct;
        public TRACK_TYPES TrackType { get; set; } = TRACK_TYPES.KALMAN_PREDICTED;

        private ConcurrentDictionary<string, trackLOG> trackLogs { get; set; }

        public DateTime LastMsgRxTime { get; set; } = DateTime.UtcNow;
        // Default matches CROSSBOW.BaseStation (canonical); always overridden via constructor when started from Form1.
        private ptLLA BaseStation { get; set; } = new ptLLA(34.4593583, -86.4326550, 174.6); // HAE, WGS-84
        private double3 _BaseStation_ECEF { get; set; } = new double3();
        public enum MSG_LENGTH_BITS
        {
            SMODE = 56,
            ACAS = 112,
        }
        public enum MSG_LENGTH_BYTES
        {
            SMODE = 7,
            ACAS = 14,
        }

        public bool isConnected { get; private set; } = false;

        public double HB_RX_s { get { return HB_RX_ms / 1000.0; } }
        public double HB_RX_ms { get { return (DateTime.UtcNow - LastMsgRxTime).TotalMilliseconds; } }

        public int MSG_SIZE { get; private set; } = 0;
        public int nRECORDS { get; private set; } = 0;

        public ADSB2() { }
        public ADSB2(ConcurrentDictionary<string, trackLOG> _trackLogs, ptLLA _bs, string _ip = "192.168.1.31", int _port = 30002, TRACK_TYPES _trackType = TRACK_TYPES.KALMAN_PREDICTED)
        {
            trackLogs = _trackLogs;
            BaseStation = _bs;
            IP_ADDRESS = _ip;
            PORT = _port;
            TrackType = _trackType;
        }

        //public void UpdateBaseStation(ptLLA _bs)
        //{
        //    BaseStation = new ptLLA(_bs.lat, _bs.lng, _bs.alt);
        //    Geocentric earth = new Geocentric(Ellipsoid.WGS84); //new Geocentric(Constants.WGS84.MajorRadius, Constants.WGS84.Flattening);
        //    (double Xa, double Ya, double Za) = earth.Forward(_bs.lat, _bs.lng, _bs.alt); // alt needs to be in HAE?
        //    _BaseStation_ECEF = new double3(Xa, Ya, Za);

        //    // propograte to any logs?
        //    foreach (KeyValuePair<string, trackLOG> kvp in trackLogs)
        //    {
        //        kvp.Value.BaseStation = new ptLLA(_bs.lat, _bs.lng, _bs.alt);
        //    }
        //}

        /// <summary>
        /// ADS-B ICAOs are exactly 6 uppercase hex characters (e.g. "A1B2C3").
        /// Used to filter stale-track cleanup so ADSB2 only removes its own entries.
        /// </summary>
        private static bool IsAdsbKey(string key) =>
            key.Length == 6 && key.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'));

        public void Start()
        {
            ts = new CancellationTokenSource();
            ct = ts.Token;
            Debug.WriteLine("Starting ADSB2 Listener");
            backgroundTCPRead();
        }
        public void Stop()
        {
            Debug.WriteLine("Stopping ADSB2 Listener");
            ts?.Cancel();
        }

        private void backgroundTCPRead()
        {

            // Start a task - this runs on the background thread...
            Task task = Task.Factory.StartNew(async () =>
            {
                TcpClient tcpclnt = new TcpClient();
                tcpclnt.NoDelay = true;
                Debug.WriteLine("Connecting.....");
                tcpclnt.Connect(IP_ADDRESS, PORT);
                Debug.WriteLine("Connected");
                isConnected = true;
                Debug.WriteLine("Reading: ");
                NetworkStream stm = tcpclnt.GetStream();
                do
                {
                    if (ct.IsCancellationRequested)
                    {
                        // task cancelled — remove only ADS-B tracks, leave ECHO/RADAR/LoRa tracks intact
                        Debug.WriteLine("task canceled, cleaning up ADSB logs");
                        foreach (KeyValuePair<string, trackLOG> aLog in trackLogs.ToList())
                        {
                            if (IsAdsbKey(aLog.Key))
                            {
                                Debug.WriteLine("Removing record " + aLog.Key);
                                trackLogs.TryRemove(aLog.Key, out _);
                            }
                        }

                        tcpclnt.Close();
                        isConnected = false;
                        Debug.WriteLine("Closed");
                        break;
                    }

                    // increase buffer size?
                    byte[] myReadBuffer = new byte[2048];
                    int numberOfBytesRead = await stm.ReadAsync(myReadBuffer, 0, myReadBuffer.Length);
                    // Note: stm.Flush() on a NetworkStream is a no-op and was removed (Issue 15)
                    if (numberOfBytesRead > 0)
                    {

                        //Double elapsedMillisecs = ((TimeSpan)(DateTime.UtcNow - lastMsgRx)).TotalMilliseconds;
                        LastMsgRxTime = DateTime.UtcNow;


                        string msgs = Encoding.ASCII.GetString(myReadBuffer, 0, numberOfBytesRead);

                        int numLines = msgs.Split('\n').Length;
                        //Debug.WriteLine(msgs);

                        MSG_SIZE = numberOfBytesRead;
                        nRECORDS = numLines;

                        foreach (string msg in msgs.Split('\n'))
                        {
                            if (!string.IsNullOrEmpty(msg) && msg.EndsWith(";") && msg.StartsWith("*"))
                            {
                                string pmsg = msg.Replace("*", string.Empty);
                                pmsg = pmsg.Replace(";", string.Empty);

                                byte[] bmsg = Convert.FromHexString(pmsg); // always exact length; BigInteger.ToByteArray() can add a sign byte

                                switch (bmsg.Length)
                                {
                                    default:
                                        // Debug.WriteLine($"Message Not Supported {msg}");
                                        break;
                                    case (int)MSG_LENGTH_BYTES.SMODE:
                                        //Debug.WriteLine($"SMODE MSG RX: {pmsg}->{pmsg}");
                                        break;
                                    case (int)MSG_LENGTH_BYTES.ACAS:
                                        //Debug.WriteLine($"ACAS RX: {pmsg}->{pmsg}");
                                        trackMSG tMsg = new trackMSG(new ADSB_MSG(bmsg, BaseStation));
                                        if (tMsg.ValidMsg)
                                        {
                                            if (trackLogs.TryGetValue(tMsg.ICAO, out var existing))
                                            {
                                                existing.Update(tMsg);
                                            }
                                            else
                                            {
                                                Debug.WriteLine("Adding: " + tMsg.ICAO);
                                                trackLogs.TryAdd(tMsg.ICAO, new trackLOG(tMsg, BaseStation, TrackType));
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                    }

                    // purge stale records — ADS-B ICAOs are 6 uppercase hex chars; only remove our own tracks
                    foreach (KeyValuePair<string, trackLOG> aLog in trackLogs.ToList())
                    {
                        if (IsAdsbKey(aLog.Key) && aLog.Value.TrackAge > 30000)
                        {
                            Debug.WriteLine("Removing stale ADSB record " + aLog.Key + " [" + (aLog.Value.TrackAge / 1000.00).ToString() + " ]");
                            trackLogs.TryRemove(aLog.Key, out _);
                        }
                    }


                }
                while (!ct.IsCancellationRequested);

                Thread.Sleep(100);
                // On stop, remove only ADS-B tracks — leave ECHO, RADAR, LoRa tracks intact
                foreach (KeyValuePair<string, trackLOG> aLog in trackLogs.ToList())
                {
                    if (IsAdsbKey(aLog.Key))
                    {
                        Debug.WriteLine("Removing ADSB record " + aLog.Key);
                        trackLogs.TryRemove(aLog.Key, out _);
                    }
                    Thread.Sleep(1);
                }
                tcpclnt.Close();
                isConnected = false;

            }, ct);
        }
        public string GetBits(byte[] bmsg)
        {
            //int c = 0;
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bmsg)
            {
                sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
                //Debug.WriteLine($"{c}: {Convert.ToString(b, 2).PadLeft(8,'0')}");
                //c++;
            }
            return sb.ToString();
        }


    }

}
