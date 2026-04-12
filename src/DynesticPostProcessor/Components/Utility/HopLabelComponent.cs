using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Utility
{
    /// <summary>
    /// Produces VP variable lines for the EasyTronic Label printer.
    /// The label template reads VP18-VP21 (+extra) from the HOPS VARS block.
    /// Wire output into HopExport's LabelVars input.
    ///
    /// Default VP mapping (matches standard HOLZ-HER EasyTronic label template):
    ///   VP18 = Auftragsnummer  → "Auftrag:"  field
    ///   VP19 = Bestellnummer   → "EINr.:"   field
    ///   VP20 = Bauteilnummer   → "Pos.:"    field
    ///   VP21 = Bauteilname     → "Material:" field
    /// </summary>
    public class HopLabelComponent : GH_Component
    {
        public HopLabelComponent() : base(
            "HopLabel", "HopLabel",
            "Creates label variable lines for the EasyTronic Label printer. " +
            "Connect output to HopExport LabelVars so the printed label carries job metadata.",
            "DYNESTIC", "Utility") { }

        public override Guid ComponentGuid =>
            new Guid("a7c3e912-5d8f-4b2e-9061-7f42d8b5c130");

        protected override Bitmap Icon => IconHelper.Load("HopLabel");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        // ---------------------------------------------------------------
        // PARAMS
        // ---------------------------------------------------------------

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Auftrag", "auftrag",
                "Order / job number printed on the label (VP18 = Auftragsnummer).",
                GH_ParamAccess.item, "");
            pManager[0].Optional = true;

            pManager.AddTextParameter("EINr", "einr",
                "Setup or purchase reference number (VP19 = Bestellnummer / EINr).",
                GH_ParamAccess.item, "");
            pManager[1].Optional = true;

            pManager.AddTextParameter("Pos", "pos",
                "Part number / position identifier shown in Pos. field (VP20 = Bauteilnummer).",
                GH_ParamAccess.item, "");
            pManager[2].Optional = true;

            pManager.AddTextParameter("Material", "material",
                "Material description shown in Material field (VP21 = Bauteilname / Material).",
                GH_ParamAccess.item, "");
            pManager[3].Optional = true;

            pManager.AddTextParameter("ExtraVars", "extraVars",
                "Additional raw VP variable lines. Each line must follow HOPS VARS syntax:\n" +
                "  VP30 := 'value';*VAR*Label\n" +
                "Use this for any VP number not covered by the named inputs above.",
                GH_ParamAccess.list);
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("LabelVars", "labelVars",
                "VARS block lines for the EasyTronic label printer. " +
                "Wire into HopExport's LabelVars input.",
                GH_ParamAccess.list);
        }

        // ---------------------------------------------------------------
        // SOLVE
        // ---------------------------------------------------------------

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string auftrag  = "";
            string einr     = "";
            string pos      = "";
            string material = "";
            var extraVars = new List<string>();

            DA.GetData(0, ref auftrag);
            DA.GetData(1, ref einr);
            DA.GetData(2, ref pos);
            DA.GetData(3, ref material);
            DA.GetDataList(4, extraVars);

            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(auftrag))
                lines.Add("   VP18 := '" + Sanitize(auftrag) + "';*VAR*Auftragsnummer");

            if (!string.IsNullOrWhiteSpace(einr))
                lines.Add("   VP19 := '" + Sanitize(einr) + "';*VAR*Bestellnummer");

            if (!string.IsNullOrWhiteSpace(pos))
                lines.Add("   VP20 := '" + Sanitize(pos) + "';*VAR*Bauteilnummer");

            if (!string.IsNullOrWhiteSpace(material))
                lines.Add("   VP21 := '" + Sanitize(material) + "';*VAR*Bauteilname");

            foreach (string extra in extraVars)
            {
                if (string.IsNullOrWhiteSpace(extra)) continue;
                // Ensure consistent 3-space indent like the rest of the VARS block
                lines.Add(extra.StartsWith("   ") ? extra : "   " + extra.TrimStart());
            }

            if (lines.Count == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "All label fields are empty — no VP variables will be written.");

            DA.SetDataList(0, lines);
        }

        // Single quotes inside values would break the HOPS parser — strip them
        private static string Sanitize(string s) => s.Replace("'", "").Trim();
    }
}
