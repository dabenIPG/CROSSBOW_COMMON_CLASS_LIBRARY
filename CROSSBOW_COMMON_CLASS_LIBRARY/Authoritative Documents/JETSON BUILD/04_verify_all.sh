#!/bin/bash
# ==============================================================================
# 04_verify_all.sh
# CROSSBOW / TRC — Jetson Orin NX Full System Verification
#
# Platform: Seeed Studio reComputer J4012 (non-Super, J401 carrier)
# JetPack:  6.2.2 (L4T 36.5, Ubuntu 22.04, CUDA 12.6, cuDNN 9.3)
#
# Purpose:
#   Runs all checkpoint verification commands in sequence and produces a
#   pass/fail report. Run this after completing JETSON_SETUP.md to confirm
#   the system is ready for imaging.
#
#   Also used to capture a baseline snapshot for comparison against
#   future builds (output saved to ~/jetson_verified_YYYYMMDD.txt).
#
# Usage:
#   chmod +x 04_verify_all.sh
#   ./04_verify_all.sh
#
# Reference: JETSON_SETUP.md checkpoints 0–8
# ==============================================================================

set +e  # Do not exit on error — verification script must run all checks

LOG="$HOME/CV/SETUP/jetson_verified_$(date +%Y%m%d_%H%M%S).txt"
PASS=0
FAIL=0
WARN=0

# ── Colours ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'; YEL='\033[1;33m'; GRN='\033[0;32m'; CYN='\033[0;36m'
BLD='\033[1m'; NC='\033[0m'

header()  { echo -e "\n${BLD}${CYN}━━━ $* ━━━${NC}"; }
ok()      { echo -e "  ${GRN}✓${NC}  $*"; PASS=$((PASS+1)); }
fail()    { echo -e "  ${RED}✗${NC}  $*"; FAIL=$((FAIL+1)); }
warn()    { echo -e "  ${YEL}!${NC}  $*"; WARN=$((WARN+1)); }
info()    { echo -e "  ${CYN}·${NC}  $*"; }

check() {
    # check "description" "command" "expected_pattern"
    local DESC="$1" CMD="$2" EXPECT="$3"
    local OUT
    OUT=$(eval "$CMD" 2>/dev/null || true)
    if echo "$OUT" | grep -q "$EXPECT"; then
        ok "$DESC"
        local FIRST
        FIRST=$(echo "$OUT" | head -1 || true)
        info "    → $FIRST"
    else
        fail "$DESC (expected: $EXPECT)"
        local FIRST
        FIRST=$(echo "$OUT" | head -1 || true)
        info "    → got: $FIRST"
    fi
}

# Tee all output to log file
exec > >(tee "$LOG") 2>&1

echo "============================================================"
echo "  TRC Jetson Full System Verification"
echo "  $(date)"
echo "  Log: $LOG"
echo "============================================================"

# ==============================================================================
# CHECKPOINT 0 — JetPack flash
# ==============================================================================
header "Checkpoint 0 — JetPack"

check "JetPack L4T revision" \
    "cat /etc/nv_tegra_release" \
    "REVISION: 5"


check "NVMe has sufficient space" \
    "df -h / | awk 'NR==2{print \$4}'" \
    "G"

check "Username is ipg" \
    "whoami" \
    "ipg"

# ==============================================================================
# CHECKPOINT 1 — Base system
# ==============================================================================
header "Checkpoint 1 — Base system"

check "nvidia-l4t-bootloader is held" \
    "apt-mark showhold" \
    "nvidia-l4t-bootloader"

check "nvidia-l4t-kernel is held" \
    "apt-mark showhold" \
    "nvidia-l4t-kernel"

check "nvidia-l4t-core is held" \
    "apt-mark showhold" \
    "nvidia-l4t-core"

check "CUDA installed (nvcc)" \
    "/usr/local/cuda-12.6/bin/nvcc --version" \
    "release 12.6"

SWAP_KB=$(grep SwapTotal /proc/meminfo | awk '{print $2}')
if [[ $SWAP_KB -ge 4000000 ]]; then
    ok "Swap ≥ 4GB ($(awk "BEGIN {printf \"%.1f\", $SWAP_KB/1048576}")GB)"
    PASS=$((PASS+1))
else
    fail "Swap < 4GB (${SWAP_KB}KB) — run swap setup"
    FAIL=$((FAIL+1))
fi

check "jetson-stats (jtop) installed" \
    "pip3 show jetson-stats 2>/dev/null" \
    "jetson-stats"

