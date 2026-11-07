using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Backend.Android;
using System.Diagnostics;
using System.Text;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Qualifies bounded shared vector geometry on the sample's actual Android drawing surface.</summary>
/// <remarks>
/// Run explicitly on an emulator and a separately authorized physical device. Uses frame-committed
/// native screenshots and synthetic touch injection, not manual physical-gesture evidence. Requires
/// Android 10 or later. Only owned sample controls and the Activity's keep-screen-on flag are changed.
/// </remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.VectorGeometryValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class VectorGeometryValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private string? artifactDirectory;
    private int assertions;
    /// <summary>Creates the explicitly selected native vector runner.</summary>
    public VectorGeometryValidationInstrumentation() { }
    /// <summary>Reattaches the managed runner to its Java instance.</summary>
    /// <param name="handle">The Java instance handle.</param>
    /// <param name="ownership">The JNI ownership mode.</param>
    public VectorGeometryValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }
    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        VectorGeometryCertification? fixture = null;
        MainActivity? activity = null;
        bool? originalKeepScreenOn = null;
        bool success = false;
        var visibility = new List<(Control Control, bool Visible)>();
        try
        {
            _ = UiAutomation;
            artifactDirectory = System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath,
                "release-validation", "vector-geometry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(artifactDirectory);
            Record("ARTIFACT_DIRECTORY=" + artifactDirectory);
            using var launch = new Intent(TargetContext!, typeof(MainActivity));
            launch.AddFlags(ActivityFlags.NewTask);
            activity = (MainActivity)StartActivitySync(launch)!;
            Wait(() => Ui(() => Application.OpenForms.Count == 1 && AndroidWindowKit.Current.GetWindowingDiagnostics().Windows.Any(w => w.Main && w.Active && w.PaintCount > 0)), "actual-native-presentation");
            Record($"DEVICE model={Build.Model} api={(int)Build.VERSION.SdkInt} fingerprint={Build.Fingerprint}");
            fixture = Ui(() =>
            {
                originalKeepScreenOn = activity.Window!.Attributes!.Flags.HasFlag(WindowManagerFlags.KeepScreenOn);
                activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
                var main = Application.OpenForms.Single();
                foreach (var control in main.Controls) { visibility.Add((control, control.Visible)); control.Visible = false; }
                var created = new VectorGeometryCertification(main.Controls, Record);
                created.Exercise();
                return created;
            });
            Wait(() => Ui(() => fixture.PaintCount > 0), "native-vector-source-painted");
            using var first = Capture(activity, "vector-source.png");
            CheckPixels(fixture, first, false);
            var clip = Ui(() => fixture.ClipPixelBounds);
            Tap(clip.Left + clip.Width / 4, clip.Top + clip.Height / 2);
            Check(Ui(() => fixture.Clicks == 1), "native-touch-included-child");
            Tap(clip.Left + 3 * clip.Width / 4, clip.Top + clip.Height / 2);
            Check(Ui(() => fixture.Clicks == 1), "native-touch-excluded-child-rejected");
            int before = Ui(() => fixture.PaintCount);
            Ui(() => { fixture.PrepareAlternative(true); return true; });
            Wait(() => Ui(() => fixture.PaintCount > before), "native-vector-alternative-painted");
            using var second = Capture(activity, "vector-alternative.png");
            CheckPixels(fixture, second, true);
            before = Ui(() => fixture.PaintCount);
            Ui(() => { fixture.PrepareAlternative(false); return true; });
            Wait(() => Ui(() => fixture.PaintCount > before), "native-vector-restored-painted");
            using var restored = Capture(activity, "vector-restored.png");
            CheckPixels(fixture, restored, false);
            success = true;
        }
        catch (Exception failure) { Record($"FAIL {failure.GetType().Name}: {failure.Message}; {failure.StackTrace}"); }
        finally
        {
            try
            {
                Ui(() =>
                {
                    fixture?.Dispose();
                    foreach (var item in visibility) item.Control.Visible = item.Visible;
                    if (originalKeepScreenOn == false) activity?.Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
                    return true;
                });
            }
            catch (Exception failure) { success = false; Record("FAIL cleanup " + failure.GetType().Name); }
            Record($"ANDROID_VECTOR_GEOMETRY_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            try
            {
                if (artifactDirectory is not null) File.WriteAllText(System.IO.Path.Combine(artifactDirectory, "vector-geometry-validation.txt"), report.ToString());
            }
            catch (Exception failure) { success = false; Record("FAIL artifact report " + failure.GetType().Name); }
            result.PutString("stream", report.ToString());
            Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }
    private void Tap(int x, int y)
    {
        long now = SystemClock.UptimeMillis();
        using var down = MotionEvent.Obtain(now, now, MotionEventActions.Down, x, y, 0);
        using var up = MotionEvent.Obtain(now, now + 30, MotionEventActions.Up, x, y, 0);
        SendPointerSync(down); SendPointerSync(up);
        WaitForIdleSync();
    }
    private void CheckPixels(VectorGeometryCertification fixture, global::Android.Graphics.Bitmap image, bool alternative)
    {
        var bounds = Ui(() => fixture.ClipPixelBounds);
        var center = Ui(() => fixture.AreaCenterPixel);
        Check(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= image.Width && bounds.Bottom <= image.Height &&
            center.X >= 0 && center.Y >= 0 && center.X < image.Width && center.Y < image.Height, "native-vector-owned-content-bounds");
        int left = image.GetPixel(bounds.Left + bounds.Width / 4, bounds.Top + bounds.Height / 2);
        int right = image.GetPixel(bounds.Left + 3 * bounds.Width / 4, bounds.Top + bounds.Height / 2);
        int middle = image.GetPixel(center.X, center.Y);
        const int gold = unchecked((int)0xFFFFD700), white = unchecked((int)0xFFFFFFFF);
        Record($"NATIVE_VECTOR_PIXELS alternative={alternative} left={left:X8} right={right:X8} middle={middle:X8} bounds={bounds}");
        Check(left == (alternative ? white : gold) && right == (alternative ? gold : white) && middle == (alternative ? gold : white), "native-vector-exact-composed-content-" + alternative);
        // A frame with only an unrelated corner changed must not alter these owned content probes.
        using var outside = image.Copy(global::Android.Graphics.Bitmap.Config.Argb8888!, true) ?? throw new InvalidOperationException("Negative frame copy unavailable.");
        outside.SetPixel(0, 0, new global::Android.Graphics.Color(image.GetPixel(0, 0) ^ 0x00FFFFFF));
        Check(outside.GetPixel(center.X, center.Y) == middle, "outside-content-change-cannot-satisfy-vector-probe");
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
        if (!OperatingSystem.IsAndroidVersionAtLeast(29)) throw new PlatformNotSupportedException("Native frame-commit qualification requires Android 10 or later.");
        int committed = 0;
        using var callback = new Java.Lang.Runnable(() => Volatile.Write(ref committed, 1));
        var observer = Ui(() =>
        {
            var view = NativeValidationViews.Main(activity);
            Check(view.IsHardwareAccelerated, "native-accelerated-view");
            var current = view.ViewTreeObserver!;
            if (OperatingSystem.IsAndroidVersionAtLeast(29)) current.RegisterFrameCommitCallback(callback);
            view.RequestRender();
            return current;
        });
        try { Wait(() => Volatile.Read(ref committed) != 0, "native-frame-committed-" + name); }
        finally { Ui(() => { if (OperatingSystem.IsAndroidVersionAtLeast(29) && observer.IsAlive) observer.UnregisterFrameCommitCallback(callback); return true; }); }
        WaitForIdleSync();
        UiAutomation!.WaitForIdle(300, 5000);
        var image = UiAutomation.TakeScreenshot() ?? throw new InvalidOperationException("Native screenshot unavailable.");
        try
        {
            using var stream = File.Create(System.IO.Path.Combine(artifactDirectory!, name));
            Check(image.Compress(global::Android.Graphics.Bitmap.CompressFormat.Png!, 100, stream), "native-frame-" + name);
            return image;
        }
        catch { image.Dispose(); throw; }
    }
    private void Wait(Func<bool> ready, string category)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(15)) { if (ready()) { Check(true, category); return; } Thread.Sleep(25); }
        Check(false, category);
    }
    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException(category);
        assertions++; Record("PASS " + category);
    }
    private void Record(string value) { report.AppendLine(value); global::Android.Util.Log.Info("MFN.Vector", value); }
}
