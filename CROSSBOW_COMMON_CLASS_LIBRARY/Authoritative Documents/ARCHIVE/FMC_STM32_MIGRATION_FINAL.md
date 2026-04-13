# FMC STM32F7 Migration — Final Record
**Date:** 2026-04-11
**From:** SAMD21 (Arduino MKR/Zero) — FW v3.2.3
**To:** STM32F7 (OpenCR board library) — FW v3.3.0
**ICD Ref:** v3.5.2 (bumped this session)
**ARCH Ref:** v3.3.6

---

## 1. ICD Changes — v3.5.2

| Byte | Before | After | Notes |
|------|--------|-------|-------|
| [7] | `FSM STAT BITS` | `FMC HEALTH_BITS` | Rename — bit layout changed (see below) |
| [44] | `TIME_BITS` | `TIME_BITS` | Unchanged |
| [45] | `RESERVED` | `HW_REV` | 0x01=V1 (SAMD21), 0x02=V2 (STM32F7) |
| [46] | `RESERVED` | `FMC POWER_BITS` | New byte — FSM/stage output states |
| [47–63] | `RESERVED (19)` | `RESERVED (17)` | Two bytes consumed |

**Byte [7] — FMC HEALTH_BITS (was FSM STAT BITS):**

| Bit | Before | After |
|-----|--------|-------|
| 0 | isReady | isReady — unchanged |
| 1 | isFSM_Powered | **RES** — moved to POWER_BITS [46] bit 0 |
| 2–5 | RES | RES — unchanged |
| 6 | isStage_Enabled | **RES** — moved to POWER_BITS [46] bit 1 |
| 7 | RES | RES — unchanged |

**Byte [46] — FMC POWER_BITS (new):**

| Bit | Field |
|-----|-------|
| 0 | isFSM_Powered |
| 1 | isStage_Enabled |
| 2–7 | RES |

**Byte [45] — HW_REV (new):**

| Value | Meaning |
|-------|---------|
| 0x01 | V1 — SAMD21 legacy |
| 0x02 | V2 — STM32F7 / OpenCR |

> ✅ **`MSG_FMC.cs` updated this session** — see §4.2 for details.

---

## 2. ARCH Changes Required — §12 FMC

| Field | Before | After |
|-------|--------|-------|
| Platform | SAMD21 | STM32F7 (OpenCR board library) |
| FW version | v3.2.x | v3.3.0 |
| Socket budget | 2/8 PTP disabled, 4/8 PTP enabled | 2/8 always (PTP gated — FW-B3) |
| FW-B4 note | open | re-opened — FW-B3 requires gate on FMC |
| Byte [7] label | FSM STAT BITS | FMC HEALTH_BITS |
| Byte [45] | RESERVED | HW_REV |
| Byte [46] | RESERVED | FMC POWER_BITS |
| Serial port | SerialUSB | Serial (abstracted via FMC_SERIAL) |

---

## 3. Files Changed

### 3.1 NEW — `hw_rev.hpp`

New file. Contains:
- `HW_REV_V1` / `HW_REV_V2` comment/uncomment revision selection
- Revision selection guard (`#error` if both or neither defined)
- Platform guard — TODO (correct OpenCR define not yet confirmed)
- `FMC_HW_REV_BYTE` — `0x01` (V1) or `0x02` (V2)
- `FSM_POW_ON` / `FSM_POW_OFF` — abstracted polarity (HIGH/LOW both revisions)
- `FMC_SERIAL` — `SerialUSB` (V1) or `Serial` (V2)
- `FMC_SPI` — `SPI` (V1) or `SPI_IMU` (V2)
- `uprintf()` — cross-platform formatted print via `FMC_SERIAL`

### 3.2 REPLACED — `pin_defs_fmc.hpp`

| Pin | V1 (SAMD21) | V2 (STM32F7) | Notes |
|-----|-------------|--------------|-------|
| WIZ_RST | 2 | 61 | Changed |
| WIZ_CS | 10 | 10 | Unchanged |
| FSM_CS_DAC | 6 | 6 | Unchanged |
| FSM_CS_ADC | 9 | 9 | Unchanged |
| FSM_PWR_EN | 4 | 4 | Unchanged |
| LED_0 | 13 | BDPIN_LED_USER_1 | Changed |
| LED_1 | 3 | BDPIN_LED_USER_2 | Changed |
| LED_2 | 5 | BDPIN_LED_USER_3 | Changed |
| LED_3 | 7 | BDPIN_LED_USER_4 | Changed |
| LED_4 | — | 36 | New (V2 only) |
| FMC_LED_COUNT | 4 | 5 | New macro |
| FMC_LED_PINS | {13,3,5,7} | {BDPIN..1..4, 36} | New macro |

### 3.3 MODIFIED — `fmc.hpp`

| Edit | Change |
|------|--------|
| C1 | Header comment — updated to ICD v3.5.2 / STM32F7 |
| C2 | Added `#include "hw_rev.hpp"` as first include |
| C3 | Removed `extern uint8_t FSM_POW_ON/OFF` — now hw_rev.hpp macros |
| C4 | `STATUS_BITS1()` replaced by `HEALTH_BITS()` + `POWER_BITS()` — ICD v3.5.2 split |

