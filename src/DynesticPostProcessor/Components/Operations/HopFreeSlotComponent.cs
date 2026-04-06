using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Operations
{
    public class HopFreeSlotComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.Orange;
        private Brep  _previewVolume = null;
        private Line  _approachLine  = Line.Unset;
        private Color _drawColor     = Color.Orange;

        public HopFreeSlotComponent() : base(
            "HopFreeSlot", "HopFreeSlot",
            "Generates free slot operations (nuten_frei_v5 macro) for the DYNESTIC CNC. Cuts an elongated slot between two points at the specified width and depth.",
            "DYNESTIC", "Operations") { }

        public override Guid ComponentGuid => new Guid("6f5e6bd3-18f9-44e5-b90b-33be8ce95bcf");

        protected override Bitmap Icon => IconHelper.Load("HopFreeSlot");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("P1", "p1", "Slot start point. Z coordinate contributes to surface height (max of P1.Z and P2.Z).", GH_ParamAccess.item);
            pManager.AddPointParameter("P2", "p2", "Slot end point. Z coordinate contributes to surface height (max of P1.Z and P2.Z).", GH_ParamAccess.item);
            pManager.AddNumberParameter("SlotWidth", "slotWidth", "Slot width in mm. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Depth", "depth", "Slot depth in mm, measured downward from the higher endpoint Z. Default 1.0.", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("ToolNr", "toolNr", "Tool magazine position number. Must be greater than 0.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colour", "colour", "Preview colour for the slot volume in the Rhino viewport.", GH_ParamAccess.item, Color.Orange);

            // Mark optional parameters
            pManager[3].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines", "List of NC-Hops nuten_frei_v5 macro strings. Wire into HopExport or HopPart.", GH_ParamAccess.list);
        }

        public override void ClearData()
        {
            base.ClearData();
            _previewVolume = null;
            _approachLine = Line.Unset;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Hardcoded tool params -- handled at machine level
            string toolType   = "WZF";
            double feedFactor = 1.0;

            // PREVIEW: clear fields first (before guards) so disconnecting inputs wipes stale geometry
            _previewVolume = null;
            _approachLine  = Line.Unset;

            // ---------------------------------------------------------------
            // GET INPUTS
            // ---------------------------------------------------------------
            Point3d p1 = Point3d.Unset;
            Point3d p2 = Point3d.Unset;
            double slotWidth = 0.0;
            double depth = 1.0;
            int toolNr = 0;
            Color colour = Color.Empty;

            if (!DA.GetData(0, ref p1)) return;
            if (!DA.GetData(1, ref p2)) return;
            if (!DA.GetData(2, ref slotWidth)) return;
            DA.GetData(3, ref depth);
            if (!DA.GetData(4, ref toolNr)) return;
            DA.GetData(5, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // 1. DEFAULTS
            // ---------------------------------------------------------------
            // (empty list output set if guards trigger)

            // ---------------------------------------------------------------
            // 2. GUARDS
            // ---------------------------------------------------------------
            if (toolNr <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "toolNr is required and must be > 0");
                DA.SetDataList(0, new List<string>());
                return;
            }

            if (slotWidth <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "slotWidth must be > 0");
                DA.SetDataList(0, new List<string>());
                return;
            }

            // ---------------------------------------------------------------
            // 3. INPUT DEFAULTS
            // ---------------------------------------------------------------
            if (depth <= 0) depth = 1.0;

            // PREVIEW: box along slot centerline at surface level, extruded downward by depth
            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
            double topZ = Math.Max(p1.Z, p2.Z);
            Point3d a = new Point3d(p1.X, p1.Y, topZ);
            Point3d b = new Point3d(p2.X, p2.Y, topZ);

            Vector3d dir = b - a;
            if (dir.Length > 0.001)
            {
                dir.Unitize();
                // Perpendicular in XY
                Vector3d perp = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
                perp.Unitize();
                double halfW = slotWidth / 2.0;

                // Four corner points of slot rectangle at topZ
                Point3d c0 = a + perp * halfW;
                Point3d c1 = b + perp * halfW;
                Point3d c2 = b - perp * halfW;
                Point3d c3 = a - perp * halfW;

                // Build closed polyline as slot base
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

            // PREVIEW: approach line from safeZ to p1
            double safeZVal = Math.Max(p1.Z, p2.Z) + 20.0;
            _approachLine = new Line(new Point3d(a.X, a.Y, safeZVal), a);

            // ---------------------------------------------------------------
            // 4. BUILD TOOL CALL + MACRO
            // ---------------------------------------------------------------
            List<string> lines = new List<string>();
            lines.Add(toolType + " (" + toolNr.ToString()
                + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
                + ",_VA,_SD,0,'')");

            // surfaceZ: highest Z of the two input points (already computed as topZ above)
            double cutZ = topZ - Math.Abs(depth);

            lines.Add("CALL _nuten_frei_v5(VAL "
                + "X1:=" + p1.X.ToString(CultureInfo.InvariantCulture) + ","
                + "Y1:=" + p1.Y.ToString(CultureInfo.InvariantCulture) + ","
                + "X2:=" + p2.X.ToString(CultureInfo.InvariantCulture) + ","
                + "Y2:=" + p2.Y.ToString(CultureInfo.InvariantCulture) + ","
                + "NB:=" + slotWidth.ToString(CultureInfo.InvariantCulture) + ","
                + "Tiefe:=" + cutZ.ToString(CultureInfo.InvariantCulture) + ","
                + "LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)");

            // ---------------------------------------------------------------
            // 5. OUTPUT
            // ---------------------------------------------------------------
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "FreeSlot: (" + p1.X.ToString(CultureInfo.InvariantCulture)
                + "," + p1.Y.ToString(CultureInfo.InvariantCulture)
                + ") to (" + p2.X.ToString(CultureInfo.InvariantCulture)
                + "," + p2.Y.ToString(CultureInfo.InvariantCulture)
                + ") W=" + slotWidth.ToString(CultureInfo.InvariantCulture));

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
                mat.Transparency = 0.55;
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
                DynesticPostProcessor.AutoWire.Spec.Float("1<8<50"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<10<100"),
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
