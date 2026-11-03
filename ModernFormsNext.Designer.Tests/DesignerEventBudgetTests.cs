using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerEventBudgetTests
{
    [Theory]
    [InlineData("enable", true)]
    [InlineData("disable", false)]
    public void ImplicitUsingsFollowLiteralProjectSettings(string setting, bool expected)
    {
        using var fixture = new Files();
        File.WriteAllText(fixture.Project, $"<Project><PropertyGroup><ImplicitUsings>{setting}</ImplicitUsings></PropertyGroup></Project>");
        const string source = "namespace Example; public class Sample { private void OnClick(object? s, EventArgs e) { } }";
        var result = DesignerEventSource.Plan(Document, fixture.Source, source, "OnClick",
            "object? sender, global::System.EventArgs e", fixture.Project, false);
        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public void FileByteBudgetPrecedesDecodingOrParsing()
    {
        using var fixture = new Files();
        using (var stream = File.Create(fixture.Source)) stream.SetLength(DesignerEventSource.MaximumSourceCharacters * 4L + 1);
        Assert.Throws<InvalidDataException>(() => DesignerProjectUserControlDiscovery.ReadEventSourceText(fixture.Source));
        Assert.Throws<InvalidDataException>(() => DesignerProjectUserControlDiscovery.ReadEventSourceBytes(fixture.Source));
    }

    [Fact]
    public void CombinedSourceBudgetBoundsAnOtherwiseValidProject()
    {
        using var fixture = new Files();
        var content = new string(' ', DesignerEventSource.MaximumSourceCharacters);
        for (var index = 0; index < 17; index++)
            File.WriteAllText(IOPath.Combine(fixture.Directory, $"Part{index}.cs"), content);
        var tree = CSharpSyntaxTree.ParseText("namespace Example; public class Sample { }", path: fixture.Source);
        var error = Assert.Throws<InvalidDataException>(() => DesignerProjectUserControlDiscovery.CreateEventCompilation(tree, fixture.Project));
        Assert.Contains("project safety budget", error.Message);
    }

    [Fact]
    public void PeImageBudgetPrecedesMetadataReading()
    {
        using var fixture = new Files();
        File.WriteAllText(fixture.Project, "<Project><ItemGroup><Reference Include=\"Large\"><HintPath>Large.dll</HintPath></Reference></ItemGroup></Project>");
        using (var stream = File.Create(IOPath.Combine(fixture.Directory, "Large.dll"))) stream.SetLength(17 * 1024 * 1024);
        var tree = CSharpSyntaxTree.ParseText("namespace Example; public class Sample { }", path: fixture.Source);
        var error = Assert.Throws<InvalidDataException>(() => DesignerProjectUserControlDiscovery.CreateEventCompilation(tree, fixture.Project));
        Assert.Contains("metadata references", error.Message);
    }

    [Fact]
    public void ChooserMethodBudgetRejectsAnUnboundedList()
    {
        const string parameters = "object? sender, global::System.EventArgs e";
        var source = "namespace Example; public class Sample { "
            + string.Join(" ", Enumerable.Range(0, 129).Select(i => $"private void Handler{i}({parameters}) {{ }}")) + " }";
        var error = Assert.Throws<InvalidDataException>(() => DesignerEventSource.CompatibleMethods(Document, "Sample.cs", source, parameters, null));
        Assert.Contains("128 method names", error.Message);
    }

    [Fact]
    public void AnalysisHonorsCancellationBeforeInspectingTheProject()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => DesignerEventSource.Plan(Document, "Sample.cs",
            "namespace Example; public class Sample { }", "OnClick", "object? sender, global::System.EventArgs e", null, true,
            cancellationToken: cancellation.Token));
    }

    private static DesignDocument Document => new() { Namespace = "Example", ClassName = "Sample" };

    private sealed class Files : IDisposable
    {
        internal string Directory { get; } = IOPath.Combine(IOPath.GetTempPath(), "ModernFormsNext-event-budget-" + Guid.NewGuid().ToString("N"));
        internal string Source => IOPath.Combine(Directory, "Sample.cs");
        internal string Project => IOPath.Combine(Directory, "Sample.csproj");
        internal Files()
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(Project, "<Project />");
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
