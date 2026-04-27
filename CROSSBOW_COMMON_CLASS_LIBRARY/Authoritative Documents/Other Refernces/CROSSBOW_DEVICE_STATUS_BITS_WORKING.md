# CROSSBOW Device Status Bits — Working Document
**Status:** DRAFT — for discussion. Will merge into ICD_INT_ENG and ICD_INT_OPS.
**Date:** 2026-04-26
**Scope:** New STATUS_BITS bytes per device for MCC and BDC REG1.
         Parallel DEVICE_WARN_BITS byte for each controller.
         TIME_BITS [253 MCC / 391 BDC] serves NTP and PTP — no separate bytes needed.

---

## Architecture

### Three-register quick look (existing + new)

| Register | Meaning | Who sets |
|----------|---------|---------|
| `DEVICE_ENABLED_BITS` | Operator/firmware has enabled device | Firmware config |
| `DEVICE_READY_BITS` | Device operational | **Derived from XXX_STATUS_BITS** |
| `DEVICE_WARN_BITS` *(new)* | Device degraded but operational | **Derived from XXX_STATUS_BITS** |

> **Key principle:** `XXX_STATUS_BITS` is the source of truth.
> `DEVICE_READY_BITS` and `DEVICE_WARN_BITS` are computed summaries derived from it.
> Where a device already computes and packs a status bit in its own message,
> the STATUS_BITS byte **mirrors** that bit rather than recomputing it.
> THEIA reads quick-look first — drills into STATUS_BITS only when needed.
> A developer with only REG1 and the ICD can determine full device health without source access.

### Device severity definitions

| Severity | Definition | `DEVICE_READY_BITS` | `DEVICE_WARN_BITS` |
|----------|-----------|--------------------|--------------------|
| **READY** | All required bits nominal. Fully operational. | 1 | 0 |
| **READY + WARN** | Primary function available but degraded. | 1 | 1 |
| **ERROR** | Cannot perform primary function. | 0 | 0 |
| *(invalid)* | Should never occur. | 0 | 1 |

> **READY and WARN can coexist.** READY+WARN satisfies advancement prerequisites.
> Advancement blocks only on ERROR (`DEVICE_READY=0`).
> **DEVICE_WARN_BITS bit N is never set if DEVICE_ENABLED_BITS bit N is 0.**
> Disabled devices have no warn state — they are simply not in service.

### Mirroring principle

Where a device already packs status bits into its own embedded message, the STATUS_BITS
byte mirrors the operationally relevant subset. This ensures:
- No recomputation of conditions the device firmware already owns
- Consistent interpretation between device firmware, MCC/BDC firmware, and THEIA
- Single source of truth per condition

**Applies to:** TMC (STAT_BITS1/2), GIM (StatusX/Y, StopCodeX/Y),
BAT (StatusWord), GNSS (solStatus, PosType).

---

## State / Mode Rules

### Regression — mid-operation

| Severity | MCC device | BDC device | Recovery |
|----------|-----------|-----------|---------|
| **ERROR** (critical) | `STATE→ISR`, `MODE→OFF` | `STATE→STNDBY`, `MODE→OFF` | Condition clears → operator re-advances |
| **ERROR** (non-critical) | No state/mode change | No state/mode change | Informational only |
| **WARN** (any) | No change. `DEVICE_WARN_BITS` set. | No change. `DEVICE_WARN_BITS` set. | Clears naturally |

> **MCC ERROR → ISR:** Laser safed. Observation platform (gimbal, cameras) stays alive.
> **BDC ERROR → STNDBY:** Beam director compromised. Safe idle with no engagement posture.
> **Non-critical devices** (GNSS, INCL, NTP, PTP, CRG): ERROR is informational only.
> Fire chain drops naturally when ISR or STNDBY is entered.

### Advancement blocking

Transition rejected with `STATUS_PREREQ_FAIL` (new status code `0x07`).
THEIA surfaces the blocking STATUS_BITS byte — operator resolves without source access.

#### State advancement prerequisites

| Transition | Required — all must be READY or READY+WARN |
|-----------|---------------------------------------------|
| `OFF → STNDBY` | MCC CommHealth · BDC CommHealth |
| `STNDBY → ISR` | BDC_GIM · BDC_VIS · BDC_JET |
| `ISR → COMBAT` | MCC_HEL · MCC_BAT `isNotLowVoltage` · BDC_FSM · MCC_TMC (cooling req. HW only) |

> Vote subsystem owns fire interlocks — `BDCTotalVote` and `isLaserTotalHW` are
> NOT advancement prerequisites. They are real-time gates owned by the vote chain.
> MCC_BDC is NOT a STNDBY→ISR prerequisite — only blocks/exits COMBAT.

#### Mode advancement prerequisites

| Mode | Minimum required |
|------|-----------------|
| `→ POS / RATE` | BDC_GIM · BDC_JET · (BDC_VIS OR BDC_MWIR) |
| `→ CUE` | BDC_GIM · BDC_JET · (BDC_VIS OR BDC_MWIR) |
| `→ ATRACK (VIS)` | BDC_GIM · BDC_JET · BDC_VIS |
| `→ ATRACK (MWIR)` | BDC_GIM · BDC_JET · BDC_MWIR |
| `ATRACK → FTRACK` | BDC_GIM · BDC_JET · BDC_FSM · (BDC_VIS OR BDC_MWIR) |

#### Immediate MODE→OFF triggers

