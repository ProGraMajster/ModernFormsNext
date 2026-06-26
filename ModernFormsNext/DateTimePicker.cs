using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a control that allows the user to select a date and/or time value.
    /// </summary>
    /// <remarks>
    /// This implementation is designed for ModernFormsNext and does not depend on the native
    /// Win32 DateTimePicker control. The class contains the control state, input handling,
    /// layout calculations, and public API, while all drawing logic is delegated to
    /// <see cref="DateTimePickerRenderer"/>.
    /// </remarks>
    [DefaultProperty (nameof (Value))]
    [DefaultEvent (nameof (ValueChanged))]
    public class DateTimePicker : Control
    {
        private const int DefaultButtonWidth = 22;
        private const int DefaultCheckBoxSize = 14;
        private const int DefaultHorizontalPadding = 6;
        private const int DefaultVerticalPadding = 4;

        /// <summary>
        /// Default minimum value compatible with common desktop scenarios.
        /// </summary>
        private static readonly DateTime s_minDateTime = new (1753, 1, 1);

        /// <summary>
        /// Default maximum value compatible with common desktop scenarios.
        /// </summary>
        private static readonly DateTime s_maxDateTime = new (9998, 12, 31);

        private DateTime value = DateTime.Now;
        private DateTime minDate = DateTime.MinValue;
        private DateTime maxDate = DateTime.MaxValue;

        private bool userHasSetValue;
        private bool isChecked = true;
        private bool showCheckBox;
        private bool showUpDown;
        private bool isDroppedDown;

        private DateTimePickerFormat format = DateTimePickerFormat.Long;
        private string? customFormat;

        private Rectangle checkBoxRect;
        private Rectangle textRect;
        private Rectangle buttonRect;

        private bool buttonPressed;
        private bool hoveredButton;

        private PopupWindow? popupWindow;
        private DateTimePickerCalendar? popupCalendar;

        /// <summary>
        /// Gets the active popup window instance, if any.
        /// </summary>
        internal PopupWindow? PopupWindow => popupWindow;

        /// <summary>
        /// Applies a date selected from the popup calendar.
        /// </summary>
        /// <param name="selectedDate">The selected date.</param>
        internal void ApplyDropDownValue (DateTime selectedDate)
        {
            var current = Value;

            var merged = new DateTime (
                selectedDate.Year,
                selectedDate.Month,
                selectedDate.Day,
                current.Hour,
                current.Minute,
                current.Second,
                current.Millisecond);

            Value = merged;
            CloseDropDown ();
        }

        /// <summary>
        /// Closes the active drop-down popup, if one exists.
        /// </summary>
        internal void CloseDropDown ()
        {
            if (popupWindow is not null) {
                popupWindow.Hide ();
                popupWindow.Dispose ();
                popupWindow = null;
            }

            popupCalendar = null;

            if (isDroppedDown) {
                isDroppedDown = false;
                OnCloseUp (EventArgs.Empty);
                Invalidate (buttonRect);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DateTimePicker"/> class.
        /// </summary>
        public DateTimePicker ()
        {
            SetControlBehavior (ControlBehaviors.Selectable, true);
            SetControlBehavior (ControlBehaviors.Hoverable, true);
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged, false);

            TabStop = true;
            Size = DefaultSize;

            UpdateLayoutRects ();
        }

        /// <summary>
        /// Gets the minimum date supported by the current culture calendar and this control.
        /// </summary>
        public static DateTime MinimumDateTime {
            get {
                DateTime cultureMin = CultureInfo.CurrentCulture.Calendar.MinSupportedDateTime;
                return cultureMin.Year < 1753 ? s_minDateTime : cultureMin;
            }
        }

        /// <summary>
        /// Gets the maximum date supported by the current culture calendar and this control.
        /// </summary>
        public static DateTime MaximumDateTime {
            get {
                DateTime cultureMax = CultureInfo.CurrentCulture.Calendar.MaxSupportedDateTime;
                return cultureMax.Year > s_maxDateTime.Year ? s_maxDateTime : cultureMax;
            }
        }

        /// <summary>
        /// Gets the preferred control height.
        /// </summary>
        [Browsable (false)]
        public int PreferredHeight => 32;

        /// <summary>
        /// Gets or sets the visual display format of the value.
        /// </summary>
        [DefaultValue (DateTimePickerFormat.Long)]
        public DateTimePickerFormat Format {
            get => format;
            set {
                if (format == value)
                    return;

                format = value;
                Invalidate ();

                OnFormatChanged (EventArgs.Empty);
                OnTextChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the custom format string used when <see cref="Format"/> is
        /// <see cref="DateTimePickerFormat.Custom"/>.
        /// </summary>
        [DefaultValue (null)]
        public string? CustomFormat {
            get => customFormat;
            set {
                if (customFormat == value)
                    return;

                customFormat = value;
                Invalidate ();
                OnTextChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a checkbox should be shown on the left side.
        /// </summary>
        /// <remarks>
        /// When enabled, the checkbox controls whether the date/time value is considered active.
        /// If disabled, the control is always treated as checked.
        /// </remarks>
        [DefaultValue (false)]
        public bool ShowCheckBox {
            get => showCheckBox;
            set {
                if (showCheckBox == value)
                    return;

                showCheckBox = value;

                // If the checkbox is hidden, the control must always be considered active.
                if (!showCheckBox)
                    isChecked = true;

                UpdateLayoutRects ();
                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether up/down buttons should be shown instead
        /// of a drop-down calendar button.
        /// </summary>
        [DefaultValue (false)]
        public bool ShowUpDown {
            get => showUpDown;
            set {
                if (showUpDown == value)
                    return;

                showUpDown = value;
                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control is checked.
        /// </summary>
        /// <remarks>
        /// When <see cref="ShowCheckBox"/> is <see langword="false"/>, this property always
        /// behaves as <see langword="true"/>.
        /// </remarks>
        [DefaultValue (true)]
        public bool Checked {
            get => !ShowCheckBox || isChecked;
            set {
                bool newValue = !ShowCheckBox || value;

                if (isChecked == newValue)
                    return;

                isChecked = newValue;
                Invalidate ();

                OnValueChanged (EventArgs.Empty);
                OnTextChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the minimum allowed date.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is outside the supported range or greater than <see cref="MaxDate"/>.
        /// </exception>
        public DateTime MinDate {
            get => EffectiveMinDate (minDate);
            set {
                if (value == minDate)
                    return;

                if (value < MinimumDateTime)
                    throw new ArgumentOutOfRangeException (nameof (value), value, $"MinDate cannot be less than {MinimumDateTime:G}.");

                if (value > EffectiveMaxDate (maxDate))
                    throw new ArgumentOutOfRangeException (nameof (value), value, "MinDate cannot be greater than MaxDate.");

                minDate = value;

                // Keep the current value inside the valid range.
                if (Value < minDate)
                    Value = minDate;

                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets the maximum allowed date.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is outside the supported range or less than <see cref="MinDate"/>.
        /// </exception>
        public DateTime MaxDate {
            get => EffectiveMaxDate (maxDate);
            set {
                if (value == maxDate)
                    return;

                if (value > MaximumDateTime)
                    throw new ArgumentOutOfRangeException (nameof (value), value, $"MaxDate cannot be greater than {MaximumDateTime:G}.");

                if (value < EffectiveMinDate (minDate))
                    throw new ArgumentOutOfRangeException (nameof (value), value, "MaxDate cannot be less than MinDate.");

                maxDate = value;

                // Keep the current value inside the valid range.
                if (Value > maxDate)
                    Value = maxDate;

                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets the currently selected date/time value.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is outside the range defined by
        /// <see cref="MinDate"/> and <see cref="MaxDate"/>.
        /// </exception>
        public DateTime Value {
            get => value;
            set {
                if (value < MinDate || value > MaxDate)
                    throw new ArgumentOutOfRangeException (nameof (value), value, $"Value must be between {MinDate:G} and {MaxDate:G}.");

                bool changed = this.value != value || !userHasSetValue;

                if (!changed)
                    return;

                this.value = value;
                userHasSetValue = true;

                // Setting the value explicitly activates the control when a checkbox is shown.
                if (ShowCheckBox)
                    isChecked = true;

                Invalidate ();
                OnValueChanged (EventArgs.Empty);
                OnTextChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Occurs when <see cref="Value"/> changes.
        /// </summary>
        public event EventHandler? ValueChanged;

        /// <summary>
        /// Occurs when the drop-down part of the control is opened.
        /// </summary>
        public event EventHandler? DropDown;

        /// <summary>
        /// Occurs when the drop-down part of the control is closed.
        /// </summary>
        public event EventHandler? CloseUp;

        /// <summary>
        /// Occurs when <see cref="Format"/> changes.
        /// </summary>
        public event EventHandler? FormatChanged;

        /// <summary>
        /// Gets or sets the formatted text of the current value.
        /// </summary>
        /// <remarks>
        /// Reading this property returns the current display text.
        /// Assigning a value attempts to parse it using the current culture.
        /// </remarks>
        public override string Text {
            get => GetDisplayText ();
            set {
                if (string.IsNullOrWhiteSpace (value)) {
                    ResetValue ();
                    return;
                }

                Value = DateTime.Parse (value, CultureInfo.CurrentCulture);
            }
        }

        /// <summary>
        /// Gets the default size of the control.
        /// </summary>
        protected override Size DefaultSize => new (200, 32);

        /// <summary>
        /// Gets the calculated rectangle of the checkbox area.
        /// </summary>
        internal Rectangle CheckBoxRectangle => checkBoxRect;

        /// <summary>
        /// Gets the calculated rectangle of the text area.
        /// </summary>
        internal Rectangle TextRectangle => textRect;

        /// <summary>
        /// Gets the calculated rectangle of the right-side button area.
        /// </summary>
        internal Rectangle ButtonRectangle => buttonRect;

        /// <summary>
        /// Gets a value indicating whether the right-side button is currently pressed.
        /// </summary>
        internal bool IsButtonPressed => buttonPressed;

        /// <summary>
        /// Gets a value indicating whether the mouse is hovering the right-side button.
        /// </summary>
        internal bool IsButtonHovered => hoveredButton;

        /// <summary>
        /// Gets a value indicating whether the drop-down is currently open.
        /// </summary>
        internal bool IsDropDownOpen => isDroppedDown;

        /// <summary>
        /// Gets the display text that should be rendered.
        /// </summary>
        internal string DisplayText => Checked ? GetDisplayText () : string.Empty;

        /// <summary>
        /// Handles control resize and recalculates internal layout rectangles.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected override void OnResize (EventArgs e)
        {
            base.OnResize (e);
            UpdateLayoutRects ();
        }

        /// <summary>
        /// Updates hover state for the right-side button.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            bool hover = buttonRect.Contains (e.Location);
            if (hoveredButton != hover) {
                hoveredButton = hover;
                Invalidate (buttonRect);
            }
        }

        /// <summary>
        /// Clears hover state when the mouse leaves the control.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);

            if (hoveredButton) {
                hoveredButton = false;
                Invalidate (buttonRect);
            }
        }

        /// <summary>
        /// Handles mouse press interaction for the checkbox and right-side button.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            Select ();

            if (ShowCheckBox && checkBoxRect.Contains (e.Location)) {
                isChecked = !isChecked;
                Invalidate ();

                OnValueChanged (EventArgs.Empty);
                OnTextChanged (EventArgs.Empty);
                return;
            }

            if (buttonRect.Contains (e.Location)) {
                buttonPressed = true;
                Invalidate (buttonRect);
            }
        }

        /// <summary>
        /// Handles mouse release interaction for the right-side button.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            bool wasPressed = buttonPressed;
            buttonPressed = false;

            if (wasPressed)
                Invalidate (buttonRect);

            if (buttonRect.Contains (e.Location)) {
                if (ShowUpDown)
                    HandleUpDownClick (e.Location);
                else
                    ToggleDropDown ();
            }
        }

        /// <summary>
        /// Handles mouse wheel input to increment or decrement the current value.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (!Enabled || (!Checked && ShowCheckBox))
                return;

            if (e.Delta.Y > 0)
                StepValue (+1);
            else if (e.Delta.Y < 0)
                StepValue (-1);
        }

        /// <summary>
        /// Handles keyboard input for value stepping, drop-down toggling, and checkbox toggling.
        /// </summary>
        /// <param name="e">The key event data.</param>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown (e);

            if (!Checked && ShowCheckBox)
                return;

            switch (e.KeyCode) {
                case Keys.Up:
                    StepValue (+1);
                    e.Handled = true;
                    break;

                case Keys.Down:
                    StepValue (-1);
                    e.Handled = true;
                    break;

                case Keys.F4:
                case Keys.Return:
                    if (!ShowUpDown) {
                        ToggleDropDown ();
                        e.Handled = true;
                    }
                    break;

                case Keys.Space:
                    if (ShowCheckBox) {
                        Checked = !Checked;
                        e.Handled = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// Paints the control using the renderer registered in <see cref="RenderManager"/>.
        /// </summary>
        /// <param name="e">The paint event data.</param>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Raises the <see cref="ValueChanged"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnValueChanged (EventArgs e)
        {
            ValueChanged?.Invoke (this, e);
            NotifyAccessibilityClients (AccessibleEvents.ValueChange);
        }

        /// <summary>
        /// Raises the <see cref="DropDown"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnDropDown (EventArgs e)
        {
            DropDown?.Invoke (this, e);
            NotifyAccessibilityClients (AccessibleEvents.StateChange);
        }

        /// <summary>
        /// Raises the <see cref="CloseUp"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnCloseUp (EventArgs e)
        {
            CloseUp?.Invoke (this, e);
            NotifyAccessibilityClients (AccessibleEvents.StateChange);
        }

        /// <summary>
        /// Raises the <see cref="FormatChanged"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnFormatChanged (EventArgs e) => FormatChanged?.Invoke (this, e);

        /// <summary>
        /// Returns the effective minimum date after applying global supported limits.
        /// </summary>
        /// <param name="minDate">The requested minimum date.</param>
        /// <returns>The effective minimum date.</returns>
        private static DateTime EffectiveMinDate (DateTime minDate)
        {
            DateTime minSupported = MinimumDateTime;
            return minDate < minSupported ? minSupported : minDate;
        }

        /// <summary>
        /// Returns the effective maximum date after applying global supported limits.
        /// </summary>
        /// <param name="maxDate">The requested maximum date.</param>
        /// <returns>The effective maximum date.</returns>
        private static DateTime EffectiveMaxDate (DateTime maxDate)
        {
            DateTime maxSupported = MaximumDateTime;
            return maxDate > maxSupported ? maxSupported : maxDate;
        }

        /// <summary>
        /// Formats the current value as display text according to the selected format.
        /// </summary>
        /// <returns>A formatted date/time string.</returns>
        private string GetDisplayText ()
        {
            DateTime displayValue = userHasSetValue ? value : DateTime.Now;

            return Format switch {
                DateTimePickerFormat.Long => displayValue.ToString ("D", CultureInfo.CurrentCulture),
                DateTimePickerFormat.Short => displayValue.ToString ("d", CultureInfo.CurrentCulture),
                DateTimePickerFormat.Time => displayValue.ToString ("t", CultureInfo.CurrentCulture),
                DateTimePickerFormat.Custom => displayValue.ToString (
                    string.IsNullOrWhiteSpace (CustomFormat) ? "G" : CustomFormat,
                    CultureInfo.CurrentCulture),
                _ => displayValue.ToString (CultureInfo.CurrentCulture)
            };
        }

        /// <summary>
        /// Resets the internal value state to its default representation.
        /// </summary>
        /// <remarks>
        /// The stored value becomes <see cref="DateTime.Now"/>, but the control is marked as
        /// not explicitly set. If the checkbox is visible, the control becomes unchecked.
        /// </remarks>
        private void ResetValue ()
        {
            value = DateTime.Now;
            userHasSetValue = false;
            isChecked = !ShowCheckBox;

            Invalidate ();
            OnValueChanged (EventArgs.Empty);
            OnTextChanged (EventArgs.Empty);
        }

        /// <summary>
        /// Updates the internal rectangles used for hit-testing and rendering.
        /// </summary>
        private void UpdateLayoutRects ()
        {
            int x = 1;
            int y = 1;
            int height = Math.Max (0, Height - 2);

            if (ShowCheckBox) {
                int checkBoxY = (Height - DefaultCheckBoxSize) / 2;
                checkBoxRect = new Rectangle (x + 6, checkBoxY, DefaultCheckBoxSize, DefaultCheckBoxSize);
                x = checkBoxRect.Right + 6;
            } else {
                checkBoxRect = Rectangle.Empty;
            }

            buttonRect = new Rectangle (Width - DefaultButtonWidth - 1, y, DefaultButtonWidth, height);

            textRect = new Rectangle (
                x + DefaultHorizontalPadding,
                y + DefaultVerticalPadding,
                Math.Max (0, buttonRect.Left - x - (DefaultHorizontalPadding * 2)),
                Math.Max (0, height - (DefaultVerticalPadding * 2)));
        }

        /// <summary>
        /// Opens or closes the drop-down part of the control.
        /// </summary>
        /// <remarks>
        /// This is currently a placeholder. A future implementation can attach a real calendar popup here.
        /// </remarks>
        /// <summary>
        /// Opens or closes the drop-down calendar.
        /// </summary>
        private void ToggleDropDown ()
        {
            if (ShowUpDown)
                return;

            if (isDroppedDown) {
                CloseDropDown ();
                return;
            }

            var hostForm = FindForm ();
            if (hostForm is null)
                return;

            popupWindow = new PopupWindow (hostForm) {
                Size = new Size (232, 268)
            };

            popupCalendar = new DateTimePickerCalendar (this) {
                Dock = DockStyle.Fill,
                Value = Value,
                MinDate = MinDate,
                MaxDate = MaxDate
            };

            popupWindow.Controls.Add(popupCalendar);

            isDroppedDown = true;
            OnDropDown (EventArgs.Empty);
            Invalidate (buttonRect);

            popupWindow.Show (this, 0, Height);
        }

        /// <summary>
        /// Handles visibility changes.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected override void OnVisibleChanged (EventArgs e)
        {
            if (!Visible)
                CloseDropDown ();

            base.OnVisibleChanged (e);
        }

        /// <summary>
        /// Handles a mouse click in the up/down button area.
        /// </summary>
        /// <param name="location">The mouse location.</param>
        private void HandleUpDownClick (Point location)
        {
            int mid = buttonRect.Top + buttonRect.Height / 2;

            if (location.Y < mid)
                StepValue (+1);
            else
                StepValue (-1);
        }

        /// <summary>
        /// Adjusts the current value according to the active display format.
        /// </summary>
        /// <param name="delta">The step amount. Positive values increment, negative values decrement.</param>
        private void StepValue (int delta)
        {
            DateTime newValue = Format switch {
                DateTimePickerFormat.Time => Value.AddMinutes (delta),
                DateTimePickerFormat.Short => Value.AddDays (delta),
                DateTimePickerFormat.Long => Value.AddDays (delta),
                DateTimePickerFormat.Custom => StepCustom (delta),
                _ => Value.AddDays (delta)
            };

            if (newValue < MinDate)
                newValue = MinDate;

            if (newValue > MaxDate)
                newValue = MaxDate;

            Value = newValue;
        }

        /// <summary>
        /// Adjusts the current value when <see cref="Format"/> is <see cref="DateTimePickerFormat.Custom"/>.
        /// </summary>
        /// <param name="delta">The step amount. Positive values increment, negative values decrement.</param>
        /// <returns>The adjusted date/time value.</returns>
        /// <remarks>
        /// This method uses a simple heuristic based on the custom format string:
        /// time tokens adjust minutes, month tokens adjust months, year tokens adjust years,
        /// otherwise days are adjusted.
        /// </remarks>
        private DateTime StepCustom (int delta)
        {
            string format = CustomFormat ?? string.Empty;

            if (format.Contains ("H") || format.Contains ("h") || format.Contains ("m") || format.Contains ("s"))
                return Value.AddMinutes (delta);

            if (format.Contains ("M"))
                return Value.AddMonths (delta);

            if (format.Contains ("y"))
                return Value.AddYears (delta);

            return Value.AddDays (delta);
        }
    }
}
