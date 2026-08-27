using System.Diagnostics;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using ModernFormsNext.VisualStudioDesignerHost;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class VisualStudioDesignerHostProcessTests
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const uint GwOwner = 4;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const long WsChild = 0x40000000L;
    private const long WsVisible = 0x10000000L;
    private const long WsClipSiblings = 0x04000000L;
    private const long WsPopup = unchecked((long)0x80000000);
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsMinimize = 0x20000000L;
    private const long WsMaximize = 0x01000000L;
    private const long WsExTopmost = 0x00000008L;
    private const long WsExWindowEdge = 0x00000100L;
    private const long WsExClientEdge = 0x00000200L;
    private const long WsExAppWindow = 0x00040000L;
    private const uint WmSettingChange = 0x001A;
    private const uint SmtoAbortIfHung = 0x0002;

    [Fact]
    public async Task RealHostReachesIpcReadyAndSurvivesTheSettingsMessageBeforeOpen()
    {
        var hostPath = IOPath.Combine(
            AppContext.BaseDirectory,
            "ModernFormsNext.VisualStudioDesignerHost.exe");
        Assert.True(File.Exists(hostPath), $"Designer host executable was not found at '{hostPath}'.");

        var pipeName = $"ModernFormsNext-EarlyStartup-{Guid.NewGuid():N}";
        var designPath = IOPath.Combine(
            IOPath.GetTempPath(),
            $"ModernFormsNext-EarlyStartup-{Guid.NewGuid():N}.mfdesign");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = hostPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };
        process.StartInfo.ArgumentList.Add("--design-file");
        process.StartInfo.ArgumentList.Add(designPath);
        process.StartInfo.ArgumentList.Add("--pipe");
        process.StartInfo.ArgumentList.Add(pipeName);

        Assert.True(process.Start());
        var logPath = DesignerHostDiagnosticLog.GetPath(IOPath.GetTempPath(), process.Id);

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await WaitForLogMarkerAsync(
                process,
                logPath,
                $"IPC_READY PipeName={pipeName}",
                timeout.Token);
            Assert.False(process.HasExited, await ReadLogAsync(logPath));

            var messageWindow = await WaitForPlatformMessageWindowAsync(process, timeout.Token);
            var sent = SendMessageTimeout(
                messageWindow,
                WmSettingChange,
                IntPtr.Zero,
                IntPtr.Zero,
                SmtoAbortIfHung,
                5_000,
                out _);
            Assert.NotEqual(IntPtr.Zero, sent);

            Assert.Equal("OK", await SendCommandAsync(pipeName, "OPEN", designPath, timeout.Token));
            Assert.Equal("DIRTY\t0", await SendCommandAsync(pipeName, "DIRTY", designPath, timeout.Token));
            Assert.False(process.HasExited, await ReadLogAsync(logPath));
            Assert.Equal("OK", await SendCommandAsync(pipeName, "SHUTDOWN", designPath, timeout.Token));

            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);

            var log = ExtractLastRun(await ReadLogAsync(logPath));
            Assert.Contains("START", log, StringComparison.Ordinal);
            Assert.Contains("ARGS_PARSED", log, StringComparison.Ordinal);
            Assert.Contains("DPI_SETUP_BEGIN", log, StringComparison.Ordinal);
            Assert.Contains("DPI_SETUP_OK", log, StringComparison.Ordinal);
            Assert.Contains("FORM_CONSTRUCTOR_BEGIN", log, StringComparison.Ordinal);
            Assert.Contains("HANDLE_CREATED", log, StringComparison.Ordinal);
            Assert.Contains("FORM_CONSTRUCTOR_OK", log, StringComparison.Ordinal);
            Assert.Contains("IPC_SERVER_CREATE_BEGIN", log, StringComparison.Ordinal);
            Assert.Contains("IPC_SERVER_CREATE_OK", log, StringComparison.Ordinal);
            Assert.Contains("APPLICATION_RUN_BEGIN", log, StringComparison.Ordinal);
            Assert.Contains("FORM_LOAD", log, StringComparison.Ordinal);
            Assert.Contains("FORM_SHOWN", log, StringComparison.Ordinal);
            Assert.Contains($"IPC_READY PipeName={pipeName}", log, StringComparison.Ordinal);
            Assert.Contains("OPEN_RECEIVED", log, StringComparison.Ordinal);
            Assert.DoesNotContain("UNHANDLED_EXCEPTION", log, StringComparison.Ordinal);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Fact]
    public async Task RealHostIsABorderlessChildAndSurvivesParentHandleRecreation()
    {
        using var firstParent = new NativeParentWindow(800, 600);
        var pipeName = $"ModernFormsNext-EmbeddedHost-{Guid.NewGuid():N}";
        var designPath = IOPath.Combine(
            IOPath.GetTempPath(),
            $"ModernFormsNext-EmbeddedHost-{Guid.NewGuid():N}.mfdesign");
        using var process = StartHost(pipeName, designPath, firstParent.Handle);
        var logPath = DesignerHostDiagnosticLog.GetPath(IOPath.GetTempPath(), process.Id);

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await WaitForLogMarkerAsync(process, logPath, "FORM_SHOWN", timeout.Token);
            await WaitForLogMarkerAsync(process, logPath, $"IPC_READY PipeName={pipeName}", timeout.Token);

            var child = await WaitForChildWindowAsync(firstParent.Handle, process, timeout.Token);
            AssertHostedChildContract(child, firstParent.Handle, 800, 600);

            firstParent.Resize(1111, 733);
            Assert.Equal("OK", await SendCommandAsync(pipeName, "RESIZE", string.Empty, timeout.Token));
            await WaitForWindowBoundsAsync(child, firstParent.Handle, 1111, 733, timeout.Token);

            firstParent.SetVisible(false);
            await WaitForConditionAsync(
                () => !IsWindowVisible(child),
                "The child remained effectively visible after its parent was hidden.",
                timeout.Token);
            Assert.Equal("OK", await SendCommandAsync(pipeName, "HIDE", string.Empty, timeout.Token));

            firstParent.SetVisible(true);
            Assert.Equal("OK", await SendCommandAsync(pipeName, "SHOW", string.Empty, timeout.Token));
            await WaitForConditionAsync(
                () => IsWindowVisible(child),
                "The child did not become visible with its parent.",
                timeout.Token);

            Assert.Equal("OK", await SendCommandAsync(pipeName, "FOCUS", string.Empty, timeout.Token));
            await WaitForLogMarkerAsync(process, logPath, "FOCUS_REQUEST", timeout.Token);
            Assert.Equal(child, GetThreadFocus(child));

            Assert.Equal("OK", await SendCommandAsync(pipeName, "PARK", string.Empty, timeout.Token));
            await WaitForConditionAsync(
                () => GetParent(child) != firstParent.Handle && !IsWindowVisible(child),
                "The child remained parented to the obsolete visible pane.",
                timeout.Token);
            Assert.True(IsWindow(child));
            Assert.False(IsTopLevelWindow(child));

            firstParent.Close();
            Assert.True(IsWindow(child));

            using var secondParent = new NativeParentWindow(977, 641);
            Assert.Equal(
                "OK",
                await SendCommandAsync(
                    pipeName,
                    "ATTACH",
                    secondParent.Handle.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
                    timeout.Token));
            Assert.Equal("OK", await SendCommandAsync(pipeName, "SHOW", string.Empty, timeout.Token));
            await WaitForConditionAsync(
                () => GetParent(child) == secondParent.Handle,
                "The child did not attach to the recreated parent HWND.",
                timeout.Token);
            AssertHostedChildContract(child, secondParent.Handle, 977, 641);
            Assert.Equal("OK", await SendCommandAsync(pipeName, "SHUTDOWN", designPath, timeout.Token));

            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);

            var log = ExtractLastRun(await ReadLogAsync(logPath));
            Assert.Contains("HOST_WINDOW_CREATED", log, StringComparison.Ordinal);
            Assert.Contains(
                "EMBEDDED_CHROME_DISABLED TitleBarVisible=False Resizeable=False " +
                "AllowMinimize=False AllowMaximize=False BorderWidth=0",
                log,
                StringComparison.Ordinal);
            Assert.Contains("PARENT_ATTACH_BEGIN", log, StringComparison.Ordinal);
            Assert.Contains("STYLE_BEFORE_ATTACH", log, StringComparison.Ordinal);
            Assert.Contains("STYLE_AFTER_ATTACH", log, StringComparison.Ordinal);
            Assert.Contains("PARENT_ATTACH_OK", log, StringComparison.Ordinal);
            Assert.Contains("ATTACHED", log, StringComparison.Ordinal);
            Assert.Contains("StyleBefore=", log, StringComparison.Ordinal);
            Assert.Contains("StyleAfter=", log, StringComparison.Ordinal);
            Assert.Contains("ExStyleBefore=", log, StringComparison.Ordinal);
            Assert.Contains("ExStyleAfter=", log, StringComparison.Ordinal);
            Assert.Contains("PARENT_PARKED", log, StringComparison.Ordinal);
            Assert.Contains("PARENT_RECREATED", log, StringComparison.Ordinal);
            Assert.Contains("BOUNDS_UPDATED X=0 Y=0 Width=1111 Height=733", log, StringComparison.Ordinal);
            Assert.Contains("VISIBILITY_CHANGED Requested=False", log, StringComparison.Ordinal);
            Assert.Contains("VISIBILITY_CHANGED Requested=True", log, StringComparison.Ordinal);
            Assert.DoesNotContain("IPC_COMMAND_EXCEPTION", log, StringComparison.Ordinal);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Fact]
    public async Task TwoRealHostsRemainIndependentChildrenOfTheirOwnParents()
    {
        using var firstParent = new NativeParentWindow(601, 401);
        using var secondParent = new NativeParentWindow(701, 501);
        var firstPipe = $"ModernFormsNext-IndependentHost-A-{Guid.NewGuid():N}";
        var secondPipe = $"ModernFormsNext-IndependentHost-B-{Guid.NewGuid():N}";
        var firstDesignPath = IOPath.Combine(IOPath.GetTempPath(), $"Host-A-{Guid.NewGuid():N}.mfdesign");
        var secondDesignPath = IOPath.Combine(IOPath.GetTempPath(), $"Host-B-{Guid.NewGuid():N}.mfdesign");
        using var firstProcess = StartHost(firstPipe, firstDesignPath, firstParent.Handle);
        using var secondProcess = StartHost(secondPipe, secondDesignPath, secondParent.Handle);

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await WaitForLogMarkerAsync(
                firstProcess,
                DesignerHostDiagnosticLog.GetPath(IOPath.GetTempPath(), firstProcess.Id),
                "FORM_SHOWN",
                timeout.Token);
            await WaitForLogMarkerAsync(
                secondProcess,
                DesignerHostDiagnosticLog.GetPath(IOPath.GetTempPath(), secondProcess.Id),
                "FORM_SHOWN",
                timeout.Token);
            var firstChild = await WaitForChildWindowAsync(firstParent.Handle, firstProcess, timeout.Token);
            var secondChild = await WaitForChildWindowAsync(secondParent.Handle, secondProcess, timeout.Token);

            Assert.NotEqual(firstChild, secondChild);
            AssertHostedChildContract(firstChild, firstParent.Handle, 601, 401);
            AssertHostedChildContract(secondChild, secondParent.Handle, 701, 501);

            Assert.Equal("OK", await SendCommandAsync(firstPipe, "SHUTDOWN", firstDesignPath, timeout.Token));
            await firstProcess.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, firstProcess.ExitCode);
            Assert.False(secondProcess.HasExited);
            Assert.True(IsWindow(secondChild));
            Assert.Equal(secondParent.Handle, GetParent(secondChild));

            Assert.Equal("OK", await SendCommandAsync(secondPipe, "SHUTDOWN", secondDesignPath, timeout.Token));
            await secondProcess.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, secondProcess.ExitCode);
        }
        finally
        {
            if (!firstProcess.HasExited)
            {
                firstProcess.Kill(entireProcessTree: true);
                await firstProcess.WaitForExitAsync();
            }
            if (!secondProcess.HasExited)
            {
                secondProcess.Kill(entireProcessTree: true);
                await secondProcess.WaitForExitAsync();
            }
        }
    }

    [Fact]
    public void ExceptionDiagnosticIncludesHResultStackAndNestedFailure()
    {
        var inner = new Win32Exception(5, "inner failure");
        var outer = new ApplicationException("outer failure", inner);

        var diagnostic = DesignerHostDiagnosticLog.FormatException("TEST_EXCEPTION", outer);

        Assert.Contains("TEST_EXCEPTION", diagnostic, StringComparison.Ordinal);
        Assert.Contains("System.ApplicationException", diagnostic, StringComparison.Ordinal);
        Assert.Contains("HResult: 0x80131600", diagnostic, StringComparison.Ordinal);
        Assert.Contains("StackTrace:", diagnostic, StringComparison.Ordinal);
        Assert.Contains("System.ComponentModel.Win32Exception", diagnostic, StringComparison.Ordinal);
        Assert.Contains("NativeErrorCode: 5", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Environment.StackTrace:", diagnostic, StringComparison.Ordinal);
    }

    private static Process StartHost(string pipeName, string designPath, IntPtr parentHandle)
    {
        var hostPath = IOPath.Combine(
            AppContext.BaseDirectory,
            "ModernFormsNext.VisualStudioDesignerHost.exe");
        Assert.True(File.Exists(hostPath), $"Designer host executable was not found at '{hostPath}'.");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = hostPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };
        process.StartInfo.ArgumentList.Add("--design-file");
        process.StartInfo.ArgumentList.Add(designPath);
        process.StartInfo.ArgumentList.Add("--pipe");
        process.StartInfo.ArgumentList.Add(pipeName);
        process.StartInfo.ArgumentList.Add("--parent-window");
        process.StartInfo.ArgumentList.Add(
            parentHandle.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(process.Start());
        return process;
    }

    private static void AssertHostedChildContract(
        IntPtr child,
        IntPtr expectedParent,
        int expectedWidth,
        int expectedHeight)
    {
        var style = GetWindowLongPtr(child, GwlStyle).ToInt64();
        var extendedStyle = GetWindowLongPtr(child, GwlExStyle).ToInt64();

        Assert.NotEqual(0, style & WsChild);
        Assert.NotEqual(0, style & WsVisible);
        Assert.NotEqual(0, style & WsClipSiblings);
        Assert.Equal(0, style & WsPopup);
        Assert.Equal(0, style & WsCaption);
        Assert.Equal(0, style & WsThickFrame);
        Assert.Equal(0, style & WsSysMenu);
        Assert.Equal(0, style & WsMinimizeBox);
        Assert.Equal(0, style & WsMaximizeBox);
        Assert.Equal(0, style & WsMinimize);
        Assert.Equal(0, style & WsMaximize);
        Assert.Equal(0, extendedStyle & WsExTopmost);
        Assert.Equal(0, extendedStyle & WsExWindowEdge);
        Assert.Equal(0, extendedStyle & WsExClientEdge);
        Assert.Equal(0, extendedStyle & WsExAppWindow);
        Assert.Equal(expectedParent, GetParent(child));
        Assert.Equal(IntPtr.Zero, GetWindow(child, GwOwner));
        Assert.False(IsTopLevelWindow(child));

        Assert.True(GetWindowRect(child, out var windowBounds));
        _ = MapWindowPoints(IntPtr.Zero, expectedParent, ref windowBounds, 2);
        Assert.Equal(0, windowBounds.Left);
        Assert.Equal(0, windowBounds.Top);
        Assert.Equal(expectedWidth, windowBounds.Right - windowBounds.Left);
        Assert.Equal(expectedHeight, windowBounds.Bottom - windowBounds.Top);
    }

    private static async Task<IntPtr> WaitForChildWindowAsync(
        IntPtr parent,
        Process process,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var child = FindChildWindowOwnedByProcess(parent, process.Id);
            if (child != IntPtr.Zero)
                return child;
            if (process.HasExited)
            {
                var logPath = DesignerHostDiagnosticLog.GetPath(IOPath.GetTempPath(), process.Id);
                throw new InvalidOperationException(
                    $"Designer host exited with code {process.ExitCode} before attachment.{Environment.NewLine}" +
                    await ReadLogAsync(logPath));
            }

            await Task.Delay(25, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return IntPtr.Zero;
    }

    private static IntPtr FindChildWindowOwnedByProcess(IntPtr parent, int processId)
    {
        var result = IntPtr.Zero;
        _ = EnumChildWindows(parent, (window, parameter) =>
        {
            _ = GetWindowThreadProcessId(window, out var ownerProcessId);
            if (ownerProcessId != processId)
                return true;

            result = window;
            return false;
        }, IntPtr.Zero);
        return result;
    }

    private static async Task WaitForWindowBoundsAsync(
        IntPtr child,
        IntPtr parent,
        int width,
        int height,
        CancellationToken cancellationToken)
        => await WaitForConditionAsync(
            () =>
            {
                if (!GetWindowRect(child, out var bounds))
                    return false;
                _ = MapWindowPoints(IntPtr.Zero, parent, ref bounds, 2);
                return bounds.Left == 0
                    && bounds.Top == 0
                    && bounds.Right - bounds.Left == width
                    && bounds.Bottom - bounds.Top == height;
            },
            $"The child did not reach parent-relative bounds 0,0,{width},{height}.",
            cancellationToken);

    private static async Task WaitForConditionAsync(
        Func<bool> predicate,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (predicate())
                return;
            await Task.Delay(25, cancellationToken);
        }

        throw new TimeoutException(failureMessage);
    }

    private static bool IsTopLevelWindow(IntPtr expected)
    {
        var found = false;
        _ = EnumWindows((window, _) =>
        {
            if (window != expected)
                return true;

            found = true;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static IntPtr GetThreadFocus(IntPtr window)
    {
        var threadId = GetWindowThreadProcessId(window, out _);
        var info = new GuiThreadInfo
        {
            Size = Marshal.SizeOf<GuiThreadInfo>()
        };
        Assert.True(GetGUIThreadInfo(threadId, ref info));
        return info.FocusWindow;
    }

    private static async Task WaitForLogMarkerAsync(
        Process process,
        string logPath,
        string marker,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var log = await ReadLogAsync(logPath);
            if (log.Contains(marker, StringComparison.Ordinal))
                return;
            if (process.HasExited)
                throw new InvalidOperationException(
                    $"Designer host exited with code {process.ExitCode} before marker '{marker}'.{Environment.NewLine}{log}");

            await Task.Delay(25, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task<IntPtr> WaitForPlatformMessageWindowAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var handle = FindPlatformMessageWindow(process.Id);
            if (handle != IntPtr.Zero)
                return handle;
            if (process.HasExited)
                throw new InvalidOperationException(
                    $"Designer host exited with code {process.ExitCode} before its platform message window was found.");

            await Task.Delay(25, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return IntPtr.Zero;
    }

    private static IntPtr FindPlatformMessageWindow(int processId)
    {
        var result = IntPtr.Zero;
        _ = EnumWindows((window, parameter) =>
        {
            _ = GetWindowThreadProcessId(window, out var ownerProcessId);
            if (ownerProcessId != processId)
                return true;

            var className = new StringBuilder(256);
            _ = GetClassName(window, className, className.Capacity);
            if (!className.ToString().StartsWith("AvaloniaMessageWindow ", StringComparison.Ordinal))
                return true;

            result = window;
            return false;
        }, IntPtr.Zero);
        return result;
    }

    private static async Task<string?> SendCommandAsync(
        string pipeName,
        string command,
        string designPath,
        CancellationToken cancellationToken)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(cancellationToken);

        var encodedDesignPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(designPath));
        await using var writer = new StreamWriter(client, Encoding.UTF8, 1024, leaveOpen: true)
        {
            AutoFlush = true
        };
        await writer.WriteLineAsync($"{command}\t{encodedDesignPath}\t");
        using var reader = new StreamReader(client, Encoding.UTF8, true, 1024, leaveOpen: true);
        return await reader.ReadLineAsync(cancellationToken);
    }

    private static async Task<string> ReadLogAsync(string logPath)
    {
        if (!File.Exists(logPath))
            return string.Empty;

        await using var stream = new FileStream(
            logPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static string ExtractLastRun(string log)
    {
        var marker = $"{Environment.NewLine}[";
        var start = log.LastIndexOf($"] START{Environment.NewLine}", StringComparison.Ordinal);
        if (start < 0)
            return log;

        var lineStart = log.LastIndexOf(marker, start, StringComparison.Ordinal);
        return lineStart < 0 ? log : log[(lineStart + Environment.NewLine.Length)..];
    }

    private sealed class NativeParentWindow : IDisposable
    {
        private const uint ParentWindowStyle = 0x92000000;
        private const uint WmClose = 0x0010;
        private const uint WmQuit = 0x0012;

        private readonly ManualResetEventSlim ready = new();
        private readonly Thread thread;
        private readonly int initialWidth;
        private readonly int initialHeight;
        private Exception? startupFailure;
        private uint threadId;
        private IntPtr handle;
        private int closed;

        public NativeParentWindow(int width, int height)
        {
            initialWidth = width;
            initialHeight = height;
            thread = new Thread(RunMessageLoop)
            {
                IsBackground = true,
                Name = "ModernFormsNext Designer test parent HWND"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!ready.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The native parent HWND was not created in time.");
            if (startupFailure is not null)
                throw new InvalidOperationException("The native parent HWND could not be created.", startupFailure);
        }

        public IntPtr Handle
            => handle != IntPtr.Zero
                ? handle
                : throw new ObjectDisposedException(nameof(NativeParentWindow));

        public void Resize(int width, int height)
        {
            if (!SetWindowPos(
                    Handle,
                    IntPtr.Zero,
                    -20_000,
                    -20_000,
                    width,
                    height,
                    SwpNoZOrder | SwpNoActivate))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The native parent HWND could not be resized.");
            }
        }

        public void SetVisible(bool visible)
            => _ = ShowWindow(Handle, visible ? SwShowNoActivate : SwHide);

        public void Close()
        {
            if (Interlocked.Exchange(ref closed, 1) != 0)
                return;

            var window = handle;
            if (window != IntPtr.Zero)
                _ = SendMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
            if (threadId != 0)
                _ = PostThreadMessage(threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
            if (!thread.Join(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The native parent HWND thread did not stop in time.");

            handle = IntPtr.Zero;
            ready.Dispose();
        }

        public void Dispose()
            => Close();

        private void RunMessageLoop()
        {
            try
            {
                threadId = GetCurrentThreadId();
                handle = CreateWindowEx(
                    0,
                    "STATIC",
                    "ModernFormsNext Designer test parent",
                    ParentWindowStyle,
                    -20_000,
                    -20_000,
                    initialWidth,
                    initialHeight,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);
                if (handle == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowEx failed.");

                ready.Set();
                while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                {
                    _ = TranslateMessage(ref message);
                    _ = DispatchMessage(ref message);
                }
            }
            catch (Exception ex)
            {
                startupFailure = ex;
                ready.Set();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public NativePoint Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public NativeRect CaretBounds;
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(
        IntPtr parent,
        EnumWindowsCallback callback,
        IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    private static IntPtr GetWindowLongPtr(IntPtr window, int index)
        => IntPtr.Size == 8
            ? GetWindowLongPtr64(window, index)
            : new IntPtr(GetWindowLong32(window, index));

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int MapWindowPoints(
        IntPtr sourceWindow,
        IntPtr destinationWindow,
        ref NativeRect points,
        uint pointCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo information);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage message, IntPtr window, uint minimum, uint maximum);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeoutMilliseconds,
        out IntPtr result);
}
