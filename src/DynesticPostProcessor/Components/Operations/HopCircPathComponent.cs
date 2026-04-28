using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

using WallabyHop.Logic;

namespace WallabyHop.Components.Operations
{
    public class HopCircPathComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.LimeGreen;
        private List<Brep> _previewVolumes = new List<Brep>();
        private List<Line> _approachLines  = new List<Line>();
        private Color      _drawColor      = Color.LimeGreen;

        public HopCircPathComponent() : base(
            "HopCircPath", "HopCircPath",
            "Generates circular profile path operations (Kreisbahn_V5 macro) for the DYNESTIC CNC. Cuts along a circular path with optional radius correction and arc angle.",
            "Wallaby Hop", "Milling") { }

        public override Guid ComponentGuid => new Guid("7beb0809-a67e-485b-913f-ebae9bd50294");

        protected override Bitmap Icon => IconHelper.Load("HopCircPath");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Center", "center", "Center point of the circular path. Z coordinate defines the plate surface height.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Radius", "radius", "Path radius in mm. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("RadiusCorr", "radiusCorr", "Radius correction mode: 1 = inside (tool inside path), -1 = outside, 0 = center (tool center on path). Default 0.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Depth", "depth", "Cut depth in mm, measured downward from center Z. Default 1.0.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Stepdown", "stepdown", "Depth per pass in mm (stepdown depth). 0 = single pass. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Angle", "angle", "Arc angle in degrees. 360 = full circle. Default 360.", GH_ParamAccess.item, 360.0);
            pManager.AddIntegerParameter("ToolNr", "toolNr", "Tool magazine position number. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colour", "colour", "Preview colour for the path cylinder in the Rhino viewport.", GH_ParamAccess.item, Color.LimeGreen);

            // Mark optional parameters
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[7].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines", "List of NC-Hops Kreisbahn_V5 macro strings. Wire into HopExport or HopPart.", GH_ParamAccess.list);
        }

        public override void ClearData()
        {
            base.ClearData();
            _previewVolumes.Clear();
            _approachLines.Clear();
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Hardcoded tool params -- handled at machine level
            string toolType   = "WZF";
            double feedFactor = 1.0;

            // PREVIEW: clear fields first (before guards) so disconnecting inputs wipes stale geometry
            _previewVolumes.Clear();
            _approachLines.Clear();

            // ---------------------------------------------------------------
            // GET INPUTS
            // ---------------------------------------------------------------
            Point3d center = Point3d.Unset;
            double radius = 0.0;
            int radiusCorr = 0;
            double depth = 1.0;
            double stepdown = 0.0;
            double angle = 360.0;
            int toolNr = 0;
            Color colour = Color.Empty;

            if (!DA.GetData(0, ref center)) return;
            if (!DA.GetData(1, ref radius)) return;
            DA.GetData(2, ref radiusCorr);
            DA.GetData(3, ref depth);
            DA.GetData(4, ref stepdown);
            DA.GetData(5, ref angle);
            if (!DA.GetData(6, ref toolNr)) return;
            DA.GetData(7, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // 1. DEFAULTS
            // ---------------------------------------------------------------
            // (empty list output set if guards trigger)

            // ---------------------------------------------------------------
            // 2. GUARDS
            // ---------------------------------------------------------------
            if (toolNr <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "toolNr is required and must be > 0");
                DA.SetDataList(0, new List<string>());
                return;
            }

            if (radius <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "radius must be > 0");
                DA.SetDataList(0, new List<string>());
                return;
            }

            // ---------------------------------------------------------------
            // 3. INPUT DEFAULTS
            // ---------------------------------------------------------------
            if (depth <= 0) depth = 1.0;
            if (angle <= 0) angle = 360.0;

            // PREVIEW: cylinder at the circular path -- extrude the path circle downward by depth
            double previewZ = center.Z;
            Point3d circlePt = new Point3d(center.X, center.Y, previewZ);
            Plane cylPlane = new Plane(circlePt, Vector3d.ZAxis);
            Circle pathCircle = new Circle(cylPlane, radius);
            Curve pathCurve = pathCircle.ToNurbsCurve();
            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            Vector3d extDir = new Vector3d(0, 0, -Math.Abs(depth));
            Surface extSrf = Surface.CreateExtrusion(pathCurve, extDir);
            if (extSrf != null)
            {
                Brep extBrep = extSrf.ToBrep();
                if (extBrep != null)
                {
                    Brep capped = extBrep.CapPlanarHoles(tol);
                    _previewVolumes.Add(capped != null ? capped : extBrep);
                }
            }

            // PREVIEW: approach line from safeZ to the 3 o'clock entry point
            double safeZ = center.Z + 20.0;
            Point3d entryPt = new Point3d(center.X + radius, center.Y, previewZ);
            _approachLines.Add(new Line(new Point3d(entryPt.X, entryPt.Y, safeZ), entryPt));

            // ---------------------------------------------------------------
            // 4. DELEGATE TO PURE CircPathLogic
            // ---------------------------------------------------------------
            var lines = CircPathLogic.Generate(new CircPathLogic.CircPathInput
            {
                CenterX = center.X, CenterY = center.Y, SurfaceZ = center.Z,
                Radius = radius,
                RadiusCorr = radiusCorr,
                Depth = depth,
                Stepdown = stepdown,
                Angle = angle,
                ToolNr = toolNr,
                ToolType = toolType,
                FeedFactor = feedFactor,
            });

            // ---------------------------------------------------------------
            // 5. OUTPUT
            // ---------------------------------------------------------------
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "CircPath: R=" + radius.ToString(CultureInfo.InvariantCulture)
                + " corr=" + radiusCorr.ToString()
                + " angle=" + angle.ToString(CultureInfo.InvariantCulture));

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
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Point(),
                WallabyHop.AutoWire.Spec.Float("1<25<500"),
                WallabyHop.AutoWire.Spec.Int("-5<0<5"),
                WallabyHop.AutoWire.Spec.Float("1<10<100"),
                WallabyHop.AutoWire.Spec.Float("0<0<50"),
                WallabyHop.AutoWire.Spec.Float("0<0<360"),
                WallabyHop.AutoWire.Spec.Int("1<1<20"),
                WallabyHop.AutoWire.Spec.Skip(),
            });
        }
    }
}
