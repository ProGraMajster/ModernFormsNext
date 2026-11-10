using System.ComponentModel;
using System.Drawing;
using System.Windows.Input;

namespace ModernFormsNext;

public partial class NavigationPage
{
    private readonly NavigationHeader navigationBar;
    private Page? observedTitle;
    private AppShell? shellOwner;
    private bool navigationBarVisible = true, navigationBarAtBottom;
    private bool lastLayoutHeaderRequest;
    private bool navigationBarPreviewPlaceholder, designHeaderPreview, navigationBarPreviewShowBack = true;
    private int navigationBarHeight = 42, navigationBarPreviewActionCount;
    private string navigationBarTitle = "", navigationBarBackText = "Back";

    /// <summary>Gets the single runtime header owned by this navigator.</summary>
    /// <remarks>Use it for styling and custom view slots on the UI thread.
    /// Keep the navigator in ownership of the header and built-in Back command.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NavigationHeader NavigationBar => navigationBar;

    /// <summary>Gets or sets whether this navigation context reserves space for its header. The default is true.</summary>
    /// <remarks>UI thread only. A displayed ancestor navigation header takes precedence within its branch.</remarks>
    [DefaultValue(true)]
    public bool NavigationBarVisible
    {
        get => navigationBarVisible;
        set { VerifyPageAccess(); if (navigationBarVisible == value) return; navigationBarVisible = value; PerformLayout(); }
    }

