using ModernFormsNext;
using ModernFormsNext.Designer.Localization;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using SkiaSharp;

namespace ModernFormsNext.Designer.Layout;

internal sealed class DesignerStatusBar : Panel
{
    private readonly DesignerSession state;
    private readonly ModernFormsDesignerOptions options;
    private readonly DesignerPersistenceCoordinator persistence;
    private readonly DesignerLayoutEngine layoutEngine = new();
    private Button? previewSettings;

    internal void AttachExecutablePreview(DesignerCommandService commands)
    {
        if (state.ExecutablePreview is not { } preview)
            return;
        var button = new Button
        {
            Name = "ExecutablePreviewSettings",
            Left = Math.Max(0, Width - 168), Top = 2, Width = 162, Height = 20,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        previewSettings = Controls.Add(button);
        button.Click += (_, _) => commands.ShowSettingsDialog(FindForm());
        preview.Changed += Refresh;
        state.SettingsChanged += (_, _) => Refresh();
        Refresh();

        void Refresh()
        {
            if (!button.IsDisposed)
                button.Text = T(preview.IsEnabled ? "ExecutablePreviewOn" : "ExecutablePreviewOff");
        }
    }

    public DesignerStatusBar(
        DesignerSession state,
        ModernFormsDesignerOptions options,
        DesignerPersistenceCoordinator persistence)
    {
        this.state = state;
        this.options = options;
        this.persistence = persistence;
        Height = 24;
        Style.BackgroundColor = new SKColor(0, 99, 177);
        state.SelectionChanged += (_, _) => Invalidate();
        state.DocumentChanged += (_, _) => Invalidate();
        state.PointerPositionChanged += (_, _) => Invalidate();
        state.SettingsChanged += (_, _) => Invalidate();
        persistence.StateChanged += (_, _) => Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var selection = state.SelectedNode is null ? state.Document.FormName : state.SelectedNode.Name;
        var layout = layoutEngine.Layout(state.Document, state.PreviewResourcePolicy);
        var selectedBounds = state.SelectedNode is null ? default : layout.GetEffectiveBounds(state.SelectedNode);
        var dock = state.SelectedNode is null ? DockStyle.None : DesignerLayoutProperties.GetDock(state.SelectedNode);
        var dockText = dock == DockStyle.None ? string.Empty : $" | Dock: {dock}";
        var position = state.SelectedNode is null ? "0, 0" : $"{selectedBounds.X}, {selectedBounds.Y}";
        var pointer = state.PointerPosition is { } point ? $"{point.X}, {point.Y}" : "-";
        var size = state.SelectedNode is null
            ? $"{state.Document.Size.Width} x {state.Document.Size.Height}"
            : $"{selectedBounds.Width} x {selectedBounds.Height}";
        var saveState = state.IsDirty ? T("Modified") : T("Saved");
        var document = state.CurrentDocumentPath is null
            ? state.Document.ClassName + ".mfdesign"
            : System.IO.Path.GetFileName(state.CurrentDocumentPath);
        var recovery = persistence.ActiveStatusText;
        var recoveryText = string.IsNullOrWhiteSpace(recovery) ? string.Empty : $" | {recovery}";
        var text = $"{saveState} | {document}{recoveryText} | {T("Render")}: {state.ControlRenderMode} | {T("Selection")}: {selection} | {T("Position")}: {position} | {T("Size")}: {size}{dockText} | {T("Pointer")}: {pointer}";

        using (var logicalPaintScope = DesignerLogicalPaintScope.Begin(e))
        {
            var logicalPaintArgs = logicalPaintScope.PaintArgs;
            logicalPaintArgs.Canvas.FillRectangle(0, 0, Width, Height, new SKColor(0, 99, 177));
            logicalPaintArgs.Canvas.DrawText(
                text,
                Theme.UIFont,
                logicalPaintArgs.LogicalToDeviceUnits(Theme.FontSize),
                new System.Drawing.Rectangle(
                    logicalPaintArgs.LogicalToDeviceUnits(10),
                    0,
                    logicalPaintArgs.LogicalToDeviceUnits(Math.Max(1, Width - (previewSettings is null ? 20 : 188))),
                    logicalPaintArgs.LogicalToDeviceUnits(Height)),
                SKColors.White,
                ContentAlignment.MiddleLeft,
                maxLines: 1,
                ellipsis: true);
        }

        base.OnPaint(e);
    }

    private string T(string key) => DesignerText.Get(key, options.Language);
}
