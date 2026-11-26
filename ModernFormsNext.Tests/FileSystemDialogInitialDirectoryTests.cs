using ModernFormsNext.WindowKit.Platform.Storage;
using ModernFormsNext.WindowKit.Platform.Storage.FileIO;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class FileSystemDialogInitialDirectoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void OmittedDirectoryLetsTheProviderChooseItsDefault(string? path)
    {
        var dialog = new FolderBrowserDialog { InitialDirectory = path };

        Assert.Null(dialog.GetInitialDirectory());
    }

    [Fact]
    public void ExistingAbsoluteDirectoryIsSuggested()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        var dialog = new FolderBrowserDialog { InitialDirectory = directory.FullName };

        using IStorageFolder? folder = dialog.GetInitialDirectory();
        Assert.Equal(directory.FullName, folder?.TryGetLocalPath());
    }

    [Fact]
    public void RelativeDirectoryResolvesAgainstTheWorkingDirectory()
    {
        var dialog = new FolderBrowserDialog { InitialDirectory = "." };

        using IStorageFolder? folder = dialog.GetInitialDirectory();
        Assert.Equal(Environment.CurrentDirectory, folder?.TryGetLocalPath());
    }

    [Fact]
    public void MissingDirectorySuppliesNoSuggestion()
    {
        var dialog = new FolderBrowserDialog
        {
            InitialDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"))
        };

        Assert.Null(dialog.GetInitialDirectory());
    }

    [Fact]
    public void NonFileUriSuppliesNoLocalDirectory()
    {
        var dialog = new FolderBrowserDialog { InitialDirectory = "content://example/tree/folder" };

        Assert.Null(dialog.GetInitialDirectory());
    }

    [Fact]
    public void MalformedPathStillRaisesAnArgumentException()
    {
        var dialog = new FolderBrowserDialog { InitialDirectory = "invalid\0path" };

        Assert.Throws<ArgumentException>(() => dialog.GetInitialDirectory());
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid\0path")]
    public void ExplicitLocationTakesPrecedence(string path)
    {
        using var location = new BclStorageFolder(new DirectoryInfo(AppContext.BaseDirectory));
        var dialog = new FolderBrowserDialog { InitialLocation = location, InitialDirectory = path };

        Assert.Same(location, dialog.GetInitialDirectory());
    }
}
