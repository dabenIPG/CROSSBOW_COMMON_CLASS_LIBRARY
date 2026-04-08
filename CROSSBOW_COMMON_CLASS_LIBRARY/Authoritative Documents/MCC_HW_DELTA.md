# MCC Hardware Delta — V1 vs V2

**Controller:** MCC (Master Control Controller)
**IP:** 192.168.1.10
**FW (unified):** v3.3.0 (`VERSION_PACK(3,3,0)`)
**ICD Reference:** CROSSBOW_ICD_INT_ENG v3.4.0
**ARCH Reference:** ARCHITECTURE.md v3.3.4
**Session:** MCC unification — 2026-04-08
**Status:** ✅ Session complete — FW files produced, docs updated, compile testing underway

---

## 1. Hardware Context

### Role
Master Control Controller — manages all power and energy subsystems: battery (BAT),
laser power supply (HEL/IPG), charger (CRG/DBU), GNSS (NovAtel), TMC supervision,
PTP/NTP time, and fire control vote aggregation.

### Power Architecture — V1 vs V2

**V1 Power Architecture:**
- `PIN_VICOR_EN` (A0) enables a single large Vicor DC-DC that powers the **relay bus**
- Relay 1 (pin 83) = GPS power (NO opto)
- Relay 2 (pin 20) = Laser (HEL) power (NO opto)
- `SOL1` (pin 5) = laser HV bus solenoid (electromechanical relay)
- `SOL2` (pin 8) = gimbal power solenoid (electromechanical relay)
- `MCC_RELAYS::TMS=3` existed in the enum but had no assigned pin — vestigial

**V2 Power Architecture:**
- `PIN_VICOR_EN` (A0) — repurposed — **300V->48V Vicor for the Gimbal** (`GIM_VICOR`, LOW=ON)
- Pin 83 (ex-relay 2) — repurposed — NC opto to enable **TMS Vicor power bank** (`TMS_VICOR`, HIGH=ON)
- Relay 1 (pin 20, swapped from 83) — NO opto, laser power enable. GPS relay retired.
- No solenoids — V2 laser manages HV bus internally
- GNSS always powered at boot — comms-only init in StateManager

### V1 vs V2 Summary

| Subsystem | V1 | V2 |
|-----------|----|----|
| Solenoids | SOL1 (pin 5) + SOL2 (pin 8) | **Retired** |
| Charger enable | GPIO pin 6 | GPIO pin 82 (was CHARGER_MODE on V1) |
| Charger mode | GPIO pin 82 | **Retired** |
| Charger I2C (DBU3200) | Present | **Retired** |
| Vicor bus | Single Vicor (A0, LOW=ON) -> relay bank | **Repurposed** -> `GIM_VICOR` (A0, LOW=ON) |
| TMS Vicor | None | Pin 83 = `TMS_VICOR` NC opto, HIGH=ON |
| Relay 1 | Pin 83 -> GPS power | Pin 20 (swapped) -> Laser power |
| Relay 2 | Pin 20 -> HEL power (NO opto) | **Retired** — pin 83 is now TMS_VICOR |
| GPS relay | Present | **Retired** |
| GNSS power | Via GPS relay | **Always powered at boot** |
| `isBDC_Ready` gate | Set by SOL_BDA (BDA solenoid on) | Set by GIM_VICOR enable in StateManager |

### Confirmed Pin Polarity

| Output | V1 pin | V1 polarity | V2 pin | V2 polarity | Mechanism |
|--------|--------|-------------|--------|-------------|-----------|
| GPS_RELAY | 83 | HIGH=ON | — | — | NO opto |
| VICOR_BUS | A0 | LOW=ON | — | — | Inverted drive |
| LASER_RELAY | 20 | HIGH=ON | 20 | HIGH=ON | NO opto |
| GIM_VICOR | — | — | A0 | LOW=ON | Inverted drive (HW-1) |
| TMS_VICOR | — | — | 83 | HIGH=ON | NC opto -> Vicor PC pin (HW-2) |
| SOL_HEL | 5 | HIGH=ON | — | — | Electromechanical |
| SOL_BDA | 8 | HIGH=ON | — | — | Electromechanical |
| CHARGER | 6 | HIGH=ON | 82 | HIGH=ON | GPIO |

