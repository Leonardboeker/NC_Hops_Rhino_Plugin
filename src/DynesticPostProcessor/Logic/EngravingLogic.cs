using System.Collections.Generic;
using System.Globalization;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for shallow engraving paths. Re-uses the
    /// SP/G01/G02M/G03M/EP block builder from ContourLogic — engraving is
    /// effectively single-pass contour with no kerf offset and no lead-in/out.
    /// One SP/EP block per input curve, plus optional sub-blocks for any
    /// disconnected segment runs within a curve.
    /// </summary>
    internal static class EngravingLogic
    {
        internal struct EngravingInput
        {
            public IReadOnlyList<IReadOnlyList<ContourLogic.ContourSegment>> Curves;
            public IReadOnlyList<double> SurfaceZPerCurve; // matches Curves.Count
            public double Depth;
            public int ToolNr;
            public double Tolerance;
            public string ToolType;     // default "WZF"
            public double FeedFactor;   // default 1.0
        }

        internal static List<string> Generate(EngravingInput input)
        {
            var lines = new List<string>();
            string toolType = string.IsNullOrEmpty(input.ToolType) ? "WZF" : input.ToolType;
            double feedFactor = input.FeedFactor > 0 ? input.FeedFactor : 1.0;
            double depth = System.Math.Abs(input.Depth);

            lines.Add(toolType + " (" + input.ToolNr.ToString(CultureInfo.InvariantCulture)
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            for (int i = 0; i < input.Curves.Count; i++)
            {
                double surfaceZ = i < input.SurfaceZPerCurve.Count
                    ? input.SurfaceZPerCurve[i] : 0.0;
                double cutZ = surfaceZ - depth;

                ContourLogic.BuildBlock(lines, input.Curves[i], cutZ,
                    input.Tolerance, nPasses: 1, leadIn: 0.0, leadOut: 0.0);
            }

            return lines;
        }
    }
}
