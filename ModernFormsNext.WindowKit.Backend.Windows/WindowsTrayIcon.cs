using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform.Services;
using SkiaSharp;
using SdBitmap = System.Drawing.Bitmap;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;

namespace ModernFormsNext.WindowKit.Backend.Windows
{
    /// <summary>
    /// Windows implementation of a notification area icon.
    /// </summary>
    internal sealed class WindowsTrayIcon : IPlatformTrayIcon
    {
        private const int IconId = 1;
        private const int MaxTooltipLength = 127;
        private const int MaxBalloonTextLength = 255;
        private const int MaxBalloonTitleLength = 63;
        private static readonly int s_notifyIconCallbackMessage = (int)WindowsMessage.WM_APP + 0x4d1;
        private static readonly uint s_taskbarCreatedMessage = RegisterWindowMessage ("TaskbarCreated");

        private readonly WndProc wnd_proc_delegate;
        private readonly IntPtr hinstance;
        private readonly string class_name;
        private readonly IntPtr hwnd;
        private IntPtr hicon;
        private bool added;
        private bool disposed;
        private SKBitmap? icon;
        private string text = string.Empty;
        private bool visible;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsTrayIcon"/> class.
        /// </summary>
        public WindowsTrayIcon ()
        {
            wnd_proc_delegate = WndProc;
            hinstance = GetModuleHandle (null);
            class_name = "ModernFormsNextNotifyIcon-" + Guid.NewGuid ();
            hwnd = CreateMessageWindow (wnd_proc_delegate, hinstance, class_name);
        }

        /// <inheritdoc/>
        public event EventHandler<PlatformTrayIconMouseEventArgs>? MouseDown;

        /// <inheritdoc/>
        public event EventHandler<PlatformTrayIconMouseEventArgs>? MouseMove;

        /// <inheritdoc/>
        public event EventHandler<PlatformTrayIconMouseEventArgs>? MouseUp;

        /// <inheritdoc/>
        public event EventHandler<PlatformTrayIconMouseEventArgs>? DoubleClick;

        /// <inheritdoc/>
        public SKBitmap? Icon {
            get => icon;
            set {
                ThrowIfDisposed ();

                icon = value;
                ReplaceIconHandle ();

                if (visible)
                    UpdateShellIcon ();
            }
        }

        /// <inheritdoc/>
        public string Text {
            get => text;
            set {
                ThrowIfDisposed ();

                text = value ?? string.Empty;

                if (visible)
                    UpdateShellIcon ();
            }
        }

        /// <inheritdoc/>
        public bool Visible {
            get => visible;
            set {
                ThrowIfDisposed ();

                if (visible == value)
                    return;

                if (value) {
                    AddShellIcon ();
                    visible = true;
                } else {
                    DeleteShellIcon ();
                    visible = false;
                }
            }
        }

        /// <inheritdoc/>
        public void ShowBalloonTip (int timeout, string title, string text, PlatformBalloonIcon icon)
        {
            ThrowIfDisposed ();

            if (!visible || !added)
                return;

            var data = CreateNotifyIconData (NIF.INFO);
            data.szInfoTitle = Clamp (title, MaxBalloonTitleLength);
            data.szInfo = Clamp (text, MaxBalloonTextLength);
            data.uTimeoutOrVersion = timeout;
            data.dwInfoFlags = ToNativeBalloonIcon (icon);

            InvokeShellNotifyIcon (NIM.MODIFY, data, "show tray balloon notification");
        }

        /// <inheritdoc/>
        public int ShowContextMenu (IReadOnlyList<PlatformTrayMenuItem> items, PixelPoint screenLocation)
        {
            ThrowIfDisposed ();
            ArgumentNullException.ThrowIfNull (items);

            if (items.Count == 0)
                return 0;

            var menu = CreateNativeMenu (items);

            try {
                SetForegroundWindow (hwnd);

                var command = TrackPopupMenu (
                    menu,
                    TrackPopupMenuFlags.LEFTALIGN | TrackPopupMenuFlags.TOPALIGN |
                    TrackPopupMenuFlags.RIGHTBUTTON | TrackPopupMenuFlags.RETURNCMD,
                    screenLocation.X,
                    screenLocation.Y,
                    0,
                    hwnd,
                    IntPtr.Zero);

                // The shell expects a benign message after a notification-area popup menu;
                // without it, the menu can remain in a sticky state after dismissal.
                PostMessage (hwnd, (uint)WindowsMessage.WM_NULL, IntPtr.Zero, IntPtr.Zero);

                return command;
            } finally {
                DestroyMenu (menu);
            }
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (disposed)
                return;

            DeleteShellIcon ();
            DestroyIconHandle ();

            if (hwnd != IntPtr.Zero)
                DestroyWindow (hwnd);

            UnregisterClass (class_name, hinstance);

            disposed = true;
        }

