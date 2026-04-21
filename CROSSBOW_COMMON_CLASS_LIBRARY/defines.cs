using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ─── CROSSBOW ICD defines.cs ──────────────────────────────────────────────────
// Authoritative shared enum and command byte definitions for all CROSSBOW C#
// applications (THEIA HMI, CROSSBOW_ENG_GUIS). Canonical source of truth — matches
// ICD v3.6.0 and defines.hpp v4.0.0.
// Do not edit per-application. All changes must be reflected in the ICD document
// and kept in sync with defines.hpp.
// Version: 4.0.0 | Date: 2026-04-12 | CB-20260412
// ⚠ MAJOR VERSION — ICD v3.6.0 command space restructuring (CB-20260412).
// All five controller firmware targets VERSION_PACK(4,0,0).
// C# clients gate new commands on LatestMSG.IsV4 (FW_VERSION >> 24 >= 4).
// ─────────────────────────────────────────────────────────────────────────────

namespace CROSSBOW
{
    // ─── IP address registry ────────────────────────────────────────────────────
    // Single authority source for all CROSSBOW node IPs on the C# side.
    // Mirrors defines.hpp IP_*_BYTES, plus C#-only entries for THEIA/HYPERION.
    // Strings — consumers call IPAddress.Parse(IPS.X) at bind/send sites.
    // .208 appears twice (THEIA / NTP_FALLBACK) — same machine, two roles.
    // ─────────────────────────────────────────────────────────────────────────────
    public static class IPS
    {
        public const string MCC = "192.168.1.10";
        public const string TMC = "192.168.1.12";
        public const string HEL = "192.168.1.13";   // IPG laser TCP target on MCC
        public const string BDC = "192.168.1.20";
        public const string GIMBAL = "192.168.1.21";   // Galil servo drive
        public const string TRC = "192.168.1.22";   // role address — only one TRC unit live at a time
        public const string FMC = "192.168.1.23";
        public const string GNSS = "192.168.1.30";   // NovAtel — also IEEE 1588 PTP grandmaster
        public const string NTP_PRIMARY = "192.168.1.33";   // HW Stratum 1 NTP server
        public const string NTP_FALLBACK = "192.168.1.208";  // Windows HMI w32tm fallback
        public const string THEIA = "192.168.1.208";  // operator HMI host (same machine as NTP_FALLBACK)
        public const string HYPERION = "192.168.1.206";  // EXT_OPS C2 / sensor fusion
    }
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
    public enum DEBUG_LEVELS
    {
        OFF = 0,
        MIN = 1,
        NORM = 2,
        VERBOSE = 3,
    }
    public enum BDC_TRACKERS
    {
        AI = 0,
        MOSSE = 1,  // TrackB — primary operational tracker
        CENT = 2,
        KALMAN = 3,  // not implemented
        LK = 4,      // Lucas-Kanade optical flow — fully implemented (CB-20260419)
    };

    public enum AF_MODES
    {
        OFF = 0,
        CONT = 1,
        ONCE = 2,
    }
    // 0xD1 ORIN_ACAM_COCO_ENABLE sub-op byte — dual-mode COCO control (ICD v4.1.0 CB-20260419)
    // uint8 op [, uint8 param (op-dependent)]
    // Binary handler pending TRC firmware (TRC-COCO-MODE1). ASCII COCO commands fully implemented.
    public enum COCO_ENABLE_OPS : byte
    {
        OFF = 0x00,  // COCO OFF (all modes)
        ON = 0x01,  // COCO ON (ambient + track both)
        AMBIENT = 0x02,  // ambient full-frame scan only; param: 0=off 1=on
        TRACK = 0x03,  // intra-box track drift indicator only; param: 0=off 1=on
        NEXT = 0x04,  // cycle to next ambient detection (no param)
        PREV = 0x05,  // cycle to previous ambient detection (no param)
        RESET = 0x06,  // clear cycle selection, return trackgate to centre (no param)
        DRIFT = 0x07,  // set drift threshold; param: encoded float (see ICD)
        INTERVAL = 0x08,  // set inference interval divisor; param: uint8 [1–N]
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
        OFF = 0,      // all revisions — GPIO disable only, no I2C side-effect on V2
        LO = 10,     // V1/V3 only — DBU3200 I2C CC/CV 10A; V2 returns STATUS_CMD_REJECTED
        MED = 30,     // V1/V3 only — DBU3200 I2C CC/CV 30A; V2 returns STATUS_CMD_REJECTED
        HI = 55,     // V1/V3 only — DBU3200 I2C CC/CV 55A; V2 returns STATUS_CMD_REJECTED
    }

