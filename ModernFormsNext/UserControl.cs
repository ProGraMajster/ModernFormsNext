using System.Drawing;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a reusable container control whose contents can be composed in code or in the
    /// ModernFormsNext designer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="UserControl"/> is a normal, platform-neutral ModernFormsNext control. It is not
    /// a top-level window and does not depend on the Windows Forms designer infrastructure.
    /// </para>
    /// <para>
    /// The control can be used as the root of a <c>.mfdesign</c> document. When it is placed inside
    /// another designed component, the parent designer treats it as one atomic control; open the
    /// UserControl's own document to edit its internal children.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public partial class NavigationPanel : UserControl
    /// {
    ///     public NavigationPanel()
    ///     {
    ///         InitializeComponent();
    ///     }
    /// }
    /// </code>
    /// </example>
    public class UserControl : ScrollableControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserControl"/> class.
        /// </summary>
        public UserControl ()
        {
            TabStop = false;
            SetControlBehavior (ControlBehaviors.Selectable, false);
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (150, 150);

        /// <summary>
        /// Gets or sets the default style copied by newly created UserControl instances.
        /// </summary>
        /// <remarks>
        /// Each instance owns its <see cref="Style"/> copy. Replacing this field does not mutate
        /// controls that have already been constructed.
        /// </remarks>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
