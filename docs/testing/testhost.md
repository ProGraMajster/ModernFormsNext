# Deterministic headless TestHost

`ModernFormsNext.Testing` is the supported application and layout testing package for
ModernFormsNext. Phase 1 hosts real framework `Form`, `UserControl`, `Panel`, and other `Control`
trees without creating a visible desktop window. It is intended for deterministic unit and
component tests; it complements rather than replaces Windows, Android, accessibility, GPU, or
other platform end-to-end validation.

## Install

Reference the package from a .NET 10 test project:

```xml
<PackageReference Include="ModernFormsNext.Testing" Version="1.10.0" />
```

The package depends on the platform-neutral ModernFormsNext runtime and WindowKit contracts. It
does not depend on Designer, VSIX, Android, or the Windows native backend.

## First test

Create the host before constructing a `Form`, because the constructor acquires its window
implementation inside the current host scope:

```csharp
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Testing;
using Xunit;

using var host = ModernFormsTestHost.Create();

var form = new Form
{
    Name = "MainForm",
    UseSystemDecorations = true
};
var button = new Button
{
    Name = "save",
    Text = "Save",
    Dock = DockStyle.Bottom,
    Height = 32
};
form.Controls.Add(button);

TestWindowHost window = host.Show(form, width: 400, height: 300);
window.PerformLayout();

Assert.Equal(400, window.CaptureTree().Bounds.Width);
Assert.Equal(32, button.Bounds.Height);
```

`Show(Control)` supports an unparented `UserControl`, `Panel`, or other control root. The host
attaches it to an internal undecorated real `Form`; normal parent/child ownership, visibility, and
layout still apply. One host can own multiple windows, and `Close()` closes all of them.

## Architecture

The host replaces two process entry points for the lifetime of one scope:

- a minimal WindowKit `IWindowImpl` records logical client size, render scale, invalidation, and
  lifecycle callbacks without allocating a native handle or rendering surface;
- a deterministic implementation is installed behind the production `Dispatcher.UIThread`
  queue and executes posted work only during an explicit drain.

Everything above those seams remains production code: `Form.Show`, `Application.OpenForms`, the
control tree, `PerformLayout`, Dock/Anchor, Padding/Margin, invalidation propagation, data binding,
dynamic resources, ThemeManager, and control-owned animation cancellation. TestHost does not
contain a second layout algorithm.

The window handle is always zero with the diagnostic descriptor `HEADLESS`, and the backend
exposes no surface. Logical DPI does not query a monitor.

## Deterministic dispatcher

`UiTestDispatcher.Run` and `Invoke` execute immediately on the thread that created the host.
`Post` and `InvokeAsync` enqueue work in the real ModernFormsNext dispatcher queue. Call
`Drain()`, `WaitForIdleAsync()`, or `host.ProcessPendingWork()` to execute it.

```csharp
var calls = new List<int>();
host.Dispatcher.Post(() => calls.Add(1));
host.Dispatcher.Post(() => calls.Add(2));

host.Dispatcher.Drain();

Assert.Equal(new[] { 1, 2 }, calls);
```

Fire-and-forget failures are available through `UnhandledExceptions`; call
`ThrowUnhandledExceptions()` to fail a test with one aggregate. A drain has an operation limit
(4096 by default), so a callback that continuously replenishes the queue fails with the pending
count instead of hanging. TestHost tests do not need `Thread.Sleep`, `Task.Delay`, or wall-clock
synchronization. Phase 1 deliberately has no timer advancement or TestClock.

All UI and host operations must run on the owner thread. This is an explicit single-threaded test
model, not a general asynchronous UI framework.

## Explicit layout and invalidation

`PerformLayout()` runs the production form adapter, client owner, and descendant
`Control.PerformLayout` paths. `LayoutUntilStable()` repeats complete passes until detached tree
geometry is unchanged and the dispatcher is empty. Its default maximum is 16 passes; exceeding
the limit throws with a readable tree dump. Callers can lower the limit for focused regression
tests.

