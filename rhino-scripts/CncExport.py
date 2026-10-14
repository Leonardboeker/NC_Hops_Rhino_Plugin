# -*- coding: utf-8 -*-
"""CncExport - getaggte freie Geometrie sammeln, pruefen, .hop + .dxf.

Phase 1 (PLAN_FREIE_GEOMETRIE.md): NUR ueber maschinenbelegte Makros,
keine einzige SP/G01-Zeile - immun gegen die offene T13-Frage:
  Aussenkontur : achsparalleles Rechteck -> _Format_V5 (auf DX/DY-Mass)
  Ausschnitt   : Kreis, durch            -> _Kreistasche_V5 (ESXY:=0)
  Bohrung      : Kreis + depth-Tag       -> Bohrung (Feld 8 = 0)
Alles andere (freie Kontur, Nut, Kantenbohrung, Tasche mit Tiefe, Gravur,
Saege) bricht mit Fehler ab, bis die jeweilige Phase freigegeben ist -
stille Modus- oder Formwechsel sind die Fehlerklasse, die an der Maschine
Geld kostet.

DX/DY sind das FERTIGmass (= Mass des Kontur-Rechtecks): _Format_V5
formatiert das Rohteil auf DX/DY, das Aufmass spannt der Bediener.
Praezisierung zu Plan Abschnitt 3.1 - "DX/DY vom Rohteil" gilt fuer den
SP-Grundplatten-Weg ab Phase 1b. Das Rohteil-Rechteck liefert hier
Staerke + Material und eine Plausibilitaetswarnung.

Vor dem Schreiben IMMER ein Bericht; Vorschau=Ja schreibt nichts.
Sichtkontrolle: CncSchau (Dots) + die .dxf.

Erster Aufruf: _-RunPythonScript "CncExport"
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import os
import HolzHerLayerExport as HHL
import holzher_kontur
import holzher_teil
reload(HHL)
reload(holzher_kontur)
reload(holzher_teil)

PHASE_SPAETER = {"kantenbohrung": "Phase 3", "nut": "Phase 2",
                 "gravur": "Phase 2", "saege": "Phase 4 (nach Maschinentest)"}


def teil_aus_objekten(objs, wzg, name=None, querformat=False):
    """Getaggte Kurven -> (holzher_teil.Teil, warnungen[]). Kern ohne
    Dialoge, headless testbar. Wirft ValueError bei allem, was still
    falsch werden koennte."""
    tol = sc.doc.ModelAbsoluteTolerance
    warnungen = []

    gruppen = {}
    ohne = []
    for o in objs:
        kind = rs.GetUserText(o, "kind")
        if not kind:
            ohne.append(o)
        else:
            gruppen.setdefault(kind, []).append(o)
    if ohne:
        raise ValueError("%d Objekt(e) OHNE kind-Tag in der Auswahl - "
                         "erst CncZuweisen. Nichts wird still verworfen."
                         % len(ohne))
    for kind in sorted(gruppen):
        if kind in PHASE_SPAETER:
            raise ValueError("kind=%s ist noch nicht freigegeben (%s)."
                             % (kind, PHASE_SPAETER[kind]))
        if kind == "tasche":
            raise ValueError("kind=tasche ist noch nicht freigegeben "
                             "(Phase 3) - durchgehende runde Ausschnitte "
                             "als 'ausschnitt' taggen.")
        if kind not in holzher_teil.KINDS:
            raise ValueError("kind='%s' ist unbekannt (Tippfehler-Schutz). "
                             "Erlaubt: %s" % (kind,
                                              ", ".join(holzher_teil.KINDS)))

    # --- Umriss: genau EINER je Teil -------------------------------------
    kontur_ids = gruppen.get("kontur", [])
    if not kontur_ids:
        raise ValueError("Kein Umriss (kind=kontur) in der Auswahl.")
    konturen, offen = holzher_kontur.prepare(kontur_ids)
    if len(konturen) != 1:
        raise ValueError("%d getrennte Konturzuege - je Teil ist genau "
                         "EIN Umriss erlaubt (Ausschnitte als "
                         "'ausschnitt' taggen)." % len(konturen))
    umriss = konturen[0][0]
    for o in kontur_ids:
        d = rs.GetUserText(o, "depth")
        if d and d != "durch":
            raise ValueError("Kontur mit Tiefe %s ist noch nicht "
                             "freigegeben (Phase 2) - Phase 1 trennt "
                             "immer durch." % d)

    # --- Rohteil: Staerke/Material + Plausibilitaet ----------------------
    rohteil_ids = gruppen.get("rohteil", [])
    if len(rohteil_ids) > 1:
        raise ValueError("%d Rohteile getaggt - je Teil hoechstens eins."
                         % len(rohteil_ids))
    dicke = None
    material = None
    roh_mass = None
    if rohteil_ids:
        ro = rohteil_ids[0]
        crv = rs.coercecurve(ro)
        roh_mass = holzher_teil._ist_rechteck(crv, tol) if crv else None
        if roh_mass is None:
            raise ValueError("Rohteil ist kein geschlossenes, "
                             "achsparalleles Rechteck.")
        t = rs.GetUserText(ro, "thickness")
        dicke = float(t) if t else None
        material = rs.GetUserText(ro, "material")
    if dicke is None:
        t = rs.GetUserText(kontur_ids[0], "thickness")
        dicke = float(t) if t else None
    if dicke is None:
        raise ValueError("thickness fehlt - Rohteil mit Staerke taggen "
                         "(CncZuweisen Art=Rohteil). DZ wird NIE geraten.")
    if not rohteil_ids:
        warnungen.append("Kein Rohteil getaggt - Staerke kam vom "
                         "Umriss-Tag, Materialname fehlt.")

    # --- Z-Lage: Draufsicht ist Pflicht, geraten wird nicht --------------
    bb = Rhino.Geometry.BoundingBox.Empty
    bb.Union(umriss.GetBoundingBox(True))
    for kind in ("ausschnitt", "bohrung"):
        for o in gruppen.get(kind, []):
            c = rs.coercecurve(o)
            if c is not None:
                bb.Union(c.GetBoundingBox(True))
    if (bb.Max.Z - bb.Min.Z) > 10 * tol:
        raise ValueError("Geometrie ist nicht plan in XY (Z-Spanne "
                         "%.2f mm) - in der Draufsicht zeichnen; geraten "
                         "wird nicht (anders als beim Korpus-Brep gibt es "
                         "hier keine Bohrnormalen)." % (bb.Max.Z - bb.Min.Z))

    # --- Teil bauen ------------------------------------------------------
    teil = holzher_teil.Teil(name or "Teil", dz=dicke, material=material)
    teil.umriss = umriss

    mass = holzher_teil._ist_rechteck(umriss, tol)
    if mass is None:
        # pruefe() formuliert die T13-Sperre; hier nur klassifizieren
        teil.rechteck = False
        ub = umriss.GetBoundingBox(True)
        teil.dx = round(ub.Max.X - ub.Min.X, 1)
        teil.dy = round(ub.Max.Y - ub.Min.Y, 1)
    else:
        teil.rechteck = True
        teil.dx, teil.dy = round(mass[0], 1), round(mass[1], 1)
    if roh_mass is not None and mass is not None:
        if abs(roh_mass[0] - mass[0]) > 0.05 or \
                abs(roh_mass[1] - mass[1]) > 0.05:
            warnungen.append("Rohteil %g x %g weicht vom Fertigmass "
                             "%g x %g ab - _Format_V5 formatiert auf "
                             "DX/DY (Fertigmass); das Aufmass spannt der "
                             "Bediener, es steht nicht in der Datei."
                             % (roh_mass[0], roh_mass[1], mass[0], mass[1]))

    for o in gruppen.get("ausschnitt", []):
        crv = rs.coercecurve(o)
        c = crv.DuplicateCurve() if crv else None
        if c is None:
            continue
        d = rs.GetUserText(o, "depth")
        if d and d != "durch":
            raise ValueError("Ausschnitt mit Tiefe %s = Tasche - noch "
                             "nicht freigegeben (Phase 3)." % d)
        teil.innen.append(c)

    for o in gruppen.get("bohrung", []):
        crv = rs.coercecurve(o)
        if crv is None:
            continue
        ok, k = crv.TryGetCircle(tol)
        if not ok:
            raise ValueError("Bohrung '%s' ist kein Kreis - CncZuweisen "
                             "haette das gemeldet; Tag pruefen."
                             % (rs.ObjectName(o) or str(o)[:8]))
        d = rs.GetUserText(o, "depth")
        if not d:
            raise ValueError("Bohrkreis OHNE depth-Tag - ein vergessener "
                             "Tag wuerde sonst still zum Durchschuss.")
        tief = None if d == "durch" else abs(float(d))
        teil.bohrungen.append((k.Center.X, k.Center.Y, 2.0 * k.Radius,
                               tief))

    # --- auf (0,0), Ecke des FERTIGTEILS = Nullpunkt ---------------------
    ub = umriss.GetBoundingBox(True)
    shift = Rhino.Geometry.Transform.Translation(-ub.Min.X, -ub.Min.Y,
                                                 -ub.Min.Z)
    if querformat and teil.dy > teil.dx:
        dreh = Rhino.Geometry.Transform.Rotation(
            Rhino.RhinoMath.ToRadians(-90.0),
            Rhino.Geometry.Vector3d.ZAxis, Rhino.Geometry.Point3d.Origin)
        umriss.Transform(dreh)
        for c in teil.innen:
            c.Transform(dreh)
        pts = []
        for x, y, d, t in teil.bohrungen:
            pts.append((y, -x, d, t))
        teil.bohrungen = pts
        teil.dx, teil.dy = teil.dy, teil.dx
        ub = umriss.GetBoundingBox(True)
        shift = Rhino.Geometry.Transform.Translation(-ub.Min.X, -ub.Min.Y,
                                                     -ub.Min.Z)
    umriss.Transform(shift)
    for c in teil.innen:
        c.Transform(shift)
    teil.bohrungen = [(x + shift.M03, y + shift.M13, d, t)
                      for x, y, d, t in teil.bohrungen]
    if teil.dy > teil.dx:
        warnungen.append("DY > DX (Hochformat) - kein automatisches "
                         "Querformat; Option Querformat=Ja dreht.")
    return teil, warnungen


def bericht(teil, fehler, warnungen, ziel=None):
    """Befehlszeilen-Bericht nach Plan Abschnitt 2."""
    z = ["%s   Fertigteil %g x %g x %g"
         % (holzher_teil.dateiname(teil.name, teil.material, teil.dz),
            teil.dx or 0, teil.dy or 0, teil.dz or 0)]
    if teil.rechteck:
        z.append("   1 Kontur   rechteckig auf Mass (_Format_V5), durch")
    elif teil.umriss is not None:
        z.append("   1 Kontur   FREI - gesperrt bis T13")
    z.append("  %2d Ausschnitt  rund, durch (_Kreistasche_V5, ESXY 0)"
             % len(teil.innen))
    dm = {}
    for x, y, d, t in teil.bohrungen:
        key = (d, "durch" if t is None else "T-%g" % t)
        dm[key] = dm.get(key, 0) + 1
    for (d, t), anz in sorted(dm.items()):
        z.append("  %2d Bohrung   D%g %s" % (anz, d, t))
    for w in warnungen:
        z.append("  WARNUNG: %s" % w)
    for f in fehler:
        z.append("  FEHLER: %s" % f)
    if ziel and not fehler:
        z.append("  -> %s" % ziel)
    return z


def export_teil(teil, ordner, wzg, seite, vor, vorschau=False):
    """Pruefen + Bericht + Schreiben. -> (ok, zeilen, info_oder_None)"""
    fehler, warnungen = holzher_teil.pruefe(teil, wzg)
    basis = holzher_teil.dateiname(teil.name, teil.material, teil.dz)
    zeilen = bericht(teil, fehler, warnungen,
                     os.path.join(ordner or "", "hop", basis + ".hop"))
    if fehler or vorschau:
        return (not fehler), zeilen, None
    # Ordner erst anlegen, wenn wirklich geschrieben wird
    ziel = holzher_teil.zielordner(ordner)
    wz = int(wzg.get("fraeser_kontur", 20))
    info = holzher_teil.schreibe(teil, os.path.join(ziel["hop"], basis),
                                 wzg, seite, wz, vor,
                                 dxf_basis=os.path.join(ziel["dxf"], basis))
    return True, zeilen, info


def main():
    objs = rs.GetObjects("Getaggte Objekte fuer den CNC-Export waehlen "
                         "(CncZuweisen vergibt die Tags)",
                         rs.filter.curve, preselect=True, select=False)
    if not objs:
        return
    ohne = [o for o in objs if not rs.GetUserText(o, "kind")]
    if ohne:
        rs.UnselectAllObjects()
        rs.SelectObjects(ohne)
        rs.MessageBox("%d Objekt(e) ohne kind-Tag - sie sind jetzt "
                      "SELEKTIERT. Erst CncZuweisen, nichts wird still "
                      "verworfen." % len(ohne), 48, "CncExport")
        return

    st = sc.sticky
    wzg = HHL.werkzeuge()
    from Rhino.Input.Custom import GetOption, OptionToggle
    o_vorschau = OptionToggle(bool(st.get("cnc_vorschau", False)),
                              "Nein", "Ja")
    o_quer = OptionToggle(bool(st.get("cnc_quer", False)), "Nein", "Ja")
    go = GetOption()
    go.SetCommandPrompt("CncExport - Enter = weiter")
    go.AcceptNothing(True)
    go.AddOptionToggle("Vorschau", o_vorschau)
    go.AddOptionToggle("Querformat", o_quer)
    while True:
        res = go.Get()
        if res == Rhino.Input.GetResult.Nothing:
            break
        if res != Rhino.Input.GetResult.Option:
            go.Dispose()
            return
    go.Dispose()
    vorschau = o_vorschau.CurrentValue
    st["cnc_vorschau"], st["cnc_quer"] = vorschau, o_quer.CurrentValue

    ordner = None
    if not vorschau:
        ordner = rs.BrowseForFolder(None, "Ordner fuer hop/ + dxf/",
                                    "CncExport")
        if not ordner:
            return

    # ein Teil je part-Tag, leer = ein Teil aus der ganzen Auswahl
    teile = {}
    for o in objs:
        teile.setdefault(rs.GetUserText(o, "part") or "", []).append(o)

    seite = dict((a, b) for a, b in HHL.SEITEN).get(
        st.get("hhl_seite_txt", HHL.SEITEN[0][0]), "1")
    vor = int(st.get("hhl_vor", 100))
    ok_zahl = 0
    for part in sorted(teile):
        try:
            teil, warn = teil_aus_objekten(teile[part], wzg,
                                           name=part or "Teil",
                                           querformat=o_quer.CurrentValue)
            ok, zeilen, info = export_teil(
                teil, ordner or "", wzg, seite, vor, vorschau=vorschau)
        except ValueError as e:
            print("  FEHLER %s: %s" % (part or "Teil", e))
            continue
        for z in zeilen:
            print(z)
        if ok and not vorschau:
            ok_zahl += 1
    print("CncExport: %d Teil(e) geschrieben%s."
          % (ok_zahl, " (Vorschau, nichts geschrieben)" if vorschau else ""))

    if not rs.IsAlias("CncExport"):
        rs.AddAlias("CncExport", '! _-RunPythonScript "CncExport"')
        print('Alias "CncExport" angelegt.')


if __name__ == "__main__":
    main()
