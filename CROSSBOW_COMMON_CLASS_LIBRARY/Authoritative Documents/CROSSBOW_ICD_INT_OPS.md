# CROSSBOW — INT_OPS Interface Control Document

**Document:** `CROSSBOW_ICD_INT_OPS`
**Doc #:** IPGD-0004
**Version:** 4.0.0
**Date:** 2026-04-26 (CB-20260426 — vote byte overhaul, device health, STATUS_BITS)

---

## Version History

**v4.0.0 changes (CB-20260426 — vote byte overhaul, device health, STATUS_BITS):**

This release merges `CROSSBOW_FIRECONTROL_REGISTERS.md`, `CROSSBOW_DEVICE_STATUS_BITS_WORKING.md`,
and accumulated session changes into the ICD. All changes are wire-level breaking.

**MCC REG1 — byte [11] `VOTE_BITS_MCC` — complete bit layout replacement:**
Previous layout had incorrect gate-chain ordering and stale field names. New layout follows the physical AND chain b0→b7:
`b0:NOT_ABORT(INVERTED)` `b1:ARMED` `b2:BDC_VOTE` `b3:LASER_TOTAL_HW` `b4:SW_VOTE` `b5:TRIGGER` `b6:FIRE_STATE` `b7:EMON`
Old bit names `isBDA_Vote_rb` → `isBDC_Vote_rb` throughout. `isCombat_Vote_rb` moved to `VOTE_BITS_MCC2`.

**MCC REG1 — 8 new bytes [256–263]:**
- `[256]` `VOTE_BITS_MCC2` — MCC detail/diagnostic byte. `b0:BAT_NOT_LOW` `b1:TRAINING_MODE` `b2:COMBAT` `b3:EMON_MISSING` `b4:EMON_UNEXPECTED` `b5:FIRE_INTERLOCKED`
- `[257]` `MCC DEVICE_WARN_BITS` — parallel to ENABLED [7] and READY [8]. Same bit layout; bit N set when device N is enabled AND degraded.
- `[258]` `MCC_TMC_STATUS_BITS`
- `[259]` `MCC_HEL_STATUS_BITS`
- `[260]` `MCC_BAT_STATUS_BITS`
- `[261]` `MCC_CRG_STATUS_BITS`
- `[262]` `MCC_GNSS_STATUS_BITS`
- `[263]` `MCC_BDC_STATUS_BITS`

MCC REG1 defined byte count: 256 → 264. Reserved: 248 bytes [264–511].

**BDC REG1 — vote bytes [164–166] renamed and updated:**
- `[164]` renamed `VOTE_BITS_BDC2` (was `BDC VOTE BITS1`). New bit layout: `b0:HORIZ_VOTE_OVERRIDE` `b1:KIZ_VOTE_OVERRIDE` `b2:LCH_VOTE_OVERRIDE` `b3:BDC_VOTE_OVERRIDE` `b4:IS_BELOW_HORIZ` `b5:IS_IN_KIZ` `b6:IS_IN_LCH`
- `[165]` renamed `VOTE_BITS_BDC` (was `BDC VOTE BITS2`). Bit b7 corrected: `isFSMLimited` → `FSM_NOT_LIMITED` (bit SET = FSM clear — was inverted). Layout: `b0:BELOW_HORIZ_VOTE` `b1:IN_KIZ_VOTE` `b2:IN_LCH_VOTE` `b3:BDC_TOTAL_VOTE` `b5:HORIZ_LOADED` `b7:FSM_NOT_LIMITED`
- `[166]` renamed `VOTE_BITS_MCC_RB` (was `MCC VOTE BITS RB`). Updated to new `VOTE_BITS_MCC` gate-chain layout.

**BDC REG1 — 8 new bytes [404–411]:**
- `[404]` `VOTE_BITS_MCC2_RB` — MCC detail byte readback
- `[405]` `BDC DEVICE_WARN_BITS`
- `[406]` `BDC_GIM_STATUS_BITS`
- `[407]` `BDC_VIS_STATUS_BITS`
- `[408]` `BDC_MWIR_STATUS_BITS`
- `[409]` `BDC_FSM_STATUS_BITS`
- `[410]` `BDC_JET_STATUS_BITS`
- `[411]` `BDC_INCL_STATUS_BITS`

BDC REG1 defined byte count: 404 → 412. Reserved: 100 bytes [412–511].

**`0xE0 SET_BCAST_FIRECONTROL_STATUS` payload updated:**
- MCC→BDC: `[VOTE_BITS_MCC][VOTE_BITS_MCC2][sysState][bdcMode]` — 4 bytes (was 2)
- BDC→TRC: `[VOTE_BITS_MCC][VOTE_BITS_BDC][sysState][bdcMode][VOTE_BITS_MCC2][VOTE_BITS_BDC2]` — 7 bytes (was 2)

**FC-CONSISTENCY-1 closed:** `EMON_MISSING`, `EMON_UNEXPECTED`, `FIRE_INTERLOCKED` implemented in `VOTE_BITS_MCC2` bits 3–5 and mirrored in `MCC_HEL_STATUS_BITS` bits 5–7. `FIRE_VOTE_BYTE` concept superseded — `VOTE_BITS_MCC` + `VOTE_BITS_MCC2` together carry the complete fire readiness state.

**New status code:** `STATUS_PREREQ_FAIL = 0x07` — returned when `SET_SYSTEM_STATE` or `SET_GIMBAL_MODE` is rejected due to unmet device health prerequisites.

**New section: Device Health** — advancement prerequisites, device criticality matrix, state/mode rules. Previously in working document only.

**C# client impact:**
- `icd.cs`: `MCC_VOTES`, `MCC_VOTES2`, `BDC_VOTES`, `BDC_VOTES2` enums fully updated.
- `MSG_MCC.cs`: `VOTE_BITS_MCC` / `VOTE_BITS_MCC2` properties replace `VoteBits` / `VoteBits2`. New `DeviceWarnBits` + per-device STATUS_BITS accessors.
- `MSG_BDC.cs`: `VOTE_BITS_BDC` / `VOTE_BITS_BDC2` / `VOTE_BITS_MCC_RB` / `VOTE_BITS_MCC2_RB` replace previous `VoteBits1–4`. New `DeviceWarnBits` + per-device STATUS_BITS accessors.
- `isNotBatLowVoltage` and `isHEL_TrainingMode` in `MSG_MCC.cs` now redirect to `VOTE_BITS_MCC2` (authoritative source).

---

**v3.6.2 changes (scope audit — whitelist-verified — 2026-04-22):**
- `0xD1 ORIN_ACAM_COCO_ENABLE` — removed from INT_OPS. Absent from `EXT_CMDS_BDC[]`. Scope corrected to INT_ENG.
- `0xF6 BDC_SET_FSM_TRACK_ENABLE` — removed from INT_OPS. Absent from `EXT_CMDS_BDC[]`. Scope corrected to INT_ENG.
- `0xFA BDC_SET_STAGE_HOME` — removed from INT_OPS. Absent from `EXT_CMDS_BDC[]`. Scope corrected to INT_ENG.

