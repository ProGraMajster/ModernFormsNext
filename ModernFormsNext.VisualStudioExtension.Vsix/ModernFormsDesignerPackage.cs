using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ModernFormsNext.VisualStudioExtension.Commands;
using ModernFormsNext.VisualStudioExtension.Detection;
using ModernFormsNext.VisualStudioExtension.Editors;
using ModernFormsNext.VisualStudioExtension.Options;

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
[InstalledProductRegistration("ModernFormsNext Designer", "Visual Studio designer support for ModernFormsNext.", "1.10.0")]
[ProvideMenuResource("ModernFormsNext.VisualStudioExtension.CTMENU", 1)]
[ProvideEditorFactory(typeof(MfDesignEditorFactory), 101)]
[ProvideEditorExtension(typeof(MfDesignEditorFactory), DesignFileExtension, 50, DefaultName = ExtensionDisplayName)]
[ProvideEditorLogicalView(typeof(MfDesignEditorFactory), LogicalViewID.Designer)]
[ProvideOptionPage(typeof(DesignerOptionsPage), "ModernFormsNext", "Designer", 0, 0, true)]
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

    /// <summary>
    /// Gets the editor factory GUID for ModernFormsNext design documents.
    /// </summary>
    public const string EditorFactoryGuidString = "C61567C8-F5AC-4F9E-9C6E-B4EC99C7AB31";

    /// <summary>
    /// Gets the extension handled by the ModernFormsNext metadata editor.
    /// </summary>
    public const string DesignFileExtension = ".mfdesign";

    /// <summary>
    /// Gets the display name used by the Designer editor.
    /// </summary>
    public const string ExtensionDisplayName = "ModernFormsNext Designer";

    internal static readonly Guid CommandSetGuid = new(CommandSetGuidString);
    internal static readonly Guid EditorFactoryGuid = new(EditorFactoryGuidString);

    private readonly ModernFormsDesignableFileDetector detector = new();
    private IVsRegisterPriorityCommandTarget? priorityCommandRegistrar;
    private DesignerSaveCommandTarget? designerSaveCommandTarget;
    private uint priorityCommandCookie = VSConstants.VSCOOKIE_NIL;

    /// <inheritdoc/>
    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        RegisterEditorFactory(new MfDesignEditorFactory(this));
        await ViewModernFormsNextDesignerCommand.InitializeAsync(this);

        priorityCommandRegistrar = await GetServiceAsync(typeof(SVsRegisterPriorityCommandTarget))
            as IVsRegisterPriorityCommandTarget;
        if (priorityCommandRegistrar is not null)
        {
            designerSaveCommandTarget = new DesignerSaveCommandTarget(GetActiveDesignerPane);
            ErrorHandler.ThrowOnFailure(priorityCommandRegistrar.RegisterPriorityCommandTarget(
                0,
                designerSaveCommandTarget,
                out priorityCommandCookie));
            DesignerEditorDiagnosticLog.Write(
                $"VS_CMD_SAVE_PRIORITY_TARGET_REGISTERED Cookie={priorityCommandCookie}");
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing
            && priorityCommandRegistrar is not null
            && priorityCommandCookie != VSConstants.VSCOOKIE_NIL)
        {
            JoinableTaskFactory.Run(async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                var result = priorityCommandRegistrar.UnregisterPriorityCommandTarget(priorityCommandCookie);
                DesignerEditorDiagnosticLog.Write(
                    $"VS_CMD_SAVE_PRIORITY_TARGET_UNREGISTERED Cookie={priorityCommandCookie} " +
                    $"HResult=0x{result:X8}");
                priorityCommandCookie = VSConstants.VSCOOKIE_NIL;
                priorityCommandRegistrar = null;
                designerSaveCommandTarget = null;
            });
        }

        base.Dispose(disposing);
    }

    private MfDesignEditorPane? GetActiveDesignerPane()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (GetService(typeof(SVsShellMonitorSelection)) is not IVsMonitorSelection monitorSelection
            || ErrorHandler.Failed(monitorSelection.GetCurrentElementValue(
                (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame,
                out var frameValue))
            || frameValue is not IVsWindowFrame frame
            || ErrorHandler.Failed(frame.GetProperty(
                (int)__VSFPROPID.VSFPROPID_DocView,
                out var docView)))
        {
            return null;
        }

        return docView as MfDesignEditorPane;
    }

    internal ModernFormsDesignableFileInfo? GetSelectedDesignableFile()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var path = GetSelectedFilePath();
        return path is null ? null : detector.Inspect(path);
    }

    internal DesignerHostingMode GetDesignerHostingMode()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return GetDialogPage(typeof(DesignerOptionsPage)) is DesignerOptionsPage options
            ? options.HostingMode
            : DesignerHostingMode.Integrated;
    }

    internal async Task OpenDesignerForCodeFileAsync(ModernFormsDesignableFileInfo fileInfo)
    {
        if (fileInfo is null)
            throw new ArgumentNullException(nameof(fileInfo));

        await JoinableTaskFactory.SwitchToMainThreadAsync();

        EnsureDesignDocument(fileInfo);
        if (VsShellUtilities.IsDocumentOpen(
                this,
                fileInfo.DesignFilePath,
                VSConstants.LOGVIEWID_Designer,
                out _,
                out _,
                out var existingFrame))
        {
            DesignerEditorDiagnosticLog.Write(
                $"PANE_ACTIVATE_EXISTING Moniker={fileInfo.DesignFilePath}");
            ErrorHandler.ThrowOnFailure(existingFrame.Show());
            return;
        }

        VsShellUtilities.OpenDocumentWithSpecificEditor(
            this,
            fileInfo.DesignFilePath,
            EditorFactoryGuid,
            VSConstants.LOGVIEWID_Designer,
            out _,
            out _,
            out var frame);

        ErrorHandler.ThrowOnFailure(frame.Show());
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
            $"  \"namespace\": \"{EscapeJson(fileInfo.NamespaceName ?? string.Empty)}\",",
            $"  \"className\": \"{EscapeJson(className)}\",",
            fileInfo.IsUserControl ? "  \"rootKind\": \"userControl\"," : string.Empty,
            $"  \"formName\": \"{EscapeJson(className)}\",",
            "  \"size\": {",
            "    \"width\": 900,",
            "    \"height\": 600",
            "  },",
            "  \"controls\": []",
            "}");

        WriteNewFileAtomically(fileInfo.DesignFilePath, json);
    }

    private static void WriteNewFileAtomically(string destinationPath, string content)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath))
            ?? throw new InvalidOperationException("The Designer document path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".mfn-designer-{Path.GetFileName(destinationPath)}-{Guid.NewGuid():N}.tmp");

        try
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.SequentialScan | FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            // This method only publishes a missing sidecar. Move makes the complete temporary
            // file visible in one operation and refuses to overwrite a concurrently created file.
            File.Move(temporaryPath, destinationPath);
            temporaryPath = string.Empty;
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static string EscapeJson(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
