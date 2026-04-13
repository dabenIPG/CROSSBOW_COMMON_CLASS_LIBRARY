# CROSSBOW — Open Action Items

**Document:** `PTP_OPEN_ACTIONS.md`
**Version:** 1.1.0
**Date:** 2026-04-05
**Project:** CROSSBOW

---

| ID | Title | Detail | Component | Type | Session | Date Opened |
|----|-------|--------|-----------|------|---------|-------------|
| GUI-1 | MCC + BDC ENG GUI timeout — not receiving on A2 | THEIA (A3) works fine. A2 path issue — suspect registration burst not landing, ParseA2 entry point, or HB_RX_ms staleness logic. Debug: add print at top of RX loop, confirm Debug.WriteLine for burst, confirm ParseA2 not ParseA3 is being called. | C# | Bug | S36 | 2026-04-05 |
| GUI-2 | HMI robust testing | Full engagement sequence, mode transitions, fire control chain — requires live HW test | HW | Test | S36 | 2026-04-05 |
| GUI-3 | MCC vs BDC time source label discrepancy | Both show activeTimeSourceLabel but report different fallover behaviour — holdover latch state or TIMESRC default diverging between the two controllers | C# / FW | Investigation | S36 | 2026-04-05 |
| GUI-4 | Fuji AWB — merge external action lists | Fuji lens AWB control tracked in separate external list. Locate, merge into this register, implement in frmBDC.cs / THEIA command path | C# | Feature | S36 | 2026-04-05 |
| GUI-5 | lbl_gimbal_hb in frmBDC — gimbalMSG.HB_TX_ms missing | gimbalMSG.HB_TX_ms property does not exist on MSG_GIMBAL. Investigate MSG_GIMBAL class to find correct HB property name | C# | Bug | S36 | 2026-04-05 |
| GUI-6 | Rolling max stats to TRC tab | Extend GUI-5 dt/HB rolling max stats to the TRC controller tab in ENG GUI | C# | Feature | S35 | 2026-04-05 |
| FW-B2 | MCC RX-side SEQ gap counter for TMC A1 stream | Track per-slot SEQ discontinuities on the MCC receive side for the TMC A1 stream | FW | Feature | S35 | 2026-04-05 |
| FW-B3 | PTP DELAY_REQ W5500 contention | Both BDC and FMC running PTP simultaneously causes W5500 DELAY_REQ stall (~40ms ARP block per attempt). isPTP_Enabled=false workaround on all controllers. Investigate suppressDelayReq per-controller or staggered FOLLOW_UP timing (FMC +50ms offset). See PTP_TIMING_CONTEXT.md. | FW | Bug | S33 | 2026-03-28 |
| SAMD-NTP | FMC SAMD21 NTP timing | FMC TIME command caused USB CDC lockup when ptp.PrintTime()/ntp.PrintTime() called — USB CDC and Ethernet share power path on SAMD21 hardware. Slim TIME command workaround implemented. isNTP_Enabled=false default. Root cause TBD. See PTP_TIMING_CONTEXT.md. | FW | Bug | S36 | 2026-04-05 |
| NEW-33 | MCC REG1 VOTE_BITS byte[3] bit 0 wrong field | Currently packs isLaserTotalHW_Vote_rb — should be isNotBatLowVoltage(). FW change in mcc.cpp buildReg01(). Hold pending HW test. | FW | Bug | S16 | 2026-03-16 |
| NEW-39 | LCH/KIZ operator validity hardcoded true | operatorValid arg to SET_LCH_VOTE() in frmMain.cs currently hardcoded true (not yet implemented). Implement proper operator validation in LCH class and wire to btn_KIZ_Upload_Click and btn_LCH_Upload_Click. | C# | Feature | S36 | 2026-04-05 |
| FW-14 | GNSS socket bug in MCC RUNONCE | RUNONCE case 6 and EXEC_UDP use wrong socket. Fix when on HW. | FW | Bug | S16 | 2026-03-16 |
| TRC-M9 | Deprecate TRC port 5010 | Legacy 64B binary port. Remove after HW validation confirms port 10018 fully operational. | FW / C# | Deferred | S15 | 2026-03-16 |
| PENDING | Merge external action list | Fuji + other items tracked outside these docs — bring to next session for full merge | All | Admin | S36 | 2026-04-05 |
| PENDING | Mutex on buildTelemetry() race condition | Low priority concurrency fix in TRC | FW | Bug | S18 | 2026-03-16 |
| PENDING | Video multicast 0xD1 ORIN_SET_STREAM_MULTICAST | TRC multicast streaming — wired in ICD, not yet deployed | FW / C# | Feature | S22 | 2026-03-16 |
| PENDING | 30 fps option 0xD2 / ASCII FRAMERATE 30 | TRC framerate control — wired in ICD, not yet deployed | FW / C# | Feature | S22 | 2026-03-16 |

---

*End of open actions.*
