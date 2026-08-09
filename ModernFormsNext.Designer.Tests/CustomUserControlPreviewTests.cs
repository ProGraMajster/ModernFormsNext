using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using SkiaSharp;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class CustomUserControlPreviewTests
{
    [Fact]
    public void PreviewLoadsChildrenCachesStableFramesAndNeverMutatesSourceDocuments()
    {
        using var project = new TemporaryPreviewProject();
        var sidebar = CreateUserControlDocument("Example", "Sidebar", 300, 200);
        sidebar.Controls.Add(CreateNode("Label", "label1", 12, 16, 120, 24, "Logo"));
        sidebar.Controls.Add(CreateNode("Button", "button1", 12, 52, 120, 32, "Home"));
        var sidebarPath = project.AddUserControl(sidebar);
        var originalSidebarJson = File.ReadAllText(sidebarPath);
        var parent = CreateParentDocument("Example.Sidebar", 40, 50, 300, 200);
        var controls = DesignerProjectUserControlDiscovery.Discover(project.ProjectFilePath);
        var cache = new DesignerEmbeddedPreviewCache(project.ProjectFilePath, controls);

        Assert.True(cache.TryGetPreview("Example.Sidebar", new DesignSize(300, 200), out var first, out var error), error);
        var parseCount = cache.ParseCount;
        Assert.True(cache.TryGetPreview("Example.Sidebar", new DesignSize(300, 200), out var second, out error), error);

        Assert.Same(first, second);
        Assert.Equal(parseCount, cache.ParseCount);
        Assert.Equal(["label1", "button1"], first!.Document.Controls.Select(node => node.Name));

        Assert.True(cache.TryGetPreview("Example.Sidebar", new DesignSize(500, 300), out _, out error), error);
        var multiSizeParseCount = cache.ParseCount;
        Assert.True(cache.TryGetPreview("Example.Sidebar", new DesignSize(300, 200), out var originalSizeAgain, out error), error);
        Assert.Same(first, originalSizeAgain);
        Assert.Equal(multiSizeParseCount, cache.ParseCount);

        Assert.Single(parent.Controls);
        Assert.Empty(parent.Controls[0].Children);

        var parentPath = Path.Combine(project.DirectoryPath, "MainForm.mfdesign");
        var files = new DesignerFileService(new TestDesignerEnvironment(project.ProjectFilePath, parentPath));
        files.SaveDesignDocument(parent);

        Assert.Equal(originalSidebarJson, File.ReadAllText(sidebarPath));
        Assert.DoesNotContain("label1", File.ReadAllText(parentPath), StringComparison.Ordinal);
        Assert.DoesNotContain("button1", File.ReadAllText(parentPath), StringComparison.Ordinal);
    }

    [Fact]
    public void RendererDrawsPreviewWhileHitTestingOutlineAndGenerationStayAtomic()
    {
        using var project = new TemporaryPreviewProject();
        var sidebar = CreateUserControlDocument("Example", "Sidebar", 300, 200);
        sidebar.Controls.Add(CreateNode("Label", "label1", 12, 16, 120, 24, "Logo"));
        sidebar.Controls.Add(CreateNode("Button", "button1", 12, 52, 120, 32, "Home"));
        project.AddUserControl(sidebar);
        var parent = CreateParentDocument("Example.Sidebar", 40, 50, 300, 200);
        var outerNode = Assert.Single(parent.Controls);
        var session = CreateSession(project, parent);

        Render(new DesignerSurfaceRenderer(), session);
        var hit = new DesignerHitTestService(new DesignerCoordinateMapper())
            .HitTestControl(session, new DesignPoint(60, 75));
        session.SelectAt(60, 75);
        var generated = new CSharpDesignerGenerator().Generate(parent);

        Assert.Contains(session.OutputLines, line => line.Contains("Rendered safe custom UserControl preview", StringComparison.Ordinal));
        Assert.Contains(session.OutputLines, line => line.Contains("Runtime rendered label1 as Label", StringComparison.Ordinal));
        Assert.Contains(session.OutputLines, line => line.Contains("Runtime rendered button1 as Button", StringComparison.Ordinal));
        Assert.Same(outerNode, hit.Node);
        Assert.Same(outerNode, session.SelectedNode);
        Assert.Equal([outerNode], session.EnumerateNodes().Select(item => item.Node));
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Contains("new Example.Sidebar()", generated.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("label1", generated.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("button1", generated.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewLayoutUsesInstanceSizeForDockAndAnchor()
    {
        using var project = new TemporaryPreviewProject();
        var sidebar = CreateUserControlDocument("Example", "Sidebar", 300, 200);
        var bottomButton = CreateNode("Button", "bottomButton", 10, 160, 80, 30, "Bottom");
        bottomButton.Properties["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, nameof(DockStyle.Bottom));
        var anchoredLabel = CreateNode("Label", "anchoredLabel", 20, 20, 100, 24, "Anchored");
        anchoredLabel.Properties["Anchor"] = DesignPropertyValue.FromEnum(
            typeof(AnchorStyles).FullName!,
            (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right).ToString());
        var centeredLabel = CreateNode("Label", "centeredLabel", 100, 80, 50, 20, "Centered");
        centeredLabel.Properties["Anchor"] = DesignPropertyValue.FromEnum(
            typeof(AnchorStyles).FullName!,
            nameof(AnchorStyles.None));
        sidebar.Controls.Add(bottomButton);
        sidebar.Controls.Add(anchoredLabel);
        sidebar.Controls.Add(centeredLabel);
        project.AddUserControl(sidebar);
        var cache = new DesignerEmbeddedPreviewCache(
            project.ProjectFilePath,
            DesignerProjectUserControlDiscovery.Discover(project.ProjectFilePath));

        Assert.True(cache.TryGetPreview("Example.Sidebar", new DesignSize(500, 300), out var preview, out var error), error);
        var projectedBottom = preview!.Document.Controls.Single(node => node.Name == "bottomButton");
        var projectedAnchor = preview.Document.Controls.Single(node => node.Name == "anchoredLabel");
        var projectedCenter = preview.Document.Controls.Single(node => node.Name == "centeredLabel");

        Assert.Equal(new DesignBounds(0, 270, 500, 30), preview.Layout.GetEffectiveBounds(projectedBottom));
        Assert.Equal(new DesignBounds(20, 20, 300, 24), preview.Layout.GetEffectiveBounds(projectedAnchor));
        Assert.Equal(new DesignBounds(200, 130, 50, 20), preview.Layout.GetEffectiveBounds(projectedCenter));
    }

    [Fact]
    public void CacheReloadsChangedDesignDocumentOnNextRequest()
    {
        using var project = new TemporaryPreviewProject();
        var sidebar = CreateUserControlDocument("Example", "Sidebar", 300, 200);
        sidebar.Controls.Add(CreateNode("Label", "label1", 10, 10, 100, 24, "One"));
        var path = project.AddUserControl(sidebar);
        var cache = new DesignerEmbeddedPreviewCache(
            project.ProjectFilePath,
            DesignerProjectUserControlDiscovery.Discover(project.ProjectFilePath));

        Assert.True(cache.TryGetPreview("Example.Sidebar", new DesignSize(300, 200), out var first, out var error), error);
        var firstParseCount = cache.ParseCount;
        sidebar.Controls.Add(CreateNode("Button", "buttonWithLongerName", 10, 50, 140, 32, "Reloaded"));
        DesignDocumentSerializer.Default.Save(path, sidebar);

        Assert.True(cache.TryGetPreview("Example.Sidebar", new DesignSize(300, 200), out var reloaded, out error), error);
        Assert.Single(first!.Document.Controls);
        Assert.Equal(2, reloaded!.Document.Controls.Count);
        Assert.True(cache.ParseCount > firstParseCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrInvalidDesignDocumentUsesPlaceholderFallback(bool invalidDocument)
    {
        using var project = new TemporaryPreviewProject();
        var sourceDocument = CreateUserControlDocument("Example", "Sidebar", 300, 200);
        project.AddUserControlSource(sourceDocument.Namespace, sourceDocument.ClassName);

        if (invalidDocument)
            File.WriteAllText(Path.Combine(project.DirectoryPath, "Sidebar.mfdesign"), "{ invalid json");

        var session = CreateSession(project, CreateParentDocument("Example.Sidebar", 40, 50, 300, 200));
        var exception = Record.Exception(() => Render(new DesignerSurfaceRenderer(), session));

        Assert.Null(exception);
        Assert.Contains(session.OutputLines, line => line.Contains("preview fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EmptyDesignDocumentUsesPlaceholderFallback()
    {
        using var project = new TemporaryPreviewProject();
        project.AddUserControlSource("Example", "Sidebar");
        File.WriteAllText(Path.Combine(project.DirectoryPath, "Sidebar.mfdesign"), string.Empty);
        var session = CreateSession(project, CreateParentDocument("Example.Sidebar", 40, 50, 300, 200));

        var exception = Record.Exception(() => Render(new DesignerSurfaceRenderer(), session));

        Assert.Null(exception);
        Assert.Contains(session.OutputLines, line => line.Contains("preview fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidDocumentFailureIsCachedAndReloadsAfterFileChanges()
    {
        using var project = new TemporaryPreviewProject();
        project.AddUserControlSource("Example", "Sidebar");
        var path = Path.Combine(project.DirectoryPath, "Sidebar.mfdesign");
        File.WriteAllText(path, "{ invalid json");
        var cache = new DesignerEmbeddedPreviewCache(
            project.ProjectFilePath,
            DesignerProjectUserControlDiscovery.Discover(project.ProjectFilePath));

        Assert.False(cache.TryGetPreview("Example.Sidebar", new DesignSize(300, 200), out _, out _));
        var invalidParseCount = cache.ParseCount;
        Assert.False(cache.TryGetPreview("Example.Sidebar", new DesignSize(300, 200), out _, out _));
        Assert.Equal(invalidParseCount, cache.ParseCount);

        var repaired = CreateUserControlDocument("Example", "Sidebar", 300, 200);
        repaired.Controls.Add(CreateNode("Label", "repairedLabel", 10, 10, 100, 24, "Repaired"));
        DesignDocumentSerializer.Default.Save(path, repaired);

        Assert.True(cache.TryGetPreview("Example.Sidebar", new DesignSize(300, 200), out var preview, out var error), error);
        Assert.Equal("repairedLabel", Assert.Single(preview!.Document.Controls).Name);
        Assert.True(cache.ParseCount > invalidParseCount);
    }

    [Fact]
    public void PreviewNormalizesMissingOptionalPropertyCollections()
    {
        using var project = new TemporaryPreviewProject();
        project.AddUserControlSource("Example", "Sidebar");
        File.WriteAllText(
            Path.Combine(project.DirectoryPath, "Sidebar.mfdesign"),
            """
            {
              "namespace": "Example",
              "className": "Sidebar",
              "rootKind": "userControl",
              "formName": "Sidebar",
              "size": { "width": 300, "height": 200 },
              "controls": [
                {
                  "typeName": "Label",
                  "name": "minimalLabel",
                  "bounds": { "x": 10, "y": 10, "width": 100, "height": 24 }
                }
              ]
            }
            """);
        var session = CreateSession(project, CreateParentDocument("Example.Sidebar", 40, 50, 300, 200));

        Render(new DesignerSurfaceRenderer(), session);

        Assert.Contains(session.OutputLines, line => line.Contains("Runtime rendered minimalLabel as Label", StringComparison.Ordinal));
        Assert.Contains(session.OutputLines, line => line.Contains("Rendered safe custom UserControl preview", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("OldNamespace", "Sidebar")]
    [InlineData("Example", "OldSidebar")]
    public void StaleNamespaceOrClassIdentityUsesPlaceholderFallback(
        string documentNamespace,
        string documentClassName)
    {
        using var project = new TemporaryPreviewProject();
        project.AddUserControlSource("Example", "Sidebar");
        var staleDocument = CreateUserControlDocument(documentNamespace, documentClassName, 300, 200);
        staleDocument.Controls.Add(CreateNode("Label", "staleLabel", 10, 10, 100, 24, "Stale"));
        DesignDocumentSerializer.Default.Save(
            Path.Combine(project.DirectoryPath, "Sidebar.mfdesign"),
            staleDocument);
        var session = CreateSession(project, CreateParentDocument("Example.Sidebar", 40, 50, 300, 200));

        Render(new DesignerSurfaceRenderer(), session);

        Assert.Contains(session.OutputLines, line => line.Contains("preview fallback", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(session.OutputLines, line => line.Contains("Runtime rendered staleLabel", StringComparison.Ordinal));
    }

    [Fact]
    public void FormRootDocumentCannotBeUsedAsCustomUserControlPreview()
    {
        using var project = new TemporaryPreviewProject();
        project.AddUserControlSource("Example", "Sidebar");
        var formDocument = CreateUserControlDocument("Example", "Sidebar", 300, 200);
        formDocument.RootKind = DesignRootKind.Form;
        DesignDocumentSerializer.Default.Save(
            Path.Combine(project.DirectoryPath, "Sidebar.mfdesign"),
            formDocument);
        var session = CreateSession(project, CreateParentDocument("Example.Sidebar", 40, 50, 300, 200));

        Render(new DesignerSurfaceRenderer(), session);

        Assert.Contains(session.OutputLines, line => line.Contains("does not declare a UserControl root", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownPreviewChildFallsBackWithoutCrashing()
    {
        using var project = new TemporaryPreviewProject();
        var sidebar = CreateUserControlDocument("Example", "Sidebar", 300, 200);
        sidebar.Controls.Add(CreateNode("Missing.Controls.UnknownWidget", "unknownWidget1", 10, 10, 160, 80, "Unknown"));
        project.AddUserControl(sidebar);
        var session = CreateSession(project, CreateParentDocument("Example.Sidebar", 40, 50, 300, 200));

        var exception = Record.Exception(() => Render(new DesignerSurfaceRenderer(), session));

        Assert.Null(exception);
        Assert.Contains(session.OutputLines, line => line.Contains("Rendered safe custom UserControl preview", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RecursivePreviewUsesLocalPlaceholderFallbackWithoutOverflow(int cycleLength)
    {
        using var project = new TemporaryPreviewProject();
        var names = Enumerable.Range(0, cycleLength).Select(index => $"Control{(char)('A' + index)}").ToArray();

        for (var index = 0; index < names.Length; index++)
        {
            var document = CreateUserControlDocument("Example", names[index], 200, 120);
            var target = names[(index + 1) % names.Length];
            document.Controls.Add(CreateNode($"Example.{target}", $"{char.ToLowerInvariant(target[0])}{target[1..]}1", 10, 10, 160, 80, target));
            project.AddUserControl(document);
        }

        var session = CreateSession(project, CreateParentDocument("Example.ControlA", 20, 20, 200, 120));
        var exception = Record.Exception(() => Render(new DesignerSurfaceRenderer(), session));

        Assert.Null(exception);
        Assert.Contains(session.OutputLines, line => line.Contains("preview cycle detected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NestedValidUserControlPreviewRendersRecursively()
    {
        using var project = new TemporaryPreviewProject();
        var card = CreateUserControlDocument("Example", "UserCard", 180, 80);
        card.Controls.Add(CreateNode("Label", "cardLabel", 10, 10, 120, 24, "User"));
        project.AddUserControl(card);
        var sidebar = CreateUserControlDocument("Example", "Sidebar", 240, 180);
        sidebar.Controls.Add(CreateNode("Example.UserCard", "userCard1", 20, 20, 180, 80, string.Empty));
        project.AddUserControl(sidebar);
        var session = CreateSession(project, CreateParentDocument("Example.Sidebar", 20, 20, 240, 180));

        Render(new DesignerSurfaceRenderer(), session);

        Assert.Contains(session.OutputLines, line => line.Contains("(Example.Sidebar)", StringComparison.Ordinal));
        Assert.Contains(session.OutputLines, line => line.Contains("(Example.UserCard)", StringComparison.Ordinal));
        Assert.DoesNotContain(session.OutputLines, line => line.Contains("cycle detected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreviewNeverExecutesResolvableCustomUserControlConstructor()
    {
        ConstructorTrapUserControl.ConstructorCalls = 0;
        using var project = new TemporaryPreviewProject();
        var document = CreateUserControlDocument(
            typeof(ConstructorTrapUserControl).Namespace!,
            nameof(ConstructorTrapUserControl),
            200,
            120);
        document.Controls.Add(CreateNode("Label", "safeLabel", 10, 10, 120, 24, "Safe"));
        project.AddUserControl(document);
        var parent = CreateParentDocument(
            typeof(ConstructorTrapUserControl).AssemblyQualifiedName!,
            20,
            20,
            200,
            120);
        var session = CreateSession(project, parent);

        Render(new DesignerSurfaceRenderer(), session);
        session.Host.Selection.Select(Assert.Single(parent.Controls));
        var propertyGrid = new DesignerPropertyGridState(session);

        Assert.Equal(0, ConstructorTrapUserControl.ConstructorCalls);
        Assert.Contains(session.OutputLines, line => line.Contains("Rendered safe custom UserControl preview", StringComparison.Ordinal));
        Assert.Contains(nameof(ConstructorTrapUserControl), propertyGrid.HeaderType, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeRendererRejectsCustomAssemblyControlEvenWhenTypeIsResolvable()
    {
        ConstructorTrapUserControl.ConstructorCalls = 0;
        var session = new DesignerSession();
        session.LoadDocument(CreateParentDocument(
            typeof(ConstructorTrapUserControl).AssemblyQualifiedName!,
            20,
            20,
            200,
            120));

        var exception = Record.Exception(() => Render(new DesignerSurfaceRenderer(), session));

        Assert.Null(exception);
        Assert.Equal(0, ConstructorTrapUserControl.ConstructorCalls);
        Assert.Contains(session.OutputLines, line => line.Contains("Could not safely create framework control", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeRendererDoesNotResolveExternalAssembliesForPreview()
    {
        var assemblyName = $"ModernFormsNext.PreviewTrap.{Guid.NewGuid():N}";
        var resolutionAttempts = 0;
        Assembly? Resolver(AssemblyLoadContext _, AssemblyName requestedName)
        {
            if (string.Equals(requestedName.Name, assemblyName, StringComparison.Ordinal))
                resolutionAttempts++;

            return null;
        }

        AssemblyLoadContext.Default.Resolving += Resolver;
        try
        {
            var session = new DesignerSession();
            session.LoadDocument(CreateParentDocument(
                $"Example.ExternalUserControl, {assemblyName}",
                20,
                20,
                200,
                120));

            Render(new DesignerSurfaceRenderer(), session);
        }
        finally
        {
            AssemblyLoadContext.Default.Resolving -= Resolver;
        }

        Assert.Equal(0, resolutionAttempts);
    }

    [Fact]
    public void OrdinaryFormRenderingStillUsesExistingRuntimePath()
    {
        var document = new DesignDocument
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "MainForm",
            Size = new DesignSize(640, 400)
        };
        document.Controls.Add(CreateNode("Button", "button1", 20, 20, 120, 36, "Save"));
        var session = new DesignerSession();
        session.LoadDocument(document);

        var exception = Record.Exception(() => Render(new DesignerSurfaceRenderer(), session));

        Assert.Null(exception);
        Assert.Contains(session.OutputLines, line => line.Contains("Runtime rendered button1 as Button", StringComparison.Ordinal));
        Assert.DoesNotContain(session.OutputLines, line => line.Contains("custom UserControl preview", StringComparison.OrdinalIgnoreCase));
    }

    private static DesignerSession CreateSession(TemporaryPreviewProject project, DesignDocument document)
    {
        var session = new DesignerSession(new TestDesignerEnvironment(project.ProjectFilePath));
        session.LoadDocument(document);
        return session;
    }

    private static DesignDocument CreateParentDocument(
        string typeName,
        int x,
        int y,
        int width,
        int height)
    {
        var document = new DesignDocument
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "MainForm",
            Size = new DesignSize(800, 500)
        };
        document.Controls.Add(CreateNode(typeName, "customUserControl1", x, y, width, height, string.Empty));
        return document;
    }

    private static DesignDocument CreateUserControlDocument(
        string namespaceName,
        string className,
        int width,
        int height)
        => new()
        {
            Namespace = namespaceName,
            ClassName = className,
            RootKind = DesignRootKind.UserControl,
            FormName = className,
            Size = new DesignSize(width, height)
        };

    private static DesignControlNode CreateNode(
        string typeName,
        string name,
        int x,
        int y,
        int width,
        int height,
        string text)
        => new()
        {
            TypeName = typeName,
            Name = name,
            Bounds = new DesignBounds(x, y, width, height),
            Properties =
            {
                ["Text"] = DesignPropertyValue.FromString(text)
            }
        };

    private static void Render(DesignerSurfaceRenderer renderer, DesignerSession session)
    {
        const int width = 1000;
        const int height = 720;
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        renderer.Render(new PaintEventArgs(info, canvas, scaling: 1), session, width, height);
    }

    private sealed class TestDesignerEnvironment(
        string projectPath,
        string? currentDocumentPath = null) : IDesignerHostEnvironment
    {
        public string? CurrentDocumentPath => currentDocumentPath;

        public string? CurrentProjectPath => projectPath;

        public void ReportOutput(string message)
        {
        }

        public void ReportStatus(string message)
        {
        }
    }

    private sealed class TemporaryPreviewProject : IDisposable
    {
        public TemporaryPreviewProject()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), $"ModernFormsNextPreviewTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
            ProjectFilePath = Path.Combine(DirectoryPath, "Example.csproj");
            File.WriteAllText(ProjectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        }

        public string DirectoryPath { get; }

        public string ProjectFilePath { get; }

        public string AddUserControl(DesignDocument document)
        {
            AddUserControlSource(document.Namespace, document.ClassName);
            var path = Path.Combine(DirectoryPath, $"{document.ClassName}.mfdesign");
            DesignDocumentSerializer.Default.Save(path, document);
            return path;
        }

        public void AddUserControlSource(string namespaceName, string className)
        {
            File.WriteAllText(
                Path.Combine(DirectoryPath, $"{className}.cs"),
                $$"""
                using ModernFormsNext;

                namespace {{namespaceName}};

                public partial class {{className}} : UserControl
                {
                }
                """);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}

public sealed class ConstructorTrapUserControl : UserControl
{
    public ConstructorTrapUserControl()
    {
        ConstructorCalls++;
        throw new InvalidOperationException("The custom UserControl constructor must not run in design-time preview.");
    }

    public static int ConstructorCalls { get; set; }
}
