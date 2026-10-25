using ModernFormsNext.CodeGeneration.Utilities;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using ModernFormsNext.Drawing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed partial class DesignerRuntimeLayoutParityTests
{
    [Theory]
    [InlineData("R")]
    [InlineData("BlurRadius")]
    public void NullShadowMembersAreValidationErrorsBeforePreviewOrGeneration(string member)
    {
        var valid = DesignerPropertyValueEditor.ToDesignPropertyValue(new Shadow(System.Drawing.Color.Black, 4), typeof(Shadow));
        var properties = valid.ObjectProperties!.ToDictionary(pair => pair.Key, pair => pair.Value);
        properties[member] = null!;
        var invalid = DesignPropertyValue.FromStructuredObject(typeof(Shadow).FullName!, properties);
        Assert.False(new DesignDocumentValidator().ValidatePropertyValue(invalid).IsValid);
        Assert.Throws<FormatException>(() => CSharpLiteralWriter.WriteValue(invalid));
        Assert.Throws<FormatException>(() => DesignerPropertyValueEditor.FromDesignPropertyValue(invalid, typeof(Shadow)));
    }

    [Theory]
    [InlineData(nameof(Control.LineHeight), "0")]
    [InlineData(nameof(Control.LineHeight), "17")]
    [InlineData(nameof(Control.LetterSpacing), "1025")]
    [InlineData(nameof(Control.LetterSpacing), "-1025")]
    public void InvalidTypographyEditRollsBackWithoutPublishingUndo(string name, string text)
    {
        var document = BasicDocument();
        var node = Node("caption", nameof(Label), 20, 20, 180, 80);
        document.Controls.Add(node);
        using var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        var property = Assert.Single(grid.Properties, item => item.Name == name);
        grid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.False(grid.CommitSelectedValue(text));
        Assert.False(node.Properties.ContainsKey(name));
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void ShadowAndTypographyEditSerializeGenerateAndRunUsingOnlyKnownValues()
    {
        var document = BasicDocument();
        var node = Node("caption", nameof(Label), 20, 20, 180, 80);
        document.Controls.Add(node);
        using var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        Commit(grid, nameof(Control.Shadow), "Shadow(#60010203; 5; -2; 3; 1)");
        Commit(grid, nameof(Control.LineHeight), "1.5");
        Commit(grid, nameof(Control.LetterSpacing), "0.75");
        string json = DesignDocumentSerializer.Default.Serialize(document);
        var restored = DesignDocumentSerializer.Default.Deserialize(json);
        using var runtime = CompileAndCreateGeneratedRoot(restored);
        var root = Assert.IsAssignableFrom<Control>(runtime.Instance);
        var caption = Assert.IsType<Label>(Assert.Single(root.Controls));
        Assert.Equal(new Shadow(System.Drawing.Color.FromArgb(96, 1, 2, 3), 5, -2, 3, 1), caption.Shadow);
        Assert.Equal(1.5d, caption.LineHeight);
        Assert.Equal(0.75d, caption.LetterSpacing);
    }

    [Theory]
    [InlineData("BlurRadius", 129d)]
    [InlineData("OffsetX", 2049d)]
    [InlineData("Spread", -129d)]
    [InlineData("R", 256d)]
    public void InvalidShadowScalarsAreRejectedBeforePreviewOrGeneration(string member, double number)
    {
        var valid = DesignerPropertyValueEditor.ToDesignPropertyValue(new Shadow(System.Drawing.Color.Black, 4), typeof(Shadow));
        var properties = valid.ObjectProperties!.ToDictionary(pair => pair.Key, pair => pair.Value);
        properties[member] = member == "R" ? DesignPropertyValue.FromInt32((int)number) : DesignPropertyValue.FromDouble(number);
        var invalid = DesignPropertyValue.FromStructuredObject(typeof(Shadow).FullName!, properties);
        Assert.False(new DesignDocumentValidator().ValidatePropertyValue(invalid).IsValid);
        Assert.Throws<FormatException>(() => CSharpLiteralWriter.WriteValue(invalid));
        Assert.Throws<FormatException>(() => DesignerPropertyValueEditor.FromDesignPropertyValue(invalid, typeof(Shadow)));
    }

    [Fact]
    public void ShadowEditorRoundTripsAndRejectsUnsupportedTextWithoutChangingData()
    {
        var shadow = new Shadow(System.Drawing.Color.FromArgb(100, 10, 20, 30), 3.25f, -1.5f, 2.75f, 0.5f);
        string text = DesignerPropertyValueEditor.ToDisplayString(shadow);
        Assert.True(DesignerPropertyValueEditor.TryConvert(text, typeof(Shadow), out var restored, out var error), error);
        Assert.Equal(shadow, restored);
        Assert.False(DesignerPropertyValueEditor.TryConvert("Shadow(#FF000000; 129; 0; 0; 0)", typeof(Shadow), out _, out _));
        Assert.False(DesignerPropertyValueEditor.TryConvert("Shadow(#FF000000; 2; 0; 0; 0; 1)", typeof(Shadow), out _, out _));
    }
}
