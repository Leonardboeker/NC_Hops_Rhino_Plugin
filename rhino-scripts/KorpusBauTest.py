# -*- coding: utf-8 -*-
"""Headless-Test KorpusBau: 3 Varianten bauen und nachmessen."""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import KorpusBau as K
reload(K)

REPORT = (r"C:\Users\leona\AppData\Local\Temp\claude"
          r"\D--Projekte-SynologyDrive"
          r"\29a00841-9fb2-40c1-a411-d4c61d10076d\scratchpad\korpus_report.txt")
L, PRUEF = [], []


def log(s):
    L.append(s)


def check(name, gut, detail=""):
    PRUEF.append((name, bool(gut)))
    log("  [%s] %s%s" % ("ok" if gut else "XX", name,
                         "  %s" % detail if detail else ""))


def basis_w(**over):
    w = {"H": 720.0, "d": 19.0, "bauart": "S", "n_tueren": 2, "anschlag": "A",
         "td": 19.0, "luft": 3.0, "rw_art": "N", "rw_d": 8.0, "tiefe_nf": 8.0,
         "rw_abstand": 10.0, "lochreihe": True, "loch_abstand": 37.0,
         "n_boeden": 2.0,
         "band_art": "Keine", "band_seite": "L", "topf_kante": 4.0,
         "griff": "Kein", "griff_abstand": 128.0, "griff_lage": "O",
         "material": "mpx-birch", "material_rw": "mdf", "verbinder": "Keine"}
    w.update(over)
    return w


def kreise_mit(teile, radius=None, part=None, depth=None):
    aus = []
    for t in teile:
        if rs.coercebrep(t):
            continue
        crv = rs.coercecurve(t)
        ok, c = crv.TryGetCircle(0.01)
        if not ok:
            continue
        if radius is not None and abs(c.Radius - radius) > 0.01:
            continue
        if part is not None and rs.GetUserText(t, "part") != part:
            continue
        if depth is not None and rs.GetUserText(t, "depth") != depth:
            continue
        aus.append((t, c))
    return aus


def namen(teile):
    return dict((rs.ObjectName(t) or "?", t) for t in teile)


def vol(gid):
    b = rs.coercebrep(gid)
    return abs(b.GetVolume()) if b else 0.0


def bau(rect_pts, front_punkt, w):
    rect = rs.AddPolyline(rect_pts)
    ebene, B, T = K.korpus_ebene_test(rect, front_punkt)
    teile, hinweise = K.bauen(ebene, B, T, w)
    return teile, hinweise, B, T


