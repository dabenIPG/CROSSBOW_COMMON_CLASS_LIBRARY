# JETSON_SETUP.md — TRC Jetson Orin NX Setup Procedure
**Document:** JETSON_SETUP.md (DOC-2)  
**Version:** 2.3.3  
**Date:** 2026-04-24  
**Confirmed on:** Unit 1 (2026-04-09), Unit 2 (2026-04-09), Unit 3 (2026-04-10), Unit 13 (2026-04-24), Unit 14 (2026-04-24) — all 54 PASS, 0 FAIL (Gate A) / 53 PASS, 1 expected FAIL (Gate B)  
**Platform:** Seeed Studio reComputer J4012 **(non-Super, J401 carrier)** — see hardware note below  
**JetPack:** 6.2.2 (L4T 36.5, Ubuntu 22.04, CUDA 12.6, cuDNN 9.3)  
**Reference:** ARCHITECTURE.md §2.5, OPENCV_BUILD_HISTORY.md, TOMORROW_SESSION_PLAN.md

---

> # ⛔ CRITICAL — READ BEFORE STARTING
>
> **Follow this document exactly, in order, with no deviations.**
>
> - Execute every step as written. Do not skip, reorder, or substitute steps.
> - Do not proceed to the next step until the current step passes its expected output.
> - Do not add steps that are not in this document.
> - If a step produces unexpected output, STOP and resolve it before continuing.
> - If you are working with an assistant (human or AI), it must also follow this document exactly. If the assistant suggests a step not in this document, do not execute it — redirect to the document.
> - Deviations that seem harmless often have downstream consequences (wrong sequencing of holds, missed gate checks, cleanup before verification). The document exists because these consequences have been learned the hard way.
>
> **The only acceptable deviation is one explicitly directed by a senior engineer who has reviewed the full context.**
>
> If you find a step that is wrong or missing, note it for a document update — do not fix it inline during a build.

---

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

**Use when:**
- Deploying a new non-Super J4012 unit from a validated image produced by Path A.
- **Replacing an existing unit** with a clean gold image when Path C (apt upgrade) is not applicable or the unit is too far out of state.

```bash
# Boot target unit from USB recovery drive, then:
sudo dd if=jetson_trc_v3.x.x_YYYYMMDD.img of=/dev/nvme0n1 \
    bs=4M status=progress conv=fsync
```

After restore, update unit-specific settings:
```bash
# Set hostname from SOM serial number
SERIAL=$(cat /proc/device-tree/serial-number | tr -d '\0')
sudo hostnamectl set-hostname trc-${SERIAL}
sudo sed -i "s/ubuntu/trc-${SERIAL}/g" /etc/hosts

# Verify
hostname
cat /etc/hosts | grep trc
```

Static IP does not need to change — 192.168.1.22 is the TRC role address and is
baked into the image correctly.

**Gate:** Image must be from a non-Super J4012 (J401 carrier). Do not restore
a non-Super image to a Super J4012 or vice versa — mechanically incompatible.

---

### Deployment Path C — In-place apt upgrade (6.2.1 → 6.2.2)

**Use when:** Unit is running JetPack 6.2.x (L4T 36.4.x) and needs upgrading to 6.2.2
(L4T 36.5) without a full reflash. Preserves existing data, applications, and
configurations. Validated 2026-04-13 through 2026-04-18 on REVISION 4.3, 4.4, and 4.7.

> ⛔ **R35 (JetPack 5.x) units cannot use Path C.** The gap between JetPack 5.x (L4T 35.x,
> Ubuntu 20.04) and 6.2.2 (L4T 36.x, Ubuntu 22.04) is a major version jump — different
> kernel, different OS, different CUDA stack. Use **Path A** (full reflash) for any unit
> showing `R35` in `/etc/nv_tegra_release`.

> **What this upgrades:** CUDA compute stack + L4T kernel. Does not change the
> bootloader. Unit stays fully operational throughout.
>
> **What still needs manual push after:** VimbaX 2026-1, OpenCV 4.13.0 (rebuild),
> TRC binary, hostname, NTP, crontab — follow relevant phases below.

**Prerequisites:**
- Internet access on the Jetson (Windows ICS or direct)
- Gateway must be set correctly and survive reboot — fix permanently via nmtui before starting
- L4T packages must be held before touching apt
- **Clock must be correct** — apt will fail with "not valid yet" errors if the system clock is wrong

**Step 1 — Verify and sync clock:**

The system clock must be correct before running apt. Old units with a dead RTC battery
will have a stale clock that causes apt repo validation failures.

```bash
# Check current time
date

# Set temporary NTP to your internet gateway (e.g. 192.168.1.8 or 192.168.1.208)
sudo tee /etc/systemd/timesyncd.conf > /dev/null << 'EOF'
[Time]
NTP=192.168.1.8
EOF
sudo systemctl restart systemd-timesyncd
sleep 15
timedatectl timesync-status
# Must show Packet count > 0
```

If NTP sync fails, force the time manually:
```bash
sudo timedatectl set-ntp false
sudo timedatectl set-time "YYYY-MM-DD HH:MM:SS"   # use current local time
sudo timedatectl set-ntp true
date
```

> **At the end of Path C** — restore NTP to gold standard:
> ```bash
> sudo tee /etc/systemd/timesyncd.conf > /dev/null << 'EOF'
> [Time]
> NTP=192.168.1.33
> FallbackNTP=192.168.1.208
> EOF
> sudo systemctl restart systemd-timesyncd
> ```

**Step 1b — Set gateway and verify internet:**

> ⚠️ Always set the gateway before testing internet — the default gateway on old units
> typically points to `.1` which has no internet access.

```bash
# Set gateway to your internet source
sudo ip route replace default via 192.168.1.208   # or .8 depending on your setup
echo "nameserver 8.8.8.8" | sudo tee /etc/resolv.conf
ping 8.8.8.8 -c 3
# Must succeed before continuing
```

