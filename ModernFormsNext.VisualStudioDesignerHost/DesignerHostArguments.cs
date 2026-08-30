using System.Globalization;
using ModernFormsNext.VisualStudioExtension;

namespace ModernFormsNext.VisualStudioDesignerHost;

/// <summary>
/// Contains command-line arguments passed from the Visual Studio extension to the
/// out-of-process ModernFormsNext designer host.
/// </summary>
public sealed class DesignerHostArguments
{
    private DesignerHostArguments(
        string? designDocumentPath,
        string? projectPath,
        string? pipeName,
        IntPtr parentWindowHandle,
        DesignerHostingMode hostingMode,
        int ownerProcessId)
    {
        DesignDocumentPath = designDocumentPath;
        ProjectPath = projectPath;
        PipeName = pipeName;
        ParentWindowHandle = parentWindowHandle;
        HostingMode = hostingMode;
        OwnerProcessId = ownerProcessId;
    }

    /// <summary>
    /// Gets the <c>.mfdesign</c> document path to load, or <see langword="null"/> when the
    /// host should start with a default sample document.
    /// </summary>
    public string? DesignDocumentPath { get; }

    /// <summary>
    /// Gets the owning project file or project directory supplied by Visual Studio, or
    /// <see langword="null"/> when the host should infer it from the design document.
    /// </summary>
    public string? ProjectPath { get; }

    /// <summary>
    /// Gets the optional private named pipe used by one Visual Studio editor pane to send
    /// document and lifetime commands to its owned Designer host process.
    /// </summary>
    public string? PipeName { get; }

    /// <summary>
    /// Gets the Visual Studio editor-pane HWND that owns this host, or zero when the Designer
    /// should run as a standalone top-level window.
    /// </summary>
    public IntPtr ParentWindowHandle { get; }

    /// <summary>
    /// Gets the explicit window-hosting mode selected by the Visual Studio extension.
    /// </summary>
    public DesignerHostingMode HostingMode { get; }

    /// <summary>
    /// Gets the Visual Studio process identifier whose lifetime owns this host, or zero when the
    /// host was started manually without an owner process monitor.
    /// </summary>
    public int OwnerProcessId { get; }

    /// <summary>
    /// Parses command-line arguments supplied by the VSIX launcher.
    /// </summary>
    /// <param name="args">The command-line argument array.</param>
    /// <returns>The parsed designer host arguments.</returns>
    public static DesignerHostArguments Parse(IReadOnlyList<string> args)
    {
        string? designFile = null;
        string? projectPath = null;
        string? pipeName = null;
        var parentWindowHandle = IntPtr.Zero;
        var hostingMode = DesignerHostingMode.Integrated;
        var hostingModeSpecified = false;
        var ownerProcessId = 0;

        for (var index = 0; index < args.Count; index++)
        {
            if (string.Equals(args[index], "--design-file", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                designFile = args[index + 1];
                index++;
                continue;
            }

            if (string.Equals(args[index], "--project", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                projectPath = args[index + 1];
                index++;
                continue;
            }

            if (string.Equals(args[index], "--pipe", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                pipeName = args[index + 1];
                index++;
                continue;
            }

            if (string.Equals(args[index], "--parent-window", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                if (!long.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawHandle)
                    || rawHandle == 0)
                {
                    throw new ArgumentException("The --parent-window value must be a non-zero native handle.", nameof(args));
                }

                parentWindowHandle = new IntPtr(rawHandle);
                index++;
                continue;
            }

            if (string.Equals(args[index], "--host-mode", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                hostingMode = args[index + 1].ToLowerInvariant() switch
                {
                    "integrated" => DesignerHostingMode.Integrated,
                    "standalone" => DesignerHostingMode.Standalone,
                    _ => throw new ArgumentException(
                        "The --host-mode value must be either 'integrated' or 'standalone'.",
                        nameof(args))
                };
                hostingModeSpecified = true;
                index++;
                continue;
            }

            if (string.Equals(args[index], "--owner-process", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                if (!int.TryParse(
                        args[index + 1],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out ownerProcessId)
                    || ownerProcessId <= 0)
                {
                    throw new ArgumentException(
                        "The --owner-process value must be a positive process identifier.",
                        nameof(args));
                }

                index++;
                continue;
            }
        }

        if (string.IsNullOrWhiteSpace(designFile)
            && args.Count > 0
            && !string.IsNullOrWhiteSpace(args[0])
            && !args[0].StartsWith("--", StringComparison.Ordinal))
        {
            designFile = args[0];
        }

        if (!hostingModeSpecified)
        {
            throw new ArgumentException(
                "The Designer host requires an explicit --host-mode value.",
                nameof(args));
        }

        if (hostingMode == DesignerHostingMode.Integrated && parentWindowHandle == IntPtr.Zero)
        {
            throw new ArgumentException(
                "Integrated hosting requires a non-zero --parent-window handle.",
                nameof(args));
        }

        if (hostingMode == DesignerHostingMode.Standalone && parentWindowHandle != IntPtr.Zero)
        {
            throw new ArgumentException(
                "Standalone hosting must not receive a --parent-window handle.",
                nameof(args));
        }

        return new DesignerHostArguments(
            designFile,
            projectPath,
            pipeName,
            parentWindowHandle,
            hostingMode,
            ownerProcessId);
    }
}
