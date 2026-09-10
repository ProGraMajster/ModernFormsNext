# Initial queue audit: series 2 (Designer)

Audit date: 2026-09-10. Baseline: `master` / `origin/master` at
`ba396f9` (the coordinator fetched before this audit). This is an initial read-only
implementation audit, not authorization to bypass the requested series order. Re-audit the
then-current `master`, complete issue body, comments, linked work, and acceptance criteria before
implementing each item. No framework code, public API, branches, commits, or GitHub state were
changed by this audit.

All eleven queued issues were OPEN. Their full bodies, comments, and paginated GitHub timelines
were fetched; all eleven comment collections were empty. Related issue bodies/comments and
relevant PR bodies were also inspected. Local raw responses are under ignored
`artifacts/autonomous-audit/series2/`; durable conclusions and source references are below.

`PASS` below means the named behavior has identifiable current implementation and existing
regression coverage; it does **not** mean tests were run during this audit. `PARTIAL` identifies
existing foundations plus missing acceptance. `BLOCKED` identifies an actual prerequisite for
the affected scope. Whole-issue status is the baseline implementation status, not a completion
claim. All checks listed below are prospective regression gates. Builds, tests, native interaction,
emulator, and publish checks were **NOT EXECUTED in this audit**. Manual Visual Studio, physical
device, and hardware validation: **NOT EXECUTED — environment unavailable** in this audit.

## Existing foundations and historical evidence

