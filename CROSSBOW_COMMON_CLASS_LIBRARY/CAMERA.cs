// CAMERA.cs  —  per-camera status block used by TRC_MSG
//
// TRC_MSG maintains a two-element array of CAMERA objects:
//   Cameras[0] = VIS  (index = BDC_CAM_IDS.VIS  = 0)
//   Cameras[1] = MWIR (index = BDC_CAM_IDS.MWIR = 1)
//
// StatusBits and TrackBits are written directly by TRC_MSG.ParseMsg()
// during the per-camera loop at TRC REG1 bytes [17–20].

namespace CROSSBOW
{
    public class CAMERA
    {
        public BDC_CAM_IDS CamID { get; private set; }

        // Set by TRC_MSG.ParseMsg() — ICD v4.2.2 TRC REG1 [17,19]
        public byte StatusBits { get; set; } = 0;

        // Set by TRC_MSG.ParseMsg() — ICD v4.2.2 TRC REG1 [18,20]
        public byte TrackBits { get; set; } = 0;

        // StatusBits accessors
        public bool isPowered   { get { return IsBitSet(StatusBits, 0); } }
        public bool isConnected { get { return IsBitSet(StatusBits, 1); } }
        public bool isCapturing { get { return IsBitSet(StatusBits, 2); } }

        // TrackBits accessors
        public bool isTracking  { get { return IsBitSet(TrackBits, 0); } }
        public bool isLocked    { get { return IsBitSet(TrackBits, 1); } }

        public CAMERA(BDC_CAM_IDS camID)
        {
            CamID = camID;
        }

        private static bool IsBitSet(byte b, int pos) => (b & (1 << pos)) != 0;
    }
}
