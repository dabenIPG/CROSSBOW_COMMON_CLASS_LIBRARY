# FW Programmer — Code Review Action Items

> Generated from review of `frmFWProgrammer.cs`, `TMC.ino`, `tmc.hpp`, `tmc.cpp`
> Priority: 🔴 Critical · 🟡 Significant · 🔵 Minor

---

## C# — `frmFWProgrammer.cs`

### 🔴 Critical

- [x] **#1 — UI thread blocking during flash**
  Refactored `ProgramSTM32` → `ProgramSTM32Async` returning `Task`. Both `Thread.Sleep` calls replaced with `await Task.Delay`. `WaitForExit` replaced with `await WaitForExitAsync`. `btnSelectFile_Click` made `async`, button disabled during operation.

- [x] **#2 — `ErrorHandler` is dead code — flash errors are invisible**
  Wired `ErrorHandler` to `process.ErrorDataReceived`. Updated `textBox1.Text = sError.ToString() + sOuput.ToString()` so errors appear above stdout output.

- [x] **#3 — SAMD COM port hardcoded to `COM6`**
  Replaced hardcoded `--port=COM6` with `--port={_comPort}` in bossac arguments.

- [x] **#4 — Hardcoded SSH/SFTP credentials in plain text**
  Consolidated to `private const` fields at the top of the class: `JETSON_HOST`, `JETSON_USER`, `JETSON_PASSWORD`, `JETSON_TRACKER_PATH`. All four Jetson methods updated to reference constants. Full credential management (e.g. per-session dialog) deferred to a future action item.

### 🟡 Significant

- [x] **#5 — `SerialPort` not disposed in `btnQuery_Click`**
  Resolved as part of #8 — port now wrapped in `using` block.

- [x] **#6 — SSH/SFTP operations block the UI thread**
  All five Jetson methods converted to `async void`. SSH/SFTP work offloaded to `Task.Run`. Upload progress streamed to `tssMsgs` via `Invoke`. `FileUploadSFTP` removed — logic folded into `btn_JetsonUpload_Click`. All buttons disabled during operation and re-enabled on completion.

- [x] **#7 — `ParseResponse` fragile on line count**
  Replaced positional `lines.Length == 6` check with `###` fence detection. Parser now tolerates any number of lines between fences and any amount of trailing whitespace.

- [x] **#8 — `btnQuery_Click` read timing fragile**
  Replaced fixed `Thread.Sleep(100)` / `ReadExisting()` pattern with an async read loop that accumulates data until the closing `###` fence is detected or a 2-second timeout expires.

- [x] **#9 — `btn_JetsonQuery_Click` — `command.Execute()` called again on same object after kill**
  Replaced with a fresh `RunCommand` call for the post-kill verification. Wait increased to 500ms. Error output now goes to both `listBox1` and `Debug.WriteLine`.

- [x] **#10 — Silent swallowed exception in `btn_JetsonReboot_Click`**
  `SshConnectionException` still caught silently (expected — reboot drops connection). All other exceptions now logged to `listBox1` and `Debug.WriteLine`.

- [x] **#11 — No confirmation before flashing**
  Added `MessageBox.Show` confirmation displaying COM port and file path before `ProgramSTM32Async` is called.

### 🔵 Minor

- [x] **#25 — Bundle `opencr_ld.exe` and `bossac.exe` with the application deployment**
  Both executables placed in a `tools\` subfolder in the project with **Copy if newer** set. Paths resolved at runtime via `Path.Combine(AppContext.BaseDirectory, "tools", ...)`. Startup validation warns if either tool is missing. Radio buttons now show the resolved tool filename in `tssProg` — or `NOT FOUND` if missing — without opening a file dialog.

- [x] **#12 — Hardcoded tool paths (`C:\Users\IPG Photonics\...`)**
  Resolved with #25 — paths now derived from `AppContext.BaseDirectory`.

- [x] **#13 — Redundant `Dispose()` in `FileUploadSFTP`**
  Removed redundant `finally` block — `using` handles disposal correctly.

- [x] **#14 — Inconsistent error logging**
  All Jetson methods now log to both `listBox1` and `Debug.WriteLine`. `Console.WriteLine` calls removed throughout.

- [x] **#27 — Jetson status bar display** *(new feature)*
  Added `UpdateJetsonStatus(reachable, processRunning, version)` helper. Ping, Query, and Upload complete all update `tssController` (TRC), `tssIP` (192.168.1.22), `tssLink` (● Lime=reachable / Tomato=unreachable), `tssVersion` (from `trackCntrl --version`), and `tssMsgs` (TRC: running / not running / unreachable). Tracker path consolidated to `JETSON_TRACKER_PATH` constant used for both SFTP upload destination and `--version` call.

- [x] **#15 — COM port list not refreshed on device change**
  Replaced one-time load with a `System.Windows.Forms.Timer` polling at 500ms. List only refreshes when `GetPortNames()` result changes. On disconnect: dropdown clears, text box clears, status bar resets to `---`, Query and Program buttons disabled. On connect: dropdown populates, buttons re-enabled. Previous selection restored if port is still present.

- [x] **#26 — Status bar controller info display** *(new feature)*
  Added `tssController`, `tssVersion`, `tssIP`, and `tssLink` to the status strip. `ParseResponse` now extracts controller name (BDC, TMC, MCC etc.), firmware version, build date, IP address, and link status from the `###` fenced INFO response. `tssLink` displays a `●` dot — `Color.Lime` when UP, `Color.Tomato` when DOWN, `Color.Gray` when not queried. All fields reset to `---` on port disconnect or bad response.

- [x] **#24 — `INFO` serial response delimiter** *(mitigated)*
  No longer a risk — `ParseResponse` now keys off `###` fence lines and uses named field search rather than positional line indexing. Adding new fields to the INFO response will not break parsing.

---


