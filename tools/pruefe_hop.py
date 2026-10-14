# -*- coding: utf-8 -*-
"""Prueft eine erzeugte .hop-Datei auf innere Widersprueche.

    python tools/pruefe_hop.py "C:/Users/leona/Downloads/Test Platte 3.hop"

Der Ausloeser: "Test Platte 3.hop" deklarierte DX=1470 / DY=471, waehrend die
Bearbeitungskoordinaten von einem Teil X=471 / Y=1470 ausgingen -- um 90 Grad verdreht.
Auf dem Blatt sieht man das nicht, an der Maschine faehrt die Nut einen Meter neben
der Platte.

Verraten haben es die UEBERSTAENDE: ein Durchgangsschnitt faehrt 50 mm ueber die
Kante hinaus, seine Endpunkte sind also Kantenlage +/- 50. Aus den Schnittlinien
laesst sich damit rueckrechnen, welche Plattenmasse die Bearbeitungen annehmen --
und das muss zu DX/DY aus dem VARS-Block passen.

Rueckgabe 0 = konsistent, 1 = Befunde.
"""
import io
import re
import sys

TOL = 1.0          # mm -- die Werte kommen gerundet aus dem Export
UEBERSTAND = 50.0  # mm -- so weit faehrt ein Formatier-/Durchgangsschnitt ueber die Kante


def zahl(s):
    try:
        return float(s)
    except ValueError:
        return None


def parameter(zeile):
    """NAME:=WERT-Paare einer CALL-Zeile, Namen gross geschrieben."""
    raus = {}
    for m in re.finditer(r"([A-Za-z_][A-Za-z0-9_]*)\s*:=\s*(-?[0-9.]+)", zeile):
        raus[m.group(1).upper()] = float(m.group(2))
    return raus


