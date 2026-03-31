---
phase: 07-opennest
plan: 01
subsystem: nesting
tags: [opennest, gh-objectwrapper, dictionary-wireformat, grain-direction, preview]

# Dependency graph
requires:
  - phase: 03-2d-operations
    provides: "op-component operationLines output pattern (List<string>)"
  - phase: 05-visual-preview
    provides: "DrawViewportWires/ClippingBox preview pattern"
provides:
  - "HopPart component: bundles outline + operationLines + grainDir into Dictionary<string,object>"
  - "GH_ObjectWrapper wire format for cross-assembly transport"
  - "Grain direction arrow preview for orientation verification"
affects: [07-02-PLAN, 07-03-PLAN, opennest-integration]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Dictionary<string,object> wire format with outline/operationLines/grainDir keys", "GH_ObjectWrapper output wrapping for part bundling", "AreaMassProperties.Compute centroid for grain arrow placement"]

key-files:
  created: ["scripts/HopPart.cs"]
  modified: []

key-decisions:
  - "operationLines stored as List<List<string>> (grouped) for downstream HopSheetExport compatibility"
  - "Grain arrow is 20mm fixed length from outline centroid, drawn in dark grey (80,80,80)"
  - "Default color cornflower blue (100,149,237) to distinguish HopPart from op-components (yellow)"
  - "grainAngle clamped to 0-360 range with fallback to 0 degrees"

patterns-established:
  - "HopPart Dictionary wire format: dict[outline]=Curve, dict[operationLines]=List<List<string>>, dict[grainDir]=Vector3d"
  - "Part bundling via GH_ObjectWrapper for OpenNest workflow integration"

requirements-completed: [NEST-01]

# Metrics
duration: 2min
completed: 2026-03-31
---

# Phase 7 Plan 1: HopPart Component Summary

**HopPart GH script component bundles closed outline curve + operation lines + grain direction into a Dictionary wrapped in GH_ObjectWrapper for cross-component nesting workflow transport**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-31T16:19:56Z
- **Completed:** 2026-03-31T16:21:26Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Created HopPart.cs with Dictionary<string,object> wire format carrying outline, operationLines, and grainDir
- GH_ObjectWrapper output wrapping enables safe cross-assembly transport between GH script components
- Grain direction arrow preview drawn at outline centroid for visual orientation verification (per D-12)
- Guard checks emit GH Error for null or non-closed outline curves

## Task Commits

Each task was committed atomically:

1. **Task 1: Create HopPart.cs script component** - `c1a8954` (feat)

## Files Created/Modified
- `scripts/HopPart.cs` - Part bundling component: closed outline + operationLines + grainDir bundled into Dictionary, wrapped in GH_ObjectWrapper, with grain arrow viewport preview

## Decisions Made
- operationLines stored as `List<List<string>>` (not flat `List<string>`) to maintain grouping structure for downstream HopSheetExport consumption
- Grain arrow length fixed at 20mm from centroid -- sufficient for visual verification without cluttering small parts
- Default color set to cornflower blue (100,149,237) to visually distinguish HopPart previews from yellow op-component previews
- `DrawViewportMeshes` override included (empty body) to satisfy the three-method preview pattern established in the project

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- HopPart.cs ready for wiring into OpenNest workflow (plan 07-02: HopSheetExport)
- Dictionary wire format established: downstream components can extract outline via `dict["outline"]`, operation lines via `dict["operationLines"]`, grain direction via `dict["grainDir"]`
- GH_ObjectWrapper pattern validated -- same approach used successfully in Phase 3/5

## Self-Check: PASSED

- FOUND: scripts/HopPart.cs
- FOUND: .planning/phases/07-opennest/07-01-SUMMARY.md
- FOUND: commit c1a8954

---
*Phase: 07-opennest*
*Completed: 2026-03-31*
