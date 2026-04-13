using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace WallabyHop.Components.Korpus
{
    public class HopShelvesComponent : GH_Component
    {
        public HopShelvesComponent()
            : base("HopShelves", "HopShelves",
                "Adjustable shelves with System-32 hole rows in side panels. Shelves are distributed evenly in the cabinet interior. Wire into HopKorpus 'shelves' input.",
                "Wallaby Hop", "Cabinet")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Count", "n",
                "Number of shelves to add. Distributed evenly in the inner cabinet height. Default 3.",
                GH_ParamAccess.item, 3);
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("ShelvesData", "shelves",
                "Shelf configuration. Wire into HopKorpus 'shelves' input.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int count = 3;
            DA.GetData(0, ref count);

            if (count < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Count must be >= 0");
                return;
            }

            var dict = new Dictionary<string, object>();
            dict["count"] = count;
            // System-32 constants
            dict["s32_raster"]  = 32.0;
            dict["s32_edge"]    = 37.0;
            dict["s32_drill_d"] = 5.0;
            dict["s32_depth"]   = 13.0;

            DA.SetData(0, new GH_ObjectWrapper(dict));
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopShelves: " + count + " shelves, evenly distributed");
        }

        protected override Bitmap Icon => IconHelper.Load("HopShelves");

        public override Guid ComponentGuid =>
            new Guid("d4e5f6a7-b8c9-0123-def0-123456789012");

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            AutoWire.Apply(this, doc, new[]
            {
                AutoWire.Spec.Int("0<3<10"),  // Count
            });
        }
    }
}
