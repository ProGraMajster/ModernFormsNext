namespace ModernFormsNext;

/// <summary>Describes synchronous execution of an available command binding.</summary>
/// <remarks>Setting Handled stops the captured route. Otherwise later available bindings may execute.</remarks>
public sealed class ExecutedCommandEventArgs : EventArgs
{
    internal ExecutedCommandEventArgs(RoutedCommand command, object? parameter, Control target, Control? source)
        => (Command, Parameter, Target, Source) = (command, parameter, target, source);

    /// <summary>Gets the command being executed.</summary>
    public RoutedCommand Command { get; }
    /// <summary>Gets the caller's unchanged, possibly sensitive parameter; it may be null.</summary>
    public object? Parameter { get; }
    /// <summary>Gets the target captured at invocation entry, even if a handler reparents it.</summary>
    public Control Target { get; }
    /// <summary>Gets the originating control, or null when no control source was supplied.</summary>
    public Control? Source { get; }
    /// <summary>Gets or sets whether execution stops after this handler. Defaults to false.</summary>
    public bool Handled { get; set; }
}
