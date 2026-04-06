# CROSSBOW — TIME_BITS Cross-Controller Alignment

**Date:** 2026-04-05 (Session 36 — updated)

---

## 1. Firmware Register Layout (`*.hpp`)

All four controllers implement identical bit layout. Named differently in firmware comments but functionally identical.

| Bit | Field | MCC `TIME_BITS()` byte 253 | TMC `STATUS_BITS3()` byte 61 | BDC `TIME_BITS()` byte 391 | FMC `TIME_BITS()` byte 44 |
|-----|-------|---------------------------|------------------------------|---------------------------|--------------------------|
| 0 | PTP enabled | `isPTP_Enabled` | `isPTP_Enabled` | `isPTP_Enabled` | `isPTP_Enabled` |
| 1 | PTP synched | `ptp.isSynched` | `ptp.isSynched` | `ptp.isSynched` | `ptp.isSynched` |
| 2 | Using PTP | `activeTimeSource==PTP` | `activeTimeSource==PTP` | `activeTimeSource==PTP` | `activeTimeSource==PTP` |
| 3 | NTP synched | `ntp.isSynched` | `ntp.isSynched` | `ntp.isSynched` | `ntp.isSynched` |
| 4 | NTP on fallback | `ntpUsingFallback` | `ntpUsingFallback` | `ntpUsingFallback` | `ntpUsingFallback` |
| 5 | NTP has fallback | `ntpHasFallback` | `ntpHasFallback` | `ntpHasFallback` | `ntpHasFallback` |
| 6–7 | RES | `0` | `0` | `0` | `0` |

**Status: ✅ Firmware fully consistent across all four controllers.**

---

## 2. C# MSG Parser — Raw Register Property

| | MSG_MCC | MSG_TMC | MSG_BDC | MSG_FMC |
|--|---------|---------|---------|---------|
| Raw byte property | `TimeBits` | `STATUS_BITS3` | `TimeBits` | `TimeBits` |
| Source byte | 253 | 61 | 391 | 44 |
| Naming pattern | `tb_*` prefixed | Flat (no prefix) | `tb_*` prefixed | `tb_*` prefixed |

---

## 3. C# MSG Parser — Bit Accessors

| Bit | Field | MSG_MCC | MSG_TMC | MSG_BDC | MSG_FMC |
|-----|-------|---------|---------|---------|---------|
| 0 | PTP enabled | `tb_isPTP_Enabled` | `isPTP_Enabled` | `tb_isPTP_Enabled` | `tb_isPTP_Enabled` |
| 1 | PTP synched | `tb_isPTP_Synched` | `isPTP_Synched` | `tb_isPTP_Synched` | `tb_isPTP_Synched` |
| 2 | Using PTP | `tb_usingPTP` | `usingPTP` | `tb_usingPTP` | `tb_usingPTP` |
| 3 | NTP synched | `tb_isNTP_Synched` | `isNTPSynched` | `tb_isNTP_Synched` | `tb_isNTP_Synched` |
| 4 | NTP on fallback | `tb_ntpUsingFallback` | `ntpUsingFallback` | `tb_ntpUsingFallback` | `tb_ntpUsingFallback` |
| 5 | NTP has fallback | `tb_ntpHasFallback` | `ntpHasFallback` | `tb_ntpHasFallback` | `tb_ntpHasFallback` |

**Legend:** TMC uses flat property names directly on `STATUS_BITS3` — no `tb_` prefix. Functionally equivalent, naming inconsistency noted as GUI-4.

---

## 4. C# MSG Parser — Derived / Epoch Properties

| Property | MSG_MCC | MSG_TMC | MSG_BDC | MSG_FMC |
|----------|---------|---------|---------|---------|
| `epochTime` type | `DateTime` ✅ | `DateTime` ✅ | `DateTime` ✅ | `DateTime` ✅ |
| `ntpTime` | alias → `epochTime` ✅ | alias → `epochTime` ✅ | alias → `epochTime` ✅ | alias → `epochTime` ✅ |
| `activeTimeSource` type | `TIME_SOURCE` enum ✅ | `TIME_SOURCE` enum ✅ | `string` ⚠️ *(should be TIME_SOURCE enum — see GUI-3)* | `TIME_SOURCE` enum ✅ |
| `activeTimeSourceLabel` | `string` ✅ | `string` ✅ | `string` ✅ *(added session 36)* | `string` ✅ |
| `activeTimeSource` reads from | `TimeBits` (tb_) ✅ | `STATUS_BITS3` ✅ | `DeviceReadyBits` ⚠️ *(should read TimeBits — see GUI-3)* | `TimeBits` (tb_) ✅ |

---

## 5. Issues

### BDC — `activeTimeSource` (⚠️ fix still needed — GUI-3)

Session 36 partial fix:
- ✅ `activeTimeSourceLabel` added to MSG_BDC.cs (session 36)

Still open:
- `activeTimeSource` still returns `string` instead of `TIME_SOURCE` enum — inconsistent with MCC/TMC/FMC
- Still reads from `DeviceReadyBits.usingPTP` and `isNTPReady` — **should read from `tb_usingPTP` / `tb_isNTP_Synched` in `TimeBits`**. This is likely the root cause of the MCC vs BDC time source discrepancy observed in GUI-3 testing.

**Action: Fix MSG_BDC.cs — tracked as GUI-3**

### TMC — naming convention (low priority)

- Uses flat property names on `STATUS_BITS3` (no `tb_` prefix, no `TimeBits` wrapper)
- Functionally correct — cosmetic inconsistency only
- Tracked as GUI-3 (low priority sub-item)

---

## 6. TRC

TRC has no TIME_BITS — PTP integration pending (NEW-38d). Current `MSG_TRC.cs` has:

| Property | MSG_TRC | Notes |
|----------|---------|-------|
| `ntpTime` | `DateTime` | Not aliased to `epochTime` — needs update post NEW-38d |
| `epochTime` | ❌ missing | Add post NEW-38d |
| `activeTimeSource` | ❌ missing | Add post NEW-38d |
| TIME_BITS byte | ❌ not in register | Add post NEW-38d |

---

## 7. Action Items

| ID | Item | Status |
|----|------|--------|
| **GUI-3** | MSG_BDC.cs — fix `activeTimeSource` to return `TIME_SOURCE` enum; redirect to read from `TimeBits` (`tb_usingPTP` / `tb_isNTP_Synched`) not `DeviceReadyBits`. Likely root cause of MCC vs BDC time source discrepancy. | ⚠️ Open |
| **GUI-3** | MSG_BDC.cs — `activeTimeSourceLabel` added session 36 | ✅ Closed |
| **GUI-3** | MSG_TMC.cs — align naming to `tb_*` pattern (low priority, cosmetic) | ⚠️ Open (low) |
| **NEW-38d** | TRC PTP integration — adds TIME_BITS to TRC REG1; MSG_TRC.cs updated post integration | ⚠️ Open |
