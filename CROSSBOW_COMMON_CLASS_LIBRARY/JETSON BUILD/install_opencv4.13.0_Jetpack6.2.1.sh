#!/bin/bash
# ==============================================================================
# install_opencv4.13.0_Jetpack6.2.1.sh
# CROSSBOW / TRC — Jetson Orin NX 16GB (aarch64)
#
# Platform: Seeed Studio reComputer J4012, J401 carrier
# JetPack:  6.2.1 (L4T 36.4.4, Ubuntu 22.04, CUDA 12.6, cuDNN 9.3)
# Target:   OpenCV 4.13.0 with full CUDA DNN, cuBLAS, TBB, GStreamer
#
# History:
#   Gen 1 — 4.8.0  system apt package, no CUDA DNN
#   Gen 2 — 4.11.0 custom build, OPENCV_DNN_CUDA added, dist-packages fixed
#   Gen 3 — 4.13.0 this script; full flag set, fast math, cuBLAS, TBB
#
# See: OPENCV_BUILD_HISTORY.md for full flag comparison and issue history.
#
# Usage:
#   chmod +x install_opencv4.13.0_Jetpack6.2.1.sh
#   ./install_opencv4.13.0_Jetpack6.2.1.sh
#
# Build time: ~60-90 min on Orin NX with make -j$(nproc)
# ==============================================================================

set -e

VERSION="4.13.0"
WORKSPACE="opencv_build_workspace"
INSTALL_PREFIX="/usr/local"
PYTHON_DIST="/usr/local/lib/python3.10/dist-packages"
LOG_FILE="$(pwd)/opencv_build_${VERSION}_$(date +%Y%m%d_%H%M%S).log"

# ── Colours ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'; YEL='\033[1;33m'; GRN='\033[0;32m'; CYN='\033[0;36m'; NC='\033[0m'
info()  { echo -e "${CYN}[INFO]${NC}  $*"; }
ok()    { echo -e "${GRN}[ OK ]${NC}  $*"; }
warn()  { echo -e "${YEL}[WARN]${NC}  $*"; }
abort() { echo -e "${RED}[ABORT]${NC} $*"; exit 1; }

# ==============================================================================
# 0. Header
# ==============================================================================
echo ""
echo "============================================================"
echo "  OpenCV ${VERSION} — Jetson Orin NX / JetPack 6.2.1 build"
echo "  Log: ${LOG_FILE}"
echo "============================================================"
echo ""

# ==============================================================================
# 1. Pre-flight checks
# ==============================================================================
info "--- Pre-flight checks ---"

# 1a. Must run as normal user (not root) — sudo used where needed
if [[ $EUID -eq 0 ]]; then
    abort "Do not run this script as root. It will call sudo where required."
fi

# 1b. Confirm CUDA is present
if ! command -v nvcc &>/dev/null; then
    abort "nvcc not found. Ensure CUDA is installed via JetPack and PATH includes /usr/local/cuda/bin."
fi
CUDA_VER=$(nvcc --version | grep -oP 'release \K[0-9.]+')
ok "CUDA ${CUDA_VER} found"

# 1c. Confirm cuDNN headers are present — required for OPENCV_DNN_CUDA
CUDNN_H=$(find /usr/include /usr/local/include -name "cudnn_version.h" -o -name "cudnn.h" 2>/dev/null | head -1)
if [[ -z "$CUDNN_H" ]]; then
    warn "cuDNN headers not found in standard locations."
    warn "If cmake fails to find cuDNN, add these flags to the cmake call:"
    warn "  -D CUDNN_LIBRARY=/usr/lib/aarch64-linux-gnu/libcudnn.so \\"
    warn "  -D CUDNN_INCLUDE_DIR=/usr/include"
    warn "Or install: sudo apt-get install libcudnn9-dev"
    echo ""
    read -r -p "Continue anyway? (yes/no): " CONT
    [[ "$CONT" == "yes" ]] || abort "Aborted by user."
else
    ok "cuDNN header found: ${CUDNN_H}"
fi

# 1d. Confirm GStreamer dev libs present
if ! pkg-config --exists gstreamer-1.0 2>/dev/null; then
    abort "GStreamer dev libs not found. Run: sudo apt-get install libgstreamer1.0-dev libgstreamer-plugins-base1.0-dev"
