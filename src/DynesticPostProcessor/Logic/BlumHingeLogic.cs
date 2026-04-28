using System;
using System.Collections.Generic;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for Blum cup hinge drilling (_Topf_V5).
    /// One macro call per hinge position; component layer is responsible
    /// for emitting the WZB tool call once, then iterating positions.
    /// </summary>
    internal static class BlumHingeLogic
    {
        internal struct HingePosition
        {
            public double X;
            public double Y;
            public double SurfaceZ;
        }

        internal struct BlumHingeInput
        {
            public IReadOnlyList<HingePosition> Positions;
            public double Distance;        // edge → cup center (DISTANCE)
            public int Side;               // 0 front, 1 back (SEITE)
            public double CupDiameter;     // TOPF_D, default 35
            public double CupDepth;        // TOPF_T, default 12.8
            public double DowelDiameter;   // DUEBEL_D, default 8
            public double DowelDepth;      // DUEBEL_T, default 13
            public int ToolNr;
        }

        internal static List<string> Generate(BlumHingeInput input)
        {
            double distance = input.Distance > 0 ? input.Distance : 22.5;
            double cupD = input.CupDiameter > 0 ? input.CupDiameter : 35.0;
            double cupT = input.CupDepth > 0 ? input.CupDepth : 12.8;

            var lines = new List<string>();
            lines.Add("WZB (" + input.ToolNr + ",_VE,_V*1,_VA,_SD,0,'')");

            foreach (var pos in input.Positions)
            {
                lines.Add("CALL _Topf_V5(VAL "
                    + "SEITE:=" + input.Side + ","
                    + "DISTANCE:=" + NcFmt.F(distance) + ","
                    + "POS1:=" + NcFmt.F(pos.Y) + ","
                    + "POS2:=0,POS3:=0,POS4:=0,"
                    + "A:=9.5,B:=45,"
                    + "TOPF_D:=" + NcFmt.F(cupD) + ","
                    + "TOPF_T:=" + NcFmt.F(-Math.Abs(cupT)) + ","
                    + "DUEBEL_D:=" + NcFmt.F(input.DowelDiameter) + ","
                    + "DUEBEL_T:=" + NcFmt.F(-Math.Abs(input.DowelDepth)) + ","
                    + "ESX1:=0,ESX2:=0,ESX3:=0,ESX4:=0,"
                    + "ESY1:=0,ESY2:=0,ESY3:=0,ESY4:=0,"
                    + "USE2:=0,USE3:=0,USE4:=0)");
            }

            return lines;
        }
    }
}
