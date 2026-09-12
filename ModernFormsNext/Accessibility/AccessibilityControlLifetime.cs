namespace ModernFormsNext.Accessibility;

// One short-lived action guard over existing control identity, not a retained accessibility tree.
internal sealed class AccessibilityControlLifetime
{
    private readonly List<(Control Control, long Version)> path = [];
    internal AccessibilityControlLifetime(Control owner)
    {
        for (Control? current = owner; current is not null; current = current.Parent)
        {
            if (path.Count == 512) throw new InvalidOperationException("The control ancestry exceeds the semantic operation limit.");
            path.Add((current, current.AccessibilityTreeVersion));
        }
    }
    internal bool IsCurrent => path.All(entry => !entry.Control.IsDisposed && !entry.Control.Disposing
        && entry.Control.AccessibilityTreeVersion == entry.Version
        && entry.Control.FindWindow()?.InputBindingsClosed != true);
}
