using System.Drawing;

namespace ModernFormsNext.Designer.Layout;

internal sealed class ExecutablePreviewApprovalDialog : Form
{
    internal ExecutablePreviewApprovalDialog(string title, string notice, string approveText, string cancelText,
        string digest, IReadOnlyList<string> artifacts)
    {
        Text = title;
        Name = "ExecutablePreviewApprovalDialog";
        Size = new Size(820, 462);
        StartPosition = FormStartPosition.CenterParent;
        Controls.Add(new Label { Left = 24, Top = 22, Width = 740, Height = 48, Text = notice });
        Controls.Add(new TextBox
        {
            Name = "PreviewBuildManifest",
            Left = 24, Top = 77, Width = 740, Height = 274, MultiLine = true, ReadOnly = true,
            Text = $"SHA-256 {digest}\r\n\r\n" + string.Join("\r\n", artifacts)
        });
        var approve = Controls.Add(new Button { Name = "ApprovePreviewBuild", Left = 454, Top = 368,
            Width = 158, Height = 30, Text = approveText });
        var cancel = Controls.Add(new Button { Name = "CancelPreviewBuild", Left = 624, Top = 368,
            Width = 140, Height = 30, Text = cancelText });
        approve.Click += (_, _) => DialogResult = DialogResult.Yes;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
    }
}
