# CROSSBOW — Document Register

**Project:** CROSSBOW  
**Document:** `CROSSBOW_DOCUMENT_REGISTER.md`  
**Doc #:** IPGD-0001  
**Version:** 1.4.9  
**Date:** 2026-04-12  
**Status:** Current  

**v1.4.9 changes (2026-04-12):**
- IPGD-0019 added — `CROSSBOW_CHANGELOG.md` (Changelog and Action Item Register)
- Section 5 added — Retired Working Files (unregistered documents absorbed into IPGD-0019)
- Self-referential entry corrected — version was inconsistent between header (1.4.8) and table entry (1.4.6); both now 1.4.9

---

This register is the canonical reference for all CROSSBOW project documents. It covers controlled deliverables, source documents, and build specifications. Intended for use as a standalone project reference and as a reusable appendix in all controlled documents.

---

## 1 — Interface Control Documents

<table>
<thead>
<tr>
  <th>Deliverable (.docx)</th>
  <th>Title</th>
  <th>Doc #</th>
  <th>Version</th>
  <th>Status</th>
  <th>Release Date</th>
  <th>Source (.md)</th>
  <th>Generator</th>
</tr>
</thead>
<tbody>

<tr>
  <td><code>CROSSBOW_ICD_INT_ENG.docx</code></td>
  <td>CROSSBOW ICD — Internal Engineering</td>
  <td>IPGD-0003</td>
  <td>3.6.0</td>
  <td>⚠️ .md current — docx pending regen</td>
  <td>2026-04-13</td>
  <td><code>CROSSBOW_ICD_INT_ENG.md</code></td>
  <td><code>gen_eng_icd.js</code></td>
</tr>
<tr>
  <td colspan="8"><em>Full internal engineering ICD covering all five controllers (MCC, BDC, TMC, FMC, TRC) — all INT_ENG and INT_OPS commands, full register layouts, ASCII command reference, and enumeration definitions. Classification: IPG Internal Use Only. Issued to IPG engineering staff only. v3.6.0 (2026-04-13): CB-20260412 command space restructuring, FW v4.0.0 fleet-wide, FW-C10 REG1 CMD_BYTE 0xA1→0x00.</em></td>
</tr>

<tr>
  <td><code>CROSSBOW_ICD_INT_OPS.docx</code></td>
  <td>CROSSBOW ICD — Internal Operations</td>
  <td>IPGD-0004</td>
  <td>3.6.0</td>
  <td>⚠️ .md current — docx pending regen</td>
  <td>2026-04-13</td>
  <td><code>CROSSBOW_ICD_INT_OPS.md</code></td>
  <td><code>gen_int_ops_icd.js</code></td>
</tr>
<tr>
  <td colspan="8"><em>ICD for Tier 1 integrators and vendor HMI builders — full A3 operator command set (MCC and BDC via port 10050, magic 0xCB 0x58). Reference spec for THEIA and bespoke HMI implementations. Classification: CONTROLLED. Distributed to Tier 1 integrators alongside IPGD-0005. v3.6.0 (2026-04-13): CB-20260412 command space restructuring, FW v4.0.0, FW-C10 REG1 CMD_BYTE note.</em></td>
</tr>

<tr>
  <td><code>CROSSBOW_ICD_EXT_OPS.docx</code></td>
  <td>CROSSBOW ICD — External Operations</td>
  <td>IPGD-0005</td>
  <td>3.3.0</td>
  <td>✅ Current</td>
  <td>2026-04-05</td>
  <td><code>CROSSBOW_ICD_EXT_OPS.md</code></td>
  <td><code>gen_ext_ops_icd.js</code></td>
