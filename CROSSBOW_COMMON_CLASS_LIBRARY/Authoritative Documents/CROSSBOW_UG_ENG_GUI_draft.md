# CROSSBOW User Guide — Engineering GUI

**Document:** `CROSSBOW_UG_ENG_GUI.md`
**Doc #:** IPGD-0014
**Version:** 1.3.0
**Date:** 2026-04-08
**Classification:** CONFIDENTIAL — IPG Internal Use Only
**Audience:** IPG engineering staff, integration engineers
**ICD Reference:** `CROSSBOW_ICD_INT_ENG` (IPGD-0003) v3.4.0 — full INT_ENG and INT_OPS command set
**Architecture Reference:** `ARCHITECTURE.md` (IPGD-0006) v3.3.4 — network topology, framing protocol, port reference

---

> **Pending cross-document updates** — items identified during this guide's authoring
> that require propagation to source documents before next release:
>
> **Cross-document diagram updates:**
>
> | ID | Document | Status | Update Required |
> |----|----------|--------|-----------------|
> | DOC-1 | `ARCHITECTURE.md` (IPGD-0006) §2.4 | 🔲 Open | Expand external topology diagram to show full HYPERION sensor inputs (ADS-B TCP:30002, LoRa UDP:15002, Echodyne TCP:29982, Stellarium HTTP:8090, CUE SIM UDP:15001) |
> | DOC-2 | `CROSSBOW_ICD_INT_ENG` (IPGD-0003) | 🔲 Open | Same diagram update in Tier Overview / Network Reference section |
> | DOC-3 | `CROSSBOW_ICD_INT_OPS` (IPGD-0004) | 🔲 Open | Same diagram update where external topology is referenced |
> | DOC-4 | `CROSSBOW_ICD_EXT_OPS` (IPGD-0005) | 🔲 Open | Verify HYPERION sensor input table is current; add Stellarium if absent |
> | DOC-5 | `CROSSBOW_UG_THEIA.md` (IPGD-0012) | 🔲 Open | Verify external topology diagram reflects current EXT_OPS ports and HYPERION sensor inputs |
> | DOC-6 | `CROSSBOW_UG_HYPERION.md` (IPGD-0013) | 🔲 Open | Verify HYPERION architecture section shows all five sensor inputs including Stellarium `trackLogs["STELLA"]` |
>
> **MCC form action items:**
>
> | ID | File | Status | Action |
> |----|------|--------|--------|
> | MCC-1 | `frmMCC.cs` | 🔲 Open | Wire `SYSTEM_STATES.OFF (0x00)` to dedicated OFF button — currently sends `STNDBY` causing unclean shutdown |
> | MCC-2 | `frmMCC_Designer.cs` | 🔲 Open | Rename groupbox `"IBIT"` → `"TIMING"` |
> | MCC-3 | `frmMCC.cs` | 🔲 Open | Wire `lbl_bdc_hb` — BDC HB source currently commented out in device status panel |
> | MCC-4 | `frmMCC.cs` | 🔲 Open | Implement `chk_Charger_Enable_CheckedChanged` — handler body is empty |
> | MCC-5 | All forms + THEIA | 🔲 Open | Audit device status / readiness / HB field parity across all windows and THEIA |
> | MCC-6 | `mcc.cs` | ✅ Done | CMD_BYTE gate on A2 receive path — `lastMsgRx`, `HB_RX_ms`, `Parse()` now gated on `0xA1` only |
> | MCC-7 | `frmMCC.cs` | ✅ Done | Auto-check `chk_MCC_UnSolEnable` on connect |
> | MCC-8 | `bdc.cs`, `tmc.cs` | ✅ Done | CMD_BYTE gate applied to BDC and TMC A2 receive paths. FMC and TRC pending when those forms are built. |
> | MCC-9 | `frmMCC.cs` | 🔲 Open | NTP config button hardcoded to `.8` primary — replace with text inputs (NTP-1) |
>
> **BDC form action items:**
>
> | ID | File | Status | Action |
> |----|------|--------|--------|
> | BDC-1 | `bdc.cs` | ✅ Done | CMD_BYTE gate on A2 receive path |
> | BDC-2 | `bdc.cs`, `frmBDC.cs` | ✅ Done | `EnableDevice()` added; device checkboxes wired correctly |
> | BDC-3 | `frmBDC.cs` | ✅ Done | `button2_Click` FSM hardcoded string parse removed |
> | BDC-4 | `frmBDC.cs` | ✅ Done | Dead `button2_Click` handler removed |
> | BDC-5 | `frmBDC.cs`, `frmBDC_Designer.cs` | ✅ Done | Vote override readbacks converted to `mb_` StatusLabels |
> | BDC-6 | `frmBDC_Designer.cs` | 🔲 Open | FMC and TRC groupbox body content pending |
> | BDC-7 | `frmBDC.cs`, `frmBDC_Designer.cs` | 🔲 Open | Add remaining VOTE_BITS2 readbacks to GEOMETRY groupbox |
> | BDC-8 | `frmBDC.cs`, `frmBDC_Designer.cs` | 🔲 Open | Add text inputs for Platform LLA/ATT Set buttons |
> | BDC-9 | `MSG_BDC.cs` | 🔲 Open | Guard TRC/FMC pass-through fields when sub-controller absent — `dt max = 65535` when TRC/FMC not connected |
>
> **Connection tracking action items:**
>
> | ID | File | Status | Action |
> |----|------|--------|--------|
> | CONN-1 | `bdc.cs` | ✅ Done | Connection tracking — established/lost/restored logging with uptime and drop time |
> | CONN-2 | `frmBDC.cs`, `frmBDC_Designer.cs` | ✅ Done | CONN uptime + DROPS counter in TIMING panel |
> | CONN-3 | `mcc.cs`, `frmMCC.cs`, `frmMCC_Designer.cs` | ✅ Done | MCC parity with BDC connection tracking |
> | CONN-4 | `tmc.cs`, `frmTMC.cs`, `frmTMC_Designer.cs` | ✅ Done | TMC parity with BDC connection tracking. FMC/TRC pending when those forms are built. |
> | CONN-5 | `bdc.cs` | 🔲 Open | Investigate BDC 660s drop — monitor MCC/TMC for same pattern. Possible: firmware slot eviction, network blip, BDC watchdog. Check whether `SET_UNSOLICITED` needs resending on reconnect. |
>
> **Form layout action items:**
>
> | ID | File | Status | Action |
> |----|------|--------|--------|
> | FORM-1 | All controller forms | 🔄 Partial | 3-column standard applied to MCC, BDC, TMC. FMC, HEL, TRC pending. |
> | FORM-2 | All controller forms | 🔲 Open | Add MODE set controls to Col 1 on all windows |
> | FORM-3 | All controller forms | 🔲 Open | Promote FW VERSION from status strip into dedicated Col 1 panel |
> | FORM-4 | `frmMCC.cs`, `frmBDC.cs` | 🔲 Open | Move ReInit control from TIMING into DEVICE STATUS panel |
> | FORM-5 | `frmMCC.cs`, `frmBDC.cs` | 🔲 Open | Move VOTE_BITS raw field from TIMING into SAFETY panel |
> | FORM-6 | `frmBDC.cs` | ✅ Done | SAFETY/GEOMETRY panel added to BDC Col 2 |
>
> **TMC hardware revision action items:**
>
> | ID | File | Status | Action |
> |----|------|--------|--------|
> | TMC-HW1 | `MSG_TMC.cs` | ✅ Done | `HW_REV` byte [62] parsed; `IsV1`/`IsV2`/`HW_REV_Label` properties added. `isPump1/2Enabled`, `isSingleLoop`, `PumpSpeedValid`, `Tv3Tv4Valid` added. |
> | TMC-HW2 | `tmc.cs` | ✅ Done | `EnableVicor()` guards HEAT channel on V2. `SetDAC()` guards PUMP/HEATER on V2. `EnableBothPumps()` added for V2. |
> | TMC-HW3 | `defines.cs` | ✅ Done | `TMC_VICORS.PUMP1=2`, `PUMP2=4` added. |
> | TMC-HW4 | `frmTMC.cs` | ✅ Done | `ApplyHwRevLayout()` — one-time V1/V2 layout switch on first packet. Pump2 controls added. Pump speed/heater/tv3tv4 hidden on V2. `tss_HW_REV` shows revision label + loop topology. |
> | MSG-1 | `MSG_TMC.cs` | 🔲 Open | Add `tb_` prefixed aliases for TIME_BITS accessors to match `MSG_MCC`/`MSG_BDC` naming convention |
> | MSG-2 | `MSG_TMC.cs`, `MSG_FMC.cs` | 🔲 Open | Document that `isNTP_DeviceEnabled` has no equivalent — TIME groupbox on TMC/FMC uses `isNTPSynched` for both ENABLED and SYNCHED indicators |

> **MCC hardware revision action items:**
>
> | ID | File | Status | Action |
> |----|------|--------|--------|
> | MCC-HW1 | `MSG_MCC.cs` | ✅ Done | `HW_REV` byte [254] parsed; `IsV1`/`IsV2`/`HW_REV_Label` added. `HealthBits`/`PowerBits` renamed from `StatusBits`/`StatusBits2`; backward-compat aliases retained. Seven `pb_*` PowerBits accessors added. `isReady` property added (was missing). Revision-aware `isVicor_Enabled`/`isRelay1/2_Enabled` compat aliases. |
> | MCC-HW2 | `mcc.cs` | ✅ Done | `EnablePower(MCC_POWER, bool)` replaces `EnableSolenoid()`, `EnableRelay()`, `VicorEnable`. Unified power dispatch — single `0xE2 PMS_POWER_ENABLE` send. `ChargeLevel` V2 rejection note added. |
> | MCC-HW3 | `defines.cs` | ✅ Done | `MCC_POWER` enum added (GPS_RELAY=0 through SOL_BDA=6). `MCC_SOLENOIDS`, `MCC_RELAYS`, `MCC_VICORS` removed. `0xE2` → `PMS_POWER_ENABLE`; `0xE4` → `RES_E4` RETIRED; `0xEC` → `RES_EC` RETIRED. |
> | MCC-HW4 | `frmMCC.cs` | ⚠️ Partial | Compile errors fixed (6 call sites: `EnableSolenoid`/`EnableRelay`/`VicorEnable` → `EnablePower`). `tssVersion` shows `HW_REV_Label`. `ApplyHwRevLayout()` pending — solenoid/GPS relay/Vicor bus controls hidden on V2; `chk_Relay3_Enable` shown on V2 as TMS_VICOR; `rad_ChargeLow/Med/High` disabled on V2; `chk_Relay4_Enable` hidden both revisions. |

