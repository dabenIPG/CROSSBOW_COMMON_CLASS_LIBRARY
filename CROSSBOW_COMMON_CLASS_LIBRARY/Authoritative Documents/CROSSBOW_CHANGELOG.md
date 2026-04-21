# CROSSBOW — Changelog and Action Item Register

**Document:** `CROSSBOW_CHANGELOG.md`
**Doc #:** IPGD-0019
**Version:** 4.5.0
**Date:** 2026-04-19
**Status:** Current
**Supersedes:** `Embedded_Controllers_ACTION_ITEMS.md` (unregistered, retired), `Embedded_Controllers_CLOSED_ACTION_ITEMS.md` (unregistered, retired)

---

This document is the unified changelog and action item register for CROSSBOW embedded firmware, ENG GUI software, and supporting infrastructure. It supersedes the standalone action item files previously maintained as unregistered working documents.

**Parts:**
- **Part 1 — Session Log**: Narrative session-by-session summary (most recent first)
- **Part 2 — Open Items**: All open action items, priority-ordered and subsystem-grouped
- **Part 3 — Closed Items**: Full closure archive, grouped by session

Session numbers marked `~` are approximate where the exact session number is uncertain from available records.

---

# PART 1 — SESSION LOG

---

## CB-20260420 — MCC V3 hardware revision (HW_REV_V3 + LASER_xK axis)
**Files:** `hw_rev.hpp`, `pin_defs_mcc.hpp`, `defines.hpp`, `mcc.hpp`, `mcc.cpp`, `MCC.ino`, `ARCHITECTURE.md`, `CROSSBOW_ICD_INT_ENG.md`
**ICD:** vTBD (MCC REG1 HW_REV `0x03`; MCC_POWER enum renames; RELAY_NTP bit 7; HEALTH_BITS bit 4 `isLaserModelMatch`)
**ARCH:** v4.0.1 → v4.1.0

**Summary:** MCC V3 hardware revision added (PMS Controller 1.0 Rev B — monolithic PCB). Unified firmware codebase now supports V1, V2, and V3. Second compile-time axis `LASER_3K`/`LASER_6K` added to `hw_rev.hpp` to gate laser-model-dependent pin dispatch. Five `MCC_POWER` enum values renamed for naming convention alignment (bit positions unchanged). `RELAY_NTP` added at bit 7 for Phoenix NTP appliance relay (V3 only). `isLaserModelMatch` added to `HEALTH_BITS` bit 4 — compile-time vs runtime model sense. All four build targets verified clean compile: V1+3K, V2+6K, V3+3K, V3+6K.

**`hw_rev.hpp` changes:**
- `HW_REV_V3` block added — PMS Controller 1.0 Rev B (monolithic PCB).
- `MCC_HW_REV_BYTE = 0x03` for V3.
- `LASER_3K` / `LASER_6K` compile-time axis added with mutual-exclusion guards. Valid combinations: V1+LASER_3K, V2+LASER_6K, V3+either.
- V3 power pin/polarity macros: `VICOR_BUS` pin 40 `HIGH=ON` (polarity inverted vs V1 LOW=ON); `RELAY_GPS` pin 67; `RELAY_LASER` dispatches pin 54 (LASER_3K) or pin 63 PIN_ENERGIZE (LASER_6K); `VICOR_GIM` pin 55 (LASER_6K only); `VICOR_TMS` pin 51 (LASER_6K only); `SOL_HEL` pin 5; `SOL_BDA` pin 50; `RELAY_NTP` pin 56.
- Error guards: `HW_REV_V1 + LASER_6K` → compile error; `HW_REV_V2 + LASER_3K` → compile error.

**`pin_defs_mcc.hpp` changes:**
- Three-way `#if HW_REV_V1 / #elif HW_REV_V2 / #elif HW_REV_V3` guards throughout — no V1==V3 assumptions.
- V3-only pins added: `PIN_WIZ_CS(10)`, `PIN_STM_RESET(62)`, `PIN_IP178_RST(52)`, `PIN_SWITCH_USB_EN(64)`, `PIN_LOCAL_NOT_ABORT(3)`, `PIN_LOCAL_ARM(4)`, `PIN_ABORT_SW_EN(44)`, `PIN_ARM_SW_EN(46)`, `PIN_isFIRE(42)`, `PIN_MODE(43)`, `PIN_ENERGIZE(63)`, `PIN_TRIG_RB(72)`, `PIN_isFIRE_Ready(75)`, `PIN_BAT_SENSE_RB(73)`, `PIN_VICOR_TEMP2(A5)`, `PIN_TEMP_4(A4)`, `PIN_RELAY3_ENABLE(54)`.
- `PIN_ARM_NOTABORT_BDA_RB` retired on V3 (HW AND gate removed — SW AND in `CheckVotes()`).
- `PIN_PRE_TRIG_RB` retired on V3 (replaced by `PIN_TRIG_RB`).
- Redundant `BDPIN_LED_USER_5` alias removed — all five LEDs come from OpenCR variant.

**`defines.hpp` changes:**
- `MCC_POWER` enum: five renames (`GPS_RELAY→RELAY_GPS`, `LASER_RELAY→RELAY_LASER`, `GIM_VICOR→VICOR_GIM`, `TMS_VICOR→VICOR_TMS`); `RELAY_NTP=7` added.
- Inline comments updated with V1/V2/V3 pin annotations and polarity per value.
- `0xE2 PMS_POWER_ENABLE` docstring: V3 valid values added.
- `0xAF SET_CHARGER` / `CHARGE_LEVELS`: V1/V3 (DBU3200 I2C restored on V3).
- HW_REV comment block: `0x03=V3` added.
- Hardware revision guard comment updated: `!defined(HW_REV_V2)` convention noted as DEF-2 risk.

**`mcc.hpp` changes:**
- `isCRG_Ready()`: V3 branch added — `dbu.isConnected()` same as V1.
- `HB_CRG()`: V3 branch added — `dbu.HB_ms()` same as V1.
- `chargeLevel` field: V3 guard added (V1/V3 — I2C charger present).
- `dbu` include guard: `#if defined(HW_REV_V1) || defined(HW_REV_V3)`.
- Power flag names updated: `isPwr_GpsRelay→isPwr_RelayGps` etc. (all seven flags renamed to match enum).
- `isPwr_RelayNtp` added (bit 7).
- `POWER_BITS()`: bit 7 `RELAY_NTP` added.

**`mcc.cpp` changes:**
- `EnablePower()`: `#elif defined(HW_REV_V3)` case block added. V3-3kW: RELAY_GPS(67), VICOR_BUS(40), RELAY_LASER(54), SOL_HEL(5), SOL_BDA(50) — sets `isBDC_Ready`. V3-6kW: RELAY_GPS(67), VICOR_BUS(40), RELAY_LASER→PIN_ENERGIZE(63), VICOR_GIM(55) — sets `isBDC_Ready`, VICOR_TMS(51), RELAY_NTP(56).
- `StateManager()`: V3 power-on/off sequences added for both LASER_3K and LASER_6K. LASER_6K boot order: VICOR_TMS first (board power), then VICOR_GIM, then RELAY_LASER (PIN_ENERGIZE). LASER_3K mirrors V1 with updated pin names.
- `CheckVotes()`: V3 branch — `isLaserTotalHW_Vote_rb` computed as SW AND of `digitalRead(PIN_SAFETY_ISNOTABORT)`, `digitalRead(PIN_SAFETY_ISARMED)`, `digitalRead(PIN_SAFETY_ISBDAVOTE)`. HW AND gate (`PIN_ARM_NOTABORT_BDA_RB`) absent on V3.
- `dbu.INIT()` / `dbu.UPDATE()` guards: `defined(HW_REV_V1) || defined(HW_REV_V3)`.
- `SEND_REG_01()` charger block: V3 branch same as V1 (DBU3200 fields).
- Serial debug strings: all `MCC_POWER` enum name references updated to new names.
- `PIN_CRG_OK` `digitalRead` block: V2 only guard confirmed.

**`ARCHITECTURE.md` changes (v4.1.0):**
- §9 MCC header: V3 + `LASER_xK` axis documented.
- §9.1 hardware variants table: V1/V2/V3 three-column with V2 pin-reuse warning.
- §9.1a (new): PMS Power Flow — full power flow diagrams for V1, V2, V3, V3*, V4 planning. `MCC_POWER` enum cross-revision table.
- §9.6: Two-axis build config (HW_REV + LASER_xK), valid combinations matrix, updated polarity macros.
- §3.1: HW_REV_V3 row added; MCC `IsV3` C# pattern; HW_REV byte `0x03`; bring-up note for V3 VICOR_BUS polarity.
- §16: MCC compat matrix entry updated.

**Items closed:** none
**Items opened:**
| ID | Item | Priority |
|----|------|----------|
| DEF-2 | `TMC_VICORS` / `TMC_DAC_CHANNELS` / `TMC_PUMP_SPEEDS` in `defines.hpp` guarded by `!defined(HW_REV_V2)` — **silent wrong-enum failure on mixed-revision fleets.** Concrete failure: MCC V3 build sets `-DHW_REV_V3` (not `HW_REV_V2`), so `!defined(HW_REV_V2)` is true — MCC gets `TMC_VICORS::PUMP=2` and `TMC_VICORS::HEAT=3`. If the fleet is running TMC V2 hardware, `HEAT=3` is a gap/invalid on TMC V2 (intentional gap to prevent misrouting). MCC sends `TMS_SET_VICOR_ENABLE(HEAT, 1)` and TMC V2 silently rejects or misroutes it — no compile error, no link error, no runtime assert. Symmetric risk: MCC V2 build + TMC V1 fleet loses `HEAT` visibility entirely. **Fix:** move `TMC_VICORS`, `TMC_DAC_CHANNELS`, `TMC_PUMP_SPEEDS` out of `defines.hpp` into `tmc.hpp`, guarded by controller-scoped `TMC_HW_REV_V1` / `TMC_HW_REV_V2`. `defines.hpp` retains only the command bytes (`0xE8`, `0xE9`). C# `defines.cs` must be updated in parallel — same three enums, same controller-scoped guard logic. MCC firmware forwards raw `uint8` payload and never interprets the enum values directly, so MCC translation unit does not need the enums post-fix. | 🔴 High |
| FW-MCC-V3-ISR | Vote ISR `attachInterrupt()` calls never implemented in `MCC.ino` — ISR functions exist (`abortVote_ISR`, `armVote_ISR`, `bdaVote_ISR`, `hwVote_ISR`, `fireVote_ISR`) but are dead code. `CheckVotes()` polling in `UPDATE()` is the active readback mechanism (~20ms worst-case latency). **Note: hardware AND gates handle actual fire control votes in real time — firmware readback is telemetry only, 20ms latency is not a safety concern.** V3 bring-up showed interrupt triggering issues — suspected hardware issue. Revisit with scope on readback pins (ISNOTABORT=2, ISARMED=7, ISBDAVOTE=8, FIREVOTE=41) to confirm signal levels and edge behaviour before attaching interrupts. Do not attach until HW verified. | 🟡 Medium |
| ICD-MCC-V3 | ICD version bump for MCC V3 changes: HW_REV `0x03`, MCC_POWER enum renames, RELAY_NTP bit 7 promotion, `isLaserModelMatch` HEALTH_BITS bit 4. Tagged vTBD pending bump decision. | 🟡 Medium |
| ARCH-MCC-V3 | ARCHITECTURE.md version bump to v4.1.0 for MCC V3 section additions. | 🟡 Medium |
| FW-MCC-LASER-MATCH | `isLaserModelMatch` HEALTH_BITS bit 4 added — compile-time `LASER_xK` vs runtime `ipg.LASER_MODEL_BITS()` sense. `false` until laser connects and model confirmed. Combination with `isHEL_Ready` (DEVICE_READY bit 2) gives full diagnostic: both false = not up; both true = nominal; HEL ready + match false = config error. Pending `MSG_MCC.cs` `pb_LaserModelMatch` property and `frmMCC` indicator. | 🟡 Medium |

---

## CB-20260421 — ENG GUI MCC V3 hardware revision support
**Files:** `defines.cs`, `MSG_MCC.cs`, `frmMCC.cs`, `frmMCC_Designer.cs`
**ICD:** no change — wire protocol unchanged, C# implementation only
**ARCH:** no change

**Summary:** ENG GUI updated to support MCC V3 hardware revision. Power control layout redesigned with logical abstraction (Gimbal Power, Relay Bus, Laser HV Sol) replacing raw hardware names. All four build targets supported: V1·48V·3kW, V2·300V·6kW, V3·48V·3kW, V3·300V·6kW.

**`defines.cs` changes:**
- `CHARGE_LEVELS`: `DISABLE→OFF` to align with firmware `CHARGE_LEVELS::OFF`; all four values annotated per revision (V1/V3 vs V2)
- `MCC_POWER` enum: five renames (`GPS_RELAY→RELAY_GPS`, `LASER_RELAY→RELAY_LASER`, `GIM_VICOR→VICOR_GIM`, `TMS_VICOR→VICOR_TMS`); `RELAY_NTP=7` added; full V1/V2/V3 pin and opto annotations
- `SET_CHARGER` docstring: V1/V3 (DBU3200 I2C) vs V2 (GPIO only) clarified
- `RES_E4`/`RES_EC` retired notices: updated to new enum names

**`MSG_MCC.cs` changes:**
- `HW_REV` comment: `0x03=V3` added
- `IsV3` property added (`HW_REV == 0x03`)
- `HW_REV_Label`: V3 case added with voltage/laser annotation
- `pb_*` renames: `pb_GpsRelay→pb_RelayGps`, `pb_LaserRelay→pb_RelayLaser`, `pb_GimVicor→pb_VicorGim`, `pb_TmsVicor→pb_VicorTms`; old names retained as backward-compat aliases
- `pb_RelayNtp` added (PowerBits bit 7, V3 only)
- `isLaserModelMatch` added (HealthBits bit 4)
- `isLaserPowerBus_Enabled` compat alias: updated to `RELAY_LASER`
- Compat aliases `isVicor_Enabled`, `isRelay1_Enabled`, `isRelay2_Enabled` updated to new enum names
- Header comments for byte [9] and [10] updated

**`frmMCC.cs` changes:**
- `ApplyHwRevLayout()`: full three-way V1/V2/V3 guard; V3·6kW detected from `pb_VicorGim`
- Power control logical abstraction:
  - `chk_Solenoid1` = Laser HV Sol (`SOL_HEL`) — V1/V3·3kW only
  - `chk_Solenoid2` = Gimbal Power (`SOL_BDA` on V1/V3·3kW, `VICOR_GIM` on V2/V3·6kW) — all revisions
  - `chk_Vicor` = Relay Bus (`VICOR_BUS`) — V1/V3·3kW only; hidden V2/V3·6kW
  - `chk_Relay1` = GPS Relay (`RELAY_GPS`) — V1/V3; hidden V2
  - `chk_Relay2` = Laser Relay (`RELAY_LASER`) — all revisions
  - `chk_Relay3` = TMS Vicor (`VICOR_TMS`) — V2/V3·6kW only
  - `chk_Relay4` = NTP Relay (`RELAY_NTP`) — V3 only; unhidden
- `chk_Solenoid2` handler: dispatches `SOL_BDA` or `VICOR_GIM` per revision
- `chk_Vicor` handler: always `VICOR_BUS` (control hidden when not applicable)
- `chk_Relay4` handler added: `RELAY_NTP`
- All `pb_*` references updated to new names
- `CHARGE_LEVELS.DISABLE` → `CHARGE_LEVELS.OFF`
- `mb_LasPow_Enabled_rb`: updated to `pb_RelayLaser`
- Status readback block: `mb_Relay4_Enabled_rb` now shows `pb_RelayNtp`; gimbal power readback revision-aware

**`frmMCC_Designer.cs` changes:**
- Checkbox labels updated: `Solenoid 1 (HEL)→Laser HV Sol`, `Solenoid 2 (BDA)→Gimbal Power`, `VICOR→Relay Bus`, `Relay 1 (GPS)→GPS Relay`, `Relay 2 (HEL)→Laser Relay`, `Relay 3 (TMS)→TMS Vicor`, `Relay 4 (RES)→NTP Relay`
- `chk_Relay4_Enable.CheckedChanged` wired to `chk_Relay4_Enable_CheckedChanged`

**Items closed:** none
**Items opened:** none — pending compile verify

---


**Files:** `telemetry.h`, `compositor.h`, `udp_listener.h`, `main.cpp`, `udp_listener.cpp`, `osd.cpp`, `osd.h`, `compositor.cpp`, `MSG_TRC.cs`, `frmTRC.cs`
**ICD:** v4.1.0 → v4.2.0

**Root cause fix — `readJetsonGpuLoad()` sysfs path:**
Path was `/sys/devices/gpu.0/load` (missing `platform/`). Corrected to `/sys/devices/platform/gpu.0/load`. Verified returning 196–197 (÷10 = ~19.6–19.7%) on live hardware. Silent failure mode (returns 0) confirmed benign for downstream consumers.

**Jetson health fields compacted int16 → uint8 (ICD v4.2.0):**
All four Jetson health values fit in uint8 (temps 0–95°C, loads 0–100%). Saves 4 bytes, frees space for `jetsonGpuTemp`. New layout:
- `jetsonTemp`    int16 [45–46] → uint8 [45]
- `jetsonCpuLoad` int16 [47–48] → uint8 [46]
- `jetsonGpuLoad` int16 [57–58] → uint8 [47] (moved adjacent to other health fields)
- `jetsonGpuTemp` uint8 [48] — **NEW** — GPU temp °C from `thermal_zone1` (millidegrees ÷ 1000)
- `som_serial`    uint64 [49–56] — unchanged
- RESERVED expands [5] → [7] bytes at [57–63]
Static asserts updated and verified. `make clean && make` required on TRC binary.

**`main.cpp`:**
- `readJetsonGpuLoad()`: sysfs path corrected to `/sys/devices/platform/gpu.0/load`
- `readJetsonGpuTemp()` added: reads `/sys/devices/virtual/thermal/thermal_zone1/temp`, millidegrees ÷ 1000
- `statsThreadFunc`: `jetsonGpuTemp` stored every 5s alongside GPU load
- `udp.jetsonGpuTemp` wired to `compositor.jetsonGpuTemp`

**`udp_listener.cpp` — `buildTelemetry()`:**
- All three existing casts updated `(int16_t)` → `(uint8_t)`
- `jetsonGpuTemp` pack added with null guard

**`osd.cpp` / `osd.h`:**
- `render()` and `drawText()` signatures gain `int jetsonGpuTemp`
- Bottom-right OSD block restructured from 2 rows → 3 rows:
  - Row 1 (SN, DIM_GREY) — moved up to `frame.rows - MARGIN_X - LINE_H * 2`
  - Row 2: `CPU: XXX%  XX°C`
  - Row 3: `GPU: XXX%  XX°C`
- Each row drawn as 3 separate `drawOutlinedText` calls for per-value colour:
  - Load colour: GREEN <60% / YELLOW 60–84% / RED ≥85%
  - Temp colour: GREEN <60°C / YELLOW 60–79°C / RED ≥80°C
  - Label always WHITE
- Right-alignment anchored on fixed-width format string; segments advance by `getTextSize`

**`compositor.cpp`:** `OSD::render()` call passes `jetsonGpuTemp.load()`

**`MSG_TRC.cs`:**
- `jetsonTemp`/`jetsonCpuLoad`/`jetsonGpuLoad` `Int16` → `byte`
- `jetsonGpuTemp` byte property added
- `ParseMsg`: three `BitConverter.ToInt16` reads → single-byte `rxBuff[ndx]` reads; `ndx += 5` → `ndx += 7` for RESERVED
- Header layout comment updated

**`frmTRC.cs`:** `lbl_TMC_mcuTemp` now shows `CPU 000°C   GPU 000°C` on one line

**Items closed:** none
**Items opened:** none

---


**Files:** `trc.cs`, `MSG_TRC.cs`, `frmTRC.cs`, `defines.cs`, `ARCHITECTURE.md`, `CROSSBOW_ICD_INT_ENG.md`
**ICD:** v4.1.0 (no change) | **ARCH:** v4.0.1 → v4.0.2

**`trc.cs` — full rewrite to A2 standardised client model (GUI-8):**
- Port `5010` (legacy raw) → `10018` (A2 engineering port)
- Full INT framing: `BuildA2Frame()` with `CrcHelper.Crc16()`, magic `0xCB 0x49`, rolling SEQ
- `CrossbowNic.GetInternalIP()` NIC binding — pins to internal NIC so TRC firmware accepts source address
- Single `0xA4 FRAME_KEEPALIVE` registration on connect (replaces raw `0xA0` send)
- `KeepaliveLoop()` — `PeriodicTimer` 30s, stale detection 2s, drop counting
- `isConnected` now driven from received frames (not socket open)
- `_wasConnected`, `_connectedSince`, `ConnectedSince`, `DropCount`, `HB_RX_ms` added — matches `tmc.cs` pattern
- `LatestMSG` (`MSG_TRC`) added — all telemetry reads from here; `System_State`/`BDC_Mode` forwarded
- All command methods now use `BuildA2Frame()`: `SetSystemState`, `SetGimbalMode`, `SetActiveCamera`, `SetTrackerEnable`, `SetFireStatus`, `SetOverlayMask`, `setTrackBox`, `SetTrackGateSize`, `TriggerAWB`, `CueFlag`, `UnsolicitedMode`, `SetNtpConfig`
- `SetTrackerEnable` overload with `mosseReseed` 3rd byte (ICD v4.1.0 `0xDB`)
- `SetFireStatus(byte voteBitsMcc, byte voteBitsBdc=0)` — corrected to two-byte payload per ICD `0xE0`
- `setTrackBox` restored using `0xD7 ORIN_ACAM_SET_TRACKGATE_CENTER` (was dead/commented with stale enum)
- `HUD_Overlays` bool removed; `SetOverlayMask(byte mask)` is now the sole overlay interface
- `VERSION` stale class removed; `ipEndPoint` dead field removed
- **GUI-8 verified on live HW CB-20260419**

**`MSG_TRC.cs` — A2 framed entry point + ICD v4.0.3 additions:**
- Frame constants added: `MAGIC_HI/LO`, `FRAME_RESPONSE_LEN=521`, `PAYLOAD_OFFSET=7`, `STATUS_OK`
- `Parse(byte[] frame)` added — validates magic, length, CRC-16/CCITT, STATUS byte, CMD_BYTE routing; calls `ParseMsg` internally. Matches `MSG_TMC.Parse()` pattern.
- `LastFrameStatus` property added
- `jetsonGpuLoad` int16 field added at [57–58] — ICD v4.0.3 (CB-20260419). `ParseMsg` updated: RESERVED skip corrected 7→5, `jetsonGpuLoad` parsed at [57–58]
- `FW_VERSION_STRING` alias added (matches `frmTMC` binding pattern)
- `HW_REV_Label` returns `"--"` (TRC has no hardware revision variants)
- `epochTime` → `ntpTime` alias; `activeTimeSourceLabel` → `"NTP"` fixed (PTP pending NEW-38d)
- `SomSerialLabel` display helper — `"N/A"` on parse failure

**`frmTRC.cs` — full update to `LatestMSG` pattern:**
- All telemetry reads migrated from direct `aTRC.*` fields to `aTRC.LatestMSG.*`
- Full TMC-pattern timing display: dt/HB rolling stats (EMA α=0.10), RX staleness (500ms threshold), gap counter, uptime, drop counter — all matching `frmTMC.cs`
- Connect/disconnect handler corrected: `timer1.Enabled` now toggled, state reset on disconnect
- `btn_TRC_resetMaxStats_Click` and `btn_SetNTP_Servers_Click` handlers added (were missing)
- TIME group populated: NTP time, delta UTC, PTP meatballs hardcoded Grey (no PTP on TRC)
- `tss_HW_REV` shows version + SOM serial (replaces stale `"--"` HW_REV)
- `tssCPUTemp` shows Jetson CPU temp + GPU load from `LatestMSG.jetsonTemp`/`jetsonGpuLoad`
- Overlay toggle uses `HUD_OVERLAY_FLAGS.All`/`None`
- `chkFire` sends `0xFF`/`0x00` (was `255`/`0` — same value, now explicit)

