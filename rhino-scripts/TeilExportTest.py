# -*- coding: utf-8 -*-
"""Kettentest: KorpusBau baut -> TeilExport legt flach und schreibt DXF."""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import KorpusBau as K
import TeilExport as TE
reload(K)
reload(TE)

SP = r"C:\Users\leona\AppData\Local\Temp\claude\D--Projekte-SynologyDrive\29a00841-9fb2-40c1-a411-d4c61d10076d\scratchpad"
REPORT = SP + r"\teilexport_report.txt"
L, PRUEF = [], []


def log(s):
    L.append(s)


def check(name, gut, detail=""):
    PRUEF.append((name, bool(gut)))
    log("  [%s] %s%s" % ("ok" if gut else "XX", name,
                         "  %s" % detail if detail else ""))


def parse(path):
    lines = [l.strip() for l in open(path).read().splitlines()]
    i, cur, rec, ents = 0, None, {}, []
    while i < len(lines) - 1:
        c, v = lines[i], lines[i + 1]
        if c == '0':
            if cur in ('POLYLINE', 'CIRCLE'):
                ents.append((cur, rec))
            cur, rec = v, {}
        else:
            rec.setdefault(c, v)
        i += 2
    return ents


def main():
    ebene = Rhino.Geometry.Plane.WorldXY
    w = {"H": 720.0, "d": 19.0, "bauart": "S", "n_tueren": 2, "anschlag": "A",
         "td": 19.0, "luft": 3.0, "rw_art": "N", "rw_d": 8.0, "tiefe_nf": 8.0,
         "rw_abstand": 10.0, "lochreihe": True, "loch_abstand": 37.0,
         "n_boeden": 2.0, "band_art": "Blum", "band_seite": "L",
         "topf_kante": 4.0, "griff": "Stange", "griff_abstand": 128.0,
         "griff_lage": "O", "material": "mpx-birch", "material_rw": "mdf",
         "verbinder": "Duebel"}
    teile, hinw = K.bauen(ebene, 800.0, 600.0, w)
    n = dict((rs.ObjectName(t) or "?", t) for t in teile)
    log("Korpus gebaut: %d Objekte" % len(teile))

    # ---- Seite links: alles senkrecht, keine Kantenbohrungen -------------
    log("S  Seite links")
    basis_s = SP + r"\teil_seite"
    dxf = basis_s + ".dxf"
    info = TE.teil_export(basis_s, n["Seite links"], "2", 20, 100)
    check("S: Kontur 720 x 600 (Querformat: H x T)",
          abs(info["breite"] - 720.0) < 0.5 and abs(info["hoehe"] - 600.0) < 0.5,
          "%.1f x %.1f" % (info["breite"], info["hoehe"]))
    # Lochreihe 42 + Duebel-Flaeche 8 (T=600 -> 4 Positionen je Fuge;
    # Montageplatte: a=37 -> Loecher existieren schon)
    check("S: 50 Bohrungen, alle senkrecht",
          info["bohr_auto"] == 50 and info["kanten"] == 0,
          "%d / %d Kanten" % (info["bohr_auto"], info["kanten"]))
    check("S: Dicke 19 aus dem Tag", abs(info["dicke"] - 19.0) < 0.01)
    ents = parse(dxf)
    kreise = [r for t, r in ents if t == "CIRCLE" and r.get("8") == "TT$501$"]
    check("S: 50 Kreise auf TT$501$ in der DXF", len(kreise) == 50,
          "%d" % len(kreise))

    # ---- Boden: 6 Duebel-Stirnbohrungen als KANTENbohrungen --------------
    log("B  Boden")
    basis_b = SP + r"\teil_boden"
    dxf2 = basis_b + ".dxf"
    info = TE.teil_export(basis_b, n["Boden"], "2", 20, 100)
    check("B: Kontur 762 x 600 (B-2d x T)",
          abs(info["breite"] - 762.0) < 0.5 and abs(info["hoehe"] - 600.0) < 0.5,
          "%.1f x %.1f" % (info["breite"], info["hoehe"]))
    check("B: 8 Bohrungen, alle Kanten (4 je Fuge bei T=600)",
          info["bohr_auto"] == 8 and info["kanten"] == 8,
          "%d / %d Kanten" % (info["bohr_auto"], info["kanten"]))
    ents = parse(dxf2)
    kreise = [r for t, r in ents if t == "CIRCLE" and r.get("8") == "TT$501$"]
    bmax, hmax = info["breite"], info["hoehe"]
    ausserhalb = 0
    for r in kreise:
        x, y = float(r.get("10", 0)), float(r.get("20", 0))
        if x < -0.001 or x > bmax + 0.001 or y < -0.001 or y > hmax + 0.001:
            ausserhalb += 1
    check("B: alle 8 Kreise AUSSERHALB der Kontur", len(kreise) == 8
          and ausserhalb == 8, "%d/%d" % (ausserhalb, len(kreise)))
    # Ueberstand = Bohrachsenhoehe = d/2 = 9.5
    ueber = set()
    for r in kreise:
        x, y = float(r.get("10", 0)), float(r.get("20", 0))
        d0 = max(-x, x - bmax, -y, y - hmax)
        ueber.add(round(d0, 1))
    check("B: Ueberstand 9.5 mm (= d/2)", ueber == set([9.5]), str(ueber))
    tiefen = set(r.get("39") for r in kreise)
    check("B: Kantenbohr-Tiefe 18 (Duebel-Hirn)",
          tiefen == set(["-18.000000"]), str(tiefen))

    # ---- Tuer links: Toepfe + Schrauben + Griffe -------------------------
    log("T  Tuer links")
    basis_t = SP + r"\teil_tuer"
    dxf3 = basis_t + ".dxf"
    info = TE.teil_export(basis_t, n["Tuer links"], "2", 20, 100)
    check("T: 2 Toepfe + 4 Schrauben + 2 Griffe = 8 Bohrungen",
          info["bohr_auto"] == 8 and info["kanten"] == 0,
          "%d / %d" % (info["bohr_auto"], info["kanten"]))
    check("T: keine fremden Durchmesser", info["fremde_d"] == [],
          str(info["fremde_d"]))

    # ---- H: die .hop-Dateien selbst --------------------------------------
    log("H  .hop-Inhalte")
    hs = open(basis_s + ".hop").read()
    hb = open(basis_b + ".hop").read()
    ht = open(basis_t + ".hop").read()
    check("H: Seite - SP mit z=-0.2 (Bezug Tisch), Anfahrt linear/links",
          "SP (" in hs and ",-0.2,2,1,_ANF,0,0,0,0,1,1," in hs)
    check("H: Seite - 42x T-13 (Lochreihe) + 8x T-12 (Duebel-Flaeche)",
          hs.count("Bohrung (") == 50
          and hs.count(",-13,10,0,0,0,0,0,0,0)") == 42
          and hs.count(",-12,10,0,0,0,0,0,0,0)") == 8,
          "%d / %d / %d" % (hs.count("Bohrung ("),
                            hs.count(",-13,10,0,0,0,0,0,0,0)"),
                            hs.count(",-12,10,0,0,0,0,0,0,0)")))
    # Nut 8.5 breit mit 4er Fraeser -> 3 Bahnen (Flanken +-2.25, dann Mitte),
    # SP-Z = 19 - 8 = 11 (Tischbezug), ohne Anfahrt und ohne Radiuskorrektur
    def sp_felder(text):
        return [z[z.index("(") + 1:z.rindex(")")].split(",")
                for z in text.splitlines() if z.startswith("SP (")]
    nut_sp = [f for f in sp_felder(hs) if f[2] == "-8"]
    check("H: Seite - Nut als 3 Fraeserbahnen auf Z -8, kein Makro",
          len(nut_sp) == 3 and "_nuten_frei" not in hs,
          "%d Bahnen" % len(nut_sp))
    check("H: Nut-Bahnen ohne Anfahrt und ohne Radiuskorrektur",
          all(f[10] == "0" and f[11] == "0" for f in nut_sp))
    ys = [float(f[1]) for f in nut_sp]
    check("H: Nut - erst beide Flanken (+-2.25), dann die Mitte",
          len(ys) == 3 and abs(ys[0] - (ys[2] - 2.25)) < 0.01
          and abs(ys[1] - (ys[2] + 2.25)) < 0.01,
          "%s" % ys)
    check("H: Nut ruft WZF 8 (4mm Nuter lt. Werkstatt)", "WZF (8," in hs)
    check("H: Seite - VARS 720/600/19",
          "DX := 720;" in hs and "DY := 600;" in hs and "DZ := 19;" in hs)
    check("H: Seite - EP wiederholt Schrittzahl", "EP (1,_ANF,0)" in hs)
    check("H: Boden - 8 HorzB, T-18", hb.count("HorzB (") == 8
          and hb.count(",-18,0,0,") == 8, "%d" % hb.count("HorzB ("))
    check("H: Boden - Nut da, keine Vertikalbohrung",
          len([f for f in sp_felder(hb) if f[2] == "-8"]) == 3
          and "Bohrung (" not in hb)
    check("H: Tuer - Toepfe D35 als TYP 3 (Feld5=30), Griffe durch",
          ht.count(",35,-13,30,") == 2 and ",35,-13,10," not in ht
          and ht.count(",5,-0.2,10,0,0,0,1,") == 2)
    check("H: Tuer - keine Nut, keine HorzB",
          len(sp_felder(ht)) == 1 and "HorzB" not in ht)
    check("H: Kopf-Offsets 0, Fertigteil+Park da",
          all(x in hs for x in (";DX=0", "Fertigteil (DX,DY,DZ",
                                "CALL Park_V7")))

    # ---- W: Audit-Fixes ---------------------------------------------------
    log("W  Audit-Fixes (Werkzeugform, Feld 8, WZB vor HorzB, Richtung)")
    check("W: WZF 7-Feld-Form ,'1'", ",_SD,_ANF,'1')" in hs)
    check("W: WZB 7-Feld-Form ,'51'", ",_SD,_ANF,'51')" in hs)
    check("W: kein Feld-8-Positionscode 2 mehr",
          ",10,0,0,2," not in hs and ",10,0,0,2," not in ht)
    ib_wzb = hb.find("WZB (")
    ib_horz = hb.find("HorzB (")
    check("W: WZB steht VOR dem ersten HorzB",
          0 <= ib_wzb < ib_horz, "%d/%d" % (ib_wzb, ib_horz))
    check("W: Nut-Bahnen nicht negativ (geklemmt)",
          all(float(f[0]) >= 0 and float(f[1]) >= 0 for f in sp_felder(hb)))
    check("W: Aussenkontur mit Radiuskorrektur LINKS (Feld 12 = 1)",
          [f[11] for f in sp_felder(hs) if f[2] == "-0.2"] == ["1"],
          "%s" % [f[11] for f in sp_felder(hs) if f[2] == "-0.2"])

    def shoelace_letzter_block(text):
        # SP+G01-Punkte des LETZTEN Konturblocks (= Aussenkontur)
        import re
        bloecke = text.split("SP (")
        letzt = bloecke[-1]
        pts = [tuple(float(v) for v in letzt.split(",")[:2])]
        for m in re.finditer(r"G01 \(([-0-9.]+),([-0-9.]+),", letzt):
            pts.append((float(m.group(1)), float(m.group(2))))
        fl = 0.0
        for (x1, y1), (x2, y2) in zip(pts, pts[1:] + [pts[0]]):
            fl += x1 * y2 - x2 * y1
        return fl
    check("W: Umriss ueberall im UHRZEIGERSINN (wie HOPS-Import)",
          shoelace_letzter_block(hs) < 0 and shoelace_letzter_block(hb) < 0
          and shoelace_letzter_block(ht) < 0,
          "%.0f/%.0f/%.0f" % (shoelace_letzter_block(hs),
                              shoelace_letzter_block(hb),
                              shoelace_letzter_block(ht)))

    # Bogen-Unit gegen die Referenzzeile aus Muster_DXF_Import.hop
    import holzher_hop
    reload(holzher_hop)
    hw = holzher_hop.HopWriter("t", 800, 400, 19)
    hw.kontur([(300, 130, 1.0), (500, 130, 0.0)], False, 20)
    check("W: Halbkreis-Bulge -> exakt die Referenz-G03M",
          "G03M (500,130,0,400,130,0,0,2,0)" in hw.zeilen,
          str([z for z in hw.zeilen if "G0" in z]))
    hw2 = holzher_hop.HopWriter("t", 800, 400, 19)
    hw2.kontur([(300, 130, -1.0), (500, 130, 0.0)], False, 20)
    check("W: negativer Bulge -> G02M, gleiches Zentrum",
          "G02M (500,130,0,400,130,0,0,2,0)" in hw2.zeilen)

    # Vollkreis -> zwei Halbboegen statt stillem Kollaps
    kreis_id = rs.AddCircle((50, 50, 0), 30)
    verts, _ = TE.contour_vertices(rs.coercecurve(kreis_id))
    check("W: Vollkreis -> 2 Vertices mit |Bulge|=1",
          len(verts) == 2 and abs(abs(verts[0][2]) - 1.0) < 0.001,
          str(verts))
    rs.DeleteObject(kreis_id)

    # ---- R: Rueckwand (merkmalloses 8-mm-Teil) ---------------------------
    log("R  Rueckwand")
    basis_r = SP + r"\teil_rueckwand"
    info = TE.teil_export(basis_r, n["Rueckwand"], "2", 20, 100)
    hr = open(basis_r + ".hop").read()
    check("R: 0 Bohrungen, 0 Nuten, Dicke 8",
          info["bohr_auto"] == 0 and info["nuten"] == 0
          and abs(info["dicke"] - 8.0) < 0.01)
    check("R: hop hat Kontur, aber kein WZB/Bohrung/Nut",
          "SP (" in hr and "WZB" not in hr and "_nuten_frei" not in hr
          and "DZ := 8;" in hr)

    # ---- X: fehlender depth-Tag muss knallen -----------------------------
    log("X  Bohrkreis ohne depth-Tag")
    streuner = rs.AddCircle((100, 37, 300), 2.5)
    rs.ObjectLayer(streuner, rs.ObjectLayer(n["Seite rechts"]).split("::")[0]
                   + "::Bohrungen")
    rs.SetUserText(streuner, "part", "Seite rechts")
    try:
        TE.teil_export(SP + r"\teil_sr", n["Seite rechts"], "2", 20, 100)
        check("X: ValueError kam", False, "kein Fehler")
    except ValueError as e:
        check("X: ValueError kam (OHNE-Tag-Waechter)",
              "OHNE depth-Tag" in str(e), str(e)[:60])
    rs.DeleteObject(streuner)

    # ---- N: Dateinamen tragen Material + Staerke -------------------------
    log("N  Dateinamen")
    dn_s = TE.dateiname(n["Seite links"], "Seite links")
    check("N: Teil + Material + Staerke im Namen",
          dn_s == "Seite_links_mpx-birch_19mm", dn_s)
    dn_r = TE.dateiname(n["Rueckwand"], "Rueckwand")
    check("N: Rueckwand mit eigenem Material und 8 mm",
          dn_r == "Rueckwand_mdf_8mm", dn_r)
    check("N: sauber() entfernt Pfadtrenner",
          TE.sauber("Korpus 01/Test:x") == "Korpus_01_Test_x",
          TE.sauber("Korpus 01/Test:x"))

    rs.DeleteObjects(teile)

    # ---- Y: bauart B - Rollentausch, HorzB-Winkel 0/180 ------------------
    log("Y  bauart BodenDeckelDurchgehend")
    w2 = dict(w)
    w2["bauart"] = "B"
    w2["band_art"] = "Keine"
    w2["griff"] = "Kein"
    teile2, _ = K.bauen(Rhino.Geometry.Plane.WorldXY, 800.0, 600.0, w2)
    n2 = dict((rs.ObjectName(t) or "?", t) for t in teile2)
    basis_y = SP + r"\teil_b_seite"
    info = TE.teil_export(basis_y, n2["Seite links"], "2", 20, 100)
    hy = open(basis_y + ".hop").read()
    check("Y: Seite hat jetzt KANTENbohrungen (Stirn)",
          info["kanten"] > 0 and info["bohr_auto"] > info["kanten"],
          "%d Kanten / %d gesamt" % (info["kanten"], info["bohr_auto"]))
    check("Y: HorzB mit Winkel 0 und 180 (kurze Kanten)",
          ",0,0,2,0,0)" in hy.replace(",180,0,2,0,0)", "")
          and ",180,0,2,0,0)" in hy)
    basis_y2 = SP + r"\teil_b_boden"
    info = TE.teil_export(basis_y2, n2["Boden"], "2", 20, 100)
    hy2 = open(basis_y2 + ".hop").read()
    check("Y: Boden hat FLAECHENbohrungen T-12",
          info["kanten"] == 0 and hy2.count(",-12,10,") > 0,
          "%d" % hy2.count(",-12,10,"))
    rs.DeleteObjects(teile2)

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
