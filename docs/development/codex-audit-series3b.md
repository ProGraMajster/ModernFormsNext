# Autonomous queue initial audit: Series 3, issues 85–87 and related features

Audit date: 2026-09-10. Baseline: `master` / `origin/master`,
`ba396f95adab82564a0681bc922096599ba8c1ca` (baseline synchronization verified by the session
coordinator). This document covers queue positions 29–36: #85, #70, #55, #13, #12, #57, #86, #87.
It is an initial source audit, not implementation or acceptance completion. Re-audit the current
master and GitHub discussion immediately before implementing each issue.

All eight live issue bodies and their complete `comments` collections were retrieved with `gh issue
view` on the audit date. Each has **zero comments** and is **OPEN**. Issue descriptions and source
were compared with tests, usage documentation, architecture/roadmap documents, known limitations,
CHANGELOG, ControlGallery, Designer, and the Windows/Android backend boundaries. Raw local evidence
is under `artifacts/autonomous-audit/issue-<number>.json`; the durable findings below do not require
those temporary files.

No framework code was changed, no tests/builds/manual sessions were run by this audit, and no branch,
commit, PR, issue state, package, or release was changed. Test files cited below are inspected coverage,
not newly executed evidence. All acceptance matrices are **initial** matrices. PARTIAL includes a
missing implementation; BLOCKED identifies a prerequisite that prevents complete acceptance today.

## Cross-issue dependency and provenance check

| Dependency | Live state | Consequence for this group |
| --- | --- | --- |
| #11 Shape foundation | CLOSED | Reuse geometry, cache/invalidation, Skia conversion, safe Designer serialization. |
| #42 Designer/runtime layout parity | CLOSED | Extend the existing parity tests; not a missing prerequisite for #70. Inherited scenarios still belong to #39. |
| #56 commands | CLOSED | `ICommand`, command sources, gestures, routed/async commands exist. Rebuilding a command runtime for #85/#12/#57 is unnecessary. |
| #59 accessibility | OPEN, merged semantic/Windows UIA/Android foundations | An open umbrella does not block additive control semantics. Broader editor/link coverage still has to use its canonical hierarchy/actions. |
| #62 IME/composition | OPEN | Real full-acceptance dependency for #86/#87; SearchBar must observe composition through the eventual shared contract. |
| #63 lifecycle/activation | OPEN | Real navigation/back/deep-link contract dependency for #12; relevant to refresh/async providers and drag cancellation. |
| #64 TestHost | OPEN, Phase 1 merged | Existing host is reusable now; later input/render/clock features should support stress and editor validation. |
| #58 diagnostics | OPEN | Required queue integration for #55 performance counters and measured stress acceptance. It is not a reason to create a second profiler. |
| #55 virtualization | OPEN | Missing shared engine is a real prerequisite for CollectionView in #13 and large documents in #87. |
| #57 transferable data / drag and drop | OPEN | Existing clipboard is partial; shared drag/drop and safe image data contracts are real dependencies for #86/#87 scope. |
| #46 GPU | OPEN | Future renderer integration, **not a blocker for #70**. Explicitly outside this session. |
| #69 Android device matrix | OPEN | Place future observations in its matrix; not proof that device testing has happened. |
| #72 Android host / #79 services umbrella | OPEN | Do not implement these umbrellas to make this group look complete. Describe supported surface/service capabilities explicitly. |

The full live bodies/comments of completed #11, #42, and #56 were also read. The #56 issue body's
older “Current implementation” paragraph predates the completed phases; its final owner comment and
current code establish completion. Open issue state alone must not be used as a dependency detector.

Relevant merged history inspected through the repository PR list, #53 PR body, issue completion
comments, and targeted `git log`:

