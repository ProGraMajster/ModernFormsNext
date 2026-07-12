using System;
using System.Drawing;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for the <see cref="RichTextBox.ContentsResized"/> event.
    /// </summary>
    public class ContentsResizedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentsResizedEventArgs"/> class.
        /// </summary>
        /// <param name="newRectangle">The requested content bounds in logical pixels.</param>
        public ContentsResizedEventArgs(Rectangle newRectangle)
        {
            NewRectangle = newRectangle;
        }

        /// <summary>
        /// Gets the requested content bounds in logical pixels.
        /// </summary>
        public Rectangle NewRectangle { get; }
    }
}
