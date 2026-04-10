# JETSON_SETUP.md — TRC Jetson Orin NX Setup Procedure
**Document:** JETSON_SETUP.md (DOC-2)  
**Version:** 2.0.0  
**Date:** 2026-04-10  
**Confirmed on:** Unit 1 (2026-04-09), Unit 2 (2026-04-09), Unit 3 (2026-04-10)  
**Platform:** Seeed Studio reComputer J4012 **(non-Super, J401 carrier)** — see hardware note below  
**JetPack:** 6.2.2 (L4T 36.5, Ubuntu 22.04, CUDA 12.6, cuDNN 9.3)  
**Reference:** ARCHITECTURE.md §2.5, OPENCV_BUILD_HISTORY.md, TOMORROW_SESSION_PLAN.md

> ⚠️ **Hardware variant — read before proceeding**
>
> Two versions of the J4012 exist in the fleet:
>
> | Variant | Carrier | Super Mode | This doc applies |
> |---------|---------|-----------|-----------------|
> | reComputer J4012 | J401 (original) | Not supported — thermal/power limit | ✅ Yes |
> | reComputer Super J4012 | New carrier, higher TDP | Supported (up to 157 TOPS) | ❌ No — future procedure |
>
> These are mechanically different boards. The baseline snapshot, all scripts, and all
> images produced by this procedure are specific to the **non-Super J4012 with J401 carrier**.
> Do not apply this procedure or restore these images to the Super unit.
>
> **Identify your unit before starting:**
> ```bash
> cat /proc/device-tree/model
> # Non-Super (this doc): NVIDIA Jetson Orin NX Engineering Reference Developer Kit
> # Super: will show a different Seeed-specific board identifier
> ```
>
> **nvpmodel on non-Super:** MAXN (mode 0) at ~25W is the ceiling.
> Super Mode power table entries do not exist on this carrier — do not attempt to enable them.

---

## Overview

This document covers three deployment paths for TRC Jetson units. Read this
section first to determine which path applies to your situation.

---

### Deployment Path A — Fresh build from scratch (this document)

**Use when:** New unit, blank NVMe, or starting over completely.

Full procedure: Phase 0 (flash) → Phase 1 → ... → Phase 9 (image).
The resulting image can then be used for Path C replication.

**Gate:** None — start at Phase 0.

---

### Deployment Path B — Restore from image

**Use when:** Deploying a second or subsequent non-Super J4012 unit from a
validated image produced by Path A.

```bash
# Boot target unit from USB recovery drive, then:
sudo dd if=jetson_trc_v3.x.x_YYYYMMDD.img of=/dev/nvme0n1 \
    bs=4M status=progress conv=fsync
```

After restore, update unit-specific settings only:
- Static IP if different from reference unit
- Hostname if required

**Gate:** Image must be from a non-Super J4012 (J401 carrier). Do not restore
a non-Super image to a Super J4012 or vice versa — mechanically incompatible.

---

### Deployment Path C — In-place upgrade of existing unit

**Use when:** Unit is already running and operational. Goal is to upgrade
JetPack, OpenCV, VimbaX, or TRC without a full reflash.

**First gate — is the existing JetPack sufficient?**

```bash
cat /etc/nv_tegra_release | grep REVISION
```

| Result | Action |
|--------|--------|
| `REVISION: 5.0` (L4T 36.5 / JetPack 6.2.2) | JetPack current — skip Phase 0, start at Phase 1 |
| `REVISION: 4.4` (L4T 36.4.4 / JetPack 6.2.1) | JetPack acceptable — skip Phase 0, start at Phase 1 |
| `REVISION: 4.3` or earlier | JetPack too old — full reflash required (Path A) |

**Second gate — check L4T packages are held:**

```bash
apt-mark showhold | grep nvidia-l4t
```

If L4T packages are NOT held, do this before any apt operations:
```bash
sudo apt-mark hold nvidia-l4t-bootloader nvidia-l4t-kernel nvidia-l4t-kernel-dtbs \
    nvidia-l4t-kernel-headers nvidia-l4t-core nvidia-l4t-init
```

**Then proceed from the relevant phase:**
- Upgrade OpenCV only → Phase 5
- Upgrade VimbaX only → Phase 4
- Upgrade TRC binary only → Phase 6
- Full software upgrade → Phase 1 onwards

---

### Path D — Super J4012 (future)

The reComputer Super J4012 uses a different carrier board with higher TDP and
Super Mode support. It requires a separate setup procedure. Do not use this
document for Super J4012 units. See future JETSON_SUPER_SETUP.md.

---

**The image is the automation.** Once Path A produces a validated image, all
subsequent non-Super J4012 units are deployed via Path B — not by re-running
this procedure.

---

## Prerequisites — Gather Before Starting (Path A only)

| Item | Detail |
|------|--------|
| Host PC with SDK Manager | Ubuntu recommended. Physical machine — not VM. SDK Manager 2.4.0+. |
| Micro-USB cable | For J401 recovery mode during flash |
| Display + keyboard | For Jetson first boot EULA acceptance |
| Ethernet cable | Host PC ↔ Jetson direct for internet sharing during install |
| Windows PC (optional) | For internet sharing via ICS to Jetson — see Phase 1.0.1 |
| VimbaX 2026-1 ARM64 tarball | Download manually from alliedvision.com. Filename: `VimbaX_Setup-2026-1-Linux_ARM64.tar.gz` |
| USB drive (≥32GB) | For imaging in Phase 9 |

