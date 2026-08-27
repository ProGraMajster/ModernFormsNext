using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

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
        => TrySendCommand(pipeName, "OPEN", designDocumentPath, projectPath, timeout);

    public static bool TrySendCommand(
        string pipeName,
        string command,
        string designDocumentPath,
        string? projectPath,
        TimeSpan timeout)
    {
        if (!string.Equals(command, "OPEN", StringComparison.Ordinal)
            && !string.Equals(command, "SAVE", StringComparison.Ordinal)
            && !string.Equals(command, "SHUTDOWN", StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported Designer host IPC command.");
        }

        return TrySendEncodedCommand(pipeName, command, designDocumentPath, projectPath, timeout);
    }

    public static bool TrySendLifecycleCommand(
        string pipeName,
        string command,
        string? payload,
        TimeSpan timeout)
    {
        if (!string.Equals(command, "ATTACH", StringComparison.Ordinal)
            && !string.Equals(command, "PARK", StringComparison.Ordinal)
            && !string.Equals(command, "RESIZE", StringComparison.Ordinal)
            && !string.Equals(command, "SHOW", StringComparison.Ordinal)
            && !string.Equals(command, "HIDE", StringComparison.Ordinal)
            && !string.Equals(command, "FOCUS", StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported Designer host lifecycle command.");
        }

        return TrySendEncodedCommand(pipeName, command, payload, null, timeout);
    }

    private static bool TrySendEncodedCommand(
        string pipeName,
        string command,
        string? payload,
        string? secondaryPayload,
        TimeSpan timeout)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            pipe.Connect((int)timeout.TotalMilliseconds);

            using var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, leaveOpen: true)
            {
                AutoFlush = true
            };

            writer.WriteLine($"{command}\t{Encode(payload)}\t{Encode(secondaryPayload)}");
            using var reader = new StreamReader(pipe, Encoding.UTF8, true, 1024, leaveOpen: true);
            return string.Equals(ReadResponse(reader, timeout), "OK", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetDocumentDirty(
        string pipeName,
        string designDocumentPath,
        string? projectPath,
        TimeSpan timeout,
        out bool isDirty)
    {
        isDirty = false;

        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            pipe.Connect((int)timeout.TotalMilliseconds);

            using var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
            writer.WriteLine($"DIRTY\t{Encode(designDocumentPath)}\t{Encode(projectPath)}");

            using var reader = new StreamReader(pipe, Encoding.UTF8, true, 1024, leaveOpen: true);
            var response = ReadResponse(reader, timeout);
            if (string.Equals(response, "DIRTY\t1", StringComparison.Ordinal))
            {
                isDirty = true;
                return true;
            }

            return string.Equals(response, "DIRTY\t0", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadResponse(StreamReader reader, TimeSpan timeout)
        => ThreadHelper.JoinableTaskFactory.Run(() => ReadResponseAsync(reader, timeout));

    private static async Task<string?> ReadResponseAsync(StreamReader reader, TimeSpan timeout)
    {
        var responseTask = reader.ReadLineAsync();
        var completed = await Task.WhenAny(responseTask, Task.Delay(timeout));
        if (completed == responseTask)
            return await responseTask;

        _ = responseTask.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return null;
    }

    private static string Encode(string? value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
}
