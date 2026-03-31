// HopCircPath -- Circular profile path operation for DYNESTIC CNC
// Inputs: center (Item), radius (Item), radiusCorr (Item),
//         depth (Item), stepdown (Item), angle (Item),
//         toolNr (Item), colour (Item)
// Outputs: operationLines
//
// radiusCorr: 1 = inside, -1 = outside, 0 = center
// Emits WZF tool call + CALL _Kreisbahn_V5 macro for HopExport.
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
  private static readonly Color _defaultColor = Color.LimeGreen;
  private Brep  _previewVolume = null;
  private Line  _approachLine  = Line.Unset;
  private Color _drawColor     = Color.LimeGreen;

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
    Point3d center, double radius, int radiusCorr,
    double depth, double stepdown, double angle,
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
    if (angle <= 0) angle = 360.0;

    // PREVIEW: cylinder at the circular path -- extrude the path circle downward by depth
    // The path circle sits at center.Z; extrude it downward to show the cutting volume.
    double previewZ = center.Z;
    Point3d circlePt = new Point3d(center.X, center.Y, previewZ);
    Plane cylPlane = new Plane(circlePt, Vector3d.ZAxis);
    Circle pathCircle = new Circle(cylPlane, radius);
    // Build a thin cylindrical shell to represent the path (not a solid pocket)
    // Extrude the circle curve (as a closed NurbsCurve) downward
    Curve pathCurve = pathCircle.ToNurbsCurve();
    double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
    Vector3d extDir = new Vector3d(0, 0, -Math.Abs(depth));
    Surface extSrf = Surface.CreateExtrusion(pathCurve, extDir);
    if (extSrf != null)
    {
      Brep extBrep = extSrf.ToBrep();
      if (extBrep != null)
      {
        Brep capped = extBrep.CapPlanarHoles(tol);
        _previewVolume = capped != null ? capped : extBrep;
      }
    }

    // PREVIEW: approach line from safeZ to the 3 o'clock entry point
    double safeZ = center.Z + 20.0;
    Point3d entryPt = new Point3d(center.X + radius, center.Y, previewZ);
    _approachLine = new Line(new Point3d(entryPt.X, entryPt.Y, safeZ), entryPt);

    // ---------------------------------------------------------------
    // 4. BUILD TOOL CALL + MACRO
    // ---------------------------------------------------------------
    List<string> lines = new List<string>();
    lines.Add(toolType + " (" + toolNr.ToString()
      + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
      + ",_VA,_SD,0,'')");

    double negDepth  = -Math.Abs(depth);
    double zuTiefe   = (stepdown > 0) ? stepdown : 0;

    lines.Add("CALL _Kreisbahn_V5(VAL "
      + "X_Mitte:=" + center.X.ToString(CultureInfo.InvariantCulture) + ","
      + "Y_Mitte:=" + center.Y.ToString(CultureInfo.InvariantCulture) + ","
      + "Tiefe:=" + negDepth.ToString(CultureInfo.InvariantCulture) + ","
      + "ZuTiefe:=" + zuTiefe.ToString(CultureInfo.InvariantCulture) + ","
      + "Radius:=" + radius.ToString(CultureInfo.InvariantCulture) + ","
      + "Radiuskorrektur:=" + radiusCorr.ToString() + ","
      + "AB:=1,Aufmass:=0,Bearb_umkehren:=1,"
      + "Winkel:=" + angle.ToString(CultureInfo.InvariantCulture) + ","
      + "ANF:=_ANF,ABF:=_ANF,Rampe:=1,Interpol:=0,esxy:=0,esmd:=0,laser:=0)");

    // ---------------------------------------------------------------
    // 5. OUTPUT
    // ---------------------------------------------------------------
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark,
      "CircPath: R=" + radius.ToString(CultureInfo.InvariantCulture)
      + " corr=" + radiusCorr.ToString()
      + " angle=" + angle.ToString(CultureInfo.InvariantCulture));

    operationLines = lines;
  }
}
