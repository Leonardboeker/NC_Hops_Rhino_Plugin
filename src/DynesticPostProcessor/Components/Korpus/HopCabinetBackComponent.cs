using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace WallabyHop.Components.Korpus
{
    public class HopCabinetBackComponent : GH_Component
    {
        public HopCabinetBackComponent()
            : base("HopCabinetBack", "HopCabinetBack",
                "Cabinet back panel options. Wire into HopKorpus 'back' input.",
                "Wallaby Hop", "Cabinet")
        {
        }

        // -----------------------------------------------------------------
        // INPUTS
        // -----------------------------------------------------------------
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Index 0 — value list (see AddedToDocument)
            pManager.AddIntegerParameter("Type", "type",
                "Back panel mounting type. 1=Eingef\u00e4lzt (Falznut rabbet in sides), 2=Eingenutert (Nut groove in sides).",
                GH_ParamAccess.item, 1);
            pManager[0].Optional = true;

            // Index 1
            pManager.AddNumberParameter("Thickness", "t",
                "Back panel thickness in mm. Also determines Falz/Nut width (thickness + 0.5mm play). Default 8.",
                GH_ParamAccess.item, 8.0);
            pManager[1].Optional = true;

            // Index 2
            pManager.AddNumberParameter("Tiefe", "depth",
                "Rabbet/groove depth into the panel face, in mm. Default 10.",
                GH_ParamAccess.item, 10.0);
            pManager[2].Optional = true;

            // Index 3
            pManager.AddNumberParameter("Reststeg", "reststeg",
                "[Eingenutert only] Distance from back edge to groove rear, in mm. Minimum 6mm recommended. Default 19.",
                GH_ParamAccess.item, 19.0);
            pManager[3].Optional = true;
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
            int type = 1;
            double thickness = 8.0, cutDepth = 10.0, reststegDist = 19.0;
            DA.GetData(0, ref type);
            DA.GetData(1, ref thickness);
            DA.GetData(2, ref cutDepth);
            DA.GetData(3, ref reststegDist);

            if (type < 1 || type > 2) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Type must be 1 (Eingef\u00e4lzt) or 2 (Eingenutert)"); return; }
            if (thickness <= 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Thickness must be > 0"); return; }
            if (cutDepth <= 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Depth must be > 0"); return; }

            // Falz/Nut width = back panel thickness + 0.5mm play
            double falzWidth = thickness + 0.5;

            var dict = new Dictionary<string, object>();
            dict["type"] = type;
            string[] typeNames = { "", "Eingef\u00e4lzt", "Eingenutert" };
            dict["typeName"] = typeNames[type];
            dict["thickness"] = thickness;
            dict["setback"] = 0.0;             // unused, kept for dict key compatibility
            dict["cutDepth"] = cutDepth;
            dict["falzWidth"] = falzWidth;
            dict["nutWidth"]  = falzWidth;
            dict["reststegDist"] = reststegDist;

            DA.SetData(0, new GH_ObjectWrapper(dict));
            DA.SetData(1, type == 2 ? reststegDist : 0.0);

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
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.ValueList(
                    ("Eingef\u00e4lzt (Falznut)", "1"),
                    ("Eingenutert (Nut)", "2")),
                WallabyHop.AutoWire.Spec.Float("3<8<25"),    // Thickness
                WallabyHop.AutoWire.Spec.Float("6<10<15"),   // Tiefe (cut depth)
                WallabyHop.AutoWire.Spec.Float("6<19<50"),   // Reststeg dist
            });
        }
    }
}
