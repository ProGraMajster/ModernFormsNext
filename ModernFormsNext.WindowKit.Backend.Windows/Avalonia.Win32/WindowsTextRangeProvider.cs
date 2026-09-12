using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

// A range retains only the backend wrapper plus the canonical weak document anchors.
// Every call re-enters the owning dispatcher and validates the original fragment ancestry.
[GeneratedComClass]
internal sealed partial class WindowsTextRangeProvider(WindowsUiaProvider provider, IPlatformAccessibleTextRange range) : ITextRangeProviderAbi
{
    private readonly WindowsUiaProvider owner = provider;
    private readonly IPlatformAccessibleTextProvider source = provider.ReadText(p => p);
    internal T Read<T>(Func<IPlatformAccessibleTextRange, T> read) => owner.ReadText(current => {
        if (!ReferenceEquals(current, source)) throw new WindowsUiaElementNotAvailableException("The retained text provider was replaced.");
        return read(range);
    });
    internal string GetText(int maximumLength = -1) => Read(r => r.GetText(maximumLength));
    internal WindowsTextRangeProvider Clone() => Read(r => owner.WrapTextRange(r.Clone()));
    private IPlatformAccessibleTextRange Other(WindowsTextRangeProvider other)
    {
        if (!ReferenceEquals(owner, other.owner)) throw new ArgumentException("Ranges must belong to the same document.");
        return other.Read(r => r);
    }
    private WindowsTextRangeProvider? Wrap(IPlatformAccessibleTextRange? value) => value is null ? null : owner.WrapTextRange(value);
    private void Mutate(Func<IPlatformAccessibleTextRange, bool> action)
        => Read(r => { owner.ValidateTextMutation(); if (!action(r)) throw new InvalidOperationException("The document rejected this text operation."); return true; });

    // WindowKit uses canonical attribute ordinals; UIA IDs remain isolated here.
    private static int Attribute(int id) => id switch {
        40005 => 0, 40006 => 1, 40007 => 2, 40014 => 3, 40030 => 4, 40026 => 5,
        40008 => 6, 40001 => 7, 40015 => 8, 40013 => 9, _ => -1 };
    private static object ToNativeAttribute(int id, object value)
    {
        if (value is PlatformTextAttributeSentinel) return value;
        // Canonical layout sizes are logical pixels (96 per inch); UIA FontSize is points
        // (72 per inch), independent of the window's current rendering scale.
        if (id == 40006 && value is double size) return size * (72d / 96d);
        if (id is 40030 or 40026 && value is bool decorated) return decorated ? 1 : 0; // TextDecorationLineStyle.Single/None.
        if (id is 40008 or 40001 && value is int argb) return ((argb >> 16) & 255) | (argb & 65280) | ((argb & 255) << 16); // COLORREF, no alpha.
        return value;
    }
    private static object FromNativeAttribute(int id, object value)
    {
        if (id == 40006 && value is double size) return size * (96d / 72d);
        if (id is 40030 or 40026 && value is int style)
            return style is 0 or 1 ? style != 0 : PlatformTextAttributeSentinel.NotSupported;
        return value;
    }
    internal object GetAttributeValue(int id) => Read(r => {
        int attribute = Attribute(id);
        if (attribute < 0) return PlatformTextAttributeSentinel.NotSupported;
        var value = ToNativeAttribute(id, r.GetAttributeValue(attribute));
        if (id is not (40008 or 40001) || value is not PlatformTextAttributeSentinel.Mixed) return value;
        object? common = null;
        foreach (var span in ColorSpans(r, id)) {
            if (common is not null && !Equals(common, span.Value)) return PlatformTextAttributeSentinel.Mixed;
            common = span.Value;
        }
        return common ?? value;
    });

    // UIA COLORREF uses 0x00bbggrr; there is no invertible mapping back to authored ARGB.
    // Compare projected values over existing canonical Format units, not copied text or a
    // second formatting model. Alpha-only differences therefore neither produce native Mixed
    // nor prevent FindAttribute from finding the exact COLORREF previously returned to UIA.
    // https://learn.microsoft.com/windows/win32/gdi/colorref
    private static IEnumerable<(IPlatformAccessibleTextRange Range, object Value)> ColorSpans(IPlatformAccessibleTextRange source, int id)
    {
        var cursor = source.Clone();
        cursor.MoveEndpointByRange(1, cursor, 0);
        int position = cursor.Start, end = source.End;
        if (position == end) {
            yield return (cursor, ToNativeAttribute(id, cursor.GetAttributeValue(Attribute(id))));
            yield break;
        }
        while (position < end) {
            var span = cursor.Clone();
            span.ExpandToEnclosingUnit(1); // The shared Format unit already follows rendered runs.
            span.MoveEndpointByRange(0, cursor, 0);
            if (span.End > end) span.MoveEndpointByRange(1, source, 1);
            int next = span.End;
            if (next <= position) throw new InvalidOperationException("The text format range did not advance.");
            yield return (span, ToNativeAttribute(id, span.GetAttributeValue(Attribute(id))));
            cursor.MoveEndpointByRange(0, span, 1);
            cursor.MoveEndpointByRange(1, cursor, 0);
            position = next;
        }
    }

