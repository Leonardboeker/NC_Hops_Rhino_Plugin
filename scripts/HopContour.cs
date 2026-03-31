// HopContour -- 2D contour cutting component for DYNESTIC CNC
// Inputs: curve (Item), depth (Item), plungeZ (Item), tolerance (Item),
//         toolNr (Item), toolDiameter (Item), stepdown (Item), colour (Item)
// Outputs: operationLines
//
// Converts Grasshopper curves (NURBS, arcs, polylines) into NC-Hops 2D contour
// macro blocks: SP / G01 / G03M / G02M / EP.
// True arcs detected via Curve.ToArcsAndLines, emitted as G03M (CCW) or G02M (CW).
// Preview: channel volume (inner+outer offset by toolDiameter/2, extruded by depth).
// toolType and feedFactor hardcoded (WZF / 1.0) -- handled at machine level.

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
  // Channel volume: inner + outer offset curves extruded downward.
  // Shows the actual material removed by the endmill along the path.
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
      if (_previewVolume != null) bb.Union(_previewVolume.GetBoundingBox(true));
      if (_approachLine.IsValid)  { bb.Union(_approachLine.From); bb.Union(_approachLine.To); }
      return bb;
    }
  }

  public override void DrawViewportMeshes(IGH_PreviewArgs args)
  {
    if (_previewVolume != null)
    {
      Rhino.Display.DisplayMaterial mat = new Rhino.Display.DisplayMaterial(_drawColor);
      mat.Transparency = 0.5;
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
    int toolNr, double toolDiameter, double stepdown,
    Color colour,
    ref object operationLines)
  {
    // Hardcoded tool params -- handled at machine level
    string toolType   = "WZF";
    double feedFactor = 1.0;

    // PREVIEW: clear first so disconnecting inputs removes stale geometry
    _previewVolume = null;
    _approachLine  = Line.Unset;
    _drawColor     = colour.IsEmpty ? _defaultColor : colour;

    // ---------------------------------------------------------------
    // 1. DEFAULTS
    // ---------------------------------------------------------------
    operationLines = new List<string>();

    // ---------------------------------------------------------------
    // 2. GUARDS
    // ---------------------------------------------------------------
    if (curve == null)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No curve connected");
      return;
    }
    if (toolNr <= 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        "toolNr is required and must be > 0");
      return;
    }

    // ---------------------------------------------------------------
    // 3. INPUT DEFAULTS
    // ---------------------------------------------------------------
    if (tolerance    <= 0) tolerance    = 0.1;
    if (depth        <= 0) depth        = 1.0;
    if (plungeZ      <= 0) plungeZ      = depth;
    if (toolDiameter <= 0) toolDiameter = 8.0;   // visual default -- not written to NC output

    // ---------------------------------------------------------------
    // 4. PLANARITY CHECK
    // ---------------------------------------------------------------
    if (!curve.IsPlanar())
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        "Curve is not planar -- cannot use for 2D contour");
      return;
    }
    BoundingBox curveBB = curve.GetBoundingBox(true);
    if (Math.Abs(curveBB.Max.Z - curveBB.Min.Z) > tolerance)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        "Curve has Z variation -- using XY projection for 2D contour");
    }

    // ---------------------------------------------------------------
    // 5. CHANNEL VOLUME PREVIEW
    //    Offset curve inward and outward by toolDiameter/2.
    //    Build planar region between offsets, extrude downward by depth.
    //    For open curves: offset both sides to form a slot shape.
    // ---------------------------------------------------------------
    double tol    = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
    double radius = toolDiameter / 2.0;
    double cutZ   = -Math.Abs(plungeZ > 0 ? plungeZ : depth);

    // Project curve to Z=cutZ for the channel base
    Curve baseCrv = curve.DuplicateCurve();
    baseCrv.Translate(new Vector3d(0, 0, cutZ - baseCrv.PointAtStart.Z));

    Curve[] outerOffsets = baseCrv.Offset(Plane.WorldXY, radius,  tol, CurveOffsetCornerStyle.Sharp);
    Curve[] innerOffsets = baseCrv.Offset(Plane.WorldXY, -radius, tol, CurveOffsetCornerStyle.Sharp);

    if (outerOffsets != null && outerOffsets.Length > 0 &&
        innerOffsets != null && innerOffsets.Length > 0)
    {
      // Join offset curves into single closed loops if needed
      Curve outerLoop = outerOffsets.Length == 1 ? outerOffsets[0]
        : Curve.JoinCurves(outerOffsets, tol)[0];
      Curve innerLoop = innerOffsets.Length == 1 ? innerOffsets[0]
        : Curve.JoinCurves(innerOffsets, tol)[0];

      // Create planar Brep from the ring region between inner and outer offset
      Curve[] regionBoundaries = new Curve[] { outerLoop, innerLoop };
      Brep[] planarBreps = Brep.CreatePlanarBreps(regionBoundaries, tol);

      if (planarBreps != null && planarBreps.Length > 0)
      {
        // Extrude the ring face downward by depth to get the channel volume
        Vector3d extDir = new Vector3d(0, 0, -Math.Abs(depth));
        Brep channelBrep = planarBreps[0].Faces[0].CreateExtrusion(extDir, true);
        if (channelBrep != null)
          _previewVolume = channelBrep;
        else
          _previewVolume = planarBreps[0];
      }
    }

    // Fallback: if offset fails (e.g. very small curve), show plain curve
    if (_previewVolume == null)
    {
      // Store curve for wire fallback -- just reuse approach line slot for ClippingBox
    }

    // Dashed approach line from safeZ to curve start
    double safeZ   = curveBB.Max.Z + 20.0;
    Point3d startP = baseCrv.PointAtStart;
    _approachLine  = new Line(new Point3d(startP.X, startP.Y, safeZ), startP);

    // ---------------------------------------------------------------
    // 6. CURVE DECOMPOSITION
    // ---------------------------------------------------------------
    PolyCurve pc = curve.ToArcsAndLines(tolerance, 0.1, 0.0, 0.0) as PolyCurve;
    if (pc == null)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        "Curve conversion to arcs and lines failed");
      return;
    }

    // ---------------------------------------------------------------
    // 7. BUILD TOOL CALL
    // ---------------------------------------------------------------
    List<string> lines = new List<string>();
    lines.Add(toolType + " (" + toolNr.ToString()
      + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
      + ",_VA,_SD,0,'')");

    // ---------------------------------------------------------------
    // 8. MULTI-PASS OR SINGLE-PASS
    // ---------------------------------------------------------------
    if (stepdown > 0)
    {
      int passCount = (int)Math.Ceiling(depth / stepdown);
      for (int p = 0; p < passCount; p++)
      {
        double passDepth = Math.Min((p + 1) * stepdown, depth);
        BuildContourBlock(lines, pc, -Math.Abs(passDepth));
      }
    }
    else
    {
      BuildContourBlock(lines, pc, -Math.Abs(plungeZ));
    }

    // ---------------------------------------------------------------
    // 9. REMARK + OUTPUT
    // ---------------------------------------------------------------
    int arcCount  = 0;
    int lineCount = 0;
    for (int i = 0; i < pc.SegmentCount; i++)
    {
      if (pc.SegmentCurve(i) as ArcCurve != null) arcCount++;
      else lineCount++;
    }
    this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
      "Contour: " + pc.SegmentCount.ToString() + " segments ("
      + arcCount.ToString() + " arcs, " + lineCount.ToString() + " lines)"
      + "  depth=" + (-Math.Abs(depth)).ToString(CultureInfo.InvariantCulture)
      + "  toolDiam=" + toolDiameter.ToString(CultureInfo.InvariantCulture));

    operationLines = lines;
  }

  // ---------------------------------------------------------------
  // HELPER: Build one SP...moves...EP contour block at a given Z
  // ---------------------------------------------------------------
  private void BuildContourBlock(List<string> lines, PolyCurve pc, double zEintauch)
  {
    Point3d startPt = pc.PointAtStart;
    lines.Add("SP (" + startPt.X.ToString(CultureInfo.InvariantCulture) + ","
      + startPt.Y.ToString(CultureInfo.InvariantCulture) + ","
      + zEintauch.ToString(CultureInfo.InvariantCulture)
      + ",2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)");

    for (int i = 0; i < pc.SegmentCount; i++)
    {
      Curve seg = pc.SegmentCurve(i);

      ArcCurve arcSeg = seg as ArcCurve;
      if (arcSeg != null)
      {
        Arc arc    = arcSeg.Arc;
        Point3d ep = arc.EndPoint;
        Point3d cp = arc.Center;
        bool isCCW = arc.Plane.Normal.Z >= 0;
        string cmd = isCCW ? "G03M" : "G02M";
        lines.Add(cmd + " ("
          + ep.X.ToString(CultureInfo.InvariantCulture) + ","
          + ep.Y.ToString(CultureInfo.InvariantCulture) + ",0,"
          + cp.X.ToString(CultureInfo.InvariantCulture) + ","
          + cp.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,2,0)");
        continue;
      }

      LineCurve lineSeg = seg as LineCurve;
      if (lineSeg != null)
      {
        Point3d ep = lineSeg.PointAtEnd;
        lines.Add("G01 ("
          + ep.X.ToString(CultureInfo.InvariantCulture) + ","
          + ep.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,0,2)");
        continue;
      }

      Point3d fallback = seg.PointAtEnd;
      lines.Add("G01 ("
        + fallback.X.ToString(CultureInfo.InvariantCulture) + ","
        + fallback.Y.ToString(CultureInfo.InvariantCulture) + ",0,0,0,2)");
    }

    lines.Add("EP (0,_ANF,0)");
  }
}
