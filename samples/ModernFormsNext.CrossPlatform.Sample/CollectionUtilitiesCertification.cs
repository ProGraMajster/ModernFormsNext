using System.Collections.ObjectModel;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Virtualization;

namespace ModernFormsNext.Validation;

/// <summary>Finite production-control checks shared by explicit native Windows and Android runners.</summary>
/// <remarks>UI thread only. Native runners deliver input separately; these assertions never claim human QA.</remarks>
internal sealed class CollectionUtilitiesCertification : IDisposable
{
    private readonly Action<string> output;
    private readonly ObservableCollection<object> source = new(Enumerable.Range(0, 100_003).Select(index => (object)$"Page {index}"));
    private readonly RefreshView refresh;
    private int executions, activations;
    internal CarouselView Carousel { get; }
    internal int Executions => executions;
    internal double PullProgress => refresh.PullProgress;

    internal CollectionUtilitiesCertification(Form form, Action<string> output)
    {
        this.output = output;
        refresh = form.Controls.Add(new RefreshView { Dock = DockStyle.Fill, AccessibleName = "Native refresh host" });
        Carousel = refresh.Controls.Add(new CarouselView { Dock = DockStyle.Fill, AccessibleName = "Native pages" });
        Carousel.ItemTemplate = new PageTemplate(this);
        Carousel.ItemsSource = source;
        refresh.Command = new DelegateCommand(() => executions++);
        refresh.PerformLayout();
        refresh.BringToFront();
    }

    internal void Exercise(Action<Keys> nativeKey)
    {
        Carousel.SelectedIndex = 90_000;
        Check(Carousel.GetItemContainer(90_000) is not null, "jump-realizes-page");
        object selected = Carousel.SelectedItem!;
        source.Insert(0, "Inserted");
        Check(Carousel.SelectedIndex == 90_001 && ReferenceEquals(selected, Carousel.SelectedItem), "insert-keeps-occurrence");
        source.Move(90_001, 70_000);
        Check(Carousel.SelectedIndex == 70_000 && ReferenceEquals(selected, Carousel.SelectedItem), "move-keeps-occurrence");
        var collection = Carousel.AccessibilityObject.GetChild(0)!;
        var peer = collection.GetSelected()!;
        Carousel.SelectedIndex = 0;
        Check(peer.PerformAction(AccessibleActions.Focus) && Carousel.SelectedIndex == 70_000, "logical-peer-reveals-page");
        Carousel.Select();
        nativeKey(Keys.PageDown);
        Check(Carousel.SelectedIndex == 70_001, "native-page-key");
        output($"UTILITIES key-before-F5 focused={Carousel.Focused} available={refresh.CanRequestRefresh} enabled={refresh.Enabled} visible={refresh.Visible} executions={executions}");
        nativeKey(Keys.F5);
        output($"UTILITIES key-after-F5 focused={Carousel.Focused} available={refresh.CanRequestRefresh} executions={executions}");
        Check(executions == 1, "native-F5-refresh");
        var refreshAction = refresh.AccessibilityObject.GetChild(0)!;
        Check(refreshAction.PerformAction(AccessibleActions.Invoke) && executions == 2, "canonical-button-invoke-refresh");
        for (int index = 0; index < 150; index++) Carousel.SelectedIndex = index % 2 == 0 ? 90_000 : 0;
        var snapshot = Carousel.GetVirtualizationSnapshot();
        Check(snapshot.ItemCount == 100_004 && snapshot.RealizedContainerCount <= 7 &&
            snapshot.RealizedContainerCount + snapshot.RecycledContainerCount <= 12, "bounded-shared-page-pool");
        output($"UTILITIES counters logical={snapshot.ItemCount} live={snapshot.RealizedContainerCount} pool={snapshot.RecycledContainerCount} created={snapshot.CreatedContainerCount} reused={snapshot.ReusedContainerCount}");
        Carousel.SelectedIndex = 0;
    }

    internal Point Point(int x, int y) => refresh.PointToScreen(new(refresh.LogicalToDeviceUnits(x), refresh.LogicalToDeviceUnits(y)));
    internal Point PagePoint(double x, double y) => Carousel.PointToScreen(new(
        Carousel.LogicalToDeviceUnits((int)(Carousel.Width * x)), Carousel.LogicalToDeviceUnits((int)(Carousel.Height * y))));
    internal Point ButtonPoint()
    {
        var bounds = Carousel.GetItemContainer(Carousel.SelectedIndex)!.Controls.OfType<Button>().Single().AccessibilityObject.Bounds;
        return new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
    }
    internal void VerifyActivation() => Check(activations == 1, "native-interactive-page-button");
    internal void VerifyPage(int index, string category) => Check(Carousel.SelectedIndex == index, category);
    internal void VerifyRefresh(int expected, string category) => Check(executions == expected && refresh.PullProgress == 0, category);
    internal void VerifyGeometry()
    {
        var page = Carousel.GetItemContainer(Carousel.SelectedIndex)!;
        var bounds = page.ClientRectangle;
        var peer = Carousel.AccessibilityObject.GetChild(0)!.GetChild(Carousel.SelectedIndex)!;
        // The canonical painter scales both rectangle edges, then subtracts them. At a
        // fractional DPI this can differ from independently rounding a scalar dimension.
        output($"UTILITIES geometry scale={Carousel.ScaleFactor} page={Carousel.PageSize} device={bounds.Size} scaled={page.ScaledBounds.Size}");
        Check(page.Size == Carousel.PageSize && bounds.Size == page.ScaledBounds.Size &&
            peer.Bounds == new Rectangle(page.PointToScreen(bounds.Location), bounds.Size), "native-page-DPI-bounds");
        Check(Carousel.GetVirtualizationSnapshot().RealizedContainerCount <= 7, "native-page-resize-bounded");
    }
    internal void HideDuringPull() { refresh.Visible = false; refresh.Visible = true; }
    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException("UTILITIES " + category);
        output("PASS utilities-" + category);
    }

    public void Dispose()
    {
        int start = Math.Max(0, Carousel.SelectedIndex - 4);
        var pages = Enumerable.Range(start, Math.Min(9, Carousel.ItemCount - start))
            .Select(Carousel.GetItemContainer).OfType<Control>().ToArray();
        int disposed = 0;
        foreach (var page in pages) page.Disposed += (_, _) => disposed++;
        refresh.Parent?.Controls.Remove(refresh);
        refresh.Dispose();
        Check(disposed == pages.Length, "live-pages-disposed");
    }

    private sealed class PageTemplate(CollectionUtilitiesCertification owner) : IItemContainerFactory<Control>
    {
        public Control CreateContainer()
        {
            var page = new Panel { Margin = Padding.Empty };
            var button = page.Controls.Add(new Button { Dock = DockStyle.Bottom, Height = 40, Text = "Open page" });
            page.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
            button.BringToFront();
            button.Click += (_, _) => owner.activations++;
            return page;
        }
        public void PrepareContainer(Control container, int index) => container.Controls.OfType<Label>().Single().Text = owner.source[index].ToString()!;
        public void ClearContainer(Control container, int previousIndex) => container.Controls.OfType<Label>().Single().Text = string.Empty;
        public void DiscardContainer(Control container) => container.Dispose();
    }
}
