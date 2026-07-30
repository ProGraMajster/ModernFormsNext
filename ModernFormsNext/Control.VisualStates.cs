using System.ComponentModel;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using ModernFormsNext.Layout;
using SkiaSharp;

namespace ModernFormsNext;

/// <summary>Provides visual-state styles and scheduler-backed state transitions.</summary>
public partial class Control
{
    private const string VisualStateAnimationKey = "Control.VisualState";
    private static readonly int s_stylePressedProperty = PropertyStore.CreateKey();
    private static readonly int s_styleFocusedProperty = PropertyStore.CreateKey();
    private static readonly int s_styleDisabledProperty = PropertyStore.CreateKey();
    private static readonly int s_styleTransitionsProperty = PropertyStore.CreateKey();

    private VisualState currentVisualState;
    private ControlStyle? transitionStyle;
    private bool pointerPressed;
    private HashSet<int>? pressedPointerIds;
    private bool keyboardPressed;
    private bool hasVisualFocus;
    private float visualStateOpacity = 1f;
    private float visualStateTranslationX;
    private float visualStateTranslationY;
    private float visualStateScaleX = 1f;
    private float visualStateScaleY = 1f;
    private float visualStateRotation;
    private Brush? subscribedStateBackgroundBrush;
    private Brush? subscribedStateForegroundBrush;
    private Brush? subscribedStateBorderBrush;

    internal AnimationScheduler? AnimationSchedulerOverride { get; set; }

    internal void CancelOwnedControlAnimations()
    {
        if (AnimationSchedulerOverride is { } scheduler)
            scheduler.CancelAll(this);
        else
            AnimationScheduler.CancelOwnedIfInitialized(this);
        if (transitionStyle is not null)
        {
            transitionStyle = null;
            ApplyStateTransforms(GetStyleForState(currentVisualState));
        }
    }

    /// <summary>Gets the style used while pointer or keyboard activation is held.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public virtual ControlStyle StylePressed
        => GetOrCreateStateStyle(s_stylePressedProperty, StyleHover);

    /// <summary>Gets the style used for focused controls without hover or press.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public virtual ControlStyle StyleFocused
        => GetOrCreateStateStyle(s_styleFocusedProperty, Style);

    /// <summary>Gets the style used while the effective control is disabled.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public virtual ControlStyle StyleDisabled
        => GetOrCreateStateStyle(s_styleDisabledProperty, Style);

    /// <summary>Gets directional visual-state transitions for this control.</summary>
    /// <remarks>
    /// <para>
    /// Each control starts with its own empty collection. Without a matching explicitly added
    /// transition, state changes apply the target style immediately and only invalidate rendering.
    /// Container and data-surface controls do not enter the Pressed state merely because they
    /// receive pointer input; they must opt in through a pressed style or transition.
    /// </para>
    /// <para>
    /// Delegate-valued easing functions are code-first configuration, so the collection is hidden
    /// from ordinary designer serialization. Design-time playback completes immediately through
    /// the existing scheduler policy and does not create a separate timer.
    /// </para>
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public VisualStateTransitionCollection StyleTransitions
    {
        get
        {
            if (Properties.GetObject(s_styleTransitionsProperty) is VisualStateTransitionCollection existing)
                return existing;
            var created = new VisualStateTransitionCollection();
            Properties.SetObject(s_styleTransitionsProperty, created);
            return created;
        }
    }

    /// <summary>Gets the state after Disabled/Pressed/Hover/Focused priority resolution.</summary>
    [Browsable(false)]
    public VisualState VisualState => ResolveVisualState();

    internal float EffectiveOpacity => Math.Clamp(Opacity * GetVisualOpacity(), 0f, 1f);
    internal float EffectiveTranslationX => TranslationX + GetVisualTranslationX();
    internal float EffectiveTranslationY => TranslationY + GetVisualTranslationY();
    internal float EffectiveScaleX => ScaleX * GetVisualScaleX() * InteractionScale;
    internal float EffectiveScaleY => ScaleY * GetVisualScaleY() * InteractionScale;
    internal float EffectiveRotation => Rotation + GetVisualRotation();
    internal Brush? EffectiveBackgroundBrush => BackgroundBrush ?? CurrentStyle.GetResolvedBackgroundBrush();
    internal Brush? EffectiveTextBrush => TextBrush ?? CurrentStyle.GetResolvedForegroundBrush();
    internal Brush? EffectiveBorderBrush => CurrentStyle.GetResolvedBorderBrush();

