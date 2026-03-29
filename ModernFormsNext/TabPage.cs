using System;
using SkiaSharp;
using static ModernFormsNext.TabStripItem;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a single tab page within a <see cref="TabControl"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="TabPage"/> acts as a container for controls displayed when the tab is selected.
    /// </para>
    /// <para>
    /// Each tab page is associated with a corresponding <see cref="TabStripItem"/> that represents
    /// its visual tab header.
    /// </para>
    /// <para>
    /// The page is automatically docked to fill the available space within its parent <see cref="TabControl"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var tabControl = new TabControl();
    ///
    /// var page = new TabPage("Home");
    /// tabControl.TabPages.Add(page);
    ///
    /// page.Controls.Add(new Button { Text = "Click me" });
    /// </code>
    /// </example>
    public class TabPage : Panel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TabPage"/> class.
        /// </summary>
        /// <remarks>
        /// The tab page is docked to fill its parent container and a corresponding
        /// <see cref="TabStripItem"/> is created automatically.
        /// </remarks>
        public TabPage()
        {
            Dock = DockStyle.Fill;
            TabStripItem = new TabStripItem();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TabPage"/> class with the specified text.
        /// </summary>
        /// <param name="text">The text displayed on the tab header.</param>
        /// <remarks>
        /// This constructor sets the tab header text through the associated <see cref="TabStripItem"/>.
        /// </remarks>
        public TabPage(string text) : this()
        {
            TabStripItem.Text = text;
        }

        /// <summary>
        /// Gets the associated <see cref="TabStripItem"/> used to render the tab header.
        /// </summary>
        /// <remarks>
        /// This property is used internally by <see cref="TabControl"/> and <see cref="TabStrip"/>
        /// to synchronize the visual tab representation with this page.
        /// </remarks>
        internal TabStripItem TabStripItem { get; }

        /// <summary>
        /// Gets or sets the text displayed on the tab header.
        /// </summary>
        /// <remarks>
        /// This property is mapped directly to the associated <see cref="TabStripItem"/>.
        /// </remarks>
        public override string Text
        {
            get => TabStripItem.Text;
            set => TabStripItem.Text = value;
        }

        /// <summary>
        /// Gets or sets the icon displayed on the tab header.
        /// </summary>
        /// <value>
        /// An <see cref="SKImage"/> representing the tab icon, or <see langword="null"/> if no icon is set.
        /// </value>
        public SKImage? Icon
        {
            get => TabStripItem.Icon;
            set => TabStripItem.Icon = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the tab can be closed by the user.
        /// </summary>
        /// <remarks>
        /// When set to <see langword="true"/>, a close button may be displayed on the tab
        /// depending on the <see cref="TabControl.ShowCloseButtons"/> setting.
        /// </remarks>
        public bool Closable
        {
            get => TabStripItem.Closable;
            set => TabStripItem.Closable = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the tab is pinned.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Pinned tabs cannot be closed and are typically displayed with higher priority.
        /// </para>
        /// <para>
        /// When a tab is pinned, its <see cref="Closable"/> property is usually ignored.
        /// </para>
        /// </remarks>
        public bool Pinned
        {
            get => TabStripItem.Pinned;
            set => TabStripItem.Pinned = value;
        }

        /// <summary>
        /// Gets or sets the display mode of the tab header.
        /// </summary>
        /// <remarks>
        /// Determines how the tab is visually presented (e.g., text only, icon only, or both).
        /// </remarks>
        public TabDisplayMode DisplayMode
        {
            get => TabStripItem.DisplayMode;
            set => TabStripItem.DisplayMode = value;
        }
    }
}