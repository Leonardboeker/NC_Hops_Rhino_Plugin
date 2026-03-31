// HopFreeSlot -- Free slot operation for DYNESTIC CNC
// Inputs: p1 (Item), p2 (Item), slotWidth (Item),
//         depth (Item), toolNr (Item), feedFactor (Item), toolType (Item),
//         colour (Item)
// Outputs: operationLines
//
// Takes two Point3d inputs (slot start and end) plus slot width.
// Emits WZF tool call + CALL _nuten_frei_v5 macro for HopExport.

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
  private Line  _centerline    = Line.Unset;
  private Line  _edgeLine1     = Line.Unset;
  private Line  _edgeLine2     = Line.Unset;
  private Line  _approachLine  = Line.Unset;
  private Color _drawColor     = Color.Orange;

  public override BoundingBox ClippingBox
  {
    get
    {
      BoundingBox bb = BoundingBox.Empty;
      if (_centerline.IsValid)  { bb.Union(_centerline.From);  bb.Union(_centerline.To);  }
      if (_edgeLine1.IsValid)   { bb.Union(_edgeLine1.From);   bb.Union(_edgeLine1.To);   }
      if (_edgeLine2.IsValid)   { bb.Union(_edgeLine2.From);   bb.Union(_edgeLine2.To);   }
      if (_approachLine.IsValid){ bb.Union(_approachLine.From); bb.Union(_approachLine.To);}
      return bb;
    }
  }

  public override void DrawViewportWires(IGH_PreviewArgs args)
  {
    if (_centerline.IsValid)
      args.Display.DrawLine(_centerline, _drawColor, 2);
    if (_edgeLine1.IsValid)
      args.Display.DrawLine(_edgeLine1, _drawColor, 1);
    if (_edgeLine2.IsValid)
      args.Display.DrawLine(_edgeLine2, _drawColor, 1);
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
    Point3d p1, Point3d p2, double slotWidth,
    double depth, int toolNr, double feedFactor, string toolType,
    Color colour,
    object toolDB,
    ref object operationLines)
  {
    // PREVIEW: clear fields first (before guards) so disconnecting inputs wipes stale geometry
    _centerline   = Line.Unset;
    _edgeLine1    = Line.Unset;
    _edgeLine2    = Line.Unset;
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
    if (feedFactor <= 0) feedFactor = 1.0;
    if (string.IsNullOrEmpty(toolType)) toolType = "WZF";
    if (depth <= 0) depth = 1.0;

    // PREVIEW: centerline and slot-edge parallel lines at cut depth
    double previewZ = -Math.Abs(depth);
    Point3d a = new Point3d(p1.X, p1.Y, previewZ);
    Point3d b = new Point3d(p2.X, p2.Y, previewZ);
    _centerline = new Line(a, b);
    // Perpendicular offset in XY plane
    Vector3d dir = b - a;
    if (dir.Length > 0.001)
    {
      dir.Unitize();
      Vector3d perp = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
      perp.Unitize();
      double halfW = slotWidth / 2.0;
      _edgeLine1 = new Line(a + perp * halfW, b + perp * halfW);
      _edgeLine2 = new Line(a - perp * halfW, b - perp * halfW);
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

    double negDepth = -Math.Abs(depth);

    lines.Add("CALL _nuten_frei_v5(VAL "
      + "X1:=" + p1.X.ToString(CultureInfo.InvariantCulture) + ","
      + "Y1:=" + p1.Y.ToString(CultureInfo.InvariantCulture) + ","
      + "X2:=" + p2.X.ToString(CultureInfo.InvariantCulture) + ","
      + "Y2:=" + p2.Y.ToString(CultureInfo.InvariantCulture) + ","
      + "NB:=" + slotWidth.ToString(CultureInfo.InvariantCulture) + ","
      + "Tiefe:=" + negDepth.ToString(CultureInfo.InvariantCulture) + ","
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
