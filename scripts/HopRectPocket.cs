// HopRectPocket -- Rectangular pocket operation for DYNESTIC CNC
// Inputs: rectCurve (Item), cornerRadius (Item), angle (Item),
//         depth (Item), stepdown (Item),
//         toolNr (Item), colour (Item)
// Outputs: operationLines
//
// Extracts center and dimensions from the rectangle curve bounding box.
// Emits WZF tool call + CALL _RechteckTasche_V5 macro for HopExport.
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
    Curve rectCurve, double cornerRadius, double angle,
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
    if (rectCurve == null)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "No rectangle curve connected");
      return;
    }

    if (toolNr <= 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "toolNr is required and must be > 0");
      return;
    }

    // ---------------------------------------------------------------
    // 3. INPUT DEFAULTS
    // ---------------------------------------------------------------
    if (cornerRadius < 0) cornerRadius = 0;
    if (depth <= 0) depth = 1.0;

    // ---------------------------------------------------------------
    // 4. EXTRACT DIMENSIONS from BoundingBox
    // ---------------------------------------------------------------
    BoundingBox bb = rectCurve.GetBoundingBox(true);
    double cx = (bb.Min.X + bb.Max.X) / 2.0;
    double cy = (bb.Min.Y + bb.Max.Y) / 2.0;
    double width  = bb.Max.X - bb.Min.X;
    double height = bb.Max.Y - bb.Min.Y;

    // PREVIEW: box from bounding rect at surface down to -depth
    double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
    double topZ  = bb.Max.Z;
    double botZ  = topZ - Math.Abs(depth);

    // Build rotated box: create base rect at topZ, extrude down
    double previewZ = topZ;
    Rectangle3d previewBounds = new Rectangle3d(
      new Plane(new Point3d(cx, cy, previewZ), Vector3d.XAxis, Vector3d.YAxis),
      new Interval(-width / 2.0, width / 2.0),
      new Interval(-height / 2.0, height / 2.0));
    Curve baseCurve = previewBounds.ToNurbsCurve();

    if (cornerRadius > 0)
    {
      Curve filleted = Curve.CreateFilletCornersCurve(baseCurve, cornerRadius, 1e-6, 1e-6);
      if (filleted != null) baseCurve = filleted;
    }

    if (angle != 0)
    {
      double angleRad = angle * Math.PI / 180.0;
      Point3d centerPoint = new Point3d(cx, cy, previewZ);
      baseCurve.Transform(Transform.Rotation(angleRad, Vector3d.ZAxis, centerPoint));
    }

    // Extrude the closed base curve downward
    if (baseCurve.IsClosed)
    {
      Vector3d extDir = new Vector3d(0, 0, -(topZ - botZ));
      Surface extSrf = Surface.CreateExtrusion(baseCurve, extDir);
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

    // PREVIEW: approach line from safeZ to bottom-left corner
    double safeZ = rectCurve.GetBoundingBox(true).Max.Z + 20.0;
    Point3d startPt = new Point3d(bb.Min.X, bb.Min.Y, topZ);
    _approachLine = new Line(new Point3d(startPt.X, startPt.Y, safeZ), startPt);

    // ---------------------------------------------------------------
    // 5. BUILD TOOL CALL (first line)
    // ---------------------------------------------------------------
    List<string> lines = new List<string>();
    string toolCall = toolType + " (" + toolNr.ToString()
      + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
      + ",_VA,_SD,0,'')";
    lines.Add(toolCall);

    // ---------------------------------------------------------------
    // 6. BUILD CALL MACRO
    // ---------------------------------------------------------------
    double negDepth   = -Math.Abs(depth);
    double zustellung = (stepdown > 0) ? stepdown : 0;

    lines.Add("CALL _RechteckTasche_V5(VAL "
      + "x_Mitte:=" + cx.ToString(CultureInfo.InvariantCulture) + ","
      + "Y_Mitte:=" + cy.ToString(CultureInfo.InvariantCulture) + ","
      + "Taschenlaenge:=" + width.ToString(CultureInfo.InvariantCulture) + ","
      + "Taschenbreite:=" + height.ToString(CultureInfo.InvariantCulture) + ","
      + "Radius:=" + cornerRadius.ToString(CultureInfo.InvariantCulture) + ","
      + "Winkel:=" + angle.ToString(CultureInfo.InvariantCulture) + ","
      + "Tiefe:=" + negDepth.ToString(CultureInfo.InvariantCulture) + ","
      + "Zustellung:=" + zustellung.ToString(CultureInfo.InvariantCulture) + ","
      + "AB:=2,ABF:=_ANF,Interpol:=1,umkehren:=0,esxy:=0,esmd:=0,laser:=0)");

    // ---------------------------------------------------------------
    // 7. OUTPUT
    // ---------------------------------------------------------------
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark,
      "RectPocket: " + width.ToString(CultureInfo.InvariantCulture)
      + " x " + height.ToString(CultureInfo.InvariantCulture)
      + " at (" + cx.ToString(CultureInfo.InvariantCulture)
      + ", " + cy.ToString(CultureInfo.InvariantCulture) + ")");

    operationLines = lines;
  }
}
