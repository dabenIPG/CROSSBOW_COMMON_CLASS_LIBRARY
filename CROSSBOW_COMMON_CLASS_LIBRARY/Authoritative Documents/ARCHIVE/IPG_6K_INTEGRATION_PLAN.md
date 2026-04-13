# IPG 6K Laser Integration Plan
**CROSSBOW MCC — HEL Subsystem**
**Date:** 2026-04-10
**Status:** Step 1 complete ✅ — validated on YLM-3K hardware. Step 2 (firmware) pending — 6K validation tomorrow before FW work begins.

---

## Implementation Sequence

**Step 1 — C# ENG GUI HEL window (direct TCP to laser)** ✅ COMPLETE
Validated on YLM-3000-SM-VV (3K) and YLM-6000-U3-SM (6K) hardware. TCP + UDP coexist
simultaneously on bench. Both laser models auto-sensed correctly.

**Step 2 — Firmware (`ipg.hpp` / `ipg.cpp` + MCC)** ✅ COMPLETE
Validated on bench 2026-04-10. TCP transport, auto-sense, model-conditional poll,
training mode, COMBAT gate, TCP drop/reconnect — all confirmed working.

---

## Step 1 — C# ENG GUI HEL Window ✅ COMPLETE

### 1.1 Validated Findings (2026-04-10)

| Item | Finding |
|------|---------|
| Transport | TCP port 10001 ✅ confirmed |
| MCC coexistence | MCC UDP 10011 + ENG GUI TCP 10001 run simultaneously — no conflict ✅ |
| Sense command | `RMODEL` (not `RMN`) — `RMN` returns hostname (e.g. `IPGP578`), not model string |
| Model string | `RMODEL: YLM-3000-SM-VV` — parse on `-` delimiter, second token = `3000` |
| Serial number | `RSN: PL2546496` ✅ |
| Poll loop | Starts after sense, `RHKPS` confirmed flowing ✅ |
| 6K validation | ⏳ Pending tomorrow |

### 1.2 Known Issues / Remaining

| Issue | Detail | Fix |
|-------|--------|-----|
| SocketException on reconnect | First `Stop()` doesn't fully close socket before second `Start()` | Null `_tcp`/`_stream` in `Stop()` ✅ applied |
| 6K not yet validated | `SDC` vs `SCS`, bit decode, no `RHKPS`/`RBSTPS` | Test tomorrow on 6K hardware |

### 1.3 Pre-Step 2 Checklist

| Item | Detail | Status |
|------|--------|--------|
| ⚠️ **Deploy `defines.hpp` fleet-wide** | `LASER_MODEL` enum + `LASER_MAX_POWER_W()` added to `defines.hpp`. Must be deployed to **all five controllers** (MCC, BDC, TMC, FMC, TRC) before Step 2 firmware build — `defines.hpp` is shared across all controllers. BDC/TMC/FMC/TRC don't use `LASER_MODEL` directly but must compile cleanly with the new definitions. | ⏳ Pending |
| 6K ENG GUI validation | Connect 6K to HEL ENG GUI, validate sense, poll, `SDC`, STA bits | ⏳ Pending |
- Status strip slots: `tssModel`, `tssSerialNumber` ✅

---

### 1.2 `hel.cs` — Full Rewrite

#### Transport
```csharp
private TcpClient   tcpClient;
private NetworkStream stream;
public  string IP   { get; private set; } = "192.168.1.13";
public  int    Port { get; private set; } = 10001;
```

`Start()` → `tcpClient.ConnectAsync(IP, Port)` → open `NetworkStream` → start background
read task + start poll timer.

`Stop()` → cancel token → close stream + client.

Liveness: `isConnected = tcpClient?.Connected ?? false` checked on each poll tick.
Reconnect watchdog: if `!isConnected` and elapsed > `RECONNECT_MS (5000)`, attempt
`tcpClient.ConnectAsync()` again.

