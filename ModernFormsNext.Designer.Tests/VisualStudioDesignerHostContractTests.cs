using ModernFormsNext.VisualStudioExtension.Hosting;
using ModernFormsNext.VisualStudioExtension.Commands;
using ModernFormsNext.VisualStudioExtension;
using ModernFormsNext.VisualStudioDesignerHost;
using System.IO.Pipes;
using System.Text;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

/// <summary>
/// Isolates real pipe deadlines from other Designer test collections' thread-pool load.
/// </summary>
/// <remarks>
/// The listener and asynchronous Windows pipe connection both use the process thread pool.
/// CPU-intensive compilation tests can delay listener startup beyond the transport deadline
/// on low-core runners. Run these contracts alone without relaxing their bounded I/O checks
/// or disabling parallel execution for the rest of the assembly.
/// </remarks>
[CollectionDefinition(nameof(DesignerNamedPipeCollection), DisableParallelization = true)]
public sealed class DesignerNamedPipeCollection { }

[Collection(nameof(DesignerNamedPipeCollection))]
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
                "--host-mode", "integrated",
                "--owner-process", "777",
                "--parent-window", "123456"
            ]);

        Assert.Equal("C:\\Project\\Form1.mfdesign", arguments.DesignDocumentPath);
        Assert.Equal("C:\\Project\\Project.csproj", arguments.ProjectPath);
        Assert.Equal("endpoint", arguments.PipeName);
        Assert.Equal(DesignerHostingMode.Integrated, arguments.HostingMode);
        Assert.Equal(777, arguments.OwnerProcessId);
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

    [Fact]
    public void StandaloneHostArgumentsHaveNoParentWindowContract()
    {
        var arguments = DesignerHostArguments.Parse(
            [
                "--host-mode", "standalone",
                "--owner-process", "778",
                "--design-file", "C:\\Project\\Form1.mfdesign"
            ]);

        Assert.Equal(DesignerHostingMode.Standalone, arguments.HostingMode);
        Assert.Equal(IntPtr.Zero, arguments.ParentWindowHandle);
        Assert.Equal(778, arguments.OwnerProcessId);
    }

    [Theory]
    [InlineData("integrated", false)]
    [InlineData("standalone", true)]
    public void HostArgumentsRejectModeAndParentMismatch(string mode, bool includeParent)
    {
        var arguments = new List<string> { "--host-mode", mode };
        if (includeParent)
        {
            arguments.Add("--parent-window");
            arguments.Add("123");
        }

        Assert.Throws<ArgumentException>(() => DesignerHostArguments.Parse(arguments));
    }

    [Theory]
    [InlineData()]
    [InlineData("--host-mode", "unknown")]
    [InlineData("--owner-process", "0", "--host-mode", "standalone")]
    public void HostArgumentsRejectMissingOrInvalidExplicitHostingContract(params string[] values)
        => Assert.Throws<ArgumentException>(() => DesignerHostArguments.Parse(values));

    [Theory]
    [InlineData("OPEN", 0)]
    [InlineData("SAVE", 1)]
    [InlineData("DIRTY", 2)]
    [InlineData("DISCARD", 3)]
    [InlineData("SHUTDOWN", 4)]
    public void HostIpcParsesOnlyTheSupportedCommandShape(
        string commandName,
        int expectedKind)
    {
        var designPath = Convert.ToBase64String(Encoding.UTF8.GetBytes("C:\\Project\\Form1.mfdesign"));
        var projectPath = Convert.ToBase64String(Encoding.UTF8.GetBytes("C:\\Project\\Project.csproj"));
        var requestId = commandName == "SAVE" ? "save-contract-1" : string.Empty;

        var parsed = DesignerHostIpcCommand.TryParse(
            $"{commandName}\t{designPath}\t{projectPath}\t{requestId}",
            out var command);

        Assert.True(parsed);
        Assert.Equal((DesignerHostIpcCommandKind)expectedKind, command.Kind);
        Assert.Equal("C:\\Project\\Form1.mfdesign", command.DesignDocumentPath);
        Assert.Equal("C:\\Project\\Project.csproj", command.ProjectPath);
        Assert.Equal(string.IsNullOrEmpty(requestId) ? null : requestId, command.RequestId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UNKNOWN\tQQ==")]
    [InlineData("OPEN")]
    [InlineData("OPEN\tnot-base64")]
    [InlineData("SAVE\tQzpcUHJvamVjdFxGb3JtMS5tZmRlc2lnbg==\t")]
    public void HostIpcRejectsMalformedOrUnknownCommands(string input)
    {
        Assert.False(DesignerHostIpcCommand.TryParse(input, out _));
    }

    [Fact]
    public void HostIpcParsesParentReattachmentWithoutTreatingTheHwndAsADocument()
    {
        var parentHandle = Convert.ToBase64String(Encoding.UTF8.GetBytes("987654"));

        var parsed = DesignerHostIpcCommand.TryParse(
            $"ATTACH\t{parentHandle}\t",
            out var command);

        Assert.True(parsed);
        Assert.Equal(DesignerHostIpcCommandKind.AttachParent, command.Kind);
        Assert.Equal(new IntPtr(987654), command.ParentWindowHandle);
        Assert.Null(command.ProjectPath);
    }

    [Theory]
    [InlineData("PARK", 6)]
    [InlineData("RESIZE", 7)]
    [InlineData("SHOW", 8)]
    [InlineData("HIDE", 9)]
    [InlineData("FOCUS", 10)]
    public void HostIpcParsesPayloadFreeWindowLifecycleCommands(
        string commandName,
        int expectedKind)
    {
        Assert.True(
            DesignerHostIpcCommand.TryParse($"{commandName}\t\t", out var command));
        Assert.Equal((DesignerHostIpcCommandKind)expectedKind, command.Kind);
        Assert.Equal(IntPtr.Zero, command.ParentWindowHandle);
    }

    [Theory]
    [InlineData("ATTACH\tMA==\t")]
    [InlineData("ATTACH\tbm90LWEtaGFuZGxl\t")]
    [InlineData("PARK\tQQ==\t")]
    [InlineData("RESIZE\t\tQQ==")]
    public void HostIpcRejectsInvalidWindowLifecyclePayloads(string input)
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

        var response = await SendOpenCommandAsync(pipeName);

        Assert.Equal("OK", response);
        Assert.NotNull(received);
        Assert.Equal(DesignerHostIpcCommandKind.Open, received.Kind);
        Assert.Equal("C:\\Project\\Form1.mfdesign", received.DesignDocumentPath);
    }

    [Fact]
    public async Task IndependentPipeEndpointsDoNotCrossTalkAndOneCanOutliveTheOther()
    {
        var firstPipeName = $"ModernFormsNext-HostContract-A-{Guid.NewGuid():N}";
        var secondPipeName = $"ModernFormsNext-HostContract-B-{Guid.NewGuid():N}";
        using var firstServer = new DesignerHostIpcServer(
            firstPipeName,
            _ => Task.FromResult("FIRST"));
        using var secondServer = new DesignerHostIpcServer(
            secondPipeName,
            _ => Task.FromResult("SECOND"));
        firstServer.Start();
        secondServer.Start();

        Assert.Equal("FIRST", await SendOpenCommandAsync(firstPipeName));
        Assert.Equal("SECOND", await SendOpenCommandAsync(secondPipeName));

        firstServer.Dispose();

        Assert.Equal("SECOND", await SendOpenCommandAsync(secondPipeName));
    }

    [Fact]
    public async Task NamedPipeConnectionDelayDoesNotConsumeResponseTimeout()
    {
        var pipeName = $"ModernFormsNext-HostContract-Delayed-{Guid.NewGuid():N}";
        var clock = new ManualTimeoutProvider();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new DesignerHostIpcServer(pipeName, _ =>
        {
            received.SetResult();
#pragma warning disable VSTHRD003 // This test deliberately gates the background handler; finally always releases it.
            return reply.Task;
#pragma warning restore VSTHRD003
        });

        // Spend four seconds of virtual time waiting for startup, then two waiting
        // for a response. Each phase fits its own five-second deadline; their sum does not.
        var response = SendOpenCommandAsync(pipeName, clock);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(4));
            server.Start();
            await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(2));
            reply.SetResult("OK");

            Assert.Equal("OK", await response.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            // Always unblock the handler, even if the client or assertion fails.
            reply.TrySetResult("OK");
        }
    }

    [Fact]
    public async Task NamedPipeClientTimesOutWhenServerDoesNotStart()
    {
        var clock = new ManualTimeoutProvider();
        var response = SendOpenCommandAsync($"ModernFormsNext-HostContract-Missing-{Guid.NewGuid():N}", clock);

        clock.Advance(TimeSpan.FromSeconds(5));

        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => response.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("connecting", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NamedPipeClientTimesOutWhenServerDoesNotRespond()
    {
        var pipeName = $"ModernFormsNext-HostContract-Silent-{Guid.NewGuid():N}";
        var clock = new ManualTimeoutProvider();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new DesignerHostIpcServer(pipeName, _ =>
        {
            received.SetResult();
#pragma warning disable VSTHRD003 // This test deliberately gates the background handler; finally always releases it.
            return reply.Task;
#pragma warning restore VSTHRD003
        });
        server.Start();

        var response = SendOpenCommandAsync(pipeName, clock);
        try
        {
            await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(5));

            var exception = await Assert.ThrowsAsync<TimeoutException>(
                () => response.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Contains("response", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            reply.TrySetResult("OK");
        }
    }

    [Fact]
    public void DesignerHostDiagnosticPathsAreIsolatedPerProcess()
    {
        var directory = IOPath.Combine(IOPath.GetTempPath(), "ModernFormsNext-DesignerHost-Log-Contract");

        var first = DesignerHostDiagnosticLog.GetPath(directory, 101);
        var second = DesignerHostDiagnosticLog.GetPath(directory, 202);

        Assert.NotEqual(first, second);
        Assert.Equal("ModernFormsNextDesignerHost-101.log", IOPath.GetFileName(first));
        Assert.Equal("ModernFormsNextDesignerHost-202.log", IOPath.GetFileName(second));
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

    private static async Task<string?> SendOpenCommandAsync(string pipeName, TimeProvider? timeProvider = null)
    {
        timeProvider ??= TimeProvider.System;
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        // Start() queues the listener. ConnectAsync already waits for that endpoint;
        // startup and test-runner scheduling must not consume the response deadline.
        using (var connectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5), timeProvider))
        {
            try
            {
                await client.ConnectAsync(connectionTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (connectionTimeout.IsCancellationRequested)
            {
                throw new TimeoutException($"Named pipe '{pipeName}' timed out after 5 seconds while connecting.", exception);
            }
        }

        var designPath = Convert.ToBase64String(Encoding.UTF8.GetBytes("C:\\Project\\Form1.mfdesign"));
        var request = Encoding.UTF8.GetBytes($"OPEN\t{designPath}\t{Environment.NewLine}");
        using var reader = new StreamReader(client, Encoding.UTF8, true, 1024, leaveOpen: true);
        using var responseTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5), timeProvider);
        try
        {
            // Write the complete UTF-8 line directly so sending and receiving are both
            // cancellable, with no buffered writer flush left to block during disposal.
            // Pipe I/O continuations do not need the test runner's synchronization context.
            await client.WriteAsync(request, responseTimeout.Token).ConfigureAwait(false);
            return await reader.ReadLineAsync(responseTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (responseTimeout.IsCancellationRequested)
        {
            throw new TimeoutException($"Named pipe '{pipeName}' timed out after 5 seconds while exchanging the command/response.", exception);
        }
    }

    // Only the client's one-shot deadline timers use this clock. Pipe I/O is real;
    // the tests advance time at explicit startup/request barriers instead of sleeping.
    private sealed class ManualTimeoutProvider : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<DeadlineTimer> timers = [];
        private TimeSpan elapsed;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new DeadlineTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            DeadlineTimer[] expired;
            lock (gate)
            {
                elapsed += duration;
                expired = timers.Where(timer => timer.DueAt <= elapsed).ToArray();
                foreach (var timer in expired)
                    timers.Remove(timer);
            }

            // Cancellation callbacks may dispose their own timer; invoke outside the lock.
            foreach (var timer in expired)
                timer.Fire();
        }

        private sealed class DeadlineTimer(ManualTimeoutProvider owner, TimerCallback callback, object? state) : ITimer
        {
            private bool disposed;
            public TimeSpan DueAt { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (period != Timeout.InfiniteTimeSpan)
                    throw new NotSupportedException("Only one-shot cancellation deadlines are supported.");

                lock (owner.gate)
                {
                    if (disposed)
                        return false;
                    owner.timers.Remove(this);
                    if (dueTime != Timeout.InfiniteTimeSpan)
                    {
                        DueAt = owner.elapsed + dueTime;
                        owner.timers.Add(this);
                    }
                    return true;
                }
            }

            public void Fire() => callback(state);

            public void Dispose()
            {
                lock (owner.gate)
                {
                    disposed = true;
                    owner.timers.Remove(this);
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
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
