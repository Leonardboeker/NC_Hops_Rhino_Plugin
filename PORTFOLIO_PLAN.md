# Portfolio Plan — DYNESTIC Post-Processor
**Ziel:** 4–5 generierte Bilder, die den Plugin-Workflow visuell erklären.
**Stil:** Dark, industrial, technisch — passt zur Portfolio-Ästhetik (RAL 9005, Grain Overlay, Bebas Neue).
**Methode:** Nano Banana → Review → Revision in Schleife bis gut.

---

## Visueller Ablauf (Story in 4 Bildern)

```
[Rhino Geometry] → [GH Canvas + Plugin] → [.hop Code] → [CNC Maschine]
     Bild 1              Bild 2              Bild 3          Bild 4
```

Optionales 5. Bild: Hero-Compositing (alle 4 Schritte in einem Bild).

---

## Bild 1 — Rhino Geometry / Input

**Was es zeigt:** Den Ausgangspunkt — 2D-Kurven (Konturen, Bohrpunkte, Taschen) auf einem flachen Sheet im Rhino Viewport. Klar, präzise, CAD-Ästhetik.

**Nano Banana Prompt (Start):**
```
Technical 3D visualization of a CAD/CAM workflow: a flat rectangular sheet (MDF board) 
in a dark Rhino 3D viewport, showing 2D vector curves on the surface — outer contour 
cutting path in red, circular drill points in blue, rectangular pocket outlines in cyan. 
Dark background #0d0d0d, thin white grid, professional engineering aesthetic. 
Top-down perspective slightly angled. Ultra-sharp, no motion blur, photorealistic render style.
```

**Revision-Loop Fragen:**
- Sind die Kurvenfarben klar lesbar (rot/blau/cyan)?
- Wirkt es wie ein echter CAD-Viewport oder zu illustrativ?
- Ist das Sheet klar als Material erkennbar?

**Ziel-Feeling:** Wie ein Rhino-Screenshot, aber cinematisch.

---

## Bild 2 — Grasshopper Canvas / Plugin

**Was es zeigt:** Den GH-Canvas mit den Custom-Komponenten (HopSheet, HopContour, HopDrill, HopExport) — orange gefärbt (GH custom plugin style), mit Wires verbunden. Zeigt: Das ist echte Software, kein generisches Diagramm.

**Nano Banana Prompt (Start):**
```
Close-up illustration of a Grasshopper 3D visual programming canvas, dark grey background. 
Custom orange-colored plugin components labeled: "HopSheet", "HopContour", "HopDrill", 
"HopExport". Components connected with thin colored wires (orange, white, grey). 
Clean node-graph aesthetic. Small parameter sliders visible. 
Dark UI chrome, minimal typography. Technical software screenshot style, slightly stylized.
```

**Revision-Loop Fragen:**
- Sind die Komponenten-Labels lesbar?
- Wirkt der Canvas wie echte GH-Software?
- Ist die Verbindungslogik (Wires) nachvollziehbar?

**Ziel-Feeling:** Erkennbar als parametrische Software, nicht als generisches Diagramm.

---

## Bild 3 — .hop NC-Code / Output

**Was es zeigt:** Das generierte `.hop` NC-File in einem dunklen Code-Editor. Zeigt echte HOPS-Makro-Syntax (`Bohrug`, `Tasche`, `HopContour`) — erklärt: Das Plugin schreibt maschinenlesbaren Code.

**Nano Banana Prompt (Start):**
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

**Revision-Loop Fragen:**
- Ist der Code lesbar und plausibel wirkend?
- Passt das Farbschema zur Portfolio-Ästhetik?
- Wirkt es wie echte NC-Syntax, nicht wie Python/JS?

**Ziel-Feeling:** "Das Plugin schreibt echten Maschinencode" — sofort verständlich.

---

## Bild 4 — CNC Maschine / Execution

**Was es zeigt:** Eine 5-Achs Nesting-CNC (Flachbett-Gantry-Stil wie DYNESTIC) fräst MDF. Späne fliegen, Spindel im Fokus, industriell. Beweist: Das ist kein akademisches Tool, das läuft real.

