using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using ModernFormsNext.VisualStudioExtension.Localization;

namespace ModernFormsNext.VisualStudioExtension.Commands;

internal sealed class ViewModernFormsNextDesignerCommand
{
    private const int CommandId = 0x0100;
    private readonly ModernFormsDesignerPackage package;

    private ViewModernFormsNextDesignerCommand(
        ModernFormsDesignerPackage package,
        OleMenuCommandService commandService)
    {
        this.package = package;

        var commandId = new CommandID(ModernFormsDesignerPackage.CommandSetGuid, CommandId);
        var command = new OleMenuCommand(Execute, commandId);
        command.BeforeQueryStatus += BeforeQueryStatus;
        commandService.AddCommand(command);
    }

    public static async Task InitializeAsync(ModernFormsDesignerPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        await package.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (await package.GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            _ = new ViewModernFormsNextDesignerCommand(package, commandService);
    }

    private void BeforeQueryStatus(object? sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (sender is not OleMenuCommand command)
            return;

        var fileInfo = package.GetSelectedDesignableFile();
        var isVisible = fileInfo is not null;
        var isDesignable = fileInfo?.IsDesignable == true;
        command.Text = VisualStudioDesignerText.ViewDesignerCommand;
        command.Visible = isVisible;
        command.Enabled = isDesignable;
    }

    private void Execute(object? sender, EventArgs e)
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
