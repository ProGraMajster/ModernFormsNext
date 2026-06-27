using System;
using System.Drawing;
using System.Globalization;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class Control
{
    /// <summary>
    /// Provides the default accessibility representation for a <see cref="Control"/>.
    /// </summary>
    /// <remarks>
    /// The object exposes ModernFormsNext control metadata in a platform-neutral form. It does
    /// not create native handles or COM providers; platform backends can adapt this object to
    /// their native accessibility systems.
    /// </remarks>
    public class ControlAccessibleObject : AccessibleObject
    {
        private readonly WeakReference<Control> _owner;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlAccessibleObject"/> class.
        /// </summary>
        /// <param name="owner">The control represented by this accessible object.</param>
        public ControlAccessibleObject(Control owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            _owner = new WeakReference<Control>(owner);
        }

        /// <inheritdoc/>
        public override Rectangle Bounds
        {
            get
            {
                if (Owner is not { } owner || !owner.Visible)
                    return Rectangle.Empty;

                return new Rectangle(owner.PointToScreen(Point.Empty), owner.ScaledSize);
            }
        }

        /// <inheritdoc/>
        public override string? DefaultAction
        {
            get
            {
                if (Owner is not { } owner)
                    return base.DefaultAction;

                return owner.AccessibleDefaultActionDescription ?? GetDefaultAction(owner) ?? base.DefaultAction;
            }
        }

        /// <inheritdoc/>
        public override string? Description => Owner?.AccessibleDescription ?? base.Description;

        /// <inheritdoc/>
        public override string? Help
        {
            get
            {
                if (Owner is not { } owner)
                    return base.Help;

                var args = new QueryAccessibilityHelpEventArgs();
                owner.OnQueryAccessibilityHelp(args);
                return args.HelpString ?? base.Help;
            }
        }

        /// <inheritdoc/>
        public override string? KeyboardShortcut => null;

        /// <inheritdoc/>
        public override string? Name
        {
            get
            {
                if (Owner is not { } owner)
                    return base.Name;

                return owner.AccessibleName ?? GetDefaultAccessibleName(owner);
            }
            set
            {
                if (Owner is { } owner)
                    owner.AccessibleName = value;
                else
                    base.Name = value;
            }
        }

        /// <summary>
        /// Gets the control represented by this accessible object.
        /// </summary>
        public Control? Owner => _owner.TryGetTarget(out Control? owner) ? owner : null;

        /// <inheritdoc/>
        public override AccessibleObject? Parent => Owner?.Parent?.AccessibilityObject;

        /// <inheritdoc/>
        public override AccessibleRole Role
        {
            get
            {
                var role = Owner?.AccessibleRole ?? AccessibleRole.Default;
                return role == AccessibleRole.Default ? GetDefaultRole(Owner) : role;
            }
        }

        /// <inheritdoc/>
        public override AccessibleStates State
        {
            get
            {
                if (Owner is not { } owner)
                    return AccessibleStates.Unavailable;

                var state = AccessibleStates.None;

                if (!owner.Enabled)
                    state |= AccessibleStates.Unavailable;

                if (!owner.Visible)
                    state |= AccessibleStates.Invisible;

                if (owner.Focused)
                    state |= AccessibleStates.Focused;

                if (owner.CanSelect)
                    state |= AccessibleStates.Focusable;

                state |= GetDefaultState(owner);

                return state;
            }
        }

        /// <inheritdoc/>
        public override string? Value
        {
            get
            {
                if (Owner is not { } owner)
                    return base.Value;

                return GetDefaultValue(owner) ?? owner.Text;
            }
            set
            {
                if (Owner is { } owner)
                    owner.Text = value ?? string.Empty;
                else
                    base.Value = value;
            }
        }

        /// <inheritdoc/>
        public override AccessibleObject? GetChild(int index)
        {
            if (Owner is not { } owner || index < 0 || index >= owner.Controls.Count)
                return null;

            return owner.Controls[index].AccessibilityObject;
        }

        /// <inheritdoc/>
        public override int GetChildCount() => Owner?.Controls.Count ?? 0;

        /// <inheritdoc/>
        public override AccessibleObject? GetFocused()
        {
            if (Owner is not { } owner)
                return null;

            foreach (var child in owner.Controls.GetAllControls(true))
            {
                if (child.Focused)
                    return child.AccessibilityObject;
            }

            return owner.Focused ? this : null;
        }

        /// <inheritdoc/>
        public override int GetHelpTopic(out string? fileName)
        {
            fileName = null;

            if (Owner is not { } owner)
                return 0;

            var args = new QueryAccessibilityHelpEventArgs();
            owner.OnQueryAccessibilityHelp(args);
            fileName = args.HelpNamespace;

            return int.TryParse(args.HelpKeyword, out int topic) ? topic : 0;
        }

        /// <inheritdoc/>
        public override AccessibleObject? HitTest(int x, int y)
        {
            if (Owner is not { } owner || !Bounds.Contains(x, y))
                return null;

            for (int i = owner.Controls.Count - 1; i >= 0; i--)
            {
                var child = owner.Controls[i];
                var hit = child.AccessibilityObject.HitTest(x, y);
                if (hit is not null)
                    return hit;
            }

            return this;
        }

        /// <inheritdoc/>
        public override AccessibleObject? Navigate(AccessibleNavigation navdir)
        {
            if (Owner is not { } owner)
                return null;

            return navdir switch
            {
                AccessibleNavigation.FirstChild => GetChildCount() > 0 ? GetChild(0) : null,
                AccessibleNavigation.LastChild => GetChildCount() > 0 ? GetChild(GetChildCount() - 1) : null,
                AccessibleNavigation.Next => GetSibling(owner, 1),
                AccessibleNavigation.Previous => GetSibling(owner, -1),
                _ => base.Navigate(navdir)
            };
        }

        /// <inheritdoc/>
        public override string ToString() => $"{nameof(ControlAccessibleObject)}: Owner = {Owner}";

        private static string? GetDefaultAction(Control owner)
            => owner switch
            {
                Button => "Press",
                CheckBox checkBox => checkBox.Checked ? "Uncheck" : "Check",
                ComboBox comboBox => comboBox.DroppedDown ? "Close" : "Open",
                DateTimePicker dateTimePicker when !dateTimePicker.ShowUpDown => dateTimePicker.IsDropDownOpen ? "Close" : "Open",
                LinkLabel => "Open",
                RadioButton => "Select",
                Switch @switch => @switch.IsToggled ? "Turn off" : "Turn on",
                _ => null
            };

        private static string? GetDefaultAccessibleName(Control owner)
        {
            if (!string.IsNullOrEmpty(owner.Text))
                return owner.Text;

            return owner.Name;
        }

        private static AccessibleRole GetDefaultRole(Control? owner)
            => owner switch
            {
                Button => AccessibleRole.PushButton,
                CheckBox => AccessibleRole.CheckButton,
                ComboBox => AccessibleRole.ComboBox,
                DataGridView => AccessibleRole.Table,
                DateTimePicker => AccessibleRole.ComboBox,
                FlowLayoutPanel => AccessibleRole.Pane,
                LinkLabel => AccessibleRole.Link,
                Label => AccessibleRole.StaticText,
                ListBox => AccessibleRole.List,
                ListView => AccessibleRole.List,
                NumericUpDown => AccessibleRole.SpinButton,
                TableLayoutPanel => AccessibleRole.Pane,
                Panel => AccessibleRole.Pane,
                PictureBox => AccessibleRole.Graphic,
                ProgressBar => AccessibleRole.ProgressBar,
                RadioButton => AccessibleRole.RadioButton,
                ScrollBar => AccessibleRole.ScrollBar,
                StatusBar => AccessibleRole.StatusBar,
                Switch => AccessibleRole.CheckButton,
                TabControl => AccessibleRole.PageTabList,
                TextBox => AccessibleRole.Text,
                TrackBar => AccessibleRole.Slider,
                _ => AccessibleRole.Client
            };

        private static AccessibleStates GetDefaultState(Control owner)
        {
            var state = AccessibleStates.None;

            if (owner is CheckBox checkBox)
            {
                state |= AccessibleStates.Selectable;

                state |= checkBox.CheckState switch
                {
                    CheckState.Checked => AccessibleStates.Checked,
                    CheckState.Indeterminate => AccessibleStates.Mixed,
                    _ => AccessibleStates.None
                };
            }
            else if (owner is RadioButton radioButton)
            {
                state |= AccessibleStates.Selectable;

                if (radioButton.Checked)
                    state |= AccessibleStates.Checked;
            }
            else if (owner is Switch @switch)
            {
                state |= AccessibleStates.Selectable;

                if (@switch.ThumbPressed)
                    state |= AccessibleStates.Pressed;

                if (@switch.Value > 0)
                    state |= AccessibleStates.Checked;
                else if (@switch.Mode == SwitchMode.ThreeState && @switch.Value == 0)
                    state |= AccessibleStates.Mixed;
            }
            else if (owner is ComboBox comboBox)
            {
                state |= AccessibleStates.HasPopup;
                state |= comboBox.DroppedDown ? AccessibleStates.Expanded : AccessibleStates.Collapsed;
            }
            else if (owner is DateTimePicker dateTimePicker)
            {
                if (!dateTimePicker.ShowUpDown)
                {
                    state |= AccessibleStates.HasPopup;
                    state |= dateTimePicker.IsDropDownOpen ? AccessibleStates.Expanded : AccessibleStates.Collapsed;
                }

                if (!dateTimePicker.Checked)
                    state |= AccessibleStates.Unavailable;
            }
            else if (owner is DataGridView dataGridView)
            {
                state |= AccessibleStates.Selectable;

                if (dataGridView.ReadOnly)
                    state |= AccessibleStates.ReadOnly;
            }
            else if (owner is LinkLabel linkLabel)
            {
                state |= AccessibleStates.Linked;

                if (linkLabel.LinkVisited)
                    state |= AccessibleStates.Traversed;
            }
            else if (owner is ListBox listBox)
            {
                state |= AccessibleStates.Selectable;

                if (listBox.SelectionMode is SelectionMode.MultiSimple or SelectionMode.MultiExtended)
                    state |= AccessibleStates.MultiSelectable;
            }
            else if (owner is NumericUpDown numericUpDown)
            {
                if (numericUpDown.ReadOnly)
                    state |= AccessibleStates.ReadOnly;

                if (numericUpDown.UpButtonPressed || numericUpDown.DownButtonPressed)
                    state |= AccessibleStates.Pressed;
            }
            else if (owner is TextBox textBox)
            {
                if (textBox.ReadOnly)
                    state |= AccessibleStates.ReadOnly;

                if (textBox.PasswordCharacter.HasValue)
                    state |= AccessibleStates.Protected;
            }
            else if (owner is TrackBar trackBar)
            {
                if (trackBar.ThumbPressed)
                    state |= AccessibleStates.Pressed;
            }

            return state;
        }

        private static string? GetDefaultValue(Control owner)
            => owner switch
            {
                ComboBox comboBox => comboBox.SelectedItem?.ToString(),
                DateTimePicker dateTimePicker => dateTimePicker.DisplayText,
                ListBox listBox => listBox.SelectedItem?.ToString(),
                NumericUpDown numericUpDown => numericUpDown.Text,
                ProgressBar progressBar => progressBar.Value.ToString(CultureInfo.CurrentCulture),
                TabControl tabControl => tabControl.SelectedTabPage?.Text,
                TextBox textBox => textBox.PasswordCharacter.HasValue ? string.Empty : textBox.Text,
                TrackBar trackBar => trackBar.Value.ToString(CultureInfo.CurrentCulture),
                Switch @switch => @switch.Value.ToString(CultureInfo.CurrentCulture),
                _ => null
            };

        private static AccessibleObject? GetSibling(Control owner, int offset)
        {
            if (owner.Parent is not { } parent)
                return null;

            int index = parent.Controls.IndexOf(owner);
            int siblingIndex = index + offset;

            if (index < 0 || siblingIndex < 0 || siblingIndex >= parent.Controls.Count)
                return null;

            return parent.Controls[siblingIndex].AccessibilityObject;
        }
    }
}
