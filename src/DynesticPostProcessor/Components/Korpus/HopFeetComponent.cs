using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace DynesticPostProcessor.Components.Korpus
{
    public class HopFeetComponent : GH_Component
    {
        public HopFeetComponent()
            : base("HopFeet", "HopFeet",
                "Cabinet levelling feet. Generates Rampa insert drill holes in the bottom panel. Wire into HopKorpus 'feet' input.",
                "DYNESTIC", "Cabinet")
        {
        }

        // -----------------------------------------------------------------
        // INPUTS
        // -----------------------------------------------------------------
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Type", "type",
                "Foot type. 0=M8 Rampa (13.5mm drill, 17mm deep), 1=M10 Rampa (15.5mm drill, 20mm deep). Default 0.",
                GH_ParamAccess.item, 0);
            pManager[0].Optional = true;

            pManager.AddNumberParameter("SockelHeight", "sockel",
                "Plinth/sockel height in mm (informational, affects nesting label). Default 100.",
                GH_ParamAccess.item, 100.0);
            pManager[1].Optional = true;

            pManager.AddNumberParameter("EdgeOffset", "edge",
                "Distance from cabinet edge to foot centre, in mm. Default 50.",
                GH_ParamAccess.item, 50.0);
            pManager[2].Optional = true;
        }

        // -----------------------------------------------------------------
        // OUTPUTS
        // -----------------------------------------------------------------
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("FeetData", "feet",
                "Feet configuration. Wire into HopKorpus 'feet' input.",
                GH_ParamAccess.item);
        }

        // -----------------------------------------------------------------
        // SOLVE
        // -----------------------------------------------------------------
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int type = 0;
            double sockel = 100.0;
            double edgeOffset = 50.0;

            DA.GetData(0, ref type);
            DA.GetData(1, ref sockel);
            DA.GetData(2, ref edgeOffset);

            // Guards
            if (type != 0 && type != 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Type must be 0 (M8 Rampa) or 1 (M10 Rampa)");
                return;
            }

            if (edgeOffset <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "EdgeOffset must be > 0");
                return;
            }

            // Build dictionary
            var dict = new Dictionary<string, object>();
            dict["type"] = type;
            dict["typeName"] = type == 0 ? "M8_Rampa" : "M10_Rampa";
            dict["drillDiameter"] = type == 0 ? 13.5 : 15.5;
            dict["drillDepth"] = type == 0 ? 17.0 : 20.0;
            dict["sockelHeight"] = sockel;
            dict["edgeOffset"] = edgeOffset;
            // Foot count determined by HopKorpus based on Width: W <= 800 -> 4 feet, W > 800 -> 6 feet
            // Positions (relative to bottom panel, set by HopKorpus):
            // 4-foot: (edgeOffset, edgeOffset), (W-edgeOffset, edgeOffset), (edgeOffset, D-edgeOffset), (W-edgeOffset, D-edgeOffset)
            // 6-foot: adds (W/2, edgeOffset), (W/2, D-edgeOffset)

            // Output
            DA.SetData(0, new GH_ObjectWrapper(dict));

            // Remark
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopFeet: " + (type == 0 ? "M8" : "M10") + " Rampa, sockel=" + sockel + "mm, offset=" + edgeOffset + "mm");
        }

        // -----------------------------------------------------------------
        // ICON + GUID
        // -----------------------------------------------------------------
        protected override Bitmap Icon => IconHelper.Load("HopFeet");

        public override Guid ComponentGuid =>
            new Guid("e5f6a7b8-c9d0-1234-ef01-234567890123");

        // -----------------------------------------------------------------
        // AUTO-WIRE
        // -----------------------------------------------------------------
        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            AutoWire.Apply(this, doc, new[]
            {
                AutoWire.Spec.Int("0<0<1"),          // Type
                AutoWire.Spec.Float("50<100<300"),   // SockelHeight
                AutoWire.Spec.Float("30<50<100"),    // EdgeOffset
            });
        }
    }
}
