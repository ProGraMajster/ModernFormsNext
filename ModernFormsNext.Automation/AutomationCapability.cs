namespace ModernFormsNext.Automation;

/// <summary>Specifies the operations explicitly allowed by a semantic session or root.</summary>
/// <remarks>Root capabilities are intersected with session capabilities. There is no transport or authentication in this package.</remarks>
[Flags]
public enum AutomationCapability
{
    /// <summary>No operations are allowed.</summary>
    None = 0,
    /// <summary>Root enumeration and inspection of known handles are allowed.</summary>
    Inspect = 1,
    /// <summary>Child enumeration and semantic searches are allowed.</summary>
    Query = 2,
    /// <summary>Advertised canonical semantic actions are allowed.</summary>
    Actions = 4
}
