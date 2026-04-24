# CROSSBOW — INT_ENG Interface Control Document

**Document:** `CROSSBOW_ICD_INT_ENG`
**Doc #:** IPGD-0003
**Version:** 4.2.1
**Date:** 2026-04-19 (CB-20260419c — Jetson health compaction, GPU temp)
**Classification:** IPG Internal Use Only
**Source:** CB_ICD_v1_7.xlsx reconciled with ARCHITECTURE.md (TRC v3.0); `defines.hpp` canonical v3.X.Y
**Audience:** IPG engineering staff, ENG GUI developers, firmware developers — all five controllers (MCC, BDC, TMC, FMC, TRC)

---

## Version History

**vTBD changes (MCC V3 hardware revision — HW_REV_V3 + LASER_xK axis):**
- MCC REG1 byte [254] `HW_REV`: `0x03`=V3 (PMS Controller 1.0 Rev B — monolithic PCB, 48V or 300V battery, 3kW or 6kW laser). Tagged vTBD pending ICD version bump.
- `MCC_POWER` enum: five renames for naming convention alignment (bit positions unchanged — no wire-level breaking change). `GPS_RELAY→RELAY_GPS`, `LASER_RELAY→RELAY_LASER`, `GIM_VICOR→VICOR_GIM`, `TMS_VICOR→VICOR_TMS`. Bit 7 promoted from `RES` → `RELAY_NTP` (Phoenix NTP appliance relay — V3 only; always 0 on V1/V2).
- `MCC_POWER` V3 valid values: `RELAY_GPS(0)`, `VICOR_BUS(1)`, `RELAY_LASER(2)`, `SOL_HEL(5)`, `SOL_BDA(6)`, `RELAY_NTP(7)` (3kW); adds `VICOR_GIM(3)`, `VICOR_TMS(4)` (6kW). `VICOR_BUS` polarity inverted V1→V3 (LOW=ON → HIGH=ON). `RELAY_LASER` dispatches to pin 54 (3kW) or `PIN_ENERGIZE` pin 63 (6kW) — compile-time gated by `LASER_3K`/`LASER_6K`.
- `LASER_3K`/`LASER_6K` compile-time axis added to `hw_rev.hpp` — gates `EnablePower(RELAY_LASER)` pin dispatch. Runtime `ipg.laserModel` sense is verification only.
- `MSG_MCC.cs`: `IsV3`/`HW_REV_Label` updated; `pb_RelayNtp` property added (PowerBits bit 7, V3 only). Enum rename aliases: `pb_GpsRelay→pb_RelayGps`, `pb_LaserRelay→pb_RelayLaser`, `pb_GimVicor→pb_VicorGim`, `pb_TmsVicor→pb_VicorTms` (old names retained as backward-compat aliases).
- `0xE2 PMS_POWER_ENABLE`: V3 valid values updated — see enum table. `RELAY_NTP(7)` valid on V3 only.
- `CHARGE_LEVELS` / `0xAF SET_CHARGER`: V1/V3 (DBU3200 I2C restored on V3). V2 still returns `STATUS_CMD_REJECTED`.
- `HEALTH_BITS` byte [9] bit 4 promoted from `RES` → `isLaserModelMatch` — compile-time `LASER_xK` vs runtime `ipg.LASER_MODEL_BITS()` sense. `false` until laser connects and model confirmed. Diagnostic: `isHEL_Ready` (DEVICE_READY bit 2) true + `isLaserModelMatch` false = hardware config error; both false = laser not yet up.
- `MSG_MCC.cs`: `pb_LaserModelMatch` property added (HealthBits bit 4). Pending `frmMCC` indicator wire-up.
- Open item: `DEF-2` — TMC-specific enums (`TMC_VICORS`, `TMC_DAC_CHANNELS`, `TMC_PUMP_SPEEDS`) currently guarded by `!defined(HW_REV_V2)` in `defines.hpp` — must migrate to controller-scoped `TMC_HW_REV_V2` guard to avoid cross-controller revision flag collisions on mixed fleets.

**v4.2.1 changes (scope/target audit — whitelist-verified):**
- Command table: `INT_OPS Target` column corrected to `—` for 13 INT_ENG-scoped rows where the column was erroneously populated: `0xB1`, `0xBC`, `0xBD`, `0xE2`, `0xE8`, `0xE9`, `0xEA`, `0xF0`, `0xF4`, `0xF5`, `0xF7`, `0xFC`, `0xFE`. Firmware `EXT_CMDS_MCC[]` and `EXT_CMDS_BDC[]` whitelists verified — all 13 are correctly excluded from A3.
- `0xD1 ORIN_ACAM_COCO_ENABLE`, `0xF6 BDC_SET_FSM_TRACK_ENABLE`, `0xFA BDC_SET_STAGE_HOME`: Scope corrected `INT_OPS → INT_ENG`; `INT_OPS Target → —`. Absent from `EXT_CMDS_BDC[]` — A3 access was never implemented.
- `0xBF`, `0xEF`, `0xFF`: Description corrected — confirmed unused in firmware (no inbound handler, no outbound CMD_BYTE role). "May be outbound" note removed.
- Appendix A added — command map visualization specification (cell format, color legend, CSS classes, regeneration instruction).

**v4.2.0 changes (CB-20260419c — Jetson health compaction + GPU temp):**
- TRC REG1 Jetson health fields compacted int16 → uint8 — resolution sufficient for all values (temps 0–95°C, loads 0–100%); saves 4 bytes.
- `jetsonTemp` [45–46] int16 → `jetsonTemp` [45] uint8. Source: `thermal_zone0` millidegrees ÷ 1000.
- `jetsonCpuLoad` [47–48] int16 → `jetsonCpuLoad` [46] uint8. Source: `/proc/stat` delta.
- `jetsonGpuLoad` [57–58] int16 → `jetsonGpuLoad` [47] uint8. Moved adjacent to other health fields. Source: `/sys/devices/platform/gpu.0/load` ÷ 10. Path corrected from `/sys/devices/gpu.0/load` (was returning 0 silently).
- `jetsonGpuTemp` uint8 [48] added — NEW. GPU temp °C from `/sys/devices/virtual/thermal/thermal_zone1/temp` millidegrees ÷ 1000. Polled every 5s.
- `som_serial` uint64 [49–56] — unchanged.
- RESERVED expands [59–63] 5 bytes → [57–63] 7 bytes. Defined: 59→57, Reserved: 5→7.
- OSD bottom-right block: 2 rows → 3 rows (SN / CPU / GPU). Load and temp values colour-coded: GREEN <60 / YELLOW 60–79°C or 60–84% / RED ≥80°C or ≥85%.
- `MSG_TRC.cs`: all four jetson properties `Int16`→`byte`. `ParseMsg` updated. Header comment updated.

**v4.1.0 changes (CB-20260419 — LK tracker + COCO ops):**
- `0xDB ORIN_ACAM_ENABLE_TRACKERS`: LK (tracker_id=4) fully implemented; `mosseReseed` 3rd byte added.
- `COCO_ENABLE_OPS` enum added (0x00–0x08).
- `BDC_TRACKERS`: `LK=4` added.

**v4.0.1 changes (CB-20260416 — AWB assigned, charger V2 fix):**
- `0xC4 CMD_VIS_AWB` assigned — trigger VIS auto white balance once. No payload. BDC routes to TRC via A2; TRC binary handler calls `cam->runAutoWhiteBalance()`. ASCII equivalent: `AWB`. INT_OPS (in `EXT_CMDS_BDC[]` whitelist). Firmware: `defines.hpp` `CMD_VIS_AWB=0xC4`, `bdc.hpp` whitelist, `trc.hpp/cpp` `SET_AWB()`, `bdc.cpp` handler + serial. C#: `defines.cs` `CMD_VIS_AWB=0xC4`, `bdc.cs` `TriggerAWB()`. TRC: `udp_listener.cpp` binary handler. `ICD_CMDS` alias retired from `types.h` — all TRC code now references `ICD::` directly.
- `0xAF SET_CHARGER` description corrected: V2 hardware now supported (GPIO enable only). "V1 only — V2 returns STATUS_CMD_REJECTED" → V1: GPIO+I2C; V2: GPIO only. FW-CRG-V2 fix CB-20260416 — firmware flashed and bench-verified.

---