**Network parameters:**

| Parameter | Value |
|-----------|-------|
| Jetson static IP | 192.168.1.22 |
| Subnet | 255.255.255.0 |
| Gateway (deployment) | 192.168.1.1 |
| Gateway (internet sharing phase) | 192.168.1.208 (Windows NIC) — temporary |
| NTP primary | 192.168.1.33 |
| NTP fallback | 192.168.1.208 |
| PTP grandmaster | 192.168.1.30 |
| Ethernet interface | `enP8p1s0` (confirmed on J4012 non-Super / JetPack 6.2.2) |

**User account:** Consistent username across all units — reference unit uses `ipg`.
Set at first-boot — cannot be easily changed later.

---

## Phase 0 — SDK Manager Flash

Flash JetPack 6.2.2 on the host Ubuntu PC using NVIDIA SDK Manager.

### 0.1 SDK Manager version

Current recommended version: **2.4.0** (released December 2025, updated March 2026).
Check installed version:
```bash
sdkmanager --version
```

If update is available, update before proceeding — 2.4.0 includes terminal scrolling
performance fixes and general stability improvements with no regressions for Orin NX.
Update via the SDK Manager UI prompt or download from developer.nvidia.com/sdk-manager.

### 0.2 Confirm target hardware

**Only use this procedure with the non-Super J4012 (J401 carrier).**
The non-Super and Super J4012 are mechanically different — do not mix them.

### 0.3 Put J401 into recovery mode

> **Pre-installed units note:** Seeed J4012 units may ship with JetPack 6.2.2
> already installed. If `cat /etc/nv_tegra_release` shows `REVISION: 5.0` after
> EULA acceptance, the unit is already at the correct version. SDK Manager USB
> flash failures on these units are often soft-fails — the existing OS is intact.
> Accept EULA, click Skip on SDK component install, and proceed to Phase 1.
> Do not spend time debugging SDK Manager USB flash on a unit that is already
> at the correct JetPack version.
> 1. Power on without FC REC jumper
> 2. Accept EULA on Jetson display
> 3. Complete minimal first-boot setup (username/password)
> 4. Power off
> Then proceed with recovery mode below.
>
> Units that have previously been set up can go straight to recovery mode.

1. Power the unit off completely
2. Locate the **FC REC** and **GND** pins on the J401 header
   (small 2-pin header near the M.2 slots — see Seeed Studio wiki for photo)
3. Short FC REC to GND with a jumper wire
4. Connect Micro-USB from J401 to host PC
5. Apply power
6. Confirm recovery mode detected on host:

```bash
lsusb | grep -i nvidia
# Expect: Bus XXX Device XXX: ID 0955:7323 NVIDIA Corp. APX
```

If APX device not detected: check jumper, try different USB port, confirm power is on.
The jumper wire can be removed once recovery mode is confirmed.

### 0.4 SDK Manager selections

Launch SDK Manager on the host Ubuntu PC. Make exactly these selections:

| Setting | Value |
|---------|-------|
| SDK Manager version | 2.4.0.13236 |
| Host OS | Ubuntu 22.04 x86_64 |
| Target hardware | Jetson Orin NX 16GB |
| JetPack version | **6.2.2** (not 6.2 or 6.2.1 — exactly 6.2.2) |
| Jetson Linux (L4T 36.5) | ✅ Selected |
| **Jetson SDK Components:** | ❌ **Deselect all** — installed via apt in Phase 1 |
| — CUDA | ❌ Deselect |
| — CUDA-X AI | ❌ Deselect |
| — Computer Vision | ❌ Deselect |
| — Developer Tools | ❌ Deselect |
| **Jetson Platform Services** | ❌ Deselect |
| DeepStream | ❌ Not selected |
| Holoscan / other additional SDKs | ❌ Not selected |
| Storage device | **NVMe** (J401 boots from NVMe, not eMMC) |

> **Why 6.2.2 over 6.2.1:** 6.2.2 (L4T 36.5) fixes a CUDA memory allocation bug
> introduced in 6.2.1 — directly relevant to GPU inference workloads.
> Same CUDA 12.6 / cuDNN 9.3 / Ubuntu 22.04 stack, no compatibility impact.
>
> **Why deselect SDK components in SDK Manager:** Component installation via SDK
> Manager requires internet access on the Jetson during the flashing session,
> which is unreliable on mixed networks. All SDK components are installed via
> `sudo apt install nvidia-jetpack` in Phase 1.0.3 — identical result, more reliable.

### 0.5 Flash and first-boot setup

Run the flash. SDK Manager will download components and flash the NVMe.
Flash time is approximately 10–20 minutes.

**OEM / first-boot configuration settings:**

| Setting | Value |
|---------|-------|
| OEM config | Pre-config (automated first boot — no manual setup wizard) |
| Username | `ipg` |
| Password | (set per deployment policy — not recorded here) |
| Storage | NVMe |

> ⚠️ **When SDK Manager prompts for SDK component installation — click Skip/Finish.**
> All SDK components (CUDA, cuDNN, TensorRT) are installed via apt in Phase 1.
> Do NOT attempt to connect the Jetson to SDK Manager for component installation —
> this is unreliable on mixed networks and is not needed.

