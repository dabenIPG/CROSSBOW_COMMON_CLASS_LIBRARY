# CROSSBOW System Architecture

**Document Version:** 4.0.8
**Date:** 2026-04-25
**ICD Reference:** ICD v4.2.2
**Status:** CB-20260425 IP range gating removed fleet-wide.


> **Change history and open items:** All version changes, closed action items, and open item tracking
> are maintained exclusively in **`CROSSBOW_CHANGELOG.md`** (IPGD-0019).

---

## 1. System Overview

CROSSBOW is a ground-based directed-energy (HEL) tracking and fire-control system. It integrates
a gimbal-mounted dual-camera payload (VIS + MWIR), a fast steering mirror (FSM), a thermal
management system, a power management system, an operator HMI, and external cueing sources
(ADS-B, radar, LoRa) into a unified sensor-to-fire-control chain.

---

### 1.1 Hardware Architecture

CROSSBOW is organised as a set of Line Replaceable Units (LRUs). Each LRU has a dedicated
embedded controller and communicates over a common 1 Gbps Ethernet subnet.

#### LRUs

| LRU | Controller | Role |
|-----|-----------|------|
| **PMS** — Power Management System | MCC | System and safety controller. Manages battery, laser power supply, charger, GNSS, NTP/PTP time. Aggregates all fire control votes. |
| **TMS** — Thermal Management System | TMC | Maintains coolant temperature for the HEL thermal load. Controls pumps, LCM compressors, fans. |
| **GNSS** — NovAtel receiver | — | Provides BESTPOS/INS/heading to MCC. Acts as **PTP grandmaster** (IEEE 1588) for precision time. |
| **NTP Server** — Phoenix Contact FL Timeserver | — | HW Stratum 1 NTP primary, GPS-disciplined. All five controllers sync to `.33`. |
| **HEL** — IPG laser module | — | 3 kW (YLM-3000-SM-VV) or 6 kW (YLM-6000). TCP status embedded in MCC REG1. |
| **BDA** — Beam Director Assembly | BDC | Line-of-sight and fire controller. Contains three sub-assemblies (see below). |

#### BDA Sub-Assemblies

| Sub-Assembly | Contents | Controller |
|---|---|---|
| **Gimbal** | Pan/tilt servo drive (Galil) | BDC (via Galil ASCII UDP ports 7777/7778) |
| **COA** — Camera Optical Assembly | VIS camera (Allied Vision Alvium), MWIR camera, TRC (Jetson Orin NX) track controller | TRC |
| **LOA** — Laser Optical Assembly | FMC (FSM controller), fast steering mirror (FSM), focus stage (M3-LS), beam delivery optics | FMC |

---

### 1.2 FW Codebase Summary

Five embedded controllers run the CROSSBOW firmware. All share `defines.hpp`/`defines.cs` as the
canonical ICD constant source and `crc.hpp` for CRC-16/CCITT. See **§3.1** for full platform
detail and hardware revision tables.

| Controller | Platform | FW Version | Role |
|-----------|---------|-----------|------|
| **MCC** | STM32F7 (OpenCR) | 4.0.0 | Power, laser, GNSS, charger, fire vote aggregation, NTP/PTP |
| **BDC** | STM32F7 (OpenCR) | 4.0.0 | Gimbal PID, camera/tracker supervision, FSM control, fire control |
| **TMC** | STM32F7 (OpenCR) | 4.0.0 | Thermal — pumps, LCM compressors, Vicors, fans |
| **FMC** | STM32F7 (OpenCR) | 4.0.0 | FSM DAC/ADC, focus stage I2C |
| **TRC** | Jetson Orin NX / Linux 6.1 | 4.0.3 | Dual-camera capture, MOSSE tracking, COCO inference, H.264 encode |

---

### 1.3 SW Codebase Summary

Four Windows C# (.NET 8 / WinForms) applications comprise the CROSSBOW software suite.
They share a common class library (`namespace CROSSBOW`) for all message parsing.
See **§3.2** for namespace and transport detail, and **§4.1–4.4** for inter-application
interfaces and ICD governance.

| Application | Role | Audience |
|-------------|------|----------|
| **THEIA** | Operator HMI — video display, gimbal/FSM control, fire control, Xbox controller | Operator |
| **HYPERION** | Reference CUE source — sensor fusion (ADS-B, radar, LoRa), Kalman filter, track selection, EXT_OPS output | Sensor operator |
| **CROSSBOW_ENG_GUIS** | Engineering management suite — MDI shell with per-controller tabs, firmware programmer, NTP/PTP management | Engineering only |
| **CROSSBOW_EMPLACEMENT_GUIS** | Emplacement and test toolset — CUE SIM track injection, HyperionSniffer, emplacement workflow (HMI-A3-18 pending) | Engineering / commissioning |

---

## 2. Network Topology

All nodes communicate over a dedicated 1 Gbps Ethernet switch on subnet `192.168.1.x`.

**IP convention (not enforced in firmware — CB-20260425):**
- `.1–.99` — embedded controllers and engineering tools (A1/A2 internal traffic)
- `.100–.199` — reserved
- `.200–.254` — external clients: THEIA, HYPERION, integration clients (A3 external traffic)

---

### 2.1 IP Node Table

| Node | IP | Role |
|------|----|------|
| MCC | 192.168.1.10 | Master control — power, laser, GNSS, charger |
| TMC | 192.168.1.12 | Thermal management — coolant, fans, TEC |
| HEL (IPG laser) | 192.168.1.13 | Laser source (read-only, status embedded in MCC REG1) |
| BDC | 192.168.1.20 | Beam director — gimbal, cameras, FSM, MWIR, fire control |
| Gimbal (Galil) | 192.168.1.21 | Pan/tilt servo drive |
| TRC (Jetson Orin NX) | 192.168.1.22 | Camera capture, tracker, video encoder — role address shared by all TRC units. Only one live at a time. |
| FMC | 192.168.1.23 | FSM DAC/ADC, focus stage |
| GPS/GNSS (NovAtel) | 192.168.1.30 | Position/heading/INS (MCC managed) + PTP grandmaster (IEEE 1588, domain 0, UTC_TIME) |
| RPI/ADS-B | 192.168.1.31 | ADS-B decoder |
| LoRa | 192.168.1.32 | LoRa/MAVLink track input |
| NTP appliance | 192.168.1.33 | HW Stratum 1 NTP primary — all five controllers sync directly |
| RADAR | 192.168.1.34 | Radar track input |

Engineering laptops and ENG GUI PCs: `.1–.99` by convention.
SW clients (THEIA, HYPERION, ENG GUI, EMPLACEMENT_GUIS): `.200–.254` by convention for external/operator clients, `.1–.99` for engineering. IP range is not enforced in firmware — convention only.

---

### 2.2 NTP/PTP Time Architecture

| Source | IP | Type | Accuracy | Users |
|--------|-----|------|----------|-------|
| NovAtel GNSS | .30 | PTP grandmaster (IEEE 1588, PTP_UDP, multicast `224.0.1.129`, domain 0, 1 Hz sync, 2-step, UTC_TIME) | ~1–100 µs (SW timestamping) | MCC primary |
| Phoenix Contact FL Timeserver | .33 | NTP Stratum 1 (GPS-disciplined) | ~1–10 ms | All five controllers direct |
| Windows HMI (w32tm) | .208 | NTP fallback | ~10 ms | All five controllers automatic fallback after 3 misses |

PTP is MCC's primary time source; NTP is retained as a warm fallback. BDC, TMC, and FMC use NTP only — PTP deferred fleet-wide (FW-B3). TRC uses `systemd-timesyncd` NTP (see §2.5). `.8` must NOT be used as an NTP target.

> **IGMP snooping** must be OFF on the network switch for PTP multicast (`224.0.1.129`) to flow correctly.

---

### 2.3 Fleet Socket Budget

Authoritative reference for all embedded controllers. Verified from source files session 28. See per-controller sections (§7, §8, §9, §10) for full detail.

| Controller | PTP disabled (default) | PTP enabled | Spare (PTP disabled) | Notes |
|------------|----------------------|-------------|----------------------|-------|
| MCC | 6/8 | 8/8 | 2 | ptp.INIT() gated — FW-B3/FW-B4 |
| BDC | 5/8 | 7/8 | 3 | ptp.INIT() gated by isPTP_Enabled ✅ FW-B4 closed CB-20260412 — FW-B3 still pending fleet-wide |
| TMC | 4/8 | 4/8 | 4 | ptp.INIT() gated by isPTP_Enabled ✅ FW-B4 confirmed from TMC source (CB-20260425) |
| FMC | 2/8 | 4/8 | 6 | ptp.INIT() gated — FW-B3/FW-B4 |
| TRC | N/A | N/A | N/A | Linux kernel sockets — no W5500 hardware limit |

**Shared socket pattern (authoritative):**
- NTP uses `&udpA2` on all four embedded controllers — zero additional sockets
- BDC TRC/FMC command TX borrows `&udpA2` — zero additional sockets
- `isPTP_Enabled` gates `ptp.UPDATE()` on all controllers
- `ptp.INIT()` gated by `isPTP_Enabled` on all four embedded controllers — confirmed CB-20260425

See per-controller sections (§7, §8, §9, §10) for full socket detail tables.

---

### 2.4 TRC Timing — NTP Configuration

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

For full TRC/Jetson setup procedure (OS install, static IP, software deployment), see **JETSON_SETUP.md v2.2.0** (DOC-2).

---

