# TRC — A2 Framing Migration Plan
**TRC_MIGRATION.md — v5 (session 22 — TRC renamed to TRC)**  
**Date:** 2026-03-14  
**Audit scope:** All 8 .cpp + 13 .h files reviewed

---

## Session 15 Changes (2026-03-14)

- ICD renumbered to v3.0.0 — `defines.hpp` canonical v3.X.Y stamped as authoritative for all 5 controllers
- **TRC-M3 closed** — `version.hpp` deleted; `version.h` (shared FW header) adopted as single source of truth. `VERSION` class and `GetSemverWord()` removed. `types.h::GlobalState::version_word` is now a plain `uint32_t` initialised inline to `VERSION_PACK(3,0,1)`. `main.cpp` updated to use `VERSION_PACK`/`VERSION_MAJOR`/`VERSION_MINOR`/`VERSION_PATCH` macros directly.
- **TRC-M11 closed** — `SYSTEM_STATES::MAINT=0x04`, `FAULT=0x05` confirmed correct in canonical `defines.hpp`. All 5 controllers now consistent.
- **Makefile** — `-DPLATFORM_LINUX` added to `CXXFLAGS` to activate IP define guard in `defines.hpp`.
- TRC wire version: `VERSION_PACK(3,0,1) = 0x03000001` (patch bump for version infrastructure alignment — no register or ICD breaking change).

---

## What Was Confirmed vs Assumed (Source Audit Corrections)

| Item | Pre-Audit Assumption | Confirmed Reality |
|------|---------------------|-------------------|
| `camid` encoding | Suspected bug (might write 1/2) | **Correct** — `udp_listener.cpp:1053` uses `BDC_CAM_IDS::VIS/MWIR` (0/1) ✅ |
| `voteBitsMcc/Bdc` write path | "Writer missing" | **Already wired** — `udp_listener.cpp:1080-1081` reads `state_` atomics ✅ |
| `nccScore` write path | "Verify" | **Confirmed** — compositor sets via `camera->setNccScore(ncc)` every tracking frame; `udp_listener.cpp:1082` packs it ✅ |
| `jetsonTemp/Load` wiring | "Needs final wiring" | **Confirmed** — `main.cpp:7` wires compositor atomics to `udp.jetsonTemp` / `udp.jetsonCpuLoad` ✅ |
| `statHbMs_` units | Unknown | **Confirmed ms** — set at `udp_listener.cpp:1137` via `duration<float, std::milli>` ✅ |
| 0xAB handler | "Not implemented" | **Exists** — `udp_listener.cpp:987-999` handles `SET_BCAST_FIRECONTROL_STATUS` on port 5010. Handler logic is correct; must **move** to A1 RX on port 10019 |
| `version.Set()` args | Unknown | **Confirmed** — `main.cpp:250`: `g_state.version.Set(4, 0, 2026, 3, 1)` → major=4, minor=0 → needs update to Set(1, 7, ...) |
| `focusScore` type path | Assumed cast needed | **Confirmed** — `camera_base` stores `std::atomic<double>`; `getFocusScore()` returns `double`; struct field becomes `float` after TRC-M1 → explicit cast required |
| `binaryThreadFunc` recv buffer | Unknown | **Too small** — `buf[256]` at line 745. Framed requests are 8-byte header + payload + 2-byte CRC. Must enlarge to ≥ 600 bytes |
| `sendReport()` sends raw bytes | Assumed | **Confirmed** — `udp_listener.cpp:1109` does `sendto(&telemetry, sizeof(telemetry), ...)` — 64 raw bytes, no framing. Must become 521-byte framed send |
| `SET_UNSOLICITED` restarts thread | Unknown | **Confirmed broken pattern** — lines 773-774 join + respawn `reportThread_` on 0x01. Must be replaced by client-table registration + `seq_init = false` fix |
| `focusScore` write path | Unknown | **Confirmed** — `compositor.cpp:321` calls `camera->setFocusScore(sigma.val[0] * sigma.val[0])` into `std::atomic<double>` ✅ |

---

## Current vs Target Architecture

