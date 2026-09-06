using System.Windows.Input;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class CommandSourceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EventOnlySourceRetainsClick(bool tray)
    {
        using var source = new Source(tray);
        int clicks = 0;
        source.OnClick(() => clicks++);
        source.Invoke();
        Assert.Equal(1, clicks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClickPrecedesCommandAndExecutionUsesCurrentParameter(bool tray)
    {
        using var source = new Source(tray);
        var calls = new List<string>();
        source.Parameter = "before";
        source.Command = new DelegateCommand(p => calls.Add($"execute:{p}"));
        source.OnClick(() => { calls.Add("click"); source.Parameter = "after"; });
        source.Invoke();
        Assert.Equal(["click", "execute:after"], calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DelegateActivationEvaluatesOnceBeforeAndOnceAfterClick(bool tray)
    {
        using var source = new Source(tray);
        var calls = new List<string>();
        source.Command = new DelegateCommand(() => calls.Add("execute"), () => { calls.Add("query"); return true; });
        source.OnClick(() => calls.Add("click"));
        calls.Clear(); // Assignment has its own availability evaluation.
        source.Invoke();
        Assert.Equal(["query", "click", "query", "execute"], calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PostClickPredicateReplacementRejectsObsoleteExecution(bool tray)
    {
        using var source = new Source(tray);
        bool clicked = false;
        int executions = 0;
        var replacement = new DelegateCommand(() => executions++);
        source.Command = new DelegateCommand(() => executions++, () =>
        {
            if (clicked) source.Command = replacement;
            return true;
        });
        source.OnClick(() => clicked = true);
        source.Invoke();
        Assert.Same(replacement, source.Command);
        Assert.True(source.Enabled);
        Assert.Equal(0, executions);
        source.Invoke();
        Assert.Equal(1, executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PostClickPredicateExceptionFailsClosedAndCanRecover(bool tray)
    {
        using var source = new Source(tray);
        var failure = new InvalidOperationException("post-click predicate failed");
        bool fail = false;
        bool failOnClick = true;
        int executions = 0;
        var command = new DelegateCommand(() => executions++, () => fail ? throw failure : true);
        source.Command = command;
        source.OnClick(() => fail = failOnClick);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(source.Invoke));
        Assert.False(source.Enabled);
        Assert.Equal(0, executions);
        fail = failOnClick = false;
        command.RaiseCanExecuteChanged();
        Assert.True(source.Enabled);
        source.Invoke();
        Assert.Equal(1, executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AvailabilityTransitionsAndParameterChangesReevaluate(bool tray)
    {
        using var source = new Source(tray);
        var command = new ProbeCommand { Predicate = p => p is string text && text == "valid" };
        source.Command = command;
        Assert.False(source.Enabled);
        source.Parameter = "valid";
        Assert.True(source.Enabled);
        source.Invoke();
        Assert.Equal(new object?[] { "valid" }, command.Executions);
        source.Parameter = null;
        Assert.False(source.Enabled);
        source.Invoke();
        Assert.Single(command.Executions);
        command.Predicate = _ => true;
        command.Raise();
        Assert.True(source.Enabled);
        command.Predicate = _ => false;
        command.Raise();
        Assert.False(source.Enabled);
        source.Command = null;
        Assert.True(source.Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacementRemovalAndStaleInvocationListsAreIsolated(bool tray)
    {
        using var source = new Source(tray);
        var first = new ProbeCommand();
        var second = new ProbeCommand();
        source.Command = first;
        EventHandler stale = first.Snapshot!;
        source.Command = second;
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(1, second.Subscribers);
        int queries = second.Queries;
        first.Predicate = _ => throw new InvalidOperationException("old command must be detached");
        first.Raise();
        stale(null, EventArgs.Empty);
        Assert.Equal(queries, second.Queries);
        source.Invoke();
        Assert.Empty(first.Executions);
        Assert.Single(second.Executions);
        source.Command = null;
        Assert.Equal(0, second.Subscribers);
        source.Invoke();
        Assert.Single(second.Executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalDisabledIntentSurvivesRequeryReplacementAndRemoval(bool tray)
    {
        using var source = new Source(tray);
        source.Enabled = false;
        var command = new ProbeCommand();
        source.Command = command;
        command.Raise();
        Assert.False(source.Enabled);
        source.Command = new ProbeCommand();
        Assert.False(source.Enabled);
        source.Command = null;
        Assert.False(source.Enabled);
        source.Command = command;
        source.Enabled = true;
        Assert.True(source.Enabled);
        source.Enabled = false;
        command.Raise();
        source.Invoke();
        Assert.False(source.Enabled);
        Assert.Empty(command.Executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalTrueCannotOverrideUnavailableCommand(bool tray)
    {
        using var source = new Source(tray);
        source.Command = new ProbeCommand { Predicate = _ => false };
        source.Enabled = false;
        source.Enabled = true;
        Assert.False(source.Enabled);
        source.Command = null;
        Assert.True(source.Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedCommandHasIndependentParametersAndDisposal(bool tray)
    {
        using var first = new Source(tray);
        using var second = new Source(tray);
        var command = new ProbeCommand { Predicate = p => p is not null };
        first.Parameter = "first";
        first.Command = command;
        second.Command = command;
        Assert.True(first.Enabled);
        Assert.False(second.Enabled);
        Assert.Equal(2, command.Subscribers);
        command.Predicate = _ => false;
        command.Raise();
        Assert.False(first.Enabled);
        Assert.False(second.Enabled);
        command.Predicate = _ => true;
        command.Raise();
        Assert.True(first.Enabled);
        Assert.True(second.Enabled);
        first.Dispose();
        Assert.Equal(1, command.Subscribers);
        Assert.Null(first.Command);
        Assert.Null(first.Parameter);
        second.Invoke();
        Assert.Equal(new object?[] { null }, command.Executions);
        second.Dispose();
        Assert.Equal(0, command.Subscribers);
        command.Raise();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedAttachDetachNeverDuplicatesSubscription(bool tray)
    {
        using var source = new Source(tray);
        var command = new ProbeCommand();
        for (int i = 0; i < 5; i++)
        {
            source.Command = command;
            source.Command = command;
            Assert.Equal(1, command.Subscribers);
            int before = command.Queries;
            command.Raise();
            Assert.Equal(before + 1, command.Queries);
            source.Command = null;
            Assert.Equal(0, command.Subscribers);
        }
        source.Command = command;
        EventHandler stale = command.Snapshot!;
        source.Dispose();
        source.Dispose();
        int queries = command.Queries;
        stale(command, EventArgs.Empty);
        Assert.Equal(queries, command.Queries);
        Assert.Equal(0, command.Subscribers);
        Assert.Throws<ObjectDisposedException>(() => source.Command = command);
        Assert.Throws<ObjectDisposedException>(() => source.Parameter = new object());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaleAvailabilityIsRecheckedBeforeClick(bool tray)
    {
        using var source = new Source(tray);
        int clicks = 0;
        var command = new ProbeCommand();
        source.Command = command;
        source.OnClick(() => clicks++);
        command.Predicate = _ => false; // Deliberately omit the notification.
        source.Invoke();
        Assert.Equal(0, clicks);
        Assert.Empty(command.Executions);
        Assert.False(source.Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClickCanReplaceOrRemoveCommand(bool tray)
    {
        using var source = new Source(tray);
        var first = new ProbeCommand();
        var second = new ProbeCommand();
        source.Command = first;
        source.OnClick(() => source.Command = ReferenceEquals(source.Command, first) ? second : null);
        source.Invoke();
        source.Invoke();
        Assert.Empty(first.Executions);
        Assert.Single(second.Executions);
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(0, second.Subscribers);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ClickCanDisableOrDisposeSource(bool tray, bool dispose)
    {
        using var source = new Source(tray);
        var command = new ProbeCommand();
        source.Command = command;
        source.OnClick(() => { if (dispose) source.Dispose(); else source.Enabled = false; });
        source.Invoke();
        Assert.Empty(command.Executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClickCanChangeAvailabilityWithoutNotification(bool tray)
    {
        using var source = new Source(tray);
        var command = new ProbeCommand();
        source.Command = command;
        source.OnClick(() => command.Predicate = _ => false);
        source.Invoke();
        Assert.Empty(command.Executions);
        Assert.False(source.Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecuteExceptionIsOriginalAndLaterActivationWorks(bool tray)
    {
        using var source = new Source(tray);
        var failure = new InvalidOperationException("execute failed");
        var command = new ProbeCommand { ExecuteFailure = failure };
        source.Command = command;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(source.Invoke));
        command.ExecuteFailure = null;
        source.Invoke();
        Assert.Single(command.Executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClickExceptionPreventsExecution(bool tray)
    {
        using var source = new Source(tray);
        var failure = new InvalidOperationException("click failed");
        var command = new ProbeCommand();
        source.Command = command;
        source.OnClick(() => throw failure);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(source.Invoke));
        Assert.Empty(command.Executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PredicateFailureFailsClosedAndRecoversByNotificationOrRemoval(bool tray)
    {
        using var source = new Source(tray);
        var failure = new InvalidOperationException("predicate failed");
        var command = new ProbeCommand { Predicate = _ => throw failure };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => source.Command = command));
        Assert.Same(command, source.Command);
        Assert.Equal(1, command.Subscribers);
        int queries = command.Queries;
        Assert.False(source.Enabled);
        Assert.False(source.Enabled);
        Assert.Equal(queries, command.Queries);
        command.Predicate = _ => true;
        command.Raise();
        Assert.True(source.Enabled);
        command.Predicate = _ => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => source.Parameter = "new"));
        Assert.Equal("new", source.Parameter);
        Assert.False(source.Enabled);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(command.Raise));
        source.Command = null;
        Assert.True(source.Enabled);
        Assert.Equal(0, command.Subscribers);
    }

    [Fact]
    public void ParentAndLocalIntentCombineWithCommandWithoutFalseNotifications()
    {
        using var parent = new Panel { Enabled = false };
        using var button = parent.Controls.Add(new Button());
        var command = new ProbeCommand();
        button.Command = command;
        int changes = 0;
        button.EnabledChanged += (_, _) => changes++;
        button.Enabled = true;
        command.Raise();
        Assert.False(button.Enabled);
        Assert.Equal(0, changes);
        parent.Enabled = true;
        Assert.True(button.Enabled);
        Assert.Equal(1, changes);
        command.Predicate = _ => false;
        command.Raise();
        Assert.False(button.Enabled);
        Assert.Equal(2, changes);
        button.Enabled = true;
        Assert.Equal(2, changes);
        button.Command = null;
        Assert.True(button.Enabled);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void PredicateReentrancyCannotOverwriteReplacementState()
    {
        using var button = new Button();
        var replacement = new ProbeCommand { Predicate = _ => false };
        var original = new ProbeCommand { Predicate = _ => { button.Command = replacement; return true; } };
        button.Command = original;
        Assert.Same(replacement, button.Command);
        Assert.False(button.Enabled);
        Assert.Equal(0, original.Subscribers);
        Assert.Equal(1, replacement.Subscribers);
    }

    [Fact]
    public void CommandDisabledDescendantIgnoresParentAvailabilityNotifications()
    {
        using var parent = new Panel();
        using var button = parent.Controls.Add(new Button { Command = new DelegateCommand(() => { }, () => false) });
        int changes = 0;
        button.EnabledChanged += (_, _) => changes++;
        parent.Enabled = false;
        parent.Enabled = true;
        Assert.False(button.Enabled);
        Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData(Keys.Enter)]
    [InlineData(Keys.Space)]
    public void ExistingButtonActivationKeysUseCommandPath(Keys key)
    {
        using var button = new ActionButton();
        var command = new ProbeCommand();
        button.Command = command;
        button.ReleaseKey(key);
        Assert.Single(command.Executions);
    }

    [Fact]
    public void RightClickPreservesEventWithoutExecutingCommand()
    {
        using var button = new ActionButton();
        var command = new ProbeCommand();
        button.Command = command;
        int clicks = 0;
        button.Click += (_, _) => clicks++;
        button.RightClick();
        Assert.Equal(1, clicks);
        Assert.Empty(command.Executions);
    }

    [Fact]
    public void DisposedOrDisabledButtonDoesNotInvokeEvents()
    {
        using var button = new Button { Enabled = false };
        int clicks = 0;
        button.Click += (_, _) => clicks++;
        button.PerformClick();
        button.Enabled = true;
        button.Dispose();
        button.PerformClick();
        Assert.Equal(0, clicks);
    }

    private sealed class ActionButton : Button
    {
        internal void ReleaseKey(Keys key) => OnKeyUp(new KeyEventArgs(key));
        internal void RightClick() => OnClick(new MouseEventArgs(MouseButtons.Right, 1, 0, 0, System.Drawing.Point.Empty));
    }

    private sealed class ProbeCommand : ICommand
    {
        private EventHandler? handlers;
        internal Predicate<object?> Predicate { get; set; } = _ => true;
        internal int Queries { get; private set; }
        internal int Subscribers => handlers?.GetInvocationList().Length ?? 0;
        internal EventHandler? Snapshot => handlers;
        internal List<object?> Executions { get; } = [];
        internal Exception? ExecuteFailure { get; set; }
        public event EventHandler? CanExecuteChanged { add => handlers += value; remove => handlers -= value; }
        public bool CanExecute(object? parameter) { Queries++; return Predicate(parameter); }
        public void Execute(object? parameter)
        {
            if (ExecuteFailure is not null) throw ExecuteFailure;
            Executions.Add(parameter);
        }
        internal void Raise() => handlers?.Invoke(null, EventArgs.Empty);
    }

    // Exercise the same consumer contract through both public action APIs, without internal hooks.
    private sealed class Source(bool tray) : IDisposable
    {
        private readonly Button? button = tray ? null : new Button();
        private readonly NotifyIconMenuItem? item = tray ? new NotifyIconMenuItem() : null;
        internal ICommand? Command { get => button is not null ? button.Command : item!.Command; set { if (button is not null) button.Command = value; else item!.Command = value; } }
        internal object? Parameter { get => button is not null ? button.CommandParameter : item!.CommandParameter; set { if (button is not null) button.CommandParameter = value; else item!.CommandParameter = value; } }
        internal bool Enabled { get => button is not null ? button.Enabled : item!.Enabled; set { if (button is not null) button.Enabled = value; else item!.Enabled = value; } }
        internal void OnClick(Action action) { if (button is not null) button.Click += (_, _) => action(); else item!.Click += (_, _) => action(); }
        internal void Invoke() { if (button is not null) button.PerformClick(); else item!.PerformClick(); }
        public void Dispose() { button?.Dispose(); item?.Dispose(); }
    }
}
