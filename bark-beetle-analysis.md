# Bark Beetle — analysis & comparison with the DYNESTIC Post-Processor plugin

*Created: 2026-04-10 — Based on: `bark-beetle-reference/` (GitHub master) + `src/DynesticPostProcessor/`*

---

## 1. What Bark Beetle does

Bark Beetle is a **Grasshopper plugin for general 3-axis CAM** that runs directly inside Rhino/GH and is targeted at ShopBot machines (G-code or `.sbp` format). Core principle: the user places geometry on named layers (`CNC cut`, `CNC pocket`, `CNC drill`, etc.) or wires GH components directly — the plugin produces toolpath curves, which a post-processor node then converts into machine code.

**Technical stack:**
- Pure Grasshopper definitions (`.gh` files), no `.gha` library
- Post-processor logic is a GH-internal C# script node or Python script node
- Layer-based workflow: Python scripts (`BarkBeetleLayers.py`, `get_cutout_cnc.py`, etc.) write Rhino UserText onto curve objects, GH reads these UserText keys and re-runs the solver
- Output: G-code (`.nc`) or ShopBot format (`.sbp`) — both plain-text coordinate sequences
- Streaming: automatic writing to a configured folder on every GH change

**Operations:**
- Cutout (contour with inside/outside detection), Pocket (X/Y raster), Drill, Engrave, Surface 3D Mill (isocurves), Trochoidal HSM, Horizontal 3D Mill (mesh), Automill (Brep analysis → automatic 2D+3D paths)
- Toolpath tools: Auto-Tab, Tab Maker, Safe Offset Curve, Make Pass Depths
- Info tools: Compile Settings, Feedrate Library, Feedrate Calculator (with chip thinning), Machining Time, Find Deepest Z
- Machine-making tools: Rack & Pinion, Harmonic Drive, gear generators (not relevant for CAM purposes)

**No compiled GH components** — everything lives inside the `.gh` file itself (GH script nodes, clusters). That means: no C# type system, no Grasshopper categories/toolbars, no `GH_Component` subclass pattern.

---

## 2. Architectural differences

