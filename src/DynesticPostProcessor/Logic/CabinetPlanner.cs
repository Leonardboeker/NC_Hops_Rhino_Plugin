using System;
using System.Collections.Generic;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure math layer for HopKorpusComponent. Computes panel sizing,
    /// hole positions, door dimensions, hinge layouts — everything that
    /// boils down to arithmetic over input dimensions.
    ///
    /// All Rhino-bound concerns (Brep, Transform, Plane, preview Breps,
    /// drill cylinder geometry) stay in HopKorpusComponent. The component
    /// queries this planner for numeric values, then applies them to
    /// Rhino types.
    /// </summary>
    internal static class CabinetPlanner
    {
        // -------------------------------------------------------------
        // INNER DIMENSIONS (butt-joint construction)
        // -------------------------------------------------------------

        internal struct InnerDims
        {
            public double InnerWidth;   // between side panels
            public double InnerHeight;  // between top and bottom
            public double InnerDepth;   // = outer depth (butt-joint construction)
        }

        internal static InnerDims ComputeInnerDimensions(double width, double height, double depth, double thickness)
        {
            return new InnerDims
            {
                InnerWidth  = width  - 2.0 * thickness,
                InnerHeight = height - 2.0 * thickness,
                InnerDepth  = depth,
            };
        }

        /// <summary>
        /// Validate cabinet dimensions. Returns null on success, error message string on failure.
        /// </summary>
        internal static string ValidateDimensions(double w, double h, double d, double thickness)
        {
            if (w <= 0 || h <= 0 || d <= 0)
                return "Dimensions must be > 0";
            double minDim = Math.Min(w, Math.Min(h, d));
            if (thickness <= 0 || thickness >= minDim / 2.0)
                return "Thickness invalid (must be > 0 and < half the smallest dimension)";
            return null;
        }

        // -------------------------------------------------------------
        // CONNECTORS
        // -------------------------------------------------------------

        /// <summary>
        /// Auto-derived connector count from cabinet depth.
        /// ≤300 mm → 2, ≤500 mm → 3, otherwise → 4.
        /// </summary>
        internal static int AutoConnectorCount(double depth)
        {
            if (depth <= 300) return 2;
            if (depth <= 500) return 3;
            return 4;
        }

        /// <summary>
        /// Even distribution of connector positions along the depth axis,
        /// inset by `edge` from both ends.
        /// </summary>
        internal static List<double> ConnectorPositions(double depth, int count, double edge)
        {
            count = Math.Max(1, count);
            var result = new List<double>();
            if (count == 1)
            {
                result.Add(depth / 2.0);
                return result;
            }
            double spacing = (depth - 2 * edge) / (count - 1);
            for (int i = 0; i < count; i++)
                result.Add(edge + i * spacing);
            return result;
        }

        // -------------------------------------------------------------
        // SYSTEM-32 HOLES
        // -------------------------------------------------------------

        /// <summary>
        /// Vertical Y positions for System-32 hole rows along the cabinet height,
        /// stepping by `raster` (typically 32 mm) starting at `edge` from bottom.
        /// </summary>
        internal static List<double> System32YPositions(double height, double edge, double raster)
        {
            var result = new List<double>();
            double yEnd = height - edge;
            for (double y = edge; y <= yEnd + 0.1; y += raster)
                result.Add(y);
            return result;
        }

        /// <summary>
        /// X positions of front + back System-32 columns. Back column is clamped
        /// to never come closer than one raster step to the front column.
        /// </summary>
        internal struct S32Columns
        {
            public double FrontX;
            public double BackX;
        }

        internal static S32Columns System32ColumnsX(double effectiveDepth, double edge, double raster)
        {
            return new S32Columns
            {
                FrontX = edge,
                BackX = Math.Max(edge + raster, effectiveDepth - edge),
            };
        }

        // -------------------------------------------------------------
        // FOOT PLATE CENTERS
        // -------------------------------------------------------------

        internal struct FootCenter
        {
            public double X;
            public double Y;
            public FootCenter(double x, double y) { X = x; Y = y; }
        }

        /// <summary>
        /// Foot plate center positions on the bottom panel (flat coords).
        /// 4 corner feet + 2 mid-edge feet for cabinets wider than 800 mm.
        /// </summary>
        internal static List<FootCenter> FootCenters(
            double cabinetWidth, double innerWidth, double depth, double footOffset)
        {
            var result = new List<FootCenter>
            {
                new FootCenter(footOffset,             footOffset),
                new FootCenter(innerWidth - footOffset, footOffset),
                new FootCenter(footOffset,             depth - footOffset),
                new FootCenter(innerWidth - footOffset, depth - footOffset),
            };
            if (cabinetWidth > 800)
            {
                result.Add(new FootCenter(innerWidth / 2.0, footOffset));
                result.Add(new FootCenter(innerWidth / 2.0, depth - footOffset));
            }
            return result;
        }

        // -------------------------------------------------------------
        // DOORS
        // -------------------------------------------------------------

        internal enum DoorOverlay
        {
            FullOverlay = 0,
            HalfOverlay = 1,
            Inset = 2,
        }

        internal struct DoorDims
        {
            public double Width;
            public double Height;
        }

        internal static DoorDims ComputeDoorDimensions(
            double cabinetW, double cabinetH,
            double innerW, double innerH,
            double thickness,
            int doorCount, double gap, DoorOverlay overlay)
        {
            int totalGaps = doorCount + 1;
            double w, h;
            switch (overlay)
            {
                case DoorOverlay.FullOverlay:
                    w = (cabinetW - totalGaps * gap) / doorCount;
                    h = cabinetH - 2 * gap;
                    break;
                case DoorOverlay.HalfOverlay:
                    w = (cabinetW - thickness - totalGaps * gap) / doorCount;
                    h = cabinetH - 2 * gap;
                    break;
                case DoorOverlay.Inset:
                default:
                    w = (innerW - totalGaps * gap) / doorCount;
                    h = innerH - 2 * gap;
                    break;
            }
            return new DoorDims
            {
                Width = Math.Max(1, w),
                Height = Math.Max(1, h),
            };
        }

        /// <summary>
        /// Auto-derived hinge count from door height.
        /// ≤900 → 2, ≤1500 → 3, ≤1800 → 4, else → 5.
        /// </summary>
        internal static int HingeCount(double doorHeight)
        {
            if (doorHeight <= 900) return 2;
            if (doorHeight <= 1500) return 3;
            if (doorHeight <= 1800) return 4;
            return 5;
        }

        /// <summary>
        /// Hinge Y positions on a door (flat panel coords, Y = height).
        /// Top and bottom hinges at firstPos and (height - firstPos);
        /// middle hinges evenly distributed between them. Sorted ascending.
        /// </summary>
        internal static List<double> HingeYPositions(double doorHeight, int hingeCount, double firstPos)
        {
            var result = new List<double>();
            if (hingeCount < 2) hingeCount = 2;
            result.Add(firstPos);
            result.Add(doorHeight - firstPos);
            if (hingeCount > 2)
            {
                double spacing = (doorHeight - 2 * firstPos) / (hingeCount - 1);
                for (int i = 1; i < hingeCount - 1; i++)
                    result.Add(firstPos + i * spacing);
                result.Sort();
            }
            return result;
        }

        // -------------------------------------------------------------
        // BACK PANEL EFFECTIVE DEPTH
        // -------------------------------------------------------------

        internal enum BackType
        {
            Inset = 0,
            Rabbeted = 1,
            Grooved = 2,
        }

        /// <summary>
        /// Distance from front opening to the front face of the back panel.
        /// Shelves and System-32 holes use this so they stop short of the back.
        /// </summary>
        internal static double EffectiveDepth(
            double cabinetDepth, double thickness,
            BackType type, double backThickness,
            double setback,        // Inset: distance from outer back to back panel rear face
            double falzWidth,      // Rabbeted: rabbet width
            double setbackDist)    // Grooved: distance from outer back to groove rear edge
        {
            double effective;
            switch (type)
            {
                case BackType.Inset:
                    effective = cabinetDepth - backThickness - setback;
                    break;
                case BackType.Rabbeted:
                    effective = cabinetDepth - falzWidth;
                    break;
                case BackType.Grooved:
                default:
                    effective = cabinetDepth - setbackDist - backThickness;
                    break;
            }
            return Math.Max(thickness + 10, effective);
        }
    }
}
