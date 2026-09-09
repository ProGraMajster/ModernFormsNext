using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

[Trait("Category", "Actions")]
public sealed class ActionTests
{
    [Fact]
    public void InvokePreservesClickBeforeDelegateCommand()
    {
        using var f = new AutomationFixture();
        var order = new List<string>();
        var b = f.Add(new Button { Command = new DelegateCommand(() => order.Add("command")) });
        b.Click += (_, _) => order.Add("click");
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(b, AccessibleActions.Invoke).Status);
        Assert.Equal(new[] { "click", "command" }, order);
    }

    [Fact]
    public void RoutedCommandUsesNormalSourceAndBindings()
    {
        using var f = new AutomationFixture();
        var panel = f.Add(new Panel());
        var command = new RoutedCommand("save");
        int calls = 0;
        panel.CommandBindings.Add(new(command, (_, e) => { calls++; e.Handled = true; }, (_, e) => e.CanExecute = true));
        var button = panel.Controls.Add(new Button { Command = command });
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(button, AccessibleActions.Invoke).Status);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void AsyncCommandAcceptancePrecedesBusinessCompletion()
    {
        using var f = new AutomationFixture();
        var pending = new TaskCompletionSource();
        int started = 0;
        var command = new AsyncCommand(() => { started++; return pending.Task; });
        var button = f.Add(new Button { Command = command });
        var result = f.Act(button, AccessibleActions.Invoke);
        Assert.Equal(AutomationActionStatus.Accepted, result.Status);
        Assert.True(command.IsExecuting);
        Assert.Equal(1, started);
        Assert.False(command.ExecutionTask!.IsCompleted);
        CompletedTaskAssertions.Worker(pending.SetResult);
        f.Host.Dispatcher.Drain();
        Assert.True(pending.Task.IsCompletedSuccessfully);
        Assert.True(command.ExecutionTask.IsCompletedSuccessfully);
        Assert.False(command.IsExecuting);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisabledOrCanExecuteFalseCannotExecute(bool commandDenied)
    {
        using var f = new AutomationFixture();
        int calls = 0;
        var b = f.Add(new Button { Command = new DelegateCommand(() => calls++, () => !commandDenied) });
        if (!commandDenied) b.Enabled = false;
        var result = f.Act(b, AccessibleActions.Invoke);
        Assert.Equal(AutomationActionStatus.Rejected, result.Status);
        Assert.Equal(AutomationErrorCode.ActionRejected, result.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ClickCanCancelCommandThroughNormalCanExecuteRecheck()
    {
        using var f = new AutomationFixture();
        bool allowed = true; int calls = 0;
        var b = f.Add(new Button { Command = new DelegateCommand(() => calls++, () => allowed) });
        b.Click += (_, _) => allowed = false;
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(b, AccessibleActions.Invoke).Status);
        Assert.Equal(0, calls); // Acceptance belongs to canonical activation, not command completion.
    }

    [Fact]
    public void ToggleAndRadioSelectionChangeRealControlState()
    {
        using var f = new AutomationFixture();
        var check = f.Add(new CheckBox());
        var first = f.Add(new RadioButton { Checked = true });
        var second = f.Add(new RadioButton());
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(check, AccessibleActions.Toggle).Status);
        Assert.True(check.Checked);
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(second, AccessibleActions.Select).Status);
        Assert.True(second.Checked);
        Assert.False(first.Checked);
    }

    [Fact]
    public void SetValueAndClearUseEditableTextBoxSemantics()
    {
        using var f = new AutomationFixture();
        var text = f.Add(new TextBox());
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(text, AccessibleActions.SetValue, AutomationActionValue.FromText("changed")).Status);
        Assert.Equal("changed", text.Text);
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(text, AccessibleActions.SetValue).Status);
        Assert.Equal(string.Empty, text.Text);
    }

    [Fact]
    public void FocusChangesRealControlFocus()
    {
        using var f = new AutomationFixture();
        var text = f.Add(new TextBox());
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(text, AccessibleActions.Focus).Status);
        Assert.True(text.Focused);
        Assert.True((f.Inspect(text).Value!.States & AccessibleStates.Focused) != 0);
    }

    [Fact]
    public void ListSelectionUsesLogicalPeer()
    {
        using var f = new AutomationFixture();
        var list = f.Add(new ListBox()); list.Items.Add("one"); list.Items.Add("two");
        var node = f.Session.FindOneAsync(f.Root.RootId, new() { Name = "two", ControlType = AccessibleControlType.ListItem }).Completed().Value!;
        Assert.Equal(AutomationActionStatus.Accepted, f.Session.PerformActionAsync(f.Root.RootId, node.Handle, AccessibleActions.Select).Completed().Status);
        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void TreeExpansionAndCollapseUseLogicalPeer()
    {
        using var f = new AutomationFixture();
        var tree = f.Add(new TreeView()); var branch = tree.Items.Add(new TreeViewItem("branch", new TreeViewItem("leaf")));
        var handle = f.Handle(tree.AccessibilityObject.GetChild(0)!);
        Assert.Equal(AutomationActionStatus.Accepted, f.Session.PerformActionAsync(f.Root.RootId, handle, AccessibleActions.Expand).Completed().Status);
        Assert.True(branch.Expanded);
        Assert.Equal(AutomationActionStatus.Accepted, f.Session.PerformActionAsync(f.Root.RootId, handle, AccessibleActions.Collapse).Completed().Status);
        Assert.False(branch.Expanded);
    }

    [Fact]
    public void NumericSetIncrementAndDecrementUseCanonicalRange()
    {
        using var f = new AutomationFixture();
        var track = f.Add(new TrackBar { Minimum = 0, Maximum = 10, SmallChange = 2 });
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(track, AccessibleActions.SetValue, AutomationActionValue.FromNumber(4)).Status);
        Assert.Equal(4, track.Value);
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(track, AccessibleActions.Increment).Status);
        Assert.Equal(6, track.Value);
        Assert.Equal(AutomationActionStatus.Accepted, f.Act(track, AccessibleActions.Decrement).Status);
        Assert.Equal(4, track.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(2.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidNumericValuesAreRejectedWithoutMutation(double number)
    {
        using var f = new AutomationFixture();
        var track = f.Add(new TrackBar { Minimum = 0, Maximum = 10, Value = 3 });
        Assert.Equal(AutomationErrorCode.InvalidArgument, f.Act(track, AccessibleActions.SetValue, AutomationActionValue.FromNumber(number)).Error);
        Assert.Equal(3, track.Value);
    }

    [Theory]
    [InlineData(AccessibleActions.None, AutomationErrorCode.InvalidArgument)]
    [InlineData(AccessibleActions.Invoke | AccessibleActions.Focus, AutomationErrorCode.InvalidArgument)]
    [InlineData(AccessibleActions.Scroll, AutomationErrorCode.ActionUnsupported)]
    [InlineData(AccessibleActions.Toggle, AutomationErrorCode.ActionUnsupported)]
    public void InvalidOrUnsupportedActionsAreExplicit(AccessibleActions action, AutomationErrorCode expected)
    {
        using var f = new AutomationFixture();
        Assert.Equal(expected, f.Act(f.Add(new Button()), action).Error);
    }

    [Fact]
    public void WrongValueTypesAndUnexpectedPayloadAreRejected()
    {
        using var f = new AutomationFixture();
        Assert.Equal(AutomationErrorCode.InvalidArgument, f.Act(f.Add(new Button()), AccessibleActions.Invoke, AutomationActionValue.FromText("x")).Error);
        Assert.Equal(AutomationErrorCode.InvalidArgument, f.Act(f.Add(new TextBox()), AccessibleActions.SetValue, AutomationActionValue.FromNumber(1)).Error);
        Assert.Equal(AutomationErrorCode.InvalidArgument, f.Act(f.Add(new TrackBar()), AccessibleActions.SetValue, AutomationActionValue.FromText("1")).Error);
    }

    [Fact]
    public void AdvertisedScrollIntoViewUsesCustomCanonicalAction()
    {
        using var f = new AutomationFixture();
        var c = f.Add(new SemanticControl()); c.Child.Actions = AccessibleActions.ScrollIntoView;
        AccessibleActions observed = AccessibleActions.None;
        c.Child.Action = (a, value) => { Assert.Null(value); observed = a; return true; };
        Assert.Equal(AutomationActionStatus.Accepted, f.Session.PerformActionAsync(f.Root.RootId, f.Handle(c.Child), AccessibleActions.ScrollIntoView).Completed().Status);
        Assert.Equal(AccessibleActions.ScrollIntoView, observed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalFalseAndExceptionsAreSafelyMapped(bool throws)
    {
        using var f = new AutomationFixture();
        var c = f.Add(new SemanticControl());
        c.Child.Action = (_, _) => throws ? throw new Exception("private action failure") : false;
        var result = f.Session.PerformActionAsync(f.Root.RootId, f.Handle(c.Child), AccessibleActions.Invoke).Completed();
        Assert.Equal(throws ? AutomationErrorCode.ApplicationError : AutomationErrorCode.ActionRejected, result.Error);
        Assert.False(f.Session.IsStopped);
    }

    [Fact]
    public void GetterDetachBetweenResolveAndActionIsRejected()
    {
        using var f = new AutomationFixture();
        var c = f.Add(new SemanticControl()); int actions = 0;
        c.Child.ActionsGetter = () => { f.Form.Controls.Remove(c); return AccessibleActions.Invoke; };
        c.Child.Action = (_, _) => { actions++; return true; };
        Assert.Equal(AutomationErrorCode.NodeUnavailable,
            f.Session.PerformActionAsync(f.Root.RootId, f.Handle(c.Child), AccessibleActions.Invoke).Completed().Error);
        Assert.Equal(0, actions);
    }

    [Fact]
    public void CurrentReadOnlyAndChangedRangeOverrideEarlierSnapshot()
    {
        using var f = new AutomationFixture();
        var text = f.Add(new TextBox()); _ = f.Inspect(text); text.ReadOnly = true;
        Assert.NotEqual(AutomationActionStatus.Accepted, f.Act(text, AccessibleActions.SetValue, AutomationActionValue.FromText("x")).Status);
        var track = f.Add(new TrackBar { Maximum = 10 }); _ = f.Inspect(track); track.Maximum = 5;
        Assert.Equal(AutomationErrorCode.InvalidArgument, f.Act(track, AccessibleActions.SetValue, AutomationActionValue.FromNumber(8)).Error);
    }
}
