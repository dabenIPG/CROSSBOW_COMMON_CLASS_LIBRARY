using GeographicLib;
using MathNet.Numerics.Providers.LinearAlgebra;
using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace CROSSBOW
{
    public class trackLOG
    {
        public TRACK_TYPES TrackType { get; set; } = TRACK_TYPES.LATEST;
        private KALMAN ekf = new KALMAN();
        public string ICAO { get; private set; } = "NA";
        public string CallSign { get; private set; } = "NA";
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
        public SortedList<long, ptLLA> PositionLog { get; set; }
        public SortedList<long, HeadingSpeed> HeadingSpeedLog { get; set; }
        public long TrackAge { get { return (long)(DateTime.UtcNow - LastUpdateTime).TotalMilliseconds; } }
        public int PositionLogCount { get { return PositionLog.Count; } }
        public int HeadingLogCount { get { return HeadingSpeedLog.Count; } }

        public bool GridSync { get; set; } = false;
        public ptLLA BaseStation { get; set; } = new ptLLA(34.4593583, -86.4326550, 174.6); // HAE, WGS-84 — canonical; always overridden via constructor
        private double3 _BaseStation_ECEF { get; set; } = new double3();
        public TRACK_CLASSIFICATION Classification { get; set; } = TRACK_CLASSIFICATION.None;


        #region POS ANGLES
        public long PositionTimeStamp { get { return PositionLog.Count > 0 ? PositionLog.Keys[PositionLog.Count - 1] : 0; } }
        public double Heading_deg { get { return HeadingSpeedLog.Count > 0 ? HeadingSpeedLog.Values[HeadingSpeedLog.Count - 1].heading : 0.0; } }
        public double Speed_mps { get { return HeadingSpeedLog.Count > 0 ? HeadingSpeedLog.Values[HeadingSpeedLog.Count - 1].speed : 0.0; } }

        public ptLLA Position
        {
            get
            {
                switch (TrackType)
                {
                    default:
                    case TRACK_TYPES.LATEST:
                        return LatestPosition;
                    case TRACK_TYPES.PREDICTED:
                        return PredictedPosition;
                    case TRACK_TYPES.FILTERED_LATEST:
                        return FilteredPosition;
                    case TRACK_TYPES.FILTERED_PREDICTED:
                        return FilteredPredictedPosition;
                    case TRACK_TYPES.KALMAN_LATEST:
                        return KalmanLatestPostion;
                    case TRACK_TYPES.KALMAN_PREDICTED:
                        return KalmanPredictedPostion;
                }
            }
        }
        public double Bearing { get { return COMMON.GetBearing(BaseStation, Position); } }
        public double Elevation { get { return COMMON.GetElevation(BaseStation, Position); } }
        public double Rangekm { get { return COMMON.geoDist(BaseStation, Position) / 1000.0; } }

        /// <summary>
        /// 3D slant range from BaseStation to current Position, metres.
        /// Combines geodesic surface distance with altitude delta.
        /// Use for targets with significant elevation angle (e.g. Stellarium).
        /// </summary>
        public double SlantRange_m
        {
            get
            {
                double groundDist = COMMON.geoDist(BaseStation, Position);    // horizontal m
                double altDelta = Position.alt - BaseStation.alt;           // vertical m
                return Math.Sqrt(groundDist * groundDist + altDelta * altDelta);
            }
        }

        public double SlantRange_km => SlantRange_m / 1000.0;

        public ptLLA LatestPosition { get { return PositionLog.Count > 0 ? PositionLog.Values[PositionLog.Count - 1] : new ptLLA(0, 0, 0); } }
        public ptLLA PredictedPosition { get { return COMMON.projectLLA(LatestPosition, TrackAge / 1000.0 * Speed_mps, Heading_deg); } }

        public double LatestBearing { get { return COMMON.GetBearing(BaseStation, LatestPosition); } }
        public double LatestRangekm { get { return COMMON.geoDist(BaseStation, LatestPosition) / 1000.0; } }
        public double LatestElevationAngle { get { return COMMON.GetElevation(BaseStation, LatestPosition); } }


        private ptLLA KalmanPredictedPostion { get { return ekf.PredictedPosition(BaseStation); } }
        private ptLLA KalmanLatestPostion { get { return ekf.LatestPosition(BaseStation); } }

        public double FilterGain { get; set; } = 0.1;
        private ptLLA _filteredPosition { get; set; } = new ptLLA();
        private bool _filteredPositionInitialised = false;

        /// <summary>
        /// Returns the last IIR-filtered position. Updated once per Update() call, not on read.
        /// Accessing this property multiple times per timer tick is safe — no state mutation occurs.
        /// </summary>
        public ptLLA FilteredPosition => _filteredPosition;

        /// <summary>
        /// Apply one step of the complementary (leaky integrator) filter.
        /// Called from Update() exactly once per new position measurement.
        /// </summary>
        private void UpdateFilter(ptLLA latest)
        {
            if (!_filteredPositionInitialised || PositionLog.Count <= 1)
            {
                _filteredPosition = latest;
                _filteredPositionInitialised = true;
                return;
            }
            double _lat = (1.0 - FilterGain) * _filteredPosition.lat + FilterGain * latest.lat;
            double _lng = (1.0 - FilterGain) * _filteredPosition.lng + FilterGain * latest.lng;
            double _alt = (1.0 - FilterGain) * _filteredPosition.alt + FilterGain * latest.alt;
            _filteredPosition = new ptLLA(_lat, _lng, _alt);
        }
        public ptLLA FilteredPredictedPosition { get { return COMMON.projectLLA(FilteredPosition, TrackAge / 1000.0 * Speed_mps, Heading_deg); } }

        #endregion


        public trackLOG()
        {
            PositionLog = new SortedList<long, ptLLA>();
            HeadingSpeedLog = new SortedList<long, HeadingSpeed>();

        }

        public trackLOG(trackMSG aMsg, ptLLA _bs, TRACK_TYPES _trackType = TRACK_TYPES.KALMAN_PREDICTED)
        {
            TrackType = _trackType;
            UpdateBaseStation(_bs);

            // add this message to log
            PositionLog = new SortedList<long, ptLLA>();
            HeadingSpeedLog = new SortedList<long, HeadingSpeed>();
            Update(aMsg, true); // init ekf
        }
        public void Update(trackMSG tMsg, bool isInit = false)
        {
            ICAO = tMsg.ICAO;
            GridSync = false;
            LastUpdateTime = tMsg.LastUpdateTime;
            long timeStamp = new DateTimeOffset(LastUpdateTime).ToUnixTimeMilliseconds();
            switch (tMsg.msgType)
            {
                default:
                case TRACK_MSGTYPES.NA:
                    break;
                case TRACK_MSGTYPES.ID:
                    CallSign = tMsg.CallSign;
                    Classification = tMsg.Classification;
                    break;
                case TRACK_MSGTYPES.POSITION:
                    if (!PositionLog.ContainsKey(timeStamp))
                    {
                        ptLLA _pos = new ptLLA(tMsg.Latitude, tMsg.Longitude, tMsg.Alt_HAE_m);
                        PositionLog.Add(timeStamp, _pos);
                        UpdateFilter(_pos); // update IIR filter once per new position measurement

                        // update Kalman filter
                        if (isInit)
                        {
                            ekf.init(LLA2NED(_pos, new HeadingSpeed()), tMsg.LastUpdateTime);
                        }
                        else
                        {
                            if (PositionLogCount == 1)
                                ekf.init(LLA2NED(_pos, new HeadingSpeed()), tMsg.LastUpdateTime);

                            if (HeadingLogCount > 0)
                                ekf.Update(LLA2NED(_pos, HeadingSpeedLog.Values[HeadingSpeedLog.Count - 1]), tMsg.LastUpdateTime);
                            else
                                ekf.Update(LLA2NED(_pos, new HeadingSpeed()), tMsg.LastUpdateTime);
                        }
                    }
                    break;
                case TRACK_MSGTYPES.VELOCITY:
                    if (!HeadingSpeedLog.ContainsKey(timeStamp))
                    {
                        HeadingSpeedLog.Add(timeStamp, new HeadingSpeed(tMsg.Heading_deg, tMsg.Speed_mps, tMsg.VerticalRate_mps));
                    }
                    break;
                case TRACK_MSGTYPES.POS_VEL:
                    // update CS if not null and not NA
                    if (tMsg.CallSign != null && tMsg.CallSign != "NA")
                        CallSign = tMsg.CallSign;
                    if (!PositionLog.ContainsKey(timeStamp))
                    {
                        Classification = tMsg.Classification;
                        ptLLA _pos = new ptLLA(tMsg.Latitude, tMsg.Longitude, tMsg.Alt_HAE_m);
                        HeadingSpeed _hs = new HeadingSpeed(tMsg.Heading_deg, tMsg.Speed_mps, tMsg.VerticalRate_mps);
                        PositionLog.Add(timeStamp, _pos);
                        UpdateFilter(_pos); // update IIR filter once per new position measurement
                        if (!HeadingSpeedLog.ContainsKey(timeStamp))
                        {
                            HeadingSpeedLog.Add(timeStamp, _hs);
                        }

                        // update Kalman filter
                        if (isInit)
                        {
                            // this is the first instance so init the filter here
                            ekf.init(LLA2NED(_pos, _hs), tMsg.LastUpdateTime);
                        }
                        else
                        {
                            if (PositionLogCount == 1)
                                ekf.init(LLA2NED(_pos, _hs), tMsg.LastUpdateTime);

                            ekf.Update(LLA2NED(_pos, _hs), tMsg.LastUpdateTime);
                        }
                    }

                    break;
            }
        }
        public void UpdateBaseStation(ptLLA _bs)
        {
            BaseStation = new ptLLA(_bs.lat, _bs.lng, _bs.alt);
            Geocentric earth = new Geocentric(Ellipsoid.WGS84); //new Geocentric(Constants.WGS84.MajorRadius, Constants.WGS84.Flattening);
            (double Xa, double Ya, double Za) = earth.Forward(_bs.lat, _bs.lng, _bs.alt); // alt is HAE (WGS-84) — CROSSBOW.BaseStation.alt is documented HAE
            _BaseStation_ECEF = new double3(Xa, Ya, Za);

            // ekf is in ecef
        }
        private MathNet.Numerics.LinearAlgebra.Vector<double> LLA2NED(ptLLA _pos, HeadingSpeed _hs)
        {
            // verified
            double3 NED = COMMON.lla2ned(_pos, BaseStation);

            // Decompose horizontal speed into NED components
            double vn = _hs.speed * Math.Cos(COMMON.deg2rad(_hs.heading)); // North, m/s
            double ve = _hs.speed * Math.Sin(COMMON.deg2rad(_hs.heading)); // East,  m/s

            // Issue 25: _hs.vd is vertical rate positive = up (ENU convention, all sensors).
            // NED Down axis is the negation of Up, so vD_NED = −vd.
            double vd = -_hs.vd; // m/s, positive = descending in NED frame

            return MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfArray(new double[]
                            { NED.x, NED.y, NED.z, vn, ve, vd });
        }

    }
    public class trackMSG
    {
        Geocentric earth = new Geocentric(Ellipsoid.WGS84); //new Geocentric(Constants.WGS84.MajorRadius, Constants.WGS84.Flattening);

        public string ICAO { get; private set; } = "NA";
        public TRACK_MSGTYPES msgType { get; private set; } = TRACK_MSGTYPES.NA;
        public string CallSign { get; private set; } = "NA";
        public string Squawk { get; private set; } = "NA";
        public DateTime LastUpdateTime { get; private set; } = DateTime.UtcNow;
        public bool ValidMsg { get; private set; } = false;
        public bool ToAdd { get; private set; } = false;
        public bool ToDelete { get; private set; } = false;
        public double Latitude { get; private set; } = 0;
        public double Longitude { get; private set; } = 0;
        public double Alt_HAE_m { get; private set; } = 0;
        public double Speed_mps { get; private set; } = 0;
        public double Heading_deg { get; private set; } = 0;
        public double VerticalRate_mps { get; private set; } = 0; // m/s, positive = ascending
        public TRACK_CLASSIFICATION Classification { get; private set; } = TRACK_CLASSIFICATION.None;
        public trackMSG() { }

        public trackMSG(string _icao, string _cs, ptLLA _pt)
        {
            ICAO = _icao;
            CallSign = _cs;
            Latitude = _pt.lat;
            Longitude = _pt.lng;
            Alt_HAE_m = _pt.alt;
            //Speed_mps = _hs.speed;
            //Heading_deg = _hs.heading;
            LastUpdateTime = DateTime.UtcNow;
            msgType = TRACK_MSGTYPES.POSITION;
            ValidMsg = true;
        }
        public trackMSG(string _icao, string _cs, HeadingSpeed _hs)
        {
            ICAO = _icao;
            CallSign = _cs;
            Speed_mps = _hs.speed;
            Heading_deg = _hs.heading;
            LastUpdateTime = DateTime.UtcNow;
            msgType = TRACK_MSGTYPES.VELOCITY; // was incorrectly POSITION — no lat/lon is set in this constructor
            ValidMsg = true;
        }
        public trackMSG(string _icao, string _cs, ptLLA _pt, HeadingSpeed _hs)
        {
            ICAO = _icao;
            CallSign = _cs;
            Latitude = _pt.lat;
            Longitude = _pt.lng;
            Alt_HAE_m = _pt.alt;
            Speed_mps = _hs.speed;
            Heading_deg = _hs.heading;
            LastUpdateTime = DateTime.UtcNow;
            msgType = TRACK_MSGTYPES.POS_VEL;
            Classification = TRACK_CLASSIFICATION.AC_LIGHT;
            ValidMsg = true;
        }

        public trackMSG(ADSB_MSG _amsg)
        {
            ICAO = _amsg.ICAO;
            CallSign = _amsg.CallSign;
            Latitude = _amsg.Latitude;
            Longitude = _amsg.Longitude;
            Alt_HAE_m = _amsg.Alt_HAE_m; // HAE metres; meaning depends on TC:
                                           //   TC 9–18: baro alt + GNSS/baro delta (NaN if delta absent)
                                           //   TC 20–22: direct GNSS HAE (delta not added)
                                           //   TC 5–8:  BaseStation.alt approx (no altitude in surface msg)
            Speed_mps          = _amsg.Speed_mps;
            Heading_deg        = _amsg.Heading_deg;
            VerticalRate_mps   = _amsg.VerticalRate_mps; // m/s, positive = climb (Issues 1 & 2 already fixed in adsb_raw.cs)
            LastUpdateTime = DateTime.UtcNow;
            Classification = (TRACK_CLASSIFICATION)_amsg.WakeVortexCat;
            ValidMsg = _amsg.ValidMsg;

            switch (_amsg.MsgType)
            {
                default:
                    msgType = TRACK_MSGTYPES.NA;
                    break;
                case ADSB_MSG.MSGTYPES.ID:
                    msgType = TRACK_MSGTYPES.ID;
                    break;
                case ADSB_MSG.MSGTYPES.POS_AIR_BARO:
                case ADSB_MSG.MSGTYPES.POS_AIR_GPS:  // TC 20–22: lat/lon correct; GNSS alt now decoded by GetPositionGps()
                case ADSB_MSG.MSGTYPES.POS_SURF:     // TC 5–8: lat/lon + ground speed decoded; alt = BaseStation.alt approx
                    msgType = TRACK_MSGTYPES.POSITION;
                    break;
                case ADSB_MSG.MSGTYPES.VEL_AIR:
                    msgType = TRACK_MSGTYPES.VELOCITY;
                    break;
            }
        }
        public trackMSG(byte[] msg, string _baseICAO = "RADAR", bool vzPositiveUp = true)
        {
            // Decodes EXT_OPS CUE payload (CMD 0xAA) — 62-byte stripped payload.
            // Caller (RADAR.ParseMsg) passes parsed.Payload after ExtOpsFrame.TryParseFrame().
            // Wire format v3.0.2: heading+speed replaces vx/vy NED at bytes [38–45].
            //
            // vzPositiveUp: true  = vz positive means ascending (ENU — use for RADAR/EXT)
            //               false = vz positive means descending (NED — use for LoRa/MAVLink)
            int ndx = 0;

            long timeStamp = BitConverter.ToInt64(msg, ndx); ndx += sizeof(long);  // [0–7]

            byte[] bID = new byte[8];
            Array.Copy(msg, ndx, bID, 0, 8); ndx += 8;                             // [8–15]
            ICAO     = _baseICAO;
            CallSign = Encoding.ASCII.GetString(bID).TrimEnd('\0');

            Classification = (TRACK_CLASSIFICATION)msg[ndx]; ndx++;                // [16]
            byte tCMD = msg[ndx]; ndx++;                                            // [17]

            Latitude  = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);    // [18–25]
            Longitude = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);    // [26–33]
            Alt_HAE_m = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);     // [34–37]

            // v3.0.2: direct heading and speed — no atan2/sqrt needed.
            // HYPERION already holds heading and speed natively from sensor fusion.
            // LLA2NED() decomposes heading+speed → vN/vE internally for Kalman.
            float heading = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float); // [38–41]
            float speed   = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float); // [42–45]
            float vz      = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float); // [46–49]

            Heading_deg      = heading;
            Speed_mps        = speed;
            VerticalRate_mps = vzPositiveUp ? vz : -vz;  // normalise to positive = ascending

            // [50–61] reserved — skip
            ndx += 12;

            LastUpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(timeStamp).UtcDateTime;
            msgType        = TRACK_MSGTYPES.POS_VEL;
            ValidMsg       = true;
        }

        public trackMSG(ECHO_MSG msg)
        {
            //ingest echodyne msg
            ValidMsg = msg.ValidMsg;

            if (ValidMsg)
            {
                //double range = Math.Sqrt(xENU * xENU + yENU * yENU + zENU * zENU);
                //double speed = Math.Sqrt(VxENU * VxENU + VyENU * VyENU + VzENU * VzENU);

                string hex = Convert.ToHexString(msg.track_UUID);
                string trackID = $"ECH_{hex.Substring(hex.Length - 4, 4)}";

                ICAO = trackID;
                CallSign = trackID;

                if (msg.prob_uav_multirotor >= 0.5 || msg.prob_uav_fixedwing >= 0.5)
                { 
                    Classification = TRACK_CLASSIFICATION.UAV;
                }
                else
                {
                    Classification = TRACK_CLASSIFICATION.None;
                }

                (double lat, double lng, double alt) = earth.Reverse(msg.POSITION_ECEF.x, msg.POSITION_ECEF.y, msg.POSITION_ECEF.z);
                Latitude = lat;
                Longitude = lng;
                Alt_HAE_m = alt;//  msg.agl_est;//  alt;

                Speed_mps        = Math.Sqrt(msg.VELOCITY_ENU.x * msg.VELOCITY_ENU.x + msg.VELOCITY_ENU.y * msg.VELOCITY_ENU.y); // 2D horizontal (Issue 32 fix)
                Heading_deg      = Math.Atan2(msg.VELOCITY_ENU.x, msg.VELOCITY_ENU.y) * 180.0 / Math.PI;
                VerticalRate_mps = msg.VELOCITY_ENU.z; // ENU z = Up, m/s, positive = ascending
                LastUpdateTime = msg.LastUpdateTime;
                msgType = TRACK_MSGTYPES.POS_VEL;
            }

        }
       

    }
    public enum TRACK_CLASSIFICATION
    {
        None = 0,
        GROUND_OBS = 3,
        SAILPLANE = 4,
        BALLOON = 5,
        UAV = 8,
        SPACE = 9,
        AC_LIGHT = 10,
        AC_MED = 11,
        AC_HEAVY = 13,
        AC_HIGHPERF = 14,
        AC_ROTOR = 15,
        RESERVED = 16,
    }
    public enum TRACK_MSGTYPES
    {
        NA = 0,
        ID = 1,
        POSITION = 2,
        VELOCITY = 3,
        POS_VEL = 4,
    }
    public enum TRACK_TYPES
    {
        LATEST = 0,
        PREDICTED = 1,
        KALMAN_LATEST = 2,
        KALMAN_PREDICTED = 3,
        FILTERED_LATEST = 4,
        FILTERED_PREDICTED = 5,
    }
}
