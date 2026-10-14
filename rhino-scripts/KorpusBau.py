# -*- coding: utf-8 -*-
"""KorpusBau - aus einem Quader oder Grundriss-Rechteck einen Korpus bauen.

Ablauf:
  1. Quader ODER Rechteck waehlen
     - Quader: Grundflaeche = Korpus-AUSSENmass, Hoehe = Korpushoehe
     - Rechteck (flach): Grundriss, Hoehe wird im Dialog abgefragt
  2. Frontseite bzw. Frontkante anklicken
  3. Ein Dialog mit allen Parametern (merkt sich die letzten Werte)

Erzeugt Seiten, Boden, Deckel, Rueckwand (eingenutet/eingefaelzt, Nut bzw.
Falz werden wirklich ausgeschnitten), Einlegeboeden, Tueren und optional die
Lochreihe System 32 als Bohrkreise.

Festlegungen, die aus dem Rechteck allein nicht hervorgehen:
  - Das Rechteck ist das AUSSENmass des Korpus. Aufschlagende Tueren liegen
    davor, das Moebel wird also um die Tuerstaerke tiefer.
  - Einschlagende Tueren sitzen buendig in der Front, umlaufend mit Luft.
  - Lochreihen werden als Kreise (Bohrbild) auf die Innenseiten gelegt, nicht
    gebohrt - so bleibt das Modell schlank und die Kreise gehen direkt in den
    CNC-Export.

Erster Aufruf:  _-RunPythonScript "KorpusBau"
"""
from __future__ import division      # IronPython 2.7: sonst Ganzzahldivision!
import rhinoscriptsyntax as rs
import scriptcontext as sc
import Rhino
import System.Drawing

from Rhino.Geometry import Interval, Box, Plane, Point3d, Vector3d, Brep

HOVER_GELB = System.Drawing.Color.FromArgb(255, 235, 59)
HOVER_RAND = System.Drawing.Color.FromArgb(255, 160, 0)

AUTO_2_TUEREN_AB = 1000.0     # mm Frontbreite -> zwingend zwei Tueren
RASTER_32 = 32.0
LOCH_D = 5.0
LOCH_T = 13.0                 # Bohrtiefe Lochreihe / Montageplatten-Loecher
PLATTE_Y = 37.0               # Kreuzmontageplatte: Schrauben 37 mm von vorn
MIN_TD_BAND = 16.0            # Topf T13 + 3 mm Restwand
GRIFF_D = 5.0
GRIFF_VON_KANTE = 40.0        # Griffbohrung: Abstand von der Griffkante
GRIFF_VON_OBEN_UNTEN = 50.0
STICKY = "korpusbau_werte2"

# Topfband-Presets: Topf immer D35/T13; Bohrbild der Topfbefestigung
# (achs = Lochabstand parallel zur Bandachse, versatz = hinter Topfmitte).
# arm_fd: erreichbarer Aufschlag = arm_fd + TopfKante (gerader Arm). Mit K=4
# sind das 15 mm - NICHT die volle 19er-Seite; die Tuergeometrie muss dem
# Bohrbild folgen, sonst kollidieren Doppeltueren an der Mittelfuge.
BAENDER = {
    "Blum":    {"topf_d": 35.0, "topf_t": 13.0, "arm_fd": 11.0,
                "schr_d": 8.0, "schr_t": 11.0, "achs": 45.0, "versatz": 9.5},
    "Hettich": {"topf_d": 35.0, "topf_t": 13.0, "arm_fd": 11.0,
                "schr_d": 10.0, "schr_t": 11.0, "achs": 52.0, "versatz": 5.5},
}

DEFAULTS = {
    "H": 720.0, "d": 19.0, "bauart": 0, "n_tueren": 2, "anschlag": 0,
    "td": 19.0, "luft": 3.0, "rueckwand": 0, "rw_d": 8.0, "tiefe_nf": 8.0,
    "rw_abstand": 10.0, "lochreihe": True, "loch_abstand": 37.0, "n_boeden": 2,
    "baender": 0, "band_seite": 0, "topf_kante": 4.0,
    "griff": 0, "griff_abstand": 128.0, "griff_lage": 0,
    "material": "mpx-birch", "material_rw": "mdf", "verbinder": 0,
}
BAUARTEN = ["SeitenDurchgehend", "BodenDeckelDurchgehend"]
ANSCHLAEGE = ["Aufschlagend", "Einschlagend"]
RUECKWAENDE = ["Eingenutet", "Eingefaelzt", "Keine"]
BAENDER_ARTEN = ["Keine", "Blum", "Hettich"]
SEITEN_LR = ["Links", "Rechts"]
GRIFF_ARTEN = ["Kein", "Knopf", "Stange"]
GRIFF_LAGEN = ["Oben", "Unten"]

# Eckverbindung Seiten <-> Boden/Deckel. Duebel 8x30 wird als echtes
# CNC-Bohrbild gesetzt (12 mm Flaeche + 18 mm Hirn); Clamex/Cabineo sind
# Fraesbilder und werden vorerst nur als Marker dargestellt (ohne process-Tag,
# damit der CNC-Export sie nicht als Bohrung mitnimmt).
VERBINDER_ARTEN = ["Keine", "Duebel", "Clamex", "Cabineo"]
DUEBEL_D = 8.0
DUEBEL_FLAECHE_T = 12.0
DUEBEL_STIRN_T = 18.0
VERB_RAND = 50.0              # Abstand des ersten/letzten Verbinders vom Rand
VERB_MAX_ABSTAND = 200.0
MARKER_D = 6.0

TAGS_JSON = r"D:\Projekte\SynologyDrive\_standards\tags.json"


def material_liste():
    """Werteliste wie beim Tag-System - eine Quelle, keine Drift."""
    try:
        import json
        f = open(TAGS_JSON)
        try:
            return list(json.load(f)["keys"]["material"])
        finally:
            f.close()
    except Exception:
        return ["mpx-birch", "mpx-beech", "mdf", "osb", "other"]


def korpus_farbe(nr):
    """Eigene Farbe je Korpus: Goldener-Schnitt-Drehung durch den Farbkreis."""
    h = ((nr - 1) * 0.618034) % 1.0
    i = int(h * 6.0)
    f = h * 6.0 - i
    v, s = 0.88, 0.55
    p, q, t = v * (1 - s), v * (1 - s * f), v * (1 - s * (1 - f))
    r, g, b = [(v, t, p), (q, v, p), (p, v, t),
               (p, q, v), (t, p, v), (v, p, q)][i % 6]
    return (int(r * 255), int(g * 255), int(b * 255))


def heller(farbe, t):
    """Farbe um Anteil t Richtung Weiss mischen (wie SubLayerGradient)."""
    return tuple(int(c + (255 - c) * t) for c in farbe)


