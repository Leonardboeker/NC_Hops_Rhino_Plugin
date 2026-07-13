using System;
using System.Collections.Generic;
using System.Drawing;

using Rhino.Geometry;

using Grasshopper.Kernel;

using WallabyHop.Logic;

namespace WallabyHop.Components.Export
{
    /// <summary>
    /// Material-removal simulation of assembled .hop content: builds the stock
    /// box from the file's DX/DY/DZ, turns every operation into a cut solid,
    /// boolean-subtracts them and outputs the remaining stock — RhinoCAM-style.
    /// A Step slider replays the job operation by operation. When the boolean
    /// fails the component falls back to a "ghost" view (stock + cut solids
    /// drawn translucent red) instead of showing nothing.
    /// </summary>
    public class HopStockSimComponent : GH_Component
    {
        private readonly List<Brep> _ghostCutters = new List<Brep>();
        private BoundingBox _bounds = BoundingBox.Empty;

        public HopStockSimComponent() : base(
            "HopStockSim", "HopStockSim",
            "Simulates the material removal encoded in a .hop content string: stock box " +
            "(from the file's DX/DY/DZ) minus every drill, saw kerf, groove, slot, pocket " +
            "and milling path. Slide Step from 0 to StepCount to replay the job operation " +
            "by operation — the order check you otherwise only get at the machine.\n\n" +
            "Milling paths (SP/G01/G02M chains, _Kreisbahn) need the tool diameter, which " +
            "is NOT stored in the .hop — wire ToolNrs + ToolDiameters (e.g. from HopToolDB) " +
            "or the default is used. Angled saw cuts are simulated as vertical kerfs; " +
            "pocket corner radii are ignored.",
            "Wallaby Hop", "7 | Export") { }

        public override Guid ComponentGuid => new Guid("23b83e31-31b4-4b1f-9cf3-48c43cf0c5c4");

        protected override Bitmap Icon => IconHelper.Load("HopAnalyzer");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("HopContent", "hopContent",
                "Full .hop file content string (from HopExport / HopJob / HopSheetExport).",
                GH_ParamAccess.item);

            pManager.AddIntegerParameter("Step", "step",
                "Replay position: number of operations already machined. " +
                "0 = untouched stock, StepCount = finished part. " +
                "Any value < 0 or > StepCount shows the finished part. Default -1 (all).",
                GH_ParamAccess.item, -1);
            pManager[1].Optional = true;

            pManager.AddIntegerParameter("ToolNrs", "toolNrs",
                "Tool numbers for the diameter lookup (paired with ToolDiameters).",
                GH_ParamAccess.list);
            pManager[2].Optional = true;

