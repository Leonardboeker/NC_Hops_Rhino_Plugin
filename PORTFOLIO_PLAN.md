# Portfolio Plan — DYNESTIC Post-Processor
**Goal:** 4–5 generated images that explain the plugin workflow visually.
**Style:** Dark, industrial, technical — fits the portfolio aesthetic (RAL 9005, grain overlay, Bebas Neue).
**Method:** Nano Banana → review → revision in a loop until good.

---

## Visual flow (story in 4 images)

```
[Rhino Geometry] → [GH Canvas + Plugin] → [.hop Code] → [CNC Machine]
     Image 1            Image 2              Image 3        Image 4
```

Optional 5th image: hero compositing (all 4 steps in one image).

---

## Image 1 — Rhino Geometry / Input

**What it shows:** the starting point — 2D curves (contours, drill points, pockets) on a flat sheet in the Rhino viewport. Clear, precise, CAD aesthetic.

**Nano Banana prompt (start):**
```
Technical 3D visualization of a CAD/CAM workflow: a flat rectangular sheet (MDF board) 
in a dark Rhino 3D viewport, showing 2D vector curves on the surface — outer contour 
cutting path in red, circular drill points in blue, rectangular pocket outlines in cyan. 
Dark background #0d0d0d, thin white grid, professional engineering aesthetic. 
Top-down perspective slightly angled. Ultra-sharp, no motion blur, photorealistic render style.
```

**Revision-loop questions:**
- Are the curve colours clearly readable (red/blue/cyan)?
- Does it look like a real CAD viewport or too illustrative?
- Is the sheet clearly recognisable as material?

**Target feel:** Like a Rhino screenshot, but cinematic.

---

## Image 2 — Grasshopper Canvas / Plugin

**What it shows:** the GH canvas with the custom components (HopSheet, HopContour, HopDrill, HopExport) — coloured orange (GH custom plugin style), connected with wires. Conveys: this is real software, not a generic diagram.

**Nano Banana prompt (start):**
```
Close-up illustration of a Grasshopper 3D visual programming canvas, dark grey background. 
Custom orange-colored plugin components labeled: "HopSheet", "HopContour", "HopDrill", 
"HopExport". Components connected with thin colored wires (orange, white, grey). 
Clean node-graph aesthetic. Small parameter sliders visible. 
Dark UI chrome, minimal typography. Technical software screenshot style, slightly stylized.
```

**Revision-loop questions:**
- Are the component labels readable?
- Does the canvas look like real GH software?
- Is the connection logic (wires) understandable?

**Target feel:** Recognisable as parametric software, not a generic diagram.

---

## Image 3 — .hop NC code / output

**What it shows:** the generated `.hop` NC file in a dark code editor. Shows real HOPS macro syntax (`Bohrug`, `Tasche`, `HopContour`) — explains: the plugin writes machine-readable code.

**Nano Banana prompt (start):**
```
Dark code editor screenshot showing CNC machine NC programming code. 
Black background, monospace font. Code lines include:
"PANTEXT(Platte_01)"
"Bohrung(120, 85, 11)"
"Tasche(60, 280, 319.6, 0.5, AT, MAXDEPTH)"
"EN_TransformMatrix()"
Syntax highlighting: keywords in orange, numbers in cyan, comments in grey.
Clean terminal aesthetic, slightly glowing text, professional dark IDE style.
No extra UI chrome — just the code, full bleed.
```

**Revision-loop questions:**
- Is the code readable and plausible-looking?
- Does the colour scheme fit the portfolio aesthetic?
- Does it look like real NC syntax, not Python/JS?

**Target feel:** "The plugin writes real machine code" — immediately understandable.

---

## Image 4 — CNC machine / execution

**What it shows:** a 5-axis nesting CNC (flatbed gantry style like the DYNESTIC) milling MDF. Chips flying, spindle in focus, industrial. Proves: this is not an academic tool, it actually runs.

**Nano Banana prompt (start):**
```
Industrial photograph of a large-format 5-axis CNC nesting machine (gantry style) 
cutting into an MDF sheet. Close-up of the spindle head with wood dust and chips flying. 
Dark workshop environment, dramatic single-source lighting from above. 
The machine is black powder-coated steel. Shallow depth of field, spindle in sharp focus. 
Cinematic, high-contrast, no people visible. Photorealistic.
```

**Revision-loop questions:**
- Does the machine look like an industrial CNC (not a hobby router)?
- Does the dust/chip effect look realistic?
- Does the lighting fit the dark portfolio style?

**Target feel:** Heavy, industrial, real — no render clichés.

---

## Image 5 (optional) — Pipeline hero

**What it shows:** all 4 steps in one image — horizontal side-by-side, connected by arrows / light lines. For the portfolio hero image or as an overview image on the detail page.

**Nano Banana prompt (start):**
```
Horizontal triptych/sequence diagram on dark background showing a CNC fabrication 
digital workflow: left panel shows CAD curves on a flat sheet (Rhino viewport style), 
center panel shows a node-graph programming canvas with orange plugin components, 
right panel shows a CNC machine cutting MDF. Connected by thin glowing arrow lines. 
Cinematic, dark industrial aesthetic, ultra-wide 21:9 format, no text labels needed.
```

**Revision-loop questions:**
- Are all 3 panels clearly separated yet connected?
- Does the pipeline read intuitively from left to right?
- Is the overall image usable as a hero banner?

---

## Revision-loop protocol

For each image:

```
Round 1: prompt → generate image → review against questions above
Round 2: name the weaknesses → targeted revision prompt ("make the spindle more industrial, 
         reduce the brightness, add wood chips")
Round 3: final image → sharp-resize to 1400px, quality 82
Round 4: save into assets/images/projects/post-processor/
```

**Stop criterion per image:** no more than 5 revisions. If still not good after round 4 → switch to a different prompt approach (e.g. illustrative instead of photorealistic).

---

## File plan for Website_2

```
assets/images/projects/post-processor/
  hero.jpg          ← Image 4 (CNC machine) or Image 5 (pipeline)
  gallery_1.jpg     ← Image 1 (Rhino geometry)
  gallery_2.jpg     ← Image 2 (GH canvas)
  gallery_3.jpg     ← Image 3 (.hop code)
  gallery_4.jpg     ← Image 4 (CNC machine)
```

**Card for projekte.html:** Image 4 (CNC machine) or Image 1 (Rhino + curves) — both have visual impact.

---

## Project page — text draft (DE/EN)

**Label:** `DIGITAL FABRICATION · GRASSHOPPER PLUGIN · C#`
**Title:** `DYNESTIC POST-PROCESSOR`

**Sidebar info:**
- Category: Digital Fabrication Tool
- Machine: HOLZ-HER DYNESTIC 7535
- Stack: C# · Grasshopper · Rhino 8
- Output: `.hop` NC file (NC-Hops format)
- Operations: Contour · Pocket · Drill · 3D milling

**Body text EN:**
> The DYNESTIC Post-Processor is a Grasshopper plugin for translating parametric Rhino geometry directly into machine-readable `.hop` files for the HOLZ-HER DYNESTIC 7535 — a 5-axis nesting CNC.
>
> Instead of the detour via proprietary CAM software (RhinoCAM), the machine code is generated entirely from the Grasshopper canvas: contours, pockets, drills and 3D milling paths are defined as native GH components (HopContour, HopDrill, HopSheet, HopExport), verified visually as a toolpath preview, and exported as a finished `.hop` file at the click of a button.

---

*Created: 2026-04-06 — Next action: start the Nano Banana image loop*
