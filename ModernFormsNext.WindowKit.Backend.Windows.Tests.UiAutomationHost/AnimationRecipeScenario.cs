using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.WindowKit.Threading;

internal static class AnimationRecipeScenario
{
    internal static int Run()
    {
        try
        {
            using var owner = new Form { Text = "Animation recipe certification", ClientSize = new(720, 480) };
            Exception? failure = null;
            owner.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    using var dialog = new DesignerAnimationRecipeDialog("Click\nScale 0.8 1000 BlendEaseInOut 0.6");
                    dialog.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            nint hwnd = dialog.PlatformHandle.Handle;
                            Require(IsWindow(hwnd), "The recipe dialog needs its own HWND.");
                            var sample = Find<Button>(dialog, "Sample");
                            var scheduler = AnimationScheduler.Default;
                            int baseline = scheduler.GetDiagnostics().ActiveAnimationCount;
                            Require(sample.ScaleX == 1f, "Opening the editor changed the sample.");
                            Click(dialog, "Play");

                            // The Windows platform can disable animation globally. In that case
                            // an immediate endpoint is the correct result from the same button.
                            bool immediate = !scheduler.Policy.AnimationsEnabled || scheduler.Policy.ReducedMotion ||
                                scheduler.Policy.DurationScale == 0d;
                            Require(immediate
                                ? sample.ScaleX == 0.8f && scheduler.GetDiagnostics().ActiveAnimationCount == baseline
                                : scheduler.GetDiagnostics().ActiveAnimationCount > baseline,
                                "Play did not follow the scheduler policy.");
                            Click(dialog, "Stop");
                            Require(sample.ScaleX == 1f && scheduler.GetDiagnostics().ActiveAnimationCount == baseline,
                                "Stop did not restore and retire the preview.");
                            Click(dialog, "Restart");
                            Require(immediate
                                ? sample.ScaleX == 0.8f
                                : scheduler.GetDiagnostics().ActiveAnimationCount > baseline,
                                "Restart did not start a fresh preview.");
                            Click(dialog, "Cancel");
                            Console.WriteLine($"ANIMATION_RECIPE:NATIVE_PLAY_STOP_RESTART:PASS:immediate={immediate}");
                        }
                        catch (Exception error)
                        {
                            failure = error;
                            dialog.DialogResult = DialogResult.Cancel;
                        }
                    });
                    Require(await dialog.ShowDialog(owner) == DialogResult.Cancel, "The recipe draft did not cancel.");
                    Require(dialog.Recipe is null, "Cancel persisted a recipe.");
                    Require(AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount == 0,
                        "Closing the dialog left scheduler work active.");
                }
                catch (Exception error) { failure ??= error; }
                finally { Application.Exit(); }
            });
            Application.Run(owner);
            if (failure is not null) throw failure;
            Console.WriteLine("ANIMATION_RECIPE:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static T Find<T>(Form dialog, string suffix) where T : Control
        => (T)dialog.Controls.Find("AnimationRecipe" + suffix, searchAllChildren: true).Single();

    private static void Click(Form dialog, string suffix)
    {
        var control = Find<Button>(dialog, suffix);
        Point point = control.PointToScreen(new Point(
            control.LogicalToDeviceUnits(control.Width / 2), control.LogicalToDeviceUnits(control.Height / 2)));
        nint handle = dialog.PlatformHandle.Handle;
        Require(ScreenToClient(handle, ref point), "Cannot map the recipe button into its native window.");
        nint coordinates = (nint)((point.Y << 16) | (point.X & 0xFFFF));
        SendMessage(handle, 0x0201, 1, coordinates);
        SendMessage(handle, 0x0202, 0, coordinates);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ScreenToClient(nint hwnd, ref Point point);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
