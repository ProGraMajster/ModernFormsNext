using System;

namespace ModernFormsNext.WindowKit;

/// <summary>Describes native occlusion overlapping a window or surface, in logical pixels.</summary>
/// <remarks>
/// SafeArea combines persistent system bars and display cutouts. Ime reports temporary keyboard
/// occlusion separately; consumers choose their own keyboard avoidance policy. Insets already
/// excluded by native client-area fitting must not be reported again. The default value is empty.
/// This immutable value contains no native objects and may be retained across host recreation.
/// </remarks>
public readonly record struct WindowInsets
{
    /// <summary>Creates validated persistent and keyboard inset values.</summary>
    /// <param name="safeArea">Persistent occlusion on the four edges, in logical pixels.</param>
    /// <param name="ime">Temporary keyboard occlusion on the four edges, in logical pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">A side is negative or non-finite.</exception>
    public WindowInsets(Thickness safeArea, Thickness ime = default)
    {
        Validate(safeArea, nameof(safeArea));
        Validate(ime, nameof(ime));
        SafeArea = safeArea;
        Ime = ime;
    }

    /// <summary>Gets persistent occlusion from system bars and display cutouts.</summary>
    public Thickness SafeArea { get; }

    /// <summary>Gets temporary keyboard occlusion; it does not imply automatic content resizing.</summary>
    public Thickness Ime { get; }

    /// <summary>Gets the empty inset value used by fitted desktop client areas.</summary>
    public static WindowInsets None => default;

    private static void Validate(Thickness value, string name)
    {
        if (!double.IsFinite(value.Left) || value.Left < 0 ||
            !double.IsFinite(value.Top) || value.Top < 0 ||
            !double.IsFinite(value.Right) || value.Right < 0 ||
            !double.IsFinite(value.Bottom) || value.Bottom < 0)
            throw new ArgumentOutOfRangeException(name, "Inset sides must be finite and nonnegative.");
    }
}

/// <summary>Provides the current immutable logical inset snapshot after a native change.</summary>
/// <param name="insets">The current occlusion overlapping the native client surface.</param>
public sealed class WindowInsetsChangedEventArgs(WindowInsets insets) : EventArgs
{
    /// <summary>Gets the current logical inset values.</summary>
    public WindowInsets Insets { get; } = insets;
}
