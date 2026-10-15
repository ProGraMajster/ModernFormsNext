using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using ModernFormsNext.DataBinding;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Virtualization;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ItemsControlTests
{
    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void Actual100kControlsStayBoundedAcrossJumpsAndProgrammaticScrollbarChanges(Orientation orientation)
    {
        using var view = new TestItems { Size = new(200, 200), ItemSize = new(20, 20), ItemOrientation = orientation };
        view.SetSource(Enumerable.Range(0, 100_000).Select(i => (object)$"item-{i}").ToArray());
        view.PerformLayout();
        for (int iteration = 0; iteration < 1_000; iteration++)
        {
            int index = iteration * 7919 % 100_000;
            view.ScrollToItem(index);
            Assert.NotNull(view.GetItemContainer(index));
            var snapshot = view.GetVirtualizationSnapshot();
            Assert.Equal(100_000, snapshot.ItemCount);
            Assert.InRange(snapshot.RealizedContainerCount, 1, 16);
            Assert.Equal(snapshot.RealizedContainerCount, view.Controls.Count);
            Assert.True(snapshot.CreatedContainerCount <= 16);
            Assert.True(snapshot.RecycledContainerCount <= 64);
            Assert.All(view.Controls, row => Assert.Equal(view.ValueAt(view.IndexOf(row)), row.Text));
        }
        var scroll = orientation == Orientation.Vertical ? view.VerticalScrollProperties : view.HorizontalScrollProperties;
        scroll.Value = 200_000;
        Assert.Contains(10_000, Enumerable.Range(view.RealizedRange.StartIndex, view.RealizedRange.Count));
        Assert.True(view.GetVirtualizationSnapshot().ReusedContainerCount > 5_000);
    }

    [Fact]
    public void OccurrenceSelectionAndBindingsMapDuplicateValuesAcrossMutations()
    {
        var values = new ObservableCollection<object> { "same", "same", "third", "fourth" };
        using var view = new TestItems { Size = new(240, 120), ItemSize = new(100, 20) };
        view.SetSource(values);
        view.SelectedIndex = 1;
        var selectedContainer = view.GetItemContainer(1);
        values.Insert(0, "inserted");
        Assert.Equal(2, view.SelectedIndex);
        Assert.Same(selectedContainer, view.GetItemContainer(2));
        values.Move(2, 4);
        Assert.Equal(4, view.SelectedIndex);
        Assert.Same(selectedContainer, view.GetItemContainer(4));
        values.RemoveAt(0);
        Assert.Equal(3, view.SelectedIndex);
        values[3] = "replacement";
        Assert.Equal(-1, view.SelectedIndex);
        Assert.All(view.Controls, row => Assert.Equal(view.ValueAt(view.IndexOf(row)), row.Text));
        Assert.Contains(view.Cleared, binding => Equals(binding.Item, "same") && binding.Index == 1);
        values.Clear();
        Assert.Empty(view.Controls);
        Assert.Equal(0, view.ItemCount);
    }

    [Fact]
    public void KeyboardNavigatesUnrealizedItemAndFocusRecoversAfterRecycling()
    {
        using var view = new TestItems { Size = new(200, 100), ItemSize = new(100, 20) };
        view.SetSource(Enumerable.Range(0, 100_000).Select(i => (object)$"item-{i}").ToArray());
        view.Select();
        var end = new KeyEventArgs(Keys.End);
        view.Key(end);
        Assert.True(end.Handled);
        Assert.Equal(99_999, view.SelectedIndex);
        Assert.True(view.GetItemContainer(99_999)!.Focused);
        view.ScrollToItem(0);
        Assert.True(view.Focused);
        Assert.Equal(99_999, view.SelectedIndex);
        view.ScrollToItem(99_999);
        Assert.True(view.GetItemContainer(99_999)!.Focused);
        view.ScrollToItem(0);
        using var other = new VisibleRoot();
        var outside = other.Controls.Add(new Button());
        outside.Select();
        view.ScrollToItem(99_999);
        Assert.True(outside.Focused);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeyboardNavigationPreservesNewerCallbackFocus(bool duringLayout)
    {
        using var root = new VisibleRoot();
        var view = root.Controls.Add(new TestItems { Size = new(200, 100), ItemSize = new(100, 20) });
        var outside = root.Controls.Add(new Button());
        view.SetSource(Enumerable.Range(0, 100_000).Select(i => (object)$"item-{i}").ToArray());
        view.Select();
        if (duringLayout) view.Arranging = () => outside.Select();
        else view.SelectedIndexChanged += (_, _) => outside.Select();

        view.Key(new KeyEventArgs(Keys.End));

        Assert.Equal(99_999, view.SelectedIndex);
        Assert.True(outside.Focused);
        Assert.False(view.Focused);
        Assert.All(view.Controls, row => Assert.False(row.Focused));
    }

    [Fact]
    public void ReentrantCleanupModeChangeKeepsNewestVirtualizationAndScrollState()
    {
        using var view = new TestItems { Size = new(200, 100), ItemSize = new(100, 20) };
        view.SetSource(Enumerable.Range(0, 100).Select(i => (object)$"item-{i}").ToArray());
        view.Clearing = () => { view.Clearing = null; view.VirtualizationEnabled = true; };

        view.VirtualizationEnabled = false;

        Assert.True(view.VirtualizationEnabled);
        Assert.True(view.AutoScroll);
        view.ScrollToItem(99);
        Assert.NotNull(view.GetItemContainer(99));
    }

    [Fact]
    public void VariableMeasurementAndInsertRetainLeadingOccurrenceAnchor()
    {
        var values = new ObservableCollection<object>(Enumerable.Range(0, 100_000).Select(i => (object)$"item-{i}"));
        using var view = new TestItems { Size = new(200, 100), ItemSize = new(100, 20), VariableItemSize = true, Extent = 30 };
        view.SetSource(values);
        view.ScrollToItem(50_000);
        int before = view.RealizedRange.StartIndex + view.CacheItemCount;
        var anchoredValue = view.ValueAt(before);
        int top = view.GetItemContainer(before)!.Top;
        values.Insert(0, "inserted");
        Assert.Equal(anchoredValue, view.ValueAt(before + 1));
        Assert.Equal(top, view.GetItemContainer(before + 1)!.Top);
        view.Extent = 35;
        view.InvalidateItemMeasurements();
        Assert.Equal(top, view.GetItemContainer(before + 1)!.Top);
        Assert.True(view.GetVirtualizationSnapshot().MeasuredItemCount > 0);
    }

    [Fact]
    public void WrapResizeChangesBoundedRowsWithoutChangingLogicalSelection()
    {
        using var view = new TestItems { Size = new(200, 100), ItemSize = new(40, 20), ItemsLayout = ItemsLayout.Wrap };
        view.SetSource(Enumerable.Range(0, 100_001).Select(i => (object)$"item-{i}").ToArray());
        view.SelectedIndex = 99_999;
        view.ScrollToItem(99_999);
        Assert.NotNull(view.GetItemContainer(99_999));
        Assert.InRange(view.Controls.Count, 1, 36);
        view.Size = new(120, 160);
        view.ScrollToItem(99_999);
        Assert.Equal(99_999, view.SelectedIndex);
        Assert.NotNull(view.GetItemContainer(99_999));
        Assert.InRange(view.Controls.Count, 1, 32);
        Assert.Throws<InvalidOperationException>(() => view.VariableItemSize = true);
    }

    [Fact]
    public void SelectionCallbackCanReplaceSourceWithoutOldBindingsReturning()
    {
        var source = new ObservableCollection<object> { "one", "two", "three" };
        using var view = new TestItems { Size = new(200, 100) };
        view.SetSource(source);
        view.SelectedIndex = 1;
        view.SelectedIndexChanged += (_, _) => view.SetSource(new object[] { "new" });
        source.RemoveAt(0);
        Assert.Equal(1, view.ItemCount);
        Assert.Equal(-1, view.SelectedIndex);
        Assert.Equal("new", view.GetItemContainer(0)!.Text);
        source.Add("unsubscribed");
        Assert.Equal(1, view.ItemCount);
    }

    [Fact]
    public void PrepareMayDisposeHostAndCleanupPartialBindingExactlyOnce()
    {
        using var view = new TestItems { Size = new(200, 100) };
        view.Preparing = (_, _, _) => view.Dispose();
        view.SetSource(new object[] { "one", "two" });
        Assert.True(view.IsDisposed);
        Assert.Single(view.Cleared);
        Assert.All(view.CreatedRows, row => Assert.True(row.IsDisposed));
    }

    [Fact]
    public void BindingListPropertyChangeKeepsSelectionAndRefreshesCanonicalBinding()
    {
        var value = new NotifyValue("before");
        var source = new BindingList<NotifyValue> { value };
        using var view = new BoundItems { Size = new(200, 100), BindingContext = new BindingContext() };
        view.SetSource(source);
        view.SelectedIndex = 0;
        value.Name = "after";
        Assert.Equal(0, view.SelectedIndex);
        Assert.Equal("after", view.GetItemContainer(0)!.Text);
        Assert.Equal(1, view.GetItemContainer(0)!.DataBindings.Count);
        view.SetSource(new object[] { new NotifyValue("replacement") });
        value.Name = "retired";
        Assert.Equal("replacement", view.GetItemContainer(0)!.Text);
    }

    [Theory]
    [InlineData(ListChangedType.ItemChanged, -1, -1)]
    [InlineData(ListChangedType.ItemChanged, 2, -1)]
    [InlineData(ListChangedType.ItemAdded, -1, -1)]
    [InlineData(ListChangedType.ItemMoved, -1, 0)]
    [InlineData(ListChangedType.ItemMoved, 0, -1)]
    public void UnknownBindingListIndicesConservativelyResetInsteadOfLookingUpAnInvalidItem(ListChangedType type, int newIndex, int oldIndex)
    {
        var source = new NotificationBindingList { new NotifyValue("borrowed") };
        using var view = new BoundItems { Size = new(200, 100), BindingContext = new BindingContext() };
        view.SetSource(source);
        view.SelectedIndex = 0;
        var notification = type == ListChangedType.ItemChanged
            ? new ListChangedEventArgs(type, newIndex, TypeDescriptor.GetProperties(typeof(NotifyValue))[nameof(NotifyValue.Name)])
            : new ListChangedEventArgs(type, newIndex, oldIndex);

        source.Notify(notification);

        Assert.Equal(-1, view.SelectedIndex);
        Assert.Equal(1, view.ItemCount);
        Assert.Equal("borrowed", view.GetItemContainer(0)!.Text);
    }

    [Fact]
    public void RecyclingCleanupNeverWritesEmptyTextBackThroughAnOldTwoWayBinding()
    {
        var oldValue = new NotifyValue("preserve source");
        using var view = new TwoWayItems { Size = new(200, 100), BindingContext = new BindingContext() };
        view.SetSource(new object[] { oldValue });
        view.SetSource(new object[] { new NotifyValue("replacement") });
        Assert.Equal("preserve source", oldValue.Name);
        Assert.Equal("replacement", view.GetItemContainer(0)!.Text);
    }

    [Fact]
    public void ListViewPreservesLegacyApiAndOptInMapsActualItemsWithoutFullRendererEnumeration()
    {
        using var root = new VisibleRoot();
        var view = root.Controls.Add(new ListView { Size = new(240, 160) });
        Assert.False(view.VirtualizationEnabled);
        view.SuspendLayout();
        for (int i = 0; i < 100_000; i++) view.Items.Add($"item-{i}");
        view.ResumeLayout();
        view.SelectedItem = view.Items[90_000];
        Assert.Same(view.Items[90_000], view.SelectedItem);
        view.VirtualizationEnabled = true;
        view.ScrollToItem(90_000);
        Assert.Equal(90_000, view.SelectedIndex);
        Assert.NotNull(view.GetItemContainer(90_000));
        Assert.InRange(view.Controls.Count, 1, 20);
        Assert.False(view.Items[90_000].Bounds.IsEmpty);
        Assert.True(view.Items[0].Bounds.IsEmpty);
        view.Items.Move(90_000, 99_999);
        Assert.Equal(99_999, view.SelectedIndex);
        Assert.Same(view.Items[99_999], view.SelectedItem);
        view.Items.RemoveAt(99_999);
        Assert.Null(view.SelectedItem);
        Assert.Equal(-1, view.SelectedIndex);
    }

    [Fact]
    public void EnablingListViewVirtualizationNormalizesLegacySelectionAndKeepsItemSelectionUsable()
    {
        using var view = new ListView();
        var first = new ListViewItem { Text = "first", Selected = true };
        var second = new ListViewItem { Text = "second", Selected = true };
        view.Items.Add(first);
        view.Items.Add(second);
        Assert.Equal(2, view.Items.Count(item => item.Selected));

        view.VirtualizationEnabled = true;

        Assert.Equal(0, view.SelectedIndex);
        Assert.Same(first, view.SelectedItem);
        Assert.True(first.Selected);
        Assert.False(second.Selected);
        second.Selected = true;
        Assert.Equal(1, view.SelectedIndex);
        Assert.Same(second, view.SelectedItem);
        Assert.False(first.Selected);
        view.VirtualizationEnabled = false;
        Assert.Same(second, view.SelectedItem);
        Assert.Single(view.Items, item => item.Selected);
    }

    [Fact]
    public void EnablingListViewVirtualizationPreservesNewerTemplateSelectionWhileRetiringLegacyFlags()
    {
        using var root = new VisibleRoot();
        var view = root.Controls.Add(new SelectionDuringPrepareListView { Size = new(300, 200) });
        view.Items.Add(new ListViewItem { Text = "first", Selected = true });
        view.Items.Add(new ListViewItem { Text = "second", Selected = true });
        var newest = new ListViewItem { Text = "newest" };
        view.Items.Add(newest);
        view.Preparing = () => view.SelectedItem = newest;

        view.VirtualizationEnabled = true;

        Assert.Equal(2, view.SelectedIndex);
        Assert.Same(newest, view.SelectedItem);
        Assert.True(newest.Selected);
        Assert.Single(view.Items, item => item.Selected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReenablingListViewVirtualizationAdoptsSelectionChangedOrClearedInLegacyMode(bool clear)
    {
        using var view = new ListView();
        var first = new ListViewItem { Text = "first" };
        var second = new ListViewItem { Text = "second" };
        view.Items.Add(first);
        view.Items.Add(second);
        view.VirtualizationEnabled = true;
        view.SelectedItem = first;
        view.VirtualizationEnabled = false;
        view.SelectedItem = clear ? null : second;

        view.VirtualizationEnabled = true;

        Assert.Equal(clear ? -1 : 1, view.SelectedIndex);
        Assert.Same(clear ? null : second, view.SelectedItem);
        Assert.False(first.Selected);
        Assert.Equal(!clear, second.Selected);
    }

    [Fact]
    public void RuntimeSourceAndPropertiesRejectAnotherThread()
    {
        using var view = new TestItems();
        Exception? observed = null;
        var worker = new Thread(() => observed = Record.Exception(() => view.ItemSize = new(100, 30)));
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(observed);
    }

    [Fact]
    public void ListViewOwnedCollectionRejectsWrongThreadBeforeMutation()
    {
        using var view = new ListView();
        Exception? observed = null;
        var worker = new Thread(() => observed = Record.Exception(() => view.Items.Add("wrong thread")));
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(observed);
        Assert.Empty(view.Items);
    }

    [Fact]
    public void PreselectedInsertedOrReplacedItemCommitsVirtualizedLogicalSelection()
    {
        using var root = new VisibleRoot();
        var view = root.Controls.Add(new ListView { VirtualizationEnabled = true });
        var first = new ListViewItem { Text = "first", Selected = true };
        view.Items.Add(first);
        Assert.Equal(0, view.SelectedIndex);
        Assert.Same(first, view.SelectedItem);
        var second = new ListViewItem { Text = "second", Selected = true };
        view.Items[0] = second;
        Assert.Equal(0, view.SelectedIndex);
        Assert.Same(second, view.SelectedItem);
        Assert.False(first.Selected);
    }

    [Fact]
    public void ReentrantPrepareMutationKeepsNewestSourceAndBindingIndices()
    {
        var source = new ObservableCollection<object> { "one", "two", "three" };
        using var view = new TestItems { Size = new(200, 100) };
        bool changed = false;
        view.Preparing = (_, _, _) => { if (!changed) { changed = true; source.Insert(0, "new"); } };
        view.SetSource(source);
        Assert.Equal(4, view.ItemCount);
        for (int index = 0; index < 4; index++) Assert.Equal(source[index], view.GetItemContainer(index)!.Text);
    }

    [Fact]
    public void DetachAndReattachPreserveLogicalSelectionWithoutStealingNewRootFocus()
    {
        using var first = new VisibleRoot();
        using var second = new VisibleRoot();
        var view = first.Controls.Add(new TestItems { Size = new(200, 100), ItemSize = new(100, 20) });
        view.SetSource(Enumerable.Range(0, 100).Select(i => (object)$"entry-{i}").ToArray());
        view.SelectedIndex = 50;
        view.ScrollToItem(50);
        view.GetItemContainer(50)!.Select();
        first.Controls.Remove(view);
        var outside = second.Controls.Add(new Button());
        outside.Select();
        second.Controls.Add(view);
        view.ScrollToItem(50);
        Assert.Equal(50, view.SelectedIndex);
        Assert.True(outside.Focused);
        Assert.False(view.GetItemContainer(50)!.Focused);
    }

    [Fact]
    public void PlainSourceResetClearsUnknownIdentityAndRebindsSameCountValues()
    {
        var source = new ArrayList { "one", "two" };
        using var view = new TestItems { Size = new(200, 100) };
        view.SetSource(source);
        view.SelectedIndex = 1;
        source[1] = "new";
        view.Refresh();
        Assert.Equal(-1, view.SelectedIndex);
        Assert.Equal("new", view.GetItemContainer(1)!.Text);
        source.Add("added without notification");
        view.PerformLayout();
        Assert.Equal(3, view.ItemCount);
        Assert.Equal(source[2], view.GetItemContainer(2)!.Text);
    }

    [Fact]
    public void NestedScrollToItemReusesCanonicalRevealThroughBothViewports()
    {
        using var root = new VisibleRoot { Size = new(1000, 800) };
        var outer = root.Controls.Add(new Panel { AutoScroll = true, Size = new(300, 200) });
        var view = outer.Controls.Add(new TestItems { Bounds = new(400, 300, 240, 120), ItemSize = new(200, 20) });
        view.SetSource(Enumerable.Range(0, 100_000).Select(i => (object)$"entry-{i}").ToArray());
        outer.PerformLayout();
        view.ScrollToItem(99_999);
        Assert.True(outer.HorizontalScrollProperties.Value > 0);
        Assert.True(outer.VerticalScrollProperties.Value > 0);
        Assert.True(view.VerticalScrollProperties.Value > 0);
        var row = view.GetItemContainer(99_999)!;
        Assert.Equal(AccessibleStates.None, row.AccessibilityObject.State & AccessibleStates.Offscreen);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    public void ListViewConvertsLogicalContainerBoundsExactlyOnceAtWindowScale(double scale)
    {
        using var window = new DpiTestWindow(scale);
        var view = window.Controls.Add(new ListView { Size = new(240, 120), Padding = new Padding(3),
            ItemsLayout = ItemsLayout.Stack, ItemSize = new(200, 20) });
        for (int i = 0; i < 100; i++) view.Items.Add($"entry-{i}");
        view.VirtualizationEnabled = true;
        view.ScrollToItem(50);
        var row = view.GetItemContainer(50)!;
        var item = view.Items[50];
        Assert.Equal(view.LogicalToDeviceUnits(row.Left), item.Bounds.Left);
        Assert.Equal(view.LogicalToDeviceUnits(row.Top), item.Bounds.Top);
        Assert.Equal(view.LogicalToDeviceUnits(row.Width) - view.LogicalToDeviceUnits(6), item.Bounds.Width);
        Assert.Equal(view.LogicalToDeviceUnits(row.Height) - view.LogicalToDeviceUnits(6), item.Bounds.Height);
        Assert.Equal(new Point(0, view.VerticalScrollProperties.Value), view.TouchScrollPosition);
    }

    [Fact]
    public void WrapKeyboardUsesRowsAndResizeRetainsTheLeadingOccurrence()
    {
        using var view = new TestItems { Size = new(215, 100), ItemSize = new(40, 20), ItemsLayout = ItemsLayout.Wrap };
        view.SetSource(Enumerable.Range(0, 100_000).Select(i => (object)$"entry-{i}").ToArray());
        view.SelectedIndex = 5;
        view.Key(new(Keys.Down));
        Assert.Equal(10, view.SelectedIndex);
        view.ScrollToItem(50_000);
        int index = view.RealizedRange.StartIndex + view.CacheItemCount;
        int top = view.GetItemContainer(index)!.Top;
        view.Size = new(135, 180);
        Assert.Equal(top, view.GetItemContainer(index)!.Top);
    }

    [Fact]
    public void ClearFailureDiscardsAllOwnedContainersAndPreservesBorrowedSource()
    {
        var source = new ObservableCollection<object> { "one", "two", "three" };
        using var view = new TestItems { Size = new(200, 100) };
        view.SetSource(source);
        view.Clearing = () => throw new InvalidOperationException("template cleanup");
        Assert.ThrowsAny<Exception>(() => source.Clear());
        Assert.All(view.CreatedRows, row => Assert.True(row.IsDisposed));
        Assert.Empty(source);
    }

    [Fact]
    public void NeverSettlingLayoutCallbacksStopAtCanonicalBoundedCompletion()
    {
        using var view = new TestItems { Size = new(200, 100) };
        view.Arranging = () => view.ItemSize = new(100, view.ItemSize.Height == 20 ? 21 : 20);
        var failure = Assert.Throws<InvalidOperationException>(() => view.SetSource(new object[] { "one" }));
        Assert.Contains("64 passes", failure.Message);
    }

    [Fact]
    public void FontAndThemeMetricInvalidationRetainCurrentLogicalAnchor()
    {
        using var view = new TestItems { Size = new(200, 100), ItemSize = new(100, 20), VariableItemSize = true, Extent = 30 };
        view.SetSource(Enumerable.Range(0, 100_000).Select(i => (object)$"entry-{i}").ToArray());
        view.ScrollToItem(50_000);
        int index = view.RealizedRange.StartIndex + view.CacheItemCount;
        int top = view.GetItemContainer(index)!.Top;
        view.Extent = 35;
        view.FontChangedForTest();
        Assert.Equal(top, view.GetItemContainer(index)!.Top);
        view.Extent = 40;
        view.ThemeChangedForTest();
        Assert.Equal(top, view.GetItemContainer(index)!.Top);
    }

    private class TestItems : ItemsControl
    {
        internal readonly List<Control> CreatedRows = [];
        internal readonly List<(object? Item, int Index)> Cleared = [];
        internal Action<Control, object?, int>? Preparing;
        internal Action? Clearing;
        internal Action? Arranging;
        internal double Extent = 20;
        private IList? values;
        internal void SetSource(IList source) { values = source; SetItemsSource(source); }
        internal void Refresh() => RefreshItemsSource();
        internal void FontChangedForTest() => OnFontChanged(EventArgs.Empty);
        internal void ThemeChangedForTest() => OnThemeChanged(EventArgs.Empty);
        internal string? ValueAt(int index) => values![index]?.ToString();
        internal int IndexOf(Control row)
        {
            for (int i = RealizedRange.StartIndex; i < RealizedRange.EndIndex; i++) if (ReferenceEquals(row, GetItemContainer(i))) return i;
            throw new InvalidOperationException("Unmapped row");
        }
        internal void Key(KeyEventArgs e) => OnKeyDown(e);
        public override bool Visible { get => true; set => base.Visible = value; }
        protected override Control CreateItemContainer() { var row = base.CreateItemContainer(); CreatedRows.Add(row); return row; }
        protected override void PrepareItemContainer(Control row, object? item, int index)
        { base.PrepareItemContainer(row, item, index); Preparing?.Invoke(row, item, index); }
        protected override void ClearItemContainer(Control row, object? item, int index)
        { Cleared.Add((item, index)); base.ClearItemContainer(row, item, index); Clearing?.Invoke(); }
        protected override double MeasureItemContainer(Control row, object? item, int index, Size estimate) => Extent;
        protected override void OnItemContainerArranged(Control row, object? item, int index) => Arranging?.Invoke();
    }

    private sealed class SelectionDuringPrepareListView : ListView
    {
        internal Action? Preparing;
        protected override void PrepareItemContainer(Control row, object? item, int index)
        {
            base.PrepareItemContainer(row, item, index);
            var callback = Preparing;
            Preparing = null;
            callback?.Invoke();
        }
    }

    private sealed class NotificationBindingList : BindingList<NotifyValue>
    {
        internal void Notify(ListChangedEventArgs notification) => OnListChanged(notification);
    }

    private sealed class BoundItems : TestItems
    {
        protected override void PrepareItemContainer(Control row, object? item, int index)
            => row.DataBindings.Add(nameof(Control.Text), item!, nameof(NotifyValue.Name));
    }

    private sealed class TwoWayItems : TestItems
    {
        protected override void PrepareItemContainer(Control row, object? item, int index)
            => row.DataBindings.Add(nameof(Control.Text), item!, nameof(NotifyValue.Name), true, DataSourceUpdateMode.OnPropertyChanged);
    }

    private sealed class NotifyValue(string name) : INotifyPropertyChanged
    {
        public string Name { get => name; set { name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class VisibleRoot : Control
    {
        public override bool Visible { get => true; set => base.Visible = value; }
    }

    private sealed class DpiTestWindow : WindowBase
    {
        internal DpiTestWindow(double scale) : this(DispatchProxy.Create<IWindowBaseImpl, DpiWindowProxy>(), scale) { }
        private DpiTestWindow(IWindowBaseImpl implementation, double scale) : base(implementation)
            => ((DpiWindowProxy)implementation).RenderScaling = scale;
    }

    private class DpiWindowProxy : DispatchProxy
    {
        public double RenderScaling { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "get_RenderScaling" ? RenderScaling : method?.ReturnType.IsValueType == true && method.ReturnType != typeof(void)
                ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
