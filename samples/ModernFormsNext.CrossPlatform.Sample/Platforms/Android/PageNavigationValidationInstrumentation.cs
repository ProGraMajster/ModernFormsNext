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

/// <summary>Explicit actual-Activity qualification of pages, native Back, IME and retained lifetimes.</summary>
/// <remarks>Run separately on an emulator and an authorized unlocked physical device. Does not claim human input or screen-reader qualification.</remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.PageNavigationValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class PageNavigationValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private MainActivity activity = null!;
    private Form main = null!;
    private int assertions;

    /// <summary>Creates the explicit native page runner.</summary>
    public PageNavigationValidationInstrumentation() { }
    /// <summary>Reattaches the runner to its Java instance.</summary>
    /// <param name="handle">The Java handle.</param>
    /// <param name="ownership">JNI ownership.</param>
    public PageNavigationValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }
    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        PageNavigationCertification? fixture = null;
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
                return new PageNavigationCertification(main, Record);
            });
            Ui(() => { fixture.ExerciseImmediate(); return true; });
            Ui(() =>
            {
                using var info = new global::Android.Views.InputMethods.EditorInfo();
                var connection = NativeValidationViews.Main(activity).OnCreateInputConnection(info)!;
                using var composition = new Java.Lang.String("native-page");
                Check(connection.SetComposingText(composition, 1) && connection.FinishComposingText(), "actual-native-InputConnection");
                return true;
            });
            HideImeAndIdle();
            bool busyAtBack = false;
            var push = Ui(() =>
            {
                main.KeyDown += (_, e) => { if (e.KeyData == Keys.BrowserBack) busyAtBack = fixture.Navigation.IsNavigating; };
                // A finite explicit busy-input case, not a scheduler/runtime policy override.
                fixture.Navigation.TransitionDuration = TimeSpan.FromSeconds(2);
                return fixture.BeginAnimatedPush();
            });
            SendKeyDownUpSync(Keycode.Back);
            Check(Ui(() => busyAtBack && main.Visible && fixture.Navigation.NavigationStack.Count == 2), "actual-system-Back-consumed-while-busy");
            Wait(() => push.IsCompleted, "native-display-scheduler-completion");
            Ui(() => { fixture.VerifyAnimatedPush(push); fixture.Navigation.TransitionDuration = TimeSpan.FromMilliseconds(180); return true; });
            Capture("page-navigation-details.png");
            Ui(() => { fixture.PrepareBack(cancel: true); return true; });
            BackWithoutIme();
            Wait(() => Ui(() => fixture.BackCompletion.IsCompleted), "native-vetoed-Back-completion");
            Ui(() => { fixture.VerifyVetoedBack(); fixture.PrepareBack(cancel: false); return true; });
            BackWithoutIme();
            Wait(() => Ui(() => fixture.BackCompletion.IsCompleted), "native-Back-transition-completion");
            Ui(() => { fixture.VerifyBack(); fixture.ExerciseRepeatedOwnership(); return true; });

            int lifecycle = Ui(() => fixture.LifecycleEvents);
            Ui(() => { activity.RequestedOrientation = ScreenOrientation.Landscape; return true; });
            Wait(() => Ui(() => NativeValidationViews.Main(activity).Width > NativeValidationViews.Main(activity).Height), "native-landscape");
            Ui(() => { fixture.VerifyRetainedPresentation(); return true; });
            Ui(() => { activity.RequestedOrientation = ScreenOrientation.Portrait; return true; });
            Wait(() => Ui(() => NativeValidationViews.Main(activity).Height > NativeValidationViews.Main(activity).Width), "native-portrait");
            Check(Ui(() => fixture.LifecycleEvents == lifecycle), "orientation-does-not-remove-pages");
            long generation = Ui(() => AndroidWindowKit.Current.GetWindowingDiagnostics().HostGeneration);
            var previousActivity = activity;
            Ui(() => { activity.Recreate(); return true; });
            Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity current &&
                !ReferenceEquals(current, previousActivity) && AndroidWindowKit.Current.GetWindowingDiagnostics() is { Attached: true } diagnostics &&
                diagnostics.HostGeneration > generation && diagnostics.Windows.Count(w => w.Main && w.Active && w.PaintCount > 0) == 1), "native-Activity-replacement");
            activity = Ui(() => (MainActivity)AndroidWindowKit.Current.ActivityTracker.CurrentActivity!);
            Ui(() => { fixture.VerifyRetainedPresentation(); return true; });
            Check(Ui(() => fixture.LifecycleEvents == lifecycle), "recreation-does-not-remove-pages");
            HideImeAndIdle();
            Capture("page-navigation-recreated.png");
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
            Record($"ANDROID_PAGE_NAVIGATION_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            File.WriteAllText(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "page-navigation-validation.txt"), report.ToString());
            result.PutString("stream", report.ToString());
            Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }

    private void BackWithoutIme() { HideImeAndIdle(); SendKeyDownUpSync(Keycode.Back); }
    private void HideImeAndIdle()
    {
        Ui(() => { main.RequestSoftwareKeyboard(false); NativeValidationViews.Main(activity).HideSoftKeyboard(); return true; });
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            Wait(() => Ui(() => !OperatingSystem.IsAndroidVersionAtLeast(30) ||
                NativeValidationViews.Main(activity).RootWindowInsets?.IsVisible(global::Android.Views.WindowInsets.Type.Ime()) != true), "native-IME-hidden-before-Back");
        WaitForIdleSync();
        UiAutomation!.WaitForIdle(300, 5000);
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
        assertions++;
        Record("PASS " + category);
    }
    private void Record(string text) { report.AppendLine(text); global::Android.Util.Log.Info("MFN.Pages", text); }
}
