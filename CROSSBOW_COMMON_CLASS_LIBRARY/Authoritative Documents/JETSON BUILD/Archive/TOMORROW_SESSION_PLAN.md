# CROSSBOW / TRC — Session Plan
**Document:** TOMORROW_SESSION_PLAN.md  
**Date:** 2026-04-09 (session capture — updated with live baseline results 2026-04-06)  
**Platform:** Jetson Orin NX 16GB, Seeed Studio reComputer J4012 **non-Super** (J401 carrier)  
**Hardware note:** Two J4012 variants in fleet — non-Super (J401, this baseline) and Super J4012
(new carrier, higher TDP). Mechanically different. Images and procedures are not interchangeable.
Super J4012 setup is a separate future procedure.  
**Related:** JETSON_SETUP.md (DOC-2, in progress), OPENCV_BUILD_HISTORY.md, TRC_MIGRATION.md

---

## Context Summary

Four tasks for the next session. Task 1 is **complete** — baseline verified on live
hardware 2026-04-06. Tasks 2–4 are the remaining work.

| Task | Scope | Status |
|------|-------|--------|
| 1 | Verify lab setup baseline | ✅ Complete — 2026-04-06 |
| 2 | Finalize Jetson config docs, scripts, verification, image procedure | ✅ Complete — 2026-04-10 |
| 3 | Fresh Jetson build from scratch using new scripts | ✅ Complete — 3 units confirmed, all 54 PASS |
| 4 | TRC upgrades: VimbaX 2026-1, PTP/NTP (NEW-38d), OpenCV bridge eval | ⏳ Pending |

## Next Session — Phase 9

| Step | Action |
|------|--------|
| 9.1 | overlayFS setup and verification on Unit 3 |
| 9.2 | Image Unit 3 NVMe → USB drive |
| 9.3 | Restore image to a 4th unit and verify 54 PASS |
| 9.4 | Unit 1 cleanup — desktop removal, crontab production (U1-1, U1-2, U1-3) |

---

## Task 1 — COMPLETE — Verified Lab Setup Baseline (2026-04-06)

Baseline snapshot saved to `~/jetson_baseline_20260406.txt` on the lab Jetson.

### Confirmed good

| Item | Confirmed value | Notes |
|------|----------------|-------|
| JetPack | R36 / 36.4.4 | Correct for JetPack 6.2.1 |
| OpenCV | 4.12.0 | Custom build — not system apt |
| Python binding path | `/usr/local/lib/python3.10/dist-packages/` | dist-packages ✅ |
| CUDA DNN targets | `[6, 7]` — FP32 and FP16 | Compiled in ✅ |
| Inference probe | CUDA FP16: OK / CUDA FP32: OK / CPU: OK | All backends working ✅ |
| TRC binary | `libopencv_dnn.so.412 => /usr/local/lib/` | Links custom build ✅ |
| CUDA | 12.6 | Correct |
| cuDNN | 9.3.0 | Correct |
| GStreamer | 1.20.3 | Present |
| FFMPEG | YES | Present |
| nvpmodel | MAXN (mode 0) | Correct for J4012 J401 carrier |
| jetson_clocks | All 8 CPUs at 1984 MHz, GPU locked, EMC FreqOverride=1 | Enabled ✅ |
| Swap | 7.6 GB | Sufficient ✅ |
| USB buffer | `usbcore.usbfs_memory_mb=1000` in extlinux APPEND | Already set ✅ |
| VimbaX | 2025-1 installed | Working — upgrade to 2026-1 in Task 3 |
| libtbb-dev | 2021.5.0 installed | Present — just not passed to cmake in 4.12.0 build |

### Confirmed gaps — carry into Task 3

