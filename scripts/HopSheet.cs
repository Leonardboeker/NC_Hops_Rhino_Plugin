// HopSheet -- Sheet dimension extractor for DYNESTIC CNC
// Inputs:  geometry (Item, GeometryBase), dz (Item, double)
// Outputs: dx, dy, dzOut
//
// Accepts a closed curve OR a planar surface (single-face Brep) drawn in Rhino.
// Extracts dx/dy from the World-XY BoundingBox of the input geometry.
// dz (material thickness) is always a manual slider input — not derived from geometry.
// Outputs wire directly into HopExport dx/dy/dz inputs.
// Shows a grey sheet outline rectangle in the GH viewport.
//
// GH canvas setup:
//   Input  geometry: Type Hint -> GeometryBase
//   Input  dz:       Type Hint -> Number (double)
//   Output dx:       rename to "dx"
//   Output dy:       rename to "dy"
//   Output dzOut:    rename to "dz"   (matches HopExport input name)
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
      args.Display.DrawCurve(_sheetOutline, Color.FromArgb(180, 180, 180), 1);
  }

  private void RunScript(GeometryBase geometry, double dz,
    ref object dx, ref object dy, ref object dzOut)
  {
    // ---------------------------------------------------------------
    // 1. DEFAULTS — downstream gets these if guards trigger
    // ---------------------------------------------------------------
    dx          = 0.0;
    dy          = 0.0;
    dzOut       = dz;
    _sheetOutline = null;

    // ---------------------------------------------------------------
    // 2. NULL GUARD — GH Warning (not Error), allows downstream to compute (per D-05)
    // ---------------------------------------------------------------
    if (geometry == null)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        "HopSheet: no geometry connected");
      return;
    }

    // ---------------------------------------------------------------
    // 3. BOUNDING BOX EXTRACTION — branch on Curve vs Brep (per D-01, D-02, D-03)
    // ---------------------------------------------------------------
    BoundingBox bbox = BoundingBox.Empty;

    // Branch A: Curve input (per D-02)
    Curve crv = geometry as Curve;
    if (crv != null)
    {
      if (!crv.IsClosed)
      {
        this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
          "HopSheet: curve must be closed");
        return;
      }
      bbox = crv.GetBoundingBox(true);  // World-axis-aligned, accurate
    }
    else
    {
      // Branch B: Brep/surface input — surfaces arrive as single-face Breps (per D-03)
      // IMPORTANT: do NOT check 'is BrepFace' directly — surfaces arrive wrapped in a Brep
      Brep brep = geometry as Brep;
      if (brep != null && brep.IsValid && brep.Faces.Count > 0)
      {
        // CRITICAL: face.GetBoundingBox(true) returns UNTRIMMED surface bounds — BUG in RhinoCommon
        // Always use DuplicateFace(false).GetBoundingBox(true) for correct trimmed-face bbox
        Brep singleFace = brep.Faces[0].DuplicateFace(false);
        bbox = singleFace.GetBoundingBox(true);
      }
    }

    // ---------------------------------------------------------------
    // 4. UNSUPPORTED GEOMETRY FALLTHROUGH — covers Mesh, Point, empty Brep, etc.
    // ---------------------------------------------------------------
    if (!bbox.IsValid)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        "HopSheet: unsupported geometry type — use a closed curve or planar surface");
      return;
    }

    // ---------------------------------------------------------------
    // 5. EXTRACT DIMENSIONS — World-XY BoundingBox spans (per D-08)
    // ---------------------------------------------------------------
    double detDx = bbox.Max.X - bbox.Min.X;  // World-X span
    double detDy = bbox.Max.Y - bbox.Min.Y;  // World-Y span

    // ---------------------------------------------------------------
    // 6. MINIMUM SIZE WARNING — catch wrong model units (Claude's discretion)
    // ---------------------------------------------------------------
    if (detDx < 10.0 || detDy < 10.0)
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        string.Format(CultureInfo.InvariantCulture,
          "HopSheet: sheet dimensions seem small — dx={0:F1}mm dy={1:F1}mm — check model units",
          detDx, detDy));

    // ---------------------------------------------------------------
    // 7. REMARK WITH DETECTED VALUES — user verification (per D-09)
    // ---------------------------------------------------------------
    this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
      string.Format(CultureInfo.InvariantCulture,
        "HopSheet: dx={0:F1}mm dy={1:F1}mm (World-XY bbox — rotate geometry to align if needed)",
        detDx, detDy));

    // ---------------------------------------------------------------
    // 8. BUILD SHEET OUTLINE RECTANGLE FOR VIEWPORT PREVIEW (per D-07)
    // ---------------------------------------------------------------
    var rect = new Rectangle3d(Plane.WorldXY,
      new Interval(bbox.Min.X, bbox.Max.X),
      new Interval(bbox.Min.Y, bbox.Max.Y));
    _sheetOutline = rect.ToNurbsCurve();

    // ---------------------------------------------------------------
    // 9. OUTPUTS — match HopExport's dx/dy/dz input types exactly (per D-06)
    // ---------------------------------------------------------------
    dx    = detDx;
    dy    = detDy;
    dzOut = dz;
  }
}
