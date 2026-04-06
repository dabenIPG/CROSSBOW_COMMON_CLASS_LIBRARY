# CROSSBOW Mini HEL — ICD Document Build Specification
**Version:** v3.1.0 | **Date:** 2026-03-17 | **Sessions:** 1–24

This document captures the complete cumulative specification used to generate all three Word ICD documents from their JavaScript generators.

---

## Revision History

| Version | Date | Sessions | Change |
|---------|------|----------|--------|
| v1.7.1 | 2026-03-10 | 1–13 | Original two-document spec (gen_internal_icd.js / gen_external_icd.js) |
| v3.0.3 | 2026-03-16 | 14–20 | Three-document split: INT_ENG / INT_OPS / EXT_OPS. Scope label rename INT→INT_ENG, EXT→INT_OPS. New commands. Integrator tier model. Video stream section. EXT_OPS namespace overlap note. This document supersedes v1.7.1. |
| v3.1.0 | 2026-03-17 | 21–24 | Doc numbers corrected (IPGD-0003/0004/0005). Output filenames unversioned. Classification fields, Tier Overview, Network Reference, Relationship sections added to all three generators. Appendix A/B added to EXT_OPS. Vote bit corrections. THEIA .208, NTP .33, TRC3→TRC throughout. |

---

## 1. Output Files

| Generator | Output file | Doc # | Version | Audience |
|-----------|-------------|-------|---------|---------|
| `gen_eng_icd.js` | `CROSSBOW_ICD_INT_ENG.docx` | IPGD-0003 | 3.1.0 | IPG engineering only |
| `gen_int_ops_icd.js` | `CROSSBOW_ICD_INT_OPS.docx` | IPGD-0004 | 3.1.0 | Tier 1 integrators |
| `gen_ext_ops_icd.js` | `CROSSBOW_ICD_EXT_OPS.docx` | IPGD-0005 | 3.1.0 | Tier 2 integrators |

**Retired generators (sessions 1–13):**

| Old generator | Old output file | Status |
|--------------|----------------|--------|
| `gen_internal_icd.js` | `CROSSBOW_INTERNAL_ICD_v1.7.1.docx` | Retired session 14 — replaced by `gen_eng_icd.js` |
| `gen_external_icd.js` | `CROSSBOW_EXTERNAL_ICD_v1.7.1.docx` | Retired session 14 — replaced by `gen_int_ops_icd.js` |

**To rebuild:**
```
node gen_eng_icd.js
node gen_int_ops_icd.js
node gen_ext_ops_icd.js
```

**Dependencies** (same `package.json`, same directory as generators):
```
npm install docx
```

**Supporting source files** (must be present alongside generators):

| File | Purpose |
|------|---------|
| `logo.jpg` | Logo image — embedded in header as `ImageRun` (130×38 px) |
| `crc.hpp` | Firmware header — CRC-16/CCITT (poly 0x1021, init 0xFFFF) |
| `frame.hpp` | Firmware header — port constants, magic bytes, STATUS codes, frame geometry |
| `version.h` | Firmware header — `VERSION_PACK(maj,min,pat)` macro |

---

## 2. Page Layout

All three documents share identical page geometry.