check "GStreamer h264parse present" \
    "gst-inspect-1.0 h264parse 2>/dev/null" \
    "h264parse"

check "GStreamer nvvidconv present" \
    "gst-inspect-1.0 nvvidconv 2>/dev/null" \
    "nvvidconv"

check "GStreamer nvv4l2h264enc present" \
    "gst-inspect-1.0 nvv4l2h264enc 2>/dev/null" \
    "nvv4l2h264enc"

# GStreamer encode pipeline test
info "GStreamer encode pipeline — start Windows receive then run:"
info "  gst-launch-1.0 videotestsrc is-live=true \\"
info "    ! \"video/x-raw,width=1280,height=720,framerate=60/1\" \\"
info "    ! nvvidconv ! \"video/x-raw(memory:NVMM),format=NV12\" \\"
info "    ! nvv4l2h264enc bitrate=10000000 ! h264parse \\"
info "    ! rtph264pay config-interval=1 pt=96 \\"
info "    ! udpsink host=192.168.1.208 port=5000 sync=false"

# ==============================================================================
# CHECKPOINT 2 — Power and performance
# ==============================================================================
header "Checkpoint 2 — Power and performance"

check "nvpmodel MAXN (mode 0)" \
    "sudo nvpmodel -q 2>/dev/null" \
    "MAXN"

CPU0_MIN=$(sudo jetson_clocks --show 2>/dev/null | grep "cpu0" | grep -oP 'MinFreq=\K[0-9]+' || echo 0)
CPU0_MAX=$(sudo jetson_clocks --show 2>/dev/null | grep "cpu0" | grep -oP 'MaxFreq=\K[0-9]+' || echo 1)
if [[ "$CPU0_MIN" == "$CPU0_MAX" && "$CPU0_MIN" != "0" ]]; then
    ok "jetson_clocks active (cpu0 locked at ${CPU0_MIN} Hz)"
    PASS=$((PASS+1))
else
    warn "jetson_clocks may not be active (cpu0 MinFreq=$CPU0_MIN MaxFreq=$CPU0_MAX)"
    WARN=$((WARN+1))
fi

# ==============================================================================
# CHECKPOINT 3 — Network and timing
# ==============================================================================
header "Checkpoint 3 — Network and timing"

ETH_IFACE=$(ip link show | grep -v lo | grep -v wlan | grep -v "usb\|docker\|br-\|can\|l4t" | awk -F': ' 'NR==1{print $2}' | cut -d@ -f1)
ETH_IP=$(ip addr show "$ETH_IFACE" 2>/dev/null | grep "inet " | awk '{print $2}' | cut -d/ -f1 || echo "")
info "Ethernet interface: $ETH_IFACE"

if [[ "$ETH_IP" == "192.168.1.22" ]]; then
    ok "Static IP 192.168.1.22 configured on $ETH_IFACE"
    PASS=$((PASS+1))
else
    fail "Static IP not set (interface: $ETH_IFACE, got: ${ETH_IP:-none}) — configure via nmtui"
    FAIL=$((FAIL+1))
fi

check "NTP primary server is .33" \
    "cat /etc/systemd/timesyncd.conf" \
    "NTP=192.168.1.33"

check "NTP fallback server is .208" \
    "cat /etc/systemd/timesyncd.conf" \
    "FallbackNTP=192.168.1.208"

check "NTP service active" \
    "timedatectl status" \
    "NTP service: active"

check "USB buffer set in extlinux" \
    "grep usbfs /boot/extlinux/extlinux.conf" \
    "usbcore.usbfs_memory_mb=1000"

# ==============================================================================
# CHECKPOINT 4 — VimbaX
# ==============================================================================
header "Checkpoint 4 — VimbaX"

check "VimbaX 2026-1 installed" \
    "ls /opt/" \
    "VimbaX_2026-1"

check "libVmbCPP.so present" \
    "ls /opt/VimbaX_2026-1/api/lib/libVmbCPP.so" \
    "libVmbCPP"

check "VimbaX in ldconfig" \
    "ldconfig -v 2>/dev/null" \
    "libVmbC"

check "GENICAM_GENTL64_PATH set" \
    "echo \$GENICAM_GENTL64_PATH" \
    "VimbaX_2026-1"

check "gst-vmbsrc plugin installed" \
    "gst-inspect-1.0 2>/dev/null | grep vmb" \
    "vmbsrc"

