using ModernFormsNext.VisualStudioExtension.Hosting;
using ModernFormsNext.VisualStudioExtension.Commands;
using ModernFormsNext.VisualStudioDesignerHost;
using System.IO.Pipes;
using System.Text;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class VisualStudioDesignerHostContractTests
{
    [Fact]
    public void HostArgumentsCarryTheTypedParentWindowContract()
    {
        var arguments = DesignerHostArguments.Parse(
            [
                "--design-file", "C:\\Project\\Form1.mfdesign",
                "--project", "C:\\Project\\Project.csproj",
                "--pipe", "endpoint",
                "--parent-window", "123456"
            ]);

        Assert.Equal("C:\\Project\\Form1.mfdesign", arguments.DesignDocumentPath);
        Assert.Equal("C:\\Project\\Project.csproj", arguments.ProjectPath);
        Assert.Equal("endpoint", arguments.PipeName);
        Assert.Equal(new IntPtr(123456), arguments.ParentWindowHandle);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("not-a-handle")]
    public void HostArgumentsRejectInvalidParentWindowHandles(string value)
    {
        Assert.Throws<ArgumentException>(
            () => DesignerHostArguments.Parse(["--parent-window", value]));
    }

    [Theory]
    [InlineData("OPEN", 0)]
    [InlineData("SAVE", 1)]
    [InlineData("DIRTY", 2)]
    [InlineData("SHUTDOWN", 3)]
    public void HostIpcParsesOnlyTheSupportedCommandShape(
        string commandName,
        int expectedKind)
    {
        var designPath = Convert.ToBase64String(Encoding.UTF8.GetBytes("C:\\Project\\Form1.mfdesign"));
        var projectPath = Convert.ToBase64String(Encoding.UTF8.GetBytes("C:\\Project\\Project.csproj"));

        var parsed = DesignerHostIpcCommand.TryParse(
            $"{commandName}\t{designPath}\t{projectPath}",
            out var command);

        Assert.True(parsed);
        Assert.Equal((DesignerHostIpcCommandKind)expectedKind, command.Kind);
        Assert.Equal("C:\\Project\\Form1.mfdesign", command.DesignDocumentPath);
        Assert.Equal("C:\\Project\\Project.csproj", command.ProjectPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UNKNOWN\tQQ==")]
    [InlineData("OPEN")]
    [InlineData("OPEN\tnot-base64")]
    public void HostIpcRejectsMalformedOrUnknownCommands(string input)
    {
        Assert.False(DesignerHostIpcCommand.TryParse(input, out _));
    }

    [Fact]
    public async Task RealNamedPipeTransportCorrelatesOneCommandWithOneResponse()
    {
        var pipeName = $"ModernFormsNext-HostContract-{Guid.NewGuid():N}";
        DesignerHostIpcCommand? received = null;
        using var server = new DesignerHostIpcServer(
            pipeName,
            command =>
            {
                received = command;
                return Task.FromResult("OK");
            });
        server.Start();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(timeout.Token);

        var designPath = Convert.ToBase64String(Encoding.UTF8.GetBytes("C:\\Project\\Form1.mfdesign"));
        await using var writer = new StreamWriter(client, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync($"OPEN\t{designPath}\t");
        using var reader = new StreamReader(client, Encoding.UTF8, true, 1024, leaveOpen: true);
        var response = await reader.ReadLineAsync(timeout.Token);

        Assert.Equal("OK", response);
        Assert.NotNull(received);
        Assert.Equal(DesignerHostIpcCommandKind.Open, received.Kind);
        Assert.Equal("C:\\Project\\Form1.mfdesign", received.DesignDocumentPath);
    }

    [Fact]
    public void IpcServerDisposeIsIdempotentBeforeAndAfterStartup()
    {
        var notStarted = new DesignerHostIpcServer(
            $"ModernFormsNext-HostContract-{Guid.NewGuid():N}",
            _ => Task.FromResult("OK"));
        notStarted.Dispose();
        notStarted.Dispose();

        var started = new DesignerHostIpcServer(
            $"ModernFormsNext-HostContract-{Guid.NewGuid():N}",
            _ => Task.FromResult("OK"));
        started.Start();
        started.Dispose();
        started.Dispose();
    }

    [Theory]
    [InlineData(false, false, true, false, false, false)]
    [InlineData(true, false, true, false, false, false)]
    [InlineData(true, true, true, true, true, true)]
    [InlineData(false, false, false, true, false, false)]
    [InlineData(true, false, false, true, true, false)]
    [InlineData(true, true, false, true, true, true)]
    public void CommandRoutingOnlyClaimsStandardViewDesignerForSupportedFiles(
        bool hasCandidateFile,
        bool isDesignable,
        bool isStandardCommand,
        bool expectedSupported,
        bool expectedVisible,
        bool expectedEnabled)
    {
        var status = VisualStudioDesignerCommandRouter.Evaluate(
            hasCandidateFile,
            isDesignable,
            isStandardCommand);

        Assert.Equal(expectedSupported, status.Supported);
        Assert.Equal(expectedVisible, status.Visible);
        Assert.Equal(expectedEnabled, status.Enabled);
    }

    [Fact]
    public void LifecycleCoordinatesAttachResizeDpiFocusCloseAndReopen()
    {
        var operations = new RecordingNativeWindowOperations();
        using var lifecycle = new VisualStudioDesignerHostLifecycle(operations);

        lifecycle.Attach(new IntPtr(11), new IntPtr(22));
        lifecycle.Resize(0, -10);
        lifecycle.UpdateDpi(144, 640, 480);
        lifecycle.Focus();
        lifecycle.Detach();
        lifecycle.Attach(new IntPtr(33), new IntPtr(44));

        Assert.Equal(VisualStudioDesignerHostState.Attached, lifecycle.State);
        Assert.Equal(144, lifecycle.Dpi);
        Assert.Equal(
            [
                "attach:11:22",
                "resize:11:1:1",
                "resize:11:640:480",
                "focus:11",
                "detach:11",
                "attach:33:44"
            ],
            operations.Calls);
    }

    [Fact]
    public void AttachFailureRollsBackPartialNativeStateAndReportsDiagnostic()
    {
        var operations = new RecordingNativeWindowOperations
        {
            AttachFailure = new InvalidOperationException("simulated SetParent failure")
        };
        using var lifecycle = new VisualStudioDesignerHostLifecycle(operations);

        var exception = Assert.Throws<InvalidOperationException>(
            () => lifecycle.Attach(new IntPtr(11), new IntPtr(22)));

        Assert.Equal(VisualStudioDesignerHostState.Faulted, lifecycle.State);
        Assert.Contains("Could not attach the Designer HWND", exception.Message, StringComparison.Ordinal);
        Assert.Contains("simulated SetParent failure", lifecycle.LastDiagnostic, StringComparison.Ordinal);
        Assert.Equal(["attach:11:22", "detach:11"], operations.Calls);
    }

    [Fact]
    public void InvalidHandlesAndDpiAreRejectedBeforeNativeMutation()
    {
        var operations = new RecordingNativeWindowOperations();
        using var lifecycle = new VisualStudioDesignerHostLifecycle(operations);

        Assert.Throws<ArgumentException>(() => lifecycle.Attach(IntPtr.Zero, new IntPtr(22)));
        Assert.Throws<ArgumentException>(() => lifecycle.Attach(new IntPtr(11), IntPtr.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => lifecycle.UpdateDpi(0, 100, 100));
        Assert.Empty(operations.Calls);
    }

    [Fact]
    public void ResizeFailureKeepsTheAttachmentAvailableForAQueuedRetry()
    {
        var operations = new RecordingNativeWindowOperations();
        using var lifecycle = new VisualStudioDesignerHostLifecycle(operations);
        lifecycle.Attach(new IntPtr(11), new IntPtr(22));
        operations.ResizeFailure = new InvalidOperationException("simulated resize failure");

        var exception = Assert.Throws<InvalidOperationException>(() => lifecycle.Resize(200, 100));
        operations.ResizeFailure = null;
        lifecycle.Resize(300, 150);
        lifecycle.Detach();

        Assert.Equal(VisualStudioDesignerHostState.Detached, lifecycle.State);
        Assert.Contains("Could not resize the Designer HWND", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            ["attach:11:22", "resize:11:200:100", "resize:11:300:150", "detach:11"],
            operations.Calls);
    }

    [Fact]
    public void DisposeDetachesOnlyTheOwnedChildAndRejectsLaterOperations()
    {
        var operations = new RecordingNativeWindowOperations();
        var lifecycle = new VisualStudioDesignerHostLifecycle(operations);
        lifecycle.Attach(new IntPtr(11), new IntPtr(22));

        lifecycle.Dispose();
        lifecycle.Dispose();

        Assert.Equal(VisualStudioDesignerHostState.Disposed, lifecycle.State);
        Assert.Equal(["attach:11:22", "detach:11"], operations.Calls);
        Assert.Throws<ObjectDisposedException>(() => lifecycle.Resize(1, 1));
    }

    private sealed class RecordingNativeWindowOperations : IVisualStudioNativeWindowOperations
    {
        public List<string> Calls { get; } = [];

        public Exception? AttachFailure { get; set; }

        public Exception? ResizeFailure { get; set; }

        public void Attach(IntPtr childHandle, IntPtr parentHandle)
        {
            Calls.Add($"attach:{childHandle}:{parentHandle}");
            if (AttachFailure is not null)
                throw AttachFailure;
        }

        public void Resize(IntPtr childHandle, int width, int height)
        {
            Calls.Add($"resize:{childHandle}:{width}:{height}");
            if (ResizeFailure is not null)
                throw ResizeFailure;
        }

        public void Focus(IntPtr childHandle)
            => Calls.Add($"focus:{childHandle}");

        public void Detach(IntPtr childHandle)
            => Calls.Add($"detach:{childHandle}");
    }
}
