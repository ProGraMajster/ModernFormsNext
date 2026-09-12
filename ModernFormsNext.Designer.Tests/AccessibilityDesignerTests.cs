using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using ModernFormsNext.Accessibility;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class AccessibilityDesignerTests
{
    [Theory]
    [InlineData(nameof(Control.AccessibleName))]
    [InlineData(nameof(Control.AccessibleDescription))]
    [InlineData(nameof(Control.AccessibleAutomationId))]
    [InlineData(nameof(Control.AccessibleDefaultActionDescription))]
    [InlineData(nameof(Control.AccessibleRole))]
    [InlineData(nameof(Control.AccessibleControlType))]
    [InlineData(nameof(Control.AccessibilityView))]
    public void SimpleAccessibilityPropertiesHaveDiscoverableSerializableMetadata(string name)
    {
        var descriptor = TypeDescriptor.GetProperties(typeof(Control))[name]!;
        Assert.True(descriptor.IsBrowsable);
        Assert.Equal("Accessibility", descriptor.Category);
        var metadata = new DesignMetadataReader().ReadProperty(typeof(Control).GetProperty(name)!);
        Assert.False(metadata.IsHidden);
        Assert.True(metadata.Serialize);
        Assert.False(metadata.ReadOnly);
        Assert.Equal("Accessibility", metadata.Category);
        using var control = new Control();
        Assert.Equal(((DefaultValueAttribute)descriptor.Attributes[typeof(DefaultValueAttribute)]!).Value, descriptor.GetValue(control));
    }

    [Theory]
    [InlineData(typeof(Control), nameof(Control.AccessibilityObject))]
    [InlineData(typeof(AccessibleObject), nameof(AccessibleObject.TextProvider))]
    public void RuntimePeersAndTextProvidersStayOutsideTheDesignModel(Type type, string name)
    {
        var descriptor = TypeDescriptor.GetProperties(type)[name]!;
        Assert.False(descriptor.IsBrowsable);
        Assert.Equal(DesignerSerializationVisibility.Hidden, descriptor.SerializationVisibility);
        var metadata = new DesignMetadataReader().ReadProperty(type.GetProperty(name)!);
        Assert.True(metadata.IsHidden);
        Assert.False(metadata.Serialize);
    }

    [Theory]
    [InlineData("LinkLabel", AccessibleControlType.Hyperlink)]
    [InlineData("NumericUpDown", AccessibleControlType.Spinner)]
    [InlineData("DataGridView", AccessibleControlType.DataGrid)]
    [InlineData("Control", AccessibleControlType.DataItem)]
    [InlineData("Control", AccessibleControlType.Header)]
    [InlineData("Control", AccessibleControlType.HeaderItem)]
    [InlineData("DateTimePicker", AccessibleControlType.Calendar)]
    public void PropertyGridSaveCodegenReverseAndActualRuntimePreviewPreserveAccessibility(string type, AccessibleControlType semanticType)
    {
        using var session = new DesignerSession();
        var document = CreateDocument(type);
        session.OpenDocument(document, "Accessible.mfdesign");
        session.SelectNode(Assert.Single(document.Controls));
        var properties = new DesignerPropertyGridState(session).Properties;
        Assert.DoesNotContain(properties, p => p.Name == nameof(Control.AccessibilityObject));
        var typeProperty = Assert.Single(properties, p => p.Name == nameof(Control.AccessibleControlType));
        Assert.Contains(semanticType.ToString(), typeProperty.StandardValues!);
        Assert.True(typeProperty.TryCommit(semanticType.ToString(), out var error), error);
        string json = DesignDocumentSerializer.Default.Serialize(session.Document);
        Assert.DoesNotContain("TextProvider", json);
        Assert.DoesNotContain("RuntimeId", json);
        var reopened = DesignDocumentSerializer.Default.Deserialize(json);
        var generator = new CSharpDesignerGenerator();
        var generated = generator.Generate(reopened);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Contains($"AccessibleControlType.{semanticType}", generated.Code);
        Assert.Contains("AccessibleName", generated.Code);
        var parsed = new CSharpDesignerParser().Parse(generated.Code);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(d => d.Message)));
        Assert.Equal(generated.Code, generator.Generate(Assert.IsType<DesignDocument>(parsed.Document)).Code);

        var node = Assert.Single(reopened.Controls);
        using var preview = Assert.IsAssignableFrom<Control>(Activator.CreateInstance(typeof(Control).Assembly.GetType($"ModernFormsNext.{type}")!));
        preview.Bounds = new Rectangle(0, 0, 240, 90);
        var errors = new List<string>();
        // Exercise the same private conversion/application seam used by the production renderer;
        // do not duplicate its enum/string mapping in a test-side preview implementation.
        var apply = typeof(DesignerSurfaceRenderer).GetMethod("ApplyNodeProperties", BindingFlags.NonPublic | BindingFlags.Static)!;
        apply.Invoke(null, [preview, node, errors]);
        Assert.Empty(errors);
        Assert.Equal(semanticType, preview.AccessibleControlType);
        Assert.Equal("Open details", preview.AccessibilityObject.Name);
        Assert.Equal("Details for the selected item", preview.AccessibilityObject.Description);
        Assert.Equal("details", preview.AccessibilityObject.AutomationId);
        Assert.Equal(AccessibilityView.Content, preview.AccessibilityView);
        var info = new SKImageInfo(260, 110);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        Assert.True(RuntimeControlPainter.TryPaint(new PaintEventArgs(info, canvas, 1), preview,
            preview.Size, new Rectangle(0, 0, 240, 90), out _, out error), error);
        Assert.Equal("Open details", preview.AccessibilityObject.Name);
    }

    [Fact]
    public void ClipboardUndoRedoAndReopenPreserveAuthoredEmptyNameWithoutMaterializingPeers()
    {
        using var session = new DesignerSession();
        var document = CreateDocument("TextBox");
        document.Controls[0].Properties[nameof(Control.AccessibleName)] = DesignPropertyValue.FromString("");
        session.OpenDocument(document, "Accessible.mfdesign");
        session.SelectNode(document.Controls[0]);
        Assert.True(session.CopySelectedNode());
        session.SelectNode(null);
        Assert.True(session.PasteCopiedNode());
        Assert.True(session.Transactions.Undo());
        Assert.True(session.Transactions.Redo());
        var json = DesignDocumentSerializer.Default.Serialize(session.Document);
        var reopened = DesignDocumentSerializer.Default.Deserialize(json);
        Assert.Equal(2, reopened.Controls.Count);
        Assert.All(reopened.Controls, node => Assert.Equal("", node.Properties[nameof(Control.AccessibleName)].GetString()));
        Assert.DoesNotContain("AccessibilityObject", json);
        Assert.DoesNotContain("TextProvider", json);
    }

    private static DesignDocument CreateDocument(string type)
    {
        var document = new DesignDocument { Namespace = "Example", ClassName = "MainForm", FormName = "mainForm", Size = new DesignSize(640, 480) };
        document.Controls.Add(new DesignControlNode {
            TypeName = type, Name = "detailsControl", Bounds = new DesignBounds(10, 10, 240, 90),
            Properties = {
                // The generator inserts an implicit Dock=None before authored properties, while
                // reverse parsing materializes it in the sorted property map. Author it here so
                // exact code equality tests accessibility round-tripping, not that normalization.
                [nameof(Control.Dock)] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, nameof(DockStyle.None)),
                [nameof(Control.AccessibleName)] = DesignPropertyValue.FromString("Open details"),
                [nameof(Control.AccessibleDescription)] = DesignPropertyValue.FromString("Details for the selected item"),
                [nameof(Control.AccessibleAutomationId)] = DesignPropertyValue.FromString("details"),
                [nameof(Control.AccessibleDefaultActionDescription)] = DesignPropertyValue.FromString("Open"),
                [nameof(Control.AccessibilityView)] = DesignPropertyValue.FromEnum(typeof(AccessibilityView).FullName!, nameof(AccessibilityView.Content))
            }
        });
        return document;
    }
}
