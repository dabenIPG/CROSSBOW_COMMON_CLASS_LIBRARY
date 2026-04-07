# CROSSBOW Firmware — Style & Patterns Reference

**Version:** 1.7 session 7 — 2026-03-10  
**Purpose:** Ground-truth reference for consistent implementation across all five controllers.  
Established by TMC (sessions 1–5), confirmed and extended by MCC (sessions 6–7).

---

## Hardware Platform

| Controller | MCU | Board Package | Serial |
|------------|-----|---------------|--------|
| TMC | STM32F767ZGT6 | OpenCR (Arduino) | `Serial` — hardware UART via USB-serial chip |
| MCC | STM32F767ZGT6 | OpenCR (Arduino) | `Serial` — hardware UART via USB-serial chip |
| FMC | SAMD21 (Cortex-M0+) | Arduino SAMD (MKR/Zero-compatible) | `SerialUSB` — native USB CDC |
| BDC | STM32F767ZGT6 | OpenCR (Arduino) | `Serial` — hardware UART via USB-serial chip |
| TRC | Jetson Orin | Linux / C++17 | stdout / syslog |

> ⚠ FMC is **SAMD21**, not STM32. All FMC-specific patterns (SerialUSB, uprintf, TSENS) are in the SAMD21 section below. The OpenCR/.ino patterns apply to TMC and MCC only.

---

## Network Addresses

| Device | IP |
|--------|----|
| MCC | 192.168.1.10 |
| TMC | 192.168.1.12 |
| BDC | 192.168.1.20 |
| Gimbal | 192.168.1.21 |
| TRC | 192.168.1.22 |
| FMC | 192.168.1.23 |
| NTP | 192.168.1.33 |
| HYPERION | 192.168.1.206 |
| THEIA | 192.168.1.208 |

---

## Shared Headers (include in all controllers)

```
crc.hpp          — CRC-16/CCITT
frame.hpp        — magic, STATUS codes, ports, FrameClient, frame helpers
version.h        — VERSION_PACK(), VERSION_MAJOR/MINOR/PATCH(), FW_BUILD_DATE/TIME
ntpClient.hpp/cpp — NtpClient with ForceSync()
```

`#include "crc.hpp"` must precede `#include "frame.hpp"`.

---

## Firmware Version

```cpp
// In controller .ino — single definition:
const uint32_t FW_VERSION = VERSION_PACK(3, 0, 0);

// In controller .hpp — extern declaration:
extern const uint32_t FW_VERSION;   // defined in CONTROLLERNAME.ino

// In controller .cpp — use FW_VERSION directly, never pass as argument
```

**Never** store `FW_VERSION` as a class member or pass it to `INIT()`.  
The `extern` in the header makes it available everywhere the header is included.

| Increment | When |
|-----------|------|
| patch | Bug fix, no register change |
| minor | New fields in reserved headroom |
| major | Breaking register layout change |

---

## .INO Structure — STM32/OpenCR controllers (TMC, MCC)

All STM32/OpenCR controllers follow this exact file structure and variable naming.
FMC (SAMD21) and BDC (Teensy) have platform-specific variations — see their sections below.

### Global declarations (top of .ino)
```cpp
#include <ctime>
#include <cstring>
#include <Ethernet.h>
#include "controller.hpp"

const uint32_t FW_VERSION = VERSION_PACK(major, minor, patch);

// ************************ BLINK STUFF *************************
int led_pin_user[5] = { BDPIN_LED_USER_1, BDPIN_LED_USER_2, BDPIN_LED_USER_3,
                         BDPIN_LED_USER_4, BDPIN_LED_USER_5 };
int cnt = 0;
bool ledOn = true;
uint32_t lastTick = 0;
uint32_t TICK = 200;
// **************************************************************

// ************************ ETHERNET STUFF *************************
byte MAC[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xFE, 0xXX };  // unique per board
IPAddress IP_CTRL(192, 168, 1, XX);

// ************************ SERIAL HANDLING *************************
String serialBuffer = "";

// ************************ MCU TEMPS *************************
uint32_t lastTick_mcuTemp = 0;
uint32_t TICK_mcuTemp = 1999;
float tempCalibrated = 25.25;

extern "C" {
  #include "stm32f7xx_hal.h"
  #include "stm32f7xx_hal_adc.h"
}
#ifndef ADC_CCR_TSEN
  #define ADC_CCR_TSEN ((uint32_t)0x00800000)
#endif
#define TEMP30_CAL_ADDR  ((uint16_t*) 0x1FF0F44C)
#define TEMP110_CAL_ADDR ((uint16_t*) 0x1FF0F44E)
#define VREF      3.3
#define AVG_SLOPE 2.5
#define V25       0.76

ADC_HandleTypeDef      hadc1;
ADC_ChannelConfTypeDef sConfig;

// ************************ CONTROLLER SPECIFIC *************************
CONTROLLER ctrl;
```

