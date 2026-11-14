// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides committed movement data for ScrollBar.Scroll and ScrollableControl.Scroll.
    /// </summary>
    /// <remarks>
    /// ScrollBar uses raw range values; ScrollableControl uses logical-pixel offsets relative
    /// to the axis minimum. Constructors without an old value retain the legacy -1 sentinel,
    /// and constructors without an orientation retain HorizontalScroll. Framework viewport
    /// notifications supply both values and the actual orientation explicitly.
    /// </remarks>
    public class ScrollEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the ScrollEventArgs class.
        /// </summary>
        public ScrollEventArgs (ScrollEventType type, int newValue)
        {
            Type = type;
            NewValue = newValue;
            OldValue = -1;
        }

        /// <summary>
        ///  Initializes a new instance of the ScrollEventArgs class.
        /// </summary>
        public ScrollEventArgs (ScrollEventType type, int newValue, ScrollOrientation scroll)
        {
            Type = type;
            NewValue = newValue;
            ScrollOrientation = scroll;
            OldValue = -1;
        }

        /// <summary>
        ///  Initializes a new instance of the ScrollEventArgs class.
        /// </summary>
        public ScrollEventArgs (ScrollEventType type, int oldValue, int newValue)
        {
            Type = type;
            NewValue = newValue;
            OldValue = oldValue;
        }

        /// <summary>
        ///  Initializes a new instance of the ScrollEventArgs class.
        /// </summary>
        public ScrollEventArgs (ScrollEventType type, int oldValue, int newValue, ScrollOrientation scroll)
        {
            Type = type;
            NewValue = newValue;
            OldValue = oldValue;
            ScrollOrientation = scroll;
        }

        /// <summary>
        ///  Specifies the type of scroll event that occurred.
        /// </summary>
        public ScrollEventType Type { get; }

        /// <summary>
        /// Gets or sets the reported new value, in the units of the event's owner.
        /// </summary>
        /// <remarks>
        /// For ScrollableControl.Scroll this describes already committed movement. Changing it
        /// does not cancel scrolling or change the offset; it is restored before the next subscriber.
        /// </remarks>
        public int NewValue { get; set; }

        /// <summary>
        /// Gets the previous reported value, in the units of the event's owner.
        /// </summary>
        public int OldValue { get; }

        /// <summary>
        /// Gets the axis whose committed value changed.
        /// </summary>
        public ScrollOrientation ScrollOrientation { get; }
    }
}
