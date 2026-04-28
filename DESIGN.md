# Plugin Design & Branding

*As of: 2026-04-13*

---

## Name

### Decision: **Wallaby Hop**

**Wallaby Hop** combines two layers of meaning:
- **Wallaby** — a marsupial, smaller cousin of the NC-HOPS kangaroo. A distinct animal, no trademark conflict. Hops precisely and nimbly (like short CNC travel moves = "hops").
- **Hop** — a direct reference to the `.hop` file format of NC-HOPS.

The wallaby is deliberately *not* the kangaroo — it is smaller, more agile, and therefore fitting for a plugin that translates toolpaths with finesse.

| Name | Available | Rating |
|------|-----------|--------|
| **Wallaby Hop** | yes | Animal + format in one, no conflict with the NC-HOPS kangaroo |
| ~~Horntail~~ | yes | Previous recommendation, replaced |
| ~~Bark Beetle~~ | **taken** | GitHub, CNC toolpath generator |
| ~~Locust~~ | **taken** | CERVER Tools |
| ~~Termite~~ | **taken** | 3D-print clay plugin |
| ~~Silkworm~~ | **taken** | 3D-print GCode |
| ~~Woodpecker~~ | **taken** | Timber construction BTL/BTLX export |

---

## Ecosystem context

The plugin sits between two existing brand worlds:

| | NC-HOPS (direkt cnc-systeme) | HOLZ-HER |
|---|---|---|
| Logo animal | Kangaroo (jumping, mustard yellow) | "H" letter on orange |
| Primary colour | Blue `#2ea3f2` (website) / Yellow `#fbf69e` (logo) | Orange `#ff6600` |
| Typeface | Gill Sans MT Bold (logo), Rajdhani + Open Sans (web) | Lato |
| Tone | Professional, technical, friendly | Premium industrial |

**Important:** Do not use a kangaroo — that is NC-HOPS trademarked. A **wallaby** is a different animal and explicitly not a conflict — smaller silhouette, different body proportions (shorter ears, more compact body, shorter tail).

---

## Colour palette

Strategy: tone HOLZ-HER's orange down slightly to show independence while still mirroring the machine world.

| Role | Colour | Hex |
|------|--------|-----|
| Primary (plugin colour) | Muted CNC orange | `#e05a00` |
| Secondary | Anthracite | `#2d2d2d` |
| Accent | Natural wood / amber | `#c4a060` |
| Background | Warm white | `#f5f0eb` |

Do not use: NC-HOPS yellow `#fbf69e` (too close to the kangaroo logo), HOLZ-HER orange `#ff6600` 1:1 (too close to manufacturer branding).

---

## Typography

- **Lato** (Regular/Medium/Bold) — like HOLZ-HER, free, humanist
- Alternative: **Open Sans** — like the NC-HOPS website
- No display fonts, no Gill Sans (licensed, too closely tied to NC-HOPS)

---

## Icon concept

### Size

| Resolution | Use |
|------------|-----|
| **24×24px** | Standard GH render (1×) |
| **48×48px** | Recommended as master — GH scales internally to 24px, but HiDPI/Retina shows the full resolution. No drawback to providing the larger version. |

Forum sources: grasshopper3d.com/forum/topics/about-component-icons, /icon-size-in-gh-1-0-0004-v6-beta
→ **Conclusion: create icons as 48×48px PNG, but align pixel-perfectly to a 24px grid.**

### Style

All icons: **wallaby figure + tool gesture** — the animal performs the operation but is not the focus. The tool / geometry dominates the icon, the wallaby silhouette is recognisable but small.

Coloured background (muted orange `#e05a00`), white symbol on top.

### Main plugin icon

**Wallaby Hop — masterbrand:** wallaby silhouette in side view, jumping. Holds a stylised milling spindle (chuck + shaft, top-down) in its forepaws. Background: `#e05a00`. Clearly readable as "CNC + jumping animal".

### Component icons by category

| Component | Icon idea | Wallaby gesture |
|-----------|-----------|-----------------|
| `HopDrill` | Drill from above (twist drill symbol) | Wallaby holds drill vertically downward |
| `HopContour` | Curve path with direction arrow | Wallaby runs / hops along a curve |
| `HopPocket` | Rectangle with hatching (pocket) | Wallaby stands in/over a recess, cutter going in |
| `HopExport` | Arrow pointing right out of a block | Wallaby carries chip / file symbol away |
| `HopNesting` | Nested rectangles | Wallaby with pouch (marsupium) = parts embedded |
| `HopAnalyzer` | Magnifier / checkmark | Wallaby with magnifier over workpiece |
| `HopKorpus` | Cabinet silhouette | Wallaby builds/holds cabinet frame |

### Construction hints for 48×48px

- Wallaby silhouette: max. 12–14px wide, positioned top-right or top-left (the tool dominates the centre)
- Lines: 2px or 4px (corresponds to 1px / 2px at 24px display)
- No diagonal lines below 15° or above 75° from the horizontal (anti-aliasing artefacts at 24px)
- Tool symbol: centred or bottom-centred, takes the largest area of the icon

---

## Sources

- Full branding analysis: `branding-research.md`
- Bark-beetle comparison: `bark-beetle-analysis.md`
- Icon size GH forum: grasshopper3d.com/forum/topics/about-component-icons
- Icon size GH v6 beta: grasshopper3d.com/forum/topics/icon-size-in-gh-1-0-0004-v6-beta
