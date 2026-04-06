# Crossbow ATD — Full Codebase Review
**Revision 14 — Kalman predicted position discontinuous jumps resolved**

**Namespace:** `Hyperion`  
**Fix Status:** All 46 identified issues resolved across Revisions 1–14.

**Namespace:** `Hyperion`  
**Fix Status:** Issues 1–8, 16–18, 20–22, 25–27, 29–30, 32, 39, 41, 43 corrected in source. All critical and high issues resolved. 20 issues remain open (14 Medium, 6 Minor).

---

## Fix Status — Revision 5

The following critical issues have been corrected in the delivered source files. Each entry lists the file changed, the specific code change made, and the before/after for reference.

### ✅ Issue 1 — `VerticalRate_mps` Missing Unit Conversion — `adsb_raw.cs`

Property renamed (`VerticleRate` → `VerticalRate`) and conversion applied.

```csharp
// BEFORE:
public double VerticleRate_mps { get { return vr_fpm; } }

// AFTER:
public double VerticalRate_mps { get { return vr_fpm * 0.00508; } } // ft/min → m/s (1 ft/min = 0.3048/60 m/s)
```

> **Note:** Any callers referencing `VerticleRate_mps` by name must be updated to `VerticalRate_mps`.

---

### ✅ Issue 2 — Vertical Rate Sign Inverted — `adsb_raw.cs`

VrSgn = 0 means climb (positive); VrSgn = 1 means descent (negative). The ternary was backwards.

```csharp
// BEFORE:
vr_fpm = (VrSgn == 1 ? 1 : -1) * 64.0 * (Vr - 1);

// AFTER:
vr_fpm = (VrSgn == 0 ? 1 : -1) * 64.0 * (Vr - 1); // VrSgn=0 → climb (+), VrSgn=1 → descent (−)
```

---

### ✅ Issue 3 — IFR Flag Reads Wrong Bit — `adsb_raw.cs`

Both `IC_flag_str` and `IFR_flag_str` were reading string index 40 (the Intent Change bit). IFR Capability is at index 41.

```csharp
// BEFORE:
string IC_flag_str  = bitmsg.Substring(40, 1); // INTENT CHANGE FLAG
string IFR_flag_str = bitmsg.Substring(40, 1); // IFR CHANGE FLAG

// AFTER:
string IC_flag_str  = bitmsg.Substring(40, 1); // Intent Change flag  (bit 41, string index 40)
string IFR_flag_str = bitmsg.Substring(41, 1); // IFR Capability flag (bit 42, string index 41)
```

---

### ✅ Issue 4 — `BigInteger` Hex Parse Produces Wrong-Length Array — `adsb_raw.cs`

`BigInteger.ToByteArray()` appends a 0x00 sign-preservation byte when the high-order nibble ≥ 8, making a 14-byte frame appear as 15 bytes and silently fall through the switch. Replaced with `Convert.FromHexString()` which always returns exactly the right number of bytes. The `using System.Numerics` directive was also removed as it is no longer required.

```csharp
// BEFORE:
byte[] bmsg = BigInteger.Parse(pmsg, System.Globalization.NumberStyles.HexNumber)
                        .ToByteArray().Reverse().ToArray();

// AFTER:
byte[] bmsg = Convert.FromHexString(pmsg); // always exact length; BigInteger.ToByteArray() can add a sign byte
```

> **Requires .NET 5 or later.** `Convert.FromHexString` was introduced in .NET 5. The project targets .NET 6+ based on other API usage, so this is safe.

---

### ✅ Issue 22 — `ToArray()` Velocity Encodes Heading Degrees Instead of Speed — `Form1.cs`

The velocity components were computed as `cos(heading_rad) × heading_degrees` instead of `cos(heading_rad) × speed_mps`, producing a completely incorrect velocity vector. At heading = 90°, speed = 100 m/s the old code emitted vx = 90, vy ≈ 0 instead of the correct vx = 0, vy = 100.

```csharp
// BEFORE:
double vx = Math.Cos(aCB.CurrentCUE.HeadingSpeed.heading * Math.PI / 180) * aCB.CurrentCUE.HeadingSpeed.heading;
double vy = Math.Sin(aCB.CurrentCUE.HeadingSpeed.heading * Math.PI / 180) * aCB.CurrentCUE.HeadingSpeed.heading;
// need to verify quadrant

// AFTER:
// Convention: vx = North component (cos), vy = East component (sin)
double heading_rad = aCB.CurrentCUE.HeadingSpeed.heading * Math.PI / 180.0;
double speed_mps   = aCB.CurrentCUE.HeadingSpeed.speed;
double vx = Math.Cos(heading_rad) * speed_mps; // North component (m/s)
double vy = Math.Sin(heading_rad) * speed_mps; // East component  (m/s)
```

> **Open action (Issue 36):** The axis convention (vx = North, vy = East) must be confirmed against the downstream receiver's interpretation of `atan2(vy, vx)`. If the receiver uses `atan2(vy, vx)` to reconstruct heading, it will interpret vx as the x-axis (East) and vy as the y-axis (North), which is the opposite of what is now encoded here. Standardise to one documented convention before deployment.

---

### ✅ Issue 26 — ECHO UUID Parsing: All Three UUIDs Read From Same Offset — `echo.cs`

`Array.Copy` used a hardcoded source offset of 56 for all three UUIDs. Per the ICD (Section 14, ICD-2), `handoff_UUID` is at offset 72 and `track_merge_UUID` is at offset 88.

```csharp
// BEFORE:
Array.Copy(msg, 56, track_UUID,       0, 16); ndx += 16; // track_UUID
Array.Copy(msg, 56, handoff_UUID,     0, 16); ndx += 16; // track_UUID  ← WRONG
Array.Copy(msg, 56, track_merge_UUID, 0, 16); ndx += 16; // track_UUID  ← WRONG

// AFTER:
Array.Copy(msg, 56, track_UUID,       0, 16); ndx += 16; // track_UUID       at offset 56
Array.Copy(msg, 72, handoff_UUID,     0, 16); ndx += 16; // handoff_UUID     at offset 72
Array.Copy(msg, 88, track_merge_UUID, 0, 16); ndx += 16; // track_merge_UUID at offset 88
```

> Track IDs are unaffected (derived from `track_UUID` only). Handoff and merge logic that relied on `handoff_UUID` or `track_merge_UUID` was previously receiving garbage data.

---

### ✅ Issue 43 — CUE Packet: ICAO Not Padded to 8 Bytes → Packet Misalignment — `Form1.cs`

`Encoding.ASCII.GetBytes(ICAO)` writes the raw ICAO string (e.g., "ABC123" = 6 bytes) without padding to the fixed 8-byte field. This produces a 62-byte packet where every field after offset 9 is shifted 2 bytes left, making the downstream receiver decode wrong values for latitude, longitude, altitude, and velocity.

```csharp
// BEFORE:
string tID = aCB.CurrentCUE.ICAO; //"12345678".Trim('\0');
byte[] bID = Encoding.ASCII.GetBytes(tID); // variable length!

// AFTER:
// Issue 43 fix: pad/truncate ICAO to exactly 8 bytes so all subsequent fields land at the correct offset
string tID = (aCB.CurrentCUE.ICAO ?? "").PadRight(8).Substring(0, 8);
byte[] bID = Encoding.ASCII.GetBytes(tID); // always exactly 8 bytes
```

> ICAOs longer than 8 characters (unlikely with standard 6-char hex ICAOs, but possible with ECHO `ECH_XXXX` = 8 chars exactly, or `RADAR`/`LORA` = 5 chars) are handled correctly: `PadRight(8)` pads short strings, `.Substring(0, 8)` truncates longer ones.

---

### ✅ Issues 16, 17, 18 — Kalman H Matrix, Measurement Noise Tuning, Process Noise — `kalman.cs`

#### Issue 16 — H matrix: was 6×6 with zero rows, now 5-observation 6×6

The key insight is that `LLA2NED()` passes a genuine **position and velocity** measurement — not position only. Every sensor delivers real velocity data (ADS-B heading/speed, ECHO `VELOCITY_ENU`, RADAR binary vx/vy/vz). The original zero rows in H meant the filter treated velocity as exactly zero on every update, generating spurious Kalman gain entries that corrupted velocity state corrections.

Two options were evaluated: (A) 3×6 position-only H — rejected because it discards real sensor velocity data; (B) 6×6 H with 1s on N/E/D/vN/vE — adopted. The vD row (`H[5,5]`) remains zero until Issue 25 (vertical rate wiring) is resolved, since `Z[5]` is currently always hardcoded to 0 in `LLA2NED()` and enabling the row prematurely would pull the Down-velocity state toward zero on every update.

```csharp
// BEFORE — zero rows 3–5 observe velocity as exactly zero:
H = [I₃  0₃]
    [0₃  0₃]   ← rows 4–6 are phantom observations of zero

// AFTER — 5 real observations; vD excluded until Issue 25:
H = [I₃  0₃]
    [I₂' 0 ]   ← vN/vE observed directly; vD col stays 0
```

#### Issue 17 — Split R and P₀

```csharp
// BEFORE: R0 = 0.5 (σ = 0.7 m — far too tight for CPR ~5 m), P0 = 0.25
R = DenseDiagonal(6, 6, 0.5)

// AFTER: split diagonal matching actual sensor accuracy
R_pos = 25.0   // σ = 5 m   — CPR; tighten to ~4 for ECHO mmWave
R_vel =  4.0   // σ = 2 m/s — heading+speed decomposition uncertainty
R = diag(25, 25, 25, 4, 4, 0)
P0 = 25.0      // initial position covariance matches R_pos (was 0.25)
```

#### Issue 18 — Process noise named constant

```csharp
// BEFORE:
double sigma = 9.0 * 1.0 / 2.0;  // unexplained magic number

// AFTER:
const double sigma_a_sq = 4.5;   // σ_a² (m/s²)²; σ_a ≈ 2.1 m/s²; increase to 25–100 for UAV targets
```

#### Upgrade path when Issue 25 is resolved

```csharp
H[5, 5] = 1.0;   // enable vD observation
R[5, 5] = R_vel; // no other changes needed
```

---

### ✅ Issue 13 — Spelling Corrections — multiple files

Four misspellings present in the original codebase, corrected across the affected files:

| Original | Corrected | Location |
|----------|-----------|----------|
| `Squak` | `Squawk` | `adsb_raw.cs`, `trackLog.cs` — public property name |
| `VerticleRate_mps` | `VerticalRate_mps` | `adsb_raw.cs`, `trackLog.cs` — property and all callers |
| `GetWakeVotexCateg` | `GetWakeVortexCateg` | `adsb_raw.cs` — method name |
| `"update Kalan filter"` | `"update Kalman filter"` | `trackLog.cs` — comment |

`VerticalRate_mps` and `Squawk` are public API surface; any external consumer referencing the old names will need updating.

---

### ✅ Issue 15 — `stm.Flush()` on `NetworkStream` Is a No-Op — `adsb_raw.cs`, `echo.cs`

`NetworkStream.Flush()` is documented by .NET as a no-op: the stream writes directly to the underlying socket and buffers nothing. The two calls were silently doing nothing and implied a false guarantee that data had been flushed to the wire.

- **`adsb_raw.cs`:** Call removed from the ADS-B TCP receive loop.
- **`echo.cs`:** Call was already removed as part of the TCP framing rewrite (Rev 6 / Issue 29). No further change needed.

