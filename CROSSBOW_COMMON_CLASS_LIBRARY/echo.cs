#pragma warning disable SYSLIB0014  // webclient
using GeographicLib;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CROSSBOW
{
    public class ECHO
    {
        public string IP_ADDRESS { get; private set; } = "192.168.1.150";
        public int PORT { get; private set; } = 29982;
        CancellationTokenSource? ts;
        CancellationToken ct;
        public TRACK_TYPES TrackType { get; set; } = TRACK_TYPES.KALMAN_PREDICTED;
        private ConcurrentDictionary<string, trackLOG> trackLogs { get; set; }
        // Default matches CROSSBOW.BaseStation (canonical); always overridden via constructor when started from Form1.
        private ptLLA BaseStation { get; set; } = new ptLLA(34.4593583, -86.4326550, 174.6); // HAE, WGS-84
        private double3 _BaseStation_ECEF { get; set; } = new double3();

        public DateTime LastMsgRxTime { get; set; } = DateTime.UtcNow;

        public double HB_RX_s { get { return HB_RX_ms / 1000.0; } }
        public double HB_RX_ms { get { return (DateTime.UtcNow - LastMsgRxTime).TotalMilliseconds; } }

        public bool isConnected { get; private set; } = false;

        Geocentric earth = new Geocentric(Ellipsoid.WGS84); //new Geocentric(Constants.WGS84.MajorRadius, Constants.WGS84.Flattening);

        long lastTick = 0;

        UdpClient client;

        public ECHO() { }
        public ECHO(ConcurrentDictionary<string, trackLOG> _trackLogs, ptLLA _bs, string _ip = "192.168.1.150", int _port = 29982, TRACK_TYPES _trackType = TRACK_TYPES.KALMAN_PREDICTED)
        {
            trackLogs = _trackLogs;
            BaseStation = _bs;
            IP_ADDRESS = _ip;
            PORT = _port;
            TrackType = _trackType;
        }

        public void Start()
        {
            ts = new CancellationTokenSource();
            ct = ts.Token;
            Debug.WriteLine("Starting ECHODYNE Listener");
            backgroundTCPRead();

            //client = new UdpClient();
            //string tIP = "192.168.1.8";

            //IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(tIP), 10032);
            //client.Connect(ipEndPoint);

        }
        public void Stop()
        {
            Debug.WriteLine("Stopping ECHODYNE Listener");
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
                        // task cancelled — remove only ECHO tracks, leave ADS-B/RADAR/LoRa tracks intact
                        Debug.WriteLine("task canceled, cleaning up ECHO logs");
                        foreach (KeyValuePair<string, trackLOG> aLog in trackLogs.ToList())
                        {
                            if (aLog.Key.StartsWith("ECH_"))
                            {
                                Debug.WriteLine("Removing ECHO record " + aLog.Key);
                                trackLogs.TryRemove(aLog.Key, out _);
                            }
                        }
                        tcpclnt.Close();
                        isConnected = false;
                        Debug.WriteLine("Closed");
                        break;
                    }

                    // Issue 29 fix: TCP does not guarantee one read = one packet.
                    // The Echodyne packet carries its own length at bytes 8–11 (uint32 n_bytes).
                    // Strategy: read a fixed 12-byte header first, extract n_bytes, then read the remainder.
                    // Minimum sanity: n_bytes must be >= 12 and <= 2048; otherwise discard and reconnect.
                    const int HEADER_SIZE = 12; // sync(8) + n_bytes(4)
                    byte[] header = new byte[HEADER_SIZE];
                    int headerRead = 0;
                    while (headerRead < HEADER_SIZE)
                    {
                        int n = await stm.ReadAsync(header, headerRead, HEADER_SIZE - headerRead);
                        if (n == 0) goto disconnect; // stream closed
                        headerRead += n;
                    }

                    uint declaredSize = BitConverter.ToUInt32(header, 8);
                    if (declaredSize < HEADER_SIZE || declaredSize > 2048)
                    {
                        Debug.WriteLine($"ECHO: invalid n_bytes={declaredSize}, skipping");
                        continue;
                    }

                    byte[] myReadBuffer = new byte[declaredSize];
                    Buffer.BlockCopy(header, 0, myReadBuffer, 0, HEADER_SIZE);
                    int remaining = (int)declaredSize - HEADER_SIZE;
                    int bodyRead = 0;
                    while (bodyRead < remaining)
                    {
                        int n = await stm.ReadAsync(myReadBuffer, HEADER_SIZE + bodyRead, remaining - bodyRead);
                        if (n == 0) goto disconnect;
                        bodyRead += n;
                    }
                    int numberOfBytesRead = (int)declaredSize;
                    if (numberOfBytesRead > 0)
                    {
                        LastMsgRxTime = DateTime.UtcNow;

                        trackMSG tMsg = new trackMSG(new ECHO_MSG(myReadBuffer));

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
                    }

                    // purge stale ECHO tracks only — do not touch ADS-B, RADAR, or LoRa entries
                    foreach (KeyValuePair<string, trackLOG> aLog in trackLogs.ToList())
                    {
                        if (aLog.Key.StartsWith("ECH_") && aLog.Value.TrackAge > 30000)
                        {
                            Debug.WriteLine("Removing stale ECHO record " + aLog.Key + " [" + (aLog.Value.TrackAge / 1000.00).ToString() + " ]");
                            trackLogs.TryRemove(aLog.Key, out _);
                        }
                    }


                }
                while (!ct.IsCancellationRequested);

                disconnect:
                Thread.Sleep(100);
                // On stop/disconnect, remove only ECHO tracks — leave ADS-B, RADAR, LoRa tracks intact
                foreach (KeyValuePair<string, trackLOG> aLog in trackLogs.ToList())
                {
                    if (aLog.Key.StartsWith("ECH_"))
                    {
                        trackLogs.TryRemove(aLog.Key, out _);
                    }
                    Thread.Sleep(1);
                }
                tcpclnt.Close();
                isConnected = false;

            }, ct);
        }

        
        public byte[] ToArray(string trackID, DateTime trackTime, double lat, double lng, double alt_hae,
                              float vx, float vy, float vz )
        {
            using (var ms = new MemoryStream())
            using (var sw = new BinaryWriter(ms))
            {

                long timeStamp = new DateTimeOffset(trackTime.ToUniversalTime()).ToUnixTimeMilliseconds(); // UTC; was incorrectly ToLocalTime()
                byte tClass = 0x08;
                byte tCMD = 7;
                string tID = trackID;// "12345678".Trim('\0');
                byte[] bID = Encoding.ASCII.GetBytes(tID);

                sw.Write((byte)0xAA);
                sw.Write(timeStamp);
                sw.Write(bID);
                sw.Write(tClass);
                sw.Write((byte)0x01);

                sw.Write((double)lat);
                sw.Write((double)lng);
                sw.Write((float)alt_hae);
                sw.Write((float)vx);
                sw.Write((float)vy);
                sw.Write((float)vz);

                sw.Write((uint)0);
                sw.Write((uint)0);
                sw.Write((uint)0);

                sw.Write((byte)0xAA);
                return ms.ToArray();
            }
        }

    }

    public class ECHO_MSG
    {
        public enum PACKET_TYPES
        {
            STANDARD = 0,
            EXTENDED = 1,
        }
        public enum STATES
        {
            INACTIVE = 0,
            UNCONFIRMED = 1,
            CONFRIMED = 2,
            AMBIGUOUS = 3,
            HANDOFF = 4,
        }
        public enum CAUSE_OF_DEATHS
        {
            NA = 0,
            KILLED_BY_MERGE = 1,
            KILLED_BY_COAST = 2,
            INVALID_STATE = 3,
            TRACKER_STOPPED = 4,
        }
        public enum FORMATION_SOURCES
        {
            INTERNAL = 0,
            HANDOFF = 1,
        }
        public bool ValidMsg { get { return  state == STATES.CONFRIMED; } }

        public string TrackID { get; private set; } = "NA";
        public ulong ID { get; private set; }

        public string Version { get { return $"{vMajor}.{vMinor}.{vPatch}";} }
        public DateTime LastMsgRxTime { get; private set; } = DateTime.UtcNow;
        public DateTime LastUpdateTime { get; private set; } = DateTime.UtcNow;
        public DateTime LastAssocTime { get; private set; } = DateTime.UtcNow;
        public DateTime AqTime { get; private set; } = DateTime.UtcNow;



        public uint radarID { get;private set; }
        
        public PACKET_TYPES packetType { get; private set; } = PACKET_TYPES.STANDARD;
        public STATES state { get; private set; } = STATES.INACTIVE;
        public CAUSE_OF_DEATHS track_cause_of_death { get; private set; } = CAUSE_OF_DEATHS.NA;
        public FORMATION_SOURCES track_formation_source { get; private set; } = FORMATION_SOURCES.INTERNAL;

        public uint n_bytes { get; private set; }
        public uint lifeTime { get; private set; }
        public float Confidence_Level { get; private set; }
        public uint informed_track_update_count { get; private set; }

        public float rcs_est { get; private set; } // dBsm
        public float rcs_est_std { get; private set; } // dBsm

        public bool track_is_focused { get; private set; }

        public float prob_aircraft { get; private set; }
        public float prob_bird { get; private set; }
        public float prob_clutter { get; private set; }
        public float prob_human { get; private set; }
        public float prob_uav_fixedwing { get; private set; }
        public float prob_uav_multirotor { get; private set; }
        public float prob_vehicle { get; private set; }

        public byte[] track_UUID = new byte[16];
        public byte[] handoff_UUID = new byte[16];
        public byte[] track_merge_UUID = new byte[16];

        public float3 POSITION_XYZ { get; private set; } = new float3();
        public float3 VELOCITY_XYZ { get; private set; } = new float3();
        public double3 POSITION_ECEF { get; private set; } = new double3();
        public float3 VELOCITY_ECEF { get; private set; } = new float3();
        public float3 POSITION_ENU { get; private set; } = new float3();
        public float3 VELOCITY_ENU { get; private set; } = new float3();
        public float agl_est { get; private set; }

        private byte vMajor { get; set; }
        private byte vMinor { get; set; }
        private byte vPatch { get; set; }

        public ECHO_MSG()
        { }
        public ECHO_MSG(byte[] msg)
        {
            // check here which message?
            parseExtMsg(msg);
        }

        private void parseExtMsg(byte[] msg)
        {
            // Echodyne packet layout — named offsets replace hard resets (Issue 28).
            // Verified against Echodyne ICD-2. All multibyte fields are little-endian.
            // Velocity components use ENU convention: x=East, y=North, z=Up (Issue 36).
            const int OFFSET_ECEF    = 128; // POSITION_ECEF starts here; sequential parse
                                            //   from byte 104 happens to land at 128 — named for clarity
            const int OFFSET_TIMESTAMPS = 200; // last_update_time/last_assoc_time/acquired_time block

            int ndx = 0;
            string packet_sync = Encoding.ASCII.GetString(msg, ndx, 8); ndx += 8;
            uint nbytes = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(uint);
            vMajor = msg[ndx]; ndx++;
            vMinor = msg[ndx]; ndx++;
            vPatch = msg[ndx]; ndx++;
            byte res = msg[ndx]; ndx++;

            radarID = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(uint);
            packetType = (PACKET_TYPES)msg[ndx]; ndx++;
            state = (STATES)msg[ndx]; ndx++;
            
            ndx += 6; // reserved

            lifeTime = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(uint);
            Confidence_Level = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            informed_track_update_count = BitConverter.ToUInt32(msg, ndx); ndx += sizeof(uint);

            ndx += 8; // reserved

            ID = BitConverter.ToUInt64(msg, ndx); ndx += sizeof(ulong);

            Array.Copy(msg, 56, track_UUID,       0, 16); ndx += 16; // track_UUID       at offset 56
            Array.Copy(msg, 72, handoff_UUID,     0, 16); ndx += 16; // handoff_UUID     at offset 72 (was incorrectly 56)
            Array.Copy(msg, 88, track_merge_UUID, 0, 16); ndx += 16; // track_merge_UUID at offset 88 (was incorrectly 56)

            POSITION_XYZ.x = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            POSITION_XYZ.y = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            POSITION_XYZ.z = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            VELOCITY_XYZ.x = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            VELOCITY_XYZ.y = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            VELOCITY_XYZ.z = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);

            ndx = OFFSET_ECEF;
            POSITION_ECEF.x = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            POSITION_ECEF.y = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            POSITION_ECEF.z = BitConverter.ToDouble(msg, ndx); ndx += sizeof(double);
            VELOCITY_ECEF.x = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            VELOCITY_ECEF.y = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            VELOCITY_ECEF.z = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);

            POSITION_ENU.x = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            POSITION_ENU.y = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            POSITION_ENU.z = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            VELOCITY_ENU.x = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            VELOCITY_ENU.y = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            VELOCITY_ENU.z = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);

            rcs_est = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            rcs_est_std = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);

            track_formation_source = (FORMATION_SOURCES)msg[ndx]; ndx++;
            track_cause_of_death = (CAUSE_OF_DEATHS)msg[ndx]; ndx++;

            track_is_focused = msg[ndx]==1; ndx++;


            //double range = Math.Sqrt(xENU * xENU + yENU * yENU + zENU * zENU);
            //double speed = Math.Sqrt(VxENU * VxENU + VyENU * VyENU + VzENU * VzENU);

            //(double lat, double lng, double alt) = earth.Reverse(xECEF, yECEF, zECEF);
            ndx = OFFSET_TIMESTAMPS;
            long last_update_time = BitConverter.ToInt64(msg, ndx); ndx += sizeof(long); //ns
            long last_assoc_time = BitConverter.ToInt64(msg, ndx); ndx += sizeof(long); //ns
            long acquired_time = BitConverter.ToInt64(msg, ndx); ndx += sizeof(long); //ns 

            if (ValidMsg)
            {
                LastUpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(last_update_time / 1_000_000).UtcDateTime;
                LastAssocTime  = DateTimeOffset.FromUnixTimeMilliseconds(last_assoc_time  / 1_000_000).UtcDateTime; // was incorrectly using last_update_time
                AqTime         = DateTimeOffset.FromUnixTimeMilliseconds(acquired_time    / 1_000_000).UtcDateTime; // was incorrectly using last_update_time
            }
            agl_est = BitConverter.ToSingle(msg, ndx); ndx += sizeof(float);
            prob_aircraft = BitConverter.ToSingle(msg, ndx) * 100.0f; ndx += sizeof(float);
            prob_bird = BitConverter.ToSingle(msg, ndx) * 100.0f; ndx += sizeof(float);
            prob_clutter = BitConverter.ToSingle(msg, ndx) * 100.0f; ndx += sizeof(float);
            prob_human = BitConverter.ToSingle(msg, ndx) * 100.0f; ndx += sizeof(float);
            prob_uav_fixedwing = BitConverter.ToSingle(msg, ndx) * 100.0f; ndx += sizeof(float);
            prob_uav_multirotor = BitConverter.ToSingle(msg, ndx) * 100.0f; ndx += sizeof(float);
            prob_vehicle = BitConverter.ToSingle(msg, ndx) * 100.0f; ndx += sizeof(float);

            string hex = Convert.ToHexString(track_UUID);
            string trackID = $"ECH_{hex.Substring(hex.Length - 4, 4)}";

            //Debug.WriteLine($"{trackID} {prob_uav_multirotor}");

        }

    }

}