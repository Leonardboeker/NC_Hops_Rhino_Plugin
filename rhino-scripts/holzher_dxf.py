# -*- coding: utf-8 -*-
"""Minimaler DXF-R12-Writer fuer den HOPS-Layer-Import.

R12 (AC1009) bewusst gewaehlt: keine Handles, keine Subclass-Marker, keine
BLOCK_RECORD-Tabelle - und Gruppencode 39 (Objekthoehe/Tiefe) sowie 62 (ACI-
Farbe) sind voll unterstuetzt. Genau das, was HOPS aus dem Layernamen und der
Objekthoehe liest.

Laeuft in IronPython 2.7 (Rhino) und CPython - keine Abhaengigkeiten.
"""

# ACI-Farbindizes, die HolzHer auswertet
ROT = 1       # Fraeskontur
GELB = 2      # Startpunkt, Nut
BLAU = 5      # Bohrung
MAGENTA = 6   # Tasche / Ausschnitt
WEISS = 7     # Grundplatte (Rohteil)


def _n(v):
    """DXF-Zahl: Punkt als Dezimaltrenner, keine Exponentialschreibweise."""
    return "%.6f" % float(v)


# In DXF R12 sind Layernamen auf diese Zeichen begrenzt (max. 31). Rhino
# liefert bei Unterlayern den vollen Pfad ("Teil 01::Kontur") und erlaubt
# Leerzeichen und Umlaute - das muss vor dem Schreiben bereinigt werden.
ERLAUBT = ("ABCDEFGHIJKLMNOPQRSTUVWXYZ"
           "abcdefghijklmnopqrstuvwxyz0123456789_$-")
MAX_LAYER = 31


