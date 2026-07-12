#nullable enable

using System;
using System.IO;
using ModernFormsNext;

namespace ControlGallery.Panels;

/// <summary>
/// Provides a manual test surface for MarkdownEditor source editing and native preview.
/// </summary>
public sealed class MarkdownEditorPanel : Panel
{
    private static readonly string SampleMarkdown = """"
        # MarkdownEditor

        Edit **bold**, *italic*, and ~~strikethrough~~ source while keeping every Markdown marker visible.

        > Preview is rendered by the existing native MarkdownViewer.

        - first item
          - nested item
        - [x] source highlighting
        - [ ] Markdown editor follow-up work

        ## Keyboard quality checks

        Put the caret at the end and press Enter:

        - continue this unordered item
        7. continue this ordered item
        - [ ] continue this task item
        > continue this quote

        Use Tab and Shift+Tab on these selected list lines:

        - first indentation target
        - second indentation target

        Put the caret directly after a marker and press Backspace:

        - marker boundary
        3. ordered marker boundary
        - [ ] task marker boundary
        > quote marker boundary
        ### heading marker boundary

        An empty list marker for testing Enter-to-exit is appended at the end of this sample.

        1. ordered item
        2. another ordered item

        [ModernFormsNext](https://github.com/ProGraMajster/ModernFormsNext)

        [Link with parentheses](<https://example.com/docs(version-1)>)

        [Zażółć gęślą jaźń](https://example.com/unicode)

        Edit this [existing link](https://old.example.com) by placing the caret inside it and using Ctrl+K.

        ![Local image](Images/icon.png "ControlGallery icon")

        Edit this existing image: ![Old image](Images/icon.png "Old title")

        HTTP image with fallback: ![Remote image](https://example.com/modernformsnext.png)

        ```csharp
        var editor = new MarkdownEditor
        {
            ViewMode = MarkdownEditorViewMode.Split,
            ShowToolbar = true
        };
        ```

        ```json
        {
            "editor": "MarkdownEditor",
            "preview": "MarkdownViewer"
        }
        ```

        ## International input

        zażółć gęślą jaźń

        ą ć ę ł ń ó ś ź ż

        This final paragraph makes the source long enough to exercise wrapping, selection, caret
        scrolling, preview debounce, undo, redo, and the vertical scroll bars on both surfaces.
        """" + "\n\n- ";

    private readonly MarkdownEditor editor;
    private readonly Label status;
    private readonly FlowLayoutPanel commands;
    private string lastRequest = "none";
    private string lastPreviewLink = "none";

    public MarkdownEditorPanel()
    {
        commands = new FlowLayoutPanel
        {
            Height = 70,
            WrapContents = true
        };

        editor = new MarkdownEditor
        {
            ViewMode = MarkdownEditorViewMode.Split,
            ShowToolbar = true,
            Markdown = SampleMarkdown
        };
        editor.PreviewViewer.DocumentStyle.ShowCodeBlockLanguage = true;

        status = new Label
        {
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Padding changes and child additions may synchronously lay out this panel.
        // Assign every field used by OnLayout before either operation can do so.
        Padding = new Padding(12);
        Controls.AddRange(commands, editor, status);

        AddButton(commands, "Editor", () => editor.ViewMode = MarkdownEditorViewMode.Editor);
        AddButton(commands, "Preview", () => editor.ViewMode = MarkdownEditorViewMode.Preview);
        AddButton(commands, "Split", () => editor.ViewMode = MarkdownEditorViewMode.Split);
        AddButton(commands, "Reset demo", () => editor.Markdown = SampleMarkdown);
        AddButton(commands, "Link", editor.RequestInsertLink);
        AddButton(commands, "Image", editor.RequestInsertImage);

        var readOnly = commands.Controls.Add(new CheckBox
        {
            Width = 105,
            Text = "Read only"
        });
        readOnly.CheckedChanged += (_, _) =>
        {
            editor.ReadOnly = readOnly.Checked;
            UpdateStatus();
        };

        var synchronize = commands.Controls.Add(new CheckBox
        {
            Width = 155,
            Text = "Synchronize scrolling",
            Checked = true
        });
        synchronize.CheckedChanged += (_, _) =>
        {
            editor.SynchronizeScrolling = synchronize.Checked;
            UpdateStatus();
        };

        editor.InsertLinkRequested += Editor_InsertLinkRequested;
        editor.InsertImageRequested += Editor_InsertImageRequested;
        editor.ImageInsertFailed += (_, e) =>
        {
            lastRequest = "Image failed: " + e.Message;
            UpdateStatus();
        };
        editor.PreviewLinkClicked += (_, e) =>
        {
            lastPreviewLink = e.Destination;
            UpdateStatus();
        };
        editor.MarkdownChanged += (_, _) => UpdateStatus();
        editor.ModifiedChanged += (_, _) => UpdateStatus();
        editor.SelectionChanged += (_, _) => UpdateStatus();
        UpdateStatus();
    }

    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        var bounds = PaddedClientRectangle;
        commands.SetBounds(bounds.Left, bounds.Top, bounds.Width, commands.Height);
        status.SetBounds(bounds.Left, Math.Max(bounds.Top, bounds.Bottom - status.Height), bounds.Width, status.Height);
        editor.SetBounds(
            bounds.Left,
            commands.Bottom,
            bounds.Width,
            Math.Max(0, status.Top - commands.Bottom));
    }