```
─── CURRENT (v22) ─────────────────────────────────────────────────
Port 5010  RX: recvfrom(buf[256]) → raw buf[0]=cmdByte → switch()
               0xAB handled here alongside GUI commands
Port 5010  TX: reportThread → sendto(&telemetry, 64 bytes) → single telemetryDest_
Port 5012  RX: ASCII commands — unchanged

─── TARGET ─────────────────────────────────────────────────────────
Port 10019 TX: trc_a1.cpp — timerfd 100 Hz → buildResponseFrame(521B) → sendto BDC hardcoded
Port 10019 RX: trc_a1.cpp — recvfrom() → parse 0xAB (4 bytes raw) → write GlobalState vote bits
Port 10018 RX: udp_listener.cpp — recvfrom(buf[600]) → parseRequestFrame() → SEQ check
                                   → existing dispatch (logic unchanged, payload indexing updated)
                                   → buildResponseFrame(521B) → sendto(client)
               A2 unsolicited thread → iterate alive clients → send framed REG1 at 100Hz
Port 5012  RX: ASCII commands — UNCHANGED, not touched
```

---

## Dependency Graph

```
TRC-M1  telemetry.h struct rewrite (BLOCKER)
  └── TRC-M2  buildTelemetry() — 6 targeted line changes

TRC-M3  version.hpp — add VERSION_PACK + GetSemverWord()
  └── TRC-M2  calls GetSemverWord()

TRC-M4  types.h — add A1/A2 port + magic constants
  └── TRC-M5  trc_frame.hpp — uses Defaults::A1_PORT, A2_PORT, FRAME_MAGIC_*
        ├── TRC-M6  trc_a1.cpp — uses frame builder
        └── TRC-M7  udp_listener — uses frame parser + builder

TRC-M6 → TRC-M8  main.cpp — launches TrcA1
TRC-M7 → TRC-M8  main.cpp — default port change, remove old report thread

TRC-M8 → TRC-M9  port 5010 deprecation (do LAST, after HW validation)

TRC-M10  BDC parse alignment verify — independent, no TRC code change
TRC-M11  SYSTEM_STATES MAINT/FAULT enum — independent cross-check
```

---

## Item Listing

---

### TRC-M1 — Rewrite `TelemetryPacket` struct to session 4 layout
**Priority:** BLOCKER — static_assert passes today but at wrong (session 3) offsets  
**File:** `telemetry.h`

The v22 struct passes `sizeof == 64` by coincidence — freed bytes from `ControlByte` (1B), `hb_ms float→uint16` (2B), and `focusScore double→float` (4B) happen to exactly absorb what `Gain` (4B) and `Exposure` (4B) freed. Every field from `systemState` onwards is at the wrong wire position.

**Replace `telemetry.h` entirely:**

