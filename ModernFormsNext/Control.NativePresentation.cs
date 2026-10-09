using System.Drawing;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly object s_nativePresentationChanged = new();
    private static readonly int s_nativeInvalidationState = PropertyStore.CreateKey();
    private bool hasNativePresentationObservers;

    // Component's existing sparse event list stores subscriptions only on paths containing
    // native hosts. Ordinary controls allocate no registry, session or per-paint state.
    internal event EventHandler NativePresentationChanged
    {
        add { Events.AddHandler(s_nativePresentationChanged, value); hasNativePresentationObservers = true; }
        remove
        {
            Events.RemoveHandler(s_nativePresentationChanged, value);
            hasNativePresentationObservers = Events[s_nativePresentationChanged] is not null;
            if (!hasNativePresentationObservers) Properties.RemoveObject(s_nativeInvalidationState);
        }
    }

    internal void NotifyNativePresentationChanged()
    {
        // Do not materialize Component.Events on a tree with no native-host subscribers.
        if (hasNativePresentationObservers)
            (Events[s_nativePresentationChanged] as EventHandler)?.Invoke(this, EventArgs.Empty);
    }

    internal void NotifyNativePresentationInvalidated()
    {
        if (!hasNativePresentationObservers) return;
        // Bounds, transforms and lifecycle already have explicit notifications. Invalidate
        // additionally observes mutable style/effect objects. Retain their relevant values
        // only on subscribed paths: an ordinary repaint must not project geometry, query
        // the provider or walk the host ancestry again. Boxing occurs only when state changes.
        // Read the virtual client rectangle so derived viewport controls retain their existing
        // invalidation contract as well as mutable border widths on ordinary controls.
        var state = new NativeInvalidationState(ClientRectangle, HasUnsupportedNativeComposition);
        if (Properties.GetObject(s_nativeInvalidationState) is NativeInvalidationState previous && previous == state) return;
        Properties.SetObject(s_nativeInvalidationState, state);
        NotifyNativePresentationChanged();
    }

    private readonly record struct NativeInvalidationState(Rectangle Client, bool Unsupported);

    internal bool HasUnsupportedNativeComposition =>
        !float.IsFinite(EffectiveOpacity) || EffectiveOpacity < 0.999f ||
        !float.IsFinite(EffectiveRotation) || Math.Abs(EffectiveRotation) > 0.0001f ||
        !float.IsFinite(EffectiveScaleX) || Math.Abs(EffectiveScaleX - 1f) > 0.0001f ||
        !float.IsFinite(EffectiveScaleY) || Math.Abs(EffectiveScaleY - 1f) > 0.0001f ||
        !float.IsFinite(EffectiveTranslationX) || !float.IsFinite(EffectiveTranslationY) ||
        HasDistinctPresentationSize || CurrentStyle.Border.GetRadius() > 0 ||
        Properties.GetObject(s_interactionEffectsProperty) is Animations.InteractionEffectCollection { Count: > 0 };
}
