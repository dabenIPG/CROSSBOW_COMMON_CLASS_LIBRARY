# CROSSBOW Device Status — Current State vs Proposed

**Document:** CROSSBOW_DEVICE_STATUS_TABLE  
**Date:** 2026-04-25  
**Purpose:** Map current DEVICE_READY sources, HMI behaviour, and client-side computation against proposed controller-level additions.

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Already at controller level — no change needed |
| ⚠️ | Client-side computation — should move to firmware |
| ➕ | New bit/byte needed in ICD |
| 🚫 | Not currently displayed / disabled |

---

## MCC Devices

| Device | Bit | DEVICE_READY Source (firmware) | IBIT Panel — Current HMI | Status Strip — Current HMI | Client-Side Computation | Proposed Change |
|--------|-----|-------------------------------|--------------------------|---------------------------|------------------------|-----------------|
| **NTP** | 0 | `ntp.isSynched` | Enabled G/R · Ready G/R/Grey · HB `00.00s` | `tss_MCC_TimeSrc` — PTP / NTP / fallback / NONE colour-coded · `tss_MCC_NTPTime` · `tss_MCC_dUTC` | ⚠️ `activeTimeSourceLabel` derived from TIME_BITS 6 flags · dUTC threshold colouring | ✅ TIME_BITS already complete — add DEVICE_WARN bit 0 when fallback active |
| **TMC** | 1 | `tmc.isConnected` | Enabled G/R · Ready G/R/Grey · HB `000ms` | `tss_status_tmc` — `COLOR_FROM_STATUS(TMC_STATUS)` | ⚠️ `TMC_STATUS` = `isTMC_DeviceReady` only — thermal/flow not checked | ➕ DEVICE_WARN bit 1 when temp or flow degraded · ➕ `isTMC_Healthy` in HEALTH_BITS |
| **HEL** | 2 | `ipg.isConnected` | Enabled G/R · Ready G/R/Grey · HB `000ms` | `tss_status_hel` — `COLOR_FROM_STATUS(HEL_STATUS)` | ⚠️ `HEL_STATUS` — sensed + HB stale + HKVoltage > 23.3 + BusVoltage > 40 (3K) + NOTREADY · model-aware logic | ➕ `isHEL_PowerValid` in HEALTH_BITS bit 6 · ➕ DEVICE_WARN bit 2 when NOTREADY or training mode |
| **BAT** | 3 | `bat.isConnected` | Enabled G/R · Ready G/R/Grey · HB `000ms` | `tss_SOC` — voltage / current / RSOC% | ✅ Raw values displayed · no threshold computation | ➕ DEVICE_WARN bit 3 when voltage approaching threshold |
| **PTP** | 4 | `ptp.isSynched` | 🚫 commented out | 🚫 none | 🚫 none — pending FW-B3 | ➕ DEVICE_WARN bit 4 when enabled but not synced · enable IBIT row when FW-B3 resolved |
| **CRG** | 5 | V1: `dbu.isConnected` · V2: GPIO OK | Enabled G/R · Ready G/R/Grey · HB `000ms` | `tss_status_charger` — VIN / IOUT text · Enabled flag | ✅ CMC CHARGE_STATUS bits displayed raw | ➕ DEVICE_WARN bit 5 when charging degraded or level reduced |
| **GNSS** | 6 | `gnss.isConnected` | Enabled G/R · Ready G/R/Grey · HB `000ms` | `tss_status_gps` — `COLOR_FROM_STATUS(GNSS_STATUS)` | ⚠️ `GPS_STATUS` = `isGNSS_DeviceReady && SIV >= 4` — threshold computed client-side | ➕ `isGPS_Valid` in HEALTH_BITS bit 5 · ➕ DEVICE_WARN bit 6 when SIV < 4 |
| **BDC** | 7 | A1 `endPacket()` result | Enabled G/R · Ready G/R/Grey · text "BDC" only — no HB | `tss_status_bdc` — `COLOR_FROM_STATUS(BDC_STATUS)` | ⚠️ `BDC_STATUS` = CommHealth (RX_HB > 10 && < 60) — timing computed client-side | ➕ DEVICE_WARN bit 7 when A1 HB degraded |

