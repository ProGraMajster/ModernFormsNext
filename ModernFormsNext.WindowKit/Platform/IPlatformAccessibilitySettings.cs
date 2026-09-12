namespace ModernFormsNext.WindowKit.Platform;

/// <summary>Optionally exposes detected accessibility preferences on an existing platform settings provider.</summary>
/// <remarks>
/// Resolve <see cref="IPlatformSettings"/> and test this capability; legacy providers remain valid.
/// Reads return detached immutable data and do not apply themes, resize controls, or change explicit
/// fonts. Subscribe and unsubscribe on the platform UI thread. Notifications run on that thread
/// after the snapshot changes; consumers own their subscription and choose whether to apply it.
/// A missing capability or null snapshot field means unavailable detection, not a normal preference.
/// </remarks>
public interface IPlatformAccessibilitySettings
{
    /// <summary>Gets the latest detected values. Native providers may refresh them on their UI thread.</summary>
    /// <returns>A detached snapshot with independently nullable color and text-scale data.</returns>
    PlatformAccessibilityPreferences GetAccessibilityPreferences();

    /// <summary>Reports changed detected values without automatically changing application styling.</summary>
    event EventHandler<PlatformAccessibilityPreferences>? AccessibilityPreferencesChanged;
}

/// <summary>Contains immutable detected accessibility preferences without native object references.</summary>
public sealed record PlatformAccessibilityPreferences
{
    /// <summary>Creates a detached preference snapshot.</summary>
    /// <param name="colorValues">Detected colors/contrast, or null when unavailable. Known fields are copied.</param>
    /// <param name="textScale">A positive finite text-size multiplier, or null when unavailable. This is not display density.</param>
    /// <exception cref="ArgumentOutOfRangeException">The scale or a color classification is invalid.</exception>
    public PlatformAccessibilityPreferences(PlatformColorValues? colorValues = null, double? textScale = null)
    {
        if (textScale is { } scale && (!double.IsFinite(scale) || scale <= 0))
            throw new ArgumentOutOfRangeException(nameof(textScale));
        if (colorValues is { } colors)
        {
            if (!Enum.IsDefined(colors.ThemeVariant) || !Enum.IsDefined(colors.ContrastPreference))
                throw new ArgumentOutOfRangeException(nameof(colorValues));
            ColorValues = colors.Detach();
        }
        TextScale = textScale;
    }

    /// <summary>Gets copied detected color/contrast values, or null when unavailable.</summary>
    public PlatformColorValues? ColorValues { get; }

    /// <summary>Gets the detected text multiplier, or null when unavailable.</summary>
    public double? TextScale { get; }
}
