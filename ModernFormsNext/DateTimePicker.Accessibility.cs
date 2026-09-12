using System.Drawing;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class DateTimePicker
{
    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new DatePickerAccessibleObject(this);

    private sealed class DatePickerAccessibleObject(DateTimePicker owner) : ControlAccessibleObject(owner)
    {
        private readonly DatePartAccessibleObject?[] parts = new DatePartAccessibleObject?[4];
        internal DateTimePicker? Picker => Owner is DateTimePicker { IsDisposed: false } picker ? picker : null;
        private bool Available => Picker is { CanUseDatePicker: true } picker && picker.AccessibilityView != AccessibilityView.Hidden;
        public override AccessibleControlType ControlType => Picker is { } picker
            && picker.AccessibleControlType != AccessibleControlType.Default ? picker.AccessibleControlType
            : Picker?.ShowUpDown == true ? AccessibleControlType.Spinner : AccessibleControlType.ComboBox;
        public override AccessibleStates State
        {
            get
            {
                var state = base.State;
                if (Picker is { Enabled: true }) state &= ~AccessibleStates.Unavailable;
                if (Picker is { ShowCheckBox: true, Checked: true }) state |= AccessibleStates.Checked;
                if (Picker is not { CanOpenDropDown: true } && Picker?.IsDropDownOpen != true)
                    state &= ~(AccessibleStates.HasPopup | AccessibleStates.Collapsed | AccessibleStates.Expanded);
                return state;
            }
        }
        public override string? Value
        {
            get => Picker?.DisplayText;
            set => PerformAction(AccessibleActions.SetValue, value ?? string.Empty);
        }
        public override AccessibleActions SupportedActions
        {
            get
            {
                if (!Available) return AccessibleActions.None;
                var picker = Picker!;
                var actions = AccessibleActions.SetValue | (picker.CanSelect ? AccessibleActions.Focus : 0);
                if (picker.ShowCheckBox) actions |= AccessibleActions.Toggle;
                if (picker.Checked)
                {
                    if (picker.Value < picker.MaxDate) actions |= AccessibleActions.Increment;
                    if (picker.Value > picker.MinDate) actions |= AccessibleActions.Decrement;
                    if (picker.IsDropDownOpen) actions |= AccessibleActions.Collapse;
                    else if (picker.CanOpenDropDown) actions |= AccessibleActions.Expand;
                }
                return actions;
            }
        }
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
        {
            if (action == 0 || (((int)action & ((int)action - 1)) != 0) || (SupportedActions & action) == 0) return false;
            var picker = Picker!;
            if (action == AccessibleActions.SetValue)
            {
                if (parameter is not string text) return false;
                // Reuse the control's culture-aware parser, reset and range validation.
                picker.Text = text;
                return true;
            }
            if (parameter is not null) return false;
            switch (action)
            {
                case AccessibleActions.Toggle: picker.Checked = !picker.Checked; return true;
                case AccessibleActions.Increment: picker.StepValue(1); return true;
                case AccessibleActions.Decrement: picker.StepValue(-1); return true;
                case AccessibleActions.Expand: return picker.OpenDropDown();
                case AccessibleActions.Collapse: picker.CloseDropDown(); return true;
                case AccessibleActions.Focus: return base.PerformAction(action);
                default: return false;
            }
        }
        private int PartCount => Picker is { } picker ? (picker.ShowCheckBox ? 1 : 0) + (picker.ShowUpDown ? 2 : 1) : 0;
        private DatePartAccessibleObject Part(int kind) => parts[kind] ??= new(this, kind);
        public override int GetChildCount() => View == AccessibilityView.Hidden ? 0 : PartCount + base.GetChildCount();
        public override AccessibleObject? GetChild(int index)
        {
            if (index < 0 || index >= GetChildCount() || Picker is not { } picker) return null;
            if (index >= PartCount) return base.GetChild(index - PartCount);
            if (picker.ShowCheckBox && index == 0) return Part(0);
            if (picker.ShowCheckBox) --index;
            return Part(picker.ShowUpDown ? index + 2 : 1);
        }
        public override AccessibleObject? HitTest(int x, int y)
        {
            for (int i = GetChildCount() - 1; i >= 0; --i)
                if (GetChild(i)?.HitTest(x, y) is { } hit) return hit;
            return base.HitTest(x, y);
        }
    }

    private sealed class DatePartAccessibleObject(DatePickerAccessibleObject root, int kind) : AccessibleObject
    {
        private DateTimePicker? Picker => root.Picker;
        private bool Attached => Picker is { } picker && (kind == 0 ? picker.ShowCheckBox : kind == 1 ? !picker.ShowUpDown : picker.ShowUpDown);
        public override AccessibleObject? Parent => Attached ? root : null;
        public override AccessibilityView View => Attached ? root.View : AccessibilityView.Hidden;
        public override AccessibleControlType ControlType => kind == 0 ? AccessibleControlType.CheckBox : AccessibleControlType.Button;
        public override AccessibleRole Role => kind == 0 ? AccessibleRole.CheckButton : AccessibleRole.PushButton;
        public override string? Name { get => kind switch { 0 => "Enable date and time", 1 => "Calendar", 2 => "Increase", _ => "Decrease" }; set { } }
        public override bool IsSensitive => root.IsSensitive;
        public override AccessibleStates State => (Attached ? root.State : AccessibleStates.Unavailable | AccessibleStates.Invisible)
            & ~(AccessibleStates.Focused | AccessibleStates.Focusable | AccessibleStates.HasPopup | AccessibleStates.Expanded | AccessibleStates.Collapsed
                | AccessibleStates.Checked | AccessibleStates.Mixed)
            | (kind == 0 && Picker?.Checked == true ? AccessibleStates.Checked : 0)
            | (SupportedActions == 0 ? AccessibleStates.Unavailable : 0);
        public override Rectangle Bounds
        {
            get
            {
                if (!Attached || Picker is not { Visible: true } picker) return Rectangle.Empty;
                var rect = kind == 0 ? picker.CheckBoxRectangle : picker.ButtonRectangle;
                if (kind >= 2)
                {
                    int middle = rect.Top + rect.Height / 2;
                    rect = kind == 2 ? Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, middle)
                        : Rectangle.FromLTRB(rect.Left, middle, rect.Right, rect.Bottom);
                }
                return CalendarAccessibleGeometry.ToScreen(picker, rect);
            }
        }
        private AccessibleActions OwnerAction => kind switch
        {
            0 => AccessibleActions.Toggle, 1 => Picker?.IsDropDownOpen == true ? AccessibleActions.Collapse : AccessibleActions.Expand,
            2 => AccessibleActions.Increment, _ => AccessibleActions.Decrement
        };
        public override AccessibleActions SupportedActions => Attached && (root.SupportedActions & OwnerAction) != 0
            ? kind == 0 ? AccessibleActions.Toggle : AccessibleActions.Invoke : AccessibleActions.None;
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
            => action != 0 && action == SupportedActions && parameter is null && root.PerformAction(OwnerAction);
        public override void DoDefaultAction() => PerformAction(SupportedActions);
        public override AccessibleObject? Navigate(AccessibleNavigation direction) => CalendarAccessibleGeometry.Navigate(this, direction);
    }
}

