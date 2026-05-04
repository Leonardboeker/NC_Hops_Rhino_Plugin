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
    /// <summary>
    /// Generates fixing clamp/chip operations (Fixchip_K) for the DYNESTIC CNC.
    /// Fixchip positions hold the workpiece to the nesting board during cutting.
    /// Each input point defines one clamp position with optional rotation angle.
    /// </summary>
    public class HopFixchipComponent : GH_Component
    {
        private static readonly Color _defaultColor = Color.Gold;
        private List<Line> _approachLines = new List<Line>();
        private Color      _drawColor     = Color.Gold;

        public HopFixchipComponent() : base(
            "HopFixchip", "HopFixchip",
            "Generates fixing clamp positions (Fixchip_K) for the DYNESTIC CNC.\n\n" +
            "Fixchips hold the workpiece to the nesting board during cutting.\n" +
            "Each input point defines one clamp position (SPX, SPY, SPZ).",
            "Wallaby Hop", "Hardware") { }

        public override Guid ComponentGuid => new Guid("7e2f5061-8293-0123-ef01-234567890123");

        protected override Bitmap Icon => IconHelper.Load("HopFixchip");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 - Positions
            pManager.AddPointParameter("Positions", "positions",
                "Clamp center positions. XY = position on board, Z = surface height.",
                GH_ParamAccess.list);

            // 1 - Angle
            pManager.AddNumberParameter("Angle", "angle",
                "Rotation angle of the clamp in degrees (WKLXY). 0 = no rotation. Default 0.",
                GH_ParamAccess.item, 0.0);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops Fixchip_K macro strings. Wire into HopExport or HopPart.",
                GH_ParamAccess.list);
        }

        public override void ClearData()
        {
            base.ClearData();
            _approachLines.Clear();
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            _approachLines.Clear();

            var positions = new List<Point3d>();
            double angle = 0.0;

            if (!DA.GetDataList(0, positions)) return;
            DA.GetData(1, ref angle);

            if (positions.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No positions connected");
                DA.SetDataList(0, new List<string>());
                return;
            }

            // Preview lines + collect pure positions
            var purePositions = new List<DrillLogic.Point2dz>();
            for (int i = 0; i < positions.Count; i++)
            {
                Point3d pt = positions[i];
                _approachLines.Add(new Line(
                    new Point3d(pt.X, pt.Y, pt.Z + MachineConstants.PreviewSafeZOffset),
                    new Point3d(pt.X, pt.Y, pt.Z)));
                purePositions.Add(new DrillLogic.Point2dz(pt.X, pt.Y, pt.Z));
            }

            var lines = FixchipLogic.Generate(new FixchipLogic.FixchipInput
            {
                Positions = purePositions,
                Angle = angle,
            });

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                positions.Count + " fixchip position(s)  WKLXY=" + angle.ToString("F1", CultureInfo.InvariantCulture));

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
                foreach (Line l in _approachLines) bb.Union(l.BoundingBox);
                return bb;
            }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args) { }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            PreviewHelper.DrawWires(args, new List<Brep>(), _approachLines, _drawColor);
        }

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Point(),
                WallabyHop.AutoWire.Spec.Float("-360<0<360"),
            });
        }
    }
}
