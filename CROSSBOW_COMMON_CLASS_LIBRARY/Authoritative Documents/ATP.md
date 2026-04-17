# CROSSBOW Acceptance Test Procedure

**Document Version:** 1.0.0 (DRAFT)
**Date:** 2026-04-17
**Companion To:** ARCHITECTURE.md v4.0.1
**ICD Reference:** ICD v3.6.0
**Hardware Scope:** V1 and V2 (dual-rev procedure)
**Process Scope:** First power-on → functional checkout. Engagement / fire-control dry-run is **out of scope** for this document.

---

## Purpose

This procedure verifies that a CROSSBOW system, delivered from manufacturing as a fully
integrated assembly, reaches a known-good functional state. It walks the technician from
pre-power inspection through per-controller bring-up, network/time-sync verification, and
subsystem functional checkout. On successful completion the system is cleared for engagement
rehearsal and operational acceptance testing (covered in a separate ATP).

This ATP does **not** cover:
- Sub-assembly bench qualification (assumed complete before delivery)
- Operator engagement sequences (CUE, AT, FT, fire-vote chain exercised end-to-end)
- Environmental or EMI qualification

Every step has a unique ID (`ATP-NN.N`), expected result, and pass/fail record line. Any
**FAIL** stops the procedure at that step pending disposition.

---

## 1. Reference Documents

| Ref | Document | Version |
|-----|----------|---------|
| R1 | ARCHITECTURE.md | 4.0.1 |
| R2 | CROSSBOW_ICD_INT_ENG | 3.6.0 |
| R3 | CROSSBOW_ICD_INT_OPS | 3.6.0 |
| R4 | CROSSBOW_ICD_EXT_OPS (IPGD-0005) | current |
| R5 | JETSON_SETUP.md (IPGD-0020) | 2.2.1 |
| R6 | CROSSBOW_CHANGELOG.md (IPGD-0019) | current — open items register |

---

## 2. Required Equipment

| Item | Purpose |
|------|---------|
| Engineering laptop, IP in `.1–.99` range, A2 ENG GUI installed | Per-controller checkout, serial over USB-CDC |
| USB-to-serial cables (×5 — MCC, BDC, TMC, FMC, TRC) | Direct serial console to each embedded controller |
| Terminal emulator — PuTTY / minicom / screen — 115200 8N1 | Serial command entry |
| Multimeter (DMM) — verified calibrated | V2 bring-up polarity verification (mandatory for V2) |
| Oscilloscope (optional) | PTP packet timing, PSU ramp debugging |
| THEIA HMI workstation (Windows, dual-NIC: `.200–.254` + `.1–.99`) | HMI connection, video RX, A3 frame check |
| HYPERION workstation (Windows, IP `.206` by default) | CUE source link check |
| Laser safety interlock key (REMOVED for all of this ATP) | **Physical safety gate — stays out entire procedure** |
| Coolant, filled and bled | Required before TMC pump enable |

> ⚠️ **Laser key stays OUT for the entire ATP.** This procedure verifies IPG comms only.
> No laser emission is commanded or expected. If the system enters a state where emission
> could occur, **STOP** and raise to test lead.

---

## 3. Personnel and Safety Preconditions

Before proceeding past §4:

- [ ] Laser safety interlock key is physically removed and under test-lead control
- [ ] No personnel in front of the gimbal LOS envelope
- [ ] Coolant loop is filled and bled (TMC will not be commanded to run dry)
- [ ] Power source is wired correctly, breakers OFF, battery disconnected
- [ ] Test lead and one additional technician present — no solo bring-up
- [ ] ESD precautions in effect for all connector mate/de-mate operations

---

## 4. Pre-Power Inspection

### ATP-4.1 — Visual Inspection
Inspect the integrated system for:
- No loose hardware, tools, or FOD inside the enclosure
- No visible damage to cables, connectors, optics, or gimbal yoke
- Gimbal physically at or near mechanical zero
- All connectors fully seated and strain-relieved

**Pass criteria:** No defects found. **Record:** Technician initials + date.

### ATP-4.2 — Harness Verification
With power OFF:
- Confirm Ethernet cables present at all five controllers + switch
- Confirm Galil gimbal drive connected to BDC harness
- Confirm GNSS antenna cable connected to `.30` receiver
- Confirm TMC coolant hoses routed and clamped
- Confirm FMC FSM drive and ADC readback cables present
- Confirm USB-C / USB-CDC ports on all embedded controllers are accessible

**Pass criteria:** All harnesses present and routed per build drawing.

### ATP-4.3 — Hardware Revision Identification

Before power-on, identify the hardware revision of each of the four embedded controllers.
This governs which polarity rules, pin maps, and expected register values apply for the
remainder of the ATP. Record both the build tag (from the board or work order) and, after
power-on, cross-check against the REG1 `HW_REV` byte.

