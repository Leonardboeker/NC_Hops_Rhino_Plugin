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
            pManager.AddCurveParameter("Outline", "outline",
                "Closed curve defining the part boundary used as the nesting shape for OpenNest. Must be planar and closed.",
                GH_ParamAccess.item);
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops macro strings from operation components (HopContour, HopDrill, etc.). All operations for this part.",
                GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("GrainAngle", "grainAngle",
                "Grain direction angle in degrees. 0 = along X-axis, 90 = along Y-axis. Displayed as arrow preview. Default 0.",
                GH_ParamAccess.item, 0.0);
            pManager[2].Optional = true;
            pManager.AddColourParameter("Colour", "colour",
                "Preview colour for the part outline in the Rhino viewport. Default cornflower blue.",
                GH_ParamAccess.item, Color.FromArgb(100, 149, 237));
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Part", "part",
                "Part dictionary wrapped in GH_ObjectWrapper. Contains outline curve, operation lines, and grain direction. Wire into OpenNest or HopSheetExport.",
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
            double grainAngle = 0.0;
            Color colour = Color.Empty;

            if (!DA.GetData(0, ref outline)) return;
            List<string> operationLines = new List<string>();
            DA.GetDataList(1, operationLines);
            DA.GetData(2, ref grainAngle);
            DA.GetData(3, ref colour);

            _drawColor = colour.IsEmpty ? _defaultColor : colour;

            // ---------------------------------------------------------------
            // 2. GUARDS
            // ---------------------------------------------------------------
            if (outline == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HopPart: no outline curve connected");
                return;
            }
            if (!outline.IsClosed)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HopPart: outline curve must be closed");
                return;
            }

            // ---------------------------------------------------------------
            // 3. INPUT DEFAULTS
            // ---------------------------------------------------------------
            if (operationLines == null) operationLines = new List<string>();
            if (grainAngle < 0 || grainAngle > 360) grainAngle = 0;

            // ---------------------------------------------------------------
            // 4. GRAIN DIRECTION VECTOR
            // ---------------------------------------------------------------
            double rad = grainAngle * Math.PI / 180.0;
            Vector3d grainDir = new Vector3d(Math.Cos(rad), Math.Sin(rad), 0);

            // ---------------------------------------------------------------
            // 5. BUILD DICTIONARY (per D-04, D-07 -- wire format)
            //    operationLines stored as List<List<string>> for grouping
            //    compatibility with downstream HopSheetExport.
            // ---------------------------------------------------------------
            var opLineGroups = new List<List<string>>();
            opLineGroups.Add(new List<string>(operationLines));

            var dict = new Dictionary<string, object>();
            dict["outline"] = outline;
            dict["operationLines"] = opLineGroups;
            dict["grainDir"] = grainDir;

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
            // 7. OUTPUT -- wrap in GH_ObjectWrapper for cross-assembly transport
            // ---------------------------------------------------------------
            DA.SetData(0, new GH_ObjectWrapper(dict));

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopPart: " + operationLines.Count + " operation lines bundled"
                + ", grain=" + grainAngle.ToString(CultureInfo.InvariantCulture) + " deg");
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

        protected override System.Drawing.Bitmap Icon => null; // real icon added when 08-02 completes

        public override Guid ComponentGuid => new Guid("2161f748-9651-46df-9c41-19da014f537b");
    }
}
