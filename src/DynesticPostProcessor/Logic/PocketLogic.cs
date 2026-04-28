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
        }

        internal static List<string> GenerateRect(RectPocketInput input)
        {
            string toolType = string.IsNullOrEmpty(input.ToolType) ? "WZF" : input.ToolType;
            double feedFactor = input.FeedFactor > 0 ? input.FeedFactor : 1.0;
            double depth = input.Depth > 0 ? input.Depth : 1.0;
            double cornerRadius = input.CornerRadius < 0 ? 0 : input.CornerRadius;
            double cutZ = input.SurfaceZ - Math.Abs(depth);
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
                + "INTERPOL:=1,ESXY:=0,ESMD:=0,LASER:=0)");

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
            double cutZ = input.SurfaceZ - Math.Abs(depth);
            double stepdownVal = input.Stepdown > 0 ? input.Stepdown : 0;

            var lines = new List<string>();
            lines.Add(toolType + " (" + input.ToolNr.ToString(CultureInfo.InvariantCulture)
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            // Note: existing component uses raw .ToString here, not Fmt — preserved for output stability
            lines.Add("CALL _Kreistasche_V5(VAL "
                + "X_Mitte:=" + input.CenterX.ToString(CultureInfo.InvariantCulture) + ","
                + "Y_Mitte:=" + input.CenterY.ToString(CultureInfo.InvariantCulture) + ","
                + "Radius:=" + input.Radius.ToString(CultureInfo.InvariantCulture) + ","
                + "Tiefe:=" + cutZ.ToString(CultureInfo.InvariantCulture) + ","
                + "Zustellung:=" + stepdownVal.ToString(CultureInfo.InvariantCulture) + ","
                + "AB:=2,ABF:=_ANF,Interpol:=0,umkehren:=0,esxy:=0,esmd:=0,laser:=0)");

            return lines;
        }

        private static string Fmt(double v) =>
            Math.Round(v, 4).ToString(CultureInfo.InvariantCulture);
    }
}
