# HOPS DXF-Layer-Import — entschlüsselte Konvention

Quelle: HolzHer-Schulungsunterlage `DXF_Import.pdf` **plus** Abgleich gegen die
Musterdateien `Muster_DXF_Import.dxf` → `Muster_DXF_Import.hop` (2026-08-24).
Wo Doku und Muster sich widersprechen, gilt das Muster — die Doku hat
Copy-Paste-Fehler (Tabelle 1.2 nennt Fräser 11, die Layernamen sagen TT$20$;
Tabelle 1.5 nennt "Bohrer ID 555" bei Layer `3_TT$220$`, erzeugt wird aber
`WZS (220, …_V*0.3…)`).

Das Muster selbst ist kein Vorbild für den Programmrahmen: es stammt aus HOPS 5.5 und
parkt mit `HH_Park`, die HOPS-7.7-Maschinendateien nur mit `Park_V7 MODE:=2`
(siehe `MASCHINENABGLEICH.md`).

## Layername = Bearbeitungsparameter

Muster: `[Seite][Vorschub]_TT$Werkzeug-ID$[_Optionen]`

| Teil | Bedeutung | Belegt durch |
|---|---|---|
| 1. Ziffer | Seite: `1`=links, `2`=rechts, `0`=Mitte — **immer zweistellig schreiben** (`00_`, `07_`), die führende 0 entfällt *nicht* | Doku 1.1–1.3 |
| 2. Ziffer | Vorschub in Zehnteln: `0`=100 %, `7`=70 %, `4`=40 %, `3`=30 % | `4_TT$20$`→`_V*0.4`, `3_TT$220$`→`_V*0.3`, `27_…`→`_V*0.7` |
| `TT$n$` | Werkzeug-ID n | `TT$20$`→`WZF (20,…)`, `TT$220$`→`WZS (220,…)`, `TT$501$`→`WZB (501,…)` |
| `_C` | Ausschnitt statt Tasche (1× Konturfräsen) | `14_TT$20$_C` → `_Kreisbahn_V5(… Radiuskorrektur:=1)` |
| `_R40` | Eckenradius 40 mm bei Rechtecktasche | `4_TT$20$_R40` → `_RechteckTasche_V5(… Radius:=40)` |
| `_LIN1_LOUT1` | An-/Abfahren linear | Doku 1.3 |

Werkzeugpräfix im .hop folgt der Werkzeugart: `WZF`=Fräser, `WZB`=Bohrer,
`WZS`=Säge. HOPS wählt das anhand der Werkzeug-ID aus der Werkzeugverwaltung.

## Objekt-Eigenschaften

| Element | Typ | Farbe | Layer | Objekthöhe (DXF-Code **39**) |
|---|---|---|---|---|
| Grundplatte (Rohteil) | Polylinie/Linie/Rechteck | weiß (7) | `0` | Plattenstärke negativ (−19) |
| Fräskontur | Linie, Bogen — **keine Kreise** (nur 2 Halbkreise) | rot (1) | `0` | 0 |
| **Startpunkt** | Kreis Ø beliebig (20) | **gelb (2)** | Werkzeug-Layer | Frästiefe negativ |
| Tasche / Ausschnitt | Kreis, Rechteck | magenta (6) | Werkzeug-Layer | Frästiefe negativ |
| Bohrung | Kreis, Ø = Bohrdurchmesser | blau (5) | `TT$501$` | Bohrtiefe negativ |
| Nut (Säge) | Rechteck | gelb | `3_TT$220$` | Nuttiefe negativ |

Einstellige Präfixe (`4_TT$20$`, `3_TT$220$`) kommen im Muster **nur bei
Taschen und Nuten** vor, nicht bei Konturen — dort also nicht nachahmen.

Horizontalbohrungen entstehen dadurch, dass der Kreismittelpunkt **außerhalb
der Plattenkontur** liegt: Der Betrag des Überstands wird zur Z-Höhe der
Kantenbohrung, die Randkoordinate wird 0. Im Muster liegen sie bei
`(110|130|150|170, −10)` bei einer Grundplatte 0…800 × 0…400 und ergeben
`HorzB (110, 0, 10, 8, −20, …, 90, …)`. **Nicht** „Mittelpunkt auf der Kante",
wie hier ursprünglich stand. Praktische Folge für jeden Exporter: Bohrkreise
dürfen die Bounding Box für den Nullpunkt **nicht** aufziehen, sonst schiebt
die Nullpunktverschiebung sie in die Platte und HOPS bohrt vertikal neben die
Kante. Die Grenze vertikal/horizontal steuert zusätzlich "Vert. Bohrung max. D"
in den Import-Einstellungen (dort 15 mm).