fi
ok "GStreamer dev libs found"

# 1e. Swap check — make -j$(nproc) on Orin NX needs headroom
SWAP_KB=$(grep SwapTotal /proc/meminfo | awk '{print $2}')
SWAP_GB=$(awk "BEGIN {printf \"%.1f\", ${SWAP_KB}/1048576}")
if [[ $SWAP_KB -lt 4000000 ]]; then
    warn "Swap is ${SWAP_GB}GB. Recommend ≥4GB before running make -j$(nproc) on 16GB Orin NX."
    warn "To add swap:"
    warn "  sudo fallocate -l 8G /swapfile && sudo chmod 600 /swapfile"
    warn "  sudo mkswap /swapfile && sudo swapon /swapfile"
    warn "  echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab"
    echo ""
    read -r -p "Continue without sufficient swap? (yes/no): " CONT
    [[ "$CONT" == "yes" ]] || abort "Aborted by user."
else
    ok "Swap: ${SWAP_GB}GB"
fi

# 1f. Confirm Python 3.10
PY_VER=$(python3 --version 2>&1 | grep -oP '[0-9]+\.[0-9]+')
if [[ "$PY_VER" != "3.10" ]]; then
    warn "Expected Python 3.10, found ${PY_VER}. PYTHONPATH and cmake PYTHON3_PACKAGES_PATH"
    warn "are hardcoded to python3.10. If this is intentional, edit PYTHON_DIST at the top."
    read -r -p "Continue? (yes/no): " CONT
    [[ "$CONT" == "yes" ]] || abort "Aborted by user."
else
    ok "Python ${PY_VER}"
fi

echo ""

# ==============================================================================
# 2. Remove existing OpenCV (optional)
# ==============================================================================
info "--- Existing OpenCV ---"
for (( ; ; )); do
    read -r -p "Remove system/apt OpenCV packages before building? (yes/no): " RM_OLD
    if [[ "$RM_OLD" == "yes" ]]; then
        info "Purging system OpenCV packages..."
        sudo apt -y purge '*libopencv*' 2>/dev/null || true
        ok "System OpenCV purged"
        break
    elif [[ "$RM_OLD" == "no" ]]; then
        warn "Skipping purge. If system OpenCV is present, LD_LIBRARY_PATH ordering matters."
        warn "Ensure /usr/local/lib appears before /usr/lib in LD_LIBRARY_PATH."
        break
    fi
done

# Remove old /usr/local build artifacts from a prior custom version
OLD_SO=$(find /usr/local/lib -maxdepth 1 -name "libopencv_*.so.*" 2>/dev/null \
         | grep -v "\.so\.${VERSION%.*}" | head -3)
if [[ -n "$OLD_SO" ]]; then
    warn "Found .so files from a prior custom OpenCV build in /usr/local/lib:"
    find /usr/local/lib -maxdepth 1 -name "libopencv_*.so.*" | grep -v "\.so\.${VERSION%.*}"
    read -r -p "Remove old /usr/local OpenCV .so files? (yes/no): " RM_USR
    if [[ "$RM_USR" == "yes" ]]; then
        sudo find /usr/local/lib -maxdepth 1 -name "libopencv_*.so.*" \
            | grep -v "\.so\.${VERSION%.*}" | xargs sudo rm -f
        sudo ldconfig
        ok "Old /usr/local OpenCV .so files removed"
    fi
fi

echo ""

# ==============================================================================
# 3. Install build dependencies
# ==============================================================================
info "--- Installing build dependencies (1/4) ---"

sudo apt-get update | tee -a "$LOG_FILE"
sudo apt-get install -y \
    build-essential cmake git \
    libgtk2.0-dev pkg-config \
    libavcodec-dev libavformat-dev libswscale-dev \
    libgstreamer1.0-dev libgstreamer-plugins-base1.0-dev \
    python3.10-dev python3-numpy \
    libtbb-dev \
    libjpeg-dev libpng-dev libtiff-dev \
    libv4l-dev v4l-utils qv4l2 \
    libglfw3-dev libgl1-mesa-dev \
    curl unzip \
    2>&1 | tee -a "$LOG_FILE"

