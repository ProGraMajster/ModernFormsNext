using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designing;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.Designer.Surface;

namespace ModernFormsNext.Designer.Tests;

public sealed class InheritedDesignerTests
{
    [Fact]
    public void DerivedDesignKeepsOneTreeAndPersistsOnlyLocalChanges()
    {
        using var project = new Project();
        var document = project.Derived();
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);
        Assert.True(document.InheritanceState is not null, string.Join(Environment.NewLine, session.OutputLines));
        var host = Assert.Single(document.Controls, node => node.Name == "hostPanel");
        var locked = Assert.Single(document.Controls, node => node.Name == "privateLabel");
        Assert.True(host.IsInherited);
        Assert.False(host.IsInheritanceLocked);
        Assert.True(locked.IsInheritanceLocked);
        Assert.Equal(DesignRootKind.Form, document.RootKind);
        host.Properties["Text"] = DesignPropertyValue.FromString("Derived text");
        host.Bounds = new(20, 30, 420, 160);
        host.Children.Add(new() { Name = "localButton", TypeName = "Button", Bounds = new(8, 9, 100, 30) });

        var json = DesignDocumentSerializer.Default.Serialize(document);
        Assert.DoesNotContain("privateLabel", json);
        Assert.DoesNotContain("Base private text", json);
        var saved = DesignDocumentSerializer.Default.Deserialize(json);
        var patch = Assert.Single(saved.Controls);
        Assert.Equal("Example.BaseForm", patch.InheritedFrom);
        Assert.Equal(new DesignBounds(20, 30, 420, 160), patch.InheritedBounds);
        using var reopened = new DesignerSession(project);
        reopened.OpenDocument(saved, project.CurrentDocumentPath);
        Assert.Equal(2, saved.Controls.Count);
        var reopenedHost = saved.Controls.Single(node => node.Name == "hostPanel");
        Assert.Equal("Derived text", reopenedHost.Properties["Text"].Value);
        Assert.Equal("localButton", Assert.Single(reopenedHost.Children).Name);