> ⚠️ **Remove FC REC jumper before first boot.**
> After flashing completes, before the Jetson reboots into the OS:
> - [ ] FC REC jumper removed from J401 header
> - [ ] Micro-USB recovery cable disconnected

**First boot on the Jetson — EULA acceptance required:**

Even with OEM pre-config selected, JetPack 6.x presents an EULA screen on first
boot that must be accepted directly on the Jetson.

1. Connect display and keyboard to Jetson
2. Accept EULA when prompted
3. Wait for first-boot configuration to complete (~2–3 min)
4. Log in as `ipg`

### 0.6 SDK components — install via apt (not SDK Manager)

Skip the SDK Manager component installation step. SDK Manager component push
is unreliable on mixed or restricted networks — it requires internet access on
the Jetson during the SDK Manager session which is not always available.

**Instead, install the full compute stack via apt once the Jetson is on the
lab network (Phase 3):**

```bash
sudo apt-get update
sudo apt install nvidia-jetpack
```

This installs the identical compute stack — CUDA 12.6, cuDNN 9.3, TensorRT 10.3,
VPI, DLA — as SDK Manager would have pushed. This is NVIDIA's documented
alternative installation method.

> **In SDK Manager:** Click Skip or Finish on the component installation step.
> The OS flash (JetPack Linux) is complete — that is all we need from SDK Manager.

```bash
# On the Jetson after first boot:
cat /etc/nv_tegra_release
# Expect: R36 (release), REVISION: 5.0

whoami
# Expect: ipg

df -h /
# NVMe should show available space ~100GB+
```

---

### ✅ CHECKPOINT 0 — Flash Complete

```bash
cat /etc/nv_tegra_release | grep REVISION
# Expect: REVISION: 5.0

df -h / | awk 'NR==2{print $4}'
# Must show 100GB+

whoami
# Expect: ipg
```

> **Note:** `nvcc` is not present yet — CUDA is installed via
> `sudo apt install nvidia-jetpack` in Phase 1 after the Jetson
> is connected to the lab network. Do not check for nvcc here.

**Physical checks:**
- [ ] FC REC jumper removed from J401 header
- [ ] Micro-USB disconnected
- [ ] Unit booting normally from NVMe

**All pass → proceed to Phase 1.**

---

## Phase 1 — Base System

### 1.1 Network and SSH — do this first

Configure static IP on the Jetson so all remaining work can be done over SSH.
Do this with display and keyboard connected — last time you'll need them.

**Preferred — Ubuntu Settings GUI:**
1. Settings → Network → Wired (`enP8p1s0`) → gear icon
2. IPv4 tab → Method: Manual
3. Address: `192.168.1.22`, Netmask: `255.255.255.0`, Gateway: `192.168.1.208`
4. DNS: `8.8.8.8`
5. Apply → toggle connection off/on

**Alternative — nmtui:**
```bash
sudo nmtui
# Edit enP8p1s0 → Manual → same settings as above
```

Verify and confirm SSH from Windows:
```bash
ip addr show enP8p1s0 | grep inet
# Expect: 192.168.1.22/24
```
```bash
# From Windows:
ssh ipg@192.168.1.22
```

Once SSH works — disconnect display and keyboard. All remaining steps via SSH.

### 1.2 Passwordless sudo

```bash
sudo visudo
```
Add at the bottom:
```
ipg ALL=(ALL) NOPASSWD: ALL
```
Save and exit (`:wq`). Verify:
```bash
sudo echo "sudo works"
```

### 1.3 Create directory structure

```bash
mkdir -p ~/CV/SETUP
mkdir -p ~/CV/TRC
ls ~/CV/
# Expect: SETUP  TRC
```

### 1.4 Hold L4T packages

**Must be done before any apt install — including nvidia-jetpack.**

```bash
sudo apt-mark hold \
    nvidia-l4t-bootloader \
    nvidia-l4t-kernel \
    nvidia-l4t-kernel-dtbs \
    nvidia-l4t-kernel-headers \
    nvidia-l4t-core \
    nvidia-l4t-init

# Confirm — must show all 6
apt-mark showhold
```

### 1.5 Enable internet sharing and install compute stack

Connect Jetson Ethernet to Windows PC. Enable Windows ICS:
1. `ncpa.cpl` → right-click WiFi adapter → Properties → Sharing
2. Enable sharing → select wired NIC (connected to Jetson)
3. Click OK

Test connectivity on Jetson:
```bash
ping 8.8.8.8 -c 3
ping google.com -c 3
```

If `google.com` fails, fix DNS:
```bash
echo "nameserver 8.8.8.8" | sudo tee /etc/resolv.conf
```

Install compute stack:
```bash
sudo apt-get update
sudo apt install nvidia-jetpack -y
```

Takes ~10-15 minutes. After install, add CUDA to PATH:
```bash
echo 'export PATH=/usr/local/cuda-12.6/bin:$PATH' >> ~/.bashrc
echo 'export LD_LIBRARY_PATH=/usr/local/cuda-12.6/lib64:$LD_LIBRARY_PATH' >> ~/.bashrc
source ~/.bashrc
```

Verify:
```bash
nvcc --version
# Expect: release 12.6

find /usr/include -name "cudnn*.h" 2>/dev/null | head -1
# Expect: /usr/include/cudnn_cnn.h or cudnn_graph.h
```

### 1.6 System update and base packages

