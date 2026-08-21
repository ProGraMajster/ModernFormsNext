using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.DataBinding;
using ModernFormsNext.WindowKit.Threading;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ModernFormsTestHostTests
{
    [Fact]
    public void CreateAndDisposeLeavesNoActiveHost()
    {
        using (ModernFormsTestHost.Create())
        {
        }

        using var next = ModernFormsTestHost.Create();
        Assert.Empty(next.Windows);
    }

    [Fact]
    public void OnlyOneProcessHostCanBeActive()
    {
        using var host = ModernFormsTestHost.Create();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(ModernFormsTestHost.Create);

        Assert.Contains("Only one ModernFormsTestHost", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowFormUsesRealFormLifecycle()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { Name = "MainForm", UseSystemDecorations = true };

        TestWindowHost window = host.Show(form, 400, 300);

        Assert.Same(form, window.FormRoot);
        Assert.Null(window.ControlRoot);
        Assert.Contains(form, Application.OpenForms);
        Assert.Equal(new TestViewport(400, 300), window.Viewport);
        Assert.False(window.IsClosed);
    }

    [Fact]
    public void ShowUserControlUsesRealControlOwnership()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new UserControl { Name = "Editor" };

        TestWindowHost window = host.Show(root, 320, 240);

        Assert.Same(root, window.ControlRoot);
        Assert.NotNull(root.Parent);
        Assert.Equal(new Rectangle(0, 0, 320, 240), root.Bounds);
    }

    [Fact]
    public void ShowPanelSupportsGenericRootControl()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { Name = "RootPanel" };

        TestWindowHost window = host.Show(root, 360, 180);

        Assert.Same(root, window.ControlRoot);
        Assert.Equal("RootPanel", window.CaptureTree().Name);
    }

    [Fact]
    public void ExplicitLayoutUsesProductionPerformLayout()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new LayoutProbePanel { Name = "Root" };
        TestWindowHost window = host.Show(root, 300, 200);
        int before = root.LayoutCalls;

        window.PerformLayout();

        Assert.True(root.LayoutCalls > before);
    }

    [Fact]
    public void NestedControlTreeUsesProductionOwnershipAndLayout()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { Name = "Root", Padding = new Padding(5) };
        var inner = new Panel { Name = "Inner", Dock = DockStyle.Fill, Padding = new Padding(7) };
        var leaf = new Button { Name = "Save", Dock = DockStyle.Fill };
        inner.Controls.Add(leaf);
        root.Controls.Add(inner);

        TestWindowHost window = host.Show(root, 300, 200);
        window.PerformLayout();

        Assert.Same(inner, leaf.Parent);
        Assert.Same(root, inner.Parent);
        Assert.Equal(inner.DisplayRectangle, leaf.Bounds);
    }

    [Fact]
    public void DockFillUsesProductionDisplayRectangle()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { Name = "Root", Padding = new Padding(10, 20, 30, 40) };
        var fill = new Panel { Name = "Fill", Dock = DockStyle.Fill, Margin = Padding.Empty };
        root.Controls.Add(fill);

        TestWindowHost window = host.Show(root, 400, 300);
        window.PerformLayout();

        Assert.Equal(root.DisplayRectangle, fill.Bounds);
    }

    [Fact]
    public void PaddingProducesRealPaddedDisplayRectangle()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { Padding = new Padding(11, 13, 17, 19) };
        var fill = new Control { Dock = DockStyle.Fill, Margin = Padding.Empty };
        root.Controls.Add(fill);

        TestWindowHost window = host.Show(root, 200, 150);
        window.PerformLayout();

        Assert.Equal(new Rectangle(11, 13, 172, 118), root.DisplayRectangle);
        Assert.Equal(root.DisplayRectangle, fill.Bounds);
    }

    [Fact]
    public void AnchorRespondsToRealResize()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { Size = new Size(300, 200) };
        var child = new Control
        {
            Bounds = new Rectangle(20, 30, 100, 50),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom
        };
        root.Controls.Add(child);
        TestWindowHost window = host.Show(root, 300, 200);

        window.Resize(500, 300);

        Assert.Equal(new Rectangle(20, 30, 300, 150), child.Bounds);
    }

    [Fact]
    public void ResizeUpdatesViewportAndRealRootBounds()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        TestWindowHost window = host.Show(root, 200, 100);

        window.Resize(640, 480);

        Assert.Equal(new TestViewport(640, 480), window.Viewport);
        Assert.Equal(new Size(640, 480), root.Size);
    }

    [Fact]
    public void RepeatedResizeIsDeterministic()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { Padding = new Padding(8) };
        var fill = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        root.Controls.Add(fill);
        TestWindowHost window = host.Show(root, 200, 100);

        window.Resize(500, 300);
        Rectangle first = fill.Bounds;
        window.Resize(200, 100);
        window.Resize(500, 300);

        Assert.Equal(first, fill.Bounds);
    }

    [Theory]
    [InlineData(1d, 0, 0, 101, 51)]
    [InlineData(1.25d, 0, 0, 126, 64)]
    [InlineData(1.5d, 0, 0, 152, 76)]
    [InlineData(2d, 0, 0, 202, 102)]
    public void ControlledRenderScaleProducesDeterministicDeviceGeometry(
        double scale,
        int x,
        int y,
        int width,
        int height)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(101, 51, scale));
        var root = new Panel { Name = "ScaledRoot" };

        TestWindowHost window = host.Show(root);
        ControlTreeSnapshot tree = window.CaptureTree();

        Assert.Equal(scale, window.Viewport.RenderScale);
        Assert.Equal(new Rectangle(x, y, width, height), tree.DeviceBounds);
    }

    [Fact]
    public void RenderScaleCanChangeWithoutAPhysicalMonitor()
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost window = host.Show(new Panel(), 80, 40);

        window.SetRenderScale(1.5d);

        Assert.Equal(1.5d, window.Viewport.RenderScale);
        Assert.Equal(new Rectangle(0, 0, 120, 60), window.CaptureTree().DeviceBounds);
    }

    [Fact]
    public void TreeSnapshotContainsRequestedStructuralState()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { Name = "Root" };
        var child = new Button { Name = "Save", Bounds = new Rectangle(3, 4, 90, 30), Enabled = false };
        root.Controls.Add(child);

        ControlTreeSnapshot tree = host.Show(root, 200, 100).CaptureTree();
        ControlTreeSnapshot childNode = Assert.Single(tree.Children);

        Assert.Equal("Root.Save", childNode.Path);
        Assert.Equal("Save", childNode.Name);
        Assert.Equal(nameof(Button), childNode.TypeName);
        Assert.Equal(child.Bounds, childNode.Bounds);
        Assert.Equal(child.ClientRectangle, childNode.ClientRectangle);
        Assert.Equal(child.DisplayRectangle, childNode.DisplayRectangle);
        Assert.False(childNode.Enabled);
    }

    [Fact]
    public void TreeSnapshotIsDetachedFromLaterMutations()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { Name = "Root" };
        var child = new Button { Name = "Before", Bounds = new Rectangle(1, 2, 30, 40) };
        root.Controls.Add(child);
        TestWindowHost window = host.Show(root, 200, 100);
        ControlTreeSnapshot snapshot = window.CaptureTree();

        child.Name = "After";
        child.Bounds = new Rectangle(5, 6, 70, 80);
        root.Controls.Clear();

        ControlTreeSnapshot capturedChild = Assert.Single(snapshot.Children);
        Assert.Equal("Before", capturedChild.Name);
        Assert.Equal(new Rectangle(1, 2, 30, 40), capturedChild.Bounds);
    }

    [Fact]
    public void SnapshotDumpContainsPathsTypesAndGeometry()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { Name = "Root" };
        root.Controls.Add(new Button { Name = "Save", Bounds = new Rectangle(20, 30, 120, 32) });

        string dump = host.Show(root, 300, 200).CaptureTree().Dump();

        Assert.Contains("Panel Root [0,0,300,200]", dump, StringComparison.Ordinal);
        Assert.Contains("Button Save [20,30,120,32]", dump, StringComparison.Ordinal);
    }

    [Fact]
    public void OneApplicationCanOwnMultipleWindows()
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost first = host.Show(new Form { UseSystemDecorations = true }, 200, 100);
        TestWindowHost second = host.Show(new Form { UseSystemDecorations = true }, 300, 150);

        Assert.Equal(2, host.Windows.Count);
        Assert.Contains(first, host.Windows);
        Assert.Contains(second, host.Windows);
    }

    [Fact]
    public void ClosingOneWindowKeepsOtherWindowsHosted()
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost first = host.Show(new Panel { Name = "First" });
        TestWindowHost second = host.Show(new Panel { Name = "Second" });

        first.Close();

        Assert.True(first.IsClosed);
        Assert.Single(host.Windows);
        Assert.Same(second, host.Windows[0]);
    }

    [Fact]
    public void CloseClosesEveryHostedWindow()
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost first = host.Show(new Panel());
        TestWindowHost second = host.Show(new UserControl());

        host.Close();

        Assert.True(first.IsClosed);
        Assert.True(second.IsClosed);
        Assert.Empty(host.Windows);
        Assert.Empty(host.GetDiagnostics().ControlTrees);
    }

    [Fact]
    public void InvalidationIsRecordedAndExplicitlyProcessed()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var child = new Button();
        root.Controls.Add(child);
        TestWindowHost window = host.Show(root);

        window.Invalidate(child);
        int pending = host.GetDiagnostics().PendingInvalidationCount;
        window.ProcessPendingWork();

        Assert.True(pending > 0);
        Assert.Equal(0, host.GetDiagnostics().PendingInvalidationCount);
    }

    [Fact]
    public void DispatcherQueueDrainsInDeterministicOrder()
    {
        using var host = ModernFormsTestHost.Create();
        var calls = new List<int>();
        host.Dispatcher.Post(() => calls.Add(1));
        host.Dispatcher.Post(() => calls.Add(2));
        host.Dispatcher.Post(() => calls.Add(3));

        int processed = host.Dispatcher.Drain();

        Assert.Equal(3, processed);
        Assert.Equal([1, 2, 3], calls);
        Assert.Equal(0, host.Dispatcher.PendingWorkCount);
    }

    [Fact]
    public async Task InvokeAsyncAndWaitForIdleUseExplicitDrain()
    {
        using var host = ModernFormsTestHost.Create();
        var invoked = false;
        Task operation = host.Dispatcher.InvokeAsync(() => invoked = true);

        Assert.False(invoked);
        await host.Dispatcher.WaitForIdleAsync();
        await operation;

        Assert.True(invoked);
    }

    [Fact]
    public void DispatcherCapturesUnhandledPostedExceptions()
    {
        using var host = ModernFormsTestHost.Create();
        host.Dispatcher.Post(() => throw new TestDispatcherException("expected"));

        host.Dispatcher.Drain();

        TestDispatcherException exception = Assert.IsType<TestDispatcherException>(Assert.Single(host.Dispatcher.UnhandledExceptions));
        Assert.Equal("expected", exception.Message);
        Assert.Throws<AggregateException>(host.Dispatcher.ThrowUnhandledExceptions);
    }

    [Fact]
    public void DispatcherDrainFailsFastWhenQueueReplenishesItself()
    {
        using var host = ModernFormsTestHost.Create();
        var keepPosting = true;
        Action? callback = null;
        callback = () =>
        {
            if (keepPosting)
                host.Dispatcher.Post(callback!);
        };
        host.Dispatcher.Post(callback);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => host.Dispatcher.Drain(5));
        keepPosting = false;
        host.Dispatcher.Drain();

        Assert.Contains("exceeded 5 operations", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LayoutUntilStableReturnsAfterAStableProductionTree()
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost window = host.Show(new Panel(), 200, 100);

        int passes = window.LayoutUntilStable();

        Assert.InRange(passes, 2, 16);
    }

    [Fact]
    public void LayoutUntilStableFailsWithTreeDiagnosticsAtTheLimit()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new OscillatingLayoutPanel { Name = "Oscillator" };
        TestWindowHost window = host.Show(root, 200, 100);
        root.Controls.Add(new Control { Name = "Moving", Bounds = new Rectangle(0, 0, 20, 20) });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => window.LayoutUntilStable(3));

        Assert.Contains("did not stabilize after 3 layout passes", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Oscillator", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SequentialHostsRestoreViewportResourcesThemeQueueAndWindows()
    {
        string key = $"TestHost.Isolation.{Guid.NewGuid():N}";
        string baselineThemeId = ThemeManager.Current.ActiveTheme!.Id;
        using (ModernFormsTestHost first = ModernFormsTestHost.Create(new TestViewport(123, 45, 2d)))
        {
            Application.Resources[key] = "temporary";
            Assert.True(ThemeManager.Current.Apply(BuiltInThemes.Dark, ImmediateTheme()).Success);
            first.Show(new Panel());
            first.Dispatcher.Post(() => { });
        }

        using var second = ModernFormsTestHost.Create();

        Assert.Equal(new TestViewport(800, 600), second.DefaultViewport);
        Assert.False(Application.Resources.ContainsKey(key));
        Assert.Equal(baselineThemeId, ThemeManager.Current.ActiveTheme!.Id);
        Assert.Equal(0, second.Dispatcher.PendingWorkCount);
        Assert.Empty(second.Windows);
    }

    [Fact]
    public void ClosingTreeCancelsControlOwnedDefaultAnimation()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var child = new Control();
        root.Controls.Add(child);
        TestWindowHost window = host.Show(root);
        AnimationHandle handle = AnimationScheduler.Default.Start(
            child,
            "TestHostCleanup",
            _ => { },
            new AnimationOptions { Duration = TimeSpan.FromHours(1) });

        window.Close();

        Assert.Equal(AnimationState.Canceled, handle.State);
        Assert.Equal(0, host.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void DeterministicCloseCannotBeCanceledByApplicationHandler()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        form.Closing += (_, eventArgs) => eventArgs.Cancel = true;
        TestWindowHost window = host.Show(form);

        window.Close();

        Assert.True(window.IsClosed);
        Assert.DoesNotContain(form, Application.OpenForms);
        Assert.Empty(host.Windows);
    }

    [Fact]
    public void HeadlessBackendCreatesNoNativeHandleSurfaceOrDesktopWindow()
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost window = host.Show(new Form { UseSystemDecorations = true });

        Assert.True(window.IsHeadless);
        Assert.False(window.Backend.HasNativeWindow);
        Assert.Equal(IntPtr.Zero, window.Backend.Handle.Handle);
        Assert.Equal("HEADLESS", window.Backend.Handle.HandleDescriptor);
        Assert.Empty(window.Backend.Surfaces);
    }

    [Fact]
    public void DiagnosticsReportWindowsTreesQueueInvalidationsAndAnimations()
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost window = host.Show(new Panel { Name = "DiagnosticRoot" });
        host.Dispatcher.Post(() => { });
        window.Invalidate();

        TestHostDiagnostics diagnostics = host.GetDiagnostics();
        string dump = diagnostics.Dump();

        Assert.Equal(1, diagnostics.HostedWindowCount);
        Assert.Equal(1, diagnostics.PendingDispatcherWorkCount);
        Assert.True(diagnostics.PendingInvalidationCount > 0);
        Assert.Single(diagnostics.ControlTrees);
        Assert.Contains("DiagnosticRoot", dump, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingDataBindingsWorkInsideHost()
    {
        using var host = ModernFormsTestHost.Create();
        var model = new BindingModel { Value = "before" };
        using var source = new BindingSource
        {
            DataSource = new BindingList<BindingModel> { model }
        };
        var textBox = new TextBox();
        Binding binding = textBox.DataBindings.Add(
            nameof(TextBox.Text),
            source,
            nameof(BindingModel.Value),
            formattingEnabled: true);
        var root = new Panel();
        root.Controls.Add(textBox);
        host.Show(root);
        textBox.BindingContext = new BindingContext();

        model.Value = "after";
        binding.ReadValue();
        host.ProcessPendingWork();

        Assert.Equal("after", textBox.Text);
    }

    [Fact]
    public void ThemeManagerAndResourceResolutionUseProductionPaths()
    {
        using var host = ModernFormsTestHost.Create();
        string key = $"TestHost.Resource.{Guid.NewGuid():N}";
        Application.Resources[key] = "resolved";
        var label = new Label();
        label.SetResourceReference(nameof(Label.Text), key);
        host.Show(label);

        ThemeApplyResult result = ThemeManager.Current.Apply(BuiltInThemes.Dark, ImmediateTheme());

        Assert.True(result.Success);
        Assert.Equal(BuiltInThemes.DarkThemeId, ThemeManager.Current.ActiveTheme!.Id);
        Assert.Equal("resolved", label.Text);
    }

    [Fact]
    public void FormFromPreviousHostScopeCannotBeReused()
    {
        Form previousForm;
        using (ModernFormsTestHost previousHost = ModernFormsTestHost.Create())
            previousForm = new Form();
        using var host = ModernFormsTestHost.Create();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => host.Show(previousForm));

        Assert.Contains("not constructed inside", exception.Message, StringComparison.Ordinal);
    }

    private static ThemeApplyOptions ImmediateTheme()
        => new()
        {
            Transition = new ThemeTransitionOptions { Enabled = false }
        };

    private sealed class LayoutProbePanel : Panel
    {
        public int LayoutCalls { get; private set; }

        protected override void OnLayout(LayoutEventArgs e)
        {
            LayoutCalls++;
            base.OnLayout(e);
        }
    }

    private sealed class OscillatingLayoutPanel : Panel
    {
        private bool moveRight;

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Controls.Count == 0)
                return;

            Control child = Controls[0];
            child.Left = moveRight ? 0 : 1;
            moveRight = !moveRight;
        }
    }

    private sealed class BindingModel : INotifyPropertyChanged
    {
        private string value = string.Empty;

        public string Value
        {
            get => value;
            set
            {
                if (this.value == value)
                    return;
                this.value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class TestDispatcherException(string message) : Exception(message);
}
