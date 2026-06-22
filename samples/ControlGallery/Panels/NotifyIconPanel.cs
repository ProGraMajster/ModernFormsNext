using System;
using System.Runtime.Versioning;
using ModernFormsNext;
using SkiaSharp;

namespace ControlGallery.Panels
{
    public class NotifyIconPanel : BasePanel
    {
        private readonly Label status_label;
        private NotifyIcon notify_icon;
        private NotifyIconContextMenu context_menu;
        private NotifyIconMenuItem pause_item;
        private SKBitmap tray_icon_bitmap;
        private int tooltip_version;

        public NotifyIconPanel ()
        {
            Controls.Add (new Label {
                Text = "NotifyIcon",
                Left = 10,
                Top = 10,
                Width = 240,
                Height = 25
            });

            var show_button = Controls.Add (new Button {
                Text = "Show Tray Icon",
                Left = 10,
                Top = 45,
                Width = 150
            });
            show_button.Click += (sender, e) => ShowTrayIcon ();

            var hide_button = Controls.Add (new Button {
                Text = "Hide Tray Icon",
                Left = 170,
                Top = 45,
                Width = 150
            });
            hide_button.Click += (sender, e) => HideTrayIcon ();

            var balloon_button = Controls.Add (new Button {
                Text = "Balloon Tip",
                Left = 10,
                Top = 85,
                Width = 150
            });
            balloon_button.Click += (sender, e) => ShowBalloonTip ();

            var tooltip_button = Controls.Add (new Button {
                Text = "Change Tooltip",
                Left = 170,
                Top = 85,
                Width = 150
            });
            tooltip_button.Click += (sender, e) => ChangeTooltip ();

            var checked_button = Controls.Add (new Button {
                Text = "Toggle Checked Item",
                Left = 10,
                Top = 125,
                Width = 150
            });
            checked_button.Click += (sender, e) => ToggleCheckedItem ();

            var dispose_button = Controls.Add (new Button {
                Text = "Dispose Icon",
                Left = 170,
                Top = 125,
                Width = 150
            });
            dispose_button.Click += (sender, e) => DisposeTrayIcon ();

            Controls.Add (new Label {
                Text = "Right-click the notification area icon to test its native menu.",
                Left = 10,
                Top = 175,
                Width = 460,
                Height = 25
            });

            status_label = Controls.Add (new Label {
                Text = "Tray icon is not running.",
                Left = 10,
                Top = 210,
                Width = 600,
                Height = 60,
                Multiline = true
            });
        }

        public override void UnloadPanel ()
        {
            DisposeTrayIcon ();
        }

        private void ChangeTooltip ()
        {
            if (!IsWindowsTrayAvailable ())
                return;

            if (!EnsureTrayIcon ())
                return;

            tooltip_version++;
            notify_icon.Text = $"ControlGallery NotifyIcon #{tooltip_version}";
            SetStatus ($"Tooltip changed to version {tooltip_version}.");
        }

        private NotifyIconContextMenu CreateContextMenu ()
        {
            var menu = new NotifyIconContextMenu ();

            menu.Items.Add ("Open status message", (sender, e) => SetStatus ("Open item selected from tray menu."));

            pause_item = menu.Items.Add ("Pause notifications");
            pause_item.Checked = true;
            pause_item.Click += (sender, e) => ToggleCheckedItem ();

            var disabled_item = menu.Items.Add ("Disabled item");
            disabled_item.Enabled = false;

            var submenu = menu.Items.Add ("More actions");
            submenu.Items.Add ("Show balloon", (sender, e) => ShowBalloonTip ());
            submenu.Items.Add ("Change tooltip", (sender, e) => ChangeTooltip ());

            menu.Items.AddSeparator ();
            menu.Items.Add ("Hide tray icon", (sender, e) => HideTrayIcon ());

            return menu;
        }

