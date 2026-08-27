using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ModernFormsNext.VisualStudioExtension.Commands;

namespace ModernFormsNext.VisualStudioExtension.Editors;

internal sealed class OutOfProcessDesignerHostControl : UserControl
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LifecycleCommandTimeout = TimeSpan.FromMilliseconds(500);

    private readonly Label statusLabel;
    private readonly Timer readinessTimer;
    private readonly string pipeName;
    private string documentPath;
    private string? projectPath;
    private Process? ownedProcess;
    private IntPtr childWindowHandle;
    private DateTime startupDeadlineUtc;
    private bool disposing;

    public OutOfProcessDesignerHostControl(string documentPath)
    {
        this.documentPath = Path.GetFullPath(documentPath);
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
            Text = "Starting ModernFormsNext Designer..."
        };
        Controls.Add(statusLabel);

        readinessTimer = new Timer { Interval = 50 };
        readinessTimer.Tick += HandleReadinessTick;
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

        if (!DesignerHostIpcClient.TrySendCommand(
            pipeName,
            "OPEN",
            candidateDocumentPath,
            candidateProjectPath,
            TimeSpan.FromSeconds(2)))
        {
            return false;
        }

        // Do not move the pane's identity until the live host confirms the reload/rename. If
        // the request fails, Visual Studio must continue to save and query the original document.
        documentPath = candidateDocumentPath;
        projectPath = candidateProjectPath;
        return true;
    }

    public bool TrySaveDocument()
        => ownedProcess is not null
            && !HasExited(ownedProcess)
            && DesignerHostIpcClient.TrySendCommand(
                pipeName,
                "SAVE",
                documentPath,
                projectPath,
                TimeSpan.FromSeconds(2));

    public bool TryGetDocumentDirty(out bool isDirty)
    {
        isDirty = false;
        if (ownedProcess is null || HasExited(ownedProcess))
            return true;
        if (readinessTimer.Enabled && childWindowHandle == IntPtr.Zero)
            return true;

        return DesignerHostIpcClient.TryGetDocumentDirty(
            pipeName,
            documentPath,
            projectPath,
            TimeSpan.FromMilliseconds(500),
            out isDirty);
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
        if (!disposing)
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
            StopOwnedHost();
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
        statusLabel.Visible = true;
        statusLabel.Text = "Starting ModernFormsNext Designer...";

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
            Arguments = BuildHostArguments(documentPath, projectPath, pipeName, Handle),
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

        childWindowHandle = FindChildWindowOwnedByProcess(Handle, ownedProcess.Id);
        if (childWindowHandle != IntPtr.Zero)
        {
            readinessTimer.Stop();
            statusLabel.Visible = false;
            ResizeHostedWindow();
            SynchronizeHostVisibility();
            return;
        }

        if (DateTime.UtcNow >= startupDeadlineUtc)
        {
            readinessTimer.Stop();
            var logHint = GetLogHint(ownedProcess);
            StopOwnedHost();
            ShowFailure(
                $"Designer host did not attach to the Visual Studio pane within {StartupTimeout.TotalSeconds:0} seconds.{logHint}");
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
        childWindowHandle = IntPtr.Zero;

        if (disposing)
            return;

        var exitCode = TryGetExitCode(ownedProcess);
        ShowFailure($"Designer host exited unexpectedly{exitCode}.{GetLogHint(ownedProcess)}");
    }

    private void StopOwnedHost()
    {
        var process = ownedProcess;
        ownedProcess = null;
        childWindowHandle = IntPtr.Zero;

        if (process is null)
            return;

        process.Exited -= HandleOwnedProcessExited;
        try
        {
            if (!process.HasExited)
            {
                _ = DesignerHostIpcClient.TrySendCommand(
                    pipeName,
                    "SHUTDOWN",
                    documentPath,
                    projectPath,
                    TimeSpan.FromSeconds(1));

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
        return true;
    }

    private void ResizeHostedWindow()
        => _ = TrySendLifecycleCommand("RESIZE");

    private void SynchronizeHostVisibility()
        => _ = TrySendLifecycleCommand(Visible ? "SHOW" : "HIDE");

    private void FocusHostedWindow()
        => _ = TrySendLifecycleCommand("FOCUS");

    private bool TrySendLifecycleCommand(string command, string? payload = null)
    {
        if (ownedProcess is null
            || HasExited(ownedProcess)
            || childWindowHandle == IntPtr.Zero)
        {
            return false;
        }

        return DesignerHostIpcClient.TrySendLifecycleCommand(
            pipeName,
            command,
            payload,
            LifecycleCommandTimeout);
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

    private static string BuildHostArguments(
        string designFilePath,
        string? projectFilePath,
        string endpointName,
        IntPtr parentHandle)
    {
        var builder = new StringBuilder();
        AppendArgument(builder, "--design-file", designFilePath);
        if (!string.IsNullOrWhiteSpace(projectFilePath))
            AppendArgument(builder, "--project", projectFilePath!);
        AppendArgument(builder, "--pipe", endpointName);
        AppendArgument(builder, "--parent-window", parentHandle.ToInt64().ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

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

    private delegate bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsCallback callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

}
