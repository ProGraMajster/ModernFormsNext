using System.Collections.Generic;

namespace ModernFormsNext.Documents;

internal sealed class ShellDocumentSyntaxHighlighter : IDocumentSyntaxHighlighter
{
    private static readonly HashSet<string> Keywords = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "begin", "break", "case", "catch", "continue", "do", "done", "else", "elseif", "end",
        "exit", "fi", "finally", "for", "foreach", "function", "if", "in", "param", "process",
        "return", "switch", "then", "throw", "trap", "try", "until", "while"
    };

    public IReadOnlyList<DocumentSyntaxSpan> Highlight(string text)
    {
        var spans = new List<DocumentSyntaxSpan>();
        var index = 0;

        while (index < text.Length)
        {
            var start = index;
            if (text[index] == '#')
            {
                index = text.IndexOf('\n', index + 1);
                if (index < 0)
                    index = text.Length;
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Comment);
            }
            else if (text[index] is '"' or '\'')
            {
                index = DocumentSyntaxScanner.ReadQuoted(text, index, text[index]);
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.String);
            }
            else if (text[index] == '$')
            {
                index++;
                if (index < text.Length && text[index] == '{')
                {
                    var end = text.IndexOf('}', index + 1);
                    index = end < 0 ? text.Length : end + 1;
                }
                else
                {
                    while (index < text.Length && DocumentSyntaxScanner.IsIdentifierPart(text[index], allowHyphen: true, allowColon: true))
                        index++;
                }
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Property);
            }
            else if (char.IsDigit(text[index]))
            {
                index = DocumentSyntaxScanner.ReadNumber(text, index);
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Number);
            }
            else if (DocumentSyntaxScanner.IsIdentifierStart(text[index]))
            {
                index = DocumentSyntaxScanner.ReadIdentifier(text, index, allowHyphen: true);
                if (Keywords.Contains(text[start..index]))
                    DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Keyword);
            }
            else if ("{}[]();|&<>=".Contains(text[index]))
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
}
