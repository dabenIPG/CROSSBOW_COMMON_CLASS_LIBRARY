using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CROSSBOW
{
    public class TRC
    {

        public CAMERA[] Cameras { get; set; } =
            [
            new CAMERA(BDC_CAM_IDS.VIS),
            new CAMERA(BDC_CAM_IDS.MWIR),
            ];
        public string IP { get; private set; } = IPS.TRC;
        public int Port { get; private set; } = 5010;   // LEGACY — pending deprecation (TRC-M9)

        private UdpClient udpClient;
        private IPEndPoint ipEndPoint;
        private CancellationTokenSource ts;
        private CancellationToken ct;
        public DateTime lastMsgRx { get; private set; } = DateTime.UtcNow;
        public VERSION Version { get; private set; } = new VERSION();
        public SYSTEM_STATES System_State { get; set; } = SYSTEM_STATES.OFF;
        public BDC_MODES BDC_Mode { get; set; } = BDC_MODES.OFF;
        public BDC_CAM_IDS Active_CAM { get; set; } = BDC_CAM_IDS.VIS;

        public Single HB_us { get; private set; } = 0;
        public double RX_HB_us { get; private set; } = 0;

        public UInt16 dt { get; private set; } = 0;
        public Int16 CPUTemp { get; private set; } = 0;
        public double streamFPS { get; private set; } = 0;
        public Size streamSize { get; private set; } = new Size(1280, 720);
        public PointF streamScale { get; set; } = new PointF(1f, 1f);
        public bool isConnected { get; private set; } = false;
        public bool UnsolicitedMode { set { SendUDPBytes(new byte[] { (byte)ICD.SET_UNSOLICITED, Convert.ToByte(value) }); } }
        public bool CueFlag { set { SendUDPBytes(new byte[] { (byte)ICD.ORIN_ACAM_SET_CUE_FLAG, Convert.ToByte(value) }); } }
        private Int64 _ntpTime { get; set; } = 0;
        public DateTime ntpTime { get { return DateTimeOffset.FromUnixTimeMilliseconds(_ntpTime).UtcDateTime; } }
        public Point TrackPoint { get; private set; } = new Point(0, 0);
        public Point AT_OFFSET_RB { get; private set; } = new Point(0, 0);
        public Point FT_OFFSET_RB { get; private set; } = new Point(0, 0);
        public Single VIS_FOCUS_SCORE { get; private set; } = 0;

        public bool HUD_Overlays { set { SendUDPBytes(new byte[] { (byte)ICD.ORIN_SET_STREAM_OVERLAYS, Convert.ToByte(value) }); } }

        public void setTrackBox(Point pt)
        {
            //// set a track box around mouse of 100x75 pixels for test
            //// [cmd, int16 x, int16 y, int16 w, int 16 h]

            //Int16 x = (Int16)pt.X;
            //Int16 y = (Int16)pt.Y;

            //Int16 w = 100;
            //Int16 h = 100;
            //if (x > streamSize.Width - w || x > streamSize.Height - h)
            //{
            //    Debug.WriteLine($"Invalid Track Point chose [{x}, {y}]");
            //    return;
            //}

            ////x -= 50;  // center on track box?
            ////y -= 50;

            //byte[] msg = new byte[9];
            //msg[0] = (byte)ICD.ORIN_ACAM_SET_TRACKBOX;
            //BitConverter.GetBytes((Int16)pt.X).CopyTo(msg, 1);
            //BitConverter.GetBytes((Int16)pt.Y).CopyTo(msg, 3);
            //BitConverter.GetBytes(w).CopyTo(msg, 5);
            //BitConverter.GetBytes(h).CopyTo(msg, 7);
            //SendUDPBytes(msg);
        }

        public byte StatusBits { get; private set; } = 0;


        public TRC() { }

        public void Start()
        {
            ts = new CancellationTokenSource();
            ct = ts.Token;
            Debug.WriteLine("Starting trackController Listener");
            backgroundUDPRead();
        }

        public void Stop()
        {
            Debug.WriteLine("Stopping trackController Listener");
            ts.Cancel();
        }
        private async Task backgroundUDPRead()
        {

            // Start a task - this runs on the background thread...
            Task task = Task.Factory.StartNew(async () =>
            {
                udpClient = new UdpClient(Port);
                ipEndPoint = new IPEndPoint(IPAddress.Parse(IP), Port);
                isConnected = true;

                Thread.Sleep(50);
                UnsolicitedMode = true;

                Debug.WriteLine("UDP Connected");
                do
                {
                    if (ct.IsCancellationRequested)
                    {
                        // another thread decided to cancel
                        Debug.WriteLine("task canceled");
                        isConnected = false;
                        udpClient.Close();
                        Debug.WriteLine("UDP Closed");
                        break;
                    }

                    //if (udpClient.Available > 0)
                    //{
                    //byte[] rxBuff = udpClient.Receive(ref ipEndPoint);
                    var res = await udpClient.ReceiveAsync();
                    byte[] rxBuff = res.Buffer;

                    int rxLen = rxBuff.Length;
                    //Debug.WriteLine($"UDP RX: {rxLen} bytes");
                    if (rxLen > 0)
                    {


                        RX_HB_us = (DateTime.UtcNow - lastMsgRx).TotalMicroseconds;
                        lastMsgRx = DateTime.UtcNow;
                                                
                        // ── ICD v1.7 session 4 TelemetryPacket (64 bytes) ──────────────
                        // NOTE: port 5010 is LEGACY and pending deprecation.
                        // TRC telemetry is now embedded in BDC REG1 and parsed
                        // via MSG_TRC.ParseMsg() — prefer that path for new code.
                        int ndx = 0;
                        byte cmd     = rxBuff[ndx]; ndx++;                                              // [0]  cmd (0xA1)
                        UInt32 verWord = BitConverter.ToUInt32(rxBuff, ndx); ndx += sizeof(UInt32);     // [1-4] version_word
                        System_State = (SYSTEM_STATES)rxBuff[ndx]; ndx++;                              // [5]  systemState
                        BDC_Mode     = (BDC_MODES)rxBuff[ndx]; ndx++;                                  // [6]  systemMode
                        HB_us        = (Single)BitConverter.ToUInt16(rxBuff, ndx); ndx += sizeof(UInt16); // [7-8] HB_ms (uint16)
                        dt           = BitConverter.ToUInt16(rxBuff, ndx); ndx += sizeof(UInt16);      // [9-10] dt_us
                        StatusBits   = rxBuff[ndx]; ndx++;                                              // [11] overlayMask
                        streamFPS    = (double)BitConverter.ToUInt16(rxBuff, ndx) / 100.0; ndx += sizeof(UInt16); // [12-13] fps x100
                        CPUTemp      = BitConverter.ToInt16(rxBuff, ndx); ndx += sizeof(Int16);        // [14-15] deviceTemp
                        Active_CAM   = (BDC_CAM_IDS)rxBuff[ndx]; ndx++;                               // [16] camid

                        for (int i = 0; i < Cameras.Length; i++)
                        {
                            Cameras[i].StatusBits = rxBuff[ndx]; ndx++;                                // [17,19] status_cam
                            Cameras[i].TrackBits  = rxBuff[ndx]; ndx++;                                // [18,20] track_cam
                        }

                        int tx = BitConverter.ToInt16(rxBuff, ndx); ndx += sizeof(Int16);              // [21-22] tx
                        int ty = BitConverter.ToInt16(rxBuff, ndx); ndx += sizeof(Int16);              // [23-24] ty
                        TrackPoint = new Point(tx, ty);

                        int atx = (sbyte)rxBuff[ndx]; ndx++;                                           // [25] atX0
                        int aty = (sbyte)rxBuff[ndx]; ndx++;                                           // [26] atY0
                        int ftx = (sbyte)rxBuff[ndx]; ndx++;                                           // [27] ftX0
                        int fty = (sbyte)rxBuff[ndx]; ndx++;                                           // [28] ftY0
                        AT_OFFSET_RB = new Point(atx, aty);
                        FT_OFFSET_RB = new Point(ftx, fty);

                        VIS_FOCUS_SCORE = BitConverter.ToSingle(rxBuff, ndx); ndx += sizeof(Single);   // [29-32] focusScore (float)
                        _ntpTime = BitConverter.ToInt64(rxBuff, ndx); ndx += sizeof(Int64);            // [33-40] ntpEpochTime
                        // [41-48] voteBitsMcc, voteBitsBdc, nccScore, jetsonTemp, jetsonCpuLoad
                        ndx += 8;
                        // [49-63] RESERVED
                        ndx += 15;






                    }
                    //}
                }
                while (!ct.IsCancellationRequested);
            }, ct);
        }
        public void SendUDPBytes(byte[] msg)
        {
            udpClient.Send(msg, IP, Port);
        }
        public void SetSystemState(SYSTEM_STATES _state)
        {
            SendUDPBytes(new byte[] { (byte)ICD.SET_SYSTEM_STATE, (byte)_state });
        }
        public void SetGimbalMode(BDC_MODES _mode)
        {
            SendUDPBytes(new byte[] { (byte)ICD.SET_GIMBAL_MODE, (byte)_mode });
        }
        public void SetActiveCamera(BDC_CAM_IDS _id)
        {
            SendUDPBytes(new byte[] { (byte)ICD.ORIN_CAM_SET_ACTIVE, (byte)_id });
        }
        public void SetTrackerEnable(BDC_TRACKERS _tracker, bool _en)
        {
            SendUDPBytes(new byte[] { (byte)ICD.ORIN_ACAM_ENABLE_TRACKERS, Convert.ToByte(_tracker), Convert.ToByte(_en) });
        }
        public void SetFireStatus(byte status)
        {
            SendUDPBytes(new byte[] { (byte)ICD.SET_BCAST_FIRECONTROL_STATUS, status });
        }
    }

    public class VERSION
    {
        public UInt32 major { get; set; }
        public UInt32 minor { get; set; }
        public UInt32 year { get; set; }
        public UInt32 month { get; set; }
        public UInt32 day { get; set; }
        public override string ToString()
        {
            return string.Format($"{major}.{minor}.{day}.{month}.{year}");
        }
    }

}
