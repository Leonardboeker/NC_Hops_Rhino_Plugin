// HopRectPocket -- Rectangular pocket operation for DYNESTIC CNC
// Inputs: rectCurve (Item), cornerRadius (Item), angle (Item),
//         depth (Item), stepdown (Item),
//         toolNr (Item), feedFactor (Item), toolType (Item),
//         colour (Item)
// Outputs: operationLines
//
// Extracts center and dimensions from the rectangle curve bounding box.
// Emits WZF tool call + CALL _RechteckTasche_V5 macro for HopExport.

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
  private Curve _previewRect  = null;
  private Line  _approachLine = Line.Unset;
  private Color _drawColor    = Color.Cyan;

  public override BoundingBox ClippingBox
  {
    get
    {
      BoundingBox bb = BoundingBox.Empty;
      if (_previewRect != null) bb.Union(_previewRect.GetBoundingBox(true));
      if (_approachLine.IsValid) { bb.Union(_approachLine.From); bb.Union(_approachLine.To); }
      return bb;
    }
  }

  public override void DrawViewportWires(IGH_PreviewArgs args)
  {
    if (_previewRect != null)
      args.Display.DrawCurve(_previewRect, _drawColor, 2);
    if (_approachLine.IsValid)
      args.Display.DrawPatternedLine(
        _approachLine.From, _approachLine.To,
        Color.FromArgb(140, 140, 140), unchecked((int)0xF0F0F0F0), 1);
  }

  public override void BeforeRunScript()
  {
    // Mark toolDB optional — suppress orange warning when not connected (per D-12)
    for (int i = 0; i < this.Component.Params.Input.Count; i++)
    {
      if (this.Component.Params.Input[i].Name == "toolDB")
      {
        this.Component.Params.Input[i].Optional = true;
        break;
      }
    }
  }

  private void RunScript(
    Curve rectCurve, double cornerRadius, double angle,
    double depth, double stepdown,
    int toolNr, double feedFactor, string toolType,
    Color colour,
    object toolDB,
    ref object operationLines)
  {
    // PREVIEW: clear fields first (before guards) so disconnecting inputs wipes stale geometry
    _previewRect  = null;
    _approachLine = Line.Unset;
    _drawColor    = colour.IsEmpty ? _defaultColor : colour;

    // ---------------------------------------------------------------
    // 1. DEFAULTS
    // ---------------------------------------------------------------
    operationLines = new List<string>();

    // ---------------------------------------------------------------
    // 1b. TOOLDB LOOKUP — overrides individual inputs when connected (per D-12)
    // ---------------------------------------------------------------
    if (toolDB != null)
    {
      var db = toolDB as Dictionary<string, object>;
      if (db == null)
      {
        this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
          "toolDB input is not a valid HopToolDB object");
        return;
      }
      int activeNr = db.ContainsKey("activeToolNr") ? (int)db["activeToolNr"] : toolNr;
      string toolKey = "tool_" + activeNr.ToString();
      if (!db.ContainsKey(toolKey))
      {
        this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
          "Tool not found in HopToolDB: toolNr=" + activeNr.ToString());
        return;
      }
      var entry = db[toolKey] as Dictionary<string, object>;
      if (entry != null)
      {
        toolNr     = (int)entry["toolNr"];
        toolType   = (string)entry["toolType"];
        feedFactor = (double)entry["feedFactor"];
      }
    }

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
    if (feedFactor <= 0) feedFactor = 1.0;
    if (string.IsNullOrEmpty(toolType)) toolType = "WZF";
    if (depth <= 0) depth = 1.0;

    // ---------------------------------------------------------------
    // 4. EXTRACT DIMENSIONS from BoundingBox
    // ---------------------------------------------------------------
    BoundingBox bb = rectCurve.GetBoundingBox(true);
    double cx = (bb.Min.X + bb.Max.X) / 2.0;
    double cy = (bb.Min.Y + bb.Max.Y) / 2.0;
    double width = bb.Max.X - bb.Min.X;
    double height = bb.Max.Y - bb.Min.Y;

    // PREVIEW: rectangle outline at pocket depth
    double previewZ = -Math.Abs(depth > 0 ? depth : 1.0);
    Rectangle3d previewBounds = new Rectangle3d(
      new Plane(new Point3d(cx, cy, previewZ), Vector3d.XAxis, Vector3d.YAxis),
      new Interval(-width / 2.0, width / 2.0),
      new Interval(-height / 2.0, height / 2.0));
    Curve previewCurve = previewBounds.ToNurbsCurve();

    // Bug fix 1: apply corner radius fillet when cornerRadius > 0
    if (cornerRadius > 0)
    {
      Curve filleted = Curve.CreateFilletCornersCurve(previewCurve, cornerRadius, 1e-6, 1e-6);
      if (filleted != null) previewCurve = filleted;
    }

    // Bug fix 2: rotate by angle (degrees) around center point
    if (angle != 0)
    {
      double angleRad = angle * Math.PI / 180.0;
      Point3d centerPoint = new Point3d(cx, cy, previewZ);
      previewCurve.Transform(Transform.Rotation(angleRad, Vector3d.ZAxis, centerPoint));
    }

    _previewRect = previewCurve;
    // PREVIEW: approach line from safeZ to bottom-left corner of rect
    double safeZ = rectCurve.GetBoundingBox(true).Max.Z + 20.0;
    Point3d startPt = new Point3d(bb.Min.X, bb.Min.Y, previewZ);
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
    double negDepth = -Math.Abs(depth);
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
