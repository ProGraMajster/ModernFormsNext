using System.Runtime.InteropServices;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsKeyboardInputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnicodePacketDoesNotInheritPreviousKeyConsumption(bool available)
    {
        using var form = new Form();
        var editor = form.Controls.Add(new TextBox());
        int calls = 0;
        form.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++, () => available), new KeyGesture(Keys.F5)));
        form.Show();
        try
        {
            editor.Select();
            var hwnd = form.PlatformHandle.Handle;
            // Exercise the real per-window message path without changing foreground focus or
            // global keyboard state. A handled key suppresses its character, but a later
            // VK_PACKET (unmapped as a framework key) starts an independent text input event.
            SendMessage(hwnd, 0x0100, (IntPtr)0x74, (IntPtr)1); // WM_KEYDOWN / F5
            SendMessage(hwnd, 0x0102, (IntPtr)'x', (IntPtr)1); // WM_CHAR
            SendMessage(hwnd, 0x0101, (IntPtr)0x74, IntPtr.Zero);
            Assert.Equal(available ? "" : "x", editor.Text);
            Assert.Equal(available ? 1 : 0, calls);
            SendMessage(hwnd, 0x0100, (IntPtr)0xE7, (IntPtr)1); // WM_KEYDOWN / VK_PACKET
            SendMessage(hwnd, 0x0102, (IntPtr)'ą', (IntPtr)1);
            SendMessage(hwnd, 0x0101, (IntPtr)0xE7, IntPtr.Zero);
            Assert.Equal(available ? "ą" : "xą", editor.Text);
            Assert.Equal(available ? 1 : 0, calls);
        }
        finally { form.Close(); }
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
