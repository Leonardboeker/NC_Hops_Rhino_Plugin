using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Operations
{
    public class HopCircPathComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.LimeGreen;
        private Brep  _previewVolume = null;
        private Line  _approachLine  = Line.Unset;
        private Color _drawColor     = Color.LimeGreen;

        public HopCircPathComponent() : base(
            "HopCircPath", "HopCircPath",
            "Generates circular profile path operations (Kreisbahn_V5 macro) for the DYNESTIC CNC. Cuts along a circular path with optional radius correction and arc angle.",
            "DYNESTIC", "Operations") { }

        public override Guid ComponentGuid => new Guid("7beb0809-a67e-485b-913f-ebae9bd50294");

        protected override Bitmap Icon => Properties.Resources.HopCircPath;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Center", "center", "Center point of the circular path. Z coordinate defines the plate surface height.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Radius", "radius", "Path radius in mm. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("RadiusCorr", "radiusCorr", "Radius correction mode: 1 = inside (tool inside path), -1 = outside, 0 = center (tool center on path). Default 0.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Depth", "depth", "Cut depth in mm, measured downward from center Z. Default 1.0.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Stepdown", "stepdown", "Depth per pass in mm (ZuTiefe). 0 = single pass. Default 0.", GH_ParamAccess.item, 0.0);
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
                    _previewVolume = capped != null ? capped : extBrep;
                }
            }

            // PREVIEW: approach line from safeZ to the 3 o'clock entry point
            double safeZ = center.Z + 20.0;
            Point3d entryPt = new Point3d(center.X + radius, center.Y, previewZ);
            _approachLine = new Line(new Point3d(entryPt.X, entryPt.Y, safeZ), entryPt);

            // ---------------------------------------------------------------
            // 4. BUILD TOOL CALL + MACRO
            // ---------------------------------------------------------------
            List<string> lines = new List<string>();
            lines.Add(toolType + " (" + toolNr.ToString()
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            // surfaceZ: Z of the input center point
            double surfaceZ  = center.Z;
            double cutZ      = surfaceZ - Math.Abs(depth);
            double zuTiefe   = (stepdown > 0) ? stepdown : 0;

            lines.Add("CALL _Kreisbahn_V5(VAL "
                + "X_Mitte:=" + center.X.ToString(CultureInfo.InvariantCulture) + ","
                + "Y_Mitte:=" + center.Y.ToString(CultureInfo.InvariantCulture) + ","
                + "Tiefe:=" + cutZ.ToString(CultureInfo.InvariantCulture) + ","
                + "ZuTiefe:=" + zuTiefe.ToString(CultureInfo.InvariantCulture) + ","
                + "Radius:=" + radius.ToString(CultureInfo.InvariantCulture) + ","
                + "Radiuskorrektur:=" + radiusCorr.ToString() + ","
                + "AB:=1,Aufmass:=0,Bearb_umkehren:=1,"
                + "Winkel:=" + angle.ToString(CultureInfo.InvariantCulture) + ","
                + "ANF:=_ANF,ABF:=_ANF,Rampe:=1,Interpol:=0,esxy:=0,esmd:=0,laser:=0)");

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
    }
}
