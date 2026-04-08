using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ─── CROSSBOW ICD defines.cs ──────────────────────────────────────────────────
// Authoritative shared enum and command byte definitions for all CROSSBOW C#
// applications (THEIA HMI, TRC3_ENG_GUI). Canonical source of truth — matches
// ICD v3.4.0 and defines.hpp v3.X.Y.
// Do not edit per-application. All changes must be reflected in the ICD document
// and kept in sync with defines.hpp.
// Version: 3.4.0 | Date: 2026-04-04 | Session 35
// ─────────────────────────────────────────────────────────────────────────────

namespace CROSSBOW
{
    public enum SYSTEM_STATES
    {
        OFF = 0x00,
        STNDBY = 0x01,
        ISR = 0x02,
        COMBAT = 0x03,
        MAINT = 0x04,
        FAULT = 0x05,
    }
    public enum BDC_MODES
    {
        OFF = 0x00,
        POS = 0x01,
        RATE = 0x02,
        CUE = 0x03,
        ATRACK = 0x04,
        FTRACK = 0x05,
    }

    public enum BDC_CAM_IDS
    {
        VIS = 0,
        MWIR = 1,
    }

    public enum MWIR_RUN_STATES
    {
        BOOT = 0,
        WARMUP_WAIT = 1,
        WARMUP_VRFY = 2,
        LENS_INIT = 3,
        COOLDOWN_WAIT = 4,
        COOLDOWN_VRFY = 5,
        SNSR_INIT = 6,
        MAIN_PROC_LOOP = 7,
        LENS_REINIT = 8
    };
    public enum MCC_DEVICES
    {
        NTP = 0,
        TMC = 1,
        HEL = 2,
        BAT = 3,
        PTP = 4,        // IEEE 1588 PTP slave — GNSS master 192.168.1.30 (was RTCLOCK, deprecated session 4)
        CRG = 5,
        GNSS = 6,
        BDC = 7,
    };
    // MCC solenoid identifiers — used with ICD.PMS_SOL_ENABLE (0xE2)
    public enum MCC_SOLENOIDS
    {
        HEL = 0,  // D5 — laser shutter solenoid
        BDA = 1,  // D8 — BDA solenoid
    };

    // MCC relay identifiers — used with ICD.PMS_RELAY_ENABLE (0xE4)
    public enum MCC_RELAYS
    {
        GPS = 1,  // D83 — GNSS power relay
        HEL = 2,  // D20 — laser power relay
        TMS = 3,  // TMS power relay
    };

    public enum BDC_DEVICES
    {
        NTP = 0,
        GIMBAL = 1,
        FUJI = 2,
        MWIR = 3,
        FSM = 4,
        JETSON = 5,
        INCL = 6,
        PTP = 7,        // IEEE 1588 PTP slave — GNSS master 192.168.1.30 (was RTCLOCK, session 32)
    };
    public enum BDC_VOTE_OVERRIDES
    {
        BELOW_HORIZ = 0,
        IN_KIZ = 1,
        IN_LCH = 2,
        BDC_TOTAL = 3,
    };
    public enum BDC_TRACKERS
    {
        AI = 0,
        MOSSE = 1,  // TrackB — primary operational tracker
        CENT = 2,
    };

    public enum AF_MODES
    {
        OFF = 0,
        CONT = 1,
        ONCE = 2,
    }
    // 0xE8 TMS_SET_DAC_VALUE — dac payload byte 0
    // Also used by ENG GUI to send direct DAC channel commands.
    // Authoritative here — absent from pin_defs_tmc.hpp (FW) and defines.hpp (C++).
    public enum TMC_DAC_CHANNELS
    {
        LCM1                 = 0x00,
        LCM2                 = 0x02,
        PUMP                 = 0x04,
        HEATER               = 0x06,
        MCP4728_WIPER        = 0x0B,
        MCP4728_CHANNEL_A_NV = 0x10,
        MCP4728_CHANNEL_B_NV = 0x12,
        MCP4728_CHANNEL_C_NV = 0x14,
        MCP4728_CHANNEL_D_NV = 0x16,
    };
    public enum TMC_FAN_SPEEDS
    {
        OFF = 0,
        LO = 128,
        HI = 255,
    }
    public enum TMC_PUMP_SPEEDS
    {
        OFF = 0,    // also turns off vicor
        LO = 350,   // 9.6V  — NOTE: too low for sustained operation, use MED+
        MED = 500,  // 12.0V
        HI = 800,   // 20.0V
    }
    public enum TMC_LCM_SPEEDS
    {
        OFF = 0,    // 
        LO = 1024,  //
        MED = 2048, //
        HI = 4095,  //
    }
    public enum TMC_VICORS
    {
        LCM1 = 0,
        LCM2 = 1,
        PUMP = 2,   // V1 — single Vicor, both pumps in parallel
        PUMP1 = 2,   // V2 — TRACO PSU pump 1 (same wire value as PUMP)
        HEAT = 3,   // V1 only — heater Vicor
        PUMP2 = 4,   // V2 only — TRACO PSU pump 2
    }

