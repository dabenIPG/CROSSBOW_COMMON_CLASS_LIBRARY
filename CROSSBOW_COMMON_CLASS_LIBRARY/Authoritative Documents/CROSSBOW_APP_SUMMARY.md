# CROSSBOW — Cross-Application Consistency Summary

**Document Version:** 3.0.5
**Date:** 2026-03-17 (updated session 24)
**ICD Reference:** v3.1.0
**ARCHITECTURE Reference:** v3.0.3

**Scope:** Consistency audit across all five embedded controllers (MCC, BDC, TMC, FMC, TRC)
and both C# applications (THEIA, TRC_ENG_GUI_PRESERVE). Covers namespace, naming, wire format,
parsing, logging, and transport path decisions.

---

## 1. Application Overview

| Application | Namespace | Transport | Controllers | Entry Point |
|-------------|-----------|-----------|-------------|-------------|
| **THEIA** | `CROSSBOW` | A3 / port 10050 / magic `0xCB 0x58` | MCC, BDC only | `Parse(data)` → internal `ParseA3` |
| **TRC_ENG_GUI_PRESERVE** | `CROSSBOW` | A2 / port 10018 / magic `0xCB 0x49` | All 5 controllers | `Parse(data)` → internal `ParseA2` |
| **HYPERION** | `Hyperion` | UDP:10009 (CUE output to THEIA) | External sensors only | N/A — separate stack |

Both THEIA and TRC_ENG_GUI_PRESERVE use the **shared CROSSBOW class library** (`namespace CROSSBOW`).
The shared library is the single source of truth for all message parsing.

---

## 2. Shared Class Library — Namespace and Naming

| Decision | Value | Status |
|----------|-------|--------|
| Namespace | `CROSSBOW` | ✅ Both apps updated |
| Class naming | `MSG_XXX` (e.g. `MSG_MCC`, `MSG_BDC`) | ✅ Canonical |
| File naming | `MSG_XXX.cs` | ✅ Canonical |
| THEIA old names | `MCC_MSG`, `BDC_MSG`, etc. | ✅ Shared library canonical — THEIA uses shared lib |
| ENG GUI old namespace | `CROSSBOW_ENG_GUIS` | ✅ Updated to `CROSSBOW` |

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

---

## 4. Message Class Audit — All Levels

### Level 0 — No Dependencies

| Class | Block | Size | THEIA name | ENG GUI name | Status |
|-------|-------|------|-----------|--------------|--------|
| `MSG_BATTERY` | MCC REG1 [34–44] | 11 B | `BATTERY_MSG` | `MSG_BATTERY` | ✅ Verified + copied to THEIA |
| `MSG_IPG` | MCC REG1 [45–65] | 21 B | `IPG_MSG` | `MSG_IPG` | ✅ Verified + copied to THEIA |
| `MSG_GIMBAL` | BDC REG1 [20–58] | 39 B | `GIMBAL_MSG` | `MSG_GIMBAL` | ✅ Verified + copied to THEIA |

### Level 1 — Single Dependency

| Class | Block | Size | Status |
|-------|-------|------|--------|
| `MSG_TMC` | MCC REG1 [66–129] | 64 B | ✅ Verified — copy ENG GUI → THEIA + namespace |
| `MSG_CMC` | MCC REG1 [213–244] | 32 B | ✅ Verified — copy ENG GUI → THEIA + namespace |
| `MSG_GNSS` | MCC REG1 [135–212] | 78 B | ✅ Verified — copy ENG GUI → THEIA + namespace |
| `MSG_FMC` | BDC REG1 [169–232] | 64 B | ✅ Merged + generated (session 15) |
| `MSG_TRC` | BDC REG1 [60–123] | 64 B | ✅ Verified — ENG GUI renamed from `TRC_MSG` |

### Level 2 — Multiple Dependencies (dispatchers)

| Class | Status | Notes |
|-------|--------|-------|
| `MSG_MCC` | ✅ Generated session 16 — deployed, HW verify pending | 13 changes applied. TransportPath pattern. TMC embedding [66–129] verified. |
| `MSG_BDC` | ✅ Generated session 16 — deployed, HW verify pending | 11 changes applied. TransportPath pattern. FMC [169–232], TRC [60–123] pass-through. |

