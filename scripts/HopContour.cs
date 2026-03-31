// HopContour -- 2D contour cutting component for DYNESTIC CNC
// Inputs: curve (Item), depth (Item), plungeZ (Item), tolerance (Item),
//         toolNr (Item), stepdown (Item), colour (Item)
// Outputs: operationLines
//
// Converts Grasshopper curves (NURBS, arcs, polylines) into NC-Hops 2D contour
// macro blocks: SP / G01 / G03M / G02M / EP.
// True arcs are detected via Curve.ToArcsAndLines and emitted as G03M (CCW) or G02M (CW).
// Output wires into HopExport.cs operationLines input.
// toolType and feedFactor are hardcoded (WZF / 1.0) -- handled at machine level.

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
  private static readonly Color _defaultColor = Color.Yellow;
  private Brep  _previewVolume = null;
  private Line  _approachLine  = Line.Unset;
  private Color _drawColor     = Color.Yellow;

  public override BoundingBox ClippingBox
  {
    get
    {
      BoundingBox bb = BoundingBox.Empty;
      if (_previewVolume != null)
        bb.Union(_previewVolume.GetBoundingBox(true));
      if (_approachLine.IsValid)
      {
        bb.Union(_approachLine.From);
        bb.Union(_approachLine.To);
      }
      return bb;
    }
  }

  public override void DrawViewportMeshes(IGH_PreviewArgs args)
  {
    if (_previewVolume != null)
    {
      Rhino.Display.DisplayMaterial mat = new Rhino.Display.DisplayMaterial(_drawColor);
      mat.Transparency = 0.55;
      args.Display.DrawBrepShaded(_previewVolume, mat);
    }
  }

  public override void DrawViewportWires(IGH_PreviewArgs args)
  {
    if (_previewVolume != null)
      args.Display.DrawBrepWires(_previewVolume, _drawColor, 1);
    if (_approachLine.IsValid)
      args.Display.DrawPatternedLine(
        _approachLine.From, _approachLine.To,
        Color.FromArgb(140, 140, 140), unchecked((int)0xF0F0F0F0), 1);
  }

  private void RunScript(
    Curve curve, double depth, double plungeZ, double tolerance,
    int toolNr, double stepdown,
    Color colour,
    ref object operationLines)
  {
    // Hardcoded tool params -- handled at machine level
    string toolType  = "WZF";
    double feedFactor = 1.0;

    // PREVIEW: clear fields first (before guards) so disconnecting inputs wipes stale geometry
    _previewVolume = null;
    _approachLine  = Line.Unset;
    _drawColor     = colour.IsEmpty ? _defaultColor : colour;

    // ---------------------------------------------------------------
    // 1. DEFAULTS -- downstream gets these if guards trigger
    // ---------------------------------------------------------------
    operationLines = new List<string>();

    // ---------------------------------------------------------------
    // 2. GUARDS -- required input validation
    // ---------------------------------------------------------------
    if (curve == null)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "No curve connected");
      return;
    }

    if (toolNr <= 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "toolNr is required and must be > 0");
      return;
    }

    // ---------------------------------------------------------------
    // 3. INPUT DEFAULTS -- fallback for disconnected inputs
    // ---------------------------------------------------------------
    if (tolerance <= 0) tolerance = 0.1;
    if (depth <= 0) depth = 1.0;
    if (plungeZ <= 0) plungeZ = depth;

    // ---------------------------------------------------------------
    // 4. PLANARITY CHECK
    // ---------------------------------------------------------------
    if (!curve.IsPlanar())
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "Curve is not planar -- cannot use for 2D contour");
      return;
    }
    BoundingBox curveBB = curve.GetBoundingBox(true);
    if (Math.Abs(curveBB.Max.Z - curveBB.Min.Z) > tolerance)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Warning,
        "Curve has Z variation -- using XY projection for 2D contour");
    }

    // PREVIEW: build extruded volume (curve extruded downward by depth)
    double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
    double cutZ = -Math.Abs(plungeZ > 0 ? plungeZ : depth);

    // Place curve at cut Z for extrusion base
    Curve baseCurve = curve.DuplicateCurve();
    baseCurve.Translate(new Vector3d(0, 0, cutZ - baseCurve.PointAtStart.Z));

    if (baseCurve.IsClosed)
    {
      Vector3d extDir = new Vector3d(0, 0, -Math.Abs(depth));
      Surface extSrf = Surface.CreateExtrusion(baseCurve, extDir);
      if (extSrf != null)
      {
        Brep extBrep = extSrf.ToBrep();
        if (extBrep != null)
        {
          Brep capped = extBrep.CapPlanarHoles(tol);
          _previewVolume = capped != null ? capped : extBrep;
        }
      }
    }
    else
    {
      // Open curve: just extrude without capping
      Vector3d extDir = new Vector3d(0, 0, -Math.Abs(depth));
      Surface extSrf = Surface.CreateExtrusion(baseCurve, extDir);
      if (extSrf != null)
        _previewVolume = extSrf.ToBrep();
    }

    // PREVIEW: approach line from safeZ above start point
    double safeZ = curve.GetBoundingBox(true).Max.Z + 20.0;
    Point3d startPt = baseCurve.PointAtStart;
    _approachLine = new Line(new Point3d(startPt.X, startPt.Y, safeZ), startPt);

    // ---------------------------------------------------------------
    // 5. CURVE DECOMPOSITION -- arcs and lines via RhinoCommon
    // ---------------------------------------------------------------
    PolyCurve pc = curve.ToArcsAndLines(tolerance, 0.1, 0.0, 0.0) as PolyCurve;
    if (pc == null)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "Curve conversion to arcs and lines failed");
      return;
    }

    // ---------------------------------------------------------------
    // 6. BUILD TOOL CALL -- first line of output per D-13
    // ---------------------------------------------------------------
    List<string> lines = new List<string>();
    string toolCall = toolType + " (" + toolNr.ToString()
      + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
      + ",_VA,_SD,0,'')";
    lines.Add(toolCall);

    // ---------------------------------------------------------------
    // 7. MULTI-PASS OR SINGLE-PASS
    // ---------------------------------------------------------------
    if (stepdown > 0)
    {
      // Multi-pass: split depth into stepdown increments
      int passCount = (int)Math.Ceiling(depth / stepdown);
      for (int p = 0; p < passCount; p++)
      {
        double passDepth = Math.Min((p + 1) * stepdown, depth);
        BuildContourBlock(lines, pc, -Math.Abs(passDepth));
      }
    }
    else
    {
      // Single pass at full depth; SP z_eintauch uses plungeZ
      BuildContourBlock(lines, pc, -Math.Abs(plungeZ));
    }

    // ---------------------------------------------------------------
    // 8. OUTPUT + REMARK
    // ---------------------------------------------------------------
    int arcCount = 0;
    int lineCount = 0;
    for (int i = 0; i < pc.SegmentCount; i++)
    {
      Curve seg = pc.SegmentCurve(i);
      ArcCurve arcCheck = seg as ArcCurve;
      if (arcCheck != null)
      {
        arcCount++;
      }
      else
      {
        lineCount++;
      }
    }
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark,
      "Contour: " + pc.SegmentCount.ToString() + " segments ("
        + arcCount.ToString() + " arcs, " + lineCount.ToString() + " lines), depth="
        + (-Math.Abs(depth)).ToString(CultureInfo.InvariantCulture));

    operationLines = lines;
  }

  // ---------------------------------------------------------------
  // HELPER: Build one SP...moves...EP contour block at a given Z
  // ---------------------------------------------------------------
  private void BuildContourBlock(List<string> lines, PolyCurve pc, double zEintauch)
  {
    // SP (contour start)
    Point3d startPt = pc.PointAtStart;
    lines.Add("SP (" + startPt.X.ToString(CultureInfo.InvariantCulture) + ","
      + startPt.Y.ToString(CultureInfo.InvariantCulture) + ","
      + zEintauch.ToString(CultureInfo.InvariantCulture)
      + ",2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)");

    // Segment iteration: classify each as arc or line
    for (int i = 0; i < pc.SegmentCount; i++)
    {
      Curve seg = pc.SegmentCurve(i);

      ArcCurve arcSeg = seg as ArcCurve;
      if (arcSeg != null)
      {
        Arc arc = arcSeg.Arc;
        Point3d endPt = arc.EndPoint;
        Point3d center = arc.Center;
        bool isCCW = arc.Plane.Normal.Z >= 0;
        string cmd = isCCW ? "G03M" : "G02M";
        lines.Add(cmd + " ("
          + endPt.X.ToString(CultureInfo.InvariantCulture) + ","
          + endPt.Y.ToString(CultureInfo.InvariantCulture) + ",0,"
          + center.X.ToString(CultureInfo.InvariantCulture) + ","
          + center.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,2,0)");
        continue;
      }

      LineCurve lineSeg = seg as LineCurve;
      if (lineSeg != null)
      {
        Point3d endPt = lineSeg.PointAtEnd;
        lines.Add("G01 ("
          + endPt.X.ToString(CultureInfo.InvariantCulture) + ","
          + endPt.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,0,2)");
        continue;
      }

      // Fallback for any other segment type
      Point3d fallbackEnd = seg.PointAtEnd;
      lines.Add("G01 ("
        + fallbackEnd.X.ToString(CultureInfo.InvariantCulture) + ","
        + fallbackEnd.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,0,2)");
    }

    // EP (contour end)
    lines.Add("EP (0,_ANF,0)");
  }
}
