using System.Collections.Generic;

namespace ModernFormsNext.Documents;

internal sealed class CSharpDocumentSyntaxHighlighter : IDocumentSyntaxHighlighter
{
    private static readonly HashSet<string> Keywords = new(System.StringComparer.Ordinal)
    {
        "abstract", "as", "async", "await", "base", "break", "case", "catch", "checked",
        "class", "const", "continue", "default", "delegate", "do", "else", "enum", "event",
        "explicit", "extern", "false", "finally", "fixed", "for", "foreach", "goto", "if",
        "implicit", "in", "interface", "internal", "is", "lock", "namespace", "new", "null",
        "operator", "out", "override", "params", "private", "protected", "public", "readonly",
        "record", "ref", "required", "return", "sealed", "sizeof", "stackalloc", "static",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "unchecked", "unsafe",
        "using", "virtual", "volatile", "when", "where", "while", "with", "yield"
    };

    private static readonly HashSet<string> Types = new(System.StringComparer.Ordinal)
    {
        "bool", "byte", "char", "decimal", "double", "dynamic", "float", "int", "long",
        "nint", "nuint", "object", "sbyte", "short", "string", "uint", "ulong", "ushort", "void"
    };

    public IReadOnlyList<DocumentSyntaxSpan> Highlight(string text)
    {
        var spans = new List<DocumentSyntaxSpan>();
        var index = 0;

        while (index < text.Length)
        {
            var start = index;
            var current = text[index];

            if (current == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                index = text.IndexOf('\n', index + 2);
                if (index < 0)
                    index = text.Length;
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Comment);
            }
            else if (current == '/' && index + 1 < text.Length && text[index + 1] == '*')
            {
                var end = text.IndexOf("*/", index + 2, System.StringComparison.Ordinal);
                index = end < 0 ? text.Length : end + 2;
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Comment);
            }
            else if (TryReadString(text, ref index))
            {
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.String);
            }
            else if (char.IsDigit(current))
            {
                index = DocumentSyntaxScanner.ReadNumber(text, index);
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Number);
            }
            else if (DocumentSyntaxScanner.IsIdentifierStart(current))
            {
                index = DocumentSyntaxScanner.ReadIdentifier(text, index);
                var identifier = text[start..index];
                if (Keywords.Contains(identifier))
                    DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Keyword);
                else if (Types.Contains(identifier) || char.IsUpper(identifier[0]))
                    DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Type);
            }
            else if ("{}[]();,.:=+-*/!?<>&|".Contains(current))
            {
                index++;
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Punctuation);
            }
            else
            {
                index++;
            }
        }

        return spans;
    }

    private static bool TryReadString(string text, ref int index)
    {
        var start = index;
        var verbatim = false;

        if (text[index] is '@' or '$')
        {
            var prefixEnd = index;
            while (prefixEnd < text.Length && text[prefixEnd] is '@' or '$')
            {
                verbatim |= text[prefixEnd] == '@';
                prefixEnd++;
            }

            if (prefixEnd >= text.Length || text[prefixEnd] != '"')
                return false;
            index = prefixEnd;
        }
        else if (text[index] is not ('"' or '\''))
        {
            return false;
        }

        var quote = text[index];
        index = DocumentSyntaxScanner.ReadQuoted(text, index, quote, verbatim && quote == '"');
        return index > start;
    }
}
