# Accessible editor text

`TextBox`, `RichTextBox`, and the actual source editor inside `MarkdownEditor`
expose `AccessibilityObject.TextProvider`. This optional capability reads the same
document and shaped layout that the control edits and paints. It does not create
another document, native editor, semantic tree, or focus manager.

All calls belong on the control's UI thread. Reading text, navigating a range,
searching, or measuring geometry does not focus the editor, change selection, or
finish an IME composition. Read-only documents remain readable and selectable.
`Select`, `SetSelection`, and compatible add/remove selection operations finish
visible preedit using the existing editor policy before changing the real selection.
Disabled or hidden editors reject selection and scroll mutations.
If a composition-finished observer protects an ancestor, the visible preedit is
accepted but the subsequent accessibility selection is rejected.

```csharp
var editor = new RichTextBox { Text = "Review the selected section." };
var text = editor.AccessibilityObject.TextProvider;
var section = text?.DocumentRange.FindText("selected section");

// Navigation changes the independent range, not the editor's current selection.
var words = section?.Clone();
words?.ExpandToEnclosingUnit(AccessibleTextUnit.Word);

// These explicitly request existing editor operations on the UI thread.
section?.Select();
section?.ScrollIntoView(alignToTop: false);
```

## Ranges and updates

Offsets are UTF-16 document positions. `RangeFromOffsets` validates bounds and
normalizes scalar boundaries. `SetSelection(anchor, caret)` preserves reverse
selection direction; a range itself always has an ordered Start and End.
`GetText(-1)` returns the entire requested range, including text beyond the limits
of the separate IME snapshot and automation IPC APIs. Placeholder text and the
renderer-only trailing-newline caret marker are excluded.

A retained range holds weak document identity and endpoints, without copying the
document. The document allocates its fixed 1024-entry edit journal only when text
accessibility is first requested. Entries contain offsets and counts, never text.
Ranges rebase lazily across those edits. Nonempty starts use left insertion affinity,
ends use right affinity, and collapsed ranges use one right-affinity anchor. A range
older than retained history throws `InvalidOperationException`; request a fresh
range instead of guessing its positions. Disposal invalidates retained ranges.

Character movement follows actual caret/grapheme boundaries. Word and rendered-line
movement use the existing RichTextKit layout. Paragraphs follow document line
breaks; unpaginated Page movement uses Document units. The single selection supports
contiguous add/remove operations and rejects requests that would create disjoint
selections. Navigation and scrolling do not temporarily move the caret to measure
or reveal text.

The provider supports font family, logical font size, weight, italic, underline,
strikethrough, ARGB foreground/background, read-only and hidden attributes. A range
with differing values returns `AccessibleTextAttributeValues.Mixed`; unsupported
attributes return `NotSupported`. Geometry uses the actual styled runs, current
scroll, padding, scale and presentation transform, with visible rectangles clipped
to the editor and ancestor bounds. Theme changes invalidate both plain and styled
layout caches before geometry is queried again.
Unauthored selection-background and placeholder colors follow the current theme;
explicit editor overrides remain intact. RichTextBox's `SelectionColor` continues
to mean the selected text's foreground formatting.

Following the native TextPattern contract, an insertion-point range has no bounding
rectangles. If no text is visible, `GetVisibleRanges` returns one degenerate range;
its presence does not claim that document characters are visible.

Text, selection and effective formatting/geometry changes publish dedicated
metadata-only accessibility notifications after the editing operation settles.
Same-content replacement still advances the edit journal and emits a text change.
Legacy Value notifications and existing composition notifications remain available.
Canonical notification delivery calls every subscriber from its current snapshot
and completes the platform notification route before propagating observer errors.
A single failure retains its exception identity; multiple failures are aggregated.

## Privacy and native adapters

Either `PasswordCharacter` or `TextInputOptions.Scope = TextInputScope.Password`
makes the known editor sensitive. Its text provider is unavailable. Retained ranges
cannot read text, lengths, selection, formatting or geometry. Leaving sensitive mode
requires fresh ranges; an older range does not become readable again. Explicitly
authored accessible labels remain the application's responsibility.

A protected or sensitive canonical ancestor also prevents fresh capability queries
and all retained range reads while that protection applies. This check runs before
reading custom text-provider getters. Caret queries report activity from the existing
focus and host-activation state; they do not reacquire an inactive IME session.

Windows exposes the baseline TextPattern and TextRange operations, plus TextPattern2
caret queries. Unsupported embedded children and annotation elements are rejected.
UIA attributes use native COLORREF/decoration representations and native mixed or
unsupported sentinel objects. Canonical font sizes are measured in logical layout
pixels; Windows converts them to points (`12` logical pixels is `9` points), and
converts point values back for `FindAttribute`. Native foreground/background attributes use
`COLORREF` RGB values. Their mixed-value detection and search compare that same native
projection across the existing formatted runs; differences in alpha alone do not prevent
a native color match. The canonical provider retains its full ARGB values and comparisons.
COM BSTR and SAFEARRAY outputs transfer ownership to
the caller; ranges retain the existing native fragment lifetime and UI dispatch.

Android publishes text selection and movement granularities for non-sensitive
providers. Set-selection and character/word/line/paragraph/page movement operate on
the same canonical document, including reverse extended selections. Native events
carry selection/count metadata without copying document text into event payloads.
Its established own-password contract keeps the explicitly authored label, help
and automation ID separate from unreadable entered text. Under a sensitive or
protected ancestor, descendant labels, help and IDs are also suppressed because
they may be generated from private content. Privacy is checked again after custom
getters and before a native node is returned.
Action discovery uses the same protection for range and text-selection metadata;
it does not read a protected numeric range to infer adjustment actions. The
existing write-only password SetText action remains available.

This capability does not add unrestricted text or text-range commands to
`ModernFormsNext.Automation.Windows`. Native provider tests and real assistive
technology runs are separate evidence; consult the phase report for executed
platform and screen-reader checks.