---

## 5. MSG_MCC — Change Summary

Base: ENG GUI `MSG_MCC.cs` (`namespace CROSSBOW`, `public class MSG_MCC`)

| # | Change | Status |
|---|--------|--------|
| 1 | A3 frame constants | ✅ `MAGIC_HI=0xCB, FRAME_RESPONSE_LEN=521, PAYLOAD_OFFSET=7, STATUS_OK=0x00` |
| 2 | `ParseA3` (private) | ✅ A3 magic/CRC/STATUS → `ParseMSG01(frame, PAYLOAD_OFFSET+1)` |
| 3 | `ParseA2` (private) | ✅ Raw A2 payload — `if (cmd == ICD.GET_REGISTER1)` |
| 4 | `Parse()` public dispatcher | ✅ Routes to ParseA3 or ParseA2 via `TransportPath` |
| 5 | `TransportPath` + `MagicLo` | ✅ `MagicLo` computed — `0x58` A3, `0x49` A2 |
| 6 | `LastFrameStatus` property | ✅ `public byte LastFrameStatus { get; private set; } = 0xFF` |
| 7 | Remove `ParseMSG02` | ✅ GET_REGISTER2 deprecated — deleted |
| 8 | Fix `CMCMsg` call | ✅ `CMCMsg.ParseMsg()` → `CMCMsg.Parse()` |
| 9 | `SW_VERSION_STRING` | ✅ `v` prefix removed — `$"{major}.{minor}.{patch}"` |
| 10 | `dt_us > dtmax` logging | ✅ `MSG_MCC:` prefix + `if (isVerboseLogEnabled) Log?.Debug(...)` |
| 11 | Sub-object names | ✅ `MSG_BATTERY`, `MSG_IPG`, `MSG_TMC`, `MSG_GNSS`, `MSG_CMC` |
| 12 | Stale NOTE comments | ✅ Both instances removed — Battery/IPG updates complete, ICD v3.0.0 is authoritative |
| 13 | `using System` | ✅ Added |

**TMC embedding verified:** `TMCMsg.Parse(msg, ndx)` enters at payload[66] on A2 path, frame[73] on A3 path — both correct per ICD.

---

## 6. MSG_BDC — Change Summary

Base: ENG GUI `MSG_BDC.cs` (`namespace CROSSBOW`, `public class MSG_BDC`)

| # | Change | Status |
|---|--------|--------|
| 1 | A3 frame constants | ✅ Same as MSG_MCC |
| 2 | `ParseA3` (private) | ✅ Full magic/CRC/STATUS validation, liveness inside STATUS_OK block |
| 3 | `ParseA2` (private) | ✅ Raw A2 payload — `if (cmd == ICD.GET_REGISTER1)` |
| 4 | `Parse()` public dispatcher | ✅ Routes via `TransportPath` |
| 5 | `TransportPath` + `MagicLo` | ✅ Computed — `0x58` A3, `0x49` A2 |
| 6 | `LastFrameStatus` property | ✅ Set in ParseA3 only |
| 7 | `dt_us > 25000` logging | ✅ `MSG_BDC:` prefix + Serilog. `dtmax` high-water only — not reset |
| 8 | `isFMCEnabled` alias | ✅ `public bool isFMCEnabled { get { return isFSMEnabled; } }` |
| 9 | `isFMCReady` alias | ✅ `public bool isFMCReady { get { return isFSMReady; } }` |
| 10 | Sub-object names | ✅ `MSG_GIMBAL`, `MSG_TRC`, `MSG_FMC` |
| 11 | `FW_VERSION_STRING` | ✅ `v` prefix removed |
| 12 | `using System` | ✅ Added |
| 13 | ParseMSG01 header comment | ✅ Full ICD v3.0.0 BDC REG1 layout comment added |

---

## 7. Key Decisions — Canonical Reference

