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
            // Index 0 — value list (see AddedToDocument)
            pManager.AddIntegerParameter("Type", "type",
                "Back panel mounting type. 0=Eingelegt (sits inside, no groove), 1=Eingef\u00e4lzt (Falznut rabbet in sides), 2=Eingenutert (Nut groove in sides).",
                GH_ParamAccess.item, 0);
            pManager[0].Optional = true;

            // Index 1
            pManager.AddNumberParameter("Thickness", "t",
                "Back panel thickness in mm. For Eingef\u00e4lzt this also sets the Falz width. Default 8.",
                GH_ParamAccess.item, 8.0);
            pManager[1].Optional = true;

            // Index 2
            pManager.AddNumberParameter("Ruecksprung", "setback",
                "[Eingelegt only] Distance from back edge to back panel face, in mm. 0 = flush with back. Default 0.",
                GH_ParamAccess.item, 0.0);
            pManager[2].Optional = true;

            // Index 3
            pManager.AddNumberParameter("Tiefe", "depth",
                "[Eingef\u00e4lzt/Eingenutert] Rabbet/groove depth into the panel face, in mm. Reststeg = panel thickness - this value. Default 10.",
                GH_ParamAccess.item, 10.0);
            pManager[3].Optional = true;

            // Index 4
            pManager.AddNumberParameter("Reststeg", "reststeg",
                "[Eingenutert only] Distance from back edge to groove rear, in mm. Minimum 6mm recommended. Default 19.",
                GH_ParamAccess.item, 19.0);
            pManager[4].Optional = true;
        }

        // -----------------------------------------------------------------
        // OUTPUTS
        // -----------------------------------------------------------------
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("BackData", "back",
                "Back panel configuration. Wire into HopKorpus 'back' input.",
                GH_ParamAccess.item);

            pManager.AddNumberParameter("Reststeg", "reststeg",
                "Remaining material after Falz/Nut cut (= panel thickness - cut depth). Relevant for Eingef\u00e4lzt and Eingenutert types.",
                GH_ParamAccess.item);
        }

        // -----------------------------------------------------------------
        // SOLVE
        // -----------------------------------------------------------------
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int type = 0;
            double thickness = 8.0, setback = 0.0, cutDepth = 10.0, reststegDist = 19.0;
            DA.GetData(0, ref type);
            DA.GetData(1, ref thickness);
            DA.GetData(2, ref setback);
            DA.GetData(3, ref cutDepth);
            DA.GetData(4, ref reststegDist);

            if (type < 0 || type > 2) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Type must be 0, 1, or 2"); return; }
            if (thickness <= 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Thickness must be > 0"); return; }
            if (cutDepth <= 0 && type > 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Depth must be > 0 for this type"); return; }

            // Compute Reststeg (only relevant for type 1 and 2)
            double reststeg = 0;
            if (type == 1) reststeg = 0; // will be computed by HopKorpus from panel MS - cutDepth
            if (type == 2) reststeg = reststegDist;

            // For type 1 (Eingefaelzt): Falzbreite = thickness + 0.5mm play
            double falzWidth = thickness + 0.5;

            var dict = new Dictionary<string, object>();
            dict["type"] = type;
            string[] typeNames = { "Eingelegt", "Eingef\u00e4lzt", "Eingenutert" };
            dict["typeName"] = typeNames[type];
            dict["thickness"] = thickness;
            dict["setback"] = setback;         // for Eingelegt: position from back edge
            dict["cutDepth"] = cutDepth;       // for Eingefaelzt/Eingenutert: how deep the Falz/Nut cuts in
            dict["falzWidth"] = falzWidth;     // for Eingefaelzt: Falzbreite = thickness + 0.5
            dict["nutWidth"]  = falzWidth;     // for Eingenutert: Nutbreite = thickness + 0.5
            dict["reststegDist"] = reststegDist; // for Eingenutert: distance from back edge to groove rear

            DA.SetData(0, new GH_ObjectWrapper(dict));
            // Output Reststeg value (computed by HopKorpus later; output from this component is the setback param for Eingenutert)
            DA.SetData(1, reststeg);

            string remark = typeNames[type] + ", t=" + thickness + "mm";
            if (type == 1) remark += ", Falztiefe=" + cutDepth + "mm, Falzbreite=" + falzWidth.ToString("F1") + "mm";
            if (type == 2) remark += ", Nuttiefe=" + cutDepth + "mm, Reststeg=" + reststegDist + "mm";
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, remark);
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
                DynesticPostProcessor.AutoWire.Spec.ValueList(
                    ("Eingelegt", "0"),
                    ("Eingef\u00e4lzt (Falznut)", "1"),
                    ("Eingenutert (Nut)", "2")),
                DynesticPostProcessor.AutoWire.Spec.Float("3<8<25"),    // Thickness
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<50"),    // Ruecksprung
                DynesticPostProcessor.AutoWire.Spec.Float("6<10<15"),   // Tiefe (cut depth)
                DynesticPostProcessor.AutoWire.Spec.Float("6<19<50"),   // Reststeg dist
            });
        }
    }
}
