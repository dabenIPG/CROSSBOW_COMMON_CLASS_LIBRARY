# CROSSBOW — Fire Control Registers
**Document:** CROSSBOW_FIRECONTROL_REGISTERS.md
**Status:** Working draft — to be merged into ICD (INT_OPS and INT_ENG)
**Session:** Fire control status cleanup — MCC/BDC vote byte review
**Relates to:** CROSSBOW_ICD_INT_OPS, CROSSBOW_ICD_INT_ENG, FC-CONSISTENCY-1

---

## Overview

Fire control status is communicated via four vote bytes carried in the `0xE0 SET_BCAST_FIRECONTROL_STATUS` broadcast. Two bytes originate at MCC, two at BDC. Together they give any downstream consumer (BDC, TRC, THEIA replacement) complete fire control state without requiring any interpretation of raw hardware signals.

The bytes form two tiers:

| Tier | MCC byte | BDC byte | Purpose |
|------|----------|----------|---------|
| Upper — summary | `VOTE_BITS_MCC` | `VOTE_BITS_BDC` | Go/no-go state. OSD reticle, status strip, operator display. |
| Detail — diagnostic | `VOTE_BITS_MCC2` | `VOTE_BITS_BDC2` | Individual inputs and override flags. Explains *why* a vote is blocked. Required for THEIA replacement without recomputing logic. |

BDC votes are invariant across all MCC hardware revisions and laser models.

---

## VOTE_BITS_MCC Design Philosophy

`VOTE_BITS_MCC` encodes the **physical gate chain in bit order**. Each bit represents one stage in the hardware AND chain from operator inputs through to laser output. Bits are ordered b0→b7 to follow the signal flow: upstream hardware inputs first, computed intermediate gates next, final gate and laser confirmation last.

**All bits = 1 means the system is firing and the laser is emitting.** Any bit clear while the trigger is held identifies the exact blocking condition — an interlock (NotAbort, Armed, BDCVote), a state or voltage condition (SW_Vote = Combat AND BatNotLow), loss of trigger, gate failure, or no energy output.

`EMON` (b7) is the sole confirmation that the laser is actively emitting energy. It is the output of a successful fire chain — not a precondition — and is intentionally excluded from all composite vote masks. TRC uses this bit alone to drive the red reticle and "FIRE" message.

```
D2 NotAbort ──┐
D3 Armed ─────┤ AND → D7 LaserTotalHW ──┐
D4 BDCVote ───┘          (b3)            │
                                          ├─ AND → D80 ──┐
D9 SW_Vote ──────────────────────────────┘               │
   (Combat && BatNotLow)  (b4)                            ├─ AND → D45 FireState (b6)
                                                          │
TRIGGER_HB ──────────────────────────────────────────────┘
   (isLaserFireRequested_Vote) (b5)

D45 FireState → IPG → EMON (b7)
```

---

## 0xE0 Packet Chain

Command: `SET_BCAST_FIRECONTROL_STATUS`
Rate: 200 Hz (TICK_VoteStatus = 5 ms) — sent every tick unconditionally, regardless of state change.
Bus: A1 (INT_ENG only — not visible to A3 clients).

```
MCC → BDC:   [0xE0][VOTE_BITS_MCC][VOTE_BITS_MCC2]                                              3 bytes
BDC → TRC:   [0xE0][VOTE_BITS_MCC][VOTE_BITS_BDC][sysState][bdcMode][VOTE_BITS_MCC2][VOTE_BITS_BDC2]   7 bytes
```

MCC sends only its two bytes. BDC inserts `VOTE_BITS_BDC` and `VOTE_BITS_BDC2` at positions 2 and 6 respectively, and passes MCC bytes through verbatim.

### TRC REG1 landing positions

| Byte | Field |
|------|-------|
| [41] | `voteBitsMcc` — VOTE_BITS_MCC |
| [42] | `voteBitsBdc` — VOTE_BITS_BDC |
| [57] | `voteBitsMcc2` — VOTE_BITS_MCC2 (first of reserved bytes [57–63]) |
| [58] | `voteBitsBdc2` — VOTE_BITS_BDC2 (second of reserved bytes [57–63]) |

---

## Hardware Version Reference