    // MCC unified power output enum — matches MCC_POWER in defines.hpp
    // Enum value N = POWER_BITS byte 10 bit N.
    // Used with ICD.PMS_POWER_ENABLE (0xE2): { (byte)MCC_POWER, (byte)(en?1:0) }
    // V1·48V·3kW valid:      RELAY_GPS, VICOR_BUS, RELAY_LASER, SOL_HEL, SOL_BDA
    // V2·300V·6kW valid:     RELAY_LASER, VICOR_GIM, VICOR_TMS
    // V3·48V·3kW valid:      RELAY_GPS, VICOR_BUS, RELAY_LASER, SOL_HEL, SOL_BDA, RELAY_NTP
    // V3·300V·6kW valid:     RELAY_GPS, VICOR_BUS, RELAY_LASER, VICOR_GIM, VICOR_TMS, RELAY_NTP
    public enum MCC_POWER
    {
        RELAY_GPS = 0,  // V1/V3     — GPS appliance · NO opto HIGH=ON · pin 83(V1) / 67(V3)
        VICOR_BUS = 1,  // V1/V3·3kW — relay bus 48V→24V · V1:A0 LOW=ON / V3:pin 40 HIGH=ON
        RELAY_LASER = 2,  // All       — laser enable · pin 20(V1) / 83(V2) / 54(V3·3kW) / 63 PIN_ENERGIZE(V3·6kW)
        VICOR_GIM = 3,  // V2/V3·6kW — gimbal 300V→48V · NC HIGH=ON · A0(V2) / pin 55(V3)
        VICOR_TMS = 4,  // V2/V3·6kW — TMS+board 300V→48V · NC HIGH=ON · pin 20(V2) / 51(V3)
        SOL_HEL = 5,  // V1/V3·3kW — laser HV bus solenoid · electromech HIGH=ON · pin 5
        SOL_BDA = 6,  // V1/V3·3kW — gimbal solenoid · electromech HIGH=ON · pin 8(V1) / 50(V3)
        RELAY_NTP = 7,  // V3 only   — NTP appliance · NO opto HIGH=ON · pin 56
    }

    // Laser model identity — sensed via RMN on connect.
    // Byte [255] of MCC REG1. 0x00 = not yet sensed / sense fault.
    // Mirror in defines.hpp.
    public enum LASER_MODEL : byte
    {
        UNKNOWN = 0x00,
        YLM_3K = 0x01,   // bit 0 — YLM-3000-SM-VV
        YLM_6K = 0x02,   // bit 1 — YLM-6000-U3-SM
    }

    public static class LaserModelExt
    {
        public static int MaxPower_W(this LASER_MODEL m)
        {
            switch (m)
            {
                case LASER_MODEL.YLM_3K: return 3000;
                case LASER_MODEL.YLM_6K: return 6000;
                default: return 0;
            }
        }

        public static bool IsSensed(this LASER_MODEL m) => m != LASER_MODEL.UNKNOWN;

        public static string Label(this LASER_MODEL m)
        {
            switch (m)
            {
                case LASER_MODEL.YLM_3K: return "YLM-3000-SM-VV";
                case LASER_MODEL.YLM_6K: return "YLM-6000-U3-SM";
                default: return "UNKNOWN";
            }
        }
    }

