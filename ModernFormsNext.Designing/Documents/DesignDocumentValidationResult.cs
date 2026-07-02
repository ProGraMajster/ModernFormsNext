using System.Collections.Generic;

namespace ModernFormsNext.Designing;

/// <summary>
/// Contains non-throwing validation messages for a designer document.
/// </summary>
/// <remarks>
/// Validation failures are normal user-editable document state. Callers should inspect
/// <see cref="Errors"/> and <see cref="Warnings"/> instead of expecting exceptions for
/// ordinary document mistakes.
/// </remarks>
public sealed class DesignDocumentValidationResult
{
    private readonly List<string> errors = [];
    private readonly List<string> warnings = [];

    /// <summary>
    /// Gets validation errors that prevent reliable code generation or hosting.
    /// </summary>
    public IReadOnlyList<string> Errors => errors;

    /// <summary>
    /// Gets validation warnings for suspicious but potentially usable document state.
    /// </summary>
    public IReadOnlyList<string> Warnings => warnings;

    /// <summary>
    /// Gets a value indicating whether the document has no validation errors.
    /// </summary>
    public bool IsValid => errors.Count == 0;

    /// <summary>
    /// Adds a validation error.
    /// </summary>
    /// <param name="message">The validation error message.</param>
    public void AddError(string message) => errors.Add(message);

    /// <summary>
    /// Adds a validation warning.
    /// </summary>
    /// <param name="message">The validation warning message.</param>
    public void AddWarning(string message) => warnings.Add(message);

    /// <summary>
    /// Adds all messages from another validation result.
    /// </summary>
    /// <param name="other">The validation result to merge.</param>
    public void AddRange(DesignDocumentValidationResult other)
    {
        ArgumentNullException.ThrowIfNull(other);

        errors.AddRange(other.Errors);
        warnings.AddRange(other.Warnings);
    }
}
