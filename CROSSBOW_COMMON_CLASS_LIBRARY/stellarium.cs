using GeographicLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace CROSSBOW
{
    /// <summary>
    /// Synthetic range constants for Stellarium az/el → LLA conversion.
    /// Astronomical distances from Stellarium are discarded — these practical
    /// ranges are used instead. At any range beyond a few km the az/el dominates
    /// gimbal pointing; the exact range has negligible effect on accuracy.
    /// </summary>
    public static class StellariumRange
    {
        /// <summary>Default — general use. Well within geodesic math comfort zone.</summary>
        public const double NEAR_KM = 10.0;

        /// <summary>Low Earth Orbit — ISS altitude (~400 km).</summary>
        public const double LEO_KM = 400.0;

        /// <summary>Geosynchronous orbit (~35,786 km).</summary>
        public const double GEO_KM = 35_786.0;

        /// <summary>Lunar distance (~384,400 km).</summary>
        public const double MOON_KM = 384_400.0;
    }

    public class STELLARIUM
    {
        public string IP_ADDRESS { get; private set; } = "localhost";
        public int PORT { get; private set; } = 8090;
        public string? Name { get; private set; } = "NA";
        public string? ObjectType { get; private set; } = "NA";
        public double Altitude { get; private set; } = 0;
        public double Azimuth { get; private set; } = 0;
        public double Range_km { get; private set; } = 0;
        public double Speed_mps { get; private set; } = 0;
        public DateTime LastMsgRxTime { get; private set; } = DateTime.UtcNow;
        public double HB_RX_s { get { return HB_RX_ms / 1000.0; } }
        public double HB_RX_ms { get { return (DateTime.UtcNow - LastMsgRxTime).TotalMilliseconds; } }

        // ADD after Speed_mps property:
        /// <summary>
        /// Synthetic range used to convert Stellarium az/el into a trackLOG LLA.
        /// Astronomical distances are truncated — at any range beyond a few km
        /// the az/el dominates pointing; exact range has negligible effect.
        /// </summary>
        public double SyntheticRange_km { get; set; } = StellariumRange.NEAR_KM;

        public bool isConnected { get; private set; } = false;
        
        private ConcurrentDictionary<string, trackLOG>? _trackLogs;
        private ptLLA _baseStation = new ptLLA(34.4593583, -86.4326550, 174.6);
        public const string TRACK_KEY = "STELLA";

        // Issue 40: single static HttpClient instance — reused across polls, avoids socket exhaustion
        private static readonly HttpClient _http = new HttpClient();

        CancellationTokenSource? ts;
        CancellationToken ct;

        public STELLARIUM() { }
        public STELLARIUM(string _ip = "localhost", int _port = 8090)
        {
            IP_ADDRESS = _ip;
            PORT = _port;
        }
        public STELLARIUM(string _ip, int _port,
                  ConcurrentDictionary<string, trackLOG> trackLogs,
                  ptLLA baseStation,
                  double syntheticRange_km = StellariumRange.NEAR_KM)
        {
            IP_ADDRESS = _ip;
            PORT = _port;
            _trackLogs = trackLogs;
            _baseStation = baseStation;
            SyntheticRange_km = syntheticRange_km;
        }

        public void Start()
        {
            ts = new CancellationTokenSource();
            ct = ts.Token;
            Debug.WriteLine("Starting STELLARIUM Listener");
            bgJSONFetch();
        }
        public void Stop()
        {
            Debug.WriteLine("Stopping STELLARIUM Listener");
            ts?.Cancel();
        }
        public void UpdateBaseStation(ptLLA bs)
        {
            _baseStation = new ptLLA(bs.lat, bs.lng, bs.alt);
        }

        private ptLLA ToSyntheticLLA()
        {
            double range_m = SyntheticRange_km * 1000.0;
            double az_rad = Azimuth * Math.PI / 180.0;
            double el_rad = Altitude * Math.PI / 180.0;  // Altitude = elevation in Stellarium

            // Az/el → NED components
            // NED: x=North, y=East, z=Down
            double dN = range_m * Math.Cos(el_rad) * Math.Cos(az_rad);
            double dE = range_m * Math.Cos(el_rad) * Math.Sin(az_rad);
            double dD = -range_m * Math.Sin(el_rad);  // negative = up in NED

            return COMMON.ned2lla(new double3(dN, dE, dD), _baseStation);
        }

        private void FeedTrackLog()
        {
            if (_trackLogs == null) return;

            ptLLA syntheticPos = ToSyntheticLLA();

            trackMSG tMsg = new trackMSG(
                TRACK_KEY,
                Name ?? TRACK_KEY,
                syntheticPos,
                new HeadingSpeed(0, Speed_mps));

            if (_trackLogs.TryGetValue(TRACK_KEY, out var existing))
                existing.Update(tMsg);
            else
            {
                Debug.WriteLine($"STELLARIUM Adding: {TRACK_KEY} — {Name} az={Azimuth:F1}° el={Altitude:F1}°");
                _trackLogs.TryAdd(TRACK_KEY, new trackLOG(tMsg, _baseStation, TRACK_TYPES.LATEST));
            }
        }

        private void bgJSONFetch()
        {
            Task task = Task.Factory.StartNew(async () =>
            {
                do
                {
                    if (ct.IsCancellationRequested)
                    {
                        Debug.WriteLine("task canceled, cleaning up logs");
                        break;
                    }

                    try
                    {
                        string url  = $"http://{IP_ADDRESS}:{PORT}/api/objects/info?format=json";
                        string json = await _http.GetStringAsync(url);
                        isConnected = true;
                        LastMsgRxTime = DateTime.UtcNow;
                        JToken? token = JObject.Parse(json);
                        Name       = token.SelectToken("localized-name")?.ToString();
                        ObjectType = token.SelectToken("object-type")?.ToString();
                        Altitude   = Convert.ToDouble(token.SelectToken("altitude"));
                        Azimuth    = Convert.ToDouble(token.SelectToken("azimuth"));
                        Range_km   = Convert.ToDouble(token.SelectToken("distance-km"));
                        Speed_mps  = Convert.ToDouble(token.SelectToken("velocity-kms")) * 1000;
                        
                        FeedTrackLog();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                        Debug.WriteLine("URL NOT VALID. CANCELLING");
                        ts?.Cancel();
                        isConnected = false;
                    }

                    // Issue 38/39 fix: configurable URL; 100 ms yield instead of spin.
                    await Task.Delay(100);

                }
                while (!ct.IsCancellationRequested);
                isConnected = false;

            }, ct);
        }
    }
}
