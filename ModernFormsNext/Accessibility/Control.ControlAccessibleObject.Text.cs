using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class Control
{
    public partial class ControlAccessibleObject
    {
        private AccessibleTextProvider? textProvider;

        /// <inheritdoc/>
        public override AccessibleTextProvider? TextProvider
        {
            get {
                if (Owner is not TextBox { IsDisposed: false, Disposing: false } editor || editor.IsAccessibilitySensitive
                    || TextBoxAccessibleTextProvider.HasSensitiveAncestor(this))
                    return null;
                return textProvider ??= editor.CreateAccessibleTextProvider(this);
            }
        }
    }
}