### setup() order — must match exactly
```cpp
void setup()
{
  pinMode(PIN_WIZ_RESET, INPUT);

  initADC();       // MUST be first — HAL corrupts timer state if called after Wire/Serial/Ethernet
  delay(1000);

  Wire.begin();

  // pinMode for all LEDs
  // pinMode/digitalWrite for all GPIO (set safe state before OUTPUT)

  Serial.begin(115200);
  Serial.println(F("START!"));

  Serial.println(F("Starting Ethernet"));
  Ethernet.begin(MAC, IP_CTRL);
  Ethernet.setRetransmissionCount(0);
  Ethernet.setRetransmissionTimeout(2);

  Serial.println(F("Delayed Start 10s ... for power seq"));
  delay(10000);

  Serial.println(F("Starting CONTROLLER"));
  ctrl.INIT();
  // NO second initADC() call after INIT — not needed, causes issues
}
```

### loop() order
```cpp
void loop()
{
  handleSerialInput();
  ctrl.UPDATE();
  readMCUTemp();
  blink();
}
```

### Support functions — copy verbatim from TMC.ino, change ctrl name only

`handleSerialInput()`, `parseSerialCommand()`, `handleCommand()`, `blink()`,  
`readMCUTemp()`, `initADC()`, `readTempADC()`, `readTemperatureCalibrated()`,  
`readTemperatureTypical()`, `getRawTempADC()`, `printSensorInfo()`

**These functions are identical across all OpenCR controllers.**  
Only `handleCommand()` changes: hardware-specific commands handled inline,  
everything else routed to `ctrl.SERIAL_CMD(command, payload)`.

---

## CRITICAL: Serial Printf Float Rule (OpenCR/STM32)

**`Serial.printf()` with `%f` / `%e` / `%g` format specifiers is UNSAFE on OpenCR/STM32.**

Any multi-byte UTF-8 character (`°` U+00B0, `µ` U+00B5, `×` U+00D7, `—` U+2014,
box-drawing characters, etc.) in a `Serial.printf()` format string corrupts the
va_args stack. The corruption manifests as:
- All float arguments print as `0.00`
- Or all float arguments print as astronomically large garbage values

**The fix — use `Serial.print(val, precision)` for all floats:**

```cpp
// WRONG — never do this on OpenCR:
Serial.printf("Temp: %.2f C\n", tempMCU);
Serial.printf("Temp: %.2f °C\n", tempMCU);   // UTF-8 degree symbol corrupts va_args

// CORRECT — TMC/MCC pattern:
Serial.print(F("Temp: ")); Serial.print(tempMCU, 2); Serial.println(F(" C"));
```

`Serial.printf()` with `%u`, `%d`, `%s`, `%X`, `%lu` (integer/string) is safe.  
`Serial.print(val, 2)` is always safe for floats — use this exclusively.

**Source files must be pure ASCII.** Verify with:
```bash
python3 -c "
data = open('controller.cpp','rb').read()
bad = [(i+1,b) for i,b in enumerate(data) if b > 127]
print(f'{len(bad)} non-ASCII bytes' if bad else 'Clean')
"
```

---

## CRITICAL: REG1 Buffer Size Must Match FRAME_PAYLOAD_SIZE

`frameBuildResponse()` always copies exactly `FRAME_PAYLOAD_SIZE` (512) bytes
from the payload buffer, regardless of how much data is actually populated.

```cpp
// WRONG — causes read past end of buffer, corrupts stack on every SEND_UNSOLICITED tick:
static uint8_t buf[256];  // only 256 bytes but frameBuildResponse reads 512

// CORRECT:
#define MCC_REG1_SIZE  512   // must match FRAME_PAYLOAD_SIZE
static uint8_t buf[MCC_REG1_SIZE];
memset(buf, 0, sizeof(buf));   // zero-init so unused bytes are 0x00
// ... write only the fields you need, rest stays 0x00 ...
frameBuildResponse(frame, MAGIC_INT_HI, MAGIC_INT_LO, seq, cmd, STATUS_OK, buf);
```

Every controller's payload buffer must be `FRAME_PAYLOAD_SIZE` (512) bytes.

---

## CRITICAL: No memset on Non-Existent Members

Do not copy `memset(reg_payloadXXX, 0, ...)` from another controller's constructor
without verifying the member exists in the new controller's class.  
If the member does not exist, the compiler resolves to an arbitrary symbol and
writes zeros into unrelated memory, corrupting all float members at construction time.

**Pattern — constructors are empty on OpenCR:**
```cpp
// controller.cpp
CONTROLLER::CONTROLLER() {}   // empty — OpenCR initializes globals correctly
```

