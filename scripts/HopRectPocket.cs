// HopRectPocket -- Rectangular pocket operation for DYNESTIC CNC
// Inputs: rectCurve (Item), cornerRadius (Item), angle (Item),
//         depth (Item), stepdown (Item),
//         toolNr (Item), feedFactor (Item), toolType (Item)
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
  private void RunScript(
    Curve rectCurve, double cornerRadius, double angle,
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
