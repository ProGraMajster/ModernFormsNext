// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;

namespace ModernFormsNext
{
    /// <summary>
    ///  Provides data for the <see cref='Control.MouseUp'/>, <see cref='Control.MouseDown'/>,
    /// <see cref='Control.MouseMove'/>, and <see cref='Control.MouseWheel'/> events.
    /// </summary>
    public class MouseEventArgs : EventArgs
    {
        private readonly Keys key_data;

        /// <summary>
        ///  Initializes a new instance of the <see cref='MouseEventArgs'/> class.
        /// </summary>
        public MouseEventArgs (
            MouseButtons button,
            int clicks,
            int x,
            int y,
            Point delta,
            int? screenX = null,
            int? screenY = null,
            Keys keyData = Keys.None)
            : this (
                button,
                clicks,
                x,
                y,
                delta,
                screenX,
                screenY,
                keyData,
                0,
                PointerDeviceKind.Mouse)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MouseEventArgs"/> class for a specific
        /// pointer sequence.
        /// </summary>
        /// <param name="button">The mouse button associated with the event.</param>
        /// <param name="clicks">The number of button presses and releases.</param>
        /// <param name="x">The pointer x-coordinate relative to the receiving control.</param>
        /// <param name="y">The pointer y-coordinate relative to the receiving control.</param>
        /// <param name="delta">The signed wheel delta.</param>
        /// <param name="screenX">The pointer x-coordinate in screen coordinates, or <see langword="null"/> to use <paramref name="x"/>.</param>
        /// <param name="screenY">The pointer y-coordinate in screen coordinates, or <see langword="null"/> to use <paramref name="y"/>.</param>
        /// <param name="keyData">The keyboard modifiers active for the event.</param>
        /// <param name="pointerId">The platform-stable pointer identifier.</param>
        /// <param name="pointerKind">The physical pointer source.</param>
        public MouseEventArgs (
            MouseButtons button,
            int clicks,
            int x,
            int y,
            Point delta,
            int? screenX,
            int? screenY,
            Keys keyData,
            int pointerId,
            PointerDeviceKind pointerKind)
        {
            Button = button;
            Clicks = clicks;
            Delta = delta;
            X = x;
            Y = y;
            ScreenLocation = new Point (screenX ?? x, screenY ?? y);
            key_data = keyData;
            PointerId = pointerId;
            PointerKind = pointerKind;
        }

        /// <summary>
        ///  Gets which mouse button was pressed.
        /// </summary>
        public MouseButtons Button { get; }

        /// <summary>
        ///  Gets the number of times the mouse button was pressed and released.
        /// </summary>
        public int Clicks { get; }

        /// <summary>
        ///  Gets the x-coordinate of a mouse click.
        /// </summary>
        public int X { get; }

        /// <summary>
        ///  Gets the y-coordinate of a mouse click.
        /// </summary>
        public int Y { get; }

        /// <summary>
        ///  Gets a signed count of the number of detents the mouse wheel has rotated in each direction.
        /// </summary>
        public Point Delta { get; }

        /// <summary>
        ///  Gets the location of the mouse during MouseEvent.
        /// </summary>
        public Point Location => new Point (X, Y);

        /// <summary>
        /// Get the mouse location in screen coordinates.
        /// </summary>
        public Point ScreenLocation { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the mouse event was handled by the receiving control.
        /// </summary>
        /// <remarks>
        /// Mouse-wheel routing uses this value to stop bubbling the event through parent controls.
        /// A scrollable control should set it only when the wheel input changed its scroll position,
        /// allowing an outer scrolling container to continue at an inner container's boundary.
        /// </remarks>
        public bool Handled { get; set; }

        /// <summary>Gets the platform-stable identifier for this pointer sequence.</summary>
        /// <remarks>Desktop mouse events use zero. Touch hosts preserve the platform pointer ID.</remarks>
        public int PointerId { get; }

        /// <summary>Gets the physical pointer source when the host can identify it.</summary>
        public PointerDeviceKind PointerKind { get; }

        /// <summary>
        /// Gets whether the Alt modifier key was also pressed.
        /// </summary>
        public bool Alt => key_data.HasFlag (Keys.Alt);

        /// <summary>
        /// Gets whether the AltGraph modifier key was also pressed.
        /// </summary>
        /// <remarks>
        /// AltGraph can be accompanied by synthetic Control and Alt flags on Windows.
        /// </remarks>
        public bool AltGraph => key_data.HasFlag (Keys.AltGraph);

        /// <summary>
        /// Gets whether the Control modifier key was also pressed.
        /// </summary>
        public bool Control => key_data.HasFlag (Keys.Control);

        /// <summary>
        /// Gets whether Control represents a mouse shortcut rather than an AltGraph sequence.
        /// </summary>
        internal bool IsShortcutControlPressed => Control && !AltGraph;

        /// <summary>
        /// Gets the modifier keys that were also pressed.
        /// </summary>
        public Keys Modifiers => key_data & Keys.Modifiers;

        /// <summary>
        /// Gets whether the Shift modifier key was also pressed.
        /// </summary>
        public bool Shift => key_data.HasFlag (Keys.Shift);
    }
}
