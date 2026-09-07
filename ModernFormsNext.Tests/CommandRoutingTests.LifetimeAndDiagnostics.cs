using System.ComponentModel;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed partial class CommandRoutingTests
{
    [Fact]
    public void BindingHasOneOwnerAndCanBeRemovedReplacedAndTransferred()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var first = Bind(command, (_, _) => { });
        var second = Bind(command, (_, _) => { });
        ui.Focus.CommandBindings.Add(first);
        Assert.Throws<ArgumentException>(() => ui.Focus.CommandBindings.Add(first));
        Assert.Throws<ArgumentException>(() => ui.Parent.CommandBindings.Add(first));
        ui.Focus.CommandBindings[0] = first;
        ui.Focus.CommandBindings[0] = second;
        ui.Parent.CommandBindings.Add(first);
        ui.Focus.CommandBindings.Clear();
        ui.Parent.CommandBindings.Add(second);
        Assert.Equal([first, second], ui.Parent.CommandBindings);
    }

    [Fact]
    public void DisposingAnOwnerReleasesBindingsWithoutDisposingTheirCommand()
    {
        using var ui = new WindowFixture();
        using var owner = new Panel();
        var command = new RoutedCommand();
        int calls = 0;
        var binding = Bind(command, (_, _) => calls++);
        var collection = owner.CommandBindings;
        collection.Add(binding);
        owner.Dispose();
        Assert.Empty(collection);
        Assert.Throws<ObjectDisposedException>(() => collection.Add(binding));
        ui.Parent.CommandBindings.Add(binding);
        command.Execute(null, ui.Focus);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DisposingCapturedOwnerDuringExecutionStopsLaterHandlers()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        ui.Focus.CommandBindings.Add(Bind(command, (_, _) => ui.Parent.Dispose(), handled: false));
        ui.Form.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        command.Execute(null, ui.Focus);
        Assert.True(ui.Focus.IsDisposed);
    }

    [Fact]
    public void FocusChangeDoesNotRetargetUnhandledExecution()
    {
        using var ui = new WindowFixture();
        var next = ui.Form.Controls.Add(new Button());
        var command = new RoutedCommand();
        int calls = 0;
        ui.Focus.CommandBindings.Add(Bind(command, (_, _) => next.Select(), handled: false));
        ui.Parent.CommandBindings.Add(Bind(command, (_, e) => { Assert.Same(ui.Focus, e.Target); calls++; }));
        next.CommandBindings.Add(Bind(command, (_, _) => Assert.Fail()));
        command.Execute(null, ui.Focus);
        Assert.Equal(1, calls);
        Assert.True(next.Focused);
    }

    [Fact]
    public void ApplicationCleanupReleasesOwnershipAndFreshCollectionHasNoInheritedHandlers()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var binding = Bind(command, (_, _) => { });
        var old = Application.CommandBindings;
        old.Add(binding);
        Assert.True(command.CanExecute(null, ui.Focus));
        Application.ReleaseCommandBindings();
        Assert.Empty(old);
        Assert.Throws<ObjectDisposedException>(() => old.Add(binding));
        Assert.NotSame(old, Application.CommandBindings);
        Assert.Empty(Application.CommandBindings);
        Assert.False(command.CanExecute(null, ui.Focus));
        ui.Form.CommandBindings.Add(binding);
        Assert.True(command.CanExecute(null, ui.Focus));
    }

    [Fact]
    public void CancelledClosePreservesBindingsWhileActualCloseReleasesEveryScope()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var local = ui.Focus.CommandBindings;
        var window = ui.Form.CommandBindings;
        local.Add(Bind(command, (_, _) => { }));
        window.Add(Bind(command, (_, _) => { }));
        EventHandler<CancelEventArgs> cancel = (_, e) => e.Cancel = true;
        ui.Form.Closing += cancel;
        ui.Form.Close();
        Assert.Single(local);
        Assert.Single(window);
        Assert.True(command.CanExecute(null, ui.Focus));
        ui.Form.Closing -= cancel;
        ui.Form.Close();
        Assert.Empty(local);
        Assert.Empty(window);
        Assert.Throws<ObjectDisposedException>(() => ui.Form.CommandBindings);
        Assert.Throws<ObjectDisposedException>(() => ui.Focus.CommandBindings);
        Assert.False(command.CanExecute(null, ui.Focus));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BackgroundRoutingAndCollectionMutationsAreRejected(int operation)
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var collection = ui.Focus.CommandBindings;
        var binding = Bind(command, (_, _) => Assert.Fail());
        Exception? failure = null;
        var thread = new Thread(() => {
            try
            {
                switch (operation)
                {
                    case 0: command.CanExecute(null, ui.Focus); break;
                    case 1: command.Execute(null, ui.Focus); break;
                    case 2: collection.Add(binding); break;
                    case 3: collection.Clear(); break;
                }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void DiagnosticsIdentifyVisitedNodesBindingQueryExecutionAndHandledOwner()
    {
        using var ui = new WindowFixture();
        var command = new RoutedCommand();
        var binding = Bind(command, (_, _) => { });
        Application.CommandBindings.Add(binding);
        var events = new List<CommandRoutingDiagnosticEventArgs>();
        command.Diagnostic += (_, e) => events.Add(e);
        command.Execute(42, ui.Focus);
        var expected = new List<object>();
        for (Control? node = ui.Focus; node is not ControlAdapter; node = node!.Parent) expected.Add(node!);
        expected.Add(ui.Form);
        expected.Add(typeof(Application));
        Assert.Equal(expected, events.Where(e => e.Kind == CommandRoutingDiagnosticKind.NodeVisited).Select(e => e.Owner));
        Assert.Contains(events, e => e.Kind == CommandRoutingDiagnosticKind.BindingFound && ReferenceEquals(binding, e.Binding));
        Assert.Contains(events, e => e.Kind == CommandRoutingDiagnosticKind.CanExecuteEvaluated && e.CanExecute == true);
        Assert.Contains(events, e => e.Kind == CommandRoutingDiagnosticKind.Executed && Equals(e.Owner, typeof(Application)));
        Assert.Equal(CommandRoutingDiagnosticKind.Handled, events[^1].Kind);
        Assert.Equal(typeof(Application), events[^1].Owner);
        Assert.All(events, e => { Assert.Same(command, e.Command); Assert.Same(ui.Focus, e.Target); Assert.Equal(typeof(int), e.ParameterType); });
    }

    [Fact]
    public void DiagnosticsNeverStringifyParameterOrCopyUserText()
    {
        using var ui = new WindowFixture();
        ui.Focus.Text = "private control text";
        var command = new RoutedCommand("Save");
        var parameter = new SensitiveParameter();
        var events = new List<CommandRoutingDiagnosticEventArgs>();
        command.Diagnostic += (_, e) => events.Add(e);
        ui.Focus.CommandBindings.Add(Bind(command, (_, e) => Assert.Same(parameter, e.Parameter)));
        command.Execute(parameter, ui.Focus);
        command.Execute(parameter, null);
        Assert.Equal(0, parameter.Stringifications);
        Assert.All(events, e => Assert.Equal(typeof(SensitiveParameter), e.ParameterType));
        Assert.DoesNotContain("private control text", string.Join(";", events.Select(e => e.FailureReason)));
        Assert.Null(typeof(CommandRoutingDiagnosticEventArgs).GetProperty("Parameter"));
        Assert.Null(typeof(CommandRoutingDiagnosticEventArgs).GetProperty("Route"));
        Assert.Contains(events, e => e.Kind == CommandRoutingDiagnosticKind.Failed && e.FailureReason == "No target context.");
    }

    [Fact]
    public void RuntimePropertiesAreHiddenFromDesignerSerialization()
    {
        using var ui = new WindowFixture();
        foreach (var pair in new[] { (typeof(Control), "CommandBindings"), (typeof(WindowBase), "CommandBindings"), (typeof(Button), "CommandTarget") })
        {
            var property = TypeDescriptor.GetProperties(pair.Item1)[pair.Item2]!;
            Assert.False(property.IsBrowsable);
            Assert.Equal(DesignerSerializationVisibility.Hidden,
                ((DesignerSerializationVisibilityAttribute)property.Attributes[typeof(DesignerSerializationVisibilityAttribute)]!).Visibility);
        }
    }

    private sealed class SensitiveParameter
    {
        internal int Stringifications;
        public override string ToString() { Stringifications++; throw new InvalidOperationException("Do not log me"); }
    }
}
