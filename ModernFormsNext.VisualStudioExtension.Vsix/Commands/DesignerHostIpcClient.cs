using System;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using ModernFormsNext.VisualStudioExtension.Editors;

namespace ModernFormsNext.VisualStudioExtension.Commands;

internal static class DesignerHostIpcClient
{
    private const string PipePrefix = "ModernFormsNextDesignerHost";
    private static long nextSaveRequestId;

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
        var requestId = Interlocked.Increment(ref nextSaveRequestId)
            .ToString(CultureInfo.InvariantCulture);
        DesignerEditorDiagnosticLog.Write(
            $"IPC_SAVE_REQUEST_BEGIN RequestId={requestId} " +
            $"ManagedThreadId={Environment.CurrentManagedThreadId} TimeoutMs={timeout.TotalMilliseconds:0}");
        DesignerEditorDiagnosticLog.Write(
            $"IPC_SAVE_REQUEST_ID RequestId={requestId} " +
            $"ManagedThreadId={Environment.CurrentManagedThreadId}");
        var response = TrySendEncodedCommandForResponse(
            pipeName,
            "SAVE",
            designDocumentPath,
            projectPath,
            timeout,
            requestId);

        return ParseSaveResponse(requestId, response);
    }

    internal static DesignerHostSaveResult ParseSaveResponse(string requestId, string? response)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("A save request ID is required.", nameof(requestId));

        var parts = response?.Split('\t');
        if (parts is not null && parts.Length >= 2)
        {
            DesignerEditorDiagnosticLog.Write(
                $"IPC_SAVE_RESPONSE_ID RequestId={requestId} " +
                $"ResponseId={parts[1]} Match={string.Equals(parts[1], requestId, StringComparison.Ordinal)}");
        }

        if (parts is { Length: 3 }
            && string.Equals(parts[0], "SAVE_RESULT", StringComparison.Ordinal)
            && string.Equals(parts[1], requestId, StringComparison.Ordinal)
            && string.Equals(parts[2], "SAVED", StringComparison.Ordinal))
        {
            return DesignerHostSaveResult.SavedFor(requestId);
        }

        if (parts is { Length: 4 }
            && string.Equals(parts[0], "SAVE_RESULT", StringComparison.Ordinal)
            && string.Equals(parts[1], requestId, StringComparison.Ordinal)
            && (string.Equals(parts[2], "CANCELED", StringComparison.Ordinal)
                || string.Equals(parts[2], "FAILED", StringComparison.Ordinal)))
        {
            string message;
            try
            {
                message = Decode(parts[3]);
            }
            catch (FormatException)
            {
                message = "The Designer host returned an invalid save diagnostic.";
            }

            return string.Equals(parts[2], "CANCELED", StringComparison.Ordinal)
                ? DesignerHostSaveResult.Canceled(message, requestId)
                : DesignerHostSaveResult.Failed(message, requestId);
        }

        return DesignerHostSaveResult.Failed(
            response is null
                ? "The Designer host did not respond to the save request."
                : "The Designer host returned an invalid or mismatched save response.",
            requestId);
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
        TimeSpan timeout,
        string? requestId = null)
    {
        var saveRequest = string.Equals(command, "SAVE", StringComparison.Ordinal);
        var stopwatch = saveRequest ? Stopwatch.StartNew() : null;
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            pipe.Connect((int)timeout.TotalMilliseconds);

            using var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, leaveOpen: true)
            {
                AutoFlush = true
            };

            if (saveRequest)
            {
                DesignerEditorDiagnosticLog.Write(
                    $"IPC_SAVE_WRITE_BEGIN RequestId={requestId} " +
                    $"ManagedThreadId={Environment.CurrentManagedThreadId}");
            }
            writer.WriteLine($"{command}\t{Encode(payload)}\t{Encode(secondaryPayload)}\t{requestId ?? string.Empty}");
            if (saveRequest)
            {
                DesignerEditorDiagnosticLog.Write(
                    $"IPC_SAVE_WRITE_END RequestId={requestId} ElapsedMs={stopwatch!.Elapsed.TotalMilliseconds:0.###}");
            }
            using var reader = new StreamReader(pipe, Encoding.UTF8, true, 1024, leaveOpen: true);
            if (saveRequest)
            {
                DesignerEditorDiagnosticLog.Write(
                    $"IPC_SAVE_WAIT_BEGIN RequestId={requestId} TimeoutMs={timeout.TotalMilliseconds:0}");
            }
            var response = ReadResponse(reader, timeout);
            if (saveRequest)
            {
                if (response is not null)
                {
                    DesignerEditorDiagnosticLog.Write(
                        $"IPC_SAVE_RESPONSE_RECEIVED RequestId={requestId} Response={response}");
                }
                DesignerEditorDiagnosticLog.Write(
                    $"IPC_SAVE_WAIT_END RequestId={requestId} " +
                    $"Outcome={(response is null ? "Timeout" : "Response")} " +
                    $"ElapsedMs={stopwatch!.Elapsed.TotalMilliseconds:0.###}");
            }
            return response;
        }
        catch (Exception exception)
        {
            if (saveRequest)
            {
                DesignerEditorDiagnosticLog.WriteException(
                    $"IPC_SAVE_WAIT_EXCEPTION RequestId={requestId}",
                    exception);
                DesignerEditorDiagnosticLog.Write(
                    $"IPC_SAVE_WAIT_END RequestId={requestId} Outcome=Exception " +
                    $"ElapsedMs={stopwatch!.Elapsed.TotalMilliseconds:0.###}");
            }
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
    private DesignerHostSaveResult(
        DesignerHostSaveOutcome outcome,
        string? error,
        string? requestId)
    {
        Outcome = outcome;
        Error = error;
        RequestId = requestId;
    }

    public static DesignerHostSaveResult Saved { get; }
        = new(DesignerHostSaveOutcome.Saved, error: null, requestId: null);

    public DesignerHostSaveOutcome Outcome { get; }

    public string? Error { get; }

    public string? RequestId { get; }

    public static DesignerHostSaveResult SavedFor(string requestId)
        => new(DesignerHostSaveOutcome.Saved, error: null, requestId);

    public static DesignerHostSaveResult Canceled(string error, string? requestId = null)
        => new(DesignerHostSaveOutcome.Canceled, error, requestId);

    public static DesignerHostSaveResult Failed(string error, string? requestId = null)
        => new(DesignerHostSaveOutcome.Failed, error, requestId);
}

internal enum DesignerHostSaveOutcome
{
    Saved,
    Canceled,
    Failed
}