| Controller | HW_REV byte (REG1) | V1 marker | V2 marker | Recorded |
|------------|--------------------|-----------|-----------|----------|
| MCC | `[254]` | `0x01` | `0x02` | `_____` |
| BDC | `[392]` | `0x01` | `0x02` | `_____` |
| TMC | `[62]`  | `0x01` | `0x02` | `_____` |
| FMC | `[45]`  | `0x01` — SAMD21/MKR | `0x02` — STM32F7/OpenCR | `_____` |

> ⚠️ **FMC is a platform change, not just a board spin.** V1 is SAMD21/MKR, V2 is STM32F7/OpenCR.
> Serial console (`SerialUSB` vs `Serial`), NTP init behavior, and TPH telemetry all differ.
> Verify firmware matches the physical board before proceeding to §5.

### ATP-4.4 — V2 Polarity Bring-Up (V2 only, first V2 unit bring-up only)

> ⚠️ **Mandatory for first V2 MCC and first V2 BDC hardware.** Skip for repeat units
> with polarity already verified, but record the unit serial in §16.

**V2 MCC — verify relay drive polarity with power ON but MCC held in reset / firmware halted:**

- `POL_PWR_GIM_ON = LOW` → drive line should read LOW when `GIM_VICOR` is commanded ON.
- `POL_PWR_TMS_ON = HIGH` → drive line should read HIGH when `TMS_VICOR` is commanded ON.

Verify with DMM on the drive line before allowing the relay sequence to complete. These
polarity macros were analytically derived from the schematic; first-article hardware must
empirically confirm (ARCH §3.1 bring-up note).

**V2 BDC — verify Vicor PSU polarity:**

- V2 non-inverted — `HIGH = ON`, safe-off = `LOW` (opposite of V1 NC-opto).
- Confirm `BDC.ino setup()` writes `POL_VICOR_OFF` (not literal `HIGH` / `LOW`) on boot.
- DMM on `PIN_VICOR1_ENABLE` at first boot: expect LOW before VICOR_ON step.

**Pass criteria:** Measured polarity matches `hw_rev.hpp` macro per ARCH §3.1 and §10.9.

---

## 5. Network Infrastructure Setup

### ATP-5.1 — Switch Configuration
- [ ] 1 Gbps managed Ethernet switch powered
- [ ] **IGMP snooping OFF** on all CROSSBOW VLAN ports (required for PTP multicast `224.0.1.129`)
- [ ] All CROSSBOW nodes on the same VLAN / broadcast domain

**Record switch model + config snapshot filename:** `_____`

### ATP-5.2 — Reference Services
| Service | IP | Status |
|---------|----|--------|
| NTP Stratum 1 (Phoenix Contact FL TIMESERVER) | `.33` | Powered, GPS-locked, green indicator |
| NTP fallback (Windows HMI `w32tm`) | `.208` | Service running |
| NovAtel GNSS (PTP grandmaster) | `.30` | Powered, FINESTEERING, sky visible |

### ATP-5.3 — NovAtel PTP Configuration (one-time, per unit)
If not already saved to NVM on this GNSS receiver, issue:

```
PTPMODE ENABLE_FINETIME
PTPTIMESCALE UTC_TIME
SAVECONFIG
```

Verify `LOG PTPSTATUSA` returns `state=MASTER`, `Time Offsets Valid=TRUE`.

**Record config state:** `[ ] newly configured  [ ] pre-configured and verified`

### ATP-5.4 — Engineering Laptop
- IP set in `.1–.99` range (e.g., `192.168.1.50`)
- A2 ENG GUI installed, version matches target firmware
- Can ping NTP server `.33` and GNSS `.30` before any CROSSBOW power-on

**Pass criteria:** `ping 192.168.1.33` and `ping 192.168.1.30` both return reply.

---

## 6. Controller Power-Up Sequence

> **Controllers may be powered in any order for initial boot** — the system is tolerant of
> peer absence via A1 liveness timeouts (ARCH §6.6, §10.6). However, recommended bring-up
> order below minimizes noisy liveness flags during first boot.

**Recommended order:**

1. Reference services (§5.2 confirmed up)
2. MCC (`.10`) — power/GNSS/laser comms
3. TMC (`.12`) — thermal, streams A1 to MCC
4. FMC (`.23`) — FSM/stage, streams A1 to BDC
5. TRC (`.22`) — Jetson, streams A1 to BDC once Linux boots
6. BDC (`.20`) — integration hub, consumes MCC/FMC/TRC A1 streams

Gimbal (`.21`) and HEL/IPG (`.13`) come up with their host controllers.

### ATP-6.1 — MCC First Boot

Power MCC. Open USB serial console at 115200 8N1.

