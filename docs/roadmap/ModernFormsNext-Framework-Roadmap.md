# ModernFormsNext framework roadmap

- Baseline audited: 2026-08-18 against ModernFormsNext 1.10.0
- Paint/gradient foundation implemented: 2026-07-19
- ThemeManager, composable animations, and platform animation polish implemented for 1.9.0
- SDK baseline: .NET 10 (`10.0.201`)
- Runtime priority: Windows first; Android is an experimental shared-control Skia vertical slice
- Purpose: architecture and delivery sequence, not a promise that every listed API is implemented

## Executive summary

ModernFormsNext already has more reusable foundation than the feature list initially suggests. The
main package contains a WinForms-like `Control` tree, mature dock/anchor/flow/table layout,
`ControlStyle`, solid and gradient brushes, renderer classes, Skia invalidation/back buffers, input
routing, a shared UI animation scheduler, `NavigationPane`, `TabControl`, `DateTimePicker`, a
document model, Markdown parser/editor, and a substantial `DocumentViewer`. Windows has the complete
windowing backend. Android has lifecycle, dispatcher, permission infrastructure, and one real shared
control tree rendered by `AndroidSkiaHostView`, but not general `Application.Run(Form)`, multi-window,
or the complete WindowKit service set.

The architecture should therefore be extended, not replaced. The implementation order is:

1. dynamic resources (first vertical slice implemented with this roadmap);
2. paint/brush contracts (implemented foundation);
3. UI animation scheduler, composition, and platform polish (implemented foundation);
4. ThemeManager (implemented foundation), then localization;
5. page lifecycle, navigation, routing, tabs, flyout, and shell;
6. virtualized data controls and SearchBar;
7. shapes and path geometry (implemented 1.10.0 foundation);
8. modular document providers and viewers;
9. charts, then diagrams.

The most important change from the suggested order is document work: provider/viewport contracts and
compatibility planning for the existing `DocumentViewer` must precede a PDF engine. Implementing
`PDFViewer` first would hard-code a second document architecture. Paint/gradient work is also an
enhancement phase rather than a greenfield implementation because brush types and Skia rendering
already exist.

## Repository audit

### Reusable foundations

| Area | Existing implementation | Reuse direction |
| --- | --- | --- |
| Controls | `Control` partials, `ControlCollection`, `Form`, `WindowBase`, `SkiaControlSurface` | Pages and new controls remain normal controls; do not add a second visual tree. |
| Properties | CLR properties plus compact internal `PropertyStore` | Dynamic resources and future generated descriptors invoke existing setters. No dependency-property clone. |
| Layout | `LayoutEngine`, `DefaultLayout`, `FlowLayoutPanel`, `TableLayoutPanel`, `Dock`, `Anchor`, margin/padding/min/max/AutoSize | Page hosts, collection presenters, shell panes, and shapes must use these transactions and constraints. |
| Styles | `ControlStyle`, parent-style fallback, normal/hover state, compatibility `BackColor`/`ForeColor` | Extend state/style representation incrementally; preserve renderer-facing style objects. |
| Paints | Observable `Drawing.Brush`; solid, glass, no-fill, linear/radial/sweep gradients; typed stops; opacity/transform/spread; shared Skia adapter; strict ThemeManager JSON | Reuse for ThemeManager, shapes, and future charts. Keep JSON validation in the one ThemeManager schema. |
| Rendering | Per-control `SKBitmap` back buffers, renderer classes, `PaintEventArgs`, Skia canvas helpers | Shapes/charts/documents render through Skia and existing clipping/invalidation. Avoid native control substitution. |
| Invalidation | Property setters call `Invalidate`, layout setters use layout transactions; windows invalidate platform surfaces | Dynamic values call normal setters and do not globally repaint. Add dirty-region precision later. |
| Animation | Shared monotonic `AnimationScheduler`, composable definitions/runs, handles, owner/key replacement, typed interpolators, Brush transitions, native reduced-motion policy, animated bounds, layout-aware state metrics, diagnostics, and compatibility helpers | Reuse for theme/shape/navigation transitions; preserve the single scheduler and existing layout-transition contract. |
| Input | Framework mouse/keyboard/text/IME pipeline, capture, hit testing, touch scrolling in `SkiaControlSurface` | Collection, SearchBar, pages, charts, and shapes share the same input path. |
| Data binding | `IBindableComponent`, `Binding`, `BindingContext`, `BindingSource`, list managers and converters | Reuse for items sources and selected values; add collection-change/virtualization contracts instead of a parallel binding engine. |
| Serialization | `System.Text.Json` in designer and binding conversion; stable design document serializer | Reuse conventions and converters, but keep theme/localization runtime schemas separate from designer files. |
| Documents | `Documents.Document`, block/inline/table/image/list/code model, Markdown parser, layout/text map/selection/cache, `DocumentViewer` | Evolve and extract compatibly. Do not recreate the requested model under duplicate public names. |
| Windows | Full `IWindowingPlatform`, Win32 input/window/services and Skia framebuffer path | Primary runtime for page hosting, printing, clipboard, system theme, pointer/keyboard validation. |
| Android | Lifecycle/permissions/main-thread dispatcher, Choreographer/settings integration, `AndroidSkiaHostView`, `SkiaControlSurface`, native IME and multi-touch | Shared controls can be validated now; window/shell/back/accessibility/service parity and broad device evidence remain separate prerequisites. |
| Packaging | Packable core and WindowKit projects, conditional Windows backend, templates, tests and samples | Add optional feature packages without reversing dependency direction. |

