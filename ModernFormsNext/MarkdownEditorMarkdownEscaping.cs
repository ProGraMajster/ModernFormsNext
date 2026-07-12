using System;
using System.Text;

namespace ModernFormsNext;

internal static class MarkdownEditorMarkdownEscaping
{
    public static string EscapeLabel(string? value)
        => EscapeCharacters(value, '\\', '[', ']');

    public static string EscapeTitle(string? value)
        => EscapeCharacters(value, '\\', '"');

    public static string FormatDestination(string? value)
    {
        value ??= string.Empty;
        var useAngleBrackets = RequiresAngleBrackets(value);
        if (!useAngleBrackets)
            return EscapeCharacters(value, '\\', '(', ')', '<', '>');

        var escaped = EscapeCharacters(value, '\\', '<', '>');
        return "<" + escaped + ">";
    }

    private static bool RequiresAngleBrackets(string value)
    {
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character) || character is '(' or ')' or '"')
                return true;
        }

        return false;
    }

    private static string EscapeCharacters(string? value, params char[] characters)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (Array.IndexOf(characters, character) >= 0)
                builder.Append('\\');
            builder.Append(character);
        }

        return builder.ToString();
    }
}
