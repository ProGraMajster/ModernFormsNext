using System.Diagnostics;

namespace ModernFormsNext.VisualStudioDesignerHost;

// Observe tracing only in the Designer process. Backend assemblies know neither the
// Designer log path nor any project or Visual Studio state.
internal sealed class DesignerStartupTraceListener : TraceListener
{
    internal const string Category = "ModernFormsNext.Startup";

    public override void Write(string? message) { }
    public override void WriteLine(string? message) { }

    public override void WriteLine(string? message, string? category)
    {
        if (category == Category)
            DesignerHostDiagnosticLog.Write(message ?? string.Empty);
    }
}