    private WindowsTextRangeProvider? FindNativeAttribute(IPlatformAccessibleTextRange source, int id, object value, bool backward)
    {
        if (Attribute(id) < 0) return null;
        if (id is not (40008 or 40001)) return Wrap(source.FindAttribute(Attribute(id), FromNativeAttribute(id, value), backward));
        if (value is not int color || (color & unchecked((int)0xff000000)) != 0) return null;
        IPlatformAccessibleTextRange? match = null, last = null;
        foreach (var span in ColorSpans(source, id)) {
            if (Equals(span.Value, color)) {
                if (match is null) match = span.Range.Clone();
                else match.MoveEndpointByRange(1, span.Range, 1);
            }
            else if (match is not null) {
                if (!backward) return Wrap(match);
                last = match;
                match = null;
            }
        }
        return Wrap(match ?? last);
    }

    private static int Call(Action action) { try { action(); return 0; } catch (Exception e) { return e.HResult; } }
    private static int Get<T>(Func<T> action, out T value) { try { value = action(); return 0; } catch (Exception e) { value = default!; return e.HResult; } }
    private static WindowsTextRangeProvider Borrow(IntPtr pointer) => WindowsUiaTextNative.Borrow<WindowsTextRangeProvider>(pointer);

    public int AbiClone(out IntPtr result) => Get(() => WindowsUiaTextNative.RangePointer(Clone()), out result);
    public int AbiCompare(IntPtr other, out int equal) => Get(() => Read(r => r.Compare(Other(Borrow(other))) ? 1 : 0), out equal);
    public int AbiCompareEndpoints(int endpoint, IntPtr other, int otherEndpoint, out int result)
        => Get(() => Read(r => r.CompareEndpoints(endpoint, Other(Borrow(other)), otherEndpoint)), out result);
    public int AbiExpandToEnclosingUnit(int unit) => Call(() => Read(r => { r.ExpandToEnclosingUnit(unit); return true; }));
    public int AbiFindAttribute(int attribute, WindowsUiaVariant value, int backward, out IntPtr result)
        => Get(() => WindowsUiaTextNative.RangePointer(Read(r => Attribute(attribute) < 0 ? null
            : FindNativeAttribute(r, attribute, value.ToObject(), backward != 0))), out result);
    public int AbiFindText(IntPtr text, int backward, int ignoreCase, out IntPtr result)
        => Get(() => WindowsUiaTextNative.RangePointer(Read(r => Wrap(r.FindText(Marshal.PtrToStringBSTR(text), backward != 0, ignoreCase != 0)))), out result);
    public int AbiGetAttributeValue(int attribute, out WindowsUiaVariant value) => Get(() => WindowsUiaVariant.FromObject(GetAttributeValue(attribute)), out value);
    public int AbiGetBoundingRectangles(out IntPtr rectangles) => Get(() => WindowsUiaNativeMethods.CreateSafeArray(Read(r =>
        r.GetBoundingRectangles().SelectMany(b => new[] { b.X, b.Y, b.Width, b.Height }).ToArray())), out rectangles);
    public int AbiGetEnclosingElement(out IntPtr element)
        => Get(() => WindowsUiaTextNative.SimplePointer(Read(r => owner.TextElement(r.GetEnclosingElement()))), out element);
    public int AbiGetText(int maximumLength, out IntPtr text) => Get(() => Marshal.StringToBSTR(GetText(maximumLength)), out text);
    public int AbiMove(int unit, int count, out int moved) => Get(() => Read(r => r.Move(unit, count)), out moved);
    public int AbiMoveEndpointByUnit(int endpoint, int unit, int count, out int moved) => Get(() => Read(r => r.MoveEndpointByUnit(endpoint, unit, count)), out moved);
    public int AbiMoveEndpointByRange(int endpoint, IntPtr other, int otherEndpoint)
        => Call(() => Read(r => { r.MoveEndpointByRange(endpoint, Other(Borrow(other)), otherEndpoint); return true; }));
    public int AbiSelect() => Call(() => Mutate(r => r.Select()));
    public int AbiAddToSelection() => Call(() => Mutate(r => r.AddToSelection()));
    public int AbiRemoveFromSelection() => Call(() => Mutate(r => r.RemoveFromSelection()));
    public int AbiScrollIntoView(int alignToTop) => Call(() => Mutate(r => r.ScrollIntoView(alignToTop != 0)));
    public int AbiGetChildren(out IntPtr children) => Get(() => WindowsUiaNativeMethods.CreateSafeArray(Read(r => r.GetChildren().Select(owner.TextElement).ToArray())), out children);
}