**Step 1c — Passwordless sudo:**

```bash
sudo grep -q "ipg ALL=(ALL) NOPASSWD" /etc/sudoers || \
    echo "ipg ALL=(ALL) NOPASSWD: ALL" | sudo tee -a /etc/sudoers
sudo echo "sudo works"
```

**Step 1d — Clean CV directory:**

Old units may have stale TRC source, old VimbaX references, and legacy scripts in `~/CV/`.
Remove and recreate clean:

```bash
rm -rf ~/CV/
mkdir -p ~/CV/SETUP
mkdir -p ~/CV/TRC
ls ~/CV/
# Expect: SETUP  TRC
```

**Step 2 — Hold L4T packages:**
```bash
sudo apt-mark hold \
    nvidia-l4t-bootloader \
    nvidia-l4t-kernel \
    nvidia-l4t-kernel-dtbs \
    nvidia-l4t-kernel-headers \
    nvidia-l4t-core \
    nvidia-l4t-init

apt-mark showhold | grep nvidia-l4t
# Must show all 6
```

**Step 3 — Update apt sources to r36.5:**
```bash
# Verify current source
cat /etc/apt/sources.list.d/nvidia-l4t-apt-source.list
# Expect: r36.4

sudo sed -i 's/r36\.4/r36.5/g' /etc/apt/sources.list.d/nvidia-l4t-apt-source.list

# Verify
cat /etc/apt/sources.list.d/nvidia-l4t-apt-source.list
# Expect: r36.5 on all three lines
```

**Step 4 — Upgrade CUDA compute stack:**

> ⚠️ **REVISION 4.7 units:** On units running L4T REVISION 4.7, `dist-upgrade` with
> L4T packages held will show "0 upgraded" because r36.5 packages have a dependency
> chain that requires all L4T packages to upgrade together. If you see this, skip to
> Step 5 — the full upgrade will happen there with the unhold.
>
> **REVISION 4.4 units:** `dist-upgrade` works normally with packages held — CUDA
> stack upgrades independently.

```bash
sudo apt-get update
sudo apt-get dist-upgrade -y
sudo apt install --fix-broken -o Dpkg::Options::="--force-overwrite"
```

> If prompted about config files — answer **N** (keep current version).
> If output shows `0 upgraded, 0 newly installed` — this is expected on 4.7 units, proceed to Step 5.

**Step 5 — Upgrade L4T kernel:**

**For REVISION 4.4 units** — selective upgrade (kernel only, bootloader stays held):
```bash
# Unhold
sudo apt-mark unhold \
    nvidia-l4t-bootloader \
    nvidia-l4t-kernel \
    nvidia-l4t-kernel-dtbs \
    nvidia-l4t-kernel-headers \
    nvidia-l4t-core \
    nvidia-l4t-init

# Install new kernel packages
sudo apt-get install -y \
    nvidia-l4t-core \
    nvidia-l4t-kernel \
    nvidia-l4t-kernel-dtbs \
    nvidia-l4t-kernel-headers \
    nvidia-l4t-init
```

**For REVISION 4.7 units** — full upgrade (all packages together):
```bash
# Unhold all
sudo apt-mark unhold \
    nvidia-l4t-bootloader \
    nvidia-l4t-kernel \
    nvidia-l4t-kernel-dtbs \
    nvidia-l4t-kernel-headers \
    nvidia-l4t-core \
    nvidia-l4t-init

# Full dist-upgrade — upgrades everything together
sudo apt-get dist-upgrade -y
```

**Both paths — rehold immediately after:**
```bash
sudo apt-mark hold \
    nvidia-l4t-bootloader \
    nvidia-l4t-kernel \
    nvidia-l4t-kernel-dtbs \
    nvidia-l4t-kernel-headers \
    nvidia-l4t-core \
    nvidia-l4t-init

apt-mark showhold | grep nvidia-l4t
# Must show all 6
```

**Step 6 — Reboot and verify:**
```bash
sudo reboot
```

> ⚠️ **Expect a long first boot after kernel upgrade** — initramfs regeneration can
> make the first boot take 2–3 minutes. Use `ping -t 192.168.1.22` and wait for
> replies before attempting SSH. Do not assume the unit is bricked until at least
> 3 minutes have passed.

After reboot — fix gateway if needed, then verify:
```bash
cat /etc/nv_tegra_release
# Expect: REVISION: 5.0

nvcc --version | grep release
# Expect: release 12.6
```

**Then continue with the relevant phases:**
- VimbaX upgrade → Phase 4 (remove old version first: `sudo rm -rf /opt/VimbaX_2025-1`)
- OpenCV rebuild → Phase 5 (purge old build first per script prompts)
- TRC binary push → Phase 6 (make clean && make mandatory — soname changed)
- All other phases as normal → Phase 7, 8

**Gate:** `cat /etc/nv_tegra_release` must show `REVISION: 5.0` before proceeding.

---

### Path D — Super J4012 (future)

The reComputer Super J4012 uses a different carrier board with higher TDP and
Super Mode support. It requires a separate setup procedure. Do not use this
document for Super J4012 units. See future JETSON_SUPER_SETUP.md.

> **IP address:** Super J4012 units will share the same TRC role address
> (192.168.1.22) as non-Super units. The address belongs to the TRC role,
> not the hardware variant.

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
| Jetson static IP | 192.168.1.22 — **role address, shared by all TRC units (non-Super and Super). Only one unit is ever live on the network at a time. The address belongs to the TRC role, not the physical hardware.** |
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

