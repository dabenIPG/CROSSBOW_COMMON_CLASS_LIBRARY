# BDC_HW_DELTA.md — Beam Director Controller V1/V2 Hardware Delta

**Document Version:** 1.1 — VICOR POLARITY CORRECTED
**Date:** 2026-04-11
**Hardware:** V2 = BDC Controller 1.0 Rev A
**Reference:** MCC_HW_DELTA.md pattern · ARCHITECTURE.md §10 · ICD v3.5.0
**Status:** ⚠ PENDING CONFIRMATION of C1–C8 before any code is written.

---

## 1. Executive Summary

| Q | Answer | Impact |
|---|---|---|
| Q1 — pins changed? | Yes — `PIN_TEMP_VICOR` 0→20. See §2. | `pin_defs_bdc.hpp` |
| **Q2 — Vicor polarity?** | **CHANGED — V1 LOW=ON → V2 HIGH=ON** | `pin_defs_bdc.hpp`, `EnableVicor()`, `BDC.ino` setup() |
| Q3 — Relay polarity? | UNCHANGED — HIGH=ON on all 4 | None |
| Q4 — Relay 4 assigned? | No — still spare | None |
| Q5 — Any relay retired? | No — all 4 retained | None |
| Q6 — DIG2 assigned? | Removed (commented out) | Delete from `pin_defs_bdc.hpp` |
| Q7 — Vicor thermistor retained? | Yes — pin moved 0→20 | `pin_defs_bdc.hpp` |
| Q8 — Vicor count? | 1 — unchanged | None |
| Q9 — New driver header? | No | Minimal gate in `pin_defs_bdc.hpp` only |
| Q10 — HW_REV byte placement? | Byte [392] | `buildReg01()`, `MSG_BDC.cs` |
| Q11 — FW version? | `3.3.0` proposed ⟵ confirm | `BDC.ino` |

**Vicor polarity change requires a compile-time revision gate.** Since this is the only
compile-time difference (no conditional driver includes, no conditional class members),
the gate lives entirely in `pin_defs_bdc.hpp` as `POL_VICOR_ON` / `POL_VICOR_OFF` macros.
**No `hw_rev.hpp` entry needed** — BDC has none of the conditions that required MCC/TMC
to use that file (no conditional `#include`, no conditional class member).

The build flag `-DHW_REV_V2` (or `-DHW_REV_V1`) is the only new compiler argument.
It is defined once in `pin_defs_bdc.hpp` with a clear comment, selected at build time.

---

## 2. Complete V1 → V2 Pin Diff

### 2.1 Unchanged pins

| Constant | GPIO | V1 active logic | V2 active logic |
|---|---|---|---|
| `PIN_WIZ_RESET` | 61 | — | Unchanged |
| `BDPIN_LED_USER_5` | 36 | — | Unchanged |
| `PIN_VICOR1_ENABLE` | 7 | **LOW = ON** | **HIGH = ON** ← polarity flipped |
| `PIN_RELAY1_ENABLE` | 2 | HIGH = ON | Unchanged |
| `PIN_RELAY2_ENABLE` | 3 | HIGH = ON | Unchanged |
| `PIN_RELAY3_ENABLE` | 4 | HIGH = ON | Unchanged |
| `PIN_RELAY4_ENABLE` | 6 | HIGH = ON | Unchanged |
| `PIN_DIG1_ENABLE` | 8 | HIGH = arm | Unchanged |

> `PIN_VICOR1_ENABLE` stays on GPIO 7. Only the polarity inverts.
> V1: inverted drive (NC opto-isolator). V2: non-inverted drive (like relays).

### 2.2 Changed pins

| Constant | V1 GPIO | V2 GPIO | Change |
|---|---|---|---|
| `PIN_TEMP_VICOR` | 0 | **20** | Analog pin remap only |

### 2.3 New pins (V2 only)

