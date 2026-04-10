---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: — GH Post-Processor
status: executing
stopped_at: Completed 87.2-01-PLAN.md (HopLayerScanComponent)
last_updated: "2026-04-10T00:52:44.050Z"
progress:
  total_phases: 10
  completed_phases: 8
  total_plans: 20
  completed_plans: 21
---

# Project State

**Last updated:** 2026-03-31
**Status:** Executing Phase 87.2

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-22)

**Core value:** One GH definition takes geometry to a ready-to-run .hop file for the DYNESTIC -- no CAM middleman.
**Current focus:** Phase 87.2 — layer-scan

## Milestone v1.0 — COMPLETE ✓

**Phase 3: COMPLETE ✓** — Human UAT passed 2026-03-30. All 3 tests confirmed.

**Phase 5 (Visual Preview): COMPLETE ✓** — Human UAT passed 2026-03-30. All 7 tasks confirmed. DrawViewportWires preview working in all 6 operation components.

**Phase 6 (UX Polish + HopSheet): COMPLETE ✓** — 2026-03-31. HopSheet live; Z-reference fixed in all 6 components; HopContour kerf compensation added; HopToolDB removed (machine-level constants).

**Phase 7 (OpenNest Integration): COMPLETE ✓** — 2026-03-31. HopPart, HopSheet, HopSheetExport working; user confirmed "ok super".

**Phase 8 (Plugin Packaging): COMPLETE ✓** — 2026-03-31. .gha auto-installs, icons embedded, AutoWire on all 10 components, Yak .yak built. User confirmed "ok top now looks good".

Roadmap updated 2026-03-30:

- Phase 6 extended: HopSheet component added (PARAM-06)
- Phase 7 added: OpenNest Integration (NEST-01–03)
- Phase 8 added: Plugin Packaging (PLUGIN-01–03)
- Milestone v2.0 added: Korpus-Generator (Phases 9–11, KORPUS-01–06)

## Progress

| Phase | Status | Plans | Progress |
|-------|--------|-------|----------|
| 1 -- Format Discovery | DONE | 2/2 | 100% |
| 2 -- Core Engine | DONE | 2/2 | 100% |
| 3 -- 2D Operations | DONE ✓ | 4/4 | 100% |
| 4 -- 3D Milling | - | 0/4 | 0% |
| 5 -- Visual Preview | DONE ✓ | 1/1 | 100% |
| 6 -- UX Polish + HopSheet | DONE ✓ | 1/1 | 100% |
| 7 -- OpenNest Integration | DONE ✓ | 3/3 | 100% |
| 8 -- Plugin Packaging | DONE ✓ | 5/5 | 100% |
| --- v2.0 Milestone --- | | | |
| 9 -- Korpus Model | IN PROGRESS | 2/4 | 50% |
| 10 -- Part Export | - | 0/4 | 0% |
| 11 -- Technical Drawings | - | 0/4 | 0% |

## Decisions

