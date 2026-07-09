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
            "Wallaby Hop", "2 | Mill") { }

        public override Guid ComponentGuid => new Guid("e2902790-ccf6-4880-b284-80e0110f1e71");

        protected override Bitmap Icon => IconHelper.Load("HopContour");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "curve", "Planar closed or open curves — each curve becomes one contour cutting path (one SP…EP block). Must lie in or near the World XY plane.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Depth", "depth", "Cutting depth in mm, measured downward from the curve's Z position. Default 1.0.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("LeadIn", "leadIn", "Lead-in length in mm. Tool approaches from outside the contour by this distance before cutting. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Tolerance", "tolerance", "NURBS to polyline/arc conversion tolerance in mm. Smaller = more segments, higher accuracy. Default 0.1.", GH_ParamAccess.item, 0.1);
            pManager.AddIntegerParameter("ToolNr", "toolNr", "Tool magazine position number. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddNumberParameter("ToolDiameter", "toolDiameter", "Tool diameter in mm, used for kerf compensation offset when side != 0. Default 8.0.", GH_ParamAccess.item, 8.0);
            pManager.AddIntegerParameter("Side", "side", "Kerf compensation side relative to direction of travel.\n+1 = Left  (tool offset to the LEFT -- inward for CCW curves)\n 0 = Center (no offset)\n-1 = Right (tool offset to the RIGHT -- outward for CCW curves)\nConnect a ValueList for a dropdown.", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Passes", "passes", "Number of passes for multi-pass cutting. 1 = single pass at full depth. Default 1.", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Overcut", "overcut", "Extra depth in mm added as a final pass after all stepdown passes (e.g. 0.2 to ensure full cut-through). Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("LeadOut", "leadOut", "Lead-out length in mm. Extends the path past the end point for a clean exit. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddBooleanParameter("AutoFlip", "autoFlip", "When true, automatically reverse curve direction so the kerf offset goes to the correct (outer) side. Only applies to closed curves with side != 0. Default false.", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("CornerStyle", "cornerStyle", "Offset corner style for kerf compensation.\n0 = Sharp (default)\n1 = Round\n2 = Smooth\nConnect a ValueList for dropdown.", GH_ParamAccess.item, 0);
            pManager.AddColourParameter("Colour", "colour", "Preview colour for the toolpath volume in the Rhino viewport.", GH_ParamAccess.item, Color.Yellow);

            // Mark optional parameters
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
            pManager[9].Optional = true;
            pManager[10].Optional = true;
            pManager[11].Optional = true;
            pManager[12].Optional = true;
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
            var curves = new List<Curve>();
            double depth = 1.0;
            double leadIn = 0.0;
            double overcut = 0.0;
            double leadOut = 0.0;
            bool autoFlip = false;
            int cornerStyle = 0;
            double tolerance = 0.1;
            int toolNr = 0;
            double toolDiameter = 8.0;
            int side = 0;
            int passes = 1;
            Color colour = Color.Empty;

            if (!DA.GetDataList(0, curves)) return;
            DA.GetData(1, ref depth);
            DA.GetData(2, ref leadIn);
            DA.GetData(8, ref overcut);
            DA.GetData(9, ref leadOut);
            DA.GetData(10, ref autoFlip);
            DA.GetData(11, ref cornerStyle);
            DA.GetData(3, ref tolerance);
            if (!DA.GetData(4, ref toolNr)) return;
            DA.GetData(5, ref toolDiameter);
            DA.GetData(6, ref side);
            DA.GetData(7, ref passes);
            DA.GetData(12, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // 1. DEFAULTS
            // ---------------------------------------------------------------
            // (empty list output set if guards trigger)

            // ---------------------------------------------------------------
            // 2. GUARDS
            // ---------------------------------------------------------------
            curves.RemoveAll(c => c == null);
            if (curves.Count == 0)
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
            if (depth <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Depth must be > 0 (got " + depth.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ") -- refusing to guess a machining depth");
                DA.SetDataList(0, new List<string>());
                return;
            }
            if (toolDiameter <= 0) toolDiameter = 8.0;
            if (passes       <  1) passes       = 1;
            if (leadIn        < 0) leadIn        = 0.0;
            if (overcut       < 0) overcut       = 0.0;
            if (leadOut       < 0) leadOut       = 0.0;
            // side: clamp to -1 / 0 / +1
            if (side > 0)  side =  1;
            if (side < 0)  side = -1;

            // ---------------------------------------------------------------
            // 4-9. ONE TOOL CALL + ONE SP…EP BLOCK PER CURVE (list input).
            // A fatal problem on ANY curve blocks the whole output — machining
            // a partial set would silently skip material.
            // ---------------------------------------------------------------
            var allLines = new List<string>();
            int totalPieces = 0, totalSegmentsAll = 0;
            foreach (Curve inputCurve in curves)
            {
                var opLines = ProcessOneCurve(inputCurve, depth, leadIn, leadOut, overcut,
                    autoFlip, cornerStyle, tolerance, toolNr, toolDiameter, side, passes,
                    toolType, feedFactor, ref totalPieces, ref totalSegmentsAll);
                if (opLines == null)
                {
                    DA.SetDataList(0, new List<string>());
                    return;
                }
                // Logic emits [tool call, blocks…] — keep the tool call once
                allLines.AddRange(allLines.Count == 0 ? (IEnumerable<string>)opLines : opLines.GetRange(1, opLines.Count - 1));
            }

            // ---------------------------------------------------------------
            // REMARK + OUTPUT
            // ---------------------------------------------------------------
            string sideLabel = side == 0 ? "center" : (side > 0 ? "left" : "right");
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Contour: " + curves.Count + " curve" + (curves.Count == 1 ? "" : "s")
                + ", " + totalPieces + " pieces, " + totalSegmentsAll + " segments"
                + "  depth=" + (-Math.Abs(depth)).ToString(CultureInfo.InvariantCulture)
                + "  side=" + sideLabel
                + "  toolD=" + toolDiameter.ToString(CultureInfo.InvariantCulture));

            DA.SetDataList(0, allLines);
        }

        /// <summary>
        /// Full per-curve pipeline: planarity check, direction check/AutoFlip,
        /// side-guaranteed kerf offset, preview volume, arc/line decomposition,
        /// pure ContourLogic generation. Returns the NC lines (tool call first)
        /// or NULL on a fatal problem (caller blocks the whole output).
        /// </summary>
        private List<string> ProcessOneCurve(Curve curve, double depth, double leadIn,
            double leadOut, double overcut, bool autoFlip, int cornerStyle, double tolerance,
            int toolNr, double toolDiameter, int side, int passes,
            string toolType, double feedFactor, ref int pieceCount, ref int segmentCount)
        {
            // ---------------------------------------------------------------
            // 4. PLANARITY CHECK
            // ---------------------------------------------------------------
            if (!curve.IsPlanar())
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Curve is not planar -- cannot use for 2D contour");
                return null;
            }

            // ---------------------------------------------------------------
            // 5. GEOMETRIC PRE-OFFSET (when side != 0)
            // ---------------------------------------------------------------
            double tol    = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            double radius = toolDiameter / 2.0;

            Curve cuttingCurve = curve;

            // Curve direction check / AutoFlip for closed curves with side offset
            if (side != 0 && curve.IsClosed)
            {
                var orient = curve.ClosedCurveOrientation(Plane.WorldXY);
                bool isCW  = orient == CurveOrientation.Clockwise;
                // Left of CCW travel (and right of CW travel) points INTO a
                // closed curve — that's a hole cut, usually not what an
                // outline cut wants, so warn / AutoFlip.
                bool wrongSide = (side == 1 && !isCW) || (side == -1 && isCW);
                if (wrongSide)
                {
                    if (autoFlip)
                    {
                        curve = curve.DuplicateCurve();
                        curve.Reverse();
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                            "AutoFlip: curve direction reversed so offset goes to correct side.");
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "Curve direction (" + (isCW ? "CW" : "CCW") + ") with side=" + side +
                            " produces an INWARD offset. Enable AutoFlip or reverse the curve.");
                    }
                }
            }

            if (side != 0)
            {
                CurveOffsetCornerStyle cs = cornerStyle == 1 ? CurveOffsetCornerStyle.Round
                                          : cornerStyle == 2 ? CurveOffsetCornerStyle.Smooth
                                          : CurveOffsetCornerStyle.Sharp;
                Curve[] offsets = OffsetOnSide(curve, side, radius, tol, cs);

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
                    if (offsets.Length == 1)
                    {
                        cuttingCurve = offsets[0];
                    }
                    else
                    {
                        // Multiple offset pieces: join them; if the join yields
                        // several disjoint curves the offset has split (concave
                        // shape + large tool) — cutting only one piece would
                        // silently skip material, so refuse loudly instead.
                        Curve[] joinedOffsets = Curve.JoinCurves(offsets, tol);
                        if (joinedOffsets == null || joinedOffsets.Length == 0)
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                                "Kerf offset produced pieces that could not be joined — "
                                + "reduce tool diameter or use side=0 (center path)");
                            return null;
                        }
                        if (joinedOffsets.Length > 1)
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                                "Kerf offset split the contour into " + joinedOffsets.Length
                                + " disjoint pieces (tool diameter " + toolDiameter.ToString(CultureInfo.InvariantCulture)
                                + " mm too large for a concave region). Machining only one piece "
                                + "would silently skip material — reduce the tool diameter or use side=0.");
                            return null;
                        }
                        cuttingCurve = joinedOffsets[0];
                    }

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
            double safeZ   = curveBB.Max.Z + MachineConstants.PreviewSafeZOffset;
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
                return null;
            }

            // ---------------------------------------------------------------
            // 8. BUILD NC OUTPUT — convert Rhino segments to ContourSegments,
            //    then delegate to pure ContourLogic
            // ---------------------------------------------------------------
            var pureP = new List<IReadOnlyList<ContourLogic.ContourSegment>>();
            foreach (List<Curve> pieceSegs in allPieces)
            {
                segmentCount += pieceSegs.Count;
                pureP.Add(ToContourSegments(pieceSegs));
            }
            pieceCount += allPieces.Count;

            return ContourLogic.Generate(new ContourLogic.ContourInput
            {
                Pieces = pureP,
                SurfaceZ = surfaceZ,
                Depth = depth,
                Passes = passes,
                Overcut = overcut,
                LeadIn = leadIn,
                LeadOut = leadOut,
                ToolNr = toolNr,
                Tolerance = tol,
                ToolType = toolType,
                FeedFactor = feedFactor,
            });
        }

        // ---------------------------------------------------------------
        // HELPER: Convert Rhino Curve segments to pure ContourSegment list.
        // Arc detection mirrors the legacy CCW determination
        // (cross product of center→start × center→mid).
        // ---------------------------------------------------------------
        /// <summary>
        /// Offset with a GUARANTEED side. Rhino's Curve.Offset sign convention
        /// is not dependable across curve orientations, so this offsets once
        /// with the expected sign (+dist = right of travel, plugin +1 = left =
        /// negated), MEASURES which side the result actually landed on, and
        /// re-offsets negated if it landed wrong. Flipped kerf = scrapped panel
        /// (H5) — measuring beats trusting the convention.
        /// </summary>
        private static Curve[] OffsetOnSide(Curve curve, int side, double radius,
            double tol, CurveOffsetCornerStyle cs)
        {
            double dist = -side * radius;
            Curve[] offsets = curve.Offset(Plane.WorldXY, dist, tol, cs);
            if (offsets == null || offsets.Length == 0 || offsets[0] == null)
                return offsets;

            double tMid = curve.Domain.ParameterAt(0.5);
            Point3d p = curve.PointAt(tMid);
            Vector3d tan = curve.TangentAt(tMid);
            double tOff;
            if (!offsets[0].ClosestPoint(p, out tOff)) return offsets;
            Vector3d toOff = offsets[0].PointAt(tOff) - p;
            if (toOff.Length < tol) return offsets;

            double cross = tan.X * toOff.Y - tan.Y * toOff.X;   // > 0 = LEFT of travel
            bool landedLeft = cross > 0;
            bool wantLeft = side == 1;
            if (landedLeft != wantLeft)
                return curve.Offset(Plane.WorldXY, -dist, tol, cs);
            return offsets;
        }

        private static IReadOnlyList<ContourLogic.ContourSegment> ToContourSegments(List<Curve> flat)
        {
            var result = new List<ContourLogic.ContourSegment>();
            if (flat == null) return result;

            foreach (Curve seg in flat)
            {
                if (seg == null) continue;
                ArcCurve arcSeg = seg as ArcCurve;
                if (arcSeg != null)
                {
                    // Direction MUST come from the curve, not from arcSeg.Arc:
                    // the Arc struct ignores curve reversal (ToArcsAndLines /
                    // Offset can emit reversed ArcCurves), which flipped every
                    // second arc of a plain circle to G02M — the machine would
                    // retrace the first half instead of cutting the second.
                    Point3d sp = arcSeg.PointAtStart;
                    Point3d ep = arcSeg.PointAtEnd;
                    Point3d cp = arcSeg.Arc.Center;
                    Vector3d ts = arcSeg.TangentAtStart; ts.Unitize();
                    Vector3d te = arcSeg.TangentAtEnd; te.Unitize();
                    Vector3d toCenter = cp - sp;
                    bool isCCW = (ts.X * toCenter.Y - ts.Y * toCenter.X) > 0;
                    result.Add(ContourLogic.ContourSegment.Arc(
                        sp.X, sp.Y, ep.X, ep.Y, cp.X, cp.Y, isCCW,
                        ts.X, ts.Y, te.X, te.Y));
                }
                else
                {
                    Point3d sp = seg.PointAtStart;
                    Point3d ep = seg.PointAtEnd;
                    result.Add(ContourLogic.ContourSegment.Line(sp.X, sp.Y, ep.X, ep.Y));
                }
            }
            return result;
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
                WallabyHop.AutoWire.Spec.Curve(),
                WallabyHop.AutoWire.Spec.Float("1<10<100"),
                WallabyHop.AutoWire.Spec.Float("0<0<100"),
                WallabyHop.AutoWire.Spec.Float("0.001<0.1<1"),
                WallabyHop.AutoWire.Spec.Int("1<1<20"),
                WallabyHop.AutoWire.Spec.Float("1<8<50"),
                WallabyHop.AutoWire.Spec.ValueList(
                    ("Left",   "1"),
                    ("Center", "0"),
                    ("Right",  "-1")),
                WallabyHop.AutoWire.Spec.Float("0<0<50"),
                WallabyHop.AutoWire.Spec.Skip(),
            });
        }
    }
}
