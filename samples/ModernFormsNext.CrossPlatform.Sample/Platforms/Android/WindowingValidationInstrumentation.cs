using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using ModernFormsNext.WindowKit.Backend.Android.Windowing;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Threading;
using System.Diagnostics;
using System.Text;
using AppLifetime = ModernFormsNext.Application;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Explicit native acceptance runner for the Application/Form Activity host.</summary>
/// <remarks>
/// Run with adb instrumentation. Exercises production Views, main Looper, IME connection and
/// native Back dispatch; it does not establish physical-device or vendor-keyboard qualification.
/// The normal sample never runs these fixtures. Its report contains categories, not editor payloads.
/// </remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.WindowingValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class WindowingValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private MainActivity activity = null!;
    private Form main = null!;
    private TextBox editor = null!;
    private int assertions;
    private string stage = "launch";
    /// <summary>Creates the native window runner.</summary>
    public WindowingValidationInstrumentation() { }
    /// <summary>Reattaches the managed runner to Android's Java instance.</summary>
    /// <param name="handle">The Java handle.</param>
    /// <param name="ownership">JNI ownership transfer.</param>
    public WindowingValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership)
        : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }
    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        bool success = false;
        try
        {
            _ = UiAutomation;
            using var launch = new Intent(TargetContext!, typeof(MainActivity));
            launch.AddFlags(ActivityFlags.NewTask);
            activity = (MainActivity)StartActivitySync(launch)!;
            Wait(() => Ui(() => Snapshot().Windows.Any(w => w.Main && w.Active && w.PaintCount > 0)), "native-started");
            main = Ui(() => AppLifetime.OpenForms.Single());
            Check(Ui(() => Dispatcher.UIThread.HasExternalEventLoop && !Dispatcher.UIThread.SupportsRunLoops),
                "Run-returned-with-main-Looper");
            Check(Ui(() => main.Visible && !main.TitleBar.Visible), "host-managed-chrome");
            Check(Ui(() => Snapshot().Windows.Single().Density > 1 &&
                Math.Abs(main.Size.Width * main.Scaling - View().Width) <= main.Scaling), "confirmed-density-once");
            stage = "dispatcher";
            int thread = Ui(() => System.Environment.CurrentManagedThreadId);
            int posted = 0, timer = 0, canceled = 0;
            Dispatcher.UIThread.Post(() => posted = System.Environment.CurrentManagedThreadId);
            using var tick = Ui(() => DispatcherTimer.RunOnce(() => timer = System.Environment.CurrentManagedThreadId, TimeSpan.FromMilliseconds(40)));
            Ui(() => { var cancel = DispatcherTimer.RunOnce(() => canceled++, TimeSpan.FromMilliseconds(30)); cancel.Dispose(); return true; });
            Wait(() => Volatile.Read(ref posted) != 0 && Volatile.Read(ref timer) != 0, "dispatcher-post-and-timer");
            Check(posted == thread && timer == thread && canceled == 0, "dispatcher-main-thread-cancel");
            Check(Dispatcher.UIThread.InvokeAsync(() => System.Environment.CurrentManagedThreadId).GetAwaiter().GetResult() == thread,
                "dispatcher-invoke-await-completion");

            stage = "focus-validation-ime";
            var target = Ui(() =>
            {
                foreach (var control in main.Controls) control.Visible = false;
                editor = main.Controls.Add(new TextBox { Bounds = new(10, 10, 210, 40), Text = "seed" });
                var button = main.Controls.Add(new Button { Bounds = new(10, 70, 210, 40), Text = "Native window target" });
                editor.Select();
                return button;
            });
            Wait(() => Ui(() => main.TextInputClient is not null), "canonical-text-client");
            ITextInputClient? retired = null;
            Ui(() =>
            {
                using var info = new EditorInfo();
                var connection = View().OnCreateInputConnection(info)!; // Borrowed; the View owns it.
                Check(connection.SetComposingText("compose", 1), "native-IME-composition");
                Check(connection.FinishComposingText(), "native-IME-finish");
                retired = main.TextInputClient;
                int clicks = 0, validations = 0;
                bool cancel = true;
                editor.Validating += (_, e) => { validations++; e.Cancel = cancel; };
                target.Click += (_, _) => clicks++;
                Tap(target);
                Check(editor.Focused && clicks == 0 && validations == 1 && ReferenceEquals(retired, main.TextInputClient),
                    "native-touch-validation-cancel");
                cancel = false;
                Tap(target);
                Check(target.Focused && clicks == 1 && validations == 2 && retired!.GetState() is null,
                    "native-touch-validation-commit");
                editor.Select();
                View().HideSoftKeyboard();
                return true;
            });
            int command = 0;
            Ui(() => { main.InputBindings.Add(new KeyBinding(new DelegateCommand(() => command++), new KeyGesture(Keys.F1))); return true; });
            Wait(() => Ui(() => View().HasWindowFocus && View().IsFocused && main.IsActive), "native-keyboard-focus-ready");
            // System injection can round-trip through the active IME and is then intentionally
            // classified as editing input. Test the hardware boundary with native View events;
            // this is synthetic source evidence, not a physical keyboard qualification.
            Ui(() =>
            {
                long keyTime = SystemClock.UptimeMillis();
                using var down = new KeyEvent(keyTime, keyTime, KeyEventActions.Down, Keycode.F1, 0, 0, 0, 0,
                    KeyEventFlags.FromSystem, InputSourceType.Keyboard);
                using var up = new KeyEvent(keyTime, keyTime + 1, KeyEventActions.Up, Keycode.F1, 0, 0, 0, 0,
                    KeyEventFlags.FromSystem, InputSourceType.Keyboard);
                View().DispatchKeyEvent(down); View().DispatchKeyEvent(up);
                return true;
            });
            Wait(() => Ui(() => command == 1), "native-hardware-form-binding");

            stage = "popup-modal";
            var popup = Ui(() => new PopupWindow(main) { Size = new(200, 120) });
            Ui(() => { popup.Controls.Add(new Button { Text = "Popup", Dock = DockStyle.Fill }); popup.Show(20, 30); return true; });
            Wait(() => Ui(() => Snapshot().Windows.Any(w => w.Popup && w.Attached)), "popup-native-attached");
            BackWithoutIme();
            Wait(() => Ui(() => !popup.Visible && main.Visible), "native-Back-dismisses-popup");
            Ui(() => { popup.Show(20, 30); popup.Hide(); popup.Show(20, 30); popup.Hide(); return true; });
            Check(Ui(() => Snapshot().Windows.Count(w => w.Popup) == 1), "popup-reusable");
            var dialog = Ui(() => new Form { Size = new(240, 180), Text = "Native modal" });
            var dialogResult = Ui(() => dialog.ShowDialog(main));
            Wait(() => Ui(() => Snapshot().Windows.Any(w => w.Modal && w.Active)), "modal-native-focused");
            BackWithoutIme();
            Wait(() => dialogResult.IsCompleted, "modal-task-completed-by-Back");
            Check(Ui(() => main.Visible && AppLifetime.OpenForms.Count == 1), "modal-owner-restored");
            Wait(() => Ui(() => main.IsActive && main.TextInputClient is not null), "modal-owner-native-focus-restored");

            stage = "presentation-reentrancy";
            long paints = Ui(() => Snapshot().Windows.Single(w => w.Main).PaintCount);
            Check(Ui(() =>
            {
                var currentView = View();
                main.Show(); main.Invalidate();
                return ReferenceEquals(currentView, View());
            }), "repeated-Show-keeps-native-View");
            Wait(() => Ui(() => Snapshot().Windows.Single(w => w.Main).PaintCount > paints), "repeated-Show-keeps-rendering");

            // Deliberately create the inner dialog first: stacking follows presentation order.
            var inner = Ui(() => new Form { Size = new(180, 140) });
            var outer = Ui(() => new Form { Size = new(260, 220) });
            var outerResult = Ui(() => outer.ShowDialog(main));
            var innerResult = Ui(() => inner.ShowDialog(outer));
            BackWithoutIme();
            Wait(() => innerResult.IsCompleted, "Back-closes-last-shown-modal");
            Check(!outerResult.IsCompleted, "Back-preserves-outer-modal");
            BackWithoutIme();
            Wait(() => outerResult.IsCompleted, "Back-closes-remaining-modal");
            Ui(() => { inner.Dispose(); outer.Dispose(); return true; });

            var reentrant = Ui(() => new Form { Size = new(240, 180) });
            Ui(() =>
            {
                // Hide intentionally clears canonical selection. Select explicitly after the
                // newer Show, as application code must; recreation alone preserves selection.
                reentrant.Closed += (_, _) => { main.Show(); editor.Select(); };
                _ = reentrant.ShowDialog(main);
                main.Hide();
                return true;
            });
            Wait(() => Ui(() => main.Visible && main.IsActive && main.TextInputClient is not null &&
                Snapshot().Windows.Single(w => w.Main).Attached), "reentrant-Show-survives-owner-Hide");
            paints = Ui(() => Snapshot().Windows.Single(w => w.Main).PaintCount);
            Ui(() => { main.Invalidate(); reentrant.Dispose(); return true; });
            Wait(() => Ui(() => Snapshot().Windows.Single(w => w.Main).PaintCount > paints), "reentrant-Show-renders-new-presentation");

            stage = "background-popup";
            Ui(() => { popup.Show(20, 30); return true; });
            SendKeyDownUpSync(Keycode.Home);
            Wait(() => Ui(() => !main.IsActive && !popup.Visible), "background-retires-popup-during-deactivation");
            TargetContext!.StartActivity(launch);
            Wait(() => Ui(() => main.IsActive && main.TextInputClient is not null), "foreground-restores-owner-input");

            stage = "hide-show";
            Ui(() => { main.Hide(); return true; });
            Check(Ui(() => !main.Visible && Snapshot().Windows.Where(w => w.Main).All(w => !w.Attached)), "hide-retires-native-presentation");
            Ui(() => { main.Show(); editor.Select(); View().HideSoftKeyboard(); return true; });
            Wait(() => Ui(() => main.IsActive && main.TextInputClient is not null), "show-reacquires-input");
            Check(Ui(() => AppLifetime.OpenForms.Count == 1), "OpenForms-single-membership");

            stage = "recreation";
            var retiredActivities = new List<WeakReference>();
            var retiredViews = new List<WeakReference>();
            for (int i = 0; i < 5; i++)
            {
                long before = Ui(() => Snapshot().HostGeneration);
                var oldClient = Ui(() => main.TextInputClient);
                string text = Ui(() => editor.Text);
                Ui(() =>
                {
                    retiredActivities.Add(new WeakReference(activity));
                    retiredViews.Add(new WeakReference(View()));
                    activity.Recreate();
                    return true;
                });
                Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity current &&
                    !ReferenceEquals(current, activity) && Snapshot().Windows.Any(w => w.Main && w.Active)), "replacement-resumed");
                activity = Ui(() => (MainActivity)AndroidWindowKit.Current.ActivityTracker.CurrentActivity!);
                Check(Ui(() => ReferenceEquals(main, AppLifetime.OpenForms.Single()) && editor.Text == text && editor.Focused &&
                    Snapshot().HostGeneration > before && Snapshot().Windows.Count(w => w.Main && w.Attached) == 1),
                    "recreation-preserves-canonical-Form-tree");
                Check(Ui(() => oldClient?.GetState() is null && main.TextInputClient is not null), "recreation-retires-old-IME-session");
                Ui(() => { View().HideSoftKeyboard(); return true; });
            }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Check(retiredViews.All(reference => !reference.IsAlive), "retired-native-Views-not-retained");
            Check(retiredActivities.All(reference => !reference.IsAlive), "retired-Activities-not-retained");
            stage = "close";
            Ui(() => { popup.Dispose(); dialog.Dispose(); return true; });
            int closing = 0, closed = 0, exits = 0;
            bool prevent = true;
            Ui(() =>
            {
                main.Closing += (_, e) => { closing++; e.Cancel = prevent; };
                main.Closed += (_, _) => closed++;
                AppLifetime.OnExit += (_, _) => exits++;
                return true;
            });
            BackWithoutIme();
            Wait(() => Ui(() => closing > 0), "native-Back-requests-Closing");
            Check(Ui(() => main.Visible && closed == 0), "native-Back-cancel-preserves-main");
            Ui(() => { prevent = false; return true; });
            BackWithoutIme();
            Wait(() => Ui(() => closed == 1 && exits == 1), "native-Back-terminal-lifetime");
            Check(Ui(() => AppLifetime.OpenForms.Count == 0 && !Snapshot().Active && !Snapshot().Attached &&
                Snapshot().Windows.Count == 0), "exit-clears-native-and-framework-window-registry");
            int afterExit = 0;
            Dispatcher.UIThread.Post(() => afterExit++);
            Wait(() => Volatile.Read(ref afterExit) == 1, "process-main-Looper-survives-framework-exit");
            success = true;
        }
        catch (Exception error)
        {
            try
            {
                report.AppendLine(Ui(() =>
                {
                    var current = AndroidWindowKit.Current.ActivityTracker.CurrentActivity;
                    var d = Snapshot();
                    var views = current is null ? [] : NativeValidationViews.Find(
                        current.FindViewById<ViewGroup>(global::Android.Resource.Id.Content)!).ToArray();
                    return $"DIAGNOSTICS gen={d.HostGeneration} attached={d.Attached} lifecycle={d.ActivityState} " +
                        $"replacement={!ReferenceEquals(current, activity)} windows={string.Join(";", d.Windows.Select(w =>
                            $"main={w.Main},visible={w.Visible},attached={w.Attached},active={w.Active}"))} " +
                        $"views={string.Join(";", views.Select(v => $"focus={v.IsFocused},windowFocus={v.HasWindowFocus},state={v.HostState.LifecycleState}"))}";
                }));
            }
            catch { report.AppendLine("DIAGNOSTICS unavailable"); }
            // Exception type/stack aid diagnosis without including arbitrary editor payloads.
            report.AppendLine($"FAIL stage={stage}; type={error.GetType().Name}; stack={error.StackTrace}");
        }
        finally
        {
            report.AppendLine($"ANDROID_WINDOWING_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            string output = System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "windowing-validation.txt");
            File.WriteAllText(output, report.ToString());
            result.PutString("stream", report.ToString());
            Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }

    private AndroidWindowingDiagnostics Snapshot() => AndroidWindowKit.Current.GetWindowingDiagnostics();
    private AndroidSkiaHostView View() => NativeValidationViews.Main(activity);
    private void BackWithoutIme()
    {
        Ui(() => { main.RequestSoftwareKeyboard(false); View().HideSoftKeyboard(); return true; });
        // Android's IME owns Back while visible. Assert the platform state before testing
        // framework window closure; do not confuse keyboard dismissal with a lost callback.
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            Wait(() => Ui(() => !OperatingSystem.IsAndroidVersionAtLeast(30) ||
                View().RootWindowInsets?.IsVisible(global::Android.Views.WindowInsets.Type.Ime()) != true),
                "native-IME-hidden-before-window-Back");
        // Insets turn invisible before Android removes the IME's Back callback. Wait for
        // native accessibility/window transitions to become idle, not an arbitrary retry of
        // Back (which could close two windows). The IME still owns the first Back during hide.
        WaitForIdleSync();
        UiAutomation!.WaitForIdle(300, 5000);
        SendKeyDownUpSync(Keycode.Back);
    }
    private void Tap(Control control)
    {
        var screen = control.PointToScreen(new System.Drawing.Point(control.ScaledWidth / 2, control.ScaledHeight / 2));
        int[] origin = new int[2]; View().GetLocationOnScreen(origin);
        long now = SystemClock.UptimeMillis();
        using var down = MotionEvent.Obtain(now, now, MotionEventActions.Down, screen.X - origin[0], screen.Y - origin[1], 0)!;
        using var up = MotionEvent.Obtain(now, now + 1, MotionEventActions.Up, screen.X - origin[0], screen.Y - origin[1], 0)!;
        View().DispatchTouchEvent(down); View().DispatchTouchEvent(up);
    }
    private T Ui<T>(Func<T> action)
    {
        T value = default!;
        Exception? failure = null;
        RunOnMainSync(() => { try { value = action(); } catch (Exception e) { failure = e; } });
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return value;
    }
    private void Wait(Func<bool> condition, string category)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (condition()) { Check(true, category); return; }
            Thread.Sleep(25);
        }
        Check(false, category);
    }
    private void Check(bool condition, string category)
    {
        if (!condition) { report.AppendLine("FAILED_CHECK=" + category); throw new InvalidOperationException(category); }
        assertions++;
        report.AppendLine("PASS " + category);
        global::Android.Util.Log.Info("MFN.Windowing", "PASS " + category);
    }
}