    public enum TMC_LCMS
    {
        LCM1 = 0,    // 
        LCM2 = 1,  //
    }

    public enum CHARGE_LEVELS
    {
        LO = 10,  //
        MED = 30, //
        HI = 55,  //
    }

    public enum ICD
    {
        SET_UNSOLICITED = 0xA0,  // Subscribe/unsubscribe to unsolicited 100 Hz push. {0x01}=subscribe, {0x00}=unsubscribe. Any accepted command auto-registers the sender. Does NOT affect A1 stream.
        RES_A1 = 0xA1,  // ⚠ RETIRED inbound (session 35) — returns STATUS_CMD_REJECTED. Value 0xA1 still appears as CMD_BYTE in received unsolicited REG1 frames; parsers must still accept it on RX.
        SET_NTP_CONFIG = 0xA2,  // NTP config (INT only, A2 only): 0 bytes=resync | byte[p]=set primary last octet | bytes[p,f]=set primary+fallback last octet
        RES_A3 = 0xA3,  // ⚠ RETIRED (session 35) — returns STATUS_CMD_REJECTED. Was GET_REGISTER3 deprecated stub.
        FRAME_KEEPALIVE = 0xA4,  // Register/keep-alive. Empty = register + ACK (ping fields: version, echo_seq, uptime_ms). Payload {0x01} = register + return REG1 now (rate-gated: max 1 Hz per client; suppressed if wantsUnsolicited). INT_ENG: all 5 controllers. INT_OPS: MCC/BDC only. Session 35: was EXT_FRAME_PING (A3/MCC/BDC only).
        SET_SYSTEM_STATE = 0xA5,  //byte (SYSTEM_STATES)
        SET_GIMBAL_MODE = 0xA6,  //byte (BDC_MODES)
        SET_LCH_MISSION_DATA = 0xA7,  // Loads LCH mission data and clears all windows (see ICD)
        SET_LCH_TARGET_DATA = 0xA8,  // Loads LCH target with windows (see ICD)
        PRINT_LCH_DATA = 0xA9,  // byte which [0 KIZ, 1 LCH], byte detail [0 false, 1 true]
        SET_BDC_VOTE_OVERRIDE = 0xAA,  // byte vote [0=HORIZ,1=KIZ,2=LCH,3=BDC], byte 0/1
        SET_BCAST_FIRECONTROL_STATUS = 0xAB,  //  	BYTE STATUS OF FIRECONTROL (MCC VOTE BITS)
        SET_BDC_HORIZ = 0xAC,  //	VECTOR OF FLOATS HORIZ ELEVATION	float[360]
        SET_HEL_POWER = 0xAD, // SETS LASER POWER    uint8 [0 100]
        CLEAR_HEL_ERROR = 0xAE, // CLEAR LASER ERROR None
        RES_AF = 0xAF,  // SYSTEM REGISTER RESPONSE

