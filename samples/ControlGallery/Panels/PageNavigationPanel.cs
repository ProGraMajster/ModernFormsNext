using ModernFormsNext;
using System.Linq;

namespace ControlGallery.Panels;

/// <summary>Demonstrates real hosted pages, validated editing, retained history and scoped Back.</summary>
public sealed class PageNavigationPanel : BasePanel
{
    private readonly NavigationPage navigation;
    private readonly CheckBox block;
    private readonly Label status;
    private int nextId;

    /// <summary>Creates an inert page tree; appearance begins when the gallery presents this panel.</summary>
    public PageNavigationPanel()
    {
        block = new CheckBox { Text = "Block departure while editing", Bounds = new(360, 10, 290, 32) };
        navigation = new NavigationPage(CreatePage("Documents"));
        Controls.Add(new PageHost { Page = navigation, Dock = DockStyle.Fill });
        var tools = Controls.Add(new Panel { Dock = DockStyle.Top, Height = 94 });
        status = Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 62, Multiline = true });
        tools.Controls.Add(block);
        tools.Controls.Add(new Label { Text = "Focus an editor to use Alt+Left / BrowserBack. Uncheck the veto to leave it.", Bounds = new(8, 50, 900, 32) });
        var push = new AsyncCommand(async () =>
        {
            var page = CreatePage("Details " + ++nextId);
            try { if (!await navigation.PushAsync(page) && page.Parent is null) page.Dispose(); }
            catch { if (page.Parent is null) page.Dispose(); throw; }
        });
        var root = new AsyncCommand(async () => { await navigation.PopToRootAsync(); }, () => navigation.CanGoBack);
        tools.Controls.Add(new Button { Text = "Open details", Command = push, Bounds = new(8, 10, 110, 32) });
        tools.Controls.Add(new Button { Text = "Back", Command = navigation.BackCommand, Bounds = new(126, 10, 100, 32) });
        tools.Controls.Add(new Button { Text = "To root", Command = root, Bounds = new(234, 10, 100, 32) });
        navigation.StackChanged += (_, _) => { root.RaiseCanExecuteChanged(); ShowStatus(); };
        navigation.Navigated += (_, e) =>
        {
            // This example owns the transferred pages and chooses to dispose them. Pop in
            // the framework does not dispose a page or prescribe application persistence.
            foreach (var removed in e.RemovedPages) if (removed.Parent is null) removed.Dispose();
            ShowStatus();
        };
        ShowStatus();
    }

    private ContentPage CreatePage(string title)
    {
        var body = new Panel { Padding = new Padding(12) };
        var editor = body.Controls.Add(new TextBox { Text = title, Dock = DockStyle.Top, Height = 38 });
        body.Controls.Add(new Label { Text = "This editor remains alive while its page is retained in history.", Bounds = new(12, 70, 720, 32) });
        editor.Validating += (_, e) => e.Cancel = block.Checked;
        return new ContentPage { Title = title, Content = body, AccessibleName = title };
    }

    private void ShowStatus() => status.Text = $"Stack: {string.Join(" → ", navigation.NavigationStack.Select(page => page.Title))}\n" +
        $"Current: {navigation.CurrentPage?.LifecycleState}; Back uses the canonical focus/validation route.";
}
