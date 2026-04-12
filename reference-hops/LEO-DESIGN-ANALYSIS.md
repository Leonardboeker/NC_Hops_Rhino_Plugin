# LEO-Design .hop File Analysis

Source: `F:\Für Leo\LEO-Design\` → `reference-hops/leo-design/`
Analysiert: 2026-04-12 | **204 Dateien** in 20 Projekten

---

## Makro-Häufigkeit (alle 204 Dateien)

| Makro | Häufig. | Beschreibung | Beispiel |
|-------|--------:|-------------|---------|
| Park_V7 | 142 | Standard-Parkposition | Blende_HKV.hop |
| HH_MarkLabel | 118 | Etiketten-Markierung (Drucker) | Blende_HKV.hop |
| BN_TrennerInnenAussen | 118 | Nesting-Separator innen/außen | Blende_HKV.hop |
| BN_NestKontur | 118 | Nesting-Kontur-Definition | Blende_HKV.hop |
| _saege_y_V7 | 110 | Sägeschnitt Y-Richtung (±Gehrung) | Linz/Format1.hop |
| _Format_V5 | 84 | Format-Bearbeitung | Format SG0085_HG.hop |
| B2Punkte_V7 | 68 | 2D-Bemaßungspunkte/Pfeile | HZK_Schrankseite.hop |
| _saege_x_V7 | 66 | Sägeschnitt X-Richtung (±Gehrung) | Linz_Steri_Raum/ |
| _Kreistasche_V5 | 47 | Kreisförmige Tasche | Linz/Schiebetür.hop |
| _Bohgy_V5 | 43 | Bohrreihe Y-Richtung | Linz_Steri_Raum/ |
| _Rechteck_V7 | 40 | Rechteck-Pocket (erweitert) | Proben/ |
| _RechteckTasche_V5 | 34 | Rechtecktasche | Proben/ |
| _Bohgx_V5 | 24 | Bohrreihe X-Richtung | Linz_Steri_Raum/ |
| Keep_Part_V7 | 22 | Kontur-Haltefunktion | Kumpel/Format_1.hop |
| P_S_RE_LI_FLAT_V7 | 22 | Kanten-Processing (flach) | Tisch_Botschaft/ |
| _Kreisbahn_V5 | 22 | Offene Kreiskontur | Ploom/Drehscheibe.hop |
| _Rbohx_einpass_V5 | 17 | Einpass-Bohrreihe X | Linz/ |
| _hg_para_V5 | 15 | HopGlass Parameter-Funktion | Linz/Theke/ |
| P_S_RE_LI_HORI_V7 | 15 | Kanten-Processing (horizontal) | Tisch_Botschaft/ |
| _saege_frei_V7 | 14 | Freisäge | Linz/ |
| BWinkel_V7 | 13 | B-Winkel Verarbeitung | Linz/ |
| _Topf_V5 | 12 | Blum Topfband (Scharnier-Ausfräsung) | Linz_Steri_Raum/Tür_Hochschrank.hop |
| Fixchip_K | 12 | Klemmschräubchen-Position | HZK_Boden_Deckel.hop |
| _Nuten_X_V5 | 8 | Nut fräsen X-Richtung | HZK_Boden_Deckel.hop |
| ToolOptiPreferTools_V7 | 7 | Werkzeug-Präferenz-Optimierung | Proben/ |
| MachineStop_V7 | 6 | Maschinenstop / Pause | Linz/ |
| P_S_HI_VO_HORI_V7 | 5 | Edge-Processing High-Volume | Tisch_Botschaft/ |
| _ExecutePocket_V5 | 4 | Custom Pocket-Ausführung | Kumpel/ |
| ABC_VAR | 3 | Custom Variable-Handler | Linz/Theke/Musterdatei_2.hop |
| P_S_HI_VO_FLAT_V7 | 2 | Edge-Processing HV flach | Linz_Steri_Raum/ |
| _Sformat_V5 | 2 | S-Format Variante | Linz/ |
| _hg_lot_V5 | 2 | HopGlass Lot-Funktion | Linz/ |

---

## WZ-Typen

| Typ | Häufig. | Bedeutung |
|-----|--------:|----------|
| WZF | 313 | Fräswerkzeug |
| WZB | 61 | Bohrwerkzeug |
| WZS | 57 | Sägewerkzeug |

---

## VP-Variablen

**Keine VP18–VP109 Variablen in den 204 Dateien gefunden.**
Metadaten laufen über `_hhdata_*` Variablen (Kanten, Nesting, Etiketten).

---

## Gruppen / Systeme

### Nesting-System (118 Dateien = 58%)
Immer zusammen: `BN_NestKontur` + `BN_TrennerInnenAussen` + `HH_MarkLabel` + `Park_V7`

### Säge-Suite (176 Dateien = 86%)
`_saege_x_V7`, `_saege_y_V7`, `_saege_frei_V7` — mit optionalem `KW` für Gehrungswinkel

### Bohr-Suite
`_Bohgx_V5`, `_Bohgy_V5`, `_Rbohx_einpass_V5` — parametrische Bohrreihen mit USE2/3/4

### Tasche-Suite
`_Kreistasche_V5`, `_RechteckTasche_V5`, `_Rechteck_V7` — 3 verschiedene Taschenvarianten

### Kanten-Processing
`P_S_RE_LI_HORI_V7`, `P_S_RE_LI_FLAT_V7`, `P_S_HI_VO_*` — Kantenbearbeitung für Möbel

### Blum-Integration
`_Topf_V5` (Topfband-Ausfräsung) + `_hhdata_*` (Kanten/Material/Nesting-Metadaten)

---

## Projektschwerpunkte

| Ordner | Dateien | Hauptmakros | Fokus |
|--------|--------:|------------|-------|
| Linz/ | 56 | _saege_*, Park_V7, _Bohgy_V5 | Theke + Möbel |
| Ploom/ | 46 | _Kreistasche_V5, Park_V7 | Display + Kreisformen |
| kÖNIGSALLEE/ | 19 | _Format_V5, Park_V7 | Küchenmöbel |
| Linz_Steri_Raum/ | 18 | B2Punkte_V7, _Bohgy_V5 | Schränke + Bohrbilder |
| Proben/ | 15 | _Rechteck_V7, _saege_y_V7 | Rechteck-Taschen |
| Gitterboxen/ | 16 | _saege_x_V7, _saege_y_V7 | Reine Sägearbeit |
| Sophia_Schwester/ | 9 | _saege_*, Park_V7 | Küchenmöbel |
| PORSEGUR/ | 8 | Park_V7, BN_*, HH_MarkLabel | Nesting |
| Buchstaben/, David/, Freundin/, MAMA/ | 9 | — | Reine Konturen (keine CALLs) |

---

## Empfohlene nächste Plugin-Komponenten

| Prio | Komponente | Makro(s) | Abdeckung | Aufwand |
|------|-----------|----------|----------:|--------|
| 1 | **HopAngleCut** | `_saege_x/y_V7` + `KW` | 86% | Klein — HopSaw erweitern |
| 2 | **HopGrooveSlot** | `_Nuten_X/Y_V5` | häufig in Schränken | Klein |
| 3 | **HopBlumHinge** | `_Topf_V5` | 11% | Mittel |
| 4 | **HopCircularPath** (erweitern) | `_Kreisbahn_V5` | 11% | Klein |
| 5 | **HopNestingSystem** | `BN_* + Park_V7 + HH_MarkLabel` | 58% | Groß |

---

## Referenzdateien `files/` (10 Key-Dateien aus Vorab-Analyse)

- `Axios_Rueckwand_variabel.hop` — parametrische Bohrgruppen
- `Boden_LegraBox_Zarge_C.hop` — _hhdata_* Metadaten
- `Gehrungs_vario.hop` — Gehrungsschnitte KW=-45.05
- `HZK_Boden_Deckel_602x278.hop` — Nuten + Fixchip
- `Drehscheibe.hop` — Kreisbahn konzentrisch
- `Brett_Endbearbeitung_Sichtseite.hop` — Rechteck V7
- `Tritt_217.hop` — direkte G-Codes gemischt mit Makros
- `Seite_Schrank_Schub_3Stueck.hop` — Multi-Unit Nesting
- `Schiebetuer.hop` — Kreistaschen-Raster
- `Musterdatei.hop` — Template-Struktur
