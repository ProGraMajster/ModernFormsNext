namespace ModernFormsNext;

/// <summary>Identifies an asynchronous command execution transition.</summary>
public enum AsyncCommandDiagnosticKind
{
    /// <summary>The invocation acquired the command's single-flight slot.</summary>
    Started,
    /// <summary>The operation completed successfully.</summary>
    Completed,
    /// <summary>The operation or an execution notification failed.</summary>
    Faulted,
    /// <summary>The operation acknowledged cancellation.</summary>
    Cancelled
}

/// <summary>Describes an async outcome without parameter values or exception messages.</summary>
/// <remarks>
/// Events run synchronously on the thread reporting the transition, which may be a background
/// continuation thread. Marshal UI work through Application.RunOnUIThread. No Task, cancellation
/// source, user text or exception instance is exposed by diagnostics.
/// </remarks>
public sealed class AsyncCommandDiagnosticEventArgs : EventArgs
{
    internal AsyncCommandDiagnosticEventArgs(AsyncCommandDiagnosticKind kind, Type? parameterType, Type? exceptionType)
        => (Kind, ParameterType, ExceptionType) = (kind, parameterType, exceptionType);

    /// <summary>Gets the execution transition.</summary>
    public AsyncCommandDiagnosticKind Kind { get; }
    /// <summary>Gets only the parameter's CLR type; null represents a null parameter.</summary>
    public Type? ParameterType { get; }
    /// <summary>Gets only the failure's CLR type, or null for a non-fault outcome.</summary>
    public Type? ExceptionType { get; }
}
