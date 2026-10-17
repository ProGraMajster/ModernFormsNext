using System.Collections.ObjectModel;
using System.Drawing;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class CarouselViewTests
{
    [Theory]
    [InlineData(830)]
    [InlineData(601)]
    [InlineData(1371)]
    public void LargePageCoordinatesUseTheCanonicalLayoutRounding(int width)
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(width, 490);
        carousel.ItemsSource = Enumerable.Range(0, 100_003).ToArray();
        carousel.SelectedIndex = 90_000;
        Assert.True(carousel.MovePrevious());
        Assert.Equal(89_999, carousel.SelectedIndex);
        Assert.NotNull(carousel.GetItemContainer(89_999));
        Assert.True(carousel.MoveNext());
        Assert.Equal(90_000, carousel.SelectedIndex);
    }

    [Fact]
    public void HundredThousandPagesUseSharedBoundedContainersAndViewportSizedCells()
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        carousel.ItemsSource = Enumerable.Range(0, 100_003).ToArray();
        Assert.Equal(0, carousel.SelectedIndex);
        carousel.SelectedIndex = 90_000;
        Assert.Equal(90_000, carousel.SelectedItem);
        var row = Assert.IsType<Control.ControlAccessibleObject>(
            carousel.GetItemContainer(90_000)!.AccessibilityObject);
        Assert.Equal(carousel.PageSize, carousel.GetItemContainer(90_000)!.Size);
        var snapshot = carousel.GetVirtualizationSnapshot();
        Assert.Equal(100_003, snapshot.ItemCount);
        Assert.InRange(snapshot.RealizedContainerCount, 1, 1 + 2 * carousel.CacheItemCount);
        long created = snapshot.CreatedContainerCount;
        for (int index = 0; index < 200; index++) carousel.SelectedIndex = 1000 + index * 433;
        Assert.Equal(created, carousel.GetVirtualizationSnapshot().CreatedContainerCount);
        Assert.NotNull(row);
    }

    [Fact]
    public void SourceReplacementSelectsFirstAndResetRetiresSelection()
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        carousel.ItemsSource = new[] { "a", "b" };
        carousel.SelectedIndex = 1;
        carousel.ItemsSource = new[] { "new-a", "new-b" };
        Assert.Equal(0, carousel.SelectedIndex);
        Assert.Equal("new-a", carousel.SelectedItem);
        carousel.RefreshItems();
        Assert.Equal(-1, carousel.SelectedIndex);
        Assert.True(carousel.MoveNext());
        Assert.Equal(0, carousel.SelectedIndex);
    }

    [Fact]
    public void ExplicitNoSelectionFromReplacementCallbackWins()
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        carousel.ItemsSource = new[] { "a", "b" };
        carousel.SelectedIndex = 1;
        carousel.SelectedIndexChanged += (_, _) =>
        {
            if (carousel.SelectedIndex < 0) carousel.SelectedIndex = -1;
        };
        carousel.ItemsSource = new[] { "new-a", "new-b" };
        Assert.Equal(-1, carousel.SelectedIndex);
    }

    [Fact]
    public void ReentrantNavigationRevealsOnlyNewestSelection()
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        carousel.ItemsSource = Enumerable.Range(0, 10).ToArray();
        carousel.SelectedIndexChanged += (_, _) =>
        {
            if (carousel.SelectedIndex == 1) carousel.SelectedIndex = 7;
        };
        carousel.MoveNext();
        Assert.Equal(7, carousel.SelectedIndex);
        Assert.False(carousel.GetItemContainer(7)!.Bounds.IsEmpty);
        var logical = carousel.AccessibilityObject.GetChild(0)!.GetChild(7)!;
        Assert.True((logical.State & Accessibility.AccessibleStates.Selected) != 0);
        Assert.False((logical.State & Accessibility.AccessibleStates.Offscreen) != 0);
    }

    [Fact]
    public void DuplicateOccurrencesMapAndRemovedPeerRetires()
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        object duplicate = new();
        var source = new ObservableCollection<object> { duplicate, duplicate, "last" };
        carousel.ItemsSource = source;
        carousel.SelectedIndex = 1;
        var list = carousel.AccessibilityObject.GetChild(0)!;
        var peer = list.GetChild(1)!;
        source.Insert(0, "new");
        Assert.Equal(2, carousel.SelectedIndex);
        Assert.Same(peer, list.GetChild(2));
        source.Move(2, 0);
        Assert.Equal(0, carousel.SelectedIndex);
        Assert.Same(peer, list.GetChild(0));
        source.RemoveAt(0);
        Assert.Equal(-1, carousel.SelectedIndex);
        Assert.Null(peer.Parent);
    }

    [Fact]
    public void ResizingAndChangingAxisKeepSelectedPageRealized()
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        carousel.ItemsSource = Enumerable.Range(0, 100_003).ToArray();
        carousel.SelectedIndex = 75_000;
        surface.Resize(640, 320);
        Assert.Equal(75_000, carousel.SelectedIndex);
        Assert.Equal(carousel.PageSize, carousel.GetItemContainer(75_000)!.Size);
        carousel.PageOrientation = Orientation.Vertical;
        Assert.Equal(75_000, carousel.SelectedIndex);
        Assert.Equal(carousel.PageSize, carousel.GetItemContainer(75_000)!.Size);
    }

    [Fact]
    public void SwipeSelectsNearestPageAndCancelRestoresExistingSelection()
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        carousel.ItemsSource = new[] { "a", "b", "c" };
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 270, 40);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 30, 40);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 30, 40);
        Assert.Equal(1, carousel.SelectedIndex);
        surface.ProcessPointer(2, ControlSurfacePointerAction.Down, 270, 40);
        surface.ProcessPointer(2, ControlSurfacePointerAction.Move, 30, 40);
        surface.ProcessPointer(2, ControlSurfacePointerAction.Cancel, 30, 40);
        Assert.Equal(1, carousel.SelectedIndex);
        var peer = carousel.AccessibilityObject.GetChild(0)!.GetChild(1)!;
        Assert.False((peer.State & Accessibility.AccessibleStates.Offscreen) != 0);
        Assert.Equal(0, surface.ActivePointerCount);
    }

    [Fact]
    public void ProgrammaticNavigationPreservesOutsideFocusAndDoesNotWrap()
    {
        using var root = new Panel();
        var carousel = root.Controls.Add(new CarouselView { Width = 320, Height = 180 });
        var editor = root.Controls.Add(new TextBox { Left = 350, Width = 100, Height = 40 });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(500, 200);
        carousel.ItemsSource = new[] { "a", "b" };
        editor.Select();
        Assert.False(carousel.MovePrevious());
        Assert.True(carousel.MoveNext());
        Assert.False(carousel.MoveNext());
        Assert.True(editor.Focused);
    }

    [Fact]
    public void MouseWheelAdvancesWholePagesInsteadOfSnappingPartialScrollBack()
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        carousel.ItemsSource = new[] { "a", "b", "c" };
        carousel.RaiseMouseWheel(new(MouseButtons.None, 0, 40, 40, new Point(0, -1)));
        Assert.Equal(1, carousel.SelectedIndex);
        carousel.RaiseMouseWheel(new(MouseButtons.None, 0, 40, 40, new Point(0, 1)));
        Assert.Equal(0, carousel.SelectedIndex);
    }

    [Fact]
    public void CanonicalLogicalPeerCanRevealAndFocusUnrealizedPage()
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        carousel.ItemsSource = Enumerable.Range(0, 100_003).ToArray();
        var peer = carousel.AccessibilityObject.GetChild(0)!.GetChild(90_000)!;
        Assert.True(peer.PerformAction(Accessibility.AccessibleActions.ScrollIntoView));
        Assert.Equal(90_000, carousel.SelectedIndex);
        Assert.True(peer.PerformAction(Accessibility.AccessibleActions.Focus));
        Assert.Same(peer, carousel.AccessibilityObject.GetFocused());
    }

    [Fact]
    public void CanonicalScrollPatternNavigatesToItsRequestedPage()
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        carousel.ItemsSource = Enumerable.Range(0, 100_003).ToArray();
        var list = carousel.AccessibilityObject.GetChild(0)!;
        Assert.True(list.PerformAction(Accessibility.AccessibleActions.Scroll,
            Accessibility.AccessibleScrollRequest.ToPercent(100, null)));
        Assert.Equal(100_002, carousel.SelectedIndex);
        Assert.NotNull(carousel.GetItemContainer(100_002));
    }

    [Fact]
    public void AccessiblePageFocusHonorsSelectionHandlerFocusRedirect()
    {
        using var root = new Panel();
        var carousel = root.Controls.Add(new CarouselView { Width = 320, Height = 180 });
        var editor = root.Controls.Add(new TextBox { Left = 350, Width = 100, Height = 40 });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(500, 200);
        carousel.ItemsSource = Enumerable.Range(0, 100_003).ToArray();
        carousel.GetItemContainer(0)!.Select();
        carousel.SelectedIndexChanged += (_, _) => { if (carousel.SelectedIndex == 90_000) editor.Select(); };
        var peer = carousel.AccessibilityObject.GetChild(0)!.GetChild(90_000)!;
        Assert.False(peer.PerformAction(Accessibility.AccessibleActions.Focus));
        Assert.True(editor.Focused);
    }

    [Theory]
    [InlineData("selection")]
    [InlineData("source")]
    [InlineData("resize")]
    [InlineData("hide-show")]
    [InlineData("dispose")]
    public void InFlightSwipeCannotReplaceNewerStateOrRetiredHost(string transition)
    {
        using var carousel = new CarouselView();
        using var surface = new SkiaControlSurface(carousel);
        surface.Resize(320, 180);
        carousel.ItemsSource = new[] { "a", "b", "c" };
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 270, 40);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 30, 40);
        switch (transition)
        {
            case "selection": carousel.SelectedIndex = 2; break;
            case "source": carousel.ItemsSource = new[] { "new-a", "new-b" }; break;
            case "resize": surface.Resize(640, 320); break;
            case "hide-show": carousel.Visible = false; carousel.Visible = true; break;
            case "dispose": carousel.Dispose(); break;
        }
        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 30, 40);
        Assert.Equal(0, surface.ActivePointerCount);
        if (transition == "dispose") Assert.True(carousel.IsDisposed);
        else Assert.Equal(transition == "selection" ? 2 : 0, carousel.SelectedIndex);
    }
}
