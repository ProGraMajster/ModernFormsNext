using System;
using System.Drawing;
using System.Globalization;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a control that displays a numeric value and allows the user
    /// to increment, decrement, or manually edit the value.
    /// </summary>
    public class NumericUpDown : Control
    {
        private decimal currentValue;
        private decimal minimum;
        private decimal maximum = 100m;
        private decimal increment = 1m;

        private int decimalPlaces;
        private bool allowDecimalValues;
        private bool allowManualEdit = true;
        private bool autoIncrement = true;
        private bool thousandsSeparator;
        private bool readOnly;
        private bool updatingEditorText;

        private bool upButtonHovered;
        private bool downButtonHovered;
        private bool upButtonPressed;
        private bool downButtonPressed;

        private readonly NumericUpDownTextBox editor;

        /// <summary>
        /// Gets the default style for all <see cref="NumericUpDown"/> controls.
        /// </summary>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle, style => {
            style.BackgroundColor = Theme.ControlMidColor;
            style.ForegroundColor = Theme.ForegroundColor;
            style.Font = Theme.UIFont;
            style.FontSize = Theme.FontSize;
            style.Border.Color = Theme.BorderLowColor;
            style.Border.Width = 1;
            style.Border.Radius = 2;
        });

        /// <summary>
        /// Gets the default hover style for all <see cref="NumericUpDown"/> controls.
        /// </summary>
        public new static ControlStyle DefaultStyleHover = new ControlStyle (DefaultStyle, style => {
            style.Border.Color = Theme.BorderHighColor;
        });

        /// <summary>
        /// Initializes a new instance of the <see cref="NumericUpDown"/> class.
        /// </summary>
        public NumericUpDown ()
        {
            SetControlBehavior (ControlBehaviors.Selectable, true);
            SetControlBehavior (ControlBehaviors.Hoverable, true);
            SetControlBehavior (ControlBehaviors.ReceivesMouseEvents, true);

            Size = DefaultSize;

            editor = Controls.AddImplicitControl (new NumericUpDownTextBox (this));
            editor.MultiLine = false;
            editor.TextChanged += Editor_TextChanged;
            editor.KeyDown += Editor_KeyDown;
            editor.KeyPress += Editor_KeyPress;
            editor.MouseWheel += Editor_MouseWheel;

            ApplyEditorReadOnly ();
            UpdateEditorBounds ();
            UpdateIncrementFromSettings ();
            UpdateEditText ();
        }

        /// <summary>
        /// Occurs when the <see cref="Value"/> property changes.
        /// </summary>
        public event EventHandler? ValueChanged;

        /// <summary>
        /// Occurs when the user explicitly commits the value, such as by pressing Enter
        /// or when the editor loses selection.
        /// </summary>
        public event EventHandler? ValueCommitted;

        /// <summary>
        /// Gets the style for this control instance.
        /// </summary>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Gets the hover style for this control instance.
        /// </summary>
        public override ControlStyle StyleHover { get; } = new ControlStyle (DefaultStyleHover);

        /// <summary>
        /// Gets the default size of the control.
        /// </summary>
        protected override Size DefaultSize => new Size (120, 28);

        /// <summary>
        /// Gets or sets the current numeric value.
        /// </summary>
        public decimal Value {
            get => currentValue;
            set => SetValue (value, true);
        }

        /// <summary>
        /// Gets or sets the minimum allowed value.
        /// </summary>
        public decimal Minimum {
            get => minimum;
            set {
                minimum = value;

                if (maximum < minimum)
                    maximum = minimum;

                if (currentValue < minimum)
                    SetValue (minimum, true);
                else
                    UpdateEditText ();

                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets the maximum allowed value.
        /// </summary>
        public decimal Maximum {
            get => maximum;
            set {
                maximum = value;

                if (minimum > maximum)
                    minimum = maximum;

                if (currentValue > maximum)
                    SetValue (maximum, true);
                else
                    UpdateEditText ();

                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets the amount by which the value is incremented or decremented.
        /// Setting this property disables automatic increment calculation.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is less than or equal to zero.
        /// </exception>
        public decimal Increment {
            get => increment;
            set {
                if (value <= 0m)
                    throw new ArgumentOutOfRangeException (nameof (Increment), "Increment must be greater than zero.");

                autoIncrement = false;
                increment = value;
            }
        }

        /// <summary>
        /// Gets or sets the number of decimal places displayed by the control.
        /// A value greater than zero automatically enables decimal values.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is less than 0 or greater than 28.
        /// </exception>
        public int DecimalPlaces {
            get => decimalPlaces;
            set {
                if (value < 0 || value > 28)
                    throw new ArgumentOutOfRangeException (nameof (DecimalPlaces), "DecimalPlaces must be between 0 and 28.");

                decimalPlaces = value;

                if (decimalPlaces > 0)
                    allowDecimalValues = true;

                if (!allowDecimalValues)
                    decimalPlaces = 0;

                UpdateIncrementFromSettings ();

                currentValue = RoundToConfiguredPrecision (currentValue);
                UpdateEditText ();
                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether decimal values are allowed.
        /// When set to <see langword="false"/>, <see cref="DecimalPlaces"/> is forced to zero.
        /// </summary>
        public bool AllowDecimalValues {
            get => allowDecimalValues;
            set {
                if (allowDecimalValues == value)
                    return;

                allowDecimalValues = value;

                if (!allowDecimalValues)
                    decimalPlaces = 0;

                UpdateIncrementFromSettings ();
                currentValue = RoundToConfiguredPrecision (currentValue);
                UpdateEditText ();
                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the increment value is automatically
        /// derived from <see cref="DecimalPlaces"/>.
        /// </summary>
        public bool AutoIncrement {
            get => autoIncrement;
            set {
                if (autoIncrement == value)
                    return;

                autoIncrement = value;

                if (autoIncrement)
                    UpdateIncrementFromSettings ();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether group separators are shown.
        /// </summary>
        public bool ThousandsSeparator {
            get => thousandsSeparator;
            set {
                if (thousandsSeparator == value)
                    return;

                thousandsSeparator = value;
                UpdateEditText ();
                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the text editor can be manually modified.
        /// Spinner buttons remain functional even when this property is <see langword="false"/>.
        /// </summary>
        public bool AllowManualEdit {
            get => allowManualEdit;
            set {
                if (allowManualEdit == value)
                    return;

                allowManualEdit = value;
                ApplyEditorReadOnly ();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the embedded editor is read-only.
        /// Spinner buttons remain functional even when this property is <see langword="true"/>.
        /// </summary>
        public bool ReadOnly {
            get => readOnly;
            set {
                if (readOnly == value)
                    return;

                readOnly = value;
                ApplyEditorReadOnly ();
            }
        }

        /// <summary>
        /// Gets or sets the textual representation of the current value.
        /// Assigning text attempts to parse and apply the value.
        /// </summary>
        public override string Text {
            get => editor.Text;
            set {
                if (editor.Text == value)
                    return;

                editor.Text = value;

                if (!updatingEditorText)
                    ValidateEditText (false);
            }
        }

        /// <summary>
        /// Gets the bounds of the text area.
        /// </summary>
        internal Rectangle TextBounds => GetTextBounds ();

        /// <summary>
        /// Gets the bounds of the up button.
        /// </summary>
        internal Rectangle UpButtonBounds => GetUpButtonBounds ();

        /// <summary>
        /// Gets the bounds of the down button.
        /// </summary>
        internal Rectangle DownButtonBounds => GetDownButtonBounds ();

        /// <summary>
        /// Gets a value indicating whether the up button is hovered.
        /// </summary>
        internal bool UpButtonHovered => upButtonHovered;

        /// <summary>
        /// Gets a value indicating whether the down button is hovered.
        /// </summary>
        internal bool DownButtonHovered => downButtonHovered;

        /// <summary>
        /// Gets a value indicating whether the up button is pressed.
        /// </summary>
        internal bool UpButtonPressed => upButtonPressed;

        /// <summary>
        /// Gets a value indicating whether the down button is pressed.
        /// </summary>
        internal bool DownButtonPressed => downButtonPressed;

        /// <summary>
        /// Paints the control using the registered renderer.
        /// </summary>
        /// <param name="e">The paint event data.</param>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Updates the embedded editor bounds when the control is resized.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected override void OnSizeChanged (EventArgs e)
        {
            base.OnSizeChanged (e);
            UpdateEditorBounds ();
        }

        /// <summary>
        /// Validates the current text when the control is deselected.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected override void OnDeselected (EventArgs e)
        {
            base.OnDeselected (e);
            ValidateEditText (true);
        }

        /// <summary>
        /// Clears hover and pressed states when the mouse leaves the control.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);

            upButtonHovered = false;
            downButtonHovered = false;
            upButtonPressed = false;
            downButtonPressed = false;

            Invalidate ();
        }

        /// <summary>
        /// Updates spinner hover state.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            var upHovered = UpButtonBounds.Contains (e.Location);
            var downHovered = DownButtonBounds.Contains (e.Location);

            if (upHovered != upButtonHovered || downHovered != downButtonHovered) {
                upButtonHovered = upHovered;
                downButtonHovered = downHovered;
                Invalidate ();
            }
        }

        /// <summary>
        /// Handles spinner button press state.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (!Enabled || e.Button != MouseButtons.Left)
                return;

            Select ();

            if (UpButtonBounds.Contains (e.Location)) {
                upButtonPressed = true;
                downButtonPressed = false;
                NotifyAccessibilityClients (AccessibleEvents.StateChange);
                Invalidate ();
            } else if (DownButtonBounds.Contains (e.Location)) {
                downButtonPressed = true;
                upButtonPressed = false;
                NotifyAccessibilityClients (AccessibleEvents.StateChange);
                Invalidate ();
            }
        }

        /// <summary>
        /// Applies increment or decrement on mouse release.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (!Enabled || e.Button != MouseButtons.Left)
                return;

            var doUp = upButtonPressed && UpButtonBounds.Contains (e.Location);
            var doDown = downButtonPressed && DownButtonBounds.Contains (e.Location);

            upButtonPressed = false;
            downButtonPressed = false;

            if (doUp || doDown)
                NotifyAccessibilityClients (AccessibleEvents.StateChange);

            Invalidate ();

            if (doUp)
                UpButton ();
            else if (doDown)
                DownButton ();
        }

        /// <summary>
        /// Handles mouse wheel input to increment or decrement the value.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (e.Handled || !Enabled)
                return;

            var previousValue = Value;
            if (e.Delta.Y > 0)
                UpButton ();
            else if (e.Delta.Y < 0)
                DownButton ();

            e.Handled = Value != previousValue;
        }

        /// <summary>
        /// Handles keyboard commands such as Up and Down at the control level.
        /// </summary>
        /// <param name="e">The key event data.</param>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown (e);

            if (!Enabled)
                return;

            switch (e.KeyCode) {
                case Keys.Up:
                    UpButton ();
                    e.Handled = true;
                    break;

                case Keys.Down:
                    DownButton ();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Increases the current value.
        /// </summary>
        public void UpButton ()
        {
            ValidateEditText (false);
            SetValue (currentValue + increment, true);
        }

        /// <summary>
        /// Decreases the current value.
        /// </summary>
        public void DownButton ()
        {
            ValidateEditText (false);
            SetValue (currentValue - increment, true);
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
        /// Raises the <see cref="ValueCommitted"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnValueCommitted (EventArgs e)
        {
            ValueCommitted?.Invoke (this, e);
        }

        private void Editor_TextChanged (object? sender, EventArgs e)
        {
            if (updatingEditorText || !allowManualEdit || readOnly)
                return;

            if (TryParseEditText (editor.Text, out var parsed)) {
                parsed = Clamp (RoundToConfiguredPrecision (parsed), minimum, maximum);

                if (parsed != currentValue) {
                    currentValue = parsed;
                    OnValueChanged (EventArgs.Empty);
                }
            }
        }

        private void Editor_KeyDown (object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode) {
                case Keys.Enter:
                    ValidateEditText (true);
                    e.Handled = true;
                    break;

                case Keys.Escape:
                    UpdateEditText ();
                    e.Handled = true;
                    break;

                case Keys.Up:
                    UpButton ();
                    e.Handled = true;
                    break;

                case Keys.Down:
                    DownButton ();
                    e.Handled = true;
                    break;
            }
        }

        private void Editor_KeyPress (object? sender, KeyPressEventArgs e)
        {
            if (!allowManualEdit || readOnly) {
                e.Handled = true;
                return;
            }

            if (char.IsControl (e.KeyChar))
                return;

            if (!IsAcceptedInputChar (editor.Text, e.KeyChar))
                e.Handled = true;
        }

        private void Editor_MouseWheel (object? sender, MouseEventArgs e)
        {
            if (!Enabled)
                return;

            if (e.Delta.Y > 0)
                UpButton ();
            else if (e.Delta.Y < 0)
                DownButton ();
        }

        private void ApplyEditorReadOnly ()
        {
            editor.ReadOnly = readOnly || !allowManualEdit;
        }

        private void SetValue (decimal value, bool raiseEvent)
        {
            var rounded = RoundToConfiguredPrecision (value);
            var clamped = Clamp (rounded, minimum, maximum);

            if (currentValue == clamped) {
                UpdateEditText ();
                return;
            }

            currentValue = clamped;
            UpdateEditText ();

            if (raiseEvent)
                OnValueChanged (EventArgs.Empty);

            Invalidate ();
        }

        private void ValidateEditText (bool raiseCommittedEvent)
        {
            if (TryParseEditText (editor.Text, out var parsed)) {
                SetValue (parsed, true);

                if (raiseCommittedEvent)
                    OnValueCommitted (EventArgs.Empty);
            } else {
                UpdateEditText ();
            }
        }

        private bool TryParseEditText (string text, out decimal value)
        {
            text = (text ?? string.Empty).Trim ();

            if (string.IsNullOrWhiteSpace (text)) {
                value = currentValue;
                return false;
            }

            if (!allowDecimalValues && text.Contains (CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)) {
                value = currentValue;
                return false;
            }

            return decimal.TryParse (
                text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out value);
        }

        private void UpdateEditText ()
        {
            updatingEditorText = true;
            editor.Text = FormatValue (currentValue);
            updatingEditorText = false;
        }

        private string FormatValue (decimal value)
        {
            var places = allowDecimalValues ? decimalPlaces : 0;
            var format = thousandsSeparator
                ? "N" + places.ToString (CultureInfo.InvariantCulture)
                : "F" + places.ToString (CultureInfo.InvariantCulture);

            return value.ToString (format, CultureInfo.CurrentCulture);
        }

        private bool IsAcceptedInputChar (string currentText, char ch)
        {
            var numberFormat = CultureInfo.CurrentCulture.NumberFormat;
            var decimalSeparator = numberFormat.NumberDecimalSeparator;
            var negativeSign = numberFormat.NegativeSign;

            if (char.IsDigit (ch))
                return true;

            if (allowDecimalValues &&
                decimalPlaces > 0 &&
                decimalSeparator.Length == 1 &&
                ch == decimalSeparator[0] &&
                !currentText.Contains (decimalSeparator)) {
                return true;
            }

            if (minimum < 0m &&
                negativeSign.Length == 1 &&
                ch == negativeSign[0] &&
                string.IsNullOrEmpty (currentText)) {
                return true;
            }

            return false;
        }

        private void UpdateIncrementFromSettings ()
        {
            if (!autoIncrement)
                return;

            increment = GetAutoIncrement ();
        }

        private decimal GetAutoIncrement ()
        {
            if (!allowDecimalValues || decimalPlaces <= 0)
                return 1m;

            decimal step = 1m;

            for (var i = 0; i < decimalPlaces; i++)
                step /= 10m;

            return step;
        }

        private decimal RoundToConfiguredPrecision (decimal value)
        {
            var places = allowDecimalValues ? decimalPlaces : 0;
            return decimal.Round (value, places, MidpointRounding.AwayFromZero);
        }

        private void UpdateEditorBounds ()
        {
            editor.Bounds = TextBounds;
        }

        private Rectangle GetTextBounds ()
        {
            var client = ClientRectangle;
            var buttonWidth = LogicalToDeviceUnits (18);

            return new Rectangle (
                client.Left,
                client.Top,
                Math.Max (0, client.Width - buttonWidth),
                client.Height);
        }

        private Rectangle GetUpButtonBounds ()
        {
            var client = ClientRectangle;
            var buttonWidth = LogicalToDeviceUnits (18);

            return new Rectangle (
                client.Right - buttonWidth,
                client.Top,
                buttonWidth,
                client.Height / 2);
        }

        private Rectangle GetDownButtonBounds ()
        {
            var client = ClientRectangle;
            var buttonWidth = LogicalToDeviceUnits (18);
            var topHeight = client.Height / 2;

            return new Rectangle (
                client.Right - buttonWidth,
                client.Top + topHeight,
                buttonWidth,
                client.Height - topHeight);
        }

        private static decimal Clamp (decimal value, decimal min, decimal max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        /// <summary>
        /// Represents the embedded text editor used by <see cref="NumericUpDown"/>.
        /// </summary>
        private sealed class NumericUpDownTextBox : TextBox
        {
            private readonly NumericUpDown owner;

            /// <summary>
            /// Initializes a new instance of the <see cref="NumericUpDownTextBox"/> class.
            /// </summary>
            /// <param name="owner">The owning numeric up-down control.</param>
            public NumericUpDownTextBox (NumericUpDown owner)
            {
                this.owner = owner;
            }

            /// <summary>
            /// Validates the current text when the editor loses selection.
            /// </summary>
            /// <param name="e">The event data.</param>
            protected override void OnDeselected (EventArgs e)
            {
                base.OnDeselected (e);
                owner.ValidateEditText (true);
            }
        }
    }
}