Expected boot print (abbreviated):
```
BOOT: MCC v3.3.0 HW_REV=V1|V2
BOOT: net <ip> <mac>
BOOT: complete  tmc=--- hel=--- gnss=--- ntp=---|RDY
```

- [ ] No assert / stack-dump / bootloop
- [ ] `INFO` serial command returns: fw version `3.3.0`, IP `192.168.1.10`, HW_REV as expected
- [ ] No red error lines in boot print

### ATP-6.2 — TMC First Boot

Power TMC. Serial console.

Expected:
```
BOOT: TMC v3.3.0 HW_REV=V1|V2
BOOT: complete
```

- [ ] `INFO` shows fw `3.3.0`, IP `.12`, HW_REV matches §4.3
- [ ] No TPH / flow / ADC init errors
- [ ] V1 only: 4 Vicors + ADS1015 init OK
- [ ] V2 only: 2 Vicors (LCM only), direct analog OK, heater / ADS1015 absent by design

### ATP-6.3 — FMC First Boot

Power FMC. Serial console.

| Rev | Console port | Serial object |
|-----|--------------|---------------|
| V1 (SAMD21) | USB CDC | `SerialUSB` |
| V2 (STM32F7) | USB CDC | `Serial` |

Expected:
```
BOOT: FMC v3.3.0 HW_REV=V1|V2
```

- [ ] `INFO` shows fw `3.3.0`, IP `.23`, HW_REV matches §4.3
- [ ] FSM DAC/ADC init OK
- [ ] M3-LS focus stage I2C init OK
- [ ] V2 only: BME280 TPH init OK (REG1 bytes `[47–58]` will be non-zero after boot)
- [ ] V1 only: TPH bytes `[47–58]` remain `0x00` by design

### ATP-6.4 — TRC First Boot

Power TRC (Jetson Orin NX). Connect via SSH or local console.

- [ ] Linux 6.1 booted, login prompt reachable
- [ ] Static IP `192.168.1.22` applied
- [ ] `systemd-timesyncd` active (see §9.3)
- [ ] Launch: `./multi_streamer --dest-host 192.168.1.208`
- [ ] Stdout shows cameras detected (Alvium + MWIR), A1/A2 threads started
- [ ] No fatal GStreamer pipeline errors

### ATP-6.5 — BDC First Boot

Power BDC last so its A1 peers are already streaming.

BDC runs a non-blocking ~26s boot state machine (ARCH §10.2):
```
POWER_SETTLE(10s) → VICOR_ON(1s) → RELAYS_ON(1s) → GIMBAL_INIT(1s)
 → TRC_INIT(2s) → FMC_INIT(2s) → NTP_INIT(2s) → PTP_INIT(1s)
 → FUJI_WAIT(5s) → DONE(0.5s)
```

Expected completion print:
```
BOOT: complete  gimbal=RDY  trc=RDY  fmc=RDY  fuji=---  ntp=RDY
```

- [ ] Full boot state machine ran (no hang at a step)
- [ ] `gimbal=RDY` — Galil Ethernet link established
- [ ] `trc=RDY` — TRC A1 stream active
- [ ] `fmc=RDY` — FMC A1 stream active
- [ ] `fuji=---` is **expected** (FW-C3 open — `fuji.SETUP()` deferred post-boot; verified separately in §11.6)
- [ ] `ntp=RDY`
- [ ] V2 only: `isSwitchEnabled` (IP175) reports healthy via `HEALTH_BITS[10]` bit 1

**BDC boot failure triage:**
- Hang at `GIMBAL_INIT` → Galil power / Ethernet cable
- Hang at `TRC_INIT` → TRC not streaming A1 (likely TRC multi_streamer not started)
- Hang at `FMC_INIT` → FMC not streaming A1 (likely FMC power or IP)
- `FUJI_WAIT` times out at 5s → **expected** (FW-C3)

---

## 7. Per-Controller Serial Checkout

Run the `HELP` command on each controller to confirm command registry is present. Then
execute the COMMON block commands listed below on all four embedded controllers.

### ATP-7.1 — COMMON Block (all four: MCC, BDC, TMC, FMC)

| Step | Command | Expected / Record |
|------|---------|-------------------|
| a | `INFO` | Build date, fw `3.3.0`, IP, MAC, HW_REV |
| b | `REG` | Full REG1 dump — no obviously corrupt fields |
| c | `STATUS` | System state = STANDBY (`0x01`); gimbal mode = OFF |
| d | `TEMPS` | All sensors in plausible range (ambient to ~40 °C at rest) |
| e | `TIME` | Active source NTP after ~60s; PTP OFF by default (FW-B3) |
| f | `NTP` | Synched, server `.33`, epoch within last minute |
| g | `A1 OFF` then `A1 ON` | Confirm flag toggle reported; re-enable before leaving |

