using System.Drawing;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.Accessibility;

internal sealed partial class PlatformAccessibleObjectAdapter : IPlatformAccessibilityText
{
    private TextProviderAdapter? textAdapter;
    public IPlatformAccessibleTextProvider? TextProvider
    {
        get
        {
            if (IsSensitive || accessible_object.TextProvider is not { } provider || IsSensitive)
                return null;
            if (textAdapter is null || !ReferenceEquals(textAdapter.Source, provider)) textAdapter = new(provider);
            return textAdapter;
        }
    }

    private sealed class TextProviderAdapter(AccessibleTextProvider source) : IPlatformAccessibleTextProvider
    {
        internal AccessibleTextProvider Source => source;
        public IPlatformAccessibleObject Owner => From(source.Owner)!;
        public IPlatformAccessibleTextRange DocumentRange => new TextRangeAdapter(source.DocumentRange);
        public bool SupportsSelection => source.SupportsSelection;
        public IPlatformAccessibleTextRange RangeFromOffsets(int start, int end) => new TextRangeAdapter(source.RangeFromOffsets(start, end));
        public bool SetSelection(int anchor, int caret) => source.SetSelection(anchor, caret);
        public IPlatformAccessibleTextRange[] GetSelection() => source.GetSelection().Select(r => (IPlatformAccessibleTextRange)new TextRangeAdapter(r)).ToArray();
        public IPlatformAccessibleTextRange[] GetVisibleRanges() => source.GetVisibleRanges().Select(r => (IPlatformAccessibleTextRange)new TextRangeAdapter(r)).ToArray();
        public IPlatformAccessibleTextRange RangeFromPoint(WindowKit.Point point) => new TextRangeAdapter(source.RangeFromPoint(new PointF((float)point.X, (float)point.Y)));
        public IPlatformAccessibleTextRange RangeFromChild(IPlatformAccessibleObject child)
            => child is PlatformAccessibleObjectAdapter adapter ? new TextRangeAdapter(source.RangeFromChild(adapter.accessible_object))
                : throw new ArgumentException("The child belongs to another accessibility provider.", nameof(child));
        public IPlatformAccessibleTextRange GetCaretRange(out bool active) => new TextRangeAdapter(source.GetCaretRange(out active));
    }

    private sealed class TextRangeAdapter(AccessibleTextRange source) : IPlatformAccessibleTextRange
    {
        private static AccessibleTextRange Other(IPlatformAccessibleTextRange value) => value is TextRangeAdapter adapter
            ? adapter.Source : throw new ArgumentException("The range belongs to another provider.", nameof(value));
        private AccessibleTextRange Source => source;
        private static IPlatformAccessibleTextRange? Wrap(AccessibleTextRange? value) => value is null ? null : new TextRangeAdapter(value);
        public int Start => source.Start;
        public int End => source.End;
        public IPlatformAccessibleTextRange Clone() => new TextRangeAdapter(source.Clone());
        public bool Compare(IPlatformAccessibleTextRange other) => source.Compare(Other(other));
        public int CompareEndpoints(int endpoint, IPlatformAccessibleTextRange other, int otherEndpoint)
            => source.CompareEndpoints((AccessibleTextEndpoint)endpoint, Other(other), (AccessibleTextEndpoint)otherEndpoint);
        public void ExpandToEnclosingUnit(int unit) => source.ExpandToEnclosingUnit((AccessibleTextUnit)unit);
        public IPlatformAccessibleTextRange? FindAttribute(int attribute, object value, bool backward)
            => Wrap(source.FindAttribute((AccessibleTextAttribute)attribute, value, backward));
        public IPlatformAccessibleTextRange? FindText(string text, bool backward, bool ignoreCase) => Wrap(source.FindText(text, backward, ignoreCase));
        public object GetAttributeValue(int attribute)
        {
            var value = source.GetAttributeValue((AccessibleTextAttribute)attribute);
            return ReferenceEquals(value, AccessibleTextAttributeValues.Mixed) ? PlatformTextAttributeSentinel.Mixed
                : ReferenceEquals(value, AccessibleTextAttributeValues.NotSupported) ? PlatformTextAttributeSentinel.NotSupported : value;
        }
        public Rect[] GetBoundingRectangles() => source.GetBoundingRectangles().Select(r => new Rect(r.X, r.Y, r.Width, r.Height)).ToArray();
        public IPlatformAccessibleObject[] GetChildren() => source.GetChildren().Select(c => From(c)!).ToArray();
        public IPlatformAccessibleObject GetEnclosingElement() => From(source.GetEnclosingElement())!;
        public string GetText(int maximumLength) => source.GetText(maximumLength);
        public int Move(int unit, int count) => source.Move((AccessibleTextUnit)unit, count);
        public int MoveEndpointByUnit(int endpoint, int unit, int count) => source.MoveEndpointByUnit((AccessibleTextEndpoint)endpoint, (AccessibleTextUnit)unit, count);
        public void MoveEndpointByRange(int endpoint, IPlatformAccessibleTextRange other, int otherEndpoint)
            => source.MoveEndpointByRange((AccessibleTextEndpoint)endpoint, Other(other), (AccessibleTextEndpoint)otherEndpoint);
        public bool Select() => source.Select();
        public bool AddToSelection() => source.AddToSelection();
        public bool RemoveFromSelection() => source.RemoveFromSelection();
        public bool ScrollIntoView(bool alignToTop) => source.ScrollIntoView(alignToTop);
    }
}
