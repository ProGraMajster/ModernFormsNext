namespace ModernFormsNext;

/// <summary>Reports an automatic shell delivery failure without including activation or state payloads.</summary>
public sealed class ShellLifecycleFailureEventArgs : EventArgs
{
    internal ShellLifecycleFailureEventArgs(Exception exception, long sequence, bool restoring)
        => (Exception, Sequence, IsStateRestoration) = (exception, sequence, restoring);
    /// <summary>Gets the original operation failure; applications choose whether to log its contents.</summary>
    public Exception Exception { get; }
    /// <summary>Gets the provider-local sequence of the activation or restoration notification.</summary>
    public long Sequence { get; }
    /// <summary>Gets whether this failure came from explicit state restoration rather than URI activation.</summary>
    public bool IsStateRestoration { get; }
}
