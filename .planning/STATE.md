---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: — GH Post-Processor
status: executing
stopped_at: Completed 08-04-PLAN.md (nesting+export components)
last_updated: "2026-03-31T17:13:26.946Z"
progress:
  total_phases: 7
  completed_phases: 5
  total_plans: 16
  completed_plans: 14
---

# Project State

**Last updated:** 2026-03-31
**Status:** Executing Phase 08

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-22)

**Core value:** One GH definition takes geometry to a ready-to-run .hop file for the DYNESTIC -- no CAM middleman.
**Current focus:** Phase 08 — plugin-packaging

## Current Phase

**Phase 3: COMPLETE ✓** — Human UAT passed 2026-03-30. All 3 tests confirmed.

**Phase 5 (Visual Preview): COMPLETE ✓** — Human UAT passed 2026-03-30. All 7 tasks confirmed. DrawViewportWires preview working in all 6 operation components.

**Phase 6 (UX Polish + HopSheet): COMPLETE ✓** — 2026-03-31. HopSheet live; Z-reference fixed in all 6 components; HopContour kerf compensation added; HopToolDB removed (machine-level constants); canvas cleanup deferred to Phase 8.

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
| 7 -- OpenNest Integration | IN PROGRESS | 2/3 | 67% |
| 8 -- Plugin Packaging | IN PROGRESS | 2/5 | 40% |
| --- v2.0 Milestone --- | | | |
| 9 -- Korpus Model | - | 0/4 | 0% |
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

## Session

**Last session:** 2026-03-31T17:19:53Z
**Stopped at:** Completed 08-04-PLAN.md (nesting+export components)
