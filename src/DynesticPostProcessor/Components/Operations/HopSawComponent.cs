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
            "Cuts a straight kerf between two points at any XY angle. " +
            "For miter cuts: set P1/P2 diagonally. " +
            "Use 'extend' to run the blade past both endpoints so the kerf fully exits the material edge.",
            "DYNESTIC", "Operations") { }

        public override Guid ComponentGuid => new Guid("c8d2f1a3-4b7e-4c9d-a1f5-2e3b6d8c0f14");

        protected override Bitmap Icon => IconHelper.Load("HopSaw");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("P1", "p1",
                "Saw cut start point. For a miter cut, place this at one end of the desired cut line. " +
                "Z coordinate contributes to surface height (max of P1.Z and P2.Z).",
                GH_ParamAccess.item);

            pManager.AddPointParameter("P2", "p2",
                "Saw cut end point. For a miter cut, place this at the other end of the diagonal cut line. " +
                "Z coordinate contributes to surface height (max of P1.Z and P2.Z).",
                GH_ParamAccess.item);

            pManager.AddNumberParameter("SawKerf", "sawKerf",
                "Saw blade kerf (cut width) in mm. Typical circular saw blade: 3.2 mm. Must be > 0.",
                GH_ParamAccess.item, 3.2);
            pManager[2].Optional = true;

            pManager.AddNumberParameter("Depth", "depth",
                "Cut depth in mm, measured downward from the plate surface (max Z of P1/P2). " +
                "Set to material thickness for a full through-cut. Default: 19.0.",
                GH_ParamAccess.item, 19.0);
            pManager[3].Optional = true;

            pManager.AddNumberParameter("Extend", "extend",
                "Extend the cut line beyond P1 and P2 by this distance in mm. " +
                "Use for miter cuts: the blade needs to travel past the panel edge so the kerf fully exits the material at both corners. " +
                "Typical value: 10-30 mm. Default: 0.",
                GH_ParamAccess.item, 0.0);
            pManager[4].Optional = true;

            pManager.AddIntegerParameter("ToolNr", "toolNr",
                "Saw blade tool magazine position number. Must be > 0.",
                GH_ParamAccess.item);

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
            // Saw tool -- WZS, slower feed than milling (0.3 as seen in HOLZHER sample file)
            string toolType   = "WZS";
            double feedFactor = 0.3;

            _previewVolume = null;
            _approachLine  = Line.Unset;

            // ---------------------------------------------------------------
            // GET INPUTS
            // ---------------------------------------------------------------
            Point3d p1 = Point3d.Unset;
            Point3d p2 = Point3d.Unset;
            double sawKerf = 3.2;
            double depth   = 19.0;
            double extend  = 0.0;
            int    toolNr  = 0;
            Color  colour  = Color.Empty;

            if (!DA.GetData(0, ref p1)) return;
            if (!DA.GetData(1, ref p2)) return;
            DA.GetData(2, ref sawKerf);
            DA.GetData(3, ref depth);
            DA.GetData(4, ref extend);
            if (!DA.GetData(5, ref toolNr)) return;
            DA.GetData(6, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // GUARDS
            // ---------------------------------------------------------------
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

            // ---------------------------------------------------------------
            // EXTEND -- push p1 back and p2 forward along cut direction
            // ---------------------------------------------------------------
            Point3d cutP1 = p1;
            Point3d cutP2 = p2;

            if (extend > 0.001)
            {
                Vector3d dir = p2 - p1;
                if (dir.Length > 0.001)
                {
                    dir.Unitize();
                    cutP1 = p1 - dir * extend;
                    cutP2 = p2 + dir * extend;
                }
            }

            // ---------------------------------------------------------------
            // PREVIEW
            // ---------------------------------------------------------------
            double tol  = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            double topZ = Math.Max(p1.Z, p2.Z);

            Point3d a = new Point3d(cutP1.X, cutP1.Y, topZ);
            Point3d b = new Point3d(cutP2.X, cutP2.Y, topZ);

            Vector3d cutDir = b - a;
            if (cutDir.Length > 0.001)
            {
                cutDir.Unitize();
                Vector3d perp = Vector3d.CrossProduct(cutDir, Vector3d.ZAxis);
                perp.Unitize();
                double halfW = sawKerf / 2.0;

                Point3d c0 = a + perp * halfW;
                Point3d c1 = b + perp * halfW;
                Point3d c2 = b - perp * halfW;
                Point3d c3 = a - perp * halfW;

                Polyline slotPoly = new Polyline(new Point3d[] { c0, c1, c2, c3, c0 });
                Curve slotCurve = slotPoly.ToNurbsCurve();

                if (slotCurve != null && slotCurve.IsClosed)
                {
                    Vector3d extDir = new Vector3d(0, 0, -Math.Abs(depth));
                    Surface extSrf = Surface.CreateExtrusion(slotCurve, extDir);
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
            }

            double safeZ = topZ + 20.0;
            _approachLine = new Line(new Point3d(a.X, a.Y, safeZ), a);

            // ---------------------------------------------------------------
            // BUILD NC OUTPUT
            // ---------------------------------------------------------------
            // Surface Z and cut-through Z
            double cutZ = topZ - Math.Abs(depth);

            // Miter angle label for remark (angle of cut in XY, 0 = parallel to X axis)
            double dx = cutP2.X - cutP1.X;
            double dy = cutP2.Y - cutP1.Y;
            double angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            List<string> lines = new List<string>();

            // Tool call -- WZS with saw feed factor
            lines.Add(toolType + " (" + toolNr.ToString()
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            // Saw cut macro -- same as nuten_frei_v5 but driven by WZS
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
            string miterNote = (Math.Abs(angleDeg % 90) > 1.0)
                ? " miter=" + Math.Round(angleDeg, 1).ToString(CultureInfo.InvariantCulture) + "deg"
                : "";
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Saw: ("
                + p1.X.ToString("F1", CultureInfo.InvariantCulture) + ","
                + p1.Y.ToString("F1", CultureInfo.InvariantCulture)
                + ") to ("
                + p2.X.ToString("F1", CultureInfo.InvariantCulture) + ","
                + p2.Y.ToString("F1", CultureInfo.InvariantCulture)
                + ")  kerf=" + sawKerf.ToString(CultureInfo.InvariantCulture)
                + "  depth=" + depth.ToString(CultureInfo.InvariantCulture)
                + (extend > 0 ? "  extend=" + extend.ToString(CultureInfo.InvariantCulture) : "")
                + miterNote);

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
                DynesticPostProcessor.AutoWire.Spec.Point(),
                DynesticPostProcessor.AutoWire.Spec.Point(),
                DynesticPostProcessor.AutoWire.Spec.Float("1<3.2<20"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<19<100"),
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<50"),
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
