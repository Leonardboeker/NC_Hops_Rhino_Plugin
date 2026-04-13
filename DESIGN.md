# Plugin Design & Branding

*Stand: 2026-04-13*

---

## Name

### Entscheidung: **Wallaby Hop**

**Wallaby Hop** verbindet zwei Bedeutungsebenen:
- **Wallaby** — ein Beuteltier, kleiner Cousin des NC-HOPS-Kängurus. Eigenständiges Tier, kein Markenschutzkonflikt. Springt präzise und wendig (wie kurze CNC-Verfahrbewegungen = "Hops").
- **Hop** — direkter Verweis auf das `.hop`-Dateiformat von NC-HOPS.

Das Wallaby ist bewusst *nicht* das Känguru — es ist kleiner, agiler, und damit passend für ein Plugin das mit Feinheit Werkzeugpfade übersetzen.

| Name | Verfügbar | Bewertung |
|------|-----------|-----------|
| **Wallaby Hop** | ja | Tier + Format in einem, kein Konflikt mit NC-HOPS-Känguru |
| ~~Horntail~~ | ja | Vorherige Empfehlung, ersetzt |
| ~~Bark Beetle~~ | **vergeben** | GitHub, CNC-Toolpath-Generator |
| ~~Locust~~ | **vergeben** | CERVER Tools |
| ~~Termite~~ | **vergeben** | 3D-Druck Clay-Plugin |
| ~~Silkworm~~ | **vergeben** | 3D-Druck GCode |
| ~~Woodpecker~~ | **vergeben** | Holzbau BTL/BTLX Export |

---

## Ökosystem-Kontext

Das Plugin sitzt zwischen zwei bestehenden Markenwelten:

| | NC-HOPS (direkt cnc-systeme) | HOLZ-HER |
|---|---|---|
| Logo-Tier | Känguru (springend, senfgelb) | "H"-Buchstabe auf Orange |
| Primärfarbe | Blau `#2ea3f2` (Website) / Gelb `#fbf69e` (Logo) | Orange `#ff6600` |
| Schrift | Gill Sans MT Bold (Logo), Rajdhani + Open Sans (Web) | Lato |
| Ton | Professionell, technisch, sympathisch | Premium-industriell |

**Wichtig:** Kein Känguru verwenden — ist NC-HOPS-Markenschutz. Ein **Wallaby** ist ein anderes Tier und explizit kein Konflikt — kleinere Silhouette, andere Körperproportionen (kürzere Ohren, kompakterer Körper, kürzerer Schwanz).

---

## Farbpalette

Strategie: Orange von HOLZ-HER leicht gedämpft, um Eigenständigkeit zu zeigen aber die Maschinenwelt zu spiegeln.

| Rolle | Farbe | Hex |
|-------|-------|-----|
| Primär (Plugin-Farbe) | Gedämpftes CNC-Orange | `#e05a00` |
| Sekundär | Anthrazit | `#2d2d2d` |
| Akzent | Naturholz / Amber | `#c4a060` |
| Hintergrund | Warmweiß | `#f5f0eb` |

Nicht verwenden: NC-HOPS-Gelb `#fbf69e` (zu nah an Känguru-Logo), HOLZ-HER-Orange `#ff6600` 1:1 (zu nah an Hersteller-Branding).

---

## Typografie

- **Lato** (Regular/Medium/Bold) — wie HOLZ-HER, kostenlos, humanistisch
- Alternativ: **Open Sans** — wie NC-HOPS Website
- Keine Display-Schriften, keine Gill Sans (lizenzpflichtig, zu sehr NC-HOPS)

---

## Icon-Konzept

### Größe

| Auflösung | Verwendung |
|-----------|-----------|
| **24×24px** | Standard GH-Render (1×) |
| **48×48px** | Empfohlen als Master — GH skaliert intern auf 24px, aber HiDPI/Retina zeigt die volle Auflösung. Kein Nachteil beim Bereitstellen der größeren Version. |

Forum-Quellen: grasshopper3d.com/forum/topics/about-component-icons, /icon-size-in-gh-1-0-0004-v6-beta
→ **Fazit: Icons als 48×48px PNG erstellen, aber pixelgenau auf 24px-Raster ausrichten.**

### Stil

Alle Icons: **Wallaby-Figur + Werkzeug-Geste** — das Tier führt die Operation aus, ist aber nicht der Fokus. Werkzeug / Geometrie dominiert das Icon, Wallaby-Silhouette ist erkennbar aber klein.

Farbige Hintergrundfläche (gedämpftes Orange `#e05a00`), weiße Symbolik darauf.

### Plugin-Haupticon

**Wallaby Hop — Masterbrand:** Wallaby-Silhouette in Seitenansicht, springend. Hält in den Vorderpfoten eine stilisierte Frässpindel (Bohrfutter + Schaft, top-down). Hintergrund: `#e05a00`. Klar lesbar als "CNC + springendes Tier".

### Komponenten-Icons nach Kategorie

| Komponente | Icon-Idee | Wallaby-Geste |
|-----------|-----------|---------------|
| `HopDrill` | Bohrer von oben (Spiralbohrer-Symbol) | Wallaby hält Bohrer senkrecht nach unten |
| `HopContour` | Kurvenpfad mit Richtungspfeil | Wallaby läuft / springt entlang einer Kurve |
| `HopPocket` | Rechteck mit Schraffur (Tasche) | Wallaby steht in/über einer Vertiefung, Fräser rein |
| `HopExport` | Pfeil nach rechts aus Block | Wallaby trägt Chip/Datei-Symbol weg |
| `HopNesting` | Verschachtelte Rechtecke | Wallaby mit Beutel (Marsupium) = Teile eingebettet |
| `HopAnalyzer` | Lupe / Checkmark | Wallaby mit Lupe über Werkstück |
| `HopKorpus` | Schrank-Silhouette | Wallaby baut/hält Korpus-Rahmen |

### Konstruktionshinweise für 48×48px

- Wallaby-Silhouette: max. 12–14px breit, oben-rechts oder oben-links positioniert (Werkzeug dominiert Mitte)
- Linien: 2px oder 4px (entspricht 1px / 2px bei 24px-Darstellung)
- Keine diagonalen Linien mit <15° oder >75° zur Horizontalen (Anti-Aliasing-Artefakte bei 24px)
- Werkzeug-Symbol: mittig oder unten-zentriert, größte Fläche des Icons

---

## Quellen

- Vollständige Branding-Analyse: `branding-research.md`
- Bark-Beetle-Vergleich: `bark-beetle-analysis.md`
- Icon-Größe GH Forum: grasshopper3d.com/forum/topics/about-component-icons
- Icon-Größe GH v6 Beta: grasshopper3d.com/forum/topics/icon-size-in-gh-1-0-0004-v6-beta
