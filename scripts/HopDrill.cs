// HopDrill -- Vertical drilling operation via Bohrung macro
// Inputs: points (List), zSurface (Item), depth (Item), diameter (Item),
//         stepdown (Item), toolNr (Item), feedFactor (Item), toolType (Item)
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
  private void RunScript(
    List<Point3d> points,
    double zSurface,
    double depth,
    double diameter,
    double stepdown,
    int toolNr,
    double feedFactor,
    string toolType,
    ref object operationLines)
  {
    // ---------------------------------------------------------------
    // 1. DEFAULTS -- downstream gets these if guards trigger
    // ---------------------------------------------------------------
    operationLines = new List<string>();

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
