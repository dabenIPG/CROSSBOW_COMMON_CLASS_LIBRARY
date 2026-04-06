#!/usr/bin/env python3
"""
CROSSBOW User Guide Word Document Generator
============================================
Generates four .docx user guides from the shared IPG template.

Usage:
    python3 gen_user_guides.py --guide theia
    python3 gen_user_guides.py --guide hyperion
    python3 gen_user_guides.py --guide eng_gui
    python3 gen_user_guides.py --guide emplacement
    python3 gen_user_guides.py --all

Prerequisites:
    - Template unpacked at ./template_ug/  (unpack CROSSBOW_MINI_USER_GUIDE__v20260205_.docx)
    - pack.py from docx skill at ./office/pack.py
    - Original docx at ./CROSSBOW_MINI_USER_GUIDE__v20260205_.docx
"""

import argparse
import os
import shutil
import subprocess
import sys
import textwrap
import uuid

TEMPLATE_DIR   = "/home/claude/template_ug"
PACK_SCRIPT    = "/mnt/skills/public/docx/scripts/office/pack.py"
ORIGINAL_DOCX  = "/mnt/user-data/uploads/CROSSBOW_MINI_USER_GUIDE__v20260205_.docx"
OUTPUT_DIR     = "/mnt/user-data/outputs"

# ── XML helpers ───────────────────────────────────────────────────────────────

def _pid():
    """Generate a valid paraId (28-bit hex, < 0x7FFFFFFF)."""
    return format(uuid.uuid4().int & 0x0FFFFFFF | 0x01000000, '08X')

def esc(text):
    """XML-escape text."""
    return (text
        .replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
        .replace("'", "&apos;"))

def p(text, bold=False, indent=False, spacing_before=0, spacing_after=100):
    """Standard body paragraph."""
    ind = '<w:ind w:left="360"/>' if indent else ''
    sp  = f'<w:spacing w:before="{spacing_before}" w:after="{spacing_after}"/>'
    rpr = '<w:rPr><w:b/></w:rPr>' if bold else ''
    return f'''    <w:p w14:paraId="{_pid()}" w14:textId="77777777" w:rsidR="00CB2100" w:rsidRDefault="00CB2100">
      <w:pPr>{sp}{ind}</w:pPr>
      <w:r>{rpr}
        <w:t xml:space="preserve">{esc(text)}</w:t>
      </w:r>
    </w:p>'''

def h1(text):
    return f'''    <w:p w14:paraId="{_pid()}" w14:textId="77777777" w:rsidR="00CB2100" w:rsidRDefault="00CB2100">
      <w:pPr><w:pStyle w:val="Heading1"/></w:pPr>
      <w:r><w:t>{esc(text)}</w:t></w:r>
    </w:p>'''

def h2(text):
    return f'''    <w:p w14:paraId="{_pid()}" w14:textId="77777777" w:rsidR="00CB2100" w:rsidRDefault="00CB2100">
      <w:pPr><w:pStyle w:val="Heading2"/></w:pPr>
      <w:r><w:t>{esc(text)}</w:t></w:r>
    </w:p>'''

def h3(text):
    return f'''    <w:p w14:paraId="{_pid()}" w14:textId="77777777" w:rsidR="00CB2100" w:rsidRDefault="00CB2100">
      <w:pPr><w:pStyle w:val="Heading3"/></w:pPr>
      <w:r><w:t>{esc(text)}</w:t></w:r>
    </w:p>'''

def spacer():
    return f'    <w:p w14:paraId="{_pid()}" w14:textId="77777777" w:rsidR="00CB2100" w:rsidRDefault="00CB2100"/>'

def reserved():
    return p("[Reserved — content to be added in a future revision.]", bold=False)

def note(text):
    return f'''    <w:p w14:paraId="{_pid()}" w14:textId="77777777" w:rsidR="00CB2100" w:rsidRDefault="00CB2100">
      <w:pPr>
        <w:spacing w:after="100"/>
        <w:ind w:left="360"/>
        <w:rPr><w:i/><w:color w:val="595959"/></w:rPr>
      </w:pPr>
      <w:r>
        <w:rPr><w:i/><w:color w:val="595959"/></w:rPr>
        <w:t xml:space="preserve">Note: {esc(text)}</w:t>
      </w:r>
    </w:p>'''

def two_col_table(rows, col1_w=2800, col2_w=6560):
    """Simple two-column info table matching the document's existing table style."""
    total = col1_w + col2_w
    border = '<w:top w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/><w:left w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/><w:bottom w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/><w:right w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/>'
    marg  = '<w:top w:w="60" w:type="dxa"/><w:left w:w="120" w:type="dxa"/><w:bottom w:w="60" w:type="dxa"/><w:right w:w="120" w:type="dxa"/>'

    def header_row(c1, c2):
        return f'''      <w:tr w14:paraId="{_pid()}" w14:textId="77777777">
        <w:trPr><w:tblHeader/></w:trPr>
        <w:tc><w:tcPr><w:tcW w:w="{col1_w}" w:type="dxa"/><w:tcBorders>{border}</w:tcBorders><w:shd w:val="clear" w:fill="D9D9D9"/><w:tcMar>{marg}</w:tcMar></w:tcPr>
          <w:p w14:paraId="{_pid()}" w14:textId="77777777"><w:pPr><w:spacing w:after="60"/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val="20"/></w:rPr><w:t>{esc(c1)}</w:t></w:r></w:p>
        </w:tc>
        <w:tc><w:tcPr><w:tcW w:w="{col2_w}" w:type="dxa"/><w:tcBorders>{border}</w:tcBorders><w:shd w:val="clear" w:fill="D9D9D9"/><w:tcMar>{marg}</w:tcMar></w:tcPr>
          <w:p w14:paraId="{_pid()}" w14:textId="77777777"><w:pPr><w:spacing w:after="60"/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val="20"/></w:rPr><w:t>{esc(c2)}</w:t></w:r></w:p>
        </w:tc>
      </w:tr>'''

    def data_row(c1, c2, shade):
        fill = "DEEAF1" if shade else "FFFFFF"
        return f'''      <w:tr w14:paraId="{_pid()}" w14:textId="77777777">
        <w:tc><w:tcPr><w:tcW w:w="{col1_w}" w:type="dxa"/><w:tcBorders>{border}</w:tcBorders><w:shd w:val="clear" w:fill="{fill}"/><w:tcMar>{marg}</w:tcMar></w:tcPr>
          <w:p w14:paraId="{_pid()}" w14:textId="77777777"><w:pPr><w:spacing w:after="60"/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val="20"/></w:rPr><w:t xml:space="preserve">{esc(c1)}</w:t></w:r></w:p>
        </w:tc>
        <w:tc><w:tcPr><w:tcW w:w="{col2_w}" w:type="dxa"/><w:tcBorders>{border}</w:tcBorders><w:shd w:val="clear" w:fill="{fill}"/><w:tcMar>{marg}</w:tcMar></w:tcPr>
          <w:p w14:paraId="{_pid()}" w14:textId="77777777"><w:pPr><w:spacing w:after="60"/></w:pPr><w:r><w:rPr><w:sz w:val="20"/></w:rPr><w:t xml:space="preserve">{esc(c2)}</w:t></w:r></w:p>
        </w:tc>
      </w:tr>'''

    tbl = f'''    <w:tbl>
      <w:tblPr>
        <w:tblW w:w="{total}" w:type="dxa"/>
        <w:tblInd w:w="85" w:type="dxa"/>
        <w:tblLook w:val="04A0" w:firstRow="1"/>
      </w:tblPr>
      <w:tblGrid>
        <w:gridCol w:w="{col1_w}"/>
        <w:gridCol w:w="{col2_w}"/>
      </w:tblGrid>'''

    has_header = isinstance(rows[0], dict) and rows[0].get('header')
    for i, row in enumerate(rows):
        if isinstance(row, dict) and row.get('header'):
            tbl += '\n' + header_row(row['c1'], row['c2'])
        else:
            c1, c2 = row
            shade = i % 2 == 1
            tbl += '\n' + data_row(c1, c2, shade)

    tbl += '\n    </w:tbl>'
    return tbl

