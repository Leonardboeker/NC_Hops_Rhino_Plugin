using System;
using System.Collections.Generic;
using System.Drawing;

using Rhino.Geometry;

using Grasshopper.Kernel;

using WallabyHop.Logic;

namespace WallabyHop.Components.Export
{
    /// <summary>
    /// Viewport backplot of assembled .hop content: toolpath segments colored
    /// by tool type, drill circles at their real diameter, sequence numbers at
    /// every chain/group start, plunge markers. This is the "do I believe this
    /// file" view — wire HopExport's or HopJob's HopContent output into it.
    /// </summary>
    public class HopBackplotComponent : GH_Component
    {
        private List<BackplotLogic.BackplotOp> _ops = new List<BackplotLogic.BackplotOp>();
        private double _textSize = 12.0;

        public HopBackplotComponent() : base(
            "HopBackplot", "HopBackplot",
            "Draws the toolpath encoded in a .hop content string directly in the viewport: " +
            "segments colored by tool (WZB=red, WZF=yellow, WZS=blue), drill circles at real " +
            "diameter, sequence numbers at chain starts, plunge markers. " +
            "Wire HopContent from HopExport or HopJob.",
            "Wallaby Hop", "7 | Export") { }

        public override Guid ComponentGuid => new Guid("3b67e27b-cb16-476e-886e-4a6412db5ac9");

        protected override Bitmap Icon => IconHelper.Load("HopAnalyzer");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        // The component has no geometry parameters, so Grasshopper would
        // treat it as not preview-capable and never call the Draw methods.
        public override bool IsPreviewCapable => true;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("HopContent", "hopContent",
                "Full .hop file content string (from HopExport / HopJob / HopSheetExport).",
                GH_ParamAccess.item);

            pManager.AddBooleanParameter("Show", "show",
                "Toggle the backplot preview on/off. Default true.",
                GH_ParamAccess.item, true);
            pManager[1].Optional = true;

            pManager.AddNumberParameter("TextSize", "textSize",
                "Sequence-number text size in viewport pixels. Default 12.",
                GH_ParamAccess.item, 12.0);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("OpCount", "opCount",
                "Number of drawable operations found in the content.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("ChainCount", "chainCount",
                "Number of machining chains/groups (= sequence numbers shown).", GH_ParamAccess.item);
        }

        public override void ClearData()
        {
            base.ClearData();
            _ops.Clear();
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            _ops.Clear();

            string content = null;
            bool show = true;
            double textSize = 12.0;

            if (!DA.GetData(0, ref content)) return;
            DA.GetData(1, ref show);
            DA.GetData(2, ref textSize);
            _textSize = textSize > 1 ? textSize : 12.0;

            if (string.IsNullOrWhiteSpace(content))
            {
                Message = "no content";
                DA.SetData(0, 0);
                DA.SetData(1, 0);
                return;
            }

            var plan = BackplotLogic.Parse(content);
            if (show) _ops.AddRange(plan.Ops);

            Message = plan.Ops.Count + " ops / " + plan.ChainCount + " chains";
            DA.SetData(0, plan.Ops.Count);
            DA.SetData(1, plan.ChainCount);
        }

        // ---------------------------------------------------------------
        // PREVIEW
        // ---------------------------------------------------------------
        private static Color ToolColor(string toolType)
        {
            switch (toolType)
            {
                case "WZB": return MachineConstants.BackplotDrillColor;
                case "WZF": return MachineConstants.BackplotMillColor;
                case "WZS": return MachineConstants.BackplotSawColor;
                default:    return Color.Gray;
            }
        }

