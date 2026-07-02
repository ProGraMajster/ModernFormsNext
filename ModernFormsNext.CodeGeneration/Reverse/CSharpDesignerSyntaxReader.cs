using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ModernFormsNext.CodeGeneration.Reverse;

/// <summary>
/// Provides small syntax helpers used by the ModernFormsNext C# designer parser.
/// </summary>
/// <remarks>
/// This reader intentionally exposes only low-level recognition helpers. It does not attempt
/// semantic analysis or execution of user code.
/// </remarks>
public sealed class CSharpDesignerSyntaxReader
{
    /// <summary>
    /// Gets the simple member name at the end of a member access expression.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns>The final member name, or <see langword="null"/> when the expression is not a simple member access.</returns>
    public string? GetFinalMemberName(ExpressionSyntax expression)
        => expression is MemberAccessExpressionSyntax memberAccess
            ? memberAccess.Name.Identifier.ValueText
            : null;

    /// <summary>
    /// Gets the identifier represented by <c>name</c> or <c>this.name</c>.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns>The object identifier, or <see langword="null"/> when the expression is not a supported object reference.</returns>
    public string? GetObjectReferenceName(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name }
                => name.Identifier.ValueText,
            _ => null
        };
}
