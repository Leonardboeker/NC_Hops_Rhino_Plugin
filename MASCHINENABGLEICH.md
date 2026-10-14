# Maschinenabgleich HolzHer / HOPS

Was die Maschine über das `.hop`-Format verrät, und woher wir es wissen.

Dieses Dokument hält Regeln fest, die **nicht** aus dem Code ableitbar sind: NC-Hops
ist kaum dokumentiert, und mehrere Parameter heißen anders, als sie sich verhalten.
Jede Regel unten steht mit ihrem Beleg. Wo kein Beleg steht, steht ausdrücklich
"unbelegt" — diese Stellen sind die gefährlichen.

Erhoben am **2026-08-10** von Leonard an der Maschine, gegengelesen gegen die ~200
Maschinendateien unter `reference-hops/`.

---

## Wie die Regeln entstanden sind

Nicht durch Code-Lesen. Der Kreislauf, der funktioniert hat:

1. Grasshopper exportiert eine Datei
2. Datei in HOPS laden — die Fehlermeldungen sind präzise und benennen den Parameter
3. In HOPS von Hand korrigieren, bis es stimmt, **speichern**
4. Korrigierte Fassung gegen die Plugin-Ausgabe diffen — der Unterschied *ist* die Regel

Schritt 3+4 hat mehr geklärt als jede Analyse davor. Ein einziger Diff hat `ESMD`
als Bezugsschalter entlarvt; ein einziger Screenshot des Säge-Dialogs hat `SZ`/`EZ`
richtiggestellt, nachdem die Parameternamen in die Irre geführt hatten.

Merksatz: **HOPS lehnt unbekannte Makro-Variablen hart ab.** Ein Tippfehler im
Parameternamen wird zum Fehler, nicht zu stillem Unsinn. Das ist ein Geschenk —
die stillen Fehler waren durchweg die teuren.

---

## Bezugsebenen — die wichtigste Erkenntnis

Die Steuerung mischt **zwei Bezugssysteme**, und mehrere Makros lassen die Wahl per
Parameter. Das war die Ursache der meisten falschen Dateien.

| Familie | Parameter | Bezug | Durchgehend |
|---|---|---|---|
| `_Rechteck_V7`, `_Kreistasche_V5` | `TIEFE` + `ESMD` | `ESMD:=0` Oberkante · `ESMD:=1` Tisch | `TIEFE:=-0.2`, `ESMD:=1` |
| `_nuten_frei_v5` | `TIEFE` | Oberkante, negativ nach unten | 5 mm Nut → `TIEFE:=-5` |
| `Bohrung` | Feld 4 + Feld 9 | Feld 9 `=0` Oberkante · `=1` Tisch | `-0.2` mit Feld 9 `=1` |
| Konturen `SP` | Z direkt | Tisch (Unterkante = 0) | `SP z = -0.2` |
| `_saege_*_V7` | `EZ` | Unterkante | `EZ:=-0.2` |

**Beleg Fräsmakros:** Maschinentest — `TIEFE:=0` hat *nichts* geschnitten. Danach
HOPS-Meldung „Tiefe ist größer als die Nutzlänge (… 7<19)" bei `TIEFE:=-19`, also
liest HOPS `|TIEFE|` als Tiefe.
**Beleg `ESMD`:** Diff der handkorrigierten `Platten Gehrung Oben 12 Stk .hop` —
`ESMD` war der einzige Parameter, der sich neben der Tiefe geändert hat.
**Beleg Bohrung Feld 9:** in `reference-hops` tragen alle 110 tiefen Bohrungen
(−10…−22) eine `0`, die flachen (−0.2/−0.3) eine `1`.
**Korrektur der Pauschale (2026-08-29, LEO-Korpus 214 Dateien):** tief→`0` 121×,
flach→`1` 228×, aber **14 flache Bohrungen mit `0`** (`Rollbrett.hop` D5,
`Muster_links.hop` D3): −0.2 von der *Oberkante* = 0.2-mm-Ansenkung/Markierung.
Die Regel bleibt „Feld 9 = Bezugsschalter"; „flach ⇒ 1" gilt nur für
Durchgangsbohrungen, nicht für Markierstiche.
**Beleg Konturen:** `Tritt_217.hop`, 19-mm-Teil, Werkzeug 19, `SP z=-0.2` als
echter Trennschnitt.
**Beleg Tisch-Bezug auch positiv (2026-08-29, LEO-Korpus):** `Oben_Unten_HKV.hop` fräst eine
Kontur mit `SP z=+16.5` bei DZ 19 (= 2.5 mm tief, Resthöhe über Tisch), und
`Vorformat_38mm*.hop` ruft `_Format_V5` mit `TIEFE:=1` (1 mm stehen lassen) —
alle 85 `_Format_V5`-Aufrufe im Korpus tragen `ESMD:=1`. Der Tisch-Bezug trägt
also echte Resthöhen, nicht nur den −0.2-Überstand.