| Constant | GPIO | Direction | Boot state | Active logic | Function |
|---|---|---|---|---|---|
| `PIN_IP175_RESET` | 52 | OUTPUT | **HIGH** | LOW pulse = reset | IP175 Ethernet switch reset |
| `PIN_SWITCH_DISABLE` | 64 | OUTPUT | **LOW** | HIGH = disable | IP175 switch power disable |
| `PIN_TEMP_RELAY` | 19 | Analog IN | — | ADC read | NTC thermistor — relay area |
| `PIN_TEMP_BAT` | 18 | Analog IN | — | ADC read | NTC thermistor — battery-in area |
| `PIN_TEMP_USB` | 16 | Analog IN | — | ADC read | NTC thermistor — USB 5V area |

### 2.4 Removed pins

| Constant | V1 GPIO | Action |
|---|---|---|
| `PIN_DIG2_ENABLE` | 42 | Delete constant — was never used in V1 |

---

## 3. Polarity Gate — `pin_defs_bdc.hpp` Pattern

The Vicor polarity difference is the only compile-time variant. Both revisions use GPIO 7.
The macro pattern is the minimal possible gate — two lines inside an `#if` block:

```cpp
// ── Build target selection ────────────────────────────────────────────────────
// Uncomment ONE of the following, or pass as compiler flag -DHW_REV_V2 / -DHW_REV_V1
// #define HW_REV_V1   // Original hardware — Vicor NC opto (LOW=ON)
// #define HW_REV_V2   // BDC Controller 1.0 Rev A — Vicor non-inverted (HIGH=ON)

#if defined(HW_REV_V2)
  #define BDC_HW_REV_BYTE  0x02
  #define POL_VICOR_ON     HIGH    // V2: non-inverted drive
  #define POL_VICOR_OFF    LOW
#else
  // Default to V1 if no flag set — preserves current behaviour for existing builds
  #define BDC_HW_REV_BYTE  0x01
  #define POL_VICOR_ON     LOW     // V1: inverted drive (NC opto)
  #define POL_VICOR_OFF    HIGH
#endif
```

`EnableVicor()` and `BDC.ino` setup() consume `POL_VICOR_ON` / `POL_VICOR_OFF`.
No other file needs the `#if defined(HW_REV_V2)` guard.

---

## 4. Surgical Changes — Complete Before/After

### Change 1 — `pin_defs_bdc.hpp` (replace entire file)

**V1 (current):**
```cpp
#pragma once
#include "Arduino.h"

#define PIN_WIZ_RESET       61
#define BDPIN_LED_USER_5        36

//VICOR ENABLEs  //NOTE: PIN LOW IS ON
#define PIN_VICOR1_ENABLE   7

// RELAYS
#define PIN_RELAY1_ENABLE   2
#define PIN_RELAY2_ENABLE   3
#define PIN_RELAY3_ENABLE   4
#define PIN_RELAY4_ENABLE   6

// VICOR TEMP
#define PIN_TEMP_VICOR      0

// DIGITAL ENABLES
#define PIN_DIG1_ENABLE     8   // BDC VOTE OUTPUT
#define PIN_DIG2_ENABLE     42
```

