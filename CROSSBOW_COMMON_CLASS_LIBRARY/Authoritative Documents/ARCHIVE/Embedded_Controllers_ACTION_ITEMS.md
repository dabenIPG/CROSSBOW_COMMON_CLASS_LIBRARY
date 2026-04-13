# CROSSBOW — Open Action Items
**Last updated:** 2026-04-06 (session 30)
**ICD Reference:** v3.3.7 (session 37)
**Closed items:** see `Embedded_Controllers_CLOSED_ACTION_ITEMS.md`

---

## 🔴 HIGH

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| THEIA-SHUTDOWN | Clean THEIA/system shutdown — graceful STANDBY→OFF | ⏳ Pending | Laser safe, relays off, state→OFF, HMI disconnect. Define shutdown sequence for commanded shutdown vs power loss. Review MCC/BDC responsibilities. No progress S27→S36. | THEIA `.cs` shutdown handler / state machine |
| HMI-A3-18 | LCH/KIZ/HORIZ bulk upload bench test | ⏳ Bench verify | Whitelist confirmed clean in firmware. Full end-to-end bench verification needed: upload from THEIA via A3, confirm receipt and correct parse in BDC, verify all fields land correctly in REG1 | None — test only |
| GUI-2 | HMI robust testing — live HW | ⏳ In progress | MCC/BDC/TMC/FMC ENG GUI stable session 29. BDC A3 (THEIA) stable. Full engagement sequence, mode transitions, fire control chain end-to-end still pending | HW — no code changes |
| FMC-NTP | FMC dt elevated — suspected NTP/USB CDC loop blocking | 🔴 Open | FMC `dt_us` observed significantly higher than other controllers in live testing session 29. Likely the same SAMD-NTP USB CDC / Ethernet contention issue causing periodic main loop stalls during NTP activity. Workaround: `isNTP_Enabled=false` default on FMC. Long-term fix: FW-C7 `SET_TIMESRC 0xAF`. Related to SAMD-NTP and FW-B3 | `fmc.cpp` — NTP poll loop, `fmc.hpp` `isNTP_Enabled` default |
| FW-B3 | PTP DELAY_REQ W5500 contention — all PTP disabled | ⏳ Open | When two or more controllers have PTP active simultaneously, W5500 blocks ~40ms per DELAY_REQ on ARP resolution, saturating main loop. Symptoms: dt spikes, A1 stream drops, A2 stalls. **Current workaround: `isPTP_Enabled=false` fleet-wide — NTP only in production.** Proposed fixes: (1) `suppressDelayReq` flag per-controller; (2) staggered DELAY_REQ timing — FMC +50ms offset after FOLLOW_UP | `ptpClient.cpp/hpp` — DELAY_REQ transmission logic |
| SAMD-NTP | FMC SAMD21 NTP/USB CDC conflict | ⏳ Open | USB CDC and Ethernet share power path on SAMD21 — `SerialUSB` calls during Ethernet TX cause board lockup. `ntp.PrintTime()` / `ptp.PrintTime()` in `TIME` command caused complete lockup requiring power cycle. Workaround: `isNTP_Enabled=false` default, slim `TIME` command. **FMC is only controller with zero active time source.** Root cause: likely USB CDC TX buffer overflow or ISR conflict with W5500 SPI | `fmc.cpp` — `TIME` handler, `PrintTime()` calls |
| HMI-AWB | VIS camera AWB passthrough — ENG GUI then HMI | ⏳ Pending | Priority bumped to HIGH. Two sub-steps: (1) **AWB-ENG**: assign new ICD command byte (0xC4 deprecated/reserved), add to `EXT_CMDS_BDC[]`, BDC→TRC dispatch, TRC binary handler (`needs impl`), wire to `frmBDC.cs`. Verify TRC-side handler exists before wiring GUI. (2) **AWB-HMI**: expose same control on THEIA HMI — note AWB control maps to Xbox controller input, binding TBD when scoping HMI form. Depends on AWB-ENG. | `frmBDC.cs`, `bdc.hpp`, TRC `udp_listener.cpp`, THEIA HMI `.cs` |
| HMI-TRACKER | Tracker controls (COCO + optical flow) — ENG GUI then HMI | ⏳ Pending | Two sub-steps: (1) **TRACKER-ENG**: COCO class filter (`0xD9`) and enable (`0xDF`) already in ICD and firmware whitelist — C# wiring to `frmBDC.cs` only. (2) **TRACKER-HMI**: expose same controls on THEIA HMI — Xbox controller binding TBD. Optical flow deferred to TRC session — confirm what TRC exposes via ASCII before assigning ICD bytes. | `frmBDC.cs`, THEIA HMI `.cs` |