        private static string Clamp (string? value, int maxLength)
        {
            if (string.IsNullOrEmpty (value))
                return string.Empty;

            return value.Length <= maxLength ? value : value.Substring (0, maxLength);
        }

        private static void AppendNativeMenuItem (IntPtr menu, PlatformTrayMenuItem item)
        {
            if (item.Separator) {
                AppendNativeMenuItem (menu, MenuFlags.SEPARATOR, UIntPtr.Zero, null, "append a tray context menu separator");
                return;
            }

            var flags = CreateNativeMenuFlags (item);

            if (item.Items.Count > 0) {
                var submenu = CreateNativeMenu (item.Items);

                try {
                    AppendNativeMenuItem (menu, flags | MenuFlags.POPUP, ToUIntPtr (submenu), item.Text, "append a tray context submenu");
                } catch {
                    DestroyMenu (submenu);
                    throw;
                }

                return;
            }

            AppendNativeMenuItem (menu, flags | MenuFlags.STRING, new UIntPtr ((uint)item.CommandId), item.Text, "append a tray context menu item");
        }

        private static void AppendNativeMenuItem (IntPtr menu, MenuFlags flags, UIntPtr id, string? text, string operation)
        {
            if (AppendMenu (menu, flags, id, text))
                return;

            var error = Marshal.GetLastWin32Error ();

            if (error != 0)
                throw new Win32Exception (error, $"Could not {operation}.");

            throw new InvalidOperationException ($"Could not {operation}.");
        }

        private static IntPtr CreateNativeMenu (IReadOnlyList<PlatformTrayMenuItem> items)
        {
            var menu = CreatePopupMenu ();

            if (menu == IntPtr.Zero)
                throw new Win32Exception (Marshal.GetLastWin32Error (), "Could not create a tray context menu.");

            try {
                foreach (var item in items)
                    AppendNativeMenuItem (menu, item);

                return menu;
            } catch {
                DestroyMenu (menu);
                throw;
            }
        }

        private static MenuFlags CreateNativeMenuFlags (PlatformTrayMenuItem item)
        {
            var flags = MenuFlags.STRING;

            if (!item.Enabled)
                flags |= MenuFlags.DISABLED | MenuFlags.GRAYED;

            if (item.Checked)
                flags |= MenuFlags.CHECKED;

            return flags;
        }

        private static IntPtr CreateMessageWindow (WndProc wndProc, IntPtr hinstance, string className)
        {
            var wnd_class = new WNDCLASSEX {
                cbSize = Marshal.SizeOf<WNDCLASSEX> (),
                hInstance = hinstance,
                lpfnWndProc = wndProc,
                lpszClassName = className
            };

            var atom = RegisterClassEx (ref wnd_class);

            if (atom == 0)
                throw new Win32Exception (Marshal.GetLastWin32Error (), "Could not register the NotifyIcon message window class.");

            var handle = CreateWindowEx (
                0,
                atom,
                null,
                0,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (handle == IntPtr.Zero) {
                var error = Marshal.GetLastWin32Error ();

                UnregisterClass (className, hinstance);
                throw new Win32Exception (error, "Could not create the NotifyIcon message window.");
            }

            return handle;
        }

        private static IntPtr CreateIconHandle (SKBitmap bitmap)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast (6, 1))
                throw new PlatformNotSupportedException ("Windows tray icon handles require Windows 7 or later.");

            using var image = SKImage.FromBitmap (bitmap);
            using var data = image.Encode (SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream (data.ToArray ());
            using var drawing_bitmap = new SdBitmap (stream);

            return drawing_bitmap.GetHicon ();
        }

        private static PlatformTrayIconMouseEventArgs CreateMouseEventArgs (MouseButton button, int clicks)
        {
            var location = PixelPoint.Origin;

            if (GetCursorPos (out var point))
                location = new PixelPoint (point.X, point.Y);

            return new PlatformTrayIconMouseEventArgs (button, clicks, location);
        }

        private static void InvokeShellNotifyIcon (NIM message, NOTIFYICONDATA data, string operation)
        {
            if (Shell_NotifyIcon (message, data) != 0)
                return;

            var error = Marshal.GetLastWin32Error ();

            if (error != 0)
                throw new Win32Exception (error, $"Could not {operation}.");

            throw new InvalidOperationException ($"Could not {operation}.");
        }

