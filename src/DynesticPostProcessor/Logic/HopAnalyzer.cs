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
        // Kept as a public alias for backwards compatibility with tests; defers to MachineConstants
        internal const double SpoilboardAllowance = MachineConstants.SpoilboardAllowanceMm;

        // Below this a contour move counts as "did not go anywhere". Coordinates are
        // emitted with 4 decimals, so 0.01 mm is far above formatting noise and far
        // below any cut a person would intend.
        internal const double DegenerateTolMm = 0.01;

        internal struct AnalysisResult
        {
            public bool IsValid;
            public int ErrorCount;
            public List<string> Errors;
            public List<string> ZWarnings;
            public List<string> FixchipWarnings;
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
            public int FixchipCount;
            public double PathLength;
            // Most negative RAW depth value (SP z, TIEFE/NT, Bohrung field 4), not a
            // physical depth: these mix the top and the table reference frames.
            public double DeepestZ;
            public double HeaderDZ;
        }

        // Operation footprint (xy) tracked through Pass 2 so we can do
        // a fixchip collision check after the main loop without re-parsing.
        private struct OpXY
        {
            public int LineNum;
            public double X;
            public double Y;
            public string Kind;
        }

        private static readonly Regex _callXYRegex = new Regex(
            @"X_Mitte\s*:=\s*([-\d.]+).*?Y_Mitte\s*:=\s*([-\d.]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static AnalysisResult Analyze(string hopContent)
        {
            var lines = (hopContent ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            var spStack       = new Stack<int>();
            var errors        = new List<string>();
            var zWarnings     = new List<string>();
            var toolCalls     = new HashSet<string>();
            var fixchips      = new List<(int LineNum, double X, double Y)>();
            var operations    = new List<OpXY>();
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
            double blockStartX = double.NaN;
            double blockStartY = double.NaN;
            bool blockMoved   = false;
            double deepestZ   = double.MaxValue;

            // Pass 1 — extract plate thickness DZ.
            // Machine-generated files put ;DZ=0 in the HEADER and the real
            // value in the VARS block ("DZ := 19;Dicke Z") — reference:
            // reference-hops/files/HZK_Boden_Deckel_602x278.hop. Check the
            // VARS form first; header value is the fallback.
            double headerDZ = 0;
            double varsDX = 0, varsDY = 0;
            foreach (string raw in lines)
            {
                string h = (raw ?? "").Trim();
                var mdim = Regex.Match(h, @"^D([XY])\s*:=\s*([-\d.]+)", RegexOptions.IgnoreCase);
                if (mdim.Success && double.TryParse(mdim.Groups[2].Value, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double dimVal) && dimVal > 0)
                {
                    if (mdim.Groups[1].Value.ToUpperInvariant() == "X") varsDX = dimVal;
                    else varsDY = dimVal;
                }
                if (h.StartsWith("DZ ", StringComparison.OrdinalIgnoreCase)
                    || h.StartsWith("DZ:", StringComparison.OrdinalIgnoreCase))
                {
                    // VARS syntax: "DZ := 19;comment" or "DZ:=19"
                    var m = Regex.Match(h, @"^DZ\s*:=\s*([-\d.]+)", RegexOptions.IgnoreCase);
                    if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double varsDz) && varsDz > 0)
                    {
                        headerDZ = varsDz;
                        break; // VARS value wins
                    }
                }
                if (h.StartsWith(";DZ=") && headerDZ <= 0)
                {
                    double.TryParse(h.Substring(4), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out headerDZ);
                    // no break — keep scanning in case a VARS DZ follows
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
                    blockMoved = false;
                    blockStartX = double.NaN; blockStartY = double.NaN;

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
                            blockStartX = spX; blockStartY = spY;
                            operations.Add(new OpXY { LineNum = lineNum, X = spX, Y = spY, Kind = "SP" });
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
                        else if (!blockMoved)
                        {
                            // WARNING, not an error: plunging at a single point is a
                            // legitimate operation (a stopped hole bored with the router),
                            // so this must not veto the export. It is almost always a
                            // collapsed input curve, but the intent is the operator's to
                            // judge -- surfacing it is enough. Downgraded 2026-08-10 after
                            // it blocked a real job on its first day.
                            zWarnings.Add("L" + matchedSp
                                + ": Contour goes nowhere — all " + movesInBlock
                                + " move(s) sit on the SP point. The tool plunges and"
                                + " retracts without cutting a path. Intentional point"
                                + " plunge, or a zero-length input curve?");
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
                            // Does this block actually go anywhere? A block whose moves
                            // all sit on the SP point plunges and retracts without
                            // cutting a path — the "Empty block" check misses it because
                            // the move count is non-zero.
                            if (!double.IsNaN(blockStartX)
                                && (Math.Abs(mx - blockStartX) > DegenerateTolMm
                                    || Math.Abs(my - blockStartY) > DegenerateTolMm))
                                blockMoved = true;
                            prevX = mx; prevY = my;
                            operations.Add(new OpXY { LineNum = lineNum, X = mx, Y = my, Kind = "Move" });
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
                else if (s.StartsWith("Bohrung (") || s.StartsWith("/Bohrung ("))
                {
                    // /Bohrung = operator-skippable, still checked like /CALL below
                    drillCount++;
                    if (NcDrill.TryParse(s, out var d))
                    {
                        if (d.HasXY)
                            operations.Add(new OpXY { LineNum = lineNum, X = d.X, Y = d.Y, Kind = "Drill" });
                        // Raw depth field, like TIEFE from the CALL scan below
                        deepestZ = Math.Min(deepestZ, d.Depth);
                        double drillDepth = headerDZ - d.BottomZ(headerDZ);   // from the top
                        if (headerDZ > 0 && drillDepth > headerDZ + SpoilboardAllowance)
                            zWarnings.Add("L" + lineNum + ": Drill depth="
                                + drillDepth.ToString("F1", CultureInfo.InvariantCulture)
                                + " mm exceeds plate DZ="
                                + headerDZ.ToString("F1", CultureInfo.InvariantCulture)
                                + " mm + 5 mm spoilboard allowance");
                    }
                }
                // Fixchip clamp declarations — checked OUTSIDE the else-if chain
                // because the machine syntax "[/]CALL Fixchip_K ( VAL SPX:=…)" also
                // matches the generic CALL branch above.
                // Positions may be variable expressions (SPY:=DY-60) — those can't
                // be collision-checked numerically and are skipped.
                if (s.Contains("Fixchip_K"))
                {
                    var mx = Regex.Match(s, @"SPX\s*:=\s*([-\d.]+)\s*,", RegexOptions.IgnoreCase);
                    var my = Regex.Match(s, @"SPY\s*:=\s*([-\d.]+)\s*,", RegexOptions.IgnoreCase);
                    if (mx.Success && my.Success
                        && double.TryParse(mx.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double fx)
                        && double.TryParse(my.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double fy))
                    {
                        fixchips.Add((lineNum, fx, fy));
                    }
                }

                // CALL named-param scan: depth + XY for fixchip check.
                // Also matches "/CALL ..." (operator-skippable blocks).
                if (s.StartsWith("CALL ") || s.StartsWith("/CALL "))
                {
                    // Slot / saw endpoints (X1:=..,Y1:=..,X2:=..,Y2:=..) -- these
                    // were invisible to every check until the part-frame guard
                    // below needed them.
                    var mEnds = Regex.Match(s,
                        @"X1\s*:=\s*(-?[\d.]+)\s*,\s*Y1\s*:=\s*(-?[\d.]+)\s*,\s*X2\s*:=\s*(-?[\d.]+)\s*,\s*Y2\s*:=\s*(-?[\d.]+)",
                        RegexOptions.IgnoreCase);
                    if (mEnds.Success
                        && double.TryParse(mEnds.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double e1x)
                        && double.TryParse(mEnds.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double e1y)
                        && double.TryParse(mEnds.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double e2x)
                        && double.TryParse(mEnds.Groups[4].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double e2y))
                    {
                        operations.Add(new OpXY { LineNum = lineNum, X = e1x, Y = e1y, Kind = "SLOT" });
                        operations.Add(new OpXY { LineNum = lineNum, X = e2x, Y = e2y, Kind = "SLOT" });
                    }

                    // X_Mitte / Y_Mitte (rect pocket, circ pocket, circ path)
                    var xyMatch = _callXYRegex.Match(s);
                    if (xyMatch.Success
                        && double.TryParse(xyMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double cmx)
                        && double.TryParse(xyMatch.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double cmy))
                    {
                        operations.Add(new OpXY { LineNum = lineNum, X = cmx, Y = cmy, Kind = "CALL" });
                    }
                    // Depth params across all macro families. Case-insensitive
                    // (files contain both TIEFE:= and Tiefe:=) and word-boundary
                    // anchored so TIEFE never matches inside ZUTIEFE. "Tiefe"
                    // covers _nuten_frei_v5 / _Kreisbahn_V5 / _Kreistasche_V5 —
                    // the saw/slot/circ family the old scan was blind to.
                    foreach (string paramName in new[] { "TIEFE", "SZ", "TOPF_T", "DUEBEL_T", "NT" })
                    {
                        var m = Regex.Match(s, @"(?<![A-Za-z_])" + paramName + @"\s*:=\s*(-?[\d.]+)",
                            RegexOptions.IgnoreCase);
                        if (m.Success && double.TryParse(m.Groups[1].Value,
                            NumberStyles.Any, CultureInfo.InvariantCulture, out double pz))
                        {
                            // NT (groove depth) is positive-down; others are absolute Z
                            if (paramName == "NT") pz = -Math.Abs(pz);
                            deepestZ = Math.Min(deepestZ, pz);
                        }
                    }
                }
            }

            // Unclosed SPs at end of file
            foreach (int openLine in spStack)
                errors.Add("L" + openLine + ": SP never closed (no matching EP)");

            // Fixchip collision check: any operation XY within ClampRadius of a fixchip = warning.
            // Skips ops on the same line as the fixchip (defensive — should never happen but cheap).
            var fixchipWarnings = new List<string>();
            double r = MachineConstants.FixchipClampRadiusMm;
            double r2 = r * r;
            foreach (var op in operations)
            {
                foreach (var fc in fixchips)
                {
                    if (op.LineNum == fc.LineNum) continue;
                    double dx = op.X - fc.X;
                    double dy = op.Y - fc.Y;
                    if (dx * dx + dy * dy < r2)
                    {
                        double d = Math.Sqrt(dx * dx + dy * dy);
                        fixchipWarnings.Add(
                            "L" + op.LineNum + ": " + op.Kind
                            + " at (" + op.X.ToString("F1", CultureInfo.InvariantCulture)
                            + "," + op.Y.ToString("F1", CultureInfo.InvariantCulture)
                            + ") within " + d.ToString("F1", CultureInfo.InvariantCulture)
                            + " mm of Fixchip_K (L" + fc.LineNum
                            + " at " + fc.X.ToString("F1", CultureInfo.InvariantCulture)
                            + "," + fc.Y.ToString("F1", CultureInfo.InvariantCulture)
                            + "); clamp radius is "
                            + r.ToString("F1", CultureInfo.InvariantCulture) + " mm");
                    }
                }
            }

            // Part-frame guard: machining coordinates must fit the declared
            // part. Through-cuts legitimately overshoot the edge by ~50 mm
            // (saw run-out), so only flag beyond 60 mm. The real accident this
            // catches: DX/DY describing the part in one orientation while the
            // operations are drawn in the other -- observed as slots running to
            // Y=1520 on a part declared DY=471, i.e. a metre beside the plate.
            const double frameSlack = 60.0;
            if (varsDX > 0 && varsDY > 0)
            {
                foreach (var op in operations)
                {
                    double outX = Math.Max(-op.X, op.X - varsDX);
                    double outY = Math.Max(-op.Y, op.Y - varsDY);
                    double beyond = Math.Max(outX, outY);
                    if (beyond > frameSlack)
                    {
                        errors.Add("L" + op.LineNum + ": " + op.Kind
                            + " at (" + op.X.ToString("F1", CultureInfo.InvariantCulture)
                            + "," + op.Y.ToString("F1", CultureInfo.InvariantCulture)
                            + ") lies " + beyond.ToString("F0", CultureInfo.InvariantCulture)
                            + " mm outside the declared part DX="
                            + varsDX.ToString("F1", CultureInfo.InvariantCulture)
                            + " x DY=" + varsDY.ToString("F1", CultureInfo.InvariantCulture)
                            + " -- dimensions and machining coordinates disagree"
                            + " (DX/DY swapped or geometry rotated 90 degrees?)");
                        break;   // one clear error per file beats fifty repeats
                    }
                }
            }

            // M7: Z-convention sanity. Every emitter writes cut depths as
            // absolute Z going DOWN from the plate surface — if the deepest
            // machined Z never reaches below the surface region, the geometry
            // is almost certainly not modelled in plate coordinates and the
            // machine would cut air (or the wrong depth).
            if (deepestZ < double.MaxValue && deepestZ > 0.5 && headerDZ > 0
                && deepestZ > headerDZ - 0.5)
            {
                zWarnings.Add("Deepest machined Z is " 
                    + deepestZ.ToString("F1", CultureInfo.InvariantCulture)
                    + " mm but the plate is only DZ=" 
                    + headerDZ.ToString("F1", CultureInfo.InvariantCulture)
                    + " mm thick — is the geometry modelled in plate coordinates"
                    + " (surface at Z=DZ, machine bed at Z=0)?");
            }

            int toolChangeCount = toolCalls.Count;
            bool isValid = errors.Count == 0 && fixchipWarnings.Count == 0;

            // Summary line
            string summary = "SP=" + spCount + " EP=" + epCount
                + " Moves=" + moveCount + " Lines=" + lines.Length;
            if (emptyBlocks > 0)
                summary += " EmptyBlocks=" + emptyBlocks;
            if (deepestZ < double.MaxValue)
                summary += " DeepestZ=" + deepestZ.ToString("F3", CultureInfo.InvariantCulture);
            if (zWarnings.Count > 0)
                summary += " ZWarnings=" + zWarnings.Count;
            if (fixchipWarnings.Count > 0)
                summary += " FixchipCollisions=" + fixchipWarnings.Count;
            summary = isValid
                ? "OK  " + summary
                : (errors.Count + fixchipWarnings.Count) + " issue(s)  " + summary;

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
                "Fixchip clamps:      " + fixchips.Count,
                "Path length:         " + (pathLength / 1000.0).ToString("F2", CultureInfo.InvariantCulture) + " m",
                "Est. time:           ~" + estMin + "m " + estSec + "s  (rough estimate)",
            };

            return new AnalysisResult
            {
                IsValid = isValid,
                ErrorCount = errors.Count + fixchipWarnings.Count,
                Errors = errors,
                ZWarnings = zWarnings,
                FixchipWarnings = fixchipWarnings,
                Summary = summary,
                Stats = stats,
                SpCount = spCount,
                EpCount = epCount,
                MoveCount = moveCount,
                EmptyBlocks = emptyBlocks,
                FixchipCount = fixchips.Count,
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
