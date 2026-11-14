using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Supplies Android lifecycle and the native Skia surface to the shared application.
/// </summary>
/// <remarks>
/// Resize the native content area for the software keyboard. Android cannot discover the
/// shared scroll controls inside a single Skia view; its automatic mode can otherwise choose
/// panning without knowing the framework caret. The shared page scrolls its current caret
/// after viewport changes and accounts only for any remaining edge-to-edge IME overlap.
/// </remarks>
[Activity(
    Name = "com.programajster.modernformsnext.sample.MainActivity",
    Label = "ModernFormsNext Cross-Platform Sample",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Unspecified,
    WindowSoftInputMode = global::Android.Views.SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation |
        ConfigChanges.ScreenSize |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.UiMode |
        ConfigChanges.Density)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "modernformsnext-sample")]
public sealed class MainActivity : ModernFormsNext.WindowKit.Backend.Android.Windowing.AndroidWindowActivity
{
    internal const string LifecycleProbeExtra = "MFN_LIFECYCLE_PROBE";
    private const int LifecycleProbeSeed = 37;
    // Native frame qualification must not sample the system's Activity-entry crossfade.
    // This flag observes the platform callback; it never changes system animation settings.
    internal bool WindowEntryCompleted { get; private set; }

    /// <inheritdoc/>
    public override void OnEnterAnimationComplete()
    {
        base.OnEnterAnimationComplete();
        WindowEntryCompleted = true;
    }

    /// <inheritdoc/>
    protected override void OnStartApplication()
    {
        var app = ((SampleApplication)Application!).SharedApp;
        ModernFormsNext.Application.Run(new MainForm(app));
    }

    /// <inheritdoc/>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        if (Intent?.GetBooleanExtra(LifecycleProbeExtra, false) == true)
            CreateWithLifecycleProbe(savedInstanceState);
        else
            base.OnCreate(savedInstanceState);
        var root = ((SampleApplication)Application!).SharedApp.Root;
        if (Intent?.GetBooleanExtra("ACCESSIBILITY_PHASE4", false) == true)
            ShowAccessibilityPhase4();
        else if (Intent?.GetBooleanExtra("ACCESSIBILITY_DEMO", false) == true)
        {
            foreach (var child in root.Controls) child.Visible = child is AccessibilityDemoPanel;
            if (!root.Controls.OfType<AccessibilityDemoPanel>().Any()) root.Controls.Add(new AccessibilityDemoPanel());
        }
    }

    private void CreateWithLifecycleProbe(Bundle? savedInstanceState)
    {
        var provider = AndroidWindowKit.Current.Lifecycle;
        long restoreSequence = 0;
        long activationSequence = 0;
        PlatformActivationKind? activationKind = null;
        // Register before the base Activity attaches the host. The application has already
        // registered its own restore handler, so the post-create sample reflects its result.
        void Restore(object? sender, PlatformApplicationStateRestoringEventArgs e) => restoreSequence = e.Sequence;
        void Activate(object? sender, PlatformApplicationActivationEventArgs e)
        {
            activationSequence = e.Sequence;
            activationKind = e.Activation.Kind;
        }
        provider.StateRestoring += Restore;
        provider.ActivationReceived += Activate;
        try { base.OnCreate(savedInstanceState); }
        finally
        {
            provider.StateRestoring -= Restore;
            provider.ActivationReceived -= Activate;
        }
        var app = ((SampleApplication)Application!).SharedApp;
        // The fixture seeds only an initial, unsaved task. It cannot mask a missing restoration
        // when a previously saved task is recreated in a new process.
        bool seed = savedInstanceState is null && restoreSequence == 0 &&
            Intent?.GetBooleanExtra("MFN_LIFECYCLE_SEED", false) == true;
        if (seed)
        {
            app.State.ClickCount = LifecycleProbeSeed;
            app.RefreshPlatformStatus();
        }
        var entries = ModernFormsNext.Application.Lifecycle.GetDiagnostics().RecentStateDeliveries;
        var restored = entries?.FirstOrDefault(entry => entry.Sequence == restoreSequence);
        global::Android.Util.Log.Info("MfnLifecycleProbe",
            $"create task={TaskId} bundle={savedInstanceState is not null} seeded={seed} " +
            $"expectedFixtureState={app.State.ClickCount == LifecycleProbeSeed} restore={restoreSequence} " +
            $"schema={restored?.SchemaVersion} activation={activationSequence} kind={activationKind}");
    }

    /// <inheritdoc/>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent?.GetBooleanExtra("ACCESSIBILITY_PHASE4", false) == true) ShowAccessibilityPhase4();
    }

    private void ShowAccessibilityPhase4()
    {
        var root = ((SampleApplication)Application!).SharedApp.Root;
        foreach (var child in root.Controls) child.Visible = child is AccessibilityPhase4Panel;
        if (!root.Controls.OfType<AccessibilityPhase4Panel>().Any()) root.Controls.Add(new AccessibilityPhase4Panel());
    }
}
