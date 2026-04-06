# CROSSBOW — NovAtel GNSS Receiver Configuration

**Document:** `CROSSBOW_GNSS_CONFIG`
**Doc #:** IPGD-0007
**Version:** 1.0.0
**Date:** 2026-03-28 (session 28)
**Classification:** IPG Internal Use Only
**Audience:** IPG engineering staff, field technicians

---

## Overview

The NovAtel GNSS receiver (OEM7 platform) serves two roles in the CROSSBOW system:

1. **Position / Attitude source** — BESTPOS, INSATTX, DUALANTENNAHEADING, TERRASTARSTATUS
   streamed to MCC via UDP (ICOM1, ports 3001/3002).
2. **PTP Grandmaster** — IEEE 1588 precision time source for MCC (and eventually all five
   controllers). GPS-disciplined clock, sub-microsecond accuracy when locked.

> **Network address:** `192.168.1.30`
> **MCC address:** `192.168.1.10`

---

## 1. PTP Configuration

These commands must be sent once and saved to NVM. They survive power cycles after `SAVECONFIG`.

### 1.1 Commands

```
PTPMODE ENABLE_FINETIME
PTPTIMESCALE UTC_TIME
SAVECONFIG
```

### 1.2 Parameter Notes

| Command | Parameter | Value | Reason |
|---------|-----------|-------|--------|
| `PTPMODE` | `ptp_mode` | `ENABLE_FINETIME` | PTP only activates after GPS `FINESTEERING`. No fix = no SYNC packets = MCC detects stale and falls back to NTP cleanly. |
| `PTPTIMESCALE` | `ptp_timescale` | `UTC_TIME` | Timestamps in UNIX/UTC epoch. Without this, timestamps are GPS time (currently 18 s ahead of UTC) and MCC clock would be wrong by 18 s. |
| `PTPPROFILE` | — | `PTP_UDP` (factory default) | Multicast UDP, E2E delay, Domain 0, 1 Hz sync, 2-step. Do not change. |

> **⚠ `ENABLE` vs `ENABLE_FINETIME`:** Factory default is `ENABLE` which keeps PTP
> active even without GPS lock, using a degraded internal oscillator. MCC cannot
> distinguish a locked from an unlocked master via PTP packets alone. `ENABLE_FINETIME`
> is strongly preferred — no lock = no SYNC = clean MCC fallback to NTP.

### 1.3 Validation

After applying PTP config, verify with:

```
LOG PTPDELTATIMEA ONCE
LOG TIMEA ONCE
```

**Expected `PTPDELTATIMEA` fields:**

| Field | Expected | Notes |
|-------|----------|-------|
| PTP State | `MASTER` | NovAtel is grandmaster |
| Clock Class | `13` | Primary reference (GPS-disciplined) |
| Time Offsets Valid | `TRUE` | GPS lock confirmed |
| Time Offset | `< 1e-6` | Sub-microsecond offset |
| Reference Clock Identity | same as Clock Identity | Self-referencing (GPS source) |

**Expected `TIMEA` fields:**

| Field | Expected | Notes |
|-------|----------|-------|
| Header clock status | `FINESTEERING` | GPS fine tracking active |
| utc status | `VALID` | UTC alignment confirmed |
| utc offset | `-18` | Current GPS–UTC leap seconds |

### 1.4 PTP Network Parameters

| Parameter | Value |
|-----------|-------|
| Profile | `PTP_UDP` |
| Transport | UDP multicast |
| Multicast group | `224.0.1.129` |
| Event port | 319 |
| General port | 320 |
| Domain | 0 |
| Sync rate | 1 Hz |
| Step mode | 2-step (SYNC + FOLLOW_UP) |
| Delay mechanism | E2E (DELAY_REQ / DELAY_RESP) |
| Timescale | UTC |

> **Switch requirement:** IGMP snooping must be **OFF** on the network switch for
> multicast PTP packets to flow to MCC. Verify in switch admin UI.

### 1.5 MCC PTP Client Reference

MCC opens two W5500 sockets (ports 319 and 320, multicast) and implements the
IEEE 1588 slave (ordinary clock) role. DELAY_REQ is sent unicast to `192.168.1.30:319`.

Time source priority on MCC:
```
PTP (GNSS .30)  →  NTP primary (.33)  →  NTP fallback (.208)
```

Fallback trigger: 5 consecutive missed SYNC cycles (~10 s) → `ptp.isSynched` cleared
→ NTP gate opens → NTP sends every 10 s.

---

## 2. Network / Ethernet Configuration

### 2.1 IP Address

Set the receiver to a static IP on the CROSSBOW subnet:

