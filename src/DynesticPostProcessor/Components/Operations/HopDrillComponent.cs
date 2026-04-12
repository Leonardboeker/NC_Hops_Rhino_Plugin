using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Operations
{
    public class HopDrillComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.Red;
        private List<Brep> _previewVolumes = new List<Brep>();
        private List<Line> _approachLines  = new List<Line>();
        private Color      _drawColor      = Color.Red;

        public HopDrillComponent() : base(
            "HopDrill", "HopDrill",
            "Generates vertical drilling operations (Bohrung macros) for the DYNESTIC CNC. Each input point becomes one drill call at the specified depth and diameter.",
            "DYNESTIC", "Bohren") { }

        public override Guid ComponentGuid => new Guid("2a763260-a3c1-4231-8ed0-cd0085267c94");

        protected override Bitmap Icon => IconHelper.Load("HopDrill");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "points", "Drill positions as 3D points. The Z coordinate of the highest point defines the plate surface height.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Depth", "depth", "Drilling depth in mm, measured downward from the highest point's Z. Default 1.0.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Diameter", "diameter", "Drill hole diameter in mm. Default 8.0.", GH_ParamAccess.item, 8.0);
            pManager.AddNumberParameter("Stepdown", "stepdown", "Depth per pass in mm for peck drilling. 0 = single pass at full depth. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddIntegerParameter("ToolNr", "toolNr", "Tool magazine position number. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colour", "colour", "Preview colour for drill cylinders in the Rhino viewport.", GH_ParamAccess.item, Color.Red);

            // Mark optional parameters
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines", "List of NC-Hops Bohrung macro strings. Wire into HopExport or HopPart.", GH_ParamAccess.list);
        }

        public override void ClearData()
        {
            base.ClearData();
            _previewVolumes.Clear();
            _approachLines.Clear();
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // PREVIEW: clear fields first (before guards) so disconnecting inputs wipes stale geometry
            _previewVolumes.Clear();
            _approachLines.Clear();

            // ---------------------------------------------------------------
            // GET INPUTS
            // ---------------------------------------------------------------
            List<Point3d> points = new List<Point3d>();
            double depth = 1.0;
            double diameter = 8.0;
            double stepdown = 0.0;
            int toolNr = 0;
            Color colour = Color.Empty;

            if (!DA.GetDataList(0, points)) return;
            DA.GetData(1, ref depth);
            DA.GetData(2, ref diameter);
            DA.GetData(3, ref stepdown);
            if (!DA.GetData(4, ref toolNr)) return;
            DA.GetData(5, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // 1. DEFAULTS
            // ---------------------------------------------------------------
            // (empty list output set if guards trigger)

            // ---------------------------------------------------------------
            // 2. GUARDS -- required inputs
            // ---------------------------------------------------------------
            if (points == null || points.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No drill points connected");
                DA.SetDataList(0, new List<string>());
                return;
            }
            if (toolNr <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "toolNr is required and must be > 0");
                DA.SetDataList(0, new List<string>());
                return;
            }

            // ---------------------------------------------------------------
            // 3. INPUT DEFAULTS -- fallback for disconnected optional inputs
            // ---------------------------------------------------------------
            if (depth <= 0) depth = 1.0;
            if (diameter <= 0) diameter = 8.0;

            // surfaceZ: highest Z across all input points
            double surfaceZ = points[0].Z;
            foreach (Point3d p in points) if (p.Z > surfaceZ) surfaceZ = p.Z;

            // PREVIEW: cylinder per drill point (after diameter default applied)
            double radius = diameter / 2.0;
            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            for (int i = 0; i < points.Count; i++)
            {
                Point3d pt = new Point3d(points[i].X, points[i].Y, surfaceZ);
                Plane cylPlane = new Plane(pt, Vector3d.ZAxis);
                Circle cylCircle = new Circle(cylPlane, radius);
                Cylinder cyl = new Cylinder(cylCircle, -Math.Abs(depth));
                Brep cylBrep = cyl.ToBrep(true, true);
                if (cylBrep != null)
                    _previewVolumes.Add(cylBrep);
            }
            // PREVIEW: approach line above first point
            if (points.Count > 0)
            {
                Point3d firstPt = new Point3d(points[0].X, points[0].Y, surfaceZ);
                double safeZ = surfaceZ + 20.0;
                _approachLines.Add(new Line(new Point3d(firstPt.X, firstPt.Y, safeZ), firstPt));
            }

            // ---------------------------------------------------------------
            // 4. BUILD TOOL CALL -- first line of output
            // ---------------------------------------------------------------
            List<string> lines = new List<string>();
            lines.Add(NcDrill.ToolCall(toolNr));

            // ---------------------------------------------------------------
            // 5. MULTI-PASS OR SINGLE-PASS DRILLING
            // ---------------------------------------------------------------
            if (stepdown > 0)
            {
                int passCount = (int)Math.Ceiling(depth / stepdown);
                for (int i = 0; i < points.Count; i++)
                {
                    Point3d pt = points[i];
                    for (int p = 0; p < passCount; p++)
                    {
                        double passDepth = Math.Min((p + 1) * stepdown, depth);
                        double cutZ = surfaceZ - passDepth;
                        lines.Add(NcDrill.BohrungLine(pt.X, pt.Y, surfaceZ, cutZ, diameter));
                    }
                }
            }
            else
            {
                double cutZ = surfaceZ - Math.Abs(depth);
                for (int i = 0; i < points.Count; i++)
                {
                    Point3d pt = points[i];
                    lines.Add(NcDrill.BohrungLine(pt.X, pt.Y, surfaceZ, cutZ, diameter));
                }
            }

            // ---------------------------------------------------------------
            // 6. OUTPUT + REMARK
            // ---------------------------------------------------------------
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                points.Count.ToString() + " drill points, surfaceZ=" + surfaceZ.ToString(CultureInfo.InvariantCulture)
                + ", cutZ=" + (surfaceZ - Math.Abs(depth)).ToString(CultureInfo.InvariantCulture)
                + ", diameter=" + diameter.ToString(CultureInfo.InvariantCulture));

            DA.SetDataList(0, lines);
        }

        // ---------------------------------------------------------------
        // PREVIEW OVERRIDES
        // ---------------------------------------------------------------
        public override BoundingBox ClippingBox
        {
            get { return PreviewHelper.GetClippingBox(_previewVolumes, _approachLines); }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            PreviewHelper.DrawMeshes(args, _previewVolumes, _drawColor);
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            PreviewHelper.DrawWires(args, _previewVolumes, _approachLines, _drawColor);
        }

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.Point(),
                DynesticPostProcessor.AutoWire.Spec.Float("1<10<100"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<8<50"),
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<50"),
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
