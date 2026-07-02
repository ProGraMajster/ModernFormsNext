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
    private readonly DesignerLayoutEngine layoutEngine = new();

    public DesignerStatusBar(DesignerSession state, ModernFormsDesignerOptions options)
    {
        this.state = state;
        this.options = options;
        Height = 24;
        Style.BackgroundColor = new SKColor(0, 99, 177);
        state.SelectionChanged += (_, _) => Invalidate();
        state.DocumentChanged += (_, _) => Invalidate();
        state.PointerPositionChanged += (_, _) => Invalidate();
        state.SettingsChanged += (_, _) => Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var selection = state.SelectedNode is null ? state.Document.FormName : state.SelectedNode.Name;
        var layout = layoutEngine.Layout(state.Document);
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
        var text = $"{saveState} | {document} | {T("Render")}: {state.ControlRenderMode} | {T("Selection")}: {selection} | {T("Position")}: {position} | {T("Size")}: {size}{dockText} | {T("Pointer")}: {pointer}";

        e.Canvas.FillRectangle(ClientRectangle, new SKColor(0, 99, 177));
        e.Canvas.DrawText(
            text,
            Theme.UIFont,
            e.LogicalToDeviceUnits(Theme.FontSize),
            new System.Drawing.Rectangle(e.LogicalToDeviceUnits(10), 0, e.LogicalToDeviceUnits(Math.Max(1, Width - 20)), e.LogicalToDeviceUnits(Height)),
            SKColors.White,
            ContentAlignment.MiddleLeft,
            maxLines: 1,
            ellipsis: true);
    }

    private string T(string key) => DesignerText.Get(key, options.Language);
}
