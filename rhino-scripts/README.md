# Rhino-Scripts — Snapshot (Stand 2026-09-06)

**Master liegt in `%APPDATA%\McNeel\Rhinoceros\8.0\scripts\`** — nur von dort
lädt Rhino. Dieser Ordner ist die versionierte Kopie fürs Projekt.

> **Hier NICHT direkt editieren.** Änderungen im Master machen, dann
> `python tools/sync_scripts.py --push`. Der Sync verweigert jede Datei, deren
> Kopie hier neuer ist als der Master — genau der Fall, in dem am 2026-08-29
> eine parallele Session ihre Park-`MODE:=2`-Änderung an eine andere Session
> verloren hat, die den Master drüberkopierte. Neue Master-Dateien meldet er
> als `NEU` und kopiert sie nur mit `--push --neu` (Scratch-Tests bleiben so
> draußen).

## Die zwei Fertigungsketten

```
Korpus:  KorpusBau  ->  TeilExport   ->  <Teil>.hop  ->  HOPS an der Maschine
         (3D + Tags)    (flachlegen)      (direkt ladbar, Vorlage umgangen)

Frei:    zeichnen  ->  CncZuweisen  ->  CncSchau  ->  CncExport  ->  .hop
         (2D, XY)      (kind/depth)     (Dots)       (Phase 1: nur Makros)
```

Beide schreiben über den gemeinsamen Kern `holzher_teil` (Reihenfolge-Regel,
Prüfungen, Writer) — Phase 1 aus `../PLAN_FREIE_GEOMETRIE.md`.

| Script | Zweck |
|---|---|
| KorpusBau | parametrischer Korpus: Teile + Bohrbilder (Bänder/Griffe/Dübel/Lochreihe) + Nut-Linien, alles getaggt (part/depth/material/thickness) |
| TeilExport | Teil(e) flachlegen, Bohrkreise+Nuten per part-Tag einsammeln, je Teil **.hop** (Maschine) + .dxf (Sichtkontrolle) |
| CncZuweisen | freie Kurven taggen: Art wählen (Rohteil/Kontur/Ausschnitt/Bohrung/…), Werte gelten für die ganze Auswahl |
| CncSchau | TextDots je getaggtem Objekt auf `CNC::Schau` — die visuelle Gegenkontrolle vor dem Export |
| CncExport | sammeln, prüfen (harte Wächter), Bericht, dann .hop + .dxf. **Phase 1: Rechteck-Kontur (`_Format_V5`) + runde Ausschnitte (`_Kreistasche_V5`, ESXY 0) + Bohrungen — keine SP-Zeile, immun gegen T13.** Freie Konturen/Nut/Kante/Tasche/Säge sind gesperrt (Fehler) bis T13/Phase 2–4 |
| holzher_teil | gemeinsamer Kern: `Teil`, `pruefe()`, `baue_hop()` (DIE Reihenfolge-Regel), `schreibe()` |
| holzher_hop | .hop-Writer — JEDE Zeile mit Beleg aus MASCHINENABGLEICH.md / reference-hops; `BAHN_ABSOLUT` = der eine T13-Schalter |
| holzher_dxf / holzher_kontur | DXF-R12-Writer / gemeinsame Kurvenlogik (Bulge, Mikro-Segmente, Vollkreise) |
| HolzHerExport / HolzHerLayerExport | ältere DXF-Wege (aCADemy / Layer-Import) — funktionieren, aber .hop ist der Königsweg |
| Tag / TagAudit / TagDot / TagLeader | Key-Value-System (tags.json) + Anzeige |
| *Test-Dateien | Headless-Suiten (Scratch-Rhino mit `/runscript`), Stand 2026-09-06: **76+43+45+37 Prüfungen grün** |

Maschinendaten: `werkzeuge.json` (Master in `_standards/`, aus der
Werkzeugdatei `../reference-hops/*.too` geparst; neu: `tisch_max_mm` konservativ + `fraeser_kontur_d`).
Format-Wahrheit: `../MASCHINENABGLEICH.md` + `../reference-hops/dxf-layer-import/KONVENTION.md`.
Plan für die Validierung aus der Ferne: lokaler Maschinenplan (nicht im Repo).
