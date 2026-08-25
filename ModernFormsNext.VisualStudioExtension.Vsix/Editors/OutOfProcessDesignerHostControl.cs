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
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);

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
        LaunchOwnedHost();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ResizeChildWindow();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ResizeChildWindow();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        FocusChildWindow();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        FocusChildWindow();
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
            ResizeChildWindow();
            return;
        }

        if (DateTime.UtcNow >= startupDeadlineUtc)
        {
            readinessTimer.Stop();
            ShowFailure(
                $"Designer host did not attach to the Visual Studio pane within {StartupTimeout.TotalSeconds:0} seconds.{GetLogHint()}");
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
        ShowFailure($"Designer host exited unexpectedly{exitCode}.{GetLogHint()}");
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

    private void ResizeChildWindow()
    {
        if (childWindowHandle == IntPtr.Zero)
            return;

        _ = SetWindowPos(
            childWindowHandle,
            IntPtr.Zero,
            0,
            0,
            Math.Max(1, ClientSize.Width),
            Math.Max(1, ClientSize.Height),
            SwpNoZOrder | SwpNoActivate | SwpShowWindow);
    }

    private void FocusChildWindow()
    {
        if (childWindowHandle != IntPtr.Zero)
            _ = SetFocus(childWindowHandle);
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

    private static string GetLogHint()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "ModernFormsNextDesignerHost.log");
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

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
