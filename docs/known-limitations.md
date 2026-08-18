# Known limitations

This page is the central index of current ModernFormsNext 1.10.0 limitations. It is intentionally
an index: follow the **Details** links for behavior, workarounds, and validation evidence. The
baseline is tag `v1.10.0` / commit `3d3c05ed17de18267a65050d6b1384da928c3e9d`, re-audited on
2026-08-18 against the code, automated tests, and GitHub issue state.

Windows is the primary supported runtime. Android is an experimental shared-control vertical
slice. A successful build or deterministic test does not imply emulator, physical-device, GPU,
accessibility, IME-vendor, or leak-profile validation.

## Classification

- **Type** distinguishes bugs, missing features, design/platform/validation/performance/tooling/
  compatibility limitations, intentional scope, and obsolete history.
- **Severity** describes product impact: Critical, High, Medium, or Low. It is not a promised
  delivery order.
- **Tracked** links an open issue. **Proposed** links the dated audit proposal; no issue was created
  by this documentation cleanup. **Intentional** means the behavior is an explicit safety or
  compatibility boundary rather than an implementation defect.

No active Critical limitation or confirmed current bug was found in the audited 1.10.0 areas.
Closed issues [#2](https://github.com/ProGraMajster/ModernFormsNext/issues/2),
[#11](https://github.com/ProGraMajster/ModernFormsNext/issues/11),
[#25](https://github.com/ProGraMajster/ModernFormsNext/issues/25)–[#29](https://github.com/ProGraMajster/ModernFormsNext/issues/29),
[#31](https://github.com/ProGraMajster/ModernFormsNext/issues/31), and
[#32](https://github.com/ProGraMajster/ModernFormsNext/issues/32) are treated as completed work,
not active limitations.

## Current limitations index

| ID | Area | Limitation | Type | Severity | Status / issue | Details |
| --- | --- | --- | --- | --- | --- | --- |
| DES-01 | Designer | No designer-wide undo/redo transaction history. | Missing feature | High | [Tracked #33](https://github.com/ProGraMajster/ModernFormsNext/issues/33) | [Designer architecture](designer-architecture.md#current-designer-limitations) |
| DES-02 | Designer | Clipboard editing is an in-session, single-control copy/paste/duplicate path; cut, a system clipboard contract, complete cross-document behavior, and transaction integration are absent. | Missing feature | High | [Tracked #34](https://github.com/ProGraMajster/ModernFormsNext/issues/34) | [Installation](installation.md#designer-keyboard-shortcuts) |
| DES-03 | Designer | No multi-selection, marquee selection, or group editing. | Missing feature | High | [Tracked #35](https://github.com/ProGraMajster/ModernFormsNext/issues/35) | [Designer architecture](designer-architecture.md#current-designer-limitations) |
| DES-04 | Designer | Existing grid/snapping math is not a complete smart-guide, equal-spacing, baseline, or configurable-grid workflow. | Missing feature | Medium | [Tracked #36](https://github.com/ProGraMajster/ModernFormsNext/issues/36) | [Designer architecture](designer-architecture.md#current-designer-limitations) |
| DES-05 | Designer | The Events view can persist bindings and generate a method, but default-event double-click, compatible-method selection, robust code navigation, and rename diagnostics are incomplete. | Missing feature | Medium | [Tracked #37](https://github.com/ProGraMajster/ModernFormsNext/issues/37) | [Designer architecture](designer-architecture.md#current-designer-limitations) |
| DES-06 | Designer | No project resource browser or stable asset-management workflow. | Missing feature | Medium | [Tracked #38](https://github.com/ProGraMajster/ModernFormsNext/issues/38) | [Designer architecture](designer-architecture.md#current-designer-limitations) |
| DES-07 | Designer | Visual editing of inherited custom Forms and UserControls is not implemented. | Missing feature | High | [Tracked #39](https://github.com/ProGraMajster/ModernFormsNext/issues/39) | [UserControls](user-controls.md#current-design-time-boundaries) |
| DES-08 | Designer | Arbitrary application code is not executed for preview. Project UserControls use data-only projections and unsupported executable controls use safe placeholders. | Known design limitation | High | Intentional safety boundary; broader isolation [tracked #40](https://github.com/ProGraMajster/ModernFormsNext/issues/40) | [Safe preview](designer-architecture.md#safe-embedded-usercontrol-preview) |
| DES-09 | Designer | Auto-save exists, but crash recovery and external-change conflict handling do not. | Missing feature | High | [Tracked #41](https://github.com/ProGraMajster/ModernFormsNext/issues/41) | [Designer architecture](designer-architecture.md#current-designer-limitations) |
| DES-10 | Designer/layout | Padding, order, DPI, Shape, animation, and UserControl paths have focused tests, but there is no comprehensive Designer/runtime parity suite. | Validation gap | High | [Tracked #42](https://github.com/ProGraMajster/ModernFormsNext/issues/42) | [Designer architecture](designer-architecture.md#current-designer-limitations) |
| DES-11 | Visual Studio integration | The VSIX HWND adapter still uses reflection for the runtime handle and lacks built-in View Designer/Shift+F7 and dependent-file automation. | Tooling limitation | Medium | [Proposed P4](audits/1.10.0-documentation-and-limitations-audit.md#p4-stabilize-the-visual-studio-designer-host-contract) | [Visual Studio integration](designer-architecture.md#current-visual-studio-integration-boundaries) |
| DES-12 | Custom controls | Toolbox discovery is source-only and refreshed by reopening the Designer; binary-only controls and source-discovered custom properties/events are not a complete metadata surface. | Tooling limitation | High | [Proposed P2](audits/1.10.0-documentation-and-limitations-audit.md#p2-complete-safe-custom-control-discovery-and-metadata) | [UserControls](user-controls.md#current-design-time-boundaries) |
| GEO-01 | Shape/Geometry | No arc segment, geometry group/boolean operations, SVG import/core path string, general Stretch contract, generic Control geometry clip, or graphical Bezier editor. | Missing feature | Medium | [Proposed P3](audits/1.10.0-documentation-and-limitations-audit.md#p3-extend-vector-geometry-and-path-designer-tooling) | [Shapes](shapes-and-vector-geometry.md#current-limitations) |
| GEO-02 | Shape/Geometry | Android shares the renderer and builds successfully, but physical-device visual, touch-hit, GPU, and cache profiling have not been recorded. | Validation gap | Medium | [Proposed P1](audits/1.10.0-documentation-and-limitations-audit.md#p1-establish-an-android-device-performance-and-reliability-matrix) | [Shapes](shapes-and-vector-geometry.md#current-limitations) |
| ANI-01 | LayoutTransition | Transitions are per-control, zero-area source/target rectangles snap, and normal ancestor clipping remains authoritative. | Known design limitation | Medium | Intentional current contract | [Animated layout](architecture/animated-layout.md#current-limitations) |
| ANI-02 | VisualStateTransition | Layout-aware interpolation covers padding and border widths; other layout metrics remain discrete. | Known design limitation | Medium | Completed [#26](https://github.com/ProGraMajster/ModernFormsNext/issues/26) defined this focused scope | [Visual-state metrics](architecture/layout-aware-visual-state-metrics.md) |
| ANI-03 | InteractionEffects | Custom delegate easing and general `AnimationDefinition` activation remain code-first; the Designer does not run a live effect preview. | Known design limitation | Medium | Completed [#28](https://github.com/ProGraMajster/ModernFormsNext/issues/28) provides the safe metadata subset; undo tracked by [#33](https://github.com/ProGraMajster/ModernFormsNext/issues/33) | [Designer effects](designer-animation-effects.md#current-limitations) |
| ANI-04 | Brush interpolation | Cross-kind gradient geometry, GlassBrush, NoBrush/null, empty-to-populated gradients, and custom/derived brushes switch discretely; color interpolation is sRGB. | Known design limitation | Medium | Completed [#27](https://github.com/ProGraMajster/ModernFormsNext/issues/27) defines the supported matrix | [Brush matrix](architecture/brush-interpolation.md#deliberate-limitations) |
| THM-01 | ThemeManager | No automatic ThemeManager reapply for OS theme changes and no theme-file hot reload; Android has no system light/dark theme provider. | Missing feature | Medium | Windows system integration overlaps [tracked #45](https://github.com/ProGraMajster/ModernFormsNext/issues/45) | [Themes](themes.md#known-limitations) |
| THM-02 | ThemeManager | No shared shadow rendering contract; line height and letter spacing are not honored globally. | Missing feature | Low | Untracked; keep with future theme/rendering work | [Themes](themes.md#known-limitations) |
| AND-01 | Android application host | No general `Application.Run(Form)`, `IWindowingPlatform`, `IWindowImpl`, or multiple framework windows. | Platform limitation | High | [Proposed P5](audits/1.10.0-documentation-and-limitations-audit.md#p5-complete-the-android-application-and-windowing-host) | [Android status](platforms/android.md#important-limitations) |
| AND-02 | Android services | Native dialogs, clipboard, file/folder pickers, drag/drop, WebView/media/native views, notifications, sharing, and several other WindowKit services are incomplete. | Platform limitation | High | Related [#20](https://github.com/ProGraMajster/ModernFormsNext/issues/20), [#57](https://github.com/ProGraMajster/ModernFormsNext/issues/57), and [#60](https://github.com/ProGraMajster/ModernFormsNext/issues/60) | [Android status](platforms/android.md#important-limitations) |
| AND-03 | Android accessibility | Shared accessibility objects do not yet provide complete Android semantics, UI automation, or screen-reader integration. | Platform limitation | High | [Tracked #59](https://github.com/ProGraMajster/ModernFormsNext/issues/59) | [Android status](platforms/android.md#important-limitations) |
| AND-04 | Android input/IME | The current TextBox path supports composition and Unicode editing, but control, keyboard, language, vendor-IME, focus-transfer, and candidate/caret coverage is incomplete. | Platform limitation | High | [Tracked #62](https://github.com/ProGraMajster/ModernFormsNext/issues/62) | [Android status](platforms/android.md#important-limitations) |
| AND-05 | Android lifecycle | The sample handles activity recreation, but there is no complete shared application activation, state-restoration, safe-area/inset, or host-independent lifecycle policy. | Platform limitation | High | [Tracked #63](https://github.com/ProGraMajster/ModernFormsNext/issues/63) | [Android status](platforms/android.md#important-limitations) |
| AND-06 | Android release quality | No recorded physical-device, 90/120 Hz, GPU, long-run, broad IME/accessibility, or profiler leak matrix; AAB/store and general trim-safety support are not declared. | Validation gap | High | [Proposed P1](audits/1.10.0-documentation-and-limitations-audit.md#p1-establish-an-android-device-performance-and-reliability-matrix) | [Android runtime validation](architecture/android-animation-runtime.md#manual-validation-checklist) |
| PLT-01 | Cross-platform backends | Windows is the only supported full runtime backend; Android is experimental and there are no supported Linux or macOS application backends. | Platform limitation | High | No active backend delivery issue; intentional current platform scope | [README status](../README.md#current-status) |
| REN-01 | Rendering/Skia | No explicit OpenGL/Vulkan/Metal/ANGLE backend selection or declared GPU-acceleration contract. | Performance limitation | Medium | [Tracked #46](https://github.com/ProGraMajster/ModernFormsNext/issues/46) | [Roadmap risks](roadmap/ModernFormsNext-Framework-Roadmap.md#known-cross-cutting-risks) |
| REN-02 | Rendering/Skia | Bounds-dependent shaders are created per rendering scope and the framework has no unified render/layout allocation budget or profiler overlay. | Performance limitation | Low | Diagnostics work [tracked #58](https://github.com/ProGraMajster/ModernFormsNext/issues/58) | [Paint and gradients](paint-and-gradients.md#current-limitations) |
| SER-01 | `.mfdesign` / code generation | `.mfdesign` is the source of truth. Reverse parsing accepts the generator's conservative subset and reports unsupported arbitrary expressions rather than evaluating or merging them. | Compatibility limitation | Medium | Intentional safety/round-trip contract | [Designer reverse sync](designer-architecture.md#current-designer-limitations) |
| TPL-01 | Templates/compatibility | The packaged starter template is Windows-only and the published libraries target .NET 10; Android needs an explicit activity/surface host. | Compatibility limitation | Medium | Older .NET tracked by [#44](https://github.com/ProGraMajster/ModernFormsNext/issues/44); Android host [proposed P5](audits/1.10.0-documentation-and-limitations-audit.md#p5-complete-the-android-application-and-windowing-host) | [Installation](installation.md#android) |
| REL-01 | Documentation/release | Browser rendering, interactive VS Designer, Marketplace publication, and Android device observation remain manual gates outside deterministic DocFX/package tests. | Validation gap | Low | Intentional manual release boundary | [Versioned documentation](releasing/versioned-documentation-artifacts.md#current-validation-boundaries) |
| REL-02 | Repository validation | Nested worktrees under `artifacts/` can contaminate project-enumeration tests; exact-SHA validation needs a clean external worktree and sequential build flags. | Tooling limitation | Low | Documented workaround; no product issue proposed | [Versioned documentation](releasing/versioned-documentation-artifacts.md#current-validation-boundaries) |
| RES-01 | Dynamic resources | Reflection-based property references need a trimming/AOT metadata strategy; merged dictionaries and factories are not implemented. | Compatibility limitation | Medium | Trimming compatibility relates to [#44](https://github.com/ProGraMajster/ModernFormsNext/issues/44) | [Dynamic resources](dynamic-resources.md#current-limits) |
| BND-01 | Data binding | No ModernFormsNext-native `BindingNavigator`; WinForms designer serialization hooks are intentionally not ported. | Missing feature | Medium | Untracked; not required by the current binding runtime | [Data binding](data-binding.md#current-limitations) |
| TXT-01 | RichTextBox | The portable RTF/editor subset omits OLE, protected ranges, URL activation, bullets/paragraph indentation rendering, custom tab stops, and native IME language-option behavior. | Compatibility limitation | Medium | Advanced input overlaps [#62](https://github.com/ProGraMajster/ModernFormsNext/issues/62) | [RichTextBox](richtextbox.md#compatibility-notes) |
| TXT-02 | Markdown/document editing | No keyboard focus/activation for individual links, native touch selection UI, stable runtime drag/drop image insertion, very-large-document virtualization, or full WYSIWYG editing. | Missing feature | Medium | Related [#55](https://github.com/ProGraMajster/ModernFormsNext/issues/55), [#57](https://github.com/ProGraMajster/ModernFormsNext/issues/57), and [#62](https://github.com/ProGraMajster/ModernFormsNext/issues/62) | [Markdown](markdown.md#compatibility-matrix), [MarkdownEditor](markdown-editor.md#current-limitations) |
| TIP-01 | ToolTip | `UseAnimation`, `UseFading`, and full inactive-window `ShowAlways` behavior are compatibility storage only. | Compatibility limitation | Low | Intentional source-compatibility surface | [ToolTips](tooltips.md#compatibility-notes) |
| LAY-01 | Layout/RTL | Flow and table layout do not have complete right-to-left behavior; related TODOs remain in the layout implementation. | Missing feature | Medium | Localization/RTL work relates to [#14](https://github.com/ProGraMajster/ModernFormsNext/issues/14) | [Roadmap](roadmap/ModernFormsNext-Framework-Roadmap.md#known-cross-cutting-risks) |

## Current status by subsystem

- **Completed and supported on the primary Windows path:** Shape controls and the documented
  geometry subset; shared scheduler; LayoutTransition; padding/border visual-state metrics;
  compatible Brush interpolation including different non-empty gradient-stop counts; ThemeManager;
  Form/UserControl design roots; `.mfdesign` serialization and generated C# for supported values;
  versioned tag/SHA-bound documentation archives.
- **Completed but intentionally bounded:** safe source-metadata effects, data-only UserControl
  preview, conservative reverse parsing, per-control layout transitions, and discrete Brush
  fallback for incompatible values.
- **Experimental:** Android shared-control hosting, animation frame pacing, lifecycle adapter,
  permissions, pointer routing, and TextBox IME integration.

See the [1.10.0 documentation and limitations audit](audits/1.10.0-documentation-and-limitations-audit.md)
for obsolete statements removed, issue coverage, proposed backlog items, and the TODO/FIXME review.
