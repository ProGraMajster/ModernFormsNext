using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>Describes an Android key transition without translating it into text.</summary>
/// <param name="Key">The supported key identity.</param>
/// <param name="IsDown"><see langword="true"/> for key down; <see langword="false"/> for key up.</param>
public readonly record struct AndroidInputKeyEvent(AndroidInputKey Key, bool IsDown)
{
    /// <summary>Gets editing modifiers, including Shift selection and the conservative right-Alt/AltGraph marker.</summary>
    /// <remarks>
    /// Software keyboards can supply modifiers for selection or navigation. Their presence does
    /// not make an event eligible for shortcuts; use <see cref="IsHardwareKey"/> for that decision.
    /// </remarks>
    public KeyModifiers Modifiers { get; init; }

    /// <summary>Gets whether the source is eligible for hardware shortcut routing.</summary>
    /// <remarks>
    /// Eligibility requires a nonnegative Android device ID and no soft-keyboard or InputConnection
    /// origin. An emulator can satisfy this rule; it is not proof of physical hardware. InputConnection
    /// and soft-keyboard events remain editing input. The two-argument constructor defaults to editing.
    /// </remarks>
    public bool IsHardwareKey { get; init; }

    /// <summary>Gets the existing WindowKit key identity corresponding to <see cref="Key"/>.</summary>
    public Key PlatformKey => AndroidInputKeyMapper.ToPlatformKey(Key);

    /// <summary>Gets Android's zero-based repeat count; zero denotes the initial transition.</summary>
    public int RepeatCount { get; init; }

    /// <summary>Gets whether this transition is canceled, normally its final key up.</summary>
    /// <remarks>
    /// Includes Android's canceled flag and a primary-handler hardware release without a matching
    /// key/device press in the current native lifetime. Legacy and IME routes retain their native flag.
    /// </remarks>
    public bool IsCanceled { get; init; }

    /// <summary>Gets whether Android marked the key as a combining accent rather than ordinary shortcut input.</summary>
    /// <remarks>No character is synthesized from this marker; composition remains owned by the IME.</remarks>
    public bool IsDeadKey { get; init; }

    /// <summary>Gets the Android device ID, or minus one for the compatibility constructor.</summary>
    /// <remarks>Negative IDs identify virtual sources. A nonnegative value alone does not prove physical input.</remarks>
    public int DeviceId { get; init; } = -1;

    // Keep source classification independent of Android runtime objects so all hosts share the
    // same conservative rule. InputConnection events never become shortcut input, even when an
    // IME supplies a real-looking device id or modifier flags.
    internal static AndroidInputKeyEvent FromSource(AndroidInputKey key, bool isDown,
        KeyModifiers modifiers, int deviceId, bool isSoftKeyboard, bool fromInputConnection)
    {
        bool hardware = deviceId >= 0 && !isSoftKeyboard && !fromInputConnection;
        return new(key, isDown) { IsHardwareKey = hardware, Modifiers = modifiers, DeviceId = deviceId };
    }
}