    internal ControlStyle ResolveCurrentStyle()
    {
        ControlStyle style = transitionStyle ?? GetStyleForState(currentVisualState);
        UpdateStateBrushInvalidationSubscriptions(style);
        return style;
    }

    internal void SetPointerVisualPressed(bool value, int pointerId = 0)
    {
        if (value && !ShouldTrackActivationVisualState())
            return;
        bool wasPressed = pointerPressed;
        if (value)
            (pressedPointerIds ??= []).Add(pointerId);
        else
            pressedPointerIds?.Remove(pointerId);
        pointerPressed = pressedPointerIds is { Count: > 0 };
        if (pointerPressed == wasPressed)
            return;
        UpdateVisualState();
    }

    internal void ClearPointerVisualPressed(int? pointerId = null)
    {
        if (!pointerPressed)
            return;
        if (pointerId is { } id)
            pressedPointerIds?.Remove(id);
        else
            pressedPointerIds?.Clear();
        pointerPressed = pressedPointerIds is { Count: > 0 };
        if (pointerPressed)
            return;
        UpdateVisualState();
    }

    internal void SetKeyboardVisualPressed(bool value)
    {
        if (value && !ShouldTrackActivationVisualState())
            return;
        if (keyboardPressed == value)
            return;
        keyboardPressed = value;
        UpdateVisualState();
    }

    internal void SetVisualFocus(bool value)
    {
        if (hasVisualFocus == value)
            return;
        hasVisualFocus = value;
        UpdateVisualState();
    }

    internal void UpdateVisualState()
    {
        VisualState targetState = ResolveVisualState();
        VisualState previousState = currentVisualState;
        if (targetState == previousState)
            return;

        bool continuesActiveTransition = transitionStyle is not null;
        ControlStyle sourceStyle = transitionStyle ?? GetStyleForState(previousState);
        ControlStyle targetStyle = GetStyleForState(targetState);
        currentVisualState = targetState;

        if (Properties.GetObject(s_styleTransitionsProperty) is not VisualStateTransitionCollection transitions ||
            !transitions.TryGet(previousState, targetState, out VisualStateTransition? transition))
        {
            if (transitionStyle is not null)
                EffectiveAnimationScheduler.Cancel(this, VisualStateAnimationKey);
            transitionStyle = null;
            ApplyStateTransforms(targetStyle);
            Invalidate();
            return;
        }

        var runtime = new VisualStateTransitionRuntime(
            sourceStyle,
            targetStyle,
            this,
            continuesActiveTransition);
        transitionStyle = runtime.AnimatedStyle;
        EffectiveAnimationScheduler.StartFrames(
            this,
            VisualStateAnimationKey,
            frame =>
            {
                runtime.Apply(frame.EasedProgress, this);
                // Easing is intentionally allowed to overshoot. Only raw timeline completion may
                // release the transient style; otherwise an early eased value >= 1 makes the
                // control jump to the target style while its animation is still active.
                if (frame.Progress >= 1f)
                    transitionStyle = null;
                Invalidate();
            },
            new AnimationOptions
            {
                Duration = transition!.Duration,
                Easing = transition.Easing,
                ReplacementMode = AnimationReplacementMode.Replace
            });
    }

    internal void RefreshVisualStateAfterThemeChange()
    {
        if (transitionStyle is not null)
            EffectiveAnimationScheduler.Cancel(this, VisualStateAnimationKey);
        transitionStyle = null;
        currentVisualState = ResolveVisualState();
        ApplyStateTransforms(GetStyleForState(currentVisualState));
    }

    private VisualState ResolveVisualState()
    {
        if (!Enabled)
            return VisualState.Disabled;
        if (pointerPressed || keyboardPressed)
            return VisualState.Pressed;
        if (IsHovering)
            return VisualState.Hover;
        if (hasVisualFocus || Focused)
            return VisualState.Focused;
        return VisualState.Normal;
    }

    private bool ShouldTrackActivationVisualState()
    {
        // Pointer routing is shared by buttons, containers, scrollbars, and data surfaces. Do not
        // turn every leaf that receives a click into a pressed visual: container state styles can
        // cover the entire viewport and look like an implicit whole-control interaction effect.
        if (GetControlBehavior(ControlBehaviors.Hoverable) ||
            Properties.GetObject(s_stylePressedProperty) is ControlStyle)
        {
            return true;
        }

        if (Properties.GetObject(s_interactionEffectsProperty) is InteractionEffectCollection effects &&
            effects.Any(static effect => effect is PressScaleEffect))
        {
            return true;
        }

        return Properties.GetObject(s_styleTransitionsProperty) is VisualStateTransitionCollection transitions &&
            transitions.Contains(VisualState.Pressed);
    }

