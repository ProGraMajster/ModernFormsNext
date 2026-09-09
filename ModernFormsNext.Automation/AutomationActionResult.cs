namespace ModernFormsNext.Automation;

/// <summary>Separates acceptance of a canonical action from later business-operation completion.</summary>
public enum AutomationActionStatus
{
    /// <summary>PerformAction returned true. Async business work may still be running.</summary>
    Accepted,
    /// <summary>The current state, session policy, or canonical path rejected the request.</summary>
    Rejected,
    /// <summary>The action is outside this service's allowlist or is not currently advertised.</summary>
    Unsupported,
    /// <summary>The root/node is not reachable, the session ended, or reachability could not be validated.</summary>
    NodeUnavailable,
    /// <summary>The action flags or value payload are invalid.</summary>
    InvalidArgument,
    /// <summary>A canonical getter or action raised an application exception.</summary>
    ApplicationError
}

/// <summary>Contains acceptance status and a safe reason; it never claims async business completion.</summary>
/// <param name="Status">The request acceptance category.</param>
/// <param name="Error">The specific semantic reason; None accompanies Accepted.</param>
public sealed record AutomationActionResult(AutomationActionStatus Status, AutomationErrorCode Error);

/// <summary>Contains one explicitly typed text or numeric action value, with no arbitrary CLR object payload.</summary>
/// <remarks>Use null as the method argument to clear editable text. Never log or echo action values; ToString intentionally omits them.</remarks>
public sealed class AutomationActionValue
{
    private AutomationActionValue(string? text, double? number) { Text = text; Number = number; }
    /// <summary>Gets the text payload, or null for a numeric payload.</summary>
    public string? Text { get; }
    /// <summary>Gets the numeric payload, or null for a text payload.</summary>
    public double? Number { get; }
    /// <summary>Creates a text payload. Length is checked by the action service.</summary>
    /// <param name="text">The requested text, which may be sensitive.</param>
    /// <returns>The typed text value.</returns>
    public static AutomationActionValue FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(text, null);
    }
    /// <summary>Creates a numeric payload. Finiteness and range are checked by the action service.</summary>
    /// <param name="number">The requested numeric value.</param>
    /// <returns>The typed numeric value.</returns>
    public static AutomationActionValue FromNumber(double number) => new(null, number);
    /// <summary>Returns a fixed diagnostic label without revealing the payload.</summary>
    /// <returns>A safe label.</returns>
    public override string ToString() => nameof(AutomationActionValue);
}
