# -*- coding: utf-8 -*-
"""Minimaler .hop-Writer fuer die HolzHer/HOPS-Steuerung.

Jede Zeile hier hat einen Beleg in 53_post-processor (MASCHINENABGLEICH.md,
reference-hops). Die Bezugsebenen sind die wichtigste Regel:

  Konturen SP     : Z direkt, Bezug TISCH  -> durchtrennen = -0.2
  Bohrung         : Feld 4 = Tiefe; Feld 9 = 0 Oberkante / 1 Tisch
                    (Rollbrett.hop: -13 mit 0, Durchgang -0.2 mit 1)
  Nut             : FRAESER-Bahnen (Flanken, dann Mitte), Z negativ von der
                    OBERKANTE mit Feld 11 = 0 (Praezedenz: Gluecksrad/
                    Rueckwand.hop, gleiches Werkzeug 8); KEIN
                    _nuten_frei_v5 - das Makro will die Nutsaege und lief in
                    der Simulation nicht (Maschine 2026-08-29)
  SP Feld 5       : NICHT die Frässchritte (Dialogfoto: Feld 5 = 1, Anzahl
                    Frässchritte = 0); Feld 11 = Anfahrbewegung,
                    Feld 12 = Radiuskorrektur (1 = links = unsere Seite)
  Kopf ;DX/;DY/;DZ: OFFSETS (immer 0) - die Masse stehen NUR im VARS-Block,
                    gerundet (kein 19.0002 an die Maschine)

Werkzeugaufrufe in der 7-Feld-Form, die JEDE konkrete Referenzdatei nutzt
(57x WZF ...,'1' / 64x WZB ...,'51'). Bohrung Feld 8 ist ein POSITIONSCODE
(Referenzen nutzen 0-8; 2 = Mitte untere Kante!) - fuer Absolutkoordinaten
gilt 0, wie HOPS' eigener DXF-Import schreibt (Audit 2026-09-02).
HorzB: nur der 90-Grad-Fall (Kante y=0) ist durch Muster_DXF_Import.hop
belegt - die anderen Winkel sind plausibel, aber am ersten Teil zu pruefen.
"""
import math

DURCH = -0.2                  # Bezug Tisch: Ueberstand unter der Unterkante

# T13 (absolut oder Wegstrecke) ist PAPIERSEITIG beantwortet
# (MASCHINENABGLEICH 2026-08-31, Audit ueber 215 Dateien): der Bezug
# steht als Feld in der G-Zeile selbst - G01 Feld 5 (Boegen Feld 7/9),
# 0 = absolut, 10 = Wegstrecke. Dieser Writer schreibt 0 und die
# feldgleiche Bogenform der belegt-absoluten Muster_DXF_Import.hop -
# also absolut, und BAHN_ABSOLUT bleibt True. Der Schalter bleibt
# trotzdem stehen, bis die Phase-1b-Abnahme (Freiformkontur laeuft in
# der HOPS-SIMULATION sauber) den Papierbefund an der Maschine
# bestaetigt; bis dahin sperrt holzher_teil.pruefe freie Konturen.
BAHN_ABSOLUT = True

# SP-Feld 11 = Anfahrbewegung, Feld 12 = Radiuskorrektur. Beleg: Foto des
# SP-Dialogs zu `SP (0,0,-0.2,2,1,_ANF,0,0,0,0,1,2,...)` (Maschine
# 2026-08-29) - dort sind genau "linear" und "rechts" angehakt, die zweite
# bzw. dritte Option ihrer Gruppe. LINKS ist fuer unsere Laufrichtung das
# Richtige: Umriss im Uhrzeigersinn -> Werkzeug aussen, Innenausschnitte
# gegen den Uhrzeigersinn -> Werkzeug innen.
KEINE, LINEAR = 0, 1          # Anfahrbewegung
MITTE, LINKS, RECHTS = 0, 1, 2  # Radiuskorrektur


def _n(v):
    s = "%.3f" % float(v)
    s = s.rstrip("0").rstrip(".")
    return s if s not in ("", "-0") else "0"