> **NTP action items:**
>
> | ID | File | Status | Action |
> |----|------|--------|--------|
> | NTP-1 | `frmMCC.cs`, `frmBDC.cs`, `frmTMC.cs` | 🔲 Open | Replace hardcoded NTP octets with text input fields. Primary pre-populated with `.33`, fallback `.208`. Currently hardcoded to test environment values. |

---

## 1. Overview

The CROSSBOW Engineering GUI is a C# .NET 8 WinForms MDI application titled
**IPG CROSSBOW MANAGEMENT SUITE**. It is used for controller diagnostics, firmware
deployment, and maintenance operations on the CROSSBOW platform. It is an internal
engineering tool — it is not present in the operational configuration and is not
accessible from the external IP range.

The application hosts the following child windows:

| Window | Type | Target | Status | Function |
|--------|------|--------|--------|----------|
| MCC | Controller view | 192.168.1.10 | ✅ Current | Master control — power, laser, GNSS, charger, TMC / GNSS / HEL pass-through |
| TMC | Controller view | 192.168.1.12 | ✅ Current | Thermal management — direct A2 |
| BDC | Controller view | 192.168.1.20 | ✅ Current | Beam director — gimbal, cameras, FSM, TRC / FMC pass-through |
| FMC | Controller view | 192.168.1.23 | ✅ Current | FSM DAC/ADC, focus stage — direct A2 |
| HEL | Controller view | 192.168.1.13 | ✅ Current | IPG laser — direct TCP port 10001 (independent of MCC pass-through) |
| TRC | Controller view | 192.168.1.22 | ✅ Current | Jetson tracker — telemetry, ASCII commands via port 5012 |
| GNSS | Controller view | 192.168.1.30 | 🔵 Planned | NovAtel receiver — direct UDP interface (merge from existing VS project) |
| Gimbal | Controller view | 192.168.1.21 | 🔵 Planned | Galil pan/tilt drive — direct interface (merge from existing VS project) |
| Upload FW | Tool | — | ✅ Current | STM32 / SAMD flash, Jetson binary deployment |
| NTP | Tool | — | ✅ Current | NTP message snooper |
| PTP | Tool | — | 🔵 Planned | PTP snooper |

All six controller views follow the same pattern: a live register display showing the
decoded REG1 fields from the controller's most recent unsolicited frame, combined with
a command panel for issuing ICD commands directly. The full INT_ENG command set —
including commands not available in THEIA — is accessible from the relevant controller
view.

MCC and BDC additionally act as pass-throughs for their sub-controllers:

- **MCC** embeds the full TMC REG1 block in its own REG1 at bytes [66–129]. The MCC
  command panel also provides access to commands that MCC routes to its sub-systems:
  TMC (thermal), GNSS (192.168.1.30 — NovAtel receiver), and HEL (192.168.1.13 — IPG
  laser). Direct child windows for GNSS and HEL are also available for subsystem-level
  access.
- **BDC** embeds TRC REG1 at bytes [60–123] and FMC REG1 at bytes [169–232] in its
  own REG1. The BDC command panel provides access to commands that BDC routes onward
  to TRC (via A2 port 10018) and to FMC (via port 10023).

Direct A2 access to TMC, FMC, and TRC is also available via their own child windows —
use the pass-through view for system-level verification and the direct view for
subsystem-level diagnostics.

---

## 2. System Context

### 2.1 Interface Tier Overview

CROSSBOW uses a three-tier interface model. The ENG GUI operates on A1 (read) and A2
(read/write) and has visibility into all tiers.

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
│  Up to 4 simultaneous clients. 60 s liveness timeout.          │
│  BDC also uses A2 to issue commands to TRC.                     │
│                                                                 │
│  MCC (.10)  BDC (.20)  TMC (.12)  FMC (.23)  TRC (.22)         │
│                                                                 │
│  Direct UDP (non-ICD framed) — ENG GUI windows:                │
│  HEL (.13)  GNSS (.30) †  GIMBAL (.21) †                       │
│  NTP (.33)  RPI/ADSB (.31)  LoRa (.32)  RADAR (.34)            │
└───────────────────────┬─────────────────────────────────────────┘
                        │ A3 boundary
┌───────────────────────▼─────────────────────────────────────────┐
│  A3 — INT_OPS (port 10050, magic 0xCB 0x58)                    │
│  THEIA and vendor HMI — MCC + BDC only                         │
│  Up to 2 simultaneous clients. 60 s liveness timeout.          │
│                                                                 │
│  THEIA (.208)  Vendor HMI (.210–.254)                          │
└───────────────────────┬─────────────────────────────────────────┘
                        │ EXT_OPS boundary
┌───────────────────────▼─────────────────────────────────────────┐
│  EXT_OPS — Tier 2 (UDP:15009, magic 0xCB 0x48)                 │
│  CUE input — HYPERION or third-party CUE providers             │
│                                                                 │
│  HYPERION (.206)  Third-party (.210–.254)                       │
└─────────────────────────────────────────────────────────────────┘
```

† GNSS and Gimbal direct-connect ENG GUI windows are planned — merge from existing VS projects.

| Tier | Port | Magic | Nodes Accessible | Audience |
|------|------|-------|-----------------|----------|
| A1 — Controller Bus | 10019 | `0xCB 0x49` | Sub → upper (internal only) | Controller firmware only |
| A2 — Engineering | 10018 | `0xCB 0x49` | All 5 ICD controllers + direct UDP devices | IPG ENG GUI — full INT_ENG access |
| A3 — INT_OPS | 10050 | `0xCB 0x58` | MCC, BDC only | THEIA, vendor HMI — see IPGD-0004 |
| EXT_OPS | 15009 | `0xCB 0x48` | THEIA / HYPERION | CUE providers — see IPGD-0005 |

> **IP range enforcement:** `.1–.99` → A1/A2 accepted. `.200–.254` → A3 only.
> `.100–.199` → reserved, silently dropped on all ports.

---

### 2.2 Network Reference

All nodes on the `192.168.1.x` subnet. Engineering laptops and ENG GUI host PC must
use the `.1–.99` range.

| Node | IP | Role | Connected To |
|------|----|------|-------------|
| MCC | 192.168.1.10 | Master Control Computer | A1→BDC, A2↔ENG GUI, A3↔THEIA |
| TMC | 192.168.1.12 | Thermal Management Controller | A1→MCC, A2↔ENG GUI |
| HEL | 192.168.1.13 | High Energy Laser | Direct ENG GUI TCP window (port 10001); status also embedded in MCC REG1 [45–65] + LASER_MODEL byte [255] |
| BDC | 192.168.1.20 | Beam Director Controller | A1←TRC/FMC/MCC, A2↔ENG GUI, A3↔THEIA |
| Gimbal | 192.168.1.21 | Galil pan/tilt servo drive | BDC CMD:7777 / DATA:7778; direct ENG GUI Galil window planned |
| TRC | 192.168.1.22 | Tracking and Range Computer | A1→BDC, A2↔ENG GUI/BDC |
| FMC | 192.168.1.23 | Fine Mirror Controller | A1→BDC, A2↔ENG GUI |
| GNSS | 192.168.1.30 | NovAtel GNSS receiver | MCC managed — PTP grandmaster + BESTPOS/INS; direct ENG GUI UDP window planned |
| RPI/ADSB | 192.168.1.31 | ADS-B decoder | HYPERION TCP:30002 |
| LoRa | 192.168.1.32 | LoRa/MAVLink track input | HYPERION UDP:15002 |
| NTP | 192.168.1.33 | HW Stratum 1 NTP server | All 5 controllers direct; `.208` auto-fallback |
| RADAR | 192.168.1.34 | Radar track input | HYPERION UDP:15001 |
| THEIA | 192.168.1.208 (default) | INT_OPS HMI — IPG reference | A3↔MCC/BDC; EXT_OPS:15009 CUE receive |
| HYPERION | 192.168.1.206 (default) | EXT_OPS CUE relay — IPG reference | Sensor inputs:15001/15002; CUE out:15009→THEIA |
| IPG reserved | 192.168.1.200–.209 | IPG nodes only | — |
| Third-party | 192.168.1.210–.254 | External integrators | A3 or EXT_OPS |
| ENG GUI host | 192.168.1.1–.99 | Engineering tools | A2 — must remain in this range |

> **IP assignment note:** THEIA and HYPERION addresses shown are IPG reference deployment
> defaults — both are operator-configurable. The constraint is that they remain in the
> `.200–.254` range so embedded controllers accept their A3 packets.

---

### 2.3 Port Reference

**Internal ports (A1 / A2 / A3):**

| Port | Label | Direction | Nodes | Notes |
|------|-------|-----------|-------|-------|
| 10019 | A1 | Sub → upper (always-on) | TMC→MCC, FMC→BDC, TRC→BDC, MCC→BDC | Unsolicited telemetry — no registration; see §2.5 |
| 10018 | A2 | Bidirectional | All five ICD controllers | ENG GUI primary port |
| 10050 | A3 | Bidirectional | MCC, BDC only | THEIA only — ENG GUI does not use this port |
| 10023 | — | BDC → FMC | FMC | BDC-managed direct FMC command link |
| 5000 | Video | TRC → THEIA | TRC | H.264 RTP unicast — THEIA receive only |
| 5012 | ASCII | Bidirectional | TRC | TRC engineering ASCII commands — ENG GUI TRC window; command set listed in §4.7 |
| 7777 | Galil CMD | BDC → Gimbal | Galil | Galil ASCII command TX |
| 7778 | Galil DATA | Gimbal → BDC | Galil | Galil ASCII data / status RX (~125 Hz) |

**External ports (EXT_OPS — for reference):**

| Port | Label | Direction | Nodes | Notes |
|------|-------|-----------|-------|-------|
| 15001 | EXT_OPS | Integrator → HYPERION | HYPERION `aRADAR` | Generic sensor input / CUE SIM injection |
| 15002 | EXT_OPS | Integrator → HYPERION | HYPERION `aLORA` | LoRa / MAVLink sensor input |
| 15009 | EXT_OPS | Bidirectional | THEIA `CueReceiver` | CUE inbound (CMD `0xAA`) + status response (CMD `0xAF`/`0xAB`) |
| 15010 | EXT_OPS | HYPERION → THEIA | HYPERION CUE output | Kalman-filtered track forwarded to THEIA |

> EXT_OPS ports are listed as a system-wide reference. The ENG GUI does not connect to
> these ports. CUE SIM (in `CROSSBOW_EMPLACEMENT_GUIS`) is the IPG tool for injecting
> test tracks into HYPERION or THEIA.

---

### 2.4 Internal Network Topology

Internal subnet — controllers, embedded devices, and engineering tools (`.1–.99`).
All ICD traffic uses magic `0xCB 0x49` (A1/A2).

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

> **Note:** GNSS (.30), HEL (.13), ADS-B (.31), LoRa (.32), and NTP (.33) are on the
> same switch and subnet but are not shown in this diagram. See §2.2 Network Reference
> for the full node list. The ENG GUI connects to GNSS and HEL via direct UDP outside
> the A2 ICD framing model.

---

### 2.5 External Network Topology

External integration zone — THEIA and integration clients (`.200–.254`).
All ICD traffic uses magic `0xCB 0x58` (A3 only). Sub-controllers are not reachable
from this zone.

```
192.168.1.x  EXTERNAL (.200–.254)
══════════════════════════════════════════════════════════════════

  Sensor inputs to HYPERION:
  ┌─────────────────────────────────────────────────────────┐
  │  ADS-B (.31) †  ──── TCP:30002 ─────────────────────┐  │
  │  LoRa  (.32) †  ──── UDP:15002 (EXT_OPS aLORA) ────┐│  │
  │  Echodyne ECHO  ──── TCP:29982 ────────────────────┐││  │
  │  Stellarium ‡   ──── HTTP:8090 (az/el synthetic) ─┐│││  │
  │  CUE SIM (.210–.254) ── UDP:15001 (EXT_OPS aRADAR)┐││││  │
  └────────────────────────────────────────────────────┼┼┼┼┼─┘
                                                        ▼▼▼▼▼
                                               HYPERION (.206 default)
                                               Kalman filter, track mgmt
                                                        │
                                               UDP:15009 (EXT_OPS, CMD 0xAA, 71B)
                                                        │
                                                        ▼
                                               THEIA (.208 default)
                                                        │
                                               A3:10050  magic 0xCB 0x58
                                               ├───────────────► MCC (.10)
                                               │                 (system state, laser,
                                               │                  GNSS, fire vote)
                                               └───────────────► BDC (.20)
                                                                 (gimbal, camera,
                                                                  FSM, fire control)

  Sub-controllers (.12 TMC, .23 FMC, .22 TRC)
       └── No A3 listener — NOT reachable from external zone
