# CROSSBOW System Architecture

**Document Version:** 3.3.4
**Date:** 2026-04-08
**ICD Reference:** ICD v3.4.0
**Status:** MCC unified V1/V2 hardware abstraction; FW v3.3.0.

**v3.3.4 changes (MCC unification — 2026-04-08):**
- §9 MCC Internal Architecture: updated for unified V1/V2 hardware abstraction (`hw_rev.hpp`). FW version updated to 3.3.0.
- §9.1 Role: V1/V2 power architecture variants documented.
- §9.5 Register bits table: `STAT_BITS2` → `POWER_BITS` reference updated; `HEALTH_BITS` noted.
- §9.6 Build Configuration (new): `hw_rev.hpp` table parallel to §11.6 TMC pattern.
- §15 Version table: MCC `3.1.0` → `3.3.0`.
- §16 Compatibility matrix: MCC HW_REV self-detection entry added.

**v3.3.3 changes (session 30 — 2026-04-07):**
- §11 TMC Internal Architecture: updated for unified V1/V2 hardware abstraction (`hw_rev.hpp`). Hardware variants documented, FW version updated to 3.3.0, socket budget unchanged (4/8).
- §11.1 Role: updated to reflect V1/V2 hardware differences (pump power supply, heater, external ADC).
- §11.3 Hardware: table updated with V1/V2 columns.
- §11.4 Temperature Channels: tv3/tv4 noted as V1-only.
- §15 Version table: TMC `3.2.0` → `3.3.0`.
- §16 Compatibility matrix: TMC HW_REV self-detection entry added.

