using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

internal static class ShadowTypographyScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "Shadow and typography certification", ClientSize = new(380, 440) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    if (!IsWindow(hwnd)) throw new InvalidOperationException("Actual HWND required.");
                    using var fixture = new ShadowTypographyCertification(form.Controls, Console.WriteLine);
                    fixture.Exercise();
                    Paint(hwnd, fixture);
                    fixture.PrepareAlternative(true);
                    Paint(hwnd, fixture);
                    fixture.PrepareAlternative(false);
                    Paint(hwnd, fixture);
                    using (var rtl = new RtlLayoutCertification(form.Controls, Console.WriteLine))
                    {
                        rtl.Exercise();
                        int before = rtl.PaintCount;
                        if (!RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100) || rtl.PaintCount <= before)
                            throw new InvalidOperationException("The actual HWND did not paint the RTL fixture.");
                        SendTab(hwnd, shift: false);
                        if (!rtl.Second.Selected) throw new InvalidOperationException("Native Tab did not follow RTL authored order.");
                        SendTab(hwnd, shift: true);
                        if (!rtl.First.Selected) throw new InvalidOperationException("Native Shift+Tab did not return to the RTL first control.");
                        Console.WriteLine("RTL_LAYOUT:NATIVE_PAINT_TAB_SHIFT_TAB:PASS");
                    }
                    using (var navigator = new BindingNavigatorCertification(form.Controls, Console.WriteLine))
                    {
                        navigator.Exercise();
                        int before = navigator.PaintCount;
                        if (!RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100) || navigator.PaintCount <= before)
                            throw new InvalidOperationException("The actual HWND did not paint the navigator fixture.");
                        navigator.Navigator.PositionTextBox.Select();
                        navigator.Navigator.PositionTextBox.Text = "2";
                        SendMessage(hwnd, 0x0100, 0x0D, 1);
                        SendMessage(hwnd, 0x0101, 0x0D, unchecked((nint)0xC0000001));
                        if (navigator.Position != 1) throw new InvalidOperationException("Native Enter did not commit navigator position.");
                        navigator.PreparePosition(false);
                        navigator.Navigator.PositionTextBox.Select();
                        SendTab(hwnd, shift: false);
                        if (!navigator.Navigator.MoveNextButton.Selected) throw new InvalidOperationException("Native Tab did not reach navigator Next.");
                        SendTab(hwnd, shift: true);
                        if (!navigator.Navigator.PositionTextBox.Selected) throw new InvalidOperationException("Native Shift+Tab did not restore the position editor.");
                        Console.WriteLine("BINDING_NAVIGATOR:NATIVE_PAINT_ENTER_TAB_SHIFT_TAB:PASS");
                    }
                    Console.WriteLine($"SHADOW_TYPOGRAPHY:NATIVE:{hwnd}");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("SHADOW_TYPOGRAPHY:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint handle);

    // This alters only this UI thread's keyboard state around synchronous messages to our HWND.
    // It restores the complete state even on failure and never sends global desktop input.
    private static void SendTab(nint hwnd, bool shift)
    {
        var original = new byte[256];
        if (!GetKeyboardState(original)) throw new InvalidOperationException("Cannot read thread keyboard state.");
        var state = (byte[])original.Clone();
        foreach (int modifier in new[] { 0x11, 0x12, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C })
            state[modifier] = 0;
        state[0x10] = state[0xA0] = shift ? (byte)0x80 : (byte)0;
        state[0xA1] = 0;
        try
        {
            if (!SetKeyboardState(state)) throw new InvalidOperationException("Cannot set thread keyboard state.");
            SendMessage(hwnd, 0x0100, 0x09, 1);
            // SendMessage does not call TranslateMessage. Deliver the same translated Tab
            // packet that the native message queue emits between key down and key up.
            SendMessage(hwnd, 0x0102, 0x09, 1);
            SendMessage(hwnd, 0x0101, 0x09, unchecked((nint)0xC0000001));
        }
        finally
        {
            if (!SetKeyboardState(original)) throw new InvalidOperationException("Cannot restore thread keyboard state.");
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState([Out] byte[] state);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKeyboardState(byte[] state);
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);

    private static void Paint(nint hwnd, ShadowTypographyCertification fixture)
    {
        int before = fixture.PaintCount;
        if (!RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100) || fixture.PaintCount <= before)
            throw new InvalidOperationException("The native HWND did not paint the appearance fixture.");
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(nint hwnd, nint updateRectangle, nint updateRegion, uint flags);
}
