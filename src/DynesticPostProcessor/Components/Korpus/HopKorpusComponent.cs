using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace DynesticPostProcessor.Components.Korpus
{
    public class HopKorpusComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.FromArgb(180, 140, 100);
        private List<Brep>  _previewBreps  = null;
        private List<Color> _previewColors = null;
        private List<Brep>  _drillBreps    = null;
        private Color _drawColor = Color.FromArgb(180, 140, 100);

        // Panel color palette (distinct wood tones for visual separation)
        private static Color PanelColor(string name)
        {
            switch (name)
            {
                case "Bottom":    return Color.FromArgb(200, 160,  80); // golden oak
                case "Top":       return Color.FromArgb(170, 130,  70); // darker oak
                case "LeftSide":  return Color.FromArgb(160, 100,  50); // walnut brown
                case "RightSide": return Color.FromArgb(150,  90,  45); // slightly diff
                case "BackPanel": return Color.FromArgb(120, 100,  80); // slate brown
                default:
                    if (name.StartsWith("Shelf")) return Color.FromArgb(210, 185, 140); // light pine
                    if (name.StartsWith("Door"))  return Color.FromArgb( 90,  55,  30); // dark mahogany
                    return Color.FromArgb(180, 140, 100);
            }
        }

        public HopKorpusComponent()
            : base("HopKorpus", "HopKorpus",
                "Parametric cabinet body generator. Produces 5 flat panels (Bottom, Top, LeftSide, RightSide, BackPanel, open front) from dimension sliders. Outputs CabinetData dictionary and individual panel objects for HopPart nesting.",
                "DYNESTIC", "Cabinet")
        {
        }

        // -----------------------------------------------------------------
        // INPUTS
        // -----------------------------------------------------------------
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Width", "W",
                "Cabinet width in mm (outer dimension).",
                GH_ParamAccess.item, 600.0);
            pManager[0].Optional = true;

            pManager.AddNumberParameter("Height", "H",
                "Cabinet height in mm (outer dimension).",
                GH_ParamAccess.item, 720.0);
            pManager[1].Optional = true;

            pManager.AddNumberParameter("Depth", "D",
                "Cabinet depth in mm (outer dimension).",
                GH_ParamAccess.item, 560.0);
            pManager[2].Optional = true;

            pManager.AddNumberParameter("Thickness", "t",
                "Material thickness in mm. Typical: 16 or 19.",
                GH_ParamAccess.item, 19.0);
            pManager[3].Optional = true;

            pManager.AddTextParameter("Type", "type",
                "Cabinet type label (e.g. base cabinet, wall cabinet). Label only, no structural effect.",
                GH_ParamAccess.item, "Cabinet");
            pManager[4].Optional = true;

            pManager.AddColourParameter("Colour", "colour",
                "Preview colour for the 3D korpus model in the Rhino viewport. Default warm brown.",
                GH_ParamAccess.item, Color.FromArgb(180, 140, 100));
            pManager[5].Optional = true;

            // Index 6
            pManager.AddGenericParameter("Back", "back",
                "Back panel options from HopCabinetBack. Optional -- defaults to surface-mounted 8mm back.",
                GH_ParamAccess.item);
            pManager[6].Optional = true;

            // Index 7
            pManager.AddGenericParameter("Connectors", "connectors",
                "Corner connector options from HopConnector. Optional -- no connector holes generated if not wired.",
                GH_ParamAccess.item);
            pManager[7].Optional = true;

            // Index 8
            pManager.AddGenericParameter("Shelves", "shelves",
                "Shelf options from HopShelves. Optional -- no shelf holes generated if not wired.",
                GH_ParamAccess.item);
            pManager[8].Optional = true;

            // Index 9
            pManager.AddGenericParameter("Feet", "feet",
                "Levelling feet options from HopFeet. Optional -- no foot holes generated if not wired.",
                GH_ParamAccess.item);
            pManager[9].Optional = true;

            // Index 10
            pManager.AddGenericParameter("Door", "door",
                "Door options from HopCabinetDoor. Optional -- no door panels added if not wired.",
                GH_ParamAccess.item);
            pManager[10].Optional = true;

            // Index 11
            pManager.AddIntegerParameter("DrillToolNr", "tool",
                "Tool magazine number used for all generated drill holes (connectors, hinges, feet, shelf holes). Must be > 0. Default 1.",
                GH_ParamAccess.item, 1);
            pManager[11].Optional = true;

            // Index 12
            pManager.AddIntegerParameter("RouterToolNr", "router",
                "Tool magazine number for the router/end mill used for Nut/Falz grooves and outer formatting contour. " +
                "Set to 0 to skip all milling operations. Default 0.",
                GH_ParamAccess.item, 0);
            pManager[12].Optional = true;
        }

        // -----------------------------------------------------------------
        // OUTPUTS
        // -----------------------------------------------------------------
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("CabinetData", "cabinet",
                "Complete cabinet dictionary wrapped in GH_ObjectWrapper. Contains W, H, D, thickness, type, and panel list. Wire into downstream Cabinet components (HopCabinetBack, HopConnector, etc.).",
                GH_ParamAccess.item);

            pManager.AddGenericParameter("Panels", "panels",
                "Individual flat panel dictionaries (GH_ObjectWrapper), one per panel. Each contains outline curve, operationLines, grainDir, panelName. Wire into HopPart for nesting.",
                GH_ParamAccess.list);

            pManager.AddBrepParameter("AssembledBreps", "breps",
                "Assembled 3D solid Breps of all panels in their corpus positions. Wire into HopDrawing for automatic drawing generation.",
                GH_ParamAccess.list);
        }

        // -----------------------------------------------------------------
        // CLEAR DATA (prevent ghost geometry on disconnect)
        // -----------------------------------------------------------------
        public override void ClearData()
        {
            base.ClearData();
            _previewBreps  = null;
            _previewColors = null;
            _drillBreps    = null;
        }

        // -----------------------------------------------------------------
        // SOLVE
        // -----------------------------------------------------------------
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 0. Clear preview state
            _previewBreps  = null;
            _previewColors = null;
            _drillBreps    = null;

            // 1. Get inputs with defaults
            double B = 600, H = 720, T = 560, MS = 19;
            string cabinetType = "Cabinet";
            Color colour = Color.Empty;

            DA.GetData(0, ref B);
            DA.GetData(1, ref H);
            DA.GetData(2, ref T);
            DA.GetData(3, ref MS);
            DA.GetData(4, ref cabinetType);
            DA.GetData(5, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // 1b. Get option inputs
            GH_ObjectWrapper backWrap = null, connWrap = null, shelvesWrap = null, feetWrap = null, doorWrap = null;
            int drillToolNr = 1;
            DA.GetData(6, ref backWrap);
            DA.GetData(7, ref connWrap);
            DA.GetData(8, ref shelvesWrap);
            DA.GetData(9, ref feetWrap);
            DA.GetData(10, ref doorWrap);
            DA.GetData(11, ref drillToolNr);
            if (drillToolNr <= 0) drillToolNr = 1;

            int routerToolNr = 0;
            DA.GetData(12, ref routerToolNr);

            Dictionary<string, object> backDict     = backWrap?.Value as Dictionary<string, object>;
            Dictionary<string, object> connDict     = connWrap?.Value as Dictionary<string, object>;
            Dictionary<string, object> shelvesDict  = shelvesWrap?.Value as Dictionary<string, object>;
            Dictionary<string, object> feetDict     = feetWrap?.Value as Dictionary<string, object>;
            Dictionary<string, object> doorDict     = doorWrap?.Value as Dictionary<string, object>;

            // 2. Guards
            if (B <= 0 || H <= 0 || T <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Dimensions must be > 0");
                return;
            }

            double minDim = Math.Min(B, Math.Min(H, T));
            if (MS <= 0 || MS >= minDim / 2.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Thickness invalid (must be > 0 and < half the smallest dimension)");
                return;
            }

            // 3. Calculate inner dimensions (butt-joint construction)
            double innerB = B - 2.0 * MS;  // between side panels
            double innerH = H - 2.0 * MS;  // between Bottom and Top
            double innerT = T;              // depth stays same for butt-joint

            // 4. Create 5 KorpusPanel objects (NO front panel -- open front)
            var boden       = new KorpusPanel("Bottom",    innerB, T, MS);
            var deckel      = new KorpusPanel("Top",       innerB, T, MS);
            var linkeSeite  = new KorpusPanel("LeftSide",  T,      H, MS);
            var rechteSeite = new KorpusPanel("RightSide", T,      H, MS);
            var rueckwand   = new KorpusPanel("BackPanel", innerB, innerH, MS);

            // 5. Compute assembled transforms (flat XY -> 3D position)
            // Korpus front-bottom-left corner at world origin (0,0,0).
            // Side panels run full height (butt joint), Boden/Deckel sit between sides.

            // Boden: horizontal at bottom, shifted right by MS (sits between sides)
            boden.AssembledOrigin = new Point3d(MS, 0, 0);
            boden.AssembledTransform = Transform.Translation(MS, 0, 0);

            // Deckel: horizontal at top, shifted right by MS, elevated to H - MS
            deckel.AssembledOrigin = new Point3d(MS, 0, H - MS);
            deckel.AssembledTransform = Transform.Translation(MS, 0, H - MS);

            // LinkeSeite: stands vertically at X=0 in YZ plane
            // Flat: (0,0,0)-(T,0,0)-(T,H,0)-(0,H,0)
            // Assembled: (0,0,0)-(0,T,0)-(0,T,H)-(0,0,H)
            var linkeSource = Plane.WorldXY;
            var linkeTarget = new Plane(
                new Point3d(0, 0, 0),
                new Vector3d(0, 1, 0),
                new Vector3d(0, 0, 1));
            linkeSeite.AssembledOrigin = new Point3d(0, 0, 0);
            linkeSeite.AssembledTransform = Transform.PlaneToPlane(linkeSource, linkeTarget);

            // RechteSeite: stands vertically at X = B - MS
            var rechteTarget = new Plane(
                new Point3d(B - MS, 0, 0),
                new Vector3d(0, 1, 0),
                new Vector3d(0, 0, 1));
            rechteSeite.AssembledOrigin = new Point3d(B - MS, 0, 0);
            rechteSeite.AssembledTransform = Transform.PlaneToPlane(linkeSource, rechteTarget);

            // Rueckwand: stands vertically at back (Y = T - MS) in XZ plane
            // Flat: (0,0,0)-(innerB,0,0)-(innerB,innerH,0)-(0,innerH,0)
            // Assembled: (MS,T-MS,MS)-(MS+innerB,T-MS,MS)-(MS+innerB,T-MS,MS+innerH)-(MS,T-MS,MS+innerH)
            var rueckTarget = new Plane(
                new Point3d(MS, T - MS, MS),
                new Vector3d(1, 0, 0),
                new Vector3d(0, 0, 1));
            rueckwand.AssembledOrigin = new Point3d(MS, T - MS, MS);
            rueckwand.AssembledTransform = Transform.PlaneToPlane(linkeSource, rueckTarget);

            // ---------------------------------------------------------------
            // PROCESS OPTION INPUTS
            // ---------------------------------------------------------------

            // Collect world-space preview elements built during processing
            var extraDrillBreps = new List<Brep>();

            // -- Process Back (adjust BackPanel geometry) --
            if (backDict != null)
            {
                int backType      = Convert.ToInt32(backDict["type"]);
                double backThick  = Convert.ToDouble(backDict["thickness"]);
                double setback    = Convert.ToDouble(backDict["setback"]);        // Eingelegt: dist from back edge
                double cutDepth   = Convert.ToDouble(backDict["cutDepth"]);       // Eingefälzt/Eingenutert: rabbet/groove depth
                double falzWidth  = Convert.ToDouble(backDict["falzWidth"]);      // = backThick + 0.5
                double reststegD  = Convert.ToDouble(backDict["reststegDist"]);   // Eingenutert: dist from back edge to groove rear

                if (backType == 0) // Eingelegt: back panel sits inside, positioned by setback from back
                {
                    rueckwand = new KorpusPanel("BackPanel", innerB, innerH, backThick);
                    rueckwand.AssembledTransform = Transform.PlaneToPlane(Plane.WorldXY,
                        new Plane(new Point3d(MS, T - backThick - setback, MS),
                                  new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)));
                }
                else if (backType == 1) // Eingefälzt: Falznut rabbet in all 4 outer panels
                {
                    // Back panel (innerB x innerH) slides into the Falz; front face at T - falzWidth
                    rueckwand = new KorpusPanel("BackPanel", innerB, innerH, backThick);
                    rueckwand.AssembledTransform = Transform.PlaneToPlane(Plane.WorldXY,
                        new Plane(new Point3d(MS, T - falzWidth, MS),
                                  new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)));
                }
                else // Eingenutert: Nut groove in outer panels, back sits at reststegDist from back
                {
                    rueckwand = new KorpusPanel("BackPanel", innerB, innerH, backThick);
                    double backY = T - reststegD - backThick;
                    rueckwand.AssembledTransform = Transform.PlaneToPlane(Plane.WorldXY,
                        new Plane(new Point3d(MS, Math.Max(0, backY), MS),
                                  new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)));
                }
            }

            // -- Build panels list (after back processing) --
            var extraPanels = new List<KorpusPanel>(); // shelves, doors added here

            // -- Falz/Nut recesses in side panels (visible viewport geometry) --
            if (backDict != null)
            {
                int backType     = Convert.ToInt32(backDict["type"]);
                double cutDepth  = Convert.ToDouble(backDict["cutDepth"]);
                double falzWidth = Convert.ToDouble(backDict["falzWidth"]);
                double nutWidth  = Convert.ToDouble(backDict["nutWidth"]);
                double reststegD = Convert.ToDouble(backDict["reststegDist"]);

                if (backType == 1) // Eingefälzt: L-shaped rabbet at back edge of all 4 panels
                {
                    // LeftSide (X=0..MS): inner face at X=MS, Falz at Y=T-falzWidth..T
                    AddRecessBox(new BoundingBox(
                        new Point3d(MS - cutDepth, T - falzWidth, 0),
                        new Point3d(MS, T, H)), extraDrillBreps);
                    // RightSide (X=B-MS..B): inner face at X=B-MS
                    AddRecessBox(new BoundingBox(
                        new Point3d(B - MS, T - falzWidth, 0),
                        new Point3d(B - MS + cutDepth, T, H)), extraDrillBreps);
                    // Boden (Z=0..MS): inner face at Z=MS
                    AddRecessBox(new BoundingBox(
                        new Point3d(MS, T - falzWidth, MS - cutDepth),
                        new Point3d(B - MS, T, MS)), extraDrillBreps);
                    // Deckel (Z=H-MS..H): inner face at Z=H-MS
                    AddRecessBox(new BoundingBox(
                        new Point3d(MS, T - falzWidth, H - MS),
                        new Point3d(B - MS, T, H - MS + cutDepth)), extraDrillBreps);
                }
                else if (backType == 2) // Eingenutert: groove at distance reststegDist from back
                {
                    double nutY1 = T - reststegD - nutWidth;
                    double nutY2 = T - reststegD;
                    // LeftSide
                    AddRecessBox(new BoundingBox(
                        new Point3d(MS - cutDepth, Math.Max(0, nutY1), 0),
                        new Point3d(MS, nutY2, H)), extraDrillBreps);
                    // RightSide
                    AddRecessBox(new BoundingBox(
                        new Point3d(B - MS, Math.Max(0, nutY1), 0),
                        new Point3d(B - MS + cutDepth, nutY2, H)), extraDrillBreps);
                    // Boden
                    AddRecessBox(new BoundingBox(
                        new Point3d(MS, Math.Max(0, nutY1), MS - cutDepth),
                        new Point3d(B - MS, nutY2, MS)), extraDrillBreps);
                    // Deckel
                    AddRecessBox(new BoundingBox(
                        new Point3d(MS, Math.Max(0, nutY1), H - MS),
                        new Point3d(B - MS, nutY2, H - MS + cutDepth)), extraDrillBreps);
                }
            }

            // -- Process Connectors --
            if (connDict != null)
            {
                double drillDia   = Convert.ToDouble(connDict["drillDiameter"]);
                double drillDepth = Convert.ToDouble(connDict["drillDepth"]);
                bool autoCount    = Convert.ToBoolean(connDict["autoCount"]);
                int connCount     = Convert.ToInt32(connDict["count"]);
                bool isRouting    = Convert.ToBoolean(connDict["isRouting"]);
                double edge       = Convert.ToDouble(connDict["edge"]);  // 37mm

                if (!isRouting && drillDia > 0)
                {
                    if (autoCount)
                        connCount = T <= 300 ? 2 : (T <= 500 ? 3 : 4);
                    connCount = Math.Max(1, connCount);

                    // Even distribution along depth T
                    double spacing = connCount > 1 ? (T - 2 * edge) / (connCount - 1) : 0;
                    var xPositions = new List<double>();
                    for (int i = 0; i < connCount; i++)
                        xPositions.Add(edge + i * spacing);

                    // Side panels: face drilling at joint zones (Y = MS/2 for Boden, Y = H-MS/2 for Deckel)
                    foreach (double xp in xPositions)
                    {
                        linkeSeite.AddDrillGroup(xp, MS / 2.0,      drillDia, drillDepth, drillToolNr);
                        linkeSeite.AddDrillGroup(xp, H - MS / 2.0,  drillDia, drillDepth, drillToolNr);
                        rechteSeite.AddDrillGroup(xp, MS / 2.0,     drillDia, drillDepth, drillToolNr);
                        rechteSeite.AddDrillGroup(xp, H - MS / 2.0, drillDia, drillDepth, drillToolNr);
                    }

                    // Boden/Deckel: edge drillings from left/right sides (horizontal, visual preview only)
                    // In world space: Boden assembled at Z=0..MS, center Z=MS/2
                    //                 Deckel assembled at Z=H-MS..H, center Z=H-MS/2
                    double edgeR = drillDia / 2.0;
                    foreach (double xp in xPositions)
                    {
                        // Boden left edge (X=MS, going +X)
                        BuildEdgeCylinder(new Point3d(MS, xp, MS / 2.0),
                            new Vector3d(1, 0, 0), edgeR, drillDepth, extraDrillBreps);
                        // Boden right edge (X=B-MS, going -X)
                        BuildEdgeCylinder(new Point3d(B - MS, xp, MS / 2.0),
                            new Vector3d(-1, 0, 0), edgeR, drillDepth, extraDrillBreps);
                        // Deckel left edge
                        BuildEdgeCylinder(new Point3d(MS, xp, H - MS / 2.0),
                            new Vector3d(1, 0, 0), edgeR, drillDepth, extraDrillBreps);
                        // Deckel right edge
                        BuildEdgeCylinder(new Point3d(B - MS, xp, H - MS / 2.0),
                            new Vector3d(-1, 0, 0), edgeR, drillDepth, extraDrillBreps);
                    }
                }
            }

            // Compute effective cabinet depth (front to back panel inner face),
            // accounting for back panel type and setback. Shelves and S32 holes respect this limit.
            double effectiveDepth = T;
            if (backDict != null)
            {
                int bt = Convert.ToInt32(backDict["type"]);
                double bThick = Convert.ToDouble(backDict["thickness"]);
                if (bt == 0) // Eingelegt: back panel front face at T - bThick - setback
                    effectiveDepth = T - bThick - Convert.ToDouble(backDict["setback"]);
                else if (bt == 1) // Eingefälzt: front face at T - falzWidth
                    effectiveDepth = T - Convert.ToDouble(backDict["falzWidth"]);
                else // Eingenutert: front face at T - reststegDist - thickness
                    effectiveDepth = T - Convert.ToDouble(backDict["reststegDist"]) - bThick;
                effectiveDepth = Math.Max(MS + 10, effectiveDepth); // minimum sanity
            }

            // -- Process Shelves (N shelves evenly distributed, System-32 holes in sides) --
            if (shelvesDict != null)
            {
                int shelfCount   = Convert.ToInt32(shelvesDict["count"]);
                double s32raster = Convert.ToDouble(shelvesDict["s32_raster"]);  // 32
                double s32edge   = Convert.ToDouble(shelvesDict["s32_edge"]);    // 37
                double s32dia    = Convert.ToDouble(shelvesDict["s32_drill_d"]); // 5
                double s32depth  = Convert.ToDouble(shelvesDict["s32_depth"]);   // 13

                if (shelfCount > 0)
                {
                    // Evenly distribute shelves in inner height
                    double shelfSpacing = innerH / (shelfCount + 1);
                    // Shelf runs from front opening to back panel face (effectiveDepth)
                    double shelfDepth = Math.Max(1, effectiveDepth);

                    for (int i = 1; i <= shelfCount; i++)
                    {
                        double shelfZ = MS + i * shelfSpacing;
                        var shelf = new KorpusPanel("Shelf" + i, innerB, shelfDepth, MS);
                        shelf.AssembledTransform = Transform.Translation(MS, 0, shelfZ);
                        extraPanels.Add(shelf);
                    }

                    // System-32 hole rows in both side panels
                    // Front column at X=s32edge, back column at X=effectiveDepth-s32edge
                    double hStart    = s32edge;
                    double hEnd      = H - s32edge;
                    double backColX  = Math.Max(s32edge + s32raster, effectiveDepth - s32edge);
                    var hPos = new List<double>();
                    for (double yp = hStart; yp <= hEnd + 0.1; yp += s32raster)
                        hPos.Add(yp);

                    foreach (double yp in hPos)
                    {
                        linkeSeite.AddDrillGroup(s32edge,  yp, s32dia, s32depth, drillToolNr);
                        linkeSeite.AddDrillGroup(backColX, yp, s32dia, s32depth, drillToolNr);
                        rechteSeite.AddDrillGroup(s32edge,  yp, s32dia, s32depth, drillToolNr);
                        rechteSeite.AddDrillGroup(backColX, yp, s32dia, s32depth, drillToolNr);
                    }
                }
            }

            // -- Process Feet (Befestigungsplatte: 4 holes in 64x64mm grid per corner) --
            if (feetDict != null)
            {
                double footDia    = Convert.ToDouble(feetDict["drillDiameter"]);
                double footDepth  = Convert.ToDouble(feetDict["drillDepth"]);
                double footOffset = Convert.ToDouble(feetDict["footOffset"]);
                double halfGrid   = Convert.ToDouble(feetDict["footGrid"]) / 2.0; // 32mm

                // Foot plate centers in flat Boden coords (X=0..innerB, Y=0..T)
                var footCenters = new List<(double x, double y)>
                {
                    (footOffset,          footOffset),
                    (innerB - footOffset, footOffset),
                    (footOffset,          T - footOffset),
                    (innerB - footOffset, T - footOffset),
                };
                if (B > 800)
                {
                    footCenters.Add((innerB / 2.0, footOffset));
                    footCenters.Add((innerB / 2.0, T - footOffset));
                }

                foreach (var (refX, refY) in footCenters)
                {
                    // 4 holes at ±halfGrid from center
                    boden.AddDrillGroup(refX - halfGrid, refY - halfGrid, footDia, footDepth, drillToolNr);
                    boden.AddDrillGroup(refX + halfGrid, refY - halfGrid, footDia, footDepth, drillToolNr);
                    boden.AddDrillGroup(refX - halfGrid, refY + halfGrid, footDia, footDepth, drillToolNr);
                    boden.AddDrillGroup(refX + halfGrid, refY + halfGrid, footDia, footDepth, drillToolNr);
                }
            }

            // -- Process Door --
            if (doorDict != null)
            {
                int doorCount    = Convert.ToInt32(doorDict["count"]);
                int overlay      = Convert.ToInt32(doorDict["overlay"]);
                double gap       = Convert.ToDouble(doorDict["gap"]);
                double cupDia    = Convert.ToDouble(doorDict["hinge_cupDia"]);    // 35
                double cupDepth  = Convert.ToDouble(doorDict["hinge_cupDepth"]);  // 13.5
                double edgeDist  = Convert.ToDouble(doorDict["hinge_edgeDist"]);  // 22.5
                double firstPos  = Convert.ToDouble(doorDict["hinge_s32Pos"]);    // 128

                // Compute door dimensions based on overlay type
                // For 2 doors: account for center gap (doorCount+1 total gaps)
                double doorW, doorH;
                int totalGaps = doorCount + 1; // left gap + center gap(s) + right gap
                if (overlay == 0) // FullOverlay
                {
                    doorW = (B - totalGaps * gap) / doorCount;
                    doorH = H - 2 * gap;
                }
                else if (overlay == 1) // HalfOverlay
                {
                    doorW = (B - MS - totalGaps * gap) / doorCount;
                    doorH = H - 2 * gap;
                }
                else // Inset
                {
                    doorW = (innerB - totalGaps * gap) / doorCount;
                    doorH = innerH - 2 * gap;
                }
                doorW = Math.Max(1, doorW);
                doorH = Math.Max(1, doorH);

                // Hinge count by door height
                int hingeCount = doorH <= 900 ? 2 : (doorH <= 1500 ? 3 : (doorH <= 1800 ? 4 : 5));

                // Hinge Y positions along door height (in flat panel coords, Y = height)
                var hingeYPositions = new List<double>();
                hingeYPositions.Add(firstPos);           // from top = firstPos, so from bottom = doorH - firstPos
                hingeYPositions.Add(doorH - firstPos);   // from bottom
                // Additional hinges evenly distributed
                if (hingeCount > 2)
                {
                    double spacing = (doorH - 2 * firstPos) / (hingeCount - 1);
                    for (int i = 1; i < hingeCount - 1; i++)
                        hingeYPositions.Add(firstPos + i * spacing);
                    hingeYPositions.Sort();
                }

                // Create door panel(s)
                for (int d = 0; d < doorCount; d++)
                {
                    string doorName = doorCount == 1 ? "Door" : (d == 0 ? "DoorLeft" : "DoorRight");
                    var doorPanel = new KorpusPanel(doorName, doorW, doorH, MS);

                    // Hinge cup holes on door panel (at X = edgeDist from hinge side, Y = hinge positions)
                    // Hinge side: left door = left side (X = edgeDist), right door = right side (X = doorW - edgeDist)
                    double hingeX = (d == 0) ? edgeDist : doorW - edgeDist;
                    // If single door, respect HingeSide:
                    int hingeSide = Convert.ToInt32(doorDict["hingeSide"]);
                    if (doorCount == 1)
                        hingeX = (hingeSide == 1) ? doorW - edgeDist : edgeDist; // 0=Left, 1=Right

                    foreach (double yp in hingeYPositions)
                        doorPanel.AddDrillGroup(hingeX, yp, cupDia, cupDepth, drillToolNr);

                    // Assemble door vertically at Y=0 (front face of cabinet)
                    // Flat panel: width=doorW along X, height=doorH along Y
                    // Assembled: width along X, height along Z, front face at Y=0
                    double dxOffset, dzOffset;
                    if (overlay == 0) // FullOverlay
                    {
                        dxOffset = gap + d * (doorW + gap);
                        dzOffset = gap;
                    }
                    else if (overlay == 1) // HalfOverlay
                    {
                        dxOffset = gap + d * (doorW + gap);
                        dzOffset = gap;
                    }
                    else // Inset
                    {
                        dxOffset = MS + gap + d * (doorW + gap);
                        dzOffset = MS + gap;
                    }
                    doorPanel.AssembledTransform = Transform.PlaneToPlane(Plane.WorldXY,
                        new Plane(new Point3d(dxOffset, 0, dzOffset),
                                  new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)));

                    extraPanels.Add(doorPanel);
                }
            }

            // ---------------------------------------------------------------
            // 6. Build KorpusData dictionary
            // ---------------------------------------------------------------
            var allPanels = new List<KorpusPanel> { boden, deckel, linkeSeite, rechteSeite, rueckwand };
            allPanels.AddRange(extraPanels);

            // ---------------------------------------------------------------
            // ROUTER OPERATIONS: Nut/Falz grooves + outer formatting contour
            // Only generated when RouterToolNr > 0
            // ---------------------------------------------------------------
            if (routerToolNr > 0)
            {
                // -- Nut/Falz grooves in the 4 outer panels --
                if (backDict != null)
                {
                    int backType    = Convert.ToInt32(backDict["type"]);
                    double falzWidth  = Convert.ToDouble(backDict["falzWidth"]);
                    double nutWidth   = Convert.ToDouble(backDict["nutWidth"]);
                    double cutDepth   = Convert.ToDouble(backDict["cutDepth"]);
                    double reststegD  = Convert.ToDouble(backDict["reststegDist"]);

                    // LeftSide/RightSide: flat Width = T (depth). Back edge at X = Width.
                    // Boden/Deckel: flat Height = T (depth). Back edge at Y = Height.

                    if (backType == 1) // Eingefälzt: Falznut at back edge
                    {
                        double sideCenterX  = linkeSeite.Width - falzWidth / 2.0;
                        double horizCenterY = boden.Height     - falzWidth / 2.0;

                        linkeSeite.AddNutFrei(sideCenterX, 0, sideCenterX, linkeSeite.Height,
                            falzWidth, cutDepth, routerToolNr);
                        rechteSeite.AddNutFrei(sideCenterX, 0, sideCenterX, rechteSeite.Height,
                            falzWidth, cutDepth, routerToolNr);
                        boden.AddNutFrei(0, horizCenterY, boden.Width, horizCenterY,
                            falzWidth, cutDepth, routerToolNr);
                        deckel.AddNutFrei(0, horizCenterY, deckel.Width, horizCenterY,
                            falzWidth, cutDepth, routerToolNr);

                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                            "Router: Falznut generated in 4 panels, Falzbreite="
                            + falzWidth.ToString("F1") + "mm, Tiefe=" + cutDepth + "mm");
                    }
                    else if (backType == 2) // Eingenutert: Nut at reststegDist from back edge
                    {
                        // Groove rear at (Width - reststegD), front at (Width - reststegD - nutWidth)
                        double sideCenterX  = linkeSeite.Width - reststegD - nutWidth / 2.0;
                        double horizCenterY = boden.Height     - reststegD - nutWidth / 2.0;

                        linkeSeite.AddNutFrei(sideCenterX, 0, sideCenterX, linkeSeite.Height,
                            nutWidth, cutDepth, routerToolNr);
                        rechteSeite.AddNutFrei(sideCenterX, 0, sideCenterX, rechteSeite.Height,
                            nutWidth, cutDepth, routerToolNr);
                        boden.AddNutFrei(0, horizCenterY, boden.Width, horizCenterY,
                            nutWidth, cutDepth, routerToolNr);
                        deckel.AddNutFrei(0, horizCenterY, deckel.Width, horizCenterY,
                            nutWidth, cutDepth, routerToolNr);

                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                            "Router: Nut generated in 4 panels, Nutbreite="
                            + nutWidth.ToString("F1") + "mm, Tiefe=" + cutDepth
                            + "mm, Reststeg=" + reststegD + "mm");
                    }
                }

                // -- Formatierung: outer contour cut for every panel --
                foreach (var panel in allPanels)
                    panel.AddFormattingContour(routerToolNr);

                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    "Router: Formatierung contour added to " + allPanels.Count + " panels");
            }

            var korpusDict = new Dictionary<string, object>();
            korpusDict["W"] = B;
            korpusDict["H"] = H;
            korpusDict["D"] = T;
            korpusDict["thickness"] = MS;
            korpusDict["type"] = cabinetType;
            korpusDict["panels"] = allPanels;

            // 6b. Build 3D preview -- transform each flat panel to assembled position, then extrude by thickness
            _previewBreps  = new List<Brep>();
            _previewColors = new List<Color>();
            foreach (var panel in allPanels)
            {
                Brep assembled = panel.FlatBrep.DuplicateBrep();
                assembled.Transform(panel.AssembledTransform);

                Vector3d normal = assembled.Faces[0].NormalAt(
                    assembled.Faces[0].Domain(0).Mid,
                    assembled.Faces[0].Domain(1).Mid);

                Point3d extEnd = Point3d.Origin + normal * panel.Thickness;
                LineCurve extPath = new LineCurve(new Line(Point3d.Origin, extEnd));

                Brep solid = assembled.Faces[0].CreateExtrusion(extPath, true);
                if (solid != null)
                {
                    _previewBreps.Add(solid);
                    _previewColors.Add(PanelColor(panel.Name));
                }
            }

            // 6c. Build drill cylinder previews (connector, feet, hinge holes + world-space extras)
            _drillBreps = new List<Brep>(extraDrillBreps); // start with recesses + edge drillings
            foreach (var panel in allPanels)
            {
                if (panel.OperationGroups.Count == 0) continue;
                // Parse Bohrung lines to get drill positions in flat panel space
                // Then apply AssembledTransform to get world position
                // Normal of assembled panel face = extrusion direction used for the drill
                Brep tmpBrep = panel.FlatBrep.DuplicateBrep();
                tmpBrep.Transform(panel.AssembledTransform);
                Vector3d panelNormal = tmpBrep.Faces[0].NormalAt(
                    tmpBrep.Faces[0].Domain(0).Mid,
                    tmpBrep.Faces[0].Domain(1).Mid);
                panelNormal.Unitize();

                foreach (var group in panel.OperationGroups)
                {
                    foreach (var line in group)
                    {
                        if (!line.StartsWith("Bohrung")) continue;
                        // Format: Bohrung (x,y,surfZ,cutZ,dia,...)
                        int p1 = line.IndexOf('(');
                        int p2 = line.IndexOf(')');
                        if (p1 < 0 || p2 < 0) continue;
                        string[] parts = line.Substring(p1 + 1, p2 - p1 - 1).Split(',');
                        if (parts.Length < 5) continue;
                        double hx, hy, hCutZ, hDia;
                        if (!double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out hx)) continue;
                        if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out hy)) continue;
                        if (!double.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out hCutZ)) continue;
                        if (!double.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out hDia)) continue;
                        double depth = Math.Abs(hCutZ);
                        double radius = hDia / 2.0;
                        if (radius <= 0 || depth <= 0) continue;

                        // Flat Z=0 = machined face of the panel (inner face for vertical panels,
                        // bottom face for horizontal panels).
                        // For horizontal panels (Boden/Deckel): Z=0 maps to bottom/top face,
                        //   normal points up/down = drill goes into material correctly.
                        // For vertical panels (LeftSide/RightSide): Z=0 maps to OUTER face after
                        //   PlaneToPlane, but machining is from the INNER face.
                        //   → Shift point by thickness*normal to reach inner face, then drill inward.
                        Point3d flatPt = new Point3d(hx, hy, 0);
                        flatPt.Transform(panel.AssembledTransform);

                        bool isVertical = Math.Abs(panelNormal.Z) < 0.5;
                        Circle drillCircle;
                        if (isVertical)
                        {
                            // Move from outer face to inner face, drill in -panelNormal direction
                            Point3d innerPt = flatPt + panelNormal * panel.Thickness;
                            drillCircle = new Circle(new Plane(innerPt, -panelNormal), radius);
                        }
                        else
                        {
                            // Horizontal: drill from flatPt into material (in panelNormal direction)
                            drillCircle = new Circle(new Plane(flatPt, panelNormal), radius);
                        }
                        Cylinder cyl = new Cylinder(drillCircle, depth);
                        Brep cylBrep = cyl.ToBrep(true, true);
                        if (cylBrep != null) _drillBreps.Add(cylBrep);
                        break; // one Bohrung line per group is enough
                    }
                }
            }

            // 7. Output 1: KorpusData
            DA.SetData(0, new GH_ObjectWrapper(korpusDict));

            // 8. Output 2: Panels list (HopPart-compatible dictionaries)
            var panelOutputs = new List<GH_ObjectWrapper>();
            foreach (var p in allPanels)
                panelOutputs.Add(new GH_ObjectWrapper(p.ToPartDict()));
            DA.SetDataList(1, panelOutputs);

            // 9. Output 3: AssembledBreps (the solid Breps from preview, already computed)
            DA.SetDataList(2, _previewBreps ?? new List<Brep>());

            // 10. Remark message
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopKorpus: " + allPanels.Count + " panels, " + cabinetType
                + " " + B + "x" + H + "x" + T + " t=" + MS);
        }

        // -----------------------------------------------------------------
        // ICON + GUID
        // -----------------------------------------------------------------
        protected override Bitmap Icon => IconHelper.Load("HopKorpus");

        public override Guid ComponentGuid =>
            new Guid("a3b7c1d2-e4f5-6789-0abc-def123456789");

        // -----------------------------------------------------------------
        // PREVIEW OVERRIDES
        // -----------------------------------------------------------------
        public override BoundingBox ClippingBox
        {
            get
            {
                BoundingBox bb = BoundingBox.Empty;
                if (_previewBreps != null)
                    foreach (var brep in _previewBreps)
                        bb.Union(brep.GetBoundingBox(true));
                if (_drillBreps != null)
                    foreach (var brep in _drillBreps)
                        bb.Union(brep.GetBoundingBox(true));
                return bb;
            }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (_previewBreps != null && _previewColors != null)
            {
                for (int i = 0; i < _previewBreps.Count; i++)
                {
                    var mat = new Rhino.Display.DisplayMaterial(_previewColors[i]);
                    mat.Transparency = 0.25;
                    args.Display.DrawBrepShaded(_previewBreps[i], mat);
                }
            }
            if (_drillBreps != null)
            {
                var drillMat = new Rhino.Display.DisplayMaterial(Color.FromArgb(50, 50, 50));
                drillMat.Transparency = 0.0;
                foreach (var brep in _drillBreps)
                    args.Display.DrawBrepShaded(brep, drillMat);
            }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_previewBreps != null && _previewColors != null)
            {
                for (int i = 0; i < _previewBreps.Count; i++)
                    args.Display.DrawBrepWires(_previewBreps[i], _previewColors[i], 1);
            }
            if (_drillBreps != null)
                foreach (var brep in _drillBreps)
                    args.Display.DrawBrepWires(brep, Color.FromArgb(40, 40, 40), 1);
        }

        // -----------------------------------------------------------------
        // PREVIEW HELPERS (world-space geometry)
        // -----------------------------------------------------------------
        private static void BuildEdgeCylinder(Point3d center, Vector3d dir,
            double radius, double depth, List<Brep> dest)
        {
            dir.Unitize();
            var plane = new Plane(center, dir);
            var cyl = new Cylinder(new Circle(plane, radius), depth);
            var b = cyl.ToBrep(true, true);
            if (b != null) dest.Add(b);
        }

        private static void AddRecessBox(BoundingBox bb, List<Brep> dest)
        {
            if (!bb.IsValid) return;
            var b = Brep.CreateFromBox(bb);
            if (b != null) dest.Add(b);
        }

        // -----------------------------------------------------------------
        // AUTO-WIRE (6 inputs)
        // -----------------------------------------------------------------
        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.Float("100<600<2400"),  // Width
                DynesticPostProcessor.AutoWire.Spec.Float("100<720<2400"),  // Height
                DynesticPostProcessor.AutoWire.Spec.Float("100<560<800"),   // Depth
                DynesticPostProcessor.AutoWire.Spec.Float("8<19<38"),       // Thickness
                DynesticPostProcessor.AutoWire.Spec.Panel("Cabinet"),       // Type
                DynesticPostProcessor.AutoWire.Spec.Skip(),                 // Colour
                DynesticPostProcessor.AutoWire.Spec.Skip(),                 // Back
                DynesticPostProcessor.AutoWire.Spec.Skip(),                 // Connectors
                DynesticPostProcessor.AutoWire.Spec.Skip(),                 // Shelves
                DynesticPostProcessor.AutoWire.Spec.Skip(),                 // Feet
                DynesticPostProcessor.AutoWire.Spec.Skip(),                 // Door
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<20"),          // DrillToolNr
                DynesticPostProcessor.AutoWire.Spec.Int("0<0<20"),          // RouterToolNr
            });
        }
    }
}