> ⚠️ **Always reflash — no exceptions.**
> All units must be reflashed with our known-good JetPack 6.2.2 image regardless
> of what version ships pre-installed. Pre-installed OS may contain unknown
> packages, OEM customizations, or security vulnerabilities.
>
> **Correct pre-flash sequence for all new units — two boots required:**
>
> **Boot 1A — Accept factory EULA:**
> 1. Power on WITHOUT FC REC jumper — monitor and keyboard required
> 2. Accept EULA on Jetson display
> 3. Complete full OEM first-boot setup (username `ipg`, password)
> 4. Do NOT install Chromium if prompted — it is not needed, skip it
> 5. Let it fully boot to desktop
>
> **Boot 1B — Reject secondary prompts:**
> 6. The unit will reboot automatically into a second-stage setup
> 7. Reject Canonical/Ubuntu Pro prompts and any other optional install prompts
> 8. Confirm fully booted and stable at desktop
>
> **Boot 2 — Confirm stable:**
> 9. Reboot — confirm fully booted and stable at desktop
> 10. Power off completely
>
> **Then proceed with recovery mode below.**
> SDK Manager flash will fail or loop if either boot is skipped.
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

### 0.4 SDK Manager selections — complete pre-flash checklist

> ⚠️ **Complete this checklist before clicking Flash. All settings must be confirmed before proceeding.**

Launch SDK Manager on the host Ubuntu PC. Make exactly these selections:

| Setting | Value |
|---------|-------|
| SDK Manager version | 2.4.0.13236 |
| Host OS | Ubuntu 22.04 x86_64 |
| Target hardware | Jetson Orin NX 16GB |
| JetPack version | **6.2.2** (not 6.2 or 6.2.1 — exactly 6.2.2) |
| Jetson Linux (L4T 36.5) | ✅ Selected |
| **Jetson SDK Components:** | ❌ **Deselect ALL** — installed via apt in Phase 1 |
| — CUDA | ❌ Deselect |
| — CUDA-X AI | ❌ Deselect |
| — Computer Vision | ❌ Deselect |
| — Developer Tools | ❌ Deselect |
| **Jetson Runtime Components:** | ❌ **Deselect ALL** — installed via apt in Phase 1 |
| **Jetson Platform Services** | ❌ Deselect |
| DeepStream | ❌ Not selected |
| Holoscan / other additional SDKs | ❌ Not selected |
| Storage device | **NVMe** (J401 boots from NVMe, not eMMC) |
| **OEM config** | **Pre-config** |
| **Username** | **`ipg`** |
| **Password** | (set per deployment policy — not recorded here) |

> **Why deselect ALL SDK and runtime components in SDK Manager:** All SDK and runtime components are installed via `sudo apt install nvidia-jetpack` in Phase 1. This is NVIDIA's documented alternative installation method — identical result, more reliable, no network dependency during the SDK Manager flash session. This is the confirmed standard for Path A.
>
> **Why 6.2.2 over 6.2.1:** 6.2.2 (L4T 36.5) fixes a CUDA memory allocation bug introduced in 6.2.1 — directly relevant to GPU inference workloads. Same CUDA 12.6 / cuDNN 9.3 / Ubuntu 22.04 stack, no compatibility impact.

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

### 1.1a Set hostname from SOM serial number

Every unit gets a unique hostname derived from its NVIDIA SOM serial number.
This is the only unit-specific configuration — all other settings are identical
across the fleet.

```bash
# Read SOM serial and set hostname
SERIAL=$(cat /proc/device-tree/serial-number | tr -d '\0')
sudo hostnamectl set-hostname trc-${SERIAL}
sudo sed -i "s/ubuntu/trc-${SERIAL}/g" /etc/hosts

# Verify
hostname
# Expect: trc-<serial> e.g. trc-1420825016588

cat /etc/hosts | grep trc
# Expect: 127.0.1.1   trc-<serial>
```

> **Why SOM serial:** The serial is unique per NVIDIA module from the factory,
> readable at any time from `/proc/device-tree/serial-number`, and requires no
> manual tracking or labelling scheme. All units share the same IP (192.168.1.22)
> so the hostname is the only software-visible unit identity.

> **Note:** The `sudo: unable to resolve host trc-XXXXXXX` warning that appears
> immediately after setting the hostname is benign — the shell hasn't picked up
> the new hostname yet. It resolves after reboot.

### 1.2 Passwordless sudo

> ⚠️ **Do this before any apt or sudo commands.** Must be first.

```bash
sudo grep -q "ipg ALL=(ALL) NOPASSWD" /etc/sudoers || \
    echo "ipg ALL=(ALL) NOPASSWD: ALL" | sudo tee -a /etc/sudoers
sudo echo "sudo works"
# Expect: sudo works
```

### 1.3 Create directory structure

```bash
mkdir -p ~/CV/SETUP
mkdir -p ~/CV/TRC
ls ~/CV/
# Expect: SETUP  TRC
```

### 1.4 Hold L4T packages

> ⚠️ **Must be done before `apt install nvidia-jetpack` — do not skip or reorder.**

```bash
sudo apt-mark hold \
    nvidia-l4t-bootloader \
    nvidia-l4t-kernel \
    nvidia-l4t-kernel-dtbs \
    nvidia-l4t-kernel-headers \
    nvidia-l4t-core \
    nvidia-l4t-init

# Confirm — must show all 6
apt-mark showhold | grep nvidia-l4t
```

### 1.5 Enable internet sharing and install compute stack

Connect Jetson Ethernet to Windows PC. Enable Windows ICS:
1. `ncpa.cpl` → right-click WiFi adapter → Properties → Sharing
2. Enable sharing → select wired NIC (connected to Jetson)
3. Click OK

Set gateway and test connectivity on Jetson:
```bash
sudo ip route replace default via 192.168.1.208
ping 8.8.8.8 -c 3
# Must succeed before continuing
```

