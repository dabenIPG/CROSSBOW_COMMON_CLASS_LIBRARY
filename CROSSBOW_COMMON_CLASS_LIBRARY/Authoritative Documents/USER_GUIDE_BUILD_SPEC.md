# CROSSBOW — User Guide Word Document Build Specification

**Document ID:** TBD
**Version:** 1.0.0
**Status:** DRAFT
**Last updated:** Session 21 — 2026-03-16
**Author:** IPG Engineering

---

## Revision History

| Version | Session | Date | Change |
|---------|---------|------|--------|
| 1.0.0 | Session 21 | 2026-03-16 | Initial draft |

---

## 1. Purpose

This specification defines the layout, typography, colour palette, section structure, and generator script approach for producing the four CROSSBOW user guide Word documents (`.docx`) from their corresponding Markdown source files.

It is the companion document to `CROSSBOW_ICD_BUILD_SPEC.md`, which governs the ICD generator pipeline.

---

## 2. Output Files

| Guide | Source file | Output file | Version | Doc # |
|-------|------------|-------------|---------|-------|
| THEIA User Guide | `THEIA_USER_GUIDE.md` | `CROSSBOW_UG_THEIA_v1.0.0.docx` | 1.0.0 | TBD |
| Engineering GUI User Guide | `ENG_GUI_USER_GUIDE.md` | `CROSSBOW_UG_ENG_GUI_v1.0.0.docx` | 1.0.0 | TBD |
| Hyperion User Guide | `HYPERION_USER_GUIDE.md` | `CROSSBOW_UG_HYPERION_v1.0.0.docx` | 1.0.0 | TBD |
| Emplacement GUI User Guide | `EMPLACEMENT_GUI_USER_GUIDE.md` | `CROSSBOW_UG_EMPLACEMENT_v1.0.0.docx` | 1.0.0 | TBD |

Output filename convention: `CROSSBOW_UG_<SHORTNAME>_v<MAJOR>.<MINOR>.<PATCH>.docx`

Version increments follow the same policy as the ICD documents:
- **PATCH** — content corrections, typo fixes
- **MINOR** — new sections, significant content additions
- **MAJOR** — structural overhaul or scope change

---

## 3. Generator Script Approach

### 3.1 Architecture

A **single parameterised generator** is used: `gen_user_guides.js`. It accepts a guide identifier as a command-line argument and produces the corresponding `.docx` output. This avoids duplicating document-layout code across four scripts while still allowing per-guide customisation (title, doc number, classification, section structure).

```
node gen_user_guides.js --guide theia
node gen_user_guides.js --guide eng_gui
node gen_user_guides.js --guide hyperion
node gen_user_guides.js --guide emplacement
node gen_user_guides.js --all           # builds all four
```

### 3.2 Dependencies

| Package | Purpose |
|---------|---------|
| `docx` | Word document generation |
| `fs` / `path` | File I/O |
| `marked` (optional) | Markdown parsing for body content import |

Same dependency set as the ICD generators. No additional packages required.

### 3.3 Script File

| File | Description |
|------|-------------|
| `gen_user_guides.js` | Single generator — all four guides |

### 3.4 Per-Guide Config Object

Each guide is defined by a config block at the top of the script:

```js
const GUIDES = {
  theia: {
    title:          "THEIA User Guide",
    shortName:      "THEIA",
    docNumber:      "TBD",
    version:        "1.0.0",
    classification: "CONTROLLED — AUTHORISED PERSONNEL ONLY",  // TBD
    outputFile:     "CROSSBOW_UG_THEIA_v1.0.0.docx",
    sections:       SECTIONS_THEIA,   // see §7
  },
  eng_gui: { ... },
  hyperion: { ... },
  emplacement: { ... },
};
```

---

## 4. Page Layout

