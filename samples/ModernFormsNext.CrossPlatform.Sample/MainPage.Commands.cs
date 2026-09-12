using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.CrossPlatform.Sample;

public sealed partial class MainPage
{
    private (Control Control, int Height)[] commandDemoRows = [];
    private InputBindingCollection? commandApplicationBindings;
    private KeyBinding? commandApplicationHelp;
    private bool commandDemoDisposed;

    private void InitializeCommandDemo()
    {
        var available = new CheckBox { Name = "LocalSaveAvailable", Text = "Enable editor Ctrl+S binding", Checked = true };
        var status = CreateLabel("Commands: editor=0; page=0; save-as=0; help=0");
        status.Name = "KeyboardCommandCounts";
        status.Multiline = true;
        var last = CreateLabel("Ctrl+S: editor, then page fallback. Ctrl+Shift+S: Save As. F1: Application help.");
        last.Name = "KeyboardCommandScope";
        last.Multiline = true;
        var editorCount = 0;
        var pageCount = 0;
        var saveAsCount = 0;
        var helpCount = 0;

        void RefreshCommands(string scope)
        {
            status.Text = $"Commands: editor={editorCount}; page={pageCount}; save-as={saveAsCount}; help={helpCount}";
            last.Text = $"Last command: {scope}. Counts survive Activity recreation.";
        }

        var editorSave = new DelegateCommand(() => { editorCount++; RefreshCommands("editor Save"); },
            () => !commandDemoDisposed && available.Checked);
        var pageSave = new DelegateCommand(() => { pageCount++; RefreshCommands("page Save fallback"); },
            () => !commandDemoDisposed);
        var saveAs = new DelegateCommand(() => { saveAsCount++; RefreshCommands("page Save As"); },
            () => !commandDemoDisposed);
        var help = new RoutedCommand("Sample.Help");
        // Application lookup receives the current input target. Let the canonical command
        // route find this page's handler; another inactive page can retain Selected controls.
        CommandBindings.Add(new CommandBinding(help,
            (_, e) => { helpCount++; RefreshCommands("Application Help"); e.Handled = true; },
            (_, e) => e.CanExecute = !commandDemoDisposed));

        nameTextBox.InputBindings.Add(new KeyBinding(editorSave, new KeyGesture(Keys.S, KeyModifiers.Control)));
        InputBindings.Add(new KeyBinding(pageSave, new KeyGesture(Keys.S, KeyModifiers.Control)));
        InputBindings.Add(new KeyBinding(saveAs, new KeyGesture(Keys.S, KeyModifiers.Control | KeyModifiers.Shift)));
        commandApplicationHelp = new KeyBinding(help, new KeyGesture(Keys.F1));
        available.CheckedChanged += (_, _) => editorSave.RaiseCanExecuteChanged();

        var saveButton = new Button { Name = "KeyboardSave", Text = "Save (same editor command)", Command = editorSave };
        var saveAsButton = new Button { Name = "KeyboardSaveAs", Text = "Save As (Ctrl+Shift+S)", Command = saveAs };
        commandDemoRows = [(status, 48), (last, 62), (available, 34), (saveButton, 38), (saveAsButton, 38)];
    }

    private void RegisterApplicationCommand()
    {
        // This is the final constructor step: a failed platform fact/layout query cannot leave
        // an unfinished page reachable through a process-wide command registration.
        commandApplicationBindings = Application.InputBindings;
        try { commandApplicationBindings.Add(commandApplicationHelp!); }
        catch
        {
            // Add commits its item before notifying opt-in duplicate diagnostics, which may throw.
            ReleaseApplicationCommand();
            throw;
        }
    }

    private void ReleaseApplicationCommand()
    {
        commandDemoDisposed = true;
        var collection = commandApplicationBindings;
        var binding = commandApplicationHelp;
        commandApplicationBindings = null;
        commandApplicationHelp = null;
        // Application.Exit retires its getter and clears the original collection. Use the
        // captured collection, and remove only this page's still-registered instance.
        if (collection is not null && binding is not null && collection.Contains(binding))
            collection.Remove(binding);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // Release the global owner before child disposal or user Disposed handlers can fail.
        // A native Activity only disposes its borrowing surface, so recreation keeps the count.
        if (!disposing) { base.Dispose(false); return; }
        var failures = new List<Exception>();
        try { DisposePreferenceDemo(); } catch (Exception error) { failures.Add(error); }
        try { ReleaseApplicationCommand(); } catch (Exception error) { failures.Add(error); }
        try { base.Dispose(true); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Sample subscriptions and controls could not be released.", failures);
    }
}
