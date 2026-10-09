using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Input;
using System.Diagnostics;
using System.Text;
using NativeView = Android.Views.View;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Runs bounded real-View hosting checks exclusively through adb instrumentation.</summary>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.NativeViewValidationInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class NativeViewValidationInstrumentation : Instrumentation
{
    private readonly StringBuilder report = new();
    private int assertions;
    private MainActivity activity = null!;
    private Form main = null!;
    /// <summary>Creates the native acceptance runner.</summary>
    public NativeViewValidationInstrumentation() { }
    /// <summary>Reattaches the managed runner to its Java instance.</summary>
    /// <param name="handle">The Java handle.</param>
    /// <param name="ownership">JNI ownership.</param>
    public NativeViewValidationInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership)
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
            using var intent = new Intent(TargetContext!, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.NewTask);
            activity = (MainActivity)StartActivitySync(intent)!;
            Wait(() => Ui(() => ModernFormsNext.Application.OpenForms.Any() &&
                AndroidWindowKit.Current.GetWindowingDiagnostics().Windows.Any(w => w.Main && w.Active && w.PaintCount > 0)), "Application-Run-real-presentation");
            main = Ui(() => ModernFormsNext.Application.OpenForms.Single());
            var first = new Factory(editable: true);
            var second = new Factory(editable: false);
            var fixture = Ui(() =>
            {
                foreach (var control in main.Controls) control.Visible = false;
                var panel = main.Controls.Add(new Panel { Bounds = new(10, 10, 230, 180), AutoScroll = true });
                var editor = main.Controls.Add(new TextBox { Bounds = new(10, 210, 230, 40), TabIndex = 0 });
                var next = main.Controls.Add(new TextBox { Bounds = new(10, 260, 230, 40), TabIndex = 3 });
                var a = panel.Controls.Add(new NativeViewHost { Bounds = new(15, 15, 150, 55), TabIndex = 1, PeerFactory = first });
                var b = panel.Controls.Add(new NativeViewHost { Bounds = new(15, 85, 150, 55), TabIndex = 2, PeerFactory = second });
                panel.Controls.Add(new Control { Bounds = new(400, 400, 10, 10) });
                return (panel, editor, next, a, b);
            });
            Wait(() => Ui(() => first.Views.Count == 1 && second.Views.Count == 1 && first.Views[0].Width > 1), "two-real-native-peers");
            Ui(() =>
            {
                var a = fixture.a.HostingDiagnostics.Placement;
                var view = first.Views.Last();
                Check(view.Parent?.Equals(first.Sites.Last().Container) == true, "native-container-parent");
                Check(view.Width == a.PixelBounds.Width && view.Height == a.PixelBounds.Height, "density-scaled-once");
                Check(first.Sites.Last().Container.Left == a.PixelBounds.X && first.Sites.Last().Container.Top == a.PixelBounds.Y, "client-relative-position");
                var skia = NativeValidationViews.Main(activity);
                var root = (ViewGroup)skia.Parent!;
                Check(root.IndexOfChild(skia) == 0 && root.ChildCount == 3, "one-Skia-presentation-with-native-siblings");
                Check(root.IndexOfChild(first.Sites.Last().Container) < root.IndexOfChild(second.Sites.Last().Container), "control-order-native-band");
                fixture.a.BringToFront();
                Check(root.IndexOfChild(first.Sites.Last().Container) > root.IndexOfChild(second.Sites.Last().Container), "native-BringToFront");
                fixture.panel.VerticalScrollProperties.Value = 45;
                Check(fixture.a.HostingDiagnostics.Placement.PixelClip.Height < a.PixelBounds.Height, "scroll-partial-clip");
                Check(first.Views.Count == 1, "scroll-retains-peer");
                using var clip = first.Sites.Last().Container.ClipBounds;
                Check(clip is not null && clip.Height() < a.PixelBounds.Height, "real-Android-rectangular-clip");
                fixture.editor.Select();
                long time = SystemClock.UptimeMillis();
                using var clippedDown = MotionEvent.Obtain(time, time, MotionEventActions.Down, 1, 1, 0)!;
                Check(!first.Sites.Last().Container.DispatchTouchEvent(clippedDown) && fixture.editor.Focused,
                    "clipped-native-airspace-rejects-pointer-entry");
                fixture.panel.Visible = false;
                Check(first.Sites.Last().Container.Visibility == ViewStates.Invisible, "ancestor-hide");
                fixture.panel.Visible = true;
                fixture.panel.Enabled = false;
                Check(!view.Enabled, "effective-native-enabled");
                fixture.panel.Enabled = true;
                fixture.panel.Opacity = .5f;
                Check(first.Sites.Last().Container.Visibility == ViewStates.Invisible, "unsupported-opacity-hidden");
                fixture.panel.Opacity = 1;
                fixture.panel.VerticalScrollProperties.Value = 0;
                Check(first.Sites.Last().Container.Visibility == ViewStates.Visible, "supported-composition-restored");
                return true;
            });
            WaitForIdleSync();
            int sharedClicks = 0, nativeClicks = 0, routed = 0;
            Ui(() =>
            {
                bool veto = true;
                fixture.editor.Validating += (_, e) => e.Cancel = veto;
                fixture.editor.Select();
                first.Views.Last().RequestFocus();
                Check(fixture.editor.Focused && !first.Views.Last().IsFocused, "native-focus-validation-veto");
                veto = false;
                first.Views.Last().RequestFocus();
                Check(fixture.a.Focused && first.Views.Last().IsFocused, "native-focus-canonical-commit");
                fixture.next.Select();
                Check(fixture.next.Focused && NativeValidationViews.Main(activity).IsFocused, "native-to-MFN-focus");
                fixture.a.Select();
                Check(first.Views.Last().IsFocused, "MFN-to-native-focus");
                using var tab = new KeyEvent(KeyEventActions.Down, Keycode.Tab);
                first.Sites.Last().Container.DispatchKeyEvent(tab);
                Check(fixture.b.Focused, "native-Tab-existing-navigation");
                var reentrantFactory = new Factory(editable: false, focusAction: fixture.next.Select);
                var reentrant = main.Controls.Add(new NativeViewHost { Bounds = new(250, 260, 100, 40), PeerFactory = reentrantFactory });
                reentrant.Select();
                Check(fixture.next.Focused && NativeValidationViews.Main(activity).IsFocused,
                    "reentrant-native-focus-preserves-newer-canonical-owner");
                reentrant.Dispose();
                Check(reentrantFactory.Views.Single().Handle == IntPtr.Zero && !reentrantFactory.Sites.Single().IsCurrent,
                    "reentrant-focus-peer-and-container-retired");
                fixture.b.Click += (_, _) => sharedClicks++;
                second.Views.Last().Click += (_, _) => nativeClicks++;
                main.InputBindings.Add(new KeyBinding(new DelegateCommand(() => routed++), new KeyGesture(Keys.F1)));
                return true;
            });
            var point = Ui(() =>
            {
                var view = second.Views.Last();
                int[] location = new int[2]; view.GetLocationOnScreen(location);
                return (x: location[0] + view.Width / 2f, y: location[1] + view.Height / 2f);
            });
            long now = SystemClock.UptimeMillis();
            using (var down = MotionEvent.Obtain(now, now, MotionEventActions.Down, point.x, point.y, 0)!)
            using (var up = MotionEvent.Obtain(now, now + 1, MotionEventActions.Up, point.x, point.y, 0)!)
            { down.SetSource(InputSourceType.Touchscreen); up.SetSource(InputSourceType.Touchscreen); SendPointerSync(down); SendPointerSync(up); }
            Wait(() => Ui(() => nativeClicks == 1 && sharedClicks == 0), "native-pointer-not-duplicated-to-Skia");
            Check(Ui(() => second.Sites.Last().Container.HasFocus), "native-button-touch-mode-keyboard-boundary");
            SendKeyDownUpSync(Keycode.F1);
            Ui(() =>
            {
                Check(routed == 0, "native-keyboard-not-duplicated-to-Skia");
                fixture.a.Select();
                var imm = (InputMethodManager)activity.GetSystemService(Context.InputMethodService)!;
                imm.ShowSoftInput(first.Views.Last(), ShowFlags.Implicit);
                return true;
            });
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
                Wait(() => Ui(() => OperatingSystem.IsAndroidVersionAtLeast(30) && first.Views.Last().RootWindowInsets?.IsVisible(global::Android.Views.WindowInsets.Type.Ime()) == true), "native-EditText-soft-keyboard");
            Ui(() =>
            {
                var imm = (InputMethodManager)activity.GetSystemService(Context.InputMethodService)!;
                imm.HideSoftInputFromWindow(first.Views.Last().WindowToken, HideSoftInputFlags.None);
                var combo = main.Controls.Add(new ComboBox { Bounds = new(10, 310, 230, 40) });
                combo.Items.Add("one"); combo.Items.Add("two");
                combo.DroppedDown = true;
                var surfaces = NativeValidationViews.Find(activity.FindViewById<ViewGroup>(global::Android.Resource.Id.Content)!).ToArray();
                var activityRoot = (ViewGroup)surfaces[0].Parent!.Parent!;
                Check(surfaces.Length == 2 && activityRoot.IndexOfChild((NativeView)surfaces[1].Parent!) >
                    activityRoot.IndexOfChild((NativeView)surfaces[0].Parent!), "popup-presentation-above-owner-native-band");
                combo.DroppedDown = false; combo.Dispose();
                return true;
            });
            Form modal = Ui(() => new Form { ClientSize = new(220, 130) });
            Ui(() => { _ = modal.ShowDialog(main); return true; });
            Wait(() => Ui(() => NativeValidationViews.Find(activity.FindViewById<ViewGroup>(global::Android.Resource.Id.Content)!).Count() == 2), "modal-presentation-above-owner-native-band");
            Ui(() =>
            {
                Check(!first.Views.Last().Enabled, "modal-disables-owner-native-peer");
                modal.Controls.Add(fixture.a);
                Check(first.Views.Count == 2 && !first.Sites[0].IsCurrent && first.Views[0].Handle == IntPtr.Zero,
                    "cross-window-reparent-retires-old-peer");
                return true;
            });
            Wait(() => Ui(() => fixture.a.HostingDiagnostics.State == NativeViewHostState.Attached && first.Views.Last().Width > 1),
                "cross-window-reparent-attaches-modal-peer");
            Ui(() =>
            {
                fixture.panel.Controls.Add(fixture.a);
                Check(first.Views.Count == 3 && !first.Sites[1].IsCurrent && first.Views[1].Handle == IntPtr.Zero,
                    "return-to-owner-recreates-peer");
                modal.DialogResult = DialogResult.OK;
                Check(first.Views.Last().Enabled, "modal-close-restores-native-peer");
                modal.Dispose();
                var other = main.Controls.Add(new Panel { Bounds = new(250, 10, 230, 180) });
                other.Controls.Add(fixture.a);
                Check(first.Views.Count == 3, "same-window-reparent-retains-peer");
                var borrowedFactory = new Factory(editable: false, ownsView: false);
                var borrowed = main.Controls.Add(new NativeViewHost { Bounds = new(10, 360, 150, 45), PeerFactory = borrowedFactory });
                var view = borrowedFactory.Views.Single();
                borrowed.Dispose();
                Check(view.Handle != IntPtr.Zero && view.Parent is null && !borrowedFactory.Sites.Single().IsCurrent,
                    "borrowed-View-detached-without-destruction");
                view.Dispose();
                return true;
            });
            // Native config delivery exercises production geometry, without mutating shared bounds.
            var originalOrientation = Ui(() => activity.RequestedOrientation);
            Ui(() => { activity.RequestedOrientation = ScreenOrientation.Landscape; return true; });
            Wait(() => Ui(() => NativeValidationViews.Main(activity).Width > NativeValidationViews.Main(activity).Height), "configuration-geometry");
            var oldSite = first.Sites.Last();
            int created = first.Views.Count;
            var oldActivity = activity;
            Ui(() => { activity.Recreate(); return true; });
            Wait(() => Ui(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity current && !ReferenceEquals(current, oldActivity)), "Activity-replacement");
            activity = Ui(() => (MainActivity)AndroidWindowKit.Current.ActivityTracker.CurrentActivity!);
            Wait(() => Ui(() => first.Views.Count > created && fixture.a.HostingDiagnostics.State == NativeViewHostState.Attached), "recreated-native-peer");
            Ui(() =>
            {
                Check(!oldSite.IsCurrent && !oldSite.TryFocus(), "stale-Activity-site-rejected");
                Check(ReferenceEquals(main, ModernFormsNext.Application.OpenForms.Single()), "shared-Form-survives-recreation");
                Check(first.Views[created - 1].Handle == IntPtr.Zero, "old-owned-View-released");
                activity.RequestedOrientation = originalOrientation;
                activity.MoveTaskToBack(true);
                return true;
            });
            Wait(() => Ui(() => fixture.a.HostingDiagnostics.State == NativeViewHostState.Suspended), "background-native-suspended");
            using var resume = new Intent(TargetContext!, typeof(MainActivity)); resume.AddFlags(ActivityFlags.NewTask);
            TargetContext!.StartActivity(resume);
            Wait(() => Ui(() => fixture.a.HostingDiagnostics.State == NativeViewHostState.Attached), "resume-native-restored");
            Ui(() =>
            {
                var latest = first.Sites.Last();
                fixture.a.Dispose(); fixture.b.Dispose();
                Check(!latest.IsCurrent && !latest.TryFocus(), "disposed-site-rejected");
                Check(first.Views.Concat(second.Views).All(v => v.Handle == IntPtr.Zero), "zero-retained-owned-Views");
                Check(first.Sites.Concat(second.Sites).All(s => s.Container.Handle == IntPtr.Zero), "zero-retained-native-containers");
                return true;
            });
            success = true;
        }
        catch (Exception error) { report.AppendLine($"FAIL {error.GetType().Name}: {error.Message}; {error.StackTrace}"); }
        finally
        {
            report.AppendLine($"ANDROID_NATIVE_VIEW_{(success ? "PASS" : "FAIL")} assertions={assertions}");
            File.WriteAllText(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, "native-view-validation.txt"), report.ToString());
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
    private void Wait(Func<bool> condition, string category)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15))
        { if (condition()) { Check(true, category); return; } Thread.Sleep(25); }
        Check(false, category);
    }
    private void Check(bool condition, string category)
    { if (!condition) throw new InvalidOperationException(category); assertions++; report.AppendLine("PASS " + category); }
    private sealed class Factory(bool editable, bool ownsView = true, Action? focusAction = null) : INativeViewFactory
    {
        internal readonly List<NativeView> Views = [];
        internal readonly List<AndroidNativeViewSite> Sites = [];
        public INativeViewPeer CreatePeer(INativeViewSite site)
        {
            var android = (AndroidNativeViewSite)site;
            NativeView view = editable ? new EditText(android.Context) :
                new global::Android.Widget.Button(android.Context) { Text = "Native validation" };
            if (view is EditText edit) edit.SetSingleLine(true);
            Views.Add(view); Sites.Add(android);
            var peer = new AndroidViewPeer(android, view, ownsView);
            return focusAction is null ? peer : new ReentrantFocusPeer(peer, focusAction);
        }
    }
    private sealed class ReentrantFocusPeer(INativeViewPeer peer, Action action) : INativeViewPeer
    {
        public void Resize(ModernFormsNext.WindowKit.PixelSize size) => peer.Resize(size);
        public void RequestFocus() => action();
        public bool TryMoveFocus(bool forward) => peer.TryMoveFocus(forward);
        public void Dispose() => peer.Dispose();
    }
}
