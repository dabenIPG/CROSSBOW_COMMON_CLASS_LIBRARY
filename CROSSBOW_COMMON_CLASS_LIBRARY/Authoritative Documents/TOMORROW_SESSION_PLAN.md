# CROSSBOW / TRC — Session Plan
**Document:** TOMORROW_SESSION_PLAN.md  
**Date:** 2026-04-09 (session capture)  
**Platform:** Jetson Orin NX 16GB, Seeed Studio reComputer J4012 (J401 carrier)  
**Related:** JETSON_SETUP.md (DOC-2, in progress), OPENCV_BUILD_HISTORY.md, TRC_MIGRATION.md

---

## Context Summary

Four tasks for the next session. Order matters — Task 1 establishes the baseline before
anything is changed, Task 2 produces the documentation and scripts, Task 3 uses those
scripts to validate a fresh build, Task 4 extends TRC.

| Task | Scope | Dependency |
|------|-------|------------|
| 1 | Verify known-good lab setup — baseline snapshot | None — do first |
| 2 | Finalize Jetson config docs, scripts, verification, image procedure | Task 1 baseline |
| 3 | Fresh Jetson build from scratch using new scripts | Task 2 complete |
| 4 | TRC upgrades: VimbaX 2026-1, PTP/NTP (NEW-38d), OpenCV bridge eval | Task 3 validated |

---

## Task 1 — Verify Lab Setup Baseline

**Goal:** Document the exact state of the current lab Jetson. Confirm whether CUDA DNN
is active. If everything is working as expected, this becomes the authoritative baseline
to compare against the fresh build in Task 3.

Run all commands on **both** the known-good home PC build AND the lab asset, side by side.
Differences identify what was or wasn't rebuilt.

### 1.1 OpenCV version and binding location

```bash
python3 -c "import cv2; print('Version:', cv2.__version__); print('File:   ', cv2.__file__)"
```

**Pass:** Version is `4.11.0` (or later if rebuilt), file path contains `dist-packages`.  
**Fail:** Version is `4.8.0` (system apt), OR file path contains `site-packages`.  
**What it means if it fails:** Lab asset was never rebuilt — TRC is running system OpenCV,
COCO inference is on CPU. Must rebuild with `install_opencv4.13.0_Jetpack6.2.1.sh`.

---

### 1.2 CUDA and cuDNN in build info

```bash
python3 -c "import cv2; print(cv2.getBuildInformation())" \
    | grep -E "NVIDIA CUDA|cuDNN|GStreamer|TBB|FFMPEG"
```

**Pass (per line):**
```
NVIDIA CUDA:                   YES (ver 12.6, ...)
cuDNN:                         YES (ver 9.3.0)
GStreamer:                     YES (1.x.x)
```

**Note:** `OPENCV_DNN_CUDA: YES` does NOT appear in getBuildInformation() for OpenCV
4.11.x — that label was removed. Confirm DNN CUDA via Step 1.3 instead.

---

### 1.3 OPENCV_DNN_CUDA confirmed in cmake cache (critical)

```bash
find ~/CV ~/. -name "CMakeCache.txt" -path "*/opencv*/release/*" 2>/dev/null | head -3
grep "OPENCV_DNN_CUDA" <path-from-above>/CMakeCache.txt
```

**Pass:** `OPENCV_DNN_CUDA:BOOL=ON`  
**Fail:** File not found (build was done elsewhere and binaries were copied), OR value is OFF.  
**If file not found:** The lab asset received a binary copy, not a native build.
This is the most likely explanation for "everything works but might be falling back to CPU."
Run the full inference probe (Step 1.5) to determine actual backend.

---

### 1.4 TRC binary links against correct OpenCV

```bash
# Find the TRC binary (target name is 'trc' per Makefile)
find ~/CV -name "trc" -type f 2>/dev/null

ldd <path-to-trc-binary> | grep -E "opencv_dnn|opencv_core"
```

**Pass:** `libopencv_dnn.so.411 => /usr/local/lib/libopencv_dnn.so.411`  
**Fail:** `libopencv_dnn.so.408 => /usr/lib/aarch64-linux-gnu/libopencv_dnn.so.408`  
(system 4.8.0 — means TRC was built against system OpenCV, not the custom build)

---

### 1.5 CUDA inference probe (full backend test)