    private AnimationScheduler EffectiveAnimationScheduler
        => AnimationSchedulerOverride ?? AnimationScheduler.Default;

    private float GetVisualOpacity()
        => transitionStyle is not null
            ? visualStateOpacity
            : Math.Clamp(GetStyleForState(currentVisualState).GetResolvedOpacity() ?? 1f, 0f, 1f);

    private float GetVisualTranslationX()
        => transitionStyle is not null
            ? visualStateTranslationX
            : GetStyleForState(currentVisualState).GetResolvedTranslationX() ?? 0f;

    private float GetVisualTranslationY()
        => transitionStyle is not null
            ? visualStateTranslationY
            : GetStyleForState(currentVisualState).GetResolvedTranslationY() ?? 0f;

    private float GetVisualScaleX()
        => transitionStyle is not null
            ? visualStateScaleX
            : GetStyleForState(currentVisualState).GetResolvedScaleX() ?? 1f;

    private float GetVisualScaleY()
        => transitionStyle is not null
            ? visualStateScaleY
            : GetStyleForState(currentVisualState).GetResolvedScaleY() ?? 1f;

    private float GetVisualRotation()
        => transitionStyle is not null
            ? visualStateRotation
            : GetStyleForState(currentVisualState).GetResolvedRotation() ?? 0f;

    private ControlStyle GetStyleForState(VisualState state)
        => state switch
        {
            VisualState.Normal => Style,
            VisualState.Hover => StyleHover,
            VisualState.Pressed => StylePressed,
            VisualState.Focused => StyleFocused,
            VisualState.Disabled => StyleDisabled,
            _ => Style
        };

    private ControlStyle GetOrCreateStateStyle(int propertyKey, ControlStyle parentStyle)
    {
        if (Properties.GetObject(propertyKey) is ControlStyle existing)
            return existing;
        var created = new ControlStyle(parentStyle);
        Properties.SetObject(propertyKey, created);
        return created;
    }

    private void ApplyStateTransforms(ControlStyle style)
    {
        visualStateOpacity = Math.Clamp(style.GetResolvedOpacity() ?? 1f, 0f, 1f);
        visualStateTranslationX = style.GetResolvedTranslationX() ?? 0f;
        visualStateTranslationY = style.GetResolvedTranslationY() ?? 0f;
        visualStateScaleX = style.GetResolvedScaleX() ?? 1f;
        visualStateScaleY = style.GetResolvedScaleY() ?? 1f;
        visualStateRotation = style.GetResolvedRotation() ?? 0f;
    }

    private void UpdateStateBrushInvalidationSubscriptions(ControlStyle style)
    {
        // State styles are plain code-first objects rather than dependency properties. Keep the
        // brushes selected by the last style resolution in the control's existing weak,
        // reference-counted subscription table so an in-place Brush mutation repaints the
        // active state without requiring another pointer or focus transition.
        ReplaceBrushInvalidationReference(
            ref subscribedStateBackgroundBrush,
            style.GetResolvedBackgroundBrush());
        ReplaceBrushInvalidationReference(
            ref subscribedStateForegroundBrush,
            style.GetResolvedForegroundBrush());
        ReplaceBrushInvalidationReference(
            ref subscribedStateBorderBrush,
            style.GetResolvedBorderBrush());
    }

    private sealed class VisualStateTransitionRuntime
    {
        private readonly VisualSnapshot from;
        private readonly VisualSnapshot to;
        private readonly IAnimationInterpolator<Brush>? backgroundInterpolator;
        private readonly IAnimationInterpolator<Brush>? foregroundInterpolator;
        private readonly IAnimationInterpolator<Brush>? borderInterpolator;

        public VisualStateTransitionRuntime(
            ControlStyle source,
            ControlStyle target,
            Control control,
            bool useCurrentTransform)
        {
            from = VisualSnapshot.Capture(source, control, useCurrentTransform);
            to = VisualSnapshot.Capture(target, control, useCurrentTransform: false);
            AnimatedStyle = new ControlStyle(target);
            backgroundInterpolator = TryCreateBrushInterpolator(from.BackgroundBrush, to.BackgroundBrush);
            foregroundInterpolator = TryCreateBrushInterpolator(from.ForegroundBrush, to.ForegroundBrush);
            borderInterpolator = TryCreateBrushInterpolator(from.BorderBrush, to.BorderBrush);
            Apply(0f, control);
        }

