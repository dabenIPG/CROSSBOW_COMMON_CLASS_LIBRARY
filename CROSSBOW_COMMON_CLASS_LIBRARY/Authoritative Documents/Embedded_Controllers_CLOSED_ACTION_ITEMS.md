# CROSSBOW — Closed Action Items
**Last updated:** 2026-04-06 (session 28)
**Purpose:** Archive of all resolved action items. Merged from EmbeddedControllerUpdate_CLOSED_ACTION_ITEMS.md and PTP_CLOSED_ACTIONS.md
**Open items:** see `Embedded_Controllers_ACTION_ITEMS.md`

---

## Session 14 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| S14-1 | Fire control vote rate 200Hz → 100Hz | `MCC::TICK_VoteStatus` changed 5ms → 10ms in `mcc.hpp` | S14 |
| S14-2 | A1 stream rates table added to ICD | ICD bumped to v1.7.2. Stream rates table added to Network Architecture section | S14 |
| FW-PRE-CHECK | Confirm `0xA0 SET_UNSOLICITED` in MCC and BDC `EXT_CMDS[]` | ✅ Confirmed present in both `EXT_CMDS_MCC[]` and `EXT_CMDS_BDC[]` | S14 |
| FW-BDC-1 | Add `CMD_MWIR_NUC1` (`0xCC`) to BDC `EXT_CMDS[]` | ✅ Already present — no flash required | S14 |
| DISC-1 | `SET_CUE_OFFSET` byte mismatch — ICD vs BDC firmware | ✅ `defines.hpp` confirmed `SET_CUE_OFFSET = 0xB4` correct — BDC case comments were stale only | S14 |
| ENUM-1 to ENUM-5 | `defines.hpp` enum names synced to ICD | ✅ `EXT_FRAME_PING`, `RES_C0`, `ORIN_ACAM_COCO_CLASS_FILTER`, `ORIN_ACAM_COCO_ENABLE`, `RES_FD` — all corrected | S14 |
| TRC-1 | TRC compile error — wrong enum name in `udp_listener.cpp:944` | ✅ `ORIN_ACAM_SET_AI_TRACK_PRIORITY` → `ORIN_ACAM_COCO_CLASS_FILTER` fixed — TRC compiles | S14 |

---

## Session 15 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| TRC-M10 | TRC `isConnected` live flag | ✅ Wired in `handleA1Frame` — was only set in dead receive loop previously | S15 |

---

## Session 17 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| NEW-12 | TransportPath enum — MSG_MCC/BDC | ✅ Complete — deployed sessions 16/17. MAGIC_LO computed from enum, not hardcoded | S17 |

---

## Session 22 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| NEW-13 | ICD scope labels INT_OPS/INT_ENG applied | ✅ Applied ICD v3.1.0 — all commands labelled with scope | S22 |
| TRC-M1 | TRC A2 framing — magic/frame validation | ✅ Complete | S22 |
| TRC-M5 | TRC A2 framing — `buildTelemetry` struct rewrite | ✅ Complete | S22 |
| TRC-M7 | TRC FW A2 framing — `udp_listener.cpp` build/parse/CRC | ✅ Complete | S22 |

---

## Session 26 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| BDC-FMC-1 | BDC→FMC A1 path — port `10023`→`10019`, `isConnected` watchdog, `OnA1Received()` | ✅ Done | S26 |
| BDC-FMC-2 | BDC→FMC command framing — `EXEC_UDP()` replaced with full INT framed sends. Port changed to `PORT_A2`. `client.begin(0)` for send-only socket | ✅ Done — `fmc.cpp/hpp` delivered | S26 |
| BDC-FMC-3 | BDC `EXT_CMDS_BDC[]` — added `0xF1`, `0xF2`, `0xF3`, `0xFB` to whitelist for HMI passthrough to FMC | ✅ Done — `bdc.hpp` delivered | S26 |
| FMC-ENG-1 | FMC eng GUI socket bind — explicit bind, source IP filter, explicit send | ✅ Done — `fmc.cs` delivered | S26 |
| FSM-TRACK | FSM tracking end-to-end — commanded position, readback, mirror movement | ✅ Confirmed working | S26 |
| NET-BAT | Battery/charger liveness — `isBAT_Ready` and `isCRG_Ready` wired to `bat.isCommOk` and `dbu.isConnected()` | ✅ Done | S26 |
| TRC-M11b | MAINT/FAULT coordinated flash — all five controllers | ✅ Confirmed correct on MCC, BDC, TMC, FMC, TRC | S26 |
| HMI-A3-20 | Eng GUI socket bind — TransportPath pattern implemented | ✅ Working on HMI and eng GUI | S26 |
| TRC-2 | THEIA not receiving video after IP change `.8`→`.208` | ✅ Closed — video panel was removed by designer, not a firmware issue | S26 |
| FW-MCC | Add `0xE6 PMS_SET_FIRE_REQUESTED_VOTE` to `EXT_CMDS_MCC[]`, remove INT guard, flash MCC | ✅ Confirmed `STATUS_OK` from `.208:10050` | S26 |
| FW-VERIFY | All EXT promotions return `STATUS_OK` — `0xE6`, `0xCC`, `0xB4` | ✅ All confirmed `STATUS_OK` from bench | S26 |

---