# Camera detection — non-fatal warn if camera not connected
CAM_OUT=$(env GENICAM_GENTL64_PATH=/opt/VimbaX_2026-1/cti/ \
    /opt/VimbaX_2026-1/bin/ListCameras_VmbCPP 2>/dev/null || echo "")
if echo "$CAM_OUT" | grep -q "VimbaUSBTL.cti"; then
    CAM_NAME=$(echo "$CAM_OUT" | grep "Camera Name" | grep -v "Simulator" | head -1 | awk -F': ' '{print $2}')
    ok "Alvium camera detected: $CAM_NAME"
    PASS=$((PASS+1))
elif echo "$CAM_OUT" | grep -q "Cameras found: 0"; then
    warn "No camera detected — connect Alvium USB3 camera and retry"
    WARN=$((WARN+1))
else
    warn "ListCameras_VmbCPP failed — check VimbaX install and ldconfig"
    WARN=$((WARN+1))
fi

# ==============================================================================
# CHECKPOINT 5 — OpenCV
# ==============================================================================
header "Checkpoint 5 — OpenCV"

CV_VER=$(python3 -c "import cv2; print(cv2.__version__)" 2>/dev/null || echo "import_failed")
CV_FILE=$(python3 -c "import cv2; print(cv2.__file__)" 2>/dev/null || echo "")

if [[ "$CV_VER" == "4.13.0" ]]; then
    ok "OpenCV version 4.13.0"
    PASS=$((PASS+1))
else
    fail "OpenCV version is '$CV_VER' (expected 4.13.0)"
    FAIL=$((FAIL+1))
fi

if echo "$CV_FILE" | grep -q "dist-packages"; then
    ok "OpenCV from dist-packages (correct path)"
    PASS=$((PASS+1))
else
    fail "OpenCV path wrong: $CV_FILE (must be dist-packages, not site-packages)"
    FAIL=$((FAIL+1))
fi