// Calendar and picker peers use the same device-space rectangles as paint and pointer input.
internal static class CalendarAccessibleGeometry
{
    internal static Rectangle ToScreen(Control owner, Rectangle bounds)
    {
        bounds = Rectangle.Intersect(bounds, owner.ClientRectangle);
        if (bounds.Width <= 0 || bounds.Height <= 0) return Rectangle.Empty;
        var a = owner.PointToScreen(bounds.Location);
        var b = owner.PointToScreen(new(bounds.Right, bounds.Top));
        var c = owner.PointToScreen(new(bounds.Left, bounds.Bottom));
        var d = owner.PointToScreen(new(bounds.Right, bounds.Bottom));
        return Rectangle.FromLTRB(Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)), Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)),
            Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)), Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)));
    }

    internal static AccessibleObject? Navigate(AccessibleObject node, AccessibleNavigation direction)
    {
        if (direction is AccessibleNavigation.FirstChild) return node.GetChild(0);
        if (direction is AccessibleNavigation.LastChild) return node.GetChild(node.GetChildCount() - 1);
        if (direction is not (AccessibleNavigation.Next or AccessibleNavigation.Previous) || node.Parent is not { } parent) return null;
        for (int i = 0; i < parent.GetChildCount(); ++i)
            if (ReferenceEquals(node, parent.GetChild(i))) return parent.GetChild(i + (direction == AccessibleNavigation.Next ? 1 : -1));
        return null;
    }
}
