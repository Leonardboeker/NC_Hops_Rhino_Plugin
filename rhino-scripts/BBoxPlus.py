# -*- coding: utf-8 -*-
"""BBoxPlus - Bounding Box PRO gewaehltem Objekt, in X und Y vergroessert.

Aufschlag gilt gesamt je Achse: 10 mm eingeben = 5 mm links + 5 mm rechts
(bzw. vorne/hinten). Z bleibt unveraendert. An der Mitte der Unterseite
jeder Box sitzt ein Referenzpunkt; Punkt + Box sind gruppiert.
Flache Objekte (keine Z-Ausdehnung) ergeben ein Rechteck statt einer Box.

Erster Aufruf:  _-RunPythonScript "BBoxPlus"  -> legt Alias "BBoxPlus" an.
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc

STICKY = "bboxplus_offset"


def main():
    objs = rs.GetObjects("Objekte fuer Bounding Boxen waehlen",
                         preselect=True, select=False)
    if not objs:
        return
    off = rs.GetReal("Aufschlag gesamt je Achse in X/Y (mm)",
                     sc.sticky.get(STICKY, 10.0), 0.0)
    if off is None:
        return
    sc.sticky[STICKY] = off
    half = off / 2.0

    tol = sc.doc.ModelAbsoluteTolerance
    made = []
    rs.EnableRedraw(False)
    for o in objs:
        bb = rs.BoundingBox(o)
        if not bb:
            continue
        x0, y0, z0 = bb[0][0] - half, bb[0][1] - half, bb[0][2]
        x1, y1, z1 = bb[6][0] + half, bb[6][1] + half, bb[6][2]

        if (z1 - z0) > tol:
            pts = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
                   (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
            shape = rs.AddBox(pts)
        else:
            shape = rs.AddRectangle(
                rs.MovePlane(rs.WorldXYPlane(), (x0, y0, z0)),
                x1 - x0, y1 - y0)

        ref = rs.AddPoint(((x0 + x1) / 2.0, (y0 + y1) / 2.0, z0))
        grp = rs.AddGroup()
        rs.AddObjectsToGroup([shape, ref], grp)
        made.extend([shape, ref])

    rs.EnableRedraw(True)
    rs.UnselectAllObjects()
    rs.SelectObjects(made)
    print("BBoxPlus: %d Boxen erzeugt (Aufschlag %s mm je Achse = %s mm je "
          "Seite), Referenzpunkt Mitte Unterseite." % (len(made) // 2, off, half))

    if not rs.IsAlias("BBoxPlus"):
        rs.AddAlias("BBoxPlus", '! _-RunPythonScript "BBoxPlus"')
        print('Alias "BBoxPlus" angelegt.')


if __name__ == "__main__":
    main()
