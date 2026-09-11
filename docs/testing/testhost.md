# Deterministic headless TestHost

`ModernFormsNext.Testing` hosts real framework Forms and controls for automated application tests
without creating a native window. It provides production input routing, explicit layout, a
controllable animation clock, scoped platform services and optional off-screen rendering.
It complements Windows/Android native integration tests.

## Install

Reference the package from a .NET 10 test project:

```xml
<PackageReference Include="ModernFormsNext.Testing" Version="1.10.0" />
```

The package uses the shared runtime and WindowKit contracts. It does not load the Windows or
Android backend, Designer, or VSIX. New APIs described here are in the current source tree;
this development change does not publish a package or change its version.

## First test

Create the host before constructing a Form; the Form constructor acquires the scoped backend.
Run all UI operations and host disposal on that same thread. Serialize tests using the host.

```csharp
using ModernFormsNext;
using ModernFormsNext.Testing;
using Xunit;

using var host = ModernFormsTestHost.Create();
var root = new Panel();
var status = root.Controls.Add(new Label { Text = "Ready", Top = 60 });
var save = root.Controls.Add(new Button { Name = "save", Text = "Save" });
save.Click += (_, _) => status.Text = "Saved";
TestWindowHost window = host.Show(root, 400, 300);

window.Input.Click(save);
host.ProcessPendingWork();
Assert.Equal("Saved", status.Text);
Assert.Same(save, window.FocusedControl);
```

`Show(Control)` wraps an unparented control in an undecorated real Form. `Show(Form)` retains its
normal chrome configuration. `host.Input`, `host.FocusedControl`, `Resize` and `CaptureTree` select
the first hosted window; use each `TestWindowHost` explicitly for multi-window tests.
For a complete serialized xUnit fixture, see the [application test template](testhost-template.md).

## Architecture

The host scopes the existing window factory, UI dispatcher, application runtime, default animation
scheduler and platform registries. It does not copy layout, hit testing, focus, commands, animation scheduling,
resource resolution, data binding or rendering logic. The native handle remains zero with the
`HEADLESS` descriptor. Ordinary hosting exposes no framebuffer. Explicit capture temporarily
provides an `IFramebufferPlatformSurface` to the normal `WindowBase` paint callback.

There is one process-wide dispatcher/default scheduler, so only one host may be active in a
process. Concurrent creation fails. Scoped registries/factories revoke their references on
teardown, including references carried by an older captured execution context. A Form cannot
move between host scopes, and construction on a foreign thread is rejected.

## Input and focus

`TestInput` sends raw events through the backend's normal input callback. Window preview,
InputBinding resolution, hit testing, capture, focus and control handlers retain their normal
order. A control overload translates its current presentation center, including nested layout,
render transforms and scale, then sends a pointer event. An overlapping control can intercept it.
Hidden/disabled controls are never directly invoked by the helper.

- `Move`, `PointerDown`, `PointerUp`, `Click`, `DoubleClick`, `Wheel`, `Leave` and `LoseCapture`
  use production pointer paths. Point overloads use logical window-client pixels including managed
  chrome; negative/outside points are useful for capture tests. Wheel deltas retain existing
  framework units. Left, middle and right buttons are supported; unsupported buttons fail explicitly.
- `KeyDown`, `KeyUp` and `PressKey` use existing framework `Keys` and explicit modifier flags.
  The supported key set is the existing WindowKit-to-framework mapper. Unsupported keys fail
  before dispatch. A key-down does not guess the character produced by a keyboard layout.
- `TextInput` sends committed Unicode text separately. Empty text is a no-op. AltGraph modifiers
  remain distinct from command gestures. This does not emulate native IME composition.
- `SetComposingText`, `CommitComposition`, `FinishComposition` and `CancelComposition` call the
  real focused editor's [shared text-service session](../text-input.md). Retain `TextInputClient`
  to verify rejected late callbacks after focus, modal/popup or host retirement. These semantic
  operations do not simulate native language profiles, candidate UI or physical keyboards.
- `Tab(backwards: true)` sends Shift+Tab; the forward default sends Tab. The helper supplies the
  production translated Tab text event only when KeyDown was not handled. A text control that
  accepts Tab retains its own behavior.
- `Focus(control)` uses canonical `Control.Select`; `window.FocusedControl` reads the actual
  adapter focus owner. Each window has independent focus. OS foreground activation is not emulated.

A convenience `PressKey` releases a consumed key even if an application command throws. A failed
`Click` cancels capture through the production capture-loss path, without inventing a successful
click after a failed press. Primary and cleanup failures are both reported when necessary.
Low-level Down/Up methods remain separate operations for gesture and repeat tests.