    public enum ICD
    {
        SET_UNSOLICITED = 0xA0,  // Subscribe/unsubscribe to unsolicited 100 Hz push. {0x01}=subscribe, {0x00}=unsubscribe. Any accepted command auto-registers the sender. Does NOT affect A1 stream.
        SET_HEL_TRAINING_MODE = 0xA1,  // Moved from 0xAF (v3.6.0). Promoted to INT_OPS. Training clamps power to 10% regardless of SET_HEL_POWER. uint8 0=COMBAT 1=TRAINING
        SET_NTP_CONFIG = 0xA2,  // Promoted INT_ENG→INT_OPS (v3.6.0). 0 bytes=resync | byte[p]=set primary last octet | bytes[p,f]=set primary+fallback last octet. Routing by destination IP.
        SET_TIMESRC = 0xA3,  // Assigned v3.6.0. Set active time source. Routing by IP — each controller applies independently. Prereq: FW-C8 (rejection handler removal) before live. uint8 0=OFF 1=NTP 2=PTP 3=AUTO
        FRAME_KEEPALIVE = 0xA4,  // Register/keep-alive. Empty = register + ACK (ping fields: version, echo_seq, uptime_ms). Payload {0x01} = register + return REG1 now (rate-gated: max 1 Hz per client; suppressed if wantsUnsolicited). INT_ENG: all 5 controllers. INT_OPS: MCC/BDC only. Session 35: was EXT_FRAME_PING (A3/MCC/BDC only).
        SET_SYSTEM_STATE = 0xA5,  //byte (SYSTEM_STATES)
        SET_GIMBAL_MODE = 0xA6,  //byte (BDC_MODES)
        SET_LCH_MISSION_DATA = 0xA7,  // Loads LCH mission data and clears all windows (see ICD)
        SET_LCH_TARGET_DATA = 0xA8,  // Loads LCH target with windows (see ICD)
        SET_REINIT = 0xA9,  // Assigned v3.6.0. Unified controller reinitialise. Replaces 0xB0 (BDC) and 0xE0 (MCC). Routing by IP. TMC/FMC not supported. uint8 subsystem (BDC: 0=NTP,1=GIM,2=FUJI,3=MWIR,4=FSM,5=JET,6=INCL,7=PTP | MCC: 0=NTP,1=TMC,2=HEL,3=BAT,4=PTP,5=CRG,6=GNSS,7=BDC)
        SET_DEVICES_ENABLE = 0xAA,  // Assigned v3.6.0. Unified device enable/disable. Replaces 0xBE (BDC) and 0xE1 (MCC). Routing by IP. TMC/FMC not supported. uint8 device (same indices as SET_REINIT); uint8 0/1
        SET_FIRE_REQUESTED_VOTE = 0xAB,  // Moved from 0xE6 (v3.6.0). Promoted to INT_OPS. Laser fire vote. uint8 0/1 — heartbeat required; watchdog cancels after 500ms silence.
        SET_BDC_HORIZ = 0xAC,  //	VECTOR OF FLOATS HORIZ ELEVATION	float[360]
        SET_HEL_POWER = 0xAD, // SETS LASER POWER    uint8 [0 100]
        CLEAR_HEL_ERROR = 0xAE, // CLEAR LASER ERROR None
        SET_CHARGER = 0xAF,  // Assigned v3.6.0. Merges 0xE3 (PMS_CHARGER_ENABLE) and 0xED (PMS_SET_CHARGER_LEVEL). Level required on every call. V1/V3: GPIO+I2C level control (DBU3200). V2: GPIO enable only — no I2C. uint8 level (CHARGE_LEVELS): 0=OFF 10=LO 30=MED 55=HI. Non-zero on V2 returns STATUS_CMD_REJECTED.

        // RESERVING 0xB FOR BDC COMMAND
        RES_B0 = 0xB0,  // ⚠ RETIRED v3.6.0 — SET_BDC_REINIT superseded by SET_REINIT (0xA9). Returns STATUS_CMD_REJECTED pending FW-C8.
        SET_BDC_VOTE_OVERRIDE = 0xB1,  // Moved from 0xAA (v3.6.0). INT_ENG. Override individual BDC geometry vote bit. byte vote (0=HORIZ,1=KIZ,2=LCH,3=BDA); byte 0/1
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
        RES_BE = 0xBE,  // ⚠ RETIRED v3.6.0 — SET_BDC_DEVICES_ENABLE superseded by SET_DEVICES_ENABLE (0xAA). Returns STATUS_CMD_REJECTED pending FW-C8.
        RES_BF = 0xBF,  // BDC REGISTER RESPONSE

        // RESERVING 0xC FOR CAM COMMANDS
        RES_C0 = 0xC0,  // 
        SET_CAM_MAG = 0xC1,  // ZOOM
        SET_CAM_FOCUS = 0xC2,  // FOCUS (AUTO)
        RES_C3 = 0xC3,  // GAIN (AUTO)
        CMD_VIS_AWB = 0xC4,  // none — trigger VIS auto white balance once (HMI-AWB)
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
        ORIN_ACAM_COCO_ENABLE = 0xD1,  // Moved from 0xDF (v3.6.0). Dual-mode COCO control — ambient full-frame scan + intra-box track drift indicator. uint8 op (COCO_ENABLE_OPS); uint8 param (op-dependent). TRC binary handler pending (TRC-COCO-MODE1). ASCII fully implemented.
        RES_D2 = 0xD2,  // ⚠ RETIRED v3.6.0 — ORIN_SET_STREAM_60FPS retired; framerate is compile/launch time only. ASCII FRAMERATE covers ENG use.
        ORIN_SET_STREAM_OVERLAYS = 0xD3,  // STREAM OVERLAY BITMASK — see HUD_OVERLAY_FLAGS enum
        ORIN_ACAM_SET_CUE_FLAG = 0xD4,  // byte 0/1
        ORIN_ACAM_SET_TRACKGATE_SIZE = 0xD5,  // uint8, unit8
        ORIN_ACAM_ENABLE_FOCUSSCORE = 0xD6,  // byte 0/1
        ORIN_ACAM_SET_TRACKGATE_CENTER = 0xD7,  // uint16, uint16
        RES_D8 = 0xD8,  // ⚠ RETIRED v3.6.0 — ORIN_SET_TESTPATTERNS retired; ASCII TESTSRC covers ENG use; TRC binary handler never implemented.
        ORIN_ACAM_COCO_CLASS_FILTER = 0xD9,  // filter COCO inference to class ID  uint8 (0-79; 0xFF=all) //
        ORIN_ACAM_RESET_TRACKB = 0xDA, //  	RESET TRACK B TO CURRENT TRACK BOX  none
        ORIN_ACAM_ENABLE_TRACKERS = 0xDB,  // uint8 tracker_id (BDC_TRACKERS); uint8 0/1 enable; [uint8 mosseReseed 0x01/0x00] — 3rd byte enables NCC-gated MOSSE template reseed from LK bbox (ICD v4.1.0). ASCII: LK MOSSE ON|OFF.
        ORIN_ACAM_SET_ATOFFSET = 0xDC,  // SET AT OFFSET FOR ACTIVE CAMERA
        ORIN_ACAM_SET_FTOFFSET = 0xDD,  // SET FT OFFSET FOR ACTIVE CAMERA
        ORIN_SET_VIEW_MODE = 0xDE,  // VIEW MODE — 0=CAM1, 1=CAM2, 2=PIP4, 3=PIP8
        RES_DF = 0xDF,  // ⚠ RETIRED v3.6.0 — ORIN_ACAM_COCO_ENABLE moved to 0xD1.

