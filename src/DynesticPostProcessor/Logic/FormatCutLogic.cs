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
        }

        internal static List<string> Generate(FormatCutInput input)
        {
            double thickness = input.Thickness > 0 ? input.Thickness : 19.0;

            var lines = new List<string>();
            lines.Add("WZS (" + input.ToolNr + ",_VE,_V*0.3,_VA,_SD,0,'')");

            foreach (var pos in input.Positions)
            {
                double cutZ = pos.SurfaceZ - Math.Abs(thickness);
                string macro;
                if (input.IsXCut)
                {
                    // _saege_x_V7: travels in X, fixed Y position
                    macro = "CALL _saege_x_V7(VAL "
                        + "SX:=0,"
                        + "SY:=" + NcFmt.F(pos.Y) + ","
                        + "SZ:=" + NcFmt.F(cutZ) + ","
                        + (input.LengthOverride > 0
                            ? "EX:=" + NcFmt.F(input.LengthOverride) + ","
                            : "EX:=0,")
                        + "EZ:=" + NcFmt.F(-0.2) + ","
                        + "BL:=2,"
                        + "EINPASSEN:=0,EL:=0,AL:=0,PARALLEL:=0,"
                        + "K:=2,"
                        + "KW:=" + NcFmt.F(input.Kw) + ","
                        + "BH:=0,RITZVERSATZ:=0.05,ESZ:=0,ESXY1:=1,ESX:=3)";
                }
                else
                {
                    // _saege_y_V7: travels in Y, fixed X position
                    macro = "CALL _saege_y_V7(VAL "
                        + "SX:=" + NcFmt.F(pos.X) + ","
                        + "SY:=0,"
                        + "SZ:=" + NcFmt.F(cutZ) + ","
                        + (input.LengthOverride > 0
                            ? "EY:=" + NcFmt.F(input.LengthOverride) + ","
                            : "EY:=0,")
                        + "EZ:=" + NcFmt.F(-0.2) + ","
                        + "BL:=2,"
                        + "EINPASSEN:=0,EL:=0,AL:=0,PARALLEL:=0,"
                        + "K:=2,"
                        + "KW:=" + NcFmt.F(input.Kw) + ","
                        + "BH:=0,RITZVERSATZ:=0.05,ESZ:=0,ESXY1:=1,ESX:=3)";
                }
                lines.Add(macro);
            }

            return lines;
        }
    }
}
