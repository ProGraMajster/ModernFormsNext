using ModernFormsNext;

namespace ModernFormsNext.VisualStudioDesignerHost;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            DesignerHostDiagnosticLog.Write("Starting ModernFormsNext Visual Studio designer host.");
            Application.Run(new VisualStudioDesignerHostForm(DesignerHostArguments.Parse(args)));
            DesignerHostDiagnosticLog.Write("Designer host exited normally.");
        }
        catch (Exception ex)
        {
            DesignerHostDiagnosticLog.Write(ex.ToString());
            Environment.ExitCode = -1;
        }
    }
}
