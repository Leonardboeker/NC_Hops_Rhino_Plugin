using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Operations
{
    public class HopContourComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.Yellow;
        private Brep  _previewVolume = null;
        private Line  _approachLine  = Line.Unset;
        private Color _drawColor     = Color.Yellow;

        public HopContourComponent() : base(
            "HopContour", "HopContour",
            "Generates 2D contour cutting paths for the DYNESTIC CNC. Converts planar curves into SP/G01/G03M macro sequences with optional kerf compensation and multi-pass stepdown.",
            "DYNESTIC", "Operations") { }

        public override Guid ComponentGuid => new Guid("e2902790-ccf6-4880-b284-80e0110f1e71");

        protected override Bitmap Icon => IconHelper.Load("HopContour");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "curve", "Planar closed or open curve defining the contour cutting path. Must lie in or near the World XY plane.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Depth", "depth", "Cutting depth in mm, measured downward from the curve's Z position. Default 1.0.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("PlungeZ", "plungeZ", "Plunge depth override in mm. When 0, equals depth. Use to plunge shallower than full depth on first pass.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Tolerance", "tolerance", "NURBS to polyline/arc conversion tolerance in mm. Smaller = more segments, higher accuracy. Default 0.1.", GH_ParamAccess.item, 0.1);
            pManager.AddIntegerParameter("ToolNr", "toolNr", "Tool magazine position number. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddNumberParameter("ToolDiameter", "toolDiameter", "Tool diameter in mm, used for kerf compensation offset when side != 0. Default 8.0.", GH_ParamAccess.item, 8.0);
            pManager.AddIntegerParameter("Side", "side", "Kerf compensation side: -1 = left of travel (inside cut), 0 = center (no offset), +1 = right of travel (outside cut). Default 0.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Stepdown", "stepdown", "Depth per pass in mm for multi-pass cutting. 0 = single pass at full depth. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddColourParameter("Colour", "colour", "Preview colour for the toolpath volume in the Rhino viewport.", GH_ParamAccess.item, Color.Yellow);

            // Mark optional parameters
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines", "List of NC-Hops macro strings (SP, G01, G03M, EP blocks). Wire into HopExport or HopPart.", GH_ParamAccess.list);
        }

        public override void ClearData()
        {
            base.ClearData();
            _previewVolume = null;
            _approachLine = Line.Unset;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string toolType   = "WZF";
            double feedFactor = 1.0;

            _previewVolume = null;
            _approachLine  = Line.Unset;

            // ---------------------------------------------------------------
            // GET INPUTS
            // ---------------------------------------------------------------
            Curve curve = null;
            double depth = 1.0;
            double plungeZ = 0.0;
            double tolerance = 0.1;
            int toolNr = 0;
            double toolDiameter = 8.0;
            int side = 0;
            double stepdown = 0.0;
            Color colour = Color.Empty;

            if (!DA.GetData(0, ref curve)) return;
            DA.GetData(1, ref depth);
            DA.GetData(2, ref plungeZ);
            DA.GetData(3, ref tolerance);
            if (!DA.GetData(4, ref toolNr)) return;
            DA.GetData(5, ref toolDiameter);
            DA.GetData(6, ref side);
            DA.GetData(7, ref stepdown);
            DA.GetData(8, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // 1. DEFAULTS
            // ---------------------------------------------------------------
            // (empty list output set if guards trigger)

            // ---------------------------------------------------------------
            // 2. GUARDS
            // ---------------------------------------------------------------
            if (curve == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No curve connected");
                DA.SetDataList(0, new List<string>());
                return;
            }
            if (toolNr <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "toolNr must be > 0");
                DA.SetDataList(0, new List<string>());
                return;
            }

            // ---------------------------------------------------------------
            // 3. INPUT DEFAULTS
            // ---------------------------------------------------------------
            if (tolerance    <= 0) tolerance    = 0.1;
            if (depth        <= 0) depth        = 1.0;
            if (plungeZ      <= 0) plungeZ      = depth;
            if (toolDiameter <= 0) toolDiameter = 8.0;
            // side: clamp to -1 / 0 / +1
            if (side > 0)  side =  1;
            if (side < 0)  side = -1;

            // ---------------------------------------------------------------
            // 4. PLANARITY CHECK
            // ---------------------------------------------------------------
            if (!curve.IsPlanar())
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Curve is not planar -- cannot use for 2D contour");
                DA.SetDataList(0, new List<string>());
                return;
            }

            // ---------------------------------------------------------------
            // 5. GEOMETRIC PRE-OFFSET (when side != 0)
            // ---------------------------------------------------------------
            double tol    = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            double radius = toolDiameter / 2.0;

            Curve cuttingCurve = curve;

            if (side != 0)
            {
                double offsetDist = side * radius;
                Curve[] offsets = curve.Offset(Plane.WorldXY, offsetDist, tol,
                    CurveOffsetCornerStyle.Sharp);
                if (offsets != null && offsets.Length > 0)
                {
                    cuttingCurve = offsets.Length == 1 ? offsets[0]
                        : Curve.JoinCurves(offsets, tol)[0];
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Side offset failed -- using center path");
                }
            }

            // ---------------------------------------------------------------
            // 6. PREVIEW VOLUME
            // ---------------------------------------------------------------
            double surfaceZ = curve.GetBoundingBox(true).Min.Z;
            double cutZ     = surfaceZ - Math.Abs(plungeZ > 0 ? plungeZ : depth);

            Curve baseCrv = curve.DuplicateCurve();
            baseCrv.Translate(new Vector3d(0, 0, cutZ - baseCrv.PointAtStart.Z));

            Curve cuttingBase = cuttingCurve.DuplicateCurve();
            cuttingBase.Translate(new Vector3d(0, 0, cutZ - cuttingBase.PointAtStart.Z));

            Curve innerBoundary;
            Curve outerBoundary;

            if (side == 0)
            {
                Curve[] outerArr = baseCrv.Offset(Plane.WorldXY,  radius, tol, CurveOffsetCornerStyle.Sharp);
                Curve[] innerArr = baseCrv.Offset(Plane.WorldXY, -radius, tol, CurveOffsetCornerStyle.Sharp);
                outerBoundary = (outerArr != null && outerArr.Length > 0) ? outerArr[0] : null;
                innerBoundary = (innerArr != null && innerArr.Length > 0) ? innerArr[0] : null;
            }
            else
            {
                outerBoundary = baseCrv;
                innerBoundary = cuttingBase;
            }

            if (outerBoundary != null && innerBoundary != null)
            {
                Brep[] planar = Brep.CreatePlanarBreps(
                    new Curve[] { outerBoundary, innerBoundary }, tol);
                if (planar != null && planar.Length > 0)
                {
                    Vector3d extDir  = new Vector3d(0, 0, -Math.Abs(depth));
                    LineCurve extPath = new LineCurve(new Line(Point3d.Origin, Point3d.Origin + extDir));
                    Brep vol = planar[0].Faces[0].CreateExtrusion(extPath, true);
                    if (vol != null) _previewVolume = vol;
                }
            }

            // Dashed approach line
            BoundingBox curveBB = curve.GetBoundingBox(true);
            double safeZ   = curveBB.Max.Z + 20.0;
            Point3d startP = cuttingBase.PointAtStart;
            _approachLine  = new Line(new Point3d(startP.X, startP.Y, safeZ), startP);

            // ---------------------------------------------------------------
            // 7. CURVE DECOMPOSITION on cuttingCurve (offset or original)
            // ---------------------------------------------------------------
            PolyCurve pc = cuttingCurve.ToArcsAndLines(tolerance, 0.1, 0.0, 0.0) as PolyCurve;
            if (pc == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Curve conversion to arcs and lines failed");
                DA.SetDataList(0, new List<string>());
                return;
            }

            // ---------------------------------------------------------------
            // 8. BUILD NC OUTPUT
            // ---------------------------------------------------------------
            List<string> lines = new List<string>();
            lines.Add(toolType + " (" + toolNr.ToString()
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            if (stepdown > 0)
            {
                int passCount = (int)Math.Ceiling(depth / stepdown);
                for (int p = 0; p < passCount; p++)
                {
                    double passDepth = Math.Min((p + 1) * stepdown, depth);
                    BuildContourBlock(lines, pc, surfaceZ - passDepth);
                }
            }
            else
            {
                BuildContourBlock(lines, pc, surfaceZ - Math.Abs(plungeZ));
            }

            // ---------------------------------------------------------------
            // 9. REMARK + OUTPUT
            // ---------------------------------------------------------------
            string sideLabel = side == 0 ? "center" : (side < 0 ? "left (inside)" : "right (outside)");
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Contour: " + pc.SegmentCount.ToString() + " segments"
                + "  depth=" + (-Math.Abs(depth)).ToString(CultureInfo.InvariantCulture)
                + "  side=" + sideLabel
                + "  toolD=" + toolDiameter.ToString(CultureInfo.InvariantCulture));

            DA.SetDataList(0, lines);
        }

        // ---------------------------------------------------------------
        // HELPER: Build one SP...moves...EP contour block
        // ---------------------------------------------------------------
        private void BuildContourBlock(List<string> lines, PolyCurve pc, double zEintauch)
        {
            Point3d startPt = pc.PointAtStart;
            lines.Add("SP (" + startPt.X.ToString(CultureInfo.InvariantCulture) + ","
                + startPt.Y.ToString(CultureInfo.InvariantCulture) + ","
                + zEintauch.ToString(CultureInfo.InvariantCulture)
                + ",2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)");

            for (int i = 0; i < pc.SegmentCount; i++)
            {
                Curve seg = pc.SegmentCurve(i);

                ArcCurve arcSeg = seg as ArcCurve;
                if (arcSeg != null)
                {
                    Arc arc    = arcSeg.Arc;
                    Point3d ep = arc.EndPoint;
                    Point3d cp = arc.Center;
                    bool isCCW = arc.Plane.Normal.Z >= 0;
                    string cmd = isCCW ? "G03M" : "G02M";
                    lines.Add(cmd + " ("
                        + ep.X.ToString(CultureInfo.InvariantCulture) + ","
                        + ep.Y.ToString(CultureInfo.InvariantCulture) + ",0,"
                        + cp.X.ToString(CultureInfo.InvariantCulture) + ","
                        + cp.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,2,0)");
                    continue;
                }

                LineCurve lineSeg = seg as LineCurve;
                if (lineSeg != null)
                {
                    Point3d ep = lineSeg.PointAtEnd;
                    lines.Add("G01 ("
                        + ep.X.ToString(CultureInfo.InvariantCulture) + ","
                        + ep.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,0,2)");
                    continue;
                }

                Point3d fallback = seg.PointAtEnd;
                lines.Add("G01 ("
                    + fallback.X.ToString(CultureInfo.InvariantCulture) + ","
                    + fallback.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,0,2)");
            }

            lines.Add("EP (0,_ANF,0)");
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
                mat.Transparency = 0.5;
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
                DynesticPostProcessor.AutoWire.Spec.Curve(),
                DynesticPostProcessor.AutoWire.Spec.Float("1<10<100"),
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<100"),
                DynesticPostProcessor.AutoWire.Spec.Float("0.001<0.1<1"),
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<8<50"),
                DynesticPostProcessor.AutoWire.Spec.Int("-1<0<1"),
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<50"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
