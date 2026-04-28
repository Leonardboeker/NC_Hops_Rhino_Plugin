# Machine Notes — Confirmed from Physical Inspection

**Date:** 2026-03-23
**Source:** Photos of machine and software (WhatsApp, 08:28–08:29)

---

## Machine

| Field | Value |
|-------|-------|
| **Manufacturer** | HOLZ-HER GmbH (Weinig Group) |
| **Model** | DYNESTIC **7535** |
| **Type** | 5-axis nesting CNC (flatbed gantry) |
| **Axes** | 5-axis (A ±180°, C ±360°) — mechanically present |
| **Primary application** | Panel cutting, milling, drilling (wood materials: MDF, plywood, particle board) |

---

## CAM software (PC side, not on the machine)

| Field | Value |
|-------|-------|
| **Software** | HOPS |
| **Version** | **7.7.12.80** |
| **Manufacturer** | direkt cns-systeme gmbh, Erich-Klink-Straße 11, D-73553 Alfdorf |
| **Slogan** | "cad/cam powered by innovation" |
| **Dongle ID** | [REDACTED] |

### Enabled features (dongle)

| Feature | Status |
|---------|--------|
| MVV_Simu | **Active** |
| FKM | — (not licensed) |
| MachineComp | — |
| NCKontur | — |
| VSPMillSAxis | — |
| Mill5Axis | — |
| **3DMilling** | — **(not licensed!)** |
| LeadingOutWithoutSafety | **Active** |
| APP | **Active** (licensed 2021-06-11) |
| CM | **Active** (licensed 2022-08-31) |

> **Important:** 3DMilling, Mill5Axis and VSPMillSAxis are **not** enabled on this dongle.
> That means: 3D milling paths cannot be created or simulated in HOPS.
> The machine (7535) supports 5-axis mechanically — but the HOPS licence does not cover it.
> → Phase 4 (3D Milling) of the post processor cannot be verified through HOPS.

---

## Machine controller (on the device)

| Field | Value |
|-------|-------|
| **Software** | HOLZHER CAMPUS |
| **Interface** | Touch display, Windows-based |
| **File format** | `.hop` (NC-Hops Part Program) |

### Confirmed .hop files (from the controller's file browser, photo 2)

| File name | Date |
|-----------|------|
| `05_Tisch_bohren.hop` | 2025-12-01 |
| `05_diplplatten_gesamt.hop` | 2025-01-23 |
| `kantenschnitt.hop` | 2024-04-12 |
| `Tor-Träger (r=f) Boden W2G2402.hop` | 2024-10-06 |
| `T-Träger (r=f) Boden W2G2402.hop` | 2024-02-11 |

> These files are present on the controller PC and are suitable as references for Phase 1 (format analysis).
> Copy one of these files → open in a text editor → decode the format.

---

## NC-Hops macro syntax (from the HOPS code window, photo 3)

The following macros could be read from the visible code panel in HOPS:

```
PANTEXT(...)                          — Panel labelling / label header
HK_MaxLabel_[HnodesLabelPos]_[...]    — Max-label definition (labelling)
EN_TransformMatrix()                  — Coordinate-system transform (header)
VCT(ZE_V_VA_3D_ARF:T)                 — Tool / vector definition
Format: 0,1,2,7,8,11:...              — Format specification (parameter block)
Tasche(60,280,319.6,0.5,0.5,AT,MAXDEPTH) — Pocket milling
Bohrug(19,165.5,11)                   — Drilling, x=19, y=165.5, z/depth=11
Bohrug(19,165.5,11)                   — (repeated)
VCT(D_K_HE_U_MORFL_VF...)             — further tool definition
```

### Known macros from research + photo confirmation

| Macro | Operation | Parameters (as known) |
|-------|-----------|------------------------|
| `Bohrug(x,y,depth)` | Vertical drill | x, y, depth |
| `BOHRUNG(x,y,z,depth,...)` | Vertical drill (long form) | 12 parameters (community-documented) |
| `HORZB(x,y,z,d,depth,...)` | Horizontal drill | 12 parameters |
| `Tasche(x,y,len,...)` | Pocket milling | x, y, length?, width?, depth?, flags |
| `PANTEXT(...)` | Panel labelling | — |
| `EN_TransformMatrix()` | Transformation | — |
| `VCT(...)` | Tool / path definition | Parameters unclear |

> `Bohrug` is presumably a short form of `BOHRUNG` — or a different macro. To be clarified with a sample file.

---

## Next steps (Phase 1)

1. **Copy one of the `.hop` files from the controller PC** (e.g. `kantenschnitt.hop` or `05_Tisch_bohren.hop`)
2. Open the file in a text editor → check whether it is ASCII
3. Document the full macro list + header/footer
4. Then: start Phase 2 (GHPython engine)

---

---

## Machine supplier / service partner

**SAMSTAG MaschinenTechnik** — contact for technical support, software-licence questions (HOPS dongle) and .hop format questions.
*(Source: Outlook screenshot from `Datein/` folder, 2026-03-24)*

---

## Sample file received (2026-03-24)

`Datein/Muster_DXF_Import.hop` — full sample file with all relevant operation types:
- Outer contour (SP/G01/G03M/EP)
- Vertical drills (Bohrung)
- Horizontal drills (HorzB)
- Groove (CALL _nuten_frei_v5)
- Circular pocket / circular path / rectangular pocket (CALL _*_V5)

**Phase 1 blocker resolved.** Full analysis in `.planning/research/HOP_FORMAT_DECODED.md`

---

*Created: 2026-03-23 — Source: photos of the machine and software*
*Updated: 2026-03-24 — Sample file received, format fully decoded*
