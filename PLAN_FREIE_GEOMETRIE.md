# Plan: freie Geometrie → .hop (HolzHer 7535D)

Stand 2026-08-31. Erstellt von einem Planungs-Agenten (Opus 5) nach Lektüre des
Bestands + Auszählung der 215 Referenzdateien, danach vom **LLM-Council
geprüft (2026-08-31)** und an sechs Stellen korrigiert. Die Council-Eingriffe
sind im Text als solche markiert.

> **Kernkorrektur des Councils:** Der Plan widersprach sich selbst — §9 nannte
> den T13-immunen Umfang „den kompletten MVP“, während §4 und §6 die ungeklärte
> freie SP/G01-Bahn mit einer bloßen Warnung in Phase 1 mitnahmen. Freie
> Konturen sind jetzt **gesperrt** (Fehler, nicht Warnung) bis T13 entschieden
> ist, und bilden die eigene Phase 1b.

Ziel: Leo zeichnet freie Geometrie in Rhino, weist zu was Kontur / Bohrung /
Nut / Schnitt ist, und bekommt `.hop` + `.dxf`. Heute geht das nur für Teile
aus dem KorpusBau-Generator.

> **Stand 2026-09-06: Phase 1 GEBAUT.** `holzher_teil.py` (gemeinsamer Kern,
> aus TeilExport extrahiert), `CncZuweisen` / `CncSchau` / `CncExport`,
> `holzher_hop.format_v5()` + `kreistasche()` + `BAHN_ABSOLUT`, Lücke 0.4
> geschlossen, `tisch_max_mm` (konservativ 2800×2070 = größtes gelaufenes
> Referenzmaß) + `fraeser_kontur_d` in werkzeuge.json. Abnahme: 43/43
> TeilExport (Refactoring-Beweis) + 76/76 KorpusBau + 45/45 LayerExport +
> 37/37 CncExport, `pruefe_hop` meldet konsistent.
> **Zwei dokumentierte Abweichungen:** (1) §9-Abnahme „textgleich bis auf
> NCNAME" kollidiert mit „keine SP-Zeile" — TeilExport fährt die belegte
> SP-Bahn (Tests pinnen sie), CncExport das T13-immune `_Format_V5`; die
> Äquivalenz prüft deshalb VARS + Bohrzeilen textgleich, volle Textgleichheit
> gibt es erst ab Phase 1b. (2) DX/DY im Makro-Weg = FERTIGmaß (so laufen
> alle `_Format_V5`-Referenzen); §3.1 „DX/DY vom Rohteil" gilt für den
> SP-Grundplatten-Weg ab Phase 1b. Kantenbohrkreis-Setzhilfe wartet auf
> Phase 3 (ihr einziger Prüfpfad wäre der dort erst freigegebene Export).
> **T13-Lage:** papierseitig beantwortet (MASCHINENABGLEICH 2026-08-31,
> G01 Feld 5 = Bezugsschalter, Writer schreibt bereits die absolute Form) —
> die Sperre in `pruefe()` bleibt trotzdem, bis die Phase-1b-Abnahme
> (Freiformkontur läuft in der HOPS-Simulation sauber) den Papierbefund an
> der Maschine bestätigt. Nächster Schritt Richtung 1b: `T14_Makro_MVP.hop` (lokale Testbatterie)
> plus eine Freiform-Testdatei in die Simulation geben.

---

## 0. Vier Befunde aus dem Bestand, die den Plan prägen

### 0.1 Die Absolut-Belegstelle ist die einzige Datei einer FREMDEN HOPS-Version

`;EXEVERSION` über alle 215 Referenzdateien: **214× `7.7.12.80`, 1× `5.5.2.219`**
— und diese eine ist `dxf-layer-import/Muster_DXF_Import.hop`, also genau die
absolute Belegstelle. `leo-design/MAMA/Brett.hop` (inkrementell, inkl.
Bogenmittelpunkte relativ zum Segmentstart), `Rollbrett.hop` und
`Gluecksrad/Rueckwand.hop` sind alle `7.7.12.80`.

**Für die Zielversion gibt es damit keinen einzigen Absolut-Beleg.** Die offene
Frage aus MASCHINENABGLEICH.md steht schlechter da, als sie dort formuliert ist.
T13 ist Blocker Nr. 1 — auch für das neue Werkzeug.
*(Nachgeprüft und bestätigt am 2026-08-31.)*

