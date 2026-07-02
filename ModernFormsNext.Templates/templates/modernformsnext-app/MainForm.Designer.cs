using ModernFormsNext;

namespace MyApp;

/// <summary>
/// Contains designer-generated initialization for <see cref="MainForm"/>.
/// </summary>
public sealed partial class MainForm
{
    private Button button1 = null!;

    private void InitializeComponent()
    {
        this.button1 = new Button();

        this.button1.Name = "button1";
        this.button1.Text = "Click me";
        this.button1.Bounds = new System.Drawing.Rectangle(260, 60, 120, 40);
        this.button1.TextAlign = ContentAlignment.MiddleCenter;
        this.button1.Click += this.button1_Click;

        this.Controls.Add(this.button1);

        this.Name = "MainForm";
        this.Text = "ModernFormsNext App";
        this.Size = new System.Drawing.Size(900, 600);
    }
}
