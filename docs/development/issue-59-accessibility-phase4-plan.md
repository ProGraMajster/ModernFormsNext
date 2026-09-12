# Issue #59 — Phase 4 accessibility audit and implementation plan

Audit date: 2026-09-11. Start from verified merged master
`f9e363fb8ed270c9db60a6e2e48f37d5a6b08f11` (PR #118), whose tree equals reviewed PR
head `479674524142b9cb4811293c3362ce23202d8007`. The tracked tree/index is clean;
the unrelated untracked `.codex/config.toml` remains untouched. This plan is
committed before Phase 4 production changes.

## Audit and existing foundation

The full current #59 body, all three comments, complete timeline, formal dependency
endpoints, PRs #102/#103/#104 and relevant source/history were read. The master
refresh found no issue/comment delta and no formal blocked-by entries. Related
#55/#57/#58/#61/#62/#63/#69/#72/#87/#97/#109 were refreshed. Source, test projects,
platform adapters, Designer metadata, samples, documentation, limitations, roadmap
and historical changelog were inspected. Four bounded preaudits cover the full
acceptance matrix, existing controls, viewport scrolling and text providers.

Phases 1–3 are implemented: canonical `AccessibleObject`, logical children, stable
runtime identity, normalized type/state/actions, privacy, Windows MSAA/UIA and the
Android virtual provider. Their historical native evidence remains attached to its
original source. Later #97 supplies bounded immutable `AutomationSession` snapshots,
canonical actions and the authenticated Windows bridge. #62/#63/#109 add the actual
editor, lifecycle, focus and keyboard seams this work must use.

The following gaps are current implementation work, not dependencies invented from
the still-open parent issues:

| Area | Existing behavior and concrete gap |
|---|---|
| LinkLabel | Real links/activation/layout exist; canonical output is generic text without independently invocable links. |
| NumericUpDown | Decimal value and real editor/spinner buttons exist; normalized range/composite semantics are missing. ReadOnly applies to its editor, not its buttons. |
| ScrollBar | Inclusive range and value operations exist; canonical range actions and orientation are absent. Control-only accessibility view already works. |
| ScrollableControl | Owns actual viewport/offsets, but Scroll has no request/metadata contract. Extra scrollbar allowance, mixed DPI units and callback ordering need regression checks before exposing percentages. |
| DataGridView | Real rows/columns/cells/editing exist; logical peers, table coordinates, header associations and grid patterns are missing. Edit cleanup currently trusts indices across callbacks. |
| DateTimePicker/calendar | Real parser, stepping, checkbox and popup/calendar exist; composite/date-cell semantics are missing. Unchecked keyboard toggle and popup callback lifetime need correction. |
| TextBox family | Existing document/layout/selection/composition can support text ranges; the focused IME proxy is not an accessibility lifetime. Persistent ranges, exact range geometry and committed text/selection notifications are missing. |
| Preferences | Reduced motion is already integrated. Existing platform color settings contain contrast classification but lack a complete truthful native/consumer path; Android font scale is currently diagnostic, not typography consumption. |
| Diagnostics/Designer | Canonical snapshots and simple accessibility metadata already exist. Bounded usability diagnostics and targeted Designer round-trip coverage are missing. |

## Architecture, compatibility and ownership decisions

Continue the one `AccessibleObject` hierarchy and normal control operations. Add
optional metadata/capabilities to that model and project them through the existing
internal WindowKit adapter. Preserve the public `IPlatformAccessibleObject` contract,
all existing enum values, action meanings, constructors and public/protected members.
Append normalized types where needed; do not expose native IDs, COM interfaces,
Android Bundles or platform objects from Core. Use focused partials and current
generation inputs; never edit generated COM/MicroCom output.

All live peers, range operations, control mutations and native semantic reads remain
UI-thread-affine. Native wrappers retain weak owners and validate membership in their
captured root after callbacks. A nested descendant in a detached/reparented container
must not remain reachable through its old HWND merely because its immediate Parent
is non-null. Geometry and metadata getters do not activate controls or create popups.
Malformed multi-axis/range/table requests are rejected before mutation. Valid
callbacks may change state; cleanup must complete without retrying an action or
targeting a replacement owner.

Sensitive classification is checked before metadata/text extraction and on retained
range calls. Align the existing known-owner safeguard with both PasswordCharacter
and the #62 Password input scope. Redaction includes lengths, selection, ranges and
style metadata; masks are not a substitute for protecting text length. Do not export
LinkData, Tag, DataSource objects or arbitrary application metadata.

## 4A — existing link, numeric and scrollbar controls

Expose cached weak logical Link peers using the existing Link identity, rendered
fragments and exact activation path. Keep container text separate from each clickable
range. Preserve visited/enabled/focused states, actual per-fragment hit testing and
stable identity after reordering. Removed, zero-length and cross-owner stale peers
must become inert; callbacks must not invoke a second/replacement link. Append a
Hyperlink classification and map it on both platforms.

NumericUpDown becomes an accurate numeric composite: writable decimal-backed owner
range, the actual implicit editor and two logical spinner buttons. The editor retains
ReadOnly/AllowManualEdit policy; those properties do not disable the owner's real
spinner/value route. Use Value/UpButton/DownButton and existing rounding. Handle
decimal extremes with saturating canonical stepping, validate finite double input
before conversion and document double/Android-float projection precision. Names
must identify the field rather than use its changing numeric value.

ScrollBar uses existing RangeValue/SetValue/Increment/Decrement and inclusive Maximum,
with checked integral inputs and safe arithmetic at integer extremes. Add a neutral
optional orientation where needed and preserve the already-correct Control view.
ValueChanged and Scroll keep their existing distinct contracts. ScrollPattern belongs
to the owning viewport, not the scrollbar.

## 4B — real viewport Scroll and reveal operations

Add optional immutable `AccessibleScrollInfo` to AccessibleObject, with finite axis
offset/range/viewport/step metadata and a viewport rectangle in the peer's coordinate
space. Reuse `AccessibleActions.Scroll` with a closed `AccessibleScrollRequest`:
relative small/large amounts, nullable absolute percentages and finite page fractions.
Null axis means unchanged; native sentinels are converted only at backend boundaries.
No new scroll state or layout engine is introduced.

Derive metrics from actual logical viewport/content geometry, not the clamped
ScrollBar.LargeChange getter. First demonstrate and correct the single-axis extra
bar allowance, DPI unit mixing and scroll callback ordering. Keep Dock/Anchor,
padding, layout suspension, touch/wheel behavior and inclusive endpoints intact.
Compute/validate requested coordinates before applying them, then recheck lifetime
across callbacks. A legal endpoint no-op is accepted without a false movement event.

Expose ScrollIntoView for real ordinary descendants through bounded ancestor reveal,
preserving selection/focus, and retain specialized item paths. Define clipping and
Offscreen separately from Invisible; preserve discovery of offscreen content. Extend
other current scrollable composites using their own actual offsets and reveal paths.

Add generated Windows IScrollProvider, exact native amount/sentinel translation and
six scroll properties. Android uses canonical directional/forward/backward actions,
API-gated granular amounts and numeric ViewScrolled events. Preserve numeric range
actions independently. Add a neutral committed scroll notification to existing
transport; filter internal notifications from legacy raw MSAA event IDs.

AutomationSession gets the typed scroll payload and optional detached metadata, using
existing capability, privacy, bounds, cancellation and reachability checks. Protocol
v1 remains compatible: omit absent new request fields, accept absent optional snapshot
fields, preserve numeric enum serialization and fail unsupported new operations
closed. No automatic retry follows an unknown mutation outcome.

## 4C — current grid/table coverage and edit safety

Use cached peers for actual row/cell/header identities with live membership checks,
current coordinates after sorting and bounded lazy enumeration. Define owner and
duplicate/occurrence policy instead of identifying a cell only by its index. Add
optional neutral grid/table/cell metadata (counts, lookup, coordinates/spans and
headers), Windows Grid/GridItem/Table/TableItem and Android collection/item mapping.
Share these contracts with the calendar rather than creating another table model.

Selection commits row/column identity together through the existing grid state and
events. Cell value actions use the normal editor/conversion/commit path. Before
exposing that path, capture editor/row/cell identity, retire old edit ownership before
cleanup callbacks, and revalidate after Begin/End/Cancel handlers, sorting, removal,
conversion or reentry. Do not write a different row because its old index was reused.
Preserve ReadOnly, bound conversion failure, selection mode and exactly-once events.

This covers the current managed row/cell model without realizing visual containers.
Future reusable recycling and large-data acceptance still belong to #55.

## 4D — date entry and the existing calendar

Reuse the existing Text/Value culture parser, range validation, checkbox, StepValue,
ApplyDropDownValue and popup ownership. Expose a meaningful composite label and real
value, toggle, step and conditional expansion capabilities. An unchecked date field
must still expose its enabling checkbox; normal keyboard Space must reach that same
toggle path. Android windowless value/checkbox/step behavior is usable without
advertising an unsupported native popup.

Expose real calendar navigation/title/today/day/month/year peers with date identity,
existing rectangles, grid/header metadata and selection. Harden date boundary
arithmetic and popup opening/closing against close/dispose/replacement/throwing
callbacks. Retire captured popup ownership before native cleanup and never reopen or
activate through an obsolete callback. Validate Windows/TestHost popup focus/lifetime;
general Android Form/window/popup hosting remains #72.

## 4E — practical Text/TextRange over existing editors

Add an optional canonical `AccessibleTextProvider`/`AccessibleTextRange` capability
for TextBox, RichTextBox and Markdown's actual source editor. It reads the existing
document and virtual styled layout, independent of focus-bound native IME sessions.
Read-only/unfocused text remains readable and selectable where permitted; range
navigation itself does not change focus, selection, composition or the document.

Ranges retain weak identity plus UTF-16 endpoints, bias, document generation and
content revision. Use a lazy, fixed-capacity **1024-entry metadata-only edit journal**
on the existing document; do not retain text snapshots or update an unbounded anchor
registry on every edit. Record all insert/remove/replace/reset/composition rollback
mutations. Keep content revision separate from the existing broader Revision.
Rebase retained ranges deterministically: a nondegenerate start uses left insertion
affinity and its end uses right affinity; a degenerate range uses one right-affinity
anchor for both endpoints. Deletion/replacement clamps affected interiors to the
corresponding replacement boundary. Degenerate anchors stay degenerate.
History that has aged out produces an explicit stale-range result requiring a fresh
range, never guessed offsets. Whole-text replacement may compute one allocation-free
prefix/suffix delta. No second document, history or editor is created.

Implement all baseline TextProvider/TextRange operations: document/selection/visible
ranges, point lookup, clone/compare/endpoints, unit movement/expansion, text/search,
truthful supported attribute/search behavior, selection and scroll. Character units
use actual caret/grapheme boundaries; words/lines use pinned RichTextKit 0.4.167;
paragraphs use real line breaks and unsupported page units promote to Document.
Handle single-selection Add/Remove according to a documented contiguous policy;
reject disjoint requests. Placeholder and trailing-newline layout markers never
become document text. GetText(-1) honors the full requested range rather than silently
using IME/IPC snapshot limits. Attribute support and native mixed/unsupported sentinel
behavior must be explicit; do not claim embedded objects that these editors lack.

Derive visible rectangles from current shaped lines/runs, including bidirectional
fragments, scroll, padding and scale. Range scrolling uses existing DoScroll/viewport
reveal without temporary caret changes. Reuse the normal derived selection path;
an external accessibility selection finishes visible preedit using the existing
policy, while reads do not finish composition. Invalidate plain/rich cached layout
on effective theme metrics/colors so current caret/range geometry follows typography.

Add committed metadata-only text/selection/format/geometry notifications, including
same-content replacement, after final document/rich-run/selection state. Preserve
legacy Value events and existing composition hooks. Implement generated native COM
text interfaces, correct range SAFEARRAY/BSTR/VARIANT lifetime, existing dispatch and
HRESULT containment. Android selection and movement-granularity actions use the same
provider and emit text-selection events, distinct from item selection. Native text
ranges do not create an unrestricted bulk-text IPC operation in Automation.Windows.

## 4F — executable preference consumption with opt-in policy

Reuse existing `PlatformColorValues`, `ColorContrastPreference`, `IPlatformSettings`
and reduced-motion policy. Add an optional accessibility-settings capability to the
same platform-settings implementation, with immutable nullable detected color/text
scale data and scoped change notifications. Null means detection unavailable, not a
verified normal preference. Existing third-party providers need not implement it.
Use the existing Windows settings/bootstrap and Android backend/windowless lifetime;
do not create a second registry, contrast enum or global follow-system latch.

Keep ThemeManager.Apply behavior compatible. Add explicit `ThemeApplyOptions.TextScale`
with default 1; apply it once after authored theme inheritance merges and before
final validation/immutable snapshot/atomic commit. Validate finite-positive scale,
finite products and safe integer/renderer representation. Retain authored unscaled
definition/options so reapplication never compounds the scale. Explicit Control.Font
and explicit rich-run fonts remain authored; inherited theme typography scales.
Missing authored typography tokens keep their existing legacy behavior; never multiply
the currently installed global font on each Apply. The preference sample supplies
explicit Body/Caption/Heading typography so its scaling is complete and repeatable.

The consumer explicitly selects its authored normal or high-contrast ThemeDefinition
from detected preference and uses existing Apply. Provide a real documented/sample
path with scoped UI-dispatched subscriptions and coherent high-contrast focus,
selection and disabled colors. Prefer immediate application for accessibility changes;
reuse the current reduced-motion scheduler. Android contrast classification and font
scale use actual supported native signals, with unsupported API levels declared.
Do not equate density changes or retained Activity state with larger rendered text.
This is preference readiness and a working consumer path, not a universal restyling
of every application or unsupported reduced-transparency detection.

## 4G — bounded diagnostics and Designer integration

Build a read-only diagnostic consumer over existing AutomationSession snapshots,
not another traversal/tree. Use stable diagnostic codes, bounded output and canonical
handles for missing interactive labels and provable semantic inconsistencies. Never
invoke actions to test them. Redacted, getter-failed or truncated metadata must not
be reported as a definite missing-name defect. Diagnostics contain no text values,
selections, links' application data or sensitive metadata.

Add focused Designer metadata/serialization/preview round-trip regressions for the
existing simple accessibility properties and additive normalized enums. Preserve
runtime-only complex peers/providers, schema and generated application structure.
Full inspector/picker UI remains #61 and will consume this diagnostic output.

## 4H — integration, documentation and validation

Extend ControlGallery and the existing cross-platform accessibility sample with
representative controls and operations. Keep DemoApp/template content clean; use it
only for startup verification. Update XML docs, semantic/custom-control examples,
Windows/Android pattern matrices, commands/IME/preference integration notes, known
limitations, roadmap and the durable session report. Remove stale claims about
already-implemented Android, snapshots, IME/lifecycle and the wrong virtualization
issue number. Do not change releases, tags, dependencies, package IDs or versions.

Use existing test projects and actual production TestHost/control/adapter paths.
Each slice needs normal, boundary, invalid-input, callback mutation/throw/reentry,
detach/reparent/dispose, privacy and meaningful rendering/geometry regressions.
Test 100/150/200% logical/native coordinate cases where relevant. Serial full Debug
and Release solution builds/tests use `-m:1 /p:UseSharedCompilation=false`; include
Windows targeting and native Android TFMs. Compare public APIs against exact merged
baseline f9e363f, validate all packages and a fresh isolated consumer, and run
documentation scripts, DocFX and all four archive validators at committed heads.

Refresh actual owned-HWND/out-of-process UIA and available API 34/36 emulator/TalkBack
observations at the tested source/APK. Separate managed, offscreen, injected native,
screen-reader and physical evidence. Unavailable checks must say
**NOT EXECUTED — environment unavailable**. Old phase results are not current results.

Revisit all seven #59 acceptance directions before finishing: useful common-control
semantics, focus reflection, real actions/patterns, custom logical extensibility,
neutral API, sensible defaults and preservation of renderer/control hierarchy. Finish
the currently implementable scopes above before advancing to #58. Actual future
recycled containers (#55), full Developer Tools UI (#61), general Android windows
(#72), physical reliability (#69) and separate rich embedded-document adapters remain
explicit boundaries. They do not justify skipping the current controls/providers.

## Native specifications consulted

- [Microsoft Scroll provider contract](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingscroll)
- [Microsoft Text/TextRange contract](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingtextandtextrange)
- [Microsoft DataGrid requirements](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportdatagridcontroltype)
- [Microsoft Calendar requirements](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportcalendarcontroltype)
- [Android accessibility nodes/actions](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo)
- [Android contrast signal](https://developer.android.com/reference/android/app/UiModeManager#getContrast())
