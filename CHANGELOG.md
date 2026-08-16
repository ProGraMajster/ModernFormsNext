# Changelog

All notable ModernFormsNext changes are documented in this file.

ModernFormsNext follows semantic versioning. Git tags use a `v` prefix, while NuGet package versions do not.

## [1.10.0] - Unreleased

ModernFormsNext 1.10.0 adds reusable vector geometry and Shape controls, animated layout and
layout-aware visual-state metrics, first-class UserControl design, an Android Choreographer-backed
animation runtime, and versioned offline release documentation. Windows remains the primary
supported runtime; Android remains experimental. See the
[full release notes](docs/1.10.0-release-notes.md) and
[migration guide](docs/migrations/1.9.0-to-1.10.0.md).

### Added

- Added `Shape`, `Ellipse`, `Circle`, `Line`, `Polygon`, `Polyline`, and `Path` controls plus
  reusable line, rectangle, ellipse, and path geometry with observable figures, segments, and point
  collections.
- Added scheduler-backed animated layout, layout-aware visual-state metric interpolation, and
  compatible Brush interpolation with deterministic fallback for incompatible values.
- Added Designer editors for layout transitions, visual-state transitions, ordered
  `InteractionEffects`, safe project-defined animation metadata, and structured vector geometry.
- Added first-class UserControl design roots, Visual Studio item templates, source-based custom
  UserControl discovery, and safe data-only nested preview.
- Added an experimental Android `Choreographer` frame source with lifecycle-aware timing, live
  animator-scale observation, and stable multi-touch pointer ownership.
- Added versioned release bundles for documentation sources, offline DocFX HTML, selected samples,
  and an aggregate SDK/reference archive tied to the exact release tag and commit.

### Changed

- Coordinated package, template, and Visual Studio extension versions are updated to `1.10.0`
  without changing package IDs, target frameworks, VSIX identity, publisher, or installation
  targets.
- Form and UserControl roots now share one Designer document/session pipeline. Project assemblies
  and arbitrary user code are not loaded for custom UserControl preview.
- Release publication now builds and validates NuGet and documentation artifacts before creating a
  GitHub Release or publishing packages.

### Fixed

- Fixed Designer container `Padding` so preview layout, selection, hit testing, guides, generation,
  and reload use the same padded content geometry as runtime layout.

### Compatibility

- The public API comparison with 1.9.0 found 40 added public types, 548 added concrete public or
  protected members, and no removed public identifiers or changed member signatures.
- Most applications only update references and rebuild. Code importing both `ModernFormsNext` and
  `System.IO` may need to qualify or alias `Path` after the vector `ModernFormsNext.Path` type is
  introduced.
- Existing immediate layout and `.mfdesign` documents remain compatible; new transitions are
  opt-in.

## [1.9.0] - 2026-08-02

ModernFormsNext 1.9.0 adds the shared paint, animation, and theme foundations used by the framework,
hardens editor input and Designer ordering, and keeps Windows as the primary supported runtime.
Android support remains experimental. See the [full release notes](docs/1.9.0-release-notes.md) and
[migration guide](docs/migrations/1.8.0-to-1.9.0.md).

### Added

- Added observable solid, linear, radial, and sweep Brushes with opacity, transforms, spread modes,
  observable gradient stops, a shared Skia factory, dynamic-resource integration, and Designer
  serialization.
- Added one monotonic, idle-aware UI animation scheduler with UI-thread callbacks, cancellation,
  pause/resume, owner/key replacement, reduced-motion policy, typed interpolation, Windows and
  experimental Android lifecycle integration, and repaint batching.
- Added `ThemeManager` with typed/inheritable definitions, strict versioned JSON, validation,
  diagnostics, atomic UI-thread apply/rollback, dedicated dynamic theme resources, built-in
  Light/Dark themes, and opt-in animated transitions.
- Added composable `AnimationDefinition` and `AnimationRun` APIs, sequence, parallel, timeline,
  keyframes, repeat, auto-reverse, custom definitions, visual-state transitions, target-local
  `RippleEffect`, and `PressScaleEffect`.
- Added a Designer collection editor and deterministic serialization/code generation for built-in
  interaction effects.

