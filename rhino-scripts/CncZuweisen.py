# -*- coding: utf-8 -*-
"""CncZuweisen - freier Geometrie sagen, was sie an der Maschine IST.

Objekte waehlen, EINMAL Art + Werte setzen - die Zuweisung gilt fuer die
ganze Auswahl (40 Loecher = ein Durchgang; der Durchmesser kommt je Kreis
aus der Geometrie, nie aus der Option). Geschrieben werden UserText-Tags
(kind/depth/nut_b/nut_t/thickness/material/part) - dieselben Schluessel,
die KorpusBau vergibt und die CncExport/TeilExport lesen. Tags ueberleben
Speichern, Kopieren und Verschieben; erneut zuweisen ueberschreibt.

Bedienfluss nach PLAN_FREIE_GEOMETRIE.md Abschnitt 2, Optionsmuster wie
KorpusBau.parameter() (GetOption + sticky).

Erster Aufruf: _-RunPythonScript "CncZuweisen"
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import holzher_teil
reload(holzher_teil)

STICKY = "cnczuweisen"
ARTEN = ["Rohteil", "Kontur", "Ausschnitt", "Bohrung", "Kantenbohrung",
         "Nut", "Tasche", "Gravur"]
DEFAULTS = {"art": 3, "tiefe": 13.0, "durch": False, "staerke": 19.0,
            "material": "mpx-birch", "breite": 8.5, "nut_t": 8.0,
            "hoehe": 9.5, "kb_tiefe": 18.0, "gravur_t": 1.0, "part": ""}


def dialog(vorbelegt_durch=None):
    """Art + Werte abfragen -> dict oder None (Esc)."""
    from Rhino.Input.Custom import GetOption, OptionDouble, OptionToggle
    st = dict(DEFAULTS)
    st.update(sc.sticky.get(STICKY) or {})
    i_art = int(st["art"])
    o_tiefe = OptionDouble(float(st["tiefe"]), 0.1, 100.0)
    o_durch = OptionToggle(bool(st["durch"]), "Nein", "Ja")
    o_staerke = OptionDouble(float(st["staerke"]), 1.0, 100.0)
    o_breite = OptionDouble(float(st["breite"]), 1.0, 100.0)
    o_nut_t = OptionDouble(float(st["nut_t"]), 0.1, 100.0)
    o_hoehe = OptionDouble(float(st["hoehe"]), 0.5, 100.0)
    o_kb_tiefe = OptionDouble(float(st["kb_tiefe"]), 1.0, 100.0)
    o_gravur = OptionDouble(float(st["gravur_t"]), 0.1, 20.0)
    material = st["material"]
    part = st["part"]

    while True:
        go = GetOption()
        art = ARTEN[i_art]
        go.SetCommandPrompt("CncZuweisen: Art=%s - Optionen anklicken, "
                            "Enter = zuweisen" % art)
        go.AcceptNothing(True)
        ix_art = go.AddOptionList("Art", ARTEN, i_art)
        ix_mat = ix_teil = -1
        if art == "Rohteil":
            go.AddOptionDouble("Staerke", o_staerke)
            ix_mat = go.AddOption("Material")
        elif art in ("Kontur", "Ausschnitt", "Tasche"):
            go.AddOptionToggle("Durch", o_durch)
            go.AddOptionDouble("Tiefe", o_tiefe)
        elif art == "Bohrung":
            go.AddOptionToggle("Durch", o_durch)
            go.AddOptionDouble("Tiefe", o_tiefe)
        elif art == "Kantenbohrung":
            go.AddOptionDouble("Tiefe", o_kb_tiefe)
            go.AddOptionDouble("Hoehe", o_hoehe)
        elif art == "Nut":
            go.AddOptionDouble("Breite", o_breite)
            go.AddOptionDouble("Tiefe", o_nut_t)
        elif art == "Gravur":
            go.AddOptionDouble("Tiefe", o_gravur)
        ix_teil = go.AddOption("Teil")

        res = go.Get()
        if res == Rhino.Input.GetResult.Nothing:
            go.Dispose()
            break
        if res != Rhino.Input.GetResult.Option:
            go.Dispose()
            return None
        opt = go.Option()
        if opt.Index == ix_art:
            i_art = opt.CurrentListOptionIndex
        elif opt.Index == ix_mat:
            m = rs.GetString("Material", material)
            if m is not None:
                material = m.strip()
        elif opt.Index == ix_teil:
            p = rs.GetString("Teilname (leer = ein Teil je Auswahl)", part)
            if p is not None:
                part = p.strip()
        go.Dispose()

    st = {"art": i_art, "tiefe": o_tiefe.CurrentValue,
          "durch": o_durch.CurrentValue, "staerke": o_staerke.CurrentValue,
          "material": material, "breite": o_breite.CurrentValue,
          "nut_t": o_nut_t.CurrentValue, "hoehe": o_hoehe.CurrentValue,
          "kb_tiefe": o_kb_tiefe.CurrentValue,
          "gravur_t": o_gravur.CurrentValue, "part": part}
    sc.sticky[STICKY] = dict(st)
    return st


def _namen(ids):
    return ", ".join(rs.ObjectName(o) or str(o)[:8] for o in ids)


def anwenden(objs, st):
    """Tags schreiben. -> (anzahl, fehlertext_oder_None)"""
    art = ARTEN[int(st["art"])].lower()
    tol = sc.doc.ModelAbsoluteTolerance

    if art in ("bohrung", "kantenbohrung", "tasche"):
        # Kreispflicht LAUT sagen statt still zu ignorieren - eine
        # Nicht-Kreis-Kurve als "Bohrung" waere der teure stille Fehler.
        keine_kreise = []
        for o in objs:
            crv = rs.coercecurve(o)
            ok = False
            if crv is not None:
                ok = crv.TryGetCircle(tol)[0]
            if not ok:
                keine_kreise.append(o)
        if keine_kreise:
            return 0, ("%s braucht KREISE - keine Kreise: %s"
                       % (art, _namen(keine_kreise)))
    if art == "rohteil":
        for o in objs:
            crv = rs.coercecurve(o)
            if crv is None or holzher_teil._ist_rechteck(crv, tol) is None:
                return 0, ("Rohteil muss ein geschlossenes, achsparalleles "
                           "RECHTECK sein: %s" % _namen([o]))

    for o in objs:
        rs.SetUserText(o, "kind", art)
        if st["part"]:
            rs.SetUserText(o, "part", st["part"])
        if art == "rohteil":
            rs.SetUserText(o, "thickness", "%g" % st["staerke"])
            if st["material"]:
                rs.SetUserText(o, "material", st["material"])
        elif art in ("kontur", "ausschnitt", "tasche", "bohrung"):
            rs.SetUserText(o, "depth",
                           "durch" if st["durch"] else "%g" % st["tiefe"])
        elif art == "kantenbohrung":
            rs.SetUserText(o, "depth", "%g" % st["kb_tiefe"])
            rs.SetUserText(o, "hoehe", "%g" % st["hoehe"])
        elif art == "nut":
            rs.SetUserText(o, "nut_b", "%g" % st["breite"])
            rs.SetUserText(o, "nut_t", "%g" % st["nut_t"])
        elif art == "gravur":
            rs.SetUserText(o, "depth", "%g" % st["gravur_t"])
    return len(objs), None


def main():
    objs = rs.GetObjects("Objekte fuer die CNC-Zuweisung waehlen (Kurven; "
                         "eine Zuweisung gilt fuer die GANZE Auswahl)",
                         rs.filter.curve, preselect=True, select=False)
    if not objs:
        return
    st = dialog()
    if st is None:
        return
    n, fehler = anwenden(objs, st)
    if fehler:
        rs.MessageBox(fehler, 48, "CncZuweisen")
        return
    art = ARTEN[int(st["art"])]
    print("CncZuweisen: %d Objekt(e) als %s getaggt. Kontrolle: CncSchau, "
          "Export: CncExport." % (n, art))

    if not rs.IsAlias("CncZuweisen"):
        rs.AddAlias("CncZuweisen", '! _-RunPythonScript "CncZuweisen"')
        print('Alias "CncZuweisen" angelegt.')


if __name__ == "__main__":
    main()
