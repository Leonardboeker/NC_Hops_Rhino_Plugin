# LEO-Design .hop File Analysis

Source folder: `F:\Für Leo\LEO-Design\`
Analysiert: 2026-04-12 | ~170 Dateien in 20+ Projekten

---

## Bekannte Makros (Plugin already supports)
- `WZF` + `SP/G01/G02M/G03M/EP` → HopContour, HopEngraving
- `WZB` + `Bohrung(...)` → HopDrill
- `WZS` + `CALL _nuten_frei_v5(...)` → HopSaw
- `CALL HH_Tasche(...)` → HopRectPocket
- `_Kreisbahn_V5` (partial) → HopCircPath

---

## Neue Makros / Patterns gefunden

### 1. Nuten — `_Nuten_X_V5` / `_Nuten_Y_V5`
**Priorität: HOCH** — kommt in fast jedem Schrankteil vor

Parameter: `NB` (Nutbreite), `NT` (Nuttiefe), `ARAND` (Abstand Rand)

Beispieldateien:
- `F:\Für Leo\LEO-Design\HZK_Boden_Deckel_602x278.hop` — Nuten mit Säge WZS(210)
- `F:\Für Leo\LEO-Design\Linz_Steri_Raum\Seite Schrank_Schub_3Stück.hop`

```
WZS (210,_VE,_V*0.3,_VA,_SD,0,'')
CALL _Nuten_X_V5 (VAL X1:=0,Y1:=50,X2:=DX,NB:=8.25,NT:=10,ARAND:=0,...)
```

---

### 2. Kreisbahn (offene Kreiskontur) — `_Kreisbahn_V5`
**Priorität: MITTEL**

Parameter: `RADIUS`, `WINKEL`, `BEARB_UMKEHREN`, `RAMPE`, Tiefe

Beispieldatei:
- `F:\Für Leo\LEO-Design\Ploom\Gluecksrad\Drehscheibe.hop`

```
CALL _Kreisbahn_V5 (VAL MX:=DX/2,MY:=DY/2,RADIUS:=400,TIEFE:=10,
    WINKEL:=180,BEARB_UMKEHREN:=1,RAMPE:=1,...)
```

---

### 3. Gehrungsschnitt — `_saege_x_V7` / `_saege_y_V7` mit `KW`
**Priorität: MITTEL**

`KW` = Schnittwinkel (z.B. `-45.05` für 45° Gehrung)

Beispieldateien:
- `F:\Für Leo\LEO-Design\PORSEGUR\Gehrungs vario.hop`
- `F:\Für Leo\LEO-Design\Linz_Steri_Raum\Axios_Rückwand_Format_Bohrungen_variabel.hop`

```
WZS (250,_VE,_V*0.3,_VA,_SD,0,'')
CALL _saege_x_V7 (VAL X:=50,Y:=0,TIEFE:=19,KW:=-45.05,...)
```

---

### 4. Fixchip-Befestigung — `Fixchip_K`
**Priorität: NIEDRIG**

Parameter: `SPX`, `SPY`, `SPZ`, `WKLXY` (Winkel)

Beispieldatei:
- `F:\Für Leo\LEO-Design\HZK_Boden_Deckel_602x278.hop`

---

### 5. Parametrische Bohrreihen — `_Bohgx_V5` / `_Bohgy_V5`
**Priorität: NIEDRIG**

`USE2/3/4` — bedingte Bohrlöcher, `PDV_Y` — Versatz

Beispieldateien:
- `F:\Für Leo\LEO-Design\kÖNIGSALLEE\Treppe\Tritt 217.hop`
- `F:\Für Leo\LEO-Design\Linz_Steri_Raum\Seite Schrank_Schub_3Stück.hop`

---

### 6. Positionierreihen — `P_S_RE_LI_HORI_V7` / `P_S_RE_LI_FLAT_V7`
**Priorität: NIEDRIG** — sehr spezialisiert

Parametrische X/Y-Positionierung für 6+ Bohrlöcher in einer Zeile

---

### 7. Kanten-Metadaten — `_hhdata_*` Variablen
Für Blum/HH-Integration: Kantentyp, Material, Stärke, Inlay

```
_hhdata_EdgeLeft := 0
_hhdata_EdgeRight := 1
_hhdata_Material := 'MDF'
_hhdata_Thickness := 19
```

Beispieldatei:
- `F:\Für Leo\LEO-Design\Linz_Steri_Raum\Boden LegraBox Zarge_C.hop`

---

### 8. `_Rechteck_V7` (erweiterte Rechteck-Tasche)
Wie HopRectPocket aber mit `RADIUSKORREKTUR` und `INTERPOL=1`

Beispieldatei:
- `F:\Für Leo\LEO-Design\Ploom\Produkt_Panel\Brett_Endbearbeitung_Sichtseite.hop`

---

## Referenzdateien in diesem Ordner

Kopierte Referenzen für Plugin-Entwicklung (Unterordner `files/`):
- `Axios_Rückwand_Format_Bohrungen_variabel.hop` — parametrische Bohrgruppen
- `Boden_LegraBox_Zarge_C.hop` — Blum/HH Metadaten
- `Gehrungs_vario.hop` — Gehrungsschnitte
- `HZK_Boden_Deckel_602x278.hop` — Nuten + Fixchip
- `Drehscheibe.hop` — Kreisbahn
- `Brett_Endbearbeitung_Sichtseite.hop` — Rechteck V7
- `Tritt_217.hop` — direkte G-Codes gemischt mit Makros
- `Seite_Schrank_Schub_3Stueck.hop` — Multi-Unit Nesting
- `Schiebetuer.hop` — Kreistaschen-Raster
- `Musterdatei.hop` — Template-Struktur

---

## Empfohlene nächste Komponenten

| Komponente | Makro | Aufwand |
|-----------|-------|---------|
| **HopGrooveSlot** | `_Nuten_X/Y_V5` | Klein — analog zu HopSaw |
| **HopCircularPath** (erweitern) | `_Kreisbahn_V5` | Mittel |
| **HopAngleCut** | `_saege_x/y_V7` + `KW` | Klein — HopSaw erweitern |
