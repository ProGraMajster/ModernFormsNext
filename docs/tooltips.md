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

`UseAnimation`, `UseFading`, and full inactive-window `ShowAlways` behavior are stored for source compatibility, but current ModernFormsNext tooltip popups do not use native operating system tooltip animation or inactive-window display behavior.
