# ModernFormsNext framework roadmap

- Baseline audited: 2026-07-17
- SDK baseline: .NET 10 (`10.0.201`)
- Runtime priority: Windows first; Android is an experimental shared-control Skia vertical slice
- Purpose: architecture and delivery sequence, not a promise that every listed API is implemented

## Executive summary

ModernFormsNext already has more reusable foundation than the feature list initially suggests. The
main package contains a WinForms-like `Control` tree, mature dock/anchor/flow/table layout,
`ControlStyle`, solid and gradient brushes, renderer classes, Skia invalidation/back buffers, input
routing, a data-binding stack, basic animations, `NavigationPane`, `TabControl`, `DateTimePicker`, a
document model, Markdown parser/editor, and a substantial `DocumentViewer`. Windows has the complete
windowing backend. Android has lifecycle, dispatcher, permission infrastructure, and one real shared
control tree rendered by `AndroidSkiaHostView`, but not general `Application.Run(Form)`, multi-window,
or the complete WindowKit service set.

The architecture should therefore be extended, not replaced. The implementation order is:

1. dynamic resources (first vertical slice implemented with this roadmap);
2. paint/brush contracts and theme tokens;
3. ThemeManager and localization;
4. page lifecycle, navigation, routing, tabs, flyout, and shell;
5. virtualized data controls and SearchBar;
6. shapes and path geometry;
7. modular document providers and viewers;
8. charts, then diagrams.

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
| Paints | `Drawing.Brush`, solid, glass, linear/radial/sweep gradient brushes, gradient stops, Skia extension renderers | Define lifetime, opacity, transforms, spread/tile behavior, immutability/notification, and JSON contracts. |
| Rendering | Per-control `SKBitmap` back buffers, renderer classes, `PaintEventArgs`, Skia canvas helpers | Shapes/charts/documents render through Skia and existing clipping/invalidation. Avoid native control substitution. |
| Invalidation | Property setters call `Invalidate`, layout setters use layout transactions; windows invalidate platform surfaces | Dynamic values call normal setters and do not globally repaint. Add dirty-region precision later. |
| Animation | `AnimationManager`, easing functions, opacity/translation/scale/rotation extensions | Reuse interpolation concepts, but move callbacks onto UI dispatcher before theme/shape transitions. |
| Input | Framework mouse/keyboard/text/IME pipeline, capture, hit testing, touch scrolling in `SkiaControlSurface` | Collection, SearchBar, pages, charts, and shapes share the same input path. |
| Data binding | `IBindableComponent`, `Binding`, `BindingContext`, `BindingSource`, list managers and converters | Reuse for items sources and selected values; add collection-change/virtualization contracts instead of a parallel binding engine. |
| Serialization | `System.Text.Json` in designer and binding conversion; stable design document serializer | Reuse conventions and converters, but keep theme/localization runtime schemas separate from designer files. |
| Documents | `Documents.Document`, block/inline/table/image/list/code model, Markdown parser, layout/text map/selection/cache, `DocumentViewer` | Evolve and extract compatibly. Do not recreate the requested model under duplicate public names. |
| Windows | Full `IWindowingPlatform`, Win32 input/window/services and Skia framebuffer path | Primary runtime for page hosting, printing, clipboard, system theme, pointer/keyboard validation. |
| Android | Lifecycle/permissions/main-thread dispatcher, `AndroidSkiaHostView`, `SkiaControlSurface`, native IME and multi-touch | Shared controls can be validated now; window/shell/back/accessibility/service parity remains a separate prerequisite. |
| Packaging | Packable core and WindowKit projects, conditional Windows backend, templates, tests and samples | Add optional feature packages without reversing dependency direction. |

### Architectural gaps

- No page lifecycle, page host, navigation stack, route registry, deep-link contract, or back-request
  abstraction.
- Existing `Theme` is a static heterogeneous bag centered on colors; it has no serializable schema,
  validation, inheritance, scoped overrides, state styles, system-theme adapter, or atomic update.
- `ControlStyle` has only a small normal/hover model. It lacks pressed/selected/disabled/focused state
  resolution, brushes for every surface, typography records, shadow, radius, and transitions.
- Existing brushes are mutable data objects with no standard opacity/transform/change notification,
  frozen lifetime, tile mode, or serializer.
- The animation loop can resume on a worker thread and invoke control setters there. It is not yet a
  safe scheduler for theme transitions or arbitrary animated properties.
- No localization catalog/provider, plural rules, missing-key diagnostics, dynamic culture change,
  or verified end-to-end RTL layout behavior.
- Lists and grids are retained collections; there is no shared item-container generator,
  virtualization/recycling, incremental loading, or observable items-source abstraction suitable
  for `CollectionView` and charts.
- No retained vector geometry/path parser/hit-testing model.
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
- `Path` collides conceptually with `System.IO.Path`; keep it in a clear namespace such as
  `ModernFormsNext.Shapes` and provide unambiguous documentation.
- `Application` is static and `Application.Run(Form)` is Windows-oriented. AppShell must not make
  `Application` state instance-based as an accidental breaking change.

