using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace ModernFormsNext.VisualStudioExtension.Editors;

internal static class DesignerEditorDiagnosticLog
{
    private static readonly object Gate = new();
    private static readonly string LogPath = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"ModernFormsNextDesignerEditor-{Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)}.log");

    public static string Path => LogPath;

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(
                    LogPath,
                    $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // Diagnostics must never change Visual Studio's save or close result.
        }
    }

    public static void WriteException(string marker, Exception exception)
    {
        var builder = new StringBuilder();
        builder.Append(marker);
        var current = exception;
        var depth = 0;
        while (current is not null)
        {
            builder.Append(depth == 0 ? " " : " InnerException: ");
            builder.Append(current.GetType().FullName);
            builder.Append(" HResult=0x");
            builder.Append(current.HResult.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" Message=");
            builder.Append(current.Message);
            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                builder.Append(" StackTrace=");
                builder.Append(current.StackTrace);
            }

            current = current.InnerException;
            depth++;
        }

        Write(builder.ToString());
    }
}