`Invalidate(control)` accepts the root or a hosted descendant. `ProcessPendingWork()` drains UI
work, stabilizes layout, and consumes recorded headless invalidations. It does not paint pixels.

## Resize and logical render scale

The viewport is an immutable logical configuration:

```csharp
using var host = ModernFormsTestHost.Create(new TestViewport(800, 600, 1.25));
TestWindowHost window = host.Show(new Panel { Name = "content" });

window.Resize(1024, 768);
window.SetRenderScale(2.0);
window.LayoutUntilStable();
```

Common controlled values are `1.0`, `1.25`, `1.5`, and `2.0`. Width, height, and layout bounds
remain logical pixels. `ControlTreeSnapshot.DeviceBounds` is only a deterministic edge-rounded
logical-to-device projection for diagnostics; it is not a bitmap or physical-monitor assertion.

## Structural snapshots and diagnostics

`CaptureTree()` returns a detached immutable tree containing:

- stable name/index path and short CLR type name;
- logical `Bounds`, `ClientRectangle`, and `DisplayRectangle`;
- diagnostic `DeviceBounds` at the configured render scale;
- effective `Visible` and `Enabled` state;
- immutable child snapshots in framework collection order.

`Dump()` produces readable failure output:

```text
Panel Root [0,0,300,200]
  Button save [20,20,120,32]
```

Changing or disposing live controls cannot change an earlier snapshot. `GetDiagnostics()` adds
hosted-window count, pending dispatcher work, pending invalidations, active control-owned
animations, captured dispatcher exceptions, and every current tree.

## Disposal and test isolation

Disposing the host deterministically:

1. closes every hosted form/tree, even when an application `Closing` handler cancels a normal
   user close or throws; cleanup continues and disposal reports the failure;
2. cancels default-scheduler entries owned by controls in those trees;
3. disposes headless adapters and unregisters every testing window backend;
4. drains the deterministic dispatcher;
5. restores application resources and the active ThemeManager definition;
6. restores the previous process dispatcher and removes the window-factory scope.

Forms must be constructed after `ModernFormsTestHost.Create()` and cannot move between host
scopes. A disposed host cannot be reused.

ModernFormsNext currently owns dispatcher, `Application.OpenForms`, resources, ThemeManager, and
the default animation scheduler at process scope. Therefore only one `ModernFormsTestHost` may be
active per process, and tests using it must be serialized. The package fails a second concurrent
creation instead of claiming unsupported parallel isolation. The package's own xUnit suite
disables parallelization; consumers should use an equivalent collection or assembly policy.

## Binding, themes, and animations

TestHost adds no alternate binding or theme implementation. Normal `DataBindings`,
`Application.Resources`, dynamic resource references, and `ThemeManager.Current.Apply` run in the
hosted tree. With the current binding lifecycle, set or refresh the normal `BindingContext` after a
control is created when a binding must be activated explicitly; this is runtime behavior, not a
TestHost helper.

Phase 1 does not virtualize animation time. Existing control-owned scheduler entries can be
started normally, and closing their hosted tree cancels them. Deterministic animation advancement
belongs to a future TestClock phase.

## Phase 1 boundaries

Phase 1 intentionally does not provide:

- pointer, keyboard, text, wheel, touch, or Tab/focus simulation (Phase 2);
- a TestClock, deterministic animation advancement, or fake wall clock;
- off-screen rendering, bitmap snapshots, screenshot comparison, or image diffs (Phase 3);
- fake application lifecycle, clipboard, accessibility, IME, native view, or other platform
  services;
- modal-dialog or OS focus semantics;
- Windows/Android native-window, monitor, compositor, GPU, device, or manual UI validation.

These remaining phases stay tracked by [GitHub issue #64](https://github.com/ProGraMajster/ModernFormsNext/issues/64).
Phase 1 establishes the deterministic application/window, dispatcher, layout, viewport, cleanup,
and structural-inspection foundation for that future work.
