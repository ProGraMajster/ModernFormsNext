using System;
using System.ComponentModel;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input
{
    /// <summary>
    /// Identifies keyboard modifier keys that are active for an input event.
    /// </summary>
    [Flags]
    public enum KeyModifiers
    {
        /// <summary>
        /// No keyboard modifiers are active.
        /// </summary>
        None = 0,

        /// <summary>
        /// The Alt modifier is active.
        /// </summary>
        Alt = 1,

        /// <summary>
        /// The Control modifier is active.
        /// </summary>
        Control = 2,

        /// <summary>
        /// The Shift modifier is active.
        /// </summary>
        Shift = 4,

        /// <summary>
        /// The platform meta modifier is active.
        /// </summary>
        /// <remarks>
        /// On Windows this commonly maps to the Windows key; other platforms may map it to
        /// their native command or meta key.
        /// </remarks>
        Meta = 8,

        /// <summary>
        /// The AltGraph modifier used by international keyboard layouts is active.
        /// </summary>
        /// <remarks>
        /// Platforms that represent AltGraph as a synthetic Control+Alt sequence should set
        /// this flag in addition to the physical modifier flags.
        /// </remarks>
        AltGraph = 16,
    }

    /// <summary>
    /// Identifies the current state of a keyboard key.
    /// </summary>
    [Flags]
    public enum KeyStates
    {
        /// <summary>
        /// The key has no active state.
        /// </summary>
        None = 0,

        /// <summary>
        /// The key is currently pressed.
        /// </summary>
        Down = 1,

        /// <summary>
        /// The key is toggled on, such as Caps Lock.
        /// </summary>
        Toggled = 2,
    }

    /// <summary>
    /// Identifies keyboard, pointer-button, and pen modifiers present on a raw input event.
    /// </summary>
    [Flags]
    public enum RawInputModifiers
    {
        /// <summary>
        /// No raw input modifiers are active.
        /// </summary>
        None = 0,

        /// <summary>
        /// The Alt modifier is active.
        /// </summary>
        Alt = 1,

        /// <summary>
        /// The Control modifier is active.
        /// </summary>
        Control = 2,

        /// <summary>
        /// The Shift modifier is active.
        /// </summary>
        Shift = 4,

        /// <summary>
        /// The platform meta modifier is active.
        /// </summary>
        Meta = 8,

        /// <summary>
        /// The left mouse button is pressed.
        /// </summary>
        LeftMouseButton = 16,

        /// <summary>
        /// The right mouse button is pressed.
        /// </summary>
        RightMouseButton = 32,

        /// <summary>
        /// The middle mouse button is pressed.
        /// </summary>
        MiddleMouseButton = 64,

        /// <summary>
        /// The first extended mouse button is pressed.
        /// </summary>
        XButton1MouseButton = 128,

        /// <summary>
        /// The second extended mouse button is pressed.
        /// </summary>
        XButton2MouseButton = 256,

        /// <summary>
        /// The AltGraph modifier used by international keyboard layouts is active.
        /// </summary>
        /// <remarks>
        /// This flag preserves AltGraph semantics when a platform also reports synthetic
        /// Control and Alt modifiers for the same physical key sequence.
        /// </remarks>
        AltGraph = 4096,

        /// <summary>
        /// Mask containing all keyboard modifier flags.
        /// </summary>
        KeyboardMask = Alt | Control | Shift | Meta | AltGraph,

        /// <summary>
        /// The pen is inverted.
        /// </summary>
        PenInverted = 512,

        /// <summary>
        /// The pen eraser is active.
        /// </summary>
        PenEraser = 1024,

        /// <summary>
        /// The pen barrel button is pressed.
        /// </summary>
        PenBarrelButton = 2048
    }

    /// <summary>
    /// Represents the backend keyboard input device.
    /// </summary>
    [PrivateApi]
    public interface IKeyboardDevice : IInputDevice
    {
    }
}
