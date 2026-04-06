# Machine Notes — Confirmed from Physical Inspection

**Date:** 2026-03-23
**Source:** Photos of machine and software (WhatsApp, 08:28–08:29)

---

## Machine

| Field | Value |
|-------|-------|
| **Hersteller** | HOLZ-HER GmbH (Weinig Group) |
| **Modell** | DYNESTIC **7535** |
| **Typ** | 5-Achs Nesting-CNC (Flachbett-Gantry) |
| **Achsen** | 5-Achs (A ±180°, C ±360°) — mechanisch vorhanden |
| **Primäranwendung** | Plattenzuschnitt, Fräsen, Bohren (Holzwerkstoffe: MDF, Sperrholz, Spanplatte) |

---

## CAM-Software (PC-seitig, nicht am Gerät)

| Field | Value |
|-------|-------|
| **Software** | HOPS |
| **Version** | **7.7.12.80** |
| **Hersteller** | direkt cns-systeme gmbh, Erich-Klink-Straße 11, D-73553 Alfdorf |
| **Slogan** | "cad/cam powered by innovation" |
| **Dongle-ID** | [REDACTED] |

### Freigeschaltete Funktionen (Dongle)

| Funktion | Status |
|----------|--------|
| MVV_Simu | **Aktiv** |
| FKM | — (nicht lizenziert) |
| MachineComp | — |
| NCKontur | — |
| VSPMillSAxis | — |
| Mill5Axis | — |
| **3DMilling** | — **(nicht lizenziert!)** |
| LeadingOutWithoutSafety | **Aktiv** |
| APP | **Aktiv** (lizenziert 11.6.2021) |
| CM | **Aktiv** (lizenziert 31.8.2022) |

> **Wichtig:** 3DMilling, Mill5Axis und VSPMillSAxis sind auf diesem Dongle **nicht** freigeschaltet.
> Das bedeutet: 3D-Fräspfade können in HOPS nicht erstellt oder simuliert werden.
> Die Maschine (7535) unterstützt 5-Achs mechanisch — aber die HOPS-Lizenz deckt das nicht ab.
> → Phase 4 (3D Milling) des Post-Prozessors kann nicht über HOPS verifiziert werden.

---

## Maschinensteuerung (am Gerät)

| Field | Value |
|-------|-------|
| **Software** | HOLZHER CAMPUS |
| **Interface** | Touch-Display, Windows-basiert |
| **Dateiformat** | `.hop` (NC-Hops Part Program) |

### Bestätigte .hop-Dateien (vom Datei-Browser des Controllers, Foto 2)

| Dateiname | Datum |
|-----------|-------|
| `05_Tisch_bohren.hop` | 01.12.2025 |
| `05_diplplatten_gesamt.hop` | 23.01.2025 |
| `kantenschnitt.hop` | 12.04.2024 |
| `Tor-Träger (r=f) Boden W2G2402.hop` | 06.10.2024 |
| `T-Träger (r=f) Boden W2G2402.hop` | 11.02.2024 |

> Diese Dateien sind auf dem Controller-PC vorhanden und eignen sich als Referenz für Phase 1 (Format-Analyse).
> Eine dieser Dateien kopieren → Texteditor öffnen → Format dekodieren.

---

## NC-Hops Makro-Syntax (aus HOPS Code-Fenster, Foto 3)

Aus dem sichtbaren Code-Panel in HOPS konnten folgende Makros abgelesen werden:

```
PANTEXT(...)                          — Panel-Beschriftung / Label-Header
HK_MaxLabel_[HnodesLabelPos]_[...]    — Max-Label-Definition (Labeling)
EN_TransformMatrix()                  — Koordinatensystem-Transformation (Header)
VCT(ZE_V_VA_3D_ARF:T)               — Werkzeug-/Vektordefinition
Format: 0,1,2,7,8,11:...             — Formatangabe (Parameterblock)
Tasche(60,280,319.6,0.5,0.5,AT,MAXDEPTH) — Taschenfräsung (Pocket)
Bohrug(19,165.5,11)                  — Bohrung (Drilling), x=19, y=165.5, z/depth=11
Bohrug(19,165.5,11)                  — (wiederholt)
VCT(D_K_HE_U_MORFL_VF...)           — weitere Werkzeugdefinition
```

### Bekannte Makros aus Recherche + Foto-Bestätigung

| Makro | Operation | Parameter (soweit bekannt) |
|-------|-----------|---------------------------|
| `Bohrug(x,y,depth)` | Vertikale Bohrung | x, y, Tiefe |
| `BOHRUNG(x,y,z,depth,...)` | Vertikale Bohrung (Langform) | 12 Parameter (community-dokumentiert) |
| `HORZB(x,y,z,d,depth,...)` | Horizontalbohrung | 12 Parameter |
| `Tasche(x,y,len,...)` | Taschenfrässung | x, y, Länge?, Breite?, Tiefe?, Flags |
| `PANTEXT(...)` | Panel-Beschriftung | — |
| `EN_TransformMatrix()` | Transformation | — |
| `VCT(...)` | Werkzeug-/Pfaddefinition | Parameter unklar |

> `Bohrug` ist vermutlich eine Kurzform von `BOHRUNG` — oder ein anderes Macro. Zu klären mit Sample-Datei.

---

## Nächste Schritte (Phase 1)

1. **Eine der `.hop`-Dateien vom Controller-PC kopieren** (z.B. `kantenschnitt.hop` oder `05_Tisch_bohren.hop`)
2. Datei im Texteditor öffnen → prüfen ob ASCII
3. Vollständige Makro-Liste + Header/Footer dokumentieren
4. Dann: Phase 2 starten (GHPython Engine)

---

---

## Maschinenlieferant / Servicepartner

**SAMSTAG MaschinenTechnik** — Kontakt für technischen Support, Software-Lizenz-Fragen (HOPS-Dongle) und .hop-Format-Fragen.
*(Quelle: Outlook-Screenshot aus `Datein/` Ordner, 2026-03-24)*

---

## Sample-Datei erhalten (2026-03-24)

`Datein/Muster_DXF_Import.hop` — vollständige Musterdatei mit allen relevanten Operationstypen:
- Außenkontur (SP/G01/G03M/EP)
- Vertikalbohrungen (Bohrung)
- Horizontalbohrungen (HorzB)
- Nut (CALL _nuten_frei_v5)
- Kreistasche / Kreisbahn / Rechtecktasche (CALL _*_V5)

**Phase 1 Blocker aufgelöst.** Vollständige Analyse in `.planning/research/HOP_FORMAT_DECODED.md`

---

*Erstellt: 2026-03-23 — Quelle: Fotos der Maschine und Software*
*Aktualisiert: 2026-03-24 — Sample-Datei erhalten, Format vollständig dekodiert*
