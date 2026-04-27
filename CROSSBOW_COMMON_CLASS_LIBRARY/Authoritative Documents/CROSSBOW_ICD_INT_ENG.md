# CROSSBOW — INT_ENG Interface Control Document

**Document:** `CROSSBOW_ICD_INT_ENG`
**Doc #:** IPGD-0003
**Version:** 4.3.0
**Date:** 2026-04-26 (CB-20260426 — vote byte overhaul, FC-CONSISTENCY-1 closed, full STATUS_BITS)
**Classification:** IPG Internal Use Only
**Audience:** IPG engineering staff, ENG GUI developers, firmware developers — all five controllers

---

## Version History

**v4.3.0 changes (CB-20260426 — vote byte overhaul, FC-CONSISTENCY-1 closed, full STATUS_BITS):**

Merges `CROSSBOW_FIRECONTROL_REGISTERS.md` and `CROSSBOW_DEVICE_STATUS_BITS_WORKING.md` into the ICD.
All register changes are wire-level breaking. See `CROSSBOW_ICD_INT_OPS` v4.0.0 for operator-facing summary.

**VOTE_BITS_MCC [11] — complete replacement:**
Gate-chain ordering corrected to follow the physical AND chain b0→b7. Old layout had incorrect ordering and stale field names (`isBDA_Vote_rb`, `isCombat_Vote_rb` on wrong bit). `isCombat_Vote_rb` moved to VOTE_BITS_MCC2.
New layout: `b0:NOT_ABORT(INVERTED)` `b1:ARMED` `b2:BDC_VOTE` `b3:LASER_TOTAL_HW` `b4:SW_VOTE` `b5:TRIGGER` `b6:FIRE_STATE` `b7:EMON`.
Composites: `ARMED_NOMINAL=0x03` `READY_TO_FIRE=0x1F` `FULL_FIRE_CHAIN=0x7F`.

**FC-CONSISTENCY-1 CLOSED:**
`isFireExpected = (VOTE_BITS_MCC & FULL_FIRE_CHAIN) == FULL_FIRE_CHAIN`. Based on this:
- `EMON_MISSING (b3)` = `isFireExpected && !ipg.isEMON()`
- `EMON_UNEXPECTED (b4)` = `!isFireExpected && ipg.isEMON()`
- `FIRE_INTERLOCKED (b5)` = `isLaserFireRequested_Vote && !isFireExpected && !ipg.isEMON()`
All three implemented in `VOTE_BITS_MCC2` and mirrored in `MCC_HEL_STATUS_BITS` bits 5–7.
`FIRE_VOTE_BYTE` concept superseded — `VOTE_BITS_MCC` + `VOTE_BITS_MCC2` together carry complete fire state.

**MCC REG1 — 8 new bytes [256–263]:** VOTE_BITS_MCC2, MCC DEVICE_WARN_BITS, and 6 per-device STATUS_BITS.
**BDC REG1 — vote bytes [164–166] renamed and corrected.** b7 of [165] corrected: was `isFSMLimited` (inverted) → `FSM_NOT_LIMITED` (SET = FSM clear).
**BDC REG1 — 8 new bytes [404–411]:** VOTE_BITS_MCC2_RB, BDC DEVICE_WARN_BITS, and 6 per-device STATUS_BITS.

**`0xE0 SET_BCAST_FIRECONTROL_STATUS` payload updated:**
- MCC→BDC: `[0xE0][VOTE_BITS_MCC][VOTE_BITS_MCC2]` — 3 bytes total (was 3, payload now 2)
- BDC→TRC: `[0xE0][VOTE_BITS_MCC][VOTE_BITS_BDC][sysState][bdcMode][VOTE_BITS_MCC2][VOTE_BITS_BDC2]` — 7 bytes payload (was 2)
TRC REG1: `voteBitsMcc` [41], `voteBitsBdc` [42], `voteBitsMcc2` [57], `voteBitsBdc2` [58].

**New status code:** `STATUS_PREREQ_FAIL = 0x07`.

**FCVOTES checkout updated** — all vote byte injections updated to new bit layout.

**`isBDA_Vote_rb` → `isBDC_Vote_rb` throughout** — BDA rename completed.

**C# changes:** `icd.cs` enums fully updated. `MSG_MCC.cs` / `MSG_BDC.cs` property renames complete. See INT_OPS v4.0.0 for full C# impact.

---

**v4.2.2 changes (CB-20260425 — stale 0xAB fire control references corrected):**
- 0xE0 description: rate corrected 100 Hz → 200 Hz. BDC REG1 HB_MCC_ms: `last 0xAB RX` → `last 0xE0 RX`. TRC REG1 voteBitsMcc: source corrected to 0xE0.

**v4.2.1 changes:** Command table INT_OPS Target corrections. Appendix A added.

**v4.2.0 changes (CB-20260419c):** TRC REG1 Jetson health compacted int16→uint8. GPU temp added.

**v4.1.0 changes (CB-20260419):** LK tracker (tracker_id=4) implemented. COCO_ENABLE_OPS enum added.

**v4.0.1 changes (CB-20260416):** `0xC4 CMD_VIS_AWB` assigned. `0xAF SET_CHARGER` V2 corrected.

**v4.0.0 changes (CB-20260413d):** BDC REG1 bytes [396–403] promoted — eight HB counter bytes.

**v3.6.0 changes:** A block fully assigned. See archived INT_ENG v4.2.2 for full history.

---

## 0xA0–0xAF — System Commands

| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xA0 | SET_UNSOLICITED | Subscribe/unsubscribe unsolicited push. Per-slot `wantsUnsolicited`. | uint8 0/1 | — | — | ✓ | `INT_OPS` | MCC, BDC | MCC, BDC |
| 0xA1 | SET_HEL_TRAINING_MODE | Training mode — clamps laser to 10%. Promoted INT_ENG→INT_OPS v3.6.0. | uint8 0=COMBAT, 1=TRAINING | — | — | ✓ | `INT_OPS` | MCC | MCC |
| 0xA2 | SET_NTP_CONFIG | NTP config / resync. Promoted INT_ENG→INT_OPS v3.6.0. | 0–2 bytes | — | — | ✓ | `INT_OPS` | MCC, BDC | MCC, BDC |
| 0xA3 | SET_TIMESRC | Time source selection. ⚠ Pending FW-C8. | uint8 0=OFF,1=NTP,2=PTP,3=AUTO | — | — | ✓ | `INT_OPS` | MCC, BDC | MCC, BDC |
| 0xA4 | FRAME_KEEPALIVE | Register/keepalive. Empty=ACK. `{0x01}`=ACK+solicited REG1. | 0 or 1 byte | — | — | ✓ | `INT_OPS` | MCC, BDC, TMC, FMC, TRC | MCC, BDC |
| 0xA5 | SET_SYSTEM_STATE | Set system state. Returns `STATUS_PREREQ_FAIL (0x07)` if prerequisites unmet. | uint8 SYSTEM_STATES | — | — | ✓ | `INT_OPS` | MCC, BDC | MCC, BDC |
| 0xA6 | SET_GIMBAL_MODE | Set gimbal/tracker mode. Returns `STATUS_PREREQ_FAIL (0x07)` if prerequisites unmet. | uint8 BDC_MODES | — | — | ✓ | `INT_OPS` | MCC, BDC | MCC, BDC |
| 0xA7 | SET_LCH_MISSION_DATA | Load LCH/KIZ mission data | (see INT_OPS) | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xA8 | SET_LCH_TARGET_DATA | Load LCH/KIZ target with windows | (see INT_OPS) | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xA9 | SET_REINIT | Unified reinitialise. Replaces 0xB0/0xE0. | uint8 subsystem | — | — | ✓ | `INT_OPS` | MCC, BDC | MCC, BDC |
| 0xAA | SET_DEVICES_ENABLE | Unified device enable/disable. Replaces 0xE1. | uint8 device; uint8 0/1 | — | — | ✓ | `INT_OPS` | MCC, BDC | MCC, BDC |
| 0xAB | SET_FIRE_REQUESTED_VOTE | Laser fire vote. Promoted INT_ENG→INT_OPS v3.6.0. Heartbeat safety gate. | uint8 0/1 | — | — | ✓ | `INT_OPS` | MCC | MCC |
| 0xAC | SET_BDC_HORIZ | Set horizon elevation vector | float[360] | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xAD | SET_HEL_POWER | Set laser power | uint8 [0–100]% | — | — | ✓ | `INT_OPS` | MCC | MCC |
| 0xAE | CLEAR_HEL_ERROR | Clear laser error | none | — | — | ✓ | `INT_OPS` | MCC | MCC |
| 0xAF | SET_CHARGER | Charger state and level. V1/V3: GPIO+I2C. V2: GPIO only. | uint8 level: 0/10/30/55 | — | — | ✓ | `INT_OPS` | MCC | MCC |

---

## 0xB0–0xBF — BDC Commands

| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xB0 | RES_B0 | **RETIRED v3.6.0** — SET_BDC_REINIT superseded by 0xA9. Returns `STATUS_CMD_REJECTED`. | — | — | — | — | `RES` | RES | RES |
| 0xB1 | SET_BDC_VOTE_OVERRIDE | Override individual BDC geometry vote. INT_ENG only. | uint8 vote (BDC_VOTE_OVERRIDES: 0=BELOW_HORIZ,1=IN_KIZ,2=IN_LCH,3=BDC_TOTAL); uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC | — |
| 0xB2 | SET_GIM_POS | Gimbal position | int32 pan, int32 tilt | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB3 | SET_GIM_SPD | Gimbal speed | int16 pan, int16 tilt | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB4 | SET_CUE_OFFSET | Cue track offset | float az_deg, float el_deg | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB5 | CMD_GIM_PARK | Park gimbal | none | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB6 | SET_GIM_LIMITS | Gimbal wrap limits | int32 panMin, panMax, tiltMin, tiltMax | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB7 | SET_PID_GAINS | PID gains | uint8 which; float kpp,kip,kdp,kpt,kit,kdt | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB8 | SET_PID_TARGET | PID setpoint | uint8 sub-cmd; float x; float y; float pidScale | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB9 | SET_PID_ENABLE | PID enable | uint8 which; uint8 0/1 | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xBA | SET_SYS_LLA | Platform position | float lat, lng, alt | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xBB | SET_SYS_ATT | Platform attitude | float roll, pitch, yaw | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xBC | SET_BDC_VICOR_ENABLE | BDC Vicor enable | uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC | — |
| 0xBD | SET_BDC_RELAY_ENABLE | BDC relay enable | uint8 relay (1-based); uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC | — |
| 0xBE | RES_BE | **RETIRED v3.6.0** — superseded by 0xAA. Returns `STATUS_CMD_REJECTED`. | — | — | — | — | `RES` | RES | RES |
| 0xBF | RES_BF | Reserved — confirmed unused. | — | — | — | — | `RES` | — | — |

