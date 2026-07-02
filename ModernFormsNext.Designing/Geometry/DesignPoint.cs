namespace ModernFormsNext.Designing;

/// <summary>
/// Represents a point in designer logical pixels.
/// </summary>
/// <param name="X">The horizontal coordinate in logical pixels.</param>
/// <param name="Y">The vertical coordinate in logical pixels.</param>
/// <remarks>
/// Designer coordinates are platform-neutral and intentionally match the unscaled
/// coordinates used by ModernFormsNext controls.
/// </remarks>
public readonly record struct DesignPoint(int X, int Y);
