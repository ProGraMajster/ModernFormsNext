namespace ModernFormsNext.Designing;

/// <summary>
/// Describes how a designer-generated member should be emitted in C# code.
/// </summary>
public enum DesignerMemberVisibility
{
    /// <summary>
    /// No member should be generated. The generator should prefer a local variable when possible.
    /// </summary>
    None,

    /// <summary>
    /// Generate a private member.
    /// </summary>
    Private,

    /// <summary>
    /// Generate a protected member.
    /// </summary>
    Protected,

    /// <summary>
    /// Generate an internal member.
    /// </summary>
    Internal,

    /// <summary>
    /// Generate a public member.
    /// </summary>
    Public
}
