using System.Drawing;
using ModernFormsNext.Documents;
using Topten.RichTextKit;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentLinkInteractionTests
{
    [Fact]
    public void PointerDownDoesNotActivateAndPointerUpOnSameLinkActivatesOnce()
    {
        using var viewer = CreateViewer("A [link](https://example.com).");
        var point = GetPointInsideLink(viewer, 0);
        var activations = 0;
        viewer.LinkClicked += (_, e) =>
        {
            activations++;
            Assert.Equal("https://example.com", e.Destination);
            Assert.Equal("link", e.Text);
        };

        viewer.PointerDown(point);

        Assert.Equal(0, activations);
        Assert.NotNull(viewer.PressedLink);

        viewer.PointerUp(point);

        Assert.Equal(1, activations);
        Assert.Null(viewer.PressedLink);
    }

    [Fact]
    public void ControlRoutingCaptureAndBackendEventOrderActivateOnce()
    {
        using var root = new RoutingRootPanel { Size = new Size(500, 320) };
        using var viewer = CreateViewer("A [routed link](https://example.com/routed).");
        viewer.Location = new Point(30, 20);
        root.Controls.Add(viewer);
        root.PerformLayout();
        viewer.PerformLayout();
        var local = GetPointInsideLink(viewer, 0);
        var routed = new Point(local.X + viewer.Left, local.Y + viewer.Top);
        var args = new MouseEventArgs(MouseButtons.Left, 1, routed.X, routed.Y, Point.Empty);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        root.RaiseMouseDown(args);
        Assert.True(viewer.Capture);
        root.RaiseClick(args);
        root.RaiseMouseUp(args);

        Assert.Equal(1, activations);
        Assert.False(viewer.Capture);
    }

    [Fact]
    public void PointerUpOutsideOrOnAnotherLinkDoesNotActivate()
    {
        using var viewer = CreateViewer("[first](https://first.example) and [second](https://second.example)");
        var first = GetPointInsideLink(viewer, 0);
        var second = GetPointInsideLink(viewer, 1);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        viewer.PointerDown(first);
        viewer.PointerUp(new Point(1, 1));
        viewer.PointerDown(first);
        viewer.PointerUp(second);

        Assert.Equal(0, activations);
        Assert.Null(viewer.PressedLink);
    }

    [Fact]
    public void TinyMovementActivatesButThresholdMovementStartsSelection()
    {
        using var viewer = CreateViewer("A [selectable link](https://example.com) and text.");
        var point = GetPointInsideLink(viewer, 0);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        viewer.PointerDown(point);
        viewer.PointerMove(new Point(point.X + 1, point.Y));
        Assert.NotNull(viewer.PressedLink);
        viewer.PointerUp(new Point(point.X + 1, point.Y));

        Assert.Equal(1, activations);

        viewer.PointerDown(point);
        viewer.PointerMove(new Point(point.X + viewer.DragThreshold, point.Y));
        viewer.PointerMove(new Point(point.X + 80, point.Y));
        viewer.PointerUp(new Point(point.X + 80, point.Y));

        Assert.Equal(1, activations);
        Assert.True(viewer.SelectionLength > 0);
        Assert.Null(viewer.PressedLink);
    }

    [Fact]
    public void DragEndingOnLinkDoesNotActivate()
    {
        using var viewer = CreateViewer("plain text before [link](https://example.com)");
        var textElement = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentTextLayoutElement>());
        var start = GetPointInsideCodePoint(textElement, 1);
        var link = GetPointInsideLink(viewer, 0);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        viewer.PointerDown(start);
        viewer.PointerMove(link);
        viewer.PointerUp(link);

        Assert.Equal(0, activations);
        Assert.True(viewer.SelectionLength > 0);
    }

    [Fact]
    public void DisabledViewerDoesNotActivateLink()
    {
        using var viewer = CreateViewer("[link](https://example.com)");
        var point = GetPointInsideLink(viewer, 0);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;
        viewer.Enabled = false;

        viewer.PointerDown(point);
        viewer.PointerUp(point);

        Assert.Equal(0, activations);
        Assert.Null(viewer.PressedLink);
    }

    [Fact]
    public void DisablingDuringPressCancelsActivationAndPressedState()
    {
        using var viewer = CreateViewer("[link](https://example.com)");
        var point = GetPointInsideLink(viewer, 0);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        viewer.PointerDown(point);
        viewer.Enabled = false;
        viewer.PointerUp(point);

        Assert.Equal(0, activations);
        Assert.Null(viewer.PressedLink);
    }

    [Fact]
    public void DoubleClickSelectsWordWithoutSecondActivation()
    {
        using var viewer = CreateViewer("A [clickable](https://example.com) link.");
        var point = GetPointInsideLink(viewer, 0);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        viewer.PointerDown(point);
        viewer.PointerUp(point);
        viewer.PointerDown(point);
        viewer.PointerDoubleClick(point);
        viewer.PointerUp(point);

        Assert.Equal(1, activations);
        Assert.Equal("clickable", viewer.SelectedText);
        Assert.Null(viewer.PressedLink);
    }

    [Fact]
    public void DocumentReplacementAndDisposeClearPressedState()
    {
        var viewer = CreateViewer("[link](https://example.com)");
        var point = GetPointInsideLink(viewer, 0);

        viewer.PointerDown(point);
        viewer.Document = new MarkdownParser().Parse("replacement");

        Assert.Null(viewer.PressedLink);

        viewer.Document = new MarkdownParser().Parse("[next](https://next.example)");
        viewer.PointerDown(GetPointInsideLink(viewer, 0));
        viewer.Dispose();

        Assert.Null(viewer.PressedLink);
    }

    [Fact]
    public void DisposingFromLinkClickedDoesNotLeaveAStaleCallbackOrPress()
    {
        var viewer = CreateViewer("[link](https://example.com)");
        var point = GetPointInsideLink(viewer, 0);
        viewer.LinkClicked += (_, _) => viewer.Dispose();

        viewer.PointerDown(point);
        viewer.PointerUp(point);

        Assert.Null(viewer.PressedLink);
    }

    [Fact]
    public void HoverUsesHandCursorAndLeavingLinkRestoresTextCursor()
    {
        using var viewer = CreateViewer("[link](https://example.com)");

        viewer.PointerMove(GetPointInsideLink(viewer, 0));
        Assert.Same(Cursors.Hand, viewer.Cursor);

        viewer.PointerMove(new Point(1, 1));
        Assert.Same(Cursors.IBeam, viewer.Cursor);
    }

    [Fact]
    public void LeavingLinkRestoresApplicationCursor()
    {
        using var viewer = CreateViewer("[link](https://example.com)");
        viewer.Cursor = Cursors.Cross;

        viewer.PointerMove(GetPointInsideLink(viewer, 0));
        Assert.Same(Cursors.Hand, viewer.Cursor);

        viewer.PointerMove(new Point(1, 1));
        Assert.Same(Cursors.Cross, viewer.Cursor);
    }

    [Fact]
    public void PressedStateRebuildsLinkRunWithPressedColor()
    {
        using var viewer = CreateViewer("[link](https://example.com)");
        viewer.DocumentStyle.LinkColor = SkiaSharp.SKColors.Blue;
        viewer.DocumentStyle.HoveredLinkColor = SkiaSharp.SKColors.Green;
        viewer.DocumentStyle.PressedLinkColor = SkiaSharp.SKColors.Red;
        var point = GetPointInsideLink(viewer, 0);

        viewer.PointerMove(point);
        var hovered = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentTextLayoutElement>());
        Assert.Contains(hovered.TextBlock.StyleRuns, run => run.Style.TextColor == SkiaSharp.SKColors.Green);

        viewer.PointerDown(point);
        var pressed = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentTextLayoutElement>());
        Assert.Contains(pressed.TextBlock.StyleRuns, run => run.Style.TextColor == SkiaSharp.SKColors.Red);

        viewer.PointerUp(point);
        var released = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentTextLayoutElement>());
        Assert.Contains(released.TextBlock.StyleRuns, run => run.Style.TextColor == SkiaSharp.SKColors.Green);
    }

    [Fact]
    public void ExplicitLayoutInvalidationClearsPressedState()
    {
        using var viewer = CreateViewer("[link](https://example.com)");

        viewer.PointerDown(GetPointInsideLink(viewer, 0));
        viewer.InvalidateDocumentLayout();

        Assert.Null(viewer.PressedLink);
    }

    [Fact]
    public void WrappedLinkActivatesFromFirstAndSecondVisualLines()
    {
        using var viewer = CreateViewer(
            "[This link has enough words to wrap across several visual lines in a narrow viewer](https://example.com/wrapped)",
            width: 150);
        var layoutLink = Assert.Single(viewer.GetDocumentLayout().Links);
        var first = GetPointInsideCodePoint(layoutLink.Element, layoutLink.Start);
        var secondLineIndex = FindCodePointOnLaterLine(layoutLink);
        var second = GetPointInsideCodePoint(layoutLink.Element, secondLineIndex);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        viewer.PointerDown(first);
        viewer.PointerUp(first);
        viewer.PointerDown(second);
        viewer.PointerUp(second);

        Assert.Equal(2, activations);
    }

    [Fact]
    public void LastGlyphRightHalfUsesActualGlyphHitInsteadOfCaretPosition()
    {
        using var viewer = CreateViewer("A [link](https://example.com) after");
        var link = Assert.Single(viewer.GetDocumentLayout().Links);
        var point = GetPointInsideCodePoint(link.Element, link.End - 1, 0.8f);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        viewer.PointerDown(point);
        viewer.PointerUp(point);

        Assert.Equal(1, activations);
    }

    [Theory]
    [InlineData("😀 [emoji link](https://example.com/emoji)")]
    [InlineData("- [list link](https://example.com/list)")]
    [InlineData("> [quote link](https://example.com/quote)")]
    [InlineData("# [heading link](https://example.com/heading)")]
    [InlineData("| Link | Value |\n| --- | --- |\n| [table link](https://example.com/table) | Cell |")]
    public void SemanticContextsAndEmojiPreserveLinkHitTesting(string markdown)
    {
        using var viewer = CreateViewer(markdown, width: 260);
        var point = GetPointInsideLink(viewer, 0);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        viewer.PointerDown(point);
        viewer.PointerUp(point);

        Assert.Equal(1, activations);
    }

    [Fact]
    public void AutolinkEmailAndMultipleLinksUseIndependentIdentities()
    {
        using var viewer = CreateViewer("https://example.com, <hello@example.com>, and [docs](https://docs.example.com)");
        var links = viewer.GetDocumentLayout().Links;
        Assert.Equal(3, links.Count);
        var destinations = new List<string>();
        viewer.LinkClicked += (_, e) => destinations.Add(e.Destination);

        for (var index = 0; index < links.Count; index++)
        {
            var point = GetPointInsideLink(viewer, index);
            viewer.PointerDown(point);
            viewer.PointerUp(point);
        }

        Assert.Equal(new[] { "https://example.com", "mailto:hello@example.com", "https://docs.example.com" }, destinations);
    }

    [Fact]
    public void OneSemanticLinkAcrossStyledRunsCanReleaseOnAnotherRun()
    {
        using var viewer = CreateViewer("[normal **bold** and *italic*](https://example.com/styled)");
        var links = viewer.GetDocumentLayout().Links;
        Assert.True(links.Count >= 3);
        Assert.All(links, link => Assert.Same(links[0].Link, link.Link));
        var down = GetPointInsideCodePoint(links[0].Element, links[0].Start);
        var up = GetPointInsideCodePoint(links[^1].Element, links[^1].Start);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        viewer.PointerDown(down);
        viewer.PointerUp(up);

        Assert.Equal(1, activations);
    }

    [Fact]
    public void ScrolledDocumentTranslatesViewportPointForLinkHitTesting()
    {
        var prefix = string.Join("\n\n", Enumerable.Repeat("A paragraph that consumes vertical space.", 20));
        using var viewer = CreateViewer(prefix + "\n\n[scrolled link](https://example.com/scrolled)", width: 260);
        var link = Assert.Single(viewer.GetDocumentLayout().Links);
        viewer.VerticalScrollBar.Value = Math.Min(
            viewer.VerticalScrollBar.Maximum,
            Math.Max(0, link.Element.Bounds.Top - viewer.PaddedClientRectangle.Top));
        var point = GetPointInsideCodePoint(link.Element, link.Start);
        point.Offset(0, -viewer.ScrollOffset);
        var activations = 0;
        viewer.LinkClicked += (_, _) => activations++;

        viewer.PointerDown(point);
        viewer.PointerUp(point);

        Assert.Equal(1, activations);
    }

    [Fact]
    public void InteractionStateUsesSingleDpiScaledThresholdBoundary()
    {
        var link = new LinkInline("https://example.com", new DocumentInline[] { new TextInline("link") });
        var state = new DocumentLinkInteractionState();

        state.Begin(link, new Point(10, 10));
        var below = state.Move(link, new Point(14, 10), 5);
        Assert.False(state.DragStarted);
        Assert.False(below.DragStartedNow);

        var boundary = state.Move(link, new Point(15, 10), 5);
        Assert.True(state.DragStarted);
        Assert.True(boundary.DragStartedNow);
        Assert.Null(state.PressedLink);
        Assert.Null(state.Complete(link));
    }

    private static InputDocumentViewer CreateViewer(string markdown, int width = 360)
        => new()
        {
            Size = new Size(width, 260),
            Document = new MarkdownParser().Parse(markdown)
        };

    private static Point GetPointInsideLink(DocumentViewer viewer, int linkIndex)
    {
        var link = viewer.GetDocumentLayout().Links[linkIndex];
        return GetPointInsideCodePoint(link.Element, link.Start);
    }

    private static int FindCodePointOnLaterLine(DocumentLayoutLink link)
    {
        var firstTop = link.Element.TextBlock.GetCaretInfo(new CaretPosition(link.Start)).CaretRectangle.Top;
        for (var index = link.Start + 1; index < link.End; index++)
        {
            var top = link.Element.TextBlock.GetCaretInfo(new CaretPosition(index)).CaretRectangle.Top;
            if (top > firstTop)
                return index;
        }

        throw new InvalidOperationException("The test link did not wrap to a later line.");
    }

    private static Point GetPointInsideCodePoint(DocumentTextLayoutElement element, int index, float horizontalFraction = 0.35f)
    {
        var start = element.TextBlock.GetCaretInfo(new CaretPosition(index)).CaretRectangle;
        var end = element.TextBlock.GetCaretInfo(new CaretPosition(index + 1)).CaretRectangle;
        var glyphWidth = end.Top == start.Top ? end.Left - start.Left : Math.Max(2f, start.Height * 0.5f);
        var x = element.TextOrigin.X + (int)Math.Round(start.Left + (Math.Max(2f, glyphWidth) * horizontalFraction));
        var y = element.TextOrigin.Y + (int)Math.Round(start.Top + (start.Height / 2f));
        return new Point(x, y);
    }

    private sealed class InputDocumentViewer : DocumentViewer
    {
        public int DragThreshold => Math.Max(1, LogicalToDeviceUnits(DocumentLinkInteractionState.DragThresholdLogicalPixels));

        public void PointerDown(Point point)
            => OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, Point.Empty));

        public void PointerMove(Point point)
            => OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, Point.Empty));

        public void PointerUp(Point point)
            => OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, Point.Empty));

        public void PointerDoubleClick(Point point)
            => OnDoubleClick(new MouseEventArgs(MouseButtons.Left, 2, point.X, point.Y, Point.Empty));
    }

    private sealed class RoutingRootPanel : Panel
    {
        public override bool Visible
        {
            get => true;
            set => base.Visible = value;
        }
    }
}
