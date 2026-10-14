using System;
using System.Collections.Generic;
using System.Drawing;

using Rhino.Geometry;

using Grasshopper.Kernel;

namespace WallabyHop.Components.Nesting
{
    /// <summary>
    /// Generates the complete nesting system block for the DYNESTIC CNC:
    ///   Park_V7 + HH_MarkLabel + BN_TrennerInnenAussen + BN_NestKontur
    /// These four macros open every nesting .hop file (127 of 214 reference files),
    /// directly after Fertigteil and before the first tool call; the exporters move
    /// them there wherever the block is wired (NcExport.StartSection).
    /// BN_NestKontur / BN_TrennerInnenAussen define nesting contour and inner/outer separation.
    /// HH_MarkLabel triggers the label printer at the configured position.
    /// </summary>
    public class HopNestingComponent : GH_Component
    {
        public HopNestingComponent() : base(
            "HopNesting", "HopNesting",
            "Generates the nesting system block for the DYNESTIC CNC.\n\n" +
            "Outputs Park_V7 + HH_MarkLabel + BN_TrennerInnenAussen + BN_NestKontur, spelled as in the machine files.\n" +
            "Wire output into the OperationLines of HopExport, HopJob or HopPart, anywhere in the list.\n" +
            "The exporter moves the block up behind Fertigteil; its Park_V7 replaces the default park call.",
            "Wallaby Hop", "6 | Nest") { }

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
                "MODE of Park_V7 (POSX/POSY are always 0).\n2 = machine standard (default; 132 of 214 machine files, all nesting files)\n" +
                "3 and 5 also occur; their meaning is not confirmed yet.",
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
                "Nesting system macro strings (Park_V7 + HH_MarkLabel + BN_TrennerInnenAussen + BN_NestKontur). " +
                "Wire into the exporter's OperationLines; it places them behind Fertigteil.",
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

            // MODE 2/3/5 occur with POSX:=0 in the machine files; 10 only with POSX:=2200.
            if (parkMode != 2 && parkMode != 3 && parkMode != 5)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "ParkMode " + parkMode + " does not occur with POSX:=0 in the machine files "
                    + "(2 = standard; 3 and 5 are rare). Check the park call before running it.");

            var lines = NcExport.NestingBlock(parkMode, labelPosX, labelPosY, labelAngle, includeLabel);

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Nesting block: Park_V7" + (includeLabel ? " + HH_MarkLabel" : "")
                + " + BN_TrennerInnenAussen + BN_NestKontur");

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
