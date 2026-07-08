using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rhino.Geometry;

namespace WallabyHop.Components.Korpus
{
    /// <summary>
    /// Data class representing a single flat panel of a cabinet body (Korpus).
    /// Each panel stores its flat Brep (at Z=0), real-world dimensions, grain direction,
    /// and the transform needed to position it in the assembled 3D korpus.
    /// </summary>
    public class KorpusPanel
    {
        /// <summary>Panel name: Bottom, Top, LeftSide, RightSide, BackPanel</summary>
        public string Name { get; set; }

        /// <summary>Planar Brep lying at Z=0, sized to panel real-world dimensions (width x height)</summary>
        public Brep FlatBrep { get; set; }

        /// <summary>Panel width in mm</summary>
        public double Width { get; set; }

        /// <summary>Panel height in mm</summary>
        public double Height { get; set; }

        /// <summary>Material thickness in mm (used for 3D assembly positioning, not flat geometry)</summary>
        public double Thickness { get; set; }

        /// <summary>Grain direction vector, default (1,0,0) = along X-axis</summary>
        public Vector3d GrainDir { get; set; }

        /// <summary>Origin point where this panel sits in the 3D assembled korpus</summary>
        public Point3d AssembledOrigin { get; set; }

        /// <summary>Transform from flat XY position to 3D assembled position</summary>
        public Transform AssembledTransform { get; set; }

        /// <summary>Operation groups (each group is a list of HOP lines: tool change + operation)</summary>
        public List<List<string>> OperationGroups { get; set; } = new List<List<string>>();

        /// <summary>
        /// Creates a new KorpusPanel with a flat rectangular Brep at the origin.
        /// </summary>
        /// <param name="name">Panel name (e.g. "Bottom", "Top")</param>
        /// <param name="width">Panel width in mm</param>
        /// <param name="height">Panel height in mm</param>
        /// <param name="thickness">Material thickness in mm</param>
        public KorpusPanel(string name, double width, double height, double thickness)
        {
            Name = name;
            Width = width;
            Height = height;
            Thickness = thickness;
            GrainDir = new Vector3d(1, 0, 0);
            AssembledOrigin = Point3d.Origin;
            AssembledTransform = Transform.Identity;

            // Create flat rectangular Brep at Z=0
            Point3d p0 = new Point3d(0, 0, 0);
            Point3d p1 = new Point3d(width, 0, 0);
            Point3d p2 = new Point3d(width, height, 0);
            Point3d p3 = new Point3d(0, height, 0);
            FlatBrep = Brep.CreateFromCornerPoints(p0, p1, p2, p3, 0.001);
        }

        /// <summary>
        /// Returns a dictionary compatible with HopPart downstream wiring.
        /// Keys: "outline", "operationLines", "grainDir", "panelName", "thickness"
        /// </summary>
        public Dictionary<string, object> ToPartDict()
        {
            var dict = new Dictionary<string, object>();

            // Extract outline curve from flat Brep naked edges
            Curve[] nakedEdges = FlatBrep.DuplicateNakedEdgeCurves(true, false);
            Curve[] joined = Curve.JoinCurves(nakedEdges);
            dict["outline"] = (joined != null && joined.Length > 0) ? joined[0] : null;

            // Operation lines: use generated groups if available, else empty placeholder
            dict["operationLines"] = OperationGroups.Count > 0
                ? OperationGroups
                : new List<List<string>> { new List<string>() };

            dict["grainDir"] = GrainDir;
            dict["panelName"] = Name;
            dict["thickness"] = Thickness;  // per-panel DZ for HopPartExport

            return dict;
        }

        /// <summary>
        /// Adds a free-slot groove using the _nuten_frei_v5 macro.
        /// X1/Y1 and X2/Y2 are the centerline start/end of the groove in flat panel 2D space.
        /// width is the groove width (= nutWidth or falzWidth), depth is positive (tool goes DOWN).
        /// </summary>
        public void AddNutFrei(double x1, double y1, double x2, double y2,
                               double width, double depth, int toolNr)
        {
            var group = new List<string>();
            group.Add("WZF (" + toolNr + ",_VE,_V*1,_VA,_SD,0,'')");
            // Re-use the tested emitter (single formatter, sci-notation safe)
            group.Add(NcSaw.FreeSlotLine(x1, y1, x2, y2, width, -Math.Abs(depth), 0.0));
            OperationGroups.Add(group);
        }

        /// <summary>
        /// Adds a rectangular formatting contour (outer perimeter cut) using SP/G01/EP.
        /// The tool follows the outer rectangle of this panel at full material depth.
        /// Radius compensation must be configured at the machine level.
        /// </summary>
        public void AddFormattingContour(int toolNr)
        {
            string w  = NcFmt.F(Width);
            string h  = NcFmt.F(Height);
            string dz = NcFmt.F(-Math.Abs(Thickness));

            var group = new List<string>();
            group.Add("WZF (" + toolNr + ",_VE,_V*1,_VA,_SD,0,'')");
            group.Add("SP (0,0," + dz + ",2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)");
            group.Add("G01 (" + w + ",0,0,0,0,2)");
            group.Add("G01 (" + w + "," + h + ",0,0,0,2)");
            group.Add("G01 (0," + h + ",0,0,0,2)");
            group.Add("G01 (0,0,0,0,0,2)");
            group.Add("EP (0,_ANF,0)");
            OperationGroups.Add(group);
        }

        /// <summary>
        /// Adds a drill operation group to this panel.
        /// X,Y are coordinates in the flat panel's local 2D space (Z=0 surface).
        /// surfaceZ is the Z of the top face (0.0 for flat panels at Z=0).
        /// depth is the drilling depth (positive value, tool goes DOWN by this amount).
        /// </summary>
        public void AddDrillGroup(double x, double y, double diameter, double depth, int toolNr)
        {
            var group = new List<string>();
            group.Add(NcDrill.ToolCall(toolNr));
            group.Add(NcDrill.DrillLine(x, y, 0.0, -depth, diameter));
            OperationGroups.Add(group);
        }
    }
}
