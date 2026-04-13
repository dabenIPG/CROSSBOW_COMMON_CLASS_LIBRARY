#!/bin/bash
# ==============================================================================
# trc_start_bench.sh — TRC Bench/Test Launch Script
# CROSSBOW / TRC — Jetson Orin NX
#
# Purpose: Test launch with MWIR test source and unicast to Windows.
#          Use this for crontab/autostart testing before deploying production.
#          Safe to run without MWIR camera connected.
#
# Differences from production:
#   - No --mwir-live flag  → MWIR uses videotestsrc
#   - Unicast to 192.168.1.208 instead of multicast 239.127.1.21
#   - Log to trc_bench.log (separate from production log)
# ==============================================================================

. /home/ipg/.bashrc

export HOME=/home/ipg
export GENICAM_GENTL64_PATH=/opt/VimbaX_2026-1/cti/
export LD_LIBRARY_PATH=/usr/local/lib:/opt/VimbaX_2026-1/api/lib/:$LD_LIBRARY_PATH
export PYTHONPATH=/usr/local/lib/python3.10/dist-packages/:$PYTHONPATH
export PATH=/usr/local/cuda-12.6/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/games:/usr/local/games:/snap/bin

cd /home/ipg/CV/TRC/
echo "[TRC BENCH] Starting bench launch: $(date)" >> trc_bench.log
./trc --dest-host 192.168.1.208 --view PIP &>> trc_bench.log
