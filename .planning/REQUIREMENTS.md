# Requirements: DYNESTIC NC-Hops Post-Processor

**Defined:** 2026-03-22
**Core Value:** One GH definition takes geometry to a ready-to-run .hopx file for the DYNESTIC — no CAM middleman.

---

## v1 Requirements

### Format (FORMAT)

- [x] **FORMAT-01**: Decode NC-Hops .hop macro syntax from a sample file (all 4 operation types) — **DONE** via `Muster_DXF_Import.hop`
- [x] **FORMAT-02**: Identify the correct macro name and parameter signature for 2D contour cutting — **DONE**: `SP` + `G01`/`G03M` + `EP`
- [x] **FORMAT-03**: Identify the correct macro name and parameter signature for pocketing — **DONE**: `CALL _RechteckTasche_V5`, `CALL _Kreistasche_V5`, `CALL _Kreisbahn_V5`, `CALL _nuten_frei_v5`
- [x] **FORMAT-04**: Identify the correct macro name and parameter signature for drilling — **DONE**: `Bohrung(x,y,z,tiefe,d,0,0,0,0,0,0,0)` (12 params) + `HorzB(x,y,z,d,tiefe,0,0,winkel,0,fläche,0,0)`
- [ ] **FORMAT-05**: Identify the correct macro name and parameter signature for 3D milling (likely via `CALL DIN ISO` ISO block) — pending (3DMilling not licensed on dongle anyway)
- [x] **FORMAT-06**: Document .hop file structure: header/program start, operation blocks — **DONE** — see research/HOP_FORMAT_DECODED.md

### Core Engine (CORE)

- [x] **CORE-01**: GHPython component generates syntactically valid .hopx file from operation inputs
- [x] **CORE-02**: File export triggered by boolean toggle component (not continuous)
- [x] **CORE-03**: Output file path configurable via GH string parameter
- [x] **CORE-04**: Program header/footer correctly wraps all operations
- [x] **CORE-05**: Component validates input and shows error messages in GH for missing required parameters

### Operations (OPS)

- [x] **OPS-01**: 2D contour cutting — generates correct macro from closed/open curves in XY plane
- [x] **OPS-02**: Pocketing — generates pocket macro from closed curve boundary
- [x] **OPS-03**: Drilling — generates BOHRUNG-style macro from list of points + depth value
- [ ] **OPS-04**: 3D milling — generates correct ISO G-code block (via CALL DIN ISO) from toolpath plane sequence
- [x] **OPS-05**: Operations can be combined in one export (multiple operation types in one .hopx file)
- [x] **OPS-06**: Operation order is user-controlled (list ordering in GH)

### Toolpath Generation (PATH)

- [x] **PATH-01**: 2D curves (on XY plane) usable directly as cutting/pocket boundaries
- [x] **PATH-02**: Drilling points extractable from any point geometry input
- [ ] **PATH-03**: 3D toolpath planes generatable from surface geometry using Contour + Perp Frames + Align Plane chain
- [ ] **PATH-04**: Align Plane step prevents 180° flips in plane sequences (critical pitfall from research)
- [x] **PATH-05**: Curves automatically converted to polyline approximation (NURBS tolerance configurable)

### Preview (PREVIEW)

- [ ] **PREVIEW-01**: Toolpath curves visualized in GH viewport before export (colored by operation type)
- [ ] **PREVIEW-02**: Drill positions shown as point/circle markers in viewport
- [ ] **PREVIEW-03**: 3D toolpath planes displayed as small axis glyphs along path
- [ ] **PREVIEW-04**: Rapid moves (safe Z traversals) shown in distinct color from cutting moves

### Parameters (PARAM)

- [x] **PARAM-01**: Tool number, diameter, feed rate, spindle speed configurable per operation
- [x] **PARAM-02**: Safe Z (retract height) configurable globally
- [x] **PARAM-03**: Material thickness configurable globally (used as default cut-through depth)
- [x] **PARAM-04**: Cut depth per pass configurable per operation (for multi-pass cutting)
- [x] **PARAM-05**: Stepover percentage configurable for pocketing operations
- [x] **PARAM-06**: HopSheet component — closed curve input → extracts dx/dy from BoundingBox; replaces three dx/dy/dz sliders in HopExport

