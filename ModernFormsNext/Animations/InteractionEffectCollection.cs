using System.Buffers;
using System.Collections;

namespace ModernFormsNext.Animations;

/// <summary>Owns and attaches the interaction effects configured for one control.</summary>
public sealed class InteractionEffectCollection : IList<InteractionEffect>
{
    private readonly Control owner;
    private readonly List<InteractionEffect> effects = [];
    private InteractionEffect[] dispatchBuffer = [];
    private int dispatchDepth;

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
        // Remove before invoking extensibility callbacks so an effect may dispose itself or
        // another effect without invalidating collection enumeration.
        while (effects.Count > 0)
        {
            InteractionEffect effect = effects[0];
            effects.RemoveAt(0);
            effect.Detach();
            owner.NotifyInteractionEffectDetached(effect);
        }
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
        => Dispatch(EffectDispatchKind.PointerDown, mouseEvent: e);

    internal void PointerUp(MouseEventArgs e)
        => Dispatch(EffectDispatchKind.PointerUp, mouseEvent: e);

    internal void PointerCanceled(int? pointerId)
        => Dispatch(EffectDispatchKind.PointerCanceled, pointerId: pointerId);

    internal void KeyDown(KeyEventArgs e)
        => Dispatch(EffectDispatchKind.KeyDown, keyEvent: e);

    internal void KeyUp(KeyEventArgs e)
        => Dispatch(EffectDispatchKind.KeyUp, keyEvent: e);

    internal void Render(InteractionEffectLayer layer, PaintEventArgs e)
        => Dispatch(EffectDispatchKind.Render, layer: layer, paintEvent: e);

    private void Dispatch(
        EffectDispatchKind kind,
        MouseEventArgs? mouseEvent = null,
        KeyEventArgs? keyEvent = null,
        int? pointerId = null,
        InteractionEffectLayer layer = default,
        PaintEventArgs? paintEvent = null)
    {
        int count = effects.Count;
        if (count == 0)
            return;

        bool rented = dispatchDepth > 0;
        InteractionEffect[] buffer;
        if (rented)
        {
            buffer = ArrayPool<InteractionEffect>.Shared.Rent(count);
        }
        else
        {
            if (dispatchBuffer.Length < count)
                Array.Resize(ref dispatchBuffer, Math.Max(count, dispatchBuffer.Length * 2));
            buffer = dispatchBuffer;
        }

        effects.CopyTo(buffer, 0);
        dispatchDepth++;
        try
        {
            for (int index = 0; index < count; index++)
            {
                InteractionEffect effect = buffer[index];
                if (!ReferenceEquals(effect.Target, owner))
                    continue;

                switch (kind)
                {
                    case EffectDispatchKind.PointerDown:
                        effect.DispatchPointerDown(mouseEvent!);
                        break;
                    case EffectDispatchKind.PointerUp:
                        effect.DispatchPointerUp(mouseEvent!);
                        break;
                    case EffectDispatchKind.PointerCanceled:
                        effect.DispatchPointerCanceled(pointerId);
                        break;
                    case EffectDispatchKind.KeyDown:
                        effect.DispatchKeyDown(keyEvent!);
                        break;
                    case EffectDispatchKind.KeyUp:
                        effect.DispatchKeyUp(keyEvent!);
                        break;
                    case EffectDispatchKind.Render:
                        if (effect.RenderLayer == layer)
                            effect.DispatchRender(paintEvent!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind));
                }
            }
        }
        finally
        {
            Array.Clear(buffer, 0, count);
            dispatchDepth--;
            if (rented)
                ArrayPool<InteractionEffect>.Shared.Return(buffer);
        }
    }

    private enum EffectDispatchKind
    {
        PointerDown,
        PointerUp,
        PointerCanceled,
        KeyDown,
        KeyUp,
        Render
    }
}