#### Auto-Sense on Connect
Immediately after TCP connect, before starting the poll loop:
```csharp
Send("RMN\r");   // triggers sense in Parse()
Send("RSN\r");   // serial number
```

`Parse()` handles `RMN` response:
```csharp
case "RMN":
    // e.g. "YLM-3000-SM-VV" or "YLM-6000-U3-SM-???"
    string[] parts = payload.Trim().Split('-');
    if (parts.Length >= 2 && int.TryParse(parts[1], out int power))
    {
        if      (power == 3000) LaserModel = LASER_MODEL.YLM_3K;
        else if (power == 6000) LaserModel = LASER_MODEL.YLM_6K;
        else                    LaserModel = LASER_MODEL.UNKNOWN;
    }
    ModelName    = payload.Trim();
    MaxPower_W   = LaserModel.MaxPower_W();
    IsSensed     = LaserModel.IsSensed();
    break;
```

#### Periodic Poll Loop
Mirror the firmware POLL state machine as a C# `async` timer or `Task.Delay` loop.
Poll interval: `20 ms` (matching firmware TICK). State variable `p1` cycles through
the same cases as firmware — gated on `IsSensed`.

| p1 | Command | Condition |
|----|---------|-----------|
| 0 | `RHKPS\r` | 3K only — skip on 6K |
| 1 | `RCT\r` | both |
| 2 | `STA\r` | both |
| 3 | `RMEC\r` | both |
| 4 | `RBSTPS\r` | 3K only — skip on 6K |
| 5 | `RCS\r` | both |
| 6 | `ROP\r` | both |
| → 0 | wrap | |

#### `Parse()` — complete response handler
```csharp
public void Parse(byte[] rxBuff)
{
    HB_RX_ms  = (DateTime.UtcNow - lastMsgRx).TotalMilliseconds;
    lastMsgRx = DateTime.UtcNow;

    string raw = Encoding.ASCII.GetString(rxBuff).Trim();
    string[] parts = raw.Split(':', 2, StringSplitOptions.TrimEntries);
    if (parts.Length < 2) return;

    string cmd     = parts[0].ToUpper();
    string payload = parts[1];

    switch (cmd)
    {
        case "RMN":    /* sense — handled above */          break;
        case "RSN":    SerialNumber = payload.Trim();       break;
        case "RHKPS":  IPGMsg.HKVoltage   = double.Parse(payload); break;
        case "RBSTPS": IPGMsg.BusVoltage  = double.Parse(payload); break;
        case "RCT":    IPGMsg.Temperature = double.Parse(payload); break;
        case "STA":    IPGMsg.StatusWord  = uint.Parse(payload);   break;
        case "RMEC":   IPGMsg.ErrorWord   = uint.Parse(payload);   break;
        case "RCS":    IPGMsg.SetPoint    = double.Parse(payload);  break;
        case "ROP":
            if (payload.Trim() == "OFF" || payload.Trim() == "LOW")
                IPGMsg.OutputPower_W = 0;
            else
                IPGMsg.OutputPower_W = double.Parse(payload);
            break;
    }
}
```

#### Send helpers
```csharp
// Generic — all commands route through here
private void Send(string cmd)
{
    if (stream == null || !tcpClient.Connected) return;
    byte[] b = Encoding.ASCII.GetBytes(cmd);
    stream.Write(b, 0, b.Length);
}

// Model-aware set power
public void SET_POWER(float pct)
{
    string cmd = LaserModel == LASER_MODEL.YLM_6K ? "SDC " : "SCS ";
    Send($"{cmd}{pct:F1}\r");
}

public void EMON()    { Send("EMON\r"); }
public void EMOFF()   { Send("EMOFF\r"); }
public void RERR()    { Send("RERR\r"); }
public void RMN_cmd() { Send("RMN\r"); }
public void RSN_cmd() { Send("RSN\r"); }
```

