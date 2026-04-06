# HYPERION Operator User Guide

**Version:** 1.2.0
**Date:** 2026-04-05
**Audience:** HYPERION sensor operators, system integrators
**ICD Reference:** `CROSSBOW_ICD_EXT_OPS` (IPGD-0005) — CUE inbound packet format, response messages, integration requirements

---

## 1. Overview

HYPERION is the sensor fusion and cueing system for CROSSBOW. It aggregates tracks from
multiple sensors (ADS-B, Echodyne radar, LoRa/MAVLink, RADAR, Stellarium), applies Kalman
filtering, and sends 71-byte CUE packets to THEIA over UDP:15009.

THEIA responds with a 39-byte status frame after each CUE packet, reporting system state,
gimbal LOS, laser LOS, and fire control vote bits. HYPERION uses this feedback to determine
whether the weapon system is ready to engage and to confirm the gimbal is tracking the
commanded target.

### Sensor Inputs

| Sensor | Protocol | Transport | Port | IP | Role |
|--------|----------|-----------|------|----|------|
| ADS-B (RPI) | Mode S 1090ES hex | TCP | 30002 | 192.168.1.31 | Air track ID, lat/lon/alt, velocity |
| Echodyne radar | Echodyne binary | TCP | 29982 | 192.168.1.34 | High-update-rate track, range/bearing |
| LoRa/MAVLink | EXT_OPS framed UDP | UDP | 15002 | any | Cooperative target track |
| RADAR / EXT | EXT_OPS framed UDP | UDP | 15001 | any | Generic sensor / CUE SIM injection |
| Stellarium | JSON REST | HTTP | 8090 | localhost | Celestial az/el reference — feeds trackLogs["STELLA"] via synthetic LLA |

> **Port 15001 (aRADAR) and 15002 (aLORA)** are HYPERION's dedicated sensor input ports in the 15000 EXT_OPS block. These are distinct from the CUE output port (15009) — no conflict on single-machine deployments.

---

## 2. CUE Output Protocol

HYPERION sends CUE packets to THEIA at UDP:15009. All packets use the EXT_OPS frame:

```
[0xCB][0x48][CMD][SEQ_NUM 2B LE][PAYLOAD_LEN 2B LE][PAYLOAD N bytes][CRC16 2B LE]
```

For the CUE inbound packet (CMD=`0xAA`): payload = 62 bytes, total frame = 71 bytes.

CRC algorithm: CRC-16/CCITT, poly=0x1021, init=0xFFFF, over all bytes before the CRC.

### CUE Packet Key Fields

| Field | Payload Offset | Notes |
|-------|----------------|-------|
| ms Time Stamp | [0–7] | int64 — ms since Unix epoch, from Kalman filter state |
| Track ID | [8–15] | ICAO or assigned ID, ASCII null-padded to 8 bytes |
| Track Class | [16] | 8=UAV, 10=AC_LIGHT, etc. — from sensor classification |
| Track CMD | [17] | 1=TRACK (normal), 0=DROP, 4=WEAPON HOLD, 5=WEAPON FREE TO FIRE |
| Latitude / Longitude | [18–33] | WGS-84 double LE — Kalman-filtered best estimate |
| Altitude HAE | [34–37] | Height Above Ellipsoid, metres — do NOT use MSL |
| Heading / Speed | [38–41] / [42–45] | True heading degrees (0–360), ground speed m/s — HYPERION converts to NED vx/vy internally for Kalman. THEIA uses heading for AC display overlay. |
| Vz | [46–49] | Vertical speed m/s — used by Kalman elevation prediction |

Full field definitions: `CROSSBOW_ICD_EXT_OPS` (IPGD-0005) §CUE Inbound Packet.

### Track CMD Usage

| Scenario | Track CMD | Value |
|----------|-----------|-------|
| Normal track update | TRACK | 1 |
| Target lost / engagement over | DROP | 0 |
| Operator requests platform position | REPORT POS/ATT | 3 |
| Enable continuous feedback | REPORT CONTINUOUS ON | 254 |
| Inhibit weapon release | WEAPON HOLD | 4 |
| Release weapon hold (votes govern firing) | WEAPON FREE TO FIRE | 5 |

> **WEAPON FREE TO FIRE** releases HYPERION's software hold on THEIA. All hardware and
> geometry interlocks remain active — the weapon cannot fire unless BDCVote and
> isLaserTotal_Vote_rb are both set in the `0xAF` status response.

---

## 3. THEIA Status Response

THEIA sends a 39-byte status frame (CMD=`0xAF`) to HYPERION's source IP and port after
every valid TRACK packet, and at 10 Hz if REPORT CONTINUOUS ON has been sent.

Full layout: `ICD_EXTERNAL_OPS §THEIA Status Response — CMD 0xAF`.