```
BDC_GIM ERROR                              → MODE→OFF (all modes)
BDC_JET ERROR                              → MODE→OFF (all modes)
BDC_VIS ERROR AND BDC_MWIR ERROR           → MODE→OFF (both cameras lost)
BDC_VIS ERROR (while in ATRACK on VIS)    → MODE→OFF
BDC_MWIR ERROR (while in ATRACK on MWIR)  → MODE→OFF
```

### Device criticality matrix

| Device | Mid-op STATE change | Mid-op MODE change | Blocks STATE > | Blocks MODE > |
|--------|--------------------|--------------------|----------------|---------------|
| **MCC_TMC** | COMBAT→ISR (cooling req. HW only) | →OFF | ISR→COMBAT (cooling req.) | No |
| **MCC_HEL** | COMBAT→ISR | →OFF | ISR→COMBAT | No |
| **MCC_BAT** `!isNotLowVoltage` | COMBAT→ISR | →OFF | ISR→COMBAT | No |
| **MCC_CRG** | No | No | No | No |
| **MCC_GNSS** | No (latch) | No (latch) | No | No |
| **MCC_BDC** | COMBAT→ISR | →OFF | ISR→COMBAT | No |
| **BDC_GIM** | ISR/COMBAT→STNDBY | →OFF (all modes) | STNDBY→ISR | All modes |
| **BDC_VIS** | ISR/COMBAT→STNDBY | →OFF (both lost or ATRACK VIS) | STNDBY→ISR | ATRACK(VIS) |
| **BDC_MWIR** | No | →OFF (both lost or ATRACK MWIR) | No | ATRACK(MWIR) |
| **BDC_FSM** | COMBAT→ISR | FTRACK/COMBAT→ATRACK | ISR→COMBAT | FTRACK |
| **BDC_JET** | ISR/COMBAT→STNDBY | →OFF (all modes) | STNDBY→ISR | All modes |
| **BDC_INCL** | No | No | No | No |
| **NTP / PTP** | No | No | No | No |

> ⚠️ **FSM:** Steers the laser beam. Loss of FSM during COMBAT = loss of laser pointing
> control → STATE→ISR (laser safed). MODE→ATRACK not OFF — coarse tracker remains viable.
>
> ⚠️ **MCC_BDC:** Vote chain error only. BDC observable via A3. Does NOT block STNDBY→ISR.
>
> ⚠️ **TMC:** COMBAT prerequisite on cooling-required HW only (V2/V3·6kW).
> Firmware determines from `LaserModel`.
>
> ⚠️ **GNSS/INCL/NTP/PTP:** Informational only. Position, attitude, and time latched on
> last valid data. Downstream vote mechanisms are the actual gates.

---

## Fire Control Consistency

> **⚠️ OPEN WORK ITEM — FC-CONSISTENCY-1**
>
> Fire control state must be fully consistent across all five subsystems:
> MCC · BDC · TRC · HEL · HMI (THEIA)
>
> The authoritative fire readiness condition owned by MCC:
> ```
> isFireExpected = BDCTotalVote
>               AND isLaserTotalHW
>               AND (System_State == COMBAT)
>               AND isNotBatLowVoltage
>               AND isTriggerPulled
> ```
>
> Five failure modes to audit across all subsystems:
> 1. GUI shows fire-ready but laser does not fire
> 2. Video OSD shows fire-ready but GUI shows interlocked
> 3. Laser fires but no EMON confirmation displayed
> 4. Vote bits disagree between MCC REG1 and BDC REG1 readbacks
> 5. TRC OSD vote display lags or mismatches MCC/BDC telemetry
>
> May require a dedicated fire vote summary byte — see FC-CONSISTENCY-1 open item.
> Review all five subsystems before implementing MCC_HEL_STATUS_BITS EMON bits.

---

## MCC Devices

### DEVICE_WARN_BITS — MCC (new byte, parallel to ENABLED [7] and READY [8])

> WARN bit is only set if the corresponding DEVICE_ENABLED bit is 1.
> Disabled devices have no warn state.

| Bit | Device | Set when | Source |
|-----|--------|---------|--------|
| 0 | NTP | enabled AND (fallback active or not synced) | `TIME_BITS.tb_ntpUsingFallback \|\| !tb_isNTP_Synched` |
| 1 | TMC | enabled AND connected but not fully nominal | `MCC_TMC_STATUS_BITS` WARN logic |
| 2 | HEL | enabled AND (training mode OR fire interlocked OR temp warn) | `MCC_HEL_STATUS_BITS` WARN logic |
| 3 | BAT | enabled AND SOC degraded but above trip | `MCC_BAT_STATUS_BITS` WARN logic |
| 4 | PTP | enabled AND not synced | `TIME_BITS.tb_isPTP_Enabled && !tb_isPTP_Synched` |
| 5 | CRG | enabled AND not charging | `MCC_CRG_STATUS_BITS` WARN logic |
| 6 | GNSS | enabled AND position valid but degraded solution | `MCC_GNSS_STATUS_BITS` WARN logic |
| 7 | BDC | enabled AND A1 reachable but vote echo silent | `MCC_BDC_STATUS_BITS` WARN logic |

---

### MCC_TMC_STATUS_BITS

**Device:** Thermal Management Controller (Lytron chiller)
**Primary function:** Active cooling for HEL and optics
**Comms:** MCC serial → TMC
**Mirroring:** STAT_BITS1 (pump bits) and STAT_BITS2 (LCM/flow error bits)