> HW-1: GIM_VICOR polarity analytically derived — verify on first V2 bring-up
> HW-2: TMS_VICOR polarity analytically derived — verify on first V2 bring-up

---

## 2. Pin Definitions (`pin_defs_mcc.hpp`) - Complete

### Removed Pins (V2)
| V1 Define | V1 Pin | Reason |
|-----------|--------|--------|
| `PIN_SOL1_EN` | 5 | Solenoid hardware retired |
| `PIN_SOL2_EN` | 8 | Solenoid hardware retired |
| `PIN_CHARGER_MODE` | 82 | Charger mode retired |

### Changed Pins
| Define | V1 Pin | V2 Pin | Notes |
|--------|--------|--------|-------|
| `PIN_CHARGER_ENABLE` | 6 | 82 | Pin 82 was CHARGER_MODE on V1 |
| `PIN_RELAY1_ENABLE` | 83 | 20 | V1=GPS relay; V2=laser relay |
| `PIN_RELAY2_ENABLE` | 20 | 83 | V1=laser relay; V2=TMS_VICOR NC opto |
| `PIN_VICOR_EN` | A0 | A0 | Same pin, different load and purpose |

---

## 3. Defines (`defines.hpp`) - Complete

### Removed Enums
All three retired — their only purpose was as ICD payload types for retired commands:
- `MCC_SOLENOIDS` — replaced by `MCC_POWER::SOL_HEL/SOL_BDA`
- `MCC_RELAYS` — replaced by `MCC_POWER::GPS_RELAY/LASER_RELAY`
- `MCC_VICORS` — replaced by `MCC_POWER::GIM_VICOR/TMS_VICOR`

### `MCC_POWER` Enum — Final

```cpp
enum class MCC_POWER
{
    GPS_RELAY   = 0,  // V1 only  — GNSS power rail, NO opto, pin 83
    VICOR_BUS   = 1,  // V1 only  — relay bank supply Vicor, LOW=ON, A0
    LASER_RELAY = 2,  // Both     — V1: laser digital bus | V2: laser enable, pin 20
    GIM_VICOR   = 3,  // V2 only  — 300V->48V Gimbal Vicor, LOW=ON, A0
    TMS_VICOR   = 4,  // V2 only  — TMS Vicor power bank, NC opto HIGH=ON, pin 83
    SOL_HEL     = 5,  // V1 only  — laser HV bus solenoid, pin 5
    SOL_BDA     = 6,  // V1 only  — gimbal power solenoid, pin 8
    // bit 7 = RES
};
```

Enum value N = `POWER_BITS` byte 10 bit N. All 7 values defined both revisions.
Unused outputs for active revision always read 0 in `POWER_BITS()`.

### ICD Command Changes

| Byte | Old | New |
|------|-----|-----|
| `0xE2` | `PMS_SOL_ENABLE` — `uint8(MCC_SOLENOIDS); uint8 0/1` | `PMS_POWER_ENABLE` — `uint8(MCC_POWER); uint8 0/1` — INT_ENG only, both revisions |
| `0xE4` | `PMS_RELAY_ENABLE` — `uint8(MCC_RELAYS); uint8 0/1` | `RES_E4` — `STATUS_CMD_REJECTED`. Use `0xE2` with `GPS_RELAY` or `LASER_RELAY` |
| `0xEC` | `PMS_VICOR_ENABLE` | `RES_EC` — `STATUS_CMD_REJECTED`. Use `0xE2` with `VICOR_BUS`, `GIM_VICOR`, or `TMS_VICOR` |