        // RESERVED 0xE (MAYBE TMS/PMS STUFF?)
        SET_BCAST_FIRECONTROL_STATUS = 0xE0,  // Moved from 0xAB (v3.6.0). INT_ENG. Internal vote sync MCC→BDC→TRC. byte voteBitsMcc (VOTE_BITS_MCC); byte voteBitsBdc (VOTE_BITS_BDC)
        RES_E1 = 0xE1,  // ⚠ RETIRED v3.6.0 — SET_MCC_DEVICES_ENABLE superseded by SET_DEVICES_ENABLE (0xAA). Returns STATUS_CMD_REJECTED pending FW-C8.
        PMS_POWER_ENABLE = 0xE2,  // uint8(MCC_POWER); uint8 0/1 — INT_ENG only, both revisions. Replaces PMS_SOL_ENABLE/PMS_RELAY_ENABLE/PMS_VICOR_ENABLE.
        RES_E3 = 0xE3,  // ⚠ RETIRED v3.6.0 — PMS_CHARGER_ENABLE merged into SET_CHARGER (0xAF). Returns STATUS_CMD_REJECTED pending FW-C8.
        RES_E4 = 0xE4,  // ⚠ RETIRED — was PMS_RELAY_ENABLE. Use PMS_POWER_ENABLE with RELAY_GPS or RELAY_LASER.
        RES_E5 = 0xE5,  //	
        RES_E6 = 0xE6,  // ⚠ RETIRED v3.6.0 — SET_FIRE_VOTE moved to 0xAB and promoted to INT_OPS. Returns STATUS_CMD_REJECTED pending FW-C8.
        TMS_INPUT_FAN_SPEED = 0xE7,  // which byte 0/1; speed (0=off, 128=low, 255=high) — see TMC_FAN_SPEEDS
        TMS_SET_DAC_VALUE = 0xE8,  // SET DAC D TO VALUE	which enum dac, uint16 val	maybe just habe 3 settings?
        TMS_SET_VICOR_ENABLE = 0xE9,  //	SET VICOR X TO ON/OFF	which byte vicor (0-3), byte on/off
        TMS_SET_LCM_ENABLE = 0xEA,  //	SET LCM X TO ON/OFF	which byte lcm enum, byte on/off
        TMS_SET_TARGET_TEMP = 0xEB,  // Set Target Temp C  byte [10-40 deg C] — firmware clamps silently
        RES_EC = 0xEC,  // ⚠ RETIRED — was PMS_VICOR_ENABLE. Use PMS_POWER_ENABLE with VICOR_BUS, VICOR_GIM, or VICOR_TMS.
        RES_ED = 0xED,  // ⚠ RETIRED v3.6.0 — PMS_SET_CHARGER_LEVEL merged into SET_CHARGER (0xAF). Returns STATUS_CMD_REJECTED pending FW-C8.
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
        FMC_STAGE_CALIB = 0xFC,  // FOCUS STAGE CALIBRATE — none
        RES_FD = 0xFD,
        FMC_SET_STAGE_ENABLE = 0xFE,  // FOCUS STAGE ENABLE — byte 0/1
        RES_FF = 0xFF,  // FSM register response

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
    // MCC fire control vote bits — 0xE0 SET_BCAST_FIRECONTROL_STATUS byte 1
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
    // BDC geometry vote bits — 0xE0 SET_BCAST_FIRECONTROL_STATUS byte 2
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
