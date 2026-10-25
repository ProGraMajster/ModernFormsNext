# Shared shadows and typography

`Control.Shadow` describes an opt-in outer shadow of the control's border box. The shared Skia
compositor paints it behind that control in the existing parent composition pass. It uses the
current border radius and the same presentation transform and effective opacity as the content.
Disabled-state opacity therefore applies to the shadow too. No native shadow or extra window is created.

```csharp
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Drawing;

var button = new Button
{
    Text = "Save",
    Bounds = new Rectangle(24, 24, 160, 40),
    Shadow = new Shadow(Color.FromArgb(80, Color.Black), blurRadius: 5, offsetY: 3),
    LetterSpacing = 0.75
};
button.Style.Border.Radius = 8;
```

Offsets, spread and blur are logical pixels. `BlurRadius` is the Gaussian standard deviation;
it is not a CSS blur diameter. Blur is bounded to 0–128, spread to -128–128, and offsets to
-2048–2048 logical pixels. All distances must be finite. Negative spread contracts the silhouette;
an empty silhouette draws nothing. The immutable description owns no native resources and may
be shared. The control lazily owns at most one paint, blur filter and rounded interior cutout path.
The cutout keeps transparent control interiors free of shadow pixels. Stable frames and changes
to control bounds or opacity reuse that cache; replacing the description or changing DPI releases
or replaces the relevant native objects. Explicit control disposal releases the cache. Finalization
does not traverse the UI or invoke managed UI cleanup.

Every ancestor clips shadows to its client area. Shadows do not change preferred size, layout,
hit testing, focus or scroll extents. Leave parent space around an elevated control; a root surface
cannot draw beyond its bounds. Moving, removing or replacing a shadow invalidates the composition
owner so old pixels are erased. GroupBox's existing `ShowShadow` API remains available; avoid enabling
both effects for the same appearance. This feature does not add shadows to an embedded native view's
own content or to native window chrome.

## Typography units and precedence

`Control.LineHeight` and `Control.LetterSpacing` are nullable `double` values backed by the normal
style. A null value inherits through the current style's parent chain, then falls back to
`Theme.LineHeight` (1) and `Theme.LetterSpacing` (0). `ControlStyle` exposes the same optional metrics.
Configure state styles before activation; direct style mutations follow the existing explicit
invalidation convention. State changes apply these metrics discretely through the existing layout
notification path; this feature does not add text-metric interpolation.

Line height is a positive multiplier, at most 16, of the font's natural line height. Character spacing
is an additional distance from -1024 through 1024 logical pixels; negative spacing is supported. The shaping engine
continues to handle ligatures, combining characters and bidirectional text. Spacing is scaled once
for DPI without integer rounding. Values must remain representable by the finite single-precision
coordinates consumed by Skia; excessive scaled text extents are rejected before shaping.
Previously these optional theme metrics were stored without global rendering. Values outside the
documented rendering bounds are now rejected by ThemeTypography and the consuming properties.

```csharp
var caption = new Label
{
    Text = "First line\nSecond line",
    Multiline = true,
    LineHeight = 1.5,
    LetterSpacing = 0.5
};
caption.Resources["Tracking"] = 0.75d;
caption.SetResourceReference(nameof(Control.LetterSpacing), "Tracking");
```

Changing a control metric on the UI thread retires cached content layout, repaints and requests
parent measurement for AutoSize. It does not change the font or raise FontChanged. TextBox reuses
its shaped block while metrics are unchanged and rebuilds it when effective spacing, line height
or DPI changes, keeping painting, selection, caret and hit testing on that same block.

| Consumer | Support |
|---|---|
| Shared control-aware TextMeasurer and SKCanvas text helpers | Matching measurement and painting metrics |
| Label, Button, CheckBox and RadioButton using shared text/image layout | Shared typography |
| Other helper consumers, including list items | Shared painting metrics; fixed row heights and other independent sizing policies still apply |
| TextBox and composed controls using its editor | Shared shaped block, caret/selection/hit testing |
| RichTextBox, MarkdownEditor, DocumentViewer and independently styled document runs | Document-specific styles; no automatic override |
| LinkLabel's character-level layout, custom renderers, explicit font-only SKCanvas/TextMeasurer calls | No automatic control metric propagation |
| Windows and Android custom Skia control trees | Same shared implementation; native qualification is recorded separately |
| Native window chrome and embedded native views | No native-content typography or shadow override |

## Themes, JSON and Designer

The Body `ThemeTypography` token supplies global defaults atomically through ThemeManager. Other
roles remain explicit application choices. Existing JSON lineHeight/letterSpacing values are
consumed without changing their units. Named shadows are inherited and published under typed
resource keys; built-in themes define Low, Medium and High roles without automatically elevating controls.

```csharp
button.SetResourceReference(nameof(Control.Shadow), ThemeTokens.Shadows.Medium.ResourceKey);
```

```json
{
  "schemaVersion": 1,
  "id": "example.appearance",
  "name": "Appearance",
  "typography": {
    "Body": { "fontFamily": "Segoe UI", "size": 14, "lineHeight": 1.4, "letterSpacing": 0.5 }
  },
  "shadows": {
    "Card": { "color": "#50000000", "blurRadius": 5, "offsetX": 0, "offsetY": 3, "spread": 0 }
  },
  "resources": {
    "Tracking": { "type": "number", "value": 0.75 }
  }
}
```

Custom resources may also use the closed `shadow` discriminator. Shadows count toward the existing
theme token/document budgets. Unknown members and out-of-range distances are rejected; no type names,
constructors, delegates or resource factories are resolved from theme JSON.

The Designer stores a shadow as eight known scalar members, validates their ranges before conversion
or generation, and emits a trusted Shadow construction. The Property Grid accepts
`Shadow(#AARRGGBB; blur; offsetX; offsetY; spread)` and bounds that text to 256 characters. Clearing
the value disables the effect. Control metrics use normal numeric metadata. All values remain subject
to the shared .mfdesign text, count, depth and preview bitmap limits. A standalone node preview is
clipped to its authored bitmap; outer pixels require the containing runtime composition. The default
application template is unchanged. Run the optional Gallery **Shadows and Typography** example for
an interactive composition with a spaced editor and disabled/opacity states.

Native Android automation uses `Invoke-ReleaseValidation.ps1 -Scenario ShadowTypography` with an
explicit device and APK. It preserves device settings and captures default, alternative and recreated
Activity frames. Synthetic automation is separate from manual visual judgment, vendor IME and TalkBack.
This capture scenario requires Android 10/API 29 or later and an accelerated native view so it can
observe Android's frame-commit callback. The shared framework renderer remains software Skia.
Control paint callbacks and an idle UI queue alone do not prove native frame submission. The runner
waits for a committed frame and requires changed pixels inside the sample card's actual screen bounds.
A negative check verifies that changing an outside pixel cannot satisfy that assertion, including on
edge-to-edge views that extend underneath system bars. This scenario does not qualify Android
API 23–28 or measure GPU/scanout timing.