### Architectural gaps

- No page lifecycle, page host, navigation stack, route registry, deep-link contract, or back-request
  abstraction.
- `ThemeManager` now provides strict JSON, validation, inheritance, atomic apply, dynamic-resource
  defaults, state styles, and transitions. Automatic OS-theme reapply, file hot reload, a shared
  shadow rendering contract, and an Android system-theme provider remain gaps.
- `ControlStyle` resolves Normal/Hover/Pressed/Focused/Disabled states with brush, typography,
  border, corner, transform, and transition data. Layout-aware interpolation is deliberately
  limited to padding and border widths; selected-state and broader metric contracts remain future.
- Brush mutation, opacity, transforms, stable stop ordering, spread modes, planner interpolation,
  strict Theme JSON, and targeted invalidation are implemented. A batch update scope, absolute
  mapping, and selectable color-space interpolation remain.
- The shared scheduler marshals callbacks to the UI dispatcher, uses elapsed monotonic time, stops
  while idle, pauses over Android background lifecycle, and integrates Windows/experimental Android
  reduced-motion preferences. Android now has a lifecycle/subscription-aware settings observer and
  Choreographer source; physical-device frame-pacing/profile validation remains future work.
- No localization catalog/provider, plural rules, missing-key diagnostics, dynamic culture change,
  or verified end-to-end RTL layout behavior.
- Lists and grids are retained collections; there is no shared item-container generator,
  virtualization/recycling, incremental loading, or observable items-source abstraction suitable
  for `CollectionView` and charts.
- The retained Shape/Geometry model, path figures, line/quadratic/cubic segments, transforms, Skia
  rendering, hit testing, Designer round trips, and ControlGallery coverage are implemented.
  Arcs, geometry groups/boolean operations, SVG import, a general Stretch contract, and graphical
  Bezier editing remain future work.
- No general document provider registry, MIME sniffing contract, paged render source, password
  request, or platform print adapter. Existing document code is in the main package.
- Android does not yet host `Form`, multiple windows, general popups/dialogs, platform accessibility,
  printing, clipboard parity, or native back/deep-link integration.

### Potential API conflicts

- `Document`, `ImageBlock`, `ListBlock`, `TableBlock`, `CodeBlock`, and `DocumentViewer` already exist.
  New document modules must preserve them, move them only with type forwarding/versioned migration,
  or introduce explicitly differentiated provider contracts.
- `DateTimePicker` already supports date and time formats. `TimePicker` should share extracted time
  parsing/spin/edit logic instead of forking it.
- `TabControl` and `NavigationPane` are controls, not page lifecycle containers. `TabbedPage` and
  `FlyoutPage` may compose/reuse render/layout primitives but must not inherit incompatible item
  semantics merely for code reuse.
- `Theme` and `BuiltInTheme` are public. ThemeManager must retain a compatibility facade and cannot
  silently redefine existing values or event order.
- `ModernFormsNext.Path` collides conceptually with `System.IO.Path`; qualify either type where both
  are used. Moving the shipped public type to a proposed `ModernFormsNext.Shapes` namespace would
  be a breaking change and is not roadmap cleanup.
- `Application` is static and `Application.Run(Form)` is Windows-oriented. AppShell must not make
  `Application` state instance-based as an accidental breaking change.

### Dependency map

```text
Dynamic resources
  -> Paint/brush value contracts
  -> Shared UI animation scheduler (implemented foundation)
  -> ThemeManager -> state styles -> every visual feature
  -> Localization -> pages and shell labels

Page lifecycle -> ContentPage -> NavigationPage -> routing
  -> TabbedPage / FlyoutPage -> AppShell

Items source + container recycling -> CollectionView
  -> CarouselView / SearchBar suggestions / chart legends
  -> RefreshView composes any scroll host

Geometry + Paint -> Shapes (implemented foundation)
  -> future chart render primitives -> diagrams

Document provider + viewport contracts
  -> PDF provider/viewer -> general DocumentViewer
  -> editable document operations -> DocumentEditor
```