```cpp
#ifndef TELEMETRY_H
#define TELEMETRY_H
#include "types.h"
#include <cstring>
#include <cstddef>

#pragma pack(push, 1)
struct TelemetryPacket {
    uint8_t   cmd_byte;          // [0]     always 0xA1
    uint32_t  version_word;      // [1-4]   VERSION_PACK(major, minor, patch)
    uint8_t   systemState;       // [5]     SYSTEM_STATES enum
    uint8_t   systemMode;        // [6]     BDC_MODES enum
    uint16_t  HB_ms;             // [7-8]   ms between sends (was float)
    uint16_t  dt_us;             // [9-10]  µs in processing loop
    uint8_t   overlayMask;       // [11]    bit0=Reticle;1=TrackPreview;2=TrackBox;
                                 //         3=CueChevrons;4=AC_Proj;5=AC_Leaders;
                                 //         6=FocusScore;7=OSD
    uint16_t  fps;               // [12-13] framerate × 100
    int16_t   deviceTemperature; // [14-15] VIS camera sensor temp °C
    uint8_t   camid;             // [16]    VIS=0, MWIR=1 (BDC_CAM_IDS)
    uint8_t   status_cam0;       // [17]    Alvium: bit0=Started;1=Active;2=Capturing;
                                 //         3=Tracking;4=TrackValid;5=FocusScore;6=OSD;7=cue
    uint8_t   status_track_cam0; // [18]    bit2=Enabled;3=Valid;4=Initializing
    uint8_t   status_cam1;       // [19]    MWIR (same layout)
    uint8_t   status_track_cam1; // [20]    MWIR tracker
    int16_t   tx;                // [21-22] tracker centre x (AT-offset adjusted)
    int16_t   ty;                // [23-24] tracker centre y
    int8_t    atX0;              // [25]    AT offset x
    int8_t    atY0;              // [26]    AT offset y
    int8_t    ftX0;              // [27]    FT offset x
    int8_t    ftY0;              // [28]    FT offset y
    float     focusScore;        // [29-32] Laplacian variance (was double — 4 bytes freed)
    int64_t   ntpEpochTime;      // [33-40] ms since epoch (std::chrono system_clock)
    uint8_t   voteBitsMcc;       // [41]    MCC fire control vote bits (0xAB relay)
    uint8_t   voteBitsBdc;       // [42]    BDC geometry vote bits (0xAB relay)
    int16_t   nccScore;          // [43-44] NCC × 10000; unpack: value / 10000.0f
    int16_t   jetsonTemp;        // [45-46] Jetson CPU temp °C
    int16_t   jetsonCpuLoad;     // [47-48] Jetson CPU load %
    uint8_t   RESERVED[15];      // [49-63] 0x00
};
#pragma pack(pop)

static_assert(sizeof(TelemetryPacket) == 64,      "TelemetryPacket must be 64 bytes");
static_assert(offsetof(TelemetryPacket, focusScore)   == 29, "focusScore offset");
static_assert(offsetof(TelemetryPacket, ntpEpochTime) == 33, "ntpEpochTime offset");
static_assert(offsetof(TelemetryPacket, voteBitsMcc)  == 41, "voteBitsMcc offset");
static_assert(offsetof(TelemetryPacket, nccScore)     == 43, "nccScore offset");
static_assert(offsetof(TelemetryPacket, jetsonTemp)   == 45, "jetsonTemp offset");
static_assert(offsetof(TelemetryPacket, RESERVED)     == 49, "RESERVED offset");

#endif // TELEMETRY_H
```

**`make clean && make` is MANDATORY after this change.** Incremental build will silently use stale .o files that still reference the old struct layout.

---

### TRC-M2 — Update `buildTelemetry()` — 6 targeted line changes
**Priority:** BLOCKER (depends on TRC-M1, TRC-M3)  
**File:** `udp_listener.cpp` lines 1036-1101

Everything else in the function is already correct. Only these exact lines change:

| Line | Current | New |
|------|---------|-----|
| 1043 | `telemetry.version_word = state_.version.GetVersionWord();` | `telemetry.version_word = state_.version.GetSemverWord();` |
| 1044 | `telemetry.ControlByte = 0xAA;` | **Delete** |
| 1048 | `telemetry.hb_ms = statHbMs_.load();` | `telemetry.HB_ms = static_cast<uint16_t>(statHbMs_.load());` |
| 1077 | `telemetry.focusScore = activeCam->getFocusScore();` | `telemetry.focusScore = static_cast<float>(activeCam->getFocusScore());` |
| 1078 | `telemetry.Gain = (float)activeCam->getGain();` | **Delete** |
| 1079 | `telemetry.Exposure = (float)activeCam->getExposure();` | **Delete** |

Confirmed correct and requiring no changes:
- `camid` — already uses `BDC_CAM_IDS::VIS/MWIR` (0/1) ✅
- `voteBitsMcc/Bdc` — already reads `state_` atomics ✅  
- `nccScore` — already `(int16_t)(getNccScore() * 10000.0f)` ✅
- `ntpEpochTime` — already `std::chrono::system_clock` ✅
- `jetsonTemp/Load` — already pointer-derefs with null guard ✅

The `memset(&telemetry, 0, sizeof(telemetry))` at line 1039 already zeroes `RESERVED`.

---

### TRC-M3 — ✅ DONE (session 15) — Migrate to `version.h`; update `main.cpp` version init
**Priority:** High — ICD item #14 — **CLOSED**  
**Files:** `version.hpp` (deleted), `version.h` (adopted from FW), `types.h`, `udp_listener.cpp`, `main.cpp`

**Decision (session 15):** `version.hpp` deleted. `version.h` (shared FW header, already contains identical `VERSION_PACK` macro plus unpack macros) adopted as single source of truth across all 5 controllers. `VERSION` class and `GetSemverWord()` removed — no longer needed.