def numbered_step(number, title, body_text):
    """Numbered step with bold title."""
    return f'''    <w:p w14:paraId="{_pid()}" w14:textId="77777777" w:rsidR="00CB2100" w:rsidRDefault="00CB2100">
      <w:pPr><w:spacing w:before="120" w:after="60"/></w:pPr>
      <w:r><w:rPr><w:b/></w:rPr><w:t xml:space="preserve">Step {number} \u2014 {esc(title)}. </w:t></w:r>
      <w:r><w:t xml:space="preserve">{esc(body_text)}</w:t></w:r>
    </w:p>'''

# ── Per-guide configurations ──────────────────────────────────────────────────

GUIDES = {
    "theia": {
        "title":          "THEIA Operator User Guide",
        "cover_subtitle": "THEIA OPERATOR USER GUIDE",
        "header_title":   "THEIA OPERATOR USER GUIDE  |  CROSSBOW MINI 3-8kW",
        "doc_number":     "TBD",
        "version":        "1.0.0",
        "date":           "2026-03-16",
        "author":         "B. Allison",
        "classification": "USER-FACING \u2014 AUTHORISED OPERATORS",
        "output_file":    "CROSSBOW_UG_THEIA_v1.0.0.docx",
        "revision_entries": [
            ("1.0.0", "2026-03-16", "B. Allison", "Initial release."),
        ],
    },
    "hyperion": {
        "title":          "HYPERION Operator User Guide",
        "cover_subtitle": "HYPERION OPERATOR USER GUIDE",
        "header_title":   "HYPERION USER GUIDE  |  CROSSBOW MINI 3-8kW",
        "doc_number":     "TBD",
        "version":        "1.0.0",
        "date":           "2026-03-16",
        "author":         "B. Allison",
        "classification": "CONTROLLED \u2014 AUTHORISED INTEGRATORS ONLY",
        "output_file":    "CROSSBOW_UG_HYPERION_v1.0.0.docx",
        "revision_entries": [
            ("1.0.0", "2026-03-16", "B. Allison", "Initial release."),
        ],
    },
    "eng_gui": {
        "title":          "TRC3 Engineering GUI User Guide",
        "cover_subtitle": "TRC3 ENGINEERING GUI USER GUIDE",
        "header_title":   "ENG GUI USER GUIDE  |  CROSSBOW MINI 3-8kW",
        "doc_number":     "TBD",
        "version":        "1.0.0",
        "date":           "2026-03-16",
        "author":         "B. Allison",
        "classification": "CONFIDENTIAL \u2014 INTERNAL USE ONLY",
        "output_file":    "CROSSBOW_UG_ENG_GUI_v1.0.0.docx",
        "revision_entries": [
            ("1.0.0", "2026-03-16", "B. Allison", "Initial release."),
        ],
    },
    "emplacement": {
        "title":          "CROSSBOW Emplacement GUI User Guide",
        "cover_subtitle": "EMPLACEMENT GUI USER GUIDE",
        "header_title":   "EMPLACEMENT GUI USER GUIDE  |  CROSSBOW MINI 3-8kW",
        "doc_number":     "TBD",
        "version":        "1.0.0",
        "date":           "2026-03-16",
        "author":         "B. Allison",
        "classification": "USER-FACING \u2014 AUTHORISED PERSONNEL",
        "output_file":    "CROSSBOW_UG_EMPLACEMENT_v1.0.0.docx",
        "revision_entries": [
            ("1.0.0", "2026-03-16", "B. Allison", "Initial release."),
        ],
    },
}

# ── Per-guide body content ────────────────────────────────────────────────────

