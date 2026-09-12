using System.Drawing;
using System.Runtime.CompilerServices;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Renderers;

namespace ModernFormsNext;

public partial class Control
{
    public partial class ControlAccessibleObject
    {
        private readonly ConditionalWeakTable<LinkLabel.Link, LinkAccessibleObject> linkObjects = new();
        private NumericButtonAccessibleObject? numericUpObject;
        private NumericButtonAccessibleObject? numericDownObject;

        /// <inheritdoc/>
        public override Orientation? Orientation
            => Owner is ScrollBar bar ? bar.AccessibilityOrientation : null;

        private IEnumerable<AccessibleObject> GetLinkObjects(LinkLabel owner)
        {
            foreach (var link in owner.Links)
                if (owner.IsAccessibilityLinkAttached(link))
                    yield return linkObjects.GetValue(link, item => new LinkAccessibleObject(this, owner, item));
        }

        private IEnumerable<AccessibleObject> GetNumericChildren(NumericUpDown owner)
        {
            yield return owner.AccessibilityEditor.AccessibilityObject;
            yield return numericUpObject ??= new NumericButtonAccessibleObject(this, owner, true);
            yield return numericDownObject ??= new NumericButtonAccessibleObject(this, owner, false);
        }

        private static bool TryGetDecimalRangeActionValue(object? parameter, decimal minimum, decimal maximum, out decimal value)
        {
            value = default;
            decimal? exact = parameter switch
            {
                byte x => x, sbyte x => x, short x => x, ushort x => x, int x => x,
                uint x => x, long x => x, ulong x => x, decimal x => x, _ => null
            };
            if (exact is { } precise)
            {
                value = precise;
                return value >= minimum && value <= maximum;
            }
            double number = parameter switch { float x => x, double x => x, _ => double.NaN };
            if (!double.IsFinite(number) || number < (double)minimum || number > (double)maximum)
                return false;
            // A UIA double cannot represent every decimal. In particular the rounded decimal
            // extrema cannot be cast back; preserve advertised endpoints without overflowing.
            if (number == (double)minimum) value = minimum;
            else if (number == (double)maximum) value = maximum;
            else
            {
                try { value = checked((decimal)number); }
                catch (OverflowException) { return false; }
            }
            return value >= minimum && value <= maximum;
        }

        private sealed class LinkAccessibleObject : LogicalItemAccessibleObject
        {
            private readonly WeakReference<LinkLabel.Link> linkReference;
            internal LinkAccessibleObject(ControlAccessibleObject root, LinkLabel owner, LinkLabel.Link link)
                : base(root, owner) => linkReference = new(link);