**`types.h` changes:**
```cpp
// FROM:
#include "version.hpp"
// ...
VERSION version;   // in GlobalState

// TO:
#include "version.h"
// ...
uint32_t version_word{VERSION_PACK(3, 0, 1)};  // ICD v3.0.1 — set once at startup
```

**`udp_listener.cpp` line 1084:**
```cpp
// FROM:
telemetry.version_word = state_.version.GetSemverWord();

// TO:
telemetry.version_word = state_.version_word;
```

**`main.cpp` lines 271–277:**
```cpp
// FROM:
// major=1, minor=7 → GetSemverWord() = VERSION_PACK(1,7,0) = 0x01007000
g_state.version.Set(1, 7, 2026, 3, 10);
std::cerr << "Version: " << g_state.version.GetVersion() << std::endl;

// TO:
// ICD v3.0.1 — VERSION_PACK(3,0,1) = 0x03000001
g_state.version_word = VERSION_PACK(3, 0, 1);
std::cerr << "Version: "
          << VERSION_MAJOR(g_state.version_word) << "."
          << VERSION_MINOR(g_state.version_word) << "."
          << VERSION_PATCH(g_state.version_word)
          << std::endl;
```

**Acceptance criteria:** `telemetry.version_word` = `0x03000001` at payload offset [1-4].

---

### TRC-M4 — Add A1/A2 port + frame magic constants to `types.h`
**Priority:** High  
**File:** `types.h` — `Defaults` namespace

```cpp
namespace Defaults {
    // ... all existing constants unchanged ...
    constexpr int     A1_PORT        = 10019;  // TRC→BDC unsolicited TX + BDC→TRC 0xAB RX
    constexpr int     A2_PORT        = 10018;  // engineering bidirectional (replaces 5010)
    constexpr uint8_t FRAME_MAGIC_HI = 0xCB;
    constexpr uint8_t FRAME_MAGIC_LO = 0x49;
    constexpr int     A2_MAX_CLIENTS = 4;
    constexpr int     A2_LIVENESS_MS = 60000; // 60s client timeout
    // UDP_BINARY_PORT = 5010 — keep until TRC-M9 deprecation pass
}
```

---

### TRC-M5 — Create `trc_frame.hpp` — CRC-16 + frame build/parse (header-only)
**Priority:** High — required by TRC-M6 and TRC-M7  
**File:** `trc_frame.hpp` (new)  
**Dependency:** TRC-M4

**CRC-16/CCITT:** poly=`0x1021`, init=`0xFFFF`, no reflection, result BE in `frame[N-2..N-1]`. Copy table verbatim from `fmc.cpp`. Covers all bytes except the trailing 2 CRC bytes.

**STATUS constants:**
```cpp
constexpr uint8_t STATUS_OK          = 0x00;
constexpr uint8_t STATUS_BAD_MAGIC   = 0x01;
constexpr uint8_t STATUS_BAD_LEN     = 0x02;
constexpr uint8_t STATUS_CRC_FAIL    = 0x03;
constexpr uint8_t STATUS_SEQ_REPLAY  = 0x04;
constexpr uint8_t STATUS_UNKNOWN_CMD = 0x05;
```

**Frame build — 521 bytes fixed:**
```cpp
// Fills out[521]. Payload zero-padded to 512 bytes.
// Layout: [0-1] magic CB49  [2] seq  [3] cmd  [4] status
//         [5-6] payload_len LE  [7-518] payload (512B)  [519-520] CRC-16 BE
void buildResponseFrame(uint8_t seq, uint8_t cmd, uint8_t status,
                        const uint8_t* payload, uint16_t payload_len,
                        uint8_t out[521]);
```

**Frame parse — A2 inbound requests:**
```cpp
struct ParsedFrame {
    uint8_t        seq{0};
    uint8_t        cmd{0};
    uint16_t       payload_len{0};
    const uint8_t* payload{nullptr}; // points into caller's buf
    uint8_t        status{STATUS_OK};
};
// Validates: len >= 8, magic, CRC-16, payload_len. Returns false on failure.
bool parseRequestFrame(const uint8_t* buf, size_t len, ParsedFrame& out);
```
Validation order: minimum length → magic check → CRC-16 → payload_len sanity.

