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
                {
                    Brep b = BuildCutter(cut);
                    if (b != null) cutters.Add(b);
                }

            var result = new List<Brep> { stock };
            bool booleanOk = true;
            if (cutters.Count > 0)
            {
                Brep[] diff = Brep.CreateBooleanDifference(
                    new[] { stock }, cutters, 0.001);
                if (diff != null && diff.Length > 0)
                {
                    result = new List<Brep>(diff);
                }
                else
                {
                    booleanOk = false;
                    _ghostCutters.AddRange(cutters);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Boolean difference failed — showing stock + cut solids (ghost view) instead.");
                }
            }

            Message = booleanOk
                ? "step " + shown + "/" + total
                : "GHOST " + shown + "/" + total;

            DA.SetDataList(0, result);
            DA.SetDataList(1, plan.StepLabels);
            DA.SetData(2, total);
        }

        private static Brep BuildCutter(StockSimLogic.CutSolid cut)
        {
            // Inflate every cutter slightly: coincident faces (groove flush to
            // an edge, through-cuts ending exactly at Z=0) make booleans fail.
            const double eps = 0.02;

            double z0 = Math.Min(cut.Z0, cut.Z1) - eps;
            double z1 = Math.Max(cut.Z0, cut.Z1) + eps;
            if (z1 - z0 < 0.01 || cut.Width <= 0) return null;

            if (cut.Kind == StockSimLogic.SolidKind.Cylinder)
            {
                var baseCircle = new Circle(
                    new Plane(new Point3d(cut.X1, cut.Y1, z0), Vector3d.ZAxis),
                    cut.Width / 2.0 + eps);
                return new Cylinder(baseCircle, z1 - z0).ToBrep(true, true);
            }

            // Slab: oriented box along the segment. RoundEnds (router paths) is
            // approximated by extending both ends by the tool radius — the sim
            // removes square corners where the real cutter leaves them round.
            var dir = new Vector3d(cut.X2 - cut.X1, cut.Y2 - cut.Y1, 0);
            double len = dir.Length;
            if (len < 0.001) return null;
            dir /= len;

            double endExt = cut.RoundEnds ? cut.Width / 2.0 : eps;
            var mid = new Point3d((cut.X1 + cut.X2) / 2.0, (cut.Y1 + cut.Y2) / 2.0, z0);
            var plane = new Plane(mid, dir, Vector3d.CrossProduct(Vector3d.ZAxis, dir));
            return new Box(plane,
                new Interval(-len / 2.0 - endExt, len / 2.0 + endExt),
                new Interval(-cut.Width / 2.0 - eps, cut.Width / 2.0 + eps),
                new Interval(0, z1 - z0)).ToBrep();
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