| Version | Bus | Laser | Charger | Notes |
|---------|-----|-------|---------|-------|
| V1 | 48V | YLM-3K (3 kW) | I2C DBU3200 | Relay bus, solenoids SOL_HEL / SOL_BDA |
| V2 | 300V | YLM-6K (6 kW) | GPIO PIN_CRG_OK | Dual Vicor, no solenoids, no I2C charger |
| V3-3kW | 48V | YLM-3K (3 kW) | I2C DBU3200 | Solenoids, adds RELAY_NTP |
| V3-6kW | 300V | YLM-6K (6 kW) | GPIO PIN_CRG_OK | Vicor GIM/TMS, PIN_ENERGIZE (pin 63) |

MCC REG1 byte [254] = `MCC_HW_REV_BYTE` (0x01=V1, 0x02=V2, 0x03=V3).
MCC REG1 byte [255] = `LASER_MODEL_BITS` (0x01=YLM-3K, 0x02=YLM-6K) — sensed via RMN on connect.

### Battery low threshold — version dependent

`isNotBatLowVoltage()` must branch on hardware revision. The threshold is bus-specific:

| Bus | Versions | Threshold | Constant |
|-----|----------|-----------|----------|
| 48V | V1, V3-3kW | 47.0 V | `BAT_LOW_THRESHOLD_48V` |
| 300V | V2, V3-6kW | 266.0 V | `BAT_LOW_THRESHOLD_300V` |

---

## VOTE_BITS_MCC — Upper Summary Byte

Byte 1 of `0xE0`. Packed by MCC. Bits ordered b0→b7 to follow the physical gate chain. All bits = 1 while trigger is held means the laser is firing. Any bit clear identifies the blocking condition.

| Bit | Mask | Name | HW point | Source | Notes |
|-----|------|------|----------|--------|-------|
| b0 | `0x01` | `NotAbort` ⊘ | D2 | `digitalRead(PIN_SAFETY_ISNOTABORT)` | ABORT plunger not pressed — vote is active. **Inverted sense**: bit CLEAR = plunger pressed = abort ACTIVE, vote lost. Bit SET = plunger out = normal operating state. |
| b1 | `0x02` | `Armed` | D3 | `digitalRead(PIN_SAFETY_ISARMED)` | Arm key/switch HW readback. |
| b2 | `0x04` | `BDCVote` | D4 | `digitalRead(PIN_SAFETY_ISBDAVOTE)` | BDC hardwire aggregate vote. Single wire from BDC representing full BDC go/no-go. Distinct from BDC software geometry votes in VOTE_BITS_BDC. |
| b3 | `0x08` | `LaserTotalHW` | D7 | HW AND rb | Hardware AND gate readback of D2 && D3 && D4. Not redundant with b0–b2: confirms the **external hardware gate is correctly processing** the NotAbort, Armed, and BDCVote inputs. A software consumer could compute b0 AND b1 AND b2 and reach the same logical result, but b3 verifies the physical gate circuit is functioning. |
| b4 | `0x10` | `SW_Vote` | D9 | `PIN_SAFETY_SWVOTE` | Software vote driven to hardware AND gate every tick: `Combat && BatNotLow`. Individual inputs (Combat, BatNotLow) are in VOTE_BITS_MCC2 for diagnostics. |
| b5 | `0x20` | `Trigger` | TRIGGER_HB | `isLaserFireRequested_Vote` → `PIN_SAFETY_FIREVOTE` | Set by `0xAB SET_FIRE_REQUESTED_VOTE` heartbeat. Operator must hold both Xbox controller triggers simultaneously and send continuously. Injected to hardware AND gate. Drops on disconnect. |
| b6 | `0x40` | `FireState` | D45 | `digitalRead(PIN_FIRE_RB)` | Final HW AND gate output readback. All conditions met, laser commanded to fire. Gate can assert without laser emitting — see EMON b7. |
| b7 | `0x80` | `EMON` | IPG | `ipg.isEMON()` | **Laser energy output confirmed via IPG RMN serial. The only reliable indication that the laser is firing.** Consistent across YLM-3K and YLM-6K. Not a vote input — see composite note below. |

### Composite masks