| Bit | Name | Mirrors | Informational |
|-----|------|---------|---------------|
| 0 | `isConnected` | Controller computed | No — ERROR gate |
| 1 | `isPump1Enabled` | STAT_BITS1 bit 1 (V1: `isPumpEnabled`) | No — ERROR gate |
| 2 | `isPump2Enabled` | STAT_BITS1 bit 5 (V2/V3 only; 0 on V1) | No — ERROR gate |
| 3 | `isLCM1_Error` | STAT_BITS2 bit 2 | No — ERROR/WARN gate |
| 4 | `isFlow1_Error` | STAT_BITS2 bit 3 | No — ERROR gate |
| 5 | `isLCM2_Error` | STAT_BITS2 bit 6 | No — ERROR/WARN gate |
| 6 | `isFlow2_Error` | STAT_BITS2 bit 7 | No — ERROR gate |
| 7 | RES | — | — |

> **Note:** `isLCM1_Enabled`, `isLCM2_Enabled` (STAT_BITS2 bits 1, 5) are config display
> bits — available in STAT_BITS2 for drill-down but not mirrored here as they do not
> affect operational severity.
> **V1 hardware:** Single pump — `isPump2Enabled` always 0. ERROR condition uses only `isPump1Enabled`.

```
ERROR: !isConnected
       OR (!isPump1Enabled AND !isPump2Enabled)  — both pumps down (V1: pump1 only required)
       OR (isLCM1_Error AND isLCM2_Error)         — both chillers faulted, no cooling path
       OR isFlow1_Error OR isFlow2_Error          — any flow error = avg flow has failed

READY+WARN: isConnected
            AND (isPump1Enabled OR isPump2Enabled)
            AND NOT (isLCM1_Error AND isLCM2_Error)
            AND NOT (isFlow1_Error OR isFlow2_Error)
            AND (!isPump2Enabled OR isLCM1_Error OR isLCM2_Error)  — something degraded

READY: isConnected
       AND isPump1Enabled AND (isPump2Enabled OR V1_hardware)
       AND !isLCM1_Error AND !isLCM2_Error
       AND !isFlow1_Error AND !isFlow2_Error
```

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| `!isConnected` | ERROR | COMBAT→ISR (cooling req.) | →OFF | ISR→COMBAT (cooling req.) |
| `!pump1 AND !pump2` | ERROR | COMBAT→ISR (cooling req.) | →OFF | ISR→COMBAT (cooling req.) |
| `LCM1_Error AND LCM2_Error` | ERROR | COMBAT→ISR (cooling req.) | →OFF | ISR→COMBAT (cooling req.) |
| `isFlow1_Error OR isFlow2_Error` | ERROR | COMBAT→ISR (cooling req.) | →OFF | ISR→COMBAT (cooling req.) |
| `!pump2 (V2/V3)` | READY+WARN | none | none | none |
| `LCM1_Error XOR LCM2_Error` | READY+WARN | none | none | none |

> ⚠️ **TBD:** Cooling requirement by HW config — confirm V2/V3·6kW vs V1/V3·3kW rule.

---

### MCC_HEL_STATUS_BITS

**Device:** IPG Photonics laser (YLM-3K or YLM-6K)
**Primary function:** Laser output on operator command
**Comms:** MCC TCP → IPG controller

> **Fire control dependency:** `isEMON_Missing` and `isEMON_Unexpected` depend on
> `isFireExpected` — see FC-CONSISTENCY-1. These bits should not be finalised until
> fire control consistency review is complete.
>
> **`isFireExpected`** (MCC-computed, to be packed as new HEALTH_BITS or VoteBits bit):
> ```
> isFireExpected = BDCTotalVote AND isLaserTotalHW
>               AND (System_State == COMBAT)
>               AND isNotBatLowVoltage
>               AND isTriggerPulled
> ```

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isSensed` | `IPGMsg.IsSensed` | No — ERROR gate |
| 1 | `isHB_OK` | `HB_HEL_ms > 0 && < 255` | No — ERROR gate |
| 2 | `isNOTREADY` | `IPGMsg.IsNotReady` (model-aware 3K bit9 / 6K bit11) | No — ERROR gate |
| 3 | `isModelMatch` | HEALTH_BITS bit 4 | No — ERROR gate |
| 4 | `isEMON` | `IPGMsg.IsEMON` (model-aware) | **Yes** — display only |
| 5 | `isEMON_Unexpected` | `!isFireExpected AND isEMON` | No — ERROR gate *(pending FC-CONSISTENCY-1)* |
| 6 | `isEMON_Missing` | `isFireExpected AND !isEMON` | No — ERROR gate *(pending FC-CONSISTENCY-1)* |
| 7 | `isFireInterlocked` | `isTriggerPulled AND !isFireExpected AND !isEMON` | No — WARN gate *(pending FC-CONSISTENCY-1)* |

> **Note:** `isTrainingMode` (previously bit 5) is already in HEALTH_BITS bit 3 and
> DEVICE_WARN_BITS bit 2. Not duplicated here.
> **Note:** `isTempWarn` — TBD pending IPGMsg StatusWord temp bit map for 3K and 6K.
> Will occupy a RES bit when confirmed.

```
ERROR: !isSensed || !isHB_OK || isNOTREADY || !isModelMatch
       OR isEMON_Unexpected   — energy out without all conditions met (safety)
       OR isEMON_Missing       — all conditions met, trigger pulled, no energy out

READY+WARN: all ERROR clear
            AND (isFireInterlocked  — trigger pulled, blocked by vote
                OR isTrainingMode   — power clamped (from HEALTH_BITS)
                OR isTempWarn)      — running warm (TBD)

