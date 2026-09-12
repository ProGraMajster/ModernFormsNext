using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SkiaSharp;
using Topten.RichTextKit;

namespace ModernFormsNext.Accessibility;

internal sealed class TextBoxAccessibleTextProvider : AccessibleTextProvider
{
    private readonly WeakReference<TextBox> editor;
    private readonly WeakReference<AccessibleObject> peer;

    internal TextBoxAccessibleTextProvider(TextBox editor, AccessibleObject peer)
    {
        this.editor = new(editor);
        this.peer = new(peer);
    }

    internal TextBox Read()
    {
        if (!editor.TryGetTarget(out var owner) || owner.IsDisposed || owner.Disposing)
            throw new ObjectDisposedException(nameof(AccessibleTextProvider));
        owner.VerifyAccessibleTextAccess();
        if (!peer.TryGetTarget(out var node)) throw new ObjectDisposedException(nameof(AccessibleTextProvider));
        if (owner.IsAccessibilitySensitive || HasSensitiveAncestor(node))
            throw new UnauthorizedAccessException("Sensitive text is not available through accessibility.");
        if (owner.IsDisposed || owner.Disposing) throw new ObjectDisposedException(nameof(AccessibleTextProvider));
        if (owner.IsAccessibilitySensitive)
            throw new UnauthorizedAccessException("Sensitive text is not available through accessibility.");
        return owner;
    }

    internal static bool HasSensitiveAncestor(AccessibleObject node)
    {
        int remaining = 512;
        for (AccessibleObject? current = node; current is not null; current = current.Parent) {
            if (current is Control.ControlAccessibleObject { Owner: TextBox { IsAccessibilitySensitive: true } }
                || current.IsSensitive || (current.State & AccessibleStates.Protected) != 0) return true;
            if (--remaining == 0) return true;
        }
        return false;
    }

    public override AccessibleObject Owner
    {
        get { _ = Read(); return peer.TryGetTarget(out var result) ? result : throw new ObjectDisposedException(nameof(AccessibleTextProvider)); }
    }

    internal TextBoxAccessibleTextRange Range(int start, int end) => new(this, start, end);
    public override AccessibleTextRange DocumentRange => Range(0, Read().document.Text.Length);
    public override AccessibleTextRange RangeFromOffsets(int start, int end)
    {
        int length = Read().document.Text.Length;
        if (start < 0 || start > length) throw new ArgumentOutOfRangeException(nameof(start));
        if (end < 0 || end > length) throw new ArgumentOutOfRangeException(nameof(end));
        return Range(start, end);
    }
    public override bool SetSelection(int anchor, int caret)
    {
        var owner = Read();
        if (anchor < 0 || anchor > owner.document.Text.Length) throw new ArgumentOutOfRangeException(nameof(anchor));
        if (caret < 0 || caret > owner.document.Text.Length) throw new ArgumentOutOfRangeException(nameof(caret));
        return owner.SetAccessibleTextSelection(anchor, caret);
    }

    internal static (int Start, int End) Selection(TextBox owner)
    {
        var document = owner.document;
        int start = document.SelectionStart >= 0 ? document.SelectionStart : document.CursorIndex;
        int end = document.SelectionEnd >= 0 ? document.SelectionEnd : document.CursorIndex;
        return (Math.Clamp(Math.Min(start, end), 0, document.Text.Length), Math.Clamp(Math.Max(start, end), 0, document.Text.Length));
    }

    public override IReadOnlyList<AccessibleTextRange> GetSelection()
    {
        var selection = Selection(Read());
        return [Range(selection.Start, selection.End)];
    }

    public override AccessibleTextRange GetCaretRange(out bool isActive)
    {
        var owner = Read();
        isActive = owner.Focused && owner.IsTextInputHostActive;
        return Range(owner.document.CursorIndex, owner.document.CursorIndex);
    }

    public override IReadOnlyList<AccessibleTextRange> GetVisibleRanges()
    {
        var owner = Read();
        if (!owner.Visible) return [Range(owner.document.CursorIndex, owner.document.CursorIndex)];
        var parts = Parts(owner, 0, owner.document.Text.Length, clip: true);
        var result = new List<AccessibleTextRange>();
        foreach (var part in parts.OrderBy(part => part.Start)) {
            var visible = ScreenBounds(owner, part.Rectangle);
            if (visible.Width <= 0 || visible.Height <= 0) continue;
            if (result.LastOrDefault() is TextBoxAccessibleTextRange previous && previous.End == part.Start)
                previous.SetEnd(part.End);
            else result.Add(Range(part.Start, part.End));
        }
        return result.Count == 0 ? [Range(owner.document.CursorIndex, owner.document.CursorIndex)] : result;
    }

