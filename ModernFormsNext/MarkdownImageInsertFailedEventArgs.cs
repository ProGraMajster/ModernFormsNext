using System;

namespace ModernFormsNext;

/// <summary>
/// Provides information about a hosted Markdown image insertion or asset failure.
/// </summary>
public sealed class MarkdownImageInsertFailedEventArgs : EventArgs
{
    internal MarkdownImageInsertFailedEventArgs(string source, string message, Exception? exception)
    {
        Source = source;
        Message = message;
        Exception = exception;
    }

    /// <summary>Gets the underlying exception, when the failure originated from an exception.</summary>
    public Exception? Exception { get; }

    /// <summary>Gets the safe, human-readable failure message.</summary>
    public string Message { get; }

    /// <summary>Gets the image source associated with the failure.</summary>
    public string Source { get; }
}