READY: all ERROR clear AND !isFireInterlocked AND !isTrainingMode AND !isTempWarn
```

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| `!isSensed` | ERROR | COMBAT→ISR | →OFF | ISR→COMBAT |
| `!isHB_OK` | ERROR | COMBAT→ISR | →OFF | ISR→COMBAT |
| `isNOTREADY` | ERROR | COMBAT→ISR | →OFF | ISR→COMBAT |
| `!isModelMatch` | ERROR | COMBAT→ISR | →OFF | ISR→COMBAT |
| `isEMON_Unexpected` | ERROR | COMBAT→ISR | →OFF | ISR→COMBAT |
| `isEMON_Missing` | ERROR | COMBAT→ISR | →OFF | ISR→COMBAT |
| `isFireInterlocked` | READY+WARN | none | none | none |
| `isTrainingMode` | READY+WARN | none | none | none |

> ⚠️ **TBD:** IPGMsg StatusWord temperature bit positions for 3K and 6K — full StatusWord map needed.
> ⚠️ **Pending:** FC-CONSISTENCY-1 review before finalising bits 5–7.

---

### MCC_BAT_STATUS_BITS

**Device:** Battery pack (RS485 BMS)
**Primary function:** Power delivery to system
**Comms:** MCC RS485 → BMS
**Mirroring:** `BatteryMsg.StatusWord` — TBD pending MSG_BATTERY source

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isConnected` | `isBAT_DeviceReady` | No — ERROR gate |
| 1 | `isNotLowVoltage` | `isNotBatLowVoltage` (HEALTH_BITS bit 2) | No — vote gate / STATE gate |
| 2 | `isCharging` | `BatteryMsg.PackCurrent > 0` | **Yes** — display only |
| 3 | `isDischarging` | `BatteryMsg.PackCurrent < 0` | **Yes** — display only |
| 4 | `isSOC_OK` | `BatteryMsg.RSOC > warn_threshold` | No — WARN gate |
| 5 | `isTempOK` | `BatteryMsg.PackTemp` within bounds | No — WARN gate |
| 6 | `isError` | `BatteryMsg.StatusWord` error bits | No — ERROR gate *(TBD — pending MSG_BATTERY source)* |
| 7 | `isAlarm` | `BatteryMsg.StatusWord` alarm bits | No — WARN gate *(TBD — pending MSG_BATTERY source)* |

```
ERROR: !isConnected OR isError

READY+WARN: isConnected AND !isError AND isNotLowVoltage
            AND (!isSOC_OK OR !isTempOK OR isAlarm)

READY: isConnected AND !isError AND isNotLowVoltage AND isSOC_OK AND isTempOK AND !isAlarm
       (isCharging, isDischarging are display-only)
```

> **Note on `isNotLowVoltage`:** Direct STATE regression trigger independent of device severity.
> `!isNotLowVoltage` → COMBAT→ISR regardless of device READY status.

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| `!isConnected OR isError` | ERROR | COMBAT→ISR | →OFF | ISR→COMBAT |
| `!isNotLowVoltage` | *(vote gate)* | COMBAT→ISR | →OFF | ISR→COMBAT |
| `!isSOC_OK OR isTempOK OR isAlarm` | READY+WARN | none | none | none |

> ⚠️ **TBD:** RSOC warn threshold (suggest 20%). PackTemp bounds from BMS datasheet.
> ⚠️ **TBD:** MSG_BATTERY StatusWord bit map — need class source to identify error/alarm/protection bits.

---

### MCC_CRG_STATUS_BITS

**Device:** Battery charger (V1: Delta DBU / V2: GPIO)
**Primary function:** Battery charging
**Comms:** MCC I2C → DBU (V1) / GPIO (V2)

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isConnected` | `isCRG_DeviceReady` | No — ERROR gate |
| 1 | `isEnabled` | `isCharger_Enabled` (HEALTH_BITS bit 1) | No — WARN gate |
| 2 | `isVIN_OK` | `CMCMsg.VIN > threshold` | No — WARN gate |
| 3 | `isCharging` | `CMCMsg.IOUT > 0` | **Yes** — display only |
| 4 | `isAtMaxLevel` | `CMCMsg.ChargeLevel == HI` | **Yes** — display only |
| 5–7 | RES | — | — |

```
ERROR: !isConnected

READY+WARN: isConnected AND (!isEnabled OR !isVIN_OK)

READY: isConnected AND isEnabled AND isVIN_OK
       (isCharging, isAtMaxLevel are display-only)
```

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| `!isConnected` | ERROR | none | none | none |
| `!isEnabled OR !isVIN_OK` | READY+WARN | none | none | none |

> ⚠️ **Note:** CRG never triggers state/mode regression — battery voltage is the gate.
> ⚠️ **Note:** V2 GPIO only — bits 3–4 always 0. Bits 0–2 remain valid.
> ⚠️ **TBD:** VIN threshold (V).

---

### MCC_GNSS_STATUS_BITS

**Device:** NovAtel GNSS receiver (dual-antenna)
**Primary function:** Position and heading for CUE mode and horizon computation
**Comms:** MCC UDP receive from NovAtel
**Mirroring:** `LatestsolStatus`, `LatestPosType` enums from NovAtel binary log

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isConnected` | `isGNSS_DeviceReady` | No — ERROR gate |
| 1 | `isHB_OK` | `HB_GNSS_ms > 0 && < 255` | No — ERROR gate |
| 2 | `isPositionValid` | `GNSSMsg.LatestsolStatus == SOL_COMPUTED` | No — ERROR gate |
| 3 | `isSIV_OK` | `GNSSMsg.SIV >= 4` | No — WARN gate |
| 4 | `isHeadingValid` | `GNSSMsg.Heading_STDEV < threshold` | No — WARN gate |
| 5 | `isINS_Converged` | `GNSSMsg.LatestPosType` is INS solution type | No — WARN gate |
| 6 | `isTerraStar_OK` | `GNSSMsg.TerraStar_SyncState` nominal | **Yes** — display only |
| 7 | RES | — | — |

