using System;
using System.Linq;
using ModernFormsNext.Renderers;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a TabControl control.
    /// </summary>
    public class TabControl : Control
    {
        private readonly TabStrip tab_strip;

        /// <summary>
        /// Initializes a new instance of the TabControl class.
        /// </summary>
        public TabControl ()
        {
            tab_strip = Controls.AddImplicitControl (new TabStrip {
                Dock = DockStyle.Top
            });

            tab_strip.SelectedTabChanged += TabStrip_SelectedTabChanged;
            tab_strip.TabCloseButtonClicked += TabStrip_TabCloseButtonClicked;

            TabPages = new TabPageCollection (this, tab_strip);
        }

        private void TabStrip_TabCloseButtonClicked (object? sender, TabStripItemEventArgs e)
        {
            var page = TabPages.FirstOrDefault (x => ReferenceEquals (x.TabStripItem, e.Item));
            if (page != null)
                CloseTab (page);
        }

        public SKColor? TabBackgroundColor {
            get => tab_strip.TabBackgroundColor;
            set => tab_strip.TabBackgroundColor = value;
        }

        public SKColor? SelectedTabBackgroundColor {
            get => tab_strip.SelectedTabBackgroundColor;
            set => tab_strip.SelectedTabBackgroundColor = value;
        }

        public SKColor? HoveredTabBackgroundColor {
            get => tab_strip.HoveredTabBackgroundColor;
            set => tab_strip.HoveredTabBackgroundColor = value;
        }

        public SKColor? TabStripBackgroundColor {
            get => tab_strip.TabStripBackgroundColor;
            set => tab_strip.TabStripBackgroundColor = value;
        }

        public bool ShowCloseButtons {
            get => tab_strip.ShowCloseButtons;
            set => tab_strip.ShowCloseButtons = value;
        }

        public int TabCornerRadius {
            get => tab_strip.TabCornerRadius;
            set => tab_strip.TabCornerRadius = value;
        }

        public int TabSpacing {
            get => tab_strip.TabSpacing;
            set => tab_strip.TabSpacing = value;
        }

        public int TabMinWidth {
            get => tab_strip.TabMinWidth;
            set => tab_strip.TabMinWidth = value;
        }

        public int TabMaxWidth {
            get => tab_strip.TabMaxWidth;
            set => tab_strip.TabMaxWidth = value;
        }

        public event EventHandler<TabPageCancelEventArgs>? TabClosing;
        public event EventHandler<TabPageEventArgs>? TabClosed;


        public bool CloseTab (TabPage page)
        {
            if (page is null)
                throw new ArgumentNullException (nameof (page));

            if (!TabPages.Contains (page))
                return false;

            if (page.Pinned || !page.Closable)
                return false;

            var closingArgs = new TabPageCancelEventArgs (page);
            TabClosing?.Invoke (this, closingArgs);

            if (closingArgs.Cancel)
                return false;

            TabPages.Remove (page);
            TabClosed?.Invoke (this, new TabPageEventArgs (page));
            return true;
        }


        public TabPage AddTab (string text, bool select = true)
        {
            var page = new TabPage (text);
            TabPages.Add (page);

            if (select)
                SelectedTabPage = page;

            return page;
        }

        public TabPage AddClosableTab (string text, bool select = true)
        {
            var page = new TabPage (text) {
                Closable = true
            };

            TabPages.Add (page);

            if (select)
                SelectedTabPage = page;

            return page;
        }

        public TabPage AddPinnedTab (string text, bool select = true)
        {
            var page = new TabPage (text) {
                Pinned = true,
                Closable = false
            };

            TabPages.Add (page);

            if (select)
                SelectedTabPage = page;

            return page;
        }

        public void CloseAllTabsExcept (TabPage page)
        {
            for (int i = TabPages.Count - 1; i >= 0; i--) {
                var current = TabPages[i];
                if (!ReferenceEquals (current, page) && !current.Pinned && current.Closable)
                    CloseTab (current);
            }
        }

        /// <summary>
        /// Gets the collection of tabs contained by this TabControl.
        /// </summary>
        public TabPageCollection TabPages { get; }

        private TabPage? GetPageFromTab (TabStripItem? item) => TabPages.FirstOrDefault (p => p.TabStripItem == item);

        /// <summary>
        /// Raises the SelectedIndexChanged event.
        /// </summary>
        protected virtual void OnSelectedIndexChanged (EventArgs e) => SelectedIndexChanged?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Gets or sets the index of the currently selected tab page. This value will be -1 if there is not a selected tab page;
        /// </summary>
        public int SelectedIndex {
            get => tab_strip.SelectedIndex;
            set => tab_strip.SelectedIndex = value;
        }

        /// <summary>
        /// Raised when the value of the SelectedIndex property changes.
        /// </summary>
        public event EventHandler? SelectedIndexChanged;

        /// <summary>
        /// Gets or sets the currently selected tab page.
        /// </summary>
        public TabPage? SelectedTabPage {
            get => GetPageFromTab (tab_strip.SelectedTab);
            set {
                if (value is null) {
                    tab_strip.SelectedTab = null;
                    return;
                }

                var index = TabPages.IndexOf (value);

                if (index == -1)
                    throw new ArgumentException ("TabPage is not part of this TabControl");

                tab_strip.SelectedIndex = index;
            }
        }

        // Handles changes of the TabStrip's selected tab.
        private void TabStrip_SelectedTabChanged (object? sender, EventArgs e)
        {
            var old_selected = Controls.OfType<TabPage> ().FirstOrDefault (tp => tp.Visible);
            var new_selected = GetPageFromTab (tab_strip.SelectedTab);

            if (old_selected == new_selected)
                return;

            if (old_selected != null)
                old_selected.Visible = false;

            if (new_selected != null)
                new_selected.Visible = true;

            OnSelectedIndexChanged (EventArgs.Empty);
        }
    }
}