### Changed

- Theme commits refresh the active Normal, Hover, Pressed, Focused, or Disabled style across all
  open framework windows and coalesce visual invalidation into one platform repaint per window per
  commit or animation tick. Visual-only frames do not force layout.
- Theme transitions and interaction effects are explicitly opt-in. Generic panels, containers, and
  DataGridView controls no longer receive implicit click/ripple presentation merely because they
  participate in standard pointer input.
- Designer child arrays now have a documented ordering contract. Ordinary containers store front-
  to-back Z-order and code generation maps that order to runtime `Controls.Add`; flow, table, and
  tab containers preserve authored layout sequence.
- Updated all coordinated package, template, and Visual Studio extension versions to `1.9.0` while
  preserving package IDs, target frameworks, VSIX identity, publisher, and installation targets.

### Fixed

- Fixed theme changes that previously required hover, click, focus, or resize before some controls
  repainted with their newly resolved resources and active visual state.
- Fixed the animation opt-in boundary so it cannot suppress base editor input. TextBox, RichTextBox,
  MarkdownEditor, and editing controls hosted by Panel or DataGridView retain caret placement,
  selection, drag selection, double-click, focus, capture, and keyboard input.
- Fixed CRLF/UTF-16 pointer hit mapping for RichTextBox and MarkdownEditor so clicks and subsequent
  insertion use the expected source index.
- Fixed pointer/capture cleanup after mouse up, cancellation, focus loss, detach, reparent, and
  disposal without leaving a stale owner or stuck press scale.
- Fixed docked and overlapping child order so Designer preview, save/reload, generated C#, reverse
  sync, runtime layout, document-outline moves, `BringToFront`, and `SendToBack` agree.

### Dependency and warning cleanup

- Removed the vulnerable MessagePack 2.5.192 path and selected MessagePack 3.1.8 for Visual Studio
  extension builds.
- Replaced the broad `Microsoft.VisualStudio.SDK` metapackage with the specific Visual Studio
  contracts used by the extension and excluded unnecessary runtime assets.
- Updated Android HarfBuzzSharp to 14.2.1.1, removed XA0141, and applied compatible patch/minor
  dependency updates without changing target frameworks or dependency majors.
- Restore and Debug/Release solution builds are release-gated at zero warnings; NuGet vulnerability
  and package-content audits are part of the release validation.

### Compatibility

- No intentional public type removals, package/namespace renames, target-framework removals, or
  extension identity changes are included.
- `GradientBrush.GradientStops` now exposes the observable `GradientStopCollection`. Common
  collection syntax remains source-compatible, but consumers requiring the concrete `List<T>` type
  must update and compiled consumers must rebuild.
- Existing `FadeToAsync`, `TranslateToAsync`, `ScaleToAsync`, and `RotateToAsync` helpers remain
  source-compatible adapters over the shared scheduler.
- Theme JSON is strict and rejects unknown fields and unsupported values instead of partially
  accepting them. Existing `.mfdesign` documents remain compatible without a schema migration.

### Known limitations

- There is no general animated-layout subsystem; layout-affecting visual-state metrics switch
  discretely.
- Brush animation requires compatible brush kinds and gradient-stop structures.
- Designer support covers the built-in ripple and press-scale effects; custom effects/animations
  remain code-first, and there is no general effect-collection undo/redo transaction stack.
- Android remains experimental, may refresh motion preferences on foreground entry instead of via a
  live `ContentObserver`, and still requires device/emulator runtime validation.
- Shapes and AppShell/navigation are not included in 1.9.0.

### Packaging

- The coordinated release produces eight `1.9.0` NuGet packages and seven matching symbol packages;
  `ModernFormsNext.Templates` intentionally has no `.snupkg`.
- Package validation checks versions, dependencies, target frameworks, DLL/XML/PDB contents,
  README/icon metadata, Source Link symbols, archive path safety, and accidental build/IDE/APK/VSIX
  artifacts. The VSIX is validated separately in Debug and Release.

## [1.8.0] - 2026-07-17

ModernFormsNext 1.8.0 expands the framework and designer substantially while keeping Windows as
the primary platform. Android is introduced as an experimental shared-control vertical slice, not
as a production-ready platform.

