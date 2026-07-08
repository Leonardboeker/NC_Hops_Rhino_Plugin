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
    /// Generates groove/slot milling operations (_Nuten_X_V5 / _Nuten_Y_V5) for the DYNESTIC CNC.
    /// X-groove = slot runs in X direction (e.g. for shelf grooves).
    /// Y-groove = slot runs in Y direction.
    /// Each position point defines one groove, using its Y (for X-groove) or X (for Y-groove) coordinate.
    /// </summary>
    public class HopGrooveSlotComponent : GH_Component
    {
        private static readonly Color _defaultColor = Color.Orange;
        private List<Brep> _previewVolumes = new List<Brep>();
        private List<Line> _approachLines  = new List<Line>();
        private Color      _drawColor      = Color.Orange;

        public HopGrooveSlotComponent() : base(
            "HopGrooveSlot", "HopGrooveSlot",
            "Generates groove/slot operations (_Nuten_X_V5 / _Nuten_Y_V5) for the DYNESTIC CNC.\n\n" +
            "X-groove: runs in X direction, positioned at Y. Y-groove: runs in Y, positioned at X.\n" +
            "Typical use: shelf dado grooves, back panel grooves.",
            "Wallaby Hop", "Milling") { }

        public override Guid ComponentGuid => new Guid("4b9c2d3e-5f60-7890-bcde-f01234567890");

        protected override Bitmap Icon => IconHelper.Load("HopGrooveSlot");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 - Direction
            pManager.AddIntegerParameter("Direction", "direction",
                "Groove direction.\n0 = X-groove (_Nuten_X_V5): slot runs in X, positioned at Y.\n1 = Y-groove (_Nuten_Y_V5): slot runs in Y, positioned at X.",
                GH_ParamAccess.item, 0);
            pManager[0].Optional = true;

            // 1 - Positions
            pManager.AddPointParameter("Position", "position",
                "Points on the groove center line. The macro positions the groove by its " +
                "distance from the panel edge (ARAND): for an X-groove the point's Y " +
                "coordinate becomes that distance, for a Y-groove the X coordinate. " +
                "Panel origin must be at (0,0).",
                GH_ParamAccess.list);

            // 2 - Width (NB)
            pManager.AddNumberParameter("Width", "width",
                "Groove width in mm (NB). Typically matches tool diameter or saw kerf. Default 8.",
                GH_ParamAccess.item, 8.0);
            pManager[2].Optional = true;

            // 3 - Depth (NT)
            pManager.AddNumberParameter("Depth", "depth",
                "Groove depth in mm (NT), measured downward from surface. Default 8.",
                GH_ParamAccess.item, 8.0);
            pManager[3].Optional = true;

            // 4 - Ebene (EBENE flag)
            pManager.AddIntegerParameter("Ebene", "ebene",
                "Reference plane flag (EBENE). Machine files only ever contain 0 or 1 here. " +
                "0 = default. Check a reference .hop from your machine if unsure.",
                GH_ParamAccess.item, 0);
            pManager[4].Optional = true;

            // 5 - ShortenStart (ALINKS)
            pManager.AddNumberParameter("ShortenStart", "shortenStart",
                "Shorten the groove at its start by this distance in mm (ALINKS). Default 0 = full length.",
                GH_ParamAccess.item, 0.0);
            pManager[5].Optional = true;

            // 6 - ShortenEnd (ARECHTS)
            pManager.AddNumberParameter("ShortenEnd", "shortenEnd",
                "Shorten the groove at its end by this distance in mm (ARECHTS). Default 0 = full length.",
                GH_ParamAccess.item, 0.0);
            pManager[6].Optional = true;

            // 7 - ToolNr
            pManager.AddIntegerParameter("ToolNr", "toolNr",
                "Tool magazine position number. Must be > 0.",
                GH_ParamAccess.item);

            // 8 - Colour
            pManager.AddColourParameter("Colour", "colour",
                "Preview colour in the Rhino viewport.",
                GH_ParamAccess.item, Color.Orange);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops WZF + _Nuten_X/Y_V5 macro strings. Wire into HopExport or HopPart.",
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
            _previewVolumes.Clear();
            _approachLines.Clear();

            int direction = 0;
            var positions = new List<Point3d>();
            double width = 8.0;
            double depth = 8.0;
            int ebene = 0;
            double shortenStart = 0.0;
            double shortenEnd = 0.0;
            int toolNr = 0;
            Color colour = Color.Empty;

            DA.GetData(0, ref direction);
            if (!DA.GetDataList(1, positions)) return;
            DA.GetData(2, ref width);
            DA.GetData(3, ref depth);
            DA.GetData(4, ref ebene);
            DA.GetData(5, ref shortenStart);
            DA.GetData(6, ref shortenEnd);
            if (!DA.GetData(7, ref toolNr)) return;
            DA.GetData(8, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            if (positions.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No positions connected");
                DA.SetDataList(0, new List<string>());
                return;
            }
            if (toolNr <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "toolNr must be > 0");
                DA.SetDataList(0, new List<string>());
                return;
            }

            if (width <= 0) width = 8.0;
            if (depth <= 0) depth = 8.0;

            bool isXGroove = (direction == 0);
            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            double previewLen = 600.0;

            // PREVIEW boxes (Rhino-side; pure logic computes NC strings only).
            // The groove sits at ARAND = pt.Y (X-groove) / pt.X (Y-groove) from
            // the panel edge — preview the groove band at exactly that offset,
            // starting at the panel origin, shortened by ALINKS/ARECHTS.
            var purePositions = new List<SlotLogic.GroovePosition>();
            for (int i = 0; i < positions.Count; i++)
            {
                Point3d pt = positions[i];
                double surfaceZ = pt.Z;
                double cutZ = surfaceZ - Math.Abs(depth);
                double arand = isXGroove ? pt.Y : pt.X;

                Point3d p1, p2;
                if (isXGroove)
                {
                    p1 = new Point3d(shortenStart, arand - width / 2.0, surfaceZ);
                    p2 = new Point3d(previewLen - shortenEnd, arand + width / 2.0, cutZ);
                }
                else
                {
                    p1 = new Point3d(arand - width / 2.0, shortenStart, surfaceZ);
                    p2 = new Point3d(arand + width / 2.0, previewLen - shortenEnd, cutZ);
                }
                BoundingBox bb = new BoundingBox(p1, p2);
                Brep box = Brep.CreateFromBox(bb);
                if (box != null) _previewVolumes.Add(box);
                _approachLines.Add(new Line(new Point3d(pt.X, pt.Y, surfaceZ + MachineConstants.PreviewSafeZOffset), new Point3d(pt.X, pt.Y, surfaceZ)));

                purePositions.Add(new SlotLogic.GroovePosition
                {
                    X = pt.X, Y = pt.Y, SurfaceZ = surfaceZ,
                });
            }

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                positions.Count + " " + (isXGroove ? "X-groove(s)" : "Y-groove(s)")
                + "  NB=" + NcFmt.F(width)
                + "  NT=" + NcFmt.F(depth)
                + "  EBENE=" + ebene
                + "  (preview length " + NcFmt.F(previewLen) + " is illustrative — actual groove runs the full panel)");

            var lines = SlotLogic.GenerateGroove(new SlotLogic.GrooveInput
            {
                IsXGroove = isXGroove,
                Positions = purePositions,
                Width = width,
                Depth = depth,
                Ebene = ebene,
                ShortenStart = shortenStart,
                ShortenEnd = shortenEnd,
                ToolNr = toolNr,
            });

            DA.SetDataList(0, lines);
        }

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
                WallabyHop.AutoWire.Spec.ValueList(
                    ("X-groove", "0"),
                    ("Y-groove", "1")),
                WallabyHop.AutoWire.Spec.Point(),
                WallabyHop.AutoWire.Spec.Float("1<8<50"),      // Width (NB)
                WallabyHop.AutoWire.Spec.Float("1<8<50"),      // Depth (NT)
                WallabyHop.AutoWire.Spec.ValueList(
                    ("Ebene 0", "0"),
                    ("Ebene 1", "1")),                          // Ebene flag
                WallabyHop.AutoWire.Spec.Float("0<0<100"),     // ShortenStart
                WallabyHop.AutoWire.Spec.Float("0<0<100"),     // ShortenEnd
                WallabyHop.AutoWire.Spec.Int("1<1<20"),        // ToolNr
                WallabyHop.AutoWire.Spec.Skip(),               // Colour
            });
        }
    }
}