**A2 client entry:**
```cpp
struct A2Client {
    sockaddr_in addr{};
    uint8_t     last_seq{0};
    bool        seq_init{true};   // true = accept any SEQ (first frame / post-reconnect)
    bool        alive{false};
    uint64_t    last_seen_ms{0};
    uint8_t     srv_seq{0};       // per-client server counter for unsolicited A2 frames
};
```

**SEQ replay check:**
```cpp
// Returns true if seq falls within the replay window [last_seq-32, last_seq] mod 256.
bool isSeqReplay(uint8_t seq, uint8_t last_seq);
```

---

### TRC-M6 — Create `trc_a1.hpp` / `trc_a1.cpp` — A1 TX + A1 RX
**Priority:** High  
**Files:** `trc_a1.hpp` (new), `trc_a1.cpp` (new)  
**Dependencies:** TRC-M1, TRC-M4, TRC-M5

**Class interface:**
```cpp
class TrcA1 {
public:
    TrcA1(UdpListener& udp, GlobalState& state);
    void start();
    void stop();
private:
    void txThreadFunc();  // 100 Hz timerfd → buildTelemetry() + buildResponseFrame() + sendto
    void rxThreadFunc();  // recvfrom() on port 10019 → parse raw 0xAB → write GlobalState
    UdpListener&      udp_;
    GlobalState&      state_;
    int               sock_{-1};
    uint8_t           a1_srv_seq_{0};
    std::atomic<bool> running_{false};
    std::thread       txThread_;
    std::thread       rxThread_;
};
```

**A1 TX thread — timerfd 100 Hz:**
```cpp
int tfd = timerfd_create(CLOCK_MONOTONIC, TFD_NONBLOCK);
// arm: interval = 10ms
udp_.buildTelemetry();   // NOTE: requires buildTelemetry() made public in UdpListener
uint8_t frame[521];
buildResponseFrame(a1_srv_seq_++, 0xA1, STATUS_OK,
                   (const uint8_t*)&udp_.telemetry, 64, frame);
sendto(sock_, frame, 521, 0, (sockaddr*)&bdc_addr, sizeof(bdc_addr));
// bdc_addr hardcoded: Defaults::BDC_HOST : Defaults::A1_PORT
```
`a1_srv_seq_` is independent of A2 — no replay protection on A1 TX (trusted stream to BDC).

**A1 RX — receive raw 0xAB from BDC:**
- `recvfrom()` on same socket (or `poll()` with short timeout)
- Validate source IP == `192.168.1.20` — silently drop anything else
- Parse: `[0]=0xAB magic, [1]=voteBitsMcc, [2]=voteBitsBdc, [3]=SystemState, [4]=BDCMode`
- `state_.voteBitsMcc.store(buf[1]); state_.voteBitsBdc.store(buf[2]);`
- **No CRC, no framing on 0xAB** — BDC sends this as a raw 4-byte packet

**Migration note on existing 0xAB handler:**  
`udp_listener.cpp:987-999` (`SET_BCAST_FIRECONTROL_STATUS`) contains the correct handler logic. Copy it to `trc_a1.cpp` RX path, then **delete** the `case ICD_CMDS::SET_BCAST_FIRECONTROL_STATUS` block from `binaryThreadFunc()` — 0xAB will no longer arrive on port 10018.

**`buildTelemetry()` access:**  
Make `buildTelemetry()` `public` in `udp_listener.h` — it is currently `private`. This is the only coupling TrcA1 needs.

---

### TRC-M7 — Upgrade `UdpListener` binary path to A2 framed protocol
**Priority:** High — largest change; mostly mechanical  
**Files:** `udp_listener.cpp`, `udp_listener.h`  
**Dependencies:** TRC-M1, TRC-M4, TRC-M5

**All existing command dispatch logic is kept unchanged.** The change is the receive wrapper, SEQ management, client registry, and framed send.

#### 7a — `udp_listener.h`

