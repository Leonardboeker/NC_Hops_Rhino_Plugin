# -*- coding: utf-8 -*-
"""PartBoard - Teile eines Layers als Uebersichtstafel auslegen.

Ablauf: Layer waehlen -> Abstand -> Teile pro Reihe -> Klickpunkt (oben links).
Pro Teil: Beschriftung (Name + Tags), 3D-Kopie, darunter Make2D (europaeische
Projektion, sichtbar + verdeckt). Originale bleiben unberuehrt; alles landet
unter PLAN::<Layername>.

Erster Aufruf:  _-RunPythonScript "PartBoard"
Danach existiert der Alias  PartBoard  dauerhaft.
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc

STICKY_SPACING = "partboard_spacing"
STICKY_PERROW = "partboard_perrow"
LOGFILE = r"C:\Users\leona\Desktop\partboard_log.txt"


def trace(msg):
    try:
        with open(LOGFILE, "a") as f:
            f.write(msg + "\n")
    except Exception:
        pass
    print(msg)


def dims(bb):
    return (bb[6][0] - bb[0][0], bb[6][1] - bb[0][1], bb[6][2] - bb[0][2])


def fmt_mm(v):
    s = "%.1f" % v
    return s[:-2] if s.endswith(".0") else s


def fit_height(text, want_h, max_w):
    # Texthoehe kappen, damit der String (~0.7*h Breite pro Zeichen) in die Zelle passt
    n = max(len(text), 1)
    return min(want_h, max_w / (0.7 * n))


def tag_line(obj):
    vals = [rs.GetUserText(obj, k) for k in ("process", "material", "thickness")]
    return " | ".join([v for v in vals if v])


def ensure_layer(path, color=None):
    if not rs.IsLayer(path):
        rs.AddLayer(path, color)
    return path


def find_perspective_view():
    for v in rs.ViewNames():
        if rs.IsViewPerspective(v):
            return v
    return None


def main():
    trace("--- PartBoard Start ---")
    layer = rs.GetLayer("Layer mit den Teilen waehlen")
    if not layer:
        trace("Abbruch: kein Layer gewaehlt")
        return
    trace("Layer: %s" % layer)
    spacing = rs.GetReal("Abstand (mm)", sc.sticky.get(STICKY_SPACING, 50.0), 1.0)
    if spacing is None:
        trace("Abbruch: kein Abstand")
        return
    per_row = rs.GetInteger("Teile pro Reihe", sc.sticky.get(STICKY_PERROW, 6), 1, 50)
    if per_row is None:
        trace("Abbruch: keine Reihenanzahl")
        return
    anchor = rs.GetPoint("Ankerpunkt klicken (oben links der Tafel)")
    if anchor is None:
        trace("Abbruch: kein Klickpunkt")
        return
    trace("Eingaben ok: Abstand=%s, proReihe=%s, Punkt=%s" % (spacing, per_row, anchor))
    sc.sticky[STICKY_SPACING] = spacing
    sc.sticky[STICKY_PERROW] = per_row
    try:
        board(layer, spacing, per_row, anchor)
        trace("--- PartBoard Ende ohne Exception ---")
    except Exception as e:
        trace("EXCEPTION: %r" % e)
        rs.MessageBox("PartBoard-Fehler: %r\nLog: %s" % (e, LOGFILE), 48, "PartBoard")
        raise

    if not rs.IsAlias("PartBoard"):
        rs.AddAlias("PartBoard", '! _-RunPythonScript "PartBoard"')
        print('Alias "PartBoard" angelegt.')


def layer_with_children(layer):
    layers = [layer]
    for child in (rs.LayerChildren(layer) or []):
        layers.extend(layer_with_children(child))
    return layers


def board(layer, spacing, per_row, anchor):
    objs = []
    for lay in layer_with_children(layer):
        objs.extend(rs.ObjectsByLayer(lay) or [])
    visible = [o for o in objs if not rs.IsObjectHidden(o) and not rs.IsObjectLocked(o)]
    trace("Gefunden: %d Objekte, davon %d sichtbar/entsperrt"
          % (len(objs), len(visible)))
    objs = visible
    if not objs:
        rs.MessageBox("Keine sichtbaren Objekte auf '%s' oder seinen "
                      "Unter-Layern gefunden." % layer, 48, "PartBoard")
        return []

    def sort_key(o):
        n = rs.ObjectName(o)
        d = dims(rs.BoundingBox(o))
        return (0, n) if n else (1, -(d[0] * d[1]))
    objs = sorted(objs, key=sort_key)

    leaf = layer.split("::")[-1]
    lay3d = ensure_layer("PLAN::%s::3D" % leaf)
    lay2dv = ensure_layer("PLAN::%s::2D-sichtbar" % leaf, (0, 0, 0))
    lay2dh = ensure_layer("PLAN::%s::2D-verdeckt" % leaf, (128, 128, 128))
    laytxt = ensure_layer("PLAN::%s::Text" % leaf)

    persp = find_perspective_view()
    prev_view = rs.CurrentView()
    if persp:
        rs.CurrentView(persp)
    rs.EnableRedraw(False)
    try:
        return _board_core(objs, leaf, spacing, per_row, anchor,
                           lay3d, lay2dv, lay2dh, laytxt)
    finally:
        rs.UnselectAllObjects()
        rs.EnableRedraw(True)
        if persp:
            rs.CurrentView(prev_view)


def _board_core(items_in, leaf, spacing, per_row, anchor,
                lay3d, lay2dv, lay2dh, laytxt):
    objs = items_in
    # Phase 1: Kopien + Make2D am Originalort erzeugen, Masse sammeln
    items = []
    made_err = []
    for idx, o in enumerate(objs):
        label = rs.ObjectName(o) or "%s-%02d" % (leaf, idx + 1)
        copy = rs.CopyObject(o)
        rs.UnselectAllObjects()
        rs.SelectObject(copy)
        rs.Command("-_Make2D _DrawingLayout=_FourViewEuropean "
                   "_ShowTangentEdges=_Yes _CreateHiddenLines=_Yes "
                   "_MaintainSourceLayers=_No _GroupOutput=_Yes _Enter", False)
        flat = rs.LastCreatedObjects(select=False)
        trace("Make2D '%s': %s" % (label, "%d Linien" % len(flat) if flat else "LEER"))
        if not flat:
            made_err.append(label)
        items.append({"copy": copy, "bb3": rs.BoundingBox(copy),
                      "flat": flat or [], "bb2": rs.BoundingBox(flat) if flat else None,
                      "label": label, "tags": tag_line(o)})

    # Phase 2: Zellenbreite aus 3D- UND 2D-Breite, dann layouten
    cell_w = spacing + max(max(dims(it["bb3"])[0],
                               dims(it["bb2"])[0] if it["bb2"] else 0.0)
                           for it in items)
    name_h = min(spacing * 0.35, cell_w * 0.10)
    text_block = name_h * 4.2

    y_cursor = anchor[1]
    groups = [items[i:i + per_row] for i in range(0, len(items), per_row)]
    for group in groups:
        row3d_h = max(dims(it["bb3"])[1] for it in group)
        text_top = y_cursor
        base3d = text_top - text_block - row3d_h
        top2d = base3d - spacing
        row2d_h = 0.0

        for ci, it in enumerate(group):
            x = anchor[0] + ci * cell_w

            texts = []
            y_txt = text_top
            h1 = fit_height(it["label"], name_h, cell_w - spacing)
            t1 = rs.AddText(it["label"], (x, y_txt, 0), h1)
            rs.ObjectLayer(t1, laytxt)
            texts.append(t1)
            y_txt -= h1 * 1.6
            if it["tags"]:
                h2 = fit_height(it["tags"], name_h * 0.6, cell_w - spacing)
                t2 = rs.AddText(it["tags"], (x, y_txt, 0), h2)
                rs.ObjectLayer(t2, laytxt)
                texts.append(t2)
                y_txt -= h2 * 1.6

            d3 = dims(it["bb3"])
            dline = "%s x %s x %s mm" % tuple(fmt_mm(v) for v in d3)
            h3 = fit_height(dline, name_h * 0.6, cell_w - spacing)
            t3 = rs.AddText(dline, (x, y_txt, 0), h3)
            rs.ObjectLayer(t3, laytxt)
            texts.append(t3)

            bb = it["bb3"]
            rs.MoveObject(it["copy"], (x - bb[0][0], base3d - bb[0][1], -bb[0][2]))
            rs.ObjectLayer(it["copy"], lay3d)

            if it["flat"]:
                fbb = it["bb2"]
                rs.MoveObjects(it["flat"], (x - fbb[0][0], top2d - fbb[6][1], 0))
                row2d_h = max(row2d_h, dims(fbb)[1])
                for f in it["flat"]:
                    src = rs.ObjectLayer(f).lower()
                    hidden = ("hidden" in src) or ("verdeckt" in src)
                    rs.ObjectLayer(f, lay2dh if hidden else lay2dv)

            # alles eines Teils zu einer Gruppe -> als Ganzes verschiebbar
            grp = rs.AddGroup()
            rs.AddObjectsToGroup(texts + [it["copy"]] + it["flat"], grp)

        y_cursor = top2d - row2d_h - spacing * 2.0

    # leere Make2D-Layer aufraeumen: Kinder zuerst, mit Existenz-Check
    m2d = [l for l in (rs.LayerNames() or [])
           if l.split("::")[0].lower().startswith("make2d")]
    for lname in sorted(m2d, key=lambda s: -s.count("::")):
        try:
            if rs.IsLayer(lname) and not rs.ObjectsByLayer(lname) \
                    and not rs.LayerChildren(lname):
                rs.DeleteLayer(lname)
        except Exception:
            pass

    print("PartBoard: %d Teile ausgelegt unter PLAN::%s." % (len(items), leaf))
    if made_err:
        print("Make2D fehlgeschlagen bei: %s" % ", ".join(made_err))
    return made_err
