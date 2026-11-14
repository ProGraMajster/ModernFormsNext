using System;
using System.Drawing;
using System.Runtime.ExceptionServices;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents the base class of a ScrollBar control.
    /// </summary>
    public abstract class ScrollBar : Control
    {
        private int large_change = 10;
        private int maximum = 100;
        private int minimum = 0;
        private int current_value = 0;
        private int small_change = 1;
        private bool thumb_pressed;
        private int thumbclick_offset;		        // Position of the last button-down event relative to the thumb edge
        
        private readonly bool vertical;
        internal Orientation AccessibilityOrientation => vertical ? Orientation.Vertical : Orientation.Horizontal;
        // Scoped to the committed ValueChanged dispatch. A nested public Value assignment
        // supplies its own neutral cause instead of inheriting the outer input operation.
        internal ScrollEventType ValueChangeType { get; private set; } = ScrollEventType.ThumbPosition;

        internal int thumb_drag_position;     // Current pixel of the midpoint of the thumb drag 

        /// <summary>
        /// Initializes a new instance of the ScrollBar class.
        /// </summary>
        protected ScrollBar (bool vertical = false)
        {
            this.vertical = vertical;
            TabStop = false;
        }

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => style.BackgroundColor = Theme.ControlMidHighColor);

        /// <summary>
        /// Gets or sets the amount the ScrollBar will change when clicked in the track area.
        /// </summary>
        public int LargeChange {
            get => (int)Math.Min (large_change, (long)maximum - minimum + 1);
            set {
                ChangeRange(() => {
                    if (value < 0)
                        throw new ArgumentOutOfRangeException (nameof (LargeChange), $"Value '{value}' must be greater than or equal to 0.");

                    if (large_change != value) {
                        large_change = value;
                        UpdateFromValue (Value);
                    }
                });
            }
        }

        /// <summary>
        /// Gets or sets the maximum value the ScrollBar will allow.
        /// </summary>
        public int Maximum {
            get => maximum;
            set {
                ChangeRange(() => {
                    if (maximum != value) {
                        maximum = value;

                        if (maximum < minimum)
                            minimum = maximum;
                        if (Value > maximum)
                            Value = maximum;

                        UpdateFromValue (Value);
                    }
                });
            }
        }

        /// <summary>
        /// Gets or sets the minimum value the ScrollBar will allow.
        /// </summary>
        public int Minimum {
            get => minimum;
            set {
                ChangeRange(() => {
                    if (minimum != value) {
                        minimum = value;

                        if (minimum > maximum)
                            maximum = minimum;
                        if (Value < minimum)
                            Value = minimum;

                        UpdateFromValue (Value);
                    }
                });
            }
        }

        /// <summary>
        /// Raised after input changes the scrollbar value, following <see cref="ValueChanged"/>.
        /// </summary>
        /// <remarks>
        /// Arrows and wheel report small increments/decrements, the track reports large changes,
        /// and dragging reports ThumbTrack. Values use the raw scrollbar range. No event is raised
        /// for a no-op or a public Value/range assignment; observe ValueChanged for those changes.
        /// A reentrant ValueChanged observer that replaces the value retires the obsolete input event.
        /// </remarks>
        public event EventHandler<ScrollEventArgs>? Scroll;

        /// <summary>
        /// Gets or sets the amount the ScrollBar will change when the increment or decrement arrows are clicked.
        /// </summary>
        public int SmallChange {
            get => small_change;
            set {
                ChangeRange(() => {
                    if (value < 0)
                        throw new ArgumentOutOfRangeException (nameof (SmallChange), $"Value '{value}' must be greater than or equal to 0.");

                    if (small_change != value) {
                        small_change = value;
                        Invalidate ();
                    }
                });
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Gets or sets the current value of the ScrollBar.
        /// </summary>
        public int Value {
            get => current_value;
            set {
                if (value < minimum || value > maximum)
                    throw new ArgumentOutOfRangeException (nameof (Value), $"'{value}' is not a valid value for 'Value'. 'Value' should be between 'Minimum' and 'Maximum'");

                UpdateFromValue (value);
            }
        }

        /// <summary>
        /// Raised when the value of the ScrollBar changes.
        /// </summary>
        public event EventHandler? ValueChanged;

        // Owning viewports need committed range changes even if the current raw value did not move.
        internal event EventHandler? RangeMetadataChanged;

        // The number of possible ScrollBar values.
        private long PossibleValuesCount => (long)maximum - minimum + 1;

        // Retrieves the effective track bounds from the renderer.
        private Rectangle GetEffectiveTrackBounds () => RenderManager.GetRenderer<ScrollBarRenderer> ()!.GetEffectiveTrackBounds (this);

        private ScrollBarElement GetElementAtLocation (Point location)
        {
            var renderer = RenderManager.GetRenderer<ScrollBarRenderer> ()!;

            if (renderer.GetDecrementArrowBounds (this).Contains (location))
                return ScrollBarElement.DecrementArrow;

            if (renderer.GetIncrementArrowBounds (this).Contains (location))
                return ScrollBarElement.IncrementArrow;

            if (renderer.GetThumbDragBounds (this).Contains (location))
                return ScrollBarElement.Thumb;

            if (renderer.GetDecrementTrackBounds (this).Contains (location))
                return ScrollBarElement.DecrementTrack;

            if (renderer.GetIncrementTrackBounds (this).Contains (location))
                return ScrollBarElement.IncrementTrack;

            // In theory this shouldn't be possible...
            return ScrollBarElement.None;
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged (EventArgs e)
        {
            base.OnSizeChanged (e);

            UpdateFromValue (current_value);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (!Enabled || !e.Button.HasFlag (MouseButtons.Left))
                return;

            switch (GetElementAtLocation (e.Location)) {
                case ScrollBarElement.DecrementArrow:
                    SetValue((int)Math.Max ((long)Value - SmallChange, Minimum), ScrollEventType.SmallDecrement);
                    break;
                case ScrollBarElement.DecrementTrack:
                    SetValue((int)Math.Max ((long)Value - LargeChange, Minimum), ScrollEventType.LargeDecrement);
                    break;
                case ScrollBarElement.Thumb:
                    thumb_pressed = true;
                    thumbclick_offset = (vertical ? e.Y : e.X) - thumb_drag_position;
                    break;
                case ScrollBarElement.IncrementTrack:
                    SetValue((int)Math.Min ((long)Value + LargeChange, Maximum), ScrollEventType.LargeIncrement);
                    break;
                case ScrollBarElement.IncrementArrow:
                    SetValue((int)Math.Min ((long)Value + SmallChange, Maximum), ScrollEventType.SmallIncrement);
                    break;
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (thumb_pressed) {
                UpdateFromPoint ((vertical ? e.Y : e.X) - thumbclick_offset);
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            thumb_pressed = false;
        }

        internal override void CancelPointerInteraction (int? pointerId = null)
        {
            thumb_pressed = false;
            base.CancelPointerInteraction (pointerId);
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (e.Handled || !Enabled)
                return;

            var delta = vertical || e.Delta.X == 0 ? e.Delta.Y : e.Delta.X;
            if (delta == 0)
                return;

            var previousValue = Value;
            SetValue((int)Math.Clamp((long)Value - ((long)delta * SmallChange), Minimum, Maximum),
                delta < 0 ? ScrollEventType.SmallIncrement : ScrollEventType.SmallDecrement);
            e.Handled = Value != previousValue;
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
        protected virtual void OnScroll (ScrollEventArgs e)
        {
            e.NewValue = Math.Max (e.NewValue, Minimum);
            e.NewValue = Math.Min (e.NewValue, Maximum);

            Scroll?.Invoke (this, e);
        }

        /// <summary>
        /// Raises the ValueChanged event.
        /// </summary>
        protected virtual void OnValueChanged (EventArgs e)
        {
            Exception? failure = null;
            try { ValueChanged?.Invoke(this, e); }
            catch (Exception exception) { failure = exception; }
            try { if (!IsDisposed && !Disposing) NotifyAccessibilityClients(AccessibleEvents.ValueChange); }
            catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        /// <inheritdoc/>
        protected override void OnVisibleChanged (EventArgs e)
        {
            base.OnVisibleChanged (e);

            if (Visible)
                UpdateFromValue (Value);
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            base.SetBoundsCore (x, y, width, height, specified);

            UpdateFromValue (Value);
        }

        // Updates ScrollBar value from a thumb drag position.
        private void UpdateFromPoint (int pixel)
        {
            if (thumb_drag_position == pixel)
                return;

            var effective_track_bounds = GetEffectiveTrackBounds ();

            pixel = Math.Max (pixel, vertical ? effective_track_bounds.Top : effective_track_bounds.Left);
            pixel = Math.Min (pixel, vertical ? effective_track_bounds.Bottom : effective_track_bounds.Right);

            if ((vertical ? effective_track_bounds.Height : effective_track_bounds.Width) <= 0) return;
            var position_percent = 
                vertical ? (double)(pixel - effective_track_bounds.Top) / effective_track_bounds.Height
                         : (double)(pixel - effective_track_bounds.Left) / effective_track_bounds.Width;

            var value_position = (long)(Math.Clamp(position_percent, 0d, 1d) * (PossibleValuesCount - 1));

            var new_value = (int)Math.Clamp((long)minimum + value_position, minimum, maximum);

            thumb_drag_position = pixel;

            int previous = current_value;
            current_value = new_value;
            // The position can change within one value; repaint even without ValueChanged.
            PublishValueChange(previous, ScrollEventType.ThumbTrack);
        }

        // Updates thumb drag position from a ScrollBar value.
        private void UpdateFromValue (int value, ScrollEventType type = ScrollEventType.ThumbPosition)
        {
            value = Math.Max (value, minimum);
            value = Math.Min (value, maximum);

            var possible = PossibleValuesCount - 1;
            var value_percent = possible > 0 ? ((double)value - minimum) / possible : 0d;

            var effective_track_bounds = GetEffectiveTrackBounds ();

            var new_pos =
                vertical ? effective_track_bounds.Y + (value_percent * effective_track_bounds.Height)
                         : effective_track_bounds.X + (value_percent * effective_track_bounds.Width);

            thumb_drag_position = (int)new_pos;

            int previous = current_value;
            current_value = value;
            // Commit before invalidation, whose observers may synchronously read or change Value.
            PublishValueChange(previous, type);
        }

        // Internal input adapters use the same clamp, thumb geometry and ValueChanged path.
        internal void SetValue(int value, ScrollEventType type) => UpdateFromValue(value, type);

        private long valueChangeVersion;

        private void PublishValueChange(int previous, ScrollEventType type)
        {
            Exception? failure = null;
            int committed = current_value;
            if (previous != committed) {
                long version = ++valueChangeVersion;
                var previousType = ValueChangeType;
                ValueChangeType = type;
                try { OnValueChanged(EventArgs.Empty); }
                catch (Exception exception) { failure = exception; }
                finally { ValueChangeType = previousType; }
                try {
                    if (type != ScrollEventType.ThumbPosition && version == valueChangeVersion && !IsDisposed && !Disposing)
                        OnScroll(new ScrollEventArgs(type, previous, committed,
                            vertical ? ScrollOrientation.VerticalScroll : ScrollOrientation.HorizontalScroll));
                }
                catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
            }
            try { if (!IsDisposed && !Disposing) Invalidate(); }
            catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void ChangeRange(Action mutation)
        {
            var previous = (minimum, maximum, small_change, LargeChange);
            Exception? failure = null;
            try { mutation(); }
            catch (Exception exception) { failure = exception; }
            try
            {
                if (previous != (minimum, maximum, small_change, LargeChange) && !IsDisposed && !Disposing)
                {
                    try { RangeMetadataChanged?.Invoke(this, EventArgs.Empty); }
                    catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
                    try { if (!IsDisposed && !Disposing) NotifyAccessibilityClients(AccessibleEvents.RangeValueChanged); }
                    catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
                }
            }
            catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private enum ScrollBarElement
        {
            None,
            DecrementArrow,
            DecrementTrack,
            Thumb,
            IncrementTrack,
            IncrementArrow
        }
    }
}
