# AGENTS.md — ModernFormsNext

## Project identity

ModernFormsNext is a code-first C#/.NET UI framework.

The goal of this repository is to build a modern, high-performance, WinForms-like desktop UI framework with full control over rendering, layout, input, styling, platform backends, and application startup.

ModernFormsNext is not a MAUI, WPF, WinUI, Avalonia, Uno, Blazor, Electron, or XAML project.

Core principles:

* Code-first UI.
* WinForms-like developer experience.
* No XAML.
* No dependency on native WinForms controls for framework UI.
* Custom rendering, primarily SkiaSharp-based.
* Lightweight controls.
* Clear public API.
* Strong documentation everywhere.
* Platform-neutral shared framework code.
* Platform-specific behavior isolated in backend projects.
* Windows is currently the primary and best-supported runtime target.

Treat this repository as a framework/library project, not as one application.

---

## Non-negotiable rules

Always follow these rules:

* Do not convert this framework to WPF, MAUI, WinUI, Avalonia, Blazor, Electron, or XAML.
* Do not add XAML.
* Do not replace the custom rendering architecture with native controls.
* Do not casually rewrite public APIs.
* Do not remove public members without approval.
* Do not move platform-specific code into shared framework projects.
* Do not add large dependencies without approval.
* Do not use `ModernFormsNext.DemoApp` as a playground.
* Do not treat generated/template projects as random test apps.
* Document new code thoroughly.

When unsure, prefer asking before making architectural or public API decisions.

---

## Required documentation discipline

Documentation is a first-class requirement in ModernFormsNext.

When adding or changing code, document it clearly. This project should be understandable for future contributors, users of the framework, and people reading the source code months later.

### Public API documentation

Every public or protected API should have XML documentation unless there is a strong reason not to.

This includes:

* public classes
* public structs
* public interfaces
* public enums
* public delegates
* public constructors
* public methods
* public properties
* public events
* public fields, if any exist
* protected members intended for derived controls
* extension methods
* options/configuration types
* event args
* renderer/style/theme abstractions
* backend abstraction interfaces

Use XML docs like:

```csharp
/// <summary>
/// Represents a clickable button control that can display text and react to pointer input.
/// </summary>
/// <remarks>
/// Use <see cref="Button"/> when an action should be triggered by the user.
/// The control raises <see cref="Control.Click"/> when it is activated.
/// </remarks>
/// <example>
/// <code>
/// var button = new Button
/// {
///     Text = "Save",
///     Width = 120,
///     Height = 36
/// };
///
/// button.Click += (_, _) => SaveDocument();
/// </code>
/// </example>
public class Button : Control
{
}
```

### What documentation should explain

Good documentation should explain:

* what the type/member does,
* when it should be used,
* what important side effects it has,
* whether it invalidates rendering,
* whether it triggers layout,
* whether it affects focus/input behavior,
* whether it is platform-specific,
* whether it is safe to call from background threads,
* whether overriding members must call `base`,
* what units are used, for example pixels, logical pixels, milliseconds,
* ownership/lifetime rules for disposable objects,
* nullability expectations,
* examples for non-trivial APIs.

### Internal code comments

Internal and private code does not need XML docs for every tiny helper, but non-obvious logic must be commented.

Add comments for:

* rendering decisions,
* layout algorithms,
* input routing,
* focus behavior,
* event order,
* platform interop,
* native resource lifetime,
* DPI/scaling behavior,
* caching,
* invalidation rules,
* dispatcher/threading behavior,
* compatibility behavior copied from WinForms-like semantics,
* workarounds for platform/runtime limitations.

Do not add useless comments like:

```csharp
// increment i
i++;
```

Do add comments like:

```csharp
// Layout can be requested while a previous layout pass is still active.
// Queue another pass instead of recursing immediately, otherwise nested controls
// can trigger an infinite layout loop.
_pendingLayout = true;
```

### Examples are expected

When adding important public APIs, include examples either in:

* XML `<example>` docs,
* README/docs,
* sample code,
* `ControlGallery`,
* template documentation,
* dedicated docs under `docs/`.

Prefer small, realistic examples.

Do not write examples using XAML.

### Documentation must stay honest

Do not document unsupported behavior as supported.

For example:

