using ModernFormsNext;
using ModernFormsNext.WindowKit.Input;

namespace ControlGallery.Panels;

/// <summary>Demonstrates one routed Save action shared by buttons and a keyboard binding.</summary>
public sealed class CommandRoutingPanel : BasePanel
{
    private CommandBindingCollection? windowBindings;
    private readonly CommandBinding windowBinding;

    /// <summary>Creates a local override and a handler in the gallery's existing window scope.</summary>
    /// <param name="window">The gallery window; its sample registration is removed when this page unloads.</param>
    public CommandRoutingPanel(WindowBase window)
    {
        var command = new RoutedCommand("Gallery.Save");
        int saves = 0;
        Controls.Add(new Label {
            Text = "Routed Save: one command, two handler locations", Left = 36, Top = 44, Width = 700, Height = 36
        });
        var allowed = Controls.Add(new CheckBox {
            Text = "Allow routed Save", Checked = true, Left = 36, Top = 104, Width = 300
        });
        var status = Controls.Add(new Label {
            Text = "Route owner: none", Left = 36, Top = 226, Width = 700, Height = 36
        });
        var local = Controls.Add(new Button {
            Text = "Save locally", Left = 36, Top = 160, Width = 170,
            Command = command, CommandParameter = "Document"
        });
        Controls.Add(new Button {
            Text = "Save via window", Left = 230, Top = 160, Width = 190,
            Command = command, CommandParameter = "Document"
        });
        local.CommandBindings.Add(new CommandBinding(command,
            (_, e) => { status.Text = $"Route owner: local; saves: {++saves}"; e.Handled = true; },
            (_, e) => e.CanExecute = allowed.Checked));
        windowBinding = new CommandBinding(command,
            (_, e) => { status.Text = $"Route owner: window; saves: {++saves}"; e.Handled = true; },
            (_, e) => e.CanExecute = allowed.Checked);
        windowBindings = window.CommandBindings;
        windowBindings.Add(windowBinding);
        // Gesture lookup remains on the page. Handler lookup starts at the selected control and
        // either finds its local override or reaches the real WindowBase registration.
        InputBindings.Add(new KeyBinding(command, new KeyGesture(Keys.S, KeyModifiers.Control)) {
            CommandParameter = "Document"
        });
        allowed.CheckedChanged += (_, _) => command.RaiseCanExecuteChanged();
        Controls.Add(new Label {
            Text = "Click a Save button, or focus it and press Ctrl+S. Save locally handles first;\nSave via window falls back to the window. Uncheck Allow routed Save to disable both.",
            Left = 36, Top = 288, Width = 740, Height = 72
        });
    }

    /// <inheritdoc/>
    public override void UnloadPanel() => ReleaseWindowBinding();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // UI-thread unloading removes the short-lived capture. A closed window has already
        // released the collection, whose Count is then zero. Finalization must not call UI code.
        if (disposing) ReleaseWindowBinding();
        base.Dispose(disposing);
    }

    private void ReleaseWindowBinding()
    {
        var bindings = windowBindings;
        windowBindings = null;
        if (bindings?.Contains(windowBinding) == true) bindings.Remove(windowBinding);
    }
}