**V2 (new — full replacement):**
```cpp
#pragma once
#include "Arduino.h"

// ── Build target selection ────────────────────────────────────────────────────
// Pass as compiler flag -DHW_REV_V2 (BDC Controller 1.0 Rev A)
//                    or -DHW_REV_V1 (original hardware)
// Default: V1 if neither flag is set.
#if defined(HW_REV_V2)
  #define BDC_HW_REV_BYTE  0x02
  #define POL_VICOR_ON     HIGH    // V2: non-inverted drive
  #define POL_VICOR_OFF    LOW
#else
  #define BDC_HW_REV_BYTE  0x01
  #define POL_VICOR_ON     LOW     // V1: inverted drive (NC opto)
  #define POL_VICOR_OFF    HIGH
#endif

// ── Ethernet ──────────────────────────────────────────────────────────────────
#define PIN_WIZ_RESET           61
#define PIN_IP175_RESET         52   // Low pulse to reset — NEW V2
#define PIN_SWITCH_DISABLE      64   // HIGH = disabled (power removed) — NEW V2

// ── LEDs ──────────────────────────────────────────────────────────────────────
#define BDPIN_LED_USER_5        36

// ── Vicor PSU ─────────────────────────────────────────────────────────────────
// V1: LOW=ON (inverted, NC opto).  V2: HIGH=ON (non-inverted).
// Use POL_VICOR_ON / POL_VICOR_OFF — do NOT write literal HIGH/LOW for this pin.
#define PIN_VICOR1_ENABLE       7

// ── Relays (HIGH = ON, both revisions) ───────────────────────────────────────
#define PIN_RELAY1_ENABLE       2
#define PIN_RELAY2_ENABLE       3
#define PIN_RELAY3_ENABLE       4
#define PIN_RELAY4_ENABLE       6

// ── Temperature sensors ───────────────────────────────────────────────────────
#define PIN_TEMP_VICOR          20   // V1: was 0 — CHANGED V2
#define PIN_TEMP_RELAY          19   // NEW V2 — relay area NTC thermistor
#define PIN_TEMP_BAT            18   // NEW V2 — battery-in area NTC thermistor
#define PIN_TEMP_USB            16   // NEW V2 — USB 5V area NTC thermistor

// ── Digital enables ───────────────────────────────────────────────────────────
#define PIN_DIG1_ENABLE         8    // BDC vote output (HIGH = arm)
// PIN_DIG2_ENABLE 42 — REMOVED V2 (was defined but never used in V1)
```

---

### Change 2 — `BDC.ino` setup() — PDU GPIO initialization block

**Location:** lines 84–101 (the `// BDC GPIO -- PDU` block)

**V1 (current):**
```cpp
    // BDC GPIO -- PDU
    digitalWrite(PIN_VICOR1_ENABLE, HIGH);   // HIGH = OFF (inverted)
    pinMode(PIN_VICOR1_ENABLE,             OUTPUT);

    digitalWrite(PIN_RELAY1_ENABLE, LOW);
    pinMode(PIN_RELAY1_ENABLE,             OUTPUT);

    digitalWrite(PIN_RELAY2_ENABLE, LOW);
    pinMode(PIN_RELAY2_ENABLE,             OUTPUT);

    digitalWrite(PIN_RELAY3_ENABLE, LOW);
    pinMode(PIN_RELAY3_ENABLE,             OUTPUT);

    digitalWrite(PIN_RELAY4_ENABLE, LOW);
    pinMode(PIN_RELAY4_ENABLE,             OUTPUT);

    digitalWrite(PIN_DIG1_ENABLE, LOW);
    pinMode(PIN_DIG1_ENABLE,               OUTPUT);
```

**V2 (new):**
```cpp
    // BDC GPIO -- PDU
    digitalWrite(PIN_VICOR1_ENABLE, POL_VICOR_OFF);   // safe-off (V1=HIGH, V2=LOW)
    pinMode(PIN_VICOR1_ENABLE,             OUTPUT);

    digitalWrite(PIN_RELAY1_ENABLE, LOW);
    pinMode(PIN_RELAY1_ENABLE,             OUTPUT);

    digitalWrite(PIN_RELAY2_ENABLE, LOW);
    pinMode(PIN_RELAY2_ENABLE,             OUTPUT);

    digitalWrite(PIN_RELAY3_ENABLE, LOW);
    pinMode(PIN_RELAY3_ENABLE,             OUTPUT);

    digitalWrite(PIN_RELAY4_ENABLE, LOW);
    pinMode(PIN_RELAY4_ENABLE,             OUTPUT);

    digitalWrite(PIN_DIG1_ENABLE, LOW);
    pinMode(PIN_DIG1_ENABLE,               OUTPUT);

    // IP175 Ethernet switch -- NEW V2
    digitalWrite(PIN_IP175_RESET,    HIGH);   // HIGH = not resetting  ⟵ C1
    pinMode(PIN_IP175_RESET,         OUTPUT);

    digitalWrite(PIN_SWITCH_DISABLE, LOW);    // LOW  = switch powered ⟵ C2
    pinMode(PIN_SWITCH_DISABLE,      OUTPUT);
```

