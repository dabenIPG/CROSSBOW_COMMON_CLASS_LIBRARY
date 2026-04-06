# CROSSBOW — EXT_OPS Interface Control Document

**Document:** `CROSSBOW_ICD_EXT_OPS`
**Doc #:** IPGD-0005
**Version:** 3.3.0
**Date:** 2026-04-05 (session 37)
**Classification:** USER-FACING — may be distributed to Tier 2 external integrators
**Scope:** `EXT_OPS` — external integrator interface to CROSSBOW cueing via THEIA or HYPERION
**Audience:** Third-party CUE providers, sensor fusion integrators, HYPERION developers

---

## Version History

**v3.3.0 changes (session 37 — 2026-04-05):**
- EXT_OPS port migrated: `UDP:10009` → `UDP:15009` throughout. Rationale: 15000 block
  isolates all EXT_OPS traffic (THEIA CueReceiver, HYPERION CUE output, HYPERION sensor
  inputs) from A2/A3 internal ports, eliminating single-machine port conflicts.
- HYPERION sensor input ports added to Network Reference: `15001` (aRADAR), `15002` (aLORA).
- IP assignment note added — THEIA `.208` and HYPERION `.206` are IPG reference defaults,
  operator-configurable within the `.200–.254` external range.
- Appendix B C# example updated: `port = 10009` → `port = 15009`.

**v3.2.0 changes (session 27 — 2026-03-26):**
- Version bump for ICD family consistency — no command or register changes in this document
- `0xA2 SET_NTP_CONFIG` is INT_ENG only and does not appear on the A3 EXT whitelist; no impact to Tier 2 integrators