#### Public properties
```csharp
public LASER_MODEL LaserModel  { get; private set; } = LASER_MODEL.UNKNOWN;
public bool        IsSensed    { get; private set; } = false;
public int         MaxPower_W  { get; private set; } = 0;
public string      ModelName   { get; private set; } = "---";
public string      SerialNumber { get; private set; } = "---";
public MSG_IPG     IPGMsg      { get; private set; } = new MSG_IPG();
public bool        IsEmitting  => IPGMsg.isEmitting;
public bool        IsEMON
{
    get
    {
        // normalized — same bit logic as firmware isEMON()
        return LaserModel == LASER_MODEL.YLM_6K
            ? (IPGMsg.StatusWord & (1u << 2)) != 0
            : (IPGMsg.StatusWord & (1u << 0)) != 0;
    }
}
public bool IsNotReady => (IPGMsg.StatusWord & (1u << 9)) != 0;  // 3K bit 9
```

---

### 1.3 `frmHEL.cs` — Wiring

#### Controls to add to designer
| Control | Name | Purpose |
|---------|------|---------|
| Button | `btn_EMON` | Send EMON |
| Button | `btn_EMOFF` | Send EMOFF |
| Label | `lbl_LaserModel` | Display sensed model |
| Label | `lbl_MaxPower` | Display max power W |

Remove or hide: `radStateCombat`, `radStateSURVL`, `rdoStateSTNDBY`, `chk_HEL_UnSolEnable`,
`chk_HEL_UnSolEnabled_rb` — these are MCC state controls, not relevant to direct laser comms.

#### `timer1_Tick` — full wiring
```csharp
private void timer1_Tick(object sender, EventArgs e)
{
    tssDate.Text         = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.ff");
    tssModel.Text        = $"MODEL: {aHEL.ModelName}";
    tssSerialNumber.Text = $"SN: {aHEL.SerialNumber}";

    lbl_HEL_HB_ms.Text = $"HEL HB RX:   {aHEL.HB_RX_ms:000.00} ms";

    lbl_mcc_hel_hk_volts.Text   = $"HK VOLTS:   {aHEL.IPGMsg.HKVoltage:0.00} V";
    lbl_mcc_hel_bus_volts.Text  = $"BUS VOLTS:  {aHEL.IPGMsg.BusVoltage:0.00} V";
    lbl_mcc_hel_case_temp.Text  = $"CASE TEMP:  {aHEL.IPGMsg.Temperature:0.0} °C";

    lbl_mcc_hel_status_word.Text =
        Convert.ToString((long)aHEL.IPGMsg.StatusWord, 2).PadLeft(32, '0');
    lbl_mcc_hel_error_word.Text  =
        Convert.ToString((long)aHEL.IPGMsg.ErrorWord,  2).PadLeft(32, '0');

    // progress bars — max driven by sensed model
    prog_mcc_hel_power.Maximum   = aHEL.MaxPower_W > 0 ? aHEL.MaxPower_W : 3000;
    prog_mcc_hel_setpoint.Maximum = 100;
    prog_mcc_hel_power.Value     =
        (int)Math.Min(aHEL.IPGMsg.OutputPower_W, prog_mcc_hel_power.Maximum);
    prog_mcc_hel_setpoint.Value  =
        (int)Math.Min(aHEL.IPGMsg.SetPoint, 100);

    chk_mcc_hel_isEmOn.Checked    = aHEL.IsEMON;
    chk_mcc_hel_isNotReady.Checked = aHEL.IsNotReady;

    // error word label background — red if any error
    lbl_mcc_hel_error_word.BackColor =
        aHEL.IPGMsg.isError ? Color.Red : Color.MistyRose;
}
```

#### Command button wiring
```csharp
private void btn_EMON_Click(object sender, EventArgs e)           { aHEL.EMON(); }
private void btn_EMOFF_Click(object sender, EventArgs e)          { aHEL.EMOFF(); }
private void btn_mcc_hel_clear_errors_Click(object sender, EventArgs e) { aHEL.RERR(); }
private void btn_mcc_hel_setPower_Click(object sender, EventArgs e)
{
    aHEL.SET_POWER((float)num_mcc_hel_setLaserPower.Value);
}
```

