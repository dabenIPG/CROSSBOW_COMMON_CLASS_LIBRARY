# OpenCV Build History — Jetson Orin NX (JetPack 6.2.x)
# CROSSBOW / TRC Platform

**Document:** OPENCV_BUILD_HISTORY.md  
**Last updated:** 2026-04-09  
**Applies to:** Seeed Studio reComputer J4012, Orin NX 16GB, Ubuntu 22.04, CUDA 12.6, cuDNN 9.3  

---

## Overview

This document captures the three-generation history of OpenCV builds on the TRC Jetson platform,
the issues discovered at each stage, and the rationale for each change. Use this as the reference
when performing future upgrades.

| Generation | Version | Date | Status |
|---|---|---|---|
| Gen 1 | 4.8.0 | Initial JetPack install | System package — no CUDA DNN, replaced |
| Gen 2 | 4.12.0 | 2026-03-09 | Custom build — CUDA DNN enabled. Confirmed active on lab Jetson 2026-04-06 |
| Gen 3 | 4.13.0 | Target | Full flag set — upgrade pending |

---

## Platform Constants (all generations)

| Item | Value |
|---|---|
| SoC | Jetson Orin NX 16GB |
| Carrier | Seeed Studio J401 (**non-Super** variant — J4012 original) |
| **Hardware note** | Two J4012 variants exist: non-Super (J401, this document) and Super J4012 (new carrier). Mechanically different — images not interchangeable. |
| CUDA Arch | `8.7` (Ampere SM 87 — do not change) |
| PTX | `""` (empty — device matches exactly, no JIT fallback needed) |
| Ubuntu | 22.04 LTS |
| Python | 3.10.x (system) |
| CUDA | 12.6 (JetPack bundled) |
| cuDNN | 9.3.0 (JetPack bundled) |
| Install prefix | `/usr/local` |
| Python binding path | `/usr/local/lib/python3.10/dist-packages/` |

> **Note — `dist-packages` not `site-packages`:** On JetPack/Ubuntu, cmake installs Python bindings
> to `dist-packages`. Using `site-packages` in `PYTHONPATH` causes `import cv2` to silently load
> the old system build with no error. This was discovered during the Gen 2 build. All scripts and
> `.bashrc` exports must use `dist-packages`.

---

## cmake Flag Comparison — All Three Generations

| Flag / Setting | Gen 1 (4.8.0) | Gen 2 (4.12.0) | Gen 3 (4.13.0) |
|---|---|---|---|
| **Version** | | | |
| OpenCV version | 4.8.0 (system apt) | 4.12.0 (confirmed live 2026-04-06) | 4.13.0 |
| **CUDA core** | | | |
| `WITH_CUDA` | ✗ | ON | ON |
| `WITH_CUDNN` | ✗ | ON | ON |
| `CUDA_ARCH_BIN` | — | `"8.7"` | `"8.7"` |
| `CUDA_ARCH_PTX` | — | `""` | `""` |
| `OPENCV_DNN_CUDA` | ✗ | **ON** ← key fix | ON |
| **CUDA performance** | | | |
| `CUDA_FAST_MATH` | ✗ | ✗ | **ON** ← added Gen 3 |
| `ENABLE_FAST_MATH` | ✗ | ✗ | **ON** ← added Gen 3 |
| `WITH_CUBLAS` | ✗ | ✗ | **ON** ← added Gen 3 |
| **CPU threading** | | | |
| `WITH_TBB` | ✗ | ✗ | **ON** ← added Gen 3 |
| `BUILD_TBB` | — | — | `OFF` (use system libtbb-dev) |
| **Python** | | | |
| `BUILD_opencv_python3` | ✗ | ON | ON |
| `BUILD_opencv_python2` | — | ✗ not set | `OFF` (explicit) |
| `PYTHON3_EXECUTABLE` | — | ✗ not set | `$(which python3)` |
| `PYTHON3_PACKAGES_PATH` | — | ✗ not set | `/usr/local/lib/python3.10/dist-packages` |
| **PYTHONPATH export** | — | `dist-packages` ✓ | `dist-packages` ✓ |
| **I/O and hardware** | | | |
| `WITH_GSTREAMER` | system | ON | ON |
| `WITH_LIBV4L` | system | ON | ON |
| `WITH_OPENGL` | ✗ | ✗ | **ON** ← added Gen 3 |
| `WITH_FFMPEG` | system | ON (implicit) | ON (explicit) |
| `OPENCV_GENERATE_PKGCONFIG` | ✗ | ON | ON |
| **Build settings** | | | |
| `CMAKE_BUILD_TYPE` | — | RELEASE | RELEASE |
| `CMAKE_INSTALL_PREFIX` | `/usr` | `/usr/local` | `/usr/local` |
| `BUILD_TESTS` | — | OFF | OFF |
| `BUILD_PERF_TESTS` | — | OFF | OFF |
| `BUILD_EXAMPLES` | — | OFF | OFF |
| `OPENCV_EXTRA_MODULES_PATH` | — | opencv_contrib | opencv_contrib |
| **Script robustness** | | | |
| cmake abort gate | ✗ | ✓ (checks CMakeCache) | ✓ carry forward |
| Workspace idempotency | ✗ | ✗ | ✓ added Gen 3 |
| Swap check before make | ✗ | ✗ | ✓ added Gen 3 |
| Build log capture | ✗ | ✗ | ✓ added Gen 3 |
| Post-build verification | ✗ | ✓ 9-step sequence | ✓ carry forward |
| **apt prerequisites** | | | |
| `libtbb2` | — | ⚠ wrong on Ubuntu 22.04 | → `libtbb-dev` ✓ |
| `libcudnn8-dev` | — | documented in §1.1 | prereq assert |