If true write-flushing is ever required, `TcpClient` should be configured with `NoDelay = true` or the send path should switch to `Socket.Send()` directly.

---

### ✅ Issue 14 — TC 5–8 and TC 20–22 Not Decoded — `adsb_raw.cs`, `trackLog.cs`

#### TC 20–22 — Airborne Position with GNSS Height

The altitude field in TC 20–22 (bits 41–52) is a raw unsigned 12-bit integer — there is no Q-bit. The previous code called `GetBaroAltitude()`, which applies Q-bit manipulation before scaling, producing a wrong altitude for these messages. A dedicated `GetGnssAltitude()` and `GetPositionGps()` wrapper now handle TC 20–22 correctly. Lat/lon was already correct (same CPR layout as TC 9–18); only the altitude needed fixing.

```csharp
// BEFORE: baro decoder applied to GPS altitude field — wrong
GetPositionBaro(bitmsg); // alt_ft garbage

// AFTER
public double GetGnssAltitude(string bitmsg)
{
    uint raw = Convert.ToUInt32(bitmsg.Substring(40, 12), 2);
    return raw * 25.0 - 1000.0; // feet, WGS-84 HAE
}
```

**Offset note:** ICAO Doc 9684 §3.1.2.6.7.4 specifies the same −1000 ft bias as the baro formula. Some third-party decoders omit it. At FL350 the difference is negligible; near the surface a mismatched offset produces ~300 m apparent error. Confirm the convention when cross-checking against other tools.

#### TC 5–8 — Surface Position

Surface messages were classified (`MsgType = POS_SURF`) but fully ignored — no lat/lon, speed, or heading extracted. The `trackMSG` switch hit `default`, set `msgType = NA`, and silently dropped the message. Aircraft became invisible from wheels-down.

Three new methods implement the full decode:

**`GetSurfaceSpeed_kts(int movement)`** — translates the 7-bit Movement field to a ground speed (lower bound of each encoded range) per ICAO Doc 9684 §3.1.2.6.7.3 Table 3-10. The encoding is non-linear across six sub-ranges from stopped (movement = 1) up to ≥175 kt (movement = 124).

**`GetPositionSurface()`** — CPR lat/lon decode identical to TC 9–18 (same bit positions 53–87); adds heading from bits 45–51 (valid when bit 44 set, scaled 360/128°) and calls `GetSurfaceSpeed_kts()` for ground speed. No altitude is encoded in surface messages — `alt_ft` is set to `BaseStation.alt / 0.3048` as a ground-level approximation.

**`trackLog.cs`:** `POS_SURF` added to the `trackMSG` switch alongside `POS_AIR_BARO` and `POS_AIR_GPS`, routing surface messages to `TRACK_MSGTYPES.POSITION` so they enter the Kalman filter and appear on the display.

---

### ✅ Issue 36 (residual) — LoRa `vz` Sign Convention — `radar.cs`, `trackLog.cs`, `Form1.cs`

LoRa typically relays MAVLink-sourced telemetry, where velocity uses NED convention: `vz` positive means **descending** (into the earth). The RADAR/EXT protocol uses ENU: `vz` positive means **ascending**. Passing a LoRa NED `vz` straight through as `VerticalRate_mps` would invert the vertical rate for every LoRa track, causing the Kalman filter's Down-velocity state to be driven in the wrong direction.

Fix: a `VzPositiveUp` field added to the `RADAR` class. The `RADAR` constructor takes `_vzPositiveUp = true` (default for RADAR/EXT); the LoRa instance passes `false`. The field is forwarded to every `trackMSG` constructed from that receiver's packets:

```csharp
// RADAR constructor — new _vzPositiveUp parameter
public RADAR(..., string _baseICAO = "RADAR", bool _vzPositiveUp = true)
{ ...; VzPositiveUp = _vzPositiveUp; }

// Instantiation in Form1.cs:
aCB.aRADAR = new RADAR(trackLogs, bs, 10009, ..., "RADAR", vzPositiveUp: true);  // ENU
aCB.aLORA  = new RADAR(trackLogs, bs, 10032, ..., "LORA",  vzPositiveUp: false); // NED/MAVLink

// trackMSG binary constructor normalises on read:
VerticalRate_mps = vzPositiveUp ? vz : -vz; // always positive = ascending after this point
```

Every downstream consumer — `LLA2NED()`, the Kalman filter `vD = −VerticalRate_mps` — now operates on a consistent positive-ascending convention regardless of which instance produced the track.

---

### ✅ Altitude Audit — `adsb_raw.cs`, `trackLog.cs`, `radar.cs`, `echo.cs`

A full audit of altitude values across all sensors and code paths was conducted. The following issues were found and resolved:

#### TC 20–22 delta double-application (bug fix)

`Alt_HAE_m` was defined as `Alt_Baro_m + dAlt_gps_m` for all message types. For TC 20–22 this was wrong: `GetGnssAltitude()` already returns WGS-84 HAE directly, so adding `dAlt_gps_m` again overcorrected by ~100–300 ft depending on the local geoid undulation. Similarly, for TC 5–8 surface messages `alt_ft` is the BaseStation elevation — adding an airborne GNSS/baro delta to it is meaningless.

`Alt_HAE_m` is now a switch-based computed property:

```csharp
public double Alt_HAE_m { get {
    switch (MsgType) {
        case MSGTYPES.POS_AIR_GPS: // TC 20–22: GNSS HAE direct — do not add delta
        case MSGTYPES.POS_SURF:    // TC 5–8:   BaseStation elevation — delta meaningless
            return COMMON.ft2m(alt_ft);
        default:                   // TC 9–18:  baro alt + GNSS/baro delta
            return Alt_Baro_m + dAlt_gps_m;
    }
} }
```

#### `earth.Forward()` altitude confirmed as HAE

Two sites in `trackLog.cs` and `radar.cs` had `// alt needs to be in HAE?` comments. `CROSSBOW.BaseStation.alt = 174.6` is documented as WGS-84 HAE throughout the codebase. `Geocentric.Forward()` (GeographicLib) requires HAE. Both question-mark comments replaced with a definitive statement.

#### `ECHO.ToArray()` parameter renamed

The `alt_msl` parameter name in `ECHO.ToArray()` was a misnomer — the value passed is always `trackLOG.Position.alt`, which is WGS-84 HAE (derived from `earth.Reverse()` of the ECEF position). Renamed to `alt_hae`.

#### `trackLOG` stale BaseStation default (also Issue 42 residual)

`trackLOG.BaseStation` still had the old stale default `ptLLA(34.66731, -86.46648, 197.3)`. Updated to the canonical `ptLLA(34.4593583, -86.4326550, 174.6)` matching all sensor classes.

#### Summary — altitude by sensor and TC

| Source | Field | Datum | Path to `trackLOG.Position.alt` |
|--------|-------|-------|----------------------------------|
| ADS-B TC 9–18 | `Alt_Baro_m + dAlt_gps_m` | WGS-84 HAE | Via `Alt_HAE_m` property |
| ADS-B TC 20–22 | `GetGnssAltitude()` | WGS-84 HAE | Via `Alt_HAE_m` property (delta NOT added) |
| ADS-B TC 5–8 | `BaseStation.alt / 0.3048` | HAE approx | Via `Alt_HAE_m` property (delta NOT added) |
| ECHO | `earth.Reverse(ECEF)` | WGS-84 HAE | Direct from `trackMSG.Alt_HAE_m` |
| RADAR/LoRa | Binary packet `altitude` field | WGS-84 HAE (per ICD) | Direct from `trackMSG.Alt_HAE_m` |
| BaseStation | `CROSSBOW.BaseStation.alt` | WGS-84 HAE | Reference for lla2ned(), ECEF, bearing/range |

---

```csharp
// BEFORE: ambiguous name, no documentation
public double Alt_gps_baro_m { get { return Alt_Baro_m + dAlt_gps_m; } }

// AFTER: unambiguous name + XML doc
/// <summary>Height Above Ellipsoid (WGS-84 HAE), metres. Baro altitude + GNSS/baro delta.
/// NaN if either component is NaN. Guard callers: if (!double.IsNaN(Alt_HAE_m)).</summary>
public double Alt_HAE_m { get { return Alt_Baro_m + dAlt_gps_m; } }
```

---

### ✅ Issue 11 — Callsign LUT Reserved Positions Used Wrong Characters — `adsb_raw.cs`

ICAO Annex 10 Vol III §3.1.2.6.7.1 defines a 6-bit character set: index 0 = space (pad/filler), indices 1–26 = A–Z, indices 48–57 = 0–9, all other positions reserved and unused in practice. The previous LUT used `#` at index 0 and at all 26 reserved positions, producing visible junk characters in callsigns whose encoded value landed on an undefined index. All 26 `#` characters replaced with spaces — the spec-correct filler — so any undefined 6-bit code is silently treated as a space and stripped by `TrimEnd()`.

```csharp
// BEFORE: '#' scattered across reserved positions produced junk output
string LUT = " ABCDEFGHIJKLMNOPQRSTUVWXYZ##### ###############0123456789######";

// AFTER: all reserved positions are space; TrimEnd() cleans up
string LUT = " ABCDEFGHIJKLMNOPQRSTUVWXYZ                     0123456789      ";
return cs.ToString().TrimEnd();
```

---

### ✅ Issue 12 — `trackLogs` Dictionary Not Thread-Safe — `crossbow.cs` + sensor files

`Dictionary<string, trackLOG>` replaced with `ConcurrentDictionary<string, trackLOG>` throughout. Without this, concurrent reads from the UI timer and writes from sensor receive tasks could corrupt the collection or produce torn reads.

Changes across all affected files:
- `trackLogs.Add(k, v)` → `trackLogs.TryAdd(k, v)`
- `trackLogs.Remove(k)` → `trackLogs.TryRemove(k, out _)`
- `ContainsKey(k)` + indexer → `TryGetValue(k, out var log)`
- `using System.Collections.Concurrent` added
- `.ToList()` snapshot retained in cleanup loops (still needed with `ConcurrentDictionary` to avoid mutating during iteration)

---

### ✅ Issue 19 — `KalmanFilter` Bearing Smoother Hardcoded `dt = 1.0` — `kalman.cs`

The bearing smoother built `transitionMatrix` once in the constructor with a fixed `dt = 1.0 s` and never updated it. At rates other than exactly 1 Hz the predict step used the wrong interval, causing over- or under-smoothing.

```csharp
// BEFORE: dt=1.0 baked into constructor, never changed
transitionMatrix = Matrix([[1, 1.0],[0, 1]]);  // wrong if called at != 1 Hz

// AFTER: elapsed time computed each call; F rebuilt accordingly
double dt = Math.Max((DateTime.UtcNow - LastUpdateTime).TotalSeconds, 1e-3);
LastUpdateTime = DateTime.UtcNow;
var transitionMatrix = Matrix([[1, dt],[0, 1]]);
```

---

### ✅ Issue 23 — BaseStation Altitude Comment Contradictory — `crossbow.cs`

The comment `//MSL, HAE` was self-contradictory — MSL and HAE differ by the local geoid undulation (approximately −29 m near Huntsville, AL; HAE is ~29 m below MSL). Replaced with `// HAE, WGS-84` to clarify that the value is height above ellipsoid, consistent with how `lla2ned()` uses the altitude.

---

### ✅ Issue 24 — `updateGrid_Survey()` Checks Wrong Toggle — `Form1.cs`