        private static SKBitmap CreateTrayIconBitmap ()
        {
            var bitmap = new SKBitmap (32, 32, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            using var canvas = new SKCanvas (bitmap);
            using var background_paint = new SKPaint { Color = new SKColor (0, 120, 215), IsAntialias = true };
            using var accent_paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var shadow_paint = new SKPaint { Color = new SKColor (0, 54, 98), IsAntialias = true };

            canvas.Clear (SKColors.Transparent);
            canvas.DrawCircle (16, 16, 15, background_paint);
            canvas.DrawCircle (16, 17, 9, shadow_paint);
            canvas.DrawCircle (16, 15, 9, accent_paint);
            canvas.DrawCircle (16, 15, 5, background_paint);

            return bitmap;
        }

        private void CreateNotifyIcon ()
        {
            if (!OperatingSystem.IsWindows ()) {
                SetStatus ("NotifyIcon manual test is available only on Windows.");
                return;
            }

            tray_icon_bitmap = CreateTrayIconBitmap ();
            context_menu = CreateContextMenu ();
            notify_icon = new NotifyIcon {
                Icon = tray_icon_bitmap,
                ContextMenu = context_menu,
                Text = "ControlGallery NotifyIcon",
                Visible = true
            };

            notify_icon.Click += (sender, e) => SetStatus ($"Click: {e.Button} at {e.ScreenLocation.X}, {e.ScreenLocation.Y}.");
            notify_icon.DoubleClick += (sender, e) => SetStatus ($"DoubleClick: {e.Button}.");
            notify_icon.MouseDown += (sender, e) => SetStatus ($"MouseDown: {e.Button}.");
            notify_icon.MouseUp += (sender, e) => SetStatus ($"MouseUp: {e.Button}.");

            SetStatus ("Tray icon created. Right-click it to open the native tray menu.");
        }

        private void DisposeTrayIcon ()
        {
            notify_icon?.Dispose ();
            notify_icon = null;

            context_menu?.Dispose ();
            context_menu = null;

            tray_icon_bitmap?.Dispose ();
            tray_icon_bitmap = null;
            pause_item = null;
            tooltip_version = 0;

            SetStatus ("Tray icon disposed.");
        }

        private bool EnsureTrayIcon ()
        {
            if (notify_icon is null)
                CreateNotifyIcon ();

            return notify_icon is not null;
        }

        private void HideTrayIcon ()
        {
            if (!IsWindowsTrayAvailable ())
                return;

            if (notify_icon is null) {
                SetStatus ("Tray icon is not running.");
                return;
            }

            notify_icon.Visible = false;
            SetStatus ("Tray icon hidden.");
        }

        private void SetStatus (string message)
        {
            if (status_label is null)
                return;

            status_label.Text = $"{DateTime.Now:T} - {message}";
        }

        private void ShowBalloonTip ()
        {
            if (!IsWindowsTrayAvailable ())
                return;

            if (!EnsureTrayIcon ())
                return;

            if (!notify_icon.Visible)
                notify_icon.Visible = true;

            notify_icon.ShowBalloonTip (
                3000,
                "ControlGallery",
                "This notification came from NotifyIconPanel.",
                NotifyIconBalloonIcon.Info);

            SetStatus ("Balloon tip requested.");
        }

        private void ShowTrayIcon ()
        {
            if (!IsWindowsTrayAvailable ())
                return;

            if (!EnsureTrayIcon ())
                return;

            notify_icon.Visible = true;
            SetStatus ("Tray icon visible.");
        }

        private void ToggleCheckedItem ()
        {
            if (!EnsureTrayIcon ())
                return;

            pause_item.Checked = !pause_item.Checked;
            SetStatus ($"Pause notifications checked: {pause_item.Checked}.");
        }

        [SupportedOSPlatformGuard ("windows")]
        private bool IsWindowsTrayAvailable ()
        {
            if (OperatingSystem.IsWindows ())
                return true;

            SetStatus ("NotifyIcon manual test is available only on Windows.");
            return false;
        }
    }
}