---

### 1.4 Files Changed — Step 1

| File | Change |
|------|--------|
| `hel.cs` | Full rewrite — TCP, auto-sense, poll loop, complete Parse() |
| `frmHEL.cs` | Full timer wiring, EMON/EMOFF handlers, set power handler |
| `frmHEL_Designer.cs` | Add EMON/EMOFF buttons; remove MCC state radio buttons; dynamic progress bar max |
| `defines.cs` | Add `LASER_MODEL` enum + `LaserModelExt` (needed by `hel.cs`) |
| `MSG_IPG.cs` | No block changes — `PowerSetting_W` signature update deferred to Step 2 |

**Not changed in Step 1:** `defines.hpp`, `ipg.hpp`, `ipg.cpp`, `mcc.hpp`, `mcc.cpp`,
`MSG_MCC.cs`, `CROSSBOW_ICD_INT_ENG.md` — all firmware and ICD changes are Step 2.

---

---

---

## Step 2 — Firmware (`ipg.hpp` / `ipg.cpp` + MCC)

*Implement after Step 1 is validated on both laser variants via ENG GUI.*

### IPG Command Reference — 3K vs 6K (validated 2026-04-10)

#### Sense / Identity

| Command | 3K | 6K |
|---------|----|----|
| `RMODEL\r` | Returns `RMN: YLM-3000-SM-VV` — **3K sense path** | Returns empty string |
| `RMN\r` | Returns `RMN: IPGP578` (hostname — ignore) | Returns `RMN: YLM-6000-U3-SM` — **6K sense path** |
| `RSN\r` | Returns serial number | Returns serial number |

**Sense strategy:** Send both `RMODEL` and `RMN` on connect. `TrySenseModel()` parses whichever response contains a `-` delimited power field. `RMN` hostname on 3K has no `-` so is correctly ignored.

#### Poll Commands

| Command | 3K | 6K | Firmware POLL |
|---------|----|----|---------------|
| `RHKPS\r` | ✅ HK voltage V | ❌ | Case 0 — 3K only |
| `RCT\r` | ✅ | ✅ | Case 1 — both |
| `STA\r` | ✅ | ✅ | Case 2 — both |
| `RMEC\r` | ✅ | ✅ | Case 3 — both |
| `RBSTPS\r` | ✅ Boost voltage V | ❌ | Case 4 — 3K only |
| `RCS\r` | ✅ setpoint % | ✅ setpoint % | Case 5 — both |
| `ROP\r` | ✅ output power W | ✅ output power W ch1 | Case 6 — both |

#### Set Power

| Command | 3K | 6K |
|---------|----|----|
| `SCS <pct>\r` | ✅ | ❌ |
| `SDC <pct>\r` | ❌ | ✅ |

#### STA Word Bit Decode

| Meaning | 3K bit | 6K bit |
|---------|--------|--------|
| Emission ON | 0 | 2 |
| Overheat | 16 | 1 |
| Not ready / PSU off | 9 | 11 |
| External ctrl enabled | 5 | 18 |
| Error present | 10 | 19 |
| Critical error | 29 | 29 |
| Ext shutdown / Fiber break | 31 | 30 |
| Bus voltage / PSU error | 20 | 25 |

---

## 1. Background

MCC currently drives a single laser type — the IPG YLM-3000-SM-VV (3K) — via the `ipg` class
in `ipg.hpp` / `ipg.cpp`. A second laser, the IPG YLM-6000-U3-SM (6K), is being integrated. The two
lasers share the same Ethernet command interface structure but differ in:

- Command mnemonics (e.g. `SCS` vs `SDC` for set power)
- `STA` status word bit layout (completely different)
- Available telemetry fields (6K has no HK/bus voltage query)
- Transport (both should be TCP on port 10001 — current class incorrectly uses UDP on port 10011)

