namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerImagePickerDialog : Form
{
    private static readonly string[] ImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".webp"
    ];

    private readonly List<string> imagePaths = [];
    private readonly ListBox imageList;
    private readonly TextBox selectedPath;
    private readonly string? projectDirectory;

    public DesignerImagePickerDialog(string? designDocumentPath, string? currentPath)
    {
        Text = "Select Image";
        Name = "DesignerImagePickerDialog";
        Size = new System.Drawing.Size(620, 420);
        StartPosition = FormStartPosition.CenterParent;

        projectDirectory = GetProjectDirectory(designDocumentPath);

        Controls.Add(new Label
        {
            Left = 16,
            Top = 14,
            Width = 560,
            Height = 22,
            Text = projectDirectory is null
                ? "Project images"
                : $"Project images: {projectDirectory}"
        });

        imageList = Controls.Add(new ListBox
        {
            Left = 16,
            Top = 44,
            Width = 570,
            Height = 230
        });

        Controls.Add(new Label
        {
            Left = 16,
            Top = 286,
            Width = 90,
            Height = 22,
            Text = "Selected"
        });

        selectedPath = Controls.Add(new TextBox
        {
            Left = 110,
            Top = 282,
            Width = 360,
            Height = 26,
            Text = currentPath ?? string.Empty
        });

        var browse = Controls.Add(new Button
        {
            Left = 482,
            Top = 282,
            Width = 104,
            Height = 26,
            Text = "Browse..."
        });

        var ok = Controls.Add(new Button
        {
            Left = 398,
            Top = 330,
            Width = 88,
            Height = 30,
            Text = "OK"
        });

        var cancel = Controls.Add(new Button
        {
            Left = 498,
            Top = 330,
            Width = 88,
            Height = 30,
            Text = "Cancel"
        });

        LoadProjectImages();

        imageList.SelectedIndexChanged += (_, _) =>
        {
            if (imageList.SelectedIndex >= 0 && imageList.SelectedIndex < imagePaths.Count)
                selectedPath.Text = imagePaths[imageList.SelectedIndex];
        };
        browse.Click += async (_, _) => await BrowseForImage();
        ok.Click += (_, _) =>
        {
            SelectedImagePath = selectedPath.Text.Trim();
            DialogResult = DialogResult.OK;
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
    }

    public string? SelectedImagePath { get; private set; }

    private void LoadProjectImages()
    {
        imagePaths.Clear();
        imageList.Items.Clear();

        if (projectDirectory is null || !Directory.Exists(projectDirectory))
        {
            imageList.Items.Add("No project directory was found.");
            return;
        }

        foreach (var path in Directory.EnumerateFiles(projectDirectory, "*.*", SearchOption.AllDirectories)
                     .Where(path => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                     .Take(500))
        {
            imagePaths.Add(path);
            imageList.Items.Add(Path.GetRelativePath(projectDirectory, path));
        }

        if (imagePaths.Count == 0)
            imageList.Items.Add("No image files were found in the project.");
    }

    private async Task BrowseForImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Image",
            InitialDirectory = projectDirectory
        };
        dialog.AddFilter("Image files", "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp");
        dialog.AddFilter("All files", "*.*");

        if (await dialog.ShowDialog(this) == DialogResult.OK && dialog.FileName is { } fileName)
            selectedPath.Text = fileName;
    }

    private static string? GetProjectDirectory(string? designDocumentPath)
    {
        if (string.IsNullOrWhiteSpace(designDocumentPath))
            return null;

        var directory = Path.GetDirectoryName(designDocumentPath);
        return string.IsNullOrWhiteSpace(directory) ? null : directory;
    }
}
