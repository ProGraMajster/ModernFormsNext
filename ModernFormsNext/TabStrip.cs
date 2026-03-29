using System;
using System.Drawing;
using System.Linq;
using ModernFormsNext.Renderers;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a control responsible for rendering and managing tab headers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="TabStrip"/> is used internally by <see cref="TabControl"/> to display
    /// tab headers and handle tab selection, navigation, and interaction.
    /// </para>
    /// <para>
    /// It manages a collection of <see cref="TabStripItem"/> objects, each representing
    /// a visual tab.
    /// </para>
    /// </remarks>
    public class TabStrip : Control
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TabStrip"/> class.
        /// </summary>
        /// <remarks>
        /// Initializes the <see cref="Tabs"/> collection.
        /// </remarks>
        public TabStrip()
        {
            Tabs = new TabStripItemCollection(this);
        }

        /// <summary>
        /// Gets or sets the background color of the tab strip area.
        /// </summary>
        public SKColor? TabStripBackgroundColor { get; set; } = null;

        /// <summary>
        /// Gets or sets the background color of tabs.
        /// </summary>
        public SKColor? TabBackgroundColor { get; set; } = null;

        /// <summary>
        /// Gets or sets the background color of the selected tab.
        /// </summary>
        public SKColor? SelectedTabBackgroundColor { get; set; } = null;

        /// <summary>
        /// Gets or sets the background color of the hovered tab.
        /// </summary>
        public SKColor? HoveredTabBackgroundColor { get; set; } = null;

        /// <summary>
        /// Gets or sets a value indicating whether close buttons are visible on tabs.
        /// </summary>
        public bool ShowCloseButtons { get; set; } = false;

        /// <summary>
        /// Gets or sets the corner radius of tab headers.
        /// </summary>
        public int TabCornerRadius { get; set; } = 10;

        /// <summary>
        /// Gets or sets the spacing between tabs.
        /// </summary>
        public int TabSpacing { get; set; } = 10;

        /// <summary>
        /// Gets or sets the minimum width of a tab.
        /// </summary>
        public int TabMinWidth { get; set; } = 56;

        /// <summary>
        /// Gets or sets the maximum width of a tab.
        /// </summary>
        public int TabMaxWidth { get; set; } = 220;

        /// <summary>
        /// Gets or sets a value indicating whether tabs can be closed using the middle mouse button.
        /// </summary>
        public bool CloseTabOnMiddleClick { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether pinned tabs can be selected.
        /// </summary>
        public bool AllowSelectPinnedTabs { get; set; } = true;

        /// <summary>
        /// Occurs when a tab close button is clicked.
        /// </summary>
        public event EventHandler<TabStripItemEventArgs>? TabCloseButtonClicked;

        private TabStripItem? GetCloseButtonAtLocation(Point location)
            => Tabs.FirstOrDefault(t => t.CloseButtonBounds.Contains(location));

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size(600, 31);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle(Control.DefaultStyle,
            (style) =>
            {
                style.BackgroundColor = Theme.BackgroundColor;
            });

        private int FindNextTab(int startIndex, bool forward, bool wrap)
        {
            if (forward)
            {
                for (var i = startIndex + 1; i < Tabs.Count; i++)
                    if (Tabs[i].Enabled)
                        return i;

                if (wrap)
                {
                    for (var i = 0; i < startIndex; i++)
                        if (Tabs[i].Enabled)
                            return i;
                }
            }
            else
            {
                for (var i = startIndex - 1; i >= 0; i--)
                    if (Tabs[i].Enabled)
                        return i;

                if (wrap)
                {
                    for (var i = Tabs.Count - 1; i > startIndex; i--)
                        if (Tabs[i].Enabled)
                            return i;
                }
            }

            return -1;
        }

        private TabStripItem? GetTabAtLocation(Point location)
            => Tabs.FirstOrDefault(tp => tp.Bounds.Contains(location));

        /// <summary>
        /// Arranges the layout of tabs within the control.
        /// </summary>
        /// <remarks>
        /// Uses a horizontal layout engine to distribute tab items.
        /// </remarks>
        private void LayoutTabs()
        {
            StackLayoutEngine.HorizontalExpand.Layout(ClientRectangle, Tabs.Cast<ILayoutable>());
        }

        /// <inheritdoc/>
        protected override void OnClick(MouseEventArgs e)
        {
            base.OnClick(e);

            var closeTab = GetCloseButtonAtLocation(e.Location);
            if (closeTab != null && ShowCloseButtons && closeTab.Closable && !closeTab.Pinned)
            {
                TabCloseButtonClicked?.Invoke(this, new TabStripItemEventArgs(closeTab));
                return;
            }

            var clickedTab = GetTabAtLocation(e.Location);
            if (clickedTab?.Enabled == true)
                SelectedTab = clickedTab;
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Navigation logic (keyboard shortcuts)
            if (e.KeyCode == Keys.Right || (e.KeyCode == Keys.Tab && e.Control && !e.Shift) || (e.KeyCode == Keys.PageDown && e.Control))
            {
                SelectNextTab(true, false, (e.KeyCode == Keys.Tab && e.Control && !e.Shift) || (e.KeyCode == Keys.PageDown && e.Control));
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Left || (e.KeyCode == Keys.Tab && e.Control && e.Shift) || (e.KeyCode == Keys.PageUp && e.Control))
            {
                SelectNextTab(false, false, (e.KeyCode == Keys.Tab && e.Control && e.Shift) || (e.KeyCode == Keys.PageUp && e.Control));
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.End)
            {
                SelectNextTab(true, true, false);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Home)
            {
                SelectNextTab(false, true, false);
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            Tabs.HoveredIndex = -1;
            Tabs.CloseButtonHoveredIndex = -1;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var hover_tab = GetTabAtLocation(e.Location);
            Tabs.HoveredIndex = hover_tab is null ? -1 : Tabs.IndexOf(hover_tab);

            var hoverClose = GetCloseButtonAtLocation(e.Location);
            Tabs.CloseButtonHoveredIndex = hoverClose is null ? -1 : Tabs.IndexOf(hoverClose);
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            LayoutTabs();
            RenderManager.Render(this, e);
        }

        /// <summary>
        /// Raises the <see cref="SelectedTabChanged"/> event.
        /// </summary>
        protected virtual void OnSelectedTabChanged(EventArgs e)
            => SelectedTabChanged?.Invoke(this, e);

        /// <summary>
        /// Occurs when the selected tab changes.
        /// </summary>
        public event EventHandler? SelectedTabChanged;

        private void SelectNextTab(bool forward, bool end, bool wrap)
        {
            if (!end)
            {
                var index = FindNextTab(SelectedIndex, forward, wrap);
                if (index != -1)
                    SelectedIndex = index;
                return;
            }

            if (forward)
            {
                var index = FindNextTab(Tabs.Count, false, false);
                if (index != -1)
                    SelectedIndex = index;
                return;
            }

            var idx = FindNextTab(-1, true, false);
            if (idx != -1)
                SelectedIndex = idx;
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle(DefaultStyle);

        /// <summary>
        /// Gets or sets the index of the selected tab.
        /// </summary>
        public int SelectedIndex
        {
            get => Tabs.SelectedIndex;
            set
            {
                if (Tabs.SelectedIndex != value)
                {
                    Tabs.SelectedIndex = value;
                    OnSelectedTabChanged(EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the currently selected tab item.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified item is not part of this control.
        /// </exception>
        public TabStripItem? SelectedTab
        {
            get => SelectedIndex >= 0 ? Tabs[SelectedIndex] : null;
            set
            {
                if (value is null)
                {
                    SelectedIndex = -1;
                    return;
                }

                var index = Tabs.IndexOf(value);

                if (index == -1)
                    throw new ArgumentException("Item is not part of this list");

                SelectedIndex = index;
            }
        }

        /// <summary>
        /// Gets the collection of tab items.
        /// </summary>
        public TabStripItemCollection Tabs { get; }
    }
}