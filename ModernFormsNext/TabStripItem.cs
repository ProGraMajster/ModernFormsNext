using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext
{
    public class TabStripItem : ILayoutable
    {
        private bool enabled = true;
        private string text;
        private SKImage? icon;
        private bool closable = false;
        private bool pinned;
        private TabDisplayMode displayMode = TabDisplayMode.TextOnly;

        public TabStripItem (string? text = null)
        {
            this.text = text ?? string.Empty;
        }

        public Rectangle Bounds { get; private set; }

        public Rectangle CloseButtonBounds { get; internal set; }

        public Rectangle IconBounds { get; internal set; }

        public Rectangle TextBounds { get; internal set; }

        public bool Enabled {
            get => enabled && Parent?.Enabled == true;
            set {
                if (enabled != value) {
                    enabled = value;
                    Parent?.Invalidate ();
                }
            }
        }

        public bool Hovered => Parent?.Tabs.HoveredIndex == Index;

        public bool CloseButtonHovered => Parent?.Tabs.CloseButtonHoveredIndex == Index;

        private int Index => Parent?.Tabs.IndexOf (this) ?? -1;

        public SKImage? Icon {
            get => icon;
            set {
                if (!ReferenceEquals (icon, value)) {
                    icon = value;
                    Parent?.Invalidate ();
                }
            }
        }

        public bool Closable {
            get => closable;
            set {
                if (closable != value) {
                    closable = value;
                    Parent?.Invalidate ();
                }
            }
        }

        public bool Pinned {
            get => pinned;
            set {
                if (pinned != value) {
                    pinned = value;
                    Parent?.Invalidate ();
                }
            }
        }

        public TabDisplayMode DisplayMode {
            get => displayMode;
            set {
                if (displayMode != value) {
                    displayMode = value;
                    Parent?.Invalidate ();
                }
            }
        }

        public Padding Margin { get; set; } = Padding.Empty;

        public Padding Padding { get; set; } = new Padding (14, 0, 14, 0);

        public TabStrip? Parent { get; internal set; }

        public bool Selected => Parent?.SelectedTab == this;

        public void SetBounds (int x, int y, int width, int height, BoundsSpecified specified = BoundsSpecified.All)
        {
            Bounds = new Rectangle (x, y, width, height);
        }

        public object? Tag { get; set; }

        public string Text {
            get => text;
            set {
                if (text != value) {
                    text = value;
                    Parent?.Invalidate ();
                }
            }
        }

        public Size GetPreferredSize (Size proposedSize)
        {
            var horizontalPadding = Parent?.LogicalToDeviceUnits (Padding.Horizontal) ?? Padding.Horizontal;
            var fontSize = Parent?.LogicalToDeviceUnits (Theme.FontSize) ?? Theme.FontSize;

            int textWidth = 0;
            if (DisplayMode != TabDisplayMode.IconOnly && !string.IsNullOrWhiteSpace (Text))
                textWidth = (int)Math.Round (TextMeasurer.MeasureText (Text, Theme.UIFont, fontSize).Width);

            int iconWidth = 0;
            if (Icon != null && DisplayMode != TabDisplayMode.TextOnly)
                iconWidth = Parent?.LogicalToDeviceUnits (16) ?? 16;

            int closeWidth = 0;
            if (Closable && !Pinned && Parent?.ShowCloseButtons == true)
                closeWidth = Parent.LogicalToDeviceUnits (18);

            int gap = 0;
            if (iconWidth > 0 && textWidth > 0)
                gap += Parent?.LogicalToDeviceUnits (8) ?? 8;

            if ((iconWidth > 0 || textWidth > 0) && closeWidth > 0)
                gap += Parent?.LogicalToDeviceUnits (8) ?? 8;

            var width = horizontalPadding + textWidth + iconWidth + closeWidth + gap;

            width = Math.Max (width, Parent?.TabMinWidth ?? 48);

            if (Parent?.TabMaxWidth is int maxWidth && maxWidth > 0)
                width = Math.Min (width, maxWidth);

            return new Size (width, Bounds.Height);
        }

        public enum TabDisplayMode
        {
            TextOnly,
            IconOnly,
            IconAndText
        }
    }
}
