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

    public static void WriteException(string marker, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        ArgumentNullException.ThrowIfNull(exception);

        Write(FormatException(marker, exception));
    }

    internal static string FormatException(string marker, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new System.Text.StringBuilder();
        builder.AppendLine(marker);
        AppendException(builder, exception, depth: 0);
        builder.AppendLine("Environment.StackTrace:");
        builder.Append(Environment.StackTrace);
        return builder.ToString();
    }

    private static void AppendException(System.Text.StringBuilder builder, Exception exception, int depth)
    {
        builder.Append("Exception[").Append(depth).AppendLine("]:");
        builder.Append("Type: ").AppendLine(exception.GetType().FullName ?? exception.GetType().Name);
        builder.Append("Message: ").AppendLine(exception.Message);
        builder.Append("HResult: 0x")
            .Append(unchecked((uint)exception.HResult).ToString("X8", System.Globalization.CultureInfo.InvariantCulture))
            .Append(" (")
            .Append(exception.HResult.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .AppendLine(")");
        if (exception is System.ComponentModel.Win32Exception win32Exception)
        {
            builder.Append("NativeErrorCode: ")
                .AppendLine(win32Exception.NativeErrorCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        builder.Append("Source: ").AppendLine(exception.Source ?? "<none>");
        builder.Append("TargetSite: ").AppendLine(exception.TargetSite?.ToString() ?? "<none>");
        builder.AppendLine("StackTrace:");
        builder.AppendLine(exception.StackTrace ?? "<unavailable>");

        if (exception.InnerException is not null)
            AppendException(builder, exception.InnerException, depth + 1);
    }
}
