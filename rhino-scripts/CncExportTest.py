# -*- coding: utf-8 -*-
"""Headless-Abnahme fuer Phase 1 (PLAN_FREIE_GEOMETRIE.md, Abschnitt 6).

Deckt ab: T14-Form (nur Makros, keine SP-Zeile), Reihenfolge-Regel,
Idempotenz (byteidentisch), Aequivalenz zu TeilExport (VARS + Bohrzeilen
textgleich - die Konturform ist bewusst verschieden: TeilExport faehrt die
maschinenbelegte SP-Bahn, CncExport das T13-immune _Format_V5; woertliche
Textgleichheit der ganzen Datei gibt es erst ab Phase 1b), alle Waechter,
und die geschlossene Luecke 0.4 (Kantenbohrer vs bohrkopf_horizontal_mm).
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import os
import KorpusBau as K
import TeilExport as TE
import CncExport as CE
import HolzHerLayerExport as HHL
import holzher_teil as HT
reload(K)
reload(TE)
reload(CE)

SP = (r"C:\Users\leona\AppData\Local\Temp\claude"
      r"\D--Projekte-SynologyDrive-53-post-processor"
      r"\8969a723-eac3-4320-ac39-25d7864936b0\scratchpad")
REPORT = SP + r"\cncexport_report.txt"
L, PRUEF = [], []


def log(s):
    L.append(s)


def check(name, gut, detail=""):
    PRUEF.append((name, bool(gut)))
    log("  [%s] %s%s" % ("ok" if gut else "XX", name,
                         "  %s" % detail if detail else ""))


def rect(x0, y0, b, h):
    return rs.AddPolyline([(x0, y0, 0), (x0 + b, y0, 0), (x0 + b, y0 + h, 0),
                           (x0, y0 + h, 0), (x0, y0, 0)])


def tagge(oid, kind, **kv):
    rs.SetUserText(oid, "kind", kind)
    for k, v in kv.items():
        rs.SetUserText(oid, k, v)
    return oid


def main():
    wzg = HHL.werkzeuge()
    ordner = os.path.join(SP, "cnc_out")

    # ---- A: T14-Form - Rechteck 600x400 + 6 Bohrungen, nur Makros --------
    log("A  T14-Form (Rechteck + 6 Bohrungen)")
    objs = []
    objs.append(tagge(rect(0, 0, 600, 400), "rohteil",
                      thickness="19", material="mpx-birch"))
    objs.append(tagge(rect(0, 0, 600, 400), "kontur", depth="durch"))
    for i in range(6):
        objs.append(tagge(rs.AddCircle((60 + i * 96, 200, 0), 2.5),
                          "bohrung", depth="13"))
    teil, warn = CE.teil_aus_objekten(objs, wzg, name="T14")
    ok, zeilen, info = CE.export_teil(teil, ordner, wzg, "1", 100)
    hop_a = os.path.join(ordner, "hop", "T14_mpx-birch_19mm.hop")
    check("A: Export ok, Datei traegt Material+Staerke",
          ok and os.path.isfile(hop_a), hop_a)
    ha = open(hop_a).read()
    check("A: _Format_V5 woertlich die Belegzeile",
          "CALL _Format_V5 ( VAL ECKENVERRUNDUNG:=0,TIEFE:=-0.2,AUFMASS:=0,"
          "ANF:=2.5,ESMD:=1,GGL:=2,LASER:=0,DEPTH:=0)" in ha)
    check("A: WZF 20 direkt vor _Format_V5",
          "WZF (20,_VE,_V,_VA,_SD,_ANF,'1')\nCALL _Format_V5"
          in ha.replace("\r\n", "\n"))
    check("A: 6 Bohrungen T-13, Feld 8 = 0",
          ha.count("Bohrung (") == 6
          and ha.count(",5,-13,10,0,0,0,0,0,0,0)") == 6,
          "%d" % ha.count("Bohrung ("))
    check("A: KEINE SP-Zeile (T13-immun)", "SP (" not in ha)
    check("A: VARS 600/400/19",
          "DX := 600;" in ha and "DY := 400;" in ha and "DZ := 19;" in ha)
    check("A: Kopf-Offsets 0, Fertigteil+Park da",
          all(x in ha for x in (";DX=0", "Fertigteil (DX,DY,DZ",
                                "CALL Park_V7")))
    check("A: Bericht nennt Format_V5 und Ziel",
          any("_Format_V5" in z for z in zeilen)
          and any("->" in z for z in zeilen))

    # ---- B: runder Ausschnitt vor der Aussenkontur -----------------------
    log("B  runder Ausschnitt (Reihenfolge)")
    objs.append(tagge(rs.AddCircle((300, 200, 0), 30), "ausschnitt",
                      depth="durch"))
    teil, warn = CE.teil_aus_objekten(objs, wzg, name="T14")
    ok, zeilen, info = CE.export_teil(teil, ordner, wzg, "1", 100)
    hb = open(hop_a).read()
    check("B: Kreistasche woertlich Belegform (ESXY 0, durch)",
          "CALL _Kreistasche_V5 ( VAL X_MITTE:=300,Y_MITTE:=200,RADIUS:=30,"
          "TIEFE:=-0.2,ZUSTELLUNG:=_AT_MAXDEPTH,AB:=0,ABF:=_ANF,INTERPOL:=1,"
          "UMKEHREN:=0,ESXY:=0,ESMD:=1,LASER:=0)" in hb)
    check("B: Ausschnitt VOR der Aussenkontur (Teil bleibt im Verbund)",
          0 < hb.find("_Kreistasche_V5") < hb.find("_Format_V5"))
    check("B: Bohrungen VOR dem Ausschnitt",
          0 < hb.find("Bohrung (") < hb.find("_Kreistasche_V5"))

    # ---- C: Idempotenz ---------------------------------------------------
    log("C  Idempotenz")
    dxf_a = os.path.join(ordner, "dxf", "T14_mpx-birch_19mm.dxf")
    d1 = open(dxf_a, "rb").read()
    teil, warn = CE.teil_aus_objekten(objs, wzg, name="T14")
    CE.export_teil(teil, ordner, wzg, "1", 100)
    h1 = open(hop_a, "rb").read()
    teil, warn = CE.teil_aus_objekten(objs, wzg, name="T14")
    CE.export_teil(teil, ordner, wzg, "1", 100)
    check("C: dritter Export weiter byteidentisch (.hop und .dxf)",
          open(hop_a, "rb").read() == h1
          and open(dxf_a, "rb").read() == d1)
    rs.DeleteObjects(objs)

    # ---- D: Aequivalenz zu TeilExport (Tuer links) -----------------------
    log("D  Aequivalenz TeilExport <-> CncExport (Bohrbild)")
    ebene = Rhino.Geometry.Plane.WorldXY
    w = {"H": 720.0, "d": 19.0, "bauart": "S", "n_tueren": 2, "anschlag": "A",
         "td": 19.0, "luft": 3.0, "rw_art": "N", "rw_d": 8.0, "tiefe_nf": 8.0,
         "rw_abstand": 10.0, "lochreihe": True, "loch_abstand": 37.0,
         "n_boeden": 2.0, "band_art": "Blum", "band_seite": "L",
         "topf_kante": 4.0, "griff": "Stange", "griff_abstand": 128.0,
         "griff_lage": "O", "material": "mpx-birch", "material_rw": "mdf",
         "verbinder": "Duebel"}
    teile, _ = K.bauen(ebene, 800.0, 600.0, w)
    n = dict((rs.ObjectName(t) or "?", t) for t in teile)
    basis_t = os.path.join(SP, "aequiv_tuer")
    TE.teil_export(basis_t, n["Tuer links"], "2", 20, 100)
    ht = open(basis_t + ".hop").read()
    rs.DeleteObjects(teile)

    def vars_von(text):
        return [z for z in text.splitlines() if ":= " in z]

    def bohr_von(text):
        return sorted(z for z in text.splitlines()
                      if z.startswith("Bohrung ("))

    dx = float([z for z in vars_von(ht) if "DX" in z][0].split(":= ")[1]
               .split(";")[0])
    dy = float([z for z in vars_von(ht) if "DY" in z][0].split(":= ")[1]
               .split(";")[0])
    frei = [tagge(rect(0, 0, dx, dy), "rohteil", thickness="19",
                  material="mpx-birch"),
            tagge(rect(0, 0, dx, dy), "kontur", depth="durch")]
    for z in bohr_von(ht):
        f = z[z.index("(") + 1:z.rindex(")")].split(",")
        x, y, d, t = float(f[0]), float(f[1]), float(f[2]), float(f[3])
        tiefe = "durch" if f[8] == "1" else "%g" % abs(t)
        frei.append(tagge(rs.AddCircle((x, y, 0), d / 2.0), "bohrung",
                          depth=tiefe))
    teil, warn = CE.teil_aus_objekten(frei, wzg, name="TuerFrei")
    ok, zeilen, info = CE.export_teil(teil, ordner, wzg, "2", 100)
    hc = open(os.path.join(ordner, "hop",
                           "TuerFrei_mpx-birch_19mm.hop")).read()
    check("D: VARS-Block textgleich", vars_von(ht) == vars_von(hc),
          "%s vs %s" % (vars_von(ht), vars_von(hc)))
    check("D: alle Bohrzeilen textgleich (inkl. Topf D35 = Typ 30)",
          bohr_von(ht) == bohr_von(hc) and len(bohr_von(hc)) == 8,
          "%d Zeilen" % len(bohr_von(hc)))
    check("D: derselbe WZB-Aufruf",
          "WZB (501,_VE,_V,_VA,_SD,_ANF,'51')" in hc)
    check("D: Konturform bewusst verschieden (SP dort, Format_V5 hier)",
          "SP (" in ht and "_Format_V5" in hc and "SP (" not in hc)
    rs.DeleteObjects(frei)

    # ---- E: Waechter -----------------------------------------------------
    log("E  Waechter")
    def erwarte_value_error(objs, teiltext, name):
        try:
            CE.teil_aus_objekten(objs, wzg, name="X")
            check(name, False, "kein Fehler")
        except ValueError as e:
            check(name, teiltext in str(e), str(e)[:70])

    def erwarte_pruef_fehler(objs, teiltext, name):
        try:
            teil, _ = CE.teil_aus_objekten(objs, wzg, name="X")
        except ValueError as e:
            check(name, False, "schon beim Sammeln: %s" % str(e)[:50])
            return
        fehler, _ = HT.pruefe(teil, wzg)
        check(name, any(teiltext in f for f in fehler),
              "; ".join(fehler)[:70] or "kein Fehler")

    o1 = rs.AddCircle((10, 10, 0), 2.5)
    erwarte_value_error([o1], "OHNE kind-Tag", "E: ohne kind -> Fehler")
    rs.DeleteObject(o1)

    basis = [tagge(rect(0, 0, 600, 400), "rohteil", thickness="19"),
             tagge(rect(0, 0, 600, 400), "kontur", depth="durch")]
    o2 = tagge(rs.AddCircle((100, 100, 0), 2.5), "bohrung")
    erwarte_value_error(basis + [o2], "OHNE depth-Tag",
                        "E: Bohrung ohne depth -> Fehler")
    rs.DeleteObject(o2)

    o3 = tagge(rs.AddCircle((100, 100, 0), 2.5), "bohrung", depth="25")
    erwarte_pruef_fehler(basis + [o3], "durchbohrt den Tisch",
                         "E: Tiefe 25 bei DZ 19 -> Fehler")
    rs.DeleteObject(o3)

    o4 = tagge(rs.AddCircle((100, 100, 0), 3.5), "bohrung", depth="13")
    erwarte_pruef_fehler(basis + [o4], "nicht am Bohrkopf",
                         "E: D7 -> Fehler (HOPS wuerde abbrechen)")
    rs.DeleteObject(o4)

    o5 = tagge(rs.AddCircle((650, 200, 0), 2.5), "bohrung", depth="13")
    erwarte_pruef_fehler(basis + [o5], "AUSSERHALB",
                         "E: Bohrung ausserhalb -> Fehler")
    rs.DeleteObject(o5)

    o6 = tagge(rs.AddCircle((1, 200, 0), 2.5), "bohrung", depth="13")
    erwarte_pruef_fehler(basis + [o6], "schneidet die Kontur",
                         "E: Bohrung schneidet Kontur -> Fehler")
    rs.DeleteObject(o6)

    o7 = tagge(rs.AddCircle((100, 100, 0), 5.0), "ausschnitt", depth="durch")
    erwarte_pruef_fehler(basis + [o7], "als Bohrung taggen",
                         "E: Ausschnitt D10 < Fraeser -> Fehler")
    rs.DeleteObject(o7)

    o8 = tagge(rs.AddLine((10, 10, 0), (100, 10, 0)), "nut", nut_b="8.5",
               nut_t="8")
    erwarte_value_error(basis + [o8], "Phase 2", "E: Nut -> Phase-2-Sperre")
    rs.DeleteObject(o8)
    rs.DeleteObjects(basis)

    dreieck = [tagge(rs.AddPolyline([(0, 0, 0), (500, 0, 0), (250, 300, 0),
                                     (0, 0, 0)]), "kontur", depth="durch"),
               tagge(rect(0, 0, 500, 300), "rohteil", thickness="19")]
    erwarte_pruef_fehler(dreieck, "T13",
                         "E: freie Kontur -> gesperrt bis T13")
    rs.DeleteObjects(dreieck)

    offen = [tagge(rs.AddPolyline([(0, 0, 0), (600, 0, 0), (600, 400, 0),
                                   (0, 400, 0), (0, 5, 0)]), "kontur",
                   depth="durch"),
             tagge(rect(0, 0, 600, 400), "rohteil", thickness="19")]
    erwarte_pruef_fehler(offen, "OFFEN", "E: offener Umriss -> Fehler")
    rs.DeleteObjects(offen)

    gross = [tagge(rect(0, 0, 3000, 500), "kontur", depth="durch"),
             tagge(rect(0, 0, 3000, 500), "rohteil", thickness="19")]
    erwarte_pruef_fehler(gross, "tisch_max_mm",
                         "E: 3000 mm -> tisch_max-Fehler")
    rs.DeleteObjects(gross)

    ohne_dz = [tagge(rect(0, 0, 600, 400), "kontur", depth="durch")]
    erwarte_value_error(ohne_dz, "thickness fehlt",
                        "E: ohne Staerke -> Fehler, nie raten")
    rs.DeleteObjects(ohne_dz)

    # Warnungen: Ausbruch + abweichendes Rohteil + Vorschau schreibt nichts
    wb = [tagge(rect(0, 0, 600, 400), "rohteil", thickness="19"),
          tagge(rect(0, 0, 600, 400), "kontur", depth="durch"),
          tagge(rs.AddCircle((4, 200, 0), 2.5), "bohrung", depth="13")]
    teil, warn = CE.teil_aus_objekten(wb, wzg, name="W")
    fehler, warnungen = HT.pruefe(teil, wzg)
    check("E: 4 mm von der Kante -> Ausbruch-WARNUNG, kein Fehler",
          not fehler and any("Ausbruch" in x for x in warnungen),
          "; ".join(warnungen)[:60])
    ok, zeilen, info = CE.export_teil(teil, ordner, wzg, "1", 100,
                                      vorschau=True)
    check("E: Vorschau schreibt nichts",
          ok and info is None
          and not os.path.isfile(os.path.join(ordner, "hop",
                                              "W_19mm.hop")))
    rs.DeleteObjects(wb)

    roh_ab = [tagge(rect(-10, -10, 620, 420), "rohteil", thickness="19"),
              tagge(rect(0, 0, 600, 400), "kontur", depth="durch")]
    teil, warn = CE.teil_aus_objekten(roh_ab, wzg, name="R")
    check("E: Rohteil != Fertigmass -> Aufmass-Warnung, DX/DY = Fertigmass",
          any("Fertigmass" in x for x in warn)
          and teil.dx == 600 and teil.dy == 400,
          "; ".join(warn)[:60])
    rs.DeleteObjects(roh_ab)

    # ---- F: Luecke 0.4 - Kantenbohrer gegen bohrkopf_horizontal_mm -------
    log("F  Kantenbohrer-Pruefung (Luecke 0.4)")
    f_rect = rect(0, 0, 300, 200)
    f_d5 = rs.AddCircle((150, -9.5, 0), 2.5)
    rs.SetUserText(f_d5, "depth", "18")
    f_d8 = rs.AddCircle((100, -9.5, 0), 4.0)
    rs.SetUserText(f_d8, "depth", "18")
    info = HHL.export(os.path.join(SP, "luecke04.dxf"),
                      [f_rect, f_d5, f_d8], "2", 20, 100, 19.2, 19.0)
    check("F: D5 in der Kante faellt jetzt auf, D8 nicht",
          info["fremde_d"] == [5.0], str(info["fremde_d"]))
    f_v5 = rs.AddCircle((150, 100, 0), 2.5)
    rs.SetUserText(f_v5, "depth", "13")
    info = HHL.export(os.path.join(SP, "luecke04b.dxf"),
                      [f_rect, f_v5], "2", 20, 100, 19.2, 19.0)
    check("F: D5 in der FLAECHE bleibt erlaubt", info["fremde_d"] == [],
          str(info["fremde_d"]))
    rs.DeleteObjects([f_rect, f_d5, f_d8, f_v5])

    # ---- G: Kern-Einheiten ----------------------------------------------
    log("G  holzher_teil-Einheiten")
    check("G: dateiname wertbasiert",
          HT.dateiname("Teil", "mpx-birch", "19") == "Teil_mpx-birch_19mm"
          and HT.dateiname("A/B:c", None, None) == "A_B_c")
    import holzher_hop
    hw = holzher_hop.HopWriter("t", 100, 100, 19)
    try:
        hw.kreistasche(50, 50, 10, 19, 20)
        check("G: Kreistasche Tiefe=DZ -> Fehler", False, "kein Fehler")
    except ValueError as e:
        check("G: Kreistasche Tiefe=DZ -> Fehler (trennt)",
              "trennt" in str(e), str(e)[:50])
    hw2 = holzher_hop.HopWriter("t", 100, 100, 19)
    hw2.kreistasche(50, 50, 10, 12, 20)
    check("G: Sack-Kreistasche ESMD 0 / AB 2 (Bezug Oberkante)",
          "TIEFE:=-12" in hw2.zeilen[-1] and "ESMD:=0" in hw2.zeilen[-1]
          and "AB:=2" in hw2.zeilen[-1])
    check("G: BAHN_ABSOLUT existiert und ist True (T13-Schalter)",
          holzher_hop.BAHN_ABSOLUT is True)

    n_ok = sum(1 for _, g in PRUEF if g)
    log("")
    log("%d/%d Pruefungen ok -> %s"
        % (n_ok, len(PRUEF), "PASS" if n_ok == len(PRUEF) else "FAIL"))
    txt = "\n".join(L)
    print(txt)
    f = open(REPORT, "w")
    f.write(txt)
    f.close()


if __name__ == "__main__":
    try:
        main()
    except Exception:
        import traceback
        f = open(REPORT, "w")
        f.write("EXCEPTION: " + traceback.format_exc())
        f.close()