---

## 🟡 MEDIUM — HMI / Feature

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| HMI-STATS-1 | HMI controller health stats — status bar meatball + IBIT detail | ✅ Implemented S30 | Status strip boxes use `CommHealth` for color after STANDBY (ping only before). IBIT labels expanded: `mb_MCC_Connected_rb` → `dt: 000.00 <00000> [00000] us`, `mb_MCC_UnSol_Enabled_rb` → `HB: 000.00 <00000> [00000] ms`. Time strip split into three controls per controller: `tss_*_TimeSrc` (colored by source), `tss_*_NTPTime` (date/time), `tss_*_dUTC` (colored by threshold). Max/avg tracking in `MSG_MCC`/`MSG_BDC`. Double-click dt label resets dt stats, double-click HB label resets HB stats. | `frmMain.cs`, `MSG_MCC.cs`, `MSG_BDC.cs`, `crossbow.cs` |
| PARALLAX | Range-based parallax | ⏳ Pending | Architecture confirmed — BDC owns all logic, FMC unaware. Two components: (1) VIS FSM home offset — range-dependent, formula/LUT TBD. (2) VIS→MWIR fixed offset — constant delta on `0xD0`. Range source arbitrator: RS232 rangefinder → radar CUE → TRC image estimate. New `targetRange` register in BDC. New `rangeSource` status register. Calibration via eng GUI + THEIA HMI (new ICD command needed) | `bdc.cpp`, `bdc.hpp` |
| HMI-COCO | COCO class filter and enable to HMI | ⏳ Pending | Folded into HMI-TRACKER — see above | — |
| HMI-TRACKGATE | Track gate size persistence on reset | ⏳ Pending | Decision needed: restore last operator-set gate size on tracker reset/reacquisition or reset to default. If persist: THEIA caches last sent `0xD5` values and re-sends on tracker reset | THEIA `frmMain.cs` |
| GUI-8 | C# client model — apply to TRC | ⏳ Open | TRC C# client not yet updated to standardized model (session 29). Apply: single `0xA4` registration, `_lastKeepalive` only in `SendKeepalive()`, any-frame liveness, `connection established` in receive loop, remove redundant elapsed check from KeepaliveLoop. TRC has no A3 — A2 only | `trc.cs`, `frmTRC.cs` |
| GUI-3 | MCC vs BDC time source label discrepancy | ⏳ Open | Two sub-items: (1) `MSG_BDC.cs` — `activeTimeSourceLabel` NTP fallback case added S30. Verify `activeTimeSource` reads from `TimeBits` (`tb_usingPTP` / `tb_isNTP_Synched`) not `DeviceReadyBits`. (2) `MSG_TMC.cs` — align to `tb_*` prefix naming (low priority, cosmetic) | `MSG_BDC.cs`, `MSG_TMC.cs` |
| GUI-5 | `lbl_gimbal_hb` — `gimbalMSG.HB_TX_ms` missing | ⏳ Open | `gimbalMSG.HB_TX_ms` property does not exist on `MSG_GIMBAL`. Find correct HB property name and fix binding in `frmBDC` | `frmBDC.cs`, `MSG_GIMBAL.cs` |
| GUI-7 | HB and status timing audit — all child devices | ⏳ Open | Review HB timing and status display for all child devices across ENG GUI — confirm correct property bindings, consistent HB_TX_ms / HB_RX_ms / dt rolling max display, and liveness indicators for: GIMBAL, FMC, TRC, TMC, GNSS, HEL, BAT, CRG. GUI-5 is a symptom of a broader gap | `frmBDC.cs`, `frmMCC.cs`, all `MSG_*.cs` classes |
| NEW-33 | MCC REG1 VOTE_BITS byte 3 bit 0 wrong field | ⏳ Open — hold pending HW test | Currently packs `isLaserTotalHW_Vote_rb` — should be `isNotBatLowVoltage()`. Hold until HW test confirms no downstream dependency on current (wrong) value | `mcc.cpp` — `buildReg01()` |

