using System.Globalization;

namespace ModernFormsNext.Designing;

public sealed partial class DesignDocumentValidator
{
    /// <summary>Gets the hard conservative vector-work budget shared by one validation projection.</summary>
    /// <remarks>
    /// Shared operands count each time they expand. Boolean output complexity is conservatively
    /// estimated as sum + six times sum squared, including nested operations and self intersections.
    /// Lines, quadratics and cubics have degree weights 1, 2 and 3; arcs reserve four cubics. This is an
    /// admission estimate, not elapsed time or a promise of an exact native allocation count.
    /// It constrains Designer data only; typed runtime geometry remains unrestricted by this policy.
    /// </remarks>
    public const long MaximumGeometryWork = 65_536;

    // Called only after the existing iterative kind/cycle/depth/value preflight. Memoization
    // avoids repeated analysis; parent sums still charge every expanded occurrence in a DAG.
    private static string? ValidateGeometryWork(DesignPropertyValue root, out long totalWork)
    {
        totalWork = 0;
        var costs = new Dictionary<DesignPropertyValue, (long Work, double Extent)>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<(DesignPropertyValue Value, bool Exit)>();
        pending.Push((root, false));
        while (pending.TryPop(out var item))
        {
            if (costs.ContainsKey(item.Value)) continue;
            if (!item.Exit && item.Value.ObjectProperties is { } children)
            {
                pending.Push((item.Value, true));
                foreach (var child in children.Values) if (!costs.ContainsKey(child)) pending.Push((child, false));
                continue;
            }
            long work = 0;
            double extent = 0;
            if (item.Value.ObjectProperties is { } properties)
            {
                foreach (var child in properties)
                {
                    if (child.Key == "Transform") continue;
                    var cost = costs[child.Value];
                    work += cost.Work;
                    extent = Math.Max(extent, cost.Extent);
                }
                if (IsStructuredValue(item.Value, "System.Drawing.PointF") || IsStructuredValue(item.Value, "System.Drawing.SizeF") || IsStructuredValue(item.Value, "System.Drawing.RectangleF"))
                    foreach (var value in properties.Values)
                        if (IsFiniteFloat(value)) extent = Math.Max(extent, Math.Abs(Convert.ToDouble(value.Value, CultureInfo.InvariantCulture)) * 2);
                if (IsStructuredValue(item.Value, "ModernFormsNext.Drawing.ArcSegment")) work += 12;
                else if (IsStructuredValue(item.Value, "ModernFormsNext.Drawing.PathFigure")) work += 2;
                else if (IsStructuredValue(item.Value, "ModernFormsNext.Drawing.LineSegment")) work++;
                else if (IsStructuredValue(item.Value, "ModernFormsNext.Drawing.QuadraticBezierSegment")) work += 2;
                else if (IsStructuredValue(item.Value, "ModernFormsNext.Drawing.BezierSegment")) work += 3;
                else if (IsStructuredValue(item.Value, "ModernFormsNext.Drawing.RectangleGeometry")) work += 4;
                else if (IsStructuredValue(item.Value, "ModernFormsNext.Drawing.EllipseGeometry")) work += 12;
                else if (IsStructuredValue(item.Value, "ModernFormsNext.Drawing.LineGeometry")) work++;
                if (IsStructuredValue(item.Value, "ModernFormsNext.Drawing.CombinedGeometry"))
                {
                    long left = properties.TryGetValue("Geometry1", out var first) ? costs[first].Work : 0;
                    long right = properties.TryGetValue("Geometry2", out var second) ? costs[second].Work : 0;
                    // Intersections within either operand also split curves, even with an empty
                    // other operand. Degree-weighted squared work reserves both resulting paths'
                    // pieces before native Op can allocate them; shared subtrees are charged again.
                    long sum = left + right;
                    work += 6 * sum * sum;
                }
                if (IsGeometryValue(item.Value) && properties.TryGetValue("Transform", out var transform))
                {
                    var matrix = transform.ObjectProperties!;
                    double Read(string name, double fallback = 0) => matrix.TryGetValue(name, out var number) ? Convert.ToDouble(number.Value, CultureInfo.InvariantCulture) : fallback;
                    double linear = Math.Max(Math.Abs(Read("M11", 1)) + Math.Abs(Read("M21")), Math.Abs(Read("M12")) + Math.Abs(Read("M22", 1)));
                    extent = extent * linear + Math.Max(Math.Abs(Read("M31")), Math.Abs(Read("M32")));
                }
            }
            if (work > MaximumGeometryWork) return "Designer geometry exceeds the conservative expanded vector-work budget.";
            if (IsGeometryValue(item.Value) && (!double.IsFinite(extent) || extent > 1_000_000_000))
                return "Designer geometry exceeds the conservative finite coordinate extent.";
            costs.Add(item.Value, (work, extent));
        }
        totalWork = costs[root].Work;
        return null;
    }
}
