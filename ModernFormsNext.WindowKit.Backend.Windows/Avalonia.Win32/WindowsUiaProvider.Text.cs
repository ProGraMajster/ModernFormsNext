using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

internal partial class WindowsUiaProvider : ITextProvider2Abi
{
    internal T ReadText<T>(Func<IPlatformAccessibleTextProvider, T> callback)
        => Read(node => {
            if (node.GetIsSensitive()) throw new WindowsUiaAccessDeniedException();
            var text = node.GetTextProvider() ?? throw new WindowsUiaElementNotAvailableException("The document no longer exposes text.");
            var result = callback(text);
            if (node.GetIsSensitive()) throw new WindowsUiaAccessDeniedException();
            if (!ReferenceEquals(text, node.GetTextProvider()))
                throw new WindowsUiaElementNotAvailableException("The document text provider changed during the request.");
            return result;
        });
    internal WindowsTextRangeProvider WrapTextRange(IPlatformAccessibleTextRange range) => new(this, range);
    internal WindowsUiaProvider TextElement(IPlatformAccessibleObject node) => Context.GetOrCreate(node);
    internal void ValidateTextMutation()
        => ReadText(provider => {
            if ((provider.Owner.State & StateUnavailable) != 0) throw new WindowsUiaElementNotEnabledException();
            return true;
        });
    internal WindowsTextRangeProvider TextDocumentRange => ReadText(p => WrapTextRange(p.DocumentRange));
    internal WindowsTextRangeProvider[] TextSelection => ReadText(p => p.GetSelection().Select(WrapTextRange).ToArray());
    internal WindowsTextRangeProvider[] TextVisibleRanges => ReadText(p => p.GetVisibleRanges().Select(WrapTextRange).ToArray());

    public int AbiGetTextSelection(out IntPtr ranges) => TryGet(() => WindowsUiaTextNative.RangeArray(TextSelection), out ranges);
    public int AbiGetVisibleRanges(out IntPtr ranges) => TryGet(() => WindowsUiaTextNative.RangeArray(TextVisibleRanges), out ranges);
    public int AbiRangeFromChild(IntPtr child, out IntPtr range)
        => TryGet(() => WindowsUiaTextNative.RangePointer(ReadText(p => WrapTextRange(p.RangeFromChild(
            WindowsUiaTextNative.Borrow<WindowsUiaProvider>(child).PlatformObject)))), out range);
    public int AbiRangeFromPoint(UiaPoint point, out IntPtr range)
        => TryGet(() => WindowsUiaTextNative.RangePointer(ReadText(p => WrapTextRange(p.RangeFromPoint(new Point(point.X, point.Y))))), out range);
    public int AbiGetDocumentRange(out IntPtr range) => TryGet(() => WindowsUiaTextNative.RangePointer(TextDocumentRange), out range);
    public int AbiGetSupportedTextSelection(out int selection) => TryGet(() => ReadText(p => p.SupportsSelection ? 1 : 0), out selection);
    public int AbiRangeFromAnnotation(IntPtr annotation, out IntPtr range)
        => TryGet<IntPtr>(() => ReadText<IntPtr>(_ => throw new ArgumentException("This document has no annotation elements.")), out range);
    public int AbiGetCaretRange(out int active, out IntPtr range)
    {
        int localActive = 0;
        int result = TryGet(() => WindowsUiaTextNative.RangePointer(ReadText(p => {
            var caret = p.GetCaretRange(out bool focused);
            localActive = focused ? 1 : 0;
            return WrapTextRange(caret);
        })), out range);
        active = result == 0 ? localActive : 0;
        return result;
    }
}
