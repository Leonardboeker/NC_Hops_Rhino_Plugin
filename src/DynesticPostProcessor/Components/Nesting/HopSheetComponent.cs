using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;

namespace WallabyHop.Components.Nesting
{
    public class HopSheetComponent : GH_Component
    {
        // ---------------------------------------------------------------
        // PREVIEW FIELDS
        // ---------------------------------------------------------------
        private Curve _sheetOutline = null;

        public HopSheetComponent()
            : base("HopSheet", "HopSheet",
                "Extracts sheet dimensions (dx, dy, dz) from a closed curve or Brep geometry's bounding box. Wire outputs directly into HopExport dx/dy/dz inputs.",
                "Wallaby Hop", "Nesting")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "geometry",
                "Closed curve defining a flat plate outline, or a solid Brep representing a 3D plate. Dimensions are extracted from the World-XY bounding box.",
                GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("DX", "dx",
                "Sheet width in mm (bounding box X extent).",
                GH_ParamAccess.item);
            pManager.AddNumberParameter("DY", "dy",
                "Sheet height in mm (bounding box Y extent).",
                GH_ParamAccess.item);
            pManager.AddNumberParameter("DZ", "dz",
                "Sheet thickness in mm (bounding box Z extent). 0 for flat curves.",
                GH_ParamAccess.item);
            pManager.AddCurveParameter("SheetCurve", "sheetCurve",
                "Flat rectangle at Z=0 with sheet dimensions. Wire into OpenNest Sheets input.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // ---------------------------------------------------------------
            // 1. DEFAULTS
            // ---------------------------------------------------------------
            DA.SetData(0, 0.0);
            DA.SetData(1, 0.0);
            DA.SetData(2, 0.0);
            _sheetOutline = null;

            // ---------------------------------------------------------------
            // 2. NULL GUARD -- Warning (not Error) so downstream still computes
            // ---------------------------------------------------------------
            GeometryBase geometry = null;
            if (!DA.GetData(0, ref geometry)) return;

            if (geometry == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "HopSheet: no geometry connected");
                return;
            }

            // ---------------------------------------------------------------
            // 3. BOUNDING BOX EXTRACTION
            //    Curve  -> closed plate outline -> bbox of curve
            //    Brep   -> full solid or surface -> bbox of entire Brep
            // ---------------------------------------------------------------
            BoundingBox bbox = BoundingBox.Empty;

            Curve crv = geometry as Curve;
            if (crv != null)
            {
                if (!crv.IsClosed)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "HopSheet: curve must be closed to define a plate outline");
                    return;
                }
                bbox = crv.GetBoundingBox(true);
            }
            else
            {
                Brep brep = geometry as Brep;
                if (brep != null && brep.IsValid)
                {
                    bbox = brep.GetBoundingBox(true);
                }
            }

            if (!bbox.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "HopSheet: unsupported geometry type -- connect a closed curve or a Brep");
                return;
            }

            // ---------------------------------------------------------------
            // 4. EXTRACT ALL THREE DIMENSIONS FROM BBOX
            // ---------------------------------------------------------------
            double detDx = bbox.Max.X - bbox.Min.X;
            double detDy = bbox.Max.Y - bbox.Min.Y;
            double detDz = bbox.Max.Z - bbox.Min.Z;

            // ---------------------------------------------------------------
            // 5. SMALL-VALUE WARNING -- likely wrong model units
            // ---------------------------------------------------------------
            if (detDx < 10.0 || detDy < 10.0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    string.Format(CultureInfo.InvariantCulture,
                        "HopSheet: dimensions seem small (dx={0:F1} dy={1:F1}) -- check model units are mm",
                        detDx, detDy));

            // ---------------------------------------------------------------
            // 6. REMARK WITH ALL THREE VALUES FOR USER VERIFICATION
            // ---------------------------------------------------------------
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                string.Format(CultureInfo.InvariantCulture,
                    "HopSheet: dx={0:F1}mm  dy={1:F1}mm  dz={2:F1}mm",
                    detDx, detDy, detDz));

            // ---------------------------------------------------------------
            // 7. VIEWPORT PREVIEW -- grey footprint rectangle at Z=0
            // ---------------------------------------------------------------
            var rect = new Rectangle3d(Plane.WorldXY,
                new Interval(bbox.Min.X, bbox.Max.X),
                new Interval(bbox.Min.Y, bbox.Max.Y));
            _sheetOutline = rect.ToNurbsCurve();

            // ---------------------------------------------------------------
            // 8. OUTPUTS
            // ---------------------------------------------------------------
            DA.SetData(0, detDx);
            DA.SetData(1, detDy);
            DA.SetData(2, detDz);
            DA.SetData(3, _sheetOutline); // flat rectangle for OpenNest Sheets
        }

        // ---------------------------------------------------------------
        // PREVIEW OVERRIDES
        // ---------------------------------------------------------------
        public override BoundingBox ClippingBox
        {
            get { return _sheetOutline != null ? _sheetOutline.GetBoundingBox(true) : BoundingBox.Empty; }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_sheetOutline != null)
                args.Display.DrawCurve(_sheetOutline, Color.FromArgb(180, 180, 180), 2);
        }

        protected override System.Drawing.Bitmap Icon => IconHelper.Load("HopSheet");

        public override Guid ComponentGuid => new Guid("e9729b96-017f-4fc9-8e9f-5ba628a24ca7");

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Brep(),
            });
        }
    }
}