---

## 3. Codebase Inventory

---

### 3.1 FW Codebase

| Controller | Platform | Language | Role |
|-----------|---------|----------|------|
| **MCC** | STM32F7 (OpenCR board library) | Arduino C++ | Power management, laser (IPG TCP), GNSS (NovAtel UDP), charger (I2C/GPIO), fire vote aggregation, NTP/PTP. V1/V2/V3 hardware abstraction (`hw_rev.hpp`). |
| **BDC** | STM32F7 (OpenCR board library) | Arduino C++ | Gimbal (Galil ASCII UDP), camera/tracker supervision, FSM/focus stage coordination, MWIR, Fuji lens, inclinometer, fire control. V1/V2 hardware abstraction. |
| **TMC** | STM32F7 (OpenCR board library) | Arduino C++ | Thermal management — pump, LCM, Vicor, fans, TPH sensor. V1/V2 hardware abstraction. |
| **FMC** | STM32F7 (OpenCR board library) | Arduino C++ | FSM SPI DAC/ADC, M3-LS focus stage I2C. V1/V2 hardware abstraction. |
| **TRC** | Jetson Orin NX / JetPack 6.2.2 | C++17 / GStreamer | Dual-camera capture (VIS/MWIR), MOSSE tracking, LK optical flow, COCO SSD MobileNet V3 inference, H.264 encode, UDP telemetry. |

#### Hardware Revision and Build Configuration

All four embedded controllers share a unified hardware abstraction via `hw_rev.hpp`. The active
revision is compile-time selected (`HW_REV_V1`/`V2`/`V3`) and self-reported in REG1 at boot.
Read the `HW_REV` register byte before interpreting `HEALTH_BITS` and `POWER_BITS`.
The table below combines the quick reference and `hw_rev.hpp` build configuration for all controllers.

| Controller | HW_REV Byte | V1 | V2 | V3 | Key Differences | ICD Breaking Change |
|-----------|------------|----|----|----|----|---------------------|
| **MCC** | REG1 [254] | STM32F7 · relay bus Vicor (A0 LOW=ON), solenoids, GPS relay, I2C charger | STM32F7 · VICOR_GIM (A0 HIGH=ON) + VICOR_TMS (pin 20 HIGH=ON), no solenoids, no relay bank, GPIO charger | STM32F7 · VICOR_BUS (pin 40 HIGH=ON), solenoids restored (pin 50), three relays (GPS/NTP/LASER), I2C charger; `LASER_6K` adds VICOR_GIM (pin 55) + VICOR_TMS (pin 51) | V2: solenoids/relay bank retired, Vicor polarity inverted, pin reuse (A0/pin20/pin83); V3: dedicated pins, relay bank restored, RELAY_NTP added, `LASER_3K`/`LASER_6K` compile axis | `HEALTH_BITS`/`POWER_BITS` rename — ICD v3.4.0; V3 `HW_REV_BYTE 0x03` — ICD vTBD |
| **BDC** | REG1 [392] | STM32F7 · Vicor NC opto LOW=ON, GPIO 0 thermistor | STM32F7 · Vicor HIGH=ON, GPIO 20 thermistor, 3 new NTCs, IP175 switch | — | Vicor PSU polarity inverted (NC opto LOW=ON → non-inverted HIGH=ON); Vicor thermistor pin moved (GPIO 0→20); three new NTC thermistors added (RELAY GPIO 19, BAT GPIO 18, USB GPIO 16); IP175 5-port Ethernet switch added (RESET GPIO 52, DISABLE GPIO 64); unused DIG2 (GPIO 42) removed | `HEALTH_BITS`/`POWER_BITS` rename; `isSwitchEnabled` HEALTH_BITS bit 1 (V2 only) — ICD v3.5.1 |
| **TMC** | REG1 [62] | STM32F7 · Vicor pump, ADS1015, heater | STM32F7 · TRACO pumps, direct MCU analog, no heater | — | Single Vicor pump (DAC speed control) replaced by two independent TRACO DC-DCs (on/off only, per-pump); heater subsystem (Vicor + DAC) removed; two ADS1015 external ADCs (8 aux temp channels) removed — replaced by direct MCU analog inputs; total Vicors reduced 4→2 (LCM only); PSU inhibit opto polarity flipped (NO CTRL_ON=LOW → NC CTRL_ON=HIGH); `tv3`/`tv4` temp channels V1-only (0x00 on V2) | None breaking — unified in session 30 |
| **FMC** | REG1 [45] | SAMD21 (MKR) · legacy | STM32F7 (OpenCR) · current | — | **Platform change** — SAMD21 → STM32F7; serial abstracted (`SerialUSB`→`Serial` via `FMC_SERIAL`); SPI bus abstracted (`SPI`→`SPI_IMU` via `FMC_SPI`); BME280 ambient TPH (Temp/Pressure/Humidity) live on V2 (REG1 [47–58]); V1 TPH bytes always 0x00; NTP unconditional on V2 (SAMD21 `SerialUSB` blocking bug not applicable on STM32) | `HEALTH_BITS` [7] / `POWER_BITS` [46] promoted from RESERVED; `isFSM_Powered`/`isStageEnabled` moved from HEALTH_BITS to POWER_BITS — ICD v3.5.2 |

#### Revision Detection — C# Pattern

```csharp
// All controllers: read HW_REV byte first; gate V2-only field reads on IsV2
bool IsV1  = (HW_REV == 0x01);
bool IsV2  = (HW_REV == 0x02);
bool IsV3  = (HW_REV == 0x03);

// MCC — byte [254]
bool mccIsV2 = msgMcc.IsV2;   // gates: HEALTH_BITS[9], POWER_BITS[10] V2-only bits (VICOR_GIM/TMS)
bool mccIsV3 = msgMcc.IsV3;   // gates: RELAY_NTP bit 7, V3 pin map, LASER_xK model-aware paths

// BDC — byte [392]
bool bdcIsV2 = msgBdc.IsV2;   // gates: isSwitchEnabled (HEALTH_BITS bit 1), TEMP_RELAY/BAT/USB [393-395]

// TMC — byte [62]
bool tmcIsV2 = msgTmc.IsV2;   // gates: tv3/tv4 channels (always 0x00 on V2)

// FMC — byte [45]
bool fmcIsV2 = msgFmc.IsV2;   // gates: POWER_BITS [46], TPH fields [47-58]
```

#### `hw_rev.hpp` Compile-Time Defines

| Define | MCC | BDC | TMC | FMC |
|--------|-----|-----|-----|-----|
| `HW_REV_V1` | V1 — relay bus Vicor (A0 LOW=ON), solenoids, GPS relay, I2C charger | V1 — Vicor NC opto LOW=ON, GPIO 0 thermistor | V1 — Vicor pump, ADS1015, heater | V1 — SAMD21/MKR layout |
| `HW_REV_V2` | V2 — VICOR_GIM (A0 HIGH=ON) + VICOR_TMS (pin 20 HIGH=ON), no solenoids, no relay bank, GPIO charger | V2 — Vicor HIGH=ON, GPIO 20 thermistor, 3 new NTCs, IP175 | V2 — TRACO pumps, direct MCU analog, no heater | V2 — STM32F7/OpenCR layout |
| `HW_REV_V3` | V3 — VICOR_BUS (pin 40 HIGH=ON), solenoids restored (SOL_BDA pin 50), three relays (GPS/NTP/LASER), I2C charger; `LASER_6K` adds VICOR_GIM (pin 55) + VICOR_TMS (pin 51) external | — | — | — |
| HW_REV byte | `MCC_HW_REV_BYTE` → REG1 [254] · `0x01`=V1, `0x02`=V2, `0x03`=V3 | `BDC_HW_REV_BYTE` → REG1 [392] | `TMC_HW_REV_BYTE` → REG1 [62] | `FMC_HW_REV_BYTE` → REG1 [45] |
| Polarity macro | `POL_VICOR_BUS_ON=LOW`(V1) / `POL_PWR_GIM_ON=HIGH`(V2) / `POL_VICOR_BUS_ON=HIGH`(V3) | `POL_VICOR_ON/OFF` | `CTRL_ON/CTRL_OFF` | `FSM_POW_ON/OFF` |
| Serial | — | — | — | `FMC_SERIAL` (`SerialUSB`↔`Serial`) |
| SPI | — | — | — | `FMC_SPI` (`SPI`↔`SPI_IMU`) |

> ⚠️ **Bring-up note (MCC V2):** `POL_PWR_GIM_ON = HIGH` and `POL_PWR_TMS_ON = HIGH` are
> confirmed on hardware 2026-04-08.

> ⚠️ **Bring-up note (MCC V3 VICOR_BUS):** `POL_VICOR_BUS_ON = HIGH` on V3 — polarity inverted
> vs V1 (was LOW=ON). Use `POL_VICOR_BUS_ON` / `POL_VICOR_BUS_OFF` macros from `hw_rev.hpp`;
> never write literal `HIGH`/`LOW` to the VICOR_BUS pin.

> ⚠️ **BDC Vicor polarity:** V1 NC opto (LOW=ON, safe-off=HIGH). V2 non-inverted (HIGH=ON,
> safe-off=LOW). Boot-safe state uses `POL_VICOR_OFF` — never write literal `HIGH`/`LOW` to
> `PIN_VICOR1_ENABLE` in `BDC.ino`.

---

---


---

### 3.2 SW Codebase

