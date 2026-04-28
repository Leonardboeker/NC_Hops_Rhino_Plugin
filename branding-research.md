# Branding research: NC-Hops & HOLZ-HER

*Researched: 2026-04-10*

---

## NC-Hops / HOPS branding

### What is NC-HOPS?

NC-HOPS is a CAD/CAM system from **direkt cnc-systeme GmbH** (Neuenbuerg, Germany).
It is a machine-independent WOP software (workshop-oriented programming) for CNC machining centres in wood, plastics and aluminium processing. The current version HOPS 8 (predecessor: HOPS 7.7) runs on Windows and supports 3-, 4- and 5-axis machines. HOLZ-HER ships HOPS as part of the CAMPUS V7 PLUS package, but the software is developed independently by direkt cnc-systeme and is cross-vendor (Biesse, Busellato, Homag, SCM and others).

### Logo — kangaroo confirmed

**Yes, the kangaroo logo is real and confirmed.**

The official SVG logo (`Logo_Hops8_ohne-cropped.svg`) was analysed directly. It shows:
- A stylised **kangaroo** (jumping silhouette, abstracted, in a soft yellow tone)
- Below/next to it the wordmark **"NC-HOPS"** in capitals

This animal is the unmistakable trademark of NC-HOPS. The shape is drawn fluidly and organically (no simple icon), with a recognisable body, tail and jumping hind legs.

### Logo colours (extracted from SVG)

| Element | Colour | Hex |
|---|---|---|
| Kangaroo silhouette (main body) | Warm mustard yellow / cream | `#fbf69e` |
| Outline / inner detail | Mid-grey (warm) | `#706d6e` |
| Wordmark "NC-HOPS" | Dark grey (almost anthracite) | `#4f4c4c` |
| Small copyright symbol in the logo | Grey (slightly lighter) | `#656263` |

The background of the logo is transparent (no frame, no background fill).

### Typography

- **Logo typeface:** Gill Sans MT Bold (classic humanist sans-serif, slightly playful, not strict)
- **Website direkt.net:** Rajdhani Bold/Medium (headlines), Open Sans (body text)
- Rajdhani is a geometric, technical typeface with a light feel — fits the industrial CAM positioning

### Website colours (direkt.net)

| Use | Colour | Hex |
|---|---|---|
| Primary accent / buttons | Light blue / cyan | `#2ea3f2` |
| Dark blue (headlines, accent) | Navy blue | `#1f467c` |
| Secondary blue | Mid blue | `#0c71c3` |
| Highlight / accent | Turquoise cyan | `#1f6581` / `#82c0c7` |
| Backgrounds | Neutral grey | `#d8d8d8` / `#e8e8e8` |
| Text | Dark | `#333` / `#666` |

The website uses **no** orange or warm accents — pure blue-grey scheme.

### Visual style

- **Tone:** Professional-industrial, technical, factual
- **Software UI:** dark colour scheme optional (Dark Mode since HOPS 7), otherwise a bright Windows UI with blue accents
- **Logo character:** The contrast between the playful kangaroo animal and the serious CAM software context is intentional — it makes the brand memorable and likeable while staying professional
- No gradient-heavy design, no aggressive colours

---

## HOLZ-HER branding

### Company

HOLZ-HER GmbH, Nuertingen (Germany). Subsidiary of **Michael Weinig AG** (since around 2014). Manufacturer of edgebanders, CNC machining centres (including the DYNESTIC series), panel saws and storage systems.

### Logo

The HOLZ-HER logo consists of two elements:
1. **Pictorial mark:** a large **"H"** in white, embedded in an orange surface / shape (described as: white H surrounded by the characteristic HOLZ-HER orange, with black shading/fill in the inside area)
2. **Wordmark:** "HOLZ-HER" in capital letters below it

Since the rebrand (around 2011, adjusted again after the Weinig acquisition) the brand is positioned as: dynamic, future-proof, internationally readable.

### Primary colour — orange

**The HOLZ-HER signal colour is unambiguously orange.**

Extracted directly from the CSS of the Weinig/HOLZ-HER website:

```css
.holzher { background: #f60 none repeat scroll 0 0 }
.holzher:after { border-color: transparent transparent transparent #f60; }
.holzher-menu a.level-1:hover { color: #f60 }
```

| Colour | CSS shorthand | Full hex |
|---|---|---|
| HOLZ-HER orange (primary) | `#f60` | `#ff6600` |

This colour is used exclusively for HOLZ-HER as a brand — other Weinig sub-brands have different colours.

### Secondary colours (from Weinig CSS)

