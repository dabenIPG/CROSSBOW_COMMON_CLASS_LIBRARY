# CROSSBOW Emplacement GUI User Guide

**Version:** 1.0.1
**Date:** 2026-04-05
**Audience:** Mission planners, site commanders, emplacement teams
**ICD Reference:** `CROSSBOW_ICD_INT_OPS` (IPGD-0004) — `0xA7`, `0xA8`, `0xAC`, `0xBA`, `0xBB`
**Architecture Reference:** `ARCHITECTURE.md` (IPGD-0006) — network topology, platform registration

---

## 1. Overview

The CROSSBOW Emplacement GUI is a standalone C#/.NET 8 Windows application used to
prepare and generate all mission data before THEIA goes operational. It is **not** a
real-time control application — its outputs are files and configuration data that
THEIA loads at mission start.

The Emplacement GUI handles three functional areas:

| Function | Tool | Output | Loaded by |
|----------|------|--------|-----------|
| Horizon profile generation | Horizon Generator tab | `.txt` (360 floats) + `.csv` (review) | THEIA → `0xAC SET_BDC_HORIZ` → BDC |
| LCH file loading and upload | LCH tab | Window data sent to BDC | THEIA → `0xA7`/`0xA8` → BDC |
| KIZ file loading and upload | KIZ tab | Window data sent to BDC | THEIA → `0xA7`/`0xA8` → BDC |
| Survey points file | Platform Registration (§4) | Survey file loaded in THEIA | THEIA → `0xBA`/`0xBB` → BDC |

The Emplacement GUI connects to BDC on A3 port 10050 (`TransportPath.A3_External`),
the same transport as THEIA. It is used during site setup, not during active engagement.

### Pre-Mission Workflow

```
1. Emplacement GUI
   ├── Generate horizon file for emplacement position
   ├── Load and verify LCH file (from Space Command)
   ├── Load and verify KIZ file (operator-generated or pre-approved)
   └── Upload LCH and KIZ to BDC

2. THEIA
   ├── Load horizon .txt → send 0xAC SET_BDC_HORIZ → BDC
   ├── Set platform LLA → 0xBA SET_SYS_LLA
   ├── Set platform attitude → 0xBB SET_SYS_ATT
   └── Confirm all BDC VOTE BITS2 zone flags set before entering COMBAT
```

---

## 2. Horizon Generator

The Horizon Generator computes a full 360° terrain horizon profile for the current
emplacement position. The output is a pair of files that capture the maximum blocking
elevation angle at every azimuth degree, derived from USGS NED elevation data. The
`.txt` file is subsequently loaded in THEIA and transmitted to BDC as a `float[360]`
array via `0xAC SET_BDC_HORIZ`, where it is used to suppress false fire-control votes
caused by terrain masking.

### 2.1 Output Files

| File | Format | Content | Used by |
|------|--------|---------|---------|
| `<name>.csv` | Text, comma-separated | One row per degree: `AZ, EL, RANGE, ALT, SeaLevelRange` | Review and archiving |
| `<name>.txt` | Text, one value per line | 360 floating-point elevation angles (degrees), index 0–359 = azimuth 0°–359° | THEIA → `0xAC SET_BDC_HORIZ` → BDC |

The `.txt` file is written automatically alongside the `.csv` when the save dialog is
confirmed — both share the same base filename and differ only in extension.

### 2.2 Step-by-Step Workflow

**Step 1 — Enter emplacement position**

Enter the platform position in the three fields at the top of the Horizon Generator tab:

| Field | Content | Notes |
|-------|---------|-------|
| Latitude | Decimal degrees, positive North | e.g. `34.459541` |
| Longitude | Decimal degrees, negative West | e.g. `-86.432505` |
| Elevation | Metres above ellipsoid (HAE) | e.g. `173` |

These values are read at form load to initialise the map. If you change them after load,
re-centre the map (Step 2) before proceeding.

**Step 2 — Centre map**

Click **Centre Map**. The GMap display re-centres on the entered position and the
status indicator turns green. This step confirms the coordinate entry is valid and
positions the map for the DTED fetch query.

**Step 3 — Fetch available DTED tiles**

Click **Fetch**. The application queries the USGS The National Map (TNM) API for all
NED 1/3 arc-second GeoTIFF tiles that overlap the current map view:

```
https://tnmaccess.nationalmap.gov/api/v1/products?
  bbox=<left>,<top>,<right>,<bottom>
  &datasets=National Elevation Dataset (NED) 1/3 arc-second
  &prodFormats=GeoTIFF
  &outputFormat=JSON
```

Available tiles are listed in the file list box, sorted by creation date (newest last),
with the most recent tile pre-selected. Each tile's bounding box is drawn on the map
as a grey overlay so coverage can be verified visually. The status bar shows the number
of tiles found.

> **If no tiles are returned:** Zoom out the map to widen the bounding box, then
> fetch again. The TNM API returns only tiles that intersect the current view extent.

**Step 4 — Download the tile**

Select the desired tile in the list (prefer the most recent creation date) and click
**Download**. A save dialog opens pre-populated with the USGS filename. Choose a local
destination and confirm. The progress bar tracks the download; the status indicator
turns green on completion.

> If the tile was previously downloaded, skip to Step 5 and open the cached file
> directly.

**Step 5 — Open the GeoTIFF**

Click **Open**. Select the downloaded `.tif` file. The application opens the file via
GDAL, reads the projection (displayed in the status bar — typically `WGS 84`), and
draws the tile bounding box on the map as a grey polygon. Both geographic (WGS 84) and
UTM projected files are supported.

**Step 6 — Process**

Click **Process**. The application iterates over every raster pixel in the GeoTIFF and
computes the terrain elevation angle from the emplacement position to that pixel using a
geodetic dip-angle model (WGS-84 ellipsoid, Haversine great-circle distance). The
algorithm bins results into 1° azimuth buckets and retains the maximum elevation angle
per bucket. Only pixels between 100 m and 50 km ground range are evaluated.

Processing time depends on tile size (a full 1/3 arc-second tile is typically 10,800 ×
10,800 pixels). The progress bar tracks completion by raster row.

**Step 7 — Save output files**

When processing completes, a save dialog opens prompting for the `.csv` filename. The
`.txt` file is written automatically to the same folder with the same base name. Both
files are saved simultaneously — there is no separate save step for the `.txt`.

The computed horizon is overlaid on the map as a red polygon showing the viewshed
footprint.

### 2.3 Algorithm Notes

The dip-angle calculation (`calculate_dip_angle`) uses the WGS-84 ellipsoid parameters
(a = 6378.137 km, b = 6356.7523 km) and accounts for ellipsoidal curvature at both
the observer and target latitudes. The azimuth from observer to each pixel is computed
using the standard geodetic bearing formula.

The refraction correction (Yoeli model, k = 0.13) is implemented in the function but
is **not applied** in the current processing pass — the `lcorr_for_refraction` argument
is passed as `false`. The result is a geometric horizon without atmospheric refraction
correction. This is conservative: refraction would slightly increase the effective
visible range, so the saved horizon is a worst-case (highest blocking angle) profile.

The sea-level range column in the `.csv` (`SeaLevelRange`) tracks the furthest pixel
in each azimuth bin where the terrain elevation is zero — it is computed in a separate
pass using the same dip-angle function with `alt2 = 0`. It is informational only and
is not included in the `.txt` file sent to BDC.

### 2.4 Output File Format

**`.csv` — review file:**

```
AZ, EL, RANGE, ALT, SeaLevelRange
0, 2.31, 4823.00, 312.00, 48200.00
1, 1.84, 6102.00, 287.00, 51300.00
...
359, 3.05, 3917.00, 341.00, 44100.00
```

| Column | Units | Description |
|--------|-------|-------------|
| `AZ` | degrees (integer) | Azimuth bin, 0°–359° true |
| `EL` | degrees (2 dp) | Maximum blocking elevation angle in this bin |
| `RANGE` | metres (2 dp) | Ground range to the horizon pixel |
| `ALT` | metres (2 dp) | Terrain elevation of the horizon pixel (HAE) |
| `SeaLevelRange` | metres (2 dp) | Range to furthest sea-level intercept in this bin |

**`.txt` — BDC input file:**

360 plain-text lines, one floating-point value per line, index 0 = azimuth 0°, index
359 = azimuth 359°. Values are written at full double precision (no rounding). This
file is loaded by THEIA and sent verbatim as `float[360]` to BDC via `0xAC SET_BDC_HORIZ`.

### 2.5 Loading the Horizon File in THEIA