| Application | Platform | Namespace | Transport | Controllers | Entry Point |
|-------------|---------|-----------|-----------|-------------|-------------|
| **THEIA** | Windows / .NET 8 / WinForms | `CROSSBOW` | A3 / port 10050 / magic `0xCB 0x58` | MCC, BDC only | `Parse(data)` → internal `ParseA3` |
| **CROSSBOW_ENG_GUIS** | Windows / .NET 8 / WinForms | `CROSSBOW_ENG_GUIS` (shell) / `CROSSBOW` (lib) | A2 / port 10018 / magic `0xCB 0x49` | All 5 controllers | `Parse(data)` → internal `ParseA2` |
| **HYPERION** | Windows / .NET 8 / WinForms | `Hyperion` | EXT_OPS UDP:15009 (CUE output to THEIA) | External sensors only | N/A — separate stack |
| **CROSSBOW_EMPLACEMENT_GUIS** | Windows / .NET 8 / WinForms | `CROSSBOW_EMPLACEMENT_GUIS` | EXT_OPS UDP:15001/15009 | HYPERION / THEIA | CUE SIM injection + HyperionSniffer |
| **CROSSBOW lib** | Shared | `CROSSBOW` | — | — | Shared class library — MSG_MCC, MSG_BDC and all sub-message parsers. Used by THEIA and CROSSBOW_ENG_GUIS. |

#### Application Descriptions

**THEIA** — Operator HMI. Connects to MCC and BDC via A3/INT_OPS (port 10050). Receives CUE
from any conforming EXT_OPS source on UDP:15009. Sub-controller data (TMC, FMC, TRC) arrives
embedded in MCC and BDC REG1 payloads — THEIA never communicates with them directly.
See **§11** for class structure, Xbox mapping, and fire control chain.

**HYPERION** — Reference CUE source. Ingests tracks from ADS-B (dump1090 TCP:30002), Echodyne
radar (TCP:29982), generic UDP:15001, LoRa/MAVLink (UDP:15002), and Stellarium (HTTP:8090).
Applies a 6-state NED Kalman filter per track. Operator selects a track; HYPERION transmits a
71-byte EXT_OPS CUE packet to THEIA on UDP:15009. Replaceable by any conforming EXT_OPS source.
See **§5.4** for architecture diagram and engagement sequence.

**CROSSBOW_ENG_GUIS** — Engineering management suite. MDI shell (`frmCROSSBOW_ENG`) with child
forms: `frmMCC`, `frmBDC`, `frmTMC`, `frmFMC`, `frmTRC` (controller GUIs); `frmHEL` (laser
TCP); `frmNTP_PTP` (time source management); `frmFWProgrammer`. Engineering-only — not present
in operational configuration. See **§4.11** for per-controller access detail.

**CROSSBOW_EMPLACEMENT_GUIS** — Emplacement and test toolset. Current scope: CUE SIM (EXT_OPS
track injection to HYPERION UDP:15001 or direct to THEIA UDP:15009) and HyperionSniffer.
Intended scope (HMI-A3-18 pending): KIZ/LCH/horizon file loading and upload via A3.
File format specs tracked under DOC-3.

---

## 4. Communications Architecture

This section is the authoritative reference for all transport paths, wire protocol, and ICD
governance. Controllers are accessed via three transport layers (A1/A2/A3) described in §4.6–4.8.

---

### 4.1 Inter-Application Interfaces

| From | To | Transport | Port | Magic | ICD | Notes |
|------|----|-----------|------|-------|-----|-------|
| THEIA | MCC | UDP / A3 | 10050 | `0xCB 0x58` | INT_OPS (IPGD-0004) | System state, laser, GNSS, fire vote |
| THEIA | BDC | UDP / A3 | 10050 | `0xCB 0x58` | INT_OPS (IPGD-0004) | Gimbal, camera, FSM, PID, fire control |
| MCC | THEIA | UDP / A3 | 10050 | `0xCB 0x58` | INT_OPS (IPGD-0004) | MCC REG1 unsolicited 100 Hz |
| BDC | THEIA | UDP / A3 | 10050 | `0xCB 0x58` | INT_OPS (IPGD-0004) | BDC REG1 unsolicited 100 Hz (includes TRC/FMC/Gimbal embedded) |
| HYPERION | THEIA | UDP / EXT_OPS | 15009 | `0xCB 0x48` | EXT_OPS (IPGD-0005) | 71B CUE packet, CMD 0xAA |
| CUE SIM | THEIA | UDP / EXT_OPS | 15009 | `0xCB 0x48` | EXT_OPS (IPGD-0005) | Direct inject, bypasses HYPERION |
| CUE SIM | HYPERION | UDP / EXT_OPS | 15001 | `0xCB 0x48` | EXT_OPS (IPGD-0005) | Simulated track injection |
| ENG GUI | All 5 controllers | UDP / A2 | 10018 | `0xCB 0x49` | INT_ENG (IPGD-0003) | Bidirectional engineering access |
| TRC | THEIA | RTP/UDP | 5000 | — | — | H.264 video unicast |

---

### 4.2 ICD Governance

| ICD | IPGD | Scope Label | Governs | Audience |
|-----|------|-------------|---------|----------|
| `CROSSBOW_ICD_INT_OPS.md` | IPGD-0004 | `INT_OPS` | THEIA ↔ MCC/BDC via A3 (port 10050) | Operators and system integrators |
| `CROSSBOW_ICD_INT_ENG.md` | IPGD-0003 | `INT_ENG` | CROSSBOW_ENG_GUIS ↔ all controllers via A2 (port 10018) | Engineering only |
| `CROSSBOW_ICD_EXT_OPS.md` | IPGD-0005 | `EXT_OPS` | HYPERION / CUE SIM ↔ THEIA (UDP:15009) | External integrators |

> Sub-controllers (TMC, FMC, TRC) have no A3 listener and are not reachable from the external zone. THEIA never communicates with them directly.

---

### 4.3 Transport Summary

**Client access (software → controller):**

| Client | Transport | Port | Magic | Controllers Accessible | C# Entry Point |
|--------|-----------|------|-------|------------------------|----------------|
| **THEIA (HMI)** | A3 External | 10050 | `0xCB 0x58` | MCC, BDC **only** | `ParseA3(byte[] frame)` |
| **CROSSBOW_ENG_GUIS** | A2 Internal | 10018 | `0xCB 0x49` | MCC, BDC, TMC, FMC, TRC | `ParseA2(byte[] msg)` |

Sub-controllers (TMC, FMC, TRC) have **no A3 listener** — they are unreachable from the
external zone. THEIA never communicates with TMC, FMC, or TRC directly.
Controller-to-controller A1 streams are described in **§4.6**.

---

### 4.4 Frame Geometry

```
[0-1]     MAGIC_HI / MAGIC_LO
[2]       SEQ_NUM    uint8  — server rolling counter
[3]       CMD_BYTE   uint8  — ICD command byte
[4]       STATUS     uint8  — 0x00 = OK; non-zero = error
[5-6]     PAYLOAD_LEN uint16 LE — always 0x0200 (512) for REG1
[7-518]   PAYLOAD    512 bytes — register data, zero-padded
[519-520] CRC-16     uint16 BE — CRC-16/CCITT over bytes [0..518]
```

**Request frame (variable length):**

```
[0-1]   MAGIC_HI / MAGIC_LO
[2]     SEQ_NUM     uint8
[3]     CMD_BYTE    uint8
[4-5]   PAYLOAD_LEN uint16 LE — 0 for no-payload commands
[6+]    PAYLOAD     (PAYLOAD_LEN bytes)
[last-2] CRC-16     uint16 BE — over all bytes before CRC field
```

Minimum request frame (no payload): 8 bytes.


---

### 4.5 CRC-16/CCITT

Poly=0x1021, init=0xFFFF, no reflection, BE wire order.
Known-answer: `crc16("123456789", 9) == 0x29B1`
Shared implementation: `crc.hpp` (all embedded controllers). Runtime-generated table —
verified correct on STM32, SAMD21, Arduino, and x86-64.

> ⚠ **CRC cross-platform verification note:** Past integration issues were observed between
> the STM32 implementation and Linux/x86 implementations. Before first HW integration, perform
> a full end-to-end CRC verification across all five controllers and both C# applications using
> the known-answer test above. Do not assume correctness from unit tests alone — verify with
> live framed packets on the wire. Log as a pre-HW-test checklist item.


---

### 4.6 A1 — Internal Unsolicited (Always-On Stream)

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