If ping fails, fix DNS:
```bash
echo "nameserver 8.8.8.8" | sudo tee /etc/resolv.conf
ping 8.8.8.8 -c 3
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

> **Note — L4T 36.5.0:** jtop 4.3.2 will show this warning on every launch — it is cosmetic only and can be ignored. jtop is fully functional including the CTRL tab:
> ```
> [WARN] jetson-stats not supported for [L4T 36.5.0]
> ```

### 1.8 Reboot

```bash
sudo reboot
```

> ⚠️ **After every reboot during the build — set gateway before any network activity:**
> ```bash
> sudo ip route replace default via 192.168.1.208
> ```
> The nmtui gateway will be left on `.208` for the duration of the build to maintain
> internet access. It is changed to `.1` permanently at the end of Phase 8.
> Every reboot reverts to `.1` until that change is made — always reset it manually.

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

### 2.3 GStreamer pipeline test

Verify the hardware encode pipeline before proceeding. Start Windows receive first:

```
gst-launch-1.0.exe udpsrc address=192.168.1.208 port=5000 buffer-size=2097152 caps="application/x-rtp,media=video,encoding-name=H264,payload=96" ! rtpjitterbuffer latency=50 drop-on-latency=true ! rtph264depay ! h264parse ! nvh264dec ! videoconvert n-threads=4 ! fpsdisplaysink sync=false text-overlay=true
```

> **Dual-NIC Windows:** If THEIA has multiple NICs, always specify `address=192.168.1.208`
> to bind the receiver to the correct interface. Without it, the stream may arrive on a
> different NIC and not display.

Then on Jetson:
```bash
gst-launch-1.0 videotestsrc is-live=true \
    ! "video/x-raw,width=1280,height=720,framerate=60/1" \
    ! nvvidconv \
    ! "video/x-raw(memory:NVMM),format=NV12" \
    ! nvv4l2h264enc bitrate=10000000 \
    ! h264parse \
    ! rtph264pay config-interval=1 pt=96 \
    ! udpsink host=192.168.1.208 port=5000 sync=false async=false
```

**Pass:** Live video visible on Windows. Ctrl+C to stop.

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

### 3.1 Update gateway for deployment — defer to end of build

> ⚠️ **Do NOT change the gateway now.** Internet access via `.208` is required for
> the remainder of the build (VimbaX download, OpenCV build, apt installs).
> The gateway change from `.208` → `.1` is the final step at the end of Phase 8,
> after all software is installed and verified.

This step is documented here for completeness. Execute it only at the end of Phase 8:

```bash
sudo nmtui
```
- Edit a connection → Wired (`enP8p1s0`)
- Gateway: change from `192.168.1.208` → `192.168.1.1`
- DNS: change from `8.8.8.8` → `192.168.1.33` (NTP appliance also serves DNS)
- Save → Activate a connection → deactivate/reactivate

```bash
ip route show default
# Expect: default via 192.168.1.1 (not 192.168.1.208)
```

### 3.2 NTP configuration

> **Timing note:** TRC handles all timing decisions internally. The OS provides
> NTP-disciplined system time via `systemd-timesyncd`. PTP/PHC reading from the
> NovAtel grandmaster (192.168.1.30) is implemented within the TRC binary as part
> of Task 4 / NEW-38d — no separate PTP client daemon (`linuxptp`, `ptp4l`) is
> needed or installed on the OS.

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

Required for Alvium USB3 camera DMA headroom.

```bash
# Backup first
sudo cp /boot/extlinux/extlinux.conf /boot/extlinux/extlinux.conf.bak

# Check APPEND line before editing — confirm it ends with nv-auto-config
grep "APPEND" /boot/extlinux/extlinux.conf

# Add USB buffer parameter
sudo sed -i 's/nv-auto-config/nv-auto-config usbcore.usbfs_memory_mb=1000/' \
    /boot/extlinux/extlinux.conf

# Verify change applied
grep "usbfs" /boot/extlinux/extlinux.conf
# Must show: usbcore.usbfs_memory_mb=1000
```

Reboot to apply:
```bash
sudo reboot
```

> ⚠️ **After reboot — set gateway before any commands:**
> ```bash
> sudo ip route replace default via 192.168.1.208
> ```

```bash
# IP address
ip addr show enP8p1s0 | grep "inet "
# Expect: inet 192.168.1.22/24

# NTP config
cat /etc/systemd/timesyncd.conf | grep -v "^#" | grep -v "^$"
# Expect: NTP=192.168.1.33, FallbackNTP=192.168.1.208

# USB buffer in kernel args
grep "usbfs" /boot/extlinux/extlinux.conf
# Expect: usbcore.usbfs_memory_mb=1000
```
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

> ⚠️ **After reboot — set gateway before any commands:**
> ```bash
> sudo ip route replace default via 192.168.1.208
> ```

### 4.6 Verify camera detected

> ⚠️ **Plug in Alvium USB3 camera before running this step.**

```bash
/opt/VimbaX_2026-1/bin/ListCameras_VmbCPP
```

> ⚠️ **If you see `libVmbCPP.so: file too short`:** A stray `libVmbCPP.so` file in the
> home directory is shadowing the real library. Check and remove:
> ```bash
> find /home/ipg -name "libVmbCPP.so" 2>/dev/null
> rm -f ~/libVmbCPP.so
> ```
> Then retry. This can be caused by earlier failed symlink attempts.

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
Two steps — videotestsrc first, then live Alvium.

**Receive (Windows) — start this first for both tests:**
```
gst-launch-1.0.exe udpsrc address=192.168.1.208 port=5000 buffer-size=2097152 caps="application/x-rtp,media=video,encoding-name=H264,payload=96" ! rtpjitterbuffer latency=50 drop-on-latency=true ! rtph264depay ! h264parse ! nvh264dec ! videoconvert n-threads=4 ! fpsdisplaysink sync=false text-overlay=true signal-fps-measurements=true
```

> **Dual-NIC Windows:** Specify `address=192.168.1.208` to bind to the correct NIC.

**Test 1 — videotestsrc (no camera required):**
```bash
gst-launch-1.0 videotestsrc is-live=true \
    ! "video/x-raw,width=1280,height=720,framerate=60/1" \
    ! nvvidconv \
    ! "video/x-raw(memory:NVMM),format=NV12" \
    ! nvv4l2h264enc bitrate=10000000 \
    ! h264parse \
    ! rtph264pay config-interval=1 pt=96 \
    ! udpsink host=192.168.1.208 port=5000 sync=false async=false
