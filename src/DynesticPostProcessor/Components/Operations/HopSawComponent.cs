using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Operations
{
    public class HopSawComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.DeepSkyBlue;
        private Brep  _previewVolume = null;
        private Line  _approachLine  = Line.Unset;
        private Color _drawColor     = Color.DeepSkyBlue;

        public HopSawComponent() : base(
            "HopSaw", "HopSaw",
            "Generates saw cut operations (WZS + nuten_frei_v5) for the DYNESTIC CNC. " +
            "Draw a line defining the cut direction and position. " +
            "Side controls whether the kerf sits left (-1), center (0), or right (+1) of the line. " +
            "Extend runs the blade past both endpoints so the kerf fully exits the material edge.",
            "DYNESTIC", "Operations") { }

        public override Guid ComponentGuid => new Guid("c8d2f1a3-4b7e-4c9d-a1f5-2e3b6d8c0f14");

        protected override Bitmap Icon => IconHelper.Load("HopSaw");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0
            pManager.AddCurveParameter("Line", "line",
                "A straight line (or any curve -- first and last point are used) defining the saw cut path. " +
                "Draw this line in Rhino to set the exact position and angle of the cut. " +
                "The line can be at any angle: 0 deg = horizontal, 90 deg = vertical, 45 deg = miter.",
                GH_ParamAccess.item);

            // 1
            pManager.AddNumberParameter("SawKerf", "sawKerf",
                "Saw blade kerf (cut width) in mm. Typical circular saw blade: 3.2 mm. Must be > 0.",
                GH_ParamAccess.item, 3.2);
            pManager[1].Optional = true;

            // 2
            pManager.AddNumberParameter("Depth", "depth",
                "Cut depth in mm downward from the line's Z. Set to material thickness for a full through-cut. Default: 19.",
                GH_ParamAccess.item, 19.0);
            pManager[2].Optional = true;

            // 3
            pManager.AddIntegerParameter("Side", "side",
                "Kerf placement relative to the line: " +
                "-1 = left of travel direction (kerf left of line), " +
                " 0 = centered on the line, " +
                "+1 = right of travel direction (kerf right of line). " +
                "Default: 0 (centered).",
                GH_ParamAccess.item, 0);
            pManager[3].Optional = true;

            // 4
            pManager.AddNumberParameter("Extend", "extend",
                "Extend the cut beyond both endpoints by this distance in mm. " +
                "For miter cuts the blade must travel past the panel edge so the kerf fully exits. " +
                "Typical: 10-30 mm. Default: 0.",
                GH_ParamAccess.item, 0.0);
            pManager[4].Optional = true;

            // 5
            pManager.AddIntegerParameter("ToolNr", "toolNr",
                "Saw blade tool magazine position number. Must be > 0.",
                GH_ParamAccess.item);

            // 6
            pManager.AddColourParameter("Colour", "colour",
                "Preview colour for the saw kerf volume in the Rhino viewport.",
                GH_ParamAccess.item, Color.DeepSkyBlue);
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "List of NC-Hops WZS + nuten_frei_v5 macro strings. Wire into HopExport or HopPart.",
                GH_ParamAccess.list);
        }

        public override void ClearData()
        {
            base.ClearData();
            _previewVolume = null;
            _approachLine  = Line.Unset;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string toolType   = "WZS";
            double feedFactor = 0.3;

            _previewVolume = null;
            _approachLine  = Line.Unset;

            // ---------------------------------------------------------------
            // GET INPUTS
            // ---------------------------------------------------------------
            Curve  inputCurve = null;
            double sawKerf    = 3.2;
            double depth      = 19.0;
            int    side       = 0;
            double extend     = 0.0;
            int    toolNr     = 0;
            Color  colour     = Color.Empty;

            if (!DA.GetData(0, ref inputCurve)) return;
            DA.GetData(1, ref sawKerf);
            DA.GetData(2, ref depth);
            DA.GetData(3, ref side);
            DA.GetData(4, ref extend);
            if (!DA.GetData(5, ref toolNr)) return;
            DA.GetData(6, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // GUARDS
            // ---------------------------------------------------------------
            if (inputCurve == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No line connected");
                DA.SetDataList(0, new List<string>());
                return;
            }
            if (toolNr <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "toolNr must be > 0");
                DA.SetDataList(0, new List<string>());
                return;
            }
            if (sawKerf <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "sawKerf must be > 0");
                DA.SetDataList(0, new List<string>());
                return;
            }

            // ---------------------------------------------------------------
            // INPUT DEFAULTS
            // ---------------------------------------------------------------
            if (depth  <= 0) depth  = 19.0;
            if (extend <  0) extend =  0.0;
            if (side > 1)  side =  1;
            if (side < -1) side = -1;

            // ---------------------------------------------------------------
            // EXTRACT START / END from curve
            // ---------------------------------------------------------------
            Point3d lineStart = inputCurve.PointAtStart;
            Point3d lineEnd   = inputCurve.PointAtEnd;

            Vector3d dir = lineEnd - lineStart;
            if (dir.Length < 0.001)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Line has zero length");
                DA.SetDataList(0, new List<string>());
                return;
            }
            dir.Unitize();

            // ---------------------------------------------------------------
            // SIDE OFFSET -- shift the centerline perpendicular by half kerf
            // left/right of travel direction
            // ---------------------------------------------------------------
            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;

            // Perpendicular in XY plane (right of travel = positive)
            Vector3d perp = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
            perp.Unitize();

            // side offset: 0 = no shift, -1 = shift left (kerf fully left of line), +1 = shift right
            double sideOffset = side * (sawKerf / 2.0);
            Vector3d shift = perp * sideOffset;

            Point3d p1 = lineStart + shift;
            Point3d p2 = lineEnd   + shift;

            // ---------------------------------------------------------------
            // EXTEND -- push both endpoints outward along cut direction
            // ---------------------------------------------------------------
            Point3d cutP1 = extend > 0.001 ? p1 - dir * extend : p1;
            Point3d cutP2 = extend > 0.001 ? p2 + dir * extend : p2;

            // ---------------------------------------------------------------
            // PREVIEW VOLUME -- kerf box extruded downward
            // ---------------------------------------------------------------
            double topZ  = Math.Max(lineStart.Z, lineEnd.Z);
            double halfW = sawKerf / 2.0;

            Point3d a = new Point3d(cutP1.X, cutP1.Y, topZ);
            Point3d b = new Point3d(cutP2.X, cutP2.Y, topZ);

            Point3d c0 = a + perp * halfW;
            Point3d c1 = b + perp * halfW;
            Point3d c2 = b - perp * halfW;
            Point3d c3 = a - perp * halfW;

            Polyline slotPoly  = new Polyline(new Point3d[] { c0, c1, c2, c3, c0 });
            Curve    slotCurve = slotPoly.ToNurbsCurve();

            if (slotCurve != null && slotCurve.IsClosed)
            {
                Vector3d extrudeDir = new Vector3d(0, 0, -Math.Abs(depth));
                Surface extSrf = Surface.CreateExtrusion(slotCurve, extrudeDir);
                if (extSrf != null)
                {
                    Brep extBrep = extSrf.ToBrep();
                    if (extBrep != null)
                    {
                        Brep capped = extBrep.CapPlanarHoles(tol);
                        _previewVolume = capped != null ? capped : extBrep;
                    }
                }
            }

            _approachLine = new Line(new Point3d(a.X, a.Y, topZ + 20.0), a);

            // ---------------------------------------------------------------
            // NC OUTPUT
            // ---------------------------------------------------------------
            double cutZ    = topZ - Math.Abs(depth);
            double angleDeg = Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI;

            string sideLabel = side == 0 ? "center" : (side < 0 ? "left" : "right");

            List<string> lines = new List<string>();
            lines.Add(toolType + " (" + toolNr.ToString()
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            lines.Add("CALL _nuten_frei_v5(VAL "
                + "X1:=" + cutP1.X.ToString(CultureInfo.InvariantCulture) + ","
                + "Y1:=" + cutP1.Y.ToString(CultureInfo.InvariantCulture) + ","
                + "X2:=" + cutP2.X.ToString(CultureInfo.InvariantCulture) + ","
                + "Y2:=" + cutP2.Y.ToString(CultureInfo.InvariantCulture) + ","
                + "NB:=" + sawKerf.ToString(CultureInfo.InvariantCulture) + ","
                + "Tiefe:=" + cutZ.ToString(CultureInfo.InvariantCulture) + ","
                + "LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)");

            // ---------------------------------------------------------------
            // REMARK + OUTPUT
            // ---------------------------------------------------------------
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Saw: angle=" + angleDeg.ToString("F1", CultureInfo.InvariantCulture) + "deg"
                + "  kerf=" + sawKerf.ToString(CultureInfo.InvariantCulture)
                + "  side=" + sideLabel
                + "  depth=" + depth.ToString(CultureInfo.InvariantCulture)
                + (extend > 0 ? "  extend=" + extend.ToString(CultureInfo.InvariantCulture) : "")
                + "  P1=(" + cutP1.X.ToString("F1", CultureInfo.InvariantCulture)
                + "," + cutP1.Y.ToString("F1", CultureInfo.InvariantCulture) + ")"
                + "  P2=(" + cutP2.X.ToString("F1", CultureInfo.InvariantCulture)
                + "," + cutP2.Y.ToString("F1", CultureInfo.InvariantCulture) + ")");

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
                if (_previewVolume != null) bb.Union(_previewVolume.GetBoundingBox(true));
                if (_approachLine.IsValid) { bb.Union(_approachLine.From); bb.Union(_approachLine.To); }
                return bb;
            }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (_previewVolume != null)
            {
                Rhino.Display.DisplayMaterial mat = new Rhino.Display.DisplayMaterial(_drawColor);
                mat.Transparency = 0.45;
                args.Display.DrawBrepShaded(_previewVolume, mat);
            }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_previewVolume != null)
                args.Display.DrawBrepWires(_previewVolume, _drawColor, 1);
            if (_approachLine.IsValid)
                args.Display.DrawPatternedLine(
                    _approachLine.From, _approachLine.To,
                    Color.FromArgb(140, 140, 140), unchecked((int)0xF0F0F0F0), 1);
        }

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.Curve(),
                DynesticPostProcessor.AutoWire.Spec.Float("1<3.2<20"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<19<100"),
                DynesticPostProcessor.AutoWire.Spec.Int("-1<0<1"),
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<50"),
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
