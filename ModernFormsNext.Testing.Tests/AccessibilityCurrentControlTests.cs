using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class AccessibilityCurrentControlTests
{
    [Fact]
    public void LinkChildrenRepresentOnlyLiveRangesAndNeverReadUserObjects()
    {
        using var host = ModernFormsTestHost.Create();
        var label = new LinkLabel { Text = "Read docs or help", Width = 300, Height = 80, TextAlign = ContentAlignment.TopLeft };
        label.Links.Clear();
        var docs = label.Links.Add(5, 4, new ExplosivePayload());
        docs.Name = "docs-link";
        docs.Tag = new ExplosivePayload();
        var help = label.Links.Add(13, 4);
        label.Links.Add(0, 0);
        host.Show(label);
        var owner = label.AccessibilityObject;
        var peer = owner.GetChild(0)!;
        Assert.Equal(2, owner.GetChildCount());
        Assert.Equal(AccessibleControlType.Text, owner.ControlType);
        Assert.Equal(AccessibleControlType.Hyperlink, peer.ControlType);
        Assert.Equal("docs", peer.Name);
        Assert.Equal("docs-link", peer.AutomationId);
        Assert.Null(peer.Value);
        Assert.Same(owner, peer.Parent);
        Assert.Same(owner.GetChild(1), peer.Navigate(AccessibleNavigation.Next));
        int clicks = 0;
        label.LinkClicked += (_, e) => { Assert.Same(docs, e.Link); clicks++; };
        Assert.True(peer.PerformAction(AccessibleActions.Invoke));
        Assert.True(docs.Visited);
        Assert.Equal(1, clicks);
        Assert.True(peer.State.HasFlag(AccessibleStates.Traversed));
        Assert.True(peer.PerformAction(AccessibleActions.Focus));
        Assert.Same(peer, owner.GetFocused());
        docs.Enabled = false;
        Assert.False(peer.PerformAction(AccessibleActions.Invoke));
        Assert.True(peer.State.HasFlag(AccessibleStates.Unavailable));
        docs.Enabled = true;
        label.Links.Remove(docs);
        Assert.Null(peer.Parent);
        Assert.Null(peer.Name);
        Assert.Equal(Rectangle.Empty, peer.Bounds);
        Assert.False(peer.PerformAction(AccessibleActions.Invoke));
        Assert.Same(help, label.Links.Single(item => item.Length > 0));
    }

    [Fact]
    public void ReorderingRetainsLinkIdentityAndRejectedOwnershipDoesNotMutateCollections()
    {
        using var host = ModernFormsTestHost.Create();
        var panel = new Panel();
        var first = panel.Controls.Add(new LinkLabel { Text = "one two three", Width = 300, Height = 40 });
        var second = panel.Controls.Add(new LinkLabel { Text = "other", Top = 50 });
        first.Links.Clear(); second.Links.Clear();
        var one = first.Links.Add(0, 3);
        var two = first.Links.Add(4, 3);
        host.Show(panel);
        var peer = first.AccessibilityObject.GetChild(0)!;
        var twoPeer = first.AccessibilityObject.GetChild(1)!;
        Assert.Throws<ArgumentException>(() => second.Links.Add(one));
        Assert.Throws<ArgumentException>(() => first.Links.Add(one));
        var overlap = new LinkLabel.Link(1, 3);
        Assert.Throws<InvalidOperationException>(() => first.Links.Add(overlap));
        Assert.Equal(2, first.Links.Count);
        Assert.Empty(second.Links);
        Assert.Same(first.AccessibilityObject, peer.Parent);
        one.Start = 8;
        Assert.Same(twoPeer, first.AccessibilityObject.GetChild(0));
        Assert.Same(peer, first.AccessibilityObject.GetChild(1));
        Assert.Equal("thr", peer.Name);
        Assert.Throws<InvalidOperationException>(() => one.Start = 5);
        Assert.Equal(8, one.Start);
        Assert.Throws<InvalidOperationException>(() => two.Length = 8);
        Assert.Equal(3, two.Length);
        first.Text = "a";
        Assert.Equal(0, first.AccessibilityObject.GetChildCount());
        Assert.False(peer.PerformAction(AccessibleActions.Invoke));
        Assert.False(twoPeer.PerformAction(AccessibleActions.Invoke));
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void LinkGeometryBeforePaintUsesCrLfAndWholeUnicodeRanges(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(360, 220, scale));
        var root = new Panel();
        host.Show(root);
        // Add after Show: accessibility performs the same layout used by rendering, without
        // requiring another paint. The link covers multiple lines whose union contains a gap.
        var label = root.Controls.Add(new LinkLabel {
            Left = 10, Top = 10, Width = 260, Height = 120,
            Text = "x\r\n😀 wide\r\ny", TextAlign = ContentAlignment.TopLeft
        });
        label.Links.Clear();
        var link = label.Links.Add(3, 10); // emoji + ' wide' + CRLF + y
        var peer = label.AccessibilityObject.GetChild(0)!;
        Assert.Equal("😀 wide\r\ny", peer.Name);
        Assert.False(peer.Bounds.IsEmpty);
        Assert.NotEmpty(link.VisualBounds);
        Assert.Equal(7, link.VisualBounds.Count); // whole surrogate pair is one rendered fragment
        foreach (var fragment in link.VisualBounds)
        {
            var center = label.PointToScreen(new Point(fragment.Left + Math.Max(0, fragment.Width / 2), fragment.Top + fragment.Height / 2));
            Assert.Same(peer, peer.HitTest(center.X, center.Y));
        }
        var last = link.VisualBounds[^1];
        var gap = label.PointToScreen(new Point(link.VisualBounds.Max(r => r.Right) - 1, last.Top + last.Height / 2));
        Assert.True(peer.Bounds.Contains(gap));
        Assert.Null(peer.HitTest(gap.X, gap.Y));
        var previous = peer.Bounds;
        label.Padding = new Padding(15);
        Assert.NotEqual(previous, peer.Bounds);
        var padded = peer.Bounds;
        label.Font = new ModernFormsNext.Font(label.Font.FamilyName, label.Font.SizeInPoints + 5);
        Assert.NotEqual(padded.Height, peer.Bounds.Height);
        label.Width = 20;
        Assert.True(peer.Bounds.Width <= label.ScaledWidth);
        Assert.False(peer.PerformAction(AccessibleActions.Invoke, "unexpected"));
    }

    [Fact]
    public void PartialSurrogateAndZeroLengthLinksDoNotBecomeIndependentActions()
    {
        using var host = ModernFormsTestHost.Create();
        var label = new LinkLabel { Text = "😀x" };
        label.Links.Clear();
        label.Links.Add(0, 1);
        label.Links.Add(1, 1);
        label.Links.Add(2, 0);
        host.Show(label);
        Assert.Equal(0, label.AccessibilityObject.GetChildCount());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinkStateCallbacksCannotActivateRemovedOrReattachedTargets(bool reattach)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var label = root.Controls.Add(new LinkLabel { Text = "activate" });
        host.Show(root);
        var peer = label.AccessibilityObject.GetChild(0)!;
        int clicks = 0;
        label.LinkClicked += (_, _) => clicks++;
        bool moved = false;
        label.AccessibilityObject.ClientNotification += (_, e) => {
            if (e.EventId != AccessibleEvents.StateChange || moved) return;
            // Removing/readding also reports visibility state. Retire this test action before
            // mutation so the fixture tests one reentrant move rather than an endless observer.
            moved = true;
            root.Controls.Remove(label);
            if (reattach) root.Controls.Add(label);
        };
        Assert.False(peer.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(0, clicks);
        Assert.True(label.Links[0].Visited);
    }

    [Fact]
    public void LinkReentrantInvokeIsRejectedAndThrowsLeaveCommittedVisitedState()
    {
        using var host = ModernFormsTestHost.Create();
        var label = new LinkLabel { Text = "activate" };
        host.Show(label);
        var peer = label.AccessibilityObject.GetChild(0)!;
        int calls = 0;
        label.LinkClicked += (_, _) => {
            calls++;
            Assert.False(peer.PerformAction(AccessibleActions.Invoke));
            throw new ApplicationException("observer");
        };
        Assert.Throws<ApplicationException>(() => peer.PerformAction(AccessibleActions.Invoke));
        Assert.True(label.Links[0].Visited);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void LinkMetadataNotificationsDescribeFinalChildState()
    {
        using var host = ModernFormsTestHost.Create();
        var label = new LinkLabel { Text = "one two", Width = 240 };
        label.Links.Clear();
        var link = label.Links.Add(0, 3);
        host.Show(label);
        var peer = label.AccessibilityObject.GetChild(0)!;
        var events = new List<AccessibleEvents>();
        label.AccessibilityObject.ClientNotification += (_, e) => {
            events.Add(e.EventId);
            if (e.EventId == AccessibleEvents.NameChange && e.ChildId == 1)
                Assert.Equal(label.Text.Substring(link.Start, link.Length), peer.Name);
        };
        link.Start = 4;
        link.Enabled = false;
        link.Visited = true;
        Assert.Contains(AccessibleEvents.Reorder, events);
        Assert.Contains(AccessibleEvents.NameChange, events);
        Assert.Contains(AccessibleEvents.StateChange, events);
        Assert.Equal("two", peer.Name);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void NumericRangeUsesRealEditorAndButtonsWithEditorOnlyReadOnly(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(360, 220, scale));
        var numeric = new NumericUpDown { AccessibleName = "Quantity", DecimalPlaces = 2, Minimum = -10m, Maximum = 10m, Value = 1.25m, ReadOnly = true, Width = 180 };
        host.Show(numeric);
        var peer = numeric.AccessibilityObject;
        Assert.Equal(AccessibleControlType.Spinner, peer.ControlType);
        Assert.Equal("Quantity", peer.Name);
        Assert.False(peer.State.HasFlag(AccessibleStates.ReadOnly));
        Assert.False(peer.RangeValue!.Value.IsReadOnly);
        Assert.Equal(3, peer.GetChildCount());
        var editorPeer = Assert.IsAssignableFrom<Control.ControlAccessibleObject>(peer.GetChild(0));
        var editor = Assert.IsAssignableFrom<TextBox>(editorPeer.Owner);
        Assert.True(editor.ReadOnly);
        Assert.True(editorPeer.State.HasFlag(AccessibleStates.ReadOnly));
        Assert.Equal("Quantity", editorPeer.Name);
        Assert.Same(peer, editorPeer.Parent);
        Assert.Same(peer.GetChild(1), editorPeer.Navigate(AccessibleNavigation.Next));
        Assert.True(peer.Bounds.Contains(editorPeer.Bounds));
        Assert.InRange(Math.Abs(editorPeer.Bounds.Right - peer.GetChild(1)!.Bounds.Left), 0, 1);
        Assert.Equal(peer.GetChild(1)!.Bounds.Top, editorPeer.Bounds.Top);
        Assert.Equal(peer.GetChild(2)!.Bounds.Bottom, editorPeer.Bounds.Bottom);
        Assert.True(peer.GetChild(1)!.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(1.26m, numeric.Value);
        Assert.True(peer.GetChild(2)!.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(1.25m, numeric.Value);
        Assert.True(peer.PerformAction(AccessibleActions.SetValue, 2.345m));
        Assert.Equal(2.35m, numeric.Value);
        numeric.AllowManualEdit = false;
        Assert.True(peer.PerformAction(AccessibleActions.Increment));
        Assert.Equal(2.36m, numeric.Value);
        Assert.False(editorPeer.PerformAction(AccessibleActions.SetValue, "5"));
        numeric.AccessibleName = "Updated quantity";
        Assert.Equal("Updated quantity", editorPeer.Name);
    }

    [Fact]
    public void NumericDecimalExtremaSaturateAndNativeEndpointProjectionRoundTrips()
    {
        using var host = ModernFormsTestHost.Create();
        var numeric = new NumericUpDown { Minimum = decimal.MinValue, Maximum = decimal.MaxValue, Increment = decimal.MaxValue };
        host.Show(numeric);
        var peer = numeric.AccessibilityObject;
        Assert.True(peer.PerformAction(AccessibleActions.SetValue, 9_007_199_254_740_993L));
        Assert.Equal(9_007_199_254_740_993m, numeric.Value);
        numeric.Value = decimal.MaxValue;
        Assert.True(peer.PerformAction(AccessibleActions.Increment));
        Assert.Equal(decimal.MaxValue, numeric.Value);
        numeric.Value = decimal.MinValue;
        Assert.True(peer.PerformAction(AccessibleActions.Decrement));
        Assert.Equal(decimal.MinValue, numeric.Value);
        Assert.True(peer.PerformAction(AccessibleActions.SetValue, peer.RangeValue!.Value.Maximum));
        Assert.Equal(decimal.MaxValue, numeric.Value);
        Assert.True(peer.PerformAction(AccessibleActions.SetValue, peer.RangeValue!.Value.Minimum));
        Assert.Equal(decimal.MinValue, numeric.Value);
        foreach (object invalid in new object[] { double.NaN, double.PositiveInfinity, "5", new ExplosivePayload(), double.MaxValue })
            Assert.False(peer.PerformAction(AccessibleActions.SetValue, invalid));
        Assert.Equal(decimal.MinValue, numeric.Value);
    }

    [Fact]
    public void NumericMetadataChangesNotifyWithoutInventingPublicValueChanges()
    {
        using var host = ModernFormsTestHost.Create();
        var numeric = new NumericUpDown { Value = 5 };
        host.Show(numeric);
        int changes = 0, metadata = 0;
        numeric.ValueChanged += (_, _) => changes++;
        numeric.AccessibilityObject.ClientNotification += (_, e) => {
            if (e.EventId == AccessibleEvents.RangeValueChanged) { metadata++; Assert.NotNull(numeric.AccessibilityObject.RangeValue); }
        };
        numeric.Minimum = -20;
        numeric.Maximum = 200;
        numeric.Increment = .25m;
        numeric.DecimalPlaces = 2;
        numeric.AutoIncrement = true;
        Assert.Equal(0, changes);
        Assert.Equal(5, metadata);
        numeric.Maximum = 3;
        Assert.Equal(1, changes);
        Assert.Equal(3, numeric.Value);
        Assert.Equal(6, metadata);
    }

    [Fact]
    public void NumericFormattingFailureRestoresEditorGuardAndLaterManualEditingWorks()
    {
        using var host = ModernFormsTestHost.Create();
        var numeric = new NumericUpDown();
        host.Show(numeric);
        var editor = (TextBox)((Control.ControlAccessibleObject)numeric.AccessibilityObject.GetChild(0)!).Owner!;
        EventHandler throwing = (_, _) => throw new ApplicationException("editor observer");
        editor.TextChanged += throwing;
        Assert.Throws<ApplicationException>(() => numeric.Value = 12);
        Assert.Equal(12, numeric.Value);
        editor.TextChanged -= throwing;
        editor.Text = "24";
        Assert.Equal(24, numeric.Value);
        int events = 0;
        numeric.ValueChanged += (_, _) => { events++; numeric.UpButton(); };
        numeric.UpButton();
        Assert.Equal(25, numeric.Value);
        Assert.Equal(1, events);
    }

    [Fact]
    public void NumericValueCallbackDisposalMakesAllRetainedActionsInert()
    {
        using var host = ModernFormsTestHost.Create();
        var numeric = new NumericUpDown();
        host.Show(numeric);
        var peer = numeric.AccessibilityObject;
        var button = peer.GetChild(1)!;
        numeric.ValueChanged += (_, _) => numeric.Dispose();
        Assert.True(button.PerformAction(AccessibleActions.Invoke));
        Assert.True(peer.State.HasFlag(AccessibleStates.Unavailable));
        Assert.True(peer.State.HasFlag(AccessibleStates.Invisible));
        Assert.False(button.PerformAction(AccessibleActions.Invoke));
        Assert.False(peer.PerformAction(AccessibleActions.SetValue, 10));
        Assert.Null(peer.RangeValue);
        Assert.Null(button.Parent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScrollBarInclusiveFullIntegerRangeRemainsWritableAndOriented(bool vertical)
    {
        using var host = ModernFormsTestHost.Create();
        ScrollBar bar = vertical ? new VerticalScrollBar() : new HorizontalScrollBar();
        bar.Minimum = int.MinValue; bar.Maximum = int.MaxValue;
        bar.SmallChange = int.MaxValue; bar.LargeChange = int.MaxValue;
        host.Show(bar);
        var peer = bar.AccessibilityObject;
        Assert.Equal(AccessibleControlType.ScrollBar, peer.ControlType);
        Assert.Equal(vertical ? Orientation.Vertical : Orientation.Horizontal, peer.Orientation);
        Assert.Equal(int.MaxValue, bar.LargeChange);
        Assert.Equal((double)int.MinValue, peer.RangeValue!.Value.Minimum);
        Assert.Equal((double)int.MaxValue, peer.RangeValue!.Value.Maximum);
        Assert.Null(peer.ScrollInfo);
        int scroll = 0, changed = 0;
        bar.Scroll += (_, _) => scroll++;
        bar.ValueChanged += (_, _) => changed++;
        bar.Value = int.MaxValue;
        Assert.True(peer.PerformAction(AccessibleActions.Increment));
        Assert.Equal(int.MaxValue, bar.Value);
        bar.Value = int.MinValue;
        Assert.True(peer.PerformAction(AccessibleActions.Decrement));
        Assert.Equal(int.MinValue, bar.Value);
        Assert.True(peer.PerformAction(AccessibleActions.SetValue, 0d));
        Assert.Equal(3, changed);
        Assert.Equal(0, scroll);
        foreach (object invalid in new object[] { .5, double.NaN, double.NegativeInfinity, (long)int.MaxValue + 1, "0" })
            Assert.False(peer.PerformAction(AccessibleActions.SetValue, invalid));
        bar.Enabled = false;
        Assert.False(peer.PerformAction(AccessibleActions.SetValue, 1));
        bar.Enabled = true;
        bar.Visible = false;
        Assert.False(peer.PerformAction(AccessibleActions.Increment));
        bar.Dispose();
        Assert.Null(peer.RangeValue);
    }

    [Fact]
    public void ScrollBarMetadataAndDegenerateRangeDoNotRaiseFalseScrollOrValueEvents()
    {
        using var host = ModernFormsTestHost.Create();
        var bar = new HorizontalScrollBar { Minimum = 7, Maximum = 7, SmallChange = 0, LargeChange = 0 };
        host.Show(bar);
        int valueEvents = 0, scrollEvents = 0, metadata = 0;
        bar.ValueChanged += (_, _) => valueEvents++;
        bar.Scroll += (_, _) => scrollEvents++;
        var peer = bar.AccessibilityObject;
        peer.ClientNotification += (_, e) => { if (e.EventId == AccessibleEvents.RangeValueChanged) metadata++; };
        Assert.True(peer.PerformAction(AccessibleActions.Increment));
        Assert.True(peer.PerformAction(AccessibleActions.Decrement));
        bar.LargeChange = int.MaxValue;
        Assert.Equal(1, bar.LargeChange);
        bar.SmallChange = 2;
        Assert.Equal(2, metadata);
        Assert.Equal(0, valueEvents);
        Assert.Equal(0, scrollEvents);
        Assert.Equal(7, peer.RangeValue!.Value.Value);
    }

    [Fact]
    public void ScrollBarNotifiesCommittedValueEvenWhenObserversThrow()
    {
        using var host = ModernFormsTestHost.Create();
        var bar = new HorizontalScrollBar();
        host.Show(bar);
        var peer = bar.AccessibilityObject;
        int notifications = 0;
        peer.ClientNotification += (_, e) => {
            if (e.EventId == AccessibleEvents.ValueChange) { notifications++; Assert.Equal(42d, peer.RangeValue!.Value.Value); }
        };
        bar.ValueChanged += (_, _) => throw new ApplicationException("value observer");
        Assert.Throws<ApplicationException>(() => peer.PerformAction(AccessibleActions.SetValue, 42));
        Assert.Equal(42, bar.Value);
        Assert.Equal(1, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicKeyCallbackCannotActivateOrStepReparentedControl(bool numericKind)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        Control target = numericKind ? new NumericUpDown() : new LinkLabel { Text = "link" };
        root.Controls.Add(target);
        host.Show(root);
        int clicks = 0;
        if (target is LinkLabel label) label.LinkClicked += (_, _) => clicks++;
        target.AccessibilityObject.PerformAction(AccessibleActions.Focus);
        target.KeyDown += (_, _) => { root.Controls.Remove(target); root.Controls.Add(target); };
        host.Input.PressKey(numericKind ? Keys.Up : Keys.Enter);
        Assert.Equal(0, clicks);
        if (target is NumericUpDown numeric) Assert.Equal(0m, numeric.Value);
    }

    [Fact]
    public void ClosedWindowRejectsRetainedRealControlAndLogicalChildActions()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var label = root.Controls.Add(new LinkLabel { Text = "open", Width = 100 });
        var numeric = root.Controls.Add(new NumericUpDown { Top = 50 });
        var bar = root.Controls.Add(new HorizontalScrollBar { Top = 100 });
        var window = host.Show(root);
        var peers = new[] { label.AccessibilityObject.GetChild(0)!, numeric.AccessibilityObject,
            numeric.AccessibilityObject.GetChild(1)!, bar.AccessibilityObject };
        window.Close();
        foreach (var peer in peers)
        {
            Assert.Equal(AccessibleActions.None, peer.SupportedActions);
            Assert.False(peer.PerformAction(AccessibleActions.Invoke));
            Assert.False(peer.PerformAction(AccessibleActions.Increment));
        }
    }

    [Fact]
    public void RetainedLinkPeerDoesNotKeepDisposedOwnerOrPayloadAlive()
    {
        var (peer, owner, link) = CreateRetainedLinkPeer();
        for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(owner.IsAlive);
        Assert.False(link.IsAlive);
        Assert.Null(peer.Parent);
        Assert.False(peer.PerformAction(AccessibleActions.Invoke));
        GC.KeepAlive(peer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (AccessibleObject Peer, WeakReference Owner, WeakReference Link) CreateRetainedLinkPeer()
    {
        using var host = ModernFormsTestHost.Create();
        var label = new LinkLabel { Text = "link" };
        host.Show(label);
        var peer = label.AccessibilityObject.GetChild(0)!;
        return (peer, new(label), new(label.Links[0]));
    }

    private sealed class ExplosivePayload
    {
        public override string ToString() => throw new InvalidOperationException("User data must not be inspected.");
    }
}
