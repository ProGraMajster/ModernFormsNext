using System;
using System.Drawing;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Represents a class that can render a ListView.
    /// </summary>
    public class ListViewRenderer : Renderer<ListView>
    {
        /// <inheritdoc/>
        protected override void Render (ListView control, PaintEventArgs e)
        {
            if (!control.VirtualizationEnabled) {
                foreach (var item in control.Items) RenderItem (control, item, e);
                return;
            }
            // The shared host has already arranged device-space item metadata during layout.
            // Paint enumerates only the bounded range and keeps the existing RenderItem override contract.
            e.Canvas.Save ();
            try {
                e.Canvas.Clip (control.NativeHostingViewport);
                var range = control.RealizedRange;
                for (int index = range.StartIndex; index < range.EndIndex && index < control.Items.Count; index++) {
                    e.Canvas.Save ();
                    try {
                        e.Canvas.Clip (control.Items[index].Bounds);
                        RenderItem (control, control.Items[index], e);
                    } finally { e.Canvas.Restore (); }
                }
            } finally { e.Canvas.Restore (); }
        }

        /// <summary>
        /// Renders a ListViewItem.
        /// </summary>
        protected virtual void RenderItem (ListView control, ListViewItem item, PaintEventArgs e)
        {
            if (item.Selected)
                e.Canvas.FillRectangle (item.Bounds, Theme.ControlHighlightLowColor);

            if (control.VirtualizationEnabled && control.ShowFocusCues && control.GetFocusedItemIndex() is int focused &&
                focused >= 0 && focused < control.Items.Count && ReferenceEquals(control.Items[focused], item))
                e.Canvas.DrawFocusRectangle (item.Bounds, 1);

            if (control.VirtualizationEnabled && control.ItemsLayout == ItemsLayout.Stack) {
                // Stack rows may be much shorter than the legacy icon grid. Keep the image and text
                // inline within the actual cell instead of placing text below an unconditional 32px icon.
                var textBounds = item.Bounds;
                int padding = e.LogicalToDeviceUnits (3);
                // ItemSize already reserves trailing cell spacing. Keep the remaining vertical
                // room for the font's full line metrics; another inset can make RichTextKit
                // reject the sole line entirely in a compact row, especially at fractional DPI.
                textBounds.Inflate (-padding, 0);
                if (item.Image is not null) {
                    int imageSize = Math.Max (0, Math.Min (e.LogicalToDeviceUnits (32), Math.Min (textBounds.Width, textBounds.Height)));
                    var imageBounds = new Rectangle (textBounds.Left, textBounds.Top + (textBounds.Height - imageSize) / 2, imageSize, imageSize);
                    if (imageSize > 0) e.Canvas.DrawBitmap (item.Image, imageBounds);
                    textBounds.X += imageSize + padding;
                    textBounds.Width -= imageSize + padding;
                }
                if (textBounds.Width > 0 && textBounds.Height > 0 && !string.IsNullOrWhiteSpace (item.Text))
                    e.Canvas.DrawText (item.Text, textBounds, control, ContentAlignment.MiddleLeft, maxLines: 1, ellipsis: true);
                return;
            }

            var image_size = e.LogicalToDeviceUnits (32);
            var image_area = new Rectangle (item.Bounds.Left, item.Bounds.Top, item.Bounds.Width, item.Bounds.Width);
            var image_bounds = DrawingExtensions.CenterSquare (image_area, image_size);
            image_bounds.Y = item.Bounds.Top + e.LogicalToDeviceUnits (3);

            if (item.Image != null)
                e.Canvas.DrawBitmap (item.Image, image_bounds);

            if (!string.IsNullOrWhiteSpace (item.Text)) {
                var font_size = e.LogicalToDeviceUnits (Theme.ItemFontSize);

                e.Canvas.Save ();
                e.Canvas.Clip (item.Bounds);

                var text_bounds = new Rectangle (item.Bounds.Left, image_bounds.Bottom + e.LogicalToDeviceUnits (3), item.Bounds.Width, item.Bounds.Bottom - image_bounds.Bottom - e.LogicalToDeviceUnits (3));

                e.Canvas.DrawText (item.Text, Theme.UIFont, font_size, text_bounds, Theme.ForegroundColor, ContentAlignment.MiddleCenter);

                e.Canvas.Restore ();
            }
        }
    }
}
