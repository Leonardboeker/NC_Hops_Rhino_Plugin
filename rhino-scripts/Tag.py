# -*- coding: utf-8 -*-
"""Tag - Teile mit process/material/thickness taggen (Attribute UserText).

Workflow: fertige Teile einer Sorte selektieren -> Tag -> drei Bestaetigungen
(vorbelegt aus Layer-Namen, Geometrie und letzter Eingabe) -> fertig.
Werte kommen aus D:\\Projekte\\SynologyDrive\\_standards\\tags.json.
status wird auf "open" gesetzt, ein vorhandener status bleibt unangetastet.

Erster Aufruf:  _-RunPythonScript "Tag"   -> legt Alias "Tag" an.
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import json
import os

TAGS_JSON = r"D:\Projekte\SynologyDrive\_standards\tags.json"
PART_FILTER = 4 + 8 + 16 + 32 + 4096  # Kurven, Flaechen, Polyflaechen, Meshes, Bloecke


def load_tags():
    if not os.path.isfile(TAGS_JSON):
        rs.MessageBox("tags.json fehlt:\n" + TAGS_JSON +
                      "\n\nOhne Werteliste wird nicht getaggt (keine stillen "
                      "Defaults).", 16, "Tag")
        return None
    key = "tags_json_cache"
    mtime = os.path.getmtime(TAGS_JSON)
    cached = sc.sticky.get(key)
    if cached and cached[0] == mtime:
        return cached[1]
    with open(TAGS_JSON, "r") as f:
        data = json.load(f)
    sc.sticky[key] = (mtime, data)
    return data


def derive_process(obj_ids, cfg):
    """Layer-Name-Hinweise (Substring, case-insensitive), sonst None."""
    hints = cfg.get("layer_hints", {})
    for oid in obj_ids:
        layer = (rs.ObjectLayer(oid) or "").lower()
        for frag, vals in hints.items():
            if frag in layer and "process" in vals:
                return vals["process"]
    return None


def derive_thickness(obj_ids, cfg):
    """Kleinste Bounding-Box-Kante des ersten Solids, gesnappt auf Lagerstaerken.
    ponytail: Welt-BBox - bei gedrehten Teilen ungenau, deshalb immer nur
    Vorschlag mit Bestaetigung; Upgrade-Pfad: BBox in Objekt-Ebene."""
    stock = cfg.get("thickness_stock", [])
    for oid in obj_ids:
        if rs.IsBrep(oid) or rs.IsPolysurface(oid) or rs.IsSurface(oid):
            bb = rs.BoundingBox(oid)
            if not bb:
                continue
            dims = [(bb[1] - bb[0]).Length, (bb[3] - bb[0]).Length,
                    (bb[4] - bb[0]).Length]
            t = min(d for d in dims if d > 1e-6)
            for s in stock:
                if abs(t - s) <= 0.3:
                    return s
            return round(t, 1)
    return None


def pick(title, options, default):
    """ListBox mit Default; Abbruch -> None."""
    if default in options:
        # Default nach vorn, damit Enter reicht
        options = [default] + [o for o in options if o != default]
    return rs.ListBox(options, "Wert waehlen (Abbrechen = Abbruch)", title, default)


def main():
    cfg = load_tags()
    if cfg is None:
        return
    keys = cfg["keys"]

    ids = rs.GetObjects("Teile zum Taggen waehlen (eine Sorte pro Aufruf)",
                        PART_FILTER, preselect=True, select=False)
    if not ids:
        return

    # --- process ---
    proc_default = derive_process(ids, cfg) or sc.sticky.get("tag_last_process")
    process = pick("Tag 1/3 - process", keys["process"], proc_default)
    if not process:
        return

    # --- material ---
    mat_default = sc.sticky.get("tag_last_material")
    material = pick("Tag 2/3 - material", keys["material"], mat_default)
    if not material:
        return

    # --- thickness (nur wo sinnvoll) ---
    thickness = None
    if process in cfg.get("thickness_required_for", []):
        t_default = derive_thickness(ids, cfg) or sc.sticky.get("tag_last_thickness") or 18
        thickness = rs.GetReal("Tag 3/3 - thickness in mm", t_default, 0.1, 200)
        if thickness is None:
            return

    for oid in ids:
        rs.SetUserText(oid, "process", process)
        rs.SetUserText(oid, "material", material)
        if thickness is not None:
            rs.SetUserText(oid, "thickness", str(thickness))
        if not rs.GetUserText(oid, "status"):
            rs.SetUserText(oid, "status", "open")

    sc.sticky["tag_last_process"] = process
    sc.sticky["tag_last_material"] = material
    if thickness is not None:
        sc.sticky["tag_last_thickness"] = thickness

    t_txt = (" / " + str(thickness) + " mm") if thickness is not None else ""
    print("Getaggt: %d Teil(e) als %s / %s%s" % (len(ids), process, material, t_txt))

    if not rs.IsAlias("Tag"):
        rs.AddAlias("Tag", '! _-RunPythonScript "Tag"')
        print('Alias "Tag" angelegt.')


if __name__ == "__main__":
    main()