**MCC new bytes needed:**
- `DEVICE_WARN_BITS` — 1 byte, same bit positions as ENABLED/READY, placed after DEVICE_READY_BITS
- `HEALTH_BITS` bit 5 → `isGPS_Valid`
- `HEALTH_BITS` bit 6 → `isHEL_PowerValid`
- `HEALTH_BITS` bit 7 → `isTMC_Healthy`

---

## BDC Devices

| Device | Bit | DEVICE_READY Source (firmware) | IBIT Panel — Current HMI | Status Strip — Current HMI | Client-Side Computation | Proposed Change |
|--------|-----|-------------------------------|--------------------------|---------------------------|------------------------|-----------------|
| **NTP** | 0 | `ntp.isSynched` | Enabled G/R · Ready G/R/Grey · HB `00.00s` | `tss_BDC_TimeSrc` — same pattern as MCC NTP | ⚠️ Same TIME_BITS decode as MCC | ✅ TIME_BITS already complete — add DEVICE_WARN bit 0 when fallback active |
| **GIMBAL** | 1 | `gimbal.isConnected` | Enabled G/R · Ready G/R/Grey · HB `000ms` | `tss_status_gim` — `COLOR_FROM_STATUS(GIM_STATUS)` | ⚠️ `GIM_STATUS` = `isGimbal_DeviceReady && isStarted && isConnected` | ➕ DEVICE_WARN bit 1 when at soft limit or speed limited |
| **VIS (FUJI)** | 2 | `fuji.isConnected` | Enabled G/R · Ready G/R/Grey · HB_FUJI `000ms` | `tss_status_vis` — `COLOR_FROM_STATUS(VIS_STATUS)` | ⚠️ `VIS_PING` (4 fields) + `isVIS_Capturing` — composite from 5 sources | ➕ `isVIS_Ready` in HEALTH_BITS bit 2 · ➕ DEVICE_WARN bit 2 when HB elevated |
| **MWIR** | 3 | `mwir.isConnected` | Enabled G/R · Ready G/R/Grey · HB_MWIR `000ms` | `tss_status_mwir` — `COLOR_FROM_STATUS(MWIR_STATUS)` | ⚠️ `MWIR_PING` + `MAIN_PROC_LOOP` + `isMWIR_Capturing` — composite from 5 sources | ➕ `isMWIR_Ready` in HEALTH_BITS bit 3 · ➕ DEVICE_WARN bit 3 when warming up (not MAIN_PROC_LOOP) |
| **FSM (FMC)** | 4 | `fmc.isConnected` | Enabled G/R · Ready G/R/Grey · HB_FMC `000ms` | `tss_status_fmc` — `COLOR_FROM_STATUS(FMC_STATUS)` | ⚠️ `FMC_STATUS` = `isReady && HB_ms > 10 && < 30` — HB window computed client-side | ➕ DEVICE_WARN bit 4 when FSM at angular limit |
| **JETSON (TRC)** | 5 | `trc.isConnected` | Enabled G/R · Ready G/R/Grey · HB_TRC `000ms` | `tss_status_trc` — `COLOR_FROM_STATUS(TRC_STATUS)` | ⚠️ Full cascade — HB bounds + temp thresholds (70/85°C) + load thresholds (50/90%) + isStreaming — all client-side | ➕ `isTRC_Fault` in HEALTH_BITS bit 4 · ➕ `isTRC_Warn` in HEALTH_BITS bit 5 · ➕ DEVICE_WARN bit 5 when temp or load elevated |
| **INCL** | 6 | `incl.isConnected` | Enabled G/R · Ready G/R/Grey · HB `000ms` | 🚫 none | ✅ Single ready bit — no computation | ➕ DEVICE_WARN bit 6 when attitude data degraded (not ready) |
| **PTP** | 7 | `ptp.isSynched` | 🚫 not shown | 🚫 none | 🚫 none — pending FW-B3 | ➕ DEVICE_WARN bit 7 when enabled but not synced |