After saving, load the `.txt` file in THEIA via the horizon file selector (see
`THEIA_USER_GUIDE.md §5 Step 3`). THEIA reads the 360 values and transmits them to BDC.
Confirm `isHorizonLoaded` (BDC VOTE BITS2 bit 5) is set before proceeding to the
pre-mission checklist.

---

## 3. LCH / KIZ — Laser Engagement Zone Windows

### 3.1 Concepts

**LCH (Laser Clearinghouse)** and **KIZ (Keep-In Zone)** both use the same time-windowed
az/el corridor model. The laser can only fire when a window is open — meaning current
UTC time falls within an authorized time slot AND the current laser LOS az/el falls
within the corresponding angular bounds.

| | LCH | KIZ |
|---|-----|-----|
| Source | USSTRATCOM JFSCC Space (Space Command) | Operator-generated or locally approved |
| Purpose | Prevent laser illumination of satellites | Restrict firing to an approved local zone |
| Constraint type | Negative — defines when/where firing is safe relative to space assets | Positive — defines the allowed firing volume |
| File format | PAM (Predictive Avoidance Message) — same format | PAM format — same parser |
| Vote bit | `InLCHVote` | `InKIZVote` |

Both votes must be high (along with all other BDC geometry votes) for `BDCVote` to pass
and enable the fire control chain.

**Vote evaluation — four independent gates (all must pass for `TotalVote`):**

| Gate | Check |
|------|-------|
| `isForExecution` | File `Comment` field must be `"For Execution"` (not `"Practice"`) |
| `isOperatorValid` | File `Laser Owner/Operator` field must match configured system operator (`"IPG"`) |
| `isLocationValid` | Source `Latitude/Longitude` in file must match system emplacement position within 10 metres |
| `WindowVote` | Current laser LOS az/el is inside a target's az/el bounds AND current UTC time is within one of that target's time windows |

If any gate fails, `TotalVote` is false and the `InLCHVote` / `InKIZVote` bit will not set.

### 3.2 File Format — PAM (Predictive Avoidance Message)

Both LCH and KIZ files follow the USSTRATCOM DECON PAM format.
Authoritative format specification: DECON ICD (excerpt retained in project files).

**LCH files** are received from USSTRATCOM JFSCC Space (Space Command) via email
as ASCII `.txt` attachments. Do not modify the file content — any change to the
`Operator`, `Comment`, or source coordinates will invalidate the vote gates.

**KIZ files** follow the same PAM format and may be generated locally or by any
approved tool. The `Comment` field must be set to `"For Execution"` for the file
to be active in combat.

#### File Naming Convention

| Type | Convention | Example |
|------|-----------|---------|
| LCH | Contains `LCH` in filename | `PAM_IPG_CROSSBOW_TEST1_LCH.txt` |
| KIZ | Contains `KIZ` in filename | `PAM_IPG_CROSSBOW_TEST1_KIZ.txt` |

The file open dialogs filter by these patterns. If a file is not found in the dialog,
verify the filename contains the correct keyword.

#### Key Header Fields (parsed by CROSSBOW)

| Field | Location in file | Notes |
|-------|-----------------|-------|
| Mission ID | `Mission ID:` | Displayed for operator confirmation |
| Operator | `Laser Owner/Operator:` | Must match `"IPG"` — gates `isOperatorValid` |
| Mission Name | `Mission Name:` | Display only |
| Mission Start | `Mission Start Date/Time (UTC):` | UTC — used as time reference for all delta calculations |
| Mission End | `Mission Stop Date/Time (UTC):` | UTC |
| Authorization | `Comment:` | Must contain `"For Execution"` — gates `isForExecution` |
| Number of Targets | `Number of Targets:` | Drives how many target blocks are parsed |

#### Window Block Structure (per target)

Each target block contains:

```
YYYY MMM dd (DDD) HHMM SS    YYYY MMM dd (DDD) HHMM SS      MM:SS
-------------------------    -------------------------    -------
2025 Aug 21 (233) 1647 09    2025 Aug 22 (234) 0047 08    0479:59
2025 Aug 21 (233) 1652 09    2025 Aug 22 (234) 0047 08    0474:59

Percent = 100.00%

Source Geometry: (WGS-84)
---------------
Method: Fixed Point
Latitude:  34.66733200 degrees N
Longitude: 86.46646600 degrees W
Altitude:  0.1949 km

Target Geometry: (WGS-84) 1
---------------
Method: Fixed Field of View
Azimuth Range:   1.0 to 10.0 degrees
Elevation Range: 1.0 to 3.0 degrees
```