def body_theia():
    return "\n".join([
        h1("1. Overview"),
        p("THEIA is the operator Human Machine Interface (HMI) for the CROSSBOW Mini HEL weapon system. It connects to the Mission Control Computer (MCC) and Beam Director Computer (BDC) via the A3 external port and receives live H.264 video from TRC3 on port 5000. THEIA accepts target cueing from any conforming CUE source (HYPERION or third-party) on UDP port 10009 and drives gimbal pointing and fire control automatically on receipt of a valid cue."),
        spacer(),
        two_col_table([
            {"header": True, "c1": "Node", "c2": "Function"},
            ("MCC  \u2014  192.168.1.10", "Power, laser, GNSS, charger, thermal management"),
            ("BDC  \u2014  192.168.1.20", "Gimbal, cameras, FSM, MWIR, fire control geometry"),
            ("TRC3  \u2014  192.168.1.22", "H.264 video source (RTP port 5000)"),
            ("CUE source  \u2014  .200\u2013.254", "External track input \u2014 HYPERION or third-party conforming sender"),
        ]),
        spacer(),

        h1("2. System Prerequisites"),
        p("Before launching THEIA, confirm the following are satisfied:"),
        p("All five controllers are powered and reachable on the 192.168.1.x network.", indent=True),
        p("TRC3 video stream is running (multi_streamer --dest-host 192.168.1.8).", indent=True),
        p("NTP synchronisation is stable \u2014 the NTP heartbeat in the THEIA status display should be non-zero and incrementing.", indent=True),
        p("If a CUE source is in use, it is configured to send to 192.168.1.8:10009.", indent=True),
        p("THEIA begins receiving unsolicited telemetry from MCC and BDC at 100 Hz automatically once connected. No explicit registration command is required."),
        spacer(),

        h1("3. System States"),
        p("All controllers share the SYSTEM_STATES enumeration. THEIA displays and sets system state via the state transition buttons. The normal operational sequence is: OFF \u2192 STANDBY \u2192 ISR \u2192 COMBAT."),
        spacer(),
        two_col_table([
            {"header": True, "c1": "State", "c2": "Meaning"},
            ("OFF (0)",     "System off \u2014 controllers not processing commands"),
            ("STANDBY (1)", "Powered, initialising \u2014 not operational"),
            ("ISR (2)",     "Sensors active, tracking enabled, laser safe"),
            ("COMBAT (3)",  "Fire control active \u2014 laser may be enabled when all votes pass"),
            ("MAINT (4)",   "Maintenance access \u2014 engineering use only"),
            ("FAULT (5)",   "Fault state \u2014 check MCC and BDC status bits"),
        ]),
        spacer(),

        h1("4. Gimbal Modes"),
        p("The gimbal mode is set from the THEIA mode panel. The active mode is displayed in the BDC telemetry strip."),
        spacer(),
        two_col_table([
            {"header": True, "c1": "Mode", "c2": "Behaviour"},
            ("OFF (0)",    "Gimbal unpowered"),
            ("POS (1)",    "Position mode \u2014 direct azimuth/elevation commanded by the operator"),
            ("RATE (2)",   "Rate mode \u2014 velocity commanded via the Xbox controller right thumbstick"),
            ("CUE (3)",    "Cue track \u2014 gimbal driven to NED az/el from incoming CUE packet"),
            ("ATRACK (4)", "Auto-track \u2014 pointing driven by TRC3 video tracker output"),
            ("FTRACK (5)", "Fine-track \u2014 FSM active, gimbal held, FSM corrects residual error"),
        ]),
        p("CUE mode is entered automatically when THEIA receives a valid CUE packet. The operator does not need to set this manually during normal CUE-driven operation."),
        spacer(),

        h1("5. Engagement Sequence"),
        p("The following describes the normal CUE-driven engagement sequence from startup to engagement. All emplacement data (horizon, KIZ, LCH) must be loaded before commencing. For full command and register detail, see the CROSSBOW ICD (INT_OPS)."),
        spacer(),
        numbered_step(1, "System to ISR",
            "Apply power and confirm all controllers appear in the THEIA network status panel. Verify PBIT has passed (all device ready bits set). Transition both MCC and BDC to ISR state using the state transition panel."),
        numbered_step(2, "Load platform position and attitude",
            "Confirm the NovAtel GNSS receiver has acquired a position solution and TerraStar-C PRO correction is active. Transfer the platform latitude, longitude, and altitude (HAE) from the GNSS readout to THEIA and latch the values. If attitude refinement has been performed, apply the corrected roll, pitch, and yaw offsets. These values are transmitted to BDC for NED-frame gimbal pointing."),
        numbered_step(3, "Load horizon and engagement zone data",
            "Load the terrain horizon file (generated by the Emplacement GUI \u2014 see the Emplacement GUI User Guide) and send to BDC. Load the KIZ and LCH files and upload to BDC. Confirm all three zone flags (Horizon Loaded, KIZ Loaded, LCH Loaded) are set in the THEIA vote panel before proceeding to COMBAT."),
        numbered_step(4, "CUE arrives; gimbal slews",
            "On receipt of a valid CUE packet, THEIA automatically converts the target position to NED azimuth/elevation, commands the gimbal to CUE mode, and begins tracking. Monitor the gimbal LOS display to confirm the gimbal is slewing toward the target."),
        numbered_step(5, "Transition to Auto-Track (ATRACK)",
            "Once the target is in the camera field of view and the video tracker indicates Valid, initiate ATRACK mode from the mode panel. In ATRACK, pointing is driven by the TRC3 video tracker, providing tighter tracking for the final engagement phase."),
        numbered_step(6, "Set COMBAT, arm, and submit engagement vote",
            "Transition both MCC and BDC to COMBAT state. Submit the operator PALOS vote confirming the target is within the authorised engagement zone. Monitor the fire control vote panel \u2014 all BDC geometry votes and MCC hardware votes must pass simultaneously."),
        numbered_step(7, "Engage",
            "With all votes passing, set the laser power level and submit the fire vote using the Xbox controller (hold left and right triggers simultaneously). The fire vote is a heartbeat \u2014 releasing either trigger cancels it immediately. Hardware interlocks remain active and cannot be bypassed."),
        numbered_step(8, "Safe shutdown",
            "Disable the laser, transition both controllers to STANDBY, park the gimbal, set gimbal mode OFF, then transition both controllers to OFF."),
        spacer(),

        h1("6. Camera and Video Controls"),
        p("All camera commands are sent to BDC, which routes them to the appropriate device. The active camera selection (VIS or MWIR) is displayed in the THEIA camera panel and reflected in BDC telemetry."),
        spacer(),
        two_col_table([
            {"header": True, "c1": "Control", "c2": "Function"},
            ("Active camera",      "Switch between VIS (Alvium) and MWIR (cooled thermal) via the camera select button"),
            ("Zoom",               "VIS zoom level controlled via D-pad up/down on the Xbox controller"),
            ("Focus",              "Manual focus via D-pad left/right (coarse) or Left Shoulder + D-pad (fine); Y button for autofocus"),
            ("MWIR polarity",      "Toggle white-hot / black-hot via B button"),
            ("Overlay bitmask",    "HUD elements (reticle, track preview, track box, CUE chevrons, OSD) controlled via THEIA overlay panel"),
            ("View mode",          "VIS standalone, MWIR standalone, or PIP composite selected from the view mode panel"),
        ]),
        spacer(),

        h1("7. Xbox Controller Mapping"),
        p("The Xbox controller provides hands-on control of gimbal, tracker, camera, and fire vote. THEIA polls the controller at 50 Hz."),
        spacer(),
        two_col_table([
            {"header": True, "c1": "Input", "c2": "Action"},
            ("Right trigger (short press)",         "Advance system mode"),
            ("Right shoulder (short press)",        "Regress system mode"),
            ("Left + Right trigger (held)",         "FIRE vote \u2014 heartbeat; releasing either trigger cancels immediately"),
            ("Left thumbstick",                     "Track gate size (width/height)"),
            ("Left Shoulder + Left thumbstick",     "Track gate position (centre)"),
            ("Left hat click",                      "Reset gate to 640\u00d7360, 100\u00d7100"),
            ("D-pad \u2191 / \u2193",               "Zoom in / out"),
            ("D-pad \u2190 / \u2192",               "Focus NEAR / FAR (coarse)"),
            ("Left Shoulder + D-pad \u2190/\u2192", "Focus NEAR / FAR (fine)"),
            ("Right thumbstick",                    "POS: gimbal velocity | CUE: pointing offset | ATRACK: aim-point | FTRACK: FSM offset"),
            ("Right hat click",                     "Zero active offset (context-sensitive)"),
            ("Back",                                "Switch to VIS camera"),
            ("Start",                               "Switch to MWIR camera"),
            ("A",                                   "Toggle CUE flag"),
            ("B",                                   "Toggle MWIR white-hot / black-hot"),
            ("X",                                   "Reset video tracker to current gate"),
            ("Y",                                   "Autofocus"),
        ], col1_w=3200, col2_w=6160),
        spacer(),

        h1("8. Fire Control Votes"),
        p("Fire control requires all votes to pass simultaneously. These are displayed in the THEIA vote panel and cannot be overridden by the operator \u2014 they reflect actual hardware and geometry state."),
        spacer(),
        h2("8.1 MCC Hardware Votes"),
        two_col_table([
            {"header": True, "c1": "Vote bit", "c2": "Meaning"},
            ("isLaserTotalHW",        "All hardware interlocks passed"),
            ("isNotAbort (inverted)", "0 = abort ACTIVE \u2014 must be 1 for fire"),
            ("isArmed",               "Weapon is armed"),
            ("isBDA",                 "Battle damage assessment \u2014 LOS clear"),
            ("isEMON",                "Emergency monitor \u2014 energy monitor OK"),
            ("isLaserFireRequested",  "Trigger pulled"),
            ("isLaserTotal",          "Master MCC vote \u2014 all MCC conditions pass"),
            ("isCombat",              "System is in COMBAT state"),
        ]),
        spacer(),
        h2("8.2 BDC Geometry Votes"),
        two_col_table([
            {"header": True, "c1": "Vote bit", "c2": "Meaning"},
            ("BelowHorizVote",  "Gimbal is below the terrain horizon elevation limit"),
            ("InKIZVote",       "Gimbal LOS is within an active KIZ window"),
            ("InLCHVote",       "Gimbal LOS is within an active LCH time window"),
            ("BDCVote",         "Master BDC geometry vote \u2014 all geometry conditions pass"),
            ("isHorizonLoaded", "Horizon file has been loaded to BDC"),
            ("isFSMLimited",    "FSM is at travel limit (inverted \u2014 0 = limited)"),
        ]),
        p("Clean fire condition: BDCVote = 1 AND isLaserTotal = 1."),
        spacer(),

        h1("9. Fault Handling"),
        h2("9.1 System in FAULT state"),
        p("1. Check the Laser Error Word in MCC telemetry \u2014 non-zero indicates a laser fault."),
        p("2. Check MCC Device Ready Bits \u2014 identify which device is not ready."),
        p("3. Use the THEIA fault panel to issue a Clear Laser Error command."),
        p("4. If the fault persists, reinitialise the affected subsystem from the maintenance panel."),
        p("5. If still unresolved, cycle back through STANDBY \u2192 ISR."),
        spacer(),
        h2("9.2 Gimbal not responding"),
        p("Check the Gimbal Status Bits in BDC telemetry (Connected bit should be set). Use the THEIA reinitialise panel to reinitialise the Galil gimbal connection. After reinitialisation, park the gimbal and confirm home position."),
        spacer(),
        h2("9.3 MWIR not ready"),
        p("Check MWIR Run State in BDC telemetry. States 1\u20136 indicate warm-up in progress \u2014 allow 3\u20135 minutes for the cooler to reach operating temperature. Do not send MWIR commands until Run State = 7 (MAIN_PROC_LOOP)."),
        spacer(),
        h2("9.4 CUE source not arriving"),
        p("Verify the CUE source (HYPERION or third-party) is configured to send to 192.168.1.8:10009. Check the THEIA CUE status indicator. THEIA will not enter CUE mode without a valid CUE packet on UDP port 10009."),
        spacer(),

        h1("10. Related Documents"),
        two_col_table([
            {"header": True, "c1": "Document", "c2": "Content"},
            ("CROSSBOW_ICD_INT_OPS_v3.0.3.docx", "Full command and register reference for A3 port integration"),
            ("HYPERION_USER_GUIDE",               "HYPERION sensor fusion and cueing system"),
            ("EMPLACEMENT_GUI_USER_GUIDE",         "Horizon generation, KIZ/LCH loading, platform registration"),
            ("ENG_GUI_USER_GUIDE",                 "Engineering diagnostic access \u2014 IPG internal use"),
            ("GSTREAMER_INSTALL.md",               "GStreamer installation for H.264 video receive"),
        ]),
    ])