        public ControlStyle AnimatedStyle { get; }

        public void Apply(float progress, Control control)
        {
            AnimatedStyle.BackgroundColor = Interpolate(from.BackgroundColor, to.BackgroundColor, progress);
            AnimatedStyle.ForegroundColor = Interpolate(from.ForegroundColor, to.ForegroundColor, progress);
            AnimatedStyle.Border.Color = Interpolate(from.BorderColor, to.BorderColor, progress);
            AnimatedStyle.BackgroundBrush = InterpolateBrush(
                from.BackgroundBrush, to.BackgroundBrush, backgroundInterpolator, progress);
            AnimatedStyle.ForegroundBrush = InterpolateBrush(
                from.ForegroundBrush, to.ForegroundBrush, foregroundInterpolator, progress);
            AnimatedStyle.BorderBrush = InterpolateBrush(
                from.BorderBrush, to.BorderBrush, borderInterpolator, progress);
            control.visualStateOpacity = Lerp(from.Opacity, to.Opacity, progress);
            control.visualStateTranslationX = Lerp(from.TranslationX, to.TranslationX, progress);
            control.visualStateTranslationY = Lerp(from.TranslationY, to.TranslationY, progress);
            control.visualStateScaleX = Lerp(from.ScaleX, to.ScaleX, progress);
            control.visualStateScaleY = Lerp(from.ScaleY, to.ScaleY, progress);
            control.visualStateRotation = Lerp(from.Rotation, to.Rotation, progress);
        }

        private static IAnimationInterpolator<Brush>? TryCreateBrushInterpolator(Brush? from, Brush? to)
        {
            if (from is null || to is null || from.GetType() != to.GetType())
                return null;
            return AnimationInterpolators.CreateBrushInterpolator();
        }

        private static Brush? InterpolateBrush(
            Brush? from,
            Brush? to,
            IAnimationInterpolator<Brush>? interpolator,
            float progress)
        {
            if (interpolator is null)
                return to;
            try
            {
                return interpolator.Interpolate(from!, to!, progress);
            }
            catch (InvalidOperationException)
            {
                return to;
            }
            catch (NotSupportedException)
            {
                return to;
            }
        }

        private static SKColor Interpolate(SKColor from, SKColor to, float progress)
            => new(
                Lerp(from.Red, to.Red, progress),
                Lerp(from.Green, to.Green, progress),
                Lerp(from.Blue, to.Blue, progress),
                Lerp(from.Alpha, to.Alpha, progress));

        private static byte Lerp(byte from, byte to, float progress)
            => (byte)Math.Clamp(
                (int)MathF.Round(from + ((to - from) * progress)),
                byte.MinValue,
                byte.MaxValue);

        private static float Lerp(float from, float to, float progress)
            => from + ((to - from) * progress);
    }

    private readonly record struct VisualSnapshot(
        SKColor BackgroundColor,
        SKColor ForegroundColor,
        SKColor BorderColor,
        Brush? BackgroundBrush,
        Brush? ForegroundBrush,
        Brush? BorderBrush,
        float Opacity,
        float TranslationX,
        float TranslationY,
        float ScaleX,
        float ScaleY,
        float Rotation)
    {
        public static VisualSnapshot Capture(
            ControlStyle style,
            Control control,
            bool useCurrentTransform = true)
            => new(
                style.GetBackgroundColor(),
                style.GetForegroundColor(),
                style.Border.GetColor(),
                style.GetResolvedBackgroundBrush(),
                style.GetResolvedForegroundBrush(),
                style.GetResolvedBorderBrush(),
                useCurrentTransform ? control.GetVisualOpacity() : Math.Clamp(style.GetResolvedOpacity() ?? 1f, 0f, 1f),
                useCurrentTransform ? control.GetVisualTranslationX() : style.GetResolvedTranslationX() ?? 0f,
                useCurrentTransform ? control.GetVisualTranslationY() : style.GetResolvedTranslationY() ?? 0f,
                useCurrentTransform ? control.GetVisualScaleX() : style.GetResolvedScaleX() ?? 1f,
                useCurrentTransform ? control.GetVisualScaleY() : style.GetResolvedScaleY() ?? 1f,
                useCurrentTransform ? control.GetVisualRotation() : style.GetResolvedRotation() ?? 0f);
    }
}