def pruefe(pfad):
    text = io.open(pfad, encoding="cp1252", errors="replace").read()
    befunde = []
    hinweise = []

    # ---- VARS-Masse
    vars_ = {}
    for m in re.finditer(r"^\s*(DX|DY|DZ)\s*:=\s*(-?[0-9.]+)", text, re.MULTILINE):
        vars_[m.group(1)] = float(m.group(2))
    if len(vars_) < 3:
        befunde.append("VARS-Block unvollstaendig: nur %s gefunden" % sorted(vars_))
        return befunde, hinweise
    dx, dy, dz = vars_["DX"], vars_["DY"], vars_["DZ"]

    # ---- Kopfzeilen sind OFFSETS, keine Masse. Alle ~200 Maschinendateien unter
    # reference-hops/ tragen dort 0.000/0.000/0; die Masse stehen nur im VARS-Block.
    # (Die erste Fassung dieser Pruefung verlangte Kopf == VARS -- das war die falsche
    # Regel, uebernommen aus einer fehlerhaften Plugin-Ausgabe.)
    for k in ("DX", "DY", "DZ"):
        m = re.search(r"^;%s=(-?[0-9.]+)" % k, text, re.MULTILINE)
        if m and abs(float(m.group(1))) > TOL:
            befunde.append("Kopf ;%s=%s -- dort gehoert der Offset 0 hin, nicht das Mass"
                           % (k, m.group(1)))

    # ---- ungerundete Masse: 1470.0002 ist ein Messrest, kein Mass
    for k, v in vars_.items():
        # Das Plugin rundet Masse bewusst auf 0.01 mm (NcFmt.Dim) -- erst was darunter
        # liegt, ist Messrauschen. Die erste Fassung mahnte schon bei 0.1 an.
        if abs(v - round(v, 2)) > 1e-9:
            hinweise.append("%s := %r ist ungerundet -- Messrest aus der Geometrie,"
                            " besser auf 0.1 mm runden" % (k, v))

    # ---- Rahmenwache: liegt eine Bearbeitung ausserhalb des Teils?
    # Loest den 90-Grad-Check unten ab, seit die Nut keine Makrozeile mehr ist,
    # sondern SP/G01-Bahnen (Maschine 2026-08-29). Der Schnitt darf um den
    # UEBERSTAND hinauslaufen, eine verdrehte Platte um ein Vielfaches mehr.
    fuer_rahmen = ("SP (", "G01 (", "G02M (", "G03M (", "Bohrung (", "HorzB (")
    aussen = []
    for nr, zeile in enumerate(text.splitlines(), 1):
        z = zeile.strip().lstrip("/")
        if not z.startswith(fuer_rahmen):
            continue
        felder = z[z.index("(") + 1:].split(",")
        if len(felder) < 2:
            continue
        x, y = zahl(felder[0]), zahl(felder[1])
        if x is None or y is None:            # Ausdruecke wie 70+9.5 -> nicht pruefbar
            continue
        if not (-UEBERSTAND - TOL <= x <= dx + UEBERSTAND + TOL
                and -UEBERSTAND - TOL <= y <= dy + UEBERSTAND + TOL):
            aussen.append((nr, z.split(" ")[0], x, y))
    if aussen:
        nr, art, x, y = aussen[0]
        befunde.append(
            "%d Bearbeitung(en) liegen ausserhalb des Teils %gx%g (erlaubt sind "
            "%g mm Ueberstand) -- erste: Zeile %d, %s bei X=%g Y=%g. Klassischer "
            "Fall: Teil um 90 Grad verdreht, oder Koordinaten in der falschen "
            "Bezugsart (absolut/inkrementell)."
            % (len(aussen), dx, dy, UEBERSTAND, nr, art, x, y))

    # ---- Bearbeitungen einsammeln
    senkrecht = []   # konstantes X, laeuft in Y
    waagerecht = []  # konstantes Y, laeuft in X
    for zeile in text.splitlines():
        if "_nuten_frei" not in zeile:
            continue
        p = parameter(zeile)
        if not all(k in p for k in ("X1", "Y1", "X2", "Y2")):
            continue
        if abs(p["X1"] - p["X2"]) < 1e-6:
            senkrecht.append(p)
        elif abs(p["Y1"] - p["Y2"]) < 1e-6:
            waagerecht.append(p)

    # ---- Der Kern: welche Plattenmasse nehmen die Schnitte an?
    # Ein Schnitt mit Ueberstand endet bei Kante + 50. Der groesste Endpunkt aller
    # senkrechten Schnitte verraet also die Y-Ausdehnung, der der waagerechten die X-Ausdehnung.
    if senkrecht:
        y_ende = max(max(p["Y1"], p["Y2"]) for p in senkrecht)
        y_start = min(min(p["Y1"], p["Y2"]) for p in senkrecht)
        if abs(y_start + UEBERSTAND) < TOL and abs((y_ende - UEBERSTAND) - dy) > TOL:
            impliziert = y_ende - UEBERSTAND
            passt_zu = "DX" if abs(impliziert - dx) <= TOL else "keinem deklarierten Mass"
            befunde.append(
                "senkrechte Schnitte laufen bis Y=%g, implizieren also Teil-Laenge %g -- "
                "deklariert ist DY=%g. Die Zahl passt zu %s: die Bearbeitungen sind "
                "gegenueber dem Fertigteil um 90 Grad verdreht."
                % (y_ende, impliziert, dy, passt_zu))
    if waagerecht:
        x_ende = max(max(p["X1"], p["X2"]) for p in waagerecht)
        x_start = min(min(p["X1"], p["X2"]) for p in waagerecht)
        if abs(x_start + UEBERSTAND) < TOL and abs((x_ende - UEBERSTAND) - dx) > TOL:
            impliziert = x_ende - UEBERSTAND
            passt_zu = "DY" if abs(impliziert - dy) <= TOL else "keinem deklarierten Mass"
            befunde.append(
                "waagerechte Schnitte laufen bis X=%g, implizieren also Teil-Breite %g -- "
                "deklariert ist DX=%g. Die Zahl passt zu %s."
                % (x_ende, impliziert, dx, passt_zu))

    # ---- Bearbeitungen muessen im Teil liegen (Rechtecktaschen)
    for zeile in text.splitlines():
        if "_Rechteck" not in zeile:
            continue
        p = parameter(zeile)
        if "X_MITTE" not in p:
            continue
        for achse, mitte, halb, mass in (("X", p.get("X_MITTE"), p.get("LAENGE", 0) / 2, dx),
                                          ("Y", p.get("Y_MITTE"), p.get("BREITE", 0) / 2, dy)):
            if mitte is None:
                continue
            if mitte - halb < -TOL or mitte + halb > mass + TOL:
                befunde.append("Rechteck %s_MITTE=%g (halbe Kante %g) ragt aus dem Teil"
                               " (0..%g)" % (achse, mitte, halb, mass))

    if dz < 1 or dz > 60:
        hinweise.append("DZ=%g mm ist als Plattendicke ungewoehnlich" % dz)
    return befunde, hinweise


def main():
    if len(sys.argv) < 2:
        print(__doc__.strip().split("\n")[0])
        print("  python tools/pruefe_hop.py <datei.hop> [...]")
        return 2
    schlecht = 0
    for pfad in sys.argv[1:]:
        befunde, hinweise = pruefe(pfad)
        print("== %s" % pfad)
        for b in befunde:
            print("   FEHLER   %s" % b)
        for h in hinweise:
            print("   Hinweis  %s" % h)
        if not befunde and not hinweise:
            print("   konsistent")
        if befunde:
            schlecht += 1
    return 1 if schlecht else 0


if __name__ == "__main__":
    sys.exit(main())
