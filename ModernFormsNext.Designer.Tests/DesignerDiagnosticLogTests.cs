using ModernFormsNext.Designer.Services;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerDiagnosticLogTests
{
    [Fact]
    public void DiagnosticPathsAreIsolatedPerProcess()
    {
        var directory = IOPath.Combine(IOPath.GetTempPath(), "ModernFormsNext-Designer-Log-Contract");

        var first = DesignerDiagnosticLog.GetPath(directory, 101);
        var second = DesignerDiagnosticLog.GetPath(directory, 202);

        Assert.NotEqual(first, second);
        Assert.Equal("designer-debug-101.log", IOPath.GetFileName(first));
        Assert.Equal("designer-debug-202.log", IOPath.GetFileName(second));
        Assert.Contains(Environment.ProcessId.ToString(), DesignerDiagnosticLog.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteAppendsToTheCurrentProcessLog()
    {
        var marker = $"diagnostic-contract-{Guid.NewGuid():N}";

        DesignerDiagnosticLog.Write(marker);

        using var stream = new FileStream(
            DesignerDiagnosticLog.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        Assert.Contains(marker, reader.ReadToEnd(), StringComparison.Ordinal);
    }
}
