using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Backend.Android.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using ModernFormsNext.WindowKit.Threading;
using Xunit;
using static ModernFormsNext.WindowKit.Backend.Android.Accessibility.AndroidAccessibilityMapper;
using static ModernFormsNext.WindowKit.Backend.Android.Tests.AndroidAccessibilityMapperTests;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public class AndroidAccessibilityProviderTests
{
    [Fact]
    public void AttachmentExposesRootAndVirtualChildWithoutNativeViews()
    {
        var root = new TestPeer();
        var child = root.Add(new TestPeer());
        using var session = new AndroidAccessibilitySession(new Host(root));
        Assert.Null(session.Find(-1));
        session.Attach();
        Assert.Same(Adapt(root), session.Find(-1));
        int id = Assert.Single(session.Children(Adapt(root)));
        Assert.True(id > 0);
        Assert.Same(Adapt(child), session.Find(id));
        Assert.Null(session.Find(8000));
    }

    [Fact]
    public void ReorderPreservesIdsRemovalInvalidatesEvenCustomRetainedParent()
    {
        var root = new TestPeer();
        var a = root.Add(new TestPeer { Name = "Duplicate" });
        var b = root.Add(new TestPeer { Name = "Duplicate" });
        using var session = Started(root);
        int aId = session.Register(Adapt(a)), bId = session.Register(Adapt(b));
        Assert.NotEqual(aId, bId);
        root.Children.Reverse();
        root.NotifyClients(AccessibleEvents.Reorder);
        Assert.Equal(new[] { bId, aId }, session.Children(Adapt(root)));
        root.Children.Remove(a);
        root.NotifyClients(AccessibleEvents.Reorder);
        Assert.Null(session.Find(aId));
        Assert.False(session.Perform(aId, ActionClick, null, true));
        root.Children.Add(a);
        Assert.NotEqual(aId, session.Register(Adapt(a)));
    }

    [Fact]
    public void DetachReattachAndDisposeRejectOldIdsAndDropSubscriptions()
    {
        var root = new TestPeer();
        var child = root.Add(new TestPeer());
        using var session = Started(root);
        int old = session.Register(Adapt(child));
        session.Detach();
        Assert.Null(session.Find(-1));
        Assert.Null(session.Find(old));
        Assert.Equal(0, session.CachedNodeCount);
        child.NotifyClients(AccessibleEvents.NameChange);
        Assert.Empty(session.DrainEvents());
        session.Attach();
        Assert.NotEqual(old, session.Register(Adapt(child)));
        session.Dispose();
        session.Attach();
        Assert.Null(session.Find(-1));
    }

    [Fact]
    public void HostRecreationKeepsOldProviderDisconnectedFromBorrowedTree()
    {
        using var root = new Panel();
        var button = new Button { Text = "Action" };
        root.Controls.Add(button);
        using var surface = new SkiaControlSurface(root);
        using var old = new AndroidAccessibilitySession(surface);
        old.Attach();
        int id = old.Register(Adapt(button.AccessibilityObject));
        old.Dispose();
        surface.Dispose();
        using var replacement = new SkiaControlSurface(root);
        using var current = new AndroidAccessibilitySession(replacement);
        current.Attach();
        Assert.Null(old.Find(id));
        Assert.Same(Adapt(button.AccessibilityObject), current.Find(current.Register(Adapt(button.AccessibilityObject))));
    }

    [Fact]
    public void ReplacingSemanticsWithinSameNativeViewContinuesItsIdNamespace()
    {
        var first = new TestPeer(); var oldChild = first.Add(new TestPeer());
        using var old = Started(first);
        int oldId = old.Register(Adapt(oldChild));
        int lastId = old.LastAllocatedId;
        old.Dispose();
        var replacement = new TestPeer(); var newChild = replacement.Add(new TestPeer());
        using var current = new AndroidAccessibilitySession(new Host(replacement), lastId);
        current.Attach();
        Assert.NotEqual(oldId, current.Register(Adapt(newChild)));
        Assert.Null(current.Find(oldId));
    }

    [Fact]
    public void IdentityExhaustionFailsPredictablyWithoutReusingOrWrappingIds()
    {
        var root = new TestPeer();
        var a = root.Add(new TestPeer());
        var b = root.Add(new TestPeer());
        using var session = new AndroidAccessibilitySession(new Host(root), int.MaxValue - 1);
        session.Attach();
        Assert.Equal(int.MaxValue, session.Register(Adapt(a)));
        Assert.Equal(AndroidAccessibilitySession.InvalidId, session.Register(Adapt(b)));
        Assert.Same(Adapt(a), session.Find(int.MaxValue));
    }

    [Fact]
    public void HiddenParentAndDisposedControlInvalidateDescendants()
    {
        using var root = new Panel();
        var container = new Panel();
        var button = new Button();
        root.Controls.Add(container);
        container.Controls.Add(button);
        using var surface = new SkiaControlSurface(root);
        using var session = new AndroidAccessibilitySession(surface);
        session.Attach();
        int id = session.Register(Adapt(button.AccessibilityObject));
        container.AccessibilityView = AccessibilityView.Hidden;
        Assert.Null(session.Find(id));
        container.AccessibilityView = AccessibilityView.Control;
        id = session.Register(Adapt(button.AccessibilityObject));
        button.Dispose();
        Assert.Null(session.Find(id));
    }

    [Fact]
    public void AccessibilityFocusIsIndependentFromKeyboardFocus()
    {
        var root = new TestPeer();
        var a = root.Add(new TestPeer { Actions = AccessibleActions.Focus });
        var b = root.Add(new TestPeer());
        using var session = Started(root);
        int aId = session.Register(Adapt(a)), bId = session.Register(Adapt(b));
        Assert.True(session.Perform(aId, ActionFocus, null, true));
        Assert.Equal(aId, session.FindFocus(false));
        Assert.True(session.Perform(bId, ActionAccessibilityFocus, null, true));
        Assert.Equal(bId, session.FindFocus(true));
        Assert.Equal(aId, session.FindFocus(false));
        Assert.False(session.Perform(aId, ActionClearAccessibilityFocus, null, true));
        Assert.True(session.Perform(bId, ActionClearAccessibilityFocus, null, true));
        Assert.False(session.Perform(bId, ActionAccessibilityFocus, null, false));
    }

    [Fact]
    public void FocusedNodeRemovalClearsAccessibilityFocus()
    {
        var root = new TestPeer();
        var child = root.Add(new TestPeer());
        using var session = Started(root);
        int id = session.Register(Adapt(child));
        session.Perform(id, ActionAccessibilityFocus, null, true);
        root.Children.Clear();
        root.NotifyClients(AccessibleEvents.Reorder);
        Assert.Equal(AndroidAccessibilitySession.InvalidId, session.FindFocus(true));
    }

    [Theory]
    [InlineData(AccessibleEvents.Focus, 8, 0)]
    [InlineData(AccessibleEvents.NameChange, 2048, 4)]
    [InlineData(AccessibleEvents.ValueChange, 2048, 64)]
    [InlineData(AccessibleEvents.StateChange, 2048, 64)]
    [InlineData(AccessibleEvents.Selection, 4, 0)]
    [InlineData(AccessibleEvents.Reorder, 2048, 1)]
    public void CustomNotificationsMapAndCoalesce(AccessibleEvents canonical, int type, int changes)
    {
        var root = new TestPeer();
        using var session = Started(root);
        session.DrainEvents();
        for (int i = 0; i < 20; i++) root.NotifyClients(canonical);
        Assert.Equal(new AndroidAccessibilityEvent(-1, type, changes), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void SensitiveEventsContainNoTextAndNeverReadValue()
    {
        var root = new TestPeer { Sensitive = true, ThrowOnValueRead = true };
        using var session = Started(root);
        session.DrainEvents();
        root.NotifyClients(AccessibleEvents.ValueChange);
        Assert.Equal(new AndroidAccessibilityEvent(-1, 2048, 0), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void EditableValueChangeStillReportsTextContent()
    {
        var root = new TestPeer { Type = AccessibleControlType.Edit };
        using var session = Started(root);
        session.DrainEvents();
        root.NotifyClients(AccessibleEvents.ValueChange);
        Assert.Equal(new AndroidAccessibilityEvent(-1, 2048, 2), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void SwitchValueAndStateChangesCoalesceWithoutTextContentFlag()
    {
        // Semantic tests do not pump a UI frame clock or exercise visual transitions.
        using var control = new Switch { Animate = false };
        using var surface = new SkiaControlSurface(control);
        using var session = new AndroidAccessibilitySession(surface);
        session.Attach(); session.DrainEvents();
        control.Toggle();
        Assert.Equal(new AndroidAccessibilityEvent(-1, 2048, 64), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void PasswordInputSensitivityFollowsKeyboardFocusNotAccessibilityFocus()
    {
        using var root = new Panel();
        var editor = new TextBox();
        var password = new TextBox { PasswordCharacter = '*' };
        root.Controls.AddRange([editor, password]);
        using var surface = new SkiaControlSurface(root);
        using var session = new AndroidAccessibilitySession(surface);
        session.Attach();
        password.Select();
        Assert.True(session.IsInputSensitive);
        Assert.True(session.Perform(session.Register(Adapt(editor.AccessibilityObject)), ActionAccessibilityFocus, null, true));
        Assert.True(session.IsInputSensitive);
        editor.Select();
        Assert.False(session.IsInputSensitive);
    }

    [Fact]
    public void WindowlessControlsNotifyWithoutWindowServiceAndDisposeStopsRouting()
    {
        using var root = new Panel();
        var button = new Button();
        root.Controls.Add(button);
        using var surface = new SkiaControlSurface(root);
        using var session = new AndroidAccessibilitySession(surface);
        session.Attach();
        session.DrainEvents();
        button.AccessibleName = "New name";
        var change = Assert.Single(session.DrainEvents());
        Assert.Equal(4, change.Changes);
        Assert.Same(Adapt(button.AccessibilityObject), session.Find(change.Id));
        surface.Dispose();
        Assert.Null(session.Find(change.Id));
    }

    [Fact]
    public void EventStormHasBoundedMemoryAndRetainsSubtreeInvalidation()
    {
        var root = new TestPeer();
        using var session = Started(root);
        for (int i = 0; i < 500; i++)
        {
            var child = root.Add(new TestPeer());
            session.Register(Adapt(child));
            child.NotifyClients(AccessibleEvents.StateChange);
        }
        var events = session.DrainEvents();
        Assert.InRange(events.Length, 1, 128);
        Assert.Contains(events, e => e.Id == -1 && e.Type == 2048 && e.Changes == 1);
    }

    [Fact]
    public void NormalFrameworkClickProducesOneInvocationNotification()
    {
        using var root = new Panel();
        var button = new Button(); root.Controls.Add(button);
        using var surface = new SkiaControlSurface(root);
        using var session = new AndroidAccessibilitySession(surface);
        session.Attach(); session.DrainEvents();
        button.PerformClick();
        Assert.Equal(1, Assert.Single(session.DrainEvents()).Type);
        int id = session.Register(Adapt(button.AccessibilityObject));
        Assert.True(session.Perform(id, ActionClick, null, true));
        Assert.Single(session.DrainEvents(), e => e.Type == 1);
    }

    [Fact]
    public void RealListBoxDuplicateOccurrencesHaveSeparateIdsAndSelectionWorks()
    {
        using var list = new ListBox { SelectionMode = SelectionMode.MultiSimple };
        using var surface = new SkiaControlSurface(list);
        list.Items.Add("same"); list.Items.Add("same");
        using var session = new AndroidAccessibilitySession(surface);
        session.Attach();
        var ids = session.Children(session.Root!);
        Assert.Equal(2, ids.Count);
        Assert.NotEqual(ids[0], ids[1]);
        Assert.True(session.Perform(ids[1], ActionSelect, null, true));
        Assert.Equal(1, list.SelectedIndex);
        Assert.True(session.Perform(ids[1], ActionClearSelection, null, true));
        Assert.False(Read(session.Find(ids[1])!).Selected);
        list.Items.RemoveAt(0);
        Assert.Null(session.Find(ids[0]));
        Assert.Same(Adapt(list.AccessibilityObject.GetChild(0)!), session.Find(ids[1]));
    }

    [Fact]
    public void RealTreeItemsExpandAndExposeLogicalChildren()
    {
        using var tree = new TreeView();
        using var surface = new SkiaControlSurface(tree);
        var branch = new TreeViewItem { Text = "Branch" };
        branch.Items.Add(new TreeViewItem { Text = "Leaf" });
        tree.Items.Add(branch);
        using var session = new AndroidAccessibilitySession(surface);
        session.Attach();
        int id = Assert.Single(session.Children(session.Root!));
        Assert.True(session.Perform(id, ActionExpand, null, true));
        Assert.True(branch.Expanded);
        int leafId = Assert.Single(session.Children(session.Find(id)!));
        Assert.DoesNotContain(ActionExpand, Actions(session.Find(leafId)!));
        tree.Items.Remove(branch);
        Assert.Null(session.Find(id));
        Assert.Null(session.Find(leafId));
    }

    [Fact]
    public void RealTabsExposeSemanticHeadersAndSelection()
    {
        using var tabs = new TabControl();
        using var surface = new SkiaControlSurface(tabs);
        var a = new TabPage { Text = "A" }; var b = new TabPage { Text = "B" };
        tabs.TabPages.Add(a); tabs.TabPages.Add(b);
        using var session = new AndroidAccessibilitySession(surface);
        session.Attach();
        var header = Enumerable.Range(0, session.Root!.GetChildCount()).Select(session.Root.GetChild)
            .First(n => n is not null && n.GetControlType() == (int)AccessibleControlType.TabItem && n.Name == "B")!;
        int id = session.Register(header);
        Assert.True(session.Perform(id, ActionSelect, null, true));
        Assert.Same(b, tabs.SelectedTabPage);
        tabs.TabPages.Remove(b);
        Assert.Null(session.Find(id));
    }

    [Fact]
    public void RealListViewItemsAndMenuCommandsDetachWithoutNativeViews()
    {
        using var root = new Panel();
        using var surface = new SkiaControlSurface(root);
        var list = new ListView(); root.Controls.Add(list);
        var menu = new Menu(); root.Controls.Add(menu);
        var item = list.Items.Add("Item");
        var command = menu.Items.Add("Run");
        int clicked = 0;
        command.Click += (_, _) => clicked++;
        using var session = new AndroidAccessibilitySession(surface);
        session.Attach();
        int listId = session.Register(Adapt(list.AccessibilityObject.GetChild(0)!));
        int commandId = session.Register(Adapt(menu.AccessibilityObject.GetChild(0)!));
        Assert.True(session.Perform(listId, ActionSelect, null, true));
        Assert.True(item.Selected);
        Assert.True(session.Perform(commandId, ActionClick, null, true));
        Assert.Equal(1, clicked);
        list.Items.Remove(item); menu.Items.Remove(command);
        Assert.Null(session.Find(listId));
        Assert.Null(session.Find(commandId));
    }

    [Fact]
    public void DispatcherExecutesInlineAndContainsExceptionsWithoutTheirMessages()
    {
        var dispatcher = new TestDispatcher { OnThread = true };
        int diagnostics = 0;
        var boundary = new AndroidAccessibilityDispatch(dispatcher, () => diagnostics++);
        Assert.Equal(42, boundary.Run(() => 42, 0));
        Assert.Equal(0, dispatcher.PostCount);
        Assert.False(boundary.Run<bool>(() => throw new InvalidOperationException(), false));
        Assert.False(boundary.Run<bool>(() => throw new InvalidOperationException(), false));
        Assert.Equal(1, diagnostics);
    }

    [Fact]
    public void DispatcherTimeoutCancelsQueuedMutation()
    {
        var dispatcher = new TestDispatcher();
        var boundary = new AndroidAccessibilityDispatch(dispatcher);
        bool changed = false;
        Assert.False(boundary.Run(() => { changed = true; return true; }, false, 1));
        dispatcher.Pending!();
        Assert.False(changed);
    }

    private static AndroidAccessibilitySession Started(TestPeer root)
    {
        var session = new AndroidAccessibilitySession(new Host(root));
        session.Attach();
        return session;
    }

    private sealed class Host(TestPeer root) : IPlatformAccessibilityHost
    {
        public IPlatformAccessibleObject AccessibilityRoot => Adapt(root);
    }

    private sealed class TestDispatcher : IPlatformDispatcher
    {
        internal bool OnThread;
        internal int PostCount;
        internal Action? Pending;
        public bool CheckAccess() => OnThread;
        public void Post(Action action) { PostCount++; Pending = action; }
        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
            => InvokeAsync(() => { action(); return true; }, cancellationToken);
        public Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<T>();
            Post(() =>
            {
                if (cancellationToken.IsCancellationRequested) completion.TrySetCanceled(cancellationToken);
                else completion.SetResult(function());
            });
            return completion.Task;
        }
    }
}
