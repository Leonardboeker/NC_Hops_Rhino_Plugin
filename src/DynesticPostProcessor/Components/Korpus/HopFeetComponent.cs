using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace WallabyHop.Components.Korpus
{
    public class HopFeetComponent : GH_Component
    {
        public HopFeetComponent()
            : base("HopFeet", "HopFeet",
                "Cabinet levelling feet via Befestigungsplatte (92x79mm, 64x64mm hole grid). Generates 4 drill holes per foot corner. Wire into HopKorpus 'feet' input.",
                "Wallaby Hop", "Cabinet")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Index 0 — ValueList: mounting variant
            pManager.AddIntegerParameter("Variant", "var",
                "Mounting variant. 0=Screw (Ø4mm, 10mm deep), 1=Press-fit (Ø10mm, 12mm deep). Default 1.",
                GH_ParamAccess.item, 1);
            pManager[0].Optional = true;

            // Index 1
            pManager.AddNumberParameter("FootOffset", "offset",
                "Distance from cabinet outer edge to foot plate centre in mm. Default 50.",
                GH_ParamAccess.item, 50.0);
            pManager[1].Optional = true;

            // Index 2
            pManager.AddNumberParameter("SockelHeight", "sockel",
                "Plinth height in mm (informational label only). Default 100.",
                GH_ParamAccess.item, 100.0);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("FeetData", "feet",
                "Feet configuration. Wire into HopKorpus 'feet' input.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int variant = 1;
            double offset = 50.0;
            double sockel = 100.0;
            DA.GetData(0, ref variant);
            DA.GetData(1, ref offset);
            DA.GetData(2, ref sockel);

            if (variant < 0 || variant > 1)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Variant must be 0 or 1"); return; }
            if (offset <= 32)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "FootOffset must be > 32mm (half the 64mm grid)"); return; }

            // Befestigungsplatte: 4 holes at ±32mm from foot centre
            double drillDia   = variant == 0 ? 4.0  : 10.0;
            double drillDepth = variant == 0 ? 10.0 : 12.0;

            var dict = new Dictionary<string, object>();
            dict["variant"]      = variant;
            dict["variantName"]  = variant == 0 ? "Screw" : "Press-fit";
            dict["drillDiameter"] = drillDia;
            dict["drillDepth"]   = drillDepth;
            dict["footOffset"]   = offset;
            dict["footGrid"]     = 64.0;   // Befestigungsplatte hole grid = 64x64mm
            dict["sockelHeight"] = sockel;

            DA.SetData(0, new GH_ObjectWrapper(dict));
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopFeet: " + (variant == 0 ? "Screw" : "Press-fit")
                + " Ø" + drillDia + "mm/" + drillDepth + "mm, offset=" + offset + "mm, sockel=" + sockel + "mm");
        }

        protected override Bitmap Icon => IconHelper.Load("HopFeet");

        public override Guid ComponentGuid =>
            new Guid("e5f6a7b8-c9d0-1234-ef01-234567890123");

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            AutoWire.Apply(this, doc, new[]
            {
                AutoWire.Spec.ValueList(
                    ("Zum Schrauben (\u00d84mm, 10mm)", "0"),
                    ("Zum Einpressen (\u00d810mm, 12mm)", "1")),  // Variant
                AutoWire.Spec.Float("33<50<100"),  // FootOffset
                AutoWire.Spec.Float("50<100<300"), // SockelHeight
            });
        }
    }
}
