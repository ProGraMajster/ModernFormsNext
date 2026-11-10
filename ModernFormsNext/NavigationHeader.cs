using System.ComponentModel;
using System.Drawing;
using System.Windows.Input;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

/// <summary>Provides runtime styling and custom content for a navigator's existing header.</summary>
/// <remarks>
/// Access on the UI thread through NavigationPage.NavigationBar or AppShell.NavigationBar.
/// The navigator owns this control. Set its visibility through the navigator's NavigationBarVisible property.
/// The default header uses standard Button and Label controls, including their focus and accessibility behavior.
/// </remarks>
public sealed class NavigationHeader : Control
{
    private readonly Label title;
    private readonly Button back;
    private readonly List<Button> actions = [];
    private Page? page;
    private Control? titleView, headerView;
    private bool changingView, showBack, previewPlaceholder;
    private int previewActionCount;
    private long updateVersion, actionsVersion;

    internal NavigationHeader(PageHost pageHost)
    {
        TabStop = false;
        Padding = new Padding(8, 2, 8, 2);
        back = new HeaderBackButton(pageHost) { Text = "Back", AutoEllipsis = true, Visible = false, TabIndex = 0 };
        back.Style.Border.Width = 0;
        Controls.Add(back);
        title = Controls.Add(new Label { AutoEllipsis = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft });
        title.FontChanged += OnBuiltInFontChanged;
        back.FontChanged += OnBuiltInFontChanged;
    }

    /// <summary>Gets the built-in title label for runtime typography and styling.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Label TitleLabel => title;

    /// <summary>Gets the built-in Back button for runtime styling and image customization.</summary>
    /// <remarks>The navigator owns its command and visibility.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button BackButton => back;

    /// <summary>Gets or sets an optional custom control in the title slot.</summary>
    /// <remarks>Assign a detached control. Replacement detaches the prior view without disposing it.
    /// Disposing the header disposes its attached views. The full header view takes precedence.
    /// Use the UI thread. A view change in the middle of another view change is rejected.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? TitleView
    {
        get => titleView?.Parent == this ? titleView : null;
        set => SetView(ref titleView, value);
    }

    /// <summary>Gets or sets a custom control that fills the entire padded header.</summary>
    /// <remarks>Assign a detached control. While shown it replaces the default title, Back button and actions.
    /// Replacement detaches the prior view. Disposing the header disposes an attached view.
    /// Use the UI thread. A view change in the middle of another view change is rejected.
    /// Runtime code may compose arbitrary controls and bind the navigator's canonical Back command.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? HeaderView
    {
        get => headerView?.Parent == this ? headerView : null;
        set => SetView(ref headerView, value);
    }

    internal void Update(Page? nextPage, string caption, string backText, ICommand? command, bool canGoBack,
        bool placeholder, int sampleActions)
    {
        if (IsDisposed || Disposing) return;
        long version = ++updateVersion;
        SuspendLayout();
        try
        {
            bool nextBack = canGoBack && !placeholder;
            if (showBack != nextBack || previewPlaceholder != placeholder) PerformLayout();
            showBack = nextBack;
            previewPlaceholder = placeholder;
            title.Text = placeholder ? "Custom navigation header (runtime)" : caption;
            if (version != updateVersion || IsDisposed || Disposing) return;
            back.Text = backText;
            if (version != updateVersion || IsDisposed || Disposing) return;
            back.Command = placeholder ? null : command;
            if (version != updateVersion || IsDisposed || Disposing) return;
            if (command is null) back.Enabled = false;
            if (version != updateVersion || IsDisposed || Disposing) return;
            if (ReferenceEquals(page, nextPage) && previewActionCount == sampleActions) return;
            previewActionCount = sampleActions;
            if (page is not null) page.NavigationActionsChanged -= OnActionsChanged;
            page = nextPage;
            if (page is not null) page.NavigationActionsChanged += OnActionsChanged;
            RebuildActions();
        }
        finally { ResumeLayout(true); }
    }

