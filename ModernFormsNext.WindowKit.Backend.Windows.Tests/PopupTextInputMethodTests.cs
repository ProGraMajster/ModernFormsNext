using ModernFormsNext.WindowKit.Backend.Windows.Win32.Input;
using ModernFormsNext.WindowKit.Input;
using Xunit;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;
using Native = ModernFormsNext.WindowKit.Backend.Windows.Tests.Imm32TextInputMethodTests.Native;
using TextRect = ModernFormsNext.WindowKit.Rect;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

[Collection(WindowsUiCollection.Name)]
public sealed class PopupTextInputMethodTests
{
    [Fact]
    public void PopupLeasePreservesTheShapedBaselineInTheOwnerCoordinateSystem()
    {
        using var editor = new Editor { Visible = true, Text = "baseline" };
        var original = editor.Client.GetState()!;
        double originalBaseline = Assert.IsType<double>(original.CaretBaseline);
        using var lease = new PopupTextInputMethod.Lease(editor.Client,
            rectangle => new TextRect(10 + rectangle.X * 2, 50 + rectangle.Y * 2,
                rectangle.Width * 2, rectangle.Height * 2));
        var mapped = lease.GetState()!;
        Assert.Equal(50 + originalBaseline * 2, mapped.CaretBaseline);
        Assert.Equal(50 + original.CaretRectangle.Y * 2, mapped.CaretRectangle.Y);
        Assert.Equal(original.Text, mapped.Text);
        lease.Dispose();
        Assert.Null(lease.GetState());
        Assert.NotNull(editor.Client.GetState());
    }

    [Fact]
    public void PopupBorrowsActualKeyboardHwndAndMapsCaretWithoutMovingNativeFocus()
    {
        using var owner = new Editor { Visible = true };
        using var popupEditor = new Editor { Visible = true };
        var native = new Native();
        using var parent = new Imm32TextInputMethod(1, () => 2, native);
        parent.SetClient(owner.Client);
        using var popup = new PopupTextInputMethod(parent,
            rectangle => new TextRect(rectangle.X + 100, rectangle.Y + 50, rectangle.Width, rectangle.Height));
        popup.SetClient(popupEditor.Client);
        nint start = 0;
        Assert.True(parent.HandleMessage(WindowsMessage.WM_IME_STARTCOMPOSITION, 0, ref start));
        native.Text[GCS.GCS_COMPSTR] = "日本";
        nint flags = (nint)GCS.GCS_COMPSTR;
        Assert.True(parent.HandleMessage(WindowsMessage.WM_IME_COMPOSITION, 0, ref flags));
        Assert.Empty(owner.Text);
        Assert.Equal("日本", popupEditor.Text);
        Assert.Equal((nint)1, native.FocusedWindow);
        Assert.Equal((nint)1, native.CaretWindow);
        Assert.True(native.Candidates[^1].rcArea.left >= 200);
        Assert.True(native.Candidates[^1].rcArea.top >= 100);
        popup.SetClient(null);
        Assert.Equal("日本", popupEditor.Text);
        Assert.False(popupEditor.Client.GetState()!.HasComposition);
    }

    [Fact]
    public void OldPopupDisposalCannotClearRestoredOwnerOrNewerPopupClient()
    {
        using var owner = new Editor { Visible = true };
        using var firstEditor = new Editor { Visible = true };
        using var secondEditor = new Editor { Visible = true };
        var native = new Native();
        using var parent = new Imm32TextInputMethod(1, () => 1, native);
        using var first = new PopupTextInputMethod(parent, rectangle => rectangle);
        using var second = new PopupTextInputMethod(parent, rectangle => rectangle);
        first.SetClient(firstEditor.Client);
        second.SetClient(secondEditor.Client);
        first.Dispose();
        Commit(parent, native, "second");
        Assert.Empty(firstEditor.Text);
        Assert.Equal("second", secondEditor.Text);

        parent.SetClient(owner.Client);
        second.Dispose();
        Commit(parent, native, "owner");
        Assert.Equal("owner", owner.Text);
        Assert.Equal("second", secondEditor.Text);
    }

    [Fact]
    public void FinishCallbackCanReplaceThePopupLeaseWithoutOuterHandoffRetargetingIt()
    {
        using var firstEditor = new Editor { Visible = true };
        using var secondEditor = new Editor { Visible = true };
        using var callbackEditor = new Editor { Visible = true };
        var native = new Native();
        using var parent = new Imm32TextInputMethod(1, () => 1, native);
        using var popup = new PopupTextInputMethod(parent, rectangle => rectangle);
        popup.SetClient(firstEditor.Client);
        nint start = 0;
        parent.HandleMessage(WindowsMessage.WM_IME_STARTCOMPOSITION, 0, ref start);
        native.Text[GCS.GCS_COMPSTR] = "visible";
        nint flags = (nint)GCS.GCS_COMPSTR;
        parent.HandleMessage(WindowsMessage.WM_IME_COMPOSITION, 0, ref flags);
        firstEditor.TextCompositionChanged += (_, args) => {
            if (args.Stage == TextCompositionStage.Finished) popup.SetClient(callbackEditor.Client);
        };
        popup.SetClient(secondEditor.Client);
        Commit(parent, native, "callback");
        Assert.Equal("visible", firstEditor.Text);
        Assert.Empty(secondEditor.Text);
        Assert.Equal("callback", callbackEditor.Text);
        Assert.Equal(native.Acquired, native.Released);
    }

    private static void Commit(Imm32TextInputMethod method, Native native, string text)
    {
        nint flags = 0;
        Assert.True(method.HandleMessage(WindowsMessage.WM_IME_STARTCOMPOSITION, 0, ref flags));
        native.Text[GCS.GCS_RESULTSTR] = text;
        flags = (nint)GCS.GCS_RESULTSTR;
        Assert.True(method.HandleMessage(WindowsMessage.WM_IME_COMPOSITION, 0, ref flags));
    }

    private sealed class Editor : TextBox
    {
        private readonly SkiaControlSurface surface;
        public Editor()
        {
            surface = new(this);
            surface.Resize(400, 120);
            Select();
        }
        public ITextInputClient Client => surface.TextInputClient ?? throw new InvalidOperationException("The editor is not focused.");
        protected override void Dispose(bool disposing)
        {
            try { if (disposing) surface.Dispose(); }
            finally { base.Dispose(disposing); }
        }
    }
}
