using ModernFormsNext;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ControlGallery.Panels;

/// <summary>Demonstrates explicit routes, copied parameters and opt-in lifecycle delivery.</summary>
public sealed class AppShellPanel : BasePanel
{
    private readonly AppShell shell = new();
    private readonly CheckBox block = new() { Text = "Veto editor departure", Bounds = new(410, 6, 220, 32) };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 86, Multiline = true };
    private readonly List<Page> created = [];
    private AppShellLifecycleBinding? binding;
    private int document;

    /// <summary>Creates route registrations; only explicit button actions invoke their factories.</summary>
    public AppShellPanel()
    {
        Controls.Add(new PageHost { Page = shell, Dock = DockStyle.Fill });
        var tools = Controls.Add(new Panel { Dock = DockStyle.Top, Height = 92 });
        Controls.Add(status);
        tools.Controls.Add(new Button { Text = "Open home", Bounds = new(8, 6, 110, 32), Command = Visit(() => "/home") });
        tools.Controls.Add(new Button { Text = "Open document", Bounds = new(126, 6, 150, 32), Command = Visit(() => "/documents/" + ++document + "?mode=edit") });
        tools.Controls.Add(new Button { Text = "Shell Back", Bounds = new(284, 6, 116, 32), Command = shell.BackCommand });
        tools.Controls.Add(block);
        tools.Controls.Add(new Button { Text = "Enable lifecycle links", Bounds = new(8, 48, 190, 32), Command = new DelegateCommand(() =>
        {
            binding ??= shell.AttachLifecycle(Application.Lifecycle, new Uri("mfn-gallery://shell/"), "gallery");
            ShowStatus();
        }) });
        tools.Controls.Add(new Button { Text = "Deliver example link", Bounds = new(206, 48, 190, 32), Command = new DelegateCommand(() =>
        {
            if (binding is null) return;
            Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Uri,
                uri: new Uri("mfn-gallery://shell/documents/linked?mode=preview")));
        }) });
        tools.Controls.Add(new Button { Text = "Capture route state", Bounds = new(404, 48, 180, 32), Command = new DelegateCommand(() =>
        {
            var data = shell.CaptureState("gallery");
            status.Text = $"Captured {data.Values.Count - 1} outer visits; no editor contents or file persistence.\n" +
                "RestoreStateAsync applies this explicit data only to a fresh, empty shell.";
        }) });
        shell.Routes.Register("home", _ => CreatePage("Home", "Open a document to add a retained route visit."));
        shell.Routes.Register("documents/{id}", context => CreatePage("Document " + context.Parameters["id"],
            $"Route: {context.Route}\nParameter id = {context.Parameters["id"]}; mode = {context.Parameters.GetValueOrDefault("mode", "view")}"));
        shell.CurrentRouteChanged += (_, _) => ShowStatus();
        shell.Navigated += (_, args) =>
        {
            // This example owns its factory results and chooses disposal after transfer.
            foreach (var page in args.RemovedPages) if (page.Parent is null) { created.Remove(page); page.Dispose(); }
        };
        ShowStatus();
    }

    private AsyncCommand Visit(Func<string> route) => new(async () =>
    {
        try { await shell.GoToAsync(route()); }
        finally
        {
            // A validation veto leaves factory output caller-owned, so the sample releases it.
            foreach (var page in created.Where(page => page.Parent is null).ToArray()) { created.Remove(page); page.Dispose(); }
            ShowStatus();
        }
    });
    private ContentPage CreatePage(string title, string description)
    {
        var body = new Panel { Padding = new Padding(12) };
        var editor = body.Controls.Add(new TextBox { Text = title, Bounds = new(12, 12, 340, 38), AccessibleName = "Shell page editor" });
        editor.Validating += (_, args) => args.Cancel = block.Checked;
        body.Controls.Add(new Label { Text = description, Multiline = true, Bounds = new(12, 65, 760, 100) });
        var page = new ContentPage { Title = title, Content = body };
        created.Add(page); return page;
    }
    private void ShowStatus() => status.Text = $"Current: {shell.CurrentRoute?.Route ?? "(empty — choose Open home)"}\n" +
        $"History: {shell.NavigationStack.Count}; lifecycle links: {(binding is null ? "disabled" : "explicitly enabled")}. " +
        "Focus an editor for Alt+Left / BrowserBack. Example link delivery registers no operating-system protocol.";
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            binding?.Dispose(); binding = null;
            base.Dispose(true);
            foreach (var page in created) if (page.Parent is null) page.Dispose();
            created.Clear();
        }
        else base.Dispose(false);
    }
}
