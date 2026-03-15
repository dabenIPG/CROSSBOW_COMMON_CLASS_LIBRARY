using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CROSSBOW
{
    public class LCH
    {

        public enum FILETYPE
        { 
            KIZ = 0,
            LCH = 1,
        }

        private enum FILECONTENTS 
        { 
            MISSIONID = 37,
            OPERATOR = 38,
            MISSIONNAME = 40,
            MISSIONSTART = 41,
            MISSIONEND = 42,
            AUTHTYPE = 45,
            NTARGETS = 47,
            DATASTART = 51,
        };

        public enum AUTHORIZATION
        { 
            PRACTICE,
            EXECUTION,
        };

        public FILETYPE FileType { get; set; } = FILETYPE.LCH;

        public string FilePath { get; set; } = "NA";
        public string SystemOperator { get; set; } = "IPG";
        
        private ptLLA _systemLocation { get; set; } = new ptLLA();
        public ptLLA SystemLocation
        {
            get { return _systemLocation; }
            set 
            { 
                _systemLocation = value;
                CheckLocation();
            }
        }

        public double Undulation { get; set; } = 0;

        public string MissionID { get; set; } = "NA";
        public string Operator { get; set; } = "IPG";
        public string MissionName { get; set; } = "NA";
        public DateTime MissionStartDateTime { get; set; } = DateTime.UtcNow;
        public DateTime MissionEndDateTime { get; set; } = DateTime.UtcNow;
        public TimeSpan MissionDuration { get { return (MissionEndDateTime - MissionStartDateTime); } }
        public AUTHORIZATION AuthorizationType { get; set; } = AUTHORIZATION.PRACTICE;
        public bool isForExecution { get { return AuthorizationType == AUTHORIZATION.EXECUTION; } }
        public bool isOperatorValid { get { return Operator.Equals(SystemOperator); } }
        public bool isLocationValid { get; private set; } = false;
        public bool WindowVote { get; private set; } = false;
        public bool TotalVote { get { return isForExecution && isOperatorValid && isLocationValid && WindowVote; } }

        public int NumberTarget { get; private set; } = 0;

        public int NumberWindows { get; private set; } = 0;

        public double Az1 { get { return LCH_Targets.Any() ? LCH_Targets.Min(t => t.Az1) : 0; } }
        public double Az2 { get { return LCH_Targets.Any() ? LCH_Targets.Max(t => t.Az2) : 0; } }
        public double El1 { get { return LCH_Targets.Any() ? LCH_Targets.Min(t => t.El1) : 0; } }
        public double El2 { get { return LCH_Targets.Any() ? LCH_Targets.Max(t => t.El2) : 0; } }

        public string Bounds { get { return $"[{Az1.ToString("0.0")},{El1.ToString("0.0")}] [{Az2.ToString("0.0")},{El2.ToString("0.0")}]"; } }
        public List<LCH_TARGET> LCH_Targets { get; private set; } = new List<LCH_TARGET>();

        public LCH() { }
        public LCH(string fname, FILETYPE _fileType, ptLLA _systemLocation, string _systemOperator = "IPG", double _undulation = 0) 
        { 
            FilePath = fname;
            FileType = _fileType;
            SystemLocation = _systemLocation;
            SystemOperator = _systemOperator;
            isLocationValid = false;
            Undulation = _undulation;
            ParseFile();
            CheckLocation();
            //Az1 = LCH_Targets.Min(t => t.Az1);
            //Az2 = LCH_Targets.Max(t => t.Az2);
            //El1 = LCH_Targets.Min(t => t.El1);
            //El2 = LCH_Targets.Max(t => t.El2);
        }

        public int LineFromPos(string[] lines, string pattern)
        {
            int lineNumber = 0;
            foreach (string line in lines)
            {
                if (line.StartsWith(pattern))
                    return lineNumber;
                lineNumber++;
            }
            return -1;

        }
        private void ParseFile()
        { 
            // data starts on line 52

            string[] lines = File.ReadAllLines(FilePath);

            // fimd mission ID
            string pattern0 = @"Mission ID:";
            int n1 = LineFromPos(lines, pattern0);
            int dL = n1 - (int)FILECONTENTS.MISSIONID;

            MissionID = (lines[(int)FILECONTENTS.MISSIONID + dL].Split(":", count: 2, StringSplitOptions.None))[1].Trim();
            Operator = (lines[(int)FILECONTENTS.OPERATOR + dL].Split(":", count: 2, StringSplitOptions.None))[1].Trim(); 
            MissionName = (lines[(int)FILECONTENTS.MISSIONNAME + dL].Split(":", count: 2, StringSplitOptions.None))[1].Trim();
            
            MissionStartDateTime =  DateTime.ParseExact( (lines[(int)FILECONTENTS.MISSIONSTART + dL].Split(":", count:2, StringSplitOptions.None))[1].Trim(), "yyyy MMM dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal); // this does not follow standard
            MissionEndDateTime = DateTime.ParseExact((lines[(int)FILECONTENTS.MISSIONEND + dL].Split(":", count: 2, StringSplitOptions.None))[1].Trim(), "yyyy MMM dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

            AuthorizationType = lines[(int)FILECONTENTS.AUTHTYPE + dL].Split(":", count: 2, StringSplitOptions.None)[1].Trim().Equals("For Execution")? AUTHORIZATION.EXECUTION : AUTHORIZATION.PRACTICE;
            NumberTarget = int.Parse(lines[(int)FILECONTENTS.NTARGETS + dL].Split(":", count: 2, StringSplitOptions.None)[1].Trim());

            string pattern1 = @"YYYY MMM dd (DDD) HHMM SS    YYYY MMM dd (DDD) HHMM SS      MM:SS";
            string pattern2 = @"Elevation Range:";
            //Regex regex = new Regex(@"YYYY MMM dd (DDD) HHMM SS    YYYY MMM dd (DDD) HHMM SS      MM:SS", RegexOptions.Multiline);
            //var matchedLines = lines.Where(line => regex.IsMatch(line)).ToList();
            //var matchedLines2 = lines.Where(line => line.StartsWith(pattern)).ToList();

            int[] DataBlockStart = new int[NumberTarget];   // Line Number where data starts
            int[] DataBlockEnd = new int[NumberTarget];   // Line Number where data starts
            int c1 = 0;
            int c2 = 0;
            int ln = 0;
            foreach (string line in lines)
            {
                if (line.StartsWith(pattern1))
                {
                    DataBlockStart[c1] = ln;
                    c1++;
                }
                if (line.StartsWith(pattern2))
                {
                    DataBlockEnd[c2] = ln;
                    c2++;
                }
                ln++;
            }

            /* DATA STARTS FROM BlockStart+2 to BlockEnd
                YYYY MMM dd (DDD) HHMM SS    YYYY MMM dd (DDD) HHMM SS      MM:SS
                -------------------------    -------------------------    -------
                2025 Apr 28 (088) 0000 01    2025 Apr 28 (088) 0010 45    0010:44               
                2025 Apr 28 (088) 0752 05    2025 Apr 28 (088) 0800 01    0007:56

                Percent = 67.00%

                Source Geometry: (WGS-84)
                ---------------
                Method: Fixed Point
                Latitude:  34.667454 degrees N
                Longitude: 86.466275 degrees W
                Altitude:  0.199 km

                Target Geometry: (WGS-84) 9
                ---------------
                Method: Fixed Field of View
                Azimuth Range:   25 to 30 degrees
                Elevation Range: 6 to 12 degrees
             */

            for (int i = 0; i < NumberTarget; i++)
            {
                LCH_Targets.Add(new LCH_TARGET(DataBlockStart[i], DataBlockEnd[i], lines, Undulation));                
                NumberWindows += LCH_Targets[i].LCH_Windows.Count;
            }

            string foo = "";
        }

        public void CheckLocation()
        {
            isLocationValid = true;
            foreach (LCH_TARGET aTarget in LCH_Targets)
            {
                double d2 = COMMON.haversine(SystemLocation.lat, SystemLocation.lng, SystemLocation.alt, aTarget.Latitude, aTarget.Longitude, aTarget.Altitude);
                isLocationValid = isLocationValid && (d2 <= 10);
            }
        }
        public void CheckLocalVote(DateTime currentTime, PointF LOS)
        {
            WindowVote = false;
            int it = 0;
            foreach (LCH_TARGET aTarget in LCH_Targets)
            {
                bool isInWindow = ((LOS.X >= aTarget.Az1) && (LOS.X <= aTarget.Az2) && (LOS.Y >= aTarget.El1) && (LOS.Y <= aTarget.El2));
                if (isInWindow)
                {
                    // is it in any time slot for this target?
                    bool isInTime = (currentTime.ToUniversalTime() >= aTarget.StartDateTime.ToUniversalTime() && currentTime.ToUniversalTime() <= aTarget.EndDateTime.ToUniversalTime());
                    if (isInTime)
                    {
                        foreach (LCH_WINDOW aWindow in aTarget.LCH_Windows)
                        {
                            WindowVote = (currentTime.ToUniversalTime() >= aWindow.StartDateTime.ToUniversalTime() && currentTime.ToUniversalTime() <= aWindow.EndDateTime.ToUniversalTime());

                            if (WindowVote)
                                break;
                        }
                    }
                }
                it++;
            }
        }

    }
    public class LCH_TARGET
    {
        private enum METALINES // line number from end
        { 
            LAT = 8,
            LNG = 7,
            ALT = 6,
            AZ = 1,
            EL = 0,
        }

        public double Latitude { get; private set; } = 0;
        public double Longitude { get; private set; } = 0;
        public double Altitude { get; private set; } = 0;
        public double Az1 { get; private set; } = 0;
        public double Az2 { get; private set; } = 0;
        public double El1 { get; private set; } = 0;
        public double El2 { get; private set; } = 0;

        public DateTime StartDateTime 
        { 
            get 
            { 
                if (LCH_Windows.Count > 0) 
                    return LCH_Windows[0].StartDateTime; 
                else
                    return DateTime.UtcNow;
            } 
        }
        public DateTime EndDateTime
        {
            get
            {
                if (LCH_Windows.Count > 0)
                    return LCH_Windows[LCH_Windows.Count-1].EndDateTime;
                else
                    return DateTime.UtcNow;
            }
        }
        public TimeSpan Duration { get { return (EndDateTime - StartDateTime); } }

        public List<LCH_WINDOW> LCH_Windows { get; private set; } = new List<LCH_WINDOW>();

        public LCH_TARGET() { }
        public LCH_TARGET(int _start, int _end, string[] _lines, double _Undulation = 0) 
        {
            Latitude = double.Parse(  (_lines[_end - (int)METALINES.LAT].Split(":", count: 2, StringSplitOptions.None)[1].Trim()).Split(" ")[0]  );
            Longitude = double.Parse((_lines[_end - (int)METALINES.LNG].Split(":", count: 2, StringSplitOptions.None)[1].Trim()).Split(" ")[0]);
            Altitude = double.Parse((_lines[_end - (int)METALINES.ALT].Split(":", count: 2, StringSplitOptions.None)[1].Trim()).Split(" ")[0]) * 1000;

            Altitude -= _Undulation; // HAE->MSL

            int NS_FLAG = ((_lines[_end - (int)METALINES.LAT].Split(":", count: 2, StringSplitOptions.None)[1].Trim()).Split(" ")[2]).Equals("N") ? 1 : -1;
            int EW_FLAG = ((_lines[_end - (int)METALINES.LNG].Split(":", count: 2, StringSplitOptions.None)[1].Trim()).Split(" ")[2]).Equals("E") ? 1 : -1;

            Latitude *= NS_FLAG;
            //Longitude *= EW_FLAG;
            Longitude = EW_FLAG == -1 ? Longitude % 180.0 : Longitude;
            Longitude *= EW_FLAG;

            string azLine = _lines[_end - (int)METALINES.AZ].Split(":",count:2, StringSplitOptions.None)[1].Trim();
            string elLine = _lines[_end - (int)METALINES.EL].Split(":", count: 2, StringSplitOptions.None)[1].Trim();

            string[] azs = azLine.Split("to");
            Az1 = double.Parse(azs[0]);
            Az2 = double.Parse(azs[1].Replace("degrees","").Trim());

            string[] els = elLine.Split("to");
            El1 = double.Parse(els[0]);
            El2 = double.Parse(els[1].Replace("degrees", "").Trim());

            for (int i = _start+2; i < _end-14; i++)
            {
                string line = _lines[i];
                LCH_Windows.Add(new LCH_WINDOW(line));

            }
        
        }

        public LCH_TARGET(DataGridViewRow dgvRow, double lat, double lng, double alt)
        {
            Latitude = lat;
            Longitude = lng;
            Altitude = alt;
            Az1 = Convert.ToDouble(dgvRow.Cells["Az1"].Value);
            El1 = Convert.ToDouble(dgvRow.Cells["El1"].Value);
            Az2 = Convert.ToDouble(dgvRow.Cells["Az2"].Value);
            El2 = Convert.ToDouble(dgvRow.Cells["El2"].Value);
            LCH_Windows.Add(new LCH_WINDOW(DateTime.Parse(dgvRow.Cells["SartTime"].Value.ToString()), DateTime.Parse(dgvRow.Cells["StopTime"].Value.ToString())));
        }

        public bool isOpen()
        {
            bool res = false;
            foreach (LCH_WINDOW aWindow in LCH_Windows)
            {
                bool isInTimeWindow = (DateTime.UtcNow.ToUniversalTime() >= aWindow.StartDateTime.ToUniversalTime() && DateTime.UtcNow.ToUniversalTime() <= aWindow.EndDateTime.ToUniversalTime());

                if (isInTimeWindow)
                {
                    res = true;
                    return res;
                }
            }
            return res;
        }

    }
    public class LCH_WINDOW
    {
        public DateTime StartDateTime { get; private set; } = DateTime.UtcNow;
        public DateTime EndDateTime { get; private set; } = DateTime.UtcNow;
        public TimeSpan Duration { get { return (EndDateTime - StartDateTime); } }

        public LCH_WINDOW() { }
        public LCH_WINDOW(string _line) 
        {

            string d1 = _line.Substring(0, 11);
            string t1 = _line.Substring(18, 7);
            string d2 = _line.Substring(29, 11);
            string t2 = _line.Substring(29 + 18, 7);

            StartDateTime = DateTime.ParseExact(d1 +" " + t1, "yyyy MMM dd HHmm ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            EndDateTime = DateTime.ParseExact(d2 + " " + t2, "yyyy MMM dd HHmm ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);


        }
        public LCH_WINDOW(DateTime startDateTime, DateTime endDateTime)
        {
            StartDateTime = startDateTime;
            EndDateTime = endDateTime;
        }
    }

}
