using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WallabyHop.Logic
{
    /// <summary>
    /// Pure formatter for the cabinet material cut list. Takes plain
    /// part data (name, thickness, dx/dy in mm) and produces the
    /// human-readable string grouped by thickness.
    ///
    /// Note: the legacy implementation in HopDrawingComponent parsed
    /// its own formatted output to compute the per-group sum. This
    /// version keeps the running total in a variable — equivalent
    /// output, no string round-trip.
    /// </summary>
    internal static class MaterialListBuilder
    {
        internal struct Part
        {
            public string Name;
            public double Thickness;   // mm
            public double DxMm;        // outline width in mm
            public double DyMm;        // outline height in mm
        }

        internal static string Build(IReadOnlyList<Part> parts)
        {
            if (parts == null || parts.Count == 0)
                return "(no Parts connected)";

            // Group by thickness, ascending
            var groups = new SortedDictionary<double, List<Part>>();
            foreach (var p in parts)
            {
                if (!groups.ContainsKey(p.Thickness))
                    groups[p.Thickness] = new List<Part>();
                groups[p.Thickness].Add(p);
            }

            var sb = new StringBuilder();
            foreach (var kv in groups)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "t={0:F0}mm:", kv.Key));

                double sumM2 = 0;
                foreach (var part in kv.Value)
                {
                    double m2 = (part.DxMm / 1000.0) * (part.DyMm / 1000.0);
                    sumM2 += m2;
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "  {0,-14} {1:F0}x{2:F0}={3:F3}m²",
                        part.Name, part.DxMm, part.DyMm, m2));
                }

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  Σ {0} parts / {1:F3}m²", kv.Value.Count, sumM2));
            }
            return sb.ToString().TrimEnd();
        }
    }
}
