# OpenCV 4.11.0 — CUDA DNN Build Procedure
# CROSSBOW / TRC — Jetson Orin NX (aarch64 / JetPack)

**Document Version:** 1.0  
**Date:** 2026-03-09  
**Applies to:** Jetson Orin NX, CUDA 12.6, cuDNN 9.3.0, JetPack  
**Purpose:** Rebuild OpenCV with `OPENCV_DNN_CUDA=ON` to enable COCO inference on GPU (CUDA FP16). Required for TRC COCO-01 hardware validation.

---

## 1. Prerequisites

### 1.1 Confirm CUDA and cuDNN are installed

```bash
nvcc --version
# Expect: release 12.6

find /usr/include -name "cudnn*.h" 2>/dev/null
# Expect: /usr/include/cudnn.h or cudnn_version.h

find /usr/lib/aarch64-linux-gnu -name "libcudnn*.so" 2>/dev/null | head -3
# Expect: /usr/lib/aarch64-linux-gnu/libcudnn.so.*
```

If cuDNN headers are missing, install:
```bash
sudo apt-get install libcudnn8-dev
```

### 1.2 Required source trees

Both must be present side-by-side in the same parent directory:

```
<workspace>/
├── build_opencv.sh
├── opencv-4.11.0/
└── opencv_contrib-4.11.0/
```

Download if not present:
```bash
version=4.11.0
wget -O opencv.zip https://github.com/opencv/opencv/archive/${version}.zip
wget -O opencv_contrib.zip https://github.com/opencv/opencv_contrib/archive/${version}.zip
unzip opencv.zip && unzip opencv_contrib.zip
```

---

## 2. Build Script

Use `build_opencv.sh` (included in outputs). Run from the workspace directory containing both source trees:

```bash
chmod +x build_opencv.sh
./build_opencv.sh          # uses default version 4.11.0
./build_opencv.sh 4.11.0   # explicit version
```

### Key cmake flags (relative to original build script — one addition):

| Flag | Value | Purpose |
|------|-------|---------|
| `OPENCV_DNN_CUDA` | `ON` | **Added** — enables CUDA kernels in DNN module |
| `WITH_CUDA` | `ON` | CUDA support |
| `WITH_CUDNN` | `ON` | cuDNN integration |
| `CUDA_ARCH_BIN` | `8.7` | Orin NX = Ampere sm_87 |
| `CUDA_ARCH_PTX` | `""` | No PTX — reduces build time |
| `OPENCV_GENERATE_PKGCONFIG` | `ON` | Required for TRC Makefile |
| `CMAKE_INSTALL_PREFIX` | `/usr/local` | Install target |

### Build time

45–90 minutes on Orin NX with `make -j$(nproc)`. The DNN CUDA module (`cuda_compile_1_generated_*.cu.o`) is the slow part — 25 CUDA kernel files compiled at arch 8.7.

### cmake abort gate

The script reads `CMakeCache.txt` after cmake and aborts if `OPENCV_DNN_CUDA` did not resolve to `ON` — prevents a 90-minute make on a misconfigured build. If it aborts, check cuDNN header paths (see §5 Troubleshooting).

---

## 3. Environment Variables

After install, the following must be set. The build script adds them to `~/.bashrc` automatically, but verify they are present:

```bash
grep "LD_LIBRARY_PATH\|PYTHONPATH\|PKG_CONFIG" ~/.bashrc
```

Expected entries:
```bash
export LD_LIBRARY_PATH=/usr/local/lib:$LD_LIBRARY_PATH
export PYTHONPATH=/usr/local/lib/python3.10/dist-packages/:$PYTHONPATH
```

**Important — `dist-packages` not `site-packages`:** On Debian/Ubuntu-derived systems (including JetPack), cmake installs Python bindings to `dist-packages`. The OpenCV build documentation says `site-packages` but this is wrong for this platform. If `python3 -c "import cv2; print(cv2.__version__)"` returns the old version, this is the cause.

Apply immediately without rebooting:
```bash
source ~/.bashrc
```

For TRC build, also ensure pkg-config finds the new install:
```bash
export PKG_CONFIG_PATH=/usr/local/lib/pkgconfig:$PKG_CONFIG_PATH
```

Add to `~/.bashrc` if not already present.

---

## 4. Verification Steps

Run these in order. Each must pass before proceeding.

### Step 1 — Confirm libs installed

```bash
ls /usr/local/lib/libopencv_dnn.so.4.11.0
# Must exist
```

### Step 2 — Confirm Python binding installed

```bash
find /usr/local -name "cv2*.so" 2>/dev/null
# Expect: /usr/local/lib/python3.10/dist-packages/cv2/python-3.10/cv2.cpython-310-aarch64-linux-gnu.so
```

### Step 3 — Confirm Python loads the new build

```bash
python3 -c "import cv2; print(cv2.__file__); print(cv2.__version__)"
# Expect:
#   /usr/local/lib/python3.10/dist-packages/cv2/__init__.py
#   4.11.0
```

If version is wrong, `PYTHONPATH` is not set or points to `site-packages`. Fix:
```bash
export PYTHONPATH=/usr/local/lib/python3.10/dist-packages/:$PYTHONPATH
```

### Step 4 — Confirm CUDA and cuDNN in build info

```bash
python3 -c "import cv2; print(cv2.getBuildInformation())" | grep -E "CUDA|cuDNN"
# Expect:
#   NVIDIA CUDA:    YES (ver 12.6, ...)
#   cuDNN:          YES (ver 9.3.0)
```

