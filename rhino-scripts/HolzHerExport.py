# -*- coding: utf-8 -*-
"""HolzHerExport - Konturen CNC-tauglich als DXF exportieren (aCADemy-Weg).

Ablauf (Originale bleiben unangetastet, gearbeitet wird auf Kopien im Speicher):
  1. Kurven waehlen (Vorauswahl geht)
  2. Join - nur innerhalb eines Layers, damit keine Kontur ueber eine
     Layergrenze laeuft
  3. Splines/NURBS -> Linien + echte Boegen, kleine Luecken schliessen
  4. Alles auf den Nullpunkt legen
  5. DXF SELBST schreiben: EINE geschlossene Polylinie je Kontur, Boegen als
     Bulge-Werte

Punkt 5 ist der Kern. Frueher hat das Script `-_Export` aufgerufen und damit
Rhino die DXF schreiben lassen - und Rhinos Export-Schema zerlegt Konturen in
Einzelsegmente (eine Kontur kommt beim Reimport vierteilig zurueck). Mit
eigenem Writer ist das Ergebnis unabhaengig von jeder Rhino-Einstellung.

Erster Aufruf:  _-RunPythonScript "HolzHerExport"
Danach existiert der Alias  HolzHerExport  dauerhaft.
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import os

import holzher_kontur
import holzher_dxf
reload(holzher_kontur)               # Rhino cached Module je Sitzung
reload(holzher_dxf)
from holzher_kontur import prepare, to_origin, contour_vertices, GAP_TOL, MIN_SEG
from holzher_dxf import DxfR12


def main():
    ids = rs.GetObjects("Konturen fuer HolzHer-Export waehlen", rs.filter.curve,
                        preselect=True, select=False)
    if not ids:
        return
    path = rs.SaveFileName("DXF fuer HolzHer speichern", "DXF (*.dxf)|*.dxf||")
    if not path:
        return

    info = export(path, ids)
    if info is None:
        print("Keine Konturen - Abbruch.")
        return

    print("HolzHerExport: %d Konturen (%d Stuetzpunkte) -> %s"
          % (info["konturen"], info["stuetzpunkte"], os.path.basename(path)))
    print("  %.1f x %.1f mm, Layer: %s"
          % (info["breite"], info["hoehe"], ", ".join(info["layer"])))
    if info["mikro_weg"]:
        print("  %d Mikro-Segment(e) unter %s mm zusammengefasst"
              % (info["mikro_weg"], MIN_SEG))

    if info["offen"]:
        rs.MessageBox("%d Kontur(en) sind OFFEN geblieben (Luecke > %s mm) - "
                      "vor dem Fraesen pruefen!" % (info["offen"], GAP_TOL),
                      48, "HolzHerExport")
    if info["leer"]:
        rs.MessageBox("%d Kontur(en) wurden NICHT exportiert (zu klein oder nur "
                      "ein Stuetzpunkt)." % info["leer"], 48, "HolzHerExport")
    if info["z_spanne"] > 10 * sc.doc.ModelAbsoluteTolerance:
        rs.MessageBox("Die Konturen sind nicht planar zu XY (Z-Spanne %.2f mm)."
                      "\nDie DXF enthaelt nur die Projektion - Masse pruefen!"
                      % info["z_spanne"], 48, "HolzHerExport")

    if not rs.IsAlias("HolzHerExport"):
        rs.AddAlias("HolzHerExport", '! _-RunPythonScript "HolzHerExport"')
        print('Alias "HolzHerExport" angelegt - ab jetzt einfach als Befehl tippen.')


def export(path, curve_ids):
    """Kern ohne Dialoge - so auch headless testbar."""
    konturen, offen = prepare(curve_ids, nach_layer=True)
    if not konturen:
        return None
    breite, hoehe, z_spanne = to_origin(konturen)

    d = DxfR12()
    # Ohne Rohteil-Rechteck sind $EXTMIN/$EXTMAX die einzige Groessenangabe in
    # der Datei; die Stuetzpunkte allein unterschlagen die Bulge-Boegen.
    d.extents(0.0, 0.0, breite, hoehe)
    n_kont = 0
    n_pts = 0
    n_drop = 0
    leer = 0
    layer = []
    for c, lay in konturen:
        verts, dropped = contour_vertices(c)
        n_drop += dropped
        if len(verts) < 2:
            leer += 1                # nicht still verschlucken, sondern melden
            continue
        n_pts += len(verts)
        n_kont += 1
        if lay not in layer:
            layer.append(lay)
        # EINE Polylinie je Kontur; Farbe ByLayer (kein Code 62)
        d.polyline(lay, verts, c.IsClosed, None, 0)
    d.save(path)

    return {"konturen": n_kont, "stuetzpunkte": n_pts, "offen": offen,
            "mikro_weg": n_drop, "leer": leer, "layer": layer,
            "breite": breite, "hoehe": hoehe, "z_spanne": z_spanne}


if __name__ == "__main__":
    main()