The goal is a single unified `ipg` class that auto-senses the connected laser, maps all
fields into the existing `MSG_IPG` block, and propagates model identity through the MCC
register so C# clients can decode correctly without out-of-band configuration.

---

## 2. Transport Fix

### Current (wrong)
```
EthernetUDP  udpClient;
uint32_t Port = 10011;
```

### Target
```
EthernetClient tcpClient;
uint32_t Port = 10001;
```

Both the 3K and 6K use **TCP on port 10001**. The W5500 TCP client is a 1-for-1 socket swap —
socket 6 switches from UDP to TCP. W5500 socket budget is unchanged at 6/8 (PTP disabled).

**W5500 socket budget after change:**

| # | Owner | Port | Type |
|---|-------|------|------|
| 1 | udpA1 | 10019 | UDP |
| 2 | udpA2 | 10018 | UDP |
| 3 | udpA3 | 10050 | UDP |
| 4 | GNSS udpRxClient | 3001 | UDP |
| 5 | GNSS udpTxClient | 3002 | UDP |
| 6 | IPG tcpClient | 10001 | **TCP** |
| 7 | PTP udpEvent | 319 | UDP (PTP enabled only) |
| 8 | PTP udpGeneral | 320 | UDP (PTP enabled only) |

TCP liveness: replace `isConnected = udpClient.endPacket()` pattern with `tcpClient.connected()`
poll. Connection is established in `START()` and checked on every `UPDATE()`.

---

## 3. Laser Model Enum — `defines.hpp` / `defines.cs`

Single authoritative definition, parallel on both sides of the wire.

### `defines.hpp`
```cpp
enum class LASER_MODEL : uint8_t
{
    UNKNOWN = 0x00,
    YLM_3K  = 0x01,   // bit 0 — YLM-3000-SM-VV
    YLM_6K  = 0x02    // bit 1 — YLM-6000-U3-SM
};

inline uint16_t LASER_MAX_POWER_W(LASER_MODEL m)
{
    switch (m)
    {
        case LASER_MODEL::YLM_3K: return 3000;
        case LASER_MODEL::YLM_6K: return 6000;
        default:                  return 0;
    }
}
```

### `defines.cs`
```csharp
public enum LASER_MODEL : byte
{
    UNKNOWN = 0x00,
    YLM_3K  = 0x01,
    YLM_6K  = 0x02
}

public static class LaserModelExt
{
    public static int MaxPower_W(this LASER_MODEL m)
    {
        switch (m)
        {
            case LASER_MODEL.YLM_3K: return 3000;
            case LASER_MODEL.YLM_6K: return 6000;
            default:                 return 0;
        }
    }

    public static bool IsSensed(this LASER_MODEL m) => m != LASER_MODEL.UNKNOWN;
}
```

Future lasers (e.g. YLR-10K) require only one new line in each file. All switch-based callers
cascade automatically.

---

## 4. Auto-Sense Logic — `ipg.cpp`

### Mechanism
On `INIT()`, immediately after TCP connect, send `RMN\r` and wait for the response in
`checkRsp()`. Parse the power field from the model string to identify the laser.

### Expected responses
```
3K → "RMN: YLM-3000-SM-VV"   power field = 3000
6K → "RMN: YLM-6000-U3-SM-???"     power field = 6000
```

### Parse logic
```cpp
// tok = value after "RMN: " — e.g. "YLM-3000-SM-VV"
char* dash = strchr(tok, '-');
if (dash != nullptr)
{
    int power = atoi(dash + 1);
    if      (power == 3000) { model_type = LASER_MODEL::YLM_3K; isInit = true; }
    else if (power == 6000) { model_type = LASER_MODEL::YLM_6K; isInit = true; }
    else
    {
        model_type = LASER_MODEL::UNKNOWN;
        // isInit remains false — POLL loop stays gated
        Serial.print(F("IPG ERROR — unrecognised laser power field: "));
        Serial.println(power);
    }
}
else
{
    model_type = LASER_MODEL::UNKNOWN;
    Serial.println(F("IPG ERROR — RMN parse failed, no '-' delimiter"));
}
```