**v4.0.0 changes (BDC HB REG1 bytes — CB-20260413d):**
- BDC REG1 bytes [396–403] promoted from RESERVED — eight HB counter bytes. `HB_NTP` [396] in x0.1s units (÷100, C# /10.0 → seconds); [397–403] raw ms. Defined count 396→404, reserved 116→108. Tagged `v4.0.0 (BDC-HB)`.
- `MSG_BDC.cs`: eight HB properties added (`HB_NTP` double, `HB_FMC_ms`/`HB_TRC_ms`/`HB_MCC_ms`/`HB_GIM_ms`/`HB_FUJI_ms`/`HB_MWIR_ms`/`HB_INCL_ms` int). Parsed at bytes [396–403] in `ParseMSG01()`.

---

**v3.6.0 changes (ICD command space restructuring — 2026-04-12 CB-20260412):**
A block fully assigned INT_OPS — all 16 slots active commands. See `CROSSBOW_CHANGELOG.md` (IPGD-0019) CB-20260412 entry for full design rationale.

**CB-20260412 firmware version:** All five controllers target `VERSION_PACK(4,0,0)`. This is a major bump driven by the breaking command space restructuring. Clients use `FW_VERSION >> 24 >= 4` (IsV4) to gate v4.0.0 behaviour.

**FW-C10 — REG1 CMD_BYTE change:** REG1 payload byte [0] (`cmd_byte`) changes from `0xA1` (legacy `RES_A1` outbound marker) to `0x00` in v4.0.0. C# parsers must accept both `0x00` (v4.0.0+) and `0xA1` (legacy pre-FW-C10) as valid REG1 frames. The byte carries no semantic meaning — it is a framing artefact only. Applies to all five controllers.

New commands assigned:
- `0xA1 SET_HEL_TRAINING_MODE` — moved from `0xAF`. Promoted INT_ENG→INT_OPS. Safety enforced in firmware (10% power clamp), not scope.
- `0xA3 SET_TIMESRC` — new time source control. INT_OPS, all five controllers, routing by IP. Prerequisite: FW-C8 (rejection handler removal at `0xA3` first).
- `0xA9 SET_REINIT` — unified controller reinitialise. INT_OPS, MCC+BDC. Replaces `0xB0` and `0xE0`. Routing implicit by destination IP. TMC/FMC not supported.
- `0xAA SET_DEVICES_ENABLE` — unified device enable/disable. INT_OPS, MCC+BDC. Replaces `0xBE` and `0xE1`. Routing implicit by destination IP. TMC/FMC not supported.
- `0xAB SET_FIRE_VOTE` — laser fire requested vote. Moved from `0xE6`. Promoted INT_ENG→INT_OPS. Heartbeat is the safety gate.
- `0xAF SET_CHARGER` — merged charger command. INT_OPS, MCC V1 only. Replaces `0xE3` and `0xED`. Level required on every call.
- `0xB1 SET_BDC_VOTE_OVERRIDE` — moved from `0xAA`. INT_ENG. BDC engineering block.
- `0xD1 ORIN_ACAM_COCO_ENABLE` — moved from `0xDF`. INT_OPS.
- `0xE0 SET_BCAST_FIRECONTROL_STATUS` — moved from `0xAB`. INT_ENG. Internal vote sync MCC→BDC→TRC.

Scope promotions INT_ENG → INT_OPS: `0xA2` SET_NTP_CONFIG.

Retired (return `STATUS_CMD_REJECTED`, pending FW-C8 handler removal):
- `0xB0 RES_B0` — SET_BDC_REINIT superseded by `0xA9`.
- `0xBE RES_BE` — SET_BDC_DEVICES_ENABLE superseded by `0xAA`.
- `0xD2 RES_D2` — ORIN_SET_STREAM_60FPS (compile/launch time only; ASCII `FRAMERATE` covers ENG use).
- `0xD8 RES_D8` — ORIN_SET_TESTPATTERN (ASCII `TESTSRC` covers ENG use; binary handler never implemented).
- `0xDF RES_DF` — ORIN_ACAM_COCO_ENABLE moved to `0xD1`.
- `0xE1 RES_E1` — SET_MCC_DEVICES_ENABLE superseded by `0xAA`.
- `0xE3 RES_E3` — PMS_CHARGER_ENABLE merged into `0xAF SET_CHARGER`.
- `0xE6 RES_E6` — PMS_SET_FIRE_REQUESTED_VOTE moved to `0xAB SET_FIRE_VOTE`.
- `0xED RES_ED` — PMS_SET_CHARGER_LEVEL merged into `0xAF SET_CHARGER`.

Slots reassigned (previously held different commands):
- `0xA9` was PRINT_LCH_DATA (retired to BDC serial) → now SET_REINIT.
- `0xAA` was SET_BDC_VOTE_OVERRIDE → now SET_DEVICES_ENABLE.
- `0xAB` was SET_BCAST_FIRECONTROL_STATUS → now SET_FIRE_VOTE.
- `0xAF` was SET_HEL_TRAINING_MODE → now SET_CHARGER.
- `0xB1` was SET_GIM_HOME (retired — home set in gimbal FW) → now SET_BDC_VOTE_OVERRIDE.
- `0xD1` was ORIN_SET_STREAM_MULTICAST (retired) → now ORIN_ACAM_COCO_ENABLE.
- `0xE0` was SET_MCC_REINIT → now SET_BCAST_FIRECONTROL_STATUS.

`defines.hpp` / `defines.cs` enum values require update — see DEF-1. HMI/THEIA impact minimal: enum names unchanged, only numeric values change.

---

**v3.5.2 changes (FMC STM32F7 port — 2026-04-11):**
- FMC FW version bumped: `3.2.0` → `3.3.0` (`VERSION_PACK(3,3,0)`).
- FMC REG1 byte [7] **BREAKING CHANGE** — renamed `FSM STAT BITS` → `FMC HEALTH_BITS`. New layout:
  - bit 0: `isReady` (unchanged)
  - bits 1–7: `RES` — `isFSM_Powered` (was bit 1) and `isStageEnabled` (was bit 6) moved to POWER_BITS byte [46].
  - Old layout: 0:isReady; 1:isFSM_Powered; 2–5:RES; 6:isStageEnabled; 7:RES (isUnsolicitedModeEnabled — retired session 35). Read HW_REV [45] — layout identical on both revisions.
- FMC REG1 byte [45]: promoted from `RESERVED` → `HW_REV`. `0x01`=V1 (SAMD21 legacy), `0x02`=V2 (STM32F7 / OpenCR). Read before interpreting HEALTH_BITS [7] and POWER_BITS [46].
- FMC REG1 byte [46]: promoted from `RESERVED` → `FMC POWER_BITS`. bit0=`isFSM_Powered`; bit1=`isStageEnabled`; bits2–7=RES. Mirrors MCC/BDC POWER_BITS pattern.
- Defined byte count: 45 → 47. Reserved: 19 → 17. Fixed block unchanged at 64 bytes.
- `MSG_FMC.cs`: `HealthBits`/`PowerBits` properties added; `StatusBits1` retained as backward-compat alias → `HealthBits`. `HW_REV` byte [45] parsed; `IsV1`/`IsV2`/`HW_REV_Label` added. `isFSM_Power_Enabled`/`isStageEnabled` now read from `PowerBits`. `isUnsolicitedModeEnabled` retired.
- See FMC_STM32_MIGRATION_FINAL.md for full change catalogue.

**v3.5.1 changes (BDC unification — 2026-04-11):**
- BDC FW version bumped: `3.2.0` → `3.3.0` (`VERSION_PACK(3,3,0)`).
- BDC REG1 byte [10] **BREAKING CHANGE** — renamed `BDC STAT BITS` → `HEALTH_BITS`. New layout:
  - bit 0: `isReady` (unchanged)
  - bit 1: `isSwitchEnabled` — **V2 only** (=`!isSwitchDisabled`; always `0` on V1)
  - bits 2–7: `RES`
  - Old layout: 0:isReady; 1–6:RES; 7:RES (was isUnsolicitedModeEnabled — retired session 35).
- BDC REG1 byte [11] — renamed `BDC STAT BITS2` → `POWER_BITS`. **Bit layout unchanged** — rename only:
  - 0:isPidEnabled; 1:isVPidEnabled; 2:isFTTrackEnabled; 3:isVicorEnabled; 4:isRelay1En; 5:isRelay2En; 6:isRelay3En; 7:isRelay4En
- BDC REG1 bytes [392–395]: promoted from `RESERVED` — four new fields:
  - [392] `HW_REV` uint8 — `0x01`=V1, `0x02`=V2 (BDC Controller 1.0 Rev A). Read before interpreting HEALTH_BITS bit 1.
  - [393] `TEMP_RELAY` int8 — relay area NTC thermistor °C (V2 live; V1 always `0x00`)
  - [394] `TEMP_BAT` int8 — battery-in area NTC thermistor °C (V2 live; V1 always `0x00`)
  - [395] `TEMP_USB` int8 — USB 5V area NTC thermistor °C (V2 live; V1 always `0x00`)
  - Bytes [396–511] — RESERVED at time of v3.5.1. Defined count: 392 → 396. Reserved: 120 → 116. Note: bytes [396–403] subsequently promoted in v4.0.0 (BDC-HB).
- `MSG_BDC.cs`: `HealthBits`/`PowerBits` properties added; `StatusBits`/`StatusBits2` retained as backward-compat aliases. `HW_REV` byte [392] parsed; `IsV1`/`IsV2`/`HW_REV_Label` added. `isSwitchEnabled` accessor added (V2-aware). `TEMP_RELAY`/`TEMP_BAT`/`TEMP_USB` properties added.
- See BDC_HW_DELTA.md for full change catalogue.

**v3.5.0 changes (IPG 6K integration Phase 1 — ENG GUI direct TCP — 2026-04-10):**
- `LASER_MODEL` enum added to `defines.hpp` and `defines.cs`: `UNKNOWN=0x00`, `YLM_3K=0x01`, `YLM_6K=0x02`. Inline function `LASER_MAX_POWER_W()` added to `defines.hpp`. `LaserModelExt` static class (`MaxPower_W()`, `IsSensed()`, `Label()`) added to `defines.cs`.
- `DEBUG_LEVELS` enum added to `defines.cs` — was present in `defines.hpp` only. Now parallel.
- `BDC_TRACKERS.KALMAN=3` added to `defines.cs` — was present in `defines.hpp` only. Now parallel.
- MCC REG1 byte [255]: `RESERVED (0x00)` → **`LASER_MODEL`** — stores laser model sensed via `RMN` command on connect. `0x00`=UNKNOWN/not yet sensed; `0x01`=YLM_3K; `0x02`=YLM_6K. Backwards compatible — old C# clients reading `0x00` see UNKNOWN which is a safe default (they were reading reserved zero before). See enum table below.
- `MSG_IPG.cs` extended:
  - `PowerSetting_W` — hardcoded `* 3000` replaced with `SetPoint / 100.0 * MaxPower_W` (model-aware via `LaserModel`).
  - New properties: `LaserModel` (`LASER_MODEL`, `public set`), `ModelName` (`string`), `SerialNumber` (`string`), `IsSensed` (derived), `MaxPower_W` (derived), `IsEMON` (model-aware bit decode), `IsNotReady` (model-aware bit decode).
  - `ParseDirect(string cmd, string payload)` method added — handles ASCII TCP response path from ENG GUI direct connection. Handles: `RMN` (model sense), `RSN`, `RHKPS`, `RBSTPS`, `RCT`, `STA`, `RMEC`, `RCS`/`SDC`/`SCS`, `ROP`.
  - `GetBit(uint word, int bit)` static helper added.
- ENG GUI `HEL` window (`hel.cs`, `frmHEL.cs`, `frmHEL_Designer.cs`) — complete rewrite:
  - Transport: `UdpClient` port 10011 → `TcpClient`/`NetworkStream` port 10001 (both lasers use TCP on port 10001).
  - Auto-sense: `RMODEL` and `RMN` both sent on connect. `RMODEL` returns model string on 3K (e.g. `YLM-3000-SM-VV`); `RMN` returns model string on 6K (e.g. `YLM-6000-U3-SM`) and hostname on 3K (ignored — no `-` delimiter). `TrySenseModel()` in `ParseDirect()` handles both paths. `RSN` follows for serial number.
  - Periodic poll loop (20 ms) — mirrors firmware `POLL()` p1 state machine. Model-conditional: `RHKPS`/`RBSTPS` 3K only; `SDC` (6K) vs `SCS` (3K) for set power.
  - Full `STA`/`ERR` bit decode with `mb_` (`CodeArtEng.Controls.StatusLabel`) controls — model-aware labels updated on sense via `UpdateModelLabels()`.
  - 3-column layout matching `frmTMC` (1320×854). IP/Port text inputs. `EMON`/`EMOFF` buttons gated on `IsSensed`.
  - All parse logic centralised in `MSG_IPG.ParseDirect()`.
  - `Stop()` nulls `_tcp`/`_stream` to prevent `SocketException` on reconnect.
- `0xAF SET_HEL_TRAINING_MODE` — was `RES_AF` (reserved stub). `uint8 0=COMBAT, 1=TRAINING`. Sets `ipg.isTrainingMode` — power clamped to 10% when training. `INT_ENG` only.
- MCC serial commands added: `HEL` (status dump), `HELPOW <0-100>` (set power), `HELCLR` (clear errors), `HELTRAIN <0|1>` (training mode). All gated on `isHEL_Valid()`.
- Firmware `ipg.hpp`/`ipg.cpp` rewritten: UDP→TCP, auto-sense, model-conditional poll, `LASER_MODEL model_type`, `isSensed()`, `isResponding()`, `isEMON()` model-aware switch, `isTrainingMode`, `lastMsgRx_ms`/`HB_RX_ms` liveness tracking, `parseLine()`/`trySenseModel()` helpers.
- MCC REG1 byte [131] `HEL HB` — was always 0 (stub). Now packs `ipg.HB_RX_ms / 100` (laser TCP response interval in x0.1s units). `HEL_STATUS` in `mcc.cs` uses this for liveness — stale > 2.0s → ERROR. BAT/CRG/GNSS HB counters remain 0 pending subsystem timing implementation.
- `mcc.cpp`: byte [255] packed as `LASER_MODEL_BITS()`; `HEALTH_BITS()` bit 3 = `isTrainingMode`; `StateManager()` COMBAT gate on `isHEL_Valid()`; `0xAF` handler sets `isTrainingMode` + applies power immediately; `HELTRAIN` serial command moved to MCC-level (out of HEL section).

**v3.4.0 changes (MCC unification — 2026-04-08):**
- MCC unified hardware abstraction (`hw_rev.hpp`) — V1/V2 hardware revision support. See MCC_HW_DELTA.md.
- MCC FW version bumped: `3.2.0` → `3.3.0` (`VERSION_PACK(3,3,0)`).
- MCC REG1 byte [9] **BREAKING CHANGE** — renamed `MCC STAT BITS` → `HEALTH_BITS`. New 3-bit layout:
  - bit 0: `isReady` (unchanged)
  - bit 1: `isChargerEnabled` — **was bit 4**
  - bit 2: `isNotBatLowVoltage` — **was bit 5**
  - bit 3: `isTrainingMode` — **added v3.5.0** — MCC HEL training mode flag
  - bits 4–7: `RES` — solenoid bits (1–2) and laser bit (3) moved to `POWER_BITS` byte [10].
- MCC REG1 byte [10] **BREAKING CHANGE** — renamed `MCC STAT BITS2` → `POWER_BITS`. Bit N = `MCC_POWER` enum value N. Revision-independent decode — unused outputs for active revision always `0`:
  - bit 0: `isPwr_GpsRelay` (V1 live | V2 always 0)
  - bit 1: `isPwr_VicorBus` (V1 live | V2 always 0)
  - bit 2: `isPwr_LaserRelay` (both revisions)
  - bit 3: `isPwr_GimVicor` (V1 always 0 | V2 live)
  - bit 4: `isPwr_TmsVicor` (V1 always 0 | V2 live)
  - bit 5: `isPwr_SolHel` (V1 live | V2 always 0)
  - bit 6: `isPwr_SolBda` (V1 live | V2 always 0)
  - bit 7: `RES`
- MCC REG1 byte [254]: reassigned from `RESERVED` → `HW_REV` (`0x01`=V1, `0x02`=V2). Allows `MSG_MCC.cs` to self-detect register layout without out-of-band configuration. Read before interpreting bytes [9] and [10].
- `0xE2 PMS_SOL_ENABLE` → **`PMS_POWER_ENABLE`** — unified power output command. Payload: `uint8 which (MCC_POWER enum); uint8 0/1`. INT_ENG only on both revisions. V1 valid: 0–2, 5–6. V2 valid: 2–4. Invalid `which` for active revision returns `STATUS_CMD_REJECTED`.
- `0xE4 PMS_RELAY_ENABLE` → **`RES_E4` RETIRED** — use `0xE2 PMS_POWER_ENABLE` with `GPS_RELAY=0` or `LASER_RELAY=2`. Returns `STATUS_CMD_REJECTED`.
- `0xEC PMS_VICOR_ENABLE` → **`RES_EC` RETIRED** — use `0xE2 PMS_POWER_ENABLE` with `VICOR_BUS=1`, `GIM_VICOR=3`, or `TMS_VICOR=4`. Returns `STATUS_CMD_REJECTED`.
- `0xED PMS_SET_CHARGER_LEVEL` — V2 now returns `STATUS_CMD_REJECTED` (no charger I2C on V2 hardware).
- `MCC_POWER` enum added (see enum tables). `MCC_SOLENOIDS`, `MCC_RELAYS`, `MCC_VICORS` removed — superseded by `MCC_POWER`.
- `SEND_FIRE_STATUS` gate updated: `isSolenoid2_Enabled` → `isPwr_LaserRelay && isBDC_Ready` (both revisions).
- `MSG_MCC.cs`: `HealthBits`/`PowerBits` properties added; `StatusBits`/`StatusBits2` retained as backward-compat aliases. `HW_REV` byte [254] parsed; `IsV1`/`IsV2`/`HW_REV_Label` added. Seven `pb_*` accessors added for `POWER_BITS`. Old `isVicor_Enabled`/`isRelay1/2_Enabled` retained as revision-aware compat aliases. `isReady` property added (was missing).

**v3.3.9 changes (session 30 — 2026-04-07):**
- TMC unified hardware abstraction (`hw_rev.hpp`) — V1/V2 hardware revision support. See TMC_HW_DELTA.md.
- TMC FW version bumped: `3.2.3` → `3.3.0` (`VERSION_PACK(3,3,0)`).
- TMC REG1 byte [7] `STAT BITS1` updated:
  - bit 1: `isPumpEnabled (V1)` | `isPump1Enabled (V2)` — same wire value, different hardware
  - bit 5: `RES (V1)` | `isPump2Enabled (V2)` — **wire format differs by revision**
  - bit 6: `isSingleLoop` — compile-time constant; `1` = single loopback coolant circuit, `0` = dual independent loops. Always present regardless of revision.
  - bit 7: `RES` (unchanged — `isUnSolicitedEnabled` retired session 35)
- TMC REG1 byte [17–18] `Pump Speed`:
  - V1: live DAC counts [0–800] (Vicor trim for pump motor voltage)
  - V2: always `0x0000` reserved (TRACO PSUs are fixed-voltage on/off only)
- TMC REG1 bytes [39–40] `tv3`/`tv4`:
  - V1: live int8 °C (Vicor heater temp / Vicor pump temp via ADS1015)
  - V2: always `0x00` reserved (ADS1015 chips removed; heater hardware removed)
- TMC REG1 byte [62]: reassigned from `RESERVED` → `HW_REV` (`0x01`=V1, `0x02`=V2). Allows `MSG_TMC.cs` to self-detect register layout without out-of-band configuration.
- `TMS_SET_VICOR_ENABLE (0xE9)` payload range updated: V1 `(0–3)`, V2 `(0–2, 4)`. `PUMP2=4` added for V2 independent TRACO PSU control.
- `TMC_VICORS` enum: `PUMP1=2` (V2 alias for `PUMP=2`), `PUMP2=4` added.
- New serial commands added to TMC: `PUMP` (status + control, HW_REV-aware), `PIDGAIN` (runtime PID gain read/set).
- `SINGLE_LOOP` build flag documented in `hw_rev.hpp` — gates PID input selection for single loopback plumbing.
- `MSG_TMC.cs`: `IsV1`/`IsV2`/`HW_REV_Label`, `isPump1/2Enabled`, `isSingleLoop`, `PumpSpeedValid`, `Tv3Tv4Valid` added.

**v3.3.8 changes (session 29 — 2026-04-06):**
- Firmware replay window fix — all six A2/A3 frame handlers: new client detection (`isNewClient` + `a_seq_init = false`) moved **before** `frameCheckReplay()`. Reconnecting clients no longer permanently locked out after slot expiry. Affected: MCC `handleA2Frame`, MCC `handleA3Frame`, BDC `handleA2Frame`, BDC `handleA3Frame`, TMC `handleA2Frame`, FMC `handleA2Frame`.
- C# client model standardized fleet-wide (`mcc.cs`, `bdc.cs`, `tmc.cs`, `fmc.cs`): registration burst (`0xA4 ×3`) retired — single `0xA4` on connect. `_lastKeepalive` updated only in `SendKeepalive()`. Any valid frame (any CMD_BYTE) updates `isConnected`/`lastMsgRx` — not just `0xA1`. `connection established` logged immediately in receive loop. Redundant elapsed check removed from `KeepaliveLoop`. A3 auto-subscribe removed — user controls via checkbox. See ARCHITECTURE.md §4.2 for authoritative standard.
- ENG GUI: dt/HB/RX consolidated display format `XXX dt:  000.00 <avg> [max] unit` with EMA α=0.10 on all four controller tabs. `frmFMC.cs` fully updated to match MCC/BDC/TMC pattern.
- FMC `fmc.cs`: `isConnected`, `_wasConnected`, `_connectedSince`, `_dropCount` state added — was missing entirely. Connection tracking now matches other controllers.
- FW version: all four controllers bumped to v3.2.3.

---

**v3.3.7 changes (session 37 — 2026-04-05):**
- EXT_OPS port references updated throughout: `UDP:10009` → `UDP:15009`. HYPERION sensor
  input ports added to network reference: `15001` (aRADAR), `15002` (aLORA). IP assignment
  note added — THEIA `.208` and HYPERION `.206` are IPG reference defaults, operator-configurable.
- Tier overview table and diagram updated to reflect 15009.
- Scope definitions table: EXT_OPS entry updated to `UDP:15009`.

---

> **Version policy:** Major version tracks ICD breaking changes and is shared with `defines.hpp`.
> Minor and patch versions may increment independently per subsystem. `defines.hpp` header reads `3.X.Y`.

> **Document scope:** This document owns command bytes and register layouts only. Network
> topology, framing protocol, IP range policy, and client access model are in
> **ARCHITECTURE.md** (IPGD-0006).

---

## Version History

**v3.3.6 changes (session 35/36 — 2026-04-04):**
- `0xA4` renamed `EXT_FRAME_PING` → `FRAME_KEEPALIVE`; semantics extended to A2 and all controllers. Empty payload = ACK only. Payload `{0x01}` = solicited REG1 return (rate-gated 1 Hz per slot; suppressed if `wantsUnsolicited=true`).
- `0xA1 GET_REGISTER1` **retired inbound** — returns `STATUS_CMD_REJECTED`. `0xA1` remains the outbound `CMD_BYTE` in all REG1 unsolicited frames.
- `0xA3 GET_REGISTER3` **retired** — returns `STATUS_CMD_REJECTED`.
- `0xA0 SET_UNSOLICITED` updated — now sets per-slot `wantsUnsolicited` flag on the sender's `FrameClient` slot. `isUnSolicitedEnabled` global flag retired across all controllers.
- `STATUS_BITS` bit 7 (`isUnsolicitedModeEnabled`) **retired** on all four controllers (MCC byte 9, BDC byte 10, TMC byte 7, FMC byte 7) — always `0`. C# callers must not read this bit.
- `GetCurrentTime()` holdover rewrite across all controllers — `EPOCH_MIN_VALID_US = 1577836800000000` guard, `_lastGoodTimeUs`/`_lastGoodStampUs` latch; free-runs from latch when both PTP and NTP invalid; `activeTimeSource = NONE` during holdover.
- `isPTP_Enabled` defaults to `false` on all controllers (FW-B3 deferred). `isNTP_Enabled` defaults to `false` on FMC (SAMD21 open bug).
- FMC serial: `A1 ON|OFF` command added; A1 ARP backoff added.
- All C# client classes: single `FRAME_KEEPALIVE` on connect (burst retired session 29 — firmware replay fix makes it unnecessary); keepalive sends `0xA4` every 30 s; any valid frame updates liveness (not just `0xA1`); `connection established` logged immediately on first receive. See ARCHITECTURE.md §4.2 for authoritative C# client connect standard.
- `MSG_BDC.cs`: property naming aligned (`_DeviceEnabled`/`_DeviceReady`; `activeTimeSourceLabel`; `TEMP_MCU`; `FW_VERSION_STRING`).
- ENG GUI: BDC and FMC tabs updated with GUI-5 (rolling max, RX staleness, gap counter, dUTC).
- FMC PTP integration complete — `fmc.hpp`, `fmc.cpp` updated; NEW-38c closed
- FMC is SAMD21 (Cortex-M0+); `SerialUSB` throughout; `uprintf()` for formatted output
- FMC REG1 byte 28 epoch field: `NTP epoch Time` → `epoch Time (PTP/NTP)`
- FMC REG1 byte 7 `FSM STAT BITS` bits 2–3: `ntp.isSynched/ntpUsingFallback` → `RES` (moved to TIME_BITS byte 44 — consistent with other controllers)
- FMC REG1 byte 44: `RESERVED (20)` → `TIME_BITS (1)` + `RESERVED (19)`
- `TIME_BITS` layout identical across all controllers: MCC (byte 253), BDC (byte 391), TMC (STATUS_BITS3 byte 61), FMC (byte 44)
- `TIME_BITS` layout now identical across all controllers: MCC (253), BDC (391), TMC (61), FMC (44)
- FMC serial commands added: `TIME`, `TIMESRC <PTP|NTP|AUTO|OFF>`, `PTPDEBUG <0-3>`
- FMC NTP IP corrected: was hardcoded `192.168.1.8` (eng IP — do not use); now `IP_NTP_BYTES` (`.33`)
- FMC socket budget: udpA1(1) + udpA2(1) + PTP udpEvent:319(1) + PTP udpGeneral:320(1) = 4/8
- BDC socket fix: TRC and FMC drivers previously opened own sockets (9 total, exceeding W5500 8-socket limit, blocking PTP init); now borrow `udpA2` via pointer — 0 sockets consumed

**v3.3.4 changes (session 32 — 2026-04-04):**
- BDC PTP integration complete — `bdc.hpp`, `bdc.cpp`, `MSG_BDC.cs` updated; `defines.hpp` updated
- `BDC_DEVICES::RTCLOCK = 7` renamed to `BDC_DEVICES::PTP = 7` in `defines.hpp`
- `IP_GNSS_BYTES` (192.168.1.30) added to `defines.hpp`; all hardcoded GNSS IPs replaced across MCC/TMC/BDC
- `0xB0 SET_BDC_REINIT` payload: `7=RTC` → `7=PTP`
- `0xBE SET_BDC_DEVICES_ENABLE` payload: `7=RTC` → `7=PTP`
- BDC REG1 byte 8 `DEVICE_ENABLED_BITS` bit 7: `RES` → `isPTP_Enabled (BDC_DEVICES::PTP)`
- BDC REG1 byte 9 `DEVICE_READY_BITS` bit 7: `RES` → `ptp.isSynched`
- BDC REG1 byte 10 `STAT BITS` bits 1–2: `ntpUsingFallback/ntpHasFallback` → `RES` (moved to TIME_BITS byte 391)
- BDC REG1 byte 12 epoch field: `NTP epoch Time` → `epoch Time (PTP/NTP)` — routes through `GetCurrentTime()`
- BDC REG1 byte 391: `RESERVED (121)` → `TIME_BITS (1)` + `RESERVED (120)`
- MCC REG1 byte 10 `STAT BITS2` bits 0–2: `ntpUsingFallback/ntpHasFallback/usingPTP` → `RES` (moved to TIME_BITS byte 253)
- MCC REG1 byte 253: `RESERVED (3)` → `TIME_BITS (1)` + `RESERVED (2)`
- `TIME_BITS` layout identical across MCC (byte 253), BDC (byte 391), TMC (STATUS_BITS3 byte 61): bit0=isPTP_Enabled, bit1=ptp.isSynched, bit2=usingPTP, bit3=ntp.isSynched, bit4=ntpUsingFallback, bit5=ntpHasFallback
- BDC serial command reference added: `TIME`, `TIMESRC`, `PTPDEBUG`, `REINIT <0-7>`, `ENABLE <dev> <0|1>`
- MCC RE_INIT_DEVICE bug fixed: `MCC_DEVICES::PTP` case was missing — `REINIT 4` was a no-op; now correctly re-inits PTP
- `MSG_BDC.cs`: `TimeBits` property added (byte 391), `tb_*` accessors, `isPTP_Enabled/isPTP_DeviceReady`, `epochTime`, `activeTimeSource`; `ntpUsingFallback/ntpHasFallback/usingPTP` redirected to `TimeBits`
- `MSG_MCC.cs`: `TimeBits` property added (byte 253), `tb_*` accessors; `ntpUsingFallback/ntpHasFallback/usingPTP` redirected from `StatusBits2` to `TimeBits`
- BDC boot sequence: `PTP_INIT(1s)` step added between `NTP_INIT` and `DONE`; total boot ~21s
- Action item NEW-38b closed
- TMC PTP integration complete — `tmc.hpp`, `tmc.cpp`, `MSG_TMC.cs` updated
- TMC REG1 byte 61 reassigned from RESERVED to `TMC STAT BITS3` — PTP+NTP time status
- TMC STAT BITS1 bits 5/6 vacated: `isNTPSynched` moved to BITS3 bit 3, `ntpUsingFallback` moved to BITS3 bit 4
- TMC STAT BITS3 layout: bit0=`isPTP_Enabled`, bit1=`isPTP_Synched`, bit2=`usingPTP`, bit3=`isNTPSynched`, bit4=`ntpUsingFallback`, bit5=`ntpHasFallback`
- TMC serial commands added: `TIME`, `TIMESRC <PTP|NTP|AUTO>`, `PTPDEBUG <0-3>`
- TMC epoch field (bytes 9–16) now routes through `GetCurrentTime()` — PTP when synched, NTP otherwise
- `MSG_TMC.cs`: `TmcReg1.StatBits3` added, `STATUS_BITS3` property + full accessor set, `epochTime`/`activeTimeSource`/`activeTimeSourceLabel` added
- TMC FW version: `VERSION_PACK(3,0,5)` (session 30 integration)

**v3.3.2 changes (session 30 — 2026-04-04):**
- FW-1 closed: `PTPDEBUG <0-3>` serial command implemented and verified
- `REINIT <device>` serial command added — mirrors `0xE0 SET_MCC_REINIT`
- `ENABLE <device> <0|1>` serial command added — mirrors `0xE1 SET_MCC_DEVICES_ENABLE`
- `MCC_DEVICES::RTCLOCK = 4` renamed to `PTP = 4` in `defines.hpp` and `defines.cs`
- `0xE0`/`0xE1` payload descriptions corrected: `0=NTP` (NTP only), `4=PTP` (PTP only) — previously `0=NTP/PTP` was misleading
- `frmMCC.cs`: `MCC_DEVICES.RTCLOCK` → `MCC_DEVICES.PTP` in EnableDevice call
- `MCC_SOLENOIDS` and `MCC_RELAYS` enums added to `defines.cs` (mirrors `defines.hpp`)

**v3.3.1 changes (session 29 — 2026-03-28):**
- NEW-36 closed: PTP HW verified — `TIME` command confirmed `offset_us=12`, `active source: PTP`, correct date
- NEW-37 closed: `MSG_MCC.cs` updated and ENG GUI verified — `epochTime`, `activeTimeSource`, `isPTP_DeviceReady`, `usingPTP` all working
- `PTPMODE ENABLE` corrected to `PTPMODE ENABLE_FINETIME` throughout
- `0xE0 SET_MCC_REINIT` / `0xE1 SET_MCC_DEVICES_ENABLE` payload updated: device index 4 = `PTP/NTP` (was `RTCLOCK`)
- MCC serial command reference added: `TIME`, `TIMESRC`, `PTPDEBUG` (pending FW-1)
- `MSG_MCC.cs` session 28/29 C# additions documented: `TIME_SOURCE` enum, `epochTime`, `activeTimeSource`, `activeTimeSourceLabel`, `isPTP_DeviceEnabled/Ready`, `ntpUsingFallback`, `ntpHasFallback`, `usingPTP`
- `SEND_REG_01` bug fixed: epoch time field now calls `GetCurrentTime()` (was `ntp.GetCurrentTime()` — ENG GUI was receiving wrong time when PTP active)

**v3.3.0 changes (session 28 — 2026-03-28):**
- MCC FW version updated: `3.0.6` → `3.1.0` (PTP integration)
- MCC REG1 byte 7 `DEVICE_ENABLED_BITS` bit 4 updated: `RES` → `isPTP_Enabled`
  - PTP slave is MCC-managed device slot 4; `0xE0 SET_MCC_REINIT` and `0xE1 SET_MCC_DEVICES_ENABLE` device index 4 (was `RTCLOCK`) now controls PTP+NTP together
- MCC REG1 byte 8 `DEVICE_READY_BITS` bit 4 updated: `RES` → `isPTP_Ready` (`ptp.isSynched`)
- MCC REG1 byte 10 `STAT BITS2` bit 2 updated: `RES` → `usingPTP` — set when PTP is the active time source
- Time source hierarchy: PTP (GNSS .30 IEEE 1588 grandmaster) → NTP primary (.33) → NTP fallback (.208)
- `GetCurrentTime()` routing method added to MCC — returns PTP time when synched, NTP otherwise
- GNSS receiver (.30) PTP configuration validated: `PTPMODE ENABLE_FINETIME`, `PTPTIMESCALE UTC_TIME`, `SAVECONFIG`; state=MASTER, offset=0.000ns, FINESTEERING confirmed, `offset_us=12µs` on MCC
- NEW-37: `MSG_MCC.cs` — unpack PTP bits (see Action Items)
- NEW-38: Propagate PTP to BDC, TMC, FMC, TRC (see Action Items)

**v3.2.0 changes (session 27 — 2026-03-26):**
- `0xA2 GET_REGISTER2` deprecated stub replaced with `0xA2 SET_NTP_CONFIG` (INT_ENG only — not on EXT_OPS whitelist)
  - 0 bytes: force resync on current server
  - 1 byte `[p]`: set primary NTP server last octet to `p`, resync
  - 2 bytes `[p, f]`: set primary to `p` and fallback to `f`, resync
- MCC REG1 byte 10 `STAT BITS2` updated: bits 0/1 now carry NTP fallback state (replaces RES)
  - bit 0: `ntpUsingFallback` — system is currently syncing from fallback server
  - bit 1: `ntpHasFallback` — a fallback server is configured
- BDC REG1 byte 10 `STAT BITS` updated: bits 1/2 now carry NTP fallback state (replaces RES)
  - bit 1: `ntpUsingFallback`
  - bit 2: `ntpHasFallback`
- TMC REG1 byte 7 `STAT BITS1` bit 6 updated: `ntpUsingFallback` (was RES/isRTCInit)
- FMC REG1 byte 7 `FSM STAT BITS` bits 2/3 updated (were RES):
  - bit 2: `ntpSynched` — NTP is synchronised (explicit; no DEVICE_READY_BITS on FMC)
  - bit 3: `ntpUsingFallback`
- NTP server defaults clarified: `.33` = HW Stratum 1 primary; `.208` = Windows HMI fallback; `.8` = eng IP, do not use as NTP
- NTP auto-recovery documented: 3 missed responses → fallback; 2-minute primary retry; latches on primary when it responds
- `NTP_STALE_MISSES` constant (= 3) added to `ntpClient.hpp` — governs primary→fallback switchover

**v3.1.0 changes (session 22 — 2026-03-16):**
- Document title updated: `CROSSBOW ICD` → `CROSSBOW — INT_ENG Interface Control Document`
- Doc number assigned: IPGD-0003
- Classification added: IPG Internal Use Only
- TRC3 renamed to TRC throughout (ticket references TRC3-xx preserved)
- THEIA IP updated throughout: 192.168.1.8 → 192.168.1.208
- NTP IP updated in Network Addresses: 192.168.1.8 → 192.168.1.33
- PixelShift corrected: −20 px → −420 px
- Column headers `TRC3 ASCII` / `TRC3 Binary` → `TRC ASCII` / `TRC Binary`
- Tier Overview section added — full A1/A2/A3 model with complete node table
- Full Network Reference section added — all node IPs
- Relationship to INT_OPS and EXT_OPS section added
- Cross-references updated to use IPGD doc numbers throughout

**v3.0.3 changes (session 20 — 2026-03-16):**
- `## Video Stream` section added. Documents H.264 RTP stream from TRC: port 5000,
  1280×720, 60 fps, 10 Mbps fixed, UDP RTP payload=96. Unicast configuration (current
  production), multicast configuration (pending `0xD1`), framerate control (pending
  `0xD2`), GStreamer receive pipeline, known quirks (PixelShift −420 px, explicit
  resolution requirement, display timer).

**v3.0.2 changes (session 17):**
- Scope label rename applied (NEW-13): `EXT` → `INT_OPS` throughout all command tables
  and scope definitions. `INT` → `INT_ENG` throughout. Column headers renamed to
  `INT_ENG Target` / `INT_OPS Target`. New label `EXT_OPS` added — covers external
  integration interface documented in `CROSSBOW_ICD_EXT_OPS` (IPGD-0005).
- v3.0.0 changelog: corrected `EXT_FRAME_PING (EXT scope)` → `(INT_OPS scope)`.

**v3.0.1 changes (session 16):**
- `## Network Architecture and Framing Protocol` section removed — content moved to
  ARCHITECTURE.md §2–§6 (IPGD-0006). Replaced with single cross-reference section
  retaining STATUS byte codes, fixed payload layout, and 0xA4 ping response detail.

**v3.0.0 changes (session 15 — initial release):**
- ICD renumbered to v3.0.0. `defines.hpp` stamped as authoritative v3.X.Y canonical source.
- `0xA4` promoted from `RES_A4` to `EXT_FRAME_PING` (INT_OPS scope).
- `0xE0`/`0xE1` device 7: `reserved` → `BDC`.
- `TMC_PUMP_SPEEDS` corrected: LO=350, MED=500, HI=800.
- `defines.hpp` inline comment sweep: 7 commands corrected.
- 7 new enumerations added to `defines.hpp` and `defines.cs`.
- `defines.cs` canonical C# equivalent created — namespace `CROSSBOW`.

**v1.7.3 changes (session 14):**
- `0xE7 TMS_INPUT_FAN_SPEED` payload corrected: `0=off, 128=low, 255=high`.
- `SYSTEM_STATES` enum corrected: `MAINT = 0x04`, `FAULT = 0x05`.

**v1.7.2 changes (session 14):**
- A1 Stream Rates table added.
- Fire Control Vote rate corrected: 200 Hz → 100 Hz.

**v1.7.1 changes (session 13):**
- Known device IP table expanded to full network.
- NTP architecture documented.

---

## Network and Interface Tier Overview

CROSSBOW uses a three-tier interface model. INT_ENG provides full access to all tiers.

```
┌─────────────────────────────────────────────────────────────────┐
│  A1 — Controller Bus (port 10019, magic 0xCB 0x49)             │
│  Always-on unsolicited telemetry — sub → upper controller       │
│                                                                 │
│  TMC (.12) → MCC (.10)    100 Hz                               │
│  FMC (.23) → BDC (.20)     50 Hz                               │
│  TRC (.22) → BDC (.20)    100 Hz                               │
│  MCC (.10) → BDC (.20)    100 Hz  (fire control vote 0xAB)     │
│  BDC (.20) → TRC (.22)    100 Hz  (fire status, raw 5B)        │
├─────────────────────────────────────────────────────────────────┤
│  A2 — Engineering Interface (port 10018, magic 0xCB 0x49)      │
│  Bidirectional — ENG GUI ↔ all 5 controllers                   │
│  Up to 4 simultaneous clients. 60s liveness timeout.           │
│  BDC also uses A2 to issue commands to TRC.                     │
│  Auth/service pathway planned (future).                         │
│                                                                 │
│  MCC (.10)  BDC (.20)  TMC (.12)  FMC (.23)  TRC (.22)         │
│  GIMBAL (.21)  HEL (.13)  GNSS (.30)                           │
│  NTP (.33)  RPI/ADSB (.31)  LoRa (.32)  RADAR (.34)            │
└───────────────────────┬─────────────────────────────────────────┘
                        │ A3 boundary
┌───────────────────────▼─────────────────────────────────────────┐
│  A3 — INT_OPS — Tier 1 (port 10050, magic 0xCB 0x58)           │
│  THEIA and vendor HMI — MCC + BDC only via A3                  │
│  Up to 2 simultaneous clients. 60s liveness timeout.           │
│                                                                 │
│  THEIA (.208)  Vendor HMI (.210–.254)                          │
└───────────────────────┬─────────────────────────────────────────┘
                        │ EXT_OPS boundary
┌───────────────────────▼─────────────────────────────────────────┐
│  EXT_OPS — Tier 2 (UDP:15009, magic 0xCB 0x48)                 │
│  CUE input — HYPERION or third-party providers                  │
│                                                                 │
│  HYPERION (.206)  Third-party (.210–.254)                       │
└─────────────────────────────────────────────────────────────────┘
```

| Tier | Port | Magic | Nodes Accessible | Audience |
|------|------|-------|-----------------|----------|
| A1 — Controller Bus | 10019 | `0xCB 0x49` | Sub→upper (internal only) | Controller firmware only |
| A2 — Engineering | 10018 | `0xCB 0x49` | All 5 controllers | IPG engineering — ENG GUI, firmware |
| A3 — INT_OPS — Tier 1 | 10050 | `0xCB 0x58` | MCC, BDC only | THEIA, vendor HMI — see IPGD-0004 |
| EXT_OPS — Tier 2 | 15009 | `0xCB 0x48` | THEIA / HYPERION | CUE providers — see IPGD-0005 |

> A3 enforces IP-based access control. `.1–.99` → A1/A2 only. `.200–.254` → A3 only.
> `.100–.199` → reserved, silently dropped.

---

## Full Network Reference

| Node | IP | Role | Connected To |
|------|----|------|-------------|
| MCC | 192.168.1.10 | Master Control Computer | A1→BDC, A2↔ENG GUI, A3↔THEIA |
| TMC | 192.168.1.12 | Thermal Management Controller | A1→MCC, A2↔ENG GUI |
| HEL | 192.168.1.13 | High Energy Laser | MCC managed — status in MCC REG1 |
| BDC | 192.168.1.20 | Beam Director Controller | A1→TRC/FMC/MCC, A2↔ENG GUI, A3↔THEIA |
| GIMBAL | 192.168.1.21 | Galil pan/tilt servo drive | BDC CMD:7777 / DATA:7778 |
| TRC | 192.168.1.22 | Tracking and Range Computer | A1→BDC, A2↔ENG GUI/BDC |
| FMC | 192.168.1.23 | Fine Mirror Controller | A1→BDC, A2↔ENG GUI |
| GNSS | 192.168.1.30 | NovAtel GNSS receiver | MCC managed |
| RPI/ADSB | 192.168.1.31 | ADS-B decoder | HYPERION |
| LoRa | 192.168.1.32 | LoRa/MAVLink track input | HYPERION |
| NTP | 192.168.1.33 | Stratum 1 NTP server | MCC, TMC, BDC, FMC, TRC direct |
| RADAR | 192.168.1.34 | Radar track input | HYPERION |
| THEIA | 192.168.1.208 (default) | INT_OPS HMI — IPG reference | A3↔MCC/BDC, EXT_OPS:15009 |
| HYPERION | 192.168.1.206 (default) | EXT_OPS CUE relay — IPG reference | EXT_OPS:15009→THEIA, sensor inputs:15001/15002 |
| IPG reserved | 192.168.1.200–.209 | IPG nodes only | — |
| Third-party | 192.168.1.210–.254 | External integrators | A3 or EXT_OPS |
| ENG laptops | 192.168.1.1–.99 | Engineering tools | A1/A2 |

> **IP assignment note:** THEIA and HYPERION operate in the `192.168.1.200–.254` external range. The addresses shown are IPG reference deployment defaults — both are operator-configurable. The constraint is that they remain in the `.200–.254` range so embedded controllers accept their A3 packets. IPG reserves `.200–.209`; third-party integrators use `.210–.254` by convention.

---

## Relationship to INT_OPS and EXT_OPS

This document (INT_ENG, IPGD-0003) is the master ICD — it covers all command bytes,
all register layouts, and all five controllers. It is distributed to IPG engineering
staff only.

Two filtered derivations are distributed externally:

| Document | Doc # | Scope | Audience |
|----------|-------|-------|----------|
| `CROSSBOW_ICD_INT_OPS` | IPGD-0004 | INT_OPS only — MCC and BDC via A3 | Tier 1 integrators, vendor HMI builders |
| `CROSSBOW_ICD_EXT_OPS` | IPGD-0005 | EXT_OPS only — CUE interface | Tier 2 integrators, CUE providers |

EXT_OPS is the cueing input interface — it always operates in conjunction with an INT_OPS
client. THEIA is IPG's reference INT_OPS implementation. A vendor replacing THEIA implements
both IPGD-0004 (control plane) and IPGD-0005 (CUE input).

```
EXT_OPS (IPGD-0005) — cueing input
    ↓
INT_OPS client — THEIA or vendor HMI (IPGD-0004)
    ↓
A3 — MCC, BDC (INT_ENG this document)
    ↓
A1/A2 — all five controllers (INT_ENG this document)
```

---


## Document Scope

Full command byte reference for all CROSSBOW subsystems. All nodes share the same byte encoding.
Scoping differs by codebase (`enum ICD` in C#/THEIA, `enum ICD_CMDS` in TRC, `enum class ICD` in BDC/FMC — no runtime impact).

### Command Scope Definitions

| Scope | Meaning |
|-------|---------|
| `INT_OPS` | Operator-accessible — included in `CROSSBOW_ICD_INT_OPS` and main ICD. Accessible via A3 port (THEIA and system integrators). Was: `EXT`. |
| `INT_ENG` | Engineering use only — included in main ICD only. Accessible via A2 port (ENG GUI, maintenance). Was: `INT`. |
| `RES` | Reserved / not applicable — omitted from all distributed documents. |
| `EXT_OPS` | External integration interface — HYPERION/CUE→THEIA protocol (UDP:15009). Not a controller ICD command. Documented in `CROSSBOW_ICD_EXT_OPS`. |

### Target Definitions

| Target | Subsystem |
|--------|-----------|
| MCC | Master Control Controller (Arduino-based, manages power, laser, GNSS, charger) |
| BDC | Beam Director Controller (manages gimbal, cameras, FSM, MWIR) |
| TRC | TRC / Orin compute node (video pipeline, tracker, COCO inference) |
| FMC | FSM Motor Controller (fine steering mirror) |
| TMC | Thermal Management Controller |

> **Operator ICD rule:** `CROSSBOW_ICD_INT_OPS` includes only `INT_OPS`-scoped commands. External users may only
> address **MCC** and **BDC** targets. Commands whose INT_OPS Target is `—` are not externally actionable.

**Implementation columns:**
- **TRC ASCII** — ASCII string equivalent (port 5012). `—` = binary only or not applicable.
- **TRC Binary** — Binary ICD handler implemented in TRC (port 5010). `needs impl` = handler not yet written.
- **BDC Binary** — BDC firmware forwards/handles this command. `—` = not routed through BDC.

---

## 0xA0–0xAF — System Commands


| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xA0 | SET_UNSOLICITED | Subscribe/unsubscribe to unsolicited telemetry push. Sets per-slot `wantsUnsolicited` flag on the sender's `FrameClient` entry. `{0x01}` = subscribe; `{0x00}` = unsubscribe (client stays registered). Does NOT affect A1 stream. **Session 35:** `isUnSolicitedEnabled` global flag retired — per-slot `wantsUnsolicited` replaces it. | uint8 0=off, 1=on | REPORT START/STOP | ✓ | ✓ | `INT_OPS` | MCC, BDC, TMC, FMC, TRC | MCC, BDC |
| 0xA1 | SET_HEL_TRAINING_MODE | Set laser training/combat mode. Training mode clamps power to 10% regardless of `SET_HEL_POWER`. **Moved from `0xAF` (v3.6.0). Promoted to INT_OPS** — safety enforced in firmware (10% clamp), not scope restriction. | uint8 `0`=COMBAT, `1`=TRAINING | — | — | ✓ | `INT_OPS` | MCC | MCC |
| 0xA2 | SET_NTP_CONFIG | Configure NTP server and/or force resync. **Promoted INT_ENG→INT_OPS (v3.6.0).** 0 bytes = force resync on current server. 1 byte `[p]` = set primary server to `192.168.1.p` + resync. 2 bytes `[p, f]` = set primary to `192.168.1.p`, fallback to `192.168.1.f` + resync. Routing by destination IP — each controller applies to its own NTP client. | 0–2 bytes (see description) | — | — | — | `INT_OPS` | MCC, BDC, TMC, FMC, TRC | MCC, BDC |
| 0xA3 | SET_TIMESRC | Set active time source. **Assigned v3.6.0.** Controls PTP/NTP selection. Routing implicit by destination IP — each controller applies independently. Prerequisite: rejection handler at `0xA3` must be removed (FW-C8) before this command is live in firmware. | uint8 `0`=OFF, `1`=NTP, `2`=PTP, `3`=AUTO | — | — | — | `INT_OPS` | MCC, BDC, TMC, FMC, TRC | MCC, BDC |
| 0xA4 | FRAME_KEEPALIVE | Register/keepalive — replaces `EXT_FRAME_PING` (session 35); extended to A2 and all controllers. Empty payload = ACK only (ping response: byte 0=`0x01`, bytes 1–2=echo SEQ_NUM, bytes 3–6=uptime_ms). Payload `{0x01}` = ACK + solicited REG1 return (rate-gated 1 Hz per slot; suppressed if `wantsUnsolicited=true`). Any accepted frame auto-registers sender and refreshes 60-second liveness. | 0 or 1 byte | — | ✓ | ✓ | `INT_OPS` | MCC, BDC, TMC, FMC, TRC | MCC, BDC |
| 0xA5 | SET_SYSTEM_STATE | Set system state | uint8 (SYSTEM_STATES enum) | — | ✓ | ✓ | `INT_OPS` | MCC, BDC, TMC, FMC, TRC | MCC, BDC |
| 0xA6 | SET_GIMBAL_MODE | Set gimbal/tracker mode | uint8 (BDC_MODES enum) | — | ✓ | ✓ | `INT_OPS` | MCC, BDC, TMC, FMC, TRC | MCC, BDC |
| 0xA7 | SET_LCH_MISSION_DATA | Load LCH/KIZ mission data, clear all windows | uint8 which (0=KIZ,1=LCH); uint8 isValid; uint64 startTimeMission_min; uint64 stopTimeMission_max; int16 az1; int16 el1; int16 az2; int16 el2; uint16 nTargets; uint16 nTotalWindows | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xA8 | SET_LCH_TARGET_DATA | Load LCH/KIZ target with windows | uint8 which (0=KIZ,1=LCH); uint16 nWindows; uint16 startTimeTarget_min; uint16 stopTimeTarget_max; uint16 az1; uint16 el1; uint16 az2; uint16 el2; nWindows×[uint16 wt1, uint16 wt2] | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xA9 | SET_REINIT | Unified controller reinitialise. **Assigned v3.6.0. Replaces `0xB0` (BDC) and `0xE0` (MCC).** Routing implicit by destination IP/port — controller selection is the destination address. TMC and FMC not supported (reinitialise via parent: MCC→TMC, BDC→FMC). Note: PTP subsystem index differs between controllers (BDC=7, MCC=4). | uint8 subsystem (BDC: 0=NTP,1=GIMBAL,2=FUJI,3=MWIR,4=FSM,5=JETSON,6=INCL,7=PTP; MCC: 0=NTP,1=TMC,2=HEL,3=BAT,4=PTP,5=CRG,6=GNSS,7=BDC) | — | — | ✓ | `INT_OPS` | MCC, BDC | MCC, BDC |
| 0xAA | SET_DEVICES_ENABLE | Unified device enable/disable. **Assigned v3.6.0. Replaces `0xBE` (BDC) and `0xE1` (MCC).** Routing implicit by destination IP/port. TMC and FMC not supported. Note: PTP device index differs between controllers (BDC=7, MCC=4). | uint8 device (BDC: 0=NTP,1=GIMBAL,2=FUJI,3=MWIR,4=FSM,5=JETSON,6=INCL,7=PTP; MCC: 0=NTP,1=TMC,2=HEL,3=BAT,4=PTP,5=CRG,6=GNSS,7=BDC); uint8 0/1 | — | — | ✓ | `INT_OPS` | MCC, BDC | MCC, BDC |
| 0xAB | SET_FIRE_VOTE | Laser fire requested vote. **Moved from `0xE6`. Promoted INT_ENG→INT_OPS (v3.6.0).** Continuous heartbeat required — vote drops on client disconnect. Heartbeat is the safety gate; no firmware state latches. | uint8 0/1 | — | — | ✓ (→MCC) | `INT_OPS` | MCC | MCC |
| 0xAC | SET_BDC_HORIZ | Set horizon elevation vector | float[360] | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xAD | SET_HEL_POWER | Set laser power level | uint8 [0–100] % | — | — | ✓ | `INT_OPS` | MCC | MCC |
| 0xAE | CLEAR_HEL_ERROR | Clear laser error state | none | — | — | ✓ | `INT_OPS` | MCC | MCC |
| 0xAF | SET_CHARGER | Set charger state and current level. **Assigned v3.6.0. Merges `0xE3` (PMS_CHARGER_ENABLE) and `0xED` (PMS_SET_CHARGER_LEVEL).** Level required on every call — enables and sets level simultaneously; cannot enable without specifying level; if already enabled, changes level immediately. **V1:** GPIO enable + I2C level control. **V2:** GPIO enable only — level `0`=disable, non-zero=enable; I2C level control not available on V2 hardware. FW-CRG-V2 fix CB-20260416. | uint8 level: `0`=disable, `10`=low, `30`=med, `55`=high | — | — | ✓ | `INT_OPS` | MCC | MCC |


---


## 0xB0–0xBF — BDC Commands


| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xB0 | RES_B0 | **RETIRED v3.6.0** — SET_BDC_REINIT superseded by `SET_REINIT (0xA9)`. Returns `STATUS_CMD_REJECTED` pending handler removal (FW-C8). | — | — | — | — | `RES` | RES | RES |
| 0xB1 | SET_BDC_VOTE_OVERRIDE | Override individual BDC geometry vote bit. **Moved from `0xAA` (v3.6.0).** | uint8 vote (0=HORIZ,1=KIZ,2=LCH,3=BDA); uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC | — |
| 0xB2 | SET_GIM_POS | Set gimbal position | int32 pan, int32 tilt | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB3 | SET_GIM_SPD | Set gimbal speed | int16 pan, int16 tilt | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB4 | SET_CUE_OFFSET | Set cue track offset (AZ, EL) | float az_deg, float el_deg | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB5 | CMD_GIM_PARK | Park gimbal at home | none | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB6 | SET_GIM_LIMITS | Set gimbal wrap limits | int32 panMin, int32 panMax, int32 tiltMin, int32 tiltMax | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB7 | SET_PID_GAINS | Set PID gains (cue or AT loop) | uint8 which (0=cue,1=AT); float kpp,kip,kdp,kpt,kit,kdt | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB8 | SET_PID_TARGET | Set PID target setpoint (14 bytes total). Sub-cmd 0x00: x=NED az (deg), y=NED el (deg); BDC applies CUE_OFFSET then full NED→gimbal rotation. Sub-cmd 0x01: x=tx (pixels), y=ty (pixels), written directly to vpInput/vtInput. pidScale = FOV scale for current camera. THEIA sends sub-cmd 0x00 only. | uint8 sub-cmd (0=CUE NED,1=video px); float x LE; float y LE; float pidScale LE | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xB9 | SET_PID_ENABLE | Enable/disable PID loop | uint8 which (0=cue,1=video); uint8 0/1 | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xBA | SET_SYS_LLA | Set platform geodetic position | float lat, float lng, float alt | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xBB | SET_SYS_ATT | Set platform attitude (RPY) | float roll, float pitch, float yaw | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xBC | SET_BDC_VICOR_ENABLE | BDC Vicor power enable | uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC | — |
| 0xBD | SET_BDC_RELAY_ENABLE | BDC relay enable | uint8 relay (1–4); uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC | — |
| 0xBE | RES_BE | **RETIRED v3.6.0** — SET_BDC_DEVICES_ENABLE superseded by `SET_DEVICES_ENABLE (0xAA)`. Returns `STATUS_CMD_REJECTED` pending handler removal (FW-C8). | — | — | — | — | `RES` | RES | RES |
| 0xBF | RES_BF | Reserved — confirmed unused in firmware. No inbound command handler; no outbound CMD_BYTE role. | — | — | — | — | `RES` | — | — |


---


## 0xC0–0xCF — BDC/Camera Commands


| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xC0 | RES_C0 | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xC1 | SET_CAM_MAG | VIS camera zoom | uint8 mag index | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xC2 | SET_CAM_FOCUS | VIS camera focus | uint16 focus position | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xC3 | RES_C3 | Reserved (was: gain auto) | — | — | — | — | `RES` | RES | RES |
| 0xC4 | CMD_VIS_AWB | Trigger VIS auto white balance once on active camera. **Assigned CB-20260416e (HMI-AWB).** No payload. BDC routes to TRC via A2; TRC calls `cam->runAutoWhiteBalance()`. ASCII equivalent: `AWB`. | none | AWB | needs impl (ICD only — TRC binary handler complete) | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xC5 | RES_C5 | Reserved (was: exposure auto) | — | — | — | — | `RES` | RES | RES |
| 0xC6 | RES_C6 | Reserved (was: gamma) | — | — | — | — | `RES` | RES | RES |
| 0xC7 | SET_CAM_IRIS | VIS camera iris position | uint8 upper nibble of iris position | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xC8 | CMD_VIS_FILTER_ENABLE | VIS ND filter enable | uint8 0/1 | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xC9 | SET_BDC_PALOS_VOTE | Set operator/position valid vote | uint8 which (0=KIZ,1=LCH); uint8 operatorValid; uint8 positionValid; uint8 forExec | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCA | GET_BDC_PALOS_VOTE | Check BDC PALOS vote | uint8 which (0=KIZ,1=LCH); uint64 timestamp; float az; float el | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCB | SET_MWIR_WHITEHOT | MWIR white hot polarity | uint8 0/1 | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCC | CMD_MWIR_NUC1 | MWIR internal NUC refresh | none | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCD | CMD_MWIR_AF_MODE | MWIR AF mode | uint8 (0=off,1=continuous,2=once) | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCE | CMD_MWIR_BUMP_FOCUS | MWIR bump focus near/far | uint8 (0=near,1=far) at default speed | — | — | ✓ | `INT_OPS` | BDC | BDC |
| 0xCF | RES_CF | Reserved | — | — | — | — | `RES` | RES | RES |


---


## 0xD0–0xDF — TRC/Orin Commands


| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xD0 | ORIN_CAM_SET_ACTIVE | Set active camera | uint8 BDC_CAM_IDS (0=VIS,1=MWIR) | SELECT CAM1|CAM2 | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD1 | ORIN_ACAM_COCO_ENABLE | Enable/disable COCO intra-trackbox inference on active camera. **Moved from `0xDF` (v3.6.0).** Model must be loaded (ISR lifecycle). Camera switch auto-disables COCO. Planned multi-op extension (COCO-07): op=0 off, op=1 on, op=2 load, op=3 unload, op=4 set_drift (param=uint8 threshold×100), op=5 set_interval (param=uint8 N). | uint8 op [, uint8 param] | COCO ON|OFF|LOAD|UNLOAD|DRIFT|INTERVAL | needs impl | — | `INT_ENG` | BDC, TRC | — |
| 0xD2 | RES_D2 | **RETIRED v3.6.0** — ORIN_SET_STREAM_60FPS retired. Framerate is compile/launch time configuration only. ASCII `FRAMERATE <fps>` on port 5012 covers all ENG use. | — | FRAMERATE <fps> | — | — | `RES` | RES | RES |
| 0xD3 | ORIN_SET_STREAM_OVERLAYS | Set HUD overlay bitmask. bit0=Reticle, bit1=TrackPreview, bit2=TrackBox, bit3=CueChevrons, bit4=AC_Projections, bit5=AC_LeaderLines, bit6=FocusScore, bit7=OSD | uint8 bitmask (8 flags — see Enumerations sheet) | RETICLE ON|OFF | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD4 | ORIN_ACAM_SET_CUE_FLAG | Set cue flag indicator (HUD chevrons) | uint8 0/1 | — | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD5 | ORIN_ACAM_SET_TRACKGATE_SIZE | Set track gate width/height | uint8 w, uint8 h (pixels, min 16) | TRACKBOX <w> <h> | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD6 | ORIN_ACAM_ENABLE_FOCUSSCORE | Enable focus score computation | uint8 0/1 | FOCUSSCORE ON|OFF | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD7 | ORIN_ACAM_SET_TRACKGATE_CENTER | Set track gate preview center (no tracker init) | uint16 x, uint16 y (pixels) | TRACKBOX <w> <h> <cx> <cy> | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xD8 | RES_D8 | **RETIRED v3.6.0** — ORIN_SET_TESTPATTERN retired. Test pattern is compile/launch time configuration only. ASCII `TESTSRC CAM1\|CAM2 TEST\|LIVE` on port 5012 covers all ENG use. TRC binary handler was never implemented. | — | TESTSRC CAM1\|CAM2 TEST\|LIVE | — | — | `RES` | RES | RES |
| 0xD9 | ORIN_ACAM_COCO_CLASS_FILTER | Filter COCO inference to specific class ID. 0xFF = accept all (default). Repurposed from ORIN_ACAM_SET_AI_TRACK_PRIORITY (never implemented). | uint8 class_id (0–79 per COCO 80-class; 0xFF=all) | COCO FILTER <id|ALL> | needs impl | — | `INT_ENG` | BDC, TRC | BDC |
| 0xDA | ORIN_ACAM_RESET_TRACKB | Reset MOSSE tracker to current preview gate | none | TRACKER RESET | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xDB | ORIN_ACAM_ENABLE_TRACKERS | Enable/disable tracker for active camera. **ICD v4.1.0 (CB-20260419):** LK (tracker_id=4) fully implemented — "placeholder" label removed. 3rd byte added: `mosseReseed 0x01/0x00` — enables NCC-gated MOSSE template reseed from `lkBbox_` when LK is healthy. ASCII `LK MOSSE ON\|OFF` maps to this byte. | uint8 tracker_id (0=AI,1=MOSSE,2=Centroid,3=Kalman,4=LK); uint8 0/1; [uint8 mosseReseed 0x01/0x00 — LK only, ICD v4.1.0] | TRACKER ON\|OFF / LK ON\|OFF / LK MOSSE ON\|OFF | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xDC | ORIN_ACAM_SET_ATOFFSET | Set AT reticle offset | int8 dx, int8 dy (pixels, −128 to 127) | ATOFFSET <x> <y> | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xDD | ORIN_ACAM_SET_FTOFFSET | Set FT (fine-track) offset | int8 dx, int8 dy (pixels, −128 to 127) | FTOFFSET <x> <y> | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xDE | ORIN_SET_VIEW_MODE | Set compositor output view | uint8 (0=CAM1,1=CAM2,2=PIP4,3=PIP8) | VIEW CAM1|CAM2|PIP|PIP8 | ✓ | ✓ | `INT_OPS` | BDC, TRC | BDC |
| 0xDF | RES_DF | **RETIRED v3.6.0** — ORIN_ACAM_COCO_ENABLE moved to `0xD1`. | — | — | — | — | `RES` | RES | RES |


---


## 0xE0–0xEF — MCC / PMS / TMS Commands


| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xE0 | SET_BCAST_FIRECONTROL_STATUS | Broadcast fire control vote bytes to all embedded controllers. **Moved from `0xAB` (v3.6.0).** Internal command — MCC aggregates vote state from A1 stream and forwards to TRC via BDC. Verify source: confirm whether MCC sends autonomously on every A1 cycle or only on vote state change. | uint8 voteBitsMcc (MCC vote bits); uint8 voteBitsBdc (BDC geometry vote bits) | — | ✓ | ✓ | `INT_ENG` | MCC, BDC, TRC | — |
| 0xE1 | RES_E1 | **RETIRED v3.6.0** — SET_MCC_DEVICES_ENABLE superseded by `SET_DEVICES_ENABLE (0xAA)`. Returns `STATUS_CMD_REJECTED` pending handler removal (FW-C8). | — | — | — | — | `RES` | RES | RES |
| 0xE2 | PMS_POWER_ENABLE | Unified power output control — replaces `PMS_SOL_ENABLE` (0xE2), `PMS_RELAY_ENABLE` (0xE4), `PMS_VICOR_ENABLE` (0xEC). INT_ENG only — A3 returns `STATUS_CMD_REJECTED`. | uint8 which (MCC_POWER enum: 0=RELAY_GPS, 1=VICOR_BUS, 2=RELAY_LASER, 3=VICOR_GIM, 4=VICOR_TMS, 5=SOL_HEL, 6=SOL_BDA, 7=RELAY_NTP); uint8 0/1. V1 valid: 0–2, 5–6. V2 valid: 2–4. V3-3kW valid: 0–2, 5–7. V3-6kW valid: 0–4, 7. Invalid `which` for active revision → `STATUS_CMD_REJECTED`. | — | — | ✓ | `INT_ENG` | MCC | — |
| 0xE3 | RES_E3 | **RETIRED v3.6.0** — PMS_CHARGER_ENABLE merged into `SET_CHARGER (0xAF)`. Returns `STATUS_CMD_REJECTED` pending handler removal (FW-C8). | — | — | — | — | `RES` | RES | RES |
| 0xE4 | ~~PMS_RELAY_ENABLE~~ → **RES_E4** | **RETIRED v3.4.0** — returns `STATUS_CMD_REJECTED`. Use `0xE2 PMS_POWER_ENABLE` with `MCC_POWER::GPS_RELAY (0)` or `MCC_POWER::LASER_RELAY (2)`. | — | — | — | — | `RES` | MCC | MCC |
| 0xE5 | RES_E5 | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xE6 | RES_E6 | **RETIRED v3.6.0** — SET_FIRE_VOTE moved to `0xAB` and promoted to INT_OPS. Returns `STATUS_CMD_REJECTED` pending handler removal (FW-C8). | — | — | — | — | `RES` | RES | RES |
| 0xE7 | TMS_INPUT_FAN_SPEED | Set fan speed | uint8 which (0/1); uint8 speed (0=off,128=low,255=high) — matches `TMC_FAN_SPEEDS` enum | — | — | ✓ | `INT_OPS` | MCC, TMC | MCC |
| 0xE8 | TMS_SET_DAC_VALUE | Set DAC output value | uint8 dac (`TMC_DAC_CHANNELS` enum); uint16 value | — | — | ✓ | `INT_ENG` | MCC, TMC | — |
| 0xE9 | TMS_SET_VICOR_ENABLE | TMS Vicor enable | uint8 vicor (TMC_VICORS enum); uint8 0/1 — V1: values 0–3 valid; V2: values 0–2,4 valid (PUMP2=4; HEAT=3 does not exist on V2) | — | — | ✓ | `INT_ENG` | MCC, TMC | — |
| 0xEA | TMS_SET_LCM_ENABLE | TMS LCM enable | uint8 lcm (enum); uint8 0/1 | — | — | ✓ | `INT_ENG` | MCC, TMC | — |
| 0xEB | TMS_SET_TARGET_TEMP | Set TMS target temperature | uint8 temp °C — **enforced range [10–40°C]**; firmware clamps silently. Values outside range are accepted without error but constrained. | — | — | ✓ | `INT_OPS` | MCC, TMC | MCC |
| 0xEC | ~~PMS_VICOR_ENABLE~~ → **RES_EC** | **RETIRED v3.4.0** — returns `STATUS_CMD_REJECTED`. Use `0xE2 PMS_POWER_ENABLE` with `MCC_POWER::VICOR_BUS (1)`, `VICOR_GIM (3)`, or `VICOR_TMS (4)`. | — | — | — | — | `RES` | MCC | MCC |
| 0xED | RES_ED | **RETIRED v3.6.0** — PMS_SET_CHARGER_LEVEL merged into `SET_CHARGER (0xAF)`. Returns `STATUS_CMD_REJECTED` pending handler removal (FW-C8). | — | — | — | — | `RES` | RES | RES |
| 0xEE | RES_EE | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xEF | RES_EF | Reserved — confirmed unused in firmware. No inbound command handler; no outbound CMD_BYTE role. | — | — | — | — | `RES` | — | — |


---


## 0xF0–0xFF — FSM / FMC Commands


| Byte | Enum | Description | Payload | TRC ASCII | TRC Binary | BDC Binary | Scope | INT_ENG Target | INT_OPS Target |
|------|------|-------------|---------|------------|-------------|------------|-------|------------|------------|
| 0xF0 | FMC_SET_FSM_POW | FSM power enable | uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xF1 | BDC_SET_FSM_HOME | FSM set home position | int16 x, int16 y | — | — | ✓ | `INT_OPS` | BDC, FMC | BDC |
| 0xF2 | BDC_SET_FSM_IFOVS | FSM set iFOV scaling | float x, float y | — | — | ✓ | `INT_OPS` | BDC, FMC | BDC |
| 0xF3 | FMC_SET_FSM_POS | FSM set position | int16 x, int16 y | — | — | ✓ | `INT_OPS` | BDC, FMC | BDC |
| 0xF4 | BDC_SET_FSM_SIGNS | FSM axis direction signs | int8 x, int8 y | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xF5 | FMC_FSM_TEST_SCAN | FSM test scan | none | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xF6 | BDC_SET_FSM_TRACK_ENABLE | FSM track mode enable | uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xF7 | FMC_READ_FSM_POS | Read FSM position from ADC | none | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xF8 | RES_F8 | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xF9 | RES_F9 | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xFA | BDC_SET_STAGE_HOME | Focus stage waist home | uint32 position | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xFB | FMC_SET_STAGE_POS | Focus stage set position | uint32 position | — | — | ✓ | `INT_OPS` | BDC, FMC | BDC |
| 0xFC | FMC_STAGE_CALIB | Focus stage calibrate | none | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xFD | RES_FD | Reserved | — | — | — | — | `RES` | RES | RES |
| 0xFE | FMC_SET_STAGE_ENABLE | Focus stage enable | uint8 0/1 | — | — | ✓ | `INT_ENG` | BDC, FMC | — |
| 0xFF | RES_FF | Reserved — confirmed unused in firmware. No inbound command handler; no outbound CMD_BYTE role. | — | — | — | — | `RES` | — | — |


---


## TRC ASCII-Only Commands

Commands accepted on TRC ASCII port (5012) with no binary ICD equivalent. Send via `nc` or `socat` — see **Example Bash Usage** section below.

### Global Commands (any active camera)

| Command | Description |
|---------|-------------|
| `SELECT CAM1\|CAM2` | Switch active camera. Calls `setCocoEnabled(false)` on old camera before switch. |
| `VIEW CAM1\|CAM2\|PIP\|PIP8` | Set compositor output view mode |
| `STATUS` | Print full system state; send one-shot 64-byte telemetry to ASCII sender's IP on binary port |
| `REPORT START [ms]\|STOP` | Start/stop unsolicited telemetry at interval (default 1000 ms, min 10 ms) |
| `DEBUG ON\|OFF` | Enable/disable debug logging (`dlog()`) |
| `DEBUG VERBOSE ON\|OFF` | Enable high-frequency per-packet logs (`vlog()`). `DEBUG OFF` implicitly clears verbose. |
| `TESTSRC CAM1\|CAM2 TEST\|LIVE` | Switch camera capture source to test pattern or live |
| `BITRATE <Mbps>` | Set H.264 encoder bitrate (1–50 Mbps) |
| `QUIT` | Graceful shutdown |

### Camera Commands (routed to active camera)

| Command | Description |
|---------|-------------|
| `EXPOSURE <µs>` | Set exposure, disables auto |
| `EXPOSURE AUTO` | Re-enable auto exposure |
| `GAIN <dB>` | Set gain, disables auto |
| `GAIN AUTO` | Re-enable auto gain |
| `GAMMA <value>` | Set gamma |
| `FRAMERATE <fps>` | Set framerate |
| `INTENSITYTARGET <0-100>` | Set AE brightness target |
| `AWB` | Trigger auto white balance once |
| `FOCUSSCORE ON\|OFF` | Enable/disable focus score computation and OSD label |
| `RETICLE ON\|OFF` | Toggle reticle overlay |
| `OSD ON\|OFF` | Toggle OSD text overlay |
| `TRACKBOX <w> <h> [cx cy]` | Set track gate size and optionally center. No tracker init. Issue `TRACKER RESET` to apply to a live tracker. |
| `ATOFFSET <x> <y>` | Set AT reticle offset (−128 to 127 px) |
| `FTOFFSET <x> <y>` | Set FT offset (−128 to 127 px) |
| `TRACKER ON\|OFF\|RESET` | Tracker lifecycle. `ON` inits MOSSE at current trackbox. `RESET` reinits at current position if tracking, else inits. `OFF` clears tracker and COCO state. |
| `TRACKER INIT <x> <y> <w> <h>` | Init tracker with explicit ROI (top-left origin) |
| `AUTOMODEREGION [x y w h]` | Set auto-mode region; no args uses current trackbox |
| `OFFSETX <pixels>` | Set sensor ROI X offset (requires camera stop/restart) |
| `OFFSETY <pixels>` | Set sensor ROI Y offset (requires camera stop/restart) |

### COCO Commands (active camera, Phase 4)

`COCO LOAD` must precede `COCO AMBIENT ON` or `COCO TRACK ON`. Binary equivalent `0xDF` wires to `LOAD`+`ON` in the ISR lifecycle (not yet implemented — COCO-04).

| Command | Description |
|---------|-------------|
| `COCO LOAD` | Load SSD MobileNet V3 model from `model_data/`, start inference thread. Probes CUDA FP16 backend; falls back to CPU if unavailable. Idempotent — no-op if already loaded. |
| `COCO UNLOAD` | Stop inference thread and release model. Frees GPU memory. |
| `COCO AMBIENT ON\|OFF` | Enable/disable ambient full-frame scan. Model must be loaded. Compositor pushes full frames at `interval` rate; detections latched for OSD display and NEXT/PREV cycle. |
| `COCO TRACK ON\|OFF` | Enable/disable intra-box track mode. Compositor pushes MOSSE trackbox crops at `interval` rate; drift computed vs crop centre. |
| `COCO ON` | Legacy alias — enables track mode on active camera. Requires model loaded. |
| `COCO OFF` | Legacy alias — disables track mode. Model stays loaded. |
| `COCO NEXT` | Step ambient cycle to next detection (wraps). Moves trackgate preview to selected detection box. |
| `COCO PREV` | Step ambient cycle to previous detection (wraps). |
| `COCO RESET` | Clear ambient cycle selection and restore trackgate to default size (256×256) and centre. |
| `COCO STATUS` | Print: loaded, ambient, track, interval, nms, minArea, maxArea, tracking, ambientDets, cycleIdx, conf, classId, class, drift, driftDx, driftDy |
| `COCO DRIFT <0.0–1.0>` | Set drift detection threshold as a fraction of `min(bbox.w, bbox.h)`. Default `0.20`. Takes effect immediately on next inference result. |
| `COCO INTERVAL <1–100>` | Push a crop every Nth fresh camera frame. Rate = `60/N` Hz (camera runs at 60 Hz). Default `3` = 20 Hz. Gated on fresh frames only — not compositor tick rate (100 Hz). |
| `COCO NMS <0.0–1.0>` | NMS IoU overlap threshold. Boxes with IoU above this value are suppressed. Lower = fewer overlapping boxes. Default `0.35`. Runtime-tunable — takes effect on next inference. |
| `COCO MINAREA <0.0–1.0>` | Minimum detection area as a fraction of frame area. Detections smaller than `frac × W × H` are discarded. Default `0.002` ≈ 1850 px² on 1280×720. Resolution-independent. |
| `COCO MAXAREA <0.0–1.0>` | Maximum detection area as a fraction of frame area. Detections larger than `frac × W × H` are discarded. Default `0.50` = half frame. |
| `COCO FILTER <id\|ALL>` | Filter inference to a specific COCO class ID (0–79) or accept all (`ALL`). Maps to `0xD9 ORIN_ACAM_COCO_CLASS_FILTER`. |

### LK ASCII Commands

| Command | Description |
|---------|-------------|
| `LK ON` | Enable sparse LK tracker on active camera. Requires tracker active (`TRACKER ON`). Synchronous — no separate thread. Default: **off**. Binary: tracker_id=4 on `0xDB` (LK-02 placeholder). |
| `LK OFF` | Disable LK. Clears all LK state — point set, bbox, drift flag. |
| `LK STATUS` | Print: enabled, pointCount, flowMag, drift, reseedInterval. |
| `LK RESEED <0–300>` | Set NCC-gated reseed interval in fresh frames. `0` = disabled (hold points from init only). Default `30` = 0.5s at 60 Hz. Reseed only fires when `nccScore ≥ 0.50` — prevents reseeding onto background when drifted. |

### ENG Debug Injection Commands

ASCII-only commands for OSD symbology verification. Inject fire control and system state without requiring live BDC/MCC traffic. Values overwritten on next `0xE0`/`0xAB` broadcast.

| Command | Description |
|---------|-------------|
| `STATE <state>` | Inject system state. Values: `OFF`, `STNDBY`, `ISR`, `COMBAT`, `MAINT`, `FAULT`. OSD top-right STATE row updates immediately. |
| `MODE <mode>` | Inject gimbal mode. Values: `OFF`, `POS`, `RATE`, `CUE`, `ATRACK`, `FTRACK`. OSD top-right MODE row and reticle arm tips update immediately. |
| `FCVOTES <mcc_hex> <bdc_hex>` | Inject fire control vote bytes directly. `mcc_hex` = MCC vote bits (e.g. `0xFE`), `bdc_hex` = BDC geometry bits (e.g. `0x80`). Drives reticle colour and interlock message. See FC symbology checkout sequence in Example Bash Usage. |

---

## Network Addresses (Single Source of Truth)

All subsystem IPs are defined in `Defaults` namespace (`types.h` for TRC, `defines.hpp` for BDC). Do not hardcode IPs in application code — reference these constants.

| Constant | Value | Used By | Purpose |
|----------|-------|---------|---------|
| `BDC_HOST` / `IP_TRC_BYTES` | `192.168.1.22` | BDC → TRC | Binary ICD commands (port 5010), ASCII commands (port 5012) |
| `IP_FMC_BYTES` | `192.168.1.23` | BDC → FMC | Binary ICD commands (port 10023) |
| `IP_GIMBAL_BYTES` | `192.168.1.21` | BDC → Gimbal | Galil ASCII (ports 7777/7778) |
| `IP_NTP_BYTES` | `192.168.1.33` | MCC, TMC, BDC, FMC, TRC | NTP sync — target NTP server |
| `Defaults::BDC_HOST` | `192.168.1.20` | TRC → BDC | **Unsolicited telemetry auto-start target (TRC-26).** TRC sends 100 Hz telemetry to this address on boot without waiting for `0xA0`. |

### TRC Startup — Required Arguments

TRC must be started with the HMI (THEIA) destination IP for H.264 video:

```bash
./multi_streamer --dest-host 192.168.1.208
```

`--dest-host` sets the video stream destination (port 5000, H.264 RTP). This is separate from the telemetry destination — telemetry auto-targets BDC (`Defaults::BDC_HOST = 192.168.1.20`) at boot regardless of `--dest-host`.

| Argument | Value | Purpose |
|----------|-------|---------|
| `--dest-host` | `192.168.1.208` (THEIA) | H.264 RTP video stream (UDP port 5000) |

If `--dest-host` is omitted, video will not stream. Telemetry to BDC is unaffected.

---

## Video Stream

TRC (Jetson Orin NX, 192.168.1.22) encodes and streams a single H.264 RTP video stream. The stream carries the compositor output — VIS standalone, MWIR standalone, or PIP composite — as selected by `0xD0` (active camera) and `0xDE` (view mode). There is **one stream** regardless of view mode.

### Stream Parameters

| Parameter | Value | Notes |
|-----------|-------|-------|
| Transport | UDP RTP unicast (default) | Multicast pending — see below |
| Destination (unicast) | 192.168.1.208 : 5000 | THEIA operator PC |
| Port | **5000** (UDP, fixed) | |
| Protocol | RTP — payload type 96, encoding H264 | |
| Codec | H.264 | Hardware encoded — Jetson `nvv4l2h264enc` |
| Resolution | **1280 × 720** (fixed) | Must be passed explicitly to decoder; auto-detect produces invalid frames |
| Framerate | **60 fps** (fixed) | 30 fps option pending — see `0xD2` |
| Bitrate | **10 Mbps** (fixed) | |
| UDP receive buffer | 2097152 bytes (2 MB) | Required at 60 fps — default OS buffer causes packet drops |
| Jitter buffer latency | 50 ms with `drop-on-latency=true` | Absorbs Jetson encoder timing jitter |
| E2E latency (HW decode) | 30–80 ms | `nvh264dec`, NVIDIA GPU |
| E2E latency (SW decode) | 50–100 ms | `avdec_h264`, no GPU |

### Unicast Configuration (Current Production)

TRC streams directly to THEIA (192.168.1.208) on port 5000. GStreamer receive pipeline (`GStreamerPipeReader.cs`):

```
udpsrc port=5000 buffer-size=2097152
  caps="application/x-rtp,media=video,encoding-name=H264,payload=96"
! rtpjitterbuffer latency=50 drop-on-latency=true
! rtph264depay ! h264parse ! nvh264dec
! videoconvert n-threads=4 ! fdsink
```

Software fallback (no NVIDIA GPU): substitute `avdec_h264` for `nvh264dec`. CPU load ~25–35% at 60 fps, ~10–15% at 30 fps.

### Multicast Configuration (Pending — `0xD1 ORIN_SET_STREAM_MULTICAST`)

`0xD1` is wired in the ICD but the binary handler is not yet implemented in `udp_listener.cpp`.

| Parameter | Value |
|-----------|-------|
| Multicast group | `239.127.1.21` (site-local, CROSSBOW reserved) |
| Port | 5000 (unchanged) |
| TRC sender change | `udpsink multicast-iface=eth0 host=239.127.1.21 port=5000` |
| Receiver change | `udpsrc multicast-group=239.127.1.21 port=5000 buffer-size=2097152` |
| Benefit | Multiple simultaneous THEIA clients without duplicate unicast streams from TRC |
| Requirement | All receiver NICs on 192.168.1.x; multicast routing enabled on switch |

### Framerate Control (Pending — `0xD2 ORIN_SET_STREAM_60FPS`)

`0xD2` is wired in the ICD but the binary handler is not yet implemented in `udp_listener.cpp`.

| Payload | Effect |
|---------|--------|
| `{0x01}` | 60 fps (default, current) |
| `{0x00}` | 30 fps — reduces CPU load on software-decode receivers |

When 30 fps is enabled: update `_displayTimer.Interval` from `16` → `33` ms in THEIA.

### Known Quirks

| Item | Value | Notes |
|------|-------|-------|
| `PixelShift` | **−420 px horizontal** | Fixed alignment offset applied in `GStreamerPipeReader.cs`. Root cause: Jetson `nvv4l2h264enc` pipeline artefact. Do not change without retesting on hardware. |
| Resolution | **1280 × 720 explicit** | `_reader.Start(5000, 1280, 720)` — auto-detect produces invalid frames. |
| Display timer | **16 ms (60 Hz)** | Change to 33 ms if `0xD2` 30 fps is enabled. |
| `0xD1` / `0xD2` status | **ICD wired, not deployed** | Binary handlers pending in `udp_listener.cpp`. |

---

## Example Bash Usage

All examples target TRC at `192.168.1.22`, ASCII port `5012`.

```bash
TRC=192.168.1.22
PORT=5012

# One-shot ASCII command helper
trc3() { echo "$*" | nc -u -w1 $TRC $PORT; }

# --- Basic session startup ---
trc3 DEBUG ON
trc3 STATUS

# --- Standard MOSSE track test ---
trc3 SELECT CAM1
trc3 TRACKBOX 128 128 640 360        # centre gate on 1280×720 frame
trc3 TRACKER ON                       # init MOSSE at current trackbox
trc3 STATUS                           # verify TrackB_Enabled + TrackB_Valid bits

# --- COCO ambient scan (boot flag: --coco-ambient loads + enables on startup) ---
trc3 COCO LOAD                        # loads model_data/, starts inference thread (~2s)
trc3 COCO AMBIENT ON                  # enable full-frame ambient scan
trc3 COCO STATUS                      # check: loaded=YES ambient=YES ambientDets=N
# STATUS output: loaded, ambient, track, interval, nms, minArea, maxArea,
#                tracking, ambientDets, cycleIdx, conf, classId, class, drift, driftDx, driftDy

# --- Ambient cycle: step through detections ---
trc3 COCO NEXT                        # gate preview moves to highest-confidence detection
trc3 COCO NEXT                        # step to next
trc3 COCO PREV                        # step back
trc3 COCO RESET                       # clear selection, gate returns to 256×256 centre

# --- Start MOSSE tracker from selected ambient detection ---
trc3 COCO NEXT                        # select detection (gate moves to it)
trc3 TRACKER ON                       # init MOSSE at gate position (= selected detection)

# --- COCO track mode (intra-box crop inference while tracking) ---
trc3 COCO TRACK ON                    # enable track mode — requires TRACKER ON
trc3 COCO STATUS                      # confirm track=YES, watch drift field

# --- Tune inference rate ---
trc3 COCO INTERVAL 6                  # reduce to 10 Hz if load too high
trc3 COCO INTERVAL 2                  # increase to 30 Hz for faster response
trc3 COCO INTERVAL 1                  # every fresh frame = 60 Hz (max)

# --- Tune NMS and area filter ---
trc3 COCO NMS 0.35                    # default — lower removes more overlapping boxes
trc3 COCO NMS 0.20                    # tighter NMS for dense scenes
trc3 COCO MINAREA 0.002               # default ~1850px² minimum (1280×720)
trc3 COCO MINAREA 0.005               # filter small noise boxes
trc3 COCO MAXAREA 0.50                # default half-frame maximum
trc3 COCO STATUS                      # confirm nms/minArea/maxArea values took effect

# --- COCO STATUS poll loop ---
while true; do trc3 COCO STATUS; sleep 2; done

# --- Tune drift threshold ---
trc3 COCO DRIFT 0.15                  # tighten from default 0.20
trc3 COCO DRIFT 0.30                  # loosen if too noisy

# --- Monitor inference results via telemetry ---
trc3 REPORT START 1000
# In another terminal — dump telemetry bytes as hex:
nc -u -l 5010 | xxd | head -4
# bytes [43-44] = nccScore (int16_t × 10000)
# bytes [45-46] = jetsonTemp (int16, °C)
# bytes [47-48] = jetsonCpuLoad (int16, %)
# bytes [57-58] = jetsonGpuLoad (int16, %)
# bytes [59-63] = RESERVED

# --- Camera switch ---
trc3 SELECT CAM2                      # switch to MWIR
trc3 TESTSRC CAM2 TEST                # switch CAM2 to test pattern (ball)
trc3 TESTSRC CAM2 LIVE                # switch back to live
trc3 SELECT CAM1                      # switch back to Alvium

# --- Teardown ---
trc3 COCO AMBIENT OFF
trc3 COCO TRACK OFF
trc3 TRACKER OFF
trc3 COCO UNLOAD                      # free GPU memory if done for session
```

### LK Validation

```bash
TRC=192.168.1.22; PORT=5012
trc3() { echo "$*" | nc -u -w1 $TRC $PORT; }

trc3 DEBUG ON
trc3 SELECT CAM1
trc3 TRACKBOX 128 128 640 360
trc3 TRACKER ON                       # MOSSE must be active first
trc3 LK ON                            # enable LK (default off)
trc3 LK STATUS                        # enabled=YES points=N flowMag=x.xx drift=NO reseed=30

# --- Tune reseed ---
trc3 LK RESEED 0                      # disable periodic reseed (hold from init only)
trc3 LK RESEED 60                     # reseed every 1s at 60 Hz
trc3 LK RESEED 30                     # restore default = every 0.5s

# --- Teardown ---
trc3 LK OFF
trc3 TRACKER OFF
```

### FC Symbology Checkout Sequence

Walks every reticle colour and interlock message in order. Requires OSD ON, reticle enabled, not tracking.

```bash
trc3 OSD ON

# 1. Abort active — yellow reticle, "ABORT"
trc3 FCVOTES 0x00 0x00

# 2. Not abort, idle — green reticle, no message
trc3 FCVOTES 0x02 0x00

# 3. Armed — orange reticle, "ARMED"
trc3 FCVOTES 0x06 0x00

# --- Trigger pulled interlocks (white reticle) ---

# 4. Trigger, no combat → "INTERLOCK - NOT COMBAT"
trc3 FCVOTES 0x22 0x00

# 5. + combat, no armed → "INTERLOCK - NOT ARMED"
trc3 FCVOTES 0xA2 0x00

# 6. + armed, FSM limited → "INTERLOCK - FSM LIMIT"
trc3 FCVOTES 0xA6 0x00

# 7. FSM clear, above horizon, no LCH → "INTERLOCK - LCH"
trc3 FCVOTES 0xA6 0x80

# 8. In LCH, no KIZ → "INTERLOCK - KIZ"
trc3 FCVOTES 0xA6 0x84

# 9. Below horizon, FSM clear, no KIZ → "INTERLOCK - KIZ"
trc3 FCVOTES 0xA6 0x81

# 10. Below horizon + KIZ → silent transitioning (green reticle, no message)
trc3 FCVOTES 0xA6 0x83

# 11. Above horizon, LCH + KIZ → silent transitioning
trc3 FCVOTES 0xA6 0x86

# 12. FIRE_STATE set, not firing → "FC ERROR"
trc3 FCVOTES 0xAE 0x80

# 13. All votes — red reticle, "FIRE"
trc3 FCVOTES 0xFE 0x80

# --- Reset ---
trc3 FCVOTES 0x00 0x00
trc3 OSD OFF
```

### OSD STATE/MODE Colour Reference

| Field | Value | Colour |
|-------|-------|--------|
| STATE | OFF | WHITE |
| STATE | STNDBY | DIM_GREY |
| STATE | ISR | BLUE |
| STATE | COMBAT | GREEN |
| STATE | MAINT | YELLOW |
| STATE | FAULT | RED |
| MODE | OFF | WHITE |
| MODE | POS / RATE | BLUE |
| MODE | CUE | YELLOW |
| MODE | ATRACK / FTRACK | GREEN |
| MCC | zero | DIM_GREY |
| MCC | FIRING | RED |
| MCC | TRIGGER | WHITE |
| MCC | ARMED | ORANGE |
| MCC | ABORT active | YELLOW |
| MCC | NOT_ABORT, idle | GREEN |
| BDC | zero | DIM_GREY |
| BDC | FSM limited | RED |
| BDC | FSM clear, geo incomplete | YELLOW |
| BDC | FSM clear, geo clear | GREEN |

> **Note:** `nc -u -w1` sends one UDP packet and exits after 1 s. For interactive sessions use `socat - UDP:$TRC:$PORT`. TRC echoes all `dlog()` output back to the ASCII sender — pipe to a log file for long test sessions: `socat - UDP:$TRC:$PORT | tee trc3_session.log`

---

## MCC Register 1 (REG1)

Sent by MCC on an unsolicited basis (when subscribed via `0xA0 SET_UNSOLICITED`) or in response to a solicited request (`0xA4 {0x01}`, rate-gated 1 Hz per slot). Frame CMD_BYTE is `0xA1` — legacy protocol constant, not a command (pending migration to `0x00` per FW-C10). Fixed block size: **256 bytes**.
Fixed block size: **256 bytes**.

> **Session 4 changes:** `Active CAM ID` removed (not used in MCC). `HB_us` uint32 µs → `HB_ms`
> uint16 ms. `dt_us` uint32 → uint16. `RTC Time` removed (redundant — strip from firmware).
> `RTC HB` byte removed. `RTC` bit (bit 4) in DEVICE_ENABLED/READY_BITS → RES (session 4). Reassigned to `PTP` in session 28 — `isPTP_Enabled` (ENABLED) and `isPTP_Ready` / `ptp.isSynched` (READY).
> `Temp 1 (Charger)` and `Temp 2 (AIR)` float → int8. Battery Pack Voltage → uint16 centi-volts.
> Battery Pack Current → int16 centi-amps. Battery Bus Voltage → uint16 centi-volts.
> Battery Pack Temp → int8. Laser HK Voltage → uint16 centi-volts. Laser Bus Voltage → uint16
> centi-volts. Laser Temperature → int8. TMC embedding 128 → 64 bytes (bytes 66–129).
> All offsets compacted (Option B). Fixed buffer reduced from 512 → 256 bytes.

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES enum |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES enum |
| 3 | 3 | 5 | 2 | HB_ms | uint16 | ms between sends |
| 5 | 5 | 7 | 2 | dt_us | uint16 | µs in processing loop |
| 7 | 7 | 8 | 1 | MCC DEVICE_ENABLED_BITS | uint8 | 0:NTP; 1:TMC; 2:HEL; 3:BAT; 4:PTP; 5:CRG; 6:GNSS; 7:BDC |
| 8 | 8 | 9 | 1 | MCC DEVICE_READY_BITS | uint8 | 0:NTP; 1:TMC; 2:HEL; 3:BAT; 4:PTP; 5:CRG; 6:GNSS; 7:BDC |
| 9 | 9 | 10 | 1 | MCC HEALTH_BITS | uint8 | 0:isReady; 1:isChargerEnabled; 2:isNotBatLowVoltage; 3:isTrainingMode (v3.5.0); 4:isLaserModelMatch (vTBD — compile-time LASER_xK vs runtime ipg.LASER_MODEL_BITS() sense; false until laser connects and model confirmed); 5–7:RES ⚠ **Breaking change v3.4.0** — solenoid/laser bits moved to POWER_BITS byte [10]. Old layout: 1:isSolenoid1_En; 2:isSolenoid2_En; 3:isLaserPower_En; 4:isChargerEnabled; 5:isNotBatLowVoltage. Read HW_REV [254] — layout identical on all revisions. |
| 10 | 10 | 11 | 1 | MCC POWER_BITS | uint8 | Bit N = MCC_POWER enum value N. Unused outputs for active revision always 0 — no guards needed in decoder. 0:isPwr_RelayGps(V1/V3\|0 V2); 1:isPwr_VicorBus(V1/V3\|0 V2); 2:isPwr_RelayLaser(all); 3:isPwr_VicorGim(0 V1\|V2\|V3-6kW only); 4:isPwr_VicorTms(0 V1\|V2\|V3-6kW only); 5:isPwr_SolHel(V1/V3-3kW\|0 V2/V3-6kW); 6:isPwr_SolBda(V1/V3-3kW\|0 V2/V3-6kW); 7:isPwr_RelayNtp(V3 only\|0 V1/V2) ⚠ **Breaking change v3.4.0** — old layout: 0–2:RES; 3:isVicorEnabled; 4:isRelay1En; 5:isRelay2En; 6:isRelay3En; 7:isRelay4En. Read HW_REV [254] to confirm revision before interpreting bits. — vTBD: bit 7 promoted RES→RELAY_NTP; enum renames. |
| 11 | 11 | 12 | 1 | MCC VOTE BITS | uint8 | 0:isLaserTotalHW_Vote_rb; 1:isNotAbort_Vote_rb; 2:isArmed_Vote_rb; 3:isBDA_Vote_rb; 4:isEMON_rb; 5:isLaserFireRequested_Vote; 6:isLaserTotal_Vote_rb; 7:isCombat_Vote_rb |
| 12 | 12 | 20 | 8 | NTP epoch Time | uint64 | ms since epoch |
| 20 | 20 | 21 | 1 | Temp 1 (Charger) | int8 | °C |
| 21 | 21 | 22 | 1 | Temp 2 (AIR) | int8 | °C |
| 22 | 22 | 26 | 4 | TPH: Temp | float | °C |
| 26 | 26 | 30 | 4 | TPH: Pressure | float | Pa |
| 30 | 30 | 34 | 4 | TPH: Humidity | float | % |
| 34 | 34 | 36 | 2 | Battery Pack Voltage | uint16 | centi-volts (e.g. 1260 = 12.60 V) |
| 36 | 36 | 38 | 2 | Battery Pack Current | int16 | centi-amps (e.g. −450 = −4.50 A) |
| 38 | 38 | 40 | 2 | Battery Bus Voltage | uint16 | centi-volts |
| 40 | 40 | 41 | 1 | Battery Pack Temp | int8 | °C |
| 41 | 41 | 42 | 1 | Battery ASOC | uint8 | % |
| 42 | 42 | 43 | 1 | Battery RSOC | uint8 | % |
| 43 | 43 | 45 | 2 | Battery Status Word | int16 | 16 bits |
| 45 | 45 | 47 | 2 | Laser HK Voltage | uint16 | centi-volts |
| 47 | 47 | 49 | 2 | Laser Bus Voltage | uint16 | centi-volts |
| 49 | 49 | 50 | 1 | Laser Temperature | int8 | °C |
| 50 | 50 | 54 | 4 | Laser Status Word | uint32 | |
| 54 | 54 | 58 | 4 | Laser Error Word | uint32 | |
| 58 | 58 | 62 | 4 | Laser SetPoint | float | % |
| 62 | 62 | 66 | 4 | Laser Output Power | float | W |
| 66 | 66 | 130 | 64 | TMC FULL REG | TMC_REG | 64-byte fixed block — see TMC REG1 |
| 130 | 130 | 131 | 1 | TIME HB | uint8 | s/10 — stamped on NTP packet receive. ⚠ Pending: also stamp on PTP sync event; rename `HB_NTP` → `HB_TIME` in firmware + C#. |
| 131 | 131 | 132 | 1 | HEL HB | uint8 | s/10 — laser TCP response interval. `ipg.HB_RX_ms / 100`. Stale > 2.0s → `HEL_STATUS` ERROR. — v3.5.0 |
| 132 | 132 | 133 | 1 | BAT HB | uint8 | s/10 — ⚠ Pending: always 0. Requires `lastMsgRx_ms` on bat class. |
| 133 | 133 | 134 | 1 | CRG HB | uint8 | s/10 — ⚠ Pending: always 0. V1 only (no CRG I2C on V2). Requires `lastMsgRx_ms` on crg class. |
| 134 | 134 | 135 | 1 | GNSS HB | uint8 | s/10 — ⚠ Pending: always 0. Requires `lastMsgRx_ms` on gnss class. |
| 135 | 135 | 136 | 1 | GNSS SOLN STATUS | uint8 | enum |
| 136 | 136 | 137 | 1 | GNSS POS TYPE | uint8 | enum |
| 137 | 137 | 138 | 1 | INS SOLN STATUS | uint8 | enum |
| 138 | 138 | 139 | 1 | TERRA STAR SYNC STATE | uint8 | enum |
| 139 | 139 | 140 | 1 | SIV | uint8 | satellites in solution |
| 140 | 140 | 141 | 1 | SIS | uint8 | satellites in view |
| 141 | 141 | 149 | 8 | GPS Latitude | double | BESTPOS |
| 149 | 149 | 157 | 8 | GPS Longitude | double | BESTPOS |
| 157 | 157 | 165 | 8 | GPS Altitude | double | BESTPOS |
| 165 | 165 | 169 | 4 | GPS Undulation | float | BESTPOS |
| 169 | 169 | 173 | 4 | GPS Heading | float | 2-ant |
| 173 | 173 | 177 | 4 | GPS Roll | float | INSATTX |
| 177 | 177 | 181 | 4 | GPS Pitch | float | INSATTX |
| 181 | 181 | 185 | 4 | GPS Yaw | float | INSATTX |
| 185 | 185 | 189 | 4 | GPS Latitude STDEV | float | BESTPOS |
| 189 | 189 | 193 | 4 | GPS Longitude STDEV | float | BESTPOS |
| 193 | 193 | 197 | 4 | GPS Altitude STDEV | float | BESTPOS |
| 197 | 197 | 201 | 4 | GPS Heading STDEV | float | 2-ant |
| 201 | 201 | 205 | 4 | GPS Roll STDEV | float | INSATTX |
| 205 | 205 | 209 | 4 | GPS Pitch STDEV | float | INSATTX |
| 209 | 209 | 213 | 4 | GPS Yaw STDEV | float | INSATTX |
| 213 | 213 | 217 | 4 | Charger Voltage input | float | V |
| 217 | 217 | 221 | 4 | Charger Voltage output | float | V |
| 221 | 221 | 225 | 4 | Charger Current output | float | A |
| 225 | 225 | 229 | 4 | Fan1 Speed | float | RPM |
| 229 | 229 | 233 | 4 | Fan2 Speed | float | RPM |
| 233 | 233 | 235 | 2 | CHARGE STATUS | uint16 | enum |
| 235 | 235 | 236 | 1 | CHARGE LEVEL | uint8 | enum |
| 236 | 236 | 240 | 4 | Current Limit | float | A |
| 240 | 240 | 244 | 4 | Voltage Limit | float | V |
| 244 | 244 | 245 | 1 | CHARGER STATUS BITS | uint8 | bit0:isConnected; 1:isHealthy; 2:isCharging; 3:isFullyCharged; 4:isHighCharge; 5:is220V |
| 245 | 245 | 249 | 4 | MCC VERSION WORD | uint32 | |
| 249 | 249 | 253 | 4 | MCU Temp | float | °C |
| 253 | 253 | 254 | 1 | TIME_BITS | uint8 | bit0:isPTP_Enabled; bit1:ptp.isSynched; bit2:usingPTP; bit3:ntp.isSynched; bit4:ntpUsingFallback; bit5:ntpHasFallback; bit6–7:RES — session 32 |
| 254 | 254 | 255 | 1 | HW_REV | uint8 | `0x01`=V1 (relay bus/solenoids/charger I2C · 48V · 3kW); `0x02`=V2 (dual Vicor/no solenoids/no I2C · 300V · 6kW); `0x03`=V3 (monolithic PCB · relay bus/solenoids/charger I2C · 48V or 300V · 3kW or 6kW — compile-time `LASER_xK` axis). Read before interpreting HEALTH_BITS [9] and POWER_BITS [10]. — v3.4.0; `0x03` added vTBD |
| 255 | 255 | 256 | 1 | LASER_MODEL | uint8 | `LASER_MODEL` enum. `0x00`=UNKNOWN/not yet sensed; `0x01`=YLM_3K (YLM-3000-SM-VV); `0x02`=YLM_6K (YLM-6000-U3-SM). Was RESERVED. Backwards compatible — old clients reading `0x00` see UNKNOWN. — v3.5.0 |

**Defined: 256 bytes. Reserved: 0 bytes. Fixed block: 256 bytes.**


## BDC Register 1 (REG1)

Sent by BDC on an unsolicited basis (when subscribed via `0xA0 SET_UNSOLICITED`) or in response to a solicited request (`0xA4 {0x01}`, rate-gated 1 Hz per slot). Frame CMD_BYTE is `0xA1` — legacy protocol constant, not a command (pending migration to `0x00` per FW-C10). Fixed block size: **512 bytes**.
Fixed block size: **512 bytes**.

> **Session 4 changes:** `HB_us` uint32 µs → `HB_ms` uint16 ms. `dt_us` uint32 → uint16.
> `RTC Time` removed (redundant — strip from firmware). `RTC` bit (bit 7) in
> DEVICE_ENABLED/READY_BITS → RES. `Vicor Temp` float → int8. TRC block moved: was 72–135,
> now **60–123**. FMC block moved: was 184–247, now **169–232**. All inline TRC field listings
> removed — cross-reference TRC REG1 section for field layout. All offsets compacted (Option B).
>
> **Firmware note:** Update `SEND_REG_01()` copy offsets for TRC (60) and FMC (169).

Embedded sub-registers:
- **TRC_REG** (64-byte fixed block) at bytes **60–123** — see TRC REG1 section
- **FMC_REG** (64-byte fixed block) at bytes **169–232** — see FMC REG1 section

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES enum |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES enum |
| 3 | 3 | 4 | 1 | Active CAM ID | uint8 | VIS=0, MWIR=1 |
| 4 | 4 | 6 | 2 | HB_ms | uint16 | ms between sends |
| 6 | 6 | 8 | 2 | dt_us | uint16 | µs in processing loop |
| 8 | 8 | 9 | 1 | BDC DEVICE_ENABLED_BITS | uint8 | 0:NTP; 1:GIMBAL; 2:FUJI; 3:MWIR; 4:FSM; 5:JETSON; 6:INCL; 7:PTP *(BDC_DEVICES::PTP — session 32)* |
| 9 | 9 | 10 | 1 | BDC DEVICE_READY_BITS | uint8 | 0:NTP; 1:GIMBAL; 2:FUJI; 3:MWIR; 4:FSM; 5:JETSON; 6:INCL; 7:PTP *(ptp.isSynched — session 32)* |
| 10 | 10 | 11 | 1 | BDC HEALTH_BITS | uint8 | 0:isReady; 1:isSwitchEnabled(V2 only, 0 on V1); 2–7:RES ⚠ **Breaking change v3.5.1** — renamed from `BDC STAT BITS`. Old layout: 0:isReady; 1–6:RES; 7:RES (isUnsolicitedModeEnabled retired session 35). Read HW_REV [392] — bit 1 always 0 on V1. |
| 11 | 11 | 12 | 1 | BDC POWER_BITS | uint8 | 0:isPidEnabled; 1:isVPidEnabled; 2:isFTTrackEnabled; 3:isVicorEnabled; 4:isRelay1En; 5:isRelay2En; 6:isRelay3En; 7:isRelay4En ⚠ **Renamed v3.5.1** from `BDC STAT BITS2` — bit layout unchanged. |
| 12 | 12 | 20 | 8 | epoch Time (PTP/NTP) | uint64 | ms since epoch — PTP when synched, NTP otherwise |
| 20 | 20 | 21 | 1 | GIMBAL STATUS BITS | uint8 | 0:isReady; 1:isConnected; 2:isStarted; 3–7:RES |
| 21 | 21 | 25 | 4 | Gimbal Pan Count | int32 | from galil (dr) |
| 25 | 25 | 29 | 4 | Gimbal Tilt Count | int32 | from galil (dr) |
| 29 | 29 | 33 | 4 | Gimbal Pan Speed | int32 | from galil (dr) |
| 33 | 33 | 37 | 4 | Gimbal Tilt Speed | int32 | from galil (dr) |
| 37 | 37 | 38 | 1 | Gimbal Pan Stop Code | uint8 | from galil (dr) |
| 38 | 38 | 39 | 1 | Gimbal Tilt Stop Code | uint8 | from galil (dr) |
| 39 | 39 | 41 | 2 | Gimbal Pan Status | uint16 | from galil (dr) |
| 41 | 41 | 43 | 2 | Gimbal Tilt Status | uint16 | from galil (dr) |
| 43 | 43 | 47 | 4 | Gimbal Pan Rel Angle | float | deg from home |
| 47 | 47 | 51 | 4 | Gimbal Tilt Rel Angle | float | deg from home |
| 51 | 51 | 55 | 4 | Gimbal Az NED Angle | float | AZ NED deg |
| 55 | 55 | 59 | 4 | Gimbal EL NED Angle | float | EL NED deg |
| 59 | 59 | 60 | 1 | TRC STATUS BITS | uint8 | 0:isReady; 1:isConnected; 2:isStarted; 3–7:RES |
| **60** | **60** | **124** | **64** | **TRC REGISTER** | **TRC_REG** | **64-byte fixed block — see TRC REG1** |
| 124 | 124 | 128 | 4 | Gimbal Base Pitch | float | from inclinometer ° |
| 128 | 128 | 132 | 4 | Gimbal Base Roll | float | from inclinometer ° |
| 132 | 132 | 133 | 1 | Vicor Temp | int8 | °C |
| 133 | 133 | 137 | 4 | TPH: Temp | float | °C |
| 137 | 137 | 141 | 4 | TPH: Pressure | float | Pa |
| 141 | 141 | 145 | 4 | TPH: Humidity | float | % |
| 145 | 145 | 146 | 1 | MWIR RUN STATE | uint8 | 0=BOOT; 1=WARMUP_WAIT; 2=WARMUP_VRFY; 3=LENS_INIT; 4=COOLDOWN_WAIT; 5=COOLDOWN_VRFY; 6=SNSR_INIT; 7=MAIN_PROC_LOOP; 8=LENS_REINIT |
| 146 | 146 | 150 | 4 | MWIR Temp 0 | float | sensor 0 °C |
| 150 | 150 | 154 | 4 | MWIR FPA Temp | float | FPA °C |
| 154 | 154 | 155 | 1 | MWIR FOV Selection RB | uint8 | current FOV readback |
| 155 | 155 | 159 | 4 | MWIR FOV | float | degrees |
| 159 | 159 | 160 | 1 | VIS FOV Selection RB | uint8 | current FOV readback |
| 160 | 160 | 164 | 4 | VIS FOV | float | degrees |
| 164 | 164 | 165 | 1 | BDC VOTE BITS1 | uint8 | 0:isHorizVoteOverride; 1:isKIZVoteOverride; 2:isLCHVoteOverride; 3:isBDAVoteOverride; 4:isBelowHoriz; 5:isInKIZ; 6:isInLCH; 7:RES |
| 165 | 165 | 166 | 1 | BDC VOTE BITS2 | uint8 | 0:BelowHorizVote; 1:InKIZVote; 2:InLCHVote; 3:BDCVote; 4:RES; 5:isHorizonLoaded; 6:RES; 7:isFSMLimited |
| 166 | 166 | 167 | 1 | MCC VOTE BITS RB | uint8 | 0:isLaserTotalHW; 1:isNotAbort; 2:isArmed; 3:isBDA; 4:isEMON; 5:isLaserFireRequested; 6:isLaserTotal; 7:isCombat |
| 167 | 167 | 168 | 1 | BDC VOTE BITS KIZ | uint8 | 0:isLoaded; 1:isEnabled; 2:isTimeValid; 3:isOperatorValid; 4:isPositionValid; 5:isForExec; 6:isInKIZ; 7:InKIZVote |
| 168 | 168 | 169 | 1 | BDC VOTE BITS LCH | uint8 | 0:isLoaded; 1:isEnabled; 2:isTimeValid; 3:isOperatorValid; 4:isPositionValid; 5:isForExec; 6:isInLCH; 7:InLCHVote |
| **169** | **169** | **233** | **64** | **FMC REGISTER** | **FMC_REG** | **64-byte fixed block — see FMC REG1** |
| 233 | 233 | 235 | 2 | FSM_X | int16 | commanded FSM X position ⚠ see FSM note |
| 235 | 235 | 237 | 2 | FSM_Y | int16 | commanded FSM Y position |
| 237 | 237 | 241 | 4 | Gimbal Home X | int32 | home encoder X (0 az) |
| 241 | 241 | 245 | 4 | Gimbal Home Y | int32 | home encoder Y (0 el) |
| 245 | 245 | 253 | 8 | Platform Latitude | double | latched |
| 253 | 253 | 261 | 8 | Platform Longitude | double | latched |
| 261 | 261 | 265 | 4 | Platform Altitude | float | latched HAE |
| 265 | 265 | 269 | 4 | Platform Roll | float | degrees |
| 269 | 269 | 273 | 4 | Platform Pitch | float | degrees |
| 273 | 273 | 277 | 4 | Platform Yaw | float | degrees |
| 277 | 277 | 281 | 4 | Target Pan (Cue Track) | int32 | encoder counts |
| 281 | 281 | 285 | 4 | Target Tilt (Cue Track) | int32 | encoder counts |
| 285 | 285 | 289 | 4 | pan kp cue | float | |
| 289 | 289 | 293 | 4 | pan ki cue | float | |
| 293 | 293 | 297 | 4 | pan kd cue | float | |
| 297 | 297 | 301 | 4 | tilt kp cue | float | |
| 301 | 301 | 305 | 4 | tilt ki cue | float | |
| 305 | 305 | 309 | 4 | tilt kd cue | float | |
| 309 | 309 | 313 | 4 | pan kp video | float | |
| 313 | 313 | 317 | 4 | pan ki video | float | |
| 317 | 317 | 321 | 4 | pan kd video | float | |
| 321 | 321 | 325 | 4 | tilt kp video | float | |
| 325 | 325 | 329 | 4 | tilt ki video | float | |
| 329 | 329 | 333 | 4 | tilt kd video | float | |
| 333 | 333 | 341 | 8 | iFOV_FSM_X_DEG_COUNT | double | |
| 341 | 341 | 349 | 8 | iFOV_FSM_Y_DEG_COUNT | double | |
| 349 | 349 | 351 | 2 | FSM_X0 | int16 | |
| 351 | 351 | 353 | 2 | FSM_Y0 | int16 | |
| 353 | 353 | 354 | 1 | FSM_X_SIGN | int8 | |
| 354 | 354 | 355 | 1 | FSM_Y_SIGN | int8 | |
| 355 | 355 | 359 | 4 | STAGE_POSITION | uint32 | |
| 359 | 359 | 363 | 4 | STAGE_HOME | uint32 | |
| 363 | 363 | 367 | 4 | FSM_NED_AZ_RB | float | from readback (noisy) |
| 367 | 367 | 371 | 4 | FSM_NED_EL_RB | float | from readback (noisy) |
| 371 | 371 | 375 | 4 | FSM_NED_AZ_C | float | from command |
| 375 | 375 | 379 | 4 | FSM_NED_EL_C | float | from command |
| 379 | 379 | 383 | 4 | HORIZON_BUFFER | float | |
| 383 | 383 | 387 | 4 | BDC VERSION WORD | uint32 | |
| 387 | 387 | 391 | 4 | MCU Temp | float | °C |
| 391 | 391 | 392 | 1 | TIME_BITS | uint8 | bit0:isPTP_Enabled; bit1:ptp.isSynched; bit2:usingPTP; bit3:ntp.isSynched; bit4:ntpUsingFallback; bit5:ntpHasFallback; bit6–7:RES — session 32 |
| 392 | 392 | 393 | 1 | HW_REV | uint8 | `0x01`=V1 (original hardware); `0x02`=V2 (Controller 1.0 Rev A). Read before interpreting HEALTH_BITS [10] bit 1. — v3.5.1 |
| 393 | 393 | 394 | 1 | TEMP_RELAY | int8 | Relay area NTC thermistor °C. V2 live; V1 always `0x00`. — v3.5.1 |
| 394 | 394 | 395 | 1 | TEMP_BAT | int8 | Battery-in area NTC thermistor °C. V2 live; V1 always `0x00`. — v3.5.1 |
| 395 | 395 | 396 | 1 | TEMP_USB | int8 | USB 5V area NTC thermistor °C. V2 live; V1 always `0x00`. — v3.5.1 |
| 396 | 396 | 397 | 1 | HB_NTP | uint8 | NTP heartbeat — x0.1s units (÷100 at pack; C# /10.0 → seconds). Elapsed since last NTP sync. — v4.0.0 (BDC-HB) |
| 397 | 397 | 398 | 1 | HB_FMC_ms | uint8 | FMC A1 stream heartbeat — raw ms elapsed since last A1 RX, saturates at 255ms. — v4.0.0 (BDC-HB) |
| 398 | 398 | 399 | 1 | HB_TRC_ms | uint8 | TRC A1 stream heartbeat — raw ms elapsed since last A1 RX, saturates at 255ms. — v4.0.0 (BDC-HB) |
| 399 | 399 | 400 | 1 | HB_MCC_ms | uint8 | MCC A1 broadcast heartbeat — raw ms elapsed since last 0xAB RX, saturates at 255ms. — v4.0.0 (BDC-HB) |
| 400 | 400 | 401 | 1 | HB_GIM_ms | uint8 | Gimbal heartbeat — raw ms elapsed since last Galil data record RX, saturates at 255ms. — v4.0.0 (BDC-HB) |
| 401 | 401 | 402 | 1 | HB_FUJI_ms | uint8 | Fuji lens heartbeat — raw ms elapsed since last C10 serial response, saturates at 255ms. — v4.0.0 (BDC-HB) |
| 402 | 402 | 403 | 1 | HB_MWIR_ms | uint8 | MWIR camera heartbeat — raw ms elapsed since last serial response, saturates at 255ms. — v4.0.0 (BDC-HB) |
| 403 | 403 | 404 | 1 | HB_INCL_ms | uint8 | Inclinometer heartbeat — raw ms elapsed since last UART frame, saturates at 255ms. — v4.0.0 (BDC-HB) |
| 404 | 404 | 512 | 108 | RESERVED | — | 0x00 — headroom to 512 |

**Defined: 404 bytes. Reserved: 108 bytes. Fixed block: 512 bytes.**

> ⚠ **FSM position note:** `FSM_X/Y` (int16, commanded) differs from `FSM Pos X/Y` in the FMC
> pass-through (int32, ADC readback). Reconciliation pending — see Action Items.

## TMC Register 1 (REG1)

Sent by TMC directly (engineering access) and passed through MCC REG1 as a 64-byte fixed block
(`tmc.buffer`, bytes 66–129 in MCC REG1). Fixed block size: **64 bytes**.

> **Session 4 changes:** Reserved 0xFF byte (byte 3) removed. `HB_us` uint32 µs → `HB_ms`
> uint16 ms. `dt_us` uint32 → uint16. `RTC Time` removed (redundant — strip from firmware).
> `ta2`, `tr1`, `tr2` removed (deprecated sensors). All temps float → int8 (range −20 to +100°C).
> `f1`/`f2` float → uint8 ×10 LPM. Fixed block reduced from 256 → 64 bytes. All offsets
> compacted (Option B).
>
> **Firmware note (session 27):** `isRTCInit` (TMC STAT BITS1 bit 6) was deprecated → `ntpUsingFallback`.
> **Firmware note (session 30/31):** TMC STAT BITS1 bits 5/6 vacated — `isNTPSynched` and `ntpUsingFallback` moved to new TMC STAT BITS3 (byte 61). Byte 61 was previously RESERVED. All PTP and NTP time status is now consolidated in BITS3.
> **Firmware note (session 30 — 2026-04-07):** TMC unified V1/V2 hardware abstraction. Byte [7] STAT BITS1 bits 5/6 now hardware-conditional (see below). Byte [17–18] and bytes [39–40] are revision-dependent. Byte [62] promoted from RESERVED to HW_REV. Read byte [62] before interpreting bits 5/6 of byte [7].

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES enum |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES enum |
| 3 | 3 | 5 | 2 | HB_ms | uint16 | ms between sends |
| 5 | 5 | 7 | 2 | dt_us | uint16 | µs in processing loop |
| 7 | 7 | 8 | 1 | TMC STAT BITS1 | uint8 | 0:isReady; 1:isPumpEnabled(V1)/isPump1Enabled(V2); 2:isHeaterEnabled(V1,always 0 on V2); 3:isInputFan1Enabled; 4:isInputFan2Enabled; 5:RES(V1)/isPump2Enabled(V2) ⚠ read HW_REV[62] first; 6:isSingleLoop (compile-time, both revisions); 7:RES *(was isUnsolicitedModeEnabled — retired session 35)* |
| 8 | 8 | 9 | 1 | TMC STAT BITS2 | uint8 | 0:isVicor1Enabled; 1:isLCM1Enabled; 2:isLCM1Error; 3:isFlow1Error; 4:isVicor2Enabled; 5:isLCM2Enabled; 6:isLCM2Error; 7:isFlow2Error |
| 9 | 9 | 17 | 8 | epoch Time (PTP/NTP) | uint64 | ms since Unix epoch — PTP when synched, NTP otherwise |
| 17 | 17 | 19 | 2 | Pump Speed | uint16 | V1: DAC counts [0–800] (Vicor trim); V2: 0x0000 reserved (TRACO PSUs on/off only) |
| 19 | 19 | 21 | 2 | LCM1 Speed Setting | uint16 | DAC counts [0–4095] — active both revisions |
| 21 | 21 | 23 | 2 | LCM1 Current Readback | uint16 | [0–4095] |
| 23 | 23 | 25 | 2 | LCM2 Speed Setting | uint16 | DAC counts [0–4095] — active both revisions |
| 25 | 25 | 27 | 2 | LCM2 Current Readback | uint16 | [0–4095] |
| 27 | 27 | 28 | 1 | f1 | uint8 | flow rate ×10 LPM (e.g. 15 = 1.5 LPM) |
| 28 | 28 | 29 | 1 | f2 | uint8 | flow rate ×10 LPM |
| 29 | 29 | 30 | 1 | tt | int8 | target temp setpoint °C [10–40, clamped] |
| 30 | 30 | 31 | 1 | ta1 | int8 | air temp 1 °C |
| 31 | 31 | 32 | 1 | tf1 | int8 | °C |
| 32 | 32 | 33 | 1 | tf2 | int8 | °C |
| 33 | 33 | 34 | 1 | tc1 | int8 | temp compressor 1 °C |
| 34 | 34 | 35 | 1 | tc2 | int8 | temp compressor 2 °C |
| 35 | 35 | 36 | 1 | to1 | int8 | temp output ch1 °C |
| 36 | 36 | 37 | 1 | to2 | int8 | temp output ch2 °C |
| 37 | 37 | 38 | 1 | tv1 | int8 | temp vicor LCM1 °C |
| 38 | 38 | 39 | 1 | tv2 | int8 | temp vicor LCM2 °C |
| 39 | 39 | 40 | 1 | tv3 | int8 | V1: temp vicor heater °C (ADS1015); V2: 0x00 reserved (sensor and hardware removed) |
| 40 | 40 | 41 | 1 | tv4 | int8 | V1: temp vicor pump °C (ADS1015); V2: 0x00 reserved (sensor and Vicor pump removed) |
| 41 | 41 | 45 | 4 | TPH: Temp | float | °C |
| 45 | 45 | 49 | 4 | TPH: Pressure | float | Pa |
| 49 | 49 | 53 | 4 | TPH: Humidity | float | % |
| 53 | 53 | 57 | 4 | TMC VERSION WORD | uint32 | VERSION_PACK — V1/V2 unified: `3.3.0` = `0x03003000` |
| 57 | 57 | 61 | 4 | MCU Temp | float | °C |
| 61 | 61 | 62 | 1 | TMC STAT BITS3 | uint8 | 0:isPTP_Enabled; 1:isPTP_Synched (ptp.isSynched); 2:usingPTP (active source); 3:isNTPSynched; 4:ntpUsingFallback; 5:ntpHasFallback; 6–7:RES |
| 62 | 62 | 63 | 1 | HW_REV | uint8 | 0x01=V1 (Vicor/ADS1015); 0x02=V2 (TRACO/direct). Read before interpreting STAT BITS1 bits 5/6 and bytes [17–18], [39–40]. |
| 63 | 63 | 64 | 1 | RESERVED | — | 0x00 |

**Defined: 63 bytes. Reserved: 1 byte. Fixed block: 64 bytes.**

---

## FMC Register 1 (REG1)

Sent by FMC directly (engineering access) and passed through BDC REG1 as a 64-byte fixed block
(`fmc.buffer`, bytes 169–232 in BDC REG1). Fixed block size: **64 bytes**.

> **Session 4 changes:** `HB_us` uint32 µs → `HB_ms` uint16 ms. `dt_us` uint32 → uint16.
> `RTC Time` removed (redundant — NTP is authoritative across all controllers; strip from firmware).
> Fixed block reduced from 128 → 64 bytes to match TRC and TMC. All offsets compacted (Option B).

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | CMD BYTE | uint8 | 0xA1 |
| 1 | 1 | 2 | 1 | System State | uint8 | SYSTEM_STATES enum |
| 2 | 2 | 3 | 1 | System Mode | uint8 | BDC_MODES enum |
| 3 | 3 | 5 | 2 | HB_ms | uint16 | ms between sends |
| 5 | 5 | 7 | 2 | dt_us | uint16 | µs in processing loop |
| 7 | 7 | 8 | 1 | FMC HEALTH_BITS | uint8 | 0:isReady; 1–7:RES ⚠️ **Breaking change v3.5.2** — renamed from `FSM STAT BITS`. `isFSM_Powered` (was bit 1) and `isStageEnabled` (was bit 6) moved to POWER_BITS byte [46]. Read HW_REV [45] to confirm revision. |
| 8 | 8 | 12 | 4 | Stage Pos | uint32 | counts; dist = (counts − 15000) / 0.5 mm |
| 12 | 12 | 16 | 4 | Stage Err | uint32 | error mask |
| 16 | 16 | 20 | 4 | Stage Status | uint32 | status mask (24 bits used) |
| 20 | 20 | 24 | 4 | FSM Pos X | int32 | ADC readback counts ⚠ see FSM position note |
| 24 | 24 | 28 | 4 | FSM Pos Y | int32 | ADC readback counts |
| 28 | 28 | 36 | 8 | epoch Time (PTP/NTP) | uint64 | ms since epoch — PTP when synched, NTP otherwise |
| 36 | 36 | 40 | 4 | FMC VERSION WORD | uint32 | |
| 40 | 40 | 44 | 4 | MCU Temp | float | °C |
| 44 | 44 | 45 | 1 | TIME_BITS | uint8 | bit0:isPTP_Enabled; bit1:ptp.isSynched; bit2:usingPTP; bit3:ntp.isSynched; bit4:ntpUsingFallback; bit5:ntpHasFallback; bit6–7:RES — session 33 |
| 45 | 45 | 46 | 1 | HW_REV | uint8 | `0x01`=V1 (SAMD21 legacy); `0x02`=V2 (STM32F7 / OpenCR). Read before interpreting HEALTH_BITS [7] and POWER_BITS [46]. — v3.5.2 |
| 46 | 46 | 47 | 1 | FMC POWER_BITS | uint8 | bit0=`isFSM_Powered`; bit1=`isStageEnabled`; bits2–7=RES. Mirrors MCC/BDC POWER_BITS pattern. — v3.5.2 |
| 47 | 47 | 51 | 4 | TPH: Temp | float | °C — BME280 ambient temperature. **V2 only** (V1 = 0.0). Cadence ~0.5 Hz. — v4.0.0 (FMC-TPH) |
| 51 | 51 | 55 | 4 | TPH: Pressure | float | Pa — BME280 ambient pressure. **V2 only** (V1 = 0.0). — v4.0.0 (FMC-TPH) |
| 55 | 55 | 59 | 4 | TPH: Humidity | float | % — BME280 relative humidity. **V2 only** (V1 = 0.0). — v4.0.0 (FMC-TPH) |
| 59 | 59 | 64 | 5 | RESERVED | — | 0x00 — headroom for future fields |

**Defined: 59 bytes. Reserved: 5 bytes. Fixed block: 64 bytes.**

> ⚠ **FSM position note:** `FSM Pos X/Y` here (int32, ADC readback) differs from `FSM_X/Y`
> in BDC REG1 (int16, commanded). Reconciliation pending — see Action Items.

---

## TRC Register 1 (REG1)

Sent by TRC/Orin directly (engineering access or solicited) and as 100 Hz unsolicited telemetry
to BDC (`Defaults::BDC_HOST = 192.168.1.20`, port 5010). Passed through BDC REG1 as a 64-byte
fixed block (bytes 60–123). Fixed block size: **64 bytes**.

> **Session 4 changes:** ControlByte (stale, always 0xAA) removed. `hb_ms` float→uint16 ms.
> `focusScore` double→float. `Gain` and `Exposure` removed (OSD-managed, not register fields).
> All offsets compacted (Option B). `static_assert(sizeof(TelemetryPacket) == 64)` must be
> re-verified after struct update. BDC embedding updated: was bytes 72–135, now bytes 60–123.

> **Session 15 changes:** `version.hpp` deleted from TRC. `version.h` (shared FW header) adopted as
> single source of truth for all 5 controllers. `GlobalState::version_word` is now a plain `uint32_t`
> initialised to `VERSION_PACK(3,0,1) = 0x03000001`. Wire format unchanged — `version_word` field at
> bytes [1–4] still carries `VERSION_PACK(major, minor, patch)`.
>
> **Version word encoding — controller comparison (CB-20260412):**
> - **All five controllers:** `VERSION_PACK(4,0,0)` = `0x04000000` — CB-20260412 fleet-wide version bump
> - **IsV4 gate:** `FW_VERSION >> 24 >= 4` — clients use this to detect ICD v3.6.0 command space
>
> *(Previous versions: TRC `3.0.2`, MCC `3.3.1`, BDC `3.3.1`, TMC `3.3.4`, FMC `3.3.0` — all superseded)*

> **Implementation note:** `ntpEpochTime` is populated from `std::chrono::system_clock`,
> NTP-synchronized at the Jetson OS level.

> **Encoding notes:**
> - `fps` = framerate × 100 (e.g. 6000 = 60 Hz). Unpack: `value / 100.0f`
> - `nccScore` = NCC quality × 10000. Unpack: `value / 10000.0f`
> - `camid` uses `BDC_CAM_IDS` encoding: VIS=0, MWIR=1

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 0 | 0 | 1 | 1 | cmd_byte | uint8 | always 0xA1 |
| 1 | 1 | 5 | 4 | version_word | uint32 | TRC firmware version |
| 5 | 5 | 6 | 1 | systemState | uint8 | SYSTEM_STATES enum |
| 6 | 6 | 7 | 1 | systemMode | uint8 | BDC_MODES enum |
| 7 | 7 | 9 | 2 | HB_ms | uint16 | ms between sends |
| 9 | 9 | 11 | 2 | dt_us | uint16 | µs in processing loop |
| 11 | 11 | 12 | 1 | overlayMask | uint8 | bit0=Reticle; 1=TrackPreview; 2=TrackBox; 3=CueChevrons; 4=AC_Proj; 5=AC_Leaders; 6=FocusScore; 7=OSD |
| 12 | 12 | 14 | 2 | fps | uint16 | framerate × 100 — unpack: value / 100.0f |
| 14 | 14 | 16 | 2 | deviceTemperature | int16 | VIS camera sensor temp °C |
| 16 | 16 | 17 | 1 | camid | uint8 | VIS=0, MWIR=1 (BDC_CAM_IDS) |
| 17 | 17 | 18 | 1 | status_cam0 | uint8 | Alvium: bit0=Started; 1=Active; 2=Capturing; 3=Tracking; 4=TrackValid; 5=FocusScoreEnabled; 6=OSDEnabled; 7=cueFlag |
| 18 | 18 | 19 | 1 | status_track_cam0 | uint8 | Alvium tracker: bit2=Enabled; 3=Valid; 4=Initializing |
| 19 | 19 | 20 | 1 | status_cam1 | uint8 | MWIR status (same bit layout as status_cam0) |
| 20 | 20 | 21 | 1 | status_track_cam1 | uint8 | MWIR tracker: bit2=Enabled; 3=Valid; 4=Initializing |
| 21 | 21 | 23 | 2 | tx | int16 | tracker centre x (AT-offset adjusted) |
| 23 | 23 | 25 | 2 | ty | int16 | tracker centre y (AT-offset adjusted) |
| 25 | 25 | 26 | 1 | atX0 | int8 | AT offset x |
| 26 | 26 | 27 | 1 | atY0 | int8 | AT offset y |
| 27 | 27 | 28 | 1 | ftX0 | int8 | FT offset x |
| 28 | 28 | 29 | 1 | ftY0 | int8 | FT offset y |
| 29 | 29 | 33 | 4 | focusScore | float | Laplacian variance |
| 33 | 33 | 41 | 8 | ntpEpochTime | int64 | ms since epoch (OS NTP-synced) |
| 41 | 41 | 42 | 1 | voteBitsMcc | uint8 | MCC fire control vote bits readback (0xAB) |
| 42 | 42 | 43 | 1 | voteBitsBdc | uint8 | BDC geometry vote bits readback (0xAB) |
| 43 | 43 | 45 | 2 | nccScore | int16 | NCC quality × 10000 — unpack: value / 10000.0f |
| 45 | 45 | 46 | 1 | jetsonTemp | uint8 | Jetson CPU temp °C (0–255). Read from `/sys/devices/virtual/thermal/thermal_zone0/temp` (millidegrees ÷ 1000). — v4.2.0 (CB-20260419c): int16→uint8 |
| 46 | 46 | 47 | 1 | jetsonCpuLoad | uint8 | Jetson CPU load % (0–100). Computed from `/proc/stat` delta. — v4.2.0 (CB-20260419c): int16→uint8, shifted from [47–48] |
| 47 | 47 | 48 | 1 | jetsonGpuLoad | uint8 | Jetson GPU load % (0–100). Read from `/sys/devices/platform/gpu.0/load` (0–1000 ÷ 10). — v4.2.0 (CB-20260419c): int16→uint8, moved from [57–58] |
| 48 | 48 | 49 | 1 | jetsonGpuTemp | uint8 | Jetson GPU temp °C (0–255). Read from `/sys/devices/virtual/thermal/thermal_zone1/temp` (millidegrees ÷ 1000). Polled every 5s. — v4.2.0 (CB-20260419c): NEW |
| 49 | 49 | 57 | 8 | som_serial | uint64 | Jetson SOM serial — read once at TRC startup from `/proc/device-tree/serial-number`, parsed as decimal via `std::stoull`. 0 on parse failure or missing file. — v4.0.0 (TRC-SOM-SN) |
| 57 | 57 | 64 | 7 | RESERVED | — | 0x00 — headroom for future fields |

**Defined: 57 bytes. Reserved: 7 bytes. Fixed block: 64 bytes.**

---

## Key Enumerations

> **Canonical source:** All enumerations below are defined in `defines.hpp` (C++) and `defines.cs` (C#, namespace `CROSSBOW`), both v3.X.Y. Enum names and values are identical across all 5 controllers (MCC, BDC, TMC, FMC, TRC), THEIA HMI, and CROSSBOW_ENG_GUIS. Exception: `TMC_DAC_CHANNELS` is absent from FW `pin_defs_tmc.hpp` (use `defines.hpp` instead).

### SYSTEM_STATES
| Value | Name |
|-------|------|
| 0 | OFF |
| 1 | STNDBY |
| 2 | ISR |
| 3 | COMBAT |
| 4 | MAINT |
| 5 | FAULT |

### BDC_MODES
| Value | Name |
|-------|------|
| 0 | OFF |
| 1 | POS |
| 2 | RATE |
| 3 | CUE |
| 4 | ATRACK |
| 5 | FTRACK |

### BDC_CAM_IDS
| Value | Name |
|-------|------|
| 0 | VIS (Alvium) |
| 1 | MWIR |

### BDC_TRACKERS — `0xDB ORIN_ACAM_ENABLE_TRACKERS` tracker_id
| Value | Enum | Name | Status |
|-------|------|------|--------|
| 0 | `AI` | TrackA (AI/DNN) | Phase 4 — COCO intra-trackbox (COCO-01 implemented, HW validation pending) |
| 1 | `MOSSE` | TrackB (MOSSE) | ✓ Active — primary operational tracker |
| 2 | `CENTROID` | TrackC (Centroid) | Not implemented |
| 3 | `KALMAN` | Kalman | Not implemented |

### AF_MODES — `0xCD CMD_MWIR_AF_MODE` payload
| Value | Enum | Meaning |
|-------|------|---------|
| 0 | `OFF` | AF disabled |
| 1 | `CONT` | Continuous AF |
| 2 | `ONCE` | Single AF trigger |

### VIEW_MODES — `0xDE ORIN_SET_VIEW_MODE` payload
| Value | Enum | Meaning |
|-------|------|---------|
| 0 | `CAM1` | VIS camera full frame |
| 1 | `CAM2` | MWIR camera full frame |
| 2 | `PIP4` | Picture-in-picture 1/4 scale |
| 3 | `PIP8` | Picture-in-picture 1/8 scale |

### MWIR_RUN_STATES — BDC REG1 byte 145
| Value | Enum | Meaning |
|-------|------|---------|
| 0 | `BOOT` | Initialising |
| 1 | `WARMUP_WAIT` | Waiting for warmup |
| 2 | `WARMUP_VRFY` | Verifying warmup |
| 3 | `LENS_INIT` | Lens initialising |
| 4 | `COOLDOWN_WAIT` | Waiting for cooldown |
| 5 | `COOLDOWN_VRFY` | Verifying cooldown |
| 6 | `SNSR_INIT` | Sensor initialising |
| 7 | `MAIN_PROC_LOOP` | Normal operation |
| 8 | `LENS_REINIT` | Lens re-initialising |

### TMC_DAC_CHANNELS — `0xE8 TMS_SET_DAC_VALUE` dac payload byte 0
| Value | Enum | Channel |
|-------|------|---------|
| 0x00 | `LCM1` | LCM 1 |
| 0x02 | `LCM2` | LCM 2 |
| 0x04 | `PUMP` | Pump (V1 — single Vicor, both pumps in parallel) |
| 0x06 | `HEATER` | Heater (V1 only — hardware removed in V2) |
| 0x0B | `MCP4728_WIPER` | MCP4728 wiper |
| 0x10 | `MCP4728_CHANNEL_A` | MCP4728 channel A |
| 0x12 | `MCP4728_CHANNEL_B` | MCP4728 channel B |
| 0x14 | `MCP4728_CHANNEL_C` | MCP4728 channel C |
| 0x16 | `MCP4728_CHANNEL_D` | MCP4728 channel D |

### HUD_OVERLAY_BITS — `0xD3 ORIN_SET_STREAM_OVERLAYS` + TRC REG1 overlayMask [11]
| Bit | Mask | Name |
|-----|------|------|
| 0 | 0x01 | Reticle |
| 1 | 0x02 | TrackPreview |
| 2 | 0x04 | TrackBox |
| 3 | 0x08 | CueChevrons |
| 4 | 0x10 | AC_Projections |
| 5 | 0x20 | AC_LeaderLines |
| 6 | 0x40 | FocusScore |
| 7 | 0x80 | OSD |

### MCC_POWER — `0xE2 PMS_POWER_ENABLE` which payload byte + POWER_BITS byte [10] bit index
*v3.4.0 — replaces `MCC_SOLENOIDS`, `MCC_RELAYS`, `MCC_VICORS` (all removed from `defines.hpp`/`defines.cs`)*
*vTBD — enum renames (bit positions unchanged); bit 7 promoted RES→RELAY_NTP; LASER_xK compile axis added*

Enum value N = `POWER_BITS` byte [10] bit N. Used as `which` payload byte in `0xE2 PMS_POWER_ENABLE`.

| Value | Enum | V1 | V2 | V3 · 3kW | V3 · 6kW | Description |
|-------|------|----|----|----------|----------|-------------|
| 0 | `RELAY_GPS` | ✅ pin 83 | — | ✅ pin 67 | ✅ pin 67 | GPS appliance power — NO opto HIGH=ON |
| 1 | `VICOR_BUS` | ✅ A0 LOW=ON | — | ✅ pin 40 HIGH=ON | ✅ pin 40 · PC open · fed from VICOR_TMS | Relay bus supply Vicor. Polarity inverts V1→V3. |
| 2 | `RELAY_LASER` | ✅ pin 20 · 3kW bus | ✅ pin 83 · 6kW enable | ✅ pin 54 · 3kW bus | ✅ pin 63 `PIN_ENERGIZE` · ext PSU 5V | Laser enable. V3 pin dispatched by `LASER_xK` compile flag. |
| 3 | `VICOR_GIM` | — | ✅ A0 NC HIGH=ON · 300V→48V | — | ✅ pin 55 HIGH=ON · 300V→48V | Gimbal Vicor. Sets `isBDC_Ready` on V2/V3-6kW. |
| 4 | `VICOR_TMS` | — | ✅ pin 20 NC HIGH=ON · 300V→48V | — | ✅ pin 51 HIGH=ON · PC always open | TMS+board supply Vicor. V3: no independent fw switch. |
| 5 | `SOL_HEL` | ✅ pin 5 · 3kW HV bus | — | ✅ pin 5 · 3kW HV bus | — | Laser HV bus solenoid — electromechanical. |
| 6 | `SOL_BDA` | ✅ pin 8 · gimbal | — | ✅ pin 50 · gimbal | — | Gimbal power solenoid. Sets `isBDC_Ready` on V1/V3-3kW. |
| 7 | `RELAY_NTP` | — | — | ✅ pin 56 HIGH=ON | ✅ pin 56 HIGH=ON | Phoenix NTP appliance power — NO opto. **New V3.** |

> V1 valid: 0, 1, 2, 5, 6. V2 valid: 2, 3, 4. V3-3kW valid: 0, 1, 2, 5, 6, 7. V3-6kW valid: 0, 1, 2, 3, 4, 7. Invalid `which` for active revision → `STATUS_CMD_REJECTED`.
> **Enum renames (vTBD):** `GPS_RELAY→RELAY_GPS`, `LASER_RELAY→RELAY_LASER`, `GIM_VICOR→VICOR_GIM`, `TMS_VICOR→VICOR_TMS`. Bit positions unchanged — no wire-level breaking change. C# backward-compat aliases retained in `MSG_MCC.cs`.

### LASER_MODEL — MCC REG1 byte [255]
*v3.5.0 — was RESERVED. Sensed via `RMN` command on laser connect. Written by firmware `SEND_REG_01()`. Read by `MSG_MCC.cs` and `MSG_IPG.cs`.*

| Value | Enum | Laser | Max Power |
|-------|------|-------|-----------|
| 0x00 | `UNKNOWN` | Not yet sensed / sense fault | 0 W |
| 0x01 | `YLM_3K` | YLM-3000-SM-VV | 3000 W |
| 0x02 | `YLM_6K` | YLM-6000-U3-SM | 6000 W |

### DEBUG_LEVELS — firmware serial debug verbosity
*Present in `defines.hpp` since v3.X.Y; added to `defines.cs` v3.5.0.*

| Value | Enum | Meaning |
|-------|------|---------|
| 0 | `OFF` | No debug output |
| 1 | `MIN` | Minimal — connect/disconnect/state changes |
| 2 | `NORM` | Normal — command/response logging |
| 3 | `VERBOSE` | Verbose — per-packet detail |

### VOTE_BITS_MCC — `0xE0 SET_BCAST_FIRECONTROL_STATUS` byte 1 + TRC REG1 [41]
| Bit | Name | Notes |
|-----|------|-------|
| 1 | NotAbort | **Inverted** — 0 = abort ACTIVE (safe-by-default) |
| 2 | Armed | Weapon armed |
| 3 | BDAVote | LOS clear, system may fire |
| 4 | Firing | Laser energized |
| 5 | Trigger | Trigger pulled |
| 6 | FireState | FC all votes passed |
| 7 | Combat | System in combat mode |

### VOTE_BITS_BDC — `0xE0 SET_BCAST_FIRECONTROL_STATUS` byte 2 + TRC REG1 [42]
| Bit | Name | Notes |
|-----|------|-------|
| 0 | BelowHorizon | Platform geometry — below horizon |
| 1 | InKIZ | Within KIZ window |
| 2 | InLCH | Within LCH window |
| 3 | BDCTotalVote | All BDC geometry votes pass — system may fire |
| 5 | HorizLoaded | Horizon elevation data loaded |
| 7 | FSMNotLimited | FSM not at travel limit |

---

---


## MSG_MCC.cs — Session 28/29 C# Additions and v3.4.0 Updates

The following properties were added to `MSG_MCC.cs` in session 28/29. All are read-only,
populated from the parsed register bytes on each `Parse()` call.

### New Enum (session 28)

```csharp
public enum TIME_SOURCE { None, PTP, NTP }
```

### New Properties (session 28/29)

| Property | Type | Source | Description |
|----------|------|--------|-------------|
| `epochTime` | `DateTime` | bytes [12–19] | Preferred alias — reflects active source (PTP or NTP). UTC. |
| `ntpTime` | `DateTime` | bytes [12–19] | Backward-compat alias → `epochTime` |
| `activeTimeSource` | `TIME_SOURCE` | derived | PTP if `isPTP_DeviceReady && usingPTP`; NTP if `isNTP_DeviceReady`; else None |
| `activeTimeSourceLabel` | `string` | derived | `"PTP"` / `"NTP"` / `"NTP (fallback)"` / `"NONE"` |
| `isPTP_DeviceEnabled` | `bool` | DEVICE_ENABLED bit 4 | PTP client enabled on MCC |
| `isPTP_DeviceReady` | `bool` | DEVICE_READY bit 4 | `ptp.isSynched` — PTP has valid lock |
| `ntpUsingFallback` | `bool` | TIME_BITS bit 4 | NTP is currently using fallback server |
| `ntpHasFallback` | `bool` | TIME_BITS bit 5 | Fallback server is configured |
| `usingPTP` | `bool` | TIME_BITS bit 2 | PTP is the active time source |

### v3.4.0 Additions (MCC unification) + vTBD (V3 additions)

| Property | Type | Source | Description |
|----------|------|--------|-------------|
| `HW_REV` | `byte` | byte [254] | Hardware revision: `0x01`=V1, `0x02`=V2, `0x03`=V3 (vTBD) |
| `IsV1` | `bool` | derived | `HW_REV == 0x01` |
| `IsV2` | `bool` | derived | `HW_REV == 0x02` |
| `IsV3` | `bool` | derived | `HW_REV == 0x03` — vTBD |
| `HW_REV_Label` | `string` | derived | Human-readable revision description |
| `HealthBits` | `byte` | byte [9] | Renamed from `StatusBits` — health only: isReady, isChargerEnabled, isNotBatLowVoltage |
| `PowerBits` | `byte` | byte [10] | Renamed from `StatusBits2` — full power map: bit N = `MCC_POWER` value N |
| `StatusBits` | `byte` | alias | Backward-compat → `HealthBits` |
| `StatusBits2` | `byte` | alias | Backward-compat → `PowerBits` |
| `isReady` | `bool` | HealthBits bit 0 | MCC system ready |
| `isCharger_Enabled` | `bool` | HealthBits bit 1 | Charger GPIO enabled |
| `isNotBatLowVoltage` | `bool` | HealthBits bit 2 | Bus voltage ≥ 47V |
| `isHEL_TrainingMode` | `bool` | HealthBits bit 3 | Training mode active — power clamped to 10% (v3.5.0) |
| `isLaserModelMatch` | `bool` | HealthBits bit 4 | Compile-time `LASER_xK` matches runtime sensed model. false until laser connects. See DEVICE_READY bit 2 for combined diagnostic. — vTBD |
| `pb_RelayGps` | `bool` | PowerBits bit 0 | GPS relay state (V1/V3 live, V2 always false) — vTBD rename from `pb_GpsRelay` |
| `pb_VicorBus` | `bool` | PowerBits bit 1 | Relay bus Vicor state (V1/V3 live, V2 always false) |
| `pb_RelayLaser` | `bool` | PowerBits bit 2 | Laser relay/enable state (all revisions) — vTBD rename from `pb_LaserRelay` |
| `pb_VicorGim` | `bool` | PowerBits bit 3 | GIM Vicor state (V2/V3-6kW live, V1/V3-3kW always false) — vTBD rename from `pb_GimVicor` |
| `pb_VicorTms` | `bool` | PowerBits bit 4 | TMS Vicor state (V2/V3-6kW live, V1/V3-3kW always false) — vTBD rename from `pb_TmsVicor` |
| `pb_SolHel` | `bool` | PowerBits bit 5 | HEL solenoid state (V1/V3-3kW live, V2/V3-6kW always false) |
| `pb_SolBda` | `bool` | PowerBits bit 6 | BDA solenoid state (V1/V3-3kW live, V2/V3-6kW always false) |
| `pb_RelayNtp` | `bool` | PowerBits bit 7 | NTP relay state (V3 live, V1/V2 always false) — vTBD new |

> **Compat aliases retained** — `pb_GpsRelay`, `pb_LaserRelay`, `pb_GimVicor`, `pb_TmsVicor` → renamed counterparts above. `isVicor_Enabled`, `isRelay1_Enabled`, `isRelay2_Enabled` revision-aware. `isRelay3/4_Enabled` return `false` permanently.
> **Removed** — `isUnSolicitedMode_Enabled` (retired session 35). `MCC_SOLENOIDS`, `MCC_RELAYS`, `MCC_VICORS` removed from `defines.cs` — use `MCC_POWER`.
> **⚠ `MCC_DEVICES` enum:** C# enum `RTCLOCK=4` — update to `PTP=4`. `isPTP_DeviceEnabled/Ready` currently use literal `4`.

### Bug Fixed (session 29)

`SEND_REG_01` in `mcc.cpp` was calling `ntp.GetCurrentTime()` directly at bytes [12–19],
bypassing the `GetCurrentTime()` routing method. ENG GUI received `1970-01-01` when PTP was
active and NTP was unsynched. Fixed — all four call sites now use `GetCurrentTime()`.

---

## MSG_IPG.cs — v3.5.0 Additions

### New Properties

| Property | Type | Access | Description |
|----------|------|--------|-------------|
| `LaserModel` | `LASER_MODEL` | `public set` | Sensed laser model. Set by `Parse()` from MCC REG1 byte [255] (MCC path) or by `ParseDirect()` from `RMN` response (ENG GUI direct path). Default `UNKNOWN`. |
| `ModelName` | `string` | `public set` | Full model name string from `RMN` response (e.g. `"YLM-3000-SM-VV"`). ENG GUI path only — empty on MCC framed path until Step 2. |
| `SerialNumber` | `string` | `public set` | Serial number from `RSN` response. ENG GUI path only — empty on MCC framed path until Step 2. |
| `IsSensed` | `bool` | derived | `LaserModel != UNKNOWN` |
| `MaxPower_W` | `int` | derived | `LaserModel.MaxPower_W()` — 3000 (3K), 6000 (6K), 0 (UNKNOWN) |
| `IsEMON` | `bool` | derived | Emission ON — normalized across models. 3K=`StatusWord` bit 0; 6K=`StatusWord` bit 2 |
| `IsNotReady` | `bool` | derived | Not-ready — normalized across models. 3K=`StatusWord` bit 9; 6K=`StatusWord` bit 11 (PSU off) |

### Updated Properties

| Property | Change |
|----------|--------|
| `PowerSetting_W` | Was `SetPoint / 100.0 * 3000` (hardcoded 3K). Now `SetPoint / 100.0 * MaxPower_W` (model-aware). Old line retained as comment. |

### New Methods

**`ParseDirect(string cmd, string payload)`** — ASCII TCP response path for ENG GUI direct laser connection. Routes by `cmd`:

| cmd | Action |
|-----|--------|
| `RMODEL` | Parse model string (e.g. `YLM-3000-SM-VV`) → set `LaserModel` + `ModelName`. Power field extracted from second `-` delimited token: `3000`→`YLM_3K`, `6000`→`YLM_6K`. |
| `RMN` | Read Machine Name (hostname) — logged only, not used for sense. |
| `RSN` | Set `SerialNumber` |
| `RHKPS` | Set `HKVoltage` (double, V) |
| `RBSTPS` | Set `BusVoltage` (double, V) |
| `RCT` | Set `Temperature` (double, °C) |
| `STA` | Set `StatusWord` (uint) |
| `RMEC` | Set `ErrorWord` (uint) |
| `RCS` / `SDC` / `SCS` | Set `SetPoint` (double, %) |
| `ROP` | Set `OutputPower_W` — `"OFF"`/`"LOW"` → 0, else parse double |

**`GetBit(uint word, int bit)`** — static helper. Returns `(word & (1u << bit)) != 0`.

---

## IPG Laser ASCII Command Reference

Commands sent directly to the laser over TCP port 10001. Both lasers use CR (`\r`) as line terminator.
Source: YLM-3000 UG `CFSUGHPL0002ENUS_RevA` and YLR-U3 UG `G21-550132-001`.

### Sense / Identity Commands

| Command | 3K Response | 6K Response | Notes |
|---------|-------------|-------------|-------|
| `RMODEL` | `RMN: YLM-3000-SM-VV` | *(empty)* | 3K model string — **3K sense path** |
| `RMN` | `RMN: IPGP578` (hostname) | `RMN: YLM-6000-U3-SM` | **6K sense path** — 3K returns hostname, ignore if no `-` delimiter |
| `RSN` | `RSN: PL2546496` | `RSN: 6103081` | Serial number — both |

### Poll Commands — Telemetry

| Command | Purpose | 3K | 6K | Poll |
|---------|---------|----|----|------|
| `RHKPS` | Housekeeping voltage V | ✅ | ❌ | 3K only |
| `RBSTPS` | Boost voltage V | ✅ | ❌ | 3K only |
| `RCT` | Case temperature °C | ✅ | ✅ | Both |
| `STA` | Status word (32-bit decimal) | ✅ | ✅ | Both |
| `RMEC` | Error word (32-bit decimal) | ✅ | ✅ | Both |
| `RCS` | Setpoint % ch1 | ✅ | ✅ | Both |
| `ROP` | Output power W ch1 | ✅ | ✅ | Both |
| `ROPS` | Output power W ch2 | ❌ | ✅ | 6K only — future |
| `RCSS` | Setpoint % ch2 | ❌ | ✅ | 6K only — future |

### Control Commands

| Command | Purpose | 3K | 6K |
|---------|---------|----|----|
| `EMON` | Start emission | ✅ | ✅ |
| `EMOFF` | Stop emission | ✅ | ✅ |
| `RERR` | Reset resettable errors | ✅ | ✅ |
| `SCS <pct>` | Set current setpoint % ch1 | ✅ | ❌ |
| `SDC <pct>` | Set diode current % ch1 | ❌ | ✅ |
| `ELE` | Enable hardware emission control | ✅ | ✅ |
| `DLE` | Disable hardware emission control | ✅ | ✅ |

### STA Word Bit Decode

32-bit decimal word. Undefined/reserved bits may be 0 or 1 — disregard.

| Bit | 3K Meaning | 6K Meaning | Our label | Polarity |
|-----|-----------|-----------|-----------|----------|
| 0 | Laser Emission | Command buffer overflow | `mb_sta_emission` (3K) | 1=active |
| 1 | Reserved | Overheating | `mb_sta_overheat` (6K) | 1=fault |
| 2 | Reserved | Emission OFF/ON | `mb_sta_emission` (6K) | 1=active |
| 3 | Reserved | High back reflection | — | 1=fault |
| 4 | Reserved | External control power enabled | — | 1=active |
| 5 | External Emission Control enabled | Reserve | `mb_sta_extctrl` (3K) | 1=active |
| 7 | Reserved | High humidity | — | 1=fault |
| 8 | Reserved | Guide laser ON | — | 1=active |
| 9 | Not Ready | Pulse too short | `mb_sta_notready` (3K) | 1=fault |
| 10 | Error Present | CW/pulse mode | `mb_sta_error` (3K) | 1=fault |
| 11 | Reserved | Power supply OFF | `mb_sta_notready` (6K) | 1=fault |
| 12 | Reserved | Modulation enabled | — | 1=active |
| 16 | High Case Temperature | Gate mode enabled | `mb_sta_overheat` (3K) | 1=fault |
| 18 | High Order Mode Error | External emission control enabled | `mb_sta_extctrl` (6K) | 1=active |
| 19 | Optical Fuse 2 Error | Power supply error | `mb_sta_error` (6K) | 1=fault |
| 20 | Bus Voltage out of range | Reserve | `mb_sta_busvolts` (3K) | 1=fault |
| 25 | Reserved | Power supply error | `mb_sta_busvolts` (6K) | 1=fault |
| 26 | Optical Fuse Level | Ground fault | — | 1=fault |
| 27 | Optical Interlock Error | External guide control enabled | — | 1=fault |
| 29 | Critical Error | Critical Error | `mb_sta_crit` (both) | 1=fault |
| 30 | Reserved | Fiber break monitoring active | `mb_sta_shutdown` (6K) | 1=fault |
| 31 | External Shutdown | Reserve | `mb_sta_shutdown` (3K) | 1=fault |

### RMEC Error Word Bit Decode — 3K only

32-bit decimal word. 6K RMEC bit mapping not fully documented in YLR-U3 ICD — use raw word only.

| Bit | 3K Meaning |
|-----|-----------|
| 0 | Case Temperature Fault |
| 4 | Bus Voltage Fault |
| 6 | Output Power Out of Range |
| 10 | Fiber Fuse (optical fuse 1) |
| 11 | Optical Interlock Disconnected |
| 13 | Critical Error |
| 15 | External Shutdown |
| 18 | Over Current |

---

Commands accepted on the MCC USB/UART serial port. All commands are uppercase.
Baud rate: 115200. Line terminator: `\n` or `\r\n`.

### HEL Commands (v3.5.0)

| Command | Description |
|---------|-------------|
| `HEL` | Full HEL status dump — model, serial, sense state, liveness, voltages, temperatures, setpoint, output power, status word, error word. |
| `HELPOW <0-100>` | Set laser power % — gated on `isHEL_Valid()`. Routes to `SDC` (6K) or `SCS` (3K). Training mode clamps to 10% regardless of argument. |
| `HELCLR` | Clear laser errors — sends `RERR`. Gated on `isHEL_Valid()`. |
| `HELTRAIN <0\|1>` | Set training mode: `0`=COMBAT (full power), `1`=TRAINING (10% max). No argument = print current state. |

### Time Source Commands (session 28/29)

| Command | Description |
|---------|-------------|
| `TIME` | Prints full dual-source time status: active source, PTP synched/offset/time, NTP synched/fallback, register bytes (DEVICE_ENABLED, DEVICE_READY, TIME_BITS byte 253 in hex + binary with field labels). |
| `TIMESRC` | Prints current time source policy (no argument). |
| `TIMESRC PTP` | PTP primary, NTP suppressed while PTP synched (default). |
| `TIMESRC NTP` | NTP only — PTP client disabled. |
| `TIMESRC AUTO` | Both sources active concurrently — NTP stays warm alongside PTP. |
| `PTPDEBUG <0-3>` | Set PTP debug level: 0=OFF (default), 1=MIN (offset/delay/stale prints), 3=VERBOSE (SYNC/FOLLOW_UP headers). |
| `REINIT <n>` | Re-initialise device n — mirrors `0xE0 SET_MCC_REINIT`. `REINIT 0`=NTP, `REINIT 4`=PTP, `REINIT 6`=GNSS etc. |
| `ENABLE <n> <0\|1>` | Enable/disable device n — mirrors `0xE1 SET_MCC_DEVICES_ENABLE`. `ENABLE 4 0` forces NTP-only mode; `ENABLE 4 1` re-enables PTP. Prints resulting source state when device 0 or 4 changed. |

### Existing Commands (unchanged)

| Command | Description |
|---------|-------------|
| `?` / `HELP` | Full command list |
| `INFO` | FW version, IP, link status, A1/A2/A3 client counts |
| `REG` | Full REG1 register dump (all 253 bytes with field labels) |
| `STATUS` | System state, device enabled/ready bits (all 8 devices), STAT_BITS, STAT_BITS2, VOTE_BITS |
| `TEMPS` | All temperature sensors |
| `NTP` | NTP server, sync status, epoch time, fallback state |
| `DEBUG <0-3>` | Set MCC debug level |
| `TMC` | TMC A1 liveness, raw 64-byte buffer dump |
| `HEL` | HEL status dump — see HEL Commands above |

---

## TMC Serial Command Reference (session 30/31)

| Command | Description |
|---------|-------------|
| `TIME` | Full dual-source status: active source, PTP synched/offset/time, NTP synched/fallback, STAT_BITS1 and STAT_BITS3 in hex + binary. |
| `TIMESRC` | Print current time source policy. |
| `TIMESRC PTP` | PTP primary, NTP suppressed while synched (default). |
| `TIMESRC NTP` | NTP only — PTP disabled. |
| `TIMESRC AUTO` | Both concurrent. |
| `PTPDEBUG <0-3>` | Set PTP debug level: 0=OFF, 1=MIN, 3=VERBOSE. |
| `NTP` | NTP server, sync status, epoch, fallback state. |
| `NTPIP / NTPFB / NTPSYNC` | NTP config — same as MCC. |

---

## BDC Serial Command Reference (session 32)

### Time Source Commands

| Command | Description |
|---------|-------------|
| `TIME` | Full dual-source status: active source, PTP synched/offset/time, NTP status, register bytes (DEVICE_ENABLED byte 8, DEVICE_READY byte 9, TIME_BITS byte 391 in hex + binary with field labels). |
| `TIMESRC` | Print current time source policy. |
| `TIMESRC PTP` | PTP primary, NTP suppressed while PTP synched (default). |
| `TIMESRC NTP` | NTP only — PTP disabled, `ptp.isSynched` cleared immediately. |
| `TIMESRC AUTO` | Both concurrent — NTP stays warm alongside PTP. |
| `PTPDEBUG <0-3>` | Set PTP debug level: 0=OFF (default), 1=MIN, 3=VERBOSE. |
| `REINIT <n>` | Re-initialise device n — mirrors `0xB0 SET_BDC_REINIT`. `REINIT 0`=NTP, `REINIT 7`=PTP, `REINIT 1`=GIMBAL etc. |
| `ENABLE <n> <0\|1>` | Enable/disable device n — mirrors `0xBE SET_BDC_DEVICES_ENABLE`. `ENABLE 7 0` forces NTP-only mode; `ENABLE 7 1` re-enables PTP. Prints resulting source state when device 0 or 7 changed. |

### NTP Commands

| Command | Description |
|---------|-------------|
| `NTP` | NTP server, sync status, misses, fallback state, epoch time. |
| `NTPIP <a.b.c.d>` | Set primary NTP server IP and resync. |
| `NTPFB <a.b.c.d> \| OFF` | Set/clear fallback NTP server. |
| `NTPSYNC` | Force immediate NTP resync on current server. |

### Existing Commands (unchanged)

| Command | Description |
|---------|-------------|
| `?` / `HELP` | Full command list |
| `INFO` | FW version, IP, link status, port/client counts |
| `REG` | Full REG1 register dump (all 392 bytes with field labels) |
| `STATUS` | System state/mode, device enabled/ready bits (all 8 devices incl. PTP), STAT_BITS, STAT_BITS2, VOTE_BITS |
| `TEMPS` | All temperature sensors |
| `DEBUG <0-3>` | Set BDC debug level |
| `FMC` | A1 FMC liveness + 64-byte buffer dump |
| `TRC` | A1 TRC liveness + 64-byte buffer dump |
| `MCC` | A1 MCC liveness + vote bits |

---

## FMC Serial Command Reference (session 33)

### Time Source Commands

| Command | Description |
|---------|-------------|
| `TIME` | Full dual-source status: active source, PTP synched/offset/time, NTP status, TIME_BITS byte 44 in hex. |
| `TIMESRC` | Print current time source policy. |
| `TIMESRC PTP` | PTP primary, NTP suppressed while PTP synched (default). |
| `TIMESRC NTP` | NTP only — PTP disabled. |
| `TIMESRC AUTO` | Both concurrent. |
| `TIMESRC OFF` | Both sources disabled — diagnostics only. |
| `PTPDEBUG <0-3>` | Set PTP debug level: 0=OFF, 1=MIN, 3=VERBOSE. |

### Existing Commands (unchanged)

| Command | Description |
|---------|-------------|
| `?` / `HELP` | Full command list |
| `INFO` | FW version, IP, link status |
| `REG` | Full REG1 register dump (all 45 bytes with field labels) |
| `STATUS` | System state/mode + status bits decoded |
| `NTP` | NTP server, sync status, epoch, fallback state |
| `NTPIP <a.b.c.d>` | Set NTP server IP and resync (not persisted) |
| `DEBUG <0-3>` | Set FMC debug level |

| ID | Item | Owner | Priority |
|----|------|-------|----------|
| ~~NEW-37~~ | `MSG_MCC.cs` PTP bits + ENG GUI display | ~~C# / HMI dev~~ | ✅ Closed session 28/29 |
| ~~FW-1~~ | `PTPDEBUG <0-3>` serial command | ✅ Closed session 30 — implemented and verified |
| ~~NEW-38a~~ | TMC PTP integration | ~~FW dev~~ | ✅ Closed session 30/31 — STAT_BITS3 at byte 61, TIME/TIMESRC/PTPDEBUG serial commands, MSG_TMC.cs updated |
| ~~NEW-38b~~ | BDC PTP integration | ~~FW dev~~ | ✅ Closed session 32 — DEVICE_ENABLED/READY bit 7, TIME_BITS byte 391, TIME/TIMESRC/PTPDEBUG serial commands, MSG_BDC.cs updated. MCC RE_INIT_DEVICE PTP bug also fixed. |
| ~~NEW-38c~~ | FMC PTP integration | ~~FW dev~~ | ✅ Closed session 33 — TIME_BITS byte 44, TIME/TIMESRC/PTPDEBUG serial commands, NTP IP fixed, socket budget 4/8 |
| NEW-38d | TRC PTP integration | FW dev | ⏳ Pending |

---

## Known Discrepancies vs CB_ICD.xlsx

| Byte | Field | Excel | This Document | Resolution |
|------|-------|-------|---------------|------------|
| 0xA0 | SET_UNSOLICITED behaviour | Required to start telemetry | TRC auto-starts telemetry to BDC on boot (TRC-26). `0xA0` is now rate-change/stop only | **This document correct** — TRC-26 implemented in `udp_listener.cpp`. Excel/BDC boot sequence does not need to send `0xA0`. |
| compositor | `if(valid)` gate on tx/ty | TRC froze last good tx/ty on MOSSE lock loss | TRC-27: gate removed — raw MOSSE output always written. BDC gates PID on `TrackB_Valid` (BDC-09) | **This document correct** — eliminates stale-freeze jump events. BDC must read `status_track_camN` bit 3. |
| 0xD3 | ORIN_SET_STREAM_OVERLAYS payload | `byte 0/1` | 8-bit bitmask (8 named flags) | **This document correct** — bitmask implemented in TRC-06. Excel needs update. |
| 0xD8 | ORIN_SET_STREAM_TESTPATTERNS payload | `uint8 0/1` | `uint8 cam_id` (0=VIS, 1=MWIR) + `uint8 enable` (0=off, 1=on) | **Resolved** — mirrors ASCII `TESTSRC CAM1\|CAM2 TEST\|LIVE`. Confirmed by owner (TRC-08 closed). Binary handler (`needs impl`) must follow this convention. |
| 0xD9 | Enum name / purpose | `ORIN_ACAM_SET_AI_TRACK_PRIORITY` | `ORIN_ACAM_COCO_CLASS_FILTER` | **Repurposed** — 0xD9 was never implemented. COCO class filtering supersedes the abstract "AI priority" concept. BDC routing removed (BDC does not participate in TRC inference). Excel needs update. |
| 0xDE | COCO enable byte (COCO-04 planning notes) | N/A | COCO-04 planning incorrectly assigned `0xDE` to `ORIN_ACAM_COCO_ENABLE` — 0xDE is `ORIN_SET_VIEW_MODE` (implemented). **Corrected: `ORIN_ACAM_COCO_ENABLE` → 0xDF.** udp_listener.cpp COCO-04 handler must use 0xDF. | **This document and code must use 0xDF.** |
| 0xDF | Enum name | `RES_DF` | `ORIN_ACAM_COCO_ENABLE` | **Retired v3.6.0** — ORIN_ACAM_COCO_ENABLE moved to `0xD1`. |
| 0xFD | Enum name | `RES_FE` | `RES_FD` | **This document correct** — Excel byte/name mismatch (typo). |


### Additional Discrepancies Found During v1.7 Consolidation (session 3)

| Byte | Field | Excel | This Document | Resolution |
|------|-------|-------|---------------|------------|
| 0xB7 | Scope | `EXY` | `INT_OPS` | Typo in CB_ICD_v1_7.xlsx. Treated as EXT. Fixed in updated Excel. |
| 0xCC | Scope | `EXE` | `INT_OPS` | Typo in CB_ICD_v1_7.xlsx. Treated as EXT. Fixed in updated Excel. |
| MCC REG1 | Byte offsets (rows 70–71) | VERSION WORD at 340–344, MCU Temp at 344–348 (overlaps CHARGER STATUS BITS at 344) | VERSION WORD at 345–349, MCU Temp at 349–353 | Cross-verified against `mcc.cpp` SEND_REG_01(). Register is 353 bytes not 348. |


---


## Framing Protocol and Network Architecture

> Network topology, IP range policy, port reference, A1/A2/A3 architecture, frame geometry,
> CRC specification, and client access model are documented in **ARCHITECTURE.md §2–§6**.
> This ICD owns command bytes and register layouts only.

### STATUS Byte Codes

| Value | Name | Meaning |
|-------|------|---------|
| `0x00` | `STATUS_OK` | Command accepted and executed |
| `0x01` | `STATUS_CMD_REJECTED` | CMD_BYTE not in `EXT_CMDS[]` whitelist |
| `0x02` | `STATUS_BAD_MAGIC` | Magic bytes incorrect |
| `0x03` | `STATUS_BAD_CRC` | CRC check failed |
| `0x04` | `STATUS_BAD_LEN` | `PAYLOAD_LEN` does not match expected for this CMD_BYTE |
| `0x05` | `STATUS_SEQ_REPLAY` | SEQ_NUM within replay-rejection window |
| `0x06` | `STATUS_NO_DATA` | Register not yet populated (device not ready) |

### Fixed 512-Byte Payload Layout

```
MCC REG1 frame (CMD_BYTE 0xA1 — legacy constant):
  Bytes   0–252  : MCC REG1 defined fields (253 bytes) — see MCC Register 1 section
  Bytes 253–255  : 0x00 reserved (3 bytes)
  Bytes 256–511  : 0x00 — MCC fixed block is 256 bytes; response always padded to 512

BDC REG1 frame (CMD_BYTE 0xA1 — legacy constant):
  Bytes   0–390  : BDC REG1 defined fields (391 bytes) — see BDC Register 1 section
  Bytes 391–511  : 0x00 reserved (121 bytes headroom)
```

New fields consume reserved bytes without changing the 512-byte total. `PAYLOAD_LEN` always
reads `512`. Older firmware clients ignore the reserved area they already skip.

### 0xA4 — EXT_FRAME_PING Response Payload

| Bytes | Field | Value |
|-------|-------|-------|
| 0 | `protocol_version` | `0x01` |
| 1–2 | `echo_seq` | uint16 — echoes request SEQ_NUM |
| 3–6 | `uptime_ms` | uint32 — server uptime in milliseconds |
| 7–511 | reserved | `0x00` |

---

---

## Appendix A — Command Map Visualization

The CROSSBOW command space (0xA0–0xFF, 96 bytes) is visualized as a **6×16 grid** at the start of any ICD review session. This appendix is the authoritative specification for that visualization so it can be regenerated consistently.

### Grid Structure

- **Rows:** six byte ranges — `0xA_`, `0xB_`, `0xC_`, `0xD_`, `0xE_`, `0xF_`
- **Columns:** low nibble `_0` through `_F` (left to right)
- **Row label:** left of each row, monospace, e.g. `0xA0`
- **Column header:** low nibble digit above each column

Each **cell** contains three stacked text elements:

| Element | CSS class | Content | Example |
|---------|-----------|---------|---------|
| Byte | `.byte` | Full hex address | `0xA0` |
| Short name | `.sname` | Enum name ≤10 chars, `text-overflow: ellipsis` | `UNSOLICITED` |
| Tag | `.stag` | Scope or status label | `INT_OPS` |

### Color Classes

Three active colors plus one reserved for problems:

| Class | Light bg | Dark bg | Meaning |
|-------|----------|---------|---------|
| `s-ok` | `#f0f4ff` | `#1a2a4a` | **INT_OPS** — accessible via A3 (THEIA / external integrators) |
| `s-eng` | `#f5f0ff` | `#2a1a4a` | **INT_ENG** — accessible via A2 only (ENG GUI / maintenance) |
| `s-av` | `var(--color-background-secondary)` | same | **Unused** — reserved, retired, or unassigned; dashed border |
| `s-mm` | `#fff8e0` | `#3a2a00` | **Problem** — reserved for issues (Scope/Target mismatch, whitelist conflict). Amber border 1.5px. **Not used when the map is clean.** |

> **Color policy:** red is not used. Amber (`s-mm`) is the sole problem indicator and only appears when an audit finds a conflict. A clean map has only blue, purple, and gray.

Text colors follow the ramp of the background fill: 800-stop in light mode, 100/200-stop in dark mode.

### Mismatch Detection Rule

A cell renders as `s-mm` (amber) when **both** conditions are true:
- `Scope = INT_ENG`
- `INT_OPS Target ≠ —`

This flags rows where the two columns contradict each other. Resolution requires checking the firmware `EXT_CMDS_MCC[]` / `EXT_CMDS_BDC[]` whitelists — the whitelist is the ground truth for A3 accessibility.

### CSS Skeleton

```css
.block-row  { display: grid; grid-template-columns: 44px 1fr; gap: 5px; }
.bl         { font-size: 10px; font-family: var(--font-mono); text-align: right; }
.cmd-grid   { display: grid; grid-template-columns: repeat(16, 1fr); gap: 2px; }
.cell       { border-radius: 3px; padding: 3px 2px; border: 0.5px solid transparent; }
.cell .byte { font-family: var(--font-mono); font-size: 9px; display: block; line-height: 1.3; }
.cell .sname{ font-family: var(--font-mono); font-size: 8px; display: block;
              white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.cell .stag { font-size: 7px; display: block; line-height: 1.5; margin-top: 1px; }

.s-ok  { background: #f0f4ff; border-color: #b8c8f0; }
.s-eng { background: #f5f0ff; border-color: #c8b8f0; }
.s-av  { background: var(--color-background-secondary);
         border: 0.5px dashed var(--color-border-secondary); }
.s-mm  { background: #fff8e0; border-color: #d0a020; border-width: 1.5px; }  /* problems only */

@media (prefers-color-scheme: dark) {
  .s-ok  { background: #1a2a4a; border-color: #3a5888; }
  .s-eng { background: #2a1a4a; border-color: #6848b8; }
  .s-mm  { background: #3a2a00; border-color: #d0a020; }
}
```

### Legend

```
■ INT_OPS   blue   — A3 accessible (included in CROSSBOW_ICD_INT_OPS)
■ INT_ENG   purple — A2 only (this document only)
□ unused    gray   — reserved, retired, or unassigned; dashed border
■ problem   amber  — Scope/Target conflict or whitelist mismatch (dormant when map is clean)
```

### Regeneration Instruction

At the start of any ICD review session, request:

> *"Render the CROSSBOW ICD command map in the session format."*

The session will render a static HTML widget (no JS-generated content) with all 96 cells hardcoded and hover `title` attributes carrying full row detail. Data is sourced from the command tables in this document. The `s-mm` class is applied when Scope/Target conflicts are detected; a clean ICD produces no amber cells.

**Format first established:** CB-20260416d  
**Specification captured here:** ICD v4.2.1
