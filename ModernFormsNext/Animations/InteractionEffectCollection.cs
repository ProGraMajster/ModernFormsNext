using System.Collections;

namespace ModernFormsNext.Animations;

/// <summary>Owns and attaches the interaction effects configured for one control.</summary>
public sealed class InteractionEffectCollection : IList<InteractionEffect>
{
    private readonly Control owner;
    private readonly List<InteractionEffect> effects = [];

    internal InteractionEffectCollection(Control owner)
        => this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

    /// <inheritdoc/>
    public int Count => effects.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public InteractionEffect this[int index]
    {
        get => effects[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            InteractionEffect previous = effects[index];
            if (ReferenceEquals(previous, value))
                return;
            if (effects.Contains(value))
                throw new ArgumentException("The effect is already attached to this control.", nameof(value));
            value.Attach(owner);
            effects[index] = value;
            previous.Detach();
            owner.NotifyInteractionEffectDetached(previous);
            owner.Invalidate();
        }
    }

    /// <inheritdoc/>
    public void Add(InteractionEffect item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (effects.Contains(item))
            return;
        item.Attach(owner);
        effects.Add(item);
        owner.Invalidate();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        foreach (InteractionEffect effect in effects)
        {
            effect.Detach();
            owner.NotifyInteractionEffectDetached(effect);
        }
        effects.Clear();
        owner.Invalidate();
    }

    /// <inheritdoc/>
    public bool Contains(InteractionEffect item) => effects.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(InteractionEffect[] array, int arrayIndex) => effects.CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public IEnumerator<InteractionEffect> GetEnumerator() => effects.GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(InteractionEffect item) => effects.IndexOf(item);

    /// <inheritdoc/>
    public void Insert(int index, InteractionEffect item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (effects.Contains(item))
            return;
        item.Attach(owner);
        effects.Insert(index, item);
        owner.Invalidate();
    }

    /// <inheritdoc/>
    public bool Remove(InteractionEffect item)
    {
        if (!effects.Remove(item))
            return false;
        item.Detach();
        owner.NotifyInteractionEffectDetached(item);
        owner.Invalidate();
        return true;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        InteractionEffect effect = effects[index];
        effects.RemoveAt(index);
        effect.Detach();
        owner.NotifyInteractionEffectDetached(effect);
        owner.Invalidate();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal void PointerDown(MouseEventArgs e)
    {
        foreach (InteractionEffect effect in effects)
            effect.DispatchPointerDown(e);
    }

    internal void PointerUp(MouseEventArgs e)
    {
        foreach (InteractionEffect effect in effects)
            effect.DispatchPointerUp(e);
    }

    internal void PointerCanceled(int? pointerId)
    {
        foreach (InteractionEffect effect in effects)
            effect.DispatchPointerCanceled(pointerId);
    }

    internal void KeyDown(KeyEventArgs e)
    {
        foreach (InteractionEffect effect in effects)
            effect.DispatchKeyDown(e);
    }

    internal void KeyUp(KeyEventArgs e)
    {
        foreach (InteractionEffect effect in effects)
            effect.DispatchKeyUp(e);
    }

    internal void Render(InteractionEffectLayer layer, PaintEventArgs e)
    {
        foreach (InteractionEffect effect in effects)
        {
            if (effect.RenderLayer == layer)
                effect.DispatchRender(e);
        }
    }
}
