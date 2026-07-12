using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModernFormsNext.Documents;

namespace ModernFormsNext;

/// <summary>
/// Provides native, multiline editing of Markdown source with formatting commands and an optional
/// <see cref="MarkdownViewer"/> preview.
/// </summary>
/// <remarks>
/// <para>
/// The editable surface reuses the platform-neutral <see cref="RichTextBox"/> editing core for
/// caret movement, selection, clipboard access, scrolling, keyboard input, and backend IME input.
/// Source highlighting changes only presentation runs; <see cref="Markdown"/> always contains the
/// original Markdown characters.
/// </para>
/// <para>
/// Preview and split modes use the existing <see cref="MarkdownViewer"/> pipeline. This control is
/// a source editor, not a WYSIWYG editor, HTML editor, or native platform text control.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var editor = new MarkdownEditor
/// {
///     Markdown = "# Hello",
///     ViewMode = MarkdownEditorViewMode.Split,
///     ShowToolbar = true
/// };
///
/// editor.ToggleBold();
/// </code>
/// </example>
[DefaultEvent(nameof(MarkdownChanged))]
[DefaultProperty(nameof(Markdown))]
[DisplayName("MarkdownEditor")]
[Category("Common")]
[Description("Edits Markdown source and optionally displays a native MarkdownViewer preview.")]
public partial class MarkdownEditor : Panel
{
    private const int DefaultPreviewUpdateDelay = 220;
    private readonly MarkdownEditorHistory history = new();
    private readonly MarkdownEditorTextBox editorSurface;
    private readonly MarkdownViewer previewViewer;
    private readonly SplitContainer splitContainer;
    private readonly ToolBar toolbar;
    private readonly Timer previewTimer;
    private readonly List<MenuItem> editingToolbarItems = new();
    private readonly Dictionary<MarkdownToolbarCommand, MenuItem> toolbarItems = new();
    private int editDepth;
    private string editBeforeText = string.Empty;
    private MarkdownSelection editBeforeSelection;
    private MarkdownEditKind editKind;
    private int cleanHistoryPosition;
    private bool applyingHistory;
    private bool disposed;
    private bool modified;
    private bool previewDelayIsImmediate;
    private bool previewIsDirty = true;
    private bool programmaticTextChange;
    private int previewUpdateDelayMilliseconds = DefaultPreviewUpdateDelay;
    private bool showToolbar = true;
    private float splitRatio = 0.5f;
    private MarkdownEditorViewMode viewMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownEditor"/> class.
    /// </summary>
    public MarkdownEditor()
    {
        SyntaxStyle = new MarkdownEditorSyntaxStyle();
        SyntaxStyle.Changed += SyntaxStyle_Changed;

        editorSurface = new MarkdownEditorTextBox(this);
        editorSurface.TextChanged += EditorSurface_TextChanged;
        editorSurface.SelectionChanged += EditorSurface_SelectionChanged;
        editorSurface.VerticalScrollBar.ValueChanged += EditorVerticalScrollBar_ValueChanged;

        previewViewer = new MarkdownViewer { Dock = DockStyle.Fill };
        previewViewer.LinkClicked += PreviewViewer_LinkClicked;
        previewViewer.VerticalScrollBar.ValueChanged += PreviewVerticalScrollBar_ValueChanged;
        splitContainer = new SplitContainer
        {
            Orientation = Orientation.Horizontal,
            SplitterWidth = 5
        };

        toolbar = new ToolBar();
        InitializeToolbar();

        previewTimer = new Timer { Interval = DefaultPreviewUpdateDelay };
        previewTimer.Tick += PreviewTimer_Tick;

        Controls.Add(toolbar);
        AttachViewControls();
        editorSurface.RefreshSyntaxHighlighting();
        UpdateToolbarState();
    }

    /// <summary>
    /// Occurs when the Markdown source changes.
    /// </summary>
    [Category("Property Changed")]
    [Description("Occurs when the Markdown source changes.")]
    public event EventHandler? MarkdownChanged;