```bash
cd ~/CV/TRCv3/v20   # or wherever model_data/ lives
python3 << 'EOF'
import cv2, numpy as np

net = cv2.dnn.readNet("model_data/frozen_inference_graph.pb",
                      "model_data/ssd_mobilenet_v3_large_coco_2020_01_14.pbtxt")
probe = np.zeros((320, 320, 3), dtype=np.uint8)
blob = cv2.dnn.blobFromImage(probe, 1/127.5, (320,320), (127.5,127.5,127.5), swapRB=True)

for backend, target, label in [
    (cv2.dnn.DNN_BACKEND_CUDA, cv2.dnn.DNN_TARGET_CUDA_FP16, "CUDA FP16"),
    (cv2.dnn.DNN_BACKEND_CUDA, cv2.dnn.DNN_TARGET_CUDA,      "CUDA FP32"),
    (cv2.dnn.DNN_BACKEND_DEFAULT, cv2.dnn.DNN_TARGET_CPU,    "CPU"),
]:
    try:
        net.setPreferableBackend(backend)
        net.setPreferableTarget(target)
        net.setInput(blob)
        net.forward()
        print(f"  {label}: OK")
    except Exception as e:
        print(f"  {label}: FAILED — {e}")
EOF
```

**Pass:** All three lines show OK.  
**CUDA FP16 FAILED:** `OPENCV_DNN_CUDA` was not compiled in. Must rebuild.  
**CUDA FP32 FAILED also:** Same cause.  
**CPU only:** TRC is running full inference on CPU — measurable in tegrastats.

---

### 1.6 VimbaX version installed

```bash
ls /opt/ | grep VimbaX
/opt/VimbaX_*/bin/ListCameras_VmbCPP
```

**Pass:** `VimbaX_2026-1` present, camera listed with correct ID.  
**Current state expected:** `VimbaX_2025-1` (per Makefile). Upgrade to 2026-1 is Task 3.

---

### 1.7 Makefile VimbaX path mismatch check

```bash
grep "VIMBAX_DIR" ~/CV/TRC*/Makefile
```

**Current state:** `VIMBAX_DIR := /opt/VimbaX_2025-1`  
**Action required:** Update to `VimbaX_2026-1` before Task 3 build.

---

### 1.8 PHC hardware timestamping availability

```bash
ls /dev/ptp*
# Orin NX EQOS Ethernet — should show /dev/ptp0 or /dev/ptp1

# If present, confirm capabilities:
sudo ethtool -T eth0 2>/dev/null | grep -E "hardware|PTP"
```

**Pass:** `/dev/ptp0` present, hardware-transmit and hardware-receive timestamps listed.  
**Impact on NEW-38d:** If hardware timestamping available, ptp4l runs in `hw` mode for
sub-microsecond accuracy. If absent, falls back to software timestamping (~1–100µs).

---

### 1.9 NTP sync status

```bash
timedatectl status
cat /etc/systemd/timesyncd.conf
```

**Pass:** `System clock synchronized: yes`, `NTP service: active`, server showing `.33`.  
**If showing .8:** Wrong NTP server — left over from engineering bench config.

---

### 1.10 Jetson power mode and clocks

```bash
sudo nvpmodel -q --verbose | head -5
# Expect: NV Power Mode: MAXN, MODE_ID: 0

# Check jetson_clocks state
sudo jetson_clocks --show 2>/dev/null | head -10
# Or via jtop: jtop (look at CTRL tab)
```

---

### 1.11 Baseline snapshot — save for comparison

```bash
# Save full state to a file for side-by-side comparison with fresh build
{
  echo "=== DATE ===" && date
  echo "=== JETPACK ===" && cat /etc/nv_tegra_release
  echo "=== OPENCV ===" && python3 -c "import cv2; print(cv2.__version__, cv2.__file__)"
  echo "=== OPENCV BUILD ===" && python3 -c "import cv2; print(cv2.getBuildInformation())" \
      | grep -E "NVIDIA CUDA|cuDNN|GStreamer|TBB|FFMPEG|DNN"
  echo "=== VIMBAX ===" && ls /opt/ | grep VimbaX
  echo "=== PTP ===" && ls /dev/ptp* 2>/dev/null || echo "none"
  echo "=== NTP ===" && timedatectl status | grep -E "synchronized|NTP service|server"
  echo "=== NVPMODEL ===" && sudo nvpmodel -q
  echo "=== TRC BINARY ===" && find ~/CV -name "trc" -type f 2>/dev/null \
      | xargs -I{} ldd {} 2>/dev/null | grep opencv_dnn
} | tee ~/jetson_baseline_$(date +%Y%m%d).txt

echo "Baseline saved."
```

