// HopPart -- Part bundling component for DYNESTIC CNC nesting workflow
// Inputs:  outline (Item, Curve), operationLines (List, string), grainAngle (Item, double)
// Outputs: part (single output)
//
// Bundles a closed part outline + all operation lines into a Dictionary<string,object>
// wrapped in GH_ObjectWrapper for cross-component transport.
// The outline curve is used by OpenNest as the nesting boundary.
// operationLines from multiple op-components are flattened into the dictionary.
// grainAngle rotates the grain direction arrow (0 = X-axis, 90 = Y-axis).
// Per D-04, D-05, D-07: HopPart is the unit OpenNest receives.
//
// GH canvas setup:
//   Input  outline:        Type Hint -> Curve
//   Input  operationLines: Type Hint -> string,  List Access
//   Input  grainAngle:     Type Hint -> double
//   Input  colour:         Type Hint -> System.Drawing.Color
//   Output part:           rename to "part"
//   Component display name: "HopPart"

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // ---------------------------------------------------------------
  // PREVIEW FIELDS
  // ---------------------------------------------------------------
  private static readonly Color _defaultColor = Color.FromArgb(100, 149, 237);
  private Curve   _outlineCurve = null;
  private Line    _grainArrow   = Line.Unset;
  private Color   _drawColor    = Color.FromArgb(100, 149, 237);

  public override BoundingBox ClippingBox
  {
    get
    {
      BoundingBox bb = BoundingBox.Empty;
      if (_outlineCurve != null) bb.Union(_outlineCurve.GetBoundingBox(true));
      if (_grainArrow.IsValid) { bb.Union(_grainArrow.From); bb.Union(_grainArrow.To); }
      return bb;
    }
  }

  public override void DrawViewportWires(IGH_PreviewArgs args)
  {
    if (_outlineCurve != null)
      args.Display.DrawCurve(_outlineCurve, _drawColor, 2);

    if (_grainArrow.IsValid)
    {
      args.Display.DrawArrow(
        _grainArrow,
        Color.FromArgb(80, 80, 80), 0.0, 3.0);
    }
  }

  public override void DrawViewportMeshes(IGH_PreviewArgs args)
  {
    // Required override -- no mesh preview for HopPart
  }

  private void RunScript(
    Curve outline,
    List<string> operationLines,
    double grainAngle,
    Color colour,
    ref object part)
  {
    // ---------------------------------------------------------------
    // 1. DEFAULTS -- clear preview fields before guards
    // ---------------------------------------------------------------
    _outlineCurve = null;
    _grainArrow   = Line.Unset;
    _drawColor    = colour.IsEmpty ? _defaultColor : colour;
    part = null;

    // ---------------------------------------------------------------
    // 2. GUARDS
    // ---------------------------------------------------------------
    if (outline == null)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        "HopPart: no outline curve connected");
      return;
    }
    if (!outline.IsClosed)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        "HopPart: outline curve must be closed");
      return;
    }

    // ---------------------------------------------------------------
    // 3. INPUT DEFAULTS
    // ---------------------------------------------------------------
    if (operationLines == null) operationLines = new List<string>();
    if (grainAngle < 0 || grainAngle > 360) grainAngle = 0;

    // ---------------------------------------------------------------
    // 4. GRAIN DIRECTION VECTOR
    // ---------------------------------------------------------------
    double rad = grainAngle * Math.PI / 180.0;
    Vector3d grainDir = new Vector3d(Math.Cos(rad), Math.Sin(rad), 0);

    // ---------------------------------------------------------------
    // 5. BUILD DICTIONARY (per D-04, D-07 -- wire format)
    //    operationLines stored as List<List<string>> for grouping
    //    compatibility with downstream HopSheetExport.
    // ---------------------------------------------------------------
    var opLineGroups = new List<List<string>>();
    opLineGroups.Add(new List<string>(operationLines));

    var dict = new Dictionary<string, object>();
    dict["outline"]        = outline;
    dict["operationLines"] = opLineGroups;
    dict["grainDir"]       = grainDir;

    // ---------------------------------------------------------------
    // 6. PREVIEW -- outline curve + grain arrow (per D-12)
    // ---------------------------------------------------------------
    _outlineCurve = outline;

    // Grain arrow: 20mm arrow from centroid along grain direction
    var amp = AreaMassProperties.Compute(outline);
    if (amp != null)
    {
      Point3d centroid = amp.Centroid;
      double arrowLen = 20.0;
      Point3d arrowEnd = centroid + grainDir * arrowLen;
      _grainArrow = new Line(centroid, arrowEnd);
    }

    // ---------------------------------------------------------------
    // 7. OUTPUT -- wrap in GH_ObjectWrapper for cross-assembly transport
    // ---------------------------------------------------------------
    part = new Grasshopper.Kernel.Types.GH_ObjectWrapper(dict);

    this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
      "HopPart: " + operationLines.Count + " operation lines bundled"
      + ", grain=" + grainAngle.ToString(CultureInfo.InvariantCulture) + " deg");
  }
}