**Add to private section:**
```cpp
A2Client  a2Clients_[Defaults::A2_MAX_CLIENTS];
uint64_t  a2LastLivenessCheckMs_{0};
std::thread a2UnsolThread_;
uint64_t  lastNucTimeMs_{0};   // NUC1 rate gate

A2Client* frameClientRegister(const sockaddr_in& addr);
A2Client* frameClientFind(const sockaddr_in& addr);
void      frameClientsExpire();
void      sendFramedResponse(int sock, const sockaddr_in& dest,
                             uint8_t seq, uint8_t cmd, uint8_t status,
                             const uint8_t* payload, uint16_t plen);
void      a2UnsolThreadFunc();
```

**Change constructor default:**
```cpp
// Was: int binaryPort = Defaults::UDP_BINARY_PORT
int binaryPort = Defaults::A2_PORT
```

**Remove** `telemetryDest_`, `telemetryDestValid_`, `telemetryDestMutex_` — replaced by `a2Clients_`.

**Make `buildTelemetry()` public** (for TrcA1 access).

#### 7b — `binaryThreadFunc()` receive loop

**Enlarge receive buffer:** `buf[256]` → `buf[600]`.

**New loop structure:**
```
recvfrom(buf, 600, ...) → n <= 0: continue

parseRequestFrame(buf, n, parsed) → on failure:
    sendFramedResponse(STATUS_bad, ...) + continue

client = frameClientFind(sender) 
if client && !client->seq_init:
    if isSeqReplay(parsed.seq, client->last_seq):
        sendFramedResponse(STATUS_SEQ_REPLAY, ...) + continue
    client->last_seq = parsed.seq
    client->last_seen_ms = now_ms()

[existing switch(parsed.cmd) dispatch — logic UNCHANGED]
  payload reads shift: buf[1] → parsed.payload[0], buf[2] → parsed.payload[1], etc.
  responses: sendFramedResponse() instead of sendto() raw bytes

default: sendFramedResponse(STATUS_UNKNOWN_CMD, ...)
```

**`case SET_UNSOLICITED (0xA0)` — rewrite:**
```cpp
if (enable) {
    A2Client* client = frameClientRegister(clientAddr);
    client->seq_init = false;  // CRITICAL — clears replay window on reconnect
                               // Without this, GUI Stop/Start locks out new session
} else {
    A2Client* client = frameClientFind(clientAddr);
    if (client) client->alive = false;
}
sendFramedResponse(binarySock_, clientAddr, parsed.seq, 0xA1, STATUS_OK, nullptr, 0);
```
**Remove** the `reportThread_.join()` + `reportThread_ = std::thread(...)` pattern — replaced by `a2UnsolThreadFunc`.

**`case GET_REGISTER1 (0xA1)` — solicited response:**
```cpp
buildTelemetry();
sendFramedResponse(binarySock_, clientAddr, parsed.seq, 0xA1, STATUS_OK,
                   (uint8_t*)&telemetry, sizeof(telemetry));
```

**`case SET_BCAST_FIRECONTROL_STATUS (0xAB)` — DELETE entire case.** Moves to A1 RX in `trc_a1.cpp`.

**`case CMD_MWIR_NUC1 (0xCC)` — add rate gate:**
```cpp
uint64_t nowMs = /* steady_clock ms */;
if (nowMs - lastNucTimeMs_ < 5ULL * 60 * 1000) {
    dlog() << "[UDP] NUC1 rate-gated" << std::endl;
    sendFramedResponse(..., STATUS_OK, nullptr, 0);
    break;
}
lastNucTimeMs_ = nowMs;
// ... existing NUC logic ...
```

#### 7c — `reportThreadFunc()` → `a2UnsolThreadFunc()`

Replace the existing `reportThread_` / `reportThreadFunc()` with:
```cpp
void UdpListener::a2UnsolThreadFunc() {
    while (running_) {
        std::this_thread::sleep_for(std::chrono::milliseconds(10)); // 100 Hz
        buildTelemetry();
        auto nowMs = /* steady_clock ms */;
        // Update heartbeat timing
        statHbMs_.store(/* elapsed since last send */);
        for (auto& client : a2Clients_) {
            if (!client.alive) continue;
            uint8_t frame[521];
            buildResponseFrame(client.srv_seq++, 0xA1, STATUS_OK,
                               (uint8_t*)&telemetry, 64, frame);
            sendto(binarySock_, frame, 521, 0,
                   (sockaddr*)&client.addr, sizeof(client.addr));
        }
        frameClientsExpire();
    }
}
```

