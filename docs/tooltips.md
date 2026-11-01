# ToolTips

`ToolTip` displays short explanatory text for ModernFormsNext controls. The API follows the familiar Windows Forms usage pattern, but the implementation is platform-neutral: ModernFormsNext shows a `PopupWindow` and renders the tooltip with SkiaSharp instead of using native WinForms tooltip windows.

## Basic Usage

```csharp
var saveButton = new Button
{
    Text = "Save",
    Width = 120,
    Height = 34
};

var toolTip = new ToolTip();
toolTip.SetToolTip(saveButton, "Save the current document.");
```

`SetToolTip(control, null)` and `SetToolTip(control, "")` remove the association.

## Styling

Tooltips use `SKColor` values like the rest of ModernFormsNext:

```csharp
var toolTip = new ToolTip
{
    BackColor = new SKColor(255, 255, 225),
    ForeColor = SKColors.Black,
    ToolTipTitle = "ModernFormsNext",
    ToolTipIcon = ToolTipIcon.Info,
    IsBalloon = true
};
```

`IsBalloon` currently changes the rounded rendering style. It does not create a native balloon tail.

For custom visual design without owner drawing, configure the tooltip's layout and drawing
properties directly:

```csharp
var toolTip = new ToolTip
{
    BackColor = new SKColor(32, 36, 42),
    ForeColor = SKColors.White,
    BorderColor = new SKColor(0, 120, 212),
    BorderWidth = 1,
    BorderRadius = 6,
    Padding = new Padding(12, 8, 12, 8),
    MaximumWidth = 280,
    MinimumTextLineHeight = 24,
    IconSize = 20,
    IconSpacing = 10,
    TitleSpacing = 4,
    TextAlign = ContentAlignment.MiddleLeft,
    TitleAlign = ContentAlignment.MiddleLeft,
    TitleForeColor = SKColors.White
};
```

`TextFont` and `TitleFont` accept ModernFormsNext `Font` values when a tooltip needs
custom typography. `IconColor` and `IconForegroundColor` customize the built-in
`Info`, `Warning`, and `Error` icons. `MinimumSize` can be used when several tooltips
should share the same visual footprint.

Automatic sizing measures the rounded device font and reflows at the final bounded width before selecting a logical height, so fractional DPI does not silently introduce a clipped extra body line. An explicit `Popup` size remains the application's responsibility.

## Manual Display

```csharp
toolTip.Show("Saved.", saveButton, 0, saveButton.Height + 8, 2500);
```

The point is relative to the associated control. The duration is in milliseconds.

## Owner Drawing

Owner-drawn tooltips use SkiaSharp:

```csharp
var toolTip = new ToolTip { OwnerDraw = true };

toolTip.Popup += (_, e) => e.ToolTipSize = new Size(240, 64);

toolTip.Draw += (_, e) =>
{
    e.DrawBackground();
    e.DrawText();
    e.DrawBorder();
};
```

`DrawToolTipEventArgs` exposes `SKCanvas`, not `System.Drawing.Graphics`. This is intentional so the component works with the ModernFormsNext rendering model across backends.

## Compatibility Notes

Supported Windows Forms-style members include `Active`, `AutomaticDelay`, `InitialDelay`, `ReshowDelay`, `AutoPopDelay`, `BackColor`, `ForeColor`, `IsBalloon`, `OwnerDraw`, `ShowAlways`, `StripAmpersands`, `ToolTipIcon`, `ToolTipTitle`, `UseAnimation`, `UseFading`, `SetToolTip`, `GetToolTip`, `RemoveAll`, `Show`, `Hide`, `Popup`, and `Draw`.

ModernFormsNext also exposes Skia/layout-oriented styling members that do not exist in
WinForms: `BorderColor`, `BorderWidth`, `BorderRadius`, `BalloonBorderRadius`,
`Padding`, `MaximumWidth`, `MinimumSize`, `MinimumTextLineHeight`, `IconColor`,
`IconForegroundColor`, `IconSize`, `IconSpacing`, `TextAlign`, `TextFont`,
`TitleAlign`, `TitleForeColor`, `TitleFont`, and `TitleSpacing`.

`UseAnimation`, `UseFading`, and `ShowAlways` affect ModernFormsNext presentation. They are not compatibility no-ops, and do not request native operating-system tooltip controls.

