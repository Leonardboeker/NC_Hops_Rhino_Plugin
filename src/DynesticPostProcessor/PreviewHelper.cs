using System.Collections.Generic;
using System.Drawing;

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
    }
}
