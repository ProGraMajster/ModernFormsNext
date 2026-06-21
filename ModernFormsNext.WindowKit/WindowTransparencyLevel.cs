using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ModernFormsNext.WindowKit.Controls;

/// <summary>
/// Describes a transparency effect requested for a top-level window background.
/// </summary>
/// <remarks>
/// Support is backend-specific. Windows is the primary supported backend, and individual
/// effects may fall back to a simpler level when the operating system or compositor does
/// not expose the requested visual effect.
/// </remarks>
public readonly record struct WindowTransparencyLevel
{
    private readonly string _value;

    private WindowTransparencyLevel(string value)
    {
        _value = value;
    }

    /// <summary>
    /// The window background is Black where nothing is drawn in the window.
    /// </summary>
    public static WindowTransparencyLevel None { get; } = new(nameof(None));

    /// <summary>
    /// The window background is Transparent where nothing is drawn in the window.
    /// </summary>
    public static WindowTransparencyLevel Transparent { get; } = new(nameof(Transparent));
        
    /// <summary>
    /// The window background is a blur-behind where nothing is drawn in the window.
    /// </summary>
    public static WindowTransparencyLevel Blur { get; } = new(nameof(Blur));
        
    /// <summary>
    /// The window background is a blur-behind with a high blur radius. This level may fallback to Blur.
    /// </summary>
    public static WindowTransparencyLevel AcrylicBlur { get; } = new(nameof(AcrylicBlur));

    /// <summary>
    /// The window background is based on desktop wallpaper tint with a blur. This currently requires Windows 11.
    /// </summary>
    public static WindowTransparencyLevel Mica { get; } = new(nameof(Mica));

    /// <summary>
    /// Returns the stable name of this transparency level.
    /// </summary>
    /// <returns>The platform-neutral transparency level name.</returns>
    public override string ToString()
    {
        return _value;
    }
}

/// <summary>
/// Represents an ordered set of transparency levels supported by a platform backend.
/// </summary>
/// <remarks>
/// Backends use this collection to report the levels they can apply. Applications should
/// prefer the first supported level that satisfies their visual requirement and gracefully
/// handle fallbacks.
/// </remarks>
public class WindowTransparencyLevelCollection : ReadOnlyCollection<WindowTransparencyLevel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WindowTransparencyLevelCollection"/> class.
    /// </summary>
    /// <param name="list">The ordered list of transparency levels to expose.</param>
    public WindowTransparencyLevelCollection(IList<WindowTransparencyLevel> list) : base(list)
    {
    }
} 