| Parameter | Value |
|-----------|-------|
| Paper size | A4 (210 × 297 mm) |
| Orientation | Portrait |
| Top margin | 25.4 mm (1 in) |
| Bottom margin | 25.4 mm (1 in) |
| Left margin | 25.4 mm (1 in) |
| Right margin | 25.4 mm (1 in) |
| Header | Document title (left), version (right) |
| Footer | Document number (left), page number (right) |
| Page numbers | Arabic numerals, starting at 1 on first body page |

---

## 5. Typography

| Element | Font | Size | Style |
|---------|------|------|-------|
| Document title (cover) | TBD | 24 pt | Bold |
| Subtitle / version (cover) | TBD | 12 pt | Regular |
| Heading 1 | TBD | 16 pt | Bold |
| Heading 2 | TBD | 13 pt | Bold |
| Heading 3 | TBD | 11 pt | Bold Italic |
| Body text | TBD | 10 pt | Regular |
| Table header | TBD | 9 pt | Bold |
| Table body | TBD | 9 pt | Regular |
| Code / monospace | Courier New | 9 pt | Regular |
| Caution / note callout | TBD | 10 pt | Italic |

> **TBD:** Font family to be confirmed when `gen_eng_icd.js` is available — user guides will use the same base typeface as the ICD documents for brand consistency.

---

## 6. Colour Palette

> **TBD:** Colour values to be pulled from `gen_eng_icd.js` once uploaded. The user guides will share the same palette as the ICD documents. Provisional roles are listed below pending confirmation.

| Role | Hex | Usage |
|------|-----|-------|
| Primary accent | TBD | Cover banner, Heading 1 underline, table header fill |
| Secondary accent | TBD | Heading 2 rule, callout border |
| Table header text | TBD | |
| Table row alt fill | TBD | Alternating row shading |
| Cover background | TBD | Cover page banner |
| Body text | #000000 | Default |
| Code background | #F5F5F5 | Inline code / code block shading |

---

## 7. Cover Page

The cover page is auto-generated. It contains:

1. **IPG logo** — top-left (TBD: logo asset path / base64 embed)
2. **Document title** — e.g. `THEIA User Guide`
3. **Subtitle** — `CROSSBOW System`
4. **Version** — `Version 1.0.0`
5. **Document number** — e.g. `TBD`
6. **Classification banner** — bottom of cover (TBD: confirm classification label per guide)
7. **Date** — ISO 8601, auto-inserted at build time

---

## 8. Common Section Structure

All four user guides share this skeleton. Per-guide deviations are listed in §9.

| # | Section title | Notes |
|---|---------------|-------|
| 1 | Introduction | Purpose, scope, intended audience, related documents |
| 2 | System Overview | Role of the application within CROSSBOW |
| 3 | Installation & Setup | Prerequisites, install steps, network config |
| 4 | Interface Overview | Annotated screenshot / layout description |
| 5 | Operating Procedures | Step-by-step task walkthroughs |
| 6 | Configuration Reference | All configurable parameters, defaults, valid ranges |
| 7 | Troubleshooting | Common faults, symptoms, remediation |
| 8 | Revision History | Version table |

Section numbers are fixed. If a section does not apply to a guide it is retained with a "Not applicable for this configuration" placeholder rather than omitted, to preserve consistent section numbering across all four documents.

---

## 9. Per-Guide Section Deviations

### 9.1 THEIA User Guide

| Section | Deviation |
|---------|-----------|
| §3 Installation | GStreamer pipeline setup; cross-reference `GSTREAMER_INSTALL.md` |
| §4 Interface Overview | Video window, PixelShift correction note (−20 px horizontal) |
| §5 Operating Procedures | CUE inbound workflow; status response interpretation |

### 9.2 Engineering GUI User Guide

| Section | Deviation |
|---------|-----------|
| §3 Installation | A1/A2 interface setup; magic byte `0xCB 0x49` note |
| §5 Operating Procedures | Engineering-only commands; INT_ENG scope |
| §6 Configuration Reference | Port assignments, controller enumeration |