Faustregel für die Praxis: **bei Bezug Tisch ist der Tiefen-Eingang nicht die
Materialstärke, sondern der Überstand unter der Unterkante** — also `0.2`, nicht `19`.

---

## Parameter, deren Name in die Irre führt

### `_saege_*_V7`: `SZ` und `EZ`
Die Namen legen „Start-Z / End-Z" nahe. Der HOPS-Dialog zeigt etwas anderes:

```
EZ  ->  "Schnitttiefe (L3)"   die echte Schnitttiefe   -0.2 = trennt
SZ  ->  "Ritztiefe"           nur bei Vorritzen aktiv  -3
```

Ein Versuch, `EZ` auf die Trenntiefe „zu korrigieren", war deshalb falsch und wurde
zurückgenommen. Die Referenzdateien führen zusätzlich in die Irre, weil `EZ` dort
zwischen −38 und +8 schwankt.

**Aufgeklärt (2026-08-29, LEO-Korpus):** die EZ-Ausreißer sind horizontale
Nutsäge-Schnitte mit `KW:=-90` in die Kante (`Boden_LegraBox_Zarge_C.hop`:
`EZ:=-38` bzw. `+8`, `BH:=8`, `EL/AL:=100`) — bei liegendem Blatt ist `EZ` die
Höhenlage an der Kante, keine Schnitttiefe. Für normale (senkrechte) Schnitte
bleibt `EZ:=-0.2` die Trennregel.

### `_saege_*_V7`: `K` ist der Schnittmodus
In Dialog-Reihenfolge: `0` Gleichlauf GL · `1` Gegenlauf GGL · `2` **Vorritzen GL**
· `3` Vorritzen GGL. Das Plugin hatte `2` fest verdrahtet und damit auf jedem
Schnitt einen Ritzgang erzwungen. Die Maschinendateien nutzen fast durchweg `0`.

### `Bohrung`: Feld 8 ist ein POSITIONSCODE, keine Konstante (2026-09-02)