---

## 0xC0–0xCF — BDC/Camera Commands

| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xC1 | SET_CAM_MAG | VIS zoom | uint8 mag index | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xC2 | SET_CAM_FOCUS | VIS focus | uint16 position | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xC4 | CMD_VIS_AWB | Auto white balance once | none | `AWB` | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xC7 | SET_CAM_IRIS | VIS iris | uint8 position | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xC8 | CMD_VIS_FILTER_ENABLE | VIS ND filter | uint8 0/1 | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xC9 | SET_BDC_PALOS_VOTE | PALOS vote | uint8 which; uint8 opValid; uint8 posValid; uint8 forExec | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCA | GET_BDC_PALOS_VOTE | PALOS query | uint8 which; uint64 ts; float az; float el | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCB | SET_MWIR_WHITEHOT | MWIR white hot | uint8 0/1 | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCC | CMD_MWIR_NUC1 | MWIR NUC | none | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCD | CMD_MWIR_AF_MODE | MWIR AF mode | uint8 0=off,1=cont,2=once | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCE | CMD_MWIR_BUMP_FOCUS | MWIR bump focus | uint8 0=near,1=far | — | — | ✓ | `INT_OPS` | BDC | BDC |

---

## 0xD0–0xDF — TRC/Orin Commands

| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xD0 | ORIN_CAM_SET_ACTIVE | Active camera | uint8 BDC_CAM_IDS | `SELECT CAM1\|CAM2` | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD1 | ORIN_ACAM_COCO_ENABLE | Dual-mode COCO control. **INT_ENG only** (absent from EXT_CMDS_BDC[]). | uint8 op (COCO_ENABLE_OPS); uint8 param | `COCO *` | ✓ | ✓ | `INT_ENG` | BDC, TRC | — |
| 0xD2 | RES_D2 | **RETIRED v3.6.0** — framerate is compile/launch time. ASCII FRAMERATE covers ENG use. | — | — | — | — | `RES` | RES | RES |
| 0xD3 | ORIN_SET_STREAM_OVERLAYS | HUD overlay bitmask (HUD_OVERLAY_BITS enum) | uint8 bitmask | `RETICLE\|OSD ON\|OFF` | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD4 | ORIN_ACAM_SET_CUE_FLAG | Cue flag | uint8 0/1 | — | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD5 | ORIN_ACAM_SET_TRACKGATE_SIZE | Trackgate size | uint8 w, uint8 h | `TRACKBOX w h` | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD6 | ORIN_ACAM_ENABLE_FOCUSSCORE | Focus score | uint8 0/1 | `FOCUSSCORE ON\|OFF` | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD7 | ORIN_ACAM_SET_TRACKGATE_CENTER | Trackgate center | uint16 x, uint16 y | — | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD8 | RES_D8 | **RETIRED v3.6.0** — ASCII TESTSRC covers ENG use. | — | — | — | — | `RES` | RES | RES |
| 0xD9 | ORIN_ACAM_COCO_CLASS_FILTER | COCO class filter | uint8 (0–79; 0xFF=all) | `COCO FILTER id\|ALL` | ✓ | ✓ | `INT_ENG` | BDC, TRC | — |
| 0xDA | ORIN_ACAM_RESET_TRACKB | Reset MOSSE | none | `TRACKER RESET` | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xDB | ORIN_ACAM_ENABLE_TRACKERS | Tracker enable. 3rd byte enables NCC-gated MOSSE reseed from LK bbox. | uint8 tracker_id (0=AI,1=MOSSE,2=CENT,4=LK); uint8 0/1; [uint8 mosseReseed] | `LK ON\|OFF` | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xDC | ORIN_ACAM_SET_ATOFFSET | AT offset | int8 dx, int8 dy | `ATOFFSET x y` | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xDD | ORIN_ACAM_SET_FTOFFSET | FT offset | int8 dx, int8 dy | `FTOFFSET x y` | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xDE | ORIN_SET_VIEW_MODE | View mode | uint8 0=CAM1,1=CAM2,2=PIP4,3=PIP8 | `VIEW CAM1\|CAM2\|PIP\|PIP8` | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xDF | RES_DF | **RETIRED v3.6.0** — ORIN_ACAM_COCO_ENABLE moved to 0xD1. | — | — | — | — | `RES` | RES | RES |

---

## 0xE0–0xEF — MCC / TMS Commands

| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xE0 | SET_BCAST_FIRECONTROL_STATUS | **Internal A1 vote broadcast. Rate: 200 Hz (TICK_VoteStatus=5 ms), unconditional.** MCC→BDC payload: `[VOTE_BITS_MCC][VOTE_BITS_MCC2]` (2 bytes). BDC→TRC payload: `[VOTE_BITS_MCC][VOTE_BITS_BDC][sysState][bdcMode][VOTE_BITS_MCC2][VOTE_BITS_BDC2]` (7 bytes). TRC stores to REG1: voteBitsMcc[41], voteBitsBdc[42], voteBitsMcc2[57], voteBitsBdc2[58]. **⚠ Breaking v4.3.0 — payload extended.** | (see description) | — | ✓ | ✓ | `INT_ENG` | MCC, BDC, TRC | — |
| 0xE1 | RES_E1 | **RETIRED v3.6.0** — superseded by 0xAA. Returns `STATUS_CMD_REJECTED`. | — | — | — | — | `RES` | RES | RES |
| 0xE2 | PMS_POWER_ENABLE | Unified power output control. INT_ENG only — A3 returns `STATUS_CMD_REJECTED`. | uint8 which (MCC_POWER enum: 0=RELAY_GPS,1=VICOR_BUS,2=RELAY_LASER,3=VICOR_GIM,4=VICOR_TMS,5=SOL_HEL,6=SOL_BDA,7=RELAY_NTP); uint8 0/1 | — | — | ✓ | `INT_ENG` | MCC | — |
| 0xE3 | RES_E3 | **RETIRED v3.6.0** — merged into 0xAF. Returns `STATUS_CMD_REJECTED`. | — | — | — | — | `RES` | RES | RES |
| 0xE4 | RES_E4 | **RETIRED v3.4.0** — use 0xE2 PMS_POWER_ENABLE. | — | — | — | — | `RES` | MCC | MCC |
| 0xE5 | RES_E5 | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xE6 | RES_E6 | **RETIRED v3.6.0** — SET_FIRE_VOTE moved to 0xAB. Returns `STATUS_CMD_REJECTED`. | — | — | — | — | `RES` | RES | RES |
| 0xE7 | TMS_INPUT_FAN_SPEED | Fan speed | uint8 which (0/1); uint8 speed (0=off,128=lo,255=hi) | — | — | ✓ | `INT_OPS` | MCC, TMC | MCC |
| 0xE8 | TMS_SET_DAC_VALUE | DAC output value | uint8 dac (TMC_DAC_CHANNELS); uint16 value | — | — | ✓ | `INT_ENG` | MCC, TMC | — |
| 0xE9 | TMS_SET_VICOR_ENABLE | TMS Vicor enable | uint8 vicor (TMC_VICORS); uint8 0/1 | — | — | ✓ | `INT_ENG` | MCC, TMC | — |
| 0xEA | TMS_SET_LCM_ENABLE | TMS LCM enable | uint8 lcm; uint8 0/1 | — | — | ✓ | `INT_ENG` | MCC, TMC | — |
| 0xEB | TMS_SET_TARGET_TEMP | TMS target temp | uint8 °C [10–40, clamped] | — | — | ✓ | `INT_OPS` | MCC, TMC | MCC |
| 0xEC | RES_EC | **RETIRED v3.4.0** — use 0xE2. | — | — | — | — | `RES` | MCC | MCC |
| 0xED | RES_ED | **RETIRED v3.6.0** — merged into 0xAF. Returns `STATUS_CMD_REJECTED`. | — | — | — | — | `RES` | RES | RES |
| 0xEE | RES_EE | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xEF | RES_EF | Reserved — confirmed unused. | — | — | — | — | `RES` | — | — |

---

## 0xF0–0xFF — FSM / FMC Commands

| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xF0 | FMC_SET_FSM_POW | FSM power enable | uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xF1 | BDC_SET_FSM_HOME | FSM home position | int16 x, int16 y | — | — | ✓ | `INT_OPS` | BDC, FMC | BDC |
| 0xF2 | BDC_SET_FSM_IFOVS | FSM iFOV scaling | float x, float y | — | — | ✓ | `INT_OPS` | BDC, FMC | BDC |
| 0xF3 | FMC_SET_FSM_POS | FSM position | int16 x, int16 y | — | — | ✓ | `INT_OPS` | BDC, FMC | BDC |
| 0xF4 | BDC_SET_FSM_SIGNS | FSM axis direction | int8 x, int8 y | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xF5 | FMC_FSM_TEST_SCAN | FSM test scan | none | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xF6 | BDC_SET_FSM_TRACK_ENABLE | FSM track enable. **INT_ENG only** (absent from EXT_CMDS_BDC[]). | uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xF7 | FMC_READ_FSM_POS | Read FSM ADC position | none | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xF8 | RES_F8 | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xF9 | RES_F9 | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xFA | BDC_SET_STAGE_HOME | Stage waist home. **INT_ENG only** (absent from EXT_CMDS_BDC[]). | uint32 position | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xFB | FMC_SET_STAGE_POS | Stage position | uint32 position | — | — | ✓ | `INT_OPS` | BDC, FMC | BDC |
| 0xFC | FMC_STAGE_CALIB | Stage calibrate | none | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xFD | RES_FD | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xFE | FMC_SET_STAGE_ENABLE | Stage enable | uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xFF | RES_FF | Reserved — confirmed unused. | — | — | — | — | `RES` | — | — |

---

## TRC ASCII-Only Commands

### Global Commands

| Command | Description |
|---------|-------------|
| `SELECT CAM1\|CAM2` | Switch active camera |
| `VIEW CAM1\|CAM2\|PIP\|PIP8` | Set view mode |
| `STATUS` | Print system state; send one-shot telemetry |
| `REPORT START [ms]\|STOP` | Unsolicited telemetry (default 1000 ms) |
| `DEBUG ON\|OFF` | Enable/disable debug logging |
| `DEBUG VERBOSE ON\|OFF` | High-frequency per-packet logs |
| `TESTSRC CAM1\|CAM2 TEST\|LIVE` | Test pattern / live source |
| `BITRATE <Mbps>` | H.264 encoder bitrate (1–50 Mbps) |
| `QUIT` | Graceful shutdown |