### Dependency map

```text
Dynamic resources
  -> Paint/brush value contracts
  -> ThemeManager -> state styles -> every visual feature
  -> Localization -> pages and shell labels

Page lifecycle -> ContentPage -> NavigationPage -> routing
  -> TabbedPage / FlyoutPage -> AppShell

Items source + container recycling -> CollectionView
  -> CarouselView / SearchBar suggestions / chart legends
  -> RefreshView composes any scroll host

Geometry + Paint -> Shapes
  -> chart render primitives -> diagrams

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

#### 2. Paint and gradient contracts — difficulty: Medium

Purpose: harden the existing brush hierarchy into reusable theme/shape/chart values rather than add
a duplicate `Paint` abstraction immediately.

Proposed API: evolve `Brush` with `Opacity`, optional transform, immutable/frozen snapshots or
change notification; add gradient spread/tile mode and typed stop collection. Consider a `Paint`
name only if stroke/fill semantics cannot be expressed without breaking existing `Brush` APIs.

Dependencies: dynamic resources, current Skia extension renderers.

Risks/platform: mutable resources need invalidation; shared Skia behavior must match on GPU/CPU
surfaces; shader ownership and allocation in paint loops. Windows/Android should share all math.

Done/tests: solid/linear/radial/sweep rendering golden tests; opacity/transform/tile tests; invalid
stops rejected; no per-frame shader leaks; JSON round trip deferred until ThemeManager schema.

#### 3. ThemeManager — difficulty: Very high

Purpose: validated themes containing colors, brushes/gradients, typography, spacing, sizes, radii,
borders, shadows, control/state styles, motion duration/easing, and platform variants.

Proposed API: `ThemeDefinition`, `ThemeVariant`, `ThemeManager.Current`, `LoadJson`, `Apply`,
`ThemeValidationResult`, typed `ThemeKeys`, window/control resource overrides, and optional
`ThemeTransitionOptions`.

Dependencies: items 1–2; a UI-dispatcher animation scheduler; backend system-theme service.

Risks/platform: compatibility with static `Theme`; atomic update/event order; JSON versioning;
strong static event leaks; animation interruption; missing fonts; Android system theme/activity
recreation. Windows implements system change first; Android maps `uiMode` after host lifecycle is
stable.

Done/tests: light/dark/system, inheritance/cycles, resource fallback, application/window/control
overrides, JSON round trip and schema validation, hot-reload rollback on invalid file, runtime switch
without rebuilding controls, optional transition cancellation, system change simulation, Windows
manual DPI check and Android configuration-change check.

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

### Stage 4 — shapes and vector geometry

#### 17. Shape and geometry system — difficulty: Very high

Purpose: `Shape`, `Ellipse`, `Line`, `Path`, `Polygon`, and `Polyline` with fill/stroke, opacity,
caps/joins/dashes, geometry scaling, hit testing, and animatable properties.

Proposed API: `ModernFormsNext.Shapes.Shape : Control`; `Fill`, `Stroke`, `StrokeThickness`,
`StrokeLineCap`, `StrokeLineJoin`, `StrokeDashArray`, `Stretch`; geometry types `PathGeometry`,
`PathFigure`, `LineSegment`, `BezierSegment`, `QuadraticBezierSegment`, `ArcSegment`; a culture-
invariant SVG/XAML-like path parser with documented supported grammar.

Dependencies: hardened paints, UI-thread animation/interpolator service, layout/invalidation.

Risks/platform: arc conversion, numerical stability, bounds including stroke, dash scaling, path
parser security/complexity, mutable point collections, hit-test performance and name collision.
All geometry stays shared Skia code.

Done/tests: parser valid/invalid corpus, line/Bezier/quadratic/arc geometry, stretch modes, fill rules,
caps/joins/dashes/transparency/gradients, bounds and hit tests, mutation invalidation, animation
cancellation, no per-frame path leaks, golden rendering on Windows and Android density checks.

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

- Foundation release: dynamic resources, paint hardening, non-animated ThemeManager core.
- Globalization release: JSON themes, system variants, localization and diagnostics.
- Navigation preview: Page/ContentPage/NavigationPage/routing; Windows host plus Android surface host.
- Shell preview: TabbedPage/FlyoutPage/AppShell after lifecycle/back tests are stable.
- Data controls preview: virtualization foundation, CollectionView, then carousel/refresh/search/time.
- Vector preview: shapes/path parser and animation integration.
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
- Existing animation dispatch must be corrected before any public API promises UI-thread-safe
  arbitrary or theme animation.
- Virtualization, document rendering, and charts compete for cache/memory budgets. Introduce shared
  diagnostics and bounded caches rather than independent unbounded stores.

## Recommended next stage

Harden the existing brush/gradient model and fix animation dispatcher affinity, then implement the
non-animated ThemeManager core on dynamic resources. This validates arbitrary typed values, atomic
theme publication, compatibility with `Theme`/`ControlStyle`, and targeted invalidation before page
and control work multiplies the number of consumers.
