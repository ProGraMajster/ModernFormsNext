using System;
using System.Drawing;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a custom painted TrackBar control.
    /// </summary>
    public class TrackBar : Control
    {
        private const int DEFAULT_MINIMUM = 0;
        private const int DEFAULT_MAXIMUM = 10;
        private const int DEFAULT_VALUE = 0;
        private const int DEFAULT_SMALL_CHANGE = 1;
        private const int DEFAULT_LARGE_CHANGE = 5;
        private const int DEFAULT_TICK_FREQUENCY = 1;
        private const int DEFAULT_PREFERRED_THICKNESS = 32;

        private bool thumb_pressed;
        private bool thumb_hovered;
        private int drag_offset_from_thumb_origin;

        private int minimum = DEFAULT_MINIMUM;
        private int maximum = DEFAULT_MAXIMUM;
        private int current_value = DEFAULT_VALUE;
        private int small_change = DEFAULT_SMALL_CHANGE;
        private int large_change = DEFAULT_LARGE_CHANGE;
        private int tick_frequency = DEFAULT_TICK_FREQUENCY;
        private Orientation orientation = Orientation.Horizontal;
        private TickStyle tick_style = TickStyle.BottomRight;

        /// <summary>
        /// Initializes a new instance of the TrackBar class.
        /// </summary>
        public TrackBar ()
        {
            AutoSize = true;
            TabStop = true;

            SetAutoSizeMode (AutoSizeMode.GrowOnly);
            SetControlBehavior (ControlBehaviors.Hoverable | ControlBehaviors.Selectable);
        }

        /// <summary>
        /// The default ControlStyle for all instances of TrackBar.
        /// </summary>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.BackgroundColor;
            });

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (104, DEFAULT_PREFERRED_THICKNESS);

        /// <summary>
        /// Gets or sets whether the control automatically keeps its thickness
        /// appropriate for the current orientation.
        /// </summary>
        public override bool AutoSize {
            get => base.AutoSize;
            set {
                if (base.AutoSize != value) {
                    base.AutoSize = value;
                    AdjustAutoSizeDimension ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the amount by which the value changes when PageUp/PageDown is used.
        /// </summary>
        public int LargeChange {
            get => large_change;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (LargeChange), $"Value '{value}' must be greater than or equal to 0.");

                if (large_change != value) {
                    large_change = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum value of the TrackBar.
        /// </summary>
        public int Maximum {
            get => maximum;
            set {
                if (maximum != value) {
                    maximum = value;

                    if (maximum < minimum)
                        minimum = maximum;

                    if (current_value > maximum)
                        SetValueCore (maximum, raiseScroll: false);

                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimum value of the TrackBar.
        /// </summary>
        public int Minimum {
            get => minimum;
            set {
                if (minimum != value) {
                    minimum = value;

                    if (minimum > maximum)
                        maximum = minimum;

                    if (current_value < minimum)
                        SetValueCore (minimum, raiseScroll: false);

                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the orientation of the TrackBar.
        /// </summary>
        public Orientation Orientation {
            get => orientation;
            set {
                if (orientation != value) {
                    orientation = value;
                    AdjustAutoSizeDimension ();
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the amount by which the value changes using the arrow keys.
        /// </summary>
        public int SmallChange {
            get => small_change;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (SmallChange), $"Value '{value}' must be greater than or equal to 0.");

                if (small_change != value) {
                    small_change = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the spacing between tick marks in value units.
        /// </summary>
        public int TickFrequency {
            get => tick_frequency;
            set {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException (nameof (TickFrequency), $"Value '{value}' must be greater than 0.");

                if (tick_frequency != value) {
                    tick_frequency = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets where tick marks are drawn.
        /// </summary>
        public TickStyle TickStyle {
            get => tick_style;
            set {
                if (tick_style != value) {
                    tick_style = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current value of the TrackBar.
        /// </summary>
        public int Value {
            get => current_value;
            set {
                if (value < minimum || value > maximum)
                    throw new ArgumentOutOfRangeException (nameof (Value), $"'{value}' is not a valid value for 'Value'. 'Value' should be between 'Minimum' and 'Maximum'.");

                SetValueCore (value, raiseScroll: true);
            }
        }

        /// <summary>
        /// Raised when the TrackBar is scrolled.
        /// </summary>
        public event EventHandler? Scroll;

        /// <summary>
        /// Raised when the Value of the TrackBar changes.
        /// </summary>
        public event EventHandler? ValueChanged;

        internal bool ThumbHovered => thumb_hovered;
        internal bool ThumbPressed => thumb_pressed;

        private void AdjustAutoSizeDimension ()
        {
            if (!AutoSize)
                return;

            if (Orientation == Orientation.Horizontal)
                Height = DEFAULT_PREFERRED_THICKNESS;
            else
                Width = DEFAULT_PREFERRED_THICKNESS;
        }

        private int Clamp (int value) => Math.Max (minimum, Math.Min (maximum, value));

        private void ChangeValueBy (int delta)
        {
            var new_value = Clamp (current_value + delta);
            SetValueCore (new_value, raiseScroll: true);
        }

        private Rectangle GetThumbBounds ()
            => RenderManager.GetRenderer<TrackBarRenderer> ()!.GetThumbBounds (this);

        private int PositionToValue (Point location)
            => RenderManager.GetRenderer<TrackBarRenderer> ()!.PositionToValue (this, location);

        /// <inheritdoc/>
        public override Size GetPreferredSize (Size proposedSize)
        {
            var current = Size;

            if (Orientation == Orientation.Horizontal) {
                var width = Math.Max (current.Width, DefaultSize.Width);
                return new Size (width, DEFAULT_PREFERRED_THICKNESS);
            }

            var height = Math.Max (current.Height, DefaultSize.Height);
            return new Size (DEFAULT_PREFERRED_THICKNESS, height);
        }

        /// <inheritdoc/>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            switch (e.KeyCode) {
                case Keys.Left:
                    if (Orientation == Orientation.Horizontal) {
                        ChangeValueBy (-SmallChange);
                        e.Handled = true;
                        return;
                    }
                    break;

                case Keys.Right:
                    if (Orientation == Orientation.Horizontal) {
                        ChangeValueBy (SmallChange);
                        e.Handled = true;
                        return;
                    }
                    break;

                case Keys.Up:
                    if (Orientation == Orientation.Vertical) {
                        ChangeValueBy (SmallChange);
                        e.Handled = true;
                        return;
                    }
                    break;

                case Keys.Down:
                    if (Orientation == Orientation.Vertical) {
                        ChangeValueBy (-SmallChange);
                        e.Handled = true;
                        return;
                    }
                    break;

                case Keys.PageUp:
                    ChangeValueBy (LargeChange);
                    e.Handled = true;
                    return;

                case Keys.PageDown:
                    ChangeValueBy (-LargeChange);
                    e.Handled = true;
                    return;

                case Keys.Home:
                    SetValueCore (Minimum, raiseScroll: true);
                    e.Handled = true;
                    return;

                case Keys.End:
                    SetValueCore (Maximum, raiseScroll: true);
                    e.Handled = true;
                    return;
            }

            base.OnKeyDown (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (!Enabled || !e.Button.HasFlag (MouseButtons.Left))
                return;

            Select ();

            var thumb_bounds = GetThumbBounds ();

            if (thumb_bounds.Contains (e.Location)) {
                thumb_pressed = true;
                drag_offset_from_thumb_origin = Orientation == Orientation.Horizontal
                    ? e.X - thumb_bounds.X
                    : e.Y - thumb_bounds.Y;

                Invalidate ();
                return;
            }

            SetValueCore (PositionToValue (e.Location), raiseScroll: true);
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);

            if (thumb_hovered) {
                thumb_hovered = false;
                Invalidate ();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            var renderer = RenderManager.GetRenderer<TrackBarRenderer> ()!;
            var thumb_bounds = renderer.GetThumbBounds (this);

            var new_hover_state = thumb_bounds.Contains (e.Location);

            if (thumb_hovered != new_hover_state) {
                thumb_hovered = new_hover_state;
                Invalidate ();
            }

            if (!thumb_pressed)
                return;

            var new_value = renderer.PositionToValueFromThumb (this, e.Location, drag_offset_from_thumb_origin);
            SetValueCore (new_value, raiseScroll: true);
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (thumb_pressed) {
                thumb_pressed = false;
                Invalidate ();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (!Enabled)
                return;

            if (e.Delta.Y > 0)
                ChangeValueBy (SmallChange);
            else if (e.Delta.Y < 0)
                ChangeValueBy (-SmallChange);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged (EventArgs e)
        {
            base.OnSizeChanged (e);
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (AutoSize) {
                if (Orientation == Orientation.Horizontal)
                    height = DEFAULT_PREFERRED_THICKNESS;
                else
                    width = DEFAULT_PREFERRED_THICKNESS;
            }

            base.SetBoundsCore (x, y, width, height, specified);
        }

        /// <summary>
        /// Raises the Scroll event.
        /// </summary>
        protected virtual void OnScroll (EventArgs e) => Scroll?.Invoke (this, e);

        /// <summary>
        /// Raises the ValueChanged event.
        /// </summary>
        protected virtual void OnValueChanged (EventArgs e) => ValueChanged?.Invoke (this, e);

        private void SetValueCore (int value, bool raiseScroll)
        {
            value = Clamp (value);

            if (current_value == value) {
                Invalidate ();
                return;
            }

            current_value = value;

            if (raiseScroll)
                OnScroll (EventArgs.Empty);

            OnValueChanged (EventArgs.Empty);
            Invalidate ();
        }
    }
}
