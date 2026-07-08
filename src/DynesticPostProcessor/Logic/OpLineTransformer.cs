using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Applies a nesting placement (translation, optionally rotation) to the
    /// coordinate values embedded in already-emitted NC operation lines.
    ///
    /// This is the C2 fix: HopSheetExport previously concatenated part-local
    /// operation lines verbatim, machining every part at its pre-nesting
    /// origin. This transformer rewrites the XY values.
    ///
    /// SCOPE (deliberate): only pure translations are supported. Rotated
    /// placements are rejected with an error instead of emitting wrong
    /// coordinates — axis-bound macros (_Bohgx_V5, _Nuten_X_V5, _saege_x_V7)
    /// would need to be swapped to their Y-counterparts under 90° rotation
    /// and re-referenced under 180°, which must be machine-validated before
    /// we ship it. Lock rotation in OpenNest (per-part Rotations=1) for now.
    /// </summary>
    internal static class OpLineTransformer
    {
        internal struct Placement
        {
            public double Tx;
            public double Ty;
            /// <summary>Rotation angle in degrees; only 0 is currently supported.</summary>
            public double RotationDeg;
        }

        internal struct TransformResult
        {
            public List<string> Lines;
            public string Error; // null on success
        }

        // Named XY parameter pairs used by the CALL macro family.
        private static readonly string[][] _namedXYPairs =
        {
            new[] { "X1", "Y1" },
            new[] { "X2", "Y2" },
            new[] { "X_Mitte", "Y_Mitte" },
            new[] { "X_MITTE", "Y_MITTE" },
            new[] { "SPX", "SPY" },
            new[] { "POSX", "POSY" },
        };

        /// <summary>
        /// Transform a part's operation lines by a placement. Returns an error
        /// (and no lines) when the placement rotates the part or when a line
        /// contains an axis-bound macro that cannot be safely translated.
        /// </summary>
        internal static TransformResult Apply(IReadOnlyList<string> opLines, Placement p)
        {
            // Rotation guard — see class remarks.
            double rot = NormalizeAngle(p.RotationDeg);
            if (Math.Abs(rot) > 0.01)
            {
                return new TransformResult
                {
                    Lines = null,
                    Error = "part is rotated by " + rot.ToString("F1", CultureInfo.InvariantCulture)
                        + " deg — only unrotated (grain-locked) placements are supported. "
                        + "Set Rotations=1 for this part in OpenNest, or place it manually.",
                };
            }

            var result = new List<string>();
            foreach (string raw in opLines)
            {
                string line = raw ?? "";
                string t = line.TrimStart('/').TrimStart();

                if (t.StartsWith("Bohrung ("))
                {
                    result.Add(TransformPositional(line, new[] { 0, 1 }, new[] { p.Tx, p.Ty }));
                }
                else if (t.StartsWith("SP ("))
                {
                    result.Add(TransformPositional(line, new[] { 0, 1 }, new[] { p.Tx, p.Ty }));
                }
                else if (t.StartsWith("G01 ("))
                {
                    result.Add(TransformPositional(line, new[] { 0, 1 }, new[] { p.Tx, p.Ty }));
                }
                else if (t.StartsWith("G02M (") || t.StartsWith("G03M ("))
                {
                    // endpoint (0,1) + arc center (3,4)
                    result.Add(TransformPositional(line, new[] { 0, 1, 3, 4 }, new[] { p.Tx, p.Ty, p.Tx, p.Ty }));
                }
                else if (t.Contains("_Bohgx_V5") || t.Contains("_Bohgy_V5"))
                {
                    // Axis-bound drill rows: SPY (x-row) / SPX (y-row) is the
                    // cross position; the along-axis start is implicit at the
                    // panel edge. Under pure translation the cross offset moves
                    // by ty/tx — but the along-axis start would ALSO need the
                    // macro's edge reference shifted, which the macro does not
                    // expose. Refuse rather than half-place it.
                    return new TransformResult
                    {
                        Lines = null,
                        Error = "contains an axis-bound drill-row macro (_Bohgx/_Bohgy) that cannot "
                            + "be repositioned on a nested sheet. Use HopDrill points for nested parts.",
                    };
                }
                else if (t.Contains("_Nuten_X_V5") || t.Contains("_Nuten_Y_V5")
                      || t.Contains("_saege_x_V7") || t.Contains("_saege_y_V7"))
                {
                    // Same class of problem: positions are edge-referenced
                    // (ARAND / SY / SX) relative to the PART, not the sheet.
                    return new TransformResult
                    {
                        Lines = null,
                        Error = "contains an edge-referenced macro (_Nuten_X/Y or _saege_x/y) that cannot "
                            + "be repositioned on a nested sheet. Use HopFreeSlot / HopSaw for nested parts.",
                    };
                }
                else if (t.StartsWith("CALL ") || t.StartsWith("/CALL "))
                {
                    result.Add(TransformNamedXY(line, p.Tx, p.Ty));
                }
                else
                {
                    // Tool calls (WZB/WZF/WZS), EP, comments — position-free.
                    result.Add(line);
                }
            }

            return new TransformResult { Lines = result, Error = null };
        }

        // Shift selected positional args inside the first (...) group.
        private static string TransformPositional(string line, int[] indices, double[] offsets)
        {
            int o = line.IndexOf('(');
            int c = line.LastIndexOf(')');
            if (o < 0 || c <= o) return line;

            string head = line.Substring(0, o + 1);
            string tail = line.Substring(c);
            string[] args = line.Substring(o + 1, c - o - 1).Split(',');

            for (int k = 0; k < indices.Length; k++)
            {
                int idx = indices[k];
                if (idx >= args.Length) continue;
                if (double.TryParse(args[idx].Trim(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double v))
                {
                    args[idx] = NcFmt.F(v + offsets[k]);
                }
            }
            return head + string.Join(",", args) + tail;
        }

        // Shift known named XY parameter pairs (X1:=…,Y1:=…, X_Mitte:=…, …).
        private static string TransformNamedXY(string line, double tx, double ty)
        {
            string result = line;
            foreach (var pair in _namedXYPairs)
            {
                result = ShiftNamedParam(result, pair[0], tx);
                result = ShiftNamedParam(result, pair[1], ty);
            }
            return result;
        }

        private static string ShiftNamedParam(string line, string name, double offset)
        {
            // Case-sensitive on purpose: X_Mitte vs X_MITTE are distinct entries
            // in _namedXYPairs, so each spelling matches its own macro family.
            var rx = new Regex(@"(?<![A-Za-z_])(" + Regex.Escape(name) + @")\s*:=\s*(-?[\d.]+)");
            return rx.Replace(line, m =>
            {
                if (double.TryParse(m.Groups[2].Value, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double v))
                    return m.Groups[1].Value + ":=" + NcFmt.F(v + offset);
                return m.Value;
            });
        }

        private static double NormalizeAngle(double deg)
        {
            double a = deg % 360.0;
            if (a > 180.0) a -= 360.0;
            if (a < -180.0) a += 360.0;
            return a;
        }
    }
}
