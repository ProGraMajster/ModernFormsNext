using System.Diagnostics;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsNativeMessageDialogTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSystemDialogUsesOwnerButtonsCancellationAndCleanup(bool exitBeforeShow)
    {
        if (!OperatingSystem.IsWindows()) return;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "ModernFormsNext.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var start = new ProcessStartInfo("dotnet") {
            CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(System.IO.Path.Combine(directory.FullName, "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost",
            "bin", configuration, "net10.0-windows", "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost.dll"));
        start.ArgumentList.Add("--message-dialog");
        if (exitBeforeShow) start.ArgumentList.Add("--exit-before-show");
        using var host = Process.Start(start)!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var output = host.StandardOutput.ReadToEndAsync(deadline.Token);
        var errors = host.StandardError.ReadToEndAsync(deadline.Token);
        try {
            await host.WaitForExitAsync(deadline.Token);
            var result = await output;
            Assert.True(host.ExitCode == 0, result + await errors);
            if (exitBeforeShow)
            {
                Assert.Contains("MESSAGE:EXIT-BEFORE-SHOW", result);
                Assert.Contains("MESSAGE:NATIVE:PASS", result);
                return;
            }
            Assert.Contains("MESSAGE:RETURNS-BEFORE-SHOW:BUSY:CANCEL", result);
            Assert.Contains("MESSAGE:OWNER-CLOSE-BEFORE-SHOW", result);
            Assert.Contains("MESSAGE:OWNER-HIDE-BEFORE-SHOW", result);
            Assert.Contains("MESSAGE:BUSY-DURING-SHOW", result);
            Assert.Contains("MESSAGE:CHOICE:OK:OK:OWNER:True:CLEAN", result);
            Assert.Contains("MESSAGE:CHOICE:YesNo:Yes:OWNER:True:CLEAN", result);
            Assert.Contains("MESSAGE:CHOICE:YesNo:No:OWNER:True:CLEAN", result);
            Assert.Contains("MESSAGE:CHOICE:YesNoCancel:Cancel:OWNER:True:CLEAN", result);
            Assert.Contains("MESSAGE:CHOICE:OK:OK:OWNER:False:CLEAN", result);
            Assert.Contains("MESSAGE:CANCEL:CLEAN", result);
            Assert.Contains("MESSAGE:OWNER-LOSS", result);
            Assert.Contains("MESSAGE:SHUTDOWN", result);
            Assert.Contains("MESSAGE:NATIVE:PASS", result);
        }
        finally {
            if (!host.HasExited) { host.Kill(true); await host.WaitForExitAsync(); }
        }
    }
}