### Key Fields to Monitor

| Field | Payload Offset | Meaning |
|-------|----------------|---------|
| System State | [0] | 0x02=ISR, 0x03=COMBAT — must be ISR or COMBAT for engagement |
| System Mode | [1] | 0x03=CUE, 0x04=ATRACK — confirms THEIA is tracking |
| MCC VOTE BITS | [3] | See bit table below |
| BDC VOTE BITS2 | [5] | See bit table below — bit 3 (BDCVote) is the master geometry vote |
| Gimbal Az NED | [6–9] | Current gimbal LOS azimuth degrees — compare to commanded |
| Gimbal EL NED | [10–13] | Current gimbal LOS elevation degrees |
| Laser Az NED | [14–17] | Laser LOS = gimbal + FSM offset |
| Laser EL NED | [18–21] | Laser LOS elevation |

### MCC VOTE BITS (status response byte [3])

| Bit | Name | For engagement |
|-----|------|----------------|
| 1 | isNotAbort_Vote_rb | Must be **1** (inverted — 0 = abort active) |
| 2 | isArmed_Vote_rb | Must be 1 |
| 3 | isBDA_Vote_rb | Must be 1 (LOS clear) |
| 6 | isLaserTotal_Vote_rb | **Master MCC vote** — must be 1 |
| 7 | isCombat_Vote_rb | Must be 1 (system in COMBAT) |

### BDC VOTE BITS2 (status response byte [5])

| Bit | Name | For engagement |
|-----|------|----------------|
| 0 | BelowHorizVote | Must be 1 (below horizon limit) |
| 1 | InKIZVote | Must be 1 (within KIZ) |
| 2 | InLCHVote | Must be 1 (within LCH) |
| 3 | BDCVote | **Master BDC vote** — must be 1 |
| 5 | isHorizonLoaded | Should be 1 before engagement |
| 6 | isKIZLoaded | Should be 1 before engagement |
| 7 | isLCHLoaded | Should be 1 before engagement |

> **Clean fire condition:** BDCVote (BITS2 bit 3) = 1 **AND** isLaserTotal_Vote_rb (MCC bit 6) = 1.

---

## 4. THEIA POS/ATT Report

THEIA sends a 41-byte POS/ATT frame (CMD=`0xAB`) on request. Send Track CMD = `3`
(REPORT POS/ATT) to request one report, or Track CMD = `254` (REPORT CONTINUOUS ON)
for continuous 10 Hz delivery.

Full layout: `ICD_EXTERNAL_OPS §THEIA POS/ATT Report — CMD 0xAB`.

| Payload Offset | Field | Notes |
|----------------|-------|-------|
| [0–7] | Latitude | WGS-84 degrees |
| [8–15] | Longitude | WGS-84 degrees |
| [16–19] | Altitude HAE | Metres — same datum as CUE inbound |
| [20–23] | Roll | Degrees NED |
| [24–27] | Pitch | Degrees NED |
| [28–31] | Yaw | Degrees NED |

HYPERION uses the POS/ATT report to verify THEIA's current platform state matches what
was set via `0xBA SET_SYS_LLA` and `0xBB SET_SYS_ATT`, and to cross-check NED→gimbal
rotation accuracy.

---

## 5. Sensor Fusion Architecture

### Kalman Filter

HYPERION maintains a single Kalman-filtered state per active track:

- **State vector:** NED position and velocity `[N, E, D, vN, vE, vD]` — position derived from LLA→NED conversion, velocity derived from heading+speed decomposition
- **Prediction step:** dead-reckoning at native filter rate
- **Update step:** sensor measurement fusion weighted by source covariance
- **Output:** best-estimate position/velocity sent in each CUE packet

The timestamp in the CUE packet (`ms Time Stamp`) reflects the Kalman filter's state
time — not the packet send time. THEIA uses this for latency compensation.

### Sensor Priority and Fusion

| Sensor | Position accuracy | Update rate | Notes |
|--------|------------------|-------------|-------|
| ADS-B | Low (GPS-reported) | ~1 Hz | Used for initial track ID and classification |
| Echodyne | High (radar range) | 10–50 Hz | Primary track source when in range |
| RADAR | Medium | 1–5 Hz | Long-range initial detect |
| LoRa/MAVLink | GPS-reported | Variable | Cooperative targets — high confidence classification |
| Stellarium | az/el only | ~10 Hz | Celestial reference — synthetic LLA via ned2lla at configurable range |

When multiple sensors track the same target (matched by Track ID or proximity), HYPERION
fuses measurements. A sensor with updated track data takes priority over a stale prediction.

### Track Handoff

When THEIA reports System Mode = `0x04` (ATRACK) in the `0xAF` status response, the
video tracker has acquired the target. At this point HYPERION should:

1. Reduce CUE rate to 1–5 Hz (or stop sending TRACK commands)
2. Continue monitoring the `0xAF` status response
3. Resume full-rate CUE if ATRACK is lost (System Mode reverts to CUE)

---

## 6. Engagement Sequence

### Step 1 — Track Acquisition
HYPERION detects a target via one or more sensors. Kalman filter initialised with first
position fix. Track Class assigned from sensor classification (UAV=8 typical).

### Step 2 — Session Establishment
Send Track CMD = `254` (REPORT CONTINUOUS ON) to begin 10 Hz `0xAF` status stream from
THEIA. Verify THEIA responds with System State ≥ `0x02` (ISR or COMBAT).

### Step 3 — CUE Tracking
Send Track CMD = `1` (TRACK) at sensor fusion rate (10–100 Hz). Verify:
- `0xAF` System Mode = `0x03` (CUE) — THEIA is in CUE mode
- Gimbal Az/EL NED in `0xAF` converging toward commanded az/el

### Step 4 — Monitor Engagement Zone
Parse BDC VOTE BITS2 byte [5] in `0xAF`:
- `isHorizonLoaded` (bit 5), `isKIZLoaded` (bit 6), `isLCHLoaded` (bit 7) — zone data loaded
- `BelowHorizVote` (bit 0), `InKIZVote` (bit 1), `InLCHVote` (bit 2) — geometry passing
- `BDCVote` (bit 3) — master geometry vote passing

### Step 5 — Confirm Fire Readiness
All of the following must be true in `0xAF`:
- MCC VOTE BITS bit 1 (`isNotAbort`) = 1
- MCC VOTE BITS bit 6 (`isLaserTotal_Vote_rb`) = 1
- BDC VOTE BITS2 bit 3 (`BDCVote`) = 1
- System State = `0x03` (COMBAT)
- System Mode = `0x03` (CUE) or `0x04` (ATRACK)

### Step 6 — Issue WEAPON FREE TO FIRE
Send Track CMD = `5` (WEAPON FREE TO FIRE). Continue sending TRACK at normal rate.
The weapon system engages based on internal fire control logic — HYPERION does not
control the laser directly.

### Step 7 — Disengage
Send Track CMD = `0` (DROP) to release the track. THEIA exits CUE mode.
Send Track CMD = `255` (REPORT CONTINUOUS OFF) to stop status stream if no longer needed.

---

## 7. Operational Notes

**Altitude:** Always provide HAE altitude in CUE packets. THEIA does not apply geoid
correction. Mixing HAE and MSL values will cause pointing errors proportional to the
local geoid height (typically 10–50 metres).

**Track ID stability:** Use a consistent Track ID for the same physical target across
the engagement. THEIA associates state to Track ID — changing ID mid-engagement resets
the track state.

**CUE rate:** Send at the Kalman filter output rate. Do not throttle below 10 Hz during
active TRACK — THEIA's track timeout will drop the track.

**Gimbal vs laser LOS:** The `0xAF` status response provides both gimbal LOS and laser
LOS (= gimbal + FSM offset). For fire control geometry assessment, use the laser LOS
angles — these represent where the laser beam is actually pointing.

**Vote bit timing:** There is latency between HYPERION sending WEAPON FREE TO FIRE and
all vote bits passing. Confirm `isLaserTotal_Vote_rb` = 1 in the `0xAF` response before
concluding the system has fired.

---

## 8. Network Reference

| Node | IP | Port | Protocol | Direction |
|------|----|------|----------|-----------|
| THEIA | 192.168.1.208 (default) | 15009 | UDP | HYPERION → THEIA (CUE output); THEIA → HYPERION (status response) |
| HYPERION | 192.168.1.206 (default) | 15001 | UDP | aRADAR sensor input / CUE SIM injection |
| HYPERION | 192.168.1.206 (default) | 15002 | UDP | aLORA LoRa/MAVLink sensor input |
| ADS-B decoder | 192.168.1.31 | 30002 | TCP | HYPERION ← dump1090 |
| Echodyne radar | 192.168.1.34 | 29982 | TCP | HYPERION ← Echodyne |
| LoRa gateway | 192.168.1.32 | — | — | HYPERION ← LoRa relay |

> **IP assignment note:** THEIA and HYPERION operate in the `192.168.1.200–.254` external range. The addresses shown are IPG reference deployment defaults — both are operator-configurable. The constraint is that they remain in the `.200–.254` range so embedded controllers accept their A3 packets. IPG reserves `.200–.209`; third-party integrators use `.210–.254` by convention.

HYPERION IP: 192.168.1.206 by default — operator-configurable within `.200–.254`.

---

## 9. Client and Port Reference

