# CROSSBOW — Closed Action Items

**Document:** `PTP_CLOSED_ACTIONS.md`
**Version:** 1.1.0
**Date:** 2026-04-05
**Project:** CROSSBOW

---

| ID | Title | Resolution | Component | Type | Session | Date Closed |
|----|-------|------------|-----------|------|---------|-------------|
| NEW-31 | frmMain.cs SET_LCH_VOTE arg swap — operatorValid duplicated | Fixed — operatorValid hardcoded to true pending proper implementation; duplicated isLocationValid arg corrected. NEW-39 opened to track full implementation. | C# | Bug | S36 | 2026-04-05 |
| NEW-9 | MSG_MCC.cs HW verify | HW verified — all fields confirmed correct on live hardware | C# | Test | S36 | 2026-04-05 |
| NEW-10 | MSG_BDC.cs HW verify | HW verified — all fields confirmed correct on live hardware | C# | Test | S36 | 2026-04-05 |
| NEW-18 | CRC cross-platform wire verification | Verified — CRC-16/CCITT confirmed correct across all five controllers and C# | FW / C# | Test | S36 | 2026-04-05 |
| NEW-36 | PTP integration — HW verify | S28/29 — TIME confirmed: offset_us=12, active source: PTP, time=2026-03-28. ENG GUI verified. | FW / C# | Test | S29 | 2026-03-28 |
| NEW-35 | FW: all firmware targets NTP .33 directly | Confirmed — IP_NTP_BYTES = .33 in defines.hpp; fallback .208 configured by default | FW | Bug | S27 | 2026-03-27 |
| NEW-38c | FMC PTP integration | FMC socket budget 4/8; TIME_BITS at byte 44; TIMESRC/TIME/PTPDEBUG serial commands implemented | FW | Feature | S33 | 2026-03-28 |
| NEW-38b | BDC PTP integration | BDC socket budget corrected (9/8 to 7/8); PTP boot step added; TIME_BITS at byte 391 | FW | Feature | S32 | 2026-03-28 |
| NEW-38a | TMC PTP integration | STAT_BITS3 at byte 61, TIME/TIMESRC/PTPDEBUG serial commands, MSG_TMC.cs updated | FW | Feature | S30/31 | 2026-03-28 |
| FW-1 | PTPDEBUG serial command | Implemented and verified on MCC; propagated to all controllers | FW | Feature | S30 | 2026-03-28 |
| FW-2 | TIMESRC UDP command | Implemented across all controllers | FW | Feature | S30 | 2026-03-28 |
| NEW-13 | ICD scope labels INT_OPS/INT_ENG applied | Applied ICD v3.1.0 — all commands labelled with scope | ICD | Feature | S22 | 2026-03-16 |
| NEW-12 | TransportPath enum — MSG_MCC/BDC | Complete — deployed sessions 16/17; MAGIC_LO computed from enum, not hardcoded | C# | Feature | S17 | 2026-03-16 |
| TRC-M7 | TRC FW A2 framing — udp_listener.cpp | Complete — A2 frame build/parse/CRC fully implemented | FW | Feature | S22 | 2026-03-16 |
| TRC-M5 | TRC A2 framing — buildTelemetry struct rewrite | Complete | FW | Feature | S22 | 2026-03-16 |
| TRC-M1 | TRC A2 framing — magic/frame validation | Complete | FW | Feature | S22 | 2026-03-16 |
| TRC-M10 | TRC isConnected live flag | Wired in handleA1Frame — was only set in dead receive loop previously | FW | Bug | S15 | 2026-03-16 |

---

*End of closed actions.*