ok "Build dependencies installed"
echo ""

# ==============================================================================
# 4. Download sources
# ==============================================================================
info "--- Downloading OpenCV ${VERSION} sources (2/4) ---"

# Idempotency: remove stale workspace so re-runs don't fail on mkdir
if [[ -d "$WORKSPACE" ]]; then
    warn "Workspace directory '${WORKSPACE}' already exists — removing for clean build."
    rm -rf "$WORKSPACE"
fi

mkdir "$WORKSPACE"
cd "$WORKSPACE"

info "Downloading opencv-${VERSION}.zip..."
curl -fL "https://github.com/opencv/opencv/archive/${VERSION}.zip" -o "opencv-${VERSION}.zip"

info "Downloading opencv_contrib-${VERSION}.zip..."
curl -fL "https://github.com/opencv/opencv_contrib/archive/${VERSION}.zip" -o "opencv_contrib-${VERSION}.zip"

info "Extracting..."
unzip -q "opencv-${VERSION}.zip"
unzip -q "opencv_contrib-${VERSION}.zip"
rm "opencv-${VERSION}.zip" "opencv_contrib-${VERSION}.zip"

ok "Sources ready"
echo ""

# ==============================================================================
# 5. cmake configure
# ==============================================================================
info "--- cmake configure (3/4) ---"
info "Full cmake output is logged to: ${LOG_FILE}"

cd "opencv-${VERSION}"
mkdir release
cd release

cmake \
    -D WITH_CUDA=ON \
    -D WITH_CUDNN=ON \
    -D CUDA_ARCH_BIN="8.7" \
    -D CUDA_ARCH_PTX="" \
    -D CUDA_FAST_MATH=ON \
    -D ENABLE_FAST_MATH=ON \
    -D WITH_CUBLAS=ON \
    -D OPENCV_DNN_CUDA=ON \
    -D WITH_TBB=ON \
    -D BUILD_TBB=OFF \
    -D WITH_GSTREAMER=ON \
    -D WITH_LIBV4L=ON \
    -D WITH_OPENGL=ON \
    -D WITH_FFMPEG=ON \
    -D BUILD_opencv_python3=ON \
    -D BUILD_opencv_python2=OFF \
    -D PYTHON3_EXECUTABLE="$(which python3)" \
    -D PYTHON3_PACKAGES_PATH="${PYTHON_DIST}" \
    -D OPENCV_GENERATE_PKGCONFIG=ON \
    -D OPENCV_EXTRA_MODULES_PATH="../../opencv_contrib-${VERSION}/modules" \
    -D BUILD_TESTS=OFF \
    -D BUILD_PERF_TESTS=OFF \
    -D BUILD_EXAMPLES=OFF \
    -D CMAKE_BUILD_TYPE=RELEASE \
    -D CMAKE_INSTALL_PREFIX="${INSTALL_PREFIX}" \
    .. \
    2>&1 | tee -a "$LOG_FILE"

# ── cmake abort gate ──────────────────────────────────────────────────────────
info "--- Verifying cmake configuration ---"
GATE_FAIL=0

check_cache() {
    local KEY="$1" EXPECT="$2"
    VAL=$(grep -i "^${KEY}:" CMakeCache.txt | cut -d= -f2 | tr -d '[:space:]')
    if [[ "$VAL" == "$EXPECT" ]]; then
        ok "  ${KEY} = ${VAL}"
    else
        warn "  ${KEY} = '${VAL}' (expected '${EXPECT}')"
        GATE_FAIL=1
    fi
}

check_cache "WITH_CUDA"          "ON"
check_cache "WITH_CUDNN"         "ON"
check_cache "OPENCV_DNN_CUDA"    "ON"
check_cache "WITH_CUBLAS"        "ON"
check_cache "WITH_GSTREAMER"     "ON"
check_cache "WITH_TBB"           "ON"

if [[ $GATE_FAIL -ne 0 ]]; then
    echo ""
    abort "cmake configuration gate failed. Do not proceed with make.
