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
            "Generates saw cut operations (WZS + nuten_frei_v5) for the DYNESTIC CNC.\n\n" +
            "DirLine: a line on the plate defining the XY travel path of the saw.\n" +
            "BladeAngle: the physical tilt of the saw blade (0 = vertical straight cut, 45 = 45 deg miter through material thickness).\n" +
            "These are two independent parameters: direction is where the saw goes, blade angle is how the blade is tilted.\n\n" +
            "Side: kerf placement Left / Center / Right of the direction line.\n" +
            "Extend: runs the blade past both endpoints so the kerf fully exits the panel edge.",
            "DYNESTIC", "Operations") { }

        public override Guid ComponentGuid => new Guid("c8d2f1a3-4b7e-4c9d-a1f5-2e3b6d8c0f14");

        protected override Bitmap Icon => IconHelper.Load("HopSaw");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 -- travel path
            pManager.AddCurveParameter("DirLine", "dirLine",
                "A line on the plate defining the XY travel path of the saw. " +
                "The midpoint is used as the cut origin. " +
                "Draw this line in Rhino at the position and direction where the cut should run.",
                GH_ParamAccess.item);

            // 1 -- blade tilt angle
            pManager.AddNumberParameter("BladeAngle", "bladeAngle",
                "Physical tilt angle of the saw blade in degrees.\n" +
                "  0  = blade vertical (straight cut, no miter)\n" +
                " 45  = blade tilted 45 deg (Gehrungsschnitt through the material thickness)\n" +
                " 22.5 = 22.5 deg miter\n" +
                "Range: -90 to +90. Default: 0.",
                GH_ParamAccess.item, 0.0);
            pManager[1].Optional = true;

            // 2 -- cut length
            pManager.AddNumberParameter("Length", "length",
                "Total length of the saw cut in mm, centered on the midpoint of dirLine. " +
                "Should be at least as wide as the panel. Default: 600.",
                GH_ParamAccess.item, 600.0);
            pManager[2].Optional = true;

            // 3 -- kerf
            pManager.AddNumberParameter("SawKerf", "sawKerf",
                "Saw blade kerf (cut width) in mm. Typical: 3.2 mm. Must be > 0.",
                GH_ParamAccess.item, 3.2);
            pManager[3].Optional = true;

            // 4 -- depth
            pManager.AddNumberParameter("Depth", "depth",
                "Cut depth in mm downward from the plate surface. Set to material thickness for a full through-cut. Default: 19.",
                GH_ParamAccess.item, 19.0);
            pManager[4].Optional = true;

            // 5 -- side (ValueList)
            pManager.AddIntegerParameter("Side", "side",
                "Kerf placement relative to the direction line.\n" +
                "-1 = Left  (kerf sits left of dirLine)\n" +
                " 0 = Center (kerf centered on dirLine)\n" +
                "+1 = Right (kerf sits right of dirLine)\n" +
                "Connect a ValueList for a dropdown.",
                GH_ParamAccess.item, 0);
            pManager[5].Optional = true;

            // 6 -- extend
            pManager.AddNumberParameter("Extend", "extend",
                "Extend the cut past both endpoints by this distance in mm. " +
                "For miter cuts the blade must exit the panel edge at both corners. " +
                "Typical: 10-30 mm. Default: 0.",
                GH_ParamAccess.item, 0.0);
            pManager[6].Optional = true;

            // 7 -- toolNr
            pManager.AddIntegerParameter("ToolNr", "toolNr",
                "Saw blade tool magazine position number. Must be > 0.",
                GH_ParamAccess.item);

            // 8 -- colour
            pManager.AddColourParameter("Colour", "colour",
                "Preview colour for the saw kerf volume in the Rhino viewport.",
                GH_ParamAccess.item, Color.DeepSkyBlue);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops WZS + nuten_frei_v5 macro strings. Wire into HopExport or HopPart.",
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
            Curve  dirCurve   = null;
            double bladeAngle = 0.0;
            double length     = 600.0;
            double sawKerf    = 3.2;
            double depth      = 19.0;
            int    side       = 0;
            double extend     = 0.0;
            int    toolNr     = 0;
            Color  colour     = Color.Empty;

            if (!DA.GetData(0, ref dirCurve)) return;
            DA.GetData(1, ref bladeAngle);
            DA.GetData(2, ref length);
            DA.GetData(3, ref sawKerf);
            DA.GetData(4, ref depth);
            DA.GetData(5, ref side);
            DA.GetData(6, ref extend);
            if (!DA.GetData(7, ref toolNr)) return;
            DA.GetData(8, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // GUARDS
            // ---------------------------------------------------------------
            if (dirCurve == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No direction line connected");
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
            // INPUT DEFAULTS + CLAMP
            // ---------------------------------------------------------------
            if (length <= 0) length = 600.0;
            if (depth  <= 0) depth  = 19.0;
            if (extend <  0) extend =  0.0;
            if (side   >  1) side   =  1;
            if (side   < -1) side   = -1;
            bladeAngle = Math.Max(-90.0, Math.Min(90.0, bladeAngle));

            // ---------------------------------------------------------------
            // EXTRACT TRAVEL DIRECTION from the direction line
            // ---------------------------------------------------------------
            Point3d lineStart = dirCurve.PointAtStart;
            Point3d lineEnd   = dirCurve.PointAtEnd;

            Vector3d travelDir = lineEnd - lineStart;
            if (travelDir.Length < 0.001)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Direction line has zero length");
                DA.SetDataList(0, new List<string>());
                return;
            }
            travelDir.Unitize();

            // Origin = midpoint of the direction line
            Point3d origin = new Point3d(
                (lineStart.X + lineEnd.X) / 2.0,
                (lineStart.Y + lineEnd.Y) / 2.0,
                (lineStart.Z + lineEnd.Z) / 2.0);

            // Perpendicular to travel direction in XY (for side offset)
            Vector3d travelPerp = Vector3d.CrossProduct(travelDir, Vector3d.ZAxis);
            travelPerp.Unitize();

            // ---------------------------------------------------------------
            // SIDE OFFSET
            // ---------------------------------------------------------------
            double sideShift = side * (sawKerf / 2.0);
            Vector3d sideVec = travelPerp * sideShift;

            // Cut endpoints centered on origin
            double halfLen = length / 2.0;
            Point3d p1 = origin - travelDir * halfLen + sideVec;
            Point3d p2 = origin + travelDir * halfLen + sideVec;

            // Extend both ends
            Point3d cutP1 = extend > 0.001 ? p1 - travelDir * extend : p1;
            Point3d cutP2 = extend > 0.001 ? p2 + travelDir * extend : p2;

            // ---------------------------------------------------------------
            // PREVIEW VOLUME
            // When bladeAngle > 0 the blade is tilted, so the kerf footprint
            // at the SURFACE is wider than sawKerf:
            //   surfaceKerf = sawKerf + depth * tan(bladeAngle)
            // We visualise this as a parallelogram extruded downward, showing
            // how the tilted blade cuts through the material.
            // ---------------------------------------------------------------
            double tol    = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            double topZ   = origin.Z;
            double botZ   = topZ - Math.Abs(depth);
            double tiltRad = Math.Abs(bladeAngle) * Math.PI / 180.0;
            double tiltOffset = Math.Abs(depth) * Math.Tan(tiltRad); // horizontal offset of bottom edge vs top

            // Top face corners (at surface)
            double halfTop = sawKerf / 2.0;
            Point3d aTop = new Point3d(cutP1.X, cutP1.Y, topZ);
            Point3d bTop = new Point3d(cutP2.X, cutP2.Y, topZ);
            Point3d t0 = aTop + travelPerp * halfTop;
            Point3d t1 = bTop + travelPerp * halfTop;
            Point3d t2 = bTop - travelPerp * halfTop;
            Point3d t3 = aTop - travelPerp * halfTop;

            // Bottom face corners (offset by tilt, perpendicular to travel dir)
            // Positive bladeAngle tilts toward +perp direction
            double tiltSign = bladeAngle >= 0 ? 1.0 : -1.0;
            Point3d b0 = new Point3d(t0.X + travelPerp.X * tiltOffset * tiltSign, t0.Y + travelPerp.Y * tiltOffset * tiltSign, botZ);
            Point3d b1 = new Point3d(t1.X + travelPerp.X * tiltOffset * tiltSign, t1.Y + travelPerp.Y * tiltOffset * tiltSign, botZ);
            Point3d b2 = new Point3d(t2.X + travelPerp.X * tiltOffset * tiltSign, t2.Y + travelPerp.Y * tiltOffset * tiltSign, botZ);
            Point3d b3 = new Point3d(t3.X + travelPerp.X * tiltOffset * tiltSign, t3.Y + travelPerp.Y * tiltOffset * tiltSign, botZ);

            // Build brep from 6 faces (top, bottom, 4 sides)
            Brep box = BuildTiltedBox(t0, t1, t2, t3, b0, b1, b2, b3, tol);
            if (box != null) _previewVolume = box;

            _approachLine = new Line(new Point3d(aTop.X, aTop.Y, topZ + 20.0), aTop);

            // ---------------------------------------------------------------
            // NC OUTPUT
            // bladeAngle is passed to LAGE parameter of _nuten_frei_v5.
            // LAGE controls the blade orientation/tilt in the HOLZHER CAMPUS controller.
            // ---------------------------------------------------------------
            double cutZ = topZ - Math.Abs(depth);

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
                + "LAGE:=" + bladeAngle.ToString(CultureInfo.InvariantCulture)
                + ",RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)");

            // ---------------------------------------------------------------
            // REMARK
            // ---------------------------------------------------------------
            string sideLabel = side == 0 ? "Center" : (side < 0 ? "Left" : "Right");
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Saw: bladeAngle=" + bladeAngle.ToString("F1", CultureInfo.InvariantCulture) + "deg"
                + "  side=" + sideLabel
                + "  length=" + length.ToString("F1", CultureInfo.InvariantCulture)
                + "  kerf=" + sawKerf.ToString(CultureInfo.InvariantCulture)
                + "  depth=" + depth.ToString(CultureInfo.InvariantCulture)
                + (extend > 0 ? "  extend=" + extend.ToString(CultureInfo.InvariantCulture) : "")
                + "  P1=(" + cutP1.X.ToString("F1", CultureInfo.InvariantCulture)
                + "," + cutP1.Y.ToString("F1", CultureInfo.InvariantCulture) + ")"
                + "  P2=(" + cutP2.X.ToString("F1", CultureInfo.InvariantCulture)
                + "," + cutP2.Y.ToString("F1", CultureInfo.InvariantCulture) + ")");

            DA.SetDataList(0, lines);
        }

        // ---------------------------------------------------------------
        // BUILD TILTED BOX BREP from 8 corners
        // ---------------------------------------------------------------
        private Brep BuildTiltedBox(
            Point3d t0, Point3d t1, Point3d t2, Point3d t3,
            Point3d b0, Point3d b1, Point3d b2, Point3d b3,
            double tol)
        {
            try
            {
                // 6 faces as planar surfaces
                var faces = new List<Brep>();

                // Top
                faces.Add(Brep.CreateFromCornerPoints(t0, t1, t2, t3, tol));
                // Bottom
                faces.Add(Brep.CreateFromCornerPoints(b0, b3, b2, b1, tol));
                // Front (t0-t1-b1-b0)
                faces.Add(Brep.CreateFromCornerPoints(t0, t1, b1, b0, tol));
                // Back (t3-t2-b2-b3)
                faces.Add(Brep.CreateFromCornerPoints(t3, b3, b2, t2, tol));
                // Left (t0-t3-b3-b0)
                faces.Add(Brep.CreateFromCornerPoints(t0, b0, b3, t3, tol));
                // Right (t1-t2-b2-b1)
                faces.Add(Brep.CreateFromCornerPoints(t1, t2, b2, b1, tol));

                var valid = new List<Brep>();
                foreach (var f in faces)
                    if (f != null) valid.Add(f);

                if (valid.Count == 0) return null;

                Brep[] joined = Brep.JoinBreps(valid, tol);
                return (joined != null && joined.Length > 0) ? joined[0] : valid[0];
            }
            catch
            {
                return null;
            }
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
                DynesticPostProcessor.AutoWire.Spec.Float("-90<0<90"),
                DynesticPostProcessor.AutoWire.Spec.Float("10<600<3000"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<3.2<20"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<19<100"),
                DynesticPostProcessor.AutoWire.Spec.ValueList(
                    ("Left",   "-1"),
                    ("Center", "0"),
                    ("Right",  "1")),
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<50"),
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