| Decision | Value |
|----------|-------|
| Namespace | `CROSSBOW` |
| Class naming | `MSG_XXX` |
| Transport entry | Single `Parse(byte[] data)` — routes internally via `TransportPath` |
| `TransportPath` default | `A3_External` |
| `SW_VERSION_STRING` / `FW_VERSION_STRING` | No `v` prefix — `$"{major}.{minor}.{patch}"` |
| Logging pattern | `Debug.WriteLine($"MSG_XXX: ...")` always + `if (isVerboseLogEnabled) Log?.Debug(...)` |
| `isVerboseLogEnabled` | Gates `Log?.Debug` and non-critical `Debug.WriteLine` |
| `ParseMSG02` | Deleted — GET_REGISTER2 deprecated |
| `CMCMsg` embedded call | `CMCMsg.Parse(msg, ndx)` — not `ParseMsg` |
| CMD dispatch | `if (cmd == ICD.GET_REGISTER1)` — no switch, REG2 deprecated |
| `isFMCEnabled`/`isFMCReady` | Legacy aliases on `MSG_BDC` — new code uses `isFSMEnabled`/`isFSMReady` |

---

## 8. Wire Format Consistency — All 5 Controllers

All verified at ICD v3.0.0. All controllers on `VERSION_PACK` semver encoding.

| Controller | IP | FW Version | A1 → | A2 | A3 | VERSION_PACK |
|------------|----|-----------|------|----|----|--------------|
| MCC | .10 | 3.0.1 | BDC | ✅ | ✅ | `VERSION_PACK(3,0,1)` |
| BDC | .20 | 3.0.1 | TRC (fire ctl) | ✅ | ✅ | `VERSION_PACK(3,0,1)` |
| TMC | .12 | 3.0.2 | MCC | ✅ | — | `VERSION_PACK(3,0,2)` — intentional patch |
| FMC | .23 | 3.0.1 | BDC | ✅ | — | `VERSION_PACK(3,0,1)` |
| TRC | .22 | 3.0.1 | BDC | ✅ | — | `VERSION_PACK(3,0,1)` |

VERSION_PACK unpack logic verified in both THEIA and ENG GUI (NEW-3 ✅).

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

`defines.hpp` is the canonical source for all 5 embedded controllers.
`defines.cs` is the canonical source for both C# applications.
Both are at version 3.X.Y. All constants, enums, and command bytes are aligned.

### New enums added session 15 (now canonical in both files)

`MWIR_RUN_STATES`, `BDC_TRACKERS`, `AF_MODES`, `VIEW_MODES`, `TMC_DAC_CHANNELS`,
`HUD_OVERLAY_BITS`, `VOTE_BITS_MCC`, `VOTE_BITS_BDC`

### Key corrections in canonical defines

| Item | Before | After |
|------|--------|-------|
| `TMC_PUMP_SPEEDS` | LO=800/MED=1007/HI=1200 (in BDC/MCC/FMC) | LO=350/MED=500/HI=800 (TMC authoritative) |
| `0xE0/0xE1` device 7 | Reserved | BDC |
| `SYSTEM_STATES` MAINT | `0xAA` | `0x04` |
| `SYSTEM_STATES` FAULT | `0xFF` | `0x05` |
| `0xA4` | `RES_A4` | `EXT_FRAME_PING` |
| `BDC_DEVICES::RTCLOCK=7` | Conflict | HW present, FW deprecated — enum retained |

---

## 10. ICD Scope Labels — Renamed in ICD v3.0.2 (NEW-13 ✅)

Rename applied in ICD v3.0.2 (session 17). All command tables, column headers, scope
definitions, and prose updated.

| Old | New | Meaning | Port |
|-----|-----|---------|------|
| `EXT` | `INT_OPS` | Operator-accessible — THEIA and integrators | A3 / 10050 |
| `INT` | `INT_ENG` | Engineering-only — ENG GUI, maintenance | A2 / 10018 |
| `RES` | `RES` | Reserved — no distribution | — |
| *(new)* | `EXT_OPS` | External integration — HYPERION/CUE→THEIA | UDP / 10009 |

---

## 11. Document Set — Current State

