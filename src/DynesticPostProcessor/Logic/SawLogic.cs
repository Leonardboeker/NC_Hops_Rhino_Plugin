using System;
using System.Collections.Generic;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for saw cuts. No Grasshopper/Rhino types.
    /// Caller supplies line endpoints as plain Point2dz tuples.
    /// </summary>
    internal static class SawLogic
    {
        internal struct LineSegment
        {
            public DrillLogic.Point2dz Start;
            public DrillLogic.Point2dz End;
            public LineSegment(DrillLogic.Point2dz s, DrillLogic.Point2dz e) { Start = s; End = e; }
        }

        internal struct SawInput
        {
            public IReadOnlyList<LineSegment> Segments;
            public IReadOnlyList<double> BladeAngles;  // single value or matching list; one entry minimum
            public double Length;       // 600 default → use actual segment length when at default
            public double SawKerf;
            public double Depth;
            public int Side;            // -1 left, 0 center, +1 right
            public double Extend;
            public int ToolNr;
        }

        internal struct SegmentResult
        {
            public double CutP1X;
            public double CutP1Y;
            public double CutP2X;
            public double CutP2Y;
            public double CutZ;
            public double CutLength;
            public double BladeAngle;
            public bool Skipped;
            public string SkipReason;
        }

        internal struct SawResult
        {
            public IReadOnlyList<string> Lines;
            public IReadOnlyList<SegmentResult> Segments;
        }

        /// <summary>
        /// Generate WZS tool call + nuten_frei_v5 lines for each non-degenerate segment.
        /// </summary>
        internal static SawResult Generate(SawInput input)
        {
            var lines = new List<string>();
            var segments = new List<SegmentResult>();

            int side = input.Side;
            if (side > 1) side = 1;
            if (side < -1) side = -1;

            double length = input.Length > 0 ? input.Length : 600.0;
            double depth = input.Depth > 0 ? input.Depth : 19.0;
            double extend = input.Extend < 0 ? 0 : input.Extend;
            double kerf = input.SawKerf;

            var angles = (input.BladeAngles == null || input.BladeAngles.Count == 0)
                ? new double[] { 0.0 }
                : (IReadOnlyList<double>)input.BladeAngles;

            for (int i = 0; i < input.Segments.Count; i++)
            {
                var seg = input.Segments[i];
                double bladeAngle = angles[i % angles.Count];

                // Travel direction (XY only — Z is plate surface)
                double dx = seg.End.X - seg.Start.X;
                double dy = seg.End.Y - seg.Start.Y;
                double dz = seg.End.Z - seg.Start.Z;
                double segLen2D = Math.Sqrt(dx * dx + dy * dy);

                if (segLen2D < 0.001)
                {
                    segments.Add(new SegmentResult
                    {
                        Skipped = true,
                        SkipReason = "Zero-length curve at index " + i,
                        BladeAngle = bladeAngle,
                    });
                    continue;
                }

                double travelX = dx / segLen2D;
                double travelY = dy / segLen2D;

                // Origin = midpoint
                double ox = (seg.Start.X + seg.End.X) / 2.0;
                double oy = (seg.Start.Y + seg.End.Y) / 2.0;
                double oz = (seg.Start.Z + seg.End.Z) / 2.0;

                // travelPerp = travelDir × Z (in XY plane)
                double perpX = travelY * 1.0 - 0.0;   // (travelDir × ZAxis).x = travelY * Zz - travelZ * Yz
                double perpY = 0.0 - travelX * 1.0;   // .y = travelZ * Xz - travelX * Zz  → -travelX
                double perpLen = Math.Sqrt(perpX * perpX + perpY * perpY);
                if (perpLen > 0)
                {
                    perpX /= perpLen;
                    perpY /= perpLen;
                }

                // Cut length: default-detection — if length is at the 600 default, use actual segment length
                double cutLength = length;
                if (Math.Abs(length - 600.0) < 0.001)
                {
                    double segLen3D = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (segLen3D > 1.0) cutLength = segLen3D;
                }

                // Side offset
                double sideShift = side * (kerf / 2.0);
                double sideVecX = perpX * sideShift;
                double sideVecY = perpY * sideShift;

                double halfLen = cutLength / 2.0;
                double p1x = ox - travelX * halfLen + sideVecX;
                double p1y = oy - travelY * halfLen + sideVecY;
                double p2x = ox + travelX * halfLen + sideVecX;
                double p2y = oy + travelY * halfLen + sideVecY;

                // Extend past endpoints
                double cutP1x = extend > 0.001 ? p1x - travelX * extend : p1x;
                double cutP1y = extend > 0.001 ? p1y - travelY * extend : p1y;
                double cutP2x = extend > 0.001 ? p2x + travelX * extend : p2x;
                double cutP2y = extend > 0.001 ? p2y + travelY * extend : p2y;

                double cutZ = oz - Math.Abs(depth);

                lines.Add(NcSaw.ToolCall(input.ToolNr));
                lines.Add(NcSaw.FreeSlotLine(cutP1x, cutP1y, cutP2x, cutP2y, kerf, cutZ, bladeAngle));

                segments.Add(new SegmentResult
                {
                    CutP1X = cutP1x, CutP1Y = cutP1y,
                    CutP2X = cutP2x, CutP2Y = cutP2y,
                    CutZ = cutZ, CutLength = cutLength,
                    BladeAngle = bladeAngle,
                    Skipped = false,
                });
            }

            return new SawResult { Lines = lines, Segments = segments };
        }
    }
}
