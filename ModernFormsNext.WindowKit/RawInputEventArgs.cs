using System;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input.Raw
{
    /// <summary>
    /// A raw input event.
    /// </summary>
    /// <remarks>
    /// Raw input events are sent from the windowing subsystem to the platform input pipeline
    /// for processing: this gives an application the opportunity to pre-process the event. After
    /// pre-processing they are consumed by the relevant <see cref="Device"/> and turned into
    /// standard Avalonia events.
    /// </remarks>
    [PrivateApi]
    public class RawInputEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RawInputEventArgs"/> class.
        /// </summary>
        /// <param name="device">The associated device.</param>
        /// <param name="timestamp">The event timestamp.</param>
        /// <param name="root">The root from which the event originates.</param>
        public RawInputEventArgs(IInputDevice device, ulong timestamp, IInputRoot root)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            Timestamp = timestamp;
            //Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>
        /// Gets the associated device.
        /// </summary>
        public IInputDevice Device { get; }

        ///// <summary>
        ///// Gets the root from which the event originates.
        ///// </summary>
        //public IInputRoot Root { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the event was handled.
        /// </summary>
        /// <remarks>
        /// If an event is not marked handled after platform input processing, then it will
        /// be passed on to the underlying OS for handling.
        /// </remarks>
        public bool Handled { get; set; }

        /// <summary>
        /// Gets the timestamp associated with the event.
        /// </summary>
        public ulong Timestamp { get; set; }
    }
}
