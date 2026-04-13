# IPG Crossbow Uploader — Operator's Manual

> Tool: `frmFWProgrammer` (.NET 8 WinForms)
> Target hardware: TMC, BDC, MCC custom PCBs (STM32F7 series MCU)
> Also supports: SAMD-based boards (LOA) via bossac, Jetson tracker via SSH/SFTP

---

## Theory of Operation

### System Overview

The IPG Crossbow Uploader is a Windows desktop tool that consolidates firmware deployment and system health monitoring for the three embedded subsystems that make up the Crossbow platform: the **STM32-based controller boards** (TMC, BDC, MCC), the **SAMD-based LOA board**, and the **Jetson-based tracker computer** (TRC). Each subsystem uses a different programming or deployment path, but all share a common status bar that provides at-a-glance system state after any operation.

```
┌─────────────────────────────────────────────────────────┐
│              IPG Crossbow Uploader (WinForms)            │
│                                                          │
│   ┌──────────────────┐     ┌──────────────────────────┐ │
│   │   STM32 Tab       │     │      Tracker Tab          │ │
│   │  COM port query  │     │  SSH/SFTP to 192.168.1.22 │ │
│   │  opencr_ld flash │     │  Ping / Query / Upload    │ │
│   │  bossac flash    │     │  Restart / Reboot         │ │
│   └──────────────────┘     └──────────────────────────┘ │
│                                                          │
│   ─────────────── Status Bar ───────────────────────── │
│   [ Prog tool ] [ Msg ] [ Ctrl ] [ Ver ] [ IP ] [ ● ]   │
└─────────────────────────────────────────────────────────┘
         │ USB-Serial                    │ Ethernet
         ▼                               ▼
  ┌─────────────┐               ┌────────────────┐
  │ TMC/BDC/MCC │               │  Jetson TRC    │
  │ STM32F7     │               │  192.168.1.22  │
  └─────────────┘               └────────────────┘
         │ USB-Serial
         ▼
  ┌─────────────┐
  │   LOA       │
  │   SAMD      │
  └─────────────┘
```

---

### STM32 Controller Boards (TMC, BDC, MCC)

The TMC (Thermal Management Controller), BDC (Beam Director Controller), and MCC (Mission Control Computer) are custom PCBs built around the STM32F7 series microcontroller and programmed via the Arduino/OpenCR toolchain. Each board exposes a USB-serial port at 115200 baud 8N1 that serves two purposes: firmware programming and runtime command/query.

**Firmware Programming — `opencr_ld.exe`**

Programming is handled by `opencr_ld.exe`, a command-line tool from the OpenCR/OpenManipulator ecosystem adapted for STM32F7 targets. When invoked, it communicates with the STM32 ROM bootloader over the selected COM port at 115200 baud, erases the application flash region, transfers the binary, verifies, and resets the MCU back into application firmware. The tool is bundled in the `tools\` subfolder and requires no separate installation.

The programmer is invoked as:
```
opencr_ld.exe <COM_PORT> 115200 <PATH_TO_BIN> 1
```

**Runtime Query — INFO Protocol**

While running application firmware, all STM32 boards respond to a simple ASCII command protocol over the same USB-serial port. The tool sends `info\r` and reads back a response delimited by `###` fences:

```
###########################################################################
BDC  v3.0.0  built Mar 11 2026 15:54:56
IP:            192.168.1.20
Link:          UP
...
###########################################################################
```

The tool reads until the closing fence is detected or a 2-second timeout expires, then parses the response to extract controller name, firmware version, build date, IP address, and Ethernet link status — all reflected in the status bar. The COM port is opened only for the duration of the query and closed immediately after, so it is never held open between operations.

**COM Port Monitoring**

A 500ms background timer continuously polls `SerialPort.GetPortNames()`. When a new device appears the dropdown updates automatically; when a device is removed the dropdown clears, the text area and status bar reset, and the Query and Program buttons disable until a port is available again.

