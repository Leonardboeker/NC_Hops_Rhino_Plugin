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
        public HopKorpusComponent()
            : base("HopKorpus", "HopKorpus",
                "Parametric cabinet body generator. Produces 5 flat panels (Boden, Deckel, LinkeSeite, RechteSeite, Rueckwand, open front) from dimension sliders. Outputs KorpusData dictionary and individual panel objects for HopPart nesting.",
                "DYNESTIC", "Korpus")
        {
        }

        // -----------------------------------------------------------------
        // INPUTS
        // -----------------------------------------------------------------
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Breite", "B",
                "Cabinet width in mm (outer dimension).",
                GH_ParamAccess.item, 600.0);
            pManager[0].Optional = true;

            pManager.AddNumberParameter("Hoehe", "H",
                "Cabinet height in mm (outer dimension).",
                GH_ParamAccess.item, 720.0);
            pManager[1].Optional = true;

            pManager.AddNumberParameter("Tiefe", "T",
                "Cabinet depth in mm (outer dimension).",
                GH_ParamAccess.item, 560.0);
            pManager[2].Optional = true;

            pManager.AddNumberParameter("Materialstaerke", "MS",
                "Material thickness in mm. Typical: 16 or 19.",
                GH_ParamAccess.item, 19.0);
            pManager[3].Optional = true;

            pManager.AddTextParameter("KorpusTyp", "typ",
                "Cabinet type label (e.g. Unterschrank, Hochschrank). Label only, no structural effect.",
                GH_ParamAccess.item, "Korpus");
            pManager[4].Optional = true;
        }

        // -----------------------------------------------------------------
        // OUTPUTS
        // -----------------------------------------------------------------
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("KorpusData", "korpus",
                "Complete korpus dictionary wrapped in GH_ObjectWrapper. Contains B, H, T, thickness, type, and panel list. Wire into downstream Korpus components (HopKorpusRueckwand, HopVerbinder, etc.).",
                GH_ParamAccess.item);

            pManager.AddGenericParameter("Panels", "panels",
                "Individual flat panel dictionaries (GH_ObjectWrapper), one per panel. Each contains outline curve, operationLines, grainDir, panelName. Wire into HopPart for nesting.",
                GH_ParamAccess.list);
        }

        // -----------------------------------------------------------------
        // SOLVE
        // -----------------------------------------------------------------
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Get inputs with defaults
            double B = 600, H = 720, T = 560, MS = 19;
            string korpusTyp = "Korpus";

            DA.GetData(0, ref B);
            DA.GetData(1, ref H);
            DA.GetData(2, ref T);
            DA.GetData(3, ref MS);
            DA.GetData(4, ref korpusTyp);

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
                    "Materialstaerke invalid (must be > 0 and < half the smallest dimension)");
                return;
            }

            // 3. Calculate inner dimensions (butt-joint construction)
            double innerB = B - 2.0 * MS;  // between side panels
            double innerH = H - 2.0 * MS;  // between Boden and Deckel
            double innerT = T;              // depth stays same for butt-joint

            // 4. Create 5 KorpusPanel objects (NO front panel -- open front)
            var boden       = new KorpusPanel("Boden",       innerB, T, MS);
            var deckel      = new KorpusPanel("Deckel",      innerB, T, MS);
            var linkeSeite  = new KorpusPanel("LinkeSeite",  T,      H, MS);
            var rechteSeite = new KorpusPanel("RechteSeite", T,      H, MS);
            var rueckwand   = new KorpusPanel("Rueckwand",   innerB, innerH, MS);

            // 5. Compute assembled transforms (flat XY → 3D position)
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

            // 6. Build KorpusData dictionary
            var panels = new List<KorpusPanel> { boden, deckel, linkeSeite, rechteSeite, rueckwand };

            var korpusDict = new Dictionary<string, object>();
            korpusDict["B"] = B;
            korpusDict["H"] = H;
            korpusDict["T"] = T;
            korpusDict["thickness"] = MS;
            korpusDict["type"] = korpusTyp;
            korpusDict["panels"] = panels;

            // 7. Output 1: KorpusData
            DA.SetData(0, new GH_ObjectWrapper(korpusDict));

            // 8. Output 2: Panels list (HopPart-compatible dictionaries)
            var panelOutputs = new List<GH_ObjectWrapper>();
            foreach (var p in panels)
                panelOutputs.Add(new GH_ObjectWrapper(p.ToPartDict()));
            DA.SetDataList(1, panelOutputs);

            // 9. Remark message
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopKorpus: " + panels.Count + " panels, " + korpusTyp
                + " " + B + "x" + H + "x" + T + " MS=" + MS);
        }

        // -----------------------------------------------------------------
        // ICON + GUID
        // -----------------------------------------------------------------
        protected override Bitmap Icon => IconHelper.Load("HopKorpus");

        public override Guid ComponentGuid =>
            new Guid("a3b7c1d2-e4f5-6789-0abc-def123456789");

        // -----------------------------------------------------------------
        // AUTO-WIRE (5 inputs)
        // -----------------------------------------------------------------
        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.Float("100<600<2400"),  // B
                DynesticPostProcessor.AutoWire.Spec.Float("100<720<2400"),  // H
                DynesticPostProcessor.AutoWire.Spec.Float("100<560<800"),   // T
                DynesticPostProcessor.AutoWire.Spec.Float("8<19<38"),       // MS
                DynesticPostProcessor.AutoWire.Spec.Panel("Korpus"),        // typ
            });
        }
    }
}
