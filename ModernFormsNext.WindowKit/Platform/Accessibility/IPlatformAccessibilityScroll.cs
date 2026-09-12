namespace ModernFormsNext.WindowKit.Platform.Accessibility;

/// <summary>Optional viewport semantics on the same canonical object; no additional tree or lifetime.</summary>
internal interface IPlatformAccessibilityScroll
{
    PlatformAccessibleScrollInfo? ScrollInfo { get; }
    int? Orientation { get; }
}

internal readonly record struct PlatformAccessibleScrollAxis(double Offset, double Minimum, double Maximum,
    double ViewportLength, double SmallChange, double LargeChange)
{
    internal bool IsValid => double.IsFinite(Offset) && double.IsFinite(Minimum) && double.IsFinite(Maximum)
        && Maximum >= Minimum && Offset >= Minimum && Offset <= Maximum && double.IsFinite(Maximum - Minimum)
        && double.IsFinite(ViewportLength) && ViewportLength >= 0 && double.IsFinite(ViewportLength + Maximum - Minimum)
        && double.IsFinite(SmallChange) && SmallChange >= 0 && double.IsFinite(LargeChange) && LargeChange >= 0;
    internal bool IsScrollable => IsValid && Maximum > Minimum && ViewportLength > 0;
    internal double Percent => IsScrollable ? (Offset - Minimum) / (Maximum - Minimum) * 100 : -1;
    internal double ViewPercent => IsScrollable ? ViewportLength / (ViewportLength + Maximum - Minimum) * 100 : 100;
}

internal readonly record struct PlatformAccessibleScrollInfo(PlatformAccessibleScrollAxis Horizontal,
    PlatformAccessibleScrollAxis Vertical, Rect ViewportBounds)
{
    internal bool IsValid => Horizontal.IsValid && Vertical.IsValid
        && double.IsFinite(ViewportBounds.X) && double.IsFinite(ViewportBounds.Y)
        && double.IsFinite(ViewportBounds.Width) && double.IsFinite(ViewportBounds.Height)
        && double.IsFinite(ViewportBounds.Right) && double.IsFinite(ViewportBounds.Bottom)
        && ViewportBounds.Width >= 0 && ViewportBounds.Height >= 0;
}

// Kind: 0=small/page amount, 1=percentage, 2=visible-region fractions.
// Amount: 0=none, 1=small decrement, 2=small increment, 3=large decrement, 4=large increment.
internal readonly record struct PlatformAccessibleScrollRequest(int Kind, double? Horizontal, double? Vertical,
    int HorizontalAmount = 0, int VerticalAmount = 0);

internal static class PlatformAccessibilityScrollExtensions
{
    internal static PlatformAccessibleScrollInfo? GetScrollInfo(this IPlatformAccessibleObject node)
    {
        if (PlatformAccessibilityPrivacy.HasSensitiveAncestor(node)) return null;
        var result = (node as IPlatformAccessibilityScroll)?.ScrollInfo;
        return result is { IsValid: true } && !PlatformAccessibilityPrivacy.HasSensitiveAncestor(node) ? result : null;
    }
    internal static int? GetOrientation(this IPlatformAccessibleObject node)
        => (node as IPlatformAccessibilityScroll)?.Orientation;
}