**`defines.cs` — 8 surgical changes:**
- Header: app name `TRC3_ENG_GUI` → `CROSSBOW_ENG_GUIS`; hpp version ref `v3.X.Y` → `v4.0.0`
- `BDC_TRACKERS`: `LK = 4` added — fully implemented per ICD v4.1.0 (CB-20260419)
- `COCO_ENABLE_OPS` enum added (8 sub-ops: OFF/ON/AMBIENT/TRACK/NEXT/PREV/RESET/DRIFT/INTERVAL)
- `LASER_MODEL.YLM_6K` comment corrected: `YLR-6000` → `YLM-6000-U3-SM`
- `SET_CHARGER (0xAF)` comment: V2 GPIO enable now supported (FW-CRG-V2 CB-20260416)
- `ORIN_ACAM_COCO_ENABLE (0xD1)` comment: updated with dual-mode description and `COCO_ENABLE_OPS` reference
- `ORIN_ACAM_ENABLE_TRACKERS (0xDB)` comment: updated with 3rd byte `mosseReseed` description
- `RES_FD`/`FMC_SET_STAGE_ENABLE` ordering: corrected to match `defines.hpp` (0xFD before 0xFE)

**ARCHITECTURE.md + CROSSBOW_ICD_INT_ENG.md — naming sweep:**
- `TRC_ENG_GUI_PRESERVE` → `CROSSBOW_ENG_GUIS` throughout both documents (6 occurrences ARCH, 1 ICD)
- ARCH §3 codebase inventory row expanded with full child form inventory

**Items closed this session:** GUI-6, GUI-8, TRC-CS-DEAD-IPENDPOINT
**Items opened this session:** none

---


**Files:** `CROSSBOW_ICD_INT_ENG.md`, `CROSSBOW_ICD_INT_OPS.md`, `ARCHITECTURE.md`, `CROSSBOW_DOCUMENT_REGISTER.md`, `CROSSBOW_CHANGELOG.md`

**Item register closures and deletions:**
- GUI-7 ✅ closed — HB and status timing audit complete and verified on live HW
- TRC-SN-LABEL ✅ closed — SOM serial on TRC OSD (CB-20260413); dropped from THEIA scope
- DEPLOY-3 ✅ closed — sustained bench test complete, all five controllers simultaneous
- DEPLOY-5 ✅ closed — NovAtel GNSS PTP configuration documented in IPGD-0018, verified on bench unit
- DEPLOY-6 ✅ closed — IGMP snooping verified on production switch, no issues
- TOOLING-1 🚫 deleted — defines.hpp→defines.cs auto-sync generator will not be implemented
- IPG-SENTINEL 🚫 deleted — ipg.hpp sentinel value cleanup deferred indefinitely

**ICD INT_ENG (IPGD-0003) — content edits:**
- `0xAF SET_CHARGER`: description corrected — V2 now GPIO enable only (not full rejection). Reflects FW-CRG-V2 fix CB-20260416.
- `0xC4`: `RES_C4` → `CMD_VIS_AWB` — trigger VIS auto white balance once, no payload. Reflects CB-20260416e AWB-ENG implementation.
- Version history entry added for CB-20260416 sessions.

**ICD INT_OPS (IPGD-0004) — content edits:**
- `0xAF SET_CHARGER`: same V2 description correction.
- `0xC4 CMD_VIS_AWB` added — INT_OPS accessible via A3 (in EXT_CMDS_BDC[] whitelist).
- Version history entry added.

**ARCHITECTURE.md (IPGD-0006) — content edits:**
- §17: reference updated from retired `Embedded_Controllers_ACTION_ITEMS.md` → IPGD-0019.
- FW-B4, FW-B5, DOC-2 marked closed in §17 table.

**Document Register (IPGD-0001) — version table updates:**
- IPGD-0003: 3.6.0 → 4.0.0 | IPGD-0006: 3.3.9 → 4.0.1 | IPGD-0019: 1.3.2 → 4.2.0
- IPGD-0020 and IPGD-0021 added to index. Self-referential entry bumped to v1.6.0.

---

## CB-20260416b — BDC tracker PID blind to track position (FW-C10 regression)
**Files:** `trc.cpp`

**Root cause:** `trc.cpp::UPDATE()` gated all buffer parsing on `buffer[0] == 0xA1`. Per FW-C10, `TelemetryPacket.cmd_byte` is now `0x00` on all controllers fleet-wide. The gate therefore never passed on current firmware, leaving `TrackPointX`, `TrackPointY`, and `isTrackBValid` at their boot defaults (0, 0, false) indefinitely.

**Symptom:** Two separate paths exist for `trc.buffer`:
- **Outbound REG1** (`handleA1Frame()` → `memcpy(buf+60, trc.buffer, 64)`) — always ran correctly. ENG GUI and THEIA displayed correct TRC telemetry including track position.
- **PID input** (`trc.UPDATE()` → typed field extraction) — silently skipped every frame. `PidUpdate()` always received stale zeros, driving a fixed large error (`0 - 640` pan, `0 - 360` tilt) regardless of actual target position.

**Fix — `trc.cpp` line 40:** dual-check matching FW-C10 pattern already used in `bdc.cpp` line 430 and `MSG_BDC.cs`:

```cpp
// BEFORE
    if (buffer[0] == 0xA1)

// AFTER
    if (buffer[0] == 0x00 || buffer[0] == 0xA1)
```

Comment updated to document FW-C10 dual-check rationale.

**Items closed:** TRC-PID-BLIND (opened and closed this session)
**Items opened:** none

---

## CB-20260416 — THEIA HMI IBIT audit + MCC charger V2 fix
**ARCH:** v4.0.1 (no change) | **Files:** `frmMain.cs`, `mcc.cpp`

Full audit of THEIA HMI (`frmMain.cs`) against ENG GUI reference (`frmMCC.cs`, `frmBDC.cs`) and MSG classes (`MSG_MCC.cs`, `MSG_BDC.cs`, `MSG_TMC.cs`). Surgical updates applied to `frmMain.cs`. One firmware bug identified and fix specified for `mcc.cpp`.

**frmMain.cs changes:**

*MCC Power bits — V1/V2 aware:*
Solenoid indicators (`mb_Solenoid1/2_Enabled_rb`) now read `pb_SolHel`/`pb_SolBda` directly and grey when N/A on V2. GNSS relay greyed on V2 (GNSS always powered). TMS Vicor (`mb_MCC_RelayTMC_Enabled_rb`) and GIM Vicor (`mb_MCC_RelayBDC_Enabled_rb`) greyed on V1. HEL relay (`mb_MCC_RelayHEL_Enabled_rb`) uses 3-state logic on V1: Green=relay+solenoid both on, Yellow=either on, Red=both off; V2: Green/Red on relay only. Color convention established fleet-wide: Grey=N/A, Green=good, Yellow=partial, Red=off-when-applicable.

*MCC + BDC device matrix — HB counters in ready labels:*
All `mb_MCC_Dev_Ready_*` and `mb_BDC_Dev_Ready_*` labels updated to carry device name + HB in `.Text` (e.g. `"HEL  025ms"`). BDC stale sub-message HBs (`fmcMSG.HB_ms`, `trcMSG.HB_ms`) replaced with BDC firmware HB counters from REG1 [396–403] (CB-20260413d). PTP device rows added to both MCC and BDC device matrices (`mb_MCC_Dev_Enabled/Ready_PTP`, `mb_BDC_Dev_Enabled/Ready_PTP`).

*BDC power bits — uncommented and cleaned:*
`mb_BDC_Relay1–4Enabled_rb` uncommented, stale checkbox dependency logic removed, simple Green/Grey pattern applied. `mb_BDC_Relay4Enabled_rb` added (FMC power). Relay load map: Relay1=MWIR, Relay2=VIS, Relay3=TRC, Relay4=FMC. No V1/V2 visibility toggling needed — all four relays unchanged both revisions.

*Version + temp labels (`mb_PingStatus_*`):*
All five controllers updated to fixed-width format `"NODE vX.Y.Z Vn  00C"`. MCU temp appended for MCC/TMC/BDC/FMC; Jetson temp for TRC. HW_REV shown as `V1`/`V2`/`--`. TRC has no HW rev so shows `--`. All temps clamped 0–99, 2-digit integer, no degree symbol. Font should be Courier New for column alignment. `MSG_TMC.cs` confirmed to already expose `HW_REV`, `IsV1`, `IsV2`, `HW_REV_Label` — no change needed.

*Training mode:*
`jtoggle_TRAIN_CheckedChanged` wired to `aCB.aMCC.SetHELTrainingMode()`. `mb_isTrainingModeEnabled_rb` added — Yellow if training, Grey if not. Toggle drives command only; readback is independent via meatball to avoid re-sending on every tick.

*HEL power display:*
`tss_status_hel_power` updated to `"sssss/mmmmm W"` format (5-digit fixed width): setting/max when not firing, actual/max when EMON active. `lg_mcc_batt_asoc` removed — was incorrectly wired to `IPGMsg.SetPoint` (laser setpoint %), not battery SOC.

**mcc.cpp — FW-CRG-V2 (⏳ pending flash):**
`SET_CHARGER` (0xAF) V2 `#elif` branch incorrectly calls `STATUS_CMD_REJECTED` for any `level > 0`. Fix: replace rejection with `EnableCharger(true)` — GPIO charger enable path works on V2, only I2C level control is absent. Stale comments updated in `mcc.cpp` line 715 and `mcc.cs` line 397. Separate hardware issue opened: V2 charger opto sticking (HW-CRG-V2-OPTO — under investigation in parallel session).

**Items closed this session:** IPG-HB-HEL-2, MSG-TMC-HWREV (already existed in MSG_TMC.cs), THEIA-MCC-1, THEIA-MCC-2, THEIA-MCC-3, THEIA-MCC-4 (training mode wired), THEIA-MCC-5 (covered by existing displays), THEIA-MCC-6 (folded into ping labels), THEIA-BDC-1, THEIA-BDC-2, THEIA-BDC-3, THEIA-BDC-4 (covered by tssStatus2 vote displays), THEIA-BDC-5 (folded into ping labels), THEIA-BDC-6, THEIA-HUD-LASERMODEL (implicit in power display), THEIA-HEL-POWER (closed same session)

**Items opened this session:** FW-CRG-V2, HW-CRG-V2-OPTO, THEIA-HUD-FIRECONTROL

---

## CB-20260413g — INFO command cleanup fleet-wide
**Files:** `mcc.cpp`, `bdc.cpp`, `tmc.cpp`, `fmc.cpp` (one line each)

**IP + LINK combined on one line** — all four controllers. Previously IP and Link were on two separate lines with wide padding. Consolidated to a single line using `Serial.print(Ethernet.localIP())` (library-formatted, no manual octet indexing) followed by inline LINK status.

Output now reads:
```
IP: 192.168.1.xx  LINK: UP
```

**HW_REV in INFO — fleet verified closed:**
- MCC ✅ — inline on version line (existing)
- BDC ✅ — added this session (CB-20260413f)
- TMC ✅ — inline on version line (confirmed from source)
- FMC ✅ — inline on version line (existing)
`FW-INFO-HW-REV` closed.

**Changes:**

| Controller | File | Lines | Notes |
|---|---|---|---|
| MCC | `mcc.cpp` | 1136–1137 | `Serial.print` / `Serial.printf` → single line |
| BDC | `bdc.cpp` | 1985–1986 | `Serial.print` / `Serial.printf` → single line |
| TMC | `tmc.cpp` | 945–946 | `Serial.print` / `Serial.printf` → single line |
| FMC | `fmc.cpp` | 884–885 | `FMC_SERIAL.print` / `uprintf` → single line |

**Items closed:** FW-INFO-HW-REV
**Items opened:** none

---

## CB-20260413f — FMC V1 stage+FSM debug + BDC INFO HW_REV gap
**ARCH:** v4.0.1 (no change) | **Files:** `bdc.cpp` (one line pending)

**FMC V1 stage+FSM readback investigation:** FSM returns 0,0 when both stage and FSM are connected on V1 (SAMD21). Verbose debug confirmed stage I2C is NOT blocking — returns 32 bytes cleanly at ~14998–14999 counts, stage healthy. FSM SPI runs immediately after but returns 0,0. `FSMPOS` serial command also returns 0,0 confirming the SPI read itself is the problem, not scheduling. `isFSM_Powered=true` confirmed. Hypothesis: **merge regression in `hw_rev.hpp`** — if `HW_REV_V2` was compiled into V1 hardware, `FMC_SPI` resolves to `SPI_IMU` (STM32 peripheral, non-existent on SAMD21) and all ADC reads return 0. **Diagnostic for next session:** run `INFO` on FMC serial and check `HW_REV=0x__` at boot — if `0x02` on a SAMD21 board, wrong hw_rev.hpp selected at compile time. Tracked as FMC-V1-FSM-0.

**BDC INFO missing HW_REV:** `INFO` command in `bdc.cpp` does not print `HW_REV`. MCC and FMC both include it. BDC has it in `REG` and `STATUS` but not `INFO`. Fix: one `Serial.printf` line after the version line. Tracked as FW-INFO-HW-REV. TMC source not available to verify — needs check.

**Items opened:** FMC-V1-FSM-0, FW-INFO-HW-REV
**Items closed:** none

---

## CB-20260413e — HB live HW observations + TRC SOM SN on frmBDC
**ARCH:** v4.0.1 (no change) | **Files:** `frmBDC.cs` (one line)

**TRC SOM SN on frmBDC:** `tss_trc_version` label updated to append SOM serial number — `frmBDC.cs` line 374. No designer change. Temporary pending a proper `tss_trc_sn` ToolStripStatusLabel when confirmed working on HW.

**Live HW validation — MCC HB counters:**

| HB | Observed | Assessment |
|---|---|---|
| BAT [132] | ~100ms | ✅ Expected — RS485 poll TICK = 100ms |
| CRG [133] | 255ms saturated | ✅ Expected — charger off, no I2C responses, correctly saturates |
| HEL [131] | 0ms | ❌ Still wrong — `ipg.HB_ms()` not updating. Check `ipg.isConnected` / `ipg.isInit` via serial STATUS to confirm TCP state. Root cause: `lastMsgRx_ms` may not be stamped if laser TCP not connected or `parseLine()` not being called. See IPG-HB-HEL-2. |
| NTP [130] | ~10s | ✅ Real — `NTP_TICK_MS = 10000`, 10s sync interval confirmed |
| GNSS [134] | 0–255ms | ✅ Expected — NovAtel streams 1–12Hz, faster messages show low values, slower saturate at 255ms |
| BDC | 0 | ✅ Correct — MCC does not receive A1 from BDC |

**Live HW validation — BDC HB counters:**

| HB | Observed | Assessment |
|---|---|---|
| GIM [400] | ~10ms | ✅ Expected — Galil data records ~125Hz |
| TRC [398] | ~10ms | ✅ Expected — TRC A1 at 100Hz |
| VIS/FUJI [401] | ~20ms | ✅ Expected — Fuji fast poll tier 30ms |
| MWIR [402] | 10–100ms | ✅ Expected — fast tier 50ms / slow tier 500ms |
| NTP [396] | ~10s | ✅ Real — same NTP_TICK_MS = 10000 |
| FMC [397] | 1–20ms | ✅ Expected — FMC A1 at 50Hz |
| INCL [403] | up to 255ms | ✅ Correct but saturates — INCL polls at ~1001ms, always saturates uint8. Consider x0.1s scale (÷100 at pack) to give useful 0–25.5s range. See INCL-HB-SCALE. |
| MCC [399] | — | Not validated this session |

**Items opened:** IPG-HB-HEL-2, INCL-HB-SCALE, TRC-SN-LABEL
**Items closed:** none

---

## CB-20260413d — BDC HB subsystem wiring
**ARCH:** v4.0.1 | **ICD:** BDC REG1 [396–403] new rows — folded into ICD-1 scope | **Files:** `gimbal.hpp`, `fuji.hpp`, `mwir.hpp`, `incl.hpp`, `bdc.hpp`, `bdc.cpp`, `MSG_BDC.cs`, `frmBDC.cs`, `frmBDC_Designer.cs`

**Pattern:** Same compute-in-getter uint8 raw ms pattern established in CB-20260413c for MCC, now applied to BDC. All seven BDC subsystems wired. NTP follows same x0.1s exception as MCC.

**Timestamp verification — all confirmed correct last-heard-from:**

| Subsystem | Source timestamp | Stamped in | Rate |
|---|---|---|---|
| FMC | `a1_fmc_last_ms` (`bdc.hpp`) | `bdc.cpp` handleA1Frame on FMC A1 RX | 50 Hz |
| TRC | `a1_trc_last_ms` (`bdc.hpp`) | `bdc.cpp` handleA1Frame on TRC A1 RX | 100 Hz |
| MCC | `a1_mcc_last_ms` (`bdc.hpp`) | `bdc.cpp` on MCC 0xAB fire control broadcast RX | 100 Hz |
| Gimbal | `lastRecordTime` (`gimbal.hpp` — already public) | `gimbal.cpp` ParseRecord() on every 154-byte data record | ~125 Hz |
| Fuji | `lastRspTime` (`fuji.hpp` — private) | `fuji.cpp` on every valid C10 response | ~16–33 Hz |
| MWIR | `lastRspTime` (`mwir.hpp` — private) | `mwir.cpp` on every valid serial response | ~20 Hz |
| INCL | `lastRspTime` (`incl.hpp` — private) | `incl.cpp` processFrame() on every accepted frame | ~1 Hz |

**NTP stamp added to BDC** — `prev_HB_NTP` / `HB_NTP` added to `bdc.hpp`. Stamp added to NTP intercept block in `bdc.cpp` — identical pattern to MCC. `HB_NTP` packed x0.1s units (÷100); C# reads `/10.0` → seconds. All other HBs raw ms.

**Unit summary:**

| Byte | Field | Firmware pack | C# parse | C# type | Display |
|------|-------|--------------|----------|---------|---------|
| [396] | HB_NTP | `/ 100` → x0.1s | `/ 10.0` → seconds | `double` | `"00.00s"` |
| [397] | HB_FMC_ms | raw ms | no divisor | `int` | `"000ms"` |
| [398] | HB_TRC_ms | raw ms | no divisor | `int` | `"000ms"` |
| [399] | HB_MCC_ms | raw ms | no divisor | `int` | `"000ms"` |
| [400] | HB_GIM_ms | raw ms | no divisor | `int` | `"000ms"` |
| [401] | HB_FUJI_ms | raw ms | no divisor | `int` | `"000ms"` |
| [402] | HB_MWIR_ms | raw ms | no divisor | `int` | `"000ms"` |
| [403] | HB_INCL_ms | raw ms | no divisor | `int` | `"000ms"` |

**Changes per file:**

`gimbal.hpp` — `uint8_t HB_ms()` getter added to public section after `lastRecordTime` (line 70). `lastRecordTime` was already public — no visibility change needed.

`fuji.hpp` — `uint8_t HB_ms()` getter added to public section after `hasPotVrefError` (line 111). `lastRspTime` stays private.

`mwir.hpp` — `uint8_t HB_ms()` getter added to public section after `isConnected` (line 103). `lastRspTime` stays private.

`incl.hpp` — `uint8_t HB_ms()` getter added to public section after `isConnected` (line 30). `lastRspTime` stays private.

`bdc.hpp` — `prev_HB_NTP` / `HB_NTP` added after `prev_HB`/`HB_ms` (line 351). Eight HB getters added after `isINCL_Ready()` (line 456): `HB_NTP_val()`, `HB_FMC()`, `HB_TRC()`, `HB_MCC()`, `HB_GIM()`, `HB_FUJI()`, `HB_MWIR()`, `HB_INCL()`.

`bdc.cpp` NTP intercept (line 353) — NTP stamp added: `delta = (millis() - prev_HB_NTP) / 100; HB_NTP = constrain(delta, 0, 255); prev_HB_NTP = millis();` — identical pattern to MCC.

`bdc.cpp` `buildReg01()` — bytes [396–403] packed after [395] V2 temps block: `buf[396]=HB_NTP; buf[397]=HB_FMC(); buf[398]=HB_TRC(); buf[399]=HB_MCC(); buf[400]=HB_GIM(); buf[401]=HB_FUJI(); buf[402]=HB_MWIR(); buf[403]=HB_INCL();`

`MSG_BDC.cs` — eight properties added after `TEMP_USB` (line 199). Comment block updated (line 401) — `[392-511] RESERVED` replaced with per-byte breakdown. Parse lines added at end of `ParseMSG01()` after `TEMP_USB` (line 557).

`frmBDC.cs` — lines 208–216 replaced. All eight labels now wired from `LatestMSG` HB properties. Previously `lbl_trc_hb` and `lbl_fmc_hb` were wired to stale embedded sub-MSG HB values (`trcMSG.HB_TX_ms`, `fmcMSG.HB_ms`) — now sourced from BDC firmware HB counters. `lbl_gimbal_hb` was commented out — now active. `lbl_visCam_hb`, `lbl_irCam_hb`, `lbl_incl_hb`, `lbl_ntp_hb` were unwired — now wired.

`frmBDC_Designer.cs` — `lbl_rtc_hb` renamed to `lbl_mcc_hb` throughout (4 occurrences: line 103, 828, 1134–1142, 2728). RTC is retired; label repurposed for MCC A1 stream HB. `.Name` property updated to `"lbl_mcc_hb"`.

**ICD impact:** BDC REG1 bytes [396–403] promoted from RESERVED. Defined count 396→404, reserved 116→108. Eight new rows to add to BDC REG1 table tagged `v4.0.0 (BDC-HB)`. Folded into ICD-1 scope.

**Items opened:** none
**Items closed:** none (BDC-HB wiring complete; ICD row additions tracked under ICD-1)
**ARCH:** v4.0.0 → v4.0.1 (§10 BDC — HB bytes [396–403] noted)

---

## CB-20260413c — MCC HB subsystem wiring + IPG HB fix
**ARCH:** v4.0.0 (no change) | **Files:** `ipg.hpp`, `ipg.cpp`, `battery.hpp`, `gnss.hpp`, `dbu3200.hpp`, `mcc.hpp`, `mcc.cpp`, `MSG_MCC.cs`, `frmMCC.cs`

**Root cause — IPG-HB-HEL:** `HB_HEL` (REG1 byte [131]) was always 0 in the GUI. Full chain traced: firmware pack correct, `MSG_MCC.cs` parse correct, `frmMCC.cs` label wired. Bug was at the pack site — `ipg.HB_RX_ms / 100` performed integer division on a ~20ms interval, always truncating to 0 before `constrain`. Fix: removed stored `HB_RX_ms` from `ipg.hpp/cpp`, replaced with `HB_ms()` getter computing `millis() - lastMsgRx_ms` at call time.

**Fleet HB pattern established — compute-in-getter, uint8 raw ms out:** All subsystem HB values (HEL, BAT, CRG, GNSS) follow a single pattern — each class owns a `uint8_t HB_ms()` getter that computes elapsed ms since last receive and constrains to uint8 (saturates at 255ms). No `/100` scale — raw ms end-to-end. `mcc.hpp` wrappers call the class getter and return `uint8_t` directly. Pack site in `mcc.cpp SEND_REG_01()` is clean direct assignment. `MSG_MCC.cs` reads raw ms with no divisor; properties typed as `int` with `_ms` suffix. Display format `"000ms"` on all four labels.

**HB_NTP is the deliberate exception** — NTP syncs every ~10s so raw ms overflows uint8 immediately. Firmware packs as x0.1s units (`millis() / 100`); `MSG_MCC.cs` reads with `/ 10.0` → seconds, typed as `double`. Range 0–25.5s fits uint8 correctly for NTP cadence.

**Unit summary:**

| Byte | Field | Firmware pack | C# parse | C# type | Display |
|------|-------|--------------|----------|---------|---------|
| [130] | HB_NTP | `/ 100` → x0.1s | `/ 10.0` → seconds | `double` | `"00.00s"` |
| [131] | HB_HEL_ms | raw ms | no divisor | `int` | `"000ms"` |
| [132] | HB_BAT_ms | raw ms | no divisor | `int` | `"000ms"` |
| [133] | HB_CRG_ms | raw ms, 0 on V2 | no divisor | `int` | `"000ms"` |
| [134] | HB_GNSS_ms | raw ms | no divisor | `int` | `"000ms"` |