### 0.2 `Fertigteil` ist das ROHTEIL, nicht der Umriss

In `Muster_DXF_Import.hop` ist `DX := 800 / DY := 400` die weiße Grundplatte,
während die freie Kontur von X≈33 bis X≈785 läuft. `TeilExport` setzt heute
DX/DY = Bounding Box des Umrisses — das stimmt nur, weil Korpusteile Rechtecke
sind. **Bei freier Geometrie ist das falsch**, sobald der Umriss kein Rechteck
ist: der Fräser liefe exakt auf der Rohteilkante.

### 0.3 Es gibt einen Weg an T13 vorbei — für den MVP-Fall

Für rechteckige Platten und runde Löcher/Ausschnitte braucht man **keine
einzige SP/G01-Zeile**:

| Bearbeitung | belegte Form (7.7) | aktive Aufrufe |
|---|---|---|
| Rechteck-Außenkontur auf Maß | `CALL _Format_V5 (VAL ECKENVERRUNDUNG:=0,TIEFE:=-0.2,AUFMASS:=0,ANF:=2.5,ESMD:=1,GGL:=2,LASER:=0,DEPTH:=0)` unter aktivem `WZF` | 61 aktiv / 31 auskommentiert |
| runde Tasche / rundes Loch | `_Kreistasche_V5` mit `ESXY:=0` | 42 aktiv, davon 3× `ESXY:=0` |
| Rechtecktasche | `_RechteckTasche_V5` mit `esxy:=0` | 37 aktiv, davon 4× `esxy:=0` |
| Bohrung | `Bohrung (…,Feld 8 = 0)` | überall |

Diese Formen tragen **absolute Koordinaten in benannten Parametern** — die
Absolut/Delta-Frage kann sie gar nicht treffen. Der MVP wird darauf gebaut.

Gegenprobe: `_Rechteck_V7` hat in 17 aktiven Aufrufen **kein einziges
`ESXY:=0`** (nur 1/6/8/9) — für absolute Rechteckkonturen unbelegt, wird nicht
benutzt. `ESXY:=9` (sehr häufig) liegt außerhalb der 0–8-Keypad-Welt und ist
unbelegt; wir schreiben es nicht.

### 0.4 Zwei Lücken im Bestand, die beim Verallgemeinern teuer würden

- `HolzHerLayerExport.export()` prüft Bohrdurchmesser nur gegen `bohrkopf_mm`.
  **Kantenbohrungen werden gegen dieselbe Liste geprüft**, obwohl
  `werkzeuge.json` ein eigenes `bohrkopf_horizontal_mm: [8, 10]` führt. Ein D5
  in der Kante fällt heute nicht auf.
- `TeilExport.teil_export()` enthält die **Reihenfolge-Regel** (Nut → Bohrung →
  HorzB → Innenausschnitte → Außenkontur zuletzt) als losen Code im Fließtext.
  Wird sie in einem zweiten Befehl nachgebaut, driftet sie. Sie ist eine
  Sicherheits-Eigenschaft, keine Stilfrage — sie gehört an genau eine Stelle.

---

## 1. Datenmodell

### Kandidaten

**(a) Tag/UserText erweitern.** Existiert bereits: `KorpusBau.bohrung()` schreibt
`part`/`depth`/`process`, Nutlinien tragen `kind="nut"`, `nut_b`, `nut_t`.
`TeilExport` und `HolzHerLayerExport` lesen `depth` schon. Tag/TagAudit/TagDot/
TagLeader arbeiten generisch auf UserText.
*Nutzen:* überlebt Speichern/Kopieren/Verschieben; Idempotenz; TagDot zeigt es
an; der Export liest denselben Schlüssel wie heute.

**(b) Layer-Konvention wie beim HOPS-DXF-Import.** Fällt aus einem harten Grund
durch: die HOPS-Konvention trägt die **Tiefe nicht im Layernamen, sondern in der
DXF-Objekthöhe (Code 39)** — und KONVENTION.md hält fest: „Rhino-Kurven haben
keine Objekthöhe". Die Tiefe variiert pro Loch. Also bräuchte man neben dem
Layer trotzdem UserText — halbes System, doppelte Wahrheit. Dazu sind Layer hier
bereits von KorpusBau vergeben. Übernommen wird nur die belegte
**Kantenbohr-Geometrie** (Kreis außerhalb der Kontur, Überstand = Z-Höhe).

