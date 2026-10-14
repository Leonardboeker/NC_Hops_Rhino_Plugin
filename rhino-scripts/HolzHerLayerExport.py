# -*- coding: utf-8 -*-
"""HolzHerLayerExport - DXF fuer den HOPS-Layer-Import schreiben.

Im Gegensatz zu HolzHerExport (nackte Kontur fuer aCADemy) traegt diese DXF
die Bearbeitungsparameter schon in sich: Werkzeug, Seite und Vorschub stecken
im Layernamen, die Tiefe in der Objekthoehe (DXF-Code 39), und je Kontur gibt
es genau EINEN gelben Startpunkt - damit genau EINE Anfahrt.

Ablauf: Konturen waehlen -> Werkzeug/Seite/Vorschub/Tiefe -> optional
Bohrkreise waehlen -> Plattenstaerke -> speichern.

Konvention entschluesselt in:
  53_post-processor\\reference-hops\\dxf-layer-import\\KONVENTION.md

Erster Aufruf: _-RunPythonScript "HolzHerLayerExport"
"""
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import os
import holzher_kontur
import holzher_dxf
reload(holzher_kontur)                   # Rhino cached Module je Sitzung
reload(holzher_dxf)
from holzher_kontur import contour_vertices, bulge_of, GAP_TOL, MIN_SEG
from holzher_dxf import DxfR12, ROT, GELB, BLAU, WEISS

STARTPUNKT_R = 10.0                      # Ø20 wie in der HolzHer-Doku
WERKZEUGE_JSON = r"D:\Projekte\SynologyDrive\_standards\werkzeuge.json"

SEITEN = [("Links der Kontur", "1"), ("Rechts der Kontur", "2"),
          ("Mitte der Kontur", "0")]


def werkzeuge():
    """Maschinendaten aus werkzeuge.json (aus der Werkzeugdatei .too abgeleitet)."""
    try:
        import json
        f = open(WERKZEUGE_JSON)
        try:
            return json.load(f)
        finally:
            f.close()
    except Exception:
        return {"fraeser_kontur": 20, "bohrer_typ": 501,
                "bohrkopf_mm": [3, 5, 6, 8, 10, 15, 35]}


def tool_layer(seite, vorschub_prozent, werkzeug_id):
    """z. B. Seite 2 + 100% -> '20_TT$20$'; Mitte -> '00_TT$20$'.

    Das Seiten-/Vorschub-Praefix ist IMMER zweistellig. Die HolzHer-Doku 1.1-1.3
    kennt fuer Mitte nur '00_' bzw. '07_', nie '0_' oder '7_' - und einstellige
    Praefixe bedeuten im Muster etwas anderes (dort nur bei Taschen/Nuten:
    '4_TT$20$' -> Kreistasche, '3_TT$220$' -> Nut).
    """
    ziffer = int(round(vorschub_prozent / 10.0)) % 10     # 100%->0, 70%->7
    return "%s%d_TT$%d$" % (seite, ziffer, werkzeug_id)


# contour_vertices() und bulge_of() liegen in holzher_kontur.py - dieselbe
# Aufbereitung nutzt auch HolzHerExport (aCADemy-Weg).


