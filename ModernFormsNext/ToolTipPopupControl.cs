using System.Drawing;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    internal sealed class ToolTipPopupControl : Control
    {
        public ToolTipPopupControl()
        {
            SetControlBehavior(ControlBehaviors.Selectable, false);
            SetControlBehavior(ControlBehaviors.ReceivesMouseEvents, false);
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
        }

        public void ApplyOwnerStyle(ToolTip owner)
        {
            Padding = owner.Padding;
            Style.BackgroundColor = owner.BackColor;
            Style.ForegroundColor = owner.ForeColor;
            Style.Border.Width = owner.BorderWidth;
            Style.Border.Radius = owner.EffectiveBorderRadius;
            Style.Border.Color = owner.BorderColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RenderManager.Render(this, e);
        }
    }
}
