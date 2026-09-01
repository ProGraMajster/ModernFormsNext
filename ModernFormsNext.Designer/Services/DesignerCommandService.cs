using ModernFormsNext.Designer.Surface;
using SkiaSharp;
using System.Drawing;
using ModernFormsNext.Designing;
using ModernFormsNext.Designer.Layout;

namespace ModernFormsNext.Designer.Services;

internal sealed class DesignerCommandService : IDisposable
{
    private static long nextCommandId;
    private readonly DesignerSession state;
    private readonly DesignerFileService files;
    private readonly ModernFormsDesignerOptions options;
    private readonly DesignerPersistenceCoordinator? persistence;

    internal DesignerCommandService(
        DesignerSession state,
        DesignerFileService files,
        ModernFormsDesignerOptions options)
    {
        this.state = state;
        this.files = files;
        this.options = options;
    }

    public DesignerCommandService(
        DesignerSession state,
        DesignerFileService files,
        ModernFormsDesignerOptions options,
        DesignerPersistenceCoordinator persistence)
    {
        this.state = state;
        this.files = files;
        this.options = options;
        this.persistence = persistence;
    }

    public void NewDocument() => state.NewDocument();

    public async void OpenDesignDocument(Form? owner)
    {
        if (owner is null)
        {
            state.Log("Cannot open .mfdesign: no owner form was found.");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Open ModernFormsNext design document"
        };
        dialog.AddFilter("ModernFormsNext design files", "*.mfdesign");
        dialog.AddFilter("All files", "*.*");

        if (await dialog.ShowDialog(owner) != DialogResult.OK || dialog.FileName is not { } path)
            return;

        try
        {
            var document = files.LoadDesignDocument(path);
            state.OpenDocument(document, path);
            state.Log($"Opened {path}.");
        }
        catch (Exception ex)
        {
            state.Log($"Could not open {path}: {ex.Message}");
        }
    }

    public void AddPanel() => state.AddControl("Panel");

    public void AddButton() => state.AddControl("Button");

    public void AddLabel() => state.AddControl("Label");

    public void AddTextBox() => state.AddControl("TextBox");

    public bool Undo() => Undo("Toolbar");

    internal bool Undo(string source)
        => ExecuteHistoryCommand("UNDO", source, state.Transactions.Undo);

    public bool Redo() => Redo("Toolbar");

    internal bool Redo(string source)
        => ExecuteHistoryCommand("REDO", source, state.Transactions.Redo);

    public bool Cut() => state.CutSelectedNode();

    public bool Copy() => state.CopySelectedNode();

    public bool Paste() => state.PasteCopiedNode();

    public bool Duplicate() => state.DuplicateSelectedNode();

    public bool CanCut => state.CanCutSelectedNode;

    public bool CanCopy => state.CanCopySelectedNode;

    public bool CanPaste => state.CanPasteCopiedNode;

    public bool CanDuplicate => state.CanDuplicateSelectedNode;

    private bool ExecuteHistoryCommand(string command, string source, Func<bool> execute)
    {
        var commandId = Interlocked.Increment(ref nextCommandId);
        state.Log($"{command}_REQUEST source={source} id={commandId} {DescribeHistoryState()}");

        var executed = execute();
        state.Log($"{command}_EXECUTED id={commandId} Executed={executed} {DescribeHistoryState()}");
        state.Log($"{command}_COMPLETED id={commandId} {DescribeHistoryState()}");
        return executed;
    }

    private string DescribeHistoryState()
    {
        var selected = state.SelectedNode;
        var bounds = selected is null
            ? "<none>"
            : $"{selected.Bounds.X},{selected.Bounds.Y},{selected.Bounds.Width},{selected.Bounds.Height}";
        return $"Revision={state.CurrentHistory.CurrentRevision} " +
            $"UndoCount={state.CurrentHistory.UndoCount} RedoCount={state.CurrentHistory.RedoCount} " +
            $"ActiveTransaction={state.Transactions.CurrentTransactionId?.ToString() ?? "<none>"} " +
            $"ReplayMode={state.Transactions.ReplayMode} Selected={selected?.Name ?? "<form>"} Bounds={bounds}";
    }

    public void AddControlType(string typeName)
    {
        try
        {
            state.AddControl(typeName);
        }
        catch (InvalidOperationException exception)
        {
            state.Log(exception.Message);
        }
    }

    public void AddComponentType(string typeName)
        => state.Log($"Component {typeName} is listed in Toolbox, but the component tray is not implemented yet.");

    public void UseRuntimeRendering() => state.SetControlRenderMode(DesignerControlRenderMode.Runtime);

    public void UsePlaceholderRendering() => state.SetControlRenderMode(DesignerControlRenderMode.Placeholder);