`0xE2 PMS_POWER_ENABLE` is INT_ENG only on both revisions — A3 always rejects.
Invalid `which` value for active revision returns `STATUS_CMD_REJECTED`.

---

## 4. Revision Gate (`hw_rev.hpp`) - Complete

- `MCC_HW_REV_BYTE`: `0x01` (V1) / `0x02` (V2)
- Revision gate: `-DHW_REV_V1` or `-DHW_REV_V2` build flag
- `PIN_PWR_*` and `POL_PWR_*_ON/OFF` macros for all power outputs
- `POL_CHARGER_ON/OFF` both revisions
- Must be first include in `mcc.hpp` — enforced by include order comment

---

## 5. Register Layout — Final

### Byte 7 — `DEVICE_ENABLED_BITS` — unchanged
`0:NTP 1:TMC 2:HEL 3:BAT 4:PTP 5:CRG 6:GNSS 7:BDC`

### Byte 8 — `DEVICE_READY_BITS` — CRG bit 5 always 0 on V2
`0:NTP 1:TMC 2:HEL 3:BAT 4:PTP 5:CRG 6:GNSS 7:BDC`

### Byte 9 — `HEALTH_BITS` — BREAKING CHANGE
System health only. Identical both revisions. No guards in function.

| Bit | Field | Both |
|-----|-------|------|
| 0 | `isReady` | live |
| 1 | `isChargerEnabled` | live |
| 2 | `isNotBatLowVoltage` | live |
| 3-7 | RES | 0 |

Old V1 layout: bits 1-2=solenoids, bit 3=laser, bit 4=charger, bit 5=battery. All changed.

### Byte 10 — `POWER_BITS` — BREAKING CHANGE
Bit N = `MCC_POWER` value N. Flat — no guards in function.

| Bit | `MCC_POWER` | Field | V1 | V2 |
|-----|------------|-------|----|----|
| 0 | `GPS_RELAY=0` | `isPwr_GpsRelay` | live | always 0 |
| 1 | `VICOR_BUS=1` | `isPwr_VicorBus` | live | always 0 |
| 2 | `LASER_RELAY=2` | `isPwr_LaserRelay` | live | live |
| 3 | `GIM_VICOR=3` | `isPwr_GimVicor` | always 0 | live |
| 4 | `TMS_VICOR=4` | `isPwr_TmsVicor` | always 0 | live |
| 5 | `SOL_HEL=5` | `isPwr_SolHel` | live | always 0 |
| 6 | `SOL_BDA=6` | `isPwr_SolBda` | live | always 0 |
| 7 | RES | — | 0 | 0 |

Old V1 layout: bits 0-2=NTP bits (moved to byte 253), bit 3=Vicor, bit 4=relay1, bit 5=relay2.

### Byte 11 — `VOTE_BITS` — unchanged both revisions
`0:LaserTotalHW 1:NotAbort 2:Armed 3:BDA 4:EMON 5:LaserFireReq 6:LaserTotal 7:Combat`

### Byte 253 — `TIME_BITS` — unchanged both revisions
`0:PTP_En 1:PTP_Synched 2:usingPTP 3:NTP_Synched 4:ntpUsingFallback 5:ntpHasFallback 6-7:RES`

### Byte 254 — `HW_REV` — new (was RESERVED)
`0x01` = V1, `0x02` = V2. Written from `MCC_HW_REV_BYTE`. Self-detecting for `MSG_MCC.cs`.

---

## 6. Class (`mcc.hpp`) - Complete

### Power Flag Members
All 7 `isPwr_*` flags declared unconditionally both revisions. No `#ifdef` on members.
`EnablePower()` is the only writer. Unused flags stay false.