### 9.3 Hyperion User Guide

| Section | Deviation |
|---------|-----------|
| §5 Operating Procedures | TBD — content review pending |

### 9.4 Emplacement GUI User Guide

| Section | Deviation |
|---------|-----------|
| §3 Installation | Location/operator validation setup |
| §5 Operating Procedures | KIZ/LCH upload workflow; `SET_LCH_VOTE` usage |

---

## 10. Tables

Table style mirrors the ICD documents:

- Header row: filled with primary accent colour, white bold text
- Body rows: alternating white / light grey fill (TBD: match ICD alt-row hex)
- Border: 0.5 pt, dark grey
- All tables include a caption above in the form: `Table N — Description`
- Column widths: set explicitly; do not use auto-fit

---

## 11. Images and Screenshots

- All images embedded as base64 in the generator script (no external file dependencies at build time)
- Target width: full text column width (approx. 160 mm on A4 with 25.4 mm margins)
- All images include a caption below in the form: `Figure N — Description`
- TBD: confirm whether screenshots are included in v1.0.0 output or deferred

---

## 12. Callouts and Notes

Two callout styles:

| Type | Trigger keyword | Border colour | Usage |
|------|----------------|---------------|-------|
| NOTE | `> **NOTE:**` in source | Secondary accent (TBD) | Informational aside |
| CAUTION | `> **CAUTION:**` in source | Amber (TBD) | Potential data loss or misconfiguration |

Callouts are rendered as single-cell borderless tables with a left colour bar (4 pt) and shaded background.

---

## 13. Classification and Distribution

| Guide | Audience | Classification label |
|-------|----------|---------------------|
| THEIA User Guide | TBD | TBD |
| Engineering GUI User Guide | IPG engineering only | TBD |
| Hyperion User Guide | TBD | TBD |
| Emplacement GUI User Guide | TBD | TBD |

Classification label appears in the footer on every page and on the cover page banner.

> **TBD:** Confirm classification labels for each guide before v1.0.0 build.

---

## 14. Build Process

```
# Single guide
node gen_user_guides.js --guide theia

# All four guides
node gen_user_guides.js --all

# With explicit output directory
node gen_user_guides.js --all --outdir ./dist
```

Output files are written to `./dist/` by default (created if absent).

Build prerequisites:
- Node.js ≥ 18
- `npm install` in generator directory (same `package.json` as ICD generators)

---

## 15. Open Items

| ID | Item | Priority | Notes |
|----|------|----------|-------|
| S19-36-1 | Confirm font family (from ICD generators) | High | Pending `gen_eng_icd.js` upload |
| S19-36-2 | Confirm colour palette hex values | High | Pending `gen_eng_icd.js` upload |
| S19-36-3 | Confirm classification label per guide | High | Before v1.0.0 build |
| S19-36-4 | Confirm doc numbers for all four guides | Medium | TBD assigned |
| S19-36-5 | Logo asset — confirm embed approach | Medium | Base64 vs. file path |
| S19-36-6 | Screenshots — include in v1.0.0 or defer? | Medium | |
| S19-36-7 | Hyperion §5 content review | Low | Guide content stable? |

---

## 16. Related Documents

| Document | Version | Notes |
|----------|---------|-------|
| `CROSSBOW_ICD_BUILD_SPEC.md` | 1.7.1 (stale — S20-38) | ICD generator spec — palette/font reference |
| `THEIA_USER_GUIDE.md` | 1.0.0 | Source |
| `ENG_GUI_USER_GUIDE.md` | 1.0.0 | Source |
| `HYPERION_USER_GUIDE.md` | 1.0.0 | Source |
| `EMPLACEMENT_GUI_USER_GUIDE.md` | 1.0.0 | Source |
| `GSTREAMER_INSTALL.md` | 3.0.0 | Cross-referenced in THEIA §3 |