# ----------------------------------------------------------------- Hilfsmittel
class _KantenPicker(Rhino.Input.Custom.GetPoint):
    """GetPoint, der die Rechteckkante unterm Cursor dick gelb hervorhebt."""

    def __init__(self, segs):
        self._segs = segs
        self.idx = None
        self.SetCommandPrompt("FRONTKANTE anklicken (leuchtet gelb)")

    def _naechste(self, p):
        def abstand(seg):
            rc = seg.ClosestPoint(p)
            t = rc[1] if isinstance(rc, tuple) else rc
            return seg.PointAt(t).DistanceTo(p)
        return min(range(len(self._segs)),
                   key=lambda i: abstand(self._segs[i]))

    def OnMouseMove(self, e):
        self.idx = self._naechste(e.Point)
        Rhino.Input.Custom.GetPoint.OnMouseMove(self, e)

    def OnDynamicDraw(self, e):
        if self.idx is not None:
            e.Display.DrawCurve(self._segs[self.idx], HOVER_GELB, 7)
        Rhino.Input.Custom.GetPoint.OnDynamicDraw(self, e)


def korpus_ebene(rect_id):
    """Rechteck + Frontkante -> (Ebene, Breite, Tiefe).

    Die Ebene liegt in der vorderen unteren linken Aussenecke:
    X laeuft nach rechts entlang der Front, Y in die Tiefe, Z nach oben.
    """
    crv = rs.coercecurve(rect_id)
    if crv is None or not crv.IsClosed:
        raise ValueError("Bitte ein geschlossenes Rechteck waehlen.")
    ok, ebene = crv.TryGetPlane(sc.doc.ModelAbsoluteTolerance)
    if not ok or abs(ebene.Normal.Z) < 0.99:
        raise ValueError("Das Rechteck muss flach liegen (Grundriss in XY).")
    segs = crv.DuplicateSegments()
    if not segs or len(segs) != 4:
        raise ValueError("Erwartet wird ein Rechteck aus 4 Kanten "
                         "(gefunden: %d)." % (len(segs) if segs else 0))

    gp = _KantenPicker(segs)
    if gp.Get() != Rhino.Input.GetResult.Point:
        return None
    punkt = gp.Point()

    def abstand(seg):
        rc = seg.ClosestPoint(punkt)
        t = rc[1] if isinstance(rc, tuple) else rc
        return seg.PointAt(t).DistanceTo(punkt)
    front = min(segs, key=abstand)

    p0, p1 = front.PointAtStart, front.PointAtEnd
    mitte = crv.GetBoundingBox(True).Center
    xdir = Vector3d(p1 - p0)
    xdir.Unitize()
    nach_innen = Vector3d(mitte - Point3d((p0.X + p1.X) / 2.0,
                                          (p0.Y + p1.Y) / 2.0,
                                          (p0.Z + p1.Z) / 2.0))
    nach_innen.Z = 0.0
    tiefe = nach_innen.Length * 2.0
    nach_innen.Unitize()
    # Rechtssystem erzwingen: X x Y muss nach oben zeigen, sonst Front spiegeln
    if Vector3d.CrossProduct(xdir, nach_innen).Z < 0:
        p0, p1 = p1, p0
        xdir = Vector3d(p1 - p0)
        xdir.Unitize()
    return Plane(p0, xdir, nach_innen), p0.DistanceTo(p1), tiefe


def _seiten_liste(x0, y0, x1, y1, px, py):
    """4 Seitenkandidaten: (Abstand zum Cursor, Ursprung, X-Richtung, Y-Richtung).
    Reihenfolge: vorn (y0), hinten (y1), links (x0), rechts (x1)."""
    dx, dy = x1 - x0, y1 - y0
    mx, my = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    return [
        (abs(py - y0) + max(0, abs(px - mx) - dx / 2), (x0, y0), (1, 0), (0, 1)),
        (abs(py - y1) + max(0, abs(px - mx) - dx / 2), (x1, y1), (-1, 0), (0, -1)),
        (abs(px - x0) + max(0, abs(py - my) - dy / 2), (x0, y1), (0, -1), (1, 0)),
        (abs(px - x1) + max(0, abs(py - my) - dy / 2), (x1, y0), (0, 1), (-1, 0)),
    ]


class _FrontPicker(Rhino.Input.Custom.GetPoint):
    """GetPoint, der die Seite unterm Cursor gelb-transparent hervorhebt.

    Angezeigt und geklickt wird ueber DIESELBE Abstandsfunktion - was leuchtet,
    wird auch genommen.
    """

    def __init__(self, x0, y0, x1, y1, z0, z1):
        self._grenzen = (x0, y0, x1, y1)
        tol = sc.doc.ModelAbsoluteTolerance
        ecken = [   # Reihenfolge wie _seiten_liste: vorn, hinten, links, rechts
            ((x0, y0), (x1, y0)), ((x1, y1), (x0, y1)),
            ((x0, y1), (x0, y0)), ((x1, y0), (x1, y1)),
        ]
        self._flaechen = []
        for (ax, ay), (bx, by) in ecken:
            self._flaechen.append(Brep.CreateFromCornerPoints(
                Point3d(ax, ay, z0), Point3d(bx, by, z0),
                Point3d(bx, by, z1), Point3d(ax, ay, z1), tol))
        self._mat = Rhino.Display.DisplayMaterial()
        self._mat.Diffuse = HOVER_GELB
        self._mat.Emission = HOVER_GELB
        self._mat.Transparency = 0.55
        self.idx = None
        self.SetCommandPrompt("FRONTSEITE des Quaders anklicken "
                              "(leuchtet gelb)")

    def OnMouseMove(self, e):
        x0, y0, x1, y1 = self._grenzen
        s = _seiten_liste(x0, y0, x1, y1, e.Point.X, e.Point.Y)
        self.idx = min(range(4), key=lambda i: s[i][0])
        Rhino.Input.Custom.GetPoint.OnMouseMove(self, e)

    def OnDynamicDraw(self, e):
        if self.idx is not None and self._flaechen[self.idx]:
            e.Display.DrawBrepShaded(self._flaechen[self.idx], self._mat)
            e.Display.DrawBrepWires(self._flaechen[self.idx], HOVER_RAND)
        Rhino.Input.Custom.GetPoint.OnDynamicDraw(self, e)


