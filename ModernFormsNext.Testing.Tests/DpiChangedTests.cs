using System.Drawing;
using System.Reflection;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class DpiChangedTests
{
    [Theory]
    [InlineData(1, 1.25)]
    [InlineData(1.25, 1.5)]
    [InlineData(1.5, 2)]
    [InlineData(2, 2.25)]
    [InlineData(1, 1.5)]
    [InlineData(1.251, 1.252)]
    public void ControlsAndWindowObserveConfirmedScaleOnceAfterTheirLayout(double oldScale, double newScale)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Size = new Size(400, 300) };
        using var parent = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var child = parent.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var grandchild = child.Controls.Add(new Control { Dock = DockStyle.Fill });
        using var sibling = parent.Controls.Add(new Control());
        form.Show();
        var backend = Backend(form);
        backend.SetRenderScale(oldScale);
        List<string> order = [];
        foreach (var (control, name) in new[] { (parent as Control, "parent"), (child, "child"), (grandchild, "grandchild"), (sibling, "sibling") })
            control.DpiChanged += (_, e) => {
                Assert.Equal(oldScale, e.OldScale); Assert.Equal(newScale, e.NewScale);
                Assert.Equal((int)(oldScale * 96), e.OldDpi);
                Assert.Equal(control.DeviceDpi, e.NewDpi); Assert.Equal(newScale, control.Scaling);
                Assert.Equal(form.ClientSize, parent.Size); Assert.Equal(parent.Size, child.Size);
                order.Add(name);
            };
        Root(form).DpiChanged += (_, _) => throw new Exception("Internal adapter must not expose another event.");
        form.DpiChanged += (sender, e) => {
            Assert.Same(form, sender); Assert.Equal(newScale, form.Scaling);
            Assert.Equal(oldScale, e.OldScale); Assert.Equal(newScale, e.NewScale);
            order.Add("window");
        };
        int geometry = 0;
        form.Resize += (_, _) => geometry++;
        form.SizeChanged += (_, _) => geometry++;
        form.ClientSizeChanged += (_, _) => geometry++;
        backend.SetRenderScale(newScale);
        backend.SetRenderScale(newScale);
        backend.ScalingChanged!(newScale);
        backend.ScalingChanged!(oldScale); // A stale payload cannot change canonical state.
        Assert.Equal(new[] { "parent", "child", "grandchild", "sibling", "window" }, order);
        Assert.Equal(0, geometry);
    }

    [Fact]
    public void ControlHookKeepsEventArgsSignatureAndClearsFrameworkCachesBeforeObservers()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var control = form.Controls.Add(new CacheProbe { Size = new Size(60, 40) });
        form.Show();
        var properties = typeof(Control).Assembly.GetType("ModernFormsNext.Layout.CommonProperties")!;
        properties.GetMethod("xSetPreferredSizeCache", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [control, new Size(321, 123)]);
        typeof(Control).GetMethod("GetBackBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, null);
        int observed = 0;
        control.DpiChanged += (_, e) => {
            Assert.True(control.CachesClearedBeforeBase);
            Assert.Equal(1.5, control.CacheScale);
            Assert.Equal(144, control.DeviceDpi);
            Assert.Equal(1.5, e.NewScale);
            observed++;
        };
        Backend(form).SetRenderScale(1.5);
        Assert.Equal(1, observed);
        Assert.Equal(1, control.Hooks);
    }

    [Theory]
    [InlineData("detach")]
    [InlineData("dispose")]
    [InlineData("reparent")]
    [InlineData("away-and-back")]
    public void ParentCallbackRetiresRemovedChildRoute(string action)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var other = new Form();
        using var parent = form.Controls.Add(new Panel());
        using var child = parent.Controls.Add(new Control());
        int events = 0;
        child.DpiChanged += (_, _) => events++;
        parent.DpiChanged += (_, _) => {
            if (action == "dispose") child.Dispose();
            else if (action == "reparent") other.Controls.Add(child);
            else { parent.Controls.Remove(child); if (action == "away-and-back") parent.Controls.Add(child); }
        };
        Backend(form).SetRenderScale(1.5);
        Assert.Equal(0, events);
        Assert.Equal(1.5, form.Scaling);
    }

    [Fact]
    public void MovingAlreadyNotifiedChildToLaterSubtreeDoesNotNotifyTwice()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var first = form.Controls.Add(new Panel());
        using var second = form.Controls.Add(new Panel());
        using var child = first.Controls.Add(new Control());
        int events = 0;
        child.DpiChanged += (_, _) => { events++; second.Controls.Add(child); };
        Backend(form).SetRenderScale(1.5);
        Assert.Equal(1, events);
        Assert.Same(second, child.Parent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingSelfOrClosingWindowFromControlObserverRetiresItsSubtree(bool close)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var parent = form.Controls.Add(new Panel());
        using var child = parent.Controls.Add(new Control());
        form.Show();
        int later = 0, children = 0, windows = 0;
        parent.DpiChanged += (_, _) => { if (close) form.Close(); else parent.Parent = null; };
        parent.DpiChanged += (_, _) => later++;
        child.DpiChanged += (_, _) => children++;
        form.DpiChanged += (_, _) => windows++;
        Backend(form).SetRenderScale(1.5);
        Assert.Equal(0, later); Assert.Equal(0, children);
        Assert.Equal(close ? 0 : 1, windows);
    }

    [Fact]
    public void SuspendedLayoutKeepsExistingDeferralWhileDpiCommits()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var parent = form.Controls.Add(new Panel());
        using var child = parent.Controls.Add(new Control { Dock = DockStyle.Fill });
        parent.SuspendLayout();
        int events = 0;
        parent.DpiChanged += (_, e) => { Assert.Equal(e.NewDpi, parent.DeviceDpi); events++; };
        Backend(form).SetRenderScale(1.5);
        Assert.Equal(1, events);
        parent.Size = new Size(200, 150);
        parent.ResumeLayout();
        Assert.Equal(parent.ClientSize, child.Size);
        Assert.Equal(1, events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewerScaleSupersedesOldControlAndWindowObservers(bool fromWindow)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var control = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var child = control.Controls.Add(new Control());
        var backend = Backend(form);
        List<double> controls = [], windows = [], children = [];
        EventHandler<DpiChangedEventArgs> reenter = (_, e) => { if (e.NewScale == 1.5) backend.SetRenderScale(2.25); };
        if (fromWindow) form.DpiChanged += reenter; else control.DpiChanged += reenter;
        control.DpiChanged += (_, e) => { Assert.Equal(e.NewScale, control.Scaling); controls.Add(e.NewScale); };
        child.DpiChanged += (_, e) => { Assert.Equal(e.NewScale, child.Scaling); children.Add(e.NewScale); };
        form.DpiChanged += (_, e) => { Assert.Equal(e.NewScale, form.Scaling); windows.Add(e.NewScale); };
        backend.SetRenderScale(1.5);
        Assert.Equal(fromWindow ? new[] { 1.5, 2.25 } : new[] { 2.25 }, controls);
        Assert.Equal(fromWindow ? new[] { 1.5, 2.25 } : new[] { 2.25 }, children);
        Assert.Equal(new[] { 2.25 }, windows);
        Assert.Equal(2.25, form.Scaling);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HideOrCloseInWindowObserverStopsOlderObservers(bool close)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        var completion = form.ShowDialog(owner);
        int later = 0;
        form.DpiChanged += (_, _) => { if (close) form.Close(); else form.Hide(); };
        form.DpiChanged += (_, _) => later++;
        Backend(form).SetRenderScale(1.5);
        Assert.False(form.Visible); Assert.False(Backend(form).IsShown); Assert.Equal(0, later);
        if (!close) { Assert.False(completion.IsCompleted); form.Close(); }
        Assert.True(completion.IsCompletedSuccessfully); Assert.True(Backend(owner).IsEnabled);
    }

    [Fact]
    public void ResizeFromObserverRetainsNewGeometryAndLifecycle()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        int load = 0, shown = 0, visible = 0, state = 0, dpi = 0, resize = 0;
        form.Load += (_, _) => load++; form.Shown += (_, _) => shown++;
        form.VisibleChanged += (_, _) => visible++; form.WindowStateChanged += (_, _) => state++;
        form.Resize += (_, _) => resize++;
        form.DpiChanged += (_, _) => { dpi++; form.Size = new Size(720, 510); };
        form.Show();
        resize = 0;
        Backend(form).SetRenderScale(1.5);
        Assert.Equal(new Size(720, 510), form.Size); Assert.Equal(form.ClientSize, fill.Size);
        Assert.Equal(1, resize); Assert.Equal(1, dpi); Assert.Equal(1, load); Assert.Equal(1, shown);
        Assert.Equal(1, visible); Assert.Equal(0, state);
        form.Location = new Point(800, 20); // A move at unchanged DPI is not a DPI notification.
        Assert.Equal(1, dpi);
    }

    [Fact]
    public void ThrowingObserversDoNotSkipRemainingCachesLayoutOrWindowNotification()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var parent = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var child = parent.Controls.Add(new CacheProbe());
        var first = new InvalidOperationException("control");
        var second = new ArgumentException("window");
        int later = 0, windows = 0;
        parent.DpiChanged += (_, _) => throw first;
        parent.DpiChanged += (_, _) => later++;
        form.DpiChanged += (_, _) => throw second;
        form.DpiChanged += (_, _) => { windows++; Assert.Equal(1.5, child.CacheScale); Assert.Equal(form.ClientSize, parent.Size); };
        var error = Assert.Throws<AggregateException>(() => Backend(form).SetRenderScale(1.5));
        Assert.Equal(new Exception[] { first, second }, error.InnerExceptions);
        Assert.Equal(1, later); Assert.Equal(1, windows); Assert.Equal(1, child.Hooks);
        Assert.Equal(1.5, form.Scaling);
        Backend(form).ScalingChanged!(1.5);
        Assert.Equal(1, windows);
    }

    [Fact]
    public void IndependentWindowsAndInitialHighDpiDoNotInventNotifications()
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(800, 600, 1.5));
        using var first = new Form(); using var second = new Form();
        int one = 0, two = 0;
        first.DpiChanged += (_, _) => one++; second.DpiChanged += (_, _) => two++;
        first.Show(); second.Show();
        Backend(first).ScalingChanged!(first.Scaling);
        Assert.Equal(0, one); Assert.Equal(0, two);
        Backend(first).SetRenderScale(2.25);
        Assert.Equal(1, one); Assert.Equal(0, two);
    }

    private sealed class CacheProbe : Control
    {
        internal bool CachesClearedBeforeBase;
        internal double CacheScale;
        internal int Hooks;
        protected override void OnDpiChanged(EventArgs e)
        {
            var properties = typeof(Control).Assembly.GetType("ModernFormsNext.Layout.CommonProperties")!;
            var size = (Size)properties.GetMethod("xGetPreferredSizeCache", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [this])!;
            CachesClearedBeforeBase = size.IsEmpty && typeof(Control).GetField("back_buffer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this) is null;
            CacheScale = Scaling; Hooks++;
            base.OnDpiChanged(e);
        }
    }

    private static HeadlessWindowImpl Backend(Form form) => Assert.IsType<HeadlessWindowImpl>(
        typeof(WindowBase).GetField("window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form));
    private static Control Root(Form form) => Assert.IsAssignableFrom<Control>(
        typeof(WindowBase).GetField("adapter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form));
}
