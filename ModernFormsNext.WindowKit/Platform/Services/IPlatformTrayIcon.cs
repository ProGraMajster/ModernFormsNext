using System;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Metadata;
using SkiaSharp;

namespace ModernFormsNext.WindowKit.Platform.Services
{
    /// <summary>
    /// Represents a backend-owned operating system tray icon.
    /// </summary>
    /// <remarks>
    /// This interface is implemented by platform backends and consumed by the public
    /// framework <c>NotifyIcon</c> component. Application code should use the public
    /// component rather than implementing this contract directly.
    /// </remarks>
    [Unstable, PrivateApi]
    public interface IPlatformTrayIcon : IDisposable
    {
        /// <summary>
        /// Occurs when a pointer button is pressed over the tray icon.
        /// </summary>
        event EventHandler<PlatformTrayIconMouseEventArgs>? MouseDown;

        /// <summary>
        /// Occurs when the pointer moves over the tray icon.
        /// </summary>
        event EventHandler<PlatformTrayIconMouseEventArgs>? MouseMove;

        /// <summary>
        /// Occurs when a pointer button is released over the tray icon.
        /// </summary>
        event EventHandler<PlatformTrayIconMouseEventArgs>? MouseUp;

        /// <summary>
        /// Occurs when the tray icon is double-clicked.
        /// </summary>
        event EventHandler<PlatformTrayIconMouseEventArgs>? DoubleClick;

        /// <summary>
        /// Gets or sets the bitmap shown by the platform tray area.
        /// </summary>
        /// <remarks>
        /// Implementations copy the bitmap into a native icon handle; callers retain ownership
        /// of the <see cref="SKBitmap"/> instance.
        /// </remarks>
        SKBitmap? Icon { get; set; }

        /// <summary>
        /// Gets or sets the tooltip text displayed by the platform tray area.
        /// </summary>
        string Text { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tray icon is visible.
        /// </summary>
        bool Visible { get; set; }

        /// <summary>
        /// Displays a platform balloon notification for the tray icon.
        /// </summary>
        /// <param name="timeout">The requested timeout in milliseconds. Some platforms may ignore it.</param>
        /// <param name="title">The balloon title.</param>
        /// <param name="text">The balloon body text.</param>
        /// <param name="icon">The balloon icon kind.</param>
        void ShowBalloonTip (int timeout, string title, string text, PlatformBalloonIcon icon);
    }

    /// <summary>
    /// Provides pointer information for platform tray icon events.
    /// </summary>
    [Unstable, PrivateApi]
    public sealed class PlatformTrayIconMouseEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlatformTrayIconMouseEventArgs"/> class.
        /// </summary>
        /// <param name="button">The mouse button associated with the event.</param>
        /// <param name="clicks">The number of clicks represented by the event.</param>
        /// <param name="screenLocation">The pointer location in screen pixels.</param>
        public PlatformTrayIconMouseEventArgs (MouseButton button, int clicks, PixelPoint screenLocation)
        {
            Button = button;
            Clicks = clicks;
            ScreenLocation = screenLocation;
        }

        /// <summary>
        /// Gets the mouse button associated with the event.
        /// </summary>
        public MouseButton Button { get; }

        /// <summary>
        /// Gets the number of clicks represented by the event.
        /// </summary>
        public int Clicks { get; }

        /// <summary>
        /// Gets the pointer location in screen pixels.
        /// </summary>
        public PixelPoint ScreenLocation { get; }
    }

    /// <summary>
    /// Identifies the platform icon shown in a tray balloon notification.
    /// </summary>
    [Unstable, PrivateApi]
    public enum PlatformBalloonIcon
    {
        /// <summary>
        /// No platform icon is requested.
        /// </summary>
        None,

        /// <summary>
        /// An informational icon is requested.
        /// </summary>
        Info,

        /// <summary>
        /// A warning icon is requested.
        /// </summary>
        Warning,

        /// <summary>
        /// An error icon is requested.
        /// </summary>
        Error
    }
}