**v3.1.0 changes (session 22 — 2026-03-16):**
- Document renamed from `ICD_EXTERNAL_INT` to `ICD_EXTERNAL_OPS` — scope label alignment
- Scope expanded: THEIA and HYPERION as current targets; direct BDC interface noted as future work
- Interface overview revised — THEIA/HYPERION presented as integration boundary; internal nodes (MCC, TRC) removed from external view
- Network and Interface Tier Overview section added — A1/A2/A3 model, integration boundary clarification
- Network Reference section added — updated IP table (.208 THEIA, .206 HYPERION, .210–.254 third-party)
- THEIA IP updated throughout: 192.168.1.8 → 192.168.1.208
- Integration Path Guidance section added — direct vs HYPERION C2 path, HYPERION as preferred intermediary
- HYPERION relay requirement documented — verbatim `0xAF`/`0xAB` forwarding to originating VENDOR
- Response routing section added — direct path vs HYPERION relay path
- Future Work section added — direct BDC, auth/service, multi-THEIA, video multicast, 30 fps
- Code reference moved to Appendix A (C/C++) and Appendix B (C#)
- Internal implementation references removed throughout (BDC mode transitions, internal commands)
- MCC VOTE BITS bit 0 corrected: `isLaserEnabled_Vote` → `isLaserTotalHW_Vote_rb`
- Fire clear note cleaned — internal field names replaced with bit position references

**v3.0.2 changes (session 17 — 2026-03-15):**
- CUE inbound velocity fields changed: `Vx NED` / `Vy NED` → `Heading` / `Speed`.
  `Vz NED` retained. Frame size unchanged (12 bytes, same offsets).
  Rationale: heading+speed is the natural output of all sensor types (ADS-B, RADAR,
  LoRa/MAVLink). HYPERION converts to NED components internally for Kalman filter.
  THEIA uses heading for AC display overlay; ignores speed and vz for pointing.

---

> **Document scope:** This document defines the EXT_OPS interface — the external integrator
> boundary for CROSSBOW cueing. It covers inbound CUE packet format, receive and response
> protocol, response message layouts (`0xAF`, `0xAB`), and integration requirements.
> This document is self-contained for Tier 2 integrators.

---

## Network and Interface Tier Overview

CROSSBOW uses a three-tier interface model. External integrators operate exclusively
at the EXT_OPS tier.

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
│  A3 — INT_OPS — Tier 1 (IPG reference HMI)             │
│  A3 port 10050                                          │
│  Full operator command set via A3                       │
│                                                         │
│   THEIA (.208)                                          │
└───────────────────────┬─────────────────────────────────┘
                        │ EXT_OPS boundary
┌───────────────────────▼─────────────────────────────────┐
│  EXT_OPS — Tier 2 (external integrators)                │
│  UDP:15009, magic 0xCB 0x48                             │
│  CUE interface only — THEIA and HYPERION as targets     │
│                                                         │
│   HYPERION (.206)   Third-party (.210–.254)             │
└─────────────────────────────────────────────────────────┘
```

| Tier | Transport | Magic | Audience |
|------|-----------|-------|----------|
| A1 — Controller Bus | Internal only | — | Controller firmware only — no external access |
| A2 — Engineering | Internal ports | — | IPG engineering via ENG GUI — planned auth/service pathway |
| A3 — INT_OPS — Tier 1 | A3 port 10050 | `0xCB 0x58` | IPG reference HMI — Tier 1 integrator |
| EXT_OPS — Tier 2 | UDP:15009 | `0xCB 0x48` | External CUE providers, sensor fusion integrators |

> **This document covers EXT_OPS (Tier 2) only.** For INT_OPS (Tier 1) A3 operator
> commands see `CROSSBOW_ICD_INT_OPS` (IPGD-0004).

---

## Interface Overview

```
CUE Source (third-party integrator)
  │
  │  UDP unicast → THEIA 192.168.1.208:15009 (default) or HYPERION 192.168.1.206:15009 (default)
  │  CMD 0xAA — CUE packet (track data, weapon commands)
  │  EXT_OPS frame: magic 0xCB 0x48, SEQ_NUM, CRC16
  │  71 bytes total
  │
  ▼
┌─────────────────────────────────┐
│  CROSSBOW (THEIA / HYPERION)    │  ← integration boundary
│  Internal processing            │
└─────────────────────────────────┘
  │
  └─ UDP reply → CUE source IP:10009
       CMD 0xAF — Status response (votes, LOS)     39 bytes
       CMD 0xAB — POS/ATT report (on request)      41 bytes
```

THEIA and HYPERION accept CUE packets from any conforming source. On receipt of a valid
framed CUE packet, the system enters cueing mode and sends a status response back to the sender.

---

## EXT_OPS Frame Protocol

All messages on UDP:15009 use the EXT_OPS frame. This section is the normative
definition of the EXT_OPS frame protocol.

```
Byte  0    : Magic HI  = 0xCB
Byte  1    : Magic LO  = 0x48
Byte  2    : CMD_BYTE
Bytes 3–4  : SEQ_NUM   uint16 LE
Bytes 5–6  : PAYLOAD_LEN uint16 LE
Bytes 7–(7+N-1) : PAYLOAD (N bytes)
Bytes (7+N)–(8+N) : CRC16 uint16 LE
```

CRC-16/CCITT, poly=0x1021, init=0xFFFF, over bytes 0 through end of PAYLOAD.
Verification: `crc16("123456789") == 0x29B1`.

---

## CUE Inbound Packet — CMD `0xAA`

Sent by any conforming integrator to THEIA or HYPERION at UDP:15009.

**Payload: 62 bytes. Total framed: 71 bytes.**

| Payload Offset | Size | Type | Field | Notes |
|----------------|------|------|-------|-------|
| 0 | 8 | int64 | ms Time Stamp | ms since Unix Epoch (1/1/1970 00:00:00) |
| 8 | 8 | uint8[8] | Track ID | 8-char track ID — ICAO padded ASCII, null-padded |
| 16 | 1 | byte | Track Class | Classification enum — see table below |
| 17 | 1 | byte | Track CMD | Command enum — see table below |
| 18 | 8 | double | Track Latitude | WGS-84 decimal degrees, North positive |
| 26 | 8 | double | Track Longitude | WGS-84 decimal degrees, East positive |
| 34 | 4 | float | Track Altitude HAE | Height Above Ellipsoid, metres |
| 38 | 4 | float | Heading | True heading, degrees (0–360, North=0) |
| 42 | 4 | float | Speed | Ground speed, m/s |
| 46 | 4 | float | Vz | Vertical speed, m/s — positive = climbing |
| 50 | 4 | uint32 | RESERVED | 0x00 |
| 54 | 4 | uint32 | RESERVED | 0x00 |
| 58 | 4 | uint32 | RESERVED | 0x00 |

### Track Class Enum (payload byte [16])

| Value | Name | Notes |
|-------|------|-------|
| 0 | None | No classification |
| 1 | RESERVED | |
| 2 | RESERVED | |
| 3 | GROUND_OBS | Ground observer |
| 4 | SAILPLANE | |
| 5 | BALLOON | |
| 6 | RESERVED | |
| 7 | RESERVED | |
| 8 | UAV | Unmanned aerial vehicle — primary CROSSBOW target class |
| 9 | SPACE | |
| 10 | AC_LIGHT | Light aircraft |
| 11 | AC_MED | Medium aircraft |
| 12 | RESERVED | |
| 13 | AC_HEAVY | Heavy aircraft |
| 14 | AC_HIGHPERF | High-performance aircraft |
| 15 | AC_ROTOR | Rotary wing |
| 16–255 | RESERVED | |

### Track CMD Enum (payload byte [17])

| Value | Name | Action |
|-------|------|--------|
| 0 | DROP | Drop current track — system exits cueing mode |
| 1 | TRACK | Initiate or update track — normal CUE pointing |
| 2 | REPORT ONCE | Send one status response (`0xAF`) immediately |
| 3 | REPORT POS/ATT | Send one POS/ATT report (`0xAB`) immediately |
| 4 | WEAPON HOLD | Suppress weapon release regardless of vote state |
| 5 | WEAPON FREE TO FIRE | Release weapon hold — votes govern firing |
| 6–253 | RESERVED | |
| 254 | REPORT CONTINUOUS ON | Begin 10 Hz status response stream to sender |
| 255 | REPORT CONTINUOUS OFF | Stop continuous status response stream |

> ⚠ **WEAPON FREE TO FIRE (5):** This command releases the software weapon hold.
> Hardware and geometry interlocks still apply — the weapon cannot fire unless all vote bits
> pass independently of this command.

### Field Notes

**Track ID ([8–15]):** ASCII, `0x00`-padded. Example: `A1B2C3` →
`0x41 0x31 0x42 0x32 0x43 0x33 0x00 0x00`.

**Track Altitude HAE ([34–37]):** Height Above Ellipsoid in metres. Do not supply MSL
altitude — the system does not apply geoid correction.

**Heading ([38–41]):** True heading in degrees, 0–360, North=0, clockwise. Used for
AC symbol orientation on the operator display and for internal velocity prediction.
Set to `0.0f` if unavailable.

**Speed ([42–45]):** Ground speed in m/s. Used internally for velocity prediction.
Set to `0.0f` if unavailable.

**Vz ([46–49]):** Vertical speed in m/s, positive = climbing. Used internally for
elevation rate prediction. Set to `0.0f` if unavailable.

---

## Receive Protocol

### Transport
- Protocol: **UDP unicast**
- Port: **15009**
- Direction: Sender → THEIA (192.168.1.208 default) or HYPERION (192.168.1.206 default)
- Total frame size: **71 bytes**
- Rate: Up to 100 Hz. The receiver processes every valid received frame.

### Frame Validation
The receiver performs the following checks in order:

1. Total length == 71 bytes. Other lengths discarded silently.
2. Magic bytes [0–1] == `0xCB 0x48`. Mismatch discarded.
3. CMD_BYTE [2] == `0xAA`.
4. PAYLOAD_LEN [5–6] == 62.
5. CRC-16/CCITT over bytes [0–68] matches bytes [69–70]. Failure discarded.

### Processing on Valid CUE

1. Track ID, class, timestamp, lat/lon/alt stored in active track state.
2. **DROP (0):** System exits cueing mode.
3. **TRACK (1):** System enters cueing mode, points to target lat/lon/alt. Sends one
   `0xAF` status response to sender.
4. **REPORT ONCE (2):** Sends one `0xAF` status response immediately.
5. **REPORT POS/ATT (3):** Sends one `0xAB` POS/ATT frame to sender.
6. **WEAPON HOLD (4):** Suppresses weapon release regardless of vote state.
7. **WEAPON FREE TO FIRE (5):** Releases weapon hold — votes govern firing.
8. **REPORT CONTINUOUS ON (254):** Begins 10 Hz `0xAF` stream to sender.
9. **REPORT CONTINUOUS OFF (255):** Stops continuous stream.

### Track Timeout
If no CUE packet is received for more than the configured timeout, the system ceases
cueing. The operator must change mode explicitly to resume normal operation.

---

## Response Messages

THEIA sends two response types back to the CUE source IP on port 15009.
Full layouts are defined in this document:

| CMD | Message | Payload | Total Frame | Trigger |
|-----|---------|---------|-------------|---------|
| `0xAF` | Status Response | 30 bytes | 39 bytes | Every TRACK; or 10 Hz if REPORT CONTINUOUS ON |
| `0xAB` | POS/ATT Report | 32 bytes | 41 bytes | On REPORT POS/ATT or REPORT CONTINUOUS ON |

**Status Response (`0xAF`):** System state, gimbal mode, active camera, fire control
vote bits, geometry vote bits (raw + computed), gimbal LOS az/el NED, laser LOS az/el
NED. See §Status Response — CMD 0xAF.

**POS/ATT Report (`0xAB`):** Platform latitude, longitude, altitude HAE, roll, pitch, yaw
as currently held by the system. See §POS/ATT Report — CMD 0xAB.

### Response Routing

**Direct path (VENDOR → THEIA):**
THEIA responds directly to the sender IP on port 15009. The VENDOR receives `0xAF`
and `0xAB` directly from THEIA.

**Via HYPERION (VENDOR → HYPERION → THEIA):**
THEIA responds to HYPERION (the immediate sender) on port 15009. The VENDOR does
not receive responses directly from THEIA.

> **HYPERION implementation requirement:** When acting as a CUE relay, HYPERION must
> subscribe to the `0xAF` continuous stream from THEIA (Track CMD = `254`,
> REPORT CONTINUOUS ON) and forward all received `0xAF` and `0xAB` frames verbatim
> to the originating VENDOR on port 15009. Failure to relay leaves the VENDOR blind
> to system state and fire control votes.

---

## Status Response — CMD `0xAF`

THEIA transmits this message to the CUE source in response to each received CUE packet (or
continuously at 10 Hz if requested). When HYPERION is acting as a CUE relay, it forwards
this message verbatim to the originating VENDOR. It provides full engagement state including
fire control votes, gimbal LOS, and laser LOS.

**Payload: 30 bytes. Total framed: 39 bytes.**

| Payload Offset | Size | Type | Field | Notes |
|----------------|------|------|-------|-------|
| 0 | 1 | byte | System State | SYSTEM_STATES enum — see below |
| 1 | 1 | byte | System Mode | BDC_MODES enum — see below |
| 2 | 1 | byte | Active CAM ID | VIS=0, MWIR=1 |
| 3 | 1 | byte | VOTE BITS | Fire control vote bits — see bit table |
| 4 | 1 | byte | GEO VOTE BITS1 | Raw geometry bits — see bit table |
| 5 | 1 | byte | GEO VOTE BITS2 | Computed geometry votes with override logic — see bit table |
| 6 | 4 | float | Gimbal Az NED | Gimbal LOS azimuth, degrees NED — incorporates platform attitude |
| 10 | 4 | float | Gimbal EL NED | Gimbal LOS elevation, degrees NED — incorporates platform attitude |
| 14 | 4 | float | Laser Az NED | Laser LOS azimuth, degrees NED |
| 18 | 4 | float | Laser EL NED | Laser LOS elevation, degrees NED |
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

### VOTE BITS (byte [3]) — fire control
| Bit | Name | Notes |
|-----|------|-------|
| 0 | `isLaserTotalHW_Vote_rb` | |
| 1 | `isNotAbort_Vote_rb` | **Inverted** — 0 = abort ACTIVE |
| 2 | `isArmed_Vote_rb` | |
| 3 | `isBDA_Vote_rb` | |
| 4 | `isEMON_rb` | |
| 5 | `isLaserFireRequested_Vote` | |
| 6 | `isLaserTotal_Vote_rb` | All fire control votes pass |
| 7 | `isCombat_Vote_rb` | |

### GEO VOTE BITS1 (byte [4]) — raw geometry
| Bit | Name |
|-----|------|
| 0 | `isHorizVoteOverride` |
| 1 | `isKIZVoteOverride` |
| 2 | `isLCHVoteOverride` |
| 3 | `isBDAVoteOverride` |
| 4 | `isBelowHoriz` |
| 5 | `isInKIZ` |
| 6 | `isInLCH` |
| 7 | RES |

### GEO VOTE BITS2 (byte [5]) — computed votes with override logic
| Bit | Name | Logic |
|-----|------|-------|
| 0 | `BelowHorizVote` | `isHorizVoteOverride ? true : isBelowHoriz` |
| 1 | `InKIZVote` | `isKIZVoteOverride ? true : isInKIZ` |
| 2 | `InLCHVote` | `isLCHVoteOverride ? true : isInLCH` |
| 3 | `BDCVote` | `isBDAVoteOverride ? true : (BelowHorizVote & InKIZVote & InLCHVote)` |
| 4 | RES | |
| 5 | `isHorizonLoaded` | |
| 6 | RES | |
| 7 | `isFSMLimited` | |

> **Clean fire condition:** GEO VOTE BITS2 bit 3 (geometry vote) = 1 AND VOTE BITS bit 6 (fire clear) = 1.

---

## POS/ATT Report — CMD `0xAB`

Sent by THEIA on request (Track CMD=3) or continuously at 10 Hz (Track CMD=254).
When HYPERION is acting as a CUE relay, it forwards this message verbatim to the
originating VENDOR. Reports current platform geodetic position and attitude as known
to the system.

**Payload: 32 bytes. Total framed: 41 bytes.**

| Payload Offset | Size | Type | Field | Notes |
|----------------|------|------|-------|-------|
| 0 | 8 | double | Latitude | WGS-84 decimal degrees |
| 8 | 8 | double | Longitude | WGS-84 decimal degrees |
| 16 | 4 | float | Altitude HAE | Height Above Ellipsoid, metres |
| 20 | 4 | float | Roll | Degrees NED |
| 24 | 4 | float | Pitch | Degrees NED |
| 28 | 4 | float | Yaw | Degrees NED |

---

## Integration Requirements

### Integration Path Guidance

Two integration paths are available:

| Path | Target | Behaviour | Recommended For |
|------|--------|-----------|-----------------|
| Direct | THEIA (192.168.1.208) | Single active track — system immediately cues on receipt | Pre-selected tracks, simple integrations, high-quality track sources |
| Via HYPERION | HYPERION (192.168.1.206) | Full air picture — commander selects and directs tracks to THEIA | Production C2 integrations, multi-sensor environments, operator-in-the-loop |

> **Recommendation:** HYPERION is the preferred C2 intermediary for production
> integrations. HYPERION maintains a full air picture from multiple sensor sources
> (ADS-B, RADAR, LoRa/MAVLink) and applies Kalman filtering to smooth and predict
> track positions. A commander reviews the air picture and selects which track to
> direct to THEIA — THEIA acts on that single directed track only.
>
> Direct THEIA integration is appropriate where the engagement decision has already
> been made upstream and a single pre-selected track is being forwarded. THEIA will
> immediately cue on any valid TRACK command received — there is no onboard track
> selection or air picture management.
>
> **Future:** HYPERION is designed to support simultaneous management of multiple
> CROSSBOW systems via multiple THEIA instances, enabling a single HYPERION C2 node
> to coordinate engagement across a distributed deployment.

### Minimum Viable Sender
1. Build a valid EXT_OPS frame: magic `0xCB 0x48`, CMD=`0xAA`, SEQ_NUM, correct CRC.
2. Send UDP to THEIA (192.168.1.208:15009 default) or HYPERION (192.168.1.206:15009 default).
3. Set Track CMD = `1` (TRACK), Track Class = `8` (UAV) or applicable class.
4. Provide valid WGS-84 lat/lon/alt HAE in IEEE 754 double/float LE.
5. Zero all reserved bytes.

### Recommended Sender Behaviour
- Send at native track update rate (10–100 Hz).
- Populate Heading and Speed — HYPERION uses these for Kalman velocity prediction;
  THEIA uses Heading for AC symbol orientation. Set `0.0f` if unavailable.
- Populate Vz when available — HYPERION uses for elevation rate prediction.
  Set `0.0f` if unknown.
- Use a stable Track ID per target across the engagement.
- Increment SEQ_NUM monotonically per packet.
- Send REPORT CONTINUOUS ON (254) at session start for continuous `0xAF` feedback.
- Parse `0xAF` GEO VOTE BITS2 bit 3 (geometry vote) and VOTE BITS bit 6 (fire clear)
  before issuing WEAPON FREE TO FIRE.

### HYPERION Relay Requirement
When HYPERION is acting as a CUE relay to THEIA, it must:
- Subscribe to the `0xAF` continuous stream from THEIA at session start
  (Track CMD = `254`, REPORT CONTINUOUS ON).
- Forward all received `0xAF` and `0xAB` frames verbatim to the originating
  VENDOR on port 15009.

---

## Integration Checklist

| # | Item | Check |
|---|------|-------|
| 1 | Integration path selected — Direct (THEIA .208) or Via HYPERION (.206) | ☐ |
| 2 | Third-party sender IP in 192.168.1.210–.254 (convention) | ☐ |
| 3 | Building valid EXT_OPS frame: magic `0xCB 0x48`, SEQ_NUM, CRC | ☐ |
| 4 | Sending UDP to 192.168.1.208:15009 (direct) or 192.168.1.206:15009 (via HYPERION) | ☐ |
| 5 | Total frame size 71 bytes, CMD_BYTE = `0xAA`, PAYLOAD_LEN = 62 | ☐ |
| 6 | Track CMD = `0x01` (TRACK) for normal pointing | ☐ |
| 7 | Lat/lon IEEE 754 double LE, altitude HAE float LE | ☐ |
| 8 | All reserved bytes zeroed | ☐ |
| 9 | Heading (degrees true) and Speed (m/s) populated (or 0.0f) | ☐ |
| 10 | Listening on sender port 15009 for `0xAF` status response | ☐ |
| 11 | Parsing `0xAF` GEO VOTE BITS2 bit 3 = 1 for geometry vote clear | ☐ |
| 12 | Parsing `0xAF` VOTE BITS bit 6 = 1 for fire clear | ☐ |
| 13 | System in CUE mode — `0xAF` System Mode = `0x03` | ☐ |
| 14 | **HYPERION path only** — HYPERION subscribed to `0xAF` continuous stream from THEIA (Track CMD = `254`) | ☐ |
| 15 | **HYPERION path only** — HYPERION relaying `0xAF` and `0xAB` verbatim to originating VENDOR | ☐ |

---

## Code Reference

C++ and C# implementation examples — struct definitions, CRC helper, frame packing
(CMD `0xAA`) and frame unpacking (CMD `0xAF`) — are provided in:

- **Appendix A** — C/C++ structs and examples
- **Appendix B** — C# structs and examples

---

## Network Reference

All EXT_OPS traffic operates on the 192.168.1.0/24 subnet.

| Node | IP | Port | Role |
|------|----|------|------|
| THEIA | 192.168.1.208 (default) | UDP:15009 | INT_OPS HMI — direct CUE target |
| HYPERION | 192.168.1.206 (default) | UDP:15009 | EXT_OPS CUE relay — recommended C2 intermediary |
| HYPERION aRADAR | 192.168.1.206 (default) | UDP:15001 | HYPERION generic sensor input / CUE SIM injection |
| HYPERION aLORA | 192.168.1.206 (default) | UDP:15002 | HYPERION LoRa/MAVLink sensor input |
| NTP | 192.168.1.33 | — | Network time server |
| IPG reserved | 192.168.1.200–.209 | — | Reserved for IPG nodes — do not assign |
| Third-party integrators | 192.168.1.210–.254 | — | External CUE providers by convention |

> **IP assignment note:** THEIA and HYPERION operate in the `192.168.1.200–.254` external range. The addresses shown are IPG reference deployment defaults — both are operator-configurable. The constraint is that they remain in the `.200–.254` range so embedded controllers accept their A3 packets. IPG reserves `.200–.209`; third-party integrators use `.210–.254` by convention.

> The A1/A2 internal network (192.168.1.10–.33) is not accessible to EXT_OPS
> integrators. For the full internal node reference see `CROSSBOW_ICD_INT_ENG` (IPGD-0003).

---

## Relationship to INT_OPS

EXT_OPS is the cueing input interface — it is not a standalone control path. It always
operates in conjunction with an INT_OPS client. The INT_OPS interface (`CROSSBOW_ICD_INT_OPS`,
IPGD-0004) defines the full operator control plane: MCC, BDC, and TRC access via A3.

THEIA is IPG's reference implementation of an INT_OPS client. A vendor may build a
bespoke HMI using the INT_OPS ICD to replace THEIA entirely — in that case the vendor
implements both interfaces: INT_OPS for system control and EXT_OPS to receive CUE input.
The two tiers are complementary, not alternatives.

```
EXT_OPS (cueing input — this document)
    ↓
INT_OPS client — THEIA or vendor HMI (CROSSBOW_ICD_INT_OPS, IPGD-0004)
    ↓
A3 — MCC, BDC, TRC
    ↓
A1/A2 — internal controllers
```

---

## Future Work

The following capabilities are planned but not yet implemented.

| Item | Description | Status |
|------|-------------|--------|
| Auth/service pathway | A2 engineering interface access via planned authentication service — enables authorised service partners to access diagnostics and maintenance functions. | Planned |
| HYPERION multi-THEIA | HYPERION C2 node managing multiple simultaneous CROSSBOW systems via multiple THEIA instances — enables coordinated engagement across distributed deployments. | Planned |
| Video multicast | `0xD1 ORIN_SET_STREAM_MULTICAST` — stream to multicast group `239.127.1.21` enabling multiple simultaneous receivers. | Defined — firmware pending |
| 30 fps stream option | `0xD2 ORIN_SET_STREAM_60FPS {0x00}` — 30 fps mode for software-decode receivers. | Defined — firmware pending |

---

## Related Documents

For the full document set see CROSSBOW Document Register (IPGD-0001).
Documents directly referenced by this ICD:

| Doc # | Document | Relevance |
|-------|----------|-----------|
| IPGD-0004 | `CROSSBOW_ICD_INT_OPS` | INT_OPS A3 operator commands — Tier 1 interface |
| IPGD-0006 | `ARCHITECTURE.md` | Network topology, CRC spec, HYPERION architecture |

---

## Appendix A — C/C++ Code Reference

### Structs

```cpp
#include <stdint.h>
#include <assert.h>

/* EXT_OPS frame header — prepend to all messages on UDP:15009 */
#pragma pack(push, 1)
typedef struct {
    uint8_t  magic_hi;      // 0xCB
    uint8_t  magic_lo;      // 0x48
    uint8_t  cmd;
    uint16_t seq_num;       // LE
    uint16_t payload_len;   // LE
} ExtOpsFrameHeader_t;      // 7 bytes
#pragma pack(pop)

/* CUE inbound payload — CMD 0xAA */
#pragma pack(push, 1)
typedef struct {
    int64_t  ms_timestamp;  // [0–7]:   ms since Unix epoch
    uint8_t  track_id[8];   // [8–15]:  ASCII null-padded
    uint8_t  track_class;   // [16]:    Track Class enum
    uint8_t  track_cmd;     // [17]:    Track CMD enum
    double   latitude;      // [18–25]: WGS-84 degrees
    double   longitude;     // [26–33]: WGS-84 degrees
    float    altitude;      // [34–37]: metres HAE
    float    heading;       // [38–41]: degrees true (0–360, North=0)
    float    speed;         // [42–45]: ground speed m/s
    float    vz;            // [46–49]: vertical speed m/s, positive=climbing
    uint32_t reserved[3];   // [50–61]
} CuePayload_t;             // 62 bytes
#pragma pack(pop)

/* Status response payload — CMD 0xAF */
#pragma pack(push, 1)
typedef struct {
    uint8_t  system_state;    // [0]:    SYSTEM_STATES enum
    uint8_t  system_mode;     // [1]:    BDC_MODES enum
    uint8_t  active_cam_id;   // [2]:    0=VIS, 1=MWIR
    uint8_t  vote_bits;       // [3]:    fire control votes
    uint8_t  geo_vote_bits1;  // [4]:    raw geometry bits
    uint8_t  geo_vote_bits2;  // [5]:    computed geometry votes
    float    gimbal_az_ned;   // [6–9]:  degrees NED
    float    gimbal_el_ned;   // [10–13]:degrees NED
    float    laser_az_ned;    // [14–17]:degrees NED
    float    laser_el_ned;    // [18–21]:degrees NED
    uint32_t reserved[2];     // [22–29]
} StatusResponsePayload_t;    // 30 bytes
#pragma pack(pop)

static_assert(sizeof(ExtOpsFrameHeader_t)     ==  7, "Header must be 7 bytes");
static_assert(sizeof(CuePayload_t)            == 62, "CUE payload must be 62 bytes");
static_assert(sizeof(StatusResponsePayload_t) == 30, "Status payload must be 30 bytes");
/* Total CUE frame  = 7 + 62 + 2 = 71 bytes */
/* Total 0xAF frame = 7 + 30 + 2 = 39 bytes */
```

### CRC-16/CCITT

```cpp
/* CRC-16/CCITT — poly=0x1021, init=0xFFFF
   Covers bytes 0 through end of PAYLOAD (everything except the CRC itself) */
static uint16_t crc16_ccitt(const uint8_t* data, size_t len) {
    uint16_t crc = 0xFFFF;
    for (size_t i = 0; i < len; i++) {
        crc ^= (uint16_t)data[i] << 8;
        for (int j = 0; j < 8; j++)
            crc = (crc & 0x8000) ? (crc << 1) ^ 0x1021 : crc << 1;
    }
    return crc;
}
/* Verification: crc16_ccitt("123456789", 9) == 0x29B1 */
```

### Pack and Send CMD 0xAA (CUE Frame)

```cpp
#include <stdint.h>
#include <string.h>
#include <time.h>
#include <sys/socket.h>
#include <arpa/inet.h>
#include <unistd.h>

#define EXT_OPS_MAGIC_HI  0xCB
#define EXT_OPS_MAGIC_LO  0x48
#define CMD_CUE           0xAA
#define CUE_PAYLOAD_LEN   62
#define CUE_FRAME_LEN     71   /* 7 header + 62 payload + 2 CRC */

/* Returns 0 on success, -1 on error */
int send_cue_frame(int sock,
                   const struct sockaddr_in* dest,
                   uint16_t*  seq_num,
                   double     lat,
                   double     lon,
                   float      alt_hae,
                   float      heading,
                   float      speed,
                   float      vz,
                   uint8_t    track_class,
                   uint8_t    track_cmd,
                   const char track_id[8])
{
    uint8_t frame[CUE_FRAME_LEN];
    memset(frame, 0, sizeof(frame));

    /* Header */
    frame[0] = EXT_OPS_MAGIC_HI;
    frame[1] = EXT_OPS_MAGIC_LO;
    frame[2] = CMD_CUE;
    frame[3] = (*seq_num) & 0xFF;
    frame[4] = (*seq_num >> 8) & 0xFF;
    frame[5] = CUE_PAYLOAD_LEN & 0xFF;
    frame[6] = (CUE_PAYLOAD_LEN >> 8) & 0xFF;
    (*seq_num)++;

    /* Payload at offset 7 */
    uint8_t* p = frame + 7;

    int64_t ms_now = (int64_t)time(NULL) * 1000;
    memcpy(p + 0,  &ms_now,     8);   /* ms timestamp [0–7]  */
    memcpy(p + 8,  track_id,    8);   /* track ID     [8–15] */
    p[16] = track_class;              /* track class  [16]   */
    p[17] = track_cmd;                /* track cmd    [17]   */
    memcpy(p + 18, &lat,        8);   /* latitude     [18–25]*/
    memcpy(p + 26, &lon,        8);   /* longitude    [26–33]*/
    memcpy(p + 34, &alt_hae,    4);   /* altitude HAE [34–37]*/
    memcpy(p + 38, &heading,    4);   /* heading      [38–41]*/
    memcpy(p + 42, &speed,      4);   /* speed        [42–45]*/
    memcpy(p + 46, &vz,         4);   /* vz           [46–49]*/
    /* reserved [50–61] already zeroed */

    /* CRC over bytes 0–68 */
    uint16_t crc = crc16_ccitt(frame, 7 + CUE_PAYLOAD_LEN);
    frame[69] = crc & 0xFF;
    frame[70] = (crc >> 8) & 0xFF;

    ssize_t sent = sendto(sock, frame, CUE_FRAME_LEN, 0,
                          (const struct sockaddr*)dest, sizeof(*dest));
    return (sent == CUE_FRAME_LEN) ? 0 : -1;
}

/* Example usage */
int main(void) {
    int sock = socket(AF_INET, SOCK_DGRAM, 0);

    struct sockaddr_in dest;
    memset(&dest, 0, sizeof(dest));
    dest.sin_family = AF_INET;
    dest.sin_port   = htons(15009);
    inet_pton(AF_INET, "192.168.1.208", &dest.sin_addr); /* THEIA */
    /* or "192.168.1.206" for HYPERION */

    uint16_t seq = 0;
    char track_id[8] = {'A','B','1','2','3','4',0,0};

    send_cue_frame(sock, &dest, &seq,
                   51.5074, -0.1278,  /* lat/lon      */
                   100.0f,            /* alt HAE m    */
                   270.0f,            /* heading deg  */
                   15.0f,             /* speed m/s    */
                   0.0f,              /* vz           */
                   8,                 /* UAV          */
                   1,                 /* TRACK        */
                   track_id);
    close(sock);
    return 0;
}
```

### Receive and Unpack CMD 0xAF (Status Response)

```cpp
#include <stdint.h>
#include <string.h>
#include <sys/socket.h>
#include <arpa/inet.h>

#define CMD_STATUS_RESPONSE  0xAF
#define STATUS_FRAME_LEN     39   /* 7 header + 30 payload + 2 CRC */

/* Vote bit helpers */
#define GEOMETRY_VOTE(bits2)   (((bits2) >> 3) & 0x01)  /* GEO VOTE BITS2 bit 3 */
#define FIRE_CLEAR(vote_bits)  (((vote_bits) >> 6) & 0x01) /* VOTE BITS bit 6    */
#define IN_CUE_MODE(payload)   ((payload)[1] == 0x03)

/* Returns 0 on valid parse, -1 on invalid frame */
int recv_status_response(int sock, StatusResponsePayload_t* out)
{
    uint8_t frame[STATUS_FRAME_LEN];
    ssize_t n = recv(sock, frame, sizeof(frame), 0);
    if (n != STATUS_FRAME_LEN)           return -1;
    if (frame[0] != EXT_OPS_MAGIC_HI)   return -1;
    if (frame[1] != EXT_OPS_MAGIC_LO)   return -1;
    if (frame[2] != CMD_STATUS_RESPONSE) return -1;

    /* Verify CRC over bytes 0–36 */
    uint16_t crc_calc = crc16_ccitt(frame, 37);
    uint16_t crc_recv = frame[37] | ((uint16_t)frame[38] << 8);
    if (crc_calc != crc_recv)            return -1;

    memcpy(out, frame + 7, sizeof(StatusResponsePayload_t));
    return 0;
}

/* Example usage */
int main(void) {
    int sock = socket(AF_INET, SOCK_DGRAM, 0);

    struct sockaddr_in bind_addr;
    memset(&bind_addr, 0, sizeof(bind_addr));
    bind_addr.sin_family      = AF_INET;
    bind_addr.sin_port        = htons(15009);
    bind_addr.sin_addr.s_addr = INADDR_ANY;
    bind(sock, (struct sockaddr*)&bind_addr, sizeof(bind_addr));

    StatusResponsePayload_t status;
    if (recv_status_response(sock, &status) == 0) {
        int geo_clear  = GEOMETRY_VOTE(status.geo_vote_bits2);
        int fire_clear = FIRE_CLEAR(status.vote_bits);
        int in_cue     = IN_CUE_MODE((uint8_t*)&status);
        /* geo_clear && fire_clear && in_cue → safe to issue WEAPON FREE TO FIRE */
    }

    close(sock);
    return 0;
}
```

### Receive and Unpack CMD 0xAB (POS/ATT Report)

```cpp
#define CMD_POSATT_REPORT   0xAB
#define POSATT_FRAME_LEN    41   /* 7 header + 32 payload + 2 CRC */

#pragma pack(push, 1)
typedef struct {
    double   latitude;    /* [0–7]:   WGS-84 degrees          */
    double   longitude;   /* [8–15]:  WGS-84 degrees          */
    float    altitude;    /* [16–19]: metres HAE               */
    float    roll;        /* [20–23]: degrees NED              */
    float    pitch;       /* [24–27]: degrees NED              */
    float    yaw;         /* [28–31]: degrees NED              */
} PosAttPayload_t;        /* 32 bytes */
#pragma pack(pop)

static_assert(sizeof(PosAttPayload_t) == 32, "POS/ATT payload must be 32 bytes");

/* Returns 0 on valid parse, -1 on invalid frame */
int recv_pos_att_report(int sock, PosAttPayload_t* out)
{
    uint8_t frame[POSATT_FRAME_LEN];
    ssize_t n = recv(sock, frame, sizeof(frame), 0);
    if (n != POSATT_FRAME_LEN)          return -1;
    if (frame[0] != EXT_OPS_MAGIC_HI)  return -1;
    if (frame[1] != EXT_OPS_MAGIC_LO)  return -1;
    if (frame[2] != CMD_POSATT_REPORT)  return -1;

    /* Verify CRC over bytes 0–38 */
    uint16_t crc_calc = crc16_ccitt(frame, 39);
    uint16_t crc_recv = frame[39] | ((uint16_t)frame[40] << 8);
    if (crc_calc != crc_recv)           return -1;

    memcpy(out, frame + 7, sizeof(PosAttPayload_t));
    return 0;
}

/* Example usage */
int main(void) {
    int sock = socket(AF_INET, SOCK_DGRAM, 0);

    struct sockaddr_in bind_addr;
    memset(&bind_addr, 0, sizeof(bind_addr));
    bind_addr.sin_family      = AF_INET;
    bind_addr.sin_port        = htons(15009);
    bind_addr.sin_addr.s_addr = INADDR_ANY;
    bind(sock, (struct sockaddr*)&bind_addr, sizeof(bind_addr));

    PosAttPayload_t pos;
    if (recv_pos_att_report(sock, &pos) == 0) {
        /* pos.latitude, pos.longitude — WGS-84 degrees  */
        /* pos.altitude                — metres HAE       */
        /* pos.roll, pos.pitch, pos.yaw — degrees NED    */
    }

    close(sock);
    return 0;
}
```

---

## Appendix B — C# Code Reference

### CRC-16/CCITT

```csharp
private static ushort Crc16Ccitt(byte[] data, int offset, int length)
{
    ushort crc = 0xFFFF;
    for (int i = offset; i < offset + length; i++)
    {
        crc ^= (ushort)(data[i] << 8);
        for (int j = 0; j < 8; j++)
            crc = (crc & 0x8000) != 0
                ? (ushort)((crc << 1) ^ 0x1021)
                : (ushort)(crc << 1);
    }
    return crc;
}
/* Verification: Crc16Ccitt("123456789", 0, 9) == 0x29B1 */
```

### StatusResponse Class

```csharp
public class StatusResponse
{
    public byte  SystemState;    /* SYSTEM_STATES enum             */
    public byte  SystemMode;     /* BDC_MODES enum — 0x03 = CUE    */
    public byte  ActiveCamId;    /* 0=VIS, 1=MWIR                  */
    public byte  VoteBits;       /* fire control votes             */
    public byte  GeoVoteBits1;   /* raw geometry bits              */
    public byte  GeoVoteBits2;   /* computed geometry votes        */
    public float GimbalAzNed;
    public float GimbalElNed;
    public float LaserAzNed;
    public float LaserElNed;

    /* Convenience helpers */
    public bool GeometryVote  => ((GeoVoteBits2 >> 3) & 0x01) == 1; /* BITS2 bit 3    */
    public bool FireClear     => ((VoteBits     >> 6) & 0x01) == 1; /* VOTE BITS bit 6*/
    public bool InCueMode     => SystemMode == 0x03;
    public bool ClearToEngage => GeometryVote && FireClear && InCueMode;
}
```

### PosAttReport Class

```csharp
public class PosAttReport
{
    public double Latitude;     /* WGS-84 degrees   */
    public double Longitude;    /* WGS-84 degrees   */
    public float  AltitudeHae;  /* metres HAE       */
    public float  Roll;         /* degrees NED      */
    public float  Pitch;        /* degrees NED      */
    public float  Yaw;          /* degrees NED      */
}
```

### Pack and Send CMD 0xAA (CUE Frame)

```csharp
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

public class ExtOpsClient : IDisposable
{
    private readonly UdpClient  _udp;
    private readonly IPEndPoint _dest;
    private          ushort     _seq;

    public ExtOpsClient(string targetIp, int port = 15009)
    {
        _udp  = new UdpClient();
        _dest = new IPEndPoint(IPAddress.Parse(targetIp), port);
    }

    public void SendCueTrack(double lat, double lon, float altHae,
                             float heading, float speed, float vz,
                             byte trackClass, byte trackCmd,
                             byte[] trackId)  /* 8 bytes, null-padded */
    {
        const int payloadLen = 62;
        byte[] frame = new byte[71]; /* 7 + 62 + 2 */

        /* Header */
        frame[0] = 0xCB;
        frame[1] = 0x48;
        frame[2] = 0xAA;
        frame[3] = (byte)(_seq & 0xFF);
        frame[4] = (byte)((_seq >> 8) & 0xFF);
        frame[5] = payloadLen & 0xFF;
        frame[6] = 0x00;
        _seq++;

        /* Payload */
        using (var ms = new MemoryStream(frame, 7, payloadLen))
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); /* [0–7]  */
            byte[] id = new byte[8];
            Array.Copy(trackId, id, Math.Min(trackId.Length, 8));
            bw.Write(id);                  /* [8–15] track ID      */
            bw.Write(trackClass);          /* [16]   track class   */
            bw.Write(trackCmd);            /* [17]   track cmd     */
            bw.Write(lat);                 /* [18–25] latitude     */
            bw.Write(lon);                 /* [26–33] longitude    */
            bw.Write(altHae);              /* [34–37] altitude HAE */
            bw.Write(heading);             /* [38–41] heading      */
            bw.Write(speed);               /* [42–45] speed        */
            bw.Write(vz);                  /* [46–49] vz           */
            bw.Write(0u); bw.Write(0u); bw.Write(0u); /* [50–61] reserved */
        }

        /* CRC over bytes 0–68 */
        ushort crc = Crc16Ccitt(frame, 0, 69);
        frame[69] = (byte)(crc & 0xFF);
        frame[70] = (byte)((crc >> 8) & 0xFF);

        _udp.Send(frame, frame.Length, _dest);
    }

    /* Example usage */
    public static void Main()
    {
        using var client = new ExtOpsClient("192.168.1.208"); /* THEIA default    */
        /* or "192.168.1.206" for HYPERION default */

        byte[] trackId = new byte[8];
        System.Text.Encoding.ASCII.GetBytes("AB1234").CopyTo(trackId, 0);

        client.SendCueTrack(
            lat:        51.5074,
            lon:        -0.1278,
            altHae:     100.0f,
            heading:    270.0f,
            speed:      15.0f,
            vz:         0.0f,
            trackClass: 8,          /* UAV   */
            trackCmd:   1,          /* TRACK */
            trackId:    trackId);
    }

    public void Dispose() => _udp.Dispose();
}
```

### Receive and Unpack CMD 0xAF (Status Response)

```csharp
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

