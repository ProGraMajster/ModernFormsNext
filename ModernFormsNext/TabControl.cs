using System;
using System.Linq;
using ModernFormsNext.Renderers;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a tabbed container control that manages a collection of <see cref="TabPage"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="TabControl"/> provides a user interface for organizing content into multiple tabs,
    /// allowing only one tab page to be visible at a time.
    /// </para>
    /// <para>
    /// Internally, it uses a <see cref="TabStrip"/> control to render and manage tab headers.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var tabControl = new TabControl();
    /// tabControl.AddTab("Home");
    /// tabControl.AddClosableTab("Editor");
    /// tabControl.AddPinnedTab("Settings");
    /// </code>
    /// </example>
    public class TabControl : Control
    {
        private readonly TabStrip tab_strip;

        /// <summary>
        /// Initializes a new instance of the <see cref="TabControl"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor initializes the internal <see cref="TabStrip"/> and connects
        /// event handlers for tab selection and closing.
        /// </remarks>
        public TabControl()
        {
            tab_strip = Controls.AddImplicitControl(new TabStrip
            {
                Dock = DockStyle.Top
            });

            tab_strip.SelectedTabChanged += TabStrip_SelectedTabChanged;
            tab_strip.TabCloseButtonClicked += TabStrip_TabCloseButtonClicked;

            TabPages = new TabPageCollection(this, tab_strip);
        }

        private void TabStrip_TabCloseButtonClicked(object? sender, TabStripItemEventArgs e)
        {
            var page = TabPages.FirstOrDefault(x => ReferenceEquals(x.TabStripItem, e.Item));
            if (page != null)
                CloseTab(page);
        }

        /// <summary>
        /// Gets or sets the background color of tabs.
        /// </summary>
        public SKColor? TabBackgroundColor
        {
            get => tab_strip.TabBackgroundColor;
            set => tab_strip.TabBackgroundColor = value;
        }

        /// <summary>
        /// Gets or sets the background color of the selected tab.
        /// </summary>
        public SKColor? SelectedTabBackgroundColor
        {
            get => tab_strip.SelectedTabBackgroundColor;
            set => tab_strip.SelectedTabBackgroundColor = value;
        }

        /// <summary>
        /// Gets or sets the background color of the hovered tab.
        /// </summary>
        public SKColor? HoveredTabBackgroundColor
        {
            get => tab_strip.HoveredTabBackgroundColor;
            set => tab_strip.HoveredTabBackgroundColor = value;
        }

        /// <summary>
        /// Gets or sets the background color of the tab strip area.
        /// </summary>
        public SKColor? TabStripBackgroundColor
        {
            get => tab_strip.TabStripBackgroundColor;
            set => tab_strip.TabStripBackgroundColor = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether close buttons are visible on tabs.
        /// </summary>
        public bool ShowCloseButtons
        {
            get => tab_strip.ShowCloseButtons;
            set => tab_strip.ShowCloseButtons = value;
        }

        /// <summary>
        /// Gets or sets the corner radius of tabs.
        /// </summary>
        public int TabCornerRadius
        {
            get => tab_strip.TabCornerRadius;
            set => tab_strip.TabCornerRadius = value;
        }

        /// <summary>
        /// Gets or sets the spacing between tabs.
        /// </summary>
        public int TabSpacing
        {
            get => tab_strip.TabSpacing;
            set => tab_strip.TabSpacing = value;
        }

        /// <summary>
        /// Gets or sets the minimum width of a tab.
        /// </summary>
        public int TabMinWidth
        {
            get => tab_strip.TabMinWidth;
            set => tab_strip.TabMinWidth = value;
        }

        /// <summary>
        /// Gets or sets the maximum width of a tab.
        /// </summary>
        public int TabMaxWidth
        {
            get => tab_strip.TabMaxWidth;
            set => tab_strip.TabMaxWidth = value;
        }

        /// <summary>
        /// Occurs when a tab is about to be closed.
        /// </summary>
        public event EventHandler<TabPageCancelEventArgs>? TabClosing;

        /// <summary>
        /// Occurs after a tab has been closed.
        /// </summary>
        public event EventHandler<TabPageEventArgs>? TabClosed;

        /// <summary>
        /// Attempts to close the specified tab.
        /// </summary>
        /// <param name="page">The tab page to close.</param>
        /// <returns>
        /// <see langword="true"/> if the tab was successfully closed; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Tabs marked as pinned or non-closable cannot be closed.
        /// </remarks>
        public bool CloseTab(TabPage page)
        {
            if (page is null)
                throw new ArgumentNullException(nameof(page));

            if (!TabPages.Contains(page))
                return false;

            if (page.Pinned || !page.Closable)
                return false;

            var closingArgs = new TabPageCancelEventArgs(page);
            TabClosing?.Invoke(this, closingArgs);

            if (closingArgs.Cancel)
                return false;

            TabPages.Remove(page);
            TabClosed?.Invoke(this, new TabPageEventArgs(page));
            return true;
        }

        /// <summary>
        /// Creates and adds a new tab.
        /// </summary>
        public TabPage AddTab(string text, bool select = true)
        {
            var page = new TabPage(text);
            TabPages.Add(page);

            if (select)
                SelectedTabPage = page;

            return page;
        }

        /// <summary>
        /// Creates and adds a new closable tab.
        /// </summary>
        public TabPage AddClosableTab(string text, bool select = true)
        {
            var page = new TabPage(text)
            {
                Closable = true
            };

            TabPages.Add(page);

            if (select)
                SelectedTabPage = page;

            return page;
        }

        /// <summary>
        /// Creates and adds a new pinned (non-closable) tab.
        /// </summary>
        public TabPage AddPinnedTab(string text, bool select = true)
        {
            var page = new TabPage(text)
            {
                Pinned = true,
                Closable = false
            };

            TabPages.Add(page);

            if (select)
                SelectedTabPage = page;

            return page;
        }

        /// <summary>
        /// Closes all tabs except the specified one.
        /// </summary>
        /// <param name="page">The tab to keep open.</param>
        public void CloseAllTabsExcept(TabPage page)
        {
            for (int i = TabPages.Count - 1; i >= 0; i--)
            {
                var current = TabPages[i];
                if (!ReferenceEquals(current, page) && !current.Pinned && current.Closable)
                    CloseTab(current);
            }
        }

        /// <summary>
        /// Gets the collection of tabs contained by this control.
        /// </summary>
        public TabPageCollection TabPages { get; }

        private TabPage? GetPageFromTab(TabStripItem? item)
            => TabPages.FirstOrDefault(p => p.TabStripItem == item);

        /// <summary>
        /// Raises the <see cref="SelectedIndexChanged"/> event.
        /// </summary>
        protected virtual void OnSelectedIndexChanged(EventArgs e)
            => SelectedIndexChanged?.Invoke(this, e);

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RenderManager.Render(this, e);
        }

        /// <summary>
        /// Gets or sets the index of the currently selected tab.
        /// </summary>
        /// <value>
        /// The selected tab index, or -1 if no tab is selected.
        /// </value>
        public int SelectedIndex
        {
            get => tab_strip.SelectedIndex;
            set => tab_strip.SelectedIndex = value;
        }

        /// <summary>
        /// Occurs when the selected tab index changes.
        /// </summary>
        public event EventHandler? SelectedIndexChanged;

        /// <summary>
        /// Gets or sets the currently selected tab page.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified tab page is not part of this control.
        /// </exception>
        public TabPage? SelectedTabPage
        {
            get => GetPageFromTab(tab_strip.SelectedTab);
            set
            {
                if (value is null)
                {
                    tab_strip.SelectedTab = null;
                    return;
                }

                var index = TabPages.IndexOf(value);

                if (index == -1)
                    throw new ArgumentException("TabPage is not part of this TabControl");

                tab_strip.SelectedIndex = index;
            }
        }

        /// <summary>
        /// Handles changes in the selected tab within the internal <see cref="TabStrip"/>.
        /// </summary>
        private void TabStrip_SelectedTabChanged(object? sender, EventArgs e)
        {
            var old_selected = Controls.OfType<TabPage>().FirstOrDefault(tp => tp.Visible);
            var new_selected = GetPageFromTab(tab_strip.SelectedTab);

            if (old_selected == new_selected)
                return;

            if (old_selected != null)
                old_selected.Visible = false;

            if (new_selected != null)
                new_selected.Visible = true;

            OnSelectedIndexChanged(EventArgs.Empty);
        }
    }
}