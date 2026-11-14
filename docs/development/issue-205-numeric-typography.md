# NumericUpDown editor typography (#205)

The implicit TextBox follows the NumericUpDown owner's effective typeface, font size,
font flags, line height and letter spacing. Consumers configure the composite control;
they do not need access to its editor.

```csharp
var amount = new NumericUpDown {
    Width = 300, Maximum = 10000, DecimalPlaces = 2,
    ThousandsSeparator = true, Value = 1234.50m
};
Font original = amount.Font;
amount.Font = new Font(original.FamilyName, original.SizeInPoints * 1.75f);
amount.LineHeight = 1.2;
// Restore the original logical text size without altering monitor DPI.
amount.Font = original;
```

Use the UI thread. Font and live metric changes refresh layout, the text document's
shaping cache, accessibility ranges and native caret geometry. The same editor/document
and text-input session remain in place: value, selection and composition are
not reset. Font and accessibility observers may reenter or dispose the owner; a nested
font change is reconciled after its callback.

Only typography is copied into the editor's owned state styles. Its colors, borders,
read-only state and accessibility identity remain its own. The framework's existing
owner style semantics apply: an explicitly configured active style may use different
typography, and the existing `StyleHover` default chain is separate from `Style`.
The editor follows the owner's **current** style in each state. This change does not
rewrite style inheritance across the framework. Passive/inherited style edits use the
normal explicit `PerformLayout()` and `Invalidate()` refresh convention.

Natural height is at least the legacy 28 logical pixels and contains the measured text,
editor borders/padding and vertical breathing room. Normal typography remains a minimum
height preference when hover uses smaller text, so entering a spinner cannot shrink its
target under the pointer. Larger active typography may increase the preference. A changed
explicit Height is respected; AutoSize opts back into natural sizing. MinimumSize,
MaximumSize, vertically stretched Dock/Anchor and intentionally constrained clipping
still apply. Preferred width includes the current edit text and the existing 18-logical-
pixel spinner area; a fixed narrow width retains the TextBox's scrolling behavior.

Logical font size and monitor DPI are independent. Measurement and painting convert
once to device pixels. Natural height accounts for rounding each border edge inward to
the editor's integer logical coordinate grid. Layout also refreshes editor bounds after
DPI changes that do not change the outer logical size. Fonts/typefaces remain borrowed;
no per-paint font allocation or disposal was introduced.

The NumericUpDown page in ControlGallery offers 100/125/150/175/200% text settings for
editable decimal and read-only examples. The default generated application is unchanged.

## Regression evidence

On base `15c73c4b4adf7545542fc6a93ccabec6fe91fbd6`, 30 of the initial 33 valid cases failed.
The first test attempts had an unused namespace import and requested an accessible child
before attaching its owner; those fixture failures are retained separately and are not
counted as product reproductions. The first implementation passed 21/33; pointer tests
exposed shrinking hover geometry. Reserving normal typography height fixed all 33 without
changing their hit coordinates or raster tolerances.

The final focused set adds callback reentry/disposal, vertical anchoring and theme/active-
state refresh: 37 new cases, plus 25 existing current-control accessibility cases, pass.
It covers five font scales crossed with five DPI scales, visible glyph bounds against a
Label reference, spinner pointer actions, decimals/thousands separators, manual editing,
read-only semantics, live composition/selection/accessibility ranges, authored size,
constraints, docking and runtime DPI. One extended fixture compile attempt used an internal
IsDisposed property; the final test observes the public unavailable accessibility state.

Full builds, solution suites, CI and native Gallery results are recorded in the implementing
PR. Headless composition is not physical IME/CJK/TalkBack qualification. Existing historical
allocation/Designer timeout observations in the scope audit remain unresolved.

Full Debug on the implementing source passed **6024/6024**, including all 441 Windows
tests. The first full build exposed an ambiguous `Font` name in the new Gallery example;
qualifying `ModernFormsNext.Font` corrected compilation, and the failed log is retained.
The owned native Gallery window was then driven through all five text sizes with Windows
UI Automation. Original PrintWindow images at 100% and 200% were inspected: both decimal
and read-only editors grew without clipping, with aligned spinner halves. The explanatory
caption was shortened after inspection exposed truncation. The app closed normally.
This is automated native interaction and image inspection with the software renderer,
not human monitor-switching, GPU scanout or physical-device qualification.

The full local Release build passed, but the suite was **6023/6024**, not green:
`PlatformPerformanceTransportTests.DisabledIngressDoesNotAllocateOrRetainAFrame` measured
**1752 B instead of zero**. All 37 new cases and the other 440 Windows tests passed.
An archived September 26 TRX (`artifacts/msbuild-audit/tests/after-Release-1/`) records the
same test failing with **7216 B**. The test and `PlatformPerformanceDiagnostics` have no
changes since their introduction in `50756a1` (#58), and their measured path does not
call NumericUpDown. This proves earlier occurrence, not the allocation origin or absence
of an indirect timing effect. No test rerun, extra warmup, tolerance or skip was used.
The type/stack of the allocation remains unknown alongside the audit's earlier observations;
this is a different test from the profiler case owned by #168.
