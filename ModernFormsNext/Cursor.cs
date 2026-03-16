using System;
using System.Collections.Generic;
using System.Text;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a mouse cursor.
    /// </summary>
    public class Cursor
    {
        internal ModernFormsNext.WindowKit.Input.Cursor cursor;

        internal Cursor (StandardCursorType type)
        {
            cursor = new ModernFormsNext.WindowKit.Input.Cursor (type);
        }

        /// <summary>
        /// The default cursor provided by the operating system.
        /// </summary>
        public static Cursor Default => Cursors.Arrow;
    }
}