---

## 🟡 MEDIUM — Firmware / Integration

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| FW-C6 | `isUnSolicitedMode_Enabled` bit retired — C# reads stale bit | ⏳ Open | `STATUS_BITS` byte 9 bit 7 retired session 35 — always 0. `MSG_MCC.cs` line 380 still reads `(StatusBits & 0x80)`. Fix: `public bool isUnSolicitedMode_Enabled { get { return HB_ms > 0; } }`. Also clean up stale comment in `mcc.hpp` line 149 (`7:isUnsolicitedEnabled` → `7:RES`). Revisit whether bit 7 should be repurposed. Same retirement confirmed in `MSG_BDC.cs` line 522. | `MSG_MCC.cs` line 380, `mcc.hpp` line 149, `MSG_BDC.cs` line 522 |
| FW-C7 | `SET_TIMESRC 0xAF` — new ICD command for time source control over UDP | ⏳ Pending | `TIMESRC` serial command (FW-2, S30) has no UDP/ICD equivalent. Add `SET_TIMESRC = 0xAF` — payload: `0=OFF, 1=NTP, 2=PTP, 3=AUTO`. Scope: all five controllers, `INT_ENG` only. Firmware handler + `EXT_CMDS[]` whitelist + C# wiring. Resolves FMC-NTP / SAMD-NTP operator control without serial access. Unblocks FW-B4 runtime PTP enable. | `defines.hpp`, `defines.cs`, all five controller `.cpp/.hpp`, C# client classes |
| FW-C8 | Revisit retired ICD slots `0xA1` and `0xA3` | ⏳ Pending | `0xA1` retired inbound (S35) but still used as outbound CMD_BYTE in REG1 frames — evaluate whether fully retirable or repurposable. `0xA3` actively rejects with `STATUS_CMD_REJECTED` — evaluate removing rejection handler and repurposing the slot. Low priority, do after any session that touches frame parsing. | All controller `.cpp` files, `defines.cs` |
| CLEANUP-1 | Dead code — `MCC_STATUS` and `BDC_STATUS` on controller classes | ⏳ Pending | `MCC.MCC_STATUS` (`return LatestMSG.HB_ms < 200`) and `BDC.BDC_STATUS` (`return LatestMSG.RX_HB > 10 && < 60`) superseded by `MSG_MCC.CommHealth` / `MSG_BDC.CommHealth` (S30). `CB.MCC_STATUS` and `CB.BDC_STATUS` no longer call them. Remove from `MCC` and `BDC` controller classes when convenient. | `mcc.cs` / `mcc.hpp` — `MCC_STATUS`, `bdc.cs` / `bdc.hpp` — `BDC_STATUS` |
| CLEANUP-2 | `WorstStatus` removed from `CB` — confirm no remaining callers | ⏳ Pending | `WorstStatus()` added then removed from `crossbow.cs` S30 when `MCC_STATUS`/`BDC_STATUS` were simplified to return `CommHealth` directly. Confirm no other callers before deleting. If no callers found — close immediately. | `crossbow.cs` |
| CLEANUP-3 | A3 ACK discrepancy — MCC visible in debug, BDC not | ⏳ Pending | MCC A3 ACK visible in debug output, BDC A3 not — both working. Likely a log level or debug print difference in firmware, not a protocol issue. Investigate when on HW. | `bdc.cpp` — A3 handler debug prints |
| CLEANUP-4 | Confirm ping stops correctly at STANDBY transition | ⏳ Pending | `PING_STATUS_*` bools stay at last value when ping loop stops at STANDBY. Verify `CB.MCC_STATUS` / `CB.BDC_STATUS` do not use stale ping state incorrectly after transition — before STANDBY ping governs, at/after STANDBY `CommHealth` governs exclusively. Confirm on HW. | `frmMain.cs` — `PingHB()`, `crossbow.cs` — `MCC_STATUS`, `BDC_STATUS` |
| BDC-1 | Gate Fuji/MWIR comms on relay state | ⏳ Pending | Disable comms to Fuji/MWIR when their relays are off — suppress spurious lost-message errors. Tie to `SET_BDC_RELAY_ENABLE` state in BDC | `bdc.cpp`, `bdc.hpp` |
| BIT-CLEANUP | Status bits audit — `defines.cs` bitmask enums | ⏳ Pending | `HUD_OVERLAY_BITS`, `VOTE_BITS_MCC`, `VOTE_BITS_BDC` use different C# bitmask enum pattern vs `defines.hpp`. Walk through to confirm intentional or align. Related to TMC `tb_*` prefix inconsistency in TIME_BITS_ALIGNMENT | `defines.cs`, `defines.hpp` |
| GNSS-WATCHDOG | GNSS `isConnected` watchdog bench verify | ⏳ Bench verify | Confirm `DEVICE_READY_BITS` bit 6 drops correctly when NovAtel goes silent. 3s timeout (`GNSS_COMMS_TIMEOUT_MS=3000`) confirmed correct in code review S28. Needs live bench test — disconnect NovAtel UDP, observe bit 6 clear in ENG GUI | `gnss.cpp`, `gnss.hpp` — code correct, test only |
| FW-B2 | MCC RX-side SEQ gap counter for TMC A1 stream | ⏳ Open | Track per-slot SEQ discontinuities on MCC receive side for TMC A1 stream — consistent with gap counter on BDC/FMC tabs | `mcc.cpp` — A1 RX handler |
| FW-B4 | MCC and FMC `ptp.INIT()` not unconditional at boot | ⏳ Open | MCC and FMC gate `ptp.INIT()` behind `if (isPTP_Enabled)` — `TIMESRC PTP` at runtime sets flag but sockets never opened, silent failure. Fix: remove guard, call `ptp.INIT()` unconditionally at boot matching BDC/TMC pattern. `isPTP_Enabled` gates `ptp.UPDATE()` only. MCC socket impact: 6/8→8/8 (designed-in). FMC: 2/8→4/8. MCC: ensure `ptp.INIT()` placed after GNSS and HEL init. Unblocked by FW-C7. | `mcc.cpp` INIT() ~line 49, `fmc.cpp` INIT() ~line 92 |
| FW-14 | GNSS socket bug in MCC `RUNONCE` | ⏳ Open — fix when on HW | `RUNONCE` case 6 and `EXEC_UDP` use `udpRxClient` to send commands — should use `udpTxClient` (port 3002). Confirmed in `gnss.cpp` code review S28 | `gnss.cpp` — `RUNONCE()` case 6, `EXEC_UDP()` |
| NEW-38d | TRC PTP integration | ⏳ Pending | TRC currently uses `systemd-timesyncd` NTP only — no PTP path, no TIME_BITS in REG1. Scope: (1) Linux: install/configure `ptp4l` as PTP slave to NovAtel `.30`; (2) TRC firmware: add TIME_BITS equivalent to TRC REG1; (3) `MSG_TRC.cs`: add `epochTime`, `activeTimeSource`, `activeTimeSourceLabel` | TRC `udp_listener.cpp`, `MSG_TRC.cs` |
| FW-C3 | BDC Fuji boot status — FUJI_WAIT always times out | ⏳ Open | `fuji.SETUP()` and `fuji.UPDATE()` are deferred until post-boot. At DONE print, `fuji=---` always shown regardless of physical connection because SETUP has not run yet. Fix: run a lightweight Fuji ping or move SETUP earlier in boot sequence | `bdc.cpp` — boot sequence, `fuji.cpp` SETUP() |
| FW-C4 | BDC A1 ARP backoff not working | ⏳ Open | ARP backoff detection not triggering correctly — `a1FailCount` not incrementing as expected when TRC offline. W5500 still blocking. Workaround: use `A1 OFF` serial command when TRC is offline. Root cause: send failure may not be returned correctly from `frameSend()` | `bdc.cpp` — A1 TX path, `frameSend()` return value |
| FW-C5 | Audit IP defines in `defines.hpp` | ⏳ Open | `IP_BDC_BYTES`, `IP_TMC_BYTES`, `IP_MCC_BYTES`, `IP_TRC_BYTES` added. Remaining hardcoded IPs in firmware source need replacing with defines. Full audit of all `.cpp`/`.hpp` files across four controllers | `defines.hpp`, all controller `.cpp` files |

