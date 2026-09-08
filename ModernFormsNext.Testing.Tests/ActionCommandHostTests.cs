using System.Drawing;
using System.Windows.Input;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ActionCommandHostTests
{
    [Theory]
    [InlineData("menu")]
    [InlineData("toolbar")]
    [InlineData("ribbon")]
    public void ExistingActionsShareQueryClickCurrentParameterAndEnabledRules(string kind)
    {
        using var host = ModernFormsTestHost.Create();
        using var action = new ActionFixture(host, kind);
        var calls = new List<string>();
        var command = new DelegateCommand(p => calls.Add($"execute:{p}"), _ => { calls.Add("query"); return true; });
        action.Item.CommandParameter = "before";
        action.Item.Command = command;
        action.Item.Click += (_, _) => { calls.Add("click"); action.Item.CommandParameter = "after"; };
        calls.Clear();
        action.Item.Invoke();
        Assert.Equal(new[] { "query", "click", "query", "query", "execute:after" }, calls);
        calls.Clear();
        action.Item.Enabled = false;
        command.RaiseCanExecuteChanged();
        calls.Clear();
        action.Item.Invoke();
        Assert.Empty(calls);
        Assert.False(action.Item.Enabled);
    }

    [Theory]
    [InlineData("menu")]
    [InlineData("toolbar")]
    [InlineData("ribbon")]
    public void RoutedTargetAndLogicalSourceStayWithinTheOriginTree(string kind)
    {
        using var host = ModernFormsTestHost.Create();
        using var action = new ActionFixture(host, kind);
        var target = action.Root.Controls.Add(new Button());
        var foreignRoot = new Panel();
        var foreign = foreignRoot.Controls.Add(new Button());
        host.Show(foreignRoot, 300, 200);
        var routed = new RoutedCommand("Save");
        bool allowed = true;
        int calls = 0;
        target.CommandBindings.Add(new(routed, (_, e) =>
        {
            calls++;
            Assert.Same(target, e.Target);
            Assert.Same(action.Owner, e.Source);
            Assert.Equal("document", e.Parameter);
            e.Handled = true;
        }, (_, e) => e.CanExecute = allowed));
        foreign.CommandBindings.Add(new(routed, (_, _) => Assert.Fail("Cross-window target ran"), (_, e) => e.CanExecute = true));
        action.Item.CommandParameter = "document";
        action.Item.CommandTarget = target;
        action.Item.Command = routed;
        Assert.True(action.Item.Enabled);
        action.Item.Invoke();
        Assert.Equal(1, calls);
        allowed = false;
        routed.RaiseCanExecuteChanged();
        Assert.False(action.Item.Enabled);
        action.Item.Invoke();
        allowed = true;
        action.Item.CommandTarget = foreign;
        Assert.False(action.Item.Enabled);
        action.Item.Invoke();
        action.Item.CommandTarget = target;
        Assert.True(action.Item.Enabled);
        action.Item.Invoke();
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("menu")]
    [InlineData("toolbar")]
    [InlineData("ribbon")]
    public void OwnerDisposalReleasesSubscriptionsAndBorrowedValues(string kind)
    {
        using var host = ModernFormsTestHost.Create();
        using var action = new ActionFixture(host, kind);
        var command = new ProbeCommand();
        action.Item.Command = command;
        action.Item.CommandParameter = new object();
        action.Item.CommandTarget = action.Root;
        Assert.Equal(1, command.Subscribers);
        action.Owner.Dispose();
        Assert.Equal(0, command.Subscribers);
        Assert.Null(action.Item.Command);
        Assert.Null(action.Item.CommandParameter);
        Assert.Null(action.Item.CommandTarget);
        action.Item.Invoke();
        Assert.Equal(0, command.Executions);
        Assert.Throws<ObjectDisposedException>(() => action.Item.Command = command);
    }

    [Fact]
    public void RemovedItemSuspendsSubscriptionAndReparentingResumesWithNewRoute()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var first = root.Controls.Add(new Menu());
        var second = root.Controls.Add(new ToolBar());
        host.Show(root, 400, 200);
        var command = new ProbeCommand();
        var item = first.Items.Add(new ActionItem { Command = command });
        Assert.Equal(1, command.Subscribers);
        first.Items.Remove(item);
        Assert.Equal(0, command.Subscribers);
        int queries = command.Queries;
        command.Notify();
        item.Invoke();
        Assert.Equal(queries, command.Queries);
        second.Items.Add(item);
        Assert.Equal(1, command.Subscribers);
        item.Invoke();
        Assert.Equal(1, command.Executions);
        item.Dispose();
        Assert.Equal(0, command.Subscribers);
    }

    [Fact]
    public void ClickReplacementUsesFreshCommandAndPostClickPredicateMutationRejectsOldSnapshot()
    {
        using var host = ModernFormsTestHost.Create();
        using var action = new ActionFixture(host, "menu");
        int oldCalls = 0, newCalls = 0;
        var replacement = new DelegateCommand(() => newCalls++);
        action.Item.Command = new DelegateCommand(() => oldCalls++);
        EventHandler<MouseEventArgs> replace = (_, _) => action.Item.Command = replacement;
        action.Item.Click += replace;
        action.Item.Invoke();
        Assert.Equal(0, oldCalls);
        Assert.Equal(1, newCalls);
        action.Item.Click -= replace;
        bool clicked = false;
        action.Item.Command = new DelegateCommand(() => oldCalls++, () =>
        {
            if (clicked) action.Item.Command = replacement;
            return true;
        });
        action.Item.Click += (_, _) => clicked = true;
        action.Item.Invoke();
        Assert.Equal(0, oldCalls);
        Assert.Equal(1, newCalls);
    }

    [Fact]
    public void OrdinaryCommandIgnoresTargetAndSourceDisposalDuringQueryPreventsExecute()
    {
        using var host = ModernFormsTestHost.Create();
        using var action = new ActionFixture(host, "toolbar");
        var probe = new ProbeCommand();
        action.Item.Command = probe;
        int queries = probe.Queries;
        using var detached = new Button();
        action.Item.CommandTarget = detached;
        Assert.Equal(queries, probe.Queries);
        action.Item.Invoke();
        Assert.Equal(1, probe.Executions);
        bool clicked = false;
        action.Item.Command = new DelegateCommand(() => Assert.Fail(), () =>
        {
            if (clicked) action.Item.Dispose();
            return true;
        });
        action.Item.Click += (_, _) => clicked = true;
        action.Item.Invoke();
    }

    [Fact]
    public void RoutedActionDisposalDuringExecutionStopsAncestorFallback()
    {
        using var host = ModernFormsTestHost.Create();
        using var action = new ActionFixture(host, "menu");
        var command = new RoutedCommand();
        int calls = 0;
        action.Owner.CommandBindings.Add(new(command, (_, _) => { calls++; action.Item.Dispose(); }, (_, e) => e.CanExecute = true));
        action.Root.CommandBindings.Add(new(command, (_, _) => Assert.Fail(), (_, e) => e.CanExecute = true));
        action.Item.Command = command;
        action.Item.Invoke();
        Assert.Equal(1, calls);
    }

    [Fact]
    public void TrayRoutedCommandRequiresExplicitLiveTargetAndHonorsItsOwnDisposal()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var target = root.Controls.Add(new Button());
        var window = host.Show(root, 300, 200);
        var command = new RoutedCommand();
        using var item = new TrayAction { Command = command, CommandParameter = "tray" };
        Assert.False(item.Enabled);
        int calls = 0;
        target.CommandBindings.Add(new(command, (_, e) =>
        {
            Assert.Null(e.Source);
            Assert.Same(target, e.Target);
            Assert.Equal("tray", e.Parameter);
            calls++;
            item.Dispose();
        }, (_, e) => e.CanExecute = true));
        root.CommandBindings.Add(new(command, (_, _) => Assert.Fail(), (_, e) => e.CanExecute = true));
        item.CommandTarget = target;
        Assert.True(item.Enabled);
        item.Invoke();
        Assert.Equal(1, calls);
        using var other = new TrayAction { Command = command, CommandTarget = target };
        window.Close();
        command.RaiseCanExecuteChanged();
        Assert.False(other.Enabled);
        other.Invoke();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MenuSemanticActionUsesAsyncAndRoutedAvailabilityWithoutNewPeers(bool routed)
    {
        using var host = ModernFormsTestHost.Create();
        using var action = new ActionFixture(host, "menu");
        var pending = new TaskCompletionSource();
        var asyncCommand = new AsyncCommand(() => pending.Task);
        var route = new RoutedCommand();
        asyncCommand.CanExecuteChanged += (_, _) => route.RaiseCanExecuteChanged();
        action.Owner.CommandBindings.Add(new(route, (_, e) => { asyncCommand.Execute(null); e.Handled = true; }, (_, e) => e.CanExecute = asyncCommand.CanExecute(null)));
        action.Item.Command = routed ? route : asyncCommand;
        var peer = Assert.IsAssignableFrom<AccessibleObject>(action.Owner.AccessibilityObject.GetChild(0));
        Assert.True(peer.PerformAction(AccessibleActions.Invoke));
        Assert.False(action.Item.Enabled);
        Assert.True(peer.State.HasFlag(AccessibleStates.Unavailable));
        Assert.False(peer.PerformAction(AccessibleActions.Invoke));
        var thread = new Thread(() => pending.SetResult());
        thread.Start();
        thread.Join();
        host.Dispatcher.Drain();
        Assert.True(action.Item.Enabled);
        Assert.Same(peer, action.Owner.AccessibilityObject.GetChild(0));
        Assert.False(peer.State.HasFlag(AccessibleStates.Unavailable));
    }

    private sealed class ActionItem : MenuItem
    {
        public void Invoke() => OnClick(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, Point.Empty));
    }

    private sealed class TrayAction : NotifyIconMenuItem
    {
        public void Invoke() => OnClick(EventArgs.Empty);
    }

    private sealed class ActionFixture : IDisposable
    {
        public Panel Root { get; } = new();
        public Control Owner { get; }
        public MenuItemCollection Collection { get; }
        public ActionItem Item { get; }

        public ActionFixture(ModernFormsTestHost host, string kind)
        {
            Owner = kind switch { "toolbar" => new ToolBar(), "ribbon" => new Ribbon(), _ => new Menu() };
            Root.Controls.Add(Owner);
            Collection = Owner is Ribbon ribbon ? ribbon.TabPages.Add("Home").Groups.Add("Actions").Items : ((MenuBase)Owner).Items;
            Item = Collection.Add(new ActionItem { Text = "Run" });
            host.Show(Root, 500, 300);
        }

        public void Dispose() => Owner.Dispose();
    }

    private sealed class ProbeCommand : ICommand
    {
        private EventHandler? changed;
        public int Subscribers, Queries, Executions;
        public event EventHandler? CanExecuteChanged
        {
            add { changed += value; Subscribers++; }
            remove { changed -= value; Subscribers--; }
        }
        public bool CanExecute(object? parameter) { Queries++; return true; }
        public void Execute(object? parameter) => Executions++;
        public void Notify() => changed?.Invoke(this, EventArgs.Empty);
    }
}
