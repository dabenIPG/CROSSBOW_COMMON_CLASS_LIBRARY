using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace CROSSBOW
{
    public class HEL
    {
        public string IP { get; set; } = "192.168.1.13";
        public int Port { get; set; } = 10001;
        private TcpClient _tcp {get;set;}
        private NetworkStream _stream { get; set; }

        private CancellationTokenSource _cts;
        public DateTime lastMsgRx { get; private set; } = DateTime.UtcNow;
        public double HB_RX_ms { get; private set; } = 0;
        public bool isConnected => _tcp?.Connected ?? false;
        public int DropCount { get; private set; } = 0;
        public MSG_IPG IPGMsg { get; private set; } = new MSG_IPG();

        // ── Poll state ───────────────────────────────────────────────────────
        private int _p1 = 0;
        private System.Timers.Timer _pollTimer;

        public HEL() { }

        public async Task Start()
        {
            _cts = new CancellationTokenSource();
            try
            {
                _tcp = new TcpClient();
                await _tcp.ConnectAsync(IP, Port);
                _stream = _tcp.GetStream();
                Debug.WriteLine($"HEL TCP connected  {IP}:{Port}");

                // reset state
                IPGMsg.LaserModel = LASER_MODEL.UNKNOWN;
                _p1 = 0;

                // start background read
                _ = Task.Run(() => BackgroundRead(_cts.Token));

                // sense immediately — RMN then RSN before poll starts
                Send("RMODEL\r");
                await Task.Delay(100);
                Send("RMN\r");
                await Task.Delay(100);
                Send("RSN\r");

                // poll timer — 20 ms matching firmware TICK
                _pollTimer = new System.Timers.Timer(20);
                _pollTimer.Elapsed += (s, e) => Poll();
                _pollTimer.AutoReset = true;
                _pollTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HEL TCP connect failed: {ex.Message}");
                DropCount++;
            }
        }

        public void Stop()
        {
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
            _pollTimer = null;
            _cts?.Cancel();
            _stream?.Close();
            _tcp?.Close();
            _tcp = null;
            _stream = null;
            IPGMsg.LaserModel = LASER_MODEL.UNKNOWN;
            Debug.WriteLine("HEL TCP stopped");
        }

        private async Task BackgroundRead(CancellationToken ct)
        {
            var buf = new byte[256];
            var sb = new StringBuilder();
            try
            {
                while (!ct.IsCancellationRequested && _tcp.Connected)
                {
                    int n = await _stream.ReadAsync(buf, 0, buf.Length, ct);
                    if (n == 0) break;

                    sb.Append(Encoding.ASCII.GetString(buf, 0, n));

                    // responses are CR terminated — process complete lines
                    int idx;
                    while ((idx = sb.ToString().IndexOf('\r')) >= 0)
                    {
                        string line = sb.ToString(0, idx).Trim();
                        sb.Remove(0, idx + 1);
                        if (line.Length > 0)
                            Parse(line);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"HEL read error: {ex.Message}");
                DropCount++;
            }
        }

        private void Poll()
        {
            if (!isConnected || !IPGMsg.IsSensed) return;

            switch (_p1)
            {
                case 0:
                    if (IPGMsg.LaserModel == LASER_MODEL.YLM_3K) Send("RHKPS\r");
                    _p1++; break;
                case 1: Send("RCT\r"); _p1++; break;
                case 2: Send("STA\r"); _p1++; break;
                case 3: Send("RMEC\r"); _p1++; break;
                case 4:
                    if (IPGMsg.LaserModel == LASER_MODEL.YLM_3K) Send("RBSTPS\r");
                    _p1++; break;
                case 5: Send("RCS\r"); _p1++; break;
                case 6: Send("ROP\r"); _p1 = 0; break;
                default: _p1 = 0; break;
            }
        }

        private void Send(string cmd)
        {
            if (_stream == null || !isConnected) return;
            try
            {
                byte[] b = Encoding.ASCII.GetBytes(cmd);
                _stream.Write(b, 0, b.Length);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HEL send error: {ex.Message}");
            }
        }

        public void EMON() { if (IPGMsg.IsSensed) Send("EMON\r"); }
        public void EMOFF() { if (IPGMsg.IsSensed) Send("EMOFF\r"); }
        public void RERR() { if (IPGMsg.IsSensed) Send("RERR\r"); }

        public void SET_POWER(int pct)
        {
            if (!IPGMsg.IsSensed) return;
            string cmd = IPGMsg.LaserModel == LASER_MODEL.YLR_6K
                ? $"SDC {pct}.0\r"
                : $"SCS {pct}\r";
            Send(cmd);
        }

        private void Parse(string line)
        {
            HB_RX_ms = (DateTime.UtcNow - lastMsgRx).TotalMilliseconds;
            lastMsgRx = DateTime.UtcNow;

            //Debug.WriteLine($"HEL RX: {line}");

            int ci = line.IndexOf(':');
            if (ci < 0) return;

            string cmd = line.Substring(0, ci).Trim().ToUpper();
            string payload = line.Substring(ci + 1).Trim();

            IPGMsg.ParseDirect(cmd, payload);
        }

    }

}
