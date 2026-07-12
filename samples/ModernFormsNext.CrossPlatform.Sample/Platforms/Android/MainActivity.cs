using Android.App;
using Android.Content.PM;
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
    ScreenOrientation = ScreenOrientation.Unspecified)]
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
    }

    /// <inheritdoc/>
    protected override void OnResume()
    {
        base.OnResume();
        AndroidWindowKit.ObserveHostActivity(this);
        host?.Resume();
    }

    /// <inheritdoc/>
    protected override void OnPause()
    {
        host?.Pause();
        base.OnPause();
    }

    /// <inheritdoc/>
    protected override void OnStop()
    {
        host?.Stop();
        base.OnStop();
    }

    /// <inheritdoc/>
    protected override void OnDestroy()
    {
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