```

**Pass:** Test pattern visible on Windows. Ctrl+C to stop.

**Test 2 — Live Alvium camera — substitute camera ID from ListCameras output:**
```bash
gst-launch-1.0 -v vmbsrc camera=DEV_1AB22C0xxxxx \
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

> **Note — iris:** The Alvium iris may be closed on first use — image will appear
> dark or black. This is normal and not a pipeline failure. The iris opens once
> TRC initialises the camera with its full configuration at runtime.

**Pass:** Live H.264 video visible on Windows (may be dark — see iris note). ✅ Confirmed working 2026-04-09.

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

# Create unified test script
cat > ~/CV/SETUP/test1.py << 'EOF'
import cv2
import numpy as np
import sys
print("PYTHON VS:       " + sys.version)
print("NUMPY VS:        " + np.__version__)
print("OPENCV VS:       " + cv2.__version__)
print("CV2 path:        " + cv2.__file__)
print("CUDA DNN targets:", cv2.dnn.getAvailableTargets(cv2.dnn.DNN_BACKEND_CUDA))
print("CUDA devices:    ", cv2.cuda.getCudaEnabledDeviceCount())
EOF

python3 ~/CV/SETUP/test1.py
# Expect:
#   OPENCV VS: 4.13.0
#   CV2 path: /usr/local/lib/python3.10/dist-packages/cv2/__init__.py
#   CUDA DNN targets: [6, 7]
#   CUDA devices: 1