```cpp
// All composites exclude EMON (b7).
// EMON is firing confirmation, not a vote input. Checked independently:
//   (voteBitsMcc & EMON) != 0
// Any bit in FULL_FIRE_CHAIN clear while Trigger is held identifies the blocking condition.

ARMED_NOMINAL   = NOT_ABORT | ARMED                                  // 0x03 — key/switch state
READY_TO_FIRE   = NOT_ABORT | ARMED | BDC_VOTE | LASER_TOTAL_HW
                  | SW_VOTE                                           // 0x1F — all inputs valid, awaiting trigger
FULL_FIRE_CHAIN = NOT_ABORT | ARMED | BDC_VOTE | LASER_TOTAL_HW
                  | SW_VOTE | TRIGGER | FIRE_STATE                   // 0x7F — all votes asserted
```

### SW_Vote — hardware drive requirement

`PIN_SAFETY_SWVOTE` (D9) must be driven **every tick** in the main loop alongside `VOTE_BITS()` packing — not only on state transitions:

```cpp
// main loop — every tick
digitalWrite(PIN_SAFETY_SWVOTE,
    (isNotBatLowVoltage() && isCombat_Vote_rb()) ? HIGH : LOW);
```

This matches the always-driven pattern of `PIN_SAFETY_FIREVOTE` and ensures the gate tracks live voltage and system state without risk of a missed transition.

---

## VOTE_BITS_MCC2 — Detail Byte

Byte 3 of `0xE0`. Packed by MCC. Passed through by BDC verbatim. Carries the individual inputs composing `SW_Vote` (MCC b4), plus status and diagnostic bits. Bits 0–2 are unblocked. Bits 3–5 pending FC-CONSISTENCY-1.

`SW_Vote` in VOTE_BITS_MCC b4 = `Combat (MCC2 b2)` AND `BatNotLow (MCC2 b0)`. A developer replacing THEIA can diagnose any SW_Vote failure from these two bits without additional register queries.

| Bit | Mask | Name | Status | Source | Notes |
|-----|------|------|--------|--------|-------|
| b7 | `0x80` | RES | — | — | Reserved. |
| b6 | `0x40` | RES | — | — | Reserved. |
| b5 | `0x20` | `FireInterlocked` | ⏳ pending | FC-CONSISTENCY-1 | Trigger held but fire blocked — gate inputs valid, no FireState output. |
| b4 | `0x10` | `EMON_Unexpected` | ⏳ pending | FC-CONSISTENCY-1 | EMON active without fire expected — safety escape. |
| b3 | `0x08` | `EMON_Missing` | ⏳ pending | FC-CONSISTENCY-1 | FireState asserted, no EMON within timeout. |
| b2 | `0x04` | `Combat` | ✅ ready | `isCombat_Vote_rb()` | System_State == COMBAT. One of the two inputs to SW_Vote. |
| b1 | `0x02` | `TrainingMode` | ✅ ready | `isTrainingMode` re-packed from HEALTH_BITS b3 | Laser power clamped to 10%. Status only — not a vote gate. Closes `TRC-TRAIN-WARN-1`. |
| b0 | `0x01` | `BatNotLow` | ✅ ready | `isNotBatLowVoltage()` | Voltage above threshold. Other input to SW_Vote. Threshold is version-conditional — see Hardware Version Reference. |

`LaserModelMatch` remains in `HEALTH_BITS` byte [9] bit 4. A mismatch should not occur in the field; surface via the HEL ready/warn health message. Available to THEIA via MCC REG1.

---

## VOTE_BITS_BDC — Upper Summary Byte

Byte 2 of `0xE0`. Packed by BDC (`VOTE_BITS2()`). MCC sends zero at this position; BDC replaces it when forwarding to TRC. Invariant across all MCC versions and laser models.

| Bit | Mask | Name | Notes |
|-----|------|------|-------|
| b7 | `0x80` | `FSMNotLimited` | FSM clear. **Bit SET = not limited.** ICD previously listed as `isFSMLimited` — name was inverted; corrected here. |
| b6 | `0x40` | RES | Reserved. |
| b5 | `0x20` | `HorizLoaded` | Horizon elevation array loaded and valid. |
| b4 | `0x10` | RES | Reserved. |
| b3 | `0x08` | `BDCTotalVote` | BDC fire gate — see formula below. |
| b2 | `0x04` | `InLCHVote` | Within laser clear heading — override or raw. Required above horizon only. |
| b1 | `0x02` | `InKIZVote` | Within kill inhibit zone — override or raw. |
| b0 | `0x01` | `BelowHorizVote` | Line of sight below horizon — override or raw. |

