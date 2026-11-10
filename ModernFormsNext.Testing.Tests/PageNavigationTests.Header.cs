using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class PageNavigationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuiltInHeaderFollowsCommittedTitleAndUsesGuardedBack(bool neutral)
    {
        using var f = new Fixture(neutral);
        using var detail = NewPage("detail");
        var navigation = f.Navigation;
        var header = navigation.NavigationBar;
        navigation.TransitionDuration = TimeSpan.Zero;
        Assert.True(header.Visible);
        Assert.False(header.BackButton.Visible);
        Assert.Equal("root", header.TitleLabel.Text);
        Assert.Same(navigation.BackCommand, header.BackButton.Command);
        Assert.Same(header.AccessibilityObject, navigation.AccessibilityObject.GetChild(0));
        Assert.Equal(AccessibleControlType.ToolBar, header.AccessibilityObject.ControlType);
        f.Root.Content!.Select();
        Assert.True(Done(navigation.PushAsync(detail, false)));
        Assert.True(header.BackButton.Visible);
        Assert.Equal("detail", header.TitleLabel.Text);
        detail.Title = "updated";
        f.Root.Title = "retained";
        Assert.Equal("updated", header.TitleLabel.Text);
        Assert.Equal("Back", header.BackButton.AccessibilityObject.Name);

        EventHandler<NavigationChangingEventArgs> veto = (_, e) => e.Cancel = true;
        navigation.Navigating += veto;
        header.BackButton.PerformClick();
        Assert.True(navigation.BackCommand.ExecutionTask!.IsCompletedSuccessfully);
        Assert.Same(detail, navigation.CurrentPage);
        navigation.Navigating -= veto;
        header.BackButton.Select();
        Assert.Same(header.BackButton, f.Focused);
        header.BackButton.PerformClick();
        Assert.True(navigation.BackCommand.ExecutionTask!.IsCompletedSuccessfully);
        Assert.Same(f.Root, navigation.CurrentPage);
        Assert.Same(f.Root.Content, f.Focused);
        Assert.Null(detail.Parent);
        Assert.False(header.BackButton.Visible);
        Assert.Equal("retained", header.TitleLabel.Text);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void HeaderReservesLogicalSpaceAndCanMoveOrOptOut(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(400, 250, scale));
        var root = NewPage("root");
        var navigation = new NavigationPage(root);
        var window = host.Show(new PageHost { Page = navigation });
        window.LayoutUntilStable();
        var header = navigation.NavigationBar;
        var hostFrame = root.Parent!.Parent!;
        Assert.Equal(new Rectangle(0, 0, 400, 42), header.Bounds);
        Assert.Equal(new Rectangle(0, 42, 400, 208), hostFrame.Bounds);
        navigation.NavigationBarHeight = 54;
        navigation.NavigationBarAtBottom = true;
        window.LayoutUntilStable();
        Assert.Equal(new Rectangle(0, 196, 400, 54), header.Bounds);
        Assert.Equal(new Rectangle(0, 0, 400, 196), hostFrame.Bounds);
        navigation.NavigationBarVisible = false;
        window.LayoutUntilStable();
        Assert.False(header.Visible);
        Assert.Equal(new Rectangle(0, 0, 400, 250), hostFrame.Bounds);
        Assert.Equal(1, navigation.AccessibilityObject.GetChildCount());
    }

    [Fact]
    public void ActivePageCanOptOutWithoutHidingOtherPagesOrTheirBackRoute()
    {
        using var f = new Fixture(false);
        using var detail = NewPage("detail");
        var navigation = f.Navigation;
        f.Root.HideNavigationBar = true;
        Assert.False(navigation.NavigationBar.Visible);
        Assert.True(Done(navigation.PushAsync(detail, false)));
        Assert.True(navigation.NavigationBar.Visible);
        Assert.True(navigation.NavigationBar.BackButton.Visible);
        detail.HideNavigationBar = true;
        Assert.False(navigation.NavigationBar.Visible);
        Assert.True(navigation.CanGoBack);
        detail.HideNavigationBar = false;
        Assert.True(navigation.NavigationBar.Visible);
        Assert.Same(detail, Done(navigation.PopAsync(false)));
        Assert.False(navigation.NavigationBar.Visible);
    }

    [Fact]
    public void BuiltInTextMetricsGrowTheHeaderAndNarrowWidthHidesActions()
    {
        using var f = new Fixture(false);
        var navigation = f.Navigation;
        var header = navigation.NavigationBar;
        f.Root.SetNavigationActions(new NavigationAction("Refresh", new DelegateCommand(() => { })));
        var refresh = Assert.Single(header.Controls.OfType<Button>(), button => button.Text == "Refresh");
        Assert.True(refresh.Visible);
        var title = header.TitleLabel;
        title.Font = new Font(title.Font.FamilyName, 54);
        Assert.True(header.Height > navigation.NavigationBarHeight);
        f.Presenter.Width = 90;
        f.Presenter.Dock = DockStyle.None;
        f.Presenter.Width = 90;
        Assert.False(refresh.Visible);
        Assert.True(title.Width <= header.Width);
    }

    [Fact]
    public void SelectedTabAndFlyoutDetailDoNotCompeteWithMenuOrAncestorHeaders()
    {
        using var host = ModernFormsTestHost.Create();
        var menu = new NavigationPage(NewPage("menu"));
        var detail = new NavigationPage(NewPage("detail"));
        var other = new NavigationPage(NewPage("other"));
        var flyout = new FlyoutPage { Flyout = menu, Detail = detail, DisplayMode = FlyoutDisplayMode.Split };
        var tabs = new TabbedPage();
        tabs.Pages.Add(flyout);
        tabs.Pages.Add(other);
        var outer = new NavigationPage(tabs);
        using var presenter = new PageHost { Page = outer };
        using var surface = new SkiaControlSurface(presenter);
        surface.Resize(720, 400);

        Assert.True(outer.NavigationBar.Visible);
        Assert.False(detail.NavigationBar.Visible);
        Assert.False(menu.NavigationBar.Visible);
        Assert.False(other.NavigationBar.Visible);
        tabs.HideNavigationBar = true;
        Assert.True(detail.NavigationBar.Visible);
        Assert.False(menu.NavigationBar.Visible);
        tabs.SelectedIndex = 1;
        Assert.False(detail.NavigationBar.Visible);
        Assert.True(other.NavigationBar.Visible);
    }

    [Fact]
    public void NestedNavigationUsesOneHeaderAndExistingSafeArea()
    {
        using var host = ModernFormsTestHost.Create();
        var inner = new NavigationPage(NewPage("inner"));
        var outer = new NavigationPage(inner);
        using var presenter = new PageHost { Page = outer };
        using var surface = new SkiaControlSurface(presenter);
        surface.Resize(400, 300);
        surface.Insets = new WindowInsets(new Thickness(9, 17, 5, 11), new Thickness(0, 0, 0, 84));

        Assert.Equal(new Rectangle(9, 17, 386, 272), presenter.Bounds);
        Assert.Equal(new Rectangle(0, 0, 386, 42), outer.NavigationBar.Bounds);
        Assert.False(inner.NavigationBar.Visible);
        outer.NavigationBarVisible = false;
        Assert.False(outer.NavigationBar.Visible);
        Assert.True(inner.NavigationBar.Visible);
        Assert.Equal(new Rectangle(0, 0, 386, 42), inner.NavigationBar.Bounds);
    }

    [Fact]
    public void RuntimeViewsAndPageActionsUseOneRealHeaderAndRespectRtlAndLifetime()
    {
        using var f = new Fixture(false);
        using var detail = NewPage("detail");
        using var customTitle = new Label { Text = "Custom title" };
        var fullHeader = new Panel();
        bool titleDisposed = false, fullDisposed = false, actionDisposed = false;
        customTitle.Disposed += (_, _) => titleDisposed = true;
        fullHeader.Disposed += (_, _) => fullDisposed = true;
        var navigation = f.Navigation;
        var header = navigation.NavigationBar;
        var action = new RoutedCommand("Save detail");
        int calls = 0;
        bool allowed = true;
        detail.CommandBindings.Add(new(action, (_, e) =>
        {
            Assert.Same(detail, e.Target);
            Assert.Equal("document", e.Parameter);
            calls++;
            e.Handled = true;
        }, (_, e) => e.CanExecute = allowed));
        detail.SetNavigationActions(new NavigationAction("Save", action, "document"));
        Assert.True(Done(navigation.PushAsync(detail, false)));
        var save = Assert.Single(header.Controls.OfType<Button>(), control => control.Text == "Save");
        save.Disposed += (_, _) => actionDisposed = true;
        Assert.True(save.Visible);
        Assert.True(header.BackButton.Left < save.Left);
        navigation.RightToLeft = RightToLeft.Yes;
        Assert.True(header.BackButton.Left > save.Left);
        save.PerformClick();
        Assert.Equal(1, calls);
        allowed = false;
        action.RaiseCanExecuteChanged();
        Assert.False(save.Enabled);
        save.PerformClick();
        Assert.Equal(1, calls);

        header.TitleView = customTitle;
        Assert.Same(header, customTitle.Parent);
        Assert.True(customTitle.Visible);
        Assert.False(header.TitleLabel.Visible);
        header.HeaderView = fullHeader;
        Assert.True(fullHeader.Visible);
        Assert.False(customTitle.Visible);
        Assert.False(header.BackButton.Visible);
        Assert.False(save.Visible);
        header.HeaderView = null;
        Assert.Null(fullHeader.Parent);
        Assert.False(fullDisposed);
        Assert.True(customTitle.Visible);

        Assert.Same(detail, Done(navigation.PopAsync(false)));
        Assert.True(actionDisposed);
        Assert.DoesNotContain(header.Controls.OfType<Button>(), control => control.Text == "Save");
        header.HeaderView = fullHeader;
        navigation.Dispose();
        Assert.True(fullDisposed);
        Assert.True(titleDisposed);
    }

    [Fact]
    public void NewerRuntimeActionListWinsDuringControlAttachment()
    {
        using var f = new Fixture(false);
        var header = f.Navigation.NavigationBar;
        var command = new DelegateCommand(() => { });
        header.ControlAdded += (_, args) =>
        {
            if (args.Value is Button { Text: "Previous" })
                f.Root.SetNavigationActions(new NavigationAction("Current", command));
        };
        f.Root.SetNavigationActions(new NavigationAction("Previous", command), new NavigationAction("Stale", command));
        var actions = header.Controls.OfType<Button>().Where(button => button != header.BackButton);
        var current = Assert.Single(actions);
        Assert.Equal("Current", current.Text);
        Assert.Same(command, current.Command);
    }

    [Fact]
    public void HeaderBackCannotPopTwiceDuringACommittedTransition()
    {
        using var f = new Fixture(false);
        using var detail = NewPage("detail");
        using var last = NewPage("last");
        Assert.True(Done(f.Navigation.PushAsync(detail, false)));
        Assert.True(Done(f.Navigation.PushAsync(last, false)));
        var back = f.Navigation.NavigationBar.BackButton;
        back.PerformClick();
        Assert.True(f.Navigation.IsNavigating);
        Assert.Same(detail, f.Navigation.CurrentPage);
        back.PerformClick();
        f.Host.Clock.Advance(TimeSpan.FromMilliseconds(180));
        Assert.Same(detail, f.Navigation.CurrentPage);
        Assert.Equal(2, f.Navigation.NavigationStack.Count);
    }
}
