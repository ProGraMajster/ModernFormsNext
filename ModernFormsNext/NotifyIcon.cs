using System;
using System.ComponentModel;
using System.Runtime.Versioning;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform.Services;
using SkiaSharp;
using DrawingPoint = System.Drawing.Point;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents an icon in the operating system notification area.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="NotifyIcon"/> is a non-visual component similar to the Windows Forms
    /// <c>NotifyIcon</c>. It is useful for applications that keep running while no normal
    /// form is visible, or for applications that need a small status affordance in the
    /// system tray.
    /// </para>
    /// <para>
    /// The first implementation is provided by the Windows backend. Creating this component
    /// on a platform whose backend does not register tray icon support throws
    /// <see cref="PlatformNotSupportedException"/>.
    /// </para>
    /// <para>
    /// Assign <see cref="Icon"/> before setting <see cref="Visible"/> to <see langword="true"/>.
    /// The bitmap is copied into a native icon handle by the backend; the caller retains
    /// ownership of the <see cref="SKBitmap"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var notifyIcon = new NotifyIcon
    /// {
    ///     Icon = SKBitmap.Decode("app-icon.png"),
    ///     Text = "ModernFormsNext app",
    ///     Visible = true
    /// };
    ///
    /// notifyIcon.Click += (_, _) => mainForm.Show();
    /// </code>
    /// </example>
    [SupportedOSPlatform ("windows")]
    public class NotifyIcon : Component
    {
        private readonly IPlatformTrayIcon platform_icon;
        private SKBitmap? icon;
        private string text = string.Empty;
        private bool disposed;
        private bool visible;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotifyIcon"/> class.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown when the active backend does not provide tray icon support.
        /// </exception>
        public NotifyIcon ()
        {
            platform_icon = CreatePlatformIcon ();
            platform_icon.MouseDown += PlatformIcon_MouseDown;
            platform_icon.MouseMove += PlatformIcon_MouseMove;
            platform_icon.MouseUp += PlatformIcon_MouseUp;
            platform_icon.DoubleClick += PlatformIcon_DoubleClick;
        }

        /// <summary>
        /// Occurs when the user clicks the notification area icon.
        /// </summary>
        public event EventHandler<MouseEventArgs>? Click;

        /// <summary>
        /// Occurs when the user double-clicks the notification area icon.
        /// </summary>
        public event EventHandler<MouseEventArgs>? DoubleClick;

        /// <summary>
        /// Occurs when the user presses a mouse button over the notification area icon.
        /// </summary>
        public event EventHandler<MouseEventArgs>? MouseDown;

        /// <summary>
        /// Occurs when the pointer moves over the notification area icon.
        /// </summary>
        public event EventHandler<MouseEventArgs>? MouseMove;

        /// <summary>
        /// Occurs when the user releases a mouse button over the notification area icon.
        /// </summary>
        public event EventHandler<MouseEventArgs>? MouseUp;

        /// <summary>
        /// Gets or sets the image displayed in the operating system notification area.
        /// </summary>
        /// <remarks>
        /// Set this property before making the component visible. The Windows backend copies
        /// the bitmap into an icon handle, so changing or disposing the original bitmap later
        /// does not update the displayed icon.
        /// </remarks>
        public SKBitmap? Icon {
            get => icon;
            set {
                ThrowIfDisposed ();

                icon = value;
                platform_icon.Icon = value;
            }
        }

        /// <summary>
        /// Gets or sets the tooltip text displayed for the notification area icon.
        /// </summary>
        /// <remarks>
        /// Windows limits tray tooltip text to a short native buffer. Longer values are
        /// truncated by the backend before they are passed to the operating system.
        /// </remarks>
        public string Text {
            get => text;
            set {
                ThrowIfDisposed ();

                text = value ?? string.Empty;
                platform_icon.Text = text;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the notification area icon is visible.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when setting the value to <see langword="true"/> before assigning <see cref="Icon"/>.
        /// </exception>
        public bool Visible {
            get => visible;
            set {
                ThrowIfDisposed ();

                if (visible == value)
                    return;

                if (value && icon is null)
                    throw new InvalidOperationException ("Assign Icon before making the NotifyIcon visible.");

                platform_icon.Visible = value;
                visible = value;
            }
        }

        /// <summary>
        /// Displays a balloon notification associated with the notification area icon.
        /// </summary>
        /// <param name="timeout">The requested display time in milliseconds.</param>
        /// <param name="tipTitle">The balloon title.</param>
        /// <param name="tipText">The balloon body text.</param>
        /// <param name="tipIcon">The icon kind shown by the platform notification.</param>
        /// <remarks>
        /// Modern Windows versions may ignore <paramref name="timeout"/> and apply system
        /// notification timing instead. The component must be visible before a balloon can
        /// be shown.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="timeout"/> is less than zero.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the icon is not currently visible.
        /// </exception>
        public void ShowBalloonTip (int timeout, string tipTitle, string tipText, NotifyIconBalloonIcon tipIcon)
        {
            ThrowIfDisposed ();
            ArgumentOutOfRangeException.ThrowIfNegative (timeout);

            if (!Visible)
                throw new InvalidOperationException ("The NotifyIcon must be visible before showing a balloon tip.");

            platform_icon.ShowBalloonTip (timeout, tipTitle ?? string.Empty, tipText ?? string.Empty, ToPlatformIcon (tipIcon));
        }

        /// <summary>
        /// Releases the resources used by the <see cref="NotifyIcon"/>.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> to release managed resources; otherwise, <see langword="false"/>.
        /// </param>
        protected override void Dispose (bool disposing)
        {
            if (disposing && !disposed) {
                platform_icon.MouseDown -= PlatformIcon_MouseDown;
                platform_icon.MouseMove -= PlatformIcon_MouseMove;
                platform_icon.MouseUp -= PlatformIcon_MouseUp;
                platform_icon.DoubleClick -= PlatformIcon_DoubleClick;
                platform_icon.Dispose ();
                disposed = true;
            }

            base.Dispose (disposing);
        }

        /// <summary>
        /// Raises the <see cref="Click"/> event.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected virtual void OnClick (MouseEventArgs e) => Click?.Invoke (this, e);

        /// <summary>
        /// Raises the <see cref="DoubleClick"/> event.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected virtual void OnDoubleClick (MouseEventArgs e) => DoubleClick?.Invoke (this, e);

        /// <summary>
        /// Raises the <see cref="MouseDown"/> event.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected virtual void OnMouseDown (MouseEventArgs e) => MouseDown?.Invoke (this, e);

        /// <summary>
        /// Raises the <see cref="MouseMove"/> event.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected virtual void OnMouseMove (MouseEventArgs e) => MouseMove?.Invoke (this, e);

        /// <summary>
        /// Raises the <see cref="MouseUp"/> event.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected virtual void OnMouseUp (MouseEventArgs e) => MouseUp?.Invoke (this, e);

        private static IPlatformTrayIcon CreatePlatformIcon ()
        {
            FrameworkBootstrap.EnsureInitialized ();

            var manager = AvaloniaGlobals.GetService<IPlatformTrayManager> ();

            if (manager is null)
                throw new PlatformNotSupportedException ("NotifyIcon is currently supported only by backends that provide tray icon support. The Windows backend provides this service.");

            return manager.CreateTrayIcon ();
        }

        private static MouseButtons ToMouseButtons (MouseButton button)
            => button switch {
                MouseButton.Left => MouseButtons.Left,
                MouseButton.Right => MouseButtons.Right,
                MouseButton.Middle => MouseButtons.Middle,
                MouseButton.XButton1 => MouseButtons.XButton1,
                MouseButton.XButton2 => MouseButtons.XButton2,
                _ => MouseButtons.None
            };

        private static PlatformBalloonIcon ToPlatformIcon (NotifyIconBalloonIcon icon)
            => icon switch {
                NotifyIconBalloonIcon.Info => PlatformBalloonIcon.Info,
                NotifyIconBalloonIcon.Warning => PlatformBalloonIcon.Warning,
                NotifyIconBalloonIcon.Error => PlatformBalloonIcon.Error,
                _ => PlatformBalloonIcon.None
            };

        private static MouseEventArgs ToMouseEventArgs (PlatformTrayIconMouseEventArgs e)
            => new MouseEventArgs (
                ToMouseButtons (e.Button),
                e.Clicks,
                e.ScreenLocation.X,
                e.ScreenLocation.Y,
                DrawingPoint.Empty,
                e.ScreenLocation.X,
                e.ScreenLocation.Y);

        private void PlatformIcon_DoubleClick (object? sender, PlatformTrayIconMouseEventArgs e)
        {
            OnDoubleClick (ToMouseEventArgs (e));
        }

        private void PlatformIcon_MouseDown (object? sender, PlatformTrayIconMouseEventArgs e)
        {
            OnMouseDown (ToMouseEventArgs (e));
        }

        private void PlatformIcon_MouseMove (object? sender, PlatformTrayIconMouseEventArgs e)
        {
            OnMouseMove (ToMouseEventArgs (e));
        }

        private void PlatformIcon_MouseUp (object? sender, PlatformTrayIconMouseEventArgs e)
        {
            var args = ToMouseEventArgs (e);

            OnMouseUp (args);

            if (e.Button != MouseButton.None)
                OnClick (args);
        }

        private void ThrowIfDisposed ()
        {
            ObjectDisposedException.ThrowIf (disposed, this);
        }
    }
}
