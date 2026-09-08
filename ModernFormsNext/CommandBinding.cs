namespace ModernFormsNext;

/// <summary>Associates a routed command with synchronous handlers on one control, window or application scope.</summary>
/// <remarks>
/// Create and register on the UI thread. A binding has reference identity and at most one collection
/// owner. Its command and delegates are immutable, so an invocation can safely snapshot registrations.
/// Removing a binding affects the next invocation; owner disposal stops the current route. Removing
/// or disposing its owner does not dispose the command or objects captured by the delegates.
/// </remarks>
public sealed class CommandBinding
{
    /// <summary>Creates a command handler registration.</summary>
    /// <param name="command">The routed command identified by reference, never by name.</param>
    /// <param name="executed">The action handler. Set Handled to stop further execution.</param>
    /// <param name="canExecute">The availability handler; null makes this registration unavailable.</param>
    /// <exception cref="ArgumentNullException">The command or execution handler is null.</exception>
    public CommandBinding(RoutedCommand command, EventHandler<ExecutedCommandEventArgs> executed,
        EventHandler<CanExecuteCommandEventArgs>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(executed);
        command.VerifyAccess();
        Command = command;
        Executed = executed;
        CanExecute = canExecute;
    }

    /// <summary>Gets the routed command matched by reference identity.</summary>
    public RoutedCommand Command { get; }
    /// <summary>Gets the execution handler invoked with the route owner as sender.</summary>
    public EventHandler<ExecutedCommandEventArgs> Executed { get; }
    /// <summary>Gets the availability handler; null is unavailable, never implicitly true.</summary>
    public EventHandler<CanExecuteCommandEventArgs>? CanExecute { get; }

    internal CommandBindingCollection? Owner { get; set; }
}
