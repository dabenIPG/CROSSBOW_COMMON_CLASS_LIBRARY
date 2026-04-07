# CROSSBOW — INT_OPS Interface Control Document

**Document:** `CROSSBOW_ICD_INT_OPS`
**Doc #:** IPGD-0004
**Version:** 3.3.7
**Date:** 2026-04-06 (session 29)

---

## Version History

**v3.3.7 changes (session 29 — 2026-04-06):**
- Firmware replay window fix (A3 reconnection): new client detection moved before `frameCheckReplay()` in MCC `handleA3Frame` and BDC `handleA3Frame`. THEIA reconnects cleanly after slot expiry — no longer permanently locked out until controller reboot. Symptom was `drop #2 after 0.0s` immediately on reconnect with all subsequent commands rejected.
- A3 unsolicited subscription on connect removed from C# client — THEIA now sends single `0xA4` registration only. `SET_UNSOLICITED` (`0xA0`) must be sent explicitly to subscribe. This matches user-controlled model on all other transports.

---

**v3.3.6 changes (session 35/36 — 2026-04-04):**
- `0xA4` renamed `EXT_FRAME_PING` → `FRAME_KEEPALIVE`; extended to A2 and all controllers. Empty payload = ACK only. Payload `{0x01}` = solicited REG1 (rate-gated 1 Hz per slot; suppressed if already subscribed).
- `0xA1 GET_REGISTER1` **retired inbound** — returns `STATUS_CMD_REJECTED`. `0xA1` remains the outbound `CMD_BYTE` in all REG1 frames.
- `0xA3 GET_REGISTER3` **retired** — returns `STATUS_CMD_REJECTED`.
- `0xA0 SET_UNSOLICITED` updated — per-slot `wantsUnsolicited` flag; `isUnSolicitedEnabled` global retired.
- `STATUS_BITS` bit 7 (`isUnsolicitedModeEnabled`) retired on MCC (byte 9) and BDC (byte 10) — always `0`.
- `GetCurrentTime()` holdover active on all controllers — `activeTimeSource = NONE` during holdover; time continues advancing from last good value.
- `isPTP_Enabled` defaults to `false` (FW-B3 deferred). C# `activeTimeSourceLabel` will show `"NONE"` until `TIMESRC PTP` is issued.
- FMC PTP integration complete; FMC socket budget 4/8
- FMC REG1 byte 28 epoch: `NTP epoch Time` → `epoch Time (PTP/NTP)` — field is now PTP-sourced when synched
- FMC REG1 byte 7 `FSM STAT BITS` bits 2–3: `ntp.isSynched/ntpUsingFallback` → `RES` (moved to TIME_BITS)
- FMC REG1 byte 44: `TIME_BITS` added — same decode as MCC byte 253, BDC byte 391, TMC byte 61
- BDC socket fix: PTP now initialises correctly (9→7 sockets); no OPS register impact

**v3.3.4 changes (session 32 — 2026-04-04):**
- `0xB0 SET_BDC_REINIT` payload: `7=RTC` → `7=PTP`
- BDC REG1 byte 8 `DEVICE_ENABLED_BITS` bit 7: `RES` → `PTP (isPTP_Enabled)`
- BDC REG1 byte 9 `DEVICE_READY_BITS` bit 7: `RES` → `PTP (ptp.isSynched)`
- BDC REG1 byte 10 `STAT BITS` bits 1–2: `ntpUsingFallback/ntpHasFallback` → `RES` (moved to TIME_BITS byte 391)
- BDC REG1 byte 12 epoch field: `NTP epoch Time` → `epoch Time (PTP/NTP)`
- BDC REG1 byte 391: `RESERVED (121)` → `TIME_BITS (1)` + `RESERVED (120)`
- MCC REG1 byte 10 `STAT BITS2` bits 0–2: `ntpUsingFallback/ntpHasFallback/usingPTP` → `RES` (moved to TIME_BITS byte 253)
- MCC REG1 byte 253: `RESERVED (3)` → `TIME_BITS (1)` + `RESERVED (2)`
- `TIME_BITS` layout (bits 0–5): isPTP_Enabled; ptp.isSynched; usingPTP; ntp.isSynched; ntpUsingFallback; ntpHasFallback
- HMI time source decode updated — see TIME_BITS note below each register table
- TMC REG1 byte 61 reassigned: RESERVED → `TMC STAT BITS3` — PTP+NTP time status
- TMC STAT BITS1 bits 5/6 vacated (moved to BITS3)
- TMC FW version: `3.0.5` (session 30/31 PTP integration)

**v3.3.2 changes (session 30 — 2026-04-04):**
- `MCC_DEVICES` slot 4 renamed `RTCLOCK` → `PTP` in firmware and C# (`defines.hpp`, `defines.cs`)
- `0xE0 SET_MCC_REINIT` / `0xE1 SET_MCC_DEVICES_ENABLE` payload corrected: `0=NTP` (NTP only), `4=PTP` (PTP only)
- New MCC serial commands: `REINIT <device>`, `ENABLE <device> <0|1>`, `PTPDEBUG <0-3>`

**v3.3.1 changes (session 29 — 2026-03-28):**
- NEW-36 closed: PTP HW verified — `offset_us=12µs`, correct time, ENG GUI confirmed
- NEW-37 closed: `MSG_MCC.cs` + ENG GUI verified
- `0xE0 SET_MCC_REINIT` / `0xE1 SET_MCC_DEVICES_ENABLE` payload: device index 0 = `NTP/PTP` (controls both), index 4 = RES (was RTCLOCK, deprecated)
- `SEND_REG_01` bug fixed: epoch time field now routes through `GetCurrentTime()` — was `ntp.GetCurrentTime()` (ENG GUI received wrong time when PTP active)

