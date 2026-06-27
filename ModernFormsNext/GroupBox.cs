using System;
using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Layout;
using ModernFormsNext.Renderers;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a container control that draws a labeled frame around related child controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GroupBox"/> is a code-first, SkiaSharp-rendered counterpart to the familiar
    /// WinForms group box. It is intended for visually and semantically grouping related options,
    /// such as a set of <see cref="RadioButton"/> or <see cref="CheckBox"/> controls.
    /// </para>
    /// <para>
    /// The control is not focusable and does not receive tab focus itself. Child controls remain
    /// reachable through normal tab navigation. The <see cref="DisplayRectangle"/> starts below the
    /// caption, so docked and anchored children are laid out inside the framed content area.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var groupBox = new GroupBox
    /// {
    ///     Text = "Connection",
    ///     Width = 220,
    ///     Height = 120
    /// };
    ///
    /// groupBox.Controls.Add(new RadioButton
    /// {
    ///     Text = "Use saved profile",
    ///     Left = 12,
    ///     Top = 30,
    ///     Checked = true
    /// });
    ///
    /// form.Controls.Add(groupBox);
    /// </code>
    /// </example>
    [DefaultProperty(nameof(Text))]
    public partial class GroupBox : Control
    {
        private const int CaptionMeasurementPadding = 16;

        private int? captionFontSize;
        private SKColor? captionForegroundColor;
        private SKColor? captionBackgroundColor;
        private ModernFormsNext.Drawing.Brush? captionBackgroundBrush;
        private SKColor? captionBorderColor;
        private int captionBorderRadius;
        private int captionBorderWidth;
        private ModernFormsNext.Drawing.Brush? contentBackgroundBrush;
        private SKColor? contentBackgroundColor;
        private int cachedCaptionHeight = -1;
        private int cachedFontSize;
        private FontStyle cachedFontStyle;
        private SKTypeface? cachedTypeface;
        private bool showShadow;
        private int shadowBlur = 4;
        private SKColor shadowColor = new SKColor(0, 0, 0, 80);
        private Point shadowOffset = new Point(2, 2);

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupBox"/> class.
        /// </summary>
        public GroupBox()
        {
            SetExtendedState(ExtendedStates.UserPreferredSizeCache, true);
            SetControlBehavior(ControlBehaviors.Selectable, false);

            TabStop = false;
        }

        /// <summary>
        /// Allows the control to optionally shrink when <see cref="Control.AutoSize"/> is <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// Use <see cref="ModernFormsNext.AutoSizeMode.GrowOnly"/> to preserve the current size as a
        /// lower bound, or <see cref="ModernFormsNext.AutoSizeMode.GrowAndShrink"/> when the control
        /// should shrink to the preferred size of its caption and child controls. Changing this value
        /// requests layout on the parent when the group box participates in layout.
        /// </remarks>
        public AutoSizeMode AutoSizeMode
        {
            get => GetAutoSizeMode();
            set
            {
                SourceGenerated.EnumValidator.Validate(value);

                if (GetAutoSizeMode() == value)
                    return;

                SetAutoSizeMode(value);

                if (Parent is not null)
                {
                    // DefaultLayout caches anchor distances lazily. Reinitialize when AutoSizeMode changes
                    // so parent layout can resize the group without using stale size constraints.
                    if (Parent.LayoutEngine == DefaultLayout.Instance)
                        Parent.LayoutEngine.InitLayout(this, BoundsSpecified.Size);

                    LayoutTransaction.DoLayout(Parent, this, PropertyNames.AutoSize);
                }
            }
        }

        /// <summary>
        /// Gets or sets the background color painted behind the caption text.
        /// </summary>
        /// <value>
        /// A color used for the caption label area, or <see langword="null"/> to use the
        /// group box background color.
        /// </value>
        /// <remarks>
        /// Changing this property invalidates the control. It does not affect child layout. When
        /// <see cref="CaptionBackgroundBrush"/> is set, that brush is used and this color becomes
        /// the fallback color for unsupported or empty brush content.
        /// Use <see cref="ContentBackgroundColor"/> or <see cref="ContentBackgroundBrush"/> to
        /// change the framed content background independently from the caption.
        /// </remarks>
        /// <example>
        /// <code>
        /// groupBox.CaptionBackgroundColor = new SKColor(255, 255, 255);
        /// groupBox.ContentBackgroundColor = new SKColor(245, 248, 252);
        /// </code>
        /// </example>
        public SKColor? CaptionBackgroundColor
        {
            get => captionBackgroundColor;
            set
            {
                if (captionBackgroundColor == value)
                    return;

                captionBackgroundColor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the brush used to paint the caption label background.
        /// </summary>
        /// <value>
        /// A solid, linear gradient, radial gradient, sweep gradient, or glass brush used behind
        /// the caption text, or <see langword="null"/> to use <see cref="CaptionBackgroundColor"/>.
        /// </value>
        /// <remarks>
        /// The brush is clipped to the caption label area and can be combined with
        /// <see cref="CaptionBorderWidth"/> and <see cref="CaptionBorderRadius"/>. Changing this
        /// property invalidates the control and does not affect child layout.
        /// </remarks>
        /// <example>
        /// <code>
        /// var brush = new ModernFormsNext.Drawing.LinearGradientBrush
        /// {
        ///     StartPoint = new SKPoint(0, 0),
        ///     EndPoint = new SKPoint(1, 0)
        /// };
        ///
        /// brush.GradientStops.Add(new ModernFormsNext.Drawing.GradientStop(Theme.AccentColor, 0));
        /// brush.GradientStops.Add(new ModernFormsNext.Drawing.GradientStop(Theme.AccentColor2, 1));
        ///
        /// groupBox.CaptionBackgroundBrush = brush;
        /// </code>
        /// </example>
        public ModernFormsNext.Drawing.Brush? CaptionBackgroundBrush
        {
            get => captionBackgroundBrush;
            set
            {
                if (captionBackgroundBrush == value)
                    return;

                captionBackgroundBrush = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the color used to draw the optional border around the caption label.
        /// </summary>
        /// <value>
        /// A color used for the caption label border, or <see langword="null"/> to use the
        /// group box frame color.
        /// </value>
        /// <remarks>
        /// This property is used only when <see cref="CaptionBorderWidth"/> is greater than zero.
        /// Changing it invalidates the control and does not affect child layout.
        /// </remarks>
        /// <example>
        /// <code>
        /// groupBox.CaptionBorderColor = Theme.AccentColor2;
        /// groupBox.CaptionBorderWidth = 1;
        /// </code>
        /// </example>
        public SKColor? CaptionBorderColor
        {
            get => captionBorderColor;
            set
            {
                if (captionBorderColor == value)
                    return;

                captionBorderColor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the corner radius used for the optional caption label border.
        /// </summary>
        /// <value>The radius in logical pixels.</value>
        /// <remarks>
        /// The value is applied to the caption background and caption border. Changing it
        /// invalidates the control and does not affect child layout.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is negative.
        /// </exception>
        public int CaptionBorderRadius
        {
            get => captionBorderRadius;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Caption border radius cannot be negative.");

                if (captionBorderRadius == value)
                    return;

                captionBorderRadius = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the width of the optional border around the caption label.
        /// </summary>
        /// <value>The border width in logical pixels. The default value is 0.</value>
        /// <remarks>
        /// A value of 0 disables the caption border. Changing this property invalidates the
        /// control and does not affect child layout.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is negative.
        /// </exception>
        /// <example>
        /// <code>
        /// groupBox.CaptionBorderWidth = 1;
        /// groupBox.CaptionBorderRadius = 3;
        /// </code>
        /// </example>
        public int CaptionBorderWidth
        {
            get => captionBorderWidth;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Caption border width cannot be negative.");

                if (captionBorderWidth == value)
                    return;

                captionBorderWidth = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the caption font size in logical pixels.
        /// </summary>
        /// <value>
        /// The caption font size, or <see langword="null"/> to use the control's current
        /// <see cref="Control.Font"/> and <see cref="ControlStyle.FontSize"/> settings.
        /// </value>
        /// <remarks>
        /// The caption size contributes to <see cref="DisplayRectangle"/>. Changing this property
        /// clears the preferred-size cache, invalidates the control, and performs layout so docked
        /// and anchored children move below the new caption height.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is less than or equal to zero.
        /// </exception>
        /// <example>
        /// <code>
        /// groupBox.CaptionFontSize = 18;
        /// </code>
        /// </example>
        public int? CaptionFontSize
        {
            get => captionFontSize;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Caption font size must be greater than zero.");

                if (captionFontSize == value)
                    return;

                captionFontSize = value;
                OnCaptionMetricsChanged(nameof(CaptionFontSize));
            }
        }

        /// <summary>
        /// Gets or sets the foreground color used for the caption text.
        /// </summary>
        /// <value>
        /// A color used for enabled caption text, or <see langword="null"/> to use the current
        /// foreground color from <see cref="Control.CurrentStyle"/>.
        /// </value>
        /// <remarks>
        /// Disabled group boxes render caption text with <see cref="Theme.ForegroundDisabledColor"/>.
        /// Changing this property invalidates the control.
        /// </remarks>
        /// <example>
        /// <code>
        /// groupBox.CaptionForegroundColor = Theme.AccentColor2;
        /// </code>
        /// </example>
        public SKColor? CaptionForegroundColor
        {
            get => captionForegroundColor;
            set
            {
                if (captionForegroundColor == value)
                    return;

                captionForegroundColor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the brush used to paint the framed content area.
        /// </summary>
        /// <value>
        /// A solid, gradient, sweep, radial, or glass brush used inside the group box frame, or
        /// <see langword="null"/> to use <see cref="ContentBackgroundColor"/> and the current
        /// style background color.
        /// </value>
        /// <remarks>
        /// The brush is clipped to the inside of the group box frame so it cannot bleed outside
        /// rounded borders. Changing this property invalidates the control and does not affect
        /// layout. The inherited <see cref="Control.BackgroundBrush"/> is still honored as a
        /// fallback for compatibility, but this property is preferred when styling only the group
        /// content area.
        /// </remarks>
        /// <example>
        /// <code>
        /// var brush = new ModernFormsNext.Drawing.LinearGradientBrush
        /// {
        ///     StartPoint = new SKPoint(0, 0),
        ///     EndPoint = new SKPoint(1, 1)
        /// };
        ///
        /// brush.GradientStops.Add(new ModernFormsNext.Drawing.GradientStop(SKColors.White, 0));
        /// brush.GradientStops.Add(new ModernFormsNext.Drawing.GradientStop(Theme.AccentColor, 1));
        ///
        /// groupBox.ContentBackgroundBrush = brush;
        /// </code>
        /// </example>
        public ModernFormsNext.Drawing.Brush? ContentBackgroundBrush
        {
            get => contentBackgroundBrush;
            set
            {
                if (contentBackgroundBrush == value)
                    return;

                contentBackgroundBrush = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the fallback color used to paint the framed content area.
        /// </summary>
        /// <value>
        /// A color used inside the group box frame, or <see langword="null"/> to use the current
        /// style background color.
        /// </value>
        /// <remarks>
        /// This color is used when <see cref="ContentBackgroundBrush"/> is <see langword="null"/>.
        /// The renderer clips the fill to the frame interior so the color cannot extend outside
        /// rounded borders. Changing this property invalidates the control and does not affect
        /// child layout.
        /// </remarks>
        /// <example>
        /// <code>
        /// groupBox.ContentBackgroundColor = new SKColor(248, 250, 252);
        /// </code>
        /// </example>
        public SKColor? ContentBackgroundColor
        {
            get => contentBackgroundColor;
            set
            {
                if (contentBackgroundColor == value)
                    return;

                contentBackgroundColor = value;
                Invalidate();
            }
        }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding(3);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size(200, 100);

        /// <inheritdoc/>
        public override Rectangle DisplayRectangle
        {
            get
            {
                var baseRectangle = base.DisplayRectangle;
                var padding = Padding;
                int captionHeight = CaptionHeight;

                return new Rectangle(
                    baseRectangle.X + padding.Left,
                    baseRectangle.Y + captionHeight + padding.Top,
                    Math.Max(0, baseRectangle.Width - padding.Horizontal),
                    Math.Max(0, baseRectangle.Height - captionHeight - padding.Vertical));
            }
        }

        /// <summary>
        /// Gets the effective caption background color used by the renderer.
        /// </summary>
        internal SKColor EffectiveCaptionBackgroundColor
            => CaptionBackgroundColor ?? CurrentStyle.GetBackgroundColor();

        /// <summary>
        /// Gets the effective caption background brush used by the renderer.
        /// </summary>
        internal ModernFormsNext.Drawing.Brush? EffectiveCaptionBackgroundBrush
            => CaptionBackgroundBrush;

        /// <summary>
        /// Gets the effective caption border color used by the renderer.
        /// </summary>
        internal SKColor EffectiveCaptionBorderColor
            => Enabled ? CaptionBorderColor ?? CurrentStyle.Border.GetColor() : Theme.BorderLowColor;

        /// <summary>
        /// Gets the effective caption font size used by layout and rendering.
        /// </summary>
        internal int EffectiveCaptionFontSize => CaptionFontSize ?? Style.GetFontSize();

        /// <summary>
        /// Gets the effective caption foreground color used by the renderer.
        /// </summary>
        internal SKColor EffectiveCaptionForegroundColor
            => Enabled ? CaptionForegroundColor ?? CurrentStyle.GetForegroundColor() : Theme.ForegroundDisabledColor;

        /// <summary>
        /// Gets the effective caption foreground brush used by the renderer.
        /// </summary>
        internal ModernFormsNext.Drawing.Brush? EffectiveCaptionForegroundBrush
            => Enabled && CaptionForegroundColor is null ? TextBrush : null;

        /// <summary>
        /// Gets the brush used by the renderer for the framed content background.
        /// </summary>
        internal ModernFormsNext.Drawing.Brush? EffectiveContentBackgroundBrush
            => ContentBackgroundBrush ?? BackgroundBrush;

        /// <summary>
        /// Gets the effective framed content background color used by the renderer.
        /// </summary>
        internal SKColor EffectiveContentBackgroundColor
            => ContentBackgroundColor ?? CurrentStyle.GetBackgroundColor();

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle(
            Control.DefaultStyle,
            style => style.Border.Width = 1);

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle(DefaultStyle);

        /// <summary>
        /// Gets or sets a value indicating whether the group frame casts a shadow.
        /// </summary>
        /// <remarks>
        /// The shadow is drawn during background painting before child controls are rendered. It is
        /// a visual effect only and does not affect layout, hit testing, or the
        /// <see cref="DisplayRectangle"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// groupBox.ShowShadow = true;
        /// groupBox.ShadowBlur = 6;
        /// groupBox.ShadowOffset = new Point(3, 3);
        /// </code>
        /// </example>
        public bool ShowShadow
        {
            get => showShadow;
            set
            {
                if (showShadow == value)
                    return;

                showShadow = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the blur radius used when <see cref="ShowShadow"/> is enabled.
        /// </summary>
        /// <value>The blur radius in logical pixels.</value>
        /// <remarks>
        /// Larger values create a softer shadow and may require slightly more paint work. Changing
        /// this property invalidates the control but does not affect layout.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is negative.
        /// </exception>
        public int ShadowBlur
        {
            get => shadowBlur;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Shadow blur cannot be negative.");

                if (shadowBlur == value)
                    return;

                shadowBlur = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the color used for the shadow when <see cref="ShowShadow"/> is enabled.
        /// </summary>
        /// <remarks>
        /// Use the alpha channel to control shadow strength. Changing this property invalidates the
        /// control but does not affect layout.
        /// </remarks>
        public SKColor ShadowColor
        {
            get => shadowColor;
            set
            {
                if (shadowColor == value)
                    return;

                shadowColor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the shadow offset used when <see cref="ShowShadow"/> is enabled.
        /// </summary>
        /// <value>The horizontal and vertical offset in logical pixels.</value>
        /// <remarks>
        /// Positive values move the shadow right and down. Changing this property invalidates the
        /// control but does not affect layout.
        /// </remarks>
        public Point ShadowOffset
        {
            get => shadowOffset;
            set
            {
                if (shadowOffset == value)
                    return;

                shadowOffset = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets the preferred caption height in logical pixels.
        /// </summary>
        internal int CaptionHeight
        {
            get
            {
                var typeface = Style.GetFont();
                int fontSize = EffectiveCaptionFontSize;
                var fontStyle = Style.GetFontStyle();

                if (cachedCaptionHeight >= 0
                    && ReferenceEquals(cachedTypeface, typeface)
                    && cachedFontSize == fontSize
                    && cachedFontStyle == fontStyle)
                {
                    return cachedCaptionHeight;
                }

                var measured = TextMeasurer.MeasureText("Ag", typeface, fontSize, TextMeasurer.MaxSize, fontStyle);
                cachedCaptionHeight = Math.Max(1, (int)Math.Ceiling(measured.Height));
                cachedTypeface = typeface;
                cachedFontSize = fontSize;
                cachedFontStyle = fontStyle;

                return cachedCaptionHeight;
            }
        }

        /// <inheritdoc/>
        internal override Size GetPreferredSizeCore(Size proposedSize)
        {
            var preferredSize = Size.Empty;

            foreach (var child in Controls)
            {
                if (child.Dock == DockStyle.Fill)
                {
                    preferredSize.Width = Math.Max(preferredSize.Width, child.Bounds.Right);
                    preferredSize.Height = Math.Max(preferredSize.Height, child.Bounds.Bottom);
                    continue;
                }

                if (child.Dock != DockStyle.Top
                    && child.Dock != DockStyle.Bottom
                    && (child.Anchor & AnchorStyles.Right) == 0)
                {
                    preferredSize.Width = Math.Max(preferredSize.Width, child.Bounds.Right + child.Margin.Right);
                }

                if (child.Dock != DockStyle.Left
                    && child.Dock != DockStyle.Right
                    && (child.Anchor & AnchorStyles.Bottom) == 0)
                {
                    preferredSize.Height = Math.Max(preferredSize.Height, child.Bounds.Bottom + child.Margin.Bottom);
                }
            }

            var padding = Padding;
            int captionHeight = CaptionHeight;
            int captionWidth = GetCaptionTextWidth();

            preferredSize.Width = Math.Max(
                preferredSize.Width + padding.Right,
                padding.Horizontal + captionWidth + CaptionMeasurementPadding);

            preferredSize.Height = Math.Max(
                preferredSize.Height + padding.Bottom,
                captionHeight + padding.Vertical);

            return preferredSize;
        }

        /// <inheritdoc/>
        protected override AccessibleObject CreateAccessibilityInstance() => new GroupBoxAccessibleObject(this);

        /// <inheritdoc/>
        protected override void OnFontChanged(EventArgs e)
        {
            ResetCaptionMetrics();
            CommonProperties.xClearPreferredSizeCache(this);
            PerformLayout(this, PropertyNames.Font);

            base.OnFontChanged(e);
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            RenderManager.Render(this, e);
        }

        /// <inheritdoc/>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (GetControlBehavior(ControlBehaviors.Transparent))
            {
                e.Canvas.Clear();
                return;
            }

            if (RenderManager.GetRenderer<Renderer>(this) is GroupBoxRenderer renderer)
            {
                renderer.RenderBackground(this, e);
                return;
            }

            base.OnPaintBackground(e);
        }

        /// <inheritdoc/>
        protected override void OnTextChanged(EventArgs e)
        {
            CommonProperties.xClearPreferredSizeCache(this);
            LayoutTransaction.DoLayoutIf(AutoSize, Parent, this, PropertyNames.Text);
            Invalidate();

            base.OnTextChanged(e);
        }

        /// <inheritdoc/>
        protected internal override void OnThemeChanged(EventArgs e)
        {
            ResetCaptionMetrics();
            CommonProperties.xClearPreferredSizeCache(this);
            PerformLayout(this, PropertyNames.Font);

            base.OnThemeChanged(e);
        }

        /// <inheritdoc/>
        public override string ToString() => $"{base.ToString()}, Text: {Text}";

        private int GetCaptionTextWidth()
        {
            if (!Text.HasValue())
                return 0;

            var measured = TextMeasurer.MeasureText(
                Text,
                Style.GetFont(),
                EffectiveCaptionFontSize,
                TextMeasurer.MaxSize,
                Style.GetFontStyle());

            return (int)Math.Ceiling(measured.Width);
        }

        private void ResetCaptionMetrics()
        {
            cachedCaptionHeight = -1;
            cachedTypeface = null;
        }

        private void OnCaptionMetricsChanged(string propertyName)
        {
            ResetCaptionMetrics();
            CommonProperties.xClearPreferredSizeCache(this);
            LayoutTransaction.DoLayoutIf(AutoSize, Parent, this, propertyName);
            PerformLayout(this, propertyName);
            Invalidate();
        }
    }
}