public class ExtOpsListener : IDisposable
{
    private readonly UdpClient  _udp;
    private          IPEndPoint _remote = new IPEndPoint(IPAddress.Any, 0);

    public ExtOpsListener(int port = 15009)
    {
        _udp = new UdpClient(port);
    }

    /* Returns parsed StatusResponse, or null on invalid frame */
    public StatusResponse ReceiveStatus()
    {
        byte[] frame = _udp.Receive(ref _remote);
        if (frame.Length != 39) return null;
        if (frame[0] != 0xCB)   return null;
        if (frame[1] != 0x48)   return null;
        if (frame[2] != 0xAF)   return null;

        /* Verify CRC over bytes 0–36 */
        ushort crcCalc = Crc16Ccitt(frame, 0, 37);
        ushort crcRecv = (ushort)(frame[37] | (frame[38] << 8));
        if (crcCalc != crcRecv) return null;

        /* Parse payload at offset 7 */
        using var ms = new MemoryStream(frame, 7, 30);
        using var br = new BinaryReader(ms);

        return new StatusResponse {
            SystemState  = br.ReadByte(),
            SystemMode   = br.ReadByte(),
            ActiveCamId  = br.ReadByte(),
            VoteBits     = br.ReadByte(),
            GeoVoteBits1 = br.ReadByte(),
            GeoVoteBits2 = br.ReadByte(),
            GimbalAzNed  = br.ReadSingle(),
            GimbalElNed  = br.ReadSingle(),
            LaserAzNed   = br.ReadSingle(),
            LaserElNed   = br.ReadSingle()
        };
    }