### Also fix: store RSN
`SerialNumber[]` char array already exists in `ipg.hpp` but the response is currently
discarded. Store it on sense:
```cpp
strncpy(SerialNumber, tok, sizeof(SerialNumber));
SerialNumber[sizeof(SerialNumber)-1] = '\0';
```

### POLL gate
The periodic POLL loop (`case 10–16`) is gated on `isSensed()`:
```cpp
void IPG::POLL()
{
    if (!isConnected)  return;
    if (!isSensed())   return;   // wait for RMN sense before polling
    // ... existing poll state machine
}
```

Sense is triggered from a pre-poll state (cases 0–1) that run unconditionally after connect.

---

## 5. `ipg.hpp` Changes

### New members
```cpp
LASER_MODEL model_type = LASER_MODEL::UNKNOWN;
bool isSensed() { return model_type != LASER_MODEL::UNKNOWN; }
uint8_t LASER_MODEL_BITS() { return (uint8_t)model_type; }
```

### `isInit` gating
`isInit` is only set `true` after a successful `RMN` sense. This means:
```cpp
bool isReady() { return isStarted && isConnected && isInit; }
```
...already returns `false` for an unsensed laser with no additional changes needed.

### `isEMON()` — normalized across both lasers
```cpp
bool isEMON()
{
    if (model_type == LASER_MODEL::YLM_6K)
        return ((status_word & (1U << 2)) != 0);   // 6K: emission = bit 2
    return ((status_word & (1U << 0)) != 0);        // 3K: emission = bit 0
}
```
`STATUS_BITS()` calls `isEMON()` so the fix propagates automatically into MCC REG1 with no
caller changes.

### `SET_POWER()` — signature and mnemonic change
```cpp
// was: void SET_POWER(uint8_t _pow)
void SET_POWER(float _pow)
{
    if (model_type == LASER_MODEL::UNKNOWN) return;   // guard
    tcpClient.print(model_type == LASER_MODEL::YLM_6K ? F("SDC ") : F("SCS "));
    tcpClient.print(_pow, 1);
    tcpClient.print(F(" \r"));
}
```
6K uses `SDC` (Set Diode Current), 3K uses `SCS` (Set Current Setpoint).
Both accept float %. Callers in `mcc.cpp` pass `uint8_t` today — update call sites to `float`.

### `FIRE_REQUEST()` — model guard
```cpp
void IPG::FIRE_REQUEST(bool _en)
{
    if (model_type == LASER_MODEL::UNKNOWN)
    {
        Serial.println(F("IPG ERROR — FIRE_REQUEST blocked, laser not sensed"));
        return;
    }
    // ... EMON / EMOFF (unchanged)
}
```

---

## 6. POLL Loop Changes (`ipg.cpp`)

### 3K-only commands — skip on 6K

| Case | Command | 3K | 6K | Action |
|------|---------|----|----|--------|
| 10 | `RHKPS` | ✅ | ❌ | skip on 6K, `p1++` |
| 14 | `RBSTPS` | ✅ | ❌ | skip on 6K, `p1++` |
| 1 | `RMODEL` | ✅ | use `RMN` | handled in sense path |

### `checkRsp()` additions for 6K

| Response token | Field | Notes |
|---------------|-------|-------|
| `SDC` | `setPoint` | 6K set-point echo — store same as `RCS` |
| `ROPS` | (future) | ch2 output power — not in current MSG_IPG, skip for now |

### `hk_volts` / `bus_volts` on 6K
No equivalent commands exist on the 6K. Fields remain at their initialised values
(`5.5f` currently — should be changed to `0.0f`). C# uses `IsLaserSensed()` /
`LASER_MODEL` to know whether to display these fields.

---

## 7. MCC REG1 Byte [255] — LASER_MODEL

### ICD change — `CROSSBOW_ICD_INT_ENG.md`

