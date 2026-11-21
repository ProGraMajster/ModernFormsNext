using System.Diagnostics.Tracing;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.VisualStudioDesignerHost;

// Observe tracing only in the Designer process. Backend assemblies know neither the
// Designer log path nor any project or Visual Studio state.
internal sealed class DesignerStartupTraceListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == StartupDiagnostics.ProviderName)
            EnableEvents(eventSource, EventLevel.Informational);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventId == 1 && eventData.Payload is [string stage])
            DesignerHostDiagnosticLog.Write(stage);
    }
}