    /* Example usage */
    public static void Main()
    {
        using var listener = new ExtOpsListener(15009);
        while (true)
        {
            var s = listener.ReceiveStatus();
            if (s == null) continue;
            Console.WriteLine($"Mode={s.SystemMode:X2} " +
                              $"Geo={s.GeometryVote} " +
                              $"Fire={s.FireClear} " +
                              $"Clear={s.ClearToEngage}");
        }
    }

    public void Dispose() => _udp.Dispose();
}
```

### Receive and Unpack CMD 0xAB (POS/ATT Report)

```csharp
/* Add to ExtOpsListener — receives and parses a POS/ATT report frame */

/* Returns parsed PosAttReport, or null on invalid frame */
public PosAttReport ReceivePosAtt()
{
    byte[] frame = _udp.Receive(ref _remote);
    if (frame.Length != 41) return null;
    if (frame[0] != 0xCB)   return null;
    if (frame[1] != 0x48)   return null;
    if (frame[2] != 0xAB)   return null;

    /* Verify CRC over bytes 0–38 */
    ushort crcCalc = Crc16Ccitt(frame, 0, 39);
    ushort crcRecv = (ushort)(frame[39] | (frame[40] << 8));
    if (crcCalc != crcRecv) return null;

    /* Parse payload at offset 7 */
    using var ms = new MemoryStream(frame, 7, 32);
    using var br = new BinaryReader(ms);

    return new PosAttReport {
        Latitude    = br.ReadDouble(),
        Longitude   = br.ReadDouble(),
        AltitudeHae = br.ReadSingle(),
        Roll        = br.ReadSingle(),
        Pitch       = br.ReadSingle(),
        Yaw         = br.ReadSingle()
    };
}

/* Example usage */
public static void Main()
{
    using var listener = new ExtOpsListener(15009);
    while (true)
    {
        var p = listener.ReceivePosAtt();
        if (p == null) continue;
        Console.WriteLine($"Lat={p.Latitude:F6} Lon={p.Longitude:F6} " +
                          $"Alt={p.AltitudeHae:F1}m " +
                          $"R={p.Roll:F2} P={p.Pitch:F2} Y={p.Yaw:F2}");
    }
}
```

---

*End of document.*
