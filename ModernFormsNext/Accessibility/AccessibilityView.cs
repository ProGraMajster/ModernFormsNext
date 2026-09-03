namespace ModernFormsNext.Accessibility;

/// <summary>
/// Specifies which platform-neutral accessibility projection should contain an
/// <see cref="AccessibleObject"/>.
/// </summary>
/// <remarks>
/// The values describe progressively more user-relevant projections. <see cref="Raw"/> objects are
/// available only in the complete semantic tree, <see cref="Control"/> objects also appear in the
/// interactive control projection, and <see cref="Content"/> objects also appear in the content
/// projection. <see cref="Hidden"/> objects are excluded from active accessibility trees. This enum
/// is independent from platform-specific view enumerations.
/// </remarks>
public enum AccessibilityView
{
    /// <summary>
    /// The framework should infer the view from the represented object.
    /// </summary>
    Default,

    /// <summary>
    /// The object appears only in the complete raw semantic tree.
    /// </summary>
    Raw,

    /// <summary>
    /// The object appears in raw and interactive-control projections.
    /// </summary>
    Control,

    /// <summary>
    /// The object appears in raw, control, and user-content projections.
    /// </summary>
    Content,

    /// <summary>
    /// The object is decorative, removed, disposed, or otherwise excluded from active trees.
    /// </summary>
    Hidden
}