**Record per-controller pass/fail:**

| | MCC | BDC | TMC | FMC |
|---|---|---|---|---|
| COMMON block pass | `___` | `___` | `___` | `___` |

> **FMC serial exception (V1/SAMD21):** `ptp.PrintTime()` / `ntp.PrintTime()` write to hardware
> `Serial` (not connected). `TIME` shows `[see PTPDEBUG]` when synced. This is expected —
> use `PTPDEBUG 2` for PTP output. V2/STM32F7 does not have this constraint.

### ATP-7.2 — SPECIFIC Block (sanity only — full function tests in §11)

| Controller | Spot-check command | Expected |
|------------|--------------------|----------|
| MCC | `RELAY` | Lists relay states — all OFF at boot (expected) |
| MCC | `SOL` (V1 only) | SOL_HEL / SOL_BDA listed and OFF. **V2: this command is vestigial — solenoids retired; skip.** |
| MCC | `CHARGER` | Status block, charger disabled at boot |
| MCC | `HEL` | IPG status readable (OFF state) — do NOT command emission |
| BDC | `VICOR` | Vicor state OFF at this point (VICOR_ON runs in boot only if relays auto-enabled; see §11) |
| BDC | `FMC` | Passes through FMC REG1 recent snapshot |
| BDC | `TRC` | Passes through TRC REG1 recent snapshot |
| TMC | `FLOWS` | Flow sensor counters incrementing only if pumps running; at rest, static |
| TMC | `PUMP` | V1: single Vicor pump status. V2: `PUMP1` / `PUMP2` independent status |
| TMC | `VICOR` | V1: 4 Vicors. V2: 2 Vicors (LCM1/LCM2 only) |
| FMC | `FSMPOS` | Current FSM X/Y ADC counts (near zero if powered but uncommanded) |
| FMC | `STAGEPOS` | Focus stage position in counts |

---

## 8. Network and Time Sync Verification

### ATP-8.1 — Ping Sweep

From engineering laptop (`.1–.99`), issue `ping` (one at a time is fine):

| Target | IP | Pass |
|--------|----|------|
| MCC | `.10` | `___` |
| HEL/IPG | `.13` | `___` |
| TMC | `.12` | `___` |
| BDC | `.20` | `___` |
| Gimbal (Galil) | `.21` | `___` |
| TRC | `.22` | `___` |
| FMC | `.23` | `___` |
| GNSS | `.30` | `___` |
| NTP | `.33` | `___` |

All must reply. If any fail, check switch port / cable / IP conflict before proceeding.

### ATP-8.2 — CRC-16/CCITT Cross-Platform Verification

> ⚠️ **Mandatory.** ARCH §6.5 documents past integration issues between STM32 and Linux/x86
> CRC implementations. Do not skip this — unit tests alone are not sufficient. This is a
> pre-HW-test checklist gate (ARCH §6.5 callout).

On each of the five controllers, invoke the CRC known-answer test via serial (embedded) or
command-line (TRC):

**Known-answer test:** `crc16("123456789", 9) == 0x29B1`

| Controller | Result | Pass |
|-----------|--------|------|
| MCC | `0x____` | `___` |
| BDC | `0x____` | `___` |
| TMC | `0x____` | `___` |
| FMC | `0x____` | `___` |
| TRC | `0x____` | `___` |

Also verify both C# apps (THEIA, TRC_ENG_GUI_PRESERVE) compute the same value — issue an A2
frame from the ENG GUI and confirm controller accepts it (implicit validation).

### ATP-8.3 — NTP Sync

Allow ~60 seconds after last controller power-on, then on each of MCC/BDC/TMC/FMC:

```
TIME
```

Expected:
```
NTP   enabled       : YES
NTP   synched       : YES
NTP   misses        : 0
NTP   usingFallback : no
NTP   lastSync      : < 30000 ms ago
```

On TRC:
```bash
timedatectl status
```
Expected: `NTP service: active`, `System clock synchronized: yes`, server `192.168.1.33`.

| Controller | NTP synched | usingFallback | Pass |
|-----------|-------------|---------------|------|
| MCC | `___` | `___` | `___` |
| BDC | `___` | `___` | `___` |
| TMC | `___` | `___` | `___` |
| FMC | `___` | `___` | `___` |
| TRC | `___` | `___` | `___` |

> **Note:** `usingFallback=no` is the nominal state with `.33` primary up. If `usingFallback=YES`,
> triage `.33` reachability before accepting — fallback to `.208` is for degraded operation only.

### ATP-8.4 — PTP Sync (MCC — optional, FW-B3 gated)

**Default state is PTP OFF on all controllers** due to FW-B3 (W5500 DELAY_REQ contention).
Per ARCH §9.5, PTP is available on MCC when enabled individually.