        // RESERVING 0xB FOR BDC COMMAND
        SET_BDC_REINIT = 0xB0,  // uint8: 0=NTP, 1=GIMBAL, 2=FUJI, 3=MWIR, 4=FSM, 5=JETSON, 6=INCL, 7=PTP
        SET_GIM_HOME = 0xB1,  // HOME POSITION (PAN/TILT)
        SET_GIM_POS = 0xB2,  // POSITION (PAN/TILT)
        SET_GIM_SPD = 0xB3,  // SPEED (PAN/TILT)
        SET_CUE_OFFSET = 0xB4,  // CUE OFFSETS (float/float)
        CMD_GIM_PARK = 0xB5,  // PARK
        SET_GIM_LIMITS = 0xB6,  //	SET WRAP LIMITS (PAN, TILT)	int32, int32, int32, int32	pan fwd, back, tilt fwd, back (deg)
        SET_PID_GAINS = 0xB7,  // PID GAINS (kpp,kip,kdp,kpt,kit,kdt)
        SET_PID_TARGET = 0xB8,  //
        SET_PID_ENABLE = 0xB9,  // PID ENABLE (OR IS THIS JUST MODE)
        SET_SYS_LLA = 0xBA,  // GIMBAL ECEF COORD
        SET_SYS_ATT = 0xBB,  // GIMBAL ANGLES?
        SET_BDC_VICOR_ENABLE = 0xBC,  //	VICOR ON/OFF	byte 0/1	
        SET_BDC_RELAY_ENABLE = 0xBD,  //	RELAY X ON/OFF	byte 1,2,3,4; byte 0/1	relay 1 based 
        SET_BDC_DEVICES_ENABLE = 0xBE,  // ENABLE DEVICE BYTE ENUM; BYTE ON/OFF (see enum)
        RES_BF = 0xBF,  // BDC REGISTER RESPONSE

        // RESERVING 0xC FOR CAM COMMANDS
        RES_C0 = 0xC0,  // 
        SET_CAM_MAG = 0xC1,  // ZOOM
        SET_CAM_FOCUS = 0xC2,  // FOCUS (AUTO)
        RES_C3 = 0xC3,  // GAIN (AUTO)
        RES_C4 = 0xC4,  // WB (AUTO)
        RES_C5 = 0xC5,  // EXP (AUTO)
        RES_C6 = 0xC6,  // GAMMA
        SET_CAM_IRIS = 0xC7,  // IRIS
        CMD_VIS_FILTER_ENABLE = 0xC8,  // FILTER
        SET_BDC_PALOS_VOTE = 0xC9,  //  	Set Operator/Position Valid Vote from GUI	byte which [0 KIZ, 1 LCH], byte OperatorValid, byte PositionValid	Send both
        GET_BDC_PALOS_VOTE = 0xCA,  //	Checks BDC PALOS VOTE	byte which [0 KIZ, 1 LCH], float az, float el, uint64 timestamp
        SET_MWIR_WHITEHOT = 0xCB,  // WHITE HOT ENABLE/DISABLE
        CMD_MWIR_NUC1 = 0xCC,  // 
        CMD_MWIR_AF_MODE = 0xCD,  // byte 0/1/2 off/cont/once
        CMD_MWIR_BUMP_FOCUS = 0xCE,  //
        RES_CF = 0xCF,  // CAMERA REGISTER RESPONSE?  

        // RESERVING 0xD FOR ORIN/TRACKER COMMANDS
        ORIN_CAM_SET_ACTIVE = 0xD0,  // ACTIVE CAMERA
        ORIN_SET_STREAM_MULTICAST = 0xD1, // ENABLE STREAM MULTICAST	byte: 0/1 [1: enabled default]
        ORIN_SET_STREAM_60FPS = 0xD2, // ENABLE STREAM 60FPS 	byte: 0/1 [0: 30FPS, 1: 60 FPS default]
        ORIN_SET_STREAM_OVERLAYS = 0xD3,  // STREAM OVERLAY BITMASK — see HUD_OVERLAY_FLAGS enum
        ORIN_ACAM_SET_CUE_FLAG = 0xD4,  // byte 0/1
        ORIN_ACAM_SET_TRACKGATE_SIZE = 0xD5,  // uint8, unit8
        ORIN_ACAM_ENABLE_FOCUSSCORE = 0xD6,  // byte 0/1
        ORIN_ACAM_SET_TRACKGATE_CENTER = 0xD7,  // uint16, uint16
        ORIN_SET_STREAM_TESTPATTERNS = 0xD8, // ENABLE CAPTURE TEST STREAMS	byte : 0 / 1[0:disabled default]
        ORIN_ACAM_COCO_CLASS_FILTER = 0xD9,  // filter COCO inference to class ID  uint8 (0-79; 0xFF=all) //
        ORIN_ACAM_RESET_TRACKB = 0xDA, //  	RESET TRACK B TO CURRENT TRACK BOX  none
        ORIN_ACAM_ENABLE_TRACKERS = 0xDB,  // ENABLE TRACKERS FOR ACTIVE CAMERA
        ORIN_ACAM_SET_ATOFFSET = 0xDC,  // SET AT OFFSET FOR ACTIVE CAMERA
        ORIN_ACAM_SET_FTOFFSET = 0xDD,  // SET FT OFFSET FOR ACTIVE CAMERA
        ORIN_SET_VIEW_MODE = 0xDE,  // VIEW MODE — 0=CAM1, 1=CAM2, 2=PIP4, 3=PIP8
        ORIN_ACAM_COCO_ENABLE = 0xDF,  // enable/disable COCO intra-trackbox inference  uint8 op [, uint8 param]

