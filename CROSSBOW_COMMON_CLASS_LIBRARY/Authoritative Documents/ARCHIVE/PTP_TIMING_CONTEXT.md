# CROSSBOW — Timing Architecture and PTP/NTP Context

**Document:** `PTP_TIMING_CONTEXT.md`
**Version:** 1.0.0
**Date:** 2026-04-05
**Project:** CROSSBOW
**Related Documents:** ARCHITECTURE.md (IPGD-0006), CROSSBOW_ICD_INT_ENG.md (IPGD-0003)

---

## 1. Overview

CROSSBOW requires a common time reference across five embedded controllers (MCC, BDC, TMC, FMC, TRC) for fire control sequencing, telemetry timestamping, and inter-controller correlation. The system uses a layered approach: IEEE 1588 PTP as the primary high-accuracy source (when available), NTP as a warm fallback, and a software holdover mechanism to bridge transient loss of both sources.

---

## 2. Time Source Hardware

### 2.1 PTP Grandmaster — NovAtel GNSS Receiver (.30)

- **Device:** NovAtel GNSS receiver at 192.168.1.30
- **Role:** IEEE 1588 PTP grandmaster — distributes GPS-disciplined UTC to all controllers on the subnet
- **Profile:** PTP_UDP, multicast 224.0.1.129, domain 0, 1 Hz sync rate, 2-step
- **Timescale:** UTC_TIME (configured via `PTPTIMESCALE UTC_TIME` — required for correct UNIX epoch)
- **Accuracy:** ~1–100 µs (software timestamping on W5500 Ethernet)
- **Configuration commands (one-time, saved to NVM):**
  ```
  PTPMODE ENABLE_FINETIME    -- PTP only active when FINESTEERING mode (clean fallback if GPS lost)
  PTPTIMESCALE UTC_TIME      -- UNIX/UTC epoch required for correct MCC clock
  SAVECONFIG
  ```
- **Validated:** S29 — `offset_us=12`, `active source: PTP`, `time=2026-03-28` confirmed on MCC
- **Note:** IGMP snooping must be OFF on the network switch for PTP multicast to flow correctly

### 2.2 NTP Primary — Hardware Stratum 1 Appliance (.33)

- **Device:** HW Stratum 1 NTP appliance at 192.168.1.33
- **Role:** Primary NTP server for all five controllers
- **Accuracy:** ~1–10 ms
- **All controllers target .33 directly** — not relayed via THEIA

### 2.3 NTP Fallback — Windows HMI (.208)

- **Device:** THEIA Windows HMI running `w32tm` on 192.168.1.208
- **Role:** Automatic NTP fallback — controllers switch after 3 consecutive missed responses (~30s)
- **Recovery:** Automatic retry of primary after 2 minutes on fallback
- **Accuracy:** ~1–10 ms (less stable than .33)
- **Note:** 192.168.1.8 (engineering IP) must NOT be used as an NTP target

---

## 3. Time Source Priority and Selection

### 3.1 Source Hierarchy (all controllers)

| Priority | Source | Condition | Accuracy |
|----------|--------|-----------|----------|
| 1 | PTP (IEEE 1588) | isPTP_Enabled=true AND GNSS locked | ~1–100 µs |
| 2 | NTP primary (.33) | isNTP_Enabled=true AND .33 reachable | ~1–10 ms |
| 3 | NTP fallback (.208) | After 3 NTP misses on primary | ~1–10 ms |
| 4 | Holdover | Both sources invalid — free-runs from last good time | Degrades over time |

### 3.2 GetCurrentTime() Logic (all controllers, session 35)

```
EPOCH_MIN_VALID_US = 1577836800000000  (2020-01-01 00:00:00 UTC in microseconds)

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
return 0   // never synced — C# layer guards against this
```

The EPOCH_MIN_VALID_US guard prevents pre-sync free-running clock values (which start near Unix epoch zero) from being accepted as valid time. Unsigned 32-bit subtraction on micros() handles the ~71-minute rollover correctly.

### 3.3 NTP Suppression

When PTP is active, NTP polling is gated off by default (`ntpSuppressedByPTP = true`). This avoids unnecessary UDP traffic and prevents NTP from interfering with PTP offset tracking. Gate re-opens immediately when PTP becomes stale. Use serial `TIMESRC AUTO` to run both concurrently (useful for testing).

