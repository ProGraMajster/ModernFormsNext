# Designer smart guides and grid

The Designer's existing pointer controller supplies temporary alignment guides while moving
and resizing manually positioned controls. It uses the production preview layout, the same
coordinate mapper as selection, and the existing gesture transaction. Guides are presentation
data; they are never serialized into `.mfdesign` or generated C#.

A nested transaction-aware edit from a gesture callback cancels and rolls back the current
gesture before another pointer update. Reparenting cannot reuse the old parent's coordinates.
Extensions should use the session's typed mutation helpers rather than mutate the model directly.
The surface participates in the framework's existing keyboard-focus scope. Clicking or tabbing
to it allows Escape to cancel the current pointer transaction before mouse-up.

## Configuration

Open **Designer settings** to enable smart snapping, toggle the visible grid, enable grid
snapping or change its spacing. These are host/session preferences, independent of authored
layout and the preview resource policy. Applying settings creates no dirty document or undo unit.
Hosts may retain their `ModernFormsDesignerOptions`; no second settings store is introduced.

```csharp
var shell = new ModernFormsDesignerShell(new ModernFormsDesignerOptions
{
    SnappingOptions = new DesignerSnappingOptions
    {
        Enabled = true,
        ShowGrid = true,
        SnapToGrid = true,
        GridSize = 12
    }
});

// Replace running preferences on the session's UI thread.
shell.Session.SnappingOptions = new DesignerSnappingOptions { Enabled = false };
```

Guides are enabled by default. The visible grid defaults to eight logical document pixels;
grid snapping defaults to off. `GridSize` accepts 1 through 4096. Visibility is independent of
snapping: a hidden grid may still snap, while a visible grid may be purely a reference.
Disabling `Enabled` bypasses both guide and grid snapping. Explicit arrangement commands and
programmatic coordinate edits retain their exact requested values.

Hold **Alt** during a pointer gesture to bypass snapping temporarily. AltGraph keeps its text/input
meaning and does not act as this shortcut. Escape, focus/capture loss and replacement selection
continue to cancel the existing gesture and roll its authored changes back.

## Alignment and stability

Eligible sibling edges and centers, the common container's edges, its padded client edges and
adjacent Margin clearances become targets. A multi-selection uses one outer rectangle and one
common offset. Selected descendants move with their ancestor once. For a group from different
parents, the common ancestor supplies the target scope; its other immediate children are eligible.
The group's own ancestors never guide against the controls being moved.

The closest valid target wins. Exact ties prefer baselines, padded placement, edges, centers,
Margin clearances and then equal spacing, followed by stable outline/anchor order. Acquisition
uses six logical **surface** pixels; a retained target releases beyond ten. Calculations always
start from the raw pointer delta, which prevents accumulated rounding and jitter between nearby
targets. The canonical device-to-surface conversion applies monitor DPI once; preview zoom changes
the document-space reach of that same surface tolerance.

Moving guides can show equal spacing between two neighbors or repeat an adjacent gap before/after
them. These values measure the additional logical distance between Margin boxes. The displayed
segments identify those clearances. An odd leftover document pixel belongs to the trailing gap;
the two displayed values remain honest when integer coordinates cannot represent exact halves.
Neighbor work is ordered and bounded by the existing model budget rather than all sibling pairs.

Resizing snaps active edges or the changing center while retaining the opposite edge. Minimum
and maximum sizes take precedence over a guide. Resize begins at production effective size,
including existing constraints, while cancellation/undo retain the original authored values.
Coordinate and size arithmetic is checked before any member changes. A guide cannot force a
partial group mutation or an overflowing rectangle.
Boundary strokes are inset for rasterization so they remain visible inside the client clip;
this does not change the authored target coordinate.

Nearby smart guides take precedence over grid rounding. Otherwise grid snapping rounds the moving
outer leading edge, or the active resize edge, to the document grid; midpoint ties round away from
zero. Its origin is the design root's client origin, shared by nested controls. The root grid uses
the same rounded coordinate boundaries as controls. Dense displays draw a coarser multiple while
retaining the configured snap spacing. Painting is bounded to 65,536 points and 131,072 loop visits,
independently of authored dimensions, spacing and zoom. Container fills may cover root-grid dots;
guides remain available in eligible nested containers.

## Applicability and lifetime

Hidden controls, inactive tab contents, clipped-away anchors, embedded project component interiors
and lazy diagnostic previews are not targets. A group containing invisible roots retains its
ordinary movement but does not invent a visible group guide rectangle. Flow/table/tab/page-managed
placement retains its existing owner; it does not acquire manual-position guides. Existing hard
preview resource limits remain in force even when ordinary trusted-project limits are disabled.
No runtime `RowCount` or `ColumnCount` limit is added.

Baseline guides apply to moving exact built-in single-line `Label` and `Button` previews in runtime
render mode. Their inert property projection uses the production text/image layout and shaping,
including font, alignment, Padding, typography and AutoEllipsis, in the preview's logical 96-DPI
space. Results are cached for the gesture. Custom controls/renderers, multiline/line-break text,
images, whole structured Style values and text longer than 4096 characters do not receive an
invented baseline. Resize still has edge/center guides. Project constructors or delegates are not
loaded to obtain metadata. Integer authored positions may leave at most half a logical pixel
between compatible shaped baselines.

Preferences, model or theme metric changes retire stale guide targets for the rest of the gesture;
the next gesture captures fresh bounds. Disabling snapping and Alt take effect immediately. Targets
and overlays are released with the existing pointer transaction on completion or cancellation.

See [multi-selection](designer-multi-selection.md), [transactions](designer-transactions-and-undo.md),
[preview resource policy](designer-preview-resources.md) and [architecture](designer-architecture.md).