**v3.3.0 changes (session 28 — 2026-03-28):**
- MCC FW version updated: `3.0.6` → `3.1.0` (PTP integration); VERSION WORD at byte 245 = `0x03001000`
- MCC REG1 byte 7 `DEVICE_ENABLED_BITS` bit 4: `RES` → `isPTP_Enabled`
- MCC REG1 byte 8 `DEVICE_READY_BITS` bit 4: `RES` → `isPTP_Ready` (`ptp.isSynched`)
- MCC REG1 byte 10 `STAT BITS2` bit 2: `RES` → `usingPTP` — set when PTP is the active time source
- Time source decode (HMI reference):
  - `DEVICE_ENABLED[4]=1` + `DEVICE_READY[4]=1` + `STAT_BITS2[2]=1` → PTP active (running on GNSS time)
  - `DEVICE_READY[4]=0` + `DEVICE_READY[0]=1` → NTP serving (PTP not yet synched or lost)
  - `DEVICE_READY[4]=0` + `DEVICE_READY[0]=1` + `STAT_BITS2[0]=1` → NTP fallback serving
  - `DEVICE_READY[4]=0` + `DEVICE_READY[0]=0` → No time source — check GNSS / network
- NEW-37: `MSG_MCC.cs` — unpack PTP bits for THEIA/ENG GUI (see Action Items)
- NEW-38: Propagate PTP pattern to BDC, TMC, FMC, TRC in a follow-on session (see Action Items)

**v3.2.0 changes (session 27 — 2026-03-26):**
- `0xA2 GET_REGISTER2` removed from INT_OPS command table — replaced by `SET_NTP_CONFIG` which is INT_ENG only (not on A3 EXT whitelist; see INT_ENG for full specification)
- MCC REG1 byte 10 `STAT BITS2` updated: bits 0/1 now carry NTP fallback state
  - bit 0: `ntpUsingFallback` — system is currently syncing from fallback server
  - bit 1: `ntpHasFallback` — a fallback server is configured
- BDC REG1 byte 10 `STAT BITS` updated: bits 1/2 now carry NTP fallback state (replaces RES)
  - bit 1: `ntpUsingFallback`
  - bit 2: `ntpHasFallback`

**v3.1.0 changes (session 22 — 2026-03-16):**
- Document renamed from `ICD_EXTERNAL_OPS` to `ICD_EXTERNAL_INT` — scope label alignment
- Scope clarified: INT_OPS is the full A3 operator control interface and reference spec
  for THEIA and vendor HMI implementations
- EXT_OPS content (0xAF, 0xAB response layouts, UDP:10009 framing) moved to
  `CROSSBOW_ICD_EXT_OPS` (IPGD-0005)
- Tier Overview section added — A1/A2/A3 model, INT_OPS boundary clarification
- Network Reference section updated — THEIA .208, HYPERION .206, IPG reserved .200–.209,
  third-party .210–.254, NTP .33
- Relationship to EXT_OPS section added
- THEIA IP updated throughout: 192.168.1.8 → 192.168.1.208
- TRC3 renamed to TRC throughout
- PixelShift corrected: −20 px → −420 px
- Doc numbers added per CROSSBOW Document Register (IPGD-0001)

**v3.0.2 changes (session 20 — 2026-03-16):**
- `## Video Stream` section added. Documents H.264 RTP stream from TRC (192.168.1.22):
  port 5000, 1280×720, 60 fps, 10 Mbps. Receive requirements (decoder, UDP buffer,
  jitter, PixelShift −420 px correction). Framerate control via `0xD2` (pending).
  Multicast via `0xD1` (pending, group 239.127.1.21).

**v3.0.1 changes (session 17):**
- EXT_OPS framing protocol defined — `0xCB 0x48` magic, CRC-16/CCITT, SEQ_NUM
- THEIA Status Response (CMD `0xAF`) added — 30-byte payload, 39-byte total frame
- THEIA POS/ATT Report (CMD `0xAB`) added — 32-byte payload, 41-byte total frame;
  altitude corrected to HAE
- Network Reference updated with UDP:10009

---

> **Document policy:** This document contains only `INT_OPS`-scoped commands.
> Engineering-only (`INT_ENG`) commands, FMC/TMC internals, and reserved bytes are
> omitted. For the full command set see `CROSSBOW_ICD_INT_ENG` (IPGD-0003).

> **Framing and transport:** A3 port (10050), magic `0xCB 0x58`, 521 bytes total.
> Full protocol specification in **ARCHITECTURE.md** (IPGD-0006) §6. STATUS byte
> codes and payload layout in the **Framing Reference** section of this document.

> **Targets:** A3 clients may address **MCC** (192.168.1.10) and **BDC**
> (192.168.1.20) directly via A3. **TRC** is accessible via BDC routing only —
> send TRC commands (0xD0–0xDF) to BDC; BDC forwards internally. TMC and FMC
> are internal to their respective controllers and not accessible via A3.

---

## Network and Interface Tier Overview

CROSSBOW uses a three-tier interface model. INT_OPS clients operate at Tier 1 — full
A3 operator control access.

```
┌─────────────────────────────────────────────────────────┐
│  A1 — Controller Bus                                    │
│  Internal controller-to-controller interface.           │
│  No external access.                                    │
├─────────────────────────────────────────────────────────┤
│  A2 — Engineering and Maintenance Interface             │
│  IPG engineering use: firmware deployment, diagnostics, │
│  full register access. Not available to integrators.    │
│  Exception: planned auth/service pathway (in scope).    │
└───────────────────────┬─────────────────────────────────┘
                        │ A3 boundary
┌───────────────────────▼─────────────────────────────────┐
│  A3 — INT_OPS — Tier 1 (this document)                  │
│  A3 port 10050, magic 0xCB 0x58                         │
│  Full operator command set — MCC, BDC, TRC via BDC      │
│                                                         │
│   THEIA (.208) — IPG reference HMI                      │
│   Vendor HMI (.210–.254) — bespoke implementations      │
└───────────────────────┬─────────────────────────────────┘
                        │ EXT_OPS boundary
┌───────────────────────▼─────────────────────────────────┐
│  EXT_OPS — Tier 2 (CROSSBOW_ICD_EXT_OPS, IPGD-0005)        │
│  UDP:10009, magic 0xCB 0x48                             │
│  CUE input — HYPERION or third-party providers          │
└─────────────────────────────────────────────────────────┘
```