| Property | Value |
|----------|-------|
| Paper size | US Letter, Portrait (8.5" × 11") |
| Margins | 1" all sides (1440 DXA each) |
| Content width | **9360 DXA** (6.5") — all tables must sum to this |
| Header height | 720 DXA |
| Footer height | 720 DXA |
| `PAGE_W` constant | 12240 DXA |
| `PAGE_H` constant | 15840 DXA |
| `MARGIN` constant | 1440 DXA |
| `CONTENT` constant | `PAGE_W - 2 * MARGIN` = 9360 DXA |

---

## 3. Typography

All three documents use the same type specification.

| Element | Font | Size (half-pts) | Size (pt) | Colour | Notes |
|---------|------|----------------|-----------|--------|-------|
| H1 | Aptos Display | sz=40 | 20pt | `#0F4761` | before=360, after=80 |
| H2 | Aptos Display | sz=32 | 16pt | `#0F4761` | before=160, after=80 |
| H3 | Aptos | sz=28 | 14pt | `#0F4761` | before=160, after=80 |
| Body | Calibri | sz=22 | 11pt | Black | spacing after=100, left-aligned |
| Table cell text | Calibri | sz=20 | 10pt | Black | top-aligned |
| Table header text | Calibri | sz=20 | 10pt | White or Black | centred, bold |
| Code | Courier New | sz=18 | 9pt | `#0F4761` | indent left=360, after=60 |
| Cover title | Aptos Display | sz=56 | 28pt | `#0F4761` | centred, bold |
| Cover subtitle | Aptos Display | sz=40 | 20pt | `#1F6B8C` | centred, bold |
| Cover descriptor | Calibri | sz=32 | 16pt | per doc | centred, bold |
| Cover version line | Calibri | sz=24 | 12pt | `#444444` | centred |
| Cover detail line | Calibri | sz=22 | 11pt | `#666666` | centred |
| Cover note line | Calibri | sz=20 | 10pt | `#888888` | centred, italic |

**Default document font:** `Calibri` size `22` (set in `styles.default.document.run`).

---

## 4. Colour Palette

All three documents share the same palette constants.

| Constant | Hex | Usage |
|----------|-----|-------|
| `HDG_COLOR` | `#0F4761` | All headings; section-header cell fill (`secHdrCell`); header/footer rule; `titleCell` fill |
| `BLUE_SEC` | `#1F6B8C` | Cover subtitle / descriptor text |
| `GREY_HDR` | `#D9D9D9` | Table column-header row fill (`hdrCell`) |
| `BLUE_LT` | `#DEEAF1` | Alternating table row (odd rows) |
| `WHITE` | `#FFFFFF` | Alternating table row (even rows) |
| `GREY_RES` | `#EDEDED` | Reserved command rows |
| `ORANGE` | `#FCE4D6` | Session-note rows in register tables (INT_ENG only) |
| `GREEN_PT` | `#E2EFDA` | Pass-through / embedded register rows (INT_ENG, INT_OPS) |
| `YELLOW_PH` | `#FFF2CC` | Placeholder rows (INT_OPS only) |
| `RED_CONF` | `#C00000` | CONFIDENTIAL text (INT_ENG only) |
| `BLACK` | `#000000` | Default body text |

> **Note:** `GREY_RES` changed from `#F2F2F2` (v1.7.1) to `#EDEDED` (v3.0.3). EXT_OPS does not define `ORANGE`, `GREEN_PT`, `YELLOW_PH`, or `RED_CONF`.

**Row colouring rules (command tables):**
- Even rows: `WHITE`
- Odd rows: `BLUE_LT`
- `RES` scope: `GREY_RES` regardless of row parity
- `INT_ENG` scope: `#EBF3FF` (INT_ENG generator only)

---

## 5. Header

Implemented as a paragraph with a right-aligned tab stop at `CONTENT` (9360 DXA). Logo `ImageRun` followed by doc title text, then tab, then version/classification block. Bottom border rule: `SINGLE`, size 4, colour `#0F4761`.

| Doc | Left content | Right content |
|-----|-------------|---------------|
| INT_ENG | Logo + `"   INT_ENG ICD  |  CROSSBOW MINI 3-8kW"` | `"IPGD-0003  |  v3.0.3  |  2026-03-16  |  "` + `CONFIDENTIAL` (bold, `#C00000`) |
| INT_OPS | Logo + `"   INT_OPS ICD  |  CROSSBOW MINI 3-8kW"` | `"IPGD-0004  |  v3.0.3  |  2026-03-16"` |
| EXT_OPS | Logo + `"   EXT_OPS ICD  |  CROSSBOW MINI 3-8kW"` | `"IPGD-0005  |  v3.0.1  |  2026-03-16  |  CONTROLLED"` |

Logo: `logo.jpg`, `ImageRun` transformation `width: 130, height: 38`, type `"jpg"`.

---

## 6. Footer

Implemented as two paragraphs with right-aligned tab stops at `CONTENT`. Top border rule on first paragraph: `SINGLE`, size 4, colour `#0F4761`.

**First paragraph (page number line):**

| Doc | Left | Right |
|-----|------|-------|
| INT_ENG | `"Document: "` (bold) + `"CROSSBOW_ICD_INT_ENG.docx"` | `"Doc #: IPGD-0003  |  Rev: 01  |  Page "` + `PAGE` + `" of "` + `NUMPAGES` |
| INT_OPS | `"Document: "` (bold) + `"CROSSBOW_ICD_INT_OPS.docx"` | `"Doc #: IPGD-0004  |  Rev: 01  |  Page "` + `PAGE` + `" of "` + `NUMPAGES` |
| EXT_OPS | `"Document: "` (bold) + `"CROSSBOW_ICD_EXT_OPS.docx"` | `"Doc #: IPGD-0005  |  Rev: 01  |  Page "` + `PAGE` + `" of "` + `NUMPAGES` |

**Second paragraph (distribution line):**

| Doc | Left | Right |
|-----|------|-------|
| INT_ENG | `"IPG Photonics  |  Proprietary"` | `"Copyright © 2026 IPG Photonics. All rights reserved."` |
| INT_OPS | `"IPG Photonics  |  Distribution: Authorised Integrators Only"` | `"Copyright © 2026 IPG Photonics. All rights reserved."` |
| EXT_OPS | `"IPG Photonics  |  Distribution: Authorised Tier 2 Integrators Only"` | `"Copyright © 2026 IPG Photonics. All rights reserved."` |

---

## 7. Document Page Order

All three documents follow this sequence:

1. **Cover page** — titles, descriptor, version line, classification note, page break
2. **Revision History page** — Authorization / Checked / Change Record table
3. **Table of Contents** — H1 and H2 headings; page break
4. **Body sections** — see §10
5. **Final section: Acronyms**

---

## 8. Revision History Page

Implemented by `revisionPage(docTitle, docNum, revRows)` — identical implementation across all three generators.

**Column widths** (5 cols, sum to 9360 DXA):

| Col | DXA | Contents |
|-----|-----|----------|
| 0 | 1090 | Row label |
| 1 | 1895 | Department / Date |
| 2 | 1900 | Name / Responsible |
| 3 | 2310 | Date / Description (colspan with col 4 for some rows) |
| 4 | 2165 | Signature |

**Three sections within one bordered table:** Authorization, Checked (4 numbered rows), Change Record.

**Change Record entries:**

### INT_ENG (IPGD-0003)

| Rev | Date | Description |
|-----|------|-------------|
| 1.7 | 2026-03-09 | Tri-port architecture; session 4 register updates; crc.hpp / frame.hpp / version.h |
| 1.7.1 | 2026-03-10 | Full network IP table; NTP architecture; A1 bidirectional TRC↔BDC; TMC A1 dest corrected to .10 |
| 2.0 | 2026-03-11 | defines.hpp/cs canonical across all 5 controllers; enumerations added; SYSTEM_STATES corrected; 0xA4 promoted to EXT_FRAME_PING; CrcHelper.cs 0x29B1 known-answer verified |
| 2.1 | 2026-03-12 | TransportPath enum added to MSG_MCC/MSG_BDC; VERSION_PACK macro; ARCHITECTURE v3.0.0; MSG_MCC.cs / MSG_BDC.cs generated |
| 3.0.0 | 2026-03-15 | Three-document split: INT_ENG / INT_OPS / EXT_OPS; scope labels renamed INT→INT_ENG, EXT→INT_OPS; new commands 0xA7, 0xA8, 0xC9, 0xBA, 0xBB, 0xD1, 0xD2; TRC A1 100 Hz timerfd TX; TelemetryPacket 28 fields; CueReceiver.cs / CueSender.cs |
| 3.0.1 | 2026-03-15 | CUE payload: Vx/Vy NED replaced by Heading/Speed; ARCHITECTURE v3.0.2; MSG_GNSS.cs HAE fix; user guides v1.0.0 |
| 3.0.2 | 2026-03-16 | Word generator three-document split; integrator tier model (Tier 1: INT_OPS+EXT_OPS; Tier 2: EXT_OPS only); EMPLACEMENT_GUI_USER_GUIDE v1.0.0; EXT_OPS namespace overlap documented (0xAA/0xAB/0xAF) |
| 3.0.3 | 2026-03-16 | Video stream section added: port 5000, 1280×720, 60 fps, 10 Mbps, H.264 RTP. Unicast current; multicast pending 0xD1 (group 239.127.1.21); framerate pending 0xD2; GStreamer pipeline; PixelShift −20 px quirk |

### INT_OPS (IPGD-0004)

Same rev history as INT_ENG except:
- 3.0.0 omits confidential internal framing details
- 3.0.3 covers video stream receive requirements (§3.2) rather than full implementation detail

### EXT_OPS (IPGD-0005)

| Rev | Date | Description |
|-----|------|-------------|
| 3.0.0 | 2026-03-15 | Document created. EXT_OPS frame protocol defined (magic 0xCB 0x48, UDP:10009). CMD 0xAA CUE inbound (62-byte payload). CMD 0xAF status response (30 bytes). CMD 0xAB POS/ATT report (32 bytes). |
| 3.0.1 | 2026-03-15 | CUE payload: Vx NED / Vy NED replaced by Heading (true degrees) / Speed (m/s). Vz NED retained. Frame size unchanged (71 bytes total). |

---

## 9. Table of Contents

- Inserted after Revision History, before Section 1
- Shows **H1 and H2** headings only (`headingStyleRange: "1-2"`)
- `hyperlink: true`
- **Note:** Word must update fields on first open (Ctrl+A → F9, or accept the update prompt)

---

## 10. Body Section Structure

### INT_ENG — 9 sections (`gen_eng_icd.js`)

| # | Title | Key content |
|---|-------|------------|
| 1 | System Overview | Controller network; key subsystems; network overview |
| 2 | Document Scope | Three-doc model; 0xAA/0xAB/0xAF namespace overlap note; classification |
| 3 | Network Architecture | §3.1 IP table; §3.2 NTP; §3.3 IP range policy; §3.4 Tri-port; §3.5 Video stream |
| 4 | Framing Protocol — All Ports | Request/response frame; STATUS codes; CRC reference; SEQ replay; client registration |
| 5 | Command Reference — All Commands | Full cmdTable — all INT_ENG and INT_OPS scope commands |
| 6 | Register Layouts | regTable for all 5 controllers (MCC, BDC, TRC, FMC, TMC) |
| 7 | Key Enumerations | SYSTEM_STATES, BDC_MODES, STATUS codes, Tracker IDs, Overlay bitmask, voteBitsMcc, voteBitsBdc |
| 8 | Open Items & Known Issues | infoTable — all open + closed items |
| 9 | Acronyms | ~75 entries including INT_ENG, INT_OPS, EXT_OPS scope term definitions |

### INT_OPS — 8 sections (`gen_int_ops_icd.js`)

| # | Title | Key content |
|---|-------|------------|
| 1 | System Overview | Controller network (A3-relevant); key subsystems |
| 2 | Document Scope | INT_OPS only; tier model note; 0xAA/0xAB/0xAF namespace note |
| 3 | Network Access | §3.1 IP table (A3 endpoints only); §3.2 Video stream (§3.2.1–3.2.4) |
| 4 | External Framing Protocol (A3) | Request/response frame; STATUS codes; CRC reference; SEQ replay; client registration |
| 5 | Command Reference | cmdTable — INT_OPS-scoped commands only (no INT_ENG rows) |
| 6 | Register Payload Layout | MCC REG1 and BDC REG1 register tables |
| 7 | Key Enumerations | SYSTEM_STATES, BDC_MODES, STATUS codes, Tracker IDs, Overlay bitmask |
| 8 | Acronyms | ~65 entries — INT_ENG-only terms excluded |

### EXT_OPS — 8 sections (`gen_ext_ops_icd.js`)

| # | Title | Key content |
|---|-------|------------|
| 1 | Interface Overview | §1.1 message summary table; §1.2 transport parameters |
| 2 | EXT_OPS Frame Protocol | §2.1 frame layout; §2.2 CRC spec; §2.3 frame validation (THEIA receive) |
| 3 | CMD 0xAA — CUE Inbound | §3.1 payload layout table; §3.2 field descriptions; §3.3 Track Class enum; §3.4 Track CMD enum |
| 4 | CMD 0xAF — Status Response | §4.1 payload layout table; §4.2 vote bit interpretation |
| 5 | CMD 0xAB — POS/ATT Report | §5.1 payload layout table |
| 6 | Integration Checklist | 12-item checklist table |
| 7 | C/C++ Struct Definitions | `ExtOpsFrameHeader_t`, `CuePayload_t`, `TheiaStatusPayload_t`, `TheiaPosAttPayload_t` |
| 8 | Acronyms | ~28 entries — EXT_OPS-relevant only |

---

## 11. Column Widths

### Command table — INT_ENG and INT_OPS (5 cols, sum to 9360)

| Col | Header | Inches | DXA |
|-----|--------|--------|-----|
| 0 | Byte | 0.50" | 720 |
| 1 | Enum | 1.75" | 2520 |
| 2 | Description | 1.50" | 2160 |
| 3 | Payload | 2.15" | 3096 |
| 4 | Scope | 0.60" | 864 |
| **Total** | | **6.50"** | **9360** |

Constant: `CMD_COLS = [720, 2520, 2160, 3096, 864]`

### Register table — INT_ENG and INT_OPS (6 cols, sum to 9360)

| Col | Header | DXA |
|-----|--------|-----|
| 0 | Start | 600 |
| 1 | End | 600 |
| 2 | Size | 550 |
| 3 | Field | 1700 |
| 4 | Type | 700 |
| 5 | Description / Notes | 5210 |

Constant: `REG_COLS = [600, 600, 550, 1700, 700, 5210]`

### Payload table — EXT_OPS (5 cols, sum to 9360)

| Col | Header | DXA |
|-----|--------|-----|
| 0 | Offset | 900 |
| 1 | Size | 600 |
| 2 | Type | 900 |
| 3 | Field | 2160 |
| 4 | Notes | 3800 |

Constant: `PAY_COLS = [900, 600, 900, 2160, CONTENT - 4560]` (= 3800)

### Enum 3-col table — EXT_OPS

| Col | DXA |
|-----|-----|
| 0 Value | 900 |
| 1 Name | 2000 |
| 2 Notes | 6460 |

Constant: `ENUM3_COLS = [900, 2000, CONTENT - 2900]`

---

## 12. Scope Values — Command Tables (INT_ENG and INT_OPS)

| Value | Meaning | Generator |
|-------|---------|-----------|
| `INT_ENG` | Engineering-only — A1/A2 ports; IPG internal use | INT_ENG only |
| `INT_OPS` | Operator-accessible — A3 port; Tier 1 integrators | Both INT_ENG and INT_OPS |
| `RES` | Reserved — shown with `GREY_RES` (`#EDEDED`) background | Both |

> **Session 14 rename:** `INT` → `INT_ENG`, `EXT` → `INT_OPS` throughout all generators and markdown ICDs.

---

## 13. Namespace Overlap Note (0xAA / 0xAB / 0xAF)

Byte values `0xAA`, `0xAB`, and `0xAF` appear in both the A3 command namespace (INT_OPS) and the EXT_OPS namespace (UDP:10009). These are entirely separate: different UDP port, different magic bytes, separate listeners. MCC and BDC do not process these byte values on port 10009. THEIA does not process them on port 10050.

- **Decision (session 20):** Option A — document, don't renumber. Note appears in INT_ENG §2 and INT_OPS §2.
- In the INT_ENG command table, `0xAF` is listed as `RES_AF` with scope `RES` and an explicit description of the EXT_OPS overlap.

---

## 14. Video Stream Section

Present in both INT_ENG (§3.5) and INT_OPS (§3.2). The INT_ENG version contains full implementation detail; INT_OPS contains operator-relevant parameters and receive requirements.

| Parameter | Value |
|-----------|-------|
| Port | 5000 (UDP, fixed) |
| Protocol | RTP — payload type 96, encoding H264 |
| Codec | H.264 hardware-encoded (Jetson `nvv4l2h264enc`) |
| Resolution | 1280×720 (fixed — must be passed explicitly; auto-detect produces invalid frames) |
| Framerate | 60 fps (fixed — 30 fps option pending 0xD2) |
| Bitrate | 10 Mbps (fixed) |
| Destination (unicast) | 192.168.1.8 : 5000 (THEIA) |
| Multicast group (pending) | 239.127.1.21, port 5000 (pending 0xD1) |
| UDP receive buffer | 2 MB minimum |
| Jitter buffer | 50 ms, drop-on-latency=true |
| E2E latency HW decode | 30–80 ms (nvh264dec) |
| E2E latency SW decode | 50–100 ms (avdec_h264) |
| PixelShift | −20 px horizontal — fixed encoder alignment artefact. Applied in `GStreamerPipeReader.cs`. Do not change without HW retest. |

**GStreamer pipeline (INT_ENG §3.5.2):**
```
udpsrc port=5000 buffer-size=2097152
  caps="application/x-rtp,media=video,encoding-name=H264,payload=96"
! rtpjitterbuffer latency=50 drop-on-latency=true
! rtph264depay ! h264parse ! nvh264dec
! videoconvert n-threads=4 ! fdsink
```
Software fallback: substitute `avdec_h264` for `nvh264dec`.

**Pending commands (ICD wired, binary handler not yet implemented):**

| CMD | Name | Function |
|-----|------|---------|
| 0xD1 | `ORIN_SET_STREAM_MULTICAST` | Switch TRC3 stream from unicast to multicast group 239.127.1.21 |
| 0xD2 | `ORIN_SET_STREAM_60FPS` | `{0x01}` = 60 fps (default); `{0x00}` = 30 fps |

---

## 15. Integrator Tier Model

Introduced session 20. Controls document distribution.

| Tier | Receives | Access |
|------|----------|--------|
| Tier 1 | `CROSSBOW_ICD_INT_OPS.docx` + `CROSSBOW_ICD_EXT_OPS.docx` | A3 port (port 10050); EXT_OPS CUE provider (port 10009) |
| Tier 2 | `CROSSBOW_ICD_EXT_OPS.docx` only | EXT_OPS CUE provider (port 10009) only |

**EXT_OPS document classification:** `CONTROLLED — AUTHORISED INTEGRATORS ONLY`

**IP range convention:**
- Internal: 192.168.1.1–.99
- Reserved (dropped): 192.168.1.100–.199
- External integrators: 192.168.1.200–.254

---

## 16. EXT_OPS Message Set

| CMD | Name | Direction | Payload | Total frame |
|-----|------|-----------|---------|------------|
| `0xAA` | CUE Inbound | Integrator → THEIA | 62 bytes | 71 bytes |
| `0xAF` | Status Response | THEIA → Integrator | 30 bytes | 39 bytes |
| `0xAB` | POS/ATT Report | THEIA → Integrator | 32 bytes | 41 bytes |

**EXT_OPS frame format** (UDP:10009, magic `0xCB 0x48`):

| Byte(s) | Field | Notes |
|---------|-------|-------|
| 0 | MAGIC_HI | 0xCB |
| 1 | MAGIC_LO | 0x48 |
| 2 | CMD_BYTE | 0xAA / 0xAF / 0xAB |
| 3–4 | SEQ_NUM | uint16 LE |
| 5–6 | PAYLOAD_LEN | uint16 LE |
| 7+ | PAYLOAD | N bytes |
| Last 2 | CRC-16 | uint16 LE — CRC-16/CCITT over bytes 0 through end of PAYLOAD |

Minimum frame: 9 bytes (header 7 + CRC 2, zero-length payload).

---

## 17. Network Architecture

### Device IP Table (192.168.1.x)

| IP | Device | Notes |
|----|--------|-------|
| .1 | Switch / gateway | |
| .8 | HMI / THEIA | NTP stratum 2 for all controllers |
| .10 | MCC | A1 (10019) + A2 (10018) + A3 (10050) |
| .12 | TMC | A1 TX → MCC + A2 |
| .13 | HEL (laser) | UDP port 10011 (IPG) |
| .20 | BDC | A1 (10019) + A2 (10018) + A3 (10050) |
| .21 | Gimbal (Galil) | ports 7777 (cmd) / 7778 (data) |
| .22 | TRC3 / Orin | A1 TX → BDC + A2 |
| .23 | FMC | A1 TX → BDC + A2 |
| .30 | GPS / NovAtel | ports 3001 (RX) / 3002 (TX) |
| .31 | RPI / ADSB | |
| .32 | LoRa | |
| .33 | NTP appliance | GPS-disciplined stratum 1 — controllers must NOT reference this directly |
| .34 | RADAR | |
| .200–.254 | External clients | A3 port access only |

### NTP Architecture
`.33` (stratum 1) → `.8` (THEIA, stratum 2) → all five embedded controllers. Controllers must not reference `.33` directly.

### Port Architecture

| Port | Label | UDP Port | Magic | Access |
|------|-------|----------|-------|--------|
| A1 | Internal Unsolicited | 10019 | `0xCB 0x49` | .1–.99 only |
| A2 | Internal Engineering | 10018 | `0xCB 0x49` | .1–.99 only |
| A3 | External | 10050 | `0xCB 0x58` | .200–.254 only |
| EXT_OPS | CUE Provider | 10009 | `0xCB 0x48` | Any (convention: .200–.254) |

A1 internal magic `0xCB 0x49` is **CONFIDENTIAL** — not present in INT_OPS or EXT_OPS ICDs.

---

## 18. Register Sizes (Session 4 — final)

| Controller | Defined | Reserved | Block |
|------------|---------|----------|-------|
| TRC REG1 | 49 bytes | 15 bytes | 64 |
| FMC REG1 | 44 bytes | 20 bytes | 64 |
| TMC REG1 | 61 bytes | 3 bytes | 64 |
| MCC REG1 | 253 bytes | 3 bytes | 256 |
| BDC REG1 | 391 bytes | 121 bytes | 512 |

---

## 19. System Overview Content

Both INT_ENG (§1) and INT_OPS (§1) open with a System Overview. Content is slightly trimmed in INT_OPS (no confidential internal port detail).

Both contain:
- **Summary paragraph** — CROSSBOW Mini counter-UAS HEL, 3–8 kW, EO/IR gimbal, FSM, distributed controller network
- **§1.1 Controller Network table** — all five controllers with IPs and roles
- **§1.2 Key Subsystems table** — Laser, Gimbal, FSM, EO/IR, GNSS, PMS, Fire Control
- **§1.3 Network Overview paragraph** — tri-port separation; NTP flow

INT_OPS §1.3 notes that external integration is via A3 only (MCC and BDC).

---

## 20. Acronyms Section (Final Section)

**INT_ENG / INT_OPS:** ~75 entries. Includes `INT_ENG`, `INT_OPS`, `EXT_OPS` scope terms.

**EXT_OPS:** ~28 entries, restricted to EXT_OPS-relevant terms.

All three lists include the three new scope acronyms added session 14:

| Acronym | Expansion |
|---------|-----------|
| `EXT_OPS` | External Operations — CUE provider interface scope (UDP:10009, magic 0xCB 0x48) |
| `INT_ENG` | Internal Engineering — command scope for engineering use only (A1/A2 ports) |
| `INT_OPS` | Internal Operations — operator-accessible command scope (A3 port, Tier 1 integrators) |

---

## 21. Open Items (as of v3.0.3)

| # | Item | Status |
|---|------|--------|
| 2 | Confirm external port 10050 with integration team | Pending |
| 8 | Implement FMC MCU Temp at byte 40 in `fmc.cpp` `SEND_REG_01()` | FW pending |
| 10 | Confirm IP range policy (.1–.99 internal / .100–.199 reserved / .200–.254 external) | Pending |
| FW-14 | MCC GNSS bug — `RUNONCE` case 6: `udpRxClient`/`PortRx` used instead of `udpTxClient`/`PortTx`; `EXEC_UDP` same issue | Fix when in front of HW |
| NEW-9 | `MSG_MCC.cs` — HW verify (deployed session 16) | Pending HW |
| NEW-10 | `MSG_BDC.cs` — HW verify (deployed session 16) | Pending HW |
| NEW-18 | CRC cross-platform wire verification — all 5 controllers + C#. Known-answer: `crc16("123456789") == 0x29B1` | Pre-HW test |
| NEW-31 | `frmMain.cs` lines 3356 + 3376 — `SET_LCH_VOTE` arg swap: 2nd arg passes `isLocationValid` twice; should be `isOperatorValid` then `isLocationValid`. KIZ and LCH upload buttons both affected. Reference: correct call at line 2463. | Fix before HW test |
| NEW-32 | `lch.cs` longitude bug — `Longitude % 180.0` applied before negation for West longitudes. No-op for CONUS (<180°) but wrong for >180°. Latent — low risk for current deployment. | Low priority |
| PENDING | Video multicast via `0xD1 ORIN_SET_STREAM_MULTICAST` | ICD wired; binary handler not yet deployed. Group 239.127.1.21. |
| PENDING | 30 fps via `0xD2 ORIN_SET_STREAM_60FPS` | ICD wired; binary handler not yet deployed |
| TRC-M9 | Deprecate TRC3 port 5010 | After HW validation |

**Closed items (sessions 14–20):**

| # | Item | Closed |
|---|------|--------|
| #4 | `EXT_FRAME_PING (0xA4)` promoted | ✅ Session 15 |
| #5 | `0xB7` added to `EXT_CMDS_BDC[]` whitelist | ✅ Session 17 |
| #6 | `telemetry.h` camid — `BDC_CAM_IDS: VIS=0, MWIR=1` confirmed correct | ✅ Session 17 |
| #7 | FSM position reconciliation — BDC int16 commanded vs FMC int32 ADC readback confirmed as correct distinct types | ✅ Session 17 |
| #12 | Session 4 register changes across all 5 controllers | ✅ Session 15 |
| #13 | VERSION_WORD semver migration — `VERSION_PACK` macro adopted, all controllers migrated | ✅ Session 16 |
| #15 | Tri-port framing on all 5 controllers using `crc.hpp` + `frame.hpp` | ✅ Session 17 |

---

## 22. Known Docx Generation Constraints

| Constraint | Detail |
|------------|--------|
| Header/footer tables | `docx-js` renders table cells in headers/footers as empty boxes. All three generators use paragraphs + tab stops — not tables. |
| TOC field population | Word must update fields on first open. Accept the prompt or Ctrl+A → F9. |
| Logo embedding | `logo.jpg` read from `__dirname + "/logo.jpg"`. Must be present alongside the generator script at run time. Embedded as `ImageRun` (130×38 px, type `"jpg"`). |
| Column width type | All table widths use `WidthType.DXA`, NOT percentage. All column arrays must sum to 9360. |
| Node version | Requires Node.js ≥ 18. Run `npm install docx` before first use. |
| `GREY_RES` changed | v1.7.1 used `#F2F2F2`; v3.0.3 uses `#EDEDED` in all three generators. |
| EXT_OPS has no CMD_COLS | EXT_OPS uses `PAY_COLS` (payload layout table) and `ENUM3_COLS`, not `CMD_COLS` or `REG_COLS`. |
| Session-note rows | `ORANGE (#FCE4D6)` rows in register tables — INT_ENG and INT_OPS only. Pass-through rows use `GREEN_PT (#E2EFDA)`. |
| Placeholder rows | `YELLOW_PH (#FFF2CC)` — INT_OPS only, for fields pending confirmation. |

---

*End of build specification — CROSSBOW ICD v3.0.3*