No helper implicitly advances time. Consecutive clicks within the controlled production interval
can count as double clicks; advance `host.Clock` to test the exclusive 500 ms boundary. Native
windows use monotonic time for the same recognition algorithm.

## Modal forms

`host.ShowDialog(dialogForm, ownerWindow)` runs the existing `Form.ShowDialog` implementation and
returns a normal `TestWindowHost`. Its `DialogCompletion` is the original result task; ordinary
windows have no dialog task. Use the dialog window's input helper while its owner is disabled.

```csharp
var owner = host.Show(new Form());
var dialogForm = new Form();
var dialog = host.ShowDialog(dialogForm, owner);
Assert.False(dialog.DialogCompletion!.IsCompleted);
dialogForm.DialogResult = DialogResult.OK;
Assert.True(dialog.DialogCompletion.IsCompletedSuccessfully);
Assert.Equal(DialogResult.OK, await dialog.DialogCompletion);
```

All operations remain on the creating thread. Await only after the task has completed, or use your
test runner's supported UI-thread scheduling. A canceled production close retains the modal owner
and pending task. Explicit test-window cleanup is authoritative and finishes even a canceled close.
Nested dialogs close before their owner during host cleanup. A result assigned before showing
completes without displaying the dialog, matching `Form.ShowDialog`. This checks shared modality
and focus ownership; native OS activation and foreground rules still need native integration tests.

## Control popups

Open a control's popup through ordinary input, then inspect `window.ActivePopup` or `window.Popups`.
These handles refer to real `PopupWindow` instances using the existing popup positioning and
input contracts. They expose their own input, canonical focus, layout, tree and image capture:

```csharp
window.Input.Click(comboBox);
TestPopupHost popup = window.ActivePopup!;
popup.LayoutUntilStable();
using RenderedSnapshot dropdown = popup.CaptureRenderedSnapshot();
popup.Input.Click(new System.Drawing.Point(12, 12));
```

Coordinates are logical pixels relative to the popup. `Hide` retains a popup for normal control
reuse; `Close` destroys it. The owner cleans up every created popup, including one never inspected
by a test. `Popups` includes live hidden popups; `ActivePopup` follows the framework's active-popup
state. Hidden popups cannot receive input or be rendered. Window-manager activation, native
shadows and cross-application focus still require native tests.

## Deterministic dispatcher

`Run` and `Invoke` execute immediately on the owner thread. `Post` and `InvokeAsync` enqueue work
in the production dispatcher. `Drain`, `WaitForIdleAsync` and `ProcessPendingWork` execute ready
work in production priority/FIFO order.

A drain has a default 4096-operation limit. A perpetually replenishing queue fails instead of
hanging. Exceptions in posted fire-and-forget work are captured in `UnhandledExceptions`;
`ThrowUnhandledExceptions` reports them as an aggregate. `InvokeAsync` reports its own exception
through its returned task.

Production dispatcher timers can have inactive operations waiting for a future clock time.
`PendingWorkCount` includes those queued operations; idle means no **ready** work. Draining does
not advance time or run dormant timers early. Neither idle nor a dispatcher checkpoint means that
arbitrary application I/O, thread-pool continuations or async business operations have finished.

### Test the real application loop

`Application.Run` uses this same production dispatcher with its controlled run-loop backend.
Create the host first and queue a close or exit action before entering the loop:

```csharp
using ModernFormsNext;
using ModernFormsNext.Testing;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

using var host = ModernFormsTestHost.Create();
using var form = new Form();
int exits = 0;
Application.OnExit += (_, _) => exits++;
host.Dispatcher.Post(Application.Exit);
Application.Run(form, ApplicationLifetimeMode.Explicit);

Assert.Equal(1, exits);
Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
Assert.Empty(host.Dispatcher.UnhandledExceptions);
```

This exercises actual startup, lifetime policy, lifecycle callbacks, cancellation and shutdown.
The backend runs ready work one operation at a time and checks cancellation between operations.
It shares a 4096-operation budget across nested frames and allows at most 64 nested frames. A loop
that runs out of ready work before exit fails with a diagnostic; it never waits or advances time
automatically. Queue explicit `host.Clock.Advance` work if a scenario needs elapsed time.
`Application.Run` is still allowed only once in a host's runtime; create a new host for another run.

## Layout, viewport and scale

`PerformLayout` runs the real Form adapter, client owner and descendant layout paths.
`LayoutUntilStable` drains ready work and repeats geometry snapshots until stable, with a default
16-pass limit and a tree dump on failure. `ProcessPendingWork` additionally consumes recorded
invalidations; it does not paint or advance time.