**v3.3.2 changes (session 29):**
- §4.2 (new): C# ENG GUI client connect sequence — authoritative standard for all four controllers (A2 and A3). Single `0xA4` registration on connect (burst retired — firmware replay fix makes it unnecessary). `_lastKeepalive` only updated in `SendKeepalive()` — not on every `Send()`. Any valid frame updates `isConnected` and `lastMsgRx` — not just `0xA1`. `connection established` logged immediately in receive loop on first valid frame. `KeepaliveLoop` redundant elapsed check removed — `SendKeepalive()` called directly on every timer tick.
- §4.2 Firmware replay window fix (all six A2/A3 handlers): new client detection (`isNewClient` check + `a_seq_init = false`) moved **before** `frameCheckReplay()` in all handlers — MCC A2/A3, BDC A2/A3, TMC A2, FMC A2. Prevents permanent lockout of reconnecting clients. Fixes `drop #2 after 0.0s` on BDC A3 (THEIA). Firmware version bumped to v3.2.3.
- §4.2 A3 connect sequence: auto-subscribe (`UnsolicitedMode = true`) removed from A3 connect path on MCC and BDC — user controls via checkbox. A3 now sends single `0xA4` registration on connect matching A2 pattern.
- §17 Open items: GUI-1 closed. FMC-NTP added (FMC dt elevated — suspected NTP/USB CDC loop blocking). GUI-8 added (TRC C# client model pending).

**v3.3.1 changes (session 28):**
- §10.1 BDC boot sequence: `FUJI_WAIT(5s)` step added between `PTP_INIT` and `DONE`. Non-blocking — advances when `fuji.isConnected` or after 5s timeout. `DONE` delay reduced to 0.5s. Boot completion print now shows subsystem status: `gimbal`, `trc`, `fmc`, `fuji`, `ntp`. Note: `fuji.SETUP()` and `fuji.UPDATE()` run post-boot only — `fuji=---` always shown at DONE regardless of physical connection (FW-C3 open).
- §12.3 FMC time source: `isNTP_Enabled` default changed `false` → `true`. SAMD-NTP root cause identified as `PrintTime()` calling `Serial` not `SerialUSB` — removed all `PrintTime()` calls from FMC serial command handlers. NTP confirmed working on bench with USB CDC active. SAMD-NTP closed.
- §6.5 Serial debug standardization (all four embedded controllers): HELP command restructured — COMMON block (identical across all controllers) + SPECIFIC block (local hardware). Unicode box style `╔══╗`. Serial buffer changed from `String serialBuffer` to `static char[64]` + `static uint8_t serialLen` on all four `.ino` files — eliminates heap fragmentation. `handleCommand` signature changed to `const char*` throughout.
- §6.6 A1 TX control: `isA1Enabled` firmware-only flag added to all four controllers (`mcc.hpp`, `bdc.hpp`, `tmc.hpp`, `fmc.hpp`). Serial command `A1 ON|OFF` on all controllers. `SEND_FIRE_STATUS()` on MCC and `SEND_FIRE_STATUS_TO_TRC()` on BDC gated on flag. Default `true` — no behavior change at boot.
- §6.6 BDC A1 ARP backoff added — `a1FailCount`/`a1BackoffCount`/`A1_FAIL_MAX=3`/`A1_BACKOFF_TICKS=5`. Note: backoff detection not working (FW-C4 open) — use `A1 OFF` as workaround when TRC offline.
- §6.5 TIME command: `lastSync ms` → `ms ago` (`millis() - lastSyncMs`) on all controllers. `PrintTime()` calls gated on `isSynched` — prints `[not synced]` when not synced (STM32 controllers) or `[not synced]`/`[see PTPDEBUG]` (FMC). `NTP enabled`, `NTP offset_us`, `NTP lastSync ms ago` fields added to TMC TIME command (were missing). NTP fallback prints gated on `DEBUG_LEVEL >= MIN` fleet-wide.
- §6.5 PTPDIAG command added to all four controllers — toggles `ptp.suppressDelayReq` for FW-B3 testing.
- §10.5 IP defines: `IP_BDC_BYTES`, `IP_TMC_BYTES`, `IP_MCC_BYTES` added to `defines.hpp`. `IP_TRC_BYTES` confirmed existing. Hardcoded IPs replaced in `SEND_FIRE_STATUS()` (MCC) and `SEND_FIRE_STATUS_TO_TRC()` (BDC). Audit pending (FW-C5).
- §6.9 (new): Serial debug standards — serial buffer pattern, HELP box structure, COMMON/SPECIFIC command split, TIME command output format, A1 TX control, FMC SerialUSB constraint. Authoritative reference for adding new serial commands to any controller.
- §17 Open items: FW-C3, FW-C4, FW-C5, DOC-3 added. SAMD-NTP closed.

**v3.3.0 changes (session 28):**
- §2.2 MCC socket budget: corrected "all 8 allocated" — actual state is 6/8 with PTP disabled (default), 8/8 with PTP enabled. `ptp.INIT()` is gated by `isPTP_Enabled` at boot — FW-B4 open to remove gate and match BDC/TMC unconditional pattern.
- §2.2a (new): Unified fleet W5500 socket budget summary — authoritative reference for all four embedded controllers. Verified from source (mcc.hpp/cpp, bdc.hpp/cpp, tmc.hpp/cpp, fmc.hpp/cpp, gnss.hpp) session 28.
- §11.2 TMC socket budget: added note — `ptp.INIT()` unconditional at boot (sockets 3/4 always allocated regardless of `isPTP_Enabled`). Correct pattern — FW-B4 will align MCC/FMC to match.
- §12.2 FMC socket budget: corrected — 2/8 with PTP disabled (current default — `ptp.INIT()` gated), 4/8 with PTP enabled. FW-B4 will remove gate.
- §12 FMC header: FW version corrected to v3.2.0; platform confirmed SAMD21.
- §11 TMC header: FW version corrected to v3.2.0; platform confirmed STM32F7 / OpenCR.
- §3 Codebase inventory: platform labels corrected — MCC/TMC/BDC are STM32F7 (OpenCR board library), FMC is SAMD21, TRC is Jetson Orin NX Linux 6.1.
- §2.5 (new): TRC timing — `systemd-timesyncd` NTP configuration documented. See DOC-1/DOC-2.

**v3.2.1 changes (session 37):**
- §7.4 HYPERION flow diagram: Stellarium updated — now feeds `trackLogs["STELLA"]` via synthetic LLA (ned2lla conversion). Was: "az/el reference only — not in trackLogs".
- §7.4 Sensor Input Reference: Stellarium track key updated to `"STELLA"`.

**v3.2.0 changes (session 37):**
- §2 Network topology: HYPERION `.206` row added. IP assignment note added.
- §2.4 External topology diagram: CUE output port `10009` → `15009`. HYPERION `.206` added.
- §3 Codebase inventory: CUE SIM added.
- §5 Port reference: 15000 EXT_OPS block added — `15001` HYPERION aRADAR, `15002` HYPERION aLORA, `15009` THEIA CueReceiver, `15010` HYPERION CUE output.
- §7.4 HYPERION architecture: sensor input ports updated (`10009`→`15001`, `10032`→`15002`). CUE output `10009`→`15009`. `ToArray()` legacy reference replaced with `BuildCueFrame()`. Sensor input reference table updated. Engagement sequence port references updated.
- §16 Compatibility matrix: EXT_OPS port migration entry added.

**v3.1.0 changes (session 35/36):**
- Section 6.6: A1 stream ARP backoff added (TMC→MCC, FMC→BDC) — `A1_FAIL_MAX=3`, `A1_BACKOFF_TICKS` — prevents W5500 ARP-stall when peer offline; serial command `A1 ON|OFF` for testing
- Section 6.7: A2 unified client model — `0xA4 FRAME_KEEPALIVE` replaces `EXT_FRAME_PING` as registration/keepalive; `0xA0 SET_UNSOLICITED` now sets per-slot `wantsUnsolicited` flag; `0xA1` and `0xA3` retired as inbound commands (return `STATUS_CMD_REJECTED`); `isUnSolicitedEnabled` global flag retired across all controllers
- Section 9.5, 10.4, 11.5, 12.3: `GetCurrentTime()` holdover rewrite — EPOCH_MIN_VALID_US guard, `_lastGoodTimeUs`/`_lastGoodStampUs` latch, free-run from latch when both PTP and NTP invalid; `activeTimeSource = NONE` during holdover
- Section 10.4, 11.5, 12.3: `isPTP_Enabled` defaults to `false` across all controllers (FW-B3 deferred — W5500 DELAY_REQ contention with simultaneous PTP clients); serial `TIMESRC PTP` to enable
- Section 12.3: FMC `isNTP_Enabled` defaults to `true` (changed session 28 — SAMD-NTP resolved). `isNTP_Enabled` was `false` (SAMD21 NTP timing bug workaround) — root cause identified as `PrintTime()` calling `Serial` not `SerialUSB`. All `PrintTime()` calls removed from FMC. NTP confirmed working on bench with USB CDC active simultaneously.
- Section 15: Firmware versions updated to session 36 state
- Section 17: Open items updated

**v3.0.8 changes (session 32):**
- Section 9.5: Register table updated — MCC `STAT_BITS2` bits 0–2 moved to `TIME_BITS` byte 253; `TIME_BITS` row added
- Section 10: BDC boot sequence updated — `PTP_INIT(1s)` added; `DONE` renumbered
- Section 10.2: BDC subsystem drivers — PTP row added
- Section 10.3 (new): BDC W5500 socket budget — 7/8 allocated
- Section 10.4 (new): BDC time source architecture — mirrors MCC section 9.5
- Section 11.2 (new): TMC W5500 socket budget — 4/8 allocated

**v3.0.7 changes (session 29):**
- Section 9: PTP subsystem fully documented — `ptpClient` class, fallback chain timing, `ntpSuppressedByPTP`, `TIMESRC`/`TIME`/`PTPDEBUG` serial commands, `PTPMODE ENABLE_FINETIME` corrected
- Section 17: NEW-36 closed (HW verified), NEW-37 closed (MSG_MCC.cs + ENG GUI verified)
- Section 17: FW-1 (`PTPDEBUG`), FW-2 (`TIMESRC` UDP), FW-3 (fallback test), NEW-38 (propagate to BDC/TMC/FMC/TRC) remain open

**v3.0.6 changes (session 28):**
- Section 2: GNSS .30 now documented as PTP grandmaster (IEEE 1588, PTP_UDP profile, multicast, domain 0, 1 Hz, 2-step; UTC_TIME timescale confirmed)
- Section 5: PTP ports 319/320 added to MCC socket table; W5500 budget now 8/8 (fully allocated)
- Section 9: MCC time source hierarchy documented (PTP primary → NTP primary → NTP fallback)
- Registers: DEVICE_ENABLED bit4=isPTP_Enabled, DEVICE_READY bit4=isPTP_Ready, STATUS_BITS2 bit2=usingPTP (all were RES)
- Section 17: NEW-36 opened — PTP integration HW verify pending

**v3.0.5 changes (session 27):**
- Section 2: NTP topology note updated — `.33` is HW Stratum 1 primary; `.208` Windows HMI is fallback; `.8` is eng IP and must not be used as NTP server
- Section 2: NTP auto-recovery behaviour documented — 3 missed responses (~30s) triggers fallback; 2-minute primary retry; latches on primary when it responds
- Section 17: NEW-35 closed — NTP server address verified and corrected in `defines.hpp` (`IP_NTP_BYTES` = `.33`); fallback `.208` configured by default in `mcc.hpp`

**v3.0.4 changes (session 16):**
- Section 2 network table: THEIA split into two rows — A3 external NIC (.200–.254) and internal NIC (.1–.99)
- Section 2: dual-NIC note added — TMC `IP_INTERNAL_MAX=99` requires internal NIC for A2 eng access

**v3.0.3 changes (session 24):**
- THEIA IP corrected throughout: 192.168.1.208 → 192.168.1.208
- NTP topology corrected: all five controllers target .33 directly — not via THEIA
- ICD filenames updated to current naming convention throughout
- Open items updated to session 24 state

**v3.0.2 changes (session 18):**
- Section 7.4 CUE Packet Format — completely replaced. Old stale 64-byte raw format removed.
  Now shows authoritative 71-byte EXT_OPS framed layout per `CROSSBOW_ICD_EXT_OPS`:
  EXT_OPS header (magic `0xCB 0x48`, CMD, SEQ_NUM, PAYLOAD_LEN, CRC16), 62-byte payload
  with ms timestamp at [0], Heading/Speed replacing vx/vy NED (v3.0.2 field change noted).
- Section 7.4 engagement sequence — steps 2/3 updated: `CueReceiver` shared library replaces
  old `RADAR` class reference.

**v3.0.1 changes (session 18):**
- Section 4.3: TransportPath / NEW-12 confirmed complete — section updated to reflect deployed state
- Section 4.4 / 4.5: Entry point column updated — callers use single `Parse()` dispatcher, not `ParseA2`/`ParseA3` directly
- Section 12.2: FSM position note removed — #7 closed, int16 commanded vs int32 readback confirmed correct distinct types
- Section 16: Compatibility matrix updated — MSG_MCC/BDC deployed, TransportPath complete
- Section 17: Open items updated — TRC-M1/M5/M6/M7, NEW-11/12 all closed. FW #14 (GNSS socket bug) added. NEW-29 (Emplacement guide) added as deferred.

**v3.0.0 changes (session 16):**
- Document version aligned to ICD v3.1.0
- Section 4 (Client Access Model) — new. Defines ENG GUI vs THEIA transport paths, ParseA2/ParseA3 entry points, TransportPath enum
- Section 5 (Consolidated Port Reference) — new. Single source of truth for all ports across all nodes
- Section 6 (IP Range Policy + Framing Protocol) — moved from ICD. ICD now owns commands and registers only
- Section 9 (MCC Internal Architecture) — new
- Section 11 (TMC Internal Architecture) — new
- Section 5.2 port table: Galil corrected to 7777 (cmd TX) and 7778 (data RX)
- Data flows: THEIA→BDC corrected to A3/10050. A2 section corrected — THEIA does not use A2
- Version references updated throughout: ICD v1.7 → v3.0.0, VERSION_PACK updated
- Compatibility matrix and open items updated

---

## 1. System Overview

CROSSBOW is a ground-based directed-energy (HEL) tracking and fire-control system. It integrates
a gimbal-mounted dual-camera payload (VIS + MWIR), a fast steering mirror (FSM), a thermal
management controller, an operator HMI, and external cueing sources (ADS-B, radar, LoRa) into
a unified sensor-to-fire control chain.

The TRC2 legacy tracker has been replaced by TRC (Jetson Orin NX). ICD v3.1.0 migration is
complete across all five embedded controllers (MCC, BDC, TMC, FMC, TRC).

---

## 2. Network Topology

All nodes communicate over a dedicated 1 Gbps Ethernet switch on subnet 192.168.1.x.
NTP server (`.33`) is the HW Stratum 1 primary — all five controllers sync directly to `.33` with `.208` (Windows HMI) as automatic fallback. `.8` is reserved for engineering use and must not be used as an NTP target.

MCC additionally uses the GNSS receiver (`.30`) as a **PTP grandmaster** (IEEE 1588, PTP_UDP profile, multicast `224.0.1.129`, domain 0, 1 Hz sync, 2-step). PTP is MCC's primary time source; NTP is retained as a warm fallback. PTP accuracy is ~1–100 µs (software timestamping); NTP accuracy is ~1–10 ms.

| Node | IP | Role |
|------|----|------|
| HMI (THEIA) | 192.168.1.208 (default) | A3 external NIC — operator workstation, THEIA → MCC/BDC port 10050 |
| HMI (THEIA) | 192.168.1.x (.1–.99)     | Internal NIC — A2 eng access, NTP sync, H.264 video RX from TRC port 5000 |
| HYPERION | 192.168.1.206 (default) | EXT_OPS C2 node — sensor fusion, Kalman filter, CUE relay to THEIA |
| MCC (Arduino) | 192.168.1.10 | Master control — power, laser, GNSS, charger |
| TMC (Arduino) | 192.168.1.12 | Thermal management — coolant, fans, TEC |
| HEL (IPG laser) | 192.168.1.13 | Laser source (read-only, status embedded in MCC REG1) |
| BDC (STM32F7) | 192.168.1.20 | Beam director — gimbal, cameras, FSM, MWIR, fire control |
| Gimbal (Galil) | 192.168.1.21 | Pan/tilt servo drive |
| TRC (Jetson Orin NX) | 192.168.1.22 | Camera capture, tracker, video encoder |
| FMC (SAMD21) | 192.168.1.23 | FSM DAC/ADC, focus stage |
| GPS/GNSS | 192.168.1.30 | NovAtel GNSS receiver — BESTPOS/INS/heading (MCC managed) + **PTP grandmaster** (IEEE 1588, PTP_UDP, multicast, domain 0, UTC_TIME) |
| RPI/ADSB | 192.168.1.31 | ADS-B decoder |
| LoRa | 192.168.1.32 | LoRa/MAVLink track input |
| NTP appliance | 192.168.1.33 | HW Stratum 1 NTP primary → all five controllers direct |
| Windows HMI (THEIA) | 192.168.1.208 | NTP fallback — `w32tm` serving on `.208` NIC |
| RADAR | 192.168.1.34 | Radar track input |

Engineering laptops and ENG GUI PCs: .1–.99 range by convention.
External integration clients (THEIA A3): .200–.254 range by convention.

> **IP assignment note:** THEIA and HYPERION operate in the `192.168.1.200–.254` external range. The addresses shown are IPG reference deployment defaults — both are operator-configurable. The constraint is that they remain in the `.200–.254` range so embedded controllers accept their A3 packets. IPG reserves `.200–.209`; third-party integrators use `.210–.254` by convention.

### 2.2 MCC W5500 Socket Budget

W5500 has 8 hardware sockets. MCC allocates **6/8 with PTP disabled (current default)** or **8/8 with PTP enabled**. Two sockets are reserved for PTP and were designed into the budget from session 28.

| # | Owner | Port | Type | Notes |
|---|-------|------|------|-------|
| 1 | MCC `udpA1` | 10019 | unicast | A1 RX — TMC unsolicited stream |
| 2 | MCC `udpA2` | 10018 | unicast | A2 eng RX+TX — shared: NTP TX/RX (`&udpA2`), TMC TX (`&udpA2`), fire control broadcast to BDC |
| 3 | MCC `udpA3` | 10050 | unicast | A3 external RX+TX |
| 4 | GNSS `udpRxClient` | 3001 | unicast | GNSS data RX from NovAtel |
| 5 | GNSS `udpTxClient` | 3002 | unicast | GNSS cmd TX to NovAtel |
| 6 | IPG `udpClient` | 10011 | unicast | HEL laser status/control |
| 7 | PTP `udpEvent` | 319 | multicast | PTP SYNC RX — **only opened when `isPTP_Enabled=true`** |
| 8 | PTP `udpGeneral` | 320 | multicast | PTP DELAY_REQ/RESP — **only opened when `isPTP_Enabled=true`** |

BAT (RS485), DBU (I2C), CRG (I2C), TPH (I2C) consume no W5500 sockets.

> ⚠️ **FW-B4 open:** MCC `ptp.INIT()` is gated by `if (isPTP_Enabled)` at boot — sockets 7/8 are not opened when PTP is disabled. `TIMESRC PTP` at runtime sets the flag but sockets were never opened — silent failure. Fix: call `ptp.INIT()` unconditionally at boot (matching BDC/TMC pattern). MCC has headroom: 6/8 → 8/8. Ensure `ptp.INIT()` placed after GNSS and HEL init.

> **IGMP snooping** must be OFF on the network switch for PTP multicast (`224.0.1.129`) to flow correctly (per NovAtel PTP docs).

> **THEIA dual-NIC:** one NIC in the `.200–.254` range is the A3 external interface (magic `0xCB 0x58`, port 10050, THEIA → MCC/BDC). A second NIC in the `.1–.99` range is used for A2 engineering access, NTP sync, and H.264 video receive from TRC port 5000. TMC enforces `IP_INTERNAL_MAX = 99` on A2 — TMC commands must originate from the internal NIC (`.1–.99`), not the A3 NIC.

### 2.2a Fleet W5500 Socket Budget — Unified Summary

Authoritative reference for all embedded controllers. Verified from source files session 28. See per-controller sections (§9, §10.3, §11.2, §12.2) for full detail.

| Controller | PTP disabled (default) | PTP enabled | Spare (PTP disabled) | Notes |
|------------|----------------------|-------------|----------------------|-------|
| MCC | 6/8 | 8/8 | 2 | ptp.INIT() gated — FW-B4 pending |
| BDC | 7/8 | 7/8 | 1 | ptp.INIT() unconditional at boot ✅ |
| TMC | 4/8 | 4/8 | 4 | ptp.INIT() unconditional at boot ✅ |
| FMC | 2/8 | 4/8 | 6 | ptp.INIT() gated — FW-B4 pending |
| TRC | N/A | N/A | N/A | Linux kernel sockets — no W5500 hardware limit |

**Shared socket pattern (authoritative):**
- NTP uses `&udpA2` on all four controllers — zero additional sockets
- BDC TRC/FMC command TX borrows `&udpA2` — zero additional sockets
- `isPTP_Enabled` gates `ptp.UPDATE()` on all controllers
- `ptp.INIT()` should be unconditional at boot (BDC/TMC pattern) — MCC/FMC pending FW-B4

### 2.3 Internal Network Topology

Internal subnet — controllers, embedded devices, and engineering tools (.1–.99).
All traffic uses magic `0xCB 0x49` (A1/A2). Video stream is internal unicast.

```
192.168.1.x  INTERNAL (.1–.99)
══════════════════════════════════════════════════════════════════

  NTP appliance (.33)  — primary; .208 Windows HMI is automatic fallback
       │ NTP Stratum 1 (all five controllers sync directly; fallback to .208 after 3 misses)
       ├──► MCC (.10)
       ├──► TMC (.12)
       ├──► BDC (.20)
       ├──► FMC (.23)
       └──► TRC (.22)

  THEIA / HMI (.208) ◄────────────── Video RTP H.264 port 5000 ─────────────────┐
                                                                                  │
                                           ┌── Gimbal (.21) ◄──── 7778 ──┐       │
                                           │   CMD→ 7777                  │       │
  ┌───────────────────────────────────┐    │                              │       │
  │         1 Gbps Ethernet Switch    │    │                              │       │
  └──┬──────┬──────┬──────┬──────┬───┘    │                              │       │
     │      │      │      │      │        │                              │       │
   MCC    TMC    BDC    TRC   FMC        │                              │       │
  (.10)  (.12)  (.20)  (.22)  (.23)       │                              │       │
     │      │      │      │      │        │                              │       │
     │      │      ├──────┘      │        │                              │       │
     │      │      │  A1:10019   │        │                              │       │
     │      │      │  TRC→BDC    │        │                              │       │
     │      │      │  FMC→BDC ◄──┘        │                              │       │
     │      │      │                      │                              │       │
     │      │      ├── Galil (.21) ────────┘                              │       │
     │      │      │   CMD:7777 / DATA:7778                               │       │
     │      │      │                                                       │       │
     │      └──────► A1:10019  TMC→MCC                                    │       │
     │             │                                                       │       │
     │             └──────────────────── A1:10019  MCC→BDC                │       │
     │                                                                     │       │
     └── A2:10018 (ENG GUI ↔ all controllers)                             │       │
                                                                           │       │
  TRC (.22) ──────────────────────────────── video port 5000 ─────────────┘       │
                                                                                   │
  ENG GUI / laptop (.1–.99)                                                        │
    └── A2:10018 → any controller                                                  │
```

### 2.4 External Network Topology

External integration zone — THEIA and integration clients (.200–.254).
All traffic uses magic `0xCB 0x58` (A3 only). Sub-controllers are not reachable.

```
192.168.1.x  EXTERNAL (.200–.254)
══════════════════════════════════════════════════════════════════

  CUE SIM / Third-party integrator (.210–.254)
       │
       │  EXT_OPS framed UDP:15001 (→ HYPERION aRADAR)
       ▼
  HYPERION (.206 default)
       │
       │  EXT_OPS framed UDP:15009 (CMD 0xAA, 71B)
       ▼
  THEIA (.208 default)
       │
       │  A3:10050  magic 0xCB 0x58
       ├────────────────────────────────► MCC (.10)
       │                                  (system state, laser, GNSS, fire vote)
       │
       └────────────────────────────────► BDC (.20)
                                          (gimbal, camera, FSM, PID, fire control)

  Sub-controllers (.12 TMC, .23 FMC, .22 TRC)
       └── No A3 listener — NOT reachable from external zone
```

---

## 2.5 TRC Timing — NTP Configuration

TRC (Jetson Orin NX, Linux 6.1) uses `systemd-timesyncd` for NTP time synchronisation. This is configured as a one-time setup step on first deployment or re-image.

**Configuration (`/etc/systemd/timesyncd.conf`):**
```ini
[Time]
NTP=192.168.1.33
FallbackNTP=192.168.1.208
```

**Commands:**
```bash
sudo vi /etc/systemd/timesyncd.conf    # edit config
sudo systemctl restart systemd-timesyncd
timedatectl status                     # verify — confirm NTP service active and synced
```

**Expected output:** `NTP service: active`, `System clock synchronized: yes`, server showing `192.168.1.33`.

> **Note:** `.33` is the HW Stratum 1 primary (Phoenix Contact FL TIMESERVER, GPS-disciplined). `.208` is the Windows HMI fallback (`w32tm`). `.8` must NOT be used as an NTP target.

> **PTP:** TRC has no PTP implementation. `ptp4l` integration is tracked as NEW-38d. Until then, `systemd-timesyncd` NTP provides ~1–10ms accuracy — sufficient for current operation.

For full TRC/Jetson setup procedure (OS install, static IP, software deployment), see **JETSON_SETUP.md** (DOC-2 — pending creation).

---

## 3. Codebase Inventory

| Name | Platform | Language | Role |
|------|----------|----------|------|
| **TRC** | Jetson Orin NX (Linux 6.1) | C++17 / OpenCV / GStreamer | Camera capture, tracking, H.264 video encode, telemetry |
| **BDC** | STM32F7 (OpenCR board library) | Arduino C++ | System controller, PID loops, fire control, subsystem routing |
| **MCC** | STM32F7 (OpenCR board library) | Arduino C++ | Power management, laser, GNSS, charger, TMC supervision |
| **TMC** | STM32F7 (OpenCR board library) | Arduino C++ | Thermal management — pump, LCM, Vicor, fans, TPH sensor |
| **FMC** | SAMD21 (Arduino) | Arduino C++ | FSM SPI DAC/ADC, M3-LS focus stage I2C. STM32 migration deferred (FMC-STM32-1) |
| **THEIA** | Windows PC | C# / .NET 8 / WinForms | Operator HMI, Xbox controller, video display, fire control |
| **HYPERION** | Windows PC | C# / .NET 8 / WinForms | Sensor fusion — ADS-B, Echodyne, RADAR, LoRa, Stellarium. Track filtering (6-state Kalman), operator track selection, CUE unicast output to THEIA. |
| **CUE SIM** | Windows PC | C# / .NET 8 / WinForms | EXT_OPS test and simulation tool — simulated track injection into HYPERION (UDP:15001) or direct to THEIA (UDP:15009). HyperionSniffer for CUE output verification. |
| **TRC_ENG_GUI_PRESERVE** | Windows PC | C# / .NET 8 / WinForms | Engineering GUI — all 5 controllers via A2 |
| **CROSSBOW lib** | Shared | C# / .NET 8 | Shared class library — namespace CROSSBOW. MSG_MCC, MSG_BDC and all sub-message parsers. Used by both THEIA and TRC_ENG_GUI_PRESERVE. |

---

## 4. Client Access Model

This section defines which software clients connect to which controllers, on which transport port,
and which C# entry points to call. This is the authoritative reference for call site decisions.

### 4.1 Transport Summary

**Client access (software → controller):**

| Client | Transport | Port | Magic | Controllers Accessible | C# Entry Point |
|--------|-----------|------|-------|------------------------|----------------|
| **THEIA (HMI)** | A3 External | 10050 | `0xCB 0x58` | MCC, BDC **only** | `ParseA3(byte[] frame)` |
| **TRC_ENG_GUI_PRESERVE** | A2 Internal | 10018 | `0xCB 0x49` | MCC, BDC, TMC, FMC, TRC | `ParseA2(byte[] msg)` |

Sub-controllers (TMC, FMC, TRC) have **no A3 listener** — they are unreachable from the
external IP range. THEIA never communicates with TMC, FMC, or TRC directly.

**Controller-to-controller (not a client path):**

| Stream | Transport | Port | Magic | Direction | Rate |
|--------|-----------|------|-------|-----------|------|
| TMC REG1 | A1 Internal | 10019 | `0xCB 0x49` | TMC → MCC | 100 Hz |
| FMC REG1 | A1 Internal | 10019 | `0xCB 0x49` | FMC → BDC | 50 Hz |
| TRC REG1 | A1 Internal | 10019 | `0xCB 0x49` | TRC → BDC | 100 Hz |
| Fire control vote (0xAB) | A1 Internal | 10019 | `0xCB 0x49` | MCC → BDC | 100 Hz |
| Fire control status (0xAB) | Raw 5B (no frame) | 10019 | — | BDC → TRC | 100 Hz |

A1 streams are always-on from boot — no registration or `0xA0` enable required.
The BDC→TRC fire control status is raw 5 bytes with no frame wrapper or CRC.

### 4.2 C# ENG GUI Client Connect Sequence (Session 29)

This is the authoritative standard for all four C# controller classes (`mcc.cs`, `bdc.cs`,
`tmc.cs`, `fmc.cs`). Any new controller client must follow this exact pattern.

