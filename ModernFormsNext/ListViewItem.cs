using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a ListViewItem.
    /// </summary>
    public class ListViewItem
    {
        private bool selected;
        private string text = string.Empty;

        /// <summary>
        /// Gets the current bounding box of the item.
        /// </summary>
        public Rectangle Bounds { get; private set; }

        /// <summary>
        /// Gets or sets the image displayed on the item.
        /// </summary>
        public SKBitmap? Image { get; set; }

        /// <summary>
        /// Gets the ListView this item is currently a part of.
        /// </summary>
        public ListView? Parent { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating if the item is currently selected.
        /// </summary>
        public bool Selected
        {
            get => selected;
            set
            {
                if (selected == value)
                    return;

                selected = value;
                Parent?.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Selection);
                Parent?.Invalidate();
            }
        }

        /// <summary>
        /// Sets the bounding box of the item. This is internal API and should not be called.
        /// </summary>
        public void SetBounds (int x, int y, int width, int height)
        {
            Bounds = new Rectangle (x, y, width, height);
        }

        /// <summary>
        /// Gets or sets an object with additional user data about this item.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Gets or sets the text displayed on the item.
        /// </summary>
        public string Text
        {
            get => text;
            set
            {
                value ??= string.Empty;
                if (text == value)
                    return;

                text = value;
                Parent?.NotifyAccessibilityClients(Accessibility.AccessibleEvents.NameChange);
                Parent?.Invalidate();
            }
        }
    }
}