```csharp
using var host = ModernFormsTestHost.Create(new TestViewport(800, 600, 1.25));
TestWindowHost window = host.Show(new Panel());
window.Resize(1024, 768);
window.SetRenderScale(2);
window.LayoutUntilStable();
ControlTreeSnapshot tree = window.CaptureTree();
```

Viewport dimensions and control bounds are logical pixels. `DeviceBounds` is an edge-rounded
structural diagnostic projection. Raster dimensions instead follow production positive-dimension
truncation after scaling, so fractional DPI can produce different edge rounding. Neither reads a
physical monitor. Geometry snapshots are detached and survive later mutations/disposal.

## Controlled time and animations

`host.Clock.CurrentTime` starts at zero. `Advance(TimeSpan)` drives the real scoped default
`AnimationScheduler` and production dispatcher timers on the UI thread, without a thread or sleep.
It first drains ready work at the old time, commits the new time, promotes due timers, processes an
animation tick and drains ready work. Negative, overflowing and reentrant advances are rejected.

```csharp
using ModernFormsNext.Animations;

AnimationScheduler.Default.Start(save, "opacity", progress => save.Opacity = progress,
    new AnimationOptions { Duration = TimeSpan.FromSeconds(1), Easing = Easings.Linear });
host.Clock.Advance(TimeSpan.FromMilliseconds(500));
Assert.Equal(0.5f, save.Opacity);
host.Clock.Advance(TimeSpan.FromMilliseconds(500));
Assert.Equal(1f, save.Opacity);
```

A large advance represents a delayed frame, not every intermediate frame. Periodic dispatcher
timers coalesce missed ticks and schedule their next interval from the target time; resolution is
whole milliseconds. Zero-interval/self-replenishing work is bounded by the dispatcher limit.
The scheduler retains its cancellation, replacement, fault, pause and owner-lifetime rules.
Framework-owned sequence, parallel, repeat, timeline and theme completion steps return to the
same scheduler/dispatcher before an advance finishes. The next positive-duration leg starts at
the current frame time; a large advance does not distribute leftover time across later legs.
Unrelated clocks, native timers, `Task.Delay`, or a separately constructed scheduler remain outside
this clock. Arbitrary user async continuations are not an idle guarantee.

## Controlled existing platform services

`host.Services` scopes controlled implementations of the same contracts production code consumes:

- `Clipboard` implements `IClipboard`; normal framework copy/paste never accesses the OS clipboard.
  It copies string, byte-array, string-array and supported immutable scalar values. Unsupported CLR
  object graphs fail atomically. It does not serialize/deserialise arbitrary application objects.
- `Lifecycle` uses the same `PlatformApplicationLifecyclePublisher` as production adapters. It
  starts in Running/Foreground, active, with one host and generation 1. Its inherited `Publish`,
  `Activate`, `RequestSaveState` and `RestoreState` methods drive normalized phase, activation and
  explicit state handoff. `SetState` remains the convenience helper for existing
  Unknown/Foreground/Background/NoHost states and production scheduler pause/rebase.
- `ThemeSettings` supplies preferences read on the next normal ThemeManager apply. It does not
  simulate future platform appearance-change notifications.
- `AnimationSettings.SetPreferences` publishes production reduced-motion, enabled and duration-scale
  preferences. Defaults are enabled, no reduced motion and scale 1.
- Screen size/scaling already comes from the headless viewport. The platform dispatcher uses the
  same deterministic UI dispatcher.

DataBindings, BindingSource, dynamic resources, Application.Resources and ThemeManager continue
using their production implementations. Follow their usual binding activation and UI-thread rules;
no special binding engine or per-control resource test listener is installed.

### Lifecycle, host replacement and insets

Applications consume `Application.Lifecycle`; tests can supply backend transitions through
`host.Services.Lifecycle`. Public facade `DeliverActivation`, `SaveState` and `RestoreState` use
that same scoped provider. There is no parallel test activation or persistence runtime.

```csharp
using ModernFormsNext.WindowKit.Backend.Lifecycle;

var lifecycle = Application.Lifecycle;
PlatformActivationKind? received = null;
lifecycle.ActivationReceived += (_, e) => received = e.Activation.Kind;
host.Services.Lifecycle.Activate(new PlatformApplicationActivation(
    PlatformActivationKind.Protocol, uri: new Uri("example://document/42")));
Assert.Equal(PlatformActivationKind.Protocol, received);

var current = host.Services.Lifecycle.Snapshot;
host.Services.Lifecycle.Publish(new PlatformApplicationLifecycleSnapshot(
    PlatformApplicationPhase.Suspended, PlatformApplicationLifecycleState.NoHost,
    hostGeneration: current.HostGeneration));
host.Services.Lifecycle.Publish(new PlatformApplicationLifecycleSnapshot(
    PlatformApplicationPhase.Running, PlatformApplicationLifecycleState.Foreground,
    isActive: true, hostCount: 1, hostGeneration: current.HostGeneration + 1));
```

