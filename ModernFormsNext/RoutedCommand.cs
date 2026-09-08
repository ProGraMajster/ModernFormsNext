using System.Windows.Input;
using ModernFormsNext.DataBinding;

namespace ModernFormsNext;

/// <summary>Identifies an action whose handlers are resolved through the current control hierarchy.</summary>
/// <remarks>
/// Commands compare by reference, not Name. Create and route on the creating UI thread. The target
/// overloads are canonical; parameter-only ICommand calls have no source/window context and are
/// unavailable. Button and InputBinding supply context automatically. There is no global focus,
/// command registry, automatic polling, preview/tunnel phase or asynchronous execution state.
/// Each invocation snapshots its route; handler exceptions propagate unchanged.
/// </remarks>
/// <example><code>
/// var save = new RoutedCommand("Save");
/// form.CommandBindings.Add(new CommandBinding(save,
///     (_, e) =&gt; { SaveDocument(e.Parameter); e.Handled = true; },
///     (_, e) =&gt; e.CanExecute = documentIsOpen));
/// var button = form.Controls.Add(new Button { Text = "Save", Command = save });
/// </code></example>
public sealed class RoutedCommand : ICommand
{
    private readonly int threadId = Environment.CurrentManagedThreadId;

    /// <summary>Creates an identity without capturing a target or registering global state.</summary>
    /// <param name="name">An optional immutable name intended for application diagnostics.</param>
    public RoutedCommand(string? name = null) => Name = name;

    /// <summary>Gets the optional debug name; equal names do not make commands equal.</summary>
    public string? Name { get; }

    /// <summary>Occurs when sources should reevaluate their own current target and parameter.</summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>Occurs for opt-in route transitions and failures.</summary>
    /// <remarks>
    /// No diagnostic args are allocated without observers. Observers must be fast and must not
    /// mutate UI state. Observer exceptions normally propagate; an original command handler
    /// exception takes precedence over an exception while reporting that failure.
    /// </remarks>
    public event EventHandler<CommandRoutingDiagnosticEventArgs>? Diagnostic;

    /// <summary>Returns false because a parameter-only call supplies no routing context.</summary>
    /// <param name="parameter">The optional application parameter.</param>
    /// <returns>False. Use the target overload or a framework command source.</returns>
    public bool CanExecute(object? parameter) => CanExecute(parameter, null);

    /// <summary>Does nothing because a parameter-only call supplies no routing context.</summary>
    /// <param name="parameter">The optional application parameter.</param>
    public void Execute(object? parameter) => Execute(parameter, null);

    /// <summary>Queries the first available registration on a captured target-to-root route.</summary>
    /// <param name="parameter">The nullable parameter passed unchanged to handlers.</param>
    /// <param name="target">An attached control; null, detached, disposed or closed targets are unavailable.</param>
    /// <returns>True when a binding allows execution before any explicit veto.</returns>
    /// <remarks>Call on the creating UI thread. Queries do not execute actions or change focus.</remarks>
    public bool CanExecute(object? parameter, Control? target)
        => CommandRouting.Prepare(this, parameter, target) is not null;

    /// <summary>Queries and executes available handlers using one stable route snapshot.</summary>
    /// <param name="parameter">The nullable parameter passed unchanged to handlers.</param>
    /// <param name="target">An attached control; unavailable targets make execution a no-op.</param>
    /// <remarks>
    /// Call on the creating UI thread. Executed.Handled stops further handlers. Reparenting,
    /// focus and registration changes affect subsequent invocations; disposal/close stops this one.
    /// No task is scheduled and original application exceptions propagate.
    /// </remarks>
    public void Execute(object? parameter, Control? target)
        => CommandRouting.Prepare(this, parameter, target)?.Execute();

    /// <summary>Notifies sources after application state affecting CanExecute changes.</summary>
    /// <remarks>
    /// Uses normal synchronous event semantics, including exception propagation. Like DelegateCommand,
    /// background notifications are marshalled by framework sources through the existing dispatcher.
    /// This method itself does not synchronize application state or access the control tree.
    /// </remarks>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    internal void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != threadId)
            throw new InvalidOperationException("Routed commands must be accessed on their creating UI thread.");
    }

    internal void Report(CommandRoutingDiagnosticKind kind, Control? target, object? parameter,
        object? owner = null, CommandBinding? binding = null, bool? canExecute = null, string? failureReason = null)
    {
        var handler = Diagnostic;
        if (handler is not null)
            handler(this, new(kind, this, target, owner, binding, parameter?.GetType(), canExecute, failureReason));
    }
}
