namespace ModernFormsNext;

/// <summary>Describes one binding's availability query during command routing.</summary>
/// <remarks>
/// Initial CanExecute and Handled are false. True selects this binding. False with Handled false
/// continues searching; false with Handled true vetoes the remaining route. The execution phase
/// has its own Handled value. Handlers run synchronously on the UI thread and should be fast.
/// </remarks>
public sealed class CanExecuteCommandEventArgs : EventArgs
{
    internal CanExecuteCommandEventArgs(RoutedCommand command, object? parameter, Control target, Control? source)
        => (Command, Parameter, Target, Source) = (command, parameter, target, source);

    /// <summary>Gets the command being queried.</summary>
    public RoutedCommand Command { get; }
    /// <summary>Gets the caller's parameter unchanged; it can contain sensitive data and may be null.</summary>
    public object? Parameter { get; }
    /// <summary>Gets the target captured when this invocation began.</summary>
    public Control Target { get; }
    /// <summary>Gets the originating control, or null for direct target calls and non-control scopes.</summary>
    public Control? Source { get; }
    /// <summary>Gets or sets whether this binding may execute. Defaults to false.</summary>
    public bool CanExecute { get; set; }
    /// <summary>Gets or sets whether this query stops searching, including when CanExecute is false.</summary>
    public bool Handled { get; set; }
}
