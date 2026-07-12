using System;
using System.Drawing;
using System.Linq;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a ToolBar control.
    /// </summary>
    public class ToolBar : MenuBase
    {
        private ToolTip? itemToolTip;
        private Timer? itemToolTipTimer;
        private MenuItem? pendingToolTipItem;

        /// <summary>
        /// Initializes a new instance of the ToolBar class.
        /// </summary>
        public ToolBar ()
        {
            Dock = DockStyle.Top;
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 34);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
          (style) => {
              style.Border.Bottom.Width = 1;
          });

        /// <inheritdoc/>
        protected override bool IsTopLevelMenu => true;

        /// <inheritdoc/>
        protected override void LayoutItems ()
        {
            StackLayoutEngine.HorizontalExpand.Layout (ClientRectangle, Items.Cast<ILayoutable> ());
        }

        /// <inheritdoc/>
        protected override void OnHoverChanged (MenuItem? oldItem, MenuItem? newItem)
        {
            base.OnHoverChanged (oldItem, newItem);

            itemToolTipTimer?.Stop ();
            itemToolTip?.Hide (this);
            pendingToolTipItem = string.IsNullOrWhiteSpace (newItem?.ToolTipText) ? null : newItem;

            if (pendingToolTipItem is not null) {
                itemToolTip ??= new ToolTip ();
                if (itemToolTipTimer is null) {
                    itemToolTipTimer = new Timer { Interval = ToolTip.DefaultDelay };
                    itemToolTipTimer.Tick += ItemToolTipTimer_Tick;
                }

                itemToolTipTimer.Start ();
            }
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                if (itemToolTipTimer is not null) {
                    itemToolTipTimer.Stop ();
                    itemToolTipTimer.Tick -= ItemToolTipTimer_Tick;
                    itemToolTipTimer.Dispose ();
                    itemToolTipTimer = null;
                }

                itemToolTip?.Dispose ();
                itemToolTip = null;
            }

            base.Dispose (disposing);
        }

        private void ItemToolTipTimer_Tick (object? sender, EventArgs e)
        {
            itemToolTipTimer?.Stop ();

            var item = pendingToolTipItem;
            if (item?.Hovered != true || itemToolTip is null || FindForm () is null)
                return;

            itemToolTip.Show (
                item.ToolTipText,
                this,
                new Point (item.Bounds.Left, item.Bounds.Bottom),
                itemToolTip.AutoPopDelay);
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