```

† ADS-B (.31) and LoRa (.32) are physically on the internal subnet (`.1–.99`) but
their data flows to HYPERION on the external range — they are sensor feeders, not
ICD clients. They have no A2 presence and are not addressable from the ENG GUI.

‡ Stellarium is a PC application with no fixed IP. HYPERION queries it via HTTP on
port 8090 (localhost or configured host). It provides celestial object az/el which
HYPERION converts to a synthetic LLA track via `ned2lla`, stored as
`trackLogs["STELLA"]`.

---

### 2.6 A1 — Always-On Sub-Controller Streams

A1 is a one-way unsolicited stream from each sub-controller to its upper-level
controller. It starts on boot and runs continuously — no registration, no keepalive,
and no ENG GUI interaction required or possible on the A1 port directly.

| Stream | Source | Destination | Port | Rate | Content |
|--------|--------|-------------|------|------|---------|
| TMC telemetry | TMC (.12) | MCC (.10) | 10019 | 100 Hz | TMC REG1 64 B |
| FMC telemetry | FMC (.23) | BDC (.20) | 10019 | 50 Hz | FMC REG1 64 B |
| TRC telemetry | TRC (.22) | BDC (.20) | 10019 | 100 Hz | TRC REG1 64 B |
| Fire control vote | MCC (.10) | BDC (.20) | 10019 | 100 Hz | `0xAB` vote frame |
| Fire control status | BDC (.20) | TRC (.22) | 10019 | 100 Hz | Raw 5 B — no frame wrapper |

The upper-level controller embeds the received sub-controller REG1 data into its own
REG1 pass-through block. This is what the ENG GUI MCC and BDC windows display for TMC
and FMC/TRC respectively — the ENG GUI sees this data via its normal A2 unsolicited
stream from MCC or BDC, not by listening on A1 directly. A1 liveness state (alive /
last seen ms) is also reflected in those REG1 fields.

> **A1 ARP backoff:** If the A1 peer is offline, the sending controller suppresses its
> stream after 3 consecutive send failures (~2 s backoff) to prevent W5500 ARP stalls
> saturating the main loop. Stream resumes automatically when the peer responds. Serial
> command `A1 ON|OFF` disables the stream for bench testing without a connected peer.

---

### 2.7 What ENG GUI Reaches That THEIA Does Not

THEIA connects via A3 (port 10050) and can address MCC and BDC only. TMC, FMC, and
TRC have no A3 listener. The ENG GUI connects via A2 (port 10018) and addresses all
five ICD controllers directly, with the full INT_ENG command set available on each.

Beyond the five ICD controllers, the ENG GUI provides direct UDP windows to hardware
that operates outside the A2/A3 framing model:

- **HEL (.13)** — direct TCP (port 10001) to the IPG laser, independent of the MCC pass-through.
  Allows laser-level diagnostics without MCC in the loop.
- **GNSS (.30)** — direct UDP to the NovAtel receiver *(planned)*.
- **Gimbal (.21)** — direct Galil interface *(planned)*.

THEIA's A3 command whitelist (`EXT_CMDS[]`) blocks all INT_ENG commands. The full
INT_ENG scope — solenoid control, relay enable, Vicor enable, DAC values, FSM axis
signs, PTP/NTP control, and hardware-level debug — is only accessible from the ENG GUI.

---

## 3. A2 Connection Model

Every controller view in the ENG GUI uses the same A2 connection lifecycle. Understanding
this model is useful when diagnosing connectivity issues or when multiple A2 clients are
active simultaneously.

### 3.1 Frame Format

All A2 traffic uses magic bytes `0xCB 0x49` (CB + ASCII `I` for Internal). The frame
geometry is identical to A3 — 521 bytes for responses (2-byte magic, 1-byte SEQ_NUM,
1-byte CMD_BYTE, 1-byte STATUS, 2-byte PAYLOAD_LEN, 512-byte payload, 2-byte CRC-16),
variable length for requests. The C# MSG classes use `TransportPath.A2_Internal` and
strip the frame header before delivering the raw 512-byte payload to the parser.

### 3.2 Connect Sequence

When a controller view's Connect checkbox is checked, `Start()` fires two async tasks:
`backgroundUDPRead()` (receive loop) and `KeepaliveLoop()` (30 s keepalive).

**Current A2 behaviour (ENG GUI):**

```
backgroundUDPRead()
  → bind to internal NIC (.1–.99)
  → FRAME_KEEPALIVE {0x01} × 3        registration burst — registers slot,
                                        gets 1 solicited REG1 response
                                        (frames 2 and 3 rate-gated to 1 Hz)
  → [SET_UNSOLICITED {0x01} missing]   ← MCC-6: must be added here
  → receive loop begins

KeepaliveLoop()
  → FRAME_KEEPALIVE {} every 30 s      ACK only — no REG1, maintains liveness
  → any Send() call also resets the    liveness window (no separate timer needed)
    30 s keepalive countdown
```

**Why the stream appears to work without `SET_UNSOLICITED`:** The firmware does not
reset `wantsUnsolicited` on slot refresh — only on first registration. If the ENG GUI
reconnects within the firmware's 60 s liveness timeout, the previous session's
subscription survives. On a fresh firmware boot, connecting without the checkbox
delivers one REG1 from the burst then nothing — the UI goes stale within 500 ms.

**Target A2 behaviour (pending MCC-6):**

```
backgroundUDPRead()
  → FRAME_KEEPALIVE {0x01} × 3        registration burst
  → await Task.Delay(50 ms)
  → SET_UNSOLICITED {0x01}             subscribe — wantsUnsolicited = true
  → receive loop begins                100 Hz stream starts immediately

KeepaliveLoop()
  → FRAME_KEEPALIVE {} every 30 s      maintains liveness; suppressed if any
                                        other command was sent within 30 s
