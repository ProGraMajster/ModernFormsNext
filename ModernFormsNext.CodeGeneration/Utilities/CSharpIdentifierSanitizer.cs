using System.Globalization;
using System.Text;
using ModernFormsNext.Designing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ModernFormsNext.CodeGeneration.Utilities;

/// <summary>
/// Provides small helpers for creating safe C# identifiers from designer input.
/// </summary>
/// <remarks>
/// The generator validates document identifiers before emitting code. Sanitizing is kept
/// available for future designer UI flows that need to propose names while the user types.
/// </remarks>
public static class CSharpIdentifierSanitizer
{
    /// <summary>
    /// Determines whether a string is a valid C# identifier.
    /// </summary>
    /// <param name="identifier">The identifier to validate.</param>
    /// <returns><see langword="true"/> when the identifier is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidIdentifier(string? identifier)
        => DesignDocumentValidator.IsValidCSharpIdentifier(identifier);

    // A type name is data, not a source fragment. Parse the entire bounded input and
    // allow only named types with ordinary type arguments; no recovery, comments,
    // directives, statements or constructor syntax can cross into generated source.
    internal static bool IsValidTypeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > DesignDocumentValidator.MaximumTypeNameLength)
            return false;
        Span<char> delimiters = stackalloc char[64];
        int nesting = 0;
        foreach (char character in value)
        {
            if (character is '<' or '[' or '(')
            {
                if (nesting == delimiters.Length) return false;
                delimiters[nesting++] = character;
            }
            else if (character is '>' or ']' or ')')
            {
                char expected = character switch { '>' => '<', ']' => '[', _ => '(' };
                if (nesting == 0 || delimiters[--nesting] != expected) return false;
            }
        }
        if (nesting != 0) return false;
        var syntax = SyntaxFactory.ParseTypeName(value, consumeFullText: true);
        // Roslyn can leave trailing directives on the unreturned end-of-file token.
        // Require exact full-text coverage as well as valid syntax and safe trivia.
        return string.Equals(syntax.ToFullString(), value, StringComparison.Ordinal)
            && !syntax.ContainsDiagnostics && syntax is NameSyntax
            && syntax.DescendantTrivia(descendIntoTrivia: true).All(trivia =>
                trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            && IsSupportedType(syntax, 0);
    }

    private static bool IsSupportedType(TypeSyntax syntax, int depth)
    {
        if (depth > 64) return false;
        return syntax switch
        {
            IdentifierNameSyntax name => IsValidIdentifier(name.Identifier.Text),
            GenericNameSyntax name => IsValidIdentifier(name.Identifier.Text)
                && name.TypeArgumentList.Arguments.All(argument => IsSupportedType(argument, depth + 1)),
            QualifiedNameSyntax name => IsSupportedType(name.Left, depth + 1)
                && IsSupportedType(name.Right, depth + 1),
            AliasQualifiedNameSyntax name => (name.Alias.Identifier.Text == "global"
                    || IsValidIdentifier(name.Alias.Identifier.Text))
                && IsSupportedType(name.Name, depth + 1),
            PredefinedTypeSyntax type => !type.Keyword.IsKind(SyntaxKind.VoidKeyword),
            NullableTypeSyntax type => IsSupportedType(type.ElementType, depth + 1),
            ArrayTypeSyntax type => IsSupportedType(type.ElementType, depth + 1)
                && type.RankSpecifiers.All(rank => rank.Sizes.All(size => size is OmittedArraySizeExpressionSyntax)),
            _ => false
        };
    }

    /// <summary>
    /// Creates a valid C# identifier from arbitrary text.
    /// </summary>
    /// <param name="value">The source text.</param>
    /// <param name="fallback">The fallback identifier used when <paramref name="value"/> contains no usable characters.</param>
    /// <returns>A valid C# identifier.</returns>
    public static string SanitizeIdentifier(string? value, string fallback = "component")
    {
        if (IsValidIdentifier(value))
            return value!;

        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(value))
        {
            foreach (var character in value)
            {
                if (builder.Length == 0)
                {
                    if (IsIdentifierStart(character))
                        builder.Append(character);
                    else if (IsIdentifierPart(character))
                        builder.Append('_').Append(character);
                }
                else if (IsIdentifierPart(character))
                {
                    builder.Append(character);
                }
                else
                {
                    builder.Append('_');
                }
            }
        }

        var candidate = builder.Length == 0 ? fallback : builder.ToString();

        if (IsValidIdentifier(candidate))
            return candidate;

        candidate = "_" + candidate.TrimStart('@');

        return IsValidIdentifier(candidate) ? candidate : "_component";
    }

    private static bool IsIdentifierStart(char value)
        => value == '_' || char.IsLetter(value);

    private static bool IsIdentifierPart(char value)
    {
        if (value == '_' || char.IsLetterOrDigit(value))
            return true;

        var category = CharUnicodeInfo.GetUnicodeCategory(value);

        return category is UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.Format;
    }
}