    /// <summary>
    /// Occurs when <see cref="Modified"/> changes.
    /// </summary>
    [Category("Property Changed")]
    [Description("Occurs when the modified state changes.")]
    public event EventHandler? ModifiedChanged;

    /// <summary>
    /// Occurs when the source selection or caret position changes.
    /// </summary>
    [Category("Behavior")]
    [Description("Occurs when the source selection or caret position changes.")]
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Occurs when a link is activated in the native Markdown preview.
    /// </summary>
    /// <remarks>
    /// The event forwards <see cref="DocumentViewer.LinkClicked"/> without opening the destination
    /// automatically or changing the source editor selection.
    /// </remarks>
    [Category("Action")]
    [Description("Occurs when a link is activated in the Markdown preview.")]
    public event EventHandler<DocumentLinkClickedEventArgs>? PreviewLinkClicked;

    /// <summary>
    /// Gets or sets a value indicating whether TAB inserts a tab character.
    /// </summary>
    [DefaultValue(true)]
    [Category("Behavior")]
    [Description("Determines whether TAB is accepted by the Markdown source editor.")]
    public bool AcceptsTab
    {
        get => editorSurface.AcceptsTab;
        set => editorSurface.AcceptsTab = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether ENTER inserts a line break.
    /// </summary>
    [DefaultValue(true)]
    [Category("Behavior")]
    [Description("Determines whether ENTER is accepted by the Markdown source editor.")]
    public bool AcceptsReturn
    {
        get => editorSurface.AcceptsReturn;
        set => editorSurface.AcceptsReturn = value;
    }

    /// <summary>
    /// Gets a value indicating whether a redo operation is available.
    /// </summary>
    [Browsable(false)]
    public bool CanRedo => history.CanRedo;

    /// <summary>
    /// Gets a value indicating whether an undo operation is available.
    /// </summary>
    [Browsable(false)]
    public bool CanUndo => history.CanUndo;

    /// <summary>
    /// Gets a value indicating whether the native preview is currently visible.
    /// </summary>
    [Browsable(false)]
    public bool IsPreviewVisible => ViewMode is MarkdownEditorViewMode.Preview or MarkdownEditorViewMode.Split;

    /// <summary>
    /// Gets or sets the Markdown source.
    /// </summary>
    /// <remarks>
    /// Assigning source programmatically clears undo history and resets <see cref="Modified"/>.
    /// A <see langword="null"/> value from nullable-oblivious callers is treated as an empty
    /// string. <see cref="Text"/> is an exact alias of this property.
    /// </remarks>
    [DefaultValue("")]
    [Category("Data")]
    [Description("The multiline Markdown source edited by the control.")]
    public string Markdown
    {
        get => editorSurface.Text;
        set
        {
            value ??= string.Empty;
            if (editorSurface.Text == value)
            {
                history.Clear();
                cleanHistoryPosition = 0;
                SetModified(false, moveCleanMarker: true);
                UpdateToolbarState();
                return;
            }

            programmaticTextChange = true;
            history.Clear();
            cleanHistoryPosition = 0;
            try
            {
                editorSurface.Text = value;
                editorSurface.Select(0, 0);
            }
            finally
            {
                programmaticTextChange = false;
            }

            SetModified(false, moveCleanMarker: true);
            UpdateToolbarState();
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of UTF-16 characters accepted by the source editor.
    /// </summary>
    /// <remarks>A value of zero means no explicit limit.</remarks>
    [DefaultValue(0)]
    [Category("Behavior")]
    public int MaxLength
    {
        get => editorSurface.MaxLength;
        set => editorSurface.MaxLength = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the Markdown has changed since the last programmatic
    /// assignment or since this property was set to <see langword="false"/>.
    /// </summary>
    [DefaultValue(false)]
    [Category("Behavior")]
    public bool Modified
    {
        get => modified;
        set => SetModified(value, moveCleanMarker: true);
    }

    /// <summary>
    /// Gets the configurable native preview control.
    /// </summary>
    /// <remarks>
    /// Configure its <see cref="DocumentViewer.DocumentStyle"/>, image options, and
    /// <see cref="DocumentViewer.LinkClicked"/> event exactly as for a standalone viewer. Links
    /// are never opened automatically.
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public MarkdownViewer PreviewViewer => previewViewer;

    /// <summary>
    /// Gets the document style used by <see cref="PreviewViewer"/>.
    /// </summary>
    [Browsable(false)]
    public DocumentStyle PreviewStyle => previewViewer.DocumentStyle;

    /// <summary>
    /// Gets or sets the debounce delay used before updating a visible preview, in milliseconds.
    /// </summary>
    /// <remarks>Set this value to zero to update the preview immediately.</remarks>
    [DefaultValue(DefaultPreviewUpdateDelay)]
    [Category("Preview")]
    [Description("Delay in milliseconds before changed Markdown is sent to the preview.")]
    public int PreviewUpdateDelayMilliseconds
    {
        get => previewUpdateDelayMilliseconds;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "The preview delay cannot be negative.");

            previewUpdateDelayMilliseconds = value;
            previewTimer.Interval = Math.Max(1, value);
            previewDelayIsImmediate = value == 0;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether source editing is blocked.
    /// </summary>
    /// <remarks>Read-only source can still be selected and copied, and preview remains active.</remarks>
    [DefaultValue(false)]
    [Category("Behavior")]
    public bool ReadOnly
    {
        get => editorSurface.ReadOnly;
        set
        {
            if (editorSurface.ReadOnly == value)
                return;

            editorSurface.ReadOnly = value;
            UpdateToolbarState();
        }
    }

    /// <summary>
    /// Gets or sets the selected source text.
    /// </summary>
    /// <remarks>Assigning this property replaces the current selection as one undo operation.</remarks>
    [Browsable(false)]
    public string SelectedText
    {
        get => editorSurface.SelectedText;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ExecuteCommand(() => editorSurface.SelectedText = value);
        }
    }

    /// <summary>
    /// Gets or sets the number of selected UTF-16 characters.
    /// </summary>
    [Browsable(false)]
    public int SelectionLength
    {
        get => editorSurface.SelectionLength;
        set => editorSurface.Select(SelectionStart, value);
    }

    /// <summary>
    /// Gets or sets the zero-based UTF-16 index at which the selection starts.
    /// </summary>
    [Browsable(false)]
    public int SelectionStart
    {
        get => GetSurfaceSelection().Start;
        set
        {
            if (value < 0 || value > editorSurface.Text.Length)
                throw new ArgumentOutOfRangeException(nameof(value));

            editorSurface.Select(value, Math.Min(SelectionLength, editorSurface.Text.Length - value));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the built-in Markdown command toolbar is visible.
    /// </summary>
    [DefaultValue(true)]
    [Category("Appearance")]
    [Description("Shows the built-in Markdown command toolbar.")]
    public bool ShowToolbar
    {
        get => showToolbar;
        set
        {
            if (showToolbar == value)
                return;

            showToolbar = value;
            toolbar.Visible = value;
            PerformLayout(this, nameof(ShowToolbar));
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the fraction of split-mode width assigned to the source editor.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the range 0.1 to 0.9.</exception>
    [DefaultValue(0.5f)]
    [Category("Preview")]
    public float SplitRatio
    {
        get => splitRatio;
        set
        {
            if (value < 0.1f || value > 0.9f)
                throw new ArgumentOutOfRangeException(nameof(value), "SplitRatio must be between 0.1 and 0.9.");
            if (Math.Abs(splitRatio - value) < 0.0001f)
                return;

            splitRatio = value;
            PerformLayout(this, nameof(SplitRatio));
        }
    }

    /// <summary>
    /// Gets the source-highlighting style.
    /// </summary>
    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public MarkdownEditorSyntaxStyle SyntaxStyle { get; }

    /// <summary>
    /// Gets or sets the control text as an exact alias of <see cref="Markdown"/>.
    /// </summary>
    [Browsable(false)]
    public override string Text
    {
        get => Markdown;
        set => Markdown = value;
    }

    /// <summary>
    /// Gets or sets whether long source lines wrap to the editor viewport.
    /// </summary>
    [DefaultValue(true)]
    [Category("Behavior")]
    public bool WordWrap
    {
        get => editorSurface.WordWrap;
        set => editorSurface.WordWrap = value;
    }

    /// <summary>
    /// Gets or sets how source-editor scroll bars are requested.
    /// </summary>
    [DefaultValue(RichTextBoxScrollBars.Vertical)]
    [Category("Appearance")]
    public RichTextBoxScrollBars ScrollBars
    {
        get => editorSurface.ScrollBars;
        set => editorSurface.ScrollBars = value;
    }

    /// <summary>
    /// Gets or sets the visible editor/preview arrangement.
    /// </summary>
    [DefaultValue(MarkdownEditorViewMode.Editor)]
    [Category("Preview")]
    [Description("Selects source-only, preview-only, or split presentation.")]
    public MarkdownEditorViewMode ViewMode
    {
        get => viewMode;
        set
        {
            if (!Enum.IsDefined(typeof(MarkdownEditorViewMode), value))
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(MarkdownEditorViewMode));
            if (viewMode == value)
                return;

            viewMode = value;
            AttachViewControls();
            if (IsPreviewVisible)
                UpdatePreviewNow();
            else
                previewTimer.Stop();
            PerformLayout(this, nameof(ViewMode));
        }
    }

    internal MarkdownEditorTextBox EditorSurface => editorSurface;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            previewTimer.Stop();
            CancelActiveImageAssetOperation();
            previewTimer.Tick -= PreviewTimer_Tick;
            previewTimer.Dispose();
            SyntaxStyle.Changed -= SyntaxStyle_Changed;
            editorSurface.TextChanged -= EditorSurface_TextChanged;
            editorSurface.SelectionChanged -= EditorSurface_SelectionChanged;
            editorSurface.VerticalScrollBar.ValueChanged -= EditorVerticalScrollBar_ValueChanged;
            previewViewer.LinkClicked -= PreviewViewer_LinkClicked;
            previewViewer.VerticalScrollBar.ValueChanged -= PreviewVerticalScrollBar_ValueChanged;
            editorSurface.Dispose();
            previewViewer.Dispose();
            splitContainer.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Raises <see cref="MarkdownChanged"/>.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMarkdownChanged(EventArgs e) => MarkdownChanged?.Invoke(this, e);

    /// <summary>
    /// Raises <see cref="ModifiedChanged"/>.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnModifiedChanged(EventArgs e) => ModifiedChanged?.Invoke(this, e);

    /// <summary>
    /// Raises <see cref="SelectionChanged"/>.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSelectionChanged(EventArgs e) => SelectionChanged?.Invoke(this, e);

    /// <summary>
    /// Raises <see cref="PreviewLinkClicked"/>.
    /// </summary>
    /// <param name="e">The preview link activation data.</param>
    protected virtual void OnPreviewLinkClicked(DocumentLinkClickedEventArgs e)
        => PreviewLinkClicked?.Invoke(this, e);

    private void EditorSurface_SelectionChanged(object? sender, EventArgs e)
    {
        UpdateToolbarState();
        OnSelectionChanged(EventArgs.Empty);
    }

    private void EditorSurface_TextChanged(object? sender, EventArgs e)
    {
        CancelActiveImageAssetOperation();
        sourceVersion++;
        editorSurface.RefreshSyntaxHighlighting();
        OnTextChanged(EventArgs.Empty);
        OnMarkdownChanged(EventArgs.Empty);
        MarkPreviewDirty();
        UpdateToolbarState();
    }

    private void SyntaxStyle_Changed(object? sender, EventArgs e)
        => editorSurface.RefreshSyntaxHighlighting();
}