```

After the fix the `Unsolicited` checkbox becomes a mid-session toggle only — to
temporarily suspend the stream without disconnecting — and auto-checks on connect.

**Firmware slot model:**

The controller allocates a `FrameClient` slot on first accepted frame from a new
source IP+port. Key properties per slot:

| Property | Init | Set by | Cleared by |
|----------|------|--------|------------|
| `active` | `false` → `true` on register | `frameClientRegister` | 60 s timeout or explicit deregister |
| `wantsUnsolicited` | `false` on **first** registration only | `0xA0 {0x01}` | `0xA0 {0x00}`, timeout |
| `last_heard_ms` | set on register | every accepted frame | — |

`wantsUnsolicited` is **not** reset on slot refresh — only on first registration.
This is intentional: subscription state survives keepalive cycles.

### 3.3 Client Table and Liveness

Each controller maintains a client table of active A2 senders. Any accepted frame —
whether a FRAME_KEEPALIVE, a SET_UNSOLICITED, or any other ICD command — auto-registers
the sender and resets its 60-second liveness window. Issuing any command from a
controller view is therefore sufficient to maintain registration; the dedicated
keepalive loop is only needed when no other commands are being sent.

Limits per controller:

| Port | Max simultaneous clients |
|------|--------------------------|
| A2 (10018) | 4 |
| A3 (10050) | 2 |

If a controller view is left open without any activity — no keepalives, no commands —
for more than 60 seconds, the controller will evict the slot. The next FRAME_KEEPALIVE
or any other command re-registers automatically.

> **Concurrent client note:** Up to four A2 clients can be active simultaneously per
> controller. In a typical bench session the ENG GUI host PC consumes one slot. If
> a second engineering laptop is also active, confirm the total A2 client count does
> not approach the limit on MCC and BDC — those two controllers also receive A3
> registrations from THEIA when it is running.

### 3.4 Unsolicited Rate and Solicited Fallback

With `wantsUnsolicited = true` (normal operating state after connect), the controller
pushes REG1 at its full rate and the FRAME_KEEPALIVE keepalive sends an empty payload —
no additional REG1 is requested.

If the ENG GUI sends `FRAME_KEEPALIVE {0x01}` (payload byte = 1) while `wantsUnsolicited`
is already true, the solicited REG1 response is suppressed. The `{0x01}` payload is most
useful before the `SET_UNSOLICITED` subscription is active — i.e. during the initial
registration burst — or after a reconnect where the subscription state is uncertain.

To unsubscribe without dropping the registration slot, send `SET_UNSOLICITED {0x00}`. The
slot remains registered and keepalives continue; unsolicited frames stop until
`SET_UNSOLICITED {0x01}` re-enables them.

---

---

## 4. Child Windows — Controller Views

### 4.1 Common Panel Elements and Standard Layout

All controller windows follow a standard three-column layout. Col 1 is identical
across every window. Col 2 and Col 3 carry controller-specific content but follow
consistent groupbox conventions.

> **Note — layout in progress:** The three-column standard defined here is the target
> layout. Some windows are partially migrated. Open items FORM-1 through FORM-6 in
> the pending actions table track the remaining changes.

#### Standard Three-Column Layout

```
┌─ Col 1 (common — all windows) ──┬─ Col 2 (controller commands) ─┬─ Col 3 (child registers) ─┐
│                                  │                               │                            │
│  ┌─ STATES / MODES ───────────┐  │  MCC / BDC:                  │  MCC:                      │
│  │  State:                    │  │   DEVICE STATUS + ReInit     │   BATTERY                  │
│  │   OFF · STNDBY · ISR · CBT │  │   MAINTENANCE                │   LASER                    │
│  │  Mode (set + readback):    │  │   SAFETY + VOTE_BITS         │   GNSS                     │
│  │   OFF · POS · RATE · CUE  │  │                               │   TMC pass-through         │
│  │   ATRACK · FTRACK          │  │  TMC:                        │   CHARGER                  │
│  └────────────────────────────┘  │   DAC controls               │   Trend chart              │
│                                  │   Fan / LCM / Vicor enable   │                            │
│  ┌─ TIMING ───────────────────┐  │                               │  BDC:                      │
│  │  PROC dt     / dt MAX      │  │  FMC:                        │   TRC pass-through         │
│  │  HB TX       / HB MAX      │  │   FSM controls               │   FMC pass-through         │
│  │  RX N.NN ms ago / GAPS: N  │  │   Focus stage                │   Gimbal data              │
│  │  ● RX OK / RX FROZEN       │  │                               │                            │
│  │  dUTC ±NNN ms [NNN ms]     │  │  HEL:                        │  TMC / FMC / TRC:          │
│  │  [src] MM/dd HH:mm:ss.ff   │  │   Power / enable             │   Direct registers only    │
│  │  MCU TEMP: NN.NN C         │  │   Status / error words       │   (no pass-through)        │
│  │  STATUS_BITS: 00000000     │  │                               │                            │
│  │  STATUS_BITS2: 00000000    │  │  TRC:                        │                            │
│  │  [Reset Stats]             │  │   Process control            │                            │
│  └────────────────────────────┘  │   Stream config              │                            │
│                                  │                               │                            │
│  ┌─ FW VERSION ───────────────┐  │                               │                            │
│  │  Controller: MCC           │  │                               │                            │
│  │  Version:    3.2.0         │  │                               │                            │
│  │  Built:      Apr 05 2026   │  │                               │                            │
│  └────────────────────────────┘  │                               │                            │
└──────────────────────────────────┴───────────────────────────────┴────────────────────────────┘
  Status strip:  PC date/time  |  version string  |  system state  |  gimbal mode
