# Label natural measurement and ellipsis (#272)

`Label.AutoEllipsis` keeps a fitting single line intact and shortens genuine overflow
inside the available text region. The current style, including transient typography,
is measured at the control's DPI. Fractional glyph advances are rounded **up** to a whole
device pixel before the text rectangle is clamped. This rounding fix already existed
in master through #441; this follow-up retains and qualifies it.

The remaining measurement defect was the implicit **1000 × 1000 device-pixel** limit in
`TextMeasurer` and `TextImageLayoutEngine`. It wrapped long captions during natural-size
measurement, even when their final field could fit them. The no-maximum overloads now
measure the natural extent. Explicit maximum-size overloads remain available for wrapping;
`int.MaxValue` means unconstrained in that dimension. Unlimited single-line text does not
align against the numeric sentinel: center/right alignment uses its natural extent.
The existing public `MaxSize` field remains available; implicit measurement does not depend
on mutable global state. Existing finite-height multiline compatibility handling is unchanged.

Image/text layout also reserves the image and renderer gap **before** sizing an ellipsized
caption. The combined rectangle can no longer expand beyond the field just to accommodate
both natural extents. Centered adjacent-region expansion happens before introducing the gap,
preserving its adjacency contract. Label and Button share this layout path. Non-ellipsis
overflow retains its existing sizing policy. Multiline labels still render in the bounded
field; removing the measurement cap does not make their paint area unlimited.

```csharp
var label = new Label {
    Text = "Integration Sample", Width = 1506, Height = 48,
    Font = new Font("Segoe UI", 27, FontStyle.Bold),
    TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
};
form.Controls.Add(label);
```

Change properties on the UI thread. AutoEllipsis invalidates rendering and requests parent
layout when auto-sizing is enabled; it does not rewrite `Text` or its accessible name.
Application text size and monitor DPI are independent inputs. The Font API retains its
existing mapping into integer style metrics; this fix adds no font-unit conversion.
No public members, dependencies, backend interop or template/startup behavior were added.

## Regression evidence

The final **47-case** fixture was run against the unchanged product files from
`c0b91dff381c4b3cf7e20771969c00bbc3b64f2e`: **27 failed / 20 passed**. Exact edited bytes
were restored afterwards. The corrected implementation passes **47/47**. The original
1506 × 48 recipe and fractional rounding already pass on the baseline, so they are retained
regressions, not attributed to the new fix. Cases include:

- Independent unbounded RichTextKit reference widths/heights above 1000 pixels.
- Short, long and Hebrew captions, left/center/right alignment, and raster equality with
  ellipsis disabled when the full caption fits.
- Five DPI scales (100/125/150/175/200%) crossed with three application text sizes
  (100/150/200%), and raster differences for actual overflow.
- Explicit finite width, finite single-line height, bounded multiline painting, padding,
  image allocation, all four non-overlay image relations and Label/Button renderer gaps.
- Five DPI cases each exercising 72 image/text arrangements, including fields that leave
  zero text space. Regions stay nonnegative, inside the field, and do not overlap.

Early retained runs included fixture mistakes: a presumed 15-to-20 font-unit conversion,
an unsupported `ControlStyle.FontFamily` assignment, and a finite-height expectation that
ignored existing multiline compatibility handling. These were corrected in the fixture;
they are not product fixes. Expanded image tests then exposed field expansion and a gap
violating the adjacent-region helper's Debug assertion. Both are fixed in the layout path.
No existing tolerance, warmup, timeout or skip was changed.

## Native gallery qualification

Open **ControlGallery → Label → Open ellipsis examples**. The separate window starts with
the original two 1506 × 48 labels, then a long caption, actual narrow overflow and an image
caption. **Label text** cycles 100/150/200% and adjusts row height; **Align** cycles
left/center/right. Move the window between monitors independently of the text-size selector.
The full matrix should keep fitting captions intact and display an ellipsis for overflow.
Source images are borrowed from Gallery's existing image cache. The window disposes itself
after closing. DemoApp remains the generated-app reference and is unchanged.

Full solution and owned native-window results are recorded with the PR qualification.
Raster/native automation is distinct from a human physical-monitor review, which is not
claimed by these tests. No ModernTubeDownloader source or pinned dependencies were changed.