**A1 ARP backoff (session 36, revised CB-20260425):** Non-blocking — `endPacket()` on W5500 returns immediately on ARP miss (drops packet, returns 0). Backoff purpose is to limit SPI bus traffic when peer is offline, not to prevent blocking. After `A1_FAIL_MAX = 3` consecutive send failures, the A1 send is suppressed for `A1_BACKOFF_TICKS` cycles (~2 s at the controller's stream rate). Recovery is instant — first successful send clears both counters. Gate uses the device *enabled* flag (not the ready flag); peer-ready flag set exclusively by `endPacket()` result. Serial command `A1 ON|OFF` allows disabling the A1 stream for bench testing. TRC (Jetson/Linux) requires no backoff — Linux kernel handles ARP asynchronously.


---

### 4.7 A2 — Internal Engineering (Bidirectional, All Controllers)

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
Start() → FRAME_KEEPALIVE {}         (single registration frame — burst retired, firmware replay fix handles reconnects)
KeepaliveLoop() → FRAME_KEEPALIVE {} every 30 s  (maintain slot liveness)
```
Registration burst (`0xA4 ×3`) retired — see §4.9 for authoritative standard. `SET_UNSOLICITED` is user-controlled via checkbox, not sent automatically on connect.

ENG GUI is the primary A2 client. BDC also uses A2 to issue commands to TRC.


---

### 4.8 A3 — External (MCC and BDC Only)

THEIA connects here. CMD_BYTE whitelist (`EXT_CMDS[]`) enforced on all received frames.
Up to **2 simultaneous external clients** per controller (MCC and BDC independently).
Same `0xA0` registration / 60-second liveness model as A2.


---

### 4.9 C# Connect Sequence

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


---

### 4.10 ParseA3 vs ParseA2

`ParseA3` validates the full 521-byte A3 frame (magic `0xCB 0x58`, CRC-16, STATUS byte) before
dispatching to `ParseMSG01`. It sets `LastFrameStatus` on every call regardless of STATUS value.

`ParseA2` receives a raw 512-byte payload with the frame header already stripped upstream. It
performs no magic or CRC validation — the A2 transport layer handles that. Liveness update
(`RX_HB` / `lastMsgRx`) is applied at the top of `ParseA2` unconditionally.

Both entry points are present on `MSG_MCC` and `MSG_BDC`. THEIA must call `ParseA3`.
ENG GUI must call `ParseA2`. Cross-wiring these will silently produce wrong results.


---

### 4.11 TransportPath Enum

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


---

### 4.12 ENG GUI — Per-Controller Access

CROSSBOW_ENG_GUIS connects to each of the five controllers independently on A2 port 10018.
Each controller has its own message class instance:

| Controller | IP | Port | C# Class | Entry Point |
|------------|----|------|----------|-------------|
| MCC | 192.168.1.10 | 10018 | `MSG_MCC` | `Parse()` → internal `ParseA2` |
| BDC | 192.168.1.20 | 10018 | `MSG_BDC` | `Parse()` → internal `ParseA2` |
| TMC | 192.168.1.12 | 10018 | `MSG_TMC` | `ParseA2` |
| FMC | 192.168.1.23 | 10018 | `MSG_FMC` | `ParseA2` |
| TRC | 192.168.1.22 | 10018 | `MSG_TRC` | `ParseA2` |


---

### 4.13 THEIA — Per-Controller Access

THEIA connects to MCC and BDC on A3 only:

| Controller | IP | Port | C# Class | Entry Point |
|------------|----|------|----------|-------------|
| MCC | 192.168.1.10 | 10050 | `MSG_MCC` | `Parse()` → internal `ParseA3` |
| BDC | 192.168.1.20 | 10050 | `MSG_BDC` | `Parse()` → internal `ParseA3` |

---


---

## 5. Data Flows

### 5.1 Video

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

### 5.2 Commands (THEIA → Subsystems)

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

### 5.3 Telemetry (Unsolicited → THEIA)

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

### 5.4 External Cueing — CUE Source → THEIA → BDC

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
| **CROSSBOW_ENG_GUIS** | `CROSSBOW_ENG_GUIS` | IPG CROSSBOW MANAGEMENT SUITE — MDI engineering GUI. Child forms: `frmMCC`, `frmBDC`, `frmTMC`, `frmFMC`, `frmTRC`, `frmHEL`, `frmNTP_PTP`, `frmFWProgrammer` | All controllers via A2 |

HYPERION and THEIA can run on the same PC but are typically on separate machines in
deployment. CROSSBOW_ENG_GUIS is engineering-only and not present in the operational configuration.

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
   Left + Right trigger → 0xAB SET_FIRE_REQUESTED_VOTE → MCC → vote aggregation → laser
```

Xbox controller is the THEIA operator's primary input at steps 4–7. HYPERION and THEIA
may be operated by one person or two depending on the engagement scenario.

---


---

## 6. TRC Internal Architecture

### 6.1 Pipeline

```
AlviumCamera (VIS, 60 Hz) / MWIRCamera (MWIR, 30 Hz)
  └── CameraBase (abstract)
        └── Lock-free triple buffer (FrameSlot)
              └── Compositor (60 Hz)
                    ├── Overlay rendering (reticle, track box, HUD, OSD, chevrons)
                    ├── ViewMode: CAM1 | CAM2 | PIP4 | PIP8
                    └── nvv4l2h264enc → rtph264pay → udpsink port 5000
```

### 6.2 Thread Architecture

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
| stats | statsThreadFunc | 1 Hz | Jetson temp (30s), CPU+GPU load (1Hz, complementary filtered) |

### 6.3 Tracker Architecture

```
CameraBase
  └── TrackerWrapper (MOSSE = TrackB)
        ├── TrackA: AI/DNN — not yet implemented
        ├── TrackB: MOSSE — implemented, primary operational tracker
        ├── TrackC: Centroid — not yet implemented
        └── Kalman: not yet implemented
```

Tracker enable/disable per-ID via `0xDB ORIN_ACAM_ENABLE_TRACKERS`.

### 6.4 TRC REG1 Telemetry Packet (64 bytes, #pragma pack(push,1))

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
| 45 | 1 | uint8 | jetsonTemp | Jetson CPU temp °C — `/sys/devices/virtual/thermal/thermal_zone0/temp` ÷ 1000 — v4.2.0 (was int16 [45–46]) |
| 46 | 1 | uint8 | jetsonCpuLoad | Jetson CPU load % — `/proc/stat` delta — v4.2.0 (was int16 [47–48]) |
| 47 | 1 | uint8 | jetsonGpuLoad | Jetson GPU load % — `/sys/devices/platform/gpu.0/load` ÷ 10 — v4.2.0 (moved from [57–58]) |
| 48 | 1 | uint8 | jetsonGpuTemp | Jetson GPU temp °C — `/sys/devices/virtual/thermal/thermal_zone1/temp` ÷ 1000, polled 5s — v4.2.0 (NEW) |
| 49 | 8 | uint64 | som_serial | Jetson SOM serial — `/proc/device-tree/serial-number` — v4.0.0 |
| 57 | 7 | uint8[] | RESERVED | 0x00 |

**Defined: 57 bytes. Reserved: 7 bytes. Fixed block: 64 bytes.**

**BDC embedding:** TRC REG1 occupies bytes [60–123] of BDC REG1 payload (64-byte fixed block).

### 6.5 CamStatus / TrackStatus Bits

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

### 6.6 Startup

```bash
./trc --dest-host 192.168.1.208 --osd ON --focusscore ON --coco-ambient
```

**Launch flags:**

| Flag | Default | Description |
|------|---------|-------------|
| `--dest-host <ip>` | 192.168.1.1 | Video stream destination (port 5000, H.264 RTP). Required. |
| `--osd <ON\|OFF>` | ON | OSD text overlay on boot. |
| `--focusscore <ON\|OFF>` | OFF | Focus score overlay on boot. |
| `--coco-ambient` | off | Load COCO model + enable ambient scan after compositor starts (500ms settle). Non-fatal on model load failure. |
| `--bitrate <1-50>` | 10 | H.264 encoder bitrate Mbps. |
| `--view <CAM1\|CAM2\|PIP\|PIP8>` | CAM1 | Default compositor view. |
| `--debug` | off | Enable debug logging from boot. |
| `--mwir-live` | off | Start MWIR in live mode (default test pattern). |

`--dest-host` sets the video stream destination. Telemetry auto-targets BDC (`192.168.1.20`) at boot regardless of `--dest-host`.

### 6.7 COCO Inference Architecture

SSD MobileNet V3 Large (80-class COCO). CUDA FP16 backend on Orin, CPU fallback.

```
Compositor (60 Hz)
  ├── Ambient push (full frame, every Nth frame when tracker off or interleaved)
  └── Track push  (MOSSE bbox crop, every Nth frame when tracking)
        └── CocoDetector (inference thread, single-slot non-blocking)
              ├── net_->detect() with runtime NMS threshold (default 0.35)
              ├── Area filter: minAreaFrac (default 0.002) × maxAreaFrac (default 0.50)
              └── CocoResult → pollResult() → compositor → OSD + telemetry
```

**Modes:**
- **Ambient** — full-frame scan, independent of tracker. Detections latched for OSD display and NEXT/PREV operator cycle. Enabled via `COCO AMBIENT ON` or `--coco-ambient` flag.
- **Track** — intra-box crop inference. Drift computed vs crop centre. Enabled via `COCO TRACK ON` (requires tracker active).

**Runtime tunables (ASCII):** `COCO NMS`, `COCO MINAREA`, `COCO MAXAREA`, `COCO DRIFT`, `COCO INTERVAL`.

### 6.8 OSD Layout

**Left column (top-down):** camera name/source, view mode, exposure, gain, ICT, gamma, FPS, DT, device temp, AMR, TRACK state + tx/ty + AT offset, COCO rows (3, model-loaded only).

**Top-right fixed-width block:** STATE / MODE / MCC / BDC — colour-coded by operational state. Anchor computed from `getTextSize("STATE: COMBAT ")` — column never shifts.

**Bottom-right:** SOM serial (dim), `JTEMP: N°C  JCPU: N%  JGPU: N%`.

---


### 6.9 Fire State HUD

TRC receives `0xE0 SET_BCAST_FIRECONTROL_STATUS` on A1 port 10019 (raw 5-byte, no frame
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


---

## 7. MCC Internal Architecture

MCC runs on Arduino/STM32F7, FW v3.3.0, IP: 192.168.1.10.

Hardware revision is selected at compile time in `hw_rev.hpp` (`HW_REV_V1`, `HW_REV_V2`, or `HW_REV_V3`). A second compile-time axis `LASER_3K` / `LASER_6K` selects the laser model and gates power pin dispatch in `EnablePower()` — the runtime `ipg.laserModel` sense is a verification check only. The active revision is reported in REG1 byte [254] (`HW_REV`) so `MSG_MCC.cs` can self-detect the register layout. Read byte [254] before interpreting `HEALTH_BITS` [9] and `POWER_BITS` [10].

### 7.1 Role

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

| Subsystem | V1 · 48V · 3kW | V2 · 300V · 6kW | V3 · 48V or 300V · 3kW or 6kW |
|-----------|----------------|-----------------|-------------------------------|
| Battery | 48V | 300V | 48V (3kW) or 300V (6kW) |
| STM32 input | 48V direct | 300V→48V via VICOR_GIM/TMS (unmanaged) | 48V direct (3kW) or 300V→48V via VICOR_TMS PC-always-open (6kW) |
| Solenoids | `SOL_HEL` pin 5 (laser HV bus) + `SOL_BDA` pin 8 (gimbal) | **Retired** | `SOL_HEL` pin 5 (3kW only) + `SOL_BDA` pin 50 (3kW only) |
| Relay bus Vicor | `VICOR_BUS` A0 LOW=ON · 48V→24V | **None** — no relay bank | `VICOR_BUS` pin 40 HIGH=ON · 48V→24V (3kW) or fed from VICOR_TMS 48V pickoff (6kW) — PC always open |
| VICOR_GIM | None | A0 NC HIGH=ON · 300V→48V gimbal ⚠️ reuses V1 VICOR_BUS pin | pin 55 HIGH=ON · 300V→48V gimbal (6kW only) |
| VICOR_TMS | None | pin 20 NC HIGH=ON · 300V→48V TMS ⚠️ reuses V1 RELAY_LASER pin | pin 51 HIGH=ON · 300V→48V · feeds MCC board + TMC header passthrough (6kW) · PC always open at boot |
| RELAY_GPS | pin 83 NO HIGH=ON → GPS 24V | **None** — GPS via unmanaged Vicor 300V→24V | pin 67 NO HIGH=ON → GPS 24V |
| RELAY_NTP | **None** — NTP appliance direct powered, no relay | **None** — NTP via unmanaged Vicor 300V→24V | pin 56 NO HIGH=ON → Phoenix NTP appliance 24V — **new V3** |
| RELAY_LASER | pin 20 NO HIGH=ON · 3kW digital bus 24V | pin 83 NO HIGH=ON · 6kW digital enable ⚠️ reuses V1 RELAY_GPS pin | pin 54 NO HIGH=ON · 3kW digital bus 24V (3kW) **or** pin 63 `PIN_ENERGIZE` NO opto · 5V out → ext PSU enable (6kW) |
| Charger enable | GPIO pin 6 · DBU3200 I2C CC/CV | GPIO pin 82 · GPIO only — no I2C | GPIO pin 6 · DBU3200 I2C CC/CV (same as V1) |
| Charger mode | pin 82 (legacy, unused) | **Retired** | pin 65 (legacy, unused) |
| `isBDC_Ready` source | Set by `SOL_BDA` in StateManager | Set by `EnablePower(VICOR_GIM)` in StateManager | Set by `SOL_BDA` in StateManager (3kW) or `EnablePower(VICOR_GIM)` (6kW) |

> ⚠️ **V2 pin-reuse note:** V2 reused three V1 pins with polarity changes to accommodate 300V architecture: A0 (VICOR_BUS→VICOR_GIM), pin 20 (RELAY_LASER→VICOR_TMS), pin 83 (RELAY_GPS→RELAY_LASER). V3 uses dedicated pins for all outputs — no reuse.

### 7.1a PMS Power Flow — Per Hardware Revision

The PMS (Power Management System) connects directly to the battery on all revisions. The MCC acts as the PDU controller, switching power to all downstream accessories via `EnablePower()`. Compile-time defines `HW_REV_Vx` and `LASER_xK` together fully determine the pin map and power sequence. `LASER_3K` always implies 48V battery; `LASER_6K` always implies 300V battery (internal or external PSU) — these are physically coupled. A mismatch is a hardware configuration error; the runtime `ipg.laserModel` sense provides fault detection only.

**V1 — 48V battery · 3kW laser** (`-DHW_REV_V1 -DLASER_3K`)
```
48V bat → PMS:
  SOL_HEL  (pin 5,  HIGH=ON) → 3kW laser HV bus 48V
  SOL_BDA  (pin 8,  HIGH=ON) → gimbal 48V              [sets isBDC_Ready]
  TMC                        → 48V passthrough · no switch
  NTP appliance              → 48V direct · no switch · no relay

48V bat → VICOR_BUS (A0, LOW=ON) → 24V relay bus:
  RELAY_GPS   (pin 83, HIGH=ON) → GPS appliance 24V
  RELAY_LASER (pin 20, HIGH=ON) → 3kW laser digital bus 24V
```

**V2 — 300V battery · 6kW laser** (`-DHW_REV_V2 -DLASER_6K`)
```
300V bat → PMS:
  VICOR_TMS (pin 20, NC HIGH=ON) → 300V→48V → TMC         [⚠ pin was V1 RELAY_LASER]
  VICOR_GIM (A0,    NC HIGH=ON) → 300V→48V → gimbal       [⚠ pin was V1 VICOR_BUS]
                                                            [sets isBDC_Ready]
  GPS + NTP → unmanaged Vicor 300V→24V direct · no firmware switch
  No relay bank on V2

300V bat → busbar direct → 6kW laser HV:
  RELAY_LASER (pin 83, HIGH=ON) → 6kW laser digital enable [⚠ pin was V1 RELAY_GPS]
```

**V3 — 48V battery · 3kW laser** (`-DHW_REV_V3 -DLASER_3K`)
```
48V bat → PMS (monolithic PCB):
  SOL_HEL  (pin 5,  HIGH=ON) → 3kW laser HV bus 48V
  SOL_BDA  (pin 50, HIGH=ON) → gimbal 48V              [sets isBDC_Ready]
  TMC                        → 48V passthrough via MCC board header · no switch

48V bat → onboard VICOR_BUS (pin 40, HIGH=ON) → 24V relay bus:
  RELAY_GPS   (pin 67, HIGH=ON) → GPS appliance 24V
  RELAY_NTP   (pin 56, HIGH=ON) → Phoenix NTP appliance 24V    [new V3]
  RELAY_LASER (pin 54, HIGH=ON) → 3kW laser digital bus 24V

Note: net label ARD_D40_LSR_PWR_EN on pin 40 is misleading —
pin 40 is the relay bus Vicor enable, not a laser signal.
```

**V3\* — 300V battery · 6kW laser** (`-DHW_REV_V3 -DLASER_6K`)
```
300V bat → PMS:
  VICOR_TMS (pin 51, HIGH=ON) → 300V→48V → MCC board power + TMC header passthrough
  VICOR_GIM (pin 55, HIGH=ON) → 300V→48V → gimbal              [sets isBDC_Ready]

VICOR_TMS 48V output → pickoff → onboard VICOR_BUS (pin 40):
  VICOR_BUS PC pin always-open at boot (no independent fw control)
  Input is VICOR_TMS 48V — VICOR_TMS must be enabled before relay bank is available
  RELAY_GPS   (pin 67, HIGH=ON) → GPS appliance 24V
  RELAY_NTP   (pin 56, HIGH=ON) → Phoenix NTP appliance 24V
  RELAY_LASER → not driven · pin 54 unused on 6kW path

300V bat → busbar direct → 6kW laser HV:
  PIN_ENERGIZE / RELAY_LASER bit 2 (pin 63, NO opto)
    closes → 5V out → external PSU enable signal

Note: V3* requires the external Vicor pack from V2 (VICOR_GIM + VICOR_TMS)
fitted to the V3 PCB. VICOR_TMS PC pin is held open — no firmware switch.
```

**V4 — 48V or 300V battery · 3kW or 6kW laser** (future)
- Consolidates V3 and V3\* into a single voltage-variable platform.
- Solenoids (`SOL_HEL`, `SOL_BDA`) retired — bits 5 and 6 available for reassignment.
- Board includes an input Vicor (bat→48V) to accommodate either battery voltage natively.
- `RELAY_NTP`, `RELAY_GPS`, and three-relay bank carried forward.

**`MCC_POWER` enum — full cross-revision reference (POWER_BITS byte [10], bit N = enum value N):**

| Bit | Enum | V1 pin | V2 pin | V3 pin · 3kW | V3 pin · 6kW | V4 | Notes |
|-----|------|--------|--------|--------------|--------------|-----|-------|
| 0 | `RELAY_GPS` | 83 NO HIGH=ON | — | 67 NO HIGH=ON | 67 NO HIGH=ON | ✅ | GPS appliance power |
| 1 | `VICOR_BUS` | A0 LOW=ON · 48V→24V | — no relay bank | 40 HIGH=ON · 48V→24V | 40 HIGH=ON · fed from VICOR_TMS | ✅ | Relay bus prerequisite. Polarity flips V1→V3. |
| 2 | `RELAY_LASER` | 20 NO HIGH=ON · 3kW bus | 83 NO HIGH=ON · 6kW enable ⚠️ | 54 NO HIGH=ON · 3kW bus | 63 PIN_ENERGIZE NO · 5V ext PSU | ✅ | V2 reuses V1 RELAY_GPS pin |
| 3 | `VICOR_GIM` | — | A0 NC HIGH=ON · 300V→48V ⚠️ | — | 55 HIGH=ON · 300V→48V gimbal | ✅ | V2 reuses V1 VICOR_BUS pin. Sets isBDC_Ready on V2/V3-6kW. |
| 4 | `VICOR_TMS` | — | 20 NC HIGH=ON · 300V→48V ⚠️ | — | 51 HIGH=ON · 300V→48V board+TMC · PC open | ✅ | V2 reuses V1 RELAY_LASER pin. No fw switch on V3*. |
| 5 | `SOL_HEL` | 5 HIGH=ON · 3kW HV bus | — | 5 HIGH=ON · 3kW HV bus | — | → `LASER_HV_EN`? | V4: solenoid retired; bit available for reassignment |
| 6 | `SOL_BDA` | 8 HIGH=ON · gimbal | — | 50 HIGH=ON · gimbal | — | — retired | Sets isBDC_Ready on V1/V3-3kW. V4: bit available. |
| 7 | `RELAY_NTP` | — | — | 56 NO HIGH=ON | 56 NO HIGH=ON | ✅ | Phoenix NTP appliance power — new V3 |

> ⚠️ **Bring-up note (V3 VICOR_BUS):** `POL_VICOR_BUS_ON = HIGH` on V3 — polarity inverted vs V1 (was LOW=ON). Use `POL_VICOR_BUS_ON` / `POL_VICOR_BUS_OFF` macros; never write literal `HIGH`/`LOW` to the VICOR_BUS pin.


### 7.2 W5500 Socket Budget

W5500 has 8 hardware sockets. MCC allocates **6/8 with PTP disabled (current default)** or
**8/8 with PTP enabled**.

| # | Owner | Port | Type | Notes |
|---|-------|------|------|-------|
| 1 | MCC `udpA1` | 10019 | unicast | A1 RX — TMC unsolicited stream |
| 2 | MCC `udpA2` | 10018 | unicast | A2 eng RX+TX — shared: NTP TX/RX, TMC TX, fire control broadcast to BDC |
| 3 | MCC `udpA3` | 10050 | unicast | A3 external RX+TX |
| 4 | GNSS `udpRxClient` | 3001 | unicast | GNSS data RX from NovAtel |
| 5 | GNSS `udpTxClient` | 3002 | unicast | GNSS cmd TX to NovAtel |
| 6 | IPG `udpClient` | 10011 | unicast | HEL laser status/control |
| 7 | PTP `udpEvent` | 319 | multicast | PTP SYNC RX — only opened when `isPTP_Enabled=true` |
| 8 | PTP `udpGeneral` | 320 | multicast | PTP DELAY_REQ/RESP — only opened when `isPTP_Enabled=true` |

BAT (RS485), DBU (I2C), CRG (I2C), TPH (I2C) consume no W5500 sockets.

### 7.3 Subsystem Embedding

MCC REG1 (256-byte payload) embeds:

| Bytes | Sub-register | Class |
|-------|--------------|-------|
| [34–44] | Battery REG1 (11 bytes) | MSG_BATTERY |
| [45–65] | IPG/Laser REG1 (21 bytes) | MSG_IPG |
| [66–129] | TMC REG1 pass-through (64 bytes) | MSG_TMC |
| [135–212] | GNSS block (78 bytes) | MSG_GNSS |
| [213–244] | Charger block (32 bytes) | MSG_CMC |

### 7.4 A1 TX → BDC

MCC sends REG1 and fire control vote (0xAB) to BDC via A1 at 50 Hz and 100 Hz respectively.
`SEND_FIRE_STATUS` gate: `isPwr_LaserRelay && isBDC_Enabled` (all revisions). `isBDC_Ready` is set exclusively by `endPacket()` result — the only real-time feedback on BDC reachability. ARP backoff added (`BDC_A1_FAIL_MAX=3`, `BDC_A1_BACKOFF_TICKS=5` × 5 ms = 25 ms suppression). Replaces V1's `isSolenoid2_Enabled` gate — laser power is the correct semantic gate on all hardware variants.

### 7.5 Vote Aggregation

```
MCC aggregates fire control votes:
  HORIZON vote   (from BDC geometry)
  KIZ vote       (from BDC KIZ engine)
  LCH vote       (from BDC LCH engine)
  BDA vote       (LOS clear)
  ARMED vote     (operator armed)
  notAbort vote  (no abort condition — inverted, safe-by-default)
  EMON           (energy monitor)
  → 0xE0 SET_BCAST_FIRECONTROL_STATUS → BDC @ 100 Hz
```

### 7.6 Time Source Architecture

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

### 7.7 Build Configuration (`hw_rev.hpp`)

Two compile-time axes must both be set. `hw_rev.hpp` enforces mutual exclusion within each axis and validates the combination.

**Axis 1 — Hardware revision:**

| Define | Effect |
|--------|--------|
| `HW_REV_V1` | V1 hardware — relay bus Vicor (A0 LOW=ON), solenoids, GPS relay, charger I2C (DBU3200) |
| `HW_REV_V2` | V2 hardware — VICOR_GIM (A0) + VICOR_TMS (pin 20) switched, no relay bank, no solenoids, GPIO-only charger |
| `HW_REV_V3` | V3 hardware — monolithic PCB, relay bus Vicor (pin 40 HIGH=ON), solenoids, three relays (GPS/NTP/LASER), charger I2C (DBU3200). 6kW variant (`LASER_6K`) adds external VICOR_GIM (pin 55) + VICOR_TMS (pin 51). |
| `MCC_HW_REV_BYTE` | Auto-set — `0x01` (V1), `0x02` (V2), `0x03` (V3); written to REG1 byte [254] |

**Axis 2 — Laser model:**

| Define | Effect |
|--------|--------|
| `LASER_3K` | 3kW laser (YLM-3000-SM-VV) — 48V battery. Gates `EnablePower(RELAY_LASER)` to drive pin 20 (V1) or pin 54 (V3). Enables `SOL_HEL` path. |
| `LASER_6K` | 6kW laser (YLM-6000) — 300V battery. Gates `EnablePower(RELAY_LASER)` to drive pin 83 (V2) or `PIN_ENERGIZE` pin 63 (V3). Disables `SOL_HEL`. |

**Valid build combinations:**

| `HW_REV` | `LASER_3K` | `LASER_6K` | Battery | Valid |
|----------|-----------|-----------|---------|-------|
| `HW_REV_V1` | ✅ only | ❌ compile error | 48V | ✅ |
| `HW_REV_V2` | ❌ compile error | ✅ only | 300V | ✅ |
| `HW_REV_V3` | ✅ | ✅ | 48V (3kW) or 300V (6kW) | ✅ |

**Power pin and polarity macros:**

| Macro group | V1 | V2 | V3 |
|-------------|----|----|-----|
| `POL_VICOR_BUS_ON/OFF` | `LOW` / `HIGH` (inverted) | — | `HIGH` / `LOW` ⚠️ polarity flips V1→V3 |
| `POL_VICOR_GIM_ON/OFF` | — | `HIGH` / `LOW` ✅ HW-1 confirmed | `HIGH` / `LOW` |
| `POL_VICOR_TMS_ON/OFF` | — | `HIGH` / `LOW` ✅ HW-2 confirmed | `HIGH` / `LOW` |

`EnablePower(MCC_POWER, bool)` is the sole function that calls `digitalWrite` on power output pins. All eight `MCC_POWER` outputs (`RELAY_GPS`, `VICOR_BUS`, `RELAY_LASER`, `VICOR_GIM`, `VICOR_TMS`, `SOL_HEL`, `SOL_BDA`, `RELAY_NTP`) are dispatched through a single switch in `EnablePower()`, gated by `#if defined(HW_REV_Vx)` and `#if defined(LASER_xK)` blocks. Outputs not valid for the active build target are rejected in the `default` case — no `digitalWrite`, no flag change.

---


---

## 8. BDC Internal Architecture

BDC is the system integration hub. Runs on STM32F7, FW v3.3.0, IP: 192.168.1.20.

Hardware revision is selected at compile time in `hw_rev.hpp` (`HW_REV_V1` or `HW_REV_V2`). The active revision is reported in REG1 byte [392] (`HW_REV`) so `MSG_BDC.cs` can self-detect the register layout. Read byte [392] before interpreting `HEALTH_BITS` [10] bit 1 (`isSwitchEnabled`).

### 8.1 Role

Beam Director Controller — system integration hub managing all payload and tracking subsystems:
- Gimbal (Galil) — pan/tilt servo drive, NED coordinate transforms, PID loops
- TRC (Orin) — camera capture, tracker, video encoder supervision
- FMC — FSM DAC/ADC, focus stage control
- MWIR camera — thermal imager control
- FUJI lens — zoom, focus, iris control
- Inclinometer — platform level
- PTP client (primary) — syncs to NovAtel GNSS grandmaster (.30) via IEEE 1588
- NTP client (fallback) — syncs to NTP (.33) with automatic fallback to .208
- Fire control — receives MCC vote aggregation (0xAB), relays to TRC

**Hardware variants:**

| Subsystem | V1 | V2 (Controller 1.0 Rev A) |
|---|---|---|
| Vicor PSU | `PIN_VICOR1_ENABLE` GPIO 7, **LOW = ON** (NC opto) | GPIO 7 unchanged, **HIGH = ON** (polarity flipped) |
| Relays 1–4 | GPIO 2/3/4/6, HIGH = ON | **Unchanged** |
| Vicor thermistor | `PIN_TEMP_VICOR` GPIO 0 | `PIN_TEMP_VICOR` GPIO **20** |
| Relay area temp | Not present | `PIN_TEMP_RELAY` GPIO 19 — new NTC thermistor |
| Battery-in temp | Not present | `PIN_TEMP_BAT` GPIO 18 — new NTC thermistor |
| USB 5V temp | Not present | `PIN_TEMP_USB` GPIO 16 — new NTC thermistor |
| Ethernet switch | Not present | IP175 5-port switch — `PIN_IP175_RESET` GPIO 52, `PIN_SWITCH_DISABLE` GPIO 64 |
| `PIN_DIG2_ENABLE` | GPIO 42 (defined, never used) | **Removed** |

### 8.2 Boot Sequence

Non-blocking state machine (~26s total before UDP_READ runs):
```
POWER_SETTLE(10s) → VICOR_ON(1s) → RELAYS_ON(1s) → GIMBAL_INIT(1s)
→ TRC_INIT(2s) → FMC_INIT(2s) → NTP_INIT(2s) → PTP_INIT(1s) → FUJI_WAIT(5s) → DONE(0.5s)
```
`PTP_INIT` added session 32 — calls `ptp.INIT(IP_GNSS_BYTES)` after network has settled.
`FUJI_WAIT` added session 28 — advances when `fuji.isConnected` or after 5s timeout. Non-blocking. Prints `BOOT: FUJI READY` or `BOOT: FUJI timeout`. Note: `fuji.SETUP()` deferred to post-boot via `pendingRelaySetup` flag — `fuji.isConnected` is always false at this step, so FUJI_WAIT always times out at 5s regardless of physical connection (FW-C3 open).
`DONE` reduced from 1s to 0.5s — Fuji now has dedicated wait step. Completion print: `BOOT: complete  gimbal=RDY|---  trc=RDY|---  fmc=RDY|---  fuji=RDY|---  ntp=RDY|---`

### 8.3 Subsystem Drivers

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

### 8.4 BDC W5500 Socket Budget

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

### 8.5 BDC Time Source Architecture

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

### 8.6 Liveness Flags

| Flag | Condition | Timeout |
|------|-----------|---------|
| `isTRC_A1_Alive` | A1 frame from .22 within 200ms | 200ms |
| `isFMC_A1_Alive` | A1 frame from .23 within 200ms | 200ms |
| `isMCC_A1_Alive` | A1 frame from .10 within 200ms | 200ms |
| `isJETSON_Ready()` | `trc.isConnected && isTRC_A1_Alive` | — |
| `isFSM_Ready()` | `fmc.isConnected && isFMC_A1_Alive` | — |

### 8.7 Mode State Machine

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

### 8.8 Dual-Loop Control (AT Mode)

```
TRC tx, ty
  ├── FSM loop (fast):   error = tx + atX0 / ty + atY0
  │     └── FMC: FSM_X + FSM_X0 / FSM_Y + FSM_Y0
  └── Gimbal loop (slow): error = tx + atX0 / ty + atY0
        └── Galil: JG velocity commands (port 7777)
```

### 8.9 Build Configuration (`hw_rev.hpp`)

| Define | Effect |
|---|---|
| `HW_REV_V1` | V1 hardware — Vicor NC opto (LOW=ON), `PIN_TEMP_VICOR` GPIO 0 |
| `HW_REV_V2` | V2 hardware — Vicor non-inverted (HIGH=ON), `PIN_TEMP_VICOR` GPIO 20, 3 new thermistors, IP175 switch pins |
| `BDC_HW_REV_BYTE` | Auto-set — `0x01` (V1) or `0x02` (V2); written to REG1 byte [392] |
| `POL_VICOR_ON` / `POL_VICOR_OFF` | Per-revision Vicor drive polarity — consumed by `EnableVicor()` and `BDC.ino` setup() |

**REG1 HB counter bytes (reserved space [396–403]):**

| Byte | Field | Units | Source |
|------|-------|-------|--------|
| [396] | `HB_NTP` | x0.1s (÷100) | NTP intercept — same pattern as MCC |
| [397] | `HB_FMC_ms` | raw ms | `a1_fmc_last_ms` — FMC A1 stream RX |
| [398] | `HB_TRC_ms` | raw ms | `a1_trc_last_ms` — TRC A1 stream RX |
| [399] | `HB_MCC_ms` | raw ms | `a1_mcc_last_ms` — MCC 0xAB broadcast RX |
| [400] | `HB_GIM_ms` | raw ms | `gimbal.lastRecordTime` — Galil data record RX |
| [401] | `HB_FUJI_ms` | raw ms | `fuji.lastRspTime` — C10 serial response RX |
| [402] | `HB_MWIR_ms` | raw ms | `mwir.lastRspTime` — serial response RX |
| [403] | `HB_INCL_ms` | raw ms | `incl.lastRspTime` — UART frame RX |

Defined: 404 bytes. Reserved: 108 bytes. Fixed block: 512 bytes.

`EnableVicor(bool en)` is the sole function that calls `digitalWrite` on `PIN_VICOR1_ENABLE`. All relay `digitalWrite` calls go through `EnableRelay(uint8_t r, bool en)`. Both revisions use HIGH=ON for all four relays — no relay polarity macros needed.

> ⚠️ **Vicor polarity note:** V1 uses a NC opto-isolator (LOW=ON, safe-off=HIGH). V2 is non-inverted (HIGH=ON, safe-off=LOW). `BDC.ino` setup() uses `POL_VICOR_OFF` for the boot-safe state — do not write literal `HIGH`/`LOW` for `PIN_VICOR1_ENABLE`.

---


---

## 9. TMC Internal Architecture

TMC runs on STM32F7 (OpenCR board library), FW v3.3.0, IP: 192.168.1.12.

Hardware revision is selected at compile time in `hw_rev.hpp` (`HW_REV_V1` or `HW_REV_V2`). The active revision is reported in REG1 byte [62] (`HW_REV`) so `MSG_TMC.cs` can self-detect the layout.

### 9.1 Role

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

### 9.2 TMC W5500 Socket Budget

W5500 has 8 hardware sockets. TMC allocates **4/8 always** — four sockets spare.

| # | Owner | Port | Type | Notes |
|---|-------|------|------|-------|
| 1 | TMC `udpA1` | 0 (ephemeral) | unicast | TX only — 100 Hz unsolicited stream to MCC |
| 2 | TMC `udpA2` | 10018 | unicast | A2 RX+TX — shared: NTP TX/RX (`&udpA2`) |
| 3 | PTP `udpEvent` | 319 | multicast | PTP SYNC RX — **always allocated at boot** |
| 4 | PTP `udpGeneral` | 320 | multicast | PTP DELAY_REQ/RESP — **always allocated at boot** |

Pump (GPIO/DAC), LCM1/2 (DAC+ADC), Vicors (GPIO), Fans (PWM), TPH (I2C), Flow sensors (interrupt) consume no W5500 sockets.

> **ptp.INIT() unconditional:** TMC calls `ptp.INIT(IP_GNSS)` at boot regardless of `isPTP_Enabled` — sockets 3/4 are always allocated. `isPTP_Enabled` gates `ptp.UPDATE()` only. This is the correct pattern — MCC and FMC will align to match (FW-B4).

### 9.3 Hardware

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

### 9.4 A1 TX → MCC

TMC streams REG1 (64 bytes) to MCC (.10) at 100 Hz via A1 port 10019. MCC embeds the
received buffer directly into MCC REG1 bytes [66–129] with no parsing at the MCC level —
raw pass-through. THEIA parses it as `MSG_TMC`.

### 9.5 Temperature Channels

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

### 9.6 Build Configuration (`hw_rev.hpp`)

| Define | Effect |
|--------|--------|
| `HW_REV_V1` | V1 hardware — Vicor/ADS1015/heater/single pump |
| `HW_REV_V2` | V2 hardware — TRACO/direct analog/no heater/dual pump |
| `SINGLE_LOOP` | Optional (independent of HW_REV) — single coolant loopback; both PIDs track `tf2` |
| `CTRL_OFF` / `CTRL_ON` | Auto-set from HW_REV — Vicor/PSU inhibit line polarity |
| `TMC_HW_REV_BYTE` | Auto-set — `0x01` (V1) or `0x02` (V2); written to REG1 byte [62] |

---


---

## 10. FMC Internal Architecture

FMC runs on STM32F7 (OpenCR board library), FW v3.3.0, IP: 192.168.1.23.

Hardware revision is selected at compile time in `hw_rev.hpp` (`HW_REV_V1` or `HW_REV_V2`). The active revision is reported in REG1 byte [45] (`HW_REV`) so `MSG_FMC.cs` can self-detect the register layout. Read byte [45] before interpreting `HEALTH_BITS` [7] and `POWER_BITS` [46].

### 10.1 Hardware

| Hardware | Interface | Notes |
|----------|-----------|-------|
| AD5752R DAC | SPI | FSM X/Y drive voltage |
| LTC1867 ADC | SPI | FSM X/Y position readback (int32 counts) |
| M3-LS focus stage | I2C | Single axis, counts-based position |

### 10.2 FMC W5500 Socket Budget

W5500 has 8 hardware sockets. FMC allocates **2/8 with PTP disabled (current default)** or **4/8 with PTP enabled** — six sockets spare.

| # | Owner | Port | Type | Notes |
|---|-------|------|------|-------|
| 1 | FMC `udpA1` | 0 (ephemeral) | unicast | TX only — 50 Hz unsolicited stream to BDC |
| 2 | FMC `udpA2` | 10018 | unicast | A2 RX+TX — shared: NTP TX/RX (`&udpA2`) |
| 3 | PTP `udpEvent` | 319 | multicast | PTP SYNC RX — **only opened when `isPTP_Enabled=true`** |
| 4 | PTP `udpGeneral` | 320 | multicast | PTP DELAY_REQ/RESP — **only opened when `isPTP_Enabled=true`** |

DAC (SPI), ADC (SPI), stage (I2C) consume no W5500 sockets. NTP shares `udpA2`.

> ⚠️ **FW-B3 constraint:** FMC `ptp.INIT()` is gated by `if (isPTP_Enabled)` at boot. Unlike MCC (FW-B4), FMC's gate is required due to W5500 multicast contention with BDC — both opening ports 319/320 simultaneously causes W5500 socket exhaustion on the shared network. FMC socket budget stays at 2/8 until FW-B3 is resolved fleet-wide.

### 10.3 FMC Time Source Architecture

FMC mirrors MCC/BDC/TMC time source architecture. `isPTP_Enabled` defaults to `false` (FW-B3 deferred). `isNTP_Enabled` defaults to `true` (SAMD21 NTP bug resolved — not applicable on STM32F7). NTP init is unconditional at boot; PTP init gated by `isPTP_Enabled`.

**`GetCurrentTime()` routing (`fmc.hpp`) — session 35 holdover:**
```
Same EPOCH_MIN_VALID_US guard + holdover path as MCC (section 9.5).
isPTP_Enabled  = false  (default — FW-B3 deferred)
isNTP_Enabled  = true   (default — STM32F7; SAMD21 NTP bug no longer applicable)
```

**NTP suppression:** `ntpSuppressedByPTP = true` (default).

**Register:** `TIME_BITS` at FMC REG1 byte 44 — identical layout to MCC (253), BDC (391), TMC (61). FSM STAT BITS bits 2-3 vacated (were `ntp.isSynched`/`ntpUsingFallback`) — all time status now in TIME_BITS. Bit 7 (`isUnsolicitedModeEnabled`) retired session 35 — always 0.

### 10.4 Embedding

FMC REG1 (64 bytes) is embedded in BDC REG1 at bytes [169–232] as a raw pass-through.
BDC also separately sets FSM calibration fields directly into `fmcMSG` from BDC REG1 fields
[333–362] (iFOV, X0/Y0, signs, stage position).

> **FSM position note:** `FSM_X/Y` commanded (int16) at BDC REG1 [233–236] and `FSM Pos X/Y`
> ADC readback (int32) in FMC REG1 [20–27] are correct distinct types — int16 fits the DAC
> command range; int32 is the signed ADC readback with sign inversion. Not a bug. ✅ Closed #7.

### 10.5 Build Configuration (`hw_rev.hpp`)

| Define | Effect |
|--------|--------|
| `HW_REV_V1` | V1 hardware — SAMD21 / MKR layout (legacy) |
| `HW_REV_V2` | V2 hardware — STM32F7 / OpenCR layout |
| `FMC_HW_REV_BYTE` | Auto-set — `0x01` (V1) or `0x02` (V2); written to REG1 byte [45] |
| `FMC_SERIAL` | Auto-set — `SerialUSB` (V1) or `Serial` (V2) — USB CDC serial port abstraction |
| `FMC_SPI` | Auto-set — `SPI` (V1) or `SPI_IMU` (V2) — FSM DAC/ADC SPI peripheral abstraction |
| `FSM_POW_ON` / `FSM_POW_OFF` | FSM power enable polarity — `HIGH`/`LOW` both revisions (abstracted for future changes) |
| `uprintf()` | Cross-platform formatted print via `FMC_SERIAL` — replaces SAMD21 `SerialUSB.printf` workaround |

---


---

## 11. THEIA Architecture

### 11.1 Class Structure

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

### 11.2 Xbox Controller Mapping

| Input | Normal | + Left Shoulder |
|-------|--------|-----------------|
| Right trigger (short press) | ADVANCE MODE | — |
| Right shoulder (short press) | REGRESS MODE | — |
| Left + Right trigger (simultaneous) | FIRE vote (heartbeat via `0xAB`) | — |
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

### 11.3 Fire Control Chain

```
Operator: Left + Right trigger simultaneously
  └── 0xAB SET_FIRE_REQUESTED_VOTE {1} → MCC (heartbeat, continuous, via A3)
        └── MCC aggregates: HORIZON + KIZ + LCH + BDA + ARMED + notAbort votes
              └── BDCTotalVote() → fire authorized if all votes pass
                    └── 0xAB → BDC @ 100 Hz → TRC (reticle color)
```

---


---


---

## Appendix A — Serial Debug Standards

All four embedded controllers share a unified serial debug architecture. Any new command
added to any controller must conform to this standard.

### A.1 Serial Buffer

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

### A.2 HELP Box Structure

All controllers print HELP using Unicode box drawing with a COMMON block (identical across
all controllers) followed by a SPECIFIC block (local hardware only):

```
╔══ <CTRL> — COMMON COMMANDS ═══════════════════════════════╗
║  <command list — same on all controllers>                  ║
╠══ <CTRL> — SPECIFIC COMMANDS ═════════════════════════════╣
║  <controller-specific hardware commands>                   ║
╚═══════════════════════════════════════════════════════════╝
```

### A.3 Common Commands (All Controllers)

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

### A.4 Specific Commands (Per Controller)

| Controller | Specific Commands |
|------------|------------------|
| TMC | FLOWS, LCM, VICOR, TEMP, FAN, VICOR \<ch\>, LCM \<ch\>, DAC, PUMP |
| BDC | REINIT, ENABLE, FMC, TRC, MCC, RELAY, VICOR |
| MCC | REINIT, ENABLE, TMC, SOL, RELAY, VICOR, CHARGER, CHARLEVEL, HEL, HELCLR, FAN, TARGETTEMP |
| FMC | FSM, FSMPOS, FSMPOW, STAGE, STAGEPOS, STAGECAL, STAGEEN, SCAN |

### A.5 TIME Command Output

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

### A.6 A1 TX Control

All four controllers have `bool isA1Enabled = true` in their `.hpp` file. The flag is:
- Firmware-only — no network command may change it
- Serial only: `A1 ON` / `A1 OFF`
- Default `true` — A1 streams from boot
- `SEND_FIRE_STATUS()` (MCC) and `SEND_FIRE_STATUS_TO_TRC()` (BDC) gated on this flag

### A.7 FMC SerialUSB Constraint

FMC (SAMD21) uses `SerialUSB` for all debug output. `Serial` goes to the hardware UART
which is not connected. Key rules:
- All debug output: `SerialUSB.println()` or `uprintf()` (formatted helper)
- Never call `ptp.PrintTime()` or `ntp.PrintTime()` — both use `Serial` internally
- `ptp.INIT()` and `ntp.INIT()` use `Serial` internally — gated on `isPTP_Enabled` /
  `isNTP_Enabled` in `fmc.cpp INIT()` so they don't fire unless the source is enabled

---


---

## Appendix B — Version Word Format

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
| BDC | 3.3.0 | `VERSION_PACK(3,3,0)` |
| FMC | 3.3.0 | `VERSION_PACK(3,3,0)` |
| TRC | 3.0.2 | `VERSION_PACK(3,0,2)` |
| TMC | 3.3.0 | `VERSION_PACK(3,3,0)` |

C# unpack:
```csharp
UInt32 major = (VERSION_WORD >> 24) & 0xFF;
UInt32 minor = (VERSION_WORD >> 12) & 0xFFF;
UInt32 patch =  VERSION_WORD        & 0xFFF;
// No "v" prefix in display string — canonical format is "3.0.1" not "v3.0.1"
```

---


---

## Appendix C — Consolidated Port Reference

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


---

## Appendix D — Compatibility Matrix

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
| MCC V1/V2/V3 hardware abstraction | ✅ MCC unification (ICD v3.4.0) + V3 add — `hw_rev.hpp`, unified codebase, HW_REV byte [254] self-detecting (`0x01`=V1, `0x02`=V2, `0x03`=V3). `LASER_3K`/`LASER_6K` compile axis. MCC_POWER enum renames (RELAY_GPS, RELAY_LASER, RELAY_NTP, VICOR_GIM, VICOR_TMS) — ICD vTBD. |
| BDC HB counters REG1 [396–403] | ✅ CB-20260413d — 8 bytes, defined count 396→404 |
| BDC V1/V2 hardware abstraction | ✅ BDC unification — `hw_rev.hpp`, unified codebase FW v3.3.0, HW_REV byte [392] self-detecting. HEALTH_BITS/POWER_BITS rename (ICD v3.5.1). Vicor polarity flip V1→V2. Three new thermistors + IP175 switch control on V2. |
| FMC STM32F7 port + V1/V2 hardware abstraction | ✅ FMC STM32F7 port — `hw_rev.hpp`, unified codebase FW v3.3.0, HW_REV byte [45] self-detecting. HEALTH_BITS byte [7] / POWER_BITS byte [46] (ICD v3.5.2). ptp.INIT() gated FW-B3. FMC_SERIAL/FMC_SPI platform abstraction. |

---

