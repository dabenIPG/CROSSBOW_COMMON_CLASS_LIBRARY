# CROSSBOW — Cross-Application Consistency Summary

**Document Version:** 4.0.0
**Date:** 2026-04-19 (CB-20260419b)
**ICD Reference:** v4.1.0 (IPGD-0003)
**ARCHITECTURE Reference:** v4.0.4 (IPGD-0006)

**Scope:** Consistency audit across all five embedded controllers (MCC, BDC, TMC, FMC, TRC)
and both C# applications (THEIA, CROSSBOW_ENG_GUIS). Covers namespace, naming, wire format,
parsing, logging, and transport path decisions.

---

## 1. Application Overview

| Application | Namespace | Transport | Controllers | Entry Point |
|-------------|-----------|-----------|-------------|-------------|
| **THEIA** | `CROSSBOW` | A3 / port 10050 / magic `0xCB 0x58` | MCC, BDC only | `Parse(data)` → internal `ParseA3` |
| **CROSSBOW_ENG_GUIS** | `CROSSBOW_ENG_GUIS` (shell) / `CROSSBOW` (lib) | A2 / port 10018 / magic `0xCB 0x49` | All 5 controllers | `Parse(data)` → internal `ParseA2` |
| **HYPERION** | `Hyperion` | UDP:15009 (CUE output to THEIA) | External sensors only | N/A — separate stack |

CROSSBOW_ENG_GUIS is the MDI shell (`frmCROSSBOW_ENG`). Child forms: `frmMCC`, `frmBDC`, `frmTMC`, `frmFMC`, `frmTRC` (A2 controller GUIs); `frmHEL` (laser direct TCP); `frmNTP_PTP` (time source management); `frmFWProgrammer` (firmware programmer).

Both THEIA and CROSSBOW_ENG_GUIS use the **shared CROSSBOW class library** (`namespace CROSSBOW`).
The shared library is the single source of truth for all message parsing.

---

## 2. Shared Class Library — Namespace and Naming

| Decision | Value | Status |
|----------|-------|--------|
| Namespace | `CROSSBOW` | ✅ Both apps updated |
| Class naming | `MSG_XXX` (e.g. `MSG_MCC`, `MSG_BDC`) | ✅ Canonical |
| File naming | `MSG_XXX.cs` | ✅ Canonical |
| ENG GUI project name | `CROSSBOW_ENG_GUIS` | ✅ MDI shell project |
| ENG GUI old name | `TRC_ENG_GUI_PRESERVE` | 🚫 Retired — use `CROSSBOW_ENG_GUIS` |

---

## 3. Transport Path Pattern — TransportPath Enum

Established session 16. Both `MSG_MCC` and `MSG_BDC` use a single public `Parse()` entry
point. Transport is selected once at construction — callers never choose ParseA3/ParseA2
directly.

```csharp
public enum TransportPath { A2_Internal, A3_External }

// THEIA
var mcc = new MSG_MCC(log, TransportPath.A3_External);
mcc.Parse(frame);   // validates 521-byte A3 frame, magic 0xCB 0x58

// ENG GUI
var mcc = new MSG_MCC(log, TransportPath.A2_Internal);
mcc.Parse(payload); // raw 512-byte A2 payload, magic 0xCB 0x49
```

`MAGIC_LO` is computed from `TransportPath` — not hardcoded. `ParseA3` and `ParseA2` are
`private` — cross-wiring is impossible at the call site.

`MSG_TRC` uses the same pattern: `Parse(byte[] frame)` validates magic/CRC/status on the
521-byte A2 framed response, then calls `ParseMsg(frame, PAYLOAD_OFFSET)` internally. The
BDC embedded path calls `ParseMsg(bdcReg1, 60)` directly — no frame wrapper needed.

---

## 4. Message Class Audit — All Levels

### Level 0 — No Dependencies

| Class | Block | Size | Status |
|-------|-------|------|--------|
| `MSG_BATTERY` | MCC REG1 [34–44] | 11 B | ✅ Verified |
| `MSG_IPG` | MCC REG1 [45–65] | 21 B | ✅ Verified + TCP direct path (v3.5.0) |
| `MSG_GIMBAL` | BDC REG1 [20–58] | 39 B | ✅ Verified |