class DxfR12(object):
    def __init__(self):
        self._layers = {}
        self._safe = {}          # Originalname -> bereinigter Name (Memo)
        self._ents = []
        self._min = [1e18, 1e18]
        self._max = [-1e18, -1e18]
        self.layer("0", WEISS)

    # -- intern -----------------------------------------------------------
    def _pair(self, code, value):
        self._ents.append("%d\n%s" % (code, value))

    def _touch(self, x, y):
        self._min[0] = min(self._min[0], x)
        self._min[1] = min(self._min[1], y)
        self._max[0] = max(self._max[0], x)
        self._max[1] = max(self._max[1], y)

    def _head(self, kind, layer, color, thickness):
        lay = self.layer(layer)
        self._pair(0, kind)
        self._pair(8, lay)
        if color is not None:
            self._pair(62, color)
        if thickness:
            self._pair(39, _n(thickness))
        return lay

    # -- oeffentlich ------------------------------------------------------
    def layer(self, name, color=WEISS):
        """Registriert den Layer und gibt den DXF-tauglichen Namen zurueck.

        Gemerkt wird pro Originalname, damit derselbe Layer bei jedem Aufruf
        denselben Zielnamen bekommt - sonst wuerde aus '20_TT$20$' bei jeder
        Entity ein neuer Layer.
        """
        if name in self._safe:
            return self._safe[name]
        s = "".join(c if c in ERLAUBT else "_" for c in name)[:MAX_LAYER]
        if not s:
            s = "LAYER"
        if s in self._layers:        # anderer Originalname, gleicher Zielname
            n, basis = 1, s[:MAX_LAYER - 3]
            while "%s_%d" % (basis, n) in self._layers:
                n += 1
            s = "%s_%d" % (basis, n)
        self._safe[name] = s
        self._layers[s] = color
        return s

    def extents(self, x0, y0, x1, y1):
        """$EXTMIN/$EXTMAX zusaetzlich aufziehen.

        Noetig, weil _touch() nur die Stuetzpunkte kennt - ein Bulge-Bogen
        laeuft ueber seine Sehne hinaus. Ein Halbkreis zwischen zwei
        Stuetzpunkten wuerde die Datei sonst als 0 mm hoch ausweisen.
        """
        self._touch(x0, y0)
        self._touch(x1, y1)

    def line(self, layer, x1, y1, x2, y2, color=ROT, thickness=0):
        self._head("LINE", layer, color, thickness)
        self._pair(10, _n(x1)); self._pair(20, _n(y1)); self._pair(30, "0.0")
        self._pair(11, _n(x2)); self._pair(21, _n(y2)); self._pair(31, "0.0")
        self._touch(x1, y1); self._touch(x2, y2)

    def arc(self, layer, cx, cy, r, ang_start, ang_end, color=ROT, thickness=0):
        """Winkel in Grad, Bogen laeuft IMMER gegen den Uhrzeigersinn."""
        self._head("ARC", layer, color, thickness)
        self._pair(10, _n(cx)); self._pair(20, _n(cy)); self._pair(30, "0.0")
        self._pair(40, _n(r))
        self._pair(50, _n(ang_start % 360.0)); self._pair(51, _n(ang_end % 360.0))
        self._touch(cx - r, cy - r); self._touch(cx + r, cy + r)

    def circle(self, layer, cx, cy, r, color=BLAU, thickness=0):
        self._head("CIRCLE", layer, color, thickness)
        self._pair(10, _n(cx)); self._pair(20, _n(cy)); self._pair(30, "0.0")
        self._pair(40, _n(r))
        self._touch(cx - r, cy - r); self._touch(cx + r, cy + r)

    def polyline(self, layer, pts, closed=True, color=WEISS, thickness=0):
        """R12-Polylinie: POLYLINE + VERTEX* + SEQEND.

        pts: (x, y) oder (x, y, bulge). Der Bulge gehoert zum Segment VON
        diesem Vertex zum naechsten: bulge = tan(Bogenwinkel/4), positiv =
        gegen den Uhrzeigersinn, 0 = Gerade.
        """
        lay = self._head("POLYLINE", layer, color, thickness)
        self._pair(66, "1")            # Vertices folgen
        self._pair(10, "0.0"); self._pair(20, "0.0"); self._pair(30, "0.0")
        self._pair(70, "1" if closed else "0")
        for p in pts:
            x, y = p[0], p[1]
            bulge = p[2] if len(p) > 2 else 0.0
            self._pair(0, "VERTEX")
            self._pair(8, lay)
            self._pair(10, _n(x)); self._pair(20, _n(y)); self._pair(30, "0.0")
            if bulge:
                self._pair(42, _n(bulge))
            self._touch(x, y)
        self._pair(0, "SEQEND")
        self._pair(8, lay)

    # -- Ausgabe ----------------------------------------------------------
    def dumps(self):
        if self._min[0] > 1e17:          # nichts gezeichnet
            self._min = [0.0, 0.0]
            self._max = [0.0, 0.0]
        o = []
        a = o.append
        a("0\nSECTION\n2\nHEADER")
        a("9\n$ACADVER\n1\nAC1009")
        a("9\n$INSBASE\n10\n0.0\n20\n0.0\n30\n0.0")
        a("9\n$EXTMIN\n10\n%s\n20\n%s\n30\n0.0" % (_n(self._min[0]), _n(self._min[1])))
        a("9\n$EXTMAX\n10\n%s\n20\n%s\n30\n0.0" % (_n(self._max[0]), _n(self._max[1])))
        a("9\n$LUNITS\n70\n2")
        a("9\n$INSUNITS\n70\n4")         # Millimeter
        a("0\nENDSEC")

        a("0\nSECTION\n2\nTABLES")
        a("0\nTABLE\n2\nLTYPE\n70\n1")
        a("0\nLTYPE\n2\nCONTINUOUS\n70\n0\n3\nSolid line\n72\n65\n73\n0\n40\n0.0")
        a("0\nENDTAB")
        a("0\nTABLE\n2\nLAYER\n70\n%d" % len(self._layers))
        for name in sorted(self._layers):
            a("0\nLAYER\n2\n%s\n70\n0\n62\n%d\n6\nCONTINUOUS"
              % (name, self._layers[name]))
        a("0\nENDTAB")
        a("0\nENDSEC")

        a("0\nSECTION\n2\nENTITIES")
        o.extend(self._ents)
        a("0\nENDSEC")
        a("0\nEOF")
        return "\n".join(o) + "\n"

    def save(self, path):
        f = open(path, "w")
        try:
            f.write(self.dumps())
        finally:
            f.close()
