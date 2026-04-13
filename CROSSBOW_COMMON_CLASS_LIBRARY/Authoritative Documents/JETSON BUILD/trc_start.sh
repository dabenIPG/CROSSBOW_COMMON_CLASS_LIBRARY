#!/bin/bash
# ==============================================================================
# trc_start.sh — TRC Production Launch Script
# CROSSBOW / TRC — Jetson Orin NX
#
# Platform: Seeed Studio reComputer J4012 (non-Super, J401 carrier)
# JetPack:  6.2.2, VimbaX 2026-1, OpenCV 4.13.0
#
# Usage: ./trc_start.sh
# Autostart: called from crontab @reboot or systemd unit
# ==============================================================================

. /home/ipg/.bashrc

export HOME=/home/ipg
export GENICAM_GENTL64_PATH=/opt/VimbaX_2026-1/cti/
export LD_LIBRARY_PATH=/usr/local/lib:/opt/VimbaX_2026-1/api/lib/:$LD_LIBRARY_PATH
export PYTHONPATH=/usr/local/lib/python3.10/dist-packages/:$PYTHONPATH
export PATH=/usr/local/cuda-12.6/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/games:/usr/local/games:/snap/bin

cd /home/ipg/CV/TRC/
echo "[TRC] Starting production launch: $(date)" >> trc.log
./trc --dest-host 239.127.1.21 --mwir-live --view PIP &>> trc.log