        // RESERVED 0xE (MAYBE TMS/PMS STUFF?)
        SET_MCC_REINIT = 0xE0,  // uint8: 0=NTP, 1=TMC, 2=HEL, 3=BAT, 4=PTP, 5=CRG, 6=GNSS, 7=BDC
        SET_MCC_DEVICES_ENABLE = 0xE1,  // uint8: 0=NTP, 1=TMC, 2=HEL, 3=BAT, 4=PTP, 5=CRG, 6=GNSS, 7=BDC; uint8 0/1 — device 4 (PTP) enable/disable PTP slave; device 0 (NTP) controls NTP only
        PMS_SOL_ENABLE = 0xE2,  //	ENABLE SOLENOID 1/2	which byte 0/1, on/off byte 0/1
        PMS_CHARGER_ENABLE = 0xE3,  // ENABLE CHARGER	on/off byte 0/1
        PMS_RELAY_ENABLE = 0xE4,  //	RELAY X ON/OFF	byte 1,2,3,4; byte 0/1	relay 1 based 
        RES_E5 = 0xE5,  //	
        PMS_SET_FIRE_REQUESTED_VOTE = 0xE6,  //	LASER FIRE VOTE REQUEST	byte on/off	Must be sent continous
        TMS_INPUT_FAN_SPEED = 0xE7,  // which byte 0/1; speed (0=off, 128=low, 255=high) — see TMC_FAN_SPEEDS
        TMS_SET_DAC_VALUE = 0xE8,  // SET DAC D TO VALUE	which enum dac, uint16 val	maybe just habe 3 settings?
        TMS_SET_VICOR_ENABLE = 0xE9,  //	SET VICOR X TO ON/OFF	which byte vicor (0-3), byte on/off
        TMS_SET_LCM_ENABLE = 0xEA,  //	SET LCM X TO ON/OFF	which byte lcm enum, byte on/off
        TMS_SET_TARGET_TEMP = 0xEB,  // Set Target Temp C  byte [10-40 deg C] — firmware clamps silently
        PMS_VICOR_ENABLE = 0xEC,  //	VICOR ON/OFF	byte 0/1
        PMS_SET_CHARGER_LEVEL = 0xED,  // SET CHARGER CURRENT LEVEL	enum low=10, med=30, high=55
        RES_EE = 0xEE,  //
        RES_EF = 0xEF,  // 

