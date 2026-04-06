using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Operations
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
            "DYNESTIC", "Operations") { }

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
            // BUILD NC OUTPUT
            // ---------------------------------------------------------------
            List<string> allLines = new List<string>();
            allLines.Add(toolType + " (" + toolNr.ToString()
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

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
                double cutZ     = surfaceZ - Math.Abs(depth);

                // ---------------------------------------------------------------
                // PREVIEW: thin extruded surface along the engraving path
                // ---------------------------------------------------------------
                Curve flat = curve.DuplicateCurve();
                flat.Translate(new Vector3d(0, 0, -flat.PointAtStart.Z));
                Surface wall = Surface.CreateExtrusion(flat, new Vector3d(0, 0, -Math.Abs(depth)));
                if (wall != null)
                {
                    Brep b = wall.ToBrep();
                    b.Translate(new Vector3d(0, 0, surfaceZ));
                    _previewVolumes.Add(b);
                }

                BoundingBox bb = curve.GetBoundingBox(true);
                Point3d startP = curve.PointAtStart;
                _approachLines.Add(new Line(
                    new Point3d(startP.X, startP.Y, bb.Max.Z + 20.0), startP));

                // ---------------------------------------------------------------
                // CURVE DECOMPOSITION
                // ---------------------------------------------------------------
                PolyCurve pc = curve.ToArcsAndLines(tolerance, 0.1, 0.0, 0.0) as PolyCurve;
                if (pc == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Curve " + ci + " decomposition failed -- skipped");
                    continue;
                }

                // ---------------------------------------------------------------
                // SP...moves...EP block (identical to HopContour, no offset)
                // ---------------------------------------------------------------
                Point3d sp = pc.PointAtStart;
                allLines.Add("SP ("
                    + sp.X.ToString(CultureInfo.InvariantCulture) + ","
                    + sp.Y.ToString(CultureInfo.InvariantCulture) + ","
                    + cutZ.ToString(CultureInfo.InvariantCulture)
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
                        allLines.Add(cmd + " ("
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
                        allLines.Add("G01 ("
                            + ep.X.ToString(CultureInfo.InvariantCulture) + ","
                            + ep.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,0,2)");
                        continue;
                    }

                    Point3d fallback = seg.PointAtEnd;
                    allLines.Add("G01 ("
                        + fallback.X.ToString(CultureInfo.InvariantCulture) + ","
                        + fallback.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,0,2)");
                }

                allLines.Add("EP (0,_ANF,0)");
                curveCount++;
            }

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Engraving: " + curveCount + " curves, depth=" + depth.ToString(CultureInfo.InvariantCulture) + "mm");

            DA.SetDataList(0, allLines);
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
                DynesticPostProcessor.AutoWire.Spec.Curve(),
                DynesticPostProcessor.AutoWire.Spec.Float("0.1<0.5<5"),
                DynesticPostProcessor.AutoWire.Spec.Float("0.01<0.05<1"),
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
