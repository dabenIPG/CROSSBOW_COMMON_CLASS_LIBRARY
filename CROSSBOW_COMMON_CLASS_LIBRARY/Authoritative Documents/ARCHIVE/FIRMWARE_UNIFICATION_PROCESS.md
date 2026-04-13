# Firmware Hardware Unification — Session Strategy & Process Guide

**Purpose:** Reusable prompt and process reference for unifying a forked embedded codebase
into a single `#ifdef`-guarded source that supports multiple hardware revisions.
**Derived from:** TMC V1/V2 unification session (2026-04-07).

---

## 1. Session Setup Prompt

Use this as the opening message to establish context:

---

*I have a fully working STM32F7 based Arduino compiled codebase called **[CONTROLLER]** that I
need to merge with a slightly behind version that has updates for new hardware pins and other
changes (we will call this one **[CONTROLLER]v2**). The files have the same name but the
**[CONTROLLER]** code is authoritative in the way it handles UDP comms, serial comms. The
thing that really separates the two is the pinDefs and some updates in logic based on hardware
changes.*

*I would like to walk through each file separately and make a markdown document of the real
differences in hardware but not the stale updates. I have attached the ICD and ARCHITECTURE
documents for reference — let's start there and then discuss a path forward. I will then start
loading the files for **[CONTROLLER]** and **[CONTROLLER]v2** (note these will have the same
filename so when I load them I will tell you which one is v2 and you should track accordingly).*

---

## 2. Reference Documents — Load First

Before any code files, upload:
- **ICD document** (`CROSSBOW_ICD_INT_ENG.md` or equivalent)
- **ARCHITECTURE.md**

Ask the AI to read and acknowledge both before proceeding. This gives it context on:
- Register layouts and byte offsets
- Port assignments and protocol details
- FW version history and what's already documented

---

## 3. File Loading Order and Protocol

Load files **one controller at a time**, alternating baseline then v2:

```
1. Baseline [file].hpp      → "here is the base [CONTROLLER] [file]"
2. V2 [file].hpp            → "here is [CONTROLLER]v2 [file]"
   (AI diffs and reports)
3. Baseline [file].cpp      → "here is the base [CONTROLLER] [file]"
4. V2 [file].cpp            → "here is [CONTROLLER]v2 [file]"
   (AI diffs and reports)
... etc
```

**Recommended file order:**
1. `pin_defs_[controller].hpp` — start here, this is the primary hardware driver
2. `defines.hpp` — shared fleet-wide enums, minimal changes expected
3. `[CONTROLLER].ino` — setup/loop, init polarity, serial buffer
4. `[controller].hpp` — class declaration, status bits, private members
5. `[controller].cpp` — the bulk of the work

**When loading v2 files:** State explicitly which is v2. The AI will prefix mentally and track it. Example:
> *"here is TMCv2 tmc.hpp"*
> *"here is the TMCv2 tmc.cpp"*

---

## 4. Diff Analysis Framework

For each file pair the AI should produce:

| Category | Symbol | Meaning |
|----------|--------|---------|
| Hardware change | 🔴 | Pin numbers, logic inversion, removed peripherals — carry forward with `#ifdef` |
| New in V2 only | 🟢 | Hardware additions (new PSU, new pin) — carry forward V2-only guarded |
| Stale V2 regression | 🔵 | Comms/protocol change that V2 got wrong — discard, keep V1 |
| Unchanged | ✅ | Identical both versions — no action |
| Needs clarification | ⚠️ | Ambiguous — ask before deciding |

**Key rule to establish early:**
> *V1 (base [CONTROLLER]) is authoritative for all comms, protocol, UDP/serial logic.
> V2 changes are carried forward ONLY if they are hardware-driven.*

---

## 5. Hardware Change Categories to Watch For

Based on TMC experience, the common categories are:

| Category | What to look for |
|----------|-----------------|
| **Pin reassignment** | Same signal, different pin number |
| **Pin removal** | Feature/circuit removed (e.g. RTC, heater) |
| **Pin addition** | New hardware (e.g. second pump PSU) |
| **External chip removal** | ADC, DAC, I2C device gone — signals move to direct MCU pins |
| **Opto type change** | NO → NC inverts all drive polarity |
| **Power supply change** | DAC-speed-controlled → on/off-only PSU |
| **Enum splits** | Single → dual (e.g. PUMP → PUMP1 + PUMP2) |
| **DAC channels retired** | When hardware no longer accepts analog trim |
| **Register layout impact** | Any of the above may change STATUS_BITS wire format |