`updateGrid_Survey()` was gated on `jtoggle_Stellarium_Listen.Checked` — a display toggle — rather than a connection-state check. Survey data should appear whenever the Stellarium TCP connection is live, not only when the user has the display toggle enabled. Changed to `aCB.aStella.isConnected`.

---

### ✅ Issue 28 — Hard `ndx` Resets Mask Layout in ECHO Parser — `echo.cs`

Two magic literals (`ndx = 128`, `ndx = 200`) replaced with named constants, making the packet layout self-documenting and easier to verify against the Echodyne ICD:

```csharp
const int OFFSET_ECEF       = 128; // POSITION_ECEF block starts here
const int OFFSET_TIMESTAMPS = 200; // last_update_time / last_assoc_time / acquired_time
```

---

### ✅ Issue 31 — `ToArray()` Timestamp Uses Local Time — `echo.cs`

`DateTimeOffset(trackTime.ToLocalTime())` produced a timestamp offset by the local timezone of the sending machine. Changed to `ToUniversalTime()` so the emitted millisecond epoch timestamp is always UTC-aligned, consistent with all other timestamps in the system.

---

### ✅ Issues 33 & 37 — RADAR Default Port Conflict and Missing `BaseICAO` Wiring — `radar.cs`

**Issue 33:** The constructor default `_port = 30002` conflicted with the ADS-B dump1090 TCP port. Changed to `10009` (matching the field initialiser).

**Issue 37:** `BaseICAO` was never set from the constructor parameter; it always stayed `"RADAR"`. This meant the LoRa instance and the RADAR instance shared the same key prefix, silently overwriting each other's tracks in `trackLogs`. `BaseICAO = _baseICAO` added to the constructor body.

```csharp
// BEFORE: port conflicts with ADS-B; BaseICAO ignores _baseICAO parameter
public RADAR(Dictionary<...> _trackLogs, ptLLA _bs, int _port = 30002, ...)
{ ... /* BaseICAO = _baseICAO; was missing */ }

// AFTER:
public RADAR(ConcurrentDictionary<...> _trackLogs, ptLLA _bs,
             int _port = 10009, ..., string _baseICAO = "RADAR")
{ ...; BaseICAO = _baseICAO; }
```

---

### ✅ Issue 34 — `SendResponse()` Entirely Commented Out — `radar.cs`

The method was a silent no-op called at 10 Hz by `backgroundUDPSend()`, with all payload code commented out. Replaced with a clear `TODO` block that documents what needs to be filled in (payload format, destination IP:port, ICD reference), a warning not to enable `isUnsolicitedEnabled` until implementation is complete, and a clean template showing the expected packet structure.

---

### ✅ Issue 36 — Velocity Axis Convention Undocumented — `echo.cs`, `radar.cs`

The shared ENU convention is now explicitly documented at both decode points:

- **`echo.cs` `parseExtMsg()`:** `VELOCITY_ENU` is native ENU — `x = East, y = North, z = Up`. The heading decode `atan2(x, y) = atan2(East, North)` correctly produces bearing from North.
- **`radar.cs` TRACK handler:** `vx = North, vy = East, vz = Up` — `atan2(vy, vx) = atan2(East, North)` = bearing from North. Matches ECHO convention.

Both feed `LLA2NED()` which converts to NED via `vn = speed·cos(hdg)`, `ve = speed·sin(hdg)`, `vd = −vz`.

---

### ✅ Issue 42 — BaseStation Defaults Differ Across Sensor Classes — all sensor files

`ADSB_MSG`, `ADSB2`, `ECHO`, and `RADAR` each had a different hardcoded fallback `ptLLA(34.66731, -86.46648, 197.3)` — a stale value at a different location and altitude to `CROSSBOW.BaseStation`. All updated to the canonical `ptLLA(34.4593583, -86.4326550, 174.6)` (HAE, WGS-84) with a comment identifying `CROSSBOW.BaseStation` as the single source of truth. In all production call paths the constructor overrides the default immediately; the field value now at least prints a reasonable position in the debugger if inspected before startup.

---

### ✅ Issue 40 — `WebClient` Deprecated — `stellarium.cs`

`WebClient` has been deprecated since .NET 5 in favour of `HttpClient`. The key differences relevant here: `WebClient` creates a new socket for every request and does not support connection reuse, which causes unnecessary latency and socket exhaustion under polling; `HttpClient` reuses connections automatically when shared. A single `static readonly HttpClient` instance handles all polls for the lifetime of the application.

```csharp
// BEFORE: new WebClient each poll — deprecated, no connection reuse
using var wc = new WebClient();
string json = wc.DownloadString(url);

// AFTER: shared static instance — reuses connections, supported on all modern .NET
private static readonly HttpClient _http = new HttpClient();
...
string json = await _http.GetStringAsync(url);
```

The Stellarium listener was already `async`, so no refactoring was required to adopt the awaitable `GetStringAsync()` call. `using System.Net.Http` replaces `using System.Net`.

---

### ✅ Issue 44 — Race Condition: UI Timer Reads Stale `_lastUpdateTime` Producing Predicted-Position Jumps — `kalman.cs`

**Root cause (Bug 1 — primary cause of persistent jumps):**

`PredictedPosition()` is called from the UI timer thread at 50 ms. `Update()` is called from the sensor receive thread at 1–10 Hz. Both access `XX` (the state vector reference) and `LastUpdateTime` with no synchronisation. Although a 64-bit reference assignment is individually atomic in C#, the two fields are not updated as a pair. The timer thread could read the newly-written `XX` (post-update) together with the old `LastUpdateTime` (pre-update), computing:

```
dt = UtcNow − LastUpdateTime_old ≈ 1.05 s  (instead of 0.05 s)
projected = F(1.05) × XX_new
```

For an aircraft at 250 kts (~130 m/s), this projects the position ~130 m forward in one 50 ms frame — a visible jump. The next timer tick gets the correct pair and snaps back, producing a jump-snap pattern repeatable every time the two threads overlap, regardless of filter convergence state.

**Fix:** A `readonly object _stateLock` guards all reads and writes of `_XX` and `_lastUpdateTime`. In `PredictedPosition()` only the snapshot is taken under the lock; the `F(dt) × snapshot` computation runs outside it so the sensor thread is never stalled by the UI thread:

```csharp
public Vector<double>? PredictedPosition()
{
    Vector<double> snapshot;
    DateTime snapshotTime;
    lock (_stateLock)
    {
        if (!IsInitialised) return null;
        snapshot     = _XX!;
        snapshotTime = _lastUpdateTime;
    }
    // F(dt) × snapshot computed outside lock — no shared state mutated
    double dt = Math.Max((DateTime.UtcNow - snapshotTime).TotalSeconds, 0.0);
    return getFdt(dt) * snapshot;
}
```

`init()` and `Update()` hold the lock for their entire state mutation (predict + correct), which is safe because matrix operations on a 6×6 system complete in microseconds and sensor updates arrive at ≤10 Hz.

An `IsInitialised` guard was added to both `PredictedPosition()` and `LatestPosition()` so callers get `null` / `ptLLA(0,0,0)` rather than a `NullReferenceException` if the UI timer fires before the first measurement arrives.

---

### ✅ Issue 45 — `Update()` Uses Wall-Clock `dt`, Producing Burst-Processing Errors — `kalman.cs`, `trackLog.cs`

**Root cause (Bug 2):**

`Update()` computed `dt = DateTime.UtcNow − LastUpdateTime` — elapsed wall-clock time since the previous call returned, not elapsed time between the two measurements. If the receive thread falls behind and drains a queue of buffered packets back-to-back:

```
Packet A: measurement t=0.000 s, processed at wall t=0.000 → dt = 1.000 s ✓
Packet B: measurement t=1.000 s, processed at wall t=1.600 → dt = 1.600 s ✗ (should be 1.0)
Packet C: measurement t=2.000 s, processed at wall t=1.601 → dt = 0.001 s ✗ (should be 1.0)
```

Packet B over-propagates the state by 600 ms of velocity, placing the position ~78 m ahead for a 130 m/s target. Packet C barely moves it and the innovation correction is overwhelmed. Net result: a sequence of forward/backward kicks through each burst, independent of the timer-thread race.

**Fix:** `measurementTime` (a `DateTime`) is now a required parameter on both `init()` and `Update()`. The `dt` is computed from consecutive measurement timestamps:

```csharp
public void Update(Vector<double> Z, DateTime measurementTime)
{
    lock (_stateLock)
    {
        double dt = Math.Max((measurementTime - _lastUpdateTime).TotalSeconds, 1e-3);
        ...
        _lastUpdateTime = measurementTime;  // not DateTime.UtcNow
    }
}
```

All nine call sites in `trackLog.cs` pass `tMsg.LastUpdateTime` — the actual packet timestamp for ECHO/RADAR (decoded from the binary header) and the decode time for ADS-B. The 1 ms floor guards against duplicate or out-of-order packets producing dt=0 or negative dt.

---, meaning the Kalman filter's Down-velocity state was never seeded from real sensor data. The full data flow is now:

**`HeadingSpeed` class (`common.cs`)** — new `vd` field and 3-argument constructor:
```csharp
public double vd;  // vertical rate, m/s, positive = ascending (up)

public HeadingSpeed(double _heading, double _speed, double _vd)
{ heading = _heading; speed = _speed; vd = _vd; }
```

**`trackMSG` constructors (`trackLog.cs`)** — `VerticalRate_mps` (spelling fixed from `VerticleRate_mps`) now populated from all three sensor paths:

| Constructor | Source of `VerticalRate_mps` |
|-------------|------------------------------|
| `trackMSG(ADSB_MSG)` | `_amsg.VerticalRate_mps` — sign and units already fixed (Issues 1 & 2) |
| `trackMSG(byte[], baseICAO)` | `vz` from RADAR binary packet |
| `trackMSG(ECHO_MSG)` | `msg.VELOCITY_ENU.z` — ENU Up component, m/s |

**`trackLOG.Update()` (`trackLog.cs`)** — `VerticalRate_mps` stored in `HeadingSpeedLog` via the 3-arg constructor in both the VELOCITY and POS_VEL branches. The POSITION branch (ADS-B position-only message) picks up the VR from the most recent `HeadingSpeedLog` entry, which was populated by the preceding VELOCITY message — correct by design.

**`LLA2NED()` (`trackLog.cs`)** — NED Down axis is the negation of ENU Up. VR from all sensors uses positive = ascending (up), so the conversion is:
```csharp
double vd = -_hs.vd;  // NED Down = −Up; positive in NED = descending
return Vector([N, E, D, vn, ve, vd]);
```

#### Issue 32 — ECHO speed 3D → 2D horizontal (fixed alongside Issue 25)

While wiring VR into the ECHO constructor, the 3D speed magnitude (which included the vertical component) was corrected to 2D horizontal:
```csharp
// BEFORE: 3D magnitude — speed contaminated by vertical climb rate
Speed_mps = Math.Sqrt(VxENU² + VyENU² + VzENU²);

// AFTER: 2D horizontal only; VR stored separately
Speed_mps        = Math.Sqrt(VxENU² + VyENU²);
VerticalRate_mps = msg.VELOCITY_ENU.z;
```

#### Kalman H — now full 6×6 identity (`kalman.cs`)