Complete reference for all HYPERION inbound sensor clients, outbound CUE path, and response relay. Single source of truth for port assignments, protocols, and class mappings.

### 9.1 Inbound Sensor Clients

| Instance | Class | Protocol | Transport | Port | IP | Track Key | `vzPositiveUp` | Notes |
|----------|-------|----------|-----------|------|----|-----------|----------------|-------|
| `aADSB` | `ADSB2` | Mode S 1090ES hex | TCP | 30002 | 192.168.1.31 | ICAO 24-bit hex (6 chars) | — | CPR decode, TC 1–22 |
| `aECHO` | `ECHO` | Echodyne binary | TCP | 29982 | 192.168.1.34 | `ECH_<last4 UUID hex>` | — | ECEF→LLA, UUID track ID |
| `aRADAR` | `RADAR` | EXT_OPS framed UDP | UDP | 15001 | any | `"RADAR"` prefix | `true` | Generic sensor / CUE SIM injection |
| `aLORA` | `RADAR` | EXT_OPS framed UDP | UDP | 15002 | any | `"LORA"` prefix | `false` | LoRa/MAVLink — vz sign corrected NED→ENU |
| `aStella` | `STELLARIUM` | JSON REST | HTTP | 8090 | localhost | `"STELLA"` | — | az/el → synthetic LLA via `ned2lla()` at `StellariumRange` |

**`vzPositiveUp` convention:**
- `true` (aRADAR) — received `vz` positive means ascending (ENU). Used as-is.
- `false` (aLORA) — received `vz` positive means descending (MAVLink NED). Sign is inverted before Kalman update.

**Stellarium synthetic range** — astronomical distances from Stellarium are discarded. `StellariumRange` constants control the projection distance:

| Constant | Value | Use case |
|----------|-------|----------|
| `NEAR_KM` | 10 km | Default — general pointing reference |
| `LEO_KM` | 400 km | ISS / low Earth orbit objects |
| `GEO_KM` | 35,786 km | Geosynchronous satellites |
| `MOON_KM` | 384,400 km | Lunar tracking |

---

### 9.2 Outbound CUE Path

| Step | From | To | Port | Format | Notes |
|------|------|----|------|--------|-------|
| Operator selects track | HYPERION DataGridView | `CurrentCUE` | — | — | Any trackLogs entry eligible |
| CUE output | HYPERION | THEIA | 15009 | EXT_OPS framed 71B CMD `0xAA` | `BuildCueFrame()` — heading+speed v3.0.2 |

HYPERION sends to the IP configured in `txt_CB_HMI` (default `192.168.1.208`), port 15009. Rate determined by `timUDP` interval.

---

### 9.3 Response Relay Path

THEIA replies to HYPERION's source port with `0xAF` status responses. HYPERION relays these verbatim to the originating vendor (e.g. CUE SIM) that last sent a packet to HYPERION's aRADAR port.

| Step | From | To | Port | Format | Notes |
|------|------|----|------|--------|-------|
| Status response | THEIA | HYPERION source port | ephemeral | EXT_OPS framed 39B CMD `0xAF` | Sent after every valid TRACK |
| Relay | HYPERION | `LastSenderEndPoint` | ephemeral | Verbatim `0xAF` / `0xAB` | `RelayReceiveLoop()` forwards to last aRADAR sender |

---

### 9.4 Port Summary

| Port | Block | Owner | Direction | Purpose |
|------|-------|-------|-----------|---------|
| 15001 | EXT_OPS | HYPERION aRADAR | Inbound | Generic sensor input / CUE SIM injection |
| 15002 | EXT_OPS | HYPERION aLORA | Inbound | LoRa/MAVLink sensor input |
| 15009 | EXT_OPS | THEIA CueReceiver | Outbound (HYPERION TX) | HYPERION → THEIA CUE output |
| 8090 | — | Stellarium HTTP | Outbound (HYPERION polls) | Stellarium az/el JSON REST |
| 29982 | — | Echodyne | Inbound | Echodyne radar binary |
| 30002 | — | ADS-B dump1090 | Inbound | Mode S 1090ES hex |

> **Single-machine deployment:** All 15000-block ports are distinct — no conflicts between aRADAR (15001), aLORA (15002), and THEIA CueReceiver (15009) when running on the same machine.

---

## 10. Related Documents

| Document | Doc # | Content |
|----------|-------|---------|
| `CROSSBOW_ICD_EXT_OPS` | IPGD-0005 | EXT_OPS interface — CUE inbound packet, response messages (`0xAF`, `0xAB`), integration checklist, C++/C# code reference |
| `ARCHITECTURE.md` | IPGD-0006 | HYPERION sensor fusion architecture detail (§7.4), full network port reference (§5), EXT_OPS CRC specification (§6) |
