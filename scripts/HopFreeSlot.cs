// HopFreeSlot -- Free slot operation for DYNESTIC CNC
// Inputs: p1 (Item), p2 (Item), slotWidth (Item),
//         depth (Item), toolNr (Item), colour (Item)
// Outputs: operationLines
//
// Takes two Point3d inputs (slot start and end) plus slot width.
// surfaceZ = Math.Max(p1.Z, p2.Z) — highest endpoint Z is the plate surface.
// Tiefe = surfaceZ - depth (absolute Z the machine cuts to).
// Emits WZF tool call + CALL _nuten_frei_v5 macro for HopExport.
// toolType and feedFactor are hardcoded (WZF / 1.0) -- handled at machine level.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // ---------------------------------------------------------------
  // PREVIEW FIELDS
  // ---------------------------------------------------------------
  private static readonly Color _defaultColor = Color.Orange;
  private Brep  _previewVolume = null;
  private Line  _approachLine  = Line.Unset;
  private Color _drawColor     = Color.Orange;

  public override BoundingBox ClippingBox
  {
    get
    {
      BoundingBox bb = BoundingBox.Empty;
      if (_previewVolume != null) bb.Union(_previewVolume.GetBoundingBox(true));
      if (_approachLine.IsValid) { bb.Union(_approachLine.From); bb.Union(_approachLine.To); }
      return bb;
    }
  }

  public override void DrawViewportMeshes(IGH_PreviewArgs args)
  {
    if (_previewVolume != null)
    {
      Rhino.Display.DisplayMaterial mat = new Rhino.Display.DisplayMaterial(_drawColor);
      mat.Transparency = 0.55;
      args.Display.DrawBrepShaded(_previewVolume, mat);
    }
  }

  public override void DrawViewportWires(IGH_PreviewArgs args)
  {
    if (_previewVolume != null)
      args.Display.DrawBrepWires(_previewVolume, _drawColor, 1);
    if (_approachLine.IsValid)
      args.Display.DrawPatternedLine(
        _approachLine.From, _approachLine.To,
        Color.FromArgb(140, 140, 140), unchecked((int)0xF0F0F0F0), 1);
  }

  private void RunScript(
    Point3d p1, Point3d p2, double slotWidth,
    double depth, int toolNr,
    Color colour,
    ref object operationLines)
  {
    // Hardcoded tool params -- handled at machine level
    string toolType   = "WZF";
    double feedFactor = 1.0;

    // PREVIEW: clear fields first (before guards) so disconnecting inputs wipes stale geometry
    _previewVolume = null;
    _approachLine  = Line.Unset;
    _drawColor     = colour.IsEmpty ? _defaultColor : colour;

    // ---------------------------------------------------------------
    // 1. DEFAULTS
    // ---------------------------------------------------------------
    operationLines = new List<string>();

    // ---------------------------------------------------------------
    // 2. GUARDS
    // ---------------------------------------------------------------
    if (toolNr <= 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "toolNr is required and must be > 0");
      return;
    }

    if (slotWidth <= 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "slotWidth must be > 0");
      return;
    }

    // ---------------------------------------------------------------
    // 3. INPUT DEFAULTS
    // ---------------------------------------------------------------
    if (depth <= 0) depth = 1.0;

    // PREVIEW: box along slot centerline at surface level, extruded downward by depth
    double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
    double topZ = Math.Max(p1.Z, p2.Z);
    Point3d a = new Point3d(p1.X, p1.Y, topZ);
    Point3d b = new Point3d(p2.X, p2.Y, topZ);

    Vector3d dir = b - a;
    if (dir.Length > 0.001)
    {
      dir.Unitize();
      // Perpendicular in XY
      Vector3d perp = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
      perp.Unitize();
      double halfW = slotWidth / 2.0;

      // Four corner points of slot rectangle at topZ
      Point3d c0 = a + perp * halfW;
      Point3d c1 = b + perp * halfW;
      Point3d c2 = b - perp * halfW;
      Point3d c3 = a - perp * halfW;

      // Build closed polyline as slot base
      Polyline slotPoly = new Polyline(new Point3d[] { c0, c1, c2, c3, c0 });
      Curve slotCurve = slotPoly.ToNurbsCurve();

      if (slotCurve != null && slotCurve.IsClosed)
      {
        Vector3d extDir = new Vector3d(0, 0, -Math.Abs(depth));
        Surface extSrf = Surface.CreateExtrusion(slotCurve, extDir);
        if (extSrf != null)
        {
          Brep extBrep = extSrf.ToBrep();
          if (extBrep != null)
          {
            Brep capped = extBrep.CapPlanarHoles(tol);
            _previewVolume = capped != null ? capped : extBrep;
          }
        }
      }
    }

    // PREVIEW: approach line from safeZ to p1
    double safeZVal = Math.Max(p1.Z, p2.Z) + 20.0;
    _approachLine = new Line(new Point3d(a.X, a.Y, safeZVal), a);

    // ---------------------------------------------------------------
    // 4. BUILD TOOL CALL + MACRO
    // ---------------------------------------------------------------
    List<string> lines = new List<string>();
    lines.Add(toolType + " (" + toolNr.ToString()
      + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
      + ",_VA,_SD,0,'')");

    // surfaceZ: highest Z of the two input points (already computed as topZ above)
    double cutZ = topZ - Math.Abs(depth);

    lines.Add("CALL _nuten_frei_v5(VAL "
      + "X1:=" + p1.X.ToString(CultureInfo.InvariantCulture) + ","
      + "Y1:=" + p1.Y.ToString(CultureInfo.InvariantCulture) + ","
      + "X2:=" + p2.X.ToString(CultureInfo.InvariantCulture) + ","
      + "Y2:=" + p2.Y.ToString(CultureInfo.InvariantCulture) + ","
      + "NB:=" + slotWidth.ToString(CultureInfo.InvariantCulture) + ","
      + "Tiefe:=" + cutZ.ToString(CultureInfo.InvariantCulture) + ","
      + "LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)");

    // ---------------------------------------------------------------
    // 5. OUTPUT
    // ---------------------------------------------------------------
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark,
      "FreeSlot: (" + p1.X.ToString(CultureInfo.InvariantCulture)
      + "," + p1.Y.ToString(CultureInfo.InvariantCulture)
      + ") to (" + p2.X.ToString(CultureInfo.InvariantCulture)
      + "," + p2.Y.ToString(CultureInfo.InvariantCulture)
      + ") W=" + slotWidth.ToString(CultureInfo.InvariantCulture));

    operationLines = lines;
  }
}
