using System.Drawing;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class PageCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TabsRetainPagesAndTransferRemovalOnCanonicalHosts(bool neutral)
    {
        using var f = new TabFixture(neutral, mount: false);
        Assert.Same(f.First, f.Tabs.SelectedPage);
        Assert.Equal(PageLifecycleState.Attached, f.First.LifecycleState);
        f.Mount();
        Assert.Equal(PageLifecycleState.Appeared, f.First.LifecycleState);
        Assert.Equal(PageLifecycleState.Attached, f.Second.LifecycleState);
        f.First.Content!.Select();
        Assert.True(f.Tabs.TrySelectPage(f.Second));
        Assert.Same(f.Second.Content, f.Focused);
        Assert.Equal(PageLifecycleState.Attached, f.First.LifecycleState);
        Assert.True(f.Tabs.TrySelectPage(f.First));
        Assert.Same(f.First.Content, f.Focused);
        Assert.True(f.Tabs.Pages.Remove(f.Second));
        Assert.Null(f.Second.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, f.Second.LifecycleState);
        f.Second.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TabsValidateBeforeSelectionOrSelectedRemovalAndRespectRedirect(bool neutral)
    {
        using var f = new TabFixture(neutral);
        var editor = Assert.IsType<TextBox>(f.First.Content);
        bool cancel = true, redirect = false;
        editor.Validating += (_, e) => { e.Cancel = cancel; if (redirect) f.Outside.Select(); };
        editor.Select();
        Assert.False(f.Tabs.TrySelectPage(f.Second));
        Assert.False(f.Tabs.Pages.Remove(f.First));
        Assert.False(f.Tabs.Pages.Clear());
        Assert.Same(editor, f.Focused);
        Assert.Equal(2, f.Tabs.Pages.Count);
        cancel = false; redirect = true;
        Assert.False(f.Tabs.TrySelectPage(f.Second));
        Assert.Same(f.Outside, f.Focused);
        Assert.Same(f.First, f.Tabs.SelectedPage);
        Assert.True(f.Tabs.TrySelectPage(f.Second));
        Assert.Same(f.Outside, f.Focused);
    }

    [Fact]
    public void TabSelectionReentryAndAppearanceReentryCannotRestoreOldWork()
    {
        using var f = new TabFixture(false);
        var third = NewCompositionPage("third");
        f.Tabs.Pages.Add(third);
        bool selecting = false;
        f.Tabs.SelectedPageChanging += (_, e) =>
        {
            if (!selecting && ReferenceEquals(e.NextPage, f.Second))
            {
                selecting = true;
                f.Tabs.TrySelectPage(third);
            }
        };
        Assert.False(f.Tabs.TrySelectPage(f.Second));
        Assert.Same(third, f.Tabs.SelectedPage);
        Assert.True(f.Tabs.TrySelectPage(f.First));
        f.Second.Appearing += (_, _) => f.Tabs.TrySelectPage(third);
        Assert.False(f.Tabs.TrySelectPage(f.Second));
        Assert.Same(third, f.Tabs.SelectedPage);
        Assert.Equal(PageLifecycleState.Attached, f.Second.LifecycleState);
        Assert.Equal(PageLifecycleState.Appeared, third.LifecycleState);
    }

    [Fact]
    public void TabsRejectOwnershipDuplicatesAndAdmissionReentryKeepsNewerCollection()
    {
        using var host = ModernFormsTestHost.Create();
        using var tabs = new TabbedPage();
        using var rejected = NewCompositionPage("rejected");
        var newer = NewCompositionPage("newer");
        bool nested = false;
        rejected.LifecycleStateChanged += (_, _) =>
        {
            if (!nested && rejected.LifecycleState == PageLifecycleState.Attached)
            { nested = true; Assert.True(tabs.Pages.Add(newer)); }
        };
        Assert.False(tabs.Pages.Add(rejected));
        Assert.Equal(new Page[] { newer }, tabs.Pages.ToArray());
        Assert.Null(rejected.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, rejected.LifecycleState);
        Assert.Throws<ArgumentException>(() => tabs.Pages.Add(newer));
        using var foreign = new Panel();
        foreign.Controls.Add(rejected);
        Assert.Throws<InvalidOperationException>(() => tabs.Pages.Add(rejected));
        Assert.Throws<ArgumentException>(() => tabs.Pages.Add(tabs));
    }

    [Fact]
    public void TabsCommitBeforeThrowingNotificationsAndNewerClearCallbackSurvives()
    {
        using var f = new TabFixture(false);
        var added = NewCompositionPage("added");
        EventHandler throwing = (_, _) => throw new InvalidOperationException("pages observer");
        f.Tabs.PagesChanged += throwing;
        Assert.Throws<InvalidOperationException>(() => f.Tabs.Pages.Add(added));
        f.Tabs.PagesChanged -= throwing;
        Assert.Contains(added, f.Tabs.Pages);
        Assert.NotNull(added.Parent);
        var newer = NewCompositionPage("newer");
        bool inserted = false;
        f.Tabs.SelectedPageChanged += (_, _) =>
        {
            if (!inserted && f.Tabs.SelectedPage is null) { inserted = true; f.Tabs.Pages.Add(newer); }
        };
        Assert.False(f.Tabs.Pages.Clear());
        Assert.Same(newer, f.Tabs.SelectedPage);
        Assert.Equal(4, f.Tabs.Pages.Count);
    }

    [Fact]
    public void MovingTabsDoesNotManufactureLifecycleAndExternalTransferSurvivesDisposal()
    {
        using var f = new TabFixture(false);
        using var foreign = new Panel();
        int changed = 0, lifecycle = 0;
        f.Tabs.SelectedPageChanged += (_, _) => changed++;
        f.First.LifecycleStateChanged += (_, _) => lifecycle++;
        var parent = f.First.Parent;
        f.Tabs.Pages.Move(0, 1);
        Assert.Same(parent, f.First.Parent);
        Assert.Same(f.First, f.Tabs.SelectedPage);
        Assert.Equal(1, f.Tabs.SelectedIndex);
        Assert.Equal(0, changed);
        Assert.Equal(0, lifecycle);
        foreign.Controls.Add(f.First);
        Assert.DoesNotContain(f.First, f.Tabs.Pages);
        Assert.Same(f.Second, f.Tabs.SelectedPage);
        f.Tabs.Dispose();
        Assert.NotEqual(PageLifecycleState.Disposed, f.First.LifecycleState);
        Assert.Same(foreign, f.First.Parent);
        Assert.Equal(PageLifecycleState.Disposed, f.Second.LifecycleState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisabledTabsRetainSelectionAndReenableWithoutLosingLocalIntent(bool neutral)
    {
        using var f = new TabFixture(neutral);
        f.Second.Enabled = false;
        Assert.False(f.Tabs.TrySelectPage(f.Second));
        f.First.Enabled = false;
        Assert.Same(f.First, f.Tabs.SelectedPage);
        f.Tabs.Enabled = false;
        f.Second.Enabled = true;
        f.Tabs.Enabled = true;
        var headers = TabHeaders(f.Tabs).ToArray();
        Assert.False(headers.Single(header => header.Name == "first").PerformAction(AccessibleActions.Select));
        Assert.True(headers.Single(header => header.Name == "second").PerformAction(AccessibleActions.Select));
        Assert.Same(f.Second, f.Tabs.SelectedPage);
    }

    [Theory]
    [InlineData(false, FlowDirection.LeftToRight)]
    [InlineData(false, FlowDirection.RightToLeft)]
    [InlineData(true, FlowDirection.LeftToRight)]
    [InlineData(true, FlowDirection.RightToLeft)]
    public void TabKeyboardOverflowUsesRealHeadersAndCanonicalReveal(bool neutral, FlowDirection direction)
    {
        using var f = new TabFixture(neutral);
        f.Tabs.FlowDirection = direction;
        for (int i = 0; i < 18; i++) f.Tabs.Pages.Add(NewCompositionPage("extra " + i));
        f.First.Content!.Select();
        f.Key(Keys.Control | Keys.Tab);
        Assert.Same(f.Second, f.Tabs.SelectedPage);
        f.Key(Keys.Control | Keys.Shift | Keys.Tab);
        Assert.Same(f.First, f.Tabs.SelectedPage);
        var headers = TabHeaders(f.Tabs).ToArray();
        Assert.Equal(20, headers.Length);
        var last = headers.Single(header => header.Name == "extra 17");
        Assert.True(last.PerformAction(AccessibleActions.Select));
        Assert.Same(f.Tabs.Pages[^1], f.Tabs.SelectedPage);
        Assert.False(last.Bounds.IsEmpty);
        Assert.False((last.State & AccessibleStates.Offscreen) != 0);
        Assert.True(last.PerformAction(AccessibleActions.Focus));
        f.Key(Keys.Home);
        Assert.Same(f.First, f.Tabs.SelectedPage);
        f.Key(Keys.End);
        Assert.Same(f.Tabs.Pages[^1], f.Tabs.SelectedPage);
        Assert.Equal(1, TabHeaders(f.Tabs).Count(header => (header.State & AccessibleStates.Selected) != 0));
    }

    [Fact]
    public void BackwardKeyboardFromEmptySelectionChoosesLastEligiblePage()
    {
        using var f = new TabFixture(false);
        f.Tabs.SelectedIndex = -1;
        f.Tabs.Select();
        f.Key(Keys.Control | Keys.Shift | Keys.Tab);
        Assert.Same(f.Second, f.Tabs.SelectedPage);
    }

    [Fact]
    public void RetiredHeaderPeersExposeNoActionAndCannotReselectTransferredPages()
    {
        using var f = new TabFixture(false);
        var first = TabHeaders(f.Tabs).Single(peer => peer.Name == "first");
        var second = TabHeaders(f.Tabs).Single(peer => peer.Name == "second");
        Assert.True(f.Tabs.Pages.Remove(f.Second));
        Assert.Equal(AccessibleActions.None, second.SupportedActions);
        Assert.False(second.PerformAction(AccessibleActions.Select));
        f.Tabs.Dispose();
        Assert.Equal(AccessibleActions.None, first.SupportedActions);
        Assert.False(first.PerformAction(AccessibleActions.Select));
        f.Second.Dispose();
    }

    [Fact]
    public void MandatorySelectedPageLossReportsEmptySelectionWhenFallbackIsVetoed()
    {
        using var f = new TabFixture(false);
        int changes = 0;
        f.Tabs.SelectedPageChanged += (_, _) => changes++;
        f.Tabs.SelectedPageChanging += (_, e) => e.Cancel = ReferenceEquals(e.NextPage, f.Second);
        f.First.Dispose();
        Assert.Null(f.Tabs.SelectedPage);
        Assert.Equal(new Page[] { f.Second }, f.Tabs.Pages.ToArray());
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ReaffirmingCurrentTabFromChangingCallbackSupersedesOlderSelection()
    {
        using var f = new TabFixture(false);
        f.First.Content!.Select();
        f.Tabs.SelectedPageChanging += (_, e) => { if (ReferenceEquals(e.NextPage, f.Second)) f.Tabs.SelectedPage = f.First; };
        Assert.False(f.Tabs.TrySelectPage(f.Second));
        Assert.Same(f.First, f.Tabs.SelectedPage);
        Assert.Same(f.First.Content, f.Focused);
    }

    [Fact]
    public void ExplicitEmptySelectionFromLossCallbackSupersedesAutomaticFallback()
    {
        using var f = new TabFixture(false);
        int changes = 0;
        f.Tabs.SelectedPageChanged += (_, _) => changes++;
        f.Tabs.PagesChanged += (_, _) => f.Tabs.SelectedPage = null;
        f.First.Dispose();
        Assert.Null(f.Tabs.SelectedPage);
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(FlowDirection.LeftToRight, 0)]
    [InlineData(FlowDirection.RightToLeft, 1)]
    public void AppendingATabDoesNotReorderExistingHeaders(FlowDirection direction, int expectedReorders)
    {
        using var f = new TabFixture(false);
        f.Tabs.FlowDirection = direction;
        for (int index = 0; index < 9; index++) f.Tabs.Pages.Add(NewCompositionPage("retained " + index));
        var row = Assert.IsAssignableFrom<Control.ControlAccessibleObject>(f.Tabs.AccessibilityObject.GetChild(0)).Owner!;
        int reorders = 0;
        row.Layout += (_, e) => { if (e.AffectedProperty == "ChildIndex") reorders++; };
        Assert.True(f.Tabs.Pages.Add(NewCompositionPage("appended")));
        Assert.Equal(expectedReorders, reorders);
        var logical = f.Tabs.Pages.Select(page => page.Title).ToArray();
        Assert.Equal(direction == FlowDirection.LeftToRight ? logical : logical.Reverse(), row.Controls.Select(control => control.Text));
        Assert.Same(f.First, f.Tabs.SelectedPage);
    }

    [Fact]
    public void TabSemanticMembershipDoesNotMeasureEveryHeaderPresentation()
    {
        using var host = ModernFormsTestHost.Create();
        var tabs = new GeometryCountingTabs { Rotation = 1 };
        for (int index = 0; index < 12; index++) tabs.Pages.Add(NewCompositionPage("tab " + index));
        using var presenter = new PageHost { Page = tabs };
        using var surface = new SkiaControlSurface(presenter);
        surface.Resize(420, 320);
        var root = tabs.AccessibilityObject;
        tabs.GeometryReads = 0;
        Assert.Equal(2, root.GetChildCount());
        var headers = root.GetChild(0)!;
        Assert.Equal(12, headers.GetChildCount());
        var last = headers.GetChild(11)!;
        Assert.Equal(0, tabs.GeometryReads);
        // State queries still calculate real clipping; only membership avoids that work.
        Assert.True((last.State & AccessibleStates.Offscreen) != 0);
        Assert.True(tabs.GeometryReads > 0);
    }

    [Fact]
    public void HeaderLayoutCallbackMutationOwnsTheNewestPageOrder()
    {
        using var f = new TabFixture(false);
        f.Tabs.Pages.Add(NewCompositionPage("third"));
        f.Tabs.Pages.Add(NewCompositionPage("fourth"));
        var row = Assert.IsAssignableFrom<Control.ControlAccessibleObject>(f.Tabs.AccessibilityObject.GetChild(0)).Owner!;
        bool changed = false;
        row.Layout += (_, e) =>
        {
            if (!changed && e.AffectedProperty == "ChildIndex")
            {
                changed = true;
                f.Tabs.Pages.Move(0, 3);
            }
        };
        f.Tabs.FlowDirection = FlowDirection.RightToLeft;
        Assert.True(changed);
        Assert.Equal(f.Tabs.Pages.Reverse().Select(page => page.Title), row.Controls.Select(control => control.Text));
        Assert.Same(f.First, f.Tabs.SelectedPage);
    }

    [Fact]
    public void CommittedTabSelectionNotifiesTheActualTabListAndVetoIsSilent()
    {
        using var f = new TabFixture(false);
        var row = f.Tabs.AccessibilityObject.GetChild(0)!;
        var selected = new List<Page?>();
        row.ClientNotification += (_, e) => { if (e.EventId == AccessibleEvents.SelectionWithin) selected.Add(f.Tabs.SelectedPage); };
        var header = TabHeaders(f.Tabs).Single(peer => peer.Name == "second");
        var headerEvents = new List<AccessibleEvents>();
        header.ClientNotification += (_, e) => { if (e.EventId is AccessibleEvents.Selection or AccessibleEvents.SelectionRemove) headerEvents.Add(e.EventId); };
        bool veto = true;
        f.First.Content!.Validating += (_, e) => e.Cancel = veto;
        f.First.Content.Select();
        Assert.False(f.Tabs.TrySelectPage(f.Second));
        Assert.Empty(selected);
        veto = false;
        Assert.True(f.Tabs.TrySelectPage(f.Second));
        Assert.Equal(new Page?[] { f.Second }, selected);
        Assert.True(f.Tabs.Pages.Remove(f.Second));
        Assert.Equal(new Page?[] { f.Second, f.First }, selected);
        Assert.Equal(new[] { AccessibleEvents.Selection, AccessibleEvents.SelectionRemove }, headerEvents);
        f.Second.Dispose();
    }

    private sealed class GeometryCountingTabs : TabbedPage
    {
        internal int GeometryReads;
        protected override Rectangle GetScaledBounds(Rectangle bounds, SizeF factor, BoundsSpecified specified)
        {
            GeometryReads++;
            return base.GetScaledBounds(bounds, factor, specified);
        }
    }

    [Theory]
    [InlineData(FlowDirection.LeftToRight)]
    [InlineData(FlowDirection.RightToLeft)]
    public void TouchHeaderTapUsesCanonicalValidationAndDragDoesNotSelect(FlowDirection direction)
    {
        using var host = ModernFormsTestHost.Create();
        var tabs = new TabbedPage { FlowDirection = direction };
        for (int index = 0; index < 12; index++) tabs.Pages.Add(NewCompositionPage("touch " + index));
        using var presenter = new PageHost { Page = tabs };
        using var surface = new SkiaControlSurface(presenter);
        surface.Resize(420, 320);
        var first = tabs.SelectedPage!;
        var editor = Assert.IsType<TextBox>(((ContentPage)first).Content);
        var row = Assert.IsAssignableFrom<Control.ControlAccessibleObject>(tabs.AccessibilityObject.GetChild(0)).Owner!;
        var scroll = Assert.IsAssignableFrom<ScrollableControl>(row);
        scroll.HorizontalScrollProperties.Value = 0;
        int before = scroll.HorizontalScrollProperties.Value;
        surface.ProcessPointer(3, ControlSurfacePointerAction.Down, 300, 16);
        surface.ProcessPointer(3, ControlSurfacePointerAction.Move, 100, 16);
        surface.ProcessPointer(3, ControlSurfacePointerAction.Up, 100, 16);
        Assert.True(scroll.HorizontalScrollProperties.Value > before);
        Assert.Same(first, tabs.SelectedPage);
        Assert.Equal(0, surface.ActivePointerCount);
        Assert.False(row.Capture);
        var target = TabHeaders(tabs).Single(peer => peer.Name == "touch 11");
        Assert.True(target.PerformAction(AccessibleActions.ScrollIntoView));
        var visible = Rectangle.Intersect(target.Bounds, new Rectangle(0, 0, 420, 56));
        Assert.False(visible.IsEmpty);
        int x = visible.Left + visible.Width / 2, y = visible.Top + visible.Height / 2;
        bool veto = true;
        editor.Validating += (_, e) => e.Cancel = veto;
        editor.Select();
        void Tap()
        {
            surface.ProcessPointer(4, ControlSurfacePointerAction.Down, x, y);
            surface.ProcessPointer(4, ControlSurfacePointerAction.Up, x, y);
        }
        Tap();
        Assert.Same(first, tabs.SelectedPage);
        Assert.Same(editor, surface.SelectedControl);
        veto = false;
        Tap();
        Assert.Same(tabs.Pages[^1], tabs.SelectedPage);
        Assert.Equal(0, surface.ActivePointerCount);
    }

    private static ContentPage NewCompositionPage(string title) => new() { Title = title, Content = new TextBox { Text = title, Dock = DockStyle.Fill } };
    private static IEnumerable<AccessibleObject> TabHeaders(TabbedPage tabs)
        => Descendants(tabs.AccessibilityObject).Where(peer => peer.ControlType == AccessibleControlType.TabItem);
    private static IEnumerable<AccessibleObject> Descendants(AccessibleObject root)
    {
        var visited = new HashSet<AccessibleObject>();
        var stack = new Stack<AccessibleObject>(); stack.Push(root);
        while (stack.TryPop(out var peer))
        {
            Assert.True(visited.Add(peer), "A canonical peer appeared twice or formed a cycle.");
            yield return peer;
            for (int i = peer.GetChildCount() - 1; i >= 0; i--) if (peer.GetChild(i) is { } child) stack.Push(child);
        }
    }
    private sealed class TabFixture : IDisposable
    {
        private readonly bool neutral;
        private SkiaControlSurface? surface;
        internal ModernFormsTestHost Host { get; } = ModernFormsTestHost.Create();
        internal TabbedPage Tabs { get; } = new();
        internal ContentPage First { get; } = NewCompositionPage("first");
        internal ContentPage Second { get; } = NewCompositionPage("second");
        internal Panel Root { get; } = new() { Width = 420, Height = 320 };
        internal Button Outside { get; } = new() { Text = "outside", Bounds = new(0, 282, 100, 32) };
        internal TestWindowHost? Window { get; private set; }
        internal Control? Focused => surface?.SelectedControl ?? Window?.FocusedControl;
        internal TabFixture(bool neutral, bool mount = true)
        {
            this.neutral = neutral;
            Tabs.Pages.Add(First); Tabs.Pages.Add(Second);
            Root.Controls.Add(new PageHost { Page = Tabs, Bounds = new(0, 0, 420, 280) });
            Root.Controls.Add(Outside);
            if (mount) Mount();
        }
        internal void Mount()
        {
            if (neutral) { surface = new(Root); surface.Resize(420, 320); }
            else Window = Host.Show(Root);
        }
        internal void Key(Keys key)
        {
            if (surface is not null) { surface.ProcessKeyDown(key); surface.ProcessKeyUp(key); }
            else Window!.Input.PressKey(key);
        }
        public void Dispose() { surface?.Dispose(); Host.Dispose(); Root.Dispose(); }
    }
}