```cpp
bool isPwr_GpsRelay   = false;  // MCC_POWER::GPS_RELAY   = 0  V1 only
bool isPwr_VicorBus   = false;  // MCC_POWER::VICOR_BUS   = 1  V1 only
bool isPwr_LaserRelay = false;  // MCC_POWER::LASER_RELAY = 2  both
bool isPwr_GimVicor   = false;  // MCC_POWER::GIM_VICOR   = 3  V2 only
bool isPwr_TmsVicor   = false;  // MCC_POWER::TMS_VICOR   = 4  V2 only
bool isPwr_SolHel     = false;  // MCC_POWER::SOL_HEL     = 5  V1 only
bool isPwr_SolBda     = false;  // MCC_POWER::SOL_BDA     = 6  V1 only
```

### Removed Functions
| Removed | Replacement |
|---------|------------|
| `EnableRelay(MCC_RELAYS, bool)` | Direct `EnablePower()` call |
| `EnableVicor(bool)` (V1) | Direct `EnablePower(VICOR_BUS)` |
| `EnableVicor(MCC_VICORS, bool)` (V2) | Direct `EnablePower(GIM/TMS_VICOR)` |
| `EnableSol(MCC_SOLENOIDS, bool)` | `EnablePower(SOL_HEL/SOL_BDA)` cases |

### Remaining Private Power Methods
```cpp
void EnablePower(MCC_POWER p, bool en);   // sole GPIO writer — all outputs
void EnableCharger(bool en);              // charger GPIO, both revisions
void SetChargeLevel(CHARGE_LEVELS level); // V1 only — I2C to DBU3200
```

---

## 7. Implementation (`mcc.cpp`) - Compile Testing

### `EnablePower()` Side-Effects

| MCC_POWER | Revision | Flags set |
|-----------|----------|-----------|
| `GPS_RELAY` | V1 | `isPwr_GpsRelay` only — `isGNSS_Enabled` NOT touched (software flag) |
| `VICOR_BUS` | V1 | `isPwr_VicorBus` |
| `LASER_RELAY` | Both | `isPwr_LaserRelay`, `isHEL_Enabled`, `ipg.RESET_POLL()` if en |
| `GIM_VICOR` | V2 | `isPwr_GimVicor`, `isBDC_Ready` |
| `TMS_VICOR` | V2 | `isPwr_TmsVicor` |
| `SOL_HEL` | V1 | `isPwr_SolHel` |
| `SOL_BDA` | V1 | `isPwr_SolBda` — `isBDC_Ready` NOT set here; explicit in StateManager |

### `StateManager()` Power Sequences

**STNDBY/ISR/COMBAT — first transition from inactive:**

| Step | V1 | V2 |
|------|----|----|
| 1 | `EnablePower(VICOR_BUS, true)` delay 100ms | `EnablePower(GIM_VICOR, true)` delay 100ms |
| 2 | `EnablePower(LASER_RELAY, true)` delay 100ms | `EnablePower(TMS_VICOR, true)` delay 100ms |
| 3 | 5s -> `ipg.INIT()` | `EnablePower(LASER_RELAY, true)` delay 100ms |
| 4 | `EnablePower(GPS_RELAY, true)` delay 100ms | 5s -> `ipg.INIT()` |
| 5 | 5s -> `gnss.INIT()` | `gnss.INIT()` (no power delay — always powered) |
| 6 | `EnablePower(SOL_HEL, true)` delay 5s | `ipg.CLEAR_ERRORS(); ipg.SET_POWER(10)` |
| 7 | `ipg.CLEAR_ERRORS(); ipg.SET_POWER(10)` | — |
| 8 | `EnablePower(SOL_BDA, true)` delay 5s; `isBDC_Ready=true` | — |
| 9 | `ipg.CLEAR_ERRORS(); ipg.SET_POWER(10)` | — |

**OFF/FAULT/MAINT — shutdown:**

