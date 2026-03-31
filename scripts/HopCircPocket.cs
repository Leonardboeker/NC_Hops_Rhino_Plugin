// HopCircPocket -- Circular pocket operation for DYNESTIC CNC
// Inputs: center (Item), radius (Item),
//         depth (Item), stepdown (Item),
//         toolNr (Item), colour (Item)
// Outputs: operationLines
//
// surfaceZ is derived from center.Z (the Z of the input point).
// Tiefe = surfaceZ - depth (absolute Z the machine cuts to).
// Emits WZF tool call + CALL _Kreistasche_V5 macro for HopExport.
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
  private static readonly Color _defaultColor = Color.Cyan;
  private Brep  _previewVolume = null;
  private Line  _approachLine  = Line.Unset;
  private Color _drawColor     = Color.Cyan;

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
    Point3d center, double radius,
    double depth, double stepdown,
    int toolNr,
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

    if (radius <= 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "radius must be > 0");
      return;
    }

    // ---------------------------------------------------------------
    // 3. INPUT DEFAULTS
    // ---------------------------------------------------------------
    if (depth <= 0) depth = 1.0;

    // PREVIEW: cylinder from center surface downward by depth
    Plane cylPlane = new Plane(center, Vector3d.ZAxis);
    Circle cylCircle = new Circle(cylPlane, radius);
    Cylinder cyl = new Cylinder(cylCircle, -Math.Abs(depth));
    Brep cylBrep = cyl.ToBrep(true, true);
    if (cylBrep != null)
      _previewVolume = cylBrep;

    // PREVIEW: approach line from safeZ to circle center
    double safeZ = center.Z + 20.0;
    _approachLine = new Line(new Point3d(center.X, center.Y, safeZ), center);

    // ---------------------------------------------------------------
    // 4. BUILD TOOL CALL + MACRO
    // ---------------------------------------------------------------
    List<string> lines = new List<string>();
    lines.Add(toolType + " (" + toolNr.ToString()
      + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
      + ",_VA,_SD,0,'')");

    // surfaceZ: Z of the input center point
    double surfaceZ   = center.Z;
    double cutZ       = surfaceZ - Math.Abs(depth);
    double zustellung = (stepdown > 0) ? stepdown : 0;

    lines.Add("CALL _Kreistasche_V5(VAL "
      + "X_Mitte:=" + center.X.ToString(CultureInfo.InvariantCulture) + ","
      + "Y_Mitte:=" + center.Y.ToString(CultureInfo.InvariantCulture) + ","
      + "Radius:=" + radius.ToString(CultureInfo.InvariantCulture) + ","
      + "Tiefe:=" + cutZ.ToString(CultureInfo.InvariantCulture) + ","
      + "Zustellung:=" + zustellung.ToString(CultureInfo.InvariantCulture) + ","
      + "AB:=2,ABF:=_ANF,Interpol:=0,umkehren:=0,esxy:=0,esmd:=0,laser:=0)");

    // ---------------------------------------------------------------
    // 5. OUTPUT
    // ---------------------------------------------------------------
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark,
      "CircPocket: R=" + radius.ToString(CultureInfo.InvariantCulture)
      + " at (" + center.X.ToString(CultureInfo.InvariantCulture)
      + ", " + center.Y.ToString(CultureInfo.InvariantCulture) + ")");

    operationLines = lines;
  }
}
