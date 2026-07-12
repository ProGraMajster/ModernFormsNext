using Android.App;
using Android.Runtime;
using ModernFormsNext.WindowKit.Backend.Android;

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
    }
}