**v3.6.1 changes (CB-20260416 — AWB assigned, charger V2 fix):**
- `0xC4 CMD_VIS_AWB` assigned. `0xAF SET_CHARGER` V2 description corrected.

**v3.6.0 changes (ICD command space restructuring — 2026-04-12):**
A block fully assigned. See archived INT_OPS v3.6.2 for full history.

---

> **Document policy:** This document contains only `INT_OPS`-scoped commands.
> Engineering-only (`INT_ENG`) commands and full STATUS_BITS decode tables are in `CROSSBOW_ICD_INT_ENG` (IPGD-0003).

> **Framing and transport:** A3 port (10050), magic `0xCB 0x58`, 521 bytes total.

> **Targets:** A3 clients may address **MCC** (192.168.1.10) and **BDC** (192.168.1.20) directly via A3.
> **TRC** is accessible via BDC routing only — send TRC commands (0xD0–0xDF) to BDC; BDC forwards internally.

---

## Network and Interface Tier Overview

CROSSBOW uses a three-tier interface model. INT_OPS clients operate at Tier 1.

```
┌─────────────────────────────────────────────────────────┐
│  A1 — Controller Bus                                    │
│  Internal controller-to-controller. No external access. │
├─────────────────────────────────────────────────────────┤
│  A2 — Engineering and Maintenance Interface             │
│  IPG engineering: firmware deployment, full diagnostics.│
└───────────────────────┬─────────────────────────────────┘
                        │ A3 boundary
┌───────────────────────▼─────────────────────────────────┐
│  A3 — INT_OPS — Tier 1 (this document)                  │
│  A3 port 10050, magic 0xCB 0x58                         │
│  Full operator command set — MCC, BDC, TRC via BDC      │
│   THEIA (.208) — IPG reference HMI                      │
│   Vendor HMI (.210–.254) — bespoke implementations      │
└───────────────────────┬─────────────────────────────────┘
                        │ EXT_OPS boundary
┌───────────────────────▼─────────────────────────────────┐
│  EXT_OPS — Tier 2 (CROSSBOW_ICD_EXT_OPS, IPGD-0005)    │
│  UDP:10009, magic 0xCB 0x48                             │
│  CUE input — HYPERION or third-party providers          │
└─────────────────────────────────────────────────────────┘
```

| Tier | Transport | Magic | Audience |
|------|-----------|-------|----------|
| A1 — Controller Bus | Internal only | — | Controller firmware only |
| A2 — Engineering | Internal ports | — | IPG engineering via ENG GUI |
| A3 — INT_OPS — Tier 1 | A3 port 10050 | `0xCB 0x58` | THEIA, vendor HMI integrators — this document |
| EXT_OPS — Tier 2 | UDP:10009 | `0xCB 0x48` | CUE providers, HYPERION — see IPGD-0005 |

---

## Command Scope

| Scope | Meaning |
|-------|---------|
| `INT_OPS` | Operator-accessible — this document. Sent via A3 port (10050, magic `0xCB 0x58`). |
| `INT_ENG` | Engineering only — A2 port. Omitted from this document. See `CROSSBOW_ICD_INT_ENG` (IPGD-0003). |
| `EXT_OPS` | Cueing input interface — see `CROSSBOW_ICD_EXT_OPS` (IPGD-0005). |

---

## 0xA0–0xAF — System Commands

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xA0 | SET_UNSOLICITED | Subscribe/unsubscribe to unsolicited telemetry push. `{0x01}` = subscribe; `{0x00}` = unsubscribe. | uint8 0=off, 1=on | MCC, BDC |
| 0xA1 | SET_HEL_TRAINING_MODE | Set training/combat mode — training clamps laser power to 10% regardless of `SET_HEL_POWER`. Promoted to INT_OPS v3.6.0. | uint8 0=COMBAT, 1=TRAINING | MCC |
| 0xA2 | SET_NTP_CONFIG | Configure NTP server and/or force resync. Promoted INT_ENG→INT_OPS v3.6.0. 0 bytes = resync. 1 byte `[p]` = set primary `192.168.1.p`. 2 bytes `[p,f]` = set primary+fallback. Routing by destination IP. | 0–2 bytes | MCC, BDC |
| 0xA3 | SET_TIMESRC | Set active time source. New v3.6.0. Routing by destination IP. ⚠ Pending FW-C8 before live. | uint8 0=OFF, 1=NTP, 2=PTP, 3=AUTO | MCC, BDC |
| 0xA4 | FRAME_KEEPALIVE | Register/keepalive. Empty = ACK only. Payload `{0x01}` = ACK + solicited REG1 (rate-gated 1 Hz per slot; suppressed if subscribed). | 0 or 1 byte | MCC, BDC |
| 0xA5 | SET_SYSTEM_STATE | Set system state. Returns `STATUS_PREREQ_FAIL (0x07)` if device health prerequisites not met. | uint8 (SYSTEM_STATES enum) | MCC, BDC |
| 0xA6 | SET_GIMBAL_MODE | Set gimbal/tracker mode. Returns `STATUS_PREREQ_FAIL (0x07)` if prerequisites not met. | uint8 (BDC_MODES enum) | MCC, BDC |
| 0xA7 | SET_LCH_MISSION_DATA | Load LCH/KIZ mission data, clear all windows | uint8 which (0=KIZ,1=LCH); uint8 isValid; uint64 startTimeMission_min; uint64 stopTimeMission_max; int16 az1; int16 el1; int16 az2; int16 el2; uint16 nTargets; uint16 nTotalWindows | BDC |
| 0xA8 | SET_LCH_TARGET_DATA | Load LCH/KIZ target with windows | uint8 which (0=KIZ,1=LCH); uint16 nWindows; uint16 startTimeTarget_min; uint16 stopTimeTarget_max; uint16 az1; uint16 el1; uint16 az2; uint16 el2; nWindows×[uint16 wt1, uint16 wt2] | BDC |
| 0xA9 | SET_REINIT | Unified controller reinitialise. Replaces `0xB0` (BDC) and `0xE0` (MCC). Routing by destination IP. TMC/FMC not supported. | uint8 subsystem (BDC: 0=NTP,1=GIM,2=FUJI,3=MWIR,4=FSM,5=JET,6=INCL,7=PTP; MCC: 0=NTP,1=TMC,2=HEL,3=BAT,4=PTP,5=CRG,6=GNSS,7=BDC) | MCC, BDC |
| 0xAA | SET_DEVICES_ENABLE | Unified device enable/disable. Replaces `0xE1` (MCC). Routing by destination IP. | uint8 device (same index as SET_REINIT); uint8 0/1 | MCC, BDC |
| 0xAB | SET_FIRE_REQUESTED_VOTE | Laser fire requested vote. Promoted to INT_OPS v3.6.0. Continuous heartbeat required — vote drops on disconnect. | uint8 0/1 | MCC |
| 0xAC | SET_BDC_HORIZ | Set horizon elevation vector | float[360] | BDC |
| 0xAD | SET_HEL_POWER | Set laser power level | uint8 [0–100] % | MCC |
| 0xAE | CLEAR_HEL_ERROR | Clear laser error state | none | MCC |
| 0xAF | SET_CHARGER | Set charger state and level. Merges `0xE3` and `0xED`. Level required on every call. **V1/V3:** GPIO enable + I2C level control (DBU3200). **V2:** GPIO enable only — level `0`=disable, non-zero=enable. | uint8 level: 0=disable, 10=low, 30=med, 55=high | MCC |