| Item | Current | Required | Action |
|------|---------|---------|--------|
| OpenCV version | 4.12.0 | 4.13.0 | Rebuild in Task 3 |
| `CUDA_FAST_MATH` | OFF | ON | Missing from 4.12.0 — add in 4.13.0 cmake |
| `ENABLE_FAST_MATH` | OFF | ON | Missing from 4.12.0 — add in 4.13.0 cmake |
| `WITH_TBB` | OFF | ON | libtbb-dev installed — just not passed to cmake |
| `WITH_OPENGL` | OFF | ON | Minor — add in 4.13.0 cmake |
| VimbaX | 2025-1 | 2026-1 | Upgrade in Task 3 |
| Makefile VIMBAX_DIR | `VimbaX_2025-1` | `VimbaX_2026-1` | Update v3.0.2 Makefile before Task 3 build |
| jetson-stats | 4.3.2 | latest | Update in Task 2 base setup script |
| PTP (ptp4l/phc2sys) | Not installed | Install | Task 2 timing script |

### Fixed during baseline session (2026-04-06)

**NTP server corrected.** `timesyncd.conf` had `NTP=192.168.1.8` (engineering bench IP —
explicitly wrong per ARCHITECTURE.md §2.5 and PTP_TIMING_CONTEXT.md). Corrected to:

```ini
[Time]
NTP=192.168.1.33
FallbackNTP=192.168.1.208
```

`System clock synchronized: no` after fix — expected. `.33` is GPS-disciplined and
requires outdoor sky view. Will sync automatically on deployment outdoors.

**RTC note:** `RTC time: 1970-01-01 00:31:35` — hardware RTC has never been set.
Will update automatically after first successful NTP sync. Not a deployment blocker.

### CUDA DNN fast-gate command (corrected for OpenCV 4.12.x)

`getAvailableBackends()` does not exist in OpenCV 4.12.x. Correct command:

```bash
python3 -c "
import cv2
print('OpenCV:    ', cv2.__version__)
print('From:      ', cv2.__file__)
print('CUDA DNN:  ', cv2.cuda.getCudaEnabledDeviceCount() > 0)
print('Targets:   ', cv2.dnn.getAvailableTargets(cv2.dnn.DNN_BACKEND_CUDA))
"
# Pass: Targets: [6, 7]   (6=FP32, 7=FP16)
# Fail: Targets: []  → rebuild required
```

### Full inference probe

```bash
# Pre-check: model files must exist
ls ~/CV/TRCv3/v3.0.2/model_data/frozen_inference_graph.pb \
   ~/CV/TRCv3/v3.0.2/model_data/ssd_mobilenet_v3_large_coco_2020_01_14.pbtxt

cd ~/CV/TRCv3/v3.0.2
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
# Confirmed result on lab asset:
#   CUDA FP16: OK
#   CUDA FP32: OK
#   CPU: OK
```

### Autostart — crontab review needed

Current crontab on lab asset:
```
SHELL=/bin/bash
@reboot bash -l -c 'sleep 60 && ./ben.sh'
```

`./ben.sh` is a relative path with no working directory specified. The `-l` login shell
flag may cause cron to start in `$HOME` which would make this work, but it is fragile.
Review during Task 3 build walkthrough — compare behaviour on fresh build, consider
converting to systemd unit with proper `WorkingDirectory=` and `After=` dependencies.

---

## Task 2 — Finalize Jetson Config Documentation and Scripts

**Goal:** Produce JETSON_SETUP.md (DOC-2 per ARCHITECTURE.md §2.5) plus all helper
scripts. Everything needed to bring a fresh Jetson from JetPack flash to production TRC.

### Scripts to produce

