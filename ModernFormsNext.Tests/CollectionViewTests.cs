using System.Collections;
using System.Collections.ObjectModel;
using System.Drawing;
using System.ComponentModel;
using ModernFormsNext.DataBinding;
using ModernFormsNext.Virtualization;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class CollectionViewTests
{
    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void ActualConsumerRecycles100kItemsWithoutLosingMappedTemplateData(Orientation direction)
    {
        var source = Enumerable.Range(0, 100_003).Select(index => (object)$"Value {index}").ToArray();
        using var view = new Probe { Size = new(240, 120), ItemSize = new(100, 20), ItemOrientation = direction };
        var factory = new Factory(() => view.ItemsSource);
        view.ItemTemplate = factory;
        view.ItemsSource = source;
        for (int step = 0; step < 600; step++)
        {
            view.ScrollToItem(step * 7919 % source.Length);
            Assert.InRange(view.Controls.Count, 1, 16);
            foreach (int index in Enumerable.Range(view.RealizedRange.StartIndex, view.RealizedRange.Count))
                Assert.Equal(source[index], view.GetItemContainer(index)!.Text);
        }
        Assert.InRange(factory.Created.Count, 1, 16);
        Assert.True(view.GetVirtualizationSnapshot().ReusedContainerCount > 2_000);
        Assert.Same(source, view.ItemsSource);
    }

    [Fact]
    public void VariableConsumerRetainsContainerPlateauAcrossTheNativeJumpSequence()
    {
        const int count = 100_003;
        using var view = new VariableProbe
        {
            Size = new(260, 130), ItemSize = new(180, 24), VariableItemSize = true
        };
        view.ItemTemplate = new Factory(() => view.ItemsSource)
        {
            Create = () => new Label { Margin = Padding.Empty }
        };
        view.ItemsSource = Enumerable.Range(0, count).Select(index => (object)$"Value {index}").ToArray();
        view.InvalidateItemMeasurements();
        view.ScrollToItem(70_000);
        for (int step = 0; step < 500; step++) view.ScrollToItem(step * 7919 % count);
        long warmed = view.GetVirtualizationSnapshot().CreatedContainerCount;
        for (int step = 500; step < 6500; step++)
        {
            view.ScrollToItem(step * 7919 % count);
            var snapshot = view.GetVirtualizationSnapshot();
            Assert.True(snapshot.CreatedContainerCount == warmed,
                $"step={step}; warmed={warmed}; created={snapshot.CreatedContainerCount}; live={snapshot.RealizedContainerCount}; pool={snapshot.RecycledContainerCount}; range={snapshot.RealizedRange}");
        }
    }

    [Fact]
    public void DuplicateAndNullOccurrencesMapSelectionThroughIndexedChanges()
    {
        var duplicate = new object();
        var source = new ObservableCollection<object?> { duplicate, duplicate, null, "last" };
        using var view = new Probe { Size = new(240, 120), ItemsSource = source };
        view.SelectedIndex = 1;
        source.Insert(0, "first");
        Assert.Equal(2, view.SelectedIndex);
        Assert.Same(duplicate, view.SelectedItem);
        source.Move(2, 4);
        Assert.Equal(4, view.SelectedIndex);
        source.RemoveAt(1);
        Assert.Equal(3, view.SelectedIndex);
        source[3] = "replacement";
        Assert.Equal(-1, view.SelectedIndex);
        view.SelectedIndex = 1;
        Assert.Null(view.SelectedItem);
        Assert.Equal(1, view.SelectedIndex);
    }

    [Fact]
    public void PlainSourceRefreshRetiresSelectionAndRebindsSameCountValues()
    {
        object[] source = ["before", "other"];
        using var view = new Probe { Size = new(240, 120), ItemsSource = source, SelectedIndex = 0 };
        source[0] = "after";
        view.RefreshItems();
        Assert.Equal(-1, view.SelectedIndex);
        Assert.Equal("after", view.GetItemContainer(0)!.Text);
        view.ItemsSource = null;
        Assert.Null(view.ItemsSource);
        Assert.Equal(0, view.ItemCount);
        Assert.Empty(view.Controls);
    }

    [Fact]
    public void FactoryReplacementUsesOriginalCleanupAndRetainsSelectionAndCanonicalFocus()
    {
        using var view = new Probe { Size = new(240, 120), ItemSize = new(100, 20) };
        var first = new Factory(() => view.ItemsSource);
        var second = new Factory(() => view.ItemsSource);
        view.ItemTemplate = first;
        view.ItemsSource = Enumerable.Range(0, 100_003).Cast<object>().ToArray();
        view.SelectedIndex = 70_000;
        view.ScrollToItem(70_000);
        view.GetItemContainer(70_000)!.Select();
        view.ItemTemplate = second;
        Assert.All(first.Created, row => Assert.True(row.IsDisposed));
        Assert.Equal(first.Created.Count, first.Discarded.Count);
        Assert.Empty(second.Discarded);
        Assert.Equal(70_000, view.SelectedIndex);
        Assert.True(view.GetItemContainer(70_000)!.Focused);
        Assert.NotEmpty(second.Created);
    }

    [Fact]
    public void CleanupUsesSavedBindingInsteadOfResolvingOldIndexAfterRemoval()
    {
        var source = new ObservableCollection<object> { "zero", "one", "two" };
        using var view = new Probe { Size = new(240, 120), ItemSize = new(100, 20) };
        var factory = new Factory(() => view.ItemsSource);
        view.ItemTemplate = factory;
        view.ItemsSource = source;
        source.RemoveAt(2);
        Assert.Contains(factory.Cleared, entry => entry.Item is "two" && entry.Index == 2);
        Assert.DoesNotContain(view.Controls, row => row.Text == "two");
    }

    [Fact]
    public void SourceReplacementDuringTemplatePreparationDoesNotRestoreOldRows()
    {
        using var view = new Probe { Size = new(240, 120) };
        object[] newest = ["new source"];
        var factory = new Factory(() => view.ItemsSource);
        bool changed = false;
        factory.Prepare = (_, _) => { if (!changed) { changed = true; view.ItemsSource = newest; } };
        view.ItemTemplate = factory;
        view.ItemsSource = new object[] { "old source", "other" };
        Assert.Same(newest, view.ItemsSource);
        Assert.Equal(1, view.ItemCount);
        Assert.Equal("new source", view.GetItemContainer(0)!.Text);
        Assert.Single(view.Controls);
    }

    [Fact]
    public void FactoryReplacementDuringOldCleanupKeepsNewestFactory()
    {
        using var view = new Probe { Size = new(240, 120) };
        var first = new Factory(() => view.ItemsSource);
        var second = new Factory(() => view.ItemsSource);
        var newest = new Factory(() => view.ItemsSource);
        bool changed = false;
        first.Clear = (_, _) => { if (!changed) { changed = true; view.ItemTemplate = newest; } };
        view.ItemTemplate = first;
        view.ItemsSource = new object[] { "value", "other" };
        view.ItemTemplate = second;
        Assert.Same(newest, view.ItemTemplate);
        Assert.NotEmpty(newest.Created);
        Assert.All(first.Created, row => Assert.True(row.IsDisposed));
        Assert.All(second.Created, row => Assert.True(row.IsDisposed));
        Assert.DoesNotContain(view.Controls, row => !newest.Created.Contains(row));
    }

    [Fact]
    public void TemplateMayDisposeViewDuringPreparationWithoutLeakingReturnedContainer()
    {
        var view = new Probe { Size = new(240, 120) };
        var factory = new Factory(() => view.ItemsSource) { Prepare = (_, _) => view.Dispose() };
        view.ItemTemplate = factory;
        view.ItemsSource = new object[] { "value" };
        Assert.True(view.IsDisposed);
        Assert.All(factory.Created, row => Assert.True(row.IsDisposed));
        Assert.Equal(factory.Created.Count, factory.Discarded.Count);
    }

    [Fact]
    public void ThrowingFactoryDiscardStillDisposesAllOwnedControls()
    {
        var view = new Probe { Size = new(240, 120), ItemSize = new(100, 20) };
        var factory = new Factory(() => view.ItemsSource) { Discard = _ => throw new InvalidOperationException("factory cleanup") };
        view.ItemTemplate = factory;
        view.ItemsSource = new object[] { "one", "two", "three" };
        Assert.ThrowsAny<Exception>(view.Dispose);
        Assert.All(factory.Created, row => Assert.True(row.IsDisposed));
        Assert.Equal(factory.Created.Count, factory.Discarded.Count);
    }

    [Fact]
    public void PublicSourceAndTemplateMutationRejectOtherThreads()
    {
        using var view = new Probe();
        Exception? failure = null;
        // Keep creation/disposal on the same owning thread. A dedicated wrong thread also
        // avoids relying on an async continuation's scheduler to preserve UI affinity.
        var thread = new Thread(() =>
        {
            try
            {
                Assert.Throws<InvalidOperationException>(() => view.ItemsSource = new object[1]);
                Assert.Throws<InvalidOperationException>(() => view.ItemTemplate = new Factory(() => null));
                Assert.Throws<InvalidOperationException>(view.RefreshItems);
            }
            catch (Exception error) { failure = error; }
        });
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void FactoryLabelIsARealKeyboardTargetAcrossUnrealizedNavigation()
    {
        using var view = new Probe { Size = new(240, 120), ItemSize = new(100, 20) };
        view.ItemTemplate = new Factory(() => view.ItemsSource);
        view.ItemsSource = Enumerable.Range(0, 100_003).Cast<object>().ToArray();
        view.Select();
        view.Press(Keys.End);
        Assert.Equal(100_002, view.SelectedIndex);
        Assert.True(view.GetItemContainer(100_002)!.Focused);
        view.ScrollToItem(0);
        Assert.Equal(100_002, view.SelectedIndex);
        view.ScrollToItem(100_002);
        Assert.True(view.GetItemContainer(100_002)!.Focused);
    }

    [Fact]
    public void SelectionCallbackReplacingSourcePreventsStaleItemActivation()
    {
        using var view = new Probe { Size = new(240, 120), ItemsSource = new object[] { "old" } };
        int invoked = 0;
        view.ItemInvoked += (_, _) => invoked++;
        view.SelectedIndexChanged += (_, _) => { if (view.SelectedIndex >= 0) view.ItemsSource = new object[] { "new" }; };
        view.GetItemContainer(0)!.RaiseClick(new(MouseButtons.Left, 1, 5, 5, Point.Empty));
        Assert.Equal(0, invoked);
        Assert.Equal("new", view.GetItemContainer(0)!.Text);
    }

    [Fact]
    public void PartialPrepareFailureClearsAndDiscardsOriginalTemplateExactlyOnce()
    {
        using var view = new Probe { Size = new(240, 120) };
        var factory = new Factory(() => view.ItemsSource) { Prepare = (_, _) => throw new InvalidOperationException("partial prepare") };
        view.ItemTemplate = factory;
        Assert.ThrowsAny<Exception>(() => view.ItemsSource = new object[] { "value" });
        Assert.Single(factory.Created);
        Assert.Single(factory.Cleared);
        Assert.Single(factory.Discarded);
        Assert.True(factory.Created[0].IsDisposed);
        Assert.Empty(view.Controls);
    }

    [Fact]
    public void CompositeTemplateBindingsRefreshOnAttachAndNeverWriteCleanupIntoOldSource()
    {
        var old = new NotifyEntry("preserve old value");
        using var view = new Probe { Size = new(240, 120), BindingContext = new BindingContext() };
        view.ItemTemplate = new CompositeBindingFactory(view);
        view.ItemsSource = new object[] { old };
        Assert.Equal(old.Name, view.GetItemContainer(0)!.Controls[0].Text);
        var newest = new NotifyEntry("replacement");
        view.ItemsSource = new object[] { newest };
        Assert.Equal("preserve old value", old.Name);
        Assert.Equal("replacement", view.GetItemContainer(0)!.Controls[0].Text);
        newest.Name = "changed through binding";
        Assert.Equal(newest.Name, view.GetItemContainer(0)!.Controls[0].Text);
    }

    [Fact]
    public void CompositeBindingsAreRetiredBeforeRecycledChildrenBindAnotherOccurrence()
    {
        var source = Enumerable.Range(0, 200).Select(index => new NotifyEntry($"Entry {index}")).ToArray();
        using var view = new Probe { Size = new(240, 120), ItemSize = new(200, 30), BindingContext = new BindingContext() };
        view.ItemTemplate = new CompositeBindingFactory(view);
        view.ItemsSource = source;
        view.ScrollToItem(190);
        var label = view.GetItemContainer(190)!.Controls[0];
        Assert.Equal("Entry 190", label.Text);
        Assert.Single(label.DataBindings.Cast<Binding>());
        Assert.Equal("Entry 0", source[0].Name);
        source[190].Name = "updated current source";
        Assert.Equal(source[190].Name, label.Text);
    }

    [Fact]
    public void BindingRetirementCallbackReparentingRowPreservesNewTreeAndDescendantBindings()
    {
        var old = new NotifyEntry("old source");
        var newer = new NotifyEntry("new owner value");
        using var outside = new Panel { BindingContext = new BindingContext() };
        using var view = new Probe { Size = new(240, 120), BindingContext = new BindingContext() };
        view.ItemTemplate = new CompositeBindingFactory(view);
        view.ItemsSource = new object[] { old };
        var row = view.GetItemContainer(0)!;
        var child = row.Controls[0];
        row.DataBindings.Add(nameof(Control.Text), old, nameof(NotifyEntry.Name));
        bool moved = false;
        row.DataBindings.CollectionChanged += (_, e) =>
        {
            if (moved || e.Action != CollectionChangeAction.Refresh) return;
            moved = true;
            outside.Controls.Add(row);
            child.DataBindings.Clear();
            child.DataBindings.Add(nameof(Control.Text), newer, nameof(NotifyEntry.Name));
            row.RefreshItemContainerBindings();
        };
        Assert.ThrowsAny<Exception>(() => view.ItemsSource = new object[] { new NotifyEntry("replacement") });
        Assert.Same(outside, row.Parent);
        Assert.False(row.IsDisposed);
        Assert.Single(child.DataBindings.Cast<Binding>());
        Assert.Equal(newer.Name, child.Text);
        newer.Name = "still bound to new owner";
        Assert.Equal(newer.Name, child.Text);
    }

    [Fact]
    public void SourceIndexerReplacingSourceCancelsOldPreparationBeforeTemplateReadsNewIndex()
    {
        using var view = new Probe { Size = new(240, 120), ItemSize = new(200, 20) };
        object[] newest = ["new source"];
        bool changed = false;
        var source = new CallbackSource((index, _) =>
        {
            if (index == 5 && !changed) { changed = true; view.ItemsSource = newest; }
        });
        view.ItemTemplate = new Factory(() => view.ItemsSource);
        view.ItemsSource = source;
        Assert.Same(newest, view.ItemsSource);
        Assert.Single(view.Controls);
        Assert.Equal("new source", view.GetItemContainer(0)!.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceIndexerReplacementCancelsOldMeasureAndArrangeCallbacks(bool variable)
    {
        using var view = new LayoutProbe { Size = new(240, 120), ItemSize = new(200, 20), VariableItemSize = variable };
        object[] newest = ["new source"];
        bool changed = false;
        view.ItemsSource = new CallbackSource((index, read) =>
        {
            if (index == 1 && read == 2 && !changed) { changed = true; view.ItemsSource = newest; }
        });
        Assert.Same(newest, view.ItemsSource);
        Assert.False(view.OldCallback);
        Assert.Equal("new source", view.GetItemContainer(0)!.Text);
    }

    private sealed class LayoutProbe : CollectionView
    {
        internal bool OldCallback;
        public override bool Visible { get => true; set => base.Visible = value; }
        private void CheckItem(object? item)
        {
            if (ItemsSource is object[] current && current[0] is "new source" && item is string value && value.StartsWith("old ")) OldCallback = true;
        }
        protected override double MeasureItemContainer(Control container, object? item, int index, Size estimatedSize) { CheckItem(item); return 20; }
        protected override void OnItemContainerArranged(Control container, object? item, int index) => CheckItem(item);
    }

    [Fact]
    public void CountGetterReplacingSourceDoesNotLetOuterRequestOverwriteNewerSource()
    {
        using var view = new Probe { Size = new(240, 120) };
        object[] newest = ["new source"];
        bool changed = false;
        view.ItemsSource = new CallbackSource((_, _) => { }, () =>
        {
            if (!changed) { changed = true; view.ItemsSource = newest; }
        });
        Assert.Same(newest, view.ItemsSource);
        Assert.Equal("new source", view.GetItemContainer(0)!.Text);
    }

    [Fact]
    public void LayoutCountGetterCannotResetSelectionCommittedByNewerSourceCallback()
    {
        using var view = new Probe { Size = new(240, 120) };
        object[] newest = ["new source", "second"];
        bool armed = false;
        view.ItemsSource = new CallbackSource((_, _) => { }, () =>
        {
            if (!armed) return;
            armed = false;
            view.ItemsSource = newest;
            view.SelectedIndex = 0;
        });
        armed = true;
        view.PerformLayout();
        Assert.Same(newest, view.ItemsSource);
        Assert.Equal(0, view.SelectedIndex);
        Assert.Equal("new source", view.SelectedItem);
    }

    [Fact]
    public void SelectedItemReadReturnsNoRetiredValueAfterReentrantIndexerReplacement()
    {
        using var view = new Probe { Size = new(240, 120) };
        object[] newest = ["new source"];
        bool armed = false;
        view.ItemsSource = new CallbackSource((_, _) =>
        {
            if (!armed) return;
            armed = false;
            view.ItemsSource = newest;
        });
        view.SelectedIndex = 0;
        armed = true;
        Assert.Null(view.SelectedItem);
        Assert.Same(newest, view.ItemsSource);
        Assert.Equal(-1, view.SelectedIndex);
    }

    private sealed class CallbackSource(Action<int, int> read, Action? count = null) : IList
    {
        private readonly int[] reads = new int[20];
        public int Count { get { count?.Invoke(); return 20; } }
        public object? this[int index] { get { read(index, ++reads[index]); return $"old {index}"; } set => throw new NotSupportedException(); }
        public bool IsReadOnly => true;
        public bool IsFixedSize => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(object? value) => false;
        public int IndexOf(object? value) => -1;
        public void Insert(int index, object? value) => throw new NotSupportedException();
        public void Remove(object? value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
        public void CopyTo(Array array, int index) => throw new NotSupportedException();
        public IEnumerator GetEnumerator() => throw new InvalidOperationException("Full source enumeration is forbidden.");
    }

    private sealed class NotifyEntry(string name) : INotifyPropertyChanged
    {
        public string Name { get => name; set { name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class CompositeBindingFactory(CollectionView owner) : IItemContainerFactory<Control>
    {
        public Control CreateContainer()
        {
            var row = new Panel();
            row.Controls.Add(new Label { Dock = DockStyle.Fill });
            return row;
        }
        public void PrepareContainer(Control container, int index)
            => container.Controls[0].DataBindings.Add(nameof(Control.Text), owner.ItemsSource![index]!, nameof(NotifyEntry.Name), true, DataSourceUpdateMode.OnPropertyChanged);
        public void ClearContainer(Control container, int previousIndex) => container.Controls[0].Text = string.Empty;
        public void DiscardContainer(Control container) { }
    }

    private sealed class Probe : CollectionView
    {
        public override bool Visible { get => true; set => base.Visible = value; }
        internal void Press(Keys key) => OnKeyDown(new(key));
    }

    private sealed class VariableProbe : CollectionView
    {
        public override bool Visible { get => true; set => base.Visible = value; }
        protected override double MeasureItemContainer(Control container, object? item, int index, Size estimatedSize)
            => 31 + (index % 7 == 0 ? 12 : 0);
    }

    private sealed class Factory(Func<IList?> source) : IItemContainerFactory<Control>
    {
        internal List<Control> Created = [];
        internal List<Control> Discarded = [];
        internal List<(object? Item, int Index)> Cleared = [];
        private readonly Dictionary<Control, object?> bindings = new();
        internal Action<Control, int>? Prepare;
        internal Action<Control, int>? Clear;
        internal Action<Control>? Discard;
        internal Func<Control>? Create;
        public Control CreateContainer() { var row = Create?.Invoke() ?? new Label(); Created.Add(row); return row; }
        public void PrepareContainer(Control container, int index)
        {
            object? value = source()![index];
            bindings[container] = value;
            container.Text = value?.ToString() ?? string.Empty;
            Prepare?.Invoke(container, index);
        }
        public void ClearContainer(Control container, int previousIndex)
        {
            bindings.Remove(container, out var old);
            Cleared.Add((old, previousIndex));
            Clear?.Invoke(container, previousIndex);
        }
        public void DiscardContainer(Control container) { Discarded.Add(container); Discard?.Invoke(container); }
    }
}