        var generated = new CSharpDesignerGenerator().Generate(saved);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.DoesNotContain("new Panel", generated.Code);
        Assert.DoesNotContain("privateLabel", generated.Code);
        Assert.DoesNotContain("this.Controls.Add(this.hostPanel)", generated.Code);
        Assert.Contains("this.hostPanel.Controls.Add(this.localButton)", generated.Code);
        var parsed = new CSharpDesignerParser().Parse(generated.Code);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        var regenerated = new CSharpDesignerGenerator().Generate(parsed.Document!);
        Assert.True(regenerated.Succeeded, string.Join(Environment.NewLine, regenerated.Validation.Errors));
        Assert.Equal(generated.Code, regenerated.Code);
        Assert.Equal(project.BaseJson, File.ReadAllText(project.BasePath));
    }

    [Fact]
    public void LockedInheritedMutationCannotBeSilentlySavedOrGenerated()
    {
        using var project = new Project();
        var document = project.Derived();
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);
        var locked = document.Controls.Single(node => node.Name == "privateLabel");
        locked.Properties["Text"] = DesignPropertyValue.FromString("Local change");

        Assert.Throws<InvalidOperationException>(() => DesignDocumentSerializer.Default.Serialize(document));
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);
        Assert.Equal(project.BaseJson, File.ReadAllText(project.BasePath));
    }

    [Fact]
    public void MissingBaseKeepsAuthoredPatchAndBlocksCodeReplacement()
    {
        using var project = new Project();
        File.Delete(project.BasePath);
        var document = project.Derived();
        document.Controls.Add(new()
        {
            Name = "hostPanel", TypeName = "Panel", InheritedFrom = "Example.BaseForm",
            Properties = new() { ["Text"] = DesignPropertyValue.FromString("Derived text") }
        });
        var before = DesignDocumentSerializer.Default.Serialize(document);
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);

        Assert.Equal(before, DesignDocumentSerializer.Default.Serialize(document));
        Assert.Contains(session.OutputLines, line => line.Contains("Inherited design unavailable", StringComparison.Ordinal));
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
    }

    [Fact]
    public void InheritanceFormatProtectsOwnershipFromOlderReadersAndDowngrades()
    {
        using var project = new Project();
        var document = project.Derived();
        Assert.Equal(1, document.Metadata.FormatVersion);
        var json = DesignDocumentSerializer.Default.Serialize(document);
        Assert.Equal(1, document.Metadata.FormatVersion);
        var loaded = DesignDocumentSerializer.Default.DeserializeWithDiagnostics(json);
        Assert.Equal(DesignDocumentSerializer.InheritedFormatVersion, loaded.FormatVersion);
        Assert.Empty(loaded.Diagnostics);
        Assert.Equal(json, DesignDocumentSerializer.Default.Serialize(loaded.Document));
        var downgraded = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        downgraded["metadata"]!["formatVersion"] = DesignDocumentSerializer.CurrentFormatVersion;
        var error = Assert.Throws<System.Text.Json.JsonException>(() => DesignDocumentSerializer.Default.Deserialize(downgraded.ToJsonString()));
        Assert.Equal("$.metadata.formatVersion", error.Path);
        Assert.Contains("cannot be downgraded", error.Message);
        Assert.Equal(1, DesignDocumentSerializer.Default.Load(project.BasePath).Metadata.FormatVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingProjectOrDerivedSourceBlocksGenerationWithoutLosingPatches(bool missingProject)
    {
        using var project = new Project();
        project.WriteSource("DerivedForm.cs", "namespace Example; public partial class RenamedForm : BaseForm { }");
        var document = project.Derived();
        var json = DesignDocumentSerializer.Default.Serialize(document);
        using var session = new DesignerSession(missingProject ? null : project);
        session.OpenDocument(document, missingProject ? null : project.CurrentDocumentPath);
        Assert.Null(document.InheritanceState);
        Assert.Equal(json, DesignDocumentSerializer.Default.Serialize(document));
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
    }

    [Fact]
    public void InvalidReferenceImageProducesAnInheritanceDiagnostic()
    {
        using var project = new Project();
        project.WriteSource("Example.csproj", "<Project><ItemGroup><Reference Include=\"Unavailable\"><HintPath>Unavailable.dll</HintPath></Reference></ItemGroup></Project>");
        project.WriteSource("Unavailable.dll", "metadata unavailable");
        using var session = new DesignerSession(project);
        var document = project.Derived();
        session.OpenDocument(document, project.CurrentDocumentPath);
        Assert.Contains(session.OutputLines, line => line.Contains("metadata", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Inherited design unavailable", StringComparison.Ordinal));
        Assert.Equal(project.BaseJson, File.ReadAllText(project.BasePath));
    }

    [Fact]
    public void GridAndCommandsProtectInheritedMembersAndAllowLocalChildren()
    {
        using var project = new Project();
        var document = project.Derived();
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);
        var inherited = document.Controls.Single(node => node.Name == "hostPanel");
        var locked = document.Controls.Single(node => node.Name == "privateLabel");
        session.SelectNode(inherited);
        var grid = new DesignerPropertyGridState(session);
        Assert.True(Assert.Single(grid.Properties, property => property.Name == "Name").IsReadOnly);
        Assert.False(Assert.Single(grid.Properties, property => property.Name == "Text").IsReadOnly);
        Assert.False(session.DeleteNode(inherited));
        Assert.False(session.CopySelectedNode());
        Assert.Throws<InvalidOperationException>(() => session.SetNodeName(inherited, "renamed"));
        var local = session.AddControl("Button");
        Assert.Same(local, Assert.Single(inherited.Children));
        Assert.False(local.IsInherited);
        Assert.True(session.Transactions.Undo());
        Assert.Empty(inherited.Children);
        Assert.True(session.Transactions.Redo());
        Assert.Same(local, Assert.Single(inherited.Children));
        session.SelectNode(locked);
        grid.Refresh();
        Assert.All(grid.Properties, property => Assert.True(property.IsReadOnly));
        Assert.Throws<InvalidOperationException>(() => session.SetNodeBounds(locked, new(1, 2, 50, 30)));
        Assert.False(session.MoveSelectedNodes(1, 2));
        session.SelectForm();
        grid.Refresh();
        Assert.Contains(grid.Properties, property => property.Name == "Caption" && !property.IsReadOnly);
        Assert.Contains(grid.Events, item => item.Name == "Submitted" && item.CanBind);
        Assert.Contains("hostPanel.Controls.Add", new CSharpDesignerGenerator().Generate(document).Code);
    }

    [Fact]
    public void RootResizeReflowsLockedMembersWithoutPersistingTheirBoundsAndSupportsUndo()
    {
        using var project = new Project();
        var document = project.Derived();
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);
        var original = DesignDocumentSerializer.Default.Serialize(document);
        var locked = document.Controls.Single(node => node.Name == "privateLabel");
        var initialBounds = locked.Bounds;
        session.ResizeDesignRoot(new(800, 600));
        Assert.Equal(initialBounds.X + 160, locked.Bounds.X);
        Assert.Equal(initialBounds.Y + 120, locked.Bounds.Y);
        var saved = DesignDocumentSerializer.Default.Serialize(document);
        Assert.DoesNotContain("privateLabel", saved);
        Assert.Equal(new DesignSize(800, 600), DesignDocumentSerializer.Default.Deserialize(saved).InheritedSize);
        using var reopened = new DesignerSession(project);
        var restored = DesignDocumentSerializer.Default.Deserialize(saved);
        reopened.OpenDocument(restored, project.CurrentDocumentPath);
        Assert.True(restored.InheritanceState is not null, string.Join(Environment.NewLine, reopened.OutputLines));
        Assert.Equal(locked.Bounds, restored.Controls.Single(node => node.Name == "privateLabel").Bounds);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(initialBounds, locked.Bounds);
        Assert.Equal(original, DesignDocumentSerializer.Default.Serialize(document));
    }

    [Theory]
    [InlineData(DesignRootKind.Form, false)]
    [InlineData(DesignRootKind.Form, true)]
    [InlineData(DesignRootKind.UserControl, false)]
    [InlineData(DesignRootKind.UserControl, true)]
    public void MultiLevelInheritancePreservesOwnershipAndInheritedMetadata(DesignRootKind kind, bool abstractBase)
    {
        using var project = new Project(kind, abstractBase);
        project.WriteSource("MiddleForm.cs", """
            using ModernFormsNext;
            namespace Example;
            public partial class MiddleForm : BaseForm { protected Button middleButton = null!; }
            """);
        project.WriteSource("DerivedForm.cs", "namespace Example; public partial class DerivedForm : MiddleForm { }");
        var middle = project.Derived();
        middle.ClassName = "MiddleForm";
        middle.Controls.Add(new()
        {
            Name = "hostPanel", TypeName = "Panel", InheritedFrom = "Example.BaseForm",
            Properties = new() { ["Text"] = DesignPropertyValue.FromString("Middle text") },
            Events = new() { ["Click"] = "BaseClick" }
        });
        middle.Controls.Insert(0, new() { Name = "middleButton", TypeName = "Button", MemberVisibility = DesignerMemberVisibility.Protected });
        project.SaveDesign("MiddleForm.mfdesign", middle);
        var document = project.Derived();
        document.BaseTypeName = "global::Example.MiddleForm";
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);
        Assert.True(document.InheritanceState is not null, string.Join(Environment.NewLine, session.OutputLines));
        Assert.Equal(kind, document.RootKind);
        Assert.Equal(3, document.Controls.Count);
        var host = document.Controls.Single(node => node.Name == "hostPanel");
        Assert.Equal("Middle text", host.Properties["Text"].Value);
        Assert.Empty(host.Events); // The middle constructor owns its own subscription.
        Assert.Equal("Example.MiddleForm", document.Controls.Single(node => node.Name == "middleButton").InheritedFrom);
        session.SelectForm();
        var grid = new DesignerPropertyGridState(session);
        Assert.Contains(grid.Properties, property => property.Name == "Caption" && !property.IsReadOnly);
        Assert.Contains(grid.Events, item => item.Name == "Submitted" && item.CanBind);
        if (abstractBase) Assert.Contains(session.OutputLines, line => line.Contains("Abstract base layout", StringComparison.Ordinal));
        host.Events["Click"] = "DerivedClick";
        document.Properties["Caption"] = DesignPropertyValue.FromString("Derived caption");
        document.Events["Submitted"] = "DerivedSubmitted";
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Contains("this.hostPanel.Click += this.DerivedClick", generated.Code);
        Assert.Contains("this.Submitted += this.DerivedSubmitted", generated.Code);
        Assert.DoesNotContain("BaseClick", generated.Code);
        Assert.DoesNotContain("middleButton", generated.Code);
        var parsed = new CSharpDesignerParser().Parse(generated.Code, new() { RootKind = kind });
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        Assert.Equal(generated.Code, new CSharpDesignerGenerator().Generate(parsed.Document!).Code);
        var saved = DesignDocumentSerializer.Default.Serialize(document);
        using var reopened = new DesignerSession(project);
        var restored = DesignDocumentSerializer.Default.Deserialize(saved);
        reopened.OpenDocument(restored, project.CurrentDocumentPath);
        Assert.Equal(3, restored.Controls.Count);
        Assert.Equal(saved, DesignDocumentSerializer.Default.Serialize(restored));
        Assert.Equal(project.BaseJson, File.ReadAllText(project.BasePath));
    }

    [Theory]
    [InlineData("namespace Example; public partial class BaseForm : object { }")]
    [InlineData("namespace Example; public sealed partial class BaseForm : ModernFormsNext.Form { }")]
    [InlineData("namespace Example; public partial class BaseForm : ModernFormsNext.Form { private BaseForm() { } }")]
    public void IncompatibleSourcePreservesAuthoredDataAndBlocksGeneration(string source)
    {
        using var project = new Project();
        project.WriteSource("BaseForm.cs", source);
        var document = project.Derived();
        var before = DesignDocumentSerializer.Default.Serialize(document);
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);
        Assert.Null(document.InheritanceState);
        Assert.Equal(before, DesignDocumentSerializer.Default.Serialize(document));
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
        Assert.Contains(session.OutputLines, line => line.Contains("Inherited design unavailable", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("protected ModernFormsNext.Button hostPanel = null!;")]
    [InlineData("protected ModernFormsNext.Panel hostPanel { private get; set; } = null!;")]
    public void MissingOrIncompatibleMemberStaysVisibleAndLocked(string member)
    {
        using var project = new Project();
        project.WriteSource("BaseForm.cs", $$"""
            namespace Example;
            public partial class BaseForm : ModernFormsNext.Form { {{member}} }
            """);
        var document = project.Derived();
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);
        var inherited = document.Controls.Single(node => node.Name == "hostPanel");
        Assert.True(inherited.IsInheritanceLocked);
        Assert.Contains(session.OutputLines, line => line.Contains("hostPanel", StringComparison.Ordinal) && line.Contains("locked", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => session.SetPropertyValue(inherited, "Text", DesignPropertyValue.FromString("Local text")));
        Assert.True(new CSharpDesignerGenerator().Generate(document).Succeeded);
        Assert.DoesNotContain("hostPanel", DesignDocumentSerializer.Default.Serialize(document));
    }

    [Fact]
    public void ShadowedMembersCannotRedirectDerivedOverrides()
    {
        using var project = new Project();
        project.WriteSource("DerivedForm.cs", """
            namespace Example;
            public partial class DerivedForm : BaseForm { protected new ModernFormsNext.Panel hostPanel = null!; }
            """);
        using var session = new DesignerSession(project);
        var document = project.Derived();
        session.OpenDocument(document, project.CurrentDocumentPath);
        Assert.True(document.Controls.Single(node => node.Name == "hostPanel").IsInheritanceLocked);
    }

    [Fact]
    public void StructuredOverridesDoNotMutateTheInheritedBaseline()
    {
        using var project = new Project();
        var basis = DesignDocumentSerializer.Default.Load(project.BasePath);
        basis.Controls[0].Properties["Padding"] = DesignPropertyValue.FromStructuredObject("ModernFormsNext.Padding",
            new Dictionary<string, DesignPropertyValue> { ["All"] = DesignPropertyValue.FromInt32(4) });
        project.SaveDesign("BaseForm.mfdesign", basis);
        using var session = new DesignerSession(project);
        var document = project.Derived();
        session.OpenDocument(document, project.CurrentDocumentPath);
        var host = document.Controls.Single(node => node.Name == "hostPanel");
        var values = Assert.IsAssignableFrom<IDictionary<string, DesignPropertyValue>>(host.Properties["Padding"].ObjectProperties);
        values["All"] = DesignPropertyValue.FromInt32(8);
        var patch = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));
        Assert.Equal(8, Assert.Single(patch.Controls).Properties["Padding"].ObjectProperties!["All"].Value);
        Assert.Equal(4, host.InheritanceState!.Properties["Padding"].ObjectProperties!["All"].Value);
    }

    [Fact]
    public void ExcessMetadataMembersAndCombinedBaseNodesAreDeferred()
    {
        using var project = new Project();
        project.WriteSource("LargeControl.cs", "public class LargeControl : ModernFormsNext.UserControl { "
            + string.Join(" ", Enumerable.Range(0, 2049).Select(index => $"public int Value{index} {{ get; set; }}")) + " }");
        var catalog = DesignerProjectUserControlDiscovery.DiscoverCatalog(project.CurrentProjectPath);
        Assert.Contains(catalog.Diagnostics, message => message.Contains("member budget", StringComparison.Ordinal));
        project.WriteSource("LargeControl.cs", "// The metadata regression has finished.");
        var basis = DesignDocumentSerializer.Default.Load(project.BasePath);
        for (int index = basis.Controls.Count; index <= 2048; index++)
            basis.Controls.Add(new() { TypeName = "Label", Name = $"baseLabel{index}" });
        project.SaveDesign("BaseForm.mfdesign", basis);
        using var session = new DesignerSession(project);
        var document = project.Derived();
        session.OpenDocument(document, project.CurrentDocumentPath);
        Assert.Null(document.InheritanceState);
        Assert.Contains(session.OutputLines, message => message.Contains("graph budget", StringComparison.Ordinal));
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
    }

    [Fact]
    public void ComplexEventMetadataRemainsNonExecutingAndUnavailable()
    {
        using var project = new Project();
        var parameters = string.Join(", ", Enumerable.Range(0, 33).Select(index => $"int value{index}"));
        string nested = "int";
        for (int index = 0; index < 33; index++) nested = $"System.Collections.Generic.List<{nested}>";
        project.WriteSource("SignatureControl.cs", $$"""
            namespace Example;
            public class SignatureControl : ModernFormsNext.UserControl
            {
                public delegate void ManyParameters({{parameters}});
                public event ManyParameters? Changed;
                public event System.Action<{{nested}}>? NestedChanged;
            }
            """);
        var catalog = DesignerProjectUserControlDiscovery.DiscoverCatalog(project.CurrentProjectPath);
        var control = Assert.Single(catalog.Controls, item => item.FullName == "Example.SignatureControl");
        Assert.Equal(2, control.Events.Count);
        Assert.All(control.Events, item => Assert.Null(item.Parameters));
        Assert.Contains(catalog.Diagnostics, message => message.Contains("unsupported delegate signature", StringComparison.Ordinal));
    }

    [Fact]
    public void DirectInheritedRemovalAndReorderingCannotBeSilentlyPersisted()
    {
        using var project = new Project();
        var document = project.Derived();
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);
        var first = document.Controls[0];
        document.Controls.RemoveAt(0);
        Assert.Throws<InvalidOperationException>(() => DesignDocumentSerializer.Default.Serialize(document));
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
        document.Controls.Add(first);
        Assert.Throws<InvalidOperationException>(() => DesignDocumentSerializer.Default.Serialize(document));
    }

    [Fact]
    public void RemovingTheBaseIdentityCannotTurnInheritedControlsIntoLocalCode()
    {
        using var project = new Project();
        var document = project.Derived();
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);
        document.BaseTypeName = null;
        Assert.Throws<InvalidOperationException>(() => DesignDocumentSerializer.Default.Serialize(document));
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Example.Missing Name")]
    [InlineData("Example.BaseForm<int>")]
    public void InvalidInheritanceTypeNamesAreRejectedAsData(string name)
    {
        using var project = new Project();
        var document = project.Derived();
        document.BaseTypeName = name;
        Assert.False(new DesignDocumentValidator().ValidateStructure(document).IsValid);
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);
    }

    [Fact]
    public void InheritedGeometryRejectsOverflowBeforeHydration()
    {
        using var project = new Project();
        var document = project.Derived();
        document.Controls.Add(new()
        {
            Name = "hostPanel", TypeName = "Panel", InheritedFrom = "Example.BaseForm",
            InheritedBounds = new(int.MaxValue, 0, 10, 20)
        });
        Assert.False(new DesignDocumentValidator().ValidateStructure(document).IsValid);
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
    }

    [Fact]
    public void OversizedSourceAndBaseCompanionProduceBoundedFallbacks()
    {
        using var project = new Project(DesignRootKind.UserControl);
        project.SaveDesign("DerivedForm.mfdesign", project.Derived());
        var control = new DesignerProjectUserControlInfo("DerivedForm", "Example.DerivedForm",
            IOPath.ChangeExtension(project.CurrentDocumentPath, ".cs"));
        using (var stream = File.Create(project.BasePath))
            stream.SetLength(DesignDocumentSerializer.MaximumContentLength * 4L + 1);
        var cache = new DesignerEmbeddedPreviewCache(project.CurrentProjectPath, [control]);
        Assert.False(cache.TryGetPreview(control.FullName, new(640, 480), out _, out var error));
        Assert.Contains("budget", error);
        project.WriteSource("BaseForm.cs", new string(' ', DesignerEventSource.MaximumSourceCharacters + 1));
        var catalog = DesignerProjectUserControlDiscovery.DiscoverCatalog(project.CurrentProjectPath);
        Assert.Contains(catalog.Diagnostics, message => message.Contains("budget", StringComparison.Ordinal));
        using var session = new DesignerSession(project);
        var document = project.Derived();
        session.OpenDocument(document, project.CurrentDocumentPath);
        Assert.Null(document.InheritanceState);
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
    }

    [Fact]
    public void DerivedGeneratedSourceCompilesWithoutRedeclaringBaseMembers()
    {
        using var project = new Project();
        project.WriteSource("DerivedForm.cs", """
            namespace Example;
            public partial class DerivedForm : BaseForm
            {
                private void OnHostClick(object? sender, System.EventArgs args) { }
                private void OnSubmitted(object? sender, System.EventArgs args) { }
            }
            """);
        var document = project.Derived();
        using var session = new DesignerSession(project);
        session.OpenDocument(document, project.CurrentDocumentPath);
        var host = document.Controls.Single(node => node.Name == "hostPanel");
        host.Events["Click"] = "OnHostClick";
        host.Children.Add(new() { Name = "localButton", TypeName = "Button" });
        document.Events["Submitted"] = "OnSubmitted";
        document.Properties["Caption"] = DesignPropertyValue.FromString("Derived caption");
        session.SelectForm();
        session.UpdateSelectedProperties("DerivedName", "Derived title", 0, 0, 800, 600, DesignerMemberVisibility.Private);
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Contains("this.Name = \"DerivedName\"", generated.Code);
        Assert.Contains("this.Text = \"Derived title\"", generated.Code);
        var compilation = DesignerProjectUserControlDiscovery.CreateEventCompilation(
            CSharpSyntaxTree.ParseText(generated.Code, path: IOPath.ChangeExtension(project.CurrentDocumentPath, ".Designer.cs")),
            project.CurrentProjectPath);
        using var output = new MemoryStream();
        var emitted = compilation.Emit(output);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
    }

    [Fact]
    public void InheritedUserControlPreviewUsesDataOnlyCompositionAndPrivateLayouts()
    {
        using var project = new Project(DesignRootKind.UserControl);
        project.SaveDesign("DerivedForm.mfdesign", project.Derived());
        var controls = DesignerProjectUserControlDiscovery.Discover(project.CurrentProjectPath);
        var cache = new DesignerEmbeddedPreviewCache(project.CurrentProjectPath, controls);
        Assert.True(cache.TryGetPreview("Example.DerivedForm", new(640, 480), out var first, out var error), error);
        Assert.Equal(2, first!.Document.Controls.Count);
        Assert.All(first.Document.Controls, node => Assert.True(node.IsInherited));
        Assert.True(cache.TryGetPreview("Example.DerivedForm", new(800, 600), out var second, out error), error);
        Assert.NotSame(first.Document, second!.Document);
        Assert.NotSame(first.Document.Controls[0], second.Document.Controls[0]);
        Assert.Equal(project.BaseJson, File.ReadAllText(project.BasePath));
    }

    private sealed class Project : IDesignerHostEnvironment, IDisposable
    {
        private readonly string root = Directory.CreateTempSubdirectory("mfn-inheritance-").FullName;
        private readonly HashSet<string> ownedFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly DesignRootKind kind;
        internal Project(DesignRootKind kind = DesignRootKind.Form, bool abstractBase = false)
        {
            this.kind = kind;
            WriteSource("Example.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            WriteSource("BaseForm.cs", $$"""
                using ModernFormsNext;
                namespace Example;
                public {{(abstractBase ? "abstract " : "")}}partial class BaseForm : {{(kind == DesignRootKind.Form ? "Form" : "UserControl")}}
                {
                    protected Panel hostPanel = null!;
                    private Label privateLabel = null!;
                    public string Caption { get; set; } = "";
                    public event System.EventHandler? Submitted;
                }
                """);
            WriteSource("DerivedForm.cs", """
                namespace Example;
                public partial class DerivedForm : BaseForm { }
                """);
            var basis = new DesignDocument
            {
                Namespace = "Example", ClassName = "BaseForm", FormName = "Base form", Size = new(640, 480), RootKind = kind,
                Controls =
                [
                    new() { Name = "hostPanel", TypeName = "Panel", MemberVisibility = DesignerMemberVisibility.Protected,
                        Bounds = new(10, 10, 400, 180), Properties = new() { ["Text"] = DesignPropertyValue.FromString("Base host") } },
                    new() { Name = "privateLabel", TypeName = "Label", Bounds = new(10, 200, 140, 28),
                        Properties = new() { ["Text"] = DesignPropertyValue.FromString("Base private text"),
                            ["Anchor"] = DesignPropertyValue.FromEnum("ModernFormsNext.AnchorStyles", "Right, Bottom") } }
                ]
            };
            SaveDesign("BaseForm.mfdesign", basis);
            BaseJson = File.ReadAllText(BasePath);
        }
        internal string BasePath => IOPath.Combine(root, "BaseForm.mfdesign");
        internal string BaseJson { get; }
        public string CurrentProjectPath => IOPath.Combine(root, "Example.csproj");
        public string CurrentDocumentPath => IOPath.Combine(root, "DerivedForm.mfdesign");
        public void ReportStatus(string message) { }
        public void ReportOutput(string message) { }
        internal DesignDocument Derived() => new()
        {
            Namespace = "Example", ClassName = "DerivedForm", FormName = "Derived form",
            BaseTypeName = "Example.BaseForm", Size = new(640, 480), RootKind = kind
        };
        internal void WriteSource(string name, string text)
        {
            ownedFiles.Add(name);
            File.WriteAllText(IOPath.Combine(root, name), text);
        }
        internal void SaveDesign(string name, DesignDocument document)
        {
            ownedFiles.Add(name);
            DesignDocumentSerializer.Default.Save(IOPath.Combine(root, name), document);
        }
        public void Dispose()
        {
            // Delete only the explicit files created by this fixture.
            foreach (var file in ownedFiles)
                File.Delete(IOPath.Combine(root, file));
            Directory.Delete(root);
        }
    }
}