- Used Encoding.ASCII for .hop output (HOPS is legacy CNC software, no UTF-8 BOM risk)
- operationLines input included as List<string> placeholder for Phase 3+ wiring
- INSTVERSION and EXEVERSION left empty (HOPS 7.7 will override per D-18)
- WZGV line conditionally omitted when empty string (per D-16)
- File naming flexibility: user generated .hop as test/.hop; renamed to test/test_output.hop for plan artifact consistency
- [Phase 03]: All 6 operation CS files passed C# compliance review with zero violations
- [Phase 05]: Fields cleared before guards (not after) to prevent ghost geometry on disconnect
- [Phase 05]: unchecked((int)0xF0F0F0F0) required for dashed line bitmask pattern (overflows int)
- [Phase 05]: Circle.Unset / Line.Unset used as sentinels; check via IsValid (value types, not null)
- [Phase 05]: HopRectPocket preview required CreateFilletCornersCurve + Transform.Rotation for correct fillet/angle display
- [Phase 06]: HopToolDB removed — toolType/feedFactor are machine-level constants (WZF/1.0), dictionary overhead not justified
- [Phase 06]: HopSheet dz input removed — auto-derived from geometry Z-extent (BoundingBox max Z - min Z)
- [Phase 06]: Z reference fixed in all 6 op-components — depth relative to geometry Z, not world Z=0
- [Phase 06]: HopContour gained toolDiameter + side inputs (left/center/right) for kerf compensation
- [Phase 06]: GH canvas cleanup deferred to Phase 8 — script components are temporary; plugin packaging is the right venue
- [Phase 07]: operationLines stored as List<List<string>> (grouped) for HopSheetExport grouping compatibility
- [Phase 07]: HopPart default color cornflower blue (100,149,237) to distinguish from yellow op-component previews
- [Phase 07]: HopSheetExport mirrors HopExport header character-for-character; sheet dz is explicit input (2D curves have no Z extent)
- [Phase 08]: Used dotnet SDK 8.0 with net48 target, local DLL refs (Private=false), PostBuild .dll-to-.gha copy
- [Phase 08]: Icon => null placeholder for all components (icons added when plan 08-02 completes)
- [Phase 08]: Base64-embedded bitmaps in .resx (not ResXFileRef) to avoid System.Resources.Extensions dependency on net48
- [Phase 08]: PIL-generated placeholder icons (nano-banana CLI unavailable); real icons replaceable later via same .resx structure
- [Phase 08-03]: Used Properties.Resources.HopXxx for Icon (icon PNGs already exist)
- [Phase 08-03]: Added System.Resources.Extensions NuGet + GenerateResourceUsePreserializedResources to fix .resx build error
- [Phase 09-01]: Butt-joint construction: side panels full height, Boden/Deckel sit between sides (innerB = B - 2*MS)
- [Phase 09-01]: PlaneToPlane transforms for assembled panel positioning (cleaner than rotation composition)
- [Phase 09-01]: Rueckwand placeholder at same MS thickness -- Phase 9.2 will replace with Rueckwand options component
- [Phase 09-01]: 5 panels (not 6): open front = absence, not a zero-size panel
- [Phase 09-02]: Face-normal extrusion for 3D preview (orientation-agnostic, works for all 5 panel orientations)
- [Phase 09-02]: Transparency 0.3 (vs 0.5 in HopContour) for more opaque furniture preview
- [Phase 87.2-02]: Area ratio thresholds 0.10/10.0 (loose) to avoid false positives on normal inside-offset shrinkage; open curves skip area check
- [Phase 09-02]: Default warm brown colour (180,140,100) to distinguish from op-component yellows/reds
- [Phase 87.2]: FindByLayer(Layer) overload used (not int) for Rhino 7/8 compatibility
- [Phase 87.2]: Rhino.Geometry.Point fully-qualified to resolve System.Drawing.Point ambiguity in HopLayerScanComponent

## Performance Metrics

| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 02 | 01 | 3min | 2 | 4 |
| 02 | 02 | 1min | 1 | 2 |
| 03 | 03 | 1min | 1 | 1 |
| 03 | 04 | 4min | 2 | 1 |
| 05 | 05 | ~60min (incl. UAT) | 7 | 6 |
| 06 | 06 | ~2 days | 4 tasks (Task 5 skipped) | 7 files (1 created, 1 deleted, 6 modified) |
| 07 | 01 | 2min | 1 | 1 |
| 07 | 02 | 2min | 1 | 1 |
| 08 | 01 | 5min | 2 | 5 |
| 08 | 04 | 4min | 2 | 4 |
| Phase 08 P02 | 6min | 2 tasks | 12 files |
| 08 | 03 | 6min | 2 | 7 |
| 09 | 01 | 2min | 2 | 2 |
| 09 | 02 | 2min | 2 | 1 |
| Phase 87.2 P01 | 8 | 2 tasks | 1 files |

## Session

**Last session:** 2026-04-10T00:52:44.047Z
**Stopped at:** Completed 87.2-01-PLAN.md (HopLayerScanComponent)