</tr>
<tr>
  <td colspan="8"><em>ICD for Tier 2 integrators and CUE providers — EXT_OPS cueing interface via UDP port 15009 (magic 0xCB 0x48). Covers CUE inbound packet (CMD 0xAA), status response (CMD 0xAF), POS/ATT report (CMD 0xAB), integration path guidance (direct THEIA vs HYPERION relay), and C++/C# code reference. Classification: USER-FACING. Distributed to Tier 2 integrators only.</em></td>
</tr>

</tbody>
</table>

---

## 2 — User Documents

<table>
<thead>
<tr>
  <th>Deliverable (.docx)</th>
  <th>Title</th>
  <th>Doc #</th>
  <th>Version</th>
  <th>Status</th>
  <th>Release Date</th>
  <th>Source (.md)</th>
  <th>Generator</th>
</tr>
</thead>
<tbody>

<tr>
  <td><code>CROSSBOW_MINI_USER_GUIDE.docx</code></td>
  <td>CROSSBOW User Manual</td>
  <td>IPGD-0002</td>
  <td>20260316</td>
  <td>✅ Current</td>
  <td>2026-03-16</td>
  <td>—</td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Top-level operator user manual. Covers system overview, emplacement procedure, normal operation, external interface, KIZ generation, maintenance software reference, and full acronym list. Audience: all operators.</em></td>
</tr>

<tr>
  <td><code>CROSSBOW_UG_THEIA.docx</code></td>
  <td>CROSSBOW User Guide — THEIA</td>
  <td>IPGD-0012 <sup>†</sup></td>
  <td>1.0.0</td>
  <td>✅ Current</td>
  <td>2026-03-16</td>
  <td><code>CROSSBOW_UG_THEIA.md</code></td>
  <td><code>gen_user_guides.py</code></td>
</tr>
<tr>
  <td colspan="8"><em>User guide for the THEIA operator interface. Covers engagement sequence, system states, gimbal modes, camera controls, Xbox controller mapping, fire control votes, and fault handling. Classification: USER-FACING.</em></td>
</tr>

<tr>
  <td><code>CROSSBOW_UG_HYPERION.docx</code></td>
  <td>CROSSBOW User Guide — HYPERION</td>
  <td>IPGD-0013 <sup>†</sup></td>
  <td>1.1.1</td>
  <td>✅ Current</td>
  <td>2026-04-05</td>
  <td><code>CROSSBOW_UG_HYPERION.md</code></td>
  <td><code>gen_user_guides.py</code></td>
</tr>
<tr>
  <td colspan="8"><em>User guide for the HYPERION sensor fusion and C2 subsystem. Covers air picture management, track selection, CUE relay protocol, Kalman filter architecture, and multi-THEIA future capability. Classification: CONTROLLED.</em></td>
</tr>

<tr>
  <td><code>CROSSBOW_UG_ENG_GUI.docx</code></td>
  <td>CROSSBOW User Guide — Engineering GUI</td>
  <td>IPGD-0014 <sup>†</sup></td>
  <td>1.3.0</td>
  <td>✅ Current</td>
  <td>2026-04-08</td>
  <td><code>CROSSBOW_UG_ENG_GUI.md</code></td>
  <td><code>gen_user_guides.py</code></td>
</tr>
<tr>
  <td colspan="8"><em>User guide for the Engineering GUI maintenance interface. Covers all five controller views, software version verification, maintenance tasks, and developer notes. Classification: CONFIDENTIAL.</em></td>
</tr>

<tr>
  <td><code>CROSSBOW_UG_EMPLACEMENT.docx</code></td>
  <td>CROSSBOW User Guide — Emplacement GUI</td>
  <td>IPGD-0015 <sup>†</sup></td>
  <td>1.0.0</td>
  <td>✅ Current</td>
  <td>2026-03-16</td>
  <td><code>CROSSBOW_UG_EMPLACEMENT.md</code></td>
  <td><code>gen_user_guides.py</code></td>
</tr>
<tr>
  <td colspan="8"><em>User guide for the Emplacement GUI. Covers horizon generation (7-step procedure), LCH and KIZ management, platform registration, and pre-mission checklist. Classification: USER-FACING.</em></td>