| Tier | Transport | Magic | Audience |
|------|-----------|-------|----------|
| A1 — Controller Bus | Internal only | — | Controller firmware only — no external access |
| A2 — Engineering | Internal ports | — | IPG engineering via ENG GUI — planned auth/service pathway |
| A3 — INT_OPS — Tier 1 | A3 port 10050 | `0xCB 0x58` | THEIA, vendor HMI integrators — this document |
| EXT_OPS — Tier 2 | UDP:10009 | `0xCB 0x48` | CUE providers, HYPERION — see IPGD-0005 |

---

## Relationship to EXT_OPS

INT_OPS is the operator control plane — it provides full A3 access to MCC, BDC, and
TRC (via BDC). EXT_OPS is the complementary cueing input interface — it feeds track
data into whichever INT_OPS client is running.

THEIA is IPG's reference INT_OPS implementation. A vendor may build a bespoke HMI
using this ICD to replace THEIA entirely — in that case the vendor implements both:
INT_OPS for system control and EXT_OPS (IPGD-0005) to receive CUE input.

```
EXT_OPS (cueing input — CROSSBOW_ICD_EXT_OPS, IPGD-0005)
    ↓
INT_OPS client — THEIA or vendor HMI (this document)
    ↓
A3 — MCC, BDC, TRC (via BDC)
    ↓
A1/A2 — internal controllers
```

For EXT_OPS interface definition see `CROSSBOW_ICD_EXT_OPS` (IPGD-0005).

---

## Command Scope

| Scope | Meaning |
|-------|---------|
| `INT_OPS` | Operator-accessible — this document. Sent via A3 port (10050, magic `0xCB 0x58`). |
| `INT_ENG` | Engineering only — A2 port (10018). Omitted from this document. See `CROSSBOW_ICD_INT_ENG` (IPGD-0003). |
| `EXT_OPS` | Cueing input interface — CUE providers and HYPERION → INT_OPS client. See `CROSSBOW_ICD_EXT_OPS` (IPGD-0005). |

---

## 0xA0–0xAF — System Commands

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xA0 | SET_UNSOLICITED | Subscribe/unsubscribe to unsolicited telemetry push. Sets per-slot `wantsUnsolicited` on the sender's client table entry. `{0x01}` = subscribe; `{0x00}` = unsubscribe. | uint8 0=off, 1=on | MCC, BDC |
| 0xA1 | RES_A1 | **RETIRED inbound (session 35)** — returns `STATUS_CMD_REJECTED`. `0xA1` remains the outbound `CMD_BYTE` in REG1 frames. Use `0xA4 {0x01}` for solicited REG1. | — | MCC, BDC |
| 0xA2 | SET_NTP_CONFIG | **INT_ENG only** — NTP server configuration and resync. Not available on A3 external port. See INT_ENG ICD (IPGD-0003) for full specification. | — | MCC |
| 0xA3 | RES_A3 | **RETIRED (session 35)** — returns `STATUS_CMD_REJECTED`. | — | MCC, BDC |
| 0xA4 | FRAME_KEEPALIVE | Register/keepalive — replaces `EXT_FRAME_PING` (session 35). Empty payload = ACK only (byte 0=`0x01`, bytes 1–2=echo SEQ_NUM, bytes 3–6=uptime_ms). Payload `{0x01}` = ACK + solicited REG1 (rate-gated 1 Hz per slot; suppressed if subscribed). Any accepted frame auto-registers sender and refreshes 60-second liveness. | 0 or 1 byte | MCC, BDC |
| 0xA5 | SET_SYSTEM_STATE | Set system state | uint8 (SYSTEM_STATES enum) | MCC, BDC |
| 0xA6 | SET_GIMBAL_MODE | Set gimbal/tracker mode | uint8 (BDC_MODES enum) | MCC, BDC |
| 0xA7 | SET_LCH_MISSION_DATA | Load LCH/KIZ mission data, clear all windows | uint8 which (0=KIZ,1=LCH); uint8 isValid; uint64 startTimeMission_min; uint64 stopTimeMission_max; int16 az1; int16 el1; int16 az2; int16 el2; uint16 nTargets; uint16 nTotalWindows | BDC |
| 0xA8 | SET_LCH_TARGET_DATA | Load LCH/KIZ target with windows | uint8 which (0=KIZ,1=LCH); uint16 nWindows; uint16 startTimeTarget_min; uint16 stopTimeTarget_max; uint16 az1; uint16 el1; uint16 az2; uint16 el2; nWindows×[uint16 wt1, uint16 wt2] | BDC |
| 0xAC | SET_BDC_HORIZ | Set horizon elevation vector | float[360] | BDC |
| 0xAD | SET_HEL_POWER | Set laser power level | uint8 [0–100] % | MCC |
| 0xAE | CLEAR_HEL_ERROR | Clear laser error state | none | MCC |

---

## 0xB0–0xBF — BDC Commands

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xB0 | SET_BDC_REINIT | Reinitialise BDC subsystem | uint8 subsystem (0=NTP, 1=GIMBAL, 2=FUJI, 3=MWIR, 4=FSM, 5=JETSON, 6=INCL, 7=PTP) | BDC |
| 0xB2 | SET_GIM_POS | Set gimbal position | int32 pan, int32 tilt | BDC |
| 0xB3 | SET_GIM_SPD | Set gimbal speed | int16 pan, int16 tilt | BDC |
| 0xB4 | SET_CUE_OFFSET | Set cue track offset (AZ, EL) | float az_deg, float el_deg | BDC |
| 0xB5 | CMD_GIM_PARK | Park gimbal at home | none | BDC |
| 0xB6 | SET_GIM_LIMITS | Set gimbal wrap limits | int32 panMin, int32 panMax, int32 tiltMin, int32 tiltMax | BDC |
| 0xB7 | SET_PID_GAINS | Set PID gains (cue or AT loop) | uint8 which (0=cue, 1=AT); float kpp, kip, kdp, kpt, kit, kdt | BDC |
| 0xB8 | SET_PID_TARGET | Set PID target setpoint. Sub-cmd 0x00: x=NED az (deg), y=NED el (deg); BDC applies CUE_OFFSET then full NED→gimbal rotation. Sub-cmd 0x01: x=tx (pixels), y=ty (pixels). THEIA sends sub-cmd 0x00 only. | uint8 sub-cmd (0=CUE NED, 1=video px); float x LE; float y LE; float pidScale LE | BDC |
| 0xB9 | SET_PID_ENABLE | Enable/disable PID loop | uint8 which (0=cue, 1=video); uint8 0/1 | BDC |
| 0xBA | SET_SYS_LLA | Set platform geodetic position | float lat, float lng, float alt | BDC |
| 0xBB | SET_SYS_ATT | Set platform attitude (RPY) | float roll, float pitch, float yaw | BDC |