| Use | Hex |
|---|---|
| Dark brown-orange (shading) | `#bb4b00` |
| Dark red-orange (hover state) | `#d94d38` |
| Neutral dark grey (text) | `#2d2d2d` |
| Mid-grey | `#c5c4c4` / `#d1d1d1` |
| White | `#ffffff` |
| Black | `#000000` |

### Typography

- **Primary typeface:** **Lato** (Light, Regular, Bold) — confirmed via `@font-face` in the Weinig CSS
- Lato is a sans-serif humanist grotesque with a warm character — not too sterile, not too playful
- Second brand typeface: "Weinig" (proprietary Weinig group typeface, internal brand communication only)

### Visual tone

- **Industrial + premium modern**
- High-quality product photography (machines, workshop, detail shots)
- Clean, tidy layout without unnecessary decorative elements
- Orange as the only signal colour on otherwise white-grey-neutral backgrounds
- Feel: comparable to Festool or Trumpf — established machine builder communicating in a modern way
- Header and footer dark (charcoal/dark grey), content areas white

---

## Commonalities & patterns

| Aspect | NC-HOPS | HOLZ-HER |
|---|---|---|
| Background philosophy | Bright, clean, technical | Bright, clean, industrial |
| Type character | Humanist (Gill Sans, Open Sans) | Humanist (Lato) |
| Accent colour | Blue (`#2ea3f2`) | Orange (`#ff6600`) |
| Secondary colours | Grey, white | Grey, white, dark brown |
| Mascot character | Kangaroo (warm, friendly) | Letter "H" (strong, clear) |
| Tone | Professional but approachable | Premium-industrial |

**Connecting pattern:**
1. Both use a neutral grey-white scaffold as a base
2. Both avoid gradients and rely on flat, solid colour fields instead
3. Both communicate precision — HOLZ-HER through material strength (orange + black), NC-HOPS through factuality with a friendly animal logo
4. Humanist sans-serif typefaces in both systems — light, readable, not cold
5. The technical context (CNC, CAM, woodworking) is visible in both but not over-emphasised

---

## Recommendations for plugin branding

*Context: a Grasshopper plugin that processes NC-HOPS files (.hop) and is embedded in the HOLZ-HER / direkt-cnc ecosystem.*

### Colour strategy

The plugin sits between two worlds: the blue NC-HOPS world and the orange HOLZ-HER world. Three possible strategies:

**Option A — bridge (recommended)**

Blend both signal colours into a distinct tertiary tone:

| Colour | Hex | Use |
|---|---|---|
| Primary accent | `#e05a00` | Slightly muted orange (between HOLZ-HER orange and NC-HOPS neutral) |
| Secondary accent | `#1f6581` | The turquoise blue from NC-HOPS — connects without copying |
| Background | `#f4f4f4` | Near-white, neutral like both parent systems |
| Text / icons | `#2d2d2d` | From the HOLZ-HER palette |

Rationale: orange associates with HOLZ-HER/CNC, blue with CAM software, the combination makes the plugin recognisable as a "binder" of both worlds.

**Option B — NC-HOPS-leaning (software side)**

Use the direkt-cnc palette primarily: blue `#2ea3f2`, dark blue `#1f467c`, white background. NC-HOPS users immediately recognise it as "part of the family".

**Option C — HOLZ-HER-leaning (machine side)**

Orange `#ff6600`, black/dark grey, Lato as the typeface. Comes across as a manufacturer-official tool.

### Typography recommendation

- **Lato** (like HOLZ-HER) or **Open Sans** (like the NC-HOPS website) — both similar in character, freely available, humanist
- For icon labels and Grasshopper labelling: Regular/Medium weight, no display fonts
- Avoid Gill Sans MT (too closely tied to the NC-HOPS logo, licensed)

### Logo / icon recommendation

- **Do not use a second kangaroo** — that is NC-HOPS trademark
- Possible motif: stylised CNC travel path / toolpath in orange or blue, geometric, Grasshopper-compatible (riffing on the Rhinoceros eye or the GH kangaroo eye is risky, but a pure path gesture is safe)
- Alternative: simple monogram or typographic logo ("HOP", "HOPS-GH", "GH:NC")

### Summary

| Priority | Recommendation |
|---|---|
| Mandatory | Do not use colours identical to HOLZ-HER orange `#ff6600` or NC-HOPS blue `#2ea3f2` — a slight tonal shift signals independence |
| Sensible | Use an orange tone in the Grasshopper plugin since end users associate CNC machine programs with HOLZ-HER |
| Avoid | No kangaroo motif, no generic green or red (no association, no context) |
| Type | Lato or Open Sans, always sans-serif, technical and clean |

---

*Sources: direkt.net (CSS, SVG), weinig.com (CSS), holzherusa.com (CSS), cad4wood.eu (SVG logo), wtp.hoechsmann.com, woodworkingnetwork.com*