            pManager.AddNumberParameter("ToolDiameters", "toolDiameters",
                "Cutting diameters in mm, paired 1:1 with ToolNrs. Used for milling " +
                "paths whose width is not in the .hop file. Default for unknown tools: 8 mm.",
                GH_ParamAccess.list);
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Stock", "stock",
                "Remaining stock after the first <Step> operations.", GH_ParamAccess.list);
            pManager.AddTextParameter("Steps", "steps",
                "One label per operation, in machining order — item N is what Step=N+1 adds.",
                GH_ParamAccess.list);
            pManager.AddIntegerParameter("StepCount", "stepCount",
                "Total number of simulated operations (slider maximum).", GH_ParamAccess.item);
        }

        public override void ClearData()
        {
            base.ClearData();
            _ghostCutters.Clear();
            _bounds = BoundingBox.Empty;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            _ghostCutters.Clear();
            _bounds = BoundingBox.Empty;

            string content = null;
            int step = -1;
            var toolNrs = new List<int>();
            var toolDias = new List<double>();

            if (!DA.GetData(0, ref content)) return;
            DA.GetData(1, ref step);
            DA.GetDataList(2, toolNrs);
            DA.GetDataList(3, toolDias);

            if (string.IsNullOrWhiteSpace(content))
            {
                Message = "no content";
                return;
            }

            var diaMap = new Dictionary<int, double>();
            if (toolNrs.Count != toolDias.Count && (toolNrs.Count > 0 || toolDias.Count > 0))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "ToolNrs (" + toolNrs.Count + ") and ToolDiameters (" + toolDias.Count
                    + ") differ in length — extra items ignored.");
            for (int i = 0; i < Math.Min(toolNrs.Count, toolDias.Count); i++)
                diaMap[toolNrs[i]] = toolDias[i];

            var plan = StockSimLogic.Parse(content, diaMap, 8.0);
            foreach (string w in plan.Warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);

            int total = plan.StepLabels.Count;
            int shown = (step < 0 || step > total) ? total : step;

            var stock = new Box(Plane.WorldXY,
                new Interval(0, plan.Dx),
                new Interval(0, plan.Dy),
                new Interval(0, plan.Dz)).ToBrep();
            _bounds = stock.GetBoundingBox(false);

            var cutters = new List<Brep>();
            foreach (var cut in plan.Cuts)
                if (cut.StepIndex <= shown)
                    cutters.AddRange(BuildCutters(cut));

            var result = new List<Brep> { stock };
            bool booleanOk = true;
            if (cutters.Count > 0)
            {
                Brep[] diff = Brep.CreateBooleanDifference(
                    new[] { stock }, cutters, 0.001);

                // Rhino booleans can also return WRONG geometry instead of
                // null (observed: a 3-face fragment). Sanity: every piece
                // solid, total volume plausible (>1% and <100.1% of the box).
                bool plausible = diff != null && diff.Length > 0;
                if (plausible)
                {
                    double boxVol = plan.Dx * plan.Dy * plan.Dz;
                    double vol = 0;
                    foreach (Brep b in diff)
                    {
                        if (b == null || !b.IsSolid) { plausible = false; break; }
                        var vp = VolumeMassProperties.Compute(b);
                        if (vp != null) vol += vp.Volume;
                    }
                    if (plausible && (vol < boxVol * 0.01 || vol > boxVol * 1.001))
                        plausible = false;
                }

                if (plausible)
                {
                    result = new List<Brep>(diff);
                }
                else
                {
                    booleanOk = false;
                    _ghostCutters.AddRange(cutters);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Boolean difference failed or returned implausible geometry — "
                        + "showing stock + cut solids (ghost view) instead.");
                }
            }

            Message = booleanOk
                ? "step " + shown + "/" + total
                : "GHOST " + shown + "/" + total;

            DA.SetDataList(0, result);
            DA.SetDataList(1, plan.StepLabels);
            DA.SetData(2, total);
        }

        private static List<Brep> BuildCutters(StockSimLogic.CutSolid cut)
        {
            // Inflate every cutter slightly: coincident faces (groove flush to
            // an edge, through-cuts ending exactly at Z=0) make booleans fail.
            const double eps = 0.02;
            var result = new List<Brep>();

            double z0 = Math.Min(cut.Z0, cut.Z1) - eps;
            double z1 = Math.Max(cut.Z0, cut.Z1) + eps;
            double h = z1 - z0;
            if (h < 0.01 || cut.Width <= 0) return result;

            if (cut.Kind == StockSimLogic.SolidKind.Cylinder)
            {
                var baseCircle = new Circle(
                    new Plane(new Point3d(cut.X1, cut.Y1, z0), Vector3d.ZAxis),
                    cut.Width / 2.0 + eps);
                result.Add(new Cylinder(baseCircle, h).ToBrep(true, true));
                return result;
            }

            if (cut.Kind == StockSimLogic.SolidKind.Ring)
            {
                // Full circular path: smooth annulus (outer minus inner
                // cylinder) instead of a jagged chain of slabs.
                var center = new Plane(new Point3d(cut.X1, cut.Y1, z0), Vector3d.ZAxis);
                double rOut = cut.PathRadius + cut.Width / 2.0 + eps;
                double rIn  = cut.PathRadius - cut.Width / 2.0 - eps;
                Brep outer = new Cylinder(new Circle(center, rOut), h).ToBrep(true, true);
                if (rIn <= eps) { result.Add(outer); return result; }   // full disc
                Brep inner = new Cylinder(new Circle(center, rIn), h).ToBrep(true, true);
                Brep[] ring = Brep.CreateBooleanDifference(
                    new[] { outer }, new[] { inner }, 0.001);
                result.Add(ring != null && ring.Length == 1 && ring[0].IsSolid ? ring[0] : outer);
                return result;
            }

            // Slab: oriented box along the segment. RoundEnds (router paths) is
            // approximated by extending both ends by the tool radius — the sim
            // removes square corners where the real cutter leaves them round.
            var dir = new Vector3d(cut.X2 - cut.X1, cut.Y2 - cut.Y1, 0);
            double len = dir.Length;
            if (len < 0.001) return result;
            dir /= len;

            var mid = new Point3d((cut.X1 + cut.X2) / 2.0, (cut.Y1 + cut.Y2) / 2.0, z0);
            var plane = new Plane(mid, dir, Vector3d.CrossProduct(Vector3d.ZAxis, dir));

            double halfL = len / 2.0 + eps;
            double halfW = cut.Width / 2.0 + eps;

            // Pocket with rounded corners (tool radius / macro RADIUS):
            // decomposed into PRIMITIVES — two overlapping boxes + four
            // corner cylinders. A filleted-curve extrusion produced open
            // breps that silently corrupted the boolean (3-face garbage).
            double r = Math.Min(cut.CornerRadius, Math.Min(halfL, halfW) - 0.01);
            if (r > 0.05 && !cut.RoundEnds)
            {
                result.Add(new Box(plane,
                    new Interval(-halfL, halfL),
                    new Interval(-(halfW - r), halfW - r),
                    new Interval(0, h)).ToBrep());
                result.Add(new Box(plane,
                    new Interval(-(halfL - r), halfL - r),
                    new Interval(-halfW, halfW),
                    new Interval(0, h)).ToBrep());
                foreach (double sx in new[] { -1.0, 1.0 })
                    foreach (double sy in new[] { -1.0, 1.0 })
                    {
                        Point3d c = plane.PointAt(sx * (halfL - r), sy * (halfW - r));
                        result.Add(new Cylinder(new Circle(
                            new Plane(c, Vector3d.ZAxis), r), h).ToBrep(true, true));
                    }
                result.RemoveAll(b => b == null);
                return result;
            }

            double endExt = cut.RoundEnds ? cut.Width / 2.0 : eps;
            result.Add(new Box(plane,
                new Interval(-len / 2.0 - endExt, len / 2.0 + endExt),
                new Interval(-halfW, halfW),
                new Interval(0, h)).ToBrep());
            result.RemoveAll(b => b == null);
            return result;
        }

        // ---------------------------------------------------------------
        // PREVIEW — the remaining stock previews itself via the Brep output;
        // only the ghost fallback needs custom drawing.
        // ---------------------------------------------------------------

        public override BoundingBox ClippingBox
        {
            get
            {
                var bb = base.ClippingBox;
                if (_bounds.IsValid) bb.Union(_bounds);
                return bb;
            }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            base.DrawViewportMeshes(args);
            if (_ghostCutters.Count == 0) return;
            var mat = new Rhino.Display.DisplayMaterial(Color.Red, 0.6);
            foreach (var b in _ghostCutters)
                args.Display.DrawBrepShaded(b, mat);
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            base.DrawViewportWires(args);
            foreach (var b in _ghostCutters)
                args.Display.DrawBrepWires(b, Color.Red, 1);
        }

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Skip(),            // HopContent
                WallabyHop.AutoWire.Spec.Int("0<99<99"),    // Step (99 ≈ "all" until wired lower)
                WallabyHop.AutoWire.Spec.Skip(),            // ToolNrs
                WallabyHop.AutoWire.Spec.Skip(),            // ToolDiameters
            });
        }
    }
}