| Field | Meaning |
|-------|---------|
| Window rows | UTC start/stop times for each open window within this target |
| Source Latitude/Longitude | Must match system emplacement position — gates `isLocationValid` (10 m tolerance) |
| Source Altitude | km HAE — geoid undulation applied internally |
| Azimuth Range | Az1 to Az2 — degrees true, rectangular bounds |
| Elevation Range | El1 to El2 — degrees above horizon |

### 3.3 Loading a File

**LCH:**
1. Click **Load LCH File**
2. Select the PAM `.txt` file (filter: `*LCH*.txt`)
3. Verify the displayed fields:

| Field | Expected |
|-------|----------|
| Mission ID | Matches the approved mission |
| Operator | `IPG` — if not, `isOperatorValid` will fail |
| Start / End Date | Covers the planned engagement window |
| Authorization | `For Execution` — if `Practice`, the vote will not pass |
| Location Valid | ✅ Green — source coordinates match emplacement position |
| N Targets / N Windows | Non-zero |

**KIZ:** Same procedure — click **Load KIZ File**, filter `*KIZ*.txt`.

> **If `isLocationValid` fails:** The source lat/lon in the file does not match the
> system BaseStation within 10 metres. Either the emplacement position has changed
> since the file was generated, or the file was generated for a different site.
> Do not proceed — obtain a corrected file or re-survey the emplacement position.

### 3.4 Uploading to BDC

After a file is loaded and all validation indicators are green, click **Upload** to
send the window data to BDC. The upload uses two ICD commands in sequence:

**Step 1 — Mission header (`0xA7 SET_LCH_MISSION_DATA`, 30 bytes):**

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1 | byte | File type — `0`=KIZ, `1`=LCH |
| 1 | 1 | byte | isValid — hardcoded `1` |
| 2 | 8 | UInt64 LE | Mission start — Unix timestamp (seconds UTC) |
| 10 | 8 | UInt64 LE | Mission end — Unix timestamp (seconds UTC) |
| 18 | 2 | UInt16 LE | Az1 — overall min azimuth (degrees) |
| 20 | 2 | Int16 LE | El1 — overall min elevation (degrees) |
| 22 | 2 | UInt16 LE | Az2 — overall max azimuth (degrees) |
| 24 | 2 | Int16 LE | El2 — overall max elevation (degrees) |
| 26 | 2 | UInt16 LE | Number of targets |
| 28 | 2 | UInt16 LE | Total number of windows (across all targets) |

**Step 2 — Per-target data (`0xA8 SET_LCH_TARGET_DATA`, 29 + N×4 bytes per target):**

Sent once per target, with 20 ms between packets. Progress bar reflects upload status
(5% after mission header, then proportional through each target).

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1 | byte | File type — `0`=KIZ, `1`=LCH |
| 1 | 2 | UInt16 LE | Window count for this target |
| 3 | 2 | UInt16 LE | Target start — delta seconds from mission start |
| 5 | 2 | UInt16 LE | Target end — delta seconds from mission start |
| 7 | 2 | UInt16 LE | Az1 degrees |
| 9 | 2 | Int16 LE | El1 degrees |
| 11 | 2 | UInt16 LE | Az2 degrees |
| 13 | 2 | Int16 LE | El2 degrees |
| 15 | 4 | float LE | Source latitude |
| 19 | 4 | float LE | Source longitude |
| 23 | 4 | float LE | Source altitude (metres) |
| 27 | 4×N | UInt16 pairs LE | Windows: `[wt1, wt2]` delta seconds from mission start, repeated N times |

**Step 3 — Vote flags (`0xC9 SET_BDC_PALOS_VOTE`):**

Sent automatically 500 ms after the last target packet:

| Byte | Field |
|------|-------|
| 0 | File type (`0`=KIZ, `1`=LCH) |
| 1 | `isOperatorValid` |
| 2 | `isLocationValid` |
| 3 | `isForExecution` |

### 3.5 Verifying Upload on BDC

After upload, confirm the following in the THEIA or ENG GUI BDC status panel:

| Field | Expected after LCH upload | Expected after KIZ upload |
|-------|--------------------------|--------------------------|
| `isLCHForExec` | ✅ | — |
| `isLCHPositionValid` | ✅ | — |
| `isLCHOperatorValid` | ✅ | — |
| `isKIZForExec` | — | ✅ |
| `isKIZPositionValid` | — | ✅ |
| `isKIZOperatorValid` | — | ✅ |
| `isKIZLoaded` / `isLCHLoaded` | BDC VOTE BITS2 bits 6/7 | Both should be set |