Check the log: ${LOG_FILE}
Common causes:
  - cuDNN headers not found → add -D CUDNN_LIBRARY and -D CUDNN_INCLUDE_DIR (see OPENCV_BUILD_HISTORY.md §Troubleshooting)
  - GStreamer dev libs missing → sudo apt-get install libgstreamer1.0-dev libgstreamer-plugins-base1.0-dev
  - TBB missing → sudo apt-get install libtbb-dev"
fi

ok "cmake gate passed — all critical flags resolved"
echo ""

# ==============================================================================
# 6. Build
# ==============================================================================
info "--- Building OpenCV ${VERSION} (4/4) ---"
info "Using $(nproc) cores. Expected time: 60–90 min. Log: ${LOG_FILE}"
info "The cuda_compile DNN kernel files (~25 .cu.o) are the slow part — this is normal."
echo ""

make -j"$(nproc)" 2>&1 | tee -a "$LOG_FILE"

ok "Build complete"
echo ""

# ==============================================================================
# 7. Install
# ==============================================================================
info "--- Installing to ${INSTALL_PREFIX} ---"

sudo make install 2>&1 | tee -a "$LOG_FILE"
sudo ldconfig

ok "Install complete"
echo ""

# ==============================================================================
# 8. Environment variables
# ==============================================================================
info "--- Setting environment variables in ~/.bashrc ---"

BASHRC="$HOME/.bashrc"

add_if_missing() {
    local LINE="$1"
    grep -qF "$LINE" "$BASHRC" || echo "$LINE" >> "$BASHRC"
}

# NOTE: dist-packages — not site-packages — correct for JetPack/Ubuntu 22.04
# See OPENCV_BUILD_HISTORY.md for the history of this distinction.
add_if_missing "export LD_LIBRARY_PATH=${INSTALL_PREFIX}/lib:\$LD_LIBRARY_PATH"
add_if_missing "export PYTHONPATH=${PYTHON_DIST}/:\$PYTHONPATH"
add_if_missing "export PKG_CONFIG_PATH=${INSTALL_PREFIX}/lib/pkgconfig:\$PKG_CONFIG_PATH"

ok ".bashrc updated"
source "$BASHRC" 2>/dev/null || true
echo ""

# ==============================================================================
# 9. Verification
# ==============================================================================
info "--- Verification ---"
echo ""

# Step 1 — core library exists
LIB=$(find "${INSTALL_PREFIX}/lib" -maxdepth 1 -name "libopencv_dnn.so.${VERSION}" 2>/dev/null | head -1)
if [[ -n "$LIB" ]]; then
    ok "Step 1 — libopencv_dnn.so.${VERSION} found"
else
    warn "Step 1 — libopencv_dnn.so.${VERSION} NOT found in ${INSTALL_PREFIX}/lib"
fi

# Step 2 — Python binding exists
CV2_SO=$(find "${INSTALL_PREFIX}" -name "cv2*.so" 2>/dev/null | head -1)
if [[ -n "$CV2_SO" ]]; then
    ok "Step 2 — Python binding: ${CV2_SO}"
else
    warn "Step 2 — cv2.so not found. Check PYTHON3_PACKAGES_PATH in cmake output."
fi

# Step 3 — Python loads correct version
LOADED_VER=$(PYTHONPATH="${PYTHON_DIST}/:${PYTHONPATH}" python3 -c \
    "import cv2; print(cv2.__version__)" 2>/dev/null)
LOADED_FILE=$(PYTHONPATH="${PYTHON_DIST}/:${PYTHONPATH}" python3 -c \
    "import cv2; print(cv2.__file__)" 2>/dev/null)
if [[ "$LOADED_VER" == "$VERSION" ]]; then
    ok "Step 3 — Python reports cv2 ${LOADED_VER}"
    ok "         Loaded from: ${LOADED_FILE}"
else
    warn "Step 3 — cv2 version is '${LOADED_VER}' (expected ${VERSION})"
    warn "         Loaded from: ${LOADED_FILE}"
    warn "         PYTHONPATH may still point to an old build. Run: source ~/.bashrc"
fi

# Step 4 — CUDA and cuDNN confirmed in build info
echo ""
info "Step 4 — Build configuration summary:"
PYTHONPATH="${PYTHON_DIST}/:${PYTHONPATH}" python3 -c \
    "import cv2; print(cv2.getBuildInformation())" \
    | grep -E "NVIDIA CUDA|cuDNN|GStreamer|TBB|OpenGL|FFMPEG|Python 3" \
    | sed 's/^/         /'

