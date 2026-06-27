using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class GroupBox
{
    internal sealed class GroupBoxAccessibleObject : ControlAccessibleObject
    {
        internal GroupBoxAccessibleObject(GroupBox owner)
            : base(owner)
        {
        }

        public override AccessibleRole Role
        {
            get
            {
                if (Owner is not GroupBox owner)
                    return AccessibleRole.Grouping;

                return owner.AccessibleRole == AccessibleRole.Default
                    ? AccessibleRole.Grouping
                    : owner.AccessibleRole;
            }
        }
    }
}
