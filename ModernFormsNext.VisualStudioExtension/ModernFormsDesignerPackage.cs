using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designing;
using ModernFormsNext.VisualStudioExtension.Commands;
using ModernFormsNext.VisualStudioExtension.Detection;
using ModernFormsNext.VisualStudioExtension.Editors;
using Task = System.Threading.Tasks.Task;

namespace ModernFormsNext.VisualStudioExtension;

/// <summary>
/// Visual Studio package that registers ModernFormsNext designer integration.
/// </summary>
/// <remarks>
/// The package exposes a safe context command for ModernFormsNext designable C# files and
/// registers a technical <c>.mfdesign</c> editor factory. It does not globally replace the
/// normal C# editor.
/// </remarks>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("#110", "#112", "1.9.0")]
[ProvideMenuResource("ModernFormsNext.VisualStudioExtension.CTMENU", 1)]
[ProvideEditorFactory(typeof(MfDesignEditorFactory), 101)]
[ProvideEditorExtension(typeof(MfDesignEditorFactory), DesignFileExtension, 50, NameResourceID = 113, DefaultName = ExtensionDisplayName)]
[Guid(PackageGuidString)]
public sealed class ModernFormsDesignerPackage : AsyncPackage
{
    /// <summary>
    /// Gets the package GUID.
    /// </summary>
    public const string PackageGuidString = "0E6A57C2-A0C1-456E-BFE1-5C8F6EB0AA11";

    /// <summary>
    /// Gets the editor factory GUID.
    /// </summary>
    public const string EditorFactoryGuidString = "C61567C8-F5AC-4F9E-9C6E-B4EC99C7AB31";

    /// <summary>
    /// Gets the command set GUID.
    /// </summary>
    public const string CommandSetGuidString = "CDA8870E-234B-44C9-BA43-362BFF40A0E3";

    /// <summary>
    /// Gets the editor extension handled by the ModernFormsNext designer metadata editor.
    /// </summary>
    public const string DesignFileExtension = ".mfdesign";

    /// <summary>
    /// Gets the display name used by the Visual Studio extension.
    /// </summary>
    public const string ExtensionDisplayName = "ModernFormsNext Designer";

    /// <summary>
    /// Gets the package description used by the VSIX manifest.
    /// </summary>
    public const string Description = "Visual Studio designer support for ModernFormsNext .mfdesign files.";

    internal static readonly Guid EditorFactoryGuid = new(EditorFactoryGuidString);
    internal static readonly Guid CommandSetGuid = new(CommandSetGuidString);

    private readonly ModernFormsDesignableFileDetector detector = new();

    /// <inheritdoc/>
    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        RegisterEditorFactory(new MfDesignEditorFactory(this));
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
        ArgumentNullException.ThrowIfNull(fileInfo);

        await JoinableTaskFactory.SwitchToMainThreadAsync();

        EnsureDesignDocument(fileInfo);

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
        {
            if (fileInfo.RootKind == DesignRootKind.UserControl)
            {
                var existing = DesignDocumentSerializer.Default.Load(fileInfo.DesignFilePath);

                if (existing.RootKind != DesignRootKind.UserControl)
                {
                    existing.RootKind = DesignRootKind.UserControl;
                    DesignDocumentSerializer.Default.Save(fileInfo.DesignFilePath, existing);
                }
            }

            return;
        }

        if (File.Exists(fileInfo.DesignerCodePath))
        {
            var sourceText = File.ReadAllText(fileInfo.DesignerCodePath);
            var parseResult = new CSharpDesignerRoundTripService().ParseDesignerCode(
                sourceText,
                new CSharpDesignerParseOptions
                {
                    NamespaceOverride = fileInfo.Namespace,
                    ClassNameOverride = fileInfo.ClassName,
                    FormNameOverride = fileInfo.ClassName,
                    RootKind = fileInfo.RootKind,
                    AnimationDefinitions = ModernFormsNext.Designer.Services.DesignerProjectAnimationDefinitionDiscovery.Discover(
                        FindNearestProjectPath(fileInfo.CodeFilePath))
                });

            if (parseResult.Success && parseResult.Document is not null)
            {
                DesignDocumentSerializer.Default.Save(fileInfo.DesignFilePath, parseResult.Document);
                return;
            }
        }

        var document = new DesignDocument
        {
            Namespace = fileInfo.Namespace ?? string.Empty,
            ClassName = fileInfo.ClassName ?? Path.GetFileNameWithoutExtension(fileInfo.CodeFilePath),
            FormName = fileInfo.ClassName ?? Path.GetFileNameWithoutExtension(fileInfo.CodeFilePath),
            RootKind = fileInfo.RootKind,
            Size = new DesignSize(900, 600)
        };

        DesignDocumentSerializer.Default.Save(fileInfo.DesignFilePath, document);
    }

    private static string? FindNearestProjectPath(string path)
    {
        string? directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string? project = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (project is not null)
                return project;
            directory = Directory.GetParent(directory)?.FullName;
        }
        return null;
    }
}