---

## 0xC0–0xCF — BDC/Camera Commands

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xC1 | SET_CAM_MAG | VIS camera zoom | uint8 mag index | BDC |
| 0xC2 | SET_CAM_FOCUS | VIS camera focus | uint16 focus position | BDC |
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

> TRC commands are routed through BDC over A3. The INT_OPS Target column shows `BDC` — BDC
> forwards to TRC internally. Direct TRC A2 access is engineering-only.

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xD0 | ORIN_CAM_SET_ACTIVE | Set active camera | uint8 BDC_CAM_IDS (0=VIS, 1=MWIR) | BDC |
| 0xD3 | ORIN_SET_STREAM_OVERLAYS | Set HUD overlay bitmask. bit0=Reticle, bit1=TrackPreview, bit2=TrackBox, bit3=CueChevrons, bit4=AC_Projections, bit5=AC_LeaderLines, bit6=FocusScore, bit7=OSD | uint8 bitmask | BDC |
| 0xD4 | ORIN_ACAM_SET_CUE_FLAG | Set cue flag indicator (HUD chevrons) | uint8 0/1 | BDC |
| 0xD5 | ORIN_ACAM_SET_TRACKGATE_SIZE | Set track gate width/height | uint8 w, uint8 h (pixels, min 16) | BDC |
| 0xD6 | ORIN_ACAM_ENABLE_FOCUSSCORE | Enable focus score computation | uint8 0/1 | BDC |
| 0xD7 | ORIN_ACAM_SET_TRACKGATE_CENTER | Set track gate preview center (no tracker init) | uint16 x, uint16 y (pixels) | BDC |
| 0xD8 | ORIN_SET_STREAM_TESTPATTERNS | Enable test pattern for specified camera. | uint8 cam_id (0=VIS, 1=MWIR) + uint8 enable (0=off, 1=on) | BDC |
| 0xDA | ORIN_ACAM_RESET_TRACKB | Reset MOSSE tracker to current preview gate | none | BDC |
| 0xDB | ORIN_ACAM_ENABLE_TRACKERS | Enable/disable tracker for active camera | uint8 tracker_id (0=AI, 1=MOSSE, 2=Centroid, 3=Kalman); uint8 0/1 | BDC |
| 0xDC | ORIN_ACAM_SET_ATOFFSET | Set AT reticle offset | int8 dx, int8 dy (pixels, −128 to 127) | BDC |
| 0xDD | ORIN_ACAM_SET_FTOFFSET | Set FT (fine-track) offset | int8 dx, int8 dy (pixels, −128 to 127) | BDC |
| 0xDE | ORIN_SET_VIEW_MODE | Set compositor output view | uint8 (0=CAM1, 1=CAM2, 2=PIP4, 3=PIP8) | BDC |
| 0xDF | ORIN_ACAM_COCO_ENABLE | Enable/disable COCO intra-trackbox inference. Model must be loaded (ISR lifecycle). Camera switch auto-disables COCO. | uint8 op [, uint8 param]: 0=off, 1=on, 2=load, 3=unload, 4=set_drift, 5=set_interval | BDC |

---

## 0xE0–0xEF — MCC / PMS / TMS Commands

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xE0 | SET_MCC_REINIT | Reinitialise MCC subsystem | uint8 subsystem (0=NTP, 1=TMC, 2=HEL, 3=BAT, 4=PTP, 5=CRG, 6=GNSS, 7=BDC) — index 0=NTP only; index 4=PTP only | MCC |
| 0xE1 | SET_MCC_DEVICES_ENABLE | Enable/disable MCC-managed device | uint8 device (0=NTP, 1=TMC, 2=HEL, 3=BAT, 4=PTP, 5=CRG, 6=GNSS, 7=BDC); uint8 0/1 — device 4 (PTP): `0` forces NTP-only mode | MCC |
| 0xE3 | PMS_CHARGER_ENABLE | Enable charger | uint8 0/1 | MCC |
| 0xE7 | TMS_INPUT_FAN_SPEED | Set fan speed | uint8 which (0/1); uint8 speed (0=off, 128=low, 255=high) | MCC |
| 0xEB | TMS_SET_TARGET_TEMP | Set TMS target temperature | uint8 temp °C — **enforced range [10–40°C]**; firmware clamps silently. | MCC |
| 0xED | PMS_SET_CHARGER_LEVEL | Set charger current level | uint8 level (low=10, med=30, high=55) | MCC |

---

## 0xF0–0xFF — FSM / FMC Commands (routed via BDC)

| Byte | Enum | Description | Payload | INT_OPS Target |
|------|------|-------------|---------|----------------|
| 0xF1 | BDC_SET_FSM_HOME | FSM set home position | int16 x, int16 y | BDC |
| 0xF2 | BDC_SET_FSM_IFOVS | FSM set iFOV scaling | float x, float y | BDC |
| 0xF3 | FMC_SET_FSM_POS | FSM set position | int16 x, int16 y | BDC |
| 0xF6 | BDC_SET_FSM_TRACK_ENABLE | FSM track mode enable | uint8 0/1 | BDC |
| 0xFA | BDC_SET_STAGE_HOME | Focus stage waist home | uint32 position | BDC |
| 0xFB | FMC_SET_STAGE_POS | Focus stage set position | uint32 position | BDC |

---

## MCC Register 1 — Response to `0xA1`

