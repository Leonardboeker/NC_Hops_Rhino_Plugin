# Bark Beetle — Analyse & Vergleich mit dem DYNESTIC Post-Processor Plugin

*Erstellt: 2026-04-10 — Basis: `bark-beetle-reference/` (GitHub master) + `src/DynesticPostProcessor/`*

---

## 1. Was Bark Beetle macht

Bark Beetle ist ein **Grasshopper-Plugin für allgemeine 3-Achsen-CAM**, das direkt in Rhino/GH läuft und auf ShopBot-Maschinen (G-Code oder `.sbp`-Format) ausgerichtet ist. Kernprinzip: Der User legt Geometrie auf benannte Layer (`CNC cut`, `CNC pocket`, `CNC drill`, etc.) oder verdrahtet GH-Komponenten direkt — das Plugin erzeugt Toolpath-Kurven, die ein Post-Processor-Knoten dann in Maschinencode überführt.

**Technischer Stack:**
- Reine Grasshopper-Definitionen (`.gh`-Dateien), keine `.gha`-Bibliothek
- Post-Processor-Logik ist GH-internes C#-Script-Knoten oder Python-Script-Knoten
- Layer-basierter Workflow: Python-Scripts (`BarkBeetleLayers.py`, `get_cutout_cnc.py`, etc.) schreiben Rhino-UserText auf Kurven-Objekte, GH liest diese UserText-Keys und startet den Solver neu
- Ausgabe: G-Code (`.nc`) oder ShopBot-Format (`.sbp`) — beides plain-text Koordinatenfolgen
- Streaming: automatisches Schreiben bei jeder GH-Änderung in einen konfigurierten Ordner

**Operationen:**
- Cutout (Kontur mit Innen/Außen-Detektion), Pocket (Raster X/Y), Drill, Engrave, Surface 3D Mill (Isocurven), Trochoidal HSM, Horizontal 3D Mill (Mesh), Automill (Brep-Analyse → automatische 2D+3D-Paths)
- Toolpath-Tools: Auto-Tab, Tab Maker, Safe Offset Curve, Make Pass Depths
- Info-Tools: Compile Settings, Feedrate Library, Feedrate Calculator (mit Chip-Thinning), Machining Time, Find Deepest Z
- Machine-making-Tools: Rack & Pinion, Harmonic Drive, Gear-Generatoren (nicht relevant für CAM-Zwecke)

**Keine kompilierten GH-Komponenten** — alles lebt in der `.gh`-Datei selbst (GH-Script-Knoten, Cluster). Das bedeutet: kein C#-Typ-System, keine Grasshopper-Kategorien/Toolbars, kein `GH_Component`-Subtyp-Pattern.

---

## 2. Architektur-Unterschiede