def quader_basis(obj_id, punkt=None):
    """Quader -> (Ebene, Breite, Tiefe, Hoehe). Frontseite per Klick.

    Erwartet einen weltachsen-parallelen Quader (so entsteht er mit dem
    Box-Befehl im Grundriss). Ein gedrehter Quader wird erkannt und abgelehnt,
    statt stillschweigend die falsche Bounding Box zu verbauen.
    """
    brep = rs.coercebrep(obj_id)
    if brep is None:
        return None
    bb = rs.BoundingBox(obj_id)
    x0, y0, z0 = bb[0][0], bb[0][1], bb[0][2]
    x1, y1, z1 = bb[6][0], bb[6][1], bb[6][2]
    dx, dy, dz = x1 - x0, y1 - y0, z1 - z0
    if min(dx, dy, dz) <= 0:
        raise ValueError("Der Quader hat keine gueltige Groesse.")
    v_bb = dx * dy * dz
    v = abs(brep.GetVolume())
    if v and abs(v - v_bb) > 0.01 * v_bb:
        raise ValueError("Der Quader ist gedreht oder kein Quader - bitte "
                         "weltachsen-parallel zeichnen (Volumen %.0f, "
                         "Huellbox %.0f)." % (v, v_bb))

    if punkt is None:
        gp = _FrontPicker(x0, y0, x1, y1, z0, z1)
        if gp.Get() != Rhino.Input.GetResult.Point:
            return None
        punkt = gp.Point()
    px, py = punkt[0], punkt[1]
    seiten = _seiten_liste(x0, y0, x1, y1, px, py)
    _, urspr, xd, yd = min(seiten, key=lambda s: s[0])
    ebene = Plane(Point3d(urspr[0], urspr[1], z0),
                  Vector3d(xd[0], xd[1], 0), Vector3d(yd[0], yd[1], 0))
    breite = dx if xd[1] == 0 else dy
    tiefe = dy if yd[0] == 0 else dx
    return ebene, breite, tiefe, dz


def box_brep(ebene, xi, yi, zi):
    return Box(ebene, Interval(xi[0], xi[1]), Interval(yi[0], yi[1]),
               Interval(zi[0], zi[1])).ToBrep()


def schneiden(brep, cutter, tol, name, fehler):
    """BooleanDifference mit Rueckfallebene: lieber ungeschnitten als weg."""
    if not cutter:
        return brep
    res = Brep.CreateBooleanDifference([brep], cutter, tol)
    if not res:
        fehler.append(name)
        return brep
    return max(res, key=lambda b: abs(b.GetVolume()))


def ablegen(brep, name, lay, dicke, teile, material=None):
    gid = sc.doc.Objects.AddBrep(brep)
    rs.ObjectName(gid, name)
    rs.ObjectLayer(gid, lay)
    rs.SetUserText(gid, "process", "cnc")
    rs.SetUserText(gid, "thickness", "%g" % dicke)
    if material:
        rs.SetUserText(gid, "material", material)
    teile.append(gid)
    return gid


def layer(pfad, farbe=None):
    teil = ""
    for stueck in pfad.split("::"):
        teil = stueck if not teil else teil + "::" + stueck
        if not rs.IsLayer(teil):
            rs.AddLayer(teil, farbe if teil == pfad else None)
    return pfad


def bohrung(p, normal, dm, tiefe, part, lay, teile):
    """DIE eine Stelle, die Bohrkreise schreibt.

    Jeder Kreis traegt part (Teilname wie am Brep), depth (mm oder 'durch')
    und process=cnc. Der Durchmesser steckt in der Geometrie selbst.
    Bewusst KEIN Werkzeug-Key am Objekt - das Tool-Mapping gehoert in den
    Export, sonst vergiftet ein Bohrerwechsel alte Modelle.
    """
    kreis = rs.AddCircle(Plane(p, normal), dm / 2.0)
    rs.ObjectLayer(kreis, lay)
    rs.SetUserText(kreis, "process", "cnc")
    rs.SetUserText(kreis, "part", part)
    rs.SetUserText(kreis, "depth", "durch" if tiefe is None else "%g" % tiefe)
    teile.append(kreis)
    return kreis


# ------------------------------------------- Raster-Mathematik (dokumentlos
# testbar; Lochindex k: Loch k liegt auf z = d + k*32, k >= 1)
def gueltige_k(H, d):
    """Bandlagen-Indizes, deren LochPAAR (k, k+1) komplett existiert.

    Die Kreuzmontageplatte greift in zwei uebereinanderliegende Loecher;
    Band k sitzt auf der Rastermitte dazwischen (z = d + k*32 + 16).
    """
    ks = []
    k = 1
    while d + (k + 1) * RASTER_32 < H - d:
        ks.append(k)
        k += 1
    return ks


