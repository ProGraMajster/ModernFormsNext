namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Identifies a platform-neutral pointer transition emitted by an Android render surface.
/// </summary>
public enum AndroidPointerAction
{
    /// <summary>The pointer contacted the surface.</summary>
    Down,

    /// <summary>The pointer moved while contacting the surface.</summary>
    Move,

    /// <summary>The pointer left the surface normally.</summary>
    Up,

    /// <summary>Android cancelled the pointer sequence.</summary>
    Cancel
}
