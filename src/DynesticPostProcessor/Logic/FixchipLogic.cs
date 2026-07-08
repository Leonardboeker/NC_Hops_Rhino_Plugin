using System.Collections.Generic;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for Fixchip clamp positions.
    ///
    /// Reference syntax (verified against 12 machine-generated calls in
    /// reference-hops/, e.g. HZK_Boden_Deckel_602x278.hop):
    ///
    ///   /CALL Fixchip_K ( VAL SPX:=0,SPY:=60,SPZ:=9.5,WKLXY:=0)
    ///
    ///   SPX/SPY  clamp position (machine files often use DX/DY variable
    ///            expressions like SPY:=DY-60 — we emit numeric values)
    ///   SPZ      clamp height, typically HALF the plate thickness (9.5 for 19mm)
    ///   WKLXY    rotation angle in degrees (0 or 180 in reference files)
    ///   Leading '/' marks the call as a skippable/optional block that the
    ///   operator can toggle at the machine.
    /// </summary>
    internal static class FixchipLogic
    {
        internal struct FixchipInput
        {
            public IReadOnlyList<DrillLogic.Point2dz> Positions;
            public double Angle;
            /// <summary>Emit with leading '/' so the operator can skip the block at the machine.</summary>
            public bool Optional;
        }

        internal static List<string> Generate(FixchipInput input)
        {
            var lines = new List<string>();
            string prefix = input.Optional ? "/" : "";
            foreach (var pt in input.Positions)
            {
                lines.Add(prefix + "CALL Fixchip_K ( VAL "
                    + "SPX:=" + NcFmt.F(pt.X) + ","
                    + "SPY:=" + NcFmt.F(pt.Y) + ","
                    + "SPZ:=" + NcFmt.F(pt.Z) + ","
                    + "WKLXY:=" + NcFmt.F(input.Angle) + ")");
            }
            return lines;
        }
    }
}