---

## Key Issues Discovered Per Generation

### Gen 1 → Gen 2: What prompted the rebuild

- **No CUDA DNN** — system OpenCV 4.8.0 from apt has no CUDA support compiled in.
  `cv::dnn::DNN_BACKEND_CUDA` is not available. All inference ran on CPU.
- **Wrong version for TRC** — COCO inference (SSD MobileNet v3, FP16) requires
  `OPENCV_DNN_CUDA=ON` for the CUDA execution path to be available at runtime.

### Gen 2 (4.12.0) — Issues found during build and deployment

1. **`OPENCV_DNN_CUDA` missing from original script** — The install script did not include this
   flag. It was discovered when `[COCO] CUDA backend unavailable` appeared in TRC logs even after
   rebuilding. Added as the single delta from the original script. Added cmake abort gate to
   catch this before a 90-minute make.

2. **`dist-packages` vs `site-packages`** — The original script's `.bashrc` export used
   `site-packages`. On Ubuntu 22.04 / JetPack, cmake installs the binding to `dist-packages`.
   Symptom: `cv2.__version__` returned `4.8.0` after a successful 4.12.0 build. Fix: change
   `PYTHONPATH` export in `.bashrc` to reference `dist-packages`.

3. **Version confirmed 4.12.0** — Live baseline check on lab Jetson 2026-04-06 confirmed
   cv2.__version__ = 4.12.0. Earlier session notes incorrectly recorded this as 4.11.0.
   No functional issue — resolved in Gen 3 by targeting 4.13.0.

4. **`libtbb2` package name** — On Ubuntu 22.04, `libtbb2` was renamed to `libtbb12`.
   `libtbb2` may install as a transitional stub but is not reliable. Correct package is
   `libtbb-dev`. Not corrected in Gen 2 (TBB not explicitly enabled).

5. **`getBuildInformation()` does not report `OPENCV_DNN_CUDA: YES`** — This label was
   removed in OpenCV 4.11.x+. Confirmation must come from CMakeCache.txt instead:
   ```bash
   grep "OPENCV_DNN_CUDA" <workspace>/opencv-4.12.0/release/CMakeCache.txt
   # Expect: OPENCV_DNN_CUDA:BOOL=ON
   ```

6. **Old `/usr/local` `.so` stubs after version upgrade** — cmake install does not remove
   prior version `.so` files. When upgrading (e.g. 4.12.0 → 4.13.0), the old
   `libopencv_dnn.so.411` remains in `/usr/local/lib`. Any binary linked against the old
   soname continues to load the old library silently. `make clean && make` in TRC is
   mandatory after any OpenCV version change. Old `.so` files should be explicitly removed:
   ```bash
   sudo rm -f /usr/local/lib/libopencv_*.so.411
   sudo ldconfig
   ```

### Gen 3 (4.13.0) — Changes and rationale

- **`OPENCV_DNN_CUDA=ON`** — Carried from Gen 2. Essential for CUDA FP16 inference.
- **`CUDA_FAST_MATH=ON` + `ENABLE_FAST_MATH=ON`** — Not in Gen 2. Enables `--use_fast_math`
  in NVCC and CPU-side fused multiply-add. Meaningful throughput gain for pixel math and
  convolution kernels. Acceptable precision trade-off for CV workloads.
- **`WITH_CUBLAS=ON`** — Not in Gen 2. Routes GEMM ops in `cv::dnn` through cuBLAS instead
  of OpenCV's own CUDA GEMM. Relevant for any layer type that is matrix-multiply-heavy
  (fully connected, attention).
- **`WITH_TBB=ON` + `BUILD_TBB=OFF`** — Not in Gen 2. Uses system TBB (`libtbb-dev`) for
  CPU parallelism. Better scheduling on Orin's heterogeneous CPU cluster than the default
  OpenMP threading.
- **Explicit Python flags** — `PYTHON3_EXECUTABLE`, `PYTHON3_PACKAGES_PATH` — prevents cmake
  from selecting a wrong Python from PATH.
- **`BUILD_opencv_python2=OFF`** — Explicit, prevents cmake auto-detection confusion.
- **4.13.0 notable fixes relevant to TRC:**
  - `cv::minAreaRect` angle range bug fixed (was broken 4.5.1–4.12.0) — affects any bounding
    box rotation math in the tracker.
  - NumPy 2.x compatibility resolved (was broken in 4.12.0).
  - JSON/AVIF/EXR codec improvements.