**Note:** OpenCV 4.11.0 does not print `OPENCV_DNN_CUDA: YES` as a labeled line in `getBuildInformation()` — this label was removed in this version. CUDA DNN is confirmed by the cmake cache instead (see Step 5).

### Step 5 — Confirm CUDA DNN in cmake cache

```bash
grep "OPENCV_DNN_CUDA\|CUDA_OBJECTS" ~/CV/SETUP/workspace/opencv-4.11.0/release/CMakeCache.txt | head -5
# Expect:
#   OPENCV_DNN_CUDA:BOOL=ON
#   OPENCV_MODULE_opencv_dnn_CUDA_OBJECTS:INTERNAL=...cu.o;...cu.o (25 files)
```

### Step 6 — Python inference probe (run from TRC directory)

```bash
cd ~/CV/TRCv3/v20
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
# Expect:
#   CUDA FP16: OK
#   CUDA FP32: OK
#   CPU: OK
```

### Step 7 — Confirm TRC links against new build

```bash
ldd ~/CV/TRCv3/v20/multi_streamer | grep opencv_dnn
# Expect:
#   libopencv_dnn.so.411 => /usr/local/lib/libopencv_dnn.so.411
# Must NOT show: /usr/lib/aarch64-linux-gnu/libopencv_dnn.so (old system build)
```

If the wrong library appears, `LD_LIBRARY_PATH` is not set or `/usr/local/lib` is not before `/usr/lib`. Fix and rebuild TRC.

### Step 8 — TRC runtime COCO probe

```bash
cd ~/CV/TRCv3/v20
./multi_streamer --dest-host 192.168.1.208 &

trc3() { echo "$*" | nc -u -w1 192.168.1.22 5012; }
trc3 DEBUG ON
trc3 COCO LOAD
```

Expected log output:
```
[COCO] Loaded 91 class names from model_data/coco.names
[COCO] CUDA FP16 backend active
[COCO] Model loaded successfully
[COCO] Inference thread started
[UDP] COCO LOAD OK
```

Must **NOT** see:
```
[COCO] CUDA backend unavailable — falling back to CPU
```

### Step 9 — GPU load confirmation

```bash
trc3 SELECT CAM1
trc3 TRACKBOX 128 128 640 360
trc3 TRACKER ON
trc3 COCO ON
watch -n1 tegrastats
```

With CUDA FP16 active, inference load should appear on the GPU side of `tegrastats`, not driving a CPU core to 100%.

---

## 5. Troubleshooting

### cmake abort — `OPENCV_DNN_CUDA` not ON

cuDNN headers not found by cmake. Add explicit paths:
```bash
-D CUDNN_LIBRARY=/usr/lib/aarch64-linux-gnu/libcudnn.so \
-D CUDNN_INCLUDE_DIR=/usr/include \
```

### Python imports old cv2 after install

`PYTHONPATH` missing or pointing to `site-packages` instead of `dist-packages`:
```bash
export PYTHONPATH=/usr/local/lib/python3.10/dist-packages/:$PYTHONPATH
```

Verify the binding was installed:
```bash
find /usr/local -name "cv2*.so"
```

If not found, cmake may have used a different Python. Check:
```bash
grep "PYTHON" ~/CV/SETUP/workspace/opencv-4.11.0/release/CMakeCache.txt \
  | grep -E "EXECUTABLE|PACKAGES_PATH|INSTALL"
```

### TRC links against system opencv

`LD_LIBRARY_PATH` not set or not exported before make:
```bash
export LD_LIBRARY_PATH=/usr/local/lib:$LD_LIBRARY_PATH
make clean && make -j$(nproc)
ldd multi_streamer | grep opencv_dnn  # verify
```

### `[COCO] CUDA backend unavailable` after correct library confirmed

`setInputParams()` must be called before the backend probe — `DetectionModel::detect()` calls `setInput()` internally and CUDA is stricter than CPU about input tensor format. Ensure `coco_detector.cpp` from outputs (v post-fix) is deployed. Check probe log — if it shows `CUDA FP16 backend active` the fix is in place.

### Competing system OpenCV

```bash
dpkg -l | grep libopencv | awk '{print $2, $3}'
```

If a system opencv is present alongside the new build, `/usr/local/lib` must be first in `LD_LIBRARY_PATH` and `/usr/local/lib/pkgconfig` must be first in `PKG_CONFIG_PATH`. Do not remove the system package — other system tools may depend on it.

---

## 6. Known Issues / Notes

| Issue | Resolution |
|-------|-----------|
| `getBuildInformation()` does not show `OPENCV_DNN_CUDA: YES` | Normal for 4.11.0 — label removed. Confirm via cmake cache instead (Step 5). |
| `COCO LOAD` shows CPU fallback despite correct library | `setInputParams()` ordering bug in probe — deploy updated `coco_detector.cpp` from outputs. |
| `dist-packages` vs `site-packages` | JetPack/Ubuntu uses `dist-packages` for system-managed packages. Build script detects this automatically. |
| First inference after cold boot takes 1–3s | Normal — CUDA kernel JIT warm-up. Only happens once per binary run. |
| `make clean` required after `camera_base.h` changes | `cocoFrameInterval_` and `lkEnabled_` atomics added — all dependent TUs must recompile. |