## Package boundaries

### Keep in `ModernFormsNext`

- dynamic resource contracts and control/window/application scopes;
- paint/brush primitives, theme/localization contracts and lightweight default JSON loaders;
- page lifecycle, navigation/routing abstractions, AppShell descriptors;
- lightweight shared controls (`CollectionView`, `CarouselView`, `RefreshView`, `SearchBar`,
  `TimePicker`) after virtualization exists;
- shape and geometry primitives rendered by Skia;
- platform-neutral provider/host interfaces that do not pull heavy codecs into core.

### Optional NuGet packages

| Package | Intended content | Must not contain |
| --- | --- | --- |
| `ModernFormsNext.Documents` | Existing/evolved document model, provider registry, viewport/source contracts, lightweight viewer host | PDF engine, platform P/Invoke |
| `ModernFormsNext.Documents.Markdown` | Markdown provider/parser/renderer and Markdig dependency | PDF or rich editor engine |
| `ModernFormsNext.Documents.RichText` | Rich editing operations, RichTextKit integration, rich serializer | Windows-only UI |
| `ModernFormsNext.Documents.Pdf` | PDF page/text/link provider, cache and password workflow | unconditional core dependency |
| `ModernFormsNext.DataVisualization` | chart model, series, axes, renderers, interaction and export abstraction | native platform controls |
| `ModernFormsNext.Diagrams` (later) | graph layout, connectors, nodes, diagram interaction | chart-only primitives duplicated from DataVisualization |

Windows printing/system-theme integrations belong in the Windows backend or a narrow optional
adapter. Android activity/back/share/print integration belongs in the Android backend. Core and
WindowKit abstractions must not reference optional packages.

## Delivery rules and common completion gates

Every stage must:

- retain existing public API unless a separately approved migration is documented;
- document every public/protected API with XML comments and realistic code examples;
- add headless unit tests for state, hierarchy, event order, fallback, cancellation, and disposal;
- add renderer/layout/input tests where a visual feature is involved;
- add a focused `ControlGallery` example for visible controls, never experimental clutter in
  `ModernFormsNext.DemoApp`;
- build `net10.0` and `net10.0-windows`; build/deploy Android when the workload/device is available;
- include manual Windows keyboard/mouse/DPI/theme checks and Android touch/IME/lifecycle checks as
  appropriate;
- avoid allocations in render/layout/input hot paths and define ownership for Skia/native objects.

## Staged roadmap

### Stage 0 — dynamic resource foundation (implemented vertical slice)

#### 1. Dynamic resources — difficulty: High

Purpose: arbitrary typed resources at application, window, ancestor-control, and control scopes with
live references and fallback.

Proposed/implemented API:

```csharp
Application.Resources["Button.Primary.Background"] = SKColors.DodgerBlue;
button.SetResourceReference(nameof(Control.BackColor), "Button.Primary.Background");
button.TryFindResource("Button.Primary.Background", out var value);
button.ClearResourceReference(nameof(Control.BackColor));
```

Dependencies: existing CLR properties, `PropertyStore`, control parent/window lookup, normal setters.

Risks and platform impact: reflection metadata for trimming/AOT; synchronous UI-thread setters;
reparenting cost proportional to affected subtree. Shared code has no Windows/Android dependency.

Completion criteria and tests: app/window/control precedence; missing/removal fallback; runtime
updates; unchanged effective values do not call setters; incompatible types; UI-thread invocation;
reparent/removal behavior; disposal and GC prove weak subscriptions; XML docs and usage guide. The
initial implementation and 14 focused tests satisfy this vertical slice. Merged dictionaries,
serialization, factories, and transitions remain later work.

### Stage 1 — visual values, themes, and localization

#### 2. Paint and gradient contracts — implemented foundation

Purpose: harden the existing brush hierarchy into reusable theme/shape/chart values rather than add
a duplicate `Paint` abstraction. The foundation is implemented.

Implemented API: observable, shared `Brush` values with `Opacity`, `Matrix3x2` transform,
`NoBrush`, platform-neutral color/point members, `GradientStopCollection`, stable stop ordering,
radial focal origin, and `Pad`/`Repeat`/`Reflect`. Existing Skia members remain compatibility views
of the same backing values.

Dependencies: dynamic resources and the shared Skia renderer; both are integrated.