#### 7d — `start()` changes

- Remove `telemetryDest_` pre-population block (lines 104-113)
- Remove `reportThread_` launch; add `a2UnsolThread_ = std::thread(&UdpListener::a2UnsolThreadFunc, this)`
- Keep ASCII socket setup unchanged
- Log message: update from `"binary port " << binaryPort_` (was 5010) to reflect A2 port

#### 7e — `stop()` changes

- Remove `reportThread_.joinable() / join()` guard
- Add `if (a2UnsolThread_.joinable()) a2UnsolThread_.join()`

---

### TRC-M8 — Update `main.cpp`
**Priority:** Medium  
**File:** `main.cpp`  
**Dependencies:** TRC-M6, TRC-M7

**Changes (all small):**

1. **Version init (line 250):**
   ```cpp
   // Was: g_state.version.Set(4, 0, 2026, 3, 1);
   g_state.version.Set(1, 7, 2026, 3, 10);
   ```

2. **`Args::binaryPort` default:**
   ```cpp
   // Was: int binaryPort = Defaults::UDP_BINARY_PORT;  // 5010
   int binaryPort = Defaults::A2_PORT;  // 10018
   ```

3. **Instantiate and start `TrcA1` after `UdpListener` construction:**
   ```cpp
   UdpListener udp(&alvium, &mwir, g_state, args.asciiPort, args.binaryPort);
   // ... existing wiring ...
   TrcA1 trcA1(udp, g_state);
   // ...
   trcA1.start();   // A1 TX → BDC at 100 Hz begins here
   udp.start();
   ```

4. **Stop order** — `trcA1.stop()` before or after `udp.stop()` (both safe):
   ```cpp
   compositor.stop();
   alvium.stop();
   mwir.stop();
   udp.stop();
   trcA1.stop();
   ```

5. **Help text (line ~1201):** Update binary port reference from `Defaults::UDP_BINARY_PORT` to `Defaults::A2_PORT`.

6. **Makefile** — add `trc_a1.cpp`:
   ```makefile
   SRCS := main.cpp alvium_camera.cpp mwir_camera.cpp compositor.cpp \
           udp_listener.cpp tracker_wrapper.cpp coco_detector.cpp osd.cpp \
           trc_a1.cpp
   trc_a1.o: trc_a1.cpp trc_a1.hpp trc_frame.hpp types.h telemetry.h udp_listener.h
   ```

---

### TRC-M9 — Deprecate port 5010 (do LAST, after HW validation)
**Priority:** Low  
**Status:** Block on BDC team confirming they are reading TRC telemetry from 10019 and no longer expect raw 64-byte on 5010.

Steps: Remove `Defaults::UDP_BINARY_PORT = 5010`, remove `--udp-bin-port` CLI arg override or keep as undocumented field debugging backdoor. **Never touch port 5012.**

---

### TRC-M10 — Verify BDC `BDC_MSG.cs` TRC REG1 parse alignment
**Priority:** High — no TRC code change, verify only  
**Gate on:** TRC-M1 struct finalized

`TRC_MSG.ParseMsg()` enters at ndx=60 (BDC REG1 byte), returns at ndx=124. Verify field reads inside `TRC_MSG.ParseMsg()` match session 4 offsets:

| Field | Expected global BDC offset | Session 4 local offset |
|-------|---------------------------|----------------------|
| `voteBitsMcc` | 60+41 = 101 | [41] |
| `voteBitsBdc` | 60+42 = 102 | [42] |
| `nccScore` | 60+43 = 103 | [43-44] |
| `jetsonTemp` | 60+45 = 105 | [45-46] |

Also confirm: `THEIA-10` in `ACTION_ITEMS.md` references stale offsets (Gain at [48-51], Exposure at [52-55]) — these fields no longer exist. Verify any THEIA display reads for those fields are removed or guarded.

---

### TRC-M11 — ✅ DONE (session 15) — `SYSTEM_STATES` MAINT/FAULT values confirmed
**Priority:** Low — cross-check only — **CLOSED**  
**File:** `defines.hpp`

