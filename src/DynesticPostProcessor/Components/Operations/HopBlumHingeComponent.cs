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
    /// Generates Blum cup hinge drilling operations (_Topf_V5) for the DYNESTIC CNC.
    /// Creates the 35mm cup hole + optional dowel holes for Blum Clip-Top / Inserta hinges.
    /// Positions are defined by X coordinates (typically measured from front edge).
    /// </summary>
    public class HopBlumHingeComponent : GH_Component
    {
        private static readonly Color _defaultColor = Color.MediumPurple;
        private List<Brep> _previewVolumes = new List<Brep>();
        private List<Line> _approachLines  = new List<Line>();
        private Color      _drawColor      = Color.MediumPurple;

        public HopBlumHingeComponent() : base(
            "HopBlumHinge", "HopBlumHinge",
            "Generates Blum cup hinge drilling operations (_Topf_V5) for the DYNESTIC CNC.\n\n" +
            "Creates the 35mm cup hole + optional dowel holes at specified positions.\n" +
            "Distance: offset from edge to cup center (typically 22.5 mm from front).\n" +
            "Pos1..Pos4: Y positions of each hinge along the board.",
            "Wallaby Hop", "Beschläge") { }

        public override Guid ComponentGuid => new Guid("6d1e4f50-7182-9012-def0-123456789012");

        protected override Bitmap Icon => IconHelper.Load("HopBlumHinge");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 - Positions (Y coordinates of hinges)
            pManager.AddPointParameter("Positions", "positions",
                "Hinge center positions. X = cup center X offset from edge. Y = hinge Y position along board. Z = surface height.",
                GH_ParamAccess.list);

            // 1 - Distance (DISTANCE from edge to cup center)
            pManager.AddNumberParameter("Distance", "distance",
                "Distance from board edge to cup center in mm (DISTANCE). Typical Blum value: 22.5. Default 22.5.",
                GH_ParamAccess.item, 22.5);
            pManager[1].Optional = true;

            // 2 - Side (0=front, 1=back)
            pManager.AddIntegerParameter("Side", "side",
                "Which edge the hinge is measured from.\n0 = front edge (SEITE:=0)\n1 = back edge (SEITE:=1).\nDefault 0.",
                GH_ParamAccess.item, 0);
            pManager[2].Optional = true;

            // 3 - CupDiameter
            pManager.AddNumberParameter("CupDiameter", "cupDiameter",
                "Cup hole diameter in mm (TOPF_D). Standard Blum = 35. Default 35.",
                GH_ParamAccess.item, 35.0);
            pManager[3].Optional = true;

            // 4 - CupDepth
            pManager.AddNumberParameter("CupDepth", "cupDepth",
                "Cup drilling depth in mm (TOPF_T). Typical Blum = 12.8. Default 12.8.",
                GH_ParamAccess.item, 12.8);
            pManager[4].Optional = true;

            // 5 - DowelDiameter
            pManager.AddNumberParameter("DowelDiameter", "dowelDiameter",
                "Mounting dowel diameter in mm (DUEBEL_D). Default 8. Set 0 to skip dowel holes.",
                GH_ParamAccess.item, 8.0);
            pManager[5].Optional = true;

            // 6 - DowelDepth
            pManager.AddNumberParameter("DowelDepth", "dowelDepth",
                "Mounting dowel depth in mm (DUEBEL_T). Default 13. Set 0 to skip dowel holes.",
                GH_ParamAccess.item, 13.0);
            pManager[6].Optional = true;

            // 7 - ToolNr
            pManager.AddIntegerParameter("ToolNr", "toolNr",
                "Tool magazine position number. Must be > 0.",
                GH_ParamAccess.item);

            // 8 - Colour
            pManager.AddColourParameter("Colour", "colour",
                "Preview colour in the Rhino viewport.",
                GH_ParamAccess.item, Color.MediumPurple);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops WZB + _Topf_V5 macro strings. Wire into HopExport or HopPart.",
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

            var positions = new List<Point3d>();
            double distance = 22.5;
            int side = 0;
            double cupDiameter = 35.0;
            double cupDepth = 12.8;
            double dowelDiameter = 8.0;
            double dowelDepth = 13.0;
            int toolNr = 0;
            Color colour = Color.Empty;

            if (!DA.GetDataList(0, positions)) return;
            DA.GetData(1, ref distance);
            DA.GetData(2, ref side);
            DA.GetData(3, ref cupDiameter);
            DA.GetData(4, ref cupDepth);
            DA.GetData(5, ref dowelDiameter);
            DA.GetData(6, ref dowelDepth);
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

            if (distance <= 0) distance = 22.5;
            if (cupDiameter <= 0) cupDiameter = 35.0;
            if (cupDepth <= 0) cupDepth = 12.8;

            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;

            List<string> lines = new List<string>();
            lines.Add("WZB (" + toolNr + ",_VE,_V*1,_VA,_SD,0,'')");

            for (int i = 0; i < positions.Count; i++)
            {
                Point3d pt = positions[i];
                double surfaceZ = pt.Z;
                double cupCutZ = surfaceZ - Math.Abs(cupDepth);

                // Preview: cup cylinder
                double cupRadius = cupDiameter / 2.0;
                Point3d cupCenter = new Point3d(pt.X, pt.Y, surfaceZ);
                Plane cupPlane = new Plane(cupCenter, Vector3d.ZAxis);
                Cylinder cupCyl = new Cylinder(new Circle(cupPlane, cupRadius), -Math.Abs(cupDepth));
                Brep cupBrep = cupCyl.ToBrep(true, true);
                if (cupBrep != null) _previewVolumes.Add(cupBrep);
                _approachLines.Add(new Line(new Point3d(pt.X, pt.Y, surfaceZ + 20.0), cupCenter));

                // _Topf_V5 takes up to 4 Y positions in one call
                // Here we output one call per position for simplicity
                string macro = "CALL _Topf_V5(VAL "
                    + "SEITE:=" + side + ","
                    + "DISTANCE:=" + NcFmt.F(distance) + ","
                    + "POS1:=" + NcFmt.F(pt.Y) + ","
                    + "POS2:=0,POS3:=0,POS4:=0,"
                    + "A:=9.5,B:=45,"
                    + "TOPF_D:=" + NcFmt.F(cupDiameter) + ","
                    + "TOPF_T:=" + NcFmt.F(-Math.Abs(cupDepth)) + ","
                    + "DUEBEL_D:=" + NcFmt.F(dowelDiameter) + ","
                    + "DUEBEL_T:=" + NcFmt.F(-Math.Abs(dowelDepth)) + ","
                    + "ESX1:=0,ESX2:=0,ESX3:=0,ESX4:=0,"
                    + "ESY1:=0,ESY2:=0,ESY3:=0,ESY4:=0,"
                    + "USE2:=0,USE3:=0,USE4:=0)";
                lines.Add(macro);

                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    "[" + i + "] Hinge at Y=" + NcFmt.F(pt.Y)
                    + "  DISTANCE=" + NcFmt.F(distance)
                    + "  Cup Ø" + NcFmt.F(cupDiameter));
            }

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
                WallabyHop.AutoWire.Spec.Point(),
                WallabyHop.AutoWire.Spec.Float("1<22.5<100"),
                WallabyHop.AutoWire.Spec.ValueList(
                    ("Front edge", "0"),
                    ("Back edge",  "1")),
                WallabyHop.AutoWire.Spec.Float("10<35<50"),
                WallabyHop.AutoWire.Spec.Float("1<12.8<30"),
                WallabyHop.AutoWire.Spec.Float("0<8<20"),
                WallabyHop.AutoWire.Spec.Float("0<13<30"),
                WallabyHop.AutoWire.Spec.Int("1<1<20"),
                WallabyHop.AutoWire.Spec.Skip(),
            });
        }
    }
}
