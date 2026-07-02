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
        Path = System.IO.Path.Combine(directory, "designer-debug.log");
    }

    public static string Path { get; }

    public static void Write(string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}";

        lock (Gate)
        {
            File.AppendAllText(Path, line);
        }
    }
}
