namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Converts Android physical-pixel values to framework logical pixels.
/// </summary>
public static class AndroidDensityConverter
{
    /// <summary>
    /// Converts a physical coordinate or length to logical pixels.
    /// </summary>
    /// <param name="physicalPixels">The Android value in physical pixels.</param>
    /// <param name="density">The display density scale. A value of 1 represents 160 DPI.</param>
    /// <returns>The corresponding value in logical pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="density"/> is not positive and finite.</exception>
    public static float ToLogical(float physicalPixels, float density)
    {
        if (!float.IsFinite(density) || density <= 0)
            throw new ArgumentOutOfRangeException(nameof(density), "Android display density must be positive and finite.");

        return physicalPixels / density;
    }
}