---

## Task 2 — Finalize Jetson Config Documentation and Scripts

**Goal:** Produce the complete JETSON_SETUP.md (DOC-2 per ARCHITECTURE.md §2.5) plus
all helper scripts. Everything needed to bring a fresh Jetson from JetPack flash to
production-ready TRC deployment, ending with a reproducible image.

### 2.1 Documents and scripts to produce

| File | Status | Notes |
|------|--------|-------|
| `OPENCV_BUILD_HISTORY.md` | ✅ Done | Gen 1/2/3 flag comparison |
| `install_opencv4.13.0_Jetpack6.2.1.sh` | ✅ Done | Full Gen 3 build script |
| `JETSON_SETUP.md` | ⏳ In progress | DOC-2 — master setup reference |
| `00_base_setup.sh` | ⏳ Needed | System update, swap, pip, apt-mark hold |
| `01_power_setup.sh` | ⏳ Needed | nvpmodel, jetson_clocks, fan config |
| `02_timing_setup.sh` | ⏳ Needed | NTP timesyncd config (.33/.208) |
| `03_install_vimba.sh` | ⏳ Needed | VimbaX 2026-1 install + CTI + USB buffer |
| `04_verify_all.sh` | ⏳ Needed | Full verification suite (Task 1 steps automated) |
| `05_image_and_overlay.sh` | ⏳ Needed | overlayFS enable + image procedure |

### 2.2 Key decisions locked (from prior session)

| Item | Decision |
|------|----------|
| apt upgrade scope | `sudo apt-mark hold nvidia-l4t-*` before any upgrade |
| jetson_clocks | Enable on boot (60s delay, jtop persist) |
| Fan profile | `cool` — set manually in jtop, saved |
| VimbaX download | Manual from alliedvision.com — script assumes pre-downloaded tarball |
| VimbaX version | 2026-1 (ARM64), tested against JetPack 6.2.1 |
| gst-vmbsrc | NOT used — remove from all documentation |
| USB buffer | Script with backup: `usbcore.usbfs_memory_mb=1000` in extlinux.conf |
| PYTHONPATH | `dist-packages` (not `site-packages`) — confirmed critical |
| NTP primary | 192.168.1.33 (HW Stratum 1) |
| NTP fallback | 192.168.1.208 (THEIA w32tm) |
| PTP grandmaster | 192.168.1.30 (NovAtel GNSS, IEEE 1588 2-step, domain 0) |
| Autostart | crontab review deferred — compare existing vs new build tomorrow |
| Overlay mode | Enable after stable build confirmed — then image |
| Headless | Separate optional script — not in base setup |

### 2.3 overlayFS + imaging procedure (to be written)

**overlayFS approach for Jetson:**
```bash
# Jetson-specific overlay (not generic overlayroot)
# Method: Jetson initrd overlay — supported in L4T 36.x
sudo /usr/sbin/nvbootctrl set-active-boot-slot B  # if dual-slot
# OR: use overlayroot package (Ubuntu)
sudo apt install overlayroot
# Edit /etc/overlayroot.conf: overlayroot="tmpfs"
sudo reboot
# Verify: mount | grep overlay
```

**Imaging approach:**
```bash
# Option A: dd the NVMe (offline — boot from USB)
sudo dd if=/dev/nvme0n1 of=/mnt/usb/jetson_trc_YYYYMMDD.img bs=4M status=progress

# Option B: Jetson partition-level backup (online — Jetson SDK Manager or l4t tools)
# sudo ./tools/kernel_flash/l4t_backup_restore.sh -e nvme0n1 backup

# Verify restore on second unit:
sudo dd if=jetson_trc_YYYYMMDD.img of=/dev/nvme0n1 bs=4M status=progress
```

---

## Task 3 — Fresh Jetson Build from Scratch

**Goal:** Use the finalized scripts from Task 2 to build a new Jetson unit from JetPack
flash to fully operational TRC. Compare against Task 1 baseline at each checkpoint.

### 3.1 Build order

