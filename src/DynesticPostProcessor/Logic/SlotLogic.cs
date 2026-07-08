using System;
using System.Collections.Generic;
using System.Globalization;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure NC-string generation for milled slots:
    ///  • free-slot (any direction, _nuten_frei_v5) milled with WZF
    ///  • axis-aligned groove rows (_Nuten_X_V5 / _Nuten_Y_V5)
    /// </summary>
    internal static class SlotLogic
    {
        // -----------------------------------------------------------
        // FREE SLOT (milled, single segment between two points)
        // -----------------------------------------------------------
        internal struct FreeSlotInput
        {
            public double P1X, P1Y, P1Z;
            public double P2X, P2Y, P2Z;
            public double SlotWidth;
            public double Depth;
            public int ToolNr;
            public string ToolType;     // default "WZF"
            public double FeedFactor;   // default 1.0
        }

        internal static List<string> GenerateFreeSlot(FreeSlotInput input)
        {
            string toolType = string.IsNullOrEmpty(input.ToolType) ? "WZF" : input.ToolType;
            double feedFactor = input.FeedFactor > 0 ? input.FeedFactor : 1.0;
            double depth = input.Depth > 0 ? input.Depth : 1.0;
            double topZ = Math.Max(input.P1Z, input.P2Z);
            double cutZ = topZ - Math.Abs(depth);

            var lines = new List<string>();
            lines.Add(toolType + " (" + input.ToolNr.ToString(CultureInfo.InvariantCulture)
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            // LAGE always 0 for free milled slots (blade angle is for sawing only)
            lines.Add(NcSaw.FreeSlotLine(input.P1X, input.P1Y, input.P2X, input.P2Y,
                input.SlotWidth, cutZ, 0.0));

            return lines;
        }

        // -----------------------------------------------------------
        // GROOVE ROW (axis-aligned, list of positions, _Nuten_X/Y_V5)
        // -----------------------------------------------------------
        //
        // Reference semantics (verified against 6 distinct machine-generated
        // calls in reference-hops/, e.g. HZK_Boden_Deckel_602x278.hop):
        //
        //   CALL _Nuten_X_V5 ( VAL NB:=8.25,NT:=10,EBENE:=0,ARAND:=10,
        //                            ALINKS:=0,ARECHTS:=0,RK:=0,ESMD:=1)
        //
        //   NB      groove width (saw kerf / cutter diameter)
        //   NT      groove depth, POSITIVE
        //   EBENE   0/1 reference-plane flag (NOT a Z height — machine files
        //           only ever contain 0 or 1 here)
        //   ARAND   THE POSITION: distance from the panel edge to the groove.
        //           For an X-groove this is the Y distance, for a Y-groove
        //           the X distance. There is no separate X/Y parameter.
        //   ALINKS/ARECHTS  shortening of the groove at either end
        //
        internal struct GroovePosition
        {
            public double X;
            public double Y;
            public double SurfaceZ;
        }

        internal struct GrooveInput
        {
            public bool IsXGroove;       // true = _Nuten_X_V5, false = _Nuten_Y_V5
            public IReadOnlyList<GroovePosition> Positions;
            public double Width;         // NB
            public double Depth;         // NT
            public int Ebene;            // EBENE reference-plane flag (0/1)
            public double ShortenStart;  // ALINKS
            public double ShortenEnd;    // ARECHTS
            public int ToolNr;
        }

        internal static List<string> GenerateGroove(GrooveInput input)
        {
            double width = input.Width > 0 ? input.Width : 8.0;
            double depth = input.Depth > 0 ? input.Depth : 8.0;
            int ebene = input.Ebene == 1 ? 1 : 0;
            double shortenStart = input.ShortenStart < 0 ? 0 : input.ShortenStart;
            double shortenEnd = input.ShortenEnd < 0 ? 0 : input.ShortenEnd;

            var lines = new List<string>();
            lines.Add("WZF (" + input.ToolNr + ",_VE,_V*1,_VA,_SD,0,'')");

            string macroName = input.IsXGroove ? "_Nuten_X_V5" : "_Nuten_Y_V5";

            foreach (var pos in input.Positions)
            {
                // ARAND is the groove's distance from the edge — derived from
                // the position point: Y coordinate for X-grooves (groove runs
                // along X at height Y), X coordinate for Y-grooves.
                double arand = input.IsXGroove ? pos.Y : pos.X;
                if (arand < 0) arand = 0;

                lines.Add("CALL " + macroName + "(VAL "
                    + "NB:=" + NcFmt.F(width) + ","
                    + "NT:=" + NcFmt.F(Math.Abs(depth)) + ","
                    + "EBENE:=" + ebene + ","
                    + "ARAND:=" + NcFmt.F(arand) + ","
                    + "ALINKS:=" + NcFmt.F(shortenStart) + ","
                    + "ARECHTS:=" + NcFmt.F(shortenEnd) + ","
                    + "RK:=0,ESMD:=1)");
            }

            return lines;
        }
    }
}
