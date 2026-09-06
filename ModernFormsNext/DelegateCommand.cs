using System.Windows.Input;

namespace ModernFormsNext;

/// <summary>
/// Represents a reusable application action implemented by delegates.
/// </summary>
/// <remarks>
/// This is a synchronous <see cref="ICommand"/> implementation. Delegates and event handlers run
/// on the calling thread; their original exceptions propagate to the caller. Call
/// <see cref="RaiseCanExecuteChanged"/> after availability changes. There is no automatic polling.
/// A command can be shared by several sources with different parameters. The command owns its
/// delegate references, but does not own or dispose the objects captured by those delegates.
/// Do not pass async lambdas to the action constructors; task-aware helpers are deferred.
/// </remarks>
/// <example>
/// <code>
/// bool canSave = true;
/// var save = new DelegateCommand(p => Console.WriteLine(p), p => canSave);
/// var button = new Button { Text = "Save", Command = save, CommandParameter = "Document" };
/// canSave = false;
/// save.RaiseCanExecuteChanged();
/// </code>
/// </example>
public sealed class DelegateCommand : ICommand
{
    private readonly Action<object?> execute;
    private readonly Predicate<object?>? canExecute;

    /// <summary>
    /// Initializes a command whose delegates do not require a parameter.
    /// </summary>
    /// <param name="execute">The action to perform when execution is allowed.</param>
    /// <param name="canExecute">The availability predicate, or null to always allow execution.</param>
    /// <exception cref="ArgumentNullException"><paramref name="execute"/> is null.</exception>
    public DelegateCommand(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this.execute = _ => execute();
        this.canExecute = canExecute is null ? null : _ => canExecute();
    }

    /// <summary>
    /// Initializes a command whose delegates receive the source's current parameter.
    /// </summary>
    /// <param name="execute">The action to perform; its parameter may be null.</param>
    /// <param name="canExecute">The parameter-aware predicate, or null to always allow execution.</param>
    /// <exception cref="ArgumentNullException"><paramref name="execute"/> is null.</exception>
    public DelegateCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this.execute = execute;
        this.canExecute = canExecute;
    }

    /// <summary>
    /// Occurs when sources should reevaluate availability with their own current parameters.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Evaluates availability without executing the action.
    /// </summary>
    /// <param name="parameter">The source parameter, which may be null.</param>
    /// <returns>The predicate result, or true when no predicate was supplied.</returns>
    /// <remarks>
    /// The predicate should be fast and free of side effects. Sources may call it more than once
    /// during activation. Predicate exceptions propagate unchanged.
    /// </remarks>
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// Executes the action if <see cref="CanExecute"/> currently returns true.
    /// </summary>
    /// <param name="parameter">The source parameter, which may be null.</param>
    /// <remarks>
    /// A false predicate makes this call a no-op. Predicate and action exceptions propagate
    /// unchanged. This method does not schedule work or raise <see cref="CanExecuteChanged"/>.
    /// </remarks>
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            execute(parameter);
    }

    /// <summary>
    /// Notifies subscribers that they should reevaluate <see cref="CanExecute"/>.
    /// </summary>
    /// <remarks>
    /// Uses normal synchronous .NET event semantics: a throwing subscriber stops notification
    /// and its exception propagates. Framework command sources marshal notifications received
    /// on a background thread through the existing UI dispatcher; a running UI loop is required
    /// to process those notifications. This method itself does not synchronize application state.
    /// </remarks>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
