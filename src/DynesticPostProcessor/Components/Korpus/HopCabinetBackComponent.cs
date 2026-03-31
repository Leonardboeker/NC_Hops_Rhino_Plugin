using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace DynesticPostProcessor.Components.Korpus
{
    public class HopCabinetBackComponent : GH_Component
    {
        public HopCabinetBackComponent()
            : base("HopCabinetBack", "HopCabinetBack",
                "Cabinet back panel options. Wire into HopKorpus 'back' input.",
                "DYNESTIC", "Cabinet")
        {
        }

        // -----------------------------------------------------------------
        // INPUTS
        // -----------------------------------------------------------------
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Type", "type",
                "Back panel mounting type. 0=SurfaceMounted (flush with back edge, no groove), 1=Rabbeted (falznut cut in outer panels), 2=Grooved (nut in outer panels at offset from back). Default 0.",
                GH_ParamAccess.item, 0);
            pManager[0].Optional = true;

            pManager.AddNumberParameter("Thickness", "t",
                "Back panel thickness in mm. Default 8.",
                GH_ParamAccess.item, 8.0);
            pManager[1].Optional = true;

            pManager.AddNumberParameter("Offset", "offset",
                "Distance from back edge to groove (Type=2 only), in mm. Default 19.",
                GH_ParamAccess.item, 19.0);
            pManager[2].Optional = true;
        }

        // -----------------------------------------------------------------
        // OUTPUTS
        // -----------------------------------------------------------------
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("BackData", "back",
                "Back panel configuration. Wire into HopKorpus 'back' input.",
                GH_ParamAccess.item);
        }

        // -----------------------------------------------------------------
        // SOLVE
        // -----------------------------------------------------------------
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int type = 0;
            double thickness = 8.0;
            double offset = 19.0;

            DA.GetData(0, ref type);
            DA.GetData(1, ref thickness);
            DA.GetData(2, ref offset);

            // Guards
            if (type < 0 || type > 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Type must be 0, 1, or 2");
                return;
            }

            if (thickness <= 0 || thickness > 25)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Thickness must be between 1 and 25 mm");
                return;
            }

            if (offset < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Offset must be >= 0");
                return;
            }

            // Build configuration dictionary
            string[] typeNames = { "SurfaceMounted", "Rabbeted", "Grooved" };

            var dict = new Dictionary<string, object>();
            dict["type"] = type;
            dict["thickness"] = thickness;
            dict["offset"] = offset;
            dict["typeName"] = typeNames[type];
            dict["rabbet"] = 10.0; // standard rabbet depth/width for Type 1

            // Output
            DA.SetData(0, new GH_ObjectWrapper(dict));

            // Remark
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopCabinetBack: " + typeNames[type] + " t=" + thickness
                + (type == 2 ? " offset=" + offset : ""));
        }

        // -----------------------------------------------------------------
        // ICON + GUID
        // -----------------------------------------------------------------
        protected override Bitmap Icon => IconHelper.Load("HopCabinetBack");

        public override Guid ComponentGuid =>
            new Guid("b2c3d4e5-f6a7-8901-bcde-f01234567890");

        // -----------------------------------------------------------------
        // AUTO-WIRE
        // -----------------------------------------------------------------
        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.Int("0<0<2"),       // Type
                DynesticPostProcessor.AutoWire.Spec.Float("3<8<25"),    // Thickness
                DynesticPostProcessor.AutoWire.Spec.Float("0<19<50"),   // Offset
            });
        }
    }
}
