# CROSSBOW — Open Action Items
**Last updated:** 2026-04-06 (session 28)
**ICD Reference:** v3.3.7 (session 37)
**Closed items:** see `Embedded_Controllers_CLOSED_ACTION_ITEMS.md`

---

## 🔴 HIGH

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| THEIA-SHUTDOWN | Clean THEIA/system shutdown — graceful STANDBY→OFF | ⏳ Pending | Laser safe, relays off, state→OFF, HMI disconnect. Define shutdown sequence for commanded shutdown vs power loss. Review MCC/BDC responsibilities. No progress S27→S36. | THEIA `.cs` shutdown handler / state machine |
| HMI-A3-18 | LCH/KIZ/HORIZ bulk upload bench test | ⏳ Partially analyzed | Architecture fully analyzed session 28. `0xAC SET_BDC_HORIZ` confirmed accessible on A2 — no BDC FW changes needed. Horizon `.txt` format: 360 lines, one float per line (azimuth 0°–359°). BDC sends one `0xAC` frame per degree (plen=6: uint16 az + float el). File format specs to be added to ICD (DOC-3). Validation and ENG GUI load capability: C# emplacement GUI work — out of scope firmware. | C# emplacement GUI — `horizFileGenerator.cs` |
| GUI-1 | MCC + BDC ENG GUI A2 timeout | ⏳ Open | Not receiving on A2 — THEIA A3 works fine. Debug: confirm `ParseA2` (not `ParseA3`) being called, add print at top of RX loop, confirm registration burst landing, check `HB_RX_ms` staleness logic | `mcc.cs`, `bdc.cs`, ENG GUI main form |
| GUI-2 | HMI robust testing — live HW | ⏳ Pending | Full engagement sequence, mode transitions, fire control chain end-to-end. Requires all five controllers live on bench | HW — no code changes |
| FW-B3 | PTP DELAY_REQ W5500 contention — all PTP disabled | ⏳ Open | When two or more controllers have PTP active simultaneously, W5500 blocks ~40ms per DELAY_REQ on ARP resolution, saturating main loop. Symptoms: dt spikes, A1 stream drops, A2 stalls. **Current workaround: `isPTP_Enabled=false` fleet-wide — NTP only in production.** Proposed fixes: (1) `suppressDelayReq` flag per-controller; (2) staggered DELAY_REQ timing — FMC +50ms offset after FOLLOW_UP | `ptpClient.cpp/hpp` — DELAY_REQ transmission logic |

---

## 🟡 MEDIUM — HMI / Feature

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| PARALLAX | Range-based parallax | ⏳ Pending | Architecture confirmed — BDC owns all logic, FMC unaware. Two components: (1) VIS FSM home offset — range-dependent, formula/LUT TBD. (2) VIS→MWIR fixed offset — constant delta on `0xD0`. Range source arbitrator: RS232 rangefinder → radar CUE → TRC image estimate. New `targetRange` register in BDC. New `rangeSource` status register. Calibration via eng GUI + THEIA HMI (new ICD command needed) | `bdc.cpp`, `bdc.hpp` |
| HMI-AWB | VIS camera AWB passthrough to HMI | ⏳ Pending | VIS camera auto white balance — expose over UDP from ENG GUI and THEIA. Currently accessible via TRC UDP debug interface only. `0xC4` deprecated/reserved. Needs: (1) new ICD command byte assigned, (2) add to `EXT_CMDS_BDC[]`, (3) BDC→TRC dispatch, (4) TRC binary handler (`needs impl`), (5) C# wiring in `frmBDC.cs` and THEIA. Merged from S27 HMI-AWB + BDC-3 + PTP GUI-4 | `frmBDC.cs`, `bdc.hpp`, TRC `udp_listener.cpp` |
| HMI-COCO | COCO class filter and enable to HMI | ⏳ Pending | Expose COCO class filter (`0xD9`) and enable (`0xDF`) to THEIA HMI and ENG GUI. Commands already in ICD and firmware whitelist — C# wiring only | `frmBDC.cs`, THEIA HMI `.cs` form |
| HMI-TRACKGATE | Track gate size persistence on reset | ⏳ Pending | Decision needed: restore last operator-set gate size on tracker reset/reacquisition or reset to default. If persist: THEIA caches last sent `0xD5` values and re-sends on tracker reset | THEIA `frmMain.cs` |
| GUI-3 | MCC vs BDC time source label discrepancy | ⏳ Open | Two sub-items: (1) `MSG_BDC.cs` — fix `activeTimeSource` to return `TIME_SOURCE` enum; redirect to read from `TimeBits` (`tb_usingPTP` / `tb_isNTP_Synched`) not `DeviceReadyBits` — likely root cause of MCC vs BDC display discrepancy. (2) `MSG_TMC.cs` — align to `tb_*` prefix naming (low priority, cosmetic) | `MSG_BDC.cs`, `MSG_TMC.cs` |
| GUI-5 | `lbl_gimbal_hb` — `gimbalMSG.HB_TX_ms` missing | ⏳ Open | `gimbalMSG.HB_TX_ms` property does not exist on `MSG_GIMBAL`. Find correct HB property name and fix binding in `frmBDC` | `frmBDC.cs`, `MSG_GIMBAL.cs` |
| GUI-7 | HB and status timing audit — all child devices | ⏳ Open | Review HB timing and status display for all child devices across ENG GUI — confirm correct property bindings, consistent HB_TX_ms / HB_RX_ms / dt rolling max display, and liveness indicators for: GIMBAL, FMC, TRC, TMC, GNSS, HEL, BAT, CRG. GUI-5 is a symptom of a broader gap | `frmBDC.cs`, `frmMCC.cs`, all `MSG_*.cs` classes |
| NEW-33 | MCC REG1 VOTE_BITS byte 3 bit 0 wrong field | ⏳ Open — hold pending HW test | Currently packs `isLaserTotalHW_Vote_rb` — should be `isNotBatLowVoltage()`. Hold until HW test confirms no downstream dependency on current (wrong) value | `mcc.cpp` — `buildReg01()` |