Risks/platform: bounds-dependent shaders remain short-lived allocations. Windows and Android share
the model, coordinate math, and shader factory, but physical-device Android GPU validation remains
manual while the backend is experimental.

Done/tests: solid/linear/radial/focal/sweep/no-fill rendering, opacity, transform, tile modes,
bounds, strict offsets, stable duplicate offsets, mutation, resource precedence/fallback, weak
subscriptions, Designer round-trip, and scoped shader disposal are covered. ControlGallery provides
manual visual checks. ThemeManager provides the strict versioned JSON schema. Batch notifications,
absolute mapping, and advanced color interpolation are explicitly deferred.

#### UI animation scheduler — implemented foundation

Purpose: provide one platform-neutral, monotonic UI scheduler for control, value, Brush, theme,
shape, and future navigation transitions without a timer per animation.

Implemented API: `AnimationScheduler`, `AnimationHandle`, `AnimationState`, `AnimationOptions`,
owner/key replacement, `AnimationPolicy`, `AnimationSchedulerDiagnostics`, built-in easing, typed
interpolators, explicit in-place Brush/GradientStop transitions, and the existing control helpers as
compatibility adapters. Callbacks use the Windows or experimental Android UI dispatcher.

Lifecycle and performance: the shared tick source runs only while work is active; Android
background time is paused and excluded; control detach/dispose and application exit cancel owned
work. Progress uses elapsed `Stopwatch` time, tick requests are coalesced, and the hot path reuses
its active-entry buffer rather than allocating a LINQ snapshot each frame.

Done/tests: deterministic manual clock/tick tests cover progress, delay, dropped frames, pause,
replacement, cancellation, faults, policy modes, dispatcher affinity, high animation counts,
interpolation, Brush/dynamic-resource invalidation, composition, keyframes, repeat, auto-reverse,
visual-state transitions, interaction effects, and owner lifetime without `Thread.Sleep`.
ControlGallery provides opt-in manual checks and cancels all work on unload. Animated bounds and
selected visual-state layout metrics are implemented in 1.10.0; physical-device Android frame-
pacing validation remains deferred.

#### 3. ThemeManager — implemented foundation

Purpose: validated themes containing colors, brushes/gradients, typography, spacing, padding,
sizes, radii, borders, motion duration/easing, custom resources, and platform variants.

Implemented API: `ThemeDefinition`, `ThemeResolvedSnapshot`, `ThemeVariant`,
`ThemeManager.Current`, `ThemeJsonSerializer`, `Apply`/`ApplyAsync`, `ThemeValidationResult`, typed
`ThemeTokens`, dedicated theme-resource fallback, diagnostics, and `ThemeTransitionOptions`.

Dependencies: dynamic resources, Brush contracts, the shared UI animation scheduler, and a small
optional backend system-theme service.

Platform status: static `Theme` remains a compatibility projection. Windows reads system
light/dark and reduced-motion settings on apply. Android remains experimental and uses an explicit
System fallback; its existing lifecycle service pauses shared scheduler time. Live OS-theme change
notifications and an Android theme provider remain future work.

Done/tests: light/dark/system/fallback, inheritance/cycles/depth/type validation, immutable Brush
ownership, resource precedence and rollback, JSON round trip/allow-list/security limits, runtime
switch without rebuilding controls, transition replacement/cancellation/motion policy/lifecycle,
Designer isolation, and ControlGallery diagnostics. Hot reload, automatic system-change reapply,
shadow tokens, and Android device/emulator validation remain explicit follow-up work.

#### 4. Localization — difficulty: High

Purpose: JSON-first provider system, dynamic culture switch, fallback (`de-DE -> de -> default`),
parameters, plurals, formatting, namespaces, external providers, missing-key diagnostics, validation,
optional generated keys, and RTL metadata.

Proposed API: `ILocalizationProvider`, `LocalizationManager`, `Localizer[key]`,
`SetLocalizedText(key)`, `LocalizationOptions`, `MissingTranslation` diagnostic, optional key generator.

Dependencies: dynamic resources; dispatcher; later page/shell consumes it.

Risks/platform: plural-rule correctness, untrusted format strings, provider precedence, culture versus
UI culture, RTL beyond text alignment, file watching on Android packages. Shared provider logic;
Windows can hot reload files, Android normally loads embedded/assets catalogs.

Done/tests: exact/neutral/default fallback, parameters and escaping, locale-specific number/date/time/
currency, plural categories for several language families, missing/invalid catalogs, external
namespace provider, live text update, cancellation-safe culture change, RTL layout audit and both
platforms' culture-change behavior.

### Stage 2 — pages and navigation

