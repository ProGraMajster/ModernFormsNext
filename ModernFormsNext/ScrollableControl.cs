using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Layout;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a ScrollableControl control.
    /// </summary>
    public partial class ScrollableControl : Control
    {
        private readonly HorizontalScrollBar hscrollbar;
        private readonly VerticalScrollBar vscrollbar;
        private readonly SizeGrip sizegrip;

        private Point scroll_position = Point.Empty;
        private Size canvas_size = Size.Empty;
        private Size auto_scroll_min_size = Size.Empty;
        private Size auto_scroll_margin = Size.Empty;
        private bool auto_scroll = false;
        private readonly bool force_hscroll_visible = false;
        private readonly bool force_vscroll_visible = false;
        private bool preserve_anchor_layout_during_scrollbar_adjustment;

        /// <summary>
        /// Initializes a new instance of the ScrollableControl class.
        /// </summary>
        public ScrollableControl ()
        {
            hscrollbar = Controls.AddImplicitControl (new HorizontalScrollBar {
                Visible = false
            });

            hscrollbar.ValueChanged += HandleScroll;
            hscrollbar.RangeMetadataChanged += HandleScrollMetadata;
            hscrollbar.Scroll += (o, e) => OnScroll (e);

            vscrollbar = Controls.AddImplicitControl (new VerticalScrollBar {
                Visible = false
            });

            vscrollbar.ValueChanged += HandleScroll;
            vscrollbar.RangeMetadataChanged += HandleScrollMetadata;
            vscrollbar.Scroll += (o, e) => OnScroll (e);

            sizegrip = Controls.AddImplicitControl (new SizeGrip {
                Visible = false
            });

            SizeChanged += (o, e) => Recalculate (true);
            VisibleChanged += (o, e) => Recalculate (true);
        }

        /// <summary>
        /// Adjusts the scrollbars based on the currently contained controls.
        /// </summary>
        protected virtual void AdjustFormScrollbars (bool displayScrollbars)
            => Recalculate (preserve_anchor_layout_during_scrollbar_adjustment);

        /// <summary>
        /// Gets or sets a value indicating the user can scroll to controls beyond the ScrollableControl's bounds.
        /// </summary>
        public bool AutoScroll {
            get => auto_scroll;
            set {
                if (auto_scroll != value) {
                    auto_scroll = value;
                    PerformLayout (this, nameof (AutoScroll));
                }
            }
        }

        internal bool IsInternalScrollControl (Control c)
            => ReferenceEquals (c, hscrollbar) || ReferenceEquals (c, vscrollbar) || ReferenceEquals (c, sizegrip);

        internal Point TouchScrollPosition => scroll_position;

        // A touch host supplies logical-pixel finger movement. Moving the finger left/up advances
        // the corresponding scrollbar, while moving right/down rewinds it. Updating the real
        // scrollbar values keeps their thumbs and the existing ScrollWindow path synchronized.
        internal bool ScrollByTouchDelta (Point delta)
        {
            if (!AutoScroll)
                return false;

            var oldHorizontal = hscrollbar.Value;
            var oldVertical = vscrollbar.Value;
            var newHorizontal = hscrollbar.Visible
                ? Math.Clamp (oldHorizontal - delta.X, hscrollbar.Minimum, hscrollbar.Maximum)
                : oldHorizontal;
            var newVertical = vscrollbar.Visible
                ? Math.Clamp (oldVertical - delta.Y, vscrollbar.Minimum, vscrollbar.Maximum)
                : oldVertical;

            if (newHorizontal != oldHorizontal) {
                hscrollbar.Value = newHorizontal;
                OnScroll (new ScrollEventArgs (ScrollEventType.ThumbTrack, newHorizontal));
            }

            if (newVertical != oldVertical) {
                vscrollbar.Value = newVertical;
                OnScroll (new ScrollEventArgs (ScrollEventType.ThumbTrack, newVertical));
            }

            return newHorizontal != oldHorizontal || newVertical != oldVertical;
        }

        // Calculates and sets the current canvas size.
        private void CalculateCanvasSize ()
        {
            var width = 0;
            var height = 0;
            var clientOrigin = base.DisplayRectangle.Location;
            // PresentationPadding is already in logical layout units and includes the
            // current visual-state transition. Reading Padding here loses styled/animated
            // metrics; scaling the presentation value would apply DPI a second time.
            var padding = PresentationPadding;
            var extra_width = scroll_position.X + padding.Right - clientOrigin.X;
            var extra_height = scroll_position.Y + padding.Bottom - clientOrigin.Y;
            var layout_bounds = LayoutEngine == DefaultLayout.Instance
                ? Size.Empty
                : CommonProperties.GetLayoutBounds (this);

            foreach (var c in Controls) {
                if (IsInternalScrollControl (c))
                    continue;

                if (c.Dock == DockStyle.Right)
                    extra_width += c.Width;
                else if (c.Dock == DockStyle.Bottom)
                    extra_height += c.Height;
            }

            if (!auto_scroll_min_size.IsEmpty) {
                width = auto_scroll_min_size.Width;
                height = auto_scroll_min_size.Height;
            }

            // Non-default layout engines arrange children as a group and publish the full
            // content extent through CommonProperties. Reading individual child bounds is not
            // sufficient for flow/table layouts, especially on the same pass that adds or
            // removes children, because those bounds belong to the preceding arrangement.
            if (!layout_bounds.IsEmpty) {
                canvas_size.Width = Math.Max (width, layout_bounds.Width + padding.Right - clientOrigin.X);
                canvas_size.Height = Math.Max (height, layout_bounds.Height + padding.Bottom - clientOrigin.Y);
                return;
            }

            foreach (var c in Controls) {
                if (IsInternalScrollControl (c))
                    continue;

                switch (c.Dock) {
                    case DockStyle.Left:
                        width = Math.Max (width, c.Right + extra_width);
                        continue;

                    case DockStyle.Top:
                        height = Math.Max (height, c.Bottom + extra_height);
                        continue;

                    case DockStyle.Bottom:
                    case DockStyle.Right:
                    case DockStyle.Fill:
                        continue;

                    default:
                        var anchor = c.Anchor;

                        if (anchor.HasFlag (AnchorStyles.Left) && !anchor.HasFlag (AnchorStyles.Right))
                            width = Math.Max (width, c.Right + extra_width);

                        if (anchor.HasFlag (AnchorStyles.Top) && !anchor.HasFlag (AnchorStyles.Bottom))
                            height = Math.Max (height, c.Bottom + extra_height);

                        continue;
                }
            }

            canvas_size.Width = width;
            canvas_size.Height = height;
        }

        /// <inheritdoc/>
        public override Rectangle DisplayRectangle {
            get {
                // A ScrollableControl DisplayRectangle includes Padding, while a normal Control does not.
                var rect = base.DisplayRectangle;

                if (hscrollbar.Visible)
                    rect.Height -= hscrollbar.Height;

                if (vscrollbar.Visible)
                    rect.Width -= vscrollbar.Width;

                return LayoutUtils.DeflateRect (rect, PresentationPadding);
            }
        }

        // Handles events from the scrollbars to update the window position.
        private void HandleScroll (object? sender, EventArgs e)
        {
            if (sender == vscrollbar && vscrollbar.Visible)
                ScrollWindow (0, vscrollbar.Value - vscrollbar.Minimum - scroll_position.Y);
            else if (sender == hscrollbar && hscrollbar.Visible)
                ScrollWindow (hscrollbar.Value - hscrollbar.Minimum - scroll_position.X, 0);
        }

        /// <summary>
        /// Provides access to the properties of the horizontal scrollbar.
        /// </summary>
        public ScrollProperties HorizontalScrollProperties => new ScrollProperties (hscrollbar);

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            var usesDerivedLayoutEngine = LayoutEngine != DefaultLayout.Instance;
            if (usesDerivedLayoutEngine) {
                // Derived layout engines must arrange their children and publish LayoutBounds
                // before the scroll range is calculated. Otherwise the range lags one pass behind.
                base.OnLayout (e);
            }

            CalculateCanvasSize ();

            // A Bounds layout runs after the client size has already changed. Keep the existing
            // anchor distances until DefaultLayout consumes them below; ResumeLayout(false)
            // would otherwise reinitialize every explicit child against the new client size.
            // Other layouts retain the established behavior, including presentation-padding
            // transitions where anchored explicit bounds intentionally stay unchanged.
            var previousPreserveAnchorLayout = preserve_anchor_layout_during_scrollbar_adjustment;
            preserve_anchor_layout_during_scrollbar_adjustment = string.Equals (
                e.AffectedProperty,
                PropertyNames.Bounds,
                StringComparison.Ordinal);

            try {
                AdjustFormScrollbars (AutoScroll);
            } finally {
                preserve_anchor_layout_during_scrollbar_adjustment = previousPreserveAnchorLayout;
            }

            // DefaultLayout keeps the established pre-adjustment order because it owns anchor
            // distance initialization during bounds and presentation-padding changes.
            if (!usesDerivedLayoutEngine)
                base.OnLayout (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (e.Handled || !Enabled || !AutoScroll)
                return;

            if (e.Delta.Y != 0 && vscrollbar.Visible) {
                vscrollbar.RaiseMouseWheel (e);
                return;
            }

            if (e.Delta.X != 0 && hscrollbar.Visible)
                hscrollbar.RaiseMouseWheel (e);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Raises the Scroll event.
        /// </summary>
        protected virtual void OnScroll (ScrollEventArgs e) => Scroll?.Invoke (this, e);

        // Recalculates all components of the ScrollableControl.
        private void Recalculate (bool doLayout)
        {
            scroll_update_depth++;
            try {
            var canvas = canvas_size;
            // Canvas, child Bounds and scrollbar SetBounds all use logical layout units.
            // ClientRectangle is DPI-scaled and cannot be mixed into these calculations.
            var client = base.DisplayRectangle;

            canvas.Width += auto_scroll_margin.Width;
            canvas.Height += auto_scroll_margin.Height;

            var right_edge = client.Width;
            var bottom_edge = client.Height;
            var prev_right_edge = 0;
            var prev_bottom_edge = 0;

            var hscroll_visible = false;
            var vscroll_visible = false;

            var bar_size = 15;

            do {
                prev_right_edge = right_edge;
                prev_bottom_edge = bottom_edge;

                if ((force_hscroll_visible || (canvas.Width > right_edge && auto_scroll)) && client.Width > 0) {
                    hscroll_visible = true;
                    bottom_edge = client.Height - bar_size;
                } else {
                    hscroll_visible = false;
                    bottom_edge = client.Height;
                }

                if ((force_vscroll_visible || (canvas.Height > bottom_edge && auto_scroll)) && client.Height > 0) {
                    vscroll_visible = true;
                    right_edge = client.Width - bar_size;
                } else {
                    vscroll_visible = false;
                    right_edge = client.Width;
                }
            }
            while (right_edge != prev_right_edge || bottom_edge != prev_bottom_edge);

            right_edge = Math.Max (right_edge, 0);
            bottom_edge = Math.Max (bottom_edge, 0);

            if (!vscroll_visible)
                vscrollbar.Value = vscrollbar.Minimum;

            if (!hscroll_visible)
                hscrollbar.Value = hscrollbar.Minimum;

            if (hscroll_visible) {
                hscrollbar.LargeChange = right_edge;
                hscrollbar.SmallChange = 5;
                hscrollbar.Maximum = ScrollMaximum(hscrollbar.Minimum, canvas.Width, right_edge);
            } else {
                if (hscrollbar.Visible)
                    ScrollWindow (-scroll_position.X, 0);

                scroll_position.X = 0;
            }

            if (vscroll_visible) {
                vscrollbar.LargeChange = bottom_edge;
                vscrollbar.SmallChange = 5;
                vscrollbar.Maximum = ScrollMaximum(vscrollbar.Minimum, canvas.Height, bottom_edge);
            } else {
                if (vscrollbar.Visible)
                    ScrollWindow (0, -scroll_position.Y);

                scroll_position.Y = 0;
            }

            SuspendLayout ();
            try {
            var sizegrip_visible = hscroll_visible && vscroll_visible;

            hscrollbar.SetBounds (
                0,
                client.Height - bar_size,
                sizegrip_visible ? client.Width - bar_size : client.Width,
                bar_size);

            hscrollbar.Visible = hscroll_visible;

            vscrollbar.SetBounds (
                client.Width - bar_size,
                0,
                bar_size,
                sizegrip_visible ? client.Height - bar_size : client.Height);

            vscrollbar.Visible = vscroll_visible;

            sizegrip.SetBounds (
                client.Width - bar_size,
                client.Height - bar_size,
                bar_size,
                bar_size);

            sizegrip.Visible = sizegrip_visible;

            } finally { ResumeLayout (doLayout); }
            } finally { EndScrollUpdate(); }
        }

        /// <summary>
        /// Raised when the ScrollableControl is scrolled.
        /// </summary>
        public event EventHandler<ScrollEventArgs>? Scroll;

        // Scrolls the control by the requested offsets.
        private void ScrollWindow (int xOffset, int yOffset)
        {
            if (xOffset == 0 && yOffset == 0)
                return;

            scroll_update_depth++;
            SuspendLayout ();
            try {
                // Commit first: a LocationChanged callback can scroll again. Its delta must be
                // measured from this position, and must not be overwritten when it returns.
                scroll_position.Offset (xOffset, yOffset);
                var lifetime = new AccessibilityControlLifetime(this);
                List<Exception>? failures = null;
                foreach (var c in Controls.ToArray()) {
                    if (!lifetime.IsCurrent || IsDisposed) break;
                    if (IsInternalScrollControl (c) || c.IsDisposed || !ReferenceEquals(c.Parent, this)) continue;
                    try { c.Location = new Point (c.Left - xOffset, c.Top - yOffset); }
                    catch (Exception error) { (failures ??= []).Add(error); }
                }
                if (failures is not null) throw new AggregateException(failures);
            } finally {
                try { ResumeLayout (false); }
                finally { EndScrollUpdate(); }
            }
        }

        /// <summary>
        /// Provides access to the properties of the vertical scrollbar.
        /// </summary>
        public ScrollProperties VerticalScrollProperties => new ScrollProperties (vscrollbar);
    }
}