---

## 0xB0–0xBF — BDC Commands

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xB2 | SET_GIM_POS | Set gimbal position | int32 pan, int32 tilt | BDC |
| 0xB3 | SET_GIM_SPD | Set gimbal speed | int16 pan, int16 tilt | BDC |
| 0xB4 | SET_CUE_OFFSET | Set cue track offset (AZ, EL) | float az_deg, float el_deg | BDC |
| 0xB5 | CMD_GIM_PARK | Park gimbal at home | none | BDC |
| 0xB6 | SET_GIM_LIMITS | Set gimbal wrap limits | int32 panMin, int32 panMax, int32 tiltMin, int32 tiltMax | BDC |
| 0xB7 | SET_PID_GAINS | Set PID gains | uint8 which (0=cue, 1=AT); float kpp, kip, kdp, kpt, kit, kdt | BDC |
| 0xB8 | SET_PID_TARGET | Set PID target setpoint | uint8 sub-cmd (0=CUE NED, 1=video px); float x; float y; float pidScale | BDC |
| 0xB9 | SET_PID_ENABLE | Enable/disable PID loop | uint8 which (0=cue, 1=video); uint8 0/1 | BDC |
| 0xBA | SET_SYS_LLA | Set platform geodetic position | float lat, float lng, float alt | BDC |
| 0xBB | SET_SYS_ATT | Set platform attitude (RPY) | float roll, float pitch, float yaw | BDC |

---

## 0xC0–0xCF — BDC/Camera Commands

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xC1 | SET_CAM_MAG | VIS camera zoom | uint8 mag index | BDC |
| 0xC2 | SET_CAM_FOCUS | VIS camera focus | uint16 focus position | BDC |
| 0xC4 | CMD_VIS_AWB | Trigger VIS auto white balance once. BDC routes to TRC. | none | BDC |
| 0xC7 | SET_CAM_IRIS | VIS camera iris position | uint8 upper nibble of iris position | BDC |
| 0xC8 | CMD_VIS_FILTER_ENABLE | VIS ND filter enable | uint8 0/1 | BDC |
| 0xC9 | SET_BDC_PALOS_VOTE | Set operator/position valid vote | uint8 which (0=KIZ, 1=LCH); uint8 operatorValid; uint8 positionValid; uint8 forExec | BDC |
| 0xCA | GET_BDC_PALOS_VOTE | Check BDC PALOS vote | uint8 which (0=KIZ, 1=LCH); uint64 timestamp; float az; float el | BDC |
| 0xCB | SET_MWIR_WHITEHOT | MWIR white hot polarity | uint8 0/1 | BDC |
| 0xCC | CMD_MWIR_NUC1 | MWIR internal NUC refresh | none | BDC |
| 0xCD | CMD_MWIR_AF_MODE | MWIR AF mode | uint8 (0=off, 1=continuous, 2=once) | BDC |
| 0xCE | CMD_MWIR_BUMP_FOCUS | MWIR bump focus near/far | uint8 (0=near, 1=far) | BDC |

---

## 0xD0–0xDF — TRC/Orin Commands (routed via BDC)

> TRC commands are routed through BDC over A3. The INT_OPS Target shows `BDC` — BDC forwards to TRC internally.

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xD0 | ORIN_CAM_SET_ACTIVE | Set active camera | uint8 BDC_CAM_IDS (0=VIS, 1=MWIR) | BDC |
| 0xD3 | ORIN_SET_STREAM_OVERLAYS | Set HUD overlay bitmask (HUD_OVERLAY_BITS enum). b0=Reticle, b1=TrackPreview, b2=TrackBox, b3=CueChevrons, b4=AC_Projections, b5=AC_LeaderLines, b6=FocusScore, b7=OSD | uint8 bitmask | BDC |
| 0xD4 | ORIN_ACAM_SET_CUE_FLAG | Set cue flag indicator | uint8 0/1 | BDC |
| 0xD5 | ORIN_ACAM_SET_TRACKGATE_SIZE | Set track gate width/height | uint8 w, uint8 h (pixels, min 16) | BDC |
| 0xD6 | ORIN_ACAM_ENABLE_FOCUSSCORE | Enable focus score computation | uint8 0/1 | BDC |
| 0xD7 | ORIN_ACAM_SET_TRACKGATE_CENTER | Set track gate preview center | uint16 x, uint16 y (pixels) | BDC |
| 0xDA | ORIN_ACAM_RESET_TRACKB | Reset MOSSE tracker to current preview gate | none | BDC |
| 0xDB | ORIN_ACAM_ENABLE_TRACKERS | Enable/disable tracker | uint8 tracker_id (0=AI, 1=MOSSE, 2=Centroid, 4=LK); uint8 0/1; [uint8 mosseReseed 0x01/0x00] | BDC |
| 0xDC | ORIN_ACAM_SET_ATOFFSET | Set AT reticle offset | int8 dx, int8 dy (pixels, −128 to 127) | BDC |
| 0xDD | ORIN_ACAM_SET_FTOFFSET | Set FT offset | int8 dx, int8 dy (pixels, −128 to 127) | BDC |
| 0xDE | ORIN_SET_VIEW_MODE | Set compositor output view | uint8 (0=CAM1, 1=CAM2, 2=PIP4, 3=PIP8) | BDC |

---

## 0xE0–0xEF — MCC / TMS Commands

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xE7 | TMS_INPUT_FAN_SPEED | Set fan speed | uint8 which (0/1); uint8 speed (0=off, 128=low, 255=high) | MCC |
| 0xEB | TMS_SET_TARGET_TEMP | Set TMS target temperature | uint8 temp °C — **enforced range [10–40°C]**; firmware clamps silently. | MCC |

---

## 0xF0–0xFF — FSM / FMC Commands (routed via BDC)

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xF1 | BDC_SET_FSM_HOME | FSM set home position | int16 x, int16 y | BDC |
| 0xF2 | BDC_SET_FSM_IFOVS | FSM set iFOV scaling | float x, float y | BDC |
| 0xF3 | FMC_SET_FSM_POS | FSM set position | int16 x, int16 y | BDC |
| 0xFB | FMC_SET_STAGE_POS | Focus stage set position | uint32 position | BDC |

---

## MCC Register 1 (REG1)