## Der entscheidende Punkt: Startpunkt = genau eine Anfahrt

Die Kontur selbst trägt **keine** Bearbeitungsinfo (rot, Layer 0). Erst der
**gelbe Startpunkt-Kreis** auf dem Werkzeug-Layer sagt HOPS: hier anfahren, mit
diesem Werkzeug, dieser Seite, diesem Vorschub, dieser Tiefe. Ein Startpunkt
pro geschlossener Kontur ⇒ genau eine Anfahrt. Der Kreis muss **mittig auf
einem Schnitt- oder Eckpunkt** der Kontur liegen.

Ergebnis im Muster — sauber, mit definiertem Werkzeug:

```
WZF (20,_VE,_V*1,_VA,_SD,0,'')
SP  (38.54616,344.55023,-5,2,0,_ANF, …)   ← EIN Startpunkt
G01 (648.2028,377.13866,0,0,0,2)
… 
EP  (0,_ANF,0)                            ← EIN Ende
```

Zum Vergleich der aCADemy-Weg ohne Layer-Konvention: `KSP ('???','???', …)` —
Werkzeug und Parameter undefiniert, dazu je Teilkontur ein eigenes
`WZF/KSP/KonturFraesen/EP`-Paket.

## Import-Einstellungen (HOPS → Optionen → Import/Export)

- **„Polylinien vereinzeln" ausschalten.** Zerlegt Polylinien in Einzelsegmente;
  das Wiederzusammenketten hängt danach an der Toleranz (0,01 mm) — jede
  kleinste Lücke wird zu einer eigenen Kontur mit eigener Anfahrt.
- Vorlage `DXF_MC`, Einheit mm, „Konturen" an, Layermodus `Alle`.
- Reiter Bohrungen: Durchmesser-Mapping (D → D neu, Tiefe, Typ) und
  „Vert. Bohrung max. D".

## Rhino-Exporter

**Beide** Exportwege schreiben die DXF inzwischen selbst; Rhinos `-_Export`
wird nirgends mehr benutzt. Der Grund ist derselbe wie bei den Konturen: Rhinos
Export-Schema zerlegt Polylinien in Einzelsegmente, sodass eine Kontur beim
Reimport vielteilig zurückkommt — und das Schema ist eine Benutzereinstellung,
auf die man sich nicht verlassen kann.

Vier Module im Rhino-Scripts-Ordner:

| Datei | Rolle |
|---|---|
| `holzher_kontur.py` | gemeinsame Kurvenlogik (Join, Lücken schließen, Vertexliste mit Bulges) — wirkt auf **beide** Exporte |
| `holzher_dxf.py` | DXF-R12-Writer (Layer, Farben, Objekthöhe, Bulges, Layernamen-Bereinigung) |
| `HolzHerLayerExport.py` | HOPS-Layer-Import: Layer-Konvention, Rohteil, Startpunkte, Bohrungen |
| `HolzHerExport.py` | aCADemy: nackte Konturen auf ihrem **Quell-Layer**, Farbe ByLayer, kein Rohteil, kein Startpunkt, Join nur innerhalb eines Layers |

Wer die Konturaufbereitung anfasst, ändert damit immer beide Wege — die
Testsuite `HolzHerLayerExportTest.py` deckt beide ab.

Warum überhaupt ein eigener Writer: Rhino-Kurven haben **keine**
Objekthöhe/Thickness-Eigenschaft — der normale Rhino-DXF-Export kann Code 39
gar nicht schreiben. Der Mini-Writer kann es:

- Format **R12 (AC1009)** statt AC1015 — keine Handles, keine Subclass-Marker,
  keine BLOCK_RECORD-Tabelle, und 39/62 sind voll unterstützt. Für CAM-Importer
  das robusteste Format.
- Konturen als **eine geschlossene POLYLINE je Kontur** auf Layer `0` in Rot,
  Krümmung über Bulge-Werte (`tan(Bogenwinkel/4)` am Vertex). Stützpunkte sind
  exakt die Segmentenden der Rhino-Kurve, Segmente unter 0,05 mm fliegen raus.
  **Das ist der Kern:** einzelne LINE/ARC-Entities muss HOPS erst wieder
  aneinanderketten, und daran scheitert es bei Freiformkonturen — jede winzige
  Lücke wird zu einer eigenen Kontur `K1/K2/K3…` mit eigener Anfahrt und
  `KSP ('???','???')`, weil nur eine davon den Startpunkt abbekommt. Eine
  geschlossene Polylinie kann HOPS nicht zerlegen. Die Warnung der Doku, dass
  bei Polylinien die Z-Tiefe ignoriert wird, ist hier folgenlos — die Tiefe
  kommt ohnehin vom Startpunkt-Kreis.