### BDCTotalVote formula

```cpp
BDCTotalVote = bdcVoteOverride
    || (FSMNotLimited
        && (BelowHorizVote ? InKIZVote
                           : InKIZVote && InLCHVote));
```

ICD previously stated `BDCVote = override OR (BelowHoriz AND InKIZ AND InLCH)`. Two errors corrected: FSM gate was undocumented; LCH is only required above horizon.

Overrides are engineering mode only — gated by `AssertIntEng()` in `bdc.cs SetOverrideVote()`.

---

## VOTE_BITS_BDC2 — Detail Byte

Byte 6 of BDC→TRC packet. Raw geometry inputs and override flags underlying VOTE_BITS_BDC results. Invariant across all MCC versions and laser models.

| Bit | Mask | Name | Notes |
|-----|------|------|-------|
| b7 | `0x80` | RES | Reserved. |
| b6 | `0x40` | `isInLCH` | Raw LCH sensor input before override. |
| b5 | `0x20` | `isInKIZ` | Raw KIZ sensor input before override. |
| b4 | `0x10` | `isBelowHoriz` | Raw horizon sensor input before override. |
| b3 | `0x08` | `isBDCVoteOverride` | Eng mode only — bypasses BDCTotalVote entirely. |
| b2 | `0x04` | `isLCHVoteOverride` | Eng mode only. |
| b1 | `0x02` | `isKIZVoteOverride` | Eng mode only. |
| b0 | `0x01` | `isHorizVoteOverride` | Eng mode only. |

KIZ and LCH validity detail lives in BDC REG1 bytes [167–168]. Not needed in broadcast — BDCTotalVote and the raw inputs in BDC2 are sufficient for operator display and THEIA replacement.

---

## OSD Fire State — Reticle Colour and Interlock Message

TRC `drawFireState()` reads `voteBitsMcc` [41], `voteBitsBdc` [42], and `voteBitsMcc2` [57]. Priority chain evaluated every frame:

```
Trigger held (MCC b5)?
  YES:
    EMON (MCC b7)?                       → RED    "FIRE"
    NotAbort clear (MCC b0 = 0)?         → WHITE  "INTERLOCK - ABORT"
    !Armed (MCC b1)?                     → WHITE  "INTERLOCK - NOT ARMED"
    !SW_Vote (MCC b4)?
      !Combat (MCC2 b2)?                 → WHITE  "INTERLOCK - NOT COMBAT"
      !BatNotLow (MCC2 b0)?             → WHITE  "INTERLOCK - BAT LOW"
    !BDCTotalVote (BDC b3)?
      !FSMNotLimited (BDC b7)?          → WHITE  "INTERLOCK - FSM LIMIT"
      BelowHoriz (BDC b0)?
        !InKIZ (BDC b1)?               → WHITE  "INTERLOCK - KIZ"
        else                           → (transitioning, silent)
      else (above horiz):
        !InLCH (BDC b2)?              → WHITE  "INTERLOCK - LCH"
        !InKIZ (BDC b1)?              → WHITE  "INTERLOCK - KIZ"
        else                          → (transitioning, silent)
    All votes clear, FireState (MCC b6) && !EMON:
                                         → WHITE  "FC ERROR"
  NO trigger:
    NotAbort clear (MCC b0 = 0)?         → YELLOW "ABORT"
    Armed (MCC b1)?                      → ORANGE "ARMED"
    else                                → GREEN  (idle)
```

`FCVOTES <mcc_hex> <bdc_hex>` on port 5012 injects both bytes directly for OSD checkout without live hardware.

---

## HMI Status Indicators — Naming Corrections