#### 5. Page lifecycle — difficulty: High

Purpose: a shared control-root lifecycle independent of native windows.

Proposed API: `Page : Control`, `PageHost`, `PageLifecycleState`, `Appearing`, `Appeared`,
`Disappearing`, `Disappeared`, guarded async hooks, title/icon/back metadata.

Dependencies: resources/localization; existing control lifecycle and dispatcher.

Risks/platform: event reentrancy, async cancellation, activity pause versus page disappearance,
ownership/disposal, binding context propagation. Windows host starts inside a form; Android host uses
`SkiaControlSurface` without claiming window parity.

Done/tests: complete legal/illegal state matrix, exact event order, cancelled/interrupted transition,
detach/reattach, resource/binding inheritance, disposal, Windows close/minimize distinction and
Android pause/rotation behavior.

#### 6. ContentPage — difficulty: Medium

Purpose: one-content page with page chrome slots and predictable layout.

Proposed API: `ContentPage.Content`, `Padding`, page background/style keys, optional title-bar content.

Dependencies: Page lifecycle and existing dock/layout engines.

Risks/platform: content ownership/reparenting and safe-area/inset abstraction on Android.

Done/tests: content replacement/disposal policy, dock/fill/min/max/AutoSize, resource inheritance,
safe-area layout, focus entry, renderer snapshot and ControlGallery example.

#### 7. NavigationPage — difficulty: Very high

Purpose: serialized page stack with push/pop/back, optional transitions, and predictable lifetime.

Proposed API: `PushAsync`, `PopAsync`, `PopToRootAsync`, `NavigationStack`, `Navigating`, `Navigated`,
`INavigationTransition`, back-request handling.

Dependencies: Page/ContentPage; fixed UI-thread animation scheduler.

Risks/platform: concurrent calls, cancellation midway, retained pages, focus restore, Android system
back and Windows close/Alt+Left semantics.

Done/tests: stack invariants, event order, duplicate pages policy, cancellation, exception rollback,
back interception, focus, resource scope, transition interruption, memory release and mouse/touch/
keyboard manual checks.

#### 8. Routing — difficulty: High

Purpose: map URI-like routes and parameters to page factories without coupling parsing to rendering.

Proposed API: `RouteRegistry.Register`, `IRouteFactory`, `RouteMatch`, `NavigateToAsync`, query/path
parameter conversion, relative/absolute routes, guards and not-found result.

Dependencies: NavigationPage; DI integration remains optional.

Risks/platform: ambiguous patterns, parameter encoding, security of external deep links, factory
lifetime, restoring state after process recreation.

Done/tests: normalization/encoding, precedence/ambiguity, typed parameters, cancellation/guards,
not-found, nested navigation, deep-link adapter contracts, and deterministic serialization of route
state.

#### 9. TabbedPage — difficulty: High

Purpose: page-aware selection and lifecycle, not merely a renamed `TabControl`.

Proposed API: `Pages`, `SelectedPage`, `SelectedIndex`, `SelectionChanging/Changed`, tab placement,
overflow policy, optional lazy page factory.

Dependencies: Page lifecycle, collection observation, theme/localization.

Risks/platform: many retained page trees, accessibility, focus restoration, touch gestures versus
carousel navigation. Reuse `TabControl` renderer ideas without inheriting incompatible ownership.

Done/tests: add/remove/reorder/select, lifecycle order, disabled tabs, overflow, keyboard arrows and
Ctrl+Tab, touch, resource scope, memory of lazy pages, Windows/Android visual checks.

#### 10. FlyoutPage — difficulty: High

Purpose: flyout/master plus detail page with responsive presentation.

Proposed API: `Flyout`, `Detail`, `IsPresented`, `FlyoutWidth`, `FlyoutBehavior` (overlay/split/auto),
edge gesture and dismissal policy.

Dependencies: Page lifecycle, responsive layout, back handling, theme.

Risks/platform: gesture conflict with horizontal scroll/carousel, safe areas, focus trap, accessibility,
window resizing and Android back dismissal.

Done/tests: overlay/split breakpoints, focus/capture, escape/back/outside-click dismissal, RTL edge,
resize/rotation, animation cancellation and platform manual input checks.

#### 11. AppShell — difficulty: Very high

Purpose: declarative code-first composition of routes, flyout, tabs, navigation stacks, and global
commands without XAML.

Proposed API: `AppShell`, `ShellItem`, `ShellSection`, `ShellContent`, route registration builders,
global navigation events and state restoration.

Dependencies: all page/navigation items; themes and localization.

Risks/platform: becoming an oversized service locator, state restoration, nested stack semantics,
deep links, Android activity lifecycle, desktop multi-window future.

