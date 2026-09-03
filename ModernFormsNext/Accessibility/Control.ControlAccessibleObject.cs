using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
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
                if (Owner is not { } owner || owner.IsDisposed || !owner.Visible)
                    return Rectangle.Empty;

                // Transform all corners. Two diagonal corners are insufficient after rotation or
                // a negative presentation scale and can produce inverted or undersized bounds.
                Point topLeft = owner.PointToScreen(Point.Empty);
                Point topRight = owner.PointToScreen(new Point(owner.ScaledWidth, 0));
                Point bottomLeft = owner.PointToScreen(new Point(0, owner.ScaledHeight));
                Point bottomRight = owner.PointToScreen(new Point(owner.ScaledWidth, owner.ScaledHeight));
                int left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
                int top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
                int right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
                int bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
                return Rectangle.FromLTRB(left, top, right, bottom);
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
        public override string? AutomationId
        {
            get
            {
                if (Owner is not { } owner)
                    return base.AutomationId;

                return owner.AccessibleAutomationId ?? owner.Name;
            }
            set
            {
                if (Owner is { } owner)
                    owner.AccessibleAutomationId = value;
                else
                    base.AutomationId = value;
            }
        }

        /// <inheritdoc/>
        public override AccessibleControlType ControlType
        {
            get
            {
                var controlType = Owner?.AccessibleControlType ?? AccessibleControlType.Default;
                return controlType == AccessibleControlType.Default ? GetDefaultControlType(Owner) : controlType;
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
        public override AccessibleObject? Parent
        {
            get
            {
                if (Owner is not { IsDisposed: false } owner)
                    return null;

                return owner.Parent?.AccessibilityObject;
            }
        }

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
                if (Owner is not { } owner || owner.IsDisposed)
                    return AccessibleStates.Unavailable | AccessibleStates.Invisible | AccessibleStates.Offscreen;

                var state = AccessibleStates.None;

                if (!owner.Enabled)
                    state |= AccessibleStates.Unavailable;

                if (!owner.Visible)
                    state |= AccessibleStates.Invisible | AccessibleStates.Offscreen;

                if (owner.Focused)
                    state |= AccessibleStates.Focused;

                if (owner.CanSelect)
                    state |= AccessibleStates.Focusable;

                state |= GetDefaultState(owner);

                return state;
            }
        }

        /// <inheritdoc/>
        public override bool IsSensitive
            => Owner is TextBox { PasswordCharacter: not null };

        /// <inheritdoc/>
        public override AccessibleRangeValue? RangeValue
            => Owner switch
            {
                TrackBar trackBar => new AccessibleRangeValue(
                    trackBar.Value,
                    trackBar.Minimum,
                    trackBar.Maximum,
                    trackBar.SmallChange,
                    trackBar.LargeChange,
                    isReadOnly: false),
                ProgressBar progressBar => new AccessibleRangeValue(
                    progressBar.Value,
                    progressBar.Minimum,
                    progressBar.Maximum,
                    progressBar.Step,
                    progressBar.Step,
                    isReadOnly: true),
                _ => null
            };

        /// <inheritdoc/>
        public override AccessibleActions SupportedActions
        {
            get
            {
                if (Owner is not { IsDisposed: false } owner)
                    return AccessibleActions.None;

                var actions = owner.CanSelect ? AccessibleActions.Focus : AccessibleActions.None;

                actions |= owner switch
                {
                    Button => AccessibleActions.Invoke,
                    CheckBox { AutoCheck: true } => AccessibleActions.Toggle,
                    RadioButton { AutoCheck: true } => AccessibleActions.Select,
                    Switch => AccessibleActions.Toggle,
                    TextBox { ReadOnly: false } => AccessibleActions.SetValue,
                    ComboBox => AccessibleActions.Expand | AccessibleActions.Collapse,
                    TrackBar => AccessibleActions.SetValue | AccessibleActions.Increment | AccessibleActions.Decrement,
                    _ => AccessibleActions.None
                };

                return actions;
            }
        }

        /// <inheritdoc/>
        public override AccessibilityView View
        {
            get
            {
                if (Owner is not { IsDisposed: false } owner || !owner.Visible)
                    return AccessibilityView.Hidden;

                var view = owner.AccessibilityView;
                return view == AccessibilityView.Default ? GetDefaultAccessibilityView(owner) : view;
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
            if (index < 0)
                return null;

            return EnumerateActiveChildren().ElementAtOrDefault(index);
        }

        /// <inheritdoc/>
        public override int GetChildCount() => EnumerateActiveChildren().Count();

        /// <summary>
        /// Enumerates the control and logical objects that are candidates for this object's child
        /// sequence before visibility and view filtering is applied.
        /// </summary>
        /// <returns>
        /// An on-demand sequence of accessible children. The default implementation returns the
        /// represented control's explicit visual children.
        /// </returns>
        /// <remarks>
        /// Override this method to insert logical children for custom-rendered or composite
        /// controls. Logical children do not need to derive from <see cref="Control"/>. Return the
        /// same child object while its logical item remains alive so <see cref="AccessibleObject.RuntimeId"/>
        /// is stable. Do not cache a strong owner reference when a weak reference is sufficient.
        /// </remarks>
        protected virtual IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (Owner is not { IsDisposed: false } owner)
                yield break;

            foreach (Control child in owner.Controls)
                yield return child.AccessibilityObject;
        }

        /// <inheritdoc/>
        public override AccessibleObject? GetFocused()
        {
            if (Owner is not { } owner)
                return null;

            foreach (AccessibleObject child in EnumerateActiveChildren())
            {
                if ((child.State & AccessibleStates.Focused) != 0)
                    return child;

                if (child.GetFocused() is { } focused)
                    return focused;
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

            // Accessibility hit testing follows the same front-to-back contract as pointer
            // dispatch: the last child is the first eligible overlapping object.
            AccessibleObject[] children = EnumerateActiveChildren().ToArray();
            for (int i = children.Length - 1; i >= 0; i--)
            {
                var hit = children[i].HitTest(x, y);
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
        public override void DoDefaultAction()
        {
            AccessibleActions action = Owner switch
            {
                Button => AccessibleActions.Invoke,
                CheckBox => AccessibleActions.Toggle,
                RadioButton => AccessibleActions.Select,
                Switch => AccessibleActions.Toggle,
                ComboBox comboBox => comboBox.DroppedDown ? AccessibleActions.Collapse : AccessibleActions.Expand,
                _ => AccessibleActions.None
            };

            if (action != AccessibleActions.None)
                PerformAction(action);
        }

        /// <inheritdoc/>
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
        {
            if (!IsSingleAction(action)
                || (SupportedActions & action) == 0
                || Owner is not { IsDisposed: false } owner
                || !owner.Enabled
                || !owner.Visible)
            {
                return false;
            }

            if (action != AccessibleActions.SetValue && parameter is not null)
                return false;

            switch (action)
            {
                case AccessibleActions.Invoke when owner is Button button:
                    button.PerformClick();
                    return true;

                case AccessibleActions.Toggle when owner is CheckBox checkBox:
                    checkBox.OnClick(CreateAccessibilityClick());
                    return true;

                case AccessibleActions.Select when owner is RadioButton radioButton:
                    radioButton.OnClick(CreateAccessibilityClick());
                    return true;

                case AccessibleActions.Toggle when owner is Switch @switch:
                    @switch.Toggle();
                    return true;

                case AccessibleActions.Expand when owner is ComboBox comboBox:
                    comboBox.DroppedDown = true;
                    return true;

                case AccessibleActions.Collapse when owner is ComboBox comboBox:
                    comboBox.DroppedDown = false;
                    return true;

                case AccessibleActions.SetValue when owner is TextBox textBox:
                    if (parameter is not null and not string)
                        return false;

                    textBox.Text = (string?)parameter ?? string.Empty;
                    return true;

                case AccessibleActions.SetValue when owner is TrackBar trackBar:
                    if (!TryGetRangeActionValue(parameter, trackBar.Minimum, trackBar.Maximum, out int requestedValue))
                        return false;

                    trackBar.Value = requestedValue;
                    return true;

                case AccessibleActions.Increment when owner is TrackBar trackBar:
                    trackBar.Value = Math.Min(trackBar.Maximum, trackBar.Value + trackBar.SmallChange);
                    return true;

                case AccessibleActions.Decrement when owner is TrackBar trackBar:
                    trackBar.Value = Math.Max(trackBar.Minimum, trackBar.Value - trackBar.SmallChange);
                    return true;

                case AccessibleActions.Focus:
                    owner.Select();
                    return owner.Focused;

                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public override void Select(AccessibleSelection flags)
        {
            if ((flags & AccessibleSelection.TakeFocus) != 0)
                PerformAction(AccessibleActions.Focus);

            if ((flags & AccessibleSelection.TakeSelection) != 0
                && (SupportedActions & AccessibleActions.Select) != 0)
            {
                PerformAction(AccessibleActions.Select);
            }
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
            if (owner is ControlAdapter { ParentForm: Form form })
                return form.Text;

            // Editable text is a value, not a label. Falling back to Text here would leak user
            // content into the accessible name and would conflate two distinct semantic fields.
            if (owner is TextBox)
                return owner.Name;

            if (!string.IsNullOrEmpty(owner.Text))
                return owner.Text;

            return owner.Name;
        }

        private static AccessibleRole GetDefaultRole(Control? owner)
            => owner switch
            {
                ControlAdapter => AccessibleRole.Window,
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
                Shape => AccessibleRole.Graphic,
                ProgressBar => AccessibleRole.ProgressBar,
                RadioButton => AccessibleRole.RadioButton,
                ScrollBar => AccessibleRole.ScrollBar,
                StatusBar => AccessibleRole.StatusBar,
                Switch => AccessibleRole.CheckButton,
                TabControl => AccessibleRole.PageTabList,
                TextBox => AccessibleRole.Text,
                TrackBar => AccessibleRole.Slider,
                TreeView => AccessibleRole.Outline,
                Menu => AccessibleRole.MenuBar,
                MenuDropDown => AccessibleRole.MenuPopup,
                _ => AccessibleRole.Client
            };

        private static AccessibleControlType GetDefaultControlType(Control? owner)
            => owner switch
            {
                ControlAdapter => AccessibleControlType.Window,
                Button => AccessibleControlType.Button,
                CheckBox => AccessibleControlType.CheckBox,
                RadioButton => AccessibleControlType.RadioButton,
                Switch => AccessibleControlType.Switch,
                TextBox => AccessibleControlType.Edit,
                ComboBox => AccessibleControlType.ComboBox,
                ListBox or ListView => AccessibleControlType.List,
                TreeView => AccessibleControlType.Tree,
                TabControl => AccessibleControlType.Tab,
                TrackBar => AccessibleControlType.Slider,
                ProgressBar => AccessibleControlType.ProgressBar,
                ScrollBar => AccessibleControlType.ScrollBar,
                Menu or MenuDropDown => AccessibleControlType.Menu,
                ToolBar => AccessibleControlType.ToolBar,
                Label => AccessibleControlType.Text,
                PictureBox or Shape => AccessibleControlType.Image,
                GroupBox => AccessibleControlType.Group,
                Panel or FlowLayoutPanel or TableLayoutPanel => AccessibleControlType.Pane,
                _ => AccessibleControlType.Custom
            };

        private static AccessibilityView GetDefaultAccessibilityView(Control owner)
            => owner switch
            {
                Label or PictureBox or Shape or ProgressBar => AccessibilityView.Content,
                _ => AccessibilityView.Control
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
                    state |= AccessibleStates.Checked | AccessibleStates.Selected;
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
            else if (owner is ProgressBar)
            {
                state |= AccessibleStates.ReadOnly;
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

        private IEnumerable<AccessibleObject> EnumerateActiveChildren()
        {
            foreach (AccessibleObject child in GetAccessibilityChildren())
            {
                if (child is null
                    || child.View == AccessibilityView.Hidden
                    || (child.State & AccessibleStates.Invisible) != 0)
                {
                    continue;
                }

                yield return child;
            }
        }

        private static AccessibleObject? GetSibling(Control owner, int offset)
        {
            if (owner.Parent is not { } parent)
                return null;

            AccessibleObject parentObject = parent.AccessibilityObject;
            AccessibleObject ownerObject = owner.AccessibilityObject;
            int index = -1;

            for (int i = 0; i < parentObject.GetChildCount(); i++)
            {
                if (ReferenceEquals(parentObject.GetChild(i), ownerObject))
                {
                    index = i;
                    break;
                }
            }

            int siblingIndex = index + offset;

            if (index < 0 || siblingIndex < 0 || siblingIndex >= parentObject.GetChildCount())
                return null;

            return parentObject.GetChild(siblingIndex);
        }

        private static MouseEventArgs CreateAccessibilityClick()
            => new(MouseButtons.Left, 1, 0, 0, Point.Empty);

        private static bool IsSingleAction(AccessibleActions action)
        {
            int value = (int)action;
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static bool TryGetRangeActionValue(object? parameter, int minimum, int maximum, out int value)
        {
            value = default;

            try
            {
                double number = parameter switch
                {
                    byte typed => typed,
                    sbyte typed => typed,
                    short typed => typed,
                    ushort typed => typed,
                    int typed => typed,
                    uint typed => typed,
                    long typed => typed,
                    ulong typed => typed,
                    float typed => typed,
                    double typed => typed,
                    decimal typed => (double)typed,
                    _ => double.NaN
                };

                if (!double.IsFinite(number)
                    || number < minimum
                    || number > maximum
                    || number != Math.Truncate(number))
                {
                    return false;
                }

                value = checked((int)number);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }
}
