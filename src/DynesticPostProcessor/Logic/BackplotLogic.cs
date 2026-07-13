using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Parses assembled .hop content into a draw-plan for the HopBackplot
    /// viewport preview: toolpath segments, arcs, drill points and macro
    /// reference points, each tagged with the active tool and a sequence
    /// index. Pure — no Rhino/Grasshopper dependency; the component turns
    /// the plan into viewport draw calls.
    /// </summary>
    internal static class BackplotLogic
    {
        internal enum OpKind { Drill, Move, Arc, MacroPoint, MacroSegment }

        internal struct BackplotOp
        {
            public OpKind Kind;
            public double StartX, StartY;
            public double EndX, EndY;
            public double CenterX, CenterY;   // arcs only
            public bool IsCCW;                // arcs only
            public double Z;                  // plunge/cut Z where known
            public double Diameter;           // drills, when parseable
            public string ToolType;           // WZB / WZF / WZS / ""
            public int ToolNr;
            public int SequenceIndex;         // order of chain/group starts
            public bool IsChainStart;         // SP or first op after tool change
        }

        internal struct BackplotPlan
        {
            public List<BackplotOp> Ops;
            public int ChainCount;
        }

        private static readonly Regex _toolRx = new Regex(
            @"^(WZB|WZF|WZS)\s*\(\s*(\d+)", RegexOptions.Compiled);

        internal static BackplotPlan Parse(string hopContent)
        {
            var ops = new List<BackplotOp>();
            string toolType = "";
            int toolNr = 0;
            int seq = 0;
            double curX = double.NaN, curY = double.NaN;
            bool nextIsChainStart = false;

            var lines = (hopContent ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string raw in lines)
            {
                string s = (raw ?? "").Trim();
                // Skippable blocks (/CALL …) are still drawn — the operator may enable them
                string t = s.StartsWith("/") ? s.Substring(1) : s;

                var toolMatch = _toolRx.Match(t);
                if (toolMatch.Success)
                {
                    toolType = toolMatch.Groups[1].Value;
                    toolNr = int.Parse(toolMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                    nextIsChainStart = true;
                    continue;
                }

                if (t.StartsWith("SP ("))
                {
                    double[] a = PositionalArgs(t, 3);
                    if (a != null)
                    {
                        curX = a[0]; curY = a[1];
                        seq++;
                        ops.Add(new BackplotOp
                        {
                            Kind = OpKind.Move,
                            StartX = a[0], StartY = a[1], EndX = a[0], EndY = a[1],
                            Z = a[2],
                            ToolType = toolType, ToolNr = toolNr,
                            SequenceIndex = seq, IsChainStart = true,
                        });
                        nextIsChainStart = false;
                    }
                }
                else if (t.StartsWith("G01 ("))
                {
                    double[] a = PositionalArgs(t, 2);
                    if (a != null && !double.IsNaN(curX))
                    {
                        ops.Add(new BackplotOp
                        {
                            Kind = OpKind.Move,
                            StartX = curX, StartY = curY, EndX = a[0], EndY = a[1],
                            ToolType = toolType, ToolNr = toolNr,
                            SequenceIndex = seq, IsChainStart = false,
                        });
                        curX = a[0]; curY = a[1];
                    }
                }
                else if (t.StartsWith("G02M (") || t.StartsWith("G03M ("))
                {
                    double[] a = PositionalArgs(t, 5);
                    if (a != null && !double.IsNaN(curX))
                    {
                        ops.Add(new BackplotOp
                        {
                            Kind = OpKind.Arc,
                            StartX = curX, StartY = curY,
                            EndX = a[0], EndY = a[1],
                            CenterX = a[3], CenterY = a[4],
                            IsCCW = t.StartsWith("G03M"),
                            ToolType = toolType, ToolNr = toolNr,
                            SequenceIndex = seq, IsChainStart = false,
                        });
                        curX = a[0]; curY = a[1];
                    }
                }
                else if (t.StartsWith("Bohrung ("))
                {
                    double[] a = PositionalArgs(t, 5);
                    if (a != null)
                    {
                        // Every drill gets its own sequence number (machining
                        // order) — one number per GROUP hid the order of all
                        // but the first operation.
                        seq++; nextIsChainStart = false;
                        ops.Add(new BackplotOp
                        {
                            Kind = OpKind.Drill,
                            StartX = a[0], StartY = a[1], EndX = a[0], EndY = a[1],
                            Z = a[3],
                            Diameter = a[4],
                            ToolType = toolType, ToolNr = toolNr,
                            SequenceIndex = seq, IsChainStart = false,
                        });
                    }
                }
                else if (t.StartsWith("CALL "))
                {
                    // Named XY pairs cover the macro families: segment macros
                    // (X1/Y1..X2/Y2), center macros (X_Mitte/X_MITTE), clamps (SPX/SPY)
                    double? x1 = NamedArg(t, "X1"), y1 = NamedArg(t, "Y1");
                    double? x2 = NamedArg(t, "X2"), y2 = NamedArg(t, "Y2");
                    if (x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue)
                    {
                        seq++; nextIsChainStart = false;   // one number per macro op
                        ops.Add(new BackplotOp
                        {
                            Kind = OpKind.MacroSegment,
                            StartX = x1.Value, StartY = y1.Value,
                            EndX = x2.Value, EndY = y2.Value,
                            ToolType = toolType, ToolNr = toolNr,
                            SequenceIndex = seq, IsChainStart = false,
                        });
                        continue;
                    }

                    double? cx = NamedArg(t, "X_Mitte") ?? NamedArg(t, "X_MITTE") ?? NamedArg(t, "SPX") ?? NamedArg(t, "POSX");
                    double? cy = NamedArg(t, "Y_Mitte") ?? NamedArg(t, "Y_MITTE") ?? NamedArg(t, "SPY") ?? NamedArg(t, "POSY");
                    if (cx.HasValue && cy.HasValue)
                    {
                        seq++; nextIsChainStart = false;   // one number per macro op
                        ops.Add(new BackplotOp
                        {
                            Kind = OpKind.MacroPoint,
                            StartX = cx.Value, StartY = cy.Value,
                            EndX = cx.Value, EndY = cy.Value,
                            ToolType = toolType, ToolNr = toolNr,
                            SequenceIndex = seq, IsChainStart = false,
                        });
                    }
                }
            }

            return new BackplotPlan { Ops = ops, ChainCount = seq };
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
            {
                if (!double.TryParse(parts[i].Trim(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out result[i]))
                    return null;
            }
            return result;
        }

        private static double? NamedArg(string line, string name)
        {
            // Case-sensitive: X_Mitte vs X_MITTE are distinct macro families
            var m = Regex.Match(line, @"(?<![A-Za-z_])" + Regex.Escape(name) + @"\s*:=\s*(-?[\d.]+)");
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out double v))
                return v;
            return null;
        }
    }
}
