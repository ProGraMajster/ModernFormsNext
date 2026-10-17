using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Backend.Android;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Runs explicit shared virtualization certification through the actual Android host.</summary>
/// <remarks>
/// Start with adb instrumentation on an emulator and an authorized physical target separately.
/// Ordinary sample startup does not run this workload. Only synthetic fixture data is used.
/// Results describe automated native checks; they never claim human visual inspection.
/// </remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.VirtualizationValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class VirtualizationValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private MainActivity activity = null!;

    /// <summary>Creates the explicit virtualization certification runner.</summary>
    public VirtualizationValidationInstrumentation() { }

    /// <summary>Reattaches the managed runner to its Java instance.</summary>
    /// <param name="handle">The Java handle.</param>
    /// <param name="ownership">JNI ownership.</param>
    public VirtualizationValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership)
        : base(handle, ownership) { }

    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }

    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        VirtualizationCertification? fixture = null;
        bool success = false;
        ScreenOrientation? originalOrientation = null;
        bool? originalKeepScreenOn = null;
        try
        {
            using var intent = new Intent(TargetContext!, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.NewTask);
            activity = (MainActivity)StartActivitySync(intent)!;
            Wait(() => Ui(() => ModernFormsNext.Application.OpenForms.Any() &&
                AndroidWindowKit.Current.GetWindowingDiagnostics().Windows.Any(window => window.Main && window.Active && window.PaintCount > 0)), "native-presentation");
            report.AppendLine($"DEVICE model={Build.Model} api={(int)Build.VERSION.SdkInt} fingerprint={Build.Fingerprint}");
            Record("STAGE construct");
            fixture = Ui(() =>
            {
                originalKeepScreenOn = activity.Window!.Attributes!.Flags.HasFlag(WindowManagerFlags.KeepScreenOn);
                activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
                var main = ModernFormsNext.Application.OpenForms.Single();
                foreach (var control in main.Controls) control.Visible = false;
                return new VirtualizationCertification(main, Record);
            });
            Record("STAGE exercise");
            Ui(() =>
            {
                fixture.Exercise(key =>
                {
                    var code = key switch { Keys.End => Keycode.MoveEnd, Keys.Enter => Keycode.Enter,
                        _ => throw new InvalidOperationException("Unmapped certification key.") };
                    var view = NativeValidationViews.Main(activity);
                    using var down = new KeyEvent(KeyEventActions.Down, code);
                    using var up = new KeyEvent(KeyEventActions.Up, code);
                    view.DispatchKeyEvent(down);
                    view.DispatchKeyEvent(up);
                });
                return true;
            });
            ExerciseUtilities();
            Record("STAGE native-touch");
            var point = Ui(() =>
            {
                fixture.View.ScrollToItem(0);
                Record("TOUCH scroll-ready");
                var row = fixture.View.GetItemContainer(0)!;
                return row.PointToScreen(new(row.LogicalToDeviceUnits(10), row.LogicalToDeviceUnits(10)));
            });
            long time = SystemClock.UptimeMillis();
            using (var down = MotionEvent.Obtain(time, time, MotionEventActions.Down, point.X, point.Y, 0)!) SendPointerSync(down);
            Record("TOUCH down-delivered");
            using (var up = MotionEvent.Obtain(time, time + 50, MotionEventActions.Up, point.X, point.Y, 0)!) SendPointerSync(up);
            Record("TOUCH up-delivered");
            Wait(() => Ui(() => fixture.View.SelectedIndex == 0), "native-touch-selects-realized-row");
            Ui(() => { fixture.View.SelectedIndex = 70_000; return true; });
            var collectionPoint = Ui(() =>
            {
                fixture.Collection.ScrollToItem(0);
                var row = fixture.Collection.GetItemContainer(0)!;
                return row.PointToScreen(new(row.LogicalToDeviceUnits(10), row.LogicalToDeviceUnits(10)));
            });
            long collectionTime = SystemClock.UptimeMillis();
            using (var down = MotionEvent.Obtain(collectionTime, collectionTime, MotionEventActions.Down, collectionPoint.X, collectionPoint.Y, 0)!) SendPointerSync(down);
            using (var up = MotionEvent.Obtain(collectionTime, collectionTime + 50, MotionEventActions.Up, collectionPoint.X, collectionPoint.Y, 0)!) SendPointerSync(up);
            Wait(() => Ui(() => fixture.Collection.SelectedIndex == 0), "native-touch-selects-collection-row");
            originalOrientation = Ui(() => activity.RequestedOrientation);
            Ui(() => { activity.RequestedOrientation = ScreenOrientation.Landscape; return true; });
            Wait(() => Ui(() => NativeValidationViews.Main(activity).Width > NativeValidationViews.Main(activity).Height), "native-landscape-resize");
            Ui(() =>
            {
                fixture.View.ScrollToItem(70_000);
                if (fixture.View.GetItemContainer(70_000) is null || fixture.View.SelectedIndex != 70_000)
                    throw new InvalidOperationException("Orientation lost the selected/realized occurrence.");
                if (fixture.Collection.GetItemContainer(0) is null || fixture.Collection.SelectedIndex != 0)
                    throw new InvalidOperationException("Orientation lost the CollectionView occurrence.");
                // Exercise portrait even when the device began in landscape. The original
                // activity orientation policy is restored by the existing finally block.
                activity.RequestedOrientation = ScreenOrientation.Portrait;
                return true;
            });
            Wait(() => Ui(() => NativeValidationViews.Main(activity).Height > NativeValidationViews.Main(activity).Width), "native-portrait-resize");
            Ui(() =>
            {
                if (fixture.Collection.GetItemContainer(0) is null || fixture.Collection.SelectedIndex != 0)
                    throw new InvalidOperationException("Portrait lost the CollectionView occurrence.");
                Record("PASS collection-native-orientation-selection");
                return true;
            });
            Record("STAGE warm");
            for (int warm = 0; warm < 10; warm++)
            {
                Ui(() => { fixture.WarmBatch(); return true; });
                Record($"WARM batch={warm + 1}/10");
                Thread.Sleep(50);
            }
            Ui(() => { fixture.CompleteWarmup(); return true; });
            long initialPaints = Ui(PaintCount);
            for (int batch = 0; batch < VirtualizationCertification.BatchCount; batch++)
            {
                int current = batch;
                Ui(() => { fixture.Batch(current); return true; });
                Thread.Sleep(1000); // Instrumentation thread only; the UI/render loop stays available.
            }
            Ui(() => { fixture.Complete(); return true; });
            long paints = Ui(PaintCount) - initialPaints;
            if (paints < 10) throw new InvalidOperationException($"Native render callbacks did not advance: {paints}.");
            report.AppendLine($"PASS native-render-loop paints={paints}");
            Ui(() => { fixture.Dispose(); fixture = null; return true; });
            success = true;
        }
        catch (Exception error) { report.AppendLine($"FAIL {error}"); }
        finally
        {
            try
            {
                if (fixture is not null) Ui(() => { fixture.Dispose(); return true; });
                if (originalOrientation is { } orientation) Ui(() => { activity.RequestedOrientation = orientation; return true; });
                if (originalKeepScreenOn == false) Ui(() => { activity.Window!.ClearFlags(WindowManagerFlags.KeepScreenOn); return true; });
            }
            catch (Exception error) { success = false; report.AppendLine($"FAIL cleanup {error}"); }
            report.AppendLine($"ANDROID_VIRTUALIZATION_{(success ? "PASS" : "FAIL")}");
            File.WriteAllText(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "virtualization-validation.txt"), report.ToString());
            result.PutString("stream", report.ToString());
            Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }

    private void ExerciseUtilities()
    {
        Record("STAGE collection-utilities");
        var utilities = Ui(() => new CollectionUtilitiesCertification(ModernFormsNext.Application.OpenForms.Single(), Record));
        try
        {
            Ui(() =>
            {
                utilities.Exercise(key =>
                {
                    var code = key switch { Keys.PageDown => Keycode.PageDown, Keys.F5 => Keycode.F5,
                        _ => throw new InvalidOperationException("Unmapped utility key.") };
                    var view = NativeValidationViews.Main(activity);
                    // The two-argument native constructor has deviceId=-1 and intentionally
                    // follows the framework's editing-only route. Exercise the established
                    // hardware shortcut boundary, without claiming a physical keyboard.
                    long time = SystemClock.UptimeMillis();
                    using var down = new KeyEvent(time, time, KeyEventActions.Down, code, 0, 0, 0, 0,
                        KeyEventFlags.FromSystem, InputSourceType.Keyboard);
                    using var up = new KeyEvent(time, time + 1, KeyEventActions.Up, code, 0, 0, 0, 0,
                        KeyEventFlags.FromSystem, InputSourceType.Keyboard);
                    Record($"UTILITIES key={key} native={code} down={view.DispatchKeyEvent(down)} up={view.DispatchKeyEvent(up)}");
                });
                return true;
            });
            var button = Ui(utilities.ButtonPoint);
            Drag(button, button);
            Ui(() => { utilities.VerifyActivation(); return true; });
            var left = Ui(() => utilities.PagePoint(.85, .3));
            var right = Ui(() => utilities.PagePoint(.15, .3));
            Drag(left, right);
            Ui(() => { utilities.VerifyPage(1, "Android-touch-next-page"); return true; });
            Drag(right, left);
            Ui(() => { utilities.VerifyPage(0, "Android-touch-previous-page"); return true; });
            Drag(left, right, canceled: true);
            Ui(() => { utilities.VerifyPage(0, "Android-touch-cancel-page"); return true; });
            var pullStart = Ui(() => utilities.Point(150, 80));
            Drag(pullStart, Ui(() => utilities.Point(150, 110)));
            Ui(() => { utilities.VerifyRefresh(2, "Android-short-pull"); return true; });
            var pullEnd = Ui(() => utilities.Point(150, 180));
            Drag(pullStart, pullEnd);
            Ui(() => { utilities.VerifyRefresh(3, "Android-edge-pull-refresh"); return true; });
            Drag(pullStart, pullEnd, canceled: true);
            Ui(() => { utilities.VerifyRefresh(3, "Android-cancel-pull"); return true; });
            Drag(pullStart, pullEnd, beforeRelease: () => Ui(() => { utilities.HideDuringPull(); return true; }));
            Ui(() => { utilities.VerifyRefresh(3, "Android-hide-retires-pull"); utilities.VerifyGeometry(); return true; });
            Ui(() => { utilities.Carousel.PageOrientation = Orientation.Vertical; return true; });
            Drag(pullStart, pullEnd);
            Ui(() => { utilities.VerifyRefresh(4, "Android-vertical-page-top-refresh"); utilities.Carousel.SelectedIndex = 1; return true; });
            Drag(pullStart, pullEnd);
            Ui(() => { utilities.VerifyRefresh(4, "Android-vertical-page-keeps-inner-gesture"); return true; });
            var orientation = Ui(() => activity.RequestedOrientation);
            try
            {
                Ui(() => { activity.RequestedOrientation = ScreenOrientation.Landscape; return true; });
                Wait(() => Ui(() => NativeValidationViews.Main(activity).Width > NativeValidationViews.Main(activity).Height), "utilities-landscape-resize");
                Ui(() => { utilities.VerifyGeometry(); activity.RequestedOrientation = ScreenOrientation.Portrait; return true; });
                Wait(() => Ui(() => NativeValidationViews.Main(activity).Height > NativeValidationViews.Main(activity).Width), "utilities-portrait-resize");
                Ui(() => { utilities.VerifyGeometry(); return true; });
            }
            finally { Ui(() => { activity.RequestedOrientation = orientation; return true; }); }
        }
        finally { Ui(() => { utilities.Dispose(); return true; }); }
    }

    private void Drag(System.Drawing.Point start, System.Drawing.Point end, bool canceled = false, Action? beforeRelease = null)
    {
        long time = SystemClock.UptimeMillis();
        using (var down = MotionEvent.Obtain(time, time, MotionEventActions.Down, start.X, start.Y, 0)!) SendPointerSync(down);
        for (int step = 1; step <= 4; step++)
        {
            Thread.Sleep(16); // Instrumentation thread, with the real UI loop free to process input.
            float x = start.X + (end.X - start.X) * step / 4f;
            float y = start.Y + (end.Y - start.Y) * step / 4f;
            using var move = MotionEvent.Obtain(time, SystemClock.UptimeMillis(), MotionEventActions.Move, x, y, 0)!;
            SendPointerSync(move);
        }
        beforeRelease?.Invoke();
        using var finish = MotionEvent.Obtain(time, SystemClock.UptimeMillis(), canceled ? MotionEventActions.Cancel : MotionEventActions.Up, end.X, end.Y, 0)!;
        SendPointerSync(finish);
    }

    private void Record(string line)
    {
        report.AppendLine(line);
        global::Android.Util.Log.Info("MFN.Virtualization", line);
    }

    private static long PaintCount() => AndroidWindowKit.Current.GetWindowingDiagnostics().Windows.Where(window => window.Main).Sum(window => window.PaintCount);

    private T Ui<T>(Func<T> action)
    {
        T value = default!;
        Exception? failure = null;
        RunOnMainSync(() => { try { value = action(); } catch (Exception error) { failure = error; } });
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return value;
    }

    private void Wait(Func<bool> condition, string category)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (condition()) { Record("PASS " + category); return; }
            Thread.Sleep(25);
        }
        throw new InvalidOperationException(category);
    }
}