#### Connect Sequence
```
Start()
  → Send 0xA4 FRAME_KEEPALIVE          // register with firmware — single frame, no burst
  → _lastKeepalive = DateTime.UtcNow   // seed keepalive timer from connect
  (A3 path only: same — no auto 0xA0 subscribe, user controls via checkbox)
```

The registration **burst** (`0xA4 ×3`) is retired. The firmware replay window fix
(session 29) resets `a_seq_init` when a new client is detected — making the burst
unnecessary. A single `0xA4` is sufficient.

#### Keepalive
```
KeepaliveLoop()  — PeriodicTimer every KEEPALIVE_INTERVAL_MS (30s)
  → SendKeepalive()  unconditionally on every tick
      → Send(0xA4)
      → _lastKeepalive = DateTime.UtcNow
```

`_lastKeepalive` is updated **only in `SendKeepalive()`** — not in `Send()`. This
ensures the timer fires reliably every 30s regardless of other TX activity. The
redundant elapsed check (`if (UtcNow - _lastKeepalive) >= interval`) is removed —
the `PeriodicTimer` is the gate.

#### Liveness
```
Receive loop — any valid frame (any CMD_BYTE):
  → isConnected = true
  → HB_RX_ms = (UtcNow - lastMsgRx).TotalMs
  → lastMsgRx = UtcNow
  → if (!_wasConnected): log "connection established", set _connectedSince

0xA1 frames additionally:
  → LatestMSG.Parse(frame)
```

All other frames (ACKs, keepalive responses) still update `isConnected` and
`lastMsgRx`. Connection state does not depend on unsolicited being enabled.

#### Connection Established
`connection established` is logged immediately in the receive loop on the first
valid frame — not in `KeepaliveLoop` (which would delay it by up to 30s).

`connection restored` (after a drop) is still logged in `KeepaliveLoop` since
it requires `_dropCount > 0` context which is managed there.

#### Connection Lost / Drop Detection
```
KeepaliveLoop — on each tick:
  stale = isConnected && (UtcNow - lastMsgRx) > STALE_WARN_MS
  if stale && _wasConnected && uptime > KEEPALIVE_INTERVAL_MS:
    → _dropCount++, _wasConnected = false
    → log "connection lost — drop #N after Xs uptime"
```

`STALE_WARN_MS = 2000ms` — appropriate when unsolicited is enabled (frames at
50–100 Hz). When unsolicited is disabled, keepalive ACKs every 30s keep
`lastMsgRx` fresh — connection loss is not declared between keepalives.

#### Firmware Side — Replay Window Fix
All six frame handlers have new client detection before replay check (session 29):
```cpp
// Moved BEFORE frameCheckReplay():
bool isNewClient = (frameClientFind(...) == -1);
int8_t clientIdx = frameClientRegister(...);
if (isNewClient && clientIdx >= 0)
    a_seq_init = false;   // clean replay window for reconnecting client

// Replay check AFTER:
if (frameCheckReplay(seq, a_last_seq, a_seq_init)) { ... return; }
```
Affected handlers: MCC `handleA2Frame`, MCC `handleA3Frame`, BDC `handleA2Frame`,
BDC `handleA3Frame`, TMC `handleA2Frame`, FMC `handleA2Frame`.

### 4.3 ParseA3 vs ParseA2

`ParseA3` validates the full 521-byte A3 frame (magic `0xCB 0x58`, CRC-16, STATUS byte) before
dispatching to `ParseMSG01`. It sets `LastFrameStatus` on every call regardless of STATUS value.

`ParseA2` receives a raw 512-byte payload with the frame header already stripped upstream. It
performs no magic or CRC validation — the A2 transport layer handles that. Liveness update
(`RX_HB` / `lastMsgRx`) is applied at the top of `ParseA2` unconditionally.

Both entry points are present on `MSG_MCC` and `MSG_BDC`. THEIA must call `ParseA3`.
ENG GUI must call `ParseA2`. Cross-wiring these will silently produce wrong results.

### 4.4 TransportPath

`MSG_MCC` and `MSG_BDC` use a `TransportPath` constructor parameter to select transport
at construction time. `MAGIC_LO` is computed — not hardcoded. `ParseA3` and `ParseA2`
are private; callers always use the single public `Parse(byte[] data)` dispatcher.

```csharp
public enum TransportPath { A2_Internal, A3_External }

// MAGIC_LO is 0x58 for A3_External, 0x49 for A2_Internal
private byte MagicLo => Transport == TransportPath.A3_External ? (byte)0x58 : (byte)0x49;

// Call sites:
new MCC(log, TransportPath.A3_External)   // THEIA — port 10050
new MCC(log, TransportPath.A2_Internal)   // ENG GUI — port 10018
new BDC(log, TransportPath.A3_External)   // THEIA
new BDC(log, TransportPath.A2_Internal)   // ENG GUI
```

Deployed session 16/17. NEW-12 ✅ closed.

### 4.5 ENG GUI — Per-Controller Access

TRC_ENG_GUI_PRESERVE connects to each of the five controllers independently on A2 port 10018.
Each controller has its own message class instance:

| Controller | IP | Port | C# Class | Entry Point |
|------------|----|------|----------|-------------|
| MCC | 192.168.1.10 | 10018 | `MSG_MCC` | `Parse()` → internal `ParseA2` |
| BDC | 192.168.1.20 | 10018 | `MSG_BDC` | `Parse()` → internal `ParseA2` |
| TMC | 192.168.1.12 | 10018 | `MSG_TMC` | `ParseA2` |
| FMC | 192.168.1.23 | 10018 | `MSG_FMC` | `ParseA2` |
| TRC | 192.168.1.22 | 10018 | `MSG_TRC` | `ParseA2` |

### 4.6 THEIA — Per-Controller Access

THEIA connects to MCC and BDC on A3 only:

| Controller | IP | Port | C# Class | Entry Point |
|------------|----|------|----------|-------------|
| MCC | 192.168.1.10 | 10050 | `MSG_MCC` | `Parse()` → internal `ParseA3` |
| BDC | 192.168.1.20 | 10050 | `MSG_BDC` | `Parse()` → internal `ParseA3` |

---

## 5. Consolidated Port Reference

Single source of truth for all UDP ports across all nodes. No port numbers appear elsewhere
in this document or in the ICD — reference this table.

| Port | Label | Protocol | Direction | Controllers | Purpose |
|------|-------|----------|-----------|-------------|---------|
| **10019** | A1 | ICD framed 521B | Sub → Upper | TMC→MCC, FMC→BDC, TRC→BDC | Unsolicited 100 Hz telemetry |
| **10019** | A1 | Raw 5B (0xAB) | BDC → TRC | BDC→TRC | Fire control status relay (no frame wrapper) |
| **10018** | A2 | ICD framed | Bidirectional | All 5 controllers | Internal engineering — ENG GUI + BDC→TRC commands |
| **10050** | A3 | ICD framed | Bidirectional | MCC, BDC only | External — THEIA HMI only |
| **10023** | — | ICD framed | Bidirectional | FMC only | BDC→FMC commands (direct, not via A2) |
| **5000** | Video | RTP/H.264 UDP | TRC → THEIA | TRC | H.264 video stream, 1280×720 @ 60 fps, payload type 96 |
| **5010** | Legacy | Raw 64B binary | Bidirectional | TRC | ⚠ DEPRECATED — pending TRC-M9 removal |
| **5012** | ASCII | UDP text | Bidirectional | TRC | Engineering ASCII commands |
| **7777** | Galil CMD | Galil ASCII | BDC → Gimbal | Galil | Command TX (JG velocity, PA position) |
| **7778** | Galil DATA | Galil ASCII | Gimbal → BDC | Galil | Data/status RX (~125 Hz) |
| **15001** | EXT_OPS | EXT_OPS framed | Integrator → HYPERION | HYPERION aRADAR | Generic sensor input / CUE SIM injection |
| **15002** | EXT_OPS | EXT_OPS framed | Integrator → HYPERION | HYPERION aLORA | LoRa/MAVLink sensor input |
| **15009** | EXT_OPS | EXT_OPS framed | Bidirectional | THEIA CueReceiver | CUE inbound (CMD 0xAA) + status response (CMD 0xAF/0xAB) |
| **15010** | EXT_OPS | EXT_OPS framed | HYPERION → THEIA | HYPERION CUE output | HYPERION forwards Kalman-filtered track to THEIA |

> **Video note:** Stream is currently unicast TRC→THEIA (.208). Multicast option (`0xD1
> ORIN_SET_STREAM_MULTICAST`) is wired in ICD but not yet deployed — see action items.
> 30 fps option via `0xD2` / ASCII `FRAMERATE 30` — see action items.

---

## 6. IP Range Policy and Framing Protocol

### 6.1 IP Range Policy

A single 192.168.1.x subnet is used. All controllers enforce range-based access control on every
incoming packet before any frame parsing:

