# Plugin Backlog

Improvements and open items.

---

## Open — needs verification on the machine

> Die am 2026-08-10 an der Maschine erhobenen Format-Regeln stehen gesammelt in
> **MASCHINENABGLEICH.md** — Bezugsebenen, irrefuehrende Parameternamen, Kopfzeilen,
> und was noch offen ist. Vor Aenderungen am Emitter dort nachsehen.

### Kontur-Vorschau in Rhino stimmt nicht ganz (2026-08-10)
- Die NC-Ausgabe ist belegt korrekt (Polylinien werden abgefahren, Frässchritte stimmen)
- Die Darstellung im Rhino-Viewport gibt die Kontur aber nicht 1:1 wieder
- Noch nicht eingegrenzt: Radiuskorrektur-Versatz, Anfahrlinien oder die
  Vorschau-Geometrie selbst
- **Action:** eine bekannte Kontur (z.B. die geschlossene Polylinie der Einlegeboeden)
  in Rhino gegen die exportierten G01-Koordinaten legen und die Abweichung benennen.

### `LAGE` parameter in HopSaw unverified
- `LAGE:=bladeAngle` in `_nuten_frei_v5` is an assumption — not tested on the machine
- Until this is verified, HopSaw may produce wrong cuts
- **Action:** run a test cut on the DYNESTIC. If `LAGE` is not the blade-tilt axis, find the correct parameter from the CAMPUS controller.

### `HopDrill` Stepdown — machine test pending
- Loop order has been fixed (outer loop = point, inner = pass) ✅
- Whether this makes a difference on the CAMPUS controller: not yet tested
- **Action:** run a test drill with stepdown and check the travel order.

### `HopExport` header dimensions — machine test pending
- Header now writes correct DX/DY/DZ values ✅
- Unclear whether CAMPUS reads the header or the VARS block
- **Action:** verify on the machine whether the header values are relevant.

---

## Missing components

### `HopAngledDrill` (angled drill)
- Removed from Phase 8.7 — first check the CAMPUS controller for whether the macro exists
- **Action:** find the macro name + parameters in CAMPUS, then implement.

---

## Done in Phase 8.7 (2026-04-06)

- ✅ `HopDrill` stepdown loop order fixed (point → passes, not the other way around)
- ✅ `HopExport` header dimensions: DX/DY/DZ from inputs instead of hardcoded 0
- ✅ `HopExport` operation sorting: WZB → WZF → WZS automatic
- ✅ `HopExport` export trigger: rising edge (false→true), no manual reset needed
- ✅ `HopSaw` length from dirLine: when Length=Default (600), the line length is used
- ✅ `PreviewHelper` static class: DrawMeshes / DrawWires / GetClippingBox
- ✅ All 7 op components migrated to PreviewHelper (no more duplicated preview code)
- ✅ `NcStrings.cs`: NcDrill / NcSaw / NcExport internal helpers without Rhino dependency
- ✅ NUnit 3 test project: 17 tests, all green
- ✅ HopEdgeBanding: deliberately dropped

---

## Notes

- Use `MACHINE_NOTES.md` for machine-specific findings (e.g. verified `LAGE` value)
- For all NC-format questions, check `Datein/Muster_DXF_Import.hop` first — that is the ground truth

## Tiefen-Bezugssystem: ENTSCHIEDEN, Maschine bestaetigt (2026-08-10, HOPS-Editor an der HolzHer)

Acht Emitter schreiben Tiefen als absolutes Zeichnungs-Z (`cutZ = SurfaceZ - |depth|`,
u.a. PocketLogic.cs:35, SlotLogic.cs:34, CircPathLogic.cs:33, ContourLogic.cs:100ff).
Design-Annahme laut HopAnalyzer M7: Werkstueck in Bett-Koordinaten, Boden = Z0,
Oberflaeche = DZ. Die einzige externe Maschinendatei mit diesen Makros
(Dateien/Muster_DXF_Import.hop) rechnet aber von der OBERKANTE negativ
(Tiefe:=-10/-20/-5). Liegt die Zeichnung mit Oberkante auf Z=DZ, kommt fuer eine
Durchfraesung Tiefe:=0 heraus -- unter Oberkanten-Konvention schneidet das nichts.

Entscheidung: entweder alle Emitter auf `Tiefe := -|depth|` relativ zur Oberkante
umstellen (aendert JEDE Ausgabe; PocketLogicTests.cs:33 pinnt das alte Verhalten),
oder die Bett-Konvention an der Maschine verifizieren und M7 verschaerfen
(faengt Durchfraesungen mit 0 und Teiltaschen derzeit nicht).

Dazu: Bohrungs-Konvention ist unklar -- eigene Goldens schreiben `Bohrung(...,19,6,...)`
(Zeichnungs-Z), die Maschinenreferenz `Bohrung(...,3,-0.3,...)` ist eine
Kantenbohrung (y=DS/2) und taugt nicht als Beleg fuer senkrechte Bohrungen.

**Aufloesung:** Fraesmakro-Familie auf Oberkanten-negativ umgestellt (5 Emitter:
PocketLogic rect+circ, CircPathLogic, SlotLogic freie Nut, SawLogic). HOPS akzeptiert
Freies_Nuten Tiefe -5 und Rechteck -19 anstandslos; Tiefe 0 hatte zuvor nichts geschnitten.
Konturen/Saegen/Bohrungen bleiben Bett-absolut (SP z=-0.2, _saege SZ=-3 in allen Referenzen).
Noch offen an der Maschine: KW-Kipprichtung (Annahme im FormatCut-Preview markiert) und die
Bohrungs-Konvention (kein senkrechtes Maschinenbeispiel im Repo).

**Dazu gelernt (HOPS-Editor):** unbekannte Makro-Variablen sind harte Fehler --
_saege_y_V7 kennt kein ESX, nur ESY (Emitter korrigiert). Und die Werkzeugpruefung
vergleicht Tiefe gegen die Nutzlaenge des Werkzeugs aus der Maschinendatenbank
("3 Dibond Faltfraeser 7<19"): die ToolNr muss zur Bearbeitung passen, das kann der
Postprozessor nicht wissen.
