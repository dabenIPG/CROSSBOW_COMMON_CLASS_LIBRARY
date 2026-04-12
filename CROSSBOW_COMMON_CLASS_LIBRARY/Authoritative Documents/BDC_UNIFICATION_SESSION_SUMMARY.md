# BDC V1/V2 Unification — Session Summary

**Date:** 2026-04-11
**Session type:** Hardware unification — BDC Controller 1.0 Rev A
**Status:** ✅ COMPLETE — all firmware, C#, and documentation changes applied

---

## 1. What Was Done

Full V1/V2 hardware unification of the BDC (Beam Director Controller), following the
established MCC/TMC pattern. Single unified codebase for both hardware revisions,
selected at compile time via `-DHW_REV_V1` / `-DHW_REV_V2`.

---

## 2. Hardware Delta (V1 → V2)

| Signal | V1 | V2 |
|---|---|---|
| `PIN_VICOR1_ENABLE` GPIO 7 | LOW = ON (NC opto) | **HIGH = ON** (non-inverted) |
| `PIN_TEMP_VICOR` | GPIO 0 | GPIO **20** |
| `PIN_TEMP_RELAY` GPIO 19 | Not present | New NTC thermistor |
| `PIN_TEMP_BAT` GPIO 18 | Not present | New NTC thermistor |
| `PIN_TEMP_USB` GPIO 16 | Not present | New NTC thermistor |
| `PIN_IP175_RESET` GPIO 52 | Not present | IP175 Ethernet switch reset |
| `PIN_SWITCH_DISABLE` GPIO 64 | Not present | IP175 switch power disable |
| `PIN_DIG2_ENABLE` GPIO 42 | Defined, never used | Removed |
| Relays 1–4 (GPIO 2/3/4/6) | HIGH = ON | **Unchanged** |
| `PIN_DIG1_ENABLE` GPIO 8 | HIGH = arm | **Unchanged** |

---

## 3. Files Changed

### Firmware (Arduino/STM32F7)