| Dimension | Bark Beetle | DYNESTIC Plugin |
|-----------|-------------|-----------------|
| **Deployment** | `.gh` file (definition), no build step | Compiled `.gha` (C#, .NET 4.8, VS project) |
| **Target machine** | ShopBot (G-code / `.sbp`) — generic | HOLZ-HER DYNESTIC 7535, `.hop` format (CAMPUS controller) |
| **NC format** | Standard G-code: `G00`/`G01`/`G02`/`G03` + ShopBot macros | Proprietary macro format: `Bohrung(...)`, `SP/G01/G03M/EP`, `CALL _RechteckTasche_V5(...)` — no standard G-code blocks |
| **Feed rate** | User-side: a Feedrate Calculator component outputs `mm/min`, the post-processor writes `F` values directly into the code | Machine-side: the DYNESTIC plugin writes **no** feed values. `_VE`, `_VA`, `_SD` are placeholders that CAMPUS resolves from the tool magazine |
| **Toolpath generation** | Full geometry algorithms in GH: curve offset, inside/outside detection, raster pocket, tabs, pass-depth generation, 3D mesh slicing — all inside Bark Beetle | Coordinate handover to machine macros: e.g. `_RechteckTasche_V5` handles the pocket strategy internally in the controller. The plugin only supplies geometry parameters (centre, width, depth). |
| **Tool types** | One tool type (cutter), parameterised by diameter + RPM + feedrate | Three tool types with their own tool-call syntax: `WZB` (drill), `WZF` (cutter), `WZS` (saw) — each with its own macro family |
| **Data flow** | Curves → toolpath curves → post-processor → code | Geometry/points → `operationLines: List<string>` → `HopExport` → `.hop` file |
| **Sorting** | No automatic operation order — user is responsible | `NcExport.SortOperationLines()` sorts block-wise: WZB → WZF → WZS (drills before milling, milling before sawing) |
| **Validation** | No dedicated validation tool | `HopAnalyzerComponent`: checks SP/EP parity, empty blocks, moves outside SP/EP, duplicate tool numbers |
| **Nesting** | Not built in (external tools) | `HopPart` + `HopSheet` + `HopSheetExport` + `HopPartExport`: integration with OpenNest, transform applied to `operationLines` |
| **High-level parametrics** | Automill: Brep → automatically mixed 2D+3D paths | `HopKorpus`: cabinet body → 5 panels with joinery, connectors, shelf pins, doors, etc. |
| **AutoWire** | No AutoWire concept | `AutoWire.cs`: when dropped on the canvas, sliders/toggles/value lists are created and wired automatically |
| **Preview** | Standard GH preview of toolpath curves | `PreviewHelper` + per-component `DrawViewportMeshes`/`DrawViewportWires`: 3D volume preview (cylinders, extruded slots, tilted saw kerf box) directly in Rhino |

**Core difference in CAM philosophy:**
Bark Beetle generates the full cutting geometry (raster, offset curves, tabs) itself and writes it as a coordinate sequence into the code. The DYNESTIC plugin delegates the cutting geometry **to the machine controller** — it only supplies semantic parameters (`centre`, `width`, `depth`, `radius`) and calls machine subroutines. That is fundamentally different and not interchangeable.

---

## 3. Adaptable ideas

### 3.1 Feedrate Calculator as a standalone component
**In Bark Beetle:** the `Feedrate Calculator` component computes `feedrate = (chipload * flutes * RPM)` from material, bit diameter, flute count, RPM. Separately: `Feedrate Calculator for Arcs` compensates for the speed difference when travelling on arcs (the inner edge runs slower than the outer one).

**Why adaptable:** the DYNESTIC plugin currently writes no feed values (`_VE`, `_VA` are placeholders). If that changes (e.g. writing `F` values directly into SP blocks), a `HopFeedrate` component would make sense. Pattern: inputs Material + BitDiameter + Flutes + RPM + MaxStepover → output `feedrate` as a number, fed into `HopContour` / `HopEngraving`.

**Concrete file:** `Bark beetle 1.02 - CNC milling - Rhino6.gh` (internal GH script node, no separate Python source available — the logic must be extracted from the `.gh` file).

---

### 3.2 Machining Time Estimator
**In Bark Beetle:** the `Machining time` component sums all toolpath lengths and divides by feedrate → total time in seconds/minutes.

**Why adaptable:** the DYNESTIC plugin has no time estimation at all. A `HopMachiningTime` component could operate on `hopContent`: parse SP/EP blocks, sum G01 distances and G02M/G03M arc lengths, run them against a rough feedrate estimate. It would be a useful feedback channel alongside `HopAnalyzer`.

**Pattern from Bark Beetle:** input `toolpaths (curves) + feedrate` → computed length → time. In the DYNESTIC context: input `hopContent + feedrate` → time. Parsing coordinates from G01/G03M lines is feasible.

**Implementation:** new component type `HopMachiningTime` next to `HopAnalyzer` in `Components/Export/`. Similar base structure to `HopAnalyzerComponent.cs` (parses `hopContent`, `Run` toggle).

---

### 3.3 Find Deepest Z / safety check
**In Bark Beetle:** the `Find deepest Z` component returns the deepest Z value across all toolpaths — a safety check before starting.

**Why adaptable:** in the DYNESTIC context: from the `operationLines` (before export) or from `hopContent` (after export), extract the deepest Z out of all `Bohrung`, `SP`, `Tiefe` parameters and compare against `DZ` (material thickness). Warn when cutZ < 0 (i.e. deeper than the panel). This would be a sensible addition for `HopAnalyzer` or as a separate check output.

**Concrete risk without this feature:** `HopDrill` computes `cutZ = surfaceZ - depth` without checking whether the result becomes negative (deeper than the machine table). A depth validation would catch that.

**Implementation:** as an additional check in the existing `HopAnalyzerComponent.cs` (parse Z values from `Bohrung` lines and `SP` lines, check against 0).

---

### 3.4 Automill concept → HopAutomill for simple Brep dissection
**In Bark Beetle:** the `Automill` component analyses a Brep, classifies faces by angle (horizontal = Pocket/Cut, vertical = 3D contour), automatically extracts depth values, produces a combined 2D+3D job without manual layer setting.

**Why partially adaptable:** a `HopAutomill` for the DYNESTIC plugin would be a component that analyses a Brep and automatically produces `HopContour` and `HopRectPocket` output lines. Concretely: horizontal planar faces → `CALL _RechteckTasche_V5(...)`, vertical outer edges → SP/G01/EP contours. This would significantly simplify the common workflow "Brep in, milling operations out".

**Limitation:** Bark Beetle's Automill produces curve output (generic), DYNESTIC needs concrete machine macro parameters. The Brep dissection logic (face-angle analysis, depth extraction) is, however, directly transferable. The output would then be `operationLines` directly.

**Not yet present in the DYNESTIC plugin** — no equivalent feature in `Components/Operations/`.

---

### 3.5 Make Pass Depths (layer generation from a 3D curve)
**In Bark Beetle:** the `Make pass depths` component takes 3D curves and produces Z-staggered copies as roughing passes down to the target depth.

**Why partially adaptable:** the `stepdown` logic is already implemented inline in `HopContourComponent.cs` (lines 258–272) (`int passCount = (int)Math.Ceiling(depth / stepdown)`). Bark Beetle's approach is more powerful: it decouples pass generation into a separate, reusable component, so it can also be used for 3D curves from other sources.

**Adaptation idea:** a `HopPassDepths` utility component that takes a 3D input curve + stepdown value and produces a list of Z-staggered curves. This would be useful for `HopEngraving` with multiple passes, which currently does not support `stepdown` (`HopEngravingComponent.cs` has no stepdown input).

---

### 3.6 Safe Offset Curve
**In Bark Beetle:** `Safe offset curve` always produces closed, non-self-intersecting offset curves — even with extreme offset values where `Curve.Offset()` fails.

**Why adaptable:** `HopContourComponent.cs` (lines 155–167) calls `curve.Offset()` and only falls back to "center path" when it fails. With small, tight curves and a large tool diameter, this fails. The safe-offset pattern from Bark Beetle (iterative shrinking + self-intersection check) would be more robust.

**Concretely:** in `HopContourComponent.cs`, section 5 "GEOMETRIC PRE-OFFSET", replace the fallback logic with a more stable offset implementation.

---

### 3.7 Layer-based workflow as an optional input pattern
**In Bark Beetle:** `BarkBeetleLayers.py` creates standard layers (`CNC drill`, `CNC pocket`, `CNC cut`, etc.). Curves on these layers are automatically connected by the GH solver to the right operations.

**Why adaptable:** the DYNESTIC plugin always expects explicit GH connections. An optional layer-scanning feature ("all curves on the `DYNESTIC_CONTOUR` layer → `HopContour` with default settings") would lower the barrier to entry — especially for users who already know Rhino layer workflows.

**Implementation:** either as a separate Python script (analogous to `get_cutout_cnc.py`) or as a GH utility component `HopLayerScan` that takes layer names as input and outputs curve lists.

---

## 4. Not relevant

### 4.1 G-code / ShopBot format
Bark Beetle writes standard G-code (`G00`, `G01`, `G02`, `G03`, `F`, `S`) and ShopBot format (`.sbp`). The DYNESTIC plugin writes the `.hop` macro format for CAMPUS. The formats are incompatible — no code reuse possible. The G-code post-processor logic from Bark Beetle's `.gh` scripts is entirely irrelevant.

### 4.2 Writing feed rate into NC code
Bark Beetle writes `F` values directly. DYNESTIC delegates this to the machine controller (`_VE`, `_VA`). As long as the CAMPUS controller behaviour is not changed, the feed-rate writing logic from Bark Beetle is not transferable — only the calculator concept (section 3.1).

### 4.3 3D milling operations (Surface 3D Mill, Horizontal 3D Mill, Trochoidal HSM)
According to `README.md` and `BACKLOG.md`: "3DMilling, Mill5Axis, and VSPMillSAxis are **not licensed** on the current HOPS dongle (ID: 3-5709426)." All 3D milling algorithms from Bark Beetle (isocurve extraction, mesh slicing, arc-based HSM) are therefore irrelevant — the machine cannot run these paths, HOPS cannot simulate them.

### 4.4 Machine-making tools
Rack & Pinion, Harmonic Drive, gear generators — not relevant for woodworking / panel processing. These tools serve machine-geometry creation (for machine builders), not machine operation.

### 4.5 OctoPrint integration (`Send and start gcode`)
Bark Beetle can upload G-code to an OctoPrint server and start the job. DYNESTIC machines run via the CAMPUS controller, not via OctoPrint — irrelevant.

### 4.6 SVG preview / AR projection
`Preview graphic for AR projection on machine bed` produces SVG for browser projection onto the machine bed. The DYNESTIC plugin has 3D preview directly in Rhino (`PreviewHelper.cs`). The SVG output is redundant.

### 4.7 All-in-one GH definition architecture
Bark Beetle's approach (everything inside one `.gh` file, no compiled library) is an explicit design decision for maximum editability. The DYNESTIC plugin is deliberately built as a compiled `.gha` (type safety, AutoWire, icon system, categories). Switching back to GH script nodes would be a regression.

---

*Reference files:*
- `bark-beetle-reference/README.md`
- `bark-beetle-reference/Python_scripts/` (BarkBeetleLayers.py, get_cutout_cnc.py, get_pocket_cnc.py, define_material_cnc.py, remove_settings.py)
- `src/DynesticPostProcessor/NcStrings.cs`
- `src/DynesticPostProcessor/AutoWire.cs`
- `src/DynesticPostProcessor/Components/Operations/HopContourComponent.cs`
- `src/DynesticPostProcessor/Components/Operations/HopDrillComponent.cs`
- `src/DynesticPostProcessor/Components/Operations/HopRectPocketComponent.cs`
- `src/DynesticPostProcessor/Components/Operations/HopSawComponent.cs`
- `src/DynesticPostProcessor/Components/Export/HopExportComponent.cs`
- `src/DynesticPostProcessor/Components/Export/HopAnalyzerComponent.cs`
- `src/DynesticPostProcessor/Components/Nesting/HopSheetExportComponent.cs`
- `src/DynesticPostProcessor/Components/Korpus/HopKorpusComponent.cs`
