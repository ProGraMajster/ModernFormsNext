using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;

namespace ModernFormsNext.VisualStudioExtension.Commands;

internal sealed class ViewModernFormsNextDesignerCommand
{
    private const int CommandId = 0x0100;
    private readonly ModernFormsDesignerPackage package;
    private readonly bool isStandardViewDesignerCommand;

    private ViewModernFormsNextDesignerCommand(
        ModernFormsDesignerPackage package,
        OleMenuCommandService commandService,
        CommandID commandId,
        bool isStandardViewDesignerCommand)
    {
        this.package = package;
        this.isStandardViewDesignerCommand = isStandardViewDesignerCommand;

        var command = new OleMenuCommand(Execute, commandId);
        command.BeforeQueryStatus += BeforeQueryStatus;
        commandService.AddCommand(command);
    }

    public static async Task InitializeAsync(ModernFormsDesignerPackage package)
    {
        if (package is null)
            throw new ArgumentNullException(nameof(package));

        await package.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (await package.GetServiceAsync(typeof(IMenuCommandService)) is not OleMenuCommandService commandService)
            return;

        _ = new ViewModernFormsNextDesignerCommand(
            package,
            commandService,
            new CommandID(ModernFormsDesignerPackage.CommandSetGuid, CommandId),
            isStandardViewDesignerCommand: false);
        _ = new ViewModernFormsNextDesignerCommand(
            package,
            commandService,
            new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.ViewForm),
            isStandardViewDesignerCommand: true);
    }

    private void BeforeQueryStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (sender is not OleMenuCommand command)
            return;

        var fileInfo = package.GetSelectedDesignableFile();
        var isVisible = fileInfo is not null;
        var isDesignable = fileInfo?.IsDesignable == true;
        var status = VisualStudioDesignerCommandRouter.Evaluate(
            isVisible,
            isDesignable,
            isStandardViewDesignerCommand);
        command.Supported = status.Supported;
        command.Visible = status.Visible;
        command.Enabled = status.Enabled;
    }

    private void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var fileInfo = package.GetSelectedDesignableFile();

        if (fileInfo?.IsDesignable != true)
            return;

        _ = package.JoinableTaskFactory.RunAsync(async () =>
        {
            await package.OpenDesignerForCodeFileAsync(fileInfo);
        });
    }
}
