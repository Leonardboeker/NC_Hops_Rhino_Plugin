// HopCircPocket -- Circular pocket operation for DYNESTIC CNC
// Inputs: center (Item), radius (Item),
//         depth (Item), stepdown (Item),
//         toolNr (Item), feedFactor (Item), toolType (Item)
// Outputs: operationLines
//
// Emits WZF tool call + CALL _Kreistasche_V5 macro for HopExport.

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
    Point3d center, double radius,
    double depth, double stepdown,
    int toolNr, double feedFactor, string toolType,
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

    // ---------------------------------------------------------------
    // 4. BUILD TOOL CALL + MACRO
    // ---------------------------------------------------------------
    List<string> lines = new List<string>();
    lines.Add(toolType + " (" + toolNr.ToString()
      + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
      + ",_VA,_SD,0,'')");

    double negDepth = -Math.Abs(depth);
    double zustellung = (stepdown > 0) ? stepdown : 0;

    lines.Add("CALL _Kreistasche_V5(VAL "
      + "X_Mitte:=" + center.X.ToString(CultureInfo.InvariantCulture) + ","
      + "Y_Mitte:=" + center.Y.ToString(CultureInfo.InvariantCulture) + ","
      + "Radius:=" + radius.ToString(CultureInfo.InvariantCulture) + ","
      + "Tiefe:=" + negDepth.ToString(CultureInfo.InvariantCulture) + ","
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