---

### SAMD LOA Board

The LOA board uses a SAMD microcontroller (Arduino Zero / M0 family) and is programmed via `bossac.exe`, the standard SAM Boot Assistant command-line flasher. Unlike the STM32, the SAMD does not enter its bootloader on command — it must be triggered by a specific USB event.

**1200-Baud Touch**

The SAMD ROM bootloader is activated by briefly opening and closing the USB-serial port at exactly 1200 baud. The firmware running on the MCU monitors for this specific baud rate and, upon detecting it, hands off execution to the ROM bootloader. The board then re-enumerates on USB as a bootloader device — potentially on a different COM port number.

The tool performs the touch sequence automatically:
1. Opens the selected COM port at 1200 baud
2. Holds for 5 seconds
3. Closes the port
4. Waits a further 5 seconds for bootloader enumeration
5. Invokes `bossac.exe` with erase, write, verify, and auto-reset flags

The `-R` flag passed to `bossac.exe` resets the MCU back into application firmware immediately after a successful flash, so no manual power cycle is required.

---

### Jetson Tracker Computer (TRC)

The Jetson is a standalone Linux computer running the `trackCntrl` C++ application, accessed over Ethernet at the fixed IP address `192.168.1.22`. The tool communicates with it via two protocols: **ICMP ping** for reachability, **SSH** for process management and version queries, and **SFTP** for binary deployment.

**Reachability — ICMP Ping**

Before any SSH or SFTP operation, the tool pings `192.168.1.22` to confirm the Jetson is reachable. The result is shown in the list box and the status bar link dot updates immediately.

**Process Management — SSH**

The tool connects via SSH (port 22) to inspect and control the `trackCntrl.exe` process. The Query operation runs `ps aux | grep trackCntrl.exe` to find the process ID, sends `SIGINT` to stop it cleanly if running, then re-checks after 500ms to confirm the process has exited. This stop step is required before uploading a new binary since Linux will not allow overwriting an executable that is currently mapped into a running process.

The tracker version is retrieved by running the binary directly with the `--version` flag:
```bash
/home/ipg/CV/TRC2/trackCntrl.exe --version
```
The response format is `trackCntrl v2.1.0` — matching the STM32 INFO protocol pattern.

**Binary Deployment — SFTP**

New tracker binaries are uploaded via SFTP directly to the deployment path `/home/ipg/CV/TRC2/trackCntrl.exe`, overwriting the existing binary in place. After upload, file permissions are set to `744` to ensure the binary is executable by the `ipg` user. Upload progress is streamed to the status bar in KB transferred. A secondary SSH connection immediately reads back the version from the newly uploaded binary to confirm a successful deployment.

**Restart and Reboot**

After upload the tracker is started via the system startup script `~/ben.sh`, launched with `nohup` so it continues running after the SSH session closes. A full system reboot via `exec sudo reboot now` is also available — this command causes the SSH connection to drop immediately, which is the expected behavior.

---

### Status Bar — Shared Across Both Tabs

The status bar is shared between the STM32 and Tracker tabs and always reflects the result of the most recent operation, regardless of which tab performed it. This means switching tabs does not clear the status — the last known state persists until the next query or operation updates it.

| Field | STM32 Tab | Tracker Tab |
|---|---|---|
| Programmer | `opencr_ld.exe` or `bossac.exe` | unchanged |
| Message | `Query OK`, `Burning`, `Done`, etc. | `TRC: running`, `TRC: not running`, `TRC: unreachable` |
| Controller | `TMC`, `BDC`, `MCC`, etc. | `TRC` |
| Version | `v3.0.0  Mar 11 2026` | `v2.1.0` |
| IP | Controller Ethernet IP | `192.168.1.22` |
| ● Link | STM32 Ethernet link UP/DOWN | Jetson ICMP reachable/unreachable |

---



Before using any section of the tool:

