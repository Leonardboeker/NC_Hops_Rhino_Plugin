using System;
using System.Collections.Generic;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for vertical drilling. No Grasshopper or Rhino
    /// dependencies — testable in isolation.
    /// </summary>
    internal static class DrillLogic
    {
        internal struct Point2dz
        {
            public double X;
            public double Y;
            public double Z;
            public Point2dz(double x, double y, double z) { X = x; Y = y; Z = z; }
        }

        internal struct DrillInput
        {
            public IReadOnlyList<Point2dz> Points;
            public double Depth;
            public double Diameter;
            public double Stepdown;
            public int ToolNr;
        }

        internal struct DrillResult
        {
            public IReadOnlyList<string> Lines;
            public double SurfaceZ;
            public double CutZ;
        }

        /// <summary>
        /// Generate the WZB tool call followed by one Bohrung line per drill pass.
        /// Caller is responsible for input validation (toolNr > 0, points non-empty).
        /// </summary>
        internal static DrillResult Generate(DrillInput input)
        {
            // surfaceZ = highest Z across all input points
            double surfaceZ = input.Points[0].Z;
            for (int i = 1; i < input.Points.Count; i++)
                if (input.Points[i].Z > surfaceZ) surfaceZ = input.Points[i].Z;

            double depth = input.Depth > 0 ? input.Depth : 1.0;
            double diameter = input.Diameter > 0 ? input.Diameter : 8.0;

            var lines = new List<string>();
            lines.Add(NcDrill.ToolCall(input.ToolNr));

            if (input.Stepdown > 0)
            {
                int passCount = (int)Math.Ceiling(depth / input.Stepdown);
                foreach (var pt in input.Points)
                {
                    for (int p = 0; p < passCount; p++)
                    {
                        double passDepth = Math.Min((p + 1) * input.Stepdown, depth);
                        double cutZ = surfaceZ - passDepth;
                        lines.Add(NcDrill.DrillLine(pt.X, pt.Y, surfaceZ, cutZ, diameter));
                    }
                }
            }
            else
            {
                double cutZ = surfaceZ - Math.Abs(depth);
                foreach (var pt in input.Points)
                    lines.Add(NcDrill.DrillLine(pt.X, pt.Y, surfaceZ, cutZ, diameter));
            }

            return new DrillResult
            {
                Lines = lines,
                SurfaceZ = surfaceZ,
                CutZ = surfaceZ - Math.Abs(depth),
            };
        }
    }
}
