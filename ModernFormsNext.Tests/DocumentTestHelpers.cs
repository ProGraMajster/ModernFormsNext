using System.Drawing;
using ModernFormsNext.Documents;

namespace ModernFormsNext.Tests;

internal static class DocumentTestHelpers
{
    public static DocumentLayout LayoutDocument(Document document, int width)
    {
        using var viewer = new DocumentViewer();
        return LayoutDocument(document, viewer, width);
    }

    public static DocumentLayout LayoutDocument(Document document, DocumentViewer viewer, int width)
        => DocumentLayoutEngine.Layout(
            viewer,
            document,
            viewer.DocumentStyle,
            new Rectangle(0, 0, width, 1000),
            null,
            null);

    public static IEnumerable<T> FlattenInlines<T>(IEnumerable<DocumentInline> inlines)
        where T : DocumentInline
    {
        foreach (var inline in inlines)
        {
            if (inline is T match)
                yield return match;

            IEnumerable<DocumentInline>? children = inline switch
            {
                StrongInline strong => strong.Inlines,
                EmphasisInline emphasis => emphasis.Inlines,
                StrikethroughInline strike => strike.Inlines,
                LinkInline link => link.Inlines,
                _ => null
            };

            if (children is null)
                continue;

            foreach (var nested in FlattenInlines<T>(children))
                yield return nested;
        }
    }

    public static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }
    }
}