1. Copy the application folder to the host PC. The `tools\` subfolder must be present alongside the executable and contain `opencr_ld.exe` (STM32) and `bossac.exe` (SAMD). A warning dialog at startup will indicate if either is missing.
2. Connect the target board via USB-serial. The COM port dropdown populates automatically and updates in real time as devices are connected and disconnected — no restart required.
3. The baud rate is fixed at **115200** for both query and programming operations.

---

## Status Bar

The status bar at the bottom of the form provides at-a-glance information about the last queried controller:

| Field | Description |
|---|---|
| Left label | Programmer tool currently selected (`opencr_ld.exe` or `bossac.exe`) |
| Message label | Current operation status (`Query OK`, `Burning`, `Done`, `No COM ports found`, etc.) |
| Controller | Name of the last queried controller: `TMC`, `BDC`, `MCC`, etc. |
| Version | Firmware version and build date: e.g. `v3.0.0  Mar 11 2026` |
| IP | Controller IP address: e.g. `192.168.1.20` |
| ● Link | Ethernet link status dot — **🟢 Lime** = UP, **🔴 Tomato** = DOWN, **⚫ Gray** = not queried |

All status bar fields reset to `---` when the COM port is disconnected.

---

## Section 1 — STM32 (TMC / BDC / MCC) via OpenCR / `opencr_ld.exe`

This is the primary programming path for STM32F7-based boards. The tool shells out to `opencr_ld.exe`, which handles the STM32 bootloader handshake and binary transfer over the selected COM port.

### COM Port Selection

The COM port dropdown auto-populates on startup and refreshes every 500ms. When a USB-serial device is connected the port appears automatically; when disconnected the dropdown clears, the text box clears, the status bar resets, and the Query and Program buttons are disabled until a port is available again.

### Configuring the programmer path

The STM32 programmer (`opencr_ld.exe`) is bundled in the `tools\` folder alongside the application executable and resolved automatically at startup. No installation or manual path configuration is required.

Click the **STM32 (MCC, TMC, BDC)** radio button to select this mode. The status bar left label shows the resolved tool filename (`opencr_ld.exe`) confirming it was found, or **`opencr_ld.exe NOT FOUND`** if the `tools\` folder is missing or incomplete. If the tool is not found, a warning dialog appears at startup with the expected path.

### Querying the firmware version

Use this to confirm which firmware is running on a board before or after a flash.

1. Select the correct COM port and ensure the board is powered.
2. Click **Query**. The tool opens the serial port, sends `info\r`, reads until the closing `###` fence is received (up to 2 second timeout), then closes the port.
3. The full INFO response appears in the text area and the status bar updates with controller name, version, IP, and link status.

A successful response looks like:
```
###########################################################################
BDC  v3.0.0  built Mar 11 2026 15:54:56
IP:            192.168.1.20
Link:          UP
A1 listen:     port 10019  (RX FMC/TRC/MCC  TX TRC)
  FMC (.23):   alive=YES  last=9 ms ago
  TRC (.22):   alive=NO  last=1501415 ms ago
  MCC (.10):   alive=YES  last=6 ms ago
A2 listen:     port 10018  clients: 0 / 4
A3 listen:     port 10050  clients: 0 / 2
Free RAM:      200460 bytes
###########################################################################
```

If the text area is empty or the response looks truncated, the board may still be in its 10-second startup delay — wait and retry.

**Available serial commands (via a serial terminal at 115200 8N1):**

