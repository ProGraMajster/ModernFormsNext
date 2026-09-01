using System.Drawing;
using System.Text;
using ModernFormsNext.Animations;
using ModernFormsNext.Documents;
using Topten.RichTextKit;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class EditorPointerInputRegressionTests
{
    [Fact]
    public void TextBoxClickPlacesCaretAtExactUtf16Index()
    {
        using var textBox = CreateTextBox<TextBox>("alpha beta gamma");

        Click(textBox, PointForIndex(textBox, 6));

        Assert.Equal(6, textBox.document.CursorIndex);
        Assert.Equal(-1, textBox.SelectionStart);
    }

    [Fact]
    public void RichTextBoxClickPlacesCaretAtExactUtf16Index()
    {
        using var textBox = CreateTextBox<RichTextBox>("alpha beta gamma");

        Click(textBox, PointForIndex(textBox, 11));

        Assert.Equal(11, textBox.document.CursorIndex);
        Assert.Equal(0, textBox.SelectionLength);
    }

    [Fact]
    public void MarkdownEditorSourceClickPlacesCaretAtExactUtf16Index()
    {
        using var editor = CreateMarkdownEditor("# alpha beta gamma");

        Click(editor.EditorSurface, PointForIndex(editor.EditorSurface, 8));

        Assert.Equal(8, editor.EditorSurface.document.CursorIndex);
        Assert.Equal(8, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void TextBoxCrLfClickMapsRenderedCaretToExactUtf16Index()
    {
        const string text = "first\r\nsecond\r\nthird";
        using var textBox = CreateTextBox<TextBox>(text);
        var expected = text.IndexOf("third", StringComparison.Ordinal) + 2;

        Click(textBox, PointForRenderedIndex(textBox, expected));

        Assert.Equal(expected, textBox.document.CursorIndex);
        Assert.Equal(-1, textBox.SelectionStart);
    }

    [Fact]
    public void MarkdownEditorCrLfClickThroughNestedTreeInsertsAtRenderedIndex()
    {
        const string markdown = "# Header\r\n\r\nEdit **bold** text.\r\n\r\n> Preview target\r\n\r\n- item";
        using var root = CreateMarkdownEditorRoot(markdown, out var editor);
        var expected = markdown.IndexOf("target", StringComparison.Ordinal) + 3;
        var local = PointForRenderedIndex(editor.EditorSurface, expected);
        var routed = PointInAncestor(editor.EditorSurface, root, local);
        Point? observedLocation = null;
        editor.EditorSurface.MouseDown += (_, e) => observedLocation = e.Location;

        Click(root, routed);
        Assert.Equal(local, observedLocation);
        editor.EditorSurface.RaiseKeyPress(new KeyPressEventArgs("123"));

        Assert.Equal(markdown.Insert(expected, "123"), editor.Markdown);
        Assert.Equal(expected + 3, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void MarkdownEditorCrLfDoubleClickThroughNestedTreeSelectsRenderedWord()
    {
        const string markdown = "# Header\r\n\r\nEdit **bold** text.\r\n\r\n> Preview target";
        using var root = CreateMarkdownEditorRoot(markdown, out var editor);
        var target = markdown.IndexOf("target", StringComparison.Ordinal) + 2;
        var local = PointForRenderedIndex(editor.EditorSurface, target);
        var routed = PointInAncestor(editor.EditorSurface, root, local);

        root.RaiseDoubleClick(Mouse(routed, clicks: 2));

        Assert.Equal("target", editor.SelectedText);
    }

    [Fact]
    public void MarkdownEditorCrLfDragSelectionUsesRenderedUtf16Boundaries()
    {
        const string markdown = "# Header\r\n\r\nEdit **bold** text.\r\n\r\n> Preview target";
        using var root = CreateMarkdownEditorRoot(markdown, out var editor);
        var start = markdown.IndexOf("bold", StringComparison.Ordinal) + 1;
        var end = markdown.IndexOf("target", StringComparison.Ordinal) + "target".Length;
        var startPoint = PointInAncestor(
            editor.EditorSurface,
            root,
            PointForRenderedIndex(editor.EditorSurface, start));
        var endPoint = PointInAncestor(
            editor.EditorSurface,
            root,
            PointForRenderedIndex(editor.EditorSurface, end));

        root.RaiseMouseDown(Mouse(startPoint));
        root.RaiseMouseMove(Mouse(endPoint));
        root.RaiseMouseUp(Mouse(endPoint));

        Assert.Equal(start, editor.SelectionStart);
        Assert.Equal(end - start, editor.SelectionLength);
        Assert.False(editor.EditorSurface.Capture);
    }

    [Fact]
    public void MarkdownEditorCrLfShiftClickExtendsFromUtf16Caret()
    {
        const string markdown = "# Header\r\n\r\nEdit **bold** text.\r\n\r\n> Preview target";
        using var root = CreateMarkdownEditorRoot(markdown, out var editor);
        var start = markdown.IndexOf("bold", StringComparison.Ordinal);
        var end = markdown.IndexOf("target", StringComparison.Ordinal) + "target".Length;
        editor.Select(start, 0);
        var endPoint = PointInAncestor(
            editor.EditorSurface,
            root,
            PointForRenderedIndex(editor.EditorSurface, end));

        root.RaiseMouseDown(Mouse(endPoint, Keys.Shift));
        root.RaiseMouseUp(Mouse(endPoint, Keys.Shift));

        Assert.Equal(start, editor.SelectionStart);
        Assert.Equal(end - start, editor.SelectionLength);
    }

    [Fact]
    public void RichTextBoxFormattedRunClickUsesRenderedTextBlock()
    {
        const string text = "WIDE prefix target suffix";
        using var textBox = CreateTextBox<RichTextBox>(text);
        textBox.Select(0, "WIDE prefix".Length);
        textBox.SelectionFont = new Font("Segoe UI", 28, FontStyle.Bold);
        textBox.DeselectAll();
        var expected = text.IndexOf("target", StringComparison.Ordinal) + 3;

        Click(textBox, PointForRenderedIndex(textBox, expected));

        Assert.Equal(expected, textBox.document.CursorIndex);
        Assert.Equal(0, textBox.SelectionLength);
    }

    [Fact]
    public void TypingAfterClickInsertsAtClickedIndex()
    {
        using var textBox = CreateTextBox<TextBox>("alpha beta");
        using var surface = new SkiaControlSurface(textBox);
        surface.Resize(textBox.Width, textBox.Height);
        var point = PointForIndex(textBox, 6);

        surface.ProcessPointer(7, ControlSurfacePointerAction.Down, point.X, point.Y);
        surface.ProcessPointer(7, ControlSurfacePointerAction.Up, point.X, point.Y);
        surface.CommitText("123");

        Assert.Equal("alpha 123beta", textBox.Text);
        Assert.Equal(9, textBox.document.CursorIndex);
    }

    [Fact]
    public void DragSelectionUsesCapturedTextEditorUntilMouseUp()
    {
        using var textBox = CreateTextBox<TextBox>("alpha beta gamma");
        using var surface = new SkiaControlSurface(textBox) { PointerDragThreshold = 1000 };
        surface.Resize(textBox.Width, textBox.Height);
        var start = PointForIndex(textBox, 2);
        var end = PointForIndex(textBox, 10);

        surface.ProcessPointer(9, ControlSurfacePointerAction.Down, start.X, start.Y);
        surface.ProcessPointer(9, ControlSurfacePointerAction.Move, end.X, end.Y);
        surface.ProcessPointer(9, ControlSurfacePointerAction.Up, end.X, end.Y);

        Assert.Equal(2, textBox.SelectionStart);
        Assert.Equal(10, textBox.SelectionEnd);
        Assert.False(textBox.Capture);
        Assert.Equal(0, surface.ActivePointerCount);
    }

    [Fact]
    public void ShiftClickExtendsSelectionFromExistingCaret()
    {
        using var textBox = CreateTextBox<TextBox>("alpha beta gamma");
        textBox.document.SetImeSelection(2, 2);
        var end = PointForIndex(textBox, 10);

        textBox.RaiseMouseDown(Mouse(end, Keys.Shift));
        textBox.RaiseMouseUp(Mouse(end, Keys.Shift));

        Assert.Equal(2, textBox.SelectionStart);
        Assert.Equal(10, textBox.SelectionEnd);
    }

    [Fact]
    public void MarkdownEditorDoubleClickSelectsWord()
    {
        using var editor = CreateMarkdownEditor("alpha beta gamma");
        var point = PointForIndex(editor.EditorSurface, 7);

        editor.EditorSurface.RaiseDoubleClick(Mouse(point, clicks: 2));

        Assert.Equal("beta", editor.SelectedText);
        Assert.Equal(6, editor.SelectionStart);
        Assert.Equal(4, editor.SelectionLength);
    }

    [Fact]
    public void PreviewInputDoesNotChangeSourceCaretOrSelection()
    {
        using var editor = CreateMarkdownEditor("- [ ] task\n[link](https://example.com)", MarkdownEditorViewMode.Split);
        editor.Select(2, 5);
        editor.PreviewViewer.PerformLayout();
        var checkBox = Assert.Single(
            editor.PreviewViewer.GetDocumentLayout().Elements.OfType<DocumentTaskCheckBoxLayoutElement>());
        var point = new Point(checkBox.Bounds.Left + 2, checkBox.Bounds.Top + 2);

        editor.PreviewViewer.RaiseMouseDown(Mouse(point));
        editor.PreviewViewer.RaiseMouseUp(Mouse(point));

        var link = Assert.Single(editor.PreviewViewer.GetDocumentLayout().Links);
        var linkPoint = PointInsideCodePoint(link.Element, link.Start);
        editor.PreviewViewer.RaiseMouseDown(Mouse(linkPoint));
        editor.PreviewViewer.RaiseMouseUp(Mouse(linkPoint));

        Assert.Equal(2, editor.SelectionStart);
        Assert.Equal(5, editor.SelectionLength);
    }

    [Fact]
    public void ParentPanelEffectDoesNotRunForChildTextEditorInput()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = new VisiblePanel
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(360, 80),
            Ripple = CreateRipple()
        };
        var textBox = CreateTextBox<TextBox>("alpha beta");
        textBox.Bounds = new Rectangle(20, 15, 300, 40);
        textBox.AnimationSchedulerOverride = harness.Scheduler;
        panel.Controls.Add(textBox);
        var local = PointForIndex(textBox, 6);
        var panelPoint = new Point(textBox.Left + local.X, textBox.Top + local.Y);

        panel.RaiseMouseDown(Mouse(panelPoint));
        panel.RaiseMouseUp(Mouse(panelPoint));

        Assert.Equal(6, textBox.document.CursorIndex);
        Assert.Equal(0, panel.Ripple.ActiveRippleCount);
        Assert.Equal(VisualState.Normal, panel.VisualState);
    }

    [Fact]
    public void DataGridEditingControlReceivesInputWithoutGridEffect()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var grid = new VisibleDataGridView
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(320, 140),
            Ripple = CreateRipple()
        };
        grid.Columns.Add("First", 220);
        grid.Rows.Add("alpha beta");
        grid.BeginEdit(0, 0);
        var editor = Assert.Single(grid.Controls.GetAllControls(true).OfType<TextBox>());
        editor.AnimationSchedulerOverride = harness.Scheduler;
        var local = PointForIndex(editor, 6);
        var gridPoint = new Point(editor.ScaledLeft + local.X, editor.ScaledTop + local.Y);

        grid.RaiseMouseDown(Mouse(gridPoint));
        grid.RaiseMouseUp(Mouse(gridPoint));

        Assert.Equal(6, editor.document.CursorIndex);
        Assert.Equal(0, grid.Ripple.ActiveRippleCount);
        Assert.NotEqual(VisualState.Pressed, grid.VisualState);
    }

    [Fact]
    public void ExplicitRippleRunsAfterCaretPlacementWithoutChangingSelection()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var textBox = CreateTextBox<TextBox>("alpha beta");
        textBox.AnimationSchedulerOverride = harness.Scheduler;
        textBox.Ripple = CreateRipple();
        var effect = new CaretProbeEffect(() => textBox.document.CursorIndex);
        textBox.InteractionEffects.Add(effect);

        Click(textBox, PointForIndex(textBox, 6));

        Assert.Equal(6, textBox.document.CursorIndex);
        Assert.Equal(6, effect.CursorAtPointerDown);
        Assert.Equal(1, textBox.Ripple.ActiveRippleCount);
        Assert.Equal(-1, textBox.SelectionStart);
    }

    [Fact]
    public void PointerCancelReleasesCaptureAndStopsDragSelection()
    {
        using var textBox = CreateTextBox<PointerTextBox>("alpha beta gamma");
        var start = PointForIndex(textBox, 2);
        var end = PointForIndex(textBox, 10);
        textBox.RaiseMouseDown(Mouse(start, pointerId: 12));

        textBox.CancelPointerForTest(12);
        textBox.MoveForTest(Mouse(end, pointerId: 12));

        Assert.False(textBox.Capture);
        Assert.Equal(2, textBox.document.CursorIndex);
        Assert.Equal(-1, textBox.SelectionStart);
    }

    [Fact]
    public void FocusLossReleasesCaptureAndStopsDragSelection()
    {
        using var textBox = CreateTextBox<PointerTextBox>("alpha beta gamma");
        using var surface = new SkiaControlSurface(textBox) { PointerDragThreshold = 1000 };
        surface.Resize(textBox.Width, textBox.Height);
        var start = PointForIndex(textBox, 2);
        var end = PointForIndex(textBox, 10);
        surface.ProcessPointer(13, ControlSurfacePointerAction.Down, start.X, start.Y);

        textBox.LoseFocusForTest();
        surface.ProcessPointer(13, ControlSurfacePointerAction.Move, end.X, end.Y);

        Assert.False(textBox.Capture);
        Assert.Equal(0, surface.ActivePointerCount);
        Assert.Equal(2, textBox.document.CursorIndex);
        Assert.Equal(-1, textBox.SelectionStart);
    }

    [Fact]
    public void PlatformCaptureLossCancelsCapturedControlSubtree()
    {
        using var root = new VisiblePanel { Size = new Size(360, 80) };
        var textBox = CreateTextBox<PointerTextBox>("alpha beta gamma");
        root.Controls.Add(textBox);
        textBox.RaiseMouseDown(Mouse(PointForIndex(textBox, 2), pointerId: 15));

        Assert.True(textBox.Capture);
        Assert.True(root.Capture);

        root.CancelCapturedPointerInteractionsInSubtree();

        Assert.False(textBox.Capture);
        Assert.False(root.Capture);
        Assert.Equal(-1, textBox.SelectionStart);
    }

    [Fact]
    public void RemovingCapturedEditorClearsPointerOwner()
    {
        using var root = new VisiblePanel { Size = new Size(360, 80) };
        var textBox = CreateTextBox<TextBox>("alpha beta");
        textBox.Bounds = new Rectangle(20, 15, 300, 40);
        root.Controls.Add(textBox);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(root.Width, root.Height);
        var local = PointForIndex(textBox, 2);
        var location = new Point(textBox.Left + local.X, textBox.Top + local.Y);
        surface.ProcessPointer(14, ControlSurfacePointerAction.Down, location.X, location.Y);

        root.Controls.Remove(textBox);

        Assert.False(textBox.Capture);
        Assert.False(root.Capture);
        Assert.Equal(0, surface.ActivePointerCount);
    }

    private static T CreateTextBox<T>(string text)
        where T : TextBox, new()
        => new()
        {
            Text = text,
            Size = new Size(340, 48)
        };

    private static MarkdownEditor CreateMarkdownEditor(
        string markdown,
        MarkdownEditorViewMode viewMode = MarkdownEditorViewMode.Editor)
    {
        var editor = new MarkdownEditor
        {
            Markdown = markdown,
            ViewMode = viewMode,
            ShowToolbar = false,
            Size = new Size(700, 260)
        };
        editor.PerformLayout();
        foreach (var control in editor.Controls.GetAllControls(true).ToArray())
            control.PerformLayout();
        editor.EditorSurface.PerformLayout();
        return editor;
    }

    private static VisiblePanel CreateMarkdownEditorRoot(string markdown, out MarkdownEditor editor)
    {
        var root = new VisiblePanel { Size = new Size(740, 300) };
        editor = new MarkdownEditor
        {
            Markdown = markdown,
            ViewMode = MarkdownEditorViewMode.Split,
            ShowToolbar = false,
            Bounds = new Rectangle(10, 10, 700, 260)
        };
        root.Controls.Add(editor);
        root.PerformLayout();
        editor.PerformLayout();
        foreach (var control in editor.Controls.GetAllControls(true).ToArray())
            control.PerformLayout();
        editor.EditorSurface.PerformLayout();
        return root;
    }

    private static Point PointForIndex(TextBox textBox, int utf16Index)
    {
        var block = textBox.document.GetTextBlock();
        var codePointIndex = textBox.document.GetLayoutCodePointIndex(utf16Index);
        var caret = block.GetCaretInfo(new CaretPosition(codePointIndex));
        return new Point(
            textBox.TextOrigin.X + (int)MathF.Round(caret.CaretXCoord),
            textBox.TextOrigin.Y + (int)MathF.Round(caret.CaretRectangle.MidY));
    }

    private static Point PointForRenderedIndex(TextBox textBox, int utf16Index)
    {
        var block = textBox is RichTextBox richTextBox
            ? richTextBox.GetRichTextBlock()
            : textBox.document.GetTextBlock();
        var caret = block.GetCaretInfo(new CaretPosition(GetRichTextKitCodePointIndex(textBox.Text, utf16Index)));
        var origin = textBox.GetTextOrigin(block);
        return new Point(
            origin.X + (int)MathF.Round(caret.CaretXCoord),
            origin.Y + (int)MathF.Round(caret.CaretRectangle.MidY));
    }

    private static int GetRichTextKitCodePointIndex(string text, int utf16Index)
    {
        var normalizedPrefix = text[..utf16Index].Replace("\r\n", "\n", StringComparison.Ordinal);
        var codePointIndex = 0;
        foreach (Rune _ in normalizedPrefix.EnumerateRunes())
            codePointIndex++;
        return codePointIndex;
    }

    private static Point PointInAncestor(Control control, Control ancestor, Point point)
    {
        for (var current = control; !ReferenceEquals(current, ancestor); current = current.Parent
            ?? throw new InvalidOperationException("The requested control is not parented to the ancestor."))
        {
            point.Offset(current.ScaledLeft, current.ScaledTop);
        }

        return point;
    }

    private static Point PointInsideCodePoint(DocumentTextLayoutElement element, int codePointIndex)
    {
        var start = element.TextBlock.GetCaretInfo(new CaretPosition(codePointIndex)).CaretRectangle;
        var end = element.TextBlock.GetCaretInfo(new CaretPosition(codePointIndex + 1)).CaretRectangle;
        var glyphWidth = end.Top == start.Top ? end.Left - start.Left : Math.Max(2f, start.Height * 0.5f);
        return new Point(
            element.TextOrigin.X + (int)MathF.Round(start.Left + (Math.Max(2f, glyphWidth) * 0.35f)),
            element.TextOrigin.Y + (int)MathF.Round(start.Top + (start.Height / 2f)));
    }

    private static void Click(TextBox textBox, Point point)
    {
        textBox.RaiseMouseDown(Mouse(point));
        textBox.RaiseMouseUp(Mouse(point));
    }

    private static void Click(Control control, Point point)
    {
        control.RaiseMouseDown(Mouse(point));
        control.RaiseMouseUp(Mouse(point));
    }

    private static MouseEventArgs Mouse(
        Point point,
        Keys modifiers = Keys.None,
        int clicks = 1,
        int pointerId = 0)
        => new(
            MouseButtons.Left,
            clicks,
            point.X,
            point.Y,
            Point.Empty,
            null,
            null,
            modifiers,
            pointerId,
            PointerDeviceKind.Mouse);

    private static RippleEffect CreateRipple()
        => new()
        {
            Duration = TimeSpan.FromMilliseconds(100),
            Easing = Easings.Linear
        };

    private sealed class CaretProbeEffect(Func<int> getCursor) : InteractionEffect
    {
        public int CursorAtPointerDown { get; private set; } = -1;

        protected override void OnPointerDown(MouseEventArgs e)
            => CursorAtPointerDown = getCursor();
    }

    private sealed class PointerTextBox : TextBox
    {
        public void CancelPointerForTest(int pointerId) => CancelPointerInteraction(pointerId);
        public void LoseFocusForTest() => OnLostFocus(EventArgs.Empty);
        public void MoveForTest(MouseEventArgs e) => OnMouseMove(e);
    }

    private sealed class VisiblePanel : Panel
    {
        public override bool Visible
        {
            get => true;
            set => base.Visible = value;
        }
    }

    private sealed class VisibleDataGridView : DataGridView
    {
        public override bool Visible
        {
            get => true;
            set => base.Visible = value;
        }
    }
}
