using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ModernFormsNext.Accessibility;

internal sealed class TextBoxAccessibleTextRange : AccessibleTextRange
{
    private readonly TextBoxAccessibleTextProvider provider;
    private readonly long generation;
    private long revision;
    private int start;
    private int end;

    internal TextBoxAccessibleTextRange(TextBoxAccessibleTextProvider provider, int start, int end)
    {
        this.provider = provider;
        var owner = provider.Read();
        var journal = owner.document.AccessibilityEdits;
        generation = journal.Generation;
        revision = journal.Revision;
        this.start = TextBoxDocument.NormalizeUtf16Boundary(owner.document.Text, Math.Min(start, end), false);
        this.end = TextBoxDocument.NormalizeUtf16Boundary(owner.document.Text, Math.Max(start, end), true);
        if (start == end) this.end = this.start;
    }

    private TextBox Read()
    {
        var owner = provider.Read();
        owner.document.AccessibilityEdits.Rebase(generation, ref revision, ref start, ref end);
        return owner;
    }

    public override int Start { get { _ = Read(); return start; } }
    public override int End { get { _ = Read(); return end; } }
    internal void SetEnd(int value) { _ = Read(); end = value; }
    public override AccessibleTextProvider Provider { get { _ = Read(); return provider; } }
    public override AccessibleTextRange Clone() { _ = Read(); return provider.Range(start, end); }

    private TextBoxAccessibleTextRange Other(AccessibleTextRange other)
    {
        _ = Read();
        if (other is not TextBoxAccessibleTextRange range || !ReferenceEquals(provider, range.provider))
            throw new ArgumentException("Text ranges must belong to the same provider.", nameof(other));
        _ = range.Read();
        return range;
    }

    private int Endpoint(AccessibleTextEndpoint endpoint) => endpoint switch {
        AccessibleTextEndpoint.Start => start,
        AccessibleTextEndpoint.End => end,
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
    };

    private void SetEndpoint(AccessibleTextEndpoint endpoint, int value)
    {
        if (endpoint == AccessibleTextEndpoint.Start) { start = value; end = Math.Max(end, start); }
        else if (endpoint == AccessibleTextEndpoint.End) { end = value; start = Math.Min(start, end); }
        else throw new ArgumentOutOfRangeException(nameof(endpoint));
    }

    public override bool Compare(AccessibleTextRange other)
    {
        var range = Other(other);
        return start == range.start && end == range.end;
    }

    public override int CompareEndpoints(AccessibleTextEndpoint endpoint, AccessibleTextRange other, AccessibleTextEndpoint otherEndpoint)
    {
        var range = Other(other);
        return Endpoint(endpoint).CompareTo(range.Endpoint(otherEndpoint));
    }

    public override void MoveEndpointByRange(AccessibleTextEndpoint endpoint, AccessibleTextRange other, AccessibleTextEndpoint otherEndpoint)
    {
        var range = Other(other);
        _ = Endpoint(endpoint);
        SetEndpoint(endpoint, range.Endpoint(otherEndpoint));
    }

    private static int EnclosingIndex(int[] boundaries, int position)
    {
        int index = Array.BinarySearch(boundaries, position);
        if (index < 0) index = ~index - 1;
        return Math.Clamp(index, 0, Math.Max(0, boundaries.Length - 2));
    }

    public override void ExpandToEnclosingUnit(AccessibleTextUnit unit)
    {
        var boundaries = provider.Boundaries(Read(), unit);
        int index = EnclosingIndex(boundaries, start);
        start = boundaries[index];
        end = boundaries[Math.Min(index + 1, boundaries.Length - 1)];
    }

    public override int Move(AccessibleTextUnit unit, int count)
    {
        var boundaries = provider.Boundaries(Read(), unit);
        if (count == 0 || boundaries.Length < 2) return 0;
        if (start == end) {
            int moved = MoveEndpointByUnit(AccessibleTextEndpoint.Start, unit, count);
            end = start;
            return moved;
        }
        int index = EnclosingIndex(boundaries, start);
        int target = (int)Math.Clamp((long)index + count, 0, boundaries.Length - 2);
        start = boundaries[target];
        end = boundaries[target + 1];
        return target - index;
    }

    public override int MoveEndpointByUnit(AccessibleTextEndpoint endpoint, AccessibleTextUnit unit, int count)
    {
        var boundaries = provider.Boundaries(Read(), unit);
        int position = Endpoint(endpoint);
        if (count == 0) return 0;
        int found = Array.BinarySearch(boundaries, position);
        long target;
        int origin;
        if (found >= 0) { origin = found; target = (long)found + count; }
        else {
            int insertion = ~found;
            origin = count > 0 ? insertion - 1 : insertion;
            target = (long)origin + count;
        }
        int actual = (int)Math.Clamp(target, 0, boundaries.Length - 1);
        SetEndpoint(endpoint, boundaries[actual]);
        return actual - origin;
    }

    public override string GetText(int maximumLength = -1)
    {
        var owner = Read();
        if (maximumLength < -1) throw new ArgumentOutOfRangeException(nameof(maximumLength));
        int length = maximumLength == -1 ? end - start : Math.Min(end - start, maximumLength);
        int until = TextBoxDocument.NormalizeUtf16Boundary(owner.document.Text, start + length, false);
        return owner.document.Text.Substring(start, until - start);
    }

    public override AccessibleTextRange? FindText(string text, bool backward = false, bool ignoreCase = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var owner = Read();
        if (text.Length > end - start) return null;
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        int found = backward ? owner.document.Text.LastIndexOf(text, end - 1, end - start, comparison)
            : owner.document.Text.IndexOf(text, start, end - start, comparison);
        return found < 0 ? null : provider.Range(found, found + text.Length);
    }

