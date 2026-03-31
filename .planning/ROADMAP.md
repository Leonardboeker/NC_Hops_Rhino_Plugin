# Roadmap: DYNESTIC NC-Hops Post-Processor

**Created:** 2026-03-22
**Last updated:** 2026-03-31 — Phase 7 planned (3 plans, 07-01 through 07-03)

---

## Milestone 1: v1.0 — GH Post-Processor

**Goal:** A complete, usable Grasshopper plugin that takes geometry to a ready-to-run .hop file — visual, parametric, nestable, packaged.

### Phase 1 — Format Discovery & Environment Setup ✓ DONE
**Exit criteria met:** `Muster_DXF_Import.hop` analyzed, full macro reference in `research/HOP_FORMAT_DECODED.md`

| Plan | Status |
|------|--------|
| 1.1 — Obtain and analyze sample .hop file | ✓ DONE |
| 1.3 — Document complete NC-Hops macro reference | ✓ DONE |

---

### Phase 2 — Core .hop Engine ✓ DONE
**Exit criteria met:** HopExport.cs compiles in Rhino 8 GH, generates .hop passing 19/19 structural checks.

| Plan | Status |
|------|--------|
| 2.1 — HopExport.cs + structural validation | ✓ DONE |
| 2.2 — Verify output in Grasshopper (human checkpoint) | ✓ DONE |

---

### Phase 3 — 2D Operations: Cutting, Pocketing, Drilling ✓ DONE
**Exit criteria met:** 6 operation components (HopContour, HopRectPocket, HopCircPocket, HopCircPath, HopFreeSlot, HopDrill). Integration test 34/34 passing. Human UAT passed 2026-03-30.

| Plan | Status |
|------|--------|
| 3.1 — HopContour (NURBS → contour blocks) | ✓ DONE |
| 3.2 — Pocket components (4 types) | ✓ DONE |
| 3.3 — HopDrill (points → Bohrung macros) | ✓ DONE |
| 3.4 — Integration verification | ✓ DONE |

---

### Phase 4 — 3D Milling
**Goal:** 3-axis surface milling via ISO G-code wrapped in NC-Hops CALL DIN ISO block.
**Requirements:** FORMAT-05, OPS-04, PATH-03, PATH-04

| Plan | Task |
|------|------|
| 4.1 | Surface contour toolpath — Contour + Divide Curve + Perp Frames + Align Plane chain |
| 4.2 | Plane list → ISO G-code lines (X Y Z I J K format) |
| 4.3 | Wrap ISO block in CALL DIN ISO .hop structure; test in CAMPUS |
| 4.4 | Depth-of-cut pass management for rough/finish passes |

**Exit criteria:** Curved surface toolpath generates a valid CALL DIN ISO block that CAMPUS accepts.

---

### Phase 5 — Visual Toolpath Preview ✓ DONE
**Exit criteria met:** DrawViewportWires preview working in all 6 operation components. Human UAT passed 2026-03-30.

Plans:
- [x] 05-PLAN.md — Add DrawViewportWires preview to all 6 operation components (colour input, default colors, dashed approach lines, ClippingBox)

Note: PREVIEW-03 (3D plane glyphs) depends on Phase 4 HopIso component — deferred to Phase 4/5 integration.

---

### Phase 6 — UX Polish & HopSheet ✓ DONE
**Exit criteria met:** HopSheet extracts dx/dy from geometry bbox; all 6 op-components have correct Z-reference; HopContour has kerf compensation; HopToolDB removed (tool params machine-level constants). Canvas cleanup deferred to Phase 8.
**Requirements:** PARAM-01–06

| Plan | Status |
|------|--------|
| 6.1 — HopSheet component | ✓ DONE |
| 6.2 — Tool parameter panel (HopToolDB) | ✓ DONE (removed after testing — machine-level constants) |
| 6.3–6.4 — Global/per-operation params | ✓ DONE (Z-reference + toolDiameter on all components) |
| 6.5 — GH definition cleanup | DEFERRED → Phase 8 (plugin packaging is the right venue) |
| 6.6 — Usage documentation | DEFERRED → Phase 8 |

---

