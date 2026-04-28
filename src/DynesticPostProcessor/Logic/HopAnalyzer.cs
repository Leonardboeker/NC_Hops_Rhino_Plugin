using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure structural analyzer for assembled .hop content. Validates
    /// SP/EP pairing, detects empty blocks, accumulates stats (drill
    /// counts, path length, tool changes), and warns when machined
    /// depth exceeds the plate thickness declared in the ;DZ= header.
    ///
    /// No Grasshopper or Rhino dependency — fully testable.
    /// </summary>
    internal static class HopAnalyzer
    {
        internal const double SpoilboardAllowance = 5.0;

        internal struct AnalysisResult
        {
            public bool IsValid;
            public int ErrorCount;
            public List<string> Errors;
            public List<string> ZWarnings;
            public string Summary;
            public List<string> Stats;

            // Raw counters (exposed for testing)
            public int SpCount;
            public int EpCount;
            public int MoveCount;
            public int EmptyBlocks;
            public int DrillCount;
            public int CallCount;
            public int ToolChangeCount;
            public double PathLength;
            public double DeepestZ;
            public double HeaderDZ;
        }

        internal static AnalysisResult Analyze(string hopContent)
        {
            var lines = (hopContent ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            var spStack       = new Stack<int>();
            var errors        = new List<string>();
            var zWarnings     = new List<string>();
            var toolCalls     = new HashSet<string>();
            int spCount       = 0;
            int epCount       = 0;
            int moveCount     = 0;
            int emptyBlocks   = 0;
            int movesInBlock  = 0;
            int drillCount    = 0;
            int callCount     = 0;
            double pathLength = 0.0;
            double prevX      = double.NaN;
            double prevY      = double.NaN;
            double deepestZ   = double.MaxValue;

            // Pass 1 — extract DZ from header (";DZ=19.000")
            double headerDZ = 0;
            foreach (string raw in lines)
            {
                string h = (raw ?? "").Trim();
                if (h.StartsWith(";DZ="))
                {
                    double.TryParse(h.Substring(4), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out headerDZ);
                    break;
                }
            }

            // Pass 2 — analyze
            for (int i = 0; i < lines.Length; i++)
            {
                string s = (lines[i] ?? "").Trim();
                int lineNum = i + 1;

                if (s.StartsWith("SP "))
                {
                    spStack.Push(lineNum);
                    spCount++;
                    movesInBlock = 0;

                    int o = s.IndexOf('('), c = s.LastIndexOf(')');
                    if (o >= 0 && c > o)
                    {
                        string[] p = s.Substring(o + 1, c - o - 1).Split(',');
                        if (p.Length >= 3
                            && double.TryParse(p[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double spX)
                            && double.TryParse(p[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double spY)
                            && double.TryParse(p[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double spZ))
                        {
                            deepestZ = Math.Min(deepestZ, spZ);
                            prevX = spX; prevY = spY;
                            if (headerDZ > 0 && spZ < -(headerDZ + SpoilboardAllowance))
                                zWarnings.Add("L" + lineNum + ": SP depth="
                                    + spZ.ToString("F3", CultureInfo.InvariantCulture)
                                    + " mm exceeds plate DZ="
                                    + headerDZ.ToString("F1", CultureInfo.InvariantCulture)
                                    + " mm + 5 mm spoilboard allowance");
                        }
                    }
                }
                else if (s.StartsWith("EP "))
                {
                    epCount++;
                    if (spStack.Count > 0)
                    {
                        int matchedSp = spStack.Pop();
                        if (movesInBlock == 0)
                        {
                            emptyBlocks++;
                            errors.Add("L" + matchedSp
                                + ": Empty SP/EP block (no moves between SP and EP)");
                        }
                        movesInBlock = 0;
                    }
                    else
                    {
                        errors.Add("L" + lineNum + ": EP without preceding SP");
                    }
                }
                else if (s.StartsWith("G01 ") || s.StartsWith("G02M ") || s.StartsWith("G03M "))
                {
                    moveCount++;
                    movesInBlock++;
                    if (spStack.Count == 0)
                        errors.Add("L" + lineNum + ": Move outside SP/EP block: "
                            + s.Substring(0, Math.Min(s.Length, 50)));

                    int o = s.IndexOf('('), c = s.IndexOf(')');
                    if (o >= 0 && c > o)
                    {
                        string[] mp = s.Substring(o + 1, c - o - 1).Split(',');
                        if (mp.Length >= 2
                            && double.TryParse(mp[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double mx)
                            && double.TryParse(mp[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double my))
                        {
                            if (!double.IsNaN(prevX))
                                pathLength += Math.Sqrt((mx - prevX) * (mx - prevX) + (my - prevY) * (my - prevY));
                            prevX = mx; prevY = my;
                        }
                    }
                }
                else if (s.StartsWith("WZB ") || s.StartsWith("WZF ") || s.StartsWith("WZS "))
                {
                    toolCalls.Add(s);
                }
                else if (s.StartsWith("CALL "))
                {
                    callCount++;
                }
                else if (s.StartsWith("Bohrung ("))
                {
                    drillCount++;
                    int o = s.IndexOf('('), c = s.LastIndexOf(')');
                    if (o >= 0 && c > o && headerDZ > 0)
                    {
                        string[] bp = s.Substring(o + 1, c - o - 1).Split(',');
                        if (bp.Length >= 4
                            && double.TryParse(bp[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double surfZ)
                            && double.TryParse(bp[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double cutZ))
                        {
                            double drillDepth = surfZ - cutZ;
                            deepestZ = Math.Min(deepestZ, cutZ);
                            if (drillDepth > headerDZ + SpoilboardAllowance)
                                zWarnings.Add("L" + lineNum + ": Drill depth="
                                    + drillDepth.ToString("F1", CultureInfo.InvariantCulture)
                                    + " mm exceeds plate DZ="
                                    + headerDZ.ToString("F1", CultureInfo.InvariantCulture)
                                    + " mm + 5 mm spoilboard allowance");
                        }
                    }
                }

                // CALL named-param scan: TIEFE, SZ, TOPF_T, DUEBEL_T → contribute to deepestZ
                if (s.StartsWith("CALL "))
                {
                    foreach (string paramName in new[] { "TIEFE", "SZ", "TOPF_T", "DUEBEL_T" })
                    {
                        var m = Regex.Match(s, paramName + @":=([-\d.]+)");
                        if (m.Success && double.TryParse(m.Groups[1].Value,
                            NumberStyles.Any, CultureInfo.InvariantCulture, out double pz))
                            deepestZ = Math.Min(deepestZ, pz);
                    }
                }
            }

            // Unclosed SPs at end of file
            foreach (int openLine in spStack)
                errors.Add("L" + openLine + ": SP never closed (no matching EP)");

            int toolChangeCount = toolCalls.Count;
            bool isValid = errors.Count == 0;

            // Summary line
            string summary = "SP=" + spCount + " EP=" + epCount
                + " Moves=" + moveCount + " Lines=" + lines.Length;
            if (emptyBlocks > 0)
                summary += " EmptyBlocks=" + emptyBlocks;
            if (deepestZ < double.MaxValue)
                summary += " DeepestZ=" + deepestZ.ToString("F3", CultureInfo.InvariantCulture);
            if (zWarnings.Count > 0)
                summary += " ZWarnings=" + zWarnings.Count;
            summary = isValid
                ? "OK  " + summary
                : errors.Count + " error(s)  " + summary;

            // Time estimate (rough): drill ~3s, SP block ~30s, call ~5s, tool change ~15s
            double estSeconds = drillCount * 3 + spCount * 30 + callCount * 5 + toolChangeCount * 15;
            int estMin = (int)(estSeconds / 60);
            int estSec = (int)(estSeconds % 60);

            var stats = new List<string>
            {
                "Drills:              " + drillCount,
                "Contour blocks (SP): " + spCount,
                "Macro calls (CALL):  " + callCount,
                "Tool changes:        " + toolChangeCount,
                "Unique tools:        " + toolChangeCount,
                "Path length:         " + (pathLength / 1000.0).ToString("F2", CultureInfo.InvariantCulture) + " m",
                "Est. time:           ~" + estMin + "m " + estSec + "s  (rough estimate)",
            };

            return new AnalysisResult
            {
                IsValid = isValid,
                ErrorCount = errors.Count,
                Errors = errors,
                ZWarnings = zWarnings,
                Summary = summary,
                Stats = stats,
                SpCount = spCount,
                EpCount = epCount,
                MoveCount = moveCount,
                EmptyBlocks = emptyBlocks,
                DrillCount = drillCount,
                CallCount = callCount,
                ToolChangeCount = toolChangeCount,
                PathLength = pathLength,
                DeepestZ = deepestZ,
                HeaderDZ = headerDZ,
            };
        }
    }
}