---

## 🟡 MEDIUM — Documentation

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| DOC-1 | Add TRC NTP setup reference to ARCHITECTURE.md | ⏳ Open | Add TRC NTP configuration reference to ARCHITECTURE.md timing section. Include `timesyncd.conf` entry (`NTP=192.168.1.33`, fallback `.208`), `timedatectl` verification command, `systemctl restart systemd-timesyncd`. Cross-reference to JETSON_SETUP.md | `ARCHITECTURE.md` |
| DOC-2 | Create JETSON_SETUP.md | ⏳ Open | New document — full TRC/Jetson Orin NX setup procedure. Minimum scope: OS version (Linux 6.1), NTP configuration (`systemd-timesyncd`), static IP assignment, A1/A2 port config, TRC software install steps. Future scope: `ptp4l` setup when NEW-38d implemented. Authoritative reference for re-imaging or new unit bring-up | New document |
| DOC-3 | File format specs in ICD INT_ENG and INT_OPS | ⏳ Open | Add file format specifications for horizon files, KIZ/LCH uploads, and survey data to both INT_ENG and INT_OPS ICDs. Currently undocumented — integrators have no reference for file structure | `CROSSBOW_ICD_INT_ENG.md`, `CROSSBOW_ICD_INT_OPS.md` |

---

