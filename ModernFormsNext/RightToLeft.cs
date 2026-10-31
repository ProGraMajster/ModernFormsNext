namespace ModernFormsNext;

/// <summary>Specifies a control's horizontal layout reading direction.</summary>
/// <remarks>
/// Flow and table containers use the effective direction when arranging children. This setting
/// does not select a culture, mirror a native window, or declare full bidirectional text support.
/// </remarks>
public enum RightToLeft
{
    /// <summary>Uses left-to-right layout independently of the parent.</summary>
    No = 0,
    /// <summary>Uses right-to-left layout independently of the parent.</summary>
    Yes = 1,
    /// <summary>Uses the parent's effective direction, or <see cref="No"/> at a root.</summary>
    Inherit = 2
}