Zero-init belongs in `SEND_REG_01()` on the local stack buffer, not the constructor.

---

## SERIAL_CMD Pattern

### Style rules (match TMC exactly)
- Use `Serial.print(val, 2)` + `Serial.println(F(" C"))` for all floats — never `printf %f`
- Section headers use `F("--- LABEL ---...")` dashes, not box-drawing characters
- `INFO` block opens and closes with `F("###...###")` separator lines
- `HELP`/`?` uses `+== ... ==+` / `|  ...  |` ASCII box (no UTF-8 box chars)
- Unknown command: `Serial.printf("Unknown command: %s  (type ? for help)\n", cmd.c_str())`

### Standard commands — every controller implements all of these

| Command | Action |
|---------|--------|
| `?` / `HELP` | ASCII box command list |
| `INFO` | FW version, IP, link, port/client status |
| `REG` | Full REG1 dump via `PRINT_REG()` |
| `STATUS` | System state, mode, all bit fields decoded |
| `TEMPS` | All temperature sensors |
| `NTP` | Sync status, epoch, UTC |
| `NTPIP <a.b.c.d>` | Set NTP IP + `ntp.INIT()` + `ntp.SendTimeRequest()` |
| `DEBUG <0-3>` | Set debug level |
| `STATE <n>` | `StateManager()` mirror |
| `MODE <n>` | `BDC_Mode` mirror |

Controller-specific liveness commands (e.g., `TMC` on MCC, `FMC`/`TRC` on BDC)
dump buffer contents and connection state — see MCC `TMC` command as template.

### TEMPS command — use Serial.print for all floats
```cpp
if (cmd == "TEMPS")
{
  Serial.println(F(""));
  Serial.println(F("--- CONTROLLER TEMPERATURES --------------------------------"));
  Serial.print(F("  TPH  (ambient):     ")); Serial.print(tph.GetTemperature(), 2); Serial.println(F(" C"));
  Serial.print(F("  TPH  (pressure):    ")); Serial.print(tph.GetPressure(),    1); Serial.println(F(" Pa"));
  Serial.print(F("  TPH  (humidity):    ")); Serial.print(tph.GetHumidity(),    1); Serial.println(F(" %"));
  Serial.print(F("  MCU  (die temp):    ")); Serial.print(tempMCU, 2);              Serial.println(F(" C"));
  Serial.println(F("------------------------------------------------------------"));
  Serial.println(F(""));
  return;
}
```

### INFO command structure
```cpp
if (cmd == "INFO")
{
  Serial.println(F("###########################################################################"));
  Serial.printf("CTRL  v%u.%u.%u  built %s %s\n",
      VERSION_MAJOR(FW_VERSION), VERSION_MINOR(FW_VERSION), VERSION_PATCH(FW_VERSION),
      FW_BUILD_DATE, FW_BUILD_TIME);
  Serial.print  (F("IP:            ")); Serial.println(Ethernet.localIP());
  Serial.printf ("Link:          %s\n", Ethernet.linkStatus() == LinkON ? "UP" : "DOWN");
  // A1 RX (if controller receives A1):
  Serial.printf ("A1 listen:     port %u  source alive: %s  last: %u ms ago\n", ...);
  // A1 TX dest (if controller sends A1):
  Serial.printf ("A1 dest:       %u.%u.%u.%u : %u\n", ...);
  // A2 always:
  Serial.printf ("A2 listen:     port %u  clients: %u / %u\n", PORT_A2, a2cnt, A2_MAX_CLIENTS);
  // per active client:
  Serial.printf ("             [%u] %u.%u.%u.%u:%u  last %u ms ago\n", ...);
  // A3 if applicable:
  Serial.printf ("A3 listen:     port %u  clients: %u / %u\n", PORT_A3, a3cnt, A3_MAX_CLIENTS);
  Serial.println(F("###########################################################################"));
  return;
}
```

---

## PRINT_REG() Pattern

Read live member variables — call any time, no side effects.

```cpp
void CONTROLLER::PRINT_REG()
{
  Serial.println(F(""));
  Serial.println(F("+=============================================================+"));
  Serial.println(F("|      CONTROLLER REG1  -  ICD v1.7 session 4                |"));
  Serial.println(F("+=============================================================+"));

  // Integer fields: Serial.printf is fine
  Serial.printf(" [0]    CMD BYTE:    0x%02X\n", (uint8_t)ICD::GET_REGISTER1);
  Serial.printf(" [3-4]  HB_ms:       %u ms\n",  HB_ms);

  // Float fields: always Serial.print(val, precision)
  Serial.print(F(" [XX-XX] TPH Temp:  ")); Serial.print(tph.GetTemperature(), 2); Serial.println(F(" C"));
  Serial.print(F(" [XX-XX] MCU Temp:  ")); Serial.print(tempMCU, 2);              Serial.println(F(" C"));

  // Version word — integer, printf is fine
  Serial.printf(" [XX-XX] VERSION:    0x%08X  (v%u.%u.%u)\n",
      FW_VERSION,
      VERSION_MAJOR(FW_VERSION), VERSION_MINOR(FW_VERSION), VERSION_PATCH(FW_VERSION));

  Serial.println(F("+-------------------------------------------------------------+"));
  Serial.println(F(""));
}
```

