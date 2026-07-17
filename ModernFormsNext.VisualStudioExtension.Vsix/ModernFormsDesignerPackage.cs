using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ModernFormsNext.VisualStudioExtension.Commands;
using ModernFormsNext.VisualStudioExtension.Detection;

namespace ModernFormsNext.VisualStudioExtension;

/// <summary>
/// Registers the ModernFormsNext designer VSIX package with Visual Studio.
/// </summary>
/// <remarks>
/// This bootstrap package is intentionally small and loadable by the classic in-process VSSDK.
/// The reusable ModernFormsNext designer UI remains in <c>ModernFormsNext.Designer</c>; this package
/// only exposes Visual Studio registration and validation commands needed to deploy the extension.
/// </remarks>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("ModernFormsNext Designer", "Visual Studio designer support for ModernFormsNext.", "1.8.0")]
[ProvideMenuResource("ModernFormsNext.VisualStudioExtension.CTMENU", 1)]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
[Guid(PackageGuidString)]
public sealed class ModernFormsDesignerPackage : AsyncPackage
{
    /// <summary>
    /// Gets the package GUID used by the VSIX manifest and pkgdef registration.
    /// </summary>
    public const string PackageGuidString = "0E6A57C2-A0C1-456E-BFE1-5C8F6EB0AA11";

    /// <summary>
    /// Gets the command set GUID used by <c>ModernFormsNext.VisualStudioExtension.vsct</c>.
    /// </summary>
    public const string CommandSetGuidString = "CDA8870E-234B-44C9-BA43-362BFF40A0E3";

    internal static readonly Guid CommandSetGuid = new(CommandSetGuidString);

