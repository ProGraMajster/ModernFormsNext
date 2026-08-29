using System.IO.Pipes;
using System.Globalization;
using System.Text;

namespace ModernFormsNext.VisualStudioDesignerHost;

internal sealed class DesignerHostIpcServer : IDisposable
{
    private readonly string pipeName;
    private readonly Func<DesignerHostIpcCommand, Task<string>> handleCommand;
    private readonly CancellationTokenSource cancellation = new();
    private Task? listenTask;
    private int disposed;
    private int readyLogged;

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

                if (Interlocked.Exchange(ref readyLogged, 1) == 0)
                    DesignerHostDiagnosticLog.Write($"IPC_READY PipeName={pipeName}");

                await pipe.WaitForConnectionAsync(cancellation.Token);

                using var reader = new StreamReader(pipe, Encoding.UTF8, true, 1024, leaveOpen: true);
                var line = await reader.ReadLineAsync(cancellation.Token);

                using var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
                if (DesignerHostIpcCommand.TryParse(line, out var command))
                {
                    DesignerHostDiagnosticLog.Write(GetReceivedMarker(command.Kind));
                    await writer.WriteLineAsync(await handleCommand(command));
                }
                else
                    await writer.WriteLineAsync("ERROR");
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                DesignerHostDiagnosticLog.WriteException("IPC_SERVER_EXCEPTION", ex);
            }
        }
    }

    private static string GetReceivedMarker(DesignerHostIpcCommandKind kind)
        => kind switch
        {
            DesignerHostIpcCommandKind.Open => "OPEN_RECEIVED",
            DesignerHostIpcCommandKind.Save => "SAVE_RECEIVED",
            DesignerHostIpcCommandKind.QueryDirty => "DIRTY_RECEIVED",
            DesignerHostIpcCommandKind.DiscardRecovery => "DISCARD_RECEIVED",
            DesignerHostIpcCommandKind.Shutdown => "SHUTDOWN_RECEIVED",
            DesignerHostIpcCommandKind.AttachParent => "PARENT_ATTACH_RECEIVED",
            DesignerHostIpcCommandKind.Park => "PARENT_PARK_RECEIVED",
            DesignerHostIpcCommandKind.Resize => "RESIZE_RECEIVED",
            DesignerHostIpcCommandKind.Show => "VISIBILITY_RECEIVED Visible=true",
            DesignerHostIpcCommandKind.Hide => "VISIBILITY_RECEIVED Visible=false",
            DesignerHostIpcCommandKind.Focus => "FOCUS_RECEIVED",
            _ => $"UNKNOWN_{kind}_RECEIVED"
        };
}

internal sealed class DesignerHostIpcCommand
{
    private DesignerHostIpcCommand(
        DesignerHostIpcCommandKind kind,
        string designDocumentPath,
        string? projectPath,
        IntPtr parentWindowHandle)
    {
        Kind = kind;
        DesignDocumentPath = designDocumentPath;
        ProjectPath = projectPath;
        ParentWindowHandle = parentWindowHandle;
    }

    public DesignerHostIpcCommandKind Kind { get; }

    public string DesignDocumentPath { get; }

    public string? ProjectPath { get; }

    public IntPtr ParentWindowHandle { get; }

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

        var normalizedProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : projectPath;
        var parentWindowHandle = IntPtr.Zero;

        switch (kind)
        {
            case DesignerHostIpcCommandKind.Open:
            case DesignerHostIpcCommandKind.Save:
            case DesignerHostIpcCommandKind.QueryDirty:
            case DesignerHostIpcCommandKind.DiscardRecovery:
            case DesignerHostIpcCommandKind.Shutdown:
                if (string.IsNullOrWhiteSpace(designDocumentPath))
                    return false;
                break;
            case DesignerHostIpcCommandKind.AttachParent:
                if (!long.TryParse(
                        designDocumentPath,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var handleValue)
                    || handleValue == 0)
                {
                    return false;
                }

                parentWindowHandle = new IntPtr(handleValue);
                if (normalizedProjectPath is not null)
                    return false;
                break;
            case DesignerHostIpcCommandKind.Park:
            case DesignerHostIpcCommandKind.Resize:
            case DesignerHostIpcCommandKind.Show:
            case DesignerHostIpcCommandKind.Hide:
            case DesignerHostIpcCommandKind.Focus:
                if (designDocumentPath.Length != 0 || normalizedProjectPath is not null)
                    return false;
                break;
            default:
                return false;
        }

        command = new DesignerHostIpcCommand(
            kind,
            designDocumentPath,
            normalizedProjectPath,
            parentWindowHandle);
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

        if (string.Equals(value, "DISCARD", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.DiscardRecovery;
            return true;
        }

        if (string.Equals(value, "SHUTDOWN", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.Shutdown;
            return true;
        }

        if (string.Equals(value, "ATTACH", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.AttachParent;
            return true;
        }

        if (string.Equals(value, "PARK", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.Park;
            return true;
        }

        if (string.Equals(value, "RESIZE", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.Resize;
            return true;
        }

        if (string.Equals(value, "SHOW", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.Show;
            return true;
        }

        if (string.Equals(value, "HIDE", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.Hide;
            return true;
        }

        if (string.Equals(value, "FOCUS", StringComparison.Ordinal))
        {
            kind = DesignerHostIpcCommandKind.Focus;
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
    DiscardRecovery,
    Shutdown,
    AttachParent,
    Park,
    Resize,
    Show,
    Hide,
    Focus
}
