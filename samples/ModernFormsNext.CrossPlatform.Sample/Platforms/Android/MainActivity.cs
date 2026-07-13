using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using ModernFormsNext.WindowKit.Backend.Android;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Supplies Android lifecycle and the native Skia surface to the shared application.
/// </summary>
[Activity(
    Name = "com.programajster.modernformsnext.sample.MainActivity",
    Label = "ModernFormsNext Cross-Platform Sample",
    MainLauncher = true,
    Exported = true,
    ScreenOrientation = ScreenOrientation.Unspecified,
    ConfigurationChanges = ConfigChanges.Orientation |
        ConfigChanges.ScreenSize |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.UiMode |
        ConfigChanges.Density)]
public sealed class MainActivity : Activity
{
    private AndroidAppHost? host;

    /// <inheritdoc/>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AndroidWindowKit.ObserveHostActivity(this);
        var application = (SampleApplication)Application!;
        host = new AndroidAppHost(this, application.SharedApp);
        SetContentView(host.View);
        application.SharedApp.NotifyLifecycle("Activity created");
    }

    /// <inheritdoc/>
    protected override void OnStart()
    {
        base.OnStart();
        host?.Start();
        ((SampleApplication)Application!).SharedApp.NotifyLifecycle("Activity started");
    }

    /// <inheritdoc/>
    protected override void OnResume()
    {
        base.OnResume();
        AndroidWindowKit.ObserveHostActivity(this);
        host?.Resume();
        ((SampleApplication)Application!).SharedApp.NotifyLifecycle("Activity resumed");
    }

    /// <inheritdoc/>
    protected override void OnPause()
    {
        host?.Pause();
        ((SampleApplication)Application!).SharedApp.NotifyLifecycle("Activity paused");
        base.OnPause();
    }

    /// <inheritdoc/>
    protected override void OnStop()
    {
        host?.Stop();
        ((SampleApplication)Application!).SharedApp.NotifyLifecycle("Activity stopped");
        base.OnStop();
    }

    /// <inheritdoc/>
    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        host?.ConfigurationChanged();
        ((SampleApplication)Application!).SharedApp.NotifyLifecycle("Configuration changed");
    }

    /// <inheritdoc/>
    protected override void OnDestroy()
    {
        ((SampleApplication)Application!).SharedApp.NotifyLifecycle(
            IsChangingConfigurations ? "Activity destroyed for recreation" : "Activity destroyed");
        host?.Dispose();
        host = null;
        base.OnDestroy();
    }

    /// <inheritdoc/>
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (!AndroidWindowKit.HandleRequestPermissionsResult(requestCode, permissions, grantResults))
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }
}
