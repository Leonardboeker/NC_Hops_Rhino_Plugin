using System;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

namespace DynesticPostProcessor
{
    /// <summary>
    /// Shared preview rendering helpers for all operation components.
    /// </summary>
    public static class PreviewHelper
    {
        /// <summary>
        /// Draw shaded breps into the mesh pass.
        /// </summary>
        public static void DrawMeshes(IGH_PreviewArgs args, List<Brep> volumes, Color color, double transparency = 0.45)
        {
            if (volumes == null || volumes.Count == 0) return;
            Rhino.Display.DisplayMaterial mat = new Rhino.Display.DisplayMaterial(color);
            mat.Transparency = transparency;
            foreach (Brep b in volumes)
                if (b != null) args.Display.DrawBrepShaded(b, mat);
        }

        /// <summary>
        /// Draw wireframe breps and dashed approach lines into the wire pass.
        /// </summary>
        public static void DrawWires(IGH_PreviewArgs args, List<Brep> volumes, List<Line> approachLines, Color color)
        {
            if (volumes != null)
                foreach (Brep b in volumes)
                    if (b != null) args.Display.DrawBrepWires(b, color, 1);

            if (approachLines != null)
                foreach (Line l in approachLines)
                    if (l.IsValid)
                        args.Display.DrawPatternedLine(
                            l.From, l.To,
                            Color.FromArgb(140, 140, 140), unchecked((int)0xF0F0F0F0), 1);
        }

        /// <summary>
        /// Compute combined bounding box from brep volumes and approach lines.
        /// </summary>
        public static BoundingBox GetClippingBox(List<Brep> volumes, List<Line> approachLines)
        {
            BoundingBox bb = BoundingBox.Empty;
            if (volumes != null)
                foreach (Brep b in volumes)
                    if (b != null) bb.Union(b.GetBoundingBox(true));
            if (approachLines != null)
                foreach (Line l in approachLines)
                    if (l.IsValid) { bb.Union(l.From); bb.Union(l.To); }
            return bb;
        }

        /// <summary>
        /// Build a slot (kerf) preview volume:
        /// - Offsets the rail curve by +halfWidth (left) and -halfWidth (right)
        /// - For open curves: connects ends with arc caps (tool radius = halfWidth)
        /// - Extrudes the resulting closed 2D boundary downward by slotDepth
        /// Works at any Z height by flattening to Z=0 and translating back.
        /// </summary>
        public static Brep BuildSlotPreview(Curve rail, double halfWidth, double slotDepth, double tol)
        {
            if (rail == null || halfWidth <= 0 || slotDepth <= 0) return null;

            // Flatten to Z=0 -- planar ops are more reliable there
            double topZ = rail.GetBoundingBox(true).Max.Z;
            Curve flat = rail.DuplicateCurve();
            flat.Translate(new Vector3d(0, 0, -topZ));

            Curve[] leftArr  = flat.Offset(Plane.WorldXY,  halfWidth, tol, CurveOffsetCornerStyle.Sharp);
            Curve[] rightArr = flat.Offset(Plane.WorldXY, -halfWidth, tol, CurveOffsetCornerStyle.Sharp);

            if (leftArr  == null || leftArr.Length  == 0) return null;
            if (rightArr == null || rightArr.Length == 0) return null;

            Curve[] leftJoined  = Curve.JoinCurves(leftArr,  tol);
            Curve[] rightJoined = Curve.JoinCurves(rightArr, tol);

            Curve leftOff  = (leftJoined  != null && leftJoined.Length  > 0) ? leftJoined[0]  : leftArr[0];
            Curve rightOff = (rightJoined != null && rightJoined.Length > 0) ? rightJoined[0] : rightArr[0];

            if (leftOff == null || rightOff == null) return null;

            Vector3d down = new Vector3d(0, 0, -Math.Abs(slotDepth));
            var faces = new List<Brep>();

            if (flat.IsClosed)
            {
                // Closed rail: top ring, bottom ring, outer wall, inner wall
                Brep[] topRing = Brep.CreatePlanarBreps(new Curve[] { leftOff, rightOff }, tol);
                if (topRing != null)
                    foreach (Brep b in topRing)
                        if (b != null) faces.Add(b);

                Curve lb = leftOff.DuplicateCurve();  lb.Translate(down);
                Curve rb = rightOff.DuplicateCurve(); rb.Translate(down);
                Brep[] botRing = Brep.CreatePlanarBreps(new Curve[] { lb, rb }, tol);
                if (botRing != null)
                    foreach (Brep b in botRing)
                        if (b != null) faces.Add(b);

                Surface lw = Surface.CreateExtrusion(leftOff,  down);
                Surface rw = Surface.CreateExtrusion(rightOff, down);
                if (lw != null) faces.Add(lw.ToBrep());
                if (rw != null) faces.Add(rw.ToBrep());
            }
            else
            {
                // Open rail: build stadium boundary with arc end caps
                Curve rightRev = rightOff.DuplicateCurve();
                rightRev.Reverse();

                Point3d lS = leftOff.PointAtStart;   // left side at rail start
                Point3d lE = leftOff.PointAtEnd;     // left side at rail end
                Point3d rS = rightOff.PointAtStart;  // right side at rail start
                Point3d rE = rightOff.PointAtEnd;    // right side at rail end

                Point3d railS = flat.PointAtStart;
                Point3d railE = flat.PointAtEnd;

                // End cap: semicircle from lE through railE to rE
                // Start cap: semicircle from rS through railS to lS
                Arc endArc   = new Arc(lE, railE, rE);
                Arc startArc = new Arc(rS, railS, lS);

                Curve endCap   = (endArc.IsValid   && endArc.Radius   > tol * 2)
                    ? (Curve)new ArcCurve(endArc)
                    : new LineCurve(lE, rE);
                Curve startCap = (startArc.IsValid && startArc.Radius > tol * 2)
                    ? (Curve)new ArcCurve(startArc)
                    : new LineCurve(rS, lS);

                Curve[] pieces = new Curve[] { leftOff, endCap, rightRev, startCap };
                Curve[] joined2D = Curve.JoinCurves(pieces, tol * 10);
                if (joined2D == null || joined2D.Length == 0) return null;

                Curve topLoop = joined2D[0];
                if (!topLoop.IsClosed) return null;

                Brep[] topFace = Brep.CreatePlanarBreps(new Curve[] { topLoop }, tol);
                if (topFace != null)
                    foreach (Brep b in topFace)
                        if (b != null) faces.Add(b);

                Curve botLoop = topLoop.DuplicateCurve();
                botLoop.Translate(down);
                Brep[] botFace = Brep.CreatePlanarBreps(new Curve[] { botLoop }, tol);
                if (botFace != null)
                    foreach (Brep b in botFace)
                        if (b != null) faces.Add(b);

                Surface sideWall = Surface.CreateExtrusion(topLoop, down);
                if (sideWall != null) faces.Add(sideWall.ToBrep());
            }

            if (faces.Count == 0) return null;

            Brep[] joinResult = Brep.JoinBreps(faces, tol * 10);
            Brep result = (joinResult != null && joinResult.Length > 0) ? joinResult[0] : faces[0];
            result.Translate(new Vector3d(0, 0, topZ));
            return result;
        }
    }
}
