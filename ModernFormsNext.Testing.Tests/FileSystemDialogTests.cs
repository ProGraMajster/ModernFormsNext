using ModernFormsNext.WindowKit.Platform.Storage.FileIO;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class FileSystemDialogTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task PickersReturnSelectionsAndClearThemOnCancelWithoutAnInitialDirectory(string? initialDirectory)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        host.Show(owner);

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        using var selectedFolder = new BclStorageFolder(directory);
        using var selectedFile = new BclStorageFile(new FileInfo(typeof(FileSystemDialogTests).Assembly.Location));
        var provider = host.Services.Storage;
        provider.Folders = [selectedFolder];
        provider.OpenFiles = [selectedFile];
        provider.SaveFile = selectedFile;

        var folder = new FolderBrowserDialog { InitialDirectory = initialDirectory };
        var open = new OpenFileDialog { InitialDirectory = initialDirectory };
        var save = new SaveFileDialog { InitialDirectory = initialDirectory };

        Assert.Equal(DialogResult.OK, await folder.ShowDialog(owner));
        Assert.Same(selectedFolder, folder.SelectedFolder);
        Assert.Equal(directory.FullName, folder.SelectedPath);
        Assert.Equal(DialogResult.OK, await open.ShowDialog(owner));
        Assert.Same(selectedFile, Assert.Single(open.SelectedFiles));
        Assert.Equal(typeof(FileSystemDialogTests).Assembly.Location, Assert.Single(open.FileNames));
        Assert.Equal(DialogResult.OK, await save.ShowDialog(owner));
        Assert.Same(selectedFile, Assert.Single(save.SelectedFiles));
        Assert.Equal(typeof(FileSystemDialogTests).Assembly.Location, Assert.Single(save.FileNames));

        provider.Folders = [];
        provider.OpenFiles = [];
        provider.SaveFile = null;

        Assert.Equal(DialogResult.Cancel, await folder.ShowDialog(owner));
        Assert.Null(folder.SelectedFolder);
        Assert.Null(folder.SelectedPath);
        Assert.Equal(DialogResult.Cancel, await open.ShowDialog(owner));
        Assert.Empty(open.SelectedFiles);
        Assert.Empty(open.FileNames);
        Assert.Equal(DialogResult.Cancel, await save.ShowDialog(owner));
        Assert.Empty(save.SelectedFiles);
        Assert.Empty(save.FileNames);
    }
}
