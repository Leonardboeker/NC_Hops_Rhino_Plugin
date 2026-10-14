using System;
using System.Collections.Generic;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for format saw cuts (_saege_x_V7 / _saege_y_V7).
    /// </summary>
    internal static class FormatCutLogic
    {
        internal struct CutPosition
        {
            public double X;
            public double Y;
            public double SurfaceZ;
        }

        internal struct FormatCutInput
        {
            public bool IsXCut;            // true = _saege_x_V7, false = _saege_y_V7
            public IReadOnlyList<CutPosition> Positions;
            public double Thickness;       // material thickness = cut depth
            public double Kw;              // bevel/miter angle (degrees)
            public double LengthOverride;  // 0 = use plate dimensions
            public int ToolNr;
            // K in the macro: 0 = Gleichlauf GL, 1 = Gegenlauf GGL,
            // 2 = Vorritzen GL, 3 = Vorritzen GGL (HOPS dialog radio order).
            // The emitter used to hardcode 2, forcing a scoring pass nobody asked for.
            public int CutMode;
        }

        internal static List<string> Generate(FormatCutInput input)
        {
            double thickness = input.Thickness > 0 ? input.Thickness : 19.0;

            var lines = new List<string>();
            lines.Add("WZS (" + input.ToolNr + ",_VE,_V*0.3,_VA,_SD,0,'')");

            foreach (var pos in input.Positions)
            {
                // Parameter meaning, read off the HOPS dialog 2026-08-10 (NOT the
                // "start/end Z" the names suggest):
                //   EZ -> "Schnitttiefe (L3)"  the actual cut depth
                //   SZ -> "Ritztiefe"          scoring depth, only used when scoring is on
                // Reference height is the part underside, so EZ = -0.2 cuts 0.2 mm past
                // it and severs the part -- machine-confirmed. SZ = -3 matches the
                // reference files and is inert unless CutMode selects a scoring pass.
                double scoreZ = pos.SurfaceZ - Math.Abs(thickness) - 3.0;
                string macro;
                if (input.IsXCut)
                {
                    // _saege_x_V7: travels in X, fixed Y position
                    macro = "CALL _saege_x_V7(VAL "
                        + "SX:=0,"
                        + "SY:=" + NcFmt.F(pos.Y) + ","
                        + "SZ:=" + NcFmt.F(scoreZ) + ","
                        + (input.LengthOverride > 0
                            ? "EX:=" + NcFmt.F(input.LengthOverride) + ","
                            : "EX:=0,")
                        + "EZ:=" + NcFmt.F(-0.2) + ","
                        + "BL:=2,"
                        + "EINPASSEN:=0,EL:=0,AL:=0,PARALLEL:=0,"
                        + "K:=" + input.CutMode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
                        + "KW:=" + NcFmt.F(input.Kw) + ","
                        + "BH:=0,RITZVERSATZ:=0.05,ESZ:=0,ESXY1:=1,ESX:=3)";
                }
                else
                {
                    // _saege_y_V7: travels in Y, fixed X position.
                    // Tail param is ESY here, not ESX -- the machine's macro
                    // rejects unknown variables outright (HOPS error [ESX],
                    // observed 2026-08-10 on the first Saege_Y line).
                    macro = "CALL _saege_y_V7(VAL "
                        + "SX:=" + NcFmt.F(pos.X) + ","
                        + "SY:=0,"
                        + "SZ:=" + NcFmt.F(scoreZ) + ","
                        + (input.LengthOverride > 0
                            ? "EY:=" + NcFmt.F(input.LengthOverride) + ","
                            : "EY:=0,")
                        + "EZ:=" + NcFmt.F(-0.2) + ","
                        + "BL:=2,"
                        + "EINPASSEN:=0,EL:=0,AL:=0,PARALLEL:=0,"
                        + "K:=" + input.CutMode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
                        + "KW:=" + NcFmt.F(input.Kw) + ","
                        + "BH:=0,RITZVERSATZ:=0.05,ESZ:=0,ESXY1:=1,ESY:=3)";
                }
                lines.Add(macro);
            }

            return lines;
        }
    }
}
