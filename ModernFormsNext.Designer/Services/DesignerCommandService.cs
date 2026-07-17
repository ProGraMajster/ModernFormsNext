using ModernFormsNext.Designer.Surface;
using SkiaSharp;
using System.Drawing;
using ModernFormsNext.Designing;
using ModernFormsNext.Designer.Layout;

namespace ModernFormsNext.Designer.Services;

internal sealed class DesignerCommandService
{
    private readonly DesignerSession state;
    private readonly DesignerFileService files;
    private readonly ModernFormsDesignerOptions options;
    private bool isSaving;

    public DesignerCommandService(
        DesignerSession state,
        DesignerFileService files,
        ModernFormsDesignerOptions options)
    {
        this.state = state;
        this.files = files;
        this.options = options;
        this.state.DocumentChanged += HandleDocumentChanged;
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

    public void AddControlType(string typeName) => state.AddControl(typeName);

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

    public void SaveDesignDocument()
        => SaveDesignDocument(isAutoSave: false);

    private void SaveDesignDocument(bool isAutoSave)
    {
        if (isSaving)
            return;

        try
        {
            isSaving = true;
            var path = files.SaveDesignDocument(state.Document);
            state.MarkSaved(isAutoSave ? "Document auto-saved." : "Document saved.");

            if (isAutoSave)
                state.LogDiagnostic($"Auto-saved {state.Document.ClassName}.mfdesign to {path}.");
            else
                state.Log($"Saved {state.Document.ClassName}.mfdesign to {path}.");

            if (options.AutoGenerateDesignerCodeOnSave)
                GenerateDesignerCode(isAutoSave);
        }
        catch (Exception ex)
        {
            state.Log($"{(isAutoSave ? "Auto-save" : "Save")} failed: {ex.Message}");
        }
        finally
        {
            isSaving = false;
        }
    }

    public void GenerateDesignerCode()
        => GenerateDesignerCode(isAutoSave: false);

    private void GenerateDesignerCode(bool isAutoSave)
    {
        var result = files.GenerateDesignerCode(state.Document);

        if (!result.Succeeded)
        {
            state.Log("Generation failed: " + string.Join("; ", result.Errors));
            return;
        }

        var preview = result.Code.Length > 300 ? result.Code[..300] + "..." : result.Code;
        if (isAutoSave)
        {
            state.LogDiagnostic($"Auto-generated {state.Document.ClassName}.Designer.cs to {result.Path}.");
        }
        else
        {
            state.Log($"Generated {state.Document.ClassName}.Designer.cs to {result.Path}.");
            state.Log(preview);
        }
    }

    private void HandleDocumentChanged(object? sender, EventArgs e)
    {
        if (isSaving || !options.AutoSaveEnabled || !state.IsDirty)
            return;

        if (string.IsNullOrWhiteSpace(state.CurrentDocumentPath))
            return;

        SaveDesignDocument(isAutoSave: true);
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
            var result = files.ImportDesignerCode(path);

            foreach (var diagnostic in result.Diagnostics)
            {
                var location = diagnostic.Line is null
                    ? string.Empty
                    : $" ({diagnostic.Line}:{diagnostic.Column})";
                state.Log($"Reverse sync {diagnostic.Severity}{location}: {diagnostic.Message}");
            }

            if (result.Success && result.Document is not null)
            {
                state.LoadDocument(result.Document);
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
}
