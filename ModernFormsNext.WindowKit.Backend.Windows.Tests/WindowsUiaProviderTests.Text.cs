using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed partial class WindowsUiaProviderTests
{
    [Fact]
    public void RealTextBoxTextPatternProvidesFullBstrAndRebasedRanges()
    {
        using var f = new TextFixture();
        f.Editor.Text = "hello😀 world";
        Assert.Same(f.Provider, f.Provider.GetPatternProvider(10014));
        Assert.Same(f.Provider, f.Provider.GetPatternProvider(10024));
        var range = f.Provider.TextDocumentRange;
        Assert.Equal(0, range.AbiGetText(-1, out var bstr));
        try { Assert.Equal(f.Editor.Text, Marshal.PtrToStringBSTR(bstr)); }
        finally { Marshal.FreeBSTR(bstr); }
        Assert.Equal(0, range.AbiClone(out var clone));
        try {
            AssertComInterface(clone, typeof(ITextRangeProviderAbi).GUID);
            Assert.Equal(0, range.AbiCompare(clone, out int equal));
            Assert.Equal(1, equal);
        }
        finally { Marshal.Release(clone); }
        Assert.True(f.Client.SetSelection(0, 0));
        Assert.True(f.Client.CommitText("new "));
        Assert.Equal("new hello😀 world", range.GetText());
    }

    [Fact]
    public void TextPatternComAbiHasBothInterfacesAndSafeArrayOutputs()
    {
        Assert.Equal(IntPtr.Size == 8 ? 24 : 16, Marshal.SizeOf<WindowsUiaVariant>());
        using var f = new TextFixture();
        f.Editor.Text = "text";
        IntPtr unknown = WindowsUiaNativeMethods.GetUnknownPointer(f.Provider);
        try {
            AssertComInterface(unknown, typeof(ITextProviderAbi).GUID);
            AssertComInterface(unknown, typeof(ITextProvider2Abi).GUID);
        }
        finally { Marshal.Release(unknown); }
        Assert.Equal(0, f.Provider.AbiGetTextSelection(out var selection));
        try { Assert.NotEqual(IntPtr.Zero, selection); }
        finally { DestroyTextArray(selection); }
        Assert.Equal(0, f.Provider.TextDocumentRange.AbiGetBoundingRectangles(out var rectangles));
        try { Assert.NotEqual(IntPtr.Zero, rectangles); }
        finally { DestroyTextArray(rectangles); }
        Assert.Equal(0, f.Provider.TextDocumentRange.AbiGetEnclosingElement(out var element));
        try { AssertComInterface(element, typeof(IRawElementProviderSimpleAbi).GUID); }
        finally { Marshal.Release(element); }
    }

    [Fact]
    public void RetainedNativeRangeRejectsSensitiveOrDetachedDocument()
    {
        using var f = new TextFixture();
        f.Editor.Text = "private";
        var retained = f.Provider.TextDocumentRange;
        f.Editor.TextInputOptions = new() { Scope = TextInputScope.Password };
        Assert.Null(f.Provider.GetPatternProvider(10014));
        Assert.NotEqual(0, retained.AbiGetText(-1, out var text));
        Assert.Equal(IntPtr.Zero, text);
        f.Editor.TextInputOptions = new();
        retained = f.Provider.TextDocumentRange;
        f.Root.Controls.Remove(f.Editor);
        Assert.NotEqual(0, retained.AbiGetText(-1, out text));
        Assert.Equal(IntPtr.Zero, text);
    }

    [Fact]
    public void NativeAttributesHaveColorrefStyleAndReservedSentinels()
    {
        using var f = new TextFixture();
        f.Editor.Text = "plain";
        var range = f.Provider.TextDocumentRange;
        Assert.IsType<int>(range.GetAttributeValue(40008));
        Assert.Equal(0, range.GetAttributeValue(40030));
        Assert.Equal(0, range.AbiGetAttributeValue(49999, out var unsupported));
        unsupported.Dispose();
        f.Editor.ReadOnly = true;
        Assert.Equal(true, range.GetAttributeValue(40015));
        Assert.Equal(0, range.AbiSelect());
        Assert.Equal("plain", f.Editor.Text);
    }

    [Fact]
    public void NativeFontSizeUsesPointsAndFindAttributeConvertsBackToLayoutPixels()
    {
        using var f = new TextFixture(new RichTextBox());
        var editor = (RichTextBox)f.Editor;
        editor.Style.FontSize = 12;
        editor.Text = "small BIG";
        var range = f.Provider.TextDocumentRange;
        Assert.Equal(0, range.AbiGetAttributeValue(40006, out var scalar));
        using (scalar) Assert.Equal(9d, scalar.ToObject());

        using var ninePoints = WindowsUiaVariant.FromObject(9d);
        Assert.Equal(0, range.AbiFindAttribute(40006, ninePoints, 0, out var all));
        try {
            Assert.NotEqual(IntPtr.Zero, all);
            Assert.Equal("small BIG", WindowsUiaTextNative.Borrow<WindowsTextRangeProvider>(all).GetText());
        }
        finally { if (all != IntPtr.Zero) Marshal.Release(all); }

        editor.Select(6, 3);
        editor.SelectionFont = new Font("Segoe UI", 24);
        Assert.Equal(PlatformTextAttributeSentinel.Mixed, range.GetAttributeValue(40006));
        using var eighteenPoints = WindowsUiaVariant.FromObject(18d);
        Assert.Equal(0, range.AbiFindAttribute(40006, eighteenPoints, 0, out var large));
        try {
            Assert.NotEqual(IntPtr.Zero, large);
            var found = WindowsUiaTextNative.Borrow<WindowsTextRangeProvider>(large);
            Assert.Equal("BIG", found.GetText());
            Assert.Equal(18d, found.GetAttributeValue(40006));
        }
        finally { if (large != IntPtr.Zero) Marshal.Release(large); }
    }

    [Fact]
    public void NativeFindAttributeVtableAcceptsTheFullByValueVariant()
    {
        using var f = new TextFixture();
        f.Editor.Text = "native text";
        Assert.Equal(0, f.Provider.AbiGetDocumentRange(out var pointer));
        IntPtr found = IntPtr.Zero;
        try {
            var find = Marshal.GetDelegateForFunctionPointer<TextFindAttributeAbi>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), 7 * IntPtr.Size));
            using var value = WindowsUiaVariant.FromObject(false);
            Assert.Equal(0, find(pointer, 40015, value, 0, out found));
            Assert.NotEqual(IntPtr.Zero, found);
            Assert.Equal("native text", WindowsUiaTextNative.Borrow<WindowsTextRangeProvider>(found).GetText());
        }
        finally { if (found != IntPtr.Zero) Marshal.Release(found); Marshal.Release(pointer); }
    }

    [Theory]
    [InlineData(40008, false)]
    [InlineData(40001, true)]
    public void NativeColorAttributeRoundtripsAlphaThroughColorref(int nativeAttribute, bool background)
    {
        using var f = new TextFixture(new RichTextBox());
        var editor = (RichTextBox)f.Editor;
        editor.Text = "alpha";
        editor.Select(0, editor.Text.Length);
        var color = new SkiaSharp.SKColor(0x11, 0x22, 0x33, 0x80);
        if (background) editor.SelectionBackColor = color; else editor.SelectionColor = color;
        var canonicalAttribute = background ? AccessibleTextAttribute.BackgroundColor : AccessibleTextAttribute.ForegroundColor;
        Assert.Equal(unchecked((int)(uint)color), editor.AccessibilityObject.TextProvider!.DocumentRange.GetAttributeValue(canonicalAttribute));
        var range = f.Provider.TextDocumentRange;
        Assert.Equal(0x332211, range.GetAttributeValue(nativeAttribute));
        using var value = WindowsUiaVariant.FromObject(0x332211);
        Assert.Equal(0, range.AbiFindAttribute(nativeAttribute, value, 0, out var found));
        try {
            Assert.NotEqual(IntPtr.Zero, found);
            Assert.Equal("alpha", WindowsUiaTextNative.Borrow<WindowsTextRangeProvider>(found).GetText());
        }
        finally { if (found != IntPtr.Zero) Marshal.Release(found); }
    }

    [Theory]
    [InlineData(40008, false)]
    [InlineData(40001, true)]
    public void NativeColorAttributeIgnoresAlphaOnlyRunDifferences(int nativeAttribute, bool background)
    {
        using var f = new TextFixture(new RichTextBox());
        var editor = (RichTextBox)f.Editor;
        editor.Text = "ABCD";
        editor.Select(0, 4);
        var first = new SkiaSharp.SKColor(0x11, 0x22, 0x33, 0x80);
        if (background) editor.SelectionBackColor = first; else editor.SelectionColor = first;
        editor.Select(2, 2);
        var second = new SkiaSharp.SKColor(0x11, 0x22, 0x33, 0x40);
        if (background) editor.SelectionBackColor = second; else editor.SelectionColor = second;
        var canonicalAttribute = background ? AccessibleTextAttribute.BackgroundColor : AccessibleTextAttribute.ForegroundColor;
        Assert.Same(AccessibleTextAttributeValues.Mixed, editor.AccessibilityObject.TextProvider!.DocumentRange.GetAttributeValue(canonicalAttribute));
        var range = f.Provider.TextDocumentRange;
        Assert.Equal(0x332211, range.GetAttributeValue(nativeAttribute));
        using var value = WindowsUiaVariant.FromObject(0x332211);
        Assert.Equal(0, range.AbiFindAttribute(nativeAttribute, value, 1, out var found));
        try {
            Assert.NotEqual(IntPtr.Zero, found);
            Assert.Equal("ABCD", WindowsUiaTextNative.Borrow<WindowsTextRangeProvider>(found).GetText());
        }
        finally { if (found != IntPtr.Zero) Marshal.Release(found); }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int TextFindAttributeAbi(IntPtr self, int attribute, WindowsUiaVariant value, int backward, out IntPtr range);

    [Theory]
    [InlineData(40008, false)]
    [InlineData(40001, true)]
    public void NativeColorAttributeFindClipsAndDistinguishesSeparatedMatches(int nativeAttribute, bool background)
    {
        using var f = new TextFixture(new RichTextBox());
        var editor = (RichTextBox)f.Editor;
        editor.Text = "aaXbb";
        editor.Select(0, 5);
        Apply(new SkiaSharp.SKColor(0x11, 0x22, 0x33, 0x80));
        editor.Select(2, 1);
        Apply(SkiaSharp.SKColors.Red);
        editor.Select(3, 2);
        Apply(new SkiaSharp.SKColor(0x11, 0x22, 0x33, 0x40));
        var range = f.Provider.WrapTextRange(f.Provider.ReadText(p => p.RangeFromOffsets(1, 4)));
        Assert.Equal(PlatformTextAttributeSentinel.Mixed, range.GetAttributeValue(nativeAttribute));
        using var value = WindowsUiaVariant.FromObject(0x332211);
        foreach (int backward in new[] { 0, 1 }) {
            Assert.Equal(0, range.AbiFindAttribute(nativeAttribute, value, backward, out var found));
            try {
                Assert.NotEqual(IntPtr.Zero, found);
                Assert.Equal(backward == 0 ? "a" : "b", WindowsUiaTextNative.Borrow<WindowsTextRangeProvider>(found).GetText());
            }
            finally { if (found != IntPtr.Zero) Marshal.Release(found); }
        }
        void Apply(SkiaSharp.SKColor color)
        {
            if (background) editor.SelectionBackColor = color; else editor.SelectionColor = color;
        }
    }

    [DllImport("oleaut32.dll", EntryPoint = "SafeArrayDestroy")]
    private static extern int DestroyTextArray(IntPtr array);

    private sealed class TextFixture : IDisposable
    {
        internal Panel Root { get; } = new();
        internal TextBox Editor { get; }
        private readonly SkiaControlSurface surface;
        private readonly WindowsUiaRootProvider nativeRoot;
        internal WindowsUiaProvider Provider { get; }
        internal ITextInputClient Client => surface.TextInputClient!;
        internal TextFixture(TextBox? editor = null)
        {
            Editor = editor ?? new TextBox();
            Root.Controls.Add(Editor); Editor.Bounds = new Rectangle(10, 10, 400, 100);
            surface = new(Root); surface.Resize(500, 200); Editor.Select();
            nativeRoot = WindowsUiaRootProvider.Create(new IntPtr(42), ((IPlatformAccessibilityHost)surface).AccessibilityRoot!, new InlineDispatcher());
            Provider = Assert.IsType<WindowsUiaProvider>(nativeRoot.Navigate(NavigateDirection.FirstChild));
        }
        public void Dispose() { nativeRoot.Dispose(); surface.Dispose(); Root.Dispose(); Editor.Dispose(); }
    }
}