| Current | Corrected | Signal | Reason |
|---------|-----------|--------|--------|
| `tssStatus2_isFiring` | `tssStatus2_isFireGate` | `FireState` MCC b6 | D45 = gate output = "laser commanded", not confirmed firing. |
| `tssStatus2_isBDA*` | `tssStatus2_isBDC*` | — | BDA → BDC rename throughout. |
| (new) `tssStatus2_isEMON` | `tssStatus2_isEMON` | `EMON` MCC b7 | Add dedicated indicator. Separates commanded-fire (b6) from confirmed-fire (b7). |
| `MSG_MCC.cs isBDA_Vote_rb` | `isBDC_Vote_rb` | MCC b2 | BDA → BDC rename. |
| `MSG_BDC.cs isBDA_Vote_rb` | `isBDC_Vote_rb` | VoteBits3 b3 | BDA → BDC rename. |

---

## Defines Changes Required

### defines.hpp

```cpp
// VOTE_BITS_MCC — full replacement (new gate-chain bit ordering)
enum class VOTE_BITS_MCC : uint8_t {
    NOT_ABORT      = 0x01,  // b0 — D2 HW rb, inverted: CLEAR = abort ACTIVE
    ARMED          = 0x02,  // b1 — D3 HW rb
    BDC_VOTE       = 0x04,  // b2 — D4 hardwire from BDC  (renamed from BDA_VOTE)
    LASER_TOTAL_HW = 0x08,  // b3 — D7 AND rb: NotAbort && Armed && BDCVote
    SW_VOTE        = 0x10,  // b4 — D9 output: Combat && BatNotLow
    TRIGGER        = 0x20,  // b5 — TRIGGER_HB → PIN_SAFETY_FIREVOTE
    FIRE_STATE     = 0x40,  // b6 — D45 final gate output rb
    EMON           = 0x80,  // b7 — IPG energy out confirmed
                            //       NOT a vote input. Sole firing indicator.
                            //       TRC red reticle / "FIRE". Excluded from all composites.
    // Composites — EMON always excluded.
    ARMED_NOMINAL   = 0x03,  // NotAbort | Armed
    READY_TO_FIRE   = 0x1F,  // NotAbort | Armed | BDCVote | LaserTotalHW | SW_Vote
    FULL_FIRE_CHAIN = 0x7F,  // READY_TO_FIRE | Trigger | FireState
};

// VOTE_BITS_MCC2 — new
enum class VOTE_BITS_MCC2 : uint8_t {
    BAT_NOT_LOW      = 0x01,  // b0 — input to SW_Vote  (unblocked)
    TRAINING_MODE    = 0x02,  // b1 — status, not a gate  (unblocked)
    COMBAT           = 0x04,  // b2 — input to SW_Vote  (unblocked)
    EMON_MISSING     = 0x08,  // b3 — pending FC-CONSISTENCY-1
    EMON_UNEXPECTED  = 0x10,  // b4 — pending FC-CONSISTENCY-1
    FIRE_INTERLOCKED = 0x20,  // b5 — pending FC-CONSISTENCY-1
};

// VOTE_BITS_BDC — rename BDCTotalVote (from BDA_VOTE2), FSMNotLimited name corrected
enum class VOTE_BITS_BDC : uint8_t {
    BELOW_HORIZ_VOTE = 0x01,
    IN_KIZ_VOTE      = 0x02,
    IN_LCH_VOTE      = 0x04,
    BDC_TOTAL_VOTE   = 0x08,  // renamed from BDA_VOTE2
    HORIZ_LOADED     = 0x20,
    FSM_NOT_LIMITED  = 0x80,  // bit SET = FSM clear (ICD name isFSMLimited was inverted)
};

// VOTE_BITS_BDC2 — new
enum class VOTE_BITS_BDC2 : uint8_t {
    HORIZ_VOTE_OVERRIDE = 0x01,
    KIZ_VOTE_OVERRIDE   = 0x02,
    LCH_VOTE_OVERRIDE   = 0x04,
    BDC_VOTE_OVERRIDE   = 0x08,
    IS_BELOW_HORIZ      = 0x10,  // raw, before override
    IS_IN_KIZ           = 0x20,  // raw, before override
    IS_IN_LCH           = 0x40,  // raw, before override
};

// Battery thresholds
static constexpr float BAT_LOW_THRESHOLD_48V  = 47.0f;   // V1, V3-3kW
static constexpr float BAT_LOW_THRESHOLD_300V = 266.0f;  // V2, V3-6kW
```