def body_hyperion():
    return "\n".join([
        h1("1. Overview"),
        p("HYPERION is the sensor fusion and cueing system for CROSSBOW. It aggregates tracks from multiple sensor inputs, applies Kalman filtering to produce a single best-estimate track, and transmits CUE packets to THEIA over UDP port 10009. THEIA responds with system state and fire control vote feedback after each valid CUE packet."),
        spacer(),
        two_col_table([
            {"header": True, "c1": "Sensor", "c2": "Role"},
            ("ADS-B (192.168.1.31)",    "Air track ID, lat/lon/alt, velocity \u2014 1 Hz cooperative"),
            ("Echodyne radar (.34)",    "High-update-rate track, range/bearing \u2014 primary non-cooperative"),
            ("RADAR (.34)",             "Long-range initial detection \u2014 1\u20135 Hz"),
            ("LoRa/MAVLink (.32)",      "Cooperative target track via MAVLink protocol"),
            ("Stellarium (TCP)",        "Star ephemeris for SPACE-class targets"),
        ]),
        spacer(),

        h1("2. CUE Output Protocol"),
        p("HYPERION sends 71-byte CUE packets to THEIA at UDP:10009 using the EXT_OPS frame format. The frame consists of a 7-byte header (magic 0xCB 0x48, command byte, sequence number, payload length), 62-byte payload, and 2-byte CRC-16/CCITT."),
        spacer(),
        h2("2.1 CUE Packet Key Fields"),
        two_col_table([
            {"header": True, "c1": "Field", "c2": "Notes"},
            ("Timestamp [0\u20137]",         "int64 \u2014 milliseconds since Unix epoch from Kalman filter state time"),
            ("Track ID [8\u201315]",          "ICAO or assigned ID, ASCII null-padded to 8 bytes"),
            ("Track Class [16]",           "8 = UAV, 10 = AC_LIGHT (see EXT_OPS ICD for full enum)"),
            ("Track CMD [17]",             "1 = TRACK (normal), 0 = DROP, 4 = WEAPON HOLD, 5 = WEAPON FREE TO FIRE"),
            ("Latitude / Longitude [18\u201333]", "WGS-84 double LE \u2014 Kalman-filtered best estimate"),
            ("Altitude HAE [34\u201337]",    "Height above WGS-84 ellipsoid in metres. Do NOT use MSL."),
            ("Heading [38\u201341]",         "True heading degrees 0\u2013360. North = 0."),
            ("Speed [42\u201345]",           "Ground speed m/s"),
            ("Vz [46\u201349]",              "Vertical speed m/s, positive = climbing"),
        ]),
        spacer(),
        h2("2.2 Track CMD Usage"),
        two_col_table([
            {"header": True, "c1": "Scenario", "c2": "Track CMD"},
            ("Normal track update",              "TRACK (1)"),
            ("Target lost / engagement over",    "DROP (0)"),
            ("Request platform position report", "REPORT POS/ATT (3)"),
            ("Enable continuous status stream",  "REPORT CONTINUOUS ON (254)"),
            ("Inhibit weapon release",           "WEAPON HOLD (4)"),
            ("Release weapon hold",              "WEAPON FREE TO FIRE (5)"),
        ]),
        note("WEAPON FREE TO FIRE releases HYPERION\u2019s software hold. All hardware interlocks and geometry votes remain active \u2014 the weapon cannot fire unless BDCVote and isLaserTotal are both set in the THEIA status response."),
        spacer(),

        h1("3. THEIA Status Response"),
        p("THEIA sends a 39-byte status frame (CMD 0xAF) to HYPERION\u2019s IP on port 10009 after every valid TRACK packet, and at 10 Hz if REPORT CONTINUOUS ON has been sent."),
        spacer(),
        two_col_table([
            {"header": True, "c1": "Field", "c2": "Notes"},
            ("System State [0]",     "0x02 = ISR, 0x03 = COMBAT \u2014 must be ISR or COMBAT for engagement"),
            ("System Mode [1]",      "0x03 = CUE, 0x04 = ATRACK \u2014 confirms THEIA is tracking"),
            ("MCC Vote Bits [3]",    "Bit 6 (isLaserTotal) = master MCC fire vote"),
            ("BDC Vote Bits2 [5]",   "Bit 3 (BDCVote) = master BDC geometry vote"),
            ("Gimbal Az NED [6\u20139]",   "Current gimbal LOS azimuth degrees NED"),
            ("Gimbal El NED [10\u201313]", "Current gimbal LOS elevation degrees NED"),
            ("Laser Az NED [14\u201317]",  "Laser LOS = gimbal + FSM offset"),
            ("Laser El NED [18\u201321]",  "Laser LOS elevation"),
        ]),
        p("For a clean fire condition both must be true: BDC Vote Bits2 bit 3 (BDCVote) = 1 AND MCC Vote Bits bit 6 (isLaserTotal) = 1."),
        spacer(),

        h1("4. Sensor Fusion Architecture"),
        h2("4.1 Kalman Filter"),
        p("HYPERION maintains a single Kalman-filtered state per active track with a state vector of lat, lon, alt (HAE), vx, vy, vz (NED). The prediction step applies dead-reckoning at the native filter rate. The update step fuses sensor measurements weighted by source covariance. The timestamp in the CUE packet reflects the Kalman filter\u2019s state time, not the packet send time \u2014 THEIA uses this for latency compensation."),
        spacer(),
        h2("4.2 Sensor Priority"),
        two_col_table([
            {"header": True, "c1": "Sensor", "c2": "Position accuracy / Update rate"},
            ("ADS-B",        "GPS-reported accuracy \u2014 ~1 Hz \u2014 used for initial track ID and classification"),
            ("Echodyne",     "High radar range accuracy \u2014 10\u201350 Hz \u2014 primary track source when in range"),
            ("RADAR",        "Medium accuracy \u2014 1\u20135 Hz \u2014 long-range initial detection"),
            ("LoRa/MAVLink", "GPS-reported \u2014 variable rate \u2014 cooperative targets, high confidence classification"),
        ]),
        spacer(),
        h2("4.3 Track Handoff"),
        p("When THEIA reports System Mode 0x04 (ATRACK) in the status response, the video tracker has acquired the target. At this point reduce the CUE rate to 1\u20135 Hz and continue monitoring the status response. Resume full-rate CUE if ATRACK is lost (System Mode reverts to CUE)."),
        spacer(),

        h1("5. Engagement Sequence"),
        numbered_step(1, "Track acquisition",
            "HYPERION detects a target via one or more sensors. The Kalman filter is initialised with the first position fix and Track Class is assigned from sensor classification."),
        numbered_step(2, "Session establishment",
            "Send Track CMD = 254 (REPORT CONTINUOUS ON) to begin 10 Hz status stream from THEIA. Verify THEIA responds with System State ISR or COMBAT."),
        numbered_step(3, "CUE tracking",
            "Send Track CMD = 1 (TRACK) at sensor fusion rate (10\u2013100 Hz). Confirm System Mode = CUE in the THEIA status response and that Gimbal Az/El NED is converging toward the commanded position."),
        numbered_step(4, "Monitor engagement zone",
            "Parse BDC Vote Bits2 in the status response. Confirm isHorizonLoaded, isKIZLoaded, isLCHLoaded are set, and that BelowHorizVote, InKIZVote, InLCHVote, and BDCVote are all passing."),
        numbered_step(5, "Confirm fire readiness",
            "All of the following must be true in the status response: MCC bit 1 (isNotAbort) = 1; MCC bit 6 (isLaserTotal) = 1; BDC bit 3 (BDCVote) = 1; System State = COMBAT; System Mode = CUE or ATRACK."),
        numbered_step(6, "Issue WEAPON FREE TO FIRE",
            "Send Track CMD = 5 (WEAPON FREE TO FIRE). Continue sending TRACK at normal rate. The weapon system engages based on internal fire control logic."),
        numbered_step(7, "Disengage",
            "Send Track CMD = 0 (DROP) to release the track. Send Track CMD = 255 (REPORT CONTINUOUS OFF) to stop the status stream."),
        spacer(),

        h1("6. Operational Notes"),
        p("Altitude. Always provide HAE altitude in CUE packets. THEIA does not apply geoid correction. Using MSL instead of HAE will cause pointing errors proportional to the local geoid separation (typically 10\u201350 m).", bold=False),
        p("Track ID stability. Use a consistent Track ID for the same physical target throughout the engagement. THEIA associates state to Track ID \u2014 changing the ID mid-engagement resets the track state.", bold=False),
        p("CUE rate. Maintain at least 10 Hz during active TRACK. THEIA\u2019s track timeout will drop the track if the rate falls below this threshold.", bold=False),
        p("Gimbal vs laser LOS. The status response provides both gimbal LOS and laser LOS (= gimbal + FSM offset). Use the laser LOS angles for fire control geometry assessment.", bold=False),
        spacer(),

        h1("7. Network Reference"),
        two_col_table([
            {"header": True, "c1": "Node", "c2": "Address / Port"},
            ("THEIA",          "192.168.1.8 : 10009 \u2014 UDP. HYPERION \u2192 THEIA (CUE); THEIA \u2192 HYPERION (status)"),
            ("ADS-B decoder",  "192.168.1.31"),
            ("LoRa gateway",   "192.168.1.32"),
            ("RADAR",          "192.168.1.34"),
            ("HYPERION host",  "192.168.1.200\u2013.254 by convention"),
        ]),
        spacer(),

        h1("8. Related Documents"),
        two_col_table([
            {"header": True, "c1": "Document", "c2": "Content"},
            ("CROSSBOW_ICD_EXT_OPS_v3.0.1.docx", "EXT_OPS CUE protocol \u2014 full field definitions, C structs, integration checklist"),
            ("THEIA_USER_GUIDE",                  "THEIA operator guide \u2014 system states, engagement sequence, vote monitoring"),
            ("ARCHITECTURE.md \u00a77.4",          "HYPERION sensor fusion architecture detail"),
        ]),
    ])

