using System.Drawing;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class NavigationHeaderDesignerTests
{
    [Theory]
    [InlineData(nameof(NavigationPage))]
    [InlineData(nameof(AppShell))]
    public void BuiltInMetadataRoundTripsAndUsesInertPreview(string type)
    {
        var node = Node(type);
        node.Properties["NavigationBarTitle"] = DesignPropertyValue.FromString("Documents");
        node.Properties["NavigationBarHeight"] = DesignPropertyValue.FromInt32(50);
        node.Properties["NavigationBarAtBottom"] = DesignPropertyValue.FromBoolean(true);
        node.Properties["NavigationBarPreviewActionCount"] = DesignPropertyValue.FromInt32(2);
        node.Properties["NavigationBarPadding"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new Padding(4, 2, 6, 3), typeof(Padding));
        var document = Document(node);
        var serializer = DesignDocumentSerializer.Default;
        var generated = new CSharpDesignerGenerator().Generate(serializer.Deserialize(serializer.Serialize(document)));
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        var parsed = new CSharpDesignerParser().Parse(generated.Code, new CSharpDesignerParseOptions
        {
            RootKind = document.RootKind,
            NamespaceOverride = document.Namespace,
            ClassNameOverride = document.ClassName
        });
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var parsedNode = Assert.Single(parsed.Document!.Controls);
        using var preview = DesignerPagePreview.TryCreate(parsedNode, new Size(320, 200), out var error);
        Assert.True(preview is not null, error);
        var header = Header(preview.Root);
        Assert.Equal("Documents", header.TitleLabel.Text);
        Assert.Equal(new Rectangle(0, 150, 320, 50), header.Bounds);
        Assert.Equal(new Padding(4, 2, 6, 3), header.Padding);
        Assert.Null(header.BackButton.Command);
        Assert.True(header.BackButton.Visible);
        Assert.False(header.BackButton.Enabled);
        var slots = header.Controls.OfType<Button>().Where(button => button.Text == "Action").ToArray();
        Assert.Equal(2, slots.Length);
        Assert.All(slots, slot => { Assert.True(slot.Visible); Assert.False(slot.Enabled); Assert.Null(slot.Command); });
        Assert.True(header.BackButton.Right < slots[0].Left);

        parsedNode.Properties["NavigationBarPreviewPlaceholder"] = DesignPropertyValue.FromBoolean(true);
        using var placeholder = DesignerPagePreview.TryCreate(parsedNode, new Size(320, 200), out error);
        Assert.True(placeholder is not null, error);
        var inert = Header(placeholder.Root);
        Assert.Contains("runtime", inert.TitleLabel.Text, StringComparison.Ordinal);
        Assert.Null(inert.TitleView);
        Assert.Null(inert.HeaderView);
        Assert.Null(inert.BackButton.Command);
        Assert.False(inert.BackButton.Visible);
    }

    [Theory]
    [InlineData("NavigationBar.TitleView")]
    [InlineData("NavigationBar.HeaderView")]
    [InlineData("NavigationBarPadding.Left")]
    [InlineData("NavigationBarHeight")]
    [InlineData("NavigationBarToolbarActions")]
    [InlineData("NavigationBarPreviewActionCount")]
    [InlineData("HideNavigationBar")]
    public void InvalidOrRuntimeOnlyHeaderMetadataCannotGenerateOrPreview(string name)
    {
        var node = Node("global::ModernFormsNext.NavigationPage");
        node.Properties[name] = DesignPropertyValue.FromInt32(4097);
        var document = Document(node);
        var result = new DesignDocumentValidator().ValidateStructure(document);
        Assert.Contains(result.Errors, error => error.Contains(name, StringComparison.Ordinal));
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);
        Assert.Null(DesignerPagePreview.TryCreate(node, new Size(320, 200), out var diagnostic));
        Assert.Contains(name, diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void InheritedMetadataIsCheckedAndOwnedHeaderCannotBeAuthored()
    {
        var node = Node(nameof(NavigationPage));
        node.InheritedFrom = "HeaderTests.BaseForm";
        node.Properties["NavigationBarPadding"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new Padding(5000), typeof(Padding));
        var document = Document(node);
        document.BaseTypeName = node.InheritedFrom;
        Assert.Contains(new DesignDocumentValidator().ValidateStructure(document).Errors,
            error => error.Contains("NavigationBarPadding", StringComparison.Ordinal));
        document.Controls.Clear();
        document.Controls.Add(Node(nameof(NavigationHeader)));
        Assert.Contains(new DesignDocumentValidator().ValidateStructure(document).Errors,
            error => error.Contains("cannot be authored", StringComparison.Ordinal));
    }

    [Fact]
    public void ReverseParserDoesNotAcceptRuntimeViewSlot()
    {
        const string source = """
            namespace HeaderTests;
            public partial class MainForm
            {
                private NavigationPage navigation;
                private void InitializeComponent()
                {
                    this.navigation = new NavigationPage();
                    this.navigation.NavigationBar.TitleView = null;
                    this.Controls.Add(this.navigation);
                }
            }
            """;
        Assert.False(new CSharpDesignerParser().Parse(source).Success);
    }

    private static NavigationHeader Header(Control control)
        => control is NavigationPage navigation ? navigation.NavigationBar : Assert.IsType<AppShell>(control).NavigationBar;

    private static DesignControlNode Node(string type) => new() { Name = "navigation", TypeName = type };
    private static DesignDocument Document(DesignControlNode node)
        => new() { Namespace = "HeaderTests", ClassName = "MainForm", FormName = "MainForm", Controls = { node } };
}
