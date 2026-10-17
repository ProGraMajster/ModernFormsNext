using System.Collections.ObjectModel;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Virtualization;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class CollectionViewAccessibilityTests
{
    [Fact]
    public void CanonicalPeerPreservesDistinctDuplicateOccurrencesThroughIndexedMutations()
    {
        var value = new object();
        var source = new ObservableCollection<object?> { value, value, null, "last" };
        using var view = new Probe { Size = new(240, 120), ItemsSource = source };
        var root = view.AccessibilityObject;
        var first = root.GetChild(0)!;
        var second = root.GetChild(1)!;
        Assert.NotEqual(first.RuntimeId, second.RuntimeId);
        source.Insert(0, "inserted");
        Assert.Same(first, root.GetChild(1));
        Assert.Same(second, root.GetChild(2));
        source.Move(2, 4);
        Assert.Same(second, root.GetChild(4));
        source.RemoveAt(1);
        Assert.Equal(AccessibilityView.Hidden, first.View);
        Assert.Same(second, root.GetChild(3));
        source[3] = "replaced";
        Assert.Equal(AccessibilityView.Hidden, second.View);
        Assert.False(second.PerformAction(AccessibleActions.Select));
        Assert.NotSame(second, root.GetChild(3));
    }

    [Fact]
    public void SourceReplacementAndResetRetirePreviouslyRequestedPeers()
    {
        object[] source = ["same", "same"];
        using var view = new Probe { Size = new(240, 120), ItemsSource = source };
        var root = view.AccessibilityObject;
        var old = root.GetChild(0)!;
        view.RefreshItems();
        Assert.Equal(AccessibilityView.Hidden, old.View);
        var afterReset = root.GetChild(0)!;
        view.ItemsSource = new object[] { "same", "same" };
        Assert.Equal(AccessibilityView.Hidden, afterReset.View);
        Assert.NotSame(afterReset, root.GetChild(0));
        view.Dispose();
        Assert.Equal(0, root.GetChildCount());
        Assert.Null(root.GetChild(0));
    }

    [Fact]
    public void LogicalCountAndIndexedSiblingNavigationDoNotRead100kSourceValues()
    {
        var source = new CountingSource(100_003);
        using var view = new Probe { Size = new(240, 120), ItemsSource = source };
        var root = view.AccessibilityObject;
        int before = source.Reads;
        Assert.Equal(source.Count, root.GetChildCount());
        var last = root.GetChild(100_002)!;
        Assert.Same(last, root.Navigate(AccessibleNavigation.LastChild));
        var previous = last.Navigate(AccessibleNavigation.Previous)!;
        Assert.Same(previous, root.GetChild(100_001));
        Assert.Equal(before, source.Reads);
        Assert.Equal("Entry 100002", last.Name);
        Assert.Equal(before + 1, source.Reads);
    }

    [Fact]
    public void UnrealizedPeerCanSelectRevealFocusAndInvokeWithoutChangingIdentity()
    {
        using var view = new Probe { Size = new(240, 120), ItemsSource = Enumerable.Range(0, 100_003).Cast<object>().ToArray() };
        var root = view.AccessibilityObject;
        var item = root.GetChild(90_000)!;
        Assert.True(item.Bounds.IsEmpty);
        Assert.True(item.PerformAction(AccessibleActions.Select));
        Assert.Same(item, root.GetSelected());
        Assert.True(item.PerformAction(AccessibleActions.Focus));
        Assert.Same(item, root.GetFocused());
        Assert.False(item.Bounds.IsEmpty);
        CollectionViewItemEventArgs? invoked = null;
        view.ItemInvoked += (_, e) => invoked = e;
        Assert.True(item.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(90_000, invoked!.Index);
        Assert.Equal(90_000, invoked.Item);
        view.ScrollToItem(0);
        Assert.Same(item, root.GetSelected());
        Assert.True(item.Bounds.IsEmpty);
        Assert.True(item.PerformAction(AccessibleActions.ScrollIntoView));
        Assert.Same(item, root.GetChild(90_000));
    }

    [Fact]
    public void TemplateControlsAreChildrenOfCanonicalOccurrenceAndInteractiveFocusStaysReal()
    {
        using var view = new Probe { Size = new(240, 120), ItemSize = new(200, 50) };
        view.ItemTemplate = new EditorFactory();
        view.ItemsSource = new object[] { "value" };
        var root = view.AccessibilityObject;
        var item = root.GetChild(0)!;
        var row = view.GetItemContainer(0)!;
        Assert.Equal(1, root.GetChildCount());
        Assert.Same(item, row.AccessibilityObject.Parent);
        Assert.Same(row.AccessibilityObject, item.GetChild(0));
        Assert.Equal(1, item.GetChildCount());
        var editor = row.Controls.OfType<TextBox>().Single();
        editor.Select();
        Assert.Same(editor.AccessibilityObject, root.GetFocused());
        Assert.Same(row.AccessibilityObject, editor.AccessibilityObject.Parent);
        Assert.Same(editor.AccessibilityObject, root.HitTest(editor.PointToScreen(new(10, 10)).X, editor.PointToScreen(new(10, 10)).Y));
    }

    [Fact]
    public void ReentrantSelectionMutationPreventsOldFocusOrActivationWork()
    {
        var source = new ObservableCollection<object>(Enumerable.Range(0, 100).Cast<object>());
        using var view = new Probe { Size = new(240, 120), ItemsSource = source };
        var item = view.AccessibilityObject.GetChild(90)!;
        view.SelectedIndexChanged += (_, _) => { if (view.SelectedIndex == 90) source.RemoveAt(90); };
        int invoked = 0;
        view.ItemInvoked += (_, _) => invoked++;
        Assert.False(item.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(0, invoked);
        Assert.Equal(-1, view.SelectedIndex);
        Assert.Equal(AccessibilityView.Hidden, item.View);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SelectionCallbackCannotDispatchActivationAfterRetiringItsControlPath(int change)
    {
        using var parent = new VisibleGroup();
        using var destination = new VisibleGroup();
        using var view = new CollectionView { Size = new(240, 120), ItemsSource = new object[] { "value" } };
        parent.Controls.Add(view);
        var item = view.AccessibilityObject.GetChild(0)!;
        int invoked = 0;
        view.ItemInvoked += (_, _) => invoked++;
        view.SelectedIndexChanged += (_, _) =>
        {
            switch (change)
            {
                case 0: view.Visible = false; break;
                case 1: view.Enabled = false; break;
                case 2: destination.Controls.Add(view); break;
                case 3: view.Dispose(); break;
            }
        };

        Assert.False(item.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(0, invoked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceValueCallbackCannotDispatchActivationAfterHidingOrRedirectingSelection(bool redirect)
    {
        using var parent = new VisibleGroup();
        var source = new CountingSource(2);
        using var view = new CollectionView { Size = new(240, 120), ItemsSource = source };
        parent.Controls.Add(view);
        var item = view.AccessibilityObject.GetChild(0)!;
        int invoked = 0;
        view.ItemInvoked += (_, _) => invoked++;
        source.OnRead = () =>
        {
            source.OnRead = null;
            if (redirect) view.SelectedIndex = 1;
            else view.Visible = false;
        };

        Assert.False(item.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(0, invoked);
        if (redirect) Assert.Equal(1, view.SelectedIndex);
    }

    [Fact]
    public void ASourceValueNameCallbackCannotResurrectRetiredOccurrence()
    {
        var value = new MutatingName();
        object[] source = new object[100_003];
        Array.Fill(source, "entry");
        source[90_000] = value;
        using var view = new Probe { Size = new(240, 120), ItemsSource = source };
        var item = view.AccessibilityObject.GetChild(90_000)!;
        value.Callback = () => view.ItemsSource = new object[] { "replacement" };
        Assert.Null(item.Name);
        Assert.Equal(AccessibilityView.Hidden, item.View);
        Assert.Equal(1, view.AccessibilityObject.GetChildCount());
    }

    [Fact]
    public void RowEnterAndSpaceUseSameLogicalActivationAsPointerAndAccessibility()
    {
        using var view = new Probe { Size = new(240, 120), ItemsSource = new object[] { "one", "two" } };
        view.SelectedIndex = 1;
        List<int> indices = [];
        view.ItemInvoked += (_, e) => indices.Add(e.Index);
        view.GetItemContainer(1)!.RaiseKeyDown(new(Keys.Enter));
        view.Press(Keys.Space);
        Assert.Equal(new[] { 1, 1 }, indices);
    }

    [Fact]
    public void CustomSensitiveTemplateNeverExposesRawModelNamesWhenRowsAreUnrealized()
    {
        var secret = new SecretValue();
        object[] values = new object[100_003];
        Array.Fill(values, secret);
        using var view = new Probe { Size = new(240, 120), ItemTemplate = new PasswordFactory(), ItemsSource = values };
        var root = view.AccessibilityObject;
        var visible = root.GetChild(0)!;
        var unrealized = root.GetChild(90_000)!;
        Assert.True(visible.IsSensitive);
        Assert.Equal("Item 1", visible.Name);
        Assert.Equal("Item 90001", unrealized.Name);
        Assert.Null(unrealized.Value);
        Assert.Equal(0, secret.Reads);
    }

    [Fact]
    public void VisibleAndSelectedLogicalRuntimeIdsSurviveGcWithoutCallerRetainingPeer()
    {
        using var view = new Probe { Size = new(240, 120), ItemsSource = Enumerable.Range(0, 100_003).Cast<object>().ToArray() };
        var root = view.AccessibilityObject;
        long visible = CaptureChildId(root, 0);
        view.SelectedIndex = 90_000;
        long selected = CaptureSelectedId(root);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.Equal(visible, CaptureChildId(root, 0));
        Assert.Equal(selected, CaptureSelectedId(root));
        view.ScrollToItem(50_000);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.Equal(selected, CaptureSelectedId(root));
    }

    [Fact]
    public void AccessibleSelectReportsNewerCallbackSelectionInsteadOfFalseSuccess()
    {
        using var view = new Probe { Size = new(240, 120), ItemsSource = new object[] { "one", "two" } };
        var item = view.AccessibilityObject.GetChild(0)!;
        view.SelectedIndexChanged += (_, _) => { if (view.SelectedIndex == 0) view.SelectedIndex = 1; };
        Assert.False(item.PerformAction(AccessibleActions.Select));
        Assert.Equal(1, view.SelectedIndex);
    }

    [Fact]
    public void AccessibleFocusReportsCallbackRedirectionWithoutStealingNewFocus()
    {
        using var group = new VisibleGroup();
        var view = group.Controls.Add(new Probe { Size = new(240, 120), ItemsSource = new object[] { "one" } });
        var outside = group.Controls.Add(new Button());
        var item = view.AccessibilityObject.GetChild(0)!;
        view.GetItemContainer(0)!.GotFocus += (_, _) => outside.Select();
        Assert.False(item.PerformAction(AccessibleActions.Focus));
        Assert.True(outside.Focused);
    }

    private sealed class VisibleGroup : Panel
    {
        public override bool Visible { get => true; set => base.Visible = value; }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long CaptureChildId(AccessibleObject root, int index) => root.GetChild(index)!.RuntimeId;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long CaptureSelectedId(AccessibleObject root) => root.GetSelected()!.RuntimeId;

    private sealed class Probe : CollectionView
    {
        public override bool Visible { get => true; set => base.Visible = value; }
        internal void Press(Keys key) => OnKeyDown(new(key));
    }

    private sealed class EditorFactory : IItemContainerFactory<Control>
    {
        public Control CreateContainer()
        {
            var row = new Panel();
            row.Controls.Add(new TextBox { Left = 5, Top = 5, Width = 150, Height = 30 });
            return row;
        }
        public void PrepareContainer(Control container, int index) => container.Controls[0].Text = "editor";
        public void ClearContainer(Control container, int previousIndex) => container.Controls[0].Text = string.Empty;
        public void DiscardContainer(Control container) { }
    }

    private sealed class MutatingName
    {
        internal Action? Callback;
        public override string ToString() { Callback?.Invoke(); return "stale"; }
    }

    private sealed class SecretValue
    {
        internal int Reads;
        public override string ToString() { Reads++; return "private model value"; }
    }

    private sealed class PasswordFactory : IItemContainerFactory<Control>
    {
        public Control CreateContainer() => new TextBox { PasswordCharacter = '*' };
        public void PrepareContainer(Control container, int index) => container.Text = "private editor value";
        public void ClearContainer(Control container, int previousIndex) => container.Text = string.Empty;
        public void DiscardContainer(Control container) { }
    }

    private sealed class CountingSource(int count) : System.Collections.IList
    {
        internal int Reads;
        internal Action? OnRead;
        public int Count => count;
        public object? this[int index] { get { Reads++; OnRead?.Invoke(); return $"Entry {index}"; } set => throw new NotSupportedException(); }
        public bool IsReadOnly => true;
        public bool IsFixedSize => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(object? value) => throw new NotSupportedException();
        public int IndexOf(object? value) => throw new NotSupportedException();
        public void Insert(int index, object? value) => throw new NotSupportedException();
        public void Remove(object? value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
        public void CopyTo(Array array, int index) => throw new NotSupportedException();
        public System.Collections.IEnumerator GetEnumerator() => throw new InvalidOperationException("Full source enumeration is forbidden.");
    }
}