### Added

- Added `CheckedListBox`, native `RichTextBox`, and SkiaSharp-rendered `ToolTip` controls, with
  ControlGallery examples and automated coverage.
- Added a native Markdown document pipeline with `MarkdownViewer` and `DocumentViewer`, including
  selection, links, images, tables, lists, footnotes, code blocks, syntax highlighting, viewport
  layout, and plain-text conversion.
- Added `MarkdownEditor` with source highlighting, formatting commands, undo/redo, editor/preview/
  split modes, synchronized scrolling, and host-controlled link and image insertion workflows.
- Added hierarchical dynamic resource dictionaries at application, window, and control scope.
  Resource lookup follows control, parent, window, then application order; live resource references
  update bound CLR properties when the winning value changes.
- Added designer docking and document-outline workflows, including outline search, display modes,
  collapse/expand actions, and control reordering between containers.
- Added WindowKit backend registration, platform service, dispatcher, and permission contracts that
  keep platform-specific implementations outside the shared UI projects.
- Added an experimental `net10.0-android` WindowKit backend, an Android smoke test, a shared
  Windows/Android sample, deterministic Android tooling, and automated backend tests.
- Added a reusable `SkiaControlSurface` so a platform host can lay out, render, hit-test, focus, and
  route input through a real ModernFormsNext control tree.
- Added architecture decision records and a staged framework roadmap covering resources, themes,
  localization, navigation, and optional feature packages.

### Changed

- Improved designer code generation and reverse parsing. Newly generated `.Designer.cs` files now
  assign the form through `Size`; the parser still accepts `ClientSize` from files generated by
  earlier releases, so existing projects can be opened and saved without a manual migration.
- Improved AltGr handling and text-editing shortcuts so international keyboard input is not
  mistaken for Ctrl/Alt commands or focus-navigation shortcuts.
- Improved font and inherited-style resolution, including safe lookup before visual attachment,
  cached SkiaSharp typefaces, and diagnostics for performance-sensitive style resolution.
- Expanded ControlGallery with the new controls and Markdown workflows, and made its Windows target
  explicit so solution builds select the correct desktop assets.
- Updated the framework, templates, published library packages, and Visual Studio designer
  extension to the coordinated `1.8.0` release version.

### Fixed

- Fixed Unicode text editing by keeping document positions in UTF-16 while converting explicitly at
  the RichTextKit layout boundary. Caret movement, hit testing, selection, rendering, Backspace, and
  Delete now preserve emoji, supplementary characters, combining text, and IME composition.
- Fixed local Markdown image reload/disposal so an in-flight asynchronous read does not keep an
  image or its temporary directory locked while the host replaces or removes the asset.
- Fixed Android pointer routing with stable multi-touch IDs, deepest enabled-control hit testing,
  independent capture, tap/click semantics, drag cancellation, and real `ScrollableControl`
  scrolling rather than an Android-only scroll model.
- Fixed Android hardware-key and screen-keyboard input, including surrounding text, selection,
  composition updates, committed text, code-point deletion, and activity recreation behavior for
  the shared text-control path.
- Fixed Android lifecycle, permission coordination, deployment, launcher resolution, diagnostics,
  and Visual Studio launch configuration for the repository samples.
- Fixed designer rendering and interaction on 100%, 125%, 150%, 175%, and 200% display scaling.
  Logical/device conversion is applied once for the surface, hit testing, drag and resize, grid,
  snapping, selection borders and adorners, resize handles, designer chrome, and SkiaSharp runtime
  previews.
- Fixed form-size round trips so logical sizes do not drift when code is parsed, regenerated, or
  edited on a High DPI display.
- Fixed duplicate native borders around Windows forms that draw ModernFormsNext-managed chrome.
- Fixed generated template content so `bin`, `obj`, IDE state, and temporary files cannot leak into
  `ModernFormsNext.Templates` packages.
- Fixed solution registration so designer tests remain in the intended solution folder and run in
  solution-level validation.

### Experimental

