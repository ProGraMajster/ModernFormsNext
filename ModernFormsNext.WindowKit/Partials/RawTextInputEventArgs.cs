using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernFormsNext.WindowKit.Input.Raw
{
    public partial class RawTextInputEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RawTextInputEventArgs"/> class.
        /// </summary>
        /// <param name="device">The keyboard device that produced the text input.</param>
        /// <param name="timestamp">The platform event timestamp.</param>
        /// <param name="root">The input root that received the event.</param>
        /// <param name="text">The text reported by the platform.</param>
        /// <param name="modifiers">The raw input modifiers active for the event.</param>
        public RawTextInputEventArgs(
            IKeyboardDevice device,
            ulong timestamp,
            IInputRoot root,
            string text,
            RawInputModifiers modifiers)
            : base(device, timestamp, root)
        {
            Text = text;
            Modifiers = modifiers;
        }

        /// <summary>
        /// Gets or sets the raw input modifiers active for the event.
        /// </summary>
        public RawInputModifiers Modifiers { get; set; }
    }
}