Done/tests: hierarchy validation, route uniqueness, nested navigation/back, selected-state restore,
dynamic localized labels/themes, permission-denied deep link, Windows form host and Android surface
host reference samples. Do not alter the default template until the shell is stable and recommended.

### Stage 3 — data presentation controls

#### 12. CollectionView foundation — difficulty: Very high

Purpose: virtualized/recycled item presentation with selection, grouping, empty view, incremental
loading, headers/footers, item templates, and list/grid layouts.

Proposed API: `ItemsSource`, `ItemTemplate`, `ItemsLayout`, `SelectionMode`, `SelectedItem(s)`,
`GroupHeaderTemplate`, `RemainingItemsThreshold`, `IItemsViewSource`, container generator/recycler.

Dependencies: existing data binding plus new observable-list and virtualization contracts; themes.

Risks/platform: variable-size measurement, focus/capture during recycling, collection changes during
layout, large data, accessibility virtual children, touch inertia. Shared implementation is required;
Android must not wrap a native RecyclerView.

Done/tests: 0/1/100k items, add/remove/move/reset, variable sizes, recycling identity, selection,
grouping, keyboard/touch scrolling, cancellation of incremental loads, allocation/performance budget,
DPI/density and ControlGallery stress page.

#### 13. CarouselView — difficulty: High

Purpose: snap-oriented virtualized single/few-item presentation with indicators and looping policy.

Proposed API: reuse CollectionView items/template/source; `Position`, `CurrentItem`, `PeekAreaInsets`,
`Loop`, `IsSwipeEnabled`, `PositionChanged`.

Dependencies: CollectionView recycler, touch/drag physics, animation scheduler.

Risks/platform: infinite-loop identity, gesture arbitration, resize mid-snap, accessibility and reduced
motion.

Done/tests: empty/single/many/loop, add/remove current item, programmatic versus gesture navigation,
snap/cancel, RTL, keyboard, multi-touch cancellation, memory/performance on both platforms.

#### 14. RefreshView — difficulty: High

Purpose: composable pull-to-refresh wrapper with command/cancellation and loading indicator.

Proposed API: `Content`, `IsRefreshing`, `RefreshCommand`, `RefreshRequested`, threshold, indicator
template/color and `CompleteRefresh`/async command semantics.

Dependencies: scroll host contract, touch gesture arbitration, theme.

Risks/platform: conflict with nested scrolling/carousel/flyout, double invocation, refresh exception,
Android lifecycle cancellation and desktop non-touch discoverability.

Done/tests: threshold/hysteresis, nested scroll at top only, mouse/touch/keyboard trigger, concurrent
refresh prevention, cancellation/disposal, error recovery and Android pause/detach.

#### 15. SearchBar — difficulty: Very high

Purpose: a full search workflow rather than a `TextBox` with an icon.

Proposed API: `Text`, `SearchCommand`, `SearchRequested`; `ISearchSuggestionProvider` with local and
async providers; debounce duration; cancellation token per request; `SuggestionTemplate`, group
descriptor/template, matching/highlight spans; `ISearchHistoryStore`, JSON default store,
`IsHistoryEnabled`; loading/empty/error views and selection/navigation events.

Dependencies: TextBox/IME, popup host, CollectionView virtualization, dispatcher/cancellation,
localization/theme, optional storage abstraction.

Risks/platform: stale async results, debounce race, provider exceptions, IME composition triggering
premature searches, focus/capture, sensitive query persistence, touch/keyboard accessibility,
unbounded history. History must be opt-out and pluggable; JSON I/O must not block UI.

Done/tests: local/async merge and grouping, request cancellation ordering, debounce with deterministic
clock, IME composition, Up/Down/Enter/Escape/Tab, mouse/touch selection, arbitrary templates,
highlighting Unicode/culture cases, loading/empty/error, disabled history, JSON round trip/corruption,
custom store, disposal during request, and Windows/Android IME/manual checks.

#### 16. TimePicker — difficulty: Medium–High

Purpose: focused time selection with culture, 12/24-hour, optional seconds, bounds, nullable value,
keyboard/touch editing, and custom rendering.

Proposed API: `TimeOnly? Time`, `MinimumTime`, `MaximumTime`, `Format`, `MinuteIncrement`,
`SecondIncrement`, `Is24Hour`, `TimeChanged`, popup/spinner presenter.

Dependencies: extract shared segmented editing/parsing from `DateTimePicker`; localization/theme;
popup/input.

Risks/platform: midnight-wrapping ranges, culture calendars/designators, mobile keyboard, duplicate
logic with DateTimePicker.

