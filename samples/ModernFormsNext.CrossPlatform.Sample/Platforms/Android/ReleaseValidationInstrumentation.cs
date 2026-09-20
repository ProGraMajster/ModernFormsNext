using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using ModernFormsNext.Animations;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.Drawing;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using SkiaSharp;
using System.Drawing;
using System.Text;
using Environment = System.Environment;
using FilePath = System.IO.Path;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Runs the explicitly requested Android release matrix on the sample's real native host.</summary>
/// <remarks>
/// Launch with adb instrumentation, never as part of normal application startup. Synthetic native
/// input is not vendor-IME or physical-gesture evidence. Frame data comes from PerformanceProfiler;
/// it measures software callbacks, not GPU completion or display scanout. This runner changes only
/// its own window and temporary sample controls, and restores their policy/visibility in finally.
/// </remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.ReleaseValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class ReleaseValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private int assertions;
    private string stage = "launch", output = string.Empty;
    private MainActivity activity = null!;
    private App app = null!;
    private Panel fixture = null!;
    private Control[] editors = [];
    private Ellipse shape = null!;
    private AnimationRun? animation;
    private int firstClicks, secondClicks;
    private bool systemReducedMotionOnly;
    private readonly List<WeakReference> retiredViews = [];

    /// <summary>Creates the explicit release validation runner.</summary>
    public ReleaseValidationInstrumentation() { }
    /// <summary>Reattaches the managed runner to its native instrumentation instance.</summary>
    /// <param name="handle">Native Java handle.</param>
    /// <param name="ownership">JNI ownership transfer.</param>
    public ReleaseValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership)
        : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments)
    {
        systemReducedMotionOnly = arguments?.GetString("systemReducedMotionOnly") == "true";
        base.OnCreate(arguments);
        Start();
    }

    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        var visibility = new List<(Control Control, bool Visible)>();
        bool originalReduced = false, success = false;
        bool originalKeepScreenOn = false, windowConfigured = false;
        var originalOrientation = global::Android.Content.PM.ScreenOrientation.Unspecified;
        int originalMode = 0;
        try
        {
            _ = UiAutomation; // Connect before launch; release by Instrumentation.Finish.
            using var intent = LaunchIntent();
            activity = (MainActivity)StartActivitySync(intent)!;
            WaitFor(() => OnUi(() => View().Width > 0 && Runtime().ActiveSurfaceCount == 1), "host-ready");
            output = FilePath.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "release-validation",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Environment.ProcessId);
            Directory.CreateDirectory(output);
            report.AppendLine("ARTIFACT_DIRECTORY=" + output);
            OnUi(() =>
            {
                app = ((SampleApplication)activity.Application!).SharedApp;
                originalReduced = AnimationScheduler.Default.Policy.ApplicationReducedMotion;
                using var attributes = activity.Window!.Attributes!;
                originalMode = attributes.PreferredDisplayModeId;
                originalKeepScreenOn = attributes.Flags.HasFlag(WindowManagerFlags.KeepScreenOn);
                originalOrientation = activity.RequestedOrientation;
                windowConfigured = true;
                activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
                foreach (var child in app.Root.Controls) { visibility.Add((child, child.Visible)); child.Visible = false; }
                CreateFixture();
                return true;
            });
            if (systemReducedMotionOnly)
            {
                stage = "system-reduced-motion";
                OnUi(() => { AnimationScheduler.Default.Policy.ReducedMotion = false; return true; });
                WaitFor(() => AnimationScheduler.Default.Policy.ReducedMotion, "native-system-reduced-motion-observed");
                Check(!AnimationScheduler.Default.Policy.ApplicationReducedMotion, "application-preference-remains-off");
                OnUi(() => { animation = shape.RotateTo(37, Options(3000)).Start(AnimationScheduler.Default); return true; });
                WaitFor(() => animation!.Completion.IsCompleted, "system-reduced-motion-complete");
                Check(OnUi(() => Math.Abs(shape.Rotation - 37) < .001), "system-reduced-motion-exact-target");
                WaitFor(() => OnUi(() => !Runtime().FrameCallbackPending && !Runtime().SchedulerDemand), "system-reduced-motion-idle");
                report.AppendLine("SYSTEM_REDUCED_MOTION=PASS; source=animator_duration_scale; application_preference=False");
                success = true;
                return;
            }
            stage = "composition";
            CheckComposition();
            OnUi(() => { fixture.Controls.OfType<Button>().First().Select(); View().HideSoftKeyboard(); return true; });
            WaitFor(() => OnUi(() => app.Root.Height > 500), "keyboard-dismissed-viewport-restored");
            Capture("fixture.png");
            stage = "native-multitouch";
            CheckPointers();
            stage = "shapes-and-frame-cadence";
            CheckFrameModes();
            stage = "orientation";
            CheckOrientation();
            stage = "background";
            CheckBackground();
            stage = "recreation-stress";
            CheckRecreation();
            stage = "protocol-activation";
            using (var protocol = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse("modernformsnext-sample://release-validation")!, TargetContext!, typeof(MainActivity)))
                OnUi(() => { activity.StartActivity(protocol); return true; });
            WaitFor(() => OnUi(() => ModernFormsNext.Application.Lifecycle.GetDiagnostics().LastActivationKind?.ToString() == "Protocol"), "native-protocol-activation");
            stage = "reduced-motion";
            OnUi(() =>
            {
                AnimationScheduler.Default.Policy.ReducedMotion = true;
                animation = shape.RotateTo(37, Options(3000)).Start(AnimationScheduler.Default);
                return true;
            });
            WaitFor(() => animation!.Completion.IsCompleted, "reduced-motion-complete");
            Check(OnUi(() => Math.Abs(shape.Rotation - 37) < .001), "reduced-motion-exact-target");
            WaitFor(() => OnUi(() => !Runtime().FrameCallbackPending && !Runtime().SchedulerDemand), "final-idle");
            Check(OnUi(() => AnimationScheduler.Default.GetDiagnostics().FaultedCount == 0), "scheduler-no-faults");
            Capture("final.png");
            success = true;
        }
        catch (Exception error)
        {
            report.AppendLine($"FAIL stage={stage} type={error.GetType().Name}");
        }
        finally
        {
            try
            {
                OnUi(() =>
                {
                    animation?.Cancel();
                    AnimationScheduler.Default.Policy.ReducedMotion = originalReduced;
                    if (fixture is not null) { app.Root.Controls.Remove(fixture); fixture.Dispose(); }
                    foreach (var item in visibility) item.Control.Visible = item.Visible;
                    if (windowConfigured && AndroidWindowKit.Current.ActivityTracker.CurrentActivity is { } current)
                    {
                        current.RequestedOrientation = originalOrientation;
                        if (originalKeepScreenOn) current.Window!.AddFlags(WindowManagerFlags.KeepScreenOn);
                        else current.Window!.ClearFlags(WindowManagerFlags.KeepScreenOn);
                        using var attributes = current.Window.Attributes!;
                        attributes.PreferredDisplayModeId = originalMode;
                        current.Window.Attributes = attributes;
                    }
                    return true;
                });
            }
            catch (Exception error) { success = false; report.AppendLine($"FAIL cleanup={error.GetType().Name}"); }
            report.AppendLine($"ANDROID_RELEASE_MATRIX_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            if (output.Length > 0) File.WriteAllText(FilePath.Combine(output, "result.txt"), report.ToString());
            result.PutString("stream", report.ToString());
            Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }

    private void CreateFixture()
    {
        fixture = app.Root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = SKColors.White });
        editors = [new TextBox(), new TextBox { MultiLine = true }, new RichTextBox { MultiLine = true }, new MarkdownEditor()];
        for (int i = 0; i < editors.Length; i++)
        {
            editors[i].Name = "ReleaseEditor" + i;
            editors[i].SetBounds(12, 12 + i * 72, Math.Max(180, app.Root.Width - 24), 62);
            fixture.Controls.Add(editors[i]);
        }
        var first = fixture.Controls.Add(new Button { Text = "First pointer", Bounds = new Rectangle(12, 306, 145, 46) });
        var second = fixture.Controls.Add(new Button { Text = "Second pointer", Bounds = new Rectangle(170, 306, 145, 46) });
        first.Click += (_, _) => firstClicks++;
        second.Click += (_, _) => secondClicks++;
        shape = fixture.Controls.Add(new Ellipse { Bounds = new Rectangle(40, 390, 110, 85),
            Fill = new SolidColorBrush(Color.CornflowerBlue), Stroke = new SolidColorBrush(Color.Navy), StrokeThickness = 3 });
        var geometry = new PathGeometry();
        var figure = new PathFigure(new PointF(5, 70), isClosed: true);
        figure.Segments.Add(new LineSegment(new PointF(45, 5)));
        figure.Segments.Add(new BezierSegment(new PointF(100, 5), new PointF(80, 85), new PointF(5, 70)));
        geometry.Figures.Add(figure);
        fixture.Controls.Add(new ModernFormsNext.Path { Bounds = new Rectangle(190, 390, 110, 85), Data = geometry,
            Fill = new SolidColorBrush(Color.Gold), Stroke = new SolidColorBrush(Color.DarkRed), StrokeThickness = 2 });
    }

    private void CheckComposition()
    {
        for (int i = 0; i < editors.Length; i++)
        {
            int index = i;
            OnUi(() =>
            {
                var editor = editors[index];
                editor.Text = string.Empty;
                // Markdown owns an ordinary rich editor child; use that same canonical focus path.
                var focus = editor is MarkdownEditor ? Descendants(editor).OfType<RichTextBox>().First() : editor;
                focus.Select();
                using var info = new EditorInfo();
                var connection = View().OnCreateInputConnection(info)!; // Borrowed; view owns disposal.
                using var composing = new Java.Lang.String("zażółć");
                Check(connection.SetComposingText(composing, 1), "native-compose-start");
                Check(app.Root.TextInputClientProvider!()!.GetState()!.HasComposition, "shared-composition-active");
                using var committed = new Java.Lang.String("zażółć 👋");
                Check(connection.CommitText(committed, 1), "native-commit");
                Check(editor.Text == "zażółć 👋", "unicode-commit-on-shared-editor");
                Check(!app.Root.TextInputClientProvider!()!.GetState()!.HasComposition, "shared-composition-ended");
                Check(OperatingSystem.IsAndroidVersionAtLeast(24)
                    ? connection.DeleteSurroundingTextInCodePoints(1, 0)
                    : connection.DeleteSurroundingText(2, 0), "native-surrogate-delete");
                Check(editor.Text == "zażółć ", "surrogate-pair-deleted-once");
                if (index > 0)
                {
                    using var newline = new Java.Lang.String("\nnext");
                    Check(connection.CommitText(newline, 1) && editor.Text.Contains('\n'), "multiline-commit");
                }
                return true;
            });
        }
        report.AppendLine("IME=native-InputConnection-synthetic; editors=4; vendor-keyboard-observation=NOT_EXECUTED");
    }

    private void CheckPointers()
    {
        // Native accessibility bounds include density, edge-to-edge insets and the view's screen
        // origin. Do not assume that fixture coordinates begin at native (0,0), especially API 35+.
        var firstPoint = PointerPoint("First pointer");
        var secondPoint = PointerPoint("Second pointer");
        OnUi(() =>
        {
            // Real MotionEvents enter the production AndroidSkiaHostView and shared capture route.
            long down = SystemClock.UptimeMillis();
            Send(MotionEventActions.Down, [11], [firstPoint], down);
            Send((MotionEventActions)((int)MotionEventActions.PointerDown | (1 << 8)), [11, 23], [firstPoint, secondPoint], down);
            Send((MotionEventActions)((int)MotionEventActions.PointerUp | (1 << 8)), [11, 23], [firstPoint, secondPoint], down);
            Check(secondClicks == 1 && firstClicks == 0, "independent-second-pointer-release");
            Send(MotionEventActions.Up, [11], [firstPoint], down);
            Check(firstClicks == 1 && secondClicks == 1, "first-pointer-survives-other-release");
            Send(MotionEventActions.Down, [31], [firstPoint], down);
            Send(MotionEventActions.Cancel, [31], [firstPoint], down);
            Check(firstClicks == 1 && secondClicks == 1, "native-cancel-does-not-click");
            return true;
        });
    }

    private Point PointerPoint(string label)
    {
        using var root = UiAutomation!.RootInActiveWindow;
        var matches = root!.FindAccessibilityNodeInfosByText(label)!;
        try
        {
            Check(matches.Count == 1, "native-pointer-target-unique");
            using var bounds = new global::Android.Graphics.Rect();
            matches[0].GetBoundsInScreen(bounds);
            var origin = OnUi(() => { int[] value = new int[2]; View().GetLocationOnScreen(value); return value; });
            return new Point((bounds.Left + bounds.Right) / 2 - origin[0], (bounds.Top + bounds.Bottom) / 2 - origin[1]);
        }
        finally { foreach (var node in matches) node.Dispose(); }
    }

    private void Send(MotionEventActions action, int[] ids, Point[] points, long down)
    {
        var properties = ids.Select(id => new MotionEvent.PointerProperties { Id = id, ToolType = MotionEventToolType.Finger }).ToArray();
        var coordinates = points.Select(value => new MotionEvent.PointerCoords { X = value.X, Y = value.Y, Pressure = 1, Size = 1 }).ToArray();
        try
        {
            using var motion = MotionEvent.Obtain(down, SystemClock.UptimeMillis(), action, ids.Length, properties, coordinates,
                0, 0, 1, 1, 0, 0, InputSourceType.Touchscreen, 0)!;
            View().DispatchTouchEvent(motion);
        }
        finally { foreach (var item in properties) item.Dispose(); foreach (var item in coordinates) item.Dispose(); }
    }

    private void CheckFrameModes()
    {
        var modes = OnUi(() =>
        {
            var nativeModes = View().Display!.GetSupportedModes()!;
            try { return nativeModes.Select(mode => (mode.ModeId, mode.RefreshRate)).ToArray(); }
            finally { foreach (var mode in nativeModes) mode.Dispose(); }
        });
        foreach (int requested in new[] { 60, 90, 120 })
        {
            var mode = modes.FirstOrDefault(mode => Math.Abs(mode.RefreshRate - requested) < 1);
            if (mode.ModeId == 0) { report.AppendLine($"RATE requested={requested} status=NOT_AVAILABLE"); continue; }
            OnUi(() => { using var attributes = activity.Window!.Attributes!; attributes.PreferredDisplayModeId = mode.ModeId; activity.Window.Attributes = attributes; return true; });
            Thread.Sleep(400); // Settle the optional window-mode request outside the measured phase.
            PerformanceProfiler? profiler = null;
            try
            {
                OnUi(() =>
                {
                    profiler = PerformanceProfiler.Start(new() { FrameCapacity = 2048, TrackAllocations = true, TrackGarbageCollections = true });
                    shape.Rotation = 0;
                    animation = Animation.Parallel(shape.RotateTo(180, Options(3000)), shape.ScaleTo(1.15f, Options(3000)))
                        .Start(AnimationScheduler.Default);
                    return true;
                });
                // OEMs may apply a preferred mode only while frames are being produced. Read the
                // reported mode during work, and retain callback intervals separately from it.
                Thread.Sleep(300);
                float actual = OnUi(() => View().Display!.RefreshRate);
                WaitFor(() => animation!.Completion.IsCompleted, "animation-completed", 20000);
                var snapshot = OnUi(() => profiler!.Capture());
                Check(snapshot.TotalFrames > 10 && snapshot.RecorderFailures == 0, "production-profiler-recorded-frames");
                Check(snapshot.Frames.All(frame => frame.Completed), "all-native-render-callbacks-completed");
                var frames = snapshot.Frames.Where(frame => frame.RenderInfo.Backend == "Android").ToArray();
                Check(frames.Length > 10 && frames.Any(frame => frame.RenderInfo.PixelWidth > 0), "android-backing-observed");
                var csv = new StringBuilder("sequence,start_ms,duration_ms,interval_ms,allocated_bytes,boundary,pixel_width,pixel_height\n");
                foreach (var frame in frames) csv.AppendLine(FormattableString.Invariant($"{frame.Sequence},{frame.Started.TotalMilliseconds},{frame.Duration.TotalMilliseconds},{frame.Interval?.TotalMilliseconds},{frame.AllocatedBytes},{frame.RenderInfo.Boundary},{frame.RenderInfo.PixelWidth},{frame.RenderInfo.PixelHeight}"));
                File.WriteAllText(FilePath.Combine(output, $"frames-{requested}.csv"), csv.ToString());
                report.AppendLine(FormattableString.Invariant($"RATE requested={requested} actual={actual} frames={frames.Length} total={snapshot.TotalFrames} recorder_failures={snapshot.RecorderFailures} gpu_time=UNAVAILABLE scanout=UNAVAILABLE"));
                Capture($"shapes-{requested}.png");
            }
            finally { OnUi(() => { profiler?.Dispose(); return true; }); }
        }
    }

    private void CheckBackground()
    {
        OnUi(() => { shape.Rotation = 0; animation = shape.RotateTo(180, Options(5000)).Start(AnimationScheduler.Default); return true; });
        Thread.Sleep(150);
        SendKeyDownUpSync(Keycode.Home);
        WaitFor(() => OnUi(() => Runtime().ActiveSurfaceCount == 0 && !Runtime().FrameCallbackPending), "background-no-surface");
        var paused = OnUi(() => (shape.Rotation, Runtime().DeliveredFrameCallbackCount));
        Thread.Sleep(20000); // Explicit background observation, on instrumentation thread only.
        Check(OnUi(() => shape.Rotation == paused.Rotation && Runtime().DeliveredFrameCallbackCount == paused.DeliveredFrameCallbackCount), "background-no-animation-or-frame-work");
        using var intent = LaunchIntent();
        // SingleTop resumes the existing Activity; StartActivitySync waits for a newly created
        // Activity and is unsuitable for this path. Observe the production lifecycle instead.
        OnUi(() => { TargetContext!.StartActivity(intent); return true; });
        WaitFor(() => OnUi(() => Runtime().ActiveSurfaceCount == 1), "foreground-rebound");
        Check(OnUi(() => shape.Rotation < 175), "resume-does-not-consume-background-time");
        WaitFor(() => animation!.Completion.IsCompleted, "resumed-animation-completed", 20000);
        report.AppendLine("BACKGROUND seconds=20 callbacks_stopped=True state_retained=True");
    }

    private void CheckOrientation()
    {
        string[] values = OnUi(() => editors.Select(editor => editor.Text).ToArray());
        foreach (bool landscape in new[] { true, false })
        {
            OnUi(() =>
            {
                activity.RequestedOrientation = landscape ? global::Android.Content.PM.ScreenOrientation.Landscape
                    : global::Android.Content.PM.ScreenOrientation.Portrait;
                return true;
            });
            WaitFor(() => OnUi(() => (View().Width > View().Height) == landscape
                && (app.Root.Width > app.Root.Height) == landscape), "native-and-shared-orientation-updated");
            Check(OnUi(() => Runtime().ActiveSurfaceCount == 1 && editors.Select(editor => editor.Text).SequenceEqual(values)),
                "orientation-retains-surface-and-editor-state");
        }
        // This fixture is intentionally portrait-sized. Rotation validates the host's viewport
        // and state, not a responsive layout claim for these fixed-position diagnostic controls.
        report.AppendLine("ORIENTATION=landscape-portrait; shared_viewport_updated=True state_retained=True");
    }

    private void CheckRecreation()
    {
        string[] values = OnUi(() => editors.Select(editor => editor.Text).ToArray());
        for (int cycle = 0; cycle < 12; cycle++)
        {
            OnUi(() => { retiredViews.Add(new WeakReference(View())); activity.Recreate(); return true; });
            var previous = activity;
            WaitFor(() => OnUi(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity current
                && !ReferenceEquals(current, previous) && Runtime().ActiveSurfaceCount == 1 && current.HasWindowFocus), "activity-recreated");
            OnUi(() => { activity = (MainActivity)AndroidWindowKit.Current.ActivityTracker.CurrentActivity!; activity.Window!.AddFlags(WindowManagerFlags.KeepScreenOn); return true; });
            Check(OnUi(() => ReferenceEquals(((SampleApplication)activity.Application!).SharedApp, app)
                && editors.Select(editor => editor.Text).SequenceEqual(values)), "recreation-retains-shared-editor-state");
            Check(OnUi(() => Runtime().ActiveSurfaceCount == 1), "recreation-exactly-one-active-surface");
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long pss = global::Android.OS.Debug.Pss;
            report.AppendLine($"MEMORY cycle={cycle + 1} managed_bytes={GC.GetTotalMemory(false)} pss_kib={pss} retired_views_alive={retiredViews.Count(reference => reference.IsAlive)}");
        }
    }

    private Intent LaunchIntent() => new Intent(TargetContext!, typeof(MainActivity)).AddFlags(ActivityFlags.NewTask);
    private AndroidSkiaHostView View() => (AndroidSkiaHostView)activity.FindViewById<ViewGroup>(global::Android.Resource.Id.Content)!.GetChildAt(0)!;
    private static AndroidAnimationRuntimeDiagnostics Runtime()
        => AndroidWindowKit.Current.GetAnimationRuntimeDiagnostics();
    private static AnimationOptions Options(int milliseconds) => new() { Duration = TimeSpan.FromMilliseconds(milliseconds), Easing = Easings.Linear };
    private static IEnumerable<Control> Descendants(Control parent)
    { foreach (var child in parent.Controls) { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; } }
    private void Capture(string file)
    {
        using var bitmap = UiAutomation!.TakeScreenshot();
        Check(bitmap is not null, "native-screenshot-available");
        using var stream = File.Create(FilePath.Combine(output, file));
        bitmap!.Compress(global::Android.Graphics.Bitmap.CompressFormat.Png!, 100, stream);
    }
    private T OnUi<T>(Func<T> operation)
    {
        T value = default!; Exception? failure = null;
        RunOnMainSync(() => { try { value = operation(); } catch (Exception error) { failure = error; } });
        if (failure is not null) throw new InvalidOperationException("UI validation failed.", failure);
        return value;
    }
    private void WaitFor(Func<bool> predicate, string category, int timeout = 10000)
    {
        long until = Environment.TickCount64 + timeout;
        do { if (predicate()) { Check(true, category); return; } Thread.Sleep(50); } while (Environment.TickCount64 < until);
        Check(false, category);
    }
    private void Check(bool condition, string category)
    {
        if (!condition) { report.AppendLine($"FAIL category={category}"); throw new InvalidOperationException(category); }
        assertions++;
    }
}
