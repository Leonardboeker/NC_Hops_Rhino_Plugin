// HopContour -- 2D contour cutting component for DYNESTIC CNC
// Inputs: curve (Item), depth (Item), plungeZ (Item), tolerance (Item),
//         toolNr (Item), toolDiameter (Item), side (Item), stepdown (Item), colour (Item)
// Outputs: operationLines
//
// side: -1 = left of travel direction (inside cut)
//        0 = center (tool center on curve, no offset)
//       +1 = right of travel direction (outside cut)
//
// When side != 0, the curve is geometrically pre-offset by toolDiameter/2
// before building the SP/G01/G03M blocks. The machine receives the offset path.
// Preview shows the actual material-removal volume on the chosen side.
//
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
    int toolNr, double toolDiameter, int side, double stepdown,
    Color colour,
    ref object operationLines)
  {
    string toolType   = "WZF";
    double feedFactor = 1.0;

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
        "toolNr must be > 0");
      return;
    }

    // ---------------------------------------------------------------
    // 3. INPUT DEFAULTS
    // ---------------------------------------------------------------
    if (tolerance    <= 0) tolerance    = 0.1;
    if (depth        <= 0) depth        = 1.0;
    if (plungeZ      <= 0) plungeZ      = depth;
    if (toolDiameter <= 0) toolDiameter = 8.0;
    // side: clamp to -1 / 0 / +1
    if (side > 0)  side =  1;
    if (side < 0)  side = -1;

    // ---------------------------------------------------------------
    // 4. PLANARITY CHECK
    // ---------------------------------------------------------------
    if (!curve.IsPlanar())
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        "Curve is not planar -- cannot use for 2D contour");
      return;
    }

    // ---------------------------------------------------------------
    // 5. GEOMETRIC PRE-OFFSET (when side != 0)
    //    Offset the input curve by toolDiameter/2 in the chosen direction.
    //    The machine receives the offset path -- tool center tracks it exactly.
    //    side = -1 (left / inside):  positive offset distance in Rhino convention
    //    side = +1 (right / outside): negative offset distance
    //    Note: Rhino Curve.Offset with positive distance offsets to the LEFT
    //    of the curve direction. Negate for right.
    // ---------------------------------------------------------------
    double tol    = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.01;
    double radius = toolDiameter / 2.0;

    Curve cuttingCurve = curve;  // curve actually sent to machine

    if (side != 0)
    {
      double offsetDist = side * radius;  // -1*r = left offset, +1*r = right offset
      Curve[] offsets = curve.Offset(Plane.WorldXY, offsetDist, tol,
        CurveOffsetCornerStyle.Sharp);
      if (offsets != null && offsets.Length > 0)
      {
        cuttingCurve = offsets.Length == 1 ? offsets[0]
          : Curve.JoinCurves(offsets, tol)[0];
      }
      else
      {
        this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
          "Side offset failed -- using center path");
      }
    }

    // ---------------------------------------------------------------
    // 6. PREVIEW VOLUME
    //    Z reference = curve's own Z position (surface of material).
    //    depth is subtracted from that -- so curves at Z=19 cut from Z=19 downward.
    //    This matches how the model is built: draw plate at correct Z, operations on top face.
    // ---------------------------------------------------------------
    double surfaceZ = curve.GetBoundingBox(true).Min.Z;
    double cutZ     = surfaceZ - Math.Abs(plungeZ > 0 ? plungeZ : depth);

    Curve baseCrv = curve.DuplicateCurve();
    baseCrv.Translate(new Vector3d(0, 0, cutZ - baseCrv.PointAtStart.Z));

    Curve cuttingBase = cuttingCurve.DuplicateCurve();
    cuttingBase.Translate(new Vector3d(0, 0, cutZ - cuttingBase.PointAtStart.Z));

    Curve innerBoundary;
    Curve outerBoundary;

    if (side == 0)
    {
      // Symmetric channel: offset both sides of original curve
      Curve[] outerArr = baseCrv.Offset(Plane.WorldXY,  radius, tol, CurveOffsetCornerStyle.Sharp);
      Curve[] innerArr = baseCrv.Offset(Plane.WorldXY, -radius, tol, CurveOffsetCornerStyle.Sharp);
      outerBoundary = (outerArr != null && outerArr.Length > 0) ? outerArr[0] : null;
      innerBoundary = (innerArr != null && innerArr.Length > 0) ? innerArr[0] : null;
    }
    else
    {
      // One-sided: between original curve and offset curve
      outerBoundary = baseCrv;
      innerBoundary = cuttingBase;
    }

    if (outerBoundary != null && innerBoundary != null)
    {
      Brep[] planar = Brep.CreatePlanarBreps(
        new Curve[] { outerBoundary, innerBoundary }, tol);
      if (planar != null && planar.Length > 0)
      {
        // CreateExtrusion takes a path Curve, not a Vector3d
        Vector3d extDir  = new Vector3d(0, 0, -Math.Abs(depth));
        LineCurve extPath = new LineCurve(new Line(Point3d.Origin, Point3d.Origin + extDir));
        Brep vol = planar[0].Faces[0].CreateExtrusion(extPath, true);
        if (vol != null) _previewVolume = vol;
      }
    }

    // Dashed approach line
    BoundingBox curveBB = curve.GetBoundingBox(true);
    double safeZ   = curveBB.Max.Z + 20.0;
    Point3d startP = cuttingBase.PointAtStart;
    _approachLine  = new Line(new Point3d(startP.X, startP.Y, safeZ), startP);

    // ---------------------------------------------------------------
    // 7. CURVE DECOMPOSITION on cuttingCurve (offset or original)
    // ---------------------------------------------------------------
    PolyCurve pc = cuttingCurve.ToArcsAndLines(tolerance, 0.1, 0.0, 0.0) as PolyCurve;
    if (pc == null)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        "Curve conversion to arcs and lines failed");
      return;
    }

    // ---------------------------------------------------------------
    // 8. BUILD NC OUTPUT
    // ---------------------------------------------------------------
    List<string> lines = new List<string>();
    lines.Add(toolType + " (" + toolNr.ToString()
      + ",_VE,_V*" + feedFactor.ToString(CultureInfo.InvariantCulture)
      + ",_VA,_SD,0,'')");

    // Z reference: surface at curve's own Z, depth subtracted from there
    if (stepdown > 0)
    {
      int passCount = (int)Math.Ceiling(depth / stepdown);
      for (int p = 0; p < passCount; p++)
      {
        double passDepth = Math.Min((p + 1) * stepdown, depth);
        BuildContourBlock(lines, pc, surfaceZ - passDepth);
      }
    }
    else
    {
      BuildContourBlock(lines, pc, surfaceZ - Math.Abs(plungeZ));
    }

    // ---------------------------------------------------------------
    // 9. REMARK + OUTPUT
    // ---------------------------------------------------------------
    string sideLabel = side == 0 ? "center" : (side < 0 ? "left (inside)" : "right (outside)");
    this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
      "Contour: " + pc.SegmentCount.ToString() + " segments"
      + "  depth=" + (-Math.Abs(depth)).ToString(CultureInfo.InvariantCulture)
      + "  side=" + sideLabel
      + "  toolD=" + toolDiameter.ToString(CultureInfo.InvariantCulture));

    operationLines = lines;
  }

  // ---------------------------------------------------------------
  // HELPER: Build one SP...moves...EP contour block
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
