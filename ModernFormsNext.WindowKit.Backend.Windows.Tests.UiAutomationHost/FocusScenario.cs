using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Threading;

// Real HWND/backend and text-method handoff, driven through the normal framework API.
// This does not synthesize desktop mouse/keyboard input or claim an installed IME UI test.
internal static class FocusScenario
{
    internal static int Run()
    {
        try
        {
            using var main = new Form();
            Exception? failure = null;
            main.Shown += (_, _) => Dispatcher.UIThread.Post(() => {
                try
                {
                    foreach (bool chrome in new[] { false, true }) Check(chrome);
                }
                catch (Exception error) { failure = error; }
                finally { main.Close(); }
            });
            Application.Run(main);
            if (failure is not null) throw failure;
            Console.WriteLine("FOCUS:PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Check(bool chrome)
    {
        using var form = new Form { UseSystemDecorations = !chrome };
        var a = form.Controls.Add(new TextBox { Top = 10, Width = 180 });
        var b = form.Controls.Add(new TextBox { Top = 55, Width = 180 });
        try
        {
            form.Show();
            Require(IsWindow(form.PlatformHandle.Handle), "Expected a real HWND.");
            // Explicit native-editor ownership is supported even if foreground policy refuses
            // activation. The actual Windows input method remains attached to this real window.
            form.SetTextInputActive(true);
            Require(form.TextInputDiagnostics.HasNativeMethod, "Expected Windows text input method.");
            a.Select();
            var old = form.TextInputClient ?? throw new InvalidOperationException("Missing first editor session.");
            Require(old.SetComposingText("retained"), "Cannot start representative composition.");
            var order = new List<string>();
            EventHandler lost = (_, _) => {
                Require(!a.Selected && b.Selected, "LostFocus saw inconsistent committed selection.");
                Require(old.GetState() is null, "LostFocus retained old text session.");
                order.Add("lost");
            };
            a.LostFocus += lost;
            b.GotFocus += (_, _) => {
                Require(!a.Selected && b.Selected, "GotFocus saw inconsistent selection.");
                order.Add("got");
            };
            b.Select();
            a.LostFocus -= lost;
            Require(order.SequenceEqual(new[] { "lost", "got" }), "Unexpected focus event ordering.");
            Require(a.Text == "retained" && !old.CommitText("stale"), "Composition was lost or old session accepted text.");
            Require(form.TextInputClient?.CommitText("second") == true && b.Text == "second", "Text handoff missed second editor.");
            form.Hide();
            Require(!a.Selected && !b.Selected && form.TextInputClient is null, "Hide retained focus or text owner.");
            b.Select();
            Require(!b.Selected, "Hidden root accepted selection.");
            form.Show();
            form.SetTextInputActive(true);
            b.Select();
            form.Close();
            Require(!a.Selected && !b.Selected && form.TextInputClient is null, "Close retained focus or text owner.");
            Console.WriteLine($"FOCUS:HWND_TEXT_HANDOFF:{chrome}:PASS");
        }
        finally { form.Close(); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint handle);
}
