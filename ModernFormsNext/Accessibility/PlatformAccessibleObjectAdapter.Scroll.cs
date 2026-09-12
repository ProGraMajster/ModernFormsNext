using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.Accessibility;

internal sealed partial class PlatformAccessibleObjectAdapter : IPlatformAccessibilityScroll
{
    public int? Orientation => accessible_object.Orientation is { } orientation ? (int)orientation : null;
    public PlatformAccessibleScrollInfo? ScrollInfo
    {
        get
        {
            if (accessible_object.IsSensitive || accessible_object.ScrollInfo is not { } scroll) return null;
            if (accessible_object.IsSensitive) return null;
            static PlatformAccessibleScrollAxis Axis(AccessibleScrollAxis a)
                => new(a.Offset, a.Minimum, a.Maximum, a.ViewportLength, a.SmallChange, a.LargeChange);
            var b = scroll.ViewportBounds;
            return new(Axis(scroll.Horizontal), Axis(scroll.Vertical), new Rect(b.X, b.Y, b.Width, b.Height));
        }
    }
    private static AccessibleScrollRequest ConvertScrollRequest(PlatformAccessibleScrollRequest request)
        => request.Kind switch
        {
            0 when request.Horizontal is null && request.Vertical is null => AccessibleScrollRequest.ByAmount(
                (AccessibleScrollAmount)request.HorizontalAmount, (AccessibleScrollAmount)request.VerticalAmount),
            1 when request.HorizontalAmount == 0 && request.VerticalAmount == 0 => AccessibleScrollRequest.ToPercent(request.Horizontal, request.Vertical),
            2 when request.HorizontalAmount == 0 && request.VerticalAmount == 0 && request.Horizontal.HasValue && request.Vertical.HasValue
                => AccessibleScrollRequest.ByViewport(request.Horizontal.Value, request.Vertical.Value),
            _ => throw new ArgumentException("Invalid viewport request.", nameof(request))
        };
}

public partial class AccessibleObject
{
    /// <summary>Gets optional orientation, or null when this concept does not apply.</summary>
    public virtual Orientation? Orientation => null;
}
