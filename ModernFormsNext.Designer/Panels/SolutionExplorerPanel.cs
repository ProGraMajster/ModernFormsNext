using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using SkiaSharp;

namespace ModernFormsNext.Designer.Panels;

internal sealed class SolutionExplorerPanel : DesignerPanelBase
{
    private const int RowHeight = 23;
    private const int TreeTop = 36;
    private const int MaxRows = 600;

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "bin",
        "obj"
    };

    private readonly DesignerSession state;
    private readonly List<SolutionExplorerRow> rows = [];
    private int scrollOffset;

    public SolutionExplorerPanel(DesignerSession state, string title = "Solution Explorer", string noProjectPathText = "No project path is available.")
        : base(title)
    {
        this.state = state;
        NoProjectPathText = noProjectPathText;
        state.DocumentChanged += (_, _) =>
        {
            ClampScrollOffset();
            Invalidate();
        };
        SizeChanged += (_, _) =>
        {
            ClampScrollOffset();
            Invalidate();
        };
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        BuildRows();
        var maxOffset = GetMaxScrollOffset();

        if (maxOffset == 0)
            return;

        var delta = e.Delta.Y == 0 ? 0 : -Math.Sign(e.Delta.Y) * (RowHeight * 3);
        var nextOffset = Math.Clamp(scrollOffset + delta, 0, maxOffset);

        if (nextOffset == scrollOffset)
            return;

        scrollOffset = nextOffset;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
            return;

        BuildRows();

        var logicalPoint = DesignerDpiCoordinateConverter.DeviceToLogicalPoint(e.X, e.Y, Scaling);
        var rowIndex = (logicalPoint.Y - TreeTop + scrollOffset) / RowHeight;

        if (rowIndex < 0 || rowIndex >= rows.Count)
            return;

        TryOpenRow(rows[rowIndex]);
    }

    protected override void OnPaintContent(PaintEventArgs e)
    {
        BuildRows();
        ClampScrollOffset();

        e.Canvas.Save();
        e.Canvas.Clip(new System.Drawing.Rectangle(0, TreeTop, Width, Math.Max(1, Height - TreeTop)));

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var y = TreeTop + (i * RowHeight) - scrollOffset;

            if (y + RowHeight < TreeTop || y > Height)
                continue;

            var indent = 14 + (row.Depth * 18);
            e.Canvas.DrawText(
                row.Glyph,
                Theme.UIFont,
                e.LogicalToDeviceUnits(Theme.FontSize),
                new System.Drawing.Rectangle(e.LogicalToDeviceUnits(indent), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(22), e.LogicalToDeviceUnits(RowHeight)),
                DesignerColors.MutedText,
                ContentAlignment.MiddleCenter);

            e.Canvas.DrawText(
                row.Name,
                Theme.UIFont,
                e.LogicalToDeviceUnits(Theme.FontSize),
                new System.Drawing.Rectangle(e.LogicalToDeviceUnits(indent + 26), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(Math.Max(1, Width - indent - 34)), e.LogicalToDeviceUnits(RowHeight)),
                row.IsDocumentFile ? DesignerColors.Text : DesignerColors.MutedText,
                ContentAlignment.MiddleLeft,
                maxLines: 1,
                ellipsis: true);
        }

        if (rows.Count == 0)
        {
            e.Canvas.DrawText(
                NoProjectPathText,
                Theme.UIFont,
                e.LogicalToDeviceUnits(Theme.FontSize),
                new System.Drawing.Rectangle(e.LogicalToDeviceUnits(12), e.LogicalToDeviceUnits(TreeTop + 10), e.LogicalToDeviceUnits(Math.Max(1, Width - 24)), e.LogicalToDeviceUnits(24)),
                DesignerColors.MutedText,
                ContentAlignment.MiddleLeft);
        }

        e.Canvas.Restore();
        DrawScrollHint(e);
    }

    private string NoProjectPathText { get; }

    private void BuildRows()
    {
        rows.Clear();

        var documentPath = state.CurrentDocumentPath;
        var root = GetProjectRoot(state.CurrentProjectPath, documentPath);

        if (root is null)
            return;

        rows.Add(new SolutionExplorerRow(Path.GetFileName(root), "S", 0, false, root));
        AddDirectoryRows(root, root, depth: 1);
    }

    private void AddDirectoryRows(string root, string directory, int depth)
    {
        if (rows.Count >= MaxRows)
            return;

        foreach (var childDirectory in Directory.EnumerateDirectories(directory)
                     .Where(path => !SkippedDirectories.Contains(Path.GetFileName(path)))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new SolutionExplorerRow(Path.GetFileName(childDirectory), "[]", depth, false, childDirectory));
            AddDirectoryRows(root, childDirectory, depth + 1);

            if (rows.Count >= MaxRows)
                return;
        }

        foreach (var file in Directory.EnumerateFiles(directory)
                     .OrderBy(path => GetFileRank(path))
                     .ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new SolutionExplorerRow(Path.GetFileName(file), GetFileGlyph(file), depth, IsDocumentRelated(file), file));

            if (rows.Count >= MaxRows)
                return;
        }
    }

    private void TryOpenRow(SolutionExplorerRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Path) || !File.Exists(row.Path))
            return;

        var extension = Path.GetExtension(row.Path);
        var designPath = string.Equals(extension, ".mfdesign", StringComparison.OrdinalIgnoreCase)
            ? row.Path
            : string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(row.Path, ".mfdesign")
                : null;

        if (designPath is null)
            return;

        if (!File.Exists(designPath))
        {
            state.Log($"No ModernFormsNext design file exists for {Path.GetFileName(row.Path)}.");
            return;
        }

        try
        {
            var document = DesignDocumentSerializer.Default.Load(designPath);
            state.OpenDocument(document, designPath);
            state.Log($"Opened {Path.GetFileName(designPath)} from Solution Explorer.");
        }
        catch (Exception ex)
        {
            state.Log($"Could not open {Path.GetFileName(designPath)}: {ex.Message}");
        }
    }

    private bool IsDocumentRelated(string file)
    {
        if (state.CurrentDocumentPath is null)
            return false;

        var currentBase = Path.GetFileNameWithoutExtension(state.CurrentDocumentPath);
        return string.Equals(Path.GetFileNameWithoutExtension(file), currentBase, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetProjectRoot(string? projectPath, string? documentPath)
    {
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            if (File.Exists(projectPath))
                return Path.GetDirectoryName(projectPath);

            if (Directory.Exists(projectPath))
                return projectPath;
        }

        var directory = string.IsNullOrWhiteSpace(documentPath)
            ? null
            : Path.GetDirectoryName(documentPath);

        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (Directory.EnumerateFiles(directory, "*.csproj").Any())
                return directory;

            directory = Path.GetDirectoryName(directory);
        }

        return string.IsNullOrWhiteSpace(documentPath)
            ? null
            : Path.GetDirectoryName(documentPath);
    }

    private static int GetFileRank(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".csproj" => 0,
            ".cs" => 1,
            ".mfdesign" => 2,
            _ => 10
        };

    private static string GetFileGlyph(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".csproj" => "C#",
            ".cs" => "C#",
            ".mfdesign" => "D",
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" => "I",
            _ => "-"
        };

    private void ClampScrollOffset()
    {
        BuildRows();
        scrollOffset = Math.Clamp(scrollOffset, 0, GetMaxScrollOffset());
    }

    private int GetMaxScrollOffset()
    {
        var contentHeight = rows.Count * RowHeight;
        var viewportHeight = Math.Max(1, Height - TreeTop);
        return Math.Max(0, contentHeight - viewportHeight);
    }

    private void DrawScrollHint(PaintEventArgs e)
    {
        var contentHeight = rows.Count * RowHeight;
        var viewportHeight = Math.Max(1, Height - TreeTop);

        if (contentHeight <= viewportHeight)
            return;

        var trackTop = TreeTop;
        var trackHeight = Math.Max(1, Height - TreeTop - 4);
        var thumbHeight = Math.Max(24, trackHeight * viewportHeight / contentHeight);
        var maxOffset = Math.Max(1, contentHeight - viewportHeight);
        var thumbTop = trackTop + ((trackHeight - thumbHeight) * scrollOffset / maxOffset);
        e.Canvas.FillRectangle(Math.Max(0, Width - 5), thumbTop, 3, thumbHeight, DesignerColors.MutedText);
    }

    private sealed record SolutionExplorerRow(string Name, string Glyph, int Depth, bool IsDocumentFile, string? Path);
}
