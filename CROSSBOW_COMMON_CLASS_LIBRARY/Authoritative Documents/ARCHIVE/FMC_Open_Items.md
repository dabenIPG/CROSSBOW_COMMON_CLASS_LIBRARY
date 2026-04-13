# FMC Code Review — Open Items

**Date:** 2025-12-18  
**Firmware:** FMC v2.1  
**Target:** SAMD21 (Sparkfun SAMD21 Pro RF)  

---

## Critical — Bugs

### 1. ~~FSM Position Byte-Offset Mismatch in UDP_PARSE~~ ✅ FIXED

**File:** `fmc.cpp` — `UDP_PARSE()`, case `ICD::FMC_SET_FSM_POS`

The X and Y values are read as `uint32_t` (4 bytes each) but stored into `int16_t` variables. The second read starts at offset 3, which overlaps with the first value instead of starting at offset 5.

```cpp
// Current (broken)
int16_t x = BytesToUInt32(msg, 1);  // reads 4 bytes, truncates to 16-bit
int16_t y = BytesToUInt32(msg, 3);  // overlaps with x

// Fix option A: if payload is two int16 values
int16_t x = BytesToInt16(msg, 1);
int16_t y = BytesToInt16(msg, 3);

// Fix option B: if payload is two int32 values
int32_t x = BytesToInt32(msg, 1);
int32_t y = BytesToInt32(msg, 5);
```

**Impact:** FSM position commands received over UDP are always corrupted.

---

### 2. ~~NTP Fractional Seconds Copy-Paste Typo~~ ✅ FIXED

**File:** `ntpClient.cpp` — `ConvertNtpTime()`

Byte index 5 is read as index 4, duplicating the high byte of the fraction.

```cpp
// Current (broken)
uint32_t frac = ((uint32_t)pktBuf[4] << 24) |
                ((uint32_t)pktBuf[4] << 16) |  // <-- should be pktBuf[5]
                ((uint32_t)pktBuf[6] << 8)  |
                 (uint32_t)pktBuf[7];
```

**Impact Analysis:**

The NTP fractional second is a 32-bit value where the full range (0 to 2³²−1) maps to
one second. Each byte position represents a different resolution tier:

- Byte 4 (bits 24–31): each increment ≈ 3.906 ms (1/256 of a second)
- Byte 5 (bits 16–23): each increment ≈ 15.26 µs (1/65536 of a second)

The bug copies byte 4 into byte 5's position, so the error in the fractional field is
`(pktBuf[4] − pktBuf[5]) / 65536` seconds, which gives:

- Worst case (bytes differ by 255): **±3.9 ms**
- Typical (average difference ~85): **±1.3 ms**

Each NTP sync has up to ~4 ms of jitter injected into the fractional part of every
timestamp (T1, T2, T3). Since the offset calculation uses differences of these timestamps,
the errors can compound, meaning the clock offset estimate could be off by several
milliseconds even when the network delay is negligible.

---

## High — Memory / Stability

### 3. ~~Memory Leak in `moveTo()`~~ ✅ FIXED

**File:** `fmc.cpp` — `moveTo()` / `to_hex_array()`

`to_hex_array()` allocates memory with `malloc` and the caller never calls `free`. Each stage move leaks ~10 bytes. On a SAMD21 with 32KB RAM, this will eventually crash.

**Fix:** Replace with a stack-allocated buffer and remove `to_hex_array()`.

```cpp
void FMC::moveTo(uint32_t pos) {
    char hex_string[9];
    snprintf(hex_string, sizeof(hex_string), "%08X", pos);
    // ... use hex_string directly
}
```

---

### 4. ~~SPI Bus Contention Between Ethernet, DAC, and ADC~~ ✅ FIXED

**File:** `fmc.cpp` — `writeSPI()`, `writeSPI_ADC()`, `readSPI_ADC()`, `init_FSM()`

The W5500 Ethernet chip, FSM DAC, and FSM ADC share a single SPI bus. The Ethernet library performs SPI access internally during `parsePacket` / `read` / `write`. If any overlap occurs, SPI data will be corrupted.

**Fix:** Wrapped all manual SPI transfers with `SPI.beginTransaction()` / `SPI.endTransaction()` using device-specific settings verified against datasheets:

