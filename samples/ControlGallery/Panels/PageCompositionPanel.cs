using ModernFormsNext;

namespace ControlGallery.Panels;

/// <summary>Demonstrates retained tab pages inside responsive flyout/detail presentation.</summary>
public sealed class PageCompositionPanel : BasePanel
{
    private readonly FlyoutPage flyout = new();
    private readonly TabbedPage tabs = new();
    private readonly CheckBox block = new() { Text = "Veto edited-page departure", CausesValidation = false };
    private readonly Label status = new() { Height = 70, Multiline = true, Dock = DockStyle.Bottom };
    private int note;

    /// <summary>Creates an inert composition; normal gallery hosting starts its page lifecycle.</summary>
    public PageCompositionPanel()
    {
        Controls.Add(new PageHost { Page = flyout, Dock = DockStyle.Fill });
        var tools = Controls.Add(new Panel { Dock = DockStyle.Top, Height = 82 });
        Controls.Add(status);
        tools.Controls.Add(block); block.Bounds = new(8, 42, 260, 32);
        var flyoutRtl = tools.Controls.Add(new CheckBox { Text = "Flyout RTL", CausesValidation = false, Bounds = new(280, 42, 130, 32) });
        var tabsRtl = tools.Controls.Add(new CheckBox { Text = "Tabs RTL", CausesValidation = false, Bounds = new(420, 42, 130, 32) });
        flyoutRtl.CheckedChanged += (_, _) => flyout.FlowDirection = flyoutRtl.Checked ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        tabsRtl.CheckedChanged += (_, _) => tabs.FlowDirection = tabsRtl.Checked ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        tools.Controls.Add(new Button { Text = "Auto / Overlay / Split", Bounds = new(8, 4, 170, 32), Command = new DelegateCommand(() =>
            flyout.DisplayMode = flyout.DisplayMode switch { FlyoutDisplayMode.Auto => FlyoutDisplayMode.Overlay, FlyoutDisplayMode.Overlay => FlyoutDisplayMode.Split, _ => FlyoutDisplayMode.Auto }) });
        tools.Controls.Add(new Button { Text = "Add note", Bounds = new(186, 4, 100, 32), Command = new DelegateCommand(() =>
        {
            var page = CreateDocument("Note " + ++note);
            try { if (!tabs.Pages.Add(page) && page.Parent is null) page.Dispose(); }
            catch { if (page.Parent is null) page.Dispose(); throw; }
        }) });
        var remove = new DelegateCommand(() =>
        {
            if (tabs.SelectedPage is { } page && tabs.Pages.Remove(page) && page.Parent is null) page.Dispose();
        }, () => tabs.SelectedPage is not null);
        tools.Controls.Add(new Button { Text = "Remove selected", Bounds = new(294, 4, 140, 32), Command = remove });
        tools.Controls.Add(new Button { Text = "Move selected last", Bounds = new(442, 4, 150, 32), Command = new DelegateCommand(() =>
        {
            if (tabs.SelectedIndex >= 0) tabs.Pages.Move(tabs.SelectedIndex, tabs.Pages.Count - 1);
        }) });
        var documents = CreateDocument("Documents");
        var settings = CreateDocument("Settings");
        tabs.Pages.Add(documents); tabs.Pages.Add(settings);
        var menu = new Panel { Padding = new Padding(12) };
        menu.Controls.Add(new Button { Text = "Documents tab", Bounds = new(12, 12, 210, 36), Command = new DelegateCommand(() => Choose(documents)) });
        menu.Controls.Add(new Button { Text = "Settings tab", Bounds = new(12, 58, 210, 36), Command = new DelegateCommand(() => Choose(settings)) });
        menu.Controls.Add(new Button { Text = "Close navigation", Bounds = new(12, 104, 210, 36), Command = flyout.CloseFlyoutCommand });
        flyout.Flyout = new ContentPage { Title = "Navigation", Content = menu };
        flyout.Detail = tabs;
        tabs.SelectedPageChanged += (_, _) => { remove.RaiseCanExecuteChanged(); ShowStatus(); };
        tabs.PagesChanged += (_, _) => { remove.RaiseCanExecuteChanged(); ShowStatus(); };
        flyout.FlyoutOpenChanged += (_, _) => ShowStatus();
        flyout.EffectiveDisplayModeChanged += (_, _) => ShowStatus();
        ShowStatus();
    }

    private ContentPage CreateDocument(string title)
    {
        var body = new Panel { Padding = new Padding(12) };
        body.Controls.Add(new Button { Text = "Open navigation", Command = flyout.ToggleFlyoutCommand, Bounds = new(12, 12, 160, 34) });
        var editor = body.Controls.Add(new TextBox { Text = title, Bounds = new(12, 62, 320, 40), AccessibleName = title + " editor" });
        editor.Validating += (_, e) => e.Cancel = block.Checked;
        body.Controls.Add(new Label { Text = "Edits survive tab changes. Ctrl+Tab changes tabs; Escape / Back dismisses an overlay.", Bounds = new(12, 116, 700, 52), Multiline = true });
        return new ContentPage { Title = title, Content = body, AccessibleName = title };
    }
    private void Choose(Page page)
    {
        if (tabs.Pages.Contains(page) && flyout.TrySetFlyoutOpen(false)) tabs.TrySelectPage(page);
    }
    private void ShowStatus() => status.Text = $"Mode: {flyout.DisplayMode} → {flyout.EffectiveDisplayMode}; overlay intent: {flyout.IsFlyoutOpen}\n" +
        $"Selected: {tabs.SelectedPage?.Title ?? "(none)"}; retained pages: {tabs.Pages.Count}. Removed pages are disposed by this example's policy.";
}