            private LinkLabel? Label => OwnerControl as LinkLabel;
            private LinkLabel.Link? Link => linkReference.TryGetTarget(out var link) ? link : null;
            private bool Attached => Label is { } owner && Link is { } link && owner.IsAccessibilityLinkAttached(link);
            public override AccessibleObject? Parent => Attached ? Root : null;
            public override AccessibleControlType ControlType => AccessibleControlType.Hyperlink;
            public override AccessibleRole Role => AccessibleRole.Link;
            public override string? AutomationId { get => Attached ? Link!.Name : null; set { } }
            public override string? Name { get => Attached ? Label!.GetAccessibilityLinkText(Link!) : null; set { } }
            public override string? Value { get => null; set { } }
            public override string? DefaultAction => "Open";
            public override AccessibilityView View => Attached ? base.View : AccessibilityView.Hidden;
            public override Rectangle Bounds
            {
                get
                {
                    if (!Attached || Label is not { Visible: true } owner) return Rectangle.Empty;
                    owner.EnsureAccessibilityLinkLayout();
                    var result = Rectangle.Empty;
                    foreach (var fragment in Link!.VisualBounds)
                    {
                        var clipped = Rectangle.Intersect(fragment, owner.PaddedClientRectangle);
                        var screen = ToScreenBounds(owner, clipped);
                        if (!screen.IsEmpty) result = result.IsEmpty ? screen : Rectangle.Union(result, screen);
                    }
                    return result;
                }
            }
            public override AccessibleStates State
            {
                get
                {
                    var state = GetCommonState(Attached, Bounds.IsEmpty) | AccessibleStates.Linked;
                    if (!Attached) return state;
                    var link = Link!;
                    if (!link.Enabled) state |= AccessibleStates.Unavailable;
                    else if (Label!.CanSelect) state |= AccessibleStates.Focusable;
                    if (link.Visited) state |= AccessibleStates.Traversed;
                    if (Label!.Focused && ReferenceEquals(Label.FocusLink, link)) state |= AccessibleStates.Focused;
                    return state;
                }
            }
            public override AccessibleActions SupportedActions
                => Attached && IsOwnerAvailable && Link!.Enabled && !Bounds.IsEmpty
                    ? AccessibleActions.Invoke | (Label!.CanSelect ? AccessibleActions.Focus : 0)
                    : AccessibleActions.None;
            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (parameter is not null || (SupportedActions & action) == 0 || !IsSingleAction(action)) return false;
                return action switch
                {
                    AccessibleActions.Invoke => Label!.ActivateAccessibilityLink(Link!),
                    AccessibleActions.Focus => Label!.FocusAccessibilityLink(Link!),
                    _ => false
                };
            }
            public override void DoDefaultAction() => PerformAction(AccessibleActions.Invoke);
            public override void Select(AccessibleSelection flags)
            {
                if ((flags & AccessibleSelection.TakeFocus) != 0) PerformAction(AccessibleActions.Focus);
            }
            public override AccessibleObject? GetFocused() => (State & AccessibleStates.Focused) != 0 ? this : null;
            public override AccessibleObject? HitTest(int x, int y)
            {
                if (!Attached || Label is not { Visible: true } owner) return null;
                owner.EnsureAccessibilityLinkLayout();
                // Union bounds contain gaps on multiline links; hit-test the actual renderer's
                // individual fragments in local coordinates, including presentation transforms.
                foreach (var fragment in Link!.VisualBounds)
                {
                    var clipped = Rectangle.Intersect(fragment, owner.PaddedClientRectangle);
                    if (ToScreenBounds(owner, clipped).Contains(x, y)) return this;
                }
                return null;
            }
        }

        private sealed class NumericButtonAccessibleObject : LogicalItemAccessibleObject
        {
            private readonly bool up;
            internal NumericButtonAccessibleObject(ControlAccessibleObject root, NumericUpDown owner, bool up)
                : base(root, owner) => this.up = up;
            private NumericUpDown? Numeric => OwnerControl as NumericUpDown;
            public override AccessibleObject? Parent => Numeric is not null ? Root : null;
            public override AccessibleControlType ControlType => AccessibleControlType.Button;
            public override AccessibleRole Role => AccessibleRole.PushButton;
            public override string? Name { get => up ? "Increase" : "Decrease"; set { } }
            public override string? Value { get => null; set { } }
            public override string? DefaultAction => "Press";
            public override Rectangle Bounds => Numeric is { Visible: true } owner
                ? ToScreenBounds(owner, up ? owner.UpButtonBounds : owner.DownButtonBounds) : Rectangle.Empty;
            public override AccessibleStates State => (GetCommonState(Numeric is not null, Bounds.IsEmpty) & ~AccessibleStates.Selectable)
                | (Numeric is { } owner && (up ? owner.UpButtonPressed : owner.DownButtonPressed)
                    ? AccessibleStates.Pressed : AccessibleStates.None);
            public override AccessibleActions SupportedActions => Numeric is not null && IsOwnerAvailable
                ? AccessibleActions.Invoke : AccessibleActions.None;
            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (action != AccessibleActions.Invoke || parameter is not null || SupportedActions == AccessibleActions.None) return false;
                if (up) Numeric!.UpButton(); else Numeric!.DownButton();
                return true;
            }
            public override void DoDefaultAction() => PerformAction(AccessibleActions.Invoke);
        }
    }
}
