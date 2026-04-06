# GStreamer Install & Config — THEIA Operator PC
**Document Version:** 3.0.0
**Date:** 2026-03-15
**For:** H.264 video receive from TRC (Jetson Orin NX)
**Target decoder:** `nvh264dec` (NVIDIA hardware) — requires CUDA-capable GPU
**Fallback decoder:** `avdec_h264` (software) — if no NVIDIA GPU

**v3.0.0 changes (session 16):**
- Section 6 test pipeline updated to verified working pipeline (buffer-size, latency=50, drop-on-latency, n-threads=4, fpsdisplaysink)
- Section 8 multicast expanded with full pipeline and THEIA code change — status: action item pending
- Section 10 quirks: latency updated from 0 → 50 ms. Display timer note clarified.
- Quick reference table updated to match verified pipeline values.
- 30 fps option added as action item (section 11).

---

## 1 — Download

Go to: **https://gstreamer.freedesktop.org/download/**

Download **both** installers for Windows MSVC 64-bit:
- `gstreamer-1.0-msvc-x86_64-<version>.msi` — **Runtime**
- `gstreamer-1.0-devel-msvc-x86_64-<version>.msi` — **Development**

> Use the same version for both. Latest stable 1.24.x recommended.

---

## 2 — Install

Run **both** installers. When prompted for install type, choose **Complete** (not Typical).

Install path: `C:\gstreamer\1.0\msvc_x86_64\`
_(This is the hardcoded path in `GStreamerPipeReader.cs` — do not change it)_

---

## 3 — NVIDIA plugin (nvh264dec)

`nvh264dec` is in `gstreamer-plugins-bad`. It is included in the **Complete** install.

Requirements:
- NVIDIA GPU with NVDEC support (GTX 900 series or newer)
- NVIDIA driver 452.39 or newer
- CUDA is **not** required separately — NVDEC is driver-level

Verify the plugin is available (see section 5).

---

## 4 — Environment variables (optional)

`GStreamerPipeReader.cs` sets these programmatically for the child process — you do **not**
need to set them system-wide. If you want `gst-launch-1.0.exe` to work from a regular command
prompt, add:

| Variable | Value |
|----------|-------|
| `GST_PLUGIN_PATH` | `C:\gstreamer\1.0\msvc_x86_64\lib\gstreamer-1.0` |
| `PATH` (append) | `C:\gstreamer\1.0\msvc_x86_64\bin` |

---

## 5 — Verify installation

Open a command prompt in `C:\gstreamer\1.0\msvc_x86_64\bin\`:

```bat
set GST_PLUGIN_PATH=C:\gstreamer\1.0\msvc_x86_64\lib\gstreamer-1.0

:: Check version
gst-launch-1.0.exe --version

:: List available decoders — look for nvh264dec and avdec_h264
gst-inspect-1.0.exe nvh264dec
gst-inspect-1.0.exe avdec_h264
```

Expected output for `nvh264dec`:
```
Plugin Details:
  Name                     nvcodec
  Description              GStreamer NVCODEC plugin
  ...
  nvh264dec: NVDEC H.264 Video Decoder
```

If `nvh264dec` is not found but `avdec_h264` is, see section 7.

---

## 6 — Test pipeline (verified — no THEIA running)

From `C:\gstreamer\1.0\msvc_x86_64\bin\` with TRC actively streaming:

```bat
set GST_PLUGIN_PATH=C:\gstreamer\1.0\msvc_x86_64\lib\gstreamer-1.0

gst-launch-1.0.exe ^
  udpsrc port=5000 buffer-size=2097152 ^
    caps="application/x-rtp,media=video,encoding-name=H264,payload=96" ^
  ! rtpjitterbuffer latency=50 drop-on-latency=true ^
  ! rtph264depay ^
  ! h264parse ^
  ! nvh264dec ^
  ! videoconvert n-threads=4 ^
  ! fpsdisplaysink sync=false text-overlay=true signal-fps-measurements=true
```

You should see a live video window with FPS overlay. If `nvh264dec` fails, substitute
`avdec_h264` (see section 7).

**Key parameters vs earlier pipeline:**

| Parameter | Old value | Verified value | Reason |
|-----------|-----------|----------------|--------|
| `buffer-size` | not set | `2097152` (2 MB) | Prevents UDP RX buffer overflow at 60 fps |
| `caps media=video` | not set | set | Explicit media type — avoids caps negotiation failure |
| `latency` | `0` | `50` | 50 ms absorbs Jetson encoder jitter without stalling |
| `drop-on-latency` | not set | `true` | Drops late packets rather than stalling pipeline |
| `videoconvert n-threads=4` | `n-threads=1` | `4` | Reduces BGR conversion CPU time |
| sink | `autovideosink` | `fpsdisplaysink` | FPS overlay for diagnostics — test only |

> **Note:** The test pipeline uses `fpsdisplaysink` for diagnostics. `GStreamerPipeReader.cs`
> uses `fdsink` to pipe raw BGR frames to the application — do not use `fpsdisplaysink` in the
> THEIA pipeline build.

---

## 7 — Fallback: software decode (no NVIDIA GPU)

If the operator PC has no NVIDIA GPU, edit `GStreamerPipeReader.cs`:

```csharp
// BEFORE (hardware)
pipeline.Append("! nvh264dec ");

