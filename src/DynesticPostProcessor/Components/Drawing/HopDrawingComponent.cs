using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace WallabyHop.Components.Drawing
{
    /// <summary>
    /// Generates a Rhino layout page (three-view orthographic + iso + title block + material list)
    /// from assembled Breps. Works with HopKorpus AssembledBreps or any solid geometry.
    /// </summary>
    public class HopDrawingComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private List<Brep> _preview = null;

        public HopDrawingComponent()
            : base("HopDrawing", "HopDrawing",
                "Generates a Rhino layout page with three-view orthographic (Top/Front/Side/Iso), " +
                "title block from a .3dm template, outer dimensions, and material list. " +
                "Wire HopKorpus 'AssembledBreps' → 'geo', and optionally 'Panels' → 'parts'.",
                "Wallaby Hop", "Drawing")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0
            pManager.AddBrepParameter("Geometry", "geo",
                "Assembled 3D Breps. Wire from HopKorpus 'AssembledBreps' or any solid geometry.",
                GH_ParamAccess.list);

            // 1
            pManager.AddGenericParameter("Parts", "parts",
                "Panel dicts from HopKorpus 'Panels'. Used for the material list only. Optional.",
                GH_ParamAccess.list);
            pManager[1].Optional = true;

            // 2
            pManager.AddTextParameter("TemplatePath", "template",
                "Path to .3dm file with title block. Objects are placed in layout page space.",
                GH_ParamAccess.item,
                @"E:\Rhino Resourcen\Plan Köpfe\Leonard Elias Böker.3dm");
            pManager[2].Optional = true;

            // 3
            pManager.AddTextParameter("ProjectName", "project",
                "Project name shown in the title block.",
                GH_ParamAccess.item, "Project");
            pManager[3].Optional = true;

            // 4
            pManager.AddTextParameter("DrawBy", "drawBy",
                "Drawn-by name for the title block.",
                GH_ParamAccess.item, "");
            pManager[4].Optional = true;

            // 5
            pManager.AddIntegerParameter("Scale", "scale",
                "Scale denominator: 10 = 1:10, 20 = 1:20. Default 10.",
                GH_ParamAccess.item, 10);
            pManager[5].Optional = true;

            // 6
            pManager.AddTextParameter("LayoutName", "layoutName",
                "Name of the Rhino layout page to create or update.",
                GH_ParamAccess.item, "FloorPlan_01");
            pManager[6].Optional = true;

            // 7
            pManager.AddTextParameter("Folder", "folder",
                "Output folder for PDF export. Leave empty to skip PDF.",
                GH_ParamAccess.item);
            pManager[7].Optional = true;

            // 8
            pManager.AddBooleanParameter("Generate", "generate",
                "Toggle to create/update the Rhino layout and export PDF.",
                GH_ParamAccess.item, false);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("MaterialList", "matList",
                "Formatted material cut list (m² per board thickness).",
                GH_ParamAccess.item);

            pManager.AddTextParameter("StatusMsg", "status",
                "Status message from the last layout generation.",
                GH_ParamAccess.item);
        }

        public override void ClearData()
        {
            base.ClearData();
            _preview = null;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            _preview = null;
            DA.SetData(0, "");
            DA.SetData(1, "");

            // ---------------------------------------------------------------
            // 1. READ INPUTS
            // ---------------------------------------------------------------
            var breps = new List<Brep>();
            DA.GetDataList(0, breps);
            breps = breps.Where(b => b != null && b.IsValid).ToList();

            var parts = new List<object>();
            DA.GetDataList(1, parts);

            string templatePath = @"E:\Rhino Resourcen\Plan Köpfe\Leonard Elias Böker.3dm";
            DA.GetData(2, ref templatePath);

            string projectName = "Projekt";
            DA.GetData(3, ref projectName);

            string drawBy = "";
            DA.GetData(4, ref drawBy);

            int scale = 10;
            DA.GetData(5, ref scale);
            if (scale <= 0) scale = 10;

            string layoutName = "Grundriss_01";
            DA.GetData(6, ref layoutName);
            if (string.IsNullOrWhiteSpace(layoutName)) layoutName = "Grundriss_01";

            string folder = null;
            DA.GetData(7, ref folder);

            bool generate = false;
            DA.GetData(8, ref generate);

            // ---------------------------------------------------------------
            // 2. MATERIAL LIST (always, from Parts if connected)
            // ---------------------------------------------------------------
            string matList = BuildMaterialList(parts);
            DA.SetData(0, matList);

            // ---------------------------------------------------------------
            // 3. PREVIEW
            // ---------------------------------------------------------------
            if (breps.Count > 0)
                _preview = breps;

            if (!generate) return;

            // ---------------------------------------------------------------
            // 4. GUARDS
            // ---------------------------------------------------------------
            if (breps.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "HopDrawing: no geometry -- wire HopKorpus 'AssembledBreps' to 'geo'");
                return;
            }

            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "HopDrawing: no active Rhino document");
                return;
            }

            // ---------------------------------------------------------------
            // 5. GENERATE LAYOUT
            // ---------------------------------------------------------------
            try
            {
                string status = GenerateLayout(doc, breps, matList,
                    templatePath, projectName, drawBy, scale, layoutName, folder);
                DA.SetData(1, status);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, status);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HopDrawing: " + ex.Message);
            }
        }

        // ===============================================================
        // LAYOUT GENERATION
        // ===============================================================
        private static string GenerateLayout(
            RhinoDoc doc,
            List<Brep> breps,
            string matList,
            string templatePath,
            string projectName,
            string drawBy,
            int scale,
            string layoutName,
            string folder)
        {
            // A3 landscape: 420 × 297 mm
            const double pageW = 420.0;
            const double pageH = 297.0;

            // ---------------------------------------------------------------
            // Find or create layout page
            // ---------------------------------------------------------------
            RhinoPageView page = null;
            foreach (RhinoView v in doc.Views)
            {
                var pv = v as RhinoPageView;
                if (pv != null && pv.PageName == layoutName)
                { page = pv; break; }
            }
            if (page == null)
                page = doc.Views.AddPageView(layoutName, pageW, pageH);

            if (page == null)
                throw new Exception("could not create layout page '" + layoutName + "'");

            Guid pvId = page.MainViewport.Id;

            // ---------------------------------------------------------------
            // Clean page: remove all existing page-space objects
            // ---------------------------------------------------------------
            var toDelete = doc.Objects
                .GetObjectList(new ObjectEnumeratorSettings { ActiveObjects = true, LockedObjects = true, HiddenObjects = true })
                .Where(o => o.Attributes.Space == ActiveSpace.PageSpace
                         && o.Attributes.ViewportId == pvId)
                .Select(o => o.Id)
                .ToList();
            foreach (var id in toDelete)
                doc.Objects.Delete(id, true);

            // Remove existing detail views
            var existingDetails = page.GetDetailViews();
            if (existingDetails != null)
                foreach (var d in existingDetails)
                    doc.Objects.Delete(d.Id, true);

            // ---------------------------------------------------------------
            // Bounding box of all geometry
            // ---------------------------------------------------------------
            BoundingBox bb = BoundingBox.Empty;
            foreach (var b in breps) bb.Union(b.GetBoundingBox(true));

            double bbW = bb.Max.X - bb.Min.X;
            double bbH = bb.Max.Z - bb.Min.Z;
            double bbD = bb.Max.Y - bb.Min.Y;

            // ---------------------------------------------------------------
            // 2×2 grid layout (left 295mm for views, right 120mm for title block)
            //
            //   [Draufsicht  ] [Isometrie   ]
            //   [Vorderansicht][Seitenansicht]
            //   [Dim text                   ]
            //   ─────────────── | title block
            // ---------------------------------------------------------------
            const double m     = 5.0;   // margin
            const double sepX  = 295.0; // left/right separator
            const double midY  = 150.0; // top/bottom row separator
            double halfX = (sepX - 3 * m) / 2.0;

            var views = new[]
            {
                ("Draufsicht",     m,           midY + m,  m + halfX,    pageH - m,  DefinedViewportProjection.Top),
                ("Isometrie",      m + halfX + m, midY + m, sepX - m,   pageH - m,  DefinedViewportProjection.Perspective),
                ("Vorderansicht",  m,           m + 8,     m + halfX,    midY - m,  DefinedViewportProjection.Front),
                ("Seitenansicht",  m + halfX + m, m + 8,   sepX - m,    midY - m,  DefinedViewportProjection.Right),
            };

            // ---------------------------------------------------------------
            // Add detail views, zoom to geometry, then set scale
            // ---------------------------------------------------------------
            // Slightly inflated bb so geometry has breathing room in each view
            double inflate = bb.Diagonal.Length * 0.08;
            BoundingBox bbPadded = new BoundingBox(
                bb.Min - new Vector3d(inflate, inflate, inflate),
                bb.Max + new Vector3d(inflate, inflate, inflate));

            foreach (var (name, x0, y0, x1, y1, proj) in views)
            {
                var detail = page.AddDetailView(
                    name,
                    new Point2d(x0, y0),
                    new Point2d(x1, y1),
                    proj);

                if (detail == null) continue;

                // Center the camera on the geometry BEFORE setting scale.
                // Activate the detail so page.ActiveViewport reflects its
                // internal model viewport, then zoom to the padded bbox.
                // Without this the camera looks at the world origin and the
                // model appears as a tiny sliver in one corner of the view.
                if (page.SetActiveDetail(name, false))
                    page.ActiveViewport.ZoomBoundingBox(bbPadded);

                // Set scale: 1 model unit = 1/scale page units
                detail.DetailGeometry.SetScale(1, doc.ModelUnitSystem, scale, doc.PageUnitSystem);

                // Lock scale so user doesn't accidentally change it
                detail.DetailGeometry.IsProjectionLocked = true;
            }

            // ---------------------------------------------------------------
            // Page-space objects: separator lines, labels, dimensions, matlist
            // ---------------------------------------------------------------
            var pa = new ObjectAttributes
            {
                Space      = ActiveSpace.PageSpace,
                ViewportId = pvId,
            };

            // Separator lines
            AddLine(doc, pa, new Point3d(sepX, m,        0), new Point3d(sepX, pageH - m, 0));
            AddLine(doc, pa, new Point3d(m,    midY,     0), new Point3d(sepX - m, midY,  0));

            // View labels
            AddText(doc, pa, "TOP VIEW",         m + 1, pageH - m - 5, 2.0);
            AddText(doc, pa, "ISOMETRIC",        m + halfX + m + 1, pageH - m - 5, 2.0);
            AddText(doc, pa, "FRONT VIEW",       m + 1, midY - m - 5, 2.0);
            AddText(doc, pa, "SIDE VIEW",        m + halfX + m + 1, midY - m - 5, 2.0);

            // Outer dimension annotation (below grid)
            string dimText = string.Format(CultureInfo.InvariantCulture,
                "W={0:F0}  H={1:F0}  D={2:F0}  mm     Scale 1:{3}",
                bbW, bbH, bbD, scale);
            AddText(doc, pa, dimText, m + 1, m + 2, 2.5);

            // ---------------------------------------------------------------
            // Title block: template or fallback
            // ---------------------------------------------------------------
            double tbX = sepX + m;
            bool templateOk = false;
            if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
                templateOk = ImportTemplate(doc, pa, pvId, templatePath, projectName, drawBy, scale);

            if (!templateOk)
                DrawFallbackTitleBlock(doc, pa, tbX, pageW - m, m, pageH - m,
                    projectName, drawBy, scale);

            // Material list in title block area
            double mlY = templateOk ? 135.0 : 125.0;
            AddText(doc, pa, "MATERIAL LIST:", tbX, mlY, 2.5);
            double lineY = mlY - 5.5;
            foreach (var line in matList.Split('\n'))
            {
                if (lineY < m + 2) break;
                string l = line.TrimEnd();
                if (!string.IsNullOrWhiteSpace(l))
                    AddText(doc, pa, l, tbX, lineY, 1.8);
                lineY -= 3.8;
            }

            doc.Views.Redraw();

            // ---------------------------------------------------------------
            // PDF export
            // ---------------------------------------------------------------
            string pdfResult = "";
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                string pdfPath = Path.Combine(folder, layoutName + ".pdf");
                pdfResult = TryExportPdf(page, pdfPath);
            }

            return string.Format("Layout '{0}' created. Template={1}. {2}",
                layoutName, templateOk ? "loaded" : "not found", pdfResult);
        }

        // ---------------------------------------------------------------
        // TEMPLATE IMPORT
        // Reads all model-space objects from the .3dm, calculates their
        // bounding box, then translates + scales them to fill the title-block
        // strip (tbX … pageW-margin, m … pageH-m) on the layout page.
        // ---------------------------------------------------------------
        private static bool ImportTemplate(
            RhinoDoc doc, ObjectAttributes baseAttr, Guid pvId,
            string path, string project, string drawBy, int scale)
        {
            try
            {
                var f = Rhino.FileIO.File3dm.Read(path);
                if (f == null) return false;

                string date = DateTime.Now.ToString("dd.MM.yyyy");
                string scaleStr = "1:" + scale;

                // --- Pass 1: collect geometry & measure overall bounding box ---
                var geos = new List<GeometryBase>();
                BoundingBox srcBb = BoundingBox.Empty;
                foreach (var obj in f.Objects)
                {
                    var geo = obj.Geometry;
                    if (geo == null) continue;
                    geos.Add(geo);
                    srcBb.Union(geo.GetBoundingBox(false));
                }

                if (!srcBb.IsValid || geos.Count == 0) return false;

                // --- Build transform: fit src bbox → title-block strip ---
                // target area: tbX=300, pageW-5=415, bottom=5, top=292  (mm)
                const double tbX   = 300.0;
                const double tbRight = 415.0;
                const double tbBot  = 5.0;
                const double tbTop  = 292.0;

                double srcW = srcBb.Max.X - srcBb.Min.X;
                double srcH = srcBb.Max.Y - srcBb.Min.Y;
                if (srcW < 0.001 || srcH < 0.001) return false;

                double tgtW = tbRight - tbX;
                double tgtH = tbTop   - tbBot;
                double s    = Math.Min(tgtW / srcW, tgtH / srcH); // uniform scale

                // Centre within strip
                double offX = tbX  + (tgtW - srcW * s) / 2.0 - srcBb.Min.X * s;
                double offY = tbBot + (tgtH - srcH * s) / 2.0 - srcBb.Min.Y * s;

                Transform xform = Transform.Scale(Point3d.Origin, s)
                                * Transform.Translation(offX / s, offY / s, 0);
                // Combine: first scale, then translate
                xform = Transform.Translation(new Vector3d(offX, offY, 0))
                      * Transform.Scale(Point3d.Origin, s);

                // --- Pass 2: transform & add ---
                foreach (var geo in geos)
                {
                    var g2 = geo.Duplicate();
                    g2.Transform(xform);

                    var attr = baseAttr.Duplicate();

                    if (g2 is TextEntity te)
                    {
                        string t = te.PlainText ?? "";
                        t = t.Replace("{PROJEKT}", project)
                             .Replace("{PROJEKTNAME}", project)
                             .Replace("{DRAWBY}", drawBy)
                             .Replace("{GEZEICHNET}", drawBy)
                             .Replace("{MASSSTAB}", scaleStr)
                             .Replace("{MASSTAB}", scaleStr)
                             .Replace("{DATUM}", date)
                             .Replace("{DATE}", date);
                        if (t != (te.PlainText ?? "")) te.PlainText = t;
                        doc.Objects.AddText(te, attr);
                    }
                    else if (g2 is Curve crv)  doc.Objects.AddCurve(crv, attr);
                    else if (g2 is Brep bp)     doc.Objects.AddBrep(bp, attr);
                    else if (g2 is Hatch h)     doc.Objects.AddHatch(h, attr);
                    else if (g2 is Mesh msh)    doc.Objects.AddMesh(msh, attr);
                }
                return true;
            }
            catch { return false; }
        }

        // ---------------------------------------------------------------
        // FALLBACK TITLE BLOCK (when template not found)
        // ---------------------------------------------------------------
        private static void DrawFallbackTitleBlock(
            RhinoDoc doc, ObjectAttributes pa,
            double x0, double x1, double y0, double y1,
            string project, string drawBy, int scale)
        {
            // Outer border
            var rect = new Rectangle3d(Plane.WorldXY,
                new Interval(x0 - 2, x1), new Interval(y0, y1));
            doc.Objects.AddCurve(rect.ToNurbsCurve(), pa);

            double[] hh = { y1 - 15, y1 - 28, y1 - 42, y1 - 56 };
            foreach (double hy in hh)
                AddLine(doc, pa, new Point3d(x0 - 2, hy, 0), new Point3d(x1, hy, 0));

            AddText(doc, pa, project,                         x0, y1 - 9,  4.5);
            AddText(doc, pa, "Drawn by:  " + drawBy,           x0, y1 - 22, 2.5);
            AddText(doc, pa, "Scale:  1:" + scale,             x0, y1 - 36, 2.5);
            AddText(doc, pa, "Date:  " + DateTime.Now.ToString("dd.MM.yyyy"), x0, y1 - 50, 2.5);
        }

        // ---------------------------------------------------------------
        // PDF EXPORT
        // ---------------------------------------------------------------
        private static string TryExportPdf(RhinoPageView page, string pdfPath)
        {
            try
            {
                var pdf = Rhino.FileIO.FilePdf.Create();
                if (pdf == null) return "(FilePdf.Create() returned null)";

                // ViewCaptureSettings is in Rhino.Display
                var settings = new Rhino.Display.ViewCaptureSettings(page, 150);
                pdf.AddPage(settings);
                pdf.Write(pdfPath);
                return "PDF: " + pdfPath;
            }
            catch (Exception ex)
            {
                return "(PDF failed: " + ex.Message + ")";
            }
        }

        // ---------------------------------------------------------------
        // MATERIAL LIST
        // ---------------------------------------------------------------
        private static string BuildMaterialList(List<object> parts)
        {
            if (parts == null || parts.Count == 0)
                return "(no Parts connected)";

            var groups = new SortedDictionary<double, List<string>>();

            foreach (var p in parts)
            {
                var wrapper = p as GH_ObjectWrapper;
                var dict    = wrapper?.Value as Dictionary<string, object>;
                if (dict == null) continue;

                var outline   = dict.ContainsKey("outline")   ? dict["outline"]   as Curve  : null;
                var name      = dict.ContainsKey("panelName") ? dict["panelName"] as string : "Part";
                var thickness = dict.ContainsKey("thickness") ? Convert.ToDouble(dict["thickness"]) : 19.0;
                if (outline == null) continue;

                BoundingBox bb = outline.GetBoundingBox(true);
                double dx  = bb.Max.X - bb.Min.X;
                double dy  = bb.Max.Y - bb.Min.Y;
                double m2  = (dx / 1000.0) * (dy / 1000.0);

                if (!groups.ContainsKey(thickness))
                    groups[thickness] = new List<string>();

                groups[thickness].Add(string.Format(CultureInfo.InvariantCulture,
                    "{0,-14} {1:F0}x{2:F0}={3:F3}m²", name, dx, dy, m2));
            }

            var sb = new StringBuilder();
            foreach (var kv in groups)
            {
                double sum = 0;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "t={0:F0}mm:", kv.Key));
                foreach (var line in kv.Value)
                {
                    sb.AppendLine("  " + line);
                    // parse last token for sum
                    var tok = line.TrimEnd().Split('=');
                    if (tok.Length > 0)
                    {
                        double a;
                        var raw = tok[tok.Length - 1].Replace("m²", "").Trim();
                        if (double.TryParse(raw, NumberStyles.Any,
                            CultureInfo.InvariantCulture, out a)) sum += a;
                    }
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  Σ {0} parts / {1:F3}m²", kv.Value.Count, sum));
            }
            return sb.ToString().TrimEnd();
        }

        // ---------------------------------------------------------------
        // PAGE-SPACE HELPERS
        // ---------------------------------------------------------------
        private static void AddLine(RhinoDoc doc, ObjectAttributes attr,
            Point3d from, Point3d to)
        {
            doc.Objects.AddLine(new Line(from, to), attr);
        }

        private static void AddText(RhinoDoc doc, ObjectAttributes attr,
            string text, double x, double y, double height)
        {
            if (string.IsNullOrEmpty(text)) return;
            var te = new TextEntity
            {
                Plane      = new Plane(new Point3d(x, y, 0), Vector3d.ZAxis),
                PlainText  = text,
                TextHeight = height,
            };
            doc.Objects.AddText(te, attr);
        }

        // ---------------------------------------------------------------
        // PREVIEW OVERRIDES
        // ---------------------------------------------------------------
        public override BoundingBox ClippingBox
        {
            get
            {
                if (_preview == null) return BoundingBox.Empty;
                BoundingBox bb = BoundingBox.Empty;
                foreach (var b in _preview) bb.Union(b.GetBoundingBox(true));
                return bb;
            }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (_preview == null) return;
            var mat = new DisplayMaterial(Color.FromArgb(200, 160, 80)) { Transparency = 0.2 };
            foreach (var b in _preview) args.Display.DrawBrepShaded(b, mat);
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_preview == null) return;
            foreach (var b in _preview)
                args.Display.DrawBrepWires(b, Color.FromArgb(80, 60, 40), 1);
        }

        protected override System.Drawing.Bitmap Icon => IconHelper.Load("HopDrawing");

        public override Guid ComponentGuid => new Guid("d5e6f7a8-b9c0-4d1e-2f3a-4b5c6d7e8f9a");

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Skip(),                                         // Geometry
                WallabyHop.AutoWire.Spec.Skip(),                                         // Parts
                WallabyHop.AutoWire.Spec.Panel(
                    @"E:\Rhino Resourcen\Plan Köpfe\Leonard Elias Böker.3dm"),                      // TemplatePath
                WallabyHop.AutoWire.Spec.Panel("Project"),                               // ProjectName
                WallabyHop.AutoWire.Spec.Panel(""),                                      // DrawBy
                WallabyHop.AutoWire.Spec.Int("5<10<50"),                                 // Scale
                WallabyHop.AutoWire.Spec.Panel("FloorPlan_01"),                          // LayoutName
                WallabyHop.AutoWire.Spec.FilePath(),                                     // Folder
                WallabyHop.AutoWire.Spec.Toggle(),                                       // Generate
            });
        }
    }
}