        // RESERVED 0xF FOR FSM COMMANDS AND RELATED
        FMC_SET_FSM_POW = 0xF0,  //	FSM ENABLE	byte 0/1
        BDC_SET_FSM_HOME = 0xF1,  //	FSM HOME	int16, int16
        BDC_SET_FSM_IFOVS = 0xF2,  //	FSM IFOVS	float, float
        FMC_SET_FSM_POS = 0xF3,  //	FSM POSITION	int16, int16	
        BDC_SET_FSM_SIGNS = 0xF4,  //  	FSM DIRECTIONS	int8, int8
        FMC_FSM_TEST_SCAN = 0xF5,  //	FSM TEST SCAN	none	
        BDC_SET_FSM_TRACK_ENABLE = 0xF6,  //	FSM TRACK ENABLE	byte 0/1
        FMC_READ_FSM_POS = 0xF7, //  	FSM POSITION FROM ADC	none
        RES_F8 = 0xF8,  //
        RES_F9 = 0xF9,  //
        BDC_SET_STAGE_HOME = 0xFA,  //	FOCUS STAGE WAIST HOME	uint32 pos
        FMC_SET_STAGE_POS = 0xFB,  // FOCUS STAGE POSITION	uint32 pos	
        FMC_STAGE_CALIB = 0xFC, // FOCUS STAGE CALIBRATE	None
        FMC_SET_STAGE_ENABLE = 0xFE, // FOCUS STAGE ENABLE	byte 0/1
        RES_FD = 0xFD,  //
        RES_FF = 0xFF,	// FSM REGISTER RESPONSE 

    }

    // -----------------------------------------------------------------------
    // HUD overlay bitmask — used with ICD.ORIN_SET_STREAM_OVERLAYS (0xD3)
    //
    // Send via: aTRC.SetOverlayBitmask((byte)flags)
    //       or: aTRC.SetOverlayBitmask(HudOverlay.Build(reticle:true, trackBox:true))
    //
    // Note: CueChevrons bit controls whether chevrons are RENDERED in the stream.
    //       CUE_FLAG (0xD4) controls whether the Jetson tracks/processes the cue target.
    //       Both must be set for full cue operation; either can be cleared independently.
    // -----------------------------------------------------------------------
    [Flags]
    public enum HUD_OVERLAY_FLAGS : byte
    {
        None            = 0,
        Reticle         = 1 << 0,  // bit0 — crosshair/reticle overlay
        TrackPreview    = 1 << 1,  // bit1 — track gate preview box (before lock)
        TrackBox        = 1 << 2,  // bit2 — active track box (during lock)
        CueChevrons     = 1 << 3,  // bit3 — cue target chevron(s) in video stream
        AC_Projections  = 1 << 4,  // bit4 — AC projected flight path overlays
        AC_LeaderLines  = 1 << 5,  // bit5 — AC leader lines to projected positions
        FocusScore      = 1 << 6,  // bit6 — focus score rendered beneath track box (OSD shows it separately)
        OSD             = 1 << 7,  // bit7 — top-left diagnostic OSD text

        // Convenience composites
        TrackingFull    = Reticle | TrackPreview | TrackBox | FocusScore,
        CueFull         = Reticle | CueChevrons,
        All             = 0xFF,
    }

    // -----------------------------------------------------------------------
    // View mode values — used with ICD.ORIN_SET_VIEW_MODE (0xDE)
    // -----------------------------------------------------------------------
    public enum VIEW_MODES : byte
    {
        CAM1 = 0,   // VIS camera full frame
        CAM2 = 1,   // MWIR camera full frame
        PIP4 = 2,   // Picture-in-picture, 1/4 size inset
        PIP8 = 3,   // Picture-in-picture, 1/8 size inset
    }

    // -----------------------------------------------------------------------
    // HudOverlay helper — builds HUD_OVERLAY_FLAGS bitmasks by named parameter
    // -----------------------------------------------------------------------
    public static class HudOverlay
    {
        /// <summary>
        /// Build an overlay bitmask from individual named flags.
        /// Example: HudOverlay.Build(reticle: true, trackBox: true)
        /// </summary>
        public static byte Build(
            bool reticle        = false,
            bool trackPreview   = false,
            bool trackBox       = false,
            bool cueChevrons    = false,
            bool acProjections  = false,
            bool acLeaderLines  = false,
            bool focusScore     = false,
            bool osd            = false)
        {
            HUD_OVERLAY_FLAGS flags = HUD_OVERLAY_FLAGS.None;
            if (reticle)       flags |= HUD_OVERLAY_FLAGS.Reticle;
            if (trackPreview)  flags |= HUD_OVERLAY_FLAGS.TrackPreview;
            if (trackBox)      flags |= HUD_OVERLAY_FLAGS.TrackBox;
            if (cueChevrons)   flags |= HUD_OVERLAY_FLAGS.CueChevrons;
            if (acProjections) flags |= HUD_OVERLAY_FLAGS.AC_Projections;
            if (acLeaderLines) flags |= HUD_OVERLAY_FLAGS.AC_LeaderLines;
            if (focusScore)    flags |= HUD_OVERLAY_FLAGS.FocusScore;
            if (osd)           flags |= HUD_OVERLAY_FLAGS.OSD;
            return (byte)flags;
        }