### Camera Commands

| Command | Description |
|---------|-------------|
| `EXPOSURE <µs>` | Set exposure, disables auto |
| `EXPOSURE AUTO` | Re-enable auto exposure |
| `GAIN <dB>` | Set gain |
| `GAIN AUTO` | Re-enable auto gain |
| `GAMMA <value>` | Set gamma |
| `FRAMERATE <fps>` | Set framerate |
| `AWB` | Trigger auto white balance |
| `FOCUSSCORE ON\|OFF` | Focus score computation |
| `RETICLE ON\|OFF` | Reticle overlay |
| `OSD ON\|OFF` | OSD text overlay |
| `TRACKBOX <w> <h> [cx cy]` | Track gate size and optional center |
| `ATOFFSET <x> <y>` | AT reticle offset (−128 to 127 px) |
| `FTOFFSET <x> <y>` | FT offset |
| `TRACKER ON\|OFF\|RESET` | Tracker lifecycle |
| `TRACKER INIT <x> <y> <w> <h>` | Explicit ROI init |

### ENG Debug Injection Commands

| Command | Description |
|---------|-------------|
| `STATE <state>` | Inject system state. Values: OFF, STNDBY, ISR, COMBAT, MAINT, FAULT. |
| `MODE <mode>` | Inject gimbal mode. Values: OFF, POS, RATE, CUE, ATRACK, FTRACK. |
| `FCVOTES <mcc_hex> <bdc_hex>` | Inject VOTE_BITS_MCC and VOTE_BITS_BDC directly. See checkout sequence. |

### FC Symbology Checkout Sequence (Updated v4.3.0)

All values use new VOTE_BITS_MCC gate-chain ordering. VOTE_BITS_BDC b7 = FSM_NOT_LIMITED (SET = clear).

```bash
trc3 OSD ON

# 1. Abort active (NOT_ABORT b0 = 0) — yellow reticle "ABORT"
trc3 FCVOTES 0x00 0x00

# 2. NOT_ABORT set, idle — green reticle, no message
trc3 FCVOTES 0x01 0x00

# 3. Armed (NOT_ABORT + ARMED) — orange reticle "ARMED"
trc3 FCVOTES 0x03 0x00

# --- Trigger pulled interlocks (white reticle) ---

# 4. Trigger (b5), no COMBAT → "INTERLOCK - NOT COMBAT"
trc3 FCVOTES 0x23 0x00

# 5. + COMBAT (SW_VOTE b4), no BDCVote → "INTERLOCK - BDC VOTE"
trc3 FCVOTES 0x33 0x00

# 6. + BDCVote (b2), no LaserTotalHW (b3) → "INTERLOCK - HW GATE"
trc3 FCVOTES 0x37 0x00

# 7. All MCC votes, FSM limited (b7 clear) → "INTERLOCK - FSM LIMIT"
trc3 FCVOTES 0x3F 0x00

# 8. FSM clear (b7=1), above horiz, no LCH → "INTERLOCK - LCH"
trc3 FCVOTES 0x3F 0x80

# 9. In LCH, no KIZ → "INTERLOCK - KIZ"
trc3 FCVOTES 0x3F 0x84

# 10. Below horizon (b0=1), no KIZ → "INTERLOCK - KIZ"
trc3 FCVOTES 0x3F 0x81

# 11. Below horizon + KIZ — silent transitioning (green)
trc3 FCVOTES 0x3F 0x83

# 12. Above horizon, LCH + KIZ — silent transitioning
trc3 FCVOTES 0x3F 0x86

# 13. FIRE_STATE (b6) set, no EMON — "FC ERROR"
trc3 FCVOTES 0x7F 0x80

# 14. All votes + EMON (b7=1) — red reticle "FIRE"
trc3 FCVOTES 0xFF 0x80

# Reset
trc3 FCVOTES 0x00 0x00
trc3 OSD OFF
```

---

## MCC Register 1 (REG1)

Fixed block size: **512-byte payload** (264 defined + 248 reserved).

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 (or 0x00 per FW-C10) |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES enum |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES enum |
| 3 | 3 | 5 | 2 | HB_ms | uint16 | ms between sends |
| 5 | 5 | 7 | 2 | dt_us | uint16 | µs in processing loop |
| 7 | 7 | 8 | 1 | MCC DEVICE_ENABLED_BITS | uint8 | b0:NTP; b1:TMC; b2:HEL; b3:BAT; b4:PTP; b5:CRG; b6:GNSS; b7:BDC |
| 8 | 8 | 9 | 1 | MCC DEVICE_READY_BITS | uint8 | b0:NTP; b1:TMC; b2:HEL; b3:BAT; b4:PTP; b5:CRG; b6:GNSS; b7:BDC |
| 9 | 9 | 10 | 1 | MCC HEALTH_BITS | uint8 | b0:isReady; b1:isChargerEnabled; b2:isNotBatLowVoltage(→VOTE_BITS_MCC2.BAT_NOT_LOW); b3:isTrainingMode(→VOTE_BITS_MCC2.TRAINING_MODE); b4:isLaserModelMatch; b5–7:RES. ⚠ Breaking v3.4.0. Bits 2–3 redirect to VOTE_BITS_MCC2 as authoritative source v4.3.0. |
| 10 | 10 | 11 | 1 | MCC POWER_BITS | uint8 | Bit N = MCC_POWER enum. b0:RELAY_GPS(V1/V3); b1:VICOR_BUS(V1/V3-3kW); b2:RELAY_LASER(all); b3:VICOR_GIM(V2/V3-6kW); b4:VICOR_TMS(V2/V3-6kW); b5:SOL_HEL(V1/V3-3kW); b6:SOL_BDA(V1/V3-3kW); b7:RELAY_NTP(V3 only). ⚠ Breaking v3.4.0. |
| 11 | 11 | 12 | 1 | **VOTE_BITS_MCC** | uint8 | **Gate-chain order b0→b7.** b0:NOT_ABORT(INVERTED-CLEAR=ABORT); b1:ARMED; b2:BDC_VOTE; b3:LASER_TOTAL_HW; b4:SW_VOTE(Combat&&BatNotLow); b5:TRIGGER; b6:FIRE_STATE; b7:EMON(display only). Composites: ARMED_NOMINAL=0x03; READY_TO_FIRE=0x1F; FULL_FIRE_CHAIN=0x7F. ⚠ **Breaking v4.3.0 — previous layout incorrect.** |
| 12 | 12 | 20 | 8 | epoch Time (PTP/NTP) | uint64 | ms since epoch |
| 20 | 20 | 21 | 1 | Temp 1 (Charger) | int8 | °C |
| 21 | 21 | 22 | 1 | Temp 2 (AIR) | int8 | °C |
| 22 | 22 | 26 | 4 | TPH: Temp | float | °C |
| 26 | 26 | 30 | 4 | TPH: Pressure | float | Pa |
| 30 | 30 | 34 | 4 | TPH: Humidity | float | % |
| 34 | 34 | 36 | 2 | Battery Pack Voltage | uint16 | centi-volts |
| 36 | 36 | 38 | 2 | Battery Pack Current | int16 | centi-amps |
| 38 | 38 | 40 | 2 | Battery Bus Voltage | uint16 | centi-volts |
| 40 | 40 | 41 | 1 | Battery Pack Temp | int8 | °C |
| 41 | 41 | 42 | 1 | Battery ASOC | uint8 | % |
| 42 | 42 | 43 | 1 | Battery RSOC | uint8 | % |
| 43 | 43 | 45 | 2 | Battery Status Word | int16 | 16 bits — TBD bit map |
| 45 | 45 | 47 | 2 | Laser HK Voltage | uint16 | centi-volts |
| 47 | 47 | 49 | 2 | Laser Bus Voltage | uint16 | centi-volts |
| 49 | 49 | 50 | 1 | Laser Temperature | int8 | °C |
| 50 | 50 | 54 | 4 | Laser Status Word | uint32 | IPG RMN status |
| 54 | 54 | 58 | 4 | Laser Error Word | uint32 | IPG RMN error |
| 58 | 58 | 62 | 4 | Laser SetPoint | float | % |
| 62 | 62 | 66 | 4 | Laser Output Power | float | W |
| 66 | 66 | 130 | 64 | TMC FULL REG | TMC_REG | 64-byte fixed block — see TMC REG1 |
| 130 | 130 | 131 | 1 | TIME HB | uint8 | s/10 — NTP receive interval |
| 131 | 131 | 132 | 1 | HEL HB | uint8 | s/10 — laser TCP response interval. Stale > 2.0s = comms loss. |
| 132 | 132 | 133 | 1 | BAT HB | uint8 | s/10 |
| 133 | 133 | 134 | 1 | CRG HB | uint8 | s/10 — V1/V3 only |
| 134 | 134 | 135 | 1 | GNSS HB | uint8 | s/10 |
| 135 | 135 | 136 | 1 | GNSS SOLN STATUS | uint8 | NovAtel enum |
| 136 | 136 | 137 | 1 | GNSS POS TYPE | uint8 | NovAtel enum |
| 137 | 137 | 138 | 1 | INS SOLN STATUS | uint8 | NovAtel enum |
| 138 | 138 | 139 | 1 | TERRA STAR SYNC STATE | uint8 | NovAtel enum |
| 139 | 139 | 140 | 1 | SIV | uint8 | satellites in solution |
| 140 | 140 | 141 | 1 | SIS | uint8 | satellites in view |
| 141 | 141 | 149 | 8 | GPS Latitude | double | BESTPOS |
| 149 | 149 | 157 | 8 | GPS Longitude | double | BESTPOS |
| 157 | 157 | 165 | 8 | GPS Altitude | double | BESTPOS |
| 165 | 165 | 169 | 4 | GPS Undulation | float | BESTPOS |
| 169 | 169 | 173 | 4 | GPS Heading | float | 2-ant |
| 173 | 173 | 177 | 4 | GPS Roll | float | INSATTX |
| 177 | 177 | 181 | 4 | GPS Pitch | float | INSATTX |
| 181 | 181 | 185 | 4 | GPS Yaw | float | INSATTX |
| 185 | 185 | 189 | 4 | GPS Latitude STDEV | float | BESTPOS |
| 189 | 189 | 193 | 4 | GPS Longitude STDEV | float | BESTPOS |
| 193 | 193 | 197 | 4 | GPS Altitude STDEV | float | BESTPOS |
| 197 | 197 | 201 | 4 | GPS Heading STDEV | float | 2-ant |
| 201 | 201 | 205 | 4 | GPS Roll STDEV | float | INSATTX |
| 205 | 205 | 209 | 4 | GPS Pitch STDEV | float | INSATTX |
| 209 | 209 | 213 | 4 | GPS Yaw STDEV | float | INSATTX |
| 213 | 213 | 217 | 4 | Charger Voltage input | float | V |
| 217 | 217 | 221 | 4 | Charger Voltage output | float | V |
| 221 | 221 | 225 | 4 | Charger Current output | float | A |
| 225 | 225 | 229 | 4 | Fan1 Speed | float | RPM |
| 229 | 229 | 233 | 4 | Fan2 Speed | float | RPM |
| 233 | 233 | 235 | 2 | CHARGE STATUS | uint16 | enum |
| 235 | 235 | 236 | 1 | CHARGE LEVEL | uint8 | enum |
| 236 | 236 | 240 | 4 | Current Limit | float | A |
| 240 | 240 | 244 | 4 | Voltage Limit | float | V |
| 244 | 244 | 245 | 1 | CHARGER STATUS BITS | uint8 | b0:isConnected; b1:isHealthy; b2:isCharging; b3:isFullyCharged; b4:isHighCharge; b5:is220V |
| 245 | 245 | 249 | 4 | MCC VERSION WORD | uint32 | VERSION_PACK(major, minor, patch) |
| 249 | 249 | 253 | 4 | MCU Temp | float | °C |
| 253 | 253 | 254 | 1 | TIME_BITS | uint8 | b0:isPTP_Enabled; b1:ptp.isSynched; b2:usingPTP; b3:ntp.isSynched; b4:ntpUsingFallback; b5:ntpHasFallback; b6–7:RES |
| 254 | 254 | 255 | 1 | HW_REV | uint8 | 0x01=V1; 0x02=V2; 0x03=V3. Read before interpreting HEALTH_BITS [9] and POWER_BITS [10]. |
| 255 | 255 | 256 | 1 | LASER_MODEL | uint8 | 0x00=UNKNOWN; 0x01=YLM_3K; 0x02=YLM_6K. Populated after auto-sense on connect. |
| 256 | 256 | 257 | 1 | **VOTE_BITS_MCC2** | uint8 | MCC detail byte. b0:BAT_NOT_LOW; b1:TRAINING_MODE; b2:COMBAT; b3:EMON_MISSING; b4:EMON_UNEXPECTED; b5:FIRE_INTERLOCKED; b6–7:RES. SW_VOTE = b2 AND b0. FC-CONSISTENCY-1 closed. **New v4.3.0.** |
| 257 | 257 | 258 | 1 | **MCC DEVICE_WARN_BITS** | uint8 | Same bit layout as ENABLED/READY. b0:NTP; b1:TMC; b2:HEL; b3:BAT; b4:PTP; b5:CRG; b6:GNSS; b7:BDC. Only set when corresponding ENABLED bit is 1. **New v4.3.0.** |
| 258 | 258 | 259 | 1 | **MCC_TMC_STATUS_BITS** | uint8 | b0:isConnected; b1:isPump1Enabled; b2:isPump2Enabled(V2/V3-0 on V1); b3:isLCM1_Error; b4:isFlow1_Error; b5:isLCM2_Error; b6:isFlow2_Error; b7:RES. See STATUS_BITS section. **New v4.3.0.** |
| 259 | 259 | 260 | 1 | **MCC_HEL_STATUS_BITS** | uint8 | b0:isSensed; b1:isHB_OK; b2:isNOTREADY(set=error); b3:isModelMatch; b4:isEMON(display); b5:isEMON_Unexpected; b6:isEMON_Missing; b7:isFireInterlocked. See STATUS_BITS section. **New v4.3.0.** |
| 260 | 260 | 261 | 1 | **MCC_BAT_STATUS_BITS** | uint8 | b0:isConnected; b1:isNotLowVoltage; b2:isCharging(display); b3:isDischarging(display); b4:isSOC_OK; b5:isTempOK; b6:isError; b7:isAlarm. **New v4.3.0.** |
| 261 | 261 | 262 | 1 | **MCC_CRG_STATUS_BITS** | uint8 | b0:isConnected; b1:isEnabled; b2:isVIN_OK; b3:isCharging(display); b4:isAtMaxLevel(display); b5–7:RES. **New v4.3.0.** |
| 262 | 262 | 263 | 1 | **MCC_GNSS_STATUS_BITS** | uint8 | b0:isConnected; b1:isHB_OK; b2:isPositionValid; b3:isSIV_OK; b4:isHeadingValid; b5:isINS_Converged; b6:isTerraStar_OK(display); b7:RES. **New v4.3.0.** |
| 263 | 263 | 264 | 1 | **MCC_BDC_STATUS_BITS** | uint8 | b0:isEnabled; b1:isReachable; b2:isVoteActive; b3–7:RES. **New v4.3.0.** |
| 264 | 264 | 512 | 248 | RESERVED | — | 0x00 |

