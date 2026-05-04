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
    public class HopEngravingComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.MediumPurple;
        private List<Brep> _previewVolumes = new List<Brep>();
        private List<Line> _approachLines  = new List<Line>();
        private Color      _drawColor      = Color.MediumPurple;

        public HopEngravingComponent() : base(
            "HopEngraving", "HopEngraving",
            "Generates engraving paths (WZF + SP/G01/G03M/EP) for the DYNESTIC CNC.\n\n" +
            "Follows the input curve exactly -- no kerf offset. Designed for shallow cuts " +
            "with V-bits or engraving spindles. Multiple curves produce one continuous engraving path per curve.",
            "Wallaby Hop", "Milling") { }

        public override Guid ComponentGuid => new Guid("d3a19f7c-5b2e-4d8a-b6c1-9f0e2a4c7d83");

        protected override Bitmap Icon => IconHelper.Load("HopEngraving");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 -- curves (list)
            pManager.AddCurveParameter("Curves", "curves",
                "One or more curves to engrave. Each curve becomes one SP...EP block. " +
                "Open or closed curves accepted. Must be planar.",
                GH_ParamAccess.list);

            // 1 -- depth
            pManager.AddNumberParameter("Depth", "depth",
                "Engraving depth in mm, measured downward from the curve's Z position. " +
                "Keep shallow for engraving: 0.2-2.0 mm typical. Default 0.5.",
                GH_ParamAccess.item, 0.5);
            pManager[1].Optional = true;

            // 2 -- tolerance
            pManager.AddNumberParameter("Tolerance", "tolerance",
                "NURBS to polyline/arc conversion tolerance in mm. Default 0.05.",
                GH_ParamAccess.item, 0.05);
            pManager[2].Optional = true;

            // 3 -- toolNr
            pManager.AddIntegerParameter("ToolNr", "toolNr",
                "Tool magazine position number. Must be > 0.",
                GH_ParamAccess.item);

            // 4 -- colour
            pManager.AddColourParameter("Colour", "colour",
                "Preview colour for the engraving path in the Rhino viewport.",
                GH_ParamAccess.item, Color.MediumPurple);
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops WZF + SP/G01/G03M/EP macro strings. Wire into HopExport or HopPart.",
                GH_ParamAccess.list);
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
            List<Curve> curves    = new List<Curve>();
            double      depth     = 0.5;
            double      tolerance = 0.05;
            int         toolNr    = 0;
            Color       colour    = Color.Empty;

            if (!DA.GetDataList(0, curves)) return;
            DA.GetData(1, ref depth);
            DA.GetData(2, ref tolerance);
            if (!DA.GetData(3, ref toolNr)) return;
            DA.GetData(4, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // GUARDS
            // ---------------------------------------------------------------
            if (curves.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No curves connected");
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
            // DEFAULTS
            // ---------------------------------------------------------------
            if (depth     <= 0) depth     = 0.5;
            if (tolerance <= 0) tolerance = 0.05;

            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;

            // ---------------------------------------------------------------
            // BUILD NC OUTPUT — collect per-curve segments, delegate to EngravingLogic
            // ---------------------------------------------------------------
            var pureCurves = new List<IReadOnlyList<ContourLogic.ContourSegment>>();
            var surfaceZs = new List<double>();
            int curveCount = 0;

            for (int ci = 0; ci < curves.Count; ci++)
            {
                Curve curve = curves[ci];
                if (curve == null) continue;

                if (!curve.IsPlanar())
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Curve " + ci + " is not planar -- skipped");
                    continue;
                }

                double surfaceZ = curve.GetBoundingBox(true).Max.Z;

                // PREVIEW: pipe along engraving path (radius = depth → 45deg V-bit footprint)
                double angleTol = RhinoDoc.ActiveDoc != null
                    ? RhinoDoc.ActiveDoc.ModelAngleToleranceRadians : 0.01;
                Brep[] engPipe = Brep.CreatePipe(
                    curve, Math.Abs(depth), false, PipeCapMode.Flat, true, tol, angleTol);
                if (engPipe != null && engPipe.Length > 0)
                    _previewVolumes.Add(engPipe[0]);

                BoundingBox bb = curve.GetBoundingBox(true);
                Point3d startP = curve.PointAtStart;
                _approachLines.Add(new Line(
                    new Point3d(startP.X, startP.Y, bb.Max.Z + MachineConstants.PreviewSafeZOffset), startP));

                // Decompose: join → ToArcsAndLines → flatten to leaf segments
                Curve[] segs2 = curve.DuplicateSegments();
                Curve[] joined = (segs2 != null && segs2.Length > 1)
                    ? Curve.JoinCurves(segs2, tol)
                    : new Curve[] { curve };
                if (joined == null || joined.Length == 0)
                    joined = new Curve[] { curve };

                var allFlat = new List<Curve>();
                foreach (Curve jc in joined)
                {
                    if (jc == null) continue;
                    Curve converted = jc.ToArcsAndLines(tolerance, 0.1, 0.0, 0.0);
                    if (converted == null) continue;

                    PolyCurve pc2 = converted as PolyCurve;
                    if (pc2 != null)
                    {
                        Curve[] flat = pc2.Explode();
                        if (flat != null && flat.Length > 0)
                            allFlat.AddRange(flat);
                        else
                            for (int si = 0; si < pc2.SegmentCount; si++)
                            {
                                Curve s = pc2.SegmentCurve(si);
                                if (s != null) allFlat.Add(s);
                            }
                    }
                    else
                    {
                        allFlat.Add(converted);
                    }
                }

                if (allFlat.Count == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Curve " + ci + " produced no NC output -- skipped");
                    continue;
                }

                pureCurves.Add(ToContourSegments(allFlat));
                surfaceZs.Add(surfaceZ);
                curveCount++;
            }

            var allLines = EngravingLogic.Generate(new EngravingLogic.EngravingInput
            {
                Curves = pureCurves,
                SurfaceZPerCurve = surfaceZs,
                Depth = depth,
                ToolNr = toolNr,
                Tolerance = tol,
                ToolType = toolType,
                FeedFactor = feedFactor,
            });

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Engraving: " + curveCount + " curves, depth=" + depth.ToString(CultureInfo.InvariantCulture) + "mm");

            DA.SetDataList(0, allLines);
        }

        // ---------------------------------------------------------------
        // HELPER: Convert Rhino Curve segments to pure ContourSegment list.
        // (Same arc CCW determination as HopContourComponent.)
        // ---------------------------------------------------------------
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
                    Arc arc = arcSeg.Arc;
                    Point3d sp = arc.StartPoint;
                    Point3d ep = arc.EndPoint;
                    Point3d cp = arc.Center;
                    Point3d mid = arc.PointAt(arc.Angle * 0.5);
                    Vector3d toStart = arc.StartPoint - cp;
                    Vector3d toMid = mid - cp;
                    bool isCCW = (toStart.X * toMid.Y - toStart.Y * toMid.X) > 0;
                    result.Add(ContourLogic.ContourSegment.Arc(
                        sp.X, sp.Y, ep.X, ep.Y, cp.X, cp.Y, isCCW, 0, 0, 0, 0));
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
                WallabyHop.AutoWire.Spec.Float("0.1<0.5<5"),
                WallabyHop.AutoWire.Spec.Float("0.01<0.05<1"),
                WallabyHop.AutoWire.Spec.Int("1<1<20"),
                WallabyHop.AutoWire.Spec.Skip(),
            });
        }
    }
}
