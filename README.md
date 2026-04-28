# Wallaby Hop

A Grasshopper plugin for generating `.hop` NC files for the **HOLZ-HER DYNESTIC 7535** CNC machine, controlled via the **HOPS 7.7** CAM software and **HOLZHER CAMPUS** machine controller.

Design parametrically in Rhino/Grasshopper, wire components together, and export production-ready `.hop` files directly — no manual NC coding, no HOPS GUI.

---

## Table of Contents

- [How it works](#how-it-works)
- [Installation](#installation)
- [The .hop file format](#the-hop-file-format)
- [Component reference](#component-reference)
  - [Drilling](#drilling)
  - [Milling](#milling)
  - [Sawing](#sawing)
  - [Hardware](#hardware)
  - [Export](#export)
  - [Cabinet (Cabinet)](#cabinet-cabinet)
  - [Nesting](#nesting)
  - [Drawing](#drawing)
  - [Utility](#utility)
- [AutoWire](#autowire)
- [Typical workflows](#typical-workflows)
- [Machine & software notes](#machine--software-notes)

---

## How it works

The plugin follows a simple pipeline:

```
[Geometry / Points / Curves]
        ↓
[Operation Components]   →   operationLines (List<string>)
        ↓
[HopExport]              →   hopContent (string) + .hop file on disk
        ↓
[HopAnalyzer]            →   isValid / errors (optional validation step)
```

Each operation component (HopDrill, HopContour, etc.) takes Grasshopper geometry as input and outputs a `List<string>` — a list of NC-Hops macro call strings. **HopExport** collects these lists, sorts them by tool type (WZB → WZF → WZS), assembles the file header, VARS block, and START section, then writes a syntactically valid `.hop` file. **HopAnalyzer** can validate the final output before machining.

The `.hop` format is not G-code. It is a macro language interpreted by the HOLZHER CAMPUS controller. Each line like `Bohrung(...)` or `CALL _RechteckTasche_V5(...)` is a machine-side subroutine call — the controller handles feed rates, Z-homing, and tool approach internally.

---

## Installation

**Requirements:**
- Rhino 7 or 8
- Grasshopper (included with Rhino)

**Steps:**
1. Build the project in Visual Studio:
   `src/DynesticPostProcessor/DynesticPostProcessor.csproj`
   Target framework: `.NET Framework 4.8` (required by Rhino 7/8)

2. The post-build step automatically copies `WallabyHop.gha` to:
   `%AppData%\Grasshopper\Libraries\`

3. Restart Rhino. The components appear in the **Wallaby Hop** tab in the Grasshopper toolbar.

---

## The .hop file format

A `.hop` file is a plain ASCII text file with CRLF line endings. It has three sections:

```
;MAKROTYP=0              ← File header (comment lines starting with ;)
;MASCHINE=HOLZHER
;NCNAME=my_part
;DX=0.000
...

VARS                     ← Variable declarations
   DX := 800.0;*VAR*Dimension X
   DY := 400.0;*VAR*Dimension Y
   DZ := 19.0;*VAR*Dimension Z

START                    ← Program body
Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)
CALL HH_Park ( VAL PARK:=3,X:=0,Y:=0)
WZB (5,_VE,_V*1,_VA,_SD,0,'')          ← Tool call
Bohrung (100.0,200.0,19.0,2.0,8.0,0,0,0,0,0,0,0)   ← Operation macro
```

**Key macros used by this plugin:**

| Macro | Operation | Component |
|-------|-----------|-----------|
| `Bohrung(x,y,surfZ,cutZ,dia,...)` | Vertical drill | HopDrill |
| `CALL _Bohgx_V5(...)` / `_Bohgy_V5(...)` | Drill row (X/Y) | HopDrillRow |
| `SP(...)` / `G01(...)` / `G02M(...)` / `G03M(...)` / `EP(...)` | Contour / engraving path | HopContour, HopEngraving |
| `CALL _RechteckTasche_V5(...)` | Rectangular pocket | HopRectPocket |
| `CALL _Kreistasche_V5(...)` | Circular pocket | HopCircPocket |
| `CALL _Kreisbahn_V5(...)` | Circular path | HopCircPath |
| `CALL _nuten_frei_v5(...)` | Free slot | HopFreeSlot |
| `CALL _Nuten_X_V5(...)` / `_Nuten_Y_V5(...)` | Groove slot (X/Y axis) | HopGrooveSlot |
| `CALL _saege_x_V7(...)` / `_saege_y_V7(...)` | Format saw cut | HopFormatCut |
| `WZS(...)` + saw path | Circular saw path | HopSaw |
| `CALL _Topf_V5(...)` | Blum hinge cup drill | HopBlumHinge |
| `Fixchip_K(...)` | Fixing clamp | HopFixchip |
| `B2Punkte_V7(...)` | Dimension line markup | HopDimension |
| `WZB(...)` | Drill tool call | All WZB ops |
| `WZF(...)` | Milling tool call | All WZF ops |
| `WZS(...)` | Saw tool call | All WZS ops |

---

## Component reference

All components live in the **Wallaby Hop** tab in Grasshopper. Subcategories group them by operation type.

---

### Drilling

#### HopDrill

Converts a list of 3D points into vertical drilling operations.

**NC macro:** `Bohrung(...)`

| Input | Type | Description |
|-------|------|-------------|
| `points` | Point3d list | Drill positions. Z of highest point = plate surface. |
| `depth` | float | Drilling depth in mm. Default: 1.0 |
| `diameter` | float | Drill diameter in mm. Default: 8.0 |
| `stepdown` | float | Depth per pass for peck drilling. 0 = single pass. |
| `toolNr` | int | Tool magazine position (must be > 0) |
| `colour` | Color | Viewport preview color |

| Output | Type | Description |
|--------|------|-------------|
| `operationLines` | string list | NC macro strings → wire into HopExport |

**Notes:**
- `surfaceZ` auto-derived as max Z across all input points.
- With `stepdown > 0`, drilling is split into multiple Bohrung passes at increasing depth.
- Renders translucent drill cylinders in the Rhino viewport.

---

#### HopDrillRow

Generates a parametric row of equally spaced holes along the X or Y axis using the `_Bohgx_V5` / `_Bohgy_V5` macros.

**NC macro:** `CALL _Bohgx_V5(...)` or `CALL _Bohgy_V5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `startPoint` | Point3d | First hole position |
| `count` | int | Number of holes in the row |
| `spacing` | float | Distance between holes in mm |
| `axis` | int | 0 = X-axis row, 1 = Y-axis row |
| `depth` | float | Drilling depth in mm |
| `diameter` | float | Drill diameter in mm |
| `toolNr` | int | Tool magazine position |
| `colour` | Color | Viewport preview color |

---

### Milling

#### HopContour

Converts a planar curve into a 2D contour cutting path using `SP/G01/G02M/G03M/EP` macros. Handles both straight segments and arcs.

**NC macros:** `SP`, `G01`, `G02M`, `G03M`, `EP`

| Input | Type | Description |
|-------|------|-------------|
| `curve` | Curve | Planar closed or open curve. Must lie in or near World XY. |
| `depth` | float | Cutting depth in mm. Default: 1.0 |
| `plungeZ` | float | First-pass plunge depth override. 0 = same as depth. |
| `tolerance` | float | NURBS → polyline/arc conversion tolerance in mm. Default: 0.1 |
| `toolNr` | int | Tool magazine position |
| `toolDiameter` | float | Tool diameter for kerf offset. Default: 8.0 |
| `side` | int | Kerf compensation: -1 = inside, 0 = center, +1 = outside |
| `stepdown` | float | Depth per pass for multi-pass cutting. 0 = single pass. |
| `colour` | Color | Viewport preview color |

**Notes:**
- Lines → `G01`, arcs → `G02M`/`G03M` (CW/CCW from arc normal).
- Kerf compensation is geometric pre-offset — no machine-side G41/G42.
- With `stepdown`, multiple full contour passes are generated.
- Renders a shaded toolpath volume in the viewport.

---

#### HopEngraving

Generates engraving paths for one or more curves. Follows the input curve exactly — no kerf offset. Designed for shallow cuts with V-bits or engraving spindles.

**NC macros:** `SP`, `G01`, `G02M`, `G03M`, `EP`

| Input | Type | Description |
|-------|------|-------------|
| `curves` | Curve list | One or more planar curves to engrave. |
| `depth` | float | Engraving depth in mm. Default: 0.5 |
| `tolerance` | float | NURBS conversion tolerance. Default: 0.05 |
| `toolNr` | int | Tool magazine position |
| `colour` | Color | Viewport preview color |

**Notes:**
- Multiple input curves each produce their own SP/EP block within one WZF call.
- Preview renders a pipe volume along the engraving path.

---

#### HopRectPocket

Generates a rectangular pocket using the `_RechteckTasche_V5` macro. Dimensions from the input curve's bounding box.

**NC macro:** `CALL _RechteckTasche_V5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `rectCurve` | Curve | Closed rectangle curve. Center and size from bounding box. |
| `cornerRadius` | float | Fillet radius in mm. 0 = sharp corners. |
| `angle` | float | Rotation angle in degrees. 0 = axis-aligned. |
| `depth` | float | Pocket depth in mm. Default: 1.0 |
| `stepdown` | float | Depth per pass. 0 = single pass. |
| `toolNr` | int | Tool magazine position |
| `colour` | Color | Viewport preview color |

---

#### HopCircPocket

Generates a circular pocket using the `_Kreistasche_V5` macro.

**NC macro:** `CALL _Kreistasche_V5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `center` | Point3d | Center of the pocket. Z = plate surface. |
| `radius` | float | Pocket radius in mm |
| `depth` | float | Pocket depth in mm. Default: 1.0 |
| `stepdown` | float | Depth per pass. 0 = single pass. |
| `toolNr` | int | Tool magazine position |
| `colour` | Color | Viewport preview color |

---

#### HopCircPath

Generates a circular profile cutting path using the `_Kreisbahn_V5` macro. Cuts along a circle (not a full pocket — path only).

**NC macro:** `CALL _Kreisbahn_V5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `center` | Point3d | Center of the circular path. |
| `radius` | float | Path radius in mm |
| `radiusCorr` | int | Radius correction: -1 = outside, 0 = center, +1 = inside |
| `depth` | float | Cut depth in mm. Default: 1.0 |
| `stepdown` | float | Depth per pass. 0 = single pass. |
| `angle` | float | Arc angle in degrees. 360 = full circle. Default: 360. |
| `toolNr` | int | Tool magazine position |
| `colour` | Color | Viewport preview color |

---

#### HopFreeSlot

Generates a free slot between two points using the `_nuten_frei_v5` macro.

**NC macro:** `CALL _nuten_frei_v5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `p1` | Point3d | Slot start point |
| `p2` | Point3d | Slot end point |
| `slotWidth` | float | Slot width in mm |
| `depth` | float | Slot depth in mm. Default: 1.0 |
| `toolNr` | int | Tool magazine position |
| `colour` | Color | Viewport preview color |

---

#### HopGrooveSlot

Generates axis-aligned groove operations using `_Nuten_X_V5` (horizontal) or `_Nuten_Y_V5` (vertical) macros. Designed for through-slot or stopped-slot dado cuts.

**NC macro:** `CALL _Nuten_X_V5(...)` or `CALL _Nuten_Y_V5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `startPoint` | Point3d | Slot start position |
| `length` | float | Slot length in mm |
| `axis` | int | 0 = X-axis groove, 1 = Y-axis groove |
| `width` | float | Groove width in mm |
| `depth` | float | Groove depth in mm |
| `toolNr` | int | Tool magazine position |
| `colour` | Color | Viewport preview color |

---

### Sawing

#### HopFormatCut

Generates format saw cuts using the `_saege_x_V7` / `_saege_y_V7` macros. Used for straight trim cuts along X or Y axis.

**NC macro:** `CALL _saege_x_V7(...)` or `CALL _saege_y_V7(...)`

| Input | Type | Description |
|-------|------|-------------|
| `position` | float | Cut position (X or Y coordinate) |
| `axis` | int | 0 = cut along X, 1 = cut along Y |
| `depth` | float | Saw depth in mm |
| `toolNr` | int | Saw tool magazine position |
| `colour` | Color | Viewport preview color |

---

#### HopSaw

Generates a freeform saw path (WZS tool call + contour sequence). For non-axis-aligned saw cuts or curved saw paths.

**NC macro:** `WZS(...)` + `SP`/`G01`/`EP`

| Input | Type | Description |
|-------|------|-------------|
| `curve` | Curve | Saw path curve |
| `depth` | float | Saw depth in mm |
| `toolNr` | int | Saw tool magazine position |
| `colour` | Color | Viewport preview color |

---

### Hardware

#### HopBlumHinge

Generates Blum cup hinge drilling operations using the `_Topf_V5` macro. Handles standard 35mm cup bore with face-frame or inset mounting.

**NC macro:** `CALL _Topf_V5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `positions` | Point3d list | Hinge center positions |
| `cupDiameter` | float | Cup bore diameter in mm. Default: 35.0 |
| `depth` | float | Cup bore depth in mm. Default: 13.0 |
| `toolNr` | int | Tool magazine position |
| `colour` | Color | Viewport preview color |

---

#### HopFixchip

Generates fixing clamp positions using the `Fixchip_K` macro. Used to define clamping points that secure the workpiece during machining.

**NC macro:** `Fixchip_K(...)`

| Input | Type | Description |
|-------|------|-------------|
| `positions` | Point3d list | Clamp center positions |
| `toolNr` | int | Tool magazine position |

---

### Export

#### HopExport

Assembles all operation lines into a complete `.hop` file and writes it to disk.

| Input | Type | Description |
|-------|------|-------------|
| `folder` | string | Output directory path. Must exist. |
| `fileName` | string | File name without `.hop` extension. |
| `export` | bool | Toggle to trigger file write. False = no output. |
| `dx` | float | Sheet width in mm. Default: 800 |
| `dy` | float | Sheet height in mm. Default: 400 |
| `dz` | float | Material thickness in mm. Default: 19 |
| `wzgv` | string | Tool preset ID for the header. Default: `7023K_681` |
| `operationLines` | string list | All NC macro strings from operation components. |

| Output | Type | Description |
|--------|------|-------------|
| `hopContent` | string | Full file content as string (for inspection) |
| `statusMsg` | string | Export status message with file path |

**Notes:**
- Merge multiple operation components using a Grasshopper **Merge** component before wiring into `operationLines`.
- Operations are automatically sorted: **WZB → WZF → WZS → rest**. Sorting is block-based — each tool call with all its following SP/EP/G01 lines moves together as a unit.
- File is written with ASCII encoding and CRLF line endings (required by CAMPUS controller).
- `export = false` means no accidental file writes.

**Generated file structure:**
```
;MAKROTYP=0
;MASCHINE=HOLZHER
;NCNAME=fileName
;WZGV=7023K_681
...
VARS
   DX := 800.0;*VAR*Dimension X
   DY := 400.0;*VAR*Dimension Y
   DZ := 19.0;*VAR*Dimension Z
START
Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)
CALL HH_Park ( VAL PARK:=3,X:=0,Y:=0)
[operationLines here]
```

---

#### HopAnalyzer

Validates the final `.hop` file content for SP/EP structural correctness. Wire `hopContent` from HopExport directly.

| Input | Type | Description |
|-------|------|-------------|
| `hopContent` | string | Full `.hop` file content from HopExport. |
| `run` | bool | Set True to run the analysis. |

| Output | Type | Description |
|--------|------|-------------|
| `isValid` | bool | True if no structural errors found. |
| `errorCount` | int | Total number of errors. |
| `errors` | string list | Error messages with line numbers. |
| `summary` | string | One-line summary: SP/EP counts, move count, error count. |

**Checks performed:**
- Every `SP` has a matching `EP`
- No moves (`G01`/`G02M`/`G03M`) outside an `SP/EP` block
- No empty `SP/EP` blocks
- No duplicate tool numbers (same `WZB`/`WZF`/`WZS` called twice)

---

### Cabinet (Cabinet)

High-level parametric components for generating complete furniture carcasses.

---

#### HopKorpus

Parametric cabinet body generator. Takes outer dimensions and produces all flat panels with correct joinery dimensions, optional back panel routing, shelf pin holes, connector holes, and levelling feet holes.

| Input | Type | Description |
|-------|------|-------------|
| `W` | float | Cabinet width in mm (outer). Default: 600 |
| `H` | float | Cabinet height in mm (outer). Default: 720 |
| `D` | float | Cabinet depth in mm (outer). Default: 560 |
| `t` | float | Material thickness in mm. Default: 19 |
| `type` | string | Label for the cabinet type |
| `colour` | Color | Viewport preview color |
| `back` | dict | Back panel config from HopCabinetBack (optional) |
| `connectors` | dict | Connector config from HopConnector (optional) |
| `shelves` | dict | Shelf config from HopShelves (optional) |
| `feet` | dict | Feet config from HopFeet (optional) |

| Output | Type | Description |
|--------|------|-------------|
| `Panels` | dict list | One dict per panel → wire into HopPart for nesting. |
| `AssembledBreps` | Brep list | 3D assembled model for visualization and HopDrawing. |

**Generated panels:** Bottom, Top, LeftSide, RightSide, BackPanel.

---

#### HopCabinetBack

Configures the back panel type for HopKorpus.

**Options:** Surface-mounted (screwed on), grooved (rabbet), or full inset.

---

#### HopCabinetDoor

Generates a door panel sized to the cabinet opening with configurable overlay, hinge style, and hinge side. Outputs a panel dict compatible with HopPart.

---

#### HopConnector

Configures corner connector (Rafix / Minifix / Exzenter) drilling patterns for HopKorpus. Outputs a connector config dict.

---

#### HopFeet

Configures levelling feet drilling positions for HopKorpus. Outputs a feet config dict.

---

#### HopShelves

Configures adjustable shelf pin hole rows for HopKorpus. Outputs a shelf config dict.

---

### Nesting

Components for preparing parts for OpenNest and generating per-part `.hop` files after nesting.

---

#### HopPart

Bundles a flat panel outline curve and its operation lines into a single part object for nesting.

| Input | Type | Description |
|-------|------|-------------|
| `dict` | dict | Panel dict from HopKorpus (optional). When connected, other inputs are ignored. |
| `outline` | Curve | Closed part boundary curve (manual mode). |
| `operationLines` | string list | NC macro strings (manual mode). |
| `grainAngle` | float | Grain direction angle in degrees. 0 = along X. |
| `colour` | Color | Preview color |

| Output | Type | Description |
|--------|------|-------------|
| `Part` | dict | Part object for HopSheetExport |
| `Outline` | Curve | Flat outline for OpenNest `Geo` input |

---

#### HopSheet

Extracts sheet dimensions from a curve or Brep for use with HopExport and OpenNest.

| Input | Type | Description |
|-------|------|-------------|
| `geometry` | Geometry | Closed curve or solid Brep defining the sheet plate. |

| Output | Type | Description |
|--------|------|-------------|
| `dx` | float | Sheet width (bounding box X) |
| `dy` | float | Sheet height (bounding box Y) |
| `dz` | float | Material thickness (bounding box Z) |
| `sheetCurve` | Curve | Flat rectangle at Z=0 for OpenNest `Sheets` input |

---

#### HopSheetExport

After OpenNest has placed parts on a sheet, applies nesting transforms to each part's operation lines and exports one `.hop` file per part.

| Input | Type | Description |
|-------|------|-------------|
| `Parts` | dict list | Part objects from HopPart |
| `Transforms` | Transform list | Placement transforms from OpenNest |
| `folder` | string | Output directory |
| `export` | bool | Toggle to trigger export |
| `dx`, `dy`, `dz` | float | Sheet dimensions for hop header |

---

#### HopPartExport

Exports a single part (without nesting) directly to a `.hop` file. Use when parts are machined one at a time rather than nested on a sheet.

---

#### HopNesting

Generates the nesting system block (nested sheet layout metadata) in the `.hop` header. Required when using OpenNest-based workflows.

---

### Drawing

#### HopDrawing

Generates a Rhino layout page (three-view orthographic — Top/Front/Side/Iso) with title block, outer dimensions, and material list from the assembled 3D model.

| Input | Type | Description |
|-------|------|-------------|
| `geo` | Brep list | 3D geometry from HopKorpus `AssembledBreps` |
| `parts` | dict list | Panel dicts from HopKorpus `Panels` (for material list) |
| `template` | string | Path to `.3dm` file containing title block objects |
| `project` | string | Project name for the title block |
| `drawBy` | string | Author name for the title block |
| `scale` | int | Scale denominator: 10 = 1:10, 20 = 1:20 |
| `layoutName` | string | Name of the Rhino layout page to create or update |

---

#### HopMaterialList

Extracts panel data from HopKorpus and outputs a formatted material list (part names, dimensions, quantities) as a data tree for use in layouts or export.

---

### Utility

#### HopToolDB

Loads tool definitions from a JSON tool database file. Outputs tool number, diameter, and name for use in operation components.

| Input | Type | Description |
|-------|------|-------------|
| `filePath` | string | Path to tool database JSON file. Defaults to `reference-hops/` directory. |
| `toolNr` | int | Tool number to look up |

| Output | Type | Description |
|--------|------|-------------|
| `toolNr` | int | Tool magazine number |
| `diameter` | float | Tool diameter in mm |
| `name` | string | Tool name/description |

---

#### HopLayerScan

Scans the current Rhino document layers and returns matching geometry for use in operation components. Enables layer-based workflow where drawing geometry drives machining.

| Input | Type | Description |
|-------|------|-------------|
| `layerName` | string | Layer name to scan (exact match) |
| `run` | bool | Toggle to trigger scan |

| Output | Type | Description |
|--------|------|-------------|
| `curves` | Curve list | All curves found on the specified layer |
| `points` | Point3d list | All points found on the specified layer |

---

#### HopLabel

Generates a label/tag object for use in HopDrawing layouts. Outputs formatted text with position for placement in Rhino layout space.

---

#### HopDimension

Generates dimension line markup using the `B2Punkte_V7` macro. Used for adding measurement annotations to the `.hop` file.

**NC macro:** `B2Punkte_V7(...)`

| Input | Type | Description |
|-------|------|-------------|
| `p1` | Point3d | Start point of dimension |
| `p2` | Point3d | End point of dimension |
| `offset` | float | Dimension line offset from geometry in mm |
| `toolNr` | int | Tool magazine position |

---

## AutoWire

When you drop a component onto the Grasshopper canvas, **AutoWire** automatically creates and connects sensible default input sources — sliders with min/default/max, toggles, number panels — so you can start working immediately.

**Behavior:**
- Only triggers when the component has **no existing connections** (safe on copy/paste and file reload).
- Slider positions are right-aligned to the component's left edge.
- Panels are created for text inputs.
- Boolean toggles default to `false`.

---

## Typical workflows

### Single part with multiple operations

```
[Points] → [HopDrill] ──────────────────┐
[Curve]  → [HopContour] ─────[Merge]──→ [HopExport] → part.hop
[Rect]   → [HopRectPocket] ─────────────┘
                                ↓
                          [HopAnalyzer]
```

### Full cabinet from dimensions

```
[HopCabinetBack] ─┐
[HopConnector]  ──┤
[HopShelves]    ──┼→ [HopKorpus] → Panels → [HopPart] → Outline → [OpenNest]
[HopFeet]       ──┘                       ↓                           ↓
                                     AssembledBreps → [HopDrawing]   Transforms
                                                                        ↓
                                                              [HopSheetExport] → .hop files
```

### Quick single-part export (no nesting)

```
[HopKorpus] → Panels → [HopPart] → [HopPartExport] → one .hop per panel
```

### Layer-based workflow

```
[HopLayerScan "Drill"] → points → [HopDrill] ──┐
[HopLayerScan "Cut"]   → curves → [HopContour] ─┼→ [Merge] → [HopExport]
[HopToolDB]            → toolNr →──────────────-┘
```

---

## Machine & software notes

| Field | Value |
|-------|-------|
| Machine | HOLZ-HER DYNESTIC 7535 |
| Controller | HOLZHER CAMPUS |
| CAM software | HOPS 7.7.12.80 (direkt cnc-systeme gmbh) |
| File format | `.hop` (NC-Hops Part Program) |
| Encoding | ASCII, CRLF line endings |
| Licensed 3D milling | Not available on current HOPS dongle |

**Important:** 3DMilling, Mill5Axis, and VSPMillSAxis are **not licensed** on the current HOPS dongle (ID: [REDACTED]). All operations in this plugin are 2.5D (XY movement + vertical Z plunge).

**Tool type codes:**
- `WZB` — drilling tool
- `WZF` — milling tool
- `WZS` — saw tool

Feed rates, spindle speed, and approach behavior are handled at the machine level via the tool magazine configuration. This plugin does not write feed values — only tool position number and the `_VE`, `_VA`, `_SD` placeholders that CAMPUS resolves at runtime.
