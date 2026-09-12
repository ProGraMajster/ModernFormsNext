namespace ModernFormsNext.Accessibility;

/// <summary>Specifies direction and control-defined size for one axis.</summary>
public enum AccessibleScrollAmount
{
    /// <summary>Leaves this axis unchanged.</summary>
    None,
    /// <summary>Moves backward by the small amount.</summary>
    SmallDecrement,
    /// <summary>Moves forward by the small amount.</summary>
    SmallIncrement,
    /// <summary>Moves backward by the page amount.</summary>
    LargeDecrement,
    /// <summary>Moves forward by the page amount.</summary>
    LargeIncrement
}

/// <summary>Identifies the explicitly typed form of a scroll request.</summary>
public enum AccessibleScrollRequestKind
{
    /// <summary>Control-defined small/page increments.</summary>
    Amount,
    /// <summary>Absolute percentages, with null axes left unchanged.</summary>
    Percent,
    /// <summary>Signed multiples of the actual visible region.</summary>
    Viewport
}

/// <summary>Contains one immutable, finite request for AccessibleActions.Scroll.</summary>
/// <remarks>Only the owning peer performs movement; construction does not access controls. Large legal requests clamp at endpoints.</remarks>
public sealed class AccessibleScrollRequest
{
    private AccessibleScrollRequest(AccessibleScrollRequestKind kind, double? horizontal, double? vertical,
        AccessibleScrollAmount horizontalAmount = default, AccessibleScrollAmount verticalAmount = default)
    { Kind = kind; Horizontal = horizontal; Vertical = vertical; HorizontalAmount = horizontalAmount; VerticalAmount = verticalAmount; }
    /// <summary>Gets the request form.</summary>
    public AccessibleScrollRequestKind Kind { get; }
    /// <summary>Gets horizontal percent/pages; null leaves the axis unchanged.</summary>
    public double? Horizontal { get; }
    /// <summary>Gets vertical percent/pages; null leaves the axis unchanged.</summary>
    public double? Vertical { get; }
    /// <summary>Gets the horizontal amount for an Amount request.</summary>
    public AccessibleScrollAmount HorizontalAmount { get; }
    /// <summary>Gets the vertical amount for an Amount request.</summary>
    public AccessibleScrollAmount VerticalAmount { get; }
    /// <summary>Creates a small/page request; None leaves an axis unchanged.</summary>
    /// <param name="horizontal">Horizontal amount.</param><param name="vertical">Vertical amount.</param>
    /// <returns>A validated request.</returns>
    public static AccessibleScrollRequest ByAmount(AccessibleScrollAmount horizontal, AccessibleScrollAmount vertical)
    {
        if (!Enum.IsDefined(horizontal)) throw new ArgumentOutOfRangeException(nameof(horizontal));
        if (!Enum.IsDefined(vertical)) throw new ArgumentOutOfRangeException(nameof(vertical));
        return new(AccessibleScrollRequestKind.Amount, null, null, horizontal, vertical);
    }
    /// <summary>Creates an absolute percentage request. Null means unchanged; valid percentages are zero through 100.</summary>
    /// <param name="horizontal">Horizontal percent or null.</param><param name="vertical">Vertical percent or null.</param>
    /// <returns>A validated request.</returns>
    public static AccessibleScrollRequest ToPercent(double? horizontal, double? vertical)
    {
        Validate(horizontal, true, nameof(horizontal)); Validate(vertical, true, nameof(vertical));
        return new(AccessibleScrollRequestKind.Percent, horizontal, vertical);
    }
    /// <summary>Creates signed finite viewport fractions; zero leaves that axis unchanged.</summary>
    /// <param name="horizontal">Horizontal visible-region multiples.</param><param name="vertical">Vertical visible-region multiples.</param>
    /// <returns>A validated request.</returns>
    public static AccessibleScrollRequest ByViewport(double horizontal, double vertical)
    {
        Validate(horizontal, false, nameof(horizontal)); Validate(vertical, false, nameof(vertical));
        return new(AccessibleScrollRequestKind.Viewport, horizontal, vertical);
    }
    private static void Validate(double? value, bool percent, string name)
    { if (value is double number && (!double.IsFinite(number) || percent && (number < 0 || number > 100))) throw new ArgumentOutOfRangeException(name); }

    internal bool TryGetOffset(AccessibleScrollAxis axis, bool horizontal, out double? offset)
    {
        offset = null;
        var amount = horizontal ? HorizontalAmount : VerticalAmount;
        var number = horizontal ? Horizontal : Vertical;
        if (Kind == AccessibleScrollRequestKind.Amount && amount == AccessibleScrollAmount.None
            || Kind == AccessibleScrollRequestKind.Percent && number is null
            || Kind == AccessibleScrollRequestKind.Viewport && number == 0) return true;
        if (!axis.IsScrollable) return false;
        double target = Kind switch
        {
            AccessibleScrollRequestKind.Percent => axis.Minimum + (axis.Maximum - axis.Minimum) * (number!.Value / 100),
            AccessibleScrollRequestKind.Viewport => axis.Offset + number!.Value * axis.ViewportLength,
            _ => axis.Offset + (amount switch
            {
                AccessibleScrollAmount.SmallDecrement => -axis.SmallChange,
                AccessibleScrollAmount.SmallIncrement => axis.SmallChange,
                AccessibleScrollAmount.LargeDecrement => -axis.LargeChange,
                AccessibleScrollAmount.LargeIncrement => axis.LargeChange,
                _ => 0
            })
        };
        offset = Math.Clamp(target, axis.Minimum, axis.Maximum);
        return true;
    }
}