| Dimension | Bark Beetle | DYNESTIC Plugin |
|-----------|-------------|-----------------|
| **Deployment** | `.gh`-Datei (Definition), kein Build-Schritt | Kompiliertes `.gha` (C#, .NET 4.8, VS-Projekt) |
| **Zielmaschine** | ShopBot (G-Code / `.sbp`) — generisch | HOLZ-HER DYNESTIC 7535, `.hop`-Format (CAMPUS-Controller) |
| **NC-Format** | Standard G-Code: `G00`/`G01`/`G02`/`G03` + ShopBot-Makros | Proprietäres Makro-Format: `Bohrung(...)`, `SP/G01/G03M/EP`, `CALL _RechteckTasche_V5(...)` — keine Standard-G-Code-Blöcke |
| **Feed-Rate** | User-seitig: Feedrate Calculator-Komponente gibt `mm/min` aus, der Post-Processor schreibt `F`-Werte direkt in den Code | Machine-seitig: DYNESTIC-Plugin schreibt **keine** Feed-Werte. `_VE`, `_VA`, `_SD` sind Platzhalter, die CAMPUS aus dem Werkzeugmagazin auflöst |
| **Toolpath-Erzeugung** | Vollständige Geometrie-Algorithmen in GH: Kurven-Offset, Innen/Außen-Detektion, Raster-Pocket, Tabs, Pass-Depth-Generierung, 3D-Mesh-Slicing — alles in Bark Beetle selbst | Koordinaten-Übergabe an Maschinen-Makros: z.B. `_RechteckTasche_V5` übernimmt die Pocket-Strategie intern im Controller. Das Plugin liefert nur Geometrie-Parameter (Mitte, Breite, Tiefe). |
| **Werkzeug-Typen** | Ein Tool-Typ (Fräser), parametrisiert über Durchmesser + RPM + Feedrate | Drei Tool-Typen mit eigenem Werkzeugaufruf-Syntax: `WZB` (Bohrer), `WZF` (Fräser), `WZS` (Säge) — jeweils eigene Macro-Familien |
| **Datenfluss** | Kurven → Toolpath-Kurven → Post-Processor → Code | Geometrie/Punkte → `operationLines: List<string>` → `HopExport` → `.hop`-Datei |
| **Sortierung** | Keine automatische Operationsreihenfolge — User verantwortlich | `NcExport.SortOperationLines()` sortiert Block-weise: WZB → WZF → WZS (Bohrungen vor Fräsen, Fräsen vor Sägen) |
| **Validierung** | Kein dediziertes Validierungs-Werkzeug | `HopAnalyzerComponent`: prüft SP/EP-Parität, leere Blöcke, Moves außerhalb SP/EP, doppelte Tool-Nummern |
| **Nesting** | Nicht eingebaut (externe Tools) | `HopPart` + `HopSheet` + `HopSheetExport` + `HopPartExport`: Integration mit OpenNest, Transform-Anwendung auf `operationLines` |
| **Hochlevel-Parametrik** | Automill: Brep → automatisch gemischte 2D+3D-Paths | `HopKorpus`: Schrankkorpus → 5 Platten mit Joinery, Verbinder, Regal-Pins, Türen, etc. |
| **AutoWire** | Kein AutoWire-Konzept | `AutoWire.cs`: beim Drop auf Canvas werden Slider/Toggle/ValueList automatisch erzeugt und verdrahtet |
| **Preview** | Standard-GH-Preview auf Toolpath-Kurven | `PreviewHelper` + komponenteninterne `DrawViewportMeshes`/`DrawViewportWires`: 3D-Volumen-Preview (Zylinder, extrudierte Slots, Säge-Kerf-Box mit Tilt) direkt in Rhino |

**Kernunterschied in der CAM-Philosophie:**
Bark Beetle generiert die vollständige Schnittgeometrie (Raster, Offset-Kurven, Tabs) selbst und schreibt sie als Koordinatenfolge in den Code. Das DYNESTIC-Plugin delegiert die Schnittgeometrie **an den Maschinen-Controller** — es liefert nur semantische Parameter (`Mitte`, `Breite`, `Tiefe`, `Radius`) und ruft Maschinen-Subroutinen auf. Das ist grundlegend anders und nicht austauschbar.

---

## 3. Adaptierbare Ideen

### 3.1 Feedrate Calculator als eigenständige Komponente
**In Bark Beetle:** `Feedrate Calculator`-Komponente berechnet `feedrate = (chipload * flutes * RPM)` aus Material, Bit-Durchmesser, Flötenanzahl, RPM. Separat: `Feedrate Calculator for Arcs` kompensiert die Geschwindigkeitsdifferenz beim Kreisbogenfahren (innere Kante läuft langsamer als äußere).

**Warum adaptierbar:** Das DYNESTIC-Plugin schreibt aktuell keine Feed-Werte (`_VE`, `_VA` sind Platzhalter). Wenn sich das ändert (z.B. direktes Schreiben von `F`-Werten in SP-Blöcke), wäre eine `HopFeedrate`-Komponente sinnvoll. Pattern: Inputs Material + BitDiameter + Flutes + RPM + MaxStepover → Output `feedrate` als Zahl, die in `HopContour` / `HopEngraving` eingespeist wird.

**Konkrete Datei:** `Bark beetle 1.02 - CNC milling - Rhino6.gh` (interner GH-Script-Knoten, kein separater Python-Source vorhanden — Logik muss aus der `.gh`-Datei extrahiert werden).

---

### 3.2 Machining Time Estimator
**In Bark Beetle:** `Machining time`-Komponente addiert alle Toolpath-Längen und dividiert durch Feedrate → Gesamtzeit in Sekunden/Minuten.

**Warum adaptierbar:** Das DYNESTIC-Plugin hat keinerlei Zeitschätzung. Eine `HopMachiningTime`-Komponente könnte auf `hopContent` operieren: SP/EP-Blöcke parsen, G01-Distanzen und G02M/G03M-Bogenlängen aufsummieren, gegen eine grobe Feedrate-Schätzung laufen lassen. Wäre ein nützlicher Rückmeldungs-Kanal neben `HopAnalyzer`.

**Pattern aus Bark Beetle:** Input `toolpaths (curves) + feedrate` → berechnete Länge → Zeit. Im DYNESTIC-Kontext: Input `hopContent + feedrate` → Zeit. Koordinaten aus G01/G03M-Zeilen parsen ist machbar.

**Umsetzung:** Neuer Komponenten-Typ `HopMachiningTime` neben `HopAnalyzer` in `Components/Export/`. Ähnliche Grundstruktur wie `HopAnalyzerComponent.cs` (parst `hopContent`, `Run`-Toggle).

---

### 3.3 Find Deepest Z / Safety Check
**In Bark Beetle:** `Find deepest Z`-Komponente gibt den tiefsten Z-Wert über alle Toolpaths aus — Safety-Check vor dem Start.

**Warum adaptierbar:** Im DYNESTIC-Kontext: Aus den `operationLines` (vor Export) oder aus `hopContent` (nach Export) das tiefste Z aus allen `Bohrung`-, `SP`-, `Tiefe`-Parametern extrahieren und mit `DZ` (Materialdicke) vergleichen. Warnung, wenn cutZ < 0 (d.h. tiefer als die Platte). Das wäre eine sinnvolle Ergänzung für `HopAnalyzer` oder als separater Check-Output.

**Konkretes Risiko ohne dieses Feature:** `HopDrill` berechnet `cutZ = surfaceZ - depth` ohne zu prüfen, ob das Ergebnis negativ wird (tiefer als Maschinenaufspanntisch). Eine Tiefenvalidierung würde das abfangen.

**Umsetzung:** Als zusätzlicher Check im bestehenden `HopAnalyzerComponent.cs` (Z-Werte aus `Bohrung`-Zeilen und `SP`-Zeilen parsen, gegen 0 prüfen).

---

### 3.4 Automill-Konzept → HopAutomill für einfache Brep-Dissection
**In Bark Beetle:** `Automill`-Komponente analysiert einen Brep, klassifiziert Flächen nach Winkel (horizontal = Pocket/Cut, vertikal = 3D-Kontur), extrahiert automatisch Tiefenwerte, erzeugt kombinierten 2D+3D-Job ohne manuelles Layer-Setzen.

**Warum partiell adaptierbar:** Ein `HopAutomill` für das DYNESTIC-Plugin wäre eine Komponente, die einen Brep analysiert und automatisch `HopContour`- und `HopRectPocket`-Output-Lines erzeugt. Konkret: horizontale planare Flächen → `CALL _RechteckTasche_V5(...)`, vertikale Außenkanten → SP/G01/EP-Konturen. Das würde den häufigen Workflow "Brep rein, Fräsoperationen raus" deutlich vereinfachen.

**Einschränkung:** Bark Beetles Automill erzeugt Kurven-Output (generisch), DYNESTIC braucht konkrete Maschinen-Makroparameter. Die Brep-Dissection-Logik (Flächenwinkel-Analyse, Tiefenextraktion) ist jedoch direkt übertragbar. Die Ausgabe wäre dann direkt `operationLines`.

**Noch nicht im DYNESTIC-Plugin vorhanden** — kein entsprechendes Feature in `Components/Operations/`.

---

### 3.5 Make Pass Depths (Schicht-Generierung aus 3D-Kurve)
**In Bark Beetle:** `Make pass depths`-Komponente nimmt 3D-Kurven und erzeugt daraus Z-gestaffelte Kopien für Roughing-Passes bis zur Zieltiefe.

**Warum partiell adaptierbar:** Die `stepdown`-Logik ist in `HopContourComponent.cs` (Zeile 258–272) bereits inline implementiert (`int passCount = (int)Math.Ceiling(depth / stepdown)`). Bark Beetles Ansatz ist mächtiger: es decoupled die Pass-Generierung in eine separate, wiederverwendbare Komponente, sodass sie auch für 3D-Kurven aus anderen Quellen nutzbar ist.

**Adaptierungsidee:** Eine `HopPassDepths`-Utility-Komponente, die aus einer 3D-Eingabekurve + stepdown-Wert eine Liste von Z-gestaffelten Kurven erzeugt. Das wäre nützlich für `HopEngraving` mit mehreren Passes, das aktuell kein `stepdown` unterstützt (`HopEngravingComponent.cs` hat keinen Stepdown-Input).

---

### 3.6 Safe Offset Curve
**In Bark Beetle:** `Safe offset curve` erzeugt immer geschlossene, nicht-selbstschneidende Offset-Kurven — auch bei extremen Offset-Werten, bei denen `Curve.Offset()` versagt.

**Warum adaptierbar:** `HopContourComponent.cs` (Zeile 155–167) ruft `curve.Offset()` auf und fällt nur auf "center path" zurück wenn es scheitert. Bei kleinen, engen Kurven mit großem Tool-Durchmesser schlägt das fehl. Das Safe-Offset-Pattern aus Bark Beetle (Iterations-basiertes Schrumpfen + Selbstschnitt-Check) wäre robuster.

**Konkret:** In `HopContourComponent.cs`, Abschnitt 5 "GEOMETRIC PRE-OFFSET", die Fallback-Logik durch eine stabilere Offset-Implementation ersetzen.

---

### 3.7 Layer-based Workflow als optionales Input-Pattern
**In Bark Beetle:** `BarkBeetleLayers.py` legt Standard-Layer (`CNC drill`, `CNC pocket`, `CNC cut`, etc.) an. Kurven auf diesen Layern werden automatisch vom GH-Solver mit den richtigen Operationen verbunden.

**Warum adaptierbar:** Das DYNESTIC-Plugin erwartet immer explizite GH-Verbindungen. Ein optionales Layer-Scanning-Feature ("alle Kurven auf Layer `DYNESTIC_CONTOUR` → `HopContour` mit Default-Settings") würde den Einstieg erleichtern — besonders für Nutzer, die bereits Rhino-Layer-Workflows kennen.

**Umsetzung:** Entweder als separates Python-Script (analog `get_cutout_cnc.py`) oder als GH-Utility-Komponente `HopLayerScan`, die Layer-Namen als Input nimmt und Kurven-Listen ausgibt.

---

## 4. Nicht relevant

### 4.1 G-Code / ShopBot-Format
Bark Beetle schreibt Standard-G-Code (`G00`, `G01`, `G02`, `G03`, `F`, `S`) und ShopBot-Format (`.sbp`). Das DYNESTIC-Plugin schreibt `.hop`-Makroformat für CAMPUS. Die Formate sind inkompatibel — keine Code-Übernahme möglich. Die G-Code-Post-Processor-Logik aus Bark Beetles `.gh`-Scripts ist komplett irrelevant.

### 4.2 Feed-Rate-Schreibung in NC-Code
Bark Beetle schreibt `F`-Werte direkt. DYNESTIC delegiert das an die Maschinensteuerung (`_VE`, `_VA`). Solange das CAMPUS-Controller-Verhalten nicht geändert wird, ist die Feedrate-Schreiblogik aus Bark Beetle nicht übertragbar — nur das Calculator-Konzept (Abschnitt 3.1).

### 4.3 3D-Milling-Operationen (Surface 3D Mill, Horizontal 3D Mill, Trochoidal HSM)
Laut `README.md` und `BACKLOG.md`: "3DMilling, Mill5Axis, and VSPMillSAxis are **not licensed** on the current HOPS dongle (ID: 3-5709426)." Alle 3D-Fräs-Algorithmen aus Bark Beetle (Isocurven-Extraktion, Mesh-Slicing, Arc-based HSM) sind daher irrelevant — die Maschine kann diese Paths nicht ausführen, HOPS kann sie nicht simulieren.

### 4.4 Machine-Making-Tools
Rack & Pinion, Harmonic Drive, Gear-Generatoren — sind für die Holzbearbeitung/Plattenverarbeitung nicht relevant. Diese Tools dienen der Maschinengeometrie-Erstellung (für den Maschinenbau), nicht dem Betrieb.

### 4.5 OctoPrint-Integration (`Send and start gcode`)
Bark Beetle kann G-Code an einen OctoPrint-Server uploaden und den Job starten. DYNESTIC-Maschinen laufen über CAMPUS-Controller, nicht über OctoPrint — irrelevant.

### 4.6 SVG-Preview / AR-Projektion
`Preview graphic for AR projection on machine bed` erzeugt SVG für Browser-Projektion auf das Maschinenbett. Das DYNESTIC-Plugin hat 3D-Preview direkt in Rhino (`PreviewHelper.cs`). Die SVG-Ausgabe ist redundant.

### 4.7 Komplett-GH-Definition-Architektur
Bark Beetles Ansatz (alles in einer `.gh`-Datei, keine kompilierte Bibliothek) ist eine explizite Designentscheidung für maximale Editierbarkeit. Das DYNESTIC-Plugin ist bewusst als kompiliertes `.gha` gebaut (Typsicherheit, AutoWire, Icon-System, Kategorien). Ein Wechsel zurück zu GH-Script-Knoten wäre ein Rückschritt.

---

*Referenz-Dateien:*
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