- Rohteil als geschlossene **POLYLINE** auf `0` in Weiß, Objekthöhe −Stärke.
- **Ein** gelber Startpunkt-Kreis je Kontur auf dem Werkzeug-Layer,
  Objekthöhe −Frästiefe.
- Bohrkreise blau auf `TT$<ID>$`, Objekthöhe −Bohrtiefe.
- Alles auf 0,0 geschoben (Maschinenecke).

**Verifiziert 2026-08-24** am harten Fall (Rippe mit Freiform-Spline-Oberkante
969×140 + Innenkreis + 2 Bohrungen), also genau der Geometrie, die vorher in
HOPS zersplittert ist: 2 rote Konturpolylinien mit 71 bzw. 2 Stützpunkten,
1 weiße Rohteilpolylinie, **je Kontur genau 1 gelber Startpunkt**, keine
Segmente unter 0,05 mm, keine losen LINE/ARC-Entities mehr, und die Konturlänge
kommt nach Reimport auf **0,001 mm** genau zurück. **Der Test gegen HOPS selbst
steht noch aus.**

### Fallen, die ein Audit (2026-08-24) aufgedeckt hat

Alle behoben und durch `HolzHerLayerExportTest.py` abgesichert (36 Prüfungen).
Wer den Exporter anfasst, sollte sie kennen:

1. **Mikro-Segment-Filter darf die Krümmung nicht mitverwerfen.** Der Bulge
   gehört zum Segment, das *an* diesem Vertex beginnt. Wird ein Stützpunkt
   wegen eines 0,03-mm-Splitters eingespart, muss der Bulge des *folgenden*
   Segments auf den überlebenden Vertex — sonst wird aus einem beliebig langen
   Bogen stumm die Sehne (Abweichung im Zentimeterbereich, Teil ist Ausschuss).
2. **Offene Konturen brauchen den Endpunkt als eigenen Vertex.** Die Schleife
   sammelt Segment*starts*; bei geschlossenen Konturen schließt Flag 70=1 den
   Ring, bei offenen fehlt sonst das letzte Segment ersatzlos. Nebeneffekt:
   Konturen aus genau einem Segment verschwanden vorher komplett.
3. **Abstände nur in XY messen.** Ein 3D-Abstandsvergleich gegen einen künstlich
   auf Z=0 gesetzten Punkt macht den Filter auf jeder Kontur außerhalb Z=0
   wirkungslos (auf Z=19 ist jeder Abstand ≥ 19 mm).
4. **Bohrkreise nicht in die Nullpunkt-Bounding-Box aufnehmen** (siehe oben,
   Horizontalbohrungen).
5. **Gemeldete Zahlen müssen die geschriebenen sein.** Verworfene Konturen
   wurden vorher in der Erfolgsmeldung mitgezählt.

6. **Der Ringschluss braucht eine eigene Prüfung.** Die Filterschleife
   vergleicht immer nach hinten; das Schlusssegment einer geschlossenen Kontur
   hat kein Folgesegment und entkommt sonst — ausgerechnet an der Naht, wo
   Spline und Gerade zusammenstoßen und Splitter besonders häufig sind.
7. **Rhino-Layernamen sind keine DXF-Layernamen.** R12 erlaubt nur
   `A–Z a–z 0–9 _ $ -` und 31 Zeichen; Rhino liefert bei Unterlayern den
   vollen Pfad (`Teil 01::Kontur`). Der Writer bereinigt und merkt sich die
   Zuordnung, damit derselbe Layer nicht bei jeder Entity einen neuen Namen
   bekommt.
8. **`$EXTMIN`/`$EXTMAX` müssen die Bögen kennen.** Stützpunkte allein
   unterschlagen die Bulge-Krümmung — ein Kreis Ø200 stünde sonst als
   „200 × 0 mm" in der Datei.

Kollineare Splitter fasst `ToArcsAndLines` selbst zusammen — der Filter greift
nur bei Knick-Splittern. Ein Test mit kollinearem Splitter prüft also nichts.

### Erste Fassung, die in HOPS gescheitert ist (nicht wiederholen)

Version 1 schrieb je Segment eine eigene LINE/ARC-Entity. Ergebnis in HOPS:
`K1, K2, K3 …`, jeweils mit eigenem `WZF/KSP/KonturFraesen/EP`, darunter
Konturen von 0,03 mm Länge, und `WZF (ID,…)` / `KSP ('???','???')`, weil der
einzige Startpunkt nur einer Teilkontur zugeordnet werden konnte. Ursache:
angefittete Bögen enden nicht exakt auf den Segmentenden der Nachbarn, und die
so entstehenden Mikro-Lücken brechen die Verkettung.