```
ERROR: !isConnected || !isHB_OK || !isPositionValid
        — no valid position; last fix latched for downstream consumers

READY+WARN: isConnected AND isHB_OK AND isPositionValid
            AND (!isSIV_OK OR !isHeadingValid OR !isINS_Converged)

READY: isConnected AND isHB_OK AND isPositionValid
       AND isSIV_OK AND isHeadingValid AND isINS_Converged
       (isTerraStar_OK is display-only)
```

> **GNSS ERROR has no state/mode consequence.** Position latched on last valid fix.
> Replaces client-side: `GPS_STATUS = isGNSS_DeviceReady && SIV >= 4` in `mcc.cs`

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| Any ERROR | ERROR | **none** | **none** | **none** |
| `!isSIV_OK OR !isHeadingValid OR !isINS_Converged` | READY+WARN | none | none | none |

> ⚠️ **TBD:** `Heading_STDEV` threshold. INS converged PosType enum values — confirm NovAtel
> classification (INS_PSRSP, INS_PSRDIFF, INS_RTKFLOAT, INS_RTKFIXED, INS_SBAS).
> ⚠️ **TBD:** TerraStar availability — not all units equipped.

---

### MCC_BDC_STATUS_BITS

**Device:** BDC controller (MCC A1 fire control link)
**Primary function:** Deliver fire control votes to BDC; receive vote echo
**Comms:** MCC UDP A1 → BDC

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isEnabled` | `isBDC_DeviceEnabled` | No — ERROR gate |
| 1 | `isReachable` | `endPacket()` result on A1 send | No — ERROR gate |
| 2 | `isVoteActive` | BDC vote echo received on A1 round-trip | No — WARN gate |
| 3–7 | RES | — | — |

```
ERROR: !isEnabled OR !isReachable

READY+WARN: isEnabled AND isReachable AND !isVoteActive

READY: isEnabled AND isReachable AND isVoteActive
```

> **MCC_BDC ERROR → COMBAT→ISR only.** BDC observable via THEIA A3.
> Does NOT block STNDBY→ISR — only blocks/exits COMBAT.

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| `!isReachable` | ERROR | COMBAT→ISR | →OFF | ISR→COMBAT |
| `!isVoteActive` during COMBAT | ERROR | →ISR | →OFF | ISR→COMBAT |
| `!isVoteActive` during ISR | READY+WARN | none | none | none |

> ⚠️ **TBD:** `isVoteActive` tracking mechanism — `HB_MCC_ms` echo on BDC side or separate counter.

---

## BDC Devices

### DEVICE_WARN_BITS — BDC (new byte, parallel to ENABLED [8] and READY [9])

> WARN bit is only set if the corresponding DEVICE_ENABLED bit is 1.

| Bit | Device | Set when | Source |
|-----|--------|---------|--------|
| 0 | NTP | enabled AND (fallback active or not synced) | `TIME_BITS.tb_ntpUsingFallback \|\| !tb_isNTP_Synched` |
| 1 | GIMBAL | enabled AND at soft limit | `BDC_GIM_STATUS_BITS` WARN logic |
| 2 | VIS | enabled AND Fuji HB degraded or FOV not valid | `BDC_VIS_STATUS_BITS` WARN logic |
| 3 | MWIR | enabled AND warming up | `BDC_MWIR_STATUS_BITS` WARN logic |
| 4 | FSM | enabled AND at angular limit | `BDC_FSM_STATUS_BITS` WARN logic |
| 5 | JETSON | enabled AND temp or load above WARN threshold | `BDC_JET_STATUS_BITS` WARN logic |
| 6 | INCL | enabled AND data valid but platform not level | `BDC_INCL_STATUS_BITS` WARN logic |
| 7 | PTP | enabled AND not synced | `TIME_BITS.tb_isPTP_Enabled && !tb_isPTP_Synched` |

---

### BDC_GIM_STATUS_BITS

**Device:** Galil motion controller (gimbal pan/tilt)
**Primary function:** Gimbal pointing and tracking
**Comms:** BDC TCP → Galil
**Mirroring:** `StatusX`, `StatusY`, `StopCodeX`, `StopCodeY` from Galil — TBD pending MSG_GIMBAL source

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isConnected` | `gimbalMSG.isConnected` | No — ERROR gate |
| 1 | `isReady` | `gimbalMSG.isReady` | No — ERROR gate |
| 2 | `isStarted` | `gimbalMSG.isStarted` | No — ERROR gate |
| 3 | `isAtSoftLimit` | StatusX/Y or StopCode — TBD | No — WARN gate |
| 4 | `isMoving` | `SpeedX != 0 \|\| SpeedY != 0` | **Yes** — display only |
| 5 | `isFault` | StopCode fault bits — TBD | No — ERROR gate |
| 6–7 | RES | — | — |

```
ERROR: !isConnected OR !isReady OR !isStarted OR isFault

READY+WARN: isConnected AND isReady AND isStarted AND !isFault AND isAtSoftLimit

READY: isConnected AND isReady AND isStarted AND !isFault AND !isAtSoftLimit
       (isMoving is display-only)
```

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| `!isConnected OR !isReady OR !isStarted OR isFault` | ERROR | ISR/COMBAT→STNDBY | →OFF (all) | STNDBY→ISR · all modes |
| `isAtSoftLimit` | READY+WARN | none | none | none |