---

## Tri-Port Architecture

| Port | Label | # | Magic | Direction |
|------|-------|---|-------|-----------|
| A1 | Internal Unsolicited | 10019 | `0xCB 0x49` | TMC→MCC, FMC→BDC, TRC→BDC, MCC→BDC, BDC→TRC |
| A2 | Internal Engineering | 10018 | `0xCB 0x49` | RX+TX all controllers + GUI |
| A3 | External | 10050 | `0xCB 0x58` | RX+TX: MCC, BDC only |

**Per-controller A1 breakdown:**

| Controller | A1 |
|------------|----|
| TMC | TX → MCC |
| FMC | TX → BDC |
| TRC | TX → BDC; RX ← BDC |
| MCC | RX ← TMC; TX → BDC (fire control vote bits) |
| BDC | RX ← FMC, TRC, MCC; TX → TRC (fire control status, state, mode — 4 bytes, mirrors MCC→BDC pattern) |

Internal magic (`0xCB 0x49`) is confidential — not in external docs.

### A1 Liveness
Clear DEVICE_READY bit if no A1 packet within `A1_TMC_TIMEOUT_MS` (200 ms).  
Recovers automatically when stream resumes.  
Applies to all A1 receivers: MCC (watches TMC), BDC (watches FMC, TRC, MCC), TRC (watches BDC).

### A1 Lightweight Packets (MCC→BDC and BDC→TRC)

Not all A1 packets are full 64-byte register blocks. MCC→BDC and BDC→TRC are small
fire control / state broadcast packets — same framing, minimal payload:

| Byte | Field | Notes |
|------|-------|-------|
| 0 | CMD BYTE | `0xAB` SET_BCAST_FIRECONTROL_STATUS |
| 1 | voteBitsMcc | MCC vote bits |
| 2 | voteBitsBdc | BDC geometry vote bits |
| 3 | System State | SYSTEM_STATES enum |
| 4 | System Mode | BDC_MODES enum |

**5 bytes total.** No CRC or framing overhead beyond the standard A1 frame wrapper.  
Receiver parses CMD byte first to distinguish from a full REG1 block (CMD = `0xA1`).

### A2/A3 Client Tables
- A2: up to 4 clients, 60-second liveness timeout
- A3: up to 2 clients, same timeout
- `0xA0 {0x01}` registers, `0xA0 {0x00}` deregisters
- **On re-registration: reset `a2_seq_init = false`** — prevents replay lockout

---

## Frame Format

### Request (client → controller)
```
[0-1]   MAGIC
[2]     SEQ_NUM   uint8 rolling
[3]     CMD_BYTE
[4-5]   PAYLOAD_LEN uint16 LE
[6+]    PAYLOAD
[last2] CRC-16 BE  (over bytes 0 through end of PAYLOAD)
```

### Response (controller → client) — always 521 bytes
```
[0-1]   MAGIC
[2]     SEQ_NUM
[3]     CMD_BYTE
[4]     STATUS    0x00=OK
[5-6]   PAYLOAD_LEN  always 0x0200 (512)
[7-518] PAYLOAD   512 bytes, 0x00-padded
[519-520] CRC-16 BE
```

### handleA2Frame() 5-step rejection chain
```cpp
if (nb < FRAME_MIN_REQ) return;
if (!frameCheckMagic(...)) { sendResponse(STATUS_BAD_MAGIC); return; }
if (!frameCheckLen(...))   { sendResponse(STATUS_BAD_LEN);   return; }
if (!crc16_check(...))     { sendResponse(STATUS_BAD_CRC);   return; }
// A3 only: whitelist check
if (!inWhitelist(cmd))     { sendResponse(STATUS_CMD_REJECTED); return; }
if (frameCheckReplay(...)) { sendResponse(STATUS_SEQ_REPLAY); return; }
frameAcceptSeq(...);
frameClientHeard(...);
UDP_PARSE(cmd, payload, plen, seq, srcIP, srcPort);
```

---

## REG1 Buffer Encoding

