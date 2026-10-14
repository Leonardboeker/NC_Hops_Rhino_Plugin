# -*- coding: utf-8 -*-
"""CncSchau - TextDots zeigen, was CncZuweisen vergeben hat.

Je getaggtem Objekt ein Dot "art tiefe" auf Layer CNC::Schau (Muster:
TagDot). Erneut ausfuehren = Refresh. Die Originalobjekte werden NICHT
angefasst - kein Umfaerben, keine Layerwechsel (das waere eine Aenderung
an der Zeichnung). Council 2026-08-31: die Sichtkontrolle gehoert in
Phase 1, nicht hinter das Risiko, das sie absichert.

Erster Aufruf: _-RunPythonScript "CncSchau"
"""
import rhinoscriptsyntax as rs

LAYER = "CNC::Schau"
SRC = "cncschau_source"


def beschriftung(o):
    kind = rs.GetUserText(o, "kind")
    if not kind:
        return None
    if kind == "rohteil":
        teile = [kind, (rs.GetUserText(o, "thickness") or "?") + "mm",
                 rs.GetUserText(o, "material") or ""]
    elif kind == "nut":
        teile = [kind, "b" + (rs.GetUserText(o, "nut_b") or "?"),
                 "t" + (rs.GetUserText(o, "nut_t") or "?")]
    elif kind == "kantenbohrung":
        teile = [kind, "t" + (rs.GetUserText(o, "depth") or "?"),
                 "h" + (rs.GetUserText(o, "hoehe") or "?")]
    else:
        teile = [kind, rs.GetUserText(o, "depth") or "TIEFE FEHLT"]
    part = rs.GetUserText(o, "part")
    if part:
        teile.append("[%s]" % part)
    return " ".join(t for t in teile if t)


def main():
    objs = rs.GetObjects("Objekte fuer die CNC-Kontrolle waehlen "
                         "(Enter = alle getaggten)", rs.filter.curve,
                         preselect=True, select=False)
    if objs is None:
        objs = []
    if not objs:
        objs = [o for o in (rs.AllObjects() or [])
                if rs.GetUserText(o, "kind")]
    if not objs:
        print("CncSchau: nichts getaggt - erst CncZuweisen.")
        return

    if not rs.IsLayer(LAYER):
        rs.AddLayer(LAYER, (255, 100, 0))

    wanted = set(str(o) for o in objs)
    alte = [d for d in (rs.ObjectsByLayer(LAYER) or [])
            if rs.GetUserText(d, SRC) in wanted]
    if alte:
        rs.DeleteObjects(alte)

    rs.EnableRedraw(False)
    made = 0
    ohne = 0
    for o in objs:
        txt = beschriftung(o)
        if not txt:
            ohne += 1
            continue
        bb = rs.BoundingBox(o)
        mid = ((bb[0][0] + bb[6][0]) / 2.0, (bb[0][1] + bb[6][1]) / 2.0,
               (bb[0][2] + bb[6][2]) / 2.0)
        dot = rs.AddTextDot(txt, mid)
        rs.ObjectLayer(dot, LAYER)
        rs.SetUserText(dot, SRC, str(o))
        made += 1
    rs.EnableRedraw(True)

    print("CncSchau: %d Dot(s) auf %s%s (erneut ausfuehren = Refresh)."
          % (made, LAYER,
             ", %d Objekt(e) OHNE kind-Tag" % ohne if ohne else ""))

    if not rs.IsAlias("CncSchau"):
        rs.AddAlias("CncSchau", '! _-RunPythonScript "CncSchau"')
        print('Alias "CncSchau" angelegt.')


if __name__ == "__main__":
    main()
