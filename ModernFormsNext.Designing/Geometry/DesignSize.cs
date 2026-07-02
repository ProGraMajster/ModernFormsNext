namespace ModernFormsNext.Designing;

/// <summary>
/// Represents a size in designer logical pixels.
/// </summary>
/// <param name="Width">The width in logical pixels.</param>
/// <param name="Height">The height in logical pixels.</param>
/// <remarks>
/// Negative values are preserved by the model so validation can report normal
/// document errors without throwing exceptions during deserialization.
/// </remarks>
public readonly record struct DesignSize(int Width, int Height);