    private void OnActionsChanged(object? sender, EventArgs args)
    {
        if (!IsDisposed && !Disposing && ReferenceEquals(sender, page)) RebuildActions();
    }

    private void RebuildActions()
    {
        long version = ++actionsVersion;
        var current = page;
        SuspendLayout();
        try
        {
            // Removing or binding a child can call application code. Publish removal first
            // and leave any newer action list in place when the older rebuild resumes.
            var previous = actions.ToArray();
            actions.Clear();
            foreach (var button in previous)
            {
                try { Controls.Remove(button); }
                finally { button.Dispose(); }
            }

            // Designer slots are disabled controls with a label only. The runtime path
            // borrows the current page's command and uses its existing routed target.
            var descriptors = current?.NavigationActions;
            int count = descriptors?.Count ?? previewActionCount;
            for (int index = 0; index < count; index++)
            {
                if (version != actionsVersion || IsDisposed || Disposing) return;
                var action = descriptors?[index];
                var button = CreateButton(action?.Text ?? "Action", index + 1);
                button.Enabled = action is not null;
                button.CommandTarget = current;
                button.CommandParameter = action?.Parameter;
                actions.Add(button);
                Controls.Add(button);
                if (version == actionsVersion && !IsDisposed && !Disposing)
                    button.Command = action?.Command;
            }
        }
        finally { ResumeLayout(true); }
    }

    private sealed class HeaderBackButton(PageHost pageHost) : Button
    {
        protected override void OnClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !Enabled || !Focused)
            {
                base.OnClick(e);
                return;
            }

