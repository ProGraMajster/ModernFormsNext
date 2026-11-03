using ModernFormsNext.Designer.Localization;
using ModernFormsNext.Designer.Services;
using SkiaSharp;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerImagePickerDialog : Form
{
    private readonly ListBox assets;
    private readonly Label status;
    private readonly PictureBox preview;
    private readonly Button select;
    private readonly string? root;
    private IReadOnlyList<string> ids = [];
    private SKBitmap? bitmap;

    public DesignerImagePickerDialog(string? projectPath, string? documentPath, string? currentId)
    {
        Text = T("ProjectAssets");
        Name = "DesignerImagePickerDialog";
        Size = new System.Drawing.Size(660, 420);
        StartPosition = FormStartPosition.CenterParent;
        string? rootError = null;
        try
        {
            root = DesignerProjectAssets.GetRoot(projectPath, documentPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            rootError = exception.Message;
        }

        assets = Controls.Add(new ListBox { Left = 16, Top = 16, Width = 370, Height = 254 });
        preview = Controls.Add(new PictureBox
        {
            Left = 400, Top = 16, Width = 224, Height = 254, SizeMode = PictureBoxSizeMode.Zoom
        });
        status = Controls.Add(new Label { Left = 16, Top = 276, Width = 608, Height = 24 });
        var embedded = Controls.Add(new CheckBox
        {
            Left = 16, Top = 308, Width = 220, Height = 28, Text = T("EmbedAsset"), Checked = true
        });
        var import = Controls.Add(new Button
        {
            Left = 240, Top = 308, Width = 122, Height = 28, Text = T("ImportAsset"), Enabled = root is not null
        });
        select = Controls.Add(new Button { Left = 422, Top = 308, Width = 94, Height = 28, Text = T("Ok") });
        var cancel = Controls.Add(new Button { Left = 530, Top = 308, Width = 94, Height = 28, Text = T("Cancel") });

        assets.SelectedIndexChanged += (_, _) => UpdatePreview();
        import.Click += async (_, _) => await ImportAsync(embedded.Checked);
        select.Click += (_, _) =>
        {
            if (!select.Enabled || assets.SelectedIndex < 0)
                return;
            SelectedId = ids[assets.SelectedIndex];
            DialogResult = DialogResult.OK;
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        Reload(currentId);
        if (rootError is not null)
            status.Text = rootError;
    }

    public string? SelectedId { get; private set; }

    private void Reload(string? currentId)
    {
        ids = [];
        assets.Items.Clear();
        UpdatePreview();
        if (root is null)
        {
            status.Text = T("NoProjectPath");
            return;
        }

        try
        {
            ids = DesignerProjectAssets.Browse(root, out bool truncated);
            foreach (var id in ids)
                assets.Items.Add(id);
            for (int index = 0; index < ids.Count; index++)
            {
                if (ids[index] != currentId)
                    continue;
                assets.SelectedIndex = index;
                return;
            }

            status.Text = currentId is not null ? T("AssetMissing")
                : truncated ? T("AssetScanLimit") : T("AssetChoose");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            status.Text = exception.Message;
        }
    }

    private void UpdatePreview()
    {
        preview.Image = null;
        bitmap?.Dispose();
        bitmap = null;
        select.Enabled = false;
        if (root is null || assets.SelectedIndex < 0 || assets.SelectedIndex >= ids.Count)
            return;

        var id = ids[assets.SelectedIndex];
        try
        {
            bitmap = DesignerProjectAssets.ReadImage(root, id);
            preview.Image = bitmap;
            status.Text = id;
            select.Enabled = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            status.Text = exception.Message;
        }
    }

    private async Task ImportAsync(bool embedded)
    {
        if (root is null)
            return;

        var picker = new OpenFileDialog { Title = T("ImportAsset"), InitialDirectory = root };
        picker.AddFilter(T("AssetImages"), "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp", "*.ico");
        picker.AddFilter(T("AssetFiles"), "*.*");
        if (await picker.ShowDialog(this) != DialogResult.OK || picker.FileName is not { } source)
            return;

        try
        {
            Reload(DesignerProjectAssets.Import(root, source, embedded));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            status.Text = exception.Message;
        }
    }

    private static string T(string key) => DesignerText.Get(key, DesignerLanguage.Auto);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            preview.Image = null;
            bitmap?.Dispose();
            bitmap = null;
        }
        base.Dispose(disposing);
    }
}