```
JetPack 6.2.1 (already flashed via SDK Manager)
  └── 00_base_setup.sh
        └── 01_power_setup.sh
              └── 02_timing_setup.sh
                    └── 03_install_vimba.sh   ← manual VimbaX 2026-1 download first
                          └── install_opencv4.13.0_Jetpack6.2.1.sh
                                └── TRC build (Makefile with VimbaX 2026-1 path)
                                      └── 04_verify_all.sh
                                            └── Hardware test (camera, COCO, stream)
                                                  └── 05_image_and_overlay.sh
```

### 3.2 TRC Makefile changes required before build

```makefile
# Change line 12:
VIMBAX_DIR := /opt/VimbaX_2026-1    # was: VimbaX_2025-1
```

After updating:
```bash
make clean && make -j$(nproc)
ldd ./trc | grep -E "opencv_dnn|VmbC"
# Must show:
#   libopencv_dnn.so.413 => /usr/local/lib/libopencv_dnn.so.413
#   libVmbCPP.so => /opt/VimbaX_2026-1/api/lib/libVmbCPP.so
```

### 3.3 Checkpoint comparison

After `04_verify_all.sh`, compare output against Task 1 baseline snapshot:
```bash
diff ~/jetson_baseline_YYYYMMDD.txt ~/jetson_fresh_YYYYMMDD.txt
```

Expected differences: OpenCV version (4.11 → 4.13), VimbaX version (2025-1 → 2026-1).  
Expected same: CUDA/cuDNN present, GStreamer present, NTP synced to .33, nvpmodel MAXN.

---

## Task 4 — TRC Upgrades

### 4.1 VimbaX 2026-1 Makefile (prerequisite — in Task 3)

Done as part of Task 3 build. No additional TRC source changes needed for VimbaX version bump alone.

### 4.2 NEW-38d — PTP/NTP TIMESRC implementation

**Open item:** ARCHITECTURE.md §17, priority Medium.  
**Scope confirmed from source audit:**

| Work item | File | Detail |
|-----------|------|--------|
| SNTP client class | `ntp_client.hpp/cpp` (new) | Queries 192.168.1.33 directly. Fallback to .208 after 3 misses. Mirrors embedded controller NTP logic. |
| PHC reader | `phc_reader.hpp` (new) | Reads `/dev/ptp0` via `clock_gettime(CLOCK_TAI)` or `CLOCK_REALTIME` after `ptp4l` + `phc2sys` discipline it. |
| `TIMESRC` ASCII handler | `udp_listener.cpp` port 5012 | `TIMESRC PTP\|NTP\|AUTO\|OFF` and `TIME` commands. Exact match to embedded controller serial command pattern. |
| `ntpEpochTime` source | `udp_listener.cpp` `buildTelemetry()` | Replace bare `system_clock::now()` with `getActiveTimeUs()` from the new time manager class. |
| `TIME_BITS` byte | `telemetry.h` byte [49] | Allocate `RESERVED[0]` (byte [49]). Add `static_assert(offsetof(...TIME_BITS) == 49)`. Identical bit layout to MCC (253) / BDC (391) / TMC (61). |
| `MSG_TRC.cs` decode | C# ENG GUI | Decode `TIME_BITS` at TRC telemetry offset [49]. Add `activeTimeSourceLabel` to TRC tab. |
| OS-level layer | Base install script | `systemd-timesyncd` with .33/.208 stays as floor. `ptp4l` + `phc2sys` added to base install as the PTP discipline layer. |

**`TIME_BITS` layout (identical to all other controllers):**

| Bit | Field |
|-----|-------|
| 0 | `isPTP_Enabled` |
| 1 | `ptp_isSynched` (PHC locked to grandmaster) |
| 2 | `usingPTP` (active time source is PTP) |
| 3 | `ntp_isSynched` |
| 4 | `ntpUsingFallback` (on .208) |
| 5 | `ntpHasFallback` |
| 6–7 | RES |

**telemetry.h change (byte [49]):**
```cpp
// Replace:
uint8_t   RESERVED[15];        // [49-63] 0x00

// With:
uint8_t   time_bits;           // [49]    timing status (same layout as MCC/BDC/TMC)
                               //         bit0=isPTP_Enabled  bit1=ptp_isSynched
                               //         bit2=usingPTP       bit3=ntp_isSynched
                               //         bit4=ntpUsingFallback bit5=ntpHasFallback
uint8_t   RESERVED[14];        // [50-63] 0x00

// Add assert:
static_assert(offsetof(TelemetryPacket, time_bits) == 49, "time_bits must be at offset 49");
static_assert(offsetof(TelemetryPacket, RESERVED)  == 50, "RESERVED must be at offset 50");
```