- [PR #53](https://github.com/ProGraMajster/ModernFormsNext/pull/53), merge `f3166f7`, implementation
  `e46244a`: shipped Shape/geometry, structured Path editors, serialization and focused tests.
- [PR #92](https://github.com/ProGraMajster/ModernFormsNext/pull/92), merge `0fb159f`: production
  Designer/runtime parity infrastructure.
- [PR #93](https://github.com/ProGraMajster/ModernFormsNext/pull/93), merge `6506e21`: TestHost Phase 1.
- [PR #90](https://github.com/ProGraMajster/ModernFormsNext/pull/90), merge `bce1dca`: tracked remaining
  limitations; it did not implement the features described in #70/#85/#86/#87.
- Command PRs #105/#106/#107/#110 culminate in merge `6e3a7cf`; safe Designer command discovery is
  a later #108 boundary, and Android hardware parity remains #109.
- Accessibility PRs #102/#103/#104 culminate in Android merge `b290a13`, preserving shared semantics.
- `44a6a14` added RichTextBox; `b4cede2` added documents/Markdown viewing; `f751075` added hosted
  MarkdownEditor workflows; `e6b711b` fixed editor hit testing; `54b0f29`/PR #100 corrected scrolling.
  These are existing implementations to extend.
- Clipboard history includes `93ad97c`, `cb0572a`, and documentation commit `ea07d7b`; code is older
  infrastructure, not a blank starting point.

Historical PR tests/manual claims belong to their stated commits. None is promoted to current-run
verification here. `docs/roadmap/ModernFormsNext-Framework-Roadmap.md` also contains dated statements
about commands/accessibility that are superseded by current code and completion comments. Its
proposed pages/items architecture remains a proposal, not shipped API.

## #85 — Add a ModernFormsNext-native BindingNavigator

[Issue #85](https://github.com/ProGraMajster/ModernFormsNext/issues/85). Initial status: **PARTIAL**;
native feature missing, reusable binding/command/accessibility foundations present. No actual hard
dependency currently prevents design/implementation.

### Existing, missing, and misleading evidence

- `ModernFormsNext/DataBinding/BindingSource.cs` owns navigation positions, count/current/list
  notifications, edit/add/remove operations, and disposal. Reuse its actual state and error behavior.
- `ModernFormsNext/DataBinding/BindingNavigator.cs` exists but is explicitly excluded by
  `<Compile Remove="DataBinding\BindingNavigator.cs" />` in `ModernFormsNext/ModernFormsNext.csproj`.
  It is a legacy ToolStrip/WinForms Designer implementation. Its filename is **not** evidence of a
  usable native navigator. Do not simply include it in compilation.
- #56 provides canonical Button/menu/toolbar command execution and enablement. Canonical
  accessibility and ThemeManager are already available; #59 being open does not require waiting
  for an entire replacement accessibility system.
- `docs/data-binding.md` and BND-01 in `docs/known-limitations.md` accurately describe the missing
  native control; the data-binding guide's version-specific “1.3.0” wording is dated.
- No dedicated BindingNavigator tests or Gallery panel were found. Existing command/host,
  accessibility, and Designer command tests supply integration patterns, not navigator coverage.

### Initial acceptance matrix

| Issue acceptance criterion | Status | Remaining evidence/work |
| --- | --- | --- |
| Navigate positions and update enabled/visible state deterministically | PARTIAL | BindingSource operations exist; native composed control and state subscriptions absent. |
| Configurable add/delete/save/cancel without persistence assumptions | PARTIAL | Add/edit/cancel source primitives exist; navigator action policy and application commands absent. |
| Keyboard/focus/accessibility and commands | PARTIAL | Shared systems exist; navigator semantics, focus sequence, actions and notification tests absent. |
| Existing rendering/theme controls, DPI/layout | PARTIAL | Must compose native lightweight controls and validate density/resize/theme changes. |
| Designer-safe properties/code generation | PARTIAL | Use supported metadata/component references and safe declarative command definitions from #108; no WinForms serialization hooks. |
| Binding tests, Gallery, XML and usage docs | PARTIAL | New control-specific coverage and example are missing. |

Implementation direction: an additive native composite over BindingSource and existing action
controls. Define whether source and externally supplied commands are borrowed, unsubscribe when the
source changes or the navigator is disposed, requery after current/list changes, and preserve
application error visibility without inventing a persistence layer. UI state changes stay on the UI
thread. Designer serialization must not execute data-source constructors or action delegates.

Validation must cover empty/single/many items, position bounds, editable/read-only source, add/delete/
cancel exceptions, rebinding, source reset, detach/dispose, focus recovery, command unavailable
fallback, multi-window isolation, themes and DPI. Runtime tests and Designer round trips are
required; Windows visual/keyboard and Android surface accessibility observations remain pending.
Manual status: **NOT EXECUTED — environment unavailable** for physical Android, screen reader,
and Visual Studio Experimental Instance in this audit.

## #70 — Extend vector geometry and Path Designer tooling

[Issue #70](https://github.com/ProGraMajster/ModernFormsNext/issues/70). Initial status: **PARTIAL**;
all advanced criteria remain open, but the Shape foundation is fully present. No GPU prerequisite.

### Existing, missing, and outdated interpretations

`ModernFormsNext/Drawing/Geometry/` contains mutable versioned geometry, transform, figures,
line/quadratic/cubic segments and change notification. `Shape.cs`, `Path.cs`,
`Rendering/Skia/SkiaGeometryConverter.cs`, and `SkiaShapeRenderer.cs` own rendering/cache/hit behavior.
Geometry is shared and UI-thread mutable; geometry/cache invalidation and native path disposal must
remain canonical.

`DesignerPathGeometryDialog.cs` is a real structured editor (figures, coordinates, Line/Quadratic/
Cubic segments), and the Designer has compact text syntax, `.mfdesign` serialization, generated C#
and safe reverse parsing. Thus “no path grammar/editor at all” is incorrect. The missing criterion is
a **core** grammar/import surface and **graphical** node/Bezier editor, plus additional primitives.

Arc/group/boolean primitives, reusable generic Control geometry clip and general Stretch API were
not found. Existing shape clipping to the control render buffer is not a generic geometry clip.
`ShapeGeometryTests.cs`, `ShapeDesignerTests.cs`, `samples/ControlGallery/Panels/ShapesPanel.cs`,
`docs/shapes-and-vector-geometry.md`, GEO-01, CHANGELOG 1.10.0, and the roadmap are consistent about
this boundary. PR #53's historical Windows manual checks do not validate new #70 work.

### Initial acceptance matrix

| Issue acceptance criterion | Status | Remaining work |
| --- | --- | --- |
| Arcs: bounds/transforms/serialization/render/hit tests | PARTIAL | Add arc representation and equivalent conversion, finite-input/degenerate tests and safe persistence. |
| Groups and defined boolean operations | PARTIAL | Missing; define fill/transform/ownership, shared-child mutation and cycle rejection before exposing API. |
| Core path grammar and/or bounded SVG import | PARTIAL | Designer-only compact grammar exists; core parser/import bounds and unsupported feature policy missing. |
| Stretch and reusable Control geometry clipping | PARTIAL | Existing DPI conversion/buffer clipping is insufficient; measurement/render/input must agree. |
| Graphical safe Bezier/path editor | PARTIAL | Structured numeric editing exists; graphical handles and atomic transactions missing. |
| Shared Windows/Android geometry, Designer/runtime parity | PARTIAL | Foundation shared; each new primitive/transform/clip needs current parity tests and runtime evidence. |
| Tests/XML/docs/Gallery | PARTIAL | Extend shipped suites, docs, and Shapes panel for advanced features. |

Implementation direction: extend the current geometry representation/converter and Designer value
pipeline. Keep public backend-neutral names and the shipped `ModernFormsNext.Path` namespace. Parser
limits must bound input, segments and nesting; SVG support may deliberately exclude scripts, remote
resources, animation and unsupported paint constructs. Define stretching with zero-area geometry,
stroke extents, negative/degenerate transforms and clipping consistently across paint and hit tests.
Avoid caching native paths outside their established ownership.

Validation: malformed/oversized grammar, arc endpoint/radius edge cases, fill rules and booleans,
recursive/group mutation, cache invalidation/disposal, transformed clipped hit targets, density and
Designer serialization/generation/reverse parity. Rendering and new graphical-editor interaction
must be observed. Physical-device Shape validation: **NOT EXECUTED — environment unavailable**.
GPU #46 is expressly unrelated to completing this CPU/Skia-based extension.

## #55 — Add reusable virtualization and item recycling infrastructure

[Issue #55](https://github.com/ProGraMajster/ModernFormsNext/issues/55). Initial status: **PARTIAL**;
the reusable engine is missing. #58 instrumentation and later #64 facilities are queue prerequisites
for its complete measured/tested acceptance, rather than permission to build private substitutes.

### Current source facts

- `ListView.cs` lays out every item during `OnPaint`, and `ListViewRenderer.cs` loops through all
  items. It explicitly documents incomplete functionality.
- `TreeView.VirtualMode` means nodes resolve on expansion. `GetVisibleItems()` and callers enumerate
  expanded logical items; this is **not** reusable viewport container recycling.
- `DocumentViewerRenderer.cs` culls drawing outside the viewport but enumerates the complete layout;
  `DocumentViewer` obtains a complete layout. `DocumentViewportTests.cs` tests culling only.
- No `ItemsPresenter`, `VirtualizingLayout`, recycler/generator, realization range or shared
  item-source implementation matching the issue's proposed foundation was found. The names are
  conceptual, so code was also checked for equivalent viewport/recycling behavior.
- Existing `ScrollableControl`, layout, capture/focus, accessibility and BindingSource contracts
  should be integrated, not copied. Roadmap Stage 3 expects recycling before CollectionView and
  prohibits an Android-native RecyclerView replacement.

### Initial acceptance matrix

| Issue acceptance direction | Status | Remaining work/evidence |
| --- | --- | --- |
| A supported list control displays 100,000+ logical items without 100,000 controls | PARTIAL | No shared realization engine or current stress acceptance. |
| Visual/container count bounded by viewport/cache | PARTIAL | Define realization range, cache policy, recycler and bounded preview. |
| Fast scrolling avoids uncontrolled allocation/memory growth | BLOCKED | Implement engine and collect bounded stress/allocation evidence with #58. |
| Selection/focus/keyboard survives recycling | PARTIAL | Stable logical item identity, focus removal/recovery and routed navigation required. |
| Insert/remove/move preserves mapping | PARTIAL | Mutation-safe source subscriptions and index/identity mapping required. |
| Programmatic navigation to unrealized items | PARTIAL | Shared scroll-to-item contract and stable estimated/measured extent correction required. |
| Infrastructure reusable by multiple controls | PARTIAL | Shared control-neutral engine and more than a one-off CollectionView implementation required. |

The issue's four phases additionally require vertical/horizontal stack integration, wrap/grid,
variable-sized items, nested scrolling, resize/DPI/font/theme changes, cache tuning, incremental/
paged/async readiness, bounded Designer previews, diagnostics, and Windows/Android validation.
These cannot be silently reduced to a fixed-height happy path.

Implementation direction: establish source/identity and generation/recycling contracts compatible
with existing layout. Bind/unbind containers explicitly, clear local subscriptions/commands/input
state before reuse, never equate selection identity with container identity, and define ownership
for borrowed templates versus discarded containers. Collection mutation during realization must
not corrupt enumeration. Use UI-thread scheduling and bounded realization transactions; asynchronous
sources can remain a later additive implementation if initial contracts preserve that possibility.

Tests must cover 0/1/100,000+ items, repeated long scroll runs, cache counts, allocations after warmup,
insert/remove/move/reset during layout, variable-size corrections, unrealized navigation,
selection/focus/capture, detach/reattach/disposal, wrap layouts, nested hosts and density changes.
No stress, allocation, Windows visual, emulator, or physical-device result is claimed here.
Physical-device validation: **NOT EXECUTED — environment unavailable**.

## #13 — Add collection and utility controls

[Issue #13](https://github.com/ProGraMajster/ModernFormsNext/issues/13). Initial status: **BLOCKED**
for complete scope by missing #55; independent controls can be planned without rebuilding
virtualization. Runtime types in this issue were not found.

Existing `DateTimePicker` already supports date/time formats, culture formatting, stepping, bounds
and a popup. `TextBox`, `ScrollableControl`, command sources, shared animation, input and theme
systems provide reusable foundations. `NavigationPane`/`TabControl` are not new collection controls.
The roadmap's items/control architecture remains proposed; it describes source identity/recycling,
async suggestions, pluggable bounded history, refresh cancellation and time-edit reuse.

This issue has a scope list rather than separate acceptance checkboxes; each scope item is retained:

| Issue scope item | Status | Missing implementation/evidence |
| --- | --- | --- |
| CollectionView: templates, selection, virtualization | BLOCKED | Must consume #55; no alternate engine or one-control recycler. |
| CarouselView | BLOCKED | Reuse collection source/recycler; snap/loop/current-item semantics missing. |
| RefreshView / pull-to-refresh | PARTIAL | Shared scroll gesture arbitration, command/cancel state and indicator missing. |
| SearchBar: suggestions/history/customization | PARTIAL | Search provider, stale-result/cancel policy, bounded history and accessible popup missing; large suggestion realization uses #55. |
| TimePicker | PARTIAL | DateTimePicker time support exists; dedicated control/nullable-time and bounds contract missing. |
| Keyboard/focus/accessibility | PARTIAL | New controls must provide canonical semantics and recycled-item focus behavior. |
| Designer integration | PARTIAL | Safe bounded preview, properties, value/reference generation and round trips missing. |
| Windows/Android support | PARTIAL | Shared surface architecture exists; new gesture/IME/popup behavior needs platform evidence. |
| Tests/docs/ControlGallery | PARTIAL | No new control-specific suites/panels found. |

Implementation direction: stabilize CollectionView source/template/selection contracts first;
compose Carousel from shared realization, Refresh from the existing scroll path, Search from text/
IME plus shared item realization and commands, and extract/reuse DateTimePicker time logic rather
than copying it. Keep query history opt-out/pluggable and I/O off the UI thread. Cancel or reject
stale provider results after new input, composition, detach or disposal. Designer preview must not
run providers or load unbounded sample data.

Validation: mutation and 100k stress, gesture arbitration, repeated/cancelled/failed refresh,
out-of-order suggestions, deterministic debounce, IME composition, privacy/history corruption,
culture/12–24-hour/midnight bounds, keyboard/accessibility, rendering/DPI, and Designer round trips.
Native Android touch/IME and physical-device results: **NOT EXECUTED — environment unavailable**.

## #12 — Add pages, navigation and AppShell

[Issue #12](https://github.com/ProGraMajster/ModernFormsNext/issues/12). Initial status: **BLOCKED**
for complete scope by #63 lifecycle/activation. Command dependency #56 is already satisfied.

`Form`, `WindowBase`, `Control`, `TabControl`, `NavigationPane`, application commands and
`SkiaControlSurface` exist; no shipped Page/ContentPage/NavigationPage/TabbedPage/FlyoutPage/AppShell,
route registry or navigation-stack runtime was found. `docs/architecture/decisions/
ADR-Page-Navigation-Architecture.md` is explicitly **Proposed**, despite its internal “Decision”
section. Do not treat its conceptual API names as existing public contracts.

The ADR favors `Page : Control`, a neutral host within a window/surface, dispatcher-serialized
navigation, separate route parsing/factories and composition over existing layout/input. It rejects
page-per-native-window and a second view-model visual hierarchy. The roadmap preserves static
`Application` compatibility and delays default template integration until runtime stabilization.

### Initial acceptance matrix

| Issue acceptance criterion | Status | Remaining work |
| --- | --- | --- |
| Deterministic/testable page lifecycle and stack transitions | BLOCKED | Shared #63 semantics plus page state/stack/ownership/rollback implementation. |
| Back/deep-link builds on lifecycle/activation | BLOCKED | Neutral activation/back contract and platform adapters; not an independent app model. |
| Shell actions reuse commands | PARTIAL | Commands exist; shell actions and route integration absent. |
| Shared Windows/Android navigation contracts | PARTIAL | Same Control/surface path available; page and host implementation absent. |
| Runtime tests/docs/samples before Designer/templates | PARTIAL | All new runtime evidence absent; maintain this order. |

The full scope also includes ContentPage, stack navigation, tabs/flyout, AppShell composition,
route registration/parameters and shared-scheduler transitions. None may disappear from completion
because the five summary criteria are shorter.

Implementation direction: define source-compatible public page/host contracts following #63,
page ownership, retained versus disposed stack entries, duplicate-page policy, exact transition
events, cancellation/exception rollback and reentrant navigation handling. Preserve normal
resources/binding/layout, return focus on pop, isolate each window/surface, and cancel animations
through the shared scheduler. Route parsing must bound/validate external deep links and avoid
executing arbitrary route strings. Android pause/recreation differs from page disappearance and
must map through lifecycle rather than be copied from Windows close events.

Tests: legal/illegal lifecycle transitions, concurrent navigation, transition cancellation and
throwing factories, back interception, routing ambiguity/encoding/unknown route, multiple hosts,
focus restoration, detach/dispose and snapshot state. Windows form and Android surface examples
precede Designer and template integration. Full Android host #72 is not automatically in scope;
device back/rotation/deep-link observations: **NOT EXECUTED — environment unavailable**.

## #57 — Add cross-platform Clipboard and runtime Drag & Drop infrastructure

[Issue #57](https://github.com/ProGraMajster/ModernFormsNext/issues/57). Initial status: **PARTIAL**;
substantial clipboard contracts exist, but safe portable data policies and runtime drag/drop are
missing. This is not a zero-code clipboard issue.

### Existing architecture and important legacy boundary

- `ModernFormsNext/Clipboard.cs` exposes asynchronous text/clear convenience methods over the
  registered WindowKit `IClipboard`.
- `ModernFormsNext.WindowKit/IClipboard.cs` already includes text/clear, `SetDataObjectAsync`,
  formats and data reads. `IDataObject`, `DataObject`, `DataObjectExtensions`, and `DataFormats`
  already provide stable text/files identifiers and multi-format values. Preserve public
  compatibility and extend these rather than introduce duplicate names/contracts in another layer.
- `WindowsPlatformBootstrap.cs` registers `Avalonia.Win32/ClipboardImpl.cs`. It supports Unicode
  text, retries while unavailable, and OLE data/format access. `WindowsClipboardService.cs` and
  `IPlatformClipboardService.cs` are empty stubs and are **not** the actual clipboard path.
- `Avalonia.Win32/OleDataObject.cs` detects a serialized-object marker and calls
  `BinaryFormatter.Deserialize`; `Avalonia.Win32/DataObject.cs` falls back to arbitrary-object
  `BinaryFormatter.Serialize`. These are source-confirmed legacy policies contrary to this issue's
  no-implicit-CLR-deserialization requirement. This audit does **not** claim the path succeeds on
  the current .NET runtime or demonstrate exploitation. It must be removed/constrained and tested
  as part of safe #57 work, not carried into a new clipboard/image/drag API.
- Windows also has OLE/interop definitions, but no production control-level drag session,
  AllowDrop/DragEnter/DragOver/DragLeave/Drop routing was found. Low-level native types are not
  proof of Explorer-drop support. No Android clipboard registration/adapter was found.
- TextBox and DocumentViewer already copy text; editor tests use fake clipboard implementations.
  This is not a native cross-application acceptance result. Designer clipboard has its own
  detached versioned document payload and must remain separate.

### Initial acceptance matrix

| Issue acceptance direction | Status | Remaining work/evidence |
| --- | --- | --- |
| Text copies between MFN and external apps | PARTIAL | Native Windows code exists; current integration/unavailable-owner/Unicode test and external workflow evidence needed; Android absent. |
| Supported controls copy/cut/paste | PARTIAL | Existing text operations; canonical command integration, portable formats and errors need expansion. |
| Data drags between MFN controls | PARTIAL | Shared drag lifecycle/routing/effects/capture implementation absent. |
| Windows receives Explorer file drops | PARTIAL | Native adapter plus real external file-drop verification absent. |
| Effects/cancellation deterministic | PARTIAL | Must handle cancellation, source/target removal and window lifetime through one session. |
| Nested controls receive routed events | PARTIAL | Production hit testing/clipping/enabled/visibility policy and parent routing absent. |
| External/custom data avoids unsafe deserialization | PARTIAL | Legacy BinaryFormatter fallback must be replaced by explicit bounded formats, bytes/streams or safe adapters. |
| Unsupported capabilities reported clearly | PARTIAL | Android/desktop capability model and documented failure behavior absent. |

Retain full scope beyond summary acceptance: image/URI/files/custom/multiple representations,
lazy payload lifetime, async/blocking behavior, custom drag visual, auto-scroll readiness, native
outbound transfer where feasible, privacy, source/target lifetime, tests/docs/Gallery.

Implementation direction: audit and extend the existing public data/clipboard contract additively,
with explicit payload type/size/format rules and capabilities. Keep Win32/OLE conversion in Windows
and Android services in Android. Ensure borrowed streams, lazy providers and COM/native allocations
have documented lifetime and cancellation. Use the production hit test/input/capture path for
internal drag routing; integrate effect negotiation and auto-scroll with existing scroll behavior.
Command adapters should reuse existing text edit methods where possible. Do not implicitly open
files/URIs or serialize Designer documents as arbitrary runtime objects.

Validation: multi-format payloads, absent/unsupported format, malicious serialized marker, bounded
payloads, clipboard temporarily unavailable, UI affinity, Unicode, delayed provider failure,
cancelled drag, nested target transitions, disposal/removal, capture loss, multiple windows/DPI,
OS inbound/outbound file transfer and platform capability negatives. Physical Android and external
manual interaction: **NOT EXECUTED — environment unavailable** in this audit.

## #86 — Complete the portable RichTextBox compatibility surface

[Issue #86](https://github.com/ProGraMajster/ModernFormsNext/issues/86). Initial status: **BLOCKED**
for complete advanced IME acceptance by #62; shared data transfer #57 and canonical #59 editor
semantics are required integration points. Portable document work can be designed independently.

`RichTextBox.cs` derives from the existing text editor path; `RichTextBoxRtf.cs` and
`RichTextBoxTextRun.cs` preserve formatted ranges. `RichTextBoxRenderer.cs` uses RichTextKit/Skia and
the same styled text origin for paint/hit testing. Public selection offsets remain UTF-16 and the
existing tests cover Unicode/CRLF/formatting/editor behavior. `SelectionBullet`, indentation,
`SelectionTabs`, `SelectionProtected`, `DetectUrls`, and `LanguageOption` exist as compatibility
properties; documentation and code explicitly say several are stored without implemented behavior.
Property presence must not be marked as feature completion.

`docs/richtextbox.md`, TXT-01, `RichTextBoxTests.cs`, and
`samples/ControlGallery/Panels/RichTextBoxPanel.cs` document/test the portable subset. OLE native
embedding is not part of it; no unsafe native RichEdit replacement is appropriate.

### Initial acceptance matrix

| Issue acceptance criterion | Status | Remaining work |
| --- | --- | --- |
| Supported RTF/feature matrix and permanent exclusions incl. OLE/security | PARTIAL | Current subset documented; explicit permanent exclusions and safe object/embed boundary need decision/documentation. |
| Protected ranges and deterministic edits/events | PARTIAL | Stored SelectionProtected does not enforce range protection; shared mutation paths and events need enforcement. |
| URL detection/pointer/keyboard/accessibility | PARTIAL | DetectUrls compatibility property is insufficient; logical links, activation and canonical semantics missing. |
| Bullets/indentation/custom tabs agree in measure/paint | PARTIAL | Compatibility values stored; paragraph model and layout/render behavior missing. |
| Advanced IME/language through #62, neutral API | BLOCKED | Must use shared composition/language infrastructure, preserving UTF-16/code-point conversions. |
| RTF/Unicode/render/input/accessibility/performance tests | PARTIAL | Existing base tests do not cover omitted behaviors; fixture, host/snapshot and large-document evidence needed. |

Implementation direction: extend one document/edit pipeline with paragraph/protected-range/link
state that transforms with edits and undo/redo. Every edit entry point (typing, paste, replace,
delete, IME, programmatic operations under the documented policy) must share enforcement and event
ordering. Link activation must not auto-open arbitrary external destinations. Match measurement,
caret/selection hit testing and paint for tab/indent/bullet metrics; retain borrowed font/image
and cache ownership. Native IME options that cannot be portable must be explicitly excluded rather
than silently claimed. No backend types enter the RichTextBox API.

Validation: representative RTF round trips/unknown destinations, protected overlapping/replaced
ranges, undo/redo and failed edits, URL Unicode boundaries and activation, paragraph edits/tabs,
font/DPI changes, clipboard workflows, large documents and composition matrices. Physical IME,
screen-reader and device results: **NOT EXECUTED — environment unavailable**.

## #87 — Complete Markdown editing interactions and large-document support

[Issue #87](https://github.com/ProGraMajster/ModernFormsNext/issues/87). Initial status: **BLOCKED**
for complete scope by #55/#57/#62; canonical accessibility #59 is an integration foundation, not a
second link tree to design independently.

### Existing interactions and scale limits

`MarkdownViewer`, `DocumentViewer`, the shared document model/layout/text map, and the source-based
`MarkdownEditor` are implemented. `DocumentViewer.OnKeyDown` supports select-all/copy/escape, while
link interaction is pointer hit/press/release based; no individual link keyboard focus or accessible
logical-link collection was found. Existing `LinkLabel`/canonical accessibility are patterns to
reuse, not replacement document models.

MarkdownEditor already supports insertion commands, request events, image assets, undo/source
fidelity and async cancellation. `InsertImageAssetAsync` in `MarkdownEditor.InsertionRequests.cs`
is the documented integration point for future dropped local files/streams. The existing
`MarkdownImageAssetProcessor` bounds copying, validates assets, handles collision/cancellation and
cleans temporary files. New drag/drop should invoke this workflow instead of implementing another
image insertion path.

Document rendering culls elements outside the viewport but still uses full eager layout. The editor
falls back to one normal highlighting run above 200,000 UTF-16 characters; this protects highlighting
cost but is **not virtualization**. `docs/markdown-editor.md` explicitly states eager full-text layout
for caret scrolling and no current WYSIWYG/touch handles/stable drag-drop surface. TXT-02 and the
Markdown compatibility guide agree.

Inspected existing coverage includes `DocumentLinkInteractionTests`, `DocumentSelectionTests`,
`DocumentTextMappingTests`, `DocumentViewportTests`, `MarkdownEditorInternationalInputTests`,
`MarkdownEditorPreviewInteractionTests`, `MarkdownImageAssetWorkflowTests`, lifecycle/history/
commands/scroll-sync/quality tests, and the MarkdownViewer/MarkdownEditor Gallery panels. These
substantial suites do not establish the missing acceptance below.

### Initial acceptance matrix

| Issue acceptance criterion | Status | Remaining work |
| --- | --- | --- |
| Individually focusable/keyboard-activatable links, visible focus and accessibility | PARTIAL | Pointer links exist; shared logical-link focus traversal, rendering and canonical semantic children/actions absent. |
| Documented supported-platform touch selection | PARTIAL | Shared pointer/text selection exists; native touch handle/selection policy and observations missing. |
| Supported dropped images via #57, path/URI/security/failure handling | BLOCKED | Secure asset workflow exists; drag/data adapter absent. |
| Large-document virtualization via #55 preserving selection/scroll/links/IME bounds | BLOCKED | Existing culling/highlight fallback is not incremental/virtualized layout; shared infrastructure and #62 surrounding-text limits needed. |
| Decide WYSIWYG scope; approved subset or permanent non-goal | PARTIAL | Current source-editor-only scope is explicit, but a permanent non-goal/approved later boundary has not been recorded as #87 acceptance. The issue permits either choice; do not invent a full WYSIWYG implementation requirement or silently drop the decision. |
| Keyboard/touch/drag/accessibility/IME/virtualization/source round trips | BLOCKED | Existing base suites need integrated new-feature evidence after dependencies. |

Implementation direction: retain source as the canonical editor content, stable document positions
independent of realized layout, and shared document/paragraph layout rather than a Markdown-only
virtualizer. Individual links need logical identity and focus recovery when unrealized; selection
must span unrealized blocks and avoid loading unbounded surrounding text into IME. Async image
resolution must reject stale insertion snapshots after source/selection/lifetime changes. Dropped
paths/URIs remain untrusted and flow through the same asset validation/collision/undo policy.

Validation: keyboard-only link traversal/activation, focus after reflow and virtualization,
selection spanning unrealized content, source/render round trips, malicious/unsupported/failed image
drops, large documents, scrolling and allocation bounds, Unicode/IME surrounding text, detach/
dispose/cancel and touch accessibility. Physical touch/IME/screen-reader evidence:
**NOT EXECUTED — environment unavailable**.

## Audit result and next recheck

No issue in this group can currently be marked COMPLETE or reduced to validation-only. Existing
subsystems substantially reduce implementation scope, especially #70, #57, #86 and #87; their
presence must not be confused with advanced acceptance completion. #85 and #70 have no whole-issue
external dependency blocker. #55 needs preceding instrumentation/testing integration. #13, #12,
#86 and #87 have real missing shared contracts for full acceptance.

Before each implementation, refresh master/issues/comments, verify the dependencies above against
completed earlier queue work, produce the issue-specific API/lifecycle/threading/disposal plan,
then run appropriate new deterministic tests and existing regressions. Designer changes require
serialization/generation/reverse/parity checks; foundational changes require full solution tests.
Visible changes require ControlGallery evidence. Template validation becomes relevant only after
stable #12 runtime integration. ApiCompat must protect the shipped 1.x contracts. Final acceptance
matrices, actual validation results and per-issue commit/PR provenance must be appended by the
implementing phase; all eight issue states remain unchanged by this audit.
