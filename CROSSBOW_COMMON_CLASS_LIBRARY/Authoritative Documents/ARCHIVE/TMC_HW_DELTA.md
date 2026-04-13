# TMC Hardware Delta: V1 → V2
**Document:** `TMC_HW_DELTA`
**Status:** COMPLETE — all files reviewed and unified codebase delivered. Testing complete.
**Date:** 2026-04-07
**FW Version:** `VERSION_PACK(3, 3, 0)`
**Scope:** Hardware-driven differences only. V1 (base TMC) is authoritative for all non-hardware logic. Stale V2 changes are catalogued in Section 8 and were not carried forward.

---

## 1. Hardware Context

The TMC (Thermal Management Controller) manages the chiller subsystem on an STM32F7 / OpenCR platform (Arduino-compiled). The chiller consists of dual LCMs (Liquid Chiller Modules), dual pumps, a heater (V1 only), Vicor/TRACO DC-DC converters, inlet fans, flow sensors, and distributed temperature sensors.

### V1 → V2 Hardware Change Summary

| Change | V1 | V2 |
|--------|----|----|
| Pump power supply | Single Vicor — both pumps in parallel, DAC-trimmed voltage (speed control) | Two TRACO DC-DCs — one per pump, on/off only, independent control |
| Heater circuit | Present — Vicor supply with DAC power control | **Removed entirely** |
| External ADC chips | Two ADS1015 (8 channels) — auxiliary temp monitoring | **Removed** — essential temps migrated to direct MCU analog inputs |
| Control opto type (Vicors/PSUs) | Normally Open (NO) | Normally Closed (NC) |
| LCM control opto type | Normally Open (NO) | **Unchanged — NO** |
| LCM DAC control (MCP47FEBXX) | Active — compressor speed trim | **Unchanged** |

---

## 2. Opto Logic Inversion (Vicor/PSU outputs only)

```cpp
#if defined(HW_REV_V1)
  #define CTRL_OFF  HIGH  // NO opto — HIGH asserts inhibit → device OFF
  #define CTRL_ON   LOW
#elif defined(HW_REV_V2)
  #define CTRL_OFF  LOW   // NC opto — LOW default closed → device OFF
  #define CTRL_ON   HIGH
#endif
```

Does NOT apply to LCM enable pins — LCM optos are NO in both revisions.

---

## 3. Pin Definitions Delta (`pin_defs_tmc.hpp`)

### Removed in V2
| Signal | V1 Pin | Reason |
|--------|--------|--------|
| `ARD_D53_RTC_CLK_EN` | 53 | RTC clock circuit removed |
| `ARD_A11_RTC_CLK_INT` | 75 | RTC clock interrupt removed |
| `TMC_AUX_ADC1_CHANNELS` enum | — | ADS1015 chip 1 removed |
| `TMC_AUX_ADC2_CHANNELS` enum | — | ADS1015 chip 2 removed |
| `PIN_VICOR_PUMP` | 83 | Single pump Vicor replaced by two TRACO PSUs |
| `PIN_VICOR_HEAT` | 72 | Heater circuit fully removed |

### Added in V2
| Signal | V2 Pin | Origin |
|--------|--------|--------|
| `PIN_TEMP_AIR1` | 72 | Was `ADC1::CH1_AIR1` |
| `PIN_TEMP_COMP1` | 29 | Was `ADC1::CH3_COMP1` |
| `PIN_TEMP_COMP2` | 30 | Was `ADC1::CH4_COMP2` |
| `PIN_TEMP_OUT1` | 42 | Was `PIN_TEMP_RES2` (renamed) |
| `PIN_VICOR_PUMP1` | 65 | TRACO PSU enable — Pump 1 |
| `PIN_VICOR_PUMP2` | 46 | TRACO PSU enable — Pump 2 |

### Retired Temperatures (permanently dropped)
`AIR2`, `RES1`, `PUMP-temp`, `HEATER-temp` — all from removed ADS1015 chips.

---

## 4. Defines Delta (`defines.hpp`)