CUDA_TARGETS=$(python3 -c "
import cv2
t = cv2.dnn.getAvailableTargets(cv2.dnn.DNN_BACKEND_CUDA)
print(t)
" 2>/dev/null || echo "[]")

if echo "$CUDA_TARGETS" | grep -q "6"; then
    ok "CUDA DNN targets available: $CUDA_TARGETS"
    PASS=$((PASS+1))
else
    fail "CUDA DNN not compiled in (targets: $CUDA_TARGETS) — rebuild OpenCV"
    FAIL=$((FAIL+1))
fi

check "CUDA in OpenCV build info" \
    "python3 -c \"import cv2; print(cv2.getBuildInformation())\"" \
    "NVIDIA CUDA"

check "cuDNN in OpenCV build info" \
    "python3 -c \"import cv2; print(cv2.getBuildInformation())\"" \
    "cuDNN"

check "GStreamer in OpenCV build info" \
    "python3 -c \"import cv2; print(cv2.getBuildInformation())\"" \
    "GStreamer"

check "TBB in OpenCV build info" \
    "python3 -c \"import cv2; print(cv2.getBuildInformation())\"" \
    "TBB"

check "pkg-config finds opencv4" \
    "pkg-config --modversion opencv4" \
    "4.13"

# ==============================================================================
# CHECKPOINT 6 — TRC build
# ==============================================================================
header "Checkpoint 6 — TRC build"

TRC_BIN="$HOME/CV/TRC/trc"

if [[ -f "$TRC_BIN" ]]; then
    ok "TRC binary exists: $TRC_BIN"
    PASS=$((PASS+1))
else
    fail "TRC binary not found at $TRC_BIN"
    FAIL=$((FAIL+1))
fi

check "TRC links against OpenCV 4.13" \
    "ldd $TRC_BIN 2>/dev/null" \
    "libopencv_dnn.so.413"

check "TRC links against VimbaX 2026-1" \
    "ldd $TRC_BIN 2>/dev/null" \
    "VimbaX_2026-1"

check "Makefile references VimbaX 2026-1" \
    "grep VIMBAX_DIR $HOME/CV/TRC/Makefile" \
    "VimbaX_2026-1"

check "TRC version string" \
    "$TRC_BIN --version 2>/dev/null" \
    "TRC 3"

# COCO inference probe — requires model files
MODEL_DIR="$HOME/CV/TRC/model_data"
if [[ -f "$MODEL_DIR/frozen_inference_graph.pb" && \
      -f "$MODEL_DIR/ssd_mobilenet_v3_large_coco_2020_01_14.pbtxt" ]]; then

    info "Running COCO inference probe..."
    PROBE_OUT=$(cd "$HOME/CV/TRC" && python3 << 'EOF' 2>/dev/null
import cv2, numpy as np
net = cv2.dnn.readNet("model_data/frozen_inference_graph.pb",
                      "model_data/ssd_mobilenet_v3_large_coco_2020_01_14.pbtxt")
blob = cv2.dnn.blobFromImage(np.zeros((320,320,3),dtype=np.uint8),
                             1/127.5,(320,320),(127.5,127.5,127.5),swapRB=True)
for backend, target, label in [
    (cv2.dnn.DNN_BACKEND_CUDA, cv2.dnn.DNN_TARGET_CUDA_FP16, "CUDA_FP16"),
    (cv2.dnn.DNN_BACKEND_CUDA, cv2.dnn.DNN_TARGET_CUDA,      "CUDA_FP32"),
    (cv2.dnn.DNN_BACKEND_DEFAULT, cv2.dnn.DNN_TARGET_CPU,    "CPU"),
]:
    try:
        net.setPreferableBackend(backend)
        net.setPreferableTarget(target)
        net.setInput(blob)
        net.forward()
        print(f"{label}:OK")
    except Exception as e:
        print(f"{label}:FAILED")
EOF
)
    while IFS= read -r RESULT; do
        LABEL=$(echo "$RESULT" | cut -d: -f1 | tr '_' ' ')
        STATUS=$(echo "$RESULT" | cut -d: -f2)
        if [[ "$STATUS" == "OK" ]]; then
            ok "Inference probe — $LABEL: OK"
            PASS=$((PASS+1))
        else
            fail "Inference probe — $LABEL: FAILED"
            FAIL=$((FAIL+1))
        fi
    done <<< "$PROBE_OUT"
else
    warn "model_data/ not found at $MODEL_DIR — skipping inference probe"
    warn "Copy model files to run full inference verification"
    WARN=$((WARN+1))
fi

# ==============================================================================
# CHECKPOINT 7 — Autostart
# ==============================================================================
header "Checkpoint 7 — Autostart"

if systemctl is-active --quiet trc.service 2>/dev/null; then
    ok "trc.service is active (systemd)"
    PASS=$((PASS+1))
elif pgrep -x trc > /dev/null 2>&1; then
    ok "trc process running (crontab)"
    PASS=$((PASS+1))
else
    warn "TRC not currently running — verify autostart config"
    WARN=$((WARN+1))
fi

# ==============================================================================
# Full snapshot
# ==============================================================================
header "System snapshot"

info "JetPack:    $(cat /etc/nv_tegra_release | grep REVISION | tr -d '#')"
info "OpenCV:     $CV_VER ($CV_FILE)"
info "VimbaX:     $(ls /opt/ | grep VimbaX | head -1)"
info "NTP server: $(grep '^NTP=' /etc/systemd/timesyncd.conf 2>/dev/null || echo 'not configured')"
info "IP:         $(ip addr show "$ETH_IFACE" 2>/dev/null | grep 'inet ' | awk '{print $2}' || echo 'not set')"
info "nvpmodel:   $(sudo nvpmodel -q 2>/dev/null | grep 'Power Mode')"
info "Swap:       $(free -h | grep Swap | awk '{print $2}')"
info "USB buffer: $(grep -o 'usbfs_memory_mb=[0-9]*' /boot/extlinux/extlinux.conf 2>/dev/null || echo 'not set')"

# ==============================================================================
# Summary
# ==============================================================================
echo ""
echo "============================================================"
echo -e "  ${BLD}Verification Summary${NC}"
echo "============================================================"
echo -e "  ${GRN}PASS:${NC}  $PASS"
if [[ $WARN -gt 0 ]]; then
    echo -e "  ${YEL}WARN:${NC}  $WARN"
fi
if [[ $FAIL -gt 0 ]]; then
    echo -e "  ${RED}FAIL:${NC}  $FAIL"
fi
echo ""
echo "  Log saved: $LOG"
echo ""

if [[ $FAIL -eq 0 && $WARN -eq 0 ]]; then
    echo -e "  ${GRN}${BLD}ALL CHECKS PASSED — system ready for imaging${NC}"
elif [[ $FAIL -eq 0 ]]; then
    echo -e "  ${YEL}${BLD}PASSED WITH WARNINGS — review warnings before imaging${NC}"
else
    echo -e "  ${RED}${BLD}FAILED — resolve failures before imaging${NC}"
fi
echo ""
