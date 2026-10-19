using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Backend.Android;
using System.Diagnostics;
using System.Text;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Explicit actual-Activity qualification of retained tabs, flyout, IME and system Back.</summary>
/// <remarks>Run separately on an emulator and an unlocked authorized phone. Synthetic native input does not establish human IME/TalkBack qualification.</remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.PageCompositionValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class PageCompositionValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private MainActivity activity = null!;
    private Form main = null!;
    private int assertions;
    /// <summary>Creates the explicit native composition runner.</summary>
    public PageCompositionValidationInstrumentation() { }
    /// <summary>Reattaches the runner to its Java instance.</summary>
    /// <param name="handle">The Java handle.</param>
    /// <param name="ownership">JNI ownership.</param>
    public PageCompositionValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }
    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        PageCompositionCertification? fixture = null;
        ScreenOrientation? orientation = null;
        bool? keepScreenOn = null;
        bool success = false;
        try
        {
            _ = UiAutomation;
            using var launch = new Intent(TargetContext!, typeof(MainActivity));
            launch.AddFlags(ActivityFlags.NewTask);
            activity = (MainActivity)StartActivitySync(launch)!;
            Wait(() => Ui(() => ModernFormsNext.Application.OpenForms.Any() &&
                AndroidWindowKit.Current.GetWindowingDiagnostics().Windows.Any(w => w.Main && w.Active && w.PaintCount > 0)), "actual-native-presentation");
            report.AppendLine($"DEVICE model={Build.Model} api={(int)Build.VERSION.SdkInt} fingerprint={Build.Fingerprint}");
            fixture = Ui(() =>
            {
                main = ModernFormsNext.Application.OpenForms.Single();
                orientation = activity.RequestedOrientation;
                keepScreenOn = activity.Window!.Attributes!.Flags.HasFlag(WindowManagerFlags.KeepScreenOn);
                activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
                foreach (var control in main.Controls) control.Visible = false;
                return new PageCompositionCertification(main, Record);
            });
            Ui(() => { fixture.ExerciseImmediate(); return true; });
            Ui(() =>
            {
                using var info = new global::Android.Views.InputMethods.EditorInfo();
                var connection = NativeValidationViews.Main(activity).OnCreateInputConnection(info)!;
                using var composition = new Java.Lang.String("native-composition");
                Check(connection.SetComposingText(composition, 1) && connection.FinishComposingText(), "actual-native-InputConnection");
                return true;
            });
            var opening = Ui(fixture.BeginAnimatedOpen);
            Wait(() => opening.IsCompleted, "native-display-scheduler-completion");
            Ui(() => { fixture.VerifyAnimatedOpen(opening); return true; });
            HideImeAndIdle();
            Capture("page-composition-overlay.png");
            int validations = Ui(() => { fixture.PrepareBack(cancel: true); return fixture.ValidationCount; });
            BackWithoutIme();
            Wait(() => Ui(() => fixture.ValidationCount > validations), "native-vetoed-Back-admission");
            Ui(() => { fixture.VerifyVetoedBack(validations); fixture.PrepareBack(cancel: false); return true; });
            BackWithoutIme();
            Wait(() => Ui(() => !fixture.Flyout.IsFlyoutOpen), "native-Back-dismissal");
            Ui(() => { fixture.VerifyClosed(); fixture.Flyout.TransitionDuration = TimeSpan.FromSeconds(2); return true; });
            HideImeAndIdle();
            opening = Ui(fixture.BeginAnimatedOpen);
            // Waiting for accessibility idleness here can wait for the very animation
            // that this case must interrupt. Settle prior input first, then hide IME
            // without draining animation/display events before delivering native Back.
            HideIme();
            Check(!opening.IsCompleted, "native-busy-before-Back");
            SendKeyDownUpSync(Keycode.Back);
            Wait(() => opening.IsCompleted, "busy-Back-completion");
            Ui(() => { fixture.VerifySupersededOpening(opening); fixture.Flyout.TransitionDuration = TimeSpan.FromMilliseconds(180); fixture.ExerciseModesAndOwnership(); return true; });
            int lifecycle = Ui(() => fixture.LifecycleEvents);
            Ui(() => { activity.RequestedOrientation = ScreenOrientation.Landscape; return true; });
            Wait(() => Ui(() => NativeValidationViews.Main(activity).Width > NativeValidationViews.Main(activity).Height), "native-landscape");
            Ui(() => { fixture.VerifyRetainedPresentation(); return true; });
            Ui(() => { activity.RequestedOrientation = ScreenOrientation.Portrait; return true; });
            Wait(() => Ui(() => NativeValidationViews.Main(activity).Height > NativeValidationViews.Main(activity).Width), "native-portrait");
            Check(Ui(() => fixture.LifecycleEvents == lifecycle), "orientation-retains-fixed-mode-lifecycle");
            long generation = Ui(() => AndroidWindowKit.Current.GetWindowingDiagnostics().HostGeneration);
            var previousActivity = activity;
            Ui(() => { activity.Recreate(); return true; });
            Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity current &&
                !ReferenceEquals(current, previousActivity) && AndroidWindowKit.Current.GetWindowingDiagnostics() is { Attached: true } diagnostics &&
                diagnostics.HostGeneration > generation && diagnostics.Windows.Count(w => w.Main && w.Active && w.PaintCount > 0) == 1), "native-Activity-replacement");
            activity = Ui(() => (MainActivity)AndroidWindowKit.Current.ActivityTracker.CurrentActivity!);
            Ui(() => { fixture.VerifyRetainedPresentation(); return true; });
            Check(Ui(() => fixture.LifecycleEvents == lifecycle), "recreation-retains-composition-lifecycle");
            HideImeAndIdle(); Capture("page-composition-recreated.png");
            bool cancelClose = true;
            int closing = 0;
            Ui(() => { main.Closing += (_, e) => { closing++; e.Cancel = cancelClose; }; return true; });
            BackWithoutIme();
            Check(Ui(() => closing == 1 && main.Visible), "root-Back-preserves-Form-Closing-veto");
            Ui(() => { cancelClose = false; return true; });
            BackWithoutIme();
            Wait(() => Ui(() => !main.Visible && ModernFormsNext.Application.OpenForms.Count == 0), "root-Back-legacy-close");
            Ui(() => { fixture.Dispose(); fixture = null; return true; });
            success = true;
        }
        catch (Exception error) { Record($"FAIL {error.GetType().Name}: {error.Message}; {error.StackTrace}"); }
        finally
        {
            try
            {
                if (fixture is not null) Ui(() => { fixture.Dispose(); return true; });
                if (orientation is { } original) Ui(() => { activity.RequestedOrientation = original; return true; });
                if (keepScreenOn == false) Ui(() => { activity.Window!.ClearFlags(WindowManagerFlags.KeepScreenOn); return true; });
            }
            catch (Exception error) { success = false; Record("FAIL cleanup " + error.GetType().Name); }
            Record($"ANDROID_PAGE_COMPOSITION_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            File.WriteAllText(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "page-composition-validation.txt"), report.ToString());
            result.PutString("stream", report.ToString());
            Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }
    private void BackWithoutIme() { HideImeAndIdle(); SendKeyDownUpSync(Keycode.Back); }
    private void HideImeAndIdle()
    {
        HideIme();
        WaitForIdleSync(); UiAutomation!.WaitForIdle(300, 5000);
    }
    private void HideIme()
    {
        Ui(() => { main.RequestSoftwareKeyboard(false); NativeValidationViews.Main(activity).HideSoftKeyboard(); return true; });
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            Wait(() => Ui(() => !OperatingSystem.IsAndroidVersionAtLeast(30) ||
                NativeValidationViews.Main(activity).RootWindowInsets?.IsVisible(global::Android.Views.WindowInsets.Type.Ime()) != true), "native-IME-hidden-before-Back");
    }
    private void Capture(string name)
    {
        using var bitmap = UiAutomation!.TakeScreenshot() ?? throw new InvalidOperationException("Native screenshot unavailable.");
        using var stream = File.Create(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, name));
        Check(bitmap.Compress(global::Android.Graphics.Bitmap.CompressFormat.Png!, 100, stream), "native-frame-" + name);
    }
    private T Ui<T>(Func<T> action)
    {
        T value = default!; Exception? failure = null;
        RunOnMainSync(() => { try { value = action(); } catch (Exception error) { failure = error; } });
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return value;
    }
    private void Wait(Func<bool> condition, string category)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15))
        { if (condition()) { Check(true, category); return; } Thread.Sleep(25); }
        Check(false, category);
    }
    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException(category);
        assertions++; Record("PASS " + category);
    }
    private void Record(string text) { report.AppendLine(text); global::Android.Util.Log.Info("MFN.Composition", text); }
}
