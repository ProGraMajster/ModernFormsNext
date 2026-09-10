# ModernFormsNext.Testing

`ModernFormsNext.Testing` provides deterministic headless application tests over real framework
controls, layout, input routing, focus, commands and rendering. A controlled clock drives the
production animation scheduler. Scoped clipboard/lifecycle/settings services keep tests isolated.

```csharp
using ModernFormsNext;
using ModernFormsNext.Testing;

using var host = ModernFormsTestHost.Create();
var form = new Form { Name = "MainForm" };
var save = form.Controls.Add(new Button { Name = "save", Dock = DockStyle.Bottom, Text = "Save" });

TestWindowHost window = host.Show(form, 400, 300);
window.Input.Click(save);
host.ProcessPendingWork();
ControlTreeSnapshot snapshot = window.CaptureTree();
using RenderedSnapshot rendered = window.CaptureRenderedSnapshot();
```

Run all host/UI operations and disposal on the creating thread; serialize tests within a process.
The host opens no native window and never accesses the OS clipboard. Off-screen snapshots are
caller-owned and depend on the actual font/rendering environment. Native IME, Android Activity
recreation, OS activation/popup behavior, accessibility and device behavior still require separate
integration tests. Navigation/virtualization testing follows their future production contracts.

`ShowDialog` uses real Form modality and exposes its result task. `ActivePopup`/`Popups` provide
input and snapshots for real control popups. Native foreground rules are separate from these
shared framework checks.

See [the TestHost guide](https://github.com/ProGraMajster/ModernFormsNext/blob/master/docs/testing/testhost.md)
for supported input keys, timer/frame semantics, diagnostics, ownership, and examples.