### 3.4 MODIFIED — `fmc.cpp`

| Edit | Change |
|------|--------|
| D1 | Header updated to ICD v3.5.2; `#include "delay.h"` removed; `uprintf()` moved to hw_rev.hpp |
| D2 | File header comment updated |
| D3 | `INIT()`: NTP init unconditional (was gated by `isNTP_Enabled`) |
| D4 | `INIT()`: PTP init **re-gated** behind `isPTP_Enabled` — FW-B3 W5500 multicast contention with BDC |
| D5 | `INIT()`: HW_REV added to boot print; `a1DestBDC` uses `IP_BDC_BYTES` macro |
| D6–D10 | `UPDATE()`/`pollA2()`/`handleA2Frame()`/`dispatchCmd()`/`SEND_UNSOLICITED()`: `SerialUSB` → `FMC_SERIAL` |
| D11 | `buildReg01()`: `STATUS_BITS1()` → `HEALTH_BITS()`; `buf[45]=FMC_HW_REV_BYTE`; `buf[46]=POWER_BITS()` |
| D12 | `PRINT_REG()`: TMC-style unicode box; HEALTH/POWER split; HW_REV line; HOLDOVER label |
| D13–D18 | `SERIAL_CMD()`: all `SerialUSB` → `FMC_SERIAL`; STATUS updated; TIME PrintTime() fix |
| D19 | `init_FSM()`: `SPI.` → `FMC_SPI.`; `delay(100)` after `endTransaction` — **closes #16** |
| D20–D27 | Hardware functions: `SPI.` → `FMC_SPI.`; `SerialUSB` → `FMC_SERIAL` |

### 3.5 MODIFIED — `FMC.ino`

| Edit | Change |
|------|--------|
| E1 | Header; STM32F7 HAL includes guarded `#if defined(HW_REV_V2)`; FW_VERSION → `VERSION_PACK(3,3,0)` |
| E2 | `FSM_POW_ON/OFF` removed; LED array uses macros; `IP_FMC` uses `IP_FMC_BYTES`; `tempCalibrated` added |
| E3 | `setup()`: `initADC()` guarded; `WIZ_RST` INPUT; `FSM_PWR_EN` → `FSM_POW_OFF`; `SerialUSB` → `FMC_SERIAL` |
| E4 | `readMCUTemp()` + `initADC()` + helpers from TMC.ino; guarded `HW_REV_V2`; V1 stub in `#else` |
| E5 | Serial handlers: `SerialUSB` → `FMC_SERIAL`; `MCUADC` command added |
| E6 | `blink()`: `4` → `FMC_LED_COUNT` |

---

## 4. Open Items

### 4.1 Closed This Session ✅

| # | Item |
|---|------|
| Old #16 | `init_FSM()` SPI transaction across `delay(100)` — delay moved after `endTransaction` |
| MSG_FMC.cs | `HealthBits`, `PowerBits`, `HW_REV` properties added |

### 4.2 C# / MSG_FMC.cs — ✅ Completed This Session

| # | Item | Status |
|---|------|--------|
| CS-1 | `HealthBits` property — byte [7], bit 0 = `isReady` | ✅ Done |
| CS-2 | `StatusBits1` backward-compat alias → `HealthBits` | ✅ Done |
| CS-3 | `PowerBits` — byte [46], bit 0 = `isFSM_Powered`, bit 1 = `isStageEnabled` | ✅ Done |
| CS-4 | `HW_REV` — byte [45]; `IsV1`/`IsV2`/`HW_REV_Label` | ✅ Done |
| CS-5 | `isFSM_Power_Enabled` / `isStageEnabled` now from `PowerBits` | ✅ Done |
| CS-6 | `isUnsolicitedModeEnabled` retired | ✅ Done |
| CS-7 | Verify BDC `SEND_REG_01()` FMC pass-through is raw memcpy | 🟡 Pending |

### 4.3 Documentation — Action Required 🔴

| # | Item | Priority |
|---|------|----------|
| DOC-1 | ICD v3.5.2 — byte [7] rename, byte [45] HW_REV, byte [46] POWER_BITS | 🔴 High |
| DOC-2 | ARCH §12 — platform, FW version, socket budget, PTP gate note | 🔴 High |
| DOC-3 | ARCH §2.2a — FMC socket budget: 2/8 always (PTP gated FW-B3) | 🔴 High |
| DOC-4 | ARCH §3 — FMC platform SAMD21→STM32F7 | 🔴 High |

### 4.4 Hardware / SPI — ✅ Confirmed

| # | Item | Status |
|---|------|--------|
| HW-1 | DAC/ADC on `SPI_IMU` bus confirmed | ✅ |
| HW-2 | FSM_PWR_EN HIGH=ON both revisions | ✅ |
| HW-3 | W5500 uses standard `SPI` — separate from `SPI_IMU` | ✅ |

### 4.5 Fleet PTP Gate — Action Required 🔴

**Root cause:** FW-B3 (W5500 DELAY_REQ multicast contention) affects all controllers.
`ptp.INIT()` must be gated by `isPTP_Enabled` fleet-wide until FW-B3 resolved.

