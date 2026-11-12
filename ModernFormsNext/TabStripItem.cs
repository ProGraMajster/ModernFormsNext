using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a single tab item within a <see cref="TabStrip"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="TabStripItem"/> defines the visual and behavioral representation of a tab,
    /// including text, icon, layout, and interaction state.
    /// </para>
    /// <para>
    /// It is internally associated with a <see cref="TabPage"/> and managed by <see cref="TabStrip"/>.
    /// </para>
    /// </remarks>
    public class TabStripItem : ILayoutable
    {
        private bool enabled = true;
        private string text;
        private SKImage? icon;
        private bool closable = false;
        private bool pinned;
        private TabDisplayMode displayMode = TabDisplayMode.TextOnly;

        /// <summary>
        /// Initializes a new instance of the <see cref="TabStripItem"/> class.
        /// </summary>
        /// <param name="text">The text displayed on the tab.</param>
        public TabStripItem(string? text = null)
        {
            this.text = text ?? string.Empty;
        }

        /// <summary>
        /// Gets the bounds of the tab item.
        /// </summary>
        public Rectangle Bounds { get; private set; }

        /// <summary>
        /// Gets the bounds of the close button.
        /// </summary>
        public Rectangle CloseButtonBounds { get; internal set; }

        /// <summary>
        /// Gets the bounds of the icon.
        /// </summary>
        public Rectangle IconBounds { get; internal set; }

        /// <summary>
        /// Gets the bounds of the text.
        /// </summary>
        public Rectangle TextBounds { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tab is enabled.
        /// </summary>
        /// <remarks>
        /// Disabled tabs cannot be selected or interacted with.
        /// </remarks>
        public bool Enabled
        {
            get => enabled && Parent?.Enabled == true;
            set
            {
                if (enabled != value)
                {
                    enabled = value;
                    Parent?.Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the tab is currently hovered.
        /// </summary>
        public bool Hovered => Parent?.Tabs.HoveredIndex == Index;

        /// <summary>
        /// Gets a value indicating whether the close button is currently hovered.
        /// </summary>
        public bool CloseButtonHovered => Parent?.Tabs.CloseButtonHoveredIndex == Index;

        private int Index => Parent?.Tabs.IndexOf(this) ?? -1;

        /// <summary>
        /// Gets or sets the icon displayed on the tab.
        /// </summary>
        /// <remarks>The borrowed image is drawn into a 16 by 16 logical-pixel slot, scaled once for DPI. The caller owns its lifetime.</remarks>
        public SKImage? Icon
        {
            get => icon;
            set
            {
                if (!ReferenceEquals(icon, value))
                {
                    icon = value;
                    Parent?.Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the tab can be closed.
        /// </summary>
        public bool Closable
        {
            get => closable;
            set
            {
                if (closable != value)
                {
                    closable = value;
                    Parent?.Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the tab is pinned.
        /// </summary>
        /// <remarks>
        /// Pinned tabs cannot be closed and are usually displayed with priority.
        /// </remarks>
        public bool Pinned
        {
            get => pinned;
            set
            {
                if (pinned != value)
                {
                    pinned = value;
                    Parent?.Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets how the tab content is displayed.
        /// </summary>
        public TabDisplayMode DisplayMode
        {
            get => displayMode;
            set
            {
                if (displayMode != value)
                {
                    displayMode = value;
                    Parent?.Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the margin of the tab.
        /// </summary>
        public Padding Margin { get; set; } = Padding.Empty;

        /// <summary>
        /// Gets or sets the padding of the tab content.
        /// </summary>
        public Padding Padding { get; set; } = new Padding(14, 0, 14, 0);

        /// <summary>
        /// Gets the parent <see cref="TabStrip"/> that owns this item.
        /// </summary>
        public TabStrip? Parent { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this tab is currently selected.
        /// </summary>
        public bool Selected => Parent?.SelectedTab == this;

        /// <summary>
        /// Sets the bounds of the tab item.
        /// </summary>
        public void SetBounds(int x, int y, int width, int height, BoundsSpecified specified = BoundsSpecified.All)
        {
            Bounds = new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// Gets or sets user-defined data associated with the tab.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Gets or sets the text displayed on the tab.
        /// </summary>
        public string Text
        {
            get => text;
            set
            {
                if (text != value)
                {
                    text = value;
                    Parent?.Invalidate();
                }
            }
        }

        /// <summary>
        /// Calculates the preferred size of the tab.
        /// </summary>
        /// <param name="proposedSize">The proposed size.</param>
        /// <returns>The preferred size in device pixels for the strip's item layout.</returns>
        /// <remarks>
        /// The width is calculated based on text, icon, close button, and padding,
        /// while respecting <see cref="TabStrip.TabMinWidth"/> and <see cref="TabStrip.TabMaxWidth"/>.
        /// Measurement uses the same effective font, selected emphasis and typography as painting.
        /// Height includes natural text/icon height and vertical breathing room. Explicit strip
        /// height or size constraints may still clip content; width caps use a single-line ellipsis.
        /// </remarks>
        public Size GetPreferredSize(Size proposedSize)
        {
            // Paint rounds each edge separately. Rounding their sum can reserve one pixel less
            // at fractional DPI and trigger ellipsis for a caption whose measured width fits.
            var horizontalPadding = Parent is { } owner
                ? owner.LogicalToDeviceUnits(Padding.Left) + owner.LogicalToDeviceUnits(Padding.Right)
                : Padding.Horizontal;
            SKSize textSize = SKSize.Empty;
            if (DisplayMode != TabDisplayMode.IconOnly && !string.IsNullOrWhiteSpace(Text))
                textSize = Parent?.MeasureHeaderText(Text, Enabled && Selected)
                    ?? TextMeasurer.MeasureText(Text, Theme.UIFont, Theme.FontSize, new Size(int.MaxValue, int.MaxValue));
            int textWidth = (int)Math.Ceiling(textSize.Width);

            int iconWidth = 0;
            if (Icon != null && DisplayMode != TabDisplayMode.TextOnly)
                iconWidth = Parent?.LogicalToDeviceUnits(16) ?? 16;

            int closeWidth = 0;
            if (Closable && !Pinned && Parent?.ShowCloseButtons == true)
                closeWidth = Parent.LogicalToDeviceUnits(14);

            int gap = 0;
            if (iconWidth > 0 && textWidth > 0)
                gap += Parent?.LogicalToDeviceUnits(8) ?? 8;

            if ((iconWidth > 0 || textWidth > 0) && closeWidth > 0)
                gap += Parent?.LogicalToDeviceUnits(8) ?? 8;

            var width = horizontalPadding + textWidth + iconWidth + closeWidth + gap;

            width = Math.Max(width, Parent?.LogicalToDeviceUnits(Parent.TabMinWidth) ?? 48);

            if (Parent?.TabMaxWidth is int maxWidth && maxWidth > 0)
                width = Math.Min(width, Parent.LogicalToDeviceUnits(maxWidth));

            int contentHeight = Math.Max((int)Math.Ceiling(textSize.Height), Math.Max(iconWidth, closeWidth));
            int verticalPadding = Parent is { } parent
                ? parent.LogicalToDeviceUnits(Padding.Top) + parent.LogicalToDeviceUnits(Padding.Bottom) + parent.LogicalToDeviceUnits(8)
                : Padding.Vertical + 8;
            int height = Math.Max(Parent?.LogicalToDeviceUnits(31) ?? 31, contentHeight + verticalPadding);
            return new Size(width, height);
        }

        /// <summary>
        /// Defines how the tab content is displayed.
        /// </summary>
        public enum TabDisplayMode
        {
            /// <summary>
            /// Displays only text.
            /// </summary>
            TextOnly,

            /// <summary>
            /// Displays only an icon.
            /// </summary>
            IconOnly,

            /// <summary>
            /// Displays both icon and text.
            /// </summary>
            IconAndText
        }
    }
}