### Nesting Integration (NEST) — Phase 7

- [x] **NEST-01**: OpenNest integration — nest parts before generating toolpaths
- [x] **NEST-02**: Multi-sheet output (separate .hop file per sheet, auto-numbered)
- [x] **NEST-03**: Sheet origin/offset applied to all operation components after nesting

### Plugin Packaging (PLUGIN) — Phase 8

### Brep Feature Recognition (ANALYZER) — Phase 8.5

- [ ] **ANALYZER-01**: HopAnalyzer component reads solid Brep → classifies faces (pocket floor, cut-through, drill hole, outer contour)
- [ ] **ANALYZER-02**: Detected features mapped to correct operation macros (HopContour / HopRectPocket / HopDrill) automatically
- [ ] **ANALYZER-03**: HopAnalyzer output is operationLines list — wires directly into HopExport without manual op-component setup

- [x] **PLUGIN-01**: All components compiled as .gha Grasshopper plugin (no script editor required)
- [x] **PLUGIN-02**: Each component has icon (24×24 px), tooltip, and GH category "DYNESTIC"
- [ ] **PLUGIN-03**: Installable via yak package manager or manual .gha drop into GH Libraries folder

---

## v2.0 Requirements — Korpus-Generator

### Parametric Model (KORPUS)

- [x] **KORPUS-01**: Parametric box/carcass from B × H × T × material thickness — 5 flat panels (open front)
- [ ] **KORPUS-02**: Joint generation — Schlitz/Zapfen, Überblattung, Dübelbohrungen; selectable per edge
- [ ] **KORPUS-03**: Part extraction — each panel as named flat outline curve (label: "Boden", "Rückwand", etc.)
- [ ] **KORPUS-04**: Parts feed directly into v1.0 nesting + toolpath pipeline (Phase 7 OpenNest wiring)
- [ ] **KORPUS-05**: Technical drawings — front/side/top views, horizontal + vertical sections, auto-dimensioning
- [ ] **KORPUS-06**: BOM output — parts list (name, qty, dimensions) as GH panel data and embedded in drawing; DXF/PDF export

### Arc Compression

- **ARC-01**: Straight-line polylines compressed to arc commands (G2/G3 or NC-Hops equivalent)
- **ARC-02**: Configurable arc tolerance threshold

### Advanced Operations

- **ADV-01**: Dogbone notch generation at inside corners for 2D profiles
- **ADV-02**: Trochoidal/HSM pocketing patterns
- **ADV-03**: Tool change sequences between operations

---

## Out of Scope

| Feature | Reason |
|---------|--------|
| RhinoCAM integration | Explicit user preference to avoid it |
| Machine simulation / collision detection | High complexity, separate concern |
| Metal cutting support | DYNESTIC is a wood/panel machine |
| Cloud or web interface | Local GH plugin only |
| Support for other machine brands | Single-machine focus for v1 |
| .hopx (NC-Hops V7 format) | Sample file uses `.hop` extension (HOPS 5.x format), machine CAMPUS shows `.hop` files — `.hopx` may not be needed. Verify with HOPS 7.7 sample from machine. |

---

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| FORMAT-01–06 | Phase 1 | Pending |
| CORE-01–05 | Phase 2 | **DONE** |
| OPS-01–03, OPS-05–06 | Phase 3 | Pending |
| PATH-01–02, PATH-05 | Phase 3 | Pending |
| OPS-04 | Phase 4 | Pending |
| PATH-03–04 | Phase 4 | Pending |
| PREVIEW-01–04 | Phase 5 | Pending |
| PARAM-01–05 | Phase 6 | Pending |

**Coverage:**
- v1 requirements: 29 total
- Mapped to phases: 29
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-22*
*Last updated: 2026-03-24 — CORE-01 through CORE-05 satisfied via Phase 2 (HopExport.cs verified in GH)*