> ⚠️ **TBD:** Galil StatusX/Y and StopCode bit map — `isFault` vs `isAtSoftLimit` definitions.
> Need MSG_GIMBAL source.

---

### BDC_VIS_STATUS_BITS

**Device:** VIS channel — Fuji lens (serial) + Alvium sensor (TRC)
**Primary function:** Visual imaging and ATRACK input
**Comms:** BDC serial → Fuji; TRC GStreamer → Alvium

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isFuji_Connected` | `isFuji_DeviceReady` | No — ERROR gate |
| 1 | `isFuji_HB_OK` | `HB_FUJI_ms > 0 && < 255` | No — WARN gate |
| 2 | `isFOV_Valid` | `VIS_FOV > 0` | No — WARN gate · jog safety gate |
| 3 | `isAlvium_Powered` | `trcMSG.Cameras[VIS].isPowered` | No — ERROR gate |
| 4 | `isAlvium_Connected` | `trcMSG.Cameras[VIS].isConnected` | No — ERROR gate |
| 5 | `isCapturing` | `trcMSG.isVIS_Capturing` | No — ERROR gate |
| 6 | `isAlvium_TempOK` | `trcMSG.deviceTemperature <= threshold` | No — WARN gate |
| 7 | RES | — | — |

```
ERROR: !isFuji_Connected OR !isAlvium_Powered OR !isAlvium_Connected OR !isCapturing

READY+WARN: isFuji_Connected AND isAlvium_Powered AND isAlvium_Connected AND isCapturing
            AND (!isFuji_HB_OK OR !isFOV_Valid OR !isAlvium_TempOK)

READY: isFuji_Connected AND isFuji_HB_OK AND isFOV_Valid
       AND isAlvium_Powered AND isAlvium_Connected AND isCapturing
       AND isAlvium_TempOK
```

> **Replaces client-side:** `VIS_PING` + `VIS_STATUS` composite in `bdc.cs`
> **`isFOV_Valid`:** Controller-level gate for THEIA-JOG-FOV-1 — BDC rejects jog commands when FOV unknown.

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| `!isFuji_Connected OR !isAlvium_*` | ERROR | ISR/COMBAT→STNDBY | →OFF (both lost or ATRACK VIS) | STNDBY→ISR · ATRACK(VIS) |
| `!isFuji_HB_OK OR !isFOV_Valid OR !isAlvium_TempOK` | READY+WARN | none | Jog rejected if !isFOV_Valid | none |

> ⚠️ **TBD:** Alvium max operating temperature — 60°C suggested, confirm from datasheet.

---

### BDC_MWIR_STATUS_BITS

**Device:** MWIR camera + TRC capture pipeline
**Primary function:** Thermal imaging and ATRACK on MWIR
**Comms:** BDC serial → MWIR head; TRC GStreamer → MWIR

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isMWIR_Connected` | `isMWIR_DeviceReady` | No — ERROR gate |
| 1 | `isHB_OK` | `HB_MWIR_ms > 0 && < 255` | No — ERROR gate |
| 2 | `isWarmupComplete` | `MWIR_Run_State == MAIN_PROC_LOOP` | No — WARN gate |
| 3 | `isFOV_Valid` | `MWIR_FOV > 0` | No — WARN gate |
| 4 | `isCapturing` | `trcMSG.isMWIR_Capturing` | No — ERROR gate |
| 5 | `isFPA_TempOK` | `MWIR_Temp_FPA` within operating range | No — WARN gate |
| 6–7 | RES | — | — |

```
ERROR: !isMWIR_Connected OR !isHB_OK OR !isCapturing

READY+WARN: isMWIR_Connected AND isHB_OK AND isCapturing
            AND (!isWarmupComplete OR !isFOV_Valid OR !isFPA_TempOK)

READY: isMWIR_Connected AND isHB_OK AND isCapturing
       AND isWarmupComplete AND isFOV_Valid AND isFPA_TempOK
```

> **Replaces client-side:** `MWIR_PING` + `MWIR_STATUS` composite in `bdc.cs`

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| `!isMWIR_Connected OR !isHB_OK OR !isCapturing` | ERROR | No (unless both cameras lost) | →OFF (both lost or ATRACK MWIR) | ATRACK(MWIR) |
| `!isWarmupComplete OR !isFOV_Valid OR !isFPA_TempOK` | READY+WARN | none | none | none |

> ⚠️ **TBD:** FPA operating temperature range from MWIR camera datasheet.

---

### BDC_FSM_STATUS_BITS

**Device:** Fast Steering Mirror via FMC controller
**Primary function:** Fine laser beam pointing in FTRACK and COMBAT
**Comms:** BDC A1 → FMC

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isFMC_Connected` | `isFSM_DeviceReady` | No — ERROR gate |
| 1 | `isHB_OK` | `HB_FMC_ms > 0 && < 255` | No — ERROR gate |
| 2 | `isFSM_Powered` | `fmcMSG.PowerBits` — DAC powered | No — ERROR gate |
| 3 | `isNotLimited` | `isFSMNotLimited` (VoteBits2 bit 7) | No — WARN gate |
| 4 | `isAtHome` | `FSM_X_C ≈ FSM_X0 && FSM_Y_C ≈ FSM_Y0` | **Yes** — display only |
| 5–7 | RES | — | — |

```
ERROR: !isFMC_Connected OR !isHB_OK OR !isFSM_Powered