    public void ToggleSolutionExplorer()
    {
        var layout = options.GetToolWindowLayout(DesignerToolWindowId.SolutionExplorer);
        layout.Mode = layout.Mode == DesignerToolWindowMode.Hidden
            ? DesignerToolWindowMode.Docked
            : DesignerToolWindowMode.Hidden;
        options.ShowSolutionExplorer = layout.Mode != DesignerToolWindowMode.Hidden;
        state.NotifySettingsChanged();
        state.Log(options.ShowSolutionExplorer
            ? "Solution Explorer panel is visible."
            : "Solution Explorer panel is hidden.");
    }

    public void DockSolutionExplorerRight()
    {
        var layout = options.GetToolWindowLayout(DesignerToolWindowId.SolutionExplorer);
        layout.Side = DesignerToolWindowSide.Right;
        layout.Mode = DesignerToolWindowMode.Docked;
        options.ShowSolutionExplorer = true;
        options.SolutionExplorerDockMode = ModernFormsNext.Designer.Layout.DesignerDockPanelMode.RightSplit;
        state.NotifySettingsChanged();
        state.Log("Solution Explorer panel is docked in the right tool area.");
    }

    public void AutoHideSolutionExplorer()
    {
        var layout = options.GetToolWindowLayout(DesignerToolWindowId.SolutionExplorer);
        layout.Mode = DesignerToolWindowMode.AutoHide;
        options.ShowSolutionExplorer = true;
        options.SolutionExplorerDockMode = ModernFormsNext.Designer.Layout.DesignerDockPanelMode.AutoHide;
        state.NotifySettingsChanged();
        state.Log("Solution Explorer panel is in auto-hide mode.");
    }

    public void TabSolutionExplorerWithProperties()
    {
        var solutionLayout = options.GetToolWindowLayout(DesignerToolWindowId.SolutionExplorer);
        var propertiesLayout = options.GetToolWindowLayout(DesignerToolWindowId.Properties);
        solutionLayout.Side = DesignerToolWindowSide.Right;
        solutionLayout.Mode = DesignerToolWindowMode.Tabbed;
        propertiesLayout.Side = DesignerToolWindowSide.Right;
        propertiesLayout.Mode = DesignerToolWindowMode.Tabbed;
        options.ShowSolutionExplorer = true;
        options.SolutionExplorerDockMode = ModernFormsNext.Designer.Layout.DesignerDockPanelMode.RightTabbed;
        state.NotifySettingsChanged();
        state.Log("Solution Explorer panel is tabbed with Properties.");
    }

    public void NarrowSolutionExplorer()
    {
        var layout = options.GetToolWindowLayout(DesignerToolWindowId.SolutionExplorer);
        layout.Size = Math.Max(240, layout.Size - 40);
        options.SolutionExplorerWidth = layout.Size;
        state.NotifySettingsChanged();
        state.Log($"Solution Explorer width: {options.SolutionExplorerWidth}.");
    }

    public void WidenSolutionExplorer()
    {
        var layout = options.GetToolWindowLayout(DesignerToolWindowId.SolutionExplorer);
        layout.Size = Math.Min(520, layout.Size + 40);
        options.SolutionExplorerWidth = layout.Size;
        state.NotifySettingsChanged();
        state.Log($"Solution Explorer width: {options.SolutionExplorerWidth}.");
    }

    public async void ShowSettingsDialog(Form? owner)
    {
        if (owner is null)
        {
            state.Log("Cannot open designer settings: no owner form was found.");
            return;
        }

        var dialog = new ModernFormsNext.Designer.Layout.DesignerSettingsDialog(options, state);
        await dialog.ShowDialog(owner);
    }

    public void ReportDiagnosticsLocation()
        => state.Log($"Designer diagnostics log: {DesignerDiagnosticLog.Path}");

    public void RunRenderingDiagnostics()
    {
        state.Log($"Running runtime rendering diagnostics. Log file: {DesignerDiagnosticLog.Path}");
        state.Log($"Current designer surface render mode: {state.ControlRenderMode}.");

        RunRenderingDiagnostic(new Panel
        {
            Name = "diagnosticPanel",
            Width = 180,
            Height = 80
        });

        RunRenderingDiagnostic(new Button
        {
            Name = "diagnosticButton",
            Text = "Button",
            Width = 120,
            Height = 32,
            TextAlign = ContentAlignment.MiddleCenter
        });

        var comboBox = new ComboBox
        {
            Name = "diagnosticComboBox",
            Width = 160,
            Height = 28
        };
        comboBox.Items.Add("ComboBox item");
        comboBox.SelectedIndex = 0;
        RunRenderingDiagnostic(comboBox);

        RunRenderingDiagnostic(new Label
        {
            Name = "diagnosticLabel",
            Text = "Label",
            Width = 120,
            Height = 24
        });

        RunRenderingDiagnostic(new TextBox
        {
            Name = "diagnosticTextBox",
            Text = "TextBox",
            Width = 160,
            Height = 28
        });
    }