- Android 1.8.0 can build and run the repository's shared-control sample with SkiaSharp rendering,
  logical-pixel density conversion, basic layout and focus, multi-touch input, scrolling, hardware
  keys, IME text editing, lifecycle tracking, dispatching, and manifest-aware permissions.
- Android does not yet support general `Application.Run(Form)`, the full WindowKit windowing model,
  multiple framework windows, complete accessibility, native dialogs, clipboard, file pickers,
  drag and drop, or the complete set of platform services. APK and sample Release/AOT paths are
  exercised; general trimming and Android App Bundle/store publication are not release-supported.
- Android APIs, hosting structure, and runtime behavior may change. Android 1.8.0 is not recommended
  for production applications. See [Android platform status](docs/platforms/android.md).

### Documentation

- Added Android backend, development, permissions, `adb`, deployment, diagnostics, and
  cross-platform sample guides with an explicit Experimental status and known limitations.
- Added dynamic resource usage and architecture documentation, including lookup precedence,
  invalidation behavior, weak listeners, diagnostics, and current reflection/AOT considerations.
- Expanded designer architecture documentation for logical coordinates, High DPI rendering,
  SkiaSharp composition, and the `Size`/legacy `ClientSize` round-trip contract.
- Added public guidance for Markdown viewing/editing, Android development, framework architecture,
  roadmap items, and release packaging.

### Packaging

- Centralized shared NuGet authorship, company, repository, MIT license, package URL, README, icon,
  release notes, Source Link, repository publishing, and symbol-package metadata.
- Enabled XML documentation and `.snupkg` symbols for published library packages; template packages
  remain symbol-free because they contain project content rather than a library API.
- Marked tests, samples, playgrounds, generated applications, and smoke tests as non-packable while
  keeping only intended library and template projects publishable.
- Kept all existing NuGet package IDs, the VSIX identity and Product ID, publisher, and Visual
  Studio installation targets unchanged.

### Breaking changes

- No intentional public API removals, package renames, namespace moves, target-framework removals,
  or identity changes were found between `v1.7.0` and this release.
- `Size` is the canonical assignment emitted for forms by the designer. This is a generated-code
  behavior change rather than a runtime API removal; the reverse parser remains compatible with
  legacy `ClientSize` assignments.
- Android APIs are new in 1.8.0 and explicitly experimental. Consumers should not treat their
  current hosting shape as a stable production contract.

### Known limitations

- Windows remains the primary and best-supported runtime and designer platform.
- The Visual Studio designer is still evolving; `.mfdesign` remains the source of truth and reverse
  parsing intentionally supports only recognized generated-code patterns.
- Android limitations are listed in [Android platform status](docs/platforms/android.md), including
  the incomplete windowing/service model and the absence of a production release guarantee.

## [1.7.0] - 2026-07-02

Compared with [1.6.0], this release adds the first ModernFormsNext designer stack, Visual Studio
extension packaging, designer document serialization, code generation, reverse parsing, and
design-time metadata infrastructure while preserving the code-first runtime model.

Published packages and artifacts:

- `ModernFormsNext`
- `ModernFormsNext.Templates`
- `ModernFormsNext.WindowKit`
- `ModernFormsNext.WindowKit.Backend`
- `ModernFormsNext.WindowKit.Backend.Windows`
- `ModernFormsNext.Designing`
- `ModernFormsNext.CodeGeneration`
- `ModernFormsNext.Designer`
- `ModernFormsNextDesigner.vsix`

### Added

- Added `ModernFormsNext.Designing` with neutral `.mfdesign` document models, geometry types,
  design properties, JSON serialization, validation, selection services, metadata attributes,
  metadata reading, and document hosting primitives.
- Added `ModernFormsNext.CodeGeneration` with deterministic C# `.Designer.cs` generation,
  member visibility handling, nested `Controls.Add(...)` generation, designer hash metadata,
  and a conservative Roslyn-based reverse parser for supported generated-code patterns.
- Added `ModernFormsNext.Designer`, a reusable ModernFormsNext designer shell containing the
  toolbox, document outline, designer surface, property grid, output panel, status bar,
  settings, docking layout, runtime/placeholder rendering modes, auto-save, localization, and
  file services.
