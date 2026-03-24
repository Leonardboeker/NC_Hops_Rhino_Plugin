// HopCircPath -- Circular profile path operation for DYNESTIC CNC
// Inputs: center (Item), radius (Item), radiusCorr (Item),
//         depth (Item), stepdown (Item), angle (Item),
//         toolNr (Item), feedFactor (Item), toolType (Item)
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
  private void RunScript(
    Point3d center, double radius, int radiusCorr,
    double depth, double stepdown, double angle,
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
    if (angle <= 0) angle = 360.0;

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
