using ModernFormsNext.WindowKit.Platform.Services;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsMessageDialogTests
{
    [Theory]
    [InlineData(MessageBoxButtons.OK, 0u, 1, 0)]
    [InlineData(MessageBoxButtons.OKCancel, 1u, 2, 1)]
    [InlineData(MessageBoxButtons.YesNo, 4u, 6, 0)]
    [InlineData(MessageBoxButtons.YesNo, 4u, 7, 1)]
    [InlineData(MessageBoxButtons.YesNoCancel, 3u, 2, 2)]
    [InlineData(MessageBoxButtons.RetryCancel, 5u, 4, 0)]
    [InlineData(MessageBoxButtons.RetryCancel, 5u, 2, 1)]
    public void ButtonsAndResultsUseNativeDefinitions(MessageBoxButtons buttons, uint flags, int result, int index)
    {
        Assert.Equal(flags, WindowsMessageDialogService.Flags(new("", "", buttons), false));
        Assert.Equal(flags | 0x2000u, WindowsMessageDialogService.Flags(new("", "", buttons), true));
        Assert.Equal(index, WindowsMessageDialogService.Selection(buttons, result));
    }
    [Theory]
    [InlineData(MessageBoxIcon.None, 0u)][InlineData(MessageBoxIcon.Information, 0x40u)]
    [InlineData(MessageBoxIcon.Warning, 0x30u)][InlineData(MessageBoxIcon.Error, 0x10u)]
    [InlineData(MessageBoxIcon.Question, 0x20u)]
    public void IconsUseNativeDefinitions(MessageBoxIcon icon, uint flags)
        => Assert.Equal(flags, WindowsMessageDialogService.Flags(new("", "", icon: icon), false));

    [Fact]
    public void InvalidNativeChoiceIsNotInvented()
        => Assert.Throws<InvalidOperationException>(() => WindowsMessageDialogService.Selection(MessageBoxButtons.YesNo, 2));
}