| File | Status | Notes |
|------|--------|-------|
| `OPENCV_BUILD_HISTORY.md` | ✅ Done | Gen 1/2/3 comparison — updated this session |
| `install_opencv4.13.0_Jetpack6.2.1.sh` | ✅ Done | Full Gen 3 build script |
| `JETSON_SETUP.md` | ⏳ Write | DOC-2 — master setup narrative |
| `00_base_setup.sh` | ⏳ Write | apt-mark hold, swap, pip, jtop update |
| `01_power_setup.sh` | ⏳ Write | nvpmodel, jetson_clocks on boot |
| `02_timing_setup.sh` | ⏳ Write | timesyncd (.33/.208) + ptp4l/phc2sys install |
| `03_install_vimba.sh` | ⏳ Write | VimbaX 2026-1 + CTI USB + verify |
| `04_verify_all.sh` | ⏳ Write | Automated verification suite |
| `05_image_and_overlay.sh` | ⏳ Write | overlayFS enable + dd image procedure |

### All decisions locked

| Item | Decision |
|------|----------|
| apt upgrade | `sudo apt-mark hold nvidia-l4t-*` before any upgrade |
| jetson_clocks | Already enabled on current unit — script must persist on fresh build |
| Fan profile | `cool` — set manually in jtop, saved. Not scripted. |
| VimbaX download | Manual from alliedvision.com — script assumes pre-downloaded tarball |
| VimbaX version | 2026-1 ARM64 — tested against JetPack 6.2.1 |
| VimbaX CTI | USB only (`VimbaUSBTL_Install.sh`) — Alvium 1800 U-291c is USB3 |
| gst-vmbsrc | NOT used — confirmed from Makefile. Remove from all documentation. |
| USB buffer | Already set on this unit — `03_install_vimba.sh` must verify/set on fresh build |
| PYTHONPATH | `dist-packages` — confirmed correct on current unit |
| NTP primary | 192.168.1.33 — corrected on current unit 2026-04-06 |
| NTP fallback | 192.168.1.208 — corrected on current unit 2026-04-06 |
| PTP grandmaster | 192.168.1.30 (NovAtel GNSS, IEEE 1588 2-step, domain 0, UTC_TIME) |
| Autostart | Review crontab vs systemd during Task 3 build walkthrough |
| Overlay mode | Enable after stable build confirmed — then image |
| Headless | Separate optional script — not in base setup |
| OpenCV version | 4.13.0 for fresh build |
| TRC active build path | `/home/ipg/CV/TRCv3/v3.0.2/` |

### overlayFS + imaging procedure outline

```bash
# overlayFS
sudo apt install overlayroot
# Edit /etc/overlayroot.conf:  overlayroot="tmpfs"
sudo reboot
mount | grep overlay   # verify

# Image — offline, boot from USB
sudo dd if=/dev/nvme0n1 of=/media/usb/jetson_trc_YYYYMMDD.img bs=4M status=progress

# Restore to new unit
sudo dd if=jetson_trc_YYYYMMDD.img of=/dev/nvme0n1 bs=4M status=progress
```

---

## Task 3 — Fresh Jetson Build from Scratch

### Build order

```
JetPack 6.2.1 — flashed via SDK Manager on host Ubuntu PC
  └── 00_base_setup.sh
        └── 01_power_setup.sh
              └── 02_timing_setup.sh
                    └── 03_install_vimba.sh    ← pre-download VimbaX 2026-1 ARM64 first
                          └── install_opencv4.13.0_Jetpack6.2.1.sh
                                └── TRC build  ← update Makefile to VimbaX 2026-1 first
                                      └── 04_verify_all.sh
                                            └── Hardware test (camera, COCO FP16, stream)
                                                  └── Autostart review
                                                        └── 05_image_and_overlay.sh
```

### Makefile change required before TRC build

Only `v3.0.2/Makefile` — leave archive versions unchanged:

```makefile
VIMBAX_DIR := /opt/VimbaX_2026-1    # was: VimbaX_2025-1
```

After rebuild:
```bash
make clean && make -j$(nproc)
ldd ./trc | grep -E "opencv_dnn|VmbC"
# Must show:
#   libopencv_dnn.so.413 => /usr/local/lib/libopencv_dnn.so.413
#   libVmbCPP.so => /opt/VimbaX_2026-1/api/lib/libVmbCPP.so
```

