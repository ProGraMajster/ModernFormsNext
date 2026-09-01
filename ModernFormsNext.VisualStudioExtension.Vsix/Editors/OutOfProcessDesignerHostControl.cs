using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ModernFormsNext.VisualStudioExtension.Commands;
using Timer = System.Windows.Forms.Timer;

namespace ModernFormsNext.VisualStudioExtension.Editors;

internal sealed class OutOfProcessDesignerHostControl : UserControl, IDesignerDocumentHost
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LifecycleCommandTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DirtyQueryTimeout = TimeSpan.FromMilliseconds(100);

    private readonly Label statusLabel;
    private readonly Timer readinessTimer;
    private readonly Timer documentStateTimer;
    private readonly string pipeName;
    private readonly object ipcGate = new();
    private readonly DesignerHostingMode hostingMode;
    private string documentPath;
    private string? projectPath;
    private Process? ownedProcess;
    private IntPtr childWindowHandle;
    private DateTime startupDeadlineUtc;
    private bool disposing;
    private bool hasPublishedDirtyState;
    private bool publishedDirtyState;
    private int dirtyQueryInFlight;
    private int focusCommandInFlight;
    private int saveInFlight;
    private int documentStateGeneration;

    public event EventHandler<DesignerDocumentDirtyChangedEventArgs>? DocumentDirtyChanged;

    public IWin32Window Window => this;

    public OutOfProcessDesignerHostControl(string documentPath)
        : this(documentPath, DesignerHostingMode.Integrated)
    {
    }

    public OutOfProcessDesignerHostControl(
        string documentPath,
        DesignerHostingMode hostingMode)
    {
        if (!Enum.IsDefined(typeof(DesignerHostingMode), hostingMode))
            throw new ArgumentOutOfRangeException(nameof(hostingMode));

        this.documentPath = Path.GetFullPath(documentPath);
        this.hostingMode = hostingMode;
        projectPath = FindNearestProjectPath(this.documentPath);
        pipeName = DesignerHostIpcClient.GetPipeName(
            $"{Process.GetCurrentProcess().Id}:{Guid.NewGuid():N}:{this.documentPath}");

        Dock = DockStyle.Fill;
        BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
        statusLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = System.Drawing.Color.Gainsboro,
            BackColor = BackColor,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Text = GetStartingStatusText(hostingMode)
        };
        statusLabel.Click += (_, _) => FocusHostedWindow();
        Controls.Add(statusLabel);

        readinessTimer = new Timer { Interval = 50 };
        readinessTimer.Tick += HandleReadinessTick;
        documentStateTimer = new Timer { Interval = 250 };
        documentStateTimer.Tick += HandleDocumentStateTick;
    }

    public bool TryOpenDocument(string path)
    {
        var candidateDocumentPath = Path.GetFullPath(path);
        var candidateProjectPath = FindNearestProjectPath(candidateDocumentPath);

        if (!IsHandleCreated)
        {
            documentPath = candidateDocumentPath;
            projectPath = candidateProjectPath;
            return true;
        }

        if (ownedProcess is null || HasExited(ownedProcess))
        {
            documentPath = candidateDocumentPath;
            projectPath = candidateProjectPath;
            LaunchOwnedHost();
            return ownedProcess is not null;
        }

        bool opened;
        lock (ipcGate)
        {
            opened = DesignerHostIpcClient.TrySendCommand(
                pipeName,
                "OPEN",
                candidateDocumentPath,
                candidateProjectPath,
                TimeSpan.FromSeconds(2));
        }
        if (!opened)
        {
            return false;
        }

        // Do not move the pane's identity until the live host confirms the reload/rename. If
        // the request fails, Visual Studio must continue to save and query the original document.
        documentPath = candidateDocumentPath;
        projectPath = candidateProjectPath;
        Interlocked.Increment(ref documentStateGeneration);
        QueueDocumentDirtyStateRefresh();
        return true;
    }

    public DesignerHostSaveResult SaveDocument()
    {
        if (ownedProcess is null || HasExited(ownedProcess))
            return DesignerHostSaveResult.Failed("The Designer host process is not running.");
        if (Interlocked.CompareExchange(ref saveInFlight, 1, 0) != 0)
        {
            DesignerEditorDiagnosticLog.Write(
                "SAVE_SINGLEFLIGHT_STATE Phase=HostControlCoalesced Active=True");
            return DesignerHostSaveResult.Canceled(
                "A Designer save is already active; the duplicate request was coalesced.");
        }

        documentStateTimer.Stop();
        Interlocked.Increment(ref documentStateGeneration);
        DesignerEditorDiagnosticLog.Write(
            $"SAVE_SINGLEFLIGHT_STATE Phase=HostControlBegin Active=True " +
            $"ManagedThreadId={Environment.CurrentManagedThreadId}");
        try
        {
            DesignerHostSaveResult result;
            DesignerEditorDiagnosticLog.Write(
                $"IPC_SAVE_GATE_WAIT_BEGIN ManagedThreadId={Environment.CurrentManagedThreadId}");
            lock (ipcGate)
            {
                DesignerEditorDiagnosticLog.Write(
                    $"IPC_SAVE_GATE_ACQUIRED ManagedThreadId={Environment.CurrentManagedThreadId}");
                result = DesignerHostIpcClient.SaveDocument(
                    pipeName,
                    documentPath,
                    projectPath,
                    TimeSpan.FromSeconds(10));
            }

            if (result.Outcome == DesignerHostSaveOutcome.Saved)
                PublishDocumentDirtyState(isDirty: false);
            return result;
        }
        finally
        {
            Interlocked.Exchange(ref saveInFlight, 0);
            DesignerEditorDiagnosticLog.Write(
                "SAVE_SINGLEFLIGHT_STATE Phase=HostControlEnd Active=False");
            if (!disposing && childWindowHandle != IntPtr.Zero)
                documentStateTimer.Start();
        }
    }

    public bool TryDiscardDocumentRecovery()
    {
        if (ownedProcess is null || HasExited(ownedProcess))
            return false;

        lock (ipcGate)
        {
            return DesignerHostIpcClient.TrySendCommand(
                pipeName,
                "DISCARD",
                documentPath,
                projectPath,
                TimeSpan.FromSeconds(10));
        }
    }

    public void PostToOwnerThread(Action action)
    {
        if (action is null)
            throw new ArgumentNullException(nameof(action));
        if (disposing || IsDisposed)
            return;

        BeginInvoke(action);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (TryReattachOwnedHost())
            return;

        LaunchOwnedHost();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        readinessTimer.Stop();
        documentStateTimer.Stop();
        if (!disposing && hostingMode == DesignerHostingMode.Integrated)
        {
            // Park the child before WinForms destroys this pane HWND. Otherwise Windows destroys
            // the child along with its parent and turns an ordinary docking/DPI handle recreation
            // into Designer process loss and recovery-state churn.
            if (!TrySendLifecycleCommand("PARK"))
                StopOwnedHost();
        }

        base.OnHandleDestroyed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ResizeHostedWindow();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ResizeHostedWindow();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        SynchronizeHostVisibility();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        FocusHostedWindow();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        FocusHostedWindow();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !this.disposing)
        {
            this.disposing = true;
            readinessTimer.Stop();
            readinessTimer.Dispose();
            documentStateTimer.Stop();
            documentStateTimer.Dispose();
            StopOwnedHost();
            DocumentDirtyChanged = null;
        }

        base.Dispose(disposing);
    }

    private void LaunchOwnedHost()
    {
        if (!IsHandleCreated || disposing)
            return;

        if (ownedProcess is not null)
        {
            if (!HasExited(ownedProcess))
                return;

            // A failed host remains useful for its exit diagnostic until a retry starts. Retire
            // that exact Process object before publishing the replacement so its late Exited
            // callback cannot mark the new session as failed.
            ownedProcess.Exited -= HandleOwnedProcessExited;
            ownedProcess.Dispose();
            ownedProcess = null;
        }

        childWindowHandle = IntPtr.Zero;
        documentStateTimer.Stop();
        statusLabel.Visible = true;
        statusLabel.Text = GetStartingStatusText(hostingMode);

        var packageDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
        var hostPath = packageDirectory is null
            ? null
            : Path.Combine(packageDirectory, "DesignerHost", "ModernFormsNext.VisualStudioDesignerHost.exe");

        if (hostPath is null || !File.Exists(hostPath))
        {
            ShowFailure($"Designer host executable was not found.{Environment.NewLine}{hostPath}");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = hostPath,
            Arguments = BuildHostArguments(
                documentPath,
                projectPath,
                pipeName,
                hostingMode,
                hostingMode == DesignerHostingMode.Integrated ? Handle : IntPtr.Zero,
                Process.GetCurrentProcess().Id),
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(documentPath) ?? packageDirectory
        };

        try
        {
            var process = Process.Start(startInfo);
            if (process is null)
            {
                ShowFailure("Designer host process could not be created.");
                return;
            }

            ownedProcess = process;
            process.EnableRaisingEvents = true;
            process.Exited += HandleOwnedProcessExited;
            startupDeadlineUtc = DateTime.UtcNow + StartupTimeout;
            readinessTimer.Start();
        }
        catch (Exception ex)
        {
            ShowFailure($"Designer host could not be started.{Environment.NewLine}{ex.Message}");
        }
    }

    private void HandleReadinessTick(object sender, EventArgs e)
    {
        if (ownedProcess is null || HasExited(ownedProcess))
        {
            readinessTimer.Stop();
            ShowExitedProcessFailure();
            return;
        }

        childWindowHandle = hostingMode == DesignerHostingMode.Integrated
            ? FindChildWindowOwnedByProcess(Handle, ownedProcess.Id)
            : FindTopLevelWindowOwnedByProcess(ownedProcess.Id);
        if (childWindowHandle != IntPtr.Zero)
        {
            readinessTimer.Stop();
            statusLabel.Text = hostingMode == DesignerHostingMode.Standalone
                ? "The ModernFormsNext Designer is open in a separate window."
                : string.Empty;
            statusLabel.Visible = hostingMode == DesignerHostingMode.Standalone;
            ResizeHostedWindow();
            SynchronizeHostVisibility();
            QueueDocumentDirtyStateRefresh();
            documentStateTimer.Start();
            return;
        }

        if (DateTime.UtcNow >= startupDeadlineUtc)
        {
            readinessTimer.Stop();
            var logHint = GetLogHint(ownedProcess);
            StopOwnedHost();
            ShowFailure(
                hostingMode == DesignerHostingMode.Integrated
                    ? $"Designer host did not attach to the Visual Studio pane within {StartupTimeout.TotalSeconds:0} seconds.{logHint}"
                    : $"Designer host did not create a standalone window within {StartupTimeout.TotalSeconds:0} seconds.{logHint}");
        }
    }

    private void HandleOwnedProcessExited(object sender, EventArgs e)
    {
        if (disposing
            || !IsHandleCreated
            || sender is not Process exitedProcess
            || !ReferenceEquals(exitedProcess, ownedProcess))
            return;

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (ReferenceEquals(exitedProcess, ownedProcess))
                    ShowExitedProcessFailure();
            }));
        }
        catch (InvalidOperationException)
        {
            // The pane is already being destroyed.
        }
    }

    private void ShowExitedProcessFailure()
    {
        readinessTimer.Stop();
        documentStateTimer.Stop();
        childWindowHandle = IntPtr.Zero;

        if (disposing)
            return;

        if (hostingMode == DesignerHostingMode.Standalone && HasSuccessfulExit(ownedProcess))
        {
            // A zero exit after the standalone window's close confirmation means Save, Don't
            // Save, or a clean close was resolved inside the canonical Designer close flow.
            // Cancel never reaches this branch because it leaves the window and process alive.
            PublishDocumentDirtyState(isDirty: false);
            DesignerEditorDiagnosticLog.Write(
                $"STANDALONE_HOST_CLOSED ProcessId={TryGetProcessId(ownedProcess!)} ExitCode=0");
            ShowFailure(
                "The standalone ModernFormsNext Designer window was closed. " +
                "Activate this document to open it again.");
            return;
        }

        PublishDocumentDirtyState(isDirty: true);

        var exitCode = TryGetExitCode(ownedProcess);
        ShowFailure($"Designer host exited unexpectedly{exitCode}.{GetLogHint(ownedProcess)}");
    }

    private void StopOwnedHost()
    {
        var process = ownedProcess;
        ownedProcess = null;
        childWindowHandle = IntPtr.Zero;
        documentStateTimer.Stop();

        if (process is null)
            return;

        DesignerEditorDiagnosticLog.Write($"HOST_SHUTDOWN_BEGIN ProcessId={TryGetProcessId(process)}");
        process.Exited -= HandleOwnedProcessExited;
        try
        {
            if (!process.HasExited)
            {
                lock (ipcGate)
                {
                    _ = DesignerHostIpcClient.TrySendCommand(
                        pipeName,
                        "SHUTDOWN",
                        documentPath,
                        projectPath,
                        TimeSpan.FromSeconds(1));
                }

                if (!process.WaitForExit(1500) && !process.HasExited)
                {
                    process.Kill();
                    _ = process.WaitForExit(1000);
                }
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        finally
        {
            DesignerEditorDiagnosticLog.Write(
                $"HOST_SHUTDOWN_END ProcessId={TryGetProcessId(process)} ExitCode={TryGetExitCode(process)} HResult=0x00000000");
            process.Dispose();
        }
    }

    private bool TryReattachOwnedHost()
    {
        if (ownedProcess is null
            || HasExited(ownedProcess)
            || childWindowHandle == IntPtr.Zero)
        {
            return false;
        }

        if (hostingMode == DesignerHostingMode.Standalone)
        {
            statusLabel.Text = "The ModernFormsNext Designer is open in a separate window.";
            statusLabel.Visible = true;
            QueueDocumentDirtyStateRefresh();
            documentStateTimer.Start();
            return true;
        }

        var attached = TrySendLifecycleCommand(
            "ATTACH",
            Handle.ToInt64().ToString(CultureInfo.InvariantCulture));
        if (!attached)
        {
            StopOwnedHost();
            return false;
        }

        statusLabel.Visible = false;
        ResizeHostedWindow();
        SynchronizeHostVisibility();
        QueueDocumentDirtyStateRefresh();
        documentStateTimer.Start();
        return true;
    }

    private void HandleDocumentStateTick(object sender, EventArgs e)
        => QueueDocumentDirtyStateRefresh();

    private void QueueDocumentDirtyStateRefresh()
    {
        if (disposing
            || ownedProcess is null
            || HasExited(ownedProcess)
            || childWindowHandle == IntPtr.Zero
            || Interlocked.CompareExchange(ref dirtyQueryInFlight, 1, 0) != 0)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            var generation = Volatile.Read(ref documentStateGeneration);
            var queryDocumentPath = documentPath;
            var queryProjectPath = projectPath;
            var succeeded = false;
            var isDirty = false;
            try
            {
                DesignerEditorDiagnosticLog.Write(
                    $"DIRTY_POLL_GATE_WAIT Generation={generation} " +
                    $"ManagedThreadId={Environment.CurrentManagedThreadId}");
                lock (ipcGate)
                {
                    DesignerEditorDiagnosticLog.Write(
                        $"DIRTY_POLL_GATE_ACQUIRED Generation={generation} " +
                        $"ManagedThreadId={Environment.CurrentManagedThreadId}");
                    succeeded = DesignerHostIpcClient.TryGetDocumentDirty(
                        pipeName,
                        queryDocumentPath,
                        queryProjectPath,
                        DirtyQueryTimeout,
                        out isDirty);
                }
            }
            catch (Exception exception)
            {
                DesignerEditorDiagnosticLog.WriteException("DIRTY_QUERY_EXCEPTION", exception);
            }
            finally
            {
                Interlocked.Exchange(ref dirtyQueryInFlight, 0);
            }

            if (!succeeded || generation != Volatile.Read(ref documentStateGeneration))
                return;

            try
            {
                PostToOwnerThread(() =>
                {
                    if (generation == Volatile.Read(ref documentStateGeneration))
                        PublishDocumentDirtyState(isDirty);
                });
            }
            catch (InvalidOperationException)
            {
                // Handle teardown raced the background query.
            }
        });
    }

    private void PublishDocumentDirtyState(bool isDirty)
    {
        if (hasPublishedDirtyState && publishedDirtyState == isDirty)
            return;

        hasPublishedDirtyState = true;
        publishedDirtyState = isDirty;
        DocumentDirtyChanged?.Invoke(this, new DesignerDocumentDirtyChangedEventArgs(isDirty));
    }

    private void ResizeHostedWindow()
    {
        if (hostingMode == DesignerHostingMode.Integrated)
            _ = TrySendLifecycleCommand("RESIZE");
    }

    private void SynchronizeHostVisibility()
    {
        if (hostingMode == DesignerHostingMode.Integrated)
            _ = TrySendLifecycleCommand(Visible ? "SHOW" : "HIDE");
    }

    private void FocusHostedWindow()
    {
        if (hostingMode == DesignerHostingMode.Standalone)
        {
            if (ownedProcess is null || HasExited(ownedProcess))
            {
                LaunchOwnedHost();
                return;
            }

            ActivateStandaloneWindow();
            return;
        }

        if (disposing || Interlocked.CompareExchange(ref focusCommandInFlight, 1, 0) != 0)
            return;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                _ = TrySendLifecycleCommand("FOCUS");
            }
            finally
            {
                Interlocked.Exchange(ref focusCommandInFlight, 0);
            }
        });
    }

    private void ActivateStandaloneWindow()
    {
        var windowHandle = childWindowHandle;
        if (windowHandle == IntPtr.Zero)
            return;

        if (IsIconic(windowHandle))
            _ = ShowWindow(windowHandle, ShowWindowRestore);

        _ = SetForegroundWindow(windowHandle);
    }

    private bool TrySendLifecycleCommand(string command, string? payload = null)
    {
        if (ownedProcess is null
            || HasExited(ownedProcess)
            || childWindowHandle == IntPtr.Zero)
        {
            return false;
        }

        DesignerEditorDiagnosticLog.Write(
            $"IPC_LIFECYCLE_GATE_WAIT Command={command} " +
            $"ManagedThreadId={Environment.CurrentManagedThreadId}");
        lock (ipcGate)
        {
            DesignerEditorDiagnosticLog.Write(
                $"IPC_LIFECYCLE_GATE_ACQUIRED Command={command} " +
                $"ManagedThreadId={Environment.CurrentManagedThreadId}");
            return DesignerHostIpcClient.TrySendLifecycleCommand(
                pipeName,
                command,
                payload,
                LifecycleCommandTimeout);
        }
    }

    private void ShowFailure(string message)
    {
        statusLabel.Text = message;
        statusLabel.Visible = true;
        statusLabel.BringToFront();
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static string TryGetExitCode(Process? process)
    {
        try
        {
            return process is not null && process.HasExited
                ? $" with code {process.ExitCode}"
                : string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static bool HasSuccessfulExit(Process? process)
    {
        try
        {
            return process is not null && process.HasExited && process.ExitCode == 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string TryGetProcessId(Process process)
    {
        try
        {
            return process.Id.ToString(CultureInfo.InvariantCulture);
        }
        catch (InvalidOperationException)
        {
            return "<unknown>";
        }
    }

    private static string GetLogHint(Process? process)
    {
        int processId;
        try
        {
            if (process is null)
                return string.Empty;
            processId = process.Id;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }

        var logPath = Path.Combine(Path.GetTempPath(), $"ModernFormsNextDesignerHost-{processId}.log");
        return File.Exists(logPath)
            ? $"{Environment.NewLine}{Environment.NewLine}Log: {logPath}"
            : string.Empty;
    }

    internal static string BuildHostArguments(
        string designFilePath,
        string? projectFilePath,
        string endpointName,
        DesignerHostingMode hostingMode,
        IntPtr parentHandle,
        int ownerProcessId)
    {
        var builder = new StringBuilder();
        AppendArgument(builder, "--design-file", designFilePath);
        if (!string.IsNullOrWhiteSpace(projectFilePath))
            AppendArgument(builder, "--project", projectFilePath!);
        AppendArgument(builder, "--pipe", endpointName);
        AppendArgument(
            builder,
            "--host-mode",
            hostingMode == DesignerHostingMode.Integrated ? "integrated" : "standalone");
        AppendArgument(builder, "--owner-process", ownerProcessId.ToString(CultureInfo.InvariantCulture));
        if (hostingMode == DesignerHostingMode.Integrated)
            AppendArgument(builder, "--parent-window", parentHandle.ToInt64().ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static string GetStartingStatusText(DesignerHostingMode mode)
        => mode == DesignerHostingMode.Standalone
            ? "Starting ModernFormsNext Designer in a separate window..."
            : "Starting ModernFormsNext Designer...";

    private static void AppendArgument(StringBuilder builder, string name, string value)
    {
        if (builder.Length > 0)
            builder.Append(' ');
        builder.Append(name);
        builder.Append(' ');
        builder.Append(QuoteProcessArgument(value));
    }

    private static string QuoteProcessArgument(string value)
    {
        if (value.Length > 0 && value.All(character => !char.IsWhiteSpace(character) && character != '"'))
            return value;

        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        var slashCount = 0;

        foreach (var character in value)
        {
            if (character == '\\')
            {
                slashCount++;
                continue;
            }

            if (character == '"')
                builder.Append('\\', slashCount * 2 + 1);
            else
                builder.Append('\\', slashCount);

            slashCount = 0;
            builder.Append(character);
        }

        builder.Append('\\', slashCount * 2);
        builder.Append('"');
        return builder.ToString();
    }

    private static string? FindNearestProjectPath(string path)
    {
        var directory = File.Exists(path) ? Path.GetDirectoryName(path) : null;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var project = Directory.EnumerateFiles(directory, "*.csproj").FirstOrDefault();
            if (project is not null)
                return project;
            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private static IntPtr FindChildWindowOwnedByProcess(IntPtr parentHandle, int processId)
    {
        var result = IntPtr.Zero;
        EnumChildWindows(parentHandle, (windowHandle, _) =>
        {
            GetWindowThreadProcessId(windowHandle, out var ownerProcessId);
            if (ownerProcessId != (uint)processId)
                return true;

            result = windowHandle;
            return false;
        }, IntPtr.Zero);
        return result;
    }

    private static IntPtr FindTopLevelWindowOwnedByProcess(int processId)
    {
        var result = IntPtr.Zero;
        EnumWindows((windowHandle, _) =>
        {
            GetWindowThreadProcessId(windowHandle, out var ownerProcessId);
            if (ownerProcessId != (uint)processId || !IsWindowVisible(windowHandle))
                return true;

            result = windowHandle;
            return false;
        }, IntPtr.Zero);
        return result;
    }

    private delegate bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsCallback callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private const int ShowWindowRestore = 9;

}
