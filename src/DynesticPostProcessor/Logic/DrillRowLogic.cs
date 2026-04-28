using System.Collections.Generic;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for parametric drill rows
    /// (_Bohgx_V5 / _Bohgy_V5 macros).
    /// </summary>
    internal static class DrillRowLogic
    {
        internal struct DrillRowInput
        {
            public bool IsXRow;          // true = _Bohgx_V5, false = _Bohgy_V5
            public double StartX;
            public double StartY;
            public double StartZ;
            public IReadOnlyList<double> Spacings;  // padded to 4 values internally
            public double Depth;
            public double Diameter;
            public bool Mirror;
            public int ToolNr;
        }

        internal struct DrillRowResult
        {
            public IReadOnlyList<string> Lines;
            public int HoleCount;
        }

        internal static DrillRowResult Generate(DrillRowInput input)
        {
            // Pad spacings to 4
            var s = new double[4];
            if (input.Spacings != null)
                for (int i = 0; i < 4 && i < input.Spacings.Count; i++)
                    s[i] = input.Spacings[i];

            // Count holes (start + each non-zero spacing)
            int holes = 1;
            for (int i = 0; i < 4; i++) if (s[i] > 0) holes++;

            double depth = input.Depth > 0 ? input.Depth : 13.0;
            double diameter = input.Diameter > 0 ? input.Diameter : 5.0;
            double cutZ = input.StartZ - System.Math.Abs(depth);
            int spiegel = input.Mirror ? 1 : 0;

            var lines = new List<string>();
            lines.Add("WZB (" + input.ToolNr + ",_VE,_V*1,_VA,_SD,0,'')");

            string macro;
            if (input.IsXRow)
            {
                macro = "CALL _Bohgx_V5(VAL "
                    + "SPY:=" + NcFmt.F(input.StartY) + ","
                    + "BIX:=" + NcFmt.F(s[0]) + ","
                    + "BIIX:=" + NcFmt.F(s[1]) + ","
                    + "BIIIX:=" + NcFmt.F(s[2]) + ","
                    + "BIIIIX:=" + NcFmt.F(s[3]) + ","
                    + "SPIEGELN:=" + spiegel + ","
                    + "T:=" + NcFmt.F(cutZ) + ","
                    + "D:=" + NcFmt.F(diameter) + ","
                    + "TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1,"
                    + "USE2:=1,USE3:=1,USE4:=1)";
            }
            else
            {
                macro = "CALL _Bohgy_V5(VAL "
                    + "SPX:=" + NcFmt.F(input.StartX) + ","
                    + "BIY:=" + NcFmt.F(s[0]) + ","
                    + "BIIY:=" + NcFmt.F(s[1]) + ","
                    + "BIIIY:=" + NcFmt.F(s[2]) + ","
                    + "BIIIIY:=" + NcFmt.F(s[3]) + ","
                    + "SPIEGELN:=" + spiegel + ","
                    + "T:=" + NcFmt.F(cutZ) + ","
                    + "D:=" + NcFmt.F(diameter) + ","
                    + "TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1,"
                    + "USE2:=1,USE3:=1,USE4:=1)";
            }
            lines.Add(macro);

            return new DrillRowResult { Lines = lines, HoleCount = holes };
        }
    }
}
