using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform;

/// <summary>Optional policy for windows whose placement and chrome belong to their native host.</summary>
/// <remarks>Query through ITopLevelImpl.TryGetFeature. Read on the UI thread.</remarks>
[PrivateApi]
public interface IWindowHostPolicy
{
    /// <summary>Gets whether the host owns placement, size and decorations instead of desktop chrome.</summary>
    bool IsHostManaged { get; }
}

/// <summary>Touch and keyboard transport implemented by the canonical framework input root.</summary>
/// <remarks>
/// Backend-facing extension to IInputRoot. Coordinates are logical client pixels. The root owns
/// focus, validation, text sessions and gesture routing; the backend never creates another tree.
/// All operations require the UI thread. Native detach must cancel input before retiring callbacks.
/// </remarks>
[PrivateApi]
public interface IWindowSurfaceInput
{
    /// <summary>Routes one stable touch identity through the existing shared gesture router.</summary>
    /// <param name="id">Native pointer identity, stable until release or cancellation.</param>
    /// <param name="action">The native transition; cancellation never produces a click.</param>
    /// <param name="position">Coordinates in logical client pixels, before safe-area offset.</param>
    void Pointer(int id, WindowSurfacePointerAction action, Point position);
    /// <summary>Routes a key without synthesizing characters or interpreting a keyboard layout.</summary>
    /// <param name="key">Platform-neutral physical/editing key identity.</param>
    /// <param name="modifiers">Modifiers supplied by the native event.</param>
    /// <param name="down">True for key-down, false for key-up.</param>
    /// <param name="textInput">True for IME editing input that bypasses shortcuts.</param>
    /// <param name="deadKey">True for a composing accent whose text belongs to the IME.</param>
    /// <param name="canceled">True for an aborted release; no normal activation occurs.</param>
    /// <returns>True when input was consumed or its route was retired.</returns>
    bool Key(Key key, KeyModifiers modifiers, bool down, bool textInput, bool deadKey, bool canceled);
    /// <summary>Cancels gestures and held keys without changing the canonical focused control.</summary>
    void CancelInput();
    /// <summary>Hides a popup using the shared visibility and text-ownership lifecycle.</summary>
    void DismissPopup();
}

/// <summary>A touch transition delivered to the canonical window root.</summary>
public enum WindowSurfacePointerAction
{
    /// <summary>A pointer touched the surface.</summary>
    Down,
    /// <summary>A held pointer moved.</summary>
    Move,
    /// <summary>A pointer was released.</summary>
    Up,
    /// <summary>A pointer sequence was canceled.</summary>
    Cancel
}
