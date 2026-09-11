using System;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Input;

namespace ControlGallery.Panels;

/// <summary>Demonstrates shared composition, keyboard hints and real popup/modal text ownership.</summary>
public sealed class TextInputPanel : Panel
{
    private readonly Label status;
    private PopupWindow? popup;
    private Form? dialog;

    /// <summary>Creates ordinary framework editors and optional explicit composition demonstrations.</summary>
    public TextInputPanel()
    {
        Controls.Add(new Label { Left = 16, Top = 12, Width = 760, Height = 44,
            Text = "Type with your keyboard or IME. Preview inserts provisional text; Cancel restores the selection." });
        Controls.Add(new Label { Left = 16, Top = 65, Width = 320, Text = "TextBox — email keyboard hint" });
        var plain = Controls.Add(new TextBox { Left = 16, Top = 95, Width = 330, Height = 38,
            TextInputOptions = new() { Scope = TextInputScope.Email, AutoCorrect = false, Action = TextInputAction.Next },
            AccessibleName = "Composition plain editor", AccessibleAutomationId = "controlgallery.ime.plain" });
        Controls.Add(new Label { Left = 370, Top = 65, Width = 330, Text = "Multiline — Unicode and selection" });
        var multiline = Controls.Add(new TextBox { Left = 370, Top = 95, Width = 350, Height = 110,
            MultiLine = true, Text = "Zażółć gęślą jaźń\n日本語 · 한글 · 😀",
            AccessibleName = "Composition multiline editor", AccessibleAutomationId = "controlgallery.ime.multiline" });
        Controls.Add(new Label { Left = 16, Top = 145, Width = 330, Text = "RichTextBox — select formatted text" });
        var rich = Controls.Add(new RichTextBox { Left = 16, Top = 176, Width = 330, Height = 110,
            Text = "Select this formatted fragment.", AccessibleName = "Composition rich editor",
            AccessibleAutomationId = "controlgallery.ime.rich" });
        rich.Select(0, 11);
        rich.SelectionFont = new Font("Segoe UI", 14, FontStyle.Bold);
        rich.DeselectAll();
        Controls.Add(new Label { Left = 370, Top = 216, Width = 350, Text = "Password — native privacy policy" });
        var password = Controls.Add(new TextBox { Left = 370, Top = 248, Width = 350, Height = 38,
            PasswordCharacter = '●', AccessibleName = "Composition password editor",
            AccessibleAutomationId = "controlgallery.ime.password" });
        Controls.Add(new Label { Left = 16, Top = 300, Width = 700, Text = "Markdown — one composition is one history edit" });
        var markdown = Controls.Add(new MarkdownEditor { Left = 16, Top = 331, Width = 704, Height = 170,
            ShowToolbar = false, ViewMode = MarkdownEditorViewMode.Editor,
            Markdown = "# Markdown\nSelect text, preview a composition, then cancel or finish." });
        status = Controls.Add(new Label { Left = 16, Top = 592, Width = 704, Height = 60,
            Text = "Focus an editor. Status shows ranges and event kinds only." });
        foreach (var editor in new[] { plain, multiline, rich, password }) Track(editor);
        TrackEditors(markdown);

        AddTool(16, 518, 124, "Preview 日本", () => CurrentClient()?.SetComposingText("日本"));
        AddTool(148, 518, 105, "Finish", () => CurrentClient()?.FinishComposition());
        AddTool(261, 518, 105, "Cancel", () => CurrentClient()?.CancelComposition());
        AddTool(374, 518, 150, "Next editor", () => CurrentClient()?.PerformEditorAction(TextInputAction.Next));
        AddTool(532, 518, 188, "Popup editor", ShowPopup);
        AddTool(16, 554, 160, "Modal editor", ShowDialog);
        AddTool(184, 554, 160, "Show keyboard", () => FindForm()?.RequestSoftwareKeyboard(true));
        AddTool(352, 554, 160, "Hide keyboard", () => FindForm()?.RequestSoftwareKeyboard(false));
    }

    private ITextInputClient? CurrentClient() => FindForm()?.TextInputClient;

    private void Track(TextBox editor)
    {
        editor.TextCompositionChanged += (_, e) => status.Text =
            $"{e.Stage}: composition [{e.Start}, {e.End}), revision {e.Revision}.";
    }

    private void TrackEditors(Control parent)
    {
        foreach (var child in parent.Controls) {
            if (child is TextBox editor) Track(editor);
            TrackEditors(child);
        }
    }

    private void AddTool(int left, int top, int width, string text, Action action)
    {
        var button = Controls.Add(new EditorTool { Left = left, Top = top, Width = width, Height = 30, Text = text });
        button.Click += (_, _) => action();
    }

    private void ShowPopup()
    {
        if (FindForm() is not { } owner) return;
        popup?.Close();
        popup = new PopupWindow(owner) { Size = new System.Drawing.Size(360, 110) };
        var editor = popup.Controls.Add(new TextBox { Dock = DockStyle.Fill, MultiLine = true,
            AccessibleName = "Popup composition editor" });
        popup.Show(this, 16, 420);
        editor.Select();
    }

    private void ShowDialog()
    {
        if (FindForm() is not { } owner) return;
        dialog = new Form { Text = "Composition in a modal editor", Size = new System.Drawing.Size(480, 220) };
        var editor = dialog.Controls.Add(new TextBox { Dock = DockStyle.Fill, MultiLine = true,
            AccessibleName = "Modal composition editor" });
        dialog.Shown += (_, _) => editor.Select();
        _ = dialog.ShowDialog(owner);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        try { if (disposing) { popup?.Close(); dialog?.Close(); } }
        finally { base.Dispose(disposing); }
    }

    private sealed class EditorTool : Button
    {
        public EditorTool()
        {
            TabStop = false;
            SetControlBehavior(ControlBehaviors.Selectable, false);
        }
    }
}
