using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace DynesticPostProcessor.Components.Nesting
{
    public class HopPartComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private static readonly Color _defaultColor = Color.FromArgb(100, 149, 237);
        private Curve _outlineCurve = null;
        private Line _grainArrow = Line.Unset;
        private Color _drawColor = Color.FromArgb(100, 149, 237);

        public HopPartComponent()
            : base("HopPart", "HopPart",
                "Bundles a closed part outline curve with operation lines into a single part object for OpenNest nesting. Supports grain direction and coloured preview.",
                "DYNESTIC", "Nesting")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Index 0 — optional dict from HopKorpus.panels (contains outline, operationLines, grainDir, panelName)
            pManager.AddGenericParameter("PartDict", "dict",
                "Panel dictionary from HopKorpus 'panels' output. When connected, all other inputs are ignored.",
                GH_ParamAccess.item);
            pManager[0].Optional = true;

            // Index 1
            pManager.AddCurveParameter("Outline", "outline",
                "Closed curve defining the part boundary. Used when PartDict is not connected.",
                GH_ParamAccess.item);
            pManager[1].Optional = true;

            // Index 2
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops macro strings from operation components. Used when PartDict is not connected.",
                GH_ParamAccess.list);
            pManager[2].Optional = true;

            // Index 3
            pManager.AddNumberParameter("GrainAngle", "grainAngle",
                "Grain direction angle in degrees. 0 = along X-axis, 90 = along Y-axis. Default 0.",
                GH_ParamAccess.item, 0.0);
            pManager[3].Optional = true;

            // Index 4
            pManager.AddColourParameter("Colour", "colour",
                "Preview colour for the part outline in the Rhino viewport. Default cornflower blue.",
                GH_ParamAccess.item, Color.FromArgb(100, 149, 237));
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Part", "part",
                "Part dictionary (GH_ObjectWrapper). Contains outline, operationLines, grainDir. Wire into HopSheetExport Parts.",
                GH_ParamAccess.item);

            pManager.AddCurveParameter("Outline", "outline",
                "Flat outline curve of the part. Wire into OpenNest Geo input.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // ---------------------------------------------------------------
            // 1. DEFAULTS -- clear preview fields before guards
            // ---------------------------------------------------------------
            _outlineCurve = null;
            _grainArrow = Line.Unset;

            Curve outline = null;
            List<string> operationLines = new List<string>();
            double grainAngle = 0.0;
            Color colour = Color.Empty;
            Vector3d grainDir = new Vector3d(1, 0, 0);
            List<List<string>> opLineGroups = null;
            string panelName = "";

            // ---------------------------------------------------------------
            // 2. Try PartDict input first (index 0)
            // ---------------------------------------------------------------
            GH_ObjectWrapper dictWrap = null;
            DA.GetData(0, ref dictWrap);
            var partDict = dictWrap?.Value as Dictionary<string, object>;

            if (partDict != null)
            {
                // Extract from HopKorpus panel dictionary
                if (partDict.ContainsKey("outline"))
                    outline = partDict["outline"] as Curve;
                if (partDict.ContainsKey("grainDir"))
                    grainDir = (Vector3d)partDict["grainDir"];
                if (partDict.ContainsKey("panelName"))
                    panelName = partDict["panelName"] as string ?? "";
                if (partDict.ContainsKey("operationLines"))
                    opLineGroups = partDict["operationLines"] as List<List<string>>;

                // grainAngle from grainDir vector (for remark only)
                grainAngle = Math.Atan2(grainDir.Y, grainDir.X) * 180.0 / Math.PI;
            }
            else
            {
                // Manual inputs (indices 1-4)
                DA.GetData(1, ref outline);
                DA.GetDataList(2, operationLines);
                DA.GetData(3, ref grainAngle);
                if (grainAngle < 0 || grainAngle > 360) grainAngle = 0;
                double rad0 = grainAngle * Math.PI / 180.0;
                grainDir = new Vector3d(Math.Cos(rad0), Math.Sin(rad0), 0);
            }

            DA.GetData(4, ref colour);
            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // 3. GUARDS
            // ---------------------------------------------------------------
            if (outline == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "HopPart: no outline — connect PartDict (from HopKorpus) or Outline curve");
                return;
            }
            if (!outline.IsClosed)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HopPart: outline curve must be closed");
                return;
            }

            // ---------------------------------------------------------------
            // 4. OPERATION LINES
            // ---------------------------------------------------------------
            if (opLineGroups == null)
            {
                opLineGroups = new List<List<string>>();
                opLineGroups.Add(new List<string>(operationLines));
            }

            // ---------------------------------------------------------------
            // 5. BUILD OUTPUT DICTIONARY
            // ---------------------------------------------------------------
            var dict = new Dictionary<string, object>();
            dict["outline"]         = outline;
            dict["operationLines"]  = opLineGroups;
            dict["grainDir"]        = grainDir;
            if (panelName.Length > 0)
                dict["panelName"] = panelName;

            // ---------------------------------------------------------------
            // 6. PREVIEW -- outline curve + grain arrow (per D-12)
            // ---------------------------------------------------------------
            _outlineCurve = outline;

            // Grain arrow: 20mm arrow from centroid along grain direction
            var amp = AreaMassProperties.Compute(outline);
            if (amp != null)
            {
                Point3d centroid = amp.Centroid;
                double arrowLen = 20.0;
                Point3d arrowEnd = centroid + grainDir * arrowLen;
                _grainArrow = new Line(centroid, arrowEnd);
            }

            // ---------------------------------------------------------------
            // 7. OUTPUT
            // ---------------------------------------------------------------
            DA.SetData(0, new GH_ObjectWrapper(dict));
            DA.SetData(1, outline); // raw curve for OpenNest Geo

            int totalOps = 0;
            foreach (var g in opLineGroups) totalOps += g.Count;
            string label = panelName.Length > 0 ? panelName + ": " : "";
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopPart: " + label + totalOps + " operation lines, grain=" + grainAngle.ToString("F1", CultureInfo.InvariantCulture) + "°");
        }

        // ---------------------------------------------------------------
        // PREVIEW OVERRIDES
        // ---------------------------------------------------------------
        public override BoundingBox ClippingBox
        {
            get
            {
                BoundingBox bb = BoundingBox.Empty;
                if (_outlineCurve != null) bb.Union(_outlineCurve.GetBoundingBox(true));
                if (_grainArrow.IsValid) { bb.Union(_grainArrow.From); bb.Union(_grainArrow.To); }
                return bb;
            }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_outlineCurve != null)
                args.Display.DrawCurve(_outlineCurve, _drawColor, 2);

            if (_grainArrow.IsValid)
            {
                args.Display.DrawArrow(
                    _grainArrow,
                    Color.FromArgb(80, 80, 80), 0.0, 3.0);
            }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            // Required override -- no mesh preview for HopPart
        }

        protected override System.Drawing.Bitmap Icon => IconHelper.Load("HopPart");

        public override Guid ComponentGuid => new Guid("2161f748-9651-46df-9c41-19da014f537b");

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.Skip(),         // PartDict
                DynesticPostProcessor.AutoWire.Spec.Curve(),        // Outline
                DynesticPostProcessor.AutoWire.Spec.Skip(),         // OperationLines
                DynesticPostProcessor.AutoWire.Spec.Float("0<0<360"), // GrainAngle
                DynesticPostProcessor.AutoWire.Spec.Skip(),         // Colour
            });
        }
    }
}