**Defined: 264 bytes. Reserved: 248 bytes. Payload: 512 bytes.**

---

## BDC Register 1 (REG1)

Fixed block size: **512 bytes**. Embedded sub-registers:
- **TRC_REG** (64-byte block) at bytes **60–123**
- **FMC_REG** (64-byte block) at bytes **169–232**

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES enum |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES enum |
| 3 | 3 | 4 | 1 | Active CAM ID | uint8 | VIS=0, MWIR=1 |
| 4 | 4 | 6 | 2 | HB_ms | uint16 | ms between sends |
| 6 | 6 | 8 | 2 | dt_us | uint16 | µs in processing loop |
| 8 | 8 | 9 | 1 | BDC DEVICE_ENABLED_BITS | uint8 | b0:NTP; b1:GIMBAL; b2:FUJI; b3:MWIR; b4:FSM; b5:JETSON; b6:INCL; b7:PTP |
| 9 | 9 | 10 | 1 | BDC DEVICE_READY_BITS | uint8 | b0:NTP; b1:GIMBAL; b2:FUJI; b3:MWIR; b4:FSM; b5:JETSON; b6:INCL; b7:PTP |
| 10 | 10 | 11 | 1 | BDC HEALTH_BITS | uint8 | b0:isReady; b1:isSwitchEnabled(V2 only); b2–7:RES. ⚠ Renamed v3.5.1. |
| 11 | 11 | 12 | 1 | BDC POWER_BITS | uint8 | b0:isPidEnabled; b1:isVPidEnabled; b2:isFTTrackEnabled; b3:isVicorEnabled; b4:isRelay1En; b5:isRelay2En; b6:isRelay3En; b7:isRelay4En. ⚠ Renamed v3.5.1. |
| 12 | 12 | 20 | 8 | epoch Time (PTP/NTP) | uint64 | ms since epoch |
| 20 | 20 | 21 | 1 | GIMBAL STATUS BITS | uint8 | b0:isReady; b1:isConnected; b2:isStarted; b3–7:RES |
| 21 | 21 | 25 | 4 | Gimbal Pan Count | int32 | from Galil (dr) |
| 25 | 25 | 29 | 4 | Gimbal Tilt Count | int32 | from Galil (dr) |
| 29 | 29 | 33 | 4 | Gimbal Pan Speed | int32 | from Galil (dr) |
| 33 | 33 | 37 | 4 | Gimbal Tilt Speed | int32 | from Galil (dr) |
| 37 | 37 | 38 | 1 | Gimbal Pan Stop Code | uint8 | from Galil (dr) |
| 38 | 38 | 39 | 1 | Gimbal Tilt Stop Code | uint8 | from Galil (dr) |
| 39 | 39 | 41 | 2 | Gimbal Pan Status | uint16 | from Galil (dr) |
| 41 | 41 | 43 | 2 | Gimbal Tilt Status | uint16 | from Galil (dr) |
| 43 | 43 | 47 | 4 | Gimbal Pan Rel Angle | float | deg from home |
| 47 | 47 | 51 | 4 | Gimbal Tilt Rel Angle | float | deg from home |
| 51 | 51 | 55 | 4 | Gimbal Az NED Angle | float | AZ NED deg |
| 55 | 55 | 59 | 4 | Gimbal EL NED Angle | float | EL NED deg |
| 59 | 59 | 60 | 1 | TRC STATUS BITS | uint8 | b0:isReady; b1:isConnected; b2:isStarted; b3–7:RES |
| **60** | **60** | **124** | **64** | **TRC REGISTER** | **TRC_REG** | **64-byte fixed block — see TRC REG1** |
| 124 | 124 | 128 | 4 | Gimbal Base Pitch | float | from inclinometer ° |
| 128 | 128 | 132 | 4 | Gimbal Base Roll | float | from inclinometer ° |
| 132 | 132 | 133 | 1 | Vicor Temp | int8 | °C |
| 133 | 133 | 137 | 4 | TPH: Temp | float | °C |
| 137 | 137 | 141 | 4 | TPH: Pressure | float | Pa |
| 141 | 141 | 145 | 4 | TPH: Humidity | float | % |
| 145 | 145 | 146 | 1 | MWIR RUN STATE | uint8 | 0=BOOT; 1=WARMUP_WAIT; 2=WARMUP_VRFY; 3=LENS_INIT; 4=COOLDOWN_WAIT; 5=COOLDOWN_VRFY; 6=SNSR_INIT; 7=MAIN_PROC_LOOP; 8=LENS_REINIT |
| 146 | 146 | 150 | 4 | MWIR Temp 0 | float | sensor 0 °C |
| 150 | 150 | 154 | 4 | MWIR FPA Temp | float | FPA °C |
| 154 | 154 | 155 | 1 | MWIR FOV Selection RB | uint8 | current FOV readback |
| 155 | 155 | 159 | 4 | MWIR FOV | float | degrees |
| 159 | 159 | 160 | 1 | VIS FOV Selection RB | uint8 | current FOV readback |
| 160 | 160 | 164 | 4 | VIS FOV | float | degrees |
| 164 | 164 | 165 | 1 | **VOTE_BITS_BDC2** | uint8 | BDC raw/override detail. b0:HORIZ_VOTE_OVERRIDE; b1:KIZ_VOTE_OVERRIDE; b2:LCH_VOTE_OVERRIDE; b3:BDC_VOTE_OVERRIDE; b4:IS_BELOW_HORIZ(raw before override); b5:IS_IN_KIZ; b6:IS_IN_LCH; b7:RES. ⚠ **Renamed v4.3.0** (was BDC VOTE BITS1). |
| 165 | 165 | 166 | 1 | **VOTE_BITS_BDC** | uint8 | BDC computed vote summary. b0:BELOW_HORIZ_VOTE; b1:IN_KIZ_VOTE; b2:IN_LCH_VOTE; b3:BDC_TOTAL_VOTE; b4:RES; b5:HORIZ_LOADED; b6:RES; b7:FSM_NOT_LIMITED(SET=FSM clear). ⚠ **Renamed v4.3.0** (was BDC VOTE BITS2). b7 name corrected — was `isFSMLimited` (inverted sense). |
| 166 | 166 | 167 | 1 | **VOTE_BITS_MCC_RB** | uint8 | MCC gate-chain readback. Same bit layout as VOTE_BITS_MCC [11]. b0:NOT_ABORT(INVERTED); b1:ARMED; b2:BDC_VOTE; b3:LASER_TOTAL_HW; b4:SW_VOTE; b5:TRIGGER; b6:FIRE_STATE; b7:EMON. ⚠ **Renamed v4.3.0** (was MCC VOTE BITS RB). |
| 167 | 167 | 168 | 1 | VOTE_BITS_KIZ | uint8 | b0:isLoaded; b1:isEnabled; b2:isTimeValid; b3:isOperatorValid; b4:isPositionValid; b5:isForExec; b6:isInKIZ; b7:InKIZVote |
| 168 | 168 | 169 | 1 | VOTE_BITS_LCH | uint8 | b0:isLoaded; b1:isEnabled; b2:isTimeValid; b3:isOperatorValid; b4:isPositionValid; b5:isForExec; b6:isInLCH; b7:InLCHVote |
| **169** | **169** | **233** | **64** | **FMC REGISTER** | **FMC_REG** | **64-byte fixed block — see FMC REG1** |
| 233 | 233 | 235 | 2 | FSM_X | int16 | commanded FSM X |
| 235 | 235 | 237 | 2 | FSM_Y | int16 | commanded FSM Y |
| 237 | 237 | 241 | 4 | Gimbal Home X | int32 | home encoder X |
| 241 | 241 | 245 | 4 | Gimbal Home Y | int32 | home encoder Y |
| 245 | 245 | 253 | 8 | Platform Latitude | double | latched |
| 253 | 253 | 261 | 8 | Platform Longitude | double | latched |
| 261 | 261 | 265 | 4 | Platform Altitude | float | latched HAE |
| 265 | 265 | 269 | 4 | Platform Roll | float | degrees |
| 269 | 269 | 273 | 4 | Platform Pitch | float | degrees |
| 273 | 273 | 277 | 4 | Platform Yaw | float | degrees |
| 277 | 277 | 281 | 4 | Target Pan (Cue Track) | int32 | encoder counts |
| 281 | 281 | 285 | 4 | Target Tilt (Cue Track) | int32 | encoder counts |
| 285 | 285 | 289 | 4 | pan kp cue | float | |
| 289 | 289 | 293 | 4 | pan ki cue | float | |
| 293 | 293 | 297 | 4 | pan kd cue | float | |
| 297 | 297 | 301 | 4 | tilt kp cue | float | |
| 301 | 301 | 305 | 4 | tilt ki cue | float | |
| 305 | 305 | 309 | 4 | tilt kd cue | float | |
| 309 | 309 | 313 | 4 | pan kp video | float | |
| 313 | 313 | 317 | 4 | pan ki video | float | |
| 317 | 317 | 321 | 4 | pan kd video | float | |
| 321 | 321 | 325 | 4 | tilt kp video | float | |
| 325 | 325 | 329 | 4 | tilt ki video | float | |
| 329 | 329 | 333 | 4 | tilt kd video | float | |
| 333 | 333 | 341 | 8 | iFOV_FSM_X_DEG_COUNT | double | |
| 341 | 341 | 349 | 8 | iFOV_FSM_Y_DEG_COUNT | double | |
| 349 | 349 | 351 | 2 | FSM_X0 | int16 | |
| 351 | 351 | 353 | 2 | FSM_Y0 | int16 | |
| 353 | 353 | 354 | 1 | FSM_X_SIGN | int8 | |
| 354 | 354 | 355 | 1 | FSM_Y_SIGN | int8 | |
| 355 | 355 | 359 | 4 | STAGE_POSITION | uint32 | |
| 359 | 359 | 363 | 4 | STAGE_HOME | uint32 | |
| 363 | 363 | 367 | 4 | FSM_NED_AZ_RB | float | from readback (noisy) |
| 367 | 367 | 371 | 4 | FSM_NED_EL_RB | float | from readback |
| 371 | 371 | 375 | 4 | FSM_NED_AZ_C | float | from command |
| 375 | 375 | 379 | 4 | FSM_NED_EL_C | float | from command |
| 379 | 379 | 383 | 4 | HORIZON_BUFFER | float | |
| 383 | 383 | 387 | 4 | BDC VERSION WORD | uint32 | VERSION_PACK |
| 387 | 387 | 391 | 4 | MCU Temp | float | °C |
| 391 | 391 | 392 | 1 | TIME_BITS | uint8 | b0:isPTP_Enabled; b1:ptp.isSynched; b2:usingPTP; b3:ntp.isSynched; b4:ntpUsingFallback; b5:ntpHasFallback; b6–7:RES |
| 392 | 392 | 393 | 1 | HW_REV | uint8 | 0x01=V1; 0x02=V2. Read before interpreting HEALTH_BITS [10] bit 1. |
| 393 | 393 | 394 | 1 | TEMP_RELAY | int8 | °C. V2 live; V1 always 0x00. |
| 394 | 394 | 395 | 1 | TEMP_BAT | int8 | °C. V2 live; V1 always 0x00. |
| 395 | 395 | 396 | 1 | TEMP_USB | int8 | °C. V2 live; V1 always 0x00. |
| 396 | 396 | 397 | 1 | HB_NTP | uint8 | x0.1s units (C# /10.0 → seconds) |
| 397 | 397 | 398 | 1 | HB_FMC_ms | uint8 | raw ms, saturates at 255 |
| 398 | 398 | 399 | 1 | HB_TRC_ms | uint8 | raw ms, saturates at 255 |
| 399 | 399 | 400 | 1 | HB_MCC_ms | uint8 | raw ms since last 0xE0 RX, saturates at 255 |
| 400 | 400 | 401 | 1 | HB_GIM_ms | uint8 | raw ms, saturates at 255 |
| 401 | 401 | 402 | 1 | HB_FUJI_ms | uint8 | raw ms, saturates at 255 |
| 402 | 402 | 403 | 1 | HB_MWIR_ms | uint8 | raw ms, saturates at 255 |
| 403 | 403 | 404 | 1 | HB_INCL_ms | uint8 | raw ms, saturates at 255. ⚠ INCL-HB-SCALE: saturates at 255ms for 1s poll interval. |
| 404 | 404 | 405 | 1 | **VOTE_BITS_MCC2_RB** | uint8 | MCC2 detail readback. Same layout as MCC VOTE_BITS_MCC2 [256]. **New v4.3.0.** |
| 405 | 405 | 406 | 1 | **BDC DEVICE_WARN_BITS** | uint8 | b0:NTP; b1:GIMBAL; b2:FUJI; b3:MWIR; b4:FSM; b5:JETSON; b6:INCL; b7:PTP. Only set when ENABLED bit is 1. **New v4.3.0.** |
| 406 | 406 | 407 | 1 | **BDC_GIM_STATUS_BITS** | uint8 | b0:isConnected; b1:isReady; b2:isStarted; b3:isAtSoftLimit(TBD); b4:isMoving(display); b5:isFault(TBD); b6–7:RES. **New v4.3.0.** |
| 407 | 407 | 408 | 1 | **BDC_VIS_STATUS_BITS** | uint8 | b0:isFuji_Connected; b1:isFuji_HB_OK; b2:isFOV_Valid; b3:isAlvium_Powered; b4:isAlvium_Connected; b5:isCapturing; b6:isAlvium_TempOK(TBD threshold); b7:RES. **New v4.3.0.** |
| 408 | 408 | 409 | 1 | **BDC_MWIR_STATUS_BITS** | uint8 | b0:isMWIR_Connected; b1:isHB_OK; b2:isWarmupComplete; b3:isFOV_Valid; b4:isCapturing; b5:isFPA_TempOK(TBD); b6–7:RES. **New v4.3.0.** |
| 409 | 409 | 410 | 1 | **BDC_FSM_STATUS_BITS** | uint8 | b0:isFMC_Connected; b1:isHB_OK; b2:isFSM_Powered; b3:isNotLimited; b4:isAtHome(display,TBD tolerance); b5–7:RES. **New v4.3.0.** |
| 410 | 410 | 411 | 1 | **BDC_JET_STATUS_BITS** | uint8 | b0:isConnected; b1:isReady; b2:isStarted; b3:isStreaming; b4:isCPU_OK(≤90%); b5:isGPU_OK(≤90%); b6:isCPU_TempOK(≤85°C); b7:isGPU_TempOK(≤85°C). WARN thresholds (50%/70°C) drive DEVICE_WARN_BITS only. **New v4.3.0.** |
| 411 | 411 | 412 | 1 | **BDC_INCL_STATUS_BITS** | uint8 | b0:isConnected; b1:isHB_OK; b2:isDataValid; b3:isLevel(TBD bounds); b4–7:RES. **New v4.3.0.** |
| 412 | 412 | 512 | 100 | RESERVED | — | 0x00 |

**Defined: 412 bytes. Reserved: 100 bytes. Fixed block: 512 bytes.**

---

## TMC Register 1 (REG1)

64-byte fixed block. Embedded in MCC REG1 at bytes [66–129].

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES enum |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES enum |
| 3 | 3 | 5 | 2 | HB_ms | uint16 | ms between sends |
| 5 | 5 | 7 | 2 | dt_us | uint16 | µs |
| 7 | 7 | 8 | 1 | TMC STAT BITS1 | uint8 | b0:isReady; b1:isPump1Enabled; b2:isHeaterEnabled(V1 only); b3:isInputFan1Enabled; b4:isInputFan2Enabled; b5:isPump2Enabled(V2/V3 read HW_REV[62]); b6:isSingleLoop; b7:RES |
| 8 | 8 | 9 | 1 | TMC STAT BITS2 | uint8 | b0:isVicor1Enabled; b1:isLCM1Enabled; b2:isLCM1Error; b3:isFlow1Error; b4:isVicor2Enabled; b5:isLCM2Enabled; b6:isLCM2Error; b7:isFlow2Error |
| 9 | 9 | 17 | 8 | epoch Time (PTP/NTP) | uint64 | ms since Unix epoch |
| 17 | 17 | 19 | 2 | Pump Speed | uint16 | V1: DAC counts [0–800]; V2: 0x0000 reserved |
| 19 | 19 | 21 | 2 | LCM1 Speed Setting | uint16 | DAC counts [0–4095] |
| 21 | 21 | 23 | 2 | LCM1 Current Readback | uint16 | [0–4095] |
| 23 | 23 | 25 | 2 | LCM2 Speed Setting | uint16 | DAC counts [0–4095] |
| 25 | 25 | 27 | 2 | LCM2 Current Readback | uint16 | [0–4095] |
| 27 | 27 | 28 | 1 | f1 | uint8 | flow rate ×10 LPM |
| 28 | 28 | 29 | 1 | f2 | uint8 | flow rate ×10 LPM |
| 29 | 29 | 30 | 1 | tt | int8 | target temp setpoint °C [10–40] |
| 30 | 30 | 31 | 1 | ta1 | int8 | air temp 1 °C |
| 31 | 31 | 32 | 1 | tf1 | int8 | °C |
| 32 | 32 | 33 | 1 | tf2 | int8 | °C |
| 33 | 33 | 34 | 1 | tc1 | int8 | temp compressor 1 °C |
| 34 | 34 | 35 | 1 | tc2 | int8 | temp compressor 2 °C |
| 35 | 35 | 36 | 1 | to1 | int8 | temp output ch1 °C |
| 36 | 36 | 37 | 1 | to2 | int8 | temp output ch2 °C |
| 37 | 37 | 38 | 1 | tv1 | int8 | temp vicor LCM1 °C |
| 38 | 38 | 39 | 1 | tv2 | int8 | temp vicor LCM2 °C |
| 39 | 39 | 40 | 1 | tv3 | int8 | V1: vicor heater °C; V2: 0x00 reserved |
| 40 | 40 | 41 | 1 | tv4 | int8 | V1: vicor pump °C; V2: 0x00 reserved |
| 41 | 41 | 45 | 4 | TPH: Temp | float | °C |
| 45 | 45 | 49 | 4 | TPH: Pressure | float | Pa |
| 49 | 49 | 53 | 4 | TPH: Humidity | float | % |
| 53 | 53 | 57 | 4 | TMC VERSION WORD | uint32 | VERSION_PACK |
| 57 | 57 | 61 | 4 | MCU Temp | float | °C |
| 61 | 61 | 62 | 1 | TMC STAT BITS3 | uint8 | b0:isPTP_Enabled; b1:isPTP_Synched; b2:usingPTP; b3:isNTPSynched; b4:ntpUsingFallback; b5:ntpHasFallback; b6–7:RES |
| 62 | 62 | 63 | 1 | HW_REV | uint8 | 0x01=V1; 0x02=V2. Read before interpreting STAT BITS1 b5 and bytes [17–18], [39–40]. |
| 63 | 63 | 64 | 1 | RESERVED | — | 0x00 |

**Defined: 63 bytes. Reserved: 1 byte. Fixed block: 64 bytes.**

---

## FMC Register 1 (REG1)

64-byte fixed block. Embedded in BDC REG1 at bytes [169–232].

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES enum |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES enum |
| 3 | 3 | 5 | 2 | HB_ms | uint16 | ms |
| 5 | 5 | 7 | 2 | dt_us | uint16 | µs |
| 7 | 7 | 8 | 1 | FMC HEALTH_BITS | uint8 | b0:isReady; b1–7:RES. ⚠ Breaking v3.5.2 — was FSM STAT BITS. |
| 8 | 8 | 16 | 8 | epoch Time (PTP/NTP) | uint64 | ms |
| 16 | 16 | 20 | 4 | FSM Pos X | int32 | ADC readback |
| 20 | 20 | 24 | 4 | FSM Pos Y | int32 | ADC readback |
| 24 | 24 | 28 | 4 | FSM Cmd X | int32 | commanded |
| 28 | 28 | 32 | 4 | FSM Cmd Y | int32 | commanded |
| 32 | 32 | 40 | 8 | epoch Time | uint64 | ms |
| 40 | 40 | 44 | 4 | FMC VERSION WORD | uint32 | VERSION_PACK |
| 44 | 44 | 45 | 4 | MCU Temp | float | °C |
| 45 | 45 | 46 | 1 | HW_REV | uint8 | 0x01=V1(SAMD21); 0x02=V2(STM32F7/OpenCR). ⚠ v3.5.2. |
| 46 | 46 | 47 | 1 | FMC POWER_BITS | uint8 | b0:isFSM_Powered; b1:isStageEnabled; b2–7:RES. ⚠ v3.5.2. |
| 47 | 47 | 64 | 17 | RESERVED | — | 0x00 |

**Defined: 47 bytes. Reserved: 17 bytes. Fixed block: 64 bytes.**

---

## TRC Register 1 (REG1)

64-byte fixed block. Embedded in BDC REG1 at bytes [60–123]. Also sent directly by TRC (ASCII port telemetry).

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES |
| 3 | 3 | 5 | 2 | HB_ms | uint16 | ms |
| 5 | 5 | 7 | 2 | dt_us | uint16 | µs |
| 7 | 7 | 8 | 1 | TRC STATUS BITS0 | uint8 | b0:isReady; b1:isConnected; b2:isStarted; b3–7:RES |
| 8 | 8 | 9 | 1 | TRC STATUS BITS1 | uint8 | b0:isTracking; b1:trackGateValid; b2:trackTargetLocked; b3–7:RES |
| 9 | 9 | 13 | 4 | Track gate X | int32 | px |
| 13 | 13 | 17 | 4 | Track gate Y | int32 | px |
| 17 | 17 | 21 | 4 | Track gate W | int32 | px |
| 21 | 21 | 25 | 4 | Track gate H | int32 | px |
| 25 | 25 | 29 | 4 | Track target X | int32 | px |
| 29 | 29 | 33 | 4 | Track target Y | int32 | px |
| 33 | 33 | 37 | 4 | TRC VERSION WORD | uint32 | VERSION_PACK |
| 37 | 37 | 41 | 4 | streamFPS | float | current H.264 encode FPS |
| 41 | 41 | 42 | 1 | **voteBitsMcc** | uint8 | VOTE_BITS_MCC readback from 0xE0. New gate-chain layout. **Updated v4.3.0.** |
| 42 | 42 | 43 | 1 | **voteBitsBdc** | uint8 | VOTE_BITS_BDC readback from 0xE0. **Updated v4.3.0.** |
| 43 | 43 | 45 | 2 | nccScore | int16 | NCC score ×10000 |
| 45 | 45 | 46 | 1 | jetsonTemp | uint8 | CPU die temp °C (thermal_zone0/1000). **Compacted v4.2.0** (was int16). |
| 46 | 46 | 47 | 1 | jetsonCpuLoad | uint8 | CPU load % (/proc/stat). **Compacted v4.2.0**. |
| 47 | 47 | 48 | 1 | jetsonGpuLoad | uint8 | GPU load % (gpu.0/load÷10). **Compacted+moved v4.2.0**. |
| 48 | 48 | 49 | 1 | jetsonGpuTemp | uint8 | GPU temp °C (thermal_zone1/1000). **New v4.2.0.** |
| 49 | 49 | 57 | 8 | som_serial | uint64 | Jetson SOM serial number |
| 57 | 57 | 58 | 1 | **voteBitsMcc2** | uint8 | VOTE_BITS_MCC2 readback from 0xE0. **New v4.3.0.** |
| 58 | 58 | 59 | 1 | **voteBitsBdc2** | uint8 | VOTE_BITS_BDC2 readback from 0xE0. **New v4.3.0.** |
| 59 | 59 | 64 | 5 | RESERVED | — | 0x00 |

**Defined: 59 bytes. Reserved: 5 bytes. Fixed block: 64 bytes.**

---

## Device STATUS_BITS — Full Decode Reference

### Architecture

| Register | Set when | Source |
|----------|----------|--------|
| `DEVICE_ENABLED_BITS` bit N | Device is in service | Firmware config / operator |
| `DEVICE_READY_BITS` bit N | Device fully operational | Derived from STATUS_BITS |
| `DEVICE_WARN_BITS` bit N | Device degraded, still operational | Derived from STATUS_BITS |

**WARN bit N is never set if ENABLED bit N is 0.**

### MCC Device STATUS_BITS

#### MCC_TMC_STATUS_BITS [258]

Device: Thermal Management Controller (Lytron chiller). Mirrors TMC STAT_BITS1/2.

| Bit | Name | Mirrors | ERROR gate |
|-----|------|---------|------------|
| b0 | `isConnected` | Controller computed | Yes |
| b1 | `isPump1Enabled` | STAT_BITS1 b1 | Yes |
| b2 | `isPump2Enabled` | STAT_BITS1 b5 (V2/V3; 0 on V1) | Yes (if V2/V3) |
| b3 | `isLCM1_Error` | STAT_BITS2 b2 | WARN if single, ERROR if both |
| b4 | `isFlow1_Error` | STAT_BITS2 b3 | Yes |
| b5 | `isLCM2_Error` | STAT_BITS2 b6 | WARN if single, ERROR if both |
| b6 | `isFlow2_Error` | STAT_BITS2 b7 | Yes |
| b7 | RES | — | — |

```
ERROR: !isConnected
       OR (!isPump1Enabled AND !isPump2Enabled)
       OR (isLCM1_Error AND isLCM2_Error)
       OR isFlow1_Error OR isFlow2_Error

READY+WARN: isConnected AND pump(s) up AND cooling path available AND something degraded

READY: isConnected AND isPump1Enabled AND (isPump2Enabled OR V1_hw)
       AND !isLCM1_Error AND !isLCM2_Error
       AND !isFlow1_Error AND !isFlow2_Error
```

> ⚠️ TBD: Cooling requirement by HW — V2/V3·6kW only, or V1 also?

#### MCC_HEL_STATUS_BITS [259]

Device: IPG Photonics laser. FC-CONSISTENCY-1 bits now implemented.

| Bit | Name | Source | Notes |
|-----|------|--------|-------|
| b0 | `isSensed` | `IPGMsg.IsSensed` | ERROR gate |
| b1 | `isHB_OK` | `HB_HEL_ms > 0 && < 255` | ERROR gate |
| b2 | `isNOTREADY` | `IPGMsg.IsNotReady` (3K=bit9, 6K=bit11) | ERROR gate — set=error |
| b3 | `isModelMatch` | `HEALTH_BITS` bit 4 | ERROR gate |
| b4 | `isEMON` | `IPGMsg.IsEMON` (model-aware) | **Display only** |
| b5 | `isEMON_Unexpected` | `!isFireExpected && ipg.isEMON()` | ERROR gate |
| b6 | `isEMON_Missing` | `isFireExpected && !ipg.isEMON()` | ERROR gate |
| b7 | `isFireInterlocked` | `isLaserFireRequested && !isFireExpected && !isEMON` | WARN gate |

```
isFireExpected = (VOTE_BITS_MCC & FULL_FIRE_CHAIN) == FULL_FIRE_CHAIN  // = 0x7F

ERROR: !isSensed || !isHB_OK || isNOTREADY || !isModelMatch
       OR isEMON_Unexpected OR isEMON_Missing

READY+WARN: all ERROR clear AND (isFireInterlocked OR isTrainingMode)

READY: all ERROR clear AND !isFireInterlocked AND !isTrainingMode
```

> ⚠️ TBD: IPGMsg StatusWord temperature bit positions — 3K and 6K full StatusWord map needed.
> `isTrainingMode` from `VOTE_BITS_MCC2.TRAINING_MODE`.

#### MCC_BAT_STATUS_BITS [260]

Device: Battery pack (RS485 BMS).

| Bit | Name | Source | Notes |
|-----|------|--------|-------|
| b0 | `isConnected` | `isBAT_DeviceReady` | ERROR gate |
| b1 | `isNotLowVoltage` | Voltage threshold (48V: 47V / 300V: 266V) | Vote gate / STATE gate |
| b2 | `isCharging` | `PackCurrent > 0` | **Display only** |
| b3 | `isDischarging` | `PackCurrent < 0` | **Display only** |
| b4 | `isSOC_OK` | `RSOC > warn_threshold (TBD)` | WARN gate |
| b5 | `isTempOK` | `PackTemp` within bounds (TBD) | WARN gate |
| b6 | `isError` | `StatusWord` error bits (TBD) | ERROR gate |
| b7 | `isAlarm` | `StatusWord` alarm bits (TBD) | WARN gate |

```
ERROR: !isConnected OR isError

READY+WARN: isConnected AND !isError AND isNotLowVoltage
            AND (!isSOC_OK OR !isTempOK OR isAlarm)

READY: isConnected AND !isError AND isNotLowVoltage AND isSOC_OK AND isTempOK AND !isAlarm
```

> ⚠️ TBD: RSOC warn threshold (suggest 20%). PackTemp bounds from BMS datasheet. MSG_BATTERY StatusWord error/alarm bit map.

#### MCC_CRG_STATUS_BITS [261]

Device: Battery charger (V1: Delta DBU I2C / V2: GPIO only).

| Bit | Name | Source | Notes |
|-----|------|--------|-------|
| b0 | `isConnected` | `isCRG_DeviceReady` | ERROR gate |
| b1 | `isEnabled` | `isCharger_Enabled` | WARN gate |
| b2 | `isVIN_OK` | `CMCMsg.VIN > threshold (TBD)` | WARN gate |
| b3 | `isCharging` | `CMCMsg.IOUT > 0` | **Display only** |
| b4 | `isAtMaxLevel` | `ChargeLevel == HI` | **Display only** |
| b5–7 | RES | — | — |

```
ERROR: !isConnected

READY+WARN: isConnected AND (!isEnabled OR !isVIN_OK)

READY: isConnected AND isEnabled AND isVIN_OK
```

> CRG ERROR has no state/mode consequence — battery voltage is the gate.
> V2: bits 3–4 always 0 (GPIO only). Bits 0–2 valid.
> ⚠️ TBD: VIN threshold (V).

#### MCC_GNSS_STATUS_BITS [262]

Device: NovAtel GNSS receiver.

| Bit | Name | Source | Notes |
|-----|------|--------|-------|
| b0 | `isConnected` | `isGNSS_DeviceReady` | ERROR gate |
| b1 | `isHB_OK` | `HB_GNSS_ms > 0 && < 255` | ERROR gate |
| b2 | `isPositionValid` | `GNSSMsg.LatestsolStatus == SOL_COMPUTED` | ERROR gate |
| b3 | `isSIV_OK` | `GNSSMsg.SIV >= 4` | WARN gate |
| b4 | `isHeadingValid` | `Heading_STDEV < threshold (TBD)` | WARN gate |
| b5 | `isINS_Converged` | PosType is INS solution type (TBD enum values) | WARN gate |
| b6 | `isTerraStar_OK` | TerraStar sync state nominal | **Display only** |
| b7 | RES | — | — |

```
ERROR: !isConnected OR !isHB_OK OR !isPositionValid

READY+WARN: isConnected AND isHB_OK AND isPositionValid
            AND (!isSIV_OK OR !isHeadingValid OR !isINS_Converged)

READY: isConnected AND isHB_OK AND isPositionValid
       AND isSIV_OK AND isHeadingValid AND isINS_Converged
```

> GNSS ERROR has no state/mode consequence. Position latched on last valid fix.
> ⚠️ TBD: Heading_STDEV threshold. INS converged PosType enum values. TerraStar availability.

#### MCC_BDC_STATUS_BITS [263]

Device: BDC controller (MCC A1 fire control link).

| Bit | Name | Source | Notes |
|-----|------|--------|-------|
| b0 | `isEnabled` | `isBDC_DeviceEnabled` | ERROR gate |
| b1 | `isReachable` | `endPacket()` result on A1 send | ERROR gate |
| b2 | `isVoteActive` | BDC vote echo round-trip (TBD mechanism) | WARN gate during ISR / ERROR during COMBAT |
| b3–7 | RES | — | — |

```
ERROR: !isEnabled OR !isReachable OR (!isVoteActive AND isCOMBAT)

READY+WARN: isEnabled AND isReachable AND !isVoteActive AND !isCOMBAT

READY: isEnabled AND isReachable AND isVoteActive
```

> MCC_BDC ERROR → COMBAT→ISR only. Does NOT block STNDBY→ISR.
> ⚠️ TBD: `isVoteActive` tracking mechanism — `HB_MCC_ms` echo or separate counter.

---

### BDC Device STATUS_BITS

#### BDC_GIM_STATUS_BITS [406]

Device: Galil motion controller. Mirrors StatusX/Y, StopCodeX/Y.

| Bit | Name | Source | Notes |
|-----|------|--------|-------|
| b0 | `isConnected` | `gimbalMSG.isConnected` | ERROR gate |
| b1 | `isReady` | `gimbalMSG.isReady` | ERROR gate |
| b2 | `isStarted` | `gimbalMSG.isStarted` | ERROR gate |
| b3 | `isAtSoftLimit` | StatusX/Y or StopCode (TBD) | WARN gate |
| b4 | `isMoving` | `SpeedX != 0 \|\| SpeedY != 0` | **Display only** |
| b5 | `isFault` | StopCode fault bits (TBD) | ERROR gate |
| b6–7 | RES | — | — |

```
ERROR: !isConnected OR !isReady OR !isStarted OR isFault

READY+WARN: isConnected AND isReady AND isStarted AND !isFault AND isAtSoftLimit

READY: isConnected AND isReady AND isStarted AND !isFault AND !isAtSoftLimit
```

> ⚠️ TBD: Galil StatusX/Y and StopCode bit map — isFault vs isAtSoftLimit.

#### BDC_VIS_STATUS_BITS [407]

Device: VIS channel — Fuji lens + Alvium sensor.

| Bit | Name | Source | Notes |
|-----|------|--------|-------|
| b0 | `isFuji_Connected` | `isFuji_DeviceReady` | ERROR gate |
| b1 | `isFuji_HB_OK` | `HB_FUJI_ms > 0 && < 255` | WARN gate |
| b2 | `isFOV_Valid` | `VIS_FOV > 0` | WARN gate; jog safety gate |
| b3 | `isAlvium_Powered` | `trcMSG.isVIS_Powered` | ERROR gate |
| b4 | `isAlvium_Connected` | `trcMSG.isVIS_Connected` | ERROR gate |
| b5 | `isCapturing` | `trcMSG.isVIS_Capturing` | ERROR gate |
| b6 | `isAlvium_TempOK` | `deviceTemperature <= threshold (TBD)` | WARN gate |
| b7 | RES | — | — |

```
ERROR: !isFuji_Connected OR !isAlvium_Powered OR !isAlvium_Connected OR !isCapturing

READY+WARN: isFuji_Connected AND isAlvium_Powered AND isAlvium_Connected AND isCapturing
            AND (!isFuji_HB_OK OR !isFOV_Valid OR !isAlvium_TempOK)

READY: all ERROR clear AND isFuji_HB_OK AND isFOV_Valid AND isAlvium_TempOK
```

> `isFOV_Valid`: controller-level gate — BDC rejects jog commands when FOV unknown.
> ⚠️ TBD: Alvium max operating temperature (suggest 60°C, confirm from datasheet).

#### BDC_MWIR_STATUS_BITS [408]

Device: MWIR camera + TRC pipeline.

| Bit | Name | Source | Notes |
|-----|------|--------|-------|
| b0 | `isMWIR_Connected` | `isMWIR_DeviceReady` | ERROR gate |
| b1 | `isHB_OK` | `HB_MWIR_ms > 0 && < 255` | ERROR gate |
| b2 | `isWarmupComplete` | `MWIR_Run_State == MAIN_PROC_LOOP` | WARN gate |
| b3 | `isFOV_Valid` | `MWIR_FOV > 0` | WARN gate |
| b4 | `isCapturing` | `trcMSG.isMWIR_Capturing` | ERROR gate |
| b5 | `isFPA_TempOK` | `MWIR_Temp_FPA` within operating range (TBD) | WARN gate |
| b6–7 | RES | — | — |

```
ERROR: !isMWIR_Connected OR !isHB_OK OR !isCapturing

READY+WARN: isMWIR_Connected AND isHB_OK AND isCapturing
            AND (!isWarmupComplete OR !isFOV_Valid OR !isFPA_TempOK)

READY: all ERROR clear AND isWarmupComplete AND isFOV_Valid AND isFPA_TempOK
```

> ⚠️ TBD: FPA operating temperature range from MWIR camera datasheet.

#### BDC_FSM_STATUS_BITS [409]

Device: Fast Steering Mirror via FMC.

| Bit | Name | Source | Notes |
|-----|------|--------|-------|
| b0 | `isFMC_Connected` | `isFSM_DeviceReady` | ERROR gate |
| b1 | `isHB_OK` | `HB_FMC_ms > 0 && < 255` | ERROR gate |
| b2 | `isFSM_Powered` | `fmcMSG.PowerBits b0` | ERROR gate |
| b3 | `isNotLimited` | `VOTE_BITS_BDC.FSM_NOT_LIMITED` | WARN gate |
| b4 | `isAtHome` | `FSM_X_C ≈ FSM_X0 && FSM_Y_C ≈ FSM_Y0` (TBD tolerance) | **Display only** |
| b5–7 | RES | — | — |

```
ERROR: !isFMC_Connected OR !isHB_OK OR !isFSM_Powered

READY+WARN: isFMC_Connected AND isHB_OK AND isFSM_Powered AND !isNotLimited

READY: isFMC_Connected AND isHB_OK AND isFSM_Powered AND isNotLimited
```

> FSM loss during COMBAT → STATE→ISR. MODE regresses to ATRACK not OFF.
> ⚠️ TBD: FSM home position tolerance (counts).

#### BDC_JET_STATUS_BITS [410]

Device: Jetson Orin SOM (TRC compute platform).

| Bit | Name | Source | Threshold |
|-----|------|--------|-----------|
| b0 | `isConnected` | `trcMSG.isConnected` | ERROR |
| b1 | `isReady` | `trcMSG.isReady` | ERROR |
| b2 | `isStarted` | `trcMSG.isStarted` | ERROR |
| b3 | `isStreaming` | `trcMSG.streamFPS > 0` | WARN at ISR+ |
| b4 | `isCPU_OK` | `jetsonCpuLoad <= 90%` | ERROR threshold |
| b5 | `isGPU_OK` | `jetsonGpuLoad <= 90%` | ERROR threshold |
| b6 | `isCPU_TempOK` | `jetsonTemp <= 85°C` | ERROR threshold |
| b7 | `isGPU_TempOK` | `jetsonGpuTemp <= 85°C` | ERROR threshold |

> WARN thresholds (50% load / 70°C) drive `DEVICE_WARN_BITS` only — not packed as STATUS_BITS ERROR bits to avoid false ERROR triggers during transient activity.

```
ERROR: !isConnected OR !isReady OR !isStarted
       OR !isCPU_OK OR !isGPU_OK
       OR !isCPU_TempOK OR !isGPU_TempOK

READY+WARN: all ERROR clear
            AND (!isStreaming at ISR+
                OR load 50–90% OR temp 70–85°C)

READY: all ERROR clear AND isStreaming (at ISR+)
       AND all loads ≤ 50% AND all temps ≤ 70°C
```

> ⚠️ TBD: Dual threshold approach — confirm firmware tracks both WARN and ERROR thresholds per sensor.

#### BDC_INCL_STATUS_BITS [411]

Device: Inclinometer (IMU / attitude sensor).

| Bit | Name | Source | Notes |
|-----|------|--------|-------|
| b0 | `isConnected` | `isINCL_DeviceReady` | ERROR gate |
| b1 | `isHB_OK` | `HB_INCL_ms > 0 && < 255` | ERROR gate |
| b2 | `isDataValid` | Pitch/roll within plausible range | ERROR gate |
| b3 | `isLevel` | Pitch/roll within operational bounds (TBD) | WARN gate |
| b4–7 | RES | — | — |

```
ERROR: !isConnected OR !isHB_OK OR !isDataValid

READY+WARN: isConnected AND isHB_OK AND isDataValid AND !isLevel

READY: isConnected AND isHB_OK AND isDataValid AND isLevel
```

> INCL ERROR has no state/mode consequence. `BelowHorizVote` is the downstream gate.
> ⚠️ HB-SCALE: `HB_INCL_ms` saturates at 255ms for 1s poll interval.
> ⚠️ TBD: Operational attitude bounds (degrees) for `isLevel`.

---

## Fire Control Reference

### VOTE_BITS_MCC Gate Chain

```
D2 NOT_ABORT (b0) ──┐
D3 ARMED (b1) ───────┤ AND → D7 LASER_TOTAL_HW (b3) ──┐
D4 BDC_VOTE (b2) ────┘                                  │
                                                         ├─ AND → D80 ──┐
D9 SW_VOTE (b4) ────────────────────────────────────────┘               │
   (COMBAT && BAT_NOT_LOW)                                               ├─ AND → D45 FIRE_STATE (b6)
                                                                         │
TRIGGER (b5) ────────────────────────────────────────────────────────────┘

D45 FIRE_STATE → IPG laser → EMON (b7)
```

All bits = 1 while trigger is held = laser firing. Any bit clear = exact blocking condition.
`EMON` (b7) is the only confirmation of actual laser emission. Excluded from all composites.

### VOTE_BITS_MCC2 FC Consistency

```cpp
bool isFireExpected = (VOTE_BITS_MCC & FULL_FIRE_CHAIN) == FULL_FIRE_CHAIN;  // 0x7F
bool EMON_MISSING     = isFireExpected && !ipg.isEMON();                       // b3
bool EMON_UNEXPECTED  = !isFireExpected && ipg.isEMON();                       // b4
bool FIRE_INTERLOCKED = isLaserFireRequested_Vote && !isFireExpected && !ipg.isEMON(); // b5
```

### 0xE0 Packet Layout

Rate: 200 Hz (TICK_VoteStatus = 5 ms), unconditional.

```
MCC → BDC (2-byte payload):
  [VOTE_BITS_MCC]   [VOTE_BITS_MCC2]

BDC → TRC (7-byte payload):
  [VOTE_BITS_MCC]   [VOTE_BITS_BDC]   [sysState]   [bdcMode]
  [VOTE_BITS_MCC2]  [VOTE_BITS_BDC2]
```

TRC REG1 landing: voteBitsMcc[41], voteBitsBdc[42], voteBitsMcc2[57], voteBitsBdc2[58].

### Battery Low Threshold — Hardware Revision Dependent

| Bus | Versions | Threshold | Constant |
|-----|----------|-----------|----------|
| 48V | V1, V3-3kW | 47.0 V | `BAT_LOW_THRESHOLD_48V` |
| 300V | V2, V3-6kW | 266.0 V | `BAT_LOW_THRESHOLD_300V` |

`isNotBatLowVoltage()` branches on HW_REV at runtime. Threshold constants in `hw_rev.hpp`, not `icd.hpp`.

---

## Framing Reference

### STATUS Byte Codes

| Value | Name | Meaning |
|-------|------|---------|
| `0x00` | `STATUS_OK` | Command accepted and executed |
| `0x01` | `STATUS_CMD_REJECTED` | CMD_BYTE not in `EXT_CMDS[]` whitelist |
| `0x02` | `STATUS_BAD_MAGIC` | Magic bytes incorrect |
| `0x03` | `STATUS_BAD_CRC` | CRC check failed |
| `0x04` | `STATUS_BAD_LEN` | `PAYLOAD_LEN` mismatch |
| `0x05` | `STATUS_SEQ_REPLAY` | SEQ_NUM within replay-rejection window |
| `0x06` | `STATUS_NO_DATA` | Register not yet populated |
| `0x07` | `STATUS_PREREQ_FAIL` | State/mode transition rejected — prerequisites not met. **New v4.3.0.** |

### A3 Frame Structure

```
Byte  0    : Magic HI  = 0xCB
Byte  1    : Magic LO  = 0x58
Byte  2    : CMD_BYTE
Byte  3–4  : SEQ_NUM   uint16 LE
Byte  5–6  : PAYLOAD_LEN uint16 LE
Bytes 7–518: PAYLOAD   (512 bytes)
Bytes 519–520: CRC16   uint16 LE (CRC-16/CCITT, poly=0x1021, init=0xFFFF)
```

---

## Network Addresses

| Constant | Value | Purpose |
|----------|-------|---------|
| `IP_MCC_BYTES` | `192.168.1.10` | MCC controller |
| `IP_TMC_BYTES` | `192.168.1.12` | TMC (A2 only) |
| `IP_HEL_BYTES` | `192.168.1.13` | HEL TCP target on MCC |
| `IP_BDC_BYTES` | `192.168.1.20` | BDC controller |
| `IP_GIMBAL_BYTES` | `192.168.1.21` | Galil servo drive |
| `IP_TRC_BYTES` / `BDC_HOST` | `192.168.1.22` | TRC (A2 only; telemetry auto-starts to BDC) |
| `IP_FMC_BYTES` | `192.168.1.23` | FMC (A2 only) |
| `IP_GNSS_BYTES` | `192.168.1.30` | NovAtel / PTP grandmaster |
| `IP_NTP_BYTES` | `192.168.1.33` | HW Stratum 1 NTP server |
| `THEIA` | `192.168.1.208` | Operator HMI / NTP fallback |

---

## Open Items / TBDs

| # | Device | Item |
|---|--------|------|
| 1 | TMC | Cooling requirement — V2/V3·6kW only or V1 also? |
| 2 | HEL | IPGMsg StatusWord temperature bit positions for 3K and 6K |
| 3 | BAT | MSG_BATTERY StatusWord bit map — error/alarm/protection bit definitions |
| 4 | BAT | RSOC warn threshold (suggest 20%) |
| 5 | BAT | PackTemp operational bounds from BMS datasheet |
| 6 | CRG | VIN threshold (V) |
| 7 | GNSS | `Heading_STDEV` threshold |
| 8 | GNSS | INS converged PosType enum values — NovAtel classification |
| 9 | GNSS | TerraStar availability — not all units equipped |
| 10 | MCC_BDC | `isVoteActive` tracking mechanism |
| 11 | GIM | Galil StatusX/Y and StopCode bit map — `isFault` vs `isAtSoftLimit` |
| 12 | MWIR | FPA operating temperature range from datasheet |
| 13 | FSM | Home position tolerance (counts) for `isAtHome` |
| 14 | VIS | Alvium max operating temperature (suggest 60°C, confirm from datasheet) |
| 15 | INCL | Operational attitude bounds (degrees) for `isLevel` |
| 16 | JET | Dual WARN/ERROR threshold tracking — confirm firmware approach |
| 17 | ALL | `STATUS_PREREQ_FAIL` — confirm firmware enforcement points for all state transitions |
| 18 | DEF-2 | TMC-specific enums guarded by `!defined(HW_REV_V2)` — migrate to `TMC_HW_REV_V2` |

---

## Action Items

| ID | Item | Status |
|----|------|--------|
| ~~FC-CONSISTENCY-1~~ | Fire vote consistency — EMON_MISSING/UNEXPECTED/FIRE_INTERLOCKED | ✅ Closed v4.3.0 |
| ~~TRC-TRAIN-WARN-1~~ | Training mode OSD indicator | ✅ Closed — VOTE_BITS_MCC2.TRAINING_MODE (b1) |
| ~~NEW-37~~ | MSG_MCC.cs PTP bits | ✅ Closed |
| ~~NEW-38a~~ | TMC PTP integration | ✅ Closed |
| FW-C8 | Rejection handler removal for retired commands | ⏳ Pending |
| FW-C10 | REG1 CMD_BYTE → 0x00 | ⏳ Pending |
| NEW-38b | BDC PTP integration | ⏳ Pending |
| NEW-38c | FMC PTP integration | ⏳ Pending |
| NEW-38d | TRC PTP integration | ⏳ Pending |
| TBD-1–18 | Open items listed above | ⏳ Pending |
