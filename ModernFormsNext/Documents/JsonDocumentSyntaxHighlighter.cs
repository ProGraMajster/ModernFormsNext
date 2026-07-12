using System.Collections.Generic;

namespace ModernFormsNext.Documents;

internal sealed class JsonDocumentSyntaxHighlighter : IDocumentSyntaxHighlighter
{
    public IReadOnlyList<DocumentSyntaxSpan> Highlight(string text)
    {
        var spans = new List<DocumentSyntaxSpan>();
        var index = 0;

        while (index < text.Length)
        {
            var start = index;
            if (text[index] == '"')
            {
                index = DocumentSyntaxScanner.ReadQuoted(text, index, '"');
                var next = index;
                while (next < text.Length && char.IsWhiteSpace(text[next]))
                    next++;
                var kind = next < text.Length && text[next] == ':'
                    ? DocumentSyntaxTokenKind.Property
                    : DocumentSyntaxTokenKind.String;
                DocumentSyntaxScanner.Add(spans, start, index, kind);
            }
            else if (char.IsDigit(text[index]) || (text[index] == '-' && index + 1 < text.Length && char.IsDigit(text[index + 1])))
            {
                index = DocumentSyntaxScanner.ReadNumber(text, index);
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Number);
            }
            else if (DocumentSyntaxScanner.IsIdentifierStart(text[index]))
            {
                index = DocumentSyntaxScanner.ReadIdentifier(text, index);
                var value = text[start..index];
                if (value is "true" or "false" or "null")
                    DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Keyword);
            }
            else if ("{}[],:".Contains(text[index]))
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
