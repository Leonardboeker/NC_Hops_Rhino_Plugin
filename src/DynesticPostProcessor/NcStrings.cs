using System;
using System.Collections.Generic;
using System.Globalization;

namespace WallabyHop
{
    internal static class NcDrill
    {
        internal static string ToolCall(int toolNr)
        {
            return "WZB (" + toolNr.ToString() + ",_VE,_V*1,_VA,_SD,0,'')";
        }

        internal static string DrillLine(double x, double y, double surfaceZ, double cutZ, double diameter)
        {
            return "Bohrung ("
                + NcFmt.F(x) + ","
                + NcFmt.F(y) + ","
                + NcFmt.F(surfaceZ) + ","
                + NcFmt.F(cutZ) + ","
                + NcFmt.F(diameter)
                + ",0,0,0,0,0,0,0)";
        }
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
            lines.Add(";DX=" + dx.ToString("F3", CultureInfo.InvariantCulture));
            lines.Add(";DY=" + dy.ToString("F3", CultureInfo.InvariantCulture));
            lines.Add(";DZ=" + dz.ToString("F3", CultureInfo.InvariantCulture));
            lines.Add(";DIALOGDLL=" + MachineConstants.HeaderDialogDll);
            lines.Add(";DIALOGPROC=" + MachineConstants.HeaderDialogProc);
            lines.Add(";AUTOSCRIPTSTART=1");
            lines.Add(";BUTTONBILD=");
            lines.Add(";DIMENSION_UNIT=0");
            return lines;
        }

        /// <summary>
        /// Assembles a complete .hop file: header + VARS + START + sorted
        /// operation lines, CRLF-joined. This is THE file format — every
        /// exporter routes through it so golden-file tests cover the real
        /// output byte-for-byte.
        /// </summary>
        internal static string AssembleFile(
            string ncName, double dx, double dy, double dz, string wzgv,
            IReadOnlyList<string> labelVars, List<string> operationLines)
        {
            var lines = BuildHeader(ncName, dx, dy, dz, wzgv);

            lines.Add("VARS");
            lines.Add("   DX := " + NcFmt.F(dx) + ";*VAR*Dimension X");
            lines.Add("   DY := " + NcFmt.F(dy) + ";*VAR*Dimension Y");
            lines.Add("   DZ := " + NcFmt.F(dz) + ";*VAR*Dimension Z");
            if (labelVars != null)
                foreach (string lv in labelVars)
                    if (!string.IsNullOrWhiteSpace(lv))
                        lines.Add(lv);

            lines.Add("START");
            lines.Add("Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)");
            lines.Add("CALL HH_Park ( VAL PARK:=3,X:=0,Y:=0)");

            foreach (string line in SortOperationLines(operationLines ?? new List<string>()))
                lines.Add(line);

            return string.Join("\r\n", lines) + "\r\n";
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
