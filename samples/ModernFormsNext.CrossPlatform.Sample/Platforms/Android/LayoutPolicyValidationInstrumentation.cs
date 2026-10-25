using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Backend.Android;
using System.Diagnostics;
using System.Text;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Qualifies inherited layout on a real Activity, scheduler and native drawing surface.</summary>
/// <remarks>Run separately on an emulator and an authorized physical device. Native frame captures
/// require Android 10 or later. This is synthetic native validation, not manual gesture coverage.</remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.LayoutPolicyValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample")]
public sealed class LayoutPolicyValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private int assertions;
    /// <summary>Creates the instrumentation runner.</summary>
    public LayoutPolicyValidationInstrumentation() { }
    /// <summary>Wraps a native runner instance.</summary>
    /// <param name="handle">Native handle.</param>
    /// <param name="ownership">Ownership of the handle.</param>
    public LayoutPolicyValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }
    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        LayoutPolicyCertification? fixture = null;
        MainActivity? activity = null;
        bool? keepScreenOn = null;
        bool success = false;
        try
        {
            _ = UiAutomation;
            using var launch = new Intent(TargetContext!, typeof(MainActivity));
            launch.AddFlags(ActivityFlags.NewTask);
            activity = (MainActivity)StartActivitySync(launch)!;
            Wait(() => Ui(() => ModernFormsNext.Application.OpenForms.Count == 1 &&
                AndroidWindowKit.Current.GetWindowingDiagnostics().Windows.Any(w => w.Main && w.Active && w.PaintCount > 0)),
                "actual-native-presentation");
            Record($"DEVICE model={Build.Model} api={(int)Build.VERSION.SdkInt} fingerprint={Build.Fingerprint}");
            fixture = Ui(() =>
            {
                keepScreenOn = activity.Window!.Attributes!.Flags.HasFlag(WindowManagerFlags.KeepScreenOn);
                activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
                var main = ModernFormsNext.Application.OpenForms.Single();
                foreach (var control in main.Controls) control.Visible = false;
                var created = new LayoutPolicyCertification(main.Controls, Record);
                created.Begin();
                return created;
            });
            if (!Ui(() => fixture.Immediate))
            {
                Wait(() => Ui(fixture.IsIntermediate), "native-inherited-intermediate-frame");
                Ui(() => { fixture.VerifyIntermediate(); return true; });
            }
            Ui(() => { fixture.SettleAndVerify(); return true; });
            using var positive = Capture(activity, "layout-policy-positive.png");
            var bounds = Ui(() => fixture.ZeroScreenBounds);
            Ui(() => { fixture.CollapseAndVerify(); return true; });
            using var collapsed = Capture(activity, "layout-policy-zero.png");
            Check(positive.Width == collapsed.Width && positive.Height == collapsed.Height, "native-frame-size-stable");
            bounds.Intersect(new System.Drawing.Rectangle(0, 0, positive.Width, positive.Height));
            Check(bounds.Width > 4 && bounds.Height > 4 && !bounds.Contains(0, 0), "native-zero-content-region");
            int x = bounds.Left + bounds.Width / 2, y = bounds.Top + bounds.Height / 2;
            Check(positive.GetPixel(x, y) != collapsed.GetPixel(x, y), "native-zero-clears-previous-content-pixel");
            Ui(() => { fixture.RestoreAndVerify(); return true; });
            using var restored = Capture(activity, "layout-policy-restored.png");
            Check(restored.GetPixel(x, y) == positive.GetPixel(x, y), "native-zero-source-restores-content-pixel");
            success = true;
        }
        catch (Exception error) { Record($"FAIL {error.GetType().Name}: {error.Message}; {error.StackTrace}"); }
        finally
        {
            try
            {
                if (fixture is not null) Ui(() => { fixture.Dispose(); return true; });
                if (keepScreenOn == false && activity is not null)
                    Ui(() => { activity.Window!.ClearFlags(WindowManagerFlags.KeepScreenOn); return true; });
            }
            catch (Exception error) { success = false; Record("FAIL cleanup " + error.GetType().Name); }
            Record($"ANDROID_LAYOUT_POLICY_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            File.WriteAllText(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "layout-policy-validation.txt"), report.ToString());
            result.PutString("stream", report.ToString());
            Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }

    private T Ui<T>(Func<T> action)
    {
        T value = default!; Exception? failure = null;
        RunOnMainSync(() => { try { value = action(); } catch (Exception error) { failure = error; } });
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return value;
    }

    private global::Android.Graphics.Bitmap Capture(MainActivity activity, string name)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            throw new PlatformNotSupportedException("Frame-committed layout qualification requires Android 10 or later.");
        int committed = 0;
        using var callback = new Java.Lang.Runnable(() => Volatile.Write(ref committed, 1));
        var observer = Ui(() =>
        {
            var view = NativeValidationViews.Main(activity);
            Check(view.IsHardwareAccelerated, "native-accelerated-view-for-frame-commit");
            var current = view.ViewTreeObserver!;
            if (OperatingSystem.IsAndroidVersionAtLeast(29)) current.RegisterFrameCommitCallback(callback);
            else throw new PlatformNotSupportedException("Frame commit is unavailable.");
            // A paint callback precedes native submission; capture only after a submitted frame.
            view.RequestRender();
            return current;
        });
        try { Wait(() => Volatile.Read(ref committed) != 0, "native-frame-committed-" + name); }
        finally
        {
            Ui(() =>
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(29) && observer.IsAlive)
                    observer.UnregisterFrameCommitCallback(callback);
                return true;
            });
        }
        WaitForIdleSync();
        UiAutomation!.WaitForIdle(300, 5000);
        var image = UiAutomation.TakeScreenshot() ?? throw new InvalidOperationException("Native screenshot unavailable.");
        try
        {
            using var stream = File.Create(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, name));
            Check(image.Compress(global::Android.Graphics.Bitmap.CompressFormat.Png!, 100, stream), "native-frame-" + name);
            return image;
        }
        catch { image.Dispose(); throw; }
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
    private void Record(string value) { report.AppendLine(value); global::Android.Util.Log.Info("MFN.LayoutPolicy", value); }
}
