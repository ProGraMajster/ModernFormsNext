using System.ComponentModel;

namespace ModernFormsNext.Animations;

/// <summary>
/// Defines attachable input and rendering behavior driven by the shared animation scheduler.
/// </summary>
/// <remarks>
/// Effects are attached through <see cref="Control.InteractionEffects"/>. They receive input
/// directly from the control pipeline rather than installing duplicate public event
/// subscriptions. Detach and disposal cancel only animations owned by this effect.
/// </remarks>
public abstract class InteractionEffect : IDisposable
{
    private bool enabled = true;
    private Control? target;
    private InteractionEffectRenderContext? renderContext;
    private bool disposed;

    /// <summary>Gets or sets whether this effect handles input and renders.</summary>
    [DefaultValue(true)]
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value)
                return;
            enabled = value;
            if (!value)
                CancelCore();
            target?.Invalidate();
        }
    }

    /// <summary>Gets the attached target, or null while detached.</summary>
    [Browsable(false)]
    public Control? Target => target;

    /// <summary>Gets or sets the effect clip. The default respects bounds and corner radius.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IInteractionEffectClip? Clip { get; set; } = ControlBoundsInteractionEffectClip.Instance;

    /// <summary>Gets the shared layer on which this effect renders.</summary>
    public virtual InteractionEffectLayer RenderLayer
        => InteractionEffectLayer.AboveContent;

    /// <summary>Disposes and detaches this effect. The operation is idempotent.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        target?.InteractionEffects.Remove(this);
        DetachCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>Called after the effect is attached to a control.</summary>
    protected virtual void OnAttached()
    {
    }

    /// <summary>Called before the effect releases its target.</summary>
    protected virtual void OnDetached()
    {
    }

    /// <summary>Called for target-local pointer down input.</summary>
    protected virtual void OnPointerDown(MouseEventArgs e)
    {
    }

    /// <summary>Called for target-local pointer up input.</summary>
    protected virtual void OnPointerUp(MouseEventArgs e)
    {
    }

    /// <summary>Called when one pointer or the entire active pointer sequence is canceled.</summary>
    /// <param name="pointerId">
    /// The canceled platform pointer identifier, or <see langword="null"/> for global cancellation.
    /// </param>
    protected virtual void OnPointerCanceled(int? pointerId)
    {
    }

    /// <summary>Called for target key down input.</summary>
    protected virtual void OnKeyDown(KeyEventArgs e)
    {
    }

    /// <summary>Called for target key up input.</summary>
    protected virtual void OnKeyUp(KeyEventArgs e)
    {
    }

    /// <summary>Renders one effect frame inside the configured clip.</summary>
    protected virtual void OnRender(InteractionEffectRenderContext context)
    {
    }

    /// <summary>Cancels effect-owned scheduler work and resets transient state.</summary>
    protected virtual void CancelCore()
    {
        if (target is not null)
            target.CancelInteractionAnimations(this);
    }

    /// <summary>Requests visual-only invalidation of the attached target.</summary>
    protected void InvalidateTarget() => target?.Invalidate();

    /// <summary>Gets the shared scheduler selected by the attached control.</summary>
    protected AnimationScheduler Scheduler
        => target?.InteractionAnimationScheduler
            ?? throw new InvalidOperationException("The interaction effect is not attached.");

    internal void Attach(Control value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (ReferenceEquals(target, value))
            return;
        if (target is not null)
            throw new InvalidOperationException("An interaction effect can be attached to only one control.");
        target = value;
        OnAttached();
    }

    internal void Detach()
    {
        if (target is null)
            return;
        DetachCore();
    }

    internal void DispatchPointerDown(MouseEventArgs e)
    {
        if (enabled && target?.Enabled == true)
            OnPointerDown(e);
    }

    internal void DispatchPointerUp(MouseEventArgs e)
    {
        if (enabled)
            OnPointerUp(e);
    }

    internal void DispatchPointerCanceled(int? pointerId)
    {
        if (enabled)
            OnPointerCanceled(pointerId);
    }

    internal void DispatchKeyDown(KeyEventArgs e)
    {
        if (enabled && target?.Enabled == true)
            OnKeyDown(e);
    }

    internal void DispatchKeyUp(KeyEventArgs e)
    {
        if (enabled)
            OnKeyUp(e);
    }

    internal void DispatchRender(PaintEventArgs e)
    {
        if (!enabled || target is null)
            return;
        e.Canvas.Save();
        try
        {
            var bounds = new System.Drawing.Rectangle(0, 0, target.ScaledWidth, target.ScaledHeight);
            Clip?.Apply(e.Canvas, target, bounds);
            if (renderContext is null)
                renderContext = new InteractionEffectRenderContext(target, e.Canvas, bounds, e.Scaling);
            else
                renderContext.Reset(target, e.Canvas, bounds, e.Scaling);
            OnRender(renderContext);
        }
        finally
        {
            e.Canvas.Restore();
        }
    }

    private void DetachCore()
    {
        CancelCore();
        OnDetached();
        target = null;
        renderContext = null;
    }
}
