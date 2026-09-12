namespace ModernFormsNext.WindowKit.Platform.Accessibility;

// Optional projection of the existing document. This is an in-process backend contract,
// deliberately not part of the bounded automation IPC protocol or a second semantic tree.
internal interface IPlatformAccessibilityText
{
    IPlatformAccessibleTextProvider? TextProvider { get; }
}

internal enum PlatformTextAttributeSentinel { NotSupported, Mixed }

internal interface IPlatformAccessibleTextProvider
{
    IPlatformAccessibleObject Owner { get; }
    IPlatformAccessibleTextRange DocumentRange { get; }
    bool SupportsSelection { get; }
    IPlatformAccessibleTextRange RangeFromOffsets(int start, int end);
    bool SetSelection(int anchor, int caret);
    IPlatformAccessibleTextRange[] GetSelection();
    IPlatformAccessibleTextRange[] GetVisibleRanges();
    IPlatformAccessibleTextRange RangeFromPoint(Point point);
    IPlatformAccessibleTextRange RangeFromChild(IPlatformAccessibleObject child);
    IPlatformAccessibleTextRange GetCaretRange(out bool active);
}

internal interface IPlatformAccessibleTextRange
{
    int Start { get; }
    int End { get; }
    IPlatformAccessibleTextRange Clone();
    bool Compare(IPlatformAccessibleTextRange other);
    int CompareEndpoints(int endpoint, IPlatformAccessibleTextRange other, int otherEndpoint);
    void ExpandToEnclosingUnit(int unit);
    IPlatformAccessibleTextRange? FindAttribute(int attribute, object value, bool backward);
    IPlatformAccessibleTextRange? FindText(string text, bool backward, bool ignoreCase);
    object GetAttributeValue(int attribute);
    Rect[] GetBoundingRectangles();
    IPlatformAccessibleObject[] GetChildren();
    IPlatformAccessibleObject GetEnclosingElement();
    string GetText(int maximumLength);
    int Move(int unit, int count);
    int MoveEndpointByUnit(int endpoint, int unit, int count);
    void MoveEndpointByRange(int endpoint, IPlatformAccessibleTextRange other, int otherEndpoint);
    bool Select();
    bool AddToSelection();
    bool RemoveFromSelection();
    bool ScrollIntoView(bool alignToTop);
}

internal static class PlatformAccessibilityTextExtensions
{
    internal static IPlatformAccessibleTextProvider? GetTextProvider(this IPlatformAccessibleObject node)
    {
        if (PlatformAccessibilityPrivacy.HasSensitiveAncestor(node)) return null;
        var provider = (node as IPlatformAccessibilityText)?.TextProvider;
        return PlatformAccessibilityPrivacy.HasSensitiveAncestor(node) ? null : provider;
    }
}
