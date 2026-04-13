using System;
using System.Collections.Generic;
using System.Drawing;

using Rhino.Geometry;

using Grasshopper.Kernel;

namespace WallabyHop.Components.Nesting
{
    /// <summary>
    /// Generates the complete nesting system block for the DYNESTIC CNC:
    ///   Park_V7 + BN_NestKontur + BN_TrennerInnenAussen + HH_MarkLabel
    /// These four macros always appear together in nesting .hop files (58% of reference files).
    /// Park_V7 moves the machine to park position after all cuts.
    /// BN_NestKontur / BN_TrennerInnenAussen define nesting contour and inner/outer separation.
    /// HH_MarkLabel triggers the label printer at the configured position.
    /// </summary>
    public class HopNestingComponent : GH_Component
    {
        public HopNestingComponent() : base(
            "HopNesting", "HopNesting",
            "Generates the nesting system block for the DYNESTIC CNC.\n\n" +
            "Outputs Park_V7 + BN_NestKontur + BN_TrennerInnenAussen + HH_MarkLabel in the correct order.\n" +
            "Wire output into HopExport OperationLines (append after all cutting operations).",
            "Wallaby Hop", "Nesting") { }

        public override Guid ComponentGuid => new Guid("9041a2b3-04c5-2345-0123-456789012345");

        protected override Bitmap Icon => IconHelper.Load("HopNesting");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 - LabelPosX
            pManager.AddNumberParameter("LabelPosX", "labelPosX",
                "X position of the label printer head (_hhdata_LabelPosX override). 0 = use machine default.",
                GH_ParamAccess.item, 0.0);
            pManager[0].Optional = true;

            // 1 - LabelPosY
            pManager.AddNumberParameter("LabelPosY", "labelPosY",
                "Y position of the label printer head (_hhdata_LabelPosY override). 0 = use machine default.",
                GH_ParamAccess.item, 0.0);
            pManager[1].Optional = true;

            // 2 - LabelAngle
            pManager.AddNumberParameter("LabelAngle", "labelAngle",
                "Label printer rotation angle in degrees. 0 = no rotation.",
                GH_ParamAccess.item, 0.0);
            pManager[2].Optional = true;

            // 3 - ParkMode
            pManager.AddIntegerParameter("ParkMode", "parkMode",
                "Park position mode for Park_V7.\n2 = standard park (default)\n0 = custom position (requires ParkX/ParkY)",
                GH_ParamAccess.item, 2);
            pManager[3].Optional = true;

            // 4 - IncludeLabel
            pManager.AddBooleanParameter("IncludeLabel", "includeLabel",
                "Include HH_MarkLabel in output. Set false to omit label printing. Default true.",
                GH_ParamAccess.item, true);
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "Nesting system macro strings (Park_V7 + BN_NestKontur + BN_TrennerInnenAussen + HH_MarkLabel). " +
                "Wire into HopExport OperationLines after all cutting operations.",
                GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double labelPosX = 0.0;
            double labelPosY = 0.0;
            double labelAngle = 0.0;
            int parkMode = 2;
            bool includeLabel = true;

            DA.GetData(0, ref labelPosX);
            DA.GetData(1, ref labelPosY);
            DA.GetData(2, ref labelAngle);
            DA.GetData(3, ref parkMode);
            DA.GetData(4, ref includeLabel);

            var lines = new List<string>();

            // 1. Park_V7 — standard park position
            lines.Add("CALL Park_V7(VAL MODE:=" + parkMode + ",POSX:=0,POSY:=0)");

            // 2. BN_TrennerInnenAussen — nesting separator inner/outer (no params)
            lines.Add("CALL BN_TrennerInnenAussen");

            // 3. BN_NestKontur — nesting contour definition (no params)
            lines.Add("CALL BN_NestKontur");

            // 4. HH_MarkLabel — label printer
            if (includeLabel)
            {
                string posX = labelPosX != 0.0 ? NcFmt.F(labelPosX) : "_hhdata_LabelPosX";
                string posY = labelPosY != 0.0 ? NcFmt.F(labelPosY) : "_hhdata_LabelPosY";
                lines.Add("CALL HH_MarkLabel(VAL "
                    + "POSX:=" + posX + ","
                    + "POSY:=" + posY + ","
                    + "ANGLE:=" + NcFmt.F(labelAngle) + ","
                    + "LASER:=0)");
            }

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Nesting block: Park_V7 + BN_TrennerInnenAussen + BN_NestKontur"
                + (includeLabel ? " + HH_MarkLabel" : ""));

            DA.SetDataList(0, lines);
        }

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Skip(),
                WallabyHop.AutoWire.Spec.Skip(),
                WallabyHop.AutoWire.Spec.Float("-360<0<360"),
                WallabyHop.AutoWire.Spec.Skip(),
                WallabyHop.AutoWire.Spec.Skip(),
            });
        }
    }
}
