using Android.App;
using Android.Runtime;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using System.Globalization;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Owns the shared application tree across ordinary Android activity recreation.
/// </summary>
[Application(Name = "com.programajster.modernformsnext.sample.SampleApplication")]
public sealed class SampleApplication : global::Android.App.Application
{
    private AndroidPlatformServices? platformServices;

    /// <summary>Creates the application object from a Java handle.</summary>
    public SampleApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    /// <summary>Gets the process-owned shared application after Android startup.</summary>
    public App SharedApp { get; private set; } = null!;

    /// <inheritdoc/>
    public override void OnCreate()
    {
        base.OnCreate();
        var backend = AndroidWindowKit.Initialize(new AndroidWindowKitOptions(this)
        {
            EnableDetailedDiagnostics = false
        });
        platformServices = new AndroidPlatformServices(backend);
        SharedApp = new App(platformServices);
        backend.Lifecycle.LifecycleChanged += (_, e) =>
            SharedApp.NotifyLifecycle($"{e.Current.Phase}: {e.Current.State} (hosts: {e.Current.HostCount})");
        backend.Lifecycle.ActivationReceived += (_, e) =>
            SharedApp.NotifyLifecycle($"Activation: {e.Activation.Kind}");
        // The sample persists one application-owned value, not controls/native objects. Android
        // supplies the Bundle handoff during recreation; process death still requires a saved Bundle.
        backend.Lifecycle.StateSaving += (_, e) => e.Data = new PlatformApplicationStateData(1,
            new Dictionary<string, string>
            {
                ["clickCount"] = SharedApp.State.ClickCount.ToString(CultureInfo.InvariantCulture)
            });
        backend.Lifecycle.StateRestoring += (_, e) =>
        {
            if (e.Data.Version == 1 && e.Data.Values.TryGetValue("clickCount", out string? value) &&
                int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int clicks) && clicks >= 0)
            {
                SharedApp.State.ClickCount = clicks;
                SharedApp.RefreshPlatformStatus();
            }
        };
    }
}