| Device | Part | Max Clock | SPI Mode | Settings Used |
|--------|------|-----------|----------|---------------|
| DAC | AD5752R | 30 MHz | SPI_MODE1 (CPOL=0, CPHA=1) | `SPISettings(1000000, MSBFIRST, SPI_MODE1)` |
| ADC | LTC1867 | 20 MHz | SPI_MODE0 (CPOL=0, CPHA=0) | `SPISettings(1000000, MSBFIRST, SPI_MODE0)` |

Note: The two devices require different SPI modes, which makes `beginTransaction` / `endTransaction` essential — each transaction reconfigures the SPI peripheral for the correct clock phase.

---

### 5. ~~`EthernetUDP` Passed by Value~~ ✅ FIXED

**File:** `fmc.hpp` / `fmc.cpp` — `SEND_REG_01()`, `SEND_REG_02()`

The UDP object is copied on every call. This is wasteful and may cause socket aliasing issues.

**Fix:** Change signatures to pass by reference:

```cpp
void SEND_REG_01(EthernetUDP &client, IPAddress ipRemote, uint32_t portRemote);
void SEND_REG_02(EthernetUDP &client, IPAddress ipRemote, uint32_t portRemote);
```

---

## Medium — Correctness / Robustness

### 6. ~~Missing Include Guard in `fmc.hpp`~~ ✅ FIXED

**File:** `fmc.hpp`

All other headers use `#pragma once` but `fmc.hpp` does not. Will cause redefinition errors if included from multiple translation units.

**Fix:** Add `#pragma once` at the top of the file.

---

### 7. ~~Pin `digitalWrite` Before `pinMode` in `setup()`~~ ✅ FIXED

**File:** `FMC.ino` — `setup()`

`digitalWrite()` is called before `pinMode(OUTPUT)` for WIZ_RST, WIZ_CS, FSM_PWR_EN, FSM_CS_DAC, and FSM_CS_ADC. On SAMD21 this enables the internal pull-up on an input pin, then switches to output, leaving the pin in an undefined state momentarily.

**Fix:** Call `pinMode()` first, then `digitalWrite()`.

---

### 8. ~~Inconsistent Debug Level Checks~~ ✅ FIXED

**File:** `fmc.cpp` — `UDP_PARSE()`

Some cases use `>= DEBUG_LEVELS::OFF` (always true — messages always print) while others use `> DEBUG_LEVELS::OFF` (only prints when debug is enabled). This appears unintentional.

**Affected cases using `>= OFF` (always print):**
- `SET_UNSOLICITED`
- `FMC_FSM_TEST_SCAN`
- `FMC_SET_FSM_POW`
- `FMC_STAGE_CALIB`
- `FMC_SET_STAGE_ENABLE`
- `FMC_READ_FSM_POS`

**Fix:** Audit each case and change to `> DEBUG_LEVELS::OFF` where the message should be suppressed at the OFF level.

---

### 9. ~~Stage I2C Response Parsing Has No Validation~~ ✅ FIXED

**File:** `fmc.cpp` — `readPos()`

The response from the stage controller was parsed by slicing fixed character positions with no length validation. Additionally, `Stage_Status` and `Stage_Err` were parsed but never stored into their member variables.

**Fix:** Added minimum length check (`na >= 30`) per the M3-LS ICD command `<10>` response format `<10 SSSSSS PPPPPPPP EEEEEEEE>\r`. Short responses are now drained and logged. All three fields (`Stage_Status`, `Stage_Pos`, `Stage_Err`) are now populated using `strtoul`.

---

## Low — Minor

### 10. ~~Serial Debug Message Disagrees With Actual Value~~ ✅ FIXED

**File:** `FMC.ino` — `parseSerialInput()`, case `'2'`

Message says "Move Stage to 20000" but calls `fmc.moveTo(29000)`.

---

### 11. ~~`scan()` Uses Approximate Pi~~ ✅ FIXED

**File:** `fmc.cpp` — `scan()`

Uses `3.14` instead of `M_PI` or Arduino's `PI` constant.

---

### 12. ~~`VERSION` Bitfield Layout Is Implementation-Defined~~ ⊘ NOT A PROBLEM

**File:** `version.hpp`

The bitfield packing order in the `VERSION` struct is compiler/architecture-dependent. If `GetVersionWord()` is ever decoded on a different platform, the fields may be in a different order.

**Note:** Not a problem as long as encoding and decoding happen on the same toolchain.

---
---

## Performance / Blocking — Future Work

