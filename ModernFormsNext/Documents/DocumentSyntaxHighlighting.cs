using System;
using System.Collections.Generic;

namespace ModernFormsNext.Documents;

internal enum DocumentSyntaxTokenKind
{
    Keyword,
    String,
    Number,
    Comment,
    Type,
    Property,
    Punctuation
}

internal readonly record struct DocumentSyntaxSpan(int Start, int Length, DocumentSyntaxTokenKind Kind)
{
    public int End => Start + Length;
}

internal interface IDocumentSyntaxHighlighter
{
    IReadOnlyList<DocumentSyntaxSpan> Highlight(string text);
}

internal static class DocumentSyntaxHighlighterRegistry
{
    private static readonly IDocumentSyntaxHighlighter CSharp = new CSharpDocumentSyntaxHighlighter();
    private static readonly IDocumentSyntaxHighlighter Json = new JsonDocumentSyntaxHighlighter();
    private static readonly IDocumentSyntaxHighlighter Xml = new XmlDocumentSyntaxHighlighter();
    private static readonly IDocumentSyntaxHighlighter Shell = new ShellDocumentSyntaxHighlighter();

    public static IReadOnlyList<DocumentSyntaxSpan> Highlight(string? language, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Resolve(language)?.Highlight(text) ?? Array.Empty<DocumentSyntaxSpan>();
    }

    public static string GetDisplayName(string language)
        => Normalize(language) switch
        {
            "csharp" or "cs" => "C#",
            "json" => "JSON",
            "xml" => "XML",
            "bash" => "Bash",
            "shell" or "sh" => "Shell",
            "powershell" or "ps1" or "pwsh" => "PowerShell",
            _ => language.Trim()
        };

    private static IDocumentSyntaxHighlighter? Resolve(string? language)
        => Normalize(language) switch
        {
            "csharp" or "cs" => CSharp,
            "json" => Json,
            "xml" => Xml,
            "bash" or "shell" or "sh" or "powershell" or "ps1" or "pwsh" => Shell,
            _ => null
        };

    private static string Normalize(string? language)
        => string.IsNullOrWhiteSpace(language) ? string.Empty : language.Trim().ToLowerInvariant();
}

internal static class DocumentSyntaxScanner
{
    public static int ReadIdentifier(string text, int start, bool allowHyphen = false, bool allowColon = false)
    {
        var index = start + 1;
        while (index < text.Length && IsIdentifierPart(text[index], allowHyphen, allowColon))
            index++;
        return index;
    }

    public static int ReadNumber(string text, int start)
    {
        var index = start;
        if (index < text.Length && (text[index] == '+' || text[index] == '-'))
            index++;

        while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] is '.' or '_' or '+' or '-'))
            index++;
        return index;
    }

    public static int ReadQuoted(string text, int quoteIndex, char quote, bool doubledQuoteEscapes = false)
    {
        var index = quoteIndex + 1;
        while (index < text.Length)
        {
            if (text[index] == quote)
            {
                if (doubledQuoteEscapes && index + 1 < text.Length && text[index + 1] == quote)
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            if (!doubledQuoteEscapes && text[index] == '\\' && index + 1 < text.Length)
                index += 2;
            else
                index++;
        }

        return text.Length;
    }

    public static bool IsIdentifierStart(char value) => value == '_' || char.IsLetter(value);

    public static bool IsIdentifierPart(char value, bool allowHyphen = false, bool allowColon = false)
        => value == '_'
            || (allowHyphen && value == '-')
            || (allowColon && value == ':')
            || char.IsLetterOrDigit(value);

    public static void Add(List<DocumentSyntaxSpan> spans, int start, int end, DocumentSyntaxTokenKind kind)
    {
        if (end > start)
            spans.Add(new DocumentSyntaxSpan(start, end - start, kind));
    }
}
