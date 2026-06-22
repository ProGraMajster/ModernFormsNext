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
- Context menu integration is not implemented yet. It needs a separate design because the
  current `ContextMenu` API is tied to a `Control`/`Form` owner, while a tray icon is not a
  visual control.
