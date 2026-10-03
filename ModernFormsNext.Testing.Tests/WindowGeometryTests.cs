using System.Drawing;
using System.Reflection;
using ModernFormsNext.WindowKit.Controls;
using Xunit;
using BackendSize = ModernFormsNext.WindowKit.Size;
using PixelPoint = ModernFormsNext.WindowKit.PixelPoint;

namespace ModernFormsNext.Testing.Tests;

public sealed class WindowGeometryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SizeNotificationsFollowCommittedRootAndNestedLayout(bool systemChrome, bool backendResize)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { UseSystemDecorations = systemChrome, Size = new Size(400, 300) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var nested = fill.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var anchored = nested.Controls.Add(new Control { Bounds = new Rectangle(300, 180, 20, 20), Anchor = AnchorStyles.Bottom | AnchorStyles.Right });
        var oldBounds = anchored.Bounds;
        List<string> order = [];
        void Observe(string name)
        {
            Assert.Equal(new Size(520, 390), form.Size);
            Assert.Equal(form.DisplayRectangle, Root(form).Bounds);
            Assert.Equal(form.ClientSize, fill.Size);
            Assert.Equal(fill.Size, nested.Size);
            Assert.Equal(new Point(oldBounds.X + 120, oldBounds.Y + 90), anchored.Location);
            order.Add(name);
        }
        form.Resize += (_, _) => Observe("Resize");
        form.SizeChanged += (_, _) => Observe("Size");
        form.ClientSizeChanged += (_, _) => Observe("Client");

        if (backendResize) Backend(form).Resize(new BackendSize(520, 390), WindowResizeReason.User);
        else form.Size = new Size(520, 390);