        private static NIIF ToNativeBalloonIcon (PlatformBalloonIcon icon)
            => icon switch {
                PlatformBalloonIcon.Info => NIIF.INFO,
                PlatformBalloonIcon.Warning => NIIF.WARNING,
                PlatformBalloonIcon.Error => NIIF.ERROR,
                _ => NIIF.NONE
            };

        private static UIntPtr ToUIntPtr (IntPtr value)
        {
            if (IntPtr.Size == 8)
                return new UIntPtr ((ulong)value.ToInt64 ());

            return new UIntPtr ((uint)value.ToInt32 ());
        }

        private void AddShellIcon ()
        {
            if (added)
                return;

            if (hicon == IntPtr.Zero)
                throw new InvalidOperationException ("Assign an icon before making a Windows tray icon visible.");

            InvokeShellNotifyIcon (NIM.ADD, CreateNotifyIconData (NIF.MESSAGE | NIF.ICON | NIF.TIP), "add the Windows tray icon");
            added = true;
        }

        private NOTIFYICONDATA CreateNotifyIconData (NIF flags)
            => new NOTIFYICONDATA {
                hWnd = hwnd,
                uID = IconId,
                uFlags = flags,
                uCallbackMessage = s_notifyIconCallbackMessage,
                hIcon = hicon,
                szTip = Clamp (text, MaxTooltipLength)
            };

        private void DeleteShellIcon ()
        {
            if (!added)
                return;

            Shell_NotifyIcon (NIM.DELETE, CreateNotifyIconData (0));
            added = false;
        }

        private void DestroyIconHandle ()
        {
            if (hicon == IntPtr.Zero)
                return;

            DestroyIcon (hicon);
            hicon = IntPtr.Zero;
        }

        private void ProcessNotifyMessage (int message)
        {
            switch ((WindowsMessage)message) {
                case WindowsMessage.WM_MOUSEMOVE:
                    MouseMove?.Invoke (this, CreateMouseEventArgs (MouseButton.None, 0));
                    break;
                case WindowsMessage.WM_LBUTTONDOWN:
                    MouseDown?.Invoke (this, CreateMouseEventArgs (MouseButton.Left, 1));
                    break;
                case WindowsMessage.WM_LBUTTONUP:
                    MouseUp?.Invoke (this, CreateMouseEventArgs (MouseButton.Left, 1));
                    break;
                case WindowsMessage.WM_LBUTTONDBLCLK:
                    DoubleClick?.Invoke (this, CreateMouseEventArgs (MouseButton.Left, 2));
                    break;
                case WindowsMessage.WM_RBUTTONDOWN:
                    MouseDown?.Invoke (this, CreateMouseEventArgs (MouseButton.Right, 1));
                    break;
                case WindowsMessage.WM_RBUTTONUP:
                    MouseUp?.Invoke (this, CreateMouseEventArgs (MouseButton.Right, 1));
                    break;
                case WindowsMessage.WM_RBUTTONDBLCLK:
                    DoubleClick?.Invoke (this, CreateMouseEventArgs (MouseButton.Right, 2));
                    break;
                case WindowsMessage.WM_MBUTTONDOWN:
                    MouseDown?.Invoke (this, CreateMouseEventArgs (MouseButton.Middle, 1));
                    break;
                case WindowsMessage.WM_MBUTTONUP:
                    MouseUp?.Invoke (this, CreateMouseEventArgs (MouseButton.Middle, 1));
                    break;
                case WindowsMessage.WM_MBUTTONDBLCLK:
                    DoubleClick?.Invoke (this, CreateMouseEventArgs (MouseButton.Middle, 2));
                    break;
            }
        }

        private void ReplaceIconHandle ()
        {
            DestroyIconHandle ();

            if (icon is not null)
                hicon = CreateIconHandle (icon);
        }

        private void RestoreShellIconAfterExplorerRestart ()
        {
            if (!visible)
                return;

            added = false;
            AddShellIcon ();
        }

        private void ThrowIfDisposed ()
        {
            ObjectDisposedException.ThrowIf (disposed, this);
        }

        private void UpdateShellIcon ()
        {
            if (!added)
                AddShellIcon ();
            else
                InvokeShellNotifyIcon (NIM.MODIFY, CreateNotifyIconData (NIF.MESSAGE | NIF.ICON | NIF.TIP), "update the Windows tray icon");
        }

        private IntPtr WndProc (IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == s_notifyIconCallbackMessage) {
                ProcessNotifyMessage (lParam.ToInt32 ());
                return IntPtr.Zero;
            }

            if (s_taskbarCreatedMessage != 0 && msg == s_taskbarCreatedMessage) {
                RestoreShellIconAfterExplorerRestart ();
                return IntPtr.Zero;
            }

            return DefWindowProc (hWnd, msg, wParam, lParam);
        }
    }
}