* Do not claim Android support is complete if it is not.
* Do not claim full cross-platform parity if Windows is currently the main target.
* Do not claim a control is production-ready if it is experimental.
* Mark experimental or incomplete APIs clearly.

Use wording such as:

```csharp
/// <remarks>
/// This API is currently primarily implemented by the Windows backend.
/// Other backends may ignore this value until platform support is added.
/// </remarks>
```

---

## Repository structure

Important areas:

### `ModernFormsNext/`

Main framework project.

This contains shared framework code such as:

* controls,
* forms,
* layout,
* rendering,
* themes,
* input,
* dialogs,
* drawing helpers,
* common UI primitives.

Most platform-neutral control logic belongs here.

Do not add Win32, Android, or other platform-specific implementation code here.

### `ModernFormsNext.WindowKit/`

Windowing and platform abstraction layer.

This area should contain platform-neutral abstractions and shared concepts such as:

* dispatcher abstractions,
* window abstractions,
* clipboard abstractions,
* cursor abstractions,
* storage/file picker abstractions,
* notification abstractions,
* platform-neutral window infrastructure.

Do not put direct Win32 calls here unless they are hidden behind proper abstractions and there is no better backend location.

### `ModernFormsNext.WindowKit.Backend/`

Shared backend infrastructure.

Use this for common backend bootstrap/platform infrastructure used by backend implementations.

### `ModernFormsNext.WindowKit.Backend.Windows/`

Windows-specific backend implementation.

Windows-specific code belongs here, including:

* Win32 interop,
* Windows message handling,
* Windows tray icon integration,
* Windows clipboard implementation,
* Windows notification implementation,
* Windows-specific window bootstrap,
* native handle management.

Do not leak Windows backend implementation details into public shared APIs unless explicitly designed as an abstraction.

### `ModernFormsNext.WindowKit.Backend.Android/`

Android backend area.

Treat this as platform-specific backend code. Do not assume Android behavior is identical to Windows.

### `ModernFormsNext.WindowKit.Backend.Tools.*`

Backend tooling, generation utilities, and interop-related tooling.

Be especially careful with generated code and native signatures.

### `ModernFormsNext.Templates/`

Project templates for users.

Templates are part of the user-facing framework experience. Keep them clean, minimal, documented, and beginner-friendly.

When framework startup changes, templates must be updated consistently.

### `samples/ControlGallery/`

Main manual visual test app for controls and rendering behavior.

Use this for:

* control demos,
* layout demos,
* visual states,
* theme checks,
* input/focus checks,
* manual rendering verification.

When adding a new visible control, update `ControlGallery` unless there is a clear reason not to.

### `samples/ModernFormsNext.DemoApp/`

This is not just a sample app.

`ModernFormsNext.DemoApp` represents the default application generated by the Visual Studio extension/template when a user creates a new ModernFormsNext app.

Treat it as a template/reference application.

Rules:

* Keep it clean.
* Keep it minimal.
* Keep it beginner-friendly.
* Keep it representative of the recommended app structure.
* Do not add experimental controls here.
* Do not add regression-test clutter here.
* Do not use it as a playground.
* Do not turn it into a control gallery.
* Do not add large demo pages unless they are intended to be part of the default template experience.
* If framework initialization changes, update this project and the Visual Studio template content together.

### `samples/Explorer/` and `samples/Outlaw/`

Example/manual validation applications.

Use them for broader real-world checks, but do not let them define framework architecture.

### `docs/`

Documentation, design notes, screenshots, usage guides, and architectural explanations.

When behavior changes in a way users need to know, update docs.

---

## SDK, solution, and build

The repository uses .NET SDK `10.0.201` through `global.json`.

Use this solution file:

```powershell
.\ModernFormsNext.slnx
```

Do not assume a `.sln` file exists.

### Restore

```powershell
dotnet restore .\ModernFormsNext.slnx
```

### Debug build

```powershell
dotnet build .\ModernFormsNext.slnx --configuration Debug --no-restore /p:EnableWindowsTargeting=true
```

### Release build

```powershell
dotnet build .\ModernFormsNext.slnx --configuration Release --no-restore --verbosity normal -m:1 /p:UseSharedCompilation=false
```

### Run ControlGallery

```powershell
dotnet run --project .\samples\ControlGallery\ControlGallery.csproj
```

### Run template/reference app

