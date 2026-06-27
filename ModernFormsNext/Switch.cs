using System;
using System.Collections.Generic;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Animations;
using ModernFormsNext.Layout;
using ModernFormsNext.Renderers;
using SkiaSharp;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a highly customizable switch control that can operate as either a Boolean
    /// switch or a three-position switch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In <see cref="SwitchMode.TwoState"/> mode the switch uses the values 0 and 1 and the
    /// <see cref="IsToggled"/> property provides the usual Boolean API. In
    /// <see cref="SwitchMode.ThreeState"/> mode the <see cref="Value"/> property can be -1, 0,
    /// or 1, which allows the thumb to rest on the left, center, or right position.
    /// </para>
    /// <para>
    /// The control is rendered by <see cref="SwitchRenderer"/> and is fully custom drawn. Visual
    /// properties invalidate rendering only; they do not create native platform controls.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var themeSwitch = new Switch
    /// {
    ///     IsToggled = true,
    ///     OffIcon = SwitchIconKind.Moon,
    ///     OnIcon = SwitchIconKind.Sun,
    ///     OnTrackColor = Theme.AccentColor2
    /// };
    ///
    /// themeSwitch.Toggled += (_, e) =>
    /// {
    ///     Console.WriteLine(e.Value ? "Light" : "Dark");
    /// };
    /// </code>
    /// </example>
    public class Switch : Control
    {
        private const string VisualPositionAnimationKey = "Switch.VisualPosition";
        private const int DragThreshold = 3;

        private SwitchMode mode;
        private SwitchActivationMode activationMode;
        private int currentValue;
        private float visualPosition;
        private bool autoToggle = true;
        private bool allowDragging = true;
        private bool updateValueWhileDragging;
        private bool animate = true;
        private int animationDuration = 160;
        private double animationSpeed = 1d;
        private Func<float, float>? animationEasing;

        private SKColor? offTrackColor;
        private SKColor? negativeTrackColor;
        private SKColor? onTrackColor;
        private SKColor? trackBorderColor;
        private int trackBorderWidth = 1;
        private int trackCornerRadius = -1;
        private MfnBrush? offTrackBrush;
        private MfnBrush? negativeTrackBrush;
        private MfnBrush? onTrackBrush;

        private SKColor? offThumbColor;
        private SKColor? negativeThumbColor;
        private SKColor? onThumbColor;
        private SKColor? thumbColor;
        private SKColor? thumbBorderColor;
        private int thumbBorderWidth = 1;
        private int thumbCornerRadius = -1;
        private int thumbInset = 4;
        private int thumbSize;
        private MfnBrush? offThumbBrush;
        private MfnBrush? negativeThumbBrush;
        private MfnBrush? onThumbBrush;
        private MfnBrush? thumbBrush;

        private SwitchIconKind offIcon;
        private SwitchIconKind negativeIcon;
        private SwitchIconKind onIcon;
        private SwitchIconKind thumbIcon;
        private SKBitmap? offIconImage;
        private SKBitmap? negativeIconImage;
        private SKBitmap? onIconImage;
        private SKBitmap? thumbIconImage;
        private SKColor? offIconColor;
        private SKColor? negativeIconColor;
        private SKColor? onIconColor;
        private SKColor? thumbIconColor;
        private int iconSize;

        private bool thumbPressed;
        private bool thumbHovered;
        private bool dragging;
        private bool suppressNextClick;
        private Point dragStartLocation;

        /// <summary>
        /// Initializes a new instance of the <see cref="Switch"/> class.
        /// </summary>
        public Switch()
        {
            TabStop = true;
            SetControlBehavior(ControlBehaviors.Hoverable | ControlBehaviors.Selectable);
        }

        /// <summary>
        /// Gets the default <see cref="ControlStyle"/> used by <see cref="Switch"/> controls.
        /// </summary>
        public new static ControlStyle DefaultStyle = new ControlStyle(Control.DefaultStyle,
            style => {
                style.BackgroundColor = SKColors.Transparent;
                style.Border.Width = 0;
            });

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle(DefaultStyle);

        /// <inheritdoc/>
        protected override Cursor DefaultCursor => Cursors.Hand;

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size(56, 28);

        /// <summary>
        /// Gets or sets whether the switch uses Boolean or three-position behavior.
        /// </summary>
        /// <remarks>
        /// Changing this property invalidates rendering. If the switch is moved from
        /// <see cref="SwitchMode.ThreeState"/> to <see cref="SwitchMode.TwoState"/> while
        /// <see cref="Value"/> is -1, the value is coerced to 0 and change events are raised.
        /// </remarks>
        public SwitchMode Mode
        {
            get => mode;
            set
            {
                SourceGenerated.EnumValidator.Validate(value);

                if (mode == value)
                    return;

                mode = value;

                if (mode == SwitchMode.TwoState && currentValue < 0)
                    SetValueCore(0, raiseEvents: true, animateChange: Animate);
                else
                    AnimateVisualToValue(currentValue, Animate);

                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the switch exposes three positions.
        /// </summary>
        /// <remarks>
        /// This is a convenience wrapper over <see cref="Mode"/> for WinForms-style code.
        /// </remarks>
        public bool ThreeState
        {
            get => Mode == SwitchMode.ThreeState;
            set => Mode = value ? SwitchMode.ThreeState : SwitchMode.TwoState;
        }

        /// <summary>
        /// Gets or sets how the switch chooses its next value when activated.
        /// </summary>
        public SwitchActivationMode ActivationMode
        {
            get => activationMode;
            set
            {
                SourceGenerated.EnumValidator.Validate(value);
                SetInvalidatingField(ref activationMode, value);
            }
        }

        /// <summary>
        /// Gets or sets whether mouse and keyboard activation automatically changes the value.
        /// </summary>
        /// <remarks>
        /// Set this to <see langword="false"/> when you want to handle <see cref="Control.Click"/>
        /// yourself and decide whether the switch should change state.
        /// </remarks>
        public bool AutoToggle
        {
            get => autoToggle;
            set => SetInvalidatingField(ref autoToggle, value);
        }

        /// <summary>
        /// Gets or sets whether the user can drag the thumb.
        /// </summary>
        public bool AllowDragging
        {
            get => allowDragging;
            set => SetInvalidatingField(ref allowDragging, value);
        }

        /// <summary>
        /// Gets or sets whether dragging updates <see cref="Value"/> continuously.
        /// </summary>
        /// <remarks>
        /// When this property is <see langword="false"/>, dragging only moves the thumb visually
        /// until the user releases the pointer. The nearest value is committed on release.
        /// </remarks>
        public bool UpdateValueWhileDragging
        {
            get => updateValueWhileDragging;
            set => SetInvalidatingField(ref updateValueWhileDragging, value);
        }

        /// <summary>
        /// Gets or sets whether value changes animate the thumb and background transition.
        /// </summary>
        public bool Animate
        {
            get => animate;
            set => SetInvalidatingField(ref animate, value);
        }

        /// <summary>
        /// Gets or sets the duration, in milliseconds, of the built-in switch animation.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than zero.
        /// </exception>
        public int AnimationDuration
        {
            get => animationDuration;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(AnimationDuration), "Animation duration cannot be less than zero.");

                SetInvalidatingField(ref animationDuration, value);
            }
        }

        /// <summary>
        /// Gets or sets a multiplier applied to <see cref="AnimationDuration"/>.
        /// </summary>
        /// <value>
        /// A positive finite multiplier. The default value is 1.0. Values greater than 1 make
        /// the switch animate faster; values between 0 and 1 make it animate more slowly.
        /// </value>
        /// <remarks>
        /// This property affects future switch animations by dividing
        /// <see cref="AnimationDuration"/> by the multiplier. Changing it invalidates rendering
        /// but does not restart an animation that is already running.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than or equal to zero, NaN, or infinite.
        /// </exception>
        public double AnimationSpeed
        {
            get => animationSpeed;
            set
            {
                if (value <= 0d || !double.IsFinite(value))
                    throw new ArgumentOutOfRangeException(nameof(AnimationSpeed), "Animation speed must be a positive finite value.");

                SetInvalidatingField(ref animationSpeed, value);
            }
        }

        /// <summary>
        /// Gets or sets the easing function used by the built-in switch animation.
        /// </summary>
        /// <remarks>
        /// Set this to <see langword="null"/> to use <see cref="Easings.EaseOutCubic"/>.
        /// The function receives a normalized value from 0 to 1 and must return a normalized
        /// progress value.
        /// </remarks>
        public Func<float, float>? AnimationEasing
        {
            get => animationEasing;
            set => animationEasing = value;
        }

        /// <summary>
        /// Gets or sets the Boolean switch value.
        /// </summary>
        /// <remarks>
        /// This property maps to <see cref="Value"/> 1 when set to <see langword="true"/> and
        /// 0 when set to <see langword="false"/>. In three-position mode, a value of -1 also
        /// reports <see langword="false"/>.
        /// </remarks>
        public bool IsToggled
        {
            get => currentValue > 0;
            set => Value = value ? 1 : 0;
        }

        /// <summary>
        /// Gets or sets the current switch value.
        /// </summary>
        /// <value>
        /// 0 or 1 in <see cref="SwitchMode.TwoState"/> mode; -1, 0, or 1 in
        /// <see cref="SwitchMode.ThreeState"/> mode.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is outside the range supported by the current <see cref="Mode"/>.
        /// </exception>
        public int Value
        {
            get => currentValue;
            set
            {
                ValidateValue(value);
                SetValueCore(value, raiseEvents: true, animateChange: Animate);
            }
        }

        /// <summary>
        /// Gets or sets the track color used when <see cref="Value"/> is 0.
        /// </summary>
        public SKColor? OffTrackColor
        {
            get => offTrackColor;
            set => SetInvalidatingField(ref offTrackColor, value);
        }

        /// <summary>
        /// Gets or sets the track color used when <see cref="Value"/> is -1.
        /// </summary>
        public SKColor? NegativeTrackColor
        {
            get => negativeTrackColor;
            set => SetInvalidatingField(ref negativeTrackColor, value);
        }

        /// <summary>
        /// Gets or sets the track color used when <see cref="Value"/> is 1.
        /// </summary>
        public SKColor? OnTrackColor
        {
            get => onTrackColor;
            set => SetInvalidatingField(ref onTrackColor, value);
        }

        /// <summary>
        /// Gets or sets the brush used to paint the track when <see cref="Value"/> is 0.
        /// </summary>
        public MfnBrush? OffTrackBrush
        {
            get => offTrackBrush;
            set => SetInvalidatingField(ref offTrackBrush, value);
        }

        /// <summary>
        /// Gets or sets the brush used to paint the track when <see cref="Value"/> is -1.
        /// </summary>
        public MfnBrush? NegativeTrackBrush
        {
            get => negativeTrackBrush;
            set => SetInvalidatingField(ref negativeTrackBrush, value);
        }

        /// <summary>
        /// Gets or sets the brush used to paint the track when <see cref="Value"/> is 1.
        /// </summary>
        public MfnBrush? OnTrackBrush
        {
            get => onTrackBrush;
            set => SetInvalidatingField(ref onTrackBrush, value);
        }

        /// <summary>
        /// Gets or sets the track border color.
        /// </summary>
        public SKColor? TrackBorderColor
        {
            get => trackBorderColor;
            set => SetInvalidatingField(ref trackBorderColor, value);
        }

        /// <summary>
        /// Gets or sets the track border width in logical pixels.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than zero.
        /// </exception>
        public int TrackBorderWidth
        {
            get => trackBorderWidth;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(TrackBorderWidth), "Border width cannot be less than zero.");

                SetInvalidatingField(ref trackBorderWidth, value);
            }
        }

        /// <summary>
        /// Gets or sets the track corner radius in logical pixels.
        /// </summary>
        /// <value>
        /// -1 to use a pill-shaped automatic radius; otherwise a non-negative explicit radius.
        /// </value>
        public int TrackCornerRadius
        {
            get => trackCornerRadius;
            set
            {
                ValidateAutoOrNonNegative(value, nameof(TrackCornerRadius));
                SetInvalidatingField(ref trackCornerRadius, value);
            }
        }

        /// <summary>
        /// Gets or sets the fallback thumb color used by all switch values.
        /// </summary>
        /// <remarks>
        /// This color is used when the active value does not provide a more specific
        /// <see cref="OffThumbColor"/>, <see cref="NegativeThumbColor"/>, or
        /// <see cref="OnThumbColor"/>. Setting this property invalidates rendering only.
        /// </remarks>
        public SKColor? ThumbColor
        {
            get => thumbColor;
            set => SetInvalidatingField(ref thumbColor, value);
        }

        /// <summary>
        /// Gets or sets the thumb color used when <see cref="Value"/> is 0.
        /// </summary>
        public SKColor? OffThumbColor
        {
            get => offThumbColor;
            set => SetInvalidatingField(ref offThumbColor, value);
        }

        /// <summary>
        /// Gets or sets the thumb color used when <see cref="Value"/> is -1.
        /// </summary>
        public SKColor? NegativeThumbColor
        {
            get => negativeThumbColor;
            set => SetInvalidatingField(ref negativeThumbColor, value);
        }

        /// <summary>
        /// Gets or sets the thumb color used when <see cref="Value"/> is 1.
        /// </summary>
        public SKColor? OnThumbColor
        {
            get => onThumbColor;
            set => SetInvalidatingField(ref onThumbColor, value);
        }

        /// <summary>
        /// Gets or sets the fallback brush used to paint the thumb for all switch values.
        /// </summary>
        /// <remarks>
        /// Use this property for a single thumb gradient shared by every value. State-specific
        /// brushes, such as <see cref="OffThumbBrush"/>, <see cref="NegativeThumbBrush"/>, and
        /// <see cref="OnThumbBrush"/>, take precedence over this fallback. Setting this property
        /// invalidates rendering only.
        /// </remarks>
        /// <example>
        /// <code>
        /// var thumbGradient = new ModernFormsNext.Drawing.LinearGradientBrush
        /// {
        ///     StartPoint = new SKPoint(0, 0),
        ///     EndPoint = new SKPoint(1, 1)
        /// };
        ///
        /// thumbGradient.GradientStops.Add(new ModernFormsNext.Drawing.GradientStop(SKColors.White, 0f));
        /// thumbGradient.GradientStops.Add(new ModernFormsNext.Drawing.GradientStop(SKColors.SteelBlue, 1f));
        ///
        /// var control = new Switch
        /// {
        ///     ThumbBrush = thumbGradient
        /// };
        /// </code>
        /// </example>
        public MfnBrush? ThumbBrush
        {
            get => thumbBrush;
            set => SetInvalidatingField(ref thumbBrush, value);
        }

        /// <summary>
        /// Gets or sets the brush used to paint the thumb when <see cref="Value"/> is 0.
        /// </summary>
        public MfnBrush? OffThumbBrush
        {
            get => offThumbBrush;
            set => SetInvalidatingField(ref offThumbBrush, value);
        }

        /// <summary>
        /// Gets or sets the brush used to paint the thumb when <see cref="Value"/> is -1.
        /// </summary>
        public MfnBrush? NegativeThumbBrush
        {
            get => negativeThumbBrush;
            set => SetInvalidatingField(ref negativeThumbBrush, value);
        }

        /// <summary>
        /// Gets or sets the brush used to paint the thumb when <see cref="Value"/> is 1.
        /// </summary>
        public MfnBrush? OnThumbBrush
        {
            get => onThumbBrush;
            set => SetInvalidatingField(ref onThumbBrush, value);
        }

        /// <summary>
        /// Gets or sets the thumb border color.
        /// </summary>
        public SKColor? ThumbBorderColor
        {
            get => thumbBorderColor;
            set => SetInvalidatingField(ref thumbBorderColor, value);
        }

        /// <summary>
        /// Gets or sets the thumb border width in logical pixels.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than zero.
        /// </exception>
        public int ThumbBorderWidth
        {
            get => thumbBorderWidth;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(ThumbBorderWidth), "Border width cannot be less than zero.");

                SetInvalidatingField(ref thumbBorderWidth, value);
            }
        }

        /// <summary>
        /// Gets or sets the thumb corner radius in logical pixels.
        /// </summary>
        /// <value>
        /// -1 to use an automatic circular radius; otherwise a non-negative explicit radius.
        /// </value>
        public int ThumbCornerRadius
        {
            get => thumbCornerRadius;
            set
            {
                ValidateAutoOrNonNegative(value, nameof(ThumbCornerRadius));
                SetInvalidatingField(ref thumbCornerRadius, value);
            }
        }

        /// <summary>
        /// Gets or sets the inset between the track edge and thumb in logical pixels.
        /// </summary>
        public int ThumbInset
        {
            get => thumbInset;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(ThumbInset), "Thumb inset cannot be less than zero.");

                SetInvalidatingField(ref thumbInset, value);
            }
        }

        /// <summary>
        /// Gets or sets the thumb size in logical pixels.
        /// </summary>
        /// <value>
        /// 0 to size the thumb from the current track height; otherwise a positive explicit size.
        /// </value>
        public int ThumbSize
        {
            get => thumbSize;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(ThumbSize), "Thumb size cannot be less than zero.");

                SetInvalidatingField(ref thumbSize, value);
            }
        }

        /// <summary>
        /// Gets or sets the built-in icon drawn at the off position.
        /// </summary>
        public SwitchIconKind OffIcon
        {
            get => offIcon;
            set
            {
                SourceGenerated.EnumValidator.Validate(value);
                SetInvalidatingField(ref offIcon, value);
            }
        }

        /// <summary>
        /// Gets or sets the built-in icon drawn at the negative position.
        /// </summary>
        public SwitchIconKind NegativeIcon
        {
            get => negativeIcon;
            set
            {
                SourceGenerated.EnumValidator.Validate(value);
                SetInvalidatingField(ref negativeIcon, value);
            }
        }

        /// <summary>
        /// Gets or sets the built-in icon drawn at the on position.
        /// </summary>
        public SwitchIconKind OnIcon
        {
            get => onIcon;
            set
            {
                SourceGenerated.EnumValidator.Validate(value);
                SetInvalidatingField(ref onIcon, value);
            }
        }

        /// <summary>
        /// Gets or sets the built-in icon drawn inside the thumb.
        /// </summary>
        public SwitchIconKind ThumbIcon
        {
            get => thumbIcon;
            set
            {
                SourceGenerated.EnumValidator.Validate(value);
                SetInvalidatingField(ref thumbIcon, value);
            }
        }

        /// <summary>
        /// Gets or sets the bitmap icon drawn at the off position.
        /// </summary>
        /// <remarks>
        /// When this value is not <see langword="null"/>, it is drawn instead of
        /// <see cref="OffIcon"/>. The control stores the bitmap reference and does not take
        /// ownership of it; keep the bitmap alive while the switch can render it and dispose it
        /// after it is no longer assigned. Setting this property invalidates rendering only.
        /// </remarks>
        public SKBitmap? OffIconImage
        {
            get => offIconImage;
            set => SetInvalidatingField(ref offIconImage, value);
        }

        /// <summary>
        /// Gets or sets the bitmap icon drawn at the negative position.
        /// </summary>
        /// <remarks>
        /// When this value is not <see langword="null"/>, it is drawn instead of
        /// <see cref="NegativeIcon"/>. The control stores the bitmap reference and does not take
        /// ownership of it; keep the bitmap alive while the switch can render it and dispose it
        /// after it is no longer assigned. Setting this property invalidates rendering only.
        /// </remarks>
        public SKBitmap? NegativeIconImage
        {
            get => negativeIconImage;
            set => SetInvalidatingField(ref negativeIconImage, value);
        }

        /// <summary>
        /// Gets or sets the bitmap icon drawn at the on position.
        /// </summary>
        /// <remarks>
        /// When this value is not <see langword="null"/>, it is drawn instead of
        /// <see cref="OnIcon"/>. The control stores the bitmap reference and does not take
        /// ownership of it; keep the bitmap alive while the switch can render it and dispose it
        /// after it is no longer assigned. Setting this property invalidates rendering only.
        /// </remarks>
        public SKBitmap? OnIconImage
        {
            get => onIconImage;
            set => SetInvalidatingField(ref onIconImage, value);
        }

        /// <summary>
        /// Gets or sets the bitmap icon drawn inside the thumb.
        /// </summary>
        /// <remarks>
        /// When this value is not <see langword="null"/>, it is drawn instead of
        /// <see cref="ThumbIcon"/>. The control stores the bitmap reference and does not take
        /// ownership of it; keep the bitmap alive while the switch can render it and dispose it
        /// after it is no longer assigned. Setting this property invalidates rendering only.
        /// </remarks>
        public SKBitmap? ThumbIconImage
        {
            get => thumbIconImage;
            set => SetInvalidatingField(ref thumbIconImage, value);
        }

        /// <summary>
        /// Gets or sets the off-position icon color.
        /// </summary>
        public SKColor? OffIconColor
        {
            get => offIconColor;
            set => SetInvalidatingField(ref offIconColor, value);
        }

        /// <summary>
        /// Gets or sets the negative-position icon color.
        /// </summary>
        public SKColor? NegativeIconColor
        {
            get => negativeIconColor;
            set => SetInvalidatingField(ref negativeIconColor, value);
        }

        /// <summary>
        /// Gets or sets the on-position icon color.
        /// </summary>
        public SKColor? OnIconColor
        {
            get => onIconColor;
            set => SetInvalidatingField(ref onIconColor, value);
        }

        /// <summary>
        /// Gets or sets the thumb icon color.
        /// </summary>
        public SKColor? ThumbIconColor
        {
            get => thumbIconColor;
            set => SetInvalidatingField(ref thumbIconColor, value);
        }

        /// <summary>
        /// Gets or sets the icon size in logical pixels.
        /// </summary>
        /// <value>
        /// 0 to size icons automatically from their slot; otherwise a positive explicit size.
        /// </value>
        public int IconSize
        {
            get => iconSize;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(IconSize), "Icon size cannot be less than zero.");

                SetInvalidatingField(ref iconSize, value);
            }
        }

        /// <summary>
        /// Occurs when <see cref="IsToggled"/> changes.
        /// </summary>
        public event EventHandler<ToggledEventArgs>? Toggled;

        /// <summary>
        /// Occurs when <see cref="Value"/> changes.
        /// </summary>
        public event EventHandler<SwitchValueChangedEventArgs>? ValueChanged;

        /// <summary>
        /// Gets a value indicating whether the thumb is currently pressed.
        /// </summary>
        internal bool ThumbPressed => thumbPressed;

        /// <summary>
        /// Gets a value indicating whether the thumb is currently hovered.
        /// </summary>
        internal bool ThumbHovered => thumbHovered;

        /// <summary>
        /// Gets the animated visual position in the range from 0 to 1.
        /// </summary>
        internal float VisualPosition => visualPosition;

        private SwitchRenderer GetRenderer()
            => RenderManager.GetRenderer<SwitchRenderer>()
                ?? throw new InvalidOperationException("No SwitchRenderer has been registered.");

        private static void ValidateAutoOrNonNegative(int value, string paramName)
        {
            if (value < -1)
                throw new ArgumentOutOfRangeException(paramName, "Use -1 for automatic sizing or a value greater than or equal to zero.");
        }

        private void ValidateValue(int value)
        {
            if (value < -1 || value > 1)
                throw new ArgumentOutOfRangeException(nameof(Value), "Switch value must be -1, 0, or 1.");

            if (Mode == SwitchMode.TwoState && value < 0)
                throw new ArgumentOutOfRangeException(nameof(Value), "Two-state switches support only values 0 and 1.");
        }

        private bool SetInvalidatingField<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            Invalidate();
            return true;
        }

        private int GetMinimumValue() => Mode == SwitchMode.ThreeState ? -1 : 0;

        private int CoerceValue(int value)
        {
            if (value > 1)
                return 1;

            if (value < GetMinimumValue())
                return GetMinimumValue();

            return value;
        }

        private float GetTargetPosition(int value)
        {
            if (Mode == SwitchMode.ThreeState)
                return (value + 1) / 2f;

            return value > 0 ? 1f : 0f;
        }

        private void SetVisualPosition(float position)
        {
            visualPosition = Math.Clamp(position, 0f, 1f);
            Invalidate();
        }

        private void AnimateVisualToValue(int value, bool animateChange)
        {
            var target = GetTargetPosition(value);
            var effectiveDuration = GetEffectiveAnimationDuration();

            if (!animateChange || effectiveDuration == 0) {
                AnimationManager.Cancel(this, VisualPositionAnimationKey);
                SetVisualPosition(target);
                return;
            }

            var animation = new Animation(
                this,
                VisualPositionAnimationKey,
                visualPosition,
                target,
                effectiveDuration,
                SetVisualPosition,
                AnimationEasing ?? Easings.EaseOutCubic);

            _ = AnimationManager.AddOrReplace(animation);
        }

        private int GetEffectiveAnimationDuration()
        {
            if (AnimationDuration == 0)
                return 0;

            return Math.Max(1, (int)Math.Round(AnimationDuration / AnimationSpeed));
        }

        private int GetNextCycleValue()
        {
            if (Mode == SwitchMode.TwoState)
                return currentValue > 0 ? 0 : 1;

            return currentValue switch
            {
                -1 => 0,
                0 => 1,
                _ => -1
            };
        }

        private int GetPreviousValue()
        {
            if (Mode == SwitchMode.TwoState)
                return 0;

            return CoerceValue(currentValue - 1);
        }

        private int GetNextValue()
        {
            if (Mode == SwitchMode.TwoState)
                return 1;

            return CoerceValue(currentValue + 1);
        }

        private void Activate(Point location, bool hasPointer)
        {
            if (!AutoToggle)
                return;

            var behavior = ActivationMode;

            if (behavior == SwitchActivationMode.Automatic) {
                behavior = Mode == SwitchMode.ThreeState && hasPointer
                    ? SwitchActivationMode.SetByPointerPosition
                    : SwitchActivationMode.Toggle;
            }

            if (behavior == SwitchActivationMode.SetByPointerPosition && hasPointer) {
                SetValueCore(GetRenderer().PositionToValue(this, location), raiseEvents: true, animateChange: Animate);
                return;
            }

            if (behavior == SwitchActivationMode.Cycle) {
                SetValueCore(GetNextCycleValue(), raiseEvents: true, animateChange: Animate);
                return;
            }

            SetValueCore(currentValue > 0 ? 0 : 1, raiseEvents: true, animateChange: Animate);
        }

        private void SetValueCore(int value, bool raiseEvents, bool animateChange)
        {
            value = CoerceValue(value);

            if (currentValue == value) {
                AnimateVisualToValue(currentValue, animateChange);
                return;
            }

            var oldValue = currentValue;
            var oldIsToggled = IsToggled;
            currentValue = value;

            AnimateVisualToValue(currentValue, animateChange);

            if (!raiseEvents)
                return;

            OnValueChanged(new SwitchValueChangedEventArgs(oldValue, currentValue));

            if (oldIsToggled != IsToggled)
                OnToggled(new ToggledEventArgs(IsToggled));
        }

        /// <summary>
        /// Toggles the switch between the off and on values.
        /// </summary>
        /// <remarks>
        /// In three-position mode this method skips the neutral value. Use <see cref="Value"/> or
        /// <see cref="ActivationMode"/> when neutral should be part of user interaction.
        /// </remarks>
        public void Toggle() => SetValueCore(currentValue > 0 ? 0 : 1, raiseEvents: true, animateChange: Animate);

        /// <inheritdoc/>
        protected override void OnClick(MouseEventArgs e)
        {
            if (suppressNextClick) {
                suppressNextClick = false;
                base.OnClick(e);
                return;
            }

            Activate(e.Location, hasPointer: true);
            base.OnClick(e);
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode) {
                case Keys.Space:
                case Keys.Enter:
                    Activate(Point.Empty, hasPointer: false);
                    e.Handled = true;
                    return;

                case Keys.Left:
                case Keys.Down:
                    SetValueCore(GetPreviousValue(), raiseEvents: true, animateChange: Animate);
                    e.Handled = true;
                    return;

                case Keys.Right:
                case Keys.Up:
                    SetValueCore(GetNextValue(), raiseEvents: true, animateChange: Animate);
                    e.Handled = true;
                    return;

                case Keys.Home:
                    SetValueCore(GetMinimumValue(), raiseEvents: true, animateChange: Animate);
                    e.Handled = true;
                    return;

                case Keys.End:
                    SetValueCore(1, raiseEvents: true, animateChange: Animate);
                    e.Handled = true;
                    return;

                case Keys.D0:
                case Keys.NumPad0:
                    SetValueCore(0, raiseEvents: true, animateChange: Animate);
                    e.Handled = true;
                    return;

                case Keys.D1:
                case Keys.NumPad1:
                    SetValueCore(1, raiseEvents: true, animateChange: Animate);
                    e.Handled = true;
                    return;

                case Keys.OemMinus:
                case Keys.Subtract:
                    if (Mode == SwitchMode.ThreeState) {
                        SetValueCore(-1, raiseEvents: true, animateChange: Animate);
                        e.Handled = true;
                        return;
                    }

                    break;
            }

            base.OnKeyDown(e);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!Enabled || !e.Button.HasFlag(MouseButtons.Left))
                return;

            thumbPressed = true;
            dragging = false;
            dragStartLocation = e.Location;
            NotifyAccessibilityClients(AccessibleEvents.StateChange);
            Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (thumbHovered && !thumbPressed) {
                thumbHovered = false;
                Invalidate();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var thumbBounds = GetRenderer().GetThumbBounds(this);
            var newHover = thumbBounds.Contains(e.Location);

            if (thumbHovered != newHover) {
                thumbHovered = newHover;
                Invalidate();
            }

            if (!thumbPressed || !AllowDragging)
                return;

            var distance = Math.Abs(e.X - dragStartLocation.X) + Math.Abs(e.Y - dragStartLocation.Y);

            if (!dragging && distance < DragThreshold)
                return;

            dragging = true;
            var renderer = GetRenderer();
            var dragPosition = renderer.PositionToVisualPosition(this, e.Location);
            SetVisualPosition(dragPosition);

            if (UpdateValueWhileDragging) {
                SetValueCore(renderer.VisualPositionToValue(this, dragPosition), raiseEvents: true, animateChange: false);
                SetVisualPosition(dragPosition);
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (!thumbPressed)
                return;

            thumbPressed = false;
            NotifyAccessibilityClients(AccessibleEvents.StateChange);

            if (dragging) {
                dragging = false;
                suppressNextClick = true;

                if (!UpdateValueWhileDragging)
                    SetValueCore(GetRenderer().VisualPositionToValue(this, visualPosition), raiseEvents: true, animateChange: Animate);
                else
                    AnimateVisualToValue(currentValue, Animate);

                return;
            }

            Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RenderManager.Render(this, e);
        }

        /// <summary>
        /// Raises the <see cref="Toggled"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnToggled(ToggledEventArgs e)
        {
            Toggled?.Invoke(this, e);
            NotifyAccessibilityClients(AccessibleEvents.StateChange);
        }

        /// <summary>
        /// Raises the <see cref="ValueChanged"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnValueChanged(SwitchValueChangedEventArgs e)
        {
            ValueChanged?.Invoke(this, e);
            NotifyAccessibilityClients(AccessibleEvents.ValueChange);
        }
    }
}