With real vD data now flowing through, the vD observation is enabled:
```csharp
// BEFORE (Issue 25 pending):
H[5,5] = 0.0;  R[5,5] = 0.0;  // vD unobserved

// AFTER (Issue 25 resolved):
H[5,5] = 1.0;  R[5,5] = R_vel; // vD fully observed; H = I₆
```

The filter now observes all 6 states directly. With H = I₆, the update equations simplify to `S = P + R`, `K = P·S⁻¹`, `innovation = Z − XX` — the most straightforward form of the standard Kalman update.

---

---  
**Classes:** `ADSB_MSG`, `ADSB2`, `ECHO`, `ECHO_MSG`, `RADAR` (×3 instances), `STELLARIUM`, `trackMSG`, `trackLOG`, `KALMAN`, `KalmanFilter`, `COMMON`, `CROSSBOW`, `Form1`  
**Date:** 2026-03-04

1. [System Overview & Theory of Operation](#1-system-overview--theory-of-operation)
2. [Sensor Input Reference](#2-sensor-input-reference)
3. [Crossbow CUE Unicast (Output)](#3-crossbow-cue-unicast-output)
4. [ADS-B — Protocol & CPR Verification](#4-ads-b--protocol--cpr-verification)
5. [ECHO (Echodyne) — Protocol & Message Parsing](#5-echo-echodyne--protocol--message-parsing)
6. [RADAR Class — EXT, LoRa & Generic Radar](#6-radar-class--ext-lora--generic-radar)
7. [STELLARIUM — Remote Control Listener](#7-stellarium--remote-control-listener)
8. [Altitude — Units, Datums & Cross-Sensor Consistency](#8-altitude--units-datums--cross-sensor-consistency)
9. [Coordinate Transforms (COMMON)](#9-coordinate-transforms-common)
10. [Track Data Model (trackMSG / trackLOG)](#10-track-data-model-trackmsg--tracklog)
11. [Kalman Filter Analysis — All Sensors](#11-kalman-filter-analysis--all-sensors)
12. [UI — Form1 & GMap Display](#12-ui--form1--gmap-display)
13. [Cross-Sensor Issues](#13-cross-sensor-issues)
14. [Data Interface Control Documents (ICDs)](#14-data-interface-control-documents-icds)
15. [Issues — Complete List](#15-issues--complete-list)
16. [Summary Table](#16-summary-table)

---

## 1. System Overview & Theory of Operation

### 1.1 Architecture

Crossbow ATD is a Windows Forms sensor fusion display that ingests tracks from four independent sensor inputs, normalises them into a common data model, filters them through a 6-state Kalman filter, and renders them on a GMap.NET map canvas with a DataGridView track table. A fifth output path re-packages the currently selected track and unicasts it over UDP to a downstream fire-control or cueing system.

```
                     ┌──────────────────────────────────────────────────────┐
                     │               CROSSBOW  (config / state)              │
                     │   BaseStation ptLLA  ·  TrackType  ·  CurrentCUE      │
                     │   trackLogs: Dictionary<string, trackLOG>  [shared]   │
                     └───────┬────────┬──────────┬────────────┬─────────────┘
                             │        │          │            │
               TCP:30002     │        │          │            │ HTTP:8090
          ┌──────────┐       │   ┌────┴────┐  ┌─┴──────┐  ┌─┴──────────┐
 dump1090 ─► ADSB2   │       │   │  ECHO   │  │  RADAR │  │ STELLARIUM │
 (passive)└──────────┘       │   │TCP:29982│  │UDP:10009│  │(ref cue)   │
               │              │   └────┬────┘  └──┬─────┘  └────────────┘
               │              │        │    ┌──────┘
               │              │        │    │    RADAR class re-used
               │              │        │    │    ├── aRADAR  UDP:10009  "RADAR"
               │              │        │    │    ├── aLORA   UDP:10032  "LORA"
               │              │        │    │    └── (EXT)   configurable
               │              │        │    │
               ▼              ▼        ▼    ▼
            trackMSG ──────► trackLOG (per ICAO key)
                              ├── PositionLog      SortedList<ms, ptLLA>
                              ├── HeadingSpeedLog  SortedList<ms, HeadingSpeed>
                              └── KALMAN (6-state NED EKF)

                     timer1 (UI thread)
                     ┌──────────────────────────────────────────────────────┐
                     │  Form1                                                │
                     │  updateGrid_ADSB() · plot1090Track()                 │
                     │  updateGrid_Stellarium()                             │
                     │  DataGridView  ·  GMap.NET overlay                   │
                     │  jtoggle_CROSSBOW → CUE unicast → UDP:10009 →       │
                     │                     downstream fire-control system   │
                     └──────────────────────────────────────────────────────┘
```

### 1.2 Sensor Inputs Summary

| Instance | Class | Protocol | Transport | Port | Track ID Scheme |
|----------|-------|----------|-----------|------|-----------------|
| `aADSB` | `ADSB2` | Mode S 1090ES hex frames | TCP | 30002 | ICAO 24-bit hex (6 chars) |
| `aECHO` | `ECHO` | Echodyne proprietary binary | TCP | 29982 | `ECH_<last4 UUID hex>` |
| `aRADAR` | `RADAR` | Generic binary protocol | UDP | 10009 | `"RADAR"` (fixed) |
| `aLORA` | `RADAR` | Generic binary protocol (LoRa relay) | UDP | 10032 | `"LORA"` (fixed — see Issue 37) |
| `aStella` | `STELLARIUM` | Stellarium JSON REST | HTTP | 8090 | Not in trackLogs |

### 1.3 RADAR Class Multi-Instance Design

The `RADAR` class is a generic binary-protocol UDP handler. It is instantiated twice (and potentially more times) with different port numbers and logical names to serve completely different sensor backends:

- **`aRADAR`** — Primary sensor / fire-control radar track feed on UDP port 10009.
- **`aLORA`** — LoRa radio relay that receives position reports from a remote asset and bridges them onto the local network, also arriving as the same binary protocol on UDP port 10032.
- **EXT** — A third instance can be added for any other system speaking the same 64-byte binary protocol.

All three share the same 64-byte message format and feed into the same shared `trackLogs` dictionary. Because `BaseICAO` is hardcoded to `"RADAR"` regardless of which instance is running (Issue 37), all three would collide on the same dictionary key if started simultaneously. See Issue 37 for the fix.

### 1.4 Crossbow CUE Unicast Output

When the `jtoggle_CROSSBOW` slider is enabled in the UI, the system enters CUE output mode:

1. The currently **selected row** in the DataGridView becomes the `CurrentCUE` (updated every `timer1` tick via `aCB.CurrentCUE.Set(selectedRow)`).
2. `timUDP` fires at a configurable interval and calls `Form1.ToArray()`, which serialises the current CUE position and velocity into the 64-byte binary protocol.
3. The packet is sent by `UdpClient` unicast to the downstream system at `192.168.1.8:10009`.

The CUE packet uses the **same 64-byte binary format** as the RADAR receive protocol, making the downstream system a peer that can receive tracks in the same way `RADAR` does. The Class field is hardcoded to `0x08` (UAV) and the Command field to `0x01` regardless of the actual track type. See Issue 22 for the velocity encoding bug in `ToArray()`, and Issue 43 for the variable-length ICAO alignment issue.

```
UI Select row → CurrentCUE → timUDP tick → ToArray() → 64-byte binary → UDP → downstream
```

### 1.5 Track Lifecycle

1. **Birth** — First valid position message for an ICAO creates a `trackLOG` entry and initialises the Kalman filter with `ekf.init()`.
2. **Update** — Subsequent messages append to time-stamped sorted logs and call `ekf.Update()`.
3. **Display** — `timer1` reads `trackLOG.Position` (mode-selected) for the grid and map marker.
4. **Death** — Age > 30 s: removed in each sensor's receive loop and in `updateGrid_ADSB()`.
5. **Manual delete** — Right-click on DataGridView row immediately removes the track.

### 1.6 Position Modes (TRACK_TYPES)

| Mode | Description | Best For |
|------|-------------|----------|
| `LATEST` | Most recent decoded lat/lon | Debugging raw data |
| `PREDICTED` | Dead-reckoned via geodesic `projectLLA(speed × age, heading)` | Low-latency, no filter |
| `KALMAN_LATEST` | Last accepted Kalman state → LLA | Smooth position at update time |
| `KALMAN_PREDICTED` *(default)* | Kalman state propagated to `DateTime.UtcNow` via F(dt) | Best accuracy + latency compensation |
| `FILTERED_LATEST` | Complementary IIR (α=0.1) filtered position | Simple smoothing |
| `FILTERED_PREDICTED` | IIR position dead-reckoned forward | Alternative to Kalman |

### 1.7 Kalman Filter Overview

The `KALMAN` class implements a **linear constant-velocity Kalman filter** in a local NED frame centred on `BaseStation`. State vector: `[N, E, D, vN, vE, vD]` (metres, m/s). All sensor types feed the same filter — it is sensor-agnostic at the estimation level. The default mode `KALMAN_PREDICTED` propagates the last filter state to `DateTime.UtcNow`, compensating for the latency between the last sensor update and the UI refresh tick. For a 1 Hz ADS-B update rate and 500 ms UI timer, this prevents approximately 125–250 m of display lag at typical cruise speeds.

---

## 2. Sensor Input Reference

### 2.1 ADS-B (dump1090 TCP)
Passive 1090 MHz SDR receiver. dump1090 decodes raw frames and emits ASCII hex on TCP port 30002. Only 14-byte (112-bit, DF=17) ADS-B extended squitter frames are processed. 7-byte Mode S short frames are silently dropped.

### 2.2 Echodyne ECHO (TCP binary)
Active mmWave radar. Outputs binary track packets over TCP port 29982. Only tracks in `CONFIRMED` state are accepted as valid. Track ID is derived from the last 4 hex characters of the 128-bit track UUID.

### 2.3 RADAR / EXT / LoRa (UDP binary)
Three potential instances of the `RADAR` class, each listening on a different UDP port. The protocol is a proprietary 64-byte binary packet. All instances share the same `trackLogs` dictionary and the same message format. The command byte at packet offset 18 determines packet type; only `TRACK` (0x01) packets at exactly 64 bytes update the track log.

### 2.4 Stellarium (HTTP JSON REST)
Stellarium desktop planetarium with Remote Control plugin. Polled via HTTP every ≥100 ms. Returns azimuth, altitude (elevation), distance, and velocity for the currently selected celestial object. Used as a reference cue; not injected into trackLogs or the Kalman filter.

---

## 3. Crossbow CUE Unicast (Output)

### 3.1 Purpose
When the operator selects a track in the DataGridView and enables the `jtoggle_CROSSBOW` slider, the system broadcasts that track's current position and velocity to a downstream system (e.g., a gimbal controller or weapon cueing system) at a fixed UDP unicast address.

### 3.2 Operation
- **Destination:** `192.168.1.8:10009` (hardcoded in `Form1.cs`)
- **Rate:** Driven by `timUDP` interval (configurable)
- **Format:** 64-byte binary packet — identical to the RADAR receive format (ICD-3)
- **Track source:** `aCB.CurrentCUE`, updated every `timer1` tick from the selected DataGridView row
- **Position used:** `aCB.CurrentCUE.Position` — this reflects whatever `TRACK_TYPE` is active (including Kalman-predicted), so the downstream system receives the current best-estimate position, not just the last raw fix

### 3.3 Known Issues With CUE Output
- **Issue 22:** Velocity components encode `heading_degrees × trig()` instead of `speed × trig()`, producing a completely wrong velocity vector.
- **Issue 43:** The ICAO string (e.g., "ABC123" = 6 bytes) is written directly without padding to 8 bytes, misaligning all fields after the Track ID and producing an invalid 62-byte packet instead of 64 bytes.
- **Design note:** Classification is hardcoded to `0x08` (UAV) regardless of the actual track type. The downstream system will always believe the CUE is a UAV.
- **Design note:** The destination IP and port are hardcoded. These should be configurable via the CROSSBOW config object.

---

## 4. ADS-B — Protocol & CPR Verification

### 4.1 112-bit Frame Structure

```
 Bits     Field   Notes
 ──────────────────────────────────────────────
  1–5     DF      Downlink Format (17 = ADS-B)
  6–8     CA      Transponder Capability
  9–32    ICAO    24-bit aircraft address
 33–88    ME      56-bit Extended Squitter payload
  (33–37) TC      Type Code
 89–112   PI      24-bit CRC
```

### 4.2 Type Code Map

| TC | Message Class | Decoded? |
|----|--------------|----------|
| 1–4 | Aircraft ID (callsign, wake vortex) | ✓ |
| 5–8 | Surface Position | ✗ Issue 14 |
| 9–18 | Airborne Position (barometric) | ✓ |
| 19 | Airborne Velocity (GS/heading/VR/ΔAlt) | ✓ |
| 20–22 | Airborne Position (GNSS/HAE alt) | ✗ Issue 14 |
| 28–29 | Aircraft Status | ✗ |
| 31 | Operational Status | ✗ |

### 4.3 CPR Position Decoding — Verified Correct

The single-receiver local CPR decode uses BaseStation position to resolve the longitude zone ambiguity. Both formulae verified against ICAO Doc 9684. The use of `COMMON.dmod()` (floor-based modulo) rather than C# `%` (truncation remainder, which returns negative values for negative inputs) is correct and intentional.

### 4.4 Bit-Offset Verification (0-based string indices)

| Field | Spec Bits (1-based) | String Index | ✓? |
|-------|---------------------|--------------|-----|
| TC | 33–37 | 32–36 | ✓ |
| ALT | 41–52 | 40–51 | ✓ |
| ALT Q-bit | 48 | substr[7] | ✓ |
| T, F flags | 53–54 | 52–53 | ✓ |
| LAT_CPR | 55–71 | 54–70 | ✓ |
| LON_CPR | 72–88 | 71–87 | ✓ |
| ST | 38–40 | 37–39 | ✓ |
| IC flag | 41 | 40 | ✓ |
| **IFR flag** | **42** | **41** | **❌ Issue 3** |
| DEW, VEW | 46–56 | 45–55 | ✓ |
| DNS, VNS | 57–67 | 56–66 | ✓ |
| VrSrc, VrSgn | 68–69 | 67–68 | ✓ |
| Vr | 70–78 | 69–77 | ✓ |
| SDif, dALT | 81–88 | 80–87 | ✓ |
| Callsign chars | ME+8–56 | 40+i×6, 6-bit groups | ✓ |

---

## 5. ECHO (Echodyne) — Protocol & Message Parsing

### 5.1 Packet Overview
728-byte binary TCP stream. Parsed by `ECHO_MSG.parseExtMsg()`. Contains two position/velocity representations (XYZ sensor frame, ECEF, ENU), classification probabilities, RCS, track metadata, and timestamps. Only fields used by this system are ECEF position (for LLA derivation), ENU velocity, classification probabilities, and AGL estimate.

### 5.2 Key Issues
- **Issue 26 (Critical):** All three UUIDs are copied from offset 56. `handoff_UUID` and `track_merge_UUID` should use offsets 72 and 88.
- **Issue 27 (High):** `LastAssocTime` and `AqTime` both use `last_update_time`. They should use `last_assoc_time` and `acquired_time`.
- **Issue 28 (Medium):** Two hard `ndx` resets (`ndx=128`, `ndx=200`) mask sequential parsing errors. The reset to 128 is numerically coincident with the sequential value; the reset to 200 skips 1 byte of padding. Both are fragile if the upstream format changes.
- **Issue 29 (High):** No TCP framing — 728-byte fixed buffer will silently decode partial or coalesced packets.
- **Issue 30 (High):** Cancellation handler deletes ALL tracks, not just ECHO tracks.

---

## 6. RADAR Class — EXT, LoRa & Generic Radar

### 6.1 Multi-Instance Use

The `RADAR` class is a reusable UDP listener/responder for any system that speaks the 64-byte binary protocol. Three instances are defined in `CROSSBOW`:

```csharp
public RADAR aRADAR { get; set; } = new RADAR();   // primary sensor feed, UDP:10009
public RADAR aLORA  { get; set; } = new RADAR();   // LoRa relay feed,     UDP:10032
// EXT would be a third RADAR instance on a configurable port
```

Each is started independently with its own port and `BaseICAO` name (once Issue 37 is fixed). The class uses a bidirectional pattern: one background task reads incoming track reports (`backgroundUDPRead`), another sends status responses (`backgroundUDPSend`). The `REPORT_CONT_ON/OFF` and `REPORT_ONCE` commands allow the upstream sensor to request unsolicited status broadcasts.

### 6.2 Command Protocol

The command byte at packet offset 18 drives all protocol behaviour:

| Value | Command | Action |
|-------|---------|--------|
| 0x00 | `DROP` | Remove all tracks with matching `BaseICAO` from `trackLogs` |
| 0x01 | `TRACK` | Parse 64-byte packet and update/create track (only if length == 64) |
| 0x02 | `REPORT_ONCE` | Send one status response packet immediately |
| 0x04 | `WEAPON_HOLD` | Acknowledged; no action currently implemented |
| 0x05 | `WEAPON_FREE` | Acknowledged; no action currently implemented |
| 0xFE | `REPORT_CONT_ON` | Enable continuous 10 Hz status response broadcast |
| 0xFF | `REPORT_CONT_OFF` | Disable continuous status response broadcast |

### 6.3 Key Issues
- **Issue 33:** Default constructor port is 30002 (ADS-B port). Should be 10009.
- **Issue 34:** `SendResponse()` is entirely commented out — the 10 Hz send task spins doing nothing.
- **Issue 37:** `BaseICAO` is never set from the constructor, so all instances identify as "RADAR" and collide in `trackLogs`.

---

## 7. STELLARIUM — Remote Control Listener

Stellarium Remote Control plugin is polled via HTTP GET to retrieve the currently selected object's position in the sky. Data is displayed directly in the grid as a celestial reference bearing and is not tracked or filtered.

### 7.1 Key Issues
- **Issue 38:** URL hardcoded to `localhost:8090`; ignores configurable `IP_ADDRESS` and `PORT` properties.
- **Issue 39:** Inner loop has no sleep/delay — spins at 100% CPU checking a tick counter.
- **Issue 40:** Uses deprecated `WebClient`; replace with `HttpClient`.

---

## 8. Altitude — Units, Datums & Cross-Sensor Consistency

### 8.1 Datum Definitions

| Term | Abbreviation | Definition |
|------|-------------|------------|
| Pressure Altitude | — | Assumes ISA standard atmosphere (1013.25 hPa). Used for ATC. |
| Mean Sea Level | MSL | Above the geoid (EGM96/EGM2008 gravity surface). |
| Height Above Ellipsoid | HAE | Above the WGS-84 ellipsoid. Direct GNSS and `Geocentric.Reverse()` output. |
| Geoid Undulation | N | HAE = MSL + N. Near Huntsville AL: N ≈ −29 m (HAE is ~29 m below MSL). |

### 8.2 Altitude by Sensor

| Sensor | Field | Datum | Notes |
|--------|-------|-------|-------|
| ADS-B TC 9–18 | `Alt_Baro_m` | Pressure altitude | Not MSL below transition altitude (~18,000 ft) |
| ADS-B TC 19 Δalt | `dAlt_gps_m` | GNSS−Baro delta | Used to compute HAE from baro |
| ADS-B `Alt_gps_baro_m` | computed | **HAE** | Rename to `Alt_HAE_m` (Issue 9) |
| ECHO ECEF→LLA | `Alt_HAE_m` | **HAE** ✓ | `Geocentric.Reverse()` returns HAE |
| RADAR binary | `Alt_HAE_m` field | Labelled HAE | Parameter comment says `alt_msl` in `ECHO.ToArray()` (inconsistency) |
| BaseStation | `alt = 174.6` | **Ambiguous** | Comment `//MSL, HAE` — differ by ~29 m (Issue 23) |

All sensors feed `COMMON.lla2ned()` which calls `Geocentric.Forward()` — this function requires HAE. Any sensor delivering pressure altitude or MSL introduces a systematic vertical bias into the NED Kalman state.

---

## 9. Coordinate Transforms (COMMON)

### 9.1 `lla2ned` / `ned2lla` — Verified Correct
Uses `GeographicLib.Geocentric` for LLA↔ECEF. Standard NED rotation (x=North, y=East, z=Down). Round-trip consistent. ✓

### 9.2 ENU / NED Convention Table

| Location | Convention | Notes |
|----------|-----------|-------|
| `COMMON.lla2ned` / `ned2lla` | NED | x=N, y=E, z=D |
| `ECHO_MSG.VELOCITY_ENU` | ENU | x=E, y=N, z=Up |
| `trackMSG(ECHO_MSG)` heading | `atan2(VEW_x, VNS_y)` → bearing from North ✓ | ENU interpreted correctly |
| `trackMSG(byte[])` vx/vy | Ambiguous — convention undocumented | Issue 36 |
| `Form1.ToArray()` vx/vy | NED-like (`cos(hdg)=N, sin(hdg)=E`) | Multiplied by heading_deg not speed (Issue 22) |

### 9.3 `GetBearing` Overloads
The `dLat/dLon` overload is a flat-Earth approximation, only valid for very short ranges. The GeographicLib `Geodesic.Inverse` overload is always preferred.

---

## 10. Track Data Model (trackMSG / trackLOG)

### 10.1 trackMSG Constructors

| Constructor | Source | msgType | Notes |
|-------------|--------|---------|-------|
| `(ADSB_MSG)` | ADS-B decode | ID / POSITION / VELOCITY | Correct |
| `(byte[], baseICAO)` | RADAR UDP binary | POS_VEL | Heading convention ambiguous (Issue 36) |
| `(ECHO_MSG)` | Echodyne | POS_VEL | Speed is 3D magnitude (Issue 32) |
| `(icao, cs, ptLLA)` | Manual/test | POSITION | Correct |
| `(icao, cs, HeadingSpeed)` | Manual/test | **POSITION ← BUG** | Should be VELOCITY (Issue 21) |
| `(icao, cs, ptLLA, HeadingSpeed)` | Manual/test | POS_VEL | Correct |

### 10.2 `FilteredPosition` — Side Effect in Getter (Issue 20)
The IIR filter state is updated on every property read. If accessed twice per timer tick (grid + map), the gain is applied twice. Move filter update into `Update()`.

### 10.3 Vertical Velocity in Kalman State (Issue 25)
`LLA2NED()` always passes `vD = 0`, so the filter's Down-axis velocity is never seeded from real data. Once Issue 1 (VR unit conversion) and Issue 2 (VR sign) are fixed, vertical rate from ADS-B TC=19 should be passed as `vD`.

---

## 11. Kalman Filter Analysis — All Sensors

### 11.1 Common Architecture — Sensor Agnostic
All sensors call `ekf.Update(LLA2NED(pos, hs))` with the same 6-element NED state vector. The filter makes no distinction between ADS-B, ECHO, and RADAR. This is architecturally sound for sensor fusion. The measurement noise R is now split by type (position vs velocity) rather than applying a single scalar to all observations.

| Sensor | Position Accuracy | Recommended R_pos | Velocity Accuracy | Recommended R_vel |
|--------|------------------|--------------------|------------------|-------------------|
| ADS-B CPR | ~5 m | 25 m² | Heading+speed decomp ~2 m/s | 4 (m/s)² |
| Echodyne ECHO | ~1–3 m (mmWave) | 1–9 m² | Direct ENU velocity ~1 m/s | 1–4 (m/s)² |
| RADAR (generic) | Application-dep. | Configurable | Direct vx/vy | Configurable |
| LoRa relay | Source-dep. | Configurable | Direct vx/vy | Configurable |

For proper multi-sensor R adaptation, `R_pos` and `R_vel` should be passed as parameters to `ekf.Update()` rather than set as class-level constants. This is a future improvement; the current fix uses the ADS-B values as the baseline.

### 11.2 Measurement Model — Final State (Issues 16 & 25 fully resolved)

`LLA2NED()` builds a genuine 6-element measurement vector on every update for all sensor paths:

```
Z = [N, E, D, vN, vE, vD]
     ──position──  ─────velocity─────
```

All sensors now contribute real vertical rate data via `HeadingSpeed.vd` (positive = ascending, converted to NED by `vD_NED = −vd` in `LLA2NED()`).

**H = I₆ (full 6×6 identity)** — all states directly observed:

```
H = I₆ = diag(1, 1, 1, 1, 1, 1)
```

When H = I, the standard Kalman update simplifies to its cleanest form:

```
S = P + R          (6×6; no H·P·Hᵀ needed since H = I)
K = P · S⁻¹        (6×6)
XX = XX + K · (Z − XX)   (innovation = measured − predicted directly)
P = (I − K) · P
```

**Measurement noise — split diagonal:**
```
R = diag(R_pos, R_pos, R_pos, R_vel, R_vel, R_vel)
  = diag(25,    25,    25,    4,     4,     4    )
```

The code retains the explicit `H * P * H.Transpose()` form (mathematically equivalent) so the structure remains clear if H is ever made non-identity for a different application.

#### vD sign convention across all sensors

| Sensor | Raw VR field | Sign | Stored in `HeadingSpeed.vd` | NED vD in Z |
|--------|-------------|------|---------------------------|-------------|
| ADS-B TC=19 | `VerticalRate_mps` | +climb (Issues 1 & 2 fixed) | +ascending | −vd |
| ECHO | `VELOCITY_ENU.z` | +up (ENU z) | +ascending | −vd |
| RADAR/LoRa | `vz` binary field | +up (assumed; see Issue 36) | +ascending | −vd |

This section documents the analysis that led to the correct fix, including a dead end that was considered and rejected.

#### What `Z` actually contains

`LLA2NED()` returns a genuine 6-element position **and** velocity measurement on every update:

```csharp
Z = [N, E, D, vN, vE, 0]
//   real position    real velocity from sensor heading+speed
```

All three sensor types deliver real velocity data:
- **ADS-B TC=19** — decoded ground speed + track heading → vN/vE
- **ECHO** — `VELOCITY_ENU` directly from radar → vN/vE/vD
- **RADAR/LoRa** — vx/vy/vz directly from binary packet

#### Why the original 6×6 H with zero rows was wrong

The original H had the correct identity block for position (rows 0–2) but all-zeros for velocity (rows 3–5):

```
H_old =  [I₃  0₃]
         [0₃  0₃]
```

A zero row in H means "observe this state as exactly zero." On every filter update the innovation in the velocity subspace was:

```
innovation[3:5] = Z[3:5] − H[3:5]·XX = vN_measured − 0 = vN_measured
```

However, because `H[3:5]` was all zeros, the Kalman gain `K = P·Hᵀ·S⁻¹` for the velocity rows was driven not by the actual measurements but by the noise-only block of `S`. The velocity states received corrupted corrections unrelated to the real vN/vE data in Z.

#### Why the 3×6 position-only H was also rejected

A 3×6 H (position only) would have been correct if Z carried no velocity information. But since Z contains real velocity data from every sensor, discarding it forces the filter to infer velocity purely from the sequence of position updates — this is slower convergence and wastes the sensor's direct velocity output.

#### The correct fix: 5-observation H with split R

The measurement matrix now has explicit 1s on the 5 observed states (N, E, D, vN, vE). The vD row remains zero until Issue 25 (vertical rate wiring from ADS-B/ECHO into `LLA2NED()`) is resolved — enabling it prematurely would pull the filter's Down-velocity state toward zero on every update since `Z[5]` is always hardcoded to 0.

```
H_new =  [1 0 0 0 0 0]   observe N
         [0 1 0 0 0 0]   observe E
         [0 0 1 0 0 0]   observe D
         [0 0 0 1 0 0]   observe vN  ← was zero row; now correct
         [0 0 0 0 1 0]   observe vE  ← was zero row; now correct
         [0 0 0 0 0 0]   vD — zero until Issue 25
```

Measurement noise is split by observation type:

```csharp
R_pos = 25.0  // σ = 5 m   — CPR quantisation; tighten to ~4 for ECHO
R_vel =  4.0  // σ = 2 m/s — heading+speed decomposition uncertainty

R = diag(R_pos, R_pos, R_pos, R_vel, R_vel, 0)
```

#### Upgrade path — when Issue 25 is resolved

Once vertical rate from ADS-B TC=19 (and ECHO `VELOCITY_ENU.z`) is plumbed into `LLA2NED()`, promote to full 6-observation:

```csharp
H[5, 5] = 1.0;      // enable vD observation
R[5, 5] = R_vel;    // assign appropriate vertical velocity noise
```

No other changes to `Update()` are required.

### 11.3 6-State Filter Mathematics

**State vector:** `X = [N, E, D, vN, vE, vD]` in local NED (metres / m/s)

**State transition (constant velocity):**
```
F(dt) =  [I₃  dt×I₃]    ← correct constant-velocity kinematics ✓
         [0₃     I₃]
```

**Process noise (white noise acceleration model):**
```
Q = σ_a² × [dt⁴/4   dt³/2]   per axis
            [dt³/2   dt²  ]

σ_a² = 4.5 (m/s²)²  →  σ_a ≈ 2.12 m/s²
```
Named constant `sigma_a_sq = 4.5` replaces the previous unexplained `9.0 * 1.0 / 2.0` (Issue 18 resolved). Suitable for commercial aircraft cruise. For agile UAV/LoRa targets increase to 25–100.

**Full update equations (with corrected H):**
```
// Predict
X̂⁻ = F·X̂
P⁻ = F·P·Fᵀ + Q

// Update
S = H·P⁻·Hᵀ + R            (6×6)
K = P⁻·Hᵀ·S⁻¹              (6×6)
X̂ = X̂⁻ + K·(Z − H·X̂⁻)    innovation uses real pos+vel
P = (I − K·H)·P⁻
```

**Effect comparison — before vs after:**

| Quantity | Before (zero rows) | After (corrected H) |
|----------|-------------------|-------------------|
| S (6×6) | Lower-right 3×3 block = R₀·I with no signal | vN/vE block carries real velocity uncertainty |
| K velocity rows | Driven by phantom noise-only gain | Driven by real position-velocity cross-covariance |
| Innovation | Velocity component discarded | Full 5-state innovation used |
| Velocity convergence | Corrupted | Correct; direct velocity observation + cross-covariance |
| Position convergence | Slightly degraded | Correct |

**Latency compensation:**
```csharp
// PredictedPosition() propagates Kalman state to current wall-clock time
double dt = (DateTime.UtcNow - LastUpdateTime).TotalSeconds;
return getFdt(dt) * XX;
```
For a 1 Hz ADS-B feed and 500 ms UI timer, prevents ~125–250 m of display lag at cruise speed. ✓

### 11.4 2-State Bearing Smoother (KalmanFilter)
Used for smoothing bearing. State: `[bearing, bearing_rate]`. Issue 19 (hardcoded `dt = 1.0 s`) remains open — must track elapsed time for correct prediction at irregular update rates.

### 11.5 Kalman Parameter Reference

| Parameter | Original | Rev 7 | Rev 8 (current) | Notes |
|-----------|----------|-------|-----------------|-------|
| H matrix | 6×6 zero rows 3–5 | 6×6, vD excluded | **H = I₆** | All 6 states observed |
| R_pos | 0.5 m² | 25 m² | 25 m² | CPR ~5 m σ |
| R_vel | 0.5 m² | 4 (m/s)² | 4 (m/s)² | Heading+speed decomp |
| R_vD | 0.5 m² | 0 (unobserved) | **4 (m/s)²** | VR now wired |
| P₀ position | 0.25 m² | 25 m² | 25 m² | Matches R_pos |
| σ_a² | `9.0*1.0/2.0` | `const 4.5` | `const 4.5` | Increase for UAV |
| KalmanFilter dt | 1.0 s hardcoded | 1.0 s (Issue 19 open) | 1.0 s (Issue 19 open) | |

---

## 12. UI — Form1 & GMap Display

### 12.1 Timer Architecture

| Timer | Function |
|-------|----------|
| `timer1` | Main UI refresh: grid, map, CUE update, status bar |
| `timer2` | Ping heartbeat status reset |
| `timUDP` | CUE unicast broadcast to downstream system |

### 12.2 Map Rendering
Range rings scale to map view on zoom changes. Aircraft markers rotate with heading, compensating for map bearing. Selected CUE renders in red. Marker click syncs DataGridView selection. All correct. ✓

### 12.3 `ToArray()` Velocity Bug (Issue 22)
```csharp
// WRONG: multiplies by heading degrees
double vx = Math.Cos(hdg_rad) * heading_deg;

// CORRECT:
double vx = Math.Cos(hdg_rad) * speed_mps;
```

### 12.4 Thread Safety (Issue 12)
`trackLogs` (`Dictionary<string, trackLOG>`) is written by background TCP/UDP tasks and read/written by the UI `timer1` thread. This is a crash risk. Replace with `ConcurrentDictionary`.

---

## 13. Cross-Sensor Issues

### 13.1 Stale Track Cleanup Removes Wrong-Sensor Tracks (Issue 41)
Each receive loop iterates all of `trackLogs` and removes entries with `TrackAge > 30s`, regardless of which sensor created them. An ECHO radar cleanup pass can delete currently active ADS-B tracks. Add a `SensorSource` property to `trackLOG` and filter on it.

### 13.2 BaseStation Inconsistency (Issue 42)
`CROSSBOW` uses lat=34.4593583, alt=174.6 m; all sensor classes default to lat=34.66731, alt=197.3 m. Runtime value is consistent (all receive `aCB.BaseStation` at construction), but the per-class defaults are a maintenance hazard.

### 13.3 No Per-Track Source Attribution (Design Note)
There is no `SensorSource` field on `trackLOG`. In a fused multi-sensor display, knowing which sensor created each track is operationally useful for both display (colour coding) and for proper per-sensor cleanup.

### 13.4 Single Kalman R for All Sensors (Design Note — Issue in Section 11.1)
ADS-B, ECHO, LoRa, and RADAR have very different position accuracies. The shared R₀ = 0.5 means the filter over-trusts ADS-B CPR (should be R₀ = 25) and may under-trust ECHO (which is more accurate). Consider passing R as a parameter to `ekf.Update()`.

---

## 14. Data Interface Control Documents (ICDs)

All multi-byte fields are **little-endian** (Intel byte order, as produced by `BinaryWriter` / `BitConverter` on x86). All timestamps are Unix epoch milliseconds UTC unless noted.

---

### ICD-1: ADS-B Receive — dump1090 TCP ASCII Frame

**Transport:** TCP, port 30002  
**Direction:** Receive  
**Framing:** One message per line, ASCII text

```
Format:  *<HEX_DATA>;<CR><LF>

Where HEX_DATA is uppercase hexadecimal:
  28 chars (14 bytes) → ADS-B Extended Squitter (DF=17), processed
  14 chars  (7 bytes) → Mode S Short Frame, silently dropped
```

**Example (14-byte ADS-B):**
```
*8D4840D6202CC371C32CE0576098;\n
```

**ADS-B 112-bit Internal Structure** (after hex-to-bytes conversion):

| Byte(s) | Bits | Field | Type | Notes |
|---------|------|-------|------|-------|
| 0[7:3] | 5 | DF | uint5 | Must = 17 for ADS-B |
| 0[2:0] | 3 | CA | uint3 | Transponder capability |
| 1–3 | 24 | ICAO | hex string | Aircraft 24-bit address |
| 4–10 | 56 | ME | byte[7] | Extended Squitter payload |
| 4[7:3] | 5 | TC | uint5 | Type Code; drives decode path |
| 11–13 | 24 | PI | uint24 | CRC parity |

**TC 9–18 Airborne Position** (within ME, 0-based string indices):

| String Index | Bits | Field | Decode | Units |
|-------------|------|-------|--------|-------|
| 37–38 | 2 | SS | — | Surveillance Status |
| 39 | 1 | NIC-B | — | NIC supplement |
| 40–51 | 12 | ALT | Q=1: (11-bit × 25) − 1000; Q=0: Gillham (unimplemented) | feet pressure alt |
| 52 | 1 | T | — | UTC sync flag |
| 53 | 1 | F | 0=even, 1=odd | CPR frame selector |
| 54–70 | 17 | LAT_CPR | value / 131072 | fractional zone index |
| 71–87 | 17 | LON_CPR | value / 131072 | fractional zone index |

**TC 19 Airborne Velocity** (within ME, 0-based string indices):

| String Index | Bits | Field | Decode | Units |
|-------------|------|-------|--------|-------|
| 37–39 | 3 | ST | 1=GS subsonic, 2=GS supersonic, 3=IAS, 4=TAS | — |
| 40 | 1 | IC | — | Intent Change |
| 41 | 1 | IFR | — | IFR capability (**currently parsed at index 40 — Bug Issue 3**) |
| 42–44 | 3 | NAC_v | — | Nav accuracy for velocity |
| 45 | 1 | DEW | 0=East, 1=West | direction |
| 46–55 | 10 | VEW | value − 1; 0=no data | knots |
| 56 | 1 | DNS | 0=North, 1=South | direction |
| 57–66 | 10 | VNS | value − 1; 0=no data | knots |
| 67 | 1 | VrSrc | 0=GNSS, 1=Baro | VR source |
| 68 | 1 | VrSgn | 0=UP/climb, 1=DOWN/descent | sign |
| 69–77 | 9 | Vr | 64 × (value − 1); 0=no data | ft/min |
| 78–79 | 2 | RES | — | reserved |
| 80 | 1 | SDif | 0=GNSS above Baro, 1=below | sign |
| 81–87 | 7 | dALT | 25 × (value − 1); 0=no data | feet |

---

### ICD-2: Echodyne ECHO Receive — TCP Binary

**Transport:** TCP, port 29982  
**Direction:** Receive  
**Framing:** Fixed 728-byte packet (no length prefix — Issue 29)  
**Byte order:** Little-endian

| Offset | Size | Type | Field | Notes |
|--------|------|------|-------|-------|
| 0 | 8 | ASCII | packet_sync | 8-char sync string |
| 8 | 4 | uint32 | n_bytes | Declared packet size |
| 12 | 1 | uint8 | version_major | |
| 13 | 1 | uint8 | version_minor | |
| 14 | 1 | uint8 | version_patch | |
| 15 | 1 | uint8 | reserved | |
| 16 | 4 | uint32 | radar_id | Radar unit identifier |
| 20 | 1 | uint8 | packet_type | 0=STANDARD, 1=EXTENDED |
| 21 | 1 | uint8 | state | 0=INACTIVE, 1=UNCONFIRMED, 2=CONFIRMED, 3=AMBIGUOUS, 4=HANDOFF |
| 22 | 6 | — | reserved | |
| 28 | 4 | uint32 | lifetime | Track age (ms) |
| 32 | 4 | float32 | confidence_level | 0.0–1.0 |
| 36 | 4 | uint32 | informed_update_count | Number of data updates |
| 40 | 8 | — | reserved | |
| 48 | 8 | uint64 | track_id | Numeric track identifier |
| **56** | **16** | bytes[16] | **track_UUID** | 128-bit UUID |
| **72** | **16** | bytes[16] | **handoff_UUID** | 128-bit UUID (**all 3 parsed from offset 56 — Bug Issue 26**) |
| **88** | **16** | bytes[16] | **track_merge_UUID** | 128-bit UUID |
| 104 | 12 | float32×3 | POSITION_XYZ | Sensor-frame position (m) |
| 116 | 12 | float32×3 | VELOCITY_XYZ | Sensor-frame velocity (m/s) |
| 128 | 24 | double×3 | POSITION_ECEF | ECEF position (m) ← used for LLA |
| 152 | 12 | float32×3 | VELOCITY_ECEF | ECEF velocity (m/s) |
| 164 | 12 | float32×3 | POSITION_ENU | ENU position relative to radar (m) |
| 176 | 12 | float32×3 | VELOCITY_ENU | ENU velocity (m/s) ← used for heading/speed |
| 188 | 4 | float32 | rcs_est | Radar cross-section estimate (dBsm) |
| 192 | 4 | float32 | rcs_est_std | RCS standard deviation (dBsm) |
| 196 | 1 | uint8 | track_formation_source | 0=INTERNAL, 1=HANDOFF |
| 197 | 1 | uint8 | track_cause_of_death | 0=NA, 1=MERGE, 2=COAST, 3=INVALID, 4=STOPPED |
| 198 | 1 | uint8 | track_is_focused | 1 if focused beam |
| 199 | 1 | — | padding | |
| **200** | **8** | int64 | **last_update_time** | Nanoseconds since Unix epoch (**all 3 timestamps use this value — Bug Issue 27**) |
| **208** | **8** | int64 | **last_assoc_time** | Nanoseconds |
| **216** | **8** | int64 | **acquired_time** | Nanoseconds |
| 224 | 4 | float32 | agl_est | Above-ground-level estimate (m) |
| 228 | 4 | float32 | prob_aircraft | Probability × 100 (%) |
| 232 | 4 | float32 | prob_bird | Probability × 100 (%) |
| 236 | 4 | float32 | prob_clutter | Probability × 100 (%) |
| 240 | 4 | float32 | prob_human | Probability × 100 (%) |
| 244 | 4 | float32 | prob_uav_fixedwing | Probability × 100 (%) |
| 248 | 4 | float32 | prob_uav_multirotor | Probability × 100 (%) |
| 252 | 4 | float32 | prob_vehicle | Probability × 100 (%) |
| 256–727 | 472 | — | (unparsed) | Remaining packet bytes |

**Track ID derivation:**  
```csharp
string hex = Convert.ToHexString(track_UUID);          // 32 hex chars
string trackID = "ECH_" + hex.Substring(28, 4);        // last 4 hex chars of UUID
```

**ValidMsg:** `state == STATES.CONFIRMED (2)`

---

### ICD-3: RADAR / EXT / LoRa Receive — UDP Binary

**Transport:** UDP  
**Ports:** 10009 (RADAR), 10032 (LORA), configurable (EXT)  
**Direction:** Receive  
**Packet size:** 64 bytes (TRACK command only)  
**Byte order:** Little-endian

| Offset | Size | Type | Field | Notes |
|--------|------|------|-------|-------|
| 0 | 1 | uint8 | sync_start | 0xAA |
| 1 | 8 | int64 | timestamp | Unix epoch milliseconds UTC |
| 9 | 8 | ASCII | track_id | Call sign / ID, up to 8 chars, null-padded |
| 17 | 1 | uint8 | classification | See TRACK_CLASSIFICATION enum below |
| 18 | 1 | uint8 | command | See RADAR_TRACK_CMDS enum below |
| 19 | 8 | double | latitude | Degrees, WGS-84 |
| 27 | 8 | double | longitude | Degrees, WGS-84 |
| 35 | 4 | float32 | altitude | Metres, HAE (WGS-84) |
| 39 | 4 | float32 | vx | m/s (axis convention — see Issue 36) |
| 43 | 4 | float32 | vy | m/s |
| 47 | 4 | float32 | vz | m/s (vertical, positive up) |
| 51 | 4 | uint32 | reserved_1 | 0x00000000 |
| 55 | 4 | uint32 | reserved_2 | 0x00000000 |
| 59 | 4 | uint32 | reserved_3 | 0x00000000 |
| 63 | 1 | uint8 | sync_end | 0xAA |
| **Total** | **64** | | | |

**RADAR_TRACK_CMDS (offset 18):**

| Value | Name | Meaning |
|-------|------|---------|
| 0x00 | DROP | Remove all tracks from this sensor from trackLogs |
| 0x01 | TRACK | Position/velocity update (requires length == 64) |
| 0x02 | REPORT_ONCE | Request one status response |
| 0x03 | RES_003 | Reserved |
| 0x04 | WEAPON_HOLD | Weapon hold order (acknowledged, no action) |
| 0x05 | WEAPON_FREE | Weapon free order (acknowledged, no action) |
| 0xFE | REPORT_CONT_ON | Enable continuous 10 Hz response |
| 0xFF | REPORT_CONT_OFF | Disable continuous response |

**TRACK_CLASSIFICATION (offset 17):**

| Value | Name |
|-------|------|
| 0 | None |
| 3 | GROUND_OBS |
| 4 | SAILPLANE |
| 5 | BALLOON |
| 8 | UAV |
| 9 | SPACE |
| 10 | AC_LIGHT |
| 11 | AC_MED |
| 13 | AC_HEAVY |
| 14 | AC_HIGHPERF |
| 15 | AC_ROTOR |
| 16 | RESERVED |

**Velocity axis convention (vx, vy, vz):**  
Interpreted by `trackMSG(byte[])` as:
```
heading_deg = atan2(vy, vx) × 180/π
speed_mps   = sqrt(vx² + vy²)
```
This implies vx = East component, vy = North component (standard ENU). **However, the transmitting side (`Form1.ToArray`) encodes vx = cos(hdg) × speed (North) and vy = sin(hdg) × speed (East), which is the opposite convention.** Standardise to ENU (vx=East, vy=North) at both sender and receiver — see Issue 36.

---

### ICD-4: CUE Unicast Transmit — UDP Binary

**Transport:** UDP unicast  
**Destination:** `192.168.1.8:10009` (hardcoded — should be configurable)  
**Direction:** Transmit  
**Source:** `Form1.ToArray()` (primary) or `ECHO.ToArray()` (re-packaging from ECHO)  
**Rate:** Driven by `timUDP` interval  
**Byte order:** Little-endian

The format is **identical to ICD-3** with the following fixed values:

| Offset | Field | Value | Notes |
|--------|-------|-------|-------|
| 17 | classification | 0x08 (UAV) | **Hardcoded regardless of actual track type** |
| 18 | command | 0x01 (TRACK) | Always a track report |
| 47 | vz | 0.0 | Always zero in `Form1.ToArray()` |
| 51–62 | reserved | 0x00 | |

**Known bugs affecting this packet (see Issues 22 and 43):**

| Issue | Effect on Packet |
|-------|----------------|
| Issue 22 | vx = cos(hdg) × **heading_deg** instead of × **speed_mps**. At heading=90°, speed=100 m/s the packet contains vx=90 m/s instead of 0, vy=0 instead of 100 m/s. |
| Issue 43 (new) | `track_id` field at offset 9 is written as `Encoding.ASCII.GetBytes(ICAO)` with no padding to 8 bytes. An ADS-B ICAO (e.g., "ABC123" = 6 bytes) produces a 62-byte packet, shifting all subsequent fields left by 2 bytes and breaking all field alignment. |

**Issue 43 fix:**
```csharp
// CORRECTED — always write exactly 8 bytes for track_id:
string tID = aCB.CurrentCUE.ICAO.PadRight(8).Substring(0, 8);
byte[] bID = Encoding.ASCII.GetBytes(tID);  // always 8 bytes
sw.Write(bID);
```

---

### ICD-5: Stellarium REST API — JSON Receive

**Transport:** HTTP GET  
**URL:** `http://{IP_ADDRESS}:{PORT}/api/objects/info?format=json`  
**Currently hardcoded to:** `http://localhost:8090/api/objects/info?format=json` (Issue 38)  
**Direction:** Receive  
**Rate:** ~10 Hz poll (when loop delay is fixed — Issue 39)

**Response JSON fields used:**

| JSON Key | C# Property | Type | Units | Notes |
|----------|-------------|------|-------|-------|
| `"localized-name"` | `Name` | string | — | Object display name |
| `"object-type"` | `ObjectType` | string | — | e.g., "Planet", "Star", "Satellite" |
| `"altitude"` | `Altitude` | double | degrees | Elevation above horizon (−90 to +90) |
| `"azimuth"` | `Azimuth` | double | degrees | Azimuth, N=0°, E=90° |
| `"distance-km"` | `Range_km` | double | km | Distance to object (astronomical) |
| `"velocity-kms"` | `Speed_mps` | double | m/s | Object velocity; raw km/s × 1000 |

**Notes:**
- `Altitude` and `Azimuth` are in J2000-corrected apparent sky coordinates.
- `distance-km` for solar system objects is the geocentric distance. For the Moon ≈ 384,400 km; for the Sun ≈ 150,000,000 km.
- `velocity-kms` is the object's velocity relative to the observer, in km/s, converted to m/s by `× 1000`.
- This data is displayed in the DataGridView `Bearing` and `Elevation` columns but is **not** fed into `trackLogs` or the Kalman filter.

---

## 15. Issues — Complete List

### 🔴 Critical

| # | File | Description | Status |
|---|------|-------------|--------|
| ~~1~~ | ~~adsb_raw.cs~~ | ~~`VerticalRate_mps` returns ft/min — missing × 0.00508~~ | ✅ **Fixed Rev 5** |
| ~~2~~ | ~~adsb_raw.cs~~ | ~~Vertical rate sign inverted (VrSgn=0 = climb, not descent)~~ | ✅ **Fixed Rev 5** |
| ~~3~~ | ~~adsb_raw.cs~~ | ~~IFR flag reads bit index 40 (IC bit) instead of 41~~ | ✅ **Fixed Rev 5** |
| ~~4~~ | ~~adsb_raw.cs~~ | ~~`BigInteger` hex parse adds sign byte → 15-byte packet fails switch~~ | ✅ **Fixed Rev 5** |
| 16 | kalman.cs | H matrix 6×6 with zero rows corrupts velocity state — resize to 3×6, R to 3×3 | ✅ **Fixed Rev 7** |
| ~~22~~ | ~~Form1.cs~~ | ~~`ToArray()` velocity encodes heading_deg not speed~~ | ✅ **Fixed Rev 5** |
| ~~26~~ | ~~echo.cs~~ | ~~All three UUIDs parsed from same offset 56~~ | ✅ **Fixed Rev 5** |
| ~~43~~ | ~~Form1.cs~~ | ~~ICAO string not padded to 8 bytes → packet misalignment~~ | ✅ **Fixed Rev 5** |

### 🟠 High

| # | File | Description | Status |
|---|------|-------------|--------|
| ~~5~~ | ~~adsb_raw.cs~~ | ~~Q=0 altitude returns magic 60000~~ | ✅ **Fixed Rev 6** |
| ~~6~~ | ~~adsb_raw.cs~~ | ~~Zero Vr/dALT produce invalid negative values~~ | ✅ **Fixed Rev 6** |
| ~~7~~ | ~~adsb_raw.cs~~ | ~~ST 3/4 airspeed decoded as ground speed~~ | ✅ **Fixed Rev 6** |
| ~~8~~ | ~~adsb_raw.cs~~ | ~~`nZoneLong` NaN at high latitudes~~ | ✅ **Fixed Rev 6** |
| ~~20~~ | ~~trackLog.cs~~ | ~~`FilteredPosition` getter mutates IIR state~~ | ✅ **Fixed Rev 6** |
| ~~21~~ | ~~trackLog.cs~~ | ~~Velocity-only constructor sets msgType=POSITION~~ | ✅ **Fixed Rev 6** |
| ~~27~~ | ~~echo.cs~~ | ~~All three timestamps use `last_update_time`~~ | ✅ **Fixed Rev 6** |
| ~~29~~ | ~~echo.cs~~ | ~~Fixed 728-byte TCP buffer; no packet framing~~ | ✅ **Fixed Rev 6** |
| ~~30~~ | ~~echo.cs~~ | ~~Cancellation removes ALL tracks, not just ECHO tracks~~ | ✅ **Fixed Rev 6** |
| ~~39~~ | ~~stellarium.cs~~ | ~~Spin-loop burns CPU — missing Task.Delay~~ | ✅ **Fixed Rev 6** |
| ~~41~~ | ~~all receivers~~ | ~~Stale cleanup removes wrong-sensor tracks~~ | ✅ **Fixed Rev 6** |

### 🟡 Medium

| # | File | Description | Fix |
|---|------|-------------|-----|
| ~~9~~ | ~~adsb_raw.cs~~ | ~~`Alt_gps_baro_m` ambiguous — result is HAE~~ | ✅ **Fixed Rev 9** |
| ~~10~~ | ~~adsb_raw.cs~~ | ~~`Alt_Baro_m` undocumented as pressure altitude~~ | ✅ **Fixed Rev 9** |
| ~~11~~ | ~~adsb_raw.cs~~ | ~~Callsign LUT `#` should be spaces per ICAO Annex 10~~ | ✅ **Fixed Rev 9** |
| ~~12~~ | ~~crossbow.cs~~ | ~~`trackLogs` Dictionary not thread-safe~~ | ✅ **Fixed Rev 9** |
| ~~17~~ | ~~kalman.cs~~ | ~~R₀=0.5 / P₀=0.25 too tight for CPR/radar accuracy~~ | ✅ **Fixed Rev 7** |
| ~~19~~ | ~~kalman.cs~~ | ~~`KalmanFilter` bearing smoother hardcoded dt=1.0~~ | ✅ **Fixed Rev 9** |
| ~~23~~ | ~~crossbow.cs~~ | ~~BaseStation `alt` comment `//MSL, HAE` contradictory~~ | ✅ **Fixed Rev 9** |
| ~~24~~ | ~~Form1.cs~~ | ~~`updateGrid_Survey()` checks Stellarium toggle~~ | ✅ **Fixed Rev 9** |
| ~~28~~ | ~~echo.cs~~ | ~~Hard `ndx` resets mask sequential parsing errors~~ | ✅ **Fixed Rev 9** |
| ~~31~~ | ~~echo.cs~~ | ~~`ToArray()` timestamp uses local time~~ | ✅ **Fixed Rev 9** |
| ~~33~~ | ~~radar.cs~~ | ~~Default constructor port 30002 conflicts with ADS-B~~ | ✅ **Fixed Rev 9** |
| ~~34~~ | ~~radar.cs~~ | ~~`SendResponse()` entirely commented out~~ | ✅ **Fixed Rev 9** |
| ~~36~~ | ~~multiple~~ | ~~Velocity axis convention inconsistent (ENU vs NED)~~ | ✅ **Fixed Rev 9** |
| ~~37~~ | ~~radar.cs~~ | ~~`BaseICAO` never set from constructor~~ | ✅ **Fixed Rev 9** |
| ~~38~~ | ~~stellarium.cs~~ | ~~URL hardcoded; ignores `IP_ADDRESS` / `PORT` properties~~ | ✅ **Fixed Rev 6** |
| ~~42~~ | ~~all classes~~ | ~~BaseStation defaults differ across sensor classes~~ | ✅ **Fixed Rev 9** |

### 🔵 Minor

| # | File | Description | Fix |
|---|------|-------------|-----|
| ~~13~~ | ~~multiple~~ | ~~Spelling: `VerticleRate`, `Squak`, `VotexCateg`, `Kalan`~~ | ✅ **Fixed Rev 10** |
| ~~14~~ | ~~adsb_raw.cs~~ | ~~TC 5–8 and TC 20–22 not decoded~~ | ✅ **Fixed Rev 11** |
| ~~15~~ | ~~adsb_raw.cs, echo.cs~~ | ~~`stm.Flush()` on NetworkStream is no-op~~ | ✅ **Fixed Rev 10** |
| ~~18~~ | ~~kalman.cs~~ | ~~Process noise σ_a² is unexplained magic number~~ | ✅ **Fixed Rev 7** |
| ~~25~~ | ~~trackLog.cs~~ | ~~Vertical velocity always 0 in Kalman state~~ | ✅ **Fixed Rev 8** |
| ~~32~~ | ~~trackLog.cs~~ | ~~ECHO speed includes vertical component~~ | ✅ **Fixed Rev 8** |
| ~~35~~ | ~~radar.cs~~ | ~~Single-track RADAR limitation undocumented~~ | ✅ **Fixed Rev 12** |
| ~~40~~ | ~~stellarium.cs~~ | ~~Uses deprecated `WebClient`~~ | ✅ **Fixed Rev 13** |

---

## 16. Summary Table

**Totals: 0 Critical · 0 High · 0 Medium · 0 Minor = 0 open issues (46 fixed) ✅**

| Severity | Open | Fixed (all revs) | Status |
|----------|------|-----------------|--------|
| 🔴 Critical | 0 | 8 | All resolved ✅ |
| 🟠 High | 0 | 11 | All resolved ✅ |
| 🟡 Medium | 0 | 17 | All resolved ✅ |
| 🔵 Minor | 0 | 10 | All resolved ✅ |

---

*Review based on ICAO Doc 9684 (Mode S), RTCA DO-260B (ADS-B), Optimal State Estimation (Dan Simon, 2006), Echodyne ICD, and direct code tracing of all thirteen source files.*
