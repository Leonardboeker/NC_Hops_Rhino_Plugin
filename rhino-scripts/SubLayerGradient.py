# -*- coding: utf-8 -*-
"""SubLayerGradient - Layer waehlen, Anzahl Unterlayer angeben,
Unterlayer werden im Farbverlauf der Elternfarbe (voll -> hell) angelegt.

Erster Aufruf:  _-RunPythonScript "SubLayerGradient"
Danach existiert der Alias  SubLayerGradient  dauerhaft.
"""
import rhinoscriptsyntax as rs

MAX_LIGHTEN = 0.8  # letzter Unterlayer: 80% Richtung Weiss


def main():
    parent = rs.GetLayer("Eltern-Layer waehlen")
    if not parent:
        return
    n = rs.GetInteger("Wieviele Unterlayer?", 5, 1, 99)
    if not n:
        return

    base = rs.LayerColor(parent)
    made = []
    for i in range(n):
        t = (float(i) / (n - 1)) * MAX_LIGHTEN if n > 1 else 0.0
        col = (int(base.R + (255 - base.R) * t),
               int(base.G + (255 - base.G) * t),
               int(base.B + (255 - base.B) * t))
        name = "%02d" % (i + 1)
        full = rs.AddLayer(name, col, parent=parent)
        made.append(full)

    print("%d Unterlayer unter '%s' angelegt: %s" % (len(made), parent,
          ", ".join(l.split("::")[-1] for l in made)))

    if not rs.IsAlias("SubLayerGradient"):
        rs.AddAlias("SubLayerGradient", '! _-RunPythonScript "SubLayerGradient"')
        print('Alias "SubLayerGradient" angelegt.')


if __name__ == "__main__":
    main()