    private void AddButton(FlowLayoutPanel panel, string text, Action action)
    {
        var button = panel.Controls.Add(new Button
        {
            Width = Math.Max(62, text.Length * 9),
            Height = 30,
            Text = text
        });
        button.Click += (_, _) =>
        {
            action();
            UpdateStatus();
        };
    }

    private void UpdateStatus()
    {
        status.Text = $"Modified: {editor.Modified}   View: {editor.ViewMode}   Selection: {editor.SelectionStart}, "
            + $"{editor.SelectionLength}   Last request: {lastRequest}   Preview link: {lastPreviewLink}";
    }

    private async void Editor_InsertLinkRequested(object? sender, InsertLinkRequestEventArgs e)
    {
        using var deferral = e.GetDeferral();
        lastRequest = "InsertLinkRequested";
        UpdateStatus();

        var owner = FindForm();
        if (owner is null)
        {
            e.Cancel = true;
            return;
        }

        using var dialog = new MarkdownLinkDialog(e.SuggestedText, e.SuggestedUrl);
        if (await dialog.ShowDialog(owner) != DialogResult.OK)
        {
            e.Cancel = true;
            return;
        }

        e.Text = dialog.LinkText;
        e.Url = dialog.Url;
        e.Handled = true;
    }

    private async void Editor_InsertImageRequested(object? sender, InsertImageRequestEventArgs e)
    {
        using var deferral = e.GetDeferral();
        lastRequest = "InsertImageRequested";
        UpdateStatus();

        var owner = FindForm();
        if (owner is null)
        {
            e.Cancel = true;
            return;
        }

        using var dialog = new MarkdownImageDialog(e.Source, e.AltText, e.Title);
        if (await dialog.ShowDialog(owner) != DialogResult.OK)
        {
            e.Cancel = true;
            return;
        }

        e.Source = dialog.Source;
        e.AltText = dialog.AltText;
        e.Title = dialog.ImageTitle;
        e.SourceKind = dialog.SourceKind;
        if (dialog.SourceKind == MarkdownImageSourceKind.LocalFile)
        {
            e.AssetOptions = new MarkdownImageAssetOptions
            {
                DestinationDirectory = Path.Combine(AppContext.BaseDirectory, "MarkdownEditorAssets"),
                MarkdownBaseDirectory = AppContext.BaseDirectory,
                CollisionBehavior = dialog.CollisionBehavior
            };
            lastRequest = $"Copy image ({dialog.CollisionBehavior})";
        }
        else
        {
            lastRequest = "Insert image reference";
        }
        e.Handled = true;
    }
}
