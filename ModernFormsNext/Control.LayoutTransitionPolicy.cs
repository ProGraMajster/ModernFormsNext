using ModernFormsNext.Animations;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly int s_inheritedLayoutTransitionProperty = PropertyStore.CreateKey();

    private LayoutTransition? InheritedLayoutTransition => GetState(States.HasInheritedLayoutTransition)
        ? (LayoutTransition?)Properties.GetObject(s_inheritedLayoutTransitionProperty) : null;

    private LayoutTransition? EffectiveLayoutTransition => LayoutTransition ?? InheritedLayoutTransition;

    private LayoutTransition? DescendantLayoutTransition => LayoutTransition is { ApplyToDescendants: true } local
        ? local : InheritedLayoutTransition;

    /// <summary>Reconciles cached policy through the canonical control tree, outside paint and ticks.</summary>
    internal void RefreshInheritedLayoutTransitionForSubtree(
        LayoutTransition? changedConfiguration = null, bool localConfigurationChanged = false)
    {
        List<Exception>? failures = null;
        RefreshInheritedLayoutTransitionCore(changedConfiguration, localConfigurationChanged, ref failures);
        if (failures is { Count: 1 })
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is { Count: > 1 })
            throw new AggregateException("Layout transition policy callbacks failed.", failures);
    }

    private void RefreshInheritedLayoutTransitionCore(
        LayoutTransition? changedConfiguration, bool localConfigurationChanged, ref List<Exception>? failures)
    {
        if (IsDisposed || Disposing) return;
        LayoutTransition? previous = InheritedLayoutTransition;
        LayoutTransition? inherited = Parent?.DescendantLayoutTransition;
        bool inheritedChanged = !ReferenceEquals(previous, inherited);
        if (inheritedChanged)
        {
            Properties.SetObject(s_inheritedLayoutTransitionProperty, inherited);
            SetState(States.HasInheritedLayoutTransition, inherited is not null);
        }

        bool propagate = localConfigurationChanged || inheritedChanged || changedConfiguration is not null &&
            (ReferenceEquals(changedConfiguration, inherited) || ReferenceEquals(changedConfiguration, LayoutTransition));
        // Do not materialize a child collection or walk descendants in the default/no-policy path.
        // Snapshot existing children before invalidation callbacks, including their route identity.
        var children = propagate && Properties.GetObject(s_controlsCollectionProperty) is ControlCollection collection
            ? collection.GetAllControls().Select(child => (Child: child, Version: child.parentAssignmentVersion)).ToArray()
            : null;
        try { RefreshActiveLayoutTransitionPolicy(); }
        catch (Exception failure) { (failures ??= []).Add(failure); }

        if (children is null || IsDisposed || Disposing) return;
        foreach (var (child, version) in children)
        {
            if (ReferenceEquals(child.Parent, this) && child.parentAssignmentVersion == version)
                child.RefreshInheritedLayoutTransitionCore(changedConfiguration, false, ref failures);
        }
    }

    private void RefreshActiveLayoutTransitionPolicy()
    {
        if (Properties.GetObject(s_layoutPresentationStateProperty) is not LayoutPresentationState state) return;
        LayoutTransition? transition = EffectiveLayoutTransition;
        if (!CanAnimateLayout(transition, state.Current, Bounds))
        {
            ResetLayoutPresentationBounds(cancelScheduledAnimation: true);
            return;
        }
        // A shared object can notify several local owners. Only a changed winner or timing
        // configuration retargets an entry; repeated visits do not restart its duration.
        if (ReferenceEquals(state.Configuration, transition) && state.Duration == transition!.Duration &&
            state.Easing == transition.Easing) return;
        BeginLayoutPresentationTransition(state.Current, Bounds, transition!);
    }

    private void ClearInheritedLayoutTransition()
    {
        if (!GetState(States.HasInheritedLayoutTransition)) return;
        Properties.SetObject(s_inheritedLayoutTransitionProperty, null);
        SetState(States.HasInheritedLayoutTransition, false);
    }
}