**Changes per file:**

`ipg.hpp` — `uint16_t HB_RX_ms` replaced by `uint8_t HB_ms()` getter: `(uint8_t)constrain(millis() - lastMsgRx_ms, 0, 255)`. Raw ms, no scale. `lastMsgRx_ms` stays private.

`ipg.cpp` — `HB_RX_ms` stamp line removed from `parseLine()`. `lastMsgRx_ms = millis()` stamp retained.

`battery.hpp` — `uint8_t HB_ms()` getter added: `(uint8_t)constrain(millis() - lastGoodRxTime, 0, 255)`. Raw ms. `lastGoodRxTime` stamped in `processFrame()` on every valid CRC-checked RS485 frame.

`gnss.hpp` — `uint8_t HB_ms()` getter added: `(uint8_t)constrain(millis() - lastRxMs, 0, 255)`. Raw ms. `lastRxMs` stamped in `UPDATE()` on every received UDP packet from NovAtel.

`dbu3200.hpp` — `uint8_t HB_ms()` getter added: `(uint8_t)constrain(millis() - lastCommSuccessTime, 0, 255)`. Raw ms. V1 only — DBU not present on V2. `lastCommSuccessTime` stamped in `onCommSuccess()`.

`mcc.hpp` — `HB_HEL()` returns `uint8_t`, calls `ipg.HB_ms()`. `HB_BAT()`, `HB_GNSS()` added as `uint8_t` getters. `HB_CRG()` added with V1/V2 guard — `dbu.HB_ms()` on V1, `0` on V2. `HB_BAT`/`HB_CRG`/`HB_GNSS` member variables retired. `lastTick_BAT`/`lastTick_CRG`/`lastTick_GNSS` stubs retired.

`mcc.cpp SEND_REG_01()` — `buf[130]=HB_NTP; buf[131]=HB_HEL(); buf[132]=HB_BAT(); buf[133]=HB_CRG(); buf[134]=HB_GNSS();`. Comments updated to reflect units.

`mcc.cpp PRINT_REG()` — HB section retitled `-- HB Counters --`, all five bytes [130]–[134] now printed. GNSS split into separate `-- GNSS --` section. Previously [131]–[133] were missing entirely.

`mcc.cpp SERIAL_CMD()` — `ipg.HB_RX_ms` reference updated to `ipg.HB_ms()`.

`MSG_MCC.cs` — `HB_HEL`→`int HB_HEL_ms`, `HB_BAT`→`int HB_BAT_ms`, `HB_CRG`→`int HB_CRG_ms`, `HB_GNSS`→`int HB_GNSS_ms`. All four parse as `(int)msg[ndx]` with no divisor. Comment block updated with units. `HB_NTP` unchanged — `double`, `/ 10.0`.

`frmMCC.cs` — all four HB labels updated to `"000ms"` format and `_ms` property names.

**HB_NTP [130]** — working correctly. Stamps in `mcc.cpp` A2 intercept block on each NTP packet received. IP check confirmed correct — `ntp.timeServerIP` is always the active server (primary or fallback) since `ntp.INIT()` overwrites it on fallback switch. No changes needed. Getter refactor deferred (IPG-HB-4).

**Items closed:** IPG-HB-HEL, IPG-HB-1, IPG-HB-2, IPG-HB-3, IPG-STUBS
**Items opened:** none
**Items deferred:** IPG-HB-4 (HB_NTP getter refactor — low disruption but touches MSG_MCC.cs and ICD byte [130] label)

---

## CB-20260413 — DEF-1 / MSG-CMC-1 / FMC-TPH / FW-C5 closures
**ICD:** v3.6.0 (FMC REG1 TPH content edit; header version held for ICD-1) | **ARCH:** v3.3.7 → v3.3.8 (§10.5 + §17 FW-C5 closure notes)

Four closures landed this session — three small, one large.

**DEF-1 closed.** Both `defines.hpp` and `defines.cs` verified containing all CB-20260412 enum changes — `SET_TIMESRC=0xA3`, `SET_REINIT=0xA9`, `SET_DEVICES_ENABLE=0xAA`, `SET_CHARGER=0xAF` added; `SET_HEL_TRAINING_MODE=0xA1`, `ORIN_ACAM_COCO_ENABLE=0xD1`, `SET_BCAST_FIRECONTROL_STATUS=0xE0`, `SET_BDC_VOTE_OVERRIDE=0xB1` reassigned; retired names replaced by `RES_xx` rejection markers in lockstep across both files. **Naming note:** slot `0xAB` retains the legacy name `SET_FIRE_REQUESTED_VOTE` from its `0xE6` origin — slot-only move; name preserved to minimise C# call-site churn. ICD-1 must use the canonical name `SET_FIRE_REQUESTED_VOTE` in v4.0.0 entries (not the `SET_FIRE_VOTE` shorthand from the original CB-20260412 spec).

**MSG-CMC-1 closed.** Owner-confirmed fixed in `MSG_CMC.cs` — `ParseMsg()` now uses literal dual-check `case (ICD)0x00:` and `case (ICD)0xA1:` to handle both v4.0.0 and legacy pre-FW-C10 REG1 frames.

**FMC-TPH closed — bench-verified on V2 STM32F7 hardware.** Firmware: `tph.hpp` include, `TPH tph` member, `tph.SETUP()`/`UPDATE()`, REG1 pack at [47–58], `PRINT_REG()` and `TEMPS` serial output — all gated `#if defined(HW_REV_V2)`. V1 leaves bytes 0x00 (decodes to 0.0f via existing `memset` in `buildReg01()`). Serial output confirmed sane: MCU 45.28°C, Ambient 30.79°C (BME280 reads warm due to board thermal coupling — same effect as TMC), Pressure 100131.88 Pa (≈1001 hPa), Humidity 30.47%. C#: `MSG_FMC.cs` parses three `BitConverter.ToSingle` reads at [47]/[51]/[55]; `TPH_Temp`/`TPH_Pressure`/`TPH_Humidity` properties added; `frmFMC.cs` populates pre-existing `lbl_FMC_tph` designer label gated on `IsV2`; V1 displays `"TPH: V1 — n/a"`. ICD INT_ENG FMC REG1 table updated with three TPH rows tagged `v4.0.0 (FMC-TPH)`; defined-bytes count 47 → 59, reserved 17 → 5. ICD header version held at v3.6.0 pending broader ICD-1 v4.0.0 rename.

**Stale comment fix folded in:** while editing `fmc.cpp` header comment, also corrected stale "`ptp.INIT()` unconditional at boot (FW-B4 closed)" line — FMC's PTP init is gated by `isPTP_Enabled` (default false) per FW-B3 W5500 multicast contention, not unconditional. Header comment now matches the body code at `INIT()`.

---

**FW-C5 closed — full firmware + C# IP-define consolidation across all five controllers.** This was the largest item closed this session. Surgical pass (option a): every hardcoded peer-IP literal in source replaced with a registry symbol; intentional patterns (SET_NTP_CONFIG last-octet overrides, parsed-octet serial commands, log strings) left in place.

**`defines.hpp` additions:**
- `IP_HEL_BYTES 192, 168, 1, 13` — IPG laser TCP target on MCC
- `IP_NTP_FALLBACK_BYTES 192, 168, 1, 208` — Windows HMI w32tm fallback NTP server
- Existing IP block also reordered by last octet (cosmetic)

**`defines.cs` additions:**
- New top-level `public static class IPS` — flat string-typed registry mirroring `defines.hpp` IP_*_BYTES set, plus C#-only entries for THEIA / HYPERION (no firmware counterpart)
- 12 `const string` entries: MCC, TMC, HEL, BDC, GIMBAL, TRC, FMC, GNSS, NTP_PRIMARY, NTP_FALLBACK, THEIA, HYPERION
- `.208` appears twice deliberately (THEIA / NTP_FALLBACK) — same physical box, two roles

**Firmware edits per controller:**

| Controller | Edits | Notes |
|---|---|---|
| MCC | 4 | `mcc.hpp` NTP initializers (×2); `mcc.cpp` 2 × IP_HEL sites at REINIT and StateManager power-on |
| BDC | 3 | `bdc.hpp` NTP initializers (×2); `BDC.ino:30` top-level IP_BDC declaration |
| TMC | 3 | `tmc.hpp` NTP initializers (×2); `tmc.cpp` `_mcc[]` temp-array dance retired (was `static const uint8_t _mcc[] = A1_DEST_MCC_IP; a1DestMCC = IPAddress(_mcc[0]…)` — now clean `a1DestMCC = IPAddress(IP_MCC_BYTES)`) |
| FMC | 1 | `fmc.hpp` NTP initializers — `fmc.cpp::INIT()` and `FMC.ino` were already clean |
| TRC | 0 | TRC controller code (Linux/Jetson C++) was already compliant — uses its own `Defaults::` namespace registry from the start, mirrors `IPS`/`IP_*_BYTES` philosophy. `Defaults::BDC_HOST` already used by `trc_a1.cpp` for both TX socket connect and RX source validation. The only remaining literal in TRC controller code is `main.cpp:254` `destHost = "192.168.1.1"` — gateway placeholder for GStreamer video output, almost always overridden via `--dest-host` at launch; intentional, left in place. |

**C# edits per controller:**

| Controller | File | Edits | Notes |
|---|---|---|---|
| MCC | `mcc.cs:50` | 1 | IP property initializer → `IPS.MCC` |
| BDC | `bdc.cs:45` | 1 | IP property initializer → `IPS.BDC` |
| TMC | `tmc.cs:34` | 1 | IP property initializer → `IPS.TMC` |
| FMC | `fmc.cs:28` | 1 | IP property initializer → `IPS.FMC` |
| TRC | `trc.cs:20`, `trc.cs:106` | 2 | IP property → `IPS.TRC`; **plus** duplicate literal at line 106 fixed (bind site was bypassing `this.IP` and re-hardcoding `192.168.1.22` — now reads `IPAddress.Parse(IP)` matching the canonical `fmc.cs:101` pattern) |

**Audited and confirmed clean (zero edits needed):**
- All firmware peer-driver classes — MCC's `ipg.cpp/.hpp`, `gnss.cpp/.hpp`, `tmc.cpp/.hpp` (MCC-side); BDC's `fmc.cpp/.hpp`, `gimbal.cpp/.hpp`, `trc.cpp/.hpp` (BDC-side); shared `ntpClient.*`, `ptpClient.*`. Every peer-driver class takes its IP via `INIT(IPAddress _IP, …)` and stores it as a private member. Drivers themselves know nothing about 192.168.x.x literals.
- All C# `MSG_*.cs` register parsers — five files (MCC, BDC, TMC, FMC, TRC) plus MCC's MSG_GNSS / MSG_IPG and BDC's MSG_FMC / MSG_GIMBAL / MSG_TRC. Pure register parsers, never construct endpoints.
- All C# `frm*.cs` form classes — `frmMCC`, `frmBDC`, `frmTMC`, `frmFMC` (no `frmTRC` exists). Forms instantiate client classes via parameterless or logger-only constructors; never pass IPs; never override the IP property (`private set` enforces this at the type level).
- `frame.hpp` `A1_DEST_*_IP` defines (lines 97-98) — left in place per option (a) "leave frame.hpp alone" rule. After TMC's `_mcc[]` fix, `A1_DEST_MCC_IP` and `A1_DEST_BDC_IP` are both unreferenced dead code; flagged for separate cleanup as **FW-C5-FRAME-CLEANUP**.

