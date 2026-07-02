using System.Globalization;
using System.Text;
using ModernFormsNext.Designing;

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
