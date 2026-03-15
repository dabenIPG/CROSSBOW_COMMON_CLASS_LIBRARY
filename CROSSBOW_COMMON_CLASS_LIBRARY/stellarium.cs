using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CROSSBOW
{
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

        public bool isConnected { get; private set; } = false;

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