```powershell
dotnet run --project .\samples\ModernFormsNext.DemoApp\ModernFormsNext.DemoApp.csproj
```

Only use `ModernFormsNext.DemoApp` to verify the default user template experience. Do not use it for random control testing.

### Tests

If test projects exist, run:

```powershell
dotnet test .\ModernFormsNext.slnx --configuration Debug --no-restore
```

If no tests exist for the changed area, say that clearly and rely on build plus manual sample verification.

Do not invent missing test projects unless explicitly asked.

---

## Change discipline

Before editing:

* Inspect the existing implementation.
* Follow nearby patterns.
* Understand the framework layer you are editing.
* Keep the change focused.
* Prefer fixing the actual cause instead of adding workaround layers.
* Keep public API compatibility in mind.
* Think about documentation before finishing the task.

Do not:

* rewrite large parts of the framework without approval,
* rename public APIs casually,
* remove public members casually,
* move files between major projects without a strong reason,
* add unrelated formatting changes,
* run whole-repo formatting unless explicitly requested,
* bump package versions unless explicitly requested,
* change NuGet metadata casually,
* add new dependencies without approval.

---

## Public API rules

ModernFormsNext is a framework, so public API design matters.

When touching public APIs:

* Preserve WinForms-like naming where practical.
* Preserve WinForms-like semantics where practical.
* Prefer additive changes over breaking changes.
* Use nullable annotations correctly.
* Add XML documentation.
* Add examples for important APIs.
* Do not expose platform-specific implementation details from shared APIs.
* Do not leak backend-specific types into the main framework API.
* Keep names simple and understandable.
* Make APIs discoverable from IntelliSense.

Sensitive API areas include:

* `Application`,
* `Form`,
* `Control`,
* layout types,
* rendering types,
* drawing abstractions,
* event args,
* input types,
* focus/navigation types,
* theme/style types,
* `WindowKit` abstractions,
* backend bootstrap APIs.

If a breaking change is required, call it out explicitly.

---

## Coding style

Use modern C# and .NET 10 features where they fit, but follow the style of the file being edited.

General rules:

* Keep nullable reference types clean.
* Prefer readable code over clever code.
* Avoid unnecessary abstractions.
* Avoid unnecessary allocations in rendering/layout/input hot paths.
* Use existing helper types before creating new ones.
* Keep controls lightweight.
* Keep platform-specific code out of shared projects.
* Keep comments where they explain important behavior.
* Do not delete TODO comments unless the TODO is truly resolved.
* Do not mass-rename private fields or properties for style-only reasons.
* Do not create huge files if the existing architecture separates responsibilities.
* Do not introduce global mutable state unless the framework design already requires it.

Some existing code may intentionally follow WinForms/Modern.Forms-style patterns. Preserve those patterns unless the change requires otherwise.

---

## Rendering rules

Rendering is custom and SkiaSharp-based.

When changing rendering code:

* Do not replace SkiaSharp rendering with native controls.
* Avoid expensive allocations inside paint/render loops.
* Dispose temporary SkiaSharp objects correctly when ownership requires disposal.
* Respect cached resource lifetime.
* Be careful with bitmaps, images, paints, fonts, surfaces, canvases, and back buffers.
* Respect bounds, clipping, DPI/scaling, theme colors, opacity, enabled state, focus state, hover state, pressed state, selected state, and disabled state.
* Do not hard-code colors when existing theme/style systems should be used.
* Do not hard-code pixel sizes without considering DPI/scaling.
* Visual-only property changes should invalidate rendering.
* Layout-affecting property changes should trigger layout behavior, not only repaint.
* Document non-obvious rendering decisions.

For new visual behavior, prefer updating `ControlGallery`.

---

## Layout rules

Layout behavior is framework-critical.

When editing layout code:

* Preserve parent/child semantics.
* Preserve `Dock`, `Anchor`, `Margin`, `Padding`, `MinimumSize`, `MaximumSize`, `AutoSize`, and related behavior.
* Be careful with nested controls.
* Avoid recursive layout loops.
* Respect layout suspension/resume behavior.
* Do not call expensive layout work from render code unless existing design already does so.
* When a property affects layout, notify/invalidate layout using the existing project pattern.
* Document layout behavior and edge cases.

When adding layout-related public APIs, include examples.

---