Sent by MCC in response to `GET_REGISTER1` (0xA1) or on an unsolicited basis.
Fixed block size: **256 bytes** in payload, always padded to 512-byte payload.

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES enum |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES enum |
| 3 | 3 | 5 | 2 | HB_ms | uint16 | ms between sends |
| 5 | 5 | 7 | 2 | dt_us | uint16 | µs in processing loop |
| 7 | 7 | 8 | 1 | MCC DEVICE_ENABLED_BITS | uint8 | 0:NTP; 1:TMC; 2:HEL; 3:BAT; 4:PTP; 5:CRG; 6:GNSS; 7:BDC |
| 8 | 8 | 9 | 1 | MCC DEVICE_READY_BITS | uint8 | 0:NTP; 1:TMC; 2:HEL; 3:BAT; 4:PTP; 5:CRG; 6:GNSS; 7:BDC |
| 9 | 9 | 10 | 1 | MCC STAT BITS | uint8 | 0:isReady; 1:isSolenoid1_En; 2:isSolenoid2_En; 3:isLaserPower_En; 4:isChargerEnabled; 5:isNotBatLowVoltage; 6:RES; 7:RES *(was isUnsolicitedModeEnabled — retired session 35)* |
| 10 | 10 | 11 | 1 | MCC STAT BITS2 | uint8 | 0–2:RES *(moved to TIME_BITS byte 253 — session 32)*; 3:isVicorEnabled; 4:isRelay1En; 5:isRelay2En; 6:isRelay3En; 7:isRelay4En |
| 11 | 11 | 12 | 1 | MCC VOTE BITS | uint8 | 0:isLaserTotalHW_Vote_rb; 1:isNotAbort_Vote_rb; 2:isArmed_Vote_rb; 3:isBDA_Vote_rb; 4:isEMON_rb; 5:isLaserFireRequested_Vote; 6:isLaserTotal_Vote_rb; 7:isCombat_Vote_rb |
| 12 | 12 | 20 | 8 | NTP epoch Time | uint64 | ms since epoch |
| 20 | 20 | 21 | 1 | Temp 1 (Charger) | int8 | °C |
| 21 | 21 | 22 | 1 | Temp 2 (AIR) | int8 | °C |
| 22 | 22 | 26 | 4 | TPH: Temp | float | °C |
| 26 | 26 | 30 | 4 | TPH: Pressure | float | Pa |
| 30 | 30 | 34 | 4 | TPH: Humidity | float | % |
| 34 | 34 | 36 | 2 | Battery Pack Voltage | uint16 | centi-volts (e.g. 1260 = 12.60 V) |
| 36 | 36 | 38 | 2 | Battery Pack Current | int16 | centi-amps (e.g. −450 = −4.50 A) |
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
| 66 | 66 | 130 | 64 | TMC FULL REG | TMC_REG | 64-byte block — thermal status (opaque to INT_OPS clients) |
| 130 | 130 | 131 | 1 | NTP HB | uint8 | s/10 |
| 131 | 131 | 132 | 1 | HEL HB | uint8 | s/10 |
| 132 | 132 | 133 | 1 | BAT HB | uint8 | s/10 |
| 133 | 133 | 134 | 1 | CRG HB | uint8 | s/10 |
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
| 244 | 244 | 245 | 1 | CHARGER STATUS BITS | uint8 | bit0:isConnected; 1:isHealthy; 2:isCharging; 3:isFullyCharged; 4:isHighCharge; 5:is220V |
| 245 | 245 | 249 | 4 | MCC VERSION WORD | uint32 | VERSION_PACK(major, minor, patch) |
| 249 | 249 | 253 | 4 | MCU Temp | float | °C |
| 253 | 253 | 254 | 1 | TIME_BITS | uint8 | bit0:isPTP_Enabled; bit1:ptp.isSynched; bit2:usingPTP; bit3:ntp.isSynched; bit4:ntpUsingFallback; bit5:ntpHasFallback; bit6–7:RES |
| 254 | 254 | 256 | 2 | RESERVED | — | 0x00 |
| 256 | 256 | 512 | 256 | RESERVED | — | 0x00 — padded to 512 |

**Defined: 254 bytes. Padded to 512 bytes. Always followed by A3 frame overhead.**

> **Time source decode (HMI — session 32):** Read `TIME_BITS` at byte 253.
> `TIME_BITS[2]=1` → PTP active (GNSS time). `TIME_BITS[2]=0` + `TIME_BITS[3]=1` → NTP serving.
> `TIME_BITS[4]=1` → NTP on fallback server. All zeros → no time source.

---

## BDC Register 1 — Response to `0xA1`

Sent by BDC in response to `GET_REGISTER1` (0xA1) or on an unsolicited basis.
Fixed block size: **512 bytes**.

