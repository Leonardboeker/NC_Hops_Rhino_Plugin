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
    public class HopCircPocketComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.Cyan;
        private List<Brep> _previewVolumes = new List<Brep>();
        private List<Line> _approachLines  = new List<Line>();
        private Color      _drawColor      = Color.Cyan;

        public HopCircPocketComponent() : base(
            "HopCircPocket", "HopCircPocket",
            "Generates circular pocket operations (Kreistasche_V5 macro) for the DYNESTIC CNC. Creates a cylindrical pocket at the specified center point, radius, and depth.",
            "Wallaby Hop", "Milling") { }

        public override Guid ComponentGuid => new Guid("795d39f9-23ad-4499-966e-583a3e17439e");

        protected override Bitmap Icon => IconHelper.Load("HopCircPocket");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Center", "center", "Center point of the circular pocket. Z coordinate defines the plate surface height.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Radius", "radius", "Pocket radius in mm. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Depth", "depth", "Pocket depth in mm, measured downward from center Z. Default 1.0.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Stepdown", "stepdown", "Depth per pass in mm (stepdown). 0 = single pass. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddIntegerParameter("ToolNr", "toolNr", "Tool magazine position number. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colour", "colour", "Preview colour for the pocket cylinder in the Rhino viewport.", GH_ParamAccess.item, Color.Cyan);

            // Mark optional parameters
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines", "List of NC-Hops Kreistasche_V5 macro strings. Wire into HopExport or HopPart.", GH_ParamAccess.list);
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
            double depth = 1.0;
            double stepdown = 0.0;
            int toolNr = 0;
            Color colour = Color.Empty;

            if (!DA.GetData(0, ref center)) return;
            if (!DA.GetData(1, ref radius)) return;
            DA.GetData(2, ref depth);
            DA.GetData(3, ref stepdown);
            if (!DA.GetData(4, ref toolNr)) return;
            DA.GetData(5, ref colour);

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

            // PREVIEW: cylinder from center surface downward by depth
            Plane cylPlane = new Plane(center, Vector3d.ZAxis);
            Circle cylCircle = new Circle(cylPlane, radius);
            Cylinder cyl = new Cylinder(cylCircle, -Math.Abs(depth));
            Brep cylBrep = cyl.ToBrep(true, true);
            if (cylBrep != null)
                _previewVolumes.Add(cylBrep);

            // PREVIEW: approach line from safeZ to circle center
            double safeZ = center.Z + MachineConstants.PreviewSafeZOffset;
            _approachLines.Add(new Line(new Point3d(center.X, center.Y, safeZ), center));

            // ---------------------------------------------------------------
            // 4. DELEGATE TO PURE PocketLogic
            // ---------------------------------------------------------------
            var lines = PocketLogic.GenerateCirc(new PocketLogic.CircPocketInput
            {
                CenterX = center.X,
                CenterY = center.Y,
                SurfaceZ = center.Z,
                Radius = radius,
                Depth = depth,
                Stepdown = stepdown,
                ToolNr = toolNr,
                ToolType = toolType,
                FeedFactor = feedFactor,
            });

            // ---------------------------------------------------------------
            // 5. OUTPUT
            // ---------------------------------------------------------------
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "CircPocket: R=" + radius.ToString(CultureInfo.InvariantCulture)
                + " at (" + center.X.ToString(CultureInfo.InvariantCulture)
                + ", " + center.Y.ToString(CultureInfo.InvariantCulture) + ")");

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
                WallabyHop.AutoWire.Spec.Float("1<10<100"),
                WallabyHop.AutoWire.Spec.Float("0<0<50"),
                WallabyHop.AutoWire.Spec.Int("1<1<20"),
                WallabyHop.AutoWire.Spec.Skip(),
            });
        }
    }
}
