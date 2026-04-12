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
        private List<Brep> _previewVolumes = new List<Brep>();
        private List<Line> _approachLines  = new List<Line>();
        private Color      _drawColor      = Color.Yellow;

        public HopContourComponent() : base(
            "HopContour", "HopContour",
            "Generates 2D contour cutting paths for the DYNESTIC CNC. Converts planar curves into SP/G01/G03M macro sequences with optional kerf compensation and multi-pass stepdown.",
            "DYNESTIC", "Fräsen") { }

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
            pManager.AddIntegerParameter("Side", "side", "Kerf compensation side relative to direction of travel.\n+1 = Left  (tool offset to the LEFT -- inward for CCW curves)\n 0 = Center (no offset)\n-1 = Right (tool offset to the RIGHT -- outward for CCW curves)\nConnect a ValueList for a dropdown.", GH_ParamAccess.item, 0);
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
            _previewVolumes.Clear();
            _approachLines.Clear();
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string toolType   = "WZF";
            double feedFactor = 1.0;

            _previewVolumes.Clear();
            _approachLines.Clear();

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

                if (offsets == null || offsets.Length == 0)
                {
                    // OFFSET FAILURE — null/empty result (tight geometry or diameter too large)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Kerf offset failed for curve — tool diameter (" +
                        toolDiameter.ToString(CultureInfo.InvariantCulture) +
                        " mm) may be too large for this geometry. Using center path.");
                }
                else
                {
                    cuttingCurve = offsets.Length == 1 ? offsets[0]
                        : Curve.JoinCurves(offsets, tol)[0];

                    // POST-OFFSET SANITY CHECK — detect self-intersecting / collapsed offset
                    // via area ratio: if offset area < 10% or > 10x original, geometry is suspect.
                    if (curve.IsClosed && cuttingCurve != null && cuttingCurve.IsClosed)
                    {
                        var areaOrig   = AreaMassProperties.Compute(curve);
                        var areaOffset = AreaMassProperties.Compute(cuttingCurve);
                        if (areaOrig != null && areaOffset != null && areaOrig.Area > 0.0)
                        {
                            double ratio = areaOffset.Area / areaOrig.Area;
                            if (ratio < 0.10 || ratio > 10.0)
                            {
                                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                                    "Kerf offset area is unexpected (ratio " +
                                    ratio.ToString("F2", CultureInfo.InvariantCulture) +
                                    ") — offset curve may be self-intersecting. " +
                                    "Check tool diameter (" +
                                    toolDiameter.ToString(CultureInfo.InvariantCulture) +
                                    " mm) against curve geometry.");
                            }
                        }
                    }
                }
            }

            // ---------------------------------------------------------------
            // 6. PREVIEW VOLUME
            // Translate cuttingCurve to surface Z, offset by tool radius to get
            // inner+outer walls, create planar ring, extrude downward by depth.
            // Fallback for open curves: extruded surface along the path.
            // ---------------------------------------------------------------
            double surfaceZ = curve.GetBoundingBox(true).Max.Z;

            Curve previewCrv = cuttingCurve.DuplicateCurve();
            double crvZ = previewCrv.PointAtStart.Z;
            if (Math.Abs(crvZ - surfaceZ) > 0.01)
                previewCrv.Translate(new Vector3d(0, 0, surfaceZ - crvZ));

            // Build preview: slot volume (parallel offsets + arc end caps + extrusion)
            Brep slotBrep = PreviewHelper.BuildSlotPreview(previewCrv, radius, Math.Abs(depth), tol);
            if (slotBrep != null)
                _previewVolumes.Add(slotBrep);
            else
            {
                Surface wall = Surface.CreateExtrusion(previewCrv, new Vector3d(0, 0, -Math.Abs(depth)));
                if (wall != null) _previewVolumes.Add(wall.ToBrep());
            }

            // Dashed approach line above start point
            BoundingBox curveBB = curve.GetBoundingBox(true);
            double safeZ   = curveBB.Max.Z + 20.0;
            Point3d startP = previewCrv.PointAtStart;
            _approachLines.Add(new Line(new Point3d(startP.X, startP.Y, safeZ), startP));

            // ---------------------------------------------------------------
            // 7. CURVE DECOMPOSITION
            // Convert to a list of PolyCurves (one per connected segment).
            // JoinCurves first to ensure connected pieces stay together.
            // Each joined piece = one SP...EP block.
            // ---------------------------------------------------------------
            Curve[] exploded = cuttingCurve.DuplicateSegments();
            Curve[] joined = (exploded != null && exploded.Length > 1)
                ? Curve.JoinCurves(exploded, tol)
                : new Curve[] { cuttingCurve };
            if (joined == null || joined.Length == 0)
                joined = new Curve[] { cuttingCurve };

            var allPieces = new List<List<Curve>>();
            foreach (Curve jc in joined)
            {
                if (jc == null) continue;
                Curve converted = jc.ToArcsAndLines(tolerance, 0.1, 0.0, 0.0);
                if (converted == null) continue;

                var pieceSegs = new List<Curve>();
                PolyCurve pc2 = converted as PolyCurve;
                if (pc2 != null)
                {
                    Curve[] flat = pc2.Explode();
                    if (flat != null && flat.Length > 0)
                        pieceSegs.AddRange(flat);
                    else
                        for (int si = 0; si < pc2.SegmentCount; si++)
                        {
                            Curve s = pc2.SegmentCurve(si);
                            if (s != null) pieceSegs.Add(s);
                        }
                }
                else
                {
                    pieceSegs.Add(converted); // single ArcCurve or LineCurve
                }
                if (pieceSegs.Count > 0) allPieces.Add(pieceSegs);
            }

            if (allPieces.Count == 0)
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

            int totalSegments = 0;
            foreach (List<Curve> pieceSegs in allPieces)
            {
                totalSegments += pieceSegs.Count;
                if (stepdown > 0)
                {
                    int passCount = (int)Math.Ceiling(depth / stepdown);
                    for (int p = 0; p < passCount; p++)
                    {
                        double passDepth = Math.Min((p + 1) * stepdown, depth);
                        BuildContourBlock(lines, pieceSegs, surfaceZ - passDepth, tol);
                    }
                }
                else
                {
                    BuildContourBlock(lines, pieceSegs, surfaceZ - Math.Abs(plungeZ), tol);
                }
            }

            // ---------------------------------------------------------------
            // 9. REMARK + OUTPUT
            // ---------------------------------------------------------------
            string sideLabel = side == 0 ? "center" : (side > 0 ? "left" : "right");
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Contour: " + allPieces.Count + " pieces, " + totalSegments + " segments"
                + "  depth=" + (-Math.Abs(depth)).ToString(CultureInfo.InvariantCulture)
                + "  side=" + sideLabel
                + "  toolD=" + toolDiameter.ToString(CultureInfo.InvariantCulture));

            DA.SetDataList(0, lines);
        }

        // ---------------------------------------------------------------
        // HELPER: Build SP...moves...EP block(s) for one PolyCurve.
        // Uses Explode() to flatten nested sub-curves, then groups segments
        // by connectivity -- each connected run becomes one SP/EP block.
        // ---------------------------------------------------------------
        private void BuildContourBlock(List<string> lines, List<Curve> flat,
            double zEintauch, double tol)
        {
            if (flat == null || flat.Count == 0) return;

            int gStart = 0;
            while (gStart < flat.Count)
            {
                int gEnd = gStart;
                while (gEnd + 1 < flat.Count &&
                       flat[gEnd].PointAtEnd.DistanceTo(
                           flat[gEnd + 1].PointAtStart) <= tol * 10)
                    gEnd++;

                Point3d startPt = flat[gStart].PointAtStart;
                lines.Add("SP (" + Fmt(startPt.X) + ","
                    + Fmt(startPt.Y) + ","
                    + Fmt(zEintauch)
                    + ",2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)");

                for (int i = gStart; i <= gEnd; i++)
                {
                    Curve seg = flat[i];
                    ArcCurve arcSeg = seg as ArcCurve;
                    if (arcSeg != null)
                    {
                        Arc arc    = arcSeg.Arc;
                        Point3d ep = arc.EndPoint;
                        Point3d cp = arc.Center;
                        bool isCCW = arc.Plane.Normal.Z >= 0;
                        string cmd = isCCW ? "G03M" : "G02M";
                        lines.Add(cmd + " ("
                            + Fmt(ep.X) + ","
                            + Fmt(ep.Y) + ",0,"
                            + Fmt(cp.X) + ","
                            + Fmt(cp.Y) + ",0,0,2,0)");
                        continue;
                    }
                    Point3d fep = seg.PointAtEnd;
                    lines.Add("G01 ("
                        + Fmt(fep.X) + ","
                        + Fmt(fep.Y) + ",0,0,0,2)");
                }

                lines.Add("EP (0,_ANF,0)");
                gStart = gEnd + 1;
            }
        }

        // Rounds to 4 decimal places (0.0001 mm) — prevents floating-point
        // noise like 3.55e-15 from appearing as scientific notation in .hop files.
        private static string Fmt(double v) =>
            Math.Round(v, 4).ToString(CultureInfo.InvariantCulture);

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
                DynesticPostProcessor.AutoWire.Spec.Curve(),
                DynesticPostProcessor.AutoWire.Spec.Float("1<10<100"),
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<100"),
                DynesticPostProcessor.AutoWire.Spec.Float("0.001<0.1<1"),
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<8<50"),
                DynesticPostProcessor.AutoWire.Spec.ValueList(
                    ("Left",   "1"),
                    ("Center", "0"),
                    ("Right",  "-1")),
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<50"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
