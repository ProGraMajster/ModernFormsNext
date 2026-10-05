using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using ModernFormsNext.WindowKit.Backend.Android;

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
    /// <inheritdoc/>
    protected override void OnStartApplication()
    {
        var app = ((SampleApplication)Application!).SharedApp;
        ModernFormsNext.Application.Run(new MainForm(app));
    }

    /// <inheritdoc/>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
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