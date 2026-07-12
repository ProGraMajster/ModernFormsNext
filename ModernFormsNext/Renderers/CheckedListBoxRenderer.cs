using System;
using System.Drawing;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Represents a class that can render a <see cref="CheckedListBox"/>.
    /// </summary>
    public class CheckedListBoxRenderer : Renderer<CheckedListBox>
    {
        /// <inheritdoc/>
        protected override void Render(CheckedListBox control, PaintEventArgs e)
        {
            for (var i = control.FirstVisibleIndex; i < Math.Min(control.Items.Count, control.FirstVisibleIndex + control.VisibleItemCount + 1); i++)
            {
                var item = control.Items[i];
                var bounds = control.GetItemRectangle(i);

                RenderItem(control, item, i, bounds, e);
            }

            if (control.Items.Count == 0 && control.Selected && control.ShowFocusCues)
            {
                var client = control.ClientRectangle;
                client.Height = control.ScaledItemHeight;

                e.Canvas.DrawFocusRectangle(client, 1);
            }
        }

        /// <summary>
        /// Renders a single <see cref="CheckedListBox"/> item.
        /// </summary>
        /// <param name="control">The control being rendered.</param>
        /// <param name="item">The item value to render.</param>
        /// <param name="index">The zero-based item index.</param>
        /// <param name="bounds">The item bounds in device pixels.</param>
        /// <param name="e">The paint event data.</param>
        protected virtual void RenderItem(CheckedListBox control, object item, int index, Rectangle bounds, PaintEventArgs e)
        {
            if (control.Items.SelectedIndexes.Contains(index))
                e.Canvas.FillRectangle(bounds, Theme.ControlHighlightLowColor);
            else if (control.ShowHover && control.Items.HoveredIndex == index)
                e.Canvas.FillRectangle(bounds, Theme.ControlMidColor);

            if (control.Selected && control.ShowFocusCues && control.Items.FocusedIndex == index)
                e.Canvas.DrawFocusRectangle(bounds, 1);

            var check_bounds = control.GetItemCheckRectangle(index);
            ControlPaint.DrawCheckBox(e, check_bounds, control.GetItemCheckState(index), !control.Enabled);

            var text_bounds = control.GetItemTextRectangle(index);

            if (item.ToString() is { } text)
                e.Canvas.DrawText(text, text_bounds, control, ContentAlignment.MiddleLeft, maxLines: 1);
        }
    }
}
