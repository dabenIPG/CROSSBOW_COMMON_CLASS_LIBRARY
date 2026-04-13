# BDC_HW_DELTA.md — Beam Director Controller V1/V2 Hardware Delta

**Document Version:** 2.0 — FINAL APPROVED
**Date:** 2026-04-11
**Hardware:** V2 = BDC Controller 1.0 Rev A
**FW Version:** 3.3.0 (VERSION_PACK(3,3,0))
**ICD:** Breaking change — HEALTH_BITS / POWER_BITS rename (parallel to MCC ICD v3.4.0)
**Status:** ⚠ AWAITING FINAL SIGN-OFF — no code written until confirmed

---

## 1. Decision Log — All Items Resolved

| ID | Item | Decision |
|---|---|---|
| C1 | PIN_IP175_RESET boot state | HIGH (not-resetting) ✅ |
| C2 | PIN_SWITCH_DISABLE boot state | LOW (switch powered) ✅ |
| C3 | IP175 runtime control | Serial commands SWRESET / SWDISABLE (V2 only) ✅ |
| C4 | Switch state visibility | Bit 1 of HEALTH_BITS (byte [10]) — no new byte ✅ |
| C5 | New thermistors NTC type | Same as existing Vicor thermistor (same BCOEFFICIENT) ✅ |
| C6 | FW version | 3.3.0 ✅ |
| C7 | REG1 layout | [392]=HW_REV [393]=TEMP_RELAY [394]=TEMP_BAT [395]=TEMP_USB ✅ |
| C8 | Switch status byte | **Resolved into HEALTH_BITS bit 1 — no new byte [396]** ✅ |
| S1 | FSMHOME command | **Removed — FSM <x> <y> only** ✅ |
| S2 | TRACKER command | TRACKER <ON\|OFF> hardcoded ID=1 (TrackB) — stub for future ✅ |
| S3 | VIEW command | VIEW <CAM1\|CAM2\|PIP4\|PIP8> → VIEW_MODES 0/1/2/3 ✅ |
| D1 | STATUS_BITS rename | **HEALTH_BITS / POWER_BITS (parallel to MCC)** — breaking ICD change ✅ |
| D2 | hw_rev.hpp | **Add BDC section** — bdc.hpp includes hw_rev.hpp before pin_defs_bdc.hpp ✅ |
| D3 | V2-only commands | Fully hidden on V1 (#if HW_REV_V2 gates HELP text + handlers) ✅ |
| D4 | ARCHITECTURE.md | §10 V1/V2 table + FW version + compat matrix row ✅ |

---

## 2. Pin Delta — Final

### 2.1 Unchanged

GPIO 7 (Vicor), 2/3/4/6 (Relays 1–4), 8 (DIG1 safety), 36 (LED), 61 (WIZ reset) — same on V2.

### 2.2 Changed

| Constant | V1 GPIO | V2 GPIO |
|---|---|---|
| `PIN_TEMP_VICOR` | 0 | **20** |

### 2.3 New (V2 only)

| Constant | GPIO | Boot state | Logic | Function |
|---|---|---|---|---|
| `PIN_IP175_RESET` | 52 | HIGH | LOW pulse = reset | IP175 switch reset |
| `PIN_SWITCH_DISABLE` | 64 | LOW | HIGH = disabled | IP175 switch power |
| `PIN_TEMP_RELAY` | 19 | — | ADC | Relay area NTC thermistor |
| `PIN_TEMP_BAT` | 18 | — | ADC | Battery-in NTC thermistor |
| `PIN_TEMP_USB` | 16 | — | ADC | USB 5V NTC thermistor |

### 2.4 Polarity change

| Signal | V1 | V2 |
|---|---|---|
| `PIN_VICOR1_ENABLE` (GPIO 7) | LOW = ON (NC opto) | **HIGH = ON** |
| All relays (GPIO 2/3/4/6) | HIGH = ON | Unchanged |

### 2.5 Removed

| Constant | V1 GPIO | Reason |
|---|---|---|
| `PIN_DIG2_ENABLE` | 42 | Removed — was defined but never used in V1 |

---

## 3. Register Layout — Final

### 3.1 HEALTH_BITS byte [10] — renamed from STATUS_BITS

Breaking change (parallel to MCC ICD v3.4.0). Old name was `BDC STAT BITS`.

```
bit 0: isReady                — both revisions
bit 1: isSwitchEnabled        — V2 only (0 on V1); =!isSwitchDisabled
bits 2–7: RES
```

**Stale bits cleaned up:** V1 `STATUS_BITS` decode in `SERIAL_CMD()` STATUS command had
stale bit fields (ntpUsingFallback/ntpHasFallback/usingPTP at bits 1–3, isUnsolicitedEnabled
at bit 7). Those moved to TIME_BITS [391] in session 32 and have been RES since. The rename
pass cleans up that stale decode simultaneously.

### 3.2 POWER_BITS byte [11] — renamed from STATUS_BITS2

Breaking change (parallel to MCC). Bit layout **unchanged** — rename only.

```
bit 0: isPidEnabled     — software, both revisions
bit 1: isvPidEnabled    — software, both revisions
bit 2: isFTTrackEnabled — software, both revisions
bit 3: isVicorEnabled   — hardware, both revisions (GPIO 7, polarity differs)
bit 4: isRelay1Enabled  — hardware, both revisions (GPIO 2, MWIR)
bit 5: isRelay2Enabled  — hardware, both revisions (GPIO 3, FUJI)
bit 6: isRelay3Enabled  — hardware, both revisions (GPIO 4, TRC)
bit 7: isRelay4Enabled  — hardware, both revisions (GPIO 6, spare)
```

No bit reassignment — purely a rename. Backward-compat aliases `StatusBits` / `StatusBits2`
retained in `MSG_BDC.cs`.

### 3.3 New REG1 bytes [392–395]

| Byte | V1 value | V2 value | Type | Field |
|---|---|---|---|---|
| [392] | `0x00` RESERVED | `HW_REV` | uint8 | `0x01`=V1, `0x02`=V2 |
| [393] | `0x00` RESERVED | `TEMP_RELAY` | int8 | Relay area °C |
| [394] | `0x00` RESERVED | `TEMP_BAT` | int8 | Battery-in °C |
| [395] | `0x00` RESERVED | `TEMP_USB` | int8 | USB 5V area °C |
| [396–511] | RESERVED | RESERVED | — | 116 bytes headroom |

All backward-compatible — V1 builds pack `0x00`.

### 3.4 TIME_BITS byte [391] — unchanged

Already unified. No V2 impact.

---

## 4. `hw_rev.hpp` — BDC Section to Add

```cpp
// ── BDC hardware revision ─────────────────────────────────────────────────────
// Build flag: -DHW_REV_V2  (BDC Controller 1.0 Rev A)
//             -DHW_REV_V1  (original hardware) — default if neither set
//
// Sole compile-time difference: Vicor drive polarity.
// V1: LOW=ON  (NC opto-isolator)
// V2: HIGH=ON (non-inverted)
//
// Consumed by: pin_defs_bdc.hpp (POL_VICOR_ON/OFF macros)
//              bdc.cpp EnableVicor(), BDC.ino setup()
//              bdc.cpp PollTemp(), buildReg01() (new temp sensors)
// ---------------------------------------------------------------------------
#if defined(HW_REV_V2)
  #define BDC_HW_REV_BYTE   0x02
  #define POL_VICOR_ON      HIGH    // V2: non-inverted drive
  #define POL_VICOR_OFF     LOW
#else
  #define BDC_HW_REV_BYTE   0x01
  #define POL_VICOR_ON      LOW     // V1: inverted drive (NC opto)
  #define POL_VICOR_OFF     HIGH
#endif
```

`bdc.hpp` include order becomes (line 12 onward):
```cpp
#include "hw_rev.hpp"        // ← ADD as first include — revision gate + polarity macros
#include <Ethernet.h>
#include "Arduino.h"
...
#include "pin_defs_bdc.hpp"  // consumes hw_rev.hpp macros — must come after
```

`pin_defs_bdc.hpp` drops any internal gate and consumes `POL_VICOR_ON/OFF` and
`BDC_HW_REV_BYTE` from `hw_rev.hpp`.

---

## 5. Command Changes — None to ICD

`0xBC` / `0xBD` are INT_ENG only (not on A3 — incorrect, see §5a below).

### 5a. Whitelist confirmation

All new serial commands mirror A3-whitelisted commands already:

| Serial cmd | Mirrors | A3 whitelist |
|---|---|---|
| `CAM` | `0xD0` | ✅ line 56 |
| `VIEW` | `0xDE` | ✅ line 57 |
| `TRACKER` | `0xDB` | ✅ line 57 |
| `FSM` | `0xF3` | ✅ line 58 |
| `STAGE` | `0xFB` | ✅ line 58 |
| `SWRESET` | serial only | N/A — no UDP command |
| `SWDISABLE` | serial only | N/A — no UDP command |

No whitelist changes required.

---

## 6. Serial Interface — Final Spec

### 6.1 HELP box (4 sections)

```
╔══ BDC — COMMON COMMANDS ═══════════════════════════════════╗
║  INFO              Build info, IP, link, port clients      ║
║  REG               Full REG1 register dump (all fields)    ║
║  STATUS            System state/mode + all bit fields      ║
║  TEMPS             All temperature sensors                 ║
║  TIME              Active time source + PTP/NTP status     ║
║  TIMESRC <PTP|NTP|AUTO|OFF>  Set time source policy        ║
║  PTPDEBUG <0-3>    Set PTP debug level                     ║
║  PTPDIAG ON|OFF    Suppress DELAY_REQ (contention test)    ║
║  A1 ON|OFF         Enable/disable A1 TX stream             ║
║  NTP               NTP sync status + server + epoch time   ║
║  NTPIP <a.b.c.d>   Set primary NTP server IP + resync      ║
║  NTPFB <a.b.c.d>   Set fallback NTP server (OFF to clear)  ║
║  NTPSYNC           Force immediate NTP resync              ║
║  DEBUG <0-3>       Set debug level  0=OFF 1=MIN 2=NORM     ║
║  STATE <n>         Set system state  0=OFF 1=STNDBY        ║
║                    2=ISR  3=COMBAT  4=MAINT  5=FAULT       ║
║  MODE  <n>         Set gimbal mode   0=OFF..5=FTRACK       ║
╠══ BDC — SPECIFIC COMMANDS ═════════════════════════════════╣
║  HW                Hardware revision + Vicor polarity      ║
║  POWER             PDU status — Vicor, relays, switch      ║
║  MCC               A1 MCC liveness + decoded vote bits     ║
║  REINIT <device>   Re-init device (0=NTP..7=PTP)           ║
║  ENABLE <dev> <0|1>  Enable/disable device                 ║
║  RELAY <1-4> <0|1>  Relay on/off                           ║
║  VICOR <0|1>        Vicor PSU enable                       ║
║  [V2] SWRESET       Pulse IP175 reset                      ║  ← hidden on V1
║  [V2] SWDISABLE <0|1>  Ethernet switch power               ║  ← hidden on V1
╠══ TRC COMMANDS ════════════════════════════════════════════╣
║  TRC               A1 liveness + decoded status + hex dump ║
║  CAM <VIS|MWIR>    Select active camera  (0xD0)            ║
║  VIEW <CAM1|CAM2|PIP4|PIP8>  Set view mode  (0xDE)         ║
║  TRACKER <ON|OFF>  Enable/disable MOSSE tracker  (0xDB)    ║
╠══ FMC COMMANDS ════════════════════════════════════════════╣
║  FMC               A1 liveness + decoded FSM/stage + hex   ║
║  FSM <x> <y>       Set FSM commanded position  (0xF3)      ║
║  STAGE <pos>       Set stage position  (0xFB)              ║
╚════════════════════════════════════════════════════════════╝
```

### 6.2 TEMPS command — V2-aware

```
  vicor  : xx.x C   (GPIO 20)
  relay  : xx.x C   (GPIO 19)   ← V2 build only
  bat    : xx.x C   (GPIO 18)   ← V2 build only
  usb    : xx.x C   (GPIO 16)   ← V2 build only
  tph    : xx.x C   xx.x hPa   xx.x %RH
  mcu    : xx.x C
```

### 6.3 POWER command

```
  vicor     : ON   (GPIO 7 — V2: HIGH=ON)
  relay 1   : ON   MWIR
  relay 2   : ON   FUJI
  relay 3   : ON   TRC
  relay 4   : OFF  spare
  switch    : ENABLED   ← V2 only line
```

### 6.4 HW command

```
  rev       : V2 (0x02)  BDC Controller 1.0 Rev A
  vicor pol : HIGH=ON (non-inverted)
  new temps : relay(GPIO19)  bat(GPIO18)  usb(GPIO16)
  switch    : IP175  reset=GPIO52  disable=GPIO64
```

### 6.5 TRC command — full decode

Fields read directly from `trc.buffer[]` using ICD §8.4 offsets:

```
  A1 alive:      YES    last 8 ms ago
  connected:     YES    ready: YES
  trc status:    0x07
  -- TRC REG1 decoded --
  version:       3.0.1
  sys state:     COMBAT (0x03)   mode: ATRACK (0x04)
  HB_ms:         10 ms    dt_us: 520 us    fps: 60.00
  cam temp:      28 C      active cam: VIS (0)
  -- VIS cam0 --
  status_cam0:   0x1F  STARTED|ACTIVE|CAPTURING|TRACKING|TRACK_VALID
  status_trk0:   0x0C  TrackB_Enabled|TrackB_Valid
  -- MWIR cam1 --
  status_cam1:   0x03  STARTED|ACTIVE
  status_trk1:   0x00
  -- Tracker --
  tx/ty:         640 / 360 px
  atX0/atY0:     0 / 0 px    ftX0/ftY0: 0 / 0 px
  focusScore:    0.8214    nccScore: 0.9312
  -- Jetson --
  jetsonTemp:    52 C    cpuLoad: 34 %
  ntpEpochTime:  14:32:07 UTC
  -- trc.buffer raw (64 bytes) --
  [hex dump 16 per row with byte offset prefix]
```

Bit decode labels for cam status / track status as per ICD §8.5.

### 6.6 FMC command — full decode

Fields read from `fmc.buffer[]` using ICD FMC REG1 offsets:

```
  A1 alive:      YES    last 12 ms ago    connected: YES
  -- FMC REG1 decoded --
  sys state:     COMBAT (0x03)
  HB_ms:         20 ms    dt_us: 310 us
  stat bits:     0x03  isReady|isFSM_Powered
  stage pos:     15600 counts
  stage err:     0x00000000    stage status: 0x00000000
  FSM pos X rb:  -45 ADC cts    FSM pos Y rb: 32 ADC cts
  ntpEpochTime:  14:32:07 UTC
  FMC version:   3.0.1    MCU temp: 38.2 C
  -- BDC FSM commanded --
  FSM_X: -45   FSM_Y: 32   FSM_X0: 0   FSM_Y0: 0
  iFOV_X: 6.000e-05 deg/cnt    iFOV_Y: 5.700e-05 deg/cnt
  FSM_X_SIGN: -1    FSM_Y_SIGN: 1
  STAGE_POS: 15600    STAGE_HOME: 15600
  -- fmc.buffer raw (64 bytes) --
  [hex dump 16 per row with byte offset prefix]
```

### 6.7 STATUS command — stale bits cleaned up

Old decode of STATUS_BITS bits 1–7 (ntpUsingFallback, ntpHasFallback, usingPTP,
isUnsolicitedEnabled) was stale since session 32. Replaced with correct HEALTH_BITS
decode:

```
  HEALTH_BITS:   0x01
    isReady:          YES
    isSwitchEnabled:  YES    ← V2 only line
```

```
  POWER_BITS:    0x4F
    isPidEnabled:     YES
    isvPidEnabled:    YES
    isFTTrackEnabled: YES
    isVicorEnabled:   YES
    isRelay1En(MWIR): YES
    isRelay2En(FUJI): YES
    isRelay3En(TRC):  YES
    isRelay4En(spr):  no
```

---

## 7. MSG_BDC.cs Changes — Final

### 7.1 Rename + compat aliases

```csharp
// Renamed properties (parsed identically — byte positions unchanged):
public byte HealthBits { get; private set; } = 0;   // byte [10] — was StatusBits
public byte PowerBits  { get; private set; } = 0;   // byte [11] — was StatusBits2

// Backward-compat aliases — existing call sites unbroken:
public byte StatusBits  => HealthBits;
public byte StatusBits2 => PowerBits;
```

### 7.2 New properties

```csharp
// HEALTH_BITS accessors:
public bool isBDCReady       { get { return IsBitSet(HealthBits, 0); } }  // was StatusBits
public bool isSwitchEnabled  { get { return IsV2 && IsBitSet(HealthBits, 1); } }  // V2 only

// POWER_BITS accessors (names and bit positions unchanged — just source byte renamed):
public bool isPID_Enabled    { get { return IsBitSet(PowerBits, 0); } }
public bool isVPID_Enabled   { get { return IsBitSet(PowerBits, 1); } }
public bool isFT_Enabled     { get { return IsBitSet(PowerBits, 2); } }
public bool isVicor_Enabled  { get { return IsBitSet(PowerBits, 3); } }
public bool isRelay1_Enabled { get { return IsBitSet(PowerBits, 4); } }
public bool isRelay2_Enabled { get { return IsBitSet(PowerBits, 5); } }
public bool isRelay3_Enabled { get { return IsBitSet(PowerBits, 6); } }
public bool isRelay4_Enabled { get { return IsBitSet(PowerBits, 7); } }

// HW_REV:
public byte   HW_REV      { get; private set; } = 0;
public bool   IsV1        => HW_REV == 0x01;
public bool   IsV2        => HW_REV == 0x02;
public string HW_REV_Label => HW_REV == 0x01 ? "V1"
                            : HW_REV == 0x02 ? "V2 — Controller 1.0 Rev A"
                            : $"unknown (0x{HW_REV:X2})";

// New V2 temperatures (0 on V1 — backward-compatible):
public sbyte TEMP_RELAY { get; private set; } = 0;
public sbyte TEMP_BAT   { get; private set; } = 0;
public sbyte TEMP_USB   { get; private set; } = 0;
```

### 7.3 ParseMSG01 parse additions — after TimeBits line 529

```csharp
TimeBits   = msg[ndx]; ndx++;          // [391] TIME_BITS — unchanged
HW_REV     = msg[ndx]; ndx++;          // [392] HW_REV
TEMP_RELAY = (sbyte)msg[ndx]; ndx++;   // [393] relay area temp
TEMP_BAT   = (sbyte)msg[ndx]; ndx++;   // [394] battery-in temp
TEMP_USB   = (sbyte)msg[ndx]; ndx++;   // [395] USB 5V area temp
```

Parse of bytes [8–11] also renamed locally:

```csharp
DeviceEnabledBits = msg[ndx]; ndx++;
DeviceReadyBits   = msg[ndx]; ndx++;
HealthBits        = msg[ndx]; ndx++;   // was StatusBits
PowerBits         = msg[ndx]; ndx++;   // was StatusBits2
```

---

## 8. Complete File Change List

| File | Nature of change | Est. lines |
|---|---|---|
| `hw_rev.hpp` | Add BDC section (~20 lines) | +20 |
| `pin_defs_bdc.hpp` | Full replacement — V2 constants, drop internal gate | ~30 |
| `bdc.hpp` | Add `hw_rev.hpp` include; rename HEALTH/POWER_BITS fns; add members; update header | +25 |
| `BDC.ino` setup() | Vicor boot state → `POL_VICOR_OFF`; add IP175 init block | +7 |
| `BDC.ino` FW_VERSION | Bump 3.2.4 → 3.3.0 | 1 line |
| `bdc.cpp` EnableVicor() | `en ? POL_VICOR_ON : POL_VICOR_OFF` | 1 line |
| `bdc.cpp` PollTemp() | Add 3 new calcTemp() calls gated #if HW_REV_V2 | +5 |
| `bdc.cpp` buildReg01() | Pack HW_REV [392] + 3 temps [393–395]; rename HEALTH/POWER_BITS calls; isSwitchEnabled in HEALTH_BITS | +10 |
| `bdc.cpp` SERIAL_CMD HELP | 4-section box; V2-only lines hidden | +15 |
| `bdc.cpp` SERIAL_CMD STATUS | Rename STATUS_BITS→HEALTH_BITS, STATUS_BITS2→POWER_BITS; fix stale decode | +10 |
| `bdc.cpp` SERIAL_CMD TEMPS | Add V2 sensors #if HW_REV_V2 | +8 |
| `bdc.cpp` SERIAL_CMD POWER | New command block | +20 |
| `bdc.cpp` SERIAL_CMD HW | New command block | +12 |
| `bdc.cpp` SERIAL_CMD SWRESET | New V2 block | +8 |
| `bdc.cpp` SERIAL_CMD SWDISABLE | New V2 block | +8 |
| `bdc.cpp` SERIAL_CMD TRC | Full decode replacing hex-only dump | +55 |
| `bdc.cpp` SERIAL_CMD FMC | Full decode replacing hex-only dump | +40 |
| `bdc.cpp` SERIAL_CMD CAM | New command | +8 |
| `bdc.cpp` SERIAL_CMD VIEW | New command | +10 |
| `bdc.cpp` SERIAL_CMD TRACKER | New command | +8 |
| `bdc.cpp` SERIAL_CMD FSM | New command | +10 |
| `bdc.cpp` SERIAL_CMD STAGE | New command | +8 |
| `bdc.cpp` PRINT_REG() | Add HW_REV + temps; rename HEALTH/POWER_BITS | +10 |
| `bdc.hpp` isSwitchDisabled member | New bool | +1 |
| `MSG_BDC.cs` ParseMSG01 | Rename + 4 new parse lines | +8 |
| `MSG_BDC.cs` properties | HW_REV, IsV1/V2, label, 3 temps, HealthBits/PowerBits, isSwitchEnabled | +20 |
| `ARCHITECTURE.md` §10 | V1/V2 table, FW version, compat matrix | ~15 |
| **Total** | | **~340 net lines** |

---

## 9. Implementation Order

Changes issued as surgical before/after blocks in this sequence:

1. `hw_rev.hpp` — BDC section (foundation; everything else depends on this)
2. `pin_defs_bdc.hpp` — full replacement
3. `bdc.hpp` — include order, HEALTH/POWER_BITS rename, new members, header
4. `BDC.ino` — Vicor boot state, IP175 init, FW version
5. `bdc.cpp` — EnableVicor(), PollTemp(), buildReg01(), PRINT_REG()
6. `bdc.cpp` — SERIAL_CMD (HELP box, STATUS, TEMPS, POWER, HW, SWRESET, SWDISABLE)
7. `bdc.cpp` — SERIAL_CMD TRC (full decode + new commands CAM, VIEW, TRACKER)
8. `bdc.cpp` — SERIAL_CMD FMC (full decode + new commands FSM, STAGE)
9. `MSG_BDC.cs` — all changes in one pass
10. `ARCHITECTURE.md` — §10 update

---

*Confirm this document and implementation begins at step 1.*