</tr>

</tbody>
</table>

<sup>†</sup> Doc numbers IPGD-0012 through IPGD-0015 are placeholder assignments pending confirmation — see open item S21-39.

---

## 3 — Technical References

<table>
<thead>
<tr>
  <th>Deliverable (.docx)</th>
  <th>Title</th>
  <th>Doc #</th>
  <th>Version</th>
  <th>Status</th>
  <th>Release Date</th>
  <th>Source (.md)</th>
  <th>Generator</th>
</tr>
</thead>
<tbody>

<tr>
  <td>—</td>
  <td>CROSSBOW System Architecture</td>
  <td>IPGD-0006</td>
  <td>3.3.7</td>
  <td>⚠️ Pending ARCH-1 update pass</td>
  <td>2026-04-11</td>
  <td><code>ARCHITECTURE.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>System architecture reference. Covers subsystem relationships, interface topology, A1/A2/A3 port assignments, data flows, client access model, and MCC PTP/NTP time source architecture. v3.3.7 (2026-04-11): FMC STM32F7 migration, BDC V1/V2 unification. Pending ARCH-1: CB-20260412 command space, FW v4.0.0, V1/V2 subsections for all controllers, FW_PATTERNS appendix updates, ICD ref bump to v3.6.0.</em></td>
</tr>

<tr>
  <td>—</td>
  <td>CROSSBOW Changelog and Action Item Register</td>
  <td>IPGD-0019</td>
  <td>1.2.0</td>
  <td>✅ Current</td>
  <td>2026-04-13</td>
  <td><code>CROSSBOW_CHANGELOG.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Unified changelog and action item register. Part 1: session-by-session narrative log. Part 2: all open action items (priority-ordered, subsystem-grouped by FW controller and SW component). Part 3: full closure archive grouped by session. v1.2.0 (2026-04-13): CB-20260412 fleet-wide pass complete (MCC/BDC/TMC/FMC/TRC), BDC/TMC/FMC unification sessions captured, all TRC migration items captured. Supersedes unregistered working files <code>Embedded_Controllers_ACTION_ITEMS.md</code> and <code>Embedded_Controllers_CLOSED_ACTION_ITEMS.md</code> (both retired). Classification: IPG Internal Use Only.</em></td>
</tr>

<tr>
  <td>—</td>
  <td>CROSSBOW Application Summary</td>
  <td>IPGD-0007</td>
  <td>3.0.5</td>
  <td>✅ Current</td>
  <td>2026-03-17</td>
  <td><code>CROSS_APP_SUMMARY.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Summary of all CROSSBOW application components. Cross-reference for software versions, subsystem descriptions, and inter-application dependencies.</em></td>
</tr>

<tr>
  <td>—</td>
  <td>GStreamer Installation Guide</td>
  <td>IPGD-0008</td>
  <td>3.0.0</td>
  <td>⚠️ Active — update pass pending (GST-1)</td>
  <td>2026-03-15</td>
  <td><code>GSTREAMER_INSTALL.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Installation and configuration guide for GStreamer on THEIA operator PC (Windows MSVC). Covers install, nvh264dec/avdec_h264, verified pipeline parameters, unicast/multicast config, EmguCV notes, and known quirks. v3.0.0 (2026-03-15). Pending GST-1 update: §8 multicast references retired 0xD1 ORIN_SET_STREAM_MULTICAST (multicast now via --dest-host launch flag ✅ working); §11 30fps references retired 0xD2; TRC binary name `multi_streamer` → `trc`. Pipeline parameters (buffer, latency, PixelShift) confirmed correct.</em></td>