**Worst-case gap when PTP is lost:**
- PTP stale detection: `PTP_STALE_MISSES=5` × `PTP_MISS_CHECK_MS=2s` → ~10s
- NTP first send after PTP clears: up to `NTP_TICK_MS=10s`
- Total worst-case gap before NTP takes over: ~20s
- During this gap: holdover free-runs from last good PTP time

---

## 4. Per-Controller Default State (Session 35/36)

| Controller | isPTP_Enabled default | isNTP_Enabled default | Reason |
|------------|----------------------|-----------------------|--------|
| MCC | false | true | FW-B3 deferred — PTP disabled until W5500 contention resolved |
| BDC | false | true | FW-B3 deferred |
| TMC | false | true | FW-B3 deferred |
| FMC | false | false | FW-B3 deferred + SAMD-NTP open bug |
| TRC | N/A (Linux) | N/A | TRC uses system clock |

All controllers will show `activeTimeSource = NONE` until `TIMESRC NTP` or `TIMESRC PTP` is issued via serial console or until `isNTP_Enabled` / `isPTP_Enabled` defaults are changed to `true` once the open bugs are resolved.

---

## 5. Serial Commands (all embedded controllers)

| Command | Description |
|---------|-------------|
| `TIME` | Print active time source, PTP and NTP status, register byte decode |
| `TIMESRC PTP` | Enable PTP as primary, NTP suppressed while PTP synched (default) |
| `TIMESRC NTP` | NTP only — PTP disabled |
| `TIMESRC AUTO` | Both PTP and NTP active concurrently — NTP stays warm |
| `TIMESRC OFF` | Disable both sources |
| `PTPDEBUG <0-3>` | Set PTP debug verbosity (0=OFF, 1=MIN, 2=NORM, 3=VERBOSE) |
| `NTPIP <a.b.c.d>` | Set primary NTP server IP + force resync |
| `NTPFB <a.b.c.d>` | Set NTP fallback server (OFF to clear) |
| `NTPSYNC` | Force immediate NTP resync on current server |
| `NTP` | Print NTP sync status, server, epoch |

---

## 6. Open Issues

### 6.1 FW-B3 — PTP DELAY_REQ W5500 Contention

**Problem:** When two controllers on the same subnet both have PTP active simultaneously (e.g. BDC and FMC), the W5500 Ethernet controller stalls on `DELAY_REQ` transmission. The W5500 performs a blocking ARP resolution which takes ~40ms per attempt. At 50–100Hz stream rates this saturates the main loop.

**Symptoms:** Main loop dt spikes; A1 stream drops; A2 responses stall.

**Workaround (current):** `isPTP_Enabled = false` by default on all controllers. Use serial `TIMESRC NTP` for time on bench/test. NTP provides ~1–10ms accuracy which is sufficient for most operations.

**Proposed fix options:**
1. `suppressDelayReq` flag per-controller — prevents DELAY_REQ from being sent, keeping SYNC/FOLLOW_UP reception intact (reduces accuracy but prevents stall)
2. Staggered DELAY_REQ timing — FMC sends DELAY_REQ offset +50ms after FOLLOW_UP receipt, preventing simultaneous transmission on the wire
3. Hardware timestamping on W5500 — not currently supported by the W5500 Arduino library

**Status:** Open — FW-B3. Not blocking for NTP-based operation.

### 6.2 SAMD-NTP — FMC SAMD21 NTP and USB CDC Interaction

**Problem:** FMC (SAMD21 / Arduino MKR / Zero compatible) has a hardware constraint where USB CDC and Ethernet share the same power/connector path. This means:
- Serial debug (USB CDC) and network operation are mutually exclusive in hardware — you cannot monitor serial output while the Ethernet is active
- Calling `SerialUSB.println()` or `uprintf()` while Ethernet is transmitting causes USB CDC buffer stalls
- `ntp.PrintTime()` and `ptp.PrintTime()` — which internally call formatted serial output — caused the `TIME` serial command to lock up the board completely

**Symptoms observed:** `TIME` command sent via serial → board locks, no further output, requires power cycle.

**Workaround (current):**
- `isNTP_Enabled = false` by default on FMC
- Slim `TIME` command implemented — removes all `PrintTime()` calls, prints only essential fields via short `uprintf()` calls
- Enable NTP manually via `TIMESRC NTP` for testing (USB disconnected, Ethernet connected)

