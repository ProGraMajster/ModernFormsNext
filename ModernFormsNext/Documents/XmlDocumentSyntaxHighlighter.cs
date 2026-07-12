using System;
using System.Collections.Generic;

namespace ModernFormsNext.Documents;

internal sealed class XmlDocumentSyntaxHighlighter : IDocumentSyntaxHighlighter
{
    public IReadOnlyList<DocumentSyntaxSpan> Highlight(string text)
    {
        var spans = new List<DocumentSyntaxSpan>();
        var index = 0;
        var insideTag = false;
        var expectTagName = false;

        while (index < text.Length)
        {
            var start = index;
            if (text.AsSpan(index).StartsWith("<!--", StringComparison.Ordinal))
            {
                var end = text.IndexOf("-->", index + 4, StringComparison.Ordinal);
                index = end < 0 ? text.Length : end + 3;
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Comment);
            }
            else if (text.AsSpan(index).StartsWith("<![CDATA[", StringComparison.Ordinal))
            {
                var end = text.IndexOf("]]>", index + 9, StringComparison.Ordinal);
                index = end < 0 ? text.Length : end + 3;
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.String);
            }
            else if (text[index] == '<')
            {
                index += index + 1 < text.Length && text[index + 1] == '/' ? 2 : 1;
                insideTag = true;
                expectTagName = true;
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Punctuation);
            }
            else if (insideTag && text[index] is '"' or '\'')
            {
                index = DocumentSyntaxScanner.ReadQuoted(text, index, text[index]);
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.String);
            }
            else if (insideTag && DocumentSyntaxScanner.IsIdentifierStart(text[index]))
            {
                index = DocumentSyntaxScanner.ReadIdentifier(text, index, allowHyphen: true, allowColon: true);
                DocumentSyntaxScanner.Add(spans, start, index, expectTagName ? DocumentSyntaxTokenKind.Type : DocumentSyntaxTokenKind.Property);
                expectTagName = false;
            }
            else if (insideTag && text[index] == '>')
            {
                index++;
                insideTag = false;
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Punctuation);
            }
            else if (insideTag && text[index] == '/' && index + 1 < text.Length && text[index + 1] == '>')
            {
                index += 2;
                insideTag = false;
                DocumentSyntaxScanner.Add(spans, start, index, DocumentSyntaxTokenKind.Punctuation);
            }
            else if (insideTag && text[index] == '=')
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
