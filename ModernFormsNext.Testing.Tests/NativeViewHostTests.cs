using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class NativeViewHostTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void NestedGeometryClippingAndScalingUseTheCanonicalPresentation(double scale)
    {
        using var app = ModernFormsTestHost.Create(new TestViewport(500, 400, scale));
        using var form = new Form { UseSystemDecorations = true };
        var panel = form.Controls.Add(new Panel { Bounds = new(20, 30, 200, 150) });
        var nested = panel.Controls.Add(new Panel { Bounds = new(10, 15, 100, 80) });
        var factory = new Factory();
        var native = nested.Controls.Add(new NativeViewHost { Bounds = new(60, 50, 80, 60), PeerFactory = factory });
        var window = app.Show(form);
        window.LayoutUntilStable();
        var session = Assert.Single(window.NativeViews.Sessions);
        var bounds = session.Placement.PixelBounds;
        var screen = native.PointToScreen(System.Drawing.Point.Empty);
        Assert.Equal(screen.X - form.Location.X, bounds.X);
        Assert.Equal(screen.Y - form.Location.Y, bounds.Y);
        Assert.InRange(bounds.Width, 80 * scale - 1, 80 * scale + 1);
        Assert.True(session.Placement.PixelClip.Width < bounds.Width);
        Assert.True(session.Placement.Visible);
        panel.Left += 20;
        Assert.InRange(session.Placement.PixelBounds.X - bounds.X, 20 * scale - 1, 20 * scale + 1);
        Assert.Equal(1, factory.Created);
        native.Left = 300;
        Assert.False(session.Placement.Visible);
        native.Left = 0;
        Assert.True(session.Placement.Visible);
    }

    [Fact]
    public void VisibilityEnabledUnsupportedCompositionAndScrollRetainThePeer()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form { UseSystemDecorations = true };
        var panel = form.Controls.Add(new Panel { Bounds = new(10, 10, 180, 120), AutoScroll = true });
        var factory = new Factory();
        var native = panel.Controls.Add(new NativeViewHost { Bounds = new(20, 20, 100, 80), PeerFactory = factory });
        var window = app.Show(form);
        window.LayoutUntilStable();
        var session = Assert.Single(window.NativeViews.Sessions);
        panel.Controls.Add(new Control { Bounds = new(390, 390, 10, 10) });
        window.LayoutUntilStable();
        var original = session.Placement.PixelBounds;
        panel.VerticalScrollProperties.Value = 50;
        Assert.True(session.Placement.PixelBounds.Y < original.Y);
        panel.Visible = false;
        Assert.False(session.Placement.Visible);
        panel.Visible = true;
        panel.Enabled = false;
        Assert.False(session.Placement.Enabled);
        panel.Enabled = true;
        panel.Opacity = .5f;
        Assert.Equal(NativeViewHostState.Unsupported, native.HostingDiagnostics.State);
        Assert.False(session.Placement.Visible);
        panel.Opacity = 1;
        panel.Rotation = 20;
        Assert.False(session.Placement.Visible);
        panel.Rotation = 0;
        panel.ScaleX = 2;
        Assert.False(session.Placement.Visible);
        panel.ScaleX = 1;
        panel.TranslationX = float.NaN;
        Assert.Equal(NativeViewHostState.Unsupported, native.HostingDiagnostics.State);
        Assert.False(session.Placement.Visible);
        panel.TranslationX = 0;
        panel.VerticalScrollProperties.Value = 0;
        Assert.True(session.Placement.Visible);
        Assert.Equal(1, factory.Created);
        Assert.Equal(0, factory.Disposed);
    }

    [Fact]
    public void SameWindowReparentRetainsLeaseCrossWindowAndPresentationReplacementRetireIt()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        using var second = new Form();
        var a = form.Controls.Add(new Panel { Bounds = new(0, 0, 200, 200) });
        var b = form.Controls.Add(new Panel { Bounds = new(200, 0, 200, 200) });
        var factory = new Factory();
        var native = a.Controls.Add(new NativeViewHost { Bounds = new(5, 5, 80, 40), PeerFactory = factory });
        var window = app.Show(form);
        var other = app.Show(second);
        var old = Assert.Single(window.NativeViews.Sessions);
        b.Controls.Add(native);
        Assert.Equal(1, factory.Created);
        Assert.False(old.IsDisposed);
        second.Controls.Add(native);
        Assert.True(old.IsDisposed);
        Assert.Equal(2, factory.Created);
        Assert.False(old.TryFocus());
        other.NativeViews.SetAvailable(false);
        Assert.Equal(2, factory.Disposed);
        other.NativeViews.SetAvailable(true);
        Assert.Equal(3, factory.Created);
        native.Dispose();
        Assert.Equal(3, factory.Disposed);
        Assert.False(other.NativeViews.Sessions.Last().TryFocus());
    }

    [Fact]
    public void NativeFocusUsesValidationAndExistingTabOrderWithoutStaleLoss()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        var a = form.Controls.Add(new TextBox { TabIndex = 0, Bounds = new(5, 5, 90, 30) });
        var native = form.Controls.Add(new NativeViewHost { TabIndex = 1, Bounds = new(5, 40, 90, 30), PeerFactory = new Factory() });
        var b = form.Controls.Add(new TextBox { TabIndex = 2, Bounds = new(5, 80, 90, 30) });
        var window = app.Show(form);
        var session = Assert.Single(window.NativeViews.Sessions);
        bool cancel = true;
        a.Validating += (_, e) => e.Cancel = cancel;
        a.Select();
        Assert.False(session.TryFocus());
        Assert.Same(a, window.FocusedControl);
        cancel = false;
        Assert.True(session.TryFocus());
        Assert.Same(native, window.FocusedControl);
        Assert.True(session.IsFocused);
        Assert.True(session.MoveFocus(true));
        Assert.Same(b, window.FocusedControl);
        a.Select();
        form.Controls.Owner.SelectNextControl(a, true, true, true, true);
        Assert.Same(native, window.FocusedControl);
        Assert.True(session.FocusRequests > 0);
        b.Select();
        session.ReturnFocus();
        Assert.Same(b, window.FocusedControl);
        Assert.False(session.IsFocused);
    }

    [Fact]
    public void MultipleHostsFollowExistingControlOrder()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        var a = form.Controls.Add(new NativeViewHost { Bounds = new(0, 0, 100, 40), PeerFactory = new Factory() });
        var b = form.Controls.Add(new NativeViewHost { Bounds = new(0, 0, 100, 40), PeerFactory = new Factory() });
        var window = app.Show(form);
        var first = window.NativeViews.Sessions[0];
        var second = window.NativeViews.Sessions[1];
        Assert.Equal(second.Generation, first.InFrontGeneration);
        a.BringToFront();
        Assert.Null(first.InFrontGeneration);
        Assert.Equal(first.Generation, second.InFrontGeneration);
        b.Dispose();
        Assert.True(second.IsDisposed);
    }

    [Fact]
    public void ReentrantFactoryAndDisposeCannotPublishAnObsoleteSession()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        var a = form.Controls.Add(new Panel { Bounds = new(0, 0, 180, 180) });
        var b = form.Controls.Add(new Panel { Bounds = new(200, 0, 180, 180) });
        NativeViewHost? native = null;
        var factory = new Factory { Creating = () => { if (native!.Parent == a) b.Controls.Add(native); } };
        native = a.Controls.Add(new NativeViewHost { Bounds = new(5, 5, 100, 50), PeerFactory = factory });
        var window = app.Show(form);
        Assert.Same(b, native.Parent);
        Assert.Equal(factory.Created - 1, factory.Disposed);
        var session = window.NativeViews.Sessions.Last();
        native.Dispose();
        Assert.False(session.IsCurrent);
        Assert.Equal(factory.Created, factory.Disposed);
    }

    [Fact]
    public void IdlePaintAndLayoutDoNotCallTheNativeSessionAgain()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        var native = form.Controls.Add(new NativeViewHost { Bounds = new(5, 5, 100, 40), PeerFactory = new Factory() });
        var window = app.Show(form);
        window.LayoutUntilStable();
        var session = Assert.Single(window.NativeViews.Sessions);
        int updates = session.UpdateCount;
        for (int i = 0; i < 20; i++) { native.Invalidate(); window.PerformLayout(); }
        Assert.Equal(updates, session.UpdateCount);
        native.Left++;
        Assert.Equal(updates + 1, session.UpdateCount);
    }

    [Fact]
    public void DesignerAncestryNeverCreatesPeerAndRuntimePropertiesAreHidden()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        var panel = form.Controls.Add(new Panel { Site = new DesignerSite() });
        var factory = new Factory();
        var native = panel.Controls.Add(new NativeViewHost { PeerFactory = factory });
        app.Show(form);
        Assert.Equal(0, factory.Created);
        Assert.Equal(NativeViewHostState.Detached, native.HostingDiagnostics.State);
        foreach (string name in new[] { "PeerFactory", "HostingCapabilities", "HostingDiagnostics" })
            Assert.Equal(DesignerSerializationVisibility.Hidden,
                ((DesignerSerializationVisibilityAttribute)TypeDescriptor.GetProperties(native)[name]!
                    .Attributes[typeof(DesignerSerializationVisibilityAttribute)]!).Visibility);
    }

    [Fact]
    public void EmptyHostFaultedFactoryAndRoundedStylesSettleWithoutNativeWork()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        var native = form.Controls.Add(new NativeViewHost { Bounds = new(5, 5, 100, 40) });
        var window = app.Show(form);
        native.Invalidate();
        Assert.Empty(window.NativeViews.Sessions);
        Assert.Equal(NativeViewHostState.Detached, native.HostingDiagnostics.State);
        native.PeerFactory = new FaultFactory();
        Assert.Equal(NativeViewHostState.Faulted, native.HostingDiagnostics.State);
        native.Invalidate();
        Assert.Empty(window.NativeViews.Sessions);
        var factory = new Factory();
        native.PeerFactory = factory;
        Assert.Equal(NativeViewHostState.Attached, native.HostingDiagnostics.State);
        native.Style.Border.Radius = 10;
        native.Invalidate();
        Assert.Equal(NativeViewHostState.Unsupported, native.HostingDiagnostics.State);
        native.Style.Border.Radius = 0;
        native.Invalidate();
        Assert.Equal(NativeViewHostState.Attached, native.HostingDiagnostics.State);
        Assert.Equal(1, factory.Created);
    }

    [Fact]
    public void RemoveAtDispatcherBoundaryAndParentDisposeReleaseLeases()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        var panel = form.Controls.Add(new Panel { Bounds = new(0, 0, 200, 200) });
        var factory = new Factory();
        var native = panel.Controls.Add(new NativeViewHost { Bounds = new(5, 5, 100, 40), PeerFactory = factory });
        var window = app.Show(form);
        panel.Controls.Remove(native);
        app.Dispatcher.Drain();
        Assert.Equal(1, factory.Disposed);
        panel.Controls.Add(native);
        Assert.Equal(2, factory.Created);
        panel.Dispose();
        Assert.Equal(2, factory.Disposed);
        Assert.All(window.NativeViews.Sessions, s => Assert.False(s.IsCurrent));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DockAnchorFlowAndTableUseTheExistingLayout(bool table)
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form { UseSystemDecorations = true };
        Panel panel = table ? new TableLayoutPanel { ColumnCount = 1, RowCount = 1 } : new FlowLayoutPanel();
        panel.Dock = DockStyle.Fill;
        form.Controls.Add(panel);
        var factory = new Factory();
        var native = panel.Controls.Add(new NativeViewHost { Size = new(100, 40), PeerFactory = factory });
        var window = app.Show(form);
        window.LayoutUntilStable();
        var before = native.HostingDiagnostics.Placement;
        form.ClientSize = new(600, 400);
        window.LayoutUntilStable();
        Assert.True(native.HostingDiagnostics.Placement.Visible);
        var screen = native.PointToScreen(System.Drawing.Point.Empty);
        Assert.Equal(screen.X - form.Location.X, native.HostingDiagnostics.Placement.PixelBounds.X);
        Assert.Equal(screen.Y - form.Location.Y, native.HostingDiagnostics.Placement.PixelBounds.Y);
        Assert.Equal(1, factory.Created);
        native.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        native.Dock = DockStyle.Fill;
        window.LayoutUntilStable();
        Assert.True(native.HostingDiagnostics.Placement.PixelBounds.Width > 0);
    }

    [Fact]
    public void FinalizerPathDoesNoNativeWorkAndWrongThreadDisposeCannotRetireLease()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        var factory = new Factory();
        var native = form.Controls.Add(new FinalizerHost { Bounds = new(5, 5, 100, 40), PeerFactory = factory });
        var window = app.Show(form);
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            native.FinalizeForTest();
            try { native.Dispose(); } catch (Exception error) { failure = error; }
        });
        worker.Start(); worker.Join();
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(0, factory.Disposed);
        Assert.True(Assert.Single(window.NativeViews.Sessions).IsCurrent);
        native.Dispose();
        Assert.Equal(1, factory.Disposed);
    }

    [Fact]
    public void PerformanceRecordsZeroIdleUpdatesAndBoundedGeometryAllocations()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        using var plainForm = new Form();
        var plainPanel = plainForm.Controls.Add(new Panel { Bounds = new(0, 0, 250, 200), AutoScroll = true });
        var plain = plainPanel.Controls.Add(new Control { Bounds = new(0, 50, 100, 40) });
        plainPanel.Controls.Add(new Control { Bounds = new(400, 400, 10, 10) });
        var plainWindow = app.Show(plainForm);
        var panel = form.Controls.Add(new Panel { Bounds = new(0, 0, 250, 200), AutoScroll = true });
        var native = panel.Controls.Add(new NativeViewHost { Bounds = new(0, 50, 100, 40), PeerFactory = new Factory() });
        panel.Controls.Add(new Control { Bounds = new(400, 400, 10, 10) });
        var window = app.Show(form);
        window.LayoutUntilStable();
        for (int i = 0; i < 100; i++) { plain.Invalidate(); native.Invalidate(); }
        static long Measure(Control control)
        {
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) control.Invalidate();
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }
        long ordinaryBytes = Measure(plain), nativeBytes = Measure(native);
        Assert.Equal(ordinaryBytes, nativeBytes); // An idle host adds no allocation to ordinary invalidation.
        Assert.Empty(plainWindow.NativeViews.Sessions);
        var session = Assert.Single(window.NativeViews.Sessions);
        int before = session.UpdateCount;
        int capabilityQueries = window.NativeViews.CapabilityQueries;
        for (int i = 0; i < 1000; i++) native.Invalidate();
        Assert.Equal(before, session.UpdateCount);
        Assert.Equal(capabilityQueries, window.NativeViews.CapabilityQueries);
        long allocations = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) native.Left++;
        long geometryBytes = GC.GetAllocatedBytesForCurrentThread() - allocations;
        Assert.Equal(before + 100, session.UpdateCount);
        before = session.UpdateCount;
        allocations = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1; i <= 20; i++) panel.VerticalScrollProperties.Value = i;
        long scrollBytes = GC.GetAllocatedBytesForCurrentThread() - allocations;
        Assert.Equal(before + 20, session.UpdateCount);
        output.WriteLine($"1000 invalidations: zero-host window={ordinaryBytes} bytes, native={nativeBytes} bytes; idle native updates=0; 100 moves: updates=100, allocations={geometryBytes} bytes; 20 scroll ticks: updates=20, allocations={scrollBytes} bytes.");
    }

    [Fact]
    public void ThrowingPeerRetirementRevokesCallbacksAndNeverLeavesAttachedDiagnostics()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        var native = form.Controls.Add(new NativeViewHost { Bounds = new(5, 5, 100, 40), PeerFactory = new ThrowingDisposeFactory() });
        var window = app.Show(form);
        var session = Assert.Single(window.NativeViews.Sessions);
        Assert.Throws<InvalidOperationException>(() => native.PeerFactory = null);
        Assert.True(session.IsDisposed);
        Assert.False(session.IsCurrent);
        Assert.False(session.TryFocus());
        Assert.Equal(NativeViewHostState.Faulted, native.HostingDiagnostics.State);
    }

    private sealed class ThrowingDisposeFactory : INativeViewFactory
    {
        public INativeViewPeer CreatePeer(INativeViewSite site) => new Peer();
        private sealed class Peer : INativeViewPeer
        {
            public void Resize(PixelSize size) { }
            public void RequestFocus() { }
            public bool TryMoveFocus(bool forward) => false;
            public void Dispose() => throw new InvalidOperationException("synthetic cleanup failure");
        }
    }

    [Fact]
    public void ExplicitInvalidationObservesMutableAndDerivedAncestorClipping()
    {
        using var app = ModernFormsTestHost.Create();
        using var form = new Form();
        var panel = form.Controls.Add(new ClippingPanel { Bounds = new(0, 0, 200, 100) });
        var native = panel.Controls.Add(new NativeViewHost { Bounds = new(0, 0, 150, 50), PeerFactory = new Factory() });
        var window = app.Show(form);
        var session = Assert.Single(window.NativeViews.Sessions);
        panel.Invalidate(); // Establish the sparse invalidation snapshot.
        panel.ClipWidth = 70;
        panel.Invalidate();
        Assert.Equal(70, session.Placement.PixelClip.Width);
        panel.ClipWidth = 200;
        panel.Style.Border.Left.Width = 20;
        panel.Invalidate();
        Assert.Equal(20, session.Placement.PixelClip.X);
        Assert.Single(window.NativeViews.Sessions);
    }

    private sealed class ClippingPanel : Panel
    {
        internal int ClipWidth = 200;
        public override Rectangle ClientRectangle => Rectangle.Intersect(base.ClientRectangle, new(0, 0, ClipWidth, 100));
    }

    private sealed class FaultFactory : INativeViewFactory
    {
        public INativeViewPeer CreatePeer(INativeViewSite site) => throw new InvalidOperationException("fault");
    }

    private sealed class FinalizerHost : NativeViewHost
    {
        internal void FinalizeForTest() => Dispose(false);
    }

    private sealed class Factory : INativeViewFactory
    {
        internal int Created, Disposed;
        internal Action? Creating;
        public INativeViewPeer CreatePeer(INativeViewSite site) { Created++; Creating?.Invoke(); return new Peer(this); }
        private sealed class Peer(Factory factory) : INativeViewPeer
        {
            public void Resize(PixelSize size) { }
            public void RequestFocus() { }
            public bool TryMoveFocus(bool forward) => false;
            public void Dispose() => factory.Disposed++;
        }
    }
    private sealed class DesignerSite : ISite
    {
        public IComponent Component => null!;
        public IContainer? Container => null;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type type) => null;
    }
}
