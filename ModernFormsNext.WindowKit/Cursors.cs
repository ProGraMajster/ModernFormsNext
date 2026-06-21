#nullable disable

// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Input
{
    /*
    =========================================================================================
        NOTE: Cursors are NOT disposable and are cached in platform implementation.
        To support loading custom cursors some measures about that should be taken beforehand
    =========================================================================================
    */

    /// <summary>
    /// Identifies a standard pointer cursor shape supported by platform backends.
    /// </summary>
    /// <remarks>
    /// Availability and exact artwork are platform-specific. Backends may map a cursor to the
    /// nearest native equivalent when the requested shape is not available directly.
    /// </remarks>
    public enum StandardCursorType
    {
        /// <summary>
        /// The default arrow cursor.
        /// </summary>
        Arrow,

        /// <summary>
        /// The text selection cursor.
        /// </summary>
        Ibeam,

        /// <summary>
        /// The wait or busy cursor.
        /// </summary>
        Wait,

        /// <summary>
        /// The crosshair cursor.
        /// </summary>
        Cross,

        /// <summary>
        /// The upward arrow cursor.
        /// </summary>
        UpArrow,

        /// <summary>
        /// The horizontal resize cursor.
        /// </summary>
        SizeWestEast,

        /// <summary>
        /// The vertical resize cursor.
        /// </summary>
        SizeNorthSouth,

        /// <summary>
        /// The move or resize-all cursor.
        /// </summary>
        SizeAll,

        /// <summary>
        /// The unavailable or prohibited cursor.
        /// </summary>
        No,

        /// <summary>
        /// The hand cursor, typically used for links or clickable content.
        /// </summary>
        Hand,

        /// <summary>
        /// The application-starting cursor.
        /// </summary>
        AppStarting,

        /// <summary>
        /// The help cursor.
        /// </summary>
        Help,

        /// <summary>
        /// The top-edge resize cursor.
        /// </summary>
        TopSide,

        /// <summary>
        /// The bottom-edge resize cursor.
        /// </summary>
        BottomSide,

        /// <summary>
        /// The left-edge resize cursor.
        /// </summary>
        LeftSide,

        /// <summary>
        /// The right-edge resize cursor.
        /// </summary>
        RightSide,

        /// <summary>
        /// The top-left corner resize cursor.
        /// </summary>
        TopLeftCorner,

        /// <summary>
        /// The top-right corner resize cursor.
        /// </summary>
        TopRightCorner,

        /// <summary>
        /// The bottom-left corner resize cursor.
        /// </summary>
        BottomLeftCorner,

        /// <summary>
        /// The bottom-right corner resize cursor.
        /// </summary>
        BottomRightCorner,

        /// <summary>
        /// The drag-move cursor.
        /// </summary>
        DragMove,

        /// <summary>
        /// The drag-copy cursor.
        /// </summary>
        DragCopy,

        /// <summary>
        /// The drag-link cursor.
        /// </summary>
        DragLink,

        /// <summary>
        /// No visible cursor.
        /// </summary>
        None,

        /// <summary>
        /// Obsolete alias for <see cref="BottomSide"/>.
        /// </summary>
        [Obsolete("Use BottomSide")]
        BottomSize = BottomSide

        // Not available in GTK directly, see http://www.pixelbeat.org/programming/x_cursors/ 
        // We might enable them later, preferably, by loading pixmax direclty from theme with fallback image
        // SizeNorthWestSouthEast,
        // SizeNorthEastSouthWest,
    }

    /// <summary>
    /// Represents a platform cursor that can be assigned to a control or window.
    /// </summary>
    /// <remarks>
    /// Cursor instances are lightweight wrappers around backend-owned cursor handles. They are
    /// not disposable; platform implementations own caching and native cursor lifetime.
    /// </remarks>
    public class Cursor
    {
        /// <summary>
        /// Gets the default arrow cursor.
        /// </summary>
        public static readonly Cursor Default = new Cursor(StandardCursorType.Arrow);

        internal Cursor(ICursorImpl platformCursor)
        {
            PlatformCursor = platformCursor;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cursor"/> class for a standard cursor type.
        /// </summary>
        /// <param name="cursorType">The standard cursor type to request from the active backend.</param>
        public Cursor(StandardCursorType cursorType)
            : this(GetCursor(cursorType))
        {
        }

        /// <summary>
        /// Gets the platform cursor implementation.
        /// </summary>
        /// <remarks>
        /// This property is intended for backend integration. Application code should normally
        /// use <see cref="Cursor"/> rather than depending on platform implementation details.
        /// </remarks>
        public ICursorImpl PlatformCursor { get; }

        /// <summary>
        /// Parses a standard cursor type name and creates a cursor for it.
        /// </summary>
        /// <param name="s">The cursor type name, matched case-insensitively.</param>
        /// <returns>A cursor for the parsed standard cursor type.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="s"/> does not name a <see cref="StandardCursorType"/>.
        /// </exception>
        public static Cursor Parse(string s)
        {
            return Enum.TryParse<StandardCursorType>(s, true, out var t) ?
                new Cursor(t) :
                throw new ArgumentException($"Unrecognized cursor type '{s}'.");
        }

        private static ICursorImpl GetCursor(StandardCursorType type)
        {
            var platform = AvaloniaGlobals.GetService<ICursorFactory> ();

            if (platform == null)
            {
                throw new Exception("Could not create Cursor: IStandardCursorFactory not registered.");
            }

            return platform.GetCursor(type);
        }
    }
}
