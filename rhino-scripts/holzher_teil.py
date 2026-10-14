# -*- coding: utf-8 -*-
"""holzher_teil - gemeinsamer Kern fuer TeilExport (Korpus) und CncExport
(freie Geometrie). Plan: 53_post-processor/PLAN_FREIE_GEOMETRIE.md, Phase 1.

Hier lebt DIE Reihenfolge-Regel (baue_hop) an genau EINER Stelle:
Nut -> Bohrungen -> Kantenbohrungen -> Taschen -> Gravuren ->
Innenausschnitte -> Aussenkontur ZULETZT. Sie ist eine Sicherheits-
Eigenschaft (das Teil bleibt bis zum letzten Schnitt im Verbund), keine
Stilfrage - wer sie in einem zweiten Befehl nachbaut, laesst sie driften.

Abweichung vom Plan-Datenmodell: `umriss`/`innen` tragen Rhino-KURVEN statt
Vertexlisten - die Vertexliste entsteht erst in baue_hop (contour_vertices),
weil die DXF-Sichtkontrolle dieselbe Kurve nochmal braucht.
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import os
import HolzHerLayerExport as HHL
import holzher_kontur
import holzher_hop
reload(HHL)
reload(holzher_kontur)
reload(holzher_hop)
from holzher_kontur import contour_vertices
from holzher_hop import HopWriter

KINDS = ("rohteil", "kontur", "ausschnitt", "bohrung", "kantenbohrung",
         "nut", "tasche", "gravur", "saege")


class Teil(object):
    """Ein flaches Teil bei (0,0), +Z oben - fertig zum Schreiben."""

    def __init__(self, name, dz=None, material=None):
        self.name = name
        self.dx = None            # Fertigteil-Breite (VARS DX)
        self.dy = None            # Fertigteil-Hoehe  (VARS DY)
        self.dz = dz              # Dicke (VARS DZ) - fehlt -> Fehler, nie raten
        self.material = material
        self.umriss = None        # Rhino.Geometry.Curve, geschlossen
        self.innen = []           # [Curve] durchgehende Innenausschnitte
        self.rechteck = False     # True -> _Format_V5 (+ _Kreistasche_V5)
                                  # False -> freie SP-Bahn (Korpusweg/Phase 1b)
        self.bohrungen = []       # [(x, y, d, tiefe_oder_None)]  None = durch
        self.kanten = []          # [(pos, kante, z_hoehe, d, tiefe)]
        self.nuten = []           # [(x1, y1, x2, y2, breite, tiefe)]
        self.taschen = []         # [("kreis", x, y, r, tiefe_oder_None)]
        self.gravuren = []        # [(Curve, tiefe)] - Phase 2, noch ohne Emitter


def sauber(text):
    """Windows-tauglicher Datei-/Ordnername (Trennzeichen raus, ein Wort)."""
    for zeichen in '\\/:*?"<>|\t\r\n':
        text = text.replace(zeichen, "_")
    return "_".join(text.split()).strip("._") or "Teil"


def dateiname(name, material=None, thickness=None):
    """Teilname + Material + Staerke - wertbasiert, ohne Objektzugriff."""
    teile = [name]
    if material:
        teile.append(str(material).strip())
    if thickness:
        try:
            teile.append("%gmm" % float(thickness))   # 19.0 -> "19mm"
        except (TypeError, ValueError):
            teile.append(str(thickness).strip() + "mm")
    return sauber("_".join(teile))


def zielordner(basis, gruppe=None):
    """<basis>[/<gruppe>]/{hop,dxf} anlegen -> {"hop": pfad, "dxf": pfad}."""
    ziel = {}
    for art in ("hop", "dxf"):
        p = os.path.join(basis, gruppe, art) if gruppe else \
            os.path.join(basis, art)
        if not os.path.isdir(p):
            os.makedirs(p)
        ziel[art] = p
    return ziel


def _kreis(c, tol):
    ok, k = c.TryGetCircle(tol)
    return k if ok else None


def _ist_rechteck(c, tol):
    """Geschlossenes, achsparalleles Rechteck? -> (dx, dy) oder None."""
    if not c.IsClosed:
        return None
    ok, poly = c.TryGetPolyline()
    if not ok:
        return None
    pts = [poly[i] for i in range(poly.Count)]
    if pts and pts[0].DistanceTo(pts[-1]) < tol:
        pts = pts[:-1]
    if len(pts) != 4:
        return None
    bb = c.GetBoundingBox(True)
    for p in pts:
        # jede Ecke muss auf einer BBox-Ecke liegen - sonst Parallelogramm
        if min(abs(p.X - bb.Min.X), abs(p.X - bb.Max.X)) > tol or \
           min(abs(p.Y - bb.Min.Y), abs(p.Y - bb.Max.Y)) > tol:
            return None
    return (bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y)


def pruefe(teil, wzg):
    """-> (fehler[], warnungen[]). Fehler verhindern die Datei.

    Prueft das GEBAUTE Teil (Geometrie schon flach und bei 0,0). Die
    kind-/Tag-Pruefungen passieren beim Sammeln (CncExport), die letzte
    Instanz vor der Maschine bleibt tools/pruefe_hop.py.
    TeilExport ruft pruefe() NICHT - der Korpusweg traegt seine Waechter
    inline und ist maschinenbelegt (SP-Konturen, Simulation 2026-08/09).
    """
    fehler, warnungen = [], []
    tol = sc.doc.ModelAbsoluteTolerance
    n = teil.name

    if not teil.dz:
        fehler.append("%s: thickness fehlt - DZ wird NIE geraten "
                      "(ein falscher Default wuerde still durchschiessen)."
                      % n)
    if teil.umriss is None:
        fehler.append("%s: kein Umriss (kind=kontur) - ohne Aussenkontur "
                      "gibt es kein Teil." % n)
    elif not teil.umriss.IsClosed:
        fehler.append("%s: Umriss ist OFFEN (Luecke > %s mm) - schliessen "
                      "oder nachzeichnen." % (n, holzher_kontur.GAP_TOL))
    if not teil.dx or not teil.dy:
        fehler.append("%s: Fertigteilmass DX/DY fehlt." % n)

    # T13-Sperre (Council 2026-08-31): freie SP/G01-Bahnen sind FEHLER,
    # nicht Warnung. T13 ist papierseitig beantwortet (G01 Feld 5 = 0 =
    # absolut, MASCHINENABGLEICH) - aber die Phase-1b-Abnahme verlangt
    # eine saubere Freiform in der HOPS-Simulation, und die steht aus.
    if not teil.rechteck:
        fehler.append("%s: freie Kontur ist noch gesperrt - T13 ist auf "
                      "dem Papier beantwortet (G01 Feld 5 = 0 = absolut), "
                      "aber die Phase-1b-Abnahme (Freiform in der HOPS-"
                      "Simulation) fehlt. Phase 1 kann nur RECHTECKIGE "
                      "Aussenkonturen." % n)
    tisch = wzg.get("tisch_max_mm")
    if tisch and teil.dx and teil.dy:
        if teil.dx > tisch[0] + 0.01 or teil.dy > tisch[1] + 0.01:
            fehler.append("%s: %g x %g mm ueberschreitet tisch_max_mm %s - "
                          "konservativer Wert (groesstes gelaufenes "
                          "Referenzteil), echtes Tischmass in der Werkstatt "
                          "erfragen (Maschinenplan)."
                          % (n, teil.dx, teil.dy, tisch))

    bohrkopf = wzg.get("bohrkopf_mm", [])
    horizontal = wzg.get("bohrkopf_horizontal_mm", [])
    d_wz = float(wzg.get("fraeser_kontur_d", 19.25))
    ebene_xy = Rhino.Geometry.Plane.WorldXY

    for c in teil.innen:
        k = _kreis(c, tol)
        if k is None:
            fehler.append("%s: Innenausschnitt ist kein Kreis - freie "
                          "Ausschnitte erst nach T13 (Phase 1b)." % n)
        elif 2 * k.Radius < d_wz - 0.01:
            # Kreistasche raeumt von innen - kleiner als der Fraeser geht nicht
            fehler.append("%s: runder Ausschnitt D%g ist kleiner als der "
                          "Konturfraeser (D%.2f) - als Bohrung taggen."
                          % (n, 2 * k.Radius, d_wz))

    for x, y, d, t in teil.bohrungen:
        if bohrkopf and not any(abs(d - b) < 0.11 for b in bohrkopf):
            fehler.append("%s: Bohrer D%g ist nicht am Bohrkopf %s - HOPS "
                          "bricht ab ('Bohrer existiert nicht', Maschine "
                          "2026-09-03)." % (n, d, bohrkopf))
        if t is not None and teil.dz and abs(t) > teil.dz + 0.01:
            fehler.append("%s: Bohrtiefe %g > Dicke %g - durchbohrt den "
                          "Tisch; 'durch' taggen." % (n, abs(t), teil.dz))
        if teil.umriss is not None:
            p = Rhino.Geometry.Point3d(x, y, 0)
            if teil.umriss.Contains(p, ebene_xy, tol) == \
                    Rhino.Geometry.PointContainment.Outside:
                fehler.append("%s: Bohrung bei %g/%g liegt AUSSERHALB des "
                              "Umrisses (Kantenbohrung? -> eigenes kind)."
                              % (n, x, y))
            else:
                ok, tp = teil.umriss.ClosestPoint(p)
                if ok:
                    abst = p.DistanceTo(teil.umriss.PointAt(tp))
                    if abst < d / 2.0 - tol:
                        fehler.append("%s: Bohrung D%g bei %g/%g schneidet "
                                      "die Kontur (Abstand %.1f < d/2)."
                                      % (n, d, x, y, abst))
                    elif abst < d / 2.0 + 2.0:
                        warnungen.append("%s: Bohrung D%g bei %g/%g nur "
                                         "%.1f mm von der Kontur - "
                                         "Ausbruchgefahr." % (n, d, x, y,
                                                              abst))
    pts = [(x, y, d) for x, y, d, _ in teil.bohrungen]
    for i in range(len(pts)):
        for j in range(i + 1, len(pts)):
            dx = pts[i][0] - pts[j][0]
            dy = pts[i][1] - pts[j][1]
            grenz = (pts[i][2] + pts[j][2]) / 2.0
            if dx * dx + dy * dy < grenz * grenz - 1e-6:
                warnungen.append("%s: zwei Bohrungen naeher als (d1+d2)/2 "
                                 "bei %g/%g." % (n, pts[i][0], pts[i][1]))

    for pos, kante, zh, d, t in teil.kanten:
        if horizontal and not any(abs(d - b) < 0.11 for b in horizontal):
            fehler.append("%s: Kantenbohrer D%g ist nicht am Horizontal-"
                          "Bohrkopf %s." % (n, d, horizontal))
        if teil.dz and zh > teil.dz + 0.01:
            fehler.append("%s: Kantenbohrhoehe %g liegt UEBER dem Teil "
                          "(Dicke %g)." % (n, zh, teil.dz))

    for x1, y1, x2, y2, nb, nt in teil.nuten:
        if nb < float(wzg.get("fraeser_nut_d", 4)) - 1e-6:
            fehler.append("%s: Nut %g mm schmaler als der Nutfraeser." % (n, nb))
        if teil.dz and abs(nt) >= teil.dz:
            fehler.append("%s: Nuttiefe %g bei Dicke %g trennt statt zu "
                          "nuten." % (n, abs(nt), teil.dz))

    for art, x, y, r, t in teil.taschen:
        if t is not None and teil.dz and abs(t) >= teil.dz:
            fehler.append("%s: Taschentiefe %g bei Dicke %g trennt statt "
                          "zu taschen." % (n, abs(t), teil.dz))

    return fehler, warnungen


def baue_hop(teil, wzg, wz):
    """Teil -> HopWriter. DIE Reihenfolge-Regel, EINE Stelle.

    Nut -> Bohrungen -> Kantenbohrungen -> Taschen -> Gravuren ->
    Innenausschnitte -> Aussenkontur ZULETZT (erst dann ist das Teil frei).
    rechteck=True schreibt die T13-immunen Makros (_Kreistasche_V5 mit
    ESXY:=0, _Format_V5); rechteck=False die freie SP-Bahn wie TeilExport
    sie maschinenbelegt faehrt.
    """
    hop = HopWriter(teil.name, teil.dx, teil.dy, teil.dz)
    for x1, y1, x2, y2, nb, nt in teil.nuten:
        hop.nut(x1, y1, x2, y2, nb, nt, int(wzg.get("fraeser_nut", 8)),
                float(wzg.get("fraeser_nut_d", 4)))
    hop.bohrungen(teil.bohrungen, int(wzg.get("bohrer_wzb", 501)),
                  wzg.get("topf_mm", [15, 35]))
    hop.kantenbohrungen(teil.kanten, int(wzg.get("bohrer_wzb", 501)))
    for art, x, y, r, t in teil.taschen:
        hop.kreistasche(x, y, r, t, wz, durch=(t is None))
    if teil.gravuren:
        raise ValueError("%s: Gravur hat noch keinen belegten Emitter "
                         "(Phase 2)." % teil.name)

    tol = sc.doc.ModelAbsoluteTolerance
    ebene_xy = Rhino.Geometry.Plane.WorldXY
    if teil.rechteck:
        for c in teil.innen:
            k = _kreis(c, tol)
            if k is None:
                raise ValueError("%s: Innenausschnitt ist kein Kreis - "
                                 "erst nach T13 (Phase 1b)." % teil.name)
            hop.kreistasche(k.Center.X, k.Center.Y, k.Radius, None, wz,
                            durch=True)
        hop.format_v5(wz)
    else:
        # Richtung normalisieren (war teilabhaengig zufaellig): Umriss im
        # UHRZEIGERSINN wie HOPS' eigener DXF-Import, Ausschnitte entgegen.
        CCW = Rhino.Geometry.CurveOrientation.CounterClockwise
        CW = Rhino.Geometry.CurveOrientation.Clockwise
        umriss = teil.umriss
        if umriss.IsClosed and \
                umriss.ClosedCurveOrientation(ebene_xy) == CCW:
            umriss.Reverse()
        for c in teil.innen:
            if c.IsClosed and c.ClosedCurveOrientation(ebene_xy) == CW:
                c.Reverse()
        # Aussenkontur ZULETZT: erst Bohrungen und Ausschnitte, dann frei
        for c in teil.innen + [umriss]:
            verts, _ = contour_vertices(c)
            if c.IsClosed and len(verts) < 2:
                raise ValueError("%s: geschlossene Kontur kollabiert (%d "
                                 "Stuetzpunkte) - NICHT exportierbar." %
                                 (teil.name, len(verts)))
            if len(verts) >= 2:
                hop.kontur(verts, c.IsClosed, wz)
    return hop


def schreibe(teil, basis, wzg, seite, wz, vor, dxf_basis=None):
    """<basis>.hop + <basis|dxf_basis>.dxf schreiben -> info-Dict.

    Die DXF ist die Sichtkontrolle (Vorlage KB) und laeuft ueber
    HHL.export mit Temp-Objekten - EIN DXF-Weg fuer alle Exporte.
    """
    hop = baue_hop(teil, wzg, wz)
    hop.save(basis + ".hop")

    temp = []
    try:
        temp.append(sc.doc.Objects.AddCurve(teil.umriss))
        for c in teil.innen:
            temp.append(sc.doc.Objects.AddCurve(c))
        for art, x, y, r, t in teil.taschen:
            gid = sc.doc.Objects.AddCircle(Rhino.Geometry.Circle(
                Rhino.Geometry.Plane(Rhino.Geometry.Point3d(x, y, 0),
                                     Rhino.Geometry.Vector3d.ZAxis), r))
            temp.append(gid)
        for x, y, d, tief in teil.bohrungen:
            gid = sc.doc.Objects.AddCircle(Rhino.Geometry.Circle(
                Rhino.Geometry.Plane(Rhino.Geometry.Point3d(x, y, 0),
                                     Rhino.Geometry.Vector3d.ZAxis), d / 2.0))
            rs.SetUserText(gid, "depth",
                           "durch" if tief is None else "%g" % tief)
            temp.append(gid)
        for pos, kante, zh, d, tief in teil.kanten:
            x, y = {"y0": (pos, -zh), "y1": (pos, teil.dy + zh),
                    "x0": (-zh, pos), "x1": (teil.dx + zh, pos)}[kante]
            gid = sc.doc.Objects.AddCircle(Rhino.Geometry.Circle(
                Rhino.Geometry.Plane(Rhino.Geometry.Point3d(x, y, 0),
                                     Rhino.Geometry.Vector3d.ZAxis), d / 2.0))
            rs.SetUserText(gid, "depth", "%g" % tief)
            temp.append(gid)
        info = HHL.export((dxf_basis or basis) + ".dxf",
                          temp, seite, wz, vor,
                          teil.dz + 0.2, teil.dz,
                          vorlage=str(wzg.get("vorlage", "KB")))
    finally:
        rs.DeleteObjects(temp)

    info["teil"] = teil.name
    info["kanten"] = len(teil.kanten)
    info["dicke"] = teil.dz
    info["nuten"] = len(teil.nuten)
    return info