- Added `samples/ModernFormsNext.DesignerPlayground` as a standalone designer host/test app.
- Added `ModernFormsNext.VisualStudioExtension` and `ModernFormsNext.VisualStudioExtension.Vsix`
  for Visual Studio designer command registration, `.mfdesign` editor hosting, VSIX packaging,
  and Experimental Instance installation.
- Added `ModernFormsNext.VisualStudioDesignerHost`, the out-of-process designer host used by the
  Visual Studio extension.
- Added a `BrushEditDialog` for editing solid and gradient brushes with preview support.
- Added English and Polish designer/extension UI strings.

### Changed

- Bumped the shared package version from `1.6.0` to `1.7.0`.
- Updated the application template to reference `ModernFormsNext` `1.7.0`.
- Updated the application template to include `MainForm.cs`, generated `MainForm.Designer.cs`,
  and companion `MainForm.mfdesign`.
- Marked template designable files with `ModernFormsNextDesigner=true` and avoided
  `<SubType>Form</SubType>` so Visual Studio does not load the built-in WinForms designer.
- Updated installation, template, and architecture documentation for the new designer workflow.

### Fixed

- Fixed Visual Studio extension packaging so the VSIX contains the package asset, generated
  package definition, extension assembly, and designer host files.
- Fixed designable-file detection so ordinary Windows Forms files are not treated as
  ModernFormsNext designer files.
- Fixed designer document path canonicalization so opening `MainForm.cs`, `MainForm.Designer.cs`,
  or `MainForm.mfdesign` resolves to one active designer session.
- Fixed generated layout ordering for docked controls so `Dock` assignments are emitted before
  layout-affecting bounds where needed.

### Removed

- Removed the temporary Visual Studio extension test command from the command table.

### Compatibility Notes

- The designer is an MVP but uses the shared `ModernFormsNext.Designer` shell in both the
  playground and Visual Studio extension. It is intentionally not a separate WPF/WinForms UI.
- `.mfdesign` remains the designer source of truth. Reverse sync from `.Designer.cs` is available
  as a conservative parser API, not as automatic destructive merge behavior.
- The Visual Studio extension currently targets Visual Studio 2022/2026-compatible VSSDK ranges
  and uses an out-of-process ModernFormsNext host while the framework gets a more formal
  embeddable designer surface API.

### Validation

- Local Debug build completed successfully.

## [1.6.0] - 2026-06-27

Compared with [1.5.0], this release adds more WinForms-compatible controls and dialog APIs while expanding the framework's custom rendering surface. It introduces `GroupBox`, a highly customizable `Switch`, printing dialogs and print preview infrastructure, gradient text rendering, and template/package hygiene updates for the next published package set.

Published packages:

- `ModernFormsNext`
- `ModernFormsNext.Templates`
- `ModernFormsNext.WindowKit`
- `ModernFormsNext.WindowKit.Backend`
- `ModernFormsNext.WindowKit.Backend.Windows`

### Added

- Added `GroupBox`, a SkiaSharp-rendered container control for visually grouping related child controls.
- Added `GroupBox` caption customization, including caption font size, foreground color, background color, background brush/gradient, border color, border width, border radius, content background color, content background brush/gradient, border styling, AutoSize support, layout-aware display rectangle behavior, and accessibility exposure.
- Added `Switch`, a fully custom-rendered switch control with two-state Boolean mode and three-position `-1`, `0`, `1` mode.
- Added `Switch` activation and interaction options, including automatic toggling, drag support, drag-time value updates, pointer-position activation, cycle activation, keyboard activation, `IsToggled`, `Value`, `Toggled`, and `ValueChanged`.
- Added `Switch` visual customization for track color/brush/gradient, thumb color/brush/gradient, borders, corner radii, thumb size/inset, built-in icons, custom bitmap icons, icon colors, animation duration, animation speed, and easing.
- Added `Switch` renderer support for smoother rounded fills/borders and direct color interpolation for three-state transitions so `-1 -> 1` animations no longer flash through the neutral state color.
- Added gradient text rendering support through `Control.TextBrush` and renderer plumbing so controls can draw text with solid, linear, radial, sweep, or glass brush content where supported.
- Added printing dialog compatibility APIs, including `PrintDocument`, `PrintDialog`, `PageSetupDialog`, `PrintPreviewDialog`, `PrintPreviewControl`, `PrinterSettings`, `PageSettings`, paper/printer model types, print events, and platform conversion helpers.
- Added platform print dialog service contracts in `ModernFormsNext.WindowKit` and a Windows backend implementation for the system print dialog path.
- Added ControlGallery pages and examples for `GroupBox`, `Switch`, gradient text, and printing workflows.

