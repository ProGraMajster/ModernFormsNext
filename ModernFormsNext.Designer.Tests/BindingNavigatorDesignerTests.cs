using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.DataBinding;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class BindingNavigatorDesignerTests
{
    [Fact]
    public void ToolboxUsesAnExactFrameworkNamespaceIdentity()
    {
        var item = Assert.Single(new DesignerToolboxService().GetItems(), item => item.DisplayName == nameof(BindingNavigator));
        Assert.Equal(typeof(BindingNavigator).FullName, item.TypeName);
        using var session = new DesignerSession();
        Assert.Equal(typeof(BindingNavigator), session.ResolveControlType(item.TypeName));
    }

    [Theory]
    [InlineData(nameof(BindingNavigator.BindingSource))]
    [InlineData(nameof(BindingNavigator.MoveFirstButton))]
    [InlineData(nameof(BindingNavigator.PositionTextBox))]
    [InlineData(nameof(BindingNavigator.SaveButton))]
    public void BorrowedRuntimeObjectsAndOwnedChromeAreNotSerialized(string name)
    {
        var property = TypeDescriptor.GetProperties(typeof(BindingNavigator))[name]!;
        Assert.False(property.IsBrowsable);
        Assert.Equal(DesignerSerializationVisibility.Hidden, property.SerializationVisibility);
        var metadata = new DesignMetadataReader().ReadProperty(typeof(BindingNavigator).GetProperty(name)!);
        Assert.True(metadata.IsHidden);
        Assert.False(metadata.Serialize);
    }

    [Fact]
    public void InertPreviewAndRoundTripKeepOnlyScalarAuthoredState()
    {
        var document = new DesignDocument { Namespace = "NavigatorExample", ClassName = "MainView", FormName = "MainView", Size = new(820, 200) };
        var node = new DesignControlNode { Name = "navigator", TypeName = typeof(BindingNavigator).FullName!, Bounds = new(0, 0, 760, 42) };
        // Author the bar's dock selection explicitly, as the actual Designer does. Otherwise
        // the generator inserts its ordinary missing-Dock default before canonical key ordering.
        node.Properties[nameof(Control.Dock)] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, "Top");
        node.Properties[nameof(BindingNavigator.CountItemFormat)] = DesignPropertyValue.FromString("Records: {0}");
        document.Controls.Add(node);
        string saved = DesignDocumentSerializer.Default.Serialize(document);
        var reopened = DesignDocumentSerializer.Default.Deserialize(saved);
        using var preview = DesignerPagePreview.TryCreate(reopened.Controls[0], new Size(760, 42), out var error);
        Assert.NotNull(preview);
        Assert.Null(error);
        Assert.Empty(reopened.Controls[0].Children);
        Assert.Equal(saved, DesignDocumentSerializer.Default.Serialize(reopened));
        var result = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(result.Succeeded);
        Assert.Contains("new ModernFormsNext.DataBinding.BindingNavigator", result.Code);
        Assert.DoesNotContain("ToolStrip", result.Code);
        Assert.DoesNotContain("BindingSource =", result.Code);
        var parsed = new CSharpDesignerParser().Parse(result.Code);
        Assert.True(parsed.Success, string.Join("; ", parsed.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(result.Code, new CSharpDesignerGenerator().Generate(parsed.Document!).Code);
    }

    [Theory]
    [InlineData("Records {name}")]
    [InlineData("Records {0:custom}")]
    [InlineData("Records }")]
    public void UnsupportedOrdinaryCountTemplatesFailBeforeFormatting(string value)
    {
        using var navigator = new BindingNavigator();
        Assert.Throws<ArgumentException>(() => navigator.CountItemFormat = value);
        Assert.Equal("of {0}", navigator.CountItemFormat);
        Assert.Equal("of 0", navigator.CountLabel.Text);
    }
}
