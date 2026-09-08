using System;
using System.Threading.Tasks;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Input;

namespace ControlGallery.Panels;

/// <summary>Demonstrates shared synchronous, routed and asynchronous actions on existing controls.</summary>
public sealed class CommandRoutingPanel : BasePanel
{
    private CommandBindingCollection? windowBindings;
    private readonly CommandBinding windowBinding;
    private readonly AsyncCommand asyncWork;
    private readonly EventHandler asyncStateChanged;
    private readonly Label asyncStatus;
    private readonly ContextMenu commandMenu = new();
    private volatile bool inactive;

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
            Text = "Click either Save button, or focus it and press Ctrl+S.\nLocal overrides window. Uncheck Allow routed Save to disable both.",
            Left = 36, Top = 288, Width = 740, Height = 72, Multiline = true
        });

        var toolbar = Controls.Add(new ToolBar { Dock = DockStyle.None, Left = 36, Top = 362, Width = 720, Height = 40 });
        toolbar.Items.Add(new MenuItem("Save from toolbar") {
            Command = command, CommandParameter = "Document", CommandTarget = local
        });
        toolbar.Items.Add(new MenuItem("Reset count") {
            Command = new DelegateCommand(() => { saves = 0; status.Text = "Route owner: none"; })
        });

        asyncStatus = Controls.Add(new Label { Text = "Async: idle", Left = 36, Top = 480, Width = 720, Height = 36 });
        // Delay is only a visible demo operation. Tests use manually completed Tasks instead.
        asyncWork = new AsyncCommand((_, token) => Task.Delay(TimeSpan.FromSeconds(3), token));
        var cancel = new DelegateCommand(asyncWork.Cancel, () => asyncWork.CanCancel);
        asyncStateChanged = (_, _) => cancel.RaiseCanExecuteChanged();
        asyncWork.CanExecuteChanged += asyncStateChanged;
        asyncWork.Diagnostic += OnAsyncDiagnostic;
        Controls.Add(new Button {
            Text = "Run async task", Left = 36, Top = 424, Width = 180, Command = asyncWork
        });
        Controls.Add(new Button {
            Text = "Cancel async task", Left = 234, Top = 424, Width = 180, Command = cancel
        });
        toolbar.Items.Add(new MenuItem("Run async from toolbar") { Command = asyncWork });

        commandMenu.Items.Add(new MenuItem("Save from context") {
            Command = command, CommandParameter = "Document", CommandTarget = local
        });
        var openMenu = Controls.Add(new Button { Text = "Open command menu", Left = 432, Top = 424, Width = 220 });
        openMenu.Click += (_, _) => commandMenu.Show(openMenu, openMenu.PointToScreen(new System.Drawing.Point(0, openMenu.Height)));
        Controls.Add(new Label {
            Text = "Toolbar Save targets the local handler. Async work disables all its sources.\nCancel requests the running operation to stop; the same command can run again.",
            Left = 36, Top = 530, Width = 740, Height = 72, Multiline = true
        });
    }

    /// <inheritdoc/>
    public override void UnloadPanel() => ReleasePage();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // UI-thread unloading removes the short-lived capture. A closed window has already
        // released the collection, whose Count is then zero. Finalization must not call UI code.
        if (disposing) ReleasePage();
        base.Dispose(disposing);
    }

    private void ReleaseWindowBinding()
    {
        var bindings = windowBindings;
        windowBindings = null;
        if (bindings?.Contains(windowBinding) == true) bindings.Remove(windowBinding);
    }

    private void OnAsyncDiagnostic(object? sender, AsyncCommandDiagnosticEventArgs e)
    {
        string text = e.Kind switch {
            AsyncCommandDiagnosticKind.Started => "Async: running (IsExecuting = true)",
            AsyncCommandDiagnosticKind.Completed => "Async: completed",
            AsyncCommandDiagnosticKind.Cancelled => "Async: cancelled",
            _ => "Async: failed"
        };
        Application.RunOnUIThread(() => {
            if (!inactive && !Disposing) asyncStatus.Text = text;
        });
    }

    private void ReleasePage()
    {
        if (inactive) return;
        inactive = true;
        ReleaseWindowBinding();
        asyncWork.CanExecuteChanged -= asyncStateChanged;
        asyncWork.Diagnostic -= OnAsyncDiagnostic;
        // This page explicitly owns this demo operation. The framework's source disposal never
        // cancels shared commands; an application owner makes its own shutdown decision.
        asyncWork.Cancel();
        commandMenu.Hide();
        commandMenu.Dispose();
    }
}
