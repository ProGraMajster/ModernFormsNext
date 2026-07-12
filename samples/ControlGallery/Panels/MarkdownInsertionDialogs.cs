#nullable enable

using System.Drawing;
using ModernFormsNext;

namespace ControlGallery.Panels;

internal sealed class MarkdownLinkDialog : Form
{
    private readonly TextBox textInput;
    private readonly TextBox urlInput;
    private readonly Label validation;

    public MarkdownLinkDialog(string text, string url)
    {
        Text = "Insert or edit link";
        Name = nameof(MarkdownLinkDialog);
        Size = new Size(520, 250);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new Label { Left = 20, Top = 50, Width = 90, Height = 24, Text = "Text" });
        textInput = Controls.Add(new TextBox { Left = 120, Top = 46, Width = 360, Height = 28, Text = text });
        Controls.Add(new Label { Left = 20, Top = 90, Width = 90, Height = 24, Text = "URL" });
        urlInput = Controls.Add(new TextBox { Left = 120, Top = 86, Width = 360, Height = 28, Text = url });
        validation = Controls.Add(new Label
        {
            Left = 120,
            Top = 120,
            Width = 360,
            Height = 24,
            Text = string.Empty
        });

        var ok = Controls.Add(new Button { Left = 300, Top = 160, Width = 86, Height = 30, Text = "OK" });
        Controls.Add(new Button
        {
            Left = 394,
            Top = 160,
            Width = 86,
            Height = 30,
            Text = "Cancel",
            DialogResult = DialogResult.Cancel
        });

        ok.Click += (_, _) => Confirm();
        Shown += (_, _) => textInput.Select();
    }

    public string LinkText => textInput.Text;

    public string Url => urlInput.Text;

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(urlInput.Text))
        {
            validation.Text = "URL cannot be empty.";
            urlInput.Select();
            return;
        }

        DialogResult = DialogResult.OK;
    }
}

internal sealed class MarkdownImageDialog : Form
{
    private readonly ComboBox collisionInput;
    private readonly TextBox sourceInput;
    private readonly TextBox altTextInput;
    private readonly TextBox titleInput;
    private readonly ComboBox sourceKindInput;
    private readonly Label validation;

    public MarkdownImageDialog(string source, string altText, string? title)
    {
        Text = "Insert or edit image";
        Name = nameof(MarkdownImageDialog);
        Size = new Size(640, 390);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new Label { Left = 20, Top = 50, Width = 100, Height = 24, Text = "Mode" });
        sourceKindInput = Controls.Add(new ComboBox { Left = 130, Top = 46, Width = 220, Height = 28 });
        sourceKindInput.Items.AddRange(["Reference (URL/path/data URI)", "Copy local file"]);
        sourceKindInput.SelectedIndex = 0;

        Controls.Add(new Label { Left = 20, Top = 90, Width = 100, Height = 24, Text = "Source" });
        sourceInput = Controls.Add(new TextBox { Left = 130, Top = 86, Width = 370, Height = 28, Text = source });
        var browse = Controls.Add(new Button { Left = 510, Top = 85, Width = 90, Height = 30, Text = "Browse..." });

        Controls.Add(new Label { Left = 20, Top = 130, Width = 100, Height = 24, Text = "Collision" });
        collisionInput = Controls.Add(new ComboBox { Left = 130, Top = 126, Width = 220, Height = 28 });
        collisionInput.Items.AddRange(["Unique name", "Cancel", "Overwrite", "Use existing"]);
        collisionInput.SelectedIndex = 0;

        Controls.Add(new Label { Left = 20, Top = 170, Width = 100, Height = 24, Text = "Alt text" });
        altTextInput = Controls.Add(new TextBox { Left = 130, Top = 166, Width = 470, Height = 28, Text = altText });
        Controls.Add(new Label { Left = 20, Top = 210, Width = 100, Height = 24, Text = "Title" });
        titleInput = Controls.Add(new TextBox
        {
            Left = 130,
            Top = 206,
            Width = 470,
            Height = 28,
            Text = title ?? string.Empty
        });
        validation = Controls.Add(new Label
        {
            Left = 130,
            Top = 240,
            Width = 470,
            Height = 24,
            Text = string.Empty
        });

        var ok = Controls.Add(new Button { Left = 420, Top = 282, Width = 86, Height = 30, Text = "OK" });
        Controls.Add(new Button
        {
            Left = 514,
            Top = 282,
            Width = 86,
            Height = 30,
            Text = "Cancel",
            DialogResult = DialogResult.Cancel
        });

        browse.Click += Browse_Click;
        ok.Click += (_, _) => Confirm();
        Shown += (_, _) => sourceInput.Select();
    }

    public string Source => sourceInput.Text;

    public string AltText => altTextInput.Text;

    public string? ImageTitle => string.IsNullOrEmpty(titleInput.Text) ? null : titleInput.Text;

    public MarkdownImageAssetCollisionBehavior CollisionBehavior => collisionInput.SelectedIndex switch
    {
        1 => MarkdownImageAssetCollisionBehavior.Cancel,
        2 => MarkdownImageAssetCollisionBehavior.Overwrite,
        3 => MarkdownImageAssetCollisionBehavior.UseExisting,
        _ => MarkdownImageAssetCollisionBehavior.GenerateUniqueName
    };

    public MarkdownImageSourceKind SourceKind
        => sourceKindInput.SelectedIndex == 1 ? MarkdownImageSourceKind.LocalFile : MarkdownImageSourceKind.Reference;

    private async void Browse_Click(object? sender, System.EventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Choose image asset" };
        picker.AddFilter("Raster images", "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.bmp");
        if (await picker.ShowDialog(this) != DialogResult.OK || picker.FileName is null)
            return;

        sourceKindInput.SelectedIndex = 1;
        sourceInput.Text = picker.FileName;
        sourceInput.Select();
    }

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(sourceInput.Text))
        {
            validation.Text = "Source cannot be empty.";
            sourceInput.Select();
            return;
        }

        DialogResult = DialogResult.OK;
    }
}