**(c) Reine Dialogauswahl ohne Persistenz.** Scheitert am zweiten Export: Leo
sieht in der DXF-Kontrolle einen Fehler, ändert ein Loch, exportiert erneut —
und muss alles neu picken. Bei 40 Löchern hört er auf, das Werkzeug zu benutzen.
Außerdem: keine Vorschau, keine Prüfung vorab, kein Wiederfinden nach vier
Wochen.

### Entscheidung: (a), aber der Bedienfluss fühlt sich an wie (c)

Der Zuweisungsbefehl schreibt die Tags im Vorbeigehen — Leo merkt vom
Datenmodell nichts, außer dass es beim zweiten Mal noch da ist.

| Key | Werte | vorhanden? |
|---|---|---|
| `kind` | `rohteil`/`kontur`/`ausschnitt`/`bohrung`/`kantenbohrung`/`nut`/`tasche`/`gravur`/`saege` | ja (`nut`) |
| `depth` | `durch` oder mm | ja |
| `nut_b`, `nut_t` | mm | ja |
| `thickness`, `material` | am Rohteil bzw. Umriss | ja |
| `part` | Teilname; leer = ein Teil je Auswahl | ja |

**Kein `werkzeug`-Key am Objekt** — KorpusBau begründet das bereits: „das
Tool-Mapping gehoert in den Export, sonst vergiftet ein Bohrerwechsel alte
Modelle". Werkzeuge kommen zur Exportzeit aus `werkzeuge.json`.

**`kind=kantenbohrung` wird explizit getaggt**, nicht aus der Kreisnormalen
abgeleitet: bei Handzeichnung ist ein Kreis 3 mm neben der Kante mehrdeutig.

### Idempotenz

| Änderung in Rhino | Verhalten |
|---|---|
| Kurve verschoben/skaliert/getrimmt | Tag bleibt, neue Lage. Gewollt. |
| Kurve gelöscht und neu gezeichnet | Neues Objekt ohne `kind` → Export **bricht laut ab** und selektiert es |
| Loch dazugezeichnet | dito, nur das neue Objekt taggen |
| Zweiter Export ohne Änderung | **byteidentisch** (kein Zeitstempel im Kopf) — als Test abgesichert |
| Teil umbenannt | neuer Dateiname, alte Datei bleibt liegen (nichts löschen, was an der Maschine liegen könnte) |

---

## 2. Bedienfluss: zwei Befehle

**`CncZuweisen`** — Zuweisung in einer Schleife, nach dem Muster
`KorpusBau.parameter()` (`Rhino.Input.Custom.GetOption` + `AddOptionList` /
`AddOptionDouble` / `AddOptionToggle`, Werte in `sc.sticky`):

```
Objekte waehlen (Enter = fertig)
> Art=Bohrung  Tiefe=13  Durch=Nein  Werkzeug=501
```

`rs.GetObjects` liefert das Hover-Highlight — **kein eigener DisplayConduit**.
(`_KantenPicker` existiert nur, weil dort eine Rechteckkante gepickt wird, die
kein Objekt ist. Hier gibt es Objekte.)

| Art | Optionen | Default |
|---|---|---|
| Rohteil | Staerke, Material | `thickness_stock`-Snap, letztes Material |
| Kontur / Ausschnitt | Durch, Tiefe, Zustellungen | Durch=Ja, Fräser 20 |
| Bohrung | Durch, Tiefe | 13; D aus der Kreisgeometrie; WZB 501; Topf über `topf_mm` |
| Kantenbohrung | Tiefe, Hoehe | Höhe = thickness/2 |
| Nut | Breite, Tiefe | letzte Breite, Fräser 8 / D4 |
| Tasche | Tiefe, Durch, Radius | — |
| Gravur | Tiefe | 1.0 |

Zwei Komfortfunktionen (~20 Zeilen, töten je eine Fehlerklasse):

- `Art=Kantenbohrung` bietet an, den Kreis **selbst zu setzen** (Punkt auf der
  Kante + Höhe → Kreis landet außen mit richtigem Überstand).
- `Art=Bohrung` auf eine Nicht-Kreis-Kurve → Fehler mit Objektnamen statt
  stillem Ignorieren.

**Mehrfachauswahl ist Pflicht, nicht Komfort** (Council 2026-08-31): erst
N Objekte wählen, dann EINMAL `Art=Bohrung Tiefe=13` setzen — die Optionen
gelten für die ganze Auswahl. Der Durchmesser kommt je Kreis aus der Geometrie,
nicht aus der Option. Ohne das scheitert das Werkzeug genau an dem Fall, den
Leo genannt hat (40 Löcher in einem Teil), und wird zum Fließband.