### Level 1 — Single Dependency

| Class | Block | Size | Status |
|-------|-------|------|--------|
| `MSG_TMC` | MCC REG1 [66–129] | 64 B | ✅ V1/V2 hardware abstraction (v3.3.9). Packed struct via `MemoryMarshal`. `FW_VERSION_STRING`, `HW_REV_Label`, `IsV1`/`IsV2`, `epochTime`, `activeTimeSourceLabel`. |
| `MSG_CMC` | MCC REG1 [213–244] | 32 B | ✅ Verified |
| `MSG_GNSS` | MCC REG1 [135–212] | 78 B | ✅ `Altitude_HAE`/`Altitude_MSL` properties added |
| `MSG_FMC` | BDC REG1 [169–232] | 64 B | ✅ V1/V2 hardware abstraction (v3.5.2). `HEALTH_BITS`/`POWER_BITS`/`HW_REV`/`TPH_*` added. |
| `MSG_TRC` | BDC REG1 [60–123] | 64 B | ✅ A2 framed `Parse()` + `ParseMsg()` dual entry. `jetsonGpuLoad` [57–58] (v4.0.3). `FW_VERSION_STRING`, `SomSerialLabel`, `epochTime`. CB-20260419b. |

### Level 2 — Multiple Dependencies (dispatchers)

| Class | Status | Notes |
|-------|--------|-------|
| `MSG_MCC` | ✅ Deployed — HW verified | V1/V2 abstraction. `HEALTH_BITS`/`POWER_BITS`. `LASER_MODEL` byte [255]. `isTrainingMode`. |
| `MSG_BDC` | ✅ Deployed — HW verified | V1/V2 abstraction. `HEALTH_BITS`/`POWER_BITS`. `HW_REV`/`TEMP_*` thermistors. HB counters [396–403]. FMC/TRC pass-through. |

---

## 5. MSG_MCC — Change Summary

| # | Change | Status |
|---|--------|--------|
| 1 | A3 frame constants | ✅ `MAGIC_HI=0xCB, FRAME_RESPONSE_LEN=521, PAYLOAD_OFFSET=7, STATUS_OK=0x00` |
| 2 | `Parse()` public dispatcher | ✅ Routes to ParseA3 or ParseA2 via `TransportPath` |
| 3 | `LastFrameStatus` property | ✅ |
| 4 | V1/V2 hardware abstraction | ✅ ICD v3.4.0 — `HEALTH_BITS` byte [9], `POWER_BITS` byte [10], `HW_REV` byte [254] |
| 5 | `LASER_MODEL` byte [255] | ✅ ICD v3.5.0 |
| 6 | `isTrainingMode` | ✅ ICD v3.5.0 — `HEALTH_BITS` bit 3 |
| 7 | `FW_VERSION_STRING` | ✅ No `v` prefix |
| 8 | Sub-object names | ✅ `MSG_BATTERY`, `MSG_IPG`, `MSG_TMC`, `MSG_GNSS`, `MSG_CMC` |

---

## 6. MSG_BDC — Change Summary

| # | Change | Status |
|---|--------|--------|
| 1 | A3 frame constants + `Parse()` dispatcher | ✅ Same pattern as MSG_MCC |
| 2 | V1/V2 hardware abstraction | ✅ ICD v3.5.1 — `HEALTH_BITS` byte [10], `POWER_BITS` byte [11], `HW_REV` byte [392] |
| 3 | BDC thermistors | ✅ ICD v3.5.1 — `TEMP_RELAY` [393], `TEMP_BAT` [394], `TEMP_USB` [395] |
| 4 | HB counters | ✅ ICD v4.0.0 — `HB_NTP`/`HB_FMC_ms`/`HB_TRC_ms`/`HB_MCC_ms`/`HB_GIM_ms`/`HB_FUJI_ms`/`HB_MWIR_ms`/`HB_INCL_ms` at [396–403] |
| 5 | FMC/TRC pass-through | ✅ `MSG_TRC.ParseMsg(bdcReg1, 60)`, `MSG_FMC.ParseMsg(bdcReg1, 169)` |
| 6 | `FW_VERSION_STRING` | ✅ No `v` prefix |

