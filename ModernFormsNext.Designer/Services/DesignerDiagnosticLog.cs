namespace ModernFormsNext.Designer.Services;

internal static class DesignerDiagnosticLog
{
    private static readonly object Gate = new();

    static DesignerDiagnosticLog()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(basePath))
            basePath = System.IO.Path.GetTempPath();

        var directory = System.IO.Path.Combine(basePath, "ModernFormsNext", "Designer");
        Directory.CreateDirectory(directory);
        Path = GetPath(directory, Environment.ProcessId);
    }

    public static string Path { get; }

    internal static string GetPath(string directory, int processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId));

        // Visual Studio may host several Designer processes concurrently. A per-process file
        // prevents those sessions (and parallel test workers) from racing one another's writes.
        return System.IO.Path.Combine(directory, $"designer-debug-{processId}.log");
    }

    public static void Write(string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}";

        lock (Gate)
        {
            File.AppendAllText(Path, line);
        }
    }
}
