using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Parses assembled .hop content into material-removal solids for the
    /// HopStockSim component: each machining operation becomes one or more
    /// cut-solid descriptors (cylinder or oriented slab) that the component
    /// subtracts from the stock box. Pure — no Rhino dependency; all geometry
    /// is described with plain doubles so the parse is unit-testable.
    ///
    /// Z convention for named depth parameters (Tiefe/TIEFE/NT):
    ///  - _Nuten NT is a POSITIVE depth below the surface (reference-verified).
    ///  - Tiefe in _nuten_frei/_Kreistasche/_Kreisbahn/_Rechteck is the
    ///    ABSOLUTE cut Z (surface minus depth) as emitted by our own logic
    ///    modules. A NEGATIVE Tiefe (KorpusPanel convention) is interpreted
    ///    as depth below the stock top.
    /// </summary>
    internal static class StockSimLogic
    {
        internal enum SolidKind { Cylinder, Slab }

        internal struct CutSolid
        {
            public SolidKind Kind;
            public double X1, Y1;        // Cylinder: center. Slab: segment start.
            public double X2, Y2;        // Slab: segment end.
            public double Z0, Z1;        // bottom / top of removed material
            public double Width;         // diameter (cylinder) or slot width (slab)
            public bool RoundEnds;       // router path: extend ends by Width/2
            public int StepIndex;        // 1-based operation group for the step slider
        }

        internal struct StockSimPlan
        {
            public double Dx, Dy, Dz;
            public List<CutSolid> Cuts;
            public List<string> StepLabels;   // one per step index (1-based)
            public List<string> Warnings;
        }

        private static readonly Regex _toolRx = new Regex(
            @"^(WZB|WZF|WZS)\s*\(\s*(\d+)", RegexOptions.Compiled);

        internal static StockSimPlan Parse(string hopContent,
            IReadOnlyDictionary<int, double> toolDiameters, double defaultMillDiameter)
        {
            var plan = new StockSimPlan
            {
                Dx = 2440, Dy = 1220, Dz = 19,
                Cuts = new List<CutSolid>(),
                StepLabels = new List<string>(),
                Warnings = new List<string>(),
            };
            if (defaultMillDiameter <= 0) defaultMillDiameter = 8.0;

            string toolType = "";
            int toolNr = 0;
            var diaWarned = new HashSet<int>();
            var unknownMacros = new HashSet<string>();
            double curX = double.NaN, curY = double.NaN, curZ = 0;
            int step = 0;

            var lines = (hopContent ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool inVars = false;
            foreach (string raw in lines)
            {
                string s = (raw ?? "").Trim();

                // Stock dimensions: VARS block wins (header ;DZ can be 0 — see HopAnalyzer)
                if (s == "VARS") { inVars = true; continue; }
                if (s == "START") { inVars = false; continue; }
                if (inVars)
                {
                    TryVar(s, "DX", ref plan.Dx);
                    TryVar(s, "DY", ref plan.Dy);
                    TryVar(s, "DZ", ref plan.Dz);
                    continue;
                }

                // Skippable blocks (/CALL …) still remove material when enabled
                string t = s.StartsWith("/") ? s.Substring(1) : s;

                var toolMatch = _toolRx.Match(t);
                if (toolMatch.Success)
                {
                    toolType = toolMatch.Groups[1].Value;
                    toolNr = int.Parse(toolMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                    continue;
                }

                if (t.StartsWith("Bohrung ("))
                {
                    double[] a = PositionalArgs(t, 5);
                    if (a == null) continue;
                    step++;
                    plan.StepLabels.Add("Drill Ø" + F(a[4]) + " @ (" + F(a[0]) + ", " + F(a[1]) + ")");
                    double zTop = Math.Max(a[2], a[3]);
                    double zBot = Math.Min(a[2], a[3]);
                    plan.Cuts.Add(new CutSolid
                    {
                        Kind = SolidKind.Cylinder,
                        X1 = a[0], Y1 = a[1], X2 = a[0], Y2 = a[1],
                        Z0 = zBot, Z1 = zTop,
                        Width = a[4] > 0 ? a[4] : defaultMillDiameter,
                        StepIndex = step,
                    });
                }
                else if (t.StartsWith("SP ("))
                {
                    double[] a = PositionalArgs(t, 3);
                    if (a == null) continue;
                    curX = a[0]; curY = a[1]; curZ = a[2];
                    step++;
                    plan.StepLabels.Add(ToolLabel(toolType, toolNr) + " path @ Z" + F(a[2]));
                }
                else if (t.StartsWith("G01 ("))
                {
                    double[] a = PositionalArgs(t, 2);
                    if (a == null || double.IsNaN(curX)) continue;
                    AddMillSegment(plan, curX, curY, a[0], a[1], curZ,
                        MillDia(toolNr, toolDiameters, defaultMillDiameter, diaWarned, plan.Warnings), step);
                    curX = a[0]; curY = a[1];
                }
                else if (t.StartsWith("G02M (") || t.StartsWith("G03M ("))
                {
                    double[] a = PositionalArgs(t, 5);
                    if (a == null || double.IsNaN(curX)) continue;
                    double dia = MillDia(toolNr, toolDiameters, defaultMillDiameter, diaWarned, plan.Warnings);
                    foreach (var seg in TessellateArc(curX, curY, a[0], a[1], a[3], a[4], t.StartsWith("G03M")))
                        AddMillSegment(plan, seg[0], seg[1], seg[2], seg[3], curZ, dia, step);
                    curX = a[0]; curY = a[1];
                }
                else if (t.IndexOf("_nuten_frei_v5", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    double? x1 = Arg(t, "X1"), y1 = Arg(t, "Y1"), x2 = Arg(t, "X2"), y2 = Arg(t, "Y2");
                    double? nb = Arg(t, "NB"), tiefe = Arg(t, "Tiefe"), lage = Arg(t, "LAGE");
                    if (!(x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue && nb.HasValue && tiefe.HasValue))
                        continue;
                    step++;
                    bool isSaw = toolType == "WZS";
                    plan.StepLabels.Add((isSaw ? "Saw cut" : "Free slot")
                        + " (" + F(x1.Value) + ", " + F(y1.Value) + ") → ("
                        + F(x2.Value) + ", " + F(y2.Value) + ")");
                    if (lage.HasValue && Math.Abs(lage.Value) > 0.01)
                        plan.Warnings.Add("Step " + step + ": angled saw cut (LAGE="
                            + F(lage.Value) + "°) simulated as a vertical kerf.");
                    plan.Cuts.Add(new CutSolid
                    {
                        Kind = SolidKind.Slab,
                        X1 = x1.Value, Y1 = y1.Value, X2 = x2.Value, Y2 = y2.Value,
                        Z0 = AbsCutZ(tiefe.Value, plan.Dz), Z1 = plan.Dz,
                        Width = nb.Value,
                        RoundEnds = !isSaw,
                        StepIndex = step,
                    });
                }
                else if (t.IndexOf("_Nuten_X_V5", StringComparison.OrdinalIgnoreCase) >= 0
                      || t.IndexOf("_Nuten_Y_V5", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bool isX = t.IndexOf("_Nuten_X_V5", StringComparison.OrdinalIgnoreCase) >= 0;
                    double? nb = Arg(t, "NB"), nt = Arg(t, "NT"), arand = Arg(t, "ARAND");
                    double al = Arg(t, "ALINKS") ?? 0, ar = Arg(t, "ARECHTS") ?? 0;
                    if (!(nb.HasValue && nt.HasValue && arand.HasValue)) continue;
                    step++;
                    plan.StepLabels.Add((isX ? "X" : "Y") + "-groove @ " + F(arand.Value)
                        + " (depth " + F(nt.Value) + ")");
                    // Groove spans the full panel minus the end shortenings
                    double runLen = isX ? plan.Dx : plan.Dy;
                    plan.Cuts.Add(new CutSolid
                    {
                        Kind = SolidKind.Slab,
                        X1 = isX ? al : arand.Value,
                        Y1 = isX ? arand.Value : al,
                        X2 = isX ? runLen - ar : arand.Value,
                        Y2 = isX ? arand.Value : runLen - ar,
                        Z0 = plan.Dz - Math.Abs(nt.Value), Z1 = plan.Dz,
                        Width = nb.Value,
                        StepIndex = step,
                    });
                }
                else if (t.IndexOf("_Kreistasche_V5", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    double? cx = Arg(t, "X_Mitte"), cy = Arg(t, "Y_Mitte");
                    double? r = Arg(t, "Radius"), tiefe = Arg(t, "Tiefe");
                    if (!(cx.HasValue && cy.HasValue && r.HasValue && tiefe.HasValue)) continue;
                    step++;
                    plan.StepLabels.Add("Circular pocket R" + F(r.Value)
                        + " @ (" + F(cx.Value) + ", " + F(cy.Value) + ")");
                    plan.Cuts.Add(new CutSolid
                    {
                        Kind = SolidKind.Cylinder,
                        X1 = cx.Value, Y1 = cy.Value, X2 = cx.Value, Y2 = cy.Value,
                        Z0 = AbsCutZ(tiefe.Value, plan.Dz), Z1 = plan.Dz,
                        Width = r.Value * 2.0,
                        StepIndex = step,
                    });
                }
                else if (t.IndexOf("_Kreisbahn_V5", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    double? cx = Arg(t, "X_Mitte"), cy = Arg(t, "Y_Mitte");
                    double? r = Arg(t, "Radius"), tiefe = Arg(t, "Tiefe");
                    if (!(cx.HasValue && cy.HasValue && r.HasValue && tiefe.HasValue)) continue;
                    step++;
                    plan.StepLabels.Add("Circular path R" + F(r.Value)
                        + " @ (" + F(cx.Value) + ", " + F(cy.Value) + ")");
                    double dia = MillDia(toolNr, toolDiameters, defaultMillDiameter, diaWarned, plan.Warnings);
                    double sweep = (Arg(t, "Winkel") ?? 360.0) * Math.PI / 180.0;
                    double z0 = AbsCutZ(tiefe.Value, plan.Dz);
                    int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 12)));
                    for (int i = 0; i < n; i++)
                    {
                        double a0 = sweep * i / n, a1 = sweep * (i + 1) / n;
                        AddMillSegment(plan,
                            cx.Value + r.Value * Math.Cos(a0), cy.Value + r.Value * Math.Sin(a0),
                            cx.Value + r.Value * Math.Cos(a1), cy.Value + r.Value * Math.Sin(a1),
                            z0, dia, step);
                    }
                }
                else if (t.IndexOf("_Rechteck_V7", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    double? cx = Arg(t, "X_MITTE"), cy = Arg(t, "Y_MITTE");
                    double? len = Arg(t, "LAENGE"), wid = Arg(t, "BREITE"), tiefe = Arg(t, "TIEFE");
                    double angDeg = Arg(t, "WINKEL") ?? 0;
                    if (!(cx.HasValue && cy.HasValue && len.HasValue && wid.HasValue && tiefe.HasValue))
                        continue;
                    step++;
                    plan.StepLabels.Add("Rect pocket " + F(len.Value) + "×" + F(wid.Value)
                        + " @ (" + F(cx.Value) + ", " + F(cy.Value) + ")");
                    // Rect pocket = slab along its long axis, square ends.
                    // ponytail: the corner RADIUS is ignored — the sim removes
                    // sharp corners the real pocket rounds off (~1 mm² per corner).
                    double ang = angDeg * Math.PI / 180.0;
                    double hx = Math.Cos(ang) * len.Value / 2.0, hy = Math.Sin(ang) * len.Value / 2.0;
                    plan.Cuts.Add(new CutSolid
                    {
                        Kind = SolidKind.Slab,
                        X1 = cx.Value - hx, Y1 = cy.Value - hy,
                        X2 = cx.Value + hx, Y2 = cy.Value + hy,
                        Z0 = AbsCutZ(tiefe.Value, plan.Dz), Z1 = plan.Dz,
                        Width = wid.Value,
                        StepIndex = step,
                    });
                }
                else if (t.StartsWith("CALL "))
                {
                    // Fixchip clamps remove no material; anything else is honest-
                    // ly reported instead of silently missing from the sim.
                    var name = Regex.Match(t, @"^CALL\s+(\S+?)\s*\(");
                    string macro = name.Success ? name.Groups[1].Value : "?";
                    if (macro.IndexOf("Fixchip", StringComparison.OrdinalIgnoreCase) < 0
                        && macro.IndexOf("HH_Park", StringComparison.OrdinalIgnoreCase) < 0
                        && unknownMacros.Add(macro))
                        plan.Warnings.Add("Macro " + macro + " is not simulated (no removal geometry known).");
                }
            }

            return plan;
        }

        // -------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------

        /// <summary>Negative named depth = depth below stock top (KorpusPanel
        /// convention); otherwise the value already IS the absolute cut Z.</summary>
        private static double AbsCutZ(double tiefe, double dz) =>
            tiefe < 0 ? dz + tiefe : tiefe;

        private static void AddMillSegment(StockSimPlan plan,
            double x1, double y1, double x2, double y2, double cutZ, double dia, int step)
        {
            double len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            if (len < 0.001) return;
            plan.Cuts.Add(new CutSolid
            {
                Kind = SolidKind.Slab,
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Z0 = cutZ, Z1 = plan.Dz,
                Width = dia,
                RoundEnds = true,
                StepIndex = step,
            });
        }

        private static double MillDia(int toolNr, IReadOnlyDictionary<int, double> dias,
            double fallback, HashSet<int> warned, List<string> warnings)
        {
            if (dias != null && dias.TryGetValue(toolNr, out double d) && d > 0) return d;
            if (warned.Add(toolNr))
                warnings.Add("No diameter known for tool " + toolNr + " — using "
                    + F(fallback) + " mm. Wire ToolNrs/ToolDiameters for exact widths.");
            return fallback;
        }

        /// <summary>Arc → straight segments (max 15° each), same center/direction
        /// convention as BackplotLogic. Returns [x1,y1,x2,y2] quads.</summary>
        private static IEnumerable<double[]> TessellateArc(
            double sx, double sy, double ex, double ey, double cx, double cy, bool ccw)
        {
            double r = Math.Sqrt((sx - cx) * (sx - cx) + (sy - cy) * (sy - cy));
            if (r < 1e-9) { yield return new[] { sx, sy, ex, ey }; yield break; }

            double a0 = Math.Atan2(sy - cy, sx - cx);
            double a1 = Math.Atan2(ey - cy, ex - cx);
            double sweep = a1 - a0;
            if (ccw && sweep <= 0) sweep += 2 * Math.PI;
            if (!ccw && sweep >= 0) sweep -= 2 * Math.PI;

            int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 12)));
            double px = sx, py = sy;
            for (int i = 1; i <= n; i++)
            {
                double a = a0 + sweep * i / n;
                double nx = cx + r * Math.Cos(a), ny = cy + r * Math.Sin(a);
                yield return new[] { px, py, nx, ny };
                px = nx; py = ny;
            }
        }

        private static void TryVar(string line, string name, ref double target)
        {
            var m = Regex.Match(line, @"^" + name + @"\s*:=\s*([-\d.]+)");
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out double v) && v > 0)
                target = v;
        }

        private static double[] PositionalArgs(string line, int count)
        {
            int o = line.IndexOf('(');
            int c = line.LastIndexOf(')');
            if (o < 0 || c <= o) return null;
            string[] parts = line.Substring(o + 1, c - o - 1).Split(',');
            if (parts.Length < count) return null;
            var result = new double[count];
            for (int i = 0; i < count; i++)
                if (!double.TryParse(parts[i].Trim(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out result[i]))
                    return null;
            return result;
        }

        private static double? Arg(string line, string name)
        {
            // IgnoreCase — unlike OpLineTransformer we only need the VALUE, and
            // macro emitters vary the casing (Tiefe/TIEFE); families never
            // collide on the same line.
            var m = Regex.Match(line, @"(?<![A-Za-z_])" + Regex.Escape(name) + @"\s*:=\s*(-?[\d.]+)",
                RegexOptions.IgnoreCase);
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out double v))
                return v;
            return null;
        }

        private static string ToolLabel(string toolType, int toolNr) =>
            (toolType == "" ? "Tool" : toolType) + " " + toolNr;

        private static string F(double v) =>
            Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
    }
}