### Changed

- Bumped the shared package version from `1.5.0` to `1.6.0`.
- Updated the packaged application template to reference `ModernFormsNext` `1.6.0`.
- Updated template and getting-started documentation examples to use `ModernFormsNext` `1.6.0`.
- Improved template package build hygiene so template source files are packaged as content without being compiled into the template package assembly.
- Improved compatibility behavior for component-style types such as `ImageList`, `NotifyIcon`, and `Timer`.

### Fixed

- Fixed three-state `Switch` animated color transitions so direct `-1` to `1` changes blend between the source and target states instead of deriving colors from the middle thumb position.
- Fixed jagged-looking `Switch` rounded fills and borders by drawing solid rounded fills directly and adjusting border radii for stroke width.
- Fixed `GroupBox` content fill clipping so content backgrounds and gradients stay inside the framed border.

### Removed

- No public APIs were removed in this release.

### Compatibility Notes

- This release is backward-compatible with `1.5.0` from a public API perspective. It is a minor release because it adds new public framework API.
- The printing surface includes both ModernFormsNext-rendered dialogs and a system-dialog path. The system path currently depends on backend support; the Windows backend provides the first implementation.
- `Switch` bitmap icon properties store `SKBitmap` references and do not take ownership of them. Keep assigned bitmaps alive while the control can render them and dispose them after they are no longer assigned.
- `GroupBox` shadow customization APIs are present, but ControlGallery keeps shadows disabled while the rendering behavior is refined.

### Validation

- Local restore completed successfully.
- Local Debug build completed successfully.
- Local Release build completed successfully.
- Local package generation completed successfully.
- Local `dotnet test` completed successfully.
- Local ControlGallery smoke run completed successfully.
- Local ModernFormsNext.DemoApp smoke run completed successfully.
- GitHub `.NET` workflow completed successfully for the release commit.
- GitHub `Release` workflow completed successfully for tag `v1.6.0`.
- NuGet public indexes show version `1.6.0` for all published ModernFormsNext packages.

## [1.5.0] - 2026-06-26

Compared with [1.4.0], this release focuses on WinForms compatibility, accessibility, and text rendering polish. It adds a new `MaskedTextBox` control, introduces a WinForms-style accessibility surface, exposes that accessibility model through the Windows MSAA backend, and fixes font-style rendering so controls honor bold, italic, and underline style information more consistently.

Published packages:

- `ModernFormsNext`
- `ModernFormsNext.Templates`
- `ModernFormsNext.WindowKit`
- `ModernFormsNext.WindowKit.Backend`
- `ModernFormsNext.WindowKit.Backend.Windows`

### Added

- Added `MaskedTextBox`, a full ModernFormsNext control built on top of the existing `TextBox` infrastructure.
- Added mask editing support backed by `System.ComponentModel.MaskedTextProvider`, including typed input, paste handling, deletion, selection replacement, prompt characters, literals, password display, overwrite mode, and validation.
- Added WinForms-compatible `MaskedTextBox` API surface for migration scenarios, including:
  - `Mask`, `MaskedTextProvider`, `MaskCompleted`, `MaskFull`
  - `TextMaskFormat`, `CutCopyMaskFormat`, `PromptChar`, `PasswordChar`, `UseSystemPasswordChar`
  - `AllowPromptAsInput`, `AsciiOnly`, `SkipLiterals`, `ResetOnPrompt`, `ResetOnSpace`
  - `InsertKeyMode`, `IsOverwriteMode`, `RejectInputOnFirstFailure`
  - `Culture`, `FormatProvider`, `ValidatingType`, `ValidateText`
  - `MaskInputRejected`, `TypeValidationCompleted`, `MaskChanged`, `IsOverwriteModeChanged`
