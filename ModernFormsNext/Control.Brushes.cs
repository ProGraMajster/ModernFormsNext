using System;
using System.Collections.Generic;
using ModernFormsNext.Drawing;

namespace ModernFormsNext;

public partial class Control
{
    private Dictionary<Brush, WeakBrushInvalidationSubscription>? brushInvalidationSubscriptions;

    /// <summary>
    /// Replaces a brush-valued field, updates its weak invalidation subscription, and invalidates
    /// this control when the effective reference changes.
    /// </summary>
    /// <param name="field">The brush backing field to replace.</param>
    /// <param name="value">The new brush, or <see langword="null"/> for the property's fallback.</param>
    /// <returns><see langword="true"/> when the field changed; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Derived controls should use this helper for public brush properties. Multiple properties
    /// that reference the same brush share one event handler through reference counting. The
    /// subscription keeps only a weak reference to the control, so a long-lived dynamic-resource
    /// brush cannot retain a control if disposal is missed. Brush changes request repaint only and
    /// must occur on the control's UI thread.
    /// </remarks>
    protected bool SetBrushField(ref Brush? field, Brush? value)
    {
        if (ReferenceEquals(field, value))
            return false;

        Brush? previous = field;
        field = value;
        if (previous is not null)
            ReleaseBrushInvalidation(previous);
        if (value is not null)
            AcquireBrushInvalidation(value);

        Invalidate();
        return true;
    }

    private void AcquireBrushInvalidation(Brush brush)
    {
        brushInvalidationSubscriptions ??= new Dictionary<Brush, WeakBrushInvalidationSubscription>(ReferenceEqualityComparer.Instance);
        if (brushInvalidationSubscriptions.TryGetValue(brush, out WeakBrushInvalidationSubscription? subscription))
        {
            subscription.AddReference();
            return;
        }

        brushInvalidationSubscriptions.Add(brush, new WeakBrushInvalidationSubscription(this, brush));
    }

    private void ReleaseBrushInvalidation(Brush brush)
    {
        if (brushInvalidationSubscriptions is null ||
            !brushInvalidationSubscriptions.TryGetValue(brush, out WeakBrushInvalidationSubscription? subscription) ||
            !subscription.ReleaseReference())
        {
            return;
        }

        brushInvalidationSubscriptions.Remove(brush);
        subscription.Dispose();
        if (brushInvalidationSubscriptions.Count == 0)
            brushInvalidationSubscriptions = null;
    }

    private void DisposeBrushInvalidationSubscriptions()
    {
        if (brushInvalidationSubscriptions is null)
            return;

        foreach (WeakBrushInvalidationSubscription subscription in brushInvalidationSubscriptions.Values)
            subscription.Dispose();

        brushInvalidationSubscriptions.Clear();
        brushInvalidationSubscriptions = null;
    }

    private sealed class WeakBrushInvalidationSubscription : IDisposable
    {
        private readonly WeakReference<Control> target;
        private Brush? source;
        private int referenceCount = 1;

        public WeakBrushInvalidationSubscription(Control target, Brush source)
        {
            this.target = new WeakReference<Control>(target);
            this.source = source;
            source.Changed += HandleBrushChanged;
        }

        public void AddReference() => referenceCount++;

        public bool ReleaseReference()
        {
            referenceCount--;
            return referenceCount == 0;
        }

        public void Dispose()
        {
            Brush? current = source;
            if (current is null)
                return;

            source = null;
            current.Changed -= HandleBrushChanged;
        }

        private void HandleBrushChanged(object? sender, EventArgs e)
        {
            if (target.TryGetTarget(out Control? control) && !control.disposedValue)
            {
                control.Invalidate();
                return;
            }

            Dispose();
        }
    }
}