**Root cause:** Likely USB CDC TX buffer overflow or interrupt conflict between USB CDC and W5500 SPI when both are active simultaneously. The SAMD21 does not have hardware serial (UART) available on the MKR/Zero pinout for this board revision — all debug output must go through USB CDC.

**Status:** Open — SAMD-NTP. Working around with isNTP_Enabled=false default.

---

## 7. Register Layout — TIME_BITS (Unified Across All Controllers)

All four embedded controllers expose an identical `TIME_BITS` byte in REG1. The byte offset differs per controller but the bit layout is identical — single decode path for all.

| Bit | Field | Notes |
|-----|-------|-------|
| 0 | isPTP_Enabled | PTP slave enabled |
| 1 | ptp.isSynched | PTP locked to grandmaster |
| 2 | usingPTP | activeTimeSource == PTP |
| 3 | ntp.isSynched | NTP locked to server |
| 4 | ntpUsingFallback | NTP on fallback server (.208) |
| 5 | ntpHasFallback | Fallback server is configured |
| 6–7 | RES | Reserved |

| Controller | TIME_BITS byte offset in REG1 |
|------------|-------------------------------|
| MCC | 253 |
| BDC | 391 |
| TMC | 61 (STATUS_BITS3) |
| FMC | 44 |

**C# decode (all controllers):**
```csharp
bool isPTP_Enabled      = (TIME_BITS & 0x01) != 0;
bool ptp_isSynched      = (TIME_BITS & 0x02) != 0;
bool usingPTP           = (TIME_BITS & 0x04) != 0;
bool ntp_isSynched      = (TIME_BITS & 0x08) != 0;
bool ntpUsingFallback   = (TIME_BITS & 0x10) != 0;
bool ntpHasFallback     = (TIME_BITS & 0x20) != 0;
```

---

## 8. W5500 Socket Budget Summary

The WIZnet W5500 has 8 hardware sockets. PTP requires 2 (udpEvent:319 + udpGeneral:320). Socket allocation must be planned carefully — exceeding 8 prevents PTP from initialising.

| Controller | Allocated | Notes |
|------------|-----------|-------|
| MCC | 8/8 | Fully allocated — no sockets spare |
| BDC | 7/8 | One spare — TRC/FMC cmd TX share udpA2 |
| TMC | 4/8 | Four spare |
| FMC | 4/8 | Four spare |

> **Key lesson (S33):** BDC originally allocated 9 sockets (TRC and FMC each opened their own socket). This prevented PTP from initialising. Fixed by sharing udpA2 pointer for TRC and FMC command TX — TX-only, single-threaded, no conflict.

---

## 9. ptpClient Class Notes

- Implements IEEE 1588 ordinary clock slave (2-step, E2E delay, multicast 224.0.1.129)
- State machine: `WAIT_SYNC → WAIT_FOLLOW_UP → WAIT_DELAY_RESP → WAIT_SYNC`
- `firstSync`: calls `setEpoch(t1)` — hard-sets to master send time (avoids epoch mismatch on first lock)
- Subsequent syncs: EMA (exponential moving average) of `offset_us`; `setEpoch(rawTime() - offset)`
- `ptp.GetCurrentTime()` free-runs continuously — even before `isSynched` — using the last known offset. The `EPOCH_MIN_VALID_US` guard in `GetCurrentTime()` (see §3.2) filters out pre-sync values that are near Unix epoch zero.
- `ptp.setDebugLevel(DEBUG_LEVELS::MIN)` enables per-sync offset/delay streaming to serial (default OFF)

---

## 10. Firmware Version Reference (Session 36 State)

| Controller | FW Version | Platform | NTP default | PTP default |
|------------|-----------|----------|-------------|-------------|
| MCC | 3.2.0 | Arduino (ATmega/SAM) | true | false |
| BDC | 3.2.0 | STM32F7 | true | false |
| TMC | 3.2.0 | Arduino (ATmega/SAM) | true | false |
| FMC | 3.2.0 | SAMD21 (Cortex-M0+) | false | false |
| TRC | 3.0.1 | Jetson Orin NX (Linux) | N/A | N/A |

---

*End of timing context document.*
