# STM32CubeIDE Migration Plan
### From: Arduino IDE + STM32F7 (OpenCR Variant) → STM32CubeIDE + HAL/LL Ecosystem

**Document Status:** Planning  
**Target Hardware:** STM32F7xx on Custom PCB (OpenCR variant origin)  
**Scope:** Migrate mature multi-firmware embedded controller codebase leveraging compiler optimizations, advanced peripheral control, RTOS integration, full STM32 ecosystem tooling, and a scalable shared library architecture.

---

## Table of Contents

1. [Migration Overview & Strategy](#1-migration-overview--strategy)
2. [Phase 1 — Download & Install Toolchain](#2-phase-1--download--install-toolchain)
3. [Phase 2 — Configure STM32CubeIDE for STM32F7](#3-phase-2--configure-stm32cubeide-for-stm32f7)
4. [Phase 3 — Simple Test Project (Blinky + UART)](#4-phase-3--simple-test-project-blinky--uart)
5. [Phase 4 — Upload & Debug](#5-phase-4--upload--debug)
6. [Phase 5 — Arduino Library Mapping & Migration](#6-phase-5--arduino-library-mapping--migration)
7. [Phase 6 — Full Codebase Migration](#7-phase-6--full-codebase-migration)
8. [Phase 7 — Shared Library Architecture (Static Library)](#8-phase-7--shared-library-architecture-static-library)
9. [Phase 8 — CMake Build System (Long-Term Target)](#9-phase-8--cmake-build-system-long-term-target)
10. [Reference: Key Differences Cheat Sheet](#10-reference-key-differences-cheat-sheet)
11. [Risk Register](#11-risk-register)

---

## 1. Migration Overview & Strategy

### Goals
- Replace Arduino IDE compilation with STM32CubeIDE (GCC ARM + ST toolchain)
- Gain access to: LTO (Link-Time Optimization), FPU tuning, `-O2`/`-O3` flags, stack/heap analysis
- Utilize STM32CubeMX for pin/clock/peripheral graphical configuration
- Access HAL and Low-Layer (LL) drivers for deterministic peripheral control
- Preserve or port existing Arduino library functionality (WIZ5500 Ethernet, etc.)
- Establish a **single shared library** consumed by all firmware variants — one source of truth
- Establish a repeatable project template for future boards
- Long-term: migrate to a CMake build system for full multi-target control and CI/CD readiness

### Guiding Principles
- **Parallel, not replacement** — Keep the Arduino IDE codebase working while the CubeIDE port is developed alongside it
- **Incremental** — Port peripheral by peripheral, validate at each step
- **HAL-first, LL-later** — Start with STM32 HAL for speed of porting; optimize hot paths to LL drivers after validation
- **One library, many firmwares** — Common code lives in exactly one place; firmware projects are consumers, not owners

### Migration Phases at a Glance

```
Phase 1: Install & validate toolchain
Phase 2: Configure CubeIDE for your exact STM32F7 variant
Phase 3: Build and flash a minimal test project (Blinky + UART loopback)
Phase 4: Establish the flash/debug workflow
Phase 5: Map and replace Arduino library dependencies (WIZ5500, etc.)
Phase 6: Full codebase migration, module by module
Phase 7: Shared Static Library architecture — CommonLib consumed by all firmware projects
Phase 8: CMake build system migration — long-term scalable target
```

---

## 2. Phase 1 — Download & Install Toolchain

### 2.1 Required Downloads

| Tool | Purpose | URL |
|---|---|---|
| **STM32CubeIDE** | Unified IDE (Eclipse + GCC ARM + CubeMX integrated) | https://www.st.com/en/development-tools/stm32cubeide.html |
| **STM32CubeMX** (optional standalone) | Graphical peripheral/clock configurator; already bundled in CubeIDE | https://www.st.com/en/development-tools/stm32cubemx.html |
| **STM32CubeProgrammer** | Flash programmer (ST-LINK, DFU, UART) | https://www.st.com/en/development-tools/stm32cubeprog.html |
| **STM32F7 HAL/BSP Firmware Package** | HAL drivers, middleware, examples for F7 family | Via CubeIDE Package Manager OR https://github.com/STMicroelectronics/STM32CubeF7 |
| **ST-LINK/V2 Drivers** | USB drivers for ST-LINK debugger/programmer (Windows only) | Bundled with STM32CubeIDE installer |
| **STM32CubeMonitor** (optional) | Real-time variable monitoring over SWD | https://www.st.com/en/development-tools/stm32cubemonitor.html |

> **Note on OpenCR:** The OpenCR board uses an STM32F746ZGT6. Confirm your exact part number from the PCB silkscreen or your variant files (`boards.txt`). The full part number determines available flash, RAM, and peripherals.

### 2.2 Installation Steps

#### Windows
1. Download the `.exe` installer from the ST website (requires a free MyST account)
2. Run the installer as Administrator
3. Accept the default install path (`C:\ST\STM32CubeIDE_x.x.x\`)
4. The installer will also install ST-LINK USB drivers automatically
5. After installation, launch CubeIDE and confirm the Welcome screen loads

#### macOS
1. Download the `.dmg` installer
2. Mount and drag STM32CubeIDE to `/Applications`
3. On first launch, macOS Gatekeeper may block it — go to **System Settings → Privacy & Security** and approve
4. Install the ST-LINK v2/v3 drivers from the included package if prompted

#### Linux (Ubuntu/Debian)
```bash
# Make the installer executable
chmod +x st-stm32cubeide_*.sh

# Run installer (will install to ~/st/stm32cubeide_x.x.x by default)
sudo ./st-stm32cubeide_*.sh

# Add udev rules for ST-LINK USB access (critical — without this, flash will fail)
sudo cp /opt/st/stm32cubeide_*/plugins/com.st.stm32cube.ide.mcu.externaltools.stlink-gdb-server*/tools/bin/udev/rules.d/*.rules /etc/udev/rules.d/
sudo udevadm control --reload-rules
sudo usermod -aG dialout $USER   # Add yourself to dialout group
# Log out and back in for group change to take effect
```

### 2.3 Install STM32F7 Firmware Package

Within STM32CubeIDE:

1. Go to **Help → Manage Embedded Software Packages**
2. Expand **STM32F7** in the package list
3. Select the latest stable release (e.g., `STM32Cube FW_F7 V1.17.x`)
4. Click **Install Now** — this downloads HAL drivers, CMSIS, middleware, and examples
5. Accept the license agreement

---

## 3. Phase 2 — Configure STM32CubeIDE for STM32F7

### 3.1 Identify Your Exact MCU

Before creating a project, confirm these details from your hardware:

| Parameter | Where to Find | Example (OpenCR) |
|---|---|---|
| Part Number | PCB silkscreen or BOM | `STM32F746ZGT6` |
| Package | Datasheet | LQFP144 |
| Flash Size | Part number suffix | 1 MB |
| RAM | Datasheet | 320 KB (256 DTCM + 64 AXI + ITCM) |
| Crystal/HSE | Schematic | 8 MHz (OpenCR) or 25 MHz |
| Debug Interface | Schematic / connector | SWD (2-wire) or JTAG |

> **Critical:** If you are targeting the exact OpenCR board, the HSE is 16 MHz and the system clock is configured to 216 MHz via PLL. If using a custom PCB, pull your clock values from the schematic before configuring CubeMX.

### 3.2 Create a New STM32 Project

1. **File → New → STM32 Project**
2. In the target selector, choose the **MCU/MPU Selector** tab
3. Search for your part (e.g., `STM32F746ZGT6`)
4. Select the matching row and click **Next**
5. Name the project (e.g., `F7_TestBlinky`), leave defaults, click **Finish**
6. CubeIDE will open the `.ioc` (CubeMX) graphical configurator

### 3.3 Configure Clocks (CubeMX Clock Tree)

This is the most important step — incorrect clocks cause all peripheral timing to be wrong.

1. In the `.ioc` file, go to **RCC** under **System Core**
2. Set **HSE** to `Crystal/Ceramic Resonator`
3. Set **HSI** as needed for fallback
4. Navigate to the **Clock Configuration** tab
5. Configure for **216 MHz SYSCLK** (the F746's maximum):
   - Input: HSE (e.g., 8 MHz)
   - PLL: `M=4, N=216, P=2` → 216 MHz
   - AHB Prescaler: `/1` → 216 MHz HCLK
   - APB1 Prescaler: `/4` → 54 MHz
   - APB2 Prescaler: `/2` → 108 MHz
6. Resolve any red clock conflicts using the **Resolve Clock Issues** button

### 3.4 Configure SWD Debug Interface

1. In the `.ioc` file, under **System Core → SYS**
2. Set **Debug** to `Serial Wire` (SWD)
3. This allocates PA13/PA14 as SWDIO/SWDCLK — do **not** use these pins for GPIO

### 3.5 Configure GPIO for LED (Blinky Test)

1. Find your LED pin in your schematic (e.g., PB7 on OpenCR)
2. Click the pin in the CubeMX pin diagram
3. Set to `GPIO_Output`
4. In **GPIO Settings**: Label it `LED_TEST`, push-pull, no pull, low speed

### 3.6 Configure UART for Loopback Test

1. Under **Connectivity**, enable e.g. `USART3`
2. Mode: `Asynchronous`
3. Baud: `115200`, 8N1
4. Check that the assigned pins (e.g., PD8/PD9) match your hardware
5. Enable the USART global interrupt under **NVIC Settings**

### 3.7 Generate Code

1. Click **Project → Generate Code** (or the gear icon)
2. CubeIDE generates:
   - `main.c` with HAL init and peripheral init stubs
   - `stm32f7xx_hal_conf.h` — HAL module enable/disable
   - `stm32f7xx_it.c` — ISR handlers
   - Linker scripts (`.ld`) for your flash/RAM layout
   - Startup assembly file

> **Important:** CubeMX marks user code regions with `/* USER CODE BEGIN */` and `/* USER CODE END */` comment pairs. **Only write your code inside these blocks.** Code outside them will be overwritten the next time you regenerate from the `.ioc` file.

---

## 4. Phase 3 — Simple Test Project (Blinky + UART)

### 4.1 Blinky (GPIO Toggle)

In `main.c`, inside the `while(1)` loop between the `USER CODE BEGIN` markers:

```c
/* USER CODE BEGIN WHILE */
while (1)
{
    HAL_GPIO_TogglePin(LED_TEST_GPIO_Port, LED_TEST_Pin);
    HAL_Delay(500);   // 500 ms
    /* USER CODE END WHILE */
}
```

### 4.2 UART Echo

Add a receive buffer and echo loop. In `main.c`:

```c
/* USER CODE BEGIN PV */
uint8_t rx_byte;
/* USER CODE END PV */

/* Inside while(1): */
/* USER CODE BEGIN WHILE */
while (1)
{
    // Blocking receive with 100ms timeout, then echo back
    if (HAL_UART_Receive(&huart3, &rx_byte, 1, 100) == HAL_OK)
    {
        HAL_UART_Transmit(&huart3, &rx_byte, 1, HAL_MAX_DELAY);
    }

    HAL_GPIO_TogglePin(LED_TEST_GPIO_Port, LED_TEST_Pin);
    HAL_Delay(500);
    /* USER CODE END WHILE */
}
```

### 4.3 printf Retargeting (Serial Console)

To use `printf` over UART (very useful for debugging):

```c
/* USER CODE BEGIN Includes */
#include <stdio.h>
/* USER CODE END Includes */

/* USER CODE BEGIN 0 */
// Retarget _write so printf goes to USART3
int _write(int file, char *ptr, int len)
{
    HAL_UART_Transmit(&huart3, (uint8_t*)ptr, len, HAL_MAX_DELAY);
    return len;
}
/* USER CODE END 0 */
```

Then in `while(1)`:
```c
printf("Tick: %lu\r\n", HAL_GetTick());
HAL_Delay(1000);
```

### 4.4 Build the Project

1. **Project → Build Project** (or `Ctrl+B`)
2. Check the **Console** tab for errors
3. Check the **Build Analyzer** view for flash/RAM usage breakdown
4. A successful build outputs a `.elf`, `.hex`, and `.bin` file in the `Debug/` folder

### 4.5 Compiler Optimization Settings

Right-click the project → **Properties → C/C++ Build → Settings → MCU GCC Compiler → Optimization**

| Build Config | Recommended Setting | Notes |
|---|---|---|
| Debug | `-Og` (optimize for debug) | Preserves variable visibility in debugger |
| Release | `-O2` or `-O3` | Enable for production/performance testing |
| Release (size) | `-Os` | Minimize flash usage |

Enable **LTO (Link-Time Optimization)** for Release builds:
- Under **MCU GCC Linker → General**: check `Enable Link-Time Optimization (-flto)`

Enable **FPU** for STM32F7 (hard float):
- Under **MCU GCC Compiler → General**: `-mfpu=fpv5-d16 -mfloat-abi=hard`
- This is usually auto-configured by CubeIDE for F7 targets — verify it is present

---

## 5. Phase 4 — Upload & Debug

### 5.1 ST-LINK Connection

Connect your ST-LINK V2 (or onboard SWD header) to the target:

| ST-LINK Pin | Target Pin |
|---|---|
| SWDIO | PA13 |
| SWDCLK | PA14 |
| GND | GND |
| 3.3V | 3.3V (only if ST-LINK powers the board) |
| NRST | NRST (optional but recommended) |

> Verify the SWD connector pinout on your custom PCB — this varies by board designer.

### 5.2 Configure the Debug/Run Configuration

1. Click the **Debug** dropdown arrow → **Debug Configurations**
2. Under **STM32 Cortex-M C/C++ Application**, create a new config (or use the auto-generated one)
3. On the **Debugger** tab:
   - Interface: `SWD`
   - Speed: `4000 kHz` (reduce to `1000 kHz` if you see connection errors)
   - Reset behavior: `Software System Reset`
4. Click **Apply**, then **Debug**

### 5.3 Flash Without Debugging (Run Only)

1. Click **Run → Run** (green play button) to flash and run without the debugger attached
2. Alternatively, use **STM32CubeProgrammer** as a standalone tool:
   - Open CubeProgrammer
   - Select **ST-LINK** tab, connect
   - Open your `.hex` file and click **Download**

### 5.4 Using the Debugger

Once in debug mode:
- **Breakpoints**: Click the left margin of any line in `main.c`
- **Watch expressions**: Right-click a variable → **Add Watch Expression**
- **Live Expressions**: Use the **Live Expressions** panel for real-time variable monitoring
- **Peripheral Registers**: Open **Window → Show View → SFRs** to inspect STM32 register state live
- **SWV / printf via SWO**: Enable **Serial Wire Viewer** for printf output without UART wires:
  - In debug config → **SWV** tab → Enable with core clock = 216000000
  - `printf` output appears in the **SWV ITM Data Console**

### 5.5 DFU Upload (No ST-LINK, Bootloader Only)

If no ST-LINK is available, the STM32F7 has a built-in USB DFU bootloader:
1. Pull BOOT0 high, power-cycle the board
2. Connect USB to the board's USB OTG port
3. Open STM32CubeProgrammer → **USB** tab → Connect
4. Flash your `.hex` file
5. Pull BOOT0 low, reset

---

## 6. Phase 5 — Arduino Library Mapping & Migration

This phase addresses the key challenge: replacing Arduino libraries with native STM32 HAL equivalents or porting the libraries.

### 6.1 Library Dependency Audit

Before migrating, document every Arduino library in use:

```
[ ] List all #include directives across the codebase
[ ] Identify which are: Arduino core, third-party, custom/in-house
[ ] Classify each: HAL-replaceable | portability layer needed | full port required
```

### 6.2 WIZ5500 Ethernet (Primary Example)

The Arduino `Ethernet` library for the WIZ5500 (using SPI) is a major dependency. Migration path:

#### Option A: Port the Existing Arduino Ethernet Library (Fastest)
The WIZnet Arduino library is largely HAL-agnostic at the application level. Swap the SPI transport layer:

1. Take the `Ethernet` and `Ethernet2` library source from Arduino
2. Replace `SPI.begin()` / `SPI.transfer()` calls with STM32 HAL equivalents:

```c
// Arduino (original)
SPI.beginTransaction(SPISettings(14000000, MSBFIRST, SPI_MODE0));
SPI.transfer(data);
SPI.endTransaction();

// STM32 HAL equivalent
HAL_SPI_Transmit(&hspi1, &data, 1, HAL_MAX_DELAY);
```

3. Replace `digitalWrite(CS_PIN, LOW)` with `HAL_GPIO_WritePin(SPI_CS_GPIO_Port, SPI_CS_Pin, GPIO_PIN_RESET)`
4. Replace `millis()` with `HAL_GetTick()`
5. Replace `delay()` with `HAL_Delay()`

#### Option B: Use WIZnet's Official ioLibrary_Driver (Recommended for Clean Migration)
WIZnet provides an SPI-agnostic driver designed for bare-metal MCU use:
- Repository: https://github.com/Wiznet/ioLibrary_Driver
- Provides W5500 driver, socket layer, and TCP/IP stack
- You provide a `spi_rb()` / `spi_wb()` function wrapper around your HAL SPI handle
- Works cleanly in STM32CubeIDE — no Arduino dependency at all

#### Option C: LwIP via STM32 Middleware (Long-term, Most Capable)
For production use with a MAC+PHY or external Ethernet controller, STM32 middleware includes LwIP. For WIZ5500 this requires a custom netif driver, but gives full socket API, DHCP, DNS, etc.

### 6.3 Common Arduino → HAL Mapping Reference

| Arduino Function | STM32 HAL Equivalent | Notes |
|---|---|---|
| `pinMode(pin, OUTPUT)` | GPIO init via CubeMX | Configured at init time |
| `digitalWrite(pin, val)` | `HAL_GPIO_WritePin(GPIOx, Pin, State)` | |
| `digitalRead(pin)` | `HAL_GPIO_ReadPin(GPIOx, Pin)` | |
| `analogRead(pin)` | `HAL_ADC_Start()` + `HAL_ADC_GetValue()` | Configure ADC in CubeMX first |
| `analogWrite(pin, val)` | `__HAL_TIM_SET_COMPARE()` (PWM) | Configure TIM in CubeMX |
| `delay(ms)` | `HAL_Delay(ms)` | Tick-based, 1 ms resolution |
| `millis()` | `HAL_GetTick()` | Returns `uint32_t` ms count |
| `micros()` | DWT cycle counter / TIM in us mode | See note below |
| `Serial.begin(baud)` | UART configured in CubeMX | |
| `Serial.print(val)` | `HAL_UART_Transmit()` or `printf` | Retarget `_write` |
| `Serial.available()` | UART RX interrupt + ring buffer | Implement manually |
| `Wire.begin()` | `HAL_I2C_Init()` (via CubeMX) | |
| `Wire.write()` | `HAL_I2C_Master_Transmit()` | |
| `Wire.read()` | `HAL_I2C_Master_Receive()` | |
| `SPI.transfer()` | `HAL_SPI_TransmitReceive()` | |
| `attachInterrupt()` | CubeMX EXTI config + `HAL_GPIO_EXTI_Callback()` | |
| `yield()` / `loop()` structure | `while(1)` in `main.c` or FreeRTOS tasks | |

> **micros() Note:** `HAL_GetTick()` is 1 ms resolution. For microsecond timing, enable the DWT (Data Watchpoint and Trace) cycle counter:
```c
CoreDebug->DEMCR |= CoreDebug_DEMCR_TRCENA_Msk;
DWT->CYCCNT = 0;
DWT->CTRL |= DWT_CTRL_CYCCNTENA_Msk;
// Usage: uint32_t us = DWT->CYCCNT / (SystemCoreClock / 1000000);
```

### 6.4 Interrupt-Driven Serial (Ring Buffer)

Arduino's `Serial.available()` hides an ISR-backed ring buffer. Replicate this in STM32:

1. Enable UART RX interrupt in CubeMX (NVIC tab)
2. Start interrupt-mode reception: `HAL_UART_Receive_IT(&huart3, &rx_byte, 1);`
3. In `stm32f7xx_it.c` or `main.c` implement the callback:
```c
void HAL_UART_RxCpltCallback(UART_HandleTypeDef *huart)
{
    if (huart->Instance == USART3)
    {
        ring_buffer_push(&uart_rx_buf, rx_byte);
        HAL_UART_Receive_IT(&huart3, &rx_byte, 1); // Re-arm
    }
}
```
4. Provide `serial_available()` and `serial_read()` wrappers around your ring buffer

---

## 7. Phase 6 — Full Codebase Migration

### 7.1 Recommended Migration Order

Migrate subsystems in this order to manage risk:

```
1. GPIO / Digital I/O          ← Simplest, validates board bring-up
2. UART / Serial Debug         ← Critical for visibility during migration
3. Timers / PWM                ← Foundational for control loops
4. SPI / WIZ5500 Ethernet      ← High value, well-defined boundary
5. I2C Peripherals             ← Sensors, IMUs, EEPROMs
6. ADC Inputs                  ← Analog sensors
7. CAN / Other Comms           ← If used
8. Application Logic           ← Port domain-specific code last
```

### 7.2 Project Structure Convention

```
F7_Project/
├── Core/
│   ├── Inc/           ← HAL config headers, main.h (generated)
│   └── Src/           ← main.c, stm32f7xx_it.c, stm32f7xx_hal_msp.c (generated)
├── Drivers/
│   ├── CMSIS/         ← ARM CMSIS headers (generated)
│   └── STM32F7xx_HAL_Driver/   ← ST HAL source (generated)
├── Middleware/        ← LwIP, FreeRTOS, FatFS (if added via CubeMX)
├── App/               ← ★ YOUR APPLICATION CODE (create this folder)
│   ├── Inc/
│   └── Src/
└── Lib/               ← ★ PORTED THIRD-PARTY LIBRARIES (WIZ5500, etc.)
    ├── wiznet/
    └── ...
```

> Keep all your application code under `App/` and `Lib/` — this way regenerating from CubeMX never touches your files.

### 7.3 Build System Notes

STM32CubeIDE uses `make` under the hood with auto-generated `Makefile`. To add your `App/` folder:

1. Right-click **App** folder in the project tree → **Properties**
2. Confirm **C/C++ Build** includes it as a source location
3. Add include paths: **Project Properties → C/C++ Build → Settings → MCU GCC Compiler → Include Paths**

### 7.4 FreeRTOS Integration (Optional but Recommended)

The Arduino `loop()` model maps naturally onto a single FreeRTOS task. As the codebase grows, use CubeMX to add FreeRTOS (CMSIS-RTOS v2 wrapper):

1. In `.ioc` file → **Middleware → FREERTOS** → Enable
2. Create tasks in CubeMX that mirror your major control loops
3. Use `osDelay()` instead of `HAL_Delay()` to yield properly to the scheduler

### 7.5 Validation Checklist Per Module

For each ported subsystem, verify before moving to the next:

```
[ ] Compiles without warnings at -Wall
[ ] Behavior matches Arduino version on equivalent hardware test
[ ] No timing regressions (measure with DWT cycle counter or logic analyzer)
[ ] ISRs have acceptable worst-case latency
[ ] No stack overflow (use CubeIDE's Thread Analyzer if using FreeRTOS)
[ ] Flash/RAM usage reviewed in Build Analyzer
```

---

## 8. Phase 7 — Shared Library Architecture (Static Library)

This phase consolidates all common code that was previously duplicated across Arduino sketches or early CubeIDE ports into a single authoritative `CommonLib` static library project. All firmware projects become consumers of this library.

### 8.1 Audit: What Belongs in CommonLib

Before creating the library, categorize every source file across your firmware bases:

| Category | Goes in CommonLib? | Rationale |
|---|---|---|
| WIZ5500 / Ethernet driver | ✅ Yes | Hardware-agnostic transport, shared by all |
| UART ring buffer | ✅ Yes | Common pattern, no firmware-specific logic |
| Protocol parsers (Modbus, custom) | ✅ Yes | Pure logic, no board dependency |
| Sensor drivers (I2C, SPI) | ✅ Yes | If used by >1 firmware |
| HAL init (`MX_*_Init`) | ❌ No | Generated per-project, pin config varies per board |
| Application state machines | ❌ No | Firmware-specific |
| CubeMX-generated files | ❌ No | Always per-project |
| Board-specific GPIO defines | ❌ No | Lives in each firmware's `App/Inc/board_config.h` |

> **Rule of thumb:** If the same `.c` file exists in more than one firmware project with identical or near-identical content, it belongs in CommonLib.

### 8.2 Workspace Structure

```
STM32_Workspace/
│
├── CommonLib/                        ← Static library CubeIDE project
│   ├── CommonLib.ioc                 ← Minimal CubeMX config (clocks only, no pins)
│   ├── Inc/
│   │   ├── wiznet_driver.h
│   │   ├── ring_buffer.h
│   │   ├── modbus_rtu.h
│   │   └── common_utils.h
│   └── Src/
│       ├── wiznet_driver.c
│       ├── ring_buffer.c
│       ├── modbus_rtu.c
│       └── common_utils.c
│
├── FirmwareA/                        ← Firmware project: Controller type A
│   ├── FirmwareA.ioc
│   ├── Core/                         ← CubeMX-generated HAL/init
│   └── App/
│       ├── Inc/
│       │   └── board_config.h        ← Board-specific pin/peripheral defines
│       └── Src/
│           └── main_app.c            ← Application logic
│
├── FirmwareB/                        ← Firmware project: Controller type B
│   ├── FirmwareB.ioc
│   ├── Core/
│   └── App/
│       ├── Inc/
│       │   └── board_config.h
│       └── Src/
│           └── main_app.c
│
└── FirmwareC/                        ← Additional variants follow same pattern
```

### 8.3 Creating the CommonLib Static Library Project

1. **File → New → STM32 Project**
2. In the **New STM32 Project** wizard:
   - Select your MCU (`STM32F746ZGT6`)
   - Project Name: `CommonLib`
   - **Project Type: `Static Library`** ← critical, not Executable
3. Open the `.ioc` file — configure **only** the clock tree (to ensure HAL tick / `HAL_GetTick()` compiles correctly); do not configure any pins
4. Generate code — CubeIDE creates the HAL skeleton without any `main()` entry point
5. Delete or ignore `Core/Src/main.c` if generated — a static library has no entry point
6. Create `Inc/` and `Src/` folders under the project root
7. Move all common source files into them

### 8.4 Library Compiler Settings

The CommonLib must be compiled with the **same MCU flags** as the firmware projects that consume it. Mismatched FPU ABI is a common and painful source of linker errors.

In **CommonLib Project Properties → C/C++ Build → Settings → MCU GCC Compiler**:

```
-mcpu=cortex-m7
-mthumb
-mfpu=fpv5-d16
-mfloat-abi=hard        ← Must match all firmware projects exactly
-Wall
-ffunction-sections
-fdata-sections
```

> **Hard rule:** All projects in the workspace — CommonLib, FirmwareA, FirmwareB, etc. — must use identical `-mfpu` and `-mfloat-abi` flags or the linker will refuse to link them together.

### 8.5 Wiring a Firmware Project to CommonLib

Repeat for every firmware project (`FirmwareA`, `FirmwareB`, etc.):

#### Step 1 — Add Project Reference
- Right-click firmware project → **Properties → Project References**
- Check `CommonLib`
- This tells CubeIDE to build CommonLib before this firmware project

#### Step 2 — Add Include Path
- **Properties → C/C++ Build → Settings → MCU GCC Compiler → Include Paths**
- Add: `${workspace_loc}/CommonLib/Inc`

#### Step 3 — Add Library to Linker
- **Properties → C/C++ Build → Settings → MCU GCC Linker → Libraries**
- Library name: `common` (CubeIDE prepends `lib` and appends `.a` automatically)
- Library search path: `${workspace_loc}/CommonLib/Debug`

> Use `${workspace_loc}` (not an absolute path) so the workspace is portable across machines and developers.

#### Step 4 — Verify the Build Order
- **Project → Build Order** — confirm `CommonLib` appears before all firmware projects
- Right-click workspace → **Build All** — `CommonLib` builds first, then each firmware links against the resulting `libcommon.a`

### 8.6 Board-Specific Abstraction Pattern

CommonLib code must never hard-code pin numbers or peripheral handles. Use a **board configuration header** injected by each firmware project:

```c
// CommonLib/Inc/wiznet_driver.h
#include "board_config.h"   // Provided by the consuming firmware project

void wiznet_init(SPI_HandleTypeDef *hspi, GPIO_TypeDef *cs_port, uint16_t cs_pin);
void wiznet_transmit(uint8_t *buf, uint16_t len);
```

```c
// FirmwareA/App/Inc/board_config.h
#define WIZNET_SPI_HANDLE   hspi1
#define WIZNET_CS_PORT      GPIOA
#define WIZNET_CS_PIN       GPIO_PIN_4

// FirmwareB/App/Inc/board_config.h
#define WIZNET_SPI_HANDLE   hspi2
#define WIZNET_CS_PORT      GPIOB
#define WIZNET_CS_PIN       GPIO_PIN_12
```

Each firmware's `App/Inc/` is on its own include path, so `board_config.h` resolves correctly per build without any `#ifdef` pollution in the library itself.

### 8.7 Build Configuration: Debug vs Release

Create matching build configurations in CommonLib and all firmware projects:

| Config | CommonLib flags | Firmware flags |
|---|---|---|
| **Debug** | `-Og -g3` | `-Og -g3` |
| **Release** | `-O2 -flto` | `-O2 -flto` |

When building Release firmware, ensure you also build CommonLib in Release mode — linking a Debug `.a` into a Release firmware defeats LTO and mixes optimization levels.

CubeIDE's **Build Configurations** (right-click project → **Build Configurations → Manage**) let you switch all projects in the workspace together.

### 8.8 Version Control Strategy

```
git/
├── common-lib/          ← Its own repository
│   ├── Inc/
│   └── Src/
│
├── firmware-a/          ← Its own repository
│   └── lib/common/      ← git submodule → common-lib @ pinned commit
│
└── firmware-b/          ← Its own repository
    └── lib/common/      ← git submodule → common-lib @ (can differ from A)
```

Each firmware repo pins the common library to a specific commit tag. To update:
```bash
cd firmware-a/lib/common
git checkout v1.5.0
cd ../..
git add lib/common
git commit -m "Bump CommonLib to v1.5.0"
```

This gives you independent versioning per firmware — FirmwareA can stay on `v1.4.x` while FirmwareB moves to `v1.5.0`, with no risk of accidental cross-contamination.

### 8.9 CommonLib Validation Checklist

```
[ ] CommonLib builds cleanly as a standalone static library (no main.c)
[ ] All firmware projects build and link successfully after adding CommonLib
[ ] No absolute paths in include or library search paths (use ${workspace_loc})
[ ] FPU/float ABI flags are identical across CommonLib and all firmware projects
[ ] board_config.h pattern tested — same CommonLib source, different pin assignments per firmware
[ ] Build All from clean produces correct .elf for every firmware variant
[ ] Debug config and Release config both verified end-to-end
[ ] CommonLib in version control with tagged releases
[ ] Each firmware repo references CommonLib via git submodule at pinned tag
```

---

## 9. Phase 8 — CMake Build System (Long-Term Target)

CMake replaces the Eclipse/make build system with a portable, IDE-agnostic, scriptable build. It is the correct long-term architecture for a multi-firmware codebase with a shared library, CI/CD pipelines, and potential team growth. STM32CubeIDE supports CMake projects natively as of **v1.7+**.

### 9.1 Why CMake Over Eclipse Make

| Capability | Eclipse Make (CubeIDE default) | CMake |
|---|---|---|
| Multi-target builds | Manual per-project | Single invocation builds all targets |
| Shared library dependency | Manual project references | `target_link_libraries()` — automatic |
| CI/CD integration | Difficult | Native — CMake + Ninja runs anywhere |
| IDE portability | CubeIDE only | VS Code + Cortex-Debug, CLion, vim, any editor |
| Compiler flag management | Per-project GUI settings | Centrally defined, inherited by all targets |
| Cross-compilation | ST toolchain file required | Standard CMake toolchain file |
| Incremental builds | make | Ninja (faster) |

### 9.2 Prerequisite Tools

| Tool | Purpose |
|---|---|
| `cmake` ≥ 3.22 | Build system generator |
| `ninja` | Fast build backend (replaces make) |
| `arm-none-eabi-gcc` | Compiler (already installed with CubeIDE) |
| ST CMake toolchain file | Tells CMake how to cross-compile for STM32 |

The ARM GCC toolchain is already on disk from CubeIDE:
```
# Windows: C:\ST\STM32CubeIDE_x.x.x\...\tools\bin\
# Linux/macOS: ~/st/stm32cubeide_x.x.x/.../tools/bin/
```
Add it to your system `PATH` so CMake can find it outside the IDE.

### 9.3 STM32 CMake Toolchain File

Create `cmake/stm32f7_toolchain.cmake` at the workspace root. This file is passed to CMake at configure time and tells it to cross-compile for Cortex-M7:

```cmake
# cmake/stm32f7_toolchain.cmake

set(CMAKE_SYSTEM_NAME Generic)
set(CMAKE_SYSTEM_PROCESSOR arm)

# Point to the GCC ARM toolchain (update path for your OS/install)
set(TOOLCHAIN_PREFIX arm-none-eabi-)
set(CMAKE_C_COMPILER   ${TOOLCHAIN_PREFIX}gcc)
set(CMAKE_CXX_COMPILER ${TOOLCHAIN_PREFIX}g++)
set(CMAKE_ASM_COMPILER ${TOOLCHAIN_PREFIX}gcc)
set(CMAKE_OBJCOPY      ${TOOLCHAIN_PREFIX}objcopy)
set(CMAKE_SIZE         ${TOOLCHAIN_PREFIX}size)

# Prevent CMake from trying to link test executables for the host machine
set(CMAKE_TRY_COMPILE_TARGET_TYPE STATIC_LIBRARY)

# STM32F7 core flags — applied to all targets inheriting this toolchain
set(CPU_FLAGS
    "-mcpu=cortex-m7 -mthumb -mfpu=fpv5-d16 -mfloat-abi=hard"
)
set(CMAKE_C_FLAGS_INIT   "${CPU_FLAGS} -ffunction-sections -fdata-sections -Wall")
set(CMAKE_CXX_FLAGS_INIT "${CPU_FLAGS} -ffunction-sections -fdata-sections -Wall")
set(CMAKE_ASM_FLAGS_INIT "${CPU_FLAGS} -x assembler-with-cpp")
set(CMAKE_EXE_LINKER_FLAGS_INIT "-Wl,--gc-sections -Wl,--print-memory-usage")
```

### 9.4 Top-Level CMakeLists.txt

```cmake
# CMakeLists.txt (workspace root)
cmake_minimum_required(VERSION 3.22)

project(EmbeddedControllers C CXX ASM)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)

# ── CMSIS / HAL ────────────────────────────────────────────────────────────────
# Path to the STM32F7 HAL package installed by CubeIDE
set(HAL_PATH "$ENV{HOME}/STM32Cube/Repository/STM32Cube_FW_F7_V1.17.x")

add_library(stm32f7_hal STATIC
    ${HAL_PATH}/Drivers/STM32F7xx_HAL_Driver/Src/stm32f7xx_hal.c
    ${HAL_PATH}/Drivers/STM32F7xx_HAL_Driver/Src/stm32f7xx_hal_gpio.c
    ${HAL_PATH}/Drivers/STM32F7xx_HAL_Driver/Src/stm32f7xx_hal_uart.c
    ${HAL_PATH}/Drivers/STM32F7xx_HAL_Driver/Src/stm32f7xx_hal_spi.c
    ${HAL_PATH}/Drivers/STM32F7xx_HAL_Driver/Src/stm32f7xx_hal_rcc.c
    ${HAL_PATH}/Drivers/STM32F7xx_HAL_Driver/Src/stm32f7xx_hal_cortex.c
    # Add other HAL modules as needed
)
target_include_directories(stm32f7_hal PUBLIC
    ${HAL_PATH}/Drivers/STM32F7xx_HAL_Driver/Inc
    ${HAL_PATH}/Drivers/CMSIS/Device/ST/STM32F7xx/Include
    ${HAL_PATH}/Drivers/CMSIS/Include
)
target_compile_definitions(stm32f7_hal PUBLIC
    STM32F746xx
    USE_HAL_DRIVER
)

# ── Common Library ─────────────────────────────────────────────────────────────
add_library(common STATIC
    common_lib/Src/wiznet_driver.c
    common_lib/Src/ring_buffer.c
    common_lib/Src/modbus_rtu.c
    common_lib/Src/common_utils.c
)
target_include_directories(common PUBLIC common_lib/Inc)
target_link_libraries(common PUBLIC stm32f7_hal)

# ── Firmware Targets ───────────────────────────────────────────────────────────
add_subdirectory(firmware_a)
add_subdirectory(firmware_b)
# add_subdirectory(firmware_c)   # Add future variants here
```

### 9.5 Per-Firmware CMakeLists.txt

```cmake
# firmware_a/CMakeLists.txt

# Collect firmware-specific sources
set(FW_A_SOURCES
    Core/Src/main.c
    Core/Src/stm32f7xx_it.c
    Core/Src/stm32f7xx_hal_msp.c
    Core/Startup/startup_stm32f746zgtx.s
    App/Src/controller_a.c
)

add_executable(FirmwareA ${FW_A_SOURCES})

target_include_directories(FirmwareA PRIVATE
    Core/Inc
    App/Inc          # Contains this firmware's board_config.h
)

# Pull in common library — this also transitively pulls stm32f7_hal
target_link_libraries(FirmwareA PRIVATE common)

# Linker script for this specific board/flash layout
target_link_options(FirmwareA PRIVATE
    -T${CMAKE_CURRENT_SOURCE_DIR}/STM32F746ZGTX_FLASH.ld
)

# Post-build: generate .hex and .bin, print size
add_custom_command(TARGET FirmwareA POST_BUILD
    COMMAND ${CMAKE_OBJCOPY} -O ihex   $<TARGET_FILE:FirmwareA> FirmwareA.hex
    COMMAND ${CMAKE_OBJCOPY} -O binary $<TARGET_FILE:FirmwareA> FirmwareA.bin
    COMMAND ${CMAKE_SIZE} $<TARGET_FILE:FirmwareA>
    COMMENT "Generating FirmwareA .hex and .bin"
)
```

`FirmwareB` follows the identical pattern — only `FW_B_SOURCES`, `App/Inc` (its own `board_config.h`), and its linker script differ.

### 9.6 Build Invocation

```bash
# From workspace root — configure (one time)
cmake -S . -B build \
      -DCMAKE_TOOLCHAIN_FILE=cmake/stm32f7_toolchain.cmake \
      -DCMAKE_BUILD_TYPE=Debug \
      -G Ninja

# Build all firmware targets
cmake --build build

# Build a single target
cmake --build build --target FirmwareA

# Release build
cmake -S . -B build_release \
      -DCMAKE_TOOLCHAIN_FILE=cmake/stm32f7_toolchain.cmake \
      -DCMAKE_BUILD_TYPE=Release \
      -G Ninja
cmake --build build_release
```

All `.elf`, `.hex`, and `.bin` files land in `build/firmware_a/`, `build/firmware_b/`, etc.

### 9.7 Opening the CMake Project in STM32CubeIDE

CubeIDE can open CMake projects directly — you keep the graphical debugger and CubeProgrammer integration:

1. **File → Open Projects from File System**
2. Select your workspace root
3. CubeIDE detects `CMakeLists.txt` and opens as a CMake project
4. The **Build** button invokes CMake/Ninja instead of Eclipse make
5. Debug configurations work identically to a generated project — point to the `.elf` file in `build/firmware_a/`

> **Note:** CubeMX `.ioc` files can still be used per-firmware for graphical peripheral configuration, but the generated code is integrated into the CMake source lists manually rather than relying on the auto-generated `Makefile`. Some teams run CubeMX for initial generation and then take full manual ownership of `CMakeLists.txt`.

### 9.8 CMake Migration Checklist

```
[ ] arm-none-eabi-gcc on system PATH, confirmed with: arm-none-eabi-gcc --version
[ ] cmake >= 3.22 installed: cmake --version
[ ] ninja installed: ninja --version
[ ] Toolchain file tested: cmake configure completes without error
[ ] stm32f7_hal CMake target compiles cleanly
[ ] common library CMake target compiles cleanly
[ ] FirmwareA target links and produces .elf / .hex
[ ] FirmwareB target links and produces .elf / .hex
[ ] Both .hex files flash and run correctly via CubeProgrammer
[ ] CubeIDE opens CMake project and debugger attaches successfully
[ ] CI pipeline (GitHub Actions / GitLab CI) runs cmake --build successfully
[ ] Build times compared vs Eclipse make — Ninja should be meaningfully faster
```

### 9.9 Recommended CMake Migration Timing

Do **not** attempt CMake migration at the same time as the HAL porting work. The recommended gate:

```
✅ Phase 7 complete (all firmware ports validated on HAL, CommonLib working)
✅ Full debug/flash workflow stable in CubeIDE Eclipse-make mode
✅ Team comfortable with STM32 HAL patterns
→ Then migrate build system to CMake as a standalone, low-risk step
```

The CMake migration is purely a build system change at that point — no firmware behavior changes — which makes validation straightforward: flash and run, compare outputs.

---

## 10. Reference: Key Differences Cheat Sheet

| Aspect | Arduino IDE / OpenCR | STM32CubeIDE |
|---|---|---|
| **Compiler** | GCC ARM (via Arduino toolchain) | GCC ARM (ST-provided, newer version) |
| **HAL Layer** | Arduino abstraction (wiring) | STM32 HAL / LL drivers |
| **Pin Config** | `pinMode()` at runtime | CubeMX graphical config at design time → generates init code |
| **Clock Config** | Pre-configured in variant | Explicit PLL config in CubeMX clock tree |
| **Linker Script** | Provided by variant | Generated by CubeMX for exact part |
| **Startup Code** | In Arduino core | Generated startup .s file (CMSIS) |
| **Libraries** | Arduino Library Manager | Manual add or ST Middleware via CubeMX |
| **Debug** | Serial.print only | Full SWD debugging, breakpoints, register view, SWV |
| **Optimization** | Limited control | Full GCC flag control, LTO, FPU ABI selection |
| **RTOS** | Third-party or none | FreeRTOS integrated via CubeMX middleware |
| **Bootloader** | Arduino/OpenCR bootloader | ST system bootloader (DFU) or direct SWD flash |

---

## 11. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Clock misconfiguration causing UART/SPI timing errors | High | High | Validate console output immediately after first flash; compare baud rate on oscilloscope |
| WIZ5500 SPI timing margin lost due to different driver | Medium | High | Scope the SPI bus during first Ethernet test; reduce SPI clock if needed |
| Arduino library assumes blocking delays that HAL doesn't handle the same way | Medium | Medium | Audit all `delay()` calls; replace with state machines or RTOS delays |
| CubeMX code regeneration overwrites user code | Low (if following `USER CODE` blocks) | High | Enforce team practice of only editing inside `USER CODE` markers; use version control |
| SWD pins accidentally used as GPIO in custom PCB | Low | High | Cross-check schematic against CubeMX pin assignments before generating |
| Stack overflow in migrated ISRs | Medium | High | Enable MPU stack monitoring; test with CubeIDE stack analyzer |
| OpenCR-specific peripheral remapping not reproduced | Medium | Medium | Document all non-default pin remaps from OpenCR variant files before starting |
| Mismatched FPU/float ABI between CommonLib and firmware projects | Medium | High | Enforce identical `-mfpu` / `-mfloat-abi` flags in all projects; linker error will surface immediately if mismatch exists |
| CommonLib change breaks a firmware variant silently | Medium | High | Tag CommonLib releases; firmware projects pin to explicit tags via git submodule; run Build All after every CommonLib change |
| board_config.h missing from a firmware project's include path | Medium | Medium | CommonLib build will fail with missing header — surfaced at compile time, not runtime |
| CMake toolchain file not found / wrong GCC path | High (first time) | Low | Document exact GCC path per OS; test `arm-none-eabi-gcc --version` on PATH before configuring |
| CMake migration attempted before HAL port is stable | Medium | High | Enforce Phase 7 completion gate before starting Phase 8 — CMake is a build system change only, not a code change |
| CubeIDE CMake integration instability (known edge cases pre-v1.10) | Low | Medium | Keep CubeIDE updated; fall back to command-line `cmake --build` if IDE CMake integration misbehaves |

---

## Appendix A: Extracting OpenCR Variant Configuration

Before starting, extract these values from your Arduino/OpenCR variant files to use in CubeMX configuration:

From `variant.h` / `variant.cpp`:
- All `PIN_*` defines → translate to STM32 port/pin notation
- `SystemCoreClock` value → target PLL configuration
- Any peripheral remaps (UART, SPI, I2C alternate functions)

From `boards.txt`:
- Upload protocol and baud rate
- Bootloader offset (important for linker script if keeping bootloader compatibility)

From `platform.txt`:
- Existing compiler flags (FPU settings, architecture flags) — replicate in CubeIDE project settings

---

*Document maintained in version control alongside the codebase. Update Phase status as work progresses.*