<tr>
  <td>—</td>
  <td>TRC Migration Guide</td>
  <td>IPGD-0009</td>
  <td>v5</td>
  <td>✅ Current</td>
  <td>2026-03-16</td>
  <td><code>TRC_MIGRATION.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Migration plan for TRC A2 framing upgrade. Covers telemetry struct rewrite, A1/A2 port migration, frame build/parse, client table, and pre-build checklist. Items TRC-M1, M5, M7 pending. Updated session 22: TRC3 → TRC, title updated.</em></td>
</tr>

<tr>
  <td>—</td>
  <td>CROSSBOW Firmware Style and Patterns Reference</td>
  <td>IPGD-0016</td>
  <td>1.7</td>
  <td>✅ Current</td>
  <td>2026-03-16</td>
  <td><code>CROSSBOW_FW_PATTERNS.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Ground-truth reference for firmware implementation patterns across all five controllers. Covers hardware platforms, tri-port architecture, frame format, REG1 encoding, NTP client, serial patterns, known bugs, and C# ENG GUI patterns. IPG Internal Use Only.</em></td>
</tr>

<tr>
  <td>—</td>
  <td>OpenCV CUDA DNN Build Procedure</td>
  <td>IPGD-0017</td>
  <td>1.0</td>
  <td>⛔ Archived — superseded by JETSON_SETUP.md (IPGD-0020)</td>
  <td>2026-03-09</td>
  <td><code>OPENCV_CUDA_BUILD.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Gen 2 build procedure for OpenCV 4.11.0/4.12.0 with CUDA DNN on Jetson Orin NX. Superseded by JETSON_SETUP.md (IPGD-0020) Phase 1 which covers Gen 3 (4.13.0). References stale binary name `multi_streamer` and path `~/CV/TRCv3/v20/`. Archived as historical record alongside OPENCV_BUILD_HISTORY.md. Active procedure: IPGD-0020 + `install_opencv4_13_0_Jetpack6_2_2.sh`.</em></td>
</tr>

<tr>
  <td>—</td>
  <td>OpenCV Build History — Jetson Orin NX</td>
  <td>IPGD-0017a</td>
  <td>—</td>
  <td>⛔ Archived — cmake flag rationale preserved for future upgrades</td>
  <td>2026-04-09</td>
  <td><code>OPENCV_BUILD_HISTORY.md</code></td>
  <td><code>install_opencv4_13_0_Jetpack6_2_2.sh</code></td>
</tr>
<tr>
  <td colspan="8"><em>Three-generation build history (Gen1: 4.8.0 apt, Gen2: 4.12.0, Gen3: 4.13.0). Contains cmake flag comparison table and issue rationale (OPENCV_DNN_CUDA discovery, dist-packages vs site-packages, TBB fix, CUDA_FAST_MATH rationale) — valuable for future OpenCV upgrades. Verification sections reference stale paths. Archived. The cmake flag rationale table should be appended to JETSON_SETUP.md (IPGD-0020) on next update. Companion script `install_opencv4_13_0_Jetpack6_2_2.sh` remains active — referenced by IPGD-0020.</em></td>
</tr>

<tr>
  <td>—</td>
  <td>CROSSBOW GNSS Receiver Configuration</td>
  <td>IPGD-0018</td>
  <td>1.0.0</td>
  <td>✅ Current</td>
  <td>2026-03-28</td>
  <td><code>CROSSBOW_GNSS_CONFIG.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>NovAtel GNSS receiver configuration reference. Covers network/IP setup, WiFi disable, ICOM UDP config, receiver mode/PPP, antenna lever arm offsets, IMU orientation, PTP grandmaster configuration (PTPMODE ENABLE_FINETIME, PTPTIMESCALE UTC_TIME), MCC data stream setup, and full commissioning sequence. IPG Internal Use Only. ⚠️ Document header incorrectly states IPGD-0007 — correct number is IPGD-0018. Fix on next edit. Minor: §7 step numbering skips step 4; two sub-sections labelled "3.1".</em></td>
</tr>