```cpp
// int8 temperature
buf[N] = (int8_t)constrain((int)roundf(val), -128, 127);

// uint8 scaled (e.g. flow x10)
buf[N] = (uint8_t)constrain((int)roundf(val * 10.0f), 0, 255);

// uint16 centi-units
uint16_t v = (uint16_t)constrain((int)(val * 100.0f), 0, 65535);
memcpy(buf + N, &v, 2);

// multi-byte — always memcpy, never pointer cast
memcpy(buf + N, &val, sizeof(val));
```

VERSION_WORD offsets:

| Controller | Offset |
|------------|--------|
| TRC | 1 |
| FMC | 36 |
| TMC | 53 |
| MCC | 245 |
| BDC | 383 |

---

## NTP Client

```cpp
// INIT
ntp.INIT(IP_NTP);

// UPDATE — rate-gated to NTP_TICK_MS (10 s)
ntp.SendTimeRequest(&udpA2);

// UDP intercept — before frame parser
if (srcPort == ntp.timeServerPort && srcIP == ntp.timeServerIP && nb == NTP_PACKET_SIZE)
{
    ntp.ProcessPacket(&udpA2, nb);
    return;
}

// Runtime IP change — use INIT + SendTimeRequest (not ForceSync, may not exist on all builds)
ntp.INIT(newIP);
ntp.SendTimeRequest(&udpA2);

// REG1 epoch field (ms from us)
uint64_t epochMs = ntp.GetCurrentTime() / 1000ULL;
memcpy(buf + OFFSET_NTP, &epochMs, 8);
```

---

## Debug Levels

```cpp
enum class DEBUG_LEVELS : uint8_t { OFF = 0, MIN = 1, NORM = 2, VERBOSE = 3 };
DEBUG_LEVELS CONTROLLER_DEBUG_LEVEL = DEBUG_LEVELS::OFF;

// Usage
if (CONTROLLER_DEBUG_LEVEL >= DEBUG_LEVELS::NORM)
    Serial.printf("...\n");
```

---

## Known Bugs Fixed — Apply to All Controllers

### 1. memset on non-existent member (constructor)
**Symptom:** All float members show garbage values from boot.  
**Cause:** `memset(reg_payloadXXX, 0, sizeof(reg_payloadXXX))` in constructor where
the member does not exist in this controller's class. Compiler resolves to wrong symbol,
zeros arbitrary memory.  
**Fix:** Empty constructor `CONTROLLER::CONTROLLER() {}`.

### 2. Serial.printf float corruption (UTF-8 in format string)
**Symptom:** All floats print as 0.00 or astronomically large numbers.  
**Cause:** Any multi-byte UTF-8 character in a `Serial.printf` format string corrupts
the va_args stack on OpenCR/STM32. Even `°` (0xC2 0xB0), `µ` (0xC2 0xB5), `×` (0xC3
0x97), `—` (0xE2 0x80 0x94), or box-drawing characters.  
**Fix:** Source files must be pure ASCII. Use `Serial.print(val, 2)` for all floats.

### 3. REG1 buffer smaller than FRAME_PAYLOAD_SIZE
**Symptom:** Periodic memory corruption — members near the buffer are overwritten
on every SEND_UNSOLICITED tick.  
**Cause:** `frameBuildResponse()` reads exactly 512 bytes from the payload buffer.
If the buffer is 256 bytes, 256 bytes past the end are read and appear in the frame.
On STM32 with static locals, those trailing bytes may be shared stack or adjacent statics.  
**Fix:** `#define CONTROLLER_REG_SIZE  512` — always match `FRAME_PAYLOAD_SIZE`.

### 4. a2_seq_init not reset on re-registration (session 29 — expanded fix)
**Symptom:** Reconnecting GUI permanently locked out — all frames rejected as replay until controller reboot.
**Root cause:** `isNewClient` detection and `a_seq_init = false` were placed **after** `frameCheckReplay()`. Reconnecting client's first frame was rejected before the window could be reset.
**Fix:** Move new client detection **before** `frameCheckReplay()` in all six handlers:
```cpp
// CORRECT ORDER — new client detection before replay check:
bool   isNewClient = (frameClientFind(...) == -1);
int8_t clientIdx   = frameClientRegister(...);
if (isNewClient && clientIdx >= 0)
    a2_seq_init = false;   // reset replay window for reconnecting client

if (frameCheckReplay(seq, a2_last_seq, a2_seq_init)) { ... return; }
frameAcceptSeq(seq, a2_last_seq, a2_seq_init);
```
**Applies to:** MCC `handleA2Frame`, MCC `handleA3Frame`, BDC `handleA2Frame`, BDC `handleA3Frame`, TMC `handleA2Frame`, FMC `handleA2Frame`. All fixed session 29.

---

## C# Engineering GUI Patterns (session 29 — standardized fleet-wide)

