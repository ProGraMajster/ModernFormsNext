namespace ModernFormsNext;

/// <summary>Identifies an opt-in command route diagnostic transition.</summary>
public enum CommandRoutingDiagnosticKind
{
    /// <summary>A captured control, window or application scope is being visited.</summary>
    NodeVisited,
    /// <summary>A registration matched the command by reference.</summary>
    BindingFound,
    /// <summary>A binding's availability handler returned.</summary>
    CanExecuteEvaluated,
    /// <summary>An execution handler returned successfully.</summary>
    Executed,
    /// <summary>A query or execution handler stopped traversal.</summary>
    Handled,
    /// <summary>Target validation, availability, lifetime or a handler prevented completion.</summary>
    Failed
}

/// <summary>Provides one route transition without logging user text or parameter values.</summary>
/// <remarks>
/// No route snapshot, parameter value, control text or exception message is included. Owners and
/// bindings are identities, not formatted strings. Consumers should not retain these references
/// beyond their required lifetime. Events are synchronous, opt-in and raised on the routing UI thread.
/// </remarks>
public sealed class CommandRoutingDiagnosticEventArgs : EventArgs
{
    internal CommandRoutingDiagnosticEventArgs(CommandRoutingDiagnosticKind kind, RoutedCommand command,
        Control? target, object? owner, CommandBinding? binding, Type? parameterType, bool? canExecute, string? failureReason)
        => (Kind, Command, Target, Owner, Binding, ParameterType, CanExecute, FailureReason) =
            (kind, command, target, owner, binding, parameterType, canExecute, failureReason);

    /// <summary>Gets the transition kind.</summary>
    public CommandRoutingDiagnosticKind Kind { get; }
    /// <summary>Gets the routed command identity.</summary>
    public RoutedCommand Command { get; }
    /// <summary>Gets the captured target, or null if none was supplied.</summary>
    public Control? Target { get; }
    /// <summary>Gets the visited Control, WindowBase, typeof(Application) terminal, or null before traversal.</summary>
    public object? Owner { get; }
    /// <summary>Gets the matched registration, or null when no binding has been visited.</summary>
    public CommandBinding? Binding { get; }
    /// <summary>Gets only the CLR parameter type; null means a null parameter.</summary>
    public Type? ParameterType { get; }
    /// <summary>Gets the query result when applicable.</summary>
    public bool? CanExecute { get; }
    /// <summary>Gets a framework-defined failure reason, never user text or an exception message.</summary>
    public string? FailureReason { get; }
}