BDC evaluates `InKIZVote` and `InLCHVote` continuously against the uploaded window data.
These votes will only go high when the laser LOS is within an open window — they are
not expected to be green at rest unless the gimbal is pointed into an authorized zone
during an active time window.

### 3.6 Example — LCH File

The following is an example of a valid LCH PAM file for CROSSBOW:

```
Mission ID:                      CROSSBOW_TEST1
Laser Owner/Operator:            IPG
Mission Name:                    CROSSBOW_TEST1
Mission Start Date/Time (UTC):   2025 Aug 21 16:47:09
Mission Stop  Date/Time (UTC):   2025 Aug 22 00:47:08
Mission Duration   (HH:MM:SS):   07:59:59
Type of Windows in this report:  Authorized Shoot(Open) Windows
Comment:                         For Execution
Number of Targets:               2

[Window table — Target 1]
Azimuth Range:   1.0 to 5.0 degrees
Elevation Range: 2.0 to 4.0 degrees

[Window table — Target 2]
Azimuth Range:   2.0 to 4.0 degrees
Elevation Range: 5.0 to 10.0 degrees
```

### 3.7 BDC Window Evaluation Model

BDC continuously evaluates whether the current laser LOS falls within an open window.
Per-target logic (same model as the THEIA-side `lch.cs` `CheckLocalVote`):

```
For each target:
  if current_az in [Az1, Az2] AND current_el in [El1, El2]:
    for each time window in target:
      if current_UTC in [window.start, window.end]:
        InLCHVote / InKIZVote = TRUE
        break
```

Evaluation is rectangular in az/el — not a frustum or cone. Targets do not overlap
in the example files but the parser supports multiple non-overlapping targets.

---

## 4. Platform Registration and Attitude Refinement

Platform registration is the process of establishing the precise position and attitude
of the CROSSBOW system at the emplacement site. This section covers the sensor
baseline, the limits that motivate refinement, the survey file format used by the
Emplacement GUI, and the execution workflow that completes in THEIA.

### 4.1 Native Sensor Accuracy

CROSSBOW carries two sensor inputs that establish initial position and attitude
automatically on startup:

**Position — NovAtel GNSS with TerraStar-C PRO L-band correction:**

| Mode | Horizontal | Vertical |
|------|-----------|---------|
| Standalone NovAtel | ~2 m | ~10 m |
| With TerraStar-C PRO | ~2 cm | ~4 cm |

TerraStar correction is acquired automatically when the receiver has a clear sky view.
Position accuracy is generally sufficient for operational use without further refinement.

**Attitude — Dual-antenna GNSS / IMU:**

| Axis | Accuracy |
|------|---------|
| Pitch | ~0.25° |
| Roll | ~0.25° |
| Azimuth (heading) | ~2° |

Pitch and roll are well-constrained by the IMU. Azimuth is derived from the dual-antenna
GNSS baseline and is the primary axis requiring refinement — a 2° azimuth error at 1 km
range produces approximately 35 m of cross-range pointing error.

### 4.2 Attitude Refinement — Concept

Attitude refinement solves for the residual roll/pitch/yaw (RPY) error of the system
by correlating known positions in space (fiducials) with observed gimbal LOS solutions.
The regression uses a least-squares matrix solution (QUEST or equivalent) and requires
a minimum of **3 fiducial points**, with **5 or more recommended** for a robust solution.
Points must have sufficient angular diversity — avoid collinear arrangements.

**Two methods for establishing fiducials:**

| Method | Fiducial source | Accuracy | Notes |
|--------|----------------|---------|-------|
| Survey points | Pre-surveyed ground features | Differential GPS — cm-level | Upload via Emplacement GUI survey file |
| Stellar alignment | Stars at known RA/Dec | Sub-arcsecond | Requires clear sky; use Stellarium for star identification and scheduling |

### 4.3 Survey Points File — Format

If using surveyed ground features, the fiducial coordinates are provided to the system
via a plain-text survey points file loaded in THEIA.

**Format rules:**
- One point per line, terminated with CR/LF
- Fields delimited by semicolon (`;`)
- Lines beginning with `#` are comments and are ignored
- All point IDs must be unique

**Fields (per line):**