def body_eng_gui():
    return "\n".join([
        h1("1. Overview"),
        p("TRC3_ENG_GUI is the engineering diagnostic application for CROSSBOW. It connects via the A2 internal engineering port (UDP 10018, magic 0xCB 0x49) and can address all five embedded controllers directly. Unlike THEIA, the ENG GUI provides access to the full INT_ENG command set including relay control, Vicor enable, FSM axis commissioning, DAC configuration, and fire vote override commands that are not accessible from THEIA."),
        spacer(),
        two_col_table([
            {"header": True, "c1": "Controller", "c2": "IP / A2 Port / Role"},
            ("MCC", "192.168.1.10 : 10018 \u2014 Power, laser, GNSS, charger, thermal"),
            ("BDC", "192.168.1.20 : 10018 \u2014 Gimbal, cameras, FSM, MWIR, fire control geometry"),
            ("TMC", "192.168.1.12 : 10018 \u2014 Thermal management \u2014 direct access"),
            ("FMC", "192.168.1.23 : 10018 \u2014 FSM DAC/ADC \u2014 direct access"),
            ("TRC3","192.168.1.22 : 10018 \u2014 Video pipeline, tracker"),
        ]),
        note("The ENG GUI is not for use during normal mission operation. It requires knowledge of the internal ICD (CROSSBOW_ICD_INT_ENG) and direct network access to the .1\u2013.99 internal subnet. Full command reference is in CROSSBOW_ICD_INT_ENG_v3.0.3.docx."),
        spacer(),

        h1("2. Transport and Parsing"),
        p("The ENG GUI uses the ParseA2() entry point internally. It receives raw 512-byte payloads with no frame header or CRC to validate \u2014 the A2 frame is stripped upstream by the controller before delivery. The TransportPath constructor parameter is set to A2_Internal at initialisation."),
        p("Both THEIA and the ENG GUI share the same CROSSBOW namespace class library (MSG_MCC, MSG_BDC, etc.). The parsing logic is identical \u2014 only the transport entry point differs. Do not diverge class logic between the two applications."),
        spacer(),

        h1("3. Controller Views"),
        h2("3.1 MCC"),
        p("Displays all MCC REG1 fields \u2014 power management, battery state, laser housekeeping, GNSS solution, charger status, TMC embedded block, and vote bits. Provides access to all MCC INT_ENG commands."),
        p("Key fields to monitor during maintenance:", bold=True),
        p("MCC DEVICE_READY_BITS [8] \u2014 all bits should be 1 for a healthy system.", indent=True),
        p("Laser Status Word [50\u201353] and Laser Error Word [54\u201357] \u2014 non-zero indicates a laser fault.", indent=True),
        p("TMC FULL REG [66\u2013129] \u2014 full 64-byte thermal telemetry block.", indent=True),
        spacer(),
        h2("3.2 BDC"),
        p("Displays all BDC REG1 fields \u2014 gimbal state, camera status, FSM, vote bits, and full TRC and FMC embedded register blocks. Provides access to BDC INT_ENG commands."),
        p("Key fields during gimbal commissioning:", bold=True),
        p("GIMBAL STATUS BITS [20] \u2014 bits 0/1/2 = Ready/Connected/Started.", indent=True),
        p("Gimbal Pan/Tilt Count [21\u201328] \u2014 raw encoder counts from the Galil controller.", indent=True),
        p("BDC VOTE BITS1/2 [164\u2013165] \u2014 geometry interlock state.", indent=True),
        spacer(),
        h2("3.3 TMC"),
        p("Displays the TMC REG1 directly (64-byte block). Shows all temperature sensors, pump/fan state, flow rates, and LCM status. Provides access to TMC INT_ENG commands including DAC value setting and LCM/Vicor enable."),
        p("Key fields during thermal commissioning:", bold=True),
        p("TMC STAT BITS1 [7] \u2014 pump, heater, and fan enable flags.", indent=True),
        p("f1/f2 [27\u201328] \u2014 coolant flow rates \u00d710 LPM.", indent=True),
        p("tf1/tf2 [31\u201332] \u2014 coolant temperature readings.", indent=True),
        spacer(),
        h2("3.4 FMC"),
        p("Displays FMC REG1 directly (64-byte block). Shows FSM position (ADC readback), focus stage position/status, and FSM power state. Provides access to FSM axis sign configuration, test scan, and stage calibration."),
        note("FSM Pos X/Y in FMC REG1 (int32, ADC readback) differs from FSM_X/Y in BDC REG1 (int16, commanded). Reconciliation of these two representations is a known open item."),
        spacer(),
        h2("3.5 TRC3"),
        p("Displays TRC REG1 (64-byte block embedded in BDC REG1 at bytes 60\u2013123). Shows tracker state, camera status, vote bit readbacks, NCC score, focus score, and Jetson temperatures."),
        spacer(),

        h1("4. Version Verification"),
        p("All five controllers should report matching major.minor versions. Version strings are decoded from VERSION_PACK(major, minor, patch) where bits [31:24] = major, bits [23:12] = minor, bits [11:0] = patch."),
        spacer(),
        two_col_table([
            {"header": True, "c1": "Controller", "c2": "Expected VERSION_PACK / Decoded"},
            ("MCC",  "0x03000001  \u2014  3.0.1"),
            ("BDC",  "0x03000001  \u2014  3.0.1"),
            ("TMC",  "0x03000002  \u2014  3.0.2  (intentional \u2014 separate patch applied)"),
            ("FMC",  "0x03000001  \u2014  3.0.1"),
            ("TRC3", "0x03000001  \u2014  3.0.1"),
        ]),
        spacer(),

        h1("5. Common Maintenance Tasks"),
        h2("5.1 Reinitialise a subsystem"),
        p("Use SET_MCC_REINIT (0xE0) for MCC subsystems or SET_BDC_REINIT (0xB0) for BDC subsystems. Subsystem byte values are defined in the INT_ENG ICD. The reinitialise command is accessible from the ENG GUI reinit panel."),
        spacer(),
        h2("5.2 Check fire control vote state"),
        p("Fire control vote bits are visible in MCC REG1 [11] and BDC REG1 [164\u2013168]. Use the ENG GUI vote panel to confirm all bits are in the expected state before hardware testing. The MCC vote readback is also mirrored at BDC REG1 [166]."),
        spacer(),
        h2("5.3 FSM axis commissioning"),
        p("FSM axis signs (BDC_SET_FSM_SIGNS, 0xF4, INT_ENG) and home position (BDC_SET_FSM_HOME, 0xF1) are configurable from the ENG GUI. These commands are not available in THEIA. After setting axis signs, verify using the FMC FSM position readback."),
        spacer(),
        h2("5.4 Thermal tuning"),
        p("DAC values for pump speed and LCM control are accessible via TMS_SET_DAC_VALUE (0xE8, INT_ENG). The full TMC_DAC_CHANNELS enumeration is in the INT_ENG ICD Key Enumerations section."),
        spacer(),

        h1("6. Developer Notes"),
        p("The shared class library (namespace CROSSBOW) is used by both THEIA and the ENG GUI. Do not diverge class logic between the two applications \u2014 maintain parity in the shared library."),
        p("ParseA2() is the internal entry point for the ENG GUI. ParseA3() is for THEIA. Both are private \u2014 the public Parse(byte[] data) dispatcher routes via TransportPath."),
        p("SW_VERSION_STRING and FW_VERSION_STRING in all MSG classes use no v prefix: format is major.minor.patch (e.g., 3.0.1). No double-v in UI display strings."),
        spacer(),

        h1("7. Related Documents"),
        two_col_table([
            {"header": True, "c1": "Document", "c2": "Content"},
            ("CROSSBOW_ICD_INT_ENG_v3.0.3.docx", "Full INT_ENG command set, register layouts, all five controllers"),
            ("THEIA_USER_GUIDE",                  "THEIA operator guide \u2014 A3 port, operator-accessible commands"),
            ("ARCHITECTURE.md",                   "Network topology, framing protocol, tri-port architecture"),
        ]),
    ])

