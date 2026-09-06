using System.Windows.Input;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class CommandTests
{
    [Fact]
    public void ParameterlessActionUsesICommandContract()
    {
        int calls = 0;
        ICommand command = new DelegateCommand(() => calls++);
        Assert.True(command.CanExecute(null));
        command.Execute(new object());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("document")]
    [InlineData(42)]
    public void ParameterIsPassedWithoutConversion(object? parameter)
    {
        object? received = new object();
        var command = new DelegateCommand(p => received = p);
        command.Execute(parameter);
        Assert.Same(parameter, received);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParameterlessPredicateGuardsDirectExecution(bool allowed)
    {
        int calls = 0;
        var command = new DelegateCommand(() => calls++, () => allowed);
        Assert.Equal(allowed, command.CanExecute(null));
        command.Execute(null);
        Assert.Equal(allowed ? 1 : 0, calls);
    }

    [Fact]
    public void ParameterPredicateControlsExecution()
    {
        var calls = new List<object?>();
        var command = new DelegateCommand(calls.Add, p => p is int value && value > 0);
        command.Execute(null);
        command.Execute("invalid");
        command.Execute(-1);
        command.Execute(2);
        Assert.Equal(new object?[] { 2 }, calls);
    }

    [Fact]
    public void DirectExecutionEvaluatesPredicateOnce()
    {
        int queries = 0;
        int executions = 0;
        var command = new DelegateCommand(() => executions++, () => { queries++; return true; });
        command.Execute(null);
        Assert.Equal(1, queries);
        Assert.Equal(1, executions);
    }

    [Fact]
    public void ExplicitRequeryUsesNormalEventSemantics()
    {
        var command = new DelegateCommand(() => { });
        int calls = 0;
        EventHandler handler = (sender, args) =>
        {
            Assert.Same(command, sender);
            Assert.Same(EventArgs.Empty, args);
            calls++;
        };
        command.CanExecuteChanged += handler;
        command.RaiseCanExecuteChanged();
        command.CanExecuteChanged -= handler;
        command.RaiseCanExecuteChanged();
        Assert.Equal(1, calls);
    }

    [Fact]
    public void NullActionsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new DelegateCommand((Action)null!));
        Assert.Throws<ArgumentNullException>(() => new DelegateCommand((Action<object?>)null!));
    }

    [Fact]
    public void OriginalExecuteExceptionPropagates()
    {
        var failure = new InvalidOperationException("action failed");
        var command = new DelegateCommand(() => throw failure);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => command.Execute(null)));
    }

    [Fact]
    public void OriginalPredicateExceptionPropagatesAndDoesNotPoisonCommand()
    {
        var failure = new InvalidOperationException("predicate failed");
        bool fail = true;
        int calls = 0;
        var command = new DelegateCommand(() => calls++, () => fail ? throw failure : true);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => command.CanExecute(null)));
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => command.Execute(null)));
        fail = false;
        command.Execute(null);
        Assert.Equal(1, calls);
    }
}