---

## 6. Questions to Ask the Hardware Owner Early

Establish these before writing any code:

1. **Opto type:** Are the optos on the new hardware Normally Open (NO) or Normally Closed (NC)?
   Which outputs does this apply to? (Not all outputs may change.)

2. **Retired peripherals:** Which chips/circuits are definitively removed? Are any temps or
   signals retired entirely vs migrated to direct MCU pins?

3. **Power supply change:** Is the new PSU on/off only, or does it accept a trim input?
   Does this affect speed control enums?

4. **Dual outputs:** If a single output becomes two (e.g. one pump Vicor → two PSUs),
   do they always move together in normal operation, or are they independently controlled?
   This determines whether you need `* <0|1>` broadcast syntax.

5. **Register continuity:** Are there any STATUS_BITS fields that need to survive for
   register layout stability even when the hardware is gone (e.g. heater bit kept as
   always-false stub)?

6. **Stale changes:** Are there any V2 logic changes that were intentional improvements
   (not just hardware adaptation)? These need separate discussion before discarding.

---

## 7. Checkpoint — Write the Delta Document First

**Before writing any unified code**, produce `[CONTROLLER]_HW_DELTA.md` covering:

- Hardware context (what the controller manages, V1 vs V2 summary table)
- Opto logic inversion (with circuit explanation)
- Pin definitions delta (removed / changed / added / retired)
- Defines delta (enums affected)
- `.ino` delta (init polarity, hardware blocks)
- `.hpp` delta (members, STATUS_BITS layout, ICD wire format table)
- `.cpp` delta (all hardware-touching functions)
- Stale V2 changes catalogue (do not carry forward)
- Unified codebase strategy (`#ifdef` guard map)
- Open questions

**Get owner review of the delta doc before writing unified code.**
This is the most important step — it catches misunderstandings before they become bugs.

---

## 8. Unified Codebase Implementation Order

1. **`hw_rev.hpp`** (new file) — revision guard, polarity macros, loop config, `HW_REV_BYTE`
2. **`pin_defs_[controller].hpp`** — all pin guards
3. **`defines.hpp`** — enum guards (fleet-wide file — use `!defined(HW_REV_V2)` pattern
   to preserve defaults for non-controller builds)
4. **`[controller].hpp`** — class members, STATUS_BITS functions
5. **`[controller].cpp`** — hardware functions, StateManager, pollTemps, serial commands
6. **`[CONTROLLER].ino`** — init block, serial buffer (always keep V1 static char pattern)

---

## 9. `hw_rev.hpp` Standard Template

```cpp
#pragma once

// ── Hardware revision ──────────────────────────────────────────────────────
// Uncomment exactly ONE, or set via build flags: -DHW_REV_V1 / -DHW_REV_V2
// #define HW_REV_V1   // Original hardware
// #define HW_REV_V2   // Rev A hardware

#if !defined(HW_REV_V1) && !defined(HW_REV_V2)
  #error "hw_rev.hpp: No hardware revision defined."
#endif
#if defined(HW_REV_V1) && defined(HW_REV_V2)
  #error "hw_rev.hpp: Both revisions defined — set exactly one."
#endif

// ── Output polarity (if opto type changes) ────────────────────────────────
// Only needed if hardware uses opto-isolated control lines that changed type.
// Document WHICH outputs this applies to and which it does NOT.
#if defined(HW_REV_V1)
  #define CTRL_OFF  HIGH   // NO opto — HIGH asserts inhibit
  #define CTRL_ON   LOW
#elif defined(HW_REV_V2)
  #define CTRL_OFF  LOW    // NC opto — LOW holds contact closed
  #define CTRL_ON   HIGH
#endif

// ── Optional topology/config flags ────────────────────────────────────────
// Add controller-specific compile-time configuration flags here.
// Example from TMC: #define SINGLE_LOOP

// ── REG1 self-detecting revision byte ─────────────────────────────────────
// Written to a reserved REG1 byte so C# clients can self-detect layout.
#if defined(HW_REV_V1)
  #define HW_REV_BYTE  0x01
#elif defined(HW_REV_V2)
  #define HW_REV_BYTE  0x02
#endif
```

---

## 10. Register Layout Rules

