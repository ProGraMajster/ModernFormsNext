using Android.App;
using Android.Content;
using Android.OS;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using SkiaSharp;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Checks real Android night-mode changes, lifecycle and retained theme resources.</summary>
/// <remarks>The explicitly invoked test temporarily changes night mode and restores the recorded mode.</remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.SystemAppearanceInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class SystemAppearanceInstrumentation : Instrumentation
{
    private static readonly ResourceProperty<Control, Color> Background = new(
        nameof(Control.BackColor),
        static control => Color.FromArgb(control.BackColor.Alpha, control.BackColor.Red, control.BackColor.Green, control.BackColor.Blue),
        static (control, value) => control.BackColor = new SKColor(value.R, value.G, value.B, value.A));
    private readonly StringBuilder report = new();
    private int checks;

    /// <summary>Creates the opt-in Android appearance test.</summary>
    public SystemAppearanceInstrumentation() { }

    /// <summary>Reattaches the test to its native instrumentation object.</summary>
    /// <param name="handle">The native Java handle.</param>
    /// <param name="ownership">JNI reference ownership.</param>
    public SystemAppearanceInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership)
        : base(handle, ownership) { }

    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }

    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        string? originalMode = null;
        bool success = false;
        try
        {
            string state = Shell("cmd uimode night").Trim();
            const string prefix = "Night mode: ";
            if (!state.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Could not record Android night mode.");
            originalMode = state[prefix.Length..].Trim();
            if (originalMode is not ("yes" or "no" or "auto" or "custom_schedule" or "custom_bedtime"))
            {
                originalMode = null;
                throw new InvalidOperationException("Unknown Android night mode cannot be restored.");
            }

            using var launch = new Intent(TargetContext!, typeof(MainActivity));
            launch.AddFlags(ActivityFlags.NewTask);
            var activity = (MainActivity)StartActivitySync(launch)!;
            var backend = AndroidWindowKit.Current;
            Wait(() => Ui(() => backend.GetWindowingDiagnostics().Windows.Any(w => w.Main && w.Active && w.PaintCount > 0)),
                "native-form-painted");
            var settings = PlatformServiceRegistry.GetRequiredService<IPlatformThemeSettings>();
            var manager = ThemeManager.Current;
            var primary = Color.FromArgb(255, 184, 34, 136);
            Shell("cmd uimode night no");
            Wait(() => Ui(() => settings.GetPreferredVariant() == PlatformColorScheme.Light), "native-light-read");

            var fixture = Ui(() =>
            {
                var form = ModernFormsNext.Application.OpenForms.Single();
                foreach (var child in form.Controls) child.Visible = false;
                var panel = new AppearancePanel { Dock = DockStyle.Fill };
                form.Controls.Add(panel);
                panel.SetResourceReference(Background, ThemeTokens.Colors.Background.ResourceKey);
                var theme = new ThemeDefinition("sample.system-appearance", "System appearance") { Variant = ThemeVariant.System };
                theme.Colors[ThemeTokens.Colors.Primary.Name] = primary;
                if (!manager.Apply(theme, new ThemeApplyOptions { FollowSystemAppearance = true }).Success)
                    throw new InvalidOperationException("Opt-in theme apply failed.");
                return (Form: form, Panel: panel);
            });

            bool Palette(ThemeVariant variant) => Ui(() =>
            {
                var snapshot = manager.ActiveSnapshot;
                Color expected = BuiltInThemes.Get(variant).Colors[ThemeTokens.Colors.Background.Name];
                return snapshot?.Variant == variant &&
                    snapshot.Get(ThemeTokens.Colors.Background).ToArgb() == expected.ToArgb() &&
                    fixture.Panel.BackColor == new SKColor(expected.R, expected.G, expected.B, expected.A) &&
                    snapshot.Get(ThemeTokens.Colors.Primary).ToArgb() == primary.ToArgb();
            });

            Wait(() => Palette(ThemeVariant.Light), "light-palette-control-and-override");
            Capture(activity, fixture.Panel, ThemeVariant.Light, "system-appearance-light.png");
            Shell("cmd uimode night yes");
            Wait(() => Palette(ThemeVariant.Dark), "dark-palette-control-and-override");
            Capture(activity, fixture.Panel, ThemeVariant.Dark, "system-appearance-dark.png");
            long switchCount = Ui(() => manager.GetDiagnostics().SuccessfulSwitches);
            Shell("cmd uimode night yes");
            Shell("cmd uimode night yes");
            WaitForIdleSync();
            Check(Ui(() => manager.GetDiagnostics().SuccessfulSwitches == switchCount), "duplicate-mode-does-not-reapply");

            Check(UiAutomation!.PerformGlobalAction(global::Android.AccessibilityServices.GlobalAction.Home), "home");
            Wait(() => Ui(() => ModernFormsNext.Application.Lifecycle.Snapshot.State == PlatformApplicationLifecycleState.Background),
                "native-background");
            Shell("cmd uimode night no");
            launch.AddFlags(ActivityFlags.ReorderToFront | ActivityFlags.SingleTop);
            TargetContext!.StartActivity(launch);
            Wait(() => Ui(() => backend.ActivityTracker.CurrentActivity is MainActivity) && Palette(ThemeVariant.Light),
                "light-after-resume");
            activity = Ui(() => (MainActivity)backend.ActivityTracker.CurrentActivity!);
            long generation = Ui(() => backend.GetWindowingDiagnostics().HostGeneration);
            Ui(() => { activity.Recreate(); return true; });
            Wait(() => Ui(() => backend.ActivityTracker.CurrentActivity is MainActivity current && !ReferenceEquals(current, activity) &&
                backend.GetWindowingDiagnostics().HostGeneration > generation &&
                ReferenceEquals(ModernFormsNext.Application.OpenForms.SingleOrDefault(), fixture.Form)), "activity-recreated-retained-form");
            Shell("cmd uimode night yes");
            Wait(() => Palette(ThemeVariant.Dark), "dark-after-recreation");

            Check(Ui(() => manager.Apply(BuiltInThemes.Light).Success), "explicit-light-apply");
            Shell("cmd uimode night no");
            Wait(() => Ui(() => settings.GetPreferredVariant() == PlatformColorScheme.Light), "native-light-after-opt-out");
            Shell("cmd uimode night yes");
            Wait(() => Ui(() => settings.GetPreferredVariant() == PlatformColorScheme.Dark), "native-dark-after-opt-out");
            WaitForIdleSync();
            Check(Ui(() => manager.ActiveTheme?.Id == BuiltInThemes.LightThemeId && manager.ActiveVariant == ThemeVariant.Light),
                "explicit-theme-preserved");
            success = true;
        }
        catch (Exception error) { report.AppendLine($"FAIL {error}"); }
        finally
        {
            try
            {
                if (originalMode is not null)
                {
                    Shell("cmd uimode night " + originalMode);
                    if (!Shell("cmd uimode night").Trim().Equals("Night mode: " + originalMode, StringComparison.Ordinal))
                        throw new InvalidOperationException("Android night mode did not restore.");
                    report.AppendLine("RESTORED " + originalMode);
                }
            }
            catch (Exception error) { success = false; report.AppendLine($"FAIL restoration {error}"); }
            report.AppendLine($"ANDROID_SYSTEM_APPEARANCE_{(success ? "PASS" : "FAIL")} checks={checks}");
            result.PutString("stream", report.ToString());
            Finish(success ? Result.Ok : Result.Canceled, result);
        }
    }

    private string Shell(string command)
    {
        using var descriptor = UiAutomation!.ExecuteShellCommand(command)!;
        using var stream = new ParcelFileDescriptor.AutoCloseInputStream(descriptor);
        using var output = new MemoryStream();
        for (int value = stream.Read(); value != -1; value = stream.Read()) output.WriteByte((byte)value);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private void Capture(MainActivity activity, AppearancePanel panel, ThemeVariant variant, string name)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29)) throw new PlatformNotSupportedException("Native frame commits require Android 10.");
        int before = Ui(() => { int count = panel.Paints; panel.Invalidate(); return count; });
        Wait(() => Ui(() => panel.Paints > before), "framework-paint-" + variant);
        int committed = 0;
        using var callback = new Java.Lang.Runnable(() => Volatile.Write(ref committed, 1));
        var observer = Ui(() =>
        {
            var view = NativeValidationViews.Main(activity);
            var current = view.ViewTreeObserver!;
            if (OperatingSystem.IsAndroidVersionAtLeast(29)) current.RegisterFrameCommitCallback(callback);
            view.RequestRender();
            return current;
        });
        try { Wait(() => Volatile.Read(ref committed) != 0, "native-frame-commit-" + variant); }
        finally
        {
            Ui(() =>
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(29) && observer.IsAlive) observer.UnregisterFrameCommitCallback(callback);
                return true;
            });
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            // Flush the window surface before inspecting the composited display frame.
            using var window = UiAutomation!.TakeScreenshot(activity.Window!);
            Check(window is not null, "native-window-readback-" + variant);
        }
        int expected = BuiltInThemes.Get(variant).Colors[ThemeTokens.Colors.Background.Name].ToArgb();
        int actual;
        var elapsed = Stopwatch.StartNew();
        // The OS can composite an intermediate light/dark frame after the app window commits.
        // Keep the exact final color assertion and bound how long the visible frame can take.
        do
        {
            using var screenshot = UiAutomation!.TakeScreenshot() ?? throw new InvalidOperationException("Native screenshot unavailable.");
            actual = screenshot.GetPixel(screenshot.Width / 2, screenshot.Height / 2);
            if (actual == expected || elapsed.Elapsed >= TimeSpan.FromSeconds(3))
            {
                using var stream = File.Create(System.IO.Path.Combine(TargetContext!.GetExternalFilesDir(null)!.AbsolutePath, name));
                Check(screenshot.Compress(global::Android.Graphics.Bitmap.CompressFormat.Png!, 100, stream), name);
                break;
            }
            Thread.Sleep(25);
        } while (true);
        Check(actual == expected, $"visible-palette-{variant} expected={expected:X8} actual={actual:X8}");
    }

    private sealed class AppearancePanel : Panel
    {
        internal int Paints { get; private set; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            Paints++;
        }
    }

    private T Ui<T>(Func<T> action)
    {
        T value = default!;
        Exception? failure = null;
        RunOnMainSync(() => { try { value = action(); } catch (Exception error) { failure = error; } });
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return value;
    }

    private void Wait(Func<bool> ready, string check)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (ready()) { Check(true, check); return; }
            Thread.Sleep(25);
        }
        Check(false, check);
    }

    private void Check(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
        checks++;
        report.AppendLine("PASS " + check);
    }
}
