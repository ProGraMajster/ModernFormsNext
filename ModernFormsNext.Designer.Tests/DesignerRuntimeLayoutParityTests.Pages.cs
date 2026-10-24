using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed partial class DesignerRuntimeLayoutParityTests
{
    [Fact]
    public void PageTextAndTitleSourceAssignmentsRetainTheLastAuthoredValue()
    {
        const string source = """
            using ModernFormsNext;
            public partial class AliasFixture
            {
                private void InitializeComponent()
                {
                    var page = new ContentPage();
                    page.Title = "Earlier";
                    page.Text = "Later";
                    this.Controls.Add(page);
                }
            }
            """;
        var parsed = new CSharpDesignerParser().Parse(source, new CSharpDesignerParseOptions
        { RootKind = DesignRootKind.UserControl, NamespaceOverride = "DesignerPageAliases" });
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
        var node = Assert.Single(parsed.Document!.Controls);
        Assert.False(node.Properties.ContainsKey("Text"));
        Assert.Equal("Later", node.Properties["Title"].Value);
        using var runtime = CompileAndCreateGeneratedRoot(parsed.Document);
        Assert.Equal("Later", Assert.IsType<ContentPage>(Assert.Single(Assert.IsAssignableFrom<Control>(runtime.Instance).Controls)).Title);
    }

    [Fact]
    public void AuthoredPageHostContentSurvivesMfdesignCodeAndCompiledRuntime()
    {
        var document = BasicDocument();
        var host = Node("pageHost", nameof(PageHost), 10, 15, 320, 220);
        var page = Node("homePage", nameof(ContentPage), 0, 0, 320, 220);
        page.Properties["Title"] = DesignPropertyValue.FromString("Home");
        var label = Node("pageBody", nameof(Label), 20, 25, 100, 30);
        label.Properties["Text"] = DesignPropertyValue.FromString("Designed content");
        page.Children.Add(label);
        host.Children.Add(page);
        document.Controls.Add(host);

        var serializer = DesignDocumentSerializer.Default;
        var reopened = serializer.Deserialize(serializer.Serialize(document));
        var generated = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        var parsed = new CSharpDesignerParser().Parse(generated.Code, new CSharpDesignerParseOptions
        {
            RootKind = reopened.RootKind,
            NamespaceOverride = reopened.Namespace,
            ClassNameOverride = reopened.ClassName
        });
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
        using var root = CompileAndCreateGeneratedRoot(parsed.Document!);
        var runtimeHost = Assert.IsType<PageHost>(Assert.Single(Assert.IsAssignableFrom<Control>(root.Instance).Controls));

        // The canonical slot must own the authored page. Merely adding it to the host's
        // ordinary Controls collection compiles but leaves navigation/lifecycle ownership empty.
        var runtimePage = Assert.IsType<ContentPage>(runtimeHost.Page);
        Assert.Equal("homePage", runtimePage.Name);
        Assert.Equal("Home", runtimePage.Title);
        Assert.True(runtimeHost.Contains(runtimePage));
        var runtimeBody = Assert.IsType<Label>(runtimePage.Content);
        Assert.Equal("Designed content", runtimeBody.Text);
        Assert.Same(runtimePage, runtimeBody.Parent);
        Assert.Single(runtimePage.Controls);
    }

    [Fact]
    public void AuthoredNavigationRootSurvivesLocalMemberAndCompiledRuntime()
    {
        var document = BasicDocument();
        var navigation = Node("navigation", nameof(NavigationPage), 0, 0, 300, 200);
        var page = Node("rootPage", nameof(ContentPage), 0, 0, 300, 200);
        page.MemberVisibility = DesignerMemberVisibility.None;
        page.Properties["Title"] = DesignPropertyValue.FromString("Root");
        navigation.Children.Add(page);
        document.Controls.Add(navigation);

        var reopened = RoundTripPageDocument(document);
        Assert.Equal(DesignerMemberVisibility.None, Assert.Single(Assert.Single(reopened.Controls).Children).MemberVisibility);
        using var root = CompileAndCreateGeneratedRoot(reopened);
        var runtime = Assert.IsType<NavigationPage>(Assert.Single(Assert.IsAssignableFrom<Control>(root.Instance).Controls));
        var current = Assert.IsType<ContentPage>(runtime.RootPage);
        Assert.Equal("Root", current.Title);
        Assert.Same(current, runtime.CurrentPage);
        Assert.Same(current, Assert.Single(runtime.NavigationStack));
        Assert.True(runtime.Contains(current));
        Assert.False(runtime.IsNavigating);
    }

    [Fact]
    public void AuthoredTabbedPagesRetainOrderAndSelectionInCompiledRuntime()
    {
        var document = BasicDocument();
        var tabs = Node("pages", nameof(TabbedPage), 0, 0, 300, 200);
        tabs.Properties["SelectedIndex"] = DesignPropertyValue.FromInt32(1);
        foreach (var title in new[] { "First", "Second" })
        {
            var page = Node(title.ToLowerInvariant(), nameof(ContentPage), 0, 0, 300, 200);
            page.Properties["Title"] = DesignPropertyValue.FromString(title);
            tabs.Children.Add(page);
        }
        document.Controls.Add(tabs);

        using var root = CompileAndCreateGeneratedRoot(RoundTripPageDocument(document));
        var runtime = Assert.IsType<TabbedPage>(Assert.Single(Assert.IsAssignableFrom<Control>(root.Instance).Controls));
        Assert.Equal(["First", "Second"], runtime.Pages.Select(page => page.Title));
        Assert.Equal(1, runtime.SelectedIndex);
        Assert.Same(runtime.Pages[1], runtime.SelectedPage);
        Assert.All(runtime.Pages, page => Assert.True(runtime.Contains(page)));
    }

    [Fact]
    public void AuthoredFlyoutRolesRetainDistinctPagesInCompiledRuntime()
    {
        var document = BasicDocument();
        var flyout = Node("workspace", nameof(FlyoutPage), 0, 0, 300, 200);
        foreach (var slot in new[]
        {
            ("detail", "Detail", DesignNodeRoleNames.FlyoutPageDetail),
            ("menu", "Menu", DesignNodeRoleNames.FlyoutPageFlyout)
        })
        {
            var page = Node(slot.Item1, nameof(ContentPage), 0, 0, 300, 200);
            page.Properties["Title"] = DesignPropertyValue.FromString(slot.Item2);
            page.Properties[DesignNodeRoleNames.RolePropertyName] = DesignPropertyValue.FromString(slot.Item3);
            flyout.Children.Add(page);
        }
        document.Controls.Add(flyout);

        using var root = CompileAndCreateGeneratedRoot(RoundTripPageDocument(document));
        var runtime = Assert.IsType<FlyoutPage>(Assert.Single(Assert.IsAssignableFrom<Control>(root.Instance).Controls));
        Assert.Equal("Detail", Assert.IsType<ContentPage>(runtime.Detail).Title);
        Assert.Equal("Menu", Assert.IsType<ContentPage>(runtime.Flyout).Title);
        Assert.NotSame(runtime.Detail, runtime.Flyout);
        Assert.True(runtime.Contains(runtime.Detail!));
        Assert.True(runtime.Contains(runtime.Flyout!));
    }

    private static DesignDocument RoundTripPageDocument(DesignDocument document)
    {
        var reopened = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));
        var generated = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        var parsed = new CSharpDesignerParser().Parse(generated.Code, new CSharpDesignerParseOptions
        {
            RootKind = reopened.RootKind,
            NamespaceOverride = reopened.Namespace,
            ClassNameOverride = reopened.ClassName
        });
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
        return parsed.Document!;
    }
}