        /// <summary>Set or clear a single flag in an existing bitmask.</summary>
        public static byte Set(byte current, HUD_OVERLAY_FLAGS flag, bool enable)
        {
            return enable
                ? (byte)(current | (byte)flag)
                : (byte)(current & ~(byte)flag);
        }

        /// <summary>Check if a specific flag is set in a bitmask byte.</summary>
        public static bool IsSet(byte mask, HUD_OVERLAY_FLAGS flag)
        {
            return (mask & (byte)flag) != 0;
        }
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // MCC fire control vote bits — 0xAB SET_BCAST_FIRECONTROL_STATUS byte 1
    // Send via: aTRC.SetFireStatus(FireVote.SetMcc(...))
    //
    // NOTE: bit1 (NotAbort) is INVERTED — 0 = abort ACTIVE (safe-by-default).
    //       At idle (0x00) isAbort() is TRUE on TRC3. To clear abort, set
    //       NotAbort = 1. All other bits are positive-logic.
    // -----------------------------------------------------------------------
    [Flags]
    public enum VOTE_BITS_MCC : byte
    {
        None          = 0,
        NotAbort      = 1 << 1,  // bit1 — INVERTED: 1 = no abort, 0 = abort ACTIVE
        Armed         = 1 << 2,  // bit2 — HEL armed
        BDAVote       = 1 << 3,  // bit3 — LOS clear, system may fire
        Firing        = 1 << 4,  // bit4 — laser energized (readback only — set by MCC)
        Trigger       = 1 << 5,  // bit5 — trigger pulled
        FireState     = 1 << 6,  // bit6 — FC has all votes, should be firing
        Combat        = 1 << 7,  // bit7 — COMBAT system state

        // Convenience composites for testing
        ArmedNominal  = NotAbort | Armed,                            // armed, no abort
        ReadyToFire   = NotAbort | Armed | BDAVote | Combat,        // all votes except trigger
        FullFireChain = NotAbort | Armed | BDAVote | Trigger | FireState | Combat,
    }

    // -----------------------------------------------------------------------
    // BDC geometry vote bits — 0xAB SET_BCAST_FIRECONTROL_STATUS byte 2
    // Send via: aTRC.SetFireStatus(mcc, FireVote.SetBdc(...))
    // -----------------------------------------------------------------------
    [Flags]
    public enum VOTE_BITS_BDC : byte
    {
        None           = 0,
        BelowHorizon   = 1 << 0,  // bit0 — LOS below horizon
        InKIZ          = 1 << 1,  // bit1 — within kill inhibit zone
        InLCH          = 1 << 2,  // bit2 — within laser clear heading
        BDAVote2       = 1 << 3,  // bit3 — BDA vote copy
        // bit4 reserved
        HorizLoaded    = 1 << 5,  // bit5 — horizon loaded
        // bit6 reserved
        FSMNotLimited  = 1 << 7,  // bit7 — FSM not at gimbal limit

        // Convenience composites for testing
        GeoNominal     = FSMNotLimited,                              // FSM ok, above horizon
        GeoBelow       = BelowHorizon | InKIZ | FSMNotLimited,      // below horizon, KIZ clear
        GeoAbove       = InLCH | InKIZ | FSMNotLimited,             // above horizon, all clear
        GeoAllClear    = BelowHorizon | InKIZ | InLCH | FSMNotLimited,
    }