| Field | Type | Description |
|-------|------|-------------|
| ID | String | Unique point identifier — e.g. `S1`, `S2` |
| Latitude | Decimal degrees | Positive North |
| Longitude | Decimal degrees | Negative West |
| Altitude | Metres HAE | Height above ellipsoid (WGS-84) |

**Example file:**

```
# CROSSBOW SURVEY POINT INPUT FILE
#
# ALL DATA ON SEPARATE LINES, END WITH CR/LF
# ALL DATA DELIMETED BY SEMI-COLON
# ALL IDS MUST BE UNIQUE
# ID, LAT DEC DEG, LNG DEC DEG, ALT M HAE
#
S1; 34.66731; -86.46648; 197.3
S2; 34.76731; -86.56648; 197.3
S3; 34.86731; -86.76648; 197.3
```

> **Survey point accuracy requirement:** Points should be surveyed using differential
> GPS to achieve cm-level accuracy. The quality of the attitude solution is bounded by
> the accuracy of the fiducial coordinates — points with metre-level position
> uncertainty will not support a precision attitude refinement. All altitudes must be
> provided as HAE (WGS-84 ellipsoid height).

### 4.4 Execution Workflow — THEIA

The attitude refinement and platform registration execution are performed in THEIA.
The Emplacement GUI role is limited to providing the survey file (§4.3 above).

**In THEIA — per fiducial point:**

1. Load the survey points file (or identify target stars via Stellarium)
2. Cue the gimbal to the fiducial point — Hyperion sends the cue command to the gimbal
3. Offset the LOS to centre precisely on the known point
4. Record the LOS solution and error — THEIA logs the observation

Repeat for at least 3 points (5 recommended) with adequate angular spread.

**Solving and latching position/attitude:**

Once sufficient observations are accumulated, THEIA performs the RPY solve. The
workflow uses a two-stage review step before any values are committed to the system:

1. **Position transfer:** The GNSS-derived LLA is transferred to an intermediate
   text field in THEIA for operator review. The operator may adjust the values if
   required before latching.
2. **Attitude transfer:** The computed RPY error offsets are placed in the same
   intermediate text field for verification.
3. **Latch:** On confirmation, THEIA commits the values to its internal `crossbow.cs`
   state and transmits to BDC:
   - `0xBA SET_SYS_LLA` — platform latitude, longitude, altitude HAE
   - `0xBB SET_SYS_ATT` — platform roll, pitch, yaw

Both commands are sent from THEIA — they are not transmitted by the Emplacement GUI.

> **See also:** `THEIA_USER_GUIDE.md` for the platform registration panel and
> the full LLA/ATT latch procedure.

---

## 5. Pre-Mission Verification Checklist

| # | Item | Check |
|---|------|-------|
| 1 | Horizon file generated for current emplacement position | ☐ |
| 2 | Horizon file loaded in THEIA — `isHorizonLoaded` (BDC VOTE BITS2 bit 5) set | ☐ |
| 3 | LCH file received from Space Command — `Comment: For Execution` confirmed | ☐ |
| 4 | LCH `isLocationValid` green — source coordinates match emplacement ±10 m | ☐ |
| 5 | LCH `isOperatorValid` green — `Laser Owner/Operator: IPG` | ☐ |
| 6 | LCH uploaded to BDC — `isLCHLoaded` set in BDC VOTE BITS2 | ☐ |
| 7 | KIZ file loaded and validated | ☐ |
| 8 | KIZ uploaded to BDC — `isKIZLoaded` set in BDC VOTE BITS2 | ☐ |
| 9 | Platform LLA set in THEIA — GNSS transfer reviewed, latched, `0xBA SET_SYS_LLA` sent to BDC | ☐ |
| 10 | Platform attitude set in THEIA — RPY offsets reviewed, latched, `0xBB SET_SYS_ATT` sent to BDC | ☐ |
| 11 | All BDC VOTE BITS2 zone flags confirmed before COMBAT | ☐ |

---

## 6. Related Documents

| Document | Content |
|----------|---------|
| `ICD_EXTERNAL_OPS_v3.0.1` | `0xA7`, `0xA8`, `0xAC`, `0xBA`, `0xBB` payload definitions |
| `THEIA_USER_GUIDE.md §5` | Engagement sequence — horizon and KIZ/LCH loading steps |
| `ARCHITECTURE.md §5` | Port reference |
| DECON ICD excerpt | PAM file format — authoritative field definitions |
