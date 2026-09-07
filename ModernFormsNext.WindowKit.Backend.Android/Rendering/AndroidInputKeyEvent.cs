using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>Describes an Android editing-key transition.</summary>
/// <param name="Key">The platform-neutral editing or navigation key.</param>
/// <param name="IsDown"><see langword="true"/> for key down; <see langword="false"/> for key up.</param>
public readonly record struct AndroidInputKeyEvent(AndroidInputKey Key, bool IsDown)
{
    /// <summary>Gets keyboard modifiers, including the conservative right-Alt/AltGraph marker.</summary>
    public KeyModifiers Modifiers { get; init; }

    /// <summary>Gets whether this transition came from a physical device through the view key path.</summary>
    /// <remarks>
    /// Only hardware transitions are eligible for input bindings. InputConnection/soft-keyboard
    /// events remain editing input. The existing two-argument constructor defaults to editing input.
    /// The backend currently forwards only its existing editing/navigation key set.
    /// </remarks>
    public bool IsHardwareKey { get; init; }

    // Keep source classification independent of Android runtime objects so all hosts share the
    // same conservative rule. InputConnection events never become shortcut input, even when an
    // IME supplies a real-looking device id or modifier flags.
    internal static AndroidInputKeyEvent FromSource(AndroidInputKey key, bool isDown,
        KeyModifiers modifiers, int deviceId, bool isSoftKeyboard, bool fromInputConnection)
    {
        bool hardware = deviceId >= 0 && !isSoftKeyboard && !fromInputConnection;
        return new(key, isDown) { IsHardwareKey = hardware, Modifiers = hardware ? modifiers : KeyModifiers.None };
    }
}