    public override AccessibleTextRange RangeFromPoint(PointF screenPoint)
    {
        if (!float.IsFinite(screenPoint.X) || !float.IsFinite(screenPoint.Y))
            throw new ArgumentOutOfRangeException(nameof(screenPoint));
        var owner = Read();
        var local = FromScreen(owner, screenPoint);
        int index = owner.AccessibleTextHitTest(Point.Round(local));
        return Range(index, index);
    }

    internal int[] Boundaries(TextBox owner, AccessibleTextUnit unit)
    {
        if (!Enum.IsDefined(unit)) throw new ArgumentOutOfRangeException(nameof(unit));
        var document = owner.document;
        int length = document.Text.Length;
        var block = owner.AccessibleTextLayout;
        _ = block.MeasuredHeight;
        var result = new SortedSet<int> { 0, length };
        IEnumerable<int> indices = unit switch {
            AccessibleTextUnit.Character => block.CaretIndicies,
            AccessibleTextUnit.Word => block.WordBoundaryIndicies,
            AccessibleTextUnit.Line => block.Lines.Select(line => line.Start),
            AccessibleTextUnit.Format => block.FontRuns.SelectMany(run => new[] { run.Start, run.End }),
            _ => []
        };
        foreach (int index in indices) result.Add(document.GetUtf16IndexFromLayoutCodePointIndex(index));
        if (unit == AccessibleTextUnit.Paragraph) {
            string text = document.Text;
            for (int i = 0; i < text.Length; i++) {
                if (text[i] == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') i++; result.Add(i + 1); }
                else if (text[i] == '\n') result.Add(i + 1);
            }
        }
        return result.ToArray();
    }

    internal static IReadOnlyList<Part> Parts(TextBox owner, int start, int end, bool clip)
    {
        var result = new List<Part>();
        if (clip && !owner.Visible) return result;
        var block = owner.AccessibleTextLayout;
        _ = block.MeasuredHeight;
        var origin = owner.GetTextOrigin(block);
        RectangleF viewport = clip ? VisibleViewport(owner) : owner.AccessibleTextViewport;
        var document = owner.document;
        int first = document.GetLayoutCodePointIndex(start), last = document.GetLayoutCodePointIndex(end);
        if (start == end) {
            var caret = TextMeasurer.GetCursorLocation(block, origin, first, owner.CurrentFontSize);
            var rect = new RectangleF(caret.X, caret.Y, Math.Max(1, caret.Width), Math.Max(1, caret.Height));
            if (clip) rect = RectangleF.Intersect(rect, viewport);
            if (rect.Width > 0 && rect.Height > 0) result.Add(new(start, end, rect));
            return result;
        }
        foreach (var line in block.Lines) {
            foreach (var run in line.Runs) {
                int from = Math.Max(first, run.Start), to = Math.Min(last, run.End);
                if (to <= from) continue;
                float x1 = run.GetXCoordOfCodePointIndex(from), x2 = run.GetXCoordOfCodePointIndex(to);
                var rect = new RectangleF(origin.X + Math.Min(x1, x2), origin.Y + line.YCoord,
                    Math.Max(1, Math.Abs(x2 - x1)), Math.Max(1, line.Height));
                if (clip) rect = RectangleF.Intersect(rect, viewport);
                if (rect.Width <= 0 || rect.Height <= 0) continue;
                int visibleStart = from, visibleEnd = to;
                if (clip) {
                    // The line hit test supplies logical cluster positions even for visual RTL runs.
                    var left = line.HitTest(rect.Left - origin.X).ClosestCodePointIndex;
                    var right = line.HitTest(rect.Right - origin.X).ClosestCodePointIndex;
                    visibleStart = Math.Clamp(Math.Min(left, right), from, to);
                    visibleEnd = Math.Clamp(Math.Max(left, right), from, to);
                    if (visibleStart == visibleEnd) { visibleStart = from; visibleEnd = to; }
                }
                result.Add(new(document.GetUtf16IndexFromLayoutCodePointIndex(visibleStart),
                    document.GetUtf16IndexFromLayoutCodePointIndex(visibleEnd), rect));
            }
        }
        return result;
    }

    internal readonly record struct Part(int Start, int End, RectangleF Rectangle);

    private static RectangleF VisibleViewport(TextBox owner)
    {
        RectangleF viewport = owner.AccessibleTextViewport;
        // Clip before translating run extents into logical offsets. Merely clipping the
        // returned screen rectangle would still expose off-viewport text in GetVisibleRanges.
        for (Control? parent = owner.Parent; parent is not null; parent = parent.Parent) {
            if (!parent.Visible) return RectangleF.Empty;
            var bounds = parent.AccessibilityObject.Bounds;
            var points = new[] { FromScreen(owner, new(bounds.Left, bounds.Top)), FromScreen(owner, new(bounds.Right, bounds.Top)),
                FromScreen(owner, new(bounds.Left, bounds.Bottom)), FromScreen(owner, new(bounds.Right, bounds.Bottom)) };
            viewport = RectangleF.Intersect(viewport, RectangleF.FromLTRB(points.Min(p => p.X), points.Min(p => p.Y),
                points.Max(p => p.X), points.Max(p => p.Y)));
        }
        return viewport;
    }

    internal static RectangleF ScreenBounds(TextBox owner, RectangleF local)
    {
        var points = new[] { ToScreen(owner, local.Location), ToScreen(owner, new(local.Right, local.Top)),
            ToScreen(owner, new(local.Left, local.Bottom)), ToScreen(owner, new(local.Right, local.Bottom)) };
        var bounds = RectangleF.FromLTRB(points.Min(point => point.X), points.Min(point => point.Y),
            points.Max(point => point.X), points.Max(point => point.Y));
        for (Control? parent = owner.Parent; parent is not null; parent = parent.Parent) {
            if (!parent.Visible) return RectangleF.Empty;
            bounds = RectangleF.Intersect(bounds, parent.AccessibilityObject.Bounds);
        }
        return bounds;
    }

    private static PointF ToScreen(TextBox owner, PointF local)
    {
        var origin = owner.PointToScreen(Point.Empty);
        var x = owner.PointToScreen(new(1024, 0));
        var y = owner.PointToScreen(new(0, 1024));
        return new(origin.X + ((x.X - origin.X) * local.X + (y.X - origin.X) * local.Y) / 1024,
            origin.Y + ((x.Y - origin.Y) * local.X + (y.Y - origin.Y) * local.Y) / 1024);
    }

    private static PointF FromScreen(TextBox owner, PointF screen)
    {
        var origin = ToScreen(owner, PointF.Empty);
        var x = ToScreen(owner, new(1024, 0));
        var y = ToScreen(owner, new(0, 1024));
        double ax = (x.X - origin.X) / 1024d, ay = (x.Y - origin.Y) / 1024d;
        double bx = (y.X - origin.X) / 1024d, by = (y.Y - origin.Y) / 1024d;
        double determinant = ax * by - ay * bx;
        if (Math.Abs(determinant) < 1e-12) throw new InvalidOperationException("The editor's presentation transform is not invertible.");
        double dx = screen.X - origin.X, dy = screen.Y - origin.Y;
        return new((float)((dx * by - dy * bx) / determinant), (float)((dy * ax - dx * ay) / determinant));
    }

    internal object Attribute(TextBox owner, int offset, AccessibleTextAttribute attribute)
    {
        if (!Enum.IsDefined(attribute)) return AccessibleTextAttributeValues.NotSupported;
        if (attribute == AccessibleTextAttribute.IsReadOnly) return owner.ReadOnly;
        if (attribute == AccessibleTextAttribute.IsHidden) return !owner.Visible;
        var block = owner.AccessibleTextLayout;
        _ = block.MeasuredHeight;
        int index = owner.document.GetLayoutCodePointIndex(Math.Min(offset, Math.Max(0, owner.document.Text.Length - 1)));
        var style = block.FontRuns.FirstOrDefault(run => run.Start <= index && run.End > index)?.Style;
        string family = style?.FontFamily ?? owner.CurrentStyle.GetFont().FamilyName;
        return attribute switch {
            AccessibleTextAttribute.FontName => family,
            AccessibleTextAttribute.FontSize => (double)(style?.FontSize ?? owner.CurrentFontSize) / owner.ScaleFactor.Height,
            AccessibleTextAttribute.FontWeight => style?.FontWeight ?? (int)owner.CurrentStyle.GetFont().FontWeight,
            AccessibleTextAttribute.Italic => style?.FontItalic ?? owner.CurrentStyle.GetFontStyle().HasFlag(FontStyle.Italic),
            AccessibleTextAttribute.Underline => style is not null ? style.Underline != UnderlineStyle.None : owner.CurrentStyle.GetFontStyle().HasFlag(FontStyle.Underline),
            AccessibleTextAttribute.Strikethrough => style is not null ? style.StrikeThrough != StrikeThroughStyle.None : owner.CurrentStyle.GetFontStyle().HasFlag(FontStyle.Strikeout),
            AccessibleTextAttribute.ForegroundColor => unchecked((int)(uint)(style?.TextColor ?? owner.CurrentStyle.GetForegroundColor())),
            AccessibleTextAttribute.BackgroundColor => unchecked((int)(uint)(style is null || style.BackgroundColor == SKColor.Empty ? owner.CurrentStyle.GetBackgroundColor() : style.BackgroundColor)),
            _ => AccessibleTextAttributeValues.NotSupported
        };
    }
}