---

## Upgrade Procedure: Gen 2 → Gen 3

When upgrading from 4.12.0 to 4.13.0 on an active TRC system:

```bash
# 1. Run the new build script (will purge old OpenCV if selected)
chmod +x install_opencv4.13.0_Jetpack6.2.1.sh
./install_opencv4.13.0_Jetpack6.2.1.sh

# 2. Remove old /usr/local .so files from prior build
sudo rm -f /usr/local/lib/libopencv_*.so.411
sudo ldconfig

# 3. Verify new version is active
python3 -c "import cv2; print(cv2.__version__)"
# Expect: 4.13.0

# 4. Rebuild TRC — mandatory (soname change .411 → .413)
cd ~/CV/TRCv3/v20
make clean && make -j$(nproc)

# 5. Verify TRC links against new build
ldd ./trc | grep opencv_dnn
# Must show: libopencv_dnn.so.413 => /usr/local/lib/libopencv_dnn.so.413
# Must NOT show: libopencv_dnn.so.411

# 6. Run CUDA inference probe (from OPENCV_CUDA_BUILD.md §4 Step 6)
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

---

## Environment Variables (all generations, post Gen 2 correction)

Add to `~/.bashrc` — verify these are present and correct:

```bash
export LD_LIBRARY_PATH=/usr/local/lib:$LD_LIBRARY_PATH
export PYTHONPATH=/usr/local/lib/python3.10/dist-packages/:$PYTHONPATH
export PKG_CONFIG_PATH=/usr/local/lib/pkgconfig:$PKG_CONFIG_PATH
```

> **Note:** `PKG_CONFIG_PATH` is required for TRC Makefile to locate `libopencv_dnn` and
> other modules via `pkg-config --libs opencv4`.

---

## Quick Verification Reference

### Step 1 — Fast gate (no model files needed, run first)

```bash
python3 -c "
import cv2
backends = cv2.dnn.getAvailableBackends()
cuda = [b for b in backends if b[0] == cv2.dnn.DNN_BACKEND_CUDA]
print('OpenCV:    ', cv2.__version__)
print('From:      ', cv2.__file__)
print('CUDA DNN:  ', 'COMPILED IN' if cuda else 'NOT COMPILED — rebuild required')
print('Backends:  ', cuda if cuda else backends)
"
```

If `NOT COMPILED` → rebuild immediately with `install_opencv4.13.0_Jetpack6.2.1.sh`.
If `COMPILED IN` → continue to Step 2.

### Step 2 — Full inference probe (run after Step 1 passes)

```bash
# Pre-check model files exist
ls ~/CV/TRCv3/v20/model_data/frozen_inference_graph.pb \
   ~/CV/TRCv3/v20/model_data/ssd_mobilenet_v3_large_coco_2020_01_14.pbtxt

cd ~/CV/TRCv3/v20
python3 -c "
import cv2, numpy as np
net = cv2.dnn.readNet('model_data/frozen_inference_graph.pb',
                      'model_data/ssd_mobilenet_v3_large_coco_2020_01_14.pbtxt')
blob = cv2.dnn.blobFromImage(np.zeros((320,320,3), dtype=np.uint8),
                             1/127.5, (320,320), (127.5,127.5,127.5), swapRB=True)
for backend, target, label in [
    (cv2.dnn.DNN_BACKEND_CUDA, cv2.dnn.DNN_TARGET_CUDA_FP16, 'CUDA FP16'),
    (cv2.dnn.DNN_BACKEND_CUDA, cv2.dnn.DNN_TARGET_CUDA,      'CUDA FP32'),
    (cv2.dnn.DNN_BACKEND_DEFAULT, cv2.dnn.DNN_TARGET_CPU,    'CPU'),
]:
    try:
        net.setPreferableBackend(backend); net.setPreferableTarget(target)
        net.setInput(blob); net.forward()
        print(f'  {label}: OK')
    except Exception as e:
        print(f'  {label}: FAILED — {e}')
"
# Expect: CUDA FP16: OK / CUDA FP32: OK / CPU: OK
```

### Other checks

```bash
# OPENCV_DNN_CUDA in cmake cache (if build was done natively on this machine)
grep "OPENCV_DNN_CUDA" ~/CV/SETUP/workspace/opencv-*/release/CMakeCache.txt

# CUDA + cuDNN in build info
python3 -c "import cv2; print(cv2.getBuildInformation())" | grep -E "CUDA|cuDNN|GStreamer|TBB"

# TRC binary links against correct OpenCV version
ldd $(find ~/CV -name "trc" -type f 2>/dev/null | head -1) | grep opencv_dnn
```

---

*Document maintained alongside `OPENCV_CUDA_BUILD.md` and `install_opencv4.13.0_Jetpack6.2.1.sh`.*