| Range | Class | Permitted Ports |
|-------|-------|-----------------|
| 192.168.1.1 – 192.168.1.99 | Internal — embedded devices + engineering | A1 (10019) and A2 (10018) |
| 192.168.1.100 – 192.168.1.199 | Reserved | Silently dropped on all ports |
| 192.168.1.200 – 192.168.1.254 | External — THEIA and integration clients | A3 (10050) only |

```cpp
uint8_t src = udpClient.remoteIP()[3];
if      (src >= 1   && src <= 99)  handleInternal(packet);   // A1 or A2
else if (src >= 200 && src <= 254) handleExternal(packet);   // A3
else                               return;                    // reserved — drop
```

> **Trust model:** Enforcement by IP convention. Internal frame magic `0xCB 0x49` is the
> backstop — a packet from a rogue .1–.99 IP still fails the magic check. Physical network
> discipline and IP assignment policy govern the outer layer.

### 6.2 Magic Byte Assignment

Frame structure is identical across all three ports. Magic bytes are the only difference.

| Port | MAGIC_HI | MAGIC_LO | Mnemonic |
|------|----------|----------|----------|
| A1 + A2 (internal) | `0xCB` | `0x49` | CB + `I` (ASCII 0x49) |
| A3 (external) | `0xCB` | `0x58` | CB + `X` (ASCII 0x58) |

> Internal magic bytes are **confidential** — not included in any external-facing document.

### 6.3 Response Frame Geometry (521 bytes, fixed)

```
[0-1]     MAGIC_HI / MAGIC_LO
[2]       SEQ_NUM    uint8  — server rolling counter
[3]       CMD_BYTE   uint8  — ICD command byte
[4]       STATUS     uint8  — 0x00 = OK; non-zero = error
[5-6]     PAYLOAD_LEN uint16 LE — always 0x0200 (512) for REG1
[7-518]   PAYLOAD    512 bytes — register data, zero-padded
[519-520] CRC-16     uint16 BE — CRC-16/CCITT over bytes [0..518]
```

### 6.4 Request Frame Geometry (variable length)

```
[0-1]   MAGIC_HI / MAGIC_LO
[2]     SEQ_NUM     uint8
[3]     CMD_BYTE    uint8
[4-5]   PAYLOAD_LEN uint16 LE — 0 for no-payload commands
[6+]    PAYLOAD     (PAYLOAD_LEN bytes)
[last-2] CRC-16     uint16 BE — over all bytes before CRC field
```

Minimum request frame (no payload): 8 bytes.

### 6.5 CRC-16/CCITT

Poly=0x1021, init=0xFFFF, no reflection, BE wire order.
Known-answer: `crc16("123456789", 9) == 0x29B1`
Shared implementation: `crc.hpp` (all embedded controllers). Runtime-generated table —
verified correct on STM32, SAMD21, Arduino, and x86-64.

> ⚠ **CRC cross-platform verification note:** Past integration issues were observed between
> the STM32 implementation and Linux/x86 implementations. Before first HW integration, perform
> a full end-to-end CRC verification across all five controllers and both C# applications using
> the known-answer test above. Do not assume correctness from unit tests alone — verify with
> live framed packets on the wire. Log as a pre-HW-test checklist item.

### 6.6 A1 — Internal Unsolicited (Always-On Stream)

Sub-controllers boot and immediately begin streaming REG1 to their upper-level controller at
100 Hz. No handshake or `0xA0` enable required.

| Source | Destination | Port | Rate | Content |
|--------|-------------|------|------|---------|
| TMC | MCC (.10) | 10019 | 100 Hz | TMC REG1 (64 bytes) |
| FMC | BDC (.20) | 10019 | 50 Hz | FMC REG1 (64 bytes) |
| TRC | BDC (.20) | 10019 | 100 Hz | TRC REG1 (64 bytes) |
| MCC | BDC (.20) | 10019 | 50 Hz | MCC REG1 via 0xAB fire control vote |
| BDC | TRC (.22) | 10019 | 100 Hz | Fire control status (raw 5B, no frame) |

Liveness timeout: if no A1 packet received within `2 × expected_interval` (200 ms), the
`DEVICE_READY` bit for that source clears. Stream resumes automatically on reconnect.