READY+WARN: isFMC_Connected AND isHB_OK AND isFSM_Powered AND !isNotLimited

READY: isFMC_Connected AND isHB_OK AND isFSM_Powered AND isNotLimited
       (isAtHome is display-only)
```

> **FSM steers the laser beam.** FSM ERROR during COMBAT → immediate STATE→ISR (laser safed).
> MODE regresses to ATRACK not OFF — coarse tracker remains viable without FSM.
> FSM ERROR also blocks ISR→COMBAT.

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| `!isFMC_Connected OR !isHB_OK OR !isFSM_Powered` | ERROR | COMBAT→ISR | FTRACK/COMBAT→ATRACK | ISR→COMBAT · FTRACK |
| `!isNotLimited` | READY+WARN | none | none | none |

> ⚠️ **TBD:** FSM home position tolerance (counts).

---

### BDC_JET_STATUS_BITS

**Device:** Jetson Orin SOM (TRC compute platform)
**Primary function:** Tracker compute and H.264 video output
**Comms:** BDC A1 → TRC; TRC sends REG1 every frame

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isConnected` | `trcMSG.isConnected` (StatusBits0 bit 1) | No — ERROR gate |
| 1 | `isReady` | `trcMSG.isReady` (StatusBits0 bit 0) | No — ERROR gate |
| 2 | `isStarted` | `trcMSG.isStarted` (StatusBits0 bit 2) | No — ERROR gate |
| 3 | `isStreaming` | `trcMSG.isStreaming` (`streamFPS > 0`) | No — WARN gate at ISR+ |
| 4 | `isCPU_OK` | `jetsonCpuLoad <= 90%` | No — ERROR gate |
| 5 | `isGPU_OK` | `jetsonGpuLoad <= 90%` | No — ERROR gate |
| 6 | `isCPU_TempOK` | `jetsonTemp <= 85°C` | No — ERROR gate |
| 7 | `isGPU_TempOK` | `jetsonGpuTemp <= 85°C` | No — ERROR gate |

> **Note on WARN thresholds:** Bits 4–7 use ERROR thresholds (90% / 85°C).
> WARN thresholds (50% load / 70°C) tracked internally by firmware — expressed via
> `DEVICE_WARN_BITS` bit 5 only. Firmware must track two threshold levels per sensor.

```
ERROR: !isConnected OR !isReady OR !isStarted
       OR !isCPU_OK OR !isGPU_OK
       OR !isCPU_TempOK OR !isGPU_TempOK

READY+WARN: all ERROR clear
            AND (!isStreaming
                OR jetsonCpuLoad 50–90%
                OR jetsonGpuLoad 50–90%
                OR jetsonTemp 70–85°C
                OR jetsonGpuTemp 70–85°C)

READY: all ERROR clear AND isStreaming (at ISR+)
       AND all loads ≤ 50% AND all temps ≤ 70°C
```

> **Replaces client-side:** `TRC_STATUS` + `TRC_TEMP_WARN` + `TRC_LOAD_ERROR` +
> `TRC_LOAD_WARN` + `TRC_LOAD_GOOD` in `bdc.cs`

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| `!isConnected OR !isReady OR !isStarted` | ERROR | ISR/COMBAT→STNDBY | →OFF (all) | STNDBY→ISR · all modes |
| `!isCPU_OK OR !isGPU_OK` | ERROR | ISR/COMBAT→STNDBY | →OFF (all) | STNDBY→ISR · all modes |
| `!isCPU_TempOK OR !isGPU_TempOK` | ERROR | ISR/COMBAT→STNDBY | →OFF (all) | STNDBY→ISR · all modes |
| `!isStreaming` at ISR+ | READY+WARN | none | none | none |
| load 50–90% OR temp 70–85°C | READY+WARN | none | none | none |

---

### BDC_INCL_STATUS_BITS

**Device:** Inclinometer (IMU / attitude sensor)
**Primary function:** Platform attitude for horizon vote computation
**Comms:** BDC serial → INCL

| Bit | Name | Source | Informational |
|-----|------|--------|---------------|
| 0 | `isConnected` | `isINC_DeviceReady` | No — ERROR gate |
| 1 | `isHB_OK` | `HB_INCL_ms > 0 && < 255` | No — ERROR gate |
| 2 | `isDataValid` | Pitch/roll within plausible range | No — ERROR gate |
| 3 | `isLevel` | Pitch/roll within operational bounds | No — WARN gate |
| 4–7 | RES | — | — |

```
ERROR: !isConnected OR !isHB_OK OR !isDataValid

READY+WARN: isConnected AND isHB_OK AND isDataValid AND !isLevel

READY: isConnected AND isHB_OK AND isDataValid AND isLevel
```

> **INCL ERROR has no state/mode consequence.** `BelowHorizVote` is the downstream gate.
> NTP and PTP ERROR are likewise informational.

| Condition | Severity | STATE change | MODE change | Blocks advancement |
|-----------|----------|-------------|-------------|-------------------|
| Any ERROR | ERROR | **none** | **none** | **none** |
| `!isLevel` | READY+WARN | none | none | none |

> ⚠️ **Note:** `HB_INCL_ms` saturates at 255ms for 1s poll (open item INCL-HB-SCALE).
> ⚠️ **TBD:** Operational attitude bounds (degrees).

---

## REG1 Byte Allocation

### MCC REG1

