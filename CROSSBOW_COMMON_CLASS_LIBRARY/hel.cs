using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CROSSBOW
{
    public class HEL
    {
        public string IP { get; private set; } = "192.168.1.13";
        public int Port { get; private set; } = 10011;
        private UdpClient udpClient;
        private IPEndPoint ipEndPoint;
        private CancellationTokenSource ts;
        private CancellationToken ct;
        public DateTime lastMsgRx { get; private set; } = DateTime.UtcNow;
        public double HB_RX_ms { get; private set; } = 0;
        public MSG_IPG IPGMsg { get; private set; } = new MSG_IPG();

        public HEL() { }

        public void Start()
        {
            ts = new CancellationTokenSource();
            ct = ts.Token;
            Debug.WriteLine("Starting IPG Listener");
            backgroundUDPRead();
        }

        public void Stop()
        {
            Debug.WriteLine("Stopping IPG Listener");
            ts.Cancel();
        }
        private async Task backgroundUDPRead()
        {

            // Start a task - this runs on the background thread...
            Task task = Task.Factory.StartNew(async () =>
            {
                udpClient = new UdpClient(Port);
                ipEndPoint = new IPEndPoint(IPAddress.Parse(IP), Port);
                udpClient.Connect(ipEndPoint);
                Debug.WriteLine("IPG UDP Connected");
                do
                {
                    if (ct.IsCancellationRequested)
                    {
                        // another thread decided to cancel
                        Debug.WriteLine("IPG UDP CANCELLED");

                        udpClient.Close();
                        Debug.WriteLine("IPG UDP Closed");
                        break;
                    }

                    var res = await udpClient.ReceiveAsync();
                    byte[] rxBuff = res.Buffer;
                    int rxLen = rxBuff.Length;
                    if (rxLen > 0)
                    {                        
                        Parse(rxBuff);
                    }
                }
                while (!ct.IsCancellationRequested);
            }, ct);
        }

        public void RMODEL()
        {
            Byte[] sendBytes = Encoding.ASCII.GetBytes("RMODEL\r");
            udpClient.Send(sendBytes, sendBytes.Length);
        }
        public void RSN()
        {
            Byte[] sendBytes = Encoding.ASCII.GetBytes("RSN\r");
            udpClient.Send(sendBytes, sendBytes.Length);
        }
        public void RHKPS()
        {
            Byte[] sendBytes = Encoding.ASCII.GetBytes("RHKPS\r");
            udpClient.Send(sendBytes, sendBytes.Length);
        }
        public void RBSTPS()
        {
            Byte[] sendBytes = Encoding.ASCII.GetBytes("RBSTPS\r");
            udpClient.Send(sendBytes, sendBytes.Length);
        }
        public void Parse(byte[] rxBuff)
        {
            HB_RX_ms = (DateTime.UtcNow - lastMsgRx).TotalMilliseconds;
            lastMsgRx = DateTime.UtcNow;

            Debug.WriteLine(Encoding.ASCII.GetString(rxBuff));

            // split msg back
            string[] s = Encoding.ASCII.GetString(rxBuff).Split(':', StringSplitOptions.TrimEntries);

            string rsp = s[0];
            string payload = s[1];


            //IPGMsg.ParseRsp(rsp, payload);

            ////switch (cmd)
            ////{
            ////    case ICD.GET_REGISTER1:
            ////        ParseMSG01(msg, ndx);
            ////        break;
            ////    case ICD.GET_REGISTER2:
            ////        ParseMSG02(msg, ndx);
            ////        break;
            ////    default:
            ////        break;
            ////}
        }
    
    }

}