```bash
sudo apt-get update
sudo apt-get upgrade -y
sudo apt-get install -y \
    v4l-utils \
    gstreamer1.0-plugins-bad \
    gstreamer1.0-plugins-good \
    gstreamer1.0-plugins-ugly \
    gstreamer1.0-libav \
    gstreamer1.0-tools \
    libcanberra-gtk-module \
    cmake-qt-gui \
    python3-pip \
    curl \
    unzip \
    net-tools \
    htop
```

### 1.7 Install jetson-stats (jtop)

```bash
sudo pip3 install -U jetson-stats
```

> **Note:** JetPack 6.2.2 ships pip 22.0.2 — does not support `--break-system-packages`.
> Use the plain command above.

```bash
jtop --version
# Expect: jtop 4.3.2
```

### 1.8 Reboot

```bash
sudo reboot
```

---

### ✅ CHECKPOINT 1 — Base System

```bash
apt-mark showhold | grep nvidia-l4t
# Must show all 6 nvidia-l4t-* packages

free -h | grep Swap
# Must show 7.6Gi

jtop --version
# Expect: 4.3.2

/usr/local/cuda-12.6/bin/nvcc --version | grep release
# Expect: release 12.6

gst-inspect-1.0 nvvidconv 2>/dev/null | grep "Long-name"
gst-inspect-1.0 nvv4l2h264enc 2>/dev/null | grep "Long-name"
gst-inspect-1.0 h264parse 2>/dev/null | grep "Long-name"
# All must return results
```

**All pass → proceed to Phase 2.**

---

## Phase 2 — Power and Performance

### 2.1 Set power mode MAXN

```bash
sudo nvpmodel -m 0
sudo nvpmodel -q
# Expect: NV Power Mode: MAXN, MODE_ID: 0
```

### 2.2 Enable jetson_clocks and set fan profile via jtop

All three settings are configured in a single jtop session:

```bash
jtop
```

In jtop navigate to the **CTRL** tab (press `5`):
- **Jetson Clocks** → ON
- **Jetson Clocks on boot** → ON (persists across reboots — activates 60s after boot)
- **Fan profile** → `cool`
- Save / exit jtop (`q`)

Verify clocks are locked:
```bash
sudo jetson_clocks --show | grep -E "cpu0:|GPU Min"
# Expect: cpu0  MinFreq=MaxFreq=1984000
# Expect: GPU MinFreq=MaxFreq=918000000
```

> **Note:** jetson_clocks activates 60 seconds after boot when set to boot mode.
> If clocks appear unlocked immediately after reboot, wait 60s and check again.

### 2.3 Remove desktop environment and LibreOffice

Required for all production units — reduces attack surface, memory footprint,
and eliminates unnecessary background services.

```bash
sudo systemctl stop gdm3
sudo systemctl disable gdm3
sudo systemctl set-default multi-user.target
sudo apt remove --purge ubuntu-desktop gdm3 -y
sudo apt purge libreoffice* -y
sudo apt autoremove -y
sudo reboot
```

After reboot the unit will boot to console only. All remaining work is via SSH.

> **Note:** This is irreversible without reinstalling packages. Confirm SSH access
> is working before running this step — you will lose local GUI access permanently.

---

### ✅ CHECKPOINT 2 — Power and Performance

```bash
# Power mode
sudo nvpmodel -q
# Expect: NV Power Mode: MAXN

# Clocks locked
sudo jetson_clocks --show | grep -E "cpu0|GPU"
# Expect: MinFreq=MaxFreq=CurrentFreq on cpu0 and GPU

# Fan (check via jtop CTRL tab — should show cool profile)
```

**All pass → proceed to Phase 3.**

---

## Phase 3 — Network and Timing

> **Jetson Ethernet interface name:** `enP8p1s0` on J4012 non-Super with JetPack 6.2.2.
> Note: earlier documentation referenced `enp8s0` and `eth0` — both incorrect for this unit.
> The name changed between JetPack versions. Always verify on your unit:
> ```bash
> ip link show | grep -v "lo\|can\|usb\|l4t"
> ```

### 3.1 Update gateway for deployment

After internet sharing phase is complete, update the gateway from the Windows ICS
address to the deployment network gateway:

```bash
sudo nmtui
```
- Edit a connection → Wired (`enP8p1s0`)
- Gateway: change from `192.168.1.208` → `192.168.1.1` (or clear if no gateway needed)
- DNS: change from `8.8.8.8` → `192.168.1.33` (NTP appliance also serves DNS)
- Save → Activate a connection → deactivate/reactivate

```bash
ip route show default
# Expect: default via 192.168.1.1 (not 192.168.1.208)
```

### 3.2 NTP configuration

```bash
sudo tee /etc/systemd/timesyncd.conf > /dev/null << 'EOF'
[Time]
NTP=192.168.1.33
FallbackNTP=192.168.1.208
EOF

sudo systemctl restart systemd-timesyncd
timedatectl status
```

Expected output:
```
NTP service: active
```

`System clock synchronized: no` is expected indoors — `.33` requires GPS sky view.
Verify the config is correct regardless:
```bash
cat /etc/systemd/timesyncd.conf
# Must show NTP=192.168.1.33 and FallbackNTP=192.168.1.208
```

### 3.3 USB memory buffer

Required for Alvium USB3 camera DMA headroom. Check if already set:
```bash
grep "usbfs" /boot/extlinux/extlinux.conf
```