        Assert.Equal(new[] { "Resize", "Size", "Client" }, order);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoOpSettersRepeatedAndStaleBackendPayloadsDoNotInventChanges(bool systemChrome)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { UseSystemDecorations = systemChrome };
        List<string> events = Observe(form);
        form.Size = form.Size;
        form.ClientSize = form.ClientSize;
        form.Location = form.Location;
        Backend(form).Resized!(new BackendSize(1, 2), WindowResizeReason.User);
        Backend(form).PositionChanged!(new PixelPoint(-999, 500));
        Assert.Empty(events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequestedSizeIsReportedAfterMinMaxClamping(bool maximum)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Size = new Size(400, 300), MinimumSize = new Size(200, 150), MaximumSize = new Size(600, 500) };
        List<Size> seen = [];
        form.SizeChanged += (_, _) => seen.Add(form.Size);
        Size expected = maximum ? form.MaximumSize : form.MinimumSize;
        form.Size = maximum ? new Size(1000, 900) : new Size(1, 1);
        Assert.Equal(new[] { expected }, seen);
        form.Size = maximum ? new Size(1000, 900) : new Size(1, 1);
        Assert.Single(seen);
    }

    [Theory]
    [InlineData("height")]
    [InlineData("visibility")]
    [InlineData("border")]
    [InlineData("decorations")]
    public void ChromeOnlyChangePublishesRealClientSizeAfterLayout(string change)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Size = new Size(400, 300) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        Size size = form.Size, client = form.ClientSize;
        List<string> events = Observe(form);
        form.ClientSizeChanged += (_, _) => Assert.Equal(form.ClientSize, fill.Size);
        switch (change) {
            case "height": form.TitleBar.Height += 10; break;
            case "visibility": form.TitleBar.Visible = false; break;
            case "border": form.Style.Border.Width = 5; Root(form).PerformLayout(); break;
            case "decorations": form.UseSystemDecorations = true; break;
        }
        Assert.Equal(size, form.Size);
        Assert.NotEqual(client, form.ClientSize);
        Assert.Equal(new[] { "Client" }, events);
        Root(form).PerformLayout();
        Assert.Single(events);
    }

    [Fact]
    public void ChangedDrawableSizeWithUnchangedEmptyClientDoesNotRaiseClientEvent()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Size = new Size(1, 1) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        Assert.Equal(Size.Empty, form.ClientSize);
        List<string> events = Observe(form);
        form.Size = new Size(2, 2);
        Assert.Equal(Size.Empty, form.ClientSize);
        Assert.Equal(new[] { "Resize", "Size" }, events);
        Assert.Equal(form.ClientSize, fill.Size);
    }

    [Theory]
    [InlineData(1d, false)]
    [InlineData(1.75d, false)]
    [InlineData(1d, true)]
    [InlineData(1.75d, true)]
    public void PositionUsesPhysicalCoordinatesWithoutScalingOrDuplicates(double scale, bool backendMove)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(800, 600, scale));
        using var form = new Form();
        Point expected = new(-1234, -210);
        List<Point> seen = [];
        form.LocationChanged += (sender, e) => {
            Assert.Same(form, sender);
            Assert.Same(EventArgs.Empty, e);
            seen.Add(form.Location);
        };
        if (backendMove) Backend(form).Move(new PixelPoint(expected.X, expected.Y));
        else form.Location = expected;
        form.Location = expected;
        Backend(form).PositionChanged!(new PixelPoint(expected.X, expected.Y));
        Assert.Equal(new[] { expected }, seen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResizeFromGeometryObserverSupersedesRemainingOldNotifications(bool fromSizeChanged)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Size = new Size(400, 300) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        List<string> events = [];
        EventHandler change = (_, _) => {
            if (form.Size.Width == 500) form.Size = new Size(600, 450);
        };
        if (fromSizeChanged) form.SizeChanged += change;
        else form.Resize += change;
        form.Resize += (_, _) => { Assert.Equal(form.ClientSize, fill.Size); events.Add("Resize:" + form.Size.Width); };
        form.SizeChanged += (_, _) => events.Add("Size:" + form.Size.Width);
        form.ClientSizeChanged += (_, _) => events.Add("Client:" + form.Size.Width);
        form.Size = new Size(500, 400);
        Assert.Equal(fromSizeChanged
            ? new[] { "Resize:500", "Resize:600", "Size:600", "Client:600" }
            : new[] { "Resize:600", "Size:600", "Client:600" }, events);
        Assert.Equal(new Size(600, 450), form.Size);
    }

    [Fact]
    public void GeometryChangeDuringLayoutFinishesLatestLayoutBeforePublishing()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Size = new Size(400, 300) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        fill.Layout += (_, _) => { if (form.Size.Width == 500) form.Size = new Size(600, 450); };
        List<Size> seen = [];
        form.SizeChanged += (_, _) => { Assert.Equal(form.ClientSize, fill.Size); seen.Add(form.Size); };
        form.Size = new Size(500, 400);
        Assert.Equal(new[] { new Size(600, 450) }, seen);
    }

    [Fact]
    public void ReentrantMoveAndCloseDoNotNotifyOldObserversOrUpdateClosedBackend()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        List<int> seen = [];
        form.LocationChanged += (_, _) => {
            seen.Add(form.Location.X);
            if (form.Location.X == 100) form.Location = new Point(200, -20);
            else form.Close();
        };
        form.LocationChanged += (_, _) => seen.Add(-1);
        form.Location = new Point(100, -20);
        Assert.Equal(new[] { 100, 200 }, seen);
        Assert.True(Backend(form).IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientObserverCanCancelInitialShowDuringLoad(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        int later = 0;
        form.ClientSizeChanged += (_, _) => form.Hide();
        form.ClientSizeChanged += (_, _) => later++;
        form.Load += (_, _) => form.Size = new Size(500, 400);
        Task<DialogResult>? completion = modal ? form.ShowDialog(owner) : null;
        if (!modal) form.Show();
        Assert.False(form.Visible);
        Assert.Equal(0, Backend(form).ShowCallCount);
        Assert.Equal(0, later);
        Assert.True(Backend(owner).IsEnabled);
        if (modal) Assert.True(completion!.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CenteringAfterLoadUsesFinalSizeAndNotifiesBeforeVisibility(bool close)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-500, -200), Size = new Size(800, 600) };
        owner.Show();
        using var form = new Form { StartPosition = FormStartPosition.CenterParent };
        List<string> order = [];
        form.Load += (_, _) => { order.Add("Load"); form.Size = new Size(400, 300); };
        form.Resize += (_, _) => order.Add("Resize");
        form.SizeChanged += (_, _) => order.Add("Size");
        form.ClientSizeChanged += (_, _) => order.Add("Client");
        form.LocationChanged += (_, _) => {
            Assert.Equal(new Point(-300, -50), form.Location);
            Assert.Equal(0, Backend(form).ShowCallCount);
            order.Add("Location");
            if (close) form.Close();
        };
        form.VisibleChanged += (_, _) => { if (form.Visible) order.Add("Visible"); };
        form.Shown += (_, _) => order.Add("Shown");
        var completion = form.ShowDialog(owner);
        Assert.Equal(close
            ? new[] { "Load", "Resize", "Size", "Client", "Location" }
            : new[] { "Load", "Resize", "Size", "Client", "Location", "Visible", "Shown" }, order);
        if (close) {
            Assert.True(completion.IsCompletedSuccessfully);
            Assert.True(Backend(owner).IsEnabled);
        }
        else form.Close();
    }

    [Theory]
    [InlineData("Resize")]
    [InlineData("SizeChanged")]
    [InlineData("ClientSizeChanged")]
    [InlineData("LocationChanged")]
    public void ObserverFailurePropagatesAfterStateAndLayoutCommitAndLaterObservers(string eventName)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        var error = new InvalidOperationException(eventName);
        int later = 0;
        EventInfo info = typeof(Form).GetEvent(eventName)!;
        EventHandler fail = (_, _) => throw error;
        EventHandler observe = (_, _) => { Assert.Equal(form.ClientSize, fill.Size); later++; };
        info.AddEventHandler(form, fail);
        info.AddEventHandler(form, observe);
        Action change = eventName == "LocationChanged"
            ? () => form.Location = new Point(123, -50)
            : () => form.Size = new Size(500, 400);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(change));
        Assert.Equal(1, later);
        Assert.Equal(form.ClientSize, fill.Size);
        change();
        Assert.Equal(1, later);
    }

    [Fact]
    public void FailuresFromDifferentSizeEventsAggregateWithoutSkippingClientNotification()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var first = new InvalidOperationException("resize");
        var second = new InvalidOperationException("size");
        bool client = false;
        form.Resize += (_, _) => throw first;
        form.SizeChanged += (_, _) => throw second;
        form.ClientSizeChanged += (_, _) => client = true;
        var error = Assert.Throws<AggregateException>(() => form.Size = new Size(500, 400));
        Assert.Equal(new Exception[] { first, second }, error.Flatten().InnerExceptions);
        Assert.True(client);
    }

    [Fact]
    public void ScaleOnlyAndFractionalBackendSizeDoNotInventIntegerGeometryEvents()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Size = new Size(400, 300) };
        List<string> events = Observe(form);
        Backend(form).SetRenderScale(1.75);
        Backend(form).Resize(new BackendSize(400.2, 300.2));
        Backend(form).Resize(new BackendSize(400.8, 300.8));
        form.Size = form.Size;
        form.ClientSize = form.ClientSize;
        Assert.Empty(events);
        Assert.Equal(new BackendSize(400.8, 300.8), Backend(form).ClientSize);
        Assert.Equal(new Size(400, 300), form.Size);
    }

    [Fact]
    public void DuplicateBackendCallbackInsideLayoutDoesNotScheduleAnEndlessLayout()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Size = new Size(400, 300) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        int layouts = 0, events = 0;
        fill.Layout += (_, _) => {
            Assert.True(++layouts < 10, "An identical callback requested endless layout.");
            Backend(form).Resized!(Backend(form).ClientSize, WindowResizeReason.User);
        };
        form.SizeChanged += (_, _) => events++;
        form.Size = new Size(500, 400);
        Assert.Equal(1, events);
        Assert.Equal(form.ClientSize, fill.Size);
    }

    [Fact]
    public void SuspendedRootCoalescesGeometryUntilLayoutCanComplete()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Size = new Size(400, 300) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        List<Size> values = [];
        form.SizeChanged += (_, _) => { Assert.Equal(form.ClientSize, fill.Size); values.Add(form.Size); };
        Root(form).SuspendLayout();
        form.Size = new Size(500, 400);
        form.Size = new Size(600, 450);
        Assert.Empty(values);
        Root(form).ResumeLayout();
        Assert.Equal(new[] { new Size(600, 450) }, values);
    }

    [Fact]
    public void SuspendedRootPublishesMoveWhenLayoutResumes()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        List<Point> values = [];
        form.LocationChanged += (_, _) => values.Add(form.Location);
        Root(form).SuspendLayout();
        form.Location = new Point(-300, 50);
        Assert.Empty(values);
        Root(form).ResumeLayout();
        Assert.Equal(new[] { new Point(-300, 50) }, values);
    }

    [Fact]
    public void ObserverDeferringNewSizeStopsOldNotificationsUntilLayoutResumes()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Size = new Size(400, 300) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        form.Resize += (_, _) => {
            if (form.Size.Width != 500) return;
            Root(form).SuspendLayout();
            form.Size = new Size(600, 450);
        };
        List<string> events = Observe(form);
        form.Size = new Size(500, 400);
        Assert.Empty(events);
        Assert.NotEqual(form.ClientSize, fill.Size);
        Root(form).ResumeLayout();
        Assert.Equal(new[] { "Resize", "Size", "Client" }, events);
        Assert.Equal(new Size(600, 450), form.Size);
        Assert.Equal(form.ClientSize, fill.Size);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseFromResizeStopsRemainingEventsAndCompletesModalOperation(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        var completion = modal ? form.ShowDialog(owner) : null;
        if (!modal) form.Show();
        int later = 0;
        form.Resize += (_, _) => form.Close();
        form.Resize += (_, _) => later++;
        form.SizeChanged += (_, _) => later++;
        form.ClientSizeChanged += (_, _) => later++;
        form.Size = new Size(500, 400);
        Assert.Equal(0, later);
        Assert.True(Backend(form).IsDisposed);
        Assert.True(Backend(owner).IsEnabled);
        if (modal) Assert.True(completion!.IsCompletedSuccessfully);
    }

    [Fact]
    public void ProtectedHooksUseTheSameOrderedDeduplicatedPath()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new GeometryForm();
        form.Calls.Clear();
        form.Size = new Size(500, 400);
        form.Location = new Point(123, 456);
        form.Size = form.Size;
        Assert.Equal(new[] { "Resize", "Size", "Client", "Location" }, form.Calls);
    }

    private sealed class GeometryForm : Form
    {
        internal List<string> Calls { get; } = [];
        protected override void OnResize(EventArgs e) { Calls.Add("Resize"); base.OnResize(e); }
        protected override void OnSizeChanged(EventArgs e) { Calls.Add("Size"); base.OnSizeChanged(e); }
        protected override void OnClientSizeChanged(EventArgs e) { Calls.Add("Client"); base.OnClientSizeChanged(e); }
        protected override void OnLocationChanged(EventArgs e) { Calls.Add("Location"); base.OnLocationChanged(e); }
    }

    private static List<string> Observe(Form form)
    {
        List<string> events = [];
        form.Resize += (_, _) => events.Add("Resize");
        form.SizeChanged += (_, _) => events.Add("Size");
        form.ClientSizeChanged += (_, _) => events.Add("Client");
        form.LocationChanged += (_, _) => events.Add("Location");
        return events;
    }

    private static HeadlessWindowImpl Backend(Form form) => Assert.IsType<HeadlessWindowImpl>(
        typeof(WindowBase).GetField("window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form));
    private static Control Root(Form form) => Assert.IsAssignableFrom<Control>(
        typeof(WindowBase).GetField("adapter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form));
}
