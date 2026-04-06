using System;
using System.Collections.Generic;
using System.Globalization;

namespace DynesticPostProcessor
{
    internal static class NcDrill
    {
        internal static string ToolCall(int toolNr)
        {
            return "WZB (" + toolNr.ToString() + ",_VE,_V*1,_VA,_SD,0,'')";
        }

        internal static string BohrungLine(double x, double y, double surfaceZ, double cutZ, double diameter)
        {
            return "Bohrung ("
                + x.ToString(CultureInfo.InvariantCulture) + ","
                + y.ToString(CultureInfo.InvariantCulture) + ","
                + surfaceZ.ToString(CultureInfo.InvariantCulture) + ","
                + cutZ.ToString(CultureInfo.InvariantCulture) + ","
                + diameter.ToString(CultureInfo.InvariantCulture)
                + ",0,0,0,0,0,0,0)";
        }
    }

    internal static class NcSaw
    {
        internal static string ToolCall(int toolNr)
        {
            return "WZS (" + toolNr.ToString() + ",_VE,_V*0.3,_VA,_SD,0,'')";
        }

        internal static string NutenFreiLine(double x1, double y1, double x2, double y2,
            double nb, double tiefe, double lage)
        {
            return "CALL _nuten_frei_v5(VAL "
                + "X1:=" + x1.ToString(CultureInfo.InvariantCulture) + ","
                + "Y1:=" + y1.ToString(CultureInfo.InvariantCulture) + ","
                + "X2:=" + x2.ToString(CultureInfo.InvariantCulture) + ","
                + "Y2:=" + y2.ToString(CultureInfo.InvariantCulture) + ","
                + "NB:=" + nb.ToString(CultureInfo.InvariantCulture) + ","
                + "Tiefe:=" + tiefe.ToString(CultureInfo.InvariantCulture) + ","
                + "LAGE:=" + lage.ToString(CultureInfo.InvariantCulture)
                + ",RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)";
        }
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
            lines.Add(";MASCHINE=HOLZHER");
            lines.Add(";NCNAME=" + ncName);
            lines.Add(";KOMMENTAR=");
            lines.Add(";DX=" + dx.ToString("F3", CultureInfo.InvariantCulture));
            lines.Add(";DY=" + dy.ToString("F3", CultureInfo.InvariantCulture));
            lines.Add(";DZ=" + dz.ToString("F3", CultureInfo.InvariantCulture));
            lines.Add(";DIALOGDLL=Dialoge.Dll");
            lines.Add(";DIALOGPROC=StandardFormAnzeigen");
            lines.Add(";AUTOSCRIPTSTART=1");
            lines.Add(";BUTTONBILD=");
            lines.Add(";DIMENSION_UNIT=0");
            return lines;
        }

        internal static List<string> SortOperationLines(List<string> lines)
        {
            var pairs = new List<KeyValuePair<int, List<string>>>();
            int i = 0;
            while (i < lines.Count)
            {
                string line = lines[i];
                int order = 99;
                if (line.StartsWith("WZB")) order = 0;
                else if (line.StartsWith("WZF")) order = 1;
                else if (line.StartsWith("WZS")) order = 2;

                var pair = new List<string>();
                pair.Add(line);
                if (i + 1 < lines.Count) pair.Add(lines[i + 1]);
                pairs.Add(new KeyValuePair<int, List<string>>(order, pair));
                i += pair.Count;
            }

            pairs.Sort((a, b) => a.Key.CompareTo(b.Key));

            var result = new List<string>();
            foreach (var p in pairs) result.AddRange(p.Value);
            return result;
        }
    }
}
