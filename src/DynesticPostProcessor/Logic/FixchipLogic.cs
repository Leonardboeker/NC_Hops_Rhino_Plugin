using System.Collections.Generic;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for Fixchip clamp positions
    /// (Fixchip_K macro). One line per clamp.
    /// </summary>
    internal static class FixchipLogic
    {
        internal struct FixchipInput
        {
            public IReadOnlyList<DrillLogic.Point2dz> Positions;
            public double Angle;
        }

        internal static List<string> Generate(FixchipInput input)
        {
            var lines = new List<string>();
            foreach (var pt in input.Positions)
            {
                lines.Add("Fixchip_K ("
                    + NcFmt.F(pt.X) + ","
                    + NcFmt.F(pt.Y) + ","
                    + NcFmt.F(pt.Z) + ","
                    + NcFmt.F(input.Angle) + ")");
            }
            return lines;
        }
    }
}
