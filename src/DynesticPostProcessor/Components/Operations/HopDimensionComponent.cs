using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Operations
{
    /// <summary>
    /// Generates dimension line markup (B2Punkte_V7) for the DYNESTIC CNC.
    /// B2Punkte creates visual dimension arrows/lines in the NC program for
    /// reference measurement display on the machine controller.
    /// Input: two points defining the measured distance.
    /// </summary>
    public class HopDimensionComponent : GH_Component
    {
        public HopDimensionComponent() : base(
            "HopDimension", "HopDimension",
            "Generates dimension line markup (B2Punkte_V7) for the DYNESTIC CNC.\n\n" +
            "Creates visual dimension arrows between two points, displayed on the machine controller.\n" +
            "Typical use: reference dimensions for setup verification.",
            "DYNESTIC", "Utility") { }

        public override Guid ComponentGuid => new Guid("8f3061a2-93b4-1234-f012-345678901234");

        protected override Bitmap Icon => IconHelper.Load("HopDimension");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 - StartPoint
            pManager.AddPointParameter("StartPoint", "startPoint",
                "First dimension point (P1). Defines one end of the measured distance.",
                GH_ParamAccess.item);

            // 1 - EndPoint
            pManager.AddPointParameter("EndPoint", "endPoint",
                "Second dimension point (P2). Defines the other end of the measured distance.",
                GH_ParamAccess.item);

            // 2 - Offset
            pManager.AddNumberParameter("Offset", "offset",
                "Perpendicular offset of the dimension line from the measured axis in mm (ABSTAND). Default 20.",
                GH_ParamAccess.item, 20.0);
            pManager[2].Optional = true;

            // 3 - Label text
            pManager.AddTextParameter("Label", "label",
                "Optional text label for the dimension (TEXT). Leave empty for no text.",
                GH_ParamAccess.item, "");
            pManager[3].Optional = true;

            // 4 - TextHeight
            pManager.AddNumberParameter("TextHeight", "textHeight",
                "Dimension text height in mm (TEXTHOEHE). Default 20.",
                GH_ParamAccess.item, 20.0);
            pManager[4].Optional = true;

            // 5 - Colour index
            pManager.AddIntegerParameter("ColorIndex", "colorIndex",
                "Colour index for the dimension line (FARBE). 0 = default machine colour.",
                GH_ParamAccess.item, 0);
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops B2Punkte_V7 macro strings. Wire into HopExport or HopPart.",
                GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Point3d startPoint = Point3d.Origin;
            Point3d endPoint   = Point3d.Origin;
            double offset = 20.0;
            string label = "";
            double textHeight = 20.0;
            int colorIndex = 0;

            if (!DA.GetData(0, ref startPoint)) return;
            if (!DA.GetData(1, ref endPoint)) return;
            DA.GetData(2, ref offset);
            DA.GetData(3, ref label);
            DA.GetData(4, ref textHeight);
            DA.GetData(5, ref colorIndex);

            if (textHeight <= 0) textHeight = 20.0;

            // Sanitize label (no single quotes)
            string safeLabel = label == null ? "" : label.Replace("'", "").Trim();

            double dist = startPoint.DistanceTo(endPoint);

            string macro = "CALL B2Punkte_V7(VAL "
                + "P1X:=" + NcFmt.F(startPoint.X) + ","
                + "P1Y:=" + NcFmt.F(startPoint.Y) + ","
                + "P2X:=" + NcFmt.F(endPoint.X) + ","
                + "P2Y:=" + NcFmt.F(endPoint.Y) + ","
                + "ABSTAND:=" + NcFmt.F(offset) + ","
                + "FARBE:=" + colorIndex + ","
                + "TEXT:='" + safeLabel + "',"
                + "TEXTHOEHE:=" + NcFmt.F(textHeight) + ","
                + "PFEILLAENGE:=30,PFEILEINNERHALB:=0,"
                + "WCS:=1,ESXY1:=0,ESXY2:=0)";

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Dimension: " + dist.ToString("F2", CultureInfo.InvariantCulture) + " mm"
                + "  P1=(" + NcFmt.F(startPoint.X) + "," + NcFmt.F(startPoint.Y) + ")"
                + "  P2=(" + NcFmt.F(endPoint.X) + "," + NcFmt.F(endPoint.Y) + ")");

            DA.SetDataList(0, new List<string> { macro });
        }

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.Point(),
                DynesticPostProcessor.AutoWire.Spec.Point(),
                DynesticPostProcessor.AutoWire.Spec.Float("0<20<200"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
                DynesticPostProcessor.AutoWire.Spec.Float("5<20<100"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