    private IEnumerable<(int Start, int End)> AttributeSpans(TextBox owner)
    {
        if (start == end) { yield return (start, end); yield break; }
        var boundaries = provider.Boundaries(owner, AccessibleTextUnit.Format);
        int cursor = start;
        foreach (int boundary in boundaries) {
            if (boundary <= cursor) continue;
            int next = Math.Min(boundary, end);
            yield return (cursor, next);
            cursor = next;
            if (cursor >= end) yield break;
        }
    }

    public override object GetAttributeValue(AccessibleTextAttribute attribute)
    {
        var owner = Read();
        object? value = null;
        foreach (var span in AttributeSpans(owner)) {
            var current = provider.Attribute(owner, span.Start, attribute);
            if (value is not null && !Equals(value, current)) return AccessibleTextAttributeValues.Mixed;
            value = current;
        }
        return value ?? AccessibleTextAttributeValues.NotSupported;
    }

    public override AccessibleTextRange? FindAttribute(AccessibleTextAttribute attribute, object value, bool backward = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        var owner = Read();
        var matches = new List<(int Start, int End)>();
        foreach (var span in AttributeSpans(owner)) {
            if (!MatchesAttribute(attribute, provider.Attribute(owner, span.Start, attribute), value)) continue;
            if (matches.Count > 0 && matches[^1].End == span.Start) matches[^1] = (matches[^1].Start, span.End);
            else matches.Add(span);
        }
        if (matches.Count == 0) return null;
        var result = backward ? matches[^1] : matches[0];
        return provider.Range(result.Start, result.End);
    }

    private static bool MatchesAttribute(AccessibleTextAttribute attribute, object actual, object requested)
    {
        if (Equals(actual, requested)) return true;
        if (attribute != AccessibleTextAttribute.FontSize || actual is not double size || requested is not double expected
            || !double.IsFinite(size) || !double.IsFinite(expected)) return false;
        // A native adapter's logical-pixel/point roundtrip can differ by a few binary units
        // even when the caller searches for the exact size it just read. Do not introduce a
        // visible font-size tolerance or approximate comparisons for any other attribute.
        double unit = Math.Max(Math.Abs(Math.BitIncrement(size) - size), Math.Abs(Math.BitIncrement(expected) - expected));
        return Math.Abs(size - expected) <= 4 * unit;
    }

    public override IReadOnlyList<RectangleF> GetBoundingRectangles()
    {
        var owner = Read();
        // UIA baseline ranges report no rectangles for a degenerate insertion point.
        // Caret geometry remains available internally for explicit range scrolling.
        if (start == end) return [];
        var result = new List<RectangleF>();
        foreach (var part in TextBoxAccessibleTextProvider.Parts(owner, start, end, clip: true)) {
            var rectangle = TextBoxAccessibleTextProvider.ScreenBounds(owner, part.Rectangle);
            if (rectangle.Width > 0 && rectangle.Height > 0) result.Add(rectangle);
        }
        return result;
    }

    public override IReadOnlyList<AccessibleObject> GetChildren() { _ = Read(); return []; }
    public override bool Select() => Read().SetAccessibleTextSelection(start, end);

    public override bool AddToSelection()
    {
        var owner = Read();
        var selection = TextBoxAccessibleTextProvider.Selection(owner);
        if (start == end) return owner.SetAccessibleTextSelection(start, end);
        if (selection.Start == selection.End) return owner.SetAccessibleTextSelection(start, end);
        if (end < selection.Start || start > selection.End) return false;
        return owner.SetAccessibleTextSelection(Math.Min(start, selection.Start), Math.Max(end, selection.End));
    }

    public override bool RemoveFromSelection()
    {
        var owner = Read();
        var selection = TextBoxAccessibleTextProvider.Selection(owner);
        if (start == end) return owner.SetAccessibleTextSelection(start, end);
        if (end <= selection.Start || start >= selection.End) return true;
        if (start > selection.Start && end < selection.End) return false;
        if (start <= selection.Start && end >= selection.End) return owner.SetAccessibleTextSelection(start, start);
        return start <= selection.Start ? owner.SetAccessibleTextSelection(end, selection.End)
            : owner.SetAccessibleTextSelection(selection.Start, start);
    }

    public override bool ScrollIntoView(bool alignToTop = true)
    {
        var owner = Read();
        var lifetime = new AccessibilityControlLifetime(owner);
        var parts = TextBoxAccessibleTextProvider.Parts(owner, start, end, clip: false);
        if (parts.Count != 0) {
            var rectangle = alignToTop ? parts[0].Rectangle : parts[^1].Rectangle;
            if (!owner.ScrollAccessibleText(rectangle, alignToTop)) return false;
        }
        if (!lifetime.IsCurrent || !owner.Enabled || !owner.Visible) return false;
        var node = owner.AccessibilityObject;
        if (TextBoxAccessibleTextProvider.HasSensitiveAncestor(node)) return false;
        // Reveal the existing editor through the same ancestor-scroll action used by native
        // ScrollItem. It preserves selection/focus and rejects reparenting during callbacks.
        bool reveal = (node.SupportedActions & AccessibleActions.ScrollIntoView) != 0;
        if (!lifetime.IsCurrent) return false;
        return (!reveal || node.PerformAction(AccessibleActions.ScrollIntoView)) && lifetime.IsCurrent;
    }
}
