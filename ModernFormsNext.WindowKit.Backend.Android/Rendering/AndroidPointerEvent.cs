namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Describes one Android pointer in density-independent logical coordinates.
/// </summary>
/// <param name="PointerId">The Android pointer identifier, which remains stable for its gesture.</param>
/// <param name="Action">The pointer transition.</param>
/// <param name="X">The horizontal coordinate in logical pixels.</param>
/// <param name="Y">The vertical coordinate in logical pixels.</param>
/// <param name="IsPrimary">Whether this is the primary pointer for the current gesture.</param>
public readonly record struct AndroidPointerEvent(
    int PointerId,
    AndroidPointerAction Action,
    float X,
    float Y,
    bool IsPrimary);