def boden_js(H, d, n_bd):
    """Gleichverteilte Boeden auf Lochindizes gerundet, Duplikate ausgewichen.

    Der Bodentraeger sitzt IN Loch j, die Bodenunterkante liegt auf
    z = d + j*32. Rueckgabe: Liste der j, aufsteigend.
    """
    licht = (H - 2 * d) - n_bd * d
    if licht <= 0 or n_bd <= 0:
        return []
    fach = licht / (n_bd + 1)
    max_j = int((H - 3 * d) // RASTER_32)     # Boden (Dicke d) unter dem Deckel
    while max_j >= 1 and d + max_j * RASTER_32 + d > H - d:
        max_j -= 1
    if max_j < 1:
        raise ValueError("Korpus zu niedrig fuer Boeden auf der Lochreihe.")
    js = []
    for i in range(n_bd):
        z0 = d + (i + 1) * fach + i * d
        j = int(round((z0 - d) / RASTER_32))
        j = max(1, min(j, max_j))
        while j in js and j < max_j:
            j += 1
        while j in js and j > 1:
            j -= 1
        if j in js:
            raise ValueError("Zu viele Einlegeboeden fuer die Lochreihe.")
        js.append(j)
    return sorted(js)


def band_anzahl(tuer_h):
    """Faustregel Bandanzahl nach Tuerhoehe (Blum/Hettich-Richtwerte)."""
    if tuer_h <= 1000.0:
        return 2
    if tuer_h <= 1600.0:
        return 3
    if tuer_h <= 2000.0:
        return 4
    return 5


def band_ks(H, d, n_band, belegt):
    """Bandlagen waehlen: Endbaender = erste/letzte freie Rastermitte,
    Zwischenbaender gleichverteilt. Belegungsmodell: Boden belegt Loch j,
    Band das Paar (k, k+1) - Konflikt genau dann, wenn j in {k, k+1}.
    Rueckgabe None, wenn nicht genug kollisionsfreie Lagen existieren.
    """
    frei = [k for k in gueltige_k(H, d)
            if k not in belegt and (k + 1) not in belegt]
    if len(frei) < n_band:
        return None
    if n_band == 1:
        return [frei[0]]
    ks = sorted(set(frei[int(round(i * (len(frei) - 1) / (n_band - 1.0)))]
                    for i in range(n_band)))
    if len(ks) != n_band:
        return None
    # benachbarte Rastermitten liegen 32 mm auseinander - zwei D35-Toepfe
    # wuerden sich ueberlappen, und die Platte braeuchte Loch k+1 doppelt
    if any(b - a < 2 for a, b in zip(ks, ks[1:])):
        return None
    return ks


def pruefe_w(w):
    """Regelpruefungen - laufen im Dialog UND in bauen() (Testpfad)."""
    if w["H"] <= 2 * w["d"]:
        raise ValueError("Korpushoehe muss groesser als 2x Materialstaerke sein.")
    if w["rw_art"] != "K" and w["tiefe_nf"] >= w["d"]:
        raise ValueError("Nut-/Falztiefe muss kleiner als die Materialstaerke "
                         "sein.")
    if w["band_art"] != "Keine":
        if w["n_tueren"] < 1:
            raise ValueError("Baender gewaehlt, aber keine Tueren.")
        if w["anschlag"] == "E":
            raise ValueError("Baender gibt es vorerst nur fuer AUFSCHLAGENDE "
                             "Tueren (einschlagend braucht andere Kroepfung).")
        if not w["lochreihe"]:
            raise ValueError("Baender brauchen die Lochreihe (Montageplatten "
                             "sitzen im 32er-Raster).")
        if w["td"] < MIN_TD_BAND:
            raise ValueError("Tuerstaerke %.0f mm zu duenn fuer den 35er-Topf "
                             "(T13 + 3 mm Restwand = min. %.0f mm)."
                             % (w["td"], MIN_TD_BAND))
    if w["griff"] != "Kein" and w["n_tueren"] < 1:
        raise ValueError("Griffbohrungen gewaehlt, aber keine Tueren.")
    if w["lochreihe"] and w["d"] < LOCH_T + 3.0:
        raise ValueError("Seitenstaerke %g mm zu duenn fuer die Lochreihe "
                         "(T%g + 3 mm Restwand = min. %g mm)."
                         % (w["d"], LOCH_T, LOCH_T + 3.0))
    if w["verbinder"] == "Duebel" and w["d"] < DUEBEL_FLAECHE_T + 3.0:
        raise ValueError("Materialstaerke %g mm zu duenn fuer Duebel "
                         "(Flaechenbohrung T%g + 3 mm Restwand)."
                         % (w["d"], DUEBEL_FLAECHE_T))
    if w["band_art"] != "Keine":
        abw = abs(w["loch_abstand"] - PLATTE_Y)
        if 0.01 < abw < 10.0:
            raise ValueError("LochAbstand %g kollidiert mit den "
                             "Plattenloechern bei %g - entweder %g verwenden "
                             "oder >= %g." % (w["loch_abstand"], PLATTE_Y,
                                              PLATTE_Y, PLATTE_Y + 10.0))


# --------------------------------------------------------------------- Aufbau
def bauen(ebene, B, T, w):
    """w = geprueftes Parameter-Dict. Rueckgabe: (Teile-Ids, Meldungen).

    Atomar: wirft eine Regel mitten im Aufbau (Bandlagen, Griff ausserhalb,
    Boeden), werden die bereits abgelegten Teile geloescht - entweder
    kompletter Korpus samt Bohrbild oder gar nichts. Ein Torso mit fertigen
    part-Tags wuerde sonst still mitexportiert.
    """
    teile = []
    try:
        return _bauen(ebene, B, T, w, teile)
    except ValueError:
        if teile:
            rs.DeleteObjects(teile)
        raise


def _bauen(ebene, B, T, w, teile):
    pruefe_w(w)
    tol = sc.doc.ModelAbsoluteTolerance
    d, H = w["d"], w["H"]
    hinweise, fehler = [], []

    # Jeder Korpus bekommt einen eigenen nummerierten Layer mit eigener Farbe;
    # die Unterlayer laufen im Farbverlauf zur helleren Variante (wie
    # SubLayerGradient). Lochreihe/Bohrungen/Verbinder behalten Signalfarben.
    nr = 1
    while rs.IsLayer("Korpus_%02d" % nr):
        nr += 1
    basis = "Korpus_%02d" % nr
    bf = korpus_farbe(nr)
    layer(basis, bf)
    lay_seiten = layer(basis + "::Seiten", heller(bf, 0.0))
    lay_bd = layer(basis + "::Boden_Deckel", heller(bf, 0.18))
    lay_rw = layer(basis + "::Rueckwand", heller(bf, 0.36))
    lay_boeden = layer(basis + "::Einlegeboeden", heller(bf, 0.54))
    lay_tuer = layer(basis + "::Tueren", heller(bf, 0.7))
    lay_loch = layer(basis + "::Lochreihe", (0, 0, 255))
    lay_bohr = layer(basis + "::Bohrungen", (255, 0, 0))
    lay_verb = layer(basis + "::Verbinder", (255, 140, 0))
    lay_nut = layer(basis + "::Nuten", (255, 0, 255))
    hinweise.append("Korpus liegt auf Layer %s." % basis)
    mat = w["material"]

    seiten_durch = (w["bauart"] == "S")
    if seiten_durch:
        s_z, bd_x = (0.0, H), (d, B - d)
    else:
        s_z, bd_x = (d, H - d), (0.0, B)

    # --- Rueckwand: Lage und Nut/Falz ------------------------------------
    rw_art, rw_d, tiefe_nf = w["rw_art"], w["rw_d"], w["tiefe_nf"]
    cutter_sl, cutter_sr, cutter_bo, cutter_de = [], [], [], []
    rw_brep = None
    y0 = T                                    # Vorderkante der Rueckwand

    if rw_art != "K":
        if rw_art == "N":                     # eingenutet
            y1 = T - w["rw_abstand"]
        else:                                 # eingefaelzt, buendig hinten
            y1 = T
        y0 = y1 - rw_d
        if y0 <= 0:
            raise ValueError("Rueckwand liegt vor der Front - Abstand oder "
                             "Staerke pruefen.")
        ueber = tiefe_nf                      # Eingriff in die Nachbarteile
        rx = (d - ueber, B - d + ueber)
        rz = (d - ueber, H - d + ueber)
        rw_brep = box_brep(ebene, rx, (y0, y1), rz)

        # Nut bzw. Falz aus den angrenzenden Teilen herausschneiden
        cutter_sl = [box_brep(ebene, (d - ueber, d), (y0, y1), rz)]
        cutter_sr = [box_brep(ebene, (B - d, B - d + ueber), (y0, y1), rz)]
        cutter_bo = [box_brep(ebene, rx, (y0, y1), (d - ueber, d))]
        cutter_de = [box_brep(ebene, rx, (y0, y1), (H - d, H - d + ueber))]

        if rw_art == "N":
            # Nut-Mittellinien auf den Innenflaechen: davon liest TeilExport
            # Lage/Breite/Tiefe fuer die _nuten_frei_v5-Zeile der .hop.
            # Der Falz bleibt aussen vor - der laeuft weiter ueber die Saege.
            # Die Linien werden auf die TEILkanten geklemmt - rx/rz spannen
            # die Rueckwand (inkl. Nuteingriff), nicht das Teil; negative
            # Nut-Koordinaten sind durch keine Maschinenreferenz gedeckt.
            ym = (y0 + y1) / 2.0
            nx = (max(rx[0], bd_x[0]), min(rx[1], bd_x[1]))
            nz = (max(rz[0], s_z[0]), min(rz[1], s_z[1]))
            for pa, pb, part in (
                    ((d, ym, nz[0]), (d, ym, nz[1]), "Seite links"),
                    ((B - d, ym, nz[0]), (B - d, ym, nz[1]), "Seite rechts"),
                    ((nx[0], ym, d), (nx[1], ym, d), "Boden"),
                    ((nx[0], ym, H - d), (nx[1], ym, H - d), "Deckel")):
                lin = sc.doc.Objects.AddLine(ebene.PointAt(*pa),
                                             ebene.PointAt(*pb))
                rs.ObjectLayer(lin, lay_nut)
                rs.SetUserText(lin, "part", part)
                rs.SetUserText(lin, "kind", "nut")
                # Nutbreite = Rueckwanddicke + 0.5 Luft (Quelle: Werkstatt
                # 2026-08-29: 8er Rueckwand -> Nut 0..8.5, Nuter faehrt
                # 3 Zustellungen: Flanke vorn, Flanke hinten, Mitte).
                rs.SetUserText(lin, "nut_b", "%g" % (rw_d + 0.5))
                rs.SetUserText(lin, "nut_t", "%g" % tiefe_nf)
                teile.append(lin)

    # --- Korpusteile ------------------------------------------------------
    sl = schneiden(box_brep(ebene, (0.0, d), (0.0, T), s_z), cutter_sl,
                   tol, "Seite links", fehler)
    sr = schneiden(box_brep(ebene, (B - d, B), (0.0, T), s_z), cutter_sr,
                   tol, "Seite rechts", fehler)
    bo = schneiden(box_brep(ebene, bd_x, (0.0, T), (0.0, d)), cutter_bo,
                   tol, "Boden", fehler)
    de = schneiden(box_brep(ebene, bd_x, (0.0, T), (H - d, H)), cutter_de,
                   tol, "Deckel", fehler)
    korpus4 = [
        ablegen(sl, "Seite links", lay_seiten, d, teile, mat),
        ablegen(sr, "Seite rechts", lay_seiten, d, teile, mat),
        ablegen(bo, "Boden", lay_bd, d, teile, mat),
        ablegen(de, "Deckel", lay_bd, d, teile, mat)]
    if rw_art == "F":
        # Falz-Fraesung ist in KEINER Exportdatei - das Flag laesst
        # TeilExport warnen, statt eine vollstaendig aussehende Datei
        # ohne diese Bearbeitung zu liefern
        for gid in korpus4:
            rs.SetUserText(gid, "falz", "ja")
    if rw_brep is not None:
        ablegen(rw_brep, "Rueckwand", lay_rw, rw_d, teile, w["material_rw"])

    # --- Einlegeboeden ----------------------------------------------------
    # Mit Lochreihe rasten die Boeden auf LOCHpositionen ein (Traeger sitzt
    # IN Loch j, Unterkante auf z = d + j*32) - Brep und Bohrbild sagen
    # dasselbe. Ohne Lochreihe: Gleichverteilung wie gehabt.
    n_bd = int(w["n_boeden"])
    belegt = set()
    if n_bd > 0:
        licht_h = (H - 2 * d) - n_bd * d
        fach = licht_h / (n_bd + 1)
        if fach <= 0:
            raise ValueError("Zu viele Einlegeboeden fuer diese Hoehe.")
        y_hinten = (y0 - 2.0) if rw_art != "K" else (T - 2.0)
        # einschlagende Tuer sitzt IN der Front (y 0..td) - die Boeden muessen
        # dahinter enden, sonst ist jeder Boden um td+Fuge zu tief zugeschnitten
        y_vorn = 0.0
        if int(w["n_tueren"]) > 0 and w["anschlag"] == "E":
            y_vorn = w["td"] + w["luft"]
            if y_vorn >= y_hinten:
                raise ValueError("Kein Platz fuer Einlegeboeden hinter der "
                                 "einschlagenden Tuer.")
        js = boden_js(H, d, n_bd) if w["lochreihe"] else None
        if js is not None:
            # Boden belegt sein Traegerloch j - und bei dicken Boeden (d>=32)
            # auch jedes weitere Loch, das im Plattenvolumen verschwindet
            for j in js:
                belegt.update(range(j, j + int(d // RASTER_32) + 1))
        for i in range(n_bd):
            z_gleich = d + (i + 1) * fach + i * d
            if js is not None:
                z0 = d + js[i] * RASTER_32
                if abs(z0 - z_gleich) > 0.5:
                    hinweise.append("Einlegeboden %d auf Lochreihe gerastet "
                                    "(%+.0f mm; Boeden bleiben verstellbar)."
                                    % (i + 1, z0 - z_gleich))
            else:
                z0 = z_gleich
            ablegen(box_brep(ebene, (d, B - d), (y_vorn, y_hinten),
                             (z0, z0 + d)),
                    "Einlegeboden %d" % (i + 1), lay_boeden, d, teile, mat)

    # --- Tueren -----------------------------------------------------------
    tueren = []                # (Name, tx0, tx1, tz0, tz1, Bandkante L/R)
    y_innen = 0.0
    n_t = int(w["n_tueren"])
    if n_t == 1 and B > AUTO_2_TUEREN_AB:
        n_t = 2
        hinweise.append("Frontbreite %.0f mm > %.0f mm - es wurden zwingend "
                        "2 Tueren gebaut." % (B, AUTO_2_TUEREN_AB))
    if n_t > 0:
        luft, td = w["luft"], w["td"]
        # Der erreichbare Aufschlag ist durch das Band begrenzt: gerader Arm
        # traegt arm_fd + TopfKante (Default 11 + 4 = 15 mm), NICHT die volle
        # Seitendicke. Die bandseitige Tuerkante rueckt entsprechend ein,
        # sonst kollidieren Doppeltueren an der Mittelfuge um genau die
        # Differenz. Ohne Baender bleibt die Geometrie wie gehabt.
        einzug = 0.0
        if w["band_art"] != "Keine" and w["anschlag"] == "A":
            aufschlag = min(d, BAENDER[w["band_art"]]["arm_fd"]
                            + w["topf_kante"])
            einzug = d - aufschlag
            if einzug > 0:
                hinweise.append("Aufschlag bandseitig %.1f mm (Arm-FD + "
                                "TopfKante) - Tuerkante rueckt %.1f mm ein."
                                % (aufschlag, einzug))
        if w["anschlag"] == "A":              # aufschlagend, vor dem Korpus
            tx0, tx1, tz0, tz1 = 0.0, B, 0.0, H
            ty = (-td, 0.0)
        else:                                 # einschlagend, in der Front
            tx0, tx1 = d + luft, B - d - luft
            tz0, tz1 = d + luft, H - d - luft
            ty = (0.0, td)
        if tx1 - tx0 <= 0 or tz1 - tz0 <= 0:
            raise ValueError("Tueroeffnung ist zu klein - Luft/Staerke pruefen.")
        y_innen = ty[1]                       # Innenseite der Tuer (zum Korpus)
        if n_t == 1:
            kante = w["band_seite"]
            x0t = tx0 + einzug if kante == "L" else tx0
            x1t = tx1 - einzug if kante == "R" else tx1
            ablegen(box_brep(ebene, (x0t, x1t), ty, (tz0, tz1)), "Tuer",
                    lay_tuer, td, teile, mat)
            tueren.append(("Tuer", x0t, x1t, tz0, tz1, kante))
        else:
            m = (tx0 + tx1) / 2.0
            ablegen(box_brep(ebene, (tx0 + einzug, m - luft / 2.0), ty,
                             (tz0, tz1)), "Tuer links", lay_tuer, td, teile,
                    mat)
            ablegen(box_brep(ebene, (m + luft / 2.0, tx1 - einzug), ty,
                             (tz0, tz1)), "Tuer rechts", lay_tuer, td, teile,
                    mat)
            # gespiegelt: jede Tuer bandet an ihrer AUSSENkante,
            # die Griffe wandern zur Mittelfuge
            tueren.append(("Tuer links", tx0 + einzug, m - luft / 2.0,
                           tz0, tz1, "L"))
            tueren.append(("Tuer rechts", m + luft / 2.0, tx1 - einzug,
                           tz0, tz1, "R"))

    # --- Lochreihe System 32 ---------------------------------------------
    if w["lochreihe"]:
        a = w["loch_abstand"]
        n_loch = 0
        z = d + RASTER_32
        while z < H - d:
            for y in (a, T - a):
                for x, normal, part in ((d, ebene.XAxis, "Seite links"),
                                        (B - d, -ebene.XAxis, "Seite rechts")):
                    bohrung(ebene.PointAt(x, y, z), normal, LOCH_D, LOCH_T,
                            part, lay_loch, teile)
                    n_loch += 1
            z += RASTER_32
        hinweise.append("Lochreihe: %d Bohrungen D%g im %g-mm-Raster."
                        % (n_loch, LOCH_D, RASTER_32))

    # --- Topfbaender ------------------------------------------------------
    if w["band_art"] != "Keine" and tueren:
        bd = BAENDER[w["band_art"]]
        topf_r = bd["topf_d"] / 2.0
        for name, tx0, tx1, tz0, tz1, kante in tueren:
            tuer_h = tz1 - tz0
            tuer_b = tx1 - tx0
            n_band = band_anzahl(tuer_h)
            if tuer_h > 2400.0:
                hinweise.append("%s: Hoehe %.0f mm - Bandanzahl (5) an der "
                                "Bandserie gegenpruefen." % (name, tuer_h))
            ks = band_ks(H, d, n_band, belegt)
            while ks is None and n_band > 2:
                n_band -= 1
                ks = band_ks(H, d, n_band, belegt)
            if ks is None:
                raise ValueError("%s: keine kollisionsfreien Bandlagen - "
                                 "Boeden anders verteilen." % name)
            if n_band != band_anzahl(tuer_h):
                hinweise.append("%s: nur %d statt %d Baender moeglich "
                                "(Boeden belegen das Raster)."
                                % (name, n_band, band_anzahl(tuer_h)))

            richtung = 1.0 if kante == "L" else -1.0
            x_kante = tx0 if kante == "L" else tx1
            x_topf = x_kante + richtung * (w["topf_kante"] + topf_r)
            seiten_name = "Seite links" if kante == "L" else "Seite rechts"
            x_seite = d if kante == "L" else B - d
            n_seite = ebene.XAxis if kante == "L" else -ebene.XAxis
            platte_da = (w["lochreihe"]
                         and abs(w["loch_abstand"] - PLATTE_Y) < 0.01)

            for k in ks:
                zb = d + k * RASTER_32 + RASTER_32 / 2.0
                if not (tz0 + topf_r <= zb <= tz1 - topf_r):
                    raise ValueError("%s: Bandlage k=%d liegt ausserhalb der "
                                     "Tuer." % (name, k))
                # Topf + Befestigungsbohrungen auf der Tuer-INNENSEITE
                bohrung(ebene.PointAt(x_topf, y_innen, zb), ebene.YAxis,
                        bd["topf_d"], bd["topf_t"], name, lay_bohr, teile)
                x_schr = x_topf + richtung * bd["versatz"]
                for dz in (-bd["achs"] / 2.0, bd["achs"] / 2.0):
                    bohrung(ebene.PointAt(x_schr, y_innen, zb + dz),
                            ebene.YAxis, bd["schr_d"], bd["schr_t"],
                            name, lay_bohr, teile)
                # Montageplatte korpusseitig: Lochpaar (k, k+1) bei y=37.
                # Liegt die Lochreihe genau dort, existieren die Loecher schon.
                if not platte_da:
                    for kk in (k, k + 1):
                        bohrung(ebene.PointAt(x_seite, PLATTE_Y,
                                              d + kk * RASTER_32),
                                n_seite, LOCH_D, LOCH_T, seiten_name,
                                lay_bohr, teile)
            spann = (ks[-1] - ks[0]) * RASTER_32
            if spann < tuer_b:
                hinweise.append("%s: Bandspannweite %.0f mm < Tuerbreite "
                                "%.0f mm - breite Tuer hebelt, ggf. Band "
                                "zulegen." % (name, spann, tuer_b))
        hinweise.append("Baender %s: Bohrbilder auf %d Tuer(en) gesetzt "
                        "(Topf D%g/T%g, Kantenabstand %g)."
                        % (w["band_art"], len(tueren), bd["topf_d"],
                           bd["topf_t"], w["topf_kante"]))

    # --- Griffbohrungen ---------------------------------------------------
    if w["griff"] != "Kein" and tueren:
        for name, tx0, tx1, tz0, tz1, kante in tueren:
            # Griff sitzt der Bandkante gegenueber (bei 2 Tueren: Mittelfuge)
            x_griff = (tx1 - GRIFF_VON_KANTE) if kante == "L" \
                else (tx0 + GRIFF_VON_KANTE)
            if w["griff_lage"] == "O":
                z_ref = tz1 - GRIFF_VON_OBEN_UNTEN
                zs = [z_ref] if w["griff"] == "Knopf" \
                    else [z_ref, z_ref - w["griff_abstand"]]
            else:
                z_ref = tz0 + GRIFF_VON_OBEN_UNTEN
                zs = [z_ref] if w["griff"] == "Knopf" \
                    else [z_ref, z_ref + w["griff_abstand"]]
            for zg in zs:
                if not (tz0 + 10.0 <= zg <= tz1 - 10.0):
                    raise ValueError("%s: Griffbohrung laege ausserhalb der "
                                     "Tuer - GriffAbstand pruefen." % name)
                bohrung(ebene.PointAt(x_griff, y_innen, zg), ebene.YAxis,
                        GRIFF_D, None, name, lay_bohr, teile)
        hinweise.append("Griffbohrungen D%g durchgehend gesetzt (%s)."
                        % (GRIFF_D, w["griff"]))

    # --- Eckverbinder Seiten <-> Boden/Deckel -----------------------------
    if w["verbinder"] != "Keine":
        n_v = max(2, int(round((T - 2 * VERB_RAND) / VERB_MAX_ABSTAND)) + 1)
        ys = [VERB_RAND + i * (T - 2 * VERB_RAND) / (n_v - 1.0)
              for i in range(n_v)]
        if seiten_durch:
            # Boden/Deckel-Hirn stoesst an die Seiten-Innenflaeche, Achse = X
            fugen = [("Boden", "Seite links", d, d / 2.0, ebene.XAxis),
                     ("Boden", "Seite rechts", B - d, d / 2.0, -ebene.XAxis),
                     ("Deckel", "Seite links", d, H - d / 2.0, ebene.XAxis),
                     ("Deckel", "Seite rechts", B - d, H - d / 2.0,
                      -ebene.XAxis)]
            punkte = [(hirn, flaech, (x, y, z), n)
                      for hirn, flaech, x, z, n in fugen for y in ys]
        else:
            # Seiten-Hirn steht auf Boden bzw. unter Deckel, Achse = Z
            fugen = [("Seite links", "Boden", d / 2.0, d, ebene.ZAxis),
                     ("Seite rechts", "Boden", B - d / 2.0, d, ebene.ZAxis),
                     ("Seite links", "Deckel", d / 2.0, H - d, -ebene.ZAxis),
                     ("Seite rechts", "Deckel", B - d / 2.0, H - d,
                      -ebene.ZAxis)]
            punkte = [(hirn, flaech, (x, y, z), n)
                      for hirn, flaech, x, z, n in fugen for y in ys]

        if w["verbinder"] == "Duebel":
            for hirn, flaech, (x, y, z), normal in punkte:
                p = ebene.PointAt(x, y, z)
                # ein Duebel = zwei Bohrbilder am selben Punkt: der part-Tag
                # sortiert sie im Export auf das jeweils richtige Teil
                bohrung(p, normal, DUEBEL_D, DUEBEL_FLAECHE_T, flaech,
                        lay_bohr, teile)
                bohrung(p, normal, DUEBEL_D, DUEBEL_STIRN_T, hirn,
                        lay_bohr, teile)
            hinweise.append("Duebel 8x30: %d Stueck je Ecke %d Positionen "
                            "(Flaeche T%g / Hirn T%g)."
                            % (len(punkte), n_v, DUEBEL_FLAECHE_T,
                               DUEBEL_STIRN_T))
        else:
            # Clamex/Cabineo: Fraesbild folgt spaeter - Marker OHNE process-
            # Tag, damit kein Export sie als Bohrung mitnimmt
            for hirn, flaech, (x, y, z), normal in punkte:
                kreis = rs.AddCircle(Plane(ebene.PointAt(x, y, z), normal),
                                     MARKER_D / 2.0)
                rs.ObjectLayer(kreis, lay_verb)
                rs.SetUserText(kreis, "part", hirn)
                rs.SetUserText(kreis, "connector", w["verbinder"])
                teile.append(kreis)
            hinweise.append("%s: %d Positionen als MARKER gesetzt (orange) - "
                            "Fraesbild ist noch nicht im CNC-Export."
                            % (w["verbinder"], len(punkte)))

    if fehler:
        hinweise.append("ACHTUNG - Nut/Falz konnte bei %s nicht geschnitten "
                        "werden, Teile sind ungeschnitten!" % ", ".join(fehler))
    return teile, hinweise


# ---------------------------------------------------------------------- Dialog
def parameter(B, hoehe_vorgabe=None):
    """Alle Parameter als klickbare Optionen in der Befehlszeile.

    Wie bei nativen Rhino-Befehlen: Option anklicken (oder Namen tippen),
    Wert setzen, Enter baut den Korpus. Bei Quader-Eingabe kommt die Hoehe
    aus der Geometrie und taucht gar nicht erst als Option auf.
    """
    from Rhino.Input.Custom import (GetOption, OptionDouble, OptionInteger,
                                    OptionToggle)
    st = dict(DEFAULTS)
    st.update(sc.sticky.get(STICKY) or {})

    o_H = OptionDouble(st["H"], 1.0, 100000.0)
    o_d = OptionDouble(st["d"], 1.0, 100.0)
    o_tueren = OptionInteger(int(st["n_tueren"]), 0, 2)
    o_td = OptionDouble(st["td"], 1.0, 100.0)
    o_luft = OptionDouble(st["luft"], 0.0, 50.0)
    o_rwd = OptionDouble(st["rw_d"], 0.0, 100.0)
    o_nf = OptionDouble(st["tiefe_nf"], 0.0, 50.0)
    o_rwa = OptionDouble(st["rw_abstand"], 0.0, 500.0)
    o_loch = OptionToggle(bool(st["lochreihe"]), "Nein", "Ja")
    o_locha = OptionDouble(st["loch_abstand"], 1.0, 500.0)
    o_boeden = OptionInteger(int(st["n_boeden"]), 0, 50)
    o_topfk = OptionDouble(st["topf_kante"], 3.0, 8.0)
    o_griffa = OptionDouble(st["griff_abstand"], 32.0, 480.0)
    i_bauart = int(st["bauart"])
    i_anschlag = int(st["anschlag"])
    i_rw = int(st["rueckwand"])
    i_baender = int(st["baender"])
    i_bandseite = int(st["band_seite"])
    i_griff = int(st["griff"])
    i_grifflage = int(st["griff_lage"])
    i_verb = int(st["verbinder"])
    mats = material_liste()
    i_mat = mats.index(st["material"]) if st["material"] in mats else 0
    i_matrw = mats.index(st["material_rw"]) if st["material_rw"] in mats else 0

    while True:
        go = GetOption()
        go.SetCommandPrompt("Korpus %.0f mm breit - Optionen anklicken, "
                            "Enter = bauen" % B)
        go.AcceptNothing(True)
        if hoehe_vorgabe is None:
            go.AddOptionDouble("Hoehe", o_H)
        go.AddOptionDouble("Staerke", o_d)
        ix_bauart = go.AddOptionList("Bauart", BAUARTEN, i_bauart)
        go.AddOptionInteger("Tueren", o_tueren)
        ix_anschlag = go.AddOptionList("Anschlag", ANSCHLAEGE, i_anschlag)
        go.AddOptionDouble("Tuerstaerke", o_td)
        go.AddOptionDouble("Fuge", o_luft)
        ix_rw = go.AddOptionList("Rueckwand", RUECKWAENDE, i_rw)
        go.AddOptionDouble("RueckwandStaerke", o_rwd)
        go.AddOptionDouble("NutFalzTiefe", o_nf)
        go.AddOptionDouble("RueckwandAbstand", o_rwa)
        go.AddOptionToggle("Lochreihe32", o_loch)
        go.AddOptionDouble("LochAbstand", o_locha)
        go.AddOptionInteger("Boeden", o_boeden)
        ix_baender = go.AddOptionList("Baender", BAENDER_ARTEN, i_baender)
        ix_bandseite = go.AddOptionList("BandSeite", SEITEN_LR, i_bandseite)
        go.AddOptionDouble("TopfKante", o_topfk)
        ix_griff = go.AddOptionList("Griff", GRIFF_ARTEN, i_griff)
        go.AddOptionDouble("GriffAbstand", o_griffa)
        ix_grifflage = go.AddOptionList("GriffLage", GRIFF_LAGEN, i_grifflage)
        ix_mat = go.AddOptionList("Material", mats, i_mat)
        ix_matrw = go.AddOptionList("MaterialRueckwand", mats, i_matrw)
        ix_verb = go.AddOptionList("Verbinder", VERBINDER_ARTEN, i_verb)

        res = go.Get()
        if res == Rhino.Input.GetResult.Nothing:
            break                              # Enter -> bauen
        if res != Rhino.Input.GetResult.Option:
            go.Dispose()
            return None                        # Esc -> Abbruch
        opt = go.Option()
        if opt.Index == ix_bauart:
            i_bauart = opt.CurrentListOptionIndex
        elif opt.Index == ix_anschlag:
            i_anschlag = opt.CurrentListOptionIndex
        elif opt.Index == ix_rw:
            i_rw = opt.CurrentListOptionIndex
        elif opt.Index == ix_baender:
            i_baender = opt.CurrentListOptionIndex
        elif opt.Index == ix_bandseite:
            i_bandseite = opt.CurrentListOptionIndex
        elif opt.Index == ix_griff:
            i_griff = opt.CurrentListOptionIndex
        elif opt.Index == ix_grifflage:
            i_grifflage = opt.CurrentListOptionIndex
        elif opt.Index == ix_mat:
            i_mat = opt.CurrentListOptionIndex
        elif opt.Index == ix_matrw:
            i_matrw = opt.CurrentListOptionIndex
        elif opt.Index == ix_verb:
            i_verb = opt.CurrentListOptionIndex
        go.Dispose()

    st = {"H": o_H.CurrentValue, "d": o_d.CurrentValue, "bauart": i_bauart,
          "n_tueren": o_tueren.CurrentValue, "anschlag": i_anschlag,
          "td": o_td.CurrentValue, "luft": o_luft.CurrentValue,
          "rueckwand": i_rw, "rw_d": o_rwd.CurrentValue,
          "tiefe_nf": o_nf.CurrentValue, "rw_abstand": o_rwa.CurrentValue,
          "lochreihe": o_loch.CurrentValue,
          "loch_abstand": o_locha.CurrentValue,
          "n_boeden": o_boeden.CurrentValue,
          "baender": i_baender, "band_seite": i_bandseite,
          "topf_kante": o_topfk.CurrentValue,
          "griff": i_griff, "griff_abstand": o_griffa.CurrentValue,
          "griff_lage": i_grifflage,
          "material": mats[i_mat], "material_rw": mats[i_matrw],
          "verbinder": i_verb}
    sc.sticky[STICKY] = dict(st)

    w = {
        "H":            hoehe_vorgabe if hoehe_vorgabe is not None else st["H"],
        "d":            st["d"],
        "bauart":       "S" if st["bauart"] == 0 else "B",
        "n_tueren":     st["n_tueren"],
        "anschlag":     "A" if st["anschlag"] == 0 else "E",
        "td":           st["td"],
        "luft":         st["luft"],
        "rw_art":       "NFK"[st["rueckwand"]],
        "rw_d":         st["rw_d"],
        "tiefe_nf":     st["tiefe_nf"],
        "rw_abstand":   st["rw_abstand"],
        "lochreihe":    st["lochreihe"],
        "loch_abstand": st["loch_abstand"],
        "n_boeden":     st["n_boeden"],
        "band_art":     BAENDER_ARTEN[st["baender"]],
        "band_seite":   "L" if st["band_seite"] == 0 else "R",
        "topf_kante":   st["topf_kante"],
        "griff":        GRIFF_ARTEN[st["griff"]],
        "griff_abstand": st["griff_abstand"],
        "griff_lage":   "O" if st["griff_lage"] == 0 else "U",
        "material":     st["material"],
        "material_rw":  st["material_rw"],
        "verbinder":    VERBINDER_ARTEN[st["verbinder"]],
    }
    pruefe_w(w)
    return w


def main():
    obj = rs.GetObject("Quader (oder Grundriss-Rechteck) des Korpus waehlen",
                       rs.filter.curve | rs.filter.polysurface |
                       rs.filter.extrusion,
                       preselect=True, select=False)
    if not obj:
        return
    try:
        hoehe_vorgabe = None
        if rs.IsCurve(obj):
            geo = korpus_ebene(obj)
            if geo is None:
                return
            ebene, B, T = geo
        else:
            geo = quader_basis(obj)
            if geo is None:
                return
            ebene, B, T, hoehe_vorgabe = geo
        if B <= 0 or T <= 0:
            raise ValueError("Eingabe hat keine gueltige Groesse.")
        w = parameter(B, hoehe_vorgabe)
        if w is None:
            return
        if B <= 2 * w["d"] or T <= w["d"]:
            raise ValueError("Rechteck ist kleiner als das Material.")

        rs.EnableRedraw(False)
        teile, hinweise = bauen(ebene, B, T, w)
    except ValueError as e:
        rs.MessageBox(str(e), 48, "KorpusBau")
        return
    finally:
        rs.EnableRedraw(True)

    rs.UnselectAllObjects()
    if hoehe_vorgabe is not None:
        rs.HideObject(obj)               # Eingabe-Quader steckt sonst im Korpus
        print("  Eingabe-Quader wurde ausgeblendet (Show macht ihn wieder "
              "sichtbar).")
    print("KorpusBau: %d Objekte, Korpus %.0f x %.0f x %.0f mm (B x T x H)."
          % (len(teile), B, T, w["H"]))
    for h in hinweise:
        print("  " + h)

    if not rs.IsAlias("KorpusBau"):
        rs.AddAlias("KorpusBau", '! _-RunPythonScript "KorpusBau"')
        print('Alias "KorpusBau" angelegt.')


if __name__ == "__main__":
    main()