---

## 7. Key Decisions — Canonical Reference

| Decision | Value |
|----------|-------|
| Namespace | `CROSSBOW` |
| Class naming | `MSG_XXX` |
| Transport entry | Single `Parse(byte[] data)` — routes internally via `TransportPath` |
| `FW_VERSION_STRING` / `SW_VERSION_STRING` | No `v` prefix — `$"{major}.{minor}.{patch}"` |
| CMD_BYTE dispatch (FW-C10) | Accept `0x00` (v4.0.0+) and `0xA1` (legacy) as valid REG1 |
| Controller client pattern | `BuildA2Frame()` / `CrcHelper.Crc16()` / `CrossbowNic.GetInternalIP()` / `KeepaliveLoop()` / `LatestMSG` |
| `isFMCEnabled`/`isFMCReady` | Legacy aliases on `MSG_BDC` — new code uses `isFSMEnabled`/`isFSMReady` |

---

## 8. Wire Format Consistency — All 5 Controllers

| Controller | IP | FW Version | A1 → | A2 | A3 | VERSION_PACK |
|------------|----|-----------|------|----|----|--------------|
| MCC | .10 | 4.0.0 | BDC | ✅ | ✅ | `VERSION_PACK(4,0,0)` |
| BDC | .20 | 4.0.0 | TRC (fire ctl) | ✅ | ✅ | `VERSION_PACK(4,0,0)` |
| TMC | .12 | 4.0.0 | MCC | ✅ | — | `VERSION_PACK(4,0,0)` |
| FMC | .23 | 4.0.0 | BDC | ✅ | — | `VERSION_PACK(4,0,0)` |
| TRC | .22 | 4.0.3 | BDC | ✅ | — | `VERSION_PACK(4,0,3)` |

**IsV4 gate:** `FW_VERSION >> 24 >= 4` — detects ICD v3.6.0 command space.
**FW-C10:** REG1 CMD_BYTE is `0x00` in v4.0.0+ (was `0xA1`). All parsers accept both.

### Sub-Register Embedding

| Sub-register | Embedded in | Payload bytes | C# class |
|--------------|-------------|---------------|----------|
| TMC REG1 | MCC REG1 | [66–129] | `MSG_TMC` |
| Battery | MCC REG1 | [34–44] | `MSG_BATTERY` |
| IPG/Laser | MCC REG1 | [45–65] | `MSG_IPG` |
| GNSS | MCC REG1 | [135–212] | `MSG_GNSS` |
| CMC/Charger | MCC REG1 | [213–244] | `MSG_CMC` |
| Gimbal | BDC REG1 | [20–58] | `MSG_GIMBAL` |
| TRC REG1 | BDC REG1 | [60–123] | `MSG_TRC` |
| FMC REG1 | BDC REG1 | [169–232] | `MSG_FMC` |

---

## 9. defines.hpp / defines.cs — Canonical State

Both at version 4.0.0 (CB-20260419b). All constants, enums, and command bytes aligned.

### Key enums

`SYSTEM_STATES`, `BDC_MODES`, `BDC_CAM_IDS`, `MCC_DEVICES`, `BDC_DEVICES`,
`BDC_TRACKERS` (incl. `LK=4` v4.1.0), `DEBUG_LEVELS`, `TMC_FAN_SPEEDS`,
`TMC_PUMP_SPEEDS`, `TMC_VICORS` (PUMP1/PUMP2), `TMC_LCM_SPEEDS`, `TMC_LCMS`,
`TMC_DAC_CHANNELS`, `AF_MODES`, `MCC_POWER`, `LASER_MODEL`, `VIEW_MODES`,
`HUD_OVERLAY_FLAGS`, `VOTE_BITS_MCC`, `VOTE_BITS_BDC`, `MWIR_RUN_STATES`,
`COCO_ENABLE_OPS` (v4.1.0), `ICD` (full command space)

### ICD v3.6.0 command space restructuring summary

