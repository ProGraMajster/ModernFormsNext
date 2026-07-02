namespace ModernFormsNext.CodeGeneration.Reverse;

/// <summary>
/// Describes the severity of a C# designer reverse-sync diagnostic.
/// </summary>
public enum CSharpDesignerDiagnosticSeverity
{
    /// <summary>
    /// Informational diagnostic that does not affect parsing.
    /// </summary>
    Info,

    /// <summary>
    /// Warning for unsupported or partially understood syntax.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that prevents a usable design document from being produced.
    /// </summary>
    Error
}