### Phase 7 — OpenNest Integration
**Goal:** Parts can be nested on a sheet inside GH before toolpath generation. Multi-sheet support. Sheet offset applied to all toolpaths.
**Requirements:** NEST-01, NEST-02, NEST-03
**Plans:** 3 plans

Plans:
- [x] 07-01-PLAN.md — HopPart component (bundles part outline + operationLines into Dictionary, grain arrow + sheet-colored preview)
- [ ] 07-02-PLAN.md — HopSheetExport component (per-sheet .hop file export from HopPart dictionaries, mirrors HopExport structure)
- [ ] 07-03-PLAN.md — Integration verification checkpoint (human verifies full nesting workflow end-to-end)

**Exit criteria:** Multiple parts fed into OpenNest, nested on one or more sheets, each sheet exports a valid .hop file with correct XY offsets.

---

### Phase 8 — Plugin Packaging
**Goal:** All components compiled as a proper `.gha` Grasshopper plugin — installable, with icons, tooltips, GH category, no script editor needed.
**Requirements:** PLUGIN-01, PLUGIN-02, PLUGIN-03

| Plan | Task |
|------|------|
| 8.1 | Visual Studio project setup — .gha target, GH SDK references, component base classes |
| 8.2 | Port all C# Script components to compiled GhComponent classes |
| 8.3 | Icons per component (24×24 px) + tooltips + GH category "DYNESTIC" |
| 8.4 | Build + install script; test in clean Rhino install |

**Exit criteria:** Plugin installs via yak or manual .gha drop; all components appear in GH component panel under "DYNESTIC"; no script editor required.

---

---

### Phase 8.5 — HopAnalyzer: Brep Feature Recognition
**Goal:** A component that reads a solid Brep (as modeled in Rhino) and automatically detects machinable features — pockets, through-cuts, drill holes — generating the correct operation components without manual wiring.
**Milestone bridge:** v1.0 → v2.0 precursor. Enables the Korpus-Generator (Phase 9) to auto-produce .hop files from modeled parts without any manual operation selection.
**Requirements:** ANALYZER-01, ANALYZER-02, ANALYZER-03

| Plan | Task |
|------|------|
| 8.5.1 | Face classification — detect XY-parallel faces at varying Z heights (pocket floors, cut-through bottoms) |
| 8.5.2 | Cylinder detection — cylindrical faces → drill positions, diameters, depths |
| 8.5.3 | Outer contour extraction — bounding profile curve at top face → HopContour candidate |
| 8.5.4 | Operation output — HopAnalyzer emits pre-wired operationLines directly into HopExport |

**Exit criteria:** A simple routed part (pocket + profile + holes) modeled as a Brep in Rhino → HopAnalyzer → .hop without any manual operation component wiring.

---

## Phase Dependencies (v1.0 + bridge)

```
Phase 1 ──► Phase 2 ──► Phase 3 ──► Phase 5 ──► Phase 6 ──► Phase 7 ──► Phase 8 ──► Phase 8.5
                     └──► Phase 4 ──► Phase 5                                              │
                                                                                           ▼
                                                                                   v2.0 Phase 9+
```

---

## Milestone 2: v2.0 — Korpus-Generator

**Goal:** Parametric cabinet/panel body generator. Takes dimensions and material → outputs flat parts (feeds into v1.0 post-processor for nesting+cutting) AND generates technical drawings (views, sections, dimensions, BOM).

**Core value:** Design a cabinet in GH → get cutting files AND workshop drawings in one step.

---

### Phase 9 — Parametric Korpus Model
**Goal:** Parametric box/carcass from dimensions (B × H × T × material thickness). Joint types selectable. All panels as flat Brep geometry.
**Requirements:** KORPUS-01, KORPUS-02

| Plan | Task |
|------|------|
| 9.1 | Basic box generator — 6 panels (top/bottom/left/right/back/front) from B×H×T×thickness |
| 9.2 | Joint generation — Schlitz/Zapfen, Überblattung, Dübelbohrungen; selectable per edge |
| 9.3 | Opening cutouts — door/drawer openings as subtracted geometry |
| 9.4 | Internal shelves and dividers — parametric grid |

**Exit criteria:** Any rectangle dimension input produces a fully jointed carcass as flat panels ready for nesting.

