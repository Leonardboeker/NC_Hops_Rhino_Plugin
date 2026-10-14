# -*- coding: utf-8 -*-
"""Gemeinsame Kurvenaufbereitung fuer die HolzHer-Exporte.

Hier steht die Logik, die BEIDE Exporte brauchen (HolzHerExport fuer aCADemy,
HolzHerLayerExport fuer den HOPS-Layer-Import): Kurven joinen, Luecken
schliessen, Splines in Linien und Boegen wandeln und daraus die Vertexliste
EINER Polylinie bauen.

Warum eine Polylinie und nicht Einzelsegmente: Sobald eine Kontur als viele
LINE/ARC-Entities in der DXF steht, muss der Empfaenger sie wieder verketten -
und daran scheitern sowohl HOPS als auch Rhinos eigener DXF-Import. Eine
geschlossene Polylinie mit Bulge-Boegen kann niemand zerlegen.
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import math

ARC_TOL = 0.01   # mm  - max. Abweichung Spline -> Bogen
ANG_TOL = 0.5    # Grad - Winkeltoleranz der Bogenanpassung
GAP_TOL = 0.2    # mm  - Luecken bis hierhin werden automatisch geschlossen
MIN_SEG = 0.05   # mm  - kuerzere Segmente werden zusammengefasst


def bulge_of(seg, p0, p1):
    """tan(Bogenwinkel/4), Vorzeichen = Drehsinn; 0 fuer Geraden."""
    if seg.IsLinear(ARC_TOL):
        return 0.0
    pm = seg.PointAt(seg.Domain.Mid)
    arc = Rhino.Geometry.Arc(p0, pm, p1)     # geht exakt durch beide Enden
    if not arc.IsValid or arc.Angle <= 1e-9:
        return 0.0
    # Drehsinn aus dem Kreuzprodukt (p0->pm) x (pm->p1) in der XY-Ebene
    ax, ay = pm.X - p0.X, pm.Y - p0.Y
    bx, by = p1.X - pm.X, p1.Y - pm.Y
    sign = 1.0 if (ax * by - ay * bx) >= 0 else -1.0
    return math.tan(arc.Angle / 4.0) * sign


def contour_vertices(curve):
    """Kurve -> ([(x, y, bulge), ...], Anzahl zusammengefasster Mikro-Segmente).

    Die Stuetzpunkte sind exakt die Segmentenden der Kurve (nicht die Enden
    angefitteter Boegen), damit aufeinanderfolgende Segmente garantiert
    denselben Punkt teilen. Die Kruemmung steckt im Bulge.
    """
    closed = curve.IsClosed          # VOR der Konversion merken
    tol = sc.doc.ModelAbsoluteTolerance

    # VOLLKREIS-Sonderfall: DuplicateSegments gibt einen Kreis als EIN
    # Segment mit Start==Ende zurueck -> bulge_of waere 0, die Kontur
    # verschwaende still (die teuerste Fehlerklasse laut MASCHINENABGLEICH).
    # HOPS' eigener Import schreibt Vollkreise als ZWEI Halbboegen - genau so.
    ok_c, kreis = curve.TryGetCircle(tol)
    if closed and ok_c:
        c0 = kreis.Center
        r = kreis.Radius
        vorz = 1.0
        try:
            if curve.ClosedCurveOrientation(Rhino.Geometry.Plane.WorldXY) == \
                    Rhino.Geometry.CurveOrientation.Clockwise:
                vorz = -1.0
        except Exception:
            pass
        return [(c0.X - r, c0.Y, vorz), (c0.X + r, c0.Y, vorz)], 0

    conv = curve.ToArcsAndLines(ARC_TOL, Rhino.RhinoMath.ToRadians(ANG_TOL),
                                0.0, 0.0)
    if conv:
        curve = conv
    segs = curve.DuplicateSegments()
    if not segs:
        segs = [curve]

    verts = []
    dropped = 0
    for s in segs:
        p0, p1 = s.PointAtStart, s.PointAtEnd
        # Abstand NUR in XY messen: die Kontur wird ohnehin nach XY projiziert,
        # und mit 3D-Abstand wuerde der Filter auf einer Kontur bei z. B. Z=19
        # nie greifen.
        if verts and math.hypot(p0.X - verts[-1][0],
                                p0.Y - verts[-1][1]) < MIN_SEG:
            # Mikro-Segment davor: dessen Stuetzpunkt sparen wir ein, aber die
            # Kruemmung DIESES Segments muss auf den ueberlebenden Vertex -
            # sonst wird aus einem Bogen beliebiger Laenge stumm eine Sehne.
            verts[-1] = (verts[-1][0], verts[-1][1], bulge_of(s, p0, p1))
            dropped += 1
            continue
        verts.append((p0.X, p0.Y, bulge_of(s, p0, p1)))

    if not closed and verts:
        # Offene Kontur: der Endpunkt des letzten Segments ist kein
        # Segmentstart und fehlt sonst - die Polylinie wuerde ein Segment zu
        # frueh enden.
        pe = segs[-1].PointAtEnd
        if math.hypot(pe.X - verts[-1][0], pe.Y - verts[-1][1]) >= MIN_SEG:
            verts.append((pe.X, pe.Y, 0.0))   # letzter Vertex traegt keinen Bulge
    elif closed and len(verts) > 3:
        # Geschlossene Kontur: das Schlusssegment verts[-1] -> verts[0] sieht
        # die Schleife nie, weil es kein Folgesegment hat, an dem der Filter
        # greifen koennte. Sitzt der Splitter dort, bliebe ein 0,03-mm-Stueck
        # in der Datei - genau die Groesse, an der HOPS Konturen zerreisst.
        if math.hypot(verts[-1][0] - verts[0][0],
                      verts[-1][1] - verts[0][1]) < MIN_SEG:
            verts.pop()          # Bulge gehoerte zum Mikro-Segment, faellt mit
            dropped += 1
    return verts, dropped


def prepare(curve_ids, nach_layer=False):
    """Auswahl -> Liste von Konturen (Kopien), Luecken geschlossen.

    nach_layer=True joint NUR innerhalb eines Rhino-Layers und gibt
    (kontur, layername) zurueck. Das erhaelt die Layerinformation und
    verhindert nebenbei, dass eine Kontur ueber eine Layergrenze laeuft -
    HOPS beginnt an jedem Layerwechsel sonst eine neue Bearbeitung.
    """
    tol = sc.doc.ModelAbsoluteTolerance
    gruppen = {}
    for i in curve_ids:
        crv = rs.coercecurve(i)
        if crv is None:
            continue
        lay = rs.ObjectLayer(i) if nach_layer else "0"
        gruppen.setdefault(lay, []).append(crv.DuplicateCurve())

    konturen = []
    offen = 0
    for lay in sorted(gruppen):
        joined = Rhino.Geometry.Curve.JoinCurves(gruppen[lay], tol)
        if not joined:
            joined = gruppen[lay]
        for c in joined:
            if not c.IsClosed:
                c.MakeClosed(GAP_TOL)
            if not c.IsClosed:
                offen += 1
            konturen.append((c, lay))
    return konturen, offen


def to_origin(konturen, extra=None):
    """Konturen (und optional weitere Geometrie) auf 0,0 schieben.

    `extra` wird MITVERSCHOBEN, zieht die Bounding Box aber NICHT auf - so
    behalten z. B. Bohrkreise ausserhalb der Platte ihren Ueberstand, aus dem
    HOPS die Z-Hoehe einer Kantenbohrung liest.
    Rueckgabe: (breite, hoehe, z_spanne)
    """
    bb = Rhino.Geometry.BoundingBox.Empty
    for c, _ in konturen:
        bb.Union(c.GetBoundingBox(True))
    shift = Rhino.Geometry.Transform.Translation(-bb.Min.X, -bb.Min.Y, 0)
    for c, _ in konturen:
        c.Transform(shift)
    for g in (extra or []):
        g.Transform(shift)
    return bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y, bb.Max.Z - bb.Min.Z
