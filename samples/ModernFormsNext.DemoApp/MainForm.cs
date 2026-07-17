using ModernFormsNext;

namespace ModernFormsNext.DemoApp;

/// <summary>
/// Main window of the ModernFormsNext reference application.
/// </summary>
/// <remarks>
/// This form intentionally contains only minimal starter content. Control demos and visual
/// regression checks belong in ControlGallery, not in this reference/template application.
/// </remarks>
public sealed class MainForm : Form
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainForm"/> class.
    /// </summary>
    public MainForm()
    {
        Text = "ModernFormsNext App";
        this.Size = new System.Drawing.Size(900, 600);

        var button = new Button
        {
            Text = "Click me",
            Left = 260,
            Top = 60,
            Width = 120,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };

        button.Top = this.ClientSize.Height / 2 - button.Height / 2;
        button.Left = this.ClientSize.Width / 2 - button.Width / 2;

        button.Click += (_, _) =>
        {
            button.Text = "Button clicked";
        };

        Controls.Add(button);
    }
}
