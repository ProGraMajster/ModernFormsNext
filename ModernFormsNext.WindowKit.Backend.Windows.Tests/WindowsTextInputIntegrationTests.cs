using System.Runtime.InteropServices;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

[Collection(WindowsUiCollection.Name)]
public sealed class WindowsTextInputIntegrationTests
{
    [Fact]
    public void NonactivatingEditablePopupReceivesTheOwnerHwndUnicodeInput()
    {
        using var form = new Form();
        var ownerEditor = form.Controls.Add(new TextBox());
        form.Show();
        using var popup = new PopupWindow(form) { Size = new System.Drawing.Size(240, 100) };
        var popupEditor = popup.Controls.Add(new TextBox());
        try
        {
            ownerEditor.Select();
            var oldOwner = form.TextInputClient!;
            popup.Show(form.PointToScreen(new System.Drawing.Point(20, 40)));
            popupEditor.Select();
            Assert.NotNull(popup.TextInputClient);
            Assert.Null(oldOwner.GetState());
            nint ownerHwnd = form.PlatformHandle.Handle;
            SendMessage(ownerHwnd, 0x0100, (nint)0xE7, 1);
            SendMessage(ownerHwnd, 0x0102, (nint)'ą', 1);
            Assert.Equal("ą", popupEditor.Text);
            Assert.Empty(ownerEditor.Text);
            var oldPopup = popup.TextInputClient!;
            popup.Hide();
            Assert.Null(oldPopup.GetState());
            SendMessage(ownerHwnd, 0x0100, (nint)0xE7, 1);
            SendMessage(ownerHwnd, 0x0102, (nint)'b', 1);
            Assert.Equal("b", ownerEditor.Text);
            Assert.Equal("ą", popupEditor.Text);
            var restoredOwner = form.TextInputClient!;
            popup.Show(form.PointToScreen(new System.Drawing.Point(30, 50)));
            // The same HWND uses ShowNoActivate again; no native Activated callback is
            // available to restore its text session after the first Hide retired it.
            Assert.NotNull(popup.TextInputClient);
            Assert.NotSame(oldPopup, popup.TextInputClient);
            Assert.Null(restoredOwner.GetState());
            Assert.False(popup.TextInputClient!.GetState()!.HasComposition);
            SendMessage(ownerHwnd, 0x0100, (nint)0xE7, 1);
            SendMessage(ownerHwnd, 0x0102, (nint)'ć', 1);
            Assert.Equal("ąć", popupEditor.Text);
            Assert.Equal("b", ownerEditor.Text);
            SendMessage(ownerHwnd, 0x0100, (nint)0x08, 1);
            SendMessage(ownerHwnd, 0x0102, (nint)'\b', 1);
            SendMessage(ownerHwnd, 0x0101, (nint)0x08, 1);
            Assert.Equal("ą", popupEditor.Text);
            Assert.Equal("b", ownerEditor.Text);
        }
        finally { popup.Close(); form.Close(); }
    }

    [Fact]
    public void RealOwnedWindowRestoresItsImeContextAndPreservesPasswordCharacterInput()
    {
        using var form = new Form();
        var editor = form.Controls.Add(new TextBox());
        form.Show();
        try
        {
            editor.Select();
            var hwnd = form.PlatformHandle.Handle;
            Assert.NotNull(form.TextInputClient);
            nint originalContext = ReadContext(hwnd);
            editor.ReadOnly = true;
            Assert.Equal(0, ReadContext(hwnd));
            SendMessage(hwnd, 0x0100, (nint)0xE7, 1);
            SendMessage(hwnd, 0x0102, (nint)'x', 1);
            Assert.Empty(editor.Text);
            editor.ReadOnly = false;
            editor.PasswordCharacter = '*';
            Assert.Equal(0, ReadContext(hwnd));
            // No language/profile or foreground change: exercise the real HWND's existing
            // Unicode path while IME is disabled for this password client.
            SendMessage(hwnd, 0x0100, (nint)0xE7, 1);
            SendMessage(hwnd, 0x0102, (nint)'ą', 1);
            Assert.Equal("ą", editor.Text);
            editor.PasswordCharacter = null;
            Assert.Equal(originalContext, ReadContext(hwnd));
            Assert.False(form.RequestSoftwareKeyboard(true));
            var old = form.TextInputClient!;
            form.Close();
            Assert.Null(old.GetState());
            Assert.False(old.CommitText("late"));
        }
        finally { form.Close(); }
    }

    private static nint ReadContext(nint hwnd)
    {
        nint context = ImmGetContext(hwnd);
        if (context != 0) Assert.True(ImmReleaseContext(hwnd, context));
        return context;
    }

    [DllImport("imm32.dll", ExactSpelling = true)]
    private static extern nint ImmGetContext(nint hwnd);
    [DllImport("imm32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(nint hwnd, nint context);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
