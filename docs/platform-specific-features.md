# Platform-Specific Features

ModernFormsNext keeps shared controls and rendering platform-neutral. Features that require
operating system integration are exposed through framework APIs and implemented by platform
backends.

Windows is currently the primary and best-supported runtime target. When a backend does not
provide a platform feature, the public framework API should fail clearly instead of pretending
that the feature worked.

## Accessibility

The [canonical AccessibleObject model](accessibility/semantic-model.md) describes control
identity, names, state, actions, ranges and logical children. Windows and Android adapt that
same model. Phase 4's current implementation adds the following capabilities; final-source
validation remains distinct from historical Phase 2/3 evidence.

| Capability | Windows | Android |
| --- | --- | --- |
| Current link/numeric/scrollbar composites | Hyperlink/Spinner/RangeValue and real logical parts | Native labelled nodes, range metadata and actions over the same controls |
| Viewports | Scroll/ScrollItem, native percentages and amounts | Directional/forward/backward actions, offsets and ShowOnScreen; granular amounts are API-gated |
| Managed grids | Grid/GridItem/Table/TableItem, headers, selection and normal value editing | Collection/item metadata, headings, selection, SetText and reveal |
| Date/calendar | Existing native popup root with date/month/year peers and grid semantics | Value, checkbox and stepping on the windowless host; no unsupported Form/calendar popup advertised |
| Existing text editors | Text/TextRange, supported attributes, selection, shaped range geometry and scroll | Native selection and movement granularity over the same provider; no character-location extra-data claim |
| Preference detection | High-contrast system colors and owned UISettings text-scale observation | Current Activity font scale and API 34+ contrast; unavailable fields remain unknown |

The [current-control](accessibility/current-controls.md),
[viewport](accessibility/scroll-viewports.md),
[grid/calendar](accessibility/grids-and-calendars.md), and [text](accessibility-text.md)
guides specify precision, privacy, thread affinity and retained-object rules. Optional native
preference detection does not automatically replace an app's theme. The
[opt-in preference consumer](accessibility/preferences.md) selects an authored ThemeDefinition
and applies its typography multiplier once through the existing ThemeManager.

Read-only [diagnostics and Designer metadata](accessibility/diagnostics-and-designer.md) consume
the shared model. General recycled containers remain #55, the full inspector/picker remains
#61, Android Form/window hosting remains #72, and physical Android reliability remains #69.
The [Phase 4 Android runner](accessibility/android-phase4-validation.md) is explicitly enabled;
its result does not substitute for TalkBack/manual or physical-device acceptance. Issue #59
remains open while those coverage and validation boundaries remain.

## Input, lifecycle and test infrastructure

[Shared IME clients](text-input.md), [commands](commands.md),
[Android hardware-key routing](android-hardware-input.md), and
[application lifecycle/state handoff](application-lifecycle.md) are already implemented
through the normal control and backend paths. Their native/vendor/device limits remain in
their respective matrices. The [headless TestHost](testing/testhost.md) supplies deterministic
input, focus, layout, scoped services and detached control/raster snapshots;
[AutomationSession](automation.md) provides bounded semantic snapshots and actions, with a
separately enabled [Windows live bridge](automation-live-bridge.md). These are available
foundations, not prerequisites still missing from the accessibility model.

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

### Activation Window

Assign `ActivationWindow` when a tray icon should restore or toggle a normal application
window after the user left-clicks the icon. `ActivationBehavior` controls what happens
after the `Click` event has been raised.

```csharp
using var trayIcon = new NotifyIcon
{
    Icon = SKBitmap.Decode("app-icon.png"),
    ActivationWindow = mainForm,
    ActivationBehavior = NotifyIconActivationBehavior.ToggleWindow,
    Text = "ModernFormsNext app",
    Visible = true
};
```

`NotifyIconActivationBehavior.ShowWindow` shows the assigned form when it is hidden,
restores it when it is minimized, and activates it. `ToggleWindow` hides the form when
it is visible and not minimized; otherwise it uses the same show, restore, and activate
behavior.

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
- `ActivationBehavior` runs only for left-click activation and only when
  `ActivationWindow` is assigned. The `Click` event is raised before the automatic window
  activation behavior runs.
- `NotifyIcon.ContextMenu` is shown by the backend as a native tray menu on right-click.
- `NotifyIconMenuItem.Checked` only controls the check mark shown by the platform menu. It
  does not toggle automatically when the user selects the item.
- `NotifyIconMenuItem.Items` creates a native submenu. Disabled items and separators are
  handled by the platform menu.