If the test plan requires PTP verification, on MCC only:

```
TIMESRC PTP
TIME
```

Expected after ~10 s:
```
PTP   enabled : YES
PTP   synched : YES
PTP   offset_us : < 1000
active source : PTP
```

Then revert:
```
TIMESRC AUTO
```

**Do not enable PTP on BDC or FMC concurrently with MCC** until FW-B3 is closed fleet-wide.
BDC/TMC/FMC PTP check deferred to post-FW-B3 re-run.

**Record:** PTP enabled this pass? `[ ] yes  [ ] no (default)` — MCC PTP synched: `___`

---

## 9. A1 Telemetry Streams

A1 streams are always-on from boot (ARCH §6.6). Verify all five expected streams are live.

### ATP-9.1 — Stream Presence

On BDC (primary A1 consumer), check liveness flags via `REG` or ENG GUI MSG_BDC view:

| Flag | Source | Rate | Timeout | Pass |
|------|--------|------|---------|------|
| `isTRC_A1_Alive` | TRC → BDC | 100 Hz | 200 ms | `___` |
| `isFMC_A1_Alive` | FMC → BDC | 50 Hz | 200 ms | `___` |
| `isMCC_A1_Alive` | MCC → BDC (via 0xAB) | 100 Hz | 200 ms | `___` |

On MCC, check TMC stream:

| Flag | Source | Rate | Pass |
|------|--------|------|------|
| TMC REG1 embedded `[66–129]` fresh | TMC → MCC | 100 Hz | `___` |

On TRC, confirm fire-control-status RX from BDC (raw 5B, port 10019) — log entry or
`voteBitsMcc` / `voteBitsBdc` updating at 100 Hz in TRC state.

### ATP-9.2 — HB Counters (BDC v4.0.1 wiring)

BDC REG1 bytes `[396–403]` hold rolling heartbeat counters (ARCH §10 v4.0.1 note). Read
via ENG GUI — all should be recently updated (< 100 ms since last RX):

| Byte | Field | Units | Reading |
|------|-------|-------|---------|
| [396] | `HB_NTP` | ×0.1 s | `___` |
| [397] | `HB_FMC_ms` | ms | `___` |
| [398] | `HB_TRC_ms` | ms | `___` |
| [399] | `HB_MCC_ms` | ms | `___` |
| [400] | `HB_GIM_ms` | ms | `___` |
| [401] | `HB_FUJI_ms` | ms | `___` (likely large — FW-C3, Fuji deferred) |
| [402] | `HB_MWIR_ms` | ms | `___` |
| [403] | `HB_INCL_ms` | ms | `___` |

**Pass criteria:** all FMC / TRC / MCC / GIM / MWIR / INCL counters below 200 ms.
`HB_FUJI_ms` large is expected (FW-C3 open).

### ATP-9.3 — A1 Bench Mode Sanity (optional)

To confirm the `isA1Enabled` guard works (useful if peer is absent for later testing):

1. On MCC serial: `A1 OFF` → BDC `isMCC_A1_Alive` clears within 200 ms
2. On MCC serial: `A1 ON` → flag restores within 200 ms
3. Repeat for TMC→MCC, FMC→BDC, TRC→BDC if time permits

**Record:** `[ ] toggle verified on all four  [ ] skipped`

---

## 10. Subsystem Functional Checks

**Preconditions for this section:** Sections 4 through 9 all PASS. Laser key still OUT.

### ATP-10.1 — Gimbal (Galil)

On BDC serial, command small pan/tilt steps:
- Command `+1°` pan, verify position readback converges
- Command `−1°` pan, return to origin
- Command `+1°` / `−1°` tilt, same
- Confirm no stall, no following-error fault from Galil data port 7778

**Pass criteria:** commanded and readback agree within ±0.05°; no fault code.

> **Safety:** keep motion limited to small excursions until full range test during
> operational acceptance (separate ATP).

### ATP-10.2 — FSM (FMC)

On FMC serial:
- `FSMPOW ON` → verify V1: `FSM_POW_ON`; V2: `FSM_POW_ON`. Observe DAC settles to zero.
- `FSMPOS 1000 0` → X-axis step command
- `FSMPOS 0 0` → null
- `FSMPOS 0 1000` → Y-axis step command
- `FSMPOS 0 0` → null
- Read back FMC REG1 `[20–27]` — ADC counts should track commanded DAC within FSM settle time

**Pass criteria:** X/Y command/readback agree; no saturation; no FSM angle-limit vote bit
trip (`FSM_NOT_LTD` stays HIGH / OK).

`FSMPOW OFF` at end.

### ATP-10.3 — Focus Stage (M3-LS)

On FMC serial:
- `STAGEEN ON` → stage enabled
- `STAGEPOS` — read current position
- `STAGECAL` or small relative move — verify motion and readback
- `STAGEEN OFF` at end

