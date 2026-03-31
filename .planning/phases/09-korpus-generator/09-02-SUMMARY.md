---
phase: 09-korpus-generator
plan: 02
subsystem: korpus
tags: [grasshopper, gh-component, brep, viewport-preview, 3d-assembly, rhino-display, cabinet]

# Dependency graph
requires:
  - phase: 09-korpus-generator
    provides: "HopKorpusComponent with 5 flat panels, AssembledTransform per panel"
provides:
  - "3D shaded+wireframe viewport preview of assembled korpus body"
  - "Colour input for user-controlled preview color"
  - "ClippingBox override for correct viewport framing"
  - "ClearData override preventing ghost geometry on disconnect"
affects: [09-03-verbinder, 10-part-export]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Face normal extrusion for converting flat panel Breps to 3D solids"
    - "DisplayMaterial with Transparency=0.3 for semi-transparent shaded preview"
    - "DrawViewportMeshes + DrawViewportWires dual override for solid+wireframe display"

key-files:
  created: []
  modified:
    - "src/DynesticPostProcessor/Components/Korpus/HopKorpusComponent.cs"

key-decisions:
  - "Face normal extrusion: extrude each assembled face along its normal by panel thickness (robust regardless of panel orientation)"
  - "Transparency 0.3 (vs 0.5 in HopContour): slightly more opaque for furniture preview realism"
  - "Colour input default warm brown Color.FromArgb(180,140,100) -- visually distinct from operation component yellows/reds"

patterns-established:
  - "Korpus preview pattern: _previewBreps list populated in SolveInstance, drawn in DrawViewportMeshes/Wires"

requirements-completed: [KORPUS-02]

# Metrics
duration: 2min
completed: 2026-03-31
---

# Phase 9.1 Plan 02: 3D Viewport Preview for Assembled Korpus Summary

**Semi-transparent shaded 3D preview of assembled cabinet body with face-normal extrusion, warm brown default colour, and live slider update via DrawViewportMeshes/Wires overrides**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-31T21:04:10Z
- **Completed:** 2026-03-31T21:06:09Z
- **Tasks:** 2/2 (Task 2: human-verify checkpoint approved)
- **Files modified:** 1

## Accomplishments
- 3D assembled korpus visible in Rhino viewport as shaded semi-transparent geometry with wireframe edges
- Each panel extruded along its face normal by material thickness for correct 3D solid representation
- Colour input (optional, default warm brown) allows user customization of preview color
- Preview clears on disconnect via ClearData override and SolveInstance reset
- AutoWire updated to 6 specs including Skip() for colour parameter

## Task Commits

Each task was committed atomically:

1. **Task 1: Add 3D assembled preview to HopKorpusComponent** - `b9e1e7e` (feat)
2. **Task 2: Verify 3D korpus preview in Grasshopper** - human-verify checkpoint approved ("klappt")

## Files Created/Modified
- `src/DynesticPostProcessor/Components/Korpus/HopKorpusComponent.cs` - Added _previewBreps field, ClearData override, Colour input at index 5, preview Brep building via AssembledTransform + face normal extrusion, ClippingBox/DrawViewportMeshes/DrawViewportWires overrides, AutoWire updated to 6 specs

## Decisions Made
- Face normal extrusion approach: each flat panel is transformed to assembled position, then the first face's normal is used as the extrusion direction. This is orientation-agnostic and works for all 5 panel orientations.
- Transparency 0.3 chosen (vs 0.5 in HopContour) for slightly more opaque korpus preview, better for furniture visualization
- Default colour warm brown (180,140,100) to visually distinguish from yellow/red/cyan operation component previews

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None - all preview geometry is computed from real panel data.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Human Verification

**Task 2: Verify 3D korpus preview in Grasshopper** -- APPROVED
- User response: "klappt" (works)
- 3D assembled korpus visible and correct in Rhino viewport
- Slider updates, panel dimensions, butt joint construction all confirmed working

## Next Phase Readiness
- Human UAT passed -- Phase 9.1 complete
- Preview infrastructure ready for HopVerbinder (Phase 9.2) to add connector previews
- Panels output remains compatible with HopPart nesting pipeline

## Self-Check: PASSED

- [x] HopKorpusComponent.cs modified (not recreated)
- [x] _previewBreps field declared
- [x] ClearData() override present
- [x] DrawViewportMeshes override present
- [x] DrawViewportWires override present
- [x] ClippingBox override present
- [x] Colour input at index 5 with Optional=true
- [x] AutoWire has 6 specs (including Skip)
- [x] dotnet build: 0 errors, 0 warnings
- [x] Commit b9e1e7e found

---
*Phase: 09-korpus-generator*
*Completed: 2026-03-31 (human verification passed)*
