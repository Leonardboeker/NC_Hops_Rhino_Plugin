# -*- coding: utf-8 -*-
"""TagAudit - prueft Tags, bevor sie in Stueckliste oder Export gehen.

Mit Auswahl:   streng - JEDES gewaehlte Teil muss vollstaendig getaggt sein.
Ohne Auswahl:  prueft alle getaggten Objekte im Dokument.

Findet:
  - fehlende Pflicht-Keys (process/material; thickness je nach process)
  - Werte, die nicht in tags.json stehen (Tippfehler/veraltete Werte)
  - doppelte IDs (entstehen beim Kopieren/Splitten - Stueckliste zaehlt falsch)
Problemteile werden am Ende SELEKTIERT, damit man sie direkt sieht.

Erster Aufruf:  _-RunPythonScript "TagAudit"   -> legt Alias "TagAudit" an.
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import json
import os

TAGS_JSON = r"D:\Projekte\SynologyDrive\_standards\tags.json"
PART_FILTER = 4 + 8 + 16 + 32 + 4096
KNOWN_KEYS = ("process", "material", "thickness", "status", "id")


def load_tags():
    if not os.path.isfile(TAGS_JSON):
        rs.MessageBox("tags.json fehlt:\n" + TAGS_JSON, 16, "TagAudit")
        return None
    with open(TAGS_JSON, "r") as f:
        return json.load(f)


def tags_of(oid):
    keys = rs.GetUserText(oid)
    if not keys:
        return {}
    return dict((k, rs.GetUserText(oid, k)) for k in keys)


def main():
    cfg = load_tags()
    if cfg is None:
        return
    lists = cfg["keys"]
    t_required = cfg.get("thickness_required_for", [])

    strict = True
    ids = rs.SelectedObjects()
    if ids:
        ids = [i for i in ids if rs.ObjectType(i) & PART_FILTER]
    else:
        strict = False
        ids = [i for i in (rs.AllObjects() or [])
               if rs.ObjectType(i) & PART_FILTER]

    untagged, missing, invalid = [], [], []
    by_id = {}

    for oid in ids:
        t = tags_of(oid)
        if "process" not in t:
            if strict:
                untagged.append(oid)
            continue

        # fehlende Pflicht-Keys
        need = ["material"]
        if t.get("process") in t_required:
            need.append("thickness")
        if any(k not in t for k in need):
            missing.append(oid)

        # ungueltige Werte
        bad = False
        for k in ("process", "material", "finish", "status"):
            if k in t and t[k] not in lists.get(k, [t[k]]):
                bad = True
        if "thickness" in t:
            try:
                if float(t["thickness"]) <= 0:
                    bad = True
            except ValueError:
                bad = True
        if bad:
            invalid.append(oid)

        # doppelte IDs
        if t.get("id"):
            by_id.setdefault(t["id"], []).append(oid)

    dupes = [oid for objs in by_id.values() if len(objs) > 1 for oid in objs]
    dupe_ids = sorted(k for k, v in by_id.items() if len(v) > 1)

    problems = list(set(untagged + missing + invalid + dupes))

    print("TagAudit (%s, %d Teile geprueft):"
          % ("Auswahl streng" if strict else "ganzes Dokument", len(ids)))
    if strict and untagged:
        print("  %d Teil(e) OHNE Tags" % len(untagged))
    if missing:
        print("  %d Teil(e) mit fehlenden Pflicht-Keys" % len(missing))
    if invalid:
        print("  %d Teil(e) mit ungueltigen Werten (nicht in tags.json)" % len(invalid))
    if dupe_ids:
        print("  doppelte IDs: %s" % ", ".join(dupe_ids))

    sc.sticky["tagaudit_ok"] = not problems
    if problems:
        rs.UnselectAllObjects()
        rs.SelectObjects(problems)
        print("-> %d Problemteil(e) sind jetzt SELEKTIERT." % len(problems))
    else:
        print("  alles sauber.")

    if not rs.IsAlias("TagAudit"):
        rs.AddAlias("TagAudit", '! _-RunPythonScript "TagAudit"')
        print('Alias "TagAudit" angelegt.')


if __name__ == "__main__":
    main()