<tr>
  <td>—</td>
  <td>TRC Jetson Orin NX Setup Procedure</td>
  <td>IPGD-0020</td>
  <td>2.2.1</td>
  <td>✅ Current</td>
  <td>2026-04-10</td>
  <td><code>JETSON_SETUP.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Full setup and deployment procedure for TRC Jetson Orin NX (non-Super J4012, J401 carrier, JetPack 6.2.2). Covers four deployment paths: A=fresh build, B=image restore (including upgrades), C=retired, D=Super J4012 (future). Includes SDK Manager flash, OpenCV 4.13.0 build, VimbaX 2026-1, GStreamer, TRC binary deployment, overlayFS read-only rootfs, and gold image creation. Confirmed on 3 units (54 PASS, 0 FAIL). Binary: `~/CV/TRC/trc`. Startup: `trc_start.sh`/`trc_start_bench.sh`. IPG Internal Use Only.</em></td>
</tr>

<tr>
  <td>—</td>
  <td>TRC Jetson Super J4012 Setup Procedure</td>
  <td>IPGD-0021</td>
  <td>—</td>
  <td>⏳ Pending</td>
  <td>—</td>
  <td><code>JETSON_SUPER_SETUP.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Setup procedure for reComputer Super J4012 (new carrier, higher TDP, Super Mode support). Not yet written — Super J4012 units share TRC role IP 192.168.1.22 but are mechanically incompatible with non-Super images. Do not apply JETSON_SETUP.md (IPGD-0020) to Super units. See JETSON_SETUP.md §Overview Path D for placeholder.</em></td>
</tr>

</tbody>
</table>

---

## 4 — Build Specifications

<table>
<thead>
<tr>
  <th>Deliverable (.docx)</th>
  <th>Title</th>
  <th>Doc #</th>
  <th>Version</th>
  <th>Status</th>
  <th>Release Date</th>
  <th>Source (.md)</th>
  <th>Generator</th>
</tr>
</thead>
<tbody>

<tr>
  <td>—</td>
  <td>CROSSBOW ICD Build Specification</td>
  <td>IPGD-0010</td>
  <td>3.1.0</td>
  <td>✅ Current</td>
  <td>2026-03-17</td>
  <td><code>CROSSBOW_ICD_BUILD_SPEC.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Build specification for all three ICD Word document generators. Updated session 24 to reflect v3.1.0 document structure changes — doc numbers, unversioned filenames, classification fields, new sections, and appendices.</em></td>
</tr>

<tr>
  <td>—</td>
  <td>CROSSBOW User Guide Build Specification</td>
  <td>IPGD-0011</td>
  <td>1.0.0</td>
  <td>⚠️ Current — S21-39/40 pending</td>
  <td>2026-03-16</td>
  <td><code>USER_GUIDE_BUILD_SPEC.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Build specification for the user guide Word document generator. Covers common 8-section skeleton, colour palette, typography, classification label assignments, and gen_user_guides.py GUIDES configuration block. Doc numbers (S21-39) and classification labels (S21-40) pending confirmation.</em></td>
</tr>

<tr>
  <td>—</td>
  <td>CROSSBOW Document Register</td>
  <td>IPGD-0001</td>
  <td>1.4.9</td>
  <td>✅ Current</td>
  <td>2026-04-12</td>
  <td><code>CROSSBOW_DOCUMENT_REGISTER.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>This document. Canonical register of all CROSSBOW project documents. Lists filename, title, doc control number, version, status, and release date for all deliverables and source documents. Also serves as a reusable appendix in all controlled documents.</em></td>
</tr>

</tbody>
</table>

---

## 5 — Retired Working Files

Unregistered working files absorbed into registered documents and retired in place. These files should not be updated — all future changes go to the superseding document.

<table>
<thead>
<tr>
  <th>Filename</th>
  <th>Former Purpose</th>
  <th>Status</th>
  <th>Retired Date</th>
  <th>Superseded By</th>
