using ModernFormsNext.WindowKit.Input;
using WindowInsets = ModernFormsNext.WindowKit.WindowInsets;

namespace ModernFormsNext.CrossPlatform.Sample;

public sealed partial class MainPage
{
    private (Control Control, int Height)[] textInputDemoRows = [];
    private TextBox[] demoEditors = [];
    private TextBox? lastInputEditor;
    private Label compositionLabel = null!;
    private int keyboardOcclusion;
    private bool scrollingCaret;
    private bool textInputDemoDisposed;

    // The Android host lends its current canonical session lookup, then clears it on detach.
    // Windows obtains the same client from the containing Form. The page never owns a session.
    internal Func<ITextInputClient?>? TextInputClientProvider { get; set; }
    private ITextInputClient? CurrentClient => TextInputClientProvider is { } provider
        ? provider() : FindForm()?.TextInputClient;

    private void InitializeTextInputDemo()
    {
        // Control exposes disposal through its public event; the framework's IsDisposed
        // property is internal. Release the borrowed session lookup when this page ends.
        Disposed += (_, _) =>
        {
            textInputDemoDisposed = true;
            TextInputClientProvider = null;
            lastInputEditor = null;
        };
        nameTextBox.Name = "ImeSingleLine";
        nameTextBox.TextInputOptions = new TextInputOptions
        {
            Capitalization = TextInputCapitalization.Words,
            Action = TextInputAction.Next
        };
        multiLineTextBox.Name = "ImeMultiline";
        multiLineTextBox.TextInputOptions = new TextInputOptions
        {
            Capitalization = TextInputCapitalization.Sentences
        };
        var richEditor = new RichTextBox
        {
            Name = "ImeRichText",
            MultiLine = true,
            Text = "RichTextBox: zażółć · 你好 · 👋",
            TextInputOptions = new TextInputOptions { Action = TextInputAction.Done }
        };
        var markdownEditor = new MarkdownEditor
        {
            Name = "ImeMarkdown",
            ViewMode = MarkdownEditorViewMode.Editor,
            ShowToolbar = false,
            Markdown = "# Markdown IME\nPolski · 日本語 · 👋"
        };
        // The actual public child tree contains the shared RichTextBox-derived source editor.
        // Configure that existing editor; the Markdown preview is not another input client.
        var markdownSource = EnumerateInputControls(markdownEditor).OfType<RichTextBox>().Single();
        markdownSource.Name = "ImeMarkdownSource";
        markdownSource.TextInputOptions = new TextInputOptions { AutoCorrect = false, Action = TextInputAction.Done };
        demoEditors = [nameTextBox, multiLineTextBox, richEditor, markdownSource];
        lastInputEditor = nameTextBox;
        compositionLabel = CreateLabel("Composition: none; ranges use UTF-16. No entered text is logged.");
        compositionLabel.Name = "ImeCompositionMetadata";
        foreach (var editor in demoEditors)
        {
            editor.GotFocus += (_, _) => lastInputEditor = editor;
            editor.Disposed += (_, _) =>
            {
                if (ReferenceEquals(lastInputEditor, editor)) lastInputEditor = null;
            };
            editor.TextCompositionChanged += (_, e) =>
            {
                compositionLabel.Text = $"Composition: {e.Stage}; range {e.Start}..{e.End}; revision {e.Revision}";
                ScrollCurrentCaretIntoView();
            };
        }
        TrackFocus(richEditor, "RichTextBox");
        TrackFocus(markdownSource, "Markdown source editor");

        var next = new Button { Text = "Focus next editor" };
        next.Click += (_, _) =>
        {
            var index = Array.IndexOf(demoEditors, lastInputEditor);
            demoEditors[(index + 1) % demoEditors.Length].Select();
            ScrollCurrentCaretIntoView();
        };
        var cancel = new Button { Text = "Compose + cancel" };
        cancel.Click += (_, _) => RunCompositionDemo(cancel: true);
        var finish = new Button { Text = "Compose + finish" };
        finish.Click += (_, _) => RunCompositionDemo(cancel: false);
        textInputDemoRows =
        [
            (CreateLabel("Rich text — the keyboard Done action dismisses input"), 28),
            (richEditor, 92),
            (CreateLabel("Markdown source — correction disabled, same composition pipeline"), 40),
            (markdownEditor, 140),
            (compositionLabel, 48),
            (CreateLabel("Gboard: compose, replace a selection and move focus. Demo buttons insert a provisional fragment in the last editor, then cancel or finish it."), 70),
            (next, 38), (cancel, 38), (finish, 38)
        ];
    }

    private void RunCompositionDemo(bool cancel)
    {
        if (textInputDemoDisposed || Disposing || lastInputEditor is not { Disposing: false } editor) return;
        editor.Select();
        var client = CurrentClient;
        if (client?.SetComposingText("demo 👋") != true) return;
        if (cancel) client.CancelComposition();
        else client.FinishComposition();
        ScrollCurrentCaretIntoView();
    }

    internal void UpdateKeyboardOcclusion(WindowInsets insets)
    {
        // SafeArea already constrains the shared root. Only its remaining overlap with the IME
        // is reserved in this sample's scroll viewport; this is an application policy, not a
        // change to framework layout. API 23–29 cannot supply typed IME occlusion here.
        var bottom = (int)Math.Ceiling(Math.Clamp(insets.Ime.Bottom - insets.SafeArea.Bottom, 0, Height));
        if (bottom == keyboardOcclusion) return;
        keyboardOcclusion = bottom;
        scrollArea.SetBounds(0, 0, Width, Math.Max(0, Height - bottom));
        ArrangeControls();
        ScrollCurrentCaretIntoView();
    }

    internal void ScrollCurrentCaretIntoView()
    {
        if (textInputDemoDisposed || Disposing || scrollingCaret || scrollArea.Height <= 0) return;
        scrollingCaret = true;
        try
        {
            var state = CurrentClient?.GetState(0);
            if (state is null) return;
            var originY = 0;
            for (Control? current = this; current is not null; current = current.Parent)
                originY += current.Top;
            var top = state.CaretRectangle.Y - originY;
            var bottom = top + state.CaretRectangle.Height;
            const int margin = 12;
            var delta = bottom > scrollArea.Height - margin ? bottom - scrollArea.Height + margin
                : top < margin ? top - margin : 0;
            var scroll = scrollArea.VerticalScrollProperties;
            var value = (int)Math.Clamp(scroll.Value + Math.Ceiling(delta), scroll.Minimum, scroll.Maximum);
            if (value != scroll.Value) scroll.Value = value;
        }
        finally { scrollingCaret = false; }
    }

    private static IEnumerable<Control> EnumerateInputControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateInputControls(child)) yield return descendant;
        }
    }
}
