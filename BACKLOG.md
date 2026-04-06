# Plugin Backlog

Verbesserungen und offene Punkte.

---

## Offen — muss auf der Maschine verifiziert werden

### `LAGE`-Parameter in HopSaw unverified
- `LAGE:=bladeAngle` in `_nuten_frei_v5` ist eine Annahme — nicht auf der Maschine getestet
- Solange das nicht verifiziert ist, kann HopSaw falsche Schnitte ausgeben
- **Aktion:** Testschnitt auf der DYNESTIC fahren. Wenn `LAGE` nicht die Blade-Tilt-Achse ist, richtigen Parameter aus dem CAMPUS-Controller raussuchen.

### `HopDrill` Stepdown — Maschinentest ausstehend
- Loop-Reihenfolge wurde gefixt (äußere Schleife = Punkt, innere = Pass) ✅
- Ob das auf der CAMPUS-Steuerung einen Unterschied macht: noch nicht getestet
- **Aktion:** Testbohrung mit Stepdown fahren und Abfahrtreihenfolge prüfen.

### `HopExport` Header-Dimensionen — Maschinentest ausstehend
- Header schreibt jetzt korrekte DX/DY/DZ-Werte ✅
- Unklar ob CAMPUS den Header oder den VARS-Block liest
- **Aktion:** Auf der Maschine prüfen ob die Werte im Header relevant sind.

---

## Fehlende Komponenten

### `HopAngledDrill` (Schrägbohrung)
- Entfernt aus Phase 8.7 — erst CAMPUS-Controller prüfen ob das Macro existiert
- **Aktion:** Macro-Name + Parameter aus CAMPUS raussuchen, dann implementieren.

---

## Erledigt in Phase 8.7 (2026-04-06)

- ✅ `HopDrill` Stepdown-Loop-Reihenfolge gefixt (Punkt → Passes, nicht umgekehrt)
- ✅ `HopExport` Header-Dimensionen: DX/DY/DZ aus Inputs statt hardcoded 0
- ✅ `HopExport` Operation-Sortierung: WZB → WZF → WZS automatisch
- ✅ `HopExport` Export-Trigger: Rising-Edge (false→true), kein manuelles Zurücksetzen nötig
- ✅ `HopSaw` Length aus dirLine: wenn Length=Default (600), wird Linienlänge verwendet
- ✅ `PreviewHelper` statische Klasse: DrawMeshes / DrawWires / GetClippingBox
- ✅ Alle 7 Op-Komponenten auf PreviewHelper umgestellt (kein duplizierter Preview-Code mehr)
- ✅ `NcStrings.cs`: NcDrill / NcSaw / NcExport interne Helper ohne Rhino-Dependency
- ✅ NUnit 3 Testprojekt: 17 Tests, alle grün
- ✅ HopEdgeBanding: bewusst gestrichen

---

## Notizen

- `MACHINE_NOTES.md` für maschinenspezifische Erkenntnisse nutzen (z.B. verifizierter `LAGE`-Wert)
- Alle NC-Format-Fragen zuerst in `Datein/Muster_DXF_Import.hop` nachschauen — das ist die Ground Truth