**Nano Banana Prompt (Start):**
```
Industrial photograph of a large-format 5-axis CNC nesting machine (gantry style) 
cutting into an MDF sheet. Close-up of the spindle head with wood dust and chips flying. 
Dark workshop environment, dramatic single-source lighting from above. 
The machine is black powder-coated steel. Shallow depth of field, spindle in sharp focus. 
Cinematic, high-contrast, no people visible. Photorealistic.
```

**Revision-Loop Fragen:**
- Sieht die Maschine nach industrieller CNC aus (nicht Hobby-Router)?
- Wirkt der Staub/Span-Effekt realistisch?
- Passt die Beleuchtung zum dunklen Portfolio-Stil?

**Ziel-Feeling:** Schwer, industrial, real — keine Render-Klischees.

---

## Bild 5 (optional) — Pipeline Hero

**Was es zeigt:** Alle 4 Schritte in einem Bild — horizontal nebeneinander, verbunden durch Pfeile/Lichtlinien. Für das Portfolio-Hero-Bild oder als Übersichtsbild in der Detailseite.

**Nano Banana Prompt (Start):**
```
Horizontal triptych/sequence diagram on dark background showing a CNC fabrication 
digital workflow: left panel shows CAD curves on a flat sheet (Rhino viewport style), 
center panel shows a node-graph programming canvas with orange plugin components, 
right panel shows a CNC machine cutting MDF. Connected by thin glowing arrow lines. 
Cinematic, dark industrial aesthetic, ultra-wide 21:9 format, no text labels needed.
```

**Revision-Loop Fragen:**
- Sind alle 3 Panels klar voneinander getrennt aber verbunden?
- Liest sich die Pipeline von links nach rechts intuitiv?
- Ist das Gesamtbild als Hero-Banner verwendbar?

---

## Revision-Loop Protokoll

Für jedes Bild:

```
Runde 1: Prompt → Bild generieren → Review gegen Fragen oben
Runde 2: Schwächen benennen → gezielter Revision-Prompt ("make the spindle more industrial, 
         reduce the brightness, add wood chips")
Runde 3: Finales Bild → sharp-resize auf 1400px, quality 82
Runde 4: In assets/images/projects/post-processor/ speichern
```

**Abbruchkriterium pro Bild:** Nicht mehr als 5 Revisionen. Wenn nach Runde 4 noch nicht gut → anderen Prompt-Ansatz wählen (zB. illustrativ statt photorealistisch).

---

## Datei-Plan für Website_2

```
assets/images/projects/post-processor/
  hero.jpg          ← Bild 4 (CNC-Maschine) oder Bild 5 (Pipeline)
  gallery_1.jpg     ← Bild 1 (Rhino Geometry)
  gallery_2.jpg     ← Bild 2 (GH Canvas)
  gallery_3.jpg     ← Bild 3 (.hop Code)
  gallery_4.jpg     ← Bild 4 (CNC Maschine)
```

**Card für projekte.html:** Bild 4 (CNC Maschine) oder Bild 1 (Rhino + Kurven) — beides hat visuellen Impact.

---

## Projektseite — Textentwurf (DE/EN)

**Label:** `DIGITAL FABRICATION · GRASSHOPPER PLUGIN · C#`
**Titel:** `DYNESTIC POST-PROCESSOR`

**Sidebar-Infos:**
- Kategorie: Digital Fabrication Tool
- Maschine: HOLZ-HER DYNESTIC 7535
- Stack: C# · Grasshopper · Rhino 8
- Output: `.hop` NC-File (NC-Hops Format)
- Operationen: Kontur · Tasche · Bohrung · 3D-Fräsen

**Fließtext DE:**
> Der DYNESTIC Post-Processor ist ein Grasshopper-Plugin für die direkte Übersetzung von parametrischen Rhino-Geometrien in maschinenlesbare `.hop`-Dateien für die HOLZ-HER DYNESTIC 7535 — eine 5-Achs Nesting-CNC.
>
> Statt des Umwegs über proprietäre CAM-Software (RhinoCAM) entsteht der Maschinencode vollständig aus dem Grasshopper-Canvas heraus: Konturen, Taschen, Bohrungen und 3D-Fräsbahnen werden als native GH-Komponenten (HopContour, HopDrill, HopSheet, HopExport) definiert, visuell als Toolpath-Preview verifiziert und per Klick als fertige `.hop`-Datei exportiert.

---

*Erstellt: 2026-04-06 — Nächste Aktion: Nano Banana Image Loop starten*