**BDC new bytes needed:**
- `DEVICE_WARN_BITS` — 1 byte, same bit positions as ENABLED/READY, placed after DEVICE_READY_BITS
- `HEALTH_BITS` bit 2 → `isVIS_Ready`
- `HEALTH_BITS` bit 3 → `isMWIR_Ready`
- `HEALTH_BITS` bit 4 → `isTRC_Fault`
- `HEALTH_BITS` bit 5 → `isTRC_Warn`
- `HEALTH_BITS` bit 6 → `isGimbal_AtLimit`

---

## Summary — Client-Side Computation to Move to Firmware

| # | Controller | Current Location | Computation | Proposed Bit |
|---|-----------|-----------------|-------------|--------------|
| 1 | MCC | `mcc.cs GPS_STATUS` | `isGNSS_DeviceReady && SIV >= 4` | MCC HEALTH_BITS [5] `isGPS_Valid` |
| 2 | MCC | `mcc.cs HEL_STATUS` | `HKVoltage > 23.3 && BusVoltage > 40` (3K) | MCC HEALTH_BITS [6] `isHEL_PowerValid` |
| 3 | MCC | `crossbow.cs TMC_STATUS` | thermal/flow nominal check | MCC HEALTH_BITS [7] `isTMC_Healthy` |
| 4 | BDC | `bdc.cs VIS_STATUS` | Fuji ready + HB nominal + Alvium capturing | BDC HEALTH_BITS [2] `isVIS_Ready` |
| 5 | BDC | `bdc.cs MWIR_STATUS` | MWIR ready + HB + MAIN_PROC_LOOP + capturing | BDC HEALTH_BITS [3] `isMWIR_Ready` |
| 6 | BDC | `bdc.cs TRC_LOAD_ERROR` | `jetsonCpuLoad > 90 \|\| jetsonGpuLoad > 90 \|\| temp > 85°C` | BDC HEALTH_BITS [4] `isTRC_Fault` |
| 7 | BDC | `bdc.cs TRC_LOAD_WARN` | `any load > 50% \|\| temp > 70°C` | BDC HEALTH_BITS [5] `isTRC_Warn` |
| 8 | BDC | `bdc.cs FMC_STATUS` | `HB_ms > 10 && < 30` window | BDC HEALTH_BITS [6] `isGimbal_AtLimit` (or FMC_STATUS simplify) |

---

## ICD Register Impact

### MCC REG1 — new bytes

| Byte | Name | Notes |
|------|------|-------|
| New ~[10] | `MCC DEVICE_WARN_BITS` | Parallel to ENABLED [7] and READY [8] · same bit assignments |
| HEALTH_BITS [9] bit 5 | `isGPS_Valid` | Promoted from RES |
| HEALTH_BITS [9] bit 6 | `isHEL_PowerValid` | Promoted from RES |
| HEALTH_BITS [9] bit 7 | `isTMC_Healthy` | Promoted from RES |

### BDC REG1 — new bytes

| Byte | Name | Notes |
|------|------|-------|
| New ~[11] | `BDC DEVICE_WARN_BITS` | Parallel to ENABLED [8] and READY [9] · same bit assignments |
| HEALTH_BITS [10] bit 2 | `isVIS_Ready` | Promoted from RES |
| HEALTH_BITS [10] bit 3 | `isMWIR_Ready` | Promoted from RES |
| HEALTH_BITS [10] bit 4 | `isTRC_Fault` | Promoted from RES |
| HEALTH_BITS [10] bit 5 | `isTRC_Warn` | Promoted from RES |
| HEALTH_BITS [10] bit 6 | `isGimbal_AtLimit` | Promoted from RES |

### Available Reserved Space

| Controller | Reserved bytes available | Bytes consumed by proposals |
|-----------|------------------------|----------------------------|
| MCC | 256 bytes ([256–511]) | 1 (DEVICE_WARN_BITS) |
| BDC | 108 bytes ([404–511]) | 1 (DEVICE_WARN_BITS) |

HEALTH_BITS promotions use existing reserved bits — zero wire impact on frame size.

---

*End of CROSSBOW_DEVICE_STATUS_TABLE.md*