> **One line changed:** `HIGH` → `POL_VICOR_OFF` (line 85 / first `digitalWrite`).
> **Six lines added:** IP175 init block at the end.

---

### Change 3 — `bdc.cpp` `EnableVicor()` — lines 2727–2734

**V1 (current):**
```cpp
void BDC::EnableVicor(bool en)
{
    isVicorEnabled = en;
    digitalWrite(PIN_VICOR1_ENABLE, en ? LOW : HIGH);   // inverted: LOW = ON
}
```

**V2 (new):**
```cpp
void BDC::EnableVicor(bool en)
{
    isVicorEnabled = en;
    digitalWrite(PIN_VICOR1_ENABLE, en ? POL_VICOR_ON : POL_VICOR_OFF);
}
```

> One line changed. Comment removed — the macro name is self-documenting.

---

### Change 4 — `bdc.hpp` — new member variables (after `tempVicor` line ~468)

**V1 (current — line 468):**
```cpp
    float tempVicor = 11.2f;
```

**V2 (new):**
```cpp
    float tempVicor = 0.0f;    // Vicor heatsink temp — analog GPIO 20 (was GPIO 0 on V1)
    float tempRelay = 0.0f;    // Relay area temp     — analog GPIO 19 — V2 only
    float tempBat   = 0.0f;    // Battery-in temp     — analog GPIO 18 — V2 only
    float tempUsb   = 0.0f;    // USB 5V area temp    — analog GPIO 16 — V2 only
```

> `tempVicor` init value corrected from `11.2f` (leftover test value) to `0.0f`.
> Three lines added.

---

### Change 5 — `bdc.hpp` — header comment (lines 1–10)

**V1 (current):**
```cpp
// bdc.hpp  -  CROSSBOW Beam Director Controller
// ---------------------------------------------------------------------------
// Session 10  -  FW v3.0.0
// Tri-port architecture:
//   ...
// Register layout: ICD v1.7 session 4 (512-byte fixed block)
// ---------------------------------------------------------------------------
```

**V2 (new):**
```cpp
// bdc.hpp  —  CROSSBOW Beam Director Controller
// ---------------------------------------------------------------------------
// Unified V1/V2 hardware abstraction. Select revision with build flag:
//   -DHW_REV_V1   Original hardware   (Vicor NC opto LOW=ON)
//   -DHW_REV_V2   Controller 1.0 RevA (Vicor non-inverted HIGH=ON; new temp sensors)
//
// Tri-port architecture:
//   A1  port 10019  INT magic  RX <- FMC (.23), TRC (.22), MCC (.10); TX -> TRC (.22)
//   A2  port 10018  INT magic  RX + TX  (engineering GUI / HMI, up to 4 clients)
//   A3  port 10050  EXT magic  RX + TX  (external integration, up to 2 clients)
//
// Register layout: ICD v3.5.0 (512-byte fixed block, byte 392 = HW_REV)
// FW version: 3.3.0
// See BDC_HW_DELTA.md for full change catalogue.
// ---------------------------------------------------------------------------
```

---

### Change 6 — `bdc.cpp` `PollTemp()` — lines 2768–2776

**V1 (current):**
```cpp
void BDC::PollTemp()
{
    if ((millis() - lastTick_Poll) < TICK_Poll) return;
    lastTick_Poll = millis();
    tempVicor = calcTemp(PIN_TEMP_VICOR);
}
```

**V2 (new):**
```cpp
void BDC::PollTemp()
{
    if ((millis() - lastTick_Poll) < TICK_Poll) return;
    lastTick_Poll = millis();
    tempVicor = calcTemp(PIN_TEMP_VICOR);   // GPIO 20 (was 0 on V1)
#if defined(HW_REV_V2)
    tempRelay = calcTemp(PIN_TEMP_RELAY);   // GPIO 19
    tempBat   = calcTemp(PIN_TEMP_BAT);     // GPIO 18
    tempUsb   = calcTemp(PIN_TEMP_USB);     // GPIO 16
#endif
}
```

