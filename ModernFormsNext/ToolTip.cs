using System.ComponentModel;
using System.Drawing;
using System.Text;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides a small popup window that displays explanatory text for controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ToolTip"/> is a Windows Forms-compatible component adapted to the
    /// ModernFormsNext architecture. It associates tooltip text with existing
    /// <see cref="Control"/> instances through <see cref="SetToolTip(Control, string?)"/>
    /// and displays a platform-neutral <see cref="PopupWindow"/> rendered with SkiaSharp.
    /// </para>
    /// <para>
    /// The implementation uses existing backend popup windows, not native WinForms tooltip
    /// controls. Windows and Android provide uniform popup opacity; an external backend without
    /// this optional capability displays an opaque tooltip while retaining shared translation.
    /// </para>
    /// <para>
    /// Use and dispose the component on its controls' UI thread. Delay and auto-pop callbacks
    /// use the existing dispatcher-backed <see cref="Timer"/>; visual transitions use the shared
    /// animation scheduler. The caller owns the component, which borrows its controls and form.
    /// Disposal removes associations, cancels transitions and closes its owned popup.
    /// </para>
    /// <para>
    /// Entrance is nominally 120 milliseconds and exit 80 milliseconds, scaled by the shared
    /// animation policy. Reduced motion, disabled animations, zero duration scale or detected
    /// high contrast completes presentation immediately. Tooltips never lease keyboard/IME
    /// focus or replace an interactive popup. See <see cref="ShowAlways"/> for owner eligibility.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var saveButton = new Button { Text = "Save" };
    /// var toolTip = new ToolTip();
    /// toolTip.SetToolTip(saveButton, "Save the current document.");
    /// </code>
    /// </example>
    [ProvideProperty(nameof(ToolTip), typeof(Control))]
    [DefaultEvent(nameof(Popup))]
    [Category("Components")]
    [Description("Displays short explanatory text for ModernFormsNext controls.")]
    public partial class ToolTip : Component, IExtenderProvider
    {
        /// <summary>
        /// The default automatic delay, in milliseconds.
        /// </summary>
        public const int DefaultDelay = 500;

        internal const int DefaultHorizontalPadding = 8;
        internal const int DefaultVerticalPadding = 7;
        internal const int DefaultBorderRadius = 2;
        internal const int DefaultBalloonBorderRadius = 8;
        internal const int DefaultBorderWidth = 1;
        internal const int DefaultIconSize = 18;
        internal const int DefaultIconSpacing = 8;
        internal const int DefaultMinimumTextLineHeight = 22;
        internal const int DefaultTitleSpacing = 2;
        internal const int DefaultMaximumWidth = 360;
        internal static readonly SKColor DefaultBackColor = new(255, 255, 225);
        internal static readonly SKColor DefaultForeColor = SKColors.Black;
        internal static readonly SKColor DefaultIconForegroundColor = SKColors.White;

        private const int ReshowRatio = 5;
        private const int AutoPopRatio = 10;
        private const int PointerOffsetX = 16;
        private const int PointerOffsetY = 20;

        private readonly Dictionary<Control, ToolTipInfo> tools = [];
        private readonly Timer delayTimer;
        private readonly Timer autoPopTimer;

        private bool active = true;
        private int automaticDelay = DefaultDelay;
        private int autoPopDelay = DefaultDelay * AutoPopRatio;
        private int initialDelay = DefaultDelay;
        private int reshowDelay = DefaultDelay / ReshowRatio;
        private SKColor backColor = DefaultBackColor;
        private SKColor? borderColor;
        private int borderRadius = DefaultBorderRadius;
        private int balloonBorderRadius = DefaultBalloonBorderRadius;
        private int borderWidth = DefaultBorderWidth;
        private SKColor foreColor = DefaultForeColor;
        private SKColor? iconColor;
        private SKColor iconForegroundColor = DefaultIconForegroundColor;
        private int iconSize = DefaultIconSize;
        private int iconSpacing = DefaultIconSpacing;
        private bool isBalloon;
        private int maximumWidth = DefaultMaximumWidth;
        private Size minimumSize = Size.Empty;
        private int minimumTextLineHeight = DefaultMinimumTextLineHeight;
        private Padding padding = new(DefaultHorizontalPadding, DefaultVerticalPadding, DefaultHorizontalPadding, DefaultVerticalPadding);
        private bool showAlways;
        private bool stripAmpersands;
        private ContentAlignment textAlign = ContentAlignment.MiddleLeft;
        private Font? textFont;
        private SKColor? titleForeColor;
        private ContentAlignment titleAlign = ContentAlignment.MiddleLeft;
        private Font? titleFont;
        private int titleSpacing = DefaultTitleSpacing;
        private bool useAnimation = true;
        private bool useFading = true;
        private string toolTipTitle = string.Empty;
        private ToolTipIcon toolTipIcon;
        private Control? pendingControl;
        private ToolTipInfo? pendingInfo;
        private Control? displayedControl;
        private Point pendingScreenLocation;
        private PopupWindow? popup;
        private Form? popupParentForm;
        private ToolTipPopupControl? popupControl;

        private PopupEventHandler? onPopup;
        private DrawToolTipEventHandler? onDraw;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolTip"/> class.
        /// </summary>
        public ToolTip()
        {
            delayTimer = new Timer();
            delayTimer.Tick += DelayTimer_Tick;

            autoPopTimer = new Timer();
            autoPopTimer.Tick += AutoPopTimer_Tick;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolTip"/> class and adds it to a
        /// component container.
        /// </summary>
        /// <param name="container">The container that owns the component.</param>
        /// <exception cref="ArgumentNullException"><paramref name="container"/> is <see langword="null"/>.</exception>
        public ToolTip(IContainer container) : this()
        {
            ArgumentNullException.ThrowIfNull(container);
            container.Add(this);
        }

        /// <summary>
        /// Occurs when a tooltip is owner-drawn.
        /// </summary>
        /// <remarks>
        /// Set <see cref="OwnerDraw"/> to <see langword="true"/> to receive this event.
        /// Drawing uses SkiaSharp through <see cref="DrawToolTipEventArgs.Canvas"/> instead of
        /// <c>System.Drawing.Graphics</c>.
        /// </remarks>
        public event DrawToolTipEventHandler? Draw
        {
            add => onDraw += value;
            remove => onDraw -= value;
        }

        /// <summary>
        /// Occurs before a tooltip popup is displayed.
        /// </summary>
        /// <remarks>
        /// The event is raised after the tooltip has measured its default content and before the
        /// <see cref="PopupWindow"/> is shown. Handlers may cancel the popup or adjust
        /// <see cref="PopupEventArgs.ToolTipSize"/>.
        /// </remarks>
        public event PopupEventHandler? Popup
        {
            add => onPopup += value;
            remove => onPopup -= value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the tooltip component is active.
        /// </summary>
        /// <remarks>
        /// Setting this value to <see langword="false"/> hides any visible tooltip and prevents
        /// automatic tooltip display until re-enabled.
        /// </remarks>
        [DefaultValue(true)]
        public bool Active
        {
            get => active;
            set
            {
                VerifyPresentationAccess();
                if (active == value)
                    return;

                active = value;

                if (!active)
                    HideCurrentToolTip(immediate: true);
            }
        }

        /// <summary>
        /// Gets or sets the base delay, in milliseconds, used to derive standard tooltip delays.
        /// </summary>
        /// <remarks>
        /// Assigning this property updates <see cref="InitialDelay"/>,
        /// <see cref="ReshowDelay"/>, and <see cref="AutoPopDelay"/> using the same familiar
        /// ratios as Windows Forms: initial equals the base delay, reshow is one fifth of it,
        /// and autopop is ten times it.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
        [DefaultValue(DefaultDelay)]
        public int AutomaticDelay
        {
            get => automaticDelay;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);

                automaticDelay = value;
                initialDelay = value;
                reshowDelay = value / ReshowRatio;
                autoPopDelay = ClampDelayMultiplier(value, AutoPopRatio);
            }
        }

        /// <summary>
        /// Gets or sets how long, in milliseconds, an automatically shown tooltip remains visible.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
        public int AutoPopDelay
        {
            get => autoPopDelay;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                autoPopDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets the tooltip background color.
        /// </summary>
        /// <remarks>
        /// This color is applied to the Skia-rendered popup control and invalidates the visible
        /// tooltip, if one is currently displayed.
        /// </remarks>
        public SKColor BackColor
        {
            get => backColor;
            set
            {
                if (backColor == value)
                    return;

                backColor = value;
                ApplyVisibleStyle();
            }
        }

        /// <summary>
        /// Gets or sets the color used for the tooltip border.
        /// </summary>
        /// <remarks>
        /// The default value follows <see cref="Theme.BorderHighColor"/>. Assign this property
        /// when a tooltip should visually match a custom control surface. Changing it invalidates
        /// the visible tooltip, if one is currently displayed.
        /// </remarks>
        public SKColor BorderColor
        {
            get => borderColor ?? Theme.BorderHighColor;
            set
            {
                if (borderColor == value)
                    return;

                borderColor = value;
                ApplyVisibleStyle();
            }
        }

        /// <summary>
        /// Gets or sets the corner radius, in logical pixels, used for normal tooltip popups.
        /// </summary>
        /// <remarks>
        /// This value is used when <see cref="IsBalloon"/> is <see langword="false"/>.
        /// Changing it invalidates the visible tooltip.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
        [DefaultValue(DefaultBorderRadius)]
        public int BorderRadius
        {
            get => borderRadius;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);

                if (borderRadius == value)
                    return;

                borderRadius = value;
                ApplyVisibleStyle();
            }
        }

        /// <summary>
        /// Gets or sets the border width, in logical pixels.
        /// </summary>
        /// <remarks>
        /// Set this value to <c>0</c> to draw a borderless tooltip.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
        [DefaultValue(DefaultBorderWidth)]
        public int BorderWidth
        {
            get => borderWidth;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);

                if (borderWidth == value)
                    return;

                borderWidth = value;
                ApplyVisibleLayoutAndStyle();
            }
        }

        /// <summary>
        /// Gets or sets the corner radius, in logical pixels, used when
        /// <see cref="IsBalloon"/> is enabled.
        /// </summary>
        /// <remarks>
        /// ModernFormsNext currently draws a rounded tooltip body rather than a native balloon
        /// tail. This property controls that rounded body.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
        [DefaultValue(DefaultBalloonBorderRadius)]
        public int BalloonBorderRadius
        {
            get => balloonBorderRadius;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);

                if (balloonBorderRadius == value)
                    return;

                balloonBorderRadius = value;
                ApplyVisibleStyle();
            }
        }

        /// <summary>
        /// Gets or sets the tooltip foreground color.
        /// </summary>
        /// <remarks>
        /// This color is used for default tooltip text and for the helper methods on
        /// <see cref="DrawToolTipEventArgs"/>.
        /// </remarks>
        public SKColor ForeColor
        {
            get => foreColor;
            set
            {
                if (foreColor == value)
                    return;

                foreColor = value;
                ApplyVisibleStyle();
            }
        }

        /// <summary>
        /// Gets or sets the optional background color used for built-in tooltip icons.
        /// </summary>
        /// <remarks>
        /// When this property is <see langword="null"/>, each <see cref="ToolTipIcon"/> uses its
        /// standard ModernFormsNext color. Set this property to force all built-in icons to use
        /// the same background color.
        /// </remarks>
        [DefaultValue(null)]
        public SKColor? IconColor
        {
            get => iconColor;
            set
            {
                if (iconColor == value)
                    return;

                iconColor = value;
                popupControl?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the foreground color used inside built-in tooltip icons.
        /// </summary>
        public SKColor IconForegroundColor
        {
            get => iconForegroundColor;
            set
            {
                if (iconForegroundColor == value)
                    return;

                iconForegroundColor = value;
                popupControl?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the built-in icon size, in logical pixels.
        /// </summary>
        /// <remarks>
        /// This property affects only the ModernFormsNext-rendered built-in icons. Owner-drawn
        /// tooltips control their own icon rendering in the <see cref="Draw"/> event.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than one.</exception>
        [DefaultValue(DefaultIconSize)]
        public int IconSize
        {
            get => iconSize;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);

                if (iconSize == value)
                    return;

                iconSize = value;
                ApplyVisibleLayoutAndStyle();
            }
        }

        /// <summary>
        /// Gets or sets the spacing, in logical pixels, between the icon and the text block.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
        [DefaultValue(DefaultIconSpacing)]
        public int IconSpacing
        {
            get => iconSpacing;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);

                if (iconSpacing == value)
                    return;

                iconSpacing = value;
                ApplyVisibleLayoutAndStyle();
            }
        }

        /// <summary>
        /// Gets or sets whether tooltip popups use rounded balloon-style rendering.
        /// </summary>
        /// <remarks>
        /// ModernFormsNext does not currently draw native balloon tails. This property affects
        /// the popup corner radius and is preserved for source compatibility with Windows
        /// Forms-style tooltip code.
        /// </remarks>
        [DefaultValue(false)]
        public bool IsBalloon
        {
            get => isBalloon;
            set
            {
                if (isBalloon == value)
                    return;

                isBalloon = value;
                ApplyVisibleStyle();
            }
        }

        /// <summary>
        /// Gets or sets the delay, in milliseconds, before an automatic tooltip first appears.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
        public int InitialDelay
        {
            get => initialDelay;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                initialDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets whether tooltip content is drawn by application code.
        /// </summary>
        /// <remarks>
        /// When this property is <see langword="true"/>, the <see cref="Draw"/> event is raised
        /// during Skia rendering. The default tooltip background is still cleared before the draw
        /// event, matching the rest of the ModernFormsNext control pipeline.
        /// </remarks>
        [DefaultValue(false)]
        public bool OwnerDraw { get; set; }

        /// <summary>
        /// Gets or sets the maximum tooltip width, in logical pixels.
        /// </summary>
        /// <remarks>
        /// Text wraps when the measured tooltip would exceed this width. The value includes
        /// padding, border, icon, and text. Use a larger value for wide help text or a smaller
        /// value for compact popups.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than one.</exception>
        [DefaultValue(DefaultMaximumWidth)]
        public int MaximumWidth
        {
            get => maximumWidth;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);

                if (maximumWidth == value)
                    return;

                maximumWidth = value;
                ApplyVisibleLayoutAndStyle();
            }
        }

        /// <summary>
        /// Gets or sets the minimum tooltip size, in logical pixels.
        /// </summary>
        /// <remarks>
        /// The measured content can grow beyond this value. Set this property when a group of
        /// tooltips should share the same visual footprint.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">A width or height is less than zero.</exception>
        public Size MinimumSize
        {
            get => minimumSize;
            set
            {
                if (value.Width < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Minimum width cannot be negative.");

                if (value.Height < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Minimum height cannot be negative.");

                if (minimumSize == value)
                    return;

                minimumSize = value;
                ApplyVisibleLayoutAndStyle();
            }
        }

        /// <summary>
        /// Gets or sets the minimum height, in logical pixels, reserved for a line of tooltip text.
        /// </summary>
        /// <remarks>
        /// This protects short, title-less tooltips from becoming too short for the text renderer
        /// at fractional DPI scales or backend-specific font metrics.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than one.</exception>
        [DefaultValue(DefaultMinimumTextLineHeight)]
        public int MinimumTextLineHeight
        {
            get => minimumTextLineHeight;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);

                if (minimumTextLineHeight == value)
                    return;

                minimumTextLineHeight = value;
                ApplyVisibleLayoutAndStyle();
            }
        }

        /// <summary>
        /// Gets or sets the inner padding, in logical pixels, between the border and tooltip content.
        /// </summary>
        /// <remarks>
        /// This value is independent from the associated control's padding and affects only the
        /// Skia-rendered popup body.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Any side is less than zero.</exception>
        public Padding Padding
        {
            get => padding;
            set
            {
                ValidatePadding(value);

                if (padding == value)
                    return;

                padding = value;
                ApplyVisibleLayoutAndStyle();
            }
        }

        /// <summary>
        /// Gets or sets the delay, in milliseconds, used when moving between tooltip regions.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
        public int ReshowDelay
        {
            get => reshowDelay;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                reshowDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets whether tooltips should appear even when the parent window is inactive.
        /// </summary>
        /// <remarks>
        /// False requires an active visible owning form. True permits a visible inactive desktop
        /// owner, including desktop process deactivation, without activating it. Known process
        /// background, suspension, loss of all hosts, exit, or owner/anchor retirement hides
        /// immediately. Android popups cannot outlive their Activity. Unknown lifecycle support
        /// falls back to actual owner visibility/activation; external backends must support
        /// nonactivating native popups to preserve native focus. Set on the owning UI thread.
        /// </remarks>
        [DefaultValue(false)]
        public bool ShowAlways
        {
            get => showAlways;
            set
            {
                VerifyPresentationAccess();
                showAlways = value;
                if (popup is not null) popup.HideOnParentDeactivation = !value;
                if (!value && popupParentForm is { IsActive: false }) HideCurrentToolTip(immediate: true);
                if (!value && pendingForm is { IsActive: false }) ClearPending();
            }
        }

        /// <summary>
        /// Gets or sets whether single ampersands are stripped from tooltip text.
        /// </summary>
        /// <remarks>
        /// This is useful when tooltip text is shared with button/menu labels that use ampersands
        /// for mnemonic markers. A doubled ampersand is rendered as a single literal ampersand.
        /// </remarks>
        [DefaultValue(false)]
        public bool StripAmpersands
        {
            get => stripAmpersands;
            set => stripAmpersands = value;
        }

        /// <summary>
        /// Gets or sets the alignment used for tooltip body text.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="ContentAlignment.MiddleLeft"/>. Title-less tooltips are still
        /// given enough line height for text to render cleanly.
        /// </remarks>
        [DefaultValue(ContentAlignment.MiddleLeft)]
        public ContentAlignment TextAlign
        {
            get => textAlign;
            set
            {
                if (!Enum.IsDefined(value))
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(ContentAlignment));

                if (textAlign == value)
                    return;

                textAlign = value;
                popupControl?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the font used for tooltip body text.
        /// </summary>
        /// <remarks>
        /// Set this property to <see langword="null"/> to use the current ModernFormsNext theme
        /// font. Changing the font affects measurement and invalidates any visible tooltip.
        /// </remarks>
        [DefaultValue(null)]
        public Font? TextFont
        {
            get => textFont;
            set
            {
                if (Equals(textFont, value))
                    return;

                textFont = value;
                ApplyVisibleLayoutAndStyle();
            }
        }

        /// <summary>
        /// Gets or sets custom user data associated with this tooltip component.
        /// </summary>
        [DefaultValue(null)]
        public object? Tag { get; set; }

        /// <summary>
        /// Gets or sets the icon displayed next to tooltip text.
        /// </summary>
        /// <exception cref="InvalidEnumArgumentException">The value is not a valid <see cref="ToolTipIcon"/>.</exception>
        [DefaultValue(ToolTipIcon.None)]
        public ToolTipIcon ToolTipIcon
        {
            get => toolTipIcon;
            set
            {
                if (!Enum.IsDefined(value))
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(ToolTipIcon));

                if (toolTipIcon == value)
                    return;

                toolTipIcon = value;
                popupControl?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the optional title displayed above tooltip text.
        /// </summary>
        [DefaultValue("")]
        public string ToolTipTitle
        {
            get => toolTipTitle;
            set
            {
                value ??= string.Empty;

                if (toolTipTitle == value)
                    return;

                toolTipTitle = value;
                popupControl?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the alignment used for the optional tooltip title.
        /// </summary>
        [DefaultValue(ContentAlignment.MiddleLeft)]
        public ContentAlignment TitleAlign
        {
            get => titleAlign;
            set
            {
                if (!Enum.IsDefined(value))
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(ContentAlignment));

                if (titleAlign == value)
                    return;

                titleAlign = value;
                popupControl?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the optional foreground color used for the tooltip title.
        /// </summary>
        /// <remarks>
        /// When this property is <see langword="null"/>, the title uses <see cref="ForeColor"/>.
        /// </remarks>
        [DefaultValue(null)]
        public SKColor? TitleForeColor
        {
            get => titleForeColor;
            set
            {
                if (titleForeColor == value)
                    return;

                titleForeColor = value;
                popupControl?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the font used for the optional tooltip title.
        /// </summary>
        /// <remarks>
        /// Set this property to <see langword="null"/> to use the current bold ModernFormsNext
        /// theme font.
        /// </remarks>
        [DefaultValue(null)]
        public Font? TitleFont
        {
            get => titleFont;
            set
            {
                if (Equals(titleFont, value))
                    return;

                titleFont = value;
                ApplyVisibleLayoutAndStyle();
            }
        }

        /// <summary>
        /// Gets or sets the spacing, in logical pixels, between the title and body text.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
        [DefaultValue(DefaultTitleSpacing)]
        public int TitleSpacing
        {
            get => titleSpacing;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);

                if (titleSpacing == value)
                    return;

                titleSpacing = value;
                ApplyVisibleLayoutAndStyle();
            }
        }

        /// <summary>
        /// Gets or sets whether tooltip animation is requested.
        /// </summary>
        /// <remarks>
        /// True translates content by at most six logical pixels during a nominal 120-millisecond
        /// entrance and 80-millisecond exit using the shared scheduler and its duration scale.
        /// False disables translation independently from <see cref="UseFading"/>. Reduced motion
        /// and detected high contrast settle immediately. Changing this value settles a visible
        /// entrance or completes an exit. This affects rendering, not control layout or focus.
        /// Set on the owning UI thread.
        /// </remarks>
        [DefaultValue(true)]
        public bool UseAnimation
        {
            get => useAnimation;
            set { VerifyPresentationAccess(); if (useAnimation != value) { useAnimation = value; SettlePresentation(); } }
        }

        /// <summary>
        /// Gets or sets whether tooltip fading is requested.
        /// </summary>
        /// <remarks>
        /// True requests whole-popup opacity during the same bounded transition as
        /// <see cref="UseAnimation"/>, including the popup background. First-party Windows and
        /// Android backends support it; missing/refused optional support retains opaque
        /// presentation. False disables fading independently from translation. Reduced motion
        /// and detected high contrast settle immediately. Set on the owning UI thread.
        /// </remarks>
        [DefaultValue(true)]
        public bool UseFading
        {
            get => useFading;
            set { VerifyPresentationAccess(); if (useFading != value) { useFading = value; SettlePresentation(); } }
        }

        /// <summary>
        /// Determines whether this tooltip can extend the specified target.
        /// </summary>
        /// <param name="target">The object to test.</param>
        /// <returns><see langword="true"/> when <paramref name="target"/> is a <see cref="Control"/>.</returns>
        public bool CanExtend(object target) => target is Control;

        /// <summary>
        /// Gets the tooltip text associated with the specified control.
        /// </summary>
        /// <param name="control">The control to inspect.</param>
        /// <returns>The tooltip text, or an empty string when no tooltip is associated.</returns>
        public string? GetToolTip(Control? control)
        {
            if (control is null)
                return string.Empty;

            return tools.TryGetValue(control, out var info)
                ? info.Caption
                : string.Empty;
        }

        /// <summary>
        /// Hides the tooltip associated with the specified control.
        /// </summary>
        /// <param name="control">The control whose tooltip should be hidden.</param>
        /// <exception cref="ArgumentNullException"><paramref name="control"/> is <see langword="null"/>.</exception>
        public void Hide(Control control)
        {
            ArgumentNullException.ThrowIfNull(control);
            VerifyPresentationAccess();

            // Popup runs before displayedControl is assigned. Hide for that same anchor is
            // still a newer dismissal request; hiding an unrelated control remains a no-op.
            if (ReferenceEquals(popupCallbackAnchor, control))
            {
                ++requestVersion;
                popupCallbackAnchor = null;
            }

            if (pendingControl == control)
                ClearPending();

            if (displayedControl == control)
                HideCurrentToolTip();
        }

        /// <summary>
        /// Removes all tooltip associations maintained by this component.
        /// </summary>
        public void RemoveAll()
        {
            VerifyPresentationAccess();
            foreach (var control in tools.Keys.ToArray())
                Detach(control);

            tools.Clear();
            ClearPending();
            HideCurrentToolTip(immediate: true);
        }

        /// <summary>
        /// Associates tooltip text with a control.
        /// </summary>
        /// <param name="control">The control that owns the tooltip text.</param>
        /// <param name="caption">The tooltip text, or <see langword="null"/>/empty to remove the association.</param>
        /// <exception cref="ArgumentNullException"><paramref name="control"/> is <see langword="null"/>.</exception>
        public void SetToolTip(Control control, string? caption)
        {
            ArgumentNullException.ThrowIfNull(control);
            VerifyPresentationAccess();

            if (string.IsNullOrEmpty(caption))
            {
                if (tools.Remove(control))
                    Detach(control);

                if (pendingControl == control)
                    ClearPending();

                if (displayedControl == control)
                    HideCurrentToolTip(immediate: true);

                return;
            }

            if (!tools.ContainsKey(control))
                Attach(control);

            tools[control] = new ToolTipInfo(caption, ToolTipDisplayMode.Automatic);
            if (pendingControl == control) pendingInfo = tools[control];
        }

        /// <summary>
        /// Displays tooltip text near the bottom of a control.
        /// </summary>
        /// <param name="text">The tooltip text to display.</param>
        /// <param name="control">The control used as the popup anchor.</param>
        public void Show(string? text, Control control)
            => Show(text, control, control.Width / 2, control.Height);

        /// <summary>
        /// Displays tooltip text near the bottom of a control for the specified duration.
        /// </summary>
        /// <param name="text">The tooltip text to display.</param>
        /// <param name="control">The control used as the popup anchor.</param>
        /// <param name="duration">The display duration, in milliseconds. Zero keeps the tooltip visible until hidden or auto-pop timing applies.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is less than zero.</exception>
        public void Show(string? text, Control control, int duration)
            => Show(text, control, control.Width / 2, control.Height, duration);

        /// <summary>
        /// Displays tooltip text at a location relative to a control.
        /// </summary>
        /// <param name="text">The tooltip text to display.</param>
        /// <param name="control">The control used as the popup anchor.</param>
        /// <param name="point">The location, in logical pixels, relative to <paramref name="control"/>.</param>
        public void Show(string? text, Control control, Point point)
            => Show(text, control, point.X, point.Y);

        /// <summary>
        /// Displays tooltip text at a location relative to a control for the specified duration.
        /// </summary>
        /// <param name="text">The tooltip text to display.</param>
        /// <param name="control">The control used as the popup anchor.</param>
        /// <param name="point">The location, in logical pixels, relative to <paramref name="control"/>.</param>
        /// <param name="duration">The display duration, in milliseconds. Zero keeps the tooltip visible until hidden or auto-pop timing applies.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is less than zero.</exception>
        public void Show(string? text, Control control, Point point, int duration)
            => Show(text, control, point.X, point.Y, duration);

        /// <summary>
        /// Displays tooltip text at a location relative to a control.
        /// </summary>
        /// <param name="text">The tooltip text to display.</param>
        /// <param name="control">The control used as the popup anchor.</param>
        /// <param name="x">The horizontal location, in logical pixels, relative to <paramref name="control"/>.</param>
        /// <param name="y">The vertical location, in logical pixels, relative to <paramref name="control"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="control"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="control"/> is not parented to a <see cref="Form"/>.</exception>
        public void Show(string? text, Control control, int x, int y)
            => Show(text, control, x, y, 0);

        /// <summary>
        /// Displays tooltip text at a location relative to a control for the specified duration.
        /// </summary>
        /// <param name="text">The tooltip text to display.</param>
        /// <param name="control">The control used as the popup anchor.</param>
        /// <param name="x">The horizontal location, in logical pixels, relative to <paramref name="control"/>.</param>
        /// <param name="y">The vertical location, in logical pixels, relative to <paramref name="control"/>.</param>
        /// <param name="duration">The display duration, in milliseconds. Zero keeps the tooltip visible until hidden or auto-pop timing applies.</param>
        /// <exception cref="ArgumentNullException"><paramref name="control"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is less than zero.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="control"/> is not parented to a <see cref="Form"/>.</exception>
        public void Show(string? text, Control control, int x, int y, int duration)
        {
            ArgumentNullException.ThrowIfNull(control);
            ArgumentOutOfRangeException.ThrowIfNegative(duration);
            VerifyPresentationAccess();
            control.GetFocusScope().VerifyAccess();

            var screenPoint = control.PointToScreen(new Point(x, y));
            var info = new ToolTipInfo(text, ToolTipDisplayMode.Absolute) { ScreenLocation = screenPoint };
            ShowToolTip(control, info, screenPoint, duration, throwIfNoForm: true);
        }

        internal int EffectiveBorderRadius => IsBalloon ? BalloonBorderRadius : BorderRadius;

        internal int EffectiveTextFontSize
            => TextFont is null ? Theme.FontSize : Math.Max(1, (int)Math.Round(TextFont.SizeInPoints));

        internal FontStyle EffectiveTextFontStyle => TextFont?.Style ?? FontStyle.Regular;

        internal SKTypeface EffectiveTextTypeface => TextFont?.ToTypeface() ?? Theme.UIFont;

        internal SKColor EffectiveTitleForeColor => highContrast ? PresentationForeColor : TitleForeColor ?? ForeColor;

        internal int EffectiveTitleFontSize
            => TitleFont is null ? Theme.FontSize : Math.Max(1, (int)Math.Round(TitleFont.SizeInPoints));

        internal FontStyle EffectiveTitleFontStyle => TitleFont?.Style ?? FontStyle.Bold;

        internal SKTypeface EffectiveTitleTypeface => TitleFont?.ToTypeface() ?? Theme.UIFontBold;

        internal void RaiseDraw(DrawToolTipEventArgs e) => OnDraw(e);

        internal SKColor ResolveIconBackColor(ToolTipIcon icon)
        {
            if (highContrast) return PresentationForeColor;
            if (IconColor is { } color)
                return color;

            return icon switch
            {
                ToolTipIcon.Warning => new SKColor(245, 169, 39),
                ToolTipIcon.Error => Theme.WarningHighlightColor,
                _ => Theme.AccentColor2
            };
        }

        /// <summary>
        /// Raises the <see cref="Draw"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnDraw(DrawToolTipEventArgs e)
        {
            onDraw?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="Popup"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnPopup(PopupEventArgs e)
        {
            onPopup?.Invoke(this, e);
        }

        /// <summary>
        /// Releases the resources used by the <see cref="ToolTip"/>.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposing) { base.Dispose(false); return; }
            DisposePresentation();
        }

        private static int ClampDelayMultiplier(int value, int multiplier)
        {
            var result = (long)value * multiplier;
            return result > int.MaxValue ? int.MaxValue : (int)result;
        }

        private static int TimerIntervalFromDelay(int delay)
            => Math.Max(1, delay);

        private static void ValidatePadding(Padding value)
        {
            if (value.Left < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Left padding cannot be negative.");

            if (value.Top < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Top padding cannot be negative.");

            if (value.Right < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Right padding cannot be negative.");

            if (value.Bottom < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Bottom padding cannot be negative.");
        }

        private void Attach(Control control)
        {
            control.MouseEnter += Control_MouseEnter;
            control.MouseMove += Control_MouseMove;
            control.MouseLeave += Control_MouseLeave;
            control.MouseDown += Control_MouseDown;
            control.Disposed += Control_Disposed;
            control.ParentChanged += AnchorPresentationChanged;
            control.VisibleChanged += AnchorPresentationChanged;
        }

        private void Detach(Control control)
        {
            control.MouseEnter -= Control_MouseEnter;
            control.MouseMove -= Control_MouseMove;
            control.MouseLeave -= Control_MouseLeave;
            control.MouseDown -= Control_MouseDown;
            control.Disposed -= Control_Disposed;
            control.ParentChanged -= AnchorPresentationChanged;
            control.VisibleChanged -= AnchorPresentationChanged;
        }

        private void Control_Disposed(object? sender, EventArgs e)
        {
            if (sender is not Control control)
                return;

            if (tools.Remove(control))
                Detach(control);

            if (pendingControl == control)
                ClearPending();

            if (displayedControl == control)
                HideCurrentToolTip(immediate: true);
        }

        private void Control_MouseDown(object? sender, MouseEventArgs e)
        {
            if (sender is Control control)
                Hide(control);
        }

        private void Control_MouseEnter(object? sender, MouseEventArgs e)
        {
            if (sender is Control control)
                BeginAutomaticShow(control, e);
        }

        private void Control_MouseLeave(object? sender, EventArgs e)
        {
            if (sender is not Control control)
                return;

            if (pendingControl == control)
                ClearPending();

            if (displayedControl == control)
                HideCurrentToolTip();
        }

        private void Control_MouseMove(object? sender, MouseEventArgs e)
        {
            if (sender is not Control control || !tools.TryGetValue(control, out _))
                return;

            pendingScreenLocation = control.PointToScreen(e.Location);

            if (pendingControl is null && displayedControl != control)
                BeginAutomaticShow(control, e);
        }

        private void BeginAutomaticShow(Control control, MouseEventArgs e)
        {
            if (presentationDisposed || presentationRetiring || !active || !tools.TryGetValue(control, out var info) || string.IsNullOrEmpty(info.Caption))
                return;

            var form = control.FindForm();
            if (form is null || !CanPresent(control, form)) return;
            long request = ++requestVersion;
            ClearPending();

            pendingControl = control;
            pendingInfo = info;
            pendingScreenLocation = control.PointToScreen(e.Location);

            ObservePendingForm(form);
            ObservePresentationServices();
            if (!IsCurrentRequest(request)) return;
            var useReshowDelay = displayedControl is not null ||
                lastHiddenAt is { } hidden && Animations.AnimationScheduler.Default.ClockTime - hidden <= TimeSpan.FromMilliseconds(Math.Max(InitialDelay, 1));
            var delay = useReshowDelay ? ReshowDelay : InitialDelay;

            delayTimer.Stop();

            if (delay == 0)
            {
                ShowPendingToolTip();
                return;
            }

            delayTimer.Interval = TimerIntervalFromDelay(delay);
            delayTimer.Start();
        }

        private void DelayTimer_Tick(object? sender, EventArgs e)
        {
            delayTimer.Stop();
            ShowPendingToolTip();
        }

        private void AutoPopTimer_Tick(object? sender, EventArgs e)
        {
            autoPopTimer.Stop();
            HideCurrentToolTip();
        }

        private void ShowPendingToolTip()
        {
            if (pendingControl is null || pendingInfo is null)
                return;

            var control = pendingControl;
            var info = pendingInfo;
            var location = pendingScreenLocation;
            location.Offset(PointerOffsetX, PointerOffsetY);
            ClearPending();
            ShowToolTip(control, info, location, AutoPopDelay, throwIfNoForm: false);
        }

        private void ShowToolTip(Control associatedControl, ToolTipInfo info, Point screenLocation, int duration, bool throwIfNoForm)
        {
            VerifyPresentationAccess();
            long request = ++requestVersion;
            ClearPending();
            if (!Active || string.IsNullOrEmpty(info.Caption))
            {
                HideCurrentToolTip(immediate: true);
                return;
            }

            var form = associatedControl.FindForm();

            if (form is null)
            {
                if (throwIfNoForm)
                    throw new InvalidOperationException("Cannot show a ToolTip for a control that is not parented to a Form.");

                return;
            }

            if (!CanPresent(associatedControl, form)) return;

            var text = ProcessText(info.Caption);
            var title = ProcessText(ToolTipTitle);
            var proposedSize = MeasureToolTipForControl(text, title, ToolTipIcon, associatedControl);
            var popupEventArgs = new PopupEventArgs(form, associatedControl, IsBalloon, proposedSize);

            // Popup is intentionally raised before the popup window is configured so handlers can
            // cancel display or reserve a custom owner-drawn size, matching the WinForms event order.
            popupCallbackAnchor = associatedControl;
            try { OnPopup(popupEventArgs); }
            finally
            {
                if (IsCurrentRequest(request)) popupCallbackAnchor = null;
            }

            if (popupEventArgs.Cancel || !IsCurrentRequest(request) || !CanPresent(associatedControl, form))
                return;

            var size = ClampSize(popupEventArgs.ToolTipSize);
            PresentToolTip(request, associatedControl, form, text, title, size, screenLocation, duration);
        }

        private bool EnsurePopup(Form form, long request)
        {
            if (popup is { InputBindingsClosed: false } && popupParentForm == form)
                return true;

            ClosePresentationPopup();
            if (!IsCurrentRequest(request)) return false;
            popup = new PopupWindow(form) { IsPassive = true, HideOnParentDeactivation = !ShowAlways };
            popupParentForm = form;
            popup.VisibleChanged += PopupPresentationVisibilityChanged;
            popup.Closed += PopupPresentationClosed;
            popupControl = new ToolTipPopupControl();
            popup.Controls.Add(popupControl);
            return IsCurrentRequest(request);
        }

        private void StartAutoPopTimer(int duration)
        {
            autoPopTimer.Stop();

            if (duration <= 0)
                return;

            autoPopTimer.Interval = TimerIntervalFromDelay(duration);
            autoPopTimer.Start();
        }

        private void ClearPending(bool stopDelayTimer = true)
        {
            if (stopDelayTimer)
                delayTimer.Stop();

            pendingControl = null;
            pendingInfo = null;
            ObservePendingForm(null);
            ReleasePresentationServicesIfIdle();
        }

        private void HideCurrentToolTip(bool immediate = false) => RetirePresentation(immediate);

        private void ApplyVisibleLayoutAndStyle()
        {
            if (popupControl is null)
                return;

            ApplyVisibleStyle();

            var size = MeasureToolTip(popupControl.TextToDisplay, popupControl.TitleToDisplay, popupControl.Icon);
            popupControl.Size = size;

            if (popup is not null)
                popup.Size = size;
        }

        private void ApplyVisibleStyle()
        {
            if (popupControl is null)
                return;

            popupControl.ApplyOwnerStyle(this);
            if (popup is not null)
                popup.Style.BackgroundColor = popupControl.CurrentStyle.GetBackgroundColor();
            popupControl.Invalidate();
        }

        private Size MeasureToolTip(string text, string title, ToolTipIcon icon)
            => MeasureToolTipForControl(text, title, icon, popupControl);

        private Size MeasureToolTipForControl(string text, string title, ToolTipIcon icon, Control? scaleSource)
        {
            double scale = scaleSource?.Scaling ?? 1;
            int Device(int value) => scaleSource?.LogicalToDeviceUnits(value) ?? value;
            int Logical(int value) => Math.Max(1, (int)Math.Ceiling(value / scale));
            var contentWidth = 0;
            var paddingWidth = Device(Padding.Left) + Device(Padding.Right) + Device(BorderWidth) * 2;
            var paddingHeight = Device(Padding.Top) + Device(Padding.Bottom) + Device(BorderWidth) * 2;
            var iconWidth = icon == ToolTipIcon.None ? 0 : Device(IconSize) + Device(IconSpacing);
            var iconHeight = icon == ToolTipIcon.None ? 0 : Device(IconSize);
            var maxTextWidth = Math.Max(1, (int)Math.Floor(MaximumWidth * scale) - paddingWidth - iconWidth);
            var maxTextSize = new Size(maxTextWidth, int.MaxValue);
            int titleFontSize = Math.Max(1, Device(EffectiveTitleFontSize));
            int textFontSize = Math.Max(1, Device(EffectiveTextFontSize));

            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleSize = TextMeasurer.MeasureText(title, EffectiveTitleTypeface, titleFontSize, maxTextSize, EffectiveTitleFontStyle);
                contentWidth = Math.Max(contentWidth, (int)Math.Ceiling(titleSize.Width));
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                var textSize = TextMeasurer.MeasureText(text, EffectiveTextTypeface, textFontSize, maxTextSize, EffectiveTextFontStyle);
                contentWidth = Math.Max(contentWidth, (int)Math.Ceiling(textSize.Width));
            }

            var width = Math.Min(MaximumWidth, Math.Max(MinimumSize.Width, Logical(contentWidth + iconWidth + paddingWidth)));
            // Painting rounds font/padding to device pixels. Measuring at logical font size and
            // scaling the result can wrap an extra line at fractional DPI. Shape at the actual
            // device font size, then reflow at the final bounded width before choosing height.
            var actualTextSize = new Size(Math.Max(1, (int)Math.Floor(width * scale) - paddingWidth - iconWidth), int.MaxValue);
            var textBlockHeight = 0;
            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleSize = TextMeasurer.MeasureText(title, EffectiveTitleTypeface, titleFontSize, actualTextSize, EffectiveTitleFontStyle);
                textBlockHeight = Math.Max(Device(MinimumTextLineHeight), (int)Math.Ceiling(titleSize.Height));
            }
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (textBlockHeight > 0)
                    textBlockHeight += Device(TitleSpacing);
                var textSize = TextMeasurer.MeasureText(text, EffectiveTextTypeface, textFontSize, actualTextSize, EffectiveTextFontStyle);
                textBlockHeight += Math.Max(Device(MinimumTextLineHeight), (int)Math.Ceiling(textSize.Height));
            }
            var height = Math.Max(MinimumSize.Height, Logical(Math.Max(textBlockHeight, iconHeight) + paddingHeight));

            return new Size(Math.Max(1, width), Math.Max(1, height));
        }

        private static Size ClampSize(Size size)
            => new(Math.Max(1, size.Width), Math.Max(1, size.Height));

        private string ProcessText(string? text)
        {
            text ??= string.Empty;

            if (!StripAmpersands || text.IndexOf('&') < 0)
                return text;

            var builder = new StringBuilder(text.Length);

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '&')
                {
                    builder.Append(text[i]);
                    continue;
                }

                if (i + 1 < text.Length && text[i + 1] == '&')
                {
                    builder.Append('&');
                    i++;
                }
            }

            return builder.ToString();
        }
    }
}
