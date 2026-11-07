namespace ModernFormsNext.Drawing;

// Sharing is supported, but cycles would recurse during invalidation as well as conversion.
// Validate before changing ownership or subscriptions, without invoking custom geometry code.
internal static class GeometryGraph
{
    internal static void RequireAcyclic(Geometry owner, Geometry? candidate)
    {
        if (candidate is null) return;
        var pending = new Stack<Geometry>();
        var visited = new HashSet<Geometry>(ReferenceEqualityComparer.Instance);
        pending.Push(candidate);
        while (pending.TryPop(out var current))
        {
            if (ReferenceEquals(current, owner)) throw new ArgumentException("Geometry composition cannot contain a cycle.", nameof(candidate));
            if (!visited.Add(current)) continue;
            if (current is GeometryGroup group)
                foreach (var child in group.Children) pending.Push(child);
            else if (current is CombinedGeometry combined)
            {
                if (combined.Geometry1 is { } first) pending.Push(first);
                if (combined.Geometry2 is { } second) pending.Push(second);
            }
        }
    }
}