---

## 🟡 MEDIUM — Firmware / Integration

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| BDC-1 | Gate Fuji/MWIR comms on relay state | ⏳ Pending | Disable comms to Fuji/MWIR when their relays are off — suppress spurious lost-message errors. Tie to `SET_BDC_RELAY_ENABLE` state in BDC | `bdc.cpp`, `bdc.hpp` |
| BIT-CLEANUP | Status bits audit — `defines.cs` bitmask enums | ⏳ Pending | `HUD_OVERLAY_BITS`, `VOTE_BITS_MCC`, `VOTE_BITS_BDC` use different C# bitmask enum pattern vs `defines.hpp`. Walk through to confirm intentional or align. Related to TMC `tb_*` prefix inconsistency in TIME_BITS_ALIGNMENT | `defines.cs`, `defines.hpp` |
| GNSS-WATCHDOG | GNSS `isConnected` watchdog bench verify | ⏳ Bench verify | Confirm `DEVICE_READY_BITS` bit 6 drops correctly when NovAtel goes silent. 3s timeout (`GNSS_COMMS_TIMEOUT_MS=3000`) confirmed correct in code review S28. Needs live bench test — disconnect NovAtel UDP, observe bit 6 clear in ENG GUI | `gnss.cpp`, `gnss.hpp` — code correct, test only |
| FW-B2 | MCC RX-side SEQ gap counter for TMC A1 stream | ⏳ Open | Track per-slot SEQ discontinuities on MCC receive side for TMC A1 stream — consistent with gap counter on BDC/FMC tabs | `mcc.cpp` — A1 RX handler |
| FW-B4 | MCC and FMC `ptp.INIT()` not unconditional at boot | ⏳ Open | MCC and FMC gate `ptp.INIT()` behind `if (isPTP_Enabled)` — `TIMESRC PTP` at runtime sets flag but sockets never opened, silent failure. Fix: remove guard, call `ptp.INIT()` unconditionally at boot matching BDC/TMC pattern. `isPTP_Enabled` gates `ptp.UPDATE()` only. MCC socket impact: 6/8→8/8 (designed-in). FMC: 2/8→4/8. MCC: ensure `ptp.INIT()` placed after GNSS and HEL init | `mcc.cpp` INIT() ~line 49, `fmc.cpp` INIT() ~line 92 |
| FW-C3 | BDC Fuji boot status — init runs post-boot | ⏳ Open | `fuji.SETUP()` and `fuji.UPDATE()` are not called during boot state machine — `fuji.isConnected` is never true at FUJI_WAIT step, so FUJI_WAIT always times out at 5s and `fuji=---` always shown at DONE regardless of physical connection. Revisit boot sequence to allow Fuji init to run during FUJI_WAIT, or restructure so DONE status accurately reflects Fuji state. Also: `FUJI: READY` print fires when init commands sent (not when lens responds) — misleading name, should be `FUJI: INIT COMPLETE` or gated on `isConnected`. | `bdc.cpp` RunBoot(), UPDATE(), `fuji.cpp` RunOnce() |
| FW-C4 | BDC A1 ARP backoff not working | ⏳ Open | W5500 ARP blocking occurs in `beginPacket()` not `endPacket()`. Timing wrapper placed around `endPacket()` only — does not capture the full block duration. `t0` needs to move before `beginPacket()`. Alternatively investigate W5500 ARP cache pre-population or static ARP. **Workaround: `A1 OFF` via serial — suppresses fire relay to TRC, eliminates loop stall.** | `bdc.cpp` — `SEND_FIRE_STATUS_TO_TRC()` |
| FW-C5 | Audit/consolidate IP defines in `defines.hpp` | ⏳ Open | `IP_BDC_BYTES`, `IP_TMC_BYTES`, `IP_MCC_BYTES` added session 28. `IP_TRC_BYTES` confirmed existing. `PORT_A1/A2/A3` confirmed in `frame.hpp`. Audit remaining hardcoded `192.168.1.x` literals across all controller `.cpp` files and replace with defines. Cross-check all controllers use defines not raw literals. | `defines.hpp`, `mcc.cpp`, `bdc.cpp`, `tmc.cpp`, `fmc.cpp` |
| FW-14 | GNSS socket bug in MCC `RUNONCE` | ⏳ Open — fix when on HW | `RUNONCE` case 6 and `EXEC_UDP` use `udpRxClient` to send commands — should use `udpTxClient` (port 3002). Confirmed in `gnss.cpp` code review S28 | `gnss.cpp` — `RUNONCE()` case 6, `EXEC_UDP()` |
| NEW-38d | TRC PTP integration | ⏳ Pending | TRC currently uses `systemd-timesyncd` NTP only — no PTP path, no TIME_BITS in REG1. Scope: (1) Linux: install/configure `ptp4l` as PTP slave to NovAtel `.30`; (2) TRC firmware: add TIME_BITS equivalent to TRC REG1; (3) `MSG_TRC.cs`: add `epochTime`, `activeTimeSource`, `activeTimeSourceLabel` | TRC `udp_listener.cpp`, `MSG_TRC.cs` |

