using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace ModernFormsNext.VisualStudioExtension.Commands;

internal static class DesignerHostIpcClient
{
    private const string PipePrefix = "ModernFormsNextDesignerHost";

    public static string GetPipeName(string hostKey)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(hostKey));

        var suffix = new StringBuilder(16);
        for (var index = 0; index < 8; index++)
            suffix.Append(hash[index].ToString("X2", CultureInfo.InvariantCulture));

        return $"{PipePrefix}-{suffix}";
    }

    public static bool TryOpenDocument(
        string pipeName,
        string designDocumentPath,
        string? projectPath,
        TimeSpan timeout)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            pipe.Connect((int)timeout.TotalMilliseconds);

            using var writer = new StreamWriter(pipe, Encoding.UTF8)
            {
                AutoFlush = true
            };

            writer.WriteLine($"OPEN\t{Encode(designDocumentPath)}\t{Encode(projectPath)}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Encode(string? value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
}