| Document | Version | Status |
|----------|---------|--------|
| `ARCHITECTURE.md` | 3.0.3 | ✅ Current — session 24 (THEIA .208, NTP direct, ICD filenames updated) |
| `CROSSBOW_ICD_INT_ENG.md` | 3.1.0 | ✅ Current — session 24 (full review and corrections) |
| `CROSSBOW_ICD_EXT_OPS.md` | 3.1.0 | ✅ Current — session 24 (full review and corrections) |
| `CROSSBOW_ICD_INT_OPS.md` | 3.1.0 | ✅ Current — session 24 (full review and corrections) |
| `ExtOpsFrame.cs` | — | ✅ Generated session 17 — shared EXT_OPS framing layer (`namespace CROSSBOW`) |
| `CueReceiver.cs` | — | ✅ Generated session 17 — THEIA UDP:10009 listener + `0xAF`/`0xAB` responder |
| `CueSender.cs` | — | ✅ Generated session 17 — integrator CUE sender + `TheiaStatus`/`TheiaPosAtt` parsers |
| `MSG_GNSS.cs` | — | ✅ Updated session 17 — `LatestPostion.alt` now HAE; `Altitude_HAE`/`Altitude_MSL` properties added |
| `THEIA_USER_GUIDE.md` | 1.0.0 | ✅ Generated session 18 — ICD ref fixed, Xbox controller section added |
| `ENG_GUI_USER_GUIDE.md` | 1.0.0 | ✅ Generated session 18 |
| `HYPERION_USER_GUIDE.md` | 1.0.0 | ✅ Generated session 18 |
| `EMPLACEMENT_GUI_USER_GUIDE.md` | 1.0.0 | ✅ Complete — session 19 (§2 Horizon Generator, §4 Platform Registration) |
| `GSTREAMER_INSTALL.md` | 3.0.0 | ✅ Current — session 16 |
| `defines.hpp` | 3.X.Y | ✅ Canonical — session 15 |
| `defines.cs` | — | ✅ Canonical — session 15 |
| `MSG_MCC.cs` | — | ✅ Generated session 16 — deployed, HW verify pending |
| `MSG_BDC.cs` | — | ✅ Generated session 16 — deployed, HW verify pending |
| `TRC_MIGRATION.md` | v4 | ✅ Current — session 15 |
| `CROSS_APP_SUMMARY.md` | 3.0.5 | ✅ This document — updated session 24 |

---

## 12. Open Items — SW Track

| ID | Item | Priority |
|----|------|----------|
| NEW-9 | `MSG_MCC.cs` — HW verify | ⏳ Deployed — HW verify pending |
| NEW-10 | `MSG_BDC.cs` — HW verify | ⏳ Deployed — HW verify pending |
| NEW-18 | CRC cross-platform verification | ⏳ Pre-HW test |
| ~~NEW-20~~ | ~~Verify UI version string — no double `v` prefix~~ | ✅ Done |
| ~~NEW-21~~ | ~~Migrate `isFMCEnabled` → `isFSMEnabled` in THEIA~~ | ✅ Done |
| ~~NEW-22~~ | ~~Update `SESSION_ACTION_ITEMS.md`~~ | ✅ Done — session 18 |
| ~~NEW-23~~ | ~~THEIA — EXT_OPS frame parser~~ | ✅ Done — `CueReceiver.cs` |
| ~~NEW-24~~ | ~~THEIA — `0xAF` status response~~ | ✅ Done — `CueReceiver.SendStatusResponse()` |
| ~~NEW-25~~ | ~~THEIA — `0xAB` POS/ATT response~~ | ✅ Done — `CueReceiver.SendPosAttReport()` |
| ~~NEW-26~~ | ~~HYPERION — EXT_OPS CUE sender~~ | ✅ Done — `CueSender.cs` |
| ~~NEW-27~~ | ~~HYPERION — `0xAF` parser~~ | ✅ Done — `TheiaStatus` with `IsFireReady` |
| ~~NEW-28~~ | ~~HYPERION — `0xAB` parser~~ | ✅ Done — `TheiaPosAtt` in `CueSender.cs` |
| NEW-29 | `EMPLACEMENT_GUI_USER_GUIDE.md` | ✅ Complete — session 19 |
| NEW-31 | `frmMain.cs` lines 3356/3376 — `SET_LCH_VOTE` arg swap | ⏳ Fix before HW test |
| NEW-33 | MCC VOTE BITS byte [3] bit 0: replace `isLaserTotalHW_Vote_rb` with `isNotBatLowVoltage()` — affects all three ICDs | ⏳ Pending FW change |
| NEW-35 | FW verify: all firmware targets NTP `.33` directly | ⏳ Pre-HW verify |
| NEW-32 | `lch.cs` longitude `% 180.0` before negation | ⏳ Low |
| S19-33 | Word ICD version realignment 1.x → 3.x.y | ⏳ Queued |
| S19-34 | Build spec three-document split + integrator tier model | ⏳ Design resolved session 19 |
| S19-35 | Build spec scope labels + new commands | ⏳ Queued |
| S19-36 | User guide Word build spec | ⏳ Queued |
| S19-37 | Merge with CROSSBOW MINI USER MANUAL v20260205 | ⏳ Queued |
| ~~NEW-30~~ | ~~`MSG_GNSS.cs` HAE fix~~ | ✅ Done — `Altitude_HAE` / `Altitude_MSL` properties added |
| ~~NEW-3~~ | ~~VERSION_PACK unpack verify~~ | ✅ Done |
| ~~NEW-12~~ | ~~TransportPath enum + constructor~~ | ✅ Done |
| ~~NEW-13~~ | ~~ICD scope label rename~~ | ✅ Done — ICD v3.0.2 |
| ~~NEW-15~~ | ~~`ICD_EXTERNAL_OPS_v3.0.1.md`~~ | ✅ Done — session 17 |
| ~~NEW-16~~ | ~~`ICD_EXTERNAL_INT_v3.0.2.md`~~ | ✅ Done — session 17 |
| ~~NEW-17~~ | ~~Three user guides (THEIA, ENG GUI, HYPERION)~~ | ✅ Done — session 18 |