    public async void SaveDesignDocument(Form? owner)
    {
        var path = state.CurrentDocumentPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = await SelectSavePath(owner, state.Document.ClassName + ".mfdesign");
            if (string.IsNullOrWhiteSpace(path))
                return;
        }

        SaveDesignDocument(path);
    }

    public DesignerSaveResult SaveDesignDocument(string path)
    {
        var result = Persistence.SaveActiveDocument(path);
        if (!result.Succeeded)
            state.Log("Save failed: " + result.Error);
        return result;
    }

    public void GenerateDesignerCode()
    {
        var result = Persistence.GenerateActiveDocumentCode();

        if (!result.Succeeded)
        {
            state.Log("Generation failed: " + string.Join("; ", result.Errors));
            return;
        }

        var preview = result.Code.Length > 300 ? result.Code[..300] + "..." : result.Code;
        state.Log($"Generated {state.Document.ClassName}.Designer.cs to {result.Path}.");
        state.Log(preview);
    }

    public async void RequestCloseDocument(int index, Form? owner)
    {
        if (index < 0 || index >= state.OpenDocuments.Count)
            return;
        if (state.OpenDocuments.Count == 1)
        {
            state.Log("The last designer document tab cannot be closed.");
            return;
        }

        var document = state.OpenDocuments[index];
        if (!await ConfirmDocumentClose(document, owner))
            return;

        // The index can only change through UI-thread document commands. Resolve it again after
        // an asynchronous system dialog so a stale click never closes a different tab.
        var currentIndex = FindDocumentIndex(document);
        if (currentIndex >= 0)
            state.CloseDocument(currentIndex);
    }

    public async Task<bool> ConfirmCloseAllDocuments(Form? owner)
    {
        if (owner is null)
            return false;

        var discardAfterConfirmation = new List<DesignerOpenDocument>();
        var confirmedStates = new List<(DesignerOpenDocument Document, long Generation, long Revision, bool IsDirty)>();
        foreach (var document in state.OpenDocuments.ToArray())
        {
            var index = FindDocumentIndex(document);
            if (index >= 0 && index != state.ActiveDocumentIndex)
                state.SwitchDocument(index);

            if (!document.IsDirty)
            {
                confirmedStates.Add((document, document.RevisionGeneration, document.History.CurrentRevision, document.IsDirty));
                continue;
            }

            var choice = await DesignerCloseConfirmationDialog.Show(owner, document.DisplayName);
            if (choice == DialogResult.Cancel)
                return false;
            if (choice == DialogResult.No)
            {
                discardAfterConfirmation.Add(document);
                confirmedStates.Add((document, document.RevisionGeneration, document.History.CurrentRevision, document.IsDirty));
                continue;
            }

            var path = document.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = await SelectSavePath(owner, document.DisplayName);
                if (string.IsNullOrWhiteSpace(path))
                    return false;
            }

            if (!SaveDesignDocument(path).Succeeded)
                return false;
            confirmedStates.Add((document, document.RevisionGeneration, document.History.CurrentRevision, document.IsDirty));
        }

        var currentDocuments = state.OpenDocuments;
        if (currentDocuments.Count != confirmedStates.Count
            || currentDocuments.Where((document, index) =>
                !ReferenceEquals(document, confirmedStates[index].Document)
                || document.RevisionGeneration != confirmedStates[index].Generation
                || document.History.CurrentRevision != confirmedStates[index].Revision
                || document.IsDirty != confirmedStates[index].IsDirty).Any())
        {
            state.Log("Designer documents changed while close confirmation was open. Review the current state and close again.");
            return false;
        }

        // Apply destructive Don't Save decisions only after every document has been confirmed.
        // A later Cancel therefore leaves all recovery scheduling intact.
        var preparedDiscards = new List<DesignerOpenDocument>();
        foreach (var document in discardAfterConfirmation)
        {
            if (Persistence.PrepareDocumentForDiscard(document, out var error))
            {
                preparedDiscards.Add(document);
                continue;
            }

            foreach (var prepared in preparedDiscards)
            {
                Persistence.ResumeDocumentProtection(prepared);
                if (!Persistence.EnsureRecoveryNow(prepared, out var restoreError))
                    state.Log($"Could not immediately restore recovery protection for {prepared.DisplayName}: {restoreError}");
            }
            state.Log($"Could not discard recovery for {document.DisplayName}: {error}");
            return false;
        }
        return true;
    }

    public void CheckForExternalChanges()
        => Persistence.CheckForExternalChanges();

    private async Task<bool> ConfirmDocumentClose(DesignerOpenDocument document, Form? owner)
    {
        if (!document.IsDirty)
            return true;

        if (owner is null)
        {
            state.Log($"Cannot close {document.DisplayName}: no owner form was found for the save confirmation.");
            return false;
        }

        var confirmedGeneration = document.RevisionGeneration;
        var confirmedRevision = document.History.CurrentRevision;
        var confirmedDirty = document.IsDirty;
        var choice = await DesignerCloseConfirmationDialog.Show(owner, document.DisplayName);
        if (!state.OpenDocuments.Contains(document)
            || document.RevisionGeneration != confirmedGeneration
            || document.History.CurrentRevision != confirmedRevision
            || document.IsDirty != confirmedDirty)
        {
            state.Log($"{document.DisplayName} changed while close confirmation was open. Review it and close again.");
            return false;
        }

        switch (choice)
        {
            case DialogResult.Yes:
            {
                var index = FindDocumentIndex(document);
                if (index >= 0 && index != state.ActiveDocumentIndex)
                    state.SwitchDocument(index);
                var path = document.Path;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = await SelectSavePath(owner, document.DisplayName);
                    if (string.IsNullOrWhiteSpace(path))
                        return false;
                }

                return SaveDesignDocument(path).Succeeded;
            }

            case DialogResult.No:
                if (Persistence.PrepareDocumentForDiscard(document, out var error))
                    return true;
                state.Log($"Could not discard recovery for {document.DisplayName}: {error}");
                return false;

            default:
                return false;
        }
    }

    private static async Task<string?> SelectSavePath(Form? owner, string suggestedName)
    {
        if (owner is null)
            return null;

        var dialog = new SaveFileDialog
        {
            Title = "Save ModernFormsNext design document",
            DefaultExtension = "mfdesign",
            FileName = suggestedName
        };
        dialog.AddFilter("ModernFormsNext design files", "*.mfdesign");
        dialog.AddFilter("All files", "*.*");
        return await dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
    }

    private int FindDocumentIndex(DesignerOpenDocument document)
    {
        for (var index = 0; index < state.OpenDocuments.Count; index++)
        {
            if (ReferenceEquals(state.OpenDocuments[index], document))
                return index;
        }

        return -1;
    }

    private void RunRenderingDiagnostic(Control control)
    {
        var width = Math.Max(1, control.Width);
        var height = Math.Max(1, control.Height);
        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        var args = new PaintEventArgs(info, canvas, scaling: 1);

        try
        {
            if (!RuntimeControlPainter.TryPaint(
                args,
                control,
                new Size(width, height),
                new Rectangle(0, 0, width, height),
                out var diagnostics,
                out var error))
            {
                state.Log($"Runtime diagnostic failed for {control.GetType().Name}: {error}");
                return;
            }

            state.Log(
                $"Runtime diagnostic rendered {control.GetType().Name}: " +
                $"{diagnostics.Width}x{diagnostics.Height}, visible samples {diagnostics.VisibleSampleCount}/{diagnostics.SampleCount}, " +
                $"opaque {diagnostics.OpaqueSampleCount}.");
        }
        catch (Exception ex)
        {
            state.Log($"Runtime diagnostic crashed for {control.GetType().Name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void ImportDesignerCode(string path)
    {
        if (!File.Exists(path))
        {
            state.Log($"Import failed: {path} does not exist.");
            return;
        }

        try
        {
            var result = files.ImportDesignerCode(
                path,
                new ModernFormsNext.CodeGeneration.Reverse.CSharpDesignerParseOptions
                {
                    RootKind = state.Document.RootKind
                });

            foreach (var diagnostic in result.Diagnostics)
            {
                var location = diagnostic.Line is null
                    ? string.Empty
                    : $" ({diagnostic.Line}:{diagnostic.Column})";
                state.Log($"Reverse sync {diagnostic.Severity}{location}: {diagnostic.Message}");
            }

            if (result.Success && result.Document is not null)
            {
                state.ReplaceDocument(result.Document, "Import designer code");
                state.Log($"Imported designer code from {path}.");
                return;
            }

            state.Log($"Import from {path} did not produce a usable design document.");
        }
        catch (Exception ex)
        {
            state.Log($"Import failed: {ex.Message}");
        }
    }

    private DesignerPersistenceCoordinator Persistence
        => persistence ?? throw new InvalidOperationException(
            "This Designer command service was created without persistence coordination.");

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
