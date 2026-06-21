using ModernFormsNext.WindowKit.Input.Raw;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input
{
    /// <summary>
    /// Represents a backend input device that can process raw platform input events.
    /// </summary>
    [NotClientImplementable, PrivateApi]
    public interface IInputDevice
    {
        /// <summary>
        /// Processes a raw event after input manager preprocessing.
        /// </summary>
        /// <param name="ev">The raw input event to process.</param>
        void ProcessRawEvent(RawInputEventArgs ev);
    }
}
