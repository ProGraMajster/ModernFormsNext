namespace ModernFormsNext.WindowKit.Platform.Services;

/// <summary>Describes a service outcome without native types or user content.</summary>
public enum PlatformServiceStatus
{
    /// <summary>The OS accepted the request; external delivery is not guaranteed.</summary>
    Success,
    /// <summary>The backend does not implement the operation.</summary>
    NotSupported,
    /// <summary>No eligible foreground host or provider is available.</summary>
    Unavailable,
    /// <summary>Another native UI operation owns the host.</summary>
    Busy,
    /// <summary>No visible registered handler can open the request.</summary>
    NoHandler,
    /// <summary>The user or system disabled access.</summary>
    PermissionDenied,
    /// <summary>The application manifest lacks the required permission.</summary>
    NotDeclared,
    /// <summary>The owning window or Activity presentation was lost; the caller may retry.</summary>
    HostLost,
    /// <summary>The backend has shut down.</summary>
    Shutdown
}

/// <summary>Reports operational failure where the existing result represents user cancellation.</summary>
/// <remarks>Caller cancellation uses OperationCanceledException; native faults propagate separately.</remarks>
public sealed class PlatformServiceException : InvalidOperationException
{
    /// <summary>Creates a content-free failure.</summary>
    /// <param name="status">The reason the operation could not complete.</param>
    public PlatformServiceException(PlatformServiceStatus status) : base($"Platform service: {status}.")
        => Status = status;
    /// <summary>Gets the operational failure reason.</summary>
    public PlatformServiceStatus Status { get; }
}
