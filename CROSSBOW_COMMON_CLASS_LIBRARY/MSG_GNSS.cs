// GNSS_MSG.cs  —  ICD v1.7 session 4 GNSS block
//
// v3.0.2 change: LatestPostion.alt now stores HAE (MSL + Undulation).
//   All consumers receive HAE directly — no geoid correction required at call sites.
//   Altitude_HAE and Altitude_MSL convenience properties added for clarity.
//
// GNSS block layout (78 bytes, MCC REG1 bytes 135–212):
//
//   [0]      GNSS SOLN STATUS       uint8    enum
//   [1]      GNSS POS TYPE          uint8    enum
//   [2]      INS SOLN STATUS        uint8    enum
//   [3]      TERRA STAR SYNC STATE  uint8    enum
//   [4]      SIV                    uint8    satellites in solution
//   [5]      SIS                    uint8    satellites in view
//   [6–13]   GPS Latitude           double   BESTPOS  °
//   [14–21]  GPS Longitude          double   BESTPOS  °
//   [22–29]  GPS Altitude           double   BESTPOS  m
//   [30–33]  GPS Undulation         float    BESTPOS  m
//   [34–37]  GPS Heading            float    2-ant    °
//   [38–41]  GPS Roll               float    INSATTX  °
//   [42–45]  GPS Pitch              float    INSATTX  °
//   [46–49]  GPS Azimuth            float    INSATTX  °
//   [50–53]  GPS Latitude  STDEV    float    BESTPOS  m
//   [54–57]  GPS Longitude STDEV    float    BESTPOS  m
//   [58–61]  GPS Altitude  STDEV    float    BESTPOS  m
//   [62–65]  GPS Heading   STDEV    float    2-ant    °
//   [66–69]  GPS Roll      STDEV    float    INSATTX  °
//   [70–73]  GPS Pitch     STDEV    float    INSATTX  °
//   [74–77]  GPS Azimuth   STDEV    float    INSATTX  °
//
// ParseMsg(byte[] msg, int ndx) → int
//   Called from MCC_MSG.ParseMSG01() with ndx = 135.
//   Reads exactly GNSS_BLOCK_LEN (78) bytes and returns ndx + 78.

using System;

namespace CROSSBOW
{
    public class MSG_GNSS
    {
        // -------------------------------------------------------------------
        // Block size constant — used by MCC_MSG to advance ndx
        // -------------------------------------------------------------------
        public const int GNSS_BLOCK_LEN = 78;

        // -------------------------------------------------------------------
        // Enums — full NovAtel encoding
        // -------------------------------------------------------------------
        public enum SOL_STATUS
        {
            COMPUTED              = 0,
            INSUFFICIENT_OBS      = 1,
            NO_CONVERGENCE        = 2,
            SINGULARITY           = 3,
            COV_TRACE             = 4,
            TEST_DIST             = 5,
            COLD_START            = 6,
            V_H_LIMIT             = 7,
            VARIANCE              = 8,
            RESIDUALS             = 9,
            INTEGRITY_WARNING     = 13,
            PENDING               = 18,
            INVALID_FIX           = 19,
            UNAUTHORIZED          = 20,
            INVALID_RATE          = 22,
        }

        public enum INS_SOL_STATUS
        {
            INS_INACTIVE                  = 0,
            INS_ALIGNING                  = 1,
            INS_HIGH_VARIANCE             = 2,
            INS_SOLUTION_GOOD             = 3,
            INS_SOLUTION_FREE             = 6,
            INS_ALIGNMENT_COMPLETE        = 7,
            DETERMINING_ORIENTATION       = 8,
            WAITING_INITIALPOS            = 9,
            WAITING_AZIMUTH               = 10,
            INITIALIZING_BIASES           = 11,
            MOTION_DETECT                 = 12,
            WAITING_ALIGNMENTORIENTATION  = 14,
        }

        public enum POS_VEL_TYPES
        {
            NONE                     = 0,
            FIXEDPOS                 = 1,
            FIXEDHEIGHT              = 2,
            DOPPLER_VELOCITY         = 8,
            SINGLE                   = 16,
            PSRDIFF                  = 17,
            WAAS                     = 18,
            PROPAGATED               = 19,
            L1_FLOAT                 = 32,
            NARROW_FLOAT             = 34,
            L1_INT                   = 48,
            WIDE_INT                 = 49,
            NARROW_INT               = 50,
            RTK_DIRECT_INS           = 51,
            INS_SBAS                 = 52,
            INS_PSRSP                = 53,
            INS_PSRDIFF              = 54,
            INS_RTKFLOAT             = 55,
            INS_RTKFIXED             = 56,
            EXT_CONSTRAINED          = 67,
            PPP_CONVERGING           = 68,
            PPP                      = 69,
            OPERATIONAL              = 70,
            WARNING                  = 71,
            OUT_OF_BOUNDS            = 72,
            INS_PPP_CONVERGING       = 73,
            INS_PPP                  = 74,
            PPP_BASIC_CONVERGING     = 77,
            PPP_BASIC                = 78,
            INS_PPP_BASIC_CONVERGING = 79,
            INS_PPP_BASIC            = 80,
        }

        public enum TERRA_STAR_SUB_TYPES
        {
            UNASSIGNED                = 0,
            TERM                      = 1,
            MODEL                     = 5,
            BUBBLE                    = 100,
            INCOMPATIBLE_SUBSCRIPTION = 104,
        }

