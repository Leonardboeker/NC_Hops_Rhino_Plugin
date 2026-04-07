# DYNESTIC Post-Processor

A Grasshopper plugin for generating `.hop` NC files for the **HOLZ-HER DYNESTIC 7535** CNC machine, controlled via the **HOPS 7.7** CAM software and **HOLZHER CAMPUS** machine controller.

Instead of writing NC code by hand or going through HOPS, you design parametrically in Rhino/Grasshopper, wire components together, and export production-ready `.hop` files directly.

---

## Table of Contents

- [How it works](#how-it-works)
- [Installation](#installation)
- [The .hop file format](#the-hop-file-format)
- [Component reference](#component-reference)
  - [Operations](#operations)
  - [Export](#export)
  - [Cabinet (Korpus)](#cabinet-korpus)
  - [Nesting](#nesting)
  - [Drawing](#drawing)
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

2. Copy the compiled `.gha` file from `bin/Release/` to your Grasshopper Libraries folder:  
   `%AppData%\Grasshopper\Libraries\`

3. Restart Rhino. The components appear in the **DYNESTIC** tab in the Grasshopper toolbar.

Alternatively, open the Grasshopper definition `Grasshopper Post.gh` which uses the pre-built components directly.

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
| `SP(...)` / `G01(...)` / `G03M(...)` / `EP(...)` | Contour cutting path | HopContour |
| `CALL _RechteckTasche_V5(...)` | Rectangular pocket | HopRectPocket |
| `CALL _Kreistasche_V5(...)` | Circular pocket | HopCircPocket |
| `CALL _Kreisbahn_V5(...)` | Circular path | HopCircPath |
| `CALL _nuten_frei_v5(...)` | Free slot | HopFreeSlot |
| `WZB(...)` | Drill tool call | All drill ops |
| `WZF(...)` | Milling tool call | All mill ops |

---

## Component reference

### Operations

These components produce `operationLines` — lists of NC macro strings that wire into **HopExport**.

---

#### HopDrill

Converts a list of 3D points into vertical drilling operations.

**Category:** DYNESTIC → Operations  
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
- `surfaceZ` is automatically derived as the maximum Z across all input points.
- With `stepdown > 0`, drilling is split into multiple Bohrung passes at increasing depth.
- Renders translucent drill cylinders in the Rhino viewport.

---

#### HopContour

Converts a planar curve into a 2D contour cutting path using `SP/G01/G03M/EP` macros. Handles both straight segments and arcs.

**Category:** DYNESTIC → Operations  
**NC macros:** `SP`, `G01`, `G02M`, `G03M`, `EP`

| Input | Type | Description |
|-------|------|-------------|
| `curve` | Curve | Planar closed or open curve. Must lie in or near World XY. |
| `depth` | float | Cutting depth in mm. Default: 1.0 |
| `plungeZ` | float | First-pass plunge depth override. 0 = same as depth. |
| `tolerance` | float | NURBS → polyline/arc conversion tolerance in mm. Default: 0.1 |
| `toolNr` | int | Tool magazine position (must be > 0) |
| `toolDiameter` | float | Tool diameter for kerf offset. Default: 8.0 |
| `side` | int | Kerf compensation: -1 = inside, 0 = center, +1 = outside |
| `stepdown` | float | Depth per pass for multi-pass cutting. 0 = single pass. |
| `colour` | Color | Viewport preview color |

| Output | Type | Description |
|--------|------|-------------|
| `operationLines` | string list | NC macro strings → wire into HopExport |

**Notes:**
- Internally converts each curve piece to lines and arcs using Rhino's `ToArcsAndLines`. Lines become `G01`, arcs become `G02M`/`G03M` (CW/CCW determined from arc normal). Both `PolyCurve` and single `ArcCurve`/`LineCurve` results are handled correctly.
- Kerf compensation is a **geometric pre-offset** applied to the curve before decomposition, using Rhino's `Curve.Offset`. No machine-side radius compensation (G41/G42) is used.
- With `stepdown`, multiple full contour passes are generated at increasing depths.
- Renders a shaded toolpath volume and dashed approach line in the viewport.

---

#### HopEngraving

Generates engraving paths for one or more curves using `SP/G01/G03M/EP` macros. Follows the input curve exactly — no kerf offset. Designed for shallow cuts with V-bits or engraving spindles.

**Category:** DYNESTIC → Operations  
**NC macros:** `SP`, `G01`, `G02M`, `G03M`, `EP`

| Input | Type | Description |
|-------|------|-------------|
| `curves` | Curve list | One or more planar curves to engrave. Each curve becomes one or more SP/EP blocks. |
| `depth` | float | Engraving depth in mm below the curve's Z position. Default: 0.5 |
| `tolerance` | float | NURBS → polyline/arc conversion tolerance in mm. Default: 0.05 |
| `toolNr` | int | Tool magazine position (must be > 0) |
| `colour` | Color | Viewport preview color |

| Output | Type | Description |
|--------|------|-------------|
| `operationLines` | string list | NC macro strings → wire into HopExport |

**Notes:**
- Multiple input curves each produce their own SP/EP block sequence within one `WZF` tool call.
- Internally handles both `PolyCurve` and single `ArcCurve`/`LineCurve` results from `ToArcsAndLines` — single-segment curves are not silently dropped.
- Preview renders a pipe volume along the engraving path (radius = depth, approximating the V-bit footprint at surface level).

---

#### HopRectPocket

Generates a rectangular pocket using the `_RechteckTasche_V5` macro. Dimensions are extracted from the input curve's bounding box.

**Category:** DYNESTIC → Operations  
**NC macro:** `CALL _RechteckTasche_V5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `rectCurve` | Curve | Closed rectangle curve. Center and size from bounding box. |
| `cornerRadius` | float | Fillet radius in mm. 0 = sharp corners. |
| `angle` | float | Rotation angle in degrees. 0 = axis-aligned. |
| `depth` | float | Pocket depth in mm. Default: 1.0 |
| `stepdown` | float | Depth per pass (Zustellung). 0 = single pass. |
| `toolNr` | int | Tool magazine position (must be > 0) |
| `colour` | Color | Viewport preview color |

---

#### HopCircPocket

Generates a circular pocket using the `_Kreistasche_V5` macro.

**Category:** DYNESTIC → Operations  
**NC macro:** `CALL _Kreistasche_V5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `center` | Point3d | Center of the pocket. Z = plate surface. |
| `radius` | float | Pocket radius in mm (must be > 0) |
| `depth` | float | Pocket depth in mm. Default: 1.0 |
| `stepdown` | float | Depth per pass. 0 = single pass. |
| `toolNr` | int | Tool magazine position (must be > 0) |
| `colour` | Color | Viewport preview color |

---

#### HopCircPath

Generates a circular profile cutting path using the `_Kreisbahn_V5` macro. Cuts along a circle (not a full pocket — just the path).

**Category:** DYNESTIC → Operations  
**NC macro:** `CALL _Kreisbahn_V5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `center` | Point3d | Center of the circular path. Z = plate surface. |
| `radius` | float | Path radius in mm (must be > 0) |
| `radiusCorr` | int | Radius correction: -1 = outside, 0 = center, +1 = inside |
| `depth` | float | Cut depth in mm. Default: 1.0 |
| `stepdown` | float | Depth per pass (ZuTiefe). 0 = single pass. |
| `angle` | float | Arc angle in degrees. 360 = full circle. Default: 360. |
| `toolNr` | int | Tool magazine position (must be > 0) |
| `colour` | Color | Viewport preview color |

---

#### HopFreeSlot

Generates a free slot (Nut) between two points using the `_nuten_frei_v5` macro.

**Category:** DYNESTIC → Operations  
**NC macro:** `CALL _nuten_frei_v5(...)`

| Input | Type | Description |
|-------|------|-------------|
| `p1` | Point3d | Slot start point |
| `p2` | Point3d | Slot end point |
| `slotWidth` | float | Slot width in mm (must be > 0) |
| `depth` | float | Slot depth in mm. Default: 1.0 |
| `toolNr` | int | Tool magazine position (must be > 0) |
| `colour` | Color | Viewport preview color |

**Notes:**
- `surfaceZ` = max Z of p1 and p2.
- Renders the slot as a swept box volume in the viewport.

---

### Export

#### HopExport

Assembles all operation lines into a complete `.hop` file and writes it to disk.

**Category:** DYNESTIC → Export

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
| `hopContent` | string | Full file content as string (for inspection in a panel) |
| `statusMsg` | string | Export status message with file path |

**Notes:**
- Multiple operation components can be merged using a standard Grasshopper **Merge** component before wiring into `operationLines`.
- `.hop` extension is added automatically — no need to include it in `fileName`.
- File is written with **ASCII encoding** and **CRLF line endings** (required by the CAMPUS controller).
- The export guard (`export = false`) means the component is completely silent until you toggle it — no accidental file writes.
- Operations are automatically sorted before writing: **WZB → WZF → WZS → rest**. Sorting is block-based — each tool call and all of its following SP/EP/G01/G03M lines stay together as a unit. This ensures drill operations run before milling, and milling before sawing, without breaking any SP/EP structure.

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

Validates the final `.hop` file content for SP/EP structural correctness. Wire `HopContent` from HopExport directly — the check runs on the fully sorted and assembled output, not on raw operation lines.

**Category:** DYNESTIC → Export

| Input | Type | Description |
|-------|------|-------------|
| `hopContent` | string | Full `.hop` file content. Wire from HopExport's `hopContent` output. |
| `run` | bool | Set True to run the analysis. Default: false. |

| Output | Type | Description |
|--------|------|-------------|
| `isValid` | bool | True if no structural errors were found. |
| `errorCount` | int | Total number of errors. |
| `errors` | string list | Error messages with line numbers. |
| `summary` | string | One-line summary: SP/EP counts, move count, error count. |

**Checks performed:**
- Every `SP` has a matching `EP`
- No `G01`/`G02M`/`G03M` moves appear outside an `SP/EP` block
- No empty `SP/EP` blocks (SP immediately followed by EP with no moves)
- No duplicate tool numbers (same `WZB`/`WZF`/`WZS` tool number called twice)

---

### Cabinet (Korpus)

High-level parametric components for generating complete furniture carcasses.

---

#### HopKorpus

Parametric cabinet body generator. Takes outer dimensions and produces all flat panels with correct joinery dimensions, optional back panel routing, shelf pin holes, connector holes, and levelling feet holes.

**Category:** DYNESTIC → Cabinet

| Input | Type | Description |
|-------|------|-------------|
| `W` | float | Cabinet width in mm (outer). Default: 600 |
| `H` | float | Cabinet height in mm (outer). Default: 720 |
| `D` | float | Cabinet depth in mm (outer). Default: 560 |
| `t` | float | Material thickness in mm. Default: 19 |
| `type` | string | Label for the cabinet type (no structural effect) |
| `colour` | Color | Viewport preview color |
| `back` | dict | Back panel config from HopCabinetBack (optional) |
| `connectors` | dict | Connector config from HopConnector (optional) |
| `shelves` | dict | Shelf config from HopShelves (optional) |
| `feet` | dict | Feet config from HopFeet (optional) |

| Output | Type | Description |
|--------|------|-------------|
| `Panels` | dict list | One dictionary per panel. Wire into HopPart for nesting. |
| `AssembledBreps` | Brep list | 3D assembled model for visualization and HopDrawing. |

**Generated panels:** Bottom, Top, LeftSide, RightSide, BackPanel — each with their correct finished dimensions after accounting for material thickness overlaps.

---

#### HopCabinetBack

Configures the back panel type for HopKorpus.

**Options:** Surface-mounted (screwed on), grooved (Falznut routed into sides), or full inset.

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

**Category:** DYNESTIC → Nesting

| Input | Type | Description |
|-------|------|-------------|
| `dict` | dict | Panel dict from HopKorpus `Panels` output. When connected, other inputs are ignored. |
| `outline` | Curve | Closed part boundary curve (manual mode). |
| `operationLines` | string list | NC macro strings (manual mode). |
| `grainAngle` | float | Grain direction angle in degrees. 0 = along X. |
| `colour` | Color | Preview color for the outline. |

| Output | Type | Description |
|--------|------|-------------|
| `Part` | dict | Part object for HopSheetExport |
| `Outline` | Curve | Flat outline for OpenNest `Geo` input |

**Notes:**
- In **HopKorpus mode** (dict connected): all geometry and operations come from the panel dictionary.
- In **manual mode**: connect any outline curve and operation lines from individual operation components.
- Renders the outline and a grain direction arrow in the viewport.

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

After OpenNest has placed parts on a sheet, applies the nesting transformations to each part's operation lines and exports one `.hop` file per part.

**Category:** DYNESTIC → Nesting

| Input | Type | Description |
|-------|------|-------------|
| `Parts` | dict list | Part objects from HopPart |
| `Transforms` | Transform list | Placement transforms from OpenNest |
| `folder` | string | Output directory |
| `export` | bool | Toggle to trigger export |
| `dx`, `dy`, `dz` | float | Sheet dimensions for the hop header |

---

#### HopPartExport

Exports a single part (without nesting) directly to a `.hop` file. Use when parts are machined one at a time rather than nested on a sheet.

---

### Drawing

#### HopDrawing

Generates a Rhino layout page (Dreitafelansicht — Top/Front/Side/Iso views) with title block, outer dimensions, and material list from the assembled 3D model.

**Category:** DYNESTIC → Drawing

| Input | Type | Description |
|-------|------|-------------|
| `geo` | Brep list | 3D geometry — wire from HopKorpus `AssembledBreps` |
| `parts` | dict list | Panel dicts from HopKorpus `Panels` (for material list) |
| `template` | string | Path to `.3dm` file containing title block objects |
| `project` | string | Project name for the title block |
| `drawBy` | string | Author name for the title block |
| `scale` | int | Scale denominator: 10 = 1:10, 20 = 1:20 |
| `layoutName` | string | Name of the Rhino layout page to create or update |

**Notes:**
- Creates or updates a named layout page directly in the active Rhino document.
- Imports title block objects from the template `.3dm` file into layout space.
- Automatically calculates view extents and positions viewports.

#### HopMaterialList

Extracts panel data from HopKorpus and outputs a formatted material list (part names, dimensions, quantities) as a data tree for use in layouts or export.

---

## AutoWire

When you drop a component onto the Grasshopper canvas, **AutoWire** automatically creates and connects sensible default input sources — sliders with min/default/max, toggles, number panels — so you can start working immediately without manually adding and connecting each input.

**Behavior:**
- Only triggers when the component has **no existing connections** (safe on copy/paste and file reload).
- Slider positions are right-aligned to the component's left edge, each pinned to its input's Y position.
- Panels are created for text inputs (you fill the value yourself).
- Boolean toggles default to `false`.

This is implemented in `AutoWire.cs` and called from each component's `AddedToDocument` override.

---

## Typical workflows

### Single part with multiple operations

```
[Points] → [HopDrill] ──────────────────┐
[Curve]  → [HopContour] ─────[Merge]──→ [HopExport] → part.hop
[Rect]   → [HopRectPocket] ─────────────┘
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

---

## Machine & software notes

| Field | Value |
|-------|-------|
| Machine | HOLZ-HER DYNESTIC 7535 |
| Controller | HOLZHER CAMPUS |
| CAM software | HOPS 7.7.12.80 (direkt cns-systeme gmbh) |
| File format | `.hop` (NC-Hops Part Program) |
| Encoding | ASCII, CRLF line endings |
| Licensed 3D milling | Not available on current HOPS dongle |

**Important:** 3DMilling, Mill5Axis, and VSPMillSAxis are **not licensed** on the current HOPS dongle (ID: 3-5709426). The machine (7535) supports 5-axis mechanically, but HOPS cannot simulate or verify 5-axis paths. All operations in this plugin are 2.5D (XY movement + vertical Z plunge).

**Tool type codes:**
- `WZB` — drilling tool (Bohrwerkzeug)
- `WZF` — milling tool (Fräswerkzeug)

Feed rates, spindle speed, and approach behavior are handled at the machine level via the tool magazine configuration. This plugin does not write feed values — only tool position number and the `_VE`, `_VA`, `_SD` placeholders that CAMPUS resolves at runtime.
