using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

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
        if (package is null)
            throw new ArgumentNullException(nameof(package));

        await package.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (await package.GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            _ = new ViewModernFormsNextDesignerCommand(package, commandService);
    }

    private void BeforeQueryStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (sender is not OleMenuCommand command)
            return;

        var fileInfo = package.GetSelectedDesignableFile();
        var isVisible = fileInfo?.IsDesignable == true;
        command.Visible = isVisible;
        command.Enabled = isVisible;
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
