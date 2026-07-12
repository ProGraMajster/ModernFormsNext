using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext
{
    internal static class WindowKitKeyMapper
    {
        public static Keys ToFormsKey(Key key) => key switch
        {
            Key.None => Keys.None,

            Key.Back => Keys.Back,
            Key.Tab => Keys.Tab,
            Key.Return => Keys.Return,
            Key.Escape => Keys.Escape,
            Key.Space => Keys.Space,

            Key.Left => Keys.Left,
            Key.Right => Keys.Right,
            Key.Up => Keys.Up,
            Key.Down => Keys.Down,

            Key.Insert => Keys.Insert,
            Key.Delete => Keys.Delete,
            Key.Home => Keys.Home,
            Key.End => Keys.End,
            Key.PageUp => Keys.PageUp,
            Key.PageDown => Keys.PageDown,

            Key.A => Keys.A,
            Key.B => Keys.B,
            Key.C => Keys.C,
            Key.D => Keys.D,
            Key.E => Keys.E,
            Key.F => Keys.F,
            Key.G => Keys.G,
            Key.H => Keys.H,
            Key.I => Keys.I,
            Key.J => Keys.J,
            Key.K => Keys.K,
            Key.L => Keys.L,
            Key.M => Keys.M,
            Key.N => Keys.N,
            Key.O => Keys.O,
            Key.P => Keys.P,
            Key.Q => Keys.Q,
            Key.R => Keys.R,
            Key.S => Keys.S,
            Key.T => Keys.T,
            Key.U => Keys.U,
            Key.V => Keys.V,
            Key.W => Keys.W,
            Key.X => Keys.X,
            Key.Y => Keys.Y,
            Key.Z => Keys.Z,

            Key.D0 => Keys.D0,
            Key.D1 => Keys.D1,
            Key.D2 => Keys.D2,
            Key.D3 => Keys.D3,
            Key.D4 => Keys.D4,
            Key.D5 => Keys.D5,
            Key.D6 => Keys.D6,
            Key.D7 => Keys.D7,
            Key.D8 => Keys.D8,
            Key.D9 => Keys.D9,

            Key.NumPad0 => Keys.NumPad0,
            Key.NumPad1 => Keys.NumPad1,
            Key.NumPad2 => Keys.NumPad2,
            Key.NumPad3 => Keys.NumPad3,
            Key.NumPad4 => Keys.NumPad4,
            Key.NumPad5 => Keys.NumPad5,
            Key.NumPad6 => Keys.NumPad6,
            Key.NumPad7 => Keys.NumPad7,
            Key.NumPad8 => Keys.NumPad8,
            Key.NumPad9 => Keys.NumPad9,

            Key.F1 => Keys.F1,
            Key.F2 => Keys.F2,
            Key.F3 => Keys.F3,
            Key.F4 => Keys.F4,
            Key.F5 => Keys.F5,
            Key.F6 => Keys.F6,
            Key.F7 => Keys.F7,
            Key.F8 => Keys.F8,
            Key.F9 => Keys.F9,
            Key.F10 => Keys.F10,
            Key.F11 => Keys.F11,
            Key.F12 => Keys.F12,

            Key.Oem3 => Keys.Oemtilde,

            Key.LeftCtrl => Keys.ControlKey,
            Key.RightCtrl => Keys.ControlKey,
            Key.LeftShift => Keys.ShiftKey,
            Key.RightShift => Keys.ShiftKey,
            Key.LeftAlt => Keys.Menu,
            Key.RightAlt => Keys.Menu,

            _ => Keys.None
        };
    }
}
