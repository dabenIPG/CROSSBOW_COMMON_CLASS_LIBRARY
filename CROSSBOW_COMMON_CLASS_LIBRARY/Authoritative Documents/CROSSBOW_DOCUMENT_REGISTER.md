# CROSSBOW — Document Register

**Project:** CROSSBOW  
**Document:** `CROSSBOW_DOCUMENT_REGISTER.md`  
**Doc #:** IPGD-0001  
**Version:** 1.4.8  
**Date:** 2026-04-08  
**Status:** Current  

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
  <td>3.4.0</td>
  <td>✅ Current</td>
  <td>2026-04-08</td>
  <td><code>CROSSBOW_ICD_INT_ENG.md</code></td>
  <td><code>gen_eng_icd.js</code></td>
</tr>
<tr>
  <td colspan="8"><em>Full internal engineering ICD covering all five controllers (MCC, BDC, TMC, FMC, TRC) — all INT_ENG and INT_OPS commands, full register layouts, ASCII command reference, and enumeration definitions. Classification: IPG Internal Use Only. Issued to IPG engineering staff only.</em></td>
</tr>

<tr>
  <td><code>CROSSBOW_ICD_INT_OPS.docx</code></td>
  <td>CROSSBOW ICD — Internal Operations</td>
  <td>IPGD-0004</td>
  <td>3.3.8</td>
  <td>✅ Current</td>
  <td>2026-04-07</td>
  <td><code>CROSSBOW_ICD_INT_OPS.md</code></td>
  <td><code>gen_int_ops_icd.js</code></td>
</tr>
<tr>
  <td colspan="8"><em>ICD for Tier 1 integrators and vendor HMI builders — full A3 operator command set (MCC and BDC via port 10050, magic 0xCB 0x58). Reference spec for THEIA and bespoke HMI implementations. Classification: CONTROLLED. Distributed to Tier 1 integrators alongside IPGD-0005.</em></td>
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
  <td>3.3.4</td>
  <td>✅ Current</td>
  <td>2026-04-08</td>
  <td><code>ARCHITECTURE.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>System architecture reference. Covers subsystem relationships, interface topology, A1/A2/A3 port assignments, data flows, client access model, and MCC PTP/NTP time source architecture. v3.3.4: §9 MCC Internal Architecture updated for unified V1/V2 hardware abstraction. §9.6 Build Configuration added. §15 MCC version 3.3.0. §16 MCC HW_REV compatibility entry added.</em></td>
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
  <td>✅ Current</td>
  <td>2026-03-16</td>
  <td><code>GSTREAMER_INSTALL.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Installation and configuration guide for GStreamer on CROSSBOW platform targets. Covers package dependencies, pipeline configuration, and video stream verification.</em></td>
</tr>

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
  <td>✅ Current</td>
  <td>2026-03-16</td>
  <td><code>OPENCV_CUDA_BUILD.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>Build procedure for OpenCV 4.11.0 with CUDA DNN on Jetson Orin NX (aarch64/JetPack). Covers prerequisites, cmake flags, verification steps, COCO inference probe, and troubleshooting. Required for TRC COCO hardware validation.</em></td>
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
  <td colspan="8"><em>NovAtel GNSS receiver configuration reference. Covers network/IP setup, WiFi disable, ICOM UDP config, receiver mode/PPP, antenna lever arm offsets, IMU orientation, PTP grandmaster configuration (PTPMODE ENABLE_FINETIME, PTPTIMESCALE UTC_TIME), MCC data stream setup, and full commissioning sequence. IPG Internal Use Only.</em></td>
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
  <td>1.4.6</td>
  <td>✅ Current</td>
  <td>2026-04-06</td>
  <td><code>CROSSBOW_DOCUMENT_REGISTER.md</code></td>
  <td>—</td>
</tr>
<tr>
  <td colspan="8"><em>This document. Canonical register of all CROSSBOW project documents. Lists filename, title, doc control number, version, status, and release date for all deliverables and source documents. Also serves as a reusable appendix in all controlled documents.</em></td>
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
| IPGD-0006 | CROSSBOW System Architecture v3.3.4 | — | `ARCHITECTURE.md` |
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

<sup>†</sup> Placeholder — pending S21-39.

---

*End of document register.*
