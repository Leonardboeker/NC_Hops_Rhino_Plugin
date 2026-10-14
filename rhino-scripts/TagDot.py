# -*- coding: utf-8 -*-
"""TagDot - Anzeigepunkt (TextDot) auf jedem Teil mit dem Wert eines Keys.

Teile waehlen -> Key waehlen -> pro Teil ein TextDot in der Teilemitte mit
dem Wert. Erneut ausfuehren = Refresh: alte Dots derselben Teile/Keys werden
ersetzt (nach Verschieben oder Tag-Aenderung einfach nochmal laufen lassen).
Dots landen auf Layer Beschriftung::TagDot.

Erster Aufruf:  _-RunPythonScript "TagDot"  -> legt Alias "TagDot" an.
"""
import rhinoscriptsyntax as rs

LAYER = "Beschriftung::TagDot"
SRC = "tagdot_source"
SRCKEY = "tagdot_key"


def main():
    objs = rs.GetObjects("Teile fuer Anzeigepunkte waehlen",
                         preselect=True, select=False)
    if not objs:
        return

    keys = set()
    for o in objs:
        for k in (rs.GetUserText(o) or []):
            keys.add(k)
    if not keys:
        rs.MessageBox("Keine Key-Values an der Auswahl - erst taggen (Tag) "
                      "oder IDs vergeben.", 48, "TagDot")
        return
    keys = sorted(keys, key=lambda k: (k != "id", k))
    key = rs.ListBox(keys, "Welcher Key soll angezeigt werden?", "TagDot", keys[0])
    if not key:
        return

    if not rs.IsLayer(LAYER):
        rs.AddLayer(LAYER, (255, 100, 0))

    # Refresh: alte Dots dieser Teile fuer diesen Key entfernen
    wanted = set(str(o) for o in objs)
    old = []
    for d in (rs.ObjectsByLayer(LAYER) or []):
        if rs.GetUserText(d, SRCKEY) == key and rs.GetUserText(d, SRC) in wanted:
            old.append(d)
    if old:
        rs.DeleteObjects(old)

    rs.EnableRedraw(False)
    made = 0
    skipped = 0
    for o in objs:
        val = rs.GetUserText(o, key)
        if not val:
            skipped += 1
            continue
        bb = rs.BoundingBox(o)
        mid = ((bb[0][0] + bb[6][0]) / 2.0, (bb[0][1] + bb[6][1]) / 2.0,
               (bb[0][2] + bb[6][2]) / 2.0)
        dot = rs.AddTextDot(val, mid)
        rs.ObjectLayer(dot, LAYER)
        rs.SetUserText(dot, SRC, str(o))
        rs.SetUserText(dot, SRCKEY, key)
        made += 1
    rs.EnableRedraw(True)

    print("TagDot: %d Punkte fuer '%s' gesetzt%s (erneut ausfuehren = Refresh)."
          % (made, key, ", %d ohne Key uebersprungen" % skipped if skipped else ""))

    if not rs.IsAlias("TagDot"):
        rs.AddAlias("TagDot", '! _-RunPythonScript "TagDot"')
        print('Alias "TagDot" angelegt.')


if __name__ == "__main__":
    main()