### Connection class
- Port: 10018 (A2), magic `0xCB 0x49`; port 10050 (A3), magic `0xCB 0x58`
- `_seq` = `(byte)new Random().Next(33, 224)` on each `Start()`
- **Single `0xA4` registration on connect** — burst (`×3`) retired (firmware replay fix session 29)
- Set `_lastKeepalive = DateTime.UtcNow` immediately after registration send
- `PeriodicTimer` keepalive every 30s — calls `SendKeepalive()` directly (no elapsed check)
- `SendKeepalive()` sends `0xA4 FRAME_KEEPALIVE` and sets `_lastKeepalive = DateTime.UtcNow`
- **`_lastKeepalive` updated only in `SendKeepalive()`** — never in `Send()` — ensures reliable 30s interval
- A3 connect: same single `0xA4` registration — no auto `0xA0` subscribe (user controls via checkbox)

### Receive loop liveness
- **Any valid frame** (any CMD_BYTE) updates `isConnected`, `HB_RX_ms`, `lastMsgRx`
- `0xA1` frames additionally call `LatestMSG.Parse()`
- **`connection established`** logged immediately on first valid frame in receive loop — not in KeepaliveLoop
- `connection restored` logged in KeepaliveLoop (requires `_dropCount > 0` context)

### Message parser
- Validate magic `[0]==0xCB`, `[1]==0x49` (A2) or `[1]==0x58` (A3)
- Validate CRC-16/CCITT over bytes 0–518
- Check `frame[4] == STATUS_OK`
- Check `frame[3] == 0xA1` before calling `LatestMSG.Parse()`

### VERSION_WORD decode
```csharp
uint major = (fw >> 24) & 0xFF;
uint minor = (fw >> 12) & 0xFFF;
uint patch =  fw        & 0xFFF;
```

---

*CROSSBOW FW Patterns Reference — session 9 (platform corrections, SAMD21, Teensy, hpp/cpp principles) — 2026-03-10*

---

## .INO Structure — SAMD21 (FMC)

Key differences from the OpenCR/STM32 pattern:

| Aspect | OpenCR/STM32 (TMC, MCC) | SAMD21 (FMC) |
|--------|------------------------|--------------|
| Serial port | `Serial` | `SerialUSB` — native USB CDC |
| printf floats | `Serial.printf(%f)` unsafe — use `Serial.print(val,2)` | `SerialUSB.printf()` does not exist — use `uprintf()` helper |
| MCU temperature | STM32 HAL ADC1 + factory cal registers | SAMD21 TSENS peripheral (§40) — stubbed at 25 °C pending impl |
| `while(!Serial)` guard | Never uncomment — stalls if USB not connected | Same — never uncomment |
| Ethernet retransmit timeout | 2 ms | 1 ms (WIZnet on SAMD) |
| `Serial.printf` integers | Safe | Not available — use `uprintf()` for everything |
| `delay.h` | Not needed | `#include "delay.h"` required |

### uprintf helper — required for all formatted output on SAMD21

```cpp
// fmc.cpp — file-scope static helper
#include <stdarg.h>
static void uprintf(const char* fmt, ...)
{
  char buf[128];
  va_list args;
  va_start(args, fmt);
  vsnprintf(buf, sizeof(buf), fmt, args);
  va_end(args);
  SerialUSB.print(buf);
}
```

Use `uprintf()` everywhere you would use `Serial.printf()` on TMC/MCC.
Use `SerialUSB.print()` / `SerialUSB.println()` for all non-formatted output.

### SAMD21 .ino structure

```cpp
// FMC.ino
#include <ctime>
#include <cstring>
#include <Ethernet.h>
#include "Arduino.h"
#include "fmc.hpp"

const uint32_t FW_VERSION = VERSION_PACK(major, minor, patch);

uint8_t FSM_POW_ON  = HIGH;   // FMC-specific externs
uint8_t FSM_POW_OFF = LOW;

// ── LED blink ─────────────────────────────────────────────────
int led_pin_user[4] = {LED_0, LED_1, LED_2, LED_3};
int cnt = 0; bool ledOn = true;
uint32_t lastTick_Blink = 0; uint32_t TICK_Blink = 200;

// ── Ethernet ──────────────────────────────────────────────────
byte MAC[] = {0xDE, 0xAD, 0xBE, 0xEF, 0xFE, 0x23};
IPAddress IP_FMC(192, 168, 1, 23);

// ── Serial input buffer ───────────────────────────────────────
String serialBuffer = "";

// ── MCU temperature ───────────────────────────────────────────
uint32_t lastTick_mcuTemp = 0;
uint32_t TICK_mcuTemp     = 2000;

FMC fmc;

void setup()
{
  // GPIO setup ...
  SerialUSB.begin(115200);
  // NOTE: do NOT add while(!SerialUSB) — stalls if USB not connected
  SerialUSB.println(F("START!"));

  Ethernet.init(WIZ_CS);
  Ethernet.begin(MAC, IP_FMC);
  Ethernet.setRetransmissionCount(0);
  Ethernet.setRetransmissionTimeout(1);

  fmc.INIT();   // no port arg — A2 always binds on PORT_A2
}

void loop()
{
  handleSerialInput();
  fmc.UPDATE();
  readMCUTemp();
  blink();
}

void readMCUTemp()
{
  if (millis() - lastTick_mcuTemp < TICK_mcuTemp) return;
  // TODO: implement SAMD21 TSENS peripheral readout
  fmc.tempMCU = 25.0f;   // stub — 25 °C placeholder
  lastTick_mcuTemp = millis();
}
```