| Controller | Current State | Action |
|---|---|---|
| FMC | ✅ Gated — this session | Done |
| MCC | ✅ Gated — FW-B4 | Done |
| BDC | ⚠️ Unconditional in boot state machine | Gate behind `isPTP_Enabled` — next BDC session |
| TMC | ⚠️ Unconditional in `INIT()` | Gate behind `isPTP_Enabled` — next TMC session |

**BDC fix (carry forward):**
```cpp
case BootStep::PTP_INIT:
    if (elapsed >= 1000)
    {
        if (isPTP_Enabled)
        {
            Serial.println(F("BOOT: ptp.INIT"));
            IPAddress ip(IP_GNSS_BYTES);
            ptp.INIT(ip);
        }
        lastBootTick = millis();
        bootStep++;
    }
    break;
```

**TMC fix (carry forward):**
```cpp
if (isPTP_Enabled)
{
    Serial.println(F("Starting PTP"));
    IPAddress IP_GNSS(IP_GNSS_BYTES);
    ptp.INIT(IP_GNSS);
    Serial.println(F("PTP  INIT  master 192.168.1.30"));
}
```

### 4.6 BDC FMC Driver — FSM Position Offsets 🟡

Found in `bdc.cpp` `handleA1Frame()` — wrong offsets:
```cpp
// Current (wrong):
fmc.fsm_posX_rb = BytesToInt32(fmc.buffer, 24);  // reads FSM Pos Y
fmc.fsm_posY_rb = BytesToInt32(fmc.buffer, 28);  // reads epoch ms

// Correct per ICD:
fmc.fsm_posX_rb = BytesToInt32(fmc.buffer, 20);  // FSM Pos X [20-23]
fmc.fsm_posY_rb = BytesToInt32(fmc.buffer, 24);  // FSM Pos Y [24-27]
```
Wrong values, no crash. Fix in next BDC session.

### 4.7 Hardware — Power

| # | Item | Priority |
|---|------|----------|
| HW-4 | FMC/BDC shared power via serial — brownout risk on USB in test | 🔴 High |
| HW-5 | Use dedicated supply for FMC in test | 🔴 High |
| HW-6 | BDC IP175 `SWRESET` serial command confirmed as recovery | ✅ Known |
| HW-7 | Verify power rail isolation FMC/BDC in production harness | 🟡 Medium |

### 4.8 Carried From Previous Open Items

| # | Item | Priority |
|---|------|----------|
| Old #13 | `scan()` blocks ~3.6s — bench only | 🟡 Low |
| Old #14 | `init_FSM()` blocks ~3.4s at boot — acceptable | 🟡 Low |
| Old #15 | `readPos()` I2C clock stretching — monitor `dt_delta` | 🟡 Medium |
| Old #18 | Aggregate loop I/O — monitor `dt_delta` | 🟡 Low |

---

## 5. Compile Status

| Target | Toolchain | Result |
|--------|-----------|--------|
| V1 — SAMD21 | Arduino SAMD core | ✅ Clean |
| V2 — STM32F7 | OpenCR board library | ✅ Clean |

---

## 6. hw_rev.hpp Abstractions Summary

| Macro | V1 (SAMD21) | V2 (STM32F7) | Purpose |
|-------|-------------|--------------|---------|
| `FMC_SERIAL` | `SerialUSB` | `Serial` | Serial port |
| `FMC_SPI` | `SPI` | `SPI_IMU` | SPI peripheral |
| `FSM_POW_ON` | `HIGH` | `HIGH` | FSM power polarity |
| `FSM_POW_OFF` | `LOW` | `LOW` | FSM power polarity |
| `FMC_HW_REV_BYTE` | `0x01` | `0x02` | REG1 byte [45] |
| `FMC_LED_COUNT` | `4` | `5` | LED array size |
| `FMC_LED_PINS` | `{13,3,5,7}` | `{BDPIN..1..4, 36}` | LED initialiser |
| `uprintf()` | via `SerialUSB` | via `Serial` | Formatted print |

---

## 7. Fleet Socket Budget — Updated

| Controller | PTP disabled (default) | PTP enabled | Notes |
|---|---|---|---|
| MCC | 6/8 | 8/8 | Gated ✅ |
| BDC | 7/8 | 7/8 | ⚠️ Needs gate — next session |
| TMC | 4/8 | 4/8 | ⚠️ Needs gate — next session |
| FMC | 2/8 | 4/8 | Gated ✅ |

---

## 8. FW Version History

```
3.3.0 — STM32F7 (OpenCR) port
         hw_rev.hpp V1/V2 hardware abstraction
         FMC HEALTH_BITS / POWER_BITS / HW_REV (ICD v3.5.2)
         ptp.INIT() re-gated (FW-B3 fleet-wide finding)
         FMC_SERIAL / FMC_SPI / uprintf platform abstraction
         SPI transaction delay fix (open item #16 closed)
3.2.3 — SAMD21 baseline (session 35/36 state)
3.2.0 — ICD v3.5.1 session 33 — TIME_BITS, PTP integration
```