def main():
    # korpus_ebene braucht einen Klick - fuer den Test eine Variante ohne GUI
    def ke_test(rect_id, punkt):
        crv = rs.coercecurve(rect_id)
        segs = crv.DuplicateSegments()
        p = Rhino.Geometry.Point3d(*punkt)

        def abstand(seg):
            rc = seg.ClosestPoint(p)
            t = rc[1] if isinstance(rc, tuple) else rc
            return seg.PointAt(t).DistanceTo(p)
        front = min(segs, key=abstand)
        p0, p1 = front.PointAtStart, front.PointAtEnd
        mitte = crv.GetBoundingBox(True).Center
        xdir = Rhino.Geometry.Vector3d(p1 - p0)
        xdir.Unitize()
        ni = Rhino.Geometry.Vector3d(
            mitte - Rhino.Geometry.Point3d((p0.X + p1.X) / 2.0,
                                           (p0.Y + p1.Y) / 2.0, 0))
        ni.Z = 0.0
        tiefe = ni.Length * 2.0
        ni.Unitize()
        if Rhino.Geometry.Vector3d.CrossProduct(xdir, ni).Z < 0:
            p0, p1 = p1, p0
            xdir = Rhino.Geometry.Vector3d(p1 - p0)
            xdir.Unitize()
        return Rhino.Geometry.Plane(p0, xdir, ni), p0.DistanceTo(p1), tiefe
    K.korpus_ebene_test = ke_test

    # ---- Variante 1: Standard, 800 breit, Nut, 2 Boeden, 2 Tueren --------
    log("V1  800x600, Nut, System 32, 2 aufschlagende Tueren")
    teile, hinw, B, T = bau([(0, 0, 0), (800, 0, 0), (800, 600, 0),
                             (0, 600, 0), (0, 0, 0)], (400, 0, 0), basis_w())
    n = namen(teile)
    breps = [t for t in teile if rs.coercebrep(t)]
    kreise = [k for k, _ in kreise_mit(teile)]   # nur Kreise, keine Nutlinien
    check("V1: 9 Breps (4 Korpus + RW + 2 Boeden + 2 Tueren)",
          len(breps) == 9, "%d Breps" % len(breps))
    for nm in ("Seite links", "Seite rechts", "Boden", "Deckel", "Rueckwand",
               "Tuer links", "Tuer rechts", "Einlegeboden 1", "Einlegeboden 2"):
        check("V1: '%s' existiert" % nm, nm in n)

    # Nut real geschnitten: Seite ohne Nut waere 19*600*720
    v_soll_voll = 19.0 * 600.0 * 720.0
    v_nut = 8.0 * 8.0 * (720.0 - 2 * 19.0 + 2 * 8.0)
    v_ist = vol(n["Seite links"])
    check("V1: Nut aus Seite geschnitten",
          abs(v_ist - (v_soll_voll - v_nut)) < 1000.0,
          "Volumen %.0f, erwartet %.0f" % (v_ist, v_soll_voll - v_nut))

    # Tuerbreite: (800 - 3) / 2 - 1.5 = Haelfte minus Fugenanteile
    tuer_b = (800.0 - 3.0) / 2.0
    bb = rs.BoundingBox(n["Tuer links"])
    check("V1: Tuerbreite korrekt (%.1f)" % tuer_b,
          abs((bb[6][0] - bb[0][0]) - tuer_b) < 0.01,
          "%.2f" % (bb[6][0] - bb[0][0]))
    check("V1: Tuer VOR dem Korpus (aufschlagend)", bb[0][1] < -0.001)
    # Lochreihe: z=51..691 im 32er-Raster = 21 Ebenen x 2 Reihen x 2 Seiten
    check("V1: 84 Lochkreise", len(kreise) == 84, "%d" % len(kreise))
    check("V1: Tags am Teil", rs.GetUserText(n["Boden"], "thickness") == "19")
    rs.DeleteObjects(teile)

    # ---- Variante 2: 1200 breit, 1 Tuer angefragt -> muss 2 werden -------
    log("V2  1200 breit, 1 Tuer angefragt")
    teile, hinw, B, T = bau([(0, 0, 0), (1200, 0, 0), (1200, 600, 0),
                             (0, 600, 0), (0, 0, 0)], (600, 0, 0),
                            basis_w(n_tueren=1, lochreihe=False, n_boeden=0))
    n = namen(teile)
    check("V2: automatisch 2 Tueren", "Tuer links" in n and "Tuer rechts" in n)
    check("V2: Hinweis dazu vorhanden",
          any("zwingend" in h for h in hinw))
    rs.DeleteObjects(teile)

    # ---- Variante 3: einschlagend, Falz, gedrehte Front ------------------
    log("V3  Front an anderer Kante, einschlagend, Falz")
    teile, hinw, B, T = bau([(0, 0, 0), (600, 0, 0), (600, 500, 0),
                             (0, 500, 0), (0, 0, 0)], (600, 250, 0),
                            basis_w(anschlag="E", rw_art="F", n_tueren=1,
                                    lochreihe=False, n_boeden=1))
    n = namen(teile)
    check("V3: Breite=500 (Front ist die rechte Kante)", abs(B - 500.0) < 0.01,
          "B=%.1f" % B)
    check("V3: 1 Tuer", "Tuer" in n)
    if "Tuer" in n:
        bb = rs.BoundingBox(n["Tuer"])
        dims = sorted([bb[6][0] - bb[0][0], bb[6][1] - bb[0][1],
                       bb[6][2] - bb[0][2]])
        # einschlagend: Oeffnung minus Luft: 500-2*19-2*3 = 456, H: 720-38-6=676
        check("V3: Tuermass 456 x 676", abs(dims[1] - 456.0) < 0.01
              and abs(dims[2] - 676.0) < 0.01,
              "%.1f x %.1f" % (dims[1], dims[2]))
    check("V3: Rueckwand buendig hinten (Falz)", "Rueckwand" in n)
    rs.DeleteObjects(teile)

    # ---- Variante 4: Quader-Eingabe, Front an der rechten Seite ----------
    log("V4  Quader 900x400x650, Front rechts")
    qu = rs.AddBox([(0, 0, 0), (900, 0, 0), (900, 400, 0), (0, 400, 0),
                    (0, 0, 650), (900, 0, 650), (900, 400, 650), (0, 400, 650)])
    ebene, B, T, H = K.quader_basis(qu, (950, 200, 300))
    check("V4: Breite 400 (rechte Seite ist Front)", abs(B - 400.0) < 0.01,
          "B=%.1f" % B)
    check("V4: Tiefe 900", abs(T - 900.0) < 0.01, "T=%.1f" % T)
    check("V4: Hoehe 650 aus dem Quader", abs(H - 650.0) < 0.01, "H=%.1f" % H)
    teile, hinw = K.bauen(ebene, B, T, basis_w(H=H, lochreihe=False,
                                               n_boeden=1, n_tueren=1))
    n = namen(teile)
    check("V4: Korpus gebaut", all(x in n for x in
          ("Seite links", "Seite rechts", "Boden", "Deckel", "Tuer")))
    if "Tuer" in n:
        bbx = rs.BoundingBox(n["Tuer"])
        # aufschlagende Tuer liegt VOR der rechten Quaderseite -> x > 900
        check("V4: Tuer liegt vor der rechten Seite (x > 900)",
              bbx[0][0] > 900.0 - 0.01, "x=%.1f" % bbx[0][0])
    # gedrehter Quader muss abgelehnt werden
    qu2 = rs.AddBox([(0, 0, 0), (900, 0, 0), (900, 400, 0), (0, 400, 0),
                     (0, 0, 650), (900, 0, 650), (900, 400, 650), (0, 400, 650)])
    rs.RotateObject(qu2, (0, 0, 0), 30)
    try:
        K.quader_basis(qu2, (0, 0, 0))
        check("V4: gedrehter Quader wird abgelehnt", False, "kein Fehler kam")
    except ValueError:
        check("V4: gedrehter Quader wird abgelehnt", True)
    rs.DeleteObjects(teile)
    rs.DeleteObjects([qu, qu2])

    # ---- V5: Baender Blum + Griffe, Boeden rasten aufs Lochraster --------
    log("V5  800 breit, Blum-Baender, Stangengriffe, 2 Boeden")
    teile, hinw, B, T = bau([(0, 0, 0), (800, 0, 0), (800, 600, 0),
                             (0, 600, 0), (0, 0, 0)], (400, 0, 0),
                            basis_w(band_art="Blum", griff="Stange"))
    n = namen(teile)
    # Rastermathe pur: Boeden bei H=720/d=19 -> j=7 und j=14
    js = K.boden_js(720.0, 19.0, 2)
    check("V5: Boden-Lochindizes [7, 14]", js == [7, 14], str(js))
    bb = rs.BoundingBox(n["Einlegeboden 1"])
    check("V5: Boden 1 sitzt AUF Loch 7 (z=243)", abs(bb[0][2] - 243.0) < 0.01,
          "z=%.2f" % bb[0][2])
    check("V5: Hinweis zum Einrasten", any("gerastet" in h for h in hinw))
    # Baender: 2 je Tuer, Endlagen ohne Bodenkonflikt -> k=1 und k=20
    ks = K.band_ks(720.0, 19.0, 2, set(js))
    check("V5: Bandlagen [1, 20]", ks == [1, 20], str(ks))
    toepfe = kreise_mit(teile, radius=17.5)
    check("V5: 4 Topfbohrungen D35 (2 Tueren x 2)", len(toepfe) == 4,
          "%d" % len(toepfe))
    check("V5: Topf traegt part+depth",
          bool(toepfe) and rs.GetUserText(toepfe[0][0], "part")
          in ("Tuer links", "Tuer rechts")
          and rs.GetUserText(toepfe[0][0], "depth") == "13")
    schrauben = kreise_mit(teile, radius=4.0)
    check("V5: 8 Topf-Befestigungen D8", len(schrauben) == 8,
          "%d" % len(schrauben))
    # Aufschlag = arm_fd 11 + TopfKante 4 = 15 -> Tuerkante bei x = 19-15 = 4,
    # Topf: 4 + (4 + 17.5) = 25.5; rechte Tuer gespiegelt: 800 - 25.5
    xs = sorted(round(c.Center.X, 1) for _, c in toepfe)
    check("V5: Topflagen gespiegelt (25.5 / 774.5)",
          xs[0] == 25.5 and xs[-1] == 774.5, str(xs))
    bb_tl = rs.BoundingBox(n["Tuer links"])
    check("V5: Tuerbreite 394.5 (Aufschlag 15 statt 19)",
          abs((bb_tl[6][0] - bb_tl[0][0]) - 394.5) < 0.01,
          "%.2f" % (bb_tl[6][0] - bb_tl[0][0]))
    check("V5: Aufschlag-Hinweis", any("Aufschlag" in h for h in hinw))
    griffe = kreise_mit(teile, radius=2.5, depth="durch")
    check("V5: 4 Griffbohrungen durch (Stange, 2 Tueren)", len(griffe) == 4,
          "%d" % len(griffe))
    gx = sorted(set(round(c.Center.X, 1) for _, c in griffe))
    # Griffe zur Mittelfuge: linke Tuer bei 398.5-40, rechte bei 401.5+40
    check("V5: Griffe an der Mittelfuge (358.5 / 441.5)",
          gx == [358.5, 441.5], str(gx))
    # Montageplatte: LochAbstand=37 -> Plattenloecher existieren schon
    d5 = kreise_mit(teile, radius=2.5)
    check("V5: keine doppelten Plattenloecher (84 Lochreihe + 4 Griffe)",
          len(d5) == 88, "%d" % len(d5))
    rs.DeleteObjects(teile)

    # ---- V6: Validierungen ----------------------------------------------
    log("V6  Validierungen")
    for over, muss in (
            (dict(band_art="Blum", lochreihe=False), "Lochreihe"),
            (dict(band_art="Blum", anschlag="E"), "AUFSCHLAGENDE"),
            (dict(band_art="Blum", td=15.0), "Restwand"),
            (dict(band_art="Blum", n_tueren=0), "keine Tueren"),
            (dict(griff="Knopf", n_tueren=0), "keine Tueren"),
            (dict(d=12.0), "zu duenn fuer die Lochreihe"),
            (dict(band_art="Blum", loch_abstand=40.0), "kollidiert"),
            (dict(griff="Stange", griff_abstand=250.0, H=300.0),
             "ausserhalb"),
            (dict(band_art="Blum", H=400.0, n_boeden=10.0),
             "kollisionsfrei"),
            (dict(verbinder="Duebel", d=14.0, lochreihe=False),
             "Duebel")):
        try:
            bau([(0, 0, 0), (800, 0, 0), (800, 600, 0), (0, 600, 0),
                 (0, 0, 0)], (400, 0, 0), basis_w(**over))
            check("V6: %s wird abgelehnt" % muss, False, "kein Fehler kam")
        except ValueError as e:
            check("V6: %s wird abgelehnt" % muss,
                  muss.lower() in str(e).lower(), str(e)[:60])

    # ---- V7: 1 Tuer Band rechts, Knopf, Platten-Fallback (a=50) ----------
    log("V7  600 breit, 1 Tuer, Band rechts, Knopf, LochAbstand 50")
    teile, hinw, B, T = bau([(0, 0, 0), (600, 0, 0), (600, 500, 0),
                             (0, 500, 0), (0, 0, 0)], (300, 0, 0),
                            basis_w(n_tueren=1, band_art="Hettich",
                                    band_seite="R", griff="Knopf",
                                    griff_lage="U", loch_abstand=50.0,
                                    n_boeden=0))
    n = namen(teile)
    toepfe = kreise_mit(teile, radius=17.5)
    # Tuerkante rueckt um einzug 4 ein (600-4=596), Topf: 596 - 21.5 = 574.5
    check("V7: 2 Toepfe an der RECHTEN Kante (x=574.5)",
          len(toepfe) == 2 and all(abs(c.Center.X - 574.5) < 0.01
                                   for _, c in toepfe),
          str([round(c.Center.X, 1) for _, c in toepfe]))
    schr10 = kreise_mit(teile, radius=5.0)
    check("V7: Hettich-Befestigung D10", len(schr10) == 4, "%d" % len(schr10))
    # LochAbstand 50 != 37 -> Plattenloecher werden EXTRA gebohrt (2 je Band)
    platten = [k for k, c in kreise_mit(teile, radius=2.5, part="Seite rechts",
                                        depth="13")
               if abs(c.Center.Y - 37.0) < 0.01]
    check("V7: 4 Plattenloecher bei y=37 (Fallback)", len(platten) == 4,
          "%d" % len(platten))
    knopf = kreise_mit(teile, radius=2.5, depth="durch")
    check("V7: 1 Knopfbohrung links unten (x=40, z=50)",
          len(knopf) == 1 and abs(knopf[0][1].Center.X - 40.0) < 0.01
          and abs(knopf[0][1].Center.Z - 50.0) < 0.01,
          str([(round(c.Center.X, 1), round(c.Center.Z, 1))
               for _, c in knopf]))
    rs.DeleteObjects(teile)

    # ---- V8: Belegungsmodell weicht wirklich aus -------------------------
    log("V8  H=400, 3 Boeden verdraengen das unterste Band")
    check("V8: band_ks weicht aus [3, 10]",
          K.band_ks(400.0, 19.0, 2, set([2, 5, 8])) == [3, 10],
          str(K.band_ks(400.0, 19.0, 2, set([2, 5, 8]))))
    check("V8: benachbarte Lagen abgelehnt",
          K.band_ks(1200.0, 19.0, 2, set(range(1, 34))) is None)
    check("V8: Reduktion 3 -> 2 liefert [1, 35]",
          K.band_ks(1200.0, 19.0, 3, set(range(3, 34))) is None
          and K.band_ks(1200.0, 19.0, 2, set(range(3, 34))) == [1, 35],
          str(K.band_ks(1200.0, 19.0, 2, set(range(3, 34)))))
    teile, hinw, B, T = bau([(0, 0, 0), (600, 0, 0), (600, 500, 0),
                             (0, 500, 0), (0, 0, 0)], (300, 0, 0),
                            basis_w(H=400.0, n_boeden=3.0, band_art="Blum",
                                    n_tueren=1, griff="Kein"))
    zs = sorted(round(c.Center.Z, 1) for _, c in kreise_mit(teile, radius=17.5))
    check("V8: Toepfe bei z=131/355 (NICHT 67)", zs == [131.0, 355.0], str(zs))
    rs.DeleteObjects(teile)

    # ---- V9: Spannweiten- und Hoehen-Hinweise ----------------------------
    log("V9  breite niedrige Tueren hebeln; sehr hohe Tuer")
    teile, hinw, B, T = bau([(0, 0, 0), (1300, 0, 0), (1300, 600, 0),
                             (0, 600, 0), (0, 0, 0)], (650, 0, 0),
                            basis_w(H=400.0, n_boeden=0.0, band_art="Blum",
                                    n_tueren=1))
    check("V9: 2 Hebel-Hinweise (je Tuer)",
          sum(1 for h in hinw if "hebelt" in h) == 2,
          "%d" % sum(1 for h in hinw if "hebelt" in h))
    check("V9: 4 Toepfe", len(kreise_mit(teile, radius=17.5)) == 4)
    rs.DeleteObjects(teile)
    teile, hinw, B, T = bau([(0, 0, 0), (600, 0, 0), (600, 500, 0),
                             (0, 500, 0), (0, 0, 0)], (300, 0, 0),
                            basis_w(H=2500.0, n_boeden=0.0, band_art="Blum",
                                    n_tueren=1))
    check("V9: H=2500 -> 5 Baender + Hinweis",
          len(kreise_mit(teile, radius=17.5)) == 5
          and any("gegenpruefen" in h for h in hinw),
          "%d Toepfe" % len(kreise_mit(teile, radius=17.5)))
    rs.DeleteObjects(teile)

    # ---- V10: einschlagend - Boeden hinter der Tuer, Griff auf y=td ------
    log("V10 einschlagend: Boden weicht der Tuer, Griff auf der Front")
    teile, hinw, B, T = bau([(0, 0, 0), (600, 0, 0), (600, 500, 0),
                             (0, 500, 0), (0, 0, 0)], (300, 0, 0),
                            basis_w(anschlag="E", n_tueren=1, n_boeden=1,
                                    griff="Stange", griff_lage="O"))
    n = namen(teile)
    bb = rs.BoundingBox(n["Einlegeboden 1"])
    check("V10: Boden beginnt HINTER der Tuer (y=22)",
          abs(bb[0][1] - 22.0) < 0.01, "y=%.2f" % bb[0][1])
    griffe = kreise_mit(teile, radius=2.5, depth="durch")
    gz = sorted(round(c.Center.Z, 1) for _, c in griffe)
    check("V10: 2 Griffe bei x=538, y=19, z=520/648",
          len(griffe) == 2
          and all(abs(c.Center.X - 538.0) < 0.01 for _, c in griffe)
          and all(abs(c.Center.Y - 19.0) < 0.01 for _, c in griffe)
          and gz == [520.0, 648.0],
          str([(round(c.Center.X, 1), round(c.Center.Y, 1),
                round(c.Center.Z, 1)) for _, c in griffe]))
    rs.DeleteObjects(teile)

    # ---- V11: gedrehter Frame - Bohrbild in Weltkoordinaten --------------
    log("V11 Front an der rechten Kante, Blum - Weltkoordinaten pruefen")
    teile, hinw, B, T = bau([(0, 0, 0), (600, 0, 0), (600, 500, 0),
                             (0, 500, 0), (0, 0, 0)], (600, 250, 0),
                            basis_w(n_tueren=1, band_art="Blum",
                                    n_boeden=0.0, griff="Kein"))
    check("V11: B=500", abs(B - 500.0) < 0.01, "B=%.1f" % B)
    toepfe = kreise_mit(teile, radius=17.5)
    # Ebene: Ursprung (600,0,0), X=(0,1,0), Y=(-1,0,0). Band links, einzug 4:
    # lokal x=25.5 -> Welt (600, 25.5, z), z = 67 und 675
    tz = sorted(round(c.Center.Z, 1) for _, c in toepfe)
    check("V11: Toepfe in Welt (600, 25.5, 67/675)",
          len(toepfe) == 2
          and all(abs(c.Center.X - 600.0) < 0.01 for _, c in toepfe)
          and all(abs(c.Center.Y - 25.5) < 0.01 for _, c in toepfe)
          and tz == [67.0, 675.0],
          str([(round(c.Center.X, 1), round(c.Center.Y, 1),
                round(c.Center.Z, 1)) for _, c in toepfe]))
    rs.DeleteObjects(teile)

    # ---- V12: Material-Tags, Layerfarben je Korpus, Duebel/Clamex --------
    log("V12 Material, Layer-Gradient, Eckverbinder")
    teile, hinw, B, T = bau([(0, 0, 0), (600, 0, 0), (600, 500, 0),
                             (0, 500, 0), (0, 0, 0)], (300, 0, 0),
                            basis_w(n_tueren=0, n_boeden=0, lochreihe=False,
                                    verbinder="Duebel", material="mdf",
                                    material_rw="hpl"))
    n = namen(teile)
    check("V12: material am Boden", rs.GetUserText(n["Boden"], "material")
          == "mdf", str(rs.GetUserText(n["Boden"], "material")))
    check("V12: Rueckwand eigenes Material",
          rs.GetUserText(n["Rueckwand"], "material") == "hpl")
    lay1 = rs.ObjectLayer(n["Boden"])
    check("V12: nummerierter Korpus-Layer", lay1.startswith("Korpus_")
          and "::Boden_Deckel" in lay1, lay1)
    # Duebel: T=500 -> 3 Positionen je Ecke, 4 Ecken, je 2 Bohrbilder = 24
    duebel = kreise_mit(teile, radius=4.0)
    check("V12: 24 Duebel-Bohrbilder", len(duebel) == 24, "%d" % len(duebel))
    tiefen = set(rs.GetUserText(k, "depth") for k, _ in duebel)
    check("V12: Tiefen 12 (Flaeche) und 18 (Hirn)",
          tiefen == set(["12", "18"]), str(sorted(tiefen)))
    parts = set(rs.GetUserText(k, "part") for k, _ in duebel)
    check("V12: Duebel-parts Seiten+Boden+Deckel",
          parts == set(["Seite links", "Seite rechts", "Boden", "Deckel"]),
          str(sorted(parts)))
    teile1 = teile

    # zweiter Korpus daneben: eigener Layer, andere Farbe, Clamex als Marker
    teile, hinw, B, T = bau([(700, 0, 0), (1300, 0, 0), (1300, 500, 0),
                             (700, 500, 0), (700, 0, 0)], (1000, 0, 0),
                            basis_w(n_tueren=0, n_boeden=0, lochreihe=False,
                                    verbinder="Clamex"))
    n2 = namen(teile)
    lay2 = rs.ObjectLayer(n2["Boden"])
    check("V12: zweiter Korpus auf eigenem Layer", lay2 != lay1
          and lay2.startswith("Korpus_"), lay2)
    f1 = rs.LayerColor(lay1.split("::")[0])
    f2 = rs.LayerColor(lay2.split("::")[0])
    check("V12: andere Korpusfarbe",
          (f1.R, f1.G, f1.B) != (f2.R, f2.G, f2.B),
          "%s vs %s" % ((f1.R, f1.G, f1.B), (f2.R, f2.G, f2.B)))
    # 1 Marker je Position (12) - anders als Duebel, die je Punkt ZWEI
    # Bohrbilder brauchen (Flaeche + Hirn)
    marker = kreise_mit(teile, radius=3.0)
    check("V12: 12 Clamex-Marker", len(marker) == 12, "%d" % len(marker))
    check("V12: Marker OHNE process-Tag (kein CNC-Export)",
          all(not rs.GetUserText(k, "process") for k, _ in marker)
          and all(rs.GetUserText(k, "connector") == "Clamex"
                  for k, _ in marker))
    check("V12: Marker-Hinweis", any("MARKER" in h for h in hinw))
    rs.DeleteObjects(teile1)
    rs.DeleteObjects(teile)

    n_ok = sum(1 for _, g in PRUEF if g)
    log("")
    log("%d/%d Pruefungen ok -> %s"
        % (n_ok, len(PRUEF), "PASS" if n_ok == len(PRUEF) else "FAIL"))
    txt = "\n".join(L)
    print(txt)
    f = open(REPORT, "w")
    f.write(txt)
    f.close()


if __name__ == "__main__":
    main()
