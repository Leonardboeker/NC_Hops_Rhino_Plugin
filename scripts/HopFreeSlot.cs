// HopFreeSlot -- Free slot operation for DYNESTIC CNC
// Inputs: p1 (Item), p2 (Item), slotWidth (Item),
//         depth (Item), toolNr (Item), feedFactor (Item), toolType (Item)
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
  private void RunScript(
    Point3d p1, Point3d p2, double slotWidth,
    double depth, int toolNr, double feedFactor, string toolType,
    ref object operationLines)
  {
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
    if (feedFactor <= 0) feedFactor = 1.0;
    if (string.IsNullOrEmpty(toolType)) toolType = "WZF";
    if (depth <= 0) depth = 1.0;

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
