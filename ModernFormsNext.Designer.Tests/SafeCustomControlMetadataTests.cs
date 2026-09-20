using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class SafeCustomControlMetadataTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceAndBinaryMetadataNeverLoadOrExecuteUserCode(bool binary)
    {
        using var project = new Fixture();
        const string source = """
            using System;
            using System.ComponentModel;
            using ModernFormsNext;
            using ModernFormsNext.Designing;
            namespace Widgets;
            public class ExplodingAttribute : Attribute { public ExplodingAttribute() => throw new Exception("attribute"); }
            public class ExplodingConverter : TypeConverter { public ExplodingConverter() => throw new Exception("converter"); }
            public enum Tone { Quiet, Loud }
            public abstract class BaseWidget : UserControl
            {
                [DefaultValue(7), Category("Data")]
                public virtual int Count { get => throw new Exception("getter"); set => throw new Exception("setter"); }
            }
            [Exploding, DesignableControl("Safe Widget", Category = "Widgets")]
            public class Widget : BaseWidget
            {
                static Widget() => throw new Exception("static initializer");
                public Widget() => throw new Exception("constructor");
                [TypeConverter(typeof(ExplodingConverter)), DefaultValue("hello"), DisplayName("Caption")]
                public string CaptionText { get => throw new Exception("getter"); set => throw new Exception("setter"); }
                [DefaultValue(Tone.Loud)] public Tone Tone { get; set; }
                [DefaultValue(typeof(int), "12")] public int Limit { get; set; }
                [Browsable(false)] public int Hidden { get; set; }
                [Browsable(false)] public new string Text { get; set; }
                [Browsable(false)] public new event EventHandler Click { add { } remove { } }
                [ReadOnly(true)] public int Locked { get; set; }
                public int InitOnly { get; init; }
                public object ExecutableOnly { get => throw new Exception("getter"); set { } }
                public float SingleValue { get; set; }
                public event EventHandler Changed { add => throw new Exception("delegate"); remove { } }
                public event Func<int> Unsupported { add => throw new Exception("delegate"); remove { } }
            }
            """;
        if (binary) project.BuildReference(source);
        else File.WriteAllText(IOPath.Combine(project.DirectoryPath, "Widget.cs"), source);
        var loads = new List<string>();
        AssemblyLoadEventHandler observer = (_, e) =>
        {
            if (e.LoadedAssembly.GetName().Name == project.AssemblyName) loads.Add(project.AssemblyName);
        };
        AppDomain.CurrentDomain.AssemblyLoad += observer;
        try
        {
            var catalog = DesignerProjectUserControlDiscovery.DiscoverCatalog(project.ProjectPath);
            var control = Assert.Single(catalog.Controls);
            Assert.Equal("Widgets.Widget", control.FullName);
            Assert.Equal("Safe Widget", control.DisplayName);
            Assert.Equal("Widgets", control.Category);
            Assert.Equal(7, control.Properties.Single(p => p.Name == "Count").DefaultValue!.Value);
            Assert.Equal("Data", control.Properties.Single(p => p.Name == "Count").Category);
            Assert.Equal("hello", control.Properties.Single(p => p.Name == "CaptionText").DefaultValue!.Value);
            Assert.Equal("Loud", control.Properties.Single(p => p.Name == "Tone").DefaultValue!.Value);
            Assert.Equal(12, control.Properties.Single(p => p.Name == "Limit").DefaultValue!.Value);
            Assert.DoesNotContain(control.Properties, p => p.Name == "Hidden");
            Assert.All(control.Properties.Where(p => p.Name is "Locked" or "InitOnly" or "ExecutableOnly" or "SingleValue"), p => Assert.True(p.ReadOnly));
            Assert.Contains(catalog.Diagnostics, d => d.Contains("TypeConverter code is not executed"));
            Assert.Null(control.Events.Single(e => e.Name == "Unsupported").Parameters);
            Assert.Contains("global::System.EventArgs", control.Events.Single(e => e.Name == "Changed").Parameters);

            using var session = new DesignerSession(project);
            var node = session.AddControl("Widgets.Widget");
            session.SelectNode(node);
            var grid = new DesignerPropertyGridState(session);
            Assert.Equal("hello", grid.Properties.Single(p => p.Name == "CaptionText").GetValueText());
            Assert.True(grid.Properties.Single(p => p.Name == "Count").TryCommit("42", out _));
            Assert.False(grid.Events.Single(e => e.Name == "Unsupported").TryCommit("OnUnsupported", out _));
            Assert.DoesNotContain(grid.Properties, p => p.Name == "Text");
            Assert.DoesNotContain(grid.Events, e => e.Name == "Click");
            Assert.Null(session.ResolveControlType($"Widgets.Widget, {project.AssemblyName}"));
            Assert.Empty(loads);
        }
        finally { AppDomain.CurrentDomain.AssemblyLoad -= observer; }
    }

    [Fact]
    public void RefreshReplacesBinaryMetadataAndPreservesDocumentSelectionAndHistory()
    {
        using var project = new Fixture();
        project.BuildReference("namespace Widgets; public class WidgetPanel : ModernFormsNext.UserControl { public int OldValue { get; set; } }");
        using var session = new DesignerSession(project);
        var node = session.AddControl("Widgets.WidgetPanel");
        session.SelectNode(node);
        session.SetPropertyValue(node, "OldValue", DesignPropertyValue.FromInt32(9));
        var grid = new DesignerPropertyGridState(session);
        var json = DesignDocumentSerializer.Default.Serialize(session.Document);
        var history = session.Transactions.UndoDescription;
        var dirty = session.IsDirty;
        var version = session.ToolboxVersion;
        project.BuildReference("namespace Widgets; public class WidgetPanel : ModernFormsNext.UserControl { public string NewValue { get; set; } }");
        session.RefreshToolbox();
        Assert.True(session.ToolboxVersion > version);
        Assert.Contains(grid.Properties, p => p.Name == "NewValue");
        Assert.DoesNotContain(grid.Properties, p => p.Name == "OldValue");
        Assert.Equal(json, DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.Same(node, session.SelectedNode);
        Assert.Equal(history, session.Transactions.UndoDescription);
        Assert.Equal(dirty, session.IsDirty);

        File.Delete(project.BinaryPath); // A catalog must not keep a file lock or a cached assembly.
        session.RefreshToolbox();
        Assert.Empty(session.ProjectUserControls);
        Assert.False(session.IsContainerNode(node));
        Assert.False(session.IsContainerNode(new DesignControlNode { TypeName = "UnknownPanel", Name = "unknown" }));
        Assert.Contains(session.OutputLines, line => line.Contains("Missing reference"));
        Assert.Equal(json, DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.True(session.Transactions.Undo());
        Assert.False(node.Properties.ContainsKey("OldValue"));
        Assert.True(session.Transactions.Redo());
        Assert.Equal(9, node.Properties["OldValue"].Value);
    }

    [Fact]
    public void InvalidAndUnresolvedReferencesProduceDiagnosticsWithoutExecutingProjectTargets()
    {
        using var project = new Fixture();
        File.WriteAllText(project.BinaryPath, "not a managed image");
        File.WriteAllText(project.ProjectPath, $"""
            <Project><ItemGroup>
              <Reference Include="Bad"><HintPath>{project.BinaryPath}</HintPath></Reference>
              <Reference Include="Computed"><HintPath>$(UntrustedProperty)/Unknown.dll</HintPath></Reference>
              <Reference Include="Conditional" Condition="'$(Unknown)' == 'yes'"><HintPath>Unknown.dll</HintPath></Reference>
            </ItemGroup><Target Name="BeforeBuild"><Error Text="Must never run" /></Target></Project>
            """);
        var catalog = DesignerProjectUserControlDiscovery.DiscoverCatalog(project.ProjectPath);
        Assert.Empty(catalog.Controls);
        Assert.Contains(catalog.Diagnostics, d => d.Contains("Cannot inspect reference") || d.Contains("Incompatible managed reference"));
        Assert.Contains(catalog.Diagnostics, d => d.Contains("unsupported project evaluation"));
        Assert.Contains(catalog.Diagnostics, d => d.Contains("Conditional reference"));
    }

    [Fact]
    public void LiteralProjectReferenceReportsStaleBuildAndRefreshesAfterRebuild()
    {
        using var project = new Fixture();
        var referenceDirectory = IOPath.Combine(project.DirectoryPath, "Referenced");
        Directory.CreateDirectory(referenceDirectory);
        var referenceProject = IOPath.Combine(referenceDirectory, "Referenced.csproj");
        File.WriteAllText(referenceProject, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var output = IOPath.Combine(referenceDirectory, "bin", "Debug", "net10.0", "Referenced.dll");
        Directory.CreateDirectory(IOPath.GetDirectoryName(output)!);
        File.WriteAllBytes(output, Compile("namespace Widgets; public class BinaryWidget : ModernFormsNext.Control { public int Count { get; set; } }", "Referenced"));
        File.SetLastWriteTimeUtc(output, DateTime.UtcNow.AddMinutes(-2));
        File.WriteAllText(project.ProjectPath, "<Project><ItemGroup><ProjectReference Include=\"Referenced/Referenced.csproj\" /></ItemGroup></Project>");
        var catalog = DesignerProjectUserControlDiscovery.DiscoverCatalog(project.ProjectPath);
        Assert.Equal("Widgets.BinaryWidget", Assert.Single(catalog.Controls).FullName);
        Assert.Contains(catalog.Diagnostics, d => d.Contains("Stale build"));
        File.SetLastWriteTimeUtc(output, DateTime.UtcNow.AddMinutes(1));
        Assert.DoesNotContain(DesignerProjectUserControlDiscovery.DiscoverCatalog(project.ProjectPath).Diagnostics, d => d.Contains("Stale build"));
    }

    [Fact]
    public void CustomPropertiesAndEventSignatureRoundTripCompileAndMatchExplicitRuntimeFixture()
    {
        using var project = new Fixture();
        const string controls = """
            using System;
            using ModernFormsNext;
            namespace Widgets;
            public enum Tone { Quiet, Loud }
            public abstract class BaseWidget<T> : UserControl { public T Item { get; set; } }
            public class Widget : BaseWidget<int>
            {
                public string Caption { get; set; }
                public double Amount { get; set; }
                public bool Active { get; set; }
                public Tone Tone { get; set; }
                public int? Optional { get; set; }
                public event EventHandler Changed;
                public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
            }
            """;
        project.BuildReference(controls);
        using var session = new DesignerSession(project);
        var document = new DesignDocument { Namespace = "Example", ClassName = "Container", RootKind = DesignRootKind.UserControl, FormName = "Container" };
        session.LoadDocument(document);
        var node = session.AddControl("Widgets.Widget");
        node.Name = "widget";
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        foreach (var (name, text) in new[] { ("Item", "21"), ("Caption", "Saved"), ("Amount", "1.25"), ("Active", "True"), ("Tone", "Loud"), ("Optional", "null") })
            Assert.True(grid.Properties.Single(p => p.Name == name).TryCommit(text, out var error), error);
        Assert.False(grid.Properties.Single(p => p.Name == "Amount").TryCommit("NaN", out _));
        Assert.False(grid.Properties.Single(p => p.Name == "Tone").TryCommit("Loud; throw new Exception()", out _));
        var changed = grid.Events.Single(e => e.Name == "Changed");
        Assert.True(changed.TryCommit("OnWidgetChanged", out _));
        project.DocumentPath = IOPath.Combine(project.DirectoryPath, "Container.mfdesign");
        File.WriteAllText(IOPath.Combine(project.DirectoryPath, "Container.cs"), "namespace Example; public partial class Container : ModernFormsNext.UserControl { public Container() { InitializeComponent(); } }");
        var handler = new DesignerFileService(project).EnsureEventHandlerMethodFromMetadata(document, "OnWidgetChanged", changed.SafeParameters!);
        Assert.True(handler.Succeeded, handler.Message);
        var saved = DesignDocumentSerializer.Default.Serialize(document);
        var reopened = DesignDocumentSerializer.Default.Deserialize(saved);
        var generated = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(generated.Succeeded, string.Join("\n", generated.Validation.Errors));
        var parsed = new CSharpDesignerParser().Parse(generated.Code, new CSharpDesignerParseOptions { RootKind = DesignRootKind.UserControl });
        Assert.True(parsed.Success, string.Join("\n", parsed.Diagnostics.Select(d => d.Message)));
        var roundTrip = Assert.Single(parsed.Document!.Controls);
        Assert.Equal("Saved", roundTrip.Properties["Caption"].Value);
        Assert.Equal("Widgets.Tone", roundTrip.Properties["Tone"].EnumTypeName);
        Assert.Equal("OnWidgetChanged", roundTrip.Events["Changed"]);

        // Execution is explicit and isolated to this benign runtime-parity fixture AFTER discovery.
        // This is not a discovery/preview path and never runs the throwing control fixture above.
        var image = Compile([controls, generated.Code, File.ReadAllText(IOPath.Combine(project.DirectoryPath, "Container.cs"))], "Parity" + Guid.NewGuid().ToString("N"));
        var context = new AssemblyLoadContext("CustomControlParity", isCollectible: true);
        try
        {
            using var stream = new MemoryStream(image);
            var assembly = context.LoadFromStream(stream);
            using var container = (UserControl)Activator.CreateInstance(assembly.GetType("Example.Container")!)!;
            var runtime = Assert.Single(container.Controls.Cast<Control>());
            Assert.Equal("Saved", runtime.GetType().GetProperty("Caption")!.GetValue(runtime));
            Assert.Equal(21, runtime.GetType().GetProperty("Item")!.GetValue(runtime));
            Assert.Equal(1.25, runtime.GetType().GetProperty("Amount")!.GetValue(runtime));
            Assert.Equal("Loud", runtime.GetType().GetProperty("Tone")!.GetValue(runtime)!.ToString());
            runtime.GetType().GetMethod("RaiseChanged")!.Invoke(runtime, null);
        }
        finally { context.Unload(); }
    }

    private static byte[] Compile(string source, string name) => Compile([source], name);

    [Fact]
    public void PartialMetadataKeepsTheBaseDeclaringSourceFileForDocumentIdentity()
    {
        using var project = new Fixture();
        File.WriteAllText(IOPath.Combine(project.DirectoryPath, "00.Widget.Designer.cs"), "namespace Widgets; public partial class Widget { public int Count { get; set; } }");
        var source = IOPath.Combine(project.DirectoryPath, "Widget.cs");
        File.WriteAllText(source, "namespace Widgets; public partial class Widget : ModernFormsNext.UserControl { }");
        var control = Assert.Single(DesignerProjectUserControlDiscovery.Discover(project.ProjectPath));
        Assert.Equal(source, control.SourceFilePath);
        Assert.Contains(control.Properties, p => p.Name == "Count");
    }

    [Fact]
    public void FrameworkBaseEditorsRemainAvailableWithoutConstructingTheCustomSubclass()
    {
        using var project = new Fixture();
        project.BuildReference("namespace Widgets; public class Check : ModernFormsNext.CheckBox { public Check() => throw new System.Exception(\"constructor\"); public int Extra { get; set; } }");
        using var session = new DesignerSession(project);
        session.SelectNode(session.AddControl("Widgets.Check"));
        var grid = new DesignerPropertyGridState(session);
        Assert.Contains(grid.Properties, p => p.Name == "Extra" && !p.IsReadOnly);
        Assert.Contains(grid.Properties, p => p.Name == "ThreeState" && !p.IsReadOnly);
        Assert.Contains(grid.Events, e => e.Name == "CheckedChanged");
    }

    [Fact]
    public void InvalidAssetsShapeReportsAnActionableFailureAndRetainsSourceDiscovery()
    {
        using var project = new Fixture();
        File.WriteAllText(IOPath.Combine(project.DirectoryPath, "Widget.cs"), "public class Widget : ModernFormsNext.UserControl {}");
        Directory.CreateDirectory(IOPath.Combine(project.DirectoryPath, "obj"));
        File.WriteAllText(IOPath.Combine(project.DirectoryPath, "obj", "project.assets.json"), "[]");
        var catalog = DesignerProjectUserControlDiscovery.DiscoverCatalog(project.ProjectPath);
        Assert.Equal("Widget", Assert.Single(catalog.Controls).Name);
        Assert.Contains(catalog.Diagnostics, d => d.Contains("Cannot inspect project references"));
    }

    [Fact]
    public void RootMetadataEditsUseExistingTransactionsAndRefreshDoesNotChangeSavedDefaults()
    {
        using var project = new Fixture();
        File.WriteAllText(IOPath.Combine(project.DirectoryPath, "Widget.cs"), """
            using System.ComponentModel;
            namespace Widgets;
            public class Widget : ModernFormsNext.UserControl
            {
                [DefaultValue(5)] public int Count { get; set; }
                public event System.EventHandler Changed;
            }
            """);
        using var session = new DesignerSession(project);
        session.LoadDocument(new DesignDocument { Namespace = "Widgets", ClassName = "Widget", RootKind = DesignRootKind.UserControl });
        var grid = new DesignerPropertyGridState(session);
        var property = grid.Properties.Single(p => p.Name == "Count");
        Assert.Equal("5", property.GetValueText());
        grid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.True(grid.CommitSelectedValue("19"));
        Assert.True(session.Transactions.Undo());
        Assert.False(session.Document.Properties.ContainsKey("Count"));
        Assert.True(session.Transactions.Redo());
        Assert.Equal(19, session.Document.Properties["Count"].Value);
        var saved = DesignDocumentSerializer.Default.Serialize(session.Document);
        session.RefreshToolbox();
        Assert.Equal(saved, DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.Contains(grid.Events, e => e.Name == "Changed" && e.SafeParameters is not null);
    }

    [Fact]
    public void RestoredPackageCompileAssetsAreReadWithoutLoadingAssemblies()
    {
        using var project = new Fixture();
        var packageRoot = IOPath.Combine(project.DirectoryPath, "packages");
        var libraryDirectory = IOPath.Combine(packageRoot, "widgets", "1.0.0", "ref", "net10.0");
        Directory.CreateDirectory(libraryDirectory);
        Directory.CreateDirectory(IOPath.Combine(project.DirectoryPath, "obj"));
        File.WriteAllBytes(IOPath.Combine(libraryDirectory, "Widgets.dll"), Compile("namespace Widgets; public class PackageWidget : ModernFormsNext.UserControl { }", "Widgets"));
        File.WriteAllText(project.ProjectPath, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"Widgets\" Version=\"1.0.0\" /></ItemGroup></Project>");
        var assets = new Dictionary<string, object>
        {
            ["targets"] = new Dictionary<string, object> { ["net10.0"] = new Dictionary<string, object> { ["Widgets/1.0.0"] = new { compile = new Dictionary<string, object> { ["ref/net10.0/Widgets.dll"] = new { } } } } },
            ["libraries"] = new Dictionary<string, object> { ["Widgets/1.0.0"] = new { type = "package", path = "widgets/1.0.0" } },
            ["packageFolders"] = new Dictionary<string, object> { [packageRoot] = new { } }
        };
        File.WriteAllText(IOPath.Combine(project.DirectoryPath, "obj", "project.assets.json"), System.Text.Json.JsonSerializer.Serialize(assets));
        var catalog = DesignerProjectUserControlDiscovery.DiscoverCatalog(project.ProjectPath);
        Assert.Equal("Widgets.PackageWidget", Assert.Single(catalog.Controls).FullName);
        Assert.Empty(catalog.Diagnostics);
    }

    [Fact]
    public void BinaryNameCollisionsAndIneligibleConstructorsStayOutOfToolbox()
    {
        using var project = new Fixture();
        const string source = "namespace Widgets; public class Widget : ModernFormsNext.UserControl { } public class NeedsArgument : ModernFormsNext.UserControl { public NeedsArgument(int arg) {} }";
        project.BuildReference(source);
        File.WriteAllBytes(IOPath.Combine(project.DirectoryPath, "Other.dll"), Compile(source, "OtherWidgets"));
        File.WriteAllText(project.ProjectPath, "<Project><ItemGroup><Reference Include=\"A\"><HintPath>Referenced.dll</HintPath></Reference><Reference Include=\"B\"><HintPath>Other.dll</HintPath></Reference></ItemGroup></Project>");
        var catalog = DesignerProjectUserControlDiscovery.DiscoverCatalog(project.ProjectPath);
        Assert.Empty(catalog.Controls);
        Assert.Contains(catalog.Diagnostics, d => d.Contains("Ambiguous binary type"));
        Assert.Contains(catalog.Diagnostics, d => d.Contains("public parameterless constructor"));
    }

    private static byte[] Compile(string[] sources, string name)
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(IOPath.PathSeparator)
            .Concat([typeof(Control).Assembly.Location, typeof(DesignDocument).Assembly.Location]).Distinct(StringComparer.OrdinalIgnoreCase);
        var compilation = CSharpCompilation.Create(name, sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            paths.Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return stream.ToArray();
    }

    private sealed class Fixture : IDesignerHostEnvironment, IDisposable
    {
        public string DirectoryPath { get; } = IOPath.Combine(IOPath.GetTempPath(), "MfnMetadata-" + Guid.NewGuid().ToString("N"));
        public string ProjectPath => IOPath.Combine(DirectoryPath, "Consumer.csproj");
        public string BinaryPath => IOPath.Combine(DirectoryPath, "Referenced.dll");
        public string AssemblyName { get; } = "MetadataFixture" + Guid.NewGuid().ToString("N");
        public string? DocumentPath { get; set; }
        public string? CurrentDocumentPath => DocumentPath;
        public string? CurrentProjectPath => ProjectPath;
        public Fixture()
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(ProjectPath, "<Project><ItemGroup><Reference Include=\"Fixture\"><HintPath>Referenced.dll</HintPath></Reference></ItemGroup></Project>");
        }
        public void BuildReference(string source) => File.WriteAllBytes(BinaryPath, Compile(source, AssemblyName));
        public void ReportOutput(string message) { }
        public void ReportStatus(string message) { }
        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}
