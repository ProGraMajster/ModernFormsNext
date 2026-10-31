using System.Runtime.InteropServices;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

[Collection(WindowsUiCollection.Name)]
public sealed class WindowsKeyboardInputTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void QueuedTranslatedTabKeepsLogicalFocusOrderInBothLayoutDirections(bool rtl, bool shift)
    {
        using var form = new Form();
        var flow = form.Controls.Add(new FlowLayoutPanel { Dock = DockStyle.Fill,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No });
        var first = flow.Controls.Add(new Button { Text = "First", TabIndex = 0 });
        var second = flow.Controls.Add(new Button { Text = "Second", TabIndex = 1 });
        form.Show();
        try
        {
            (shift ? second : first).Select();
            DeliverQueuedTab(form.PlatformHandle.Handle, shift);
            Assert.True((shift ? first : second).Selected);
            Assert.False((shift ? second : first).Selected);
        }
        finally { form.Close(); }
    }

    [Fact]
    public void ConsumedTabKeyDownSuppressesItsTranslatedNavigation()
    {
        using var form = new Form();
        var first = form.Controls.Add(new Button { TabIndex = 0 });
        form.Controls.Add(new Button { TabIndex = 1 });
        form.KeyDown += (_, e) => { if (e.KeyCode == Keys.Tab) e.Handled = true; };
        form.Show();
        try
        {
            first.Select();
            DeliverQueuedTab(form.PlatformHandle.Handle, shift: false);
            Assert.True(first.Selected);
        }
        finally { form.Close(); }
    }

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

    // Use our HWND's real message queue and TranslateMessage, without global input or focus
    // stealing. Keyboard modifiers belong to this UI thread and are restored in full.
    private static void DeliverQueuedTab(nint hwnd, bool shift)
    {
        var original = new byte[256];
        Assert.True(GetKeyboardState(original));
        var state = (byte[])original.Clone();
        foreach (int modifier in new[] { 0x11, 0x12, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C }) state[modifier] = 0;
        state[0x10] = state[0xA0] = shift ? (byte)0x80 : (byte)0;
        state[0xA1] = 0;
        try
        {
            Assert.True(SetKeyboardState(state));
            Assert.True(PostMessage(hwnd, 0x0100, 9, 0x000F0001));
            Assert.True(PostMessage(hwnd, 0x0101, 9, unchecked((nint)0xC00F0001)));
            int messages = 0;
            while (PeekMessage(out var message, hwnd, 0x0100, 0x0109, 1))
            {
                Assert.True(++messages <= 16, "Unexpected keyboard message cycle.");
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }
        finally { Assert.True(SetKeyboardState(original)); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    { internal nint Window; internal uint Id; internal nuint WParam; internal nint LParam; internal uint Time; internal System.Drawing.Point Point; internal uint Private; }
    [DllImport("user32.dll")] private static extern bool GetKeyboardState([Out] byte[] state);
    [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] state);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, nint hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref Message message);
}
