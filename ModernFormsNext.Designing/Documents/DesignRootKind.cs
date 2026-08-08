namespace ModernFormsNext.Designing;

/// <summary>
/// Identifies the runtime base type represented by a designer document root.
/// </summary>
/// <remarks>
/// The root kind changes only root-specific presentation and generated initialization. Child
/// selection, layout, serialization, and code generation continue to use the same document tree.
/// </remarks>
public enum DesignRootKind
{
    /// <summary>
    /// The document represents a top-level <c>ModernFormsNext.Form</c>.
    /// </summary>
    Form,

    /// <summary>
    /// The document represents a reusable <c>ModernFormsNext.UserControl</c>.
    /// </summary>
    UserControl
}