Sent by MCC unsolicited (when subscribed via `0xA0`) or on solicited request (`0xA4 {0x01}`, rate-gated 1 Hz per slot).
Frame CMD_BYTE is `0xA1` — legacy constant, pending migration to `0x00` (FW-C10).
Fixed block size: **512-byte payload** (256 defined + 248 reserved).

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES enum |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES enum |
| 3 | 3 | 5 | 2 | HB_ms | uint16 | ms between sends |
| 5 | 5 | 7 | 2 | dt_us | uint16 | µs in processing loop |
| 7 | 7 | 8 | 1 | MCC DEVICE_ENABLED_BITS | uint8 | b0:NTP; b1:TMC; b2:HEL; b3:BAT; b4:PTP; b5:CRG; b6:GNSS; b7:BDC |
| 8 | 8 | 9 | 1 | MCC DEVICE_READY_BITS | uint8 | b0:NTP; b1:TMC; b2:HEL; b3:BAT; b4:PTP; b5:CRG; b6:GNSS; b7:BDC |
| 9 | 9 | 10 | 1 | MCC HEALTH_BITS | uint8 | b0:isReady; b1:isChargerEnabled; b2:isNotBatLowVoltage(→VOTE_BITS_MCC2.BAT_NOT_LOW); b3:isTrainingMode(→VOTE_BITS_MCC2.TRAINING_MODE); b4:isLaserModelMatch; b5–7:RES. ⚠ Breaking change v3.4.0. |
| 10 | 10 | 11 | 1 | MCC POWER_BITS | uint8 | Bit N = MCC_POWER enum value N. b0:RELAY_GPS(V1/V3); b1:VICOR_BUS(V1/V3-3kW); b2:RELAY_LASER(all); b3:VICOR_GIM(V2/V3-6kW); b4:VICOR_TMS(V2/V3-6kW); b5:SOL_HEL(V1/V3-3kW); b6:SOL_BDA(V1/V3-3kW); b7:RELAY_NTP(V3 only). ⚠ Breaking change v3.4.0. |
| 11 | 11 | 12 | 1 | **VOTE_BITS_MCC** | uint8 | Gate-chain order b0→b7. **b0:NOT_ABORT(INVERTED — CLEAR=abort ACTIVE)**; b1:ARMED; b2:BDC_VOTE; b3:LASER_TOTAL_HW; b4:SW_VOTE(Combat&&BatNotLow); b5:TRIGGER; b6:FIRE_STATE; b7:EMON(display only). Composites: ARMED_NOMINAL=0x03; READY_TO_FIRE=0x1F; FULL_FIRE_CHAIN=0x7F. ⚠ **Breaking change v4.0.0** — previous layout had incorrect bit ordering and stale field names. |
| 12 | 12 | 20 | 8 | epoch Time (PTP/NTP) | uint64 | ms since epoch — PTP when synched, NTP otherwise |
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
| 43 | 43 | 45 | 2 | Battery Status Word | int16 | 16 bits |
| 45 | 45 | 47 | 2 | Laser HK Voltage | uint16 | centi-volts |
| 47 | 47 | 49 | 2 | Laser Bus Voltage | uint16 | centi-volts |
| 49 | 49 | 50 | 1 | Laser Temperature | int8 | °C |
| 50 | 50 | 54 | 4 | Laser Status Word | uint32 | |
| 54 | 54 | 58 | 4 | Laser Error Word | uint32 | |
| 58 | 58 | 62 | 4 | Laser SetPoint | float | % |
| 62 | 62 | 66 | 4 | Laser Output Power | float | W |
| 66 | 66 | 130 | 64 | TMC FULL REG | TMC_REG | 64-byte block. Byte [62] within block = HW_REV (0x01=V1, 0x02=V2). See INT_ENG for full decode. |
| 130 | 130 | 131 | 1 | TIME HB | uint8 | s/10 — NTP receive interval |
| 131 | 131 | 132 | 1 | HEL HB | uint8 | s/10 — laser TCP response interval. Stale > 2.0s = comms loss. |
| 132 | 132 | 133 | 1 | BAT HB | uint8 | s/10 |
| 133 | 133 | 134 | 1 | CRG HB | uint8 | s/10 — V1/V3 only |
| 134 | 134 | 135 | 1 | GNSS HB | uint8 | s/10 |
| 135 | 135 | 136 | 1 | GNSS SOLN STATUS | uint8 | enum |
| 136 | 136 | 137 | 1 | GNSS POS TYPE | uint8 | enum |
| 137 | 137 | 138 | 1 | INS SOLN STATUS | uint8 | enum |
| 138 | 138 | 139 | 1 | TERRA STAR SYNC STATE | uint8 | enum |
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
| 255 | 255 | 256 | 1 | LASER_MODEL | uint8 | 0x00=UNKNOWN; 0x01=YLM_3K; 0x02=YLM_6K. Populated after laser auto-sense on connect. |
| 256 | 256 | 257 | 1 | **VOTE_BITS_MCC2** | uint8 | MCC detail byte. b0:BAT_NOT_LOW; b1:TRAINING_MODE; b2:COMBAT; b3:EMON_MISSING; b4:EMON_UNEXPECTED; b5:FIRE_INTERLOCKED; b6–7:RES. SW_VOTE in [11]b4 = b2 AND b0. **New v4.0.0.** |
| 257 | 257 | 258 | 1 | **MCC DEVICE_WARN_BITS** | uint8 | Parallel to ENABLED [7] and READY [8]. Same bit layout. Bit N set when device N is enabled AND degraded but operational. WARN does not block advancement. **New v4.0.0.** |
| 258 | 258 | 259 | 1 | **MCC_TMC_STATUS_BITS** | uint8 | b0:isConnected; b1:isPump1Enabled; b2:isPump2Enabled(V2/V3, 0 on V1); b3:isLCM1_Error; b4:isFlow1_Error; b5:isLCM2_Error; b6:isFlow2_Error; b7:RES. **New v4.0.0.** |
| 259 | 259 | 260 | 1 | **MCC_HEL_STATUS_BITS** | uint8 | b0:isSensed; b1:isHB_OK; b2:isNOTREADY(set=error); b3:isModelMatch; b4:isEMON(display); b5:isEMON_Unexpected; b6:isEMON_Missing; b7:isFireInterlocked. **New v4.0.0.** |
| 260 | 260 | 261 | 1 | **MCC_BAT_STATUS_BITS** | uint8 | b0:isConnected; b1:isNotLowVoltage; b2:isCharging(display); b3:isDischarging(display); b4:isSOC_OK; b5:isTempOK; b6:isError; b7:isAlarm. **New v4.0.0.** |
| 261 | 261 | 262 | 1 | **MCC_CRG_STATUS_BITS** | uint8 | b0:isConnected; b1:isEnabled; b2:isVIN_OK; b3:isCharging(display); b4:isAtMaxLevel(display); b5–7:RES. **New v4.0.0.** |
| 262 | 262 | 263 | 1 | **MCC_GNSS_STATUS_BITS** | uint8 | b0:isConnected; b1:isHB_OK; b2:isPositionValid; b3:isSIV_OK; b4:isHeadingValid; b5:isINS_Converged; b6:isTerraStar_OK(display); b7:RES. **New v4.0.0.** |
| 263 | 263 | 264 | 1 | **MCC_BDC_STATUS_BITS** | uint8 | b0:isEnabled; b1:isReachable; b2:isVoteActive; b3–7:RES. **New v4.0.0.** |
| 264 | 264 | 512 | 248 | RESERVED | — | 0x00 |