| Command | Description |
|---|---|
| `?` or `HELP` | List all available commands |
| `INFO` | Firmware version, IP address, Ethernet link status |
| `REG` | Full REG1 register dump — all 64 data fields with byte offsets |
| `STATUS` | System state, gimbal mode, all status bits decoded |
| `TEMPS` | All thermistor readings + BME280 ambient + MCU die temp |
| `FLOWS` | Flow sensor 1 and 2 readings in LPM + error flags |
| `LCM` | LCM1/LCM2 speeds (DAC counts), current readbacks, enable states |
| `VICOR` | Vicor power rail enable states (LCM1, LCM2, PUMP, HEAT) |
| `NTP` | NTP sync status and current UTC epoch time |
| `NTPIP <a.b.c.d>` | Override NTP server IP at runtime (not persisted across reboot) |
| `DEBUG <0–3>` | Set firmware debug verbosity: 0=OFF, 1=MIN, 2=NORM, 3=VERBOSE |

**ICD mirror commands** (same effect as framed UDP — useful for bench testing without MCC):

| Command | Description |
|---|---|
| `STATE <n>` | Set system state: 0=OFF, 1=STNDBY, 2=ISR, 3=COMBAT, 4=MAINT, 5=FAULT |
| `MODE <n>` | Set gimbal mode: 0=OFF, 1=POS, 2=RATE, 3=CUE, 4=ATRACK, 5=FTRACK |
| `TEMP <10–40>` | Set chiller target temperature in °C (firmware clamps to [10, 40]) |
| `FAN <ch> <spd>` | Set input fan speed: ch=0 or 1, spd=0=OFF / 1=LO / 2=HI |
| `VICOR <ch> <en>` | Enable/disable Vicor: ch=0=LCM1, 1=LCM2, 2=PUMP, 3=HEAT; en=0 or 1 |
| `LCM <ch> <en>` | Enable/disable LCM: ch=0=LCM1, 1=LCM2; en=0 or 1 |
| `DAC <ch> <val>` | Set DAC output: ch=0=LCM1, 1=LCM2, 2=PUMP; val=0–4095 |
| `PUMP <val>` | Shorthand for setting pump DAC directly; val=0–4095 |

**Hardware-level debug commands** (handled in `TMC.ino`):

| Command | Description |
|---|---|
| `SET <pin>,<value>` | Raw `analogWrite` to any pin — for hardware bring-up only |
| `READ <pin>` | Raw `analogRead` from any pin |
| `MCUADC` | Print raw ADC value, calibrated temp, and typical temp for the STM32 internal sensor |

### Programming (flashing) the STM32 firmware

1. Ensure the **STM32 (MCC, TMC, BDC)** radio button is selected.
2. Select the correct COM port from the dropdown.
3. Click **Program**. A file dialog opens filtered to `.bin` files. Navigate to your compiled binary (e.g. `TMC.ino.bin` from the Arduino build output).
4. A confirmation dialog displays the selected COM port and file path — click **Yes** to proceed or **No** to cancel.
5. The form remains responsive during programming. The status bar shows `Burning...` while `opencr_ld.exe` runs.
6. The output text area displays stderr (errors) followed by stdout (progress). A successful flash ends with an exit code of 0. The status bar shows `Done` when complete.

**Arguments passed to `opencr_ld.exe`:**
```
opencr_ld.exe <COM_PORT> 115200 <PATH_TO_BIN> 1
```

---

## Section 2 — SAMD via bossac

This path targets SAMD-based boards (e.g. Arduino Zero / M0 family) using `bossac.exe`. The SAMD bootloader is entered via a "1200-baud touch" — a brief open/close of the serial port at 1200 baud which triggers the MCU to reset into its ROM bootloader.

### Configuring the programmer path

The SAMD programmer (`bossac.exe`) is bundled in the `tools\` folder alongside the application executable and resolved automatically at startup.

Click the **SAMD (LOA)** radio button to select this mode. The status bar left label shows the resolved tool filename (`bossac.exe`) confirming it was found, or **`bossac.exe NOT FOUND`** if the `tools\` folder is missing or incomplete.

### Querying the SAMD firmware version

The query mechanism is identical to the STM32 path — the tool sends `info\r` over the selected COM port at 115200 baud and reads until the closing `###` fence. Ensure the board is running application firmware (not in bootloader mode) before querying.

### Programming (flashing) the SAMD firmware