def body_emplacement():
    return "\n".join([
        h1("1. Overview"),
        p("The CROSSBOW Emplacement GUI is a standalone Windows application used to prepare all mission data before THEIA goes operational. It is not a real-time control application \u2014 its outputs are files and configuration data that THEIA loads at mission start. It handles four functional areas: horizon profile generation, LCH file loading and upload, KIZ file loading and upload, and survey points file preparation for platform registration."),
        spacer(),
        two_col_table([
            {"header": True, "c1": "Function", "c2": "Output / Loaded by"},
            ("Horizon generation",   "Terrain profile .txt file (360 float values) \u2192 THEIA \u2192 BDC via SET_BDC_HORIZ"),
            ("LCH file loading",     "Window data uploaded from THEIA to BDC via SET_LCH_MISSION_DATA / SET_LCH_TARGET_DATA"),
            ("KIZ file loading",     "Window data uploaded from THEIA to BDC via same commands"),
            ("Survey points file",   "Survey file loaded in THEIA \u2192 THEIA transmits LLA/ATT to BDC via SET_SYS_LLA / SET_SYS_ATT"),
        ]),
        spacer(),
        h2("1.1 Pre-Mission Workflow"),
        p("Step 1: Emplacement GUI \u2014 generate horizon file, load and verify LCH file, load and verify KIZ file, upload LCH and KIZ to BDC.", bold=False),
        p("Step 2: THEIA \u2014 load horizon .txt and send to BDC, set platform LLA, set platform attitude, confirm all BDC vote zone flags are set before entering COMBAT.", bold=False),
        spacer(),

        h1("2. Horizon Generator"),
        p("The Horizon Generator computes a full 360\u00b0 terrain profile for the emplacement position using USGS National Elevation Dataset (NED) data. The output captures the maximum terrain blocking elevation angle at every azimuth degree. This file is loaded into THEIA and transmitted to BDC, where it is used to suppress fire-control votes when the laser LOS is masked by terrain."),
        spacer(),
        h2("2.1 Step-by-Step Procedure"),
        numbered_step(1, "Enter emplacement position",
            "Enter platform latitude (decimal degrees, positive North), longitude (decimal degrees, negative West), and elevation (metres HAE) in the three fields at the top of the Horizon Generator tab."),
        numbered_step(2, "Centre map",
            "Click Centre Map. The map display re-centres on the entered position and the status indicator turns green. This confirms the coordinate entry is valid."),
        numbered_step(3, "Fetch available DTED tiles",
            "Click Fetch. The application queries the USGS National Map API for all NED 1/3 arc-second GeoTIFF tiles covering the current map view. Available tiles are listed with bounding boxes drawn on the map. If no tiles are returned, zoom out to widen the bounding box and fetch again."),
        numbered_step(4, "Download the tile",
            "Select the desired tile (prefer the most recent creation date) and click Download. A save dialog opens pre-populated with the USGS filename. If the tile was previously downloaded, proceed directly to Step 5."),
        numbered_step(5, "Open the GeoTIFF",
            "Click Open and select the downloaded .tif file. The application reads the projection (typically WGS 84) and draws the tile bounding box on the map."),
        numbered_step(6, "Process",
            "Click Process. The application iterates over every raster pixel and computes the terrain elevation angle from the emplacement position, binning results into 1\u00b0 azimuth buckets and retaining the maximum per bucket. Processing time depends on tile size; a full 1/3 arc-second tile is typically 10,800 \u00d7 10,800 pixels."),
        numbered_step(7, "Save output files",
            "A save dialog opens when processing completes. The .txt file (360 plain-text elevation values, BDC input) and .csv file (review spreadsheet) are written simultaneously to the same folder with the same base name."),
        spacer(),
        h2("2.2 Loading the Horizon File in THEIA"),
        p("After saving, load the .txt file in THEIA via the horizon file selector. THEIA reads the 360 float values and transmits them to BDC as a SET_BDC_HORIZ command. Confirm the isHorizonLoaded flag sets in the THEIA vote panel."),
        spacer(),

        h1("3. LCH / KIZ File Management"),
        h2("3.1 LCH Files"),
        p("Launch Corridor Hold (LCH) files, also known as Program Approval Messages (PAMs), are received from Space Command and define time-windowed engagement corridors for laser clearinghouse deconfliction. Each file contains a mission header and one or more targets, each with az/el bounding boxes and open time windows."),
        spacer(),
        p("Loading procedure: Open the LCH tab, click Open and select the LCH file. The parser displays the mission ID, start/stop dates, number of targets, and total windows. Verify the displayed values match the expected mission. Two validation indicators must both show green before upload:", bold=False),
        p("isLocationValid \u2014 source coordinates in the file match the current emplacement position within 10 m.", indent=True),
        p("isOperatorValid \u2014 Laser Owner/Operator field matches 'IPG'.", indent=True),
        p("Click Upload when both indicators are green. Confirm isLCHLoaded sets in the THEIA vote panel."),
        spacer(),
        h2("3.2 KIZ Files"),
        p("Kill Inhibit Zone (KIZ) files use the same format as LCH/PAM files. They define the keep-in volume within which the laser is permitted to fire. Load and upload via the KIZ tab using the same procedure as LCH. Confirm isKIZLoaded sets in the THEIA vote panel."),
        spacer(),
        h2("3.3 File Format"),
        p("Both LCH and KIZ files use a plain-text PAM format. Key header fields: Mission ID, Laser Owner/Operator, Mission Start/Stop Date/Time (UTC), Number of Targets. Each target block defines an azimuth range and elevation range, followed by open time windows."),
        spacer(),
        h2("3.4 BDC Window Evaluation Model"),
        p("BDC evaluates whether the current laser LOS falls within an open window at 1 kHz. For each target, if the LOS azimuth is within [Az1, Az2] AND elevation is within [El1, El2], BDC checks all time windows for that target. If current UTC falls within an open window, InKIZVote or InLCHVote is asserted. Evaluation is rectangular in az/el."),
        spacer(),

        h1("4. Platform Registration"),
        h2("4.1 Native Sensor Accuracy"),
        two_col_table([
            {"header": True, "c1": "Sensor", "c2": "Accuracy"},
            ("NovAtel GNSS (standalone)",              "~2 m horizontal, ~10 m vertical"),
            ("NovAtel with TerraStar-C PRO correction","~2 cm horizontal, ~4 cm vertical"),
            ("IMU pitch / roll",                       "~0.25\u00b0"),
            ("Dual-antenna GNSS azimuth",              "~2\u00b0 \u2014 primary axis requiring refinement for precision engagements"),
        ]),
        note("A 2\u00b0 azimuth error produces approximately 35 m of cross-range pointing error at 1 km range."),
        spacer(),
        h2("4.2 Attitude Refinement Concept"),
        p("Attitude refinement solves for the residual RPY error by correlating known reference points (surveyed ground features or identified stars) with observed gimbal LOS solutions. A minimum of three points are required; five or more are recommended for a robust solution. Points must have adequate angular diversity \u2014 avoid collinear arrangements."),
        spacer(),
        h2("4.3 Survey Points File Format"),
        p("If using surveyed ground features, provide fiducial coordinates in a plain-text survey points file. Format rules: one point per line, fields delimited by semicolons, lines beginning with # are comments, all point IDs must be unique."),
        p("Fields per line: ID (unique string), Latitude (decimal degrees, positive North), Longitude (decimal degrees, negative West), Altitude (metres HAE)."),
        p("Example: S1; 34.66731; -86.46648; 197.3"),
        p("All altitudes must be HAE (WGS-84 ellipsoid height). Differential GPS accuracy is required \u2014 metre-level uncertainty will not support a precision attitude solution."),
        spacer(),
        h2("4.4 Execution in THEIA"),
        p("Load the survey points file in THEIA. For each reference point: cue the gimbal to the fiducial position, offset the LOS to centre precisely on the known point, and record the LOS solution. After recording at least three points (five recommended), THEIA performs the RPY solve. Review the position and attitude corrections in the intermediate text fields before latching. On confirmation, THEIA transmits the corrected LLA and RPY values to BDC."),
        spacer(),

        h1("5. Pre-Mission Verification Checklist"),
        two_col_table([
            {"header": True, "c1": "Item", "c2": "Check"},
            ("Horizon file generated for current emplacement position",                    "\u2610"),
            ("Horizon file loaded in THEIA \u2014 isHorizonLoaded flag set",               "\u2610"),
            ("LCH file received from Space Command \u2014 'For Execution' confirmed",      "\u2610"),
            ("LCH isLocationValid green \u2014 source coordinates match emplacement",      "\u2610"),
            ("LCH isOperatorValid green \u2014 Laser Owner/Operator = IPG",               "\u2610"),
            ("LCH uploaded to BDC \u2014 isLCHLoaded set",                                "\u2610"),
            ("KIZ file loaded and validated",                                              "\u2610"),
            ("KIZ uploaded to BDC \u2014 isKIZLoaded set",                                "\u2610"),
            ("Platform LLA set in THEIA \u2014 GNSS transfer reviewed, latched, sent",   "\u2610"),
            ("Platform attitude set \u2014 RPY offsets reviewed, latched, sent",          "\u2610"),
            ("All BDC vote zone flags confirmed before COMBAT",                            "\u2610"),
        ], col1_w=6400, col2_w=960),
        spacer(),

        h1("6. Related Documents"),
        two_col_table([
            {"header": True, "c1": "Document", "c2": "Content"},
            ("CROSSBOW_ICD_INT_OPS_v3.0.3.docx", "SET_BDC_HORIZ (0xAC), SET_LCH_MISSION_DATA (0xA7), SET_LCH_TARGET_DATA (0xA8), SET_SYS_LLA (0xBA), SET_SYS_ATT (0xBB) payload definitions"),
            ("THEIA_USER_GUIDE",                  "Engagement sequence \u2014 horizon and KIZ/LCH loading steps, platform registration panel"),
            ("ARCHITECTURE.md",                   "Network topology, port reference"),
        ]),
    ])

