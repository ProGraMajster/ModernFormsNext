namespace ModernFormsNext;

/// <summary>
/// Identifies a pointer transition delivered to a platform-hosted <see cref="SkiaControlSurface"/>.
/// </summary>
public enum ControlSurfacePointerAction
{
    /// <summary>The primary pointer contacted the surface.</summary>
    Down,

    /// <summary>The primary pointer moved over the surface.</summary>
    Move,

    /// <summary>The primary pointer left the surface normally.</summary>
    Up,

    /// <summary>The platform cancelled the active pointer sequence.</summary>
    Cancel
}
