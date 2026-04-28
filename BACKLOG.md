# Plugin Backlog

Improvements and open items.

---

## Open — needs verification on the machine

### `LAGE` parameter in HopSaw unverified
- `LAGE:=bladeAngle` in `_nuten_frei_v5` is an assumption — not tested on the machine
- Until this is verified, HopSaw may produce wrong cuts
- **Action:** run a test cut on the DYNESTIC. If `LAGE` is not the blade-tilt axis, find the correct parameter from the CAMPUS controller.

### `HopDrill` Stepdown — machine test pending
- Loop order has been fixed (outer loop = point, inner = pass) ✅
- Whether this makes a difference on the CAMPUS controller: not yet tested
- **Action:** run a test drill with stepdown and check the travel order.

### `HopExport` header dimensions — machine test pending
- Header now writes correct DX/DY/DZ values ✅
- Unclear whether CAMPUS reads the header or the VARS block
- **Action:** verify on the machine whether the header values are relevant.

---

## Missing components

### `HopAngledDrill` (angled drill)
- Removed from Phase 8.7 — first check the CAMPUS controller for whether the macro exists
- **Action:** find the macro name + parameters in CAMPUS, then implement.

---

## Done in Phase 8.7 (2026-04-06)

- ✅ `HopDrill` stepdown loop order fixed (point → passes, not the other way around)
- ✅ `HopExport` header dimensions: DX/DY/DZ from inputs instead of hardcoded 0
- ✅ `HopExport` operation sorting: WZB → WZF → WZS automatic
- ✅ `HopExport` export trigger: rising edge (false→true), no manual reset needed
- ✅ `HopSaw` length from dirLine: when Length=Default (600), the line length is used
- ✅ `PreviewHelper` static class: DrawMeshes / DrawWires / GetClippingBox
- ✅ All 7 op components migrated to PreviewHelper (no more duplicated preview code)
- ✅ `NcStrings.cs`: NcDrill / NcSaw / NcExport internal helpers without Rhino dependency
- ✅ NUnit 3 test project: 17 tests, all green
- ✅ HopEdgeBanding: deliberately dropped

---

## Notes

- Use `MACHINE_NOTES.md` for machine-specific findings (e.g. verified `LAGE` value)
- For all NC-format questions, check `Datein/Muster_DXF_Import.hop` first — that is the ground truth
