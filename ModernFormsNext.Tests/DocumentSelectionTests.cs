using System.Drawing;
using ModernFormsNext.Documents;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Platform;
using Topten.RichTextKit;
using Xunit;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;

namespace ModernFormsNext.Tests;

[Collection("Clipboard")]
public class DocumentSelectionTests
{
    private static readonly ClipboardTestService.InMemoryClipboard ClipboardService = ClipboardTestService.GetOrRegister();

    [Fact]
    public void EmptyDocumentSelectionIsStable()
    {
        using var viewer = new DocumentViewer();

        viewer.SelectAll();

        Assert.Equal(0, viewer.SelectionStart);
        Assert.Equal(0, viewer.SelectionLength);
        Assert.Equal(string.Empty, viewer.SelectedText);
    }

    [Fact]
    public void SelectionCanCrossHeadingParagraphAndCodeBlock()
    {
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse("# Heading\n\nParagraph\n\n```\ncode line\n```")
        };

        viewer.SelectAll();

        Assert.Equal("Heading\n\nParagraph\n\ncode line", viewer.SelectedText);
    }

    [Fact]
    public void SelectAndClearSelectionUseLogicalTextOffsets()
    {
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse("Alpha beta gamma")
        };

        viewer.Select(6, 4);

        Assert.Equal(6, viewer.SelectionStart);
        Assert.Equal(4, viewer.SelectionLength);
        Assert.Equal("beta", viewer.SelectedText);

        viewer.ClearSelection();

        Assert.Equal(0, viewer.SelectionLength);
        Assert.Equal(string.Empty, viewer.SelectedText);
    }

    [Fact]
    public void SelectAllIncludesListTaskTableAndFootnoteCopySemantics()
    {
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse("""
                - item
                - [x] done

                | A | B |
                | - | - |
                | one | two |

                Text[^note].

                [^note]: Footnote body.
                """)
        };

        viewer.SelectAll();
        var text = viewer.SelectedText;

        Assert.Contains("\u2022 item", text);
        Assert.Contains("[x] done", text);
        Assert.Contains("A\tB", text);
        Assert.Contains("one\ttwo", text);
        Assert.Contains("Text[1].", text);
        Assert.Contains("[1] Footnote body.", text);
    }

    [Fact]
    public void ReversedDragRangeNormalizesStartAndLength()
    {
        var selection = new DocumentSelection();

        selection.SelectFromAnchor(9, 2, 12);

        Assert.Equal(2, selection.Start);
        Assert.Equal(7, selection.Length);
        Assert.Equal(9, selection.End);
    }

    [Fact]
    public void ReplacingDocumentClearsSelection()
    {
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse("First document")
        };
        viewer.SelectAll();

        viewer.Document = new MarkdownParser().Parse("Second document");

        Assert.Equal(0, viewer.SelectionLength);
        Assert.Equal(string.Empty, viewer.SelectedText);
    }

    [Fact]
    public void LayoutInvalidationPreservesSelection()
    {
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse("Persistent selection")
        };
        viewer.Select(0, 10);

        viewer.InvalidateDocumentLayout();

        Assert.Equal("Persistent", viewer.SelectedText);
    }

    [Fact]
    public void CtrlASelectsAllAndCtrlCCopiesThroughPlatformClipboard()
    {
        using var viewer = new InputDocumentViewer
        {
            Document = new MarkdownParser().Parse("Copy this text")
        };

        var selectAll = viewer.RaiseKeyDown(Keys.Control | Keys.A);
        var copy = viewer.RaiseKeyDown(Keys.Control | Keys.C);

        Assert.True(selectAll.Handled);
        Assert.True(copy.Handled);
        Assert.Equal("Copy this text", ClipboardService.Text);
    }

    [Fact]
    public void CtrlCWithoutSelectionIsNotConsumed()
    {
        ClipboardService.Text = "unchanged";
        using var viewer = new InputDocumentViewer
        {
            Document = new MarkdownParser().Parse("No selection")
        };

        var copy = viewer.RaiseKeyDown(Keys.Control | Keys.C);

        Assert.False(copy.Handled);
        Assert.Equal("unchanged", ClipboardService.Text);
    }

    [Fact]
    public void LinkClickActivatesButLinkDragSelectsWithoutActivation()
    {
        using var viewer = new InputDocumentViewer
        {
            Size = new DrawingSize(360, 180),
            Document = new MarkdownParser().Parse("A [selectable link](https://example.com) and text.")
        };
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        var element = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentTextLayoutElement>());
        var clickPoint = GetPointInsideCodePoint(element, 4);

        viewer.RaiseMouseDown(clickPoint);
        viewer.RaiseMouseUp(clickPoint);

        Assert.Equal(1, activations);

        var dragEnd = GetPointInsideCodePoint(element, 15);
        viewer.RaiseMouseDown(clickPoint);
        viewer.RaiseMouseMove(dragEnd);
        viewer.RaiseMouseUp(dragEnd);

        Assert.Equal(1, activations);
        Assert.True(viewer.SelectionLength > 0);
        Assert.Contains("ectable", viewer.SelectedText);
    }

    private static DrawingPoint GetPointInsideCodePoint(DocumentTextLayoutElement element, int index)
    {
        var start = element.TextBlock.GetCaretInfo(new CaretPosition(index)).CaretRectangle;
        var end = element.TextBlock.GetCaretInfo(new CaretPosition(index + 1)).CaretRectangle;
        var x = element.TextOrigin.X + (int)Math.Round((start.Left + end.Left) / 2f);
        var y = element.TextOrigin.Y + (int)Math.Round(start.Top + (start.Height / 2f));
        return new DrawingPoint(x, y);
    }

    private sealed class InputDocumentViewer : DocumentViewer
    {
        public KeyEventArgs RaiseKeyDown(Keys keys)
        {
            var e = new KeyEventArgs(keys);
            OnKeyDown(e);
            return e;
        }

        public void RaiseMouseDown(DrawingPoint point)
            => OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, DrawingPoint.Empty));

        public void RaiseMouseMove(DrawingPoint point)
            => OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, DrawingPoint.Empty));

        public void RaiseMouseUp(DrawingPoint point)
            => OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, DrawingPoint.Empty));
    }

}
