using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CROSSBOW
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CB
    {
        public enum READY_STATUS
        {
            ALIVE,
            READY,
            WARN,
            ERROR,
            NA,
        }

        public MCC aMCC { get; private set; }
        public BDC aBDC { get; private set; }

        public ADSB2 aADSB { get; set; } = new ADSB2();
        public RADAR aRADAR { get; set; } = new RADAR();

        public RADAR aLORA { get; set; } = new RADAR();


        public int ADSB_IP_PORT { get; set; } = 30002;

        public string ECHO_IP_ADDRESS { get; set; } = "192.168.1.150";
        public int ECHO_PORT { get; set; } = 29982;

        public STELLARIUM aStella { get; set; } = new STELLARIUM();
        public ECHO aECHO { get; set; } = new ECHO();

        public LCH aLCH { get; set; } = new LCH();
        public LCH aKIZ { get; set; } = new LCH();

        public double[] HORIZON { get; set; } = new double[360];
        public List<PointF> HORIZON_LIST { get; set; } = new List<PointF>();
        public List<PointF> HORIZON_LIST_WITH_BUFFER { get; set; } = new List<PointF>();

        public SYSTEM_STATES System_State
        {
            get { return aMCC?.System_State ?? SYSTEM_STATES.OFF; }
            set 
            {
                Last_System_State = value;
                aBDC.SetState(value);
                aMCC.SetState(value);
                Log.Information($"STATE:{Last_System_State.ToString()} -> {value.ToString()}");

            }
        }
        public SYSTEM_STATES Last_System_State { get; private set; } = SYSTEM_STATES.OFF;
        public BDC_MODES BDC_Mode { get; private set; } = BDC_MODES.OFF;
        public BDC_MODES Last_BDC_Mode { get; private set; } = BDC_MODES.OFF;
        public BDC_CAM_IDS Active_CAM { get; private set; } = BDC_CAM_IDS.VIS;
        public BDC_CAM_IDS Last_Active_CAM { get; private set; } = BDC_CAM_IDS.VIS;

        private int _AI_Track_Priority = 0;
        public int AI_Track_Priority
        {
            get { return _AI_Track_Priority; }
            set
            {
                _AI_Track_Priority = Math.Clamp(value, 0, 10);
            }
        }

        // SETTINGS CONTROLLED BY JSON CONFIG FILE LOADED ON START
        [JsonProperty]
        public string SYSTEM_NAME { get; set; } = "CROSSBOW";

        [JsonProperty]
        public string SERIAL_NUMBER { get; set; } = "CBM_0001";

        [JsonProperty]
        public int LASER_MAX_POWER_W { get; set; } = 3000;

        [JsonProperty] 
        public ptLLA BaseStation { get; set; } = new ptLLA(34.4593583, -86.4326550, 174.6);  //MSL, HAE 

        [JsonProperty] 
        public RPY BaseStationAtitude { get; set; } = new RPY();

        [JsonProperty]
        public string ADSB_IP_ADDRESS { get; set; } = "192.168.86.7"; //"192.168.1.31";

        [JsonProperty]
        public string STELLAR_IP_ADDRESS { get; set; } = "192.168.1.8";

        [JsonProperty]
        public Point FSM_HOME { get; set; } = new Point(0, 0);

        [JsonProperty]
        public UInt32 STAGE_HOME { get; set; } = 200;

        [JsonProperty]
        public TRACK_TYPES TrackType { get; private set; } = TRACK_TYPES.KALMAN_PREDICTED;

        [JsonProperty]
        public PID CUE_PID { get; private set; } = new PID();

        [JsonProperty]
        public PID VID_PID { get; private set; } = new PID();


        // LOG BACKING VARIABLE
        public int BATTERY_SOC { get; set; } = 0;

        public string BUILD_VERSION
        {
            get
            {
                System.Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                DateTime buildDate = new FileInfo(Assembly.GetExecutingAssembly().Location).LastWriteTime;
                return $"IPG CROSSBOW MINI 3kW HMI: [v{version} ({buildDate})]";
            }
        }

        public ConcurrentDictionary<string, trackLOG> trackLogs { get; set; } = new ConcurrentDictionary<string, trackLOG>();

        public Stopwatch UpTime { get; private set; } = Stopwatch.StartNew();

        public ushort _fujiFocus {  get { return (ushort)((_fujiFocusH << 8) | _fujiFocusL); } }
        public uint _fujiFocusL { get; set; } = 0;
        public uint _fujiFocusH { get; set; } = 240;
        public void camFocus(bool isFine = true, BDC.BDC_FOCUS_DIR focusDir = BDC.BDC_FOCUS_DIR.FAR )
        {
            if (Active_CAM == BDC_CAM_IDS.VIS)
            {
                if (isFine)
                {
                    _fujiFocusL += 10*(uint)focusDir;
                    _fujiFocusL = Math.Clamp(_fujiFocusL, 0, 255);
                }
                else
                {
                    _fujiFocusH += (uint)focusDir;
                    _fujiFocusH = Math.Clamp(_fujiFocusH, 0, 255);
                }
                aBDC.SetAcamFocus(_fujiFocus);
            }
            else
            {
                aBDC.MWIR_BUMP_FOCUS = focusDir;
            }
        }

        private int _maxZoomLevel = 9;
        private int _minZoomLevel = 0;
        public void setZOOM_LEVEL(bool _up)
        {

            int _Z = (Active_CAM == BDC_CAM_IDS.VIS) ? (int)aBDC.LatestMSG.VIS_FOV_ndx : (int)aBDC.LatestMSG.MWIR_FOV_ndx;
            int _rqZ = _up?(_Z+1):(_Z-1);

            if (_minZoomLevel <= _rqZ && _rqZ <= _maxZoomLevel)
            {
                aBDC.SetAcamMag(Convert.ToByte(_rqZ));
            }

            //get 
            //{
            //    switch (Active_CAM)
            //    {
            //        case BDC_CAM_IDS.VIS:
            //            return ;
            //        case BDC_CAM_IDS.MWIR:
            //            return ;
            //        default:
            //            return 0;
            //    }
            //}
            //set
            //{
            //    if (_minZoomLevel <= value && value <= _maxZoomLevel)
            //    {
            //        aBDC.SetAcamMag(Convert.ToByte(value));
            //    }
            //}
        }

        public int getZoomPercent { get { return  (int)( 100.0*((Active_CAM == BDC_CAM_IDS.VIS) ? (double)aBDC.LatestMSG.VIS_FOV_ndx : (double)aBDC.LatestMSG.MWIR_FOV_ndx) / (double)((Active_CAM == BDC_CAM_IDS.VIS) ? (double)5 : (double)9)); } }

        public double CAM_FOV { get { return (Active_CAM == BDC_CAM_IDS.VIS) ? aBDC.LatestMSG.VIS_FOV : aBDC.LatestMSG.MWIR_FOV; } }

        public double CAM_iFOV { get { return CAM_FOV / 1280.0; } }// hard code stream width for now

        public double XBOX_FOV_SCALE { get { return CAM_FOV / 30.0 / 2.0; } } //rate helper by fov scaled to widest of 2 cams?

        public double LOS_Azimuth_deg { get { return aBDC.LatestMSG.gimbalMSG.NED_Azimuth_deg; } }
        public double LOS_Elevation_deg { get { return aBDC.LatestMSG.gimbalMSG.NED_Elevation_deg; } }

        public double SYTEM_HEADING_deg { get { return aMCC.LatestMSG.GNSSMsg.Heading; } }

        public CUE CurrentCUE { get; set; } = new CUE();
        public CB() 
        {
        }
        private ILogger Log { get; set; }
        public CB(ILogger _log)
        {
            Log = _log;
            aMCC = new MCC(Log, TransportPath.A3_External);
            aBDC = new BDC(Log, TransportPath.A3_External);
        }

        /// <summary>
        /// Initialise sensor receivers with this CB reference so they can send 0xAF status
        /// responses. Call once after CB is fully constructed and trackLogs is ready.
        /// Must be called before aRADAR.Start() / aLORA.Start().
        /// </summary>
        public void InitSensors(int radarPort = 10009, int loraPort = 10009,
                                 TRACK_TYPES trackType = TRACK_TYPES.KALMAN_PREDICTED)
        {
            aRADAR = new RADAR(trackLogs, BaseStation, this,
                               radarPort, trackType, "RADAR");

            aLORA  = new RADAR(trackLogs, BaseStation, this,
                               loraPort,  trackType, "LORA");
        }

        public bool VerboseLogEnabled { get; set; } = false;

        public bool PING_STATUS_MCC { get;set; } = false;
        public bool PING_STATUS_TMC { get; set; } = false;
        public bool PING_STATUS_HEL { get; set; } = false;
        public bool PING_STATUS_GNSS { get; set; } = false;

        public bool PING_STATUS_BDC { get; set; } = false;
        public bool PING_STATUS_TRC { get; set; } = false;
        public bool PING_STATUS_FMC { get; set; } = false;
        public bool PING_STATUS_GIM { get; set; } = false;
        public bool PING_STATUS_NTP { get; set; } = false;

        public bool PING_STATUS_ADSB { get; set; }
        public bool MSG_STATUS_ADSB { get; set; } = false;

        public READY_STATUS MCC_STATUS
        {
            get
            {
                if (System_State < SYSTEM_STATES.STNDBY)
                    return PING_STATUS_MCC ? READY_STATUS.ALIVE : READY_STATUS.ERROR;
                return aMCC.LatestMSG.CommHealth;
            }
        }
        public READY_STATUS TMC_STATUS
        {
            get
            {
                if (System_State < SYSTEM_STATES.STNDBY)
                {
                    // ping only
                    return (PING_STATUS_TMC ? READY_STATUS.ALIVE : READY_STATUS.ERROR);
                }
                else if (System_State >= SYSTEM_STATES.STNDBY)
                {
                    return PING_STATUS_TMC ? (aMCC.TMC_STATUS ? READY_STATUS.READY : READY_STATUS.WARN) : READY_STATUS.ERROR;
                }
                else
                    return READY_STATUS.NA;
            }
        }
        public READY_STATUS GNSS_STATUS
        {
            get
            {
                if (System_State < SYSTEM_STATES.STNDBY)
                {
                    // ping only
                    return (PING_STATUS_GNSS ? READY_STATUS.ALIVE : READY_STATUS.ERROR);
                }
                else if (System_State >= SYSTEM_STATES.STNDBY)
                {
                    return PING_STATUS_GNSS ? (aMCC.GPS_STATUS ? READY_STATUS.READY : READY_STATUS.WARN) : READY_STATUS.ERROR;
                }
                else
                    return READY_STATUS.NA;
            }
        }
        public READY_STATUS HEL_STATUS { get { return (READY_STATUS)aMCC.HEL_STATUS; } }

        public READY_STATUS BDC_STATUS
        {
            get
            {
                if (System_State < SYSTEM_STATES.STNDBY)
                    return PING_STATUS_BDC ? READY_STATUS.ALIVE : READY_STATUS.ERROR;
                return aBDC.LatestMSG.CommHealth;
            }
        }
        public READY_STATUS GIM_STATUS
        {
            get
            {
                if (System_State < SYSTEM_STATES.STNDBY)
                {
                    // ping only
                    return (PING_STATUS_GIM ? READY_STATUS.ALIVE : READY_STATUS.ERROR);
                }
                else if (System_State >= SYSTEM_STATES.STNDBY)
                {
                    return PING_STATUS_GIM ? (aBDC.GIM_STATUS ? READY_STATUS.READY : READY_STATUS.WARN) : READY_STATUS.ERROR;
                }
                else
                    return READY_STATUS.NA;
            }
        }
        public READY_STATUS TRC_STATUS
        {
            get
            {
                if (System_State < SYSTEM_STATES.STNDBY)
                {
                    // ping only
                    return (PING_STATUS_TRC ? READY_STATUS.ALIVE : READY_STATUS.ERROR);
                }
                else if (System_State >= SYSTEM_STATES.STNDBY)
                {
                    return PING_STATUS_TRC ? (aBDC.TRC_STATUS ? READY_STATUS.READY : READY_STATUS.WARN) : READY_STATUS.ERROR;
                }
                else
                    return READY_STATUS.NA;
            }
        }
        public READY_STATUS FMC_STATUS
        {
            get
            {
                if (System_State < SYSTEM_STATES.STNDBY)
                {
                    // ping only
                    return (PING_STATUS_FMC ? READY_STATUS.ALIVE : READY_STATUS.ERROR);
                }
                else if (System_State >= SYSTEM_STATES.STNDBY)
                {
                    return PING_STATUS_FMC ? (aBDC.FMC_STATUS ? READY_STATUS.READY : READY_STATUS.WARN) : READY_STATUS.ERROR;
                }
                else
                    return READY_STATUS.NA;
            }
        }
        public bool NTP_STATUS { get { return true; } }

        public Color COLOR_FROM_STATUS(READY_STATUS status)
        {
            switch (status)
            {
                case READY_STATUS.ALIVE:
                    return Color.Blue;
                case READY_STATUS.READY:
                    return Color.Green;
                case READY_STATUS.WARN:
                    return Color.Orange;
                case READY_STATUS.NA:
                    return Color.Gray;
                default:
                case READY_STATUS.ERROR:
                    return Color.Red;
            }
        }
        public READY_STATUS WorstStatus(READY_STATUS a, READY_STATUS b)
        {
            int Rank(READY_STATUS s) => s switch
            {
                READY_STATUS.ERROR => 0,
                READY_STATUS.WARN => 1,
                READY_STATUS.ALIVE => 2,
                READY_STATUS.READY => 3,
                READY_STATUS.NA => 4,
                _ => 5
            };
            return Rank(a) <= Rank(b) ? a : b;
        }

        // this will be on the BDC
        //public void SetState(SYSTEM_STATES rqState)
        //{
        //    Last_System_State = System_State;
        //    // check state logic
        //    System_State = rqState;
        //    aBDC.SetState(System_State);
        //    aMCC.SetState(System_State);
        //}
        public void SetMode(BDC_MODES rqMode)
        {
            // check mode logic
            BDC_Mode = rqMode;
        }

        public void SetActiveCamera(BDC_CAM_IDS _camID)
        {
            Active_CAM = _camID;
            //aBDC.set
            aBDC.SetActiveCamera(Active_CAM);
        }

        public void ModeManager(bool isAdvance, bool isLShoulderPressed = false)
        {
            Last_BDC_Mode = BDC_Mode;
            switch (BDC_Mode)
            {
                case BDC_MODES.OFF:
                    if (isAdvance)
                        BDC_Mode = BDC_MODES.POS;
                    break;
                case BDC_MODES.POS:
                    BDC_Mode = (isAdvance ? (isCUE_FLAG_SET ? BDC_MODES.CUE : (isLShoulderPressed ? BDC_MODES.RATE : BDC_MODES.ATRACK)) : BDC_MODES.OFF);
                    break;
                case BDC_MODES.RATE:
                    BDC_Mode = (isAdvance ? (isCUE_FLAG_SET ? BDC_MODES.CUE : BDC_MODES.ATRACK) : BDC_MODES.POS);
                    break;
                case BDC_MODES.CUE:
                    BDC_Mode = (isAdvance ? BDC_MODES.ATRACK : (isLShoulderPressed ? BDC_MODES.RATE : BDC_MODES.POS));
                    break;
                case BDC_MODES.ATRACK:
                    //BDC_Mode = (isAdvance ? BDC_MODES.FTRACK : (isCUE_FLAG_SET ? BDC_MODES.CUE : (isLShoulderPressed ? BDC_MODES.RATE : BDC_MODES.POS)));
                    // disable FT for now
                    if (!isAdvance)
                        BDC_Mode =  isCUE_FLAG_SET ? BDC_MODES.CUE : (isLShoulderPressed ? BDC_MODES.RATE : BDC_MODES.POS);
                    break;
                case BDC_MODES.FTRACK:
                    if (!isAdvance)
                        BDC_Mode = BDC_MODES.ATRACK;
                    break;
            }


            if (BDC_Mode != Last_BDC_Mode)
            {
                if (BDC_Mode == BDC_MODES.CUE)
                    CUE_TRACK_ENABLED = true; // turn on cue track messenger
                if (Last_BDC_Mode == BDC_MODES.CUE)
                    CUE_TRACK_ENABLED = false; // turn off cue track messenger

                if (BDC_Mode == BDC_MODES.ATRACK)
                    aBDC.SetAcamTrackerEnable(BDC_TRACKERS.MOSSE, true);
                if (Last_BDC_Mode == BDC_MODES.ATRACK && BDC_Mode!= BDC_MODES.FTRACK)
                    aBDC.SetAcamTrackerEnable(BDC_TRACKERS.MOSSE, false);

                // zero out offsets
                if (BDC_Mode == BDC_MODES.CUE)
                    CUE_OFFSET = new PointF(0, 0);
                if (BDC_Mode == BDC_MODES.ATRACK)
                    AT_OFFSET = new Point(0, 0);                          // entry — reset accumulator + send command
                if (Last_BDC_Mode == BDC_MODES.ATRACK && BDC_Mode != BDC_MODES.FTRACK)
                    _atOffset = new Point(0, 0);                          // exit — backing field only, no command
                if (BDC_Mode == BDC_MODES.FTRACK)
                    FT_OFFSET = new Point(0, 0);                          // entry — reset accumulator + send command
                if (Last_BDC_Mode == BDC_MODES.FTRACK)
                    _ftOffset = new Point(0, 0);                          // exit — backing field only, no command

                if (Last_BDC_Mode == BDC_MODES.ATRACK)
                {
                    TRACK_GATE_CENTER = new Point(640, 360);
                    TRACK_GATE_SIZE = new Size(255, 255);
                    AI_Track_Priority = 0;
                    aBDC.SetAITrackPriority(Convert.ToByte(AI_Track_Priority));
                }

                aBDC.SetMode(BDC_Mode);
                Log.Information($"MODE->{BDC_Mode}");

            }

        }

        public void ResetTrackB()
        {
            aBDC.ResetTrackB();
            AT_OFFSET = new Point(0, 0);    // reset local accumulator — no separate send needed,
            FT_OFFSET = new Point(0, 0);    // FT_OFFSET = new Point(0, 0); // uncomment when FTRACK is active
        }

        private bool _isCueFlagSet = false;
        public bool isCUE_FLAG_SET
        {
            get { return _isCueFlagSet; }
            set
            {
                _isCueFlagSet = value;
                aBDC.EnableCUEFlag = _isCueFlagSet;
                Log.Information($"CUE FLAG SET->{_isCueFlagSet}");

            }
        }
        public bool isCUE_TRACK_ENABLED { get; set; } = false;
        public bool CUE_TRACK_ENABLED
        {
            get { return isCUE_TRACK_ENABLED; }
            set
            {
                if (BDC_Mode == BDC_MODES.CUE && value && isCUE_FLAG_SET)
                {
                    ts_cue = new CancellationTokenSource();
                    ct_cue = ts_cue.Token;
                    isCUE_TRACK_ENABLED = true;
                    BG_CUE_TASK();
                }
                else
                {
                    isCUE_TRACK_ENABLED = false;
                    ts_cue.Cancel();
                }
                Log.Information($"CUE TRACK ENABLED->{isCUE_TRACK_ENABLED}");
            }

        }

        private Size _TrackGateSize = new Size(255, 255);
        private Point _TrackGateCenter = new Point(640, 360);
        public Size TRACK_GATE_SIZE
        { 
            get { return _TrackGateSize; }
            set
            {
                _TrackGateSize = new Size((int) value.Width, (int) value.Height);
                aBDC.SetTrackGateSize(_TrackGateSize);
            }
        }

        public Point TRACK_GATE_CENTER
        {
            get { return _TrackGateCenter; }
            set
            {
                _TrackGateCenter = new Point((int)value.X, (int)value.Y);
                aBDC.SetTrackGateCenter(_TrackGateCenter);
            }
        }
        private PointF _cueOffset = new PointF();
        public PointF CUE_OFFSET
        {
            get { return _cueOffset; }
            set 
            { 
                _cueOffset = new PointF(value.X, value.Y);
                aBDC.SetCUEOffset(_cueOffset.X, _cueOffset.Y);
            }
        }

        private Point _atOffset = new Point();
        public Point AT_OFFSET
        {
            get { return _atOffset; }
            set
            {
                _atOffset = new Point(value.X, value.Y);
                aBDC.SetATOffset((sbyte)_atOffset.X, (sbyte)_atOffset.Y);
            }
        }
        private Point _ftOffset = new Point();
        public Point FT_OFFSET
        {
            get { return _ftOffset; }
            set
            {
                _ftOffset = new Point(value.X, value.Y);
                aBDC.SetFTOffset((sbyte)_ftOffset.X, (sbyte)_ftOffset.Y);
            }
        }
        private CancellationTokenSource ts_cue { get; set; } = new CancellationTokenSource();
        private CancellationToken ct_cue { get; set; }
        private void BG_CUE_TASK()
        {

            // Start a task - this runs on the background thread...
            Task task = Task.Run(() =>
            {
                Debug.WriteLine("CUE MESSENGER STARTED");
                do
                {
                    if (ct_cue.IsCancellationRequested)
                    {
                        // another thread decided to cancel
                        Debug.WriteLine("CUE MESSENGER CANCELLED");
                        break;
                    }
                    // send CUE angles to bdc?

                    Single bearing = (Single)CurrentCUE.Bearing;
                    //if (bearing > 180)
                    //    bearing = 360 - bearing;

                    Single elevation = (Single)CurrentCUE.Elevation;

                    aBDC.SetPIDCUETargetNED(bearing, elevation, 1);
                    Thread.Sleep(50);
                }
                while (!ct_cue.IsCancellationRequested);
                Debug.WriteLine("CUE TRACK MSGENR END");
            }, ct_cue);
        }

        
        private bool _mwir_color_flag = false;
        public bool MWIR_COLOR_FLAG
        {
            get { return _mwir_color_flag; }
            set
            {
                _mwir_color_flag = value;
                aBDC.MWIR_WhiteHot = _mwir_color_flag;
            }
        }

        public void UpdateBaseStation(ptLLA _bs)
        {
            foreach (KeyValuePair<string, trackLOG> kvp in trackLogs)
            {
                //kvp.Value.BaseStation = new ptLLA(_bs.lat, _bs.lng, _bs.alt);
                kvp.Value.UpdateBaseStation(_bs);
            }

        }

        public void UpdateTrackType(TRACK_TYPES _trackType)
        {
            TrackType = _trackType;
            aADSB.TrackType = _trackType;
            aRADAR.TrackType = _trackType;
            
            // set defaults for all?

            foreach (KeyValuePair<string, trackLOG> kvp in trackLogs)
            {
                //kvp.Value.BaseStation = new ptLLA(_bs.lat, _bs.lng, _bs.alt);
                if (!kvp.Value.ICAO.StartsWith("SRVY_"))
                    kvp.Value.TrackType = _trackType;
            }
        }

        public void SaveConfig(string filePath)
        {
            string json = JsonConvert.SerializeObject(BaseStation, Formatting.Indented);
            File.WriteAllText(filePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        public void LoadConfig(string filePath)
        {
            JObject o1 = JObject.Parse(File.ReadAllText(filePath));

            foreach (var x in o1)
            {
                switch (x.Key)
                {
                    case "SERIAL_NUMBER":
                        SERIAL_NUMBER = x.Value.ToString();
                        break;
                    case "ADSB_IP_ADDRESS":
                        if (x.Value !=null)
                            ADSB_IP_ADDRESS = x.Value.ToString();
                        break;
                    case "BaseStation":
                        double lat = (double)o1["BaseStation"]["lat"];
                        double lng = (double)o1["BaseStation"]["lng"];
                        double alt = (double)o1["BaseStation"]["alt"];
                        BaseStation = new ptLLA(lat, lng, alt);
                        break;
                    case "BaseStationAtitude":
                        double roll = (double)o1["BaseStationAtitude"]["roll"];
                        double pitch = (double)o1["BaseStationAtitude"]["pitch"];
                        double yaw = (double)o1["BaseStationAtitude"]["yaw"];
                        BaseStationAtitude = new RPY(roll, pitch, yaw);
                        break;
                    case "TrackType":
                        Enum.TryParse(x.Value.ToString(), out TRACK_TYPES aTT);
                        break;
                    case "FSM_HOME":
                        {
                            string[] xy = o1["FSM_HOME"].ToString().Split(',');
                            FSM_HOME = new Point(int.Parse(xy[0]), int.Parse(xy[1]));
                        }
                        break;
                    case "STAGE_HOME":
                        STAGE_HOME = (uint)x.Value;
                        break;
                    case "CUE_PID":
                        {
                            CUE_PID.isEnabled =  (bool)o1["CUE_PID"]["isEnabled"];
                            CUE_PID.pkp = (float)o1["CUE_PID"]["pkp"];
                            CUE_PID.pki = (float)o1["CUE_PID"]["pki"];
                            CUE_PID.pkd = (float)o1["CUE_PID"]["pkd"];
                            CUE_PID.tkp = (float)o1["CUE_PID"]["tkp"];
                            CUE_PID.tki = (float)o1["CUE_PID"]["tki"];
                            CUE_PID.tkd = (float)o1["CUE_PID"]["tkd"];
                        }
                        break;
                    case "VID_PID":
                        {
                            VID_PID.isEnabled = (bool)o1["VID_PID"]["isEnabled"];
                            VID_PID.pkp = (float)o1["VID_PID"]["pkp"];
                            VID_PID.pki = (float)o1["VID_PID"]["pki"];
                            VID_PID.pkd = (float)o1["VID_PID"]["pkd"];
                            VID_PID.tkp = (float)o1["VID_PID"]["tkp"];
                            VID_PID.tki = (float)o1["VID_PID"]["tki"];
                            VID_PID.tkd = (float)o1["VID_PID"]["tkd"];
                        }
                        break;
                    default:
                        break;
                }

            }
        }

        public bool isLOS_Within_LCH_Limits { get { return aLCH.NumberTarget == 0 ? false : LOS_Azimuth_deg >= aLCH.Az1 && LOS_Azimuth_deg <= aLCH.Az2 && LOS_Elevation_deg >= aLCH.El1 && LOS_Elevation_deg <= aLCH.El2; } }

    }

    public class PID
    {
        public bool isEnabled { get; set; } = false;
        public float pkp { get; set; } = 0;
        public float pki { get; set; } = 0;
        public float pkd { get; set; } = 0;
        public float tkp { get; set; } = 0;
        public float tki { get; set; } = 0;
        public float tkd { get; set; } = 0;

    }

}
