using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Input.Raw
{
    /// <summary>
    /// Provides raw text input event data from a platform backend.
    /// </summary>
    [PrivateApi]
    public partial class RawTextInputEventArgs : RawInputEventArgs
    {
        //public RawTextInputEventArgs(
        //    IKeyboardDevice device,
        //    ulong timestamp,
        //    IInputRoot root,
        //    string text)
        //    : base(device, timestamp, root)
        //{
        //    Text = text;
        //}

        /// <summary>
        /// Gets the text reported by the platform text input event.
        /// </summary>
        public string Text { get; }
    }
}
