using System.Diagnostics.Tracing;

namespace ModernFormsNext.WindowKit.Backend;

// Keep startup messages out of applications' general Trace listeners.
[EventSource(Name = ProviderName)]
internal sealed class StartupDiagnostics : EventSource
{
    internal const string ProviderName = "ModernFormsNext-Startup";
    internal static readonly StartupDiagnostics Log = new();

    private StartupDiagnostics() { }

    /// <summary>Emits an initialization stage when a listener enables this provider.</summary>
    [Event(1, Level = EventLevel.Informational)]
    public void Stage(string message)
    {
        if (IsEnabled())
            WriteEvent(1, message);
    }
}
