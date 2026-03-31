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
        private List<Brep> _previewBreps = null;
        private Color _drawColor = Color.FromArgb(180, 140, 100);

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
        }

        // -----------------------------------------------------------------
        // CLEAR DATA (prevent ghost geometry on disconnect)
        // -----------------------------------------------------------------
        public override void ClearData()
        {
            base.ClearData();
            _previewBreps = null;
        }

        // -----------------------------------------------------------------
        // SOLVE
        // -----------------------------------------------------------------
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 0. Clear preview state
            _previewBreps = null;

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

            // -- Process Back (adjust BackPanel geometry) --
            if (backDict != null)
            {
                int backType = Convert.ToInt32(backDict["type"]);
                double backThickness = Convert.ToDouble(backDict["thickness"]);
                double backOffset = Convert.ToDouble(backDict["offset"]);
                double rabbet = Convert.ToDouble(backDict["rabbet"]); // 10.0

                if (backType == 0) // SurfaceMounted: back panel placed against back face, no groove
                {
                    // Recreate rueckwand with actual back thickness (was using MS as placeholder)
                    rueckwand = new KorpusPanel("BackPanel", innerB, innerH, backThickness);
                    // Assembled: flush with back face (Y = T - backThickness)
                    rueckwand.AssembledTransform = Transform.PlaneToPlane(Plane.WorldXY,
                        new Plane(new Point3d(MS, T - backThickness, MS), new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)));
                }
                else if (backType == 1) // Rabbeted: back panel fits inside rabbet cuts
                {
                    // Back panel is smaller (sits in rabbet in all 4 outer panels)
                    double bpW = innerB - 2 * rabbet;
                    double bpH = innerH - 2 * rabbet;
                    rueckwand = new KorpusPanel("BackPanel", Math.Max(1, bpW), Math.Max(1, bpH), backThickness);
                    rueckwand.AssembledTransform = Transform.PlaneToPlane(Plane.WorldXY,
                        new Plane(new Point3d(MS + rabbet, T - backThickness, MS + rabbet), new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)));
                    // Note: rabbet routing ops in outer panels would be added here in a future phase
                }
                else // Grooved (type == 2): back panel same size, fits in groove at backOffset from back
                {
                    rueckwand = new KorpusPanel("BackPanel", innerB, innerH, backThickness);
                    double grooveY = T - backOffset - backThickness;
                    rueckwand.AssembledTransform = Transform.PlaneToPlane(Plane.WorldXY,
                        new Plane(new Point3d(MS, grooveY, MS), new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)));
                    // Note: groove routing ops in side/top/bottom panels would be added in a future phase
                }
                rueckwand.AssembledOrigin = rueckwand.FlatBrep.GetBoundingBox(false).Center;
            }

            // -- Build panels list (after back processing) --
            var panels = new List<KorpusPanel> { boden, deckel, linkeSeite, rechteSeite, rueckwand };
            var extraPanels = new List<KorpusPanel>(); // shelves, doors added here

            // -- Process Connectors --
            if (connDict != null)
            {
                double drillDia   = Convert.ToDouble(connDict["drillDiameter"]);
                double drillDepth = Convert.ToDouble(connDict["drillDepth"]);
                bool autoCount    = Convert.ToBoolean(connDict["autoCount"]);
                int connCount     = Convert.ToInt32(connDict["count"]);
                bool isRouting    = Convert.ToBoolean(connDict["isRouting"]);
                double grid       = Convert.ToDouble(connDict["grid"]);   // 32mm
                double edge       = Convert.ToDouble(connDict["edge"]);   // 37mm

                if (!isRouting && drillDia > 0)
                {
                    // Auto-count by depth
                    if (autoCount)
                        connCount = T <= 300 ? 2 : (T <= 500 ? 3 : 4);

                    // Generate connector positions along depth (T dimension)
                    var xPositions = new List<double>();
                    for (int i = 0; i < connCount; i++)
                    {
                        double xPos = edge + i * grid;
                        if (xPos < T - edge + 0.1) xPositions.Add(xPos);
                    }
                    if (xPositions.Count == 0) xPositions.Add(edge);

                    // LeftSide panel (width=T along X, height=H along Y):
                    // Holes at bottom zone (Y = MS/2) and top zone (Y = H - MS/2)
                    foreach (double xp in xPositions)
                    {
                        linkeSeite.AddDrillGroup(xp, MS / 2.0, drillDia, drillDepth, drillToolNr);
                        linkeSeite.AddDrillGroup(xp, H - MS / 2.0, drillDia, drillDepth, drillToolNr);
                    }
                    // RightSide: same pattern
                    foreach (double xp in xPositions)
                    {
                        rechteSeite.AddDrillGroup(xp, MS / 2.0, drillDia, drillDepth, drillToolNr);
                        rechteSeite.AddDrillGroup(xp, H - MS / 2.0, drillDia, drillDepth, drillToolNr);
                    }
                    // Bottom panel (width=innerB along X, height=T along Y):
                    // Holes at left zone (X = MS/2) and right zone (X = innerB - MS/2)
                    foreach (double xp in xPositions)
                    {
                        boden.AddDrillGroup(MS / 2.0, xp, drillDia, drillDepth, drillToolNr);
                        boden.AddDrillGroup(innerB - MS / 2.0, xp, drillDia, drillDepth, drillToolNr);
                    }
                    // Top panel: same as bottom
                    foreach (double xp in xPositions)
                    {
                        deckel.AddDrillGroup(MS / 2.0, xp, drillDia, drillDepth, drillToolNr);
                        deckel.AddDrillGroup(innerB - MS / 2.0, xp, drillDia, drillDepth, drillToolNr);
                    }
                }
            }

            // -- Process Shelves --
            if (shelvesDict != null)
            {
                bool fixedShelf   = Convert.ToBoolean(shelvesDict["fixedShelf"]);
                double fixedH     = Convert.ToDouble(shelvesDict["fixedHeight"]);
                int adjCount      = Convert.ToInt32(shelvesDict["adjustableCount"]);
                double s32raster  = Convert.ToDouble(shelvesDict["s32_raster"]);  // 32
                double s32edge    = Convert.ToDouble(shelvesDict["s32_edge"]);    // 37
                double s32dia     = Convert.ToDouble(shelvesDict["s32_drill_d"]); // 5
                double s32depth   = Convert.ToDouble(shelvesDict["s32_depth"]);   // 13

                // Fixed shelf: add as extra panel
                if (fixedShelf && fixedH > 0 && fixedH < innerH)
                {
                    double shelfDepth = T - 2 * MS; // fits between front opening and back panel
                    var fixedShelfPanel = new KorpusPanel("FixedShelf", innerB, Math.Max(1, shelfDepth), MS);
                    fixedShelfPanel.AssembledTransform = Transform.PlaneToPlane(Plane.WorldXY,
                        new Plane(new Point3d(MS, MS, MS + fixedH), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0)));
                    extraPanels.Add(fixedShelfPanel);
                }

                // Adjustable shelf holes: 2 rows (front + back) in both side panels
                if (adjCount > 0)
                {
                    // Determine available height range for holes (avoid connector zone)
                    double yStart = s32edge;
                    double yEnd   = H - s32edge;
                    // Generate full rows from yStart to yEnd in 32mm increments
                    var yPositions = new List<double>();
                    for (double yp = yStart; yp <= yEnd + 0.1; yp += s32raster)
                        yPositions.Add(yp);

                    // 2 columns: front (X = s32edge) and back (X = T - s32edge)
                    // LeftSide panel: width=T (X is depth direction), height=H (Y is height direction)
                    foreach (double yp in yPositions)
                    {
                        linkeSeite.AddDrillGroup(s32edge,     yp, s32dia, s32depth, drillToolNr);
                        linkeSeite.AddDrillGroup(T - s32edge, yp, s32dia, s32depth, drillToolNr);
                    }
                    // RightSide: same
                    foreach (double yp in yPositions)
                    {
                        rechteSeite.AddDrillGroup(s32edge,     yp, s32dia, s32depth, drillToolNr);
                        rechteSeite.AddDrillGroup(T - s32edge, yp, s32dia, s32depth, drillToolNr);
                    }
                }
            }

            // -- Process Feet --
            if (feetDict != null)
            {
                double footDia    = Convert.ToDouble(feetDict["drillDiameter"]);
                double footDepth  = Convert.ToDouble(feetDict["drillDepth"]);
                double edgeOffset = Convert.ToDouble(feetDict["edgeOffset"]);

                // Bottom panel: width=innerB along X, height=T along Y
                // 4 feet for W <= 800, 6 feet for W > 800
                bool sixFeet = B > 800;

                // 4 corner positions (in flat panel local coords):
                boden.AddDrillGroup(edgeOffset,         edgeOffset,         footDia, footDepth, drillToolNr);
                boden.AddDrillGroup(innerB - edgeOffset, edgeOffset,        footDia, footDepth, drillToolNr);
                boden.AddDrillGroup(edgeOffset,          T - edgeOffset,    footDia, footDepth, drillToolNr);
                boden.AddDrillGroup(innerB - edgeOffset, T - edgeOffset,    footDia, footDepth, drillToolNr);

                if (sixFeet)
                {
                    // 2 centre feet
                    boden.AddDrillGroup(innerB / 2.0, edgeOffset,     footDia, footDepth, drillToolNr);
                    boden.AddDrillGroup(innerB / 2.0, T - edgeOffset, footDia, footDepth, drillToolNr);
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
                double doorW, doorH;
                if (overlay == 0) // FullOverlay
                {
                    doorW = (innerB + 2 * MS - 2 * gap) / doorCount;
                    doorH = H - 2 * gap;
                }
                else if (overlay == 1) // HalfOverlay
                {
                    doorW = (innerB + MS - 2 * gap) / doorCount;
                    doorH = H - 2 * gap;
                }
                else // Inset
                {
                    doorW = (innerB - 2 * gap) / doorCount;
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

                    extraPanels.Add(doorPanel);
                }
            }

            // ---------------------------------------------------------------
            // 6. Build KorpusData dictionary
            // ---------------------------------------------------------------
            var allPanels = new List<KorpusPanel> { boden, deckel, linkeSeite, rechteSeite, rueckwand };
            allPanels.AddRange(extraPanels);

            var korpusDict = new Dictionary<string, object>();
            korpusDict["W"] = B;
            korpusDict["H"] = H;
            korpusDict["D"] = T;
            korpusDict["thickness"] = MS;
            korpusDict["type"] = cabinetType;
            korpusDict["panels"] = allPanels;

            // 6b. Build 3D preview -- transform each flat panel to assembled position, then extrude by thickness
            _previewBreps = new List<Brep>();
            foreach (var panel in allPanels)
            {
                Brep assembled = panel.FlatBrep.DuplicateBrep();
                assembled.Transform(panel.AssembledTransform);

                // Get the face normal of the assembled planar Brep
                Vector3d normal = assembled.Faces[0].NormalAt(
                    assembled.Faces[0].Domain(0).Mid,
                    assembled.Faces[0].Domain(1).Mid);

                // Create extrusion path along the normal by thickness
                Point3d extStart = Point3d.Origin;
                Point3d extEnd = extStart + normal * panel.Thickness;
                LineCurve extPath = new LineCurve(new Line(extStart, extEnd));

                Brep solid = assembled.Faces[0].CreateExtrusion(extPath, true);
                if (solid != null)
                {
                    _previewBreps.Add(solid);
                }
            }

            // 7. Output 1: KorpusData
            DA.SetData(0, new GH_ObjectWrapper(korpusDict));

            // 8. Output 2: Panels list (HopPart-compatible dictionaries)
            var panelOutputs = new List<GH_ObjectWrapper>();
            foreach (var p in allPanels)
                panelOutputs.Add(new GH_ObjectWrapper(p.ToPartDict()));
            DA.SetDataList(1, panelOutputs);

            // 9. Remark message
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
                {
                    foreach (var brep in _previewBreps)
                        bb.Union(brep.GetBoundingBox(true));
                }
                return bb;
            }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (_previewBreps != null)
            {
                var mat = new Rhino.Display.DisplayMaterial(_drawColor);
                mat.Transparency = 0.3;
                foreach (var brep in _previewBreps)
                    args.Display.DrawBrepShaded(brep, mat);
            }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_previewBreps != null)
            {
                foreach (var brep in _previewBreps)
                    args.Display.DrawBrepWires(brep, _drawColor, 1);
            }
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
            });
        }
    }
}
