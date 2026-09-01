namespace ModernFormsNext.VisualStudioExtension.Commands;

internal readonly struct VisualStudioDesignerCommandStatus
{
    public VisualStudioDesignerCommandStatus(bool supported, bool visible, bool enabled)
    {
        Supported = supported;
        Visible = visible;
        Enabled = enabled;
    }

    public bool Supported { get; }

    public bool Visible { get; }

    public bool Enabled { get; }
}

internal static class VisualStudioDesignerCommandRouter
{
    public static VisualStudioDesignerCommandStatus Evaluate(
        bool hasCandidateFile,
        bool isDesignable,
        bool isStandardViewDesignerCommand)
    {
        if (isStandardViewDesignerCommand)
        {
            // Returning unsupported is essential: it lets Visual Studio continue routing the
            // built-in View Designer command for WinForms and other project systems.
            return new VisualStudioDesignerCommandStatus(
                supported: isDesignable,
                visible: isDesignable,
                enabled: isDesignable);
        }

        return new VisualStudioDesignerCommandStatus(
            supported: true,
            visible: hasCandidateFile,
            enabled: isDesignable);
    }
}