        public override BoundingBox ClippingBox
        {
            get
            {
                BoundingBox bb = BoundingBox.Empty;
                foreach (var op in _ops)
                {
                    bb.Union(new Point3d(op.StartX, op.StartY, 0));
                    bb.Union(new Point3d(op.EndX, op.EndY, 0));
                }
                // Inflate: a perfectly flat box is degenerate and gets
                // rejected by preview-bounds scanners; plunge markers reach
                // up to PreviewSafeZOffset anyway.
                if (bb.IsValid)
                {
                    bb.Union(bb.Min + new Vector3d(0, 0, -1));
                    bb.Union(bb.Max + new Vector3d(0, 0, MachineConstants.PreviewSafeZOffset));
                }
                return bb;
            }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args) { }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            foreach (var op in _ops)
            {
                Color c = ToolColor(op.ToolType);
                var a = new Point3d(op.StartX, op.StartY, 0);
                var b = new Point3d(op.EndX, op.EndY, 0);

                switch (op.Kind)
                {
                    case BackplotLogic.OpKind.Drill:
                    {
                        double r = op.Diameter > 0 ? op.Diameter / 2.0 : 4.0;
                        args.Display.DrawCircle(new Circle(new Plane(a, Vector3d.ZAxis), r), c, 2);
                        // small cross at center
                        args.Display.DrawLine(new Line(a + new Vector3d(-r, 0, 0), a + new Vector3d(r, 0, 0)), c, 1);
                        args.Display.DrawLine(new Line(a + new Vector3d(0, -r, 0), a + new Vector3d(0, r, 0)), c, 1);
                        break;
                    }
                    case BackplotLogic.OpKind.Move:
                    {
                        if (op.IsChainStart)
                        {
                            // plunge marker + sequence number at chain start
                            args.Display.DrawLine(new Line(
                                new Point3d(op.StartX, op.StartY, MachineConstants.PreviewSafeZOffset), a), c, 1);
                            args.Display.Draw2dText(op.SequenceIndex.ToString(), c, a, true, (int)_textSize);
                        }
                        else
                        {
                            args.Display.DrawLine(new Line(a, b), c, 2);
                        }
                        break;
                    }
                    case BackplotLogic.OpKind.Arc:
                    {
                        var center = new Point3d(op.CenterX, op.CenterY, 0);
                        Arc arc = ArcFrom(a, b, center, op.IsCCW);
                        if (arc.IsValid) args.Display.DrawArc(arc, c, 2);
                        else args.Display.DrawLine(new Line(a, b), c, 2);
                        break;
                    }
                    case BackplotLogic.OpKind.MacroSegment:
                    {
                        args.Display.DrawLine(new Line(a, b), c, 3);
                        break;
                    }
                    case BackplotLogic.OpKind.MacroPoint:
                    {
                        args.Display.DrawCircle(new Circle(new Plane(a, Vector3d.ZAxis), 6.0), c, 2);
                        break;
                    }
                }
            }

            // Sequence dots for drill groups / macro groups (non-SP chain starts
            // carry their group's index on the first op)
            int lastSeq = -1;
            foreach (var op in _ops)
            {
                if (op.Kind != BackplotLogic.OpKind.Move && op.SequenceIndex != lastSeq)
                {
                    lastSeq = op.SequenceIndex;
                    var p = new Point3d(op.StartX, op.StartY, 0);
                    args.Display.Draw2dText(op.SequenceIndex.ToString(), ToolColor(op.ToolType), p, true, (int)_textSize);
                }
            }
        }

        // Build a Rhino Arc from start/end/center + direction. The .hop arc
        // convention stores endpoint + center; direction from G02M/G03M.
        private static Arc ArcFrom(Point3d start, Point3d end, Point3d center, bool ccw)
        {
            double r = start.DistanceTo(center);
            if (r < 1e-9) return Arc.Unset;

            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double a1 = Math.Atan2(end.Y - center.Y, end.X - center.X);
            double sweep = a1 - a0;
            if (ccw && sweep <= 0) sweep += 2 * Math.PI;
            if (!ccw && sweep >= 0) sweep -= 2 * Math.PI;

            var plane = new Plane(center, Vector3d.ZAxis);
            // rotate plane so its X axis points at the start point
            plane.Rotate(a0, Vector3d.ZAxis, center);
            return new Arc(plane, r, sweep);
        }
    }
}