## Input, focus, and events

When editing input, focus, or events:

* Preserve WinForms-like event order where possible.
* Respect `Enabled`.
* Respect `Visible`.
* Respect focus state.
* Respect hover/capture/pressed state.
* Respect keyboard navigation.
* Respect parent-child relationships.
* Do not swallow keyboard or pointer events unless required.
* Do not introduce global state for per-control input behavior.
* Avoid blocking the UI thread in event handlers.
* Document event order if it matters.
* Document whether events fire before or after state changes.

For new event args or events, add XML documentation and an example when useful.

---

## Threading and dispatcher

UI work should stay on the UI/dispatcher thread.

Rules:

* Do not block the dispatcher/message loop with long-running work.
* Do not use `Task.Run` as a random fix for UI-thread problems.
* Do not access controls from background threads unless the framework provides a safe pattern.
* Keep async code explicit and exception-safe.
* Prefer dispatcher-safe scheduling for UI updates.
* Document thread-affinity requirements.

If an API must be called on the UI thread, document that clearly.

---

## Platform boundary

Shared framework code must remain platform-neutral.

Do:

* Put Windows interop in `ModernFormsNext.WindowKit.Backend.Windows`.
* Put common backend contracts/services in `ModernFormsNext.WindowKit.Backend`.
* Put platform-neutral abstractions in `ModernFormsNext.WindowKit`.
* Keep `ModernFormsNext` focused on controls, layout, rendering, and framework behavior.

Do not:

* add Win32 P/Invoke to `ModernFormsNext/`,
* add Windows-only behavior to shared controls,
* make Android/backend code depend on Windows-specific behavior,
* expose backend implementation classes through public shared APIs,
* assume all platforms support every feature equally.

When a feature is currently Windows-only, document it.

---

## Interop and generated code

Be careful with:

* generated code,
* MicroCom-related code,
* Win32 interop,
* native signatures,
* platform backend partials,
* IDL-related files,
* backend generator/patcher tools,
* native handles,
* callbacks,
* unmanaged memory.

Rules:

* Do not manually edit generated files unless there is no generator path and the reason is documented.
* Prefer updating the generator/source definition when possible.
* Preserve native signatures.
* Preserve memory ownership rules.
* Be explicit about `IntPtr`, handles, disposal, lifetime, callbacks, and threading.
* Do not simplify interop code unless you understand the native contract.
* Add comments explaining non-obvious native behavior.

---

## Controls

When adding or changing controls:

* Keep API style close to WinForms where practical.
* Keep rendering consistent with the theme/style system.
* Keep layout behavior predictable.
* Add XML docs to public/protected APIs.
* Add examples for important properties/events.
* Add visible demos to `samples/ControlGallery`.
* Do not add control demos to `ModernFormsNext.DemoApp` unless they belong in the default user template.
* Ensure property changes invalidate rendering/layout as appropriate.
* Avoid making controls depend on platform backend internals.

For controls with complex behavior, document:

* state model,
* event order,
* rendering behavior,
* layout behavior,
* keyboard/mouse interaction,
* accessibility-related expectations if applicable.

---

## Templates and Visual Studio extension experience

The template experience is part of the product.

When editing template-related code:

* Keep generated code clean and minimal.
* Keep generated code beginner-friendly.
* Avoid experimental APIs in the default template.
* Keep startup code clear.
* Keep comments helpful but not noisy.
* Ensure generated projects restore and build.
* Keep `ModernFormsNext.DemoApp` aligned with the template output.
* Update documentation when the user-facing startup flow changes.

The generated application should show the recommended way to start a ModernFormsNext app.

---

## Samples and manual verification

Samples have different roles:

* `ControlGallery` is for control demos and visual/manual regression checks.
* `ModernFormsNext.DemoApp` is the default generated app/template reference.
* `Explorer` and `Outlaw` are broader example/manual validation apps.

When changing visible UI behavior:

* Prefer adding or updating a `ControlGallery` page/demo.
* Run the relevant sample manually on Windows when possible.
* Mention if manual visual verification could not be performed.

Do not let samples become the source of framework logic.

Framework logic belongs in framework projects.

---

## Packaging and metadata

This repository produces library/template packages.

Rules:

* Do not bump versions unless explicitly asked.
* Do not change NuGet metadata casually.
* Do not change package IDs casually.
* Do not change license information casually.
* Keep package readme/icon/license behavior working.
* Preserve MIT license and third-party attribution.
* If adding derived code from another project, update attribution/license documentation.

---

## Documentation files

When changing public behavior, update documentation.

Possible documentation locations:

* XML docs in source code,
* `README.md`,
* docs under `docs/`,
* comments near important implementation details,
* examples in `ControlGallery`,
* template comments,
* release notes if applicable.

Documentation should answer:

* What does this do?
* Why does it exist?
* How do I use it?
* What should I avoid?
* Is it platform-specific?
* Is it experimental?
* Does it affect rendering?
* Does it affect layout?
* Does it require UI thread access?
* Are there examples?

Do not leave important behavior only in your final chat response. Put durable documentation into the repository when appropriate.

---

## Error handling

When adding error handling:

* Prefer clear exceptions over silent failure.
* Use exception types that make sense.
* Include useful messages.
* Avoid swallowing exceptions unless the framework contract requires it.
* Document cases where failure is expected or ignored.
* Do not crash the UI thread for recoverable backend/platform issues unless existing behavior requires it.

For platform backend failures, include enough context to debug the platform-specific issue.

---

## Performance

This is a UI framework. Performance matters.

Be especially careful in:

* rendering loops,
* layout passes,
* input dispatch,
* hit testing,
* text measurement,
* image handling,
* resource caching,
* platform message handling.

Avoid:

* repeated allocations in hot paths,
* unnecessary LINQ in hot paths,
* repeated text measurement when caching is appropriate,
* repeated image decoding,
* blocking I/O on the UI thread,
* heavy reflection in normal UI paths.

Document performance-sensitive decisions.

---

## Accessibility and usability

Where applicable:

* Preserve keyboard navigation behavior.
* Preserve focus visibility.
* Keep text readable.
* Respect disabled states.
* Respect hover/pressed states.
* Avoid visual-only interactions when a keyboard path is expected.
* Do not remove accessibility-related hooks or future extension points casually.

If accessibility support is incomplete, document limitations honestly.

---

## Git and diff hygiene

Before finishing:

* Review the diff.
* Remove accidental debug code.
* Remove unrelated whitespace-only changes.
* Remove local machine paths.
* Do not commit build outputs.
* Do not commit `.user` files.
* Do not commit IDE caches.
* Do not commit generated binaries.
* Do not touch unrelated files.

Avoid touching:

* unrelated projects,
* solution structure,
* package metadata,
* generated files,
* licenses,
* global formatting,

unless the task requires it.

---

## Validation checklist

For normal code changes, run at least:

```powershell
dotnet restore .\ModernFormsNext.slnx
dotnet build .\ModernFormsNext.slnx --configuration Debug --no-restore /p:EnableWindowsTargeting=true
```

For release/package-related changes, also run:

```powershell
dotnet build .\ModernFormsNext.slnx --configuration Release --no-restore --verbosity normal -m:1 /p:UseSharedCompilation=false
```

For visible UI/control changes, run:

```powershell
dotnet run --project .\samples\ControlGallery\ControlGallery.csproj
```

For template/startup changes, run:

```powershell
dotnet run --project .\samples\ModernFormsNext.DemoApp\ModernFormsNext.DemoApp.csproj
```

Use `ModernFormsNext.DemoApp` only to verify the default generated application experience.

If a command cannot be run in the current environment, state that clearly.

---

## Expected final report after changes

When completing a task, report:

* what changed,
* which files were changed,
* what documentation was added or updated,
* which commands were run,
* build/test status,
* whether visual/manual verification was done,
* whether template verification was needed,
* any risks around public API, rendering, layout, threading, or platform-specific behavior.

Do not claim verification was done if it was not.

---

## Agent behavior

The agent should behave like a careful framework maintainer.

Default behavior:

* Make small, focused changes.
* Preserve architecture.
* Preserve public API unless asked otherwise.
* Document thoroughly.
* Add examples when useful.
* Prefer `ControlGallery` for UI demos.
* Preserve `ModernFormsNext.DemoApp` as the default generated app.
* Keep platform boundaries clean.
* Build before finishing when possible.
* Be honest about limitations.

When the request is ambiguous, choose the safest minimal change or ask for clarification before changing architecture.