**A1 ARP backoff (session 36):** When the peer is offline, W5500 ARP resolution blocks for ~40 ms per send attempt, saturating the main loop. After `A1_FAIL_MAX = 3` consecutive send failures, the A1 send is suppressed for `A1_BACKOFF_TICKS` cycles (~2 s at the controller's stream rate). Recovery is instant — first successful send clears both counters. Serial command `A1 ON|OFF` allows disabling the A1 stream for bench testing without a connected peer.

### 6.7 A2 — Internal Engineering (Bidirectional, All Controllers)

**Session 35 unified client model** — applies to all five controllers (MCC, BDC, TMC, FMC, TRC):

| Command | Byte | Description |
|---------|------|-------------|
| `FRAME_KEEPALIVE` | `0xA4` | Replaces `EXT_FRAME_PING`. Register/keepalive. Empty payload = ACK only (ping response). Payload `{0x01}` = ACK + solicited REG1 return (rate-gated 1 Hz per slot); suppressed if `wantsUnsolicited=true` on that slot. |
| `SET_UNSOLICITED` | `0xA0` | Sets per-slot `wantsUnsolicited` flag on the sender's client table entry. `{0x01}` = subscribe to 50/100 Hz unsolicited push. `{0x00}` = unsubscribe (client stays registered). Does NOT affect A1 stream. |
| `RES_A1` | `0xA1` | **RETIRED inbound** — returns `STATUS_CMD_REJECTED`. `0xA1` is still used as the outbound `CMD_BYTE` in all REG1 unsolicited frames. |
| `RES_A3` | `0xA3` | **RETIRED** — returns `STATUS_CMD_REJECTED`. |

**Client table:** Any accepted A2 or A3 frame auto-registers the sender and refreshes its 60-second liveness window. Up to **4 simultaneous A2 clients** and **2 simultaneous A3 clients** per controller. `isUnSolicitedEnabled` global flag retired (session 35) — per-slot `wantsUnsolicited` in `FrameClient` replaces it.

**STATUS_BITS bit 7** (`isUnsolicitedModeEnabled`) retired session 35 across all controllers — always `0`. C# callers should not read this bit.

**C# client connect sequence (A2):**
```
Start() → registration burst: FRAME_KEEPALIVE {0x01} ×3  (advances past stale replay window)
        → SET_UNSOLICITED {0x01}                          (subscribe to 50/100 Hz stream)
KeepaliveLoop() → FRAME_KEEPALIVE {} every 30 s           (maintain slot liveness)
```

ENG GUI is the primary A2 client. BDC also uses A2 to issue commands to TRC.

### 6.8 A3 — External (MCC and BDC Only)

THEIA connects here. CMD_BYTE whitelist (`EXT_CMDS[]`) enforced on all received frames.
Up to **2 simultaneous external clients** per controller (MCC and BDC independently).
Same `0xA0` registration / 60-second liveness model as A2.

### 6.9 Serial Debug — Standards (Session 28)

All four embedded controllers share a unified serial debug architecture. Any new command
added to any controller must conform to this standard.

#### Serial Buffer

All four `.ino` files use identical fixed-size char buffer pattern:

```cpp
// ── Serial input buffer ───────────────────────────────────────────────────
static char    serialBuffer[64];
static uint8_t serialLen = 0;
```

`handleSerialInput()` reads characters into the buffer, null-terminates on `\n`/`\r`, and
calls `parseSerialCommand(serialBuffer, serialLen)`. Characters beyond 63 are silently
dropped — no heap allocation, no String fragmentation.

Handler signatures are `const char*` throughout:
```cpp
void parseSerialCommand(const char* input, uint8_t len);
void handleCommand(const char* command, const char* payload);
```

Re-wrap to `String` occurs only at the class boundary:
```cpp
mcc.SERIAL_CMD(String(command), String(payload));   // .ino → class boundary only
```

**FMC exception:** uses `SerialUSB` not `Serial`. All handler logic is identical; only
the serial object name differs.

#### HELP Box Structure

All controllers print HELP using Unicode box drawing with a COMMON block (identical across
all controllers) followed by a SPECIFIC block (local hardware only):

```
╔══ <CTRL> — COMMON COMMANDS ═══════════════════════════════╗
║  <command list — same on all controllers>                  ║
╠══ <CTRL> — SPECIFIC COMMANDS ═════════════════════════════╣
║  <controller-specific hardware commands>                   ║
╚═══════════════════════════════════════════════════════════╝
```

#### COMMON Commands (All Controllers)

These commands appear in the COMMON block of every controller's HELP output with identical
syntax and behavior. When adding a new common command, add it to all four controllers.

| Command | Description |
|---------|-------------|
| `INFO` | Build info, IP, link, firmware version |
| `REG` | Full REG1 register dump (all fields) |
| `STATUS` | System state/mode + status bits decoded |
| `TEMPS` | Temperature sensors |
| `TIME` | Active time source + PTP/NTP status |
| `TIMESRC <PTP\|NTP\|AUTO\|OFF>` | Set time source policy |
| `PTPDEBUG <0-3>` | Set PTP debug level (0=OFF 1=MIN 2=NORM 3=VERBOSE) |
| `PTPDIAG ON\|OFF` | Suppress DELAY_REQ — W5500 SPI contention testing (FW-B3) |
| `A1 ON\|OFF` | Enable/disable A1 TX stream (firmware only, no network gate) |
| `NTP` | NTP sync status + server + epoch time |
| `NTPIP <a.b.c.d>` | Set primary NTP server IP + force resync |
| `NTPFB <a.b.c.d>` | Set fallback NTP server (OFF to clear) |
| `NTPSYNC` | Force immediate NTP resync |
| `DEBUG <0-3>` | Set controller debug level |
| `STATE <n>` | Set system state (0=OFF 1=STNDBY 2=ISR 3=COMBAT 4=MAINT 5=FAULT) |
| `MODE <n>` | Set gimbal mode (0=OFF 1=POS 2=RATE 3=CUE 4=ATRACK 5=FTRACK) |

#### SPECIFIC Commands (Per Controller)

| Controller | Specific Commands |
|------------|------------------|
| TMC | FLOWS, LCM, VICOR, TEMP, FAN, VICOR \<ch\>, LCM \<ch\>, DAC, PUMP |
| BDC | REINIT, ENABLE, FMC, TRC, MCC, RELAY, VICOR |
| MCC | REINIT, ENABLE, TMC, SOL, RELAY, VICOR, CHARGER, CHARLEVEL, HEL, HELCLR, FAN, TARGETTEMP |
| FMC | FSM, FSMPOS, FSMPOW, STAGE, STAGEPOS, STAGECAL, STAGEEN, SCAN |

#### TIME Command Output (All Controllers)

```
TIME  active source : PTP|NTP|NONE
------------------------------------------------
PTP   enabled       : YES|NO
PTP   synched       : YES|NO
PTP   misses        : <n>
PTP   offset_us     : <n>
PTP   lastSync      : <n> ms ago
PTP   time          : <date/time> | [not synced]
------------------------------------------------
NTP   enabled       : YES|NO
NTP   synched       : YES|NO
NTP   misses        : <n> / <NTP_STALE_MISSES>
NTP   offset_us     : <n>
NTP   usingFallback : YES|no
NTP   lastSync      : <n> ms ago
NTP   time          : <date/time> | [not synced]
------------------------------------------------
  <register bytes>
```

**FMC exception:** `PrintTime()` calls `Serial` not `SerialUSB` — cannot call on SAMD21.
FMC prints `[see PTPDEBUG]` when synced, `[not synced]` when not synced.

#### A1 TX Control

All four controllers have `bool isA1Enabled = true` in their `.hpp` file. The flag is:
- Firmware-only — no network command may change it
- Serial only: `A1 ON` / `A1 OFF`
- Default `true` — A1 streams from boot
- `SEND_FIRE_STATUS()` (MCC) and `SEND_FIRE_STATUS_TO_TRC()` (BDC) gated on this flag

#### FMC SerialUSB Constraint

FMC (SAMD21) uses `SerialUSB` for all debug output. `Serial` goes to the hardware UART
which is not connected. Key rules:
- All debug output: `SerialUSB.println()` or `uprintf()` (formatted helper)
- Never call `ptp.PrintTime()` or `ntp.PrintTime()` — both use `Serial` internally
- `ptp.INIT()` and `ntp.INIT()` use `Serial` internally — gated on `isPTP_Enabled` /
  `isNTP_Enabled` in `fmc.cpp INIT()` so they don't fire unless the source is enabled

---

## 7. Data Flows

### 7.1 Video

```
TRC (Jetson Orin NX, .22)
  └── GStreamer: nvv4l2h264enc → rtph264pay → udpsink
        └── UDP port 5000 → THEIA (.208)
              └── GStreamer: udpsrc port=5000
                    → application/x-rtp,encoding-name=H264,payload=96
                    → rtpjitterbuffer(latency=0)
                    → rtph264depay → h264parse
                    → nvh264dec (HW) / avdec_h264 (SW fallback)
                    → videoconvert → video/x-raw,format=BGR
                    → GStreamerPipeReader → EmguCV Mat → VideoPanel
```

- Resolution: **1280×720 fixed**
- Framerate: **60 fps** (30 fps option via `0xD2` — action item pending)
- Transport: **unicast** TRC→THEIA (multicast via `0xD1` — action item pending)
- BDC is **not** in the video path
- `PixelShift = -420` horizontal correction applied in `GStreamerPipeReader.cs`
- GStreamer install path (Windows): `C:\gstreamer\1.0\msvc_x86_64\`
- Hardware decoder: `nvh264dec` (NVIDIA GTX 900+ / driver 452.39+)
- Software fallback: `avdec_h264` (~10–15% CPU at 720p/30fps)

### 7.2 Commands (THEIA → Subsystems)

```
THEIA (.208)
  └── A3 / UDP port 10050 (external, magic 0xCB 0x58)
        ├── → MCC (.10) — system state, laser, power, GNSS
        └── → BDC (.20) — gimbal, camera, FSM, PID, fire control votes
              ├── BDC routes → TRC (.22) via A2 / port 10018
              ├── BDC routes → FMC (.23) via port 10023
              └── BDC routes → Gimbal (.21) via Galil ASCII port 7777 (CMD TX)
                                              Galil data/status RX port 7778
```

THEIA does NOT communicate directly with TMC, FMC, or TRC.
TRC ASCII engineering commands (port 5012) are ENG GUI only — not used in production THEIA.

### 7.3 Telemetry (Unsolicited → THEIA)

```
─── A1 Port 10019 — Sub-controller → Upper-level ──────────────────────────
TMC  (.12) → MCC  (.10):10019  TMC REG1 64B @ 100 Hz
FMC  (.23) → BDC  (.20):10019  FMC REG1 64B @ 50 Hz
TRC (.22) → BDC  (.20):10019  TRC REG1 64B @ 100 Hz
MCC  (.10) → BDC  (.20):10019  Fire control vote 0xAB @ 100 Hz

─── A3 Port 10050 — Controllers → THEIA ───────────────────────────────────
MCC (.10):10050 → THEIA (.208)   MCC REG1 512B @ 100 Hz  (unsolicited, A3 registered)
BDC (.20):10050 → THEIA (.208)   BDC REG1 512B @ 100 Hz  (unsolicited, A3 registered)
  BDC REG1 includes embedded sub-registers:
    [20–58]   Gimbal block (MSG_GIMBAL)
    [60–123]  TRC REG1 64B pass-through (MSG_TRC)
    [169–232] FMC REG1 64B pass-through (MSG_FMC)
```

THEIA receives TMC data embedded in MCC REG1 ([66–129]) and FMC/TRC data embedded in
BDC REG1. THEIA never directly requests TMC or FMC telemetry.

### 7.4 External Cueing — CUE Source → THEIA → BDC

#### CUE Input Source

THEIA receives cueing via its `RADAR` class instance on UDP port 15009 (EXT_OPS). Any
conforming source that produces valid EXT_OPS framed 71-byte packets can serve as a CUE
source. HYPERION is the CROSSBOW reference implementation. CUE SIM is the IPG test and
simulation tool. Third-party integrators may supply their own cueing system provided they
conform to the packet format defined in `CROSSBOW_ICD_EXT_OPS` (IPGD-0005).

#### Software Packages Summary

Three separate Windows C# applications form the CROSSBOW operational software stack:

| Application | Namespace | Role | Connects To |
|-------------|-----------|------|-------------|
| **HYPERION** | `Hyperion` | Reference CUE source — sensor fusion, track management, operator track selection, EXT_OPS CUE output | External sensors (15001/15002); THEIA via UDP:15009 |
| **THEIA** | `CROSSBOW` | Operator HMI, fire control, gimbal/FSM control, video display | MCC + BDC via A3; any conforming CUE source via UDP:15009 |
| **CUE SIM** | `CROSSBOW_EMPLACEMENT_GUIS` | EXT_OPS test tool — simulated track injection, HYPERION sniffer, direct THEIA verification | HYPERION via UDP:15001; THEIA direct via UDP:15009 |
| **TRC_ENG_GUI_PRESERVE** | `CROSSBOW` | Engineering GUI — all 5 controllers | All controllers via A2 |

HYPERION and THEIA can run on the same PC but are typically on separate machines in
deployment. TRC_ENG_GUI_PRESERVE is engineering-only and not present in the operational configuration.

#### HYPERION Architecture

HYPERION ingests tracks from up to four independent sensor sources simultaneously, normalises
them into a common data model, applies a 6-state NED Kalman filter per track, and displays all
live tracks on a GMap.NET map canvas with a DataGridView. The operator selects the desired
track, and HYPERION transmits it as a CUE packet to THEIA.

```
External Sensors                    HYPERION (namespace Hyperion, .NET / WinForms)
─────────────────                   ────────────────────────────────────────────────
ADS-B (1090 MHz SDR)
  dump1090 TCP:30002 ─────────────► ADSB2 class
                                    (Mode S DF=17 frames, CPR decode, TC 1–22)
                                          │
Echodyne ECHO radar
  TCP:29982 ──────────────────────► ECHO class
                                    (728B binary, ECEF→LLA, UUID track ID)
                                          │
Generic RADAR / EXT / CUE SIM
  UDP:15001 ──────────────────────► RADAR class (aRADAR, "RADAR" prefix)
                                          │
LoRa / MAVLink relay
  UDP:15002 ──────────────────────► RADAR class (aLORA, "LORA" prefix)
                                    (MAVLink NED vz sign corrected to ENU)
                                          │
Stellarium (celestial ref)
  HTTP:8090 ──────────────────────► STELLARIUM class
                                    (az/el → synthetic LLA via ned2lla → trackLogs["STELLA"])
                                          │
                                          ▼
                              ConcurrentDictionary<string, trackLOG>
                              trackLOG per ICAO key:
                                ├── PositionLog      SortedList<ms, ptLLA>
                                ├── HeadingSpeedLog  SortedList<ms, HeadingSpeed>
                                └── KALMAN (6-state NED linear KF)
                                      State: [N, E, D, vN, vE, vD]
                                      Mode:  KALMAN_PREDICTED (default)
                                          │
                              DataGridView + GMap.NET display
                                          │
                              Operator selects track → CurrentCUE
                                          │
                              timUDP → BuildCueFrame() → 71B EXT_OPS framed
                                          │
                                          ▼
                              UDP unicast → THEIA:192.168.1.208:15009 (default)
```

#### Sensor Input Reference

| Instance | Class | Protocol | Transport | Port | Track Key |
|----------|-------|----------|-----------|------|-----------|
| `aADSB` | `ADSB2` | Mode S 1090ES hex | TCP | 30002 | ICAO 24-bit hex (6 chars) |
| `aECHO` | `ECHO` | Echodyne binary | TCP | 29982 | `ECH_<last4 UUID hex>` |
| `aRADAR` | `RADAR` | EXT_OPS framed UDP | UDP | 15001 | `"RADAR"` prefix |
| `aLORA` | `RADAR` | EXT_OPS framed UDP (LoRa) | UDP | 15002 | `"LORA"` prefix |
| `aStella` | `STELLARIUM` | JSON REST | HTTP | 8090 | `"STELLA"` — synthetic LLA via ned2lla |

All altitude values normalised to WGS-84 HAE before entering `lla2ned()`. LoRa vz sign
corrected (MAVLink NED: positive=down) to ENU (positive=up) before Kalman update.

#### Kalman Filter

6-state linear constant-velocity filter in local NED frame centred on `BaseStation`. State
vector `[N, E, D, vN, vE, vD]`. H = I₆ (all 6 states directly observed). `KALMAN_PREDICTED`
mode propagates the last filter state to `DateTime.UtcNow`, compensating ~125–250 m of
display lag at 1 Hz ADS-B update rate and 500 ms UI timer.

| Parameter | Value | Notes |
|-----------|-------|-------|
| R_pos | 25.0 (σ = 5 m) | CPR/RADAR position noise |
| R_vel | 4.0 (σ = 2 m/s) | heading+speed decomposition noise |
| σ_a² | 4.5 (m/s²)² | process noise — increase to 25–100 for UAV manoeuvres |
| dt | measurement timestamp delta | actual packet timestamps, not wall-clock |
| Thread safety | `_stateLock` | guards `_XX` + `_lastUpdateTime` across UI/sensor threads |

#### CUE Packet Format — CMD `0xAA` (71 bytes total, EXT_OPS framed)

HYPERION transmits the selected track as a 71-byte EXT_OPS framed UDP packet to THEIA at
`192.168.1.208:15009` (default — operator-configurable). THEIA receives and validates via
the `RADAR` class (`CueReceiver` path, shared CROSSBOW library).
Authoritative definition: `CROSSBOW_ICD_EXT_OPS` (IPGD-0005).

**EXT_OPS frame wrapper (7-byte header + 2-byte CRC):**

```
[0]     Magic HI  = 0xCB
[1]     Magic LO  = 0x48
[2]     CMD_BYTE  = 0xAA
[3–4]   SEQ_NUM   uint16 LE
[5–6]   PAYLOAD_LEN = 62 (uint16 LE)
[7–68]  PAYLOAD   62 bytes (CUE payload — see below)
[69–70] CRC-16/CCITT uint16 LE — over bytes [0–68]
```

**CUE payload (62 bytes, payload offsets):**

| Payload Offset | Size | Type | Field | Notes |
|----------------|------|------|-------|-------|
| 0 | 8 | int64 | ms Time Stamp | ms since Unix epoch |
| 8 | 8 | uint8[8] | Track ID | ASCII, null-padded |
| 16 | 1 | byte | Track Class | 8=UAV, 10=AC_LIGHT, etc. — see CROSSBOW_ICD_INT_OPS |
| 17 | 1 | byte | Track CMD | 0=DROP, 1=TRACK, 4=HOLD, 5=FREE, 254=CONT ON, 255=CONT OFF |
| 18 | 8 | double | Latitude | WGS-84 degrees, North positive |
| 26 | 8 | double | Longitude | WGS-84 degrees, East positive |
| 34 | 4 | float | Altitude HAE | Metres — HAE only, do NOT use MSL |
| 38 | 4 | float | Heading | True heading degrees (0–360, North=0) |
| 42 | 4 | float | Speed | Ground speed m/s |
| 46 | 4 | float | Vz | Vertical speed m/s, positive=climbing |
| 50 | 12 | uint32[3] | RESERVED | 0x00 |

> **Heading/Speed note (v3.0.2 change):** `vx`/`vy` NED fields replaced by `Heading`/`Speed`
> in ICD v3.1.0. HYPERION converts heading+speed to NED components internally for the Kalman
> filter. THEIA uses heading for AC display overlay only — pointing uses lat/lon/alt.

#### Full Operator Engagement Sequence

```
1. HYPERION — sensor fusion display running, tracks updating
      │
2. HYPERION operator selects target track in DataGridView
   Enables jtoggle_CROSSBOW → EXT_OPS framed CUE packets (CMD 0xAA, 71B)
   flow to THEIA:15009 at timUDP rate
      │
3. THEIA receives CUE via RADAR class (shared CROSSBOW library, UDP:15009)
   Frame validated (magic 0xCB 0x48, CRC, 71B). CUE bearing/elevation
   computed from Kalman-predicted LLA + platform BaseStation LLA
      │
4. THEIA operator accepts CUE (Xbox A button → toggle CUE_FLAG)
   BG_CUE_TASK @ 50 Hz → 0xB8 SET_PID_TARGET (NED az/el) → BDC via A3:10050
      │
5. BDC enters CUE mode → gimbal PID drives LOS toward target
      │
6. THEIA operator advances mode (right trigger) → AT mode
   TRC MOSSE tracker locks on target in video frame
   Dual-loop: gimbal (slow) + FSM (fast) close on tracker tx/ty
      │
7. Fire control (if authorised):
   Left + Right trigger → 0xE6 fire vote → MCC → vote aggregation → laser
```

Xbox controller is the THEIA operator's primary input at steps 4–7. HYPERION and THEIA
may be operated by one person or two depending on the engagement scenario.

---

## 8. TRC Internal Architecture

### 8.1 Pipeline

```
AlviumCamera (VIS, 60 Hz) / MWIRCamera (MWIR, 30 Hz)
  └── CameraBase (abstract)
        └── Lock-free triple buffer (FrameSlot)
              └── Compositor (60 Hz)
                    ├── Overlay rendering (reticle, track box, HUD, OSD, chevrons)
                    ├── ViewMode: CAM1 | CAM2 | PIP4 | PIP8
                    └── nvv4l2h264enc → rtph264pay → udpsink port 5000
```

### 8.2 Thread Architecture

| Thread | Source | Rate | Purpose |
|--------|--------|------|---------|
| capture (Alvium) | AlviumCamera | 60 Hz | Frame grab, tracker |
| capture (MWIR) | MwirCamera | 30 Hz | Frame grab |
| compositor | Compositor | 60 Hz | Overlay render, encode push |
| A1 TX | TrcA1::txThreadFunc | 100 Hz | Telemetry → BDC |
| A1 RX | TrcA1::rxThreadFunc | blocking | Fire control status ← BDC |
| A2 binary | UdpListener::binaryThreadFunc | blocking | Command receive |
| A2 unsolicited | UdpListener::a2UnsolThreadFunc | 100 Hz | Telemetry → A2 clients |
| ASCII | UdpListener::asciiThreadFunc | blocking | ASCII command receive |
| stats | statsThreadFunc | 1 Hz | Jetson temp/CPU load |

### 8.3 Tracker Architecture

```
CameraBase
  └── TrackerWrapper (MOSSE = TrackB)
        ├── TrackA: AI/DNN — not yet implemented
        ├── TrackB: MOSSE — implemented, primary operational tracker
        ├── TrackC: Centroid — not yet implemented
        └── Kalman: not yet implemented
```

Tracker enable/disable per-ID via `0xDB ORIN_ACAM_ENABLE_TRACKERS`.

### 8.4 TRC REG1 Telemetry Packet (64 bytes, #pragma pack(push,1))

| Offset | Size | Type | Field | Notes |
|--------|------|------|-------|-------|
| 0 | 1 | uint8 | cmd_byte | 0xA1 always |
| 1 | 4 | uint32 | version_word | VERSION_PACK(3,0,1) |
| 5 | 1 | uint8 | systemState | SYSTEM_STATES enum |
| 6 | 1 | uint8 | systemMode | BDC_MODES enum |
| 7 | 2 | uint16 | HB_ms | ms between sends |
| 9 | 2 | uint16 | dt_us | Frame processing time µs |
| 11 | 1 | uint8 | overlayMask | HUD overlay bitmask (0xD3) |
| 12 | 2 | uint16 | fps | Framerate × 100 |
| 14 | 2 | int16 | deviceTemperature | VIS camera sensor temp °C |
| 16 | 1 | uint8 | camid | VIS=0, MWIR=1 |
| 17 | 1 | uint8 | status_cam0 | Alvium CamStatus bitmask |
| 18 | 1 | uint8 | status_track_cam0 | Alvium tracker state |
| 19 | 1 | uint8 | status_cam1 | MWIR CamStatus bitmask |
| 20 | 1 | uint8 | status_track_cam1 | MWIR tracker state |
| 21 | 2 | int16 | tx | Tracker centre X (AT-offset adjusted) |
| 23 | 2 | int16 | ty | Tracker centre Y |
| 25 | 1 | int8 | atX0 | AT offset X |
| 26 | 1 | int8 | atY0 | AT offset Y |
| 27 | 1 | int8 | ftX0 | FT offset X |
| 28 | 1 | int8 | ftY0 | FT offset Y |
| 29 | 4 | float | focusScore | Laplacian variance |
| 33 | 8 | int64 | ntpEpochTime | ms since Unix epoch |
| 41 | 1 | uint8 | voteBitsMcc | MCC fire control vote bits (relay from 0xAB) |
| 42 | 1 | uint8 | voteBitsBdc | BDC geometry vote bits (relay from 0xAB) |
| 43 | 2 | int16 | nccScore | NCC quality × 10000 |
| 45 | 2 | int16 | jetsonTemp | Jetson CPU temp °C |
| 47 | 2 | int16 | jetsonCpuLoad | Jetson CPU load % |
| 49 | 15 | uint8[] | RESERVED | 0x00 |

**BDC embedding:** TRC REG1 occupies bytes [60–123] of BDC REG1 payload (64-byte fixed block).

### 8.5 CamStatus / TrackStatus Bits

**CamStatusBits** (status_cam0 / status_cam1):

| Bit | Mask | Name |
|-----|------|------|
| 0 | 0x01 | STARTED |
| 1 | 0x02 | ACTIVE |
| 2 | 0x04 | CAPTURING |
| 3 | 0x08 | TRACKING |
| 4 | 0x10 | TRACK_VALID |
| 5 | 0x20 | FOCUS_SCORE_ENABLED |
| 6 | 0x40 | OSD_ENABLED |
| 7 | 0x80 | CUE_FLAG |

**TrackStatusBits** (status_track_camN):

| Bit | Field |
|-----|-------|
| 0-1 | TrackA (AI/DNN) — not implemented |
| 2 | TrackB_Enabled (MOSSE) |
| 3 | TrackB_Valid |
| 4 | TrackB_Init |
| 5-6 | TrackC — not implemented |
| 7 | Kalman — not implemented |

### 8.6 Startup

```bash
./multi_streamer --dest-host 192.168.1.208
```

`--dest-host` sets the video stream destination (port 5000, H.264 RTP). Telemetry auto-targets
BDC (`192.168.1.20`) at boot regardless of `--dest-host`. If omitted, video does not stream.

---

## 9. MCC Internal Architecture

MCC runs on Arduino/STM32F7, FW v3.3.0, IP: 192.168.1.10.

Hardware revision is selected at compile time in `hw_rev.hpp` (`HW_REV_V1` or `HW_REV_V2`). The active revision is reported in REG1 byte [254] (`HW_REV`) so `MSG_MCC.cs` can self-detect the register layout. Read byte [254] before interpreting `HEALTH_BITS` [9] and `POWER_BITS` [10].

### 9.1 Role

Master Control Controller — manages all power and energy subsystems:
- Battery (BAT) — pack voltage, current, state of charge
- Laser power supply (HEL/IPG) — power bus enable, status, fire vote
- Charger (CRG/DBU) — charge control (V1: I2C + GPIO; V2: GPIO only)
- GNSS (NovAtel) — position, heading, INS solution
- TMC supervision — receives TMC REG1 via A1 and embeds it in MCC REG1
- PTP client (primary) — syncs to NovAtel GNSS grandmaster (.30) via IEEE 1588
- NTP client (fallback) — syncs to NTP (.33) with automatic fallback to .208
- Fire control — aggregates all vote bits, issues 0xAB to BDC at 50 Hz

**Hardware variants:**

| Subsystem | V1 | V2 |
|-----------|----|----|\n| Solenoids | SOL_HEL (pin 5) + SOL_BDA (pin 8) — laser HV bus + gimbal power | **Retired** — hardware removed |
| Relay bus | Single Vicor (A0, LOW=ON) → relay bank | **Repurposed** → `GIM_VICOR` (A0, LOW=ON) — 300V→48V Gimbal Vicor |
| TMS Vicor | None (`MCC_RELAYS::TMS` was pinless) | Pin 83 = `TMS_VICOR` — NC opto → TMS Vicor power bank (HIGH=ON) |
| GPS relay | Pin 83 (NO opto) → GNSS power | **Retired** — GNSS always powered at boot |
| Laser relay | Pin 20 (NO opto) → laser digital bus | Pin 20 (NO opto) → laser enable (swapped from pin 83) |
| Charger enable | GPIO pin 6 | GPIO pin 82 (was CHARGER_MODE on V1) |
| Charger I2C | DBU3200 — CC/CV control, status | **Retired** — new charger GPIO only |
| `isBDC_Ready` source | Set by SOL_BDA on in StateManager | Set by `EnablePower(GIM_VICOR)` in StateManager |

### 9.2 Subsystem Embedding

MCC REG1 (256-byte payload) embeds:

| Bytes | Sub-register | Class |
|-------|--------------|-------|
| [34–44] | Battery REG1 (11 bytes) | MSG_BATTERY |
| [45–65] | IPG/Laser REG1 (21 bytes) | MSG_IPG |
| [66–129] | TMC REG1 pass-through (64 bytes) | MSG_TMC |
| [135–212] | GNSS block (78 bytes) | MSG_GNSS |
| [213–244] | Charger block (32 bytes) | MSG_CMC |

### 9.3 A1 TX → BDC

MCC sends REG1 and fire control vote (0xAB) to BDC via A1 at 50 Hz and 100 Hz respectively.
`SEND_FIRE_STATUS` gate: `isPwr_LaserRelay && isBDC_Ready` (both revisions). Replaces V1's `isSolenoid2_Enabled` gate — laser power is the correct semantic gate on both hardware variants.

### 9.4 Vote Aggregation

```
MCC aggregates fire control votes:
  HORIZON vote   (from BDC geometry)
  KIZ vote       (from BDC KIZ engine)
  LCH vote       (from BDC LCH engine)
  BDA vote       (LOS clear)
  ARMED vote     (operator armed)
  notAbort vote  (no abort condition — inverted, safe-by-default)
  EMON           (energy monitor)
  → 0xAB SET_BCAST_FIRECONTROL_STATUS → BDC @ 100 Hz
```

### 9.5 Time Source Architecture

MCC maintains two concurrent time sources with automatic priority routing.

**Source hierarchy:**

| Priority | Source | Server | Socket | Accuracy |
|----------|--------|--------|--------|----------|
| 1 | PTP (IEEE 1588) | NovAtel GNSS `.30` | udpEvent:319 + udpGeneral:320 | ~1–100 µs |
| 2 | NTP primary | `.33` HW Stratum 1 | udpA2 (shared) | ~1–10 ms |
| 3 | NTP fallback | `.208` Windows HMI | udpA2 (shared) | ~1–10 ms |

**`GetCurrentTime()` routing (`mcc.hpp`) — session 35 holdover:**
```
EPOCH_MIN_VALID_US = 1577836800000000ULL  (2020-01-01 UTC)

if isPTP_Enabled:
    t = ptp.GetCurrentTime()
    if t >= EPOCH_MIN_VALID_US:
        activeTimeSource = ptp.isSynched ? PTP : NONE
        latch _lastGoodTimeUs = t, _lastGoodStampUs = micros()
        return t

if isNTP_Enabled:
    t = ntp.GetCurrentTime()
    if t >= EPOCH_MIN_VALID_US:
        activeTimeSource = ntp.isSynched ? NTP : NONE
        latch _lastGoodTimeUs = t, _lastGoodStampUs = micros()
        return t

// Holdover — both sources invalid or below epoch floor
activeTimeSource = NONE
if _lastGoodTimeUs > 0:
    return _lastGoodTimeUs + (micros() - _lastGoodStampUs)
return 0
```
`isPTP_Enabled` defaults to `false` (FW-B3 deferred). Enable via serial `TIMESRC PTP`.

**NTP suppression** (`ntpSuppressedByPTP = true`, default):
While `ptp.isSynched`, NTP polling is gated off. Gate re-opens immediately when PTP becomes stale. Use `TIMESRC AUTO` to run both concurrently.

**Fallback timing:**
- PTP stale detection: `PTP_STALE_MISSES = 5` × `PTP_MISS_CHECK_MS = 2s` → ~10 s
- NTP first send after PTP clears: up to `NTP_TICK_MS = 10 s`
- Worst-case gap: ~20 s (PTP lost → NTP synched)

**`ptpClient` class** (`ptpClient.hpp` / `ptpClient.cpp`):
- Implements IEEE 1588 ordinary clock slave (2-step, E2E delay, multicast `224.0.1.129`)
- State machine: `WAIT_SYNC → WAIT_FOLLOW_UP → WAIT_DELAY_RESP → WAIT_SYNC`
- `firstSync`: `setEpoch(t1)` — hard-set to master send time (avoids epoch mismatch)
- Subsequent syncs: EMA of `offset_us`; `setEpoch(rawTime() - offset)`
- Debug: `ptp.setDebugLevel(DEBUG_LEVELS::MIN)` enables offset/delay streaming (default OFF)

**NovAtel PTP configuration** (one-time, saved to NVM):
```
PTPMODE ENABLE_FINETIME    ← PTP only when FINESTEERING — clean fallback if GPS lost
PTPTIMESCALE UTC_TIME      ← UNIX/UTC epoch — required for correct MCC clock
SAVECONFIG
```
Validated session 29: state=MASTER, `offset=0.000ns`, `Time Offsets Valid=TRUE`, `offset_us=12µs` on MCC.

**Serial commands:**

| Command | Description |
|---------|-------------|
| `TIME` | Full dual-source status: active source, PTP offset/time, NTP status, register bytes |
| `TIMESRC PTP` | PTP primary, NTP suppressed (default) |
| `TIMESRC NTP` | NTP only, PTP disabled |
| `TIMESRC AUTO` | Both concurrent — NTP stays warm |
| `PTPDEBUG <0-3>` | Set PTP debug level *(FW-1 — pending)* |

**Register bits** (session 28/29, updated session 32, updated MCC unification):

| Register | Byte | Bit | Field |
|----------|------|-----|-------|
| DEVICE_ENABLED | 7 | 4 | `isPTP_Enabled` |
| DEVICE_READY | 8 | 4 | `isPTP_Ready` (`ptp.isSynched`) |
| HEALTH_BITS | 9 | 0 | `isReady` |
| HEALTH_BITS | 9 | 1 | `isChargerEnabled` |
| HEALTH_BITS | 9 | 2 | `isNotBatLowVoltage` |
| POWER_BITS | 10 | N | `isPwr_<X>` where N = `MCC_POWER` enum value — see ICD v3.4.0 |
| TIME_BITS | 253 | 0 | `isPTP_Enabled` |
| TIME_BITS | 253 | 1 | `ptp.isSynched` |
| TIME_BITS | 253 | 2 | `usingPTP` (active time source is PTP) |
| TIME_BITS | 253 | 3 | `ntp.isSynched` |
| TIME_BITS | 253 | 4 | `ntpUsingFallback` |
| TIME_BITS | 253 | 5 | `ntpHasFallback` |
| HW_REV | 254 | — | `0x01`=V1, `0x02`=V2 — read before interpreting HEALTH_BITS and POWER_BITS |

`TIME_BITS` layout is identical across MCC (byte 253), BDC (byte 391), and TMC (`STATUS_BITS3` byte 61) — single decode path for all controllers.

### 9.6 Build Configuration (`hw_rev.hpp`)

| Define | Effect |
|--------|--------|
| `HW_REV_V1` | V1 hardware — relay bus Vicor, solenoids, GPS relay, charger I2C |
| `HW_REV_V2` | V2 hardware — dual Vicor (GIM+TMS), no solenoids, no GPS relay, GPIO-only charger |
| `MCC_HW_REV_BYTE` | Auto-set — `0x01` (V1) or `0x02` (V2); written to REG1 byte [254] |
| `PIN_PWR_*` / `POL_PWR_*_ON/OFF` | Per-revision pin and polarity macros for all 7 power outputs |
| `POL_PWR_GIM_ON = LOW` | GIM_VICOR inverted drive — ⚠️ analytically derived, verify on V2 bring-up |
| `POL_PWR_TMS_ON = HIGH` | TMS_VICOR NC opto — ⚠️ analytically derived, verify on V2 bring-up |

`EnablePower(MCC_POWER, bool)` is the sole function that calls `digitalWrite` on power output pins. All seven `MCC_POWER` outputs (GPS_RELAY, VICOR_BUS, LASER_RELAY, GIM_VICOR, TMS_VICOR, SOL_HEL, SOL_BDA) are dispatched through a single switch in `EnablePower()`. `EnableRelay()`, `EnableVicor()`, and `EnableSol()` wrappers were removed — all call sites use `EnablePower()` directly.

---

## 10. BDC Internal Architecture

BDC is the system integration hub. Runs on STM32F7, FW v3.0.1, IP: 192.168.1.20.

### 10.1 Boot Sequence

Non-blocking state machine (~26s total before UDP_READ runs):
```
POWER_SETTLE(10s) → VICOR_ON(1s) → RELAYS_ON(1s) → GIMBAL_INIT(1s)
→ TRC_INIT(2s) → FMC_INIT(2s) → NTP_INIT(2s) → PTP_INIT(1s) → FUJI_WAIT(5s) → DONE(0.5s)
```
`PTP_INIT` added session 32 — calls `ptp.INIT(IP_GNSS_BYTES)` after network has settled.
`FUJI_WAIT` added session 28 — advances when `fuji.isConnected` or after 5s timeout. Non-blocking. Prints `BOOT: FUJI READY` or `BOOT: FUJI timeout`. Note: `fuji.SETUP()` deferred to post-boot via `pendingRelaySetup` flag — `fuji.isConnected` is always false at this step, so FUJI_WAIT always times out at 5s regardless of physical connection (FW-C3 open).
`DONE` reduced from 1s to 0.5s — Fuji now has dedicated wait step. Completion print: `BOOT: complete  gimbal=RDY|---  trc=RDY|---  fmc=RDY|---  fuji=RDY|---  ntp=RDY|---`

### 10.2 Subsystem Drivers

| Driver | Transport | Rate | Notes |
|--------|-----------|------|-------|
| Gimbal (Galil) | Ethernet ASCII UDP | ~125 Hz RX / cmd TX | CMD port 7777 (`clientCmd`), data port 7778 (`clientData`) |
| TRC | A1 / 10019 RX | 100 Hz | 521-byte framed REG1 |
| TRC cmd | A2 / 10018 TX | on-demand | Framed requests via trc.EXEC_UDP() |
| FMC | A1 / 10019 RX | 50 Hz | 521-byte framed REG1 |
| MCC | A1 / 10019 RX | 50 Hz | 521-byte framed REG1 |
| Fuji lens | C10 serial | on-demand | Zoom, focus |
| MWIR camera | Serial | on-demand | NUC, AF, polarity |
| Inclinometer | Serial | ~10 Hz | Platform level |
| NTP client | Ethernet | gated — see 10.4 | Shared udpA2; suppressed while PTP synched |
| PTP client | Ethernet multicast | 1 Hz sync | IEEE 1588; GNSS master .30; primary time source |
| PALOS fire control | Internal | per-vote-cycle | KIZ, LCH, horizon validation |

### 10.3 BDC W5500 Socket Budget

W5500 has 8 hardware sockets. BDC allocates 7/8 — one spare remaining.

| # | Owner | Port | Type | Notes |
|---|-------|------|------|-------|
| 1 | BDC `udpA1` | 10019 | unicast | FMC/TRC/MCC RX + fire control TX to TRC |
| 2 | BDC `udpA2` | 10018 | unicast | A2 eng RX+TX; NTP intercept; TRC cmd TX (shared); FMC cmd TX (shared) |
| 3 | BDC `udpA3` | 10050 | unicast | A3 external RX+TX |
| 4 | GIMBAL `clientCmd` | 7777 | unicast | Galil cmd TX (`EthernetUDP`) |
| 5 | GIMBAL `clientData` | 7778 | unicast | Galil data RX+TX (`EthernetUDP`) |
| 6 | PTP `udpEvent` | 319 | multicast | PTP SYNC RX + DELAY_REQ TX |
| 7 | PTP `udpGeneral` | 320 | multicast | PTP FOLLOW_UP + DELAY_RESP RX |

TRC and FMC command TX borrows `udpA2` via pointer — TX-only, single-threaded, no conflict. Previously each opened their own socket (TRC on 10017, FMC on 10018), consuming 9 sockets total and preventing PTP from initialising. Fuji (serial), MWIR (serial), Inclinometer (serial), TPH (I2C) consume no W5500 sockets.

### 10.4 BDC Time Source Architecture

BDC mirrors MCC time source architecture (section 9.5) exactly. `isPTP_Enabled` defaults to `false` (FW-B3 deferred — W5500 `DELAY_REQ` contention when both BDC and FMC run PTP simultaneously). Enable via serial `TIMESRC PTP`.

**`GetCurrentTime()` routing (`bdc.hpp`) — session 35 holdover:**
```
Same EPOCH_MIN_VALID_US guard + holdover path as MCC (section 9.5).
isPTP_Enabled = false  (default — FW-B3 deferred)
```

**NTP suppression:** `ntpSuppressedByPTP = true` (default) — NTP polling gated while PTP synched.

**Serial commands:**

| Command | Description |
|---------|-------------|
| `TIME` | Full dual-source status: active source, PTP offset/time, NTP status, register bytes |
| `TIMESRC <PTP\|NTP\|AUTO>` | Set time source policy |
| `PTPDEBUG <0-3>` | Set PTP debug level |
| `REINIT 7` | Re-initialise PTP — mirrors `0xB0 SET_BDC_REINIT` device 7 |
| `ENABLE 7 <0\|1>` | Enable/disable PTP — mirrors `0xBE SET_BDC_DEVICES_ENABLE` device 7 |

**Register bits** (session 32):

| Register | Byte | Bit | Field |
|----------|------|-----|-------|
| DEVICE_ENABLED | 8 | 7 | `isPTP_Enabled` (`BDC_DEVICES::PTP`) |
| DEVICE_READY | 9 | 7 | `ptp.isSynched` |
| TIME_BITS | 391 | 0–5 | Same layout as MCC byte 253 / TMC STATUS_BITS3 byte 61 |

### 10.5 Liveness Flags

| Flag | Condition | Timeout |
|------|-----------|---------|
| `isTRC_A1_Alive` | A1 frame from .22 within 200ms | 200ms |
| `isFMC_A1_Alive` | A1 frame from .23 within 200ms | 200ms |
| `isMCC_A1_Alive` | A1 frame from .10 within 200ms | 200ms |
| `isJETSON_Ready()` | `trc.isConnected && isTRC_A1_Alive` | — |
| `isFSM_Ready()` | `fmc.isConnected && isFMC_A1_Alive` | — |

### 10.6 Mode State Machine

```
OFF ──► POS ──────────────────────────────► AT ──► FT (not yet impl.)
         │                                   ▲
         │ (if CUE_FLAG set)                 │
         └──────────► CUE ──────────────────►│
```

| Mode | Gimbal Drive | FSM Drive | TRC Tracker |
|------|-------------|-----------|--------------|
| OFF | Torque only | — | Off |
| POS | Right thumb → JG velocity | — | Off |
| RATE | Right thumb → JG acceleration | — | Off |
| CUE | BDC PID on cue NED az/el | — | Off |
| AT | BDC PID on tx+atX0/ty+atY0 (slow) | BDC PID on tx+atX0/ty+atY0 (fast) | TrackB ON |
| FT | Drives to AT lock | Operator FT offset | TrackB ON |

### 10.7 Dual-Loop Control (AT Mode)

```
TRC tx, ty
  ├── FSM loop (fast):   error = tx + atX0 / ty + atY0
  │     └── FMC: FSM_X + FSM_X0 / FSM_Y + FSM_Y0
  └── Gimbal loop (slow): error = tx + atX0 / ty + atY0
        └── Galil: JG velocity commands (port 7777)
```

---

## 11. TMC Internal Architecture

TMC runs on STM32F7 (OpenCR board library), FW v3.3.0, IP: 192.168.1.12.

Hardware revision is selected at compile time in `hw_rev.hpp` (`HW_REV_V1` or `HW_REV_V2`). The active revision is reported in REG1 byte [62] (`HW_REV`) so `MSG_TMC.cs` can self-detect the layout.

### 11.1 Role

Thermal Management Controller — maintains coolant temperature for the HEL thermal load.

| Subsystem | V1 | V2 |
|-----------|----|----|
| Coolant pumps | Single Vicor DC-DC, both pumps in parallel, DAC speed control | Two TRACO DC-DCs, one per pump, on/off only, independent control |
| LCM1 / LCM2 | DAC-controlled compressor speed (MCP47FEBXX I2C) | **Unchanged** |
| Vicor converters | 4 Vicors (LCM1, LCM2, Pump, Heater) — NO opto inhibit | LCM Vicors only (2) — NC opto inhibit |
| Heater | Present — Vicor supply + DAC control | **Removed** |
| External ADC | Two ADS1015 chips (8 aux temp channels) | **Removed** — essential temps on direct MCU analog |
| Input fans | Fan1 / Fan2 PWM speed control | **Unchanged** |
| TPH sensor | BME280 I2C (temp, pressure, humidity) | **Unchanged** |
| Flow sensors | f1 / f2 turbine meters, interrupt-driven | **Unchanged** |
| Opto type (PSU inhibit) | Normally Open (NO) | Normally Closed (NC) |
| Opto type (LCM enable) | Normally Open (NO) | **Unchanged — NO** |

### 11.2 TMC W5500 Socket Budget

W5500 has 8 hardware sockets. TMC allocates **4/8 always** — four sockets spare.

| # | Owner | Port | Type | Notes |
|---|-------|------|------|-------|
| 1 | TMC `udpA1` | 0 (ephemeral) | unicast | TX only — 100 Hz unsolicited stream to MCC |
| 2 | TMC `udpA2` | 10018 | unicast | A2 RX+TX — shared: NTP TX/RX (`&udpA2`) |
| 3 | PTP `udpEvent` | 319 | multicast | PTP SYNC RX — **always allocated at boot** |
| 4 | PTP `udpGeneral` | 320 | multicast | PTP DELAY_REQ/RESP — **always allocated at boot** |

Pump (GPIO/DAC), LCM1/2 (DAC+ADC), Vicors (GPIO), Fans (PWM), TPH (I2C), Flow sensors (interrupt) consume no W5500 sockets.

> **ptp.INIT() unconditional:** TMC calls `ptp.INIT(IP_GNSS)` at boot regardless of `isPTP_Enabled` — sockets 3/4 are always allocated. `isPTP_Enabled` gates `ptp.UPDATE()` only. This is the correct pattern — MCC and FMC will align to match (FW-B4).

### 11.3 Hardware

| Hardware | Interface | V1 | V2 |
|----------|-----------|----|----|
| Pump PSU | GPIO enable | `PIN_VICOR_PUMP` (83) — Vicor, DAC trim via `TMC_PUMP_SPEEDS` | `PIN_VICOR_PUMP1` (65), `PIN_VICOR_PUMP2` (46) — TRACO, on/off only |
| LCM1 / LCM2 | DAC (MCP47FEBXX 0x63) + ADC | Speed setting + current readback | **Unchanged** |
| Vicor LCM1 | GPIO enable | `PIN_VICOR_LCM1` (1) | **Unchanged** |
| Vicor LCM2 | GPIO enable | `PIN_VICOR_LCM2` (0) | **Unchanged** |
| Heater | GPIO enable + DAC | `PIN_VICOR_HEAT` (72) | **Removed** |
| Fan1 / Fan2 | PWM | Pins 5 / 9 | **Unchanged** |
| TPH sensor | I2C | BME280 | **Unchanged** |
| Flow sensors | Interrupt | Pins 7 / 8 | **Unchanged** |
| Aux temp ADC | I2C | Two ADS1015 chips (0x48, 0x49) | **Removed** |
| PSU inhibit opto | GPIO | Normally Open (CTRL_ON=LOW) | Normally Closed (CTRL_ON=HIGH) |
| LCM enable opto | GPIO | Normally Open (HIGH=ON) | **Unchanged** |

### 11.4 A1 TX → MCC

TMC streams REG1 (64 bytes) to MCC (.10) at 100 Hz via A1 port 10019. MCC embeds the
received buffer directly into MCC REG1 bytes [66–129] with no parsing at the MCC level —
raw pass-through. THEIA parses it as `MSG_TMC`.

### 11.5 Temperature Channels

| Field | Description | Source |
|-------|-------------|--------|
| `tt` | Target setpoint °C — range [10–40°C], enforced by firmware (clamp, no error) | Serial/ICD command |
| `ta1` | Air temp 1 °C | V1: ADS1015 ADC1 CH1 → V2: `PIN_TEMP_AIR1` (72) direct |
| `tf1` / `tf2` | Flow temp 1/2 °C | Direct MCU analog (both revisions) |
| `tc1` / `tc2` | Compressor temp 1/2 °C | V1: ADS1015 ADC1 CH3/CH4 → V2: `PIN_TEMP_COMP1/2` (29/30) direct |
| `to1` | Output channel 1 temp °C | V1: ADS1015 ADC2 CH1 → V2: `PIN_TEMP_OUT1` (42) direct |
| `to2` | Output channel 2 temp °C | Direct MCU analog (both revisions) |
| `tv1` / `tv2` | Vicor LCM1/2 temp °C | Direct MCU analog (both revisions) |
| `tv3` | Vicor heater temp °C | **V1 only** — ADS1015 ADC2 CH3; 0x00 on V2 |
| `tv4` | Vicor pump temp °C | **V1 only** — ADS1015 ADC2 CH4; 0x00 on V2 |

### 11.6 Build Configuration (`hw_rev.hpp`)

| Define | Effect |
|--------|--------|
| `HW_REV_V1` | V1 hardware — Vicor/ADS1015/heater/single pump |
| `HW_REV_V2` | V2 hardware — TRACO/direct analog/no heater/dual pump |
| `SINGLE_LOOP` | Optional (independent of HW_REV) — single coolant loopback; both PIDs track `tf2` |
| `CTRL_OFF` / `CTRL_ON` | Auto-set from HW_REV — Vicor/PSU inhibit line polarity |
| `TMC_HW_REV_BYTE` | Auto-set — `0x01` (V1) or `0x02` (V2); written to REG1 byte [62] |

---

## 12. FMC Internal Architecture

FMC runs on SAMD21 (Arduino), FW v3.2.0, IP: 192.168.1.23.

### 12.1 Hardware

| Hardware | Interface | Notes |
|----------|-----------|-------|
| AD5752R DAC | SPI | FSM X/Y drive voltage |
| LTC1867 ADC | SPI | FSM X/Y position readback (int32 counts) |
| M3-LS focus stage | I2C | Single axis, counts-based position |

### 12.2 FMC W5500 Socket Budget

W5500 has 8 hardware sockets. FMC allocates **2/8 with PTP disabled (current default)** or **4/8 with PTP enabled** — six sockets spare.

| # | Owner | Port | Type | Notes |
|---|-------|------|------|-------|
| 1 | FMC `udpA1` | 0 (ephemeral) | unicast | TX only — 50 Hz unsolicited stream to BDC |
| 2 | FMC `udpA2` | 10018 | unicast | A2 RX+TX — shared: NTP TX/RX (`&udpA2`) |
| 3 | PTP `udpEvent` | 319 | multicast | PTP SYNC RX — **only opened when `isPTP_Enabled=true`** |
| 4 | PTP `udpGeneral` | 320 | multicast | PTP DELAY_REQ/RESP — **only opened when `isPTP_Enabled=true`** |

DAC (SPI), ADC (SPI), stage (I2C) consume no W5500 sockets. NTP shares `udpA2`.

> ⚠️ **FW-B4 open:** FMC `ptp.INIT()` is gated by `if (isPTP_Enabled)` at boot — same issue as MCC. Fix: call `ptp.INIT()` unconditionally at boot. FMC has ample headroom: 2/8 → 4/8.

### 12.3 FMC Time Source Architecture

FMC mirrors MCC/BDC/TMC time source architecture. `isPTP_Enabled` and `isNTP_Enabled` both default to `false` (FW-B3 deferred; SAMD21 NTP timing open bug). Enable via serial `TIMESRC PTP` or `TIMESRC NTP`.

**`GetCurrentTime()` routing (`fmc.hpp`) — session 35 holdover:**
```
Same EPOCH_MIN_VALID_US guard + holdover path as MCC (section 9.5).
isPTP_Enabled  = false  (default — FW-B3 deferred)
isNTP_Enabled  = false  (default — SAMD21 NTP open bug)
```

**NTP suppression:** `ntpSuppressedByPTP = true` (default). INIT() guards NTP init behind `isNTP_Enabled` flag.

**Register:** `TIME_BITS` at FMC REG1 byte 44 — identical layout to MCC (253), BDC (391), TMC (61). FSM STAT BITS bits 2-3 vacated (were `ntp.isSynched`/`ntpUsingFallback`) — all time status now in TIME_BITS. Bit 7 (`isUnsolicitedModeEnabled`) retired session 35 — always 0.

### 12.4 Embedding

FMC REG1 (64 bytes) is embedded in BDC REG1 at bytes [169–232] as a raw pass-through.
BDC also separately sets FSM calibration fields directly into `fmcMSG` from BDC REG1 fields
[333–362] (iFOV, X0/Y0, signs, stage position).

> **FSM position note:** `FSM_X/Y` commanded (int16) at BDC REG1 [233–236] and `FSM Pos X/Y`
> ADC readback (int32) in FMC REG1 [20–27] are correct distinct types — int16 fits the DAC
> command range; int32 is the signed ADC readback with sign inversion. Not a bug. ✅ Closed #7.

---

## 13. THEIA (HMI) Architecture

### 13.1 Class Structure

```
frmMain (WinForms)
  └── CROSSBOW (application root)
        ├── MSG_MCC  — system state, fire control votes, NTP, GNSS (A3/10050)
        ├── MSG_BDC  — gimbal/tracker/FMC/MWIR commands, mode management (A3/10050)
        ├── ADSB2    — ADS-B receiver, track ingestion
        ├── RADAR    — radar/LoRa track ingestion
        ├── LCH / KIZ — laser control hour file parsing and validation
        ├── KALMAN   — 6-state NED Kalman filter per track
        ├── trackLogs — ConcurrentDictionary<ICAO, trackLOG>
        └── xInput   — Xbox controller (SharpDX, 50 Hz poll)
```

### 13.2 Xbox Controller Mapping

| Input | Normal | + Left Shoulder |
|-------|--------|-----------------|
| Right trigger (short press) | ADVANCE MODE | — |
| Right shoulder (short press) | REGRESS MODE | — |
| Left + Right trigger (simultaneous) | FIRE vote (heartbeat via `0xE6`) | — |
| Either trigger released | Cancel fire vote | — |
| Left thumbstick ↕↔ | Track gate size (W/H) | Track gate position (center) |
| Left hat click | Reset gate to 640×360, 100×100 | — |
| D-pad ↑ / ↓ | Zoom in / out | Cycle AI tracks ++ / -- |
| D-pad ← / → | Focus NEAR / FAR (coarse) | Focus NEAR / FAR (fine) |
| Right thumbstick | POS: gimbal vel / CUE: offset / AT: AIMPT / FT: offset | — |
| Right hat click | Zero active offset (context) | — |
| Back | VIS CAM | — |
| Start | MWIR CAM | — |
| A | Toggle CUE_FLAG | — |
| B | Toggle MWIR WHITE/BLACK HOT | — |
| X | Reset tracker to current gate (`0xDA`) | — |
| Y | Autofocus | — |

### 13.3 Fire Control Chain

```
Operator: Left + Right trigger simultaneously
  └── 0xE6 PMS_SET_FIRE_REQUESTED_VOTE {1} → MCC (heartbeat, continuous, via A3)
        └── MCC aggregates: HORIZON + KIZ + LCH + BDA + ARMED + notAbort votes
              └── BDCTotalVote() → fire authorized if all votes pass
                    └── 0xAB → BDC @ 100 Hz → TRC (reticle color)
```

---

## 14. Fire State HUD

TRC receives `0xAB SET_BCAST_FIRECONTROL_STATUS` on A1 port 10019 (raw 5-byte, no frame
wrapper) and stores vote bits in `state_.voteBitsMcc` / `state_.voteBitsBdc`. Compositor
renders reticle color and interlock messages every frame.

**`voteBitsMcc` layout:**

| Bit | Meaning |
|-----|---------|
| 1 | notAbort (0 = abort ACTIVE — inverted, safe-by-default) |
| 2 | armed |
| 3 | BDAVote (LOS clear) |
| 4 | firing (laser energized) |
| 5 | trigger pulled |
| 6 | fireState (all votes, should be firing) |
| 7 | COMBAT state |

**Reticle color scheme:**

| Color | Condition |
|-------|-----------|
| GREEN | Nominal / idle |
| ORANGE | Armed |
| YELLOW | Abort active |
| WHITE | Trigger pulled, interlock blocking |
| RED | Laser firing |

---

## 15. Version Word Format

All five controllers use `VERSION_PACK` semver encoding as of ICD v3.1.0:

```
VERSION_PACK(major, minor, patch):
  bits[31:24] = major  (8 bits,  0–255)
  bits[23:12] = minor  (12 bits, 0–4095)
  bits[11:0]  = patch  (12 bits, 0–4095)
```

| Controller | Current Version | VERSION_PACK value |
|------------|----------------|--------------------|
| MCC | 3.3.0 | `VERSION_PACK(3,3,0)` |
| BDC | 3.2.0 | `VERSION_PACK(3,2,0)` |
| FMC | 3.2.0 | `VERSION_PACK(3,2,0)` |
| TRC | 3.0.1 | `VERSION_PACK(3,0,1)` |
| TMC | 3.3.0 | `VERSION_PACK(3,3,0)` |

C# unpack:
```csharp
UInt32 major = (VERSION_WORD >> 24) & 0xFF;
UInt32 minor = (VERSION_WORD >> 12) & 0xFFF;
UInt32 patch =  VERSION_WORD        & 0xFFF;
// No "v" prefix in display string — canonical format is "3.0.1" not "v3.0.1"
```

---

## 16. Compatibility Matrix

| Interface | Status |
|-----------|--------|
| ICD command byte values (all nodes) | ✅ Identical — `defines.hpp` canonical v3.0.0 |
| Frame protocol (magic, geometry, CRC) | ✅ Verified session 15 |
| CRC-16/CCITT implementation | ✅ All controllers on shared `crc.hpp` |
| TRC telemetry session 4 offsets | ✅ BDC parse confirmed (TRC-M10) |
| A1 TRC→BDC alive | ✅ Confirmed session 15 |
| A1 FMC→BDC alive | ✅ Confirmed |
| A1 MCC→BDC alive | ✅ Confirmed |
| A1 TMC→MCC alive | ✅ Confirmed |
| FMC IP/port vs BDC driver | ✅ Match |
| VERSION_PACK format | ✅ All 5 controllers |
| SYSTEM_STATES enum values | ✅ MAINT=0x04, FAULT=0x05 confirmed session 15 |
| TRC binary port | ✅ A2:10018 (legacy 5010 pending TRC-M9 deprecation) |
| THEIA video receive | ✅ H.264 GStreamer pipeline verified |
| MSG_MCC / MSG_BDC shared class | ✅ Deployed sessions 16/17 — HW verify pending |
| TransportPath enum (MSG_MCC/BDC) | ✅ Complete — NEW-12 closed |
| TRC FW A2 framing (udp_listener.cpp) | ✅ Complete — TRC-M7 closed |
| ICD scope labels (INT_OPS/INT_ENG) | ✅ Applied ICD v3.1.0 — NEW-13 closed |
| EXT_OPS framing (CueReceiver/CueSender) | ✅ Deployed session 17 |
| EXT_OPS 15000 port block migration | ✅ Session 37 — 15001/15002/15009/15010 verified |
| TMC V1/V2 hardware abstraction | ✅ Session 30 — `hw_rev.hpp`, unified codebase FW v3.3.0, HW_REV byte [62] self-detecting |
| TMC `SINGLE_LOOP` topology flag | ✅ Session 30 — STATUS_BITS1 bit 6, both revisions |
| MCC V1/V2 hardware abstraction | ✅ MCC unification — `hw_rev.hpp`, unified codebase FW v3.3.0, HW_REV byte [254] self-detecting. HEALTH_BITS/POWER_BITS breaking change — ICD v3.4.0 required. |

---

## 17. Known Open Items

Full open and closed action item tracking has moved to the unified register:
- **`Embedded_Controllers_ACTION_ITEMS.md`** — all open items, priority-ordered
- **`Embedded_Controllers_CLOSED_ACTION_ITEMS.md`** — full closure archive

Quick reference — high priority items as of session 29:

| ID | Item | Priority |
|----|------|----------|
| THEIA-SHUTDOWN | Graceful STANDBY→OFF sequence — laser safe, relays off, HMI disconnect | 🔴 High |
| HMI-A3-18 | LCH/KIZ/HORIZ — architecture analyzed; C# emplacement GUI work pending | 🔴 High |
| FMC-NTP | FMC dt elevated — suspected NTP/USB CDC main loop blocking | 🔴 High |
| GUI-2 | HMI robust testing — full engagement sequence on live HW | 🔴 High |
| FW-B3 | PTP DELAY_REQ W5500 contention — `isPTP_Enabled=false` fleet-wide workaround | 🔴 High |
| GUI-8 | TRC C# client model — apply standardized pattern from session 29 | 🟡 Medium |
| FW-B4 | MCC and FMC `ptp.INIT()` not unconditional at boot — `TIMESRC PTP` silent failure | 🟡 Medium |
| FW-C3 | BDC Fuji boot status — `fuji.SETUP()` deferred post-boot, FUJI_WAIT always times out | 🟡 Medium |
| FW-C4 | BDC A1 ARP backoff not working — `A1 OFF` workaround when TRC offline | 🟡 Medium |
| FW-C5 | Audit/consolidate IP defines in `defines.hpp` — remove remaining hardcoded IPs | 🟡 Medium |
| FW-14 | GNSS socket bug — MCC `RUNONCE` case 6 and `EXEC_UDP` use wrong socket | 🟡 Medium |
| NEW-38d | TRC PTP integration — TIME_BITS, MSG_TRC.cs, `ptp4l` | 🟡 Medium |
| DOC-1 | Add TRC NTP setup reference to ARCHITECTURE.md §2.5 | 🟡 Medium |
| DOC-2 | Create JETSON_SETUP.md — full Jetson Orin NX setup procedure | 🟡 Medium |
| DOC-3 | Add file format specs (horizon, KIZ/LCH, survey) to ICD INT_ENG and INT_OPS | 🟡 Medium |
