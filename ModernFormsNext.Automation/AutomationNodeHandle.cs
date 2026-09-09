namespace ModernFormsNext.Automation;

/// <summary>Identifies a canonical peer within one semantic session, independently of its name or position.</summary>
/// <param name="SessionId">The session's random nonce; this is not a PID or authentication credential.</param>
/// <param name="RuntimeId">The positive canonical Int64 ID written as an invariant decimal string.</param>
/// <remarks>Every live operation also requires an explicit root registration ID and revalidates reachability.</remarks>
public readonly record struct AutomationNodeHandle(string SessionId, string RuntimeId);