| Byte | Assignment | Notes |
|------|------------|-------|
| `0xA1` | `SET_HEL_TRAINING_MODE` | Moved from `0xAF` |
| `0xAB` | `SET_FIRE_REQUESTED_VOTE` | Moved from `0xE6` |
| `0xC4` | `CMD_VIS_AWB` | Assigned CB-20260416e |
| `0xD1` | `ORIN_ACAM_COCO_ENABLE` | Moved from `0xDF` |
| `0xE0` | `SET_BCAST_FIRECONTROL_STATUS` | Moved from `0xAB` |

---

## 10. ICD Scope Labels

| Label | Meaning | Port |
|-------|---------|------|
| `INT_OPS` | Operator-accessible — THEIA and integrators | A3 / 10050 |
| `INT_ENG` | Engineering-only — ENG GUI, maintenance | A2 / 10018 |
| `RES` | Reserved — no distribution | — |
| `EXT_OPS` | External integration — HYPERION/CUE→THEIA | UDP / 15009 |

---

## 11. Document Set — Current State

| Document | Version | Status |
|----------|---------|--------|
| `ARCHITECTURE.md` (IPGD-0006) | 4.0.4 | ✅ Current — CB-20260419b |
| `CROSSBOW_ICD_INT_ENG.md` (IPGD-0003) | 4.1.0 | ✅ Current — CB-20260419 |
| `CROSSBOW_ICD_EXT_OPS.md` (IPGD-0005) | 3.3.0 | ✅ Current |
| `CROSSBOW_ICD_INT_OPS.md` (IPGD-0004) | 3.6.1 | ✅ Current — CB-20260416 |
| `CROSSBOW_CHANGELOG.md` (IPGD-0019) | 4.4.0 | ✅ Current — CB-20260419b |
| `CROSSBOW_DOCUMENT_REGISTER.md` (IPGD-0001) | 1.7.0 | ✅ Current — CB-20260419b |
| `CROSSBOW_UG_ENG_GUI_draft.md` (IPGD-0014) | 1.4.0 | ✅ Current — §4.7 TRC complete |
| `JETSON_SETUP.md` (IPGD-0020) | 2.2.1 | ✅ Current |
| `CROSSBOW_GNSS_CONFIG.md` (IPGD-0018) | 1.0.0 | ✅ Current |
| `defines.hpp` | 4.0.0 | ✅ Canonical |
| `defines.cs` | 4.0.0 | ✅ Canonical — CB-20260419b |
| `MSG_MCC.cs` | — | ✅ Deployed — HW verified |
| `MSG_BDC.cs` | — | ✅ Deployed — HW verified |
| `MSG_TMC.cs` | — | ✅ V1/V2 abstraction — HW verified |
| `MSG_FMC.cs` | — | ✅ V1/V2 abstraction — HW verified |
| `MSG_TRC.cs` | — | ✅ A2 framed Parse() + jetsonGpuLoad — CB-20260419b |
| `trc.cs` | — | ✅ Port 10018, GUI-8 compliant — verified CB-20260419b |
| `frmTRC.cs` | — | ✅ LatestMSG pattern, TMC-parallel — CB-20260419b |

---

## 12. Open Items — SW Track

| ID | Item | Priority |
|----|------|----------|
| NEW-18 | CRC cross-platform verification | ⏳ Pre-HW test |
| NEW-31 | `frmMain.cs` — `SET_LCH_VOTE` arg swap | ⏳ Fix before HW test |
| NEW-32 | `lch.cs` longitude `% 180.0` before negation | ⏳ Low |
| S19-33–37 | Word ICD / user guide build spec updates | ⏳ Queued |

---

## 13. Open Items — FW Track

| ID | Item | Priority |
|----|------|----------|
| TRC-M9 | Deprecate TRC port 5010 | Low — after sustained HW validation of port 10018 |
| FW-14 | GNSS socket bug — `RUNONCE` case 6 / `EXEC_UDP` socket | 🟡 Verify on HW |
| NEW-38d | TRC PTP integration — `ptp4l`, `TIME_BITS`, `MSG_TRC.cs` | ⏳ Pending |
| TRC-COCO-MODE1 | COCO ambient scan binary handler `0xD1` | 🟡 Medium |
| TRC-LK-UPGRADE | LK-guided MOSSE reseed | 🟡 Medium |