# ── Body content dispatcher ───────────────────────────────────────────────────

BODY_FUNCS = {
    "theia":       body_theia,
    "hyperion":    body_hyperion,
    "eng_gui":     body_eng_gui,
    "emplacement": body_emplacement,
}

# ── Document generation ───────────────────────────────────────────────────────

def build_guide(guide_id):
    cfg = GUIDES[guide_id]
    print(f"Building {cfg['output_file']} ...")

    # 1. Copy template to working directory
    work_dir = f"/home/claude/work_{guide_id}"
    if os.path.exists(work_dir):
        shutil.rmtree(work_dir)
    shutil.copytree(TEMPLATE_DIR, work_dir)

    # 2. Update document.xml
    doc_path = os.path.join(work_dir, "word", "document.xml")
    with open(doc_path, "r", encoding="utf-8") as f:
        doc = f.read()

    # Cover subtitle
    doc = doc.replace(
        "<w:t>USER GUIDE</w:t>",
        f"<w:t>{cfg['cover_subtitle']}</w:t>",
        1  # first occurrence only (cover page)
    )

    # Author name in revision table (B. Allison appears multiple times — replace all)
    # We target the specific revision table author cells
    doc = doc.replace("<w:t>B. Allison</w:t>", f"<w:t>{cfg['author']}</w:t>")

    # Revision table date
    doc = doc.replace("<w:t>31.01.2026</w:t>", f"<w:t>{cfg['date']}</w:t>")

    # Revision table change record — replace "Initial Release" row description
    doc = doc.replace(
        "<w:t>Initial        </w:t>",
        f"<w:t>Initial release. Version {cfg['version']}.</w:t>"
    )

    # Inject body content — replace everything from after </w:sdtContent></w:sdt> to <w:sectPr
    body_content = BODY_FUNCS[guide_id]()
    toc_end     = "</w:sdtContent>\n    </w:sdt>"
    sect_start  = "\n    <w:sectPr"

    toc_end_idx  = doc.find(toc_end)
    sect_start_idx = doc.find(sect_start)

    assert toc_end_idx != -1,   "Could not find TOC end marker"
    assert sect_start_idx != -1, "Could not find sectPr start marker"

    doc = (
        doc[:toc_end_idx + len(toc_end)]
        + "\n"
        + body_content
        + "\n"
        + doc[sect_start_idx:]
    )

    with open(doc_path, "w", encoding="utf-8") as f:
        f.write(doc)

    # 3. Update header (guide title and doc number)
    hdr_path = os.path.join(work_dir, "word", "header1.xml")
    with open(hdr_path, "r", encoding="utf-8") as f:
        hdr = f.read()
    hdr = hdr.replace("USER GUIDE", cfg["header_title"])
    hdr = hdr.replace("IPGD-0002",  cfg["doc_number"])
    with open(hdr_path, "w", encoding="utf-8") as f:
        f.write(hdr)

    # 4. Update footer (doc number and filename)
    ftr_path = os.path.join(work_dir, "word", "footer1.xml")
    with open(ftr_path, "r", encoding="utf-8") as f:
        ftr = f.read()
    ftr = ftr.replace("XXXX-YY", cfg["doc_number"])
    ftr = ftr.replace(
        "CROSSBOW MINI USER MANUAL (v20260205)",
        cfg["output_file"]
    )
    with open(ftr_path, "w", encoding="utf-8") as f:
        f.write(ftr)

    # 5. Pack
    output_path = os.path.join(OUTPUT_DIR, cfg["output_file"])
    result = subprocess.run(
        ["python3", PACK_SCRIPT, work_dir, output_path,
         "--original", ORIGINAL_DOCX, "--validate", "false"],
        capture_output=True, text=True
    )
    if result.returncode != 0:
        print(f"  ERROR: {result.stdout}\n{result.stderr}")
        return False

    print(f"  Written: {output_path}")
    shutil.rmtree(work_dir)
    return True


def main():
    parser = argparse.ArgumentParser(description="CROSSBOW User Guide Generator")
    parser.add_argument("--guide", choices=list(GUIDES.keys()),
                        help="Guide to build")
    parser.add_argument("--all", action="store_true",
                        help="Build all four guides")
    args = parser.parse_args()

    if not args.all and not args.guide:
        parser.print_help()
        sys.exit(1)

    # Unpack the original docx as the template if template_ug doesn't exist
    if not os.path.exists(TEMPLATE_DIR):
        print(f"Creating template from {ORIGINAL_DOCX} ...")
        result = subprocess.run(
            ["python3", "/mnt/skills/public/docx/scripts/office/unpack.py",
             ORIGINAL_DOCX, TEMPLATE_DIR],
            capture_output=True, text=True
        )
        if result.returncode != 0:
            print(f"Failed to unpack template: {result.stdout}")
            sys.exit(1)
        print("Template ready.")

    guides_to_build = list(GUIDES.keys()) if args.all else [args.guide]
    ok = 0
    for gid in guides_to_build:
        if build_guide(gid):
            ok += 1

    print(f"\n{ok}/{len(guides_to_build)} guides built successfully.")


if __name__ == "__main__":
    main()