// AFTER (software fallback)
pipeline.Append("! avdec_h264 ");
```

Software decode at 1280×720 60 fps uses ~25–35% of a modern CPU core.
At 30 fps (when implemented): ~10–15% CPU. Latency increases by ~15–30 ms vs hardware decode.

---

## 8 — Unicast vs multicast

### Current: Unicast (active)

TRC sends H.264 RTP directly to the THEIA operator PC IP on port 5000. This is the
current production configuration. TRC is started with:

```bash
./multi_streamer --dest-host 192.168.1.208
```

### Planned: Multicast (action item — pending `0xD1` wiring)

> ⏳ **Action item:** `0xD1 ORIN_SET_STREAM_MULTICAST` binary handler not yet implemented
> in TRC (`needs impl` in ICD). The following documents the intended configuration for
> when this is wired.

**Multicast group:** `239.127.1.21` (site-local, CROSSBOW reserved)
**Port:** `5000` (unchanged)

**TRC sender pipeline change** (when `0xD1` is implemented):
```
udpsink multicast-iface=eth0 host=239.127.1.21 port=5000
```

**THEIA receiver test pipeline:**
```bat
gst-launch-1.0.exe ^
  udpsrc multicast-group=239.127.1.21 port=5000 buffer-size=2097152 ^
    caps="application/x-rtp,media=video,encoding-name=H264,payload=96" ^
  ! rtpjitterbuffer latency=50 drop-on-latency=true ^
  ! rtph264depay ^
  ! h264parse ^
  ! nvh264dec ^
  ! videoconvert n-threads=4 ^
  ! fpsdisplaysink sync=false text-overlay=true signal-fps-measurements=true
```

**`GStreamerPipeReader.cs` change required:**

```csharp
// BEFORE (unicast)
pipeline.Append($"udpsrc port={port} buffer-size=2097152 ");
pipeline.Append($"caps=\"application/x-rtp,media=video,encoding-name=H264,payload=96\" ");

// AFTER (multicast)
pipeline.Append($"udpsrc multicast-group=239.127.1.21 port={port} buffer-size=2097152 ");
pipeline.Append($"caps=\"application/x-rtp,media=video,encoding-name=H264,payload=96\" ");
```

Multicast enables multiple simultaneous THEIA clients to receive the same stream without
TRC sending duplicate unicast streams. Requires all receiver NICs to be on the
192.168.1.x subnet with multicast routing enabled on the switch.

---

## 9 — EmguCV NuGet compatibility

`GStreamerPipeReader.cs` and `VideoPanel.cs` depend on `Emgu.CV` for `Mat` and `DepthType`
only. No additional GStreamer NuGet package is required — `gst-launch-1.0.exe` is launched as
a subprocess and is independent of EmguCV's internal GStreamer binding.

**Note:** THEIA-01 replaced the EmguCV `ImageBox` control with `VideoPanel` (a
double-buffered WinForms Panel). EmguCV is still present for `Mat`/`DepthType` — it is no
longer used for display.

---

## 10 — Known stream quirks (TRC-specific)

| Item | Value | Notes |
|------|-------|-------|
| `PixelShift` | **-420** | Horizontal pixel correction in `GStreamerPipeReader.cs`. Root cause unknown — likely a fixed alignment offset in the Jetson `nvv4l2h264enc` pipeline. Do not change without retesting on hardware. |
| Resolution | **1280×720 fixed** | Must be passed explicitly: `_reader.Start(5000, 1280, 720)`. Auto-detect does not produce valid frames for this stream. |
| Jitter buffer latency | **50 ms** | `rtpjitterbuffer latency=50`. Was 0 ms — increased to absorb Jetson encoder timing jitter. `drop-on-latency=true` prevents pipeline stall on overflow. |
| Display timer | **16 ms (60 Hz)** | `_displayTimer` drives `videoPanel1.Refresh()` on the UI thread. If 30 fps is enabled (see section 11), change to `33 ms`. |
| UDP buffer | **2097152 (2 MB)** | `udpsrc buffer-size=2097152`. Required at 60 fps — default OS buffer causes packet drops. |

---

## 11 — 30 fps option (action item)

> ⏳ **Action item:** 30 fps operation is not yet wired end-to-end. The following documents
> the intended implementation path.

TRC currently streams at **60 fps fixed** (compositor tick rate = 60 Hz).

**To enable 30 fps:**

1. **TRC:** Send `0xD2 ORIN_SET_STREAM_60FPS {0x00}` (set 30 fps) or ASCII `FRAMERATE 30`.
   Binary handler marked `needs impl` in ICD — must be implemented in `udp_listener.cpp`.

2. **THEIA `GStreamerPipeReader.cs`:** Update startup call:
   ```csharp
   // 60 fps (current)
   _reader.Start(5000, 1280, 720);   // display timer = 16 ms

   // 30 fps (when implemented)
   _reader.Start(5000, 1280, 720);   // display timer = 33 ms
   ```
   Update `_displayTimer.Interval` from `16` → `33` to match.

3. **CPU impact:** Software decode (`avdec_h264`) at 30 fps drops from ~25–35% to ~10–15%
   CPU. Hardware decode (`nvh264dec`) impact is negligible at either framerate.

---

## Quick reference

| Item | Value |
|------|-------|
| GStreamer install path | `C:\gstreamer\1.0\msvc_x86_64\` |
| Install type required | **Complete** |
| Video port (TRC) | **5000** (UDP, H.264 RTP, payload=96) |
| Resolution | **1280×720 fixed** |
| Framerate | **60 fps** (30 fps — action item pending) |
| Transport | **Unicast** (multicast — action item pending) |
| Multicast group (planned) | `239.127.1.21` |
| Hardware decoder | `nvh264dec` |
| Software fallback | `avdec_h264` |
| UDP buffer size | `2097152` (2 MB) |
| Jitter buffer latency | `50 ms` with `drop-on-latency=true` |
| Expected E2E latency | 30–80 ms (hardware), 50–100 ms (software) |
| `PixelShift` correction | `-420` px horizontal |