Done/tests: cultures/12–24h/seconds, bounds and wrapping policy, nullable state, keyboard/spinner/touch,
format parsing, DST explicitly irrelevant to `TimeOnly`, accessibility and shared regression tests for
DateTimePicker.

### Stage 4 — shapes and vector geometry (implemented 1.10.0 foundation)

#### 17. Shape and geometry system — delivered foundation; advanced scope remains

Implemented: `Shape`, `Ellipse`, `Circle`, `Line`, `Path`, `Polygon`, and `Polyline`; fill/stroke,
opacity, caps/joins/dashes, transforms, fill rules, layout, invalidation, hit testing, Skia resource
caching, and Designer round trips. `ModernFormsNext.Drawing` contains line, rectangle, ellipse,
path/figure, line-segment, quadratic-Bezier, and cubic-Bezier geometry. The Designer provides
structured point/path editors and culture-invariant compact parsing. ControlGallery and focused
tests cover runtime/Designer behavior without introducing another renderer or layout engine.

Remaining advanced scope: arc segments, geometry groups/boolean operations, SVG import or a core
SVG-style path grammar, a general `Stretch` contract, generic Control geometry clips, and a
graphical Bezier editor. These are proposals, not 1.10.0 capabilities. Android compiles the shared
renderer, but physical-device visual/touch/GPU/cache profiling is still a validation gap. See
[Shapes and vector geometry](../shapes-and-vector-geometry.md) and
[Known limitations](../known-limitations.md).

### Stage 5 — modular documents

#### 18. DocumentViewer provider and viewport architecture — difficulty: Very high

Purpose: first reconcile the existing model/viewer with a modular provider system before adding PDF,
then make `DocumentViewer` select providers by format/MIME while preserving current behavior.

Proposed API: `IDocumentProvider`, `IDocumentSource`, format/MIME probe, `DocumentOpenRequest`,
`IDocumentPageSource`, viewport/prefetch contract, cancellation, capability flags, link/text/search
interfaces and renderer/provider registry; evolve `DocumentViewer` with `OpenAsync`, loading/error/
unsupported views, and capability-gated navigation/search/zoom commands.

Dependencies: resources/theme/localization, virtualization/cache patterns, optional package ADR.

Risks/platform: public type extraction, stream ownership, seekability, cancellation, MIME spoofing,
provider precedence and package cycles.

Done/tests: compatibility API inventory and existing document suite stays green; no breaking move
without approved migration; provider selection/fallback by extension/MIME/content; switch and
cancellation; non-seekable stream; visible-page requests only; cache budget; malformed/unsupported/
ambiguous document isolation; capability gating. Produce a package migration prototype before
changing NuGet contents.

#### 19. PDFViewer / PDF provider — difficulty: Extreme

Purpose: optional PDF module supporting file/stream, visible-page rendering, continuous/single-page,
zoom, thumbnails, text search/selection/copy, links, password documents, navigation, cache and
supported-platform printing.

Proposed API: PDF implementation of item 18 plus `PdfViewer` convenience control only if it adds
meaningful PDF-specific commands; otherwise `DocumentViewer` with a PDF provider is preferred.

Dependencies: item 18, CollectionView-like page virtualization, optional PDF engine,
clipboard/print services.

Risks/platform: engine license/security/CVEs, native binaries and Android ABIs, password handling,
font fidelity, huge pages, malformed files, memory/GPU cache, print parity. Engine selection is a
separate approval because it adds a large dependency.

Done/tests: file/seekable/non-seekable stream, visible-page decode and cancellation, zoom/cache
eviction, continuous/single mode, thumbnails, search/text mapping/selection/copy, internal/external
links, password success/failure/no logging, corrupted/encrypted/large corpus, Windows print and
explicit Android print limitation/adapter, package size/license audit.

#### 20. DocumentEditor — difficulty: Extreme and incremental

Purpose: evolve the existing `Document`, `ParagraphBlock`, `HeadingBlock`, `TextInline`, `ImageBlock`,
`ListBlock`, `TableBlock`, and `CodeBlock` into an editable operation model, not a Word clone.

Proposed API: immutable or transaction-controlled document nodes, selection/range, edit operations,
undo/redo transactions, commands, clipboard format adapters, serialization providers and an editor
control that renders through the document layout engine.

Dependencies: document model compatibility decision, viewer/layout, input/IME, accessibility,
localization/theme, RichText optional package.

Risks/platform: current model names already public, structural selection, Unicode graphemes/IME,
table editing, image ownership, undo memory, paste sanitization, huge documents and accessibility.

