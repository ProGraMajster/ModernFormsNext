using System.ComponentModel;
using ModernFormsNext.Animations;
using ModernFormsNext.DataBinding;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class PageCompositionTests
{
    [Theory]
    [InlineData(1d)]
    [InlineData(2.625d)]
    public void RetainedCompositionKeepsCanonicalResourcesBindingsAndOwnershipAcrossCycles(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(600, 400, scale));
        var model = new CompositionModel { Caption = "before" };
        using var source = new BindingSource { DataSource = new BindingList<CompositionModel> { model } };
        var editor = new TextBox();
        var binding = editor.DataBindings.Add(nameof(TextBox.Text), source, nameof(CompositionModel.Caption), formattingEnabled: true);
        const string key = "Composition.Caption";
        var label = new Label(); label.SetResourceReference(nameof(Label.Text), key);
        var body = new Panel(); body.Controls.Add(editor); body.Controls.Add(label);
        var first = new ContentPage { Title = "bound", Content = body };
        var tabs = new TabbedPage(); tabs.Resources[key] = "tabs";
        tabs.Pages.Add(first);
        for (int index = 0; index < 10; index++) tabs.Pages.Add(NewCompositionPage("retained " + index));
        var flyout = new FlyoutPage { DisplayMode = FlyoutDisplayMode.Overlay, Detail = tabs, Flyout = NewCompositionPage("menu") };
        using var root = new Panel { BindingContext = new BindingContext() };
        root.Controls.Add(new PageHost { Page = flyout, Dock = DockStyle.Fill });
        host.Show(root);
        editor.BindingContext = root.BindingContext;
        binding.ReadValue();
        Assert.Equal("before", editor.Text);
        Assert.Equal("tabs", label.Text);
        editor.Select();
        Assert.True(tabs.TrySelectPage(tabs.Pages[1]));
        tabs.Resources[key] = "updated while retained";
        model.Caption = "edited while retained"; binding.ReadValue();
        Assert.Equal("updated while retained", label.Text);
        Assert.Equal("edited while retained", editor.Text);
        Assert.True(ThemeManager.Current.Apply(BuiltInThemes.Dark,
            new ThemeApplyOptions { Transition = new ThemeTransitionOptions { Duration = TimeSpan.Zero } }).Success);
        for (int index = 0; index < 100; index++)
        {
            var page = NewCompositionPage("cycle " + index);
            Assert.True(tabs.Pages.Add(page));
            Assert.True(tabs.TrySelectPage(page));
            Assert.True(tabs.Pages.Remove(page));
            Assert.Null(page.Parent); page.Dispose();
            Assert.True(flyout.TrySetFlyoutOpen(true));
            Assert.True(flyout.TrySetFlyoutOpen(false));
        }
        Assert.Equal(11, tabs.Pages.Count);
        Assert.True(tabs.TrySelectPage(first));
        Assert.Equal("updated while retained", label.Text);
        Assert.Equal("edited while retained", editor.Text);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        Assert.True(tabs.TrySelectPage(tabs.Pages[1]));
        Assert.True(tabs.Pages.Remove(first));
        var transferred = root.Controls.Add(new PageHost { Page = first });
        transferred.Resources[key] = "new owner";
        Assert.Equal("new owner", label.Text);
        model.Caption = "after transfer"; binding.ReadValue();
        Assert.Equal("after transfer", editor.Text);
        tabs.Dispose();
        Assert.NotEqual(PageLifecycleState.Disposed, first.LifecycleState);
        Assert.Same(first, transferred.Page);
    }

    private sealed class CompositionModel
    {
        public string Caption { get; set; } = "";
    }
}
