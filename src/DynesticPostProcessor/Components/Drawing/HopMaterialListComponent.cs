using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace WallabyHop.Components.Drawing
{
    public class HopMaterialListComponent : GH_Component
    {
        public HopMaterialListComponent()
            : base("HopMaterialList", "HopMaterialList",
                "Calculates total material area (m²) per board thickness from HopKorpus panel dictionaries. " +
                "Outputs a formatted cut list suitable for ordering material.",
                "Wallaby Hop", "Drawing")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Parts", "parts",
                "Panel dictionaries from HopKorpus 'Panels' output or HopPart 'Part' output.",
                GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("MaterialList", "matList",
                "Formatted material cut list grouped by board thickness, with area in m² per group.",
                GH_ParamAccess.item);

            pManager.AddNumberParameter("TotalAreaM2", "totalM2",
                "Total material area across all panels in m².",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var parts = new List<object>();
            DA.GetDataList(0, parts);

            // Group: thickness → list of (name, dx, dy, area)
            var groups = new SortedDictionary<double, List<(string name, double dx, double dy, double areaM2)>>();

            foreach (var p in parts)
            {
                var wrapper = p as GH_ObjectWrapper;
                if (wrapper == null) continue;
                var dict = wrapper.Value as Dictionary<string, object>;
                if (dict == null) continue;

                var outline = dict.ContainsKey("outline") ? dict["outline"] as Curve : null;
                if (outline == null) continue;

                var name = dict.ContainsKey("panelName") ? dict["panelName"] as string ?? "Part" : "Part";
                var thickness = dict.ContainsKey("thickness") ? Convert.ToDouble(dict["thickness"]) : 19.0;

                BoundingBox bb = outline.GetBoundingBox(true);
                double dx = bb.Max.X - bb.Min.X;
                double dy = bb.Max.Y - bb.Min.Y;
                double areaM2 = (dx / 1000.0) * (dy / 1000.0);

                if (!groups.ContainsKey(thickness))
                    groups[thickness] = new List<(string, double, double, double)>();

                groups[thickness].Add((name, dx, dy, areaM2));
            }

            // Build output text
            var sb = new StringBuilder();
            sb.AppendLine("MATERIAL LIST  /  CUT LIST");
            sb.AppendLine("─────────────────────────────────────────");

            double totalArea = 0;

            foreach (var kv in groups)
            {
                double t = kv.Key;
                var items = kv.Value;
                double groupArea = 0;

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "Thickness {0:F0}mm:", t));

                foreach (var item in items)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "  {0,-14}  {1:F0} × {2:F0}mm  =  {3:F4} m²",
                        item.name, item.dx, item.dy, item.areaM2));
                    groupArea += item.areaM2;
                }

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  → Subtotal: {0} parts  /  {1:F3} m²",
                    items.Count, groupArea));
                sb.AppendLine();

                totalArea += groupArea;
            }

            sb.AppendLine("─────────────────────────────────────────");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "TOTAL: {0} parts  /  {1:F3} m²",
                parts.Count, totalArea));

            string matList = sb.ToString().TrimEnd();

            DA.SetData(0, matList);
            DA.SetData(1, Math.Round(totalArea, 4));

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                string.Format(CultureInfo.InvariantCulture,
                    "MaterialList: {0} parts, {1} thickness groups, {2:F3} m² total",
                    parts.Count, groups.Count, totalArea));
        }

        protected override System.Drawing.Bitmap Icon => IconHelper.Load("HopMaterialList");

        public override Guid ComponentGuid => new Guid("c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f");

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Skip(), // Parts
            });
        }
    }
}
