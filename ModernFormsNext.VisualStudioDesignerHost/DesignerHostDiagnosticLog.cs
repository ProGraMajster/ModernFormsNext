namespace ModernFormsNext.VisualStudioDesignerHost;

internal static class DesignerHostDiagnosticLog
{
    private static readonly object Gate = new();

    public static string Path { get; } = GetPath(System.IO.Path.GetTempPath(), Environment.ProcessId);

    public static string GetPath(string directory, int processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId));

        return IOPath.Combine(directory, $"ModernFormsNextDesignerHost-{processId}.log");
    }

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(
                    Path,
                    $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never make host startup or IPC less reliable.
        }
    }
}