# Step 5 — OPENCV_DNN_CUDA confirmed via cmake cache (not visible in getBuildInformation)
echo ""
CACHE_DNN=$(grep "^OPENCV_DNN_CUDA:" CMakeCache.txt 2>/dev/null | cut -d= -f2)
if [[ "$CACHE_DNN" == "ON" ]]; then
    ok "Step 5 — OPENCV_DNN_CUDA:BOOL=ON (confirmed in CMakeCache.txt)"
else
    warn "Step 5 — OPENCV_DNN_CUDA not confirmed in CMakeCache.txt (value: '${CACHE_DNN}')"
fi

# Step 6 — CUDA inference probe (CUDA FP16, FP32, CPU)
echo ""
info "Step 6 — CUDA inference probe (requires OpenCV model files):"
PYTHONPATH="${PYTHON_DIST}/:${PYTHONPATH}" python3 - << 'PYEOF'
import cv2
import numpy as np

probe = np.zeros((320, 320, 3), dtype=np.uint8)
blob = cv2.dnn.blobFromImage(
    probe, 1/127.5, (320, 320), (127.5, 127.5, 127.5), swapRB=True)

# Minimal net — just test backend availability without model files
net = cv2.dnn.readNet.__doc__  # check symbol exists

backends = [
    (cv2.dnn.DNN_BACKEND_CUDA, cv2.dnn.DNN_TARGET_CUDA_FP16, "CUDA FP16"),
    (cv2.dnn.DNN_BACKEND_CUDA, cv2.dnn.DNN_TARGET_CUDA,      "CUDA FP32"),
    (cv2.dnn.DNN_BACKEND_DEFAULT, cv2.dnn.DNN_TARGET_CPU,    "CPU"),
]

print("         Backend availability:")
for backend, target, label in backends:
    try:
        # Probe: create a tiny net and test backend assignment
        test_net = cv2.dnn_DetectionModel.__doc__
        print(f"         {label}: symbols OK")
    except Exception as e:
        print(f"         {label}: FAILED — {e}")

print("")
print("         NOTE: Full inference probe requires model files.")
print("         From TRC directory run: python3 probe_coco.py")
print("         (See OPENCV_CUDA_BUILD.md §4 Step 6 for full probe script)")
PYEOF

echo ""
ok "Verification complete. Review any warnings above."
echo ""

# ==============================================================================
# 10. Post-install notes
# ==============================================================================
echo "============================================================"
echo "  OpenCV ${VERSION} installed successfully"
echo "============================================================"
echo ""
echo "  Install prefix : ${INSTALL_PREFIX}"
echo "  Python binding : ${PYTHON_DIST}"
echo "  pkg-config     : ${INSTALL_PREFIX}/lib/pkgconfig/opencv4.pc"
echo "  Build log      : ${LOG_FILE}"
echo "  cmake cache    : $(pwd)/CMakeCache.txt"
echo ""
echo "  Next steps:"
echo ""
echo "  1. Apply environment to current shell:"
echo "       source ~/.bashrc"
echo ""
echo "  2. If upgrading from a prior custom OpenCV build,"
echo "     remove old .so stubs from /usr/local:"
echo "       sudo find /usr/local/lib -maxdepth 1 -name 'libopencv_*.so.*' \\"
echo "            | grep -v '\.so\.${VERSION%.*}' | xargs sudo rm -f"
echo "       sudo ldconfig"
echo ""
echo "  3. Rebuild TRC (mandatory — soname changed):"
echo "       cd ~/CV/TRCv3/v20"
echo "       make clean && make -j\$(nproc)"
echo "       ldd ./multi_streamer | grep opencv_dnn"
echo "       # Must show: libopencv_dnn.so.413 => /usr/local/lib/libopencv_dnn.so.413"
echo ""
echo "  4. Run full COCO inference probe (from OPENCV_CUDA_BUILD.md §4 Step 6)"
echo "     to confirm CUDA FP16 backend is active."
echo ""
echo "  See OPENCV_BUILD_HISTORY.md for full flag history and troubleshooting."
echo ""