| File | Changes |
|---|---|
| `hw_rev.hpp` | **NEW** — BDC revision gate. `BDC_HW_REV_BYTE`, `POL_VICOR_ON/OFF` macros. Same structure as MCC/TMC. |
| `pin_defs_bdc.hpp` | Full replacement — V2 pin constants, polarity macros, consumes `hw_rev.hpp`. `PIN_DIG2_ENABLE` removed. |
| `bdc.hpp` | Include `hw_rev.hpp` first; `HEALTH_BITS()` / `POWER_BITS()` replacing `STATUS_BITS()` / `STATUS_BITS2()`; `isSwitchDisabled` + V2 temp members added; header updated to v3.3.0. |
| `BDC.ino` | FW version 3.2.4 → 3.3.0; Vicor boot state uses `POL_VICOR_OFF`; IP175 init block added (#if HW_REV_V2). |
| `bdc.cpp` EnableVicor() | `en ? LOW : HIGH` → `en ? POL_VICOR_ON : POL_VICOR_OFF` |
| `bdc.cpp` PollTemp() | 3 new `calcTemp()` calls for relay/bat/usb sensors (#if HW_REV_V2) |
| `bdc.cpp` buildReg01() | `STATUS_BITS()` → `HEALTH_BITS()`, `STATUS_BITS2()` → `POWER_BITS()`; pack `HW_REV` [392] + new temps [393–395] |
| `bdc.cpp` PRINT_REG() | Renamed register labels; added HW_REV + V2 temp lines |
| `bdc.cpp` SERIAL_CMD HELP | 4-section box: COMMON / SPECIFIC / TRC / FMC |
| `bdc.cpp` SERIAL_CMD STATUS | `STATUS_BITS` → `HEALTH_BITS`, `STATUS_BITS2` → `POWER_BITS`; stale bit decode fixed (ntpUsingFallback etc. removed from byte [10] — moved to TIME_BITS session 32) |
| `bdc.cpp` SERIAL_CMD TEMPS | V2 sensor lines added (#if HW_REV_V2) |
| `bdc.cpp` SERIAL_CMD TIME | `STATUS_BITS()` → `HEALTH_BITS()` (missed in initial pass, fixed at compile) |
| `bdc.cpp` SERIAL_CMD POWER | **New command** — PDU status: Vicor, relays 1–4, IP175 switch (V2) |
| `bdc.cpp` SERIAL_CMD HW | **New command** — hardware revision, Vicor polarity, V2 features |
| `bdc.cpp` SERIAL_CMD SWRESET | **New command** — V2 only, pulse IP175 reset 100ms |
| `bdc.cpp` SERIAL_CMD SWDISABLE | **New command** — V2 only, control IP175 switch power |
| `bdc.cpp` SERIAL_CMD TRC | Full decoded output (was hex-only dump); new fields: version, state/mode, cam status bits, tracker status bits, tx/ty, atX0/atY0, focusScore, nccScore, jetsonTemp/CPU, epochTime |
| `bdc.cpp` SERIAL_CMD FMC | Full decoded output (was hex-only dump); new fields: state, stat bits, stage pos/err/status, FSM rb pos, epochTime, FMC version, MCU temp + BDC commanded FSM state |
| `bdc.cpp` SERIAL_CMD CAM | **New command** — mirrors 0xD0, sets active camera VIS/MWIR |
| `bdc.cpp` SERIAL_CMD VIEW | **New command** — mirrors 0xDE, sets view mode CAM1/CAM2/PIP4/PIP8 |
| `bdc.cpp` SERIAL_CMD TRACKER | **New command** — mirrors 0xDB, enables/disables MOSSE tracker (ID=1, stub for future) |
| `bdc.cpp` SERIAL_CMD FSM | **New command** — mirrors 0xF3, sets FSM commanded position + passes to FMC |
| `bdc.cpp` SERIAL_CMD STAGE | **New command** — mirrors 0xFB, sets stage position + passes to FMC |

### C# (ENG GUI / CROSSBOW library)

| File | Changes |
|---|---|
| `MSG_BDC.cs` | `HealthBits`/`PowerBits` replacing `StatusBits`/`StatusBits2` (compat aliases retained); `HW_REV`, `IsV1`, `IsV2`, `HW_REV_Label` added; `TEMP_RELAY`, `TEMP_BAT`, `TEMP_USB` added; `isSwitchEnabled` added; ParseMSG01 bytes [392–395] added |
| `frmBDC.cs` | Bug fix: `lbl_BDC_Time_Bits` now populated from `TimeBits`; `StatusBits`→`HealthBits`, `StatusBits2`→`PowerBits` in bit display; `TEMPERATURE_VICOR` added to temp label; `mb_Vicor_Enabled_rb`, `mb_Relay1-4_Enabled_rb` now wired to `PowerBits` accessors |

### Documentation

| File | Changes |
|---|---|
| `hw_rev.hpp` | New file — BDC section |
| `BDC_HW_DELTA.md` | New file — full decision log, pin delta, register layout, serial spec |
| `ARCHITECTURE.md` | v3.3.5 → v3.3.6; §10 header FW v3.0.1→v3.3.0; new §10.1 Role with V1/V2 table; §10.1–10.7 renumbered to §10.2–10.8; new §10.9 Build Configuration; §15 BDC 3.2.0→3.3.0; §16 BDC compat matrix row added |
| `CROSSBOW_ICD_INT_ENG.md` | v3.5.0 → v3.5.1; BDC REG1 byte [10] `STAT BITS`→`HEALTH_BITS`; byte [11] `STAT BITS2`→`POWER_BITS`; bytes [392–395] added; defined count 392→396 |
| `CROSSBOW_ICD_INT_OPS.md` | v3.5.0 → v3.5.1; same register table edits; FW version table BDC 3.2.0→3.3.0 |

---

## 4. REG1 Register Changes (breaking)

| Byte | V1 (old) | V2 (new) | Notes |
|---|---|---|---|
| [10] | `BDC STAT BITS` — bit 0: isReady; 1–7: RES/stale | `HEALTH_BITS` — bit 0: isReady; bit 1: isSwitchEnabled (V2); 2–7: RES | Breaking rename — ICD v3.5.1 |
| [11] | `BDC STAT BITS2` — isPid/vPid/FT/Vicor/Relay1-4 | `POWER_BITS` — **same bits, rename only** | Breaking rename — ICD v3.5.1 |
| [392] | RESERVED | `HW_REV` — 0x01=V1, 0x02=V2 | New |
| [393] | RESERVED | `TEMP_RELAY` int8 °C (V2 live; V1=0) | New |
| [394] | RESERVED | `TEMP_BAT` int8 °C (V2 live; V1=0) | New |
| [395] | RESERVED | `TEMP_USB` int8 °C (V2 live; V1=0) | New |
| [391] | TIME_BITS | TIME_BITS | **Unchanged** |
| [396–511] | RESERVED (120 bytes) | RESERVED (116 bytes) | Headroom reduced by 4 |

---

## 5. New Serial Commands

| Command | Section | Description |
|---|---|---|
| `HW` | SPECIFIC | Hardware revision, Vicor polarity, V2 features |
| `POWER` | SPECIFIC | Full PDU: Vicor, relays 1–4, IP175 switch (V2) |
| `SWRESET` | SPECIFIC (V2 only) | Pulse PIN_IP175_RESET LOW for 100ms |
| `SWDISABLE <0\|1>` | SPECIFIC (V2 only) | Control PIN_SWITCH_DISABLE |
| `CAM <VIS\|MWIR>` | TRC | Set active camera — mirrors 0xD0 |
| `VIEW <CAM1\|CAM2\|PIP4\|PIP8>` | TRC | Set view mode — mirrors 0xDE |
| `TRACKER <ON\|OFF>` | TRC | Enable/disable MOSSE — mirrors 0xDB ID=1 |
| `FSM <x> <y>` | FMC | Set FSM commanded position — mirrors 0xF3 |
| `STAGE <pos>` | FMC | Set stage position — mirrors 0xFB |

Existing `TRC` and `FMC` commands replaced with full field-decoded output
(was raw hex dump only).

---

## 6. Build Instructions

```
V1 build:  -DHW_REV_V1   (or leave undefined — V1 is default)
V2 build:  -DHW_REV_V2

FW version: VERSION_PACK(3,3,0)  =  0x03003000
```

---

## 7. Open Items (carry forward)

| ID | Item | Priority |
|---|---|---|
| BDC-GUI-1 | Add designer controls for V2 fields: `HW_REV_Label`, `TEMP_RELAY`, `TEMP_BAT`, `TEMP_USB`, `isSwitchEnabled` to `frmBDC` | 🟡 Medium |
| BDC-GUI-2 | Wire new controls once added: `lbl_BDC_HW_Rev`, `lbl_BDC_temps_v2` (visible only if `IsV2`), `mb_BDC_switchEnabled_rb` | 🟡 Medium |
| FW-C3 | BDC Fuji boot status — `fuji.SETUP()` deferred post-boot, FUJI_WAIT always times out | 🟡 Medium |
| FW-C4 | BDC A1 ARP backoff not working — `A1 OFF` workaround when TRC offline | 🟡 Medium |
| FW-B3 | PTP DELAY_REQ W5500 contention — `isPTP_Enabled=false` fleet-wide | 🔴 High |
| TRACKER-2 | `TRACKER <id> <ON\|OFF>` — extend to accept arbitrary tracker ID (currently hardcoded ID=1) | 🟢 Low |

---

## 8. Verified

- ✅ Firmware compiles clean (V1 default build)
- ✅ C# compiles clean
- ✅ All ICD documents updated to v3.5.1
- ✅ ARCHITECTURE.md updated to v3.3.6
- ✅ BDC_HW_DELTA.md produced as permanent record