---

## 🟡 MEDIUM — Documentation

| ID | Item | Status | Detail | Files |
|----|------|--------|--------|-------|
| DOC-1 | Add TRC NTP setup reference to ARCHITECTURE.md | ⏳ Open | Add TRC NTP configuration reference to ARCHITECTURE.md timing section. Include `timesyncd.conf` entry (`NTP=192.168.1.33`, fallback `.208`), `timedatectl` verification command, `systemctl restart systemd-timesyncd`. Cross-reference to JETSON_SETUP.md | `ARCHITECTURE.md` |
| DOC-2 | Create JETSON_SETUP.md | ⏳ Open | New document — full TRC/Jetson Orin NX setup procedure. Minimum scope: OS version (Linux 6.1), NTP configuration (`systemd-timesyncd`), static IP assignment, A1/A2 port config, TRC software install steps. Future scope: `ptp4l` setup when NEW-38d implemented. Authoritative reference for re-imaging or new unit bring-up | New document |
| DOC-3 | Add file format specs to ICD INT_ENG and INT_OPS | ⏳ Open | Horizon `.txt` format (360 floats, one per line, index 0=azimuth 0°), KIZ/LCH PAM file format, and survey points format (semicolon-delimited: ID;LAT;LNG;ALT) are documented only in `EMPLACEMENT_GUI_USER_GUIDE.md`. Add file format specifications to `CROSSBOW_ICD_INT_ENG.md` and `CROSSBOW_ICD_INT_OPS` alongside `0xAC`, `0xA7`, `0xA8` command definitions. Use emplacement GUI user guide as authoritative source. Future: add horizon/KIZ/LCH validation and load to ENG GUI. | `CROSSBOW_ICD_INT_ENG.md`, `CROSSBOW_ICD_INT_OPS` |

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
| ICD version | v3.3.7 (session 37) — INT_ENG (IPGD-0003), INT_OPS (IPGD-0004), EXT_OPS (IPGD-0005) |
| MCC firmware | v3.2.2 — STM32F7, OpenCR board library |
| BDC firmware | v3.2.2 — STM32F7, OpenCR board library |
| TMC firmware | v3.2.2 — STM32F7, OpenCR board library |
| FMC firmware | v3.2.2 — SAMD21, Arduino (STM32 migration deferred) |
| TRC firmware | v3.0.1 — Jetson Orin NX, Linux 6.1 |
| NTP primary | 192.168.1.33 — Phoenix Contact FL TIMESERVER (HW Stratum 1, GPS-disciplined) |
| NTP fallback | 192.168.1.208 — Windows HMI (w32tm) |
| PTP grandmaster | 192.168.1.30 — NovAtel GNSS receiver (IEEE 1588, domain 0, 1Hz sync, 2-step) |
| PTP status | Disabled fleet-wide — FW-B3 workaround |

## Reference — W5500 Socket Budget

| Controller | Sockets (PTP disabled) | Sockets (PTP enabled) | Notes |
|------------|----------------------|----------------------|-------|
| MCC | 6/8 | 8/8 | udpA1, udpA2, udpA3, gnss.rx:3001, gnss.tx:3002, ipg(HEL). NTP shares udpA2 |
| BDC | 7/8 | 7/8 | udpA1, udpA2, udpA3, gimbal:7777, gimbal:7778, ptp:319, ptp:320. NTP/TRC/FMC share udpA2. ptp.INIT() unconditional |
| TMC | 4/8 | 4/8 | udpA1, udpA2, ptp:319, ptp:320. NTP shares udpA2. ptp.INIT() unconditional |
| FMC | 2/8 | 4/8 | udpA1, udpA2. NTP shares udpA2. ptp.INIT() gated — FW-B4 pending |
| TRC | N/A — Linux | N/A — Linux | OS kernel manages sockets — no W5500 hardware limit |
