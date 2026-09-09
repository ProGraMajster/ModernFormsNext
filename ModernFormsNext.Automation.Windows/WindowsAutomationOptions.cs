namespace ModernFormsNext.Automation.Windows;

/// <summary>Configures an explicitly started Windows development bridge; immutable after initialization.</summary>
public sealed record WindowsAutomationOptions
{
    /// <summary>Gets a public discovery label (1–128 characters), not an identity or control-derived value.</summary>
    public string ApplicationName { get; init; } = AppDomain.CurrentDomain.FriendlyName;
    /// <summary>Gets the transport policy, intersected with the borrowed semantic session.</summary>
    public AutomationCapability Capabilities { get; init; } = AutomationCapability.Inspect | AutomationCapability.Query | AutomationCapability.Actions;
    /// <summary>Gets the request byte budget (4096–1048576); default 65536, excluding the four-byte prefix.</summary>
    public int MaxRequestBytes { get; init; } = 64 * 1024;
    /// <summary>Gets the response byte budget (4096–16777216); default 4194304, excluding the prefix.</summary>
    public int MaxResponseBytes { get; init; } = 4 * 1024 * 1024;
    /// <summary>Gets the maximum request/wait deadline (1–60000 milliseconds); default 60000.</summary>
    public int MaxDeadlineMilliseconds { get; init; } = 60000;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApplicationName) || ApplicationName.Length > 128 || ApplicationName.Any(char.IsControl)
            || !Protocol.ValidCapabilities(Capabilities) || MaxRequestBytes is < 4096 or > 1048576
            || MaxResponseBytes is < 4096 or > 16777216 || MaxDeadlineMilliseconds is < 1 or > 60000)
            throw new ArgumentException("Invalid automation server options.");
    }
}

/// <summary>Contains public discovery metadata; no credential or semantic root data is included.</summary>
/// <param name="InstanceId">Random bridge lifetime nonce, independent of process ID and semantic SessionId.</param>
/// <param name="ApplicationName">The application's public discovery label.</param>
/// <param name="ProcessId">The owning process.</param>
/// <param name="ProcessStartUtcTicks">Process creation identity as a precision-preserving decimal string.</param>
/// <param name="EndpointName">Local named-pipe endpoint, derived from process ID and instance nonce.</param>
/// <param name="ProtocolVersion">Wire protocol integer version.</param>
/// <param name="Capabilities">The server/session intersection, before client negotiation.</param>
/// <param name="CreatedUtc">Descriptor publication time; never used for elapsed deadlines.</param>
public sealed record AutomationApplicationInfo(string InstanceId, string ApplicationName, int ProcessId,
    string ProcessStartUtcTicks, string EndpointName, int ProtocolVersion, AutomationCapability Capabilities, DateTimeOffset CreatedUtc);

/// <summary>Contains negotiated connection metadata, detached from the server.</summary>
/// <param name="InstanceId">The authenticated bridge nonce.</param>
/// <param name="SessionId">Semantic handle scope, never an authentication credential.</param>
/// <param name="Capabilities">The granted server/session/client capability intersection.</param>
/// <param name="MaxRequestBytes">Effective request frame budget.</param>
/// <param name="MaxResponseBytes">Effective response frame budget.</param>
/// <param name="MaxDeadlineMilliseconds">Maximum per-request deadline.</param>
public sealed record AutomationConnectionInfo(string InstanceId, string SessionId, AutomationCapability Capabilities,
    int MaxRequestBytes, int MaxResponseBytes, int MaxDeadlineMilliseconds);
