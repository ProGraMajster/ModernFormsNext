using System;
using System.Drawing;

namespace ModernFormsNext.Help
{
    /// <summary>
    ///  Provides data for the <see cref="Control.HelpRequested"/> event.
    /// </summary>
    public class HelpEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="HelpEventArgs"/> class.
        /// </summary>
        public HelpEventArgs(Point mousePos)
        {
            MousePos = mousePos;
        }

        /// <summary>
        ///  Gets the screen coordinates of the mouse pointer.
        /// </summary>
        public Point MousePos { get; }

        /// <summary>
        ///  Gets or sets a value indicating whether the Help event was handled.
        /// </summary>
        public bool Handled { get; set; }
    }
}