If not present, add `usbcore.usbfs_memory_mb=1000` to the APPEND line:
```bash
# Backup first
sudo cp /boot/extlinux/extlinux.conf /boot/extlinux/extlinux.conf.bak

# Edit
sudo vi /boot/extlinux/extlinux.conf
# Add to APPEND line (before last quote if present):
#   usbcore.usbfs_memory_mb=1000

# Verify the change
diff /boot/extlinux/extlinux.conf /boot/extlinux/extlinux.conf.bak
grep "usbfs" /boot/extlinux/extlinux.conf
```

Reboot to apply:
```bash
sudo reboot
```

---

### ✅ CHECKPOINT 3 — Network and Timing

```bash
# IP address
ip addr show eth0 | grep "inet "
# Expect: inet 192.168.1.22/24

# NTP config (correct servers)
cat /etc/systemd/timesyncd.conf
# Expect: NTP=192.168.1.33, FallbackNTP=192.168.1.208

# USB buffer in kernel args
grep "usbfs" /boot/extlinux/extlinux.conf
# Expect: usbcore.usbfs_memory_mb=1000

# Ping network (if other devices available)
ping -c 3 192.168.1.208
```

**All pass → proceed to Phase 4.**

---

## Phase 4 — VimbaX 2026-1

**Pre-requisite:** VimbaX ARM64 tarball must be on the Jetson before starting.
All install files are staged in `~/CV/SETUP/` — create if not present:

```bash
mkdir -p ~/CV/SETUP
cd ~/CV/SETUP
```

Transfer tarball from host:
```bash
# From Windows host:
scp VimbaX_Setup-2026-1-Linux_ARM64.tar.gz ipg@192.168.1.22:~/CV/SETUP/
```

### 4.1 Extract and install

```bash
cd ~
sudo mv VimbaX_Setup-2026-1-Linux_ARM64.tar.gz /opt/
cd /opt/
sudo tar xvf VimbaX_Setup-2026-1-Linux_ARM64.tar.gz
ls /opt/VimbaX_2026-1/
# Should show: api  bin  cti  ...
```

### 4.2 Install USB transport layer

Camera is Alvium 1800 U-291c (USB3). Use the unified GenTL path installer:

```bash
cd /opt/VimbaX_2026-1/cti/
sudo ./Install_GenTL_Path.sh
# Expect: "Registering GENICAM_GENTL64_PATH for Vimba X"
#         "Registering AVTUSBTL device types"
#         "Done. Please reboot before using the Transport Layers"
```

> **Note — VimbaX 2026-1:** There is no separate `VimbaUSBTL_Install.sh` in this
> release. `Install_GenTL_Path.sh` registers all transport layers including USB.
> Earlier documentation referenced `VimbaUSBTL_Install.sh` — this is incorrect for 2026-1.

### 4.3 Configure ldconfig for VimbaX libs

```bash
sudo tee /etc/ld.so.conf.d/vimbax.conf > /dev/null << 'EOF'
/opt/VimbaX_2026-1/api/lib/
EOF

sudo ldconfig
```

> **Note — VimbaX 2026-1:** `ldconfig` will show this warning — it is harmless:
> ```
> /sbin/ldconfig.real: /opt/VimbaX_2026-1/api/lib/libVmbNUC.so.1 is not a symbolic link
> ```
> `libVmbNUC.so.1` is a new 2026-1 library (Goldeye camera support) shipped without
> a symlink. Does not affect USB camera operation. All VimbaX libs are still registered.

```bash
# Verify libs registered
ldconfig -v 2>/dev/null | grep -i libVmb
# Expect: libVmbC.so and libVmbCPP.so listed
```

### 4.4 GENICAM environment variable

Add to `~/.bashrc`:
```bash
echo 'export GENICAM_GENTL64_PATH=/opt/VimbaX_2026-1/cti/' >> ~/.bashrc
source ~/.bashrc
```

### 4.5 Reboot

```bash
sudo reboot
```

### 4.6 Verify camera detected

```bash
/opt/VimbaX_2026-1/bin/ListCameras_VmbCPP
```

Expected output (camera details will vary by unit):
```
Vmb Version Major: 1 Minor: 3 Patch: 0
TransportLayers found: 4
Cameras found: 4   ← 1 real camera + 3 simulators (normal in 2026-1)

/// Camera Name  : Allied Vision 1800 U-xxx
/// Camera ID    : DEV_1AB22C0xxxxxx
/// @ TransportLayer Path : /opt/VimbaX_2026-1/cti/VimbaUSBTL.cti
```

> **Note — VimbaX 2026-1:** Three additional "Camera Simulator" entries will appear
> from `VimbaCameraSimulatorTL.cti` — this is a new component in 2026-1 and is normal.
> The real physical camera is the one with `VimbaUSBTL.cti` as its transport layer.

### 4.7 Install gst-vmbsrc GStreamer plugin

Used for diagnostic camera testing via GStreamer pipelines. TRC does not use
this plugin operationally — camera access is via VimbaX API directly in C++.
The prebuilt ARM64 binary is sufficient for diagnostic use.