- **Always allocate a REG1 byte for `HW_REV`** — use the first previously-RESERVED byte.
  This allows `MSG_[CONTROLLER].cs` to self-detect without out-of-band configuration.
- **STATUS_BITS wire format changes must be documented in the ICD** with a per-revision table.
- **When a hardware feature is removed**, keep its STATUS_BITS position as a stub
  (always 0) to preserve byte layout for existing C# clients.
- **New STATUS_BITS** (e.g. second pump, loop topology) go into previously-RES bit positions.
- **Document byte offsets, not just bit names** — C# `MemoryMarshal.Read<struct>` depends on exact layout.

---

## 11. C# Client Update Checklist

After firmware is written and tested, update in this order:

1. **`MSG_[CONTROLLER].cs`**
   - Add `HW_REV` property from new byte
   - Add `IsV1` / `IsV2` / `HW_REV_Label` helpers
   - Update STATUS_BITS accessors (new bits, renamed bits)
   - Add `PumpSpeedValid`, `Tv3Tv4Valid` or equivalent validity flags
   - Update struct layout (`TmcReg1` equivalent) if byte positions changed

2. **`[controller].cs`** (command sender)
   - Guard commands that are invalid on one revision (e.g. HEAT on V2)
   - Guard DAC channels that no longer exist on new hardware
   - Add convenience methods for new capabilities (e.g. `EnableBothPumps()`)

3. **`defines.cs`**
   - Add new enum values matching firmware `defines.hpp`
   - Use duplicate values for aliases (e.g. `PUMP1 = 2` alongside `PUMP = 2`)

4. **`frm[CONTROLLER].cs`** + Designer
   - Add new designer controls for new hardware (e.g. Pump2 checkbox)
   - Add `ApplyHwRevLayout(bool defaultV1 = false)` method
   - Call with `defaultV1: true` in constructor (safe default before first packet)
   - Call without args in `timer1_Tick` on first non-zero `HW_REV`
   - Reset on disconnect: `_hwRevApplied = false; ApplyHwRevLayout(defaultV1: true)`
   - Update status strip to show `HW_REV_Label` + any topology flags

---

## 12. Document Update Checklist

After code and GUI are tested, update these documents:

| Document | What to update |
|----------|---------------|
| `CROSSBOW_ICD_INT_ENG.md` | Version history entry; REG1 table (new/changed bytes); command payload notes |
| `CROSSBOW_ICD_INT_OPS.md` | Version history; embedded block note (if controller feeds pass-through to INT_OPS visible register); FW version table |
| `ARCHITECTURE.md` | Version history; controller section (role, hardware table, build config); FW version table; compatibility matrix |
| `CROSSBOW_UG_ENG_GUI_draft.md` | ICD/ARCH refs; controller section (description, temp table, control table); action items; revision history |
| `CROSSBOW_DOCUMENT_REGISTER.md` | Version bumps for all updated docs |
| `[CONTROLLER]_HW_DELTA.md` | Close resolved items; add new features section; final file manifest |
| `[CONTROLLER]_TEST_AND_GUI.md` | Close all tested items; add open items; serial command reference |

---

## 13. Testing Protocol

### Serial console — run in order:
1. `INFO` — confirm FW version, HW_REV byte, link UP, A1 enabled
2. `REG` — confirm HW_REV byte appears; check all bytes for plausible values
3. `STATUS` — confirm boot state, no errors
4. `TEMPS` — confirm V1-only sensors show `RETIRED` on V2
5. `VICOR` — confirm V1/V2 channel names match hardware
6. `STATE 2` → `STATUS` → `STATE 1` — full state transition cycle
7. Hardware-specific commands (pump control, DAC, etc.)

### GUI:
1. Connect → confirm HW_REV shows in status strip
2. Confirm V2-hidden controls are not visible
3. Confirm V2-only controls are visible on V2
4. State transition via GUI → confirm readbacks update
5. Disconnect → confirm layout resets to V1 defaults
6. Reconnect → confirm layout re-applies from new HW_REV

---

## 14. PMC-Specific Notes (Placeholder)

*(Fill in before starting the PMC session)*

- PMC hardware differences from V1 to V2: `___`
- Files expected: `___`
- Known opto/polarity changes: `___`
- Known removed peripherals: `___`
- Known new hardware: `___`
- ICD documents affected: `___`

---

*End of process guide. Replace `[CONTROLLER]` with target controller name throughout.*