| Byte | From | To | nBytes | Name | Type | Notes |
|------|------|----|--------|------|------|-------|
| 255 | 255 | 256 | 1 | **LASER_MODEL** | uint8 | `LASER_MODEL` enum. `0x00`=UNKNOWN/not sensed; `0x01`=YLM_3K; `0x02`=YLM_6K. Was RESERVED. |

**Backwards compatible** — old C# clients reading `0x00` here see `UNKNOWN` which is a safe
default (they were reading reserved zero before).

### Firmware pack in `mcc.cpp` `SEND_REG_01()`
```cpp
// [255] LASER_MODEL
buf[255] = ipg.LASER_MODEL_BITS();
```

### `MSG_MCC.cs` parse
```csharp
LaserModel = (LASER_MODEL)msg[255];
```

New properties on `MSG_MCC`:
```csharp
public LASER_MODEL LaserModel    { get; private set; } = LASER_MODEL.UNKNOWN;
// Callers use extension methods:
//   msg.LaserModel.IsSensed()
//   msg.LaserModel.MaxPower_W()
```

### `MSG_IPG.cs` — fix hardcoded max power
```csharp
// was: public double PowerSetting_W => SetPoint / 100.0 * 3000;
// now driven by caller from MSG_MCC.LaserModel:
public double PowerSetting_W(LASER_MODEL model) => SetPoint / 100.0 * model.MaxPower_W();
```

---

## 8. Vote / State Machine — `mcc.cpp` / `mcc.hpp`

### New helper
```cpp
// mcc.hpp
bool isHEL_Valid() { return ipg.isSensed() && ipg.isReady(); }
```

### `StateManager()` — block COMBAT if HEL not valid
```cpp
case SYSTEM_STATES::COMBAT:
{
    if (!isHEL_Valid())
    {
        Serial.println(F("STATE COMBAT BLOCKED — HEL model not sensed or not ready"));
        StateManager(SYSTEM_STATES::STNDBY);
        return;
    }
    // ... existing combat entry
}
```

### `VOTE_BITS()` — include HEL validity
```cpp
// existing VOTE_BITS — add isHEL_Valid() to a spare bit or extend as needed
// HEL validity is already implicit in DEVICE_READY_BITS bit 2 (isHEL_Ready → ipg.isReady())
// isHEL_Valid() adds the sense check on top — COMBAT gate is sufficient
```

### Flow summary
```
RMN sense fails / power unknown
  → model_type = LASER_MODEL::UNKNOWN
  → isInit = false
  → isReady() = false
  → DEVICE_READY_BITS [8] bit 2 = 0   (HEL not ready)
  → isHEL_Valid() = false
  → COMBAT blocked / regressed to STNDBY
  → byte [255] = 0x00 (LASER_MODEL::UNKNOWN)
  → C# LaserModel.IsSensed() = false
  → ENG GUI shows HEL fault — no special-casing needed
```

---

## 9. `MSG_IPG.cs` Summary of Changes

| Field | Change |
|-------|--------|
| `PowerSetting_W` | Remove hardcoded `3000` — accept `LASER_MODEL` parameter |
| No block size change | `IPG_BLOCK_LEN` stays `21` |
| No offset changes | All bytes [45–65] unchanged |

---

## 10. Files to Update

| File | Changes |
|------|---------|
| `defines.hpp` | Add `LASER_MODEL` enum + `LASER_MAX_POWER_W()` |
| `defines.cs` | Add `LASER_MODEL` enum + `LaserModelExt` static class |
| `ipg.hpp` | TCP socket; `model_type`; `isSensed()`; `LASER_MODEL_BITS()`; `isEMON()` normalized; `SET_POWER(float)`; `FIRE_REQUEST()` guard; `hk_volts`/`bus_volts` init to `0.0f` |
| `ipg.cpp` | TCP transport; sense state machine (cases 0–1); POLL gate on `isSensed()`; 3K-only command skip; `checkRsp()` additions; `SerialNumber` store |
| `mcc.hpp` | `isHEL_Valid()` helper |
| `mcc.cpp` | `SEND_REG_01()` pack byte [255]; `StateManager()` COMBAT gate; `SET_POWER()` call sites `uint8_t` → `float` |
| `MSG_MCC.cs` | Parse byte [255] as `LASER_MODEL`; extension method callers |
| `MSG_IPG.cs` | `PowerSetting_W` accepts `LASER_MODEL` parameter |
| `CROSSBOW_ICD_INT_ENG.md` | Byte [255] RESERVED → LASER_MODEL; IPG command delta table |