```bash
cd ~/CV/SETUP

# Download prebuilt binary
wget https://github.com/alliedvision/gst-vmbsrc/releases/download/1.0.0/gst-vmbsrc-1.0.0-linux-arm64.zip
unzip ./gst-vmbsrc-1.0.0-linux-arm64.zip

# Install plugin
sudo cp ./deploy/lib/libgstvmbsrc.so /usr/lib/$(uname -m)-linux-gnu/gstreamer-1.0/

# Update ldconfig cache
sudo ldconfig

# Verify plugin detected
gst-inspect-1.0 | grep vmb
# Expect: vmbsrc:  vmbsrc: VimbaX GStreamer source
```

> **Note:** Prebuilt binary does not include NVMM zero-copy support — requires
> building from source against Jetson GStreamer libs. For diagnostic use this is
> acceptable. nvvidconv handles the CPU→NVMM copy transparently.

### 4.8 Camera pipeline test

Verify full camera → encode → stream path before proceeding to OpenCV.
Start receive side on Windows first, then send from Jetson.

**Receive (Windows):**
```
gst-launch-1.0.exe udpsrc port=5000 buffer-size=2097152 caps="application/x-rtp,media=video,encoding-name=H264,payload=96" ! rtpjitterbuffer latency=50 drop-on-latency=true ! rtph264depay ! h264parse ! nvh264dec ! videoconvert n-threads=4 ! fpsdisplaysink sync=false text-overlay=true signal-fps-measurements=true
```

**Send (Jetson) — update camera ID from ListCameras output:**
```bash
gst-launch-1.0 -v vmbsrc camera=DEV_1AB22C04ECA1 \
    width=1024 height=720 exposuretime=20000 \
    ! video/x-raw,format=UYVY \
    ! nvvidconv \
    ! "video/x-raw(memory:NVMM),width=1024,height=720" \
    ! queue max-size-time=800000 leaky=2 \
    ! nvv4l2h264enc insert-sps-pps=1 idrinterval=15 maxperf-enable=true \
    ! h264parse \
    ! rtph264pay \
    ! udpsink host=192.168.1.208 port=5000 sync=0
```

**Pass:** Live H.264 video visible on Windows. ✅ Confirmed working 2026-04-09.

---

### ✅ CHECKPOINT 4 — VimbaX

```bash
# SDK installed
ls /opt/VimbaX_2026-1/api/lib/libVmbCPP.so
# Must exist

# ldconfig finds libs
ldconfig -v 2>/dev/null | grep -i "libVmb"
# Must show libVmbC and libVmbCPP

# GENICAM path set
echo $GENICAM_GENTL64_PATH
# Must show /opt/VimbaX_2026-1/cti/

# Camera detected
/opt/VimbaX_2026-1/bin/ListCameras_VmbCPP
# Must show Cameras found: 1 with correct Camera ID
```

**All pass → proceed to Phase 5.**

---

## Phase 5 — OpenCV 4.13.0

This phase uses the provided build script. Build time is 60–90 minutes.

### 5.1 Transfer build script

```bash
# From Windows host or USB — transfer to CV/SETUP staging area:
scp install_opencv4.13.0_Jetpack6.2.2.sh ipg@192.168.1.22:~/CV/SETUP/
```

### 5.2 Run build script

```bash
cd ~/CV/SETUP/
# Note: script filename references 6.2.1 but is fully compatible with 6.2.2
chmod +x install_opencv4.13.0_Jetpack6.2.2.sh
./install_opencv4.13.0_Jetpack6.2.2.sh
```

The script will:
- Pre-flight check CUDA, cuDNN, swap, GStreamer
- Ask whether to purge system OpenCV (answer **yes**)
- Download sources, configure, build, install
- Run cmake gate (aborts if critical flags not resolved)
- Run verification steps automatically

Monitor the cmake gate output — if it aborts, check the log file path printed on screen.

### 5.3 Apply environment

```bash
source ~/.bashrc
```

---

### ✅ CHECKPOINT 5 — OpenCV

```bash
source ~/.bashrc

# Create quick test script if not already present
cat > ~/CV/SETUP/test1.py << 'EOF'
import cv2
import numpy as np
import sys
print("PYTHON VS: " + sys.version)
print("NUMPY VS: " + np.__version__)
print("OPENCV VS: " + cv2.__version__)
EOF

python3 ~/CV/SETUP/test1.py
# Expect: Python 3.10.x, NumPy 1.21.x, OpenCV 4.13.0

# CUDA DNN targets
python3 -c "
import cv2
print('CUDA DNN targets:', cv2.dnn.getAvailableTargets(cv2.dnn.DNN_BACKEND_CUDA))
print('CUDA devices:    ', cv2.cuda.getCudaEnabledDeviceCount())
"
# Expect: targets [6, 7], devices 1

```bash
# Version and location
python3 -c "import cv2; print(cv2.__version__); print(cv2.__file__)"
# Expect: 4.13.0
# Expect: /usr/local/lib/python3.10/dist-packages/cv2/__init__.py

# CUDA DNN targets
python3 -c "
import cv2
print('Targets:', cv2.dnn.getAvailableTargets(cv2.dnn.DNN_BACKEND_CUDA))
print('CUDA DNN compiled:', cv2.cuda.getCudaEnabledDeviceCount() > 0)
"
# Expect: Targets: [6, 7]

# Key build flags
python3 -c "import cv2; print(cv2.getBuildInformation())" \
    | grep -E "NVIDIA CUDA|cuDNN|GStreamer|TBB|FFMPEG"
# Expect: all YES