def main():
    curves = rs.GetObjects("Fraeskonturen waehlen (getaggte Bohrkreise werden "
                           "automatisch als Bohrungen uebernommen)",
                           rs.filter.curve, preselect=True, select=False)
    if not curves:
        return

    st = sc.sticky
    idx = rs.ListBox([s[0] for s in SEITEN], "Fraeser laeuft ...",
                     "HolzHerLayerExport", st.get("hhl_seite_txt", SEITEN[0][0]))
    if idx is None:
        return
    seite = dict((a, b) for a, b in SEITEN)[idx]
    st["hhl_seite_txt"] = idx

    wz = rs.GetInteger("Werkzeug-ID (Fraeser)", st.get("hhl_wz", 20), 1, 9999)
    if wz is None:
        return
    vor = rs.GetInteger("Vorschub in Prozent (10er-Schritte)",
                        st.get("hhl_vor", 100), 10, 100)
    if vor is None:
        return
    tiefe = rs.GetReal("Fraestiefe in mm (positiv eingeben)",
                       st.get("hhl_tiefe", 19.0), 0.01)
    if tiefe is None:
        return
    staerke = rs.GetReal("Plattenstaerke in mm", st.get("hhl_staerke", 19.0), 0.1)
    if staerke is None:
        return
    st["hhl_wz"], st["hhl_vor"] = wz, vor
    st["hhl_tiefe"], st["hhl_staerke"] = tiefe, staerke

    bohrungen = rs.GetObjects("Bohrkreise waehlen (Enter = keine)",
                              rs.filter.curve, select=False)
    bohr_wz, bohr_tiefe = None, None
    if bohrungen:
        bohr_wz = rs.GetInteger("Werkzeug-ID (Bohrer)", st.get("hhl_bwz", 501), 1, 9999)
        if bohr_wz is None:
            return
        bohr_tiefe = rs.GetReal("Bohrtiefe in mm (positiv)", st.get("hhl_bt", 13.0), 0.01)
        if bohr_tiefe is None:
            return
        st["hhl_bwz"], st["hhl_bt"] = bohr_wz, bohr_tiefe

    path = rs.SaveFileName("DXF fuer HOPS-Layer-Import speichern",
                           "DXF (*.dxf)|*.dxf||")
    if not path:
        return

    vorlage = str(werkzeuge().get("vorlage", "KB"))
    info = export(path, curves, seite, wz, vor, tiefe, staerke,
                  bohrungen, bohr_wz, bohr_tiefe, vorlage)
    print("  Vorlage %s (werkzeuge.json)" % vorlage)

    print("HolzHerLayerExport: %d Konturen (%d Stuetzpunkte), %d Bohrungen "
          "manuell + %d aus depth-Tags -> %s"
          % (info["konturen"], info["segmente"], info["bohrungen"],
             info["bohr_auto"], os.path.basename(path)))
    if info["fremde_d"]:
        rs.MessageBox("Bohrdurchmesser %s mm sind NICHT am Bohrkopf "
                      "(vorhanden: %s).\nHOPS kann sie nicht bohren - "
                      "Werkzeug pruefen!" % (info["fremde_d"],
                                             werkzeuge().get("bohrkopf_mm")),
                      48, "HolzHerLayerExport")
    print("  Rohteil %.1f x %.1f x %.1f mm, Werkzeug-Layer '%s', Tiefe -%.1f"
          % (info["breite"], info["hoehe"], staerke, info["layer"], abs(tiefe)))
    if info["mikro_weg"]:
        print("  %d Mikro-Segment(e) unter %s mm zusammengefasst"
              % (info["mikro_weg"], MIN_SEG))
    if info["offen"]:
        rs.MessageBox("%d Kontur(en) sind OFFEN geblieben (Luecke > %s mm).\n"
                      "Sie werden als offene Polylinie exportiert - vor dem "
                      "Fraesen pruefen!" % (info["offen"], GAP_TOL), 48,
                      "HolzHerLayerExport")
    if info["leer"]:
        rs.MessageBox("%d Kontur(en) wurden NICHT exportiert (zu klein oder nur "
                      "ein Stuetzpunkt).\nDafuer fehlt in der DXF jede "
                      "Bearbeitung." % info["leer"], 48, "HolzHerLayerExport")
    if info["z_spanne"] > 10 * sc.doc.ModelAbsoluteTolerance:
        rs.MessageBox("Die Konturen sind nicht planar zu XY (Z-Spanne %.2f mm).\n"
                      "Die DXF enthaelt nur die Projektion - Masse pruefen!"
                      % info["z_spanne"], 48, "HolzHerLayerExport")

    if not rs.IsAlias("HolzHerLayerExport"):
        rs.AddAlias("HolzHerLayerExport",
                    '! _-RunPythonScript "HolzHerLayerExport"')
        print('Alias "HolzHerLayerExport" angelegt.')


