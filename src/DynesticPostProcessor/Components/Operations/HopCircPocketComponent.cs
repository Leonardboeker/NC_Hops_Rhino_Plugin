using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Operations
{
    public class HopCircPocketComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.Cyan;
        private Brep  _previewVolume = null;
        private Line  _approachLine  = Line.Unset;
        private Color _drawColor     = Color.Cyan;

        public HopCircPocketComponent() : base(
            "HopCircPocket", "HopCircPocket",
            "Generates circular pocket operations (Kreistasche_V5 macro) for the DYNESTIC CNC. Creates a cylindrical pocket at the specified center point, radius, and depth.",
            "DYNESTIC", "Operations") { }

        public override Guid ComponentGuid => new Guid("795d39f9-23ad-4499-966e-583a3e17439e");

        protected override Bitmap Icon => IconHelper.Load("HopCircPocket");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Center", "center", "Center point of the circular pocket. Z coordinate defines the plate surface height.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Radius", "radius", "Pocket radius in mm. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Depth", "depth", "Pocket depth in mm, measured downward from center Z. Default 1.0.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Stepdown", "stepdown", "Depth per pass in mm (Zustellung). 0 = single pass. Default 0.", GH_ParamAccess.item, 0.0);
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
            _previewVolume = null;
            _approachLine = Line.Unset;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Hardcoded tool params -- handled at machine level
            string toolType   = "WZF";
            double feedFactor = 1.0;

            // PREVIEW: clear fields first (before guards) so disconnecting inputs wipes stale geometry
            _previewVolume = null;
            _approachLine  = Line.Unset;

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
                _previewVolume = cylBrep;

            // PREVIEW: approach line from safeZ to circle center
            double safeZ = center.Z + 20.0;
            _approachLine = new Line(new Point3d(center.X, center.Y, safeZ), center);

            // ---------------------------------------------------------------
            // 4. BUILD TOOL CALL + MACRO
            // ---------------------------------------------------------------
            List<string> lines = new List<string>();
            lines.Add(toolType + " (" + toolNr.ToString()
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            // surfaceZ: Z of the input center point
            double surfaceZ   = center.Z;
            double cutZ       = surfaceZ - Math.Abs(depth);
            double zustellung = (stepdown > 0) ? stepdown : 0;

            lines.Add("CALL _Kreistasche_V5(VAL "
                + "X_Mitte:=" + center.X.ToString(CultureInfo.InvariantCulture) + ","
                + "Y_Mitte:=" + center.Y.ToString(CultureInfo.InvariantCulture) + ","
                + "Radius:=" + radius.ToString(CultureInfo.InvariantCulture) + ","
                + "Tiefe:=" + cutZ.ToString(CultureInfo.InvariantCulture) + ","
                + "Zustellung:=" + zustellung.ToString(CultureInfo.InvariantCulture) + ","
                + "AB:=2,ABF:=_ANF,Interpol:=0,umkehren:=0,esxy:=0,esmd:=0,laser:=0)");

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
            get
            {
                BoundingBox bb = BoundingBox.Empty;
                if (_previewVolume != null) bb.Union(_previewVolume.GetBoundingBox(true));
                if (_approachLine.IsValid) { bb.Union(_approachLine.From); bb.Union(_approachLine.To); }
                return bb;
            }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (_previewVolume != null)
            {
                Rhino.Display.DisplayMaterial mat = new Rhino.Display.DisplayMaterial(_drawColor);
                mat.Transparency = 0.55;
                args.Display.DrawBrepShaded(_previewVolume, mat);
            }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_previewVolume != null)
                args.Display.DrawBrepWires(_previewVolume, _drawColor, 1);
            if (_approachLine.IsValid)
                args.Display.DrawPatternedLine(
                    _approachLine.From, _approachLine.To,
                    Color.FromArgb(140, 140, 140), unchecked((int)0xF0F0F0F0), 1);
        }

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.Point(),
                DynesticPostProcessor.AutoWire.Spec.Float("1<25<500"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<10<100"),
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<50"),
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
