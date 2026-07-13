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
            // Reference semantics (verified against machine files, e.g.
            //   /CALL _Bohgx_V5 ( VAL SPY:=9.5,BIX:=50,BIIX:=220,... INKREMENT:=1)
            // BIX/BIY is the POSITION OF THE FIRST HOLE along the row axis,
            // BIIX..BIIIIX are the increments to holes 2..4, and USEn gates
            // whether hole n exists. Two old bugs fixed here:
            //  1. BIX carried spacing[0] instead of the start coordinate —
            //     the row's start position was silently dropped.
            //  2. USE2..4 were hardcoded 1 — a 0 increment drilled hole n
            //     ON TOP of the previous hole instead of skipping it.
            // Max 3 spacings (holes 2..4); extra spacings are ignored.
            var s = new double[3];
            if (input.Spacings != null)
                for (int i = 0; i < 3 && i < input.Spacings.Count; i++)
                    s[i] = input.Spacings[i];

            int holes = 1;
            for (int i = 0; i < 3; i++) if (s[i] > 0) holes++;

            double depth = input.Depth > 0 ? input.Depth : 13.0;
            double diameter = input.Diameter > 0 ? input.Diameter : 5.0;
            double cutZ = input.StartZ - System.Math.Abs(depth);
            int spiegel = input.Mirror ? 1 : 0;
            double alongStart = input.IsXRow ? input.StartX : input.StartY;
            double crossPos   = input.IsXRow ? input.StartY : input.StartX;

            var lines = new List<string>();
            lines.Add("WZB (" + input.ToolNr + ",_VE,_V*1,_VA,_SD,0,'')");

            string ax = input.IsXRow ? "X" : "Y";
            lines.Add("CALL _Bohg" + (input.IsXRow ? "x" : "y") + "_V5(VAL "
                + "SP" + (input.IsXRow ? "Y" : "X") + ":=" + NcFmt.F(crossPos) + ","
                + "BI" + ax + ":=" + NcFmt.F(alongStart) + ","
                + "BII" + ax + ":=" + NcFmt.F(s[0]) + ","
                + "BIII" + ax + ":=" + NcFmt.F(s[1]) + ","
                + "BIIII" + ax + ":=" + NcFmt.F(s[2]) + ","
                + "SPIEGELN:=" + spiegel + ","
                + "T:=" + NcFmt.F(cutZ) + ","
                + "D:=" + NcFmt.F(diameter) + ","
                + "TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1,"
                + "USE2:=" + (s[0] > 0 ? 1 : 0) + ","
                + "USE3:=" + (s[1] > 0 ? 1 : 0) + ","
                + "USE4:=" + (s[2] > 0 ? 1 : 0) + ")");

            return new DrillRowResult { Lines = lines, HoleCount = holes };
        }
    }
}
