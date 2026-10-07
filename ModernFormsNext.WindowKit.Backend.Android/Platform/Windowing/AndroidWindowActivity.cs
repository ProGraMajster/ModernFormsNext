using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Content.PM;

namespace ModernFormsNext.WindowKit.Backend.Android.Windowing;

/// <summary>Minimal Activity entry point for the framework Application/Form windowing path.</summary>
/// <remarks>
/// Initialize AndroidWindowKit from Application.OnCreate. Override OnStartApplication to call
/// ModernFormsNext.Application.Run(new MainForm()). Native recreation reattaches the same Form.
/// Custom Activities can instead own AndroidActivityHost and forward the documented callbacks.
/// This type neither starts a Looper nor automatically serializes the control tree.
/// </remarks>
public abstract class AndroidWindowActivity : Activity
{
    private AndroidActivityHost? host;
    /// <summary>Starts the shared application once, on the main thread after host attachment.</summary>
    protected abstract void OnStartApplication();
    /// <inheritdoc/>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        host = new AndroidActivityHost(this, savedInstanceState);
        if (host.BeginStartup())
        {
            try { OnStartApplication(); }
            catch { AndroidWindowKit.Current.Windowing.Shutdown(); throw; }
        }
    }
    /// <inheritdoc/>
    protected override void OnStart() { base.OnStart(); host?.Start(); }
    /// <inheritdoc/>
    protected override void OnResume() { base.OnResume(); host?.Resume(); }
    /// <inheritdoc/>
    protected override void OnPause() { try { host?.Pause(); } finally { base.OnPause(); } }
    /// <inheritdoc/>
    protected override void OnStop() { try { host?.Stop(); } finally { base.OnStop(); } }
    /// <inheritdoc/>
    public override void OnWindowFocusChanged(bool hasFocus) { base.OnWindowFocusChanged(hasFocus); host?.WindowFocusChanged(hasFocus); }
    /// <inheritdoc/>
    public override void OnConfigurationChanged(Configuration newConfig) { base.OnConfigurationChanged(newConfig); host?.ConfigurationChanged(); }
    /// <inheritdoc/>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent); Intent = intent; AndroidWindowKit.HandleNewIntent(this, intent);
    }
    /// <inheritdoc/>
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (!AndroidWindowKit.HandleRequestPermissionsResult(this, requestCode, permissions, grantResults))
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }
    /// <inheritdoc/>
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (!AndroidWindowKit.HandleActivityResult(this, requestCode, resultCode, data))
            base.OnActivityResult(requestCode, resultCode, data);
    }
    /// <inheritdoc/>
    public override void OnBackPressed() { if (host?.HandleBack() != true) Finish(); }
    /// <inheritdoc/>
    protected override void OnDestroy()
    {
        var previous = host; host = null;
        try { previous?.Dispose(); } finally { base.OnDestroy(); }
    }
}
