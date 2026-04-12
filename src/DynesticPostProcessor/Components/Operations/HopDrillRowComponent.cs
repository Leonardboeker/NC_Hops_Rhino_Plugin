using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Operations
{
    /// <summary>
    /// Generates parametric drill row operations (_Bohgx_V5 / _Bohgy_V5) for the DYNESTIC CNC.
    /// Defines a row of holes by start position + 4 incremental spacings.
    /// Typical use: cabinet shelf pin rows, connector bore patterns.
    /// </summary>
    public class HopDrillRowComponent : GH_Component
    {
        private static readonly Color _defaultColor = Color.Tomato;
        private List<Brep> _previewVolumes = new List<Brep>();
        private List<Line> _approachLines  = new List<Line>();
        private Color      _drawColor      = Color.Tomato;

        public HopDrillRowComponent() : base(
            "HopDrillRow", "HopDrillRow",
            "Generates parametric drill row operations (_Bohgx_V5 / _Bohgy_V5) for the DYNESTIC CNC.\n\n" +
            "X-row: holes spaced along X, fixed Y position.\n" +
            "Y-row: holes spaced along Y, fixed X position.\n" +
            "Up to 4 increment spacing values (BIX..BIIIIX / BIY..BIIIIY).",
            "DYNESTIC", "Bohren") { }

        public override Guid ComponentGuid => new Guid("5c0d3e4f-6071-8901-cdef-012345678901");

        protected override Bitmap Icon => IconHelper.Load("HopDrillRow");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 - Direction
            pManager.AddIntegerParameter("Direction", "direction",
                "Drill row direction.\n0 = X-row (_Bohgx_V5): holes in X, positioned at Y.\n1 = Y-row (_Bohgy_V5): holes in Y, positioned at X.",
                GH_ParamAccess.item, 0);
            pManager[0].Optional = true;

            // 1 - StartPoint
            pManager.AddPointParameter("StartPoint", "startPoint",
                "Start point of the first hole. Y used for X-row, X used for Y-row. Z = surface height.",
                GH_ParamAccess.item);

            // 2 - Spacings (up to 4)
            pManager.AddNumberParameter("Spacings", "spacings",
                "Incremental spacings between holes (BIX/BIY..BIIIIX/BIIIIY) in mm. Supply 1-4 values.\n" +
                "Each spacing is the gap from the previous hole to the next. Unused spacings = 0 (disabled).",
                GH_ParamAccess.list);

            // 3 - Depth
            pManager.AddNumberParameter("Depth", "depth",
                "Drill depth in mm. Default 13.",
                GH_ParamAccess.item, 13.0);
            pManager[3].Optional = true;

            // 4 - Diameter
            pManager.AddNumberParameter("Diameter", "diameter",
                "Hole diameter in mm. Default 5.",
                GH_ParamAccess.item, 5.0);
            pManager[4].Optional = true;

            // 5 - Mirror
            pManager.AddBooleanParameter("Mirror", "mirror",
                "Mirror the drill row (SPIEGELN). Default false.",
                GH_ParamAccess.item, false);
            pManager[5].Optional = true;

            // 6 - ToolNr
            pManager.AddIntegerParameter("ToolNr", "toolNr",
                "Tool magazine position number. Must be > 0.",
                GH_ParamAccess.item);

            // 7 - Colour
            pManager.AddColourParameter("Colour", "colour",
                "Preview colour in the Rhino viewport.",
                GH_ParamAccess.item, Color.Tomato);
            pManager[7].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops WZB + _Bohgx/y_V5 macro strings. Wire into HopExport or HopPart.",
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
            Point3d startPoint = Point3d.Origin;
            var spacings = new List<double>();
            double depth = 13.0;
            double diameter = 5.0;
            bool mirror = false;
            int toolNr = 0;
            Color colour = Color.Empty;

            DA.GetData(0, ref direction);
            if (!DA.GetData(1, ref startPoint)) return;
            if (!DA.GetDataList(2, spacings)) return;
            DA.GetData(3, ref depth);
            DA.GetData(4, ref diameter);
            DA.GetData(5, ref mirror);
            if (!DA.GetData(6, ref toolNr)) return;
            DA.GetData(7, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            if (spacings.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one spacing value required");
                DA.SetDataList(0, new List<string>());
                return;
            }
            if (toolNr <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "toolNr must be > 0");
                DA.SetDataList(0, new List<string>());
                return;
            }

            if (depth <= 0) depth = 13.0;
            if (diameter <= 0) diameter = 5.0;

            // Pad spacings to 4 values
            while (spacings.Count < 4) spacings.Add(0.0);

            bool isXRow = (direction == 0);
            double surfaceZ = startPoint.Z;
            double cutZ = surfaceZ - Math.Abs(depth);
            double radius = diameter / 2.0;
            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;

            // Compute hole positions for preview
            var holePositions = new List<Point3d>();
            holePositions.Add(startPoint);
            double cursor = isXRow ? startPoint.X : startPoint.Y;
            for (int s = 0; s < 4; s++)
            {
                if (spacings[s] > 0)
                {
                    cursor += spacings[s];
                    if (isXRow)
                        holePositions.Add(new Point3d(cursor, startPoint.Y, startPoint.Z));
                    else
                        holePositions.Add(new Point3d(startPoint.X, cursor, startPoint.Z));
                }
            }

            foreach (Point3d hp in holePositions)
            {
                Point3d topPt = new Point3d(hp.X, hp.Y, surfaceZ);
                Plane cylPlane = new Plane(topPt, Vector3d.ZAxis);
                Cylinder cyl = new Cylinder(new Circle(cylPlane, radius), -Math.Abs(depth));
                Brep cylBrep = cyl.ToBrep(true, true);
                if (cylBrep != null) _previewVolumes.Add(cylBrep);
            }
            _approachLines.Add(new Line(
                new Point3d(startPoint.X, startPoint.Y, surfaceZ + 20.0),
                new Point3d(startPoint.X, startPoint.Y, surfaceZ)));

            // NC macro
            List<string> lines = new List<string>();
            lines.Add("WZB (" + toolNr + ",_VE,_V*1,_VA,_SD,0,'')");

            string macro;
            int spiegelVal = mirror ? 1 : 0;

            if (isXRow)
            {
                macro = "CALL _Bohgx_V5(VAL "
                    + "SPY:=" + NcFmt.F(startPoint.Y) + ","
                    + "BIX:=" + NcFmt.F(spacings[0]) + ","
                    + "BIIX:=" + NcFmt.F(spacings[1]) + ","
                    + "BIIIX:=" + NcFmt.F(spacings[2]) + ","
                    + "BIIIIX:=" + NcFmt.F(spacings[3]) + ","
                    + "SPIEGELN:=" + spiegelVal + ","
                    + "T:=" + NcFmt.F(cutZ) + ","
                    + "D:=" + NcFmt.F(diameter) + ","
                    + "TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1,"
                    + "USE2:=1,USE3:=1,USE4:=1)";
            }
            else
            {
                macro = "CALL _Bohgy_V5(VAL "
                    + "SPX:=" + NcFmt.F(startPoint.X) + ","
                    + "BIY:=" + NcFmt.F(spacings[0]) + ","
                    + "BIIY:=" + NcFmt.F(spacings[1]) + ","
                    + "BIIIY:=" + NcFmt.F(spacings[2]) + ","
                    + "BIIIIY:=" + NcFmt.F(spacings[3]) + ","
                    + "SPIEGELN:=" + spiegelVal + ","
                    + "T:=" + NcFmt.F(cutZ) + ","
                    + "D:=" + NcFmt.F(diameter) + ","
                    + "TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1,"
                    + "USE2:=1,USE3:=1,USE4:=1)";
            }
            lines.Add(macro);

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                (isXRow ? "X-row" : "Y-row")
                + "  " + holePositions.Count + " holes"
                + "  D=" + NcFmt.F(diameter)
                + "  T=" + NcFmt.F(cutZ));

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
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.ValueList(
                    ("X-row", "0"),
                    ("Y-row", "1")),
                DynesticPostProcessor.AutoWire.Spec.Point(),
                DynesticPostProcessor.AutoWire.Spec.Float("0<32<500"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<13<50"),
                DynesticPostProcessor.AutoWire.Spec.Float("1<5<50"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
