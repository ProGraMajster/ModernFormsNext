using ModernFormsNext;

namespace ModernFormsNext.VisualStudioDesignerHost;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            WriteLog("Starting ModernFormsNext Visual Studio designer host.");
            Application.Run(new VisualStudioDesignerHostForm(DesignerHostArguments.Parse(args)));
            WriteLog("Designer host exited normally.");
        }
        catch (Exception ex)
        {
            WriteLog(ex.ToString());
            Environment.ExitCode = -1;
        }
    }

    private static void WriteLog(string message)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "ModernFormsNextDesignerHost.log");
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never make host startup less reliable.
        }
    }
}