# pkg-config works (needed for TRC Makefile)
pkg-config --modversion opencv4
# Expect: 4.13.0
```

**All pass → proceed to Phase 6.**

---

## Phase 6 — TRC Build

### 6.1 Get TRC source

```bash
# If not already present — transfer from host:
scp -r /path/to/TRCv3/v3.0.2 ipg@192.168.1.22:~/CV/TRCv3/
```

### 6.2 Update Makefile VimbaX path

```bash
cd ~/CV/TRCv3/v3.0.2
grep "VIMBAX_DIR" Makefile
# Current: VIMBAX_DIR := /opt/VimbaX_2025-1

# Update to 2026-1
sed -i 's|VimbaX_2025-1|VimbaX_2026-1|g' Makefile
grep "VIMBAX_DIR" Makefile
# Confirm: VIMBAX_DIR := /opt/VimbaX_2026-1
```

### 6.3 Build

```bash
make clean && make -j$(nproc) 2>&1 | tee ~/trc_build_$(date +%Y%m%d).log
```

Watch for errors. Build should complete in 3–5 minutes.

### 6.4 Verify binary links

```bash
ldd ./trc | grep -E "opencv_dnn|VmbC"
# Must show:
#   libopencv_dnn.so.413 => /usr/local/lib/libopencv_dnn.so.413
#   libVmbCPP.so => /opt/VimbaX_2026-1/api/lib/libVmbCPP.so
```

### 6.5 Copy model files

```bash
# model_data/ must be present in the working directory
ls ~/CV/TRCv3/v3.0.2/model_data/
# Must show:
#   frozen_inference_graph.pb
#   ssd_mobilenet_v3_large_coco_2020_01_14.pbtxt
#   coco.names
```

If missing, copy from known-good source.

---

### ✅ CHECKPOINT 6 — TRC Build

```bash
cd ~/CV/TRCv3/v3.0.2

# Binary exists
ls -lh trc

# Correct library links
ldd ./trc | grep -E "opencv_dnn|VmbC|Vmb"
# libopencv_dnn.so.413 must be from /usr/local/lib/
# libVmbCPP must be from /opt/VimbaX_2026-1/

# Version string
./trc --version
# Expect: TRC 3.0.2 <date> <time>

# COCO inference probe
python3 << 'EOF'
import cv2, numpy as np
net = cv2.dnn.readNet("model_data/frozen_inference_graph.pb",
                      "model_data/ssd_mobilenet_v3_large_coco_2020_01_14.pbtxt")
blob = cv2.dnn.blobFromImage(np.zeros((320,320,3),dtype=np.uint8),
                             1/127.5,(320,320),(127.5,127.5,127.5),swapRB=True)
for backend, target, label in [
    (cv2.dnn.DNN_BACKEND_CUDA, cv2.dnn.DNN_TARGET_CUDA_FP16, "CUDA FP16"),
    (cv2.dnn.DNN_BACKEND_CUDA, cv2.dnn.DNN_TARGET_CUDA,      "CUDA FP32"),
    (cv2.dnn.DNN_BACKEND_DEFAULT, cv2.dnn.DNN_TARGET_CPU,    "CPU"),
]:
    try:
        net.setPreferableBackend(backend); net.setPreferableTarget(target)
        net.setInput(blob); net.forward()
        print(f"  {label}: OK")
    except Exception as e:
        print(f"  {label}: FAILED — {e}")
EOF
# All three must show OK
```

**All pass → proceed to Phase 7.**

---

## Phase 7 — Autostart

### 7.1 Transfer launch scripts

Transfer both start scripts to the Jetson:
```bash
# From Windows host:
scp trc_start.sh trc_start_bench.sh ipg@192.168.1.22:~/CV/TRC/
```

On Jetson:
```bash
chmod +x ~/CV/TRC/trc_start.sh
chmod +x ~/CV/TRC/trc_start_bench.sh
```

### 7.2 Test bench autostart first

Use the bench script (MWIR test source, unicast) for initial autostart testing.
This lets you verify autostart behaviour without needing MWIR camera or multicast.

```bash
crontab -e
```

Add:
```
SHELL=/bin/bash
@reboot sleep 30 && /home/ipg/CV/TRC/trc_start_bench.sh
```

> **Why sleep 30:** Allows Alvium USB camera to enumerate after boot.
> 30 seconds is sufficient — original notes used 60s which is unnecessarily long.

Reboot and verify:
```bash
sudo reboot
# Wait ~60 seconds after reboot, then:
ps aux | grep trc | grep -v grep
# Must show trc process running

tail -20 ~/CV/TRC/trc_bench.log
# Must show TRC startup messages, no fatal errors
```

On Windows — confirm stream visible on `192.168.1.208:5000`.

### 7.3 Switch to production autostart

Once bench autostart confirmed working:

```bash
crontab -e
```

Change to production script:
```
SHELL=/bin/bash
@reboot sleep 30 && /home/ipg/CV/TRC/trc_start.sh
```

Reboot and confirm multicast stream on `239.127.1.21:5000`.

---

### ✅ CHECKPOINT 7 — Autostart

```bash
# After reboot — wait ~60s then check
ps aux | grep trc | grep -v grep
# Must show trc process running

tail -20 ~/CV/TRC/trc.log
# Must show startup messages, no fatal errors

