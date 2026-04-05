using GeographicLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace   CROSSBOW
{
    public class RADAR
    {
        // Track commands — use ExtOpsFrame.TrackCmd (shared EXT_OPS enum).
        // RADAR_TRACK_CMDS removed — values are identical to ExtOpsFrame.TrackCmd.

        private readonly CB _aCB; // parent CB — provides system state for SendResponse()

        public bool isUnsolicitedEnabled { get; set; } = false;
        public int PORT { get; private set; } = 10009;
        private UdpClient? udpClient;
        private UdpClient? udpClient2;
        private IPEndPoint iPEndPoint;

        private CancellationTokenSource ts = new CancellationTokenSource();
        private CancellationToken ct;

        private CancellationTokenSource ts2 = new CancellationTokenSource();
        private CancellationToken ct2;

        private ConcurrentDictionary<string, trackLOG> trackLogs { get; set; }
        private ptLLA BaseStation { get; set; } = new ptLLA(34.4593583, -86.4326550, 174.6);
        private string BaseICAO { get; set; } = "RADAR";
        /// <summary>
        /// True  = vz field positive means ascending (ENU — RADAR/EXT).
        /// False = vz field positive means descending (NED/MAVLink — LoRa).
        /// </summary>
        private bool VzPositiveUp { get; set; } = true;
        private double3 _BaseStation_ECEF { get; set; } = new double3();

        public DateTime LastMsgRxTime { get; private set; } = DateTime.UtcNow;
        public double HB_RX_s { get { return HB_RX_ms / 1000.0; } }
        public double HB_RX_ms { get { return (DateTime.UtcNow - LastMsgRxTime).TotalMilliseconds; } }

        public TRACK_TYPES TrackType { get; set; } = TRACK_TYPES.KALMAN_PREDICTED;
        public bool isConnected { get; private set; } = false;

        public RADAR() { }

        public RADAR(ConcurrentDictionary<string, trackLOG> _trackLogs, ptLLA _bs,
                     CB _cb,
                     int _port = 15009, TRACK_TYPES _trackType = TRACK_TYPES.KALMAN_PREDICTED,
                     string _baseICAO = "RADAR", bool _vzPositiveUp = true)
        {
            trackLogs    = _trackLogs;
            BaseStation  = _bs;
            _aCB         = _cb;
            PORT         = _port;
            TrackType    = _trackType;
            BaseICAO     = _baseICAO;
            VzPositiveUp = _vzPositiveUp;
        }

        
        public void UpdateBaseStation(ptLLA _bs)
        {
            BaseStation = new ptLLA(_bs.lat, _bs.lng, _bs.alt);
            Geocentric earth = new Geocentric(Ellipsoid.WGS84); //new Geocentric(Constants.WGS84.MajorRadius, Constants.WGS84.Flattening);
            (double Xa, double Ya, double Za) = earth.Forward(_bs.lat, _bs.lng, _bs.alt); // alt is HAE (WGS-84) — CROSSBOW.BaseStation.alt is documented HAE
            _BaseStation_ECEF = new double3(Xa, Ya, Za);

            // propograte to any logs?
            foreach (KeyValuePair<string, trackLOG> kvp in trackLogs)
            {
                kvp.Value.BaseStation = new ptLLA(_bs.lat, _bs.lng, _bs.alt);
            }
        }
        public void Start()
        {
            ts = new CancellationTokenSource();
            ct = ts.Token;

            ts2 = new CancellationTokenSource();
            ct2 = ts2.Token;

            Debug.WriteLine("Starting {BaseICAO} Listener");
            backgroundUDPRead();

            Debug.WriteLine("Starting {BaseICAO} Sender");
            backgroundUDPSend();

        }
        public void Stop()
        {
            Debug.WriteLine($"Stopping {BaseICAO} Listener");
            ts2.Cancel();
            ts.Cancel();
        }
        private void backgroundUDPRead()
        {

            // Start a task - this runs on the background thread...
            Task task = Task.Factory.StartNew(async () =>
            {
                try
                {
                    //udpClient.Close();
                    udpClient = null;
                    GC.Collect();
                    udpClient = new UdpClient(PORT);
                }
                catch
                {
                    //udpClient.Close();
                    Debug.WriteLine($"{BaseICAO} Listener Socket Error");
                    return;
                }

                Debug.WriteLine($"{BaseICAO} Listener Connected");
                do
                {
                    if (ct.IsCancellationRequested)
                    {
                        // another thread decided to cancel
                        Debug.WriteLine($"{BaseICAO} Listener CANCELLED");

                        udpClient.Close();
                        Debug.WriteLine($"{BaseICAO} Listener Closed");
                        break;
                    }

                    var res = await udpClient.ReceiveAsync();
                    iPEndPoint = res.RemoteEndPoint;
                    isUnsolicitedEnabled = true;
                    isConnected = true;
                    byte[] rxBuff = res.Buffer;

                    //byte[] rxBuff = udpClient.Receive(ref iPEndPoint);

                    ParseMsg(rxBuff);

                }
                while (!ct.IsCancellationRequested);
                Debug.WriteLine($"{BaseICAO} Listener EXIT");
                isUnsolicitedEnabled = false;
                Thread.Sleep(100);
                // purge list here?
                foreach (KeyValuePair<string, trackLOG> aLog in trackLogs.ToList())
                {
                    if (aLog.Value.ICAO == BaseICAO)
                        trackLogs.TryRemove(aLog.Key, out _);
                    Thread.Sleep(1);
                }
                udpClient.Close();
                isConnected = false;
            }, ct);

        }
        private void ParseMsg(byte[] rxBuff)
        {
            // EXT_OPS frame validation — magic 0xCB 0x48, CRC-16/CCITT, PAYLOAD_LEN.
            if (!ExtOpsFrame.TryParseFrame(rxBuff, rxBuff.Length, out var parsed))
            {
                Debug.WriteLine($"{BaseICAO} frame validation failed — discarding");
                return;
            }

            if (parsed.Cmd != ExtOpsFrame.CMD_CUE_INBOUND)
            {
                Debug.WriteLine($"{BaseICAO} unexpected CMD 0x{parsed.Cmd:X2} — discarding");
                return;
            }

            if (parsed.PayloadLen != ExtOpsFrame.PAYLOAD_LEN_CUE)
            {
                Debug.WriteLine($"{BaseICAO} bad payload length {parsed.PayloadLen} — discarding");
                return;
            }

            var trackCmd = (ExtOpsFrame.TrackCmd)parsed.Payload[17];

            switch (trackCmd)
            {
                case ExtOpsFrame.TrackCmd.Drop:
                    foreach (KeyValuePair<string, trackLOG> aLog in trackLogs.ToList())
                    {
                        if (aLog.Value.ICAO == BaseICAO)
                            trackLogs.TryRemove(aLog.Key, out _);
                        Thread.Sleep(1);
                    }
                    break;

                case ExtOpsFrame.TrackCmd.Track:
                    LastMsgRxTime = DateTime.UtcNow;
                    trackMSG tMsg = new trackMSG(parsed.Payload, BaseICAO, VzPositiveUp);
                    if (tMsg.ValidMsg)
                    {
                        if (trackLogs.TryGetValue(tMsg.ICAO, out var existing))
                            existing.Update(tMsg);
                        else
                        {
                            Debug.WriteLine($"{BaseICAO} Adding: {tMsg.ICAO}");
                            trackLogs.TryAdd(tMsg.ICAO, new trackLOG(tMsg, BaseStation, TrackType));
                        }
                    }
                    break;

                case ExtOpsFrame.TrackCmd.ReportOnce:
                    Debug.WriteLine($"{BaseICAO} RESPONDER -> SEND ONCE");
                    SendResponse();
                    break;

                case ExtOpsFrame.TrackCmd.WeaponHold:
                    Debug.WriteLine($"{BaseICAO} WEAPON HOLD");
                    break;

                case ExtOpsFrame.TrackCmd.WeaponFreeToFire:
                    Debug.WriteLine($"{BaseICAO} WEAPON FREE TO FIRE");
                    break;

                case ExtOpsFrame.TrackCmd.ReportContinuousOn:
                    Debug.WriteLine($"{BaseICAO} RESPONDER -> ON");
                    isUnsolicitedEnabled = true;
                    break;

                case ExtOpsFrame.TrackCmd.ReportContinuousOff:
                    Debug.WriteLine($"{BaseICAO} RESPONDER -> OFF");
                    isUnsolicitedEnabled = false;
                    break;

                default:
                    Debug.WriteLine($"{BaseICAO} unhandled TrackCmd 0x{(byte)trackCmd:X2}");
                    break;
            }
        }
        private void backgroundUDPSend()
        {

            // Start a task - this runs on the background thread...
            Task task = Task.Factory.StartNew(async () =>
            {
                //udpClient = new UdpClient(PORT);


                Debug.WriteLine($"{BaseICAO} Sender Connected");
                do
                {
                    if (ct2.IsCancellationRequested)
                    {
                        // another thread decided to cancel
                        Debug.WriteLine($"{BaseICAO} Sender CANCELLED");

                        //udpClient.Close();
                        Debug.WriteLine($"{BaseICAO} Sender Closed");
                        break;
                    }

                    if (isUnsolicitedEnabled)
                        SendResponse();
                    Thread.Sleep(100);
                }
                while (!ct2.IsCancellationRequested);
                Debug.WriteLine($"{BaseICAO} Sender EXIT");
                Thread.Sleep(100);
                //udpClient.Close();
            }, ct2);

        }
        private ushort _txSeq = 0;

        private void SendResponse()
        {
            if (_aCB == null || iPEndPoint == null) return;

            try
            {
                byte[] payload = new byte[ExtOpsFrame.PAYLOAD_LEN_STATUS];

                payload[0] = (byte)_aCB.System_State;
                payload[1] = (byte)_aCB.BDC_Mode;
                payload[2] = (byte)_aCB.Active_CAM;

                // LatestMSG is null until Parse() is called — default to zero if not yet populated
                payload[3] = _aCB.aMCC?.LatestMSG?.VoteBits ?? 0;
                payload[4] = _aCB.aBDC?.LatestMSG?.VoteBits1 ?? 0;
                payload[5] = _aCB.aBDC?.LatestMSG?.VoteBits2 ?? 0;

                ExtOpsFrame.WriteFloat(payload, 6, _aCB.aBDC?.LatestMSG?.LOS_GIM.X ?? 0f);
                ExtOpsFrame.WriteFloat(payload, 10, _aCB.aBDC?.LatestMSG?.LOS_GIM.Y ?? 0f);
                ExtOpsFrame.WriteFloat(payload, 14, _aCB.aBDC?.LatestMSG?.LOS_FSM_RB.X ?? 0f);
                ExtOpsFrame.WriteFloat(payload, 18, _aCB.aBDC?.LatestMSG?.LOS_FSM_RB.Y ?? 0f);
                // [22–29] RESERVED — already zero

                byte[] frame = ExtOpsFrame.BuildFrame(ExtOpsFrame.CMD_STATUS_RESPONSE,
                                                       _txSeq++, payload);
                udpClient?.Send(frame, frame.Length, iPEndPoint);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{BaseICAO} SendResponse error: {ex.Message}");
            }
        }
    }


}
