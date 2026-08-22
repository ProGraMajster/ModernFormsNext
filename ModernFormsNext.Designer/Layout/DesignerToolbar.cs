using ModernFormsNext;
using ModernFormsNext.Designer.Localization;
using ModernFormsNext.Designer.Services;
using SkiaSharp;
using System.Drawing;

namespace ModernFormsNext.Designer.Layout;

internal sealed class DesignerToolbar : Panel
{
    private readonly DesignerCommandService commands;
    private readonly DesignerSession session;
    private readonly ModernFormsDesignerOptions options;
    private readonly List<(Button Button, string Key)> localizedButtons = [];
    private Button? undoButton;
    private Button? redoButton;
    private Button? cutButton;
    private Button? copyButton;
    private Button? pasteButton;
    private Button? duplicateButton;

    public DesignerToolbar(DesignerCommandService commands, DesignerSession session, ModernFormsDesignerOptions options)
    {
        this.commands = commands;
        this.session = session;
        this.options = options;
        Height = 42;
        Style.BackgroundColor = new SKColor(34, 39, 45);
        CreateButtons();
        session.Transactions.HistoryChanged += (_, _) => RefreshCommandButtons();
        session.DocumentTabsChanged += (_, _) => RefreshCommandButtons();
        session.DocumentChanged += (_, _) => RefreshCommandButtons();
        session.SelectionChanged += (_, _) => RefreshCommandButtons();
        session.ClipboardChanged += (_, _) => RefreshCommandButtons();
        RefreshCommandButtons();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var logicalPaintScope = DesignerLogicalPaintScope.Begin(e))
        {
            var logicalPaintArgs = logicalPaintScope.PaintArgs;
            logicalPaintArgs.Canvas.FillRectangle(0, 0, Width, Height, new SKColor(34, 39, 45));
            logicalPaintArgs.Canvas.DrawLine(0, Height - 1, Width, Height - 1, DesignerColors.PanelBorder);
        }

        // Buttons are rendered into device-pixel buffers by ModernFormsNext. Keeping their
        // composition outside the logical paint scope makes visual and pointer bounds identical.
        base.OnPaint(e);
    }

    private void CreateButtons()
    {
        var x = 8;
        AddButton("New", x, commands.NewDocument, 70);
        x += 78;
        AddButton("Open", x, () => commands.OpenDesignDocument(FindForm()), 80);
        x += 88;
        undoButton = AddButton("Undo", x, () => commands.Undo(), 76);
        x += 84;
        redoButton = AddButton("Redo", x, () => commands.Redo(), 76);
        x += 84;
        cutButton = AddButton("Cut", x, () => commands.Cut(), 62);
        x += 70;
        copyButton = AddButton("Copy", x, () => commands.Copy(), 68);
        x += 76;
        pasteButton = AddButton("Paste", x, () => commands.Paste(), 70);
        x += 78;
        duplicateButton = AddButton("Duplicate", x, () => commands.Duplicate(), 92);
        x += 100;
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

        RefreshCommandButtons();
        Invalidate();
    }

    private Button AddButton(string key, int left, Action action, int width)
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
        return button;
    }

    private void RefreshCommandButtons()
    {
        if (undoButton is not null)
            undoButton.Enabled = session.Transactions.CanUndo;
        if (redoButton is not null)
            redoButton.Enabled = session.Transactions.CanRedo;
        if (cutButton is not null)
            cutButton.Enabled = commands.CanCut;
        if (copyButton is not null)
            copyButton.Enabled = commands.CanCopy;
        if (pasteButton is not null)
            pasteButton.Enabled = commands.CanPaste;
        if (duplicateButton is not null)
            duplicateButton.Enabled = commands.CanDuplicate;

        Invalidate();
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
