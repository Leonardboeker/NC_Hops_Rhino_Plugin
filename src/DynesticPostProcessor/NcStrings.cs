using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WallabyHop
{
    internal static class NcDrill
    {
        internal static string ToolCall(int toolNr)
        {
            return "WZB (" + toolNr.ToString() + ",_VE,_V*1,_VA,_SD,0,'')";
        }

        /// <summary>
        /// Bohrung (X, Y, DIAMETER, DEPTH, 10, ...) — parameter order read off the
        /// machine files on 2026-08-10, e.g. "Bohrung (0,25,8,-16,10,0,0,2,0,0,0,0)"
        /// = an 8 mm bit, 16 mm deep. The emitter previously wrote
        /// (X, Y, surfaceZ, cutZ, diameter), so HOPS read the surface Z as the bit
        /// diameter and rejected every line with "Bohrungsdurchmesser ist nicht
        /// korrekt". DEPTH is top-relative and negative, like the mill macros.
        /// The 5th field is the HOPS drill-type code (10 blind hole, 20 through,
        /// 30 hinge cup — MASCHINENABGLEICH.md); it is NOT the tool number. Cup
        /// diameters get 30: with 10, HOPS looks for a type-1 bit of that diameter
        /// and stops ("Bohrer (Type 1) mit Durchmesser 35.00 existiert nicht"); all
        /// D35 reference lines carry 30, and the tool table has Topf15/Topf35.
        /// </summary>
        /// <param name="depthFromTable">
        /// The 9th field is the depth reference, the drill's counterpart to ESMD on
        /// the mill macros: 0 = depth counts from the part TOP, 1 = the value is the
        /// Z above the TABLE. Reference files (731 lines): 0 is always negative,
        /// -0.2 to -22, where -0.2 is a 0.2 mm marking dimple; 1 is -0.2/-0.3/-0.4
        /// for through holes and +10 for a stepped first pass that leaves material.
        /// </param>
        internal static string DrillLine(double x, double y, double diameter, double depth,
            bool depthFromTable = false)
        {
            return "Bohrung ("
                + NcFmt.F(x) + ","
                + NcFmt.F(y) + ","
                + NcFmt.Dim(diameter) + ","
                + NcFmt.F(-Math.Abs(depth)) + ","
                + (IsCupDiameter(diameter) ? "30" : "10")
                + ",0,0,0,"
                + (depthFromTable ? "1" : "0")
                + ",0,0,0)";
        }

        // Topf15/Topf35 = DrillingType 3 in the tool table; same sizes and
        // tolerance as rhino-scripts/holzher_hop.py (werkzeuge.json topf_mm).
        private static readonly double[] _cupDiametersMm = { 15, 35 };

        internal static bool IsCupDiameter(double diameter) =>
            Array.Exists(_cupDiametersMm, cup => Math.Abs(diameter - cup) < 0.11);

        // Field 5 codes of the HOPS drill-type dropdown: standard, blind, through,
        // hinge cup, countersink. Anything else is no machine line (e.g. a diameter
        // in the old X, Y, surfZ, cutZ, DIA order).
        private static readonly int[] _bitTypes = { 0, 10, 20, 30, 40 };

        /// <summary>One Bohrung line read back — the fields DrillLine writes.</summary>
        internal struct DrillSpec
        {
            // Numeric X/Y at an absolute position (field 8 = 0 or 1). Machine files
            // mostly use expressions (PM, DX-37), and codes 2-9 anchor the same X/Y
            // to another corner or edge -- neither is a point in the part frame.
            public bool HasXY;
            public double X, Y;
            public double Diameter;      // field 3
            public double Depth;         // field 4, signed as written
            public int BitType;          // field 5, drill-type code
            public int Pos;              // field 8, position/anchor code (-1 = no number)
            public bool DepthFromTable;  // field 9

            /// <summary>Hole bottom in the part frame (table Z=0, top Z=dz). From the
            /// table the depth field IS that Z: -0.2 = through, 0.2 mm into the
            /// spoilboard; +10 = 10 mm of material left (seen in the reference files).</summary>
            public double BottomZ(double dz) => DepthFromTable ? Depth : dz - Math.Abs(Depth);
        }

        /// <summary>Reads "[/]Bohrung (X,Y,DIA,DEPTH,TYPE,_,_,POS,REF,…)". False when it
        /// is no Bohrung line, diameter/depth/type/reference are not plain numbers, the
        /// type is no HOPS drill-type code or the diameter is not positive.</summary>
        internal static bool TryParse(string line, out DrillSpec d)
        {
            d = new DrillSpec();
            string t = (line ?? "").Trim().TrimStart('/').TrimStart();
            int o = t.IndexOf('('), c = t.LastIndexOf(')');
            if (!t.StartsWith("Bohrung") || o < 0 || c <= o) return false;
            string[] p = t.Substring(o + 1, c - o - 1).Split(',');
            if (p.Length < 9
                || !Num(p[2], out d.Diameter) || !Num(p[3], out d.Depth)
                || !int.TryParse(p[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out d.BitType)
                || !Num(p[8], out double reference)
                || Array.IndexOf(_bitTypes, d.BitType) < 0 || !(d.Diameter > 0))
                return false;
            d.DepthFromTable = reference == 1;
            if (!int.TryParse(p[7].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out d.Pos))
                d.Pos = -1;
            d.HasXY = Num(p[0], out d.X) && Num(p[1], out d.Y) && (d.Pos == 0 || d.Pos == 1);
            return true;
        }

        private static bool Num(string s, out double v) =>
            double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }

    internal static class NcSaw
    {
        internal static string ToolCall(int toolNr)
        {
            return "WZS (" + toolNr.ToString() + ",_VE,_V*0.3,_VA,_SD,0,'')";
        }

        internal static string FreeSlotLine(double x1, double y1, double x2, double y2,
            double slotWidth, double depth, double bladeAngle)
        {
            return "CALL _nuten_frei_v5(VAL "
                + "X1:=" + NcFmt.F(x1) + ","
                + "Y1:=" + NcFmt.F(y1) + ","
                + "X2:=" + NcFmt.F(x2) + ","
                + "Y2:=" + NcFmt.F(y2) + ","
                + "NB:=" + NcFmt.F(slotWidth) + ","
                + "Tiefe:=" + NcFmt.F(depth) + ","
                + "LAGE:=" + NcFmt.F(bladeAngle)
                + ",RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)";
        }
    }

    internal static class NcFmt
    {
        internal static string F(double v) =>
            Math.Round(v, 4).ToString(CultureInfo.InvariantCulture);

        // Part DIMENSIONS get 2 decimals. Coordinates keep 4 (F above), but a
        // dimension like 19.0002 or 1470.0002 is bounding-box measurement noise,
        // not a size -- it must not reach the machine as the plate thickness.
        internal static string Dim(double v) =>
            Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
    }

    internal static class NcExport
    {
        internal static List<string> BuildHeader(string ncName, double dx, double dy, double dz, string wzgv)
        {
            var lines = new List<string>();
            lines.Add(";MAKROTYP=0");
            lines.Add(";INSTVERSION=");
            lines.Add(";EXEVERSION=");
            lines.Add(";BILD=");
            lines.Add(";INFO=");
            if (!string.IsNullOrEmpty(wzgv))
                lines.Add(";WZGV=" + wzgv);
            lines.Add(";WZGVCONFIG=");
            lines.Add(";MASCHINE=" + MachineConstants.HeaderMachineId);
            lines.Add(";NCNAME=" + ncName);
            lines.Add(";KOMMENTAR=");
            // Header ;DX/;DY/;DZ are OFFSETS, not dimensions. Every machine-made
            // file under reference-hops/ carries 0.000/0.000/0 here (no exception
            // in ~200 files), as do Muster_DXF_Import.hop and the two nesting
            // exporters in this repo. The real dimensions live only in the VARS
            // block. HopAnalyzer reads header ;DZ solely as a fallback when VARS
            // has no DZ -- AssembleFile always writes one, so nothing breaks.
            lines.Add(";DX=0.000");
            lines.Add(";DY=0.000");
            lines.Add(";DZ=0");
            lines.Add(";DIALOGDLL=" + MachineConstants.HeaderDialogDll);
            lines.Add(";DIALOGPROC=" + MachineConstants.HeaderDialogProc);
            lines.Add(";AUTOSCRIPTSTART=1");
            lines.Add(";BUTTONBILD=");
            lines.Add(";DIMENSION_UNIT=0");
            return lines;
        }

        /// <summary>
        /// Assembles a complete .hop file: header + VARS + START + sorted
        /// operation lines, CRLF-joined. HopExport and HopJob route through it
        /// so golden-file tests cover their output byte-for-byte; HopSheetExport
        /// and HopPartExport share its START section via StartSection.
        /// </summary>
        internal static string AssembleFile(
            string ncName, double dx, double dy, double dz, string wzgv,
            IReadOnlyList<string> labelVars, List<string> operationLines)
        {
            var lines = BuildHeader(ncName, dx, dy, dz, wzgv);

            lines.Add("VARS");
            lines.Add("   DX := " + NcFmt.Dim(dx) + ";*VAR*Dimension X");
            lines.Add("   DY := " + NcFmt.Dim(dy) + ";*VAR*Dimension Y");
            lines.Add("   DZ := " + NcFmt.Dim(dz) + ";*VAR*Dimension Z");
            if (labelVars != null)
                foreach (string lv in labelVars)
                    if (!string.IsNullOrWhiteSpace(lv))
                        lines.Add(lv);

            var ops = new List<string>(operationLines ?? new List<string>());
            lines.AddRange(StartSection(ops));
            lines.AddRange(SortOperationLines(ops));

            return string.Join("\r\n", lines) + "\r\n";
        }

        // START head macros in machine order (124 of 127 nesting files; the
        // other three only add a Keep_Part_V7 or a leading HLineX).
        private static readonly string[] _startMacros =
            { "Park_V7", "HH_MarkLabel", "BN_TrennerInnenAussen", "BN_NestKontur" };
        private static readonly Regex _callNameRx = new Regex(@"^CALL\s+(\w+)");

        private static string MacroName(string line)
        {
            var m = _callNameRx.Match(line ?? "");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>True for a park, label or nest macro that belongs in the START head.</summary>
        internal static bool IsStartMacro(string line) =>
            Array.IndexOf(_startMacros, MacroName(line)) >= 0;

        /// <summary>
        /// START section head as in the machine files: START, Fertigteil, exactly
        /// one Park_V7, then HH_MarkLabel, BN_TrennerInnenAussen, BN_NestKontur.
        /// Removes those macros from <paramref name="ops"/> wherever they were
        /// wired, so they never land between tool blocks or twice; a Park_V7
        /// from HopNesting replaces the default park.
        /// </summary>
        internal static List<string> StartSection(List<string> ops)
        {
            var head = new List<string> { "START", "Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)" };
            foreach (string macro in _startMacros)
            {
                string hit = ops.Find(l => MacroName(l) == macro);
                if (hit != null) head.Add(hit);
                else if (macro == "Park_V7") head.Add(ParkLine());
            }
            ops.RemoveAll(IsStartMacro);
            return head;
        }

        /// <summary>
        /// Park call of the START section, spelled as in the machine files.
        /// Corpus (MASCHINENABGLEICH): Park_V7 in 151 of 214 files, MODE:=2 in
        /// 132 of them incl. all 127 nesting files; HH_Park in none. What the
        /// MODE values mean is still open (open question 7 in the machine plan).
        /// </summary>
        internal static string ParkLine(int mode = 2) =>
            "CALL Park_V7 ( VAL MODE:=" + mode.ToString(CultureInfo.InvariantCulture)
            + ",POSX:=0,POSY:=0)";

        /// <summary>
        /// HopNesting's system block, spelled as in all 127 nesting machine files.
        /// StartSection moves it behind Fertigteil wherever it is wired.
        /// A label position of 0 keeps the machine default (_hhdata_LabelPos*).
        /// </summary>
        internal static List<string> NestingBlock(int parkMode, double labelX, double labelY,
            double labelAngle, bool includeLabel)
        {
            var lines = new List<string> { ParkLine(parkMode) };
            if (includeLabel)
                lines.Add("CALL HH_MarkLabel ( VAL POSX:="
                    + (labelX != 0.0 ? NcFmt.F(labelX) : "_hhdata_LabelPosX")
                    + ",POSY:=" + (labelY != 0.0 ? NcFmt.F(labelY) : "_hhdata_LabelPosY")
                    + ",ANGLE:=" + NcFmt.F(labelAngle) + ",LASER:=0)");
            lines.Add("CALL BN_TrennerInnenAussen ()");
            lines.Add("CALL BN_NestKontur ()");
            return lines;
        }

        internal static List<string> SortOperationLines(List<string> lines)
        {
            // Group lines into operation blocks: each block starts at a WZx line
            // and contains every following line until the next WZx line.
            // Buckets: WZB=0, WZF=1, WZS=2, others=3.
            // Relative order within each bucket is preserved (stable).
            var buckets = new List<List<string>>[4];
            for (int b = 0; b < 4; b++) buckets[b] = new List<List<string>>();

            List<string> current = null;
            int currentBucket = 3;

            foreach (string line in lines)
            {
                int bucket = -1;
                if      (line.StartsWith("WZB")) bucket = 0;
                else if (line.StartsWith("WZF")) bucket = 1;
                else if (line.StartsWith("WZS")) bucket = 2;

                if (bucket >= 0)
                {
                    if (current != null) buckets[currentBucket].Add(current);
                    current = new List<string>();
                    currentBucket = bucket;
                }
                else if (current == null)
                {
                    current = new List<string>();
                    currentBucket = 3;
                }
                current.Add(line);
            }
            if (current != null && current.Count > 0)
                buckets[currentBucket].Add(current);

            // Merge blocks that share the same WZx tool-call line (e.g. four HopSaw
            // components with tool 10 → one WZS block with all their operations).
            for (int b = 0; b < 4; b++)
            {
                var merged = new List<List<string>>();
                var toolToIndex = new Dictionary<string, int>();
                foreach (var block in buckets[b])
                {
                    if (block.Count == 0) continue;
                    string toolLine = block[0];
                    int idx;
                    if (toolToIndex.TryGetValue(toolLine, out idx))
                    {
                        for (int j = 1; j < block.Count; j++)
                            merged[idx].Add(block[j]);
                    }
                    else
                    {
                        toolToIndex[toolLine] = merged.Count;
                        merged.Add(new List<string>(block));
                    }
                }
                buckets[b] = merged;
            }

            var result = new List<string>();
            foreach (var bucket in buckets)
                foreach (var block in bucket)
                    result.AddRange(block);
            return result;
        }
    }
}