**Defined: 264 bytes. Reserved: 248 bytes. Padded to 512-byte payload.**

> **Time source decode:** Read `TIME_BITS` [253]. `b2=1` → PTP active. `b2=0` + `b3=1` → NTP serving. `b4=1` → NTP on fallback. All zeros → no time source.

> **Device health decode:** `DEVICE_ENABLED_BITS[N]=1` → device in service. `DEVICE_READY_BITS[N]=0` → ERROR (blocks or regresses state). `DEVICE_WARN_BITS[N]=1` → WARN (degraded but operational). `DEVICE_READY_BITS[N]=1` + `DEVICE_WARN_BITS[N]=0` → READY. See Device Health section.

> **VOTE_BITS_MCC2 note:** `isNotBatLowVoltage` and `isTrainingMode` (previously in HEALTH_BITS [9] bits 2–3) now redirect to `VOTE_BITS_MCC2.BAT_NOT_LOW` and `VOTE_BITS_MCC2.TRAINING_MODE` as the authoritative source. HEALTH_BITS bits 2–3 remain populated by firmware for backward compatibility.

---

## BDC Register 1 (REG1)

Sent by BDC unsolicited (when subscribed via `0xA0`) or on solicited request (`0xA4 {0x01}`, rate-gated 1 Hz per slot).
Frame CMD_BYTE is `0xA1`. Fixed block size: **512 bytes**.