### `TMC_VICORS` enum
```cpp
enum class TMC_VICORS {
  LCM1  = 0,  LCM2 = 1,
  PUMP  = 2,  // V1 — single Vicor, both pumps in parallel (also PUMP1 alias in C# defines.cs)
  HEAT  = 3,  // V1 only
  PUMP1 = 2,  // V2 — TRACO PSU Pump 1 (same wire value as PUMP)
  PUMP2 = 4,  // V2 only — TRACO PSU Pump 2
};
```

### `TMC_PUMP_SPEEDS` — V1 only (DAC trim). V2 TRACO PSUs are on/off only.

### `TMC_DAC_CHANNELS` — `PUMP` and `HEATER` entries V1 only. `LCM1`/`LCM2` active both revisions.

---

## 5. REG1 Wire Format — STATUS_BITS1 (byte [7])

| Bit | V1 | V2 |
|-----|----|----|
| 0 | `isReady` | `isReady` |
| 1 | `isPumpEnabled` | `isPump1Enabled` |
| 2 | `isHeaterEnabled` | `isHeaterEnabled` (always 0) |
| 3 | `isInputFan1Enabled` | `isInputFan1Enabled` |
| 4 | `isInputFan2Enabled` | `isInputFan2Enabled` |
| 5 | `RES` | **`isPump2Enabled`** ← wire format change |
| 6 | **`isSingleLoop`** | **`isSingleLoop`** ← new (both revisions) |
| 7 | `RES` (retired session 35) | `RES` |

⚠️ `MSG_TMC.cs` must read HW_REV byte [62] before interpreting bit 5.

### Other REG1 wire format changes

| Byte(s) | V1 | V2 |
|---------|----|----|
| [17–18] | Pump Speed — DAC counts [0–800] | `0x0000` reserved |
| [39] | `tv3` — Vicor heater temp int8 °C | `0x00` reserved |
| [40] | `tv4` — Vicor pump temp int8 °C | `0x00` reserved |
| [62] | RESERVED | **`HW_REV`** — `0x01`=V1, `0x02`=V2 |

---

## 6. Unified Codebase — `#ifdef` Guard Map

| # | File | Item | Revision |
|---|------|------|----------|
| 1 | `pin_defs` | `PIN_VICOR_PUMP` / `PIN_VICOR_HEAT` | V1 only |
| 2 | `pin_defs` | RTC clock pins, ADS1015 enums | V1 only |
| 3 | `pin_defs` | `PIN_VICOR_PUMP1/2`, direct temp pins | V2 only |
| 4 | `defines` | `TMC_VICORS::PUMP/HEAT`, `TMC_PUMP_SPEEDS`, DAC PUMP/HEATER | V1 only |
| 5 | `defines` | `TMC_VICORS::PUMP1/PUMP2` | V2 only |
| 6 | `TMC.ino` | Vicor/PSU init polarity (`CTRL_OFF` macro) | Both |
| 7 | `TMC.ino` | Heat/pump init block | V1/V2 |
| 8 | `tmc.hpp` | ADS1015 include/objects, pump/heater members, tv3/tv4 | V1 only |
| 9 | `tmc.hpp` | `isPump1/2Enabled` | V2 only |
| 10 | `tmc.hpp` | STATUS_BITS1 bits 1/5 | V1/V2 |
| 11 | `tmc.hpp` | STATUS_BITS1 bit 6 (`isSingleLoop`) | Both (compile-time) |
| 12 | `tmc.cpp` | INIT ADS1015 block, pump/heater DAC | V1 only |
| 13 | `tmc.cpp` | `pollTemps()` cases 4/6/7/8 (function + arg) | V1/V2 |
| 14 | `tmc.cpp` | `pollTemps()` cases 9/10 (tv3/tv4), ptr rollover | V1 only |
| 15 | `tmc.cpp` | `buildReg01()` bytes 17–18, 39–40 | V1/V2 |
| 16 | `tmc.cpp` | `StateManager()` pump sequences | V1/V2 |
| 17 | `tmc.cpp` | `dispatchCmd()` vicor enable/DAC cases | V1/V2 |
| 18 | `tmc.cpp` | `SERIAL_CMD` PUMP/VICOR/DAC/TEMPS/LCM display | V1/V2 |
| 19 | `tmc.cpp` | `PidUpdate()` `SINGLE_LOOP` block | Both |

