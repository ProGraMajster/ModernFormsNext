using System.Drawing;
using System.Reflection;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

// TestHost intentionally does not implement popups. Reuse the existing semantic-test window
// proxy pattern to exercise real MenuDropDown.Show without extending the public TestHost scope.
[Collection(InputBindingCollectionTests.Name)]
public sealed class ActionCommandPopupTests
{
    [Fact]
    public void ActualWindowCloseIgnoresAsyncCompletionBeforeControlDisposal()
    {
        using var ui = new PopupFixture();
        var pending = new TaskCompletionSource();
        var command = new AsyncCommand(() => pending.Task);
        var button = ui.Origin.Controls.Add(new Button { Command = command });
        button.PerformClick();
        ui.Form.Close();
        Assert.False(button.IsDisposed); // Exercise production Close, not TestHost's forced cleanup.
        int mutations = 0;
        button.EnabledChanged += (_, _) => mutations++;
        var context = SynchronizationContext.Current;
        try { SynchronizationContext.SetSynchronizationContext(null); pending.SetResult(); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
        Assert.True(command.ExecutionTask!.IsCompletedSuccessfully);
        command.RaiseCanExecuteChanged();
        Assert.Equal(0, mutations);
        Assert.False(button.Enabled);
    }

    [Fact]
    public void ContextActionQueriesBeforeAndAfterClickAndPreservesParameterAndLocalEnabled()
    {
        using var ui = new PopupFixture();
        var calls = new List<string>();
        var command = new DelegateCommand(p => calls.Add($"execute:{p}"), _ => { calls.Add("query"); return true; });
        ui.Item.CommandParameter = "document";
        ui.Item.Command = command;
        ui.Item.Click += (_, _) => calls.Add("click");
        calls.Clear();
        ui.Item.Invoke();
        Assert.Equal(new[] { "query", "click", "query", "execute:document" }, calls);
        ui.Item.Enabled = false;
        command.RaiseCanExecuteChanged();
        calls.Clear();
        ui.Item.Invoke();
        Assert.Empty(calls);
    }

    [Fact]
    public void ContextTargetUsesOriginBoundaryAndNeverTraversesPopupOrForeignWindow()
    {
        using var ui = new PopupFixture();
        using var foreign = new PopupFixture();
        var target = ui.Origin.Controls.Add(new Button());
        var routed = new RoutedCommand();
        int calls = 0;
        bool allowed = true;
        target.CommandBindings.Add(new(routed, (_, e) =>
        {
            Assert.Same(ui.Origin, e.Source);
            Assert.Same(target, e.Target);
            Assert.Equal("context", e.Parameter);
            calls++;
            e.Handled = true;
        }, (_, e) => e.CanExecute = allowed));
        ui.Menu.CommandBindings.Add(new(routed, (_, _) => Assert.Fail("Popup entered logical route"), (_, e) => e.CanExecute = true));
        foreign.Origin.CommandBindings.Add(new(routed, (_, _) => Assert.Fail("Foreign window entered route"), (_, e) => e.CanExecute = true));
        ui.Item.CommandParameter = "context";
        ui.Item.CommandTarget = target;
        ui.Item.Command = routed;
        ui.Item.Invoke();
        Assert.Equal(1, calls);
        allowed = false;
        routed.RaiseCanExecuteChanged();
        Assert.False(ui.Item.Enabled);
        allowed = true;
        ui.Item.CommandTarget = foreign.Origin;
        Assert.False(ui.Item.Enabled);
        ui.Item.Invoke();
        ui.Item.CommandTarget = target;
        ui.Item.Invoke();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ContextShowRefreshesOriginAndForeignWindowReuseFailsClosedForRouting()
    {
        using var ui = new PopupFixture();
        using var foreign = new PopupFixture();
        var nested = ui.Origin.Controls.Add(new Panel());
        var routed = new RoutedCommand();
        int calls = 0;
        nested.CommandBindings.Add(new(routed, (_, e) => { calls++; Assert.Same(nested, e.Source); e.Handled = true; }, (_, e) => e.CanExecute = true));
        foreign.Origin.CommandBindings.Add(new(routed, (_, _) => Assert.Fail(), (_, e) => e.CanExecute = true));
        ui.Item.Command = routed;
        Assert.False(ui.Item.Enabled);
        ui.Menu.Show(nested, Point.Empty);
        Assert.True(ui.Item.Enabled);
        ui.Item.Invoke();
        Assert.Equal(1, calls);
        ui.Menu.Show(foreign.Origin, Point.Empty);
        Assert.False(ui.Item.Enabled);
        ui.Item.Invoke();
    }

    [Fact]
    public void ContextDisposalReleasesBorrowedCommandParameterAndTarget()
    {
        using var ui = new PopupFixture();
        var command = new DelegateCommand(() => Assert.Fail());
        ui.Item.Command = command;
        ui.Item.CommandParameter = new object();
        ui.Item.CommandTarget = ui.Origin;
        ui.Menu.Dispose();
        Assert.Null(ui.Item.Command);
        Assert.Null(ui.Item.CommandParameter);
        Assert.Null(ui.Item.CommandTarget);
        command.RaiseCanExecuteChanged();
        ui.Item.Invoke();
        Assert.False(ui.Item.Enabled);
    }

    [Fact]
    public void NestedPopupKeepsLogicalMenuRouteAndOnlyBorrowsCommandItems()
    {
        using var ui = new PopupFixture();
        using var menu = ui.Origin.Controls.Add(new Menu());
        var parent = menu.Items.Add("File");
        var item = parent.Items.Add(new ActionItem());
        var command = new RoutedCommand();
        int calls = 0;
        menu.CommandBindings.Add(new(command, (_, e) => { Assert.Same(menu, e.Source); calls++; e.Handled = true; }, (_, e) => e.CanExecute = true));
        item.Command = command;
        using var popup = new MenuDropDown(parent);
        popup.Show(menu, Point.Empty);
        item.Invoke();
        Assert.Equal(1, calls);
        popup.Dispose();
        Assert.Same(command, item.Command);
        using var next = new MenuDropDown(parent);
        next.Show(menu, Point.Empty);
        item.Invoke();
        Assert.Equal(2, calls);
    }

    private sealed class ActionItem : MenuItem
    {
        internal void Invoke() => OnClick(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, Point.Empty));
    }

    private sealed class PopupFixture : IDisposable
    {
        internal Form Form { get; } = new(DispatchProxy.Create<IWindowImpl, WindowProxy>());
        internal Panel Origin { get; }
        internal ContextMenu Menu { get; } = new();
        internal ActionItem Item { get; }
        internal PopupFixture()
        {
            Origin = Form.Controls.Add(new Panel());
            Item = Menu.Items.Add(new ActionItem { Text = "Run" });
            Menu.Show(Origin, Point.Empty);
        }
        public void Dispose()
        {
            Menu.Hide();
            Menu.Dispose();
            Form.Dispose();
            Form.adapter.Dispose();
        }
    }

    private class WindowProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(ITopLevelImpl.CreatePopup)) return DispatchProxy.Create<IPopupImpl, WindowProxy>();
            if (method?.Name is "get_RenderScaling" or "get_DesktopScaling") return 1d;
            return method?.ReturnType is { } type && type != typeof(void) && type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