| Step | V1 | V2 |
|------|----|----|
| 1 | `EnableCharger(false)` delay 500ms | `EnableCharger(false)` delay 500ms |
| 2 | `EnablePower(SOL_HEL, false)` delay 500ms | — |
| 3 | `EnablePower(SOL_BDA, false)` delay 500ms | — |
| 4 | `EnablePower(LASER_RELAY, false)` delay 500ms | `EnablePower(LASER_RELAY, false)` delay 500ms |
| 5 | `EnablePower(GPS_RELAY, false)` delay 500ms | — |
| 6 | `EnablePower(VICOR_BUS, false)` delay 500ms | `EnablePower(GIM_VICOR, false)` delay 500ms |
| 7 | — | `EnablePower(TMS_VICOR, false)` delay 500ms |

### Key Logic Notes
- `SEND_FIRE_STATUS` gate: `isPwr_LaserRelay && isBDC_Ready` — both revisions
- `isGNSS_Enabled` is a software flag only — not power-coupled either revision
- `isBDC_Ready` on V1: set explicitly in StateManager after SOL_BDA on; not a side-effect
- `isBDC_Ready` on V2: set by `EnablePower(GIM_VICOR, en)` side-effect in StateManager

---

## 7A. ICD and C# Client Updates Required

### `CROSSBOW_ICD_INT_ENG.md`
- Byte 9: rename STATUS_BITS -> HEALTH_BITS; new 3-bit layout
- Byte 10: rename STATUS_BITS2 -> POWER_BITS; full MCC_POWER-mapped layout with V1/V2 columns
- Byte 254: RESERVED -> HW_REV (0x01=V1, 0x02=V2)
- 0xE2: rename PMS_SOL_ENABLE -> PMS_POWER_ENABLE; new payload format
- 0xE4: mark RETIRED -> RES_E4
- 0xEC: mark RETIRED -> RES_EC
- 0xED: add V2 rejection note
- FW version: bump to 3.3.0
- Add MCC_POWER enum table; remove MCC_SOLENOIDS, MCC_RELAYS, MCC_VICORS

### `MSG_MCC.cs`
- Add `HW_REV` property (byte 254), `IsV1`/`IsV2` helpers, `HW_REV_Label`
- Replace all byte 9 accessors: rename to `HealthBits`; new bit positions
- Replace all byte 10 accessors: rename to `PowerBits`; `pb_GpsRelay` through `pb_SolBda`
- Remove: `isSolenoid1/2Enabled`, `isVicorEnabled`, `isRelay1/2/3/4Enabled`, `isLaserPowerEnabled`

### `mcc.cs`
- Add `PMS_POWER_ENABLE` (0xE2) — `uint8(MCC_POWER); uint8 0/1`
- Remove or stub 0xE4 and 0xEC handlers
- Guard 0xED to V1 only (check `IsV1`)

### `defines.cs`
- Add `MCC_POWER` enum: `GPS_RELAY=0` through `SOL_BDA=6`
- Remove `MCC_SOLENOIDS`, `MCC_RELAYS`, `MCC_VICORS`
- Verify `TMC_VICORS` has `PUMP1=2`, `PUMP2=4` (session 30)
- Verify `FRAME_KEEPALIVE=0xA4`, `BDC_DEVICES.PTP=7`

### `frmMCC.cs`
- Add `ApplyHwRevLayout()` — show/hide controls based on HW_REV_BYTE
- Hide solenoid controls on V2
- V1: single Vicor toggle; V2: separate GIM + TMS toggles
- Hide charger level on V2
- Show HW_REV_Label in status strip
- 7-bit POWER_BITS display matching MCC_POWER enum

---

## 8. Files Produced