Embedded sub-registers (opaque to INT_OPS clients — field detail in full ICD):
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
| 8 | 8 | 9 | 1 | BDC DEVICE_ENABLED_BITS | uint8 | 0:NTP; 1:GIMBAL; 2:FUJI; 3:MWIR; 4:FSM; 5:JETSON; 6:INCL; 7:PTP *(session 32)* |
| 9 | 9 | 10 | 1 | BDC DEVICE_READY_BITS | uint8 | 0:NTP; 1:GIMBAL; 2:FUJI; 3:MWIR; 4:FSM; 5:JETSON; 6:INCL; 7:PTP *(ptp.isSynched — session 32)* |
| 10 | 10 | 11 | 1 | BDC STAT BITS | uint8 | 0:isReady; 1–6:RES *(session 32)*; 7:RES *(was isUnsolicitedModeEnabled — retired session 35)* |
| 11 | 11 | 12 | 1 | BDC STAT BITS2 | uint8 | 0:isPidEnabled; 1:isVPidEnabled; 2:isFTTrackEnabled; 3:isVicorEnabled; 4:isRelay1En; 5:isRelay2En; 6:isRelay3En; 7:isRelay4En |
| 12 | 12 | 20 | 8 | epoch Time (PTP/NTP) | uint64 | ms since epoch — PTP when synched, NTP otherwise |
| 20 | 20 | 21 | 1 | GIMBAL STATUS BITS | uint8 | 0:isReady; 1:isConnected; 2:isStarted; 3–7:RES |
| 21 | 21 | 25 | 4 | Gimbal Pan Count | int32 | from galil (dr) |
| 25 | 25 | 29 | 4 | Gimbal Tilt Count | int32 | from galil (dr) |
| 29 | 29 | 33 | 4 | Gimbal Pan Speed | int32 | from galil (dr) |
| 33 | 33 | 37 | 4 | Gimbal Tilt Speed | int32 | from galil (dr) |
| 37 | 37 | 38 | 1 | Gimbal Pan Stop Code | uint8 | from galil (dr) |
| 38 | 38 | 39 | 1 | Gimbal Tilt Stop Code | uint8 | from galil (dr) |
| 39 | 39 | 41 | 2 | Gimbal Pan Status | uint16 | from galil (dr) |
| 41 | 41 | 43 | 2 | Gimbal Tilt Status | uint16 | from galil (dr) |
| 43 | 43 | 47 | 4 | Gimbal Pan Rel Angle | float | deg from home |
| 47 | 47 | 51 | 4 | Gimbal Tilt Rel Angle | float | deg from home |
| 51 | 51 | 55 | 4 | Gimbal Az NED Angle | float | AZ NED deg |
| 55 | 55 | 59 | 4 | Gimbal EL NED Angle | float | EL NED deg |
| 59 | 59 | 60 | 1 | TRC STATUS BITS | uint8 | 0:isReady; 1:isConnected; 2:isStarted; 3–7:RES |
| **60** | **60** | **124** | **64** | **TRC REGISTER** | **TRC_REG** | **64-byte block (opaque — see full ICD)** |
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
| 164 | 164 | 165 | 1 | BDC VOTE BITS1 | uint8 | 0:isHorizVoteOverride; 1:isKIZVoteOverride; 2:isLCHVoteOverride; 3:isBDAVoteOverride; 4:isBelowHoriz; 5:isInKIZ; 6:isInLCH; 7:RES |
| 165 | 165 | 166 | 1 | BDC VOTE BITS2 | uint8 | 0:BelowHorizVote; 1:InKIZVote; 2:InLCHVote; 3:BDCVote; 4:RES; 5:isHorizonLoaded; 6:RES; 7:isFSMLimited |
| 166 | 166 | 167 | 1 | MCC VOTE BITS RB | uint8 | 0:isLaserTotalHW; 1:isNotAbort; 2:isArmed; 3:isBDA; 4:isEMON; 5:isLaserFireRequested; 6:isLaserTotal; 7:isCombat |
| 167 | 167 | 168 | 1 | BDC VOTE BITS KIZ | uint8 | 0:isLoaded; 1:isEnabled; 2:isTimeValid; 3:isOperatorValid; 4:isPositionValid; 5:isForExec; 6:isInKIZ; 7:InKIZVote |
| 168 | 168 | 169 | 1 | BDC VOTE BITS LCH | uint8 | 0:isLoaded; 1:isEnabled; 2:isTimeValid; 3:isOperatorValid; 4:isPositionValid; 5:isForExec; 6:isInLCH; 7:InLCHVote |
| **169** | **169** | **233** | **64** | **FMC REGISTER** | **FMC_REG** | **64-byte block (opaque — see full ICD)** |
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
| 363 | 363 | 379 | 16 | FSM NED AZ/EL (RB + cmd) | float[4] | from readback (noisy) + from command |
| 379 | 379 | 383 | 4 | HORIZON_BUFFER | float | |
| 383 | 383 | 387 | 4 | BDC VERSION WORD | uint32 | VERSION_PACK(major, minor, patch) |
| 387 | 387 | 391 | 4 | MCU Temp | float | °C |
| 391 | 391 | 392 | 1 | TIME_BITS | uint8 | bit0:isPTP_Enabled; bit1:ptp.isSynched; bit2:usingPTP; bit3:ntp.isSynched; bit4:ntpUsingFallback; bit5:ntpHasFallback; bit6–7:RES |
| 392 | 392 | 512 | 120 | RESERVED | — | 0x00 |

**Defined: 392 bytes. Reserved: 120 bytes. Fixed block: 512 bytes.**

> **Time source decode (HMI — session 32):** Read `TIME_BITS` at byte 391.
> `TIME_BITS[2]=1` → PTP active (GNSS time). `TIME_BITS[2]=0` + `TIME_BITS[3]=1` → NTP serving.
> `TIME_BITS[4]=1` → NTP on fallback server. All zeros → no time source.

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

### VERSION_PACK Encoding
```
uint32 bits[31:24] = major
uint32 bits[23:12] = minor
uint32 bits[11:0]  = patch
```
Current firmware versions (session 36): MCC = `VERSION_PACK(3,2,0)` = `0x03002000`. BDC = `VERSION_PACK(3,2,0)` = `0x03002000`. TMC = `VERSION_PACK(3,2,0)` = `0x03002000`. FMC = `VERSION_PACK(3,2,0)` = `0x03002000`. TRC = `VERSION_PACK(3,0,1)` = `0x03000001`.

---

## Action Items

| ID | Item | Owner | Priority |
|----|------|-------|----------|
| ~~NEW-37~~ | `MSG_MCC.cs` PTP bits + ENG GUI display | ~~C# / HMI dev~~ | ✅ Closed session 28/29 |
| ~~FW-1~~ | `PTPDEBUG <0-3>` serial command | ✅ Closed session 30 |
| ~~NEW-38a~~ | TMC PTP integration | ✅ Closed session 30/31 |
| NEW-38b | BDC PTP integration | ⏳ Next |
| NEW-38c | FMC PTP integration | ⏳ Pending |
| NEW-38d | TRC PTP integration | ⏳ Pending |

---

## Framing Reference

> Full protocol specification: **ARCHITECTURE.md §6**

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

THEIA strips the 9-byte frame header and 2-byte CRC before passing the 512-byte payload to `MSG_MCC.ParseA3()` or `MSG_BDC.ParseA3()`.

### STATUS Byte Codes (byte 0 of response payload for commands other than 0xA1)

