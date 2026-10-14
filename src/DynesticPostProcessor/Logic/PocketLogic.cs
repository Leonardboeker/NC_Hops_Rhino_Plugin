using System;
using System.Collections.Generic;
using System.Globalization;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for pocket macros: rectangular
    /// (_Rechteck_V7) and circular (_Kreistasche_V5).
    /// </summary>
    internal static class PocketLogic
    {
        internal struct RectPocketInput
        {
            public double CenterX;
            public double CenterY;
            public double SurfaceZ;
            public double Width;       // X dimension (LAENGE)
            public double Height;      // Y dimension (BREITE)
            public double CornerRadius;
            public double Angle;
            public double Depth;
            public double Stepdown;
            public int ToolNr;
            public string ToolType;    // default WZF
            public double FeedFactor;  // default 1.0
            // ESMD in the macro: which plane the depth counts from.
            // false = part TOP  (a 5 mm pocket is TIEFE:=-5)
            // true  = machine TABLE at Z=0 (a through cut is TIEFE:=-0.2)
            // Machine-confirmed 2026-08-10 by diffing a hand-corrected file:
            // ESMD was the only parameter that changed besides the depth.
            public bool DepthFromTable;
        }

        internal static List<string> GenerateRect(RectPocketInput input)
        {
            string toolType = string.IsNullOrEmpty(input.ToolType) ? "WZF" : input.ToolType;
            double feedFactor = input.FeedFactor > 0 ? input.FeedFactor : 1.0;
            double depth = input.Depth > 0 ? input.Depth : 1.0;
            double cornerRadius = input.CornerRadius < 0 ? 0 : input.CornerRadius;
            // Machine-verified 2026-08-10: the mill-macro family (TIEFE/Tiefe)
            // is referenced from the part TOP, negative down (reference file
            // Muster_DXF_Import.hop: -10/-20/-5; a through cut emitted as 0 cut
            // NOTHING on the HolzHer). Drawing Z must not leak in here -- the
            // contour/saw/drill emitters use bed-absolute Z, this family not.
            double cutZ = -Math.Abs(depth);
            double stepdownVal = input.Stepdown > 0 ? input.Stepdown : 0;

            var lines = new List<string>();
            lines.Add(toolType + " (" + input.ToolNr.ToString(CultureInfo.InvariantCulture)
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            lines.Add("CALL _Rechteck_V7(VAL "
                + "X_MITTE:=" + Fmt(input.CenterX) + ","
                + "Y_MITTE:=" + Fmt(input.CenterY) + ","
                + "LAENGE:=" + Fmt(input.Width) + ","
                + "BREITE:=" + Fmt(input.Height) + ","
                + "RADIUS:=" + Fmt(cornerRadius) + ","
                + "WINKEL:=" + Fmt(input.Angle) + ","
                + "TIEFE:=" + Fmt(cutZ) + ","
                + "ZUTIEFE:=" + Fmt(stepdownVal) + ","
                + "RADIUSKORREKTUR:=2,"
                + "AB:=2,AUFMASS:=0,ANF:=_ANF,ABF:=_ANF,"
                + "UMKEHREN:=0,RAMPE:=0,RAMPENLAENGE:=50,QUADRANT:=1,"
                + "INTERPOL:=1,ESXY:=0,ESMD:="
                + (input.DepthFromTable ? "1" : "0")
                + ",LASER:=0)");

            return lines;
        }

        internal struct CircPocketInput
        {
            public double CenterX;
            public double CenterY;
            public double SurfaceZ;
            public double Radius;
            public double Depth;
            public double Stepdown;
            public int ToolNr;
            public string ToolType;
            public double FeedFactor;
        }

        internal static List<string> GenerateCirc(CircPocketInput input)
        {
            string toolType = string.IsNullOrEmpty(input.ToolType) ? "WZF" : input.ToolType;
            double feedFactor = input.FeedFactor > 0 ? input.FeedFactor : 1.0;
            double depth = input.Depth > 0 ? input.Depth : 1.0;
            // Machine-verified 2026-08-10: the mill-macro family (TIEFE/Tiefe)
            // is referenced from the part TOP, negative down (reference file
            // Muster_DXF_Import.hop: -10/-20/-5; a through cut emitted as 0 cut
            // NOTHING on the HolzHer). Drawing Z must not leak in here -- the
            // contour/saw/drill emitters use bed-absolute Z, this family not.
            double cutZ = -Math.Abs(depth);
            double stepdownVal = input.Stepdown > 0 ? input.Stepdown : 0;

            var lines = new List<string>();
            lines.Add(toolType + " (" + input.ToolNr.ToString(CultureInfo.InvariantCulture)
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            lines.Add("CALL _Kreistasche_V5(VAL "
                + "X_Mitte:=" + NcFmt.F(input.CenterX) + ","
                + "Y_Mitte:=" + NcFmt.F(input.CenterY) + ","
                + "Radius:=" + NcFmt.F(input.Radius) + ","
                + "Tiefe:=" + NcFmt.F(cutZ) + ","
                + "Zustellung:=" + NcFmt.F(stepdownVal) + ","
                + "AB:=2,ABF:=_ANF,Interpol:=0,umkehren:=0,esxy:=0,esmd:=0,laser:=0)");

            return lines;
        }

        // Single plugin-wide formatter — see NcFmt.F
        private static string Fmt(double v) => NcFmt.F(v);
    }
}