| New byte | Name |
|----------|------|
| ~[10] | `MCC_DEVICE_WARN_BITS` |
| ~[11] | `MCC_TMC_STATUS_BITS` |
| ~[12] | `MCC_HEL_STATUS_BITS` |
| ~[13] | `MCC_BAT_STATUS_BITS` |
| ~[14] | `MCC_CRG_STATUS_BITS` |
| ~[15] | `MCC_GNSS_STATUS_BITS` |
| ~[16] | `MCC_BDC_STATUS_BITS` |

**7 new bytes.** 249 RES bytes remain ([263–511]).

### BDC REG1

| New byte | Name |
|----------|------|
| ~[11] | `BDC_DEVICE_WARN_BITS` |
| ~[12] | `BDC_GIM_STATUS_BITS` |
| ~[13] | `BDC_VIS_STATUS_BITS` |
| ~[14] | `BDC_MWIR_STATUS_BITS` |
| ~[15] | `BDC_FSM_STATUS_BITS` |
| ~[16] | `BDC_JET_STATUS_BITS` |
| ~[17] | `BDC_INCL_STATUS_BITS` |

**7 new bytes.** 101 RES bytes remain ([411–511]).

---

## Open Items / TBDs

| # | Device | Item |
|---|--------|------|
| 1 | TMC | Cooling requirement by HW config — V2/V3·6kW vs V1/V3·3kW |
| 2 | HEL | IPGMsg StatusWord temperature bit positions — 3K and 6K full bit map needed |
| 3 | HEL | Bits 5–7 pending FC-CONSISTENCY-1 fire control review |
| 4 | BAT | MSG_BATTERY StatusWord bit map — error/alarm/protection bit definitions |
| 5 | BAT | RSOC warn threshold (suggest 20%) |
| 6 | BAT | PackTemp bounds from BMS datasheet |
| 7 | CRG | VIN threshold (V) |
| 8 | GNSS | `Heading_STDEV` threshold |
| 9 | GNSS | INS converged PosType enum values — NovAtel classification |
| 10 | GNSS | TerraStar availability — not all units equipped |
| 11 | BDC | `isVoteActive` tracking mechanism |
| 12 | GIM | MSG_GIMBAL StatusX/Y and StopCode bit map — `isFault` vs `isAtSoftLimit` |
| 13 | MWIR | FPA operating temperature range |
| 14 | FSM | Home position tolerance (counts) |
| 15 | JET | Dual WARN/ERROR threshold tracking — confirm firmware approach |
| 16 | VIS | Alvium max operating temperature — confirm from datasheet (suggest 60°C) |
| 17 | INCL | Operational attitude bounds (degrees) |
| 18 | ALL | Exact byte positions TBD — pending ICD byte map update |
| 19 | ALL | `STATUS_PREREQ_FAIL` (0x07) — new status code needed in ICD |
| 20 | ALL | `isFireExpected` — new MCC-computed HEALTH_BITS or VoteBits bit needed |

---

## Fire Control Open Item

### FC-CONSISTENCY-1 — Fire Vote Consistency Across All Subsystems

**Priority:** 🔴 High — safety-critical

**Scope:** Five subsystems must be in complete agreement on fire state at all times.
Any discrepancy between what THEIA displays, what TRC OSD shows, and what the laser
does is a safety escape.

**Subsystems to review:**
- MCC — fire vote computation, `isFireExpected`, EMON monitoring
- BDC — vote aggregation, `BDCTotalVote`, VoteBits readback
- TRC — `voteBitsMcc`, `voteBitsBdc` from REG1 [41–42], OSD reticle colour logic
- HEL — EMON vs trigger state, NOTREADY conditions, training mode behaviour
- HMI (THEIA) — vote strip display, status strip, IBIT tab vote checkboxes

**Known potential escapes:**
1. GUI shows fire-ready but laser interlocked (vote bits disagree between MCC and BDC readback)
2. TRC OSD vote display lags or mismatches MCC/BDC telemetry (voteBitsMcc/Bdc stale)
3. Laser fires without EMON confirmed in THEIA display
4. `isEMON_Unexpected` (energy without command) — not currently monitored anywhere
5. Training mode flag reaches THEIA but not TRC OSD (see TRC-TRAIN-WARN-1)
6. `isFireInterlocked` state (trigger pulled but blocked) not surfaced to operator

**Candidate addition:** A dedicated `FIRE_VOTE_BYTE` — one authoritative packed byte
in MCC REG1 and mirrored in BDC REG1 — that contains the complete fire readiness state
in a single location readable by all clients including TRC, THEIA, and ENG GUI.

**Candidate `FIRE_VOTE_BYTE` bits:**
```
bit 0  isFireExpected    — BDCTotalVote AND isLaserTotalHW AND COMBAT AND isNotBatLow AND isTriggerPulled
bit 1  isEMON            — laser energy output confirmed
bit 2  isEMON_Missing    — isFireExpected AND !isEMON
bit 3  isEMON_Unexpected — !isFireExpected AND isEMON
bit 4  isFireInterlocked — isTriggerPulled AND !isFireExpected AND !isEMON
bit 5  isTrainingMode    — power clamped to 10%
bit 6  isTriggerPulled   — operator trigger input active
bit 7  RES
```

This byte would be:
- Packed by MCC (authoritative source)
- Mirrored in BDC REG1 (for A3 clients)
- Embedded in TRC REG1 `voteBitsMcc` field (for OSD)
- Read by THEIA for main status strip and IBIT display

**Next step:** Review all five subsystems — MCC, BDC, TRC, HEL, THEIA — against this
byte definition before any implementation. Look for escapes, stale data paths,
and display/behaviour mismatches.

---

*End of CROSSBOW_DEVICE_STATUS_BITS_WORKING.md*
