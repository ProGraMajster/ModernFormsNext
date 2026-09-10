# Application test template

Use this fixture in an existing .NET 10 xUnit test project. Add a reference to
`ModernFormsNext.Testing` using the framework version used by your application. The new input,
clock and image APIs require a build containing this continuation of issue #64; this document
does not imply that such a package has been published.

The host owns process-wide UI state. Disable parallel execution for the entire test assembly
when tests share it, including tests that access framework state without constructing a host:

```csharp
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

Keep creation, input, assertions and cleanup on the same test thread. This fixture is deliberately
synchronous: it does not yield to arbitrary thread-pool continuations while a host is alive.

```csharp
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.Testing;
using Xunit;

public sealed class SaveFormTests
{
    [Fact]
    public void SaveUsesRealInputAndCompletesItsAnimation()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        var name = form.Controls.Add(new TextBox
        {
            Name = "name", Bounds = new Rectangle(12, 12, 200, 32), TabIndex = 0
        });
        var save = form.Controls.Add(new Button
        {
            Name = "save", Text = "Save", Bounds = new Rectangle(12, 60, 110, 36), TabIndex = 1
        });
        var status = form.Controls.Add(new Label
        {
            Name = "status", Bounds = new Rectangle(12, 110, 220, 30)
        });
        save.Click += (_, _) => status.Text = name.Text;
        var window = host.Show(form, 280, 170);

        Assert.True(window.Input.Focus(name));
        window.Input.TextInput("Ada");
        window.Input.Tab();
        Assert.Same(save, window.FocusedControl);
        window.Input.Click(save);
        Assert.Equal("Ada", status.Text);

        AnimationScheduler.Default.Start(save, "saved", value => save.Opacity = value,
            new AnimationOptions { Duration = TimeSpan.FromSeconds(1), Easing = Easings.Linear });
        host.Clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(.5f, save.Opacity);
        host.Clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(1f, save.Opacity);

        ControlTreeSnapshot tree = window.CaptureTree();
        using RenderedSnapshot image = window.CaptureRenderedSnapshot();
        Assert.Equal(280, image.PixelWidth);
        Assert.Contains("save", tree.Dump());
    }
}
```

For an application test, instantiate your actual Form and assert its observable state. Keep
layout, binding, resources, commands and rendering in production code. Do not replace event
routing with direct calls to event handlers. Use the host's fake clipboard/lifecycle/settings
through their existing platform contracts when a test needs controlled inputs.

Failure handling can retain `host.GetDiagnostics().Dump()` and an explicitly captured PNG using
`File.WriteAllBytes(path, image.EncodePng())`. These artifacts may contain application data; write
them only to a test-controlled output directory. Font-dependent pixels can vary across machines,
so prefer structural assertions unless your raster environment is pinned.

See the [TestHost guide](testhost.md) for modal forms, input coordinates, queued-work bounds,
timer/frame semantics, cleanup and the boundary with native integration tests.