Use explicit versioned `PlatformApplicationStateData` to test save/restore as described in the
[application lifecycle guide](../application-lifecycle.md). These publications test shared
application semantics; they do not create, destroy or recreate an Android Activity. Publishing
`Exiting` or `Exited` requests graceful exit of the current application runtime, including an
active `Application.Run` loop. Reentrant exit cleanup occurs after the current notification;
drain ready work when testing it outside a running loop.

`window.SetActive(bool)` sends the ordinary backend window-activation callback. It changes that
window's `IsActive`, independently of the provider's application `Snapshot.IsActive`. A popup or
window focus test does not emulate an operating system's foreground policy.

`window.SetInsets(new WindowInsets(...))` supplies the backend's inset feature and raises the real
Form `InsetsChanged` event. Window insets are informational. `SkiaControlSurface.Insets.SafeArea`
constrains its borrowed root through normal layout, preserving root padding; `Ime` remains an
application keyboard-avoidance policy. Values are logical pixels and do not read a physical
display. See the lifecycle guide for native fitting and density rules.

## Rendering and snapshots

```csharp
using RenderedSnapshot pixels = window.CaptureRenderedSnapshot();
File.WriteAllBytes("actual.png", pixels.EncodePng());
Assert.Equal(2048, pixels.PixelWidth); // 1024 logical pixels at scale 2
```

Capture stabilizes layout, then executes one normal paint callback. It includes Form chrome where
configured, keeps the same control hierarchy, does not advance time, and leaves work posted by
painting pending. A reentrant capture of the same window fails. The default/max framebuffer budget
is 16,777,216 device pixels; callers can select a smaller positive budget. This bounds the capture
buffer, not allocations in arbitrary application controls or their production backbuffers.

The caller owns the disposable detached `RenderedSnapshot`. Pixel reads and PNG encoding can run
on a background thread; it retains no control, host or native resource. `GetPixel` returns straight
alpha; `CopyPixels` returns an independent BGRA8888 premultiplied byte copy. Snapshot pixels survive
window/host disposal. Captured images depend on actual fonts/Skia/platform rasterization; there is
no automatic golden-image comparison or cross-machine pixel identity promise. Use geometry and
state assertions when font variability matters.

## Diagnostics and cleanup

`GetDiagnostics` includes detached trees, pending queue/invalidation counts, active animations,
focused control names, captured dispatcher exceptions and bounded recent input kinds. Input history
is capped at 64 entries per window and excludes key values and committed text. Explicit screenshots
may contain application data; callers decide when and where to save them.

Closing a TestWindowHost authoritatively cleans up its owned tree, even if a normal Form close is
canceled or throws. Host disposal closes all windows, cancels owned scheduler work, restores the
active theme/application resources, revokes service/factory scopes and restores the prior default
scheduler/dispatcher. It also restores borrowed application loop/lifetime state, `OpenForms`,
`OnExit` subscribers, global input/command bindings, active menu/popup references, the lifecycle
facade and synchronization context. Scoped exit never releases the borrowed application's bindings.
If disposal occurs inside a Run callback, the local loop is canceled before restoration; its later
cleanup cannot exit the restored runtime.

Cleanup proceeds through independent steps and reports failures. Retained host/input/service APIs
and the scoped lifecycle facade fail after disposal; their subscriptions and captured service
references are revoked. Detached tree/image/lifecycle diagnostics remain usable under their own
ownership rules. Create a new host rather than reuse controls or a facade from an expired scope.

## Phase 1 boundaries

Phase 1 was merged in PR #93 and must not be reimplemented. The current continuation adds input,
clock, rendering and existing-contract service integration on that foundation. The lifecycle
continuation (#63) adds shared phase/activation/restoration and real application lifetime tests.
Future touch/drag helpers, navigation (#12) and shared virtualization (#55) coverage require their
production contracts. Shared modal/popup behavior is testable; this package
does not emulate OS foreground behavior, IME services, platform accessibility, GPU or native view
hosting.

Windows native HWND/UIA/automation tests, Android emulator checks, physical-device validation,
TalkBack, and manual Visual Studio/visual assessment remain separate evidence. See the
[session acceptance report](../development/codex-autonomous-issue-run.md) for actual executed checks
and remaining work on [issue #64](https://github.com/ProGraMajster/ModernFormsNext/issues/64).