    // -----------------------------------------------------------------------
    // FireVote helper — builds and manipulates vote bitmasks for 0xAB
    // Mirrors HudOverlay pattern.
    // Usage:
    //   byte mcc = FireVote.SetMcc(armed: true, notAbort: true);
    //   byte bdc = FireVote.SetBdc(fsmNotLimited: true, inKIZ: true, belowHorizon: true);
    //   aTRC.SetFireStatus(mcc, bdc);
    //
    //   // Checkbox example (Armed):
    //   aTRC.SetFireStatus(FireVote.Set(aTRC.VoteBitsMcc_RB, VOTE_BITS_MCC.Armed, chk_Armed.Checked),
    //                      aTRC.VoteBitsBdc_RB);
    // -----------------------------------------------------------------------
    public static class FireVote
    {
        /// <summary>Build an MCC vote bitmask from named flags.</summary>
        public static byte SetMcc(
            bool notAbort   = false,
            bool armed      = false,
            bool bdaVote    = false,
            bool firing     = false,
            bool trigger    = false,
            bool fireState  = false,
            bool combat     = false)
        {
            VOTE_BITS_MCC flags = VOTE_BITS_MCC.None;
            if (notAbort)   flags |= VOTE_BITS_MCC.NotAbort;
            if (armed)      flags |= VOTE_BITS_MCC.Armed;
            if (bdaVote)    flags |= VOTE_BITS_MCC.BDAVote;
            if (firing)     flags |= VOTE_BITS_MCC.Firing;
            if (trigger)    flags |= VOTE_BITS_MCC.Trigger;
            if (fireState)  flags |= VOTE_BITS_MCC.FireState;
            if (combat)     flags |= VOTE_BITS_MCC.Combat;
            return (byte)flags;
        }

        /// <summary>Build a BDC vote bitmask from named flags.</summary>
        public static byte SetBdc(
            bool belowHorizon  = false,
            bool inKIZ         = false,
            bool inLCH         = false,
            bool bdaVote2      = false,
            bool horizLoaded   = false,
            bool fsmNotLimited = false)
        {
            VOTE_BITS_BDC flags = VOTE_BITS_BDC.None;
            if (belowHorizon)   flags |= VOTE_BITS_BDC.BelowHorizon;
            if (inKIZ)          flags |= VOTE_BITS_BDC.InKIZ;
            if (inLCH)          flags |= VOTE_BITS_BDC.InLCH;
            if (bdaVote2)       flags |= VOTE_BITS_BDC.BDAVote2;
            if (horizLoaded)    flags |= VOTE_BITS_BDC.HorizLoaded;
            if (fsmNotLimited)  flags |= VOTE_BITS_BDC.FSMNotLimited;
            return (byte)flags;
        }

        /// <summary>Set or clear a single MCC flag in an existing bitmask.</summary>
        public static byte Set(byte current, VOTE_BITS_MCC flag, bool enable)
        {
            return enable
                ? (byte)(current | (byte)flag)
                : (byte)(current & ~(byte)flag);
        }

        /// <summary>Set or clear a single BDC flag in an existing bitmask.</summary>
        public static byte Set(byte current, VOTE_BITS_BDC flag, bool enable)
        {
            return enable
                ? (byte)(current | (byte)flag)
                : (byte)(current & ~(byte)flag);
        }

        /// <summary>Check if a specific MCC flag is set.</summary>
        public static bool IsSet(byte mask, VOTE_BITS_MCC flag) => (mask & (byte)flag) != 0;

        /// <summary>Check if a specific BDC flag is set.</summary>
        public static bool IsSet(byte mask, VOTE_BITS_BDC flag) => (mask & (byte)flag) != 0;
    }

    // -----------------------------------------------------------------------
    // TRC3 ASCII command permutations — all fixed commands fully expanded.
    // Bind a single ComboBox:
    //   comboAscii.DataSource = Enum.GetValues(typeof(TRC_ASCII_CMD));
    // Send on button click:
    //   aTRC.SendUDPString(((TRC_ASCII_CMD)comboAscii.SelectedItem).ToCommand());
    // -----------------------------------------------------------------------

