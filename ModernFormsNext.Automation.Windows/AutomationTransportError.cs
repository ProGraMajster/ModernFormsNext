namespace ModernFormsNext.Automation.Windows;

/// <summary>Contains safe transport failures independent of canonical semantic result codes.</summary>
public enum AutomationTransportError
{
    /// <summary>No transport error.</summary>
    None,
    /// <summary>The frame or request shape is invalid.</summary>
    InvalidRequest,
    /// <summary>The protocol version is incompatible.</summary>
    ProtocolMismatch,
    /// <summary>Peer identity, instance or secret authentication failed.</summary>
    AuthenticationFailed,
    /// <summary>Another authenticated client or request owns the available slot.</summary>
    Busy,
    /// <summary>The operation was not granted by transport capability negotiation.</summary>
    CapabilityDenied,
    /// <summary>A byte or string payload exceeded its explicit budget.</summary>
    PayloadTooLarge,
    /// <summary>The request deadline elapsed.</summary>
    DeadlineExceeded,
    /// <summary>The request was cancelled before an outcome became ambiguous.</summary>
    Cancelled,
    /// <summary>The server or connection session ended.</summary>
    SessionEnded,
    /// <summary>The process, endpoint or connection is unavailable.</summary>
    ApplicationUnavailable,
    /// <summary>A mutation may have executed but its response was lost. Never automatically retry it.</summary>
    OutcomeUnknown,
    /// <summary>A failure occurred without exposing arbitrary application details.</summary>
    ApplicationError,
    /// <summary>The discovery directory or file did not satisfy the restrictive local ACL/path policy.</summary>
    UnsafeDiscovery
}

/// <summary>Reports a controlled transport failure without application messages, credentials or request values.</summary>
/// <remarks>Semantic failures remain in AutomationResult/AutomationActionResult. This exception describes only the connection/protocol layer.</remarks>
public sealed class AutomationTransportException : Exception
{
    /// <summary>Constructs a safe transport exception with no inner application exception.</summary>
    /// <param name="error">The controlled failure code.</param>
    public AutomationTransportException(AutomationTransportError error) : base($"Automation transport: {error}.") => Error = error;
    /// <summary>Gets the structured transport failure.</summary>
    public AutomationTransportError Error { get; }
}