---

## 7. New Features (session 30)

| Feature | Description |
|---------|-------------|
| `PUMP` serial command | No args: status; V1 with args: DAC value; V2 with args: `* / 1 / 2` on/off |
| `PIDGAIN` serial command | No args: print kp1/ki1/kd1/kp2/ki2/kd2; with args: set gains and call `SetTunings()` live |
| `SINGLE_LOOP` STATUS bit | STATUS_BITS1 bit 6 — compile-time constant, always present both revisions |
| `SINGLE_LOOP` serial | INFO and STATUS commands show loop topology: SINGLE / PARALLEL LOOP |
| `HW_REV` in REG1 | Byte [62] = `TMC_HW_REV_BYTE` — self-detecting for `MSG_TMC.cs` |
| PID runtime tuning | `kp1/ki1/kd1/kp2/ki2/kd2` are class members; `PIDGAIN` updates live via `SetTunings()` |

---

## 8. Stale V2 Changes — Not Carried Forward (V1 Authoritative)

| # | Item | V1 (kept) | V2 (discarded) |
|---|------|-----------|----------------|
| S1 | Serial buffer | `static char[64]` | `String` heap |
| S2 | `isPTP_Enabled` default | `false` | `true` |
| S3 | `GetCurrentTime()` | Full holdover + epoch guard | Simplified |
| S4 | A1 backoff members | Present | Missing |
| S5 | `isUnSolicitedEnabled` | Absent (retired) | Present |
| S6 | `dispatchCmd` `clientIdx` | Present | Missing |
| S7 | `handleA2Frame` replay fix | `isNewClient` before replay | After replay |
| S8 | `FRAME_KEEPALIVE 0xA4` handler | Full rate-gate + ping | Missing |
| S9 | `PRINT_REG` byte [62] | `HW_REV` decoded | Labelled RESERVED |

---

## 9. Open Items

| # | Item | Priority | Status |
|---|------|----------|--------|
| PID-1 | PID gain tuning — kp=50/ki=100/kd=10 causing overshoot on LCM speed control | 🟡 Medium | Open — use `PIDGAIN` serial command for runtime tuning |
| T7 | V1 heater verify — no heater hardware available for bench test | 🟢 Low | Deferred |

All other items from initial open list (T1–T6, G1–G9, T2a–T2c) closed and verified.

---

## 10. File Manifest — Session 2026-04-07

| File | Description | Status |
|------|-------------|--------|
| `hw_rev.hpp` | New — revision guard, `CTRL_ON/OFF`, `SINGLE_LOOP`, `TMC_HW_REV_BYTE` | ✅ |
| `pin_defs_tmc.hpp` | Replaced — all pins with V1/V2 guards, ADS1015 enums | ✅ |
| `defines.hpp` | Updated — `TMC_VICORS`, `TMC_PUMP_SPEEDS`, `TMC_DAC_CHANNELS` guarded | ✅ |
| `tmc.hpp` | Replaced — unified class, hardware-conditional members, `isSingleLoop` bit | ✅ |
| `tmc.cpp` | Replaced — 1873 lines, all hardware sections guarded, `PUMP`/`PIDGAIN` commands | ✅ |
| `TMC.ino` | Replaced — v3.3.0, unified setup(), static serial buffer | ✅ |
| `defines.cs` | Updated — `TMC_VICORS` PUMP1/PUMP2 added | ✅ |
| `MSG_TMC.cs` | Updated — `IsV1`/`IsV2`/`HW_REV_Label`, `isPump1/2Enabled`, `isSingleLoop`, guards | ✅ |
| `tmc.cs` | Updated — `EnableVicor` V2 guard, `SetDAC` V2 guard, `EnableBothPumps()` | ✅ |
| `frmTMC.cs` | Updated — `ApplyHwRevLayout()`, pump2 handler, HW_REV display, loop topology | ✅ |
