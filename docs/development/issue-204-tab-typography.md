# Tab header typography (#204)

`TabControl` headers now use their owner's effective font, font size, line height and
letter spacing. A standalone `TabStrip` uses its own effective style. Selected captions
add bold emphasis while retaining italic, underline and strikeout. Disabled captions
retain the existing disabled color and do not add selected emphasis.

```csharp
var tabs = new TabControl { Dock = DockStyle.Fill };
tabs.AddTab("General");
tabs.AddClosableTab("Advanced");
tabs.ShowCloseButtons = true;

// UI thread: no access to the implicit strip is required.
Font original = tabs.Font;
tabs.Font = new Font(original.FamilyName, original.SizeInPoints * 1.75f, original.Style);
// Restore the application's original typography, without changing monitor DPI.
tabs.Font = original;
```

Both measurement and painting resolve the same borrowed typeface and style flags through
the existing shaping engine. There is no per-frame typeface creation or ownership
transfer. Text size and DPI are independent: the logical font and item geometry are
converted once to device pixels. Each padding edge is rounded separately in both paths;
rounding their sum previously left a one-pixel deficit at fractional DPI. Natural text
width is rounded upward. `TabMinWidth` and `TabMaxWidth` are logical pixels; zero maximum
removes the width cap. A capped caption uses a single-line ellipsis.

The implicit strip grows or shrinks above the legacy 31-logical-pixel minimum to contain
its text, icons and padding. Page docking follows the resulting header height. A default
standalone strip behaves similarly. An explicitly **changed** standalone `Height` remains
authored; setting `AutoSize = true` opts back into natural sizing. An unchanged Height
assignment is the existing no-op and does not pin the default height. Fill/Left/Right
docking and vertical stretch anchoring keep ownership of the height. `MinimumSize` and
`MaximumSize` still apply, so explicit constraints can intentionally clip large text.
Preferred size reports natural content requirements even when actual height is constrained.

Change `Font`, `LineHeight` or `LetterSpacing` on the UI thread. These live properties
request layout and repaint. Direct `Style.Font`, `Style.FontSize` or inherited-style edits
follow the framework's existing explicit refresh convention:

```csharp
tabs.Style.FontSize = 28;
tabs.PerformLayout();
tabs.Invalidate();
```

Theme typography, presentation metrics and DPI changes refresh measurement. A color-only
theme change stays on the repaint path. Item icons remain borrowed images in a 16-by-16
logical-pixel slot; close targets, pinned behavior and keyboard navigation use the same
item bounds as painting. Assigning a larger source bitmap does not enlarge that slot.

`ControlGallery` now has a 100/125/150/175/200% text-size button on its TabControl page and
a standalone 200% italic strip on its TabStrip page. The default generated application
is unchanged.

## Regression evidence

The initial 22 regressions all failed against the previous implementation. Forty-four
focused cases now cover default and custom fonts before first layout, runtime font/reset,
inherited styles and overrides, five DPI values crossed with five text scales, selected
font flags, live metrics, maximum size, runtime DPI changes, authored height, dock/anchor
ownership, icons, close targets, pinned tabs and keyboard selection. Raster comparisons
use an ordinary Label with the same font as an independent visible reference.

Development failures are retained: an initial test compilation attempt accessed internal
members from the external test project; the corrected fixture uses test-only inspection
of the implicit strip. Subsequent tests exposed unchanged-bounds docking being mistaken
for an authored Height, per-edge padding rounding at 125% DPI, and natural height overriding
vertical docking. These were corrected without relaxing the raster assertions. Full build,
suite, CI and native/manual results are recorded in the implementing PR; headless pixel
tests alone are not physical-monitor or Android qualification.

The first full Debug solution run passed **5987/5987**, including all 441 Windows tests,
after restore and build (nine existing warnings). An owned native ControlGallery window
was then driven through Windows UI Automation: select TabControl, invoke the text-size
button through all five settings, and select TabStrip. Unmodified PrintWindow captures
at 100% and 200% text size and of the standalone italic strip were inspected: captions
are readable and unclipped, the close glyph remains aligned and page content moves below
the enlarged header. The app closed normally. This used the software renderer on the
current desktop; it is automated native interaction and image inspection, not a human
monitor-switching, GPU scanout or physical Android test. Source, logs and PNG evidence
are retained separately from historical runs under the scope re-audit artifacts.