---

### Phase 10 — Part Export & Post-Processor Integration
**Goal:** Flat panels from Korpus-Generator feed directly into v1.0 nesting + toolpath pipeline.
**Requirements:** KORPUS-03, KORPUS-04

| Plan | Task |
|------|------|
| 10.1 | Part extraction — each panel as named flat curve (with label: "Boden", "Rückwand", etc.) |
| 10.2 | OpenNest wiring — parts auto-fed into Phase 7 nesting pipeline |
| 10.3 | Material/grain-direction constraint for nesting (panels rotated only 0°/180° if needed) |
| 10.4 | BOM output — parts list as GH panel data (name, qty, dimensions) |

**Exit criteria:** Korpus model → nesting → .hop file(s) in one GH definition without manual steps.

---

### Phase 11 — Technical Drawing Output
**Goal:** Auto-generated workshop drawings — plan views, sections, dimensions, BOM table. Output as DXF and/or PDF.
**Requirements:** KORPUS-05, KORPUS-06

| Plan | Task |
|------|------|
| 11.1 | Standard views — front/side/top projections of assembled korpus |
| 11.2 | Section cuts — horizontal + vertical sections showing joints and dimensions |
| 11.3 | Auto-dimensioning — overall dimensions + material thickness annotations |
| 11.4 | BOM table embedded in drawing; DXF export via Rhino Make2D pipeline |

**Exit criteria:** Single GH definition produces workshop-ready DXF drawings that could be handed to a fabricator without explanation.

---

## Phase Dependencies (full)

```
v1.0:  Phase 1 ──► Phase 2 ──► Phase 3 ──► Phase 5 ──► Phase 6 ──► Phase 7 ──► Phase 8
                            └──► Phase 4 ──► Phase 5

v2.0:  Phase 9 (Korpus) ──► Phase 10 (Parts+Nesting) ──► Phase 11 (Drawings)
                                    │
                                    └──► v1.0 Phase 7 (OpenNest) + Phase 8 (Plugin)
```

---

## Key Risks

| Risk | Mitigation |
|------|------------|
| FORMAT-05: 3D milling ISO block structure unknown | CCSOFTCZ post-processor trial can generate reference files |
| OpenNest API changes | Pin to specific OpenNest version; document wiring |
| .gha compilation complexity | Use GH SDK NuGet; reference Rhino 8 targets |
| Make2D dimensioning quality | Manual DXF annotation fallback if auto-dim is unreliable |
| Korpus joint geometry complexity | Start with butt joints; add lap/slot joints incrementally |

---

## Requirements Index

| Requirement | Phase | Milestone |
|-------------|-------|-----------|
| FORMAT-01–04, FORMAT-06 | Phase 1 | v1.0 ✓ |
| FORMAT-05 | Phase 4 | v1.0 |
| CORE-01–05 | Phase 2 | v1.0 ✓ |
| OPS-01–03, OPS-05–06, PATH-01–02, PATH-05 | Phase 3 | v1.0 ✓ |
| OPS-04, PATH-03–04 | Phase 4 | v1.0 |
| PREVIEW-01–04 | Phase 5 | v1.0 |
| PARAM-01–06 | Phase 6 | v1.0 |
| NEST-01–03 | Phase 7 | v1.0 |
| PLUGIN-01–03 | Phase 8 | v1.0 |
| KORPUS-01–02 | Phase 9 | v2.0 |
| KORPUS-03–04 | Phase 10 | v2.0 |
| KORPUS-05–06 | Phase 11 | v2.0 |

---

*Roadmap created: 2026-03-22*
*Updated: 2026-03-30 — Phase 3 closed; Phase 5 planned (1 plan, 05-01-PLAN.md); Phase 6 extended with HopSheet; Phase 7 (OpenNest) + Phase 8 (Plugin) added to v1.0; Milestone v2.0 (Korpus-Generator, Phases 9–11) added*
*Updated: 2026-03-31 — Phase 6 closed DONE; canvas cleanup deferred to Phase 8*
*Updated: 2026-03-31 — Phase 7 planned: 3 plans (07-01 HopPart, 07-02 HopSheetExport, 07-03 Integration checkpoint)*
