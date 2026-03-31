using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace DynesticPostProcessor.Components.Korpus
{
    public class HopShelvesComponent : GH_Component
    {
        public HopShelvesComponent()
            : base("HopShelves", "HopShelves",
                "Cabinet shelf options. Adjustable shelves use System-32 hole rows in side panels. Fixed shelves add a structural panel. Wire into HopKorpus 'shelves' input.",
                "DYNESTIC", "Cabinet")
        {
        }

        // -----------------------------------------------------------------
        // INPUTS
        // -----------------------------------------------------------------
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("FixedShelf", "fixed",
                "Add a fixed structural shelf (Konstruktionsboden). Default False.",
                GH_ParamAccess.item, false);
            pManager[0].Optional = true;

            pManager.AddNumberParameter("FixedHeight", "fixedH",
                "Height of fixed shelf from bottom (inside measurement), in mm. Only used if FixedShelf=True. Default 400.",
                GH_ParamAccess.item, 400.0);
            pManager[1].Optional = true;

            pManager.AddIntegerParameter("AdjustableCount", "adj",
                "Number of adjustable shelf positions (System-32 hole rows in side panels). 0 = none. Default 3.",
                GH_ParamAccess.item, 3);
            pManager[2].Optional = true;
        }

        // -----------------------------------------------------------------
        // OUTPUTS
        // -----------------------------------------------------------------
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("ShelvesData", "shelves",
                "Shelf configuration. Wire into HopKorpus 'shelves' input.",
                GH_ParamAccess.item);
        }

        // -----------------------------------------------------------------
        // SOLVE
        // -----------------------------------------------------------------
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool fixedShelf = false;
            double fixedH = 400.0;
            int adjCount = 3;

            DA.GetData(0, ref fixedShelf);
            DA.GetData(1, ref fixedH);
            DA.GetData(2, ref adjCount);

            // Guards
            if (adjCount < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "AdjustableCount must be >= 0");
                return;
            }

            if (fixedShelf && fixedH <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "FixedHeight must be > 0 when FixedShelf is True");
                return;
            }

            // Build dictionary
            var dict = new Dictionary<string, object>();
            dict["fixedShelf"] = fixedShelf;
            dict["fixedHeight"] = fixedH;
            dict["adjustableCount"] = adjCount;

            // System-32 constants (used by HopKorpus to generate hole rows in side panels)
            dict["s32_raster"] = 32.0;    // hole spacing
            dict["s32_edge"] = 37.0;      // distance from front/back edge to first hole center
            dict["s32_drill_d"] = 5.0;    // hole diameter (Bodentraeger)
            dict["s32_depth"] = 13.0;     // drill depth

            // Output
            DA.SetData(0, new GH_ObjectWrapper(dict));

            // Remark
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopShelves: " + (fixedShelf ? "1 fixed @ " + fixedH + "mm, " : "") + adjCount + " adjustable positions");
        }

        // -----------------------------------------------------------------
        // ICON + GUID
        // -----------------------------------------------------------------
        protected override Bitmap Icon => IconHelper.Load("HopShelves");

        public override Guid ComponentGuid =>
            new Guid("d4e5f6a7-b8c9-0123-def0-123456789012");

        // -----------------------------------------------------------------
        // AUTO-WIRE
        // -----------------------------------------------------------------
        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            AutoWire.Apply(this, doc, new[]
            {
                AutoWire.Spec.Toggle(),              // FixedShelf bool
                AutoWire.Spec.Float("50<400<2000"),  // FixedHeight
                AutoWire.Spec.Int("0<3<10"),         // AdjustableCount
            });
        }
    }
}
