using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Backend.Android;
using System.Diagnostics;
using System.Text;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Checks opt-in brush morphs on a native Activity and independently captured pixels.</summary>
/// <remarks>Requires Android 10 frame commits. Synthetic enabled-state changes do not certify
/// manual gestures, vendor IME, physical scanout or frame pacing. Settings are not changed.</remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.BrushMorphValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample")]
public sealed class BrushMorphValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private int assertions;
    /// <summary>Creates the instrumentation runner.</summary>
    public BrushMorphValidationInstrumentation() { }
    /// <summary>Wraps the native runner.</summary>
    /// <param name="handle">Native handle.</param>
    /// <param name="ownership">Native ownership.</param>
    public BrushMorphValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }
    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        BrushMorphCertification? fixture = null;
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
                AndroidWindowKit.Current.GetWindowingDiagnostics().Windows.Any(w => w.Main && w.Active && w.PaintCount > 0)), "native-presentation");
            // Activity entry is composed outside our view. A frame commit can still be sampled
            // through that system crossfade until the native Activity callback has completed.
            Wait(() => Ui(() => activity.WindowEntryCompleted), "native-activity-entry-complete");
            Record($"DEVICE model={Build.Model} api={(int)Build.VERSION.SdkInt} fingerprint={Build.Fingerprint}");
            fixture = Ui(() =>
            {
                keepScreenOn = activity.Window!.Attributes!.Flags.HasFlag(WindowManagerFlags.KeepScreenOn);
                activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
                var main = ModernFormsNext.Application.OpenForms.Single();
                foreach (var control in main.Controls) control.Visible = false;
                var created = new BrushMorphCertification(main.Controls, Record);
                created.VerifySource();
                return created;
            });
            Ui(() =>
            {
                var view = NativeValidationViews.Main(activity);
                bool highlight = OperatingSystem.IsAndroidVersionAtLeast(26) && view.DefaultFocusHighlightEnabled;
                Record($"NATIVE_VIEW focused={view.HasFocus} touch={view.IsInTouchMode} pressed={view.Pressed} alpha={view.Alpha} defaultFocusHighlight={highlight}");
                return true;
            });
            var encodedPoint = Ui(() => fixture.EncodedCenter);
            var linearPoint = Ui(() => fixture.LinearCenter);
            var glassPoint = Ui(() => fixture.GlassCenter);
            using var source = Capture(activity, fixture, "brush-morph-source.png");
            Check(Red(source, encodedPoint) < 8 && Red(source, linearPoint) < 8, "native-black-source-pixels");
            int emptyPixel = Red(source, glassPoint);
            Check(emptyPixel is > 100 and < 240, "native-no-paint-reveals-parent");
            Ui(() => { fixture.Begin(); return true; });
            bool immediate = Ui(() => fixture.Immediate);
            if (!immediate)
            {
                Wait(() => Ui(() => fixture.IsIntermediate), "native-intermediate-brush-frame");
                Ui(() => { fixture.VerifyIntermediate(); return true; });
            }
            using var middle = Capture(activity, fixture, "brush-morph-middle.png");
            if (!immediate)
            {
                int encodedPixel = Red(middle, encodedPoint), linearPixel = Red(middle, linearPoint);
                Record($"NATIVE_MIDPOINT encoded={encodedPixel} linear={linearPixel} glass={Red(middle, glassPoint)}");
                Check(encodedPixel is > 40 and < 230 && linearPixel > encodedPixel + 25, "native-linear-light-brighter-than-encoded");
                Check(Red(middle, glassPoint) > emptyPixel && Red(middle, glassPoint) < 255, "native-no-paint-glass-fade-pixels");
            }
            Wait(() => Ui(() => fixture.IsSettled), "native-brush-timeline-settled");
            Ui(() => { fixture.VerifySettled(); return true; });
            using var target = Capture(activity, fixture, "brush-morph-target.png");
            Check(Red(target, encodedPoint) > 247 && Red(target, linearPoint) > 247 && Red(target, glassPoint) > 247,
                "native-white-target-pixels");
            Ui(() => { fixture.Restore(); return true; });
            using var restored = Capture(activity, fixture, "brush-morph-restored.png");
            Record($"NATIVE_RESTORE source={Red(source, encodedPoint)}/{Red(source, linearPoint)}/{emptyPixel} restored={Red(restored, encodedPoint)}/{Red(restored, linearPoint)}/{Red(restored, glassPoint)}");
            Check(Red(restored, encodedPoint) == Red(source, encodedPoint) && Red(restored, linearPoint) == Red(source, linearPoint) &&
                Red(restored, glassPoint) == emptyPixel, "native-restored-source-pixels");
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
            Record($"ANDROID_BRUSH_MORPH_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            File.WriteAllText(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "brush-morph-validation.txt"), report.ToString());
            result.PutString("stream", report.ToString());
            Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }

    private static int Red(global::Android.Graphics.Bitmap image, System.Drawing.Point point)
    {
        if (point.X < 0 || point.Y < 0 || point.X >= image.Width || point.Y >= image.Height)
            throw new InvalidOperationException("Controlled brush pixel is outside the native frame.");
        return (image.GetPixel(point.X, point.Y) >> 16) & 255;
    }

    private T Ui<T>(Func<T> action)
    {
        T value = default!; Exception? failure = null;
        RunOnMainSync(() => { try { value = action(); } catch (Exception error) { failure = error; } });
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return value;
    }

    private global::Android.Graphics.Bitmap Capture(MainActivity activity, BrushMorphCertification fixture, string name)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29)) throw new PlatformNotSupportedException("Android 10 frame commits required.");
        var elapsed = Stopwatch.StartNew();
        // First finish a new framework paint. A native commit alone can still contain the
        // previous cached back buffer when a style change has only queued its dispatcher work.
        int before = Ui(() => { int count = fixture.PaintCount; fixture.Root.Invalidate(); return count; });
        Wait(() => Ui(() => fixture.PaintCount > before), "framework-paint-" + name);
        int committed = 0;
        using var callback = new Java.Lang.Runnable(() => Volatile.Write(ref committed, 1));
        var observer = Ui(() =>
        {
            var view = NativeValidationViews.Main(activity);
            Check(view.IsHardwareAccelerated, "native-accelerated-frame-commit");
            var current = view.ViewTreeObserver!;
            if (OperatingSystem.IsAndroidVersionAtLeast(29)) current.RegisterFrameCommitCallback(callback);
            else throw new PlatformNotSupportedException("Frame commit unavailable.");
            view.RequestRender();
            return current;
        });
        try { Wait(() => Volatile.Read(ref committed) != 0, "native-committed-" + name); }
        finally
        {
            Ui(() => { if (OperatingSystem.IsAndroidVersionAtLeast(29) && observer.IsAlive) observer.UnregisterFrameCommitCallback(callback); return true; });
        }
        // The accessibility stream can remain busy throughout an animation. WaitForIdle would
        // delay the requested midpoint; the owned paint and native commit are the frame barrier.
        // https://developer.android.com/reference/android/app/UiAutomation#waitForIdle(long,long)
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            // Android's window readback synchronously flushes SurfaceFlinger transactions before
            // capture; a View frame-commit callback alone does not flush system composition.
            // Retain the full-display image and all original pixel assertions below. The window
            // readback is a native fence, not a delay, a retry or a substitute offscreen render.
            // https://android.googlesource.com/platform/frameworks/base/+/refs/heads/android14-release/core/java/android/app/UiAutomation.java
            using var ownedWindow = UiAutomation!.TakeScreenshot(activity.Window!);
            Check(ownedWindow is not null, "native-window-surface-readback-" + name);
        }
        var image = UiAutomation!.TakeScreenshot() ?? throw new InvalidOperationException("Native screenshot unavailable.");
        Record($"CAPTURE {name} renderToSnapshotMs={elapsed.Elapsed.TotalMilliseconds:F1}");
        try
        {
            using var stream = File.Create(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, name));
            Check(image.Compress(global::Android.Graphics.Bitmap.CompressFormat.Png!, 100, stream), "native-capture-" + name);
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
        assertions++; Record("PASS " + category);
    }
    private void Record(string value) { report.AppendLine(value); global::Android.Util.Log.Info("MFN.BrushMorph", value); }
}