    private readonly ModernFormsDesignableFileDetector detector = new();
    private readonly Dictionary<string, Process> designerHostProcesses = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        await ViewModernFormsNextDesignerCommand.InitializeAsync(this);
    }

    internal ModernFormsDesignableFileInfo? GetSelectedDesignableFile()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var path = GetSelectedFilePath();
        return path is null ? null : detector.Inspect(path);
    }

    internal async Task OpenDesignerForCodeFileAsync(ModernFormsDesignableFileInfo fileInfo)
    {
        if (fileInfo is null)
            throw new ArgumentNullException(nameof(fileInfo));

        await JoinableTaskFactory.SwitchToMainThreadAsync();

        EnsureDesignDocument(fileInfo);
        LaunchDesignerHost(fileInfo.DesignFilePath);
    }

    private string? GetSelectedFilePath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var dteFileName = TryGetSelectedDteFileName();

        if (dteFileName is not null)
            return dteFileName;

        if (GetService(typeof(SVsShellMonitorSelection)) is not IVsMonitorSelection monitorSelection)
            return TryGetActiveDocumentFileName();

        ErrorHandler.ThrowOnFailure(monitorSelection.GetCurrentSelection(
            out var hierarchyPointer,
            out var itemId,
            out _,
            out var selectionContainerPointer));

        try
        {
            if (hierarchyPointer == IntPtr.Zero
                || itemId == VSConstants.VSITEMID_NIL)
            {
                return TryGetActiveDocumentFileName();
            }

            var hierarchy = Marshal.GetObjectForIUnknown(hierarchyPointer) as IVsHierarchy;

            if (hierarchy is null)
                return TryGetActiveDocumentFileName();

            return TryGetProjectItemFileName(hierarchy, itemId)
                ?? TryGetCanonicalFileName(hierarchy, itemId)
                ?? TryGetActiveDocumentFileName();
        }
        finally
        {
            if (hierarchyPointer != IntPtr.Zero)
                Marshal.Release(hierarchyPointer);

            if (selectionContainerPointer != IntPtr.Zero)
                Marshal.Release(selectionContainerPointer);
        }
    }

    private string? TryGetActiveDocumentFileName()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (GetService(typeof(EnvDTE.DTE)) is not EnvDTE.DTE dte)
            return null;

        try
        {
            return NormalizeExistingFilePath(dte.ActiveDocument?.FullName);
        }
        catch
        {
            return null;
        }
    }

    private string? TryGetSelectedDteFileName()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (GetService(typeof(EnvDTE.DTE)) is not EnvDTE.DTE dte
            || dte.SelectedItems.Count == 0)
        {
            return null;
        }

        try
        {
            var selectedItem = dte.SelectedItems.Item(1);

            return selectedItem?.ProjectItem is null
                ? null
                : TryGetProjectItemFileName(selectedItem.ProjectItem);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetProjectItemFileName(IVsHierarchy hierarchy, uint itemId)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (ErrorHandler.Failed(hierarchy.GetProperty(
                itemId,
                (int)__VSHPROPID.VSHPROPID_ExtObject,
                out var extObject))
            || extObject is not EnvDTE.ProjectItem projectItem)
        {
            return null;
        }

        return TryGetProjectItemFileName(projectItem);
    }

    private static string? TryGetProjectItemFileName(EnvDTE.ProjectItem projectItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            for (short index = 1; index <= projectItem.FileCount; index++)
            {
                var fileName = NormalizeExistingFilePath(projectItem.FileNames[index]);

                if (fileName is not null)
                    return fileName;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? TryGetCanonicalFileName(IVsHierarchy hierarchy, uint itemId)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (ErrorHandler.Failed(hierarchy.GetCanonicalName(itemId, out var canonicalName))
            || string.IsNullOrWhiteSpace(canonicalName))
        {
            return null;
        }

        var normalizedCanonicalName = NormalizeExistingFilePath(canonicalName);

        if (normalizedCanonicalName is not null)
            return normalizedCanonicalName;

        var projectDirectory = TryGetProjectDirectory(hierarchy);

        if (projectDirectory is null)
            return null;

        try
        {
            var candidate = NormalizeExistingFilePath(Path.GetFullPath(Path.Combine(projectDirectory, canonicalName)));

            return candidate;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetProjectDirectory(IVsHierarchy hierarchy)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (ErrorHandler.Succeeded(hierarchy.GetProperty(
                VSConstants.VSITEMID_ROOT,
                (int)__VSHPROPID.VSHPROPID_ProjectDir,
                out var projectDirectoryObject))
            && projectDirectoryObject is string projectDirectory
            && Directory.Exists(projectDirectory))
        {
            return projectDirectory;
        }

        if (ErrorHandler.Failed(hierarchy.GetCanonicalName(VSConstants.VSITEMID_ROOT, out var rootName))
            || string.IsNullOrWhiteSpace(rootName))
        {
            return null;
        }

        if (Directory.Exists(rootName))
            return rootName;

        return File.Exists(rootName)
            ? Path.GetDirectoryName(rootName)
            : null;
    }

    private static string? NormalizeExistingFilePath(string? path)
    {
        var candidate = path ?? string.Empty;

        if (candidate.Trim().Length == 0)
            return null;

        if (Path.IsPathRooted(candidate) && File.Exists(candidate))
            return candidate;

        var sourceExtensionIndex = candidate.IndexOf(".cs", StringComparison.OrdinalIgnoreCase);

        if (sourceExtensionIndex < 0)
            return null;

        var sourcePath = candidate.Substring(0, sourceExtensionIndex + 3);

        return Path.IsPathRooted(sourcePath) && File.Exists(sourcePath)
            ? sourcePath
            : null;
    }

    private static void EnsureDesignDocument(ModernFormsDesignableFileInfo fileInfo)
    {
        if (File.Exists(fileInfo.DesignFilePath))
            return;

        var className = string.IsNullOrWhiteSpace(fileInfo.ClassName)
            ? Path.GetFileNameWithoutExtension(fileInfo.CodeFilePath)
            : fileInfo.ClassName;

        var json = string.Join(
            Environment.NewLine,
            "{",
            "  \"metadata\": {",
            "    \"toolName\": \"ModernFormsNext Designer\"",
            "  },",
            $"  \"namespace\": \"\",",
            $"  \"className\": \"{EscapeJson(className)}\",",
            $"  \"formName\": \"{EscapeJson(className)}\",",
            "  \"size\": {",
            "    \"width\": 900,",
            "    \"height\": 600",
            "  },",
            "  \"controls\": []",
            "}");

        File.WriteAllText(fileInfo.DesignFilePath, json);
    }

    private void LaunchDesignerHost(string designFilePath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var projectPath = FindNearestProjectPath(designFilePath);
        var hostKey = projectPath ?? Path.GetDirectoryName(designFilePath) ?? designFilePath;
        var pipeName = DesignerHostIpcClient.GetPipeName(hostKey);

        if (designerHostProcesses.TryGetValue(hostKey, out var existingHost)
            && !existingHost.HasExited)
        {
            if (!DesignerHostIpcClient.TryOpenDocument(pipeName, designFilePath, projectPath, TimeSpan.FromSeconds(2)))
            {
                VsShellUtilities.ShowMessageBox(
                    this,
                    "ModernFormsNext Designer is already running, but it did not accept the open-document command. Try closing the designer host window and opening the designer again.",
                    "ModernFormsNext Designer",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }

            ActivateExistingHost(existingHost);
            return;
        }

        var packageDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
        var hostPath = packageDirectory is null
            ? null
            : Path.Combine(packageDirectory, "DesignerHost", "ModernFormsNext.VisualStudioDesignerHost.exe");

        if (hostPath is null || !File.Exists(hostPath))
        {
            VsShellUtilities.ShowMessageBox(
                this,
                $"ModernFormsNext Designer host was not found.{Environment.NewLine}{hostPath}",
                "ModernFormsNext Designer",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = hostPath,
            Arguments = BuildHostArguments(designFilePath, projectPath, pipeName),
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(designFilePath) ?? packageDirectory ?? Environment.CurrentDirectory
        };

        try
        {
            var process = Process.Start(startInfo);

            if (process is not null)
            {
                designerHostProcesses[hostKey] = process;
                process.EnableRaisingEvents = true;
                process.Exited += (_, _) => designerHostProcesses.Remove(hostKey);
            }

            if (process is not null && process.WaitForExit(1500))
            {
                var logPath = Path.Combine(Path.GetTempPath(), "ModernFormsNextDesignerHost.log");
                var logHint = File.Exists(logPath)
                    ? $"{Environment.NewLine}{Environment.NewLine}Log: {logPath}"
                    : string.Empty;

                VsShellUtilities.ShowMessageBox(
                    this,
                    $"ModernFormsNext Designer host exited immediately with code {process.ExitCode}.{logHint}",
                    "ModernFormsNext Designer",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
        catch (Exception ex)
        {
            VsShellUtilities.ShowMessageBox(
                this,
                $"ModernFormsNext Designer host could not be started.{Environment.NewLine}{ex.Message}",
                "ModernFormsNext Designer",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }

    private static string BuildHostArguments(string designFilePath, string? projectPath, string pipeName)
        => string.IsNullOrWhiteSpace(projectPath)
            ? $"--design-file {QuoteProcessArgument(designFilePath)} --pipe {QuoteProcessArgument(pipeName)}"
            : $"--design-file {QuoteProcessArgument(designFilePath)} --project {QuoteProcessArgument(projectPath!)} --pipe {QuoteProcessArgument(pipeName)}";

    private static string? FindNearestProjectPath(string path)
    {
        var directory = File.Exists(path)
            ? Path.GetDirectoryName(path)
            : Directory.Exists(path)
                ? path
                : null;

        while (!string.IsNullOrWhiteSpace(directory))
        {
            var project = Directory.EnumerateFiles(directory, "*.csproj").FirstOrDefault();

            if (project is not null)
                return project;

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private static void ActivateExistingHost(Process process)
    {
        try
        {
            process.Refresh();

            if (process.MainWindowHandle == IntPtr.Zero)
                return;

            ShowWindow(process.MainWindowHandle, 5);
            SetForegroundWindow(process.MainWindowHandle);
        }
        catch
        {
            // Activation is best-effort. The command should never fail just because Windows
            // refuses foreground focus for a still-running designer process.
        }
    }

    private static string EscapeJson(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string QuoteProcessArgument(string value)
        => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
