using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using ModernFormsNext.VisualStudioExtension.Detection;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class UserControlDesignerTests
{
    [Fact]
    public void LegacyDocumentWithoutRootKindLoadsAsForm()
    {
        const string json = """
            {
              "metadata": { "formatVersion": 1 },
              "namespace": "Example",
              "className": "MainForm",
              "formName": "MainForm",
              "size": { "width": 900, "height": 600 },
              "controls": []
            }
            """;

        var document = DesignDocumentSerializer.Default.Deserialize(json);

        Assert.Equal(DesignRootKind.Form, document.RootKind);
        Assert.DoesNotContain("rootKind", DesignDocumentSerializer.Default.Serialize(document), StringComparison.Ordinal);
    }

    [Fact]
    public void UserControlDocumentRoundTripsRootPropertiesAndNestedChildren()
    {
        var document = CreateUserControlDocument("NavigationPanel");
        document.Properties["Padding"] = DesignPropertyValue.FromStructuredObject(
            typeof(Padding).FullName!,
            new Dictionary<string, DesignPropertyValue>
            {
                ["Left"] = DesignPropertyValue.FromInt32(8),
                ["Top"] = DesignPropertyValue.FromInt32(8),
                ["Right"] = DesignPropertyValue.FromInt32(8),
                ["Bottom"] = DesignPropertyValue.FromInt32(8)
            });
        var panel = new DesignControlNode
        {
            TypeName = "Panel",
            Name = "panel1",
            Bounds = new DesignBounds(12, 12, 240, 120)
        };
        panel.Children.Add(new DesignControlNode
        {
            TypeName = "Label",
            Name = "label1",
            Bounds = new DesignBounds(8, 8, 100, 24)
        });
        document.Controls.Add(panel);

        var json = DesignDocumentSerializer.Default.Serialize(document);
        var reopened = DesignDocumentSerializer.Default.Deserialize(json);

        Assert.Contains("\"rootKind\": \"userControl\"", json, StringComparison.Ordinal);
        Assert.Equal(DesignRootKind.UserControl, reopened.RootKind);
        Assert.True(reopened.Properties.ContainsKey("Padding"));
        Assert.Equal(new DesignSize(480, 320), reopened.Size);
        Assert.Equal("label1", Assert.Single(Assert.Single(reopened.Controls).Children).Name);
    }

    [Fact]
    public void GeneratorUsesSharedPartialClassShapeForUserControl()
    {
        var document = CreateUserControlDocument("NavigationPanel");
        document.Properties["Text"] = DesignPropertyValue.FromString("Navigation");
        document.Controls.Add(new DesignControlNode
        {
            TypeName = "Button",
            Name = "button1",
            Bounds = new DesignBounds(20, 24, 120, 36)
        });

        var result = new CSharpDesignerGenerator().Generate(document);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Validation.Errors));
        Assert.Contains("public partial class NavigationPanel", result.Code, StringComparison.Ordinal);
        Assert.Contains("this.button1 = new Button();", result.Code, StringComparison.Ordinal);
        Assert.Contains("this.Controls.Add(this.button1);", result.Code, StringComparison.Ordinal);
        Assert.Contains("this.Name = \"NavigationPanel\";", result.Code, StringComparison.Ordinal);
        Assert.Contains("this.Size = new System.Drawing.Size(480, 320);", result.Code, StringComparison.Ordinal);
        Assert.Contains("this.Text = \"Navigation\";", result.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("this.Text = \"NavigationPanel\";", result.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void ReverseParserPreservesUserControlRootKindAndProperties()
    {
        const string source = """
            using ModernFormsNext;

            namespace Example;

            public partial class NavigationPanel
            {
                private void InitializeComponent()
                {
                    this.Name = "NavigationPanel";
                    this.Size = new System.Drawing.Size(480, 320);
                    this.AutoScroll = true;
                }
            }
            """;

        var result = new CSharpDesignerParser().Parse(
            source,
            new CSharpDesignerParseOptions { RootKind = DesignRootKind.UserControl });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var document = Assert.IsType<DesignDocument>(result.Document);
        Assert.Equal(DesignRootKind.UserControl, document.RootKind);
        Assert.Equal(new DesignSize(480, 320), document.Size);
        Assert.True(Assert.IsType<bool>(document.Properties["AutoScroll"].Value));
    }

    [Fact]
    public void ImportDesignerCodePreservesActiveUserControlRootKind()
    {
        using var project = TemporaryDesignerProject.Create("namespace Example; public class Placeholder { }");
        var designerPath = IOPath.Combine(project.DirectoryPath, "NavigationPanel.Designer.cs");
        File.WriteAllText(
            designerPath,
            """
            using ModernFormsNext;

            namespace Example;

            public partial class NavigationPanel
            {
                private void InitializeComponent()
                {
                    this.Name = "NavigationPanel";
                    this.Size = new System.Drawing.Size(480, 320);
                }
            }
            """);
        var session = new DesignerSession();
        session.LoadDocument(CreateUserControlDocument("NavigationPanel"));
        var commands = new DesignerCommandService(
            session,
            new DesignerFileService(),
            new ModernFormsDesignerOptions());

        commands.ImportDesignerCode(designerPath);

        Assert.Equal(DesignRootKind.UserControl, session.Document.RootKind);
        Assert.Equal(new DesignSize(480, 320), session.Document.Size);
    }

    [Fact]
    public void ValidatorRejectsDirectSelfReferenceWithoutRegressingFormGeneration()
    {
        var userControl = CreateUserControlDocument("NavigationPanel");
        userControl.Controls.Add(new DesignControlNode
        {
            TypeName = "Example.NavigationPanel",
            Name = "navigationPanel1",
            Bounds = new DesignBounds(0, 0, 100, 100)
        });

        var invalid = new DesignDocumentValidator().Validate(userControl);
        var formResult = new CSharpDesignerGenerator().Generate(new DesignDocument
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "MainForm",
            Size = new DesignSize(900, 600)
        });

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Errors, error => error.Contains("cannot contain the design root type", StringComparison.Ordinal));
        Assert.True(formResult.Succeeded, string.Join(Environment.NewLine, formResult.Validation.Errors));
        Assert.Contains("this.Text = \"MainForm\";", formResult.Code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DesignRootKind.Form)]
    [InlineData(DesignRootKind.UserControl)]
    public void ProjectUserControlsAppearInToolboxAndStayAtomicInParentDesigner(DesignRootKind parentRootKind)
    {
        using var project = TemporaryDesignerProject.Create(
            """
            using ModernFormsNext;

            namespace Example;

            public partial class Sidebar : UserControl
            {
            }
            """);
        var environment = new TestDesignerEnvironment(project.ProjectFilePath);
        var session = new DesignerSession(environment);
        session.LoadDocument(new DesignDocument
        {
            Namespace = "Example",
            ClassName = parentRootKind == DesignRootKind.Form ? "MainForm" : "DashboardView",
            FormName = parentRootKind == DesignRootKind.Form ? "MainForm" : "DashboardView",
            RootKind = parentRootKind,
            Size = new DesignSize(900, 600)
        });

        var item = Assert.Single(
            new DesignerToolboxService().GetItems(session.ProjectUserControls),
            candidate => candidate.TypeName == "Example.Sidebar");
        var node = session.AddControl(item.TypeName);

        Assert.Equal("My Project", item.Category);
        Assert.Equal("Example.Sidebar", node.TypeName);
        Assert.False(session.IsContainerNode(node));

        var generated = new CSharpDesignerGenerator().Generate(session.Document);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Contains("this.sidebar1 = new Example.Sidebar();", generated.Code, StringComparison.Ordinal);

        node.Children.Add(new DesignControlNode
        {
            TypeName = "Label",
            Name = "implementationDetail",
            Bounds = new DesignBounds(4, 4, 40, 20)
        });
        session.SelectAt(node.Bounds.X + 8, node.Bounds.Y + 8);
        Assert.Same(node, session.SelectedNode);
    }

    [Fact]
    public void UserControlRootCanBeResizedButCannotBeDeleted()
    {
        var session = new DesignerSession();
        session.LoadDocument(CreateUserControlDocument("NavigationPanel"));
        session.SelectForm();
        var surface = new Panel { Width = 1000, Height = 800 };
        var mapper = new DesignerCoordinateMapper();
        var view = mapper.GetView(session, surface.Width, surface.Height);
        var hitTest = new DesignerHitTestService(mapper);

        var handle = hitTest.HitTestResizeHandle(
            session,
            surface.Width,
            surface.Height,
            view.FormX + view.FormWidth,
            view.FormY + view.FormHeight);

        var controller = new DesignerMouseController(session);
        controller.HandleMouseDown(surface, MouseAt(view.FormX + view.FormWidth, view.FormY + view.FormHeight));
        controller.HandleMouseMove(surface, MouseAt(view.FormX + view.FormWidth + 15, view.FormY + view.FormHeight + 8));
        controller.HandleMouseMove(surface, MouseAt(view.FormX + view.FormWidth + 30, view.FormY + view.FormHeight + 14));
        controller.HandleMouseMove(surface, MouseAt(view.FormX + view.FormWidth + 40, view.FormY + view.FormHeight + 20));
        controller.HandleMouseUp(surface, MouseAt(view.FormX + view.FormWidth + 40, view.FormY + view.FormHeight + 20));

        Assert.Equal(0, view.TitleHeight);
        Assert.Equal(DesignerResizeHandle.BottomRight, handle);
        Assert.Equal(new DesignSize(520, 340), session.Document.Size);
        Assert.False(session.DeleteSelectedNode());
    }

    [Theory]
    [InlineData(DesignRootKind.Form, "ModernFormsNext.Form", "StartPosition", "Dock")]
    [InlineData(DesignRootKind.UserControl, "ModernFormsNext.UserControl", "Dock", "StartPosition")]
    public void RootPropertyGridSharesCommonDescriptorsAndKeepsRootSpecificProperties(
        DesignRootKind rootKind,
        string expectedType,
        string expectedSpecificProperty,
        string unexpectedSpecificProperty)
    {
        var session = new DesignerSession();
        var document = CreateUserControlDocument("RootControl");
        document.RootKind = rootKind;
        session.LoadDocument(document);

        var propertyGrid = new DesignerPropertyGridState(session);

        Assert.Equal(expectedType, propertyGrid.HeaderType);
        Assert.Single(propertyGrid.Properties, property => property.Name == "Name");
        Assert.Single(propertyGrid.Properties, property => property.Name == "Namespace");
        Assert.Single(propertyGrid.Properties, property => property.Name == "ClassName");
        Assert.Contains(propertyGrid.Properties, property => property.Name == expectedSpecificProperty);
        Assert.DoesNotContain(propertyGrid.Properties, property => property.Name == unexpectedSpecificProperty);
    }

    [Fact]
    public void UserControlRootPropertyGridPersistsSupportedLayoutPropertiesAndGeneratesAssignments()
    {
        var session = new DesignerSession();
        session.LoadDocument(CreateUserControlDocument("RootControl"));
        var propertyGrid = new DesignerPropertyGridState(session);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Padding"] = "1, 2, 3, 4",
            ["Margin"] = "5, 6, 7, 8",
            ["Dock"] = "Fill",
            ["Anchor"] = "Top, Left, Right",
            ["MinimumSize"] = "120, 80",
            ["MaximumSize"] = "800, 600"
        };

        foreach (var (propertyName, value) in values)
        {
            var property = Assert.Single(propertyGrid.Properties, candidate => candidate.Name == propertyName);
            propertyGrid.SelectRow(new DesignerPropertyGridRow(property));
            Assert.True(propertyGrid.CommitSelectedValue(value));
        }

        var json = DesignDocumentSerializer.Default.Serialize(session.Document);
        var reopened = DesignDocumentSerializer.Default.Deserialize(json);
        var generated = new CSharpDesignerGenerator().Generate(reopened);

        Assert.All(values.Keys, propertyName => Assert.True(reopened.Properties.ContainsKey(propertyName)));
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Contains("this.Padding = new Padding(1, 2, 3, 4);", generated.Code, StringComparison.Ordinal);
        Assert.Contains("this.Margin = new Padding(5, 6, 7, 8);", generated.Code, StringComparison.Ordinal);
        Assert.Contains("this.Dock = ModernFormsNext.DockStyle.Fill;", generated.Code, StringComparison.Ordinal);
        Assert.Contains("this.Anchor = ModernFormsNext.AnchorStyles.Top | ModernFormsNext.AnchorStyles.Left | ModernFormsNext.AnchorStyles.Right;", generated.Code, StringComparison.Ordinal);
        Assert.Contains("this.MinimumSize = new System.Drawing.Size(120, 80);", generated.Code, StringComparison.Ordinal);
        Assert.Contains("this.MaximumSize = new System.Drawing.Size(800, 600);", generated.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("StartPosition", generated.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowState", generated.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void DesignerCommandRejectsDirectSelfReferenceWithoutChangingDocument()
    {
        using var project = TemporaryDesignerProject.Create(
            "using ModernFormsNext; namespace Example; public class RootControl : UserControl { }");
        var session = new DesignerSession(new TestDesignerEnvironment(project.ProjectFilePath));
        session.LoadDocument(CreateUserControlDocument("RootControl"));
        var commands = new DesignerCommandService(
            session,
            new DesignerFileService(),
            new ModernFormsDesignerOptions());

        var exception = Record.Exception(() => commands.AddControlType("Example.RootControl"));

        Assert.Null(exception);
        Assert.Empty(session.Document.Controls);
        Assert.Contains(session.OutputLines, line => line.Contains("cannot contain itself", StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectReferenceGuardRejectsObviousTransitiveCycle()
    {
        using var project = TemporaryDesignerProject.Create(
            """
            using ModernFormsNext;

            namespace Example;

            public partial class ControlA : UserControl { }
            public partial class ControlB : UserControl { }
            """);
        var controlA = CreateUserControlDocument("ControlA");
        var controlB = CreateUserControlDocument("ControlB");
        controlB.Controls.Add(new DesignControlNode
        {
            TypeName = "Example.ControlA",
            Name = "controlA1",
            Bounds = new DesignBounds(0, 0, 100, 100)
        });
        DesignDocumentSerializer.Default.Save(IOPath.Combine(project.DirectoryPath, "ControlB.mfdesign"), controlB);

        var allowed = DesignerControlReferenceGuard.CanReference(
            controlA,
            "Example.ControlB",
            project.ProjectFilePath,
            out var error);

        Assert.False(allowed);
        Assert.Contains("cyclic UserControl reference", Assert.IsType<string>(error), StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectDiscoveryRejectsFalseAndUnconstructableTypesButFollowsProjectBases()
    {
        using var project = TemporaryDesignerProject.Create(
            """
            using ModernFormsNext;
            using WinForms = System.Windows.Forms;

            namespace Example
            {
                namespace Components
                {
                    public abstract class AbstractBase : UserControl { }
                    public class ConcreteControl : AbstractBase { }
                    public class GenericBase<T> : UserControl { }
                    public class ClosedControl : GenericBase<int> { }
                    public class OpenControl<T> : UserControl { }
                    internal class InternalControl : UserControl { }
                    public class Container
                    {
                        public class NestedControl : UserControl { }
                    }
                    public class WindowsControl : WinForms.UserControl { }
                    public class ConstructorMustNotRun : UserControl
                    {
                        public ConstructorMustNotRun() => throw new System.InvalidOperationException("design-time execution");
                    }
                }
            }

            namespace Unrelated
            {
                public class UserControl { }
                public class FalsePositive : UserControl { }
            }
            """);
        foreach (var excludedDirectoryName in new[] { "bin", "obj", "artifacts", ".git", ".vs" })
        {
            var excludedDirectory = IOPath.Combine(project.DirectoryPath, excludedDirectoryName);
            Directory.CreateDirectory(excludedDirectory);
            File.WriteAllText(
                IOPath.Combine(excludedDirectory, $"Hidden{excludedDirectoryName.TrimStart('.')}Control.cs"),
                $"using ModernFormsNext; public class Hidden{excludedDirectoryName.TrimStart('.')}Control : UserControl {{ }}");
        }

        var controls = DesignerProjectUserControlDiscovery.Discover(project.ProjectFilePath);

        Assert.Contains(controls, control => control.FullName == "Example.Components.ConcreteControl");
        Assert.Contains(controls, control => control.FullName == "Example.Components.ClosedControl");
        Assert.Contains(controls, control => control.FullName == "Example.Components.ConstructorMustNotRun");
        Assert.DoesNotContain(controls, control => control.Name is "AbstractBase"
            or "GenericBase"
            or "OpenControl"
            or "InternalControl"
            or "NestedControl"
            or "WindowsControl"
            or "FalsePositive"
            || control.Name.StartsWith("Hidden", StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectReferenceGuardIgnoresMissingAndBrokenUnrelatedDocuments()
    {
        using var project = TemporaryDesignerProject.Create(
            """
            using ModernFormsNext;

            namespace Example;

            public class ControlA : UserControl { }
            public class ControlB : UserControl { }
            """);
        File.WriteAllText(IOPath.Combine(project.DirectoryPath, "Empty.mfdesign"), string.Empty);
        File.WriteAllText(
            IOPath.Combine(project.DirectoryPath, "StructurallyBroken.mfdesign"),
            """{ "className": "Broken", "controls": null }""");
        var controlA = CreateUserControlDocument("ControlA");

        var allowed = DesignerControlReferenceGuard.CanReference(
            controlA,
            "Example.ControlB",
            project.ProjectFilePath,
            out var error);

        Assert.True(allowed, error);
        Assert.Null(error);
    }

    [Fact]
    public void ProjectReferenceGuardRejectsLongerTransitiveCycle()
    {
        using var project = TemporaryDesignerProject.Create(
            """
            using ModernFormsNext;

            namespace Example;

            public class ControlA : UserControl { }
            public class ControlB : UserControl { }
            public class ControlC : UserControl { }
            """);
        var controlA = CreateUserControlDocument("ControlA");
        var controlB = CreateUserControlDocument("ControlB");
        var controlC = CreateUserControlDocument("ControlC");
        controlB.Controls.Add(CreateControlReference("Example.ControlC", "controlC1"));
        controlC.Controls.Add(CreateControlReference("Example.ControlA", "controlA1"));
        DesignDocumentSerializer.Default.Save(IOPath.Combine(project.DirectoryPath, "ControlB.mfdesign"), controlB);
        DesignDocumentSerializer.Default.Save(IOPath.Combine(project.DirectoryPath, "ControlC.mfdesign"), controlC);

        var allowed = DesignerControlReferenceGuard.CanReference(
            controlA,
            "Example.ControlB",
            project.ProjectFilePath,
            out var error);

        Assert.False(allowed);
        Assert.Contains("cyclic UserControl reference", Assert.IsType<string>(error), StringComparison.Ordinal);
    }

    [Fact]
    public void VisualStudioDetectorPreservesDeclaredClassAndNestedNamespace()
    {
        using var project = TemporaryDesignerProject.Create(
            """
            using ModernFormsNext;

            namespace Example
            {
                namespace Components
                {
                    public partial class NavigationPanel : UserControl
                    {
                        public NavigationPanel() => InitializeComponent();
                    }
                }
            }
            """);
        File.WriteAllText(
            project.ProjectFilePath,
            """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="ModernFormsNext" Version="1.9.0" /></ItemGroup></Project>""");
        var sourcePath = IOPath.Combine(project.DirectoryPath, "Controls.cs");

        var result = new ModernFormsDesignableFileDetector().Inspect(sourcePath);

        var fileInfo = Assert.IsType<ModernFormsDesignableFileInfo>(result);
        Assert.True(fileInfo.IsDesignable);
        Assert.Equal("NavigationPanel", fileInfo.ClassName);
        Assert.Equal("Example.Components", fileInfo.Namespace);
        Assert.Equal(DesignRootKind.UserControl, fileInfo.RootKind);
    }

    [Theory]
    [InlineData("internal", "")]
    [InlineData("public abstract", "")]
    [InlineData("public", "<T>")]
    public void VisualStudioDetectorDoesNotAutoExposeUnsupportedUserControlRoots(
        string modifiers,
        string typeParameters)
    {
        using var project = TemporaryDesignerProject.Create(
            $$"""
            using ModernFormsNext;

            namespace Example;

            {{modifiers}} partial class UnsupportedControl{{typeParameters}} : UserControl { }
            """);
        File.WriteAllText(
            project.ProjectFilePath,
            """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="ModernFormsNext" Version="1.9.0" /></ItemGroup></Project>""");

        var result = new ModernFormsDesignableFileDetector().Inspect(
            IOPath.Combine(project.DirectoryPath, "Controls.cs"));

        Assert.False(Assert.IsType<ModernFormsDesignableFileInfo>(result).IsDesignable);
    }

    [Fact]
    public void OpeningUserControlSynchronizesRenamedClassAndNamespaceWithoutChangingRuntimeName()
    {
        using var project = TemporaryDesignerProject.Create(
            """
            using ModernFormsNext;

            namespace NewNamespace;

            public partial class RenamedControl : UserControl { }
            """);
        var designPath = IOPath.Combine(project.DirectoryPath, "Controls.mfdesign");
        var environment = new TestDesignerEnvironment(project.ProjectFilePath, designPath);
        var session = new DesignerSession(environment);
        var document = CreateUserControlDocument("OldControl");
        document.Namespace = "OldNamespace";
        document.FormName = "StableRuntimeName";

        session.LoadDocument(document);

        Assert.Equal(DesignRootKind.UserControl, session.Document.RootKind);
        Assert.Equal("RenamedControl", session.Document.ClassName);
        Assert.Equal("NewNamespace", session.Document.Namespace);
        Assert.Equal("StableRuntimeName", session.Document.FormName);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void ReopeningDesignerRefreshesRenamedAndDeletedProjectUserControls()
    {
        using var project = TemporaryDesignerProject.Create(
            "using ModernFormsNext; namespace OldNamespace; public class OldControl : UserControl { }");

        var firstSession = new DesignerSession(new TestDesignerEnvironment(project.ProjectFilePath));
        Assert.Contains(firstSession.ProjectUserControls, control => control.FullName == "OldNamespace.OldControl");

        File.WriteAllText(
            IOPath.Combine(project.DirectoryPath, "Controls.cs"),
            "using ModernFormsNext; namespace NewNamespace; public class RenamedControl : UserControl { }");
        var reopenedSession = new DesignerSession(new TestDesignerEnvironment(project.ProjectFilePath));

        Assert.DoesNotContain(reopenedSession.ProjectUserControls, control => control.FullName == "OldNamespace.OldControl");
        Assert.Contains(reopenedSession.ProjectUserControls, control => control.FullName == "NewNamespace.RenamedControl");

        File.Delete(IOPath.Combine(project.DirectoryPath, "Controls.cs"));
        var afterDeletion = new DesignerSession(new TestDesignerEnvironment(project.ProjectFilePath));
        var parent = new DesignDocument
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "MainForm",
            Size = new DesignSize(900, 600)
        };
        parent.Controls.Add(CreateControlReference("NewNamespace.RenamedControl", "renamedControl1"));

        var exception = Record.Exception(() => afterDeletion.LoadDocument(parent));

        Assert.Null(exception);
        Assert.Empty(afterDeletion.ProjectUserControls);
        Assert.DoesNotContain(
            new DesignerToolboxService().GetItems(afterDeletion.ProjectUserControls),
            item => item.Category == "My Project");
        Assert.Null(afterDeletion.ResolveControlType(parent.Controls[0]));
    }

    [Fact]
    public void DesignerGenerationDoesNotModifyUserCodeFile()
    {
        using var project = TemporaryDesignerProject.Create("namespace Example; public class Placeholder { }");
        var designPath = IOPath.Combine(project.DirectoryPath, "NavigationPanel.mfdesign");
        var userCodePath = IOPath.Combine(project.DirectoryPath, "NavigationPanel.cs");
        const string userCode = """
            using ModernFormsNext;

            namespace Example;

            public partial class NavigationPanel : UserControl
            {
                public NavigationPanel() => InitializeComponent();

                public string UserOwnedValue => "preserve me";
            }
            """;
        File.WriteAllText(userCodePath, userCode);
        var files = new DesignerFileService(
            new TestDesignerEnvironment(project.ProjectFilePath, designPath));
        var document = CreateUserControlDocument("NavigationPanel");

        var savedPath = files.SaveDesignDocument(document);
        var generated = files.GenerateDesignerCode(document);

        Assert.Equal(designPath, savedPath);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Errors));
        Assert.Equal(IOPath.Combine(project.DirectoryPath, "NavigationPanel.Designer.cs"), generated.Path);
        Assert.Equal(userCode, File.ReadAllText(userCodePath));
    }

    private static DesignDocument CreateUserControlDocument(string className)
        => new()
        {
            Namespace = "Example",
            ClassName = className,
            RootKind = DesignRootKind.UserControl,
            FormName = className,
            Size = new DesignSize(480, 320)
        };

    private static DesignControlNode CreateControlReference(string typeName, string name)
        => new()
        {
            TypeName = typeName,
            Name = name,
            Bounds = new DesignBounds(0, 0, 100, 100)
        };

    private static MouseEventArgs MouseAt(int x, int y)
        => new(MouseButtons.Left, clicks: 1, x, y, System.Drawing.Point.Empty);

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

    private sealed class TemporaryDesignerProject : IDisposable
    {
        private TemporaryDesignerProject(string directoryPath, string projectFilePath)
        {
            DirectoryPath = directoryPath;
            ProjectFilePath = projectFilePath;
        }

        public string DirectoryPath { get; }

        public string ProjectFilePath { get; }

        public static TemporaryDesignerProject Create(string source)
        {
            var directory = IOPath.Combine(IOPath.GetTempPath(), $"ModernFormsNextDesignerTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var projectPath = IOPath.Combine(directory, "Example.csproj");
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(IOPath.Combine(directory, "Controls.cs"), source);
            return new TemporaryDesignerProject(directory, projectPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