    /// <summary>Gets or sets the requested header height in logical pixels, from 1 to 4096. The default is 42.</summary>
    /// <remarks>The built-in header can grow to fit scaled text. The available client area still bounds its height.</remarks>
    [DefaultValue(42)]
    public int NavigationBarHeight
    {
        get => navigationBarHeight;
        set
        {
            VerifyPageAccess();
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 4096);
            if (navigationBarHeight == value) return;
            navigationBarHeight = value;
            PerformLayout();
        }
    }

    /// <summary>Gets or sets whether the header appears below the page rather than above it.</summary>
    [DefaultValue(false)]
    public bool NavigationBarAtBottom
    {
        get => navigationBarAtBottom;
        set { VerifyPageAccess(); if (navigationBarAtBottom == value) return; navigationBarAtBottom = value; PerformLayout(); }
    }

    /// <summary>Gets or sets an optional fixed header title. An empty value uses the current page title.</summary>
    [DefaultValue("")]
    public string NavigationBarTitle
    {
        get => navigationBarTitle;
        set { VerifyPageAccess(); navigationBarTitle = value ?? ""; RefreshNavigationBar(); }
    }

    /// <summary>Gets or sets the label of the built-in Back button.</summary>
    [DefaultValue("Back")]
    public string NavigationBarBackText
    {
        get => navigationBarBackText;
        set { VerifyPageAccess(); navigationBarBackText = string.IsNullOrWhiteSpace(value) ? "Back" : value; RefreshNavigationBar(); }
    }

    /// <summary>Gets or sets the padding around the header content, from 0 to 4096 logical pixels per side.</summary>
    public Padding NavigationBarPadding
    {
        get => navigationBar.Padding;
        set
        {
            VerifyPageAccess();
            if (value.Left is < 0 or > 4096 || value.Top is < 0 or > 4096
                || value.Right is < 0 or > 4096 || value.Bottom is < 0 or > 4096)
                throw new ArgumentOutOfRangeException(nameof(value), "Header padding must be between 0 and 4096 logical pixels per side.");
            navigationBar.Padding = value;
        }
    }

    /// <summary>Gets or sets whether Designer represents a runtime custom view with an inert label.</summary>
    /// <remarks>This data-only hint does not select, create or invoke a runtime view.</remarks>
    [DefaultValue(false)]
    public bool NavigationBarPreviewPlaceholder
    {
        get => navigationBarPreviewPlaceholder;
        set { VerifyPageAccess(); navigationBarPreviewPlaceholder = value; RefreshNavigationBar(); }
    }

    /// <summary>Gets or sets whether Designer draws a disabled Back sample. The default is true.</summary>
    /// <remarks>This data-only property does not affect runtime Back visibility or commands.</remarks>
    [DefaultValue(true)]
    public bool NavigationBarPreviewShowBack
    {
        get => navigationBarPreviewShowBack;
        set { VerifyPageAccess(); navigationBarPreviewShowBack = value; if (designHeaderPreview) RefreshNavigationBar(); }
    }

    /// <summary>Gets or sets how many inert toolbar slots Designer draws, from 0 to 16.</summary>
    /// <remarks>The count creates no command descriptors, references or runtime actions.</remarks>
    [DefaultValue(0)]
    public int NavigationBarPreviewActionCount
    {
        get => navigationBarPreviewActionCount;
        set
        {
            VerifyPageAccess();
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 16);
            navigationBarPreviewActionCount = value;
            if (designHeaderPreview) RefreshNavigationBar();
        }
    }

    internal void SetShellHeaderOwner(AppShell shell)
    {
        shellOwner = shell;
        RefreshNavigationBar();
    }

    internal void RefreshShellHeader() => RefreshNavigationBar();

    internal void EnableNavigationBarPreview()
    {
        if (designHeaderPreview) return;
        designHeaderPreview = true;
        RefreshNavigationBar();
    }

    private void RefreshNavigationBar()
    {
        if (navigationBar is null || retiring || IsDisposed || Disposing) return;
        var current = CurrentPage;
        ObserveNavigationHeaderPage(current);
        string caption = navigationBarTitle.Length != 0 ? navigationBarTitle : current?.Title ?? shellOwner?.Title ?? Title;
        ICommand command = shellOwner?.BackCommand ?? BackCommand;
        navigationBar.Update(designHeaderPreview ? null : current, caption, navigationBarBackText,
            designHeaderPreview ? null : command,
            designHeaderPreview ? navigationBarPreviewShowBack : shellOwner?.CanGoBack ?? CanGoBack,
            designHeaderPreview && navigationBarPreviewPlaceholder,
            designHeaderPreview ? navigationBarPreviewActionCount : 0);
        // A stack commit can change the current page's opt-out without changing outer bounds.
        if (lastLayoutHeaderRequest != IsNavigationBarRequested) PerformLayout();
    }

    private void ObserveNavigationHeaderPage(Page? current)
    {
        if (ReferenceEquals(observedTitle, current)) return;
        if (observedTitle is not null)
        {
            observedTitle.TextChanged -= OnNavigationTitleChanged;
            observedTitle.NavigationBarVisibilityChanged -= OnNavigationBarVisibilityChanged;
        }
        observedTitle = current;
        if (observedTitle is not null)
        {
            observedTitle.TextChanged += OnNavigationTitleChanged;
            observedTitle.NavigationBarVisibilityChanged += OnNavigationBarVisibilityChanged;
        }
    }

    private bool IsNavigationBarRequested => navigationBarVisible && CurrentPage?.HideNavigationBar != true;
    private void OnNavigationTitleChanged(object? sender, EventArgs e) => RefreshNavigationBar();
    private void OnNavigationBarVisibilityChanged(object? sender, EventArgs e) => PerformLayout();

    /// <inheritdoc/>
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        RefreshNavigationBar();
    }

    private Rectangle LayoutNavigationBar(Rectangle bounds)
    {
        if (navigationBar is null) return bounds;
        lastLayoutHeaderRequest = IsNavigationBarRequested;
        bool visible = lastLayoutHeaderRequest && bounds.Width > 0 && bounds.Height > 0;
        // An ancestor's active header owns the navigation chrome for its branch.
        // The flyout menu remains auxiliary chrome even when it has a separate stack.
        Control branch = this;
        for (Control? ancestor = Parent; visible && ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is NavigationPage { IsNavigationBarRequested: true }
                || ancestor is FlyoutPage flyout && flyout.IsMenuPresentationBranch(branch))
                visible = false;
            branch = ancestor;
        }

        navigationBar.Visible = visible;
        if (!visible) return bounds;
        int height = Math.Min(Math.Max(navigationBarHeight, navigationBar.GetBuiltInMinimumHeight()), bounds.Height);
        navigationBar.Bounds = new(bounds.X, navigationBarAtBottom ? bounds.Bottom - height : bounds.Y, bounds.Width, height);
        return new(bounds.X, navigationBarAtBottom ? bounds.Y : bounds.Y + height, bounds.Width, bounds.Height - height);
    }
}
