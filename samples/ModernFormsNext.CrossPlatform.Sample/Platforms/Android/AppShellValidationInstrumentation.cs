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

/// <summary>Qualifies shell routes in an actual Activity, including native Back, text, rotation and recreation.</summary>
/// <remarks>Run independently on an emulator and an unlocked authorized phone. This is synthetic native input, not human IME/TalkBack qualification.</remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.AppShellValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class AppShellValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private MainActivity activity = null!;
    private Form main = null!;
    private int assertions;
    /// <summary>Creates the explicitly invoked native shell runner.</summary>
    public AppShellValidationInstrumentation() { }
    /// <summary>Reattaches the runner to its Java instance.</summary>
    /// <param name="handle">The Java handle.</param>
    /// <param name="ownership">JNI ownership.</param>
    public AppShellValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }
    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        AppShellCertification? fixture = null;
        ScreenOrientation? orientation = null;
        bool? keepScreenOn = null;
        bool success = false;
        try
        {
            _ = UiAutomation;
            using var launch = new Intent(TargetContext!, typeof(MainActivity)); launch.AddFlags(ActivityFlags.NewTask);
            activity = (MainActivity)StartActivitySync(launch)!;
            Wait(() => Ui(() => ModernFormsNext.Application.OpenForms.Any() &&
                AndroidWindowKit.Current.GetWindowingDiagnostics().Windows.Any(w => w.Main && w.Active && w.PaintCount > 0)), "actual-native-presentation");
            Record($"DEVICE model={Build.Model} api={(int)Build.VERSION.SdkInt} fingerprint={Build.Fingerprint}");
            fixture = Ui(() =>
            {
                main = ModernFormsNext.Application.OpenForms.Single();
                orientation = activity.RequestedOrientation;
                keepScreenOn = activity.Window!.Attributes!.Flags.HasFlag(WindowManagerFlags.KeepScreenOn);
                activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
                foreach (var control in main.Controls) control.Visible = false;
                return new AppShellCertification(main, Record);
            });
            Ui(() => { fixture.ExerciseImmediate(); return true; });
            Ui(() =>
            {
                using var info = new global::Android.Views.InputMethods.EditorInfo();
                var connection = NativeValidationViews.Main(activity).OnCreateInputConnection(info)!;
                using var text = new Java.Lang.String("native-shell-editor");
                Check(connection.SetComposingText(text, 1) && connection.FinishComposingText(), "actual-native-InputConnection");
                return true;
            });
            HideImeAndIdle(); Capture("app-shell-document.png");
            Ui(() => { fixture.PrepareNestedBack(); return true; });
            Back(); Ui(() => { fixture.VerifyInnerBack(); fixture.OpenFlyout(); return true; });
            Back(); Ui(() => { fixture.VerifyFlyoutBack(); return true; });
            Back(); Ui(() => { fixture.VerifyOuterBack(); return true; });
            // Back commits first and completes the existing incoming appearance asynchronously.
            // Keep the real duration and wait for its queue before testing immediate root work.
            Wait(() => Ui(() => !fixture.Shell.IsNavigating), "outer-native-Back-settled");
            Ui(() => { fixture.BeginSlow(); return true; });
            Back(); Ui(() => { fixture.VerifyPendingBack(); return true; });
            var old = Ui(fixture.CompleteSlow);
            Wait(() => old.IsCompleted, "late-factory-completion");
            Ui(() => { fixture.VerifyLate(); return true; });
            var links = Ui(fixture.DeliverLinks);
            Wait(() => links.IsCompleted, "lifecycle-activation-completion");
            Ui(() => { fixture.VerifyLinks(links); fixture.ExerciseStateAndCycles(); return true; });
            int lifecycle = Ui(() => fixture.LifecycleEvents);
            var snapshot = Ui(() => ModernFormsNext.Application.Lifecycle.Snapshot);
            Check(UiAutomation!.PerformGlobalAction(global::Android.AccessibilityServices.GlobalAction.Home), "native-Home-request");
            Wait(() => Ui(() => ModernFormsNext.Application.Lifecycle.Snapshot.State == WindowKit.Backend.Lifecycle.PlatformApplicationLifecycleState.Background), "actual-Activity-pause");
            Ui(() => { fixture.VerifyRetainedPresentation(); return true; });
            using var resume = new Intent(TargetContext!, typeof(MainActivity));
            resume.AddFlags(ActivityFlags.NewTask | ActivityFlags.ReorderToFront | ActivityFlags.SingleTop);
            TargetContext!.StartActivity(resume);
            Wait(() => Ui(() => ModernFormsNext.Application.Lifecycle.Snapshot.State == snapshot.State), "actual-Activity-resume");
            Ui(() => { activity.RequestedOrientation = ScreenOrientation.Landscape; return true; });
            Wait(() => Ui(() => NativeValidationViews.Main(activity).Width > NativeValidationViews.Main(activity).Height), "native-landscape");
            Ui(() => { fixture.VerifyRetainedPresentation(); activity.RequestedOrientation = ScreenOrientation.Portrait; return true; });
            Wait(() => Ui(() => NativeValidationViews.Main(activity).Height > NativeValidationViews.Main(activity).Width), "native-portrait");
            long generation = Ui(() => AndroidWindowKit.Current.GetWindowingDiagnostics().HostGeneration);
            var previous = activity;
            Ui(() => { activity.Recreate(); return true; });
            Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity current && !ReferenceEquals(current, previous) &&
                AndroidWindowKit.Current.GetWindowingDiagnostics() is { Attached: true } diagnostics && diagnostics.HostGeneration > generation &&
                diagnostics.Windows.Count(w => w.Main && w.Active && w.PaintCount > 0) == 1), "native-Activity-replacement");
            activity = Ui(() => (MainActivity)AndroidWindowKit.Current.ActivityTracker.CurrentActivity!);
            Ui(() => { fixture.VerifyRetainedPresentation(); return true; });
            Check(Ui(() => fixture.LifecycleEvents == lifecycle), "pause-rotation-recreation-retain-page-lifecycle");
            HideImeAndIdle(); Capture("app-shell-recreated.png");
            Ui(() => { fixture.ReturnToRoot(); return true; });
            bool cancelClose = true; int closing = 0;
            Ui(() => { main.Closing += (_, args) => { closing++; args.Cancel = cancelClose; }; return true; });
            Back(); Check(Ui(() => closing == 1 && main.Visible), "root-Back-Form-close-veto");
            Ui(() => { cancelClose = false; return true; });
            Back(); Wait(() => Ui(() => !main.Visible && ModernFormsNext.Application.OpenForms.Count == 0), "root-Back-normal-close");
            Ui(() => { fixture.Dispose(); fixture = null; return true; }); success = true;
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
            Record($"ANDROID_APP_SHELL_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            File.WriteAllText(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "app-shell-validation.txt"), report.ToString());
            result.PutString("stream", report.ToString()); Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }
    private void Back() { HideImeAndIdle(); SendKeyDownUpSync(Keycode.Back); WaitForIdleSync(); }
    private void HideImeAndIdle()
    {
        Ui(() => { main.RequestSoftwareKeyboard(false); NativeValidationViews.Main(activity).HideSoftKeyboard(); return true; });
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            Wait(() => Ui(() => !OperatingSystem.IsAndroidVersionAtLeast(30) || NativeValidationViews.Main(activity).RootWindowInsets?.IsVisible(
                global::Android.Views.WindowInsets.Type.Ime()) != true), "native-IME-hidden-before-Back");
        WaitForIdleSync(); UiAutomation!.WaitForIdle(300, 5000);
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
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15)) { if (condition()) { Check(true, category); return; } Thread.Sleep(25); }
        Check(false, category);
    }
    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException(category);
        assertions++; Record("PASS " + category);
    }
    private void Record(string text) { report.AppendLine(text); global::Android.Util.Log.Info("MFN.Shell", text); }
}
