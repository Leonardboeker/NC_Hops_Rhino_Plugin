# Plugin Backlog

Verbesserungen und offene Punkte für die nächste Session.

---

## Prio 1 — Bugs / muss verifiziert werden

### `LAGE`-Parameter in HopSaw unverified
- `LAGE:=bladeAngle` in `_nuten_frei_v5` ist eine Annahme — nicht auf der Maschine getestet
- Solange das nicht verifiziert ist, kann HopSaw falsche Schnitte ausgeben
- **Aktion:** Testschnitt auf der DYNESTIC fahren. Wenn `LAGE` nicht die Blade-Tilt-Achse ist, richtigen Parameter aus dem CAMPUS-Controller raussuchen.

### `HopDrill` Stepdown-Reihenfolge
- Aktuell: alle Punkte bei Pass 1, dann alle Punkte bei Pass 2 (äußere Schleife = Pass, innere = Punkt)
- Richtig für Peckboring: alle Schritte an Loch 1 fertig, dann Loch 2 (äußere Schleife = Punkt, innere = Pass)
- Datei: `src/.../Components/Operations/HopDrillComponent.cs`, SolveInstance ~Zeile 130
- **Aktion:** Loop-Reihenfolge tauschen. Ggf. mit Maschine testen ob das den Unterschied macht.

### `HopExport` Header-Dimensionen hardcoded 0
- `;DX=0.000`, `;DY=0.000`, `;DZ=0` im Header sind immer 0, unabhängig von den Inputs
- Die VARS-Sektion hat die richtigen Werte — unklar ob CAMPUS den Header oder VARS liest
- Datei: `src/.../Components/Export/HopExportComponent.cs`, BuildHeader ~Zeile 145
- **Aktion:** Auf der Maschine prüfen. Falls der Header gelesen wird, Werte aus den Inputs eintragen.

---

## Prio 2 — Workflow-Verbesserungen

### Operation-Sortierung in HopExport
- Maschinen laufen effizienter in fester Reihenfolge: Bohrungen → Fräsen → Sägen
- Aktuell: Reihenfolge = wie die Inputs connected sind
- **Aktion:** In HopExport die `operationLines` nach Tool-Typ vorsortieren bevor sie in den Header geschrieben werden. Erkennbar am Präfix: `WZB` = Bohrung, `WZF` = Fräsen, `WZS` = Sägen.

### HopExport Export-Trigger verbessern
- Ein Toggle ist nervig: false → true → false → true bei jedem Export
- **Aktion:** Alternativ einen Knopf-Pattern bauen: Component merkt sich ob `export` in der letzten Iteration `false` war — bei Wechsel auf `true` wird einmalig exportiert, kein manuelles Zurücksetzen nötig.

### HopSaw Length-Redundanz
- Beide Inputs `dirLine` (hat eine Länge) und `Length` (eigener Slider) koexistieren
- Wenn `Length` nicht verbunden ist, könnte die Linienlänge direkt als Schnittlänge verwendet werden
- **Aktion:** Wenn `Length` auf dem Default-Wert (600) ist und `dirLine`-Länge > 0, `dirLine`-Länge nehmen. Oder explizit: wenn `Length`-Input nicht connected → aus Linie ableiten.

---

## Prio 3 — Fehlende Komponenten

### `HopAngledDrill` (Schrägbohrung)
- Falls die DYNESTIC 7535 ein Winkelaggregat hat
- Macro: vermutlich `Bohrung` mit zusätzlichem Winkelparameter, oder separates Macro
- **Aktion:** Erst im CAMPUS-Controller nachschauen ob das Macro existiert und wie die Parameter heißen.

### `HopEdgeBanding` Marker
- Markiert Kanten die danach auf der Kantenanleimmaschine bearbeitet werden
- Kein CNC-Output nötig — nur als Planungshilfe / Stückliste
- **Aktion:** Einfache Komponente die eine Kante (Linie) + Material-Tag entgegennimmt und in die Stückliste schreibt.

---

## Prio 4 — Code-Qualität

### Duplikate Preview-Logik
- `BuildTiltedBox`, `DrawViewportMeshes`, `DrawViewportWires` wiederholen sich in fast jeder Operations-Komponente
- **Aktion:** `PreviewHelper`-Klasse mit statischen Methoden extrahieren. Alle Operations-Komponenten erben davon oder rufen Helper auf.

### Unit-Tests
- Kein einziger Test vorhanden
- NC-Generierung ist deterministisch → gut testbar
- **Aktion:** Mindestens einen Test pro Operations-Komponente der den Output-String gegen ein bekanntes `.hop`-Sample vergleicht. Testprojekt ist schon angelegt unter `test/`.

---

## Notizen

- `MACHINE_NOTES.md` für maschinenspezifische Erkenntnisse nutzen (z.B. verifizierter `LAGE`-Wert)
- Alle NC-Format-Fragen zuerst in `Datein/Muster_DXF_Import.hop` nachschauen — das ist die Ground Truth
