using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

// Android KeyEvent key-code and meta-state constants are stable platform identities. Keeping this
// translation free of Java objects makes the production boundary testable without another resolver.
internal static class AndroidInputKeyMapper
{
    internal static AndroidInputKeyEvent? FromNative(int keyCode, bool isDown, int metaState,
        int deviceId, bool isSoftKeyboard = false, bool fromInputConnection = false,
        int repeatCount = 0, bool isCanceled = false, bool isDeadKey = false)
    {
        AndroidInputKey? key = keyCode switch
        {
            >= 29 and <= 54 => AndroidInputKey.A + (keyCode - 29),
            >= 7 and <= 16 => AndroidInputKey.D0 + (keyCode - 7),
            >= 131 and <= 142 => AndroidInputKey.F1 + (keyCode - 131),
            >= 144 and <= 153 => AndroidInputKey.NumPad0 + (keyCode - 144),
            67 => AndroidInputKey.Backspace,
            112 => AndroidInputKey.Delete,
            66 => AndroidInputKey.Enter,
            160 => AndroidInputKey.Enter,
            21 => AndroidInputKey.Left,
            19 => AndroidInputKey.Up,
            22 => AndroidInputKey.Right,
            20 => AndroidInputKey.Down,
            61 => AndroidInputKey.Tab,
            111 => AndroidInputKey.Escape,
            62 => AndroidInputKey.Space,
            122 => AndroidInputKey.Home,
            123 => AndroidInputKey.End,
            92 => AndroidInputKey.PageUp,
            93 => AndroidInputKey.PageDown,
            124 => AndroidInputKey.Insert,
            154 => AndroidInputKey.Divide,
            155 => AndroidInputKey.Multiply,
            156 => AndroidInputKey.Subtract,
            157 => AndroidInputKey.Add,
            158 => AndroidInputKey.Decimal,
            159 => AndroidInputKey.Separator,
            59 => AndroidInputKey.LeftShift,
            60 => AndroidInputKey.RightShift,
            113 => AndroidInputKey.LeftCtrl,
            114 => AndroidInputKey.RightCtrl,
            57 => AndroidInputKey.LeftAlt,
            58 => AndroidInputKey.RightAlt,
            117 => AndroidInputKey.LeftMeta,
            118 => AndroidInputKey.RightMeta,
            55 => AndroidInputKey.OemComma,
            56 => AndroidInputKey.OemPeriod,
            68 => AndroidInputKey.OemTilde,
            69 => AndroidInputKey.OemMinus,
            70 => AndroidInputKey.OemPlus,
            71 => AndroidInputKey.OemOpenBrackets,
            72 => AndroidInputKey.OemCloseBrackets,
            73 => AndroidInputKey.OemPipe,
            74 => AndroidInputKey.OemSemicolon,
            75 => AndroidInputKey.OemQuotes,
            76 => AndroidInputKey.OemQuestion,
            _ => null
        };
        if (key is null) return null;
        return AndroidInputKeyEvent.FromSource(key.Value, isDown, MapModifiers(metaState), deviceId,
            isSoftKeyboard, fromInputConnection) with
        {
            RepeatCount = Math.Max(0, repeatCount),
            IsCanceled = isCanceled,
            IsDeadKey = isDeadKey
        };
    }

    internal static KeyModifiers MapModifiers(int metaState)
    {
        var result = KeyModifiers.None;
        // Include side-specific bits even for synthetic sources lacking aggregate bits.
        if ((metaState & 0x7000) != 0) result |= KeyModifiers.Control;
        if ((metaState & 0xC1) != 0) result |= KeyModifiers.Shift;
        if ((metaState & 0x32) != 0) result |= KeyModifiers.Alt;
        if ((metaState & 0x70000) != 0) result |= KeyModifiers.Meta;
        // Android does not expose a dedicated AltGraph bit. Right Alt is conservatively
        // reserved for international text input rather than matching a Ctrl+Alt command.
        if ((metaState & 0x20) != 0) result |= KeyModifiers.AltGraph;
        return result;
    }