## 🟢 LOW — Deferred / Monitoring

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| FSM-1 | FSM deadband and slew rate limiter | ⏳ Deferred | FSM deadband (~2–4px / 130–260 counts) and slew rate limiter (~2000 counts/step at 50Hz) — prevents jitter and mechanical stress at low error signals | `bdc.cpp` `PidUpdate()`, `fmc.cpp` `write_x_pos()` / `write_y_pos()` |
| FMC-STM32-1 | FMC STM32 migration paused | ⏳ Deferred | Migration from SAMD21 to STM32F7 blocked on shared SPI bus conflict — W5500, FSM DAC, ADC on OpenCR. All `#ifdef PLATFORM_STM32` scaffolding in place. Resume when SPI bus sharing resolved | `fmc.cpp`, `fmc.hpp` |
| BDC-2 | Fuji startup comms errors | ⏳ Deferred | Spurious comms errors during Fuji VIS camera settling period after boot — system recovers automatically | `bdc.cpp` — Fuji comms init |
| GUI-6 | Rolling max stats to TRC tab | ⏳ Open | Extend dt/HB rolling max stats to TRC controller tab in ENG GUI — consistent with BDC and FMC tabs updated S35/36 | ENG GUI TRC tab form |
| TRC-M9 | Deprecate TRC port 5010 | ⏳ Deferred | Legacy 64B binary port. Remove from TRC firmware and C# after HW validation confirms port 10018 fully operational | TRC `udp_listener.cpp`, relevant C# client |
| TRC-MUTEX | `buildTelemetry()` race condition in TRC | ⏳ Deferred | Low priority — mutex on `buildTelemetry()` race condition. Linux threading means concurrent access to telemetry struct is possible | TRC `udp_listener.cpp` |
| TRC-MULTICAST | Video multicast `0xD1` not deployed | ⏳ Pending | `0xD1 ORIN_SET_STREAM_MULTICAST` wired in ICD, binary handler `needs impl` in TRC. Currently unicast only | TRC `udp_listener.cpp` — `0xD1` handler |
| TRC-FRAMERATE | 30fps option `0xD2` not deployed | ⏳ Pending | `0xD2 ORIN_SET_STREAM_60FPS` wired in ICD, binary handler `needs impl`. ASCII `FRAMERATE 30` works. Binary path fixed 60fps only | TRC `udp_listener.cpp` — `0xD2` handler |
| DEPLOY-3 | Sustained bench test | ⏳ Pending | All five controllers running simultaneously for full session duration. Verify no memory leaks, socket drops, stream degradation, or watchdog trips | — |
| DEPLOY-4 | Verify `.33` GPS lock before mission | ⏳ Pending | Confirm Phoenix Contact FL TIMESERVER has GPS lock (LOCK LED steady) before relying on it as primary NTP/Stratum 1. Without GPS lock degrades to internal oscillator | — |

