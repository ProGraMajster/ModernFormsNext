using System.ComponentModel;
using ModernFormsNext.Animations;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

/// <summary>Provides attachable scheduler-backed interaction effects for controls.</summary>
public partial class Control
{
    private static readonly int s_interactionEffectsProperty = PropertyStore.CreateKey();
    private static readonly int s_rippleEffectProperty = PropertyStore.CreateKey();
    private static readonly int s_pressEffectProperty = PropertyStore.CreateKey();
    private static readonly int s_interactionScalesProperty = PropertyStore.CreateKey();
    private float interactionScale = 1f;
    private int interactionKeyDownRouteDepth;
    private int interactionKeyDownNotifiedDepth;
    private int interactionKeyUpRouteDepth;
    private int interactionKeyUpNotifiedDepth;

    /// <summary>Gets the effects attached to this control.</summary>
    /// <remarks>
    /// Effects are code-first objects that can contain easing delegates and runtime state, so the
    /// collection is intentionally hidden from ordinary designer serialization.
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public InteractionEffectCollection InteractionEffects
    {
        get
        {
            if (Properties.GetObject(s_interactionEffectsProperty) is InteractionEffectCollection existing)
                return existing;
            var created = new InteractionEffectCollection(this);
            Properties.SetObject(s_interactionEffectsProperty, created);
            return created;
        }
    }

    /// <summary>Gets or sets the convenience ripple effect attached to this control.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public RippleEffect? Ripple
    {
        get => Properties.GetObject(s_rippleEffectProperty) as RippleEffect;
        set => SetConvenienceEffect(s_rippleEffectProperty, value);
    }

    /// <summary>Gets or sets the convenience press-scale effect attached to this control.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public PressScaleEffect? PressEffect
    {
        get => Properties.GetObject(s_pressEffectProperty) as PressScaleEffect;
        set => SetConvenienceEffect(s_pressEffectProperty, value);
    }

    internal AnimationScheduler InteractionAnimationScheduler
        => AnimationSchedulerOverride ?? AnimationScheduler.Default;

    internal float InteractionScale => interactionScale;

    internal void SetInteractionScale(InteractionEffect owner, float value)
    {
        if (Properties.GetObject(s_interactionScalesProperty) is not Dictionary<InteractionEffect, float> scales)
        {
            scales = new Dictionary<InteractionEffect, float>(ReferenceEqualityComparer.Instance);
            Properties.SetObject(s_interactionScalesProperty, scales);
        }
        scales[owner] = value;
        RecalculateInteractionScale(scales);
        Invalidate();
    }

    internal void RemoveInteractionScale(InteractionEffect owner)
    {
        if (Properties.GetObject(s_interactionScalesProperty) is not Dictionary<InteractionEffect, float> scales)
            return;
        scales.Remove(owner);
        RecalculateInteractionScale(scales);
        Invalidate();
    }

    internal void CancelInteractionAnimations(InteractionEffect effect)
    {
        if (AnimationSchedulerOverride is { } scheduler)
            scheduler.CancelAll(effect);
        else
            AnimationScheduler.CancelOwnedIfInitialized(effect);
    }

    internal void RenderInteractionEffects(InteractionEffectLayer layer, PaintEventArgs e)
    {
        if (Properties.GetObject(s_interactionEffectsProperty) is InteractionEffectCollection effects)
            effects.Render(layer, e);
    }

    internal void NotifyInteractionEffectDetached(InteractionEffect effect)
    {
        if (ReferenceEquals(Properties.GetObject(s_rippleEffectProperty), effect))
            Properties.RemoveObject(s_rippleEffectProperty);
        if (ReferenceEquals(Properties.GetObject(s_pressEffectProperty), effect))
            Properties.RemoveObject(s_pressEffectProperty);
    }

    internal void NotifyInteractionKeyUp(KeyEventArgs e)
    {
        if (interactionKeyUpRouteDepth > 0)
        {
            if (interactionKeyUpNotifiedDepth == interactionKeyUpRouteDepth)
                return;
            interactionKeyUpNotifiedDepth = interactionKeyUpRouteDepth;
        }

        SetKeyboardVisualPressed(false);
        if (Properties.GetObject(s_interactionEffectsProperty) is InteractionEffectCollection effects)
            effects.KeyUp(e);
    }

    private void NotifyInteractionPointerDown(MouseEventArgs e)
    {
        if (Properties.GetObject(s_interactionEffectsProperty) is InteractionEffectCollection effects)
            effects.PointerDown(e);
    }

    private void NotifyInteractionPointerUp(MouseEventArgs e)
    {
        if (Properties.GetObject(s_interactionEffectsProperty) is InteractionEffectCollection effects)
            effects.PointerUp(e);
    }

    private void NotifyInteractionPointerCanceled(int? pointerId = null)
    {
        if (Properties.GetObject(s_interactionEffectsProperty) is InteractionEffectCollection effects)
            effects.PointerCanceled(pointerId);
    }

    private void NotifyInteractionKeyDown(KeyEventArgs e)
    {
        if (interactionKeyDownRouteDepth > 0)
        {
            if (interactionKeyDownNotifiedDepth == interactionKeyDownRouteDepth)
                return;
            interactionKeyDownNotifiedDepth = interactionKeyDownRouteDepth;
        }

        if (e.KeyCode.In(Keys.Space, Keys.Enter))
            SetKeyboardVisualPressed(true);
        if (Properties.GetObject(s_interactionEffectsProperty) is InteractionEffectCollection effects)
            effects.KeyDown(e);
    }

    private void NotifyInteractionEffectsDisabled()
    {
        if (Properties.GetObject(s_interactionEffectsProperty) is InteractionEffectCollection effects)
            effects.PointerCanceled(pointerId: null);
    }

    private void DisposeInteractionEffects()
    {
        if (Properties.GetObject(s_interactionEffectsProperty) is InteractionEffectCollection effects)
            effects.Clear();
        Properties.RemoveObject(s_interactionEffectsProperty);
        Properties.RemoveObject(s_rippleEffectProperty);
        Properties.RemoveObject(s_pressEffectProperty);
        Properties.RemoveObject(s_interactionScalesProperty);
        interactionScale = 1f;
    }

    private void SetConvenienceEffect(int propertyKey, InteractionEffect? value)
    {
        InteractionEffect? previous = Properties.GetObject(propertyKey) as InteractionEffect;
        if (ReferenceEquals(previous, value))
            return;
        if (previous is not null)
            InteractionEffects.Remove(previous);
        Properties.AddOrRemoveValue(propertyKey, value);
        if (value is not null)
            InteractionEffects.Add(value);
    }

    private void RecalculateInteractionScale(Dictionary<InteractionEffect, float> scales)
    {
        float result = 1f;
        foreach (float value in scales.Values)
            result *= value;
        interactionScale = result;
    }
}
