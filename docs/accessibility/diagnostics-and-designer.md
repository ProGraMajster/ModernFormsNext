# Snapshot diagnostics and Designer metadata

`ModernFormsNext.Automation.AccessibilityDiagnostics` analyzes a capture already
produced by an explicitly enabled `AutomationSession`. It does not register roots,
walk controls, read live semantic getters, invoke actions, focus controls or start
a listener. The input and result are detached, so analysis may run on any thread.

```csharp
var capture = await session.FindAllAsync(root.RootId, new AutomationQuery());
var report = AccessibilityDiagnostics.Analyze(capture, maximumDiagnostics: 256);

foreach (var finding in report.Diagnostics)
    Console.WriteLine($"{finding.Code}: {finding.RootId}/{finding.Handle.RuntimeId}");

// Incomplete coverage is not a successful accessibility audit.
if (report.CoverageIncomplete)
    Console.WriteLine("Some metadata could not be checked.");
```

The output contains stable diagnostic codes, canonical session handles, root IDs
and capture IDs. It does not contain names, text values, selections, document text,
link application data or exception messages, and it retains no control or peer.
Handles keep the existing session and lifetime rules; the report does not certify
that an element still exists. A fresh capture is required to observe changes.

The implemented checks are deliberately specific:

| Code | Captured condition |
| --- | --- |
| `MissingInteractiveName` | A known interactive control type has a null, empty or whitespace accessible name. Canonical name fallback has already run during capture. |
| `ContradictoryExpansionState` | Expanded and collapsed are both advertised. |
| `InvalidBounds` | Width or height is negative. Offscreen, zero-sized or clipped bounds alone are not errors. |
| `InvalidRange` | Numeric metadata is invalid, or a read-only range advertises `SetValue`. |
| `InvalidGridCoordinates` | Cell coordinates/spans are invalid or exceed its containing grid when that grid is present in the same capture. An omitted grid is not an invalid association. |

The default output budget is 256 findings; callers may choose 1–4096.
`Truncated` means additional findings exceeded that budget. `CoverageIncomplete`
also records capture faults, capture limits and skipped privacy-protected metadata.
Redacted, truncated or getter-failed nodes never produce definite findings from
missing metadata. A fault without a node identity makes the entire capture
uncertain. These checks do not certify screen-reader usability, contrast,
keyboard behavior, focus order or compliance with an accessibility standard.

The API consumes the existing bounded node-array result; it introduces no
unrestricted document-text command into local IPC. The future full inspector and
picker in #61 can display these results without defining another semantic model.

During capture, payload reads check the live canonical parent chain before and
after each application getter. A getter that protects its owner or an ancestor
causes already-read payload for that node to be discarded; later payload getters
are skipped. Cycles, parent changes during verification and failed privacy getters
fail closed. Each chain is limited to 512 ancestors and the entire operation to
`512 * MaxNodes` ancestor checks. Exhausting this work budget reports truncation
and unknown privacy, rather than returning data classified using stale ancestry.
This remains a bounded UI-thread capture, not an atomic transaction across all
application callbacks or a guarantee that a snapshot stays current after return.

## Designer

The existing property grid exposes `AccessibleName`, `AccessibleDescription`,
`AccessibleAutomationId`, `AccessibleDefaultActionDescription`, `AccessibleRole`,
`AccessibleControlType` and `AccessibilityView` in the Accessibility category.
Their existing null/default values remain unchanged. An authored empty accessible
name stays distinct from null, which requests the canonical fallback.

These simple strings and enums use the existing `.mfdesign` serializer, C#
generator, reverse parser, clipboard transactions and runtime preview property
application. The appended Hyperlink, Spinner, DataGrid, DataItem, Header,
HeaderItem and Calendar values use ordinary enum metadata. Changing the declared
type describes semantics; it does not create missing control behavior or patterns.

`AccessibilityObject` and the complex text provider are runtime-only and hidden
from property browsing and serialization. Documents do not retain peers, text
ranges, native providers or runtime IDs. The default generated application and
document schema are unchanged. This integration adds no inspector UI or automatic
diagnostic actions while editing a document.
