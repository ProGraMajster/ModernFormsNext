using System.ComponentModel;
using System.Runtime.ExceptionServices;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly int s_rightToLeftProperty = PropertyStore.CreateKey();
    private static readonly int s_notifiedRightToLeftProperty = PropertyStore.CreateKey();
    private static readonly int s_rightToLeftRevisionProperty = PropertyStore.CreateKey();
    private static readonly object s_rightToLeftChangedEvent = new();

    private RightToLeft LocalRightToLeft => (RightToLeft)Properties.GetInteger(
        s_rightToLeftProperty, (int)RightToLeft.Inherit);

    /// <summary>Gets the effective layout reading direction, or sets its local selection.</summary>
    /// <remarks>
    /// The local default is <see cref="ModernFormsNext.RightToLeft.Inherit"/>. The getter resolves
    /// through the existing parent tree and returns No at a root. Explicit No or Yes stops
    /// inheritance. An effective change performs layout, invalidates painting, and notifies
    /// inheriting descendants. Change this property on the owning UI thread. FlowLayoutPanel
    /// composes it with FlowDirection; TableLayoutPanel keeps logical cell indexes unchanged.
    /// Ordinary Dock/Anchor in other containers, culture and authored text are not rewritten.
    /// </remarks>
    /// <example><code>
    /// form.Controls.Add(new FlowLayoutPanel { RightToLeft = RightToLeft.Yes });
    /// // A nested container can opt out with RightToLeft.No.
    /// </code></example>
    /// <exception cref="InvalidEnumArgumentException">The selection is not No, Yes or Inherit.</exception>
    [AmbientValue(RightToLeft.Inherit)]
    [Localizable(true)]
    public virtual RightToLeft RightToLeft
    {
        get
        {
            RightToLeft local = LocalRightToLeft;
            return local == RightToLeft.Inherit ? Parent?.RightToLeft ?? RightToLeft.No : local;
        }
        set
        {
            if (value is not (RightToLeft.No or RightToLeft.Yes or RightToLeft.Inherit))
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(RightToLeft));
            if (LocalRightToLeft == value) return;
            Properties.AddOrRemoveValue(s_rightToLeftProperty, value, RightToLeft.Inherit);
            Properties.SetInteger(s_rightToLeftRevisionProperty,
                unchecked(Properties.GetInteger(s_rightToLeftRevisionProperty) + 1));
            RefreshInheritedRightToLeftForSubtree();
        }
    }

    /// <summary>Occurs after this control's effective layout reading direction changes.</summary>
    /// <remarks>
    /// Own layout and repaint invalidation precede the event. Descendant notifications follow
    /// through the canonical tree. A reentrant selection owns the newer state; observer failures
    /// propagate after other still-current descendants have been reconciled.
    /// </remarks>
    public event EventHandler? RightToLeftChanged
    {
        add => Events.AddHandler(s_rightToLeftChangedEvent, value);
        remove => Events.RemoveHandler(s_rightToLeftChangedEvent, value);
    }

    /// <summary>Updates layout and painting and raises <see cref="RightToLeftChanged"/>.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>Runs synchronously on the owning UI thread. Overrides must call base.</remarks>
    protected virtual void OnRightToLeftChanged(EventArgs e)
    {
        int revision = Properties.GetInteger(s_rightToLeftRevisionProperty);
        PerformLayout(this, nameof(RightToLeft));
        if (IsDisposed || Disposing || revision != Properties.GetInteger(s_rightToLeftRevisionProperty)) return;
        Invalidate();
        if (IsDisposed || Disposing || revision != Properties.GetInteger(s_rightToLeftRevisionProperty)) return;
        (Events[s_rightToLeftChangedEvent] as EventHandler)?.Invoke(this, e);
    }

    internal bool ShouldSerializeRightToLeft() => LocalRightToLeft != RightToLeft.Inherit;
    private void ResetRightToLeft() => RightToLeft = RightToLeft.Inherit;

    internal void RefreshInheritedRightToLeftForSubtree()
    {
        List<Exception>? failures = null;
        RefreshRightToLeftCore(ref failures);
        if (failures is { Count: 1 }) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is { Count: > 1 }) throw new AggregateException("Layout direction observers failed.", failures);
    }

    private void RefreshRightToLeftCore(ref List<Exception>? failures)
    {
        if (IsDisposed || Disposing) return;
        RightToLeft current = RightToLeft;
        bool changed = Properties.GetInteger(s_notifiedRightToLeftProperty) != (int)current;
        // The ordinary default tree has no pending direction work. Avoid materializing or
        // walking its children on every admission/recycle; a previously notified owner keeps
        // a revision so a reentrant same-direction reparent still finishes pending descendants.
        if (!changed && !Properties.ContainsInteger(s_rightToLeftRevisionProperty)) return;
        if (changed)
        {
            // Remember the last reconciled direction, not a second effective-value tree.
            // A parent observer can set a child's local selection before that child's turn.
            Properties.AddOrRemoveValue(s_notifiedRightToLeftProperty, (int)current, 0);
            Properties.SetInteger(s_rightToLeftRevisionProperty,
                unchecked(Properties.GetInteger(s_rightToLeftRevisionProperty) + 1));
        }
        int revision = Properties.GetInteger(s_rightToLeftRevisionProperty);
        int parentVersion = parentAssignmentVersion;
        var children = Properties.GetObject(s_controlsCollectionProperty) is ControlCollection collection
            ? collection.GetAllControls().Select(child => (Child: child, Version: child.parentAssignmentVersion)).ToArray()
            : null;
        if (changed)
        {
            try { OnRightToLeftChanged(EventArgs.Empty); }
            catch (Exception failure) { (failures ??= []).Add(failure); }
        }
        if (children is null) return;
        foreach (var (child, version) in children)
        {
            if (IsDisposed || Disposing || revision != Properties.GetInteger(s_rightToLeftRevisionProperty)
                || parentVersion != parentAssignmentVersion || RightToLeft != current) break;
            if (ReferenceEquals(child.Parent, this) && child.parentAssignmentVersion == version)
                child.RefreshRightToLeftCore(ref failures);
        }
    }
}