            // The canonical AsyncCommand retires the button's enabled focus as it starts.
            // First hand focus to the existing host so its normal navigation transaction can
            // restore the incoming page. A veto or a canceled factory may leave the same page.
            pageHost.Select();
            if (!pageHost.Focused) return;
            try { base.OnClick(e); }
            finally { pageHost.RestorePageFocus(); }
        }
    }

    private static Button CreateButton(string text, int tabIndex)
    {
        var button = new Button { Text = text, AutoEllipsis = true, TabIndex = tabIndex };
        button.Style.Border.Width = 0;
        return button;
    }

    // CurrentStyle holds the font and line-height resolved by the existing typography/theme system.
    internal int GetBuiltInMinimumHeight()
    {
        if (HeaderView is not null) return 0;
        double textHeight = TitleView is null ? LineHeightOf(title) : 0;
        if (showBack) textHeight = Math.Max(textHeight, LineHeightOf(back));
        foreach (var action in actions) textHeight = Math.Max(textHeight, LineHeightOf(action));
        return (int)Math.Clamp(Math.Ceiling(textHeight) + Padding.Vertical + 8, 0, 4096);

        static double LineHeightOf(Control control) =>
            control.CurrentStyle.GetFontSize() * control.CurrentStyle.GetLineHeight();
    }

    private void OnBuiltInFontChanged(object? sender, EventArgs e) => Parent?.PerformLayout();

    /// <inheritdoc/>
    protected internal override void OnThemeChanged(EventArgs e)
    {
        base.OnThemeChanged(e);
        Parent?.PerformLayout();
        PerformLayout();
    }

    /// <inheritdoc/>
    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        Parent?.PerformLayout();
    }

    private void SetView(ref Control? slot, Control? view)
    {
        GetFocusScope().VerifyAccess();
        ObjectDisposedException.ThrowIf(IsDisposed || Disposing, this);
        if (changingView) throw new InvalidOperationException("A navigation header view change is already in progress.");
        if (ReferenceEquals(slot, view) && (view is null || view.Parent == this)) return;
        if (view is not null)
        {
            if (view.Parent is not null || view.IsDisposed || view.Disposing)
                throw new ArgumentException("A header view must be detached and usable.", nameof(view));
            for (Control? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
                if (ReferenceEquals(ancestor, view)) throw new ArgumentException("A header view cannot contain its owner.", nameof(view));
        }

        changingView = true;
        SuspendLayout();
        try
        {
            var previous = slot;
            slot = null;
            if (previous?.Parent == this) Controls.Remove(previous);
            if (!IsDisposed && !Disposing && view is { Parent: null, IsDisposed: false, Disposing: false })
            {
                slot = view;
                Controls.Add(view);
            }
        }
        finally
        {
            if (slot?.Parent != this) slot = null;
            changingView = false;
            ResumeLayout(true);
            Parent?.PerformLayout();
        }
    }

    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new HeaderAccessibleObject(this);

    private sealed class HeaderAccessibleObject(NavigationHeader owner) : ControlAccessibleObject(owner)
    {
        public override string Name => base.Name is { Length: > 0 } name ? name : "Navigation header";
        public override AccessibleControlType ControlType => AccessibleControlType.ToolBar;
    }

    /// <inheritdoc/>
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        PerformLayout();
    }

    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (title is null || back is null || changingView || IsDisposed || Disposing) return;
        var bounds = LogicalPaddedClientRectangle;
        var customHeader = previewPlaceholder ? null : HeaderView;
        var customTitle = previewPlaceholder ? null : TitleView;
        if (HeaderView is { } assignedHeader) assignedHeader.Visible = customHeader is not null;
        if (TitleView is { } assignedTitle) assignedTitle.Visible = customHeader is null && customTitle is not null;
        title.Visible = customHeader is null && customTitle is null;
        bool backFits = customHeader is null && showBack && bounds.Width > 0 && bounds.Height > 0;
        back.Visible = backFits;
        long version = actionsVersion;
        if (customHeader is not null)
        {
            customHeader.Bounds = bounds;
            for (int i = 0; version == actionsVersion && i < actions.Count; i++) actions[i].Visible = false;
            return;
        }

        bool rtl = RightToLeft == RightToLeft.Yes;
        title.TextAlign = rtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
        int leading = 0;
        if (backFits)
        {
            int width = Math.Min(Math.Max(56, bounds.Width - 48), MeasureButtonWidth(back, 56));
            width = Math.Min(width, bounds.Width);
            back.Bounds = Place(0, width);
            leading = Math.Min(bounds.Width, width + 4);
        }

        // Admit actions in declaration order, reserving a usable title region. Hidden
        // actions leave both hit testing and tab traversal until there is room again.
        Span<int> widths = stackalloc int[16];
        int available = Math.Max(0, bounds.Width - leading - 48);
        int consumed = 0;
        for (int i = 0; version == actionsVersion && i < actions.Count; i++)
        {
            var action = actions[i];
            int width = MeasureButtonWidth(action, 44);
            bool fits = !previewPlaceholder && bounds.Height > 0 && width <= available - consumed;
            widths[i] = fits ? width : 0;
            if (fits) consumed += width;
            action.Visible = fits;
        }
        int trailing = bounds.Width - consumed;
        for (int i = 0, offset = trailing; version == actionsVersion && i < actions.Count; i++)
        {
            if (widths[i] == 0) continue;
            actions[i].Bounds = Place(offset, widths[i]);
            offset += widths[i];
        }
        (customTitle ?? title).Bounds = Place(leading, Math.Max(0, trailing - leading));

        Rectangle Place(int x, int width) => new(bounds.X + (rtl ? bounds.Width - x - width : x),
            bounds.Y, width, bounds.Height);
    }

    private static int MeasureButtonWidth(Button button, int minimum)
    {
        double textWidth = TextMeasurer.MeasureText(button.Text, button).Width / button.Scaling;
        int imageWidth = ((IHaveTextAndImageAlign)button).GetImage()?.Width ?? 0;
        double width = button.TextImageRelation is TextImageRelation.ImageBeforeText or TextImageRelation.TextBeforeImage
            ? textWidth + imageWidth
            : Math.Max(textWidth, imageWidth);
        return Math.Max(minimum, (int)Math.Ceiling(width) + 16);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && page is not null)
        {
            page.NavigationActionsChanged -= OnActionsChanged;
            page = null;
        }
        base.Dispose(disposing);
    }
}
