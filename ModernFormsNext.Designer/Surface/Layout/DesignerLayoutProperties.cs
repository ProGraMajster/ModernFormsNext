using ModernFormsNext;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using ModernFormsNext.Layout;
using System.Drawing;

namespace ModernFormsNext.Designer.Surface;

internal static class DesignerLayoutProperties
{
    public const string DockPropertyName = "Dock";

    public const string AnchorPropertyName = "Anchor";

    public const string PaddingPropertyName = "Padding";

    public const string MarginPropertyName = "Margin";

    public const string MinimumSizePropertyName = "MinimumSize";

    public const string MaximumSizePropertyName = "MaximumSize";

    public const string VisiblePropertyName = "Visible";

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

    public static AnchorStyles GetAnchor(DesignControlNode node)
    {
        if (!node.Properties.TryGetValue(AnchorPropertyName, out var value))
            return AnchorStyles.Top | AnchorStyles.Left;

        try
        {
            if (value.Kind is DesignPropertyValueKind.Enum or DesignPropertyValueKind.String
                && value.Value is string anchorName)
            {
                return Enum.TryParse<AnchorStyles>(anchorName, ignoreCase: false, out var anchor)
                    ? anchor
                    : AnchorStyles.Top | AnchorStyles.Left;
            }
        }
        catch
        {
            return AnchorStyles.Top | AnchorStyles.Left;
        }

        return AnchorStyles.Top | AnchorStyles.Left;
    }

    public static Padding GetPadding(DesignControlNode node)
        => GetPadding(node.Properties);

    public static Padding GetMargin(DesignControlNode node)
        => GetPadding(node.Properties, MarginPropertyName, new Padding(3));

    public static Padding GetPadding(IReadOnlyDictionary<string, DesignPropertyValue> properties)
        => GetPadding(properties, PaddingPropertyName, Padding.Empty);

    public static bool IsVisible(DesignControlNode node)
    {
        if (!node.Properties.TryGetValue(VisiblePropertyName, out var value))
            return true;

        return value.Kind switch
        {
            DesignPropertyValueKind.Boolean when value.Value is bool visible => visible,
            DesignPropertyValueKind.String when bool.TryParse(value.ToString(), out var visible) => visible,
            _ => true
        };
    }

    public static Size GetMinimumSize(DesignControlNode node)
        => GetSize(node, MinimumSizePropertyName);

    public static Size GetMaximumSize(DesignControlNode node)
        => GetSize(node, MaximumSizePropertyName);

    public static DesignBounds ApplySizeConstraints(DesignControlNode node, DesignBounds bounds)
    {
        var minimum = GetMinimumSize(node);
        var maximum = GetMaximumSize(node);
        var width = maximum.Width > 0 ? Math.Min(bounds.Width, maximum.Width) : bounds.Width;
        var height = maximum.Height > 0 ? Math.Min(bounds.Height, maximum.Height) : bounds.Height;

        width = Math.Max(width, minimum.Width);
        height = Math.Max(height, minimum.Height);

        return new DesignBounds(bounds.X, bounds.Y, Math.Max(0, width), Math.Max(0, height));
    }

    private static Padding GetPadding(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        string propertyName,
        Padding defaultValue)
    {
        if (!properties.TryGetValue(propertyName, out var value))
            return defaultValue;

        try
        {
            return DesignerPropertyValueEditor.FromDesignPropertyValue(value, typeof(Padding)) is Padding padding
                ? padding
                : defaultValue;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return defaultValue;
        }
    }

    private static Size GetSize(DesignControlNode node, string propertyName)
    {
        if (!node.Properties.TryGetValue(propertyName, out var value))
            return Size.Empty;

        try
        {
            return DesignerPropertyValueEditor.FromDesignPropertyValue(value, typeof(Size)) is Size size
                ? size
                : Size.Empty;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return Size.Empty;
        }
    }

    public static DesignBounds GetPaddedContentBounds(DesignBounds bounds, Padding padding)
    {
        // Control.Padding uses this same normalization before ScrollableControl.DisplayRectangle
        // deflates its runtime client area. Reuse both helpers so the Designer follows the runtime
        // geometry rules; final dimensions are then limited by DesignBounds' nonnegative contract.
        padding = LayoutUtils.ClampNegativePaddingToZero(padding);
        var content = LayoutUtils.DeflateRect(
            new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            padding);

        return new DesignBounds(
            content.X,
            content.Y,
            Math.Max(0, content.Width),
            Math.Max(0, content.Height));
    }

    public static DesignBounds GetContainerContentBounds(DesignControlNode container, DesignBounds bounds)
        => UsesPaddedDisplayRectangle(container.TypeName)
            ? GetPaddedContentBounds(bounds, GetPadding(container))
            : bounds;

    private static bool UsesPaddedDisplayRectangle(string typeName)
    {
        // Resolve only framework types from the already loaded assembly. Project UserControls are
        // rendered from their .mfdesign data and must never be loaded or instantiated here.
        var normalized = DesignerProjectUserControlDiscovery.NormalizeTypeName(typeName);
        var frameworkAssembly = typeof(Control).Assembly;
        var type = frameworkAssembly.GetType(normalized, throwOnError: false)
            ?? frameworkAssembly.GetType($"ModernFormsNext.{normalized}", throwOnError: false);

        return type is not null && typeof(ScrollableControl).IsAssignableFrom(type);
    }

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
