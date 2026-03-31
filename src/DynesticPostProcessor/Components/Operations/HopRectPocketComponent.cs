using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Operations
{
    public class HopRectPocketComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.Cyan;
        private Brep  _previewVolume = null;
        private Line  _approachLine  = Line.Unset;
        private Color _drawColor     = Color.Cyan;

        public HopRectPocketComponent() : base(
            "HopRectPocket", "HopRectPocket",
            "Generates rectangular pocket operations (RechteckTasche_V5 macro) for the DYNESTIC CNC. Extracts center and dimensions from a rectangle curve's bounding box.",
            "DYNESTIC", "Operations") { }

        public override Guid ComponentGuid => new Guid("6e2f23b6-557f-46a1-80a7-41feebc7982d");

        protected override Bitmap Icon => Properties.Resources.HopRectPocket;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("RectCurve", "rectCurve", "Closed rectangle curve defining the pocket boundary. Center and dimensions are extracted from its bounding box.", GH_ParamAccess.item);
            pManager.AddNumberParameter("CornerRadius", "cornerRadius", "Fillet radius for pocket corners in mm. 0 = sharp corners. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Angle", "angle", "Rotation angle of the pocket in degrees. 0 = axis-aligned. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Depth", "depth", "Pocket depth in mm, measured downward from the curve's Z. Default 1.0.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Stepdown", "stepdown", "Depth per pass in mm (Zustellung). 0 = single pass. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddIntegerParameter("ToolNr", "toolNr", "Tool magazine position number. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colour", "colour", "Preview colour for the pocket volume in the Rhino viewport.", GH_ParamAccess.item, Color.Cyan);

            // Mark optional parameters
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines", "List of NC-Hops macro strings (CALL _RechteckTasche_V5). Wire into HopExport or HopPart.", GH_ParamAccess.list);
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
            Curve rectCurve = null;
            double cornerRadius = 0.0;
            double angle = 0.0;
            double depth = 1.0;
            double stepdown = 0.0;
            int toolNr = 0;
            Color colour = Color.Empty;

            if (!DA.GetData(0, ref rectCurve)) return;
            DA.GetData(1, ref cornerRadius);
            DA.GetData(2, ref angle);
            DA.GetData(3, ref depth);
            DA.GetData(4, ref stepdown);
            if (!DA.GetData(5, ref toolNr)) return;
            DA.GetData(6, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // 2. GUARDS
            // ---------------------------------------------------------------
            if (rectCurve == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No rectangle curve connected");
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
            // 3. INPUT DEFAULTS
            // ---------------------------------------------------------------
            if (cornerRadius < 0) cornerRadius = 0;
            if (depth <= 0) depth = 1.0;

            // ---------------------------------------------------------------
            // 4. EXTRACT DIMENSIONS from BoundingBox
            // ---------------------------------------------------------------
            BoundingBox bb = rectCurve.GetBoundingBox(true);
            double cx = (bb.Min.X + bb.Max.X) / 2.0;
            double cy = (bb.Min.Y + bb.Max.Y) / 2.0;
            double width  = bb.Max.X - bb.Min.X;
            double height = bb.Max.Y - bb.Min.Y;

            // PREVIEW: box from bounding rect at surface down to -depth
            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            double topZ  = bb.Max.Z;
            double botZ  = topZ - Math.Abs(depth);

            // Build rotated box: create base rect at topZ, extrude down
            double previewZ = topZ;
            Rectangle3d previewBounds = new Rectangle3d(
                new Plane(new Point3d(cx, cy, previewZ), Vector3d.XAxis, Vector3d.YAxis),
                new Interval(-width / 2.0, width / 2.0),
                new Interval(-height / 2.0, height / 2.0));
            Curve baseCurve = previewBounds.ToNurbsCurve();

            if (cornerRadius > 0)
            {
                Curve filleted = Curve.CreateFilletCornersCurve(baseCurve, cornerRadius, 1e-6, 1e-6);
                if (filleted != null) baseCurve = filleted;
            }

            if (angle != 0)
            {
                double angleRad = angle * Math.PI / 180.0;
                Point3d centerPoint = new Point3d(cx, cy, previewZ);
                baseCurve.Transform(Transform.Rotation(angleRad, Vector3d.ZAxis, centerPoint));
            }

            // Extrude the closed base curve downward
            if (baseCurve.IsClosed)
            {
                Vector3d extDir = new Vector3d(0, 0, -(topZ - botZ));
                Surface extSrf = Surface.CreateExtrusion(baseCurve, extDir);
                if (extSrf != null)
                {
                    Brep extBrep = extSrf.ToBrep();
                    if (extBrep != null)
                    {
                        Brep capped = extBrep.CapPlanarHoles(tol);
                        _previewVolume = capped != null ? capped : extBrep;
                    }
                }
            }

            // PREVIEW: approach line from safeZ to bottom-left corner
            double safeZ = rectCurve.GetBoundingBox(true).Max.Z + 20.0;
            Point3d startPt = new Point3d(bb.Min.X, bb.Min.Y, topZ);
            _approachLine = new Line(new Point3d(startPt.X, startPt.Y, safeZ), startPt);

            // ---------------------------------------------------------------
            // 5. BUILD TOOL CALL (first line)
            // ---------------------------------------------------------------
            List<string> lines = new List<string>();
            string toolCall = toolType + " (" + toolNr.ToString()
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')";
            lines.Add(toolCall);

            // ---------------------------------------------------------------
            // 6. BUILD CALL MACRO
            // ---------------------------------------------------------------
            double surfaceZ   = bb.Min.Z;
            double cutZ       = surfaceZ - Math.Abs(depth);
            double zustellung = (stepdown > 0) ? stepdown : 0;

            lines.Add("CALL _RechteckTasche_V5(VAL "
                + "x_Mitte:=" + cx.ToString(CultureInfo.InvariantCulture) + ","
                + "Y_Mitte:=" + cy.ToString(CultureInfo.InvariantCulture) + ","
                + "Taschenlaenge:=" + width.ToString(CultureInfo.InvariantCulture) + ","
                + "Taschenbreite:=" + height.ToString(CultureInfo.InvariantCulture) + ","
                + "Radius:=" + cornerRadius.ToString(CultureInfo.InvariantCulture) + ","
                + "Winkel:=" + angle.ToString(CultureInfo.InvariantCulture) + ","
                + "Tiefe:=" + cutZ.ToString(CultureInfo.InvariantCulture) + ","
                + "Zustellung:=" + zustellung.ToString(CultureInfo.InvariantCulture) + ","
                + "AB:=2,ABF:=_ANF,Interpol:=1,umkehren:=0,esxy:=0,esmd:=0,laser:=0)");

            // ---------------------------------------------------------------
            // 7. OUTPUT
            // ---------------------------------------------------------------
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "RectPocket: " + width.ToString(CultureInfo.InvariantCulture)
                + " x " + height.ToString(CultureInfo.InvariantCulture)
                + " at (" + cx.ToString(CultureInfo.InvariantCulture)
                + ", " + cy.ToString(CultureInfo.InvariantCulture) + ")");

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
