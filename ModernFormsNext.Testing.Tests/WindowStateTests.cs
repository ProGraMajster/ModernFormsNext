using System.Reflection;
using ModernFormsNext.WindowKit.Controls;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class WindowStateTests
{
    [Theory]
    [InlineData(FormWindowState.Normal, FormWindowState.Maximized, false)]
    [InlineData(FormWindowState.Maximized, FormWindowState.Normal, false)]
    [InlineData(FormWindowState.Normal, FormWindowState.Minimized, false)]
    [InlineData(FormWindowState.Minimized, FormWindowState.Normal, false)]
    [InlineData(FormWindowState.Maximized, FormWindowState.Minimized, false)]
    [InlineData(FormWindowState.Normal, FormWindowState.Maximized, true)]
    [InlineData(FormWindowState.Maximized, FormWindowState.Normal, true)]
    [InlineData(FormWindowState.Normal, FormWindowState.Minimized, true)]
    [InlineData(FormWindowState.Minimized, FormWindowState.Normal, true)]
    [InlineData(FormWindowState.Maximized, FormWindowState.Minimized, true)]
    public void ConfirmedTransitionHasOldAndNewStateWithoutGeometryDependency(FormWindowState initial, FormWindowState next, bool backendChange)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { WindowState = initial };
        form.Show();
        var size = form.Size;
        int states = 0, geometry = 0;
        form.SizeChanged += (_, _) => geometry++;
        form.WindowStateChanged += (sender, e) => {
            Assert.Same(form, sender);
            Assert.Equal(initial, e.OldState);
            Assert.Equal(next, e.NewState);
            Assert.Equal(next, form.WindowState);
            states++;
        };
        if (backendChange) Backend(form).WindowState = (WindowState)next;
        else form.WindowState = next;
        form.WindowState = form.WindowState;
        Backend(form).WindowStateChanged!((WindowState)next);
        // A stale payload must not change either the property or notification history.
        Backend(form).WindowStateChanged!((WindowState)initial);
        Assert.Equal(1, states);
        Assert.Equal(0, geometry);
        Assert.Equal(size, form.Size);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PreShowConfigurationIsConfirmedAfterLoadAndVisibilityWithoutDuplicates(bool duringLoad, bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        List<string> order = [];
        form.Load += (_, _) => { order.Add("Load"); if (duringLoad) form.WindowState = FormWindowState.Maximized; };
        form.VisibleChanged += (_, _) => order.Add(form.Visible ? "Visible" : "Hidden");
        form.Shown += (_, _) => order.Add("Shown");
        form.WindowStateChanged += (_, e) => {
            Assert.Equal(FormWindowState.Normal, e.OldState);
            Assert.Equal(FormWindowState.Maximized, e.NewState);
            Assert.True(Backend(form).IsShown);
            order.Add("State");
        };
        if (!duringLoad) {
            form.WindowState = FormWindowState.Maximized;
            Assert.Equal(FormWindowState.Maximized, form.WindowState);
            Assert.Empty(order);
        }
        if (modal) _ = form.ShowDialog(owner); else form.Show();
        Assert.Equal(new[] { "Load", "Visible", "State", "Shown" }, order);
        form.Hide();
        form.Show();
        Assert.Equal(new[] { "Load", "Visible", "State", "Shown", "Hidden", "Visible" }, order);
        form.Close();
    }

    [Theory]
    [InlineData(FormWindowState.Normal, FormWindowState.Maximized)]
    [InlineData(FormWindowState.Maximized, FormWindowState.Minimized)]
    [InlineData(FormWindowState.Minimized, FormWindowState.Normal)]
    public void HiddenConfigurationCoalescesUntilNextShow(FormWindowState initial, FormWindowState next)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { WindowState = initial };
        form.Show();
        form.Hide();
        List<WindowStateChangedEventArgs> events = [];
        form.WindowStateChanged += (_, e) => events.Add(e);
        form.WindowState = FormWindowState.Minimized;
        form.WindowState = next;
        Assert.Equal(next, form.WindowState);
        Assert.Empty(events);
        form.Show();
        var change = Assert.Single(events);
        Assert.Equal(initial, change.OldState);
        Assert.Equal(next, change.NewState);
        form.Hide();
        form.Show();
        Assert.Single(events);
    }

    [Fact]
    public void ReentrantChangeSupersedesRemainingOldObservers()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Show();
        List<string> calls = [];
        form.WindowStateChanged += (_, e) => {
            calls.Add($"{e.OldState}>{e.NewState}");
            form.WindowState = form.WindowState;
            if (e.NewState == FormWindowState.Maximized) form.WindowState = FormWindowState.Normal;
        };
        form.WindowStateChanged += (_, e) => { Assert.Equal(e.NewState, form.WindowState); calls.Add("last:" + e.NewState); };
        form.WindowState = FormWindowState.Maximized;
        Assert.Equal(new[] { "Normal>Maximized", "Maximized>Normal", "last:Normal" }, calls);
        Assert.Equal(FormWindowState.Normal, form.WindowState);
        form.Hide();
        form.Show();
        Assert.Equal(FormWindowState.Normal, form.WindowState);
        Assert.Equal(3, calls.Count);
    }

    [Fact]
    public void UnappliedConfigurationDoesNotInventATransition()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        int events = 0;
        form.WindowStateChanged += (_, _) => events++;
        form.WindowState = FormWindowState.Maximized;
        form.WindowState = FormWindowState.Normal;
        form.Show();
        Assert.Equal(0, events);
        form.Hide();
        form.WindowState = FormWindowState.Minimized;
        form.WindowState = FormWindowState.Normal;
        form.Show();
        Assert.Equal(0, events);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CloseOrHideAfterCompletedShowStopsOlderObservers(bool close, bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form();
        var completion = modal ? form.ShowDialog(owner) : null;
        if (!modal) form.Show();
        int later = 0, hidden = 0, closed = 0;
        form.VisibleChanged += (_, _) => { if (!form.Visible) hidden++; };
        form.Closed += (_, _) => closed++;
        form.WindowStateChanged += (_, _) => { if (close) form.Close(); else form.Hide(); };
        form.WindowStateChanged += (_, _) => later++;
        form.WindowState = FormWindowState.Minimized;
        Assert.False(form.Visible);
        Assert.False(Backend(form).IsShown);
        Assert.Equal(1, hidden);
        Assert.Equal(close ? 1 : 0, closed);
        Assert.Equal(0, later);
        if (modal && !close) {
            // Preserve existing Hide semantics for an already established modal session:
            // hiding does not close the dialog or transfer its ownership contract.
            Assert.False(completion!.IsCompleted);
            Assert.False(Backend(owner).IsEnabled);
        }
        form.Close();
        if (modal) Assert.True(completion!.IsCompletedSuccessfully);
        Assert.True(Backend(owner).IsEnabled);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StateObserverCanCancelNativeShowAndRestoreModalOwner(bool close, bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form { WindowState = FormWindowState.Maximized };
        int later = 0, shown = 0;
        form.WindowStateChanged += (_, _) => { if (close) form.Close(); else form.Hide(); };
        form.WindowStateChanged += (_, _) => later++;
        form.Shown += (_, _) => shown++;
        var completion = modal ? form.ShowDialog(owner) : null;
        if (!modal) form.Show();
        Assert.False(form.Visible);
        Assert.False(Backend(form).IsShown);
        Assert.Equal(close, Backend(form).IsDisposed);
        Assert.Equal(0, later);
        Assert.Equal(0, shown);
        Assert.DoesNotContain(form, Application.OpenForms);
        Assert.True(Backend(owner).IsEnabled);
        if (modal) Assert.True(completion!.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObserverFailureDuringShowPreservesStateButRetiresDisplay(bool modal)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        owner.Show();
        using var form = new Form { WindowState = FormWindowState.Maximized };
        var failure = new InvalidOperationException("observer");
        int later = 0;
        EventHandler<WindowStateChangedEventArgs> fail = (_, _) => throw failure;
        form.WindowStateChanged += fail;
        form.WindowStateChanged += (_, e) => { Assert.Equal(e.NewState, form.WindowState); later++; };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => {
            if (modal) _ = form.ShowDialog(owner); else form.Show();
        }));
        Assert.Equal(FormWindowState.Maximized, form.WindowState);
        Assert.Equal(1, later);
        Assert.False(form.Visible);
        Assert.False(Backend(form).IsShown);
        Assert.DoesNotContain(form, Application.OpenForms);
        Assert.True(Backend(owner).IsEnabled);
        form.WindowStateChanged -= fail;
        form.Show();
        Assert.Equal(1, later);
        Assert.True(form.Visible);
    }

    [Fact]
    public void VisibleObserverFailuresAggregateAfterCommitWithoutRetryDuplicates()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Show();
        var first = new InvalidOperationException("first");
        var second = new ArgumentException("second");
        form.WindowStateChanged += (_, _) => throw first;
        form.WindowStateChanged += (_, _) => throw second;
        var error = Assert.Throws<AggregateException>(() => form.WindowState = FormWindowState.Minimized);
        Assert.Equal(new Exception[] { first, second }, error.InnerExceptions);
        Assert.Equal(FormWindowState.Minimized, form.WindowState);
        Assert.True(form.Visible);
        form.WindowState = form.WindowState;
    }

    [Fact]
    public void InstancesAndProtectedHookKeepIndependentNotificationHistory()
    {
        using var host = ModernFormsTestHost.Create();
        using var first = new StateForm();
        using var second = new StateForm();
        first.Show(); second.Show();
        first.WindowState = FormWindowState.Maximized;
        Assert.Single(first.Changes);
        Assert.Empty(second.Changes);
        second.WindowState = FormWindowState.Minimized;
        Assert.Single(first.Changes);
        Assert.Equal(FormWindowState.Minimized, Assert.Single(second.Changes).NewState);
    }

    private sealed class StateForm : Form
    {
        internal List<WindowStateChangedEventArgs> Changes { get; } = [];
        protected override void OnWindowStateChanged(WindowStateChangedEventArgs e) { Changes.Add(e); base.OnWindowStateChanged(e); }
    }

    private static HeadlessWindowImpl Backend(Form form) => Assert.IsType<HeadlessWindowImpl>(
        typeof(WindowBase).GetField("window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form));
}
