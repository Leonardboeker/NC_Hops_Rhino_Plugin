# -*- coding: utf-8 -*-
"""TagLeader - Beschriftungspfeile aus UserText-Keys erzeugen.

Zwei Betriebsarten:
  1. LAYOUT (der Normalfall): Detail aktivieren (Doppelklick rein), Teile
     waehlen, TagLeader -> Pfeile entstehen auf dem LAYOUT-Blatt (Papierraum),
     Spitzen zeigen durch das Detail auf die Teile.
  2. Modellraum: normale Ansicht aktiv -> Pfeile in Welt-XY wie gehabt.

Der Pfeiltext ist ein Textfeld (%%<UserText>%%) - aendert sich der Tag am
Objekt, aktualisiert sich der Pfeil von selbst.

Erster Aufruf:  _-RunPythonScript "TagLeader"  -> legt Alias "TagLeader" an.
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino

LAYER = "Beschriftung::TagLeader"


def transformed_bbox(obj, xf):
    corners = rs.BoundingBox(obj)
    pts = []
    for c in corners:
        p = Rhino.Geometry.Point3d(c[0], c[1], c[2])
        if xf is not None:
            p.Transform(xf)
        pts.append(p)
    xs = [p.X for p in pts]
    ys = [p.Y for p in pts]
    # Mitte des Welt-Quaders projiziert = Punkt, der bei Platten sichtbar
    # AUF dem Teil liegt (Bounding-Box-Oberkante liegt oft daneben)
    c = rs.BoundingBox(obj)
    mid = Rhino.Geometry.Point3d((c[0][0] + c[6][0]) / 2.0,
                                 (c[0][1] + c[6][1]) / 2.0,
                                 (c[0][2] + c[6][2]) / 2.0)
    if xf is not None:
        mid.Transform(xf)
    return min(xs), min(ys), max(xs), max(ys), mid


def main():
    objs = rs.GetObjects("Teile fuer Beschriftungspfeile waehlen",
                         preselect=True, select=False)
    if not objs:
        return

    keys = set()
    for o in objs:
        for k in (rs.GetUserText(o) or []):
            keys.add(k)
    if not keys:
        rs.MessageBox("Keine Key-Values an der Auswahl gefunden - erst taggen "
                      "(Befehl: Tag) oder IDs vergeben.", 48, "TagLeader")
        return
    keys = sorted(keys, key=lambda k: (k != "id", k))
    key = rs.ListBox(keys, "Welcher Key soll an den Pfeil?", "TagLeader", keys[0])
    if not key:
        return

    # Kontext: Layout mit aktivem Detail -> Papierraum-Modus
    xf = None
    av = sc.doc.Views.ActiveView
    if isinstance(av, Rhino.Display.RhinoPageView):
        det = av.ActiveDetail
        if det is None:
            rs.MessageBox("Auf dem Layout bitte zuerst ins Detail doppelklicken "
                          "(Detail aktivieren), Teile waehlen, dann TagLeader.",
                          48, "TagLeader")
            return
        xf = det.WorldToPageTransform
        av.SetPageAsActive()  # raus aus dem Detail, Pfeile aufs Blatt

    try:
        th = sc.doc.DimStyles.Current.TextHeight
        if xf is None:
            th *= sc.doc.DimStyles.Current.DimensionScale
    except Exception:
        th = 2.5

    entries = []
    skipped = 0
    min_x = min_y = 1e18
    max_x = max_y = -1e18
    for o in objs:
        val = rs.GetUserText(o, key)
        if not val:
            skipped += 1
            continue
        x0, y0, x1, y1, mid = transformed_bbox(o, xf)
        min_x, min_y = min(min_x, x0), min(min_y, y0)
        max_x, max_y = max(max_x, x1), max(max_y, y1)
        entries.append((mid.X, mid.Y, o, val))
    if not entries:
        rs.MessageBox("Kein gewaehltes Teil traegt den Key '%s'." % key,
                      48, "TagLeader")
        return
    entries.sort(key=lambda e: e[0])

    height = max_y - min_y
    y_line = max_y + max(height * 0.15, th * 4)

    if not rs.IsLayer(LAYER):
        rs.AddLayer(LAYER, (200, 0, 0))

    rs.EnableRedraw(False)
    prev_x = None
    made = 0
    for cx, top_y, o, val in entries:
        gap = max(len(val), 3) * 0.8 * th + th * 2
        x = cx + th * 1.5
        if prev_x is not None and x < prev_x + gap:
            x = prev_x + gap
        prev_x = x
        field = '%%<UserText("%s","%s")>%%' % (o, key)
        leader = rs.AddLeader([(cx, top_y, 0), (x, y_line, 0)], text=field)
        if leader:
            rs.ObjectLayer(leader, LAYER)
            made += 1
    rs.EnableRedraw(True)

    where = "auf dem Layout" if xf is not None else "im Modellraum"
    print("TagLeader: %d Pfeile fuer '%s' %s gesetzt%s." % (
        made, key, where,
        ", %d Teile ohne diesen Key uebersprungen" % skipped if skipped else ""))

    if not rs.IsAlias("TagLeader"):
        rs.AddAlias("TagLeader", '! _-RunPythonScript "TagLeader"')
        print('Alias "TagLeader" angelegt.')


if __name__ == "__main__":
    main()
