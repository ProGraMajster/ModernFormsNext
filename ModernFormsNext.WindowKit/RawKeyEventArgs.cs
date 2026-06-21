using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input.Raw
{
    /// <summary>
    /// Identifies the kind of raw keyboard event reported by a platform backend.
    /// </summary>
    public enum RawKeyEventType
    {
        /// <summary>
        /// A key was pressed.
        /// </summary>
        KeyDown,

        /// <summary>
        /// A key was released.
        /// </summary>
        KeyUp
    }

    /// <summary>
    /// Provides raw keyboard event data from the platform backend.
    /// </summary>
    [PrivateApi]
    public class RawKeyEventArgs : RawInputEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RawKeyEventArgs"/> class.
        /// </summary>
        /// <param name="device">The keyboard device that produced the event.</param>
        /// <param name="timestamp">The platform event timestamp.</param>
        /// <param name="root">The input root that received the event.</param>
        /// <param name="type">The raw keyboard event type.</param>
        /// <param name="key">The key reported by the platform.</param>
        /// <param name="modifiers">The raw input modifiers active for the event.</param>
        public RawKeyEventArgs(
            IKeyboardDevice device,
            ulong timestamp,
            IInputRoot root,
            RawKeyEventType type,
            Key key, RawInputModifiers modifiers)
            : base(device, timestamp, root)
        {
            Key = key;
            Type = type;
            Modifiers = modifiers;
        }

        /// <summary>
        /// Gets or sets the key reported by the platform.
        /// </summary>
        public Key Key { get; set; }

        /// <summary>
        /// Gets or sets the raw input modifiers active for the event.
        /// </summary>
        public RawInputModifiers Modifiers { get; set; }

        /// <summary>
        /// Gets or sets the raw keyboard event type.
        /// </summary>
        public RawKeyEventType Type { get; set; }
    }
}
