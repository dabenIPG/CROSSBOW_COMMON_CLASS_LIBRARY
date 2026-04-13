# TMC v3.3.0 — Testing Checklist & ENG GUI Update Log
**Document:** `TMC_TEST_AND_GUI`
**Date:** 2026-04-07
**FW Version:** `VERSION_PACK(3, 3, 0)`
**Status:** Testing complete — V1 ✅ V2 ✅ GUI ✅

---

## 1. Build Verification

| Build | Status | Notes |
|-------|--------|-------|
| `HW_REV_V1` | ✅ | Tested on hardware |
| `HW_REV_V2` | ✅ | Tested on hardware |
| `HW_REV_V1` + `SINGLE_LOOP` | ✅ | Both PIDs track tf2 confirmed |
| `HW_REV_V2` + `SINGLE_LOOP` | ✅ | Both PIDs track tf2 confirmed |
| Neither defined | ✅ | `#error` fires as expected |
| Both defined | ✅ | `#error` fires as expected |

---

## 2. Firmware Test Results

| ID | Item | Result |
|----|------|--------|
| T1 | V2 hardware compile + boot verify | ✅ Confirmed |
| T2 | V2 pump independence test (PUMP1 on, PUMP2 off) | ✅ Confirmed via `VICOR 2 1` / `VICOR 3 1` |
| T2a | `PUMP` serial command — status + control both revisions | ✅ Confirmed |
| T2b | `SINGLE_LOOP` shown in INFO/STATUS output | ✅ Confirmed |
| T2c | STATUS_BITS1 bit 6 = `isSingleLoop` (compile-time) | ✅ Confirmed |
| T3 | V2 opto polarity — CTRL_OFF=LOW holds devices off at boot | ✅ Confirmed |
| T4 | V2 temp sensor verify — ta1/tc1/tc2/to1 from direct pins | ✅ Confirmed |
| T5 | SINGLE_LOOP mode — both PIDs track tf2 | ✅ Confirmed |
| T6 | V1 PID loop — LCM speed responds to tt setpoint | ✅ Confirmed (overshoot noted — see open items) |
| T7 | V1 heater verify — no hardware available | ⏳ Deferred |
| `PIDGAIN` | Runtime PID gain read/set via serial | ✅ Confirmed |

---

## 3. ENG GUI Update Log

### C# / `MSG_TMC.cs`

| ID | Item | Status |
|----|------|--------|
| G1 | `HW_REV` property + `IsV1`/`IsV2`/`HW_REV_Label` accessors (byte [62]) | ✅ |
| G2 | STATUS_BITS1 bit 5 — `isPump2Enabled` gated on `IsV2` | ✅ |
| G3 | `PumpSpeedValid` — hide pump speed on V2 | ✅ |
| G4 | `Tv3Tv4Valid` — hide tv3/tv4 on V2 | ✅ |
| G5 | `isPump1Enabled` alias for bit 1 | ✅ |
| G6 | `isSingleLoop` — bit 6, unconditional both revisions | ✅ |

### `tmc.cs`

| ID | Item | Status |
|----|------|--------|
| G7 | `EnableVicor()` — guard HEAT channel on V2 | ✅ |
| G8 | `SetDAC()` — guard PUMP/HEATER channels on V2 | ✅ |
| G9 | `EnableBothPumps()` — V2 convenience method | ✅ |

### `defines.cs`

| ID | Item | Status |
|----|------|--------|
| G10 | `TMC_VICORS.PUMP1 = 2`, `PUMP2 = 4` added | ✅ |

### `frmTMC.cs` / Designer

| ID | Item | Status |
|----|------|--------|
| G11 | `ApplyHwRevLayout(defaultV1)` — one-time layout switch on first packet | ✅ |
| G12 | `groupBox9` — `chk_Vicor_Pump2_Enable` + `mb_Vicor_Pump2_Enabled_rb` added in designer | ✅ |
| G13 | Pump2 handler — `EnableVicor(PUMP2, ...)` | ✅ |
| G14 | Pump speed DAC group hidden on V2 (label2, cmb, button, readback) | ✅ |
| G15 | Heater controls hidden on V2 | ✅ |
| G16 | tv3/tv4 labels hidden on V2 | ✅ |
| G17 | `tss_HW_REV` shows `HW_REV_Label` + loop topology (SINGLE / PARALLEL LOOP) | ✅ |
| G18 | `mb_Vicor_Pump_Enabled_rb` → `isPump1Enabled`; `mb_Vicor_Pump2_Enabled_rb` → `isPump2Enabled` | ✅ |
| G19 | Disconnect resets layout to V1 defaults | ✅ |
| G20 | Version display — `3.3.0` format confirmed (no `v` prefix) | ✅ |

---

## 4. Open Items

| # | Item | Priority | Notes |
|---|------|----------|-------|
| PID-1 | PID gain tuning — kp=50/ki=100/kd=10 causing overshoot on LCM speed control | 🟡 Medium | Use `PIDGAIN <ch> <kp> <ki> <kd>` serial command for runtime tuning without recompile |
| T7 | V1 heater verify — no heater hardware available | 🟢 Low | Deferred to bench test when hardware present |

---

## 5. Serial Command Reference (new in v3.3.0)

### `PUMP`
```
PUMP                    — print pump status
PUMP <val>              — V1 only: set pump Vicor DAC [0–4095]
PUMP * <0|1>            — V2 only: set both PUMP1 and PUMP2
PUMP 1 <0|1>            — V2 only: set PUMP1 only
PUMP 2 <0|1>            — V2 only: set PUMP2 only
```

### `PIDGAIN`
```
PIDGAIN                 — print kp/ki/kd for both loops
PIDGAIN <ch> <kp> <ki> <kd>   — set gains for loop 1 or 2, apply immediately
```
Note: `PIDGAIN` calls `SetTunings()` directly — gains take effect on the running PID without re-entering ISR/COMBAT state.

---

## 6. File Manifest

| File | Version | Status |
|------|---------|--------|
| `hw_rev.hpp` | — | ✅ New |
| `pin_defs_tmc.hpp` | — | ✅ Replaced |
| `defines.hpp` | — | ✅ Updated |
| `tmc.hpp` | — | ✅ Replaced |
| `tmc.cpp` | 1873 lines | ✅ Replaced |
| `TMC.ino` | — | ✅ Replaced |
| `defines.cs` | — | ✅ Updated |
| `MSG_TMC.cs` | — | ✅ Updated |
| `tmc.cs` | — | ✅ Updated |
| `frmTMC.cs` | — | ✅ Updated |
| `CROSSBOW_ICD_INT_ENG.md` | v3.3.9 | ✅ Updated |
| `CROSSBOW_ICD_INT_OPS.md` | v3.3.8 | ✅ Updated |
| `ARCHITECTURE.md` | v3.3.3 | ✅ Updated |
| `TMC_HW_DELTA.md` | — | ✅ Updated |
| `TMC_UNIFIED_PREVIEW.md` | — | ✅ Reference |