## Presentation and lifetime

Both effects are enabled by default and run through the existing shared animation scheduler:

| Property | Enabled | Disabled |
| --- | --- | --- |
| `UseAnimation` | Content moves from +6 logical pixels to its normal position on entrance, and toward -6 on exit. | No translation. |
| `UseFading` | The entire popup, including its background, fades from transparent to opaque on entrance and back on exit. | Opaque presentation. |

Entrance is nominally 120 ms and exit 80 ms with ease-out timing. The scheduler's duration scale applies. Reduced motion, disabled animations, a zero duration scale, or detected high contrast completes immediately. Changing either property during presentation settles the entrance or finishes the exit. Translation affects rendering within the existing popup clip, without reflowing the owner or its controls.

Windows uses uniform opacity on the existing popup window; Android uses its existing popup host view. An external backend without optional opacity support, or a backend refusing opacity, keeps opaque presentation. Translation can still run. Tooltips do not create a separate render tree, timer service, focus scope or native tooltip control.

`InitialDelay`, `ReshowDelay` and `AutoPopDelay` still use the existing dispatcher timers. Recent-hide history uses the scheduler's monotonic clock. Manual `Show` bypasses the initial delay. A positive duration starts at show and begins dismissal at its deadline, followed by the bounded exit; zero keeps a manual tooltip until dismissal. `Hide` begins the exit, and repeating it cannot extend the deadline. Replacing a tooltip reuses its current frame and cancels stale work; an old completion cannot dismiss the replacement. Switching owning forms closes the old popup. `Active = false`, association removal and disposal retire it immediately.

Use the component and its associated controls on their owning UI thread. The caller owns and disposes the component; the component borrows the form and anchors, owns its popup, and observes settings/lifecycle only while pending or visible. Manual anchors are observed for retirement without adding an automatic-hover association. `Popup` runs before configuration and can cancel or resize; a newer show/hide/dispose from a callback takes precedence. Observer failures are reported after owned cleanup; failures at an asynchronous animation endpoint are also written to the existing trace diagnostics because scheduler ticks isolate callback exceptions. Finalization performs no managed UI cleanup.

## Inactive windows and input

| Owner/application state | `ShowAlways = false` | `ShowAlways = true` |
| --- | --- | --- |
| Visible active owner | Eligible | Eligible |
| Visible inactive desktop owner, including process deactivation while the desktop host remains foreground | Pending and visible tooltips are dismissed | Eligible without activation |
| Hidden, closed or disposed owner; hidden, disposed or detached anchor | Dismiss immediately | Dismiss immediately |
| Known application background, suspension, no host or exit | Dismiss immediately | Dismiss immediately |

Returning to an eligible state does not resurrect a dismissed tooltip. Each component presents for one owning form at a time; separate components can serve different windows. Android popups remain confined to the Activity's lifetime. When an external backend cannot detect lifecycle, owner visibility/activation remains the fallback; its native popup must support nonactivating presentation to preserve native focus.

Tooltips are passive: they do not take keyboard focus, replace an interactive menu/list popup, or lease the parent's native text input/IME connection. First-party backends also avoid native focus acquisition and pass pointer input through the tooltip. For example:

```csharp
var toolTip = new ToolTip
{
    UseAnimation = true,
    UseFading = false, // Translation without opacity.
    ShowAlways = true // Eligible while this visible desktop owner is inactive.
};
toolTip.SetToolTip(saveButton, "Save the current document.");
// Dispose toolTip on the UI thread when its owning UI is retired.
```

## Accessibility and contrast

The existing control peer exposes the tooltip role/type and a name combining title and body. It is semantically invisible until entrance completes, announces show at that boundary, and announces hide when dismissal begins. Opacity/translation frames do not generate repeated name/show announcements. Replacing content keeps the same peer identity for the same popup and publishes the changed name through the canonical notification path.

Detected high contrast uses the platform's background/foreground palette for the default renderer without changing authored colors, and settles visual transitions immediately. If a palette is unavailable, authored colors remain the fallback. Owner-drawn content remains responsible for its own accessible contrast. Windows uses the existing accessibility bridge; Android receives the canonical tooltip classification through its existing adapter. This does not establish full TalkBack/screen-reader announcement parity; that broader work remains under #59. Headless notification tests do not constitute a human screen-reader observation.
