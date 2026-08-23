using System.Drawing;

namespace ModernFormsNext.Designer.Layout;

/// <summary>
/// Presents the explicit Save, Don't Save, and Cancel decision required before a dirty Designer
/// document is closed.
/// </summary>
internal sealed class DesignerCloseConfirmationDialog : Form
{
    private DesignerCloseConfirmationDialog(string documentName)
    {
        Text = "Unsaved Designer changes";
        Name = nameof(DesignerCloseConfirmationDialog);
        Size = new Size(560, 210);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new Label
        {
            Left = 24,
            Top = 28,
            Width = 510,
            Height = 54,
            Multiline = true,
            Text = $"Save changes to {documentName}?\nChoosing Don't Save also discards its recovery copy."
        });

        var save = AddButton("Save", 246, DialogResult.Yes);
        var discard = AddButton("Don't Save", 338, DialogResult.No, width: 104);
        var cancel = AddButton("Cancel", 450, DialogResult.Cancel);
        Button AddButton(string text, int left, DialogResult result, int width = 84)
        {
            var button = Controls.Add(new Button
            {
                Left = left,
                Top = 124,
                Width = width,
                Height = 30,
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter
            });
            button.Click += (_, _) => DialogResult = result;
            return button;
        }
    }

    public static async Task<DialogResult> Show(Form owner, string documentName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        using var dialog = new DesignerCloseConfirmationDialog(documentName);
        return await dialog.ShowDialog(owner);
    }
}