### Checkpoint comparison against Task 1 baseline

```bash
diff ~/jetson_baseline_20260406.txt ~/jetson_fresh_YYYYMMDD.txt
```

Expected differences: OpenCV 4.12.0 → 4.13.0, VimbaX 2025-1 → 2026-1.  
Expected same: CUDA/cuDNN, GStreamer, NTP on .33, nvpmodel MAXN, jetson_clocks enabled.

---

## Task 4 — TRC Source Upgrades

### 4.1 VimbaX 2026-1 Makefile path

Done as part of Task 3. No TRC source changes beyond Makefile for version bump.

### 4.2 NEW-38d — PTP/NTP TIMESRC implementation

**Confirmed from source audit:** `ntpEpochTime` is telemetry/correlation only —
not in the fire control vote chain. Medium priority, not a blocker.

**`/dev/ptp*` not present on current unit** — tooling not installed yet. Confirm
PHC availability after `02_timing_setup.sh` installs `ptp4l`. If `/dev/ptp0` present,
hardware timestamping available (sub-µs). If absent, software timestamping (~1–100µs).

#### Scope

| Work item | File | Detail |
|-----------|------|--------|
| SNTP client class | `ntp_client.hpp/cpp` (new) | Queries .33, fallback .208 after 3 misses |
| PHC reader | `phc_reader.hpp` (new) | Reads `/dev/ptp0` |
| `TIMESRC` ASCII handler | `udp_listener.cpp` port 5012 | `TIMESRC PTP\|NTP\|AUTO\|OFF` + `TIME` |
| `ntpEpochTime` source | `udp_listener.cpp` `buildTelemetry()` | Replace `system_clock::now()` |
| `TIME_BITS` byte | `telemetry.h` byte [49] | `RESERVED[0]` — same layout as MCC/BDC/TMC |
| `MSG_TRC.cs` | C# ENG GUI | Decode `TIME_BITS` at [49], add to TRC tab |

#### telemetry.h change

```cpp
// Replace:
uint8_t   RESERVED[15];        // [49-63]

// With:
uint8_t   time_bits;           // [49]  bit0=isPTP_Enabled bit1=ptp_isSynched
                               //       bit2=usingPTP      bit3=ntp_isSynched
                               //       bit4=ntpUsingFallback bit5=ntpHasFallback
uint8_t   RESERVED[14];        // [50-63]

static_assert(offsetof(TelemetryPacket, time_bits) == 49, "time_bits at offset 49");
static_assert(offsetof(TelemetryPacket, RESERVED)  == 50, "RESERVED at offset 50");
```

BDC parse of TRC block at bytes [60-123] is unaffected. `make clean && make` mandatory.

### 4.3 VimbaX OpenCV bridge evaluation (new in VimbaX 2025-3+)

`VmbOpenCVHelper.h` — zero-copy `cv::Mat` wrapping of VimbaX frame buffers.  
Measure compositor `dt_us` before/after. If marked improvement → implement in
`alvium_camera.cpp`. If marginal → document as evaluated, keep existing copy path.

### 4.4 Version bump

```cpp
g_state.version_word = VERSION_PACK(3, 1, 0);   // was 3.0.2
```

Update ARCHITECTURE.md §15 TRC: `3.0.2` → `3.1.0`.

---

---

## Unit 1 Open Actions (192.168.1.22 — lab baseline)

| ID | Action | Command |
|----|--------|---------|
| U1-1 | Remove desktop/LibreOffice | `sudo systemctl stop gdm3 && sudo systemctl disable gdm3 && sudo systemctl set-default multi-user.target && sudo apt remove --purge ubuntu-desktop gdm3 -y && sudo apt purge libreoffice* -y && sudo apt autoremove -y && sudo reboot` |
| U1-2 | Switch crontab to production | `crontab -e` → change to `trc_start.sh` |
| U1-3 | Run 04_verify_all.sh after above | Confirm 54 PASS, 0 WARN, 0 FAIL |