| Value | Name | Meaning |
|-------|------|---------|
| `0x00` | `STATUS_OK` | Command accepted and executed |
| `0x01` | `STATUS_CMD_REJECTED` | CMD_BYTE not in `EXT_CMDS[]` whitelist |
| `0x02` | `STATUS_BAD_MAGIC` | Magic bytes incorrect |
| `0x03` | `STATUS_BAD_CRC` | CRC check failed |
| `0x04` | `STATUS_BAD_LEN` | `PAYLOAD_LEN` does not match expected |
| `0x05` | `STATUS_SEQ_REPLAY` | SEQ_NUM within replay-rejection window |
| `0x06` | `STATUS_NO_DATA` | Register not yet populated (device not ready) |

### 0xA4 — EXT_FRAME_PING Response Payload

| Bytes | Field | Value |
|-------|-------|-------|
| 0 | `protocol_version` | `0x01` |
| 1–2 | `echo_seq` | uint16 — echoes request SEQ_NUM |
| 3–6 | `uptime_ms` | uint32 — server uptime in milliseconds |
| 7–511 | reserved | `0x00` |

---

## Network Reference

| Node | IP | A3 Port | UDP:10009 |
|------|----|---------|-----------|
| MCC | 192.168.1.10 | 10050 | — |
| BDC | 192.168.1.20 | 10050 | — |
| THEIA | 192.168.1.208 | — | Receives CUE inbound; sends status response to sender |

External integration clients: 192.168.1.200–.254 by convention.

> Sub-controllers (TMC .12, TRC .22, FMC .23) are not addressable via A3.
> For CUE inbound packet format and integration requirements, see `CROSSBOW_ICD_EXT_OPS`.

---

## Video Stream

TRC (Jetson Orin NX, 192.168.1.22) encodes and streams a single H.264 RTP video stream to the operator PC (THEIA, 192.168.1.208). The stream carries the compositor output — VIS standalone, MWIR standalone, or PIP composite — as selected by `0xD0 SET_ACTIVE_CAMERA` and `0xDE SET_VIEW_MODE`. There is **one stream** regardless of view mode.

### Stream Parameters

| Parameter | Value | Notes |
|-----------|-------|-------|
| Transport | UDP RTP unicast (default) | Multicast pending — see `0xD1` |
| Destination (unicast) | 192.168.1.208 : 5000 | THEIA operator PC |
| Port | **5000** (UDP, fixed) | |
| Protocol | RTP — payload type 96, encoding H264 | |
| Codec | H.264 hardware encoded | Jetson `nvv4l2h264enc` |
| Resolution | **1280 × 720** (fixed) | Must be configured explicitly — auto-detect produces invalid frames |
| Framerate | **60 fps** (fixed) | 30 fps option pending — see `0xD2` |
| Bitrate | **10 Mbps** (fixed) | |
| E2E latency (HW decode) | 30–80 ms | NVIDIA `nvh264dec` |
| E2E latency (SW decode) | 50–100 ms | Software `avdec_h264` |

### Receive Requirements

| Requirement | Value | Notes |
|-------------|-------|-------|
| Decoder (recommended) | `nvh264dec` | NVIDIA hardware H.264 decode — GTX 900 series or newer, driver ≥452.39 |
| Decoder (fallback) | `avdec_h264` | Software decode. CPU load ~25–35% at 60 fps, ~10–15% at 30 fps |
| UDP receive buffer | 2 MB minimum | Default OS buffer insufficient at 60 fps |
| Jitter buffer | 50 ms, `drop-on-latency=true` | Absorbs Jetson encoder timing jitter |
| Resolution | Explicit 1280×720 | Auto-detect produces invalid frames |
| PixelShift correction | −420 px horizontal | Fixed alignment artefact of Jetson encoder — must be applied in receiver |

### Framerate Control — `0xD2 ORIN_SET_STREAM_60FPS` (Pending)

`0xD2` is defined in this ICD but the firmware handler is not yet implemented.

| Payload | Effect |
|---------|--------|
| `{0x01}` | 60 fps (default, current) |
| `{0x00}` | 30 fps — recommended for software-decode receivers |

### Multicast — `0xD1 ORIN_SET_STREAM_MULTICAST` (Pending)

`0xD1` is defined in this ICD but the firmware handler is not yet implemented.

| Parameter | Value |
|-----------|-------|
| Multicast group | `239.127.1.21` (site-local, CROSSBOW reserved) |
| Port | 5000 (unchanged) |
| Requirement | Receiver NIC on 192.168.1.x subnet; multicast routing enabled on switch |
| Benefit | Multiple simultaneous receivers without duplicate unicast streams from TRC |

---



The bidirectional THEIA↔integrator interface on UDP:10009 uses a lightweight framing protocol
consistent with the A3 internal port. All three message types (inbound CUE, status response,
POS/ATT report) use this frame structure.

### Frame Structure

```
Byte  0    : Magic HI  = 0xCB
Byte  1    : Magic LO  = 0x48
Byte  2    : CMD_BYTE
Bytes 3–4  : SEQ_NUM   uint16 LE
Bytes 5–6  : PAYLOAD_LEN uint16 LE (bytes of payload only, not including header or CRC)
Bytes 7–(7+PAYLOAD_LEN-1) : PAYLOAD
Bytes (7+PAYLOAD_LEN)–(8+PAYLOAD_LEN) : CRC16 uint16 LE
```

CRC covers bytes 0 through end of PAYLOAD (i.e. everything except the CRC itself).
CRC algorithm: CRC-16/CCITT, poly=0x1021, init=0xFFFF. See ARCHITECTURE.md §6 for
cross-platform verification note.

### CMD_BYTE Assignments

| CMD | Direction | Description | Payload | Total Frame |
|-----|-----------|-------------|---------|-------------|
| `0xAA` | Integrator → THEIA | CUE packet (inbound track data) | 62 bytes | 71 bytes |
| `0xAF` | THEIA → Integrator | Status response (system + fire control state) | 30 bytes | 39 bytes |
| `0xAB` | THEIA → Integrator | POS/ATT report (platform position/attitude) | 32 bytes | 41 bytes |

THEIA sends the status response (`0xAF`) to the source IP of the most recently received CUE
packet. THEIA sends the POS/ATT report (`0xAB`) on request via Track CMD = `3` (REPORT POS/ATT)
in the inbound CUE packet, or continuously at 10 Hz when Track CMD = `254` (REPORT CONTINUOUS ON).

---

## THEIA Status Response — CMD `0xAF`