**Patterns confirmed across the fleet:**
1. **Peer-driver discipline (firmware):** every peer-driver class takes IP via `INIT()`, stores as private member. The only IP literals exist at controller-level call sites where the driver is initialised (`ipg.INIT(IP_HEL)`, `gimbal.INIT(IP_GIMBAL_BYTES)`, etc.).
2. **Property discipline (C#):** every controller client class has `public string IP { get; private set; } = IPS.<NODE>;`. The `private set` is type-enforced — no form code can override it, no parser ever constructs an endpoint. Single point of edit per controller.
3. **Total surface area for the entire fleet:** 11 firmware edits + 6 C# edits + 2 new firmware defines + 1 new C# class. Roughly 20 line-level changes for the whole 5-controller cleanup.

---

**HW-FMC-1 closed — bench-verified.** Shared 5V line on USB serial connector between FMC and BDC corrected in hardware (merged FMC-HW-4, FMC-HW-5, FMC-HW-7). User confirmed brownout no longer observed with both controllers active. Production harness isolation verified on user's bench.

---

**BDC-FSM-VOTE-LATCH — opened and closed same session.** User-reported bug: "FMC fsm limit vote not clearing on the BDC until system goes into track." Root cause: `isFSMNotLimited` (VOTE_BITS_BDC bit 7, `FSM_NOT_LTD` — inverted logic, bit set = OK) was only updated inside the ATRACK/FTRACK case body of `BDC::PidUpdate()`, but the variable is read every telemetry tick at `bdc.hpp:224` to build the broadcast vote bitmask. On exit from track mode with the bit cleared (track point off-center had pushed the predictive computation past `FSM_ANGLE_MAX_TARGET_SPACE_DEG = 2.0°`), the value stuck at `false` and the broadcast vote kept reporting NO-FIRE until the next track entry recomputed it.

Initial Claude proposal (default `isFSMNotLimited = true` at top of `PidUpdate()`) was correctly rejected by user — defaulting to `true` would lie about the physical state when the FSM is parked at a non-zero position. Correct fix: compute `isFSMNotLimited` from the FMC FSM position readback at the top of `PidUpdate()`. The data is already available — `fmc.fsm_posX_rb` and `fmc.fsm_posY_rb` are extracted at `bdc.cpp:435-436` from FMC REG1 bytes [20-23]/[24-27] (FW-B5 offset fix) on every A1 frame. Conversion `(fsm_posX_rb - FSM_X0) * iFOV_FSM_X_DEG_COUNT` gives target-space degrees (matching the existing constant's units), and the magnitude check `sqrt(ax_rb² + ay_rb²) <= FSM_ANGLE_MAX_TARGET_SPACE_DEG` produces the correct limit state. SIGN omitted (magnitude only); gimbal NED offset omitted (we want local FSM angle, not world frame). The ATRACK/FTRACK case body still overwrites with the predictive (track-error-derived) value when actively driving the FSM — predictive leads the readback by one tick, which is the correct behaviour in track mode. In all other modes the readback value persists.

**Architectural placement decision (preserve in future maintenance):** user moved the `if ((millis() - prev_PID_Millis) < TICK_PID) return;` rate gate from above the readback block to BELOW it. The FSM limit check is an instantaneous physical state read, not a control-loop concept, and gating it at PID rate would mean some A1 frames carry a vote bit up to one PID period stale. With the gate moved below, the readback updates at full UPDATE-loop rate while the predictive computation remains gated to PID rate. Both computations live inside `PidUpdate()` together by design — they are two halves of the same FSM-limit decision, paired alongside the existing FSM_X/FSM_Y/Set_FM_POS code; hoisting either out of `PidUpdate()` would split a cohesive design. Do not move the rate gate back above the FSM block. Do not move either computation out of `PidUpdate()`.

ARCH was consulted during diagnosis (ARCH §10 BDC subsection has only one passing mention of "fire control votes" — no semantic definition of `FSM_NOT_LTD`). The bit's name (`FSM_NOT_LTD`) implies physical state, not predicted-command state — the readback-based interpretation is the natural one. Bench verification pending on user's end at time of rollup.

---

**TRC-SOM-SN closed — bench-verified.** Format: `uint64 LE` at TelemetryPacket bytes [49-56], user-specified (Claude initially proposed ASCII, was corrected). Bytes [57-63] remain RESERVED (7 bytes). 8 surgical edits applied across 5 files:

- `telemetry.h` — `som_serial` `uint64_t` field replaces 8 bytes of `RESERVED[15]`; `RESERVED[7]` retained for future use; two new `static_assert`s for offsets 49 and 57
- `types.h` — `uint64_t somSerial{0}` added to `GlobalState` after `version_word` (set-once-at-startup semantics, no atomic needed)
- `main.cpp` — boot-time read of `/proc/device-tree/serial-number` immediately after `version_word` print, parsed via `std::stoull` with try/catch fallback to 0 on parse failure or missing file. Logs `"SOM Serial: <n> (raw: \"...\")"` to stderr for boot visibility
- `udp_listener.cpp` — `telemetry.som_serial = state_.somSerial` packed in `buildTelemetry()` immediately after the Jetson stats block
- `MSG_TRC.cs` — `SomSerial` `UInt64` property added near Jetson health properties; `ParseMsg()` reads 8 bytes via `BitConverter.ToUInt64(rxBuff, ndx); ndx += sizeof(UInt64);` then skips remaining 7 RESERVED bytes (was `ndx += 15`); layout doc comment at top of file updated to show `[49-56] somSerial uint64` + `[57-63] RESERVED 7 bytes`

**Bonus:** user additionally wired `SomSerial` to the TRC on-screen display (OSD overlay) so the SN renders on the live video stream — beyond the surgical change set scope.

**ICD INT_ENG TRC REG1 update held per user request** — tracked as new low-priority item TRC-SOM-SN-ICD. Edit drafted (split `[49-63] RESERVED 15 bytes` row into `[49-56] som_serial uint64 LE` tagged `v4.0.0 (TRC-SOM-SN)` + `[57-63] RESERVED 7 bytes`; defined / reserved totals 49 / 15 → 57 / 7), to be applied at next ICD touch or folded into ICD-1.

---

**Items closed:** DEF-1, MSG-CMC-1, FMC-TPH, FW-C5, HW-FMC-1, BDC-FSM-VOTE-LATCH, TRC-SOM-SN, TRC-SOM-SN-ICD
**Items opened:** ARCH-FMC-HW (low — FMC §12.1 V1/V2 hardware table refactor), FW-C5-FRAME-CLEANUP (low — retire dead `A1_DEST_*_IP` defines from `frame.hpp`), TRC-CS-DEAD-IPENDPOINT (low — retire dead `ipEndPoint` field in `trc.cs`), BDC-FSM-VOTE-LATCH (opened+closed same session), TRC-SOM-SN-ICD (opened+closed same session — ICD edit was deferred earlier in session, applied in cleanup pass)
**ARCH:** v3.3.7 → v3.3.8 → v3.3.9 across the day. v3.3.8 captured FW-C5 + FMC-TPH closures; v3.3.9 added BDC-FSM-VOTE-LATCH + TRC-SOM-SN + HW-FMC-1 closure notes, marked the §17 rows, fixed the long-standing §10.5 mislabel in the v3.3.7 / v3.3.8 changes blocks (the bullets referenced "§10.5 IP defines" but actual §10.5 is "BDC Time Source Architecture" — IP defines are not currently a body section in ARCH).

---

## CB-20260412 — ICD Command Space Restructuring
**ICD:** v3.5.2 → v3.6.0 (pending update pass — ICD-1) | **ARCH:** v3.3.7 (pending update pass — ARCH-1)

Major ICD command space audit and restructuring. A block now fully assigned INT_OPS — all 16 slots active. Significant number of retirements, merges, moves, and scope promotions applied across all six command blocks.

**Retirements (slots freed this session):**
- `0xA9` PRINT_LCH_DATA → BDC serial command only, UDP path removed
- `0xB1` SET_GIM_HOME → gimbal FW handles home directly, ICD slot freed
- `0xD1` ORIN_SET_STREAM_MULTICAST → compile/launch time config only, not runtime-controllable
- `0xD2` ORIN_SET_STREAM_60FPS → compile/launch time only; ASCII `FRAMERATE` covers ENG use
- `0xD8` ORIN_SET_TESTPATTERN → ASCII `TESTSRC` covers ENG use; TRC binary handler never implemented
- `0xDF` ORIN_COCO_ENABLE → moved to `0xD1` (slot freed)
- `0xB0`, `0xBE`, `0xE0`, `0xE1` → superseded by unified fleet commands; pending handler removal (FW-C8)
- `0xE3`, `0xED` → merged into `0xAF` SET_CHARGER; pending handler removal (FW-C8)
- `0xE6` → moved to `0xAB`; pending handler removal at old address (FW-C8)
- `0xAF`, `0xAB`, `0xAA` → reassigned to new commands (slots not wasted)

**New assignments:**
- `0xA1` ← SET_HEL_TRAINING_MODE (moved from `0xAF`, INT_OPS)
- `0xA3` ← SET_TIMESRC (new, INT_OPS, all five controllers; pending FW-C8 handler removal first)
- `0xA9` ← SET_REINIT (new, INT_OPS, MCC+BDC; replaces `0xB0`+`0xE0`; routing by IP)
- `0xAA` ← SET_DEVICES_ENABLE (new, INT_OPS, MCC+BDC; replaces `0xBE`+`0xE1`; routing by IP)
- `0xAB` ← SET_FIRE_VOTE (moved from `0xE6`, promoted INT_ENG→INT_OPS)
- `0xAF` ← SET_CHARGER (new merged command, INT_OPS, MCC V1 only; replaces `0xE3`+`0xED`)
- `0xD1` ← ORIN_COCO_ENABLE (moved from `0xDF`, INT_OPS)
- `0xE0` ← SET_BCAST_FIRECONTROL_STATUS (moved from `0xAB`, INT_ENG; internal vote sync)
- `0xB1` ← SET_BDC_VOTE_OVERRIDE (moved from `0xAA`, INT_ENG; BDC engineering block)

**Scope promotions (INT_ENG → INT_OPS):**
- `0xA2` SET_NTP_CONFIG — operator NTP config, routing by IP
- `0xA3` SET_TIMESRC — operator time source control (new)
- `0xA1` SET_HEL_TRAINING_MODE — safety enforced in firmware (10% clamp), not scope restriction
- `0xAB` SET_FIRE_VOTE — heartbeat is the safety gate; vote drops on client disconnect

**Key design decisions recorded:**
- Fleet commands route by destination IP — no "which controller" payload byte needed
- SET_REINIT and SET_DEVICES_ENABLE: MCC and BDC only (TMC/FMC not supported; handled by parent)
- SET_CHARGER: level required on every call — enables and sets level simultaneously, cannot enable without specifying level
- REG1 CMD_BYTE `0xA1` is a legacy protocol artifact — magic bytes and port already identify the frame, no parser branches on `0xA1`. Target: change to `0x00` (non-command marker) fleet-wide (FW-C10), then fully free `0xA1` for assignment
- `0xD8` ORIN_SET_TESTPATTERN: ASCII path sufficient for all ENG use; binary handler never written and not needed

**Items opened:** FW-C8 (expanded scope), FW-C10, FW-C11, FW-C12, FW-C13, ICD-1, DEF-1, ARCH-1
**Items closed:** FW-C9, CLEANUP-2, TRC-MULTICAST (retired), TRC-FRAMERATE (retired)

---

### CB-20260412 — MCC Controller Review (continuation)
**Files reviewed:** `mcc.cpp`, `mcc.hpp`, `MCC.ino`, `mcc.cs`, `MSG_MCC.cs`, `defines.hpp`, `defines.cs`, `ipg.cpp`, `ipg.hpp`, `IPG_6K_INTEGRATION_PLAN.md`, `MCC_HW_DELTA.md` (synced)

**Confirmed complete from prior sessions (no changes needed):**
- V1/V2 hardware unification (`hw_rev.hpp`, `pin_defs_mcc.hpp`, `mcc.hpp`, `MCC.ino`) ✅
- HW-1/HW-2/PIN-SWAP all verified on hardware ✅
- `HEALTH_BITS()` byte [9] bit 3 = `isTrainingMode` already present in `mcc.hpp` ✅
- `isHEL_TrainingMode` accessor already in `MSG_MCC.cs` HealthBits bit 3 ✅
- `isUnSolicitedMode_Enabled` confirmed removed from `MSG_MCC.cs` — **FW-C6 CLOSED** ✅
- `isHEL_Valid()` helper present in `mcc.hpp` ✅
- `LASER_MODEL` byte [255] packed in `SEND_REG_01()`, parsed in `MSG_MCC.cs` ✅
- `isEMON()` model-normalised in `ipg.hpp` (6K=bit2, 3K=bit0) ✅
- IPG 6K Step 2 firmware complete and bench-validated 2026-04-10 ✅
- `FW_VERSION = VERSION_PACK(3,3,6)` — firmware is v3.3.6, ahead of delta doc ✅

**Key findings requiring action:**
- `defines.hpp` / `defines.cs` both at ICD v3.4.0 — all CB-20260412 enum changes pending (DEF-1)
- `EXT_CMDS_MCC[]` whitelist stale — V1/V2 split no longer needed; 10 byte changes pending
- `mcc.cpp` switch cases: 5 new cases to add, 5 to convert to rejections, 1 to delete, 2 to rename (FW-C8/C11/C12/C13)
- `mcc.cs` command methods: 4 enum refs to update, 2 `AssertIntEng` guards to remove, charger API to merge
- `MSG_MCC.cs` parser: `ICD.RES_A1` CMD_BYTE check breaks after DEF-1 — fix to literal `0xA1` with FW-C10 comment
- `mcc.cpp` FW-C10 scope: 3 locations — `buf[0]`, A2 frameBuildResponse CMD_BYTE, A3 frameBuildResponse CMD_BYTE
- `ipg.hpp` sentinel values `hk_volts`/`bus_volts` = `5.5f` — LOW priority change to `0.0f`
- `SET_POWER()` remains `uint8_t` — confirmed correct, no change needed
- CRG-1 still wrong in code: `mcc.cpp` line ~902 `isChargerAlarm = (digitalRead(PIN_CRG_ALARM) == HIGH)` — D42 is charge-good indicator so HIGH=OK, logic is inverted
- COMBAT gate `isHEL_Valid()` in StateManager — verify present in `mcc.cpp` during edit pass
- IPG HB counters (BAT, GNSS, CRG) all always 0 — `lastTick_*` stubs declared but never wired

**Edit workflow agreed:** Surgical prompted edits — line number + before/after text blocks provided per change. Fleet-wide sweep: once edits begin on any file, that controller must be completed before moving to the next. Order: MCC → BDC → TMC → FMC → TRC.

**Firmware version convention — v4.0.0 major bump:**
All five controller firmware targets `VERSION_PACK(4,0,0)` to signal ICD v3.6.0 command space. This is a wire-level breaking change — old C# sending retired bytes to new firmware (or vice versa) produces incorrect behaviour, not graceful degradation. Major version bump is the unambiguous gate. C# clients add `FW_MAJOR >= 4` check (`FW_VERSION >> 24`) before sending any new commands (0xA1, 0xA9, 0xAA, 0xAB, 0xAF, etc.). `IsV4` property to be added to `MSG_MCC.cs` (and equivalent for other controllers) during C# edit pass. A controller is not considered updated until it transmits 4.x.x in REG1 bytes [245–248].

**Items opened:** CRG-1, CRG-2, CRG-3, IPG-HB-1, IPG-HB-2, IPG-HB-3, IPG-HB-4, IPG-STUBS
**Items closed:** FW-C6, OQ-5, OQ-6, HW-1, HW-2, PIN-SWAP

---

## ~Session 39 — 2026-04-11
**FMC STM32F7 Port Complete**
**ICD:** v3.5.2 | **ARCH:** v3.3.7 | **FW:** FMC 3.2.x → 3.3.0 (all four embedded controllers now 3.3.0)

FMC v2 platform migration from SAMD21 to STM32F7 (OpenCR board library) complete. `hw_rev.hpp` self-detection added — HW_REV byte [45]: `0x01`=V1 (SAMD21 legacy), `0x02`=V2 (STM32F7). FMC REG1 breaking changes: byte [7] renamed `FSM STAT BITS` → `HEALTH_BITS`; byte [45] promoted RESERVED → `HW_REV`; byte [46] promoted RESERVED → `FMC POWER_BITS` (`isFSM_Powered` bit 0, `isStageEnabled` bit 1). `isNTP_Enabled` default changed false → true (SAMD21 NTP/USB CDC bug not applicable on STM32). NTP init unconditional at boot. `ptp.INIT()` remains gated behind `isPTP_Enabled` — reason is now FW-B3 (multicast contention fleet-wide), not SAMD platform limitation. FMC socket budget: 2/8 always (PTP gated). `MSG_FMC.cs`: `HealthBits`/`PowerBits` properties added; `StatusBits1` retained as backward-compat alias → `HealthBits`; `HW_REV` byte [45] parsed; `IsV1`/`IsV2`/`HW_REV_Label` added. See `FMC_STM32_MIGRATION_FINAL.md`.

New item opened: **HW-FMC-1** (FMC/BDC power isolation — brownout risk via shared serial power in test).
Items closed: **FMC-STM32-1**, **FMC-NTP**, **SAMD-NTP**.

---

## ~Session 38–39 — 2026-04-11
**IPG 6K Laser Integration Phase 1**
**ICD:** v3.5.0 | **FW:** MCC 3.3.0

`LASER_MODEL` enum added to `defines.hpp` and `defines.cs` (`UNKNOWN=0x00`, `YLM_3K=0x01`, `YLM_6K=0x02`). ENG GUI HEL window completely rewritten: transport changed UDP port 10011 → TCP port 10001; auto-sense via `RMODEL`/`RMN` on connect; model-conditional periodic poll (20ms). `MSG_IPG.cs` extended with `ParseDirect()`, `LaserModel`/`SerialNumber`/`IsSensed`/`MaxPower_W`/`IsEMON`/`IsNotReady` properties. `PowerSetting_W` now model-aware. MCC REG1 byte [255]: RESERVED → `LASER_MODEL`. MCC REG1 byte [9] bit 3: `isTrainingMode` added. MCC serial commands added: `HEL`, `HELPOW`, `HELCLR`, `HELTRAIN`. `ipg.hpp`/`ipg.cpp` rewritten for TCP/auto-sense. MCC byte [131] `HEL HB` now live (packs `ipg.HB_RX_ms / 100`).

**⚠️ `0xAF SET_HEL_TRAINING_MODE` assigned — was `RES_AF`. This conflicts with S30 assignment of `0xAF = SET_TIMESRC` (FW-C7/ICD-AF). The HEL assignment takes precedence as the implemented command. FW-C7 requires a new ICD byte. See FW-C9.**

New item opened: **FW-C9** (0xAF slot conflict — assign new byte for SET_TIMESRC).

---

## ~Session 37 — 2026-04-07
**TMC V1/V2 Hardware Unification**
**ICD:** v3.3.9 | **ARCH:** v3.3.3 | **FW:** TMC → 3.3.0

V1→V2 hardware changes: single Vicor pump (DAC-trimmed) → two TRACO DC-DCs (on/off per pump); heater removed; ADS1015 external ADC chips removed; direct MCU analog inputs for temps. `CTRL_ON/OFF` polarity macros in `hw_rev.hpp`. HW_REV byte [62] self-detecting. STATUS_BITS1 bit 5 `RES`→`isPump2Enabled` (V2 only). New serial commands: `PUMP`, `PIDGAIN`. `isSingleLoop` STATUS_BITS1 bit 6 both revisions. Stale V2 protocol regressions discarded (V1 authoritative). V1 ✅ V2 ✅ SINGLE_LOOP ✅. PID overshoot noted (PID-1). V1 heater deferred — no hardware (T7). See `TMC_HW_DELTA.md`, `TMC_TEST_AND_GUI.md`.

---

## ~Session 38 — 2026-04-11 (a)
**FMC STM32F7 Migration**
**ICD:** v3.5.2 | **ARCH:** v3.3.7 | **FW:** FMC SAMD21 v3.2.3 → STM32F7 v3.3.0

SAMD21 → STM32F7 (OpenCR) platform migration. `hw_rev.hpp` abstraction macros: `FMC_SERIAL`, `FMC_SPI`, `uprintf`, `FMC_HW_REV_BYTE`, `FSM_POW_ON/OFF`. SPI bus contention fixed (beginTransaction/endTransaction). `delay(100)` moved after `endTransaction` in `init_FSM()` (#16 closed). ICD v3.5.2: byte [7] → `FMC HEALTH_BITS` (isReady only); byte [45] → `HW_REV`; byte [46] → `FMC POWER_BITS` (isFSM_Powered/isStageEnabled). `ptp.INIT()` re-gated behind `isPTP_Enabled` (FW-B3). `MSG_FMC.cs`: `HealthBits`/`PowerBits`/`HW_REV`/`IsV1`/`IsV2` added. All SAMD21 bugs (#1–#11, #16) closed. Performance items #13/14/15/18 carried as FMC-13/14/15/18. `micros()` rollover (#17) status unverified (FMC-17). V1 ✅ V2 ✅. See `FMC_STM32_MIGRATION_FINAL.md`. `FMC_Open_Items.md` archived — historical SAMD21 record only.

**Items opened:** FMC-HW-4, FMC-HW-5, FMC-HW-7, FMC-13, FMC-14, FMC-15, FMC-17, FMC-18, FMC-CS7

---

## ~Session 38 — 2026-04-11 (b)
**BDC V1/V2 Hardware Unification**
**ICD:** v3.5.1 | **ARCH:** v3.3.6 | **FW:** BDC 3.2.x → 3.3.0

BDC `hw_rev.hpp` self-detection. HW_REV at byte [392]: `0x01`=V1, `0x02`=V2 (BDC Controller 1.0 Rev A). REG1 byte [10] renamed → `HEALTH_BITS` (breaking: bit 1 = `isSwitchEnabled`, V2 only). Byte [11] renamed → `POWER_BITS` (layout unchanged — rename only). Bytes [393–395] promoted: `TEMP_RELAY`, `TEMP_BAT`, `TEMP_USB` (V2 live; V1 always 0x00). `MSG_BDC.cs`: `HealthBits`/`PowerBits` added, `StatusBits`/`StatusBits2` retained as backward-compat aliases. `IsV1`/`IsV2`/`HW_REV_Label` added. Three thermistor properties added. Vicor polarity flip V1→V2 documented. See `BDC_HW_DELTA.md`.

---

## ~Session 37–38 — 2026-04-08
**MCC V1/V2 Hardware Unification**
**ICD:** v3.4.0 | **ARCH:** v3.3.4 | **FW:** MCC 3.2.x → 3.3.0

MCC `hw_rev.hpp` self-detection. HW_REV at byte [254]. REG1 byte [9] renamed → `HEALTH_BITS` (breaking: `isChargerEnabled` was bit 4 → bit 1; `isNotBatLowVoltage` was bit 5 → bit 2; solenoid bits 1–2 and laser bit 3 moved to `POWER_BITS`). Byte [10] renamed → `POWER_BITS` (bit N = `MCC_POWER` enum N; revision-independent decode). `MSG_MCC.cs`: `HealthBits`/`PowerBits` added; `StatusBits`/`StatusBits2` retained as backward-compat aliases. See `MCC_HW_DELTA.md`. **NEW-33 closed** — `isNotBatLowVoltage` now correctly placed at HEALTH_BITS bit 2.

Items closed: **NEW-33**.

---

## ~Session 35–37 — 2026-04-10
**TRC Address Documentation / JETSON_SETUP.md Complete**
**ARCH:** v3.3.5

TRC `.22` documented as role address shared by all TRC units (non-Super and Super) — only one unit ever live at a time. Address belongs to the role, not the hardware. `JETSON_SETUP.md` complete at v2.2.0 — DOC-2 closed. ARCH §2.5 updated with DOC-2 cross-reference.

Items closed: **DOC-2**.

---

## Session 36 — ~2026-03-xx
**HW Verification / LCH Vote Fix**

MSG_MCC.cs and MSG_BDC.cs all fields confirmed correct on live hardware. CRC-16/CCITT confirmed correct across all five controllers and C#. `frmMain.cs` SET_LCH_VOTE arg swap fixed (`operatorValid` was duplicated). NEW-39 (LCH/KIZ `operatorValid` hardcoded true) confirmed complete from S28.

Items closed: **NEW-9**, **NEW-10**, **NEW-18**, **NEW-31**, **NEW-39**.

---

## Session 33 — ~2026-03-xx
**FMC PTP Integration (SAMD21)**

TIME_BITS at byte [44]. Socket budget 4/8 with PTP enabled. NTP IP corrected `.8`→`.33`. `isNTP_Enabled=false` default (SAMD-NTP workaround active at this time). TIME/TIMESRC/PTPDEBUG serial commands. MSG_FMC.cs updated.

Items closed: **NEW-38c**.

---

## Session 32 — ~2026-03-xx
**BDC PTP Integration**

Socket budget corrected 9/8→7/8 (corrected double-count). TIME_BITS at byte [391]. Boot step PTP_INIT added. MSG_BDC.cs updated.

Items closed: **NEW-38b**.

---

## Session 30/31 — ~2026-03-xx
**TMC PTP Integration**
**FW:** TMC → v3.0.5

STAT_BITS3 at byte [61]. TIME/TIMESRC/PTPDEBUG serial commands. MSG_TMC.cs updated.

Items closed: **NEW-38a**.

---

## Session 30 — 2026-04-06
**HMI Controller Health Stats / CommHealth**
**ICD:** v3.3.8 | **ARCH:** v3.3.2 → v3.3.3

Full timing health system implemented across `MSG_MCC.cs`, `MSG_BDC.cs`, `crossbow.cs`, `frmMain.cs`. MSG_MCC/MSG_BDC now own all timing stats (`dtmax`, `HbMax`, `DtAvg`, `HbAvg`, `DUtcMax`), thresholds (`DT_WARN_US=15000`, `DT_BAD_US=30000`, `HB_WARN_MS=15000`, `HB_BAD_MS=30000`, `DUTC_WARN_MS=3.0`, `DUTC_BAD_MS=10.0`), `EWMA_ALPHA=0.10`. `CommHealth` property returns instantaneous `READY_STATUS` from live `dt_us`/`HB_ms`. IBIT labels expanded to show dt/HB with avg/max fields. Time strip split into three `ToolStripStatusLabel` controls per controller. Double-click resets on dt/HB labels. MSG_BDC dtmax bug fixed (was threshold-gated, now true running max). MSG_BDC `activeTimeSourceLabel` NTP fallback case added. `CB.MCC_STATUS`/`CB.BDC_STATUS` simplified — before STANDBY: ping only; at/after STANDBY: CommHealth exclusively. `WorstStatus()` added then removed.

`SET_TIMESRC = 0xAF` assigned (payload: 0=OFF, 1=NTP, 2=PTP, 3=AUTO, INT_ENG only). **⚠️ Later reassigned to `SET_HEL_TRAINING_MODE` in ICD v3.5.0 — see FW-C9.**

FW-1/FW-2 confirmed (PTPDEBUG, TIMESRC serial commands fleet-wide). TMC hw_rev.hpp unified codebase (ARCH v3.3.3).

Items closed: **HMI-STATS-1**, **HMI-STATS-TIME**, **CB-COMMHEALTH**, **MSG-BDC-DTMAX**, **MSG-BDC-TIMESRC**, **ICD-AF**, **FW-1**, **FW-2**, **NEW-38a** (TMC PTP).

---

## Session 29 — ~2026-03-xx
**ENG GUI Client Connect Fix / PTP HW Verify**
**ICD:** v3.3.8 | **ARCH:** v3.3.2

GUI-1 closed: six A2/A3 handler root causes fixed — (1) new client detection moved before replay window check (prevented permanent lockout of reconnecting clients); (2) `_lastKeepalive` only updated in `SendKeepalive()`, not on every `Send()`; (3) any valid frame updates `isConnected`/`lastMsgRx`, not just `0xA1`; (4) `connection established` logged immediately on first valid frame. Applied fleet-wide: `mcc.cs`, `bdc.cs`, `tmc.cs`, `fmc.cs`. C# ENG GUI client connect standard established (ARCH §4.2). PTP HW verify: `offset_us=12`, `active source: PTP`, `time=2026-03-28` confirmed on MCC.

Items closed: **GUI-1**, **NEW-36**, **NEW-37**.
Items opened: **FMC-NTP** (FMC dt elevated — suspected NTP/USB CDC loop), **GUI-8** (TRC C# client model pending).

---

## Session 28 — ~2026-03-xx
**Serial Standards / IP Defines / PTPDIAG / BDC Boot**
**ARCH:** v3.3.1

Serial buffer changed `String serialBuffer` → `static char[64]` + `static uint8_t serialLen` on all four `.ino` files. HELP command restructured (COMMON + SPECIFIC blocks). TIME command standardised — `lastSync ms ago`, `PrintTime()` gated on `isSynched`. A1 TX control (`isA1Enabled` flag, `A1 ON|OFF` serial command) added to all four controllers. BDC A1 ARP backoff added (`a1FailCount`/`a1BackoffCount`/`A1_FAIL_MAX=3`). PTPDIAG command added (toggles `ptp.suppressDelayReq`). `IP_BDC_BYTES`, `IP_TMC_BYTES`, `IP_MCC_BYTES` added to `defines.hpp`. BDC boot: `FUJI_WAIT(5s)` step added. SAMD-NTP root cause identified: `PrintTime()` calling `Serial` not `SerialUSB` — removed all `PrintTime()` calls from FMC handlers. NTP confirmed working on SAMD bench with USB CDC active. `0xAF SET_HEL_TRAINING_MODE` set `isTrainingMode` + power clamp added to MCC.

Items opened: **FW-C3**, **FW-C4**, **FW-C5**, **DOC-3**.

---

## Session 27 — ~2026-03-xx
**NTP Integration Complete / NIC Bind Fix**
**ICD:** v3.2.0

`SET_NTP_CONFIG 0xA2` implemented: 0 bytes=resync, 1 byte=set primary octet, 2 bytes=set primary+fallback. NTP auto-recovery implemented (`consecutiveMisses`, `NTP_STALE_MISSES=3`, 2-min primary retry). NTP stratum/LI validation (rejects stratum 0, stratum ≥16, LI=3). NTP server defaults: `.33` HW Stratum 1 primary, `.208` Windows HMI fallback, `.8` removed. NTP fallback status bits added to all controller REG1. `CrossbowNic.cs` auto-detects internal NIC (<100) and external NIC (≥200). ICD v3.2.0 issued. HYPERION↔THEIA CUE relay confirmed working.

Items closed: **NET-1**, **NTP-RECOVER**, **NTP-STRATUM**, **NTP-SERVERS**, **NTP-STATUS**, **NIC-BIND**, **ICD-3.2.0**, **HYPERION-THEIA**, **MCC-1**, **TMC-TEMP-1**, **DEPLOY-1**, **DEPLOY-2**, **NEW-35**.

---

## Session 26 — ~2026-03-xx
**BDC→FMC Path / ENG GUI Socket Bind / Fire Control Verify**

BDC→FMC A1 path: port `10023`→`10019`, `isConnected` watchdog, `OnA1Received()`. BDC→FMC command framing: `EXEC_UDP()` replaced with full INT framed sends, port PORT_A2, `client.begin(0)` for send-only socket. `EXT_CMDS_BDC[]`: `0xF1`/`F2`/`F3`/`FB` added for HMI passthrough. ENG GUI TransportPath pattern implemented. MAINT/FAULT coordinated flash confirmed all five controllers. Fire control vote EXT promotions confirmed (`0xE6`, `0xCC`, `0xB4` all STATUS_OK).

Items closed: **BDC-FMC-1**, **BDC-FMC-2**, **BDC-FMC-3**, **FMC-ENG-1**, **FSM-TRACK**, **NET-BAT**, **TRC-M11b**, **HMI-A3-20**, **TRC-2**, **FW-MCC**, **FW-VERIFY**.

---

## Session 22 — ~2026-xx-xx
**ICD Scope Labels / TRC A2 Framing Complete**
**ICD:** v3.1.0

INT_OPS/INT_ENG scope labels applied to all commands. TRC A2 framing complete: magic/frame validation (TRC-M1), `buildTelemetry` struct rewrite (TRC-M5), `udp_listener.cpp` build/parse/CRC (TRC-M7).

Items closed: **NEW-13**, **TRC-M1**, **TRC-M5**, **TRC-M7**.

---

## Session 17 — ~2026-xx-xx
**TransportPath Enum**

MAGIC_LO computed from `TransportPath` enum, not hardcoded. Deployed sessions 16/17.

Items closed: **NEW-12**.

---

## Session 15 — ~2026-xx-xx

TRC `isConnected` live flag wired in `handleA1Frame` — was only set in dead receive loop.

Items closed: **TRC-M10**.

---

## Session 14 — ~2026-xx-xx
**Initial ICD Reconciliation**
**ICD:** v1.7.2

Stream rates table added to ICD. `EXT_CMDS[]` confirmations. `defines.hpp` enum names synced to ICD. TRC compile error fixed.

Items closed: **S14-1**, **S14-2**, **FW-PRE-CHECK**, **FW-BDC-1**, **DISC-1**, **ENUM-1**, **ENUM-2**, **ENUM-3**, **ENUM-4**, **ENUM-5**, **TRC-1**.

---

# PART 2 — OPEN ITEMS

**Last reconciled:** 2026-04-16 (CB-20260416c — end-of-session closures)
**ICD Reference:** INT_ENG v3.5.2 → v3.6.0 pending (IPGD-0003) | INT_OPS v3.3.8 (IPGD-0004) | EXT_OPS v3.3.0 (IPGD-0005)
**ARCH Reference:** v3.3.7 → pending update (IPGD-0006)
**Closed items:** Part 3 of this document

---

## 🔴 HIGH

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| ~~FW-CRG-V2~~ | ~~MCC V2 SET_CHARGER rejects enable — firmware fix pending flash~~ | ✅ **CLOSED CB-20260416** | Flashed and bench-verified. `EnableCharger(true)` path confirmed working on V2 hardware. | `mcc.cpp` ✅ |
| ~~HW-CRG-V2-OPTO~~ | ~~V2 charger opto sticking — enable/disable unreliable~~ | ✅ **CLOSED CB-20260416** | Root cause: mis-wire. Corrected on bench. Charger enable/disable confirmed reliable on V2. | Hardware ✅ |
| THEIA-HUD-FIRECONTROL | TRC video overlay fire control label | ⏳ Pending | Display key MCC→BDC vote state on HUD video overlay. Minimum: `isNotBatLowVoltage`, `isHEL_TrainingMode`. Review full MCC vote chain for additional overlay candidates. Coordinate with TRC OSD implementation. | `frmMain.cs` — video overlay draw path; TRC OSD |
| THEIA-SHUTDOWN | Clean THEIA/system shutdown — graceful STANDBY→OFF | ⏳ Pending | Laser safe, relays off, state→OFF, HMI disconnect. Define shutdown sequence for commanded shutdown vs power loss. Review MCC/BDC responsibilities. No progress S27→~S39. | THEIA `.cs` shutdown handler / state machine |
| HMI-A3-18 | LCH/KIZ/HORIZ bulk upload bench test | ⏳ Bench verify | Whitelist confirmed clean in firmware. Full end-to-end bench verification needed: upload from THEIA via A3, confirm receipt and correct parse in BDC, verify all fields land correctly in REG1. | None — test only |
| GUI-2 | HMI robust testing — live HW | ⏳ In progress | MCC/BDC/TMC/FMC ENG GUI stable S29. BDC A3 (THEIA) stable. Full engagement sequence, mode transitions, fire control chain end-to-end still pending. | HW — no code changes |
| FW-B3 | PTP DELAY_REQ W5500 contention — fleet-wide workaround active | 🟢 Low | When two or more controllers have PTP active simultaneously, W5500 blocks ~40ms per DELAY_REQ on ARP resolution, saturating main loop. **Workaround: `isPTP_Enabled=false` fleet-wide — NTP only in production. NTP server (.33) provides adequate time accuracy for current operations.** Proposed fixes when PTP is needed: (1) `suppressDelayReq` flag per-controller; (2) staggered DELAY_REQ timing — FMC +50ms offset after FOLLOW_UP. Unblocks FW-B4. | `ptpClient.cpp/hpp` — DELAY_REQ transmission logic |
| ~~HW-FMC-1~~ | ~~FMC/BDC shared power rail — HW fix applied, bench verify pending~~ | ✅ **CLOSED** | **Bench-verified CB-20260413.** Shared 5V line on USB serial connector between FMC and BDC corrected in hardware. Merged FMC-HW-4, FMC-HW-5, FMC-HW-7. Brownout no longer observed with both controllers active. Production harness isolation confirmed on user's bench. | Hardware — bench + production harness ✅ |
| HMI-AWB | VIS camera AWB passthrough — HMI binding pending | ⏳ AWB-ENG closed CB-20260416e | **(1) AWB-ENG ✅ CLOSED** — `CMD_VIS_AWB = 0xC4` assigned; `bdc.hpp` whitelist, `trc.hpp/cpp` `SET_AWB()`, `bdc.cpp` UDP handler + serial + HELP, `udp_listener.cpp` binary handler all complete. `ICD_CMDS` alias retired from `types.h`. **(2) AWB-HMI ⏳ pending** — expose on THEIA HMI; AWB maps to Xbox controller input, binding TBD. | THEIA `frmMain.cs` — Xbox binding |
| HMI-TRACKER | Tracker controls (COCO + optical flow) — ENG GUI then HMI | ⏳ Pending | Two sub-steps: **(1) TRACKER-ENG:** COCO class filter (`0xD9`) in ICD and firmware whitelist — C# wiring to `frmBDC.cs` only. COCO enable is now `0xD1` (moved from `0xDF` — update C# reference). **(2) TRACKER-HMI:** expose on THEIA HMI — Xbox controller binding TBD. Optical flow deferred to TRC session. | `frmBDC.cs`, THEIA HMI `.cs`, `defines.cs` (`ORIN_ACAM_COCO_ENABLE` enum value → `0xD1`) |

---

## 🟡 MEDIUM — Firmware

### Fleet / Cross-cutting

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| ~~FW-B4~~ | ~~Fleet ptp.INIT() gate — all controllers must gate ptp.INIT()~~ | ✅ **CLOSED** | Confirmed in source: TMC `tmc.cpp` line 45 gates `ptp.INIT()` behind `isPTP_Enabled`. BDC `bdc.cpp` line 197 gates in boot state machine `PTP_INIT` step. MCC ✅ gated. FMC ✅ gated. All five controllers confirmed gated. |
| ~~FW-B5~~ | ~~BDC FSM position offsets wrong in handleA1Frame()~~ | ✅ **CLOSED** | `fmc.fsm_posX_rb` offset corrected 24→20, `fmc.fsm_posY_rb` 28→24. Confirmed against FMC firmware (`buf+20`/`buf+24`) and `MSG_FMC.cs` parser. Closed CB-20260412 BDC pass. | `bdc.cpp` ✅ |
| ~~GUI-3~~ | ~~MSG_BDC.cs activeTimeSource reads from wrong bits~~ | ✅ **CLOSED** | `activeTimeSourceLabel` line 599: `isNTP_DeviceReady` (DeviceReadyBits bit 0) → `tb_isNTP_Synched` (TimeBits bit 3). Now reads from correct TIME_BITS source. Closed CB-20260412 BDC pass. | `MSG_BDC.cs` ✅ |
| ~~FW-C5~~ | ~~Audit/consolidate IP defines in defines.hpp~~ | ✅ **CLOSED** | **Closed CB-20260413.** Full firmware + C# IP-define consolidation across all five controllers. `defines.hpp` gained `IP_HEL_BYTES` and `IP_NTP_FALLBACK_BYTES`; `defines.cs` gained new flat `IPS` static class with 12 string constants for all CROSSBOW node IPs (mirrors firmware-side `IP_*_BYTES` plus C#-only THEIA/HYPERION). Firmware: 11 edits across MCC (4) / BDC (3) / TMC (3) / FMC (1); TRC controller code already compliant via its own `Defaults::` namespace registry. C#: 6 edits across all five client classes (TRC had a duplicate literal at `trc.cs:106` bypassing the IP property — fixed). All peer-driver classes (firmware) and `MSG_*.cs` / `frm*.cs` files (C#) audited and confirmed clean — discipline is type-enforced via `private set` on the C# IP properties and via `INIT(IPAddress)` signatures on firmware peer drivers. Surgical option (a) — intentional patterns left in place: SET_NTP_CONFIG last-octet handlers, parsed-octet serial command handlers, log strings. **Spawned cleanup items:** FW-C5-FRAME-CLEANUP (retire dead `A1_DEST_*_IP` from `frame.hpp` after TMC's `_mcc[]` dance fix made them unreferenced), TRC-CS-DEAD-IPENDPOINT (retire dead `ipEndPoint` field in `trc.cs`). | `defines.hpp` ✅ `defines.cs` ✅ `mcc.hpp/.cpp` ✅ `bdc.hpp` ✅ `BDC.ino` ✅ `tmc.hpp/.cpp` ✅ `fmc.hpp` ✅ `mcc.cs` ✅ `bdc.cs` ✅ `tmc.cs` ✅ `fmc.cs` ✅ `trc.cs` ✅ |
| FW-C7 | Implement `SET_TIMESRC` at `0xA3` | ⏳ Pending — **byte assigned CB-20260412** | `TIMESRC` serial command exists (FW-2, S30) but has no UDP/ICD equivalent. **Byte assigned: `0xA3`, INT_OPS, all five controllers.** Payload: `0=OFF, 1=NTP, 2=PTP, 3=AUTO`. Routing by IP. Firmware handler + `EXT_CMDS[]` whitelist entry + C# wiring in all five client classes. Resolves FMC NTP operator control without serial access. Unblocks FW-B4 runtime PTP enable. Prerequisite: FW-C8 (handler removal at `0xA3` first). | `defines.hpp`, `defines.cs`, all five controller `.cpp/.hpp`, C# client classes |
| ~~FW-C8~~ | ~~Handler removal pass — all retired/superseded command slots~~ | ✅ **CLOSED** | All retired handlers removed during CB-20260412 session passes (MCC, BDC, TMC, FMC). `0xE4` PMS_RELAY_ENABLE and `0xEC` PMS_VICOR_ENABLE confirmed never implemented in any controller — both hit default. Fleet clean. |
| ~~FW-C10~~ | ~~REG1 CMD_BYTE 0xA1 → 0x00 fleet-wide~~ | ✅ **CLOSED** | All five controllers confirmed: MCC ✅ BDC ✅ TMC ✅ FMC ✅ TRC ✅ — all `buf[0]`/`cmd_byte` set to `0x00` with FW-C10 comment. All C# parsers (`MSG_MCC`, `MSG_BDC`, `MSG_TMC`, `MSG_FMC`, `MSG_TRC`) updated to accept `0x00 \|\| 0xA1` dual-check. `0xA1` now fully available for new assignment. |
| ~~FW-C11~~ | ~~Implement `SET_REINIT` at `0xA9` — MCC and BDC~~ | ✅ **CLOSED** | Confirmed in current source: MCC `mcc.cpp` line 610 ✅, BDC `bdc.cpp` line 1188 ✅. |
| ~~FW-C12~~ | ~~Implement `SET_DEVICES_ENABLE` at `0xAA` — MCC and BDC~~ | ✅ **CLOSED** | Confirmed in current source: MCC `mcc.cpp` line 622 ✅, BDC `bdc.cpp` line 1200 ✅. |
| ~~FW-C13~~ | ~~Implement `SET_CHARGER` at `0xAF` — MCC~~ | ✅ **CLOSED** | Confirmed in current source: MCC `mcc.cpp` line 712 ✅. |
| ICD-1 | ICD INT_ENG update pass — CB-20260412 + BDC HB bytes | ⏳ Pending | Bump ICD to v3.6.0. Full list of changes: **(New)** `0xA1` SET_HEL_TRAINING_MODE, `0xA3` SET_TIMESRC, `0xA9` SET_REINIT, `0xAA` SET_DEVICES_ENABLE, `0xAB` SET_FIRE_VOTE, `0xAF` SET_CHARGER, `0xD1` ORIN_COCO_ENABLE, `0xE0` SET_BCAST_FIRECONTROL_STATUS, `0xB1` SET_BDC_VOTE_OVERRIDE. **(Retired)** `0xA9`, `0xB0`, `0xB1` (old), `0xBE`, `0xD1` (old), `0xD2`, `0xD8`, `0xDF`, `0xE0` (old), `0xE1`, `0xE3`, `0xE6`, `0xED`. **(Scope to INT_OPS)** `0xA2`, `0xA3`, `0xA1`, `0xAB`. **(INT_ENG)** `0xE0` BCAST_FC, `0xB1` VOTE_OVR. Update version history section. Bump ICD document register entry. | `CROSSBOW_ICD_INT_ENG.md`, IPGD-0003 register entry |
| ~~DEF-1~~ | ~~defines.hpp / defines.cs update pass — CB-20260412 enum changes~~ | ✅ **CLOSED** | **Verified CB-20260413.** Both files contain all CB-20260412 enum changes — `SET_TIMESRC=0xA3`, `SET_REINIT=0xA9`, `SET_DEVICES_ENABLE=0xAA`, `SET_CHARGER=0xAF` all added; `SET_HEL_TRAINING_MODE=0xA1`, `ORIN_ACAM_COCO_ENABLE=0xD1`, `SET_BCAST_FIRECONTROL_STATUS=0xE0`, `SET_BDC_VOTE_OVERRIDE=0xB1` all reassigned; all retired names removed (replaced by `RES_xx` rejection markers, both files in lockstep). **Naming note:** slot `0xAB` retains the legacy name `SET_FIRE_REQUESTED_VOTE` from its `0xE6` origin — slot-only move, name preserved to avoid C# call-site churn. ICD-1 to use canonical name `SET_FIRE_REQUESTED_VOTE` in v4.0.0 entries (not the `SET_FIRE_VOTE` shorthand used in the original CB-20260412 spec). | `defines.hpp` ✅ `defines.cs` ✅ |
| ARCH-1 | ARCHITECTURE.md update pass — CB-20260412 | ⏳ Pending | Update: §5 Port reference — note `0xA9`/`0xAA` as new unified fleet commands. §17 Open items — add ICD-1, DEF-1, FW-C8 through FW-C13, FW-C10. Note 0xA1 REG1 CMD_BYTE legacy status. ICD reference bump to v3.6.0 in ARCH header. All controller FW versions → 4.0.0. IsV4 gate documented. **Hardware revision sections:** Each controller section (MCC §9, BDC §10, TMC §?, FMC §12) needs V1/V2 subsections noting platform differences — MCC HW rev (laser/no-laser), BDC V1/V2 (Vicor/TRACO, IP175, new thermistors), TMC V1/V2 (single Vicor/two TRACOs, heater removed, ADS1015 removed), FMC V1/V2 (SAMD21/STM32F7). **CROSSBOW_FW_PATTERNS.md updates to incorporate into ARCH patterns appendix:** (1) platform table FMC row → V1 SAMD21 / V2 STM32F7; (2) line 19 warning update — FMC V2 follows OpenCR pattern; (3) `buildReg01()` example `ICD::GET_REGISTER1` → `0x00`; (4) HPP template `isUnSolicitedEnabled` → retired, replaced by per-client `wantsUnsolicited`. | `ARCHITECTURE.md` |
| UG-1 | CROSSBOW_UG_ENG_GUI_draft.md update pass | 🟡 Partial | TRC section (§4.7) now written CB-20260419b. Remaining: ICD/ARCH version refs; MCC section (LASER_MODEL, HEL training mode, IsV4 gate, charger UI); BDC section (V1/V2 hardware table, IP175, HEALTH_BITS/POWER_BITS rename, new temps, IsV2 layout switching); TMC section (V1/V2 hardware table, PUMP/PIDGAIN serial commands, isSingleLoop); FMC section (V1 SAMD21 / V2 STM32F7 platform note); retired stream controls. | `CROSSBOW_UG_ENG_GUI_draft.md` |
| DOC-REG-1 | CROSSBOW_DOCUMENT_REGISTER.md version bumps | ⏳ Pending | Bump version entries for all documents updated during CB-20260412 and unification sessions: ICD INT_ENG, ICD INT_OPS, ARCHITECTURE.md, UG_ENG_GUI, BDC_HW_DELTA.md, TMC_HW_DELTA.md, FMC_STM32_MIGRATION_FINAL.md. Add new entries for CROSSBOW_CHANGELOG.md v1.2.0 and CROSSBOW_FW_PATTERNS.md v1.7. | `CROSSBOW_DOCUMENT_REGISTER.md` |
| ~~PMC-1~~ | ~~PMC hardware unification session~~ | ✅ **CLOSED CB-20260416** | Completed. | PMC firmware ✅ |
| BIT-CLEANUP | Status bits audit — defines.cs bitmask enums | ⏳ Pending | `HUD_OVERLAY_BITS`, `VOTE_BITS_MCC`, `VOTE_BITS_BDC` use different C# bitmask enum pattern vs `defines.hpp`. Walk through to confirm intentional or align. Related to TMC `tb_*` prefix inconsistency in TIME_BITS. | `defines.cs`, `defines.hpp` |

### MCC

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| FW-B2 | MCC RX-side SEQ gap counter for TMC A1 stream | ⏳ Open | Track per-slot SEQ discontinuities on MCC receive side for TMC A1 stream — consistent with gap counter on BDC/FMC tabs. | `mcc.cpp` — A1 RX handler |
| FW-14 | GNSS socket bug — RUNONCE case 6 and EXEC_UDP socket usage | 🟡 Verify on HW | `RUNONCE` case 6 (line 142): sends UPTIMEB ONCE via `udpRxClient` on `PortRx` (3001) — may be intentional if NovAtel responds on the same port it receives on. Cases 0–5 use `udpTxClient`/`PortTx` (3002). Verify on HW whether UPTIME response arrives correctly. `EXEC_UDP` (line 206): uses `udpRxClient` but sends to `PortTx` (3002) — port is correct but socket object is wrong (naming bug at minimum). Fix `EXEC_UDP` to use `udpTxClient`. | `gnss.cpp` — `RUNONCE()` case 6 (verify), `EXEC_UDP()` (fix socket object) |
| GNSS-WATCHDOG | GNSS isConnected watchdog bench verify | ⏳ Bench verify | Confirm `DEVICE_READY_BITS` bit 6 drops correctly when NovAtel goes silent. 3s timeout (`GNSS_COMMS_TIMEOUT_MS=3000`) confirmed correct in code review S28. Test only — disconnect NovAtel UDP, observe bit 6 clear in ENG GUI. | `gnss.cpp`, `gnss.hpp` — code correct, test only |
| ~~CRG-1~~ | ~~Charger pin D42 polarity — rename and invert logic~~ | ✅ **CLOSED** | `PIN_CRG_ALARM` → `PIN_CRG_OK` in `pin_defs_mcc.hpp`; logic inverted (`== LOW` = alarm) in `mcc.cpp`; serial STATUS and `MCC.ino` updated. Closed CB-20260412 MCC pass. | ✅ |
| CRG-2 | `PIN_CRG_OK` → `isCRG_Ready` on V2 | ⏳ Pending CRG-1 | Map `PIN_CRG_OK` read → `isCRG_Ready()` on V2 so device status panel matches V1. | `mcc.hpp` — `isCRG_Ready()` V2 case |
| CRG-3 | `frmMCC.cs` + designer — `mb_CrgAlarm_rb` control wiring | ⏳ Pending CRG-1 | `frmMCC.cs` + designer: add `mb_CrgAlarm_rb` StatusLabel to `groupBox12`; wire readback to corrected `isCrgAlarm` logic after CRG-1. | `frmMCC.cs`, `frmMCC_Designer.cs` |
| ~~IPG-HB-1~~ | ~~`HB_BAT` always 0 — not wired~~ | ✅ **CLOSED** | `HB_BAT` (REG1 byte [132]) always packs 0. Wire: add `lastMsgRx_ms` to `bat` class, stamp on each received packet, compute delta at `SEND_REG_01()` pack time — same pattern as `ipg.HB_RX_ms`. | `battery.hpp`, `mcc.cpp` `SEND_REG_01()` |
| ~~IPG-HB-HEL~~ | ~~`HB_HEL` (REG1 byte [131]) — verify updating correctly on HW~~ | ✅ **CLOSED** | `HB_HEL` reads `ipg.HB_RX_ms` which is stamped in `parseLine()` — only updates when a TCP line is received and parsed from laser. If laser connected but not actively sending lines, `lastMsgRx_ms` may not be re-stamped and HB grows unbounded. Verify on HW that byte [131] reflects live laser TCP interval. If not updating: stamp `lastMsgRx_ms` at TCP receive level rather than inside `parseLine()`. | `ipg.cpp` — `parseLine()`, `UPDATE()`; `mcc.cpp` — `SEND_REG_01()` byte [131] |
| ~~IPG-HB-2~~ | ~~`HB_GNSS` always 0 — not wired~~ | ✅ **CLOSED** | `HB_GNSS` (REG1 byte [134]) always packs 0. Wire: add `lastMsgRx_ms` to `gnss` class, stamp on each received position fix, compute delta at `SEND_REG_01()` pack time. | `gnss.hpp`, `mcc.cpp` `SEND_REG_01()` |
| ~~IPG-HB-3~~ | ~~`HB_CRG` always 0 — not wired (V1 only)~~ | ✅ **CLOSED** | `HB_CRG` (REG1 byte [133]) always packs 0. V1 only — CRG has no I2C on V2. Implement if CRG polling exists; gate behind `#if defined(HW_REV_V1)`. | `mcc.cpp` `SEND_REG_01()` |
| ~~IPG-HB-HEL-2~~ | ~~Laser HB still 0ms on live HW~~ | ✅ **CLOSED CB-20260416** | Root cause identified and resolved CB-20260416. `lastMsgRx_ms` was not being stamped correctly — fixed and verified on live HW. | `ipg.cpp` ✅ |
| INCL-HB-SCALE | INCL HB saturates at 255ms — scale too fine | 🟢 Low | INCL polls at ~1001ms so HB always saturates uint8 raw ms at 255ms — not useful. Consider changing INCL pack to x0.1s units (÷100 at pack, /10.0 in C# → seconds) giving 0–25.5s range that shows the 1s interval meaningfully. Coordinate: `incl.hpp HB_ms()`, `bdc.hpp HB_INCL()`, `bdc.cpp buf[403]`, `MSG_BDC.cs HB_INCL_ms` type/parse, `frmBDC.cs` format string. | `incl.hpp`, `bdc.hpp`, `bdc.cpp`, `MSG_BDC.cs`, `frmBDC.cs` |
| ~~TRC-SN-LABEL~~ | ~~TRC SOM SN — promote from version label to dedicated tss_trc_sn~~ | ✅ **CLOSED CB-20260416** | SOM serial shown on TRC OSD video overlay (CB-20260413). Removed from THEIA scope — not needed at HMI level. |
| IPG-HB-4 | `HB_NTP` → `HB_TIME` rename — PTP sync not stamped | ⏳ Pending | REG1 byte [130] named `HB_NTP` but should reflect both NTP and PTP receive events. Rename `HB_NTP` → `HB_TIME` in firmware, ICD (byte [130] label), and `MSG_MCC.cs` (`HB_NTP` property). Stamp on PTP sync event in addition to NTP packet receive. Low disruption — existing C# callers update property name only. | `mcc.hpp`, `mcc.cpp`, `MSG_MCC.cs`, `CROSSBOW_ICD_INT_ENG.md` byte [130] |
| ~~IPG-STUBS~~ | ~~Dead `lastTick_*` stubs in `mcc.hpp`~~ | ✅ **CLOSED** | `lastTick_BAT`, `lastTick_CRG`, `lastTick_GNSS` declared but never written in `mcc.hpp`. Either remove or wire up when IPG-HB-1/2/3 implemented. `lastTick_HEL` used. | `mcc.hpp` |

### BDC

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| BDC-1 | Gate Fuji/MWIR comms on relay state | ⏳ Pending | Disable comms to Fuji/MWIR when their relays are off — suppress spurious lost-message errors. Tie to `SET_BDC_RELAY_ENABLE` state in BDC. | `bdc.cpp`, `bdc.hpp` |
| FW-C3 | BDC Fuji boot status — FUJI_WAIT always times out | ⏳ Open | `fuji.SETUP()` and `fuji.UPDATE()` deferred until post-boot. At DONE print, `fuji=---` always shown regardless of physical connection. Fix: run lightweight Fuji ping or move SETUP earlier in boot sequence. | `bdc.cpp` — boot sequence, `fuji.cpp` SETUP() |
| FW-C4 | BDC A1 ARP backoff not working | ⏳ Open | `a1FailCount` not incrementing correctly when TRC offline. Workaround: `A1 OFF` serial command when TRC is offline. Root cause: send failure may not be returned correctly from `frameSend()`. | `bdc.cpp` — A1 TX path, `frameSend()` return value |
| CLEANUP-3 | A3 ACK discrepancy — MCC visible in debug, BDC not | ⏳ Pending | MCC A3 ACK visible in debug output, BDC A3 not — both working. Likely a log level or debug print difference, not a protocol issue. Investigate when on HW. | `bdc.cpp` — A3 handler debug prints |
| ~~BDC-FSM-VOTE-LATCH~~ | ~~`isFSMNotLimited` stale outside ATRACK/FTRACK — vote latches NO-FIRE on track exit~~ | ✅ **CLOSED** | **Opened and closed CB-20260413.** Bug: `isFSMNotLimited` (VOTE_BITS_BDC bit 7, `FSM_NOT_LTD` — inverted logic, bit set = "FSM not limited" = OK) was only updated inside the ATRACK/FTRACK case body of `BDC::PidUpdate()`. The variable is read every telemetry tick to build the broadcast vote bitmask at `bdc.hpp:224`, but the *write* only happened in track mode. On exit from ATRACK/FTRACK with the bit cleared (track point too far off-center → predicted FSM correction exceeds `FSM_ANGLE_MAX_TARGET_SPACE_DEG = 2.0°`), the value stuck at `false` and the broadcast vote kept reporting NO-FIRE until the next track entry recomputed it. User symptom: "FMC fsm limit vote not clearing on the BDC until system goes into track." Fix: compute `isFSMNotLimited` from the FMC FSM position readback (`fmc.fsm_posX_rb` / `fsm_posY_rb` — already extracted at `bdc.cpp:435-436` from FMC REG1 bytes [20-23] / [24-27] via the FW-B5 offset fix) at the top of `PidUpdate()`. Conversion: `(fsm_posX_rb - FSM_X0) * iFOV_FSM_X_DEG_COUNT` gives target-space degrees (matching units of the existing constants), magnitude check via `sqrt(ax_rb² + ay_rb²) <= FSM_ANGLE_MAX_TARGET_SPACE_DEG`. Sign omitted (magnitude only). Gimbal NED offset omitted (we want local FSM angle, not world frame). The ATRACK/FTRACK case body still overwrites with the predictive (track-error-derived) value when actively driving the FSM — the predictive computation leads the readback by one tick, which is the correct behaviour in track mode. In all other modes the readback value persists, so the vote tracks actual FSM angular state instead of latching the last ATRACK predictive value. **Placement note (preserve this design choice):** user moved the `if ((millis() - prev_PID_Millis) < TICK_PID) return;` rate gate from above the readback block to BELOW it — intentional. The FSM limit check is an instantaneous physical state read, not a control-loop concept, and gating it at PID rate would mean some A1 frames carry a vote bit up to one PID period stale. Both the readback fallback and the predictive override live inside `PidUpdate()` together by design — they are two halves of the same FSM-limit decision, paired alongside the existing FSM_X/FSM_Y/Set_FM_POS code. Do not move the rate gate back above the FSM block. Do not hoist either computation out of `PidUpdate()`. | `bdc.cpp` — `BDC::PidUpdate()` ✅ |

### FMC

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| ~~FMC-V1-FSM-0~~ | ~~FMC V1 FSM ADC returns 0,0 when stage connected~~ | ✅ **CLOSED CB-20260416** | Hardware issue — resolved on bench. | Hardware ✅ |
| ~~FW-INFO-HW-REV~~ | ~~HW_REV missing from INFO command~~ | ✅ **CLOSED** | MCC ✅ has `HW_REV=0x%02X` inline on INFO version line. FMC ✅ same. BDC ❌ missing — `INFO` handler in `bdc.cpp` line 1982 has no HW_REV print (present in `REG` line 1913 and `STATUS` line 2816). TMC source not uploaded — needs verification. Fix for BDC: add `Serial.printf("HW_REV:        0x%02X  (%s)\n", BDC_HW_REV_BYTE, ...)` after version line at `bdc.cpp` line 1984. Apply same check+fix to TMC if missing. | `bdc.cpp` line 1984; `tmc.cpp` INFO handler (verify) |
| FMC-15 | `readPos()` I2C clock stretching — can block indefinitely | 🟡 Medium | `Wire.requestFrom()` in `readPos()` blocks until stage releases I2C clock. Stage holds clock during calibration or mid-move. No timeout in SAMD21/STM32 Wire library. Monitor `dt_delta` in heartbeat register. Consider polling only when stage is known idle. | `fmc.cpp` — `readPos()`, `checkStagePos()` |
| FMC-17 | `micros()` rollover — NTP timestamp jump every ~71.6 min | 🟡 Verify | `GetCurrentTime()` uses `uint32_t` `micros()` which rolls over every ~71.6 min. At rollover, `(micros() - microsEpoch)` wraps to large value → ~4295s forward jump until next NTP sync. Not listed as fixed in migration doc, not carried in §4.8. Verify during FMC code pass whether ntpClient.cpp was updated. | `ntpClient.cpp` — `GetCurrentTime()` |
| FMC-CS7 | BDC `SEND_REG_01()` FMC pass-through — verify raw memcpy | 🟡 Verify | Migration CS-7: verify BDC `SEND_REG_01()` passes FMC REG1 block to clients via raw `memcpy` with no field interpretation. May be superseded by FW-B5 offset fix. Confirm during FMC code pass that BDC's fmc.buffer is populated and forwarded correctly. | `bdc.cpp` — `SEND_REG_01()`, `handleA1Frame()` |
| FMC-13 | `scan()` blocks main loop ~3.6s | 🟢 Low | `FMC_FSM_TEST_SCAN` command runs 361-iteration loop with `delay(10)` each. System goes dark to remote host during scan — no UDP, no heartbeat, no NTP. Bench-test use only. Document constraint; do not trigger during live operation. | `fmc.cpp` — `scan()` |
| FMC-14 | `init_FSM()` blocks ~3.4s at boot | 🟢 Low | Contains `delay(1000)` + `delay(100)×2` + `delay(2000)`. Acceptable at startup. Do not trigger via serial re-init during operation. | `fmc.cpp` — `init_FSM()` |
| FMC-18 | Aggregate loop I/O load — monitor `dt_delta` | 🟢 Low | Main loop performs per-cycle: UDP parse, FSM ADC read (50ms), stage I2C poll (100ms), NTP send (10s), heartbeat (20ms). Not a problem currently but monitor `dt_delta` in heartbeat register. If it grows beyond a few ms, stagger I/O timing. | `fmc.cpp` — `UPDATE()` |
| PID-1 | PID gain tuning — overshoot on LCM speed control | 🟡 Open | `kp=50/ki=100/kd=10` causing overshoot on LCM speed PID. Use `PIDGAIN <ch> <kp> <ki> <kd>` serial command for runtime tuning without recompile — calls `SetTunings()` directly on running PID. Tune on bench with hardware present. | `tmc.cpp` — PID gains; `PIDGAIN` serial command |
| T7 | V1 heater verify — no hardware available | 🟢 Low | V1 heater circuit (Vicor + DAC control) was not bench-tested — no V1 heater hardware present at time of TMC unification. Verify `PIN_VICOR_HEAT` enable/disable and DAC trim when V1 hardware is available. | `tmc.cpp` — `EnableVicor(HEAT)`, `SetDAC(HEATER)` |

### FMC

*(No FMC-specific items currently open. FMC-STM32-1, FMC-NTP, SAMD-NTP closed ~S39.)*

| ~~FMC-TPH~~ | ~~BME280 TPH integration — FMC V2~~ | ✅ **CLOSED** | **Bench-verified CB-20260413 on V2 STM32F7 hardware.** Firmware: `tph.hpp` include, `TPH tph` member, `tph.SETUP()`/`UPDATE()`, REG1 pack at [47–58], `PRINT_REG()` and `TEMPS` serial output — all gated `#if defined(HW_REV_V2)`. V1 leaves bytes 0x00 (decodes to 0.0f via existing `memset` in `buildReg01()`). Serial verification: MCU 45.28°C, Ambient 30.79°C, Pressure 100131.88 Pa (≈1001 hPa), Humidity 30.47% — all physically sane. C#: `MSG_FMC.cs` parses three `BitConverter.ToSingle` reads at [47]/[51]/[55]; `TPH_Temp`/`TPH_Pressure`/`TPH_Humidity` properties added; `frmFMC.cs` populates pre-existing `lbl_FMC_tph` designer label gated on `IsV2`; V1 displays "TPH: V1 — n/a". ICD INT_ENG FMC REG1 table updated with three TPH rows tagged v4.0.0 (FMC-TPH); defined-bytes 47 → 59, reserved 17 → 5. | `fmc.cpp` ✅ `fmc.hpp` ✅ `MSG_FMC.cs` ✅ `frmFMC.cs` ✅ `CROSSBOW_ICD_INT_ENG.md` ✅ |

### TRC

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| NEW-38d | TRC PTP integration | ⏳ Pending | TRC uses `systemd-timesyncd` NTP only — no PTP path, no TIME_BITS in REG1. Scope: (1) Linux: install/configure `ptp4l` as PTP slave to NovAtel `.30`; (2) TRC firmware: add TIME_BITS equivalent to REG1; (3) `MSG_TRC.cs`: add `epochTime`, `activeTimeSource`, `activeTimeSourceLabel`. | TRC `udp_listener.cpp`, `MSG_TRC.cs` |
| ~~TRC-SOM-SN~~ | ~~TRC SOM serial number — read and pack into REG1~~ | ✅ **CLOSED** | **Bench-verified CB-20260413.** Format: `uint64 LE` at TelemetryPacket bytes [49-56] (user-specified, supersedes any prior ASCII-string suggestion). Bytes [57-63] remain RESERVED (7 bytes). 8 edits applied across 5 files: `telemetry.h` (struct field + 2 static_asserts for offset 49 and 57), `types.h` (`uint64_t somSerial{0}` added to `GlobalState` after `version_word`), `main.cpp` (read `/proc/device-tree/serial-number` once at startup right after version_word print, parse via `std::stoull` with try/catch fallback to 0, log `"SOM Serial: <n> (raw: \"...\")"` to stderr), `udp_listener.cpp` (`telemetry.som_serial = state_.somSerial` packed in `buildTelemetry()` after Jetson stats), `MSG_TRC.cs` (`SomSerial` UInt64 property added near Jetson health properties; `ParseMsg()` reads 8 bytes via `BitConverter.ToUInt64` then skips 7 RESERVED; layout doc comment updated). User additionally wired `SomSerial` to the TRC on-screen display (OSD overlay) so the SN is visible on the live video stream — bonus addition beyond the surgical change set. ICD INT_ENG TRC REG1 update **held** per user request — tracked separately as TRC-SOM-SN-ICD (low, deferred). | `telemetry.h` ✅ `types.h` ✅ `main.cpp` ✅ `udp_listener.cpp` ✅ `MSG_TRC.cs` ✅ TRC OSD ✅ |
| TRC-A1-CHK | A1 fire control packet byte [3] — checksum not validated | 🟢 Low | `trc_a1.hpp` line 26 + `trc_a1.cpp` line 191: byte [3] of the raw 4-byte `SET_BCAST_FIRECONTROL_STATUS` packet is documented as "reserved / checksum (not validated)" and currently ignored. Define checksum scheme (e.g. XOR of bytes [0-2]) and add validation in `rxThreadFunc` — discard packet and log on mismatch. Coordinate with BDC `SEND_FIRE_STATUS_TO_TRC()` to pack the same checksum at byte [3]. | `trc_a1.cpp` — `rxThreadFunc()`; `bdc.cpp` — `SEND_FIRE_STATUS_TO_TRC()` |
| TRC-COCO-PROD | `--coco-ambient` in production launch | 🟡 Medium | Add `--coco-ambient` flag to `trc_start.sh` once ambient scan validated on live hardware. Confirm COCO model path present in production deployment. Opened CB-20260419. | `trc_start.sh` |
| TRC-COCO-PERF | COCO inference performance exploration | 🟡 Medium | Current baseline: SSD MobileNet V3 Large FP16, 320×320 input, ~20Hz ambient at interval=3. Confirmed CUDA FP16 active on Orin NX. Observed: good detection on live Alvium; degraded on compressed/recorded video (expected — model trained on natural images). `SCORE_THRESHOLD=0.40` and `CONF_THRESHOLD=0.50` hardcoded in `coco_detector.h` — not runtime-tunable. Explore: (1) TensorRT engine conversion for lower inference latency; (2) YOLOv8n/YOLOv8s as drop-in replacement (better aerial/vehicle performance); (3) expose `SCORE_THRESHOLD` via ASCII for live tuning; (4) measure actual `detect()` ms on Orin to confirm inference time vs frame-drop rate. Opened CB-20260419. | `coco_detector.h`, `coco_detector.cpp` |
| COCO-04 | COCO telemetry fields in TRC REG1 | 🟡 Medium | No COCO state currently in the 64-byte TRC REG1 packet — monitoring inference results requires ASCII `COCO STATUS` poll. 5 bytes RESERVED at [59–63]. Proposed: `cocoConfidence uint16` × 10000 at [59–60] (2 bytes), `cocoClassId uint8` at [61] (1 byte), `ambientDetCount uint8` at [62] (1 byte) — uses 4 of 5 reserved bytes, leaves 1 at [63]. Coordinate with `MSG_TRC.cs` parser and ICD TRC REG1 table. Opened CB-20260419. | `telemetry.h`, `udp_listener.cpp`, `MSG_TRC.cs` |
| TRC-COCO-UDP | ORIN_ACAM_COCO_ENABLE via UDP — not yet implemented | 🟢 Low | After CB-20260412, `ORIN_ACAM_COCO_ENABLE = 0xD1`. TRC never had a UDP handler for this command (was at 0xDF, never implemented). Add `case ICD_CMDS::ORIN_ACAM_COCO_ENABLE:` at 0xD1 in `udp_listener.cpp` dispatch when COCO UDP control is needed. Coordinate with `coco_detector.cpp` enable/disable interface. | `udp_listener.cpp` — binary dispatch; `coco_detector.cpp` |
| TRC-MUTEX | `buildTelemetry()` race condition — A1 TX vs A2 binary threads | 🟢 Low | `buildTelemetry()` is called from both `trc_a1.cpp` txThreadFunc (100 Hz) and `udp_listener.cpp` binaryThreadFunc (on solicited request). No mutex guards the shared `telemetry` struct. Benign at current rates — add mutex when threading issues surface. Consider moving to lock-free double-buffer. | `udp_listener.cpp` — `buildTelemetry()`; `trc_a1.hpp` |
| ~~TRC-TRAINING~~ | ~~Training mode visibility — review VOTE_BITS_MCC~~ | ✅ **CLOSED CB-20260416** | Resolved via THEIA-HUD-FIRECONTROL — training mode displayed on THEIA HMI via `mb_isTrainingModeEnabled_rb` and `jtoggle_TRAIN`. OSD overlay deferred to THEIA-HUD-FIRECONTROL session. | `frmMain.cs` ✅ |
| TRC-STATBITS | TRC STATUS_BITS (BDC REG1 byte [59]) review | 🟡 Open | `trc.hpp STATUS_BITS()`: bits 3–6 hardcoded `false` (AF/AE/AG/AWB — camera auto-control, BDC has no visibility into these). `isStarted` (bit 2) never wired — verify in `trc.cpp` or treat as available. `isTRC_A1_Alive` (stream liveness) not packed — only `isConnected` is (latches true, never resets). Proposed: wire `isTRC_A1_Alive` into bit 3; repurpose bits 4–6 or document as reserved. Coordinate with `MSG_TRC.cs` `StatusBits0` accessors. | `trc.hpp` — `STATUS_BITS()`; `trc.cpp` — `isStarted` wiring; `MSG_TRC.cs` — `StatusBits0` accessors |

---

## 🟡 MEDIUM — Software

### ENG GUI (C#)

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| ~~FW-C6~~ | ~~isUnSolicitedMode_Enabled bit retired — C# reads stale bit~~ | ✅ **CLOSED** | Confirmed removed from `MSG_MCC.cs` during CB-20260412 MCC review — line 438 comment confirms retirement. `MSG_BDC.cs` status to be verified during BDC pass. | `MSG_MCC.cs` ✅ — `MSG_BDC.cs` ⏳ verify |
| ~~MSG-CMC-1~~ | ~~`MSG_CMC.cs` ParseMsg — `ICD.RES_A1` stale reference~~ | ✅ **CLOSED** | **Owner-confirmed fixed CB-20260413.** `ParseMsg()` now uses literal dual-check `case (ICD)0x00:` and `case (ICD)0xA1:` to handle both v4.0.0 and legacy pre-FW-C10 REG1 frames. | `MSG_CMC.cs` ✅ |
| ~~CLEANUP-1~~ | ~~Dead code — MCC_STATUS and BDC_STATUS on controller classes~~ | ✅ **CLOSED CB-20260416** | Removed. | `mcc.cs`, `bdc.cs` ✅ |

| CLEANUP-4 | Confirm ping stops correctly at STANDBY transition | ⏳ Pending | `PING_STATUS_*` bools stay at last value when ping loop stops. Verify `CB.MCC_STATUS`/`CB.BDC_STATUS` do not use stale ping state after STANDBY transition. Confirm on HW. | `frmMain.cs` — `PingHB()`, `crossbow.cs` |
| GUI-3 | MSG_BDC.cs activeTimeSource reads from correct bits | ⏳ Open | Verify `activeTimeSource` reads from `TimeBits` (`tb_usingPTP`/`tb_isNTP_Synched`), not `DeviceReadyBits`. `MSG_TMC.cs` — align to `tb_*` prefix naming (cosmetic). | `MSG_BDC.cs`, `MSG_TMC.cs` |
| GUI-5 | lbl_gimbal_hb — gimbalMSG.HB_TX_ms missing | ⏳ Open | `gimbalMSG.HB_TX_ms` property does not exist on `MSG_GIMBAL`. Find correct HB property name and fix binding in `frmBDC`. | `frmBDC.cs`, `MSG_GIMBAL.cs` |
| ~~GUI-6~~ | ~~Rolling max stats to TRC tab~~ | ✅ **CLOSED CB-20260419b** | dt/HB rolling max stats with EMA α=0.10, RX staleness, gap counter, uptime, drop counter — all applied to `frmTRC.cs` matching `frmTMC` pattern. | `frmTRC.cs` ✅ |
| ~~GUI-7~~ | ~~HB and status timing audit — all child devices~~ | ✅ **CLOSED CB-20260416** | Audit complete and verified on live HW. All HB bindings confirmed correct. | `frmBDC.cs`, `frmMCC.cs` ✅ |
| ~~GUI-8~~ | ~~C# client model — apply to TRC~~ | ✅ **CLOSED CB-20260419b** | `trc.cs` fully rewritten: port 10018, INT framing, `BuildA2Frame`/`CrcHelper`, `CrossbowNic` NIC binding, single `0xA4` registration, `KeepaliveLoop`, `isConnected` frame-driven, `DropCount`, `ConnectedSince`, `HB_RX_ms`, `LatestMSG`. **Verified on live HW.** | `trc.cs` ✅ `frmTRC.cs` ✅ |
| NEW-32 | `lch.cs` longitude `% 180.0` before negation | 🟢 Low | Longitude sign negation applied before `% 180.0` modulo — order should be reversed. Fix before operational LCH use. | `lch.cs` — longitude calculation |
| S19-33 | Word ICD version realignment 1.x → 3.x.y | 🟢 Low | Word/docx ICD versions still carry 1.x numbering from early builds. Realign to match .md versions (3.x.y). Part of build spec three-document split (S19-34). | IPGD-0003/0004/0005 .docx |
| S19-34 | Build spec three-document split + integrator tier model | 🟢 Low | Design resolved session 19. Implementation queued. Split build spec into: (1) INT_ENG full, (2) INT_OPS integrator, (3) EXT_OPS external. | Build spec docs |
| S19-35 | Build spec scope labels + new commands | 🟢 Low | Update build spec with ICD v3.x scope label renames (INT_ENG/INT_OPS/EXT_OPS) and all commands added since v3.0.2. | Build spec docs |
| S19-36 | User guide Word build spec | 🟢 Low | Generate .docx from THEIA/ENG GUI/HYPERION user guide .md sources. | User guide .docx outputs |
| S19-37 | Merge with CROSSBOW MINI USER MANUAL v20260205 | 🟢 Low | Merge applicable sections from MINI USER MANUAL into THEIA_USER_GUIDE.md. | `THEIA_USER_GUIDE.md` |
| PARALLAX | Range-based parallax | ⏳ Pending | BDC owns all logic. Two components: (1) VIS FSM home offset — range-dependent, formula/LUT TBD. (2) VIS→MWIR fixed offset — constant delta on `0xD0`. Range source arbitrator: RS232 rangefinder → radar CUE → TRC image estimate. New `targetRange` register in BDC. New `rangeSource` status register. Calibration via ENG GUI + THEIA HMI. | `bdc.cpp`, `bdc.hpp` |
| HMI-COCO | COCO class filter and enable to HMI | ⏳ Pending | Folded into HMI-TRACKER. | — |
| HMI-TRACKGATE | Track gate size persistence on reset | ⏳ Pending | Decision needed: restore last operator-set gate size on tracker reset/reacquisition or reset to default. If persist: THEIA caches last sent `0xD5` values and re-sends on reset. | THEIA `frmMain.cs` |

### Documentation

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| DOC-1 | Add TRC NTP setup reference to ARCHITECTURE.md §2.5 | ⏳ Open | Add `timesyncd.conf` entry (`NTP=192.168.1.33`, fallback `.208`), `timedatectl` verification command, `systemctl restart systemd-timesyncd`. Cross-reference to JETSON_SETUP.md. Assess whether partially addressed by ARCH v3.3.5 update. | `ARCHITECTURE.md` §2.5 |
| DOC-3 | File format specs in ICD INT_ENG and INT_OPS | ⏳ Open | Add file format specifications for horizon files, KIZ/LCH uploads, and survey data to both INT_ENG and INT_OPS ICDs. Currently undocumented — integrators have no reference for file structure. | `CROSSBOW_ICD_INT_ENG.md`, `CROSSBOW_ICD_INT_OPS.md` |
| CROSS-APP-1 | CROSS_APP_SUMMARY.md update pass | 🟢 Low | Document at v3.0.5 (2026-03-17), ICD ref v3.1.0, ARCH ref v3.0.3 — all significantly stale. Update needed: header refs → ICD v3.6.0 / ARCH v3.3.7; §5/§6/§7 `ICD.GET_REGISTER1` dispatch → literal bytes (FW-C10); §8 FW version table → 4.0.0 fleet-wide; §9 defines → v4.0.0; §11 document set → all current versions + new docs; §12 close NEW-9/10/18/31/33/35; §12 add NEW-32, S19-33–37; §13 close #14→FW-14, TRC-M9. Bump document version to 4.0.0. | `CROSS_APP_SUMMARY.md` |
| GST-1 | GSTREAMER_INSTALL.md update pass — retired command references | 🟡 Open | §8 Multicast: references `0xD1 ORIN_SET_STREAM_MULTICAST` as pending action item — command **retired** in CB-20260412. Multicast already works via `--dest-host 239.127.1.21` launch flag (per TRC README). Rewrite §8 to document current working multicast path. §11 30fps: references `0xD2 ORIN_SET_STREAM_60FPS` as pending — **retired** (RES_D2). 30fps now ASCII-only via `FRAMERATE 30`. TRC binary name `multi_streamer` → `trc` throughout. Pipeline parameters (buffer-size, latency, PixelShift -420) confirmed correct — no changes needed there. | `GSTREAMER_INSTALL.md` — §8, §11, binary name |
| ~~PROG-STATE~~ | ~~FW Programmer user guide — STATE command wrong values~~ | ✅ **CLOSED** | Fixed: `0xAA=MAINT, 0xFF=FAULT` → `4=MAINT, 5=FAULT`. Matches canonical `defines.hpp` SYSTEM_STATES enum corrected session 15. | `USER_GUIDE-CROSSBOW_PROGRAMMER.md` ✅ |
| PROG-UG-1 | FW Programmer user guide update pass | 🟡 Open | Multiple staleness issues: (1) FW version examples `v3.0.0`/`v2.1.0` → 4.0.0; (2) PUMP/HEAT/DAC PUMP commands not marked TMC V1 only (V2 TRACO PSUs have no DAC trim, heater removed); (3) VICOR ch=3 HEAT not marked V2 removed; (4) BDC V2 new serial commands missing (HW, POWER, SWRESET, SWDISABLE, CAM, VIEW, TRACKER, FSM, STAGE); (5) FMC V2 (STM32F7/OpenCR) missing from STM32 programming section. | `USER_GUIDE-CROSSBOW_PROGRAMMER.md` |
| ~~PROG-TRC-PATH~~ | ~~FW Programmer — TRC binary name, path, and startup script corrections~~ | ✅ **CLOSED** | `frmFWProgrammer.cs`: `JETSON_TRACKER_PATH` → `/home/ipg/CV/TRC/trc`; startup → `~/CV/TRC/trc_start.sh`; all `trackCntrl.exe` grep/log strings → `trc`; version comment updated. User guide §3 corrections tracked under PROG-UG-1. | `frmFWProgrammer.cs` ✅ |

---

## 🟢 LOW / Deferred

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| FSM-1 | FSM deadband and slew rate limiter | ⏳ Deferred | FSM deadband (~2–4px / 130–260 counts) and slew rate limiter (~2000 counts/step at 50Hz) — prevents jitter and mechanical stress at low error signals. | `bdc.cpp` `PidUpdate()`, `fmc.cpp` `write_x_pos()`/`write_y_pos()` |
| ~~IPG-SENTINEL~~ | ~~`ipg.hpp` sentinel values — `hk_volts`/`bus_volts` = `5.5f`~~ | 🚫 **DELETED** — will not implement. |
| IPG-ROPS | 6K ch2 output power (`ROPS`) — not in current poll | 🟢 Low | YLM-6K supports `ROPS` command for channel 2 output power readback. Not in current firmware POLL loop — future extension if dual-channel monitoring required. No action until 6K system is in field use. | `ipg.cpp` — `POLL()` loop |
| BDC-2 | Fuji startup comms errors | ⏳ Deferred | Spurious comms errors during Fuji VIS camera settling after boot — system recovers automatically. | `bdc.cpp` — Fuji comms init |
| TRC-M9 | Deprecate TRC port 5010 | ⏳ Deferred | Legacy 64B binary port. Remove from TRC firmware and C# after HW validation confirms port 10018 fully operational. | TRC `udp_listener.cpp`, relevant C# client |
| TRC-MUTEX | buildTelemetry() race condition | ⏳ Deferred | Mutex on `buildTelemetry()` race condition. Linux threading means concurrent access to telemetry struct is possible. Low priority. | TRC `udp_listener.cpp` |
| ~~DEPLOY-3~~ | ~~Sustained bench test~~ | ✅ **CLOSED CB-20260416** | All five controllers running simultaneously — bench test complete. |
| DEPLOY-4 | Verify .33 GPS lock before mission | ⏳ Pending | Confirm Phoenix Contact FL TIMESERVER has GPS lock (LOCK LED steady) before relying on it as primary NTP/Stratum 1. Without GPS lock degrades to internal oscillator. | — |
| ~~DEPLOY-5~~ | ~~NovAtel GNSS (.30) — PTP configuration per production system~~ | ✅ **CLOSED CB-20260416** | Configuration procedure documented in CROSSBOW_GNSS_CONFIG.md (IPGD-0018) — `PTPMODE ENABLE_FINETIME` → `PTPTIMESCALE UTC_TIME` → `SAVECONFIG`. Applied and verified on bench unit. Each production unit requires same procedure at commissioning. |
| ~~DEPLOY-6~~ | ~~IGMP snooping — verify switch compatibility for PTP multicast~~ | ✅ **CLOSED CB-20260416** | Verified on production switch. No issues with PTP multicast. |
| ARCH-FMC-HW | ARCH §12.1 FMC Hardware table — V1/V2 column refactor | 🟢 Low | Opened CB-20260413. ARCH §12.1 FMC Hardware table currently has a single column. Refactor to V1/V2 columns parallel to the TMC §11.3 pattern, with a BME280 V2 row added (now that FMC-TPH is closed and the BME280 is part of the V2 build). Documentation cleanup, no functional impact. Pairs naturally with ARCH-1 if that's the next ARCH pass. | `ARCHITECTURE.md` §12.1 |
| FW-C5-FRAME-CLEANUP | Retire dead `A1_DEST_*_IP` defines from `frame.hpp` | 🟢 Low | Opened CB-20260413. After FW-C5's TMC pass, `A1_DEST_MCC_IP` (line 97) and `A1_DEST_BDC_IP` (line 98) in `frame.hpp` are both unreferenced. `A1_DEST_MCC_IP` had exactly one consumer (the `_mcc[]` temp-array dance in `tmc.cpp:21–22`, now cleaned up to `IPAddress(IP_MCC_BYTES)`); `A1_DEST_BDC_IP` was already unreferenced before this session. Both were left in place per FW-C5 option (a) "leave frame.hpp alone" rule. One-line cleanup: delete both `#define` lines and the surrounding "Fixed destinations for A1 TX" comment block. While in there, also refresh the now-stale comment at `tmc.hpp:235` ("`A1_DEST_MCC_IP from frame.hpp`") and the stale TODO at `fmc.hpp:188` ("NOTE: add `A1_DEST_BDC_IP = {192,168,1,20}` to frame.hpp if not already defined"). Dead code, harmless to leave but cleaner to remove. | `frame.hpp` lines 96–98; `tmc.hpp:235`; `fmc.hpp:188` |
| ~~TRC-CS-DEAD-IPENDPOINT~~ | ~~Retire dead `ipEndPoint` field in `trc.cs`~~ | ✅ **CLOSED CB-20260419b** | Removed in full `trc.cs` rewrite — field, assignment, and commented reference all gone. | `trc.cs` ✅ |
| ~~TRC-SOM-SN-ICD~~ | ~~TRC REG1 ICD entry for `som_serial` field~~ | ✅ **CLOSED** | **Closed CB-20260413.** TRC REG1 row added to `CROSSBOW_ICD_INT_ENG.md`: split `[49-63] RESERVED 15 bytes` into `[49-56] som_serial uint64 LE` (tagged `v4.0.0 (TRC-SOM-SN)`, with note about `/proc/device-tree/serial-number` source and `std::stoull` parse) + `[57-63] RESERVED 7 bytes`. Defined / Reserved totals: 49 / 15 → 57 / 7. ICD INT_ENG header version held at 3.6.0 (ICD-1 will do the v4.0.0 rename pass for the whole document). | `CROSSBOW_ICD_INT_ENG.md` ✅ |

---

## Reference — ICD Command Space Summary (CB-20260412)

| Byte | Assignment | Scope | Notes |
|------|------------|-------|-------|
| `0xA1` | SET_HEL_TRAINING_MODE | INT_OPS | Moved from `0xAF`. Legacy REG1 CMD_BYTE role cleared (FW-C10 pending). |
| `0xA3` | SET_TIMESRC | INT_OPS | New — pending FW-C8 (rejection handler removal) before live. |
| `0xA9` | SET_REINIT | INT_OPS | New unified — replaces `0xB0`+`0xE0`. MCC+BDC, routing by IP. FW-C11 pending. |
| `0xAA` | SET_DEVICES_ENABLE | INT_OPS | New unified — replaces `0xBE`+`0xE1`. MCC+BDC, routing by IP. FW-C12 pending. |
| `0xAB` | SET_FIRE_VOTE | INT_OPS | Moved from `0xE6`. Promoted to INT_OPS. |
| `0xAF` | SET_CHARGER | INT_OPS | New merged — replaces `0xE3`+`0xED`. MCC V1 only. FW-C13 pending. |
| `0xC4` | RES | — | Candidate for AWB command (HMI-AWB). |
| `0xD1` | ORIN_COCO_ENABLE | INT_OPS | Moved from `0xDF`. TRC binary: needs impl. |
| `0xE0` | SET_BCAST_FIRECONTROL_STATUS | INT_ENG | Moved from `0xAB`. Internal vote sync MCC→BDC→TRC. |
| `0xB1` | SET_BDC_VOTE_OVERRIDE | INT_ENG | Moved from `0xAA`. BDC ENG block. |
| **Freed this session** | `0xA9`(old), `0xB0`, `0xB1`(old), `0xBE`, `0xD1`(old), `0xD2`, `0xD8`, `0xDF`, `0xE0`(old), `0xE1`, `0xE3`, `0xE6`, `0xED` | — | All pending FW-C8 handler removal where applicable. |
| **Available (clean)** | `0xA3`(after FW-C8), `0xC0`, `0xC3`, `0xC4`, `0xC5`, `0xC6`, `0xCF`, `0xD2`, `0xD8`, `0xDF`, `0xE1`, `0xE5`, `0xEE`, `0xF8`, `0xF9`, `0xFD` | — | `0xA3` available only after FW-C8. Others clean. |
| **Available (needs FW-C8)** | `0xA3`, `0xB0`, `0xBE`, `0xE0`, `0xE3`, `0xE4`, `0xE6`, `0xEC`, `0xED` | — | Handler removal required before new assignment. |
| **Awaiting confirmation** | `0xBF`, `0xCF`, `0xEF`, `0xFF` | — | May be outbound response CMD_BYTEs — firmware check required. |

---

## Reference — W5500 Socket Budget

| Controller | PTP disabled | PTP enabled | ptp.INIT() | Notes |
|---|---|---|---|---|
| MCC | 6/8 | 8/8 | Gated ✅ | udpA1, udpA2, udpA3, gnss.rx:3001, gnss.tx:3002, HEL |
| BDC | 7/8 | 7/8 | Unconditional ⚠️ | udpA1, udpA2, udpA3, gimbal×2, ptp×2. Needs gate — FW-B4 |
| TMC | 4/8 | 4/8 | Unconditional ⚠️ | udpA1, udpA2, ptp×2. Needs gate — FW-B4 |
| FMC | 2/8 | 4/8 | Gated ✅ | udpA1, udpA2. STM32F7 (V2). Gated for FW-B3, not SAMD reason |
| TRC | N/A | N/A | N/A | Linux kernel manages sockets |

---

## Reference — Firmware and ICD Versions

| Item | Value |
|------|-------|
| ICD INT_ENG | v3.5.2 — 2026-04-11 (IPGD-0003) |
| ICD INT_OPS | v3.3.8 (IPGD-0004) |
| ICD EXT_OPS | v3.3.0 (IPGD-0005) |
| ARCH | v3.3.7 — 2026-04-11 (IPGD-0006) |
| MCC firmware | v3.3.0 — STM32F7, OpenCR |
| BDC firmware | v3.3.0 — STM32F7, OpenCR |
| TMC firmware | v3.3.0 — STM32F7, OpenCR |
| FMC firmware | v3.3.0 — STM32F7, OpenCR (V2 board) |
| TRC firmware | v3.0.2 — Jetson Orin NX, Linux 6.1 |
| NTP primary | 192.168.1.33 — Phoenix Contact FL TIMESERVER (HW Stratum 1, GPS-disciplined) |
| NTP fallback | 192.168.1.208 — Windows HMI (w32tm) |
| PTP grandmaster | 192.168.1.30 — NovAtel GNSS (IEEE 1588, domain 0, 1Hz sync, 2-step) |
| PTP status | Disabled fleet-wide — FW-B3 workaround |

---

# PART 3 — CLOSED ITEMS

*Most recent first. Within each session: FW → SW → Docs.*

---

## CB-20260419 — TRC COCO ambient, OSD redesign, GPU telemetry, NMS/area filter
**ARCH:** v4.0.3 | **ICD:** v4.0.3 (TRC REG1 [57–58] jetsonGpuLoad; COCO/ENG ASCII commands)
**Files:** `compositor.cpp`, `osd.cpp`, `osd.h`, `camera_base.h`, `coco_detector.h`, `coco_detector.cpp`, `alvium_camera.h`, `mwir_camera.h`, `udp_listener.cpp`, `udp_listener.h`, `main.cpp`, `compositor.h`, `telemetry.h`

### Compositor — COCO push/poll outside tracker block (bug fix)

COCO push/poll and ambient draw were inside `if (camera->isTrackerEnabled())` — ambient inference never fired when tracker was off. Moved COCO push/poll block and `drawCocoAmbientBoxes()` call to after the tracker block. Track-specific draws (bbox, COCO detbox, LK overlay, reticle, cue chevrons) remain inside tracker block. `trackerActive` and `trackerBbox` remain in scope at the new location. **Root cause of ambient scan producing zero detections on live camera.**

### Compositor — ambient detection hold on no-detection frame

Previously, a no-detection inference result cleared `ambientDetections_` immediately. Changed to hold the last known list on ambient no-detection — avoids OSD flicker in busy scenes where the model briefly misses between good frames. Track mode (non-ambient) still clears drift/detbox on no-detection as before.

### OSD — layout redesign

**Top-right fixed-width block (4 rows, new):**
- `STATE: %-6s` — WHITE/DIM_GREY/BLUE/GREEN/YELLOW/RED for OFF/STNDBY/ISR/COMBAT/MAINT/FAULT
- `MODE:  %-6s` — WHITE/BLUE/BLUE/YELLOW/GREEN/GREEN for OFF/POS/RATE/CUE/ATRACK/FTRACK
- `MCC:   0xAA` — DIM_GREY/RED/WHITE/ORANGE/YELLOW/GREEN by priority: zero/FIRING/TRIGGER/ARMED/ABORT/idle
- `BDC:   0xBB` — DIM_GREY/RED/YELLOW/GREEN by FSM+geometry state
- Anchor computed from `getTextSize("STATE: COMBAT ")` — column never shifts as values change
- Bit checks inline (FC namespace not yet declared at drawText call site)
- BLUE defined as `cv::Scalar(255, 128, 0)` BGR — verify on hardware

**Removed** STATE/MODE/FC from left column.

**3 COCO rows below TRACK (new, only shown when model loaded):**
- `COCO AMB: N dets  [idx]` (WHITE) / `scanning...` (YELLOW) / `off` (DIM_GREY)
- `COCO SEL: classname conf` (GREEN) / `none` (DIM_GREY)
- `COCO TRK: OK/DRIFT/off` (GREEN/ORANGE/DIM_GREY)

**Bottom-right row updated:** `JTEMP: 45C  JCPU: 23%  JGPU: 67%` — added JGPU.

`OSD::render()` and `OSD::drawText()` signatures updated: added `int jetsonGpuLoad` parameter (6th arg). `osd.h` declarations updated to match.

### camera_base.h — trackbox reset fixes

`requestTrackerOff()`: added `trackBoxW_.store(256); trackBoxH_.store(256)` — resets gate size to default on track exit. Previously only cx/cy were reset.

`resetAmbientCycle()`: added W/H/Cx/Cy reset to defaults — fixes disappearing/tiny gate after COCO NEXT + RESET (NEXT sets gate to detection box size which may be small).

### COCO NMS + area filter

**New runtime tunables** on `CocoDetector` with `std::atomic<float>` backing:
- `nmsThreshold_` — default `0.35` (NMS was effectively disabled before; `detect()` was called with default `nmsThreshold=0.0` which passes all overlapping boxes)
- `minAreaFrac_` — default `0.002` (~1850 px² on 1280×720)
- `maxAreaFrac_` — default `0.50` (half frame)

`net_->detect()` now passes `nmsThreshold_.load()` explicitly. Area filter applied post-detect before building `result->detections` — filtered list used for both best-detection selection and ambient detections vector. Filter is resolution-independent (fraction of frame area) so applies equally to full-frame ambient and intra-box track crops.

**ASCII commands:** `COCO NMS`, `COCO MINAREA`, `COCO MAXAREA` — all range-clamped 0.0–1.0, logged to dlog.

**Implementation:** tunables stored as atomics in `CocoDetector`. Camera-base has non-pure-virtual setters (default stores to `camera_base` atomics). `AlviumCamera` and `MwirCamera` override to also forward to `cocoDetector_` — same pattern as `setCocoDriftThreshold`. Getters read from camera_base atomics (reflected in `COCO STATUS` output).

`COCO STATUS` updated to include: `nms=X  minArea=X  maxArea=X`.

### GPU telemetry

**Sysfs source:** `/sys/devices/platform/gpu.0/load` — returns 0–1000, divide by 10 for %. Path confirmed on JetPack 6.2.2 / Orin NX (note: `/sys/devices/gpu.0/load` does not exist on this platform).

**Stats thread:** `readJetsonGpuLoad()` added alongside existing `readJetsonCpuLoad()`. Both now polled every 1s (previously CPU was every 5s). **Complementary filter** applied to both: `filtered = 0.3 × new + 0.7 × previous` — ~3s time constant, smooths jitter without masking load spikes. Temperature remains on 30s cycle.

**Chain:** sysfs → `compositor.jetsonGpuLoad` atomic → `OSD::render()` → `JGPU: N%` display, and → `udp_listener.buildTelemetry()` → `telemetry.jetsonGpuLoad` at REG1 bytes [57–58].

**TelemetryPacket:** `RESERVED[7]` at [57–63] split to `jetsonGpuLoad int16` at [57–58] + `RESERVED[5]` at [59–63]. `static_assert` for RESERVED updated: 57 → 59. New `static_assert(offsetof(TelemetryPacket, jetsonGpuLoad) == 57)` added. **`make clean && make` required.**

### `--coco-ambient` launch flag

`Args::cocoAmbient` flag added to `main.cpp`. Triggers `cocoLoadModel()` + `setCocoAmbientEnabled(true)` on boot. Block moved to **after** `compositor.start()` with 500ms settle delay — ensures camera frames are flowing before first ambient push fires. Non-fatal on model load failure (warning logged, TRC continues). Default: off.

### ICD updates (CB-20260419)

- **TRC REG1:** `jetsonGpuLoad int16` at [57–58] tagged `v4.0.3 (CB-20260419)`. RESERVED shrinks [7] → [5] at [59–63]. Defined: 57 → 59. Reserved: 7 → 5.
- **COCO ASCII table:** Full rewrite — AMBIENT ON/OFF, TRACK ON/OFF, NEXT, PREV, RESET, NMS, MINAREA, MAXAREA, updated STATUS fields. FILTER no longer marked "not yet implemented".
- **ENG Debug Injection section** (new): STATE, MODE, FCVOTES commands documented.
- **Example Bash Usage:** Full rewrite — COCO ambient workflow, NMS/area tuning, correct telemetry byte map, FC symbology checkout sequence, OSD colour reference table.

### Open items from this session

| ID | Item | Priority |
|----|------|----------|
| TRC-COCO-PROD | Add `--coco-ambient` to `trc_start.sh` production launch once validated | 🟡 Medium |
| TRC-COCO-PERF | COCO inference performance exploration. Current: SSD MobileNet V3 Large FP16, ~20Hz ambient at interval=3. Observed: good detection on live Alvium; degraded on compressed/recorded video (expected — model trained on natural images). `SCORE_THRESHOLD=0.40` and `CONF_THRESHOLD=0.50` hardcoded in `coco_detector.h` — not yet runtime-tunable. Explore: (1) TensorRT engine conversion for lower latency; (2) YOLOv8n/YOLOv8s as drop-in replacement; (3) expose `SCORE_THRESHOLD` via ASCII; (4) measure actual `detect()` ms on Orin to confirm inference time vs frame-drop rate. | 🟡 Medium |
| COCO-04 | COCO telemetry in TRC REG1 — no COCO fields currently in the 64-byte packet. 5 bytes RESERVED at [59–63]. Candidates: `cocoConfidence uint16` × 10000 (2 bytes), `cocoClassId uint8` (1 byte), `ambientDetCount uint8` (1 byte) — uses 4 of 5 reserved bytes. Useful for ground station monitoring without ASCII STATUS poll. Coordinate with C# `MSG_TRC.cs` parser. | 🟡 Medium |
| ICD-1 | ICD v4.0.0 rename pass — full document version bump, all session tags aligned | 🟡 Medium |
| ARCH-TRC-19 | ARCHITECTURE.md §8 TRC — update for GPU telemetry, COCO NMS/area filter, OSD layout | ✅ Closed CB-20260419 |
| TRC-ASCII-SEC | Subnet allowlist `192.168.1.0/24` at top of `processAsciiCommand()` | 🟡 Medium |
| TRC-CMD-COMMENT | Fix `0xB8`/`0xB9` comments in `udp_listener.cpp` → `0xA5`/`0xA6` | 🟢 Low |
**Files:** `defines.hpp`, `bdc.hpp`, `trc.hpp`, `trc.cpp`, `bdc.cpp`, `udp_listener.cpp`, `types.h`, `defines.cs`, `bdc.cs`

**AWB-ENG complete.** `CMD_VIS_AWB` assigned to `0xC4` (reserved slot — was `RES_C4`). Full implementation across all seven files:

- `defines.hpp` — `RES_C4` → `CMD_VIS_AWB = 0xC4` with comment `// none — trigger VIS auto white balance once (HMI-AWB)`
- `defines.cs` — `RES_C4 = 0xC4` → `CMD_VIS_AWB = 0xC4` — C# enum parity with `defines.hpp`
- `bdc.hpp` — `0xC4` added to `EXT_CMDS_BDC[]` camera group: `0xC1, 0xC2, 0xC4, 0xC7, 0xC8`
- `trc.hpp` — `SET_AWB()` declaration added after `SET_VIEW_MODE()`
- `trc.cpp` — `SET_AWB()` implementation added: sends `CMD_VIS_AWB` (0xC4) as 1-byte no-payload frame via `EXEC_UDP`. TRC ASCII equivalent: `AWB`
- `bdc.cpp` — three edits: (1) UDP handler `case ICD::CMD_VIS_AWB` dispatches to `trc.SET_AWB()`; (2) serial command `AWB` added before `// -- MCC` block; (3) HELP text line added in TRC COMMANDS section
- `bdc.cs` — `TriggerAWB()` method added alongside VIS camera commands: `Send((byte)ICD.CMD_VIS_AWB)` — no-payload pattern matching `ResetTrackB()` and `GimbalPark()`
- `udp_listener.cpp` — binary handler added after `CMD_MWIR_NUC1` block: `case ICD::CMD_VIS_AWB` calls `cam->runAutoWhiteBalance()`. Mirrors existing ASCII path.

**ICD_CMDS alias retired.** `types.h` contained `using ICD_CMDS = ICD;` — a redundant alias of the canonical `enum class ICD` in `defines.hpp`. Global find/replace of `ICD_CMDS` → `ICD` applied across all TRC-side source files. Alias removed from `types.h`. All case labels and casts in `udp_listener.cpp` now reference `ICD::` directly, consistent with BDC/FMC firmware convention.

**AWB-HMI** (THEIA HMI Xbox controller binding) remains open — depends on AWB-ENG (now complete).

**Items closed:** HMI-AWB (AWB-ENG sub-step)
**Items opened:** none

---

## CB-20260416d — ICD command matrix visualization + 0xAF description fix
**Files:** `CROSSBOW_ICD_INT_ENG.md`

**ICD command matrix format established.** 6×16 color-coded grid (rows 0xA_–0xF_, columns _0–_F) adopted as the canonical quick-reference view for the full command space. Color legend: INT_OPS (blue), INT_ENG (purple), available (grey), outbound slot (yellow), retired (red), retiring this session (orange), needs impl (green), conflict/notable (amber border), candidate (green dashed), awaiting confirmation (dotted). Hover titles carry detail. Produced as an HTML widget — to be regenerated at the start of any ICD review session.

**Notable flags in current matrix:**
- **0xAF** — ICD description stale: still says "V2 returns STATUS_CMD_REJECTED". Fix: V2 now GPIO enable only (FW-CRG-V2 closed CB-20260416). ICD text correction due under ICD-1.
- **0xA3** — candidate: byte assigned, FW-C7 not implemented.
- **0xC4** — candidate: proposed for AWB (HMI-AWB pending).
- **0xD9** — needs impl: COCO class filter, TRC binary handler not written.
- **0xBF/0xCF/0xEF/0xFF** — awaiting confirmation: may be outbound response CMD_BYTEs.

**Items closed:** none
**Items opened:** none

---

## CB-20260416c — End-of-session closures

| ID | Item | Resolution |
|----|------|------------|
| FW-CRG-V2 | MCC V2 SET_CHARGER firmware fix | ✅ Flashed and bench-verified. `EnableCharger(true)` path confirmed working on V2. |
| HW-CRG-V2-OPTO | V2 charger opto sticking | ✅ Root cause: mis-wire. Corrected on bench. Charger enable/disable reliable on V2. |
| FMC-V1-FSM-0 | FMC V1 FSM ADC returns 0,0 when stage connected | ✅ Hardware issue — resolved on bench. |
| TRC-TRAINING | Training mode visibility to TRC | ✅ Training mode displayed on THEIA HMI via `mb_isTrainingModeEnabled_rb` / `jtoggle_TRAIN`. OSD overlay aspect deferred to THEIA-HUD-FIRECONTROL. |
| PMC-1 | PMC hardware unification session | ✅ Completed. |
| CLEANUP-1 | Dead MCC_STATUS / BDC_STATUS on controller classes | ✅ Removed from `mcc.cs`, `bdc.cs`. |

---

## CB-20260416b — BDC tracker PID blind to track position

| ID | Item | Resolution |
|----|------|------------|
| TRC-PID-BLIND | `trc.cpp UPDATE()` cmd_byte gate never passes on FW-C10 firmware — PID always gets stale zero track position | ✅ `buffer[0] == 0xA1` → `buffer[0] == 0x00 \|\| buffer[0] == 0xA1`. Comment updated. Verified root cause: outbound REG1 path (memcpy) was always correct; only PID typed-field extraction was broken. |

---

## CB-20260416 — THEIA HMI IBIT audit + MCC charger V2 fix

| ID | Item | Resolution |
|----|------|------------|
| THEIA-MCC-1 | MCC power bits V1/V2 aware in frmMain | ✅ Power indicators updated — V1/V2 visibility and state logic applied. Color convention: Grey=N/A, Green=good, Yellow=partial, Red=off-when-applicable. |
| THEIA-MCC-2 | MCC PTP device row missing from IBIT matrix | ✅ `mb_MCC_Dev_Enabled/Ready_PTP` added to device matrix. |
| THEIA-MCC-3 | MCC HB counters absent from IBIT | ✅ All five MCC HBs (NTP/HEL/BAT/CRG/GNSS) added to `mb_MCC_Dev_Ready_*` `.Text` labels. |
| THEIA-MCC-4 | Training mode — wired in frmMain | ✅ `jtoggle_TRAIN_CheckedChanged` → `SetHELTrainingMode()`. `mb_isTrainingModeEnabled_rb` Yellow/Grey readback added. |
| THEIA-MCC-5 | Missing MCC vote bits (isBDA, isLaserTotalHW, EMON) | ✅ Closed — covered by existing displays at THEIA level. |
| THEIA-MCC-6 | MCC temperatures absent | ✅ Closed — MCU temp folded into `mb_PingStatus_MCC` label. |
| THEIA-BDC-1 | BDC power bits commented out | ✅ Uncommented and cleaned. Stale checkbox dependency removed. `mb_BDC_Relay4Enabled_rb` added (FMC power). |
| THEIA-BDC-2 | BDC PTP device row missing from IBIT matrix | ✅ `mb_BDC_Dev_Enabled/Ready_PTP` added to device matrix. |
| THEIA-BDC-3 | BDC stale sub-message HBs + missing HB counters | ✅ Stale `fmcMSG.HB_ms`/`trcMSG.HB_ms` replaced with BDC firmware counters [396–403]. All 8 BDC HBs wired into `mb_BDC_Dev_Ready_*` `.Text` labels. |
| THEIA-BDC-4 | KIZ/LCH loaded/enabled/timeValid + BDCTotalVote | ✅ Closed — `tssStatus2_isInKIZ`/`tssStatus2_isInLCH` already encode full vote outcome. Unloaded = bad vote = Red. No additional indicators needed at THEIA level. |
| THEIA-BDC-5 | BDC temperatures absent | ✅ Closed — MCU temp folded into `mb_PingStatus_BDC` label. Jetson temp into `mb_PingStatus_TRC`. |
| THEIA-BDC-6 | TRC SOM serial + BDC HW_REV label | ✅ Closed — SOM serial removed from THEIA (not needed at this level). HW_REV folded into all five `mb_PingStatus_*` labels. |
| THEIA-HUD-LASERMODEL | Laser model display | ✅ Closed — implicit in `tss_status_hel_power` setting/max format. Model name not needed on HUD or status bar. |
| THEIA-HEL-POWER | `tss_status_hel_power` format + `lg_mcc_batt_asoc` | ✅ Power label updated to `"sssss/mmmmm W"` fixed-width 5-digit format (setting/max or actual/max). `lg_mcc_batt_asoc` removed — was incorrectly wired to laser setpoint, not battery SOC. |
| IPG-HB-HEL-2 | Laser HB still 0ms on live HW | ✅ Root cause identified and resolved CB-20260416. Verified on live HW. |
| MSG-TMC-HWREV | Expose HW_REV on MSG_TMC | ✅ Already present — `HW_REV`, `IsV1`, `IsV2`, `HW_REV_Label` all exist in `MSG_TMC.cs`. No change needed. |

---

## CB-20260412 — ICD Restructuring + MCC Review Session

| ID | Item | Resolution |
|----|------|------------|
| CLEANUP-2 | WorstStatus() — confirm no remaining callers in crossbow.cs | ✅ Confirmed — no callers remain. `WorstStatus()` safely deleted. |
| FW-C9 | 0xAF slot conflict — assign new byte for SET_TIMESRC | ✅ Resolved — `0xA3` assigned as SET_TIMESRC (INT_OPS, all five controllers). `0xAF` correctly stays as SET_HEL_TRAINING_MODE (moved to `0xA1`). FW-C7 updated with byte assignment. |
| TRC-MULTICAST | Video multicast 0xD1 not deployed | ✅ Retired — `0xD1` slot repurposed for ORIN_COCO_ENABLE (moved from `0xDF`). Multicast is compile/launch time config only — not runtime-controllable via UDP. Slot freed. |
| TRC-FRAMERATE | 30fps option 0xD2 not deployed | ✅ Retired — `0xD2` slot freed. Framerate is compile/launch time config only. ASCII `FRAMERATE 30` on port 5012 covers all ENG use. Binary handler was never implemented. |
| FW-C6 | isUnSolicitedMode_Enabled bit retired — C# reads stale bit | ✅ Confirmed removed — `MSG_MCC.cs` CB-20260412 MCC review: removal confirmed at line 438 comment. `STATUS_BITS` bit 7 access gone. `MSG_BDC.cs` to be verified during BDC pass. |
| OQ-5 | BDC and FMC defines.hpp — deploy merged canonical file | ✅ Closed (confirmed in synced MCC delta doc) — fleet canonical `defines.hpp` deployed to all controllers. |
| OQ-6 | defines.cs parity: TMC_VICORS, FRAME_KEEPALIVE, BDC_DEVICES.PTP | ✅ Closed (confirmed in synced MCC delta doc) — `TMC_VICORS.PUMP1=2/PUMP2=4`, `FRAME_KEEPALIVE=0xA4`, `BDC_DEVICES.PTP=7` all verified. |
| HW-1 | GIM_VICOR (A0) polarity — verify on V2 bring-up | ✅ Confirmed — HIGH=ON (NC opto, polarity inverted vs V1). `hw_rev.hpp` polarity macros corrected. |
| HW-2 | TMS_VICOR (pin 20) polarity — verify on V2 bring-up | ✅ Confirmed — HIGH=ON (NC opto). Pin 20, not pin 83 as initially assumed. |
| PIN-SWAP | Pins 83/20 swap assumption | ✅ Confirmed not a swap — functions changed, pin numbers same as V1. `pin_defs_mcc.hpp` corrected accordingly. |
| CRG-1 | Charger pin D42 polarity — rename and invert logic | ✅ Implemented — `PIN_CRG_ALARM` → `PIN_CRG_OK` in `pin_defs_mcc.hpp`; logic inverted (`== LOW` = alarm) in `mcc.cpp`; serial STATUS and `MCC.ino` `pinMode` updated. D42 HIGH=charge OK confirmed. |

---

## ~Session 39 — FMC STM32F7 Port (~2026-04-11)

| ID | Item | Resolution |
|----|------|------------|
| FMC-STM32-1 | FMC STM32 migration | ✅ Complete — FMC v2 STM32F7 (OpenCR) port done. `hw_rev.hpp` self-detecting HW_REV byte [45]. HEALTH_BITS [7], POWER_BITS [46] added. ICD v3.5.2. ARCH v3.3.7. |
| FMC-NTP | FMC dt elevated — suspected NTP/USB CDC loop blocking | ✅ Closed — SAMD21 NTP/USB CDC conflict not applicable on STM32F7. `isNTP_Enabled` default true. NTP init unconditional at boot. |
| SAMD-NTP | FMC SAMD21 NTP/USB CDC conflict | ✅ Closed — SAMD21 retired (FMC v2 is STM32F7). USB CDC/Ethernet power path conflict no longer applicable. |

---

## CB-20260412 — TRC Controller Pass

| ID | Item | Resolution |
|----|------|------------|
| FW-C10 (TRC) | REG1 CMD_BYTE 0xA1 → 0x00 | ✅ `telemetry.cmd_byte`, `buildResponseFrame` calls in `trc_a1.cpp` and `udp_listener.cpp` all changed to literal `0x00`. |
| GET_REGISTER1 retired | `ICD_CMDS::GET_REGISTER1` no longer exists post-DEF-1 | ✅ All enum references replaced with literal `0x00`. Case replaced with `FRAME_KEEPALIVE` — **pending review in sweep pass.** |
| SET_BCAST_FIRECONTROL (TRC) | `0xAB` → `0xE0` comment/log updates | ✅ Comment and log string updates in `trc_a1.hpp`, `trc_a1.cpp`, `udp_listener.cpp`, `telemetry.h`, `types.h`. Enum name unchanged — auto-handled by DEF-1. |
| Version | `VERSION_PACK(3,0,2)` → `VERSION_PACK(4,0,0)` | ✅ `TRC_VERSION` compile-time constant added to `main.cpp`. All version references unified. `g_state.version_word` assignment updated. |
| TRC-TRAINING | Training mode via vote bits | 🟡 Opened — see Part 2 TRC section |
| TRC-COCO-UDP | COCO enable via UDP at 0xD1 | 🟢 Opened — see Part 2 TRC section |
| TRC-A1-CHK | Fire control packet byte [3] checksum | 🟢 Opened — see Part 2 TRC section |
| GET_REGISTER1 (C#) | Retired stream command properties in `trc.cs` | ✅ `EnableTestPatterns` / `StreamMulticast_Enable` / `Stream60FPS_Enable` deleted — `ORIN_SET_STREAM_TESTPATTERNS` / `ORIN_SET_STREAM_MULTICAST` / `ORIN_SET_STREAM_60FPS` retired ICD v4.0.0. Corresponding event handlers and controls deleted from `frmTRC.cs` / `frmTRC_Designer.cs`. |
| SW_MAJOR / IsV4 (TRC) | Version gate added to `MSG_TRC.cs` | ✅ `SW_MAJOR` and `IsV4` properties added after `SW_VERSION_STRING`. Consistent with `FW_MAJOR`/`IsV4` on all other controllers. TRC uses `SW_` prefix throughout. |
| FW-C10 (MSG_TRC) | cmd_byte comment updated | ✅ Header and parse comments updated 0xA1 → 0x00 / legacy note. |

| ID | Item | Resolution |
|----|------|------------|
| FW-C10 (FMC) | REG1 CMD_BYTE 0xA1 → 0x00 | ✅ `buf[0]` in `buildReg01()` and `sendA2Unsolicited()` frameBuildResponse CMD_BYTE both changed to `0x00`. BDC A1 receiver check already handles both 0x00/0xA1 from BDC pass. |
| RES_A1 (FMC) | `case ICD::RES_A1` deleted from fmc.cpp | ✅ Default handler catches. 0xA1 = `SET_HEL_TRAINING_MODE` — FMC has no laser handler. |
| SET_TIMESRC (FMC) | New `SET_TIMESRC` stub inserted — FMC had no prior RES_A3 case | ✅ FMC never had `RES_A3` — slot 0xA3 had no handler. New `case ICD::SET_TIMESRC:` stub inserted after `SET_NTP_CONFIG` case. Pending rejection correct — FW-C7/C8 unblocks. Note: TMC had `RES_A3` renamed in its pass ✅. Both sub-controllers now have SET_TIMESRC stubs consistent with MCC/BDC. |
| FMC-CS7 | BDC SEND_REG_01() FMC pass-through — verify raw memcpy | ✅ Confirmed — BDC `handleA1Frame()` populates `fmc.buffer` via `memcpy`; `SEND_REG_01()` forwards via `memcpy(buf + offset, fmc.buffer, ...)`. No field interpretation. `MSG_BDC.cs` calls `FMCMsg.ParseMSG01()` at correct offset. |
| REQ_REG_01 | `fmc.cs` `REQ_REG_01()` — deleted | ✅ Method sent `ICD.RES_A1` which always returned STATUS_CMD_REJECTED. Enum no longer exists after DEF-1. Not called from `frmFMC.cs`. Deleted. |
| FW-C6 (FMC) | `isUnsolicitedModeEnabled` in `frmFMC.cs` | ✅ Already commented out at line 128. FW-C6 clean fleet-wide. |

| ID | Item | Resolution |
|----|------|------------|
| FW-C10 (TMC) | REG1 CMD_BYTE 0xA1 → 0x00 | ✅ `buf[0]` in `buildReg01()` and frameBuildResponse CMD_BYTE both changed to `0x00`. `MSG_TMC.cs` parse check updated. `tmc.cs` frame check updated. `mcc.cpp` line 246 TMC A1 check updated to dual 0x00/0xA1. |
| RES_A1 (TMC) | `case ICD::RES_A1` deleted from tmc.cpp | ✅ Default handler catches. 0xA1 = `SET_HEL_TRAINING_MODE` — TMC has no laser handler, default rejects correctly. |
| SET_TIMESRC (TMC) | `RES_A3` → `SET_TIMESRC` stub in tmc.cpp | ✅ Renamed, rejection retained pending FW-C7/C8. |

| ID | Item | Resolution |
|----|------|------------|
| FW-B5 | BDC FSM position offsets wrong in handleA1Frame() | ✅ Fixed — `fsm_posX_rb` offset 24→20, `fsm_posY_rb` 28→24. Verified against `fmc.cpp` pack locations and `MSG_FMC.cs` parser. |
| GUI-3 | MSG_BDC.cs activeTimeSource reads from DeviceReadyBits | ✅ Fixed — `activeTimeSourceLabel` now reads `tb_isNTP_Synched` (TimeBits bit 3) instead of `isNTP_DeviceReady` (DeviceReadyBits bit 0). |
| FW-C6 (BDC) | isUnSolicitedMode_Enabled in MSG_BDC.cs | ✅ Confirmed clean — not present in MSG_BDC.cs. FW-C6 fully closed fleet-wide. |
| GimbalSetHome | `ICD.SET_GIM_HOME` retired — bdc.cs method removed | ✅ `GimbalSetHome()` deleted from `bdc.cs` — `SET_GIM_HOME` (0xB1) retired in ICD v4.0.0; slot taken by `SET_BDC_VOTE_OVERRIDE`. Not called from `frmBDC.cs`. |

| ID | Item | Resolution |
|----|------|------------|
| BDC-UNIFY | BDC V1/V2 hardware unification | ✅ Complete — full unified codebase for BDC Controller 1.0 Rev A. Vicor polarity V1 LOW=ON → V2 HIGH=ON (`POL_VICOR_ON/OFF` macros via `hw_rev.hpp`). `PIN_TEMP_VICOR` GPIO 0→20. Three new V2 thermistors relay/bat/USB. IP175 Ethernet switch control (GPIO 52/64 V2 only). `PIN_DIG2_ENABLE` removed (never used). `STATUS_BITS`→`HEALTH_BITS` / `STATUS_BITS2`→`POWER_BITS` breaking rename. REG1 bytes [392–395] promoted RESERVED→HW_REV+temps. Nine new serial commands. TRC/FMC serial upgraded from hex dump to full field decode. ICD v3.5.1, ARCH v3.3.6, FW v3.3.0. Note: `BDC_HW_DELTA.md` header reads "AWAITING SIGN-OFF" — stale pre-implementation artifact, archived as-is. |

---

## ~Session 39 — FMC STM32F7 Migration (~2026-04-11)

| ID | Item | Resolution |
|----|------|------------|
| NEW-33 | MCC REG1 VOTE_BITS byte 3 bit 0 wrong field | ✅ Closed — MCC HEALTH_BITS byte [9] fully redefined in ICD v3.4.0. `isNotBatLowVoltage` now at bit 2. POWER_BITS byte [10] carries solenoid/laser bits. |

---

## ~Session 35–37 — Documentation (~2026-04-10)

| ID | Item | Resolution |
|----|------|------------|
| DOC-2 | Create JETSON_SETUP.md | ✅ Closed — JETSON_SETUP.md complete at v2.2.0. ARCH §2.5 cross-reference updated (ARCH v3.3.5). |

---

## Session 36 (~2026-03-xx)

| ID | Item | Resolution |
|----|------|------------|
| NEW-9 | MSG_MCC.cs HW verify | ✅ All fields confirmed correct on live hardware |
| NEW-10 | MSG_BDC.cs HW verify | ✅ All fields confirmed correct on live hardware |
| NEW-18 | CRC cross-platform wire verification | ✅ CRC-16/CCITT confirmed correct across all five controllers and C# |
| NEW-31 | frmMain.cs SET_LCH_VOTE arg swap — operatorValid duplicated | ✅ Fixed — operatorValid hardcoded true pending full implementation |
| NEW-39 | LCH/KIZ operatorValid hardcoded true | ✅ Confirmed complete S28 |

---

## Session 33 — FMC PTP (SAMD21) (~2026-03-xx)

| ID | Item | Resolution |
|----|------|------------|
| NEW-38c | FMC PTP integration (SAMD21 era) | ✅ TIME_BITS at byte [44]. Socket budget 4/8. NTP IP corrected `.8`→`.33`. `isNTP_Enabled=false` default (SAMD-NTP workaround). TIME/TIMESRC/PTPDEBUG serial commands. MSG_FMC.cs updated. |

---

## Session 32 — BDC PTP (~2026-03-xx)

| ID | Item | Resolution |
|----|------|------------|
| NEW-38b | BDC PTP integration | ✅ Socket budget corrected 9/8→7/8. TIME_BITS at byte [391]. Boot step PTP_INIT added. MSG_BDC.cs updated. |

---

## Session 30/31 — TMC PTP (~2026-03-xx)

| ID | Item | Resolution |
|----|------|------------|
| NEW-38a | TMC PTP integration | ✅ STAT_BITS3 at byte [61]. TIME/TIMESRC/PTPDEBUG serial commands. MSG_TMC.cs updated. TMC FW v3.0.5. |

---

## Session 30 — HMI Stats / CommHealth (2026-04-06)

| ID | Item | Resolution |
|----|------|------------|
| HMI-STATS-1 | HMI controller timing stats | ✅ MSG_MCC/MSG_BDC own all stats. CommHealth property. IBIT labels expanded. Double-click resets on dt/HB labels. |
| HMI-STATS-TIME | Time source status strip split into three controls | ✅ `tss_*_TimeSrc` (Green=PTP, Blue=NTP, Orange=fallback, Red=NONE), `tss_*_NTPTime`, `tss_*_dUTC` (Green<3ms, Orange 3–10ms, Red>10ms). |
| CB-COMMHEALTH | CB.MCC_STATUS / CB.BDC_STATUS simplified | ✅ Before STANDBY: ping only. At/after STANDBY: CommHealth exclusively. Old logic removed. WorstStatus() added then removed. |
| MSG-BDC-DTMAX | MSG_BDC dtmax logic bug | ✅ Fixed — was threshold-gated, now true running max (`if (dt_us > dtmax)`). |
| MSG-BDC-TIMESRC | MSG_BDC activeTimeSourceLabel NTP fallback case | ✅ Fixed to match MCC — returns "NTP (fallback)" when ntpUsingFallback set. |
| ICD-AF | SET_TIMESRC = 0xAF assigned | ✅ Slot reserved in ICD at time of assignment. ⚠️ Subsequently reassigned to `SET_HEL_TRAINING_MODE` (ICD v3.5.0). FW-C7 requires new byte — see FW-C9. |
| FW-1 | PTPDEBUG <0-3> serial command | ✅ Implemented and verified on MCC; propagated to all controllers. |
| FW-2 | TIMESRC UDP command — PTP, NTP, AUTO, OFF | ✅ Implemented across all controllers. |

---

## Session 29 (~2026-03-xx)

| ID | Item | Resolution |
|----|------|------------|
| GUI-1 | MCC + BDC ENG GUI A2/A3 timeout | ✅ Six handler root causes fixed. New client detection before replay check. `_lastKeepalive` only in `SendKeepalive()`. Any-frame liveness. `connection established` in receive loop. Applied fleet-wide: `mcc.cs`, `bdc.cs`, `tmc.cs`, `fmc.cs`. |
| NEW-36 | PTP integration HW verify | ✅ `offset_us=12`, `active source: PTP`, `time=2026-03-28` confirmed on MCC. |
| NEW-37 | MSG_MCC.cs PTP bits + ENG GUI display | ✅ `epochTime`, `activeTimeSource`, `isPTP_DeviceReady`, `usingPTP` all working. |

---

## Session 28 (~2026-03-xx)

| ID | Item | Resolution |
|----|------|------------|
| SAMD-NTP (partial) | FMC SAMD21 PrintTime() serial lockup — root cause found | ✅ Root cause: `PrintTime()` called `Serial` not `SerialUSB`. Removed from FMC handlers. NTP confirmed on bench. Note: definitively closed ~S39 by FMC STM32F7 port. |

---

## Session 27 (~2026-03-xx)

| ID | Item | Resolution |
|----|------|------------|
| NET-1 | NTP server IP set over UDP — SET_NTP_CONFIG 0xA2 | ✅ Done — all four controllers + C# classes |
| NTP-RECOVER | NTP auto-recovery with consecutiveMisses | ✅ Done — ntpClient.hpp/cpp, all four controllers |
| NTP-STRATUM | NTP stratum/LI validation | ✅ ProcessPacket rejects stratum 0, stratum ≥16, LI=3 |
| NTP-SERVERS | NTP server defaults — .33 primary, .208 fallback | ✅ Done — defines.hpp, all four controller headers |
| NTP-STATUS | NTP fallback status bits in all controller REG1 | ✅ ICD v3.2.0. Note: superseded by unified TIME_BITS layout. |
| NIC-BIND | Dual-NIC ENG GUI fix | ✅ CrossbowNic.cs auto-detects internal (<100) and external (≥200) NIC |
| ICD-3.2.0 | ICD bumped to v3.2.0 | ✅ All ICD documents and ARCHITECTURE updated |
| HYPERION-THEIA | HYPERION↔THEIA CUE relay path | ✅ Working session 27 |
| MCC-1 | MCC CloudEnergy battery bridge init | ✅ Battery comms reliable without explicit init sequence |
| TMC-TEMP-1 | TMC MCU temp reading off | ✅ No longer observed |
| DEPLOY-1 | Windows NIC internal NIC assignment | ✅ Handled by CrossbowNic.cs auto-detection |
| DEPLOY-2 | Clean rebuild after file replacements | ✅ Done session 27 |
| NEW-35 | FW: all firmware targets NTP .33 directly | ✅ IP_NTP_BYTES = .33 in defines.hpp; fallback .208 by default |

---

## Session 26 (~2026-03-xx)

| ID | Item | Resolution |
|----|------|------------|
| BDC-FMC-1 | BDC→FMC A1 path — port, isConnected watchdog, OnA1Received() | ✅ Done |
| BDC-FMC-2 | BDC→FMC command framing — EXEC_UDP() replaced with INT framed sends | ✅ Done — fmc.cpp/hpp delivered |
| BDC-FMC-3 | BDC EXT_CMDS_BDC[] — 0xF1/F2/F3/FB added to whitelist | ✅ Done — bdc.hpp delivered |
| FMC-ENG-1 | FMC ENG GUI socket bind — explicit bind, source IP filter, explicit send | ✅ Done — fmc.cs delivered |
| FSM-TRACK | FSM tracking end-to-end — commanded position, readback, mirror movement | ✅ Confirmed working |
| NET-BAT | Battery/charger liveness — isBAT_Ready / isCRG_Ready | ✅ Wired to bat.isCommOk and dbu.isConnected() |
| TRC-M11b | MAINT/FAULT coordinated flash — all five controllers | ✅ Confirmed correct on MCC, BDC, TMC, FMC, TRC |
| HMI-A3-20 | ENG GUI socket bind — TransportPath pattern | ✅ Working on HMI and ENG GUI |
| TRC-2 | THEIA not receiving video after IP change .8→.208 | ✅ Video panel removed by designer — not a firmware issue |
| FW-MCC | Add 0xE6 PMS_SET_FIRE_REQUESTED_VOTE to EXT_CMDS_MCC[] | ✅ STATUS_OK confirmed from .208:10050 |
| FW-VERIFY | All EXT promotions return STATUS_OK | ✅ 0xE6, 0xCC, 0xB4 all confirmed |

---

## Session 22 (~2026-xx-xx)

| ID | Item | Resolution |
|----|------|------------|
| NEW-13 | ICD scope labels INT_OPS/INT_ENG applied | ✅ Applied ICD v3.1.0 |
| TRC-M1 | TRC A2 framing — magic/frame validation | ✅ Complete |
| TRC-M5 | TRC A2 framing — buildTelemetry struct rewrite | ✅ Complete |
| TRC-M7 | TRC FW A2 framing — udp_listener.cpp build/parse/CRC | ✅ Complete |

---

## Session 17 (~2026-xx-xx)

| ID | Item | Resolution |
|----|------|------------|
| NEW-12 | TransportPath enum — MSG_MCC/BDC | ✅ MAGIC_LO computed from enum, not hardcoded. Deployed sessions 16/17. |

---

## Session 15 (~2026-xx-xx)

| ID | Item | Resolution |
|----|------|------------|
| TRC-M10 | TRC isConnected live flag | ✅ Wired in handleA1Frame — was only set in dead receive loop. |

---

## Session 14 (~2026-xx-xx)

| ID | Item | Resolution |
|----|------|------------|
| S14-1 | Fire control vote rate 200Hz → 100Hz | ✅ MCC::TICK_VoteStatus changed 5ms → 10ms in mcc.hpp |
| S14-2 | A1 stream rates table added to ICD | ✅ ICD bumped to v1.7.2 |
| FW-PRE-CHECK | Confirm 0xA0 SET_UNSOLICITED in MCC and BDC EXT_CMDS[] | ✅ Confirmed present in both EXT_CMDS_MCC[] and EXT_CMDS_BDC[] |
| FW-BDC-1 | Add CMD_MWIR_NUC1 (0xCC) to BDC EXT_CMDS[] | ✅ Already present — no flash required |
| DISC-1 | SET_CUE_OFFSET byte mismatch — ICD vs BDC firmware | ✅ defines.hpp confirmed SET_CUE_OFFSET = 0xB4 correct — BDC case comments were stale only |
| ENUM-1 to ENUM-5 | defines.hpp enum names synced to ICD | ✅ EXT_FRAME_PING, RES_C0, ORIN_ACAM_COCO_CLASS_FILTER, ORIN_ACAM_COCO_ENABLE, RES_FD — all corrected |
| TRC-1 | TRC compile error — wrong enum name in udp_listener.cpp:944 | ✅ ORIN_ACAM_SET_AI_TRACK_PRIORITY → ORIN_ACAM_COCO_CLASS_FILTER fixed — TRC compiles |

---

*End of CROSSBOW_CHANGELOG.md (IPGD-0019 v1.0.0)*
