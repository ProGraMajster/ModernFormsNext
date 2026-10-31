using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Backend.Android;
using System.Diagnostics;
using System.Text;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Qualifies the shared appearance fixture on an actual Android Activity and drawing surface.</summary>
/// <remarks>Run separately on an emulator and an authorized physical device. Frame-committed captures
/// require Android 10 or later with an accelerated native view; these are synthetic native evidence.</remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.ShadowTypographyValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class ShadowTypographyValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private int assertions;

    /// <summary>Creates the explicitly selected native appearance runner.</summary>
    public ShadowTypographyValidationInstrumentation() { }
    /// <summary>Reattaches a runner to its Java instance.</summary>
    /// <param name="handle">The Java handle.</param>
    /// <param name="ownership">The JNI ownership mode.</param>
    public ShadowTypographyValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }
    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        ShadowTypographyCertification? fixture = null;
        RtlLayoutCertification? rtl = null;
        BindingNavigatorCertification? navigator = null;
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
                var created = new ShadowTypographyCertification(main.Controls, Record);
                created.Exercise();
                return created;
            });
            Wait(() => Ui(() => fixture.PaintCount > 0), "native-default-appearance-painted");
            using var defaultFrame = Capture(activity, "shadow-typography-default.png");
            int before = Ui(() => fixture.PaintCount);
            Ui(() => { fixture.PrepareAlternative(true); return true; });
            Wait(() => Ui(() => fixture.PaintCount > before), "native-resource-opacity-disabled-painted");
            using var alternativeFrame = Capture(activity, "shadow-typography-alternative.png");
            CheckChangedContent(fixture, defaultFrame, alternativeFrame);
            rtl = Ui(() =>
            {
                fixture.Root.Visible = false;
                var created = new RtlLayoutCertification(ModernFormsNext.Application.OpenForms.Single().Controls, Record);
                created.Exercise();
                return created;
            });
            Wait(() => Ui(() => rtl.PaintCount > 0), "native-rtl-layout-painted");
            using var rtlFrame = Capture(activity, "rtl-layout-rtl.png");
            before = Ui(() => rtl.PaintCount);
            Ui(() => { rtl.PrepareDirection(false); return true; });
            Wait(() => Ui(() => rtl.PaintCount > before), "native-ltr-layout-painted");
            using var ltrFrame = Capture(activity, "rtl-layout-ltr.png");
            CheckLayoutChangedContent(rtl, rtlFrame, ltrFrame);
            navigator = Ui(() =>
            {
                rtl.Root.Visible = false;
                var created = new BindingNavigatorCertification(ModernFormsNext.Application.OpenForms.Single().Controls, Record);
                created.Exercise();
                return created;
            });
            Wait(() => Ui(() => navigator.PaintCount > 0), "native-binding-navigator-painted");
            using var navigatorFrame = Capture(activity, "binding-navigator-first.png");
            before = Ui(() => navigator.PaintCount);
            Ui(() => { navigator.PreparePosition(true); return true; });
            Wait(() => Ui(() => navigator.PaintCount > before), "native-binding-navigator-current-update-painted");
            using var navigatorChangedFrame = Capture(activity, "binding-navigator-second.png");
            CheckNavigatorChangedContent(navigator, navigatorFrame, navigatorChangedFrame);
            Ui(() => { navigator.Root.Visible = false; fixture.Root.Visible = true; return true; });
            long generation = Ui(() => AndroidWindowKit.Current.GetWindowingDiagnostics().HostGeneration);
            var previous = activity;
            Ui(() => { activity.Recreate(); return true; });
            Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity current &&
                !ReferenceEquals(current, previous) && AndroidWindowKit.Current.GetWindowingDiagnostics() is { Attached: true } diagnostics &&
                diagnostics.HostGeneration > generation && diagnostics.Windows.Any(w => w.Main && w.Active && w.PaintCount > 0)),
                "native-Activity-replacement");
            activity = Ui(() => (MainActivity)AndroidWindowKit.Current.ActivityTracker.CurrentActivity!);
            before = Ui(() => fixture.PaintCount);
            Ui(() => { fixture.Exercise(); fixture.Root.Invalidate(); return true; });
            Wait(() => Ui(() => fixture.PaintCount > before), "native-retained-tree-repainted");
            using var recreatedFrame = Capture(activity, "shadow-typography-recreated.png");
            before = Ui(() => rtl.PaintCount);
            Ui(() => { fixture.Root.Visible = false; rtl.Root.Visible = true; rtl.Exercise(); return true; });
            Wait(() => Ui(() => rtl.PaintCount > before), "native-retained-rtl-layout-painted");
            using var rtlRecreatedFrame = Capture(activity, "rtl-layout-recreated.png");
            before = Ui(() => navigator.PaintCount);
            Ui(() => { rtl.Root.Visible = false; navigator.Root.Visible = true; navigator.CheckRetainedState(); return true; });
            Wait(() => Ui(() => navigator.PaintCount > before), "native-retained-binding-navigator-painted");
            using var navigatorRecreatedFrame = Capture(activity, "binding-navigator-recreated.png");
            success = true;
        }
        catch (Exception error) { Record($"FAIL {error.GetType().Name}: {error.Message}; {error.StackTrace}"); }
        finally
        {
            try
            {
                if (navigator is not null) Ui(() => { navigator.Dispose(); return true; });
                if (rtl is not null) Ui(() => { rtl.Dispose(); return true; });
                if (fixture is not null) Ui(() => { fixture.Dispose(); return true; });
                if (keepScreenOn == false && activity is not null)
                    Ui(() => { activity.Window!.ClearFlags(WindowManagerFlags.KeepScreenOn); return true; });
            }
            catch (Exception error) { success = false; Record("FAIL cleanup " + error.GetType().Name); }
            Record($"ANDROID_SHADOW_TYPOGRAPHY_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            File.WriteAllText(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "shadow-typography-validation.txt"), report.ToString());
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
            throw new PlatformNotSupportedException("Frame-committed appearance qualification requires Android 10 or later.");
        int committed = 0;
        using var callback = new Java.Lang.Runnable(() => Volatile.Write(ref committed, 1));
        var observer = Ui(() =>
        {
            var view = NativeValidationViews.Main(activity);
            Check(view.IsHardwareAccelerated, "native-accelerated-view-for-frame-commit");
            var current = view.ViewTreeObserver!;
            if (OperatingSystem.IsAndroidVersionAtLeast(29)) current.RegisterFrameCommitCallback(callback);
            else throw new PlatformNotSupportedException("Frame commit is unavailable.");
            // A control paint callback precedes native frame submission. Observe an actual
            // submitted frame of the existing view before capturing; UI idleness alone cannot
            // establish that the renderer's updated pixels reached the native composition.
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

    private void CheckChangedContent(ShadowTypographyCertification fixture, global::Android.Graphics.Bitmap before,
        global::Android.Graphics.Bitmap after)
    {
        var bounds = Ui(() =>
        {
            var card = fixture.Root.Controls.OfType<Panel>().Single();
            return new System.Drawing.Rectangle(card.PointToScreen(System.Drawing.Point.Empty), card.LogicalToDeviceUnits(card.Size));
        });
        Check(before.Width == after.Width && before.Height == after.Height, "native-frame-size-stable");
        // Edge-to-edge native views can extend underneath system bars. Compare the actual
        // sample card, rather than the view's rectangle, so those overlays cannot satisfy it.
        bounds.Intersect(new System.Drawing.Rectangle(0, 0, before.Width, before.Height));
        Check(bounds.Width > 0 && bounds.Height > 0 && !bounds.Contains(0, 0), "native-content-capture-bounds");
        using var outsideChange = before.Copy(global::Android.Graphics.Bitmap.Config.Argb8888!, true)
            ?? throw new InvalidOperationException("Native negative capture fixture unavailable.");
        int[] corner = new int[1];
        before.GetPixels(corner, 0, 1, 0, 0, 1, 1);
        corner[0] ^= 0x00FFFFFF;
        outsideChange.SetPixels(corner, 0, 1, 0, 0, 1, 1);
        Check(CountChangedPixels(before, outsideChange, bounds) == 0,
            "native-outside-card-change-cannot-satisfy-appearance");
        int different = CountChangedPixels(before, after, bounds);
        Record($"NATIVE_CONTENT_PIXELS different={different} total={checked(bounds.Width * bounds.Height)} bounds={bounds}");
        Check(different > 0, "native-alternative-content-pixels-changed");
    }

    private void CheckLayoutChangedContent(RtlLayoutCertification fixture, global::Android.Graphics.Bitmap before,
        global::Android.Graphics.Bitmap after)
    {
        Check(before.Width == after.Width && before.Height == after.Height, "rtl-native-frame-size-stable");
        var bounds = Ui(() => fixture.FlowPixelBounds);
        bounds.Intersect(new System.Drawing.Rectangle(0, 0, before.Width, before.Height));
        Check(bounds.Width > 0 && bounds.Height > 0 && !bounds.Contains(0, 0), "rtl-native-flow-capture-bounds");
        using var outside = before.Copy(global::Android.Graphics.Bitmap.Config.Argb8888!, true)
            ?? throw new InvalidOperationException("RTL negative capture fixture unavailable.");
        outside.SetPixel(0, 0, new global::Android.Graphics.Color(before.GetPixel(0, 0) ^ 0x00FFFFFF));
        Check(CountChangedPixels(before, outside, bounds) == 0, "rtl-outside-flow-change-cannot-satisfy-layout");
        int different = CountChangedPixels(before, after, bounds);
        Record($"RTL_NATIVE_CONTENT_PIXELS different={different} bounds={bounds}");
        Check(different > 0, "rtl-native-flow-content-pixels-changed");
    }

    private void CheckNavigatorChangedContent(BindingNavigatorCertification fixture, global::Android.Graphics.Bitmap before,
        global::Android.Graphics.Bitmap after)
    {
        Check(before.Width == after.Width && before.Height == after.Height, "navigator-native-frame-size-stable");
        var bounds = Ui(() => fixture.ContentPixelBounds);
        bounds.Intersect(new System.Drawing.Rectangle(0, 0, before.Width, before.Height));
        Check(bounds.Width > 0 && bounds.Height > 0 && !bounds.Contains(0, 0), "navigator-native-bound-content-capture");
        using var outside = before.Copy(global::Android.Graphics.Bitmap.Config.Argb8888!, true)
            ?? throw new InvalidOperationException("Navigator negative capture fixture unavailable.");
        outside.SetPixel(0, 0, new global::Android.Graphics.Color(before.GetPixel(0, 0) ^ 0x00FFFFFF));
        Check(CountChangedPixels(before, outside, bounds) == 0, "navigator-outside-content-change-cannot-satisfy-binding");
        int different = CountChangedPixels(before, after, bounds);
        Record($"NAVIGATOR_NATIVE_CONTENT_PIXELS different={different} bounds={bounds}");
        Check(different > 0, "navigator-native-bound-content-pixels-changed");
    }

    private static int CountChangedPixels(global::Android.Graphics.Bitmap before,
        global::Android.Graphics.Bitmap after, System.Drawing.Rectangle bounds)
    {
        int count = checked(bounds.Width * bounds.Height);
        int[] first = new int[count], second = new int[count];
        before.GetPixels(first, 0, bounds.Width, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        after.GetPixels(second, 0, bounds.Width, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        int different = 0;
        for (int i = 0; i < count; i++) if (first[i] != second[i]) different++;
        return different;
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

    private void Record(string value) { report.AppendLine(value); global::Android.Util.Log.Info("MFN.Appearance", value); }
}