    internal static Key ToPlatformKey(AndroidInputKey key) => key switch
    {
        AndroidInputKey.Backspace => Key.Back,
        AndroidInputKey.Delete => Key.Delete,
        AndroidInputKey.Enter => Key.Enter,
        AndroidInputKey.Left => Key.Left,
        AndroidInputKey.Up => Key.Up,
        AndroidInputKey.Right => Key.Right,
        AndroidInputKey.Down => Key.Down,
        AndroidInputKey.A => Key.A,
        AndroidInputKey.B => Key.B,
        AndroidInputKey.C => Key.C,
        AndroidInputKey.D => Key.D,
        AndroidInputKey.E => Key.E,
        AndroidInputKey.F => Key.F,
        AndroidInputKey.G => Key.G,
        AndroidInputKey.H => Key.H,
        AndroidInputKey.I => Key.I,
        AndroidInputKey.J => Key.J,
        AndroidInputKey.K => Key.K,
        AndroidInputKey.L => Key.L,
        AndroidInputKey.M => Key.M,
        AndroidInputKey.N => Key.N,
        AndroidInputKey.O => Key.O,
        AndroidInputKey.P => Key.P,
        AndroidInputKey.Q => Key.Q,
        AndroidInputKey.R => Key.R,
        AndroidInputKey.S => Key.S,
        AndroidInputKey.T => Key.T,
        AndroidInputKey.U => Key.U,
        AndroidInputKey.V => Key.V,
        AndroidInputKey.W => Key.W,
        AndroidInputKey.X => Key.X,
        AndroidInputKey.Y => Key.Y,
        AndroidInputKey.Z => Key.Z,
        AndroidInputKey.D0 => Key.D0,
        AndroidInputKey.D1 => Key.D1,
        AndroidInputKey.D2 => Key.D2,
        AndroidInputKey.D3 => Key.D3,
        AndroidInputKey.D4 => Key.D4,
        AndroidInputKey.D5 => Key.D5,
        AndroidInputKey.D6 => Key.D6,
        AndroidInputKey.D7 => Key.D7,
        AndroidInputKey.D8 => Key.D8,
        AndroidInputKey.D9 => Key.D9,
        AndroidInputKey.F1 => Key.F1,
        AndroidInputKey.F2 => Key.F2,
        AndroidInputKey.F3 => Key.F3,
        AndroidInputKey.F4 => Key.F4,
        AndroidInputKey.F5 => Key.F5,
        AndroidInputKey.F6 => Key.F6,
        AndroidInputKey.F7 => Key.F7,
        AndroidInputKey.F8 => Key.F8,
        AndroidInputKey.F9 => Key.F9,
        AndroidInputKey.F10 => Key.F10,
        AndroidInputKey.F11 => Key.F11,
        AndroidInputKey.F12 => Key.F12,
        AndroidInputKey.Tab => Key.Tab,
        AndroidInputKey.Escape => Key.Escape,
        AndroidInputKey.Space => Key.Space,
        AndroidInputKey.Home => Key.Home,
        AndroidInputKey.End => Key.End,
        AndroidInputKey.PageUp => Key.PageUp,
        AndroidInputKey.PageDown => Key.PageDown,
        AndroidInputKey.Insert => Key.Insert,
        AndroidInputKey.NumPad0 => Key.NumPad0,
        AndroidInputKey.NumPad1 => Key.NumPad1,
        AndroidInputKey.NumPad2 => Key.NumPad2,
        AndroidInputKey.NumPad3 => Key.NumPad3,
        AndroidInputKey.NumPad4 => Key.NumPad4,
        AndroidInputKey.NumPad5 => Key.NumPad5,
        AndroidInputKey.NumPad6 => Key.NumPad6,
        AndroidInputKey.NumPad7 => Key.NumPad7,
        AndroidInputKey.NumPad8 => Key.NumPad8,
        AndroidInputKey.NumPad9 => Key.NumPad9,
        AndroidInputKey.Divide => Key.Divide,
        AndroidInputKey.Multiply => Key.Multiply,
        AndroidInputKey.Subtract => Key.Subtract,
        AndroidInputKey.Add => Key.Add,
        AndroidInputKey.Decimal => Key.Decimal,
        AndroidInputKey.Separator => Key.Separator,
        AndroidInputKey.LeftShift => Key.LeftShift,
        AndroidInputKey.RightShift => Key.RightShift,
        AndroidInputKey.LeftCtrl => Key.LeftCtrl,
        AndroidInputKey.RightCtrl => Key.RightCtrl,
        AndroidInputKey.LeftAlt => Key.LeftAlt,
        AndroidInputKey.RightAlt => Key.RightAlt,
        AndroidInputKey.LeftMeta => Key.LWin,
        AndroidInputKey.RightMeta => Key.RWin,
        AndroidInputKey.OemComma => Key.OemComma,
        AndroidInputKey.OemPeriod => Key.OemPeriod,
        AndroidInputKey.OemTilde => Key.OemTilde,
        AndroidInputKey.OemMinus => Key.OemMinus,
        AndroidInputKey.OemPlus => Key.OemPlus,
        AndroidInputKey.OemOpenBrackets => Key.OemOpenBrackets,
        AndroidInputKey.OemCloseBrackets => Key.OemCloseBrackets,
        AndroidInputKey.OemPipe => Key.OemPipe,
        AndroidInputKey.OemSemicolon => Key.OemSemicolon,
        AndroidInputKey.OemQuotes => Key.OemQuotes,
        AndroidInputKey.OemQuestion => Key.OemQuestion,
        _ => Key.None
    };

    internal static bool IsLegacyKey(AndroidInputKey key)
        => key is >= AndroidInputKey.Backspace and <= AndroidInputKey.Down;
}