    /// <summary>
    /// Complete set of fixed ASCII command permutations for TRC3 (port 5012).
    /// Underscores in the name become spaces in the transmitted string via ToCommand().
    /// Numeric-parameter commands (BITRATE, EXPOSURE, etc.) are handled separately
    /// via TrcAscii.Build(cmd, value) with a text input field.
    /// </summary>
    public enum TRC_ASCII_CMD
    {
        // SELECT
        SELECT_CAM1,            // SELECT CAM1
        SELECT_CAM2,            // SELECT CAM2
        // VIEW
        VIEW_CAM1,              // VIEW CAM1
        VIEW_CAM2,              // VIEW CAM2
        VIEW_PIP,               // VIEW PIP
        VIEW_PIP8,              // VIEW PIP8
        // TESTSRC
        TESTSRC_CAM1_TEST,      // TESTSRC CAM1 TEST
        TESTSRC_CAM1_LIVE,      // TESTSRC CAM1 LIVE
        TESTSRC_CAM2_TEST,      // TESTSRC CAM2 TEST
        TESTSRC_CAM2_LIVE,      // TESTSRC CAM2 LIVE
        // TRACKER
        TRACKER_ON,             // TRACKER ON
        TRACKER_OFF,            // TRACKER OFF
        TRACKER_RESET,          // TRACKER RESET
        TRACKER_INIT,           // TRACKER INIT
        // RETICLE
        RETICLE_ON,             // RETICLE ON
        RETICLE_OFF,            // RETICLE OFF
        // OSD
        OSD_ON,                 // OSD ON
        OSD_OFF,                // OSD OFF
        // FOCUSSCORE
        FOCUSSCORE_ON,          // FOCUSSCORE ON
        FOCUSSCORE_OFF,         // FOCUSSCORE OFF
        // DEBUG
        DEBUG_ON,               // DEBUG ON
        DEBUG_OFF,              // DEBUG OFF
        DEBUG_VERBOSE_ON,       // DEBUG VERBOSE ON  — high-frequency per-packet logs (e.g. 0xD7)
        DEBUG_VERBOSE_OFF,      // DEBUG VERBOSE OFF
        // AWB
        AWB,                    // AWB — trigger auto white balance on active camera
        // STATUS
        STATUS,                 // STATUS — one-shot 64-byte telemetry reply to ASCII sender
    }

    /// <summary>
    /// Extension method: converts TRC_ASCII_CMD enum value to the exact string TRC3 expects.
    /// Replaces underscores with spaces: TRACKER_RESET → "TRACKER RESET"
    /// </summary>
    public static class TrcAsciiExtensions
    {
        public static string ToCommand(this TRC_ASCII_CMD cmd)
            => cmd.ToString().Replace('_', ' ');
    }

    /// <summary>
    /// Helpers for numeric-parameter ASCII commands that can't be in the fixed enum.
    /// </summary>
    public static class TrcAscii
    {
        /// <summary>BITRATE &lt;kbps&gt;  e.g. "BITRATE 8000"</summary>
        public static string Bitrate(int kbps)       => $"BITRATE {kbps}";
        /// <summary>FRAMERATE &lt;fps&gt;  e.g. "FRAMERATE 30"</summary>
        public static string Framerate(int fps)      => $"FRAMERATE {fps}";
        /// <summary>EXPOSURE &lt;us&gt;   e.g. "EXPOSURE 5000"</summary>
        public static string Exposure(int us)        => $"EXPOSURE {us}";
        /// <summary>GAIN &lt;dB&gt;       e.g. "GAIN 6.0"</summary>
        public static string Gain(double dB)         => $"GAIN {dB}";
        /// <summary>GAMMA &lt;value&gt;   e.g. "GAMMA 1.2"</summary>
        public static string Gamma(double value)     => $"GAMMA {value}";
        /// <summary>ATOFFSET &lt;x&gt; &lt;y&gt;</summary>
        public static string AtOffset(int x, int y) => $"ATOFFSET {x} {y}";
        /// <summary>FTOFFSET &lt;x&gt; &lt;y&gt;</summary>
        public static string FtOffset(int x, int y) => $"FTOFFSET {x} {y}";
        /// <summary>TRACKBOX &lt;w&gt; &lt;h&gt; [&lt;cx&gt; &lt;cy&gt;]</summary>
        public static string TrackBox(int w, int h, int cx = -1, int cy = -1)
            => cx < 0 ? $"TRACKBOX {w} {h}" : $"TRACKBOX {w} {h} {cx} {cy}";
        /// <summary>REPORT START &lt;hz&gt; | REPORT STOP</summary>
        public static string Report(bool start, int hz = 50)
            => start ? $"REPORT START {hz}" : "REPORT STOP";
    }



}
