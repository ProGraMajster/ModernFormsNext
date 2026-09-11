namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>Identifies supported key identities forwarded by the Android surface.</summary>
/// <remarks>
/// These identities do not translate keys into text. Values zero through six preserve the original
/// editing-key event contract; the extended set is delivered through the optional primary handler.
/// Android system, media and unsupported OEM keys retain their native behavior.
/// </remarks>
public enum AndroidInputKey
{
    /// <summary>The backspace key identity.</summary>
    Backspace = 0,
    /// <summary>The delete key identity.</summary>
    Delete = 1,
    /// <summary>The enter key identity.</summary>
    Enter = 2,
    /// <summary>The left key identity.</summary>
    Left = 3,
    /// <summary>The up key identity.</summary>
    Up = 4,
    /// <summary>The right key identity.</summary>
    Right = 5,
    /// <summary>The down key identity.</summary>
    Down = 6,
    /// <summary>The a key identity.</summary>
    A,
    /// <summary>The b key identity.</summary>
    B,
    /// <summary>The c key identity.</summary>
    C,
    /// <summary>The d key identity.</summary>
    D,
    /// <summary>The e key identity.</summary>
    E,
    /// <summary>The f key identity.</summary>
    F,
    /// <summary>The g key identity.</summary>
    G,
    /// <summary>The h key identity.</summary>
    H,
    /// <summary>The i key identity.</summary>
    I,
    /// <summary>The j key identity.</summary>
    J,
    /// <summary>The k key identity.</summary>
    K,
    /// <summary>The l key identity.</summary>
    L,
    /// <summary>The m key identity.</summary>
    M,
    /// <summary>The n key identity.</summary>
    N,
    /// <summary>The o key identity.</summary>
    O,
    /// <summary>The p key identity.</summary>
    P,
    /// <summary>The q key identity.</summary>
    Q,
    /// <summary>The r key identity.</summary>
    R,
    /// <summary>The s key identity.</summary>
    S,
    /// <summary>The t key identity.</summary>
    T,
    /// <summary>The u key identity.</summary>
    U,
    /// <summary>The v key identity.</summary>
    V,
    /// <summary>The w key identity.</summary>
    W,
    /// <summary>The x key identity.</summary>
    X,
    /// <summary>The y key identity.</summary>
    Y,
    /// <summary>The z key identity.</summary>
    Z,
    /// <summary>The digit 0 key identity.</summary>
    D0,
    /// <summary>The digit 1 key identity.</summary>
    D1,
    /// <summary>The digit 2 key identity.</summary>
    D2,
    /// <summary>The digit 3 key identity.</summary>
    D3,
    /// <summary>The digit 4 key identity.</summary>
    D4,
    /// <summary>The digit 5 key identity.</summary>
    D5,
    /// <summary>The digit 6 key identity.</summary>
    D6,
    /// <summary>The digit 7 key identity.</summary>
    D7,
    /// <summary>The digit 8 key identity.</summary>
    D8,
    /// <summary>The digit 9 key identity.</summary>
    D9,
    /// <summary>The f1 key identity.</summary>
    F1,
    /// <summary>The f2 key identity.</summary>
    F2,
    /// <summary>The f3 key identity.</summary>
    F3,
    /// <summary>The f4 key identity.</summary>
    F4,
    /// <summary>The f5 key identity.</summary>
    F5,
    /// <summary>The f6 key identity.</summary>
    F6,
    /// <summary>The f7 key identity.</summary>
    F7,
    /// <summary>The f8 key identity.</summary>
    F8,
    /// <summary>The f9 key identity.</summary>
    F9,
    /// <summary>The f10 key identity.</summary>
    F10,
    /// <summary>The f11 key identity.</summary>
    F11,
    /// <summary>The f12 key identity.</summary>
    F12,
    /// <summary>The tab key identity.</summary>
    Tab,
    /// <summary>The escape key identity.</summary>
    Escape,
    /// <summary>The space key identity.</summary>
    Space,
    /// <summary>The home key identity.</summary>
    Home,
    /// <summary>The end key identity.</summary>
    End,
    /// <summary>The page up key identity.</summary>
    PageUp,
    /// <summary>The page down key identity.</summary>
    PageDown,
    /// <summary>The insert key identity.</summary>
    Insert,
    /// <summary>The numeric keypad 0 key identity.</summary>
    NumPad0,
    /// <summary>The numeric keypad 1 key identity.</summary>
    NumPad1,
    /// <summary>The numeric keypad 2 key identity.</summary>
    NumPad2,
    /// <summary>The numeric keypad 3 key identity.</summary>
    NumPad3,
    /// <summary>The numeric keypad 4 key identity.</summary>
    NumPad4,
    /// <summary>The numeric keypad 5 key identity.</summary>
    NumPad5,
    /// <summary>The numeric keypad 6 key identity.</summary>
    NumPad6,
    /// <summary>The numeric keypad 7 key identity.</summary>
    NumPad7,
    /// <summary>The numeric keypad 8 key identity.</summary>
    NumPad8,
    /// <summary>The numeric keypad 9 key identity.</summary>
    NumPad9,
    /// <summary>The divide key identity.</summary>
    Divide,
    /// <summary>The multiply key identity.</summary>
    Multiply,
    /// <summary>The subtract key identity.</summary>
    Subtract,
    /// <summary>The add key identity.</summary>
    Add,
    /// <summary>The decimal key identity.</summary>
    Decimal,
    /// <summary>The separator key identity.</summary>
    Separator,
    /// <summary>The left shift key identity.</summary>
    LeftShift,
    /// <summary>The right shift key identity.</summary>
    RightShift,
    /// <summary>The left ctrl key identity.</summary>
    LeftCtrl,
    /// <summary>The right ctrl key identity.</summary>
    RightCtrl,
    /// <summary>The left alt key identity.</summary>
    LeftAlt,
    /// <summary>The right alt key identity.</summary>
    RightAlt,
    /// <summary>The left meta key identity.</summary>
    LeftMeta,
    /// <summary>The right meta key identity.</summary>
    RightMeta,
    /// <summary>The standard oem comma key identity.</summary>
    OemComma,
    /// <summary>The standard oem period key identity.</summary>
    OemPeriod,
    /// <summary>The standard oem tilde key identity.</summary>
    OemTilde,
    /// <summary>The standard oem minus key identity.</summary>
    OemMinus,
    /// <summary>The standard oem plus key identity.</summary>
    OemPlus,
    /// <summary>The standard oem open brackets key identity.</summary>
    OemOpenBrackets,
    /// <summary>The standard oem close brackets key identity.</summary>
    OemCloseBrackets,
    /// <summary>The standard oem pipe key identity.</summary>
    OemPipe,
    /// <summary>The standard oem semicolon key identity.</summary>
    OemSemicolon,
    /// <summary>The standard oem quotes key identity.</summary>
    OemQuotes,
    /// <summary>The standard oem question key identity.</summary>
    OemQuestion
}
