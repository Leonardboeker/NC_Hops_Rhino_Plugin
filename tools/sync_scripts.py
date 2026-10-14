# -*- coding: utf-8 -*-
r"""Haelt rhino-scripts/ und die Rhino-Scripts im APPDATA im Gleichstand.

    python tools/sync_scripts.py            # nur berichten
    python tools/sync_scripts.py --push     # Master -> Snapshot kopieren

Der Master liegt in %APPDATA%\McNeel\Rhinoceros\8.0\scripts (nur von dort
laedt Rhino), rhino-scripts/ ist die versionierte Kopie fuers Projekt.

Der Ausloeser: eine zweite Session hat am 2026-08-29 direkt im SNAPSHOT
editiert (Park MODE:=2), eine dritte hat kurz darauf den Master
darueberkopiert -- die Aenderung war weg. --push verweigert deshalb jede
Datei, deren Snapshot NEUER ist als der Master: dort steckt fremde Arbeit,
die erst in den Master gehoert.

Rueckgabe 0 = im Gleichstand oder sauber kopiert, 1 = Handarbeit noetig.
"""
import filecmp
import os
import shutil
import sys

MASTER = os.path.join(os.environ.get("APPDATA", ""), "McNeel", "Rhinoceros",
                      "8.0", "scripts")
SNAPSHOT = os.path.join(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))), "rhino-scripts")


def main(push):
    if not os.path.isdir(MASTER):
        print("Master nicht gefunden: %s" % MASTER)
        return 1
    gleich, master_neuer, snapshot_neuer, neu = [], [], [], []
    # Beide Seiten listen: eine NEUE Master-Datei existiert im Snapshot
    # noch nicht und wuerde sonst nie synchronisiert (Luecke bis 2026-09-06;
    # nur .py - werkzeuge.json u. ae. haben ihren Master in _standards).
    namen = set(os.listdir(SNAPSHOT))
    namen.update(n for n in os.listdir(MASTER) if n.endswith(".py"))
    for name in sorted(namen):
        s = os.path.join(SNAPSHOT, name)
        m = os.path.join(MASTER, name)
        if not os.path.isfile(m):
            continue
        if not os.path.isfile(s):
            neu.append(name)
        elif filecmp.cmp(m, s, shallow=False):
            gleich.append(name)
        elif os.path.getmtime(s) > os.path.getmtime(m):
            snapshot_neuer.append(name)
        else:
            master_neuer.append(name)

    print("%d Datei(en) identisch" % len(gleich))
    for name in snapshot_neuer:
        print("  STOP     %s -- Snapshot ist NEUER als der Master. Dort hat "
              "jemand direkt editiert; erst in den Master uebernehmen." % name)
    for name in master_neuer:
        print("  %s %s" % ("kopiere " if push else "offen   ", name))
        if push:
            shutil.copy2(os.path.join(MASTER, name),
                         os.path.join(SNAPSHOT, name))
    for name in neu:
        # Nicht jede Master-Datei gehoert ins Projekt (Scratch-Tests) -
        # neue Dateien kommen deshalb nur mit --neu mit.
        mit = push and "--neu" in sys.argv
        print("  %s %s -- NUR im Master%s"
              % ("kopiere " if mit else "NEU     ", name,
                 "" if mit else " (mit --push --neu uebernehmen)"))
        if mit:
            shutil.copy2(os.path.join(MASTER, name),
                         os.path.join(SNAPSHOT, name))
    if snapshot_neuer:
        return 1
    if master_neuer and not push:
        print("\n--push kopiert die offenen Dateien Master -> Snapshot.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main("--push" in sys.argv))
