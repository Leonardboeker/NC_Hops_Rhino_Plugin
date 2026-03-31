// HopCircPath -- Circular profile path operation for DYNESTIC CNC
// Inputs: center (Item), radius (Item), radiusCorr (Item),
//         depth (Item), stepdown (Item), angle (Item),
//         toolNr (Item), feedFactor (Item), toolType (Item),
//         colour (Item)
// Outputs: operationLines
//
// radiusCorr: 1 = inside, -1 = outside, 0 = center
// Emits WZF tool call + CALL _Kreisbahn_V5 macro for HopExport.

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
  private Circle _previewCircle  = Circle.Unset;
  private Line   _approachLine   = Line.Unset;
  private Color  _drawColor      = Color.LimeGreen;

  public override BoundingBox ClippingBox
  {
    get
    {
      BoundingBox bb = BoundingBox.Empty;
      if (_previewCircle.IsValid) bb.Union(_previewCircle.BoundingBox);
      if (_approachLine.IsValid) { bb.Union(_approachLine.From); bb.Union(_approachLine.To); }
      return bb;
    }
  }

  public override void DrawViewportWires(IGH_PreviewArgs args)
  {
    if (_previewCircle.IsValid)
      args.Display.DrawCircle(_previewCircle, _drawColor, 2);
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
    Point3d center, double radius, int radiusCorr,
    double depth, double stepdown, double angle,
    int toolNr, double feedFactor, string toolType,
    Color colour,
    object toolDB,
    ref object operationLines)
  {
    // PREVIEW: clear fields first (before guards) so disconnecting inputs wipes stale geometry
    _previewCircle = Circle.Unset;
    _approachLine  = Line.Unset;
    _drawColor     = colour.IsEmpty ? _defaultColor : colour;

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
    if (feedFactor <= 0) feedFactor = 1.0;
    if (string.IsNullOrEmpty(toolType)) toolType = "WZF";
    if (depth <= 0) depth = 1.0;
    if (angle <= 0) angle = 360.0;

    // PREVIEW: circle at cut depth (the circular path the tool follows)
    double previewZ = center.Z - Math.Abs(depth);
    Point3d circlePt = new Point3d(center.X, center.Y, previewZ);
    _previewCircle = new Circle(new Plane(circlePt, Vector3d.ZAxis), radius);
    // PREVIEW: approach line from safeZ to the 3 o'clock point (standard entry for circular moves)
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

    double negDepth = -Math.Abs(depth);
    double zuTiefe = (stepdown > 0) ? stepdown : 0;

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
