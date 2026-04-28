using System;
using System.Collections.Generic;
using System.Globalization;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for circular path cuts (_Kreisbahn_V5).
    /// </summary>
    internal static class CircPathLogic
    {
        internal struct CircPathInput
        {
            public double CenterX;
            public double CenterY;
            public double SurfaceZ;
            public double Radius;
            public int RadiusCorr;     // -1 outside, 0 center, +1 inside
            public double Depth;
            public double Stepdown;
            public double Angle;       // arc angle in degrees, default 360
            public int ToolNr;
            public string ToolType;    // default WZF
            public double FeedFactor;  // default 1.0
        }

        internal static List<string> Generate(CircPathInput input)
        {
            string toolType = string.IsNullOrEmpty(input.ToolType) ? "WZF" : input.ToolType;
            double feedFactor = input.FeedFactor > 0 ? input.FeedFactor : 1.0;
            double depth = input.Depth > 0 ? input.Depth : 1.0;
            double angle = input.Angle > 0 ? input.Angle : 360.0;
            double cutZ = input.SurfaceZ - Math.Abs(depth);
            double stepdownVal = input.Stepdown > 0 ? input.Stepdown : 0;

            var lines = new List<string>();
            lines.Add(toolType + " (" + input.ToolNr.ToString(CultureInfo.InvariantCulture)
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            // Note: existing component uses raw .ToString — preserved
            lines.Add("CALL _Kreisbahn_V5(VAL "
                + "X_Mitte:=" + input.CenterX.ToString(CultureInfo.InvariantCulture) + ","
                + "Y_Mitte:=" + input.CenterY.ToString(CultureInfo.InvariantCulture) + ","
                + "Tiefe:=" + cutZ.ToString(CultureInfo.InvariantCulture) + ","
                + "ZuTiefe:=" + stepdownVal.ToString(CultureInfo.InvariantCulture) + ","
                + "Radius:=" + input.Radius.ToString(CultureInfo.InvariantCulture) + ","
                + "Radiuskorrektur:=" + input.RadiusCorr.ToString(CultureInfo.InvariantCulture) + ","
                + "AB:=1,Aufmass:=0,Bearb_umkehren:=1,"
                + "Winkel:=" + angle.ToString(CultureInfo.InvariantCulture) + ","
                + "ANF:=_ANF,ABF:=_ANF,Rampe:=1,Interpol:=0,esxy:=0,esmd:=0,laser:=0)");

            return lines;
        }
    }
}
