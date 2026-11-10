using System.ComponentModel;

namespace ModernFormsNext;

public partial class AppShell
{
    /// <summary>Gets the runtime header owned by the existing route navigator.</summary>
    /// <remarks>Its built-in Back button invokes the shell's canonical Back command.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NavigationHeader NavigationBar => navigator.NavigationBar;

    /// <summary>Gets or sets whether the shell displays its navigation header. The default is true.</summary>
    [DefaultValue(true)]
    public bool NavigationBarVisible { get => navigator.NavigationBarVisible; set => navigator.NavigationBarVisible = value; }

    /// <summary>Gets or sets the header height in logical pixels, from 1 to 4096.</summary>
    [DefaultValue(42)]
    public int NavigationBarHeight { get => navigator.NavigationBarHeight; set => navigator.NavigationBarHeight = value; }

    /// <summary>Gets or sets whether the header appears below the page instead of above it.</summary>
    [DefaultValue(false)]
    public bool NavigationBarAtBottom { get => navigator.NavigationBarAtBottom; set => navigator.NavigationBarAtBottom = value; }

    /// <summary>Gets or sets the optional fixed title displayed in the header.</summary>
    [DefaultValue("")]
    public string NavigationBarTitle { get => navigator.NavigationBarTitle; set => navigator.NavigationBarTitle = value; }

    /// <summary>Gets or sets the built-in Back button label.</summary>
    [DefaultValue("Back")]
    public string NavigationBarBackText { get => navigator.NavigationBarBackText; set => navigator.NavigationBarBackText = value; }

    /// <summary>Gets or sets the padding around the header content in logical pixels.</summary>
    public Padding NavigationBarPadding { get => navigator.NavigationBarPadding; set => navigator.NavigationBarPadding = value; }

    /// <summary>Gets or sets the data-only Designer hint for a custom runtime header.</summary>
    [DefaultValue(false)]
    public bool NavigationBarPreviewPlaceholder
    { get => navigator.NavigationBarPreviewPlaceholder; set => navigator.NavigationBarPreviewPlaceholder = value; }

    /// <summary>Gets or sets whether Designer draws a disabled Back sample. The default is true.</summary>
    /// <remarks>This data-only property has no runtime effect.</remarks>
    [DefaultValue(true)]
    public bool NavigationBarPreviewShowBack
    { get => navigator.NavigationBarPreviewShowBack; set => navigator.NavigationBarPreviewShowBack = value; }

    /// <summary>Gets or sets how many inert toolbar slots Designer draws, from 0 to 16.</summary>
    /// <remarks>This data-only property has no runtime effect.</remarks>
    [DefaultValue(0)]
    public int NavigationBarPreviewActionCount
    { get => navigator.NavigationBarPreviewActionCount; set => navigator.NavigationBarPreviewActionCount = value; }

    internal void EnableNavigationBarPreview() => navigator.EnableNavigationBarPreview();

    /// <inheritdoc/>
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        navigator?.RefreshShellHeader();
    }
}