**`CncExport`** — Sammeln, Prüfen, Schreiben. Vor dem Schreiben **immer** ein
Bericht in der Befehlszeile:

```
Platte_mpx-birch_19mm   Rohteil 800 x 400 x 19
   1 Kontur   durch, Fraeser 20, Radiuskorrektur links, im Uhrzeigersinn
   2 Ausschnitt  durch
  12 Bohrung   D5 T-13 (Bohrkopf ok)
   0 Kantenbohrung / Nut / Tasche
  WARNUNG: Konturbahnen ABSOLUT geschrieben - T13 an der Maschine offen
  -> hop\Platte_mpx-birch_19mm.hop  +  dxf\...
```

`Vorschau=Ja` schreibt nichts. Die DXF-Sichtkontrolle bleibt die grafische
Kontrolle. **`CncSchau`** — TextDots je Objekt auf Layer `CNC::Schau`, nach dem
`TagDot`-Muster (~60 Zeilen) — gehört **in Phase 1**, nicht in Phase 2
(Council 2026-08-31): Leo taggt allein, ohne Gegenkontrolle; die einzige
Prüfinstanz darf nicht eine Phase nach dem Risiko kommen, das sie absichert.
Ein scrollender Kommandozeilenbericht ist keine visuelle Kontrolle.
*Nicht* die Originalobjekte umfärben — das wäre eine Änderung an Leos
Zeichnung.

---

## 3. Teildefinition

1. **DX/DY vom Rohteil**: geschlossenes Rechteck mit `kind=rohteil` (= die weiße
   Grundplatte der HOPS-Konvention), BBox auf 0,1 mm gerundet.
