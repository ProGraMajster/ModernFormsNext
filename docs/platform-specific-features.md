# Platform-Specific Features

ModernFormsNext keeps shared controls and rendering platform-neutral. Features that require
operating system integration are exposed through framework APIs and implemented by platform
backends.

Windows is currently the primary and best-supported runtime target. When a backend does not
provide a platform feature, the public framework API should fail clearly instead of pretending
that the feature worked.

## NotifyIcon

`NotifyIcon` represents an icon in the operating system notification area. The first
implementation is provided by the Windows backend.

Use `NotifyIcon` when an application should remain available from the Windows notification
area while no normal form is visible, or when it needs to show small status notifications.

```csharp
using ModernFormsNext;
using SkiaSharp;

using var trayIcon = new NotifyIcon
{
    Icon = SKBitmap.Decode("app-icon.png"),
    Text = "ModernFormsNext app",
    Visible = true
};

trayIcon.Click += (_, _) => mainForm.Show();
trayIcon.ShowBalloonTip(
    3000,
    "ModernFormsNext",
    "The application is still running.",
    NotifyIconBalloonIcon.Info);
```

### Tray Context Menu

Use `NotifyIconContextMenu` for tray icons. Do not use the regular `ContextMenu` type here:
regular context menus are controls hosted in ModernFormsNext popup windows, while tray icons
are non-visual operating system objects.

```csharp
var menu = new NotifyIconContextMenu();

menu.Items.Add("Open", (_, _) => mainForm.Show());

var pauseItem = menu.Items.Add("Pause notifications");
pauseItem.Checked = true;
pauseItem.Click += (_, _) =>
{
    pauseItem.Checked = !pauseItem.Checked;
};

menu.Items.AddSeparator();
menu.Items.Add("Exit", (_, _) => Application.Exit());

trayIcon.ContextMenu = menu;
```

Important behavior:

- Target `net10.0-windows` and include the Windows backend when using this component.
- Creating `NotifyIcon` on a platform whose backend does not provide tray icon support throws
  `PlatformNotSupportedException`.
- Set `Icon` before setting `Visible` to `true`; otherwise `Visible` throws
  `InvalidOperationException`.
- The backend copies the supplied `SKBitmap` into a native icon handle. The caller still owns
  the original bitmap and should dispose it according to application lifetime rules.
- Dispose the `NotifyIcon` when it is no longer needed so the native tray icon and hidden
  message window are removed.
- `ShowBalloonTip` requires the icon to be visible. Modern Windows versions may ignore the
  requested timeout and apply system notification timing.
- `NotifyIcon.ContextMenu` is shown by the backend as a native tray menu on right-click.
- `NotifyIconMenuItem.Checked` only controls the check mark shown by the platform menu. It
  does not toggle automatically when the user selects the item.
- `NotifyIconMenuItem.Items` creates a native submenu. Disabled items and separators are
  handled by the platform menu.