        public enum TERRA_STAR_SYNCH_STATES
        {
            NO_SIGNAL = 0,
            SEARCH    = 1,
            LOCKED    = 2,
        }

        public enum TERRA_STAR_LOCAL_AREA_STATUS
        {
            DISABLED             = 0,
            WAITING_FOR_POSITION = 1,
            RANGE_CHECK          = 16,
            IN_RANGE             = 129,
            OUT_OF_RANGE         = 130,
            POSITION_TOO_OLD     = 255,
        }

        public enum TERRA_STAR_GEOGATING_STATUS
        {
            DISABLED             = 0,
            WAITING_FOR_POSITION = 1,
            ONSHORE              = 129,
            OFFSHORE             = 130,
            POSITION_TOO_OLD     = 255,
            PROCESSING           = 1000,
        }

        public enum GPS_CLOCK_STATUS
        {
            VALID      = 0,
            CONVERGING = 1,
            ITERATING  = 2,
            INVALID    = 3,
        }

        public enum UTC_STATUS
        {
            INVALID = 0,
            VALID   = 1,
            Warning = 2,
        }

        // -------------------------------------------------------------------
        // Properties
        // -------------------------------------------------------------------
        public DateTime lastMsgRx { get; private set; } = DateTime.UtcNow;
        public double   HB_RX_us  { get; private set; } = 1000000;   // default 1 s

        public DateTime         GPS_UTC_TIME      { get; private set; } = DateTime.UtcNow;
        public double           gps_time_dt       { get; private set; } = 0;
        public GPS_CLOCK_STATUS GPS_Clock_Status  { get; private set; } = GPS_CLOCK_STATUS.INVALID;
        public UTC_STATUS       GPS_UTC_Status    { get; private set; } = UTC_STATUS.INVALID;

        public ptLLA   LatestPostion    { get; private set; } = new ptLLA();
        public double3 LatestPostionSTD { get; private set; } = new double3();

        public int SIV { get; private set; } = 0;
        public int SIS { get; private set; } = 0;

        public SOL_STATUS     LatestsolStatus    { get; private set; } = SOL_STATUS.INSUFFICIENT_OBS;
        public POS_VEL_TYPES  LatestPosType      { get; private set; } = POS_VEL_TYPES.NONE;
        public INS_SOL_STATUS LatestINSSolStatus { get; private set; } = INS_SOL_STATUS.INS_INACTIVE;

        private DateTime LastBestPosMsgRx { get; set; } = DateTime.UtcNow;

        public double Roll    { get; private set; } = 0;
        public double Pitch   { get; private set; } = 0;
        public double Azimuth { get; private set; } = 0;

        public double Roll_STDEV    { get; private set; } = 0;
        public double Pitch_STDEV   { get; private set; } = 0;
        public double Azimuth_STDEV { get; private set; } = 0;

        public double Heading       { get; private set; } = 0;
        public double Heading_STDEV { get; private set; } = 0;

        public double Undulation  { get; private set; } = 0;

        /// <summary>
        /// Height Above Ellipsoid — MSL altitude + geoid undulation.
        /// This is the canonical altitude used throughout CROSSBOW (CUE packets,
        /// platform LLA sent to BDC, 0xAB POS/ATT response).
        /// </summary>
        public double Altitude_HAE { get { return LatestPostion.alt; } }

        /// <summary>
        /// MSL altitude as reported by GNSS BESTPOS. Back-calculated from HAE.
        /// Use only when a downstream consumer explicitly requires MSL.
        /// </summary>
        public double Altitude_MSL { get { return LatestPostion.alt - Undulation; } }

        public bool   isConnected { get; private set; } = false;

        public TERRA_STAR_SYNCH_STATES TerraStar_SyncState { get; private set; } = TERRA_STAR_SYNCH_STATES.NO_SIGNAL;

        // -------------------------------------------------------------------
        // ParseMsg — reads 78 bytes starting at ndx, returns updated ndx
        // -------------------------------------------------------------------
        public int ParseMsg(byte[] msg, int ndx)
        {
            LastBestPosMsgRx = DateTime.UtcNow;

            LatestsolStatus     = (SOL_STATUS)             msg[ndx]; ndx++;
            LatestPosType       = (POS_VEL_TYPES)          msg[ndx]; ndx++;
            LatestINSSolStatus  = (INS_SOL_STATUS)         msg[ndx]; ndx++;
            TerraStar_SyncState = (TERRA_STAR_SYNCH_STATES)msg[ndx]; ndx++;

            SIV = msg[ndx]; ndx++;
            SIS = msg[ndx]; ndx++;

            double lat     = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            double lng     = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            double alt_msl = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);

            Undulation = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            Heading    = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            Roll       = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            Pitch      = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            Azimuth    = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);

            float latSTDEV = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            float lngSTDEV = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            float altSTDEV = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            Heading_STDEV  = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            Roll_STDEV     = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            Pitch_STDEV    = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            Azimuth_STDEV  = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);

            // Store HAE — all CROSSBOW consumers use HAE. Undulation already parsed above.
            LatestPostion    = new ptLLA(lat, lng, alt_msl + Undulation);
            LatestPostionSTD = new double3(latSTDEV, lngSTDEV, altSTDEV);

            lastMsgRx = DateTime.UtcNow;
            return ndx;
        }
    }
}