2. **Fehlt das Rohteil**: DX/DY = BBox des Umrisses + **WARNUNG** („der Fräser
   läuft exakt auf der Rohteilkante"). Für den rechteckigen MVP korrekt und
   still, für Freiform die Warnung, die Späne spart.
3. **DZ** aus `thickness`. Fehlt es → **Fehler**, kein Default (Muster: der
   `depth`-Wächter, „ein vergessener Tag wuerde sonst still zum Durchschuss").
4. **Nullpunkt**: Rohteil-BBox-Min → (0,0), Kantenbohrkreise ziehen die Box
   nicht auf — `holzher_kontur.to_origin(konturen, extra)` kann das bereits.
5. **Orientierung**: gezeichnet wird in Welt-XY-Draufsicht, +Z ist oben.
   - Z-Spanne > 10× Toleranz → **Fehler**. Bewusster Verzicht auf Raten:
     `flach_transform` darf raten, weil ein Brep + getaggte Bohrnormalen da
     sind. Bei freier Geometrie gibt es nichts zu raten.
   - **Kein automatisches Querformat** — Leo hat die Lage selbst gewählt.
     Option `Querformat=Ja` als Opt-in, Hinweis wenn DY > DX.
   - **`tisch_max_mm` ist Pflichtfeld** in `werkzeuge.json`, nicht offene Frage:
     bis die Werkstatt das echte Maß nennt, steht dort ein **konservativer Default**,
     und eine Überschreitung ist ein **Fehler**, keine Warnung. Grund (Council):
     freie Geometrie erlaubt erstmals Teile, die größer sind als jede
     Korpusplatte — das ist neu und fällt sonst erst an der Maschine auf.

---

## 4. Bearbeitungsarten, nach Nutzen pro Risiko

| # | Art | Beleglage | Restrisiko | Phase |
|---|---|---|---|---|
| 1 | Bohrung vertikal | maximal (Feld 8 = 0, Feld-5-Typcode per Dialogfoto, T05 unterwegs) | gering — Bohrzeilen sind in beiden Epochen absolut | 1 |
| 2 | Rechteck-Außenkontur via `_Format_V5` | 61 aktive Aufrufe in 7.7 | sehr gering, **immun gegen T13** | 1 |
| 3 | Runder Ausschnitt via `_Kreistasche_V5`, `ESXY:=0` | 42 aktiv | gering, immun gegen T13 | 1 |
| 4 | Freie Kontur (SP/G01/G02M/G03M) | Writer läuft heute schon | **T13** — ungeklärt | **1b, GESPERRT** bis T13 entschieden |
| 5 | Kontur mit Tiefe / Gravur | `Oben_Unten_HKV.hop` (z=+16.5 bei DZ 19) | rk/Anfahrt unbelegt → `rk=MITTE`, `anfahrt=KEINE` + Warnung | 2 |
| 6 | Mehrfachzustellung | siehe unten, **ohne** SP-Feld 5 lösbar | gering | 2 |
| 7 | Nut (freie Linie) | `nut()` existiert, Präzedenz Gluecksrad, T10 unterwegs | mittel bis T10 zurück | 2 |
| 8 | Rechtecktasche | 37 aktiv, `esxy:=0` belegt | gering | 3 |
| 9 | Kantenbohrung | nur 90° belegt, T09 unterwegs | mittel | 3 |
| 10 | Sägeschnitt | 188 Aufrufe, aber **175 in Kantenform**; Weltkoordinatenform nur 13. Sägen-ID fehlt in `werkzeuge.json` | **hoch** | 4, nach Maschinentest |
| 11 | Freiform-Tasche (`_ExecutePocket_V5`, 4×) | dünn | hoch | nicht geplant |

### Mehrfachzustellung ohne SP-Feld 5

SP-Feld 5 ist offen, `ZUTIEFE:=_AT_MAXDEPTH` gibt es nur in Makros. Für freie
Konturen unnötig: **dieselbe Bahn mehrfach unter EINEM `WZF`, mit fallendem Z**
— genau das Muster, das `holzher_hop.nut()` bereits fährt und das
`Gluecksrad/Rueckwand.hop` belegt. Kosten: Dateigröße. Nutzen: 38-mm-Platten
machbar, und das Teil bleibt bis zum letzten Durchgang im Verbund.

`zustellungen=n` → n Blöcke, z interpoliert von `-t_max` bis `DURCH`, letzter
Durchgang trennt.

---

## 5. Validierung

### 5.1 In Rhino: `holzher_teil.pruefe(teil, wzg) -> (fehler, warnungen)`

**Fehler** (verhindern die Datei):

- Objekt ohne `kind` → Fehler + Objekte werden selektiert. Nie still verwerfen.
- `kind`-Wert unbekannt (Tippfehler-Schutz).
- `thickness` fehlt / `depth` fehlt / Tiefe = 0 (belegter Stumm-Fall).
- Tiefe > `thickness` und nicht `durch` → trennt statt zu fräsen.
- Kein Umriss oder mehr als einer je `part`.
- Umriss/Ausschnitt **offen** (nach `MakeClosed(GAP_TOL)`).
- Bearbeitung außerhalb des Umrisses (`Curve.Contains`), außer `kantenbohrung` —
  die muss außen liegen; Überstand > DZ → Fehler.
- Bohrdurchmesser nicht in `bohrkopf_mm` bzw. **`bohrkopf_horizontal_mm`** →
  Fehler, nicht Warnung (HOPS bricht ab). **Schließt Lücke 0.4.**
- Bohrungsmitte näher als d/2 an der Kontur.
- Z-Spanne > 10× Toleranz.
- Nutbreite < Fräser, Nuttiefe ≥ DZ (steckt schon in `nut()`).

**Warnungen:**

- Kein Rohteil getaggt.
- Freie Kontur absolut geschrieben, T13 offen — **immer, solange T13 offen**.
- Bohrung < d/2 + 2 mm von der Kontur (Ausbruch).
- Zwei Bohrungen näher als (d1+d2)/2.
- Kantenbohrung/Nut/Tasche/Gravur: „Form nur teilweise maschinenbelegt".
- DY > DX (Querformat).
- Durchtrennte Außenkontur: „Teil wird beim letzten Durchgang frei". Es gibt
  keine Brücken/Tabs, und das soll es auch nicht heimlich geben.

**Umlaufrichtung** wird nicht geprüft, sondern **erzwungen** (Code aus
`TeilExport`). Bei offener Kontur ist links/rechts sinnlos → `rk=MITTE` +
Warnung.

### 5.2 In `pruefe_hop.py` (letzte Instanz vor der Maschine)

- **`pruefe_belegt(text)`** — Whitelist aller Anweisungs-/Makronamen mit
  Belegstelle. Alles andere → WARNUNG. Randbedingung „jede Zeile braucht einen
  Beleg" mechanisch umgesetzt statt nur im Docstring.
- **`pruefe_umlauf(text)`** — Shoelace je SP-Block; geschlossen + im
  Uhrzeigersinn muss Feld 12 = 1 tragen, gegen den Uhrzeigersinn = 2. **Genau
  der Fehler vom 2026-08-29** wäre so vor dem Versand aufgefallen.
- **`pruefe_bohrungen(text, dz)`** — |Tiefe| > DZ+1 → Fehler; Feld 8 ≠ 0 bei
  X ≠ 0 → Fehler (der „um DX/2 versetzt"-Fall); Durchmesser gegen
  `werkzeuge.json` (optionales `--werkzeuge <pfad>`).
- **SP-Z-Plausibilität**: z < −(DZ+1) zu tief, z > DZ über dem Teil.
- **Rahmenwache erweitern** auf `_Kreistasche_V5` / `_RechteckTasche_V5`.
- **T13-Abhängigkeit**: Rahmenwache und Shoelace setzen absolute Koordinaten
  voraus. Kippt T13, braucht `pruefe_hop` einen Akkumulator (~15 Zeilen).

**Nicht** in `pruefe_hop`: alles was Rhino-Geometrie braucht.

---

## 6. Phasenschnitt

### Phase 1 — MVP: „Leo zeichnet eine Platte mit Löchern und fräst sie"

Umfang: Rohteil-Rechteck + Außenkontur + **runde** Ausschnitte + vertikale
Bohrungen — ausschließlich über belegte Makros, **keine einzige SP/G01-Zeile**.

> **Council 2026-08-31:** Freie Konturen sind in Phase 1 **gesperrt**, nicht nur
> gewarnt. `pruefe()` wirft dafür einen **Fehler** („freie Kontur nicht möglich,
> solange T13 offen“). Begründung: eine Warnzeile verlangt beim Maschinentest
> ein Urteil gegen die Werkstatt-Erfahrung — ein Maschinentest soll aber nur
> Rohmaterial liefern, kein Urteil. Der spätere Umbau
> kostet laut §9 einen halben Tag; ein Fehlschuss im Maschinentest kostet mehr.

**Neue Dateien** (Master, danach `python tools/sync_scripts.py --push`):

`holzher_teil.py` — gemeinsamer Kern, extrahiert aus `TeilExport`:

```python
KINDS = ("rohteil","kontur","ausschnitt","bohrung","kantenbohrung",
         "nut","tasche","gravur","saege")

class Teil(object):
    def __init__(self, name, dx, dy, dz, material=None, thickness=None):
        self.umriss   = None      # (verts, closed)
        self.innen    = []        # [(verts, closed, tiefe_oder_None)]
        self.rechteck = False     # True -> _Format_V5 statt SP/G01
        self.bohrungen= []        # [(x, y, d, tiefe_oder_None)]
        self.kanten   = []        # [(pos, kante, z_hoehe, d, tiefe)]
        self.nuten    = []        # [(x1, y1, x2, y2, breite, tiefe)]
        self.taschen  = []        # [("kreis", x, y, r, tiefe)|("rechteck", ...)]
        self.gravuren = []        # [(verts, closed, tiefe)]

def sauber(text)                                 # aus TeilExport verschoben
def dateiname(name, material, thickness)         # aus TeilExport verschoben
def zielordner(basis, gruppe)                    # aus TeilExport.main verschoben
def pruefe(teil, wzg)    -> (fehler[], warnungen[])
def baue_hop(teil, wzg)  -> HopWriter            # DIE Reihenfolge-Regel, EINE Stelle
def schreibe(teil, basis, wzg, seite, vor) -> info
```

`baue_hop` ist wörtlich die Reihenfolge aus `TeilExport`: Nut → Bohrungen →
Kantenbohrungen → Taschen → Gravuren → Innenausschnitte → **Außenkontur
zuletzt**.

`CncZuweisen.py`, `CncExport.py`, `CncExportTest.py` (Aufbau wie
`TeilExportTest.py`).

**Änderungen am Bestand:**

- `TeilExport.teil_export()` baut ein `Teil` und ruft `holzher_teil.schreibe()`.
  Gelöscht: Assembly-Block, DXF-Temp-Block, `sauber`, `dateiname`, Ordnerlogik.
  ~90 Zeilen weniger.
- `holzher_hop.py`: `BAHN_ABSOLUT = True` als der eine T13-Schalter;
  `format_v5(wz)`; `kreistasche(x, y, r, tiefe, wz, durch=False)`.
- `HolzHerLayerExport.export()`: Kantenbohrungen gegen
  `bohrkopf_horizontal_mm` (Lücke 0.4).

**Abnahme (headless):**

1. `TeilExportTest` weiter **43/43 grün** — der Refactoring-Beweis.
2. `KorpusBauTest` 76/76, `HolzHerLayerExportTest` 45/45 unverändert.
3. **Äquivalenztest** (der wertvollste): dieselben Teile einmal über
   `TeilExport`, einmal flachgelegt als freie Kurven über `CncExport` — die
   beiden `.hop` müssen bis auf `;NCNAME=` textgleich sein.
4. Idempotenz: zweimal exportieren → byteidentisch.
5. `pruefe_hop.py` meldet „konsistent".
6. Wächtertests: Kreis ohne `depth`, Objekt ohne `kind`, Tiefe > Stärke,
   D7-Bohrer, offener Umriss → je ein `ValueError`.
7. Am Rechner: Rechteck 600×400 + 6 Kreise → die `.hop` enthält `_Format_V5`,
   6 `Bohrung`-Zeilen mit Feld 8 = 0, **keine** `SP`-Zeile. Das ist die Datei,
   die als **T14** in den Maschinentest geht.

### Phase 1b — freie Bahnen (erst nach T13)

Freie Kontur, freier Ausschnitt, freie Nutlinie, Gravur — die Sperre aus
`pruefe()` fällt, `BAHN_ABSOLUT` steht auf dem entschiedenen Wert. Abnahme:
T14-Nachfolger mit einer Freiformkontur läuft in der Simulation sauber.

### Phase 2 — Tiefe, Wiederholung, Nut

`kontur(..., zustellungen=n)`, `gravur()`, `kind=nut` für freie Linien.
Abnahme: n SP-Blöcke mit monoton fallendem Z, letzter = −0.2, alle
unter EINEM `WZF`; ≥ 8 neue Prüfungen.

### Phase 3 — Kanten und Taschen

`kind=kantenbohrung` inkl. Setz-Hilfe, `rechtecktasche()`, Taschen-Rahmenwache.
Abnahme: emittierte Makrozeilen stimmen **zeichenweise** mit der Referenzzeile
überein, außer in den Koordinaten. Neue Batteriedateien **T15/T16**, bevor
Späne fallen.

### Phase 4 — Säge (GESPERRT)

Erst nach: T13 entschieden, Antwort der Werkstatt zu Kanten- vs. Koordinatenform,
und einem `saege_wzs`-Eintrag in `werkzeuge.json`.

---

## 7. Verhältnis zu KorpusBau / TeilExport

**Neuer Befehl daneben, gemeinsamer Kern.** Verworfen: EIN Befehl, der die
Auswahl beschnuppert (Brep → Korpusweg, Kurven → freier Weg). Stiller
Moduswechsel ist die Fehlerklasse, an der dieses Projekt schon dreimal Geld
verloren hat.

| | gemeinsam | nur TeilExport | nur CncExport |
|---|---|---|---|
| Kurvenaufbereitung | `holzher_kontur` | | |
| .hop-Zeilen | `holzher_hop` | | |
| DXF | `holzher_dxf`, `HHL.export` | | |
| Reihenfolge, Prüfung, Dateinamen | **`holzher_teil` (neu)** | | |
| Eingang | | `daten_zum_teil`, `flach_transform` | `sammle` (Tags) |

**Langfristige Löschchance (nicht jetzt):** Wenn `KorpusBau` seinen Teilen
zusätzlich `kind=` mitgäbe und flachgelegt ablegte, wäre `TeilExport`
vollständig durch `CncExport` ersetzbar. Erst wenn der freie Pfad an der
Maschine bewiesen ist — vorher wäre es das Wegwerfen des einzigen Pfades, der
heute echte Teile produziert.

---

## 8. Risiken

| Risiko | Was schiefgeht | Gegenmaßnahme |
|---|---|---|
| **T13** (Befund 0.1: kein Absolut-Beleg in 7.7) | Jede freie Kontur fährt woandershin | MVP nutzt Makros → kein SP/G01 im Standardfall; `BAHN_ABSOLUT` an EINEM Ort; Warnzeile je Datei |
| Vergessener Tag | Sackloch wird Durchschuss | Keine Defaults, unbekanntes `kind` = Fehler, untaggte Objekte werden selektiert |
| Umlaufrichtung | Fräser auf der falschen Seite (2026-08-29 passiert) | Richtung erzwungen + `pruefe_umlauf` als zweite Instanz |
| Bohrer nicht am Bohrkopf | HOPS bricht ab, Maschinentest verschenkt | Prüfung gegen beide Bohrkopflisten |
| Rohteil = Umriss angenommen | Fräser läuft auf der Rohteilkante | `kind=rohteil`, sonst Warnung |
| Teil wird beim Trennen frei | Verrutschen, Werkzeugbruch | Außenkontur zuletzt, Warnung, Mehrfachzustellung |
| Teil größer als der Tisch | unbekannt | `tisch_max_mm` erfragen |
| Zwei Exportpfade driften | Einer bleibt korrekt, der andere nicht | Gemeinsamer Kern + **Äquivalenztest** |
| Rhino-Modulcache | Test testet altes Modul | `reload()`, Neustart nach Änderungen |
| Master/Snapshot | Arbeit verschwindet (2026-08-29 passiert) | Immer im Master editieren, `sync_scripts.py --push` |
| Makrozeilen abgeschrieben | Positionscode-Falle wie Bohrung Feld 8 | `ESXY:=0` nur wo aktiv belegt; `_Rechteck_V7` nicht verwenden; `pruefe_belegt` als Bremse |

---

## 9. T13: was jetzt baubar ist, was der Umbau kostet

**Jetzt baubar (T13-immun):** Rohteildefinition, Tagsystem, Zuweisungsbefehl,
gesamte Prüfkette, Bohrungen, Kantenbohrungen, `_Format_V5`-Rechteckkontur,
`_Kreistasche_V5`/`_RechteckTasche_V5`, Dateinamen/Ordner, DXF-Sichtkontrolle,
alle Tests. **Das ist der komplette MVP.**

**Gesperrt bis T13 entschieden (Phase 1b):** freie SP/G01-Konturen, freie
Nutbahnen, Gravur. `pruefe()` wirft einen **Fehler**, keine Warnung — damit
stimmt der Satz „das ist der komplette MVP“ oben wieder.

**Umbaukosten, falls inkrementell gilt:**

| Ort | Aufwand |
|---|---|
| `holzher_hop._bahn()` | ~12 Zeilen: Cursor mitführen, Differenzen emittieren, Bogenmittelpunkt relativ zum Segmentstart |
| `_bogen_zentrum()` | unverändert, nur der Aufrufer zieht den Startpunkt ab |
| `TeilExportTest` | ~6 Prüfungen: Shoelace-Helfer akkumuliert; Bogen-Referenz wechselt auf `Brett.hop` |
| `pruefe_hop.py` | ~15 Zeilen: Rahmenwache + Umlaufprüfung akkumulieren |
| **Nicht betroffen** | `Bohrung`, `HorzB`, alle Makros, VARS/Kopf, Reihenfolge, Prüfkette, `holzher_teil`, `CncZuweisen`, `CncExport` |

**Schätzung: ein halber Tag.** Warten ist deshalb die falsche Entscheidung — der
Umbau ist billiger als der Stillstand. Voraussetzung: die Bahnform sitzt an
EINER Stelle.

---

## 10. Wo der Plan unsicher ist (nicht geraten)

1. **`ESXY:=9`** ist der häufigste Wert und liegt außerhalb der 0–8-Welt.
   Vermutung „Teilmitte" — unbelegt, wird nicht geschrieben.
2. **`_Format_V5` bei Nicht-Rechtecken**: nur für rechteckige Fertigteile
   belegt. Deshalb nur bei achsparallelem Rechteck ohne Verrundung mit exakt
   DX/DY; alles andere über SP/G01.
3. **Sägewerkzeug-ID** fehlt in `werkzeuge.json` (Referenzen zeigen `WZS 220`
   und `WZS 250`) → Phase 4 gesperrt.
4. **Max. Teilegröße / Tischmaß** der 7535D unbekannt.
5. **Gravur-Anfahrt und -Radiuskorrektur** unbelegt; `rk=0`/`anfahrt=0` ist vom
   Nut-Präzedenzfall abgeleitet.
6. **Ob `Bohrung`-Zeilen sicher absolut sind**: das 3×4-Raster in
   `Muster_DXF_Import.hop` ist inkrementell nicht deutbar, aber T05 ist die
   eigentliche Antwort und noch nicht zurück.
7. `KorpusBau.py` wurde nicht vollständig gelesen (46 kB) — falls dort weitere
   Tags entstehen, die der freie Weg spiegeln müsste, kann das entgangen sein.
