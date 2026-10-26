using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Backend.Android;
using System.Diagnostics;
using System.Text;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Qualifies shared visual-state metrics on a real Activity and committed native frames.</summary>
/// <remarks>Run independently on an emulator and an authorized physical device. Frame captures
/// require Android 10 or later. Enabled-state transitions are synthetic native coverage;
/// this does not certify manual gestures, vendor IME behavior or physical display scanout.</remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.VisualStateMetricsValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample")]
public sealed class VisualStateMetricsValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private int assertions;
    /// <summary>Creates the instrumentation runner.</summary>
    public VisualStateMetricsValidationInstrumentation() { }
    /// <summary>Wraps a native runner instance.</summary>
    /// <param name="handle">Native handle.</param>
    /// <param name="ownership">Ownership of the native handle.</param>
    public VisualStateMetricsValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }
    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        VisualStateMetricCertification? fixture = null;
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
                var created = new VisualStateMetricCertification(main.Controls, Record);
                created.VerifySource();
                return created;
            });
            using var source = Capture(activity, "visual-state-metrics-source.png");
            var captionBounds = Ui(() => fixture.CaptionScreenBounds);
            var editorBounds = Ui(() => fixture.EditorScreenBounds);
            double scale = Ui(() => fixture.Root.Scaling);
            Ui(() => { fixture.Begin(); return true; });
            if (!Ui(() => fixture.Immediate))
            {
                Wait(() => Ui(fixture.IsIntermediate), "native-intermediate-metric-frame");
                Ui(() => { fixture.VerifyIntermediate(); return true; });
            }
            Wait(() => Ui(() => fixture.IsSettled), "native-metric-timeline-settled");
            Ui(() => { fixture.VerifySettled(); return true; });
            using var target = Capture(activity, "visual-state-metrics-target.png");
            Check(source.Width == target.Width && source.Height == target.Height, "native-frame-size-stable");
            captionBounds.Intersect(new System.Drawing.Rectangle(0, 0, source.Width, source.Height));
            Check(captionBounds.Width > 24 && captionBounds.Height > 24 && !captionBounds.Contains(0, 0), "native-controlled-caption-region");
            int cornerX = captionBounds.Left + 4, cornerY = captionBounds.Top + 4;
            Check(source.GetPixel(cornerX, cornerY) != target.GetPixel(cornerX, cornerY), "native-rounded-corner-content-changed");
            int sourceCaptionInk = MeasureInkWidth(source, captionBounds, scale);
            int targetCaptionInk = MeasureInkWidth(target, captionBounds, scale);
            Record($"NATIVE_CAPTION sourceWidth={sourceCaptionInk} targetWidth={targetCaptionInk}");
            Check(sourceCaptionInk > 4 && targetCaptionInk > sourceCaptionInk, "native-caption-glyph-width-increased");
            int sourceInk = MeasureInkWidth(source, editorBounds, scale);
            int targetInk = MeasureInkWidth(target, editorBounds, scale);
            Record($"NATIVE_GLYPHS sourceWidth={sourceInk} targetWidth={targetInk}");
            Check(sourceInk > 4 && targetInk > sourceInk, "native-editor-glyph-width-increased");
            Ui(() => { fixture.CancelAndRestore(); return true; });
            using var restored = Capture(activity, "visual-state-metrics-restored.png");
            Check(restored.Width == source.Width && restored.Height == source.Height, "native-restored-frame-size-stable");
            Check(MeasureInkWidth(restored, editorBounds, scale) == sourceInk, "native-editor-glyph-width-restored");
            Check(MeasureInkWidth(restored, captionBounds, scale) == sourceCaptionInk, "native-caption-glyph-width-restored");
            Check(restored.GetPixel(cornerX, cornerY) == source.GetPixel(cornerX, cornerY), "native-corner-content-restored");
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
            Record($"ANDROID_VISUAL_STATE_METRICS_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            File.WriteAllText(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "visual-state-metrics-validation.txt"), report.ToString());
            result.PutString("stream", report.ToString());
            Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }

    private static int MeasureInkWidth(global::Android.Graphics.Bitmap image, System.Drawing.Rectangle bounds, double scale)
    {
        // Exclude the border and rounded corner arcs; compare actual glyph pixels independently
        // of framework measurement. Read a bounded native region in one call instead of JNI/pixel.
        int horizontalInset = Math.Max(4, (int)Math.Ceiling(18 * scale));
        int verticalInset = Math.Max(2, (int)Math.Ceiling(4 * scale));
        bounds.Inflate(-horizontalInset, -verticalInset);
        bounds.Intersect(new System.Drawing.Rectangle(0, 0, image.Width, image.Height));
        if (bounds.Width < 1 || bounds.Height < 1 || (long)bounds.Width * bounds.Height > 2_097_152)
            throw new InvalidOperationException("Native glyph region is empty or exceeds its pixel budget.");
        var pixels = new int[checked(bounds.Width * bounds.Height)];
        image.GetPixels(pixels, 0, bounds.Width, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        int left = bounds.Width, right = -1;
        for (int y = 0; y < bounds.Height; y++)
        {
            for (int x = 0; x < bounds.Width; x++)
            {
                int pixel = pixels[y * bounds.Width + x];
                if (((pixel >> 16) & 255) >= 192 || ((pixel >> 8) & 255) >= 192 || (pixel & 255) >= 192) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }
        return right < left ? 0 : right - left + 1;
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
            throw new PlatformNotSupportedException("Frame-committed metric qualification requires Android 10 or later.");
        int committed = 0;
        using var callback = new Java.Lang.Runnable(() => Volatile.Write(ref committed, 1));
        var observer = Ui(() =>
        {
            var view = NativeValidationViews.Main(activity);
            Check(view.IsHardwareAccelerated, "native-accelerated-view-for-frame-commit");
            var current = view.ViewTreeObserver!;
            if (OperatingSystem.IsAndroidVersionAtLeast(29)) current.RegisterFrameCommitCallback(callback);
            else throw new PlatformNotSupportedException("Frame commit is unavailable.");
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
    private void Record(string value) { report.AppendLine(value); global::Android.Util.Log.Info("MFN.StateMetrics", value); }
}
