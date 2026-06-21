
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input.Raw
{
    /// <summary>
    /// Provides raw mouse wheel event data from a platform backend.
    /// </summary>
    [PrivateApi]
    public class RawMouseWheelEventArgs : RawPointerEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RawMouseWheelEventArgs"/> class.
        /// </summary>
        /// <param name="device">The input device that produced the event.</param>
        /// <param name="timestamp">The platform event timestamp.</param>
        /// <param name="root">The input root that received the event.</param>
        /// <param name="position">The pointer position in client DIPs.</param>
        /// <param name="delta">The wheel delta.</param>
        /// <param name="inputModifiers">The raw input modifiers active for the event.</param>
        public RawMouseWheelEventArgs(
            IInputDevice device,
            ulong timestamp,
            IInputRoot root,
            Point position,
            Vector delta, RawInputModifiers inputModifiers)
            : base(device, timestamp, root, RawPointerEventType.Wheel, position, inputModifiers)
        {
            Delta = delta;
        }

        /// <summary>
        /// Gets the wheel delta reported by the platform.
        /// </summary>
        public Vector Delta { get; private set; }
    }
}