### SAMD21 MCU temperature — TSENS peripheral (pending implementation)

SAMD21 exposes a die temperature sensor via the TSENS peripheral (datasheet §40).
Not accessible via `analogRead()`. Requires:
1. Read calibration values from NVM Software Calibration Area (§9.5):
   - `TSENS_GAIN`   at `NVM_SW_CALIB_AREA + 0`
   - `TSENS_OFFSET` at `NVM_SW_CALIB_AREA + 4`
2. Perform single ADC conversion on channel 0x18 (internal temperature)
3. Apply calibration formula from datasheet

Currently stubbed at 25 °C in `FMC.ino readMCUTemp()`.

---

## .INO Structure — STM32/OpenCR (BDC)

BDC runs on the same STM32F767ZGT6/OpenCR platform as TMC and MCC. **Follow the TMC/MCC `.ino` pattern exactly** — same `initADC()`, same MCU temperature HAL pattern, same `Serial.print(val,2)` float rule, same `setup()` / `loop()` order.

Key BDC-specific differences from TMC/MCC:

| Aspect | TMC / MCC | BDC |
|--------|-----------|-----|
| A1 direction | TX only (sends up) | **RX only** (receives from FMC + TRC) |
| A2 | RX + TX | RX + TX |
| A3 | MCC only | RX + TX |
| Sub-controller blocks | MCC embeds TMC (64 bytes) | BDC embeds TRC (bytes 60–123) + FMC (bytes 169–232) |
| REG1 size | 512 bytes | 512 bytes — same |
| MAC / IP | unique per board | `192.168.1.20` |

> ⚠ BDC is the most complex controller — **three A1 sources** (FMC at .23, TRC at .22, MCC at .10), three ports total, two embedded sub-blocks to parse and re-embed in REG1, and A1 TX down to TRC. Use MCC as the primary template.

---

## HPP / CPP Driver Design Principles

These conventions are established by TMC and MCC and must be followed in all controllers.

### .hpp — class declaration rules

```cpp
#pragma once
#include <Ethernet.h>
#include "Arduino.h"
#include "defines.hpp"
#include "conversion.hpp"
#include "ntpClient.hpp"
#include "frame.hpp"      // magic, ports, STATUS codes, helpers
#include "version.h"      // VERSION_PACK, FW_BUILD_DATE/TIME

extern const uint32_t FW_VERSION;   // defined in CONTROLLER.ino

class CONTROLLER
{
public:
    NtpClient ntp;               // public — used directly by .ino for NTP

    float tempMCU = 25.0f;       // public — written by .ino readMCUTemp()

    uint8_t STATUS_BITS1();      // public — inline bit-packing helper
    // STATUS_BITS2() etc if needed

    CONTROLLER();                // empty constructor — no memset here
    void INIT();                 // no port args — ports come from frame.hpp constants
    void UPDATE();               // called every loop()

    // Public hardware control methods (controller-specific)
    // ...

    // Debug — called from .ino handleCommand()
    void SERIAL_CMD(const String& cmd, const String& payload);
    void PRINT_REG();

private:
    // ── A1 TX socket + destination ───────────────────────────
    EthernetUDP udpA1;
    IPAddress   a1DestXXX;       // fixed: upper-level controller IP

    // ── A2 socket + client table ─────────────────────────────
    EthernetUDP udpA2;
    FrameClient a2Clients[A2_MAX_CLIENTS];
    uint8_t     a2_last_seq = 0;
    bool        a2_seq_init = false;
    uint8_t     a1_srv_seq  = 0;

    // ── A3 socket + client table (MCC, BDC only) ─────────────
    EthernetUDP udpA3;
    FrameClient a3Clients[A3_MAX_CLIENTS];
    uint8_t     a3_last_seq = 0;
    bool        a3_seq_init = false;

    // ── 512-byte staging payload ──────────────────────────────
    uint8_t reg_payload512[FRAME_PAYLOAD_SIZE];

    // ── State ─────────────────────────────────────────────────
    bool isReady             = false;
    bool isUnSolicitedEnabled = true;
    DEBUG_LEVELS CTRL_DEBUG_LEVEL = DEBUG_LEVELS::MIN;
    SYSTEM_STATES System_State = SYSTEM_STATES::OFF;
    BDC_MODES     BDC_Mode     = BDC_MODES::OFF;

    // ── Timing ────────────────────────────────────────────────
    uint32_t lastTick        = 0;
    uint32_t TICK            = 10;     // unsolicited interval ms
    uint32_t lastTick_expire = 0;
    uint32_t TICK_expire     = 1000;
    uint32_t prev_dt  = 0; uint32_t dt_delta = 0;
    uint32_t prev_HB  = 0; uint32_t HB_delta = 0;

    // ── Internal methods ──────────────────────────────────────
    void pollA2();
    void pollA3();   // MCC, BDC only
    void handleA2Frame(const uint8_t* buf, uint16_t nb,
                       IPAddress srcIP, uint16_t srcPort);
    void dispatchCmd(uint8_t cmd, const uint8_t* payload, uint16_t plen,
                     uint8_t seq, IPAddress srcIP, uint16_t srcPort);
    void sendA2Response(uint8_t cmd, uint8_t seq, uint8_t status,
                        const uint8_t* payload512,
                        IPAddress dstIP, uint16_t dstPort);
    void sendA2Unsolicited();
    void buildReg01();
    void SEND_UNSOLICITED();
    void printHex(byte msg[], int nb);
};
```

