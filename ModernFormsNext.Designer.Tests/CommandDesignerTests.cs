using System.ComponentModel;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class CommandDesignerTests
{
    [Theory]
    [InlineData(typeof(Button), "Command")]
    [InlineData(typeof(Button), "CommandParameter")]
    [InlineData(typeof(Button), "CommandTarget")]
    [InlineData(typeof(MenuItem), "Command")]
    [InlineData(typeof(MenuItem), "CommandParameter")]
    [InlineData(typeof(MenuItem), "CommandTarget")]
    [InlineData(typeof(NotifyIconMenuItem), "Command")]
    [InlineData(typeof(NotifyIconMenuItem), "CommandParameter")]
    [InlineData(typeof(NotifyIconMenuItem), "CommandTarget")]
    [InlineData(typeof(Control), "InputBindings")]
    [InlineData(typeof(Control), "CommandBindings")]
    [InlineData(typeof(WindowBase), "InputBindings")]
    [InlineData(typeof(WindowBase), "CommandBindings")]
    public void RuntimeMetadataExcludesCommandsFromBrowsingAndSerialization(Type type, string name)
    {
        var descriptor = TypeDescriptor.GetProperties(type)[name]!;
        Assert.False(descriptor.IsBrowsable);
        Assert.Equal(DesignerSerializationVisibility.Hidden, descriptor.SerializationVisibility);
        var metadata = new DesignMetadataReader().ReadProperty(type.GetProperty(name)!);
        Assert.True(metadata.IsHidden);
        Assert.False(metadata.Serialize);
    }

    [Theory]
    [InlineData("Button")]
    [InlineData("Menu")]
    [InlineData("ToolBar")]
    public void PropertyGridSaveReopenCodegenAndReverseParserKeepRuntimeCommandsOut(string type)
    {
        using var session = new DesignerSession();
        var document = CreateDocument(type);
        session.OpenDocument(document, "Commands.mfdesign");
        session.SelectNode(Assert.Single(document.Controls));
        AssertRuntimePropertiesHidden(session);
        string saved = DesignDocumentSerializer.Default.Serialize(session.Document);
        Assert.DoesNotContain("Command", saved, StringComparison.Ordinal);
        DesignDocument reopened = DesignDocumentSerializer.Default.Deserialize(saved);
        using var reopenedSession = new DesignerSession();
        reopenedSession.OpenDocument(reopened, "Commands.mfdesign");
        reopenedSession.SelectNode(Assert.Single(reopened.Controls));
        AssertRuntimePropertiesHidden(reopenedSession);
        Assert.False(reopenedSession.IsDirty);
        var generator = new CSharpDesignerGenerator();
        var generated = generator.Generate(reopened);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.DoesNotContain(".Command", generated.Code, StringComparison.Ordinal);
        Assert.DoesNotContain(".InputBindings", generated.Code, StringComparison.Ordinal);
        var parsed = new CSharpDesignerParser().Parse(generated.Code);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(d => d.Message)));
        Assert.Equal(generated.Code, generator.Generate(Assert.IsType<DesignDocument>(parsed.Document)).Code);
    }

    [Fact]
    public void CommandCapableControlCopyPasteUndoRedoAndReopenPreserveOnlyDesignModel()
    {
        using var session = new DesignerSession();
        session.OpenDocument(CreateDocument("Button"), "Commands.mfdesign");
        session.SelectNode(Assert.Single(session.Document.Controls));
        Assert.True(session.CopySelectedNode());
        Assert.DoesNotContain("Command", session.Clipboard.Content!, StringComparison.Ordinal);
        session.SelectNode(null);
        Assert.True(session.PasteCopiedNode());
        Assert.Equal(2, session.Document.Controls.Count);
        AssertRuntimePropertiesHidden(session);
        Assert.True(session.Transactions.Undo());
        Assert.Single(session.Document.Controls);
        Assert.True(session.Transactions.Redo());
        Assert.Equal(2, session.Document.Controls.Count);
        string saved = DesignDocumentSerializer.Default.Serialize(session.Document);
        using var reopenedSession = new DesignerSession();
        reopenedSession.OpenDocument(DesignDocumentSerializer.Default.Deserialize(saved), "Commands.mfdesign");
        Assert.Equal(2, reopenedSession.Document.Controls.Count);
        Assert.False(reopenedSession.IsDirty);
        Assert.DoesNotContain("Command", saved, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeAssignmentsRemainOutsideModelAndMetadataInspectionNeverStartsWork()
    {
        using var button = new Button();
        var work = new AsyncCommand(() => { Assert.Fail("Designer started runtime command"); return Task.CompletedTask; });
        button.Command = work;
        button.CommandParameter = "private-runtime-value";
        button.CommandTarget = button;
        foreach (string name in new[] { "Command", "CommandParameter", "CommandTarget" })
            Assert.Equal(DesignerSerializationVisibility.Hidden, TypeDescriptor.GetProperties(button)[name]!.SerializationVisibility);
        using var session = new DesignerSession();
        session.OpenDocument(CreateDocument("Button"), "Commands.mfdesign");
        session.SelectNode(Assert.Single(session.Document.Controls));
        AssertRuntimePropertiesHidden(session);
        Assert.DoesNotContain("private-runtime-value", DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.Null(work.ExecutionTask);
    }

    private static void AssertRuntimePropertiesHidden(DesignerSession session)
    {
        var properties = new DesignerPropertyGridState(session).Properties;
        foreach (string name in new[] { "Command", "CommandParameter", "CommandTarget", "InputBindings", "CommandBindings" })
            Assert.DoesNotContain(properties, property => property.Name == name);
        Assert.Contains(properties, property => property.Name == "Text");
    }

    private static DesignDocument CreateDocument(string type)
    {
        var document = new DesignDocument { Namespace = "Example", ClassName = "MainForm", FormName = "mainForm", Size = new DesignSize(640, 480) };
        document.Controls.Add(new DesignControlNode
        {
            TypeName = type, Name = "action1", Bounds = new DesignBounds(10, 10, 200, 36),
            Properties = { ["Text"] = DesignPropertyValue.FromString("Action") }
        });
        return document;
    }
}
