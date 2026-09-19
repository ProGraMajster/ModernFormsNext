# High DPI and coordinate ownership

ModernFormsNext keeps layout logical and its existing custom painting/input API in
device pixels. `Scaling` is physical pixels per logical pixel (for example 2.25).
Windows is the primary validated runtime for these changes.

| Value | Units / owner |
| --- | --- |
| Control Bounds, Location, Size, Dock, Anchor, Margin, Padding, font size | Logical pixels; layout must not multiply these by DPI. |
| Control ClientSize, LogicalClientRectangle, LogicalPaddedClientRectangle | Logical client area, excluding borders; the padded rectangle also removes presentation padding. |
| Control DisplayRectangle | Logical layout/scroll extent; may exceed the visible viewport. |
| Control ClientRectangle, PaddedClientRectangle, ScaledBounds | Device pixels for the existing painter API. |
| PaintEventArgs canvas, control MouseEventArgs.Location, Control.PointToScreen input | Device pixels relative to the control; PointToScreen returns physical screen pixels. |
| WindowBase.PointToClient / PointToScreen input | Physical screen / logical native client, respectively; integer public APIs round or truncate at their boundary. |
| WindowKit client/frame size, invalidation, pointer input | Logical; Windows divides physical coordinates once. |
| HWND rectangles, WM_DPICHANGED suggested rectangle, screen/work area | Physical pixels; the suggested rectangle is applied directly. |
| Native framebuffer, row stride, control SKBitmap | Physical pixels. A 5120x2880 BGRA buffer uses 20,480 bytes per row and 58,982,400 backing bytes. |

The raster canvas is already device-sized. Renderers scale their logical geometry
once; there is no extra DPI multiplier on the native framebuffer. Borders are
converted in their drawing scope. Text measurement uses the scaled font, while
image sizes, glyphs and padding are converted once by shared text/image layout.

## Custom layout and painting

Use logical client rectangles when placing children. `ClientSize` now follows
logical layout semantics; applications that compensated for its former high-DPI
value should remove that compensation. `ClientRectangle` retains its established
device-space meaning, so existing custom painters keep that contract.

```csharp
content.Layout += (_, _) => {
    var area = content.LogicalPaddedClientRectangle;
    setup.Location = new Point(
        area.Left + Math.Max(0, (area.Width - setup.Width) / 2),
        area.Top + Math.Max(0, (area.Height - setup.Height) / 2));
};
```

`Control.Invalidate(Rectangle)` accepts a control-local **device** rectangle.
`WindowBase.Invalidate(Rectangle)` accepts a **logical native client** rectangle.
The parameterless overloads invalidate the complete corresponding surface. Make
layout, painting and invalidation calls on the UI thread. An override that changes
its logical client geometry should also override `LogicalClientRectangle`.

## Monitor transitions and popups

The Windows bootstrap requests Per-Monitor V2 awareness before creating its HWNDs,
with older OS fallbacks. A manifest or embedding host that already established
process awareness retains ownership. The framework does not force a separate
thread-awareness policy. Initial scale comes from GetDpiForWindow where supported.

After applying the physical WM_DPICHANGED suggested rectangle, the backend
notifies the framework. Cached bitmaps and preferred sizes are discarded, text
editors refresh their device geometry, and layout observes the new size/scale.
Custom controls with device caches can override `OnDpiChanged(EventArgs)`, discard
those caches and call base. Logical bounds and font sizes remain unchanged.

Popup placement preserves fractional logical coordinates internally until the
positioner returns to physical screen coordinates, avoiding cumulative truncation
at fractional scales. The existing positioner constrains against physical work
areas. Managed window border offsets are included in control screen conversion
and removed before routing input into the content tree.

## Damage and performance

A local invalidation accumulates a bounded union through batching. Transformed
bounds are mapped conservatively; move, hide and layout changes erase old pixels.
Invisible or clipped children retain dirty state until paintable. Composition
culls against damage before allocating child buffers. Changed controls repaint
their own cached image; unchanged ancestors can recompose the damaged area.
Overlapping siblings preserve paint order. Transform changes may use a larger
refresh to preserve correctness.

SkiaSharp 3 converts a mutable bitmap to an immutable image when DrawBitmap is
called, before destination clipping. The ordinary composition path therefore
extracts a bounded bitmap subset before that safe copy. It does not lend mutable
pixel storage to an image with an uncontrolled native lifetime.

Windows presents damage through the BeginPaint DC and a bounded GDI transfer.
The source pointer starts at the first damaged row; a local top-down bitmap header
describes that row band while preserving the full framebuffer stride. Tests cover
both DIB-section and device-dependent destinations, including damage ending at the
bottom edge and nonzero source offsets. Both paths must preserve pixels outside damage.
The diagnostic HUD/region visualizer intentionally requests
full redraws; recording alone preserves partial presentation. Surface allocation
counters cover explicit framework backbuffers, not every temporary internal Skia
image allocation. Measurements are local elapsed work, not display-refresh/FPS
promises. See [the audit and measurements](development/issue-120-high-dpi.md).

## Reproduction

ControlGallery's **High DPI** page contains nested/docked panels, an icon/text
button, centered setup card, AutoSize caption container, anchored button with a layout transition,
scrolling and a ComboBox popup. Move it between monitors, resize/maximize and return.
The existing Windows UiAutomationHost also provides:

```powershell
dotnet run --project ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost -c Release -- --high-dpi
# Explicit local acceptance for the connected FullHD/100% + 5K/225% displays:
dotnet run --project ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost -c Release -- --high-dpi-physical artifacts/dpi-physical
```

The first mode injects DPI messages on an owned HWND for all seven scales and runs
in CI. The physical mode uses actual monitor moves and OS DPI notifications, saves
pixel evidence and checks the entire displayed client against backing pixels. It does
not alter display settings. Pointer messages in both modes are automated inputs;
manual hardware-pointer interaction is not implied by those results.