### .cpp — method implementation order

Follow this order consistently across all controllers:

```
1.  Constructor — empty
2.  INIT()      — socket bind, A1 dest, client table zero, hardware init
3.  UPDATE()    — dt timing, pollA2(), pollA3(), ntp.SendTimeRequest(), SEND_UNSOLICITED(), expire
4.  pollA2()    — parsePacket, NTP check, IP range check, read, handleA2Frame
5.  pollA3()    — same pattern as A2 but external IP range + A3 magic
6.  handleA2Frame()  — 5-step rejection chain
7.  handleA3Frame()  — same chain, external magic
8.  dispatchCmd()    — switch on ICD cmd, send responses
9.  sendA2Response() — frameBuildResponse + frameSend
10. sendA2Unsolicited()
11. SEND_UNSOLICITED() — tick gate, buildReg01, frameSendA1, sendA2Unsolicited, sendA3Unsolicited
12. buildReg01() — memset 512, all field memcpys, byte-offset comments matching ICD table
13. PRINT_REG() — live member read, all byte offsets labeled
14. SERIAL_CMD() — ? INFO REG STATUS NTP NTPIP DEBUG STATE MODE + controller-specific
15. Hardware methods (device-specific SPI, I2C, GPIO etc.)
16. printHex()  — debug utility
```

### buildReg01() rules

```cpp
void CONTROLLER::buildReg01()
{
  // 1. Always zero entire buffer first — ensures all RESERVED bytes are 0x00
  memset(reg_payload512, 0x00, FRAME_PAYLOAD_SIZE);

  uint8_t* buf = reg_payload512;  // alias for readability

  // 2. Write fields with byte-offset comment matching ICD table exactly
  buf[0] = (uint8_t)ICD::GET_REGISTER1;   // [0] CMD BYTE
  buf[1] = (uint8_t)System_State;          // [1] System State
  buf[2] = (uint8_t)BDC_Mode;              // [2] System Mode

  uint16_t hb_ms = (uint16_t)HB_delta;
  memcpy(buf + 3, &hb_ms, 2);             // [3–4] HB_ms uint16

  // 3. Use memcpy for ALL multi-byte fields — never pointer cast
  uint64_t t = ntp.GetCurrentTime() / 1000;
  memcpy(buf + OFFSET, &t, 8);

  // 4. Embedded sub-controller buffers — copy into their fixed slot
  memcpy(buf + TMC_BLOCK_OFFSET, tmc.buffer, TMC_BLOCK_LEN);  // MCC example
}
```

### A1 sub-controller liveness (MCC, BDC)

```cpp
// In pollA1() or handleA1Frame():
if (srcPort == PORT_A1 && srcIP == expectedSubCtrlIP)
{
  lastA1_rx_ms = millis();      // update liveness timestamp
  subCtrl.ParseBuffer(buf, nb); // update sub-controller shadow buffer
}

// In buildReg01() or UPDATE():
bool isSubCtrlAlive = (millis() - lastA1_rx_ms) <= A1_TIMEOUT_MS;  // 200 ms
// Clear DEVICE_READY bit if not alive
```

---

*CROSSBOW FW Patterns Reference — session 9 (FMC + platform corrections) — 2026-03-10*