Embedded sub-registers (opaque to INT_OPS clients):
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
| **60** | **60** | **124** | **64** | **TRC REGISTER** | **TRC_REG** | **64-byte block (opaque — see INT_ENG TRC REG1)** |
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
| 164 | 164 | 165 | 1 | **VOTE_BITS_BDC2** | uint8 | BDC raw/override detail. b0:HORIZ_VOTE_OVERRIDE; b1:KIZ_VOTE_OVERRIDE; b2:LCH_VOTE_OVERRIDE; b3:BDC_VOTE_OVERRIDE; b4:IS_BELOW_HORIZ; b5:IS_IN_KIZ; b6:IS_IN_LCH; b7:RES. ⚠ **Renamed v4.0.0** (was BDC VOTE BITS1). |
| 165 | 165 | 166 | 1 | **VOTE_BITS_BDC** | uint8 | BDC computed vote summary. b0:BELOW_HORIZ_VOTE; b1:IN_KIZ_VOTE; b2:IN_LCH_VOTE; b3:BDC_TOTAL_VOTE; b4:RES; b5:HORIZ_LOADED; b6:RES; b7:FSM_NOT_LIMITED(SET=clear). ⚠ **Renamed v4.0.0** (was BDC VOTE BITS2). b7 name corrected — was `isFSMLimited` (inverted sense). |
| 166 | 166 | 167 | 1 | **VOTE_BITS_MCC_RB** | uint8 | MCC gate-chain readback. Same bit layout as VOTE_BITS_MCC [11]. b0:NOT_ABORT(INVERTED); b1:ARMED; b2:BDC_VOTE; b3:LASER_TOTAL_HW; b4:SW_VOTE; b5:TRIGGER; b6:FIRE_STATE; b7:EMON. ⚠ **Renamed v4.0.0** (was MCC VOTE BITS RB). |
| 167 | 167 | 168 | 1 | VOTE_BITS_KIZ | uint8 | b0:isLoaded; b1:isEnabled; b2:isTimeValid; b3:isOperatorValid; b4:isPositionValid; b5:isForExec; b6:isInKIZ; b7:InKIZVote |
| 168 | 168 | 169 | 1 | VOTE_BITS_LCH | uint8 | b0:isLoaded; b1:isEnabled; b2:isTimeValid; b3:isOperatorValid; b4:isPositionValid; b5:isForExec; b6:isInLCH; b7:InLCHVote |
| **169** | **169** | **233** | **64** | **FMC REGISTER** | **FMC_REG** | **64-byte block (opaque — see INT_ENG FMC REG1)** |
| 233 | 233 | 235 | 2 | FSM_X | int16 | commanded FSM X position |
| 235 | 235 | 237 | 2 | FSM_Y | int16 | commanded FSM Y position |
| 237 | 237 | 241 | 4 | Gimbal Home X | int32 | home encoder X (0 az) |
| 241 | 241 | 245 | 4 | Gimbal Home Y | int32 | home encoder Y (0 el) |
| 245 | 245 | 253 | 8 | Platform Latitude | double | latched |
| 253 | 253 | 261 | 8 | Platform Longitude | double | latched |
| 261 | 261 | 265 | 4 | Platform Altitude | float | latched HAE |
| 265 | 265 | 269 | 4 | Platform Roll | float | degrees |
| 269 | 269 | 273 | 4 | Platform Pitch | float | degrees |
| 273 | 273 | 277 | 4 | Platform Yaw | float | degrees |
| 277 | 277 | 281 | 4 | Target Pan (Cue Track) | int32 | encoder counts |
| 281 | 281 | 285 | 4 | Target Tilt (Cue Track) | int32 | encoder counts |
| 285 | 285 | 333 | 48 | PID Gains (cue + video) | float[12] | pan/tilt kp/ki/kd, cue then video |
| 333 | 333 | 341 | 8 | iFOV_FSM_X_DEG_COUNT | double | |
| 341 | 341 | 349 | 8 | iFOV_FSM_Y_DEG_COUNT | double | |
| 349 | 349 | 355 | 6 | FSM home + signs | mixed | FSM_X0, FSM_Y0, FSM_X_SIGN, FSM_Y_SIGN |
| 355 | 355 | 363 | 8 | Stage position/home | uint32 | STAGE_POSITION + STAGE_HOME |
| 363 | 363 | 379 | 16 | FSM NED AZ/EL (RB + cmd) | float[4] | readback + commanded |
| 379 | 379 | 383 | 4 | HORIZON_BUFFER | float | |
| 383 | 383 | 387 | 4 | BDC VERSION WORD | uint32 | VERSION_PACK(major, minor, patch) |
| 387 | 387 | 391 | 4 | MCU Temp | float | °C |
| 391 | 391 | 392 | 1 | TIME_BITS | uint8 | b0:isPTP_Enabled; b1:ptp.isSynched; b2:usingPTP; b3:ntp.isSynched; b4:ntpUsingFallback; b5:ntpHasFallback; b6–7:RES |
| 392 | 392 | 393 | 1 | HW_REV | uint8 | 0x01=V1; 0x02=V2. Read before interpreting HEALTH_BITS [10] bit 1. |
| 393 | 393 | 394 | 1 | TEMP_RELAY | int8 | Relay area temp °C. V2 live; V1 always 0x00. |
| 394 | 394 | 395 | 1 | TEMP_BAT | int8 | Battery-in area temp °C. V2 live; V1 always 0x00. |
| 395 | 395 | 396 | 1 | TEMP_USB | int8 | USB 5V area temp °C. V2 live; V1 always 0x00. |
| 396 | 396 | 397 | 1 | HB_NTP | uint8 | x0.1s units (C# /10.0 → seconds) |
| 397 | 397 | 398 | 1 | HB_FMC_ms | uint8 | raw ms, saturates at 255 |
| 398 | 398 | 399 | 1 | HB_TRC_ms | uint8 | raw ms, saturates at 255 |
| 399 | 399 | 400 | 1 | HB_MCC_ms | uint8 | raw ms since last 0xE0 RX, saturates at 255 |
| 400 | 400 | 401 | 1 | HB_GIM_ms | uint8 | raw ms, saturates at 255 |
| 401 | 401 | 402 | 1 | HB_FUJI_ms | uint8 | raw ms, saturates at 255 |
| 402 | 402 | 403 | 1 | HB_MWIR_ms | uint8 | raw ms, saturates at 255 |
| 403 | 403 | 404 | 1 | HB_INCL_ms | uint8 | raw ms, saturates at 255 ⚠ INCL-HB-SCALE: saturates at 255ms for 1s poll |
| 404 | 404 | 405 | 1 | **VOTE_BITS_MCC2_RB** | uint8 | MCC detail byte readback. Same layout as VOTE_BITS_MCC2 [MCC 256]. **New v4.0.0.** |
| 405 | 405 | 406 | 1 | **BDC DEVICE_WARN_BITS** | uint8 | Parallel to ENABLED [8] and READY [9]. Same bit layout. **New v4.0.0.** |
| 406 | 406 | 407 | 1 | **BDC_GIM_STATUS_BITS** | uint8 | b0:isConnected; b1:isReady; b2:isStarted; b3:isAtSoftLimit; b4:isMoving(display); b5:isFault; b6–7:RES. **New v4.0.0.** |
| 407 | 407 | 408 | 1 | **BDC_VIS_STATUS_BITS** | uint8 | b0:isFuji_Connected; b1:isFuji_HB_OK; b2:isFOV_Valid; b3:isAlvium_Powered; b4:isAlvium_Connected; b5:isCapturing; b6:isAlvium_TempOK; b7:RES. **New v4.0.0.** |
| 408 | 408 | 409 | 1 | **BDC_MWIR_STATUS_BITS** | uint8 | b0:isMWIR_Connected; b1:isHB_OK; b2:isWarmupComplete; b3:isFOV_Valid; b4:isCapturing; b5:isFPA_TempOK; b6–7:RES. **New v4.0.0.** |
| 409 | 409 | 410 | 1 | **BDC_FSM_STATUS_BITS** | uint8 | b0:isFMC_Connected; b1:isHB_OK; b2:isFSM_Powered; b3:isNotLimited; b4:isAtHome(display); b5–7:RES. **New v4.0.0.** |
| 410 | 410 | 411 | 1 | **BDC_JET_STATUS_BITS** | uint8 | b0:isConnected; b1:isReady; b2:isStarted; b3:isStreaming; b4:isCPU_OK(≤90%); b5:isGPU_OK(≤90%); b6:isCPU_TempOK(≤85°C); b7:isGPU_TempOK(≤85°C). **New v4.0.0.** |
| 411 | 411 | 412 | 1 | **BDC_INCL_STATUS_BITS** | uint8 | b0:isConnected; b1:isHB_OK; b2:isDataValid; b3:isLevel; b4–7:RES. **New v4.0.0.** |
| 412 | 412 | 512 | 100 | RESERVED | — | 0x00 |

**Defined: 412 bytes. Reserved: 100 bytes. Fixed block: 512 bytes.**

> **Time source decode:** Read `TIME_BITS` [391]. `b2=1` → PTP active. `b2=0` + `b3=1` → NTP serving. `b4=1` → NTP on fallback. All zeros → no time source.

> **Device health decode:** Same pattern as MCC. Read ENABLED/READY/WARN in parallel — one register byte per severity level. Drill into STATUS_BITS only when a flag is set.

> **Fire control decode (for a clean fire):** `VOTE_BITS_BDC b3 (BDC_TOTAL_VOTE) = 1` AND `VOTE_BITS_MCC_RB b6 (FIRE_STATE) = 1` AND `VOTE_BITS_MCC_RB b7 (EMON) = 1`.

---

## Device Health

### Architecture

Three register bytes per controller provide a layered health view:

| Register | Meaning | Who sets |
|----------|---------|----------|
| `DEVICE_ENABLED_BITS` | Device is in service | Firmware config / operator |
| `DEVICE_READY_BITS` | Device is fully operational | Derived from XXX_STATUS_BITS |
| `DEVICE_WARN_BITS` | Device degraded but operational | Derived from XXX_STATUS_BITS |

**Source of truth:** `XXX_STATUS_BITS` per device. READY and WARN are computed summaries.

| Severity | `DEVICE_READY_BITS` | `DEVICE_WARN_BITS` | Definition |
|----------|--------------------|--------------------|------------|
| **READY** | 1 | 0 | All required conditions nominal. |
| **READY+WARN** | 1 | 1 | Primary function available but degraded. |
| **ERROR** | 0 | 0 | Cannot perform primary function. |
| *(invalid)* | 0 | 1 | Should never occur. |

**Key rules:**
- READY+WARN satisfies all advancement prerequisites. Only ERROR blocks advancement.
- `DEVICE_WARN_BITS` bit N is never set if `DEVICE_ENABLED_BITS` bit N is 0. Disabled devices have no warn state.

---

### State / Mode Rules

#### Mid-operation regression

| Severity | MCC device ERROR | BDC device ERROR | Recovery |
|----------|-----------------|-----------------|---------|
| **ERROR — critical** | `STATE→ISR`, `MODE→OFF` | `STATE→STNDBY`, `MODE→OFF` | Condition clears → operator re-advances |
| **ERROR — non-critical** | No state/mode change | No state/mode change | Informational only |
| **WARN — any** | No change | No change | `DEVICE_WARN_BITS` set; clears naturally |

> MCC ERROR → ISR: laser safed; observation platform stays alive.
> BDC ERROR → STNDBY: beam director compromised; safe idle with no engagement posture.
> Non-critical devices (GNSS, INCL, NTP, PTP, CRG): ERROR is informational only. Fire chain drops naturally when ISR or STNDBY is entered.

#### Advancement prerequisites

`SET_SYSTEM_STATE` and `SET_GIMBAL_MODE` return `STATUS_PREREQ_FAIL (0x07)` if prerequisites are not met.
THEIA should surface the blocking STATUS_BITS byte — operator resolves without source access.

| Transition | All must be READY or READY+WARN |
|-----------|--------------------------------|
| `OFF → STNDBY` | MCC CommHealth · BDC CommHealth |
| `STNDBY → ISR` | BDC_GIM · BDC_VIS · BDC_JET |
| `ISR → COMBAT` | MCC_HEL · MCC_BAT `isNotLowVoltage` · BDC_FSM · MCC_TMC (cooling-required HW only — V2/V3·6kW) |

| Mode | Minimum required |
|------|-----------------|
| `→ POS / RATE` | BDC_GIM · BDC_JET · (BDC_VIS OR BDC_MWIR) |
| `→ CUE` | BDC_GIM · BDC_JET · (BDC_VIS OR BDC_MWIR) |
| `→ ATRACK (VIS)` | BDC_GIM · BDC_JET · BDC_VIS |
| `→ ATRACK (MWIR)` | BDC_GIM · BDC_JET · BDC_MWIR |
| `ATRACK → FTRACK` | BDC_GIM · BDC_JET · BDC_FSM · (BDC_VIS OR BDC_MWIR) |

#### Immediate MODE→OFF triggers

```
BDC_GIM ERROR                                → MODE→OFF (all modes)
BDC_JET ERROR                                → MODE→OFF (all modes)
BDC_VIS ERROR AND BDC_MWIR ERROR             → MODE→OFF (both cameras lost)
BDC_VIS ERROR (while ATRACK on VIS)          → MODE→OFF
BDC_MWIR ERROR (while ATRACK on MWIR)        → MODE→OFF
```

---

### Device Criticality Matrix

| Device | Mid-op STATE change | Mid-op MODE change | Blocks STATE > | Blocks MODE > |
|--------|--------------------|--------------------|----------------|---------------|
| **MCC_TMC** | COMBAT→ISR (cooling HW only) | →OFF | ISR→COMBAT (cooling HW only) | No |
| **MCC_HEL** | COMBAT→ISR | →OFF | ISR→COMBAT | No |
| **MCC_BAT** `!isNotLowVoltage` | COMBAT→ISR | →OFF | ISR→COMBAT | No |
| **MCC_CRG** | No | No | No | No |
| **MCC_GNSS** | No | No | No | No |
| **MCC_BDC** | COMBAT→ISR | →OFF | ISR→COMBAT | No |
| **BDC_GIM** | ISR/COMBAT→STNDBY | →OFF (all modes) | STNDBY→ISR | All modes |
| **BDC_VIS** | ISR/COMBAT→STNDBY | →OFF (both lost or ATRACK VIS) | STNDBY→ISR | ATRACK(VIS) |
| **BDC_MWIR** | No | →OFF (both lost or ATRACK MWIR) | No | ATRACK(MWIR) |
| **BDC_FSM** | COMBAT→ISR | FTRACK/COMBAT→ATRACK | ISR→COMBAT | FTRACK |
| **BDC_JET** | ISR/COMBAT→STNDBY | →OFF (all modes) | STNDBY→ISR | All modes |
| **BDC_INCL** | No | No | No | No |
| **NTP / PTP** | No | No | No | No |

> ⚠️ **FSM:** Beam steering. Loss during COMBAT → STATE→ISR (laser safed). MODE regresses to ATRACK not OFF — coarse tracker remains viable.
> ⚠️ **MCC_BDC:** Vote chain link only. BDC observable via A3. Does NOT block STNDBY→ISR.
> ⚠️ **TMC:** COMBAT prerequisite on cooling-required HW only (V2/V3·6kW). Firmware determines from `LaserModel`.
> ⚠️ **GNSS/INCL/NTP/PTP:** Informational only. Position/attitude latched on last valid data.

---

## Key Enumerations

### SYSTEM_STATES
| Value | Name |
|-------|------|
| 0 | OFF |
| 1 | STNDBY |
| 2 | ISR |
| 3 | COMBAT |
| 4 | MAINT |
| 5 | FAULT |

### BDC_MODES
| Value | Name |
|-------|------|
| 0 | OFF |
| 1 | POS |
| 2 | RATE |
| 3 | CUE |
| 4 | ATRACK |
| 5 | FTRACK |

### BDC_CAM_IDS
| Value | Name |
|-------|------|
| 0 | VIS (Alvium) |
| 1 | MWIR |

### MCC_VOTES (icd.cs / icd.hpp)
| Bit | Mask | Name | Notes |
|-----|------|------|-------|
| b0 | 0x01 | NOT_ABORT | INVERTED — CLEAR = abort ACTIVE |
| b1 | 0x02 | ARMED | D3 HW readback |
| b2 | 0x04 | BDC_VOTE | D4 hardwire from BDC |
| b3 | 0x08 | LASER_TOTAL_HW | D7 AND gate: NotAbort && Armed && BDCVote |
| b4 | 0x10 | SW_VOTE | Combat && BatNotLow |
| b5 | 0x20 | TRIGGER | Fire requested heartbeat |
| b6 | 0x40 | FIRE_STATE | D45 final gate output |
| b7 | 0x80 | EMON | IPG energy confirmed — display only, excluded from composites |
| — | 0x03 | ARMED_NOMINAL | NOT_ABORT \| ARMED |
| — | 0x1F | READY_TO_FIRE | ARMED_NOMINAL \| BDC_VOTE \| LASER_TOTAL_HW \| SW_VOTE |
| — | 0x7F | FULL_FIRE_CHAIN | READY_TO_FIRE \| TRIGGER \| FIRE_STATE |

### MCC_VOTES2 (icd.cs / icd.hpp)
| Bit | Mask | Name | Notes |
|-----|------|------|-------|
| b0 | 0x01 | BAT_NOT_LOW | Input to SW_VOTE |
| b1 | 0x02 | TRAINING_MODE | Power clamped to 10%; status, not a gate |
| b2 | 0x04 | COMBAT | System_State == COMBAT; input to SW_VOTE |
| b3 | 0x08 | EMON_MISSING | FireState asserted, no EMON within timeout |
| b4 | 0x10 | EMON_UNEXPECTED | EMON present without fire chain |
| b5 | 0x20 | FIRE_INTERLOCKED | Trigger held but fire chain incomplete |

### BDC_VOTES (icd.cs / icd.hpp)
| Bit | Mask | Name |
|-----|------|------|
| b0 | 0x01 | BELOW_HORIZ_VOTE |
| b1 | 0x02 | IN_KIZ_VOTE |
| b2 | 0x04 | IN_LCH_VOTE |
| b3 | 0x08 | BDC_TOTAL_VOTE |
| b5 | 0x20 | HORIZ_LOADED |
| b7 | 0x80 | FSM_NOT_LIMITED |

### BDC_VOTES2 (icd.cs / icd.hpp)
| Bit | Mask | Name |
|-----|------|------|
| b0 | 0x01 | HORIZ_VOTE_OVERRIDE |
| b1 | 0x02 | KIZ_VOTE_OVERRIDE |
| b2 | 0x04 | LCH_VOTE_OVERRIDE |
| b3 | 0x08 | BDC_VOTE_OVERRIDE |
| b4 | 0x10 | IS_BELOW_HORIZ |
| b5 | 0x20 | IS_IN_KIZ |
| b6 | 0x40 | IS_IN_LCH |

### VERSION_PACK Encoding
```
uint32 bits[31:24] = major
uint32 bits[23:12] = minor
uint32 bits[11:0]  = patch
```

---

## Framing Reference

### A3 Frame Structure (521 bytes total)

```
Byte  0    : Magic HI  = 0xCB
Byte  1    : Magic LO  = 0x58
Byte  2    : CMD_BYTE
Byte  3–4  : SEQ_NUM   uint16 LE
Byte  5–6  : PAYLOAD_LEN uint16 LE (always 512 for 0xA1 response)
Bytes 7–518: PAYLOAD   (512 bytes)
Bytes 519–520: CRC16   uint16 LE (CRC-16/CCITT, poly=0x1021, init=0xFFFF, over bytes 0–518)
```

### STATUS Byte Codes

| Value | Name | Meaning |
|-------|------|---------|
| `0x00` | `STATUS_OK` | Command accepted and executed |
| `0x01` | `STATUS_CMD_REJECTED` | CMD_BYTE not in `EXT_CMDS[]` whitelist |
| `0x02` | `STATUS_BAD_MAGIC` | Magic bytes incorrect |
| `0x03` | `STATUS_BAD_CRC` | CRC check failed |
| `0x04` | `STATUS_BAD_LEN` | `PAYLOAD_LEN` does not match expected |
| `0x05` | `STATUS_SEQ_REPLAY` | SEQ_NUM within replay-rejection window |
| `0x06` | `STATUS_NO_DATA` | Register not yet populated |
| `0x07` | `STATUS_PREREQ_FAIL` | State/mode transition rejected — device health prerequisites not met. **New v4.0.0.** |

### 0xA4 — FRAME_KEEPALIVE Response Payload

| Bytes | Field | Value |
|-------|-------|-------|
| 0 | `protocol_version` | `0x01` |
| 1–2 | `echo_seq` | uint16 — echoes request SEQ_NUM |
| 3–6 | `uptime_ms` | uint32 — server uptime ms |
| 7–511 | reserved | `0x00` |

---

## Network Reference

| Node | IP | A3 Port |
|------|----|---------|
| MCC | 192.168.1.10 | 10050 |
| BDC | 192.168.1.20 | 10050 |
| THEIA | 192.168.1.208 | — |

External integration clients: 192.168.1.200–.254 by convention.
Sub-controllers (TMC .12, TRC .22, FMC .23) are not addressable via A3.

---

## Video Stream

TRC (Jetson Orin NX, 192.168.1.22) streams H.264 RTP video to THEIA (192.168.1.208).

| Parameter | Value |
|-----------|-------|
| Transport | UDP RTP unicast |
| Port | 5000 (UDP, fixed) |
| Codec | H.264 hardware encoded |
| Resolution | 1280 × 720 (fixed — must be passed explicitly) |
| Framerate | 60 fps (fixed) |
| Bitrate | 10 Mbps |
| E2E latency (HW decode) | 30–80 ms |
| E2E latency (SW decode) | 50–100 ms |
| UDP receive buffer | 2 MB minimum |
| Jitter buffer | 50 ms, drop-on-latency=true |
| PixelShift correction | **−420 px horizontal** (fixed Jetson encoder artefact) |

---

## THEIA Status Response — CMD `0xAF`

Sent by THEIA to CUE source continuously at 10 Hz when requested. Payload: 30 bytes.

| Payload Offset | Size | Type | Field | Notes |
|----------------|------|------|-------|-------|
| 0 | 1 | byte | System State | SYSTEM_STATES enum |
| 1 | 1 | byte | System Mode | BDC_MODES enum |
| 2 | 1 | byte | Active CAM ID | VIS=0, MWIR=1 |
| 3 | 1 | byte | VOTE_BITS_MCC | Gate-chain summary — see MCC_VOTES enum |
| 4 | 1 | byte | VOTE_BITS_BDC2 | BDC raw/override detail — see BDC_VOTES2 enum |
| 5 | 1 | byte | VOTE_BITS_BDC | BDC computed votes — see BDC_VOTES enum |
| 6 | 4 | float | Gimbal Az NED | degrees |
| 10 | 4 | float | Gimbal EL NED | degrees |
| 14 | 4 | float | Laser Az NED | degrees |
| 18 | 4 | float | Laser EL NED | degrees |
| 22 | 4 | uint32 | RESERVED | 0x00 |
| 26 | 4 | uint32 | RESERVED | 0x00 |

> For a clean fire: `VOTE_BITS_BDC b3 (BDC_TOTAL_VOTE) = 1` AND `VOTE_BITS_MCC b6 (FIRE_STATE) = 1` AND `VOTE_BITS_MCC b7 (EMON) = 1`.

---

## THEIA POS/ATT Report — CMD `0xAB`

Payload: 32 bytes.

| Payload Offset | Size | Type | Field | Notes |
|----------------|------|------|-------|-------|
| 0 | 8 | double | Latitude | WGS-84 decimal degrees |
| 8 | 8 | double | Longitude | WGS-84 decimal degrees |
| 16 | 4 | float | Altitude HAE | metres |
| 20 | 4 | float | Roll | degrees NED |
| 24 | 4 | float | Pitch | degrees NED |
| 28 | 4 | float | Yaw | degrees NED |

---

## Open Items / TBDs

| # | Device | Item |
|---|--------|------|
| 1 | TMC | Cooling requirement by HW — V2/V3·6kW vs V1/V3·3kW firmware rule |
| 2 | HEL | IPGMsg StatusWord temperature bit positions — 3K and 6K full bit map |
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
| 13 | FSM | Home position tolerance (counts) |
| 14 | VIS | Alvium max operating temperature — confirm from datasheet (suggest 60°C) |
| 15 | INCL | Operational attitude bounds (degrees) for `isLevel` |
| 16 | JET | Dual WARN/ERROR threshold tracking — confirm firmware approach for load/temp |

---

## Action Items

| ID | Item | Status |
|----|------|--------|
| ~~NEW-37~~ | `MSG_MCC.cs` PTP bits + ENG GUI display | ✅ Closed |
| ~~FC-CONSISTENCY-1~~ | EMON_MISSING / EMON_UNEXPECTED / FIRE_INTERLOCKED | ✅ Closed v4.0.0 — in VOTE_BITS_MCC2 |
| NEW-38b | BDC PTP integration | ⏳ Pending |
| NEW-38c | FMC PTP integration | ⏳ Pending |
| NEW-38d | TRC PTP integration | ⏳ Pending |
| TBD-1–16 | Open items listed above | ⏳ Pending |