> `calcTemp()` is the same NTC formula — assumes same thermistor type. ⟵ **C5 confirm.**
> Four lines added (3 reads + 1 guard). On V1 build, compiles away entirely.

---

### Change 7 — `bdc.cpp` `buildReg01()` — pack HW_REV + new temps

**Location:** find the TIME_BITS pack line in `buildReg01()`, then add immediately after.

Find this existing line (packs TIME_BITS at byte [391]):
```cpp
    buf[391] = TIME_BITS();
```

**Add immediately after:**
```cpp
    // [392] HW_REV — self-detecting byte for MSG_BDC.cs
    buf[392] = BDC_HW_REV_BYTE;

    // [393-395] New V2 temperature sensors (0x00 on V1 builds — backward compatible)
#if defined(HW_REV_V2)
    buf[393] = (int8_t)constrain((int)tempRelay, -128, 127);
    buf[394] = (int8_t)constrain((int)tempBat,   -128, 127);
    buf[395] = (int8_t)constrain((int)tempUsb,   -128, 127);
#else
    buf[393] = 0x00;
    buf[394] = 0x00;
    buf[395] = 0x00;
#endif
```

> 8 lines added. On V1 build, temps pack as 0x00 (safe default for old clients).

---

### Change 8 — `bdc.cpp` `PRINT_REG()` — add new bytes to serial dump

**Location:** after the TIME_BITS line in PRINT_REG() (~line 1920):

Find:
```cpp
    Serial.println(F(" [391]    TIME_BITS (session 32)"));
```

**Replace with:**
```cpp
    Serial.println(F(" [391]    TIME_BITS (session 32)"));
    Serial.printf (" [392]    HW_REV:          0x%02X  (%s)\n",
        BDC_HW_REV_BYTE, (BDC_HW_REV_BYTE == 0x02) ? "V2 Controller 1.0 RevA" : "V1");
#if defined(HW_REV_V2)
    Serial.print(F(" [393]    TEMP_RELAY:      ")); Serial.print(tempRelay, 1); Serial.println(F(" C"));
    Serial.print(F(" [394]    TEMP_BAT:        ")); Serial.print(tempBat,   1); Serial.println(F(" C"));
    Serial.print(F(" [395]    TEMP_USB:        ")); Serial.print(tempUsb,   1); Serial.println(F(" C"));
#endif
```

---

### Change 9 — `BDC.ino` `FW_VERSION` — line 18

**V1 (current):**
```cpp
const uint32_t FW_VERSION = VERSION_PACK(3, 2, 4);
```

**V2 (new):**
```cpp
const uint32_t FW_VERSION = VERSION_PACK(3, 3, 0);   // unified V1/V2 — BDC_HW_DELTA.md
```

> ⟵ **C6 confirm version.**

---

### Change 10 — `MSG_BDC.cs` `ParseMSG01()` — after TimeBits line 529

**V1 (current — line 529):**
```cpp
            // [391] TIME_BITS (session 32) — consolidated time source status
            TimeBits = msg[ndx]; ndx++;
```

**V2 (new):**
```cpp
            // [391] TIME_BITS (session 32) — consolidated time source status
            TimeBits = msg[ndx]; ndx++;

            // [392] HW_REV — 0x01=V1, 0x02=V2 (BDC Controller 1.0 Rev A)
            HW_REV = msg[ndx]; ndx++;

            // [393-395] V2 temperature sensors — 0x00 on V1 (backward-compatible)
            TEMP_RELAY = (sbyte)msg[ndx]; ndx++;
            TEMP_BAT   = (sbyte)msg[ndx]; ndx++;
            TEMP_USB   = (sbyte)msg[ndx]; ndx++;
```

---

### Change 11 — `MSG_BDC.cs` new properties (after `TEMPERATURE_VICOR` ~line 188)

