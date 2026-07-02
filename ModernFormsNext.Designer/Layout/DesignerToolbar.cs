using ModernFormsNext;
using ModernFormsNext.Designer.Localization;
using ModernFormsNext.Designer.Services;
using SkiaSharp;
using System.Drawing;

namespace ModernFormsNext.Designer.Layout;

internal sealed class DesignerToolbar : Panel
{
    private readonly DesignerCommandService commands;
    private readonly ModernFormsDesignerOptions options;
    private readonly List<(Button Button, string Key)> localizedButtons = [];

    public DesignerToolbar(DesignerCommandService commands, ModernFormsDesignerOptions options)
    {
        this.commands = commands;
        this.options = options;
        Height = 42;
        Style.BackgroundColor = new SKColor(34, 39, 45);
        CreateButtons();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Canvas.FillRectangle(ClientRectangle, new SKColor(34, 39, 45));
        base.OnPaint(e);
        e.Canvas.DrawLine(0, Height - 1, Width, Height - 1, DesignerColors.PanelBorder);
    }

    private void CreateButtons()
    {
        var x = 8;
        AddButton("New", x, commands.NewDocument, 70);
        x += 78;
        AddButton("Open", x, () => commands.OpenDesignDocument(FindForm()), 80);
        x += 88;
        AddButton("AddPanel", x, commands.AddPanel, 100);
        x += 108;
        AddButton("AddButton", x, commands.AddButton, 105);
        x += 113;
        AddButton("AddLabel", x, commands.AddLabel, 95);
        x += 103;
        AddButton("AddTextBox", x, commands.AddTextBox, 115);
        x += 123;
        AddButton("SaveDesign", x, commands.SaveDesignDocument, 135);
        x += 143;
        AddButton("GenerateDesignerCode", x, commands.GenerateDesignerCode, 175);
        x += 183;
        AddSettingsButton(x);
    }

    public void RefreshTexts()
    {
        foreach (var (button, key) in localizedButtons)
            button.Text = T(key);

        Invalidate();
    }

    private void AddButton(string key, int left, Action action, int width)
    {
        var button = Controls.Add(new Button
        {
            Left = left,
            Top = 7,
            Width = width,
            Height = 28,
            Text = T(key),
            TextAlign = ContentAlignment.MiddleCenter
        });

        localizedButtons.Add((button, key));
        button.Style.BackgroundColor = new SKColor(52, 58, 66);
        button.Style.Border.Color = new SKColor(82, 91, 102);
        button.Style.Border.Width = 1;
        button.Click += (_, _) => action();
    }

    private void AddSettingsButton(int left)
    {
        var button = Controls.Add(new Button
        {
            Left = left,
            Top = 7,
            Width = 92,
            Height = 28,
            Text = T("Settings"),
            TextAlign = ContentAlignment.MiddleCenter
        });

        localizedButtons.Add((button, "Settings"));
        button.Style.BackgroundColor = new SKColor(52, 58, 66);
        button.Style.Border.Color = new SKColor(82, 91, 102);
        button.Style.Border.Width = 1;
        button.Click += (_, _) => commands.ShowSettingsDialog(button.FindForm());
    }

    private string T(string key) => DesignerText.Get(key, options.Language);
}