```
IPCONFIG ETHA STATIC 192.168.1.30 255.255.255.0 192.168.1.1
SAVECONFIG
```

| Parameter | Value |
|-----------|-------|
| Interface | `ETHA` (primary Ethernet) |
| Mode | `STATIC` |
| IP Address | `192.168.1.30` |
| Subnet Mask | `255.255.255.0` |
| Gateway | `192.168.1.1` |

### 2.2 WiFi

Disable WiFi radio and access point — not used in CROSSBOW deployment:

```
WIFIMODE OFF
WIFINETCONFIG 1 DISABLE
SAVECONFIG
```

---

## 3. ICOM / Port Configuration

### 3.1 ICOM UDP Mode

Configure ICOM ports 1–3 to use UDP transport. This is required for MCC to
receive NovAtel binary logs over Ethernet (ports 3001/3002).

```
ICOMCONFIG ICOM1 UDP
ICOMCONFIG ICOM2 UDP
ICOMCONFIG ICOM3 UDP
SAVECONFIG
```

| Port | Transport | Used by |
|------|-----------|---------|
| ICOM1 | UDP | MCC data stream (BESTPOS, TIMEB, INSATTX, etc.) |
| ICOM2 | UDP | Available — reserved |
| ICOM3 | UDP | Available — reserved |

> **MCC port mapping:** MCC listens on UDP 3001 (data RX) and 3002 (command TX).
> The NovAtel `LOG ICOM1 ...` commands in MCC `RUNONCE()` direct all log output
> to ICOM1 which delivers to MCC via UDP.

---

## 4. Receiver Mode / PPP Configuration

### 3.1 Receiver Dynamics

Set the receiver as a rover (mobile platform, not a fixed base station):

```
RTKDYNAMICS ROVER
```

### 3.2 TerraStar PPP

Enable PPP corrections in auto mode (TerraStar subscription required):

```
TERRASTARCONTROL ENABLE AUTO
```

| Parameter | Value | Meaning |
|-----------|-------|---------|
| enable | `ENABLE` | Activate TerraStar corrections |
| mode | `AUTO` | Automatically use best available correction level |

> **Subscription note:** TerraStar corrections require an active subscription and
> L-band satellite signal lock. `TERRASTARSTATUSB ONCHANGED` in the MCC data stream
> monitors sync state — look for `LOCKED` in `TerraStar_SyncState`.

---

## 5. Antenna and IMU Configuration

Offsets are measured in the **IMU body frame** (metres). Standard deviations
default to 0.01 m (1 cm) — update if a more precise survey is available.

### 4.1 Antenna 1 Offset (Primary — Position)

```
SETINSTRANSLATION ANT1 -0.273 -0.636 -0.045 0.01 0.01 0.01
```

| Axis | Value (m) | Std Dev (m) |
|------|-----------|-------------|
| X | -0.273 | 0.01 |
| Y | -0.636 | 0.01 |
| Z | -0.045 | 0.01 |

### 4.2 Antenna 2 Offset (Secondary — Heading)

```
SETINSTRANSLATION ANT2 -0.273 0.458 -0.045 0.01 0.01 0.01
```

| Axis | Value (m) | Std Dev (m) |
|------|-----------|-------------|
| X | -0.273 | 0.01 |
| Y | 0.458 | 0.01 |
| Z | -0.045 | 0.01 |

> **Baseline check:** ANT1→ANT2 Y separation = 0.458 − (−0.636) = **1.094 m**.
> Verify this matches the physical antenna separation after installation.

### 4.3 IMU Orientation

IMU body frame relative to vehicle frame: **X = left, Y = forward, Z = down**.

```
SETIMUORIENTATION 5
```

> **⚠ Verify orientation index:** NovAtel OEM7 IMU orientation enums vary by
> IMU model. Confirm orientation index `5` matches X=left, Y=forward, Z=down
> for the installed IMU using `LOG RAWIMUXA ONCE` and the OEM7 firmware manual
> (APN-064). If the INS solution shows incorrect roll/pitch/heading, the
> orientation index is the first thing to check.

**Standard NovAtel OEM7 orientation reference:**

| Index | X | Y | Z |
|-------|---|---|---|
| 1 | Right | Forward | Up |
| 2 | Forward | Left | Up |
| 3 | Left | Backward | Up |
| 4 | Backward | Right | Up |
| 5 | Left | Forward | Down |
| 6 | Forward | Right | Down |

---

## 6. Data Stream Configuration (MCC UDP Logs)

These are sent by MCC `RUNONCE()` at startup to configure the NovAtel data stream:

```
unlogall
LOG ICOM1 TIMEB ONTIME 1
LOG ICOM1 BESTPOSB ONTIME 10
LOG ICOM1 TERRASTARSTATUSB ONCHANGED
LOG ICOM1 INSATTXB ONTIME 11
LOG ICOM1 DUALANTENNAHEADINGB ONTIME 12
LOG ICOM1 UPTIMEB ONCE
```

| Log | Rate | Purpose |
|-----|------|---------|
| `TIMEB` | 1 Hz | UTC time validation |
| `BESTPOSB` | 0.1 Hz | Position (lat/lng/alt), solution status, SVs |
| `TERRASTARSTATUSB` | on change | TerraStar PPP correction status |
| `INSATTXB` | ~0.09 Hz | INS roll/pitch/azimuth |
| `DUALANTENNAHEADINGB` | ~0.08 Hz | Dual-antenna heading |
| `UPTIMEB` | once | Receiver uptime on connect |

> **Note:** `INSATTXB ONTIME 11` and `DUALANTENNAHEADINGB ONTIME 12` use non-integer
> rates to avoid phase-locking with other 1 Hz streams and reduce burst collisions on
> the UDP socket.

---

## 7. Full Initialization Sequence

Complete sequence to configure a replacement or factory-reset receiver:

```
# ── Step 1 — Network ────────────────────────────────────────────────────────
IPCONFIG ETHA STATIC 192.168.1.30 255.255.255.0 192.168.1.1
WIFIMODE OFF
WIFINETCONFIG 1 DISABLE

# ── Step 2 — Receiver mode ──────────────────────────────────────────────────
RTKDYNAMICS ROVER
TERRASTARCONTROL ENABLE AUTO

# ── Step 3 — Antenna offsets / IMU orientation ──────────────────────────────
SETINSTRANSLATION ANT1 -0.273 -0.636 -0.045 0.01 0.01 0.01
SETINSTRANSLATION ANT2 -0.273  0.458 -0.045 0.01 0.01 0.01
SETIMUORIENTATION 5          # X=left Y=forward Z=down — verify for IMU model

# ── Step 5 — PTP ────────────────────────────────────────────────────────────
PTPMODE ENABLE_FINETIME      # PTP only when FINESTEERING — clean MCC fallback
PTPTIMESCALE UTC_TIME        # UNIX/UTC epoch — required for MCC clock alignment

# ── Step 6 — Data streams (normally set by MCC RUNONCE on connect) ───────────
# Only needed for standalone testing or if MCC is not connected:
# unlogall
# LOG ICOM1 TIMEB ONTIME 1
# LOG ICOM1 BESTPOSB ONTIME 10
# LOG ICOM1 TERRASTARSTATUSB ONCHANGED
# LOG ICOM1 INSATTXB ONTIME 11
# LOG ICOM1 DUALANTENNAHEADINGB ONTIME 12
# LOG ICOM1 UPTIMEB ONCE

# ── Step 7 — Save all to NVM ────────────────────────────────────────────────
SAVECONFIG
```

> **Order matters:** Set IP address first — if the receiver is already at a
> different IP, subsequent commands may need to be sent via serial (USB) rather
> than Ethernet. Always `SAVECONFIG` last.

---

## 8. Verification Checklist Checklist

| Check | Command | Pass Condition |
|-------|---------|----------------|
| GPS lock | `LOG TIMEA ONCE` | `FINESTEERING`, utc status `VALID` |
| PTP active | `LOG PTPDELTATIMEA ONCE` | State `MASTER`, Offsets Valid `TRUE`, offset `< 1e-6` |
| PTP timescale | `LOG PTPDELTATIMEA ONCE` | Time Offset should track UTC (not GPS+18s) |
| MCC receiving | MCC serial `TIME` | `PTP synched: YES`, `active source: PTP`, year correct |
| MCC offset | MCC serial `TIME` | `offset_us < 1000` |
| Wireshark | filter `udp.port==319` | SYNC at 1 Hz from `.30` → `224.0.1.129` |

---

## 9. Known Issues / Notes

| Item | Note |
|------|------|
| IGMP snooping | Must be OFF on switch — multicast will not reach MCC if enabled |
| `PTPMODE ENABLE` | Do not use in production — NovAtel sends SYNC without GPS lock, MCC cannot detect degraded accuracy |
| Leap seconds | `PTPTIMESCALE UTC_TIME` handles this automatically. If ever reverted to `PTP_TIME`, set `PTP_GPS_UTC_OFFSET_SEC 18` in `ptpClient.hpp` and update if a new leap second is announced |
| TerraStar | Requires active subscription and L-band signal lock for PPP corrections |

---

*Paste additional initialization commands and this document will be updated.*