def export(path, curves, seite, wz, vor, tiefe, staerke,
           bohrungen=None, bohr_wz=None, bohr_tiefe=None, vorlage="KB"):
    """Kern ohne Dialoge - so auch headless testbar.

    vorlage="KB": Leos HOPS-7.7-Importvorlage (Maschinentest 2026-09-01,
    Seite_links.hop). Sie macht aus JEDEM Layer KonturFraesen-Bloecke und
    ignoriert Startpunkte, Layernamen-Werkzeug und Konturtiefen. Die DXF
    enthaelt deshalb NUR Konturen + Bohrkreise: das weisse Rohteil-Rechteck
    wurde dort als Doppel-Kontur gefraest, der gelbe Startpunkt als Tasche,
    und beide haben Rohteilmass und Nullpunkt verschoben. Frastiefe stellt
    der Bediener in der Vorlage ein (Referenz Unterkante, -0.2).
    vorlage="SP": HolzHer-Schulungsdoku (Muster_DXF_Import) mit Rohteil,
    gelbem Startpunkt und Tiefe als Objekthoehe - fuer Vorlagen, die das
    wirklich auswerten (DXF_MC-Klasse).

    Klassifiziert die Auswahl SELBST: Kreise mit depth-UserText (KorpusBau-
    Bohrbilder) werden IMMER als Bohrungen exportiert, egal in welchem Prompt
    sie ankamen. Vorher wurde ein per Fensterauswahl mitgefangener Lochreihen-
    Kreis stumm zur Fraeskontur - an der Maschine fraeste sie dann 'Kreise
    und sowas'.
    """
    # --- Geometrie aufbereiten (nur Kopien im Speicher) ------------------
    tol = sc.doc.ModelAbsoluteTolerance
    wzg = werkzeuge()

    auto_bohr = []                # (Kreis, Tiefe) aus depth-Tags
    kontur_ids = []
    for c in curves:
        crv = rs.coercecurve(c)
        if crv is None:
            continue
        tag = None
        try:
            tag = rs.GetUserText(c, "depth")
        except Exception:
            pass
        ok, circ = crv.TryGetCircle(tol)
        if tag and ok:
            t = abs(staerke) + 1.0 if tag == "durch" else abs(float(tag))
            auto_bohr.append((circ, t))
        else:
            kontur_ids.append(c)

    geo = [rs.coercecurve(c).DuplicateCurve() for c in kontur_ids]
    joined = Rhino.Geometry.Curve.JoinCurves(geo, tol) or geo
    konturen = []
    offen = 0
    for c in joined:
        if not c.IsClosed:
            c.MakeClosed(GAP_TOL)
        if not c.IsClosed:
            offen += 1
        konturen.append(c)
    if not konturen:
        raise ValueError("Keine Fraeskontur in der Auswahl - nur Bohrkreise?")

    bohr_geo = []
    for b in (bohrungen or []):
        crv = rs.coercecurve(b)
        ok, circ = crv.TryGetCircle(tol)
        if ok:
            tag = None
            try:
                tag = rs.GetUserText(b, "depth")
            except Exception:
                pass
            if tag:               # getaggt: Tiefe vom Tag, nicht vom Prompt
                t = abs(staerke) + 1.0 if tag == "durch" else abs(float(tag))
                auto_bohr.append((circ, t))
            else:
                bohr_geo.append(circ)

    # Nullpunkt und Rohteilmass kommen NUR aus den Konturen. Bohrkreise duerfen
    # hier nicht mitzaehlen: Horizontalbohrungen liegen absichtlich AUSSERHALB
    # der Plattenkontur (der Betrag des Ueberstands ist fuer HOPS die Z-Hoehe
    # der Kantenbohrung). Wuerden sie die Bounding Box aufziehen, schoebe der
    # Nullpunkt sie in die Platte hinein - aus der Kantenbohrung wuerde eine
    # Vertikalbohrung neben der Kante. Mitverschoben werden sie natuerlich.
    bb = Rhino.Geometry.BoundingBox.Empty
    for c in konturen:
        bb.Union(c.GetBoundingBox(True))
    dx, dy = -bb.Min.X, -bb.Min.Y
    shift = Rhino.Geometry.Transform.Translation(dx, dy, 0)
    for c in konturen:
        c.Transform(shift)
    for circ in bohr_geo:
        circ.Transform(shift)
    for circ, _ in auto_bohr:
        circ.Transform(shift)
    breite = bb.Max.X - bb.Min.X
    hoehe = bb.Max.Y - bb.Min.Y

    # --- DXF schreiben ---------------------------------------------------
    lay_wz = tool_layer(seite, vor, wz)
    d = DxfR12()
    if vorlage == "SP":
        d.layer(lay_wz, GELB)

    if vorlage == "SP":
        # Grundplatte: Rohteil-Rechteck, weiss, Layer 0, Objekthoehe -Staerke
        d.polyline("0", [(0.0, 0.0), (breite, 0.0), (breite, hoehe),
                         (0.0, hoehe)], True, WEISS, -abs(staerke))

    n_seg = 0
    n_drop = 0
    n_kont = 0
    leer = 0
    for c in konturen:
        verts, dropped = contour_vertices(c)
        n_drop += dropped
        if len(verts) < 2:
            leer += 1            # nicht still verschlucken, sondern melden
            continue
        n_seg += len(verts)
        n_kont += 1
        # EINE geschlossene Polylinie je Kontur -> HOPS kann sie nicht teilen
        d.polyline("0", verts, c.IsClosed, ROT, 0)
        if vorlage == "SP":
            # genau EIN Startpunkt je Kontur -> genau EINE Anfahrt.
            # Er sitzt auf Vertex 0, also auf einem echten Stuetzpunkt.
            d.circle(lay_wz, verts[0][0], verts[0][1], STARTPUNKT_R, GELB,
                     -abs(tiefe))

    if bohr_geo:
        lay_b = "TT$%d$" % bohr_wz
        d.layer(lay_b, BLAU)
        for circ in bohr_geo:
            p = circ.Center
            d.circle(lay_b, p.X, p.Y, circ.Radius, BLAU, -abs(bohr_tiefe))

    fremde_d = set()
    if auto_bohr:
        lay_a = "TT$%d$" % int(wzg.get("bohrer_typ", 501))
        d.layer(lay_a, BLAU)
        bohrkopf = wzg.get("bohrkopf_mm", [])
        # Kreis AUSSERHALB der Kontur = Kantenbohrung (KONVENTION.md) -
        # die prueft gegen den HORIZONTAL-Bohrkopf, nicht den vertikalen
        # (werkzeuge.json bohrkopf_horizontal_mm; vorher fiel ein D5 in
        # der Kante nicht auf - Luecke 0.4 im PLAN_FREIE_GEOMETRIE).
        horizontal = wzg.get("bohrkopf_horizontal_mm", []) or bohrkopf
        for circ, t in auto_bohr:
            p = circ.Center
            d.circle(lay_a, p.X, p.Y, circ.Radius, BLAU, -abs(t))
            dm = round(circ.Radius * 2.0, 1)
            kante = (p.X < -0.001 or p.X > breite + 0.001 or
                     p.Y < -0.001 or p.Y > hoehe + 0.001)
            kopf = horizontal if kante else bohrkopf
            if kopf and not any(abs(dm - b) < 0.11 for b in kopf):
                fremde_d.add(dm)

    d.save(path)
    return {"konturen": n_kont,            # geschriebene, nicht eingelesene
            "segmente": n_seg, "bohrungen": len(bohr_geo), "offen": offen,
            "bohr_auto": len(auto_bohr), "fremde_d": sorted(fremde_d),
            "mikro_weg": n_drop, "leer": leer,
            "z_spanne": bb.Max.Z - bb.Min.Z,
            "breite": breite, "hoehe": hoehe, "layer": lay_wz}


if __name__ == "__main__":
    main()
