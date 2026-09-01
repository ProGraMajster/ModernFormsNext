using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsPlatformBootstrapTests
{
    [Fact]
    public void InitializeServicesRegistersSettingsRequiredByTheNativeMessageWindow()
    {
        WindowsPlatformBootstrap.InitializeServices();

        var settings = AvaloniaGlobals.GetRequiredService<IPlatformSettings>();

        Assert.IsType<Win32PlatformSettings>(settings);
    }
}
