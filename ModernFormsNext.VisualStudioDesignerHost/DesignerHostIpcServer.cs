using System.IO.Pipes;
using System.Text;

namespace ModernFormsNext.VisualStudioDesignerHost;

internal sealed class DesignerHostIpcServer : IDisposable
{
    private readonly string pipeName;
    private readonly Action<DesignerHostIpcCommand> handleCommand;
    private readonly CancellationTokenSource cancellation = new();
    private Task? listenTask;

    public DesignerHostIpcServer(string pipeName, Action<DesignerHostIpcCommand> handleCommand)
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
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellation.Token);

                using var reader = new StreamReader(pipe, Encoding.UTF8);
                var line = await reader.ReadLineAsync(cancellation.Token);

                if (DesignerHostIpcCommand.TryParse(line, out var command))
                    handleCommand(command);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                WriteLog($"IPC server error: {ex}");
            }
        }
    }

    private static void WriteLog(string message)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "ModernFormsNextDesignerHost.log");
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must not interrupt IPC.
        }
    }
}

internal sealed class DesignerHostIpcCommand
{
    private DesignerHostIpcCommand(string designDocumentPath, string? projectPath)
    {
        DesignDocumentPath = designDocumentPath;
        ProjectPath = projectPath;
    }

    public string DesignDocumentPath { get; }

    public string? ProjectPath { get; }

    public static bool TryParse(string? line, out DesignerHostIpcCommand command)
    {
        command = null!;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        var parts = line.Split('\t');

        if (parts.Length < 2 || !string.Equals(parts[0], "OPEN", StringComparison.Ordinal))
            return false;

        var designDocumentPath = Decode(parts[1]);
        var projectPath = parts.Length > 2 ? Decode(parts[2]) : null;

        if (string.IsNullOrWhiteSpace(designDocumentPath))
            return false;

        command = new DesignerHostIpcCommand(designDocumentPath, string.IsNullOrWhiteSpace(projectPath) ? null : projectPath);
        return true;
    }

    private static string Decode(string value)
        => Encoding.UTF8.GetString(Convert.FromBase64String(value));
}
