using System;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input.Raw
    {
    /// <summary>
    /// Provides raw touch event data from a platform backend.
    /// </summary>
    [PrivateApi]
    public class RawTouchEventArgs : RawPointerEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RawTouchEventArgs"/> class.
        /// </summary>
        /// <param name="device">The input device that produced the event.</param>
        /// <param name="timestamp">The platform event timestamp.</param>
        /// <param name="root">The input root that received the event.</param>
        /// <param name="type">The raw pointer event type.</param>
        /// <param name="position">The touch position in client DIPs.</param>
        /// <param name="inputModifiers">The raw input modifiers active for the event.</param>
        /// <param name="rawPointerId">The platform touch pointer identifier.</param>
        public RawTouchEventArgs(IInputDevice device, ulong timestamp, IInputRoot root,
            RawPointerEventType type, Point position, RawInputModifiers inputModifiers,
            long rawPointerId) 
            : base(device, timestamp, root, type, position, inputModifiers)
        {
            RawPointerId = rawPointerId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RawTouchEventArgs"/> class.
        /// </summary>
        /// <param name="device">The input device that produced the event.</param>
        /// <param name="timestamp">The platform event timestamp.</param>
        /// <param name="root">The input root that received the event.</param>
        /// <param name="type">The raw pointer event type.</param>
        /// <param name="point">The touch point properties and position.</param>
        /// <param name="inputModifiers">The raw input modifiers active for the event.</param>
        /// <param name="rawPointerId">The platform touch pointer identifier.</param>
        public RawTouchEventArgs(IInputDevice device, ulong timestamp, IInputRoot root,
            RawPointerEventType type, RawPointerPoint point, RawInputModifiers inputModifiers,
            long rawPointerId)
            : base(device, timestamp, root, type, point, inputModifiers)
        {
            RawPointerId = rawPointerId;
        }
    }
}