---

## 11. ICD Delta — IPG Command Coverage

### Shared commands (same mnemonic, same behavior — no change)
`EMON`, `EMOFF`, `RERR`, `RCT`, `ROP`, `RSN`, `RMN`, `RMEC`, `RFV`, `RET`, `RCS`, `ELE`, `DLE`

### 3K-only commands (skip on 6K)
`RHKPS`, `RBSTPS`, `RBPSN`, `RBPSM`

### Renamed on 6K
| 3K | 6K | Field |
|----|-----|-------|
| `SCS` | `SDC` | Set power setpoint |
| `RMODEL` | `RMN` | Read model name (sense path only) |

### STA status word — normalized via `isEMON()`
| | 3K bit | 6K bit |
|--|--------|--------|
| Emission ON | bit 0 | bit 2 |
| All other bits | raw, stored in `status_word` | raw, stored in `status_word` |

Raw `status_word` is passed through to C# unchanged. Only `isEMON()` normalizes the
emission bit. C# callers wanting other bit fields must check `LASER_MODEL` and apply the
correct bit mask per laser.

---

## 12. Open Items

| Item | Notes |
|------|-------|
| 6K ch2 output power (`ROPS`) | Not in current poll — future extension |
| 6K pulse mode (`SPRR`, `SPW`, `EGM`) | Not required for CROSSBOW fire control |
| 6K aiming beam (`ABN`/`ABF`) | Not required — no guide laser in CROSSBOW path |
| **HB counter — BAT** | `HB_BAT` always 0. Add `lastMsgRx_ms` to `bat` class, stamp on each received packet, compute delta at REG1 pack time. Same pattern as `ipg.HB_RX_ms`. |
| **HB counter — GNSS** | `HB_GNSS` always 0. Add `lastMsgRx_ms` to `gnss` class, stamp on each received position fix, compute delta at REG1 pack time. |
| **HB counter — CRG** | `HB_CRG` always 0. V1 only — CRG has no I2C on V2. Gate packing behind `#if defined(HW_REV_V1)` or check `isV2`. Add receive timestamp if CRG polling exists. |
| **HB counter — TIME (was NTP)** | `HB_NTP` byte [130] is stamped on NTP packet receive only. Should also stamp on PTP sync event. Consider renaming `HB_NTP` → `HB_TIME` in firmware, ICD, and `MSG_MCC.cs` to reflect dual PTP/NTP source. |
| **HB counter — `lastTick_*` cleanup** | `lastTick_BAT`, `lastTick_CRG`, `lastTick_GNSS`, `lastTick_HEL` declared in `mcc.hpp` but never written — remove stubs or wire up when HB counters are implemented. |

## 13. Pending Serial Command Enhancements

After HEL firmware validated on bench:

### TMC Serial Section
Add `TMC` command to MCC serial with:
- A1 liveness (already partially exists — expand)
- Full TMC REG1 dump with field decode (mirrors `PRINT_REG()` style)
- Any direct TMC commands (PUMP, HEATER, FAN, TARGET TEMP) accessible from MCC serial

### Power OFF Command
Add a universal power-off shorthand to the serial power commands.
Candidates: `PWR OFF`, `POWER 0`, or prefix existing commands e.g. `HEL OFF` → sends `SET_POWER(0)`.
Apply consistently across all power-controllable subsystems.
Exact command names TBD — decide at implementation time.

---

*End of plan.*
