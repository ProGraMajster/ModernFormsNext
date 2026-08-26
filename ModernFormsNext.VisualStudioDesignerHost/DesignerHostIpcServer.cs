using System.IO.Pipes;
using System.Text;

namespace ModernFormsNext.VisualStudioDesignerHost;

internal sealed class DesignerHostIpcServer : IDisposable
{
    private readonly string pipeName;
    private readonly Func<DesignerHostIpcCommand, Task<string>> handleCommand;
    private readonly CancellationTokenSource cancellation = new();
    private Task? listenTask;
    private int disposed;

    public DesignerHostIpcServer(string pipeName, Func<DesignerHostIpcCommand, Task<string>> handleCommand)
    {
        this.pipeName = pipeName;
        this.handleCommand = handleCommand;
    }

    public void Start()
    {
        listenTask = Task.Run(ListenAsync);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        cancellation.Cancel();

        try
        {
            listenTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Shutdown is best-effort because the host process is already closing.
        }

        cancellation.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellation.Token);

                using var reader = new StreamReader(pipe, Encoding.UTF8, true, 1024, leaveOpen: true);
                var line = await reader.ReadLineAsync(cancellation.Token);

                using var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
                if (DesignerHostIpcCommand.TryParse(line, out var command))
                    await writer.WriteLineAsync(await handleCommand(command));
                else
                    await writer.WriteLineAsync("ERROR");
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                DesignerHostDiagnosticLog.Write($"IPC server error: {ex}");
            }
        }
    }
}

internal sealed class DesignerHostIpcCommand
{
    private DesignerHostIpcCommand(
        DesignerHostIpcCommandKind kind,
        string designDocumentPath,
        string? projectPath)
    {
        Kind = kind;
        DesignDocumentPath = designDocumentPath;
        ProjectPath = projectPath;
    }

    public DesignerHostIpcCommandKind Kind { get; }

    public string DesignDocumentPath { get; }

    public string? ProjectPath { get; }

    public static bool TryParse(string? line, out DesignerHostIpcCommand command)
    {
        command = null!;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        var parts = line.Split('\t');

        if (parts.Length < 2 || !TryParseKind(parts[0], out var kind))
            return false;

        string designDocumentPath;
        string? projectPath;
        try
        {
            designDocumentPath = Decode(parts[1]);
            projectPath = parts.Length > 2 ? Decode(parts[2]) : null;
        }
        catch (FormatException)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(designDocumentPath))
            return false;

        command = new DesignerHostIpcCommand(
            kind,
            designDocumentPath,
            string.IsNullOrWhiteSpace(projectPath) ? null : projectPath);
        return true;
    }

    private static bool TryParseKind(string value, out DesignerHostIpcCommandKind kind)
    {
        if (string.Equals(value, "OPEN", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.Open;
            return true;
        }

        if (string.Equals(value, "SAVE", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.Save;
            return true;
        }

        if (string.Equals(value, "DIRTY", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.QueryDirty;
            return true;
        }

        if (string.Equals(value, "SHUTDOWN", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.Shutdown;
            return true;
        }

        kind = default;
        return false;
    }

    private static string Decode(string value)
        => Encoding.UTF8.GetString(Convert.FromBase64String(value));
}

internal enum DesignerHostIpcCommandKind
{
    Open,
    Save,
    QueryDirty,
    Shutdown
}