---

## Reference — Firmware and ICD Versions

| Item | Value |
|------|-------|
| ICD version | INT_ENG v3.3.8 (session 29, IPGD-0003), INT_OPS v3.3.7 (session 29, IPGD-0004), EXT_OPS v3.3.0 (IPGD-0005) |
| MCC firmware | v3.2.3 — STM32F7, OpenCR board library |
| BDC firmware | v3.2.3 — STM32F7, OpenCR board library |
| TMC firmware | v3.2.3 — STM32F7, OpenCR board library |
| FMC firmware | v3.2.3 — SAMD21, Arduino (STM32 migration deferred) |
| TRC firmware | v3.0.1 — Jetson Orin NX, Linux 6.1 |
| NTP primary | 192.168.1.33 — Phoenix Contact FL TIMESERVER (HW Stratum 1, GPS-disciplined) |
| NTP fallback | 192.168.1.208 — Windows HMI (w32tm) |
| PTP grandmaster | 192.168.1.30 — NovAtel GNSS receiver (IEEE 1588, domain 0, 1Hz sync, 2-step) |
| PTP status | Disabled fleet-wide — FW-B3 workaround |

---

## Reference — W5500 Socket Budget

| Controller | Sockets (PTP disabled) | Sockets (PTP enabled) | Notes |
|------------|----------------------|----------------------|-------|
| MCC | 6/8 | 8/8 | udpA1, udpA2, udpA3, gnss.rx:3001, gnss.tx:3002, ipg(HEL). NTP shares udpA2 |
| BDC | 7/8 | 7/8 | udpA1, udpA2, udpA3, gimbal:7777, gimbal:7778, ptp:319, ptp:320. NTP/TRC/FMC share udpA2. ptp.INIT() unconditional |
| TMC | 4/8 | 4/8 | udpA1, udpA2, ptp:319, ptp:320. NTP shares udpA2. ptp.INIT() unconditional |
| FMC | 2/8 | 4/8 | udpA1, udpA2. NTP shares udpA2. ptp.INIT() gated — FW-B4 pending |
| TRC | N/A — Linux | N/A — Linux | OS kernel manages sockets — no W5500 hardware limit |

---

## Reference — ICD Reserved Slots Available for Assignment

| Byte | Current | Notes |
|------|---------|-------|
| `0xA1` | RES — retired inbound, still used as outbound REG1 CMD_BYTE | Do not reuse until outbound usage retired — FW-C8 |
| `0xA3` | RES — retired, actively rejects with STATUS_CMD_REJECTED | Removal of handler needed before reuse — FW-C8 |
| `0xAF` | **Assigned S30** — `SET_TIMESRC` — FW-C7 pending implementation | payload: 0=OFF, 1=NTP, 2=PTP, 3=AUTO |
| `0xE5` | RES | Available |
| `0xEE` | RES | Available |
| `0xEF` | RES | Available |
| `0xF8` | RES | Available |
| `0xF9` | RES | Available |
| `0xFD` | RES | Available |