**V1 (current — line 188):**
```csharp
        public sbyte TEMPERATURE_VICOR { get; private set; } = 0;
```

**V2 (new — insert after that line):**
```csharp
        public sbyte TEMPERATURE_VICOR { get; private set; } = 0;

        // ── V2 temperature sensors (BDC Controller 1.0 Rev A) ────────────────
        // 0 on V1 — backward-compatible (was RESERVED 0x00)
        public sbyte TEMP_RELAY { get; private set; } = 0;
        public sbyte TEMP_BAT   { get; private set; } = 0;
        public sbyte TEMP_USB   { get; private set; } = 0;

        // ── Hardware revision ─────────────────────────────────────────────────
        public byte   HW_REV      { get; private set; } = 0;
        public bool   IsV1        => HW_REV == 0x01;
        public bool   IsV2        => HW_REV == 0x02;
        public string HW_REV_Label => HW_REV == 0x01 ? "V1"
                                    : HW_REV == 0x02 ? "V2 — Controller 1.0 Rev A"
                                    : $"unknown (0x{HW_REV:X2})";
```

---

## 5. REG1 New Bytes Summary

| Byte | V1 | V2 | Type | Field | Notes |
|---|---|---|---|---|---|
| [391] | TIME_BITS | TIME_BITS | uint8 | Unchanged | Already unified |
| **[392]** | `0x00` RESERVED | **`HW_REV`** | uint8 | `0x01`=V1, `0x02`=V2 | Self-detecting |
| **[393]** | `0x00` RESERVED | **`TEMP_RELAY`** | int8 | Relay area °C | V2 live; V1=0 |
| **[394]** | `0x00` RESERVED | **`TEMP_BAT`** | int8 | Battery-in °C | V2 live; V1=0 |
| **[395]** | `0x00` RESERVED | **`TEMP_USB`** | int8 | USB 5V area °C | V2 live; V1=0 |
| [396–511] | RESERVED | RESERVED | — | 116 bytes headroom | |

---

## 6. Confirmations Required (C1–C8)

| # | Item | Proposed | ✓/✗ |
|---|---|---|---|
| **C1** | `PIN_IP175_RESET` boot state | HIGH (not-resetting) | |
| **C2** | `PIN_SWITCH_DISABLE` boot state | LOW (switch powered) | |
| **C3** | Runtime control for IP175 pins | NO — setup() init only | |
| **C4** | Expose switch state in REG1 | NO — not needed initially | |
| **C5** | New thermistors same NTC type as Vicor thermistor (same `BCOEFFICIENT`, `THERMISTORNOMINAL`) | YES assumed | |
| **C6** | BDC V2 FW version | `3.3.0` | |
| **C7** | REG1 byte [392] = HW_REV | Confirmed | |
| **C8** | Bytes [393–395] = TEMP_RELAY/BAT/USB in that order | Confirmed | |

---

## 7. Total Change Footprint

| File | Changes | Net new lines |
|---|---|---|
| `pin_defs_bdc.hpp` | Full replacement with V2 content | +14 |
| `BDC.ino` setup() | 1 line changed (Vicor boot); +6 lines (IP175 init) | +5 |
| `BDC.ino` FW_VERSION | Version bump | 0 |
| `bdc.hpp` header | Comment rewrite | 0 |
| `bdc.hpp` temp vars | 3 new members | +3 |
| `bdc.cpp` EnableVicor() | 1 line changed | 0 |
| `bdc.cpp` PollTemp() | 4 lines added | +4 |
| `bdc.cpp` buildReg01() | 8 lines added | +8 |
| `bdc.cpp` PRINT_REG() | 5 lines changed/added | +4 |
| `MSG_BDC.cs` ParseMSG01 | 6 lines added | +6 |
| `MSG_BDC.cs` properties | 10 lines added | +10 |
| **Total** | | **~54 net new lines** |

No `hw_rev.hpp` changes. No ICD breaking changes. No command changes. No A3 whitelist changes.

---

*Confirm C1–C8, then implementation proceeds change-by-change with your edits.*