# Confirm video stream active
sudo tcpdump -i enP8p1s0 -c 10 host 239.127.1.21
# Should see UDP packets to multicast group
```

**All pass → proceed to Phase 8.**

---

## Phase 8 — Full System Verification Gate

Run the verify script with TRC running:

```bash
cd ~/CV/SETUP/
./04_verify_all.sh
```

**Gate:** Must show **54 PASS, 0 WARN, 0 FAIL** before proceeding to imaging.

The log is saved to `~/CV/SETUP/jetson_verified_YYYYMMDD_HHMMSS.txt` — keep this
as the definitive pre-image baseline record.

### Pass criteria reference

| Check | Expected |
|-------|----------|
| JetPack | R36 / REVISION: 5.0 (L4T 36.5 / JetPack 6.2.2) |
| OpenCV version | 4.13.0 |
| OpenCV location | `/usr/local/lib/python3.10/dist-packages/` |
| CUDA DNN targets | `[6, 7]` |
| CUDA | YES ver 12.6 with FAST_MATH |
| cuDNN | YES ver 9.3.0 |
| GStreamer | YES |
| TBB | YES |
| VimbaX | `VimbaX_2026-1` |
| Camera detected | Allied Vision 1800 U-xxx via VimbaUSBTL |
| NTP server | 192.168.1.33 |
| NTP fallback | 192.168.1.208 |
| nvpmodel | MAXN |
| jetson_clocks | MinFreq=MaxFreq=1984000 on CPU0, GPU locked |
| Swap | 7.6GB |
| USB buffer | `usbcore.usbfs_memory_mb=1000` |
| TRC binary | `libopencv_dnn.so.413 => /usr/local/lib/` |
| TRC binary | `libVmbCPP => /opt/VimbaX_2026-1/` |
| Makefile | `VIMBAX_DIR := /opt/VimbaX_2026-1` |
| Autostart | TRC running after reboot |
| COCO FP16 | OK |
| COCO FP32 | OK |
| CPU inference | OK |

---

## Phase 9 — overlayFS and Image

### 9.1 Enable overlayFS (read-only rootfs)

overlayFS makes the rootfs read-only with a tmpfs overlay. Changes made at runtime
do not persist across reboots — the system always boots to the known-good state.

```bash
sudo apt install overlayroot -y

sudo tee /etc/overlayroot.conf > /dev/null << 'EOF'
overlayroot="tmpfs"
overlayroot_cfgdisk="disabled"
EOF

# Verify config
cat /etc/overlayroot.conf
```

Reboot to activate:
```bash
sudo reboot
```

After reboot, verify overlay is active and TRC still starts:
```bash
mount | grep overlay
# Expect: overlay on / type overlay (rw,...)

# Wait ~60s then confirm TRC running
ps aux | grep trc | grep -v grep
# Must show trc process
```

**Note:** With overlayFS active, any changes made to the system do NOT persist.
To make permanent changes, disable overlay first:
```bash
sudo overlayroot-chroot   # enter chroot with write access
# make changes
exit
sudo reboot
```

### 9.2 Create image

Boot the Jetson from a USB drive (or use another machine to image the NVMe directly).

**Method A — Image from running system (if overlayFS is active, rootfs is clean):**
```bash
# Identify NVMe device
lsblk | grep nvme

# Image to USB drive (adjust paths)
sudo dd if=/dev/nvme0n1 of=/media/usb/jetson_trc_v3.0.2_$(date +%Y%m%d).img \
    bs=4M status=progress conv=fsync

# Record image size and checksum
sha256sum /media/usb/jetson_trc_v3.0.2_$(date +%Y%m%d).img | tee \
    /media/usb/jetson_trc_v3.0.2_$(date +%Y%m%d).sha256
```

**Method B — Image via Jetson SDK Manager backup tools (recommended for full partition layout):**
```bash
sudo ./tools/kernel_flash/l4t_backup_restore.sh -e nvme0n1 backup
```

### 9.3 Restore image to new unit

```bash
# Boot new Jetson from USB with recovery tools, then:
sudo dd if=jetson_trc_v3.0.2_YYYYMMDD.img of=/dev/nvme0n1 \
    bs=4M status=progress conv=fsync

# After restore, update unit-specific settings:
#   - Static IP (if different unit has different IP)
#   - Hostname
sudo reboot
```

---

## Known Issues and Notes

| Issue | Notes |
|-------|-------|
| `System clock synchronized: no` | Expected indoors — `.33` requires GPS sky view outdoors |
| RTC at epoch zero | Expected on first power-on — resolves after first NTP sync |
| VimbaX path in Makefiles | Archive versions intentionally left at 2025-1. Only v3.0.2 updated. |
| gst-vmbsrc | NOT used. TRC accesses Alvium directly via VimbaX API. Do not install. |
| Super Mode | NOT supported on J4012 non-Super (J401 carrier). MAXN is the ceiling. Super J4012 (new carrier) supports it but requires a separate procedure — do not mix images between variants. |
| `OPENCV_DNN_CUDA` not in getBuildInformation() | Normal for OpenCV 4.11.x+. Confirm via cmake cache or getAvailableTargets(). |
| overlayFS and permanent changes | Must use `overlayroot-chroot` to make changes that survive reboot. |

---

## Version History

| Version | Date | Notes |
|---------|------|-------|
| 1.0.0 | 2026-04-09 | Initial — based on live baseline verification of lab Jetson 2026-04-06 |

---

*See also: OPENCV_BUILD_HISTORY.md, TOMORROW_SESSION_PLAN.md, ARCHITECTURE.md §2.5*
