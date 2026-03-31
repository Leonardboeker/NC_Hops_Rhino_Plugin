// HopSheet -- Sheet dimension extractor for DYNESTIC CNC
// Inputs:  geometry (Item, GeometryBase)
// Outputs: dx, dy, dz
//
// Accepts a closed curve (flat plate outline) OR a solid Brep (3D part/plate model).
// Extracts dx/dy/dz from the World-XY BoundingBox of the input geometry.
// For a 3D Brep: dz = Z extent (plate thickness derived automatically).
// For a flat curve: dz = 0 (user must set thickness via HopExport directly if needed).
// Outputs wire directly into HopExport dx/dy/dz inputs.
// Shows a grey sheet footprint rectangle in the GH viewport.
//
// GH canvas setup:
//   Input  geometry: Type Hint -> GeometryBase
//   Output dx:       rename to "dx"
//   Output dy:       rename to "dy"
//   Output dz:       rename to "dz"   (matches HopExport input name)
//   Component display name: "HopSheet"

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
  // ---------------------------------------------------------------
  // PREVIEW FIELDS
  // ---------------------------------------------------------------
  private Curve _sheetOutline = null;

  public override BoundingBox ClippingBox
  {
    get { return _sheetOutline != null ? _sheetOutline.GetBoundingBox(true) : BoundingBox.Empty; }
  }

  public override void DrawViewportWires(IGH_PreviewArgs args)
  {
    if (_sheetOutline != null)
      args.Display.DrawCurve(_sheetOutline, Color.FromArgb(180, 180, 180), 2);
  }

  private void RunScript(GeometryBase geometry,
    ref object dx, ref object dy, ref object dz)
  {
    // ---------------------------------------------------------------
    // 1. DEFAULTS
    // ---------------------------------------------------------------
    dx  = 0.0;
    dy  = 0.0;
    dz  = 0.0;
    _sheetOutline = null;

    // ---------------------------------------------------------------
    // 2. NULL GUARD — Warning (not Error) so downstream still computes
    // ---------------------------------------------------------------
    if (geometry == null)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        "HopSheet: no geometry connected");
      return;
    }

    // ---------------------------------------------------------------
    // 3. BOUNDING BOX EXTRACTION
    //    Curve  -> closed plate outline -> bbox of curve
    //    Brep   -> full solid or surface -> bbox of entire Brep
    //    For a solid Brep, brep.GetBoundingBox(true) is correct and gives all 3 spans.
    //    Do NOT use Faces[0] only -- that gives the bbox of one face, not the whole part.
    // ---------------------------------------------------------------
    BoundingBox bbox = BoundingBox.Empty;

    Curve crv = geometry as Curve;
    if (crv != null)
    {
      if (!crv.IsClosed)
      {
        this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
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
        // Use full-Brep bbox -- correct for both solids and single-face surfaces
        bbox = brep.GetBoundingBox(true);
      }
    }

    if (!bbox.IsValid)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
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
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        string.Format(CultureInfo.InvariantCulture,
          "HopSheet: dimensions seem small (dx={0:F1} dy={1:F1}) -- check model units are mm",
          detDx, detDy));

    // ---------------------------------------------------------------
    // 6. REMARK WITH ALL THREE VALUES FOR USER VERIFICATION
    // ---------------------------------------------------------------
    this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
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
    dx = detDx;
    dy = detDy;
    dz = detDz;
  }
}
