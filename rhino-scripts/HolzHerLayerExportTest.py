# -*- coding: utf-8 -*-
"""Headless-Test HolzHerLayerExport.

A  Struktur am harten Fall (Freiform-Rippe) - der urspruengliche Gruentest.
B  Mikro-Segment: Bogen hinter einem 0,03-mm-Splitter darf NICHT zur Sehne
   werden (Blocker aus dem Audit).
C  Offene Kontur: der Endpunkt des letzten Segments muss als Vertex drin sein.
D  Kontur auf Z=19: der Mikro-Filter muss trotzdem greifen (er hat frueher in
   3D gemessen und war damit auf jeder Kontur ausserhalb Z=0 wirkungslos).
E  Horizontalbohrung: ein Bohrkreis ausserhalb der Kontur muss ausserhalb
   BLEIBEN, sonst wird aus der Kantenbohrung eine Vertikalbohrung.
F  tool_layer(): Praefix immer zweistellig.
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import math
import HolzHerLayerExport as H
reload(H)

SP = r"C:\Users\leona\AppData\Local\Temp\claude\D--Projekte-SynologyDrive\29a00841-9fb2-40c1-a411-d4c61d10076d\scratchpad"
DXF = SP + r"\hhl_test.dxf"
DXF_B = SP + r"\hhl_test_bohr.dxf"
REPORT = SP + r"\hhl_test_report.txt"

L = []
PRUEF = []


def log(s):
    L.append(s)


def check(name, gut, detail=""):
    PRUEF.append((name, bool(gut)))
    log("  [%s] %s%s" % ("ok" if gut else "XX", name,
                         "  %s" % detail if detail else ""))


def P(x, y, z=0.0):
    return Rhino.Geometry.Point3d(x, y, z)


def parse(path):
    """DXF -> (Entity-Liste, Polylinien als Vertexlisten mit Bulge)."""
    lines = [l.strip() for l in open(path).read().splitlines()]
    i, cur, rec, ents, polys, vs = 0, None, {}, [], [], []
    while i < len(lines) - 1:
        c, v = lines[i], lines[i + 1]
        if c == '0':
            if cur == 'VERTEX':
                vs.append((float(rec.get('10', 0)), float(rec.get('20', 0)),
                           float(rec.get('42', 0))))
            elif cur in ('POLYLINE', 'CIRCLE', 'LINE', 'ARC'):
                ents.append((cur, rec.get('8'), rec.get('62'),
                             float(rec.get('10', 0)), float(rec.get('20', 0))))
            if v == 'SEQEND' and vs:
                polys.append(vs); vs = []
            cur, rec = v, {}
        else:
            rec.setdefault(c, v)
        i += 2
    return ents, polys


# ---------------------------------------------------------------- A Struktur
def test_a():
    log("A  Struktur am Freiform-Teil")
    top = rs.AddInterpCurve([(0, 140, 0), (200, 105, 0), (420, 132, 0),
                             (650, 88, 0), (900, 125, 0), (969, 110, 0)])
    rest = [rs.AddLine((969, 110, 0), (969, 0, 0)),
            rs.AddLine((969, 0, 0), (0, 0, 0)),
            rs.AddLine((0, 0, 0), (0, 140, 0))]
    kontur = rs.JoinCurves([top] + rest, delete_input=True)[0]
    loch = rs.AddCircle((480, 60, 0), 35)
    bohr = [rs.AddCircle((80, 60, 0), 3), rs.AddCircle((880, 60, 0), 3)]
    soll = rs.CurveLength(kontur) + rs.CurveLength(loch)

    info = H.export(DXF, [kontur, loch], "2", 20, 100, 19.0, 19.0,
                    bohr, 501, 13.0, vorlage="SP")
    log("   export -> %s" % info)
    ents, polys = parse(DXF)

    z = {}
    for t, lay, col, x, y in ents:
        k = "%s|%s|%s" % (t, lay, col)
        z[k] = z.get(k, 0) + 1
    check("2 rote Konturpolylinien", z.get("POLYLINE|0|1") == 2)
    check("1 weisse Rohteilpolylinie", z.get("POLYLINE|0|7") == 1)
    check("2 gelbe Startpunkte auf 20_TT$20$",
          z.get("CIRCLE|20_TT$20$|2") == 2)
    check("2 blaue Bohrungen", z.get("CIRCLE|TT$501$|5") == 2)
    check("keine losen LINE/ARC-Entities",
          not any(t in ("LINE", "ARC") for t, _, _, _, _ in ents))

    kurz = 0
    for vl in polys:
        for a, b in zip(vl, vl[1:] + [vl[0]]):
            if math.hypot(b[0] - a[0], b[1] - a[1]) < H.MIN_SEG:
                kurz += 1
    check("keine Mikro-Segmente in der Datei", kurz == 0, "(%d)" % kurz)

    before = set(str(o) for o in rs.AllObjects())
    rs.Command('_-Import "%s" _Enter' % DXF, False)
    neu = [o for o in rs.AllObjects() if str(o) not in before]
    ist = sum(rs.CurveLength(o) for o in neu
              if rs.IsCurve(o) and rs.ObjectLayer(o) == "0")
    check("Konturlaenge nach Reimport exakt", abs(ist - soll) < 0.5,
          "Soll %.2f / Ist %.2f" % (soll, ist))
    rs.DeleteObjects(neu)


# ------------------------------------------------- B/D Mikro-Segment + Bogen
def mikro_am_ende():
    """Geschlossene Kontur, deren LETZTES Segment der 0,03-mm-Splitter ist.

    Dieses Schlusssegment sieht die Filterschleife nie - es hat kein
    Folgesegment. Ohne Sonderbehandlung bliebe ein 0,03-mm-Ringschluss stehen.
    """
    pc = Rhino.Geometry.PolyCurve()
    pc.Append(Rhino.Geometry.Line(P(0, 0), P(200, 0)))
    pc.Append(Rhino.Geometry.Line(P(200, 0), P(200, 100)))
    pc.Append(Rhino.Geometry.Line(P(200, 100), P(0, 100)))
    pc.Append(Rhino.Geometry.Line(P(0, 100), P(0.025, 0.016)))
    pc.Append(Rhino.Geometry.Line(P(0.025, 0.016), P(0, 0)))   # Splitter
    return pc


def mikro_kontur(z=0.0):
    """Gerade 100 -> Splitter 0,03 -> Halbkreis r=50 -> zurueck.

    Der Splitter ist ein KNICK, nicht kollinear - genau die Form, die in Leos
    kaputter .hop-Datei stand (K2 ging von 239.09426,55.63308 nach
    239.11956,55.61753, also dx=0.025 / dy=-0.016). Kollineare Splitter fasst
    ToArcsAndLines schon selbst zusammen und erreichen den Filter nie.
    """
    jx, jy = 100.025, -0.016
    pc = Rhino.Geometry.PolyCurve()
    pc.Append(Rhino.Geometry.Line(P(0, 0, z), P(100, 0, z)))
    pc.Append(Rhino.Geometry.Line(P(100, 0, z), P(jx, jy, z)))
    pc.Append(Rhino.Geometry.Arc(P(jx, jy, z), P(jx + 50, jy + 50, z),
                                 P(jx, jy + 100, z)))
    pc.Append(Rhino.Geometry.Line(P(jx, jy + 100, z), P(0, 100, z)))
    pc.Append(Rhino.Geometry.Line(P(0, 100, z), P(0, 0, z)))
    return pc


def test_bd():
    for z, name in ((0.0, "B  Mikro-Segment vor Halbkreis (Z=0)"),
                    (19.0, "D  dasselbe auf Z=19")):
        log(name)
        verts, dropped = H.contour_vertices(mikro_kontur(z))
        check("%s: Splitter zusammengefasst" % name[0], dropped == 1,
              "dropped=%d" % dropped)
        # Vertex (100,0) muss den Bulge des Halbkreises tragen: tan(pi/4)=1
        tref = [v for v in verts if abs(v[0] - 100.0) < 0.1 and abs(v[1]) < 0.1]
        b = tref[0][2] if tref else None
        check("%s: Halbkreis bleibt Bogen (Bulge ~1.0)" % name[0],
              b is not None and abs(abs(b) - 1.0) < 0.02, "Bulge=%s" % b)


# ------------------------------------------------------------ C offene Kontur
def test_c():
    log("C  Offene Kontur")
    pc = Rhino.Geometry.PolyCurve()
    pc.Append(Rhino.Geometry.Line(P(0, 0), P(100, 0)))
    pc.Append(Rhino.Geometry.Arc(P(100, 0), P(135, 15), P(150, 50)))
    pc.Append(Rhino.Geometry.Line(P(150, 50), P(400, 50)))
    verts, _ = H.contour_vertices(pc)
    check("C: Endpunkt ist dabei (4 Vertices)", len(verts) == 4,
          "%d Vertices" % len(verts))
    if verts:
        e = verts[-1]
        check("C: letzter Vertex auf dem Kurvenende",
              abs(e[0] - 400.0) < 0.01 and abs(e[1] - 50.0) < 0.01,
              "(%.2f, %.2f)" % (e[0], e[1]))
        check("C: letzter Vertex ohne Bulge", abs(e[2]) < 1e-9)


# --------------------------------------------------------- E Horizontalbohrung
def test_e():
    log("E  Horizontalbohrung ausserhalb der Kontur")
    kontur = rs.AddRectangle(rs.WorldXYPlane(), 600, 300)
    # Kantenbohrung: absichtlich 10 mm UNTERHALB der Plattenkante
    kante = rs.AddCircle((200, -10, 0), 4)
    H.export(DXF_B, [kontur], "2", 20, 100, 19.0, 19.0, [kante], 501, 20.0,
             vorlage="SP")
    ents, _ = parse(DXF_B)
    bohr = [(x, y) for t, lay, col, x, y in ents if lay == "TT$501$"]
    check("E: Bohrkreis existiert", len(bohr) == 1)
    if bohr:
        y = bohr[0][1]
        check("E: liegt weiter AUSSERHALB (y < 0)", y < -0.001, "y=%.3f" % y)
        check("E: Ueberstand unveraendert 10 mm", abs(y + 10.0) < 0.01,
              "y=%.3f" % y)
    rs.DeleteObjects([kontur, kante])


# ------------------------------------------------------------- F Layernamen
def test_f():
    log("F  Layernamen")
    check("F: Mitte -> 00_TT$20$", H.tool_layer("0", 100, 20) == "00_TT$20$",
          H.tool_layer("0", 100, 20))
    check("F: rechts 100%% -> 20_TT$20$",
          H.tool_layer("2", 100, 20) == "20_TT$20$", H.tool_layer("2", 100, 20))
    check("F: links 70%% -> 17_TT$11$",
          H.tool_layer("1", 70, 11) == "17_TT$11$", H.tool_layer("1", 70, 11))


# ------------------------------------- G HolzHerExport: KEINE Zersplitterung
def reimport(path):
    before = set(str(o) for o in rs.AllObjects())
    rs.Command('_-Import "%s" _Enter' % path, False)
    return [o for o in rs.AllObjects() if str(o) not in before]


def test_g():
    log("G  HolzHerExport - eine Kontur bleibt EINE Kurve")
    import HolzHerExport as HE
    reload(HE)
    dxf = SP + r"\hhe_test.dxf"

    # Rechteck aus 4 EINZELNEN Linien - so liegt es typisch im Modell.
    # Genau hier kam frueher eine vierteilige Kontur zurueck.
    lines = [rs.AddLine((0, 0, 0), (600, 0, 0)),
             rs.AddLine((600, 0, 0), (600, 300, 0)),
             rs.AddLine((600, 300, 0), (0, 300, 0)),
             rs.AddLine((0, 300, 0), (0, 0, 0))]
    # Zweite Kontur mit echtem Bogen, um zu sehen dass Boegen Boegen bleiben
    kreis = rs.AddCircle((300, 150, 0), 60)
    soll = 1800.0 + rs.CurveLength(kreis)

    info = HE.export(dxf, lines + [kreis])
    log("   export -> %s" % info)
    check("G: 2 Konturen gemeldet", info["konturen"] == 2,
          "%s" % info["konturen"])

    ents, polys = parse(dxf)
    check("G: genau 2 POLYLINE-Entities in der Datei",
          sum(1 for t, _, _, _, _ in ents if t == "POLYLINE") == 2)
    check("G: keine losen LINE/ARC-Entities",
          not any(t in ("LINE", "ARC") for t, _, _, _, _ in ents))
    rechteck = [p for p in polys if len(p) == 4]
    check("G: Rechteck ist EINE Polylinie mit 4 Stuetzpunkten",
          len(rechteck) == 1, "%s" % [len(p) for p in polys])

    neu = reimport(dxf)
    kurven = [o for o in neu if rs.IsCurve(o)]
    check("G: Reimport ergibt 2 Kurven, nicht 5+", len(kurven) == 2,
          "%d Kurven" % len(kurven))
    check("G: beide reimportierten Kurven sind geschlossen",
          all(rs.IsCurveClosed(o) for o in kurven))
    ist = sum(rs.CurveLength(o) for o in kurven)
    check("G: Laenge exakt (Bogen blieb Bogen)", abs(ist - soll) < 0.5,
          "Soll %.2f / Ist %.2f" % (soll, ist))
    rs.DeleteObjects(neu + [kreis])


# ------------------------------------------- H Splitter am Ringschluss (H/I/J)
def test_h():
    log("H  Mikro-Segment am Ringschluss")
    verts, dropped = H.contour_vertices(mikro_am_ende())
    check("H: Splitter am Ende zusammengefasst", dropped == 1,
          "dropped=%d" % dropped)
    if verts:
        d0 = math.hypot(verts[-1][0] - verts[0][0], verts[-1][1] - verts[0][1])
        check("H: Ringschluss laenger als %s mm" % H.MIN_SEG, d0 >= H.MIN_SEG,
              "%.4f mm" % d0)


def test_i():
    log("I  Layernamen werden R12-tauglich bereinigt")
    import holzher_dxf as HD
    reload(HD)
    d = HD.DxfR12()
    a = d.layer(u"Teil 01::Kontur")
    b = d.layer("20_TT$20$")
    c1 = d.layer(u"Teil 01::Kontur")           # zweiter Aufruf, gleicher Name
    # '+' und '/' sind in R12 unzulaessig -> beide werden zu '_' und der
    # bereinigte Name traefe auf denselben Zielnamen wie 'Teil 01::Kontur'
    c2 = d.layer(u"Teil+01//Kontur")
    check("I: Unterlayer bereinigt", a == "Teil_01__Kontur", a)
    check("I: Werkzeug-Layer unveraendert", b == "20_TT$20$", b)
    check("I: gleicher Name -> gleicher Zielname", c1 == a, "%s / %s" % (c1, a))
    check("I: Kollision bekommt eigenen Namen", c2 != a and c2, c2)
    lang = d.layer("X" * 60)
    check("I: auf 31 Zeichen gekuerzt", len(lang) <= 31, "%d" % len(lang))


def test_j():
    log("J  DXF-Header meldet die echte Ausdehnung")
    import HolzHerExport as HE
    reload(HE)
    dxf = SP + r"\hhe_extents.dxf"
    kreis = rs.AddCircle((0, 0, 0), 100)       # 2 Halbboegen, Stuetzpunkte nur
    info = HE.export(dxf, [kreis])             # links und rechts -> Hoehe 0?
    txt = [l.strip() for l in open(dxf).read().splitlines()]
    def hdr(var):
        i = txt.index(var)
        return float(txt[i + 2]), float(txt[i + 4])
    xmax, ymax = hdr("$EXTMAX")
    check("J: $EXTMAX Y ist nicht 0 (Bogen zaehlt mit)", ymax > 199.0,
          "ymax=%.2f" % ymax)
    check("J: $EXTMAX passt zur gemeldeten Groesse",
          abs(xmax - info["breite"]) < 0.01 and abs(ymax - info["hoehe"]) < 0.01,
          "%.2f x %.2f vs %.2f x %.2f" % (xmax, ymax, info["breite"],
                                          info["hoehe"]))
    rs.DeleteObject(kreis)


# --------------------------- K getaggte Bohrkreise in der Kontur-Auswahl
def test_k():
    log("K  depth-getaggte Kreise werden NIE zur Fraeskontur")
    kontur = rs.AddRectangle(rs.WorldXYPlane(), 600, 300)
    k1 = rs.AddCircle((100, 100, 0), 2.5)      # Lochreihe D5 T13
    rs.SetUserText(k1, "depth", "13")
    k2 = rs.AddCircle((200, 100, 0), 2.5)      # Griff D5 durch
    rs.SetUserText(k2, "depth", "durch")
    k3 = rs.AddCircle((300, 150, 0), 40)       # UNGETAGGT: echte Innenkontur
    dxf = SP + r"\hhl_tags.dxf"
    # alle vier zusammen als 'Konturen' uebergeben - wie eine Fensterauswahl
    info = H.export(dxf, [kontur, k1, k2, k3], "2", 20, 100, 19.0, 19.0)
    check("K: nur 2 Konturen (Rechteck + ungetaggter Kreis)",
          info["konturen"] == 2, "%d" % info["konturen"])
    check("K: 2 Bohrungen aus Tags", info["bohr_auto"] == 2)
    ents, _ = parse(dxf)
    bohr = [(x, y) for t, lay, col, x, y in ents
            if t == "CIRCLE" and lay == "TT$501$"]
    check("K: Bohrkreise auf TT$501$", len(bohr) == 2, str(bohr))
    # Tiefen: 13 und durch=Staerke+1=20
    lines = [l.strip() for l in open(dxf).read().splitlines()]
    tiefen = set()
    for i, l in enumerate(lines):
        if l == "TT$501$" and lines[i - 1] == "8":
            # Codes stehen auf den Positionen i+1, i+3, ... bis zur naechsten
            # Entity (Code 0); Code 39 = Objekthoehe/Tiefe
            j = i + 1
            while j < len(lines) - 1 and lines[j] not in ("39", "0"):
                j += 2
            if j < len(lines) - 1 and lines[j] == "39":
                tiefen.add(float(lines[j + 1]))
    check("K: Tiefen -13 und -20 (durch)", tiefen == set([-13.0, -20.0]),
          str(sorted(tiefen)))
    check("K: keine fremden Durchmesser (5 mm am Bohrkopf)",
          info["fremde_d"] == [], str(info["fremde_d"]))
    rs.DeleteObjects([kontur, k1, k2, k3])


# ----------------------- L Vorlage KB: nur Konturen + Bohrungen, sonst NICHTS
def test_l():
    log("L  Vorlage KB (Leos HOPS 7.7): kein Rohteil, kein Startpunkt")
    kontur = rs.AddRectangle(rs.WorldXYPlane(), 600, 300)
    k1 = rs.AddCircle((100, 100, 0), 2.5)
    rs.SetUserText(k1, "depth", "13")
    dxf = SP + r"\hhl_kb.dxf"
    info = H.export(dxf, [kontur, k1], "2", 20, 100, 19.0, 19.0)  # Default KB
    ents, _ = parse(dxf)
    weiss = [1 for t, lay, col, x, y in ents if t == "POLYLINE" and col == "7"]
    gelb = [1 for t, lay, col, x, y in ents if t == "CIRCLE" and col == "2"]
    rot = [1 for t, lay, col, x, y in ents if t == "POLYLINE" and col == "1"]
    blau = [1 for t, lay, col, x, y in ents
            if t == "CIRCLE" and lay == "TT$501$"]
    check("L: KEINE weisse Rohteil-Polylinie", len(weiss) == 0, "%d" % len(weiss))
    check("L: KEIN gelber Startpunkt", len(gelb) == 0, "%d" % len(gelb))
    check("L: Kontur da", len(rot) == 1 and info["konturen"] == 1)
    check("L: Bohrung da", len(blau) == 1)
    rs.DeleteObjects([kontur, k1])


def main():
    for t in (test_a, test_bd, test_c, test_e, test_f, test_g,
              test_h, test_i, test_j, test_k, test_l):
        try:
            t()
        except Exception as e:
            check("%s EXCEPTION" % t.__name__, False, repr(e))
    n_ok = sum(1 for _, g in PRUEF if g)
    log("")
    log("%d/%d Pruefungen ok -> %s" % (n_ok, len(PRUEF),
                                       "PASS" if n_ok == len(PRUEF) else "FAIL"))
    txt = "\n".join(L)
    print(txt)
    f = open(REPORT, "w"); f.write(txt); f.close()


if __name__ == "__main__":
    main()
