namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Represents the shared cross-platform ModernFormsNext application.
/// </summary>
/// <remarks>
/// Both platform hosts create or retrieve this class and attach the same <see cref="Root"/>
/// control tree. The class contains no Android or Windows types.
/// </remarks>
public sealed class App
{
    /// <summary>Creates the shared application using injected platform services.</summary>
    /// <param name="platformServices">Platform facts, dispatcher, and permission integration.</param>
    public App(ISamplePlatformServices platformServices)
    {
        PlatformServices = platformServices ?? throw new ArgumentNullException(nameof(platformServices));
        State = new SampleAppState();
        Root = new MainPage(this);
    }

    /// <summary>Gets the persistent shared application state.</summary>
    public SampleAppState State { get; }

    /// <summary>Gets the injected platform integration.</summary>
    public ISamplePlatformServices PlatformServices { get; }

    /// <summary>Gets the single framework control root used by every host.</summary>
    public MainPage Root { get; }

    /// <summary>Refreshes dynamic host and surface information displayed by the root.</summary>
    public void RefreshPlatformStatus() => Root.RefreshStatus();
}
