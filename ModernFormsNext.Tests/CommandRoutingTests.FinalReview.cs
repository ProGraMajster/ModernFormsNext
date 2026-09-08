using Xunit;

namespace ModernFormsNext.Tests;

public sealed partial class CommandRoutingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaleDetachedOrDisposedFocusUsesWindowRootForRoutedKeyboard(bool dispose)
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        int calls = 0;
        ui.Form.CommandBindings.Add(Bind(command, (_, e) => { Assert.Same(ui.Form.adapter, e.Target); calls++; }));
        ui.Form.InputBindings.Add(new KeyBinding(command, SaveGesture));
        if (dispose) ui.Focus.Dispose(); else ui.Focus.Parent = null;
        // Exercise stale backend focus bookkeeping explicitly, including disposed controls that
        // still retain their Parent reference. Gesture precedence itself must remain unchanged.
        ui.Form.adapter.SelectedControl = ui.Focus;

        Assert.True(ui.Key().Handled);
        Assert.Equal(1, calls);
        if (!dispose) ui.Focus.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposingPreviouslyVisitedOwnerStopsRouteEvenAfterTargetWasReparented(bool duringQuery)
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        int windowExecutions = 0;
        ui.Parent.CommandBindings.Add(Bind(command, (_, _) => ui.Form.Controls.Add(ui.Focus), handled: false));
        ui.Form.CommandBindings.Add(new(command, (_, _) => {
            windowExecutions++;
            if (!duringQuery) ui.Parent.Dispose();
        }, (_, e) => {
            if (duringQuery) ui.Parent.Dispose();
            e.CanExecute = true;
        }));
        Application.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail("Disposed route must not reach application fallback.")));

        command.Execute(null, ui.Focus);

        Assert.False(ui.Focus.IsDisposed);
        Assert.True(ui.Parent.IsDisposed);
        Assert.Equal(duringQuery ? 0 : 1, windowExecutions);
    }

    [Fact]
    public void DisposingUnvisitedOwnerDuringQueryAbortsBeforeSelectedExecution()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        ui.Focus.CommandBindings.Add(new(command, (_, _) => Assert.Fail(), (_, e) => {
            ui.Form.Controls.Add(ui.Focus);
            ui.Parent.Dispose();
            e.CanExecute = true;
        }));

        Assert.False(command.CanExecute(null, ui.Focus));
        Assert.False(ui.Focus.IsDisposed);
    }

    [Fact]
    public void UnhandledExecutionVisitsAllOwnersOnceAndSkipsUnavailableLaterBinding()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var calls = new List<string>();
        var owners = new[] { "target", "parent", "window", "application" };
        var scopes = Scopes(ui);
        for (int i = 0; i < scopes.Length; i++)
        {
            string owner = owners[i];
            scopes[i].Add(new(command, (_, _) => calls.Add(owner + " execute"),
                (_, e) => { calls.Add(owner + " query"); e.CanExecute = true; }));
            scopes[i].Add(new(command, (_, _) => Assert.Fail(), (_, _) => calls.Add(owner + " unavailable")));
        }

        command.Execute(null, ui.Focus);

        Assert.Equal(owners.SelectMany(owner => new[] { owner + " query", owner + " execute", owner + " unavailable" }), calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedAvailabilityQueriesKeepParametersTargetsAndExecutionCursorsIndependent(bool sameCommand)
    {
        using var ui = new WindowFixture();
        var next = ui.Parent.Controls.Add(new Button());
        var outer = new RoutedCommand("outer");
        var inner = sameCommand ? outer : new RoutedCommand("inner");
        var calls = new List<string>();
        next.CommandBindings.Add(new(inner, (_, _) => Assert.Fail("A nested query must not execute."), (_, e) => {
            Assert.Equal("inner parameter", e.Parameter);
            Assert.Same(next, e.Target);
            calls.Add("inner query");
            e.CanExecute = true;
        }));
        ui.Focus.CommandBindings.Add(new(outer, (_, e) => {
            Assert.Equal("outer parameter", e.Parameter);
            Assert.Same(ui.Focus, e.Target);
            calls.Add("outer execute");
            e.Handled = true;
        }, (_, e) => {
            calls.Add("outer query");
            e.CanExecute = inner.CanExecute("inner parameter", next);
        }));

        outer.Execute("outer parameter", ui.Focus);

        Assert.Equal(["outer query", "inner query", "outer execute"], calls);
    }

    [Fact]
    public void NestedDiagnosticQueryDoesNotReplaceOuterRouteOrDiagnosticTarget()
    {
        using var ui = new WindowFixture();
        var next = ui.Parent.Controls.Add(new Button());
        var command = new RoutedCommand();
        int nestedQueries = 0, executions = 0;
        next.CommandBindings.Add(new(command, (_, _) => Assert.Fail(), (_, e) => { nestedQueries++; e.CanExecute = true; }));
        ui.Focus.CommandBindings.Add(Bind(command, (_, e) => { Assert.Same(ui.Focus, e.Target); executions++; }));
        bool observing = false;
        command.Diagnostic += (_, e) => {
            if (observing || e.Kind != CommandRoutingDiagnosticKind.BindingFound) return;
            observing = true;
            try { Assert.True(command.CanExecute(null, next)); }
            finally { observing = false; }
            Assert.Same(ui.Focus, e.Target);
        };

        command.Execute(null, ui.Focus);

        Assert.Equal(1, nestedQueries);
        Assert.Equal(1, executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InnerCommandFailureCanBeCaughtWithoutCorruptingOuterContinuation(bool queryFailure)
    {
        using var ui = new WindowFixture();
        var outer = new RoutedCommand();
        var inner = new RoutedCommand();
        var failure = new InvalidOperationException("private inner failure");
        bool fail = true;
        int continuations = 0, innerExecutions = 0;
        ui.Focus.CommandBindings.Add(new(inner, (_, e) => {
            if (fail && !queryFailure) throw failure;
            innerExecutions++;
            e.Handled = true;
        }, (_, e) => { if (fail && queryFailure) throw failure; e.CanExecute = true; }));
        ui.Focus.CommandBindings.Add(Bind(outer, (_, _) => {
            if (fail) Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => inner.Execute(null, ui.Focus)));
            else inner.Execute(null, ui.Focus);
        }, handled: false));
        ui.Parent.CommandBindings.Add(Bind(outer, (_, _) => continuations++));

        outer.Execute(null, ui.Focus);
        fail = false;
        outer.Execute(null, ui.Focus);

        Assert.Equal(2, continuations);
        Assert.Equal(1, innerExecutions);
    }

    [Fact]
    public void DiagnosticObserverFailurePropagatesAndNextInvocationRecovers()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        int calls = 0;
        var failure = new InvalidOperationException("observer");
        EventHandler<CommandRoutingDiagnosticEventArgs> observer = (_, _) => throw failure;
        ui.Focus.CommandBindings.Add(Bind(command, (_, _) => calls++));
        command.Diagnostic += observer;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => command.Execute(null, ui.Focus)));
        Assert.Equal(0, calls);
        command.Diagnostic -= observer;
        command.Execute(null, ui.Focus);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void RoutedKeyTargetMutationDuringQueryRejectsStaleExecutionAndUsesNewTargetOnNextPress()
    {
        using var ui = new WindowFixture();
        var next = ui.Parent.Controls.Add(new Button());
        var command = new RoutedCommand();
        var binding = new KeyBinding(command, SaveGesture) { CommandTarget = ui.Focus };
        int calls = 0;
        ui.Focus.CommandBindings.Add(new(command, (_, _) => Assert.Fail(), (_, e) => {
            binding.CommandTarget = next;
            e.CanExecute = true;
        }));
        next.CommandBindings.Add(Bind(command, (_, e) => { Assert.Same(next, e.Target); calls++; }));
        ui.Form.InputBindings.Add(binding);

        Assert.False(ui.Key().Handled);
        Assert.Equal(0, calls);
        Assert.True(ui.Key().Handled);
        Assert.Equal(1, calls);
    }
}
