using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class PageDesignerSafetyTests
{
    [Fact]
    public void CyclicPageGraphIsRejectedWithoutNormalizingOrReplacingTheActiveDocument()
    {
        using var session = new DesignerSession();
        var original = Document();
        session.LoadDocument(original);
        var malformed = Document();
        var page = Page("cycle");
        page.Children.Add(page);
        malformed.Controls.Add(page);

        var validation = new DesignDocumentValidator().Validate(malformed);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("cyclic", StringComparison.Ordinal));
        var generated = new CSharpDesignerGenerator().Generate(malformed);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);
        Assert.Throws<ArgumentException>(() => session.LoadDocument(malformed));
        Assert.Same(original, session.Document);
        Assert.Same(page, Assert.Single(page.Children));
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void APageCannotBeSharedByTwoAuthoredOwners()
    {
        var document = Document();
        var first = Page("first");
        var second = Page("second");
        var shared = Page("shared");
        first.Children.Add(shared);
        second.Children.Add(shared);
        document.Controls.Add(first);
        document.Controls.Add(second);
        Assert.False(new DesignDocumentValidator().Validate(document).IsValid);
        Assert.False(new CSharpDesignerGenerator().Generate(document).Succeeded);
        Assert.Same(shared, first.Children[0]);
        Assert.Same(shared, second.Children[0]);
    }

    [Fact]
    public void ExcessivePageDepthAndNodeCountAreRejectedBeforeRecursiveWork()
    {
        var document = Document();
        var root = Page("root");
        document.Controls.Add(root);
        var current = root;
        for (int depth = 0; depth <= DesignDocumentValidator.MaximumTreeDepth; depth++)
        {
            var next = Page("page" + depth);
            current.Children.Add(next);
            current = next;
        }
        Assert.False(new DesignDocumentValidator().Validate(document).IsValid);
        Assert.Empty(new CSharpDesignerGenerator().Generate(document).Code);
        document.Controls.Clear();
        for (int index = 0; index <= DesignDocumentValidator.MaximumNodeCount; index++) document.Controls.Add(Page("page" + index));
        Assert.False(new DesignDocumentValidator().ValidateStructure(document).IsValid);
        Assert.Equal(10_001, document.Controls.Count);
    }

    [Fact]
    public void NullModelCollectionsAreDiagnosticsInsteadOfPartialInitialization()
    {
        var document = Document();
        var page = Page("broken");
        page.Children = null!;
        document.Controls.Add(page);
        Assert.False(new DesignDocumentValidator().Validate(document).IsValid);
        Assert.Empty(new CSharpDesignerGenerator().Generate(document).Code);
        Assert.Null(page.Children);
    }

    [Theory]
    [InlineData("new NavigationPage(LoadPage())")]
    [InlineData("new NavigationPage(new ContentPage())")]
    [InlineData("new NavigationPage() { Title = LoadTitle() }")]
    [InlineData("new ContentPage(LoadPage())")]
    public void ExecutableOrInlineConstructorSourceIsExplicitlyUnsupported(string creation)
    {
        // This is syntax input only; the parser must neither compile nor execute its calls.
        string source = $$"""
            namespace Safety;
            public partial class MainForm
            {
                private NavigationPage navigation;
                private void InitializeComponent()
                {
                    this.navigation = {{creation}};
                    this.Controls.Add(this.navigation);
                }
            }
            """;
        var parsed = new CSharpDesignerParser().Parse(source);
        Assert.False(parsed.Success);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Severity == CSharpDesignerDiagnosticSeverity.Error &&
            diagnostic.Syntax is not null && diagnostic.Syntax.Contains(creation, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("this.host.Page = BuildPage();")]
    [InlineData("this.host.Page = this.page; this.host.Page = null;")]
    [InlineData("this.host.Page = this.page; this.host.Page = this.page;")]
    [InlineData("this.host.Controls.Add(this.page);")]
    public void UnsupportedPageOwnershipSourceCannotBeReportedAsASuccessfulRoundTrip(string assignment)
    {
        string source = $$"""
            namespace Safety;
            public partial class MainForm
            {
                private PageHost host;
                private ContentPage page;
                private void InitializeComponent()
                {
                    this.host = new PageHost();
                    this.page = new ContentPage();
                    {{assignment}}
                    this.Controls.Add(this.host);
                }
            }
            """;
        Assert.False(new CSharpDesignerParser().Parse(source).Success);
    }

    private static DesignControlNode Page(string name) => new() { Name = name, TypeName = nameof(ContentPage) };
    private static DesignDocument Document() => new() { Namespace = "Safety", ClassName = "MainForm", FormName = "MainForm" };
}
