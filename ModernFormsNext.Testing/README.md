# ModernFormsNext.Testing

`ModernFormsNext.Testing` provides the supported deterministic, headless Phase 1 host for
ModernFormsNext application and layout tests. It creates real framework controls, uses the
production `Form` lifecycle and `Control.PerformLayout`, and never opens a visible native window.

```csharp
using ModernFormsNext;
using ModernFormsNext.Testing;

using var host = ModernFormsTestHost.Create();
var form = new Form { Name = "MainForm" };
form.Controls.Add(new Button { Name = "save", Dock = DockStyle.Bottom, Text = "Save" });

TestWindowHost window = host.Show(form, 400, 300);
window.PerformLayout();
ControlTreeSnapshot snapshot = window.CaptureTree();
```

The Phase 1 package does not simulate pointer/keyboard input, focus, time, native lifecycle, or
bitmap rendering. It complements rather than replaces platform end-to-end testing. Tests using an
active host must be serialized within a process because ModernFormsNext application and dispatcher
state is process-wide.
