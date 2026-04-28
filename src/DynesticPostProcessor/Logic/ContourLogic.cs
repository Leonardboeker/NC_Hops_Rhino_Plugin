using System;
using System.Collections.Generic;
using System.Globalization;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for 2D contour cuts. The Rhino-bound
    /// geometry layer (curve.Offset, ToArcsAndLines, ArcCurve detection)
    /// decomposes the input curve into a list of ContourSegment, which
    /// this logic then formats into SP/G01/G02M/G03M/EP NC instructions.
    /// </summary>
    internal static class ContourLogic
    {
        internal enum SegKind { Line, Arc }

        internal struct ContourSegment
        {
            public SegKind Kind;
            // Line: StartX/Y -> EndX/Y. Arc: StartX/Y -> EndX/Y around CenterX/Y.
            public double StartX, StartY;
            public double EndX,   EndY;
            // Arc-only:
            public double CenterX, CenterY;
            public bool IsCCW;
            // Line-only — start tangent (for lead-in) and end tangent (for lead-out)
            public double TangentStartX, TangentStartY;
            public double TangentEndX,   TangentEndY;

            internal static ContourSegment Line(double sx, double sy, double ex, double ey)
            {
                double dx = ex - sx, dy = ey - sy;
                double len = Math.Sqrt(dx * dx + dy * dy);
                double tx = len > 0 ? dx / len : 1;
                double ty = len > 0 ? dy / len : 0;
                return new ContourSegment
                {
                    Kind = SegKind.Line,
                    StartX = sx, StartY = sy,
                    EndX = ex, EndY = ey,
                    TangentStartX = tx, TangentStartY = ty,
                    TangentEndX = tx, TangentEndY = ty,
                };
            }

            internal static ContourSegment Arc(double sx, double sy, double ex, double ey,
                double cx, double cy, bool isCCW,
                double tangentStartX, double tangentStartY,
                double tangentEndX, double tangentEndY)
            {
                return new ContourSegment
                {
                    Kind = SegKind.Arc,
                    StartX = sx, StartY = sy,
                    EndX = ex, EndY = ey,
                    CenterX = cx, CenterY = cy,
                    IsCCW = isCCW,
                    TangentStartX = tangentStartX, TangentStartY = tangentStartY,
                    TangentEndX = tangentEndX, TangentEndY = tangentEndY,
                };
            }
        }

        internal struct ContourInput
        {
            // One inner list = one connected piece (becomes one or more SP/EP blocks).
            public IReadOnlyList<IReadOnlyList<ContourSegment>> Pieces;
            public double SurfaceZ;
            public double Depth;
            public int Passes;
            public double Overcut;
            public double LeadIn;
            public double LeadOut;
            public int ToolNr;
            public double Tolerance;
            public string ToolType;     // "WZF" by default
            public double FeedFactor;   // 1.0 by default
        }

        /// <summary>
        /// Generate the full NC line list (tool call + per-piece SP...EP blocks).
        /// </summary>
        internal static List<string> Generate(ContourInput input)
        {
            var lines = new List<string>();
            string toolType = string.IsNullOrEmpty(input.ToolType) ? "WZF" : input.ToolType;
            double feedFactor = input.FeedFactor > 0 ? input.FeedFactor : 1.0;

            lines.Add(toolType + " (" + input.ToolNr.ToString(CultureInfo.InvariantCulture)
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            int passes = input.Passes < 1 ? 1 : input.Passes;
            double depth = Math.Abs(input.Depth);

            foreach (var piece in input.Pieces)
            {
                if (passes > 1)
                {
                    double firstZ = input.SurfaceZ - (depth / passes);
                    BuildBlock(lines, piece, firstZ, input.Tolerance, passes, input.LeadIn, input.LeadOut);
                }
                else
                {
                    BuildBlock(lines, piece, input.SurfaceZ - depth, input.Tolerance, 1, input.LeadIn, input.LeadOut);
                }
                if (input.Overcut > 0)
                    BuildBlock(lines, piece, input.SurfaceZ - depth - input.Overcut, input.Tolerance, 1, 0.0, input.LeadOut);
            }

            return lines;
        }

        /// <summary>
        /// Builds SP/(moves)/EP block(s) for one piece. Connectivity is checked
        /// segment-to-segment via tolerance — disconnected runs split into
        /// separate SP/EP blocks (matches HopContourComponent legacy behavior).
        /// </summary>
        internal static void BuildBlock(List<string> lines, IReadOnlyList<ContourSegment> flat,
            double zPlunge, double tol, int nPasses, double leadIn, double leadOut)
        {
            if (flat == null || flat.Count == 0) return;

            int gStart = 0;
            while (gStart < flat.Count)
            {
                int gEnd = gStart;
                while (gEnd + 1 < flat.Count
                       && Distance(flat[gEnd].EndX, flat[gEnd].EndY,
                                   flat[gEnd + 1].StartX, flat[gEnd + 1].StartY) <= tol * 10)
                    gEnd++;

                double startX = flat[gStart].StartX;
                double startY = flat[gStart].StartY;
                string spTail = nPasses > 1
                    ? ",2,0,_ANF,0,0,0,0,1,0," + nPasses + ",0,0,0,0,0,0,0,0,0,0)"
                    : ",2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)";

                // Lead-in: approach along reversed entry tangent
                double spX = startX;
                double spY = startY;
                if (leadIn > 0.0)
                {
                    spX = startX - flat[gStart].TangentStartX * leadIn;
                    spY = startY - flat[gStart].TangentStartY * leadIn;
                }

                lines.Add("SP (" + Fmt(spX) + "," + Fmt(spY) + "," + Fmt(zPlunge) + spTail);

                if (leadIn > 0.0)
                    lines.Add("G01 (" + Fmt(startX) + "," + Fmt(startY) + ",0,0,0,2)");

                for (int i = gStart; i <= gEnd; i++)
                {
                    var seg = flat[i];
                    if (seg.Kind == SegKind.Arc)
                    {
                        string cmd = seg.IsCCW ? "G03M" : "G02M";
                        lines.Add(cmd + " ("
                            + Fmt(seg.EndX) + "," + Fmt(seg.EndY) + ",0,"
                            + Fmt(seg.CenterX) + "," + Fmt(seg.CenterY)
                            + ",0,0,2,0)");
                    }
                    else
                    {
                        lines.Add("G01 (" + Fmt(seg.EndX) + "," + Fmt(seg.EndY) + ",0,0,0,2)");
                    }
                }

                if (leadOut > 0.0)
                {
                    double depX = flat[gEnd].EndX + flat[gEnd].TangentEndX * leadOut;
                    double depY = flat[gEnd].EndY + flat[gEnd].TangentEndY * leadOut;
                    lines.Add("G01 (" + Fmt(depX) + "," + Fmt(depY) + ",0,0,0,2)");
                }

                lines.Add("EP (0,_ANF,0)");
                gStart = gEnd + 1;
            }
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1, dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // Round to 4 decimals to prevent FP noise (3e-15) from showing up
        // as scientific notation in .hop files.
        internal static string Fmt(double v) =>
            Math.Round(v, 4).ToString(CultureInfo.InvariantCulture);
    }
}