</tr>
</thead>
<tbody>

<tr>
  <td><code>Embedded_Controllers_ACTION_ITEMS.md</code></td>
  <td>Open action item tracking — embedded firmware and ENG GUI</td>
  <td>🚫 Retired</td>
  <td>2026-04-12</td>
  <td>IPGD-0019 Part 2 — <code>CROSSBOW_CHANGELOG.md</code></td>
</tr>

<tr>
  <td><code>Embedded_Controllers_CLOSED_ACTION_ITEMS.md</code></td>
  <td>Closed action item archive — embedded firmware and ENG GUI</td>
  <td>🚫 Retired</td>
  <td>2026-04-12</td>
  <td>IPGD-0019 Part 3 — <code>CROSSBOW_CHANGELOG.md</code></td>
</tr>

</tbody>
</table>

---

## Document Number Index

| Doc # | Title | Deliverable | Source |
|-------|-------|-------------|--------|
| IPGD-0001 | CROSSBOW Document Register | — | `CROSSBOW_DOCUMENT_REGISTER.md` |
| IPGD-0002 | CROSSBOW User Manual | `CROSSBOW_MINI_USER_GUIDE.docx` | — |
| IPGD-0003 | CROSSBOW ICD — Internal Engineering | `CROSSBOW_ICD_INT_ENG.docx` | `CROSSBOW_ICD_INT_ENG.md` |
| IPGD-0004 | CROSSBOW ICD — Internal Operations | `CROSSBOW_ICD_INT_OPS.docx` | `CROSSBOW_ICD_INT_OPS.md` |
| IPGD-0005 | CROSSBOW ICD — External Operations | `CROSSBOW_ICD_EXT_OPS.docx` | `CROSSBOW_ICD_EXT_OPS.md` |
| IPGD-0006 | CROSSBOW System Architecture | — | `ARCHITECTURE.md` |
| IPGD-0007 | CROSSBOW Application Summary | — | `CROSS_APP_SUMMARY.md` |
| IPGD-0008 | GStreamer Installation Guide | — | `GSTREAMER_INSTALL.md` |
| IPGD-0009 | TRC Migration Guide | — | `TRC_MIGRATION.md` |
| IPGD-0010 | CROSSBOW ICD Build Specification | — | `CROSSBOW_ICD_BUILD_SPEC.md` |
| IPGD-0011 | CROSSBOW User Guide Build Specification | — | `USER_GUIDE_BUILD_SPEC.md` |
| IPGD-0012 † | CROSSBOW User Guide — THEIA | `CROSSBOW_UG_THEIA.docx` | `CROSSBOW_UG_THEIA.md` |
| IPGD-0013 † | CROSSBOW User Guide — HYPERION | `CROSSBOW_UG_HYPERION.docx` | `CROSSBOW_UG_HYPERION.md` |
| IPGD-0014 † | CROSSBOW User Guide — Engineering GUI | `CROSSBOW_UG_ENG_GUI.docx` | `CROSSBOW_UG_ENG_GUI.md` |
| IPGD-0015 † | CROSSBOW User Guide — Emplacement GUI | `CROSSBOW_UG_EMPLACEMENT.docx` | `CROSSBOW_UG_EMPLACEMENT.md` |
| IPGD-0016 | CROSSBOW Firmware Style and Patterns Reference | — | `CROSSBOW_FW_PATTERNS.md` |
| IPGD-0017 | OpenCV CUDA DNN Build Procedure | — | `OPENCV_CUDA_BUILD.md` |
| IPGD-0018 | CROSSBOW GNSS Receiver Configuration | — | `CROSSBOW_GNSS_CONFIG.md` |
| IPGD-0019 | CROSSBOW Changelog and Action Item Register | — | `CROSSBOW_CHANGELOG.md` |

<sup>†</sup> Placeholder — pending S21-39.

---

*End of document register.*
