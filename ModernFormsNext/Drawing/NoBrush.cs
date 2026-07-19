namespace ModernFormsNext.Drawing;

/// <summary>
/// Represents an explicit request to paint no fill.
/// </summary>
/// <remarks>
/// Assigning <see cref="NoBrush"/> differs from assigning <see langword="null"/>. A null brush
/// preserves the existing control behavior and uses its fallback color; <see cref="NoBrush"/>
/// leaves the paint area untouched. The type is platform-neutral and can be shared through dynamic
/// resources.
/// </remarks>
public sealed class NoBrush : Brush
{
    /// <summary>
    /// Initializes an explicit no-fill brush.
    /// </summary>
    public NoBrush()
    {
    }
}