| Source | Current state / verified implementation evidence |
| --- | --- |
| [#32](https://github.com/ProGraMajster/ModernFormsNext/issues/32) | CLOSED. Form/UserControl root model, source-only project UserControl discovery, atomic nested component boundaries; safe projection added by `6b5edcf`. Broader binary/custom-member claims in the older issue are refined by OPEN #68. |
| [#28](https://github.com/ProGraMajster/ModernFormsNext/issues/28) | CLOSED. Safe animation/effect metadata introduced by `839bcee`, editor refinement `8979d19`. General definition activation and arbitrary easing remain excluded. |
| [#42](https://github.com/ProGraMajster/ModernFormsNext/issues/42), [PR #92](https://github.com/ProGraMajster/ModernFormsNext/pull/92) | CLOSED / MERGED at `0fb159f`. Production-runtime versus Designer structured geometry/serialization/code-generation harness exists. Inherited-root coverage is explicitly deferred until #39; that does not make #39 a blocker for the already-completed parity foundation. |
| [#33](https://github.com/ProGraMajster/ModernFormsNext/issues/33), [PR #94](https://github.com/ProGraMajster/ModernFormsNext/pull/94) | CLOSED / MERGED at `d4c17c8`. Per-document bounded transactions/history, typed changes, selection restoration, cancellation, observer-failure rollback, and committed-state generation. |
| [#34](https://github.com/ProGraMajster/ModernFormsNext/issues/34), [PR #95](https://github.com/ProGraMajster/ModernFormsNext/pull/95) | CLOSED / MERGED at `46b98b5`. Data-only versioned subtree clipboard, unique names, atomic copy/cut/paste/duplicate; selection remains single-node. |
| [#41](https://github.com/ProGraMajster/ModernFormsNext/issues/41), [PR #96](https://github.com/ProGraMajster/ModernFormsNext/pull/96) | CLOSED / MERGED at `7946361`. Recovery envelope, bounded exact-file watching, dirty conflicts, atomic per-file save, and transactional generated-code import. |
| [#71](https://github.com/ProGraMajster/ModernFormsNext/issues/71), [PR #101](https://github.com/ProGraMajster/ModernFormsNext/pull/101) | CLOSED / MERGED at `1dce91b`. Existing owned out-of-process host, typed HWND, lifecycle/IPC, View Designer, and focus/DPI contract. The PR records historical interactive evidence; no new Visual Studio verification occurred here. |
| [#56](https://github.com/ProGraMajster/ModernFormsNext/issues/56), [PR #110](https://github.com/ProGraMajster/ModernFormsNext/pull/110) | CLOSED / MERGED at `6e3a7cf`. All runtime command phases completed; latest issue comments explicitly separate #108. `CommandDesignerTests` verify the current hidden/nonserializable tooling boundary. |
| [#44](https://github.com/ProGraMajster/ModernFormsNext/issues/44) | OPEN, future multi-targeting investigation. It is related to #84, not a dependency requiring .NET 8/9 support before resource trimming work. |

Shared inspection covered [Designer architecture](../designer-architecture.md),
[known limitations](../known-limitations.md), [roadmap](../roadmap/ModernFormsNext-Framework-Roadmap.md),
[CHANGELOG](../../CHANGELOG.md), [UserControls](../user-controls.md),
[transactions](../designer-transactions-and-undo.md), [clipboard](../designer-copy-paste.md),
[recovery](../designer-autosave-and-recovery.md), [host](../visual-studio-designer-host.md),
[animation editors](../designer-animation-effects.md), [dynamic resources](../dynamic-resources.md),
[commands](../commands.md), and the corresponding source/test projects. The appropriate manual
Designer sample is [DesignerPlayground](https://github.com/ProGraMajster/ModernFormsNext/tree/ba396f95adab82564a0681bc922096599ba8c1ca/samples/ModernFormsNext.DesignerPlayground);
runtime animation/command demonstrations already exist in [ControlGallery](https://github.com/ProGraMajster/ModernFormsNext/tree/ba396f95adab82564a0681bc922096599ba8c1ca/samples/ControlGallery).
The template reference app is not a testing playground.

## #40 — Add safe design-time execution and control isolation

Source: [issue #40](https://github.com/ProGraMajster/ModernFormsNext/issues/40).
Baseline status: **PARTIAL**, with concrete implementation gaps; not validation-only.

Already implemented: source-only discovery and private `.mfdesign` projection; no project assembly
loading or custom constructors; safe placeholders for missing/invalid/recursive custom controls;
framework-only runtime type resolution; per-control render/property error capture; owned
out-of-process Visual Studio host. Reopening and cache refresh after `.mfdesign` changes exist.
The request to investigate an out-of-process host is outdated as a greenfield task.

Important current gaps in
[DesignerSurfaceRenderer](https://github.com/ProGraMajster/ModernFormsNext/blob/ba396f95adab82564a0681bc922096599ba8c1ca/ModernFormsNext.Designer/Surface/DesignerSurfaceRenderer.cs):
`DrawRuntimeControl` creates framework controls but does not dispose them on success or failure,
and does not install a design-time `Site`. Its property application can set
[PictureBox.ImageLocation](https://github.com/ProGraMajster/ModernFormsNext/blob/ba396f95adab82564a0681bc922096599ba8c1ca/ModernFormsNext/PictureBox.cs), whose production setter starts
asynchronous URL/file loading and can start a GIF timer. Restricting constructors to the framework
assembly therefore does not by itself meet the no-network/no-runtime-services scope. Error strings
currently retain exception type/message rather than a structured source/stack trace. There is no
complete bounded preview-initialization/retry policy. Do not solve these gaps by allowing custom
project execution or by treating a child process as a security sandbox.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| One failing custom control does not crash the whole document | PASS for supported data-only custom preview | Extend failure/resource-bound tests around permitted framework previews. |
| Runtime-only controls show useful placeholders | PASS for unsupported custom types | Audit executable built-in property paths and provide explicit diagnostics/placeholders. |
| Errors identify control and failure source | PARTIAL | Node/type/property diagnostics exist; structured stack/source and actionable retry coverage incomplete. |
| Failed instances disposed safely | PARTIAL | Data-only custom controls create no runtime instance; created framework preview instances have an actual disposal gap. |
| Reopen/reload after fixing code | PARTIAL | Design-file changes refresh cache and document reopen works; source discovery refresh still needs reopening (#68). |

Dependency: no uncompleted issue is a hard prerequisite. Preserve completed #32/#41/#71.
Regression gates: `CustomUserControlPreviewTests`, `RuntimeControlPainterTests`,
`DesignerSafetyBannerTests`, `DesignerDiagnosticLogTests`, host lifecycle/process tests; add
deterministic no-I/O, failing-setter/render/dispose, bounded-work, retry, and design-mode fixtures.
Windows host changes stay in host/backend code; shared preview behavior must remain platform-neutral.

## #68 — Complete safe custom-control discovery and Designer metadata

Source: [issue #68](https://github.com/ProGraMajster/ModernFormsNext/issues/68).
Baseline status: **PARTIAL**. The user's queue requires #40 first.

[DesignerProjectUserControlDiscovery](https://github.com/ProGraMajster/ModernFormsNext/blob/ba396f95adab82564a0681bc922096599ba8c1ca/ModernFormsNext.Designer/Services/DesignerProjectUserControlDiscovery.cs)
parses project source, follows project-local inheritance, and admits public concrete non-generic
top-level UserControls. `DesignerToolboxService` reflects only the already-loaded framework
assembly and appends source descriptors. Project custom controls deliberately do not expose their
runtime reflected members in PropertyGrid. Source read errors can yield an empty catalog without
actionable metadata diagnostics. No safe binary catalog/rebuild/reference-refresh service exists.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| Source and binary discovery without execution | PARTIAL | Source UserControls exist; referenced binary-only controls and broader safe control metadata missing. |
| Safe properties/defaults/categories/converters/events | PARTIAL | Framework metadata exists; complete custom descriptor surface missing. Converter metadata must not instantiate arbitrary converter code. |
| Refresh after rebuild/reference changes | PARTIAL | Current source discovery requires reopen; controlled refresh/invalidation is missing. |
| Preserve unsupported/executable placeholders | PASS | Retain source preview/constructor-trap behavior. |
| Incompatible/stale/unsupported diagnostics | PARTIAL | Preview diagnostics exist; binary/version/discovery diagnostics missing. |
| Nested/inherited/serialization/generation/runtime parity | PARTIAL | Nested component fixtures and parity harness exist; binary/custom-member/multilevel inherited-root coverage missing. |

Dependencies: #32 and #42 foundations are CLOSED. #40 safety must be preserved and precedes this
item by user instruction. #39 is **related**, not a reason to block all metadata work; metadata can
represent base descriptors before visual inherited-root editing is implemented. The older audit's
list of #33/#37/#39 relations is not a set of current hard blockers.
Regression gates: `UserControlDesignerTests`, `CustomUserControlPreviewTests`,
`DesignerRuntimeLayoutParityTests`, clipboard/recovery/codegen tests, and binary/source metadata
fixtures proving module initializers, constructors, attributes, converters, and callbacks do not run.

## #35 — Add Designer multi-selection and group editing

Source: [issue #35](https://github.com/ProGraMajster/ModernFormsNext/issues/35).
Baseline status: **PARTIAL** foundations, multi-selection itself missing.

[DesignerSelectionService](https://github.com/ProGraMajster/ModernFormsNext/blob/ba396f95adab82564a0681bc922096599ba8c1ca/ModernFormsNext.Designing/Hosting/DesignerSelectionService.cs)
has only `SelectedNode`; null means root selection. `DesignerSession`, mouse controller, outline,
PropertyGrid, selection adorner, and clipboard operations consume this canonical single-node
contract. Existing typed transactions can group multiple changes but do not supply a second
selection model. Preserve existing `SelectedNode` semantics additively as the primary selection.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| Select/manipulate multiple controls | PARTIAL | Ctrl/Shift selection, marquee, primary selection, group move/delete/copy/reparent and group adorners missing. |
| Preserve hierarchy and valid layout | PARTIAL | Single-node validation/parity exists; ancestor/descendant normalization, compatible parents, relative positions and alignment/distribution need group rules. |
| Safe mixed properties | PARTIAL | Single-node typed property edits exist; intersection of editable properties/mixed-value UI missing. |
| All group operations atomic and undoable | PARTIAL | #33/#34 provide transaction/clipboard primitives; complete group operations and selection replay remain to implement. |

Dependencies: #33/#34/#42 are CLOSED. No hard open prerequisite; earlier queue order is respected.
Regression gates: transaction and clipboard suites, parity, serialization/recovery, modifier/marquee
input, group failure rollback, mixed values, constrained/docked controls, nested parents, and DPI.

## #36 — Add Designer smart guides, snapping and configurable grid

Source: [issue #36](https://github.com/ProGraMajster/ModernFormsNext/issues/36).
Baseline status: **PARTIAL**. Queue prerequisite: #35.

Existing `DesignerCoordinateMapper` and `DesignerDpiCoordinateConverter` separate document units,
preview zoom, and device DPI. `DesignerLayoutProperties` supplies padded content geometry;
`DesignerSurfaceRenderer.DrawGrid` draws a fixed eight-logical-unit grid. The active mouse
controller applies pointer deltas directly; no configurable grid/smart-guide resolver or snap
toggle was found. Historical prose about "snapping math" must not be read as a complete snapping
implementation. Reuse these coordinate/layout paths and production text metrics for baselines.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| Precise alignment without manual coordinates | PARTIAL | Basic drag/resize exists; sibling/container edge, center, and baseline guides missing. |
| Repeatable equal spacing | PARTIAL | Equal-spacing hints/selection transforms missing. |
| Predictable nested snapping | PARTIAL | Nested padding/DPI geometry exists; Margin targets, stable priority/hysteresis and multi-selection require implementation. |
| Disable snapping for unrestricted movement | PARTIAL | Current movement is unrestricted; configurable grid/snap flags and temporary bypass need defined UI/input behavior. |

The user explicitly orders #35 first so group snapping uses the canonical group selection. #33/#42
are satisfied; #62 is not an inferred blocker. Gates: pointer transactions, capture cancellation,
nested/Dock/Anchor constraints, Margin/Padding, zoom and 100/125/150/175/200% transforms, zero/invalid
grid sizes, close target switching, group-relative geometry, and exact undo/redo.

## #37 — Complete Designer event-handler generation and code navigation

Source: [issue #37](https://github.com/ProGraMajster/ModernFormsNext/issues/37).
Baseline status: **PARTIAL**; no live dependency blocker.

The Events view, event-field double-click, handler-name persistence, delegate parameter generation,
and subscriptions exist. `DesignerPropertyGridState.TryCreateDefaultEventHandler` and
`CommitSelectedEvent` **already wrap model edits in transactions**. The issue's blanket statement
that transaction integration remains missing is outdated for those paths. However,
[DesignerFileService.EnsureEventHandlerMethod](https://github.com/ProGraMajster/ModernFormsNext/blob/ba396f95adab82564a0681bc922096599ba8c1ca/ModernFormsNext.Designer/Services/DesignerFileService.cs)
runs afterward, checks existing methods by name only, and the PropertyGrid merely logs its result.
There is no code-navigation service in `IDesignerHostEnvironment`, no compatible-method picker,
and no control-default-event surface activation. Binding/file success and diagnostics must be
coordinated without deleting user methods during unbind or undo.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| Create/assign without editing generated code | PARTIAL | Events workflow exists; control double-click and compatible existing-method selection missing. |
| Generated code compiles with no duplicate subscriptions | PARTIAL | Deterministic event map/subscriptions exist; signature compatibility, overloads, collision and missing-handler checks incomplete. |
| User code outside `.Designer.cs` | PASS | Handler insertion targets sibling user code; normal generator owns only generated file. |
| Broken bindings diagnosed without document corruption | PARTIAL | Identifier checks and file errors exist; file-failure/model consistency, missing/renamed methods and navigation diagnostics incomplete. |

Explicit #33 and #71 dependencies are CLOSED. #99 Ctrl+S is unrelated and excluded from this session.
Gates: existing round-trip/transaction/UserControl tests plus compiled delegate fixtures,
overloads/generics/namespaces, existing-method preservation, failed insertion, repeated generation,
undo/redo, and host navigation contract tests. Real Visual Studio navigation remains separate
interactive evidence.

## #81 — Extend safe .mfdesign reverse synchronization fidelity

Source: [issue #81](https://github.com/ProGraMajster/ModernFormsNext/issues/81).
Baseline status: **PARTIAL**, including a concrete diagnostic/import risk.

[CSharpDesignerParser](https://github.com/ProGraMajster/ModernFormsNext/blob/ba396f95adab82564a0681bc922096599ba8c1ca/ModernFormsNext.CodeGeneration/Reverse/CSharpDesignerParser.cs)
uses Roslyn syntax only. It already handles generated initialization, layout, enums, events,
known brushes/geometry/effects/transitions and selected initializers. Diagnostics already carry
line, column and syntax; source-range/recovery presentation is incomplete. #41 imports supported
external edits as one existing transaction and protects dirty/coalesced `.mfdesign` state.

`DesignerPersistenceCoordinator` parses generated text with default warning policy; parse success
can include warnings, then it discards successful-result diagnostics and automatically replaces
a clean active document. This needs a non-destructive warning/conflict audit. Some structured
initializer helpers ignore unsupported inner entries rather than reporting their exact spans.
No arbitrary C# evaluation should be introduced to improve fidelity.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| Published accepted syntax/construction subset and exclusions | PARTIAL | Conservative rules are documented; exhaustive bounded contract and recovery mapping missing. |
| More justified common initializer/value forms | PARTIAL | Safe special forms exist; choose additional forms from real generator/manual-edit fixtures. |
| Unsupported spans/recovery without partial mutation | PARTIAL | Line/column/syntax exist; warning-bearing import and silently ignored inner entries require correction. |
| Preserve comments/user code where ownership permits | PARTIAL | Normal save preserves sibling user file; full generated file is rewritten and model has no comment ownership representation. Define supported owned regions explicitly. |
| Transactions and external changes | PASS foundation | #33/#41 already integrate replacement; extend diagnostic/conflict semantics instead of adding another watcher/history. |
| Round-trip/malformed/unsupported/no-execution tests | PARTIAL | Existing round-trip, shape/effect and persistence suites cover the current subset; new syntax/recovery paths need fixtures. |

Related #33/#41/#42 are CLOSED, not blockers. Gates: repeated round trips, malformed syntax,
side-effecting expressions, unsupported nested initializers, mixed warnings/errors, clean/dirty
external edits, source-span fidelity, generated ownership, and runtime parity.

## #108 — Add safe declarative Designer command discovery and editing

Source: [issue #108](https://github.com/ProGraMajster/ModernFormsNext/issues/108).
Baseline status: **PARTIAL**; tooling catalog/editor missing, runtime already complete.

`CommandDesignerTests` and [command boundary](../commands.md#designer-boundary) establish inert
hidden/nonserializable `Command`, `CommandParameter`, `CommandTarget`, `InputBindings` and
`CommandBindings`. Safe model operations preserve ordinary control data and reject runtime object
graphs. Runtime sources/routing/async helpers from #56 must be consumed, not reimplemented.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| Explicit identities discovered without execution | PARTIAL | Runtime-only exclusion exists; data-only opt-in catalog missing. |
| Assign known command/parameters/targets/ordered bindings | PARTIAL | Runtime APIs exist; safe Designer metadata/editor missing. |
| Versioned safe representation before serialization | PARTIAL | Existing design schema is bounded; command-specific representation/migration contract missing. |
| Save/reopen/clipboard/history/codegen/reverse diagnostics | PARTIAL | Tests cover exclusion; supported declarative entries need complete integration. |
| Inert commands and runtime compatibility | PASS for current boundary | Preserve inertness while introducing declarative descriptors. |
| Declaration/fallback/migration docs and executable fixtures | PARTIAL | Code-behind fallback documented; declarative docs/fixtures missing. |

#56 CLOSED satisfies the runtime prerequisite. #40/#68 are related safety/metadata work and earlier
in the queue; reuse their catalog boundary. No need to finish #61 or #99. Gates: constructor/module/
getter/callback traps, version/identifier/reference validation, declaration changes, ordered binding
round trips, atomic model edits, compiled generated runtime behavior, and unchanged command tests.

## #75 — Add Designer serialization and preview for custom animation definitions and easing

Source: [issue #75](https://github.com/ProGraMajster/ModernFormsNext/issues/75).
Baseline status: **PARTIAL**.

Existing `DesignAnimationDefinitionDescriptor`, source discovery, `InteractionEffectDesignValue`,
`KnownEasingDesignValue`, built-in/custom opt-in effect metadata, editors and codegen/parser are
substantial foundations. General `AnimationDefinition` metadata can be discovered but is deliberately
not offered as an effect: production runtime has no general control-level activation collection.
The standard PropertyGrid dialog path already surrounds edits with shared transactions.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| General/custom definition metadata without execution | PARTIAL | Safe effect descriptors and general type discovery exist; activation/attachment contract missing. |
| Registered easing identifiers/bounded parameters | PARTIAL | Known easing names exist; custom registered identity/parameter policy missing. |
| Opt-in play/stop/restart with production scheduler/reduced motion | PARTIAL | Production scheduler/policy exists; Designer preview controls/session missing. |
| Time/resource bounds and unsupported placeholders | PARTIAL | Safe unsupported metadata behavior exists; preview limits/cancellation/disposal missing. |
| Transactions/undo-redo | PASS foundation | Shared dialog transactions exist; add specific animation/easing/preview lifecycle coverage. |
| Round trips/runtime equivalence | PARTIAL | Existing supported effect/transition tests; general definitions/custom easing missing. |

#28/#33 CLOSED. #40 safety is an explicit dependency and precedes this item. The older #28 constraint
calls for an explicit safe serialization/activation contract; design it before implementation and
preserve additive runtime compatibility. No arbitrary delegates/constructors may run in Designer.
Gates: `AnimationEffectDefinitionDesignerTests`, editor/transition tests, shared scheduler policy,
unsupported/failing/long-running definitions, reduced motion, repeat/restart, detach/dispose,
save/reopen/undo, and equivalent generated runtime behavior. Android scheduler evidence remains
distinct from Designer preview and device observations.

## #84 — Complete DynamicResource trimming/AOT and dictionary composition support

Source: [issue #84](https://github.com/ProGraMajster/ModernFormsNext/issues/84).
Baseline status: **PARTIAL**. No hard open dependency identified.

[Control.Resources](https://github.com/ProGraMajster/ModernFormsNext/blob/ba396f95adab82564a0681bc922096599ba8c1ca/ModernFormsNext/Control.Resources.cs) caches reflected `PropertyInfo`
by runtime type/property name; `ResourceReferenceBinding` invokes get/set and handles fallback and
runtime errors. Missing/non-writable properties already produce useful argument errors, but no
trimming metadata contract was found at that boundary. `ResourceDictionary` is a flat dictionary;
merged dictionaries/factories are absent. Preserve weak subscriptions, scoped lookup, exact types,
UI-thread setter rules, ThemeManager snapshot/rollback and atomic publication.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| Implement trimming/AOT property metadata contract | PARTIAL | JIT reflection path exists; annotations/explicit registration or generation strategy missing. |
| Actionable build/runtime unavailable-member diagnostics | PARTIAL | Runtime property validation exists; trimming-specific build/deployment diagnostics missing. |
| Merge precedence/cycles/change propagation | PARTIAL | Flat change hub exists; composition missing. |
| Factory lifetime/cache/null/error/UI-thread contract | PARTIAL | General values exist; bounded factory semantics missing. |
| Preserve ThemeManager notifications/atomic updates | PASS existing contract | Require unchanged tests plus merges/factory interactions. |
| JIT, trimmed and supported AOT publish/serialization/replacement | PARTIAL | JIT/resource/theme tests and Android sample AOT configuration exist; representative new trimmed/AOT resource publish evidence missing. |

#44 is related future multi-targeting, not a blocker. Do not expand the supported TFM matrix or
claim framework-wide AOT compatibility. Gates: `DynamicResourceTests`, `BrushResourceTests`, all
theming suites, custom inherited property registration, trim warnings, actual representative
trimmed/AOT publish and execution, merge removal/reparent/cycles, factory ownership and reentrancy,
failure rollback, serialization and runtime replacement. Android Release AOT configuration alone
does not demonstrate arbitrary reflection survives trimming.

## #38 — Add Designer project resource browser and asset management

Source: [issue #38](https://github.com/ProGraMajster/ModernFormsNext/issues/38).
Baseline status: **PARTIAL / BLOCKED for complete editor implementation** by actual contracts.

An image picker already enumerates up to 500 image files and displays relative list labels, but
[DesignerImagePickerDialog](https://github.com/ProGraMajster/ModernFormsNext/blob/ba396f95adab82564a0681bc922096599ba8c1ca/ModernFormsNext.Designer/Properties/DesignerImagePickerDialog.cs)
stores the full selected filesystem path; `DesignerPropertyDialogEditors.ImageLocation` persists
that raw value. Runtime PictureBox resolves paths/URLs directly. This is not a stable project
asset identifier, import/build-action manager, font/icon catalog, or Designer/runtime resolver.
Dynamic resource object keys alone do not supply an embedded/copy-to-output asset contract.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| PropertyGrid selection and preview | PARTIAL | Image-path picker exists; project catalog/import and bounded image/icon/font preview missing. |
| Projects remain portable after moving machines | BLOCKED | Shared stable asset identifier/loading/build-action contract must precede editors; current picker persists absolute paths. |
| Missing assets produce safe diagnostics | PARTIAL | Image errors/placeholder paths exist; catalog rename/incompatibility diagnostics missing. |
| Same identifier in Designer and runtime | BLOCKED | Shared runtime resolver and project-relative/embedded identity rules absent. |

Explicit #68 metadata/refresh dependency remains OPEN; #42 parity foundation is CLOSED. The user
orders this after resource stabilization (#84), but #84 dictionary composition is not itself the
missing asset URI/loading contract. Resolve that contract within the authorized resource work;
do not invent a Designer-only loader. Gates: relocation fixtures, embedded/content build assets,
missing/renamed/wrong-format inputs, safe imports, path containment, copy/save/codegen round trips,
Windows runtime and Android packaging parity where declared, and disposal/cancellation of previews.

## #39 — Add inherited Form and UserControl Designer support

Source: [issue #39](https://github.com/ProGraMajster/ModernFormsNext/issues/39).
Baseline status: **PARTIAL** foundations, visual inherited-root support missing.

Source discovery already traverses project-local UserControl inheritance, including abstract/generic
bases for concrete descendants. `DesignDocument` has `RootKind`, local root values/events and
`Controls`; `DesignControlNode.MemberVisibility` is generated field visibility, not inheritance
ownership. There is no base design identity, inherited-node ownership/locking, or property override
model. The generated partial class deliberately omits the user-authored base type. Parent custom
UserControl preview is an atomic component projection, not support for editing a derived root.

| Acceptance criterion | Baseline | Remaining work |
| --- | --- | --- |
| Derived form opens and edits visually | PARTIAL | Plain Form/UserControl roots work; safe custom-base root model missing. |
| Inherited controls displayed/protected | PARTIAL | Safe component projection exists; inherited ownership/visibility/lock policy missing. |
| Local controls added normally | PARTIAL | Ordinary insertion exists; mixed inherited/local hierarchy constraints need integration. |
| Generated code contains derived-owned changes only | PARTIAL | Separate user/generated ownership exists; inherited override emission and duplicate-tree prevention missing. |
| Reopen preserves complete inherited design | PARTIAL | Local model persists; base identity/version resolution, multilevel hierarchy and override persistence missing. |

No explicit open hard dependency is named in #39. #68 is related and earlier in the queue; #33/#42
provide completed history/parity foundations. Define additive base/local ownership and metadata
rules before editing. Gates: generated assembly fixtures for Form/UserControl multilevel bases,
private/protected/public members, abstract/missing/incompatible bases, constructor traps, base
changes/rebuild refresh, local insertion and override undo/recovery/clipboard, and no duplicate base
field construction/`Controls.Add` calls. Do not implement inheritance by copying the base tree into
the derived document's owned `Controls` array.

## Real ordering edges and remaining documentation corrections

| Edge | Reason and classification |
| --- | --- |
| #40 -> #68 | Explicit user ordering; metadata must preserve the completed safety boundary. |
| #35 -> #36 | Explicit user ordering; snap/group transforms need the canonical multi-selection contract. |
| #33/#71 -> #37 | Explicit issue dependencies, both CLOSED. No #99 dependency. |
| #33/#41/#42 -> #81 | Related completed infrastructure already present; no uncompleted blocker. |
| #56 -> #108 | Completed runtime foundation. #40/#68 are related reusable tooling contracts, not authorization for another runtime. |
| #28/#33/#40 -> #75 | Explicit foundations; #28/#33 CLOSED, #40 safety work OPEN. |
| #68 + stable runtime asset contract -> #38 | Explicit real prerequisites. #42 is CLOSED; #84 precedes by user ordering/resource stabilization but does not automatically supply asset loading. |
| #39 -> inherited-root parity cases | Follow-up coverage only; does not block existing #42 or all #68 metadata work. |

Do not turn all "related" links into hard blockers or introduce a #39/#68 dependency cycle.
None of these eleven issues is currently proven COMPLETE or reduced solely to unavailable manual
validation. The implementation gaps are independently actionable when their queue prerequisites
are met. No feature issue was closed or externally commented on during this audit.

Stale documentation to correct in the appropriate implementation phase, rather than silently
changing acceptance now:

- `docs/designer-animation-effects.md` says Designer-wide undo/redo does not exist; #33 and the
  current dialog transaction path supersede that statement.
- #37's remaining-transaction wording is too broad: model event edits are already transactional;
  handler-file coordination remains a real gap.
- `DesignDocument.Events` / `DesignControlNode.Events` XML remarks still describe future generation
  although subscriptions and handler insertion already exist.
- The roadmap's "Designer transaction/parity backlog" recommendation predates CLOSED #33/#42.
- Fixed grid and historical "snapping math" prose does not establish an implemented configurable
  smart-guide/snapping workflow in the current mouse controller.
- #56's original current-implementation paragraph predates its completed phase comments; latest
  comments, merged code, and CLOSED state are authoritative for the runtime prerequisite.

This file is the sole durable file changed by the series-2 audit worker. No tests were modified,
no build/test result is claimed, and no implementation commit/PR was created.
