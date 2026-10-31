using ModernFormsNext.Accessibility;

namespace ModernFormsNext.DataBinding;

public partial class BindingNavigator
{
    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new NavigatorAccessibleObject(this);

    private sealed class NavigatorAccessibleObject(BindingNavigator owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleControlType ControlType => AccessibleControlType.ToolBar;

        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (Owner is BindingNavigator { IsDisposed: false } current && !current.retiring
                && current.PositionTextBox is not null)
            {
                // These are the existing implicit controls, with their existing parent/identity.
                // Use the same canonical composite hook as SearchBar; never create proxy peers
                // or expose scrollbar implementation chrome as authored navigation actions.
                yield return current.MoveFirstButton.AccessibilityObject;
                yield return current.MovePreviousButton.AccessibilityObject;
                yield return current.PositionTextBox.AccessibilityObject;
                yield return current.CountLabel.AccessibilityObject;
                yield return current.MoveNextButton.AccessibilityObject;
                yield return current.MoveLastButton.AccessibilityObject;
                yield return current.AddNewButton.AccessibilityObject;
                yield return current.DeleteButton.AccessibilityObject;
                yield return current.SaveButton.AccessibilityObject;
                yield return current.CancelButton.AccessibilityObject;
            }
            foreach (var child in base.GetAccessibilityChildren()) yield return child;
        }
    }
}
