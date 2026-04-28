using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

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
            "Wallaby Hop", "Sawing") { }

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

            DA.GetData(0, ref direction);
            if (!DA.GetDataList(1, positions)) return;
            DA.GetData(2, ref thickness);
            DA.GetData(3, ref kw);
            DA.GetData(4, ref lengthOverride);
            if (!DA.GetData(5, ref toolNr)) return;
            DA.GetData(6, ref colour);

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
            if (thickness <= 0) thickness = 19.0;
            bool isXCut = (direction == 0);

            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            double defaultLength = lengthOverride > 0 ? lengthOverride : 1200.0;

            // ---------------------------------------------------------------
            // BUILD OUTPUT
            // ---------------------------------------------------------------
            List<string> lines = new List<string>();
            lines.Add("WZS (" + toolNr + ",_VE,_V*0.3,_VA,_SD,0,'')");

            for (int i = 0; i < positions.Count; i++)
            {
                Point3d pt = positions[i];
                double surfaceZ = pt.Z;
                double cutZ     = surfaceZ - Math.Abs(thickness);

                // Preview: a thin box representing the kerf
                double halfLen = defaultLength / 2.0;
                Point3d p1, p2;
                if (isXCut)
                {
                    p1 = new Point3d(pt.X - halfLen, pt.Y, surfaceZ);
                    p2 = new Point3d(pt.X + halfLen, pt.Y, surfaceZ);
                }
                else
                {
                    p1 = new Point3d(pt.X, pt.Y - halfLen, surfaceZ);
                    p2 = new Point3d(pt.X, pt.Y + halfLen, surfaceZ);
                }
                _approachLines.Add(new Line(new Point3d(p1.X, p1.Y, surfaceZ + 20.0), p1));

                // Build preview box (thin kerf)
                double kerfW = 3.2;
                Vector3d perp = isXCut ? Vector3d.YAxis : Vector3d.XAxis;
                var corners = new List<Point3d>
                {
                    p1 + perp * (kerfW / 2),
                    p2 + perp * (kerfW / 2),
                    p2 - perp * (kerfW / 2),
                    p1 - perp * (kerfW / 2),
                };
                var bottomCorners = new List<Point3d>();
                foreach (var c in corners)
                    bottomCorners.Add(new Point3d(c.X, c.Y, cutZ));

                Brep box = Brep.CreateFromCornerPoints(corners[0], corners[1], corners[2], corners[3], tol);
                if (box != null) _previewVolumes.Add(box);

                // NC macro
                string macro;
                if (isXCut)
                {
                    // _saege_x_V7: saw in X direction, fixed Y position (SY)
                    macro = "CALL _saege_x_V7(VAL "
                        + "SX:=0,"
                        + "SY:=" + NcFmt.F(pt.Y) + ","
                        + "SZ:=" + NcFmt.F(cutZ) + ","
                        + (lengthOverride > 0 ? "EX:=" + NcFmt.F(lengthOverride) + "," : "EX:=0,")
                        + "EZ:=" + NcFmt.F(-0.2) + ","
                        + "BL:=2,"
                        + "EINPASSEN:=0,EL:=0,AL:=0,PARALLEL:=0,"
                        + "K:=2,"
                        + "KW:=" + NcFmt.F(kw) + ","
                        + "BH:=0,RITZVERSATZ:=0.05,ESZ:=0,ESXY1:=1,ESX:=3)";
                }
                else
                {
                    // _saege_y_V7: saw in Y direction, fixed X position (SX)
                    macro = "CALL _saege_y_V7(VAL "
                        + "SX:=" + NcFmt.F(pt.X) + ","
                        + "SY:=0,"
                        + "SZ:=" + NcFmt.F(cutZ) + ","
                        + (lengthOverride > 0 ? "EY:=" + NcFmt.F(lengthOverride) + "," : "EY:=0,")
                        + "EZ:=" + NcFmt.F(-0.2) + ","
                        + "BL:=2,"
                        + "EINPASSEN:=0,EL:=0,AL:=0,PARALLEL:=0,"
                        + "K:=2,"
                        + "KW:=" + NcFmt.F(kw) + ","
                        + "BH:=0,RITZVERSATZ:=0.05,ESZ:=0,ESXY1:=1,ESX:=3)";
                }
                lines.Add(macro);

                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    "[" + i + "] " + (isXCut ? "X-cut at Y=" : "Y-cut at X=")
                    + NcFmt.F(isXCut ? pt.Y : pt.X)
                    + "  SZ=" + NcFmt.F(cutZ)
                    + "  KW=" + kw.ToString("F2", CultureInfo.InvariantCulture));
            }

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
            });
        }
    }
}
