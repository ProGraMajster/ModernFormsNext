using ModernFormsNext;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

internal static class DesignerLayoutProperties
{
    public const string DockPropertyName = "Dock";

    public static DockStyle GetDock(DesignControlNode node)
    {
        if (!node.Properties.TryGetValue(DockPropertyName, out var value))
            return DockStyle.None;

        try
        {
            if (value.Kind == DesignPropertyValueKind.Enum && value.Value is string enumName)
                return Enum.TryParse<DockStyle>(enumName, ignoreCase: false, out var dock) ? dock : DockStyle.None;

            if (value.Kind == DesignPropertyValueKind.String && value.Value is string stringValue)
                return Enum.TryParse<DockStyle>(stringValue, ignoreCase: false, out var dock) ? dock : DockStyle.None;
        }
        catch
        {
            return DockStyle.None;
        }

        return DockStyle.None;
    }

    public static bool IsDocked(DesignControlNode node)
        => GetDock(node) != DockStyle.None;

    public static IReadOnlyList<DesignerResizeHandle> GetResizeHandles(DesignControlNode node)
        => GetDock(node) switch
        {
            DockStyle.Top => [DesignerResizeHandle.Bottom],
            DockStyle.Bottom => [DesignerResizeHandle.Top],
            DockStyle.Left => [DesignerResizeHandle.Right],
            DockStyle.Right => [DesignerResizeHandle.Left],
            DockStyle.Fill => [],
            _ =>
            [
                DesignerResizeHandle.TopLeft,
                DesignerResizeHandle.Top,
                DesignerResizeHandle.TopRight,
                DesignerResizeHandle.Right,
                DesignerResizeHandle.BottomRight,
                DesignerResizeHandle.Bottom,
                DesignerResizeHandle.BottomLeft,
                DesignerResizeHandle.Left
            ]
        };

    public static bool CanResize(DesignControlNode node, DesignerResizeHandle handle)
        => GetResizeHandles(node).Contains(handle);
}