Done/tests: paragraph/heading/run/image/list/table/code insert/delete/split/merge; Unicode/IME;
selection mapping; undo grouping/redo invalidation; clipboard sanitize; serializer round trip;
incremental layout and performance. Advanced pagination, track changes, mail merge, and Word fidelity
are explicitly out of initial scope.

### Stage 6 — visualization and diagrams

#### 21. DataVisualization — difficulty: Extreme

Purpose: optional chart module for line, area, bar, column, pie, doughnut, scatter, bubble, radar,
candlestick, and heatmap with shared series/axes/legend/tooltip/selection/zoom/pan/animation/realtime/
aggregation/export architecture.

Proposed API: `Chart`, typed `Series<T>`, numeric/category/time/log axes, scales, legend/tooltip
presenters, viewport/selection, decimation/aggregation strategy, data adapter, exporter interfaces.

Dependencies: paints, geometry, themes, item-source observation, input, animation, document/export
abstractions where appropriate.

Risks/platform: large-data performance, numerical/date precision, label layout, gesture conflict,
allocation in realtime updates, accessibility, SVG/PDF export fidelity. Shared Skia renderer first;
platform code only for save/share/print destinations.

Done/tests: each series mapping/bounds/empty/NaN cases; axes/ticks/culture; legend/tooltip/selection;
mouse/keyboard/touch zoom/pan; realtime bounded memory; 100k/1m point performance budgets with
decimation; theme switch; deterministic bitmap/SVG export and PDF through optional adapter;
ControlGallery-like dedicated visualization sample.

#### 22. Diagrams — difficulty: Extreme, after charts

Purpose: flow, organization, dependency/network graphs, mind maps, Sankey, timeline, and gauge using
shared geometry/input/theme primitives without forcing unrelated models into one class.

Proposed API: common node/port/edge/viewport/selection/command foundation; pluggable layout engines;
specialized Sankey/timeline/gauge models where graph semantics do not fit.

Dependencies: geometry/shapes, visualization viewport and interaction, item sources, editing command
and undo concepts.

Risks/platform: automatic layout complexity, cycles, connector routing, very large graphs, text
measurement, accessibility, drag/zoom/touch conflicts and export.

Done/tests: deterministic layout fixtures, cycle and disconnected graph handling, routing/hit testing,
selection/drag/keyboard, undo where editable, virtualization/performance, theme/localization, SVG/
bitmap export and platform manual interaction. Sankey/gauge should not be forced through graph APIs
when specialized render models are clearer.

## Version bands

Exact semantic versions are intentionally not assigned until release capacity is known. Use these
dependency-based bands:

- Foundation work: dynamic resources, paint hardening, the shared/composable UI animation platform,
  interaction effects, and the ThemeManager/JSON/atomic-transition foundation are implemented.
- Globalization release: localization, culture fallback, live resource updates, and diagnostics on
  the completed theme foundation.
- Navigation preview: Page/ContentPage/NavigationPage/routing; Windows host plus Android surface host.
- Shell preview: TabbedPage/FlyoutPage/AppShell after lifecycle/back tests are stable.
- Data controls preview: virtualization foundation, CollectionView, then carousel/refresh/search/time.
- Vector foundation: shapes, retained geometry, Skia rendering, hit testing, Designer round trips,
  and compact path parsing are implemented; advanced geometry/authoring remains future.
- Documents preview: provider contracts and compatibility plan, then optional PDF, viewer unification,
  editor slices.
- Visualization preview: core charts first; advanced charts and diagrams in later increments.

## Known cross-cutting risks

- Android parity must be reported feature by feature; the current backend is not a full windowing
  backend.
- The main package already carries Markdig and RichTextKit plus document APIs. Modularization may
  require type forwarding or a major-version migration and cannot be performed silently.
- Theme/localization/page changes touch application lifetime and static state; tests need isolation
  and explicit reset APIs before broad parallel execution.
- Reflection-based CLR property references need a trimming/source-generation strategy before AOT is
  advertised.
- Android physical-device pacing and profiling remain open even though shared animation callbacks
  use Choreographer and the lifecycle/subscription-aware provider observes motion preferences.
- Virtualization, document rendering, and charts compete for cache/memory budgets. Introduce shared
  diagnostics and bounded caches rather than independent unbounded stores.

## Recommended next stage

**Localization on the completed resource/theme foundation.** Build JSON-first localization
providers, culture fallback, formatting/plurals, live resource updates, safe diagnostics, and RTL
metadata without duplicating the property or resource systems. Shape foundation is already shipped;
advanced geometry and theme shadows should wait for explicit contracts. In parallel, prioritize the
Designer transaction/parity backlog and Android device evidence before broadening platform claims.
