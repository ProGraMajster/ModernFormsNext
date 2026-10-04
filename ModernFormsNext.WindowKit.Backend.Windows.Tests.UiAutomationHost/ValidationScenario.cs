using System.ComponentModel;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.DataBinding;
using ModernFormsNext.WindowKit.Threading;

// Real HWND and Windows text-method evidence, driven through framework APIs. No foreground
// forcing, synthetic desktop input, or claim of installed CJK IME qualification.
internal static class ValidationScenario
{
    internal static int Run()
    {
        try
        {
            using var main = new Form();
            Exception? failure = null;
            main.Shown += (_, _) => Dispatcher.UIThread.Post(() => {
                try { foreach (bool chrome in new[] { false, true }) Check(chrome); }
                catch (Exception error) { failure = error; }
                finally { main.Close(); }
            });
            Application.Run(main);
            if (failure is not null) throw failure;
            Console.WriteLine("VALIDATION:PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Check(bool chrome)
    {
        using var form = new Form { UseSystemDecorations = !chrome };
        var model = new Model();
        var a = form.Controls.Add(new TextBox { Top = 10, Width = 180, BindingContext = new BindingContext() });
        var b = form.Controls.Add(new TextBox { Top = 55, Width = 180 });
        a.DataBindings.Add(nameof(TextBox.Text), model, nameof(Model.Value), true);
        try
        {
            form.Show();
            Require(IsWindow(form.PlatformHandle.Handle), "Expected real HWND.");
            form.SetTextInputActive(true);
            Require(form.TextInputDiagnostics.HasNativeMethod, "Expected native Windows text method.");
            a.Select();
            a.Text = "accepted";
            var old = form.TextInputClient!;
            var order = new List<string>();
            a.Validating += (_, _) => { Require(a.Selected && !b.Selected, "Validation changed owner early."); order.Add("validating"); };
            a.Validated += (_, _) => { Require(model.Value == "accepted" && a.Selected, "Validated preceded write or followed commit."); order.Add("validated"); };
            a.LostFocus += (_, _) => order.Add("lost");
            b.GotFocus += (_, _) => order.Add("got");
            b.Select();
            Require(order.SequenceEqual(new[] { "validating", "validated", "lost", "got" }), "Invalid event order.");
            Require(b.Selected && !a.Selected && model.Writes == 1, "Accepted validation failed.");
            Require(old.GetState() is null && form.TextInputClient is not null, "Native session handoff failed.");

            a.Select();
            var retained = form.TextInputClient!;
            retained.SetComposingText("unaccepted");
            var revision = retained.GetState()!.Revision;
            order.Clear();
            a.Validating += (_, e) => e.Cancel = true;
            b.Select();
            Require(a.Selected && !b.Selected && model.Value == "accepted" && model.Writes == 1, "Canceled validation committed.");
            Require(ReferenceEquals(retained, form.TextInputClient) && retained.GetState()!.HasComposition &&
                retained.GetState()!.Revision == revision, "Cancellation changed native editor session/composition.");
            Require(order.SequenceEqual(new[] { "validating" }), "Cancellation published completion/focus events.");
            Console.WriteLine($"VALIDATION:HWND:{chrome}:ACCEPT_CANCEL:PASS");
        }
        finally { form.Close(); }
    }

    private sealed class Model
    {
        private string value = "before";
        public string Value { get => value; set { this.value = value; Writes++; } }
        public int Writes { get; private set; }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint handle);
}