| File | Status | Notes |
|------|--------|-------|
| `hw_rev.hpp` | ✅ Complete | Revision gate, polarity macros, `MCC_HW_REV_BYTE` |
| `pin_defs_mcc.hpp` | ✅ Complete | Full V1/V2 guards |
| `defines.hpp` | ✅ Complete | Fleet canonical; `MCC_POWER` 7 values; enums removed; commands updated |
| `mcc.hpp` | ✅ Complete | `HEALTH_BITS`, `POWER_BITS`, `isPwr_*` flags, wrappers removed |
| `mcc.cpp` | ⚠️ Compile testing | Manual comment merge complete — awaiting compile result |
| `MCC.ino` | ✅ Complete | V1/V2 guards on `Wire.begin()`, solenoid `pinMode` |
| `defines.cs` | ✅ Complete | `MCC_POWER` added; `MCC_SOLENOIDS`/`RELAYS`/`VICORS` removed; `0xE2`/`E4`/`EC` updated |
| `mcc.cs` | ✅ Complete | `EnablePower()` replaces `EnableSolenoid`/`EnableRelay`/`VicorEnable` |
| `MSG_MCC.cs` | ✅ Complete | `HealthBits`/`PowerBits`/`HW_REV`/`IsV1`/`IsV2`/`pb_*` added; compat aliases retained |
| `frmMCC.cs` | ⚠️ Partial | Compile errors fixed; `HW_REV_Label` in tssVersion; `ApplyHwRevLayout()` pending |

---

## 9. Documents Updated This Session

| Document | Version | Changes |
|----------|---------|---------|
| `CROSSBOW_ICD_INT_ENG.md` | 3.3.8 → **3.4.0** | HEALTH_BITS, POWER_BITS, HW_REV byte 254, PMS_POWER_ENABLE, RES_E4/EC retired, MCC_POWER enum, MSG_MCC.cs section |
| `ARCHITECTURE.md` | 3.3.3 → **3.3.4** | §9 V1/V2 hardware variants table, §9.3 fire gate, §9.5 register bits, §9.6 Build Configuration (new), §15 MCC 3.3.0, §16 compat row |
| `CROSSBOW_UG_ENG_GUI_draft.md` | 1.2.0 → **1.3.0** | MCC-HW1–4 action items added |
| `CROSSBOW_DOCUMENT_REGISTER.md` | 1.4.7 → **1.4.8** | Version bumps for all three above |

---

## 10. Stale V2 Regressions Discarded

| Item | V1 (kept) | V2 (discarded) |
|------|-----------|----------------|
| Serial buffer | `static char[64]` | `String` — heap fragmentation |
| MCU temp read | DWT cycle counter | `HAL_ADC_PollForConversion` — SysTick race |
| `isUnSolicitedEnabled` | Retired session 35 | Active — wrong |
| NTP bits in STAT_BITS2 | Moved to TIME_BITS byte 253 | Still in STAT_BITS2 bits 0-2 |
| `isPTP_Enabled` default | `false` | `true` — wrong |
| `clientIdx` in UDP_PARSE | Present | Missing |
| IP defines | All 8 present | 4 missing |
| ICD 0xA1 name | `RES_A1` (session 35) | `GET_REGISTER1` (old) |
| ICD 0xA4 name | `FRAME_KEEPALIVE` (session 35) | `EXT_FRAME_PING` (old) |
| SEND_REG_01 unsolicited gate | `wantsUnsolicited` per-slot | All active clients unconditionally |

---

## 11. Open Items — Next Session

| ID | Status | Item |
|----|--------|------|
| COMPILE | ⚠️ Pending | `mcc.cpp` compile result — fix any errors |
| MCC-HW4 | ⚠️ Pending | `frmMCC.cs` `ApplyHwRevLayout()` — implement |
| HW-1 | ⚠️ Verify HW | `POL_PWR_GIM_ON = LOW` — confirm on V2 bring-up |
| HW-2 | ⚠️ Verify HW | `POL_PWR_TMS_ON = HIGH` — confirm on V2 bring-up |
| OQ-5 | ⚠️ Pending | BDC and FMC `defines.hpp` — deploy merged canonical file |
| OQ-6 | ⚠️ Pending | `defines.cs` parity: `TMC_VICORS`, `FRAME_KEEPALIVE`, `BDC_DEVICES.PTP` |
| INT_OPS | ⚠️ Verify | Confirm `0xED` not on INT_OPS whitelist — add V2 rejection note if listed |