## Session 27 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| NET-1 | NTP server IP set over UDP — `SET_NTP_CONFIG 0xA2`: 0 bytes=resync, 1 byte=set primary octet, 2 bytes=set primary+fallback octet | ✅ Done — all four Arduino controllers + C# classes | S27 |
| NTP-RECOVER | NTP auto-recovery — primary/fallback with `consecutiveMisses`, `NTP_STALE_MISSES=3`, 2-min primary retry, latches on primary when it responds | ✅ Done — `ntpClient.hpp/cpp`, all four controllers | S27 |
| NTP-STRATUM | NTP stratum/LI validation — `ProcessPacket` rejects stratum 0, stratum ≥16, LI=3. `.33` correctly triggers fallback when GPS unlocked | ✅ Done — `ntpClient.cpp` | S27 |
| NTP-SERVERS | NTP server defaults — `.33` HW Stratum 1 primary, `.208` Windows HMI fallback, `.8` eng IP removed from NTP role | ✅ Done — `defines.hpp`, `mcc.hpp`, `bdc.hpp`, `tmc.hpp`, `fmc.hpp` | S27 |
| NTP-STATUS | NTP fallback status bits added to all controller REG1 | ✅ Done — ICD v3.2.0. Note: superseded by unified TIME_BITS layout (S32) — original byte positions now RES | S27 |
| NIC-BIND | Dual-NIC eng GUI fix — `CrossbowNic.cs` auto-detects internal NIC (<100) and external NIC (≥200) | ✅ Done — `CrossbowNic.cs`, `mcc.cs`, `bdc.cs`, `tmc.cs`, `fmc.cs` | S27 |
| ICD-3.2.0 | ICD bumped to v3.2.0 — `SET_NTP_CONFIG`, all controller status bit layouts, NTP server policy documented | ✅ Done — INT_ENG, INT_OPS, EXT_OPS, ARCHITECTURE, DOCUMENT_REGISTER | S27 |
| HYPERION-THEIA | HYPERION↔THEIA CUE relay path not working | ✅ Working session 27 | S27 |
| MCC-1 | MCC CloudEnergy battery bridge init — needed initial handshake for reliable comms | ✅ Battery comms reliable on bench without explicit init sequence | S27 |
| TMC-TEMP-1 | TMC MCU temp reading off — CAL1=947, CAL2=1198, raw~982. MCUADC printf fixed | ✅ No longer observed | S27 |
| DEPLOY-1 | Windows NIC: confirm `192.168.1.x (<100)` internal NIC assigned before first run | ✅ Handled by `CrossbowNic.cs` auto-detection | S27 |
| DEPLOY-2 | Clean rebuild after all file replacements | ✅ Done session 27 | S27 |
| NEW-35 | FW: all firmware targets NTP `.33` directly | ✅ `IP_NTP_BYTES = .33` confirmed in `defines.hpp`; fallback `.208` configured by default | S27 |

---

## Session 28 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| SAMD-NTP | FMC SAMD21 NTP/USB CDC conflict | ✅ Root cause identified — `ntp.PrintTime()` and `ptp.PrintTime()` call `Serial` not `SerialUSB`, causing USB CDC conflict. All `PrintTime()` calls removed from FMC serial command handlers (`TIME` command now prints `[not synced]` / `[see PTPDEBUG]`). `isNTP_Enabled` default changed `false` → `true`. NTP confirmed working on bench with USB CDC active simultaneously. | S28 |
| NEW-39 | LCH/KIZ `operatorValid` hardcoded true | ✅ Closed S28 — implementation confirmed complete | S28 |

---

## Session 28/29 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| NEW-36 | PTP integration HW verify | ✅ `TIME` command confirmed `offset_us=12`, `active source: PTP`, `time=2026-03-28` on MCC | S29 |
| NEW-37 | `MSG_MCC.cs` PTP bits + ENG GUI display | ✅ `epochTime`, `activeTimeSource`, `isPTP_DeviceReady`, `usingPTP` all working | S29 |

---

## Session 30 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| FW-1 | `PTPDEBUG <0-3>` serial command | ✅ Implemented and verified on MCC; propagated to all controllers | S30 |
| FW-2 | `TIMESRC` UDP command — `PTP`, `NTP`, `AUTO`, `OFF` | ✅ Implemented across all controllers | S30 |

---

## Session 30/31 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| NEW-38a | TMC PTP integration | ✅ `STAT_BITS3` at byte 61, `TIME`/`TIMESRC`/`PTPDEBUG` serial commands, `MSG_TMC.cs` updated. TMC FW v3.0.5 | S30/31 |

---

## Session 32 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| NEW-38b | BDC PTP integration | ✅ Socket budget corrected 9/8→7/8 (TRC/FMC share `udpA2`). `TIME_BITS` at byte 391. Boot step `PTP_INIT` added. `MSG_BDC.cs` updated | S32 |

---

## Session 33 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| NEW-38c | FMC PTP integration | ✅ `TIME_BITS` at byte 44. Socket budget 4/8. NTP IP corrected from `.8` to `.33`. `isNTP_Enabled=false` default (SAMD-NTP workaround). `TIME`/`TIMESRC`/`PTPDEBUG` serial commands | S33 |

---

## Session 36 Closures

| ID | Item | Resolution | Session |
|----|------|------------|---------|
| NEW-9 | `MSG_MCC.cs` HW verify | ✅ All fields confirmed correct on live hardware | S36 |
| NEW-10 | `MSG_BDC.cs` HW verify | ✅ All fields confirmed correct on live hardware | S36 |
| NEW-18 | CRC cross-platform wire verification | ✅ CRC-16/CCITT confirmed correct across all five controllers and C# | S36 |
| NEW-31 | `frmMain.cs` SET_LCH_VOTE arg swap — `operatorValid` duplicated | ✅ Fixed — `operatorValid` hardcoded true pending proper implementation; NEW-39 opened to track full implementation | S36 |