### mcc.hpp — updated packing functions

```cpp
bool isSW_Vote() {
    return isCombat_Vote_rb() && isNotBatLowVoltage();
}

uint8_t VOTE_BITS() {
    uint8_t mask = 0;
    mask |= (isNotAbort_Vote_rb        ? 0x01 : 0x00);  // b0 — D2
    mask |= (isArmed_Vote_rb           ? 0x02 : 0x00);  // b1 — D3
    mask |= (isBDC_Vote_rb             ? 0x04 : 0x00);  // b2 — D4
    mask |= (isLaserTotalHW_Vote_rb    ? 0x08 : 0x00);  // b3 — D7
    mask |= (isSW_Vote()               ? 0x10 : 0x00);  // b4 — D9
    mask |= (isLaserFireRequested_Vote ? 0x20 : 0x00);  // b5 — TRIGGER_HB
    mask |= (isLaserTotal_Vote_rb      ? 0x40 : 0x00);  // b6 — D45 FireState
    mask |= (ipg.isEMON()              ? 0x80 : 0x00);  // b7 — EMON
    return mask;
}

uint8_t VOTE_BITS2() {
    uint8_t mask = 0;
    mask |= (isNotBatLowVoltage() ? 0x01 : 0x00);  // b0
    mask |= (isTrainingMode       ? 0x02 : 0x00);  // b1
    mask |= (isCombat_Vote_rb()   ? 0x04 : 0x00);  // b2
    // b3–b5: FC-CONSISTENCY-1 pending
    return mask;
}
```

---

## ICD Corrections Required

| Location | Current | Correction |
|----------|---------|------------|
| VOTE_BITS_MCC bit ordering | b0=LaserTotalHW … b7=Combat | b0=NotAbort, b1=Armed, b2=BDCVote, b3=LaserTotalHW, b4=SW_Vote, b5=Trigger, b6=FireState, b7=EMON |
| VOTE_BITS_MCC | b0 and b4 (EMON) unnamed/missing | Add both with full definitions |
| VOTE_BITS_MCC | b7=`isCombat_Vote_rb` | Removed — moved to MCC2 b2 as input to SW_Vote |
| VOTE_BITS_MCC2 | Does not exist | Add new byte |
| VOTE_BITS_BDC2 | Does not exist | Add new byte |
| BDC VOTE BITS2 [165] b7 | `isFSMLimited` | `isFSMNotLimited` — bit SET = FSM clear |
| BDC VOTE BITS2 [165] BDCTotalVote | `override OR (BelowHoriz AND InKIZ AND InLCH)` | `override OR (FSMNotLimited AND (BelowHoriz ? InKIZ : InKIZ AND InLCH))` |
| `0xAB` command name | `SET_FIRE_VOTE` | `SET_FIRE_REQUESTED_VOTE` |
| 0xE0 send condition | "on state change" | "every TICK_VoteStatus (5 ms) unconditionally" |
| 0xE0 packet length | 2B (MCC→BDC) / 5B (BDC→TRC) | 3B (MCC→BDC) / 7B (BDC→TRC) |
| All `isBDA_Vote_rb` | `isBDA_Vote_rb` | `isBDC_Vote_rb` |
| TRC REG1 [41] source | sourced from `0xAB` | sourced from `0xE0` (per IPGD-0003) |

---

## Open Items

| ID | Item | Status |
|----|------|--------|
| FC-CONSISTENCY-1 | Define `FireExpected` and implement MCC2 bits 3–5 | Pending |
| TRC-TRAIN-WARN-1 | Training mode OSD indicator | Closed by MCC2 b1 once implemented |
| — | RELAY_LASER / SOL_HEL visibility — vote bytes vs DEVICE_ENABLED_BITS | Discuss next |
| — | `PIN_SAFETY_SWVOTE` always-driven in main loop | Implement with MCC2 |
| — | `isNotBatLowVoltage()` version-conditional threshold (47 V / 266 V) | Implement with MCC2 |
| — | `isSW_Vote()` helper in mcc.hpp | Implement with VOTE_BITS() repack |
| — | defines.cs — mirror all enum changes from defines.hpp | Implement with above |