class HopWriter(object):
    def __init__(self, name, dx, dy, dz):
        self.name = name
        self.dx = round(dx, 1)
        self.dy = round(dy, 1)
        self.dz = round(dz, 1)
        self.zeilen = []

    # -- Bearbeitungen ----------------------------------------------------
    def kontur(self, verts, closed, wz, z=DURCH, schritte=1, rk=LINKS,
               anfahrt=LINEAR):
        """EINE Kontur: WZF + SP + G01/G02M/G03M + EP.

        verts: [(x, y, bulge)] wie aus holzher_kontur.contour_vertices -
        der Bulge gehoert zum Segment, das an diesem Vertex BEGINNT.
        """
        self.zeilen.append("WZF (%d,_VE,_V,_VA,_SD,_ANF,'1')" % wz)
        self._bahn(verts, closed, z, schritte, rk, anfahrt)

    def _bahn(self, verts, closed, z, schritte, rk, anfahrt):
        """SP + Bewegungen + EP OHNE Werkzeugaufruf (Nut faehrt mehrere)."""
        if len(verts) < 2:
            raise ValueError("Kontur mit weniger als 2 Stuetzpunkten - "
                             "Vollkreis nicht zerlegt?")
        if schritte < 1:
            # Ein Test fuhr mit 0 null Durchgaenge - aber im LEO-Korpus ist 0
            # der HAEUFIGSTE Wert (85x) in gelaufenen Dateien, dazu -1/-2.
            # Die Bedeutung von Feld 5 ist offen (Frage 4 im Maschinenplan); bis dahin
            # schreiben wir die belegte 1 und lassen nichts Unbekanntes durch.
            raise ValueError("SP Feld 5 = %d ist ungeklaert - der Writer "
                             "schreibt die belegte 1." % schritte)
        z_ = self.zeilen
        x0, y0 = verts[0][0], verts[0][1]
        z_.append("SP (%s,%s,%s,2,%d,_ANF,0,0,0,0,%d,%d,0,0,0,0,0,0,0,0,0,0,0)"
                  % (_n(x0), _n(y0), _n(z), schritte, anfahrt, rk))
        paare = list(zip(verts, verts[1:]))
        if closed:
            paare.append((verts[-1], verts[0]))
        for (ax, ay, b), (bx, by, _) in paare:
            if abs(b) < 1e-9:
                z_.append("G01 (%s,%s,0,0,0,2)" % (_n(bx), _n(by)))
            else:
                cx, cy = _bogen_zentrum(ax, ay, bx, by, b)
                g = "G03M" if b > 0 else "G02M"   # b>0 = gegen Uhrzeiger
                z_.append("%s (%s,%s,0,%s,%s,0,0,2,0)"
                          % (g, _n(bx), _n(by), _n(cx), _n(cy)))
        z_.append("EP (%d,_ANF,0)" % schritte)

    def format_v5(self, wz):
        """Aussenkontur 'Rechteck auf DX/DY' ueber das belegte Makro.

        Zeile WOERTLICH aus den Referenzen - 17 aktive Aufrufe exakt so,
        z. B. eine lokale Referenzdatei (Seite 2000x400), dort unter
        aktivem WZF: TIEFE:=-0.2 + ESMD:=1 = Bezug Tisch, durchtrennen
        (alle 85 _Format_V5-Aufrufe im LEO-Korpus tragen ESMD:=1,
        MASCHINENABGLEICH). Das Makro traegt KEINE Koordinaten - es
        formatiert auf DX/DY aus VARS und ist damit immun gegen T13.
        DX/DY sind folglich das FERTIGmass, nicht das Rohteil.
        """
        self.zeilen.append("WZF (%d,_VE,_V,_VA,_SD,_ANF,'1')" % wz)
        self.zeilen.append(
            "CALL _Format_V5 ( VAL ECKENVERRUNDUNG:=0,TIEFE:=-0.2,"
            "AUFMASS:=0,ANF:=2.5,ESMD:=1,GGL:=2,LASER:=0,DEPTH:=0)")

    def kreistasche(self, x, y, r, tiefe, wz, durch=False):
        """Runde Tasche/runder Ausschnitt via _Kreistasche_V5, ESXY:=0.

        ESXY:=0 = absolute Mitte in benannten Parametern (3 aktive Belege,
        z. B. leo-design/Linz/Theke/'Steher - Bauteilauswahl.hop') - damit
        immun gegen T13. durch=True schreibt woertlich die Beleg-
        Kombination dieser Datei: TIEFE:=-0.2, ESMD:=1 (Bezug Tisch,
        durchtrennen), AB:=0. Sacktiefe schreibt TIEFE:=-t, ESMD:=0
        (Bezug Oberkante, MASCHINENABGLEICH Bezugsebenen-Tabelle) mit
        AB:=2 wie die aktiven Sack-Aufrufe (z. B. RADIUS:=20.4/2,
        TIEFE:=-12,AB:=2,ESMD:=0 - dort nur mit anderem ESXY).
        """
        if r <= 0:
            raise ValueError("Kreistasche ohne Radius.")
        if durch or tiefe is None:
            if not durch:
                raise ValueError("Kreistasche ohne Tiefe und ohne durch - "
                                 "ein vergessener Wert wuerde still zum "
                                 "Durchschuss.")
            tiefe_s, esmd, ab = "-0.2", 1, 0
        else:
            if abs(tiefe) < 0.01:
                raise ValueError("Kreistaschentiefe 0 schneidet nichts "
                                 "(belegter Stumm-Fall).")
            if abs(tiefe) >= self.dz:
                raise ValueError("Kreistasche %s mm bei Teildicke %s mm - "
                                 "das trennt; 'durch' verwenden."
                                 % (_n(tiefe), _n(self.dz)))
            tiefe_s, esmd, ab = _n(-abs(tiefe)), 0, 2
        self.zeilen.append("WZF (%d,_VE,_V,_VA,_SD,_ANF,'1')" % wz)
        self.zeilen.append(
            "CALL _Kreistasche_V5 ( VAL X_MITTE:=%s,Y_MITTE:=%s,"
            "RADIUS:=%s,TIEFE:=%s,ZUSTELLUNG:=_AT_MAXDEPTH,AB:=%d,"
            "ABF:=_ANF,INTERPOL:=1,UMKEHREN:=0,ESXY:=0,ESMD:=%d,LASER:=0)"
            % (_n(x), _n(y), _n(r), tiefe_s, ab, esmd))

    def bohrungen(self, punkte, wz, topf_mm=()):
        """punkte: [(x, y, d, tiefe_mm_oder_None)] - None = durchgehend.

        Durchgang nach Rollbrett-Muster: Tiefe -0.2 mit Feld 9 = 1 (Tisch).
        Sackloch: -tiefe mit Feld 9 = 0 (Oberkante).
        Feld 5 = Bohrertyp x 10: Standardbohrer 10, TOPFbohrer 30 - sonst
        sucht HOPS einen Typ-1-Bohrer mit D35 und bricht ab ('Bohrer (Type 1)
        mit Durchmesser 35.00 existiert nicht', Maschine 2026-09-03; Beleg
        Arbeitsplatte.hop: alle D35-Zeilen tragen 30).
        """
        if not punkte:
            return
        self.zeilen.append("WZB (%d,_VE,_V,_VA,_SD,_ANF,'51')" % wz)
        for x, y, d, t in punkte:
            if t is not None and abs(t) < 0.01:
                raise ValueError("Bohrtiefe 0 bohrt nichts (belegter "
                                 "Stumm-Fall).")
            f5 = 30 if any(abs(d - tm) < 0.11 for tm in topf_mm) else 10
            if t is None:
                self.zeilen.append(
                    "Bohrung (%s,%s,%s,%s,%d,0,0,0,1,0,0,0)"
                    % (_n(x), _n(y), _n(d), _n(DURCH), f5))
            else:
                self.zeilen.append(
                    "Bohrung (%s,%s,%s,%s,%d,0,0,0,0,0,0,0)"
                    % (_n(x), _n(y), _n(d), _n(-abs(t)), f5))

    def kantenbohrungen(self, punkte, wz=501):
        """punkte: [(pos_entlang_kante, kante, z_hoehe, d, tiefe)] mit
        kante aus 'y0'/'y1'/'x0'/'x1'. Nur y0 (Winkel 90) ist belegt.
        Schreibt einen eigenen WZB-Aufruf - die einzige maschinenbelegte
        HorzB-Referenz steht unter einem aktiven WZB (501,...)."""
        if not punkte:
            return
        self.zeilen.append("WZB (%d,_VE,_V,_VA,_SD,_ANF,'51')" % wz)
        winkel = {"y0": (90, lambda p: (p, 0.0)),
                  "y1": (270, lambda p: (p, self.dy)),
                  "x0": (0, lambda p: (0.0, p)),
                  "x1": (180, lambda p: (self.dx, p))}
        for pos, kante, zh, d, t in punkte:
            w, xy = winkel[kante]
            x, y = xy(pos)
            self.zeilen.append(
                "HorzB (%s,%s,%s,%s,%s,0,0,%d,0,2,0,0)"
                % (_n(x), _n(y), _n(zh), _n(d), _n(-abs(t)), w))

    def nut(self, x1, y1, x2, y2, breite, tiefe, wz, d_wz):
        """Nut als FRAESER-Durchgaenge, nicht als Makro.

        Beleg 2026-08-29 (Maschine): `_nuten_frei_v5` stand im Baum und der
        Dialog zeigte die Werte richtig an, die Simulation fuhr aber nichts
        - Statuszeile "Saegeblattbreite oder Werkzeug nicht definiert", und
        das Dialogbild zeigt ein Saegeblatt. Das Makro will die Nutsaege.
        Die Werkstatt nutet mit dem Fraeser (4 mm): erst die beiden Flanken,
        dann die Mitte. Genau das schreibt diese Methode.

        x1/y1 -> x2/y2 ist die MITTE der Nut, `tiefe` misst von OBEN und wird
        auch so geschrieben (negativ). Beleg ist der direkte Praezedenzfall
        eine lokale Referenzdatei (Rueckwand): gleiches Werkzeug (WZF 8), gleiche
        freie SP/G01/EP-Nutbahn, DZ 19, `SP (83,0,-10,0,0,_ANF,...,0,2,2,...)`
        - Tiefe von oben, Feld 11 = 0. Die Kombination Feld 11 = 0 mit
        POSITIVER Z kommt in 722 Korpuszeilen null mal vor (Audit 2026-08-29);
        Feld 11 = 0 und tiefe negative Z gehoeren zusammen.
        Abweichung vom Praezedenzfall: dort Feld 12 = 2, hier 0 - der
        Praezedenzfall programmiert die Nutkante, wir die Bahnmitte.
        """
        if d_wz <= 0:
            raise ValueError("Nutfraeser ohne Durchmesser.")
        if breite < d_wz - 1e-6:
            raise ValueError("Nut %s mm ist schmaler als der Fraeser %s mm."
                             % (_n(breite), _n(d_wz)))
        if abs(tiefe) >= self.dz:
            raise ValueError("Nut %s mm bei Teildicke %s mm - das trennt, "
                             "statt zu nuten." % (_n(tiefe), _n(self.dz)))
        z = -abs(tiefe)
        laenge = math.hypot(x2 - x1, y2 - y1)
        if laenge < 1e-6:
            raise ValueError("Nutlinie ohne Laenge.")
        px, py = -(y2 - y1) / laenge, (x2 - x1) / laenge   # quer zur Nut
        spanne = breite - d_wz                 # Weg zwischen den Flankenbahnen
        if spanne < 1e-6:
            versatz = [0.0]
        else:
            n = int(math.ceil(spanne / d_wz)) + 1
            schritt = spanne / (n - 1)
            bahnen = [-spanne / 2.0 + i * schritt for i in range(n)]
            # Reihenfolge wie von Hand: erst beide Flanken, dann die Mitte
            versatz = [bahnen[0], bahnen[-1]] + bahnen[1:-1]
        self.zeilen.append("WZF (%d,_VE,_V,_VA,_SD,_ANF,'1')" % wz)
        for v in versatz:
            # Bahnmitte = Nutmitte + Versatz; Fraeser laeuft ohne Korrektur
            # und ohne Anfahrbogen, sonst liefe er aus der Nut heraus.
            self._bahn([(x1 + v * px, y1 + v * py, 0.0),
                        (x2 + v * px, y2 + v * py, 0.0)],
                       False, z, 1, MITTE, KEINE)

    # -- Datei ------------------------------------------------------------
    def text(self):
        kopf = [
            ";MAKROTYP=0", ";INSTVERSION=", ";EXEVERSION=",
            ";BILD=", ";INFO=", ";WZGV=7535D_250", ";WZGVCONFIG=",
            ";MASCHINE=HOLZHER", ";NCNAME=%s" % self.name, ";KOMMENTAR=",
            ";DX=0", ";DY=0", ";DZ=0",
            ";DIALOGDLL=Dialoge.Dll", ";DIALOGPROC=StandardFormAnzeigen",
            ";DIALOGKIND=0", ";AUTOSCRIPTSTART=1", ";BUTTONBILD=",
            ";DIMENSION_UNIT=0",
            "VARS",
            "   DX := %s;*VAR*Breite X" % _n(self.dx),
            "   DY := %s;*VAR*Breite Y" % _n(self.dy),
            "   DZ := %s;*VAR*Dicke Z" % _n(self.dz),
            "START",
            "Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)",
            "CALL Park_V7 ( VAL MODE:=2,POSX:=0,POSY:=0)",
        ]
        return "\r\n".join(kopf + self.zeilen) + "\r\n"

    def save(self, path):
        f = open(path, "wb")
        try:
            f.write(self.text().encode("cp1252", "replace"))
        finally:
            f.close()


def _bogen_zentrum(ax, ay, bx, by, bulge):
    """Kreismittelpunkt aus Bulge: h = (Sehne/2)*(1-b^2)/(2b), links der
    Sehne bei b>0 (gegen Uhrzeiger)."""
    mx, my = (ax + bx) / 2.0, (ay + by) / 2.0
    dx, dy = bx - ax, by - ay
    sehne = math.hypot(dx, dy)
    if sehne < 1e-12:
        return mx, my
    h = (sehne / 2.0) * (1.0 - bulge * bulge) / (2.0 * bulge)
    nx, ny = -dy / sehne, dx / sehne          # links von a->b
    return mx + nx * h, my + ny * h