---

### Production (multicast, live MWIR)
```bash
cd ~/CV/TRC
./trc --dest-host 239.127.1.21 --mwir-live --view PIP &> trc.log
```

### Bench / test (MWIR test source, unicast to Windows)
```bash
cd ~/CV/TRC
./trc --dest-host 192.168.1.208 --view PIP
```

### View switching (ASCII commands via UDP port 5012)
```bash
echo "SELECT CAM1" | nc -u -w1 192.168.1.22 5012   # VIS only
echo "SELECT CAM2" | nc -u -w1 192.168.1.22 5012   # MWIR only
echo "SELECT PIP"  | nc -u -w1 192.168.1.22 5012   # PIP (both cameras)
echo "SELECT PIP8" | nc -u -w1 192.168.1.22 5012   # PIP 8-way
echo "DEBUG ON"    | nc -u -w1 192.168.1.22 5012   # Enable debug output
```

> **Note:** `--view` at launch sets the default. View can be changed at runtime
> via ASCII commands on port 5012 without restarting TRC.

---

## TRC Open Actions (Task 4)

| ID | Item | Detail |
|----|------|--------|
| TRC-CAM-1 | VIS test source flag | Add `--vis-test` launch argument to substitute videotestsrc for Alvium camera — allows full pipeline testing without physical camera |
| TRC-CAM-2 | Camera index / ID selection | Add `--alvium-id <DEV_xxx>` launch argument to select camera by ID explicitly — prevents simulator cameras (added in VimbaX 2026-1) from being selected if ordering changes |
| TRC-CAM-3 | Camera simulator filtering | Currently `cameras[0]` works because VimbaX orders real hardware first. Add explicit filter to skip `Camera Simulator` entries for robustness |

| ID | Item | Task | Priority |
|----|------|------|----------|
| NEW-38d | TRC PTP integration — TIME_BITS, MSG_TRC.cs, ptp4l | 4 | Medium |
| DOC-1 | Add TRC NTP/PTP reference to ARCHITECTURE.md §2.5 | 2 | Medium |
| DOC-2 | Create JETSON_SETUP.md | 2 | Medium |
| GUI-8 | TRC C# client model — apply session 29 standard | — | Medium |
| TRC-M9 | Deprecate port 5010 | After HW validate | Low |
| MUTEX | buildTelemetry() race condition | — | Low |
| TRC-AUTOSTART | crontab → systemd review | 3 | Medium |
| ARCH-TRC-VER | ARCHITECTURE.md §15 TRC version: 3.0.1 → 3.0.2 | 2 | Minor |

---

## Carry-Forward Warnings

1. **NTP corrected on current unit** — fresh build must also get `.33`/`.208`. Do not
   copy old `timesyncd.conf` — always use `02_timing_setup.sh`.

2. **RTC not set on current unit** — resolves after first outdoor NTP sync. Not a blocker.

3. **VimbaX archive Makefiles** — all archives still reference 2025-1. Only
   `v3.0.2/Makefile` needs updating. Archives intentionally left as-is.

4. **gst-vmbsrc** — confirmed not used. Remove from any doc still referencing it.

5. **ARCHITECTURE.md §15 TRC version** — code is 3.0.2, arch doc shows 3.0.1. Fix in Task 2.

6. **Performance flags missing from 4.12.0** (`CUDA_FAST_MATH`, `ENABLE_FAST_MATH`,
   `WITH_TBB`) — current COCO FP16 inference is working correctly. These are improvements
   only, captured in the 4.13.0 install script.

7. **TRC active build path confirmed** — `/home/ipg/CV/TRCv3/v3.0.2/`. Use this path
   in all scripts and documentation. Do not reference `v20` (old archive path).

---

*This document is DOC-2 (JETSON_SETUP.md) in progress — will be renamed and expanded
as scripts are finalized. See ARCHITECTURE.md §2.5.*
