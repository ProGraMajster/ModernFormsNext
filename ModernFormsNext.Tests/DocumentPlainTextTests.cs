using ModernFormsNext.Documents;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentPlainTextTests
{
    [Fact]
    public void PlainTextExportIncludesListsTablesImagesAndFootnotes()
    {
        var document = new MarkdownParser().Parse("""
            # Title

            - [x] Done
              - Nested

            ![Logo](Images/icon.png)

            | A | B |
            | - | - |
            | one | two |

            Text[^1].

            [^1]: Footnote body.
            """);

        var text = document.GetPlainText();

        Assert.Contains("Title", text);
        Assert.Contains("[x] Done", text);
        Assert.Contains("\u25e6 Nested", text);
        Assert.Contains("Logo", text);
        Assert.Contains("A\tB", text);
        Assert.Contains("one\ttwo", text);
        Assert.Contains("Text[1].", text);
        Assert.Contains("[1] Footnote body.", text);
    }
}