- Added compatibility enums and event types used by `MaskedTextBox`:
  - `MaskFormat`
  - `InsertKeyMode`
  - `HorizontalAlignment`
  - `MaskInputRejectedEventArgs`
  - `MaskInputRejectedEventHandler`
  - `TypeValidationEventArgs`
  - `TypeValidationEventHandler`
- Added a `MaskedTextBox` page to ControlGallery with common examples such as phone numbers, dates, postal codes, license-style input, password-style masked input, and rejected-input feedback.
- Added a WinForms-style accessibility object model:
  - `AccessibleObject`
  - `AccessibleEvents`
  - `AccessibleNavigation`
  - `AccessibleRoles`
  - `AccessibleSelection`
  - `AccessibleStates`
  - `Control.ControlAccessibleObject`
  - `PlatformAccessibleObjectAdapter`
- Added accessibility-related control APIs and event plumbing so controls can expose accessible names, descriptions, roles, states, default actions, navigation, selection, and help.
- Added platform accessibility contracts in `ModernFormsNext.WindowKit`, including `IPlatformAccessibilityHost` and `IPlatformAccessibilityService`.
- Added a Windows MSAA bridge in the Windows backend so the ModernFormsNext accessibility model can be exposed to platform accessibility clients.
- Added WinForms-like help infrastructure:
  - `Help`
  - `HelpEventArgs`
  - `HelpEventHandler`
  - `HelpNavigator`
  - `HelpProvider`
- Added internal property-store support used by the new compatibility/accessibility surface.

### Changed

- Extended `TextBox` with protected editing hooks so derived controls can reuse rendering, caret movement, selection, focus, and clipboard routing while replacing text-editing behavior.
- Updated `TextBoxRenderer` to render the document display text, allowing derived text controls to show prompts, literals, password characters, and placeholders through the same rendering path.
- Improved font-style propagation through control styles, text measurement, text layout, and renderer paths.
- Updated font dialog/gallery behavior to reflect the improved font-style model.
- Updated several controls to participate in the new accessibility surface.
- Bumped the shared package version from `1.4.0` to `1.5.0`.

### Fixed

- Fixed `TextBoxDocument.MaxLength` so assigning a new value updates the backing maximum length correctly.
- Fixed text rendering paths so controls honor full font-style information more consistently.
- Fixed compatibility behavior for text display paths that need to distinguish raw text from rendered display text.

### Removed

- No public APIs were removed in this release.

### Compatibility Notes

- `MaskedTextBox` is intended to make WinForms migrations easier and intentionally includes several WinForms-compatible members that are currently compatibility stubs, such as `AcceptsTab`, `CanUndo`, `Undo`, `ClearUndo`, `ScrollToCaret`, and `WordWrap`.
- `MaskedTextBox.BeepOnError` is preserved as API, but ModernFormsNext does not yet expose a platform-neutral system-beep service. Rejected input still raises `MaskInputRejected`.
- Accessibility is now represented in shared framework code and exposed through the Windows backend. Other backend implementations may need additional platform-specific work before they expose the same accessibility behavior to native accessibility clients.
- The release is backward-compatible with `1.4.0` from a public API perspective. It is a minor release because it adds new public framework API.

### Validation

- Local restore completed successfully.
- Local Release build completed successfully.
- Local package generation completed successfully.
- Local `dotnet test` completed successfully.
- GitHub `.NET` workflow completed successfully for the release commit.
- GitHub `Release` workflow completed successfully for tag `v1.5.0`.
- NuGet public indexes show version `1.5.0` for all published ModernFormsNext packages.

[1.10.0]: https://github.com/ProGraMajster/ModernFormsNext/compare/v1.9.0...HEAD
[1.9.0]: https://github.com/ProGraMajster/ModernFormsNext/compare/v1.8.0...v1.9.0
[1.8.0]: https://github.com/ProGraMajster/ModernFormsNext/compare/v1.7.0...v1.8.0
[1.7.0]: https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.7.0
[1.6.0]: https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.6.0
[1.5.0]: https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.5.0
[1.4.0]: https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.4.0