THEIA transmits this message to the CUE source in response to each received CUE packet (or
continuously at 10 Hz if requested). It provides full engagement state including fire control
votes, gimbal LOS, and laser LOS.

**Payload: 30 bytes. Total framed: 39 bytes.**

| Payload Offset | Size | Type | Field | Notes |
|----------------|------|------|-------|-------|
| 0 | 1 | byte | System State | SYSTEM_STATES enum — see below |
| 1 | 1 | byte | System Mode | BDC_MODES enum — see below |
| 2 | 1 | byte | Active CAM ID | VIS=0, MWIR=1 |
| 3 | 1 | byte | MCC VOTE BITS | Fire control vote bits — see bit table |
| 4 | 1 | byte | BDC VOTE BITS1 | Raw geometry bits — see bit table |
| 5 | 1 | byte | BDC VOTE BITS2 | Computed geometry votes with override logic — see bit table |
| 6 | 4 | float | Gimbal Az NED | Gimbal LOS azimuth, degrees NED — incorporates platform attitude |
| 10 | 4 | float | Gimbal EL NED | Gimbal LOS elevation, degrees NED — incorporates platform attitude |
| 14 | 4 | float | Laser Az NED | Laser LOS azimuth, degrees NED — gimbal LOS + FSM offset |
| 18 | 4 | float | Laser EL NED | Laser LOS elevation, degrees NED — gimbal LOS + FSM offset |
| 22 | 4 | uint32 | RESERVED | 0x00 |
| 26 | 4 | uint32 | RESERVED | 0x00 |

### SYSTEM_STATES Enum
| Value | Name |
|-------|------|
| 0x00 | OFF |
| 0x01 | STNDBY |
| 0x02 | ISR |
| 0x03 | COMBAT |
| 0x04 | MAINT |
| 0x05 | FAULT |

### BDC_MODES Enum
| Value | Name |
|-------|------|
| 0x00 | OFF |
| 0x01 | POS |
| 0x02 | RATE |
| 0x03 | CUE |
| 0x04 | ATRACK |
| 0x05 | FTRACK |

### MCC VOTE BITS (byte [3])
| Bit | Name | Notes |
|-----|------|-------|
| 0 | isLaserTotalHW_Vote_rb | |
| 1 | isNotAbort_Vote_rb | **Inverted** — 0 = abort ACTIVE |
| 2 | isArmed_Vote_rb | |
| 3 | isBDA_Vote_rb | |
| 4 | isEMON_rb | |
| 5 | isLaserFireRequested_Vote | |
| 6 | isLaserTotal_Vote_rb | All MCC votes pass |
| 7 | isCombat_Vote_rb | |

### BDC VOTE BITS1 (byte [4]) — raw geometry
| Bit | Name |
|-----|------|
| 0 | isHorizVoteOverride |
| 1 | isKIZVoteOverride |
| 2 | isLCHVoteOverride |
| 3 | isBDAVoteOverride |
| 4 | isBelowHoriz |
| 5 | isInKIZ |
| 6 | isInLCH |
| 7 | RES |

### BDC VOTE BITS2 (byte [5]) — computed votes with override logic
| Bit | Name | Logic |
|-----|------|-------|
| 0 | BelowHorizVote | isHorizVoteOverride ? true : isBelowHoriz |
| 1 | InKIZVote | isKIZVoteOverride ? true : isInKIZ |
| 2 | InLCHVote | isLCHVoteOverride ? true : isInLCH |
| 3 | BDCVote | isBDAVoteOverride ? true : (BelowHorizVote & InKIZVote & InLCHVote) |
| 4 | RES | |
| 5 | isHorizonLoaded | |
| 6 | RES | |
| 7 | `isFSMLimited` | |

> For a clean fire: `BDCVote` (BITS2 bit 3) = 1 and `isLaserTotal_Vote_rb` (MCC VOTE BITS bit 6) = 1.

### C Struct

```c
#pragma pack(push, 1)
typedef struct {
    uint8_t  system_state;     // payload [0]: SYSTEM_STATES
    uint8_t  system_mode;      // payload [1]: BDC_MODES
    uint8_t  active_cam_id;    // payload [2]: 0=VIS, 1=MWIR
    uint8_t  mcc_vote_bits;    // payload [3]: fire control votes
    uint8_t  bdc_vote_bits1;   // payload [4]: raw geometry
    uint8_t  bdc_vote_bits2;   // payload [5]: computed votes
    float    gimbal_az_ned;    // payload [6–9]: degrees
    float    gimbal_el_ned;    // payload [10–13]: degrees
    float    laser_az_ned;     // payload [14–17]: degrees
    float    laser_el_ned;     // payload [18–21]: degrees
    uint32_t reserved[2];      // payload [22–29]
} TheiaStatusPayload_t;        // 30 bytes
#pragma pack(pop)
```

---

## THEIA POS/ATT Report — CMD `0xAB`

Sent by THEIA on request (Track CMD=3) or continuously at 10 Hz (Track CMD=254).
Reports current platform geodetic position and attitude as known to THEIA.

**Payload: 32 bytes. Total framed: 41 bytes.**

| Payload Offset | Size | Type | Field | Notes |
|----------------|------|------|-------|-------|
| 0 | 8 | double | Latitude | WGS-84 decimal degrees |
| 8 | 8 | double | Longitude | WGS-84 decimal degrees |
| 16 | 4 | float | Altitude HAE | Height Above Ellipsoid, metres |
| 20 | 4 | float | Roll | Degrees NED |
| 24 | 4 | float | Pitch | Degrees NED |
| 28 | 4 | float | Yaw | Degrees NED |

### C Struct

```c
#pragma pack(push, 1)
typedef struct {
    double   latitude;    // payload [0–7]: WGS-84 degrees
    double   longitude;   // payload [8–15]: WGS-84 degrees
    float    altitude;    // payload [16–19]: metres HAE
    float    roll;        // payload [20–23]: degrees NED
    float    pitch;       // payload [24–27]: degrees NED
    float    yaw;         // payload [28–31]: degrees NED
} TheiaPosAttPayload_t;   // 32 bytes
#pragma pack(pop)
```
