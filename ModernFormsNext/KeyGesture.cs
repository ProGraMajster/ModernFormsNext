using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext;

/// <summary>Identifies one keyboard shortcut by a framework key and exact modifier flags.</summary>
/// <remarks>
/// Uses logical key identities supplied by the current backend, not text, scan codes or key chords.
/// AltGraph is reserved for international text input and never matches a shortcut. Printable keys
/// require Control, Alt or Meta so ordinary and Shift-modified typing remain available to controls.
/// The default value is invalid and never matches. This immutable value is safe to share across threads.
/// </remarks>
/// <example><code>var save = new KeyGesture(Keys.S, KeyModifiers.Control);</code></example>
public readonly struct KeyGesture : IEquatable<KeyGesture>
{
    private const KeyModifiers SupportedModifiers = KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Meta;

    /// <summary>Creates a single-key gesture.</summary>
    /// <param name="key">A defined non-modifier keyboard key, without embedded modifier flags.</param>
    /// <param name="modifiers">The exact required modifiers. AltGraph is not supported for shortcuts.</param>
    /// <exception cref="ArgumentException">The key/modifier combination is invalid or reserved for typing.</exception>
    public KeyGesture(Keys key, KeyModifiers modifiers = KeyModifiers.None)
    {
        if ((modifiers & ~SupportedModifiers) != 0 || !IsKeyboardKey(key))
            throw new ArgumentException("A gesture requires a keyboard key and supported shortcut modifiers.");
        if (IsPrintableKey(key) && (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0)
            throw new ArgumentException("Printable shortcut keys require Control, Alt or Meta; unmodified and Shift-only keys are reserved for text input.");
        Key = key;
        Modifiers = modifiers;
    }

    /// <summary>Gets the framework key without modifier flags; None identifies the default invalid value.</summary>
    public Keys Key { get; }

    /// <summary>Gets the exact modifiers required to match this gesture.</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>Tests an existing framework key event without consuming it or evaluating a command.</summary>
    /// <param name="e">The non-null key event. The caller decides whether its KeyDown or KeyUp stage is relevant.</param>
    /// <returns>True for the same key and exact modifiers, excluding all AltGraph input.</returns>
    public bool Matches(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return Key != Keys.None && !e.AltGraph && (e.KeyData & Keys.KeyCode) == Key && FromKeys(e.Modifiers) == Modifiers;
    }

    /// <inheritdoc/>
    public bool Equals(KeyGesture other) => Key == other.Key && Modifiers == other.Modifiers;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KeyGesture other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Key, Modifiers);
    /// <summary>Compares two gesture values for equality.</summary>
    public static bool operator ==(KeyGesture left, KeyGesture right) => left.Equals(right);
    /// <summary>Compares two gesture values for inequality.</summary>
    public static bool operator !=(KeyGesture left, KeyGesture right) => !left.Equals(right);

    /// <summary>Returns a stable diagnostic representation, not a localized label or parseable contract.</summary>
    public override string ToString()
        => Key == Keys.None ? "<invalid>" :
            ((Modifiers & KeyModifiers.Control) != 0 ? "Ctrl+" : "") +
            ((Modifiers & KeyModifiers.Shift) != 0 ? "Shift+" : "") +
            ((Modifiers & KeyModifiers.Alt) != 0 ? "Alt+" : "") +
            ((Modifiers & KeyModifiers.Meta) != 0 ? "Meta+" : "") + Key;

    private static KeyModifiers FromKeys(Keys keys)
        => ((keys & Keys.Control) != 0 ? KeyModifiers.Control : 0) |
           ((keys & Keys.Shift) != 0 ? KeyModifiers.Shift : 0) |
           ((keys & Keys.Alt) != 0 ? KeyModifiers.Alt : 0) |
           ((keys & Keys.Meta) != 0 ? KeyModifiers.Meta : 0) |
           // Unknown modifier bits must not accidentally match a known gesture.
           ((keys & ~(Keys.Control | Keys.Shift | Keys.Alt | Keys.Meta)) != 0 ? (KeyModifiers)(-1) : 0);

    private static bool IsKeyboardKey(Keys key)
        => (key & Keys.Modifiers) == 0 && Enum.IsDefined(key) && key is not
            (Keys.None or Keys.LButton or Keys.RButton or Keys.MButton or Keys.XButton1 or Keys.XButton2 or
             Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LShiftKey or Keys.RShiftKey or
             Keys.LControlKey or Keys.RControlKey or Keys.LMenu or Keys.RMenu or Keys.LWin or Keys.RWin or Keys.ProcessKey);

    private static bool IsPrintableKey(Keys key)
        => key == Keys.Space || key is >= Keys.D0 and <= Keys.Z || key is >= Keys.NumPad0 and <= Keys.Divide ||
           key is >= Keys.OemSemicolon and <= Keys.OemBackslash;
}