```

#### Col 1 — STATES / MODES Panel

Present on all windows. State and mode radio buttons fire immediately on selection —
there is no confirm dialog.

**System State** — four radio buttons:

| Button | `SYSTEM_STATES` value | Notes |
|--------|----------------------|-------|
| OFF | `OFF` (0x00) | ⚠️ Currently missing — see MCC-1. Needed for clean shutdown. |
| STNDBY | `STNDBY` (0x01) | |
| ISR | `ISR` (0x02) | |
| CBT | `COMBAT` (0x03) | |

**ICD command:** `0xE4 SET_SYS_STATE`

**Gimbal Mode** — set control + readback label. The command routes to BDC regardless
of which window sends it. Non-BDC windows show the mode readback from their own REG1
where the field is available (e.g. MCC `BDC_Mode`).

| Mode | `BDC_MODES` value |
|------|------------------|
| OFF | 0x00 |
| POS | 0x01 |
| RATE | 0x02 |
| CUE | 0x03 |
| ATRACK | 0x04 |
| FTRACK | 0x05 |

**ICD command:** `0xB9 SET_BDC_MODE` → routed to BDC from all windows

> ⚠️ **FORM-2 pending:** Mode set controls are currently only present on the BDC
> window. All other windows show the mode readback in the status strip only.

#### Col 1 — TIMING Panel

Present on all windows. All fields update on every UI timer tick (~100 ms). The
TIMING panel is the first place to check when a controller appears unresponsive.

| Field | Source | Description |
|-------|--------|-------------|
| `PROC dt` | `LatestMSG.dt_us` | Controller main loop cycle time (µs) — nominal ~100–200 µs |
| `dt MAX` | Rolling max | Peak `dt_us` since last reset — sticky until Reset Stats |
| `HB TX` | `LatestMSG.HB_TX_ms` | Controller heartbeat TX interval (ms) — nominal ~10 ms at 100 Hz |
| `HB MAX` | Rolling max | Peak `HB_TX_ms` since last reset |
| `RX` | `lastMsgRx` | Time since last frame received (ms) — turns **red** if > 500 ms |
| `GAPS` | Gap counter | Count of inter-frame gaps > 200 ms — turns **orange** if > 0 |
| Connection LED | Composite | **Green** = RX OK · **Red** = RX FROZEN · **Grey** = WAITING |
| `dUTC` | `lastMsgRx − epochTime` | Difference between PC receive time and controller UTC epoch (ms) — tracks NTP/PTP alignment; rolling max in brackets |
| Epoch time | `LatestMSG.epochTime` | UTC time from controller with active source label: `[PTP]`, `[NTP]`, `[NTP-fallback]`, `[NONE]` |
| `MCU TEMP` | `LatestMSG.TEMP_MCU` | STM32 internal die temperature (°C) |
| `STATUS_BITS` | `LatestMSG.StatusBits` | 8-bit binary string |
| `STATUS_BITS2` | `LatestMSG.StatusBits2` | 8-bit binary string |
| **Reset Stats** | Button | Clears `dt MAX`, `HB MAX`, gap count, and `dUTC` max |

> `dUTC` spikes > ±50 ms indicate NTP sync problems. Sustained non-zero values with
> `[NTP-fallback]` label mean the primary NTP server (`.33`) is unreachable — check
> network and NTP appliance. Values with `[NONE]` mean both PTP and NTP have failed
> and the controller is free-running from the last good timestamp.

#### Col 1 — FW VERSION Panel

Present on all windows. Displays the firmware identity of the connected controller.

| Field | Source | Example |
|-------|--------|---------|
| Controller | Parsed from INFO response | `MCC` |
| Version | `LatestMSG.FW_VERSION_STRING` | `3.2.0` |
| Built | Build date from INFO | `Apr 05 2026 14:22:11` |

> ⚠️ **FORM-3 pending:** Version and build date are currently shown in the status
> strip only (`tssVersion`). Promotion to a dedicated Col 1 panel is a pending form
> change.

Version format is `major.minor.patch` with no `v` prefix — canonical format throughout
all CROSSBOW applications. `VERSION_PACK` decode: bits [31:24] = major, [23:12] =
minor, [11:0] = patch.

#### Col 2 — DEVICE STATUS Panel (MCC and BDC)

One enabled/ready LED pair per managed device, plus a heartbeat label and an enable
checkbox. ReInit also lives here.

| Indicator | State | Meaning |
|-----------|-------|---------|
| Enabled — **Green** | `DEVICE_ENABLED_BITS` bit set | Device enabled |
| Enabled — **Grey** | bit clear | Device disabled |
| Ready — **Green** | enabled + `DEVICE_READY_BITS` bit set | Device healthy and ready |
| Ready — **Red** | enabled + bit clear | Enabled but not ready — initialising or faulted |
| Ready — **Grey** | not enabled | Ready state not applicable |

All eight MCC devices should show Green/Green during normal operation. A Red Ready
indicator with Green Enabled means the device is initialising (normal at boot — allow
~20 s) or has faulted (use ReInit to recover without power-cycling).

**ReInit control** — device dropdown (`MCC_DEVICES` enum) + **ReInit** button.
Reinitialises a specific subsystem in place. Use to recover a stuck NTP, PTP, or GNSS
client.

**ICD command:** `0xE0 SET_MCC_REINIT` (MCC) · `0xB0 SET_BDC_REINIT` (BDC)

#### Col 2 — SAFETY Panel (MCC and BDC only)

Vote readbacks and the laser fire request command. All checkboxes are read-only
readbacks except **Laser Fire Request**.

The raw `VOTE_BITS` byte is also displayed here as an 8-bit binary string alongside
the decoded individual bit readbacks.

> ⚠️ **FORM-5/FORM-6 pending:** VOTE_BITS is currently in the TIMING panel on MCC.
> SAFETY panel is not yet present on BDC.

#### Connection Control

Each window has a **Connect** checkbox. Checking it calls `Start()` — executes the
A2 connect sequence (§3.2). Unchecking calls `Stop()`. The window continues to
display the last received values when disconnected; fields go stale and the
connection LED turns red after 500 ms.

An **Unsolicited** checkbox maps to `SET_UNSOLICITED {0x01}/{0x00}`. Checked by
default after connect. Unchecking stops the 100 Hz push but keeps the registration
slot alive — the keepalive loop continues at 30 s.

#### Shared Class Library

All controller message classes are defined in the **CROSSBOW Common Class Library**
(`namespace CROSSBOW`, separate C# project referenced by both ENG GUI and THEIA).
`TransportPath.A2_Internal` is used throughout the ENG GUI. Do not diverge parsing
logic between the two applications — maintain parity in the shared library.

---

### 4.2 MCC — Master Control Computer

**Target:** 192.168.1.10 · A2 port 10018
**Class:** `MCC` (`namespace CROSSBOW`, `TransportPath.A2_Internal`)
**Log file:** `C:\temp\CROSSBOW_MC_LOG_<date>.txt` (Serilog, daily rolling)

The MCC window is the most content-rich child window. It displays live MCC REG1
telemetry, provides direct access to all MCC INT_ENG commands, and embeds pass-through
panels for TMC (via MCC REG1 [66–129]), GNSS, and the battery charger (CMC).

#### Current vs. Target Layout

The MCC window is partially migrated to the three-column standard. The table below
shows current groupbox positions and their target column.

| Groupbox | Current position | Target column | Action item |
|----------|-----------------|---------------|-------------|
| STATE | Col 1 (left panel) | Col 1 | Add OFF button (MCC-1); add MODE controls (FORM-2) |
| TIMING (currently "IBIT") | Centre panel | Col 1 | Rename (MCC-2); move VOTE_BITS out (FORM-5) |
| FW VERSION | Status strip only | Col 1 | Promote to dedicated panel (FORM-3) |
| MCC DEVICE STATUS | Col 1 (left panel) | Col 2 | Move ReInit here (FORM-4); wire BDC HB (MCC-3) |
| MAINTENANCE | Col 1 (left panel) | Col 2 | Fix empty charger handler (MCC-4) |
| SAFETY + VOTE_BITS | Col 1 (left panel) | Col 2 | Move VOTE_BITS from TIMING (FORM-5) |
| BATTERY | Right panel | Col 3 | No change |
| LASER | Right panel | Col 3 | No change |
| GNSS | Right panel | Col 3 | No change |
| TMC pass-through | Bottom panel | Col 3 | No change |
| CHARGER (CMC) | Bottom panel | Col 3 | No change |
| Trend chart | Centre panel | Col 3 | No change |

#### Col 2 — MCC DEVICE STATUS

Enabled/ready LED pairs, enable checkboxes, and heartbeat labels for all eight
MCC-managed devices. ReInit also lives here (pending FORM-4 migration).

| Device | `MCC_DEVICES` | Enable checkbox | HB source |
|--------|--------------|-----------------|-----------|
| Battery | `BAT` | `chk_battery_enable` | `HB_BAT` |
| Charger | `CRG` | `chk_charger_enable` | `HB_CRG` |
| Chiller (TMC) | `TMC` | `chk_chiller_enable` | `TMCMsg.dt_us / 1000` |
| Laser (HEL) | `HEL` | `chk_laser_enable` | `HB_HEL` |
| NTP | `NTP` | `chk_ntp_enable` | `HB_NTP` |
| PTP | `PTP` | `chk_ptp_enable` | ⚠️ not yet wired |
| GNSS | `GNSS` | `chk_gnss_enable` | `HB_GNSS` |
| BDC | `BDC` | `chk_bdc_enable` | ⚠️ MCC-3 — commented out |

Each checkbox calls `aMCC.EnableDevice(MCC_DEVICES.xxx, bool)`.
**ICD command:** `0xE1 SET_MCC_DEVICES_ENABLE`

**ReInit** — `MCC_DEVICES` enum dropdown + **ReInit** button →
`aMCC.ReInitDevice(mccd)`.
**ICD command:** `0xE0 SET_MCC_REINIT`

#### Col 2 — MAINTENANCE

Enable/disable switches with paired readback checkboxes. The left checkbox is the
command; the right (`_rb` suffix) is the readback from the most recent REG1 frame —
they should agree within one update cycle.

| Control | Maps to | ICD command |
|---------|---------|-------------|
| Solenoid 1 (HEL) | `MCC_SOLENOIDS.HEL` | `0xE2 SET_MCC_SOLENOID` |
| Solenoid 2 (BDA) | `MCC_SOLENOIDS.BDA` | `0xE2 SET_MCC_SOLENOID` |
| Relay 1 (GPS) | `MCC_RELAYS.GPS` | `0xE3 SET_MCC_RELAY` |
| Relay 2 (HEL) | `MCC_RELAYS.HEL` | `0xE3 SET_MCC_RELAY` |
| Relay 3 (TMS) | `MCC_RELAYS.TMS` | `0xE3 SET_MCC_RELAY` |
| VICOR | — | `aMCC.VicorEnable` |

Additional readbacks (no command from this panel):
- **Laser Power Bus Enabled** — `isLaserPowerBus_Enabled`
- **Charger Enabled** — `isCharger_Enabled`
- **Not Battery Low Voltage** — `isNotBatLowVoltage`

> ⚠️ **MCC-4:** `chk_Charger_Enable_CheckedChanged` handler is empty. Two separate
> charger controls exist: `chk_charger_enable` enables the MCC charger device
> (`EnableDevice(MCC_DEVICES.CRG)`); `chk_Charge_Enable` should turn the charger
> on/off (`aMCC.ChargeEnabled`). Handler needs implementation.

#### Col 2 — SAFETY

Vote readbacks and the laser fire request command. All checkboxes update from the most
recent MCC REG1 frame. The raw `VOTE_BITS` byte is displayed as an 8-bit binary
string alongside the decoded bit readbacks (pending FORM-5 migration from TIMING).

| Readback | Property | Description |
|----------|----------|-------------|
| Not Abort Vote | `isNotAbort_Vote_rb` | 0 = abort ACTIVE — inverted, safe-by-default |
| Armed Vote | `isArmed_Vote_rb` | System armed |
| BDA Vote | `isBDA_Vote_rb` | Beam director area clear |
| Total HW Vote | `isLaserTotalHW_Vote_rb` | Hardware interlock chain complete |
| Fire Requested Vote | `isLaserFireRequested_Vote_rb` | Operator trigger pulled (readback) |
| Total Laser Vote | `isLaserTotal_Vote_rb` | All votes passed — laser may fire |
| Combat State Vote | `isCombat_Vote_rb` | System in COMBAT state |

**Laser Fire Request** (`chk_LaserFireRequested_Vote`) — writable command checkbox.
Sets `aMCC.LaserFireRequest = true`. A 100 ms watchdog timer (`LaserFireWatchDog`)
re-asserts the request every tick while connected and `isCombat_Vote_rb` is true.

**ICD command:** `0xE6 PMS_SET_FIRE_REQUESTED_VOTE`

> ⚠️ This is a live command included for bench testing and integration verification
> only. Confirm the system is in a safe state and all hardware interlocks are
> satisfied before enabling this checkbox.

#### Col 3 — LASER

Displays IPG laser housekeeping from `aMCC.LatestMSG.IPGMsg` — the laser block
embedded in MCC REG1.

| Field | Source | Format |
|-------|--------|--------|
| HK Voltage | `IPGMsg.HKVoltage` | V (0.00) |
| Bus Voltage | `IPGMsg.BusVoltage` | V (0.00) |
| Case Temp | `IPGMsg.Temperature` | °C (0.00) |
| Status Word | `IPGMsg.StatusWord` | 32-bit binary |
| Error Word | `IPGMsg.ErrorWord` | 32-bit binary |
| Setpoint | `IPGMsg.SetPoint` | Progress bar 0–100% |
| Output Power | `IPGMsg.OutputPower_W` | Progress bar 0–3000 W |
| EMON | `isHEL_EMON` | Readback checkbox |
| NOT READY | `isHEL_NOTREADY` | Readback checkbox |

| Control | Action | ICD command |
|---------|--------|-------------|
| **Clear Errors** | `aMCC.ClearLaserError()` | `0xEC SET_MCC_HEL_CLEAR_ERROR` |
| **Set Power** (numeric + button) | `aMCC.SetLaserPower(uint)` | `0xED SET_MCC_HEL_POWER` |

#### Col 3 — BATTERY

Battery management system data from `aMCC.LatestMSG.BatteryMsg`. All fields
read-only.

| Field | Source | Display |
|-------|--------|---------|
| Pack Voltage | `PackVoltage` | Circular gauge 0–100 |
| Pack Current | `PackCurrent` | Circular gauge + signed label (A) |
| Pack Temp | `PackTemp` | Circular gauge 0–100 |
| Bus Voltage | `BusVoltage` | Circular gauge 0–100 |
| ASOC | `ASOC` | Level gauge 0–100% |
| RSOC | `RSOC` | Level gauge 0–100% |
| Contactor Closed | `isContractorClosed` | Green / Red |
| Breaker Closed | `isBreakerClosed` | Green / Red |
| Status Word | `StatusWord` | 16-bit binary |

#### Col 3 — GNSS

NovAtel data from `aMCC.LatestMSG.GNSSMsg` — the GNSS block embedded in MCC REG1.
Four sub-panels:

**Header:** Last RX time, UTC time, TerraStar sync state, geoid undulation (m)

**BESTPOS:** Lat / Lng / Alt HAE with ±σ, solution status enum, position type enum,
satellites in solution (SIS) / in view (SIV)

**INS:** Roll / Pitch / Azimuth with ±σ

**ANT HEADING:** Heading with ±σ

> Azimuth STDEV > ~0.5° warrants investigation before entering COMBAT — a 2° azimuth
> error at 1 km produces ~35 m cross-range pointing error. See Emplacement GUI guide
> (IPGD-0015) for the attitude refinement procedure.

#### Col 3 — TMC Pass-Through

Full TMC thermal telemetry from `aMCC.LatestMSG.TMCMsg` (MCC REG1 bytes [66–129]).
Data arrives via the A1 TMC→MCC stream — identical content to the TMC child window
but without direct A2 access. All fields read-only here; TMC commands are issued
from the dedicated TMC window.

**Timing:** TMC PROC dt, TMC HB TX, active time source + epoch, STATUS_BITS1/2/3,
MCU temp, TPH ambient

**Temperatures:** TARGET, TF1, TF2, VIC1–4, OUT1–2, AIR1, COMP1–2 (all °C)

**Actuator readbacks:** Fan1/Fan2 enabled · Vicor1/Vicor2 enabled · LCM1/LCM2
enabled + speed (DAC counts) + current (A) · Pump enabled + speed (DAC counts)

#### Col 3 — CHARGER (CMC)

Charger management data from `aMCC.LatestMSG.CMCMsg`.

| Field | Source | Format |
|-------|--------|--------|
| VIN | `CMCMsg.VIN` | V (0.00) |
| VOUT [VOUT_MAX] | `CMCMsg.VOUT / VOUT_MAX` | V (0.00) |
| IOUT [IOUT_MAX] | `CMCMsg.IOUT / IOUT_MAX` | A (0.00) |
| Fan speeds | `FAN1_SPEED / FAN2_SPEED` | RPM |
| CHARGE_STATUS | `CHARGE_STATUS` | 16-bit binary |
| CHARGE_CURVE_CONFIG | `CHARGE_CURVE_CONFIG` | 16-bit binary |
| ONOFF_CONFIG | `ONOFF_CONFIG` | 8-bit binary |
| STATUS_BITS1 | `STATUS_BITS1` | 8-bit binary |
| Charge Level | `ChargeLevel` | LO / MED / HI |

| Control | Action | ICD command |
|---------|--------|-------------|
| **LO / MED / HI** radio buttons | `aMCC.ChargeLevel = CHARGE_LEVELS.xxx` | `0xEA SET_MCC_CHARGE_LEVEL` |
| Charge Enable checkbox | `aMCC.ChargeEnabled = bool` | `0xE5 SET_MCC_CHARGE_ENABLE` |

#### Col 3 — Temperature Trend Chart

ScottPlot `DataStreamer` — last 600 samples (~60 s at 100 ms timer rate).
Y-axis fixed: major ticks at 0, 25, 50 °C. Pan/zoom disabled.

| Series | Source | Legend |
|--------|--------|--------|
| Ambient | `aMCC.LatestMSG.TEMPERATURE` | `TPH` |
| MCU die | `aMCC.LatestMSG.TEMP_MCU` | `MCU` |

---

---

### 4.3 TMC — Thermal Management Controller

**Target:** 192.168.1.12 · A2 port 10018
**Class:** `TMC` (`namespace CROSSBOW`, A2 internal only — no A3 path)
**Log file:** Debug output only (no Serilog — TMC uses `Debug.WriteLine`)

The TMC window provides direct A2 access to the Thermal Management Controller. TMC manages the liquid cooling loop (LCM1/LCM2 compressors, coolant pumps, fans) and Vicor/TRACO power converters. It has no sub-controllers and no pass-through blocks. All telemetry is from TMC REG1 (64 bytes).

TMC supports two hardware revisions selected at compile time (`hw_rev.hpp`). The active revision is reported in REG1 byte [62] and shown in the status strip as `HW: V1 — Vicor/ADS1015` or `HW: V2 — TRACO/direct`. The GUI automatically adjusts control visibility on first packet — pump speed DAC controls and heater controls are hidden on V2; a second independent pump control (PUMP2) is shown on V2.

TMC telemetry is also visible via the MCC window pass-through (MCC REG1 bytes [66–129]) — use the MCC view for system-level verification and the TMC window for subsystem-level control and diagnostics.

#### Col 1 — STATES / MODES

Standard state radio buttons (STNDBY / ISR / CBT). No OFF button currently — see MCC-1.

#### Col 1 — TIMING

Standard TIMING panel — see §4.1. Fields specific to TMC:

| Field | Source | Notes |
|-------|--------|-------|
| `TMC PROC dt` | `LatestMSG.dt_us` | µs — nominal ~200–400 µs |
| `TMC HB TX` | `LatestMSG.HB_TX_ms` | ms — nominal ~10 ms at 100 Hz |
| `CONN` | Form uptime counter | Time since first REG1 received this session |
| `DROPS` | `aTMC.DropCount` | Cumulative drop count — turns OrangeRed if > 0 |

#### Col 1 — TIME

Standard TIME panel — see §4.1. TMC TIME_BITS use `STATUS_BITS3` rather than a dedicated `TimeBits` byte — see MSG-1. NTP ENABLED indicator uses `isNTPSynched` as proxy (TMC has no `isNTP_DeviceEnabled` bit — see MSG-2).

#### Col 1 — Temps

TPH ambient (°C, Pa, %) and MCU die temperature. ScottPlot trend chart — last 600 samples.

#### Col 2 — SENSOR READINGS

All TMC temperature, flow, and current readbacks. Read-only.

**Temperatures (all °C, integer display):**

| Label | Source | Notes |
|-------|--------|-------|
| TARGET | `TEMP_TARGET` | Set-point readback |
| OTF1 | `TEMP_TF1` | Fluid temp 1 — direct MCU analog, both revisions |
| OTF2 | `TEMP_TF2` | Fluid temp 2 — direct MCU analog, both revisions |
| VIC1 | `TEMP_V1` | Vicor LCM1 temperature — both revisions |
| VIC2 | `TEMP_V2` | Vicor LCM2 temperature — both revisions |
| IN1 | `TEMP_O1` | Output channel 1 — V1: ADS1015; V2: direct MCU analog |
| IN2 | `TEMP_O2` | Output channel 2 — direct MCU analog, both revisions |
| AIR1 | `TEMP_AIR1` | Air temp 1 — V1: ADS1015; V2: direct MCU analog |
| COMP1 | `TEMP_C1` | Compressor 1 — V1: ADS1015; V2: direct MCU analog |
| COMP2 | `TEMP_C2` | Compressor 2 — V1: ADS1015; V2: direct MCU analog |
| VIC3 | `TEMP_V3` | Vicor heater temp — **V1 only** (hidden on V2; field reserved 0x00) |
| VIC4 | `TEMP_V4` | Vicor pump temp — **V1 only** (hidden on V2; field reserved 0x00) |

**Flow (LPM):** FLOW1, FLOW2

**LCM current (A):** LCM1_CURRENT, LCM2_CURRENT

#### Col 3 — MAINT CONTROL

Command panel. All controls send ICD commands immediately on change.

**LCM1 / LCM2 — liquid cooling modules:**

| Control | Action | ICD command |
|---------|--------|-------------|
| Vicor Enable checkbox | `aTMC.EnableVicor(LCM1/LCM2, bool)` | `0xE9 TMS_SET_VICOR_ENABLE` |
| LCM Enable checkbox | `aTMC.EnableLCM(LCM1/LCM2, bool)` | `0xEA TMS_SET_LCM_ENABLE` |
| Speed dropdown | `aTMC.SetDAC(LCM1/LCM2, value)` | `0xE8 TMS_SET_DAC_VALUE` |
| Set Speed button | Sends selected DAC value | — |

Readbacks: `mb_LCM1_Vicor_Enabled_rb`, `mb_LCM1_Enabled_rb`, `mb_LCM1_Error_rb`, `lbl_LCM1_Speed_rb`, `lbl_LCM1_Current_rb`

**AUX — pump, fans, heater:**

Controls in this group are hardware-revision-dependent. The GUI auto-hides V1-only controls when connected to V2 hardware (`HW_REV` byte [62]).

| Control | Revision | Action | ICD command |
|---------|----------|--------|-------------|
| Pump Enable (PUMP1) | Both | `aTMC.EnableVicor(PUMP/PUMP1, bool)` — V1: Vicor; V2: TRACO PSU 1 | `0xE9` |
| Pump 2 Enable | **V2 only** | `aTMC.EnableVicor(PUMP2, bool)` — TRACO PSU 2 | `0xE9` |
| Pump Speed dropdown | **V1 only** | `aTMC.SetDAC(PUMP, value)` — Vicor DAC trim [0–800] | `0xE8` |
| Fan1 / Fan2 (3-state) | Both | `aTMC.SetInputFanSpeed(0/1, OFF/LO/HI)` | `0xE7` |
| Heater Enable | **V1 only** | `aTMC.EnableVicor(HEAT, bool)` | `0xE9` |
| Target Temp (text + button) | Both | `aTMC.SetTargetTemp(byte)` — firmware clamps to 10–40 °C | `0xEB` |

**Status strip:** shows `HW: V1 — Vicor/ADS1015` or `HW: V2 — TRACO/direct` + `SINGLE LOOP` or `PARALLEL LOOP` (from STATUS_BITS1 bit 6).

**NTP config:** `btn_SetNTP_Servers` — hardcoded to test environment values. See NTP-1.

---

### 4.4 BDC — Beam Director Controller

**Target:** 192.168.1.20 · A2 port 10018
**Class:** `BDC` (`namespace CROSSBOW`, `TransportPath.A2_Internal`)
**Log file:** `C:\temp\CROSSBOW_BDC_LOG_<date>.txt` (Serilog, daily rolling)

The BDC window is the second most content-rich child window after MCC. It displays live BDC REG1 telemetry, provides direct access to all BDC INT_ENG commands, and embeds pass-through panels for Gimbal, TRC, and FMC.

#### Col 1 — STATES / MODES

Standard state radio buttons. Connect handler auto-subscribes to unsolicited stream on connect.

#### Col 1 — TIMING

Standard TIMING panel — see §4.1. Includes CONN uptime and DROPS counter.

#### Col 1 — TIME

Standard TIME panel. PTP + NTP indicators from `TimeBits` byte 391.

#### Col 1 — Temps

MCU temp, TPH ambient. ScottPlot trend chart.

#### Col 2 — BDC DEVICE STATUS

Eight BDC-managed devices — enabled/ready LED pairs, enable checkboxes, HB labels, and ReInit.

| Device | `BDC_DEVICES` | Enable checkbox | HB source |
|--------|--------------|-----------------|-----------|
| NTP | `NTP` | `chk_ntp_enable` | `lbl_ntp_hb` |
| GIMBAL | `GIMBAL` | `chk_gimbal_enable` | `lbl_gimbal_hb` |
| FUJI (VIS cam) | `FUJI` | `chk_visCam_enable` | `lbl_visCam_hb` |
| MWIR | `MWIR` | `chk_irCam_enable` | `lbl_irCam_hb` |
| FSM | `FSM` | `chk_fmc_enable` | `lbl_fmc_hb` |
| JETSON (TRC) | `JETSON` | `chk_trc_enable` | `lbl_trc_hb` |
| INCL | `INCL` | `chk_incl_enable` | `lbl_incl_hb` |
| PTP | `PTP` | `chk_ptp_enable` | — |

Each checkbox calls `aBDC.EnableDevice(BDC_DEVICES.xxx, bool)`.
**ICD command:** `0xBE SET_BDC_DEVICES_ENABLE`

**ReInit** — `BDC_DEVICES` enum dropdown + **ReInit** button → `aBDC.ReInitDevice(bdcd)`.
**ICD command:** `0xB0 SET_BDC_REINIT`

#### Col 2 — MAINTENANCE

Vicor and relay enable controls with readbacks.

| Control | Maps to | ICD command |
|---------|---------|-------------|
| Vicor Enable | `aBDC.VicorEnabled` | `0xBA SET_BDC_VICOR_ENABLE` |
| Relay 1–4 Enable | `aBDC.EnableRelay(n, bool)` | `0xBB SET_BDC_RELAY_ENABLE` |

Raw STATUS_BITS and STATUS_BITS2 8-bit binary readbacks.

#### Col 2 — PLATFORM

Platform position and attitude latched readbacks from BDC REG1 [245–276]. Set buttons send commands to BDC.

| Field | Source | ICD command |
|-------|--------|-------------|
| LAT / LNG / ALT | `PLATFORM_LLA.lat/lng/alt` | `0xC0 SET_SYS_LLA` via `aBDC.SetPlatformLLA()` |
| ROLL / PITCH / YAW | `PLATFORM_RPY.roll/pitch/yaw` | `0xC1 SET_SYS_ATT` via `aBDC.SetPlatformATT()` |

> ⚠️ **BDC-8 pending:** Set buttons currently stubbed — text inputs for LLA/ATT values not yet implemented.

#### Col 2 — PREDICTIVE AVOIDANCE (GEOMETRY)

Vote readbacks, override controls, and KIZ/LCH file status indicators.

**Override commands** (checkboxes — `aBDC.SetOverrideVote()`):
- HORIZ Override, KIZ Override, LCH Override — each with `mb_` readback indicator (Red when override active)

**Geometry status:**

| Indicator | Source | Meaning |
|-----------|--------|---------|
| `mb_BelowHoriz_rb` | `BelowHorizVote` (VB2 bit 0) | Below horizon vote passes |
| `mb_InKIZ_rb` | `InKIZVote` (VB2 bit 1) | KIZ vote passes |
| `mb_InLCH_rb` | `InLCHVote` (VB2 bit 2) | LCH vote passes |
| `mb_HorizLoaded_rb` | `isHorizonLoaded` (VB2 bit 5) | Horizon profile loaded — Red if not loaded |
| `mb_BDCVote_rb` | `BDCTotalVote` (VB2 bit 3) | All BDC geometry votes pass |
| `mb_FSMOk_rb` | `isFSMNotLimited` (VB2 bit 7) | FSM not at travel limit — Red if limited |

**KIZ detail** (6 indicators): LOAD · ENAB · TIME · OPER · POS · EXEC

**LCH detail** (6 indicators): LOAD · ENAB · TIME · OPER · POS · EXEC

**Raw vote bytes:** MCC VoteBits3, KIZ VoteBitsKIZ, LCH VoteBitsLCH (8-bit binary)

#### Col 3 — GIMBAL

Live gimbal register data from BDC REG1 bytes [20–58] + non-contiguous fields.

**Position/speed:**
- PosX / PosY — relative angle (°) + raw encoder count
- VX / VY — encoder speed counts
- StatusX / StatusY — 16-bit Galil status word (binary) + stop code

**Non-contiguous:**
- HomeX / HomeY — encoder home counts [237–244]
- BasePitch / BaseRoll — inclinometer °  [124–131]

**LOS (NED) sub-groupbox:**
- GIM AZ / GIM EL — gimbal NED pointing angles from `LOS_GIM`
- FSM AZ c / FSM EL c — FSM commanded NED from `LOS_FSM_C`
- FSM AZ r / FSM EL r — FSM readback NED from `LOS_FSM_RB`
- CUE PAN / CUE TILT — encoder target counts from `TARGET_PAN/TILT`

**Status strip (bottom):** version · ● READY · ● CONN · ● START · temp · BDC epoch time

**Commands:** PARK button (`aBDC.GimbalSetHome()`), STOP button

#### Col 3 — FMC

FSM and focus stage data from FMC REG1 pass-through [169–232] + BDC REG1 FSM fields [233–379].

> ⚠️ **BDC-6 pending:** FMC groupbox body content not yet wired in `updateFMCMsg()`. Status strip complete.

**Status strip (bottom):** version · ● READY · ● PWR · ● STAGE · MCU temp · epoch time

#### Col 4 — TRC

TRC register data from TRC REG1 pass-through [60–123].

> ⚠️ **BDC-6 pending:** TRC groupbox body content not yet wired in `updateTRCMsg()`. Status strip complete. `updateTRCMsg()` not yet called in `timer1_Tick`.

**Status strip (bottom):** version · ● READY · ● CONN · ● START · Jetson temp · NTP epoch time

---

### 4.5 FMC — Fine Mirror Controller

**Target:** 192.168.1.23 · A2 port 10018
**Class:** `FMC` (`namespace CROSSBOW`, A2 internal only)

> 🔲 **Section pending** — FMC form not yet built. FMC telemetry currently accessible via BDC pass-through (§4.4 Col 3). Direct A2 window planned.

Key content when built:
- FSM position X/Y (ADC readback, int32) and commanded X/Y (int16)
- FSM axis signs and null offsets (calibration)
- iFOV deg/count calibration constants
- Focus stage position (counts → mm), home, error mask, status
- `FSMTestScan()`, `FMC_SET_FSM_SIGNS()`, `STAGE_CALIBRATE()`, `STAGE_ENABLED` controls
- TIME_BITS, MCU temp, epoch time

---

### 4.6 HEL — High Energy Laser

**Target:** 192.168.1.13 · direct TCP port 10001
**Class:** `HEL` (`namespace CROSSBOW`, direct TCP — not ICD framed)
**Transport:** TCP port 10001 — both YLM-3K and YLR-6K use the same port.

> ⚠️ **MCC co-existence note:** Current MCC firmware (pre-Step 2) uses UDP port 10011 for laser comms — a different socket. ENG GUI direct TCP and MCC UDP can coexist. After Step 2 firmware is applied, MCC will use TCP 10001 and the ENG GUI direct window should only be used with MCC HEL device disabled (`0xE1 SET_MCC_DEVICES_ENABLE, device=2, en=0`).

#### Layout — 3 columns (1320 × 854)

**Col 1 — left panel (330px):**

*CONNECTION groupbox:*
- `IP` text input (default `192.168.1.13`) + `PORT` text input (default `10001`)
- `HEL Connect` checkbox — triggers `await aHEL.Start()` on check, `aHEL.Stop()` on uncheck
- `mb_HEL_connStatus` — `OFFLINE` (red) / `SENSING` (grey) / `SENSED` (green)
- `MODEL:` label — populated from `RMN` response after connect
- `SN:` label — populated from `RSN` response after connect

*TIMING groupbox:*
- `HEL HB RX` — ms between received laser responses
- `DROPS` — TCP read error count

*Laser data groupbox:*
- `HK VOLTS` — housekeeping voltage V (3K only — `RHKPS`)
- `BUS VOLTS` — bus voltage V (3K only — `RBSTPS`)
- `CASE TEMP` — case temperature °C (`RCT`)
- `mb_hel_isEmOn` — emission on indicator (model-aware bit decode)
- `mb_hel_isNotReady` — not-ready indicator (model-aware bit decode)
- `SET POINT` progress bar — % (`RCS`/`SDC`/`SCS`)
- `POWER [W]` progress bar — W (`ROP`), maximum driven by sensed model

*Commands:*
- `EMON` / `EMOFF` buttons — gated on `IsSensed`
- Power spinner (0–100%) + `SET` button — sends `SCS` (3K) or `SDC` (6K)
- `CLR` button — sends `RERR`

**Col 2 — center:**

*STA groupbox:* 8 `mb_` StatusLabel bit indicators + raw 32-bit binary. Labels are model-aware — updated once on sense by `UpdateModelLabels()`:

| mb_ control | 3K bit | 6K bit |
|-------------|--------|--------|
| `mb_sta_emission` | B0: EM ON | B2: EM ON |
| `mb_sta_overheat` | B16: HI TEMP | B1: OVHEAT |
| `mb_sta_notready` | B9: NOT RDY | B11: PSU OFF |
| `mb_sta_busvolts` | B20: BUS V | B25: PWR ERR |
| `mb_sta_extctrl` | B5: EXT CTL | B18: EXT CTRL |
| `mb_sta_error` | B10: ERROR | B19: PWR ERR |
| `mb_sta_crit` | B29: CRIT ERR | B29: CRIT ERR |
| `mb_sta_shutdown` | B31: EXT SHT | B30: FIBR BRK |

*ERR groupbox:* 8 `mb_` StatusLabel bit indicators + raw 32-bit binary. 3K only — on 6K, bit indicators are hidden and `lbl_err_6k_note` ("6K ERR bits not fully mapped — see raw word") is shown instead:

| mb_ control | 3K bit |
|-------------|--------|
| `mb_err_temp` | B0: CASE TEMP |
| `mb_err_busv` | B4: BUS VOLT |
| `mb_err_outpwr` | B6: OUT PWR |
| `mb_err_fuse` | B10: FIBR FUSE |
| `mb_err_optint` | B11: OPT INT |
| `mb_err_crit` | B13: CRIT ERR |
| `mb_err_extshut` | B15: EXT SHUT |
| `mb_err_overcurr` | B18: OVER CURR |

**Col 3 — right panel (330px):** Reserved — empty.

**Status strip (bottom):** `MODEL:` · `SN:` · date/time

#### Auto-Sense and Poll Loop

On connect, `hel.cs` sends `RMN\r` then `RSN\r` before starting the poll timer. The `RMN` response (e.g. `RMN: YLM-3000-SM-VV`) is parsed by `MSG_IPG.ParseDirect()` — the power field in the model name determines `LaserModel` (`3000`→`YLM_3K`, `6000`→`YLR_6K`, anything else→`UNKNOWN` + error logged).

Poll timer fires every 20 ms, gated on `IsSensed`. State machine (p1):

| p1 | Command | Condition |
|----|---------|-----------|
| 0 | `RHKPS\r` | 3K only |
| 1 | `RCT\r` | both |
| 2 | `STA\r` | both |
| 3 | `RMEC\r` | both |
| 4 | `RBSTPS\r` | 3K only |
| 5 | `RCS\r` | both |
| 6 | `ROP\r` | both → wrap to 0 |

All responses routed to `MSG_IPG.ParseDirect(cmd, payload)`.

#### IPG Command Coverage — 3K vs 6K

| Command | Purpose | 3K | 6K | Used |
|---------|---------|----|----|------|
| `RMODEL` | Model string (sense) | ✅ | ❌ empty | Sense — 3K path |
| `RMN` | Model name / hostname | hostname only | ✅ model | Sense — 6K path |
| `RSN` | Serial number | ✅ | ✅ | Connect |
| `RHKPS` | HK voltage V | ✅ | ❌ | Poll p0, 3K only |
| `RBSTPS` | Boost voltage V | ✅ | ❌ | Poll p4, 3K only |
| `RCT` | Temperature °C | ✅ | ✅ | Poll p1, both |
| `STA` | Status word 32-bit | ✅ | ✅ | Poll p2, both |
| `RMEC` | Error word 32-bit | ✅ | ✅ | Poll p3, both |
| `RCS` | Setpoint % ch1 | ✅ | ✅ | Poll p5, both |
| `ROP` | Output power W ch1 | ✅ | ✅ | Poll p6, both |
| `ROPS` | Output power W ch2 | ❌ | ✅ | Future |
| `SCS <pct>` | Set power % | ✅ | ❌ | On command |
| `SDC <pct>` | Set power % | ❌ | ✅ | On command |
| `EMON` / `EMOFF` / `RERR` | Control | ✅ | ✅ | On command |

#### STA Bit Decode — Model-Aware

| Our label | 3K bit | 6K bit |
|-----------|--------|--------|
| `mb_sta_emission` | B0: Emission ON | B2: Emission ON |
| `mb_sta_overheat` | B16: Hi Case Temp | B1: Overheating |
| `mb_sta_notready` | B9: Not Ready | B11: PSU OFF |
| `mb_sta_busvolts` | B20: Bus V fault | B25: PSU error |
| `mb_sta_extctrl` | B5: Ext ctrl enabled | B18: Ext ctrl enabled |
| `mb_sta_error` | B10: Error present | B19: PSU error |
| `mb_sta_crit` | B29: Critical error | B29: Critical error |
| `mb_sta_shutdown` | B31: Ext shutdown | B30: Fiber break |

---

### 4.7 TRC — Tracking and Range Computer

**Target:** 192.168.1.22 · A2 port 10018 (ICD) + port 5012 (ASCII)

> 🔲 **Section pending** — TRC form not yet built. TRC telemetry currently accessible via BDC pass-through (§4.4 Col 4). Direct A2 + ASCII window planned.

Key content when built:
- Camera status: VIS and MWIR — Started / Active / Capturing / Tracking / TrackValid
- Tracker state: TX/TY centre position, AT/FT offsets, focus score, NCC score
- Active camera selection, FPS, device temp, Jetson temp / CPU load
- Overlay mask bits (Reticle, TrackPreview, TrackBox, CueChevrons, AC_Proj, AC_Leaders, FocusScore, OSD)
- Vote bits readbacks: `voteBitsMcc`, `voteBitsBdc`
- ASCII command reference — port 5012, session 33:

| Command | Function |
|---------|----------|
| `TESTSRC CAM1\|CAM2 TEST\|LIVE` | Enable/disable test pattern |
| `TIMESRC NTP\|PTP\|SYS` | Set time source |
| `A1 ON\|OFF` | Enable/disable A1 stream to BDC |
| `TRACKER EN\|DIS` | Enable/disable tracker |
| `CAMSEL VIS\|MWIR` | Select active camera |

---

### 4.8 Upload FW — Firmware Deployment Tool

> 🔲 **Section pending** — tool exists, content to be written.

Covers: STM32 DFU flash (MCC, BDC, TMC, FMC), SAMD flash, Jetson binary deployment via SCP. Per-controller procedures, version verification steps.

---

### 4.9 NTP — NTP Message Snooper

> 🔲 **Section pending** — tool exists, content to be written.

Covers: NTP packet capture and decode, stratum display, offset monitoring, server response verification. Used to verify `.33` appliance and `.208` fallback are responding correctly before entering COMBAT.

---

## 5. Troubleshooting

> 🔲 **Section pending**

Planned content:

- **RX FROZEN** — controller not pushing: check `wantsUnsolicited` (disconnect/reconnect, confirm Unsolicited checkbox auto-checked), check network cable, check firmware is running (ping .IP)
- **DROPS counter incrementing** — connection instability: monitor MCC and TMC for same pattern (CONN-5), check switch, check firmware watchdog logs
- **dt MAX = 65535** — sub-controller absent (TRC/FMC not connected to BDC): expected, ignore. If BDC is connected: check A1 stream status via `A1 ON` serial command
- **NTP SYNCHED grey** — NTP appliance unreachable: verify `.33` is powered, check `dUTC` trend, use NTP snooper (§4.9)
- **PTP SYNCHED grey** — PTP grandmaster not advertising: verify GNSS lock on `.30`, check MCC PTP device status
- **Gimbal CONN red** — BDC not reaching Galil: check port 7778 traffic, check Galil power
- **BDC DROPS after ~600s** — known issue (CONN-5): cause under investigation, reconnect to restore stream

---

## 6. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-01 | IPG | Initial draft — §1–3 complete |
| 1.1.0 | 2026-04-06 | IPG | §4.1–4.4 added; placeholder §4.5–4.9, §5–6; action item table restructured with status tracking |
| 1.2.0 | 2026-04-07 | IPG | §4.3 TMC updated for V1/V2 hardware abstraction — description, temperature table (COMP1/2 added, VIC3/4 V1-only noted), AUX control table (PUMP2 V2, heater/speed V1-only). TMC-HW1–4 action items added (all closed). ICD ref updated to v3.3.9, ARCH ref to v3.3.3. |
| 1.3.0 | 2026-04-08 | IPG | MCC-HW1–4 action items added. MCC-HW1–3 closed (MSG_MCC.cs, mcc.cs, defines.cs). MCC-HW4 partial (compile errors fixed, ApplyHwRevLayout pending). ICD ref updated to v3.4.0, ARCH ref to v3.3.4. |