Confirmed: `MAINT = 0x04`, `FAULT = 0x05` in canonical `defines.hpp` (v3.X.Y). All 5 controllers (MCC, BDC, TMC, FMC, TRC) now share the same authoritative `defines.hpp`. No TRC source change required — `defines.hpp` drop-in covers this.

---

## Files To Produce

| File | Item | Action | Status |
|------|------|--------|--------|
| `telemetry.h` | TRC-M1 | Full struct rewrite + 6 static_asserts | ⏳ |
| `version.hpp` | TRC-M3 | **DELETED** — replaced by `version.h` (shared FW header) | ✅ Done session 15 |
| `version.h` | TRC-M3 | Adopted from FW — no changes needed | ✅ Done session 15 |
| `types.h` | TRC-M3+M4 | `version.hpp` → `version.h`; `VERSION version` → `uint32_t version_word`; A1/A2/magic constants in `Defaults` | ✅ Done session 15 |
| `trc_frame.hpp` | TRC-M5 | CRC16 table + `buildResponseFrame()` + `parseRequestFrame()` (header-only) | ⏳ |
| `trc_a1.hpp` | TRC-M6 | `TrcA1` class interface | ✅ Done |
| `trc_a1.cpp` | TRC-M6 | A1 TX timerfd + A1 RX 0xAB; delete 0xAB from A2 | ✅ Done |
| `udp_listener.h` | TRC-M7 | Port default, A2 client table, `buildTelemetry()` public | ✅ Done |
| `udp_listener.cpp` | TRC-M2+M7+M3 | 6 buildTelemetry changes + full A2 framing upgrade + `version_word` direct access | ✅ Done session 15 |
| `main.cpp` | TRC-M3+M8 | VERSION_PACK(3,0,1), port default, TrcA1 instantiation + launch | ✅ Done session 15 |
| `Makefile` | TRC-M8 | `trc_a1.cpp` in SRCS + dep line + `-DPLATFORM_LINUX` | ✅ Done session 15 |
| `defines.hpp` | TRC-M11 | Canonical v3.X.Y drop-in — all enums correct | ✅ Done session 15 |

---

## Pre-Build Checklist (before first hardware test)

- [ ] `make clean && make` — MANDATORY after `telemetry.h` change
- [ ] All 6 `static_assert` + `offsetof` checks pass at compile time
- [ ] `trc_frame.hpp` CRC-16 round-trip: `buildResponseFrame` + `parseRequestFrame` on known frame matches
- [ ] Wireshark: A1 frames to `192.168.1.20:10019` at ~100 Hz, 521 bytes, first 2 bytes `CB 49`
- [ ] Wireshark: A2 response to GUI, 521 bytes, magic `CB 49`, STATUS byte `00`, CRC valid
- [ ] GUI Stop/Start: reconnect succeeds on first frame — `seq_init = false` fix in 0xA0 handler
- [ ] `telemetry.version_word` = `0x03000001` at payload offset [1-4]
- [ ] `camid` = `0x00` on VIS, `0x01` on MWIR at payload offset [16]
- [ ] `voteBitsMcc/Bdc` at payload offsets [41]/[42] track live BDC 0xAB values in real time
- [ ] BDC `BDC_MSG.ParseMSG01` TRC block at [60-123] parses without offset errors (TRC-M10)

---

## System Migration Status

| Controller | Register | Version | Framing | SEQ fix | A1 ok | A2 ok |
|---|---|---|---|---|---|---|
| TMC | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| MCC | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| FMC | ✅ | ✅ | ✅ | ⏳ item #37 | ✅ | ✅ |
| BDC | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **TRC** | **⏳ M1** | **✅ M3 done** | **⏳ M5/6/7** | **⏳ M7** | **⏳ M6** | **⏳ M7** |

> **TRC version note (session 15):** `version.hpp` deleted. `version.h` adopted.
> `GlobalState::version_word = VERSION_PACK(3,0,1) = 0x03000001`.
> Remaining TRC blockers: TRC-M1 (telemetry.h struct), TRC-M5 (trc_frame.hpp), TRC-M6/M7 (A1/A2 framing).