The SAMD flash process is a two-phase sequence:

**Phase 1 — 1200-baud touch (bootloader entry):**

1. Select the **SAMD (LOA)** radio button and choose the correct COM port.
2. Click **Program** and select your `.bin` file.
3. A confirmation dialog displays the selected COM port and file path — click **Yes** to proceed.
4. The tool opens the selected COM port at 1200 baud, holds it open for 5 seconds, then closes it. This triggers the SAMD to reset into its ROM bootloader. The board will re-enumerate on USB, potentially on a new COM port number.
5. The tool waits a further 5 seconds for the bootloader to settle. The form remains responsive during both waits.

> **Note:** After the 1200-baud touch the board re-enumerates. If your system assigns a new COM port number to the bootloader, update the COM port selection before the next flash attempt, or use a fixed USB port assignment in Windows Device Manager.

**Phase 2 — `bossac.exe` flash:**

6. `bossac.exe` is invoked with the following arguments:
   ```
   bossac.exe --port=<COM_PORT> -U true -i -e -w -v "<PATH_TO_BIN>" -R
   ```
   The `-R` flag resets the board back into application firmware after a successful flash.

7. Output appears in the text area. A successful flash shows erase, write, and verify progress, ending with a reset confirmation.

---

## Section 3 — Jetson Tracker via SSH / SFTP

The Jetson section manages the companion tracking computer (fixed IP: `192.168.1.22`). Operations include pinging, querying the tracker process, uploading a new tracker binary, and rebooting or restarting the tracker service.

> All Jetson operations require the host PC to be on the same subnet (`192.168.1.x`). Verify connectivity with the Ping button before attempting uploads or SSH commands.

When on the Tracker tab the status bar updates to reflect Jetson state:

| Field | Description |
|---|---|
| Controller | Always `TRC` when Jetson operations are active |
| Version | Tracker version from `trackCntrl --version`, e.g. `v2.1.0` |
| IP | Fixed Jetson address `192.168.1.22` |
| ● Link | **🟢 Lime** = reachable, **🔴 Tomato** = unreachable |
| Message | `TRC: running`, `TRC: not running`, or `TRC: unreachable` |

### Recommended sequence for a tracker firmware update

**Ping → Query (stops running process) → Upload → Restart**

### Ping

Click **Ping** to send an ICMP ping to `192.168.1.22`. The result is added to the list box and the status bar link dot updates immediately — green if reachable, red if not.

### Query (check and stop tracker process)

Click **Query** to ping, then connect via SSH and check whether `trackCntrl.exe` is currently running.

- If running: the PID is shown, a `SIGINT` is sent to stop it cleanly, and a fresh check after 500ms confirms whether the process exited.
- If not running: the list box shows `trackCntrl not running`.
- The tracker version is retrieved via `trackCntrl --version` and shown in the status bar.

Run this before uploading to ensure the old binary is not held open.

### Upload tracker binary

1. Click **Upload**. A file dialog opens filtered to `.exe` files.
2. Select the compiled tracker binary.
3. The tool connects via SFTP and uploads to:
   ```
   /home/ipg/CV/TRC2/trackCntrl.exe
   ```
4. File permissions are set to `744` after upload. The list box shows `Tracker Uploaded` on success.
5. The version is immediately read back from the newly uploaded binary via `--version` and the status bar updates automatically.

### Restart tracker

Click **Restart** to launch the tracker in the background via the startup script:
```bash
nohup ~/ben.sh > /dev/null 2>&1 &
```
The process continues running after the SSH session closes.

### Reboot Jetson

Click **Reboot** to issue a full system reboot:
```bash
exec sudo reboot now
```
The SSH connection drops immediately — this is expected. Allow approximately 60 seconds for the Jetson to complete its boot sequence before reconnecting.

> **Warning:** Reboot interrupts all running processes including any active tracking operation. Confirm the system is in a safe state before rebooting.
