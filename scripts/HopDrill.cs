// HopDrill -- Vertical drilling operation via Bohrung macro
// Inputs: points (List), zSurface (Item), depth (Item), diameter (Item),
//         stepdown (Item), toolNr (Item), feedFactor (Item), toolType (Item),
//         colour (Item)
// Outputs: operationLines
//
// Converts a list of Grasshopper points into NC-Hops Bohrung macro strings.
// Each point becomes one Bohrung call with the specified depth and diameter.
// When stepdown > 0, drilling is split into multiple passes at increasing depths.
// Output wires into HopExport.cs operationLines input.

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
  private static readonly Color _defaultColor = Color.Red;
  private List<Circle> _drillCircles    = new List<Circle>();
  private List<Line>   _drillDepthLines = new List<Line>();
  private Line         _approachLine    = Line.Unset;
  private Color        _drawColor       = Color.Red;

  public override BoundingBox ClippingBox
  {
    get
    {
      BoundingBox bb = BoundingBox.Empty;
      foreach (var c in _drillCircles) bb.Union(c.BoundingBox);
      foreach (var l in _drillDepthLines) { bb.Union(l.From); bb.Union(l.To); }
      if (_approachLine.IsValid) { bb.Union(_approachLine.From); bb.Union(_approachLine.To); }
      return bb;
    }
  }

  public override void DrawViewportWires(IGH_PreviewArgs args)
  {
    foreach (var c in _drillCircles)
      args.Display.DrawCircle(c, _drawColor, 2);
    foreach (var l in _drillDepthLines)
      args.Display.DrawLine(l, _drawColor, 1);
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
    List<Point3d> points,
    double zSurface,
    double depth,
    double diameter,
    double stepdown,
    int toolNr,
    double feedFactor,
    string toolType,
    Color colour,
    object toolDB,
    ref object operationLines)
  {
    // PREVIEW: clear fields first (before guards) so disconnecting inputs wipes stale geometry
    _drillCircles.Clear();
    _drillDepthLines.Clear();
    _approachLine = Line.Unset;
    _drawColor    = colour.IsEmpty ? _defaultColor : colour;

    // ---------------------------------------------------------------
    // 1. DEFAULTS -- downstream gets these if guards trigger
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
    // 2. GUARDS -- required inputs
    // ---------------------------------------------------------------
    if (points == null || points.Count == 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "No drill points connected");
      return;
    }
    if (toolNr <= 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "toolNr is required and must be > 0");
      return;
    }

    // ---------------------------------------------------------------
    // 3. INPUT DEFAULTS -- fallback for disconnected optional inputs
    // ---------------------------------------------------------------
    if (feedFactor <= 0) feedFactor = 1.0;
    if (string.IsNullOrEmpty(toolType)) toolType = "WZB";
    if (depth <= 0) depth = 1.0;
    if (diameter <= 0) diameter = 8.0;

    // PREVIEW: circle + depth line per drill point (after diameter default applied)
    double radius = diameter / 2.0;
    for (int i = 0; i < points.Count; i++)
    {
      Point3d pt = new Point3d(points[i].X, points[i].Y, zSurface);
      _drillCircles.Add(new Circle(new Plane(pt, Vector3d.ZAxis), radius));
      _drillDepthLines.Add(new Line(pt, new Point3d(pt.X, pt.Y, zSurface - Math.Abs(depth))));
    }
    // PREVIEW: approach line above first point
    if (points.Count > 0)
    {
      Point3d firstPt = new Point3d(points[0].X, points[0].Y, zSurface);
      double safeZ = zSurface + 20.0;
      _approachLine = new Line(new Point3d(firstPt.X, firstPt.Y, safeZ), firstPt);
    }

    // ---------------------------------------------------------------
    // 4. BUILD TOOL CALL -- first line of output per D-13
    // ---------------------------------------------------------------
    List<string> lines = new List<string>();
    string toolCall = toolType + " (" + toolNr.ToString()
      + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
      + ",_VA,_SD,0,'')";
    lines.Add(toolCall);

    // ---------------------------------------------------------------
    // 5. MULTI-PASS OR SINGLE-PASS DRILLING
    // ---------------------------------------------------------------
    if (stepdown > 0)
    {
      // Multi-pass: split depth into incremental passes per D-11
      int passCount = (int)Math.Ceiling(depth / stepdown);
      for (int p = 0; p < passCount; p++)
      {
        double passDepth = Math.Min((p + 1) * stepdown, depth);
        double negPassDepth = -passDepth;
        for (int i = 0; i < points.Count; i++)
        {
          Point3d pt = points[i];
          lines.Add("Bohrung ("
            + pt.X.ToString(CultureInfo.InvariantCulture) + ","
            + pt.Y.ToString(CultureInfo.InvariantCulture) + ","
            + zSurface.ToString(CultureInfo.InvariantCulture) + ","
            + negPassDepth.ToString(CultureInfo.InvariantCulture) + ","
            + diameter.ToString(CultureInfo.InvariantCulture)
            + ",0,0,0,0,0,0,0)");
        }
      }
    }
    else
    {
      // Single pass at full depth per D-08
      double negDepth = -Math.Abs(depth);
      for (int i = 0; i < points.Count; i++)
      {
        Point3d pt = points[i];
        lines.Add("Bohrung ("
          + pt.X.ToString(CultureInfo.InvariantCulture) + ","
          + pt.Y.ToString(CultureInfo.InvariantCulture) + ","
          + zSurface.ToString(CultureInfo.InvariantCulture) + ","
          + negDepth.ToString(CultureInfo.InvariantCulture) + ","
          + diameter.ToString(CultureInfo.InvariantCulture)
          + ",0,0,0,0,0,0,0)");
      }
    }

    // ---------------------------------------------------------------
    // 6. OUTPUT + REMARK
    // ---------------------------------------------------------------
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark,
      points.Count.ToString() + " drill points, depth=" + (-Math.Abs(depth)).ToString(CultureInfo.InvariantCulture)
      + ", diameter=" + diameter.ToString(CultureInfo.InvariantCulture));
    operationLines = lines;
  }
}