pkg-config --modversion opencv4
# Expect: 4.13.0
```

**All pass → proceed to Phase 6.**

---

## Phase 6 — TRC Build

### 6.1 Get TRC source

```bash
# Transfer from Windows host:
scp -r /path/to/TRC/* ipg@192.168.1.22:~/CV/TRC/
```

### 6.2 Update Makefile VimbaX path

```bash
cd ~/CV/TRC
grep "VIMBAX_DIR" Makefile
# Check the output before proceeding:
# If showing VimbaX_2025-1 → run the sed below
# If already showing VimbaX_2026-1 → skip the sed, proceed to 6.3
```

Only run if Makefile still shows `2025-1`:
```bash
sed -i 's|VimbaX_2025-1|VimbaX_2026-1|g' Makefile
grep "VIMBAX_DIR" Makefile
# Expect: VIMBAX_DIR := /opt/VimbaX_2026-1
```

### 6.3 Build

```bash
make clean && make -j$(nproc) 2>&1 | tee ~/CV/TRC/trc_build_$(date +%Y%m%d).log
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
ls ~/CV/TRC/model_data/
# Must show:
#   frozen_inference_graph.pb
#   ssd_mobilenet_v3_large_coco_2020_01_14.pbtxt
#   coco.names
```

If missing, copy from known-good source.

---

### ✅ CHECKPOINT 6 — TRC Build

```bash
cd ~/CV/TRC

# Binary exists
ls -lh trc

# Correct library links
ldd ./trc | grep -E "opencv_dnn|VmbC|Vmb"
# libopencv_dnn.so.413 must be from /usr/local/lib/
# libVmbCPP must be from /opt/VimbaX_2026-1/

# Version string
./trc --version
# Expect: TRC 4.x.x <date> <time> — version will match current TRC source build

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

### 7.1 Transfer launch and setup scripts

Transfer all scripts to the Jetson in one step:
```bash
# From Windows host:
scp trc_start.sh trc_start_bench.sh ipg@192.168.1.22:~/CV/TRC/
scp 04_verify_all.sh cleanup_pre_image.sh ipg@192.168.1.22:~/CV/SETUP/
```

On Jetson:
```bash
chmod +x ~/CV/TRC/trc_start.sh
chmod +x ~/CV/TRC/trc_start_bench.sh
chmod +x ~/CV/SETUP/04_verify_all.sh
chmod +x ~/CV/SETUP/cleanup_pre_image.sh
```

> ⚠️ **Windows line endings — apply to ALL transferred scripts without exception:**
> Scripts transferred from Windows via SCP contain `\r\n` line endings which cause
> `/bin/bash^M: bad interpreter` errors on Linux. Fix all scripts after every SCP transfer:
> ```bash
> sed -i 's/\r//' ~/CV/TRC/trc_start.sh
> sed -i 's/\r//' ~/CV/TRC/trc_start_bench.sh
> sed -i 's/\r//' ~/CV/SETUP/04_verify_all.sh
> sed -i 's/\r//' ~/CV/SETUP/cleanup_pre_image.sh
> sed -i 's/\r//' ~/CV/SETUP/install_opencv4.13.0_Jetpack6.2.2.sh
> ```
> This applies to every `.sh` file transferred from Windows — run the sed before `chmod +x`, every time.

### 7.2 Configure bench autostart — gold standard for image

The **bench script is the gold standard for the production image**. Units boot
into bench mode by default. The switch to production (`trc_start.sh`) is a
post-deployment step done on-site, not baked into the image.

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

### 7.3 Switch to production autostart — POST-DEPLOYMENT ONLY

> ⚠️ **Do not do this before imaging.** The bench crontab is the gold standard
> for the image. Switch to production on-site after the unit is installed and
> the MWIR camera and multicast network are confirmed available.

On the deployed unit:

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

tail -20 ~/CV/TRC/trc_bench.log
# Must show TRC startup messages, no fatal errors
```

**Confirm stream active — start Windows GStreamer receiver and confirm video visible:**
```
gst-launch-1.0.exe udpsrc address=192.168.1.208 port=5000 buffer-size=2097152 caps="application/x-rtp,media=video,encoding-name=H264,payload=96" ! rtpjitterbuffer latency=50 drop-on-latency=true ! rtph264depay ! h264parse ! nvh264dec ! videoconvert n-threads=4 ! fpsdisplaysink sync=false text-overlay=true signal-fps-measurements=true
```
**Pass:** Live video visible on Windows at `192.168.1.208:5000`.

**All pass → proceed to Phase 8.**

---

## Phase 8 — Full System Verification Gate

### 8.1 Remove desktop environment and LibreOffice

Final pre-imaging step — do this last, after all software is installed and verified.
Reduces attack surface and memory footprint.

```bash
sudo systemctl stop gdm3
sudo systemctl disable gdm3
sudo systemctl set-default multi-user.target
sudo apt remove --purge ubuntu-desktop gdm3 -y
sudo apt purge libreoffice* -y
sudo apt autoremove -y
sudo reboot
```

After reboot unit boots to console only. Confirm SSH and TRC autostart still work:
```bash
# After reboot — wait ~60s
ssh ipg@192.168.1.22
ps aux | grep trc | grep -v grep
# Must show trc running
```

### 8.2 Pre-image cleanup

Run the cleanup script to strip build artifacts, source files, and installer debris
from the image. This produces a minimal, clean production image.

```bash
cd ~/CV/SETUP/
./cleanup_pre_image.sh
```

**cleanup_pre_image.sh contents:**

```bash
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
# Remove stray start scripts — these belong in ~/CV/TRC/ not SETUP/
rm -f  ~/CV/SETUP/trc_start.sh
rm -f  ~/CV/SETUP/trc_start_bench.sh

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
```

Save and make executable:
```bash
chmod +x ~/CV/SETUP/cleanup_pre_image.sh
```

**Expected survivors after cleanup:**

| Location | Files |
|---|---|
| `~/CV/SETUP/` | `04_verify_all.sh`, `cleanup_pre_image.sh`, `test1.py`, `jetson_verified_*.txt` |
| `~/CV/TRC/` | `trc`, `trc_start.sh`, `trc_start_bench.sh`, `trc_bench.log`, `model_data/` |
| `/opt/` | `VimbaX_2026-1/` only — no installer tarball |

### 8.3 Final verification — two gates

> ⛔ **Gate A MUST run BEFORE cleanup (§8.2). Gate B runs AFTER cleanup.**
> Running cleanup before Gate A means you will never get a 54 PASS baseline record for this unit.
> Do not proceed to §8.2 until Gate A shows 54 PASS, 0 FAIL.

> ⚠️ **Before running Gate A — verify the TRC version check in `04_verify_all.sh` matches the current TRC major version.**
> The script checks for a TRC version string prefix (e.g. `"TRC 4"`). If the TRC major version has changed since the last build, update the script first or Gate A will show 1 FAIL on the version check:
> ```bash
> grep "TRC " ~/CV/SETUP/04_verify_all.sh | grep -i "version\|expect"
> # Confirm it matches current TRC major version — e.g. "TRC 4"
> # If not, update:
> sed -i 's/"TRC 3"/"TRC 4"/' ~/CV/SETUP/04_verify_all.sh
> ```

Run the verify script **twice** at different stages. Both results are expected and correct:

**Gate A — Pre-cleanup (run BEFORE 8.2):**
```bash
cd ~/CV/SETUP/
./04_verify_all.sh
```
Expected: **54 PASS, 0 FAIL** — Makefile present, all checks pass.

> ⚠️ **If Gate A does not show 54 PASS, 0 FAIL — do not proceed to cleanup. Resolve all failures first.**

**Then run §8.2 cleanup.**

**Gate B — Post-cleanup (run AFTER 8.2):**
```bash
cd ~/CV/SETUP/
./04_verify_all.sh
```
Expected: **53 PASS, 1 FAIL** — the Makefile check fails because the Makefile
was intentionally removed by cleanup. This is correct pre-image state.

> ⚠️ **The single FAIL after cleanup is expected and required.** It confirms
> cleanup ran. Do not attempt to fix it. The equivalent check — that TRC
> actually links against the correct VimbaX — passes via the ldd check above it.
> Gate B (53 PASS, 1 expected FAIL) is the imaging gate.

The log is saved to `~/CV/SETUP/jetson_verified_YYYYMMDD_HHMMSS.txt` — keep the
Gate B log as the definitive pre-image baseline record.

The unit is now ready for inventory and deployment. Shut down cleanly:
```bash
sudo poweroff
```

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
| Makefile | `VIMBAX_DIR := /opt/VimbaX_2026-1` | Gate A only — removed by cleanup. Gate B: this check fails (expected). |
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

> ⚠️ **DO NOT use `dd` or `l4t_backup_restore.sh` for Jetson cloning.**
> Raw `dd` copies the NVMe but not the QSPI bootloader partition. The restored unit
> will fail to boot (HTTP boot / "no bootable device") because the QSPI on the target
> unit contains different partition UUIDs than the source NVMe. `l4t_backup_restore.sh`
> has the same limitation and also requires initrd mode which is unreliable without
> a serial console.
>
> **The correct tool is `l4t_initrd_flash.sh` with `--massflash`.** This is NVIDIA's
> supported cloning method — it captures and restores both QSPI and NVMe correctly.
> This procedure is pending validation. Until validated, new units use **Path A**
> (fresh build from scratch).

**Validated imaging procedure (pending — use Path A until confirmed):**

```bash
# On Ubuntu host PC — put source unit in recovery mode first, then:
cd ~/nvidia/nvidia_sdk/JetPack_6.2.2_Linux_JETSON_ORIN_NX_TARGETS/Linux_for_Tegra/

# Step 1 — Generate massflash package from source unit
sudo ./tools/kernel_flash/l4t_initrd_flash.sh \
    --generate-massflash-package \
    --network usb0 \
    p3509-a02-p3767-0000 nvme0n1

# Step 2 — Flash target unit from massflash package
# Put target unit in recovery mode, then:
sudo ./tools/kernel_flash/l4t_initrd_flash.sh \
    --massflash 1 \
    --network usb0 \
    p3509-a02-p3767-0000 nvme0n1
```

> **Ubuntu host disk space:** Requires ≥50GB free. Remove unused JetPack versions
> (e.g. JetPack 6.2.1) before generating massflash package:
> ```bash
> sudo rm -rf ~/nvidia/nvidia_sdk/JetPack_6.2.1_Linux*
> df -h ~  # Confirm ≥50GB free
> ```

### 9.3 Restore image to new unit

Post-restore — set hostname from SOM serial (IP does not need to change):

```bash
SERIAL=$(cat /proc/device-tree/serial-number | tr -d '\0')
sudo hostnamectl set-hostname trc-${SERIAL}
sudo sed -i "s/trc-<source-unit-serial>/trc-${SERIAL}/g" /etc/hosts
hostname

# Verify
cd ~/CV/SETUP/
./04_verify_all.sh
# Expect: 53 PASS, 1 expected FAIL (Makefile)
```

---

## Fleet Registry

All deployed TRC units. Update this table when a unit is built, upgraded, or retired.

> **SOM serial** is read from `/proc/device-tree/serial-number` — unique per NVIDIA module from factory.
> **Hostname** is `trc-<full SOM serial>`. **IP** is always `192.168.1.22` (role address).

| Hostname | SOM Serial | TRC Version | JetPack | Path | Date | Location | Notes |
|---|---|---|---|---|---|---|---|
| `trc-1420825016588` | 1420825016588 | 3.0.2 | 6.2.2 | A | 2026-04-09 | — | Unit 1 |
| `trc-1423624314616` | 1423624314616 | 3.0.2 | 6.2.2 | A | 2026-04-09 | — | Unit 2 |
| `trc-1420825019234` | 1420825019234 | 3.0.2 | 6.2.2 | A | 2026-04-10 | — | Unit 3 |
| `trc-1420825020919` | 1420825020919 | 4.0.1 | 6.2.2 | A | 2026-04-13 | — | Unit 4 |
| `trc-1420825016537` | 1420825016537 | 4.0.1 | 6.2.2 | A | 2026-04-13 | — | Unit 5 |
| `trc-1423624314071` | 1423624314071 | 4.1.2 | 6.2.2 | C | 2026-04-13 | I1 | Path C validated (4.4→5.0) |
| `trc-1420825013697` | 1420825013697 | 4.0.1 | 6.2.2 | C | 2026-04-13 | — | Path C validated (4.7→5.0) |
| `trc-1420825019951` | 1420825019951 | 4.1.2 | 6.2.2 | C | 2026-04-15 | P2 | Path C (4.7→5.0) |
| `trc-1420825020046` | 1420825020046 | 4.0.2 | 6.2.2 | C | 2026-04-17 | — | Path C (4.4→5.0) |
| `trc-1420825022024` | 1420825022024 | 4.0.2 | 6.2.2 | C | 2026-04-17 | Home Lab | Path C (4.4→5.0) |
| `trc-1422124347828` | 1422124347828 | 4.0.2 | 6.2.2 | C | 2026-04-18 | — | Path C validated (4.3→5.0) |
| `trc-1420825018548` | 1420825018548 | 4.0.3 | 6.2.2 | A | 2026-04-19 | Inventory | Path A — was JetPack 5.x (R35), reflashed. Tracker WIP |
| `trc-1423624313027` | 1423624313027 | 4.1.2 | 6.2.2 | A | 2026-04-24 | Inventory | Path A — was JetPack 5.x (R35), reflashed. Tracker WIP |
| `trc-1420825014551` | 1420825014551 | 4.1.3 | 6.2.2 | A | 2026-04-24 | Inventory | Path A — fresh unit. |

> **`04_verify_all.sh` TRC version check:** The script checks for a TRC version string prefix (e.g. `"TRC 4"`). After any TRC major version change, update the check in the script and push the updated script to all units:
> ```bash
> sed -i 's/"TRC 3"/"TRC 4"/' ~/CV/SETUP/04_verify_all.sh
> ```
> The updated `04_verify_all.sh` should be included in the standard file transfer (Phase 7.1) so new units always have the correct check from the start.

---

## Known Issues and Notes

| Issue | Notes |
|-------|-------|
| `System clock synchronized: no` | Expected indoors — `.33` requires GPS sky view outdoors |
| RTC at epoch zero | Expected on first power-on — resolves after first NTP sync |
| VimbaX path in Makefiles | Archive versions intentionally left at 2025-1. TRC path is now ~/CV/TRC — no archive paths. |
| gst-vmbsrc | NOT used operationally. TRC accesses Alvium directly via VimbaX API. Installed for diagnostic GStreamer pipeline testing only — removed by cleanup_pre_image.sh. |
| Super Mode | NOT supported on J4012 non-Super (J401 carrier). MAXN is the ceiling. Super J4012 (new carrier) supports it but requires a separate procedure — do not mix images between variants. |
| `OPENCV_DNN_CUDA` not in getBuildInformation() | Normal for OpenCV 4.11.x+. Confirm via cmake cache or getAvailableTargets(). |
| overlayFS and permanent changes | Must use `overlayroot-chroot` to make changes that survive reboot. |
| `libVmbCPP.so: file too short` | Stray `libVmbCPP.so` in `~/` shadows real library. Run `find /home/ipg -name "libVmbCPP.so"` and `rm -f ~/libVmbCPP.so`. Can be caused by failed symlink attempts during Phase 4 troubleshooting. |
| `dd` / `l4t_backup_restore.sh` clone failure | Raw dd copies NVMe only — QSPI bootloader not transferred. Target unit boots to HTTP boot / "no bootable device". Use `l4t_initrd_flash.sh --massflash` for cloning (Phase 9). Until validated, new units use Path A. |

---

## Version History

| Version | Date | Notes |
|---------|------|-------|
| 2.3.3 | 2026-04-24 | §0.3 Boot 1 split into Boot 1A (EULA, no Chromium) and Boot 1B (reject Canonical/Ubuntu Pro prompts) — two sub-steps now explicit before Boot 2. §6 Checkpoint: TRC version string expected output updated from `TRC 3.0.2` to `TRC 4.x.x` — version matches current TRC source build, not hardcoded. §8.3: added explicit pre-Gate A step to verify and update `04_verify_all.sh` TRC version check before running Gate A (prevents false FAIL on version string mismatch). Fleet Registry: `trc-1420825014551` added (Unit 14, TRC 4.1.3, Path A, Inventory). |
| 2.3.2 | 2026-04-24 | Added FOLLOW EXACTLY instruction block at top. §0.4: consolidated all SDK Manager settings (including OEM config, username, Pre-config, NVMe, deselect ALL SDK and runtime components) into single pre-flash checklist. Phase 1 reordered: sudo (1.2) → hold (1.4) → nvidia-jetpack (1.5) → PATH → packages → jtop. Passwordless sudo updated to use tee method (no visudo). Added hostname `sudo: unable to resolve host` warning as expected. §1.7: jtop L4T 36.5.0 warning documented as cosmetic. §1.8 and all reboot steps: post-reboot gateway reminder added. §3.1: deferred to end of Phase 8 — gateway stays on .208 for entire build. §6.2: check Makefile before sed — skip if already 2026-1. §7.1: `sed -i 's/\r//'` now explicitly required for ALL transferred scripts before chmod. Checkpoint 7: tcpdump replaced with Windows GStreamer stream verification. §8.3: Gate A BEFORE cleanup — explicit warning added. Gateway change removed from build close-out — set on-site at deployment only. Fleet Registry: `trc-1420825018548` location updated to Inventory; `trc-1423624313027` added (Unit 13, Path A, TRC 4.1.2, Inventory). `04_verify_all.sh` TRC version check note: update from TRC 3 → TRC 4. |
| 2.3.1 | 2026-04-19 | TRC 4.1.0 introduced. Makefile note: new source transfers from Windows may reset VIMBAX_DIR to `VimbaX_2025-1` — always run `sed -i 's|VimbaX_2025-1|VimbaX_2026-1|g' Makefile` after transfer. Scripts need `chmod +x` after transfer (both `trc_start.sh` and `trc_start_bench.sh`). Hot-reload procedure: `pkill trc_start_bench.sh && pkill ./trc` → fix line endings → rebuild → restart. |
| 2.3.0 | 2026-04-19 | Fleet Registry: `trc-1420825018548` added — JetPack 5.x (R35) unit, required full Path A reflash. TRC 4.0.3 introduced. R35 warning added to Path C. |
| 2.2.9 | 2026-04-18 | Path C validated on REVISION 4.3 → 5.0. Fleet Registry: `trc-1422124347828` added (11 units). |
| 2.2.8 | 2026-04-17 | Fleet Registry: `trc-1420825022024` added. Path C Step 1b: gateway before ping. Step 1c: passwordless sudo. Step 1d: clean CV directory. |
| 2.2.7 | 2026-04-17 | Fleet Registry section added. `04_verify_all.sh` TRC version check note added. |
| 2.2.6 | 2026-04-15 | Path C Step 1: clock sync verification added. Path C Step 4: REVISION 4.7 note. Path C Step 5: split 4.4/4.7 paths. Phase 2.3 and 4.8: Windows GStreamer `address=` parameter for dual-NIC. |
| 2.2.5 | 2026-04-15 | Path C Step 6: long boot warning added. Phase 4.8: two-step pipeline test and iris note added. |
| 2.2.4 | 2026-04-13 | Path C reinstated — validated in-place apt upgrade 6.2.1 → 6.2.2. Path B updated. Phase 8.2 cleanup: stray trc_start scripts removal added. |
| 2.2.3 | 2026-04-13 | Phase 0.3: two-boot sequence documented. Phase 7.1: Windows line endings fix added. |
| 2.2.2 | 2026-04-13 | Phase 4.6: `libVmbCPP.so: file too short` fix documented. Phase 8.2: `test1.py` kept as survivor. Phase 9.2/9.3: rewritten — dd/l4t_backup_restore NOT supported for cloning. Known issues: two new entries. |
| 2.2.1 | 2026-04-10 | Phase 7.1: `04_verify_all.sh` and `cleanup_pre_image.sh` added to file push step. Phase 7.3: marked POST-DEPLOYMENT ONLY. Checkpoint 7: corrected log reference and tcpdump target. Phase 8.2: `gst-vmbsrc/` directory added to cleanup script. Pass criteria table: Makefile row annotated with Gate A/B caveat. |
| 2.2.0 | 2026-04-10 | Path C retired — upgrade path is Path B (image restore). Path B updated to cover upgrades. Path D: Super units share same TRC role IP. IP 192.168.1.22 documented as TRC role address shared by all units. Hostname scheme: `trc-<SOM serial>` from `/proc/device-tree/serial-number` — Phase 1.1a added. Phase 8 restructured: 8.1 desktop removal, 8.2 cleanup script (`cleanup_pre_image.sh`), 8.3 two-gate verify (54 PASS pre-cleanup / 53 PASS + 1 expected FAIL post-cleanup). Bench crontab documented as gold standard for image. |
| 1.0.0 | 2026-04-09 | Initial — based on live baseline verification of lab Jetson 2026-04-06 |

---

*See also: OPENCV_BUILD_HISTORY.md, TOMORROW_SESSION_PLAN.md, ARCHITECTURE.md §2.5*