---

## 13. Open Items — FW Track

| ID | Item | Priority |
|----|------|----------|
| ~~TRC-M1~~ | ~~Rewrite `TelemetryPacket` struct~~ | ✅ Done |
| ~~TRC-M5~~ | ~~Create `trc_frame.hpp`~~ | ✅ Done |
| ~~TRC-M6~~ | ~~`trc_a1.cpp` A1 TX/RX upgrade~~ | ✅ Done |
| ~~TRC-M7~~ | ~~`udp_listener.cpp` A2 framing~~ | ✅ Done — full frame parse, SEQ replay, client registry, unsolicited thread |
| ~~#5~~ | ~~`0xB7` to `EXT_CMDS_BDC[]`~~ | ✅ Done — already present in `bdc.hpp` EXT_CMDS_BDC[] |
| ~~#6~~ | ~~Fix `telemetry.h` camid comment~~ | ✅ Done — `BDC_CAM_IDS: VIS=0, MWIR=1` correct |
| ~~#7~~ | ~~FSM position reconciliation (int16 vs int32)~~ | ✅ Closed — int16 commanded (DAC range fits) and int32 readback (ADC with sign inversion) are correct distinct types. Not a bug. |
| #14 | GNSS bug — `RUNONCE` case 6 and `EXEC_UDP` use wrong socket | ⏳ HW-present — two bugs confirmed in source: case 6 uses `udpRxClient`/`PortRx` instead of `udpTxClient`/`PortTx`; `EXEC_UDP` uses `udpRxClient` instead of `udpTxClient`. Fix when in front of HW. Cosmetic: `START` error string line 36 also misnames socket. |
| ~~#37~~ | ~~`fmc.cpp` a2_seq_init=false on re-registration~~ | ✅ Done — applied in `udp_listener.cpp` SET_UNSOLICITED handler |
| ~~TRC-M8~~ | ~~`udp_listener.cpp` binary handlers for `OFFSETX`, `OFFSETY`, `AUTOMODEREGION`~~ | ⚠ Deprecated — commands require camera stop/restart, incompatible with streaming. ASCII port 5012 access retained. Binary handlers will not be implemented. |
| — | Mutex on `buildTelemetry()` race condition | Low |
| TRC-M9 | Deprecate TRC port 5010 | Low |
| ~~—~~ | ~~Old MCC bench unit HW issue (`voteBitsMcc=0x10`)~~ | ✅ Closed |
