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

    public static DesignerHostSaveResult SaveDocument(
        string pipeName,
        string designDocumentPath,
        string? projectPath,
        TimeSpan timeout)
    {
        var response = TrySendEncodedCommandForResponse(
            pipeName,
            "SAVE",
            designDocumentPath,
            projectPath,
            timeout);

        if (string.Equals(response, "SAVE_RESULT\tSAVED", StringComparison.Ordinal))
            return DesignerHostSaveResult.Saved;

        var parts = response?.Split('\t');
        if (parts is { Length: 3 }
            && string.Equals(parts[0], "SAVE_RESULT", StringComparison.Ordinal)
            && (string.Equals(parts[1], "CANCELED", StringComparison.Ordinal)
                || string.Equals(parts[1], "FAILED", StringComparison.Ordinal)))
        {
            string message;
            try
            {
                message = Decode(parts[2]);
            }
            catch (FormatException)
            {
                message = "The Designer host returned an invalid save diagnostic.";
            }

            return string.Equals(parts[1], "CANCELED", StringComparison.Ordinal)
                ? DesignerHostSaveResult.Canceled(message)
                : DesignerHostSaveResult.Failed(message);
        }

        return DesignerHostSaveResult.Failed(
            response is null
                ? "The Designer host did not respond to the save request."
                : "The Designer host returned an invalid save response.");
    }

    public static bool TrySendCommand(
        string pipeName,
        string command,
        string designDocumentPath,
        string? projectPath,
        TimeSpan timeout)
    {
        if (!string.Equals(command, "OPEN", StringComparison.Ordinal)
            && !string.Equals(command, "DISCARD", StringComparison.Ordinal)
            && !string.Equals(command, "SHUTDOWN", StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported Designer host IPC command.");
        }

        return string.Equals(
            TrySendEncodedCommandForResponse(pipeName, command, designDocumentPath, projectPath, timeout),
            "OK",
            StringComparison.Ordinal);
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

        return string.Equals(
            TrySendEncodedCommandForResponse(pipeName, command, payload, null, timeout),
            "OK",
            StringComparison.Ordinal);
    }

    private static string? TrySendEncodedCommandForResponse(
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
            return ReadResponse(reader, timeout);
        }
        catch
        {
            return null;
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

    private static string Decode(string value)
        => Encoding.UTF8.GetString(Convert.FromBase64String(value));
}

internal readonly struct DesignerHostSaveResult
{
    private DesignerHostSaveResult(DesignerHostSaveOutcome outcome, string? error)
    {
        Outcome = outcome;
        Error = error;
    }

    public static DesignerHostSaveResult Saved { get; }
        = new(DesignerHostSaveOutcome.Saved, error: null);

    public DesignerHostSaveOutcome Outcome { get; }

    public string? Error { get; }

    public static DesignerHostSaveResult Canceled(string error)
        => new(DesignerHostSaveOutcome.Canceled, error);

    public static DesignerHostSaveResult Failed(string error)
        => new(DesignerHostSaveOutcome.Failed, error);
}

internal enum DesignerHostSaveOutcome
{
    Saved,
    Canceled,
    Failed
}
