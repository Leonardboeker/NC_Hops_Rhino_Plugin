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
    /// Generates format sawing operations (_saege_x_V7 / _saege_y_V7) for the DYNESTIC CNC.
    /// X-cut = saw travels in X direction, positioned at a fixed Y.
    /// Y-cut = saw travels in Y direction, positioned at a fixed X.
    /// Supports optional miter angle (KW) for miter/bevel cuts.
    /// </summary>
    public class HopFormatCutComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.DodgerBlue;
        private List<Brep> _previewVolumes = new List<Brep>();
        private List<Line> _approachLines  = new List<Line>();
        private Color      _drawColor      = Color.DodgerBlue;

        public HopFormatCutComponent() : base(
            "HopFormatCut", "HopFormatCut",
            "Generates format sawing operations (_saege_x_V7 / _saege_y_V7) for the DYNESTIC CNC.\n\n" +
            "Direction X: saw travels in X, positioned at Y coordinate.\n" +
            "Direction Y: saw travels in Y, positioned at X coordinate.\n" +
            "KW (wedge angle) sets the bevel/miter angle. 0 = straight cut.",
            "Wallaby Hop", "3 | Saw") { }

        public override Guid ComponentGuid => new Guid("3a8b1c2d-4e5f-6789-abcd-ef0123456789");

        protected override Bitmap Icon => IconHelper.Load("HopFormatCut");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 - Direction
            pManager.AddIntegerParameter("Direction", "direction",
                "Cut direction.\n0 = X-cut (_saege_x_V7): saw travels in X, positioned at fixed Y.\n1 = Y-cut (_saege_y_V7): saw travels in Y, positioned at fixed X.",
                GH_ParamAccess.item, 0);
            pManager[0].Optional = true;

            // 1 - Position (point — X used for Y-cut, Y used for X-cut)
            pManager.AddPointParameter("Position", "position",
                "A point on the cut line. For X-cut: Y coordinate is the saw position. For Y-cut: X coordinate is the saw position. Z defines the surface height.",
                GH_ParamAccess.list);

            // 2 - Thickness (material thickness / cut depth)
            pManager.AddNumberParameter("Thickness", "thickness",
                "Material thickness = saw cut depth in mm. The saw cuts from surface Z downward. Default 19.",
                GH_ParamAccess.item, 19.0);
            pManager[2].Optional = true;

            // 3 - KW (wedge / bevel angle)
            pManager.AddNumberParameter("KW", "kw",
                "Bevel/miter angle in degrees (wedge angle). 0 = straight cut. Positive = left bevel, negative = right bevel. Default 0.",
                GH_ParamAccess.item, 0.0);
            pManager[3].Optional = true;

            // 4 - Length override
            pManager.AddNumberParameter("Length", "length",
                "Saw travel length override in mm. 0 = use plate DX/DY. Default 0.",
                GH_ParamAccess.item, 0.0);
            pManager[4].Optional = true;

            // 5 - ToolNr
            pManager.AddIntegerParameter("ToolNr", "toolNr",
                "Saw blade tool magazine position number. Must be > 0.",
                GH_ParamAccess.item);

            // 6 - Colour
            pManager.AddColourParameter("Colour", "colour",
                "Preview colour for the format cut in the Rhino viewport.",
                GH_ParamAccess.item, Color.DodgerBlue);
            pManager[6].Optional = true;

            // 7 - Side (which side of the line the kerf falls on)
            pManager.AddIntegerParameter("Side", "side",
                "Kerf side relative to the cut line.\n"
                + " 0 = centered on the line\n"
                + "+1 = kerf on the POSITIVE axis side (X-cut: +Y, Y-cut: +X); blade edge on the line\n"
                + "-1 = kerf on the NEGATIVE axis side; blade edge on the line\n"
                + "For formatting to final size put the kerf on the WASTE side -- "
                + "otherwise the part ends up half a kerf small per edge. Default 0.",
                GH_ParamAccess.item, 0);
            pManager[7].Optional = true;

            // 8 - Kerf width (for the side shift and the preview)
            pManager.AddNumberParameter("Kerf", "kerf",
                "Saw blade kerf width in mm; used for the Side shift (kerf/2) and the preview band. Default 4.",
                GH_ParamAccess.item, 4.0);
            pManager[8].Optional = true;

            // 9 - CutMode (macro parameter K)
            pManager.AddIntegerParameter("CutMode", "cutMode",
                "Saw pass mode (macro parameter K), in HOPS dialog order:\n"
                + "0 = Gleichlauf GL (single pass, climb) -- default, what the reference files use\n"
                + "1 = Gegenlauf GGL (single pass, conventional)\n"
                + "2 = Vorritzen GL (scoring pass first, then the cut)\n"
                + "3 = Vorritzen GGL\n"
                + "Only pick a Vorritzen mode for coated/veneered boards that chip. "
                + "The emitter used to force 2 regardless. Default 0.",
                GH_ParamAccess.item, 0);
            pManager[9].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops WZS + _saege_x/y_V7 macro strings. Wire into HopExport or HopPart.",
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

            // ---------------------------------------------------------------
            // GET INPUTS
            // ---------------------------------------------------------------
            int direction = 0;
            var positions = new List<Point3d>();
            double thickness = 19.0;
            double kw = 0.0;
            double lengthOverride = 0.0;
            int toolNr = 0;
            Color colour = Color.Empty;
            int side = 0;
            double kerf = 4.0;
            int cutMode = 0;

            DA.GetData(0, ref direction);
            if (!DA.GetDataList(1, positions)) return;
            DA.GetData(2, ref thickness);
            DA.GetData(3, ref kw);
            DA.GetData(4, ref lengthOverride);
            if (!DA.GetData(5, ref toolNr)) return;
            DA.GetData(6, ref colour);
            DA.GetData(7, ref side);
            DA.GetData(8, ref kerf);
            DA.GetData(9, ref cutMode);
            if (cutMode < 0 || cutMode > 3)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "CutMode must be 0..3");
                return;
            }
            if (side < -1 || side > 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Side must be -1, 0 or +1");
                return;
            }
            if (kerf <= 0) kerf = 4.0;

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // GUARDS
            // ---------------------------------------------------------------
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

            // ---------------------------------------------------------------
            // DEFAULTS
            // ---------------------------------------------------------------
            if (thickness <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Thickness must be > 0 (got " + thickness.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ") -- refusing to guess a machining depth");
                DA.SetDataList(0, new List<string>());
                return;
            }
            bool isXCut = (direction == 0);

            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            double defaultLength = lengthOverride > 0 ? lengthOverride : 1200.0;

            // ---------------------------------------------------------------
            // BUILD OUTPUT
            // ---------------------------------------------------------------
            // PREVIEW (Rhino-side) + collect pure positions
            // Side shift: with side != 0 the kerf center moves by kerf/2 so the
            // blade EDGE lands on the drawn line and the kerf falls on the chosen
            // side. This is the shift Leo applied by hand between test files 2
            // and 3 -- now it is an input instead of moved geometry.
            double sideShift = side * (kerf / 2.0);

            var purePositions = new List<FormatCutLogic.CutPosition>();
            for (int i = 0; i < positions.Count; i++)
            {
                Point3d pt = positions[i];
                double surfaceZ = pt.Z;
                Vector3d perp = isXCut ? Vector3d.YAxis : Vector3d.XAxis;
                Point3d cutPt = pt + perp * sideShift;

                double halfLen = defaultLength / 2.0;
                Point3d p1, p2;
                if (isXCut)
                {
                    p1 = new Point3d(cutPt.X - halfLen, cutPt.Y, surfaceZ);
                    p2 = new Point3d(cutPt.X + halfLen, cutPt.Y, surfaceZ);
                }
                else
                {
                    p1 = new Point3d(cutPt.X, cutPt.Y - halfLen, surfaceZ);
                    p2 = new Point3d(cutPt.X, cutPt.Y + halfLen, surfaceZ);
                }
                _approachLines.Add(new Line(new Point3d(p1.X, p1.Y, surfaceZ + MachineConstants.PreviewSafeZOffset), p1));

                // The preview must show WHERE the cut lands, not a flat patch on
                // the surface: the blade plane runs through the full thickness,
                // and with KW != 0 the bottom edge is displaced by
                // thickness * tan(KW). Sign: the first version tilted the wrong
                // way -- Leo compared the Rhino preview against the same file in
                // HOPS on 2026-08-10 and they leaned opposite ways. HOPS shows what
                // the machine does, so the preview was corrected to match it.
                double tiltRad = RhinoMath.ToRadians(-kw);
                double bottomOffset = Math.Abs(thickness) * Math.Tan(tiltRad);
                Point3d p1b = new Point3d(p1.X, p1.Y, surfaceZ - Math.Abs(thickness)) + perp * bottomOffset;
                Point3d p2b = new Point3d(p2.X, p2.Y, surfaceZ - Math.Abs(thickness)) + perp * bottomOffset;
                Brep blade = Brep.CreateFromCornerPoints(p1, p2, p2b, p1b, tol);
                if (blade != null) _previewVolumes.Add(blade);

                // kerf band on the top surface, so the side shift is visible
                var corners = new List<Point3d>
                {
                    p1 + perp * (kerf / 2),
                    p2 + perp * (kerf / 2),
                    p2 - perp * (kerf / 2),
                    p1 - perp * (kerf / 2),
                };
                Brep box = Brep.CreateFromCornerPoints(corners[0], corners[1], corners[2], corners[3], tol);
                if (box != null) _previewVolumes.Add(box);

                purePositions.Add(new FormatCutLogic.CutPosition
                {
                    X = cutPt.X, Y = cutPt.Y, SurfaceZ = surfaceZ,
                });

// per-position remark replaced by one aggregate below
            }

            var lines = FormatCutLogic.Generate(new FormatCutLogic.FormatCutInput
            {
                IsXCut = isXCut,
                Positions = purePositions,
                Thickness = thickness,
                Kw = kw,
                LengthOverride = lengthOverride,
                ToolNr = toolNr,
                CutMode = cutMode,
            });

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                positions.Count + " " + (isXCut ? "X-cut(s)" : "Y-cut(s)")
                + "  thickness=" + NcFmt.F(thickness)
                + "  KW=" + kw.ToString("F2", CultureInfo.InvariantCulture)
                + "  K=" + cutMode + (cutMode >= 2 ? " (Vorritzen!)" : "")
                + (side == 0 ? "  kerf centered"
                   : "  kerf " + (side > 0 ? "+" : "-") + " side, shift "
                     + NcFmt.F(side * (kerf / 2.0)) + " mm"));

            DA.SetDataList(0, lines);
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
                WallabyHop.AutoWire.Spec.ValueList(
                    ("X-cut (fixed Y)", "0"),
                    ("Y-cut (fixed X)", "1")),
                WallabyHop.AutoWire.Spec.Point(),
                WallabyHop.AutoWire.Spec.Float("1<19<100"),
                WallabyHop.AutoWire.Spec.Float("-90<0<90"),
                WallabyHop.AutoWire.Spec.Float("0<0<3000"),
                WallabyHop.AutoWire.Spec.Int("1<1<20"),
                WallabyHop.AutoWire.Spec.Skip(),
                WallabyHop.AutoWire.Spec.ValueList(
                    ("kerf centered", "0"),
                    ("kerf +side (blade edge on line)", "1"),
                    ("kerf -side (blade edge on line)", "-1")),
                WallabyHop.AutoWire.Spec.Float("1<4<10"),
                WallabyHop.AutoWire.Spec.ValueList(
                    ("Gleichlauf GL", "0"),
                    ("Gegenlauf GGL", "1"),
                    ("Vorritzen GL", "2"),
                    ("Vorritzen GGL", "3")),
            });
        }
    }
}
