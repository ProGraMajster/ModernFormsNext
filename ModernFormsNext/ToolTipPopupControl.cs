using System.Drawing;
using ModernFormsNext.Renderers;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext
{
    internal sealed class ToolTipPopupControl : Control
    {
        public ToolTipPopupControl()
        {
            SetControlBehavior(ControlBehaviors.Selectable, false);
            SetControlBehavior(ControlBehaviors.ReceivesMouseEvents, false);
            TabStop = false;
            AccessibleRole = AccessibleRole.ToolTip;
            Padding = new Padding(ToolTip.DefaultHorizontalPadding, ToolTip.DefaultVerticalPadding, ToolTip.DefaultHorizontalPadding, ToolTip.DefaultVerticalPadding);
        }

        public new static ControlStyle DefaultStyle = new ControlStyle(Control.DefaultStyle, style =>
        {
            style.BackgroundColor = ToolTip.DefaultBackColor;
            style.ForegroundColor = ToolTip.DefaultForeColor;
            style.Border.Width = ToolTip.DefaultBorderWidth;
            style.Border.Color = Theme.BorderHighColor;
            style.Border.Radius = ToolTip.DefaultBorderRadius;
        });

        public Control? AssociatedControl { get; private set; }

        public WindowBase? AssociatedWindow { get; private set; }

        public string TextToDisplay { get; private set; } = string.Empty;

        public string TitleToDisplay { get; private set; } = string.Empty;

        public ToolTipIcon Icon { get; private set; }

        public ToolTip? OwnerToolTip { get; private set; }

        internal bool PresentationVisible { get; private set; }

        internal void SetPresentationVisible(bool visible)
        {
            if (PresentationVisible == visible) return;
            PresentationVisible = visible;
            // Announce a useful completed tooltip, never every opacity/translation frame.
            NotifyAccessibilityClients(visible ? AccessibleEvents.Show : AccessibleEvents.Hide);
        }

        public override ControlStyle Style { get; } = new ControlStyle(DefaultStyle);

        public void Configure(
            ToolTip owner,
            WindowBase? associatedWindow,
            Control? associatedControl,
            string text,
            string title,
            ToolTipIcon icon,
            Size size)
        {
            OwnerToolTip = owner;
            AssociatedWindow = associatedWindow;
            AssociatedControl = associatedControl;
            TextToDisplay = text;
            TitleToDisplay = title;
            Icon = icon;
            Size = size;

            ApplyOwnerStyle(owner);

            Invalidate();
            AccessibleName = string.IsNullOrEmpty(title) ? text : title + ". " + text;
        }

        public void ApplyOwnerStyle(ToolTip owner)
        {
            Padding = owner.Padding;
            Style.BackgroundColor = owner.PresentationBackColor;
            Style.ForegroundColor = owner.PresentationForeColor;
            Style.Border.Width = owner.BorderWidth;
            Style.Border.Radius = owner.EffectiveBorderRadius;
            Style.Border.Color = owner.PresentationBorderColor;
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new ToolTipAccessibleObject(this);

        protected override void Dispose(bool disposing)
        {
            // A retained retired peer/control must not keep the component, owner form or
            // manual anchor alive. Finalization never touches managed presentation state.
            if (disposing)
            {
                OwnerToolTip = null;
                AssociatedControl = null;
                AssociatedWindow = null;
                TextToDisplay = TitleToDisplay = string.Empty;
            }
            base.Dispose(disposing);
        }

        private sealed class ToolTipAccessibleObject(ToolTipPopupControl owner) : ControlAccessibleObject(owner)
        {
            public override AccessibleControlType ControlType => AccessibleControlType.ToolTip;
            public override AccessibleStates State => Owner is ToolTipPopupControl { PresentationVisible: true }
                ? base.State : base.State | AccessibleStates.Invisible;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RenderManager.Render(this, e);
        }
    }
}