**`make clean && make` mandatory after telemetry.h change.**  
**BDC TRC parse at [60-123] unaffected** — byte [49] is inside TRC's own local packet,
before the BDC embedding window starts at [60] of BDC's REG1.

### 4.3 VimbaX OpenCV bridge evaluation (NEW in 2025-3 / 2026-1)

**What it is:** `VmbOpenCVHelper.h` in the VimbaX 2025-3+ SDK. Enables wrapping a
VimbaX frame buffer directly as a `cv::Mat` without memcpy.

**Current TRC flow (alvium_camera.cpp — inferred):**
```
VimbaX frame callback → copy pixel data → cv::Mat → compositor
```

**With OpenCV bridge:**
```
VimbaX frame callback → VmbOpenCVHelper::ToMat() → cv::Mat (zero-copy) → compositor
```

**Evaluation criteria during Task 4:**
1. Include `VmbOpenCVHelper.h` from VimbaX 2026-1 SDK
2. Check if the Alvium frame pixel format (likely UYVY or BGR) is directly wrappable
3. Measure frame callback latency before and after (tegrastats + compositor dt_us)
4. Check memory bandwidth reduction (should show in jetson_clocks --show memory clock)

**If improvement is marked:** Implement in `alvium_camera.cpp`. Add to OPENCV_BUILD_HISTORY.md.  
**If marginal:** Document as evaluated, keep existing copy path. Note in TRC build doc.

### 4.4 Task 4 version bump

After all changes:
```cpp
// main.cpp line 334:
g_state.version_word = VERSION_PACK(3, 1, 0);   // was 3.0.2
// Rationale: TIMESRC / TIME_BITS is a telemetry register change (minor bump)
```

Update ARCHITECTURE.md §15 TRC version: `3.0.2` → `3.1.0`.

---

## Open Items Carried Into This Session

| ID | Item | Task | Priority |
|----|------|------|----------|
| NEW-38d | TRC PTP integration — TIME_BITS, MSG_TRC.cs, ptp4l | Task 4 | Medium |
| DOC-1 | Add TRC NTP/PTP setup reference to ARCHITECTURE.md §2.5 | Task 2 | Medium |
| DOC-2 | Create JETSON_SETUP.md | Task 2 | Medium |
| GUI-8 | TRC C# client model — apply session 29 standard pattern | — | Medium |
| TRC-M9 | Deprecate port 5010 | After HW validate | Low |
| MUTEX | buildTelemetry() race condition | — | Low |
| TRC-AUTOSTART | crontab → systemd — review during Task 3 build walk | Task 3 | Medium |

---

## Known Issues / Carry-Forward Warnings

1. **CUDA DNN unverified on lab asset.** If Task 1 Step 1.3 shows cmake cache not found,
   the lab Jetson received a binary copy without a native build. Run full inference probe
   (Step 1.5) to confirm actual backend. May need full OpenCV rebuild before Task 3.

2. **VimbaX path inconsistency.** Makefile has `VimbaX_2025-1`. Lab may have 2025-1
   installed. Update Makefile to `2026-1` and verify install path before `make clean && make`.

3. **ARCHITECTURE.md TRC version stale.** Code is at 3.0.2, arch doc shows 3.0.1.
   Update §15 version table as part of Task 2 doc finalization.

4. **gst-vmbsrc references.** Source confirms TRC does not use gst-vmbsrc. Remove any
   references from install documentation. VimbaX direct API is the confirmed approach.

5. **systemd-timesyncd NTP configured as .8.** Original install notes used 192.168.1.8
   (engineering bench IP). ARCHITECTURE.md §2.5 and PTP_TIMING_CONTEXT.md both confirm
   .33 is the correct primary. Verify and correct on both lab asset and fresh build.

6. **extlinux.conf USB buffer.** `usbcore.usbfs_memory_mb=1000` must be in kernel boot
   args. Confirm present on lab asset; script with backup for fresh build.

---

*This document IS DOC-2 (JETSON_SETUP.md) in progress — will be renamed and expanded
as scripts are finalized. See ARCHITECTURE.md §2.5 reference.*