**Pass criteria:** commanded move reflected in readback; no I2C bus error.

### ATP-10.4 — MWIR Camera

On BDC serial, issue `MWIR` subcommands (NUC, polarity toggle, AF). Verify response strings
received on serial port. Coordinate with TRC operator to confirm the MWIR video pane in the
TRC compositor shows a valid image (unfocused is OK — full focus/cal is operational).

**Pass criteria:** MWIR returns to each command; TRC reports `status_cam1` bit `STARTED` and
`ACTIVE` set.

### ATP-10.5 — Fuji Lens (C10)

> Note: `fuji=---` at BDC boot is expected (FW-C3). Functional verification happens here.

On BDC serial: `FUJI` — query status. If `fuji.isConnected` flips true post-boot once
`fuji.SETUP()` has run (via `pendingRelaySetup` flag after VICOR_ON):

- Zoom command → confirm response
- Focus command → confirm response

**Pass criteria:** at least one successful zoom and one focus response received via serial.
If `fuji.isConnected` remains false indefinitely, log as FW-C3 manifestation and continue
(not a hard fail — known open item).

### ATP-10.6 — Inclinometer

On BDC serial: `INCL` or observe BDC REG1 inclinometer block.

**Pass criteria:** roll/pitch values present and plausible (±0.5° at a known-level test bench);
`HB_INCL_ms` < 200 ms (§9.2).

### ATP-10.7 — Laser (IPG/HEL) — Communications Only

> ⚠️ **Key still OUT.** No emission permitted. This step verifies IPG status comms only.

On MCC serial: `HEL` — read IPG status block.
- Laser reports `OFF` / `DISARMED`
- Communications link healthy (status byte updating)
- No laser faults logged

**Record:** IPG reports state = `_____`, comm link = `_____`

**Pass criteria:** HEL block populated in MCC REG1 `[45–65]`; no comm-lost flag set.

### ATP-10.8 — Charger

On MCC serial: `CHARGER` — read status.

| Rev | Interface | Expected at boot |
|-----|-----------|------------------|
| V1 | DBU3200 I2C + GPIO pin 6 enable | I2C alive, enable LOW, not charging |
| V2 | GPIO only, enable pin 82 | GPIO state readable, not charging |

`CHARLEVEL` — confirm level query returns. Do **not** command charge during this ATP.

**Pass criteria:** charger status readable; `isChargerEnabled` = 0; no protection fault.

### ATP-10.9 — TMC Subsystems

**Preconditions:** coolant filled and bled (§3).

On TMC serial:

| Step | Command | Expected |
|------|---------|----------|
| a | `FLOWS` | f1/f2 counters present, zero or near-zero (pumps off) |
| b | `PUMP` (V1) or `PUMP1` / `PUMP2` (V2) | Pump states OFF |
| c | `LCM` | LCM1/LCM2 Vicor + DAC states OFF |
| d | `FAN` | Fan1/Fan2 PWM = 0 |
| e | `TEMP` | All `tt/ta1/tf1/tf2/tc1/tc2/to1/to2/tv1/tv2` plausible; `tv3/tv4` present (V1) or `0x00` (V2) |
| f | `VICOR` | V1: 4 Vicors OFF. V2: 2 Vicors OFF (LCM only) |

**Short pump run (optional, with test lead approval):**
- Enable Vicor LCM1 only, run pump 1 at low setting for 10 s, verify `tf1` and `f1`
  change in expected direction, then stop.
- Repeat for Vicor LCM2 / pump 2.

**Pass criteria:** All subsystems respond to commands; no overtemp, no fault.

### ATP-10.10 — V2-Specific Extras

| Subsystem | Check | Pass |
|-----------|-------|------|
| V2 BDC — 3 new NTC thermistors (`TEMP_RELAY`, `TEMP_BAT`, `TEMP_USB`) | Plausible ambient readings | `___` |
| V2 BDC — IP175 Ethernet switch | `isSwitchEnabled` = 1, `PIN_IP175_RESET` released | `___` |
| V2 FMC — BME280 ambient TPH (REG1 `[47–58]`) | Three floats present: Temp °C, Pressure Pa, Humidity % — non-zero, plausible | `___` |
| V2 MCC — `GIM_VICOR` + `TMS_VICOR` enable/disable via `EnablePower()` | Drive line polarity matches §4.4 result | `___` |
| V2 TMC — Dual independent TRACO pumps | Each commandable independently (see §10.9 step b) | `___` |

### ATP-10.11 — V1-Specific Extras

