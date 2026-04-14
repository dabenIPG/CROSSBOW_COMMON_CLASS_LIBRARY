#!/bin/bash
# cleanup_pre_image.sh — Strip build artifacts and installer debris before imaging
# Run once after full build and desktop removal, before 04_verify_all.sh pre-image run.
# CROSSBOW TRC — Jetson Orin NX

echo "=== TRC Pre-Image Cleanup ==="

# /opt/ — VimbaX installer tarball
sudo rm -f /opt/VimbaX_Setup-*-Linux_ARM64.tar.gz

# ~/CV/SETUP/ — build scripts, logs, dev artifacts
rm -f  ~/CV/SETUP/gst-vmbsrc-*.zip
rm -f  ~/CV/SETUP/install_opencv*.sh
rm -f  ~/CV/SETUP/opencv_build_*.log
# test1.py kept — diagnostic tool
rm -rf ~/CV/SETUP/opencv_build_workspace/
rm -rf ~/CV/SETUP/deploy/
rm -rf ~/CV/SETUP/gst-vmbsrc/

# ~/CV/TRC/ — build artifacts, source, docs (binary-only production image)
rm -f ~/CV/TRC/*.o
rm -f ~/CV/TRC/trc_build_*.log
rm -f ~/CV/TRC/*.cpp
rm -f ~/CV/TRC/*.c
rm -f ~/CV/TRC/*.h
rm -f ~/CV/TRC/*.hpp
rm -f ~/CV/TRC/README.md
rm -f ~/CV/TRC/TRC_MIGRATION.md
rm -f ~/CV/TRC/Makefile
rm -f ~/CV/TRC/version.h

echo "=== Cleanup complete. Verify survivors: ==="
echo "--- ~/CV/SETUP/ ---"
ls -lh ~/CV/SETUP/
echo "--- ~/CV/TRC/ ---"
ls -lh ~/CV/TRC/
echo "--- /opt/ VimbaX ---"
ls /opt/ | grep -i vimba