*Identified during final review. These are not bugs but could affect system responsiveness.*

---

### 13. `scan()` Blocks the Main Loop for ~3.6 Seconds

**File:** `fmc.cpp` — `scan()`

**Trigger:** UDP command `ICD::FMC_FSM_TEST_SCAN` or serial `'s'`

The scan function runs a tight loop of 361 iterations with `delay(10)` each, totaling ~3.6 seconds. During this time there is no UDP processing, no heartbeat, no stage polling, and no NTP sync — the system goes completely dark to the remote host.

**Recommendation:** If this is only used for bench testing, document that constraint. If it could be triggered during operation, refactor to a non-blocking state machine that advances one point per `UPDATE()` call using the existing tick-timer pattern.

---

### 14. `init_FSM()` Blocks for ~3.4 Seconds

**File:** `fmc.cpp` — `init_FSM()`

**Trigger:** Startup, or serial `'i'` command (which adds another `delay(100)`)

Contains `delay(1000)` + `delay(100)` x2 + `delay(2000)` totaling 3.3+ seconds. Acceptable at startup but stalls the entire system if called during operation via serial re-init.

**Recommendation:** Acceptable for current use. If remote re-init is ever added, this would need to be made non-blocking.

---

### 15. `readPos()` I2C Clock Stretching Can Block Indefinitely

**File:** `fmc.cpp` — `readPos()` → `Wire.requestFrom()`

**Trigger:** Every 100 ms via `checkStagePos()` when stage is enabled

Per the M3-LS ICD, the stage holds the I2C clock line low until its response is ready. `Wire.requestFrom()` blocks until the clock is released. If the stage is busy (e.g., mid-calibration, which moves the carriage up to 250 µm) or unresponsive, this blocks the entire loop for an unbounded duration.

**Recommendation:** The SAMD21 Wire library does not have a built-in timeout for clock stretching. Consider only polling the stage when it is known to be idle, or implementing a software watchdog that resets the I2C bus if `readPos()` takes too long.

---

### 16. `init_FSM()` Holds SPI Transaction Open Across `delay(100)`

**File:** `fmc.cpp` — `init_FSM()`

**Trigger:** Startup, or serial `'i'` command

Both DAC config transfers call `SPI.beginTransaction()`, then `delay(100)`, then `SPI.endTransaction()`. During that 100 ms window per transfer (200 ms total), the Ethernet library is locked out of SPI.

**Recommendation:** Move the `delay(100)` after `SPI.endTransaction()` so the bus is released before the settling delay:

```cpp
SPI.beginTransaction(SPISettings(1000000, MSBFIRST, SPI_MODE1));
digitalWrite(FSM_CS_DAC, LOW);
SPI.transfer(0b00001100);
SPI.transfer(0x00);
SPI.transfer(0b00000100);
digitalWrite(FSM_CS_DAC, HIGH);
SPI.endTransaction();
delay(100);  // settle after releasing bus
```

---

### 17. `micros()` Rollover Causes Momentary NTP Timestamp Jump

**File:** `ntpClient.cpp` — `GetCurrentTime()`

**Trigger:** Every ~71.6 minutes

`microsEpoch` is a `uint32_t` storing `micros()`, which rolls over every ~71.6 minutes. At rollover, `GetCurrentTime()` computes `(micros() - microsEpoch)` which wraps to a large value, causing a ~4295 second forward jump in the reported timestamp. The clock self-corrects on the next NTP sync (every 10 seconds), but heartbeat register timestamps will be wrong during that window.

**Recommendation:** Detect rollover by checking if `micros() < microsEpoch` and force an immediate NTP re-sync, or promote `microsEpoch` tracking to handle the wraparound.

---

### 18. Aggregate Loop I/O Load

**File:** `fmc.cpp` — `UPDATE()`

**Trigger:** Every loop iteration

The main loop performs per-cycle: UDP parse (SPI to W5500), FSM ADC read every 50 ms (2x SPI transactions), stage I2C poll every 100 ms, NTP send every 10 s (SPI to W5500), and unsolicited heartbeat every 20 ms (SPI to W5500). This is a significant amount of bus traffic per cycle.

**Recommendation:** Not a problem currently, but monitor `dt_delta` in the heartbeat register. If it grows beyond a few milliseconds, consider staggering the I/O so that ADC reads and stage polls don't land on the same loop iteration as the heartbeat send.