| Subsystem | Check | Pass |
|-----------|-------|------|
| V1 MCC — SOL_HEL, SOL_BDA solenoids | Drive state OFF at boot, toggleable via `SOL` serial command | `___` |
| V1 MCC — DBU3200 charger I2C | `CHARGER` returns valid status block over I2C | `___` |
| V1 MCC — GPS relay (pin 83) | GNSS receiver reports NODATA when relay OFF, data when ON | `___` |
| V1 TMC — Heater (Vicor + DAC) | `VICOR 4` readable, heater OFF at boot | `___` |
| V1 TMC — ADS1015 aux channels | `tv3`/`tv4` present and plausible | `___` |
| V1 FMC — SAMD21 `SerialUSB` console | Console active over USB CDC; `TIME` shows `[see PTPDEBUG]` when synced (expected) | `___` |

---

## 11. Video Pipeline

### ATP-11.1 — TRC → THEIA Video

Preconditions: TRC is running `./multi_streamer --dest-host 192.168.1.208`. THEIA
workstation is on `.208` with GStreamer installed at `C:\gstreamer\1.0\msvc_x86_64\`.

On THEIA:
1. Launch THEIA application
2. VideoPanel should populate with 1280×720 @ 60 fps stream (ARCH §7.1)
3. Verify picture is not shifted (`PixelShift = -420` correction applied internally)
4. Toggle camera source (Back = VIS, Start = MWIR on Xbox controller — verify both panes)

**Pass criteria:** live video, no frame stutter beyond startup jitter window, no GStreamer
decoder errors in THEIA log.

**Record hardware decoder path:** `[ ] nvh264dec (HW)  [ ] avdec_h264 (SW fallback)` —
CPU at 720p/30fps should be ~10–15% on SW fallback.

---

## 12. HMI (THEIA) A3 Connection

### ATP-12.1 — A3 Registration

With THEIA launched:
- THEIA `mcc.cs` and `bdc.cs` should each send a single `0xA4 FRAME_KEEPALIVE` on connect (ARCH §4.2)
- THEIA log shows `connection established` for both MCC and BDC within one frame RX
- MCC A3 client table shows THEIA slot registered
- BDC A3 client table shows THEIA slot registered

### ATP-12.2 — Unsolicited Stream

THEIA operator enables unsolicited via the checkbox (per §4.2 — no auto-subscribe):
- `0xA0 SET_UNSOLICITED {0x01}` sent to MCC and BDC
- 100 Hz REG1 stream arrives at THEIA for both MCC and BDC
- HB_RX_ms in THEIA GUI reads ≤ 20 ms steady-state

**Pass criteria:** both unsolicited streams active; THEIA GUI fields update live.

### ATP-12.3 — 30 s Keepalive

Leave THEIA running idle for > 60 s. Confirm:
- No `connection lost` log events
- `KeepaliveLoop` fires at 30 s cadence (`_lastKeepalive` advances)
- STATUS_BITS bit 7 (`isUnsolicitedModeEnabled`) reads `0` on both MCC and BDC
  (retired session 35 — C# side should not depend on this bit)

---

## 13. HYPERION Integration Link Check

> Scope limited to link check — full CUE-to-engagement flow is operational acceptance territory.

### ATP-13.1 — Sensor Inputs

On HYPERION workstation (`.206`):
- ADS-B decoder at `.31` reachable → `trackLogs` populating from ICAO source
- RADAR input on UDP `15001` (EXT_OPS framed) — test packets or live source accepted
- LoRa input on UDP `15002` accepted
- Stellarium (optional) feeding `trackLogs["STELLA"]`

### ATP-13.2 — CUE Output to THEIA

HYPERION operator selects a track and enables `jtoggle_CROSSBOW`. Verify on THEIA:
- UDP `15009` receives EXT_OPS framed 71-byte packets (CMD `0xAA`, magic `0xCB 0x48`)
- THEIA `RADAR` class / `CueReceiver` validates frame (magic, CRC, length)
- Track appears in THEIA CUE display

**Do NOT press Xbox A (CUE_FLAG) or advance mode.** This is a link check only — mode
progression is out of scope for this ATP.

**Pass criteria:** at least one valid CUE frame received and validated by THEIA. No CRC
rejects on EXT_OPS path.

### ATP-13.3 — CUE SIM Backup (optional)

If HYPERION is not present for this ATP run, CUE SIM can be substituted:
- CUE SIM → HYPERION sniffer verifies CUE output format
- CUE SIM → direct THEIA (`15009`) substitutes for HYPERION CUE source

Record which CUE source was used: `[ ] HYPERION  [ ] CUE SIM  [ ] not exercised`

---

## 14. Known Open Items — Acceptable States for This ATP

The following items are tracked as open in `CROSSBOW_CHANGELOG.md` and have expected
manifestations during ATP. Do **not** fail the ATP on these:

| ID | Expected manifestation during ATP |
|----|-----------------------------------|
| FW-B3 | PTP disabled by default fleet-wide — NTP is primary time source during ATP |
| FW-C3 | BDC boot shows `fuji=---` / `HB_FUJI_ms` large — Fuji SETUP runs post-boot |
| FW-C4 | BDC A1 ARP backoff not working — mitigated by `A1 OFF` when peer offline |
| FW-14 | MCC GNSS `RUNONCE` case 6 / `EXEC_UDP` socket — does not block first-boot checkout |
| THEIA-SHUTDOWN | No graceful STANDBY→OFF sequence — do not test shutdown behaviour this pass |
| HMI-A3-18 | LCH/KIZ/HORIZ GUI emplacement work pending — fire-control vote gating tests deferred |
| GUI-8 | TRC C# client model pending standardization — TRC ENG GUI tab may be rough |

Any item not in this table that produces an error during the ATP is a **FAIL** until
triaged by test lead.

---

## 15. Acceptance Criteria Summary

System is accepted for operational acceptance testing when:

- [ ] §4 — Pre-power inspection PASS, HW_REV of all four embedded controllers recorded
- [ ] §4.4 — V2 polarity bring-up PASS (V2 units only; first-article if applicable)
- [ ] §5 — Network infrastructure confirmed operational
- [ ] §6 — All five controllers boot cleanly, no assert/fault
- [ ] §7 — COMMON and SPECIFIC serial checkouts PASS on all four embedded controllers
- [ ] §8.1 — Full ping sweep PASS
- [ ] §8.2 — CRC-16/CCITT known-answer PASS on all five controllers
- [ ] §8.3 — NTP synched on all five
- [ ] §9 — All A1 streams live, HB counters fresh (FUJI exception per FW-C3)
- [ ] §10 — Subsystem functional checks PASS (rev-specific items complete)
- [ ] §11 — Video pipeline delivers 1280×720 @ 60 fps stable
- [ ] §12 — THEIA A3 connection, keepalive, unsolicited stream PASS
- [ ] §13 — HYPERION CUE link check PASS (or CUE SIM substituted)

---

## 16. Sign-Off

**System serial number:** `____________________`

**HW revisions (from §4.3):**
- MCC: `[ ] V1  [ ] V2`
- BDC: `[ ] V1  [ ] V2`
- TMC: `[ ] V1  [ ] V2`
- FMC: `[ ] V1  [ ] V2`

**First V2 unit polarity verification performed (§4.4):** `[ ] yes  [ ] not applicable (repeat unit)`
If yes, unit serial: `____________________`

**Firmware versions observed:**
| Controller | Expected | Observed |
|-----------|----------|----------|
| MCC | 3.3.0 | `______` |
| BDC | 3.3.0 | `______` |
| TMC | 3.3.0 | `______` |
| FMC | 3.3.0 | `______` |
| TRC | 3.0.2 | `______` |

**ATP result:** `[ ] PASS — cleared for operational acceptance ATP`
                `[ ] CONDITIONAL PASS — deviations listed below`
                `[ ] FAIL — see deviation log`

**Deviation / waiver log:**
```
______________________________________________________________
______________________________________________________________
______________________________________________________________
```

**Signatures:**

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Test technician | | | |
| Test lead | | | |
| QA witness | | | |

---

## Appendix A — Quick Reference

### Network Map

```
.10  MCC       .13  HEL/IPG      .20  BDC        .21  Gimbal (Galil)
.12  TMC       .22  TRC (Jetson) .23  FMC        .30  GNSS (PTP master)
.33  NTP       .206 HYPERION     .208 THEIA      .1–.99 ENG / internal
```

### Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 10018 | A2 internal | ENG GUI ↔ all controllers |
| 10019 | A1 internal | Sub-controller → upper unsolicited streams |
| 10050 | A3 external | THEIA → MCC / BDC |
| 5000 | UDP RTP | TRC → THEIA H.264 video |
| 7777 | UDP | BDC → Galil gimbal CMD |
| 7778 | UDP | Galil → BDC data/status |
| 319/320 | PTP | MCC ↔ GNSS multicast |
| 15001 | EXT_OPS | CUE source → HYPERION aRADAR |
| 15002 | EXT_OPS | CUE source → HYPERION aLORA |
| 15009 | EXT_OPS | HYPERION/CUE SIM → THEIA |

### HW_REV Byte Quick Reference

| Controller | REG1 byte | V1 | V2 |
|-----------|-----------|----|----|
| MCC | `[254]` | `0x01` | `0x02` |
| BDC | `[392]` | `0x01` | `0x02` |
| TMC | `[62]` | `0x01` | `0x02` |
| FMC | `[45]` | `0x01` (SAMD21) | `0x02` (STM32F7) |

---

*End of ATP v1.0.0 — review with test lead before first use. Feedback and revisions
back to this document, not to ARCHITECTURE.md.*