Die Beispielzeile unten (`…,10,0,0,2,0,…`) wurde lange als Vorlage kopiert —
das Feld 8 (`2`) ist aber ein Bezugs-/Positionscode. Beleg
eine lokale Referenzdatei (Format_1): ein 6-Loch-Muster nutzt bei identischen
XY-Werten die Codes 1/7/5/3 (vier Ecken) und 2/6 (Mitte untere/obere Kante) —
gleiche Koordinaten, verschiedene physische Positionen. Alle Referenzzeilen
mit Code `2` haben X=0. Für ABSOLUTE Koordinaten gilt Code `0` (so schreibt
HOPS' eigener DXF-Import, `Muster_DXF_Import.hop`) oder `1`
(`Rollbrett.hop`, handgeschrieben, gelaufen). **Wer Feld 8 aus der
Beispielzeile übernimmt, bohrt um DX/2 versetzt.** Gefunden im Audit des
Python-hop-Writers — der C#-Emitter sollte dieselbe Stelle prüfen.

### `Bohrung`: Reihenfolge ist (X, Y, **Durchmesser**, **Tiefe**, 10, …)
Nicht (X, Y, Fläche-Z, Ziel-Z, Durchmesser). Beispiel aus der Maschine:
`Bohrung (0,25,8,-16,10,0,0,2,0,0,0,0)` = 8-mm-Bohrer, 16 mm tief.
Bei falscher Reihenfolge liest HOPS die Oberflächen-Z als Durchmesser und meldet
„Bohrungsdurchmesser ist nicht korrekt" — einmal pro Zeile.

### `SP`: Feld 5 ist NICHT die Frässchritte — GEKLÄRT durch Dialogfoto (2026-08-29)

Foto des HOPS-`SP`-Dialogs zur Zeile
`SP (0,0,-0.2,2,1,_ANF,0,0,0,0,1,2,0,…)` aus `Tuer_links.hop`: im Dialog steht
**„Anzahl Frässchritte = 0"**, während Feld 5 in der Zeile `1` trägt. Damit ist
die alte Lesart widerlegt und der Korpus-Widerspruch unten aufgelöst — negative
Werte (`-1`, `-2`) sind für einen Schrittzähler nie plausibel gewesen. Welches
Feld die Frässchritte trägt, ist offen (Kandidaten: die Nullen in 7–10);
bis dahin bleibt `1` in Feld 5, das ist die belegte, gelaufene Form.

Der Dialog erlaubt außerdem die Zuordnung der beiden anderen gesetzten Felder:

| Feld | Dialog-Gruppe | Werte |
|---|---|---|
| 4 | Bearbeitungsart | `0` keine Änderung · `1` Gleichlauf (Umlauf ändern) · **`2` Gegenlauf (Umlauf ändern)** · Gleich-/Gegenlauf (Werkzeug ändern) |
| 11 | Anfahrbewegung | `0` keine · **`1` linear** · `2` radial · `3` seitlich |
| 12 | Radiuskorrektur | `0` mitte · `1` links · **`2` rechts** |

Fett = im Foto angehakt, passend zu `…,2,1,_ANF,0,0,0,0,1,2,…`. Das erklärt
auch die Ausnahmen im Korpus: die 9 SP-Zeilen mit Feld 11 = `0` sind genau die
tiefen Nut-/Falzdurchgänge — dort will man **keine Anfahrbewegung**, sonst
fährt das Werkzeug aus der Nut heraus.

**Konsequenz für den Writer (Maschine 2026-08-29):** der Fräser lief auf der
falschen Seite. Wir schreiben jetzt Feld 12 = `1` (links). Das passt zur
Laufrichtung: Umriss im Uhrzeigersinn → Werkzeug außen = links; Innenausschnitte
gegen den Uhrzeigersinn → Werkzeug innen = ebenfalls links. Eine Seite für
beides — genau deshalb ist die Richtungskonvention erzwungen.

### `SP`: Feld 5 ist die Anzahl Frässchritte — WACKELT (2026-08-29, überholt)
Ursprüngliche Lesart: nicht Feld 13; zwei Maschinendateien stimmen überein, und
`EP` wiederholt die Zahl:

```
kontouren nuter und fraesen.hop   SP (…,2,2,_ANF,…)  EP (2,_ANF,0)
Tritt_217.hop                     SP (…,2,3,_ANF,…)  EP (3,_ANF,0)
```

Mit `0` in Feld 5 fuhr die Kontur im damaligen Test **null Durchgänge**, ohne dass
irgendetwas meckerte.

**Widerspruch aus dem LEO-Korpus (2026-08-29, 214 Dateien, 126 SP-Zeilen):**
Feld 5 ist mehrheitlich `0` (85×) — in Dateien, die nachweislich gelaufen sind —
dazu `-1` (24×) und `-2` (8×). Die HOPS-eigenen `KSP`-Zeilen vor `KonturFraesen`
tragen fast immer `-2` (496×, sonst `0`). Eine „Anzahl Frässchritte" ist mit
negativen Werten nicht vereinbar, und `EP` spiegelt die Zahl nicht immer
(`David/Nummer 1.hop`: `KSP(…,-2,_ANF,…)` → `EP(2,_ANF,0)`). Entweder galt
„0 fährt nichts" nur im damaligen Testkontext, oder Feld 5 ist kein
Schrittzähler (Kandidat: Korrektur-/Modus-Code). Bis zur Klärung (offene
Frage 4 im Maschinenplan): weiter `1` schreiben — einmal belegt
(`SP (50,0,-0.2,2,1,…)`) und in beiden Deutungen harmlos.

**Felder 11/12 sind NICHT durchgehend `1,2`:** 9 SP-Zeilen tragen Feld 11 = `0`
(tiefe Nut-/Falzdurchgänge, `Linz_Steri_Raum/Arbeitsplatte*.hop` mit `z=-18`,
eine lokale Referenzdatei (Seite 1144x400) mit `z=-10`), 1× Feld 12 = `1`
(`Oben_Unten_HKV.hop`). Für Weltkoordinaten-Konturen wie aus dem Writer bleibt
`…,1,2,…` die mit Abstand häufigste und sichere Form (117× bzw. 125×).

### `SP`: Feld 10 ist ein Positionscode, Feld 7 gehört zur Kantenform (2026-08-29)

Korpus-Auszählung der 126 SP-Zeilen:

- **Feld 10:** `0` (96×), sonst `1`–`8` — dieselbe Codewelt wie Bohrung Feld 8.
  Die kantenbezogenen Formatkonturen (alle Koordinaten `0`) tragen hier exakt die
  Kantencodes `7/3/1/5` wie `ESXY1` in der korrigierten Gehrungsdatei. Beleg:
  `Tritt_217.hop`, `kÖNIGSALLEE/Treppe/Gitter.hop`,
  `Linz/Theke/Verblendung/Oben_links_VV.hop`.
- **Feld 7:** `0` in 116 von 126 Zeilen. **Alle** 10 Ausnahmen (`1`, `5`, `-2`,
  `-1`) stehen in ebendiesen Kantenform-Zeilen (z. B.
  `Linz/Theke/Seiten/Seite_links_von Vorne.hop`: `SP (0,0,-0.2,2,-2,_ANF,5,0,0,2,…)`).
  Feld 7 gehört also funktional zur Kantenform; Bedeutung offen → offene Frage 4 im Maschinenplan.
- **Feld 13:** `0` (121×), `2` (4×, tiefe Durchgänge), `3` (1×).

Für Weltkoordinaten-Konturen (Writer-Fall) war `…,0,0,0,0,1,2,0,…`
(Felder 7–13) die belegte Normalform. **Überholt seit 2026-08-29:** Feld 12
ist die Radiuskorrektur, und die Korpusdateien laufen alle gegen den
Uhrzeigersinn (69 von 69 geschlossenen Weltkoordinaten-Konturen, ausnahmslos
Feld 12 = `2`). Unser Umriss läuft im Uhrzeigersinn — dieselbe physische
Fräserseite heißt dann `1`. Writer-Normalform ist damit `…,0,0,0,0,1,1,0,…`.

### GEKLÄRT: `G01`/`G02M`/`G03M` tragen den Bezug in der Zeile selbst (2026-08-31)

Die Frage war als blockierend eingestuft — sie ist es nicht mehr. **Der
Bezugsschalter ist ein Feld in der G-Zeile: bei `G01` das Feld 5, bei Bogen-
zeilen Feld 7 (Endpunkt) und Feld 9 (Mittelpunkt). `0` = absolut,
`10` = Wegstrecke.** Damit erklären sich beide Formen im Korpus, ohne dass
eine von ihnen falsch wäre.

Auszahlung über alle 215 Dateien, nur Blöcke mit einheitlichem Marker:

| Marker | Blöcke | mit negativen Koordinaten | außerhalb Teil +60 mm |
|---|---|---|---|
| `0` | 19 | 3 | 3 |
| `10` | 43 | **42** | 0 |

Negative Koordinaten sind bei absoluter Lesart auf einem Teil 0…DX unmöglich —
42 von 43 Marker-10-Blöcken haben sie, kein einziger läuft aus dem Teil heraus,
wenn man ihn als Wegstrecke liest. Die drei Marker-`0`-Blöcke mit negativen
Werten sind kein Gegenbeleg: eine lokale Referenzdatei (Brett) startet
dort bei `SP (109.63,-20)` und fährt nach `(109.63,90.5)` — ein Anfahren
20 mm unterhalb der Kante, absolut gelesen völlig plausibel.

**Zwei frühere Vermutungen sind damit widerlegt:** weder `;EXEVERSION` noch die
Form des `WZF`-Aufrufs ist der Schalter. Die `*_Schablone_hinten.hop`-Dateien
sind absolut, tragen Version 7.7.12.80 UND die Werkzeugform `…,_ANF,'1')` —
genau die, die der Writer schreibt.

**Der Writer schreibt bereits richtig.** `G01 (x,y,0,0,0,2)` hat Feld 5 = `0`,
und die Bogenzeile `G02M/G03M (x,y,0,cx,cy,0,0,2,0)` ist feldgleich mit der
belegt-absoluten Form aus `Muster_DXF_Import.hop`
(`G03M (500,130,0,400,130,0,0,2,0)`). Nichts zu ändern — die Alarmstufe
„jede exportierte Kontur ist unsicher“ ist zurückgenommen.

Vermutete Gesamtsemantik: dasselbe Codefeld wie `SP` Feld 10 und `Bohrung`
Feld 8 — `0` Weltkoordinaten, `1`–`8` Kantenanker, `10` inkrementell. Passt zu
`Tritt_217.hop:107-108`, wo `SP` Feld 10 = `7` und `G01` Feld 5 = `7`
zusammen auftreten.

### `_saege_y_V7` endet auf `ESY`, nicht `ESX`
`_saege_x_V7` → `ESX`, `_saege_y_V7` → `ESY`. Vertauscht bricht HOPS mit
„Variable existiert nicht in Makro oder ist falsch definiert [ESX]" ab.

---

## Kopfzeilen

`;DX` `;DY` `;DZ` im Kopf sind **Offsets**, keine Maße. Alle ~200 Maschinendateien
tragen dort `0.000/0.000/0`. Die Maße stehen ausschließlich im `VARS`-Block.

Maße gehören gerundet: Bounding-Box-Rauschen wie `19.0002` darf nicht als
Plattendicke an die Maschine.

---

## Formatschnitte: zwei Schreibweisen

Die Maschine schreibt Formatschnitte **kantenbezogen**: alle Koordinaten `0`, und
`ESXY1` benennt die Kante (in der korrigierten Gehrungsdatei `7, 3, 1, 5`). Beide
Gehrungen tragen dann dasselbe `KW`, weil der Winkel relativ zur Kante gilt.

Das Plugin schreibt stattdessen **Weltkoordinaten** und braucht deshalb `±KW`.

**Bewusst nicht umgebaut.** Die korrigierte Datei wurde von HOPS selbst neu
geschrieben (`;EXEVERSION=…[Hops.exe]`), die Kantenform könnte also nur dessen
interne Darstellung sein. Und die Koordinaten des Plugins waren rechnerisch
korrekt: erwartete Fertigbreite 460,786 mm, aus den Plugin-Koordinaten errechnet
460,79 mm.
**Offener Test:** einmal unverändert exportieren, in HOPS laden, 3D-Simulation.
Landen die vier Schnitte an den richtigen Kanten, bleibt es bei Koordinaten.
Die Kantennummerierung ist ohnehin unbelegt — sie aus einer einzigen Datei zu
raten, wäre an der Maschine teuer.

**LEO-Korpus (2026-08-29): die Kantenform ist gelebte Praxis, kein
Re-Save-Artefakt.** Alle 188 `_saege_x/y_V7`-Aufrufe in den 214 Dateien setzen
`ESXY1`; nur 13 stehen auf `0` — ausnahmslos freie Schrägschnitte mitten im Teil
(`Steher _Stoss.hop`, `Brett_Duft_Spender.hop`). 103 Aufrufe sind reine
Kantenform (alle Koordinaten `0`, `ESXY1` ∈ 1/3/5/7 → 59/38/34/44), 72 eine
**Mischform**: Kantencode UND Koordinaten ≠ 0 (`Gitterboxen/Aufsätze/Format.hop`:
`SY:=10, ESXY1:=1` — Schnitt mit Abstand zur Kante). Das Plugin-Weltkoordinaten-
Schema mit `ESXY1:=0` ist im Korpus also die absolute Ausnahme, nicht die Norm.
Der Simulationstest bleibt der Entscheider, aber die Beweislast hat sich gedreht.
Zusatzcode: die Sägemakros führen neben `ESXY1` einen zweiten Kantencode
(`ESX`/`ESY`, z. B. `ESXY1:=7, ESY:=3`), Bedeutung unbelegt.

**`KW` ist im Korpus nie positiv:** 140× `0`, 48× negativ (−2.4 … −90), kein
einziges `+KW` in 214 Dateien. Die ±KW-Strategie des Plugins benutzt mit `+KW`
einen komplett unbelegten Ast — die offene Frage „Kipprichtung von positivem KW"
ist damit dringlicher, nicht kleiner.

---

## Fallstricke im Plugin, die stille Fehler erzeugt haben

**Polylinien wurden nie abgefahren.** Rhino gibt eine Polylinie an mehreren Stellen
als *ein* Kurvenobjekt zurück; `ToArcsAndLines` liefert einen `PolyCurve`, dessen
einzige Spanne die ganze `PolylineCurve` ist. Gelesen wurde sie als eine Linie von
Start- zu Endpunkt:

- offene Polylinie → gerade Linie quer über das Teil, alle Zwischenpunkte weg, **ohne Meldung**
- geschlossene Polylinie → Start = Ende, Kontur kollabiert auf einen Punkt

Derselbe Fehler, nur einmal sichtbar. Eine geschlossene Polylinie mit 5128 mm und
15 Spannen wurde zu einer Spanne. `DuplicateSegments()` hilft nicht — es gibt eine
`PolylineCurve` unverändert zurück. Die Zerlegung muss **nach** beiden
Flattening-Zweigen greifen.

**Diagnose schlägt Raten.** Zwei Runden Vermutung („Kreis? Spline?") brachten
nichts; eine Fehlermeldung, die Kurventyp, `closed`, Länge, Grad und die
Zerlegungswerte ausgibt, hatte die Antwort beim ersten Versuch. Die Meldung trägt
zusätzlich ein `BuildTag`, damit eine veraltete `.gha` im Libraries-Ordner von
einem echten Fehler unterscheidbar ist.

**Rhino sperrt die installierte `.gha`.** Der Build kann nicht kopieren, solange
Rhino läuft. `-p:SkipGhaInstall=true` baut und testet ohne den Kopierschritt.

---

## Werkzeuge in diesem Repo

- `tools/pruefe_hop.py <datei.hop>` — prüft eine erzeugte Datei auf innere
  Widersprüche. Rechnet aus den Überständen der Durchgangsschnitte zurück, welche
  Teilegröße die Bearbeitungen annehmen, und vergleicht sie mit `VARS`. Genau diese
  Arithmetik hat den 90-Grad-Dreher zwischen `DX`/`DY` und den
  Bearbeitungskoordinaten gefunden.
- `HopAnalyzer` — Teil-Rahmen-Wache (Bearbeitung mehr als 60 mm außerhalb des
  deklarierten Teils = Fehler, der normale 50-mm-Überstand nicht) und eine Warnung
  für Konturen, deren Bewegungen alle auf dem `SP`-Punkt liegen.

---

## Rückwandnut — Praxis an der Maschine (Quelle: Werkstatt, 2026-08-29)

**`_nuten_frei_v5` ist das SÄGE-Makro — für uns unbrauchbar (Maschine 2026-08-29).**
Die exportierte Datei lud sauber, HOPS zeigte `Freies_Nuten` im Baum und der
Dialog alle Werte richtig (SX/SY/EX/EY, `L2` = Nutbreite 8,5, `L3` = −8,
Nutlage mitte, Gleichlauf) — **die Simulation fuhr die Nut trotzdem nicht.**
Statuszeile: „Sägeblattbreite oder Werkzeug nicht definiert", und das
Dialogbild zeigt ein Sägeblatt. Das Makro erwartet die Nutsäge, ein `WZF` auf
einen Fräser erfüllt es nicht. Deshalb schreibt der Writer die Nut jetzt als
**normale Fräserbahnen** (`SP`/`G01`/`EP`) — kein Makro, keine Sägeblattfrage.

- Die Rückwandnut läuft über den **Nuter** (Fräser), nicht über die Nutsäge.
- Werkzeug ist der **4mm Nuter** = Schneiden-ID **8** (Werkzeugdatei
  `reference-hops/*.too`, ToolData21: ToolNo 19, NominalRadius 2, einzige
  Schneide mit ID 8; Werkstatt: „der 4mm"). `WZF (8,…)` ist 8× in `leo-design` belegt. Der
  6mm Nuter (Schneiden-ID 6) bleibt für anderes.
- Für eine 8-mm-Rückwand ist die Nut **8,5 mm breit** (0,5 mm Luft). Der Nuter
  fährt **drei Zustellungen**: vordere Nutflanke (0), hintere Flanke (8,5),
  dann die Mitte — mit D4 gehen sich die drei Läufe genau aus.
- Umsetzung im Writer (`holzher_hop.nut`): Bahnmitten um ±(NB−D)/2 = ±2,25
  quer zur Nutmittellinie versetzt, dann die Mitte; `SP`-Z = **−Tiefe von der
  Oberkante** (also −8, nicht +11), Feld 11 = `0`, Feld 12 = `0`.
- **Präzedenzfall für genau diesen Fall** (Audit 2026-08-29):
  eine lokale Referenzdatei (Rueckwand) Z. 104–107 fräst eine Nut mit
  demselben Werkzeug (`WZF (8,…)`) als freie `SP`/`G01`/`EP`-Bahn auf einem
  19-mm-Teil: `SP (70+9.5+3.5,0,-10,0,0,_ANF,0,0,0,0,0,2,2,…)`. Tiefe von
  oben, Feld 11 = `0`. Die Kombination **Feld 11 = `0` mit positiver Z gibt es
  in 722 Korpuszeilen kein einziges Mal** — dort steht immer eine tiefe
  negative Z (−1 … −18), während Feld 11 = `1` mit positiver Z oder −0,2
  einhergeht. Wer bei Feld 11 = `0` die Tischhöhe schreibt, fräst Luft.
  Einzige bewusste Abweichung vom Präzedenzfall: dort Feld 12 = `2`, bei uns
  `0` — der Präzedenzfall programmiert die Nutkante, wir die Bahnmitte.
- Umsetzung in der Kette: KorpusBau schreibt `nut_b = Rückwanddicke + 0,5`,
  TeilExport reicht das als `NB` an `_nuten_frei_v5` durch. Ob das Makro die
  drei Zustellungen selbst erzeugt, klärt T10 (Simulation) — bis dahin bleibt
  die Export-Warnung stehen.

---

## Abgleich gegen den LEO-Design-Korpus (2026-08-29)

Auf Council-Empfehlung (Maschinenplan v2) wurden alle Regeln oben gegen die 214
Dateien unter `reference-hops/` ausgezählt (204 LEO-Design + 10 Key-Dateien,
alle `;EXEVERSION=7.7.12.80`). Korrigiert: SP Feld 5 („0 fährt nichts"),
Felder 11/12, Bohrung-Feld-9-Pauschale. Neu belegt: ESXY1-Praxis und
KW-Vorzeichen, SP Felder 7/10, Park-MODE-Verteilung, Fertigteil-Formen,
Tisch-Bezug mit positiven Z-Werten.

Nut-Makros: **`_nuten_frei_v5` kommt in keiner der 214 Maschinendateien vor**
(einziger Beleg bleibt `Muster_DXF_Import.hop`, HOPS' eigener DXF-Import); die
Praxis nutzt `_Nuten_X_V5` (10 Aufrufe), eine `_Nuten_Y_V5` existiert nirgends.
Ob `_nuten_frei_v5` die drei Zustellungen der Rückwandnut (siehe oben) selbst
erzeugt, prüft T10 — der Korpus kann es nicht belegen.

Zwei Unschärfen in `reference-hops/LEO-DESIGN-ANALYSIS.md` fielen dabei auf:
die Beschreibung `_saege_*_V7 (±Gehrung)` suggeriert positive Winkel (im Korpus
unbelegt), und die Wrapper-Empfehlung „`_Nuten_X/Y_V5`" nennt eine Y-Variante,
die es nicht gibt.

---

## Was die Maschine noch klären muss

| Frage | Warum offen |
|---|---|
| Kipprichtung von positivem `KW` | Vorschau wurde nach einem Vergleich mit HOPS gedreht; an der Maschine ungeprüft. Verschärft 2026-08-29: der Korpus enthält **kein** positives `KW` (140× `0`, 48× negativ; `KW:=-90` = horizontale Nutsäge) |
| Koordinaten- vs. Kantenform der Formatschnitte | siehe oben, Zwei-Minuten-Simulationstest. Korpus 2026-08-29: 175 von 188 Sägeaufrufen nutzen Kantencodes, nur 13 die reine Koordinatenform |
| `SP` Feld 5 (Schrittzahl?) und Feld 7 | „Anzahl Frässchritte" durch Korpus widerlegt bzw. kontextabhängig (85× `0`, 32× negativ in gelaufenen Dateien); Feld 7 nur in Kantenform-Zeilen ≠ 0 → offene Frage 4 im Maschinenplan |
| ~~Fünftes Feld der `Bohrung`~~ **GELÖST, Codetabelle belegt 2026-08-29** | Feld 5 = HOPS-**Bohrtyp-Code** (Dropdown im Werkzeugdialog): `00` Standard · `10` Sackloch · `20` Durchgangsloch · `30` Topfband · `40` Senker · `50`–`70` unbelegt. Beleg: Foto des HOPS-Werkzeugdialogs, Bohrer D35 steht auf „(30) Topfband" (Quelle: Werkstatt, 2026-08-29). Frühere Belege passen dazu: 6 Referenzzeilen `30` = D35, Live-Meldung 'Bohrer (Type 1) mit Durchmesser 35.00 existiert nicht' bei `10`, Topf35 = DrillingType 3 in der Werkzeugdatei (`reference-hops/*.too`) (Code = Typ × 10). Praxis: normale Bohrungen mit `10` (Sackloch, „immer die 10"), Durchgänge bei uns weiter als `10` + Tisch −0.2/Feld 9=1 (Rollbrett-Muster) — ob stattdessen `20` üblich ist, ist offen, aber nicht nötig. Topfdurchmesser (D15/D35, ±0.1) schreiben beide Writer mit `30`: `holzher_hop.py` und, seit 2026-10-14, `NcDrill.DrillLine` — vorher schrieb das C#-Plugin auch Topfbänder der Korpus-Türen mit `10`. |
| Kontur-Vorschau in Rhino | stimmt nicht zu 100 %; Ausgabe ist belegt korrekt, die Darstellung noch nicht geprüft |
| ~~`Fertigteil` mit 12 oder 13 Parametern, `HH_Park` vs. `Park_V7`~~ **WEITGEHEND GELÖST 2026-08-29 (LEO-Korpus)** | `Fertigteil`: 123× 12 Felder, 91× 13 Felder, beide aus derselben HOPS-Version 7.7.12.80 → gleichwertig; die Writer-Zeile `Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)` ist wortgleich die häufigste. 13. Feld fast immer `0` (1× `19`, `Rohr_2.hop`); Felder 6/7 tragen vereinzelt Aufmaß-artige Werte (`2/1.5`, `50/50`, `100/100`, `250/250`, `-10/-20`), Felder 1/2 dürfen Ausdrücke sein (`KB-38-35`, `NL-10`). `HH_Park` kommt in **keiner** der 214 Dateien vor; Standard ist `Park_V7 MODE:=2` (132×, darunter alle 127 Nesting-Dateien und 5 Einzelteile), `MODE:=3` nur im Cluster Linz/Theke/Unterkonstruktion (15×), `MODE:=10` parkt auf Position (`POSX:=2200`, 3×), `MODE:=5` 1×. Offen bleibt nur die MODE-Semantik (offene Frage 7 im Maschinenplan); der Python-Writer schreibt seit 2026-08-29 den Korpus-Standard `MODE:=2` (vorher `MODE:=3` — belegt, aber Minderheitsform), das C#-Plugin seit 2026-10-14 ebenfalls (`NcExport.ParkLine`, vorher `HH_Park ( VAL PARK:=3,X:=0,Y:=0)` in HopExport/HopJob/HopPartExport/HopSheetExport); ein `Park_V7` aus HopNesting ersetzt dort den Standard und rückt direkt hinter `Fertigteil`, gefolgt vom übrigen Nesting-Block in Maschinenreihenfolge und -schreibweise (`HH_MarkLabel ( VAL …)`, `BN_TrennerInnenAussen ()`, `BN_NestKontur ()` — so in allen 127 Nesting-Dateien; `NcExport.StartSection`, seit 2026-10-14 auch in HopPartExport/HopSheetExport). Herkunft der alten Zeile: `HH_Park ( VAL PARK:=3,X:=0,Y:=0)` steht im DXF-Import-Muster `reference-hops/dxf-layer-import/Muster_DXF_Import.hop` (geschrieben von HOPS 5.5); der HOPS-7.7-Korpus kennt nur `Park_V7`. Für den Park-Aufruf gilt deshalb der Korpus, nicht das Muster (Ausnahme zur Regel in `KONVENTION.md`); `test/test/validate_hop.sh` warnt bei `HH_Park`. |
