# -*- coding: utf-8 -*-
"""TeilExport - EIN Korpusteil flachlegen und fuer die HolzHer schreiben.

Je Teil entstehen ZWEI Dateien:
  <Teil>.hop  - direkt ladbar, umgeht die DXF-Importvorlage komplett:
                Konturen mit SP z=-0.2 (Bezug Tisch/Unterkante, automatisch
                durch), Bohrungen mit echter Tiefe je Loch, Kantenbohrungen
                als HorzB, RUECKWANDNUT als Fraeserbahnen (Flanken,
                dann Mitte - das Nut-Makro will die Saege).
  <Teil>.dxf  - Sichtkontrolle / Fallback fuer den DXF-Import (Vorlage KB:
                nur Konturen + Bohrkreise; Nut und Tiefen kann die DXF
                nicht transportieren).

Ablauf: Teile waehlen (ganzer Korpus geht) -> Ordner -> fertig. Je Korpus
entsteht darin ein Ordner mit dem Namen des Korpus-Layers, darin "hop" und
"dxf"; die Dateinamen tragen Material und Staerke.
Bohrkreise und Nut-Linien werden ueber den part-UserText gefunden, alles
wird flachgelegt (Querformat, Bohr-/Nutseite oben) und auf 0,0 gelegt.

Dieses Script SAMMELT nur (Brep -> flaches Teil); geschrieben wird ueber
holzher_teil.schreibe() - dieselbe Reihenfolge-Regel und derselbe Writer,
den auch CncExport (freie Geometrie) benutzt.

Erster Aufruf: _-RunPythonScript "TeilExport"
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import os
import HolzHerLayerExport as HHL
import holzher_kontur
import holzher_hop
import holzher_teil
reload(HHL)
reload(holzher_kontur)
reload(holzher_hop)
reload(holzher_teil)
from holzher_kontur import contour_vertices

SEITEN = HHL.SEITEN

sauber = holzher_teil.sauber


def dateiname(teil_id, name):
    """Teilname + Material + Staerke - beides steht als Tag am Teil."""
    return holzher_teil.dateiname(name,
                                  (rs.GetUserText(teil_id, "material")
                                   or "").strip(),
                                  (rs.GetUserText(teil_id, "thickness")
                                   or "").strip())


def daten_zum_teil(teil_id):
    """Bohrkreise + Nut-Linien mit part == Teilname im selben Korpusbaum."""
    name = rs.ObjectName(teil_id)
    if not name:
        raise ValueError("Das Teil hat keinen Objektnamen - ist es aus "
                         "KorpusBau?")
    wurzel = rs.ObjectLayer(teil_id).split("::")[0]
    tol = sc.doc.ModelAbsoluteTolerance
    kreise, nuten = [], []
    for unterlayer in ("::Lochreihe", "::Bohrungen"):
        for o in rs.ObjectsByLayer(wurzel + unterlayer) or []:
            if rs.GetUserText(o, "part") != name:
                continue
            crv = rs.coercecurve(o)
            if crv is None:
                continue
            ok, circ = crv.TryGetCircle(tol)
            if ok:
                kreise.append((circ, rs.GetUserText(o, "depth")))
    if rs.IsLayer(wurzel + "::Nuten"):
        for o in rs.ObjectsByLayer(wurzel + "::Nuten") or []:
            if rs.GetUserText(o, "part") != name:
                continue
            crv = rs.coercecurve(o)
            if crv is None:
                continue
            nuten.append((crv.PointAtStart, crv.PointAtEnd,
                          float(rs.GetUserText(o, "nut_b") or 8.5),
                          float(rs.GetUserText(o, "nut_t") or 8.0)))
    return name, kreise, nuten


def flach_transform(teil_id, kreise, nuten):
    """Teil flach auf XY, Querformat, Bohr-/Nutseite OBEN.

    Abstimmung, welche Grossflaeche nach oben kommt: senkrechte Bohrkreise
    stimmen mit ihrer Normalen; gibt es keine (z. B. Boden mit reinen
    Kantenbohrungen), entscheidet die Nut-Linie - sie muss auf der OBERSEITE
    liegen, sonst wuerde _nuten_frei_v5 die falsche Seite nuten.
    """
    brep = rs.coercebrep(teil_id)
    bestes, beste_flaeche = None, -1.0
    for f in brep.Faces:
        ok, ebene = f.TryGetPlane(sc.doc.ModelAbsoluteTolerance)
        if not ok:
            continue
        a = Rhino.Geometry.AreaMassProperties.Compute(f)
        if a and a.Area > beste_flaeche:
            beste_flaeche = a.Area
            bestes = ebene
    if bestes is None:
        raise ValueError("Keine ebene Plattenflaeche gefunden.")
    ebene = bestes

    def bau(eb):
        xf = Rhino.Geometry.Transform.PlaneToPlane(eb,
                                                   Rhino.Geometry.Plane.WorldXY)
        bb = brep.GetBoundingBox(xf)
        lift = Rhino.Geometry.Transform.Translation(0, 0, -bb.Min.Z)
        ges = lift * xf
        bb2 = brep.GetBoundingBox(ges)
        if (bb2.Max.Y - bb2.Min.Y) > (bb2.Max.X - bb2.Min.X):
            ges = Rhino.Geometry.Transform.Rotation(
                Rhino.RhinoMath.ToRadians(-90.0),
                Rhino.Geometry.Vector3d.ZAxis,
                Rhino.Geometry.Point3d.Origin) * ges
        return ges, bb.Max.Z - bb.Min.Z

    n = ebene.Normal
    stimmen = 0
    for circ, _ in kreise:
        cn = circ.Plane.Normal
        if abs(cn * n) > 0.9:
            stimmen += 1 if (cn * n) > 0 else -1
    kippen = stimmen < 0
    if stimmen == 0 and nuten:
        xf_test, dicke = bau(ebene)
        p = Rhino.Geometry.Point3d(nuten[0][0])
        p.Transform(xf_test)
        kippen = p.Z < dicke / 2.0        # Nut laege unten -> Teil wenden
    if kippen:
        ebene = Rhino.Geometry.Plane(ebene)
        ebene.Flip()
    return bau(ebene)


def teil_export(pfad_basis, teil_id, seite, wz, vor, dxf_basis=None):
    """Kern ohne Dialoge. Schreibt <basis>.hop und <basis>.dxf."""
    name, kreise, nuten = daten_zum_teil(teil_id)
    dicke_tag = rs.GetUserText(teil_id, "thickness")
    xf, dicke_geo = flach_transform(teil_id, kreise, nuten)
    dicke = float(dicke_tag) if dicke_tag else dicke_geo
    tol = sc.doc.ModelAbsoluteTolerance
    wzg = HHL.werkzeuge()

    # --- Umriss aus der groessten ebenen Flaeche -------------------------
    brep = rs.coercebrep(teil_id).DuplicateBrep()
    brep.Transform(xf)
    flaeche, beste = None, -1.0
    for f in brep.Faces:
        ok, e = f.TryGetPlane(tol)
        if ok and abs(e.Normal.Z) > 0.9:
            a = Rhino.Geometry.AreaMassProperties.Compute(f)
            if a and a.Area > beste:
                beste, flaeche = a.Area, f
    if flaeche is None:
        raise ValueError("Keine Plattenflaeche gefunden.")
    umriss, innen = None, []
    for loop in flaeche.Loops:
        c = loop.To3dCurve()
        if loop.LoopType == Rhino.Geometry.BrepLoopType.Outer:
            umriss = c
        elif loop.LoopType == Rhino.Geometry.BrepLoopType.Inner:
            innen.append(c)

    # --- ALLES selbst auf den Nullpunkt legen (Konturen bestimmen ihn) ---
    fb = umriss.GetBoundingBox(True)
    schub = Rhino.Geometry.Transform.Translation(-fb.Min.X, -fb.Min.Y,
                                                 -fb.Min.Z)
    umriss.Transform(schub)
    for c in innen:
        c.Transform(schub)
    breite = fb.Max.X - fb.Min.X
    hoehe = fb.Max.Y - fb.Min.Y
    # Kreise und Nuten NUR in XY schieben - der Z-Anteil von schub wuerde
    # die Kantenbohr-Hoehen (z. B. d/2) ins Negative ziehen
    schub_xy = Rhino.Geometry.Transform.Translation(-fb.Min.X, -fb.Min.Y, 0)
    gesamt = schub_xy * xf
    ebene_xy = Rhino.Geometry.Plane.WorldXY

    vertikal = []                         # (x, y, d, tiefe_oder_None)
    kanten = []                           # (pos, kante, z_hoehe, d, tiefe)
    for circ, tiefe_tag in kreise:
        c2 = Rhino.Geometry.Circle(circ.Plane, circ.Radius)
        c2.Transform(gesamt)
        nz = abs(c2.Plane.Normal.Z)
        if not tiefe_tag:
            raise ValueError("%s: Bohrkreis OHNE depth-Tag - ein vergessener "
                             "Tag wuerde sonst still zum Durchschuss." % name)
        tief = None if tiefe_tag == "durch" else abs(float(tiefe_tag))
        if nz > 0.9:
            if tief is not None and c2.Plane.Normal.Z < -0.5:
                raise ValueError("%s: Sackloch von der UNTERSEITE - die "
                                 "Maschine bohrt nur von oben, Teil anders "
                                 "orientieren." % name)
            vertikal.append((c2.Center.X, c2.Center.Y, c2.Radius * 2.0, tief))
        elif nz < 0.1:
            if tief is None:
                raise ValueError("%s: durchgehende KANTENbohrung ergibt "
                                 "keinen Sinn - depth-Tag pruefen." % name)
            n2 = Rhino.Geometry.Vector3d(c2.Plane.Normal.X,
                                         c2.Plane.Normal.Y, 0)
            n2.Unitize()
            probe = Rhino.Geometry.Point3d(c2.Center.X + n2.X,
                                           c2.Center.Y + n2.Y, 0)
            if umriss.Contains(probe, ebene_xy, tol) == \
                    Rhino.Geometry.PointContainment.Inside:
                n2 = -n2
            if abs(n2.Y) > 0.9:
                kante = "y0" if n2.Y < 0 else "y1"
                pos = c2.Center.X
            elif abs(n2.X) > 0.9:
                kante = "x0" if n2.X < 0 else "x1"
                pos = c2.Center.Y
            else:
                raise ValueError("%s: Kantenbohrung nicht achsparallel - "
                                 "nicht unterstuetzt." % name)
            kanten.append((pos, kante, c2.Center.Z, c2.Radius * 2.0, tief))
        else:
            raise ValueError("Bohrkreis weder senkrecht noch in der Kante "
                             "(schraege Bohrung?) - nicht unterstuetzt.")

    nut_zeilen = []                       # (x1, y1, x2, y2, breite, tiefe)
    for pa, pb, nb, nt in nuten:
        a = Rhino.Geometry.Point3d(pa)
        b = Rhino.Geometry.Point3d(pb)
        a.Transform(gesamt)
        b.Transform(gesamt)
        if abs(a.Z - b.Z) > 0.5:
            raise ValueError("%s: Nut-Linie liegt nach dem Flachlegen nicht "
                             "in der Ebene." % name)
        if a.Z < dicke / 2.0:
            raise ValueError("%s: Nut laege auf der UNTERSEITE - "
                             "gefraest wird von oben." % name)
        def _kx(v):
            return max(0.0, min(breite, v))
        def _ky(v):
            return max(0.0, min(hoehe, v))
        nut_zeilen.append((_kx(a.X), _ky(a.Y), _kx(b.X), _ky(b.Y), nb, nt))

    # --- Teil bauen, schreiben tut holzher_teil --------------------------
    teil = holzher_teil.Teil(name, dz=dicke,
                             material=rs.GetUserText(teil_id, "material"))
    teil.dx, teil.dy = breite, hoehe
    teil.umriss = umriss
    teil.innen = innen
    teil.bohrungen = vertikal
    teil.kanten = kanten
    teil.nuten = nut_zeilen
    info = holzher_teil.schreibe(teil, pfad_basis, wzg, seite, wz, vor,
                                 dxf_basis=dxf_basis)
    info["falz"] = rs.GetUserText(teil_id, "falz") == "ja"
    return info


def main():
    teile = rs.GetObjects("Korpusteile fuer den Export waehlen (ganzer Korpus "
                          "geht - Kreise/Texte sortieren sich selbst aus)",
                          rs.filter.polysurface | rs.filter.extrusion,
                          preselect=True, select=False)
    if not teile:
        return
    st = sc.sticky
    wzg = HHL.werkzeuge()
    seite = dict((a, b) for a, b in SEITEN).get(
        st.get("hhl_seite_txt", SEITEN[0][0]), "1")
    wz = int(wzg.get("fraeser_kontur", 20))
    vor = int(st.get("hhl_vor", 100))

    ordner = rs.BrowseForFolder(None, "Ordner fuer die Dateien (.hop + .dxf "
                                      "je Teil)", "TeilExport")
    if not ordner:
        return

    ok_zahl = 0
    warnungen = []
    vergeben = set()
    for teil in teile:
        name = rs.ObjectName(teil) or "Teil"
        # Je Korpus ein Ordner (Name = Korpus-Layer), darin hop/ und dxf/.
        korpus = sauber(rs.ObjectLayer(teil).split("::")[0])
        ziel = holzher_teil.zielordner(ordner, korpus)
        basis = dateiname(teil, name)
        nr = 2
        while (korpus, basis.lower()) in vergeben:
            basis = "%s_%d" % (dateiname(teil, name), nr)
            nr += 1
        vergeben.add((korpus, basis.lower()))
        try:
            info = teil_export(os.path.join(ziel["hop"], basis), teil, seite,
                               wz, vor,
                               dxf_basis=os.path.join(ziel["dxf"], basis))
        except ValueError as e:
            warnungen.append("%s: %s" % (name, e))
            continue
        ok_zahl += 1
        print("  %-16s %.0f x %.0f x %.0f mm, %d Bohrungen (%d Kanten), "
              "%d Nut(en) -> %s\\{hop,dxf}\\%s"
              % (info["teil"], info["breite"], info["hoehe"], info["dicke"],
                 info["bohr_auto"] + info["kanten"], info["kanten"],
                 info["nuten"], korpus, basis))
        if info["fremde_d"]:
            warnungen.append("%s: Durchmesser %s mm nicht am Bohrkopf!"
                             % (name, info["fremde_d"]))
        if info.get("falz"):
            warnungen.append("%s: FALZ ist NICHT in der Datei - laeuft "
                             "weiter ueber die Saege!" % name)
        if info["nuten"]:
            warnungen.append("%s: Nut als Fraeserbahnen mit Werkzeug %d "
                             "(Flanken, dann Mitte) - erste Datei in der "
                             "Simulation ansehen."
                             % (name, int(wzg.get("fraeser_nut", 8))))
        if info["kanten"]:
            warnungen.append("%s: %d Kantenbohrungen als HorzB - nur der "
                             "90-Grad-Fall ist maschinenbelegt, erste Datei "
                             "in der Simulation ansehen." % (name,
                                                             info["kanten"]))

    print("TeilExport: %d von %d Teilen -> %s" % (ok_zahl, len(teile), ordner))
    if warnungen:
        rs.MessageBox("\n".join(warnungen), 48, "TeilExport")

    if not rs.IsAlias("TeilExport"):
        rs.AddAlias("TeilExport", '! _-RunPythonScript "TeilExport"')
        print('Alias "TeilExport" angelegt.')


if __name__ == "__main__":
    main()
